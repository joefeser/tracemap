# Historical `dotnetperf` Validation-Corpus Runway

## Purpose

The historical `dotnetperf` repository is a candidate external validation
corpus for TraceMap's .NET source, metadata, PDB, and IL identity model. It is a
strong legacy Cecil/.NET Framework rewriting corpus, not a comprehensive modern
ECMA-335 or cross-language corpus. Its recorded Bitbucket origin currently
requires repository access, so it must not be presented as a reproducibly
public fixture unless a licensed public mirror or independently reproducible
fixture set is established.

Tracking issue: [#759](https://github.com/joefeser/tracemap/issues/759).

This document is a historical-corpus validation record, not the active
implementation plan. The current status and first compiled-evidence slice live
in [../DOTNET_COMPLETENESS_STATUS.md](../DOTNET_COMPLETENESS_STATUS.md) and the
[`compiled-dotnet-evidence-foundation`](../../.kiro/specs/compiled-dotnet-evidence-foundation/requirements.md)
Kiro spec. Issues #766-#769 divide public fixtures, the Windows lane, and corpus
minimization. Do not duplicate those plans here.

Pin all investigation to commit:

```text
db8c3359badfec620ccdc6df062b1756ef9607f8
```

This remains #769's historical-master pin. Task 11 also has a distinct
`BuildableFix` Windows profile at
`642bdaede0b97a400c24266e30670ed5c1c98689` for build and bounded-scan
validation. Its admission and receipt contract is documented in
[TASK11_WINDOWS_LANE.md](TASK11_WINDOWS_LANE.md). Evidence from that profile
must not be presented as evidence for the historical-master corpus or as a
completed Task 10 public ECMA-335 suite. The fix profile has no passing private
bounded receipt yet.

## Source and Checkout

The inspected checkout records this origin:

```text
git@bitbucket.org:metadatadashboard/dotnetperf.git
```

On an authorized Windows machine, create an isolated checkout and detach it at
the pinned commit:

```powershell
$DotNetPerfRoot = 'C:\work\dotnetperf-validation'
git clone git@bitbucket.org:metadatadashboard/dotnetperf.git $DotNetPerfRoot
git -C $DotNetPerfRoot fetch --all --tags --prune
git -C $DotNetPerfRoot checkout --detach db8c3359badfec620ccdc6df062b1756ef9607f8
git -C $DotNetPerfRoot status --short
```

The final command must produce no paths before validation begins. If the origin
cannot be fetched with the operator's existing authorized SSH configuration,
stop and record the corpus as unavailable. Do not request, embed, recover, or
reuse historical repository credentials or signing material. A private local
checkout can inform research, but it cannot satisfy a public reproducibility
claim.

The inspected repository contains approximately 34,606 declared MSTest methods.
Most are generated control-flow matrices, so the method count must not be
reported as independent semantic coverage.

## Useful Coverage

- branch legality across exception-handling regions;
- `switch`, `leave`, and protected-block control flow;
- branch retargeting after instrumentation;
- nested and generic runtime identities;
- duplicate assembly identity cases;
- raw IL fixtures, netmodules, and checked-in assembly/PDB inputs; and
- old MSBuild, aliases, reflection-heavy code, WPF/XAML, properties, and events.

## Known Blind Spots

- no Visual Basic or F# source corpus;
- incomplete metadata-table, token, blob, and evaluation-stack coverage;
- little or no modern signature, function-pointer, `calli`, custom-modifier,
  portable-PDB, SourceLink, type-forwarding, or modern-runtime coverage;
- no general hostile-PE corpus;
- historical strong-name and credential material must be treated as
  compromised; and
- the full legacy build is not expected to run safely or reproducibly on the
  current macOS development environment.

## Identity Requirements

TraceMap must keep these as separate identities connected by evidence-backed
edges:

- source symbol;
- compiled metadata member;
- PDB method/document occurrence; and
- rewritten method body or member.

Do not collapse them into a shared display-string identity. Regression cases
must include:

- operand-aware canonical method-body hashes that preserve opcode operands;
- operand-insensitive method-body hashes only as explicitly non-unique
  heuristics that cannot establish an identity edge;
- nested-type `/` versus `+` naming;
- same-name and same-arity overloads;
- properties and events versus generated accessors;
- constructed generic identities versus generic definitions; and
- duplicate assemblies containing similar-looking types.

## Companion Public Fixtures

`dotnetperf` cannot prove language equivalence. Maintain a purpose-built
C#/Visual Basic/F# matrix that compiles equivalent CLR shapes and tests exact
metadata signatures. Add sanitized Web Forms fixtures for:

- project/default imports such as `Odbc.OdbcCommand`;
- inherited receiver fields and failed or unavailable base projects;
- `ArrayList` and derived parameter collections;
- overload identity and resolution; and
- page handler to business layer to inherited data access to database terminal
  traversal.

Golden assertions should cover rule IDs, evidence tiers, identity provenance,
receiver-bridge and surface-evidence edges, terminal kind/count, and explicit
gap or truncation classifications. Artifact byte size is diagnostic context,
not an acceptance contract.

## Staged Windows Validation

Use an isolated Windows x64 environment. From the authorized checkout above,
verify and enter the pinned repository root before beginning the smallest proof:

```powershell
if ((git -C $DotNetPerfRoot rev-parse HEAD).Trim() -ne 'db8c3359badfec620ccdc6df062b1756ef9607f8') {
    throw 'DOTNETPERF_PINNED_COMMIT_MISMATCH'
}
Set-Location $DotNetPerfRoot
```

Then run:

```bat
"C:\Windows\Microsoft.NET\Framework\v4.0.30319\ilasm.exe" /NOLOGO /DLL /DEBUG /OPTIMIZE /OUTPUT:"%TEMP%\dotnetperf-ret.dll" "src\NetPerf.Tests.Unit\Test Data\instr_ret.il.txt"
```

Run TraceMap itself from a separate clean checkout pinned to the exact candidate
commit. Do not substitute a globally installed tool whose source commit is
unknown:

```powershell
$TraceMapRoot = 'C:\work\tracemap-validation'
$TraceMapCommit = '<approved 40-character TraceMap commit SHA>'
$TraceMapOut = Join-Path $env:TEMP "tracemap-dotnetperf-$($TraceMapCommit.Substring(0, 12))"

if ($TraceMapCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'TRACEMAP_PINNED_COMMIT_REQUIRED'
}
git -C $TraceMapRoot fetch origin $TraceMapCommit
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_FETCH_FAILED:$LASTEXITCODE" }
git -C $TraceMapRoot checkout --detach $TraceMapCommit
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_CHECKOUT_FAILED:$LASTEXITCODE" }
if ((git -C $TraceMapRoot rev-parse HEAD).Trim() -ne $TraceMapCommit) {
    throw 'TRACEMAP_PINNED_COMMIT_MISMATCH'
}
if (git -C $TraceMapRoot status --porcelain --untracked-files=all) {
    throw 'TRACEMAP_CHECKOUT_NOT_CLEAN'
}

dotnet build "$TraceMapRoot\src\dotnet\TraceMap.Cli\TraceMap.Cli.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_BUILD_FAILED:$LASTEXITCODE" }
$TraceMapGenerator = "$TraceMapRoot\src\dotnet\TraceMap.Cli\bin\Release\net10.0\tracemap.dll"
$GeneratorSha256 = (Get-FileHash -LiteralPath $TraceMapGenerator -Algorithm SHA256).Hash.ToLowerInvariant()
Remove-Item -LiteralPath $TraceMapOut -Recurse -Force -ErrorAction SilentlyContinue
dotnet $TraceMapGenerator `
    scan --repo $DotNetPerfRoot --out $TraceMapOut
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_SCAN_FAILED:$LASTEXITCODE" }
```

Assert the bounded artifact and provenance contract before inspecting any
identity result:

```powershell
$requiredArtifacts = @(
    'scan-manifest.json',
    'facts.ndjson',
    'index.sqlite',
    'report.md',
    'logs\analyzer.log'
)
foreach ($relativePath in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $TraceMapOut $relativePath) -PathType Leaf)) {
        throw "TRACEMAP_REQUIRED_ARTIFACT_MISSING:$relativePath"
    }
}

