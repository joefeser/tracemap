# EDIT ONLY THIS BLOCK.
$OutputRoot = 'C:\work\tracemap-output'
$ReportPath = '' # Optional: full path to one webforms-modernization.json file.
# END EDIT BLOCK.

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    if (-not (Test-Path -LiteralPath $OutputRoot -PathType Container)) {
        throw "The configured output root was not found: $OutputRoot"
    }
    $latest = Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'webforms-page-list-*' |
        ForEach-Object {
            $candidate = Join-Path $_.FullName 'webforms-modernization.json'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { Get-Item -LiteralPath $candidate }
        } |
        Sort-Object -Property LastWriteTimeUtc, FullName -Descending |
        Select-Object -First 1
    if ($null -eq $latest) {
        throw "No webforms-page-list-*/webforms-modernization.json report was found under: $OutputRoot"
    }
    $ReportPath = $latest.FullName
}

if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
    throw "The configured report was not found: $ReportPath"
}

$packet = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
if ($packet.summary.truncated) {
    throw 'The selected report is truncated. Complete the page-list report before triaging unresolved chains.'
}

$chains = @($packet.eventChains)
$gaps = @($packet.gaps)
$aliasBySurface = @{}
foreach ($item in @($packet.surfaceSelection.items)) {
    foreach ($surfaceId in @($item.surfaceIds)) { $aliasBySurface[$surfaceId] = $item.alias }
}

function Get-PageAlias([object]$chain) {
    if ($aliasBySurface.ContainsKey($chain.surfaceId)) { return $aliasBySurface[$chain.surfaceId] }
    return 'page-alias-unavailable'
}

function Get-LinkedGapClassifications([object]$chain) {
    $support = @{}
    foreach ($id in @($chain.supportingFactIds)) { $support[$id] = $true }
    return @($gaps | Where-Object {
        $linked = $false
        foreach ($id in @($_.supportingFactIds)) {
            if ($support.ContainsKey($id)) { $linked = $true; break }
        }
        $linked
    } | ForEach-Object { $_.classification } | Sort-Object -Unique)
}

$handlerUnavailable = @($chains | Where-Object { [string]::IsNullOrWhiteSpace($_.handlerFactId) })
$resolvedWithoutTerminal = @($chains | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_.handlerFactId) -and
    [string]::IsNullOrWhiteSpace($_.terminalKind)
})

$rows = foreach ($chain in $resolvedWithoutTerminal) {
    $observation = $chain.traversalObservation
    $stopState = if ($null -ne $observation -and -not [string]::IsNullOrWhiteSpace($observation.stopState)) {
        $observation.stopState
    }
    elseif ([string]::IsNullOrWhiteSpace($chain.legacyPathId)) {
        'traversal-observation-unavailable'
    }
    elseif (@($chain.supportingEdgeIds).Count -eq 0) {
        'no-observed-downstream-edge'
    }
    else {
        'observed-downstream-without-supported-terminal'
    }
    [pscustomobject]@{
        Alias = Get-PageAlias $chain
        StopState = $stopState
        ReachedNodeCount = if ($null -eq $observation) { 0 } else { [int]$observation.reachedNodeCount }
        TraversedEdgeCount = if ($null -eq $observation) { 0 } else { [int]$observation.traversedEdgeCount }
        DownstreamEdgeCount = if ($null -eq $observation) { 0 } else { [int]$observation.downstreamEdgeCount }
        CallEvidenceState = if ($null -eq $observation -or [string]::IsNullOrWhiteSpace($observation.callEvidenceState)) { 'unavailable' } else { $observation.callEvidenceState }
        HandlerOwnedCallEvidenceCount = if ($null -eq $observation) { 0 } else { [int]$observation.handlerOwnedCallEvidenceCount }
        TraversalTruncated = if ($null -eq $observation) { $false } else { [bool]$observation.truncated }
        Classification = $chain.classification
        EvidenceTiers = @($chain.evidenceTiers)
        RuleIds = @($chain.ruleIds)
        LinkedGapClassifications = @(Get-LinkedGapClassifications $chain)
    }
}

Write-Host 'focused-webforms-unresolved-triage=completed'
Write-Host "reportFile=$([System.IO.Path]::GetFileName($ReportPath))"
Write-Host 'truncated=false'
Write-Host "totalEventChains=$($chains.Count)"
Write-Host "handlerUnavailableChains=$($handlerUnavailable.Count)"
Write-Host "handlerResolvedTerminalUnavailableChains=$($resolvedWithoutTerminal.Count)"

