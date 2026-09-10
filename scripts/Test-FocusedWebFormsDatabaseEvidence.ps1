param([string]$IndexPath = '', [string]$InspectionPath = '', [string]$OutputRoot = '')

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Test-FocusedWebFormsRawEvidence.ps1') -IndexPath $IndexPath -OutputRoot $OutputRoot -InspectionPath $InspectionPath -DatabaseEvidence
Write-Host 'This console summary is safe to share. Keep the local inspection JSON and SQL at work.'
