[CmdletBinding()]
param(
    [string]$ProofRoot = '',
    [string]$PacketPath = '',
    [string]$OutputRoot = '',
    [string]$ConfigPath = '',
    [switch]$FromSavedProof,
    [switch]$RecheckApi
)

# Short entry point: saved proof -> readable paths -> fresh private workbench.
# No publish, source scan, or combine is performed by this wrapper.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_VIEW_POWERSHELL_7_REQUIRED' }

$handoffs = [Collections.Generic.List[string]]::new()
$selectedProofRoot = ''
$replayArgs = @{}
if ($ProofRoot) {
    $replayArgs.ProofRoot = $ProofRoot
    $replayArgs.AllowBaseIndex = $true
}
if ($RecheckApi) { $replayArgs.RecheckApi = $true }
& (Join-Path $PSScriptRoot 'Replay-ExistingWebFormsCompiledPathReviews.ps1') @replayArgs |
    ForEach-Object {
        $line = [string]$_
        Write-Output $line
        if ($line.StartsWith('compiledReplayProofRoot=', [StringComparison]::Ordinal)) {
            $selectedProofRoot = $line.Substring('compiledReplayProofRoot='.Length)
        }
        if ($line.StartsWith('compiledPathReviewHandoff=', [StringComparison]::Ordinal)) {
            $handoffs.Add($line.Substring('compiledPathReviewHandoff='.Length))
        }
    }
if ($handoffs.Count -eq 0) { throw 'WEBFORMS_VIEW_HANDOFF_UNAVAILABLE' }
# API projection is emitted first when SQL paths are absent. Never substitute
# the subsequent zero-path SQL projection for that selected API review.
$reviewArgs = @{ CompiledPathHandoffPath = $handoffs[0] }
if ($FromSavedProof) {
    if ($PacketPath) { throw 'WEBFORMS_VIEW_PACKET_AND_SAVED_PROOF_CONFLICT' }
    if (!$selectedProofRoot) { throw 'WEBFORMS_VIEW_PROOF_ROOT_UNAVAILABLE' }
    $packetLines = @(& (Join-Path $PSScriptRoot 'New-SavedWebFormsProofPacket.ps1') -ProofRoot $selectedProofRoot -CompiledPathHandoffPath $handoffs[0])
    foreach ($line in $packetLines) { Write-Output $line }
    $packetLine = @($packetLines | Where-Object { $_ -cmatch '^savedProofPacket=' })
    if ($packetLine.Count -ne 1) { throw 'WEBFORMS_VIEW_SAVED_PACKET_UNAVAILABLE' }
    $PacketPath = ([string]$packetLine[0]).Substring('savedProofPacket='.Length)
    # Focused proof output is intentionally separate from full-site reports.
    if (!$OutputRoot) { $OutputRoot = $selectedProofRoot }
}
if ($PacketPath) { $reviewArgs.PacketPath = $PacketPath }
if ($OutputRoot) { $reviewArgs.OutputRoot = $OutputRoot }
if ($ConfigPath) { $reviewArgs.ConfigPath = $ConfigPath }
& (Join-Path $PSScriptRoot 'New-FocusedWebFormsStandaloneReview.ps1') @reviewArgs
