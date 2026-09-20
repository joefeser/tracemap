[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PriorReviewRoot,
    [Parameter(Mandatory)][string]$ReviewRoot,
    [Parameter(Mandatory)][ValidatePattern('^page-[0-9]{3,}$')][string]$PageId
)

$ErrorActionPreference = 'Stop'

function Property-Value([object]$Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Values([object]$Value) {
    if ($null -eq $Value) { return @() }
    return @($Value)
}

function Read-Handoff([string]$Root) {
    $path = Join-Path ([IO.Path]::GetFullPath($Root)) "workbench/$PageId.handoff.json"
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "WEBFORMS_PAGE_HANDOFF_COMPARE_INPUT_UNAVAILABLE:$path"
    }
    $file = Get-Item -LiteralPath $path
    if ($file.Length -le 0 -or $file.Length -gt 512MB) {
        throw "WEBFORMS_PAGE_HANDOFF_COMPARE_INPUT_LIMIT:$path"
    }
    $handoff = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -Depth 100
    if ($handoff.schemaVersion -ne 'webforms-application-page-handoff.v1' -or $handoff.pageId -ne $PageId) {
        throw "WEBFORMS_PAGE_HANDOFF_COMPARE_SCHEMA_INVALID:$path"
    }
    return [pscustomobject]@{ Path = $path; Bytes = [long]$file.Length; Handoff = $handoff }
}

function Measure-Handoff([object]$InputHandoff) {
    $handoff = $InputHandoff.Handoff
    $chains = @(Values $handoff.eventChains)
    $boundaries = @(Values $handoff.downstreamBoundaries)
    $available = @($chains | Where-Object { (Property-Value $_ 'terminalReachabilityAvailable') -eq $true }).Count
    $complete = @($chains | Where-Object {
        (Property-Value $_ 'terminalReachabilityAvailable') -eq $true -and
        (Property-Value $_ 'terminalReachabilityComplete') -eq $true
    }).Count
    $incomplete = @($chains | Where-Object {
        (Property-Value $_ 'terminalReachabilityAvailable') -eq $true -and
        (Property-Value $_ 'terminalReachabilityComplete') -eq $false
    }).Count
    $terminalIds = @($chains | ForEach-Object { Values (Property-Value $_ 'reachableTerminalIds') } |
        Where-Object { $_ } | Sort-Object -Unique)
    $reportedTerminalSum = (@($chains | ForEach-Object {
        $value = Property-Value $_ 'distinctReachableTerminalCount'
        if ($null -ne $value) { [int]$value }
    }) | Measure-Object -Sum).Sum
    if ($null -eq $reportedTerminalSum) { $reportedTerminalSum = 0 }

    $chainOutcomes = @($chains | ForEach-Object {
        @(
            [string](Property-Value $_ 'chainId'),
            [string](Property-Value $_ 'handlerFactId'),
            [string](Property-Value $_ 'classification'),
            [string](Property-Value $_ 'terminalKind'),
            [string](Property-Value $_ 'traversalStopState'),
            [string](Property-Value $_ 'terminalReachabilityAvailable'),
            [string](Property-Value $_ 'terminalReachabilityComplete'),
            [string](Property-Value $_ 'distinctReachableTerminalCount'),
            [string](Property-Value $_ 'minimumTerminalDistance'),
            (@(Values (Property-Value $_ 'reachableTerminalIds')) | Sort-Object) -join ','
        ) -join '|'
    } | Sort-Object)
    $boundaryOutcomes = @($boundaries | ForEach-Object {
        @(
            [string](Property-Value $_ 'boundaryId'),
            [string](Property-Value $_ 'chainId'),
            [string](Property-Value $_ 'handlerId'),
            [string](Property-Value $_ 'boundaryCategory'),
            [string](Property-Value $_ 'boundaryKind'),
            [string](Property-Value $_ 'boundaryTargetId'),
            [string](Property-Value $_ 'terminalEvidenceId'),
            [string](Property-Value $_ 'classification'),
            [string](Property-Value $_ 'legacyPathId')
        ) -join '|'
    } | Sort-Object)

    $pathOwners = @($chains) + @($boundaries)

    return [pscustomobject]@{
        Bytes = [long]$InputHandoff.Bytes
        EventChains = $chains.Count
        Boundaries = $boundaries.Count
        PathEvidence = [int]((@($pathOwners | ForEach-Object { @(Values (Property-Value $_ 'pathEvidence')).Count }) | Measure-Object -Sum).Sum)
        CallEvidence = [int]((@($chains | ForEach-Object { @(Values (Property-Value $_ 'callEvidence')).Count }) | Measure-Object -Sum).Sum)
        SupportingFacts = [int]((@($pathOwners | ForEach-Object { @(Values (Property-Value $_ 'supportingFactIds')).Count }) | Measure-Object -Sum).Sum)
        SupportingEdges = [int]((@($pathOwners | ForEach-Object { @(Values (Property-Value $_ 'supportingEdgeIds')).Count }) | Measure-Object -Sum).Sum)
        TerminalInventoryAvailableChains = $available
        TerminalInventoryCompleteChains = $complete
        TerminalInventoryIncompleteChains = $incomplete
        TerminalInventoryUnavailableChains = $chains.Count - $available
        DistinctTerminalIds = if ($terminalIds.Count -gt 0) { $terminalIds.Count } else { $null }
        ReportedTerminalCountSum = [int]$reportedTerminalSum
        PathDetailTruncatedChains = @($chains | Where-Object { (Property-Value $_ 'pathEnumerationTruncated') -eq $true }).Count
        ChainOutcomes = $chainOutcomes
        BoundaryOutcomes = $boundaryOutcomes
    }
}

