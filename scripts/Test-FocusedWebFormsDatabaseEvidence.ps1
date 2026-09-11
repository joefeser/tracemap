param([string]$IndexPath = '', [string]$InspectionPath = '', [string]$OutputRoot = '', [string]$ConfigPath = '')

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Test-FocusedWebFormsRawEvidence.ps1') -IndexPath $IndexPath -OutputRoot $OutputRoot -InspectionPath $InspectionPath -ConfigPath $ConfigPath -DatabaseEvidence
Write-Host 'This console summary is safe to share. Keep the local inspection JSON and SQL at work.'