foreach ($group in @($rows | Group-Object -Property StopState | Sort-Object -Property Name)) {
    $aliases = @($group.Group.Alias | Sort-Object -Unique)
    Write-Host "stopState=$($group.Name)|chains=$($group.Count)|pages=$($aliases.Count)"
    Write-Host "stopStateAliases-$($group.Name)=$($aliases -join ',')"
    Write-Host "stopStateTraversal-$($group.Name)=reachedNodes:$([long](@($group.Group | Measure-Object -Property ReachedNodeCount -Sum).Sum))|traversedEdges:$([long](@($group.Group | Measure-Object -Property TraversedEdgeCount -Sum).Sum))|downstreamEdges:$([long](@($group.Group | Measure-Object -Property DownstreamEdgeCount -Sum).Sum))|truncatedChains:$(@($group.Group | Where-Object TraversalTruncated).Count)"
}
foreach ($group in @($rows | Group-Object -Property CallEvidenceState | Sort-Object -Property Name)) {
    $aliases = @($group.Group.Alias | Sort-Object -Unique)
    $ownedCallEdges = [long](@($group.Group | Measure-Object -Property HandlerOwnedCallEvidenceCount -Sum).Sum)
    Write-Host "callEvidenceState=$($group.Name)|chains=$($group.Count)|handlerOwnedCallEdges=$ownedCallEdges|pages=$($aliases.Count)"
    Write-Host "callEvidenceStateAliases-$($group.Name)=$($aliases -join ',')"
}
foreach ($group in @($rows | Group-Object -Property Classification | Sort-Object -Property Name)) {
    Write-Host "resolvedNoTerminalClassification=$($group.Name)|count=$($group.Count)"
}

$tierRows = foreach ($row in $rows) {
    foreach ($tier in $row.EvidenceTiers) { [pscustomobject]@{ Alias = $row.Alias; Tier = $tier } }
}
foreach ($group in @($tierRows | Group-Object -Property Tier | Sort-Object -Property Name)) {
    Write-Host "resolvedNoTerminalEvidenceTier=$($group.Name)|chains=$($group.Count)"
}

$ruleRows = foreach ($row in $rows) {
    foreach ($ruleId in $row.RuleIds) { [pscustomobject]@{ Alias = $row.Alias; RuleId = $ruleId } }
}
foreach ($group in @($ruleRows | Group-Object -Property RuleId | Sort-Object -Property Name)) {
    Write-Host "resolvedNoTerminalRule=$($group.Name)|chains=$($group.Count)"
}

$linkedGapRows = foreach ($row in $rows) {
    foreach ($classification in $row.LinkedGapClassifications) { $classification }
}
if (@($linkedGapRows).Count -eq 0) {
    Write-Host 'resolvedNoTerminalLinkedGap=none|count=0'
}
else {
    foreach ($group in @($linkedGapRows | Group-Object | Sort-Object -Property Name)) {
        Write-Host "resolvedNoTerminalLinkedGap=$($group.Name)|chains=$($group.Count)"
    }
}

$unavailableAliases = @($handlerUnavailable | ForEach-Object { Get-PageAlias $_ } | Sort-Object -Unique)
Write-Host "handlerUnavailablePages=$($unavailableAliases.Count)"
Write-Host "handlerUnavailablePageAliases=$($unavailableAliases -join ',')"
$unavailableGapRows = foreach ($chain in $handlerUnavailable) {
    foreach ($classification in @(Get-LinkedGapClassifications $chain)) { $classification }
}
if (@($unavailableGapRows).Count -eq 0) {
    Write-Host 'handlerUnavailableLinkedGap=none|count=0'
}
else {
    foreach ($group in @($unavailableGapRows | Group-Object | Sort-Object -Property Name)) {
        Write-Host "handlerUnavailableLinkedGap=$($group.Name)|chains=$($group.Count)"
    }
}

Write-Host 'stopStateMeaning=bounded-static-traversal-counts-are-not-proof-of-runtime-presence-or-absence'
Write-Host 'callEvidenceMeaning=handler-owned-call-evidence-unjoined-distinguishes-retained-call-facts-from-no-handler-owned-call-evidence-retained'
Write-Host 'nonClaim=static-evidence-does-not-prove-runtime-execution-reachability-or-successful-binding'
