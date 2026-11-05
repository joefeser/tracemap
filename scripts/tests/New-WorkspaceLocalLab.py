#!/usr/bin/env python3
"""Build a persistent, public-only readable wizard/SQL lab. Refuses existing roots."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-root', required=True)
    args = parser.parse_args()
    root = Path(args.output_root).resolve()
    if root.exists():
        raise RuntimeError('Existing lab preserved; choose a fresh output root')
    root.mkdir(parents=True)
    repo = Path(__file__).resolve().parents[2]
    replay = repo / 'scripts/tests/Test-WorkspaceWizardLauncher.py'
    result = subprocess.run([sys.executable, str(replay), '--output-root', str(root / 'example'),
                             '--sql-beside-workspace'], text=True, capture_output=True, timeout=360)
    (root / 'Build-Example.log').write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode:
        raise RuntimeError('Public example failed; retained Build-Example.log and example/logs')
    shutil.copytree(repo / 'artifacts/workspace-cli/Release', root / 'tool')
    shutil.copyfile(__file__, root / 'New-WorkspaceLocalLab.py')
    shutil.copyfile(replay, root / 'Test-WorkspaceWizardLauncher.py')
    cli = root / 'tool/tracemap.dll'
    workspace = root / 'example/workspace'
    source = root / 'example/web'
    head = subprocess.check_output(['git', '-C', str(source), 'rev-parse', 'HEAD'], text=True).strip()
    generator = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    limits = root / 'size-cases'
    limits.mkdir()

    def write_sql(name, byte_count, procedure):
        ending = '\nCREATE PROC dbo.' + procedure + ' AS SELECT 1 AS Id;'
        data = ('--' + 'x' * (byte_count - len(ending.encode()) - 2) + ending).encode()
        assert len(data) == byte_count
        (limits / name).write_bytes(data)
        return {'path': name, 'sha256': hashlib.sha256(data).hexdigest()}

    def manifest(name, files):
        payload = {'version': 'sqlserver-local-definitions-input/1', 'visibility': 'local-only',
                   'contextRepository': 'web', 'contextCommitSha': head,
                   'definitionSet': 'synthetic-size-case', 'server': 'ExampleServer',
                   'database': 'ExampleDatabase', 'files': files}
        bounded = hashlib.sha256(json.dumps(payload, sort_keys=True).encode()).hexdigest()
        payload['provenance'] = {'generatorSha256': generator, 'boundedInputSha256': bounded}
        path = limits / name
        path.write_text(json.dumps(payload, indent=2), encoding='utf-8')
        return path

    exact = write_sql('at-file-limit.sql', 262144, 'ReadAtLimit')
    over = write_sql('over-file-limit.sql', 262145, 'ReadOverLimit')
    aggregate = [exact] + [write_sql(f'aggregate-{i}.sql', 262144, f'ReadAggregate{i}') for i in range(1, 5)]
    cases = [('01-file-limit-pass', manifest('01-file-limit-pass.local.json', [exact]), 0, None),
             ('02-file-limit-fail', manifest('02-file-limit-fail.local.json', [over]), 1, 'input-size-limit'),
             ('03-total-limit-pass', manifest('03-total-limit-pass.local.json', aggregate[:4]), 0, None),
             ('04-total-limit-fail', manifest('04-total-limit-fail.local.json', aggregate), 1, 'ddl-input-size-limit')]
    measurements = []
    for name, path, expected, reason in cases:
        command = ['dotnet', str(cli), 'sqlserver-definitions', 'verify', '--input', str(path), '--source-root', str(limits)]
        check = subprocess.run(command, text=True, capture_output=True, timeout=60)
        text = check.stdout + check.stderr
        (limits / (name + '.log')).write_text(text, encoding='utf-8')
        if check.returncode != expected or (reason and reason not in text):
            raise RuntimeError(f'{name} unexpected result; inspect {limits / (name + ".log")}')
        measurements.append(f'- {name}: exit {check.returncode}; ' + (reason or 'verified partial SQL definitions'))
    continued = subprocess.run(['dotnet', str(cli), 'webforms-review', 'wizard', '--root', str(workspace), '--continue'],
                               input='workspace\nverify\n', text=True, capture_output=True, timeout=60)
    (root / 'Frozen-Tool-Verification.log').write_text(continued.stdout + continued.stderr, encoding='utf-8')
    if continued.returncode:
        raise RuntimeError('Copied tool could not verify the existing public workspace; logs retained')

    (root / 'Continue-Wizard.ps1').write_text('''# Continue the PUBLIC example; does not refer to work inputs.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
& dotnet (Join-Path $PSScriptRoot 'tool/tracemap.dll') webforms-review wizard --root (Join-Path $PSScriptRoot 'example/workspace') --continue
exit $LASTEXITCODE
''', encoding='utf-8')
    (root / 'Run-Size-Cases.ps1').write_text('''# These are deliberately generated PUBLIC boundary cases. No SQL is executed.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$cli = Join-Path $PSScriptRoot 'tool/tracemap.dll'
$base = Join-Path $PSScriptRoot 'size-cases'
foreach ($case in @(@('01-file-limit-pass', 0), @('02-file-limit-fail', 1), @('03-total-limit-pass', 0), @('04-total-limit-fail', 1))) {
    Write-Host "Running $($case[0]); expected exit $($case[1])"
    & dotnet $cli sqlserver-definitions verify --input (Join-Path $base ($case[0] + '.local.json')) --source-root $base
    if ($LASTEXITCODE -ne $case[1]) { throw "Unexpected result: $($case[0])" }
}
Write-Host 'All four expected boundary outcomes matched. SQL was not executed.'
''', encoding='utf-8')
    (root / 'Inspect-SqlInputs.ps1').write_text('''# Read-only size/path inspection. No hashes or SQL contents are printed.
param([Parameter(Mandatory=$true)][string]$Manifest, [string]$BaseFolder)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$manifestPath = (Resolve-Path -LiteralPath $Manifest).Path
if (!$BaseFolder) { $BaseFolder = Split-Path $manifestPath -Parent }
$base = (Resolve-Path -LiteralPath $BaseFolder).Path
$config = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifestBytes = (Get-Item -LiteralPath $manifestPath).Length
Write-Host "Manifest bytes: $manifestBytes; manifest cap: 4194304"
$ordinary = $config.version -eq 'sqlserver-local-definitions-input/1'
if (!$ordinary) { throw 'This inspector covers ordinary local definitions only; selected-definition originals have different limits.' }
[long]$total = 0
$rows = foreach ($entry in $config.files) {
    if ([IO.Path]::IsPathRooted($entry.path) -or $entry.path.Contains('\\') -or $entry.path.Split('/') -contains '..') { throw 'Manifest path is not a supported relative slash-separated SQL path.' }
    $file = Join-Path $base $entry.path
    $exists = Test-Path -LiteralPath $file -PathType Leaf
    $bytes = if ($exists) { (Get-Item -LiteralPath $file).Length } else { 0 }
    $total += $bytes
    [pscustomobject]@{ RelativePath=$entry.path; ResolvedPath=$file; Exists=$exists; Bytes=$bytes; FileLimit=262144; OverLimit=($bytes -gt 262144) }
}
$rows | Format-List
Write-Host "Total bytes: $total; total cap: 1048576; over total limit: $($total -gt 1048576)"
Write-Host 'Inspection only; scanner admission and digest validation remain separate.'
''', encoding='utf-8')
    (root / 'Rebuild-Public-Example.ps1').write_text('''# Generate another fresh PUBLIC lab on this host; never relocate saved wizard configs.
param([Parameter(Mandatory=$true)][string]$TraceMapCheckout, [Parameter(Mandatory=$true)][string]$NewOutputRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$builder = Join-Path $TraceMapCheckout 'scripts/tests/New-WorkspaceLocalLab.py'
$python = if (Get-Command python3 -ErrorAction SilentlyContinue) { 'python3' } else { 'python' }
& $python $builder --output-root $NewOutputRoot
exit $LASTEXITCODE
''', encoding='utf-8')
    run = next((workspace / 'runs').glob('workspace-*'))
    version = subprocess.check_output(['dotnet', str(cli), 'version'], text=True)
    readme = f'''# TraceMap local lab — public synthetic files only

This folder was built and exercised locally. No work inputs were used, no database
was contacted, and SQL was never executed. Static evidence remains partial.

## Open these first

- `example/workspace/root.config.json`: wizard root.
- `example/workspace/web/project.config.json`: saved website state.
- `example/workspace/web/workspace-answers.local.json`: saved workspace answers.
- `example/workspace/sql-definitions.local.json`: SQL manifest; path is `sql/schema.sql`.
- `example/workspace/sql/schema.sql`: the small working SQL file.
- `example/workspace/workspace.local.json`: verified generated workspace config.
- `{run.relative_to(root)}/START-HERE.md`: completed reports and evidence links.
- `{run.relative_to(root)}/combined.sqlite`: actual combined evidence database.
- `{run.relative_to(root)}/sql/sql-evidence.sqlite`: actual SQL sidecar.
- `example/logs/`: full wizard prompts, supplied-stage outcomes and collection logs.

The outer `example/workspace` is the configuration root. The inner `web` is only
website state. SQL/config do NOT need to move into the inner `web` directory.
Workspace creation alone does not produce SQLite; choosing collect does.

## Use it here (PowerShell 7 and .NET 10)

```powershell
pwsh -NoProfile -File ./Continue-Wizard.ps1
pwsh -NoProfile -File ./Run-Size-Cases.ps1
pwsh -NoProfile -File ./Inspect-SqlInputs.ps1 -Manifest ./example/workspace/sql-definitions.local.json
```

Continue selects the completed public wizard. Choose workspace, then verify,
collect or later. A new collect preserves existing runs and prints its new paths.
The tool copy under `tool/` is frozen so an unrelated checkout update cannot change
this demonstration. Frozen-tool continuation/verification passed locally.

## Size limits reproduced here, not guessed

Ordinary `sqlserver-local-definitions-input/1`: 262144 bytes per SQL file,
1048576 total SQL bytes, 32 files. Input manifest cap: 4194304 bytes. Selection
inputs have a different contract; this lab does not silently select or truncate SQL.

{chr(10).join(measurements)}

Each case has its manifest, actual SQL bytes, and captured result in `size-cases/`.
The long first-line comments are intentional byte padding for boundary testing.
An over-limit file is not evidence of bad folder selection. The private failure's
specific cause is NOT established by this public lab. `Inspect-SqlInputs.ps1`
can report local paths and sizes without printing SQL or hashes; nothing is sent.
Do not raise limits, truncate a real definition, or invent a hash to force admission.

## Reproduce on another machine

Generated wizard configurations contain absolute paths. Do NOT move/copy them
and expect continuation elsewhere. Keep this local example intact. On Windows,
use the tested TraceMap branch and regenerate public files in a fresh folder:

```powershell
pwsh -NoProfile -File ./Rebuild-Public-Example.ps1 -TraceMapCheckout C:/work/tracemap -NewOutputRoot C:/reviews/tracemap-public-lab
```

This touches only the chosen fresh public lab. Existing roots are refused.
Your private wizard/scans are not reset or used. `New-WorkspaceLocalLab.py` and
`Test-WorkspaceWizardLauncher.py` are copied here for reading; the regeneration
wrapper invokes their repository versions so the tool checkout is explicit.

## Exact frozen tool

```text
{version.strip()}
CLI SHA-256: {hashlib.sha256(cli.read_bytes()).hexdigest()}
```

This demonstrates a portable managed fixture, not IIS deployment, application
runtime, private acceptance, complete SQL lineage or migration readiness.
'''
    (root / 'README.md').write_text(readme, encoding='utf-8')
    print(f'PASS persistent local lab: {root / "README.md"}')
    print('\n'.join(measurements))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(f'FAIL: {error}', file=sys.stderr)
        sys.exit(1)
