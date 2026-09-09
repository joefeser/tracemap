# Runs the configured bounded page-list report, then reads only its exact new JSON.
param([string]$OutputRoot = 'C:\work\tracemap-output')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 is required.' }
$started = [DateTime]::UtcNow.AddSeconds(-1)
& (Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.ps1')
$reports = @(Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'webforms-page-list-*' |
    ForEach-Object { Get-Item -LiteralPath (Join-Path $_.FullName 'webforms-modernization.json') -ErrorAction SilentlyContinue } |
    Where-Object { $_.LastWriteTimeUtc -ge $started } |
    Sort-Object LastWriteTimeUtc -Descending)
if ($reports.Count -ne 1) {
    throw 'Could not identify exactly one JSON report created by this bounded run; triage was not attempted.'
}
& (Join-Path $PSScriptRoot 'Triage-CompletedWebFormsPages.ps1') -ReportPath $reports[0].FullName
