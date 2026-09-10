param([string]$MethodName = '', [string]$IndexPath = '', [string]$ReportPath = '', [string]$OutputRoot = '')

$ErrorActionPreference = 'Stop'
if (!$MethodName) { $MethodName = Read-Host 'Method name to inspect (kept local)' }
if ([string]::IsNullOrWhiteSpace($MethodName)) { throw 'A method name is required.' }
& (Join-Path $PSScriptRoot 'Test-FocusedWebFormsRawEvidence.ps1') -IndexPath $IndexPath -ReportPath $ReportPath -OutputRoot $OutputRoot -CreateLocalInspection -StartingMethodName $MethodName.Trim()
Write-Host 'Open the newest JSON in local-inspection-private under your configured output root.'
Write-Host 'Inspect hops and directCalls. Keep this private file at work. Share only whether the expected data-access calls are present or where the retained chain stops, without private names or paths.'
