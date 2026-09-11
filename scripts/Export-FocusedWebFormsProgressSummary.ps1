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
    $latestFile = Get-ChildItem "C:\work\tracemap-progress\focused-webforms-*.json" -File -ErrorAction Stop |
        Where-Object { $_.Name -notlike "*.performance.json" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $latestFile) { throw "FOCUSED_PROGRESS_RECEIPT_UNAVAILABLE" }
    $ProgressPath = $latestFile.FullName
}

if (-not (Test-Path -LiteralPath $ProgressPath -PathType Leaf)) {
    throw "FOCUSED_PROGRESS_RECEIPT_UNAVAILABLE"
}

$receipt = Get-Content -LiteralPath $ProgressPath -Raw | ConvertFrom-Json
if ((Get-OptionalProperty $receipt "schemaVersion") -ne "tracemap-scan-progress/v1") {
    throw "FOCUSED_PROGRESS_RECEIPT_UNSUPPORTED"
}

$latest = Get-OptionalProperty $receipt "latest"
$history = @((Get-OptionalProperty $receipt "history") | Sort-Object sequence)
$terminalStates = @("completed", "partial", "failed", "cancelled", "timed-out")
$durations = [Collections.Generic.List[object]]::new()
foreach ($start in $history | Where-Object { $_.state -eq "started" }) {
    $startOrdinal = Get-OptionalProperty $start "ordinal"
    $terminal = $history |
        Where-Object {
            $_.sequence -gt $start.sequence -and
            $_.operation -eq $start.operation -and
            $_.stage -eq $start.stage -and
            (Get-OptionalProperty $_ "ordinal") -eq $startOrdinal -and
            $_.state -in $terminalStates
        } |
        Select-Object -First 1
    if ($null -ne $terminal) {
        $durations.Add([pscustomobject]@{
            Stage = $start.stage
            ElapsedMilliseconds = [long]$terminal.elapsedMilliseconds - [long]$start.elapsedMilliseconds
        })
    }
}

$longest = $durations |
    Sort-Object -Property @(
        @{ Expression = { $_.ElapsedMilliseconds }; Descending = $true },
        @{ Expression = { $_.Stage }; Descending = $false }
    ) |
    Select-Object -First 1
$rankedDurations = @($durations |
    Sort-Object -Property @(
        @{ Expression = { $_.ElapsedMilliseconds }; Descending = $true },
        @{ Expression = { $_.Stage }; Descending = $false }
    ))
$transitions = 0
$priorStage = $null
foreach ($event in $history) {
    if ($null -ne $priorStage -and $event.stage -ne $priorStage) { $transitions++ }
    $priorStage = $event.stage
}

"focused-webforms-progress=completed"
"terminalState=$(Get-OptionalProperty $latest 'state')"
"terminalStage=$(Get-OptionalProperty $latest 'stage')"
"lastSuccessfulStage=$(Get-OptionalProperty $latest 'lastSuccessfulStage')"
"totalElapsedMs=$(Get-OptionalProperty $latest 'elapsedMilliseconds')"
"checkpointHistoryCount=$($history.Count)"
"longestObservedStage=$(if ($null -eq $longest) { 'unavailable' } else { $longest.Stage })"
"longestObservedStageElapsedMs=$(if ($null -eq $longest) { 'unavailable' } else { $longest.ElapsedMilliseconds })"
"stageTransitionCount=$transitions"
for ($index = 0; $index -lt 5; $index++) {
    $label = ($index + 1).ToString("D2", [Globalization.CultureInfo]::InvariantCulture)
    if ($index -lt $rankedDurations.Count) {
        "topObservedStage$label=$($rankedDurations[$index].Stage)|elapsedMs=$($rankedDurations[$index].ElapsedMilliseconds)"
    }
    else {
        "topObservedStage$label=unavailable"
    }
}
