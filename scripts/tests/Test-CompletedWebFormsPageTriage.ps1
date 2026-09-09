$ErrorActionPreference = 'Stop'
$temp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
$scriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'Triage-CompletedWebFormsPages.ps1'
try {
    $dir = Join-Path $temp 'webforms-depth-comparison-fixture/depth-8'
    [void][IO.Directory]::CreateDirectory($dir)
    $packet = @{
        summary=@{truncated=$true}
        surfaceSelection=@{items=@(@{alias='page-004';surfaceIds=@('s')},@{alias='page-020';surfaceIds=@('empty')})}
        eventChains=@(
            @{surfaceId='s';bindingFactId='PRIVATE-binding'},
            @{surfaceId='s';handlerFactId='h';terminalKind='sql-query'},
            @{surfaceId='s';handlerFactId='h';traversalObservation=@{truncated=$true}},
            @{surfaceId='s';handlerFactId='h';traversalObservation=@{downstreamEdgeCount=2}},
            @{surfaceId='s';handlerFactId='h';traversalObservation=@{downstreamEdgeCount=0}},
            @{surfaceId='s';handlerFactId='h';traversalObservation=$null}
        )
        gaps=@(
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
        'handlerFocus=page-004|gap=HandlerUnavailable|rule=legacy.webforms.handler-resolution.v1|count=1',
        'gap=other-retained-gap|rule=other-rule|count=1',
        'handlerFocus=page-026|exactBindingLinkedGaps=0|cause=not-established'
    )) { if (-not $output.Contains($expected)) { throw "Missing: $expected" } }
    if ($output.Contains('PRIVATE') -or $output.Contains('gap=GeneratedFileMissing')) { throw 'Private or unrelated evidence disclosed.' }
    Write-Host 'PASS targeted retained triage: discovery, optional fields, buckets, exact links, privacy'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
