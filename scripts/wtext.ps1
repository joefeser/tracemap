[CmdletBinding()]
param(
    [string]$OutputRoot,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent)
)

# Recheck only the previously rejected context DLL at bounded text ceilings.
# The existing publish output and probe remain untouched. All new files stay local.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_TEXT_PROBE_POWERSHELL_7_REQUIRED' }

if (!$OutputRoot) {
    $latest = Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory |
        Where-Object { $_.Name -cmatch '^tracemap-existing-publish-[0-9a-f]{32}$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'probe/scan-manifest.json') -PathType Leaf) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (!$latest) { throw 'WEBFORMS_TEXT_PROBE_MANIFEST_UNAVAILABLE' }
    $OutputRoot = $latest.FullName
}
$manifestPath = Join-Path $OutputRoot 'probe/scan-manifest.json'
$receiptPath = Join-Path $OutputRoot 'publish-receipt.local.json'
if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
    !(Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
    throw 'WEBFORMS_TEXT_PROBE_INPUT_UNAVAILABLE'
}
$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 30
$receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 20
$failed = @($manifest.compiledInputProvenance.outcomes | Where-Object {
    $_.outcome -ceq 'limit-exhausted' -and @($_.gapKinds) -ccontains 'ManagedInputTextLimitExceeded'
})
if ($failed.Count -ne 1 -or [int]$manifest.compiledInputProvenance.effectiveLimits.maxTextLength -ne 4096) {
    throw 'WEBFORMS_TEXT_PROBE_EXPECTED_FAILURE_UNAVAILABLE'
}
$matchingRows = @($receipt.assemblyInventory | Where-Object {
    [string]$_.sha256 -ceq [string]$failed[0].rawFileSha256
})
if ($matchingRows.Count -ne 1 -or
    $matchingRows[0].disposition -cne 'artifact-context-no-source-commit' -or
    [string]$matchingRows[0].path -cnotmatch '^bin/[^/\\]+\.dll$') {
    throw 'WEBFORMS_TEXT_PROBE_CONTEXT_NOT_UNIQUE'
}
$target = Join-Path $OutputRoot ([string]$matchingRows[0].path)
if (!(Test-Path -LiteralPath $target -PathType Leaf) -or
    (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -cne [string]$failed[0].rawFileSha256) {
    throw 'WEBFORMS_TEXT_PROBE_TARGET_MISMATCH'
}

$scratch = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-text-probe-' + [guid]::NewGuid().ToString('N'))
$repo = Join-Path $scratch 'empty-repo'
[void][IO.Directory]::CreateDirectory($repo)
& git -C $repo init -q
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_TEXT_PROBE_GIT_INIT_FAILED' }
& git -C $repo -c user.name=PublicProbe -c user.email=probe@example.invalid commit --allow-empty -qm empty
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_TEXT_PROBE_GIT_COMMIT_FAILED' }
$project = Join-Path $TraceMapRoot 'src/dotnet/TraceMap.Cli'
if (!(Test-Path -LiteralPath $project -PathType Container)) { throw 'WEBFORMS_TEXT_PROBE_CLI_UNAVAILABLE' }

Write-Output 'textProbeInputs=1'
foreach ($ceiling in @(8192, 16384, 32768, 65536)) {
    $scanRoot = Join-Path $scratch "scan-$ceiling"
    $log = Join-Path $scratch "scan-$ceiling.local.log"
    & dotnet run --no-build --project $project -- scan --repo $repo --out $scanRoot `
        --compiled-input $target --compiled-max-artifacts 1 --compiled-max-text $ceiling *> $log
    if ($LASTEXITCODE -ne 0) {
        Write-Output "textProbeResult=scan-failed;exit=$LASTEXITCODE"
        return
    }
    $scanManifestPath = Join-Path $scanRoot 'scan-manifest.json'
    if (!(Test-Path -LiteralPath $scanManifestPath -PathType Leaf)) {
        Write-Output 'textProbeResult=manifest-missing'
        return
    }
    $scanManifest = [IO.File]::ReadAllText($scanManifestPath) | ConvertFrom-Json -Depth 30
    $outcomes = @($scanManifest.compiledInputProvenance.outcomes)
    if ($outcomes.Count -ne 1 -or [int]$scanManifest.compiledInputProvenance.omittedInputCount -ne 0) {
        Write-Output 'textProbeResult=unexpected-input-count'
        return
    }
    $outcome = $outcomes[0]
    if ($outcome.outcome -ceq 'admitted' -and ![string]::IsNullOrWhiteSpace([string]$outcome.assemblyIdentity)) {
        Write-Output "textProbeAdmittedCeiling=$ceiling"
        return
    }
    if ($outcome.outcome -cne 'limit-exhausted' -or
        @($outcome.gapKinds) -cnotcontains 'ManagedInputTextLimitExceeded') {
        Write-Output 'textProbeResult=other-admission-gap'
        return
    }
}
Write-Output 'textProbeAdmittedCeiling=none-at-or-below-65536'
