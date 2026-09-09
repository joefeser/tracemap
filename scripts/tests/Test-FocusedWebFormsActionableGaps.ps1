$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$temp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
try {
    $chains = @(
        @{ surfaceId = 's1'; handlerFactId = $null; terminalKind = $null; supportingFactIds = @('private-1'); ruleIds = @('legacy.webforms.handler-resolution.v1'); evidenceTiers = @('Tier3SyntaxOrTextual'); coverageLabels = @('reduced-static-webforms-handler') },
        @{ surfaceId = 's2'; handlerFactId = 'private-handler-2'; terminalKind = $null; supportingFactIds = @('private-2'); ruleIds = @('legacy.webforms.event-flow.v1'); evidenceTiers = @('Tier1Semantic'); coverageLabels = @('bounded-static-webforms-flow'); traversalObservation = @{ stopState = 'no-observed-downstream-edge' } },
        @{ surfaceId = 's3'; handlerFactId = 'private-handler-3'; terminalKind = $null; supportingFactIds = @('private-3'); ruleIds = @('legacy.webforms.event-flow.v1'); evidenceTiers = @('Tier2Structural'); coverageLabels = @('bounded-static-webforms-flow'); traversalObservation = @{ stopState = 'observed-downstream-without-supported-terminal'; leafNodeKinds = @('symbol'); leafSurfaceKinds = @('http-client'); leafRuleIds = @('csharp.semantic.callgraph.v1'); traversedEdgeKinds = @('calls'); traversedRuleIds = @('csharp.semantic.callgraph.v1'); diagnosticShapesTruncated = $false } },
        @{ surfaceId = 's4'; handlerFactId = 'private-handler-4'; terminalKind = $null; supportingFactIds = @('private-4'); ruleIds = @('legacy.webforms.event-flow.v1'); evidenceTiers = @('Tier4Unknown'); coverageLabels = @('reduced-static-webforms-flow'); traversalObservation = @{ stopState = 'bounded-traversal-truncated' } },
        @{ surfaceId = 's5'; handlerFactId = 'private-handler-5'; terminalKind = 'sql-query'; supportingFactIds = @('private-5') }
    )
    $packet = @{
        schemaVersion = 'webforms-modernization-packet.v1'
        summary = @{ truncated = $true }
        surfaceSelection = @{ items = @(1..5 | ForEach-Object { @{ alias = "page-00$_"; surfaceIds = @("s$_") } }) }
        eventChains = $chains
        gaps = @(@{ classification = 'NoBackendEvidence'; supportingFactIds = @('private-3') })
    }
    $path = Join-Path $temp 'webforms-modernization.json'
    [IO.File]::WriteAllText($path, ($packet | ConvertTo-Json -Depth 20))
    $output = (& (Join-Path $scripts 'Summarize-FocusedWebFormsActionableGaps.ps1') -ReportPath $path 6>&1 | Out-String)
    foreach ($expected in @(
        'bucket=handler-resolution-unavailable|chains=1|pages=1',
        'bucket=handler-call-evidence-missing|chains=1|pages=1',
        'bucket=terminal-coverage-review|chains=1|pages=1',
        'bucket=bounded-traversal-truncated|chains=1|pages=1',
        'bucketrule-terminal-coverage-review=legacy.webforms.event-flow.v1|chains=1',
        'buckettier-handler-call-evidence-missing=Tier1Semantic|chains=1',
        'bucketlinkedGap-terminal-coverage-review=NoBackendEvidence|chains=1',
        'bucketleafNodeKind-terminal-coverage-review=symbol|chains=1',
        'bucketleafSurfaceKind-terminal-coverage-review=http-client|chains=1',
        'bucketleafRule-terminal-coverage-review=csharp.semantic.callgraph.v1|chains=1',
        'buckettraversedEdgeKind-terminal-coverage-review=calls|chains=1',
        'bucketDiagnosticShapesTruncated-terminal-coverage-review=False',
        'priority01=terminal-coverage-review'
    )) {
        if (-not $output.Contains($expected)) { throw "Missing expected result: $expected" }
    }
    foreach ($private in @('private-1', 'private-2', 'private-3', 'private-handler')) {
        if ($output.Contains($private)) { throw 'Private identity leaked.' }
    }
    Write-Host 'PASS actionable gap summary'
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
