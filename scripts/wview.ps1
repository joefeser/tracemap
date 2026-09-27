[CmdletBinding()]
param(
    [string]$ProofRoot = '',
    [string]$PacketPath = '',
    [string]$OutputRoot = '',
    [string]$ConfigPath = ''
)

# Short entry point: saved proof -> readable paths -> fresh private workbench.
# No publish, source scan, or combine is performed by this wrapper.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_VIEW_POWERSHELL_7_REQUIRED' }

$handoffs = [Collections.Generic.List[string]]::new()
$replayArgs = @{}
if ($ProofRoot) { $replayArgs.ProofRoot = $ProofRoot }
& (Join-Path $PSScriptRoot 'Replay-ExistingWebFormsCompiledPathReviews.ps1') @replayArgs |
    ForEach-Object {
        $line = [string]$_
        Write-Output $line
        if ($line.StartsWith('compiledPathReviewHandoff=', [StringComparison]::Ordinal)) {
            $handoffs.Add($line.Substring('compiledPathReviewHandoff='.Length))
        }
    }
if ($handoffs.Count -eq 0) { throw 'WEBFORMS_VIEW_HANDOFF_UNAVAILABLE' }
# API projection is emitted first when SQL paths are absent. Never substitute
# the subsequent zero-path SQL projection for that selected API review.
$reviewArgs = @{ CompiledPathHandoffPath = $handoffs[0] }
if ($PacketPath) { $reviewArgs.PacketPath = $PacketPath }
if ($OutputRoot) { $reviewArgs.OutputRoot = $OutputRoot }
if ($ConfigPath) { $reviewArgs.ConfigPath = $ConfigPath }
& (Join-Path $PSScriptRoot 'New-FocusedWebFormsStandaloneReview.ps1') @reviewArgs
