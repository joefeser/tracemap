param([string]$IndexPath = '', [string]$ReportPath = '', [string]$OutputRoot = '', [string]$ConfigPath = '')

$ErrorActionPreference = 'Stop'
# Reuses the local JSON settings and latest report; scans no application source.
& (Join-Path $PSScriptRoot 'Test-FocusedWebFormsRawEvidence.ps1') -IndexPath $IndexPath -ReportPath $ReportPath -OutputRoot $OutputRoot -ConfigPath $ConfigPath -BatchInspection
