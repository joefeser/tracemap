param([string]$IndexPath = '', [string]$ReportPath = '', [string]$OutputRoot = '')

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Test-FocusedWebFormsRawEvidence.ps1') -IndexPath $IndexPath -ReportPath $ReportPath -OutputRoot $OutputRoot -CreateLocalInspection
Write-Host 'Open the newest JSON in the local-inspection-private folder under your output root.'
Write-Host 'PRIVATE: do not send that JSON or a photo of it. Follow its inspection instructions and share only the closed result category.'
