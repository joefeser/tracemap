# Runs the configured bounded page-list report, then reads only its exact new JSON.
param([string]$OutputRoot = '', [string]$ConfigPath = '')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 is required.' }
if (!$ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.json' }
. (Join-Path $PSScriptRoot 'webforms-review/FocusedWebFormsConfig.ps1')
if (!$OutputRoot) { $OutputRoot = (Read-FocusedWebFormsConfig -ConfigPath $ConfigPath).OutputRoot }
$started = [DateTime]::UtcNow.AddSeconds(-1)
& (Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.ps1') -OutputRootOverride $OutputRoot -ConfigPath $ConfigPath
$reports = @(Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'webforms-page-list-*' |
    ForEach-Object { Get-Item -LiteralPath (Join-Path $_.FullName 'webforms-modernization.json') -ErrorAction SilentlyContinue } |
    Where-Object { $_.LastWriteTimeUtc -ge $started } |
    Sort-Object LastWriteTimeUtc -Descending)
if ($reports.Count -ne 1) {
    throw 'Could not identify exactly one JSON report created by this bounded run; triage was not attempted.'
}
& (Join-Path $PSScriptRoot 'Triage-CompletedWebFormsPages.ps1') -ReportPath $reports[0].FullName