$manifest = Get-Content -LiteralPath (Join-Path $TraceMapOut 'scan-manifest.json') -Raw |
    ConvertFrom-Json
if ($manifest.commitSha -ne 'db8c3359badfec620ccdc6df062b1756ef9607f8') {
    throw 'TRACEMAP_CORPUS_COMMIT_MISMATCH'
}
if ([string]::IsNullOrWhiteSpace([string]$manifest.scannerVersion)) {
    throw 'TRACEMAP_SCANNER_VERSION_MISSING'
}
if ([string]$manifest.sourceSnapshotDigest -notmatch '^[0-9a-f]{64}$') {
    throw 'TRACEMAP_BOUNDED_INPUT_SHA256_MISSING'
}

$facts = @(Get-Content -LiteralPath (Join-Path $TraceMapOut 'facts.ndjson') |
    ForEach-Object { $_ | ConvertFrom-Json })
if ($facts.Count -eq 0) { throw 'TRACEMAP_FACTS_EMPTY' }
$invalidFacts = @($facts | Where-Object {
    $_.commitSha -ne 'db8c3359badfec620ccdc6df062b1756ef9607f8' -or
    [string]::IsNullOrWhiteSpace([string]$_.ruleId) -or
    [string]::IsNullOrWhiteSpace([string]$_.evidenceTier) -or
    [string]::IsNullOrWhiteSpace([string]$_.evidence.extractorId) -or
    [string]::IsNullOrWhiteSpace([string]$_.evidence.extractorVersion)
})
if ($invalidFacts.Count -ne 0) {
    throw "TRACEMAP_FACT_PROVENANCE_INVALID:$($invalidFacts.Count)"
}

