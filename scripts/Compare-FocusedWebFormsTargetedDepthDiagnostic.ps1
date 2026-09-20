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
function Get-SurfaceRequestId([string]$FilePath) {
    $normalized = $FilePath.Trim().Replace('\', '/')
    while ($normalized.StartsWith('./', [StringComparison]::Ordinal)) { $normalized = $normalized.Substring(2) }
    $normalized = $normalized.TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($normalized)) { throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_PAGE_IDENTITY_INVALID' }
    $material = "webforms-modernization/surface-request/v1`0$($normalized.ToUpperInvariant())"
    $bytes = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($material))
    return 'surface-request-' + [Convert]::ToHexString($bytes).ToLowerInvariant().Substring(0, 24)
}
function Get-RenderedBoundaryTupleKeys([object[]]$Boundaries) {
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
            $pathReasons = Property-Value $observation 'pathEnumerationTruncationReasons'
            if ($null -ne $pathReasons) { Values $pathReasons } else { Values (Property-Value $observation 'truncationReasons') }
        } else {
            Values (Property-Value $_ 'traversalTruncationReasons')
        }
        $reasons -contains 'depth'
    }).Count
}
function Get-ReachabilitySummary([object[]]$Chains) {
    $seenHandlers = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $observations = @($Chains | ForEach-Object {
        $observation = Property-Value $_ 'traversalObservation'
        if ($null -eq $observation) { return }
        $handlerKey = [string](Property-Value $_ 'handlerFactId')
        if ([string]::IsNullOrWhiteSpace($handlerKey)) { $handlerKey = [string](Property-Value $_ 'chainId') }
        if ([string]::IsNullOrWhiteSpace($handlerKey)) { $handlerKey = "anonymous-$($seenHandlers.Count)" }
        if ($seenHandlers.Add($handlerKey)) { $observation }
    })
    if ($observations.Count -eq 0) {
        return [pscustomobject]@{ Available = $false; Complete = $null; TerminalCount = $null; MinimumDistance = $null; LimitReasons = @(); PathTruncated = $null; PathReasons = @() }
    }
    $availableObservations = @($observations | Where-Object {
        $available = Property-Value $_ 'terminalReachabilityAvailable'
        if ($null -ne $available) { return $available -eq $true }
        return $null -ne (Property-Value $_ 'terminalReachabilityComplete')
    })
    if ($availableObservations.Count -ne $observations.Count) {
        return [pscustomobject]@{ Available = $false; Complete = $null; TerminalCount = $null; MinimumDistance = $null; LimitReasons = @(); PathTruncated = @($observations | Where-Object { Property-Value $_ 'pathEnumerationTruncated' }).Count -gt 0; PathReasons = @($observations | ForEach-Object { Values (Property-Value $_ 'pathEnumerationTruncationReasons') } | Select-Object -Unique | Sort-Object) }
    }
    $minimums = @($observations | ForEach-Object { Property-Value $_ 'minimumTerminalDistance' } | Where-Object { $null -ne $_ })
    $terminalIds = @($observations | ForEach-Object { Values (Property-Value $_ 'reachableTerminalIds') } | Where-Object { $_ } | Select-Object -Unique | Sort-Object)
    return [pscustomobject]@{
        Available = $true
        Complete = @($observations | Where-Object { !(Property-Value $_ 'terminalReachabilityComplete') }).Count -eq 0
        TerminalCount = $terminalIds.Count
        MinimumDistance = if ($minimums.Count -gt 0) { [int](($minimums | Measure-Object -Minimum).Minimum) } else { $null }
        LimitReasons = @($observations | ForEach-Object { Values (Property-Value $_ 'terminalReachabilityLimitReasons') } | Select-Object -Unique | Sort-Object)
        PathTruncated = @($observations | Where-Object { Property-Value $_ 'pathEnumerationTruncated' }).Count -gt 0
        PathReasons = @($observations | ForEach-Object { Values (Property-Value $_ 'pathEnumerationTruncationReasons') } | Select-Object -Unique | Sort-Object)
    }
}

$requestedPageIds = @($PageId | ForEach-Object { $_.Trim() } | Select-Object -Unique)
if ($requestedPageIds.Count -lt 1 -or $requestedPageIds.Count -gt 2 -or
    @($requestedPageIds | Where-Object { $_ -cnotmatch '^page-[0-9]{3,}$' }).Count -ne 0) {
    throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_PAGE_SELECTION_INVALID'
}

