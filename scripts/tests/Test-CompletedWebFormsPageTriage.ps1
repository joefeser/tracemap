$ErrorActionPreference = 'Stop'
$temp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
$scriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'Triage-CompletedWebFormsPages.ps1'
try {
    $dir = Join-Path $temp 'webforms-depth-comparison-fixture/depth-8'
    [void][IO.Directory]::CreateDirectory($dir)
    $packet = @{
        sources=@(@{commitSha='synthetic'})
        summary=@{truncated=$true}
        surfaceSelection=@{items=@(@{alias='page-004';surfaceIds=@('s')},@{alias='page-020';surfaceIds=@('empty')})}
        eventChains=@(
            @{surfaceId='s';bindingFactId='PRIVATE-binding'},
            @{surfaceId='s';handlerFactId='h';terminalKind='sql-query'},
            @{surfaceId='s';handlerFactId='h';traversalObservation=@{truncated=$true;stopState='bounded-traversal-truncated';callEvidenceState='call-evidence-observation-incomplete';handlerOwnedCallEvidenceCount=1};pathEvidence=@(@{evidenceId='PRIVATE-node';evidenceKind='path-node'})},
            @{surfaceId='s';handlerFactId='h';traversalObservation=@{downstreamEdgeCount=2;stopState='observed-downstream-without-supported-terminal';callEvidenceState='joined-downstream-edge-observed';handlerOwnedCallEvidenceCount=2}},
            @{surfaceId='s';handlerFactId='h';traversalObservation=@{downstreamEdgeCount=0;stopState='no-observed-downstream-edge';callEvidenceState='handler-owned-call-evidence-unjoined';handlerOwnedCallEvidenceCount=3}},
            @{surfaceId='s';handlerFactId='h';traversalObservation=$null}
        )
        gaps=@(
            @{classification='TruncatedByLimit';scopeKind='legacy-flow';scopeId='PRIVATE-node';truncationReason='depth'},
            @{classification='TruncatedByLimit';scopeKind='legacy-flow';scopeId='unrelated';truncationReason='work'},
            @{supportingFactIds=@('PRIVATE-binding');classification='HandlerUnavailable';ruleId='legacy.webforms.handler-resolution.v1'},
            @{supportingFactIds=@('unrelated');classification='GeneratedFileMissing';ruleId='PRIVATE-rule'},
            @{supportingFactIds=@('PRIVATE-binding');classification='PRIVATE-kind';ruleId='PRIVATE-rule'}
        )
    }
    [IO.File]::WriteAllText((Join-Path $dir 'webforms-modernization.json'), ($packet | ConvertTo-Json -Depth 20))
    $output = (& $scriptPath -OutputRoot $temp 6>&1 | Out-String)
    foreach ($expected in @(
        'chains=6|terminal=1|noRetainedEvents=False|handlerUnavailable=1|truncated=1|downstreamNoTerminal=1|noEdge=1|observationUnavailable=1',
        'page=page-020|hasTerminal=False|chains=0|terminal=0|noRetainedEvents=True',
        'stopStates=bounded-traversal-truncated:1,no-observed-downstream-edge:1,observed-downstream-without-supported-terminal:1',
        'callEvidenceStates=call-evidence-observation-incomplete:1,handler-owned-call-evidence-unjoined:1,joined-downstream-edge-observed:1|handlerOwnedCallEvidence=6',
        'handlerFocus=page-004|gap=HandlerUnavailable|rule=legacy.webforms.handler-resolution.v1|count=1',
        'gap=other-retained-gap|rule=other-rule|count=1',
        'page=page-004|nodeAssociatedReasons=depth|basis=exact-retained-node-not-proof-of-chain-stop',
        'page=page-020|nodeAssociatedReasons=not-established',
        'handlerFocus=page-026|exactBindingLinkedGaps=0|cause=not-established'
    )) { if (-not $output.Contains($expected)) { throw "Missing: $expected" } }
    if ($output.Contains('PRIVATE') -or $output.Contains('gap=GeneratedFileMissing')) { throw 'Private or unrelated evidence disclosed.' }
    $dir10 = Join-Path $temp 'webforms-depth-comparison-fixture/depth-10'
    [void][IO.Directory]::CreateDirectory($dir10)
    $path10 = Join-Path $dir10 'webforms-modernization.json'
    [IO.File]::WriteAllText($path10, ($packet | ConvertTo-Json -Depth 20))
    $compare = Join-Path (Split-Path -Parent $PSScriptRoot) 'Compare-CompletedWebFormsPageTriage.ps1'
    $paired = (& $compare -OutputRoot $temp 6>&1 | Out-String)
    if (-not $paired.Contains('provenance=matched') -or -not $paired.Contains('depth=10') -or $paired.Contains('PRIVATE')) { throw 'Pair comparison failed.' }
    $packet.sources[0].commitSha='mismatch'
    [IO.File]::WriteAllText($path10, ($packet | ConvertTo-Json -Depth 20))
    $rejected = $false
    try { & $compare -OutputRoot $temp } catch { $rejected = $_.Exception.Message -like '*Provenance mismatch*' }
    if (-not $rejected) { throw 'Mismatched sources accepted.' }
    Write-Host 'PASS targeted retained triage: discovery, optional fields, buckets, exact links, privacy'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
