[CmdletBinding()]
param(
    [string]$ReviewOutputPath,
    [string]$OutputRoot,
    [string]$WebFormsFolder = '.',
    [string]$BackendFolder = '.',
    [string]$ControlsFolder = '.',
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$SummaryDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot)
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = if ([IO.Path]::DirectorySeparatorChar -eq '\') { 'C:\work\tracemap-output' } else { Join-Path ([IO.Path]::GetTempPath()) 'tracemap-output' }
}
if ([string]::IsNullOrWhiteSpace($ReviewOutputPath)) {
    $candidate = @(Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'focused-webforms-*' -ErrorAction Stop |
        Where-Object {
            (Test-Path -LiteralPath (Join-Path $_.FullName 'scan/facts.ndjson') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'scan/scan-manifest.json') -PathType Leaf)
        } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1)
    if ($candidate.Count -ne 1) { throw 'FocusedWebFormsResumeFailed:RetainedOutputUnavailable' }
    $ReviewOutputPath = $candidate[0].FullName
}
$ReviewOutputPath = [IO.Path]::GetFullPath($ReviewOutputPath)
if ([string]::IsNullOrWhiteSpace($SummaryDirectory)) {
    $SummaryDirectory = if ([IO.Path]::DirectorySeparatorChar -eq '\') { 'C:\work\tracemap-summary' } else { Join-Path ([IO.Path]::GetTempPath()) 'tracemap-summary' }
}

$traceMapHead = (git -C $TraceMapRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $traceMapHead -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'FocusedWebFormsResumeFailed:TraceMapHeadUnavailable'
}

& (Join-Path $PSScriptRoot 'Export-FocusedWebFormsWorkspaceSummary.ps1') `
    -ReviewOutputPath $ReviewOutputPath `
    -WebFormsFolder $WebFormsFolder `
    -BackendFolder $BackendFolder `
    -ControlsFolder $ControlsFolder `
    -TraceMapHead $traceMapHead `
    -OutputDirectory $SummaryDirectory

$resultPath = Join-Path $ReviewOutputPath 'local-review-result.json'
if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
    & (Join-Path $PSScriptRoot 'Export-FocusedWebFormsEvidenceSummary.ps1') `
        -ReviewOutputPath $ReviewOutputPath `
        -OutputDirectory $SummaryDirectory
    & (Join-Path $PSScriptRoot 'Export-FocusedWebFormsAccuracySummary.ps1') `
        -ReviewOutputPath $ReviewOutputPath `
        -WebFormsFolder $WebFormsFolder `
        -BackendFolder $BackendFolder `
        -ControlsFolder $ControlsFolder `
        -OutputDirectory $SummaryDirectory
} else {
    'focused-webforms-review-summaries=partial;reason=local-review-result-unavailable'
}

"resumed-output-directory=$([IO.Path]::GetFileName($ReviewOutputPath))"
"summaryDirectory=$SummaryDirectory"
