[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-probe-summary-test-' + [guid]::NewGuid().ToString('N'))
try {
    [void][IO.Directory]::CreateDirectory((Join-Path $root 'probe'))
    $manifest = @{
        compiledInputProvenance = @{
            omittedInputCount = 0
            effectiveLimits = @{ maxTextLength = 4096 }
            outcomes = @(
                @{ outcome = 'admitted'; safeLocator = 'private-one'; rawFileSha256 = 'hash'; assemblyIdentity = 'private-identity'; gapKinds = @('UnboundSource') },
                @{ outcome = 'unreadable'; safeLocator = 'private-two'; rawFileSha256 = 'hash'; assemblyIdentity = ''; gapKinds = @('SystemReflectionMetadataReaderFailure') }
            )
        }
    }
    $receipt = @{ assemblyInventory = @(
        @{ path = 'private-one'; sha256 = 'other-hash'; disposition = 'selected' },
        @{ path = 'private-two'; sha256 = 'hash'; disposition = 'artifact-context-no-source-commit' }) }
    [IO.File]::WriteAllText((Join-Path $root 'probe/scan-manifest.json'), ($manifest | ConvertTo-Json -Depth 10))
    [IO.File]::WriteAllText((Join-Path $root 'publish-receipt.local.json'), ($receipt | ConvertTo-Json -Depth 10))
    $lines = @(& (Join-Path $TraceMapRoot 'scripts/wp.ps1') -OutputRoot $root)
    foreach ($expected in @('probeSelected=2', 'probeOutcomes=2', 'probeAdmitted=1',
            'probeNonadmitted=1', 'probeMissingIdentity=1', 'probeReady=False',
            'probeNonadmittedSelected=0', 'probeNonadmittedContext=1', 'probeTextLimit=4096',
            'probeOutcome.unreadable=1', 'probeGap.SystemReflectionMetadataReaderFailure=1')) {
        if ($lines -cnotcontains $expected) { throw 'WEBFORMS_PROBE_SUMMARY_TEST_FAILED' }
    }
    if (($lines -join "`n") -match 'private-') { throw 'WEBFORMS_PROBE_SUMMARY_PRIVACY_FAILED' }
    $manifest.compiledInputProvenance.outcomes[1].outcome = 'admitted'
    $manifest.compiledInputProvenance.outcomes[1].assemblyIdentity = 'private-identity-two'
    [IO.File]::WriteAllText((Join-Path $root 'probe/scan-manifest.json'), ($manifest | ConvertTo-Json -Depth 10))
    $readyLines = @(& (Join-Path $TraceMapRoot 'scripts/wp.ps1') -OutputRoot $root)
    if ($readyLines -cnotcontains 'probeReady=True' -or $readyLines -cnotcontains 'probeAdmitted=2') {
        throw 'WEBFORMS_PROBE_SUMMARY_READY_TEST_FAILED'
    }
    [void][IO.Directory]::CreateDirectory((Join-Path $root 'scan'))
    $scan = @{ ilBodyProvenance = @{
        coverageState = 'il-partial'
        effectiveLimits = @{ maxTextLength = 4096 }
        outcomes = @(
            @{ provenanceState = 'bound'; outcome = 'gap'; gapKinds = @('IlTextLimitExceeded'); safeLocator = 'private-one' },
            @{ provenanceState = 'bound'; outcome = 'gap'; gapKinds = @('IlReaderDisagreement'); safeLocator = 'private-two' },
            @{ provenanceState = 'unbound'; outcome = 'admitted'; gapKinds = @(); safeLocator = 'private-three' })
    } }
    [IO.File]::WriteAllText((Join-Path $root 'scan/scan-manifest.json'), ($scan | ConvertTo-Json -Depth 10))
    $scanLines = @(& (Join-Path $TraceMapRoot 'scripts/wp.ps1') -OutputRoot $root)
    foreach ($expected in @('scanIlCoverage=il-partial', 'scanBoundIlInputs=2',
            'scanBoundIlInputsWithGaps=2', 'scanIlTextLimit=4096',
            'scanBoundIlGap.IlTextLimitExceeded=1', 'scanBoundIlGap.IlReaderDisagreement=1')) {
        if ($scanLines -cnotcontains $expected) { throw 'WEBFORMS_PROBE_SUMMARY_SCAN_TEST_FAILED' }
    }
    if (($scanLines -join "`n") -match 'private-') { throw 'WEBFORMS_PROBE_SUMMARY_SCAN_PRIVACY_FAILED' }
    $pathReport = @{
        summary = @{ selectorCandidateCount = 1; graphNodeCount = 7; graphEdgeCount = 5
            pathCount = 0; gapCount = 2; truncated = $false }
        gaps = @(
            @{ gapKind = 'SelectorNoMatch'; message = 'private-source-one' },
            @{ gapKind = 'UnlinkedSurface'; message = 'private-source-two' })
    }
    [IO.File]::WriteAllText((Join-Path $root 'handler-paths.json'),
        ($pathReport | ConvertTo-Json -Depth 10))
    $pathLines = @(& (Join-Path $TraceMapRoot 'scripts/wp.ps1') -OutputRoot $root)
    foreach ($expected in @('pathSelectorCandidates=1', 'pathGraphNodes=7',
            'pathGraphEdges=5', 'pathCount=0', 'pathGapCount=2', 'pathTruncated=False',
            'pathGap.SelectorNoMatch=1', 'pathGap.UnlinkedSurface=1')) {
        if ($pathLines -cnotcontains $expected) { throw 'WEBFORMS_PROBE_SUMMARY_PATH_TEST_FAILED' }
    }
    if (($pathLines -join "`n") -match 'private-') { throw 'WEBFORMS_PROBE_SUMMARY_PATH_PRIVACY_FAILED' }
    Write-Output 'webFormsProbeSummaryTest=pass'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
