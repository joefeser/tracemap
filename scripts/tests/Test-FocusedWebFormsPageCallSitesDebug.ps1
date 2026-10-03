$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$entry = Join-Path $scripts 'Debug-FocusedWebFormsPageCallSites.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-call-sites-' + [Guid]::NewGuid().ToString('N'))

try {
    [IO.Directory]::CreateDirectory((Join-Path $temp 'workbench')) | Out-Null
    $handoff = [ordered]@{
        schemaVersion = 'webforms-application-page-handoff.v1'
        claimLevel = 'local-only'
        pageId = 'page-011'
        counts = [ordered]@{ retainedCalls = 1; normalizedCallSites = 1 }
        eventChains = @(
            [ordered]@{
                chainId = 'chain-private'
                handlerId = 'handler-private'
                handlerSymbol = 'Handler_Click'
                traversalStopState = 'bounded-traversal-truncated'
                callEvidence = @(
                    [ordered]@{
                        callSiteId = '7c9f000000004d8'
                        calleeName = 'Insert'
                        callKind = 'SyntaxInvocation'
                        resolution = 'syntax-only'
                        evidence = [ordered]@{ filePath = 'Pages/Example.aspx.vb'; startLine = 42; endLine = 42 }
                    }
                )
            }
        )
    }
    [IO.File]::WriteAllText(
        (Join-Path $temp 'workbench/page-011.handoff.json'),
        (($handoff | ConvertTo-Json -Depth 20) + "`n"),
        [Text.UTF8Encoding]::new($false))

    $output = @(& $entry -ReviewRoot $temp -PageId page-011 -CallSitePattern '7c9f*4d8')
    if ($output -notcontains 'webformsPageCallSites=valid' -or
        $output -notcontains 'chainCount=1' -or
        $output -notcontains 'retainedCalls=1' -or
        $output -notcontains 'matchedCallFacts=1' -or
        @($output | Where-Object { $_ -like 'callSite=7c9f000000004d8;*' }).Count -ne 1) {
        throw 'Focused Web Forms call-site diagnostic output was incomplete.'
    }

    $emptyHandoff = [ordered]@{
        schemaVersion = 'webforms-application-page-handoff.v1'
        claimLevel = 'local-only'
        pageId = 'page-012'
        counts = [ordered]@{ retainedCalls = 0; normalizedCallSites = 0 }
        eventChains = @(
            [ordered]@{
                chainId = 'empty-chain'
                handlerId = 'empty-handler'
                handlerSymbol = 'Empty_Click'
                traversalStopState = 'no-observed-downstream-edge'
                callEvidence = @()
            }
        )
    }
    [IO.File]::WriteAllText(
        (Join-Path $temp 'workbench/page-012.handoff.json'),
        (($emptyHandoff | ConvertTo-Json -Depth 20) + "`n"),
        [Text.UTF8Encoding]::new($false))
    $emptyOutput = @(& $entry -ReviewRoot $temp -PageId page-012)
    if ($emptyOutput -notcontains 'materializedCalls=0' -or
        $emptyOutput -notcontains 'diagnosis=no-call-evidence-in-this-handoff;check-review-root-and-report-local-page-alias') {
        throw 'Focused Web Forms call-site diagnostic did not explain an empty handoff.'
    }
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

Write-Output 'PASS focused Web Forms page call-site diagnostic'
