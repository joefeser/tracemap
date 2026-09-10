param([string]$IndexPath = '', [string]$ReportPath = '', [string]$OutputRoot = '')

$ErrorActionPreference = 'Stop'
# Reuses the local form-list settings and latest report; scans no application source.
& (Join-Path $PSScriptRoot 'Test-FocusedWebFormsRawEvidence.ps1') -IndexPath $IndexPath -ReportPath $ReportPath -OutputRoot $OutputRoot -BatchInspection