"traceMapCommit=$TraceMapCommit"
"generatorPath=src/dotnet/TraceMap.Cli/bin/Release/net10.0/tracemap.dll"
"generatorSha256=$GeneratorSha256"
"corpusCommit=$($manifest.commitSha)"
"boundedInputSha256=$($manifest.sourceSnapshotDigest)"
"scannerVersion=$($manifest.scannerVersion)"
$facts |
    Group-Object { "$($_.evidence.extractorId)|$($_.evidence.extractorVersion)" } |
    Sort-Object Name |
    ForEach-Object { "extractor=$($_.Name);facts=$($_.Count)" }
```

Retain those receipt lines with the private validation run. They bind the
baseline to the invoked generator bytes and TraceMap's bounded source snapshot.
Do not publish this private-corpus receipt as a shareable artifact. Any
shareable derivative must first apply its documented privacy projection and
hash that projected input rather than this private source snapshot.

This stage is only a pinned TraceMap baseline until the scanner has documented
rule IDs and fact shapes for source-to-metadata, metadata-to-PDB, and
metadata-to-rewritten-IL identity edges. Before calling a run identity-corpus
validation, add assertions for those exact rule IDs, both endpoint identities,
evidence tiers, spans or metadata locations, extractor versions, and expected
explicit gaps. A successful build, test run, scan exit code, artifact count, or
artifact byte size does not prove those identity edges.

Then proceed in bounded stages:

1. Record Windows, Visual Studio, MSBuild, .NET Framework, ILAsm, and test-runner
   versions.
2. Run the ILAsm fixture smoke without altering project files.
3. Build the smallest viable project/test slice.
4. Run representative branch, switch, exception-region, `leave`, instrumentation,
   nested/generic, and duplicate-identity tests.
5. Run the pinned TraceMap scan and provenance assertions above.
6. Capture exact commands, exit codes, logs, selected tests, output paths,
   TraceMap commit, scanner/extractor versions, and blockers.
7. Add public TraceMap regression fixtures only after the expected identity and
   evidence contract is documented.
8. Add rule-specific identity-edge assertions; until they pass, label the run a
   baseline rather than identity-corpus validation.
9. Run the complete historical corpus only after the bounded slices are stable.

Do not push modernization changes from the validation environment. Do not use
or disclose historical keys, credentials, or signing material.