$prior = Measure-Handoff (Read-Handoff $PriorReviewRoot)
$current = Measure-Handoff (Read-Handoff $ReviewRoot)
$chainOutcomeDifferences = @(Compare-Object $prior.ChainOutcomes $current.ChainOutcomes).Count
$boundaryOutcomeDifferences = @(Compare-Object $prior.BoundaryOutcomes $current.BoundaryOutcomes).Count
$retainedOutcomeDifferences = $chainOutcomeDifferences + $boundaryOutcomeDifferences

Write-Output 'webFormsPageHandoffComparison=valid'
Write-Output "pageId=$PageId"
foreach ($name in @(
    'Bytes','EventChains','Boundaries','PathEvidence','CallEvidence','SupportingFacts','SupportingEdges',
    'TerminalInventoryAvailableChains','TerminalInventoryCompleteChains','TerminalInventoryIncompleteChains',
    'TerminalInventoryUnavailableChains','DistinctTerminalIds','ReportedTerminalCountSum','PathDetailTruncatedChains')) {
    $priorValue = Property-Value $prior $name
    $currentValue = Property-Value $current $name
    $delta = if ($priorValue -is [ValueType] -and $currentValue -is [ValueType]) { [long]$currentValue - [long]$priorValue } else { 'unavailable' }
    Write-Output "$name=prior:$priorValue|current:$currentValue|delta:$delta"
}
Write-Output "chainOutcomeDifferences=$chainOutcomeDifferences"
Write-Output "boundaryOutcomeDifferences=$boundaryOutcomeDifferences"
Write-Output "retainedOutcomeDifferences=$retainedOutcomeDifferences"
Write-Output "classification=$(if ($retainedOutcomeDifferences -eq 0 -and $current.PathEvidence -lt $prior.PathEvidence) { 'stable-retained-outcomes-with-reduced-path-detail' } elseif ($retainedOutcomeDifferences -eq 0) { 'stable-retained-outcomes' } else { 'retained-outcomes-changed-review-required' })"
Write-Output 'nonClaim=static-handoff-comparison-does-not-prove-runtime-equivalence-or-execution'
