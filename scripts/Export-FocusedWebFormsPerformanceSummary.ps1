[CmdletBinding()]
param(
    [string]$ProgressPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-OptionalProperty {
    param([AllowNull()][object]$Value, [string]$Name)

    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

if ([string]::IsNullOrWhiteSpace($ProgressPath)) {
    $latest = Get-ChildItem "C:\work\tracemap-progress\focused-webforms-*.json" -File -ErrorAction Stop |
        Where-Object { $_.Name -notlike "*.performance.json" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $latest) { throw "FOCUSED_PERFORMANCE_PROGRESS_UNAVAILABLE" }
    $ProgressPath = $latest.FullName
}

$performancePath = "$ProgressPath.performance.json"
if (-not (Test-Path -LiteralPath $performancePath -PathType Leaf)) {
    throw "FOCUSED_PERFORMANCE_RECEIPT_UNAVAILABLE"
}

$performance = Get-Content -LiteralPath $performancePath -Raw | ConvertFrom-Json -Depth 20
if ((Get-OptionalProperty $performance "schemaVersion") -ne "tracemap-scan-performance/v1") {
    throw "FOCUSED_PERFORMANCE_RECEIPT_UNSUPPORTED"
}

$active = Get-OptionalProperty $performance "activeExtractor"
$slowest = Get-OptionalProperty $performance "slowestExtractor"
$timings = @(Get-OptionalProperty $performance "extractorTimings")

"focused-webforms-performance=completed"
"runState=$(Get-OptionalProperty $performance 'runState')"
"timingCoverage=$(Get-OptionalProperty $performance 'timingCoverage')"
"heartbeatObserved=$(([bool](Get-OptionalProperty $performance 'heartbeatObserved')).ToString().ToLowerInvariant())"
"heartbeatCount=$(Get-OptionalProperty $performance 'heartbeatCount')"
"timingsTruncated=$(([bool](Get-OptionalProperty $performance 'timingsTruncated')).ToString().ToLowerInvariant())"
"extractorTimingCount=$($timings.Count)"
"activeExtractor=$(if ($null -eq $active) { 'unavailable' } else { Get-OptionalProperty $active 'extractor' })"
"slowestExtractor=$(if ($null -eq $slowest) { 'unavailable' } else { Get-OptionalProperty $slowest 'extractor' })"
"slowestExtractorVersion=$(if ($null -eq $slowest) { 'unavailable' } else { Get-OptionalProperty $slowest 'extractorVersion' })"
"slowestElapsedMs=$(if ($null -eq $slowest) { 'unavailable' } else { Get-OptionalProperty $slowest 'elapsedMilliseconds' })"
"slowestEmittedFactCount=$(if ($null -eq $slowest) { 'unavailable' } else { Get-OptionalProperty $slowest 'emittedFactCount' })"
"slowestEmittedGapCount=$(if ($null -eq $slowest) { 'unavailable' } else { Get-OptionalProperty $slowest 'emittedGapCount' })"
"nextAction=$(Get-OptionalProperty $performance 'nextAction')"
