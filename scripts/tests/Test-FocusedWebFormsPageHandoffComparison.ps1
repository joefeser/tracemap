$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$subject = Join-Path $scripts 'Compare-FocusedWebFormsPageHandoff.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-page-handoff-compare-' + [Guid]::NewGuid().ToString('N'))

function Write-Handoff([string]$Root, [int]$PathCount) {
    $workbench = Join-Path $Root 'workbench'
    [IO.Directory]::CreateDirectory($workbench) | Out-Null
    $paths = if ($PathCount -gt 0) { @(0..($PathCount - 1) | ForEach-Object { @{ factId = "path-$_" } }) } else { @() }
    $handoff = [ordered]@{
        schemaVersion = 'webforms-application-page-handoff.v1'
        pageId = 'page-011'
        eventChains = @([ordered]@{
            chainId = 'chain-one'; handlerFactId = 'handler-one'; classification = 'StrongStaticPath'
            terminalKind = 'sql-query'; traversalStopState = 'supported-terminal-reached'
            terminalReachabilityAvailable = $true; terminalReachabilityComplete = $true
            distinctReachableTerminalCount = 1; reachableTerminalIds = @('terminal-one')
            minimumTerminalDistance = 12; pathEnumerationTruncated = $true
            pathEvidence = $paths; callEvidence = @(@{ callEvidenceId = 'call-one' })
            supportingFactIds = @('fact-one','fact-two'); supportingEdgeIds = @('edge-one')
        })
        downstreamBoundaries = @([ordered]@{
            boundaryId = 'boundary-one'; chainId = 'chain-one'; handlerId = 'handler-one'
            boundaryCategory = 'database'; boundaryKind = 'sql-query'; boundaryTargetId = 'target-one'
            terminalEvidenceId = 'terminal-evidence-one'; classification = 'StrongStaticPath'; legacyPathId = 'legacy-one'
            pathEvidence = @(); supportingFactIds = @(); supportingEdgeIds = @()
        })
    }
    [IO.File]::WriteAllText((Join-Path $workbench 'page-011.handoff.json'), (($handoff | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
}

try {
    $prior = Join-Path $temp 'prior'
    $current = Join-Path $temp 'current'
    Write-Handoff $prior 4
    Write-Handoff $current 1
    $output = @(& $subject -PriorReviewRoot $prior -ReviewRoot $current -PageId page-011)
    foreach ($expected in @(
        'webFormsPageHandoffComparison=valid',
        'EventChains=prior:1|current:1|delta:0',
        'Boundaries=prior:1|current:1|delta:0',
        'PathEvidence=prior:4|current:1|delta:-3',
        'CallEvidence=prior:1|current:1|delta:0',
        'retainedOutcomeDifferences=0',
        'classification=stable-retained-outcomes-with-reduced-path-detail')) {
        if ($expected -notin $output) { throw "Page handoff comparison omitted: $expected" }
    }
    Write-Host 'PASS focused Web Forms page handoff comparison'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
