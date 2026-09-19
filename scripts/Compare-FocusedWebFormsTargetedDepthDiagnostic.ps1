# Read-only: compares a completed targeted depth-10 packet with its receipted
# depth-8 page handoffs. It never launches TraceMap or modifies artifacts.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [ValidateCount(1, 2)][string[]]$PageId = @('page-002', 'page-003'),
    [string]$DiagnosticDirectory = '',
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_POWERSHELL_7_REQUIRED' }

$commonPath = Join-Path $PSScriptRoot 'webforms-review/ClaudeReview.Common.ps1'
if (!(Test-Path -LiteralPath $commonPath -PathType Leaf)) { throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_COMMON_UNAVAILABLE' }
. $commonPath

function Values([object]$Value) { if ($null -eq $Value) { @() } else { @($Value) } }
function Property-Value([object]$Value, [string]$Name) {
    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}
function Get-TerminalKeys([object[]]$Boundaries) {
    $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($boundary in $Boundaries) {
        $parts = @([string]$boundary.boundaryKind, [string]$boundary.boundaryTargetId, [string]$boundary.terminalEvidenceId)
        if (@($parts | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) {
            throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_TERMINAL_IDENTITY_INVALID'
        }
        [void]$keys.Add((@($parts | ForEach-Object { "$($_.Length):$_" }) -join ''))
    }
    return ,$keys
}
function Get-DepthChainCount([object[]]$Chains) {
    return @($Chains | Where-Object {
        $observation = Property-Value $_ 'traversalObservation'
        $reasons = if ($null -ne $observation) {
            Values (Property-Value $observation 'truncationReasons')
        } else {
            Values (Property-Value $_ 'traversalTruncationReasons')
        }
        $reasons -contains 'depth'
    }).Count
}

$requestedPageIds = @($PageId | ForEach-Object { $_.Trim() } | Select-Object -Unique)
if ($requestedPageIds.Count -lt 1 -or $requestedPageIds.Count -gt 2 -or
    @($requestedPageIds | Where-Object { $_ -cnotmatch '^page-[0-9]{3,}$' }).Count -ne 0) {
    throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_PAGE_SELECTION_INVALID'
}

$context = Get-FocusedWebFormsClaudeEvidenceContext $ReviewRoot $TraceMapRoot
$root = $context.Root
$prefix = 'targeted-depth-10-' + ($requestedPageIds -join '-') + '-'
if ([string]::IsNullOrWhiteSpace($DiagnosticDirectory)) {
    $diagnosticsRoot = Join-Path $root 'diagnostics'
    $latest = @(Get-ChildItem -LiteralPath $diagnosticsRoot -Directory -Filter "$prefix*" -ErrorAction Stop |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'webforms-modernization.json') -PathType Leaf } |
        Sort-Object Name -Descending | Select-Object -First 1)
    if ($latest.Count -ne 1) { throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_DIAGNOSTIC_UNAVAILABLE' }
    $DiagnosticDirectory = $latest[0].FullName
}
$diagnosticRoot = [IO.Path]::GetFullPath($DiagnosticDirectory).TrimEnd('\', '/')
$expectedRoot = [IO.Path]::GetFullPath((Join-Path $root 'diagnostics')).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (!$diagnosticRoot.StartsWith($expectedRoot, $comparison)) { throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_DIAGNOSTIC_OUTSIDE_REVIEW' }
$diagnosticPacket = Read-FocusedWebFormsBoundedJson (Join-Path $diagnosticRoot 'webforms-modernization.json') 128MB 'WEBFORMS_TARGETED_DEPTH_COMPARE_PACKET_UNAVAILABLE'
if ($diagnosticPacket.schemaVersion -ne 'webforms-modernization-packet.v1' -or
    $diagnosticPacket.surfaceSelection.items.Count -ne $requestedPageIds.Count -or
    @($diagnosticPacket.surfaceSelection.items | Where-Object { $_.status -ne 'matched' }).Count -ne 0) {
    throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_PACKET_INVALID'
}

$aggregateBaselineKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$aggregateDiagnosticKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$baselineDepthChains = 0
$diagnosticDepthChains = 0
for ($index = 0; $index -lt $requestedPageIds.Count; $index++) {
    $pageId = $requestedPageIds[$index]
    $handoffRelativePath = "workbench/$pageId.handoff.json"
    $receipt = Read-FocusedWebFormsBoundedJson (Join-Path $root 'run-receipt.json') 16MB 'WEBFORMS_TARGETED_DEPTH_COMPARE_RECEIPT_UNAVAILABLE'
    Assert-FocusedWebFormsReceiptedArtifact $receipt 'workbench' $root $handoffRelativePath
    $baseline = Read-FocusedWebFormsBoundedJson (Join-Path $root $handoffRelativePath) 32MB 'WEBFORMS_TARGETED_DEPTH_COMPARE_HANDOFF_UNAVAILABLE'
    $baselineSources = @($baseline.packet.sources | ForEach-Object { "$($_.scanId)|$($_.commitSha)" } | Sort-Object)
    $diagnosticSources = @($diagnosticPacket.sources | ForEach-Object { "$($_.scanId)|$($_.commitSha)" } | Sort-Object)
    if (($baselineSources -join "`n") -cne ($diagnosticSources -join "`n")) {
        throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_PROVENANCE_MISMATCH'
    }
    $selection = $diagnosticPacket.surfaceSelection.items[$index]
    $surfaceIds = @(Values $selection.surfaceIds)
    $diagnosticChains = @($diagnosticPacket.eventChains | Where-Object { $_.surfaceId -in $surfaceIds })
    $diagnosticBoundaries = @($diagnosticPacket.downstreamBoundaries | Where-Object { $_.surfaceId -in $surfaceIds })
    $baselineChains = @(Values $baseline.eventChains)
    $baselineBoundaries = @(Values $baseline.downstreamBoundaries)
    $baselineKeys = Get-TerminalKeys $baselineBoundaries
    $diagnosticKeys = Get-TerminalKeys $diagnosticBoundaries
    foreach ($key in $baselineKeys) { [void]$aggregateBaselineKeys.Add($key) }
    foreach ($key in $diagnosticKeys) { [void]$aggregateDiagnosticKeys.Add($key) }
    $baselinePageDepth = Get-DepthChainCount $baselineChains
    $diagnosticPageDepth = Get-DepthChainCount $diagnosticChains
    $baselineDepthChains += $baselinePageDepth
    $diagnosticDepthChains += $diagnosticPageDepth
    $added = @($diagnosticKeys | Where-Object { !$baselineKeys.Contains($_) }).Count
    $lost = @($baselineKeys | Where-Object { !$diagnosticKeys.Contains($_) }).Count
    Write-Output "page=$pageId|depth8Chains=$($baselineChains.Count)|depth10Chains=$($diagnosticChains.Count)|depth8DepthTruncated=$baselinePageDepth|depth10DepthTruncated=$diagnosticPageDepth|depth8TerminalEvidence=$($baselineKeys.Count)|depth10TerminalEvidence=$($diagnosticKeys.Count)|addedTerminalEvidence=$added|lostTerminalEvidence=$lost"
}

$aggregateAdded = @($aggregateDiagnosticKeys | Where-Object { !$aggregateBaselineKeys.Contains($_) }).Count
$aggregateLost = @($aggregateBaselineKeys | Where-Object { !$aggregateDiagnosticKeys.Contains($_) }).Count
Write-Output 'webformsTargetedDepthComparison=completed'
Write-Output "pageIds=$($requestedPageIds -join ',')"
Write-Output "depth8DepthTruncated=$baselineDepthChains"
Write-Output "depth10DepthTruncated=$diagnosticDepthChains"
Write-Output "depth8TerminalEvidence=$($aggregateBaselineKeys.Count)"
Write-Output "depth10TerminalEvidence=$($aggregateDiagnosticKeys.Count)"
Write-Output "terminalDelta=added:$aggregateAdded|lost:$aggregateLost"
Write-Output "diagnosticPacket=$(Join-Path $diagnosticRoot 'webforms-modernization.json')"
Write-Output 'nonClaim=distinct-evidence-tuples-not-runtime-operations;partial-results-do-not-prove-absence'
