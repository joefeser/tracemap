[CmdletBinding()]
param(
    [string]$OutputRoot,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent)
)

# Categorical, local-only dual-reader diagnosis of the saved bound IL gaps.
# Prints no private names, paths, identities, hashes, or method tokens.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_IL_PROBE_POWERSHELL_7_REQUIRED' }
if (!$OutputRoot) {
    $latest = Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory |
        Where-Object { $_.Name -cmatch '^tracemap-existing-publish-[0-9a-f]{32}$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'scan/scan-manifest.json') -PathType Leaf) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (!$latest) { throw 'WEBFORMS_IL_PROBE_SCAN_UNAVAILABLE' }
    $OutputRoot = $latest.FullName
}
$scanPath = Join-Path $OutputRoot 'scan/scan-manifest.json'
$receiptPath = Join-Path $OutputRoot 'publish-receipt.local.json'
if (!(Test-Path -LiteralPath $scanPath -PathType Leaf) -or
    !(Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
    throw 'WEBFORMS_IL_PROBE_INPUT_UNAVAILABLE'
}
$scan = [IO.File]::ReadAllText($scanPath) | ConvertFrom-Json -Depth 30
$receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 20
$bound = @($scan.ilBodyProvenance.outcomes | Where-Object { $_.provenanceState -ceq 'bound' })
$disputed = @($bound | Where-Object { @($_.gapKinds) -ccontains 'IlReaderDisagreement' })
$textLimited = @($bound | Where-Object { @($_.gapKinds) -ccontains 'IlTextLimitExceeded' })
if ($bound.Count -ne 2 -or $disputed.Count -ne 1 -or $textLimited.Count -ne 1 -or
    [int]$scan.ilBodyProvenance.effectiveLimits.maxTextLength -ne 4096) {
    throw 'WEBFORMS_IL_PROBE_EXPECTED_GAPS_UNAVAILABLE'
}
function Find-VerifiedInput($Outcome) {
    $rows = @($receipt.assemblyInventory | Where-Object {
        [string]$_.sha256 -ceq [string]$Outcome.rawFileSha256
    })
    if ($rows.Count -ne 1 -or $rows[0].disposition -cne 'selected' -or
        [string]$rows[0].path -cnotmatch '^bin/[^/\\]+\.dll$') {
        throw 'WEBFORMS_IL_PROBE_BOUND_INPUT_NOT_UNIQUE'
    }
    $path = Join-Path $OutputRoot ([string]$rows[0].path)
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne [string]$Outcome.rawFileSha256) {
        throw 'WEBFORMS_IL_PROBE_BOUND_INPUT_CHANGED'
    }
    return $path
}
$disputedInput = Find-VerifiedInput $disputed[0]
$textInput = Find-VerifiedInput $textLimited[0]
$project = Join-Path $TraceMapRoot 'scripts/diagnostics/TraceMap.IlReaderProbe/TraceMap.IlReaderProbe.csproj'
if (!(Test-Path -LiteralPath $project -PathType Leaf)) { throw 'WEBFORMS_IL_PROBE_TOOL_UNAVAILABLE' }
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-il-probe-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
& dotnet build $project --nologo -v quiet *> (Join-Path $scratch 'build.local.log')
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_IL_PROBE_BUILD_FAILED' }
$generator = Join-Path $TraceMapRoot 'scripts/diagnostics/TraceMap.IlReaderProbe/bin/Debug/net10.0/TraceMap.IlReaderProbe.dll'
if (!(Test-Path -LiteralPath $generator -PathType Leaf)) { throw 'WEBFORMS_IL_PROBE_GENERATOR_UNAVAILABLE' }
$generatorSha = (Get-FileHash -LiteralPath $generator -Algorithm SHA256).Hash.ToLowerInvariant()

function Invoke-CategoricalProbe([string]$Kind, [string]$Path, [int]$Ceiling) {
    $log = Join-Path $scratch "$Kind-$Ceiling.local.log"
    & dotnet run --no-build --project $project -- $Path $Ceiling *> $log
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_IL_PROBE_RUN_FAILED' }
    $lines = @([IO.File]::ReadAllLines($log) | Where-Object {
        $_ -cmatch '^ilReaderProbe[A-Za-z]+=(?:[A-Za-z0-9-]+|[0-9]+)$'
    })
    if (@($lines | Where-Object { $_ -cmatch '^ilReaderProbeStatus=' }).Count -ne 1) {
        throw 'WEBFORMS_IL_PROBE_RESULT_UNAVAILABLE'
    }
    $inputHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $localReceipt = [ordered]@{
        schemaVersion = 'webforms-il-categorical-probe.v1'
        visibility = 'local-only'
        generatorSha256 = $generatorSha
        boundedInputSha256 = $inputHash
        sourceCommitSha = [string]$receipt.sourceCommitSha
        maxTextLength = $Ceiling
        result = $lines
    }
    [IO.File]::WriteAllText((Join-Path $scratch "$Kind-$Ceiling.receipt.local.json"),
        (($localReceipt | ConvertTo-Json -Depth 5) + "`n"), [Text.UTF8Encoding]::new($false))
    foreach ($line in $lines) { Write-Output "$Kind.$line" }
    $script:lastProbeStatus = [string](($lines | Where-Object { $_ -cmatch '^ilReaderProbeStatus=' }) -replace '^ilReaderProbeStatus=', '')
}

Invoke-CategoricalProbe 'disputed' $disputedInput 4096
if ($script:lastProbeStatus -cne 'disputed') { Write-Output 'ilProbeResult=disputed-input-not-reproduced'; return }
foreach ($ceiling in @(8192, 16384, 32768, 65536)) {
    Invoke-CategoricalProbe 'text' $textInput $ceiling
    if ($script:lastProbeStatus -cne 'IlTextLimitExceeded') {
        Write-Output "ilTextFirstNonLimitCeiling=$ceiling"
        return
    }
}
Write-Output 'ilTextFirstNonLimitCeiling=none-at-or-below-65536'
