[CmdletBinding()]
param([Parameter(Mandatory, Position = 0)][string]$ReportDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WebFormsGapClassification.ps1')
try { Write-WebFormsGapClassification $ReportDirectory }
catch {
    # Avoid leaking paths or arbitrary source strings through exception messages.
    $code = $_.Exception.Message
    if ($code -cnotmatch '\AWEBFORMS_GAP_CLASSIFICATION_[A-Z_]+\z') { $code = 'WEBFORMS_GAP_CLASSIFICATION_INPUT_INVALID' }
    throw $code
}