$context = Get-FocusedWebFormsClaudeEvidenceContext $ReviewRoot $TraceMapRoot
$root = $context.Root
$receipt = Read-FocusedWebFormsBoundedJson (Join-Path $root 'run-receipt.json') 16MB 'WEBFORMS_TARGETED_DEPTH_COMPARE_RECEIPT_UNAVAILABLE'
$pageContexts = @($requestedPageIds | ForEach-Object {
    $pageId = $_
    $handoffRelativePath = "workbench/$pageId.handoff.json"
    Assert-FocusedWebFormsReceiptedArtifact $receipt 'workbench' $root $handoffRelativePath
    $handoff = Read-FocusedWebFormsBoundedJson (Join-Path $root $handoffRelativePath) 32MB 'WEBFORMS_TARGETED_DEPTH_COMPARE_HANDOFF_UNAVAILABLE'
    $filePath = [string](Property-Value (Property-Value $handoff 'subject') 'filePath')
    [pscustomobject]@{ PageId = $pageId; Handoff = $handoff; RequestId = Get-SurfaceRequestId $filePath }
})
$expectedRequestIds = @($pageContexts.RequestId | Sort-Object)
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
    @($diagnosticPacket.surfaceSelection.items | Where-Object { $_.status -ne 'matched' }).Count -ne 0 -or
    ((@($diagnosticPacket.surfaceSelection.items.requestId | Sort-Object) -join "`n") -cne ($expectedRequestIds -join "`n")) -or
    @($diagnosticPacket.surfaceSelection.items.requestId | Select-Object -Unique).Count -ne $requestedPageIds.Count) {
    throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_PACKET_INVALID'
}
$baselinePacketPath = Join-Path $diagnosticRoot 'baseline-depth-8/webforms-modernization.json'
$baselinePacket = if (Test-Path -LiteralPath $baselinePacketPath -PathType Leaf) {
    Read-FocusedWebFormsBoundedJson $baselinePacketPath 128MB 'WEBFORMS_TARGETED_DEPTH_COMPARE_BASELINE_PACKET_UNAVAILABLE'
} else { $null }
if ($null -ne $baselinePacket -and ($baselinePacket.schemaVersion -ne 'webforms-modernization-packet.v1' -or
    $baselinePacket.surfaceSelection.items.Count -ne $requestedPageIds.Count -or
    @($baselinePacket.surfaceSelection.items | Where-Object { $_.status -ne 'matched' }).Count -ne 0 -or
    ((@($baselinePacket.surfaceSelection.items.requestId | Sort-Object) -join "`n") -cne ($expectedRequestIds -join "`n")) -or
    @($baselinePacket.surfaceSelection.items.requestId | Select-Object -Unique).Count -ne $requestedPageIds.Count)) {
    throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_BASELINE_PACKET_INVALID'
}
if ($null -ne $baselinePacket) {
    $baselinePacketSources = @($baselinePacket.sources | ForEach-Object { "$($_.scanId)|$($_.commitSha)" } | Sort-Object)
    $diagnosticPacketSources = @($diagnosticPacket.sources | ForEach-Object { "$($_.scanId)|$($_.commitSha)" } | Sort-Object)
    if (($baselinePacketSources -join "`n") -cne ($diagnosticPacketSources -join "`n")) {
        throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_PROVENANCE_MISMATCH'
    }
}

$aggregateBaselineKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$aggregateDiagnosticKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$baselineDepthChains = 0
$diagnosticDepthChains = 0
for ($index = 0; $index -lt $requestedPageIds.Count; $index++) {
    $pageContext = $pageContexts[$index]
    $pageId = $pageContext.PageId
    $baseline = $pageContext.Handoff
    $baselineSources = @($baseline.packet.sources | ForEach-Object { "$($_.scanId)|$($_.commitSha)" } | Sort-Object)
    $diagnosticSources = @($diagnosticPacket.sources | ForEach-Object { "$($_.scanId)|$($_.commitSha)" } | Sort-Object)
    if (($baselineSources -join "`n") -cne ($diagnosticSources -join "`n")) {
        throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_PROVENANCE_MISMATCH'
    }
    $selection = @($diagnosticPacket.surfaceSelection.items | Where-Object { $_.requestId -ceq $pageContext.RequestId })
    if ($selection.Count -ne 1) { throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_PACKET_IDENTITY_INVALID' }
    $selection = $selection[0]
    $surfaceIds = @(Values $selection.surfaceIds)
    $diagnosticChains = @($diagnosticPacket.eventChains | Where-Object { $_.surfaceId -in $surfaceIds })
    $diagnosticBoundaries = @($diagnosticPacket.downstreamBoundaries | Where-Object { $_.surfaceId -in $surfaceIds })
    if ($null -ne $baselinePacket) {
        $baselineSelection = @($baselinePacket.surfaceSelection.items | Where-Object { $_.requestId -ceq $pageContext.RequestId })
        if ($baselineSelection.Count -ne 1) { throw 'WEBFORMS_TARGETED_DEPTH_COMPARE_BASELINE_IDENTITY_INVALID' }
        $baselineSelection = $baselineSelection[0]
        $baselineSurfaceIds = @(Values $baselineSelection.surfaceIds)
        $baselineChains = @($baselinePacket.eventChains | Where-Object { $_.surfaceId -in $baselineSurfaceIds })
        $baselineBoundaries = @($baselinePacket.downstreamBoundaries | Where-Object { $_.surfaceId -in $baselineSurfaceIds })
    } else {
        $baselineChains = @(Values $baseline.eventChains)
        $baselineBoundaries = @(Values $baseline.downstreamBoundaries)
    }
    $baselineKeys = Get-RenderedBoundaryTupleKeys $baselineBoundaries
    $diagnosticKeys = Get-RenderedBoundaryTupleKeys $diagnosticBoundaries
    foreach ($key in $baselineKeys) { [void]$aggregateBaselineKeys.Add($key) }
    foreach ($key in $diagnosticKeys) { [void]$aggregateDiagnosticKeys.Add($key) }
    $baselinePageDepth = Get-DepthChainCount $baselineChains
    $diagnosticPageDepth = Get-DepthChainCount $diagnosticChains
    $baselineDepthChains += $baselinePageDepth
    $diagnosticDepthChains += $diagnosticPageDepth
    $added = @($diagnosticKeys | Where-Object { !$baselineKeys.Contains($_) }).Count
    $lost = @($baselineKeys | Where-Object { !$diagnosticKeys.Contains($_) }).Count
    Write-Output "page=$pageId|depth8Chains=$($baselineChains.Count)|depth10Chains=$($diagnosticChains.Count)|depth8DepthTruncated=$baselinePageDepth|depth10DepthTruncated=$diagnosticPageDepth|depth8RenderedBoundaryTuples=$($baselineKeys.Count)|depth10RenderedBoundaryTuples=$($diagnosticKeys.Count)|addedRenderedBoundaryTuples=$added|lostRenderedBoundaryTuples=$lost"
    $baselineReachability = Get-ReachabilitySummary $baselineChains
    $diagnosticReachability = Get-ReachabilitySummary $diagnosticChains
    Write-Output "reachability=$pageId|depth8Available=$($baselineReachability.Available)|depth8Complete=$($baselineReachability.Complete)|depth8DistinctTerminals=$($baselineReachability.TerminalCount)|depth8MinimumDistance=$($baselineReachability.MinimumDistance)|depth8LimitReasons=$($baselineReachability.LimitReasons -join ',')|depth8PathDetailTruncated=$($baselineReachability.PathTruncated)|depth8PathDetailReasons=$($baselineReachability.PathReasons -join ',')|depth10Complete=$($diagnosticReachability.Complete)|depth10DistinctTerminals=$($diagnosticReachability.TerminalCount)|depth10MinimumDistance=$($diagnosticReachability.MinimumDistance)|depth10LimitReasons=$($diagnosticReachability.LimitReasons -join ',')|depth10PathDetailTruncated=$($diagnosticReachability.PathTruncated)|depth10PathDetailReasons=$($diagnosticReachability.PathReasons -join ',')"
}

$aggregateAdded = @($aggregateDiagnosticKeys | Where-Object { !$aggregateBaselineKeys.Contains($_) }).Count
$aggregateLost = @($aggregateBaselineKeys | Where-Object { !$aggregateDiagnosticKeys.Contains($_) }).Count
Write-Output 'webformsTargetedDepthComparison=completed'
Write-Output "pageIds=$($requestedPageIds -join ',')"
Write-Output "depth8DepthTruncated=$baselineDepthChains"
Write-Output "depth10DepthTruncated=$diagnosticDepthChains"
Write-Output 'renderedBoundaryTupleBasis=boundaryKind+boundaryTargetId+terminalEvidenceId;path-detail-output-not-terminal-inventory'
Write-Output "depth8RenderedBoundaryTuples=$($aggregateBaselineKeys.Count)"
Write-Output "depth10RenderedBoundaryTuples=$($aggregateDiagnosticKeys.Count)"
Write-Output "renderedBoundaryTupleDelta=added:$aggregateAdded|lost:$aggregateLost"
Write-Output "diagnosticPacket=$(Join-Path $diagnosticRoot 'webforms-modernization.json')"
Write-Output 'nonClaim=distinct-evidence-tuples-not-runtime-operations;partial-results-do-not-prove-absence'
