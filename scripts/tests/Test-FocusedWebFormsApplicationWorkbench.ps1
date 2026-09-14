$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $scripts 'New-FocusedWebFormsApplicationWorkbench.ps1'
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw 'Application workbench script syntax is invalid.' }

$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-application-workbench-' + [Guid]::NewGuid().ToString('N'))
$outputRoot = Join-Path $temp 'output'
$sourceRoot = Join-Path $temp 'source'
$corpus = Join-Path $temp 'evidence-docs'
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $sourceRoot 'Pages')) | Out-Null
[IO.Directory]::CreateDirectory($corpus) | Out-Null
[IO.File]::WriteAllText((Join-Path $sourceRoot 'Pages/First.aspx'), "<%@ Page Language=`"C#`" %>`n<asp:Button ID=`"Go`" runat=`"server`" />`n", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $corpus 'manifest.json'), (@{
    schemaVersion = 'tracemap-evidence-docs.v1'; tracemapGenerated = $true
    inputs = @(@{ sourceRefs = @(@{ scanId = 'scan-one'; commitSha = ('a' * 40) }) })
} | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $corpus 'query-recipes.json'), '{"schemaVersion":"tracemap-evidence-query-recipes.v1"}', [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $corpus 'chunks.jsonl'), "{`"chunkId`":`"chunk-001`"}`n", [Text.UTF8Encoding]::new($false))
$chunksHashBefore = (Get-FileHash -LiteralPath (Join-Path $corpus 'chunks.jsonl') -Algorithm SHA256).Hash
$badCorpus = ''
function dotnet {
    if (($args -contains '--validate-application-workbench-inputs') -and ($args -contains $badCorpus)) { $global:LASTEXITCODE = 1 }
    else { $global:LASTEXITCODE = 0 }
}

$packetPath = Join-Path $temp 'webforms-modernization.json'
$evidence = @{ factId = 'fact-surface-1'; ruleId = 'legacy.webforms.surface.v1'; evidenceTier = 'Tier2Structural'; coverageLabel = 'complete'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx'; startLine = 1; endLine = 2; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @(); supportingEdgeIds = @(); limitations = @() }
$bindingEvidence = @{ factId = 'binding-one'; ruleId = 'legacy.webforms.event-binding.v1'; evidenceTier = 'Tier2Structural'; coverageLabel = 'complete'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx'; startLine = 2; endLine = 2; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @(); supportingEdgeIds = @(); limitations = @() }
$handlerEvidence = @{ factId = 'fact-handler'; ruleId = 'legacy.webforms.handler-resolution.v1'; evidenceTier = 'Tier1Semantic'; coverageLabel = 'complete'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx.cs'; startLine = 10; endLine = 14; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @(); supportingEdgeIds = @(); limitations = @() }
$clientEvidence = @{ factId = 'fact-client-one'; ruleId = 'legacy.webforms.inline-client-behavior.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; coverageLabel = 'complete'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx'; startLine = 20; endLine = 24; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @(); supportingEdgeIds = @(); limitations = @('static client behavior only') }
$httpEvidence = @{ factId = 'fact-http-one'; ruleId = 'legacy.webforms.inline-client-http-request.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; coverageLabel = 'bounded-static-webforms-inline-client-http'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx'; startLine = 22; endLine = 30; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @('fact-client-one'); supportingEdgeIds = @(); limitations = @('static HTTP request only') }
$httpHandlerEvidence = @{ factId = 'fact-http-handler'; ruleId = 'legacy.webforms.client-http-handler-resolution.v1'; evidenceTier = 'Tier2Structural'; coverageLabel = 'bounded-structural-webforms-client-http-handler'; commitSha = ('a' * 40); filePath = 'api/SaveAudit.ashx.vb'; startLine = 3; endLine = 8; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @('fact-http-one'); supportingEdgeIds = @(); limitations = @('static HTTP handler only') }
$httpCallEvidence = @{ factId = 'fact-http-call'; ruleId = 'vb.syntax.callgraph.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; coverageLabel = 'reduced-static-webforms-client-http-handler-call'; commitSha = ('a' * 40); filePath = 'api/SaveAudit.ashx.vb'; startLine = 5; endLine = 5; extractorId = 'vb-syntax'; extractorVersion = '1'; supportingFactIds = @('fact-http-handler'); supportingEdgeIds = @(); limitations = @('static call evidence only') }
$missingHttpEvidence = @{ factId = 'fact-http-missing'; ruleId = 'legacy.webforms.inline-client-http-request.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; coverageLabel = 'bounded-static-webforms-inline-client-http'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx'; startLine = 32; endLine = 34; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @(); supportingEdgeIds = @(); limitations = @('static HTTP request only') }
$serverEvidence = @{ factId = 'fact-server-one'; ruleId = 'legacy.webforms.server-behavior.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; coverageLabel = 'reduced-static-webforms-server-behavior'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx.vb'; startLine = 30; endLine = 30; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @('fact-handler'); supportingEdgeIds = @(); limitations = @('static server behavior only') }
$inlineServerEvidence = @{ factId = 'fact-inline-server-one'; ruleId = 'legacy.webforms.inline-server-expression.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; coverageLabel = 'reduced-static-webforms-inline-server-expression'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx'; startLine = 40; endLine = 40; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @('fact-policy'); supportingEdgeIds = @(); limitations = @('static inline server reference only') }
$packet = [ordered]@{
    schemaVersion = 'webforms-modernization-packet.v1'; packetId = 'packet-one'; ruleId = 'legacy.webforms.modernization-packet.v1'; claimLevel = 'local-only'; coverage = 'reduced'
    sources = @(@{ sourceId = 'source-one'; repositoryId = 'repo-one'; scanId = 'scan-one'; commitSha = ('a' * 40); analysisLevel = 'semantic'; buildStatus = 'succeeded' })
    summary = @{ projectCount = 1; surfaceCount = 2; eventChainCount = 3; downstreamBoundaryCount = 1; identityStateCount = 1; batchDataMovementCount = 1; structuralSliceCandidateCount = 1; clientBehaviorCount = 3; serverBehaviorCount = 2; gapCount = 1; truncated = $false }
    projects = @(@{ projectId = 'project-one'; surfaceCount = 2; evidence = @(); supportingFactIds = @() })
    surfaces = @(
        @{ surfaceId = 'Surface-One'; surfaceKind = 'page'; projectId = 'project-one'; compositionTargetIds = @(); controlIds = @(); evidence = @{ factId = 'fact-surface-2'; ruleId = 'legacy.webforms.surface.v1'; evidenceTier = 'Tier2Structural'; coverageLabel = 'complete'; commitSha = ('a' * 40); filePath = 'Pages/Second.aspx'; startLine = 1; endLine = 1; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @(); supportingEdgeIds = @(); limitations = @() }; supportingEvidence = @(); supportingFactIds = @() },
        @{ surfaceId = 'surface-one'; surfaceKind = 'page'; projectId = 'project-one'; compositionTargetIds = @(); controlIds = @('Go'); evidence = $evidence; supportingEvidence = @(); supportingFactIds = @('fact-surface-1') }
    )
    eventChains = @(
        @{ chainId = 'chain-one'; surfaceId = 'surface-one'; eventSourceId = 'Go.Click'; bindingFactId = 'binding-one'; handlerId = 'handler-one'; handlerFactId = 'fact-handler'; handlerSymbol = 'App.First.Go_Click()'; classification = 'terminal-reached'; terminalKind = 'database'; evidence = @($handlerEvidence, $bindingEvidence); pathEvidence = @(); supportingFactIds = @('fact-handler'); supportingEdgeIds = @(); ruleIds = @('legacy.webforms.event-flow.v1'); evidenceTiers = @('Tier1Semantic'); coverageLabels = @('complete'); limitations = @() },
        @{ chainId = 'chain-http'; surfaceId = 'surface-one'; eventSourceId = 'webforms-client-http:one'; bindingFactId = 'fact-http-one'; handlerId = 'handler-http'; handlerFactId = 'fact-http-handler'; handlerSymbol = 'SaveAudit.ProcessRequest/1'; classification = 'NoBackendEvidence'; terminalKind = $null; evidence = @($httpEvidence, $httpHandlerEvidence); pathEvidence = @(); supportingFactIds = @('fact-http-one','fact-http-handler'); supportingEdgeIds = @('fact-http-call'); ruleIds = @('legacy.webforms.client-http-handler-resolution.v1'); evidenceTiers = @('Tier2Structural','Tier3SyntaxOrTextual'); coverageLabels = @('bounded-structural-webforms-client-http-handler'); limitations = @(); traversalObservation = @{ stopState = 'observed-downstream-without-supported-terminal'; truncated = $false }; callEvidence = @(@{ callEvidenceId = 'fact-http-call'; calleeName = 'Save'; callKind = 'SyntaxInvocation'; evidence = $httpCallEvidence; limitations = @('static call evidence only') }); callEvidenceTotalCount = 1; callEvidenceTruncated = $false },
        @{ chainId = 'chain-http-missing'; surfaceId = 'surface-one'; eventSourceId = 'webforms-client-http:missing'; bindingFactId = 'fact-http-missing'; handlerId = $null; handlerFactId = $null; handlerSymbol = $null; classification = 'handler-unavailable'; terminalKind = $null; evidence = @($missingHttpEvidence); pathEvidence = @(); supportingFactIds = @('fact-http-missing'); supportingEdgeIds = @(); ruleIds = @('legacy.webforms.inline-client-http-request.v1'); evidenceTiers = @('Tier3SyntaxOrTextual'); coverageLabels = @('bounded-static-webforms-inline-client-http'); limitations = @() }
    )
    downstreamBoundaries = @(@{ boundaryId = 'boundary-one'; chainId = 'chain-one'; surfaceId = 'surface-one'; handlerId = 'handler-one'; boundaryCategory = 'database'; boundaryKind = 'stored-procedure-candidate'; boundaryTargetId = 'target-one'; terminalEvidenceId = 'fact-db'; classification = 'retained'; evidence = @($evidence); pathEvidence = @(); supportingFactIds = @('fact-db'); supportingEdgeIds = @(); ruleIds = @('legacy.boundary.v1'); evidenceTiers = @('Tier2Structural'); coverageLabels = @('complete'); limitations = @() })
    identityStateInventory = @(
        @{ identityStateId = 'identity-one'; identityKind = 'session'; classification = 'observed'; surfaceId = 'surface-one'; safeMetadata = @{}; evidence = $evidence; supportingFactIds = @(); limitations = @() },
        @{ identityStateId = 'identity-unassociated'; identityKind = 'principal'; classification = 'observed'; safeMetadata = @{}; evidence = $evidence; supportingFactIds = @(); limitations = @() }
    )
    batchDataMovementInventory = @(
        @{ batchDataMovementId = 'batch-one'; surfaceKind = 'file-data-movement'; mechanism = 'system-io'; operationKind = 'read'; ownerStatus = 'member-declared'; projectResolution = 'resolved'; projectId = 'project-one'; safeMetadata = @{}; evidence = $evidence; supportingFactIds = @(); limitations = @() },
        @{ batchDataMovementId = 'batch-unassociated'; surfaceKind = 'database-operation'; mechanism = 'ado-net'; operationKind = 'read'; ownerStatus = 'member-declared'; projectResolution = 'unmatched'; projectId = 'project-missing'; safeMetadata = @{}; evidence = $evidence; supportingFactIds = @(); limitations = @() }
    )
    structuralSliceCandidates = @(@{ candidateId = 'candidate-one'; classification = 'structural'; ruleId = 'legacy.slice.v1'; evidenceTier = 'Tier2Structural'; ownerNamingRequired = $true; surfaceIds = @('surface-one'); evidence = @($evidence); supportingFactIds = @(); coverageLabels = @('complete'); limitations = @() })
    clientBehaviorInventory = @(
        @{ clientBehaviorId = 'client-one'; behaviorKind = 'client-event-binding'; surfaceId = 'surface-one'; selectorKind = 'generated-client-id'; selectorTarget = 'Go'; targetResolution = 'unique-static-target'; safeMetadata = @{ clientEventName = 'click'; controlId = 'Go'; generatedClientIdDependency = 'true'; serverHandlerName = 'App.First.Go_Click()' }; evidence = $clientEvidence; supportingFactIds = @('binding-one'); limitations = @('static client behavior only') },
        @{ clientBehaviorId = 'client-http-one'; behaviorKind = 'client-http-request'; surfaceId = 'surface-one'; selectorKind = 'generated-client-id'; selectorTarget = 'Go'; targetResolution = 'unique-repository-handler-file'; safeMetadata = @{ clientEventName = 'click'; controlId = 'Go'; generatedClientIdDependency = 'true'; serverHandlerName = 'not-applicable'; httpMethod = 'POST'; endpointKind = 'ashx'; endpointName = 'SaveAudit.ashx'; endpointDeclarationFile = 'api/SaveAudit.ashx'; callbackKinds = 'complete'; requestVerificationTokenCandidate = 'true' }; evidence = $httpEvidence; supportingFactIds = @('fact-client-one'); limitations = @('static HTTP request only') },
        @{ clientBehaviorId = 'client-http-missing'; behaviorKind = 'client-http-request'; surfaceId = 'surface-one'; selectorKind = 'not-applicable'; selectorTarget = $null; targetResolution = 'unique-repository-handler-file'; safeMetadata = @{ clientEventName = 'not-applicable'; controlId = 'unresolved'; generatedClientIdDependency = 'false'; serverHandlerName = 'not-applicable'; httpMethod = 'POST'; endpointKind = 'ashx'; endpointName = 'MissingEntry.ashx'; endpointDeclarationFile = 'api/MissingEntry.ashx'; callbackKinds = 'none-observed'; requestVerificationTokenCandidate = 'false' }; evidence = $missingHttpEvidence; supportingFactIds = @(); limitations = @('static HTTP request only') }
    )
    serverBehaviorInventory = @(
        @{ serverBehaviorId = 'server-one'; behaviorKind = 'control-state-mutation'; surfaceId = 'surface-one'; targetResolution = 'same-surface-control'; safeMetadata = @{ handlerName = 'Go_Click'; controlId = 'Go'; stateMember = 'enabled'; branchContext = 'if'; endResponse = 'not-applicable'; navigationKind = 'not-applicable'; lifecycleOperation = 'not-applicable' }; evidence = $serverEvidence; supportingFactIds = @('fact-handler'); limitations = @('static server behavior only') },
        @{ serverBehaviorId = 'server-two'; behaviorKind = 'inline-server-reference'; surfaceId = 'surface-one'; targetResolution = 'unique-repository-declaration'; safeMetadata = @{ handlerName = 'unavailable'; controlId = 'not-applicable'; stateMember = 'not-applicable'; branchContext = 'unconditional'; endResponse = 'not-applicable'; navigationKind = 'not-applicable'; lifecycleOperation = 'not-applicable'; expressionKind = 'render-expression'; referencedTypeName = 'Sample.Controls.OrderPolicy'; declarationFile = 'App_Code/Controls/OrderPolicy.vb'; declarationPathKind = 'app-code' }; evidence = $inlineServerEvidence; supportingFactIds = @('fact-policy'); limitations = @('static inline server reference only') }
    )
    gaps = @(@{ gapId = 'gap-one'; classification = 'HandlerTerminalUnavailable'; scopeKind = 'event-chain'; scopeId = 'chain-one'; ruleId = 'legacy.gap.v1'; evidenceTier = 'Tier4Unknown'; coverageLabel = 'reduced'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx'; startLine = 2; endLine = 2; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @(); limitations = @('missing evidence is not absence') })
    ownerQuestions = @(); limitations = @()
}
[IO.File]::WriteAllText($packetPath, (($packet | ConvertTo-Json -Depth 30) + "`n"), [Text.UTF8Encoding]::new($false))

try {
    $workbench = Join-Path $outputRoot 'workbench-one'
    & $scriptPath -PacketPath $packetPath -OutputRoot $outputRoot -OutputDirectory $workbench -EvidenceDocsRoot $corpus | Out-Null
    foreach ($expected in @('index.html', 'application-handoff.json', 'webforms-modernization.snapshot.json', 'page-001.html', 'page-001.handoff.json', 'page-002.html', 'page-002.handoff.json')) {
        if (!(Test-Path -LiteralPath (Join-Path $workbench $expected) -PathType Leaf)) { throw "Missing workbench file: $expected" }
    }
    $index = [IO.File]::ReadAllText((Join-Path $workbench 'index.html'))
    if (!$index.Contains('<th>Client behaviors</th>', [StringComparison]::Ordinal)) { throw 'Application index omitted client-behavior counts.' }
    if (!$index.Contains('<th>Server behaviors</th>', [StringComparison]::Ordinal)) { throw 'Application index omitted server-behavior counts.' }
    foreach ($heading in @('<th>Handler unavailable</th>','<th>Downstream / no terminal</th>','<th>No downstream</th>','<th>Truncated</th>','<th>Evidence gaps</th>')) {
        if (!$index.Contains($heading, [StringComparison]::Ordinal)) { throw "Application index omitted chain-outcome heading: $heading" }
    }
    if ($index.IndexOf('Pages/First.aspx', [StringComparison]::Ordinal) -gt $index.IndexOf('Pages/Second.aspx', [StringComparison]::Ordinal)) { throw 'Pages were not ordered by retained path.' }
    if (!$index.Contains('43', [StringComparison]::Ordinal) -and !$index.Contains('2 selected surfaces', [StringComparison]::Ordinal)) { throw 'Index did not report surface count.' }
    $first = [IO.File]::ReadAllText((Join-Path $workbench 'page-001.html'))
    foreach ($expected in @('Go.Click', 'App.First.Go_Click()', 'Pages/First.aspx:L2-2', 'Pages/First.aspx.cs:L10-14', 'SaveAudit.ProcessRequest/1', 'api/SaveAudit.ashx.vb:L3-8', 'Inline client behavior (3)', 'generated-client-id', 'Pages/First.aspx:L20-24', 'POST ashx', 'SaveAudit.ashx', 'api/SaveAudit.ashx', 'unique-repository-handler-file', 'Server behavior (2)', 'Go.enabled', 'Pages/First.aspx.vb:L30-30', 'Sample.Controls.OrderPolicy', 'App_Code/Controls/OrderPolicy.vb', 'Evidence-backed behavior summary', '1 of 1 client event binding(s) correlate to one retained server handler', '1 of 2 inline HTTP request(s) join through a handler declaration to one retained entry method', 'Chain outcomes include 1 unresolved handler(s), 1 with downstream calls but no supported terminal', '1 bounded handler-call evidence row(s) are retained', 'Handler unavailable</strong><br>1', 'Downstream / no terminal</strong><br>1', 'Retained calls', 'Save', 'SyntaxInvocation', 'api/SaveAudit.ashx.vb:L5-5', 'Boundary status:</strong> 1 detected', 'stored-procedure-candidate', 'HandlerTerminalUnavailable', 'Tier4Unknown', ('a' * 40), 'legacy-webforms/1', 'identity-one', 'candidate-one', 'Raw source omitted')) {
        if (!$first.Contains($expected, [StringComparison]::Ordinal)) { throw "Page report missing: $expected" }
    }
    $second = [IO.File]::ReadAllText((Join-Path $workbench 'page-002.html'))
    if (!$second.Contains('not applicable; no retained event chains', [StringComparison]::Ordinal)) { throw 'Zero-boundary state did not distinguish a page without retained event chains.' }
    $handoff = [IO.File]::ReadAllText((Join-Path $workbench 'page-001.handoff.json')) | ConvertFrom-Json -Depth 30
    if ($handoff.subject.filePath -ne 'Pages/First.aspx' -or $handoff.counts.eventChains -ne 3 -or $handoff.counts.clientBehaviors -ne 3 -or $handoff.counts.serverBehaviors -ne 2 -or $handoff.analysis.boundaryStatus -ne '1 detected' -or $handoff.evidenceDocs.status -ne 'supplied-read-only') { throw 'Page handoff projection was incomplete.' }
    if ($handoff.counts.retainedCalls -ne 1 -or $handoff.chainOutcomes.unresolvedHandlers -ne 1 -or $handoff.chainOutcomes.downstreamWithoutSupportedTerminal -ne 1) { throw 'Page handoff omitted chain-outcome or retained-call counts.' }
    if ($handoff.inventories.clientBehavior[0].id -ne 'client-one' -or $handoff.inventories.clientBehavior[0].evidenceFactId -ne 'fact-client-one') { throw 'Page handoff omitted inline client behavior evidence.' }
    if (@($handoff.inventories.clientBehavior | Where-Object { $_.id -eq 'client-http-one' -and $_.safeMetadata.endpointName -eq 'SaveAudit.ashx' }).Count -ne 1) { throw 'Page handoff omitted inline client HTTP evidence.' }
    if (@($handoff.inventories.serverBehavior | Where-Object { $_.id -eq 'server-one' -and $_.evidenceFactId -eq 'fact-server-one' }).Count -ne 1) { throw 'Page handoff omitted server behavior evidence.' }
    if (@($handoff.inventories.serverBehavior | Where-Object { $_.id -eq 'server-two' -and $_.safeMetadata.referencedTypeName -eq 'Sample.Controls.OrderPolicy' }).Count -ne 1) { throw 'Page handoff omitted inline server-expression evidence.' }
    $serverChain = @($handoff.eventChains | Where-Object { $_.chainId -eq 'chain-one' })[0]
    if ($null -eq $serverChain -or @($serverChain.evidence.factId | Where-Object { $_ -in @('binding-one', 'fact-handler') }).Count -ne 2 -or
        $handoff.downstreamBoundaries[0].boundaryId -ne 'boundary-one' -or $handoff.downstreamBoundaries[0].terminalEvidenceId -ne 'fact-db') { throw 'Page handoff omitted bounded chain or boundary evidence.' }
    $httpChain = @($handoff.eventChains | Where-Object { $_.chainId -eq 'chain-http' })[0]
    if ($httpChain.callEvidence[0].calleeName -ne 'Save' -or $httpChain.callEvidence[0].evidence.factId -ne 'fact-http-call') { throw 'Page handoff omitted bounded handler-call evidence.' }
    if ($handoff.inventories.PSObject.Properties.Name -contains 'projectDataMovement') { throw 'Project-scoped data movement was duplicated into the page handoff.' }
    if (@($handoff.retrievalHints).Count -ne 3 -or @($handoff.retrievalHints | Where-Object { !$_.recipeId }).Count -ne 0) { throw 'Retrieval hints were not serialized as a flat recipe list.' }
    $applicationHandoff = [IO.File]::ReadAllText((Join-Path $workbench 'application-handoff.json')) | ConvertFrom-Json -Depth 30
    if (@($applicationHandoff.projectDataMovement).Count -ne 1 -or $applicationHandoff.projectDataMovement[0].id -ne 'batch-one' -or
        $applicationHandoff.projectDataMovement[0].evidence.factId -ne 'fact-surface-1') { throw 'Application handoff omitted project-scoped data movement evidence.' }
    if (!$index.Contains('batch-one', [StringComparison]::Ordinal)) { throw 'Application index omitted project-scoped data movement.' }
    foreach ($unassociated in @($applicationHandoff.unassociatedIdentityState[0], $applicationHandoff.unassociatedBatchDataMovement[0])) {
        if (!$unassociated.evidence.factId -or !$unassociated.evidence.ruleId -or !$unassociated.evidence.evidenceTier -or !$unassociated.evidence.filePath -or !$unassociated.evidence.commitSha -or !$unassociated.evidence.extractorId -or !$unassociated.evidence.extractorVersion) { throw 'Unassociated inventory evidence provenance was incomplete.' }
    }
    foreach ($expected in @('identity-unassociated', 'batch-unassociated', 'legacy.webforms.surface.v1', 'Tier2Structural', 'Pages/First.aspx', 'legacy-webforms/1')) {
        if (!$index.Contains($expected, [StringComparison]::Ordinal)) { throw "Application index missing unassociated provenance: $expected" }
    }
    $chunksHashAfter = (Get-FileHash -LiteralPath (Join-Path $corpus 'chunks.jsonl') -Algorithm SHA256).Hash
    if ($chunksHashAfter -ne $chunksHashBefore) { throw 'Workbench modified chunks.jsonl.' }

    $sourceWorkbench = Join-Path $outputRoot 'workbench-source'
    & $scriptPath -PacketPath $packetPath -OutputRoot $outputRoot -OutputDirectory $sourceWorkbench -SourceRoot $sourceRoot -IncludeRawSource -SourceContextLines 1 | Out-Null
    $sourceReport = [IO.File]::ReadAllText((Join-Path $sourceWorkbench 'page-001.html'))
    if (!$sourceReport.Contains('asp:Button', [StringComparison]::Ordinal)) { throw 'Opt-in source excerpt was not rendered.' }
    if (!$sourceReport.Contains('pre code{background:transparent', [StringComparison]::Ordinal)) { throw 'Source excerpt inherited inline-code block styling.' }
    if ((Get-FileHash -LiteralPath (Join-Path $corpus 'chunks.jsonl') -Algorithm SHA256).Hash -ne $chunksHashBefore) { throw 'Source-mode workbench modified chunks.jsonl.' }

    if ($IsLinux -or $IsMacOS) {
        $outsideSource = Join-Path $temp 'outside-source.aspx'
        $outsideSentinel = 'OUTSIDE-SOURCE-MUST-NOT-RENDER'
        [IO.File]::WriteAllText($outsideSource, $outsideSentinel, [Text.UTF8Encoding]::new($false))
        Remove-Item -LiteralPath (Join-Path $sourceRoot 'Pages/First.aspx')
        New-Item -ItemType SymbolicLink -Path (Join-Path $sourceRoot 'Pages/First.aspx') -Target $outsideSource | Out-Null
        $symlinkWorkbench = Join-Path $outputRoot 'workbench-symlink'
        & $scriptPath -PacketPath $packetPath -OutputRoot $outputRoot -OutputDirectory $symlinkWorkbench -SourceRoot $sourceRoot -IncludeRawSource | Out-Null
        $symlinkReport = [IO.File]::ReadAllText((Join-Path $symlinkWorkbench 'page-001.html'))
        if ($symlinkReport.Contains($outsideSentinel, [StringComparison]::Ordinal) -or !$symlinkReport.Contains('crossed a symlink or junction', [StringComparison]::Ordinal)) { throw 'Source symlink escape was not rejected.' }

        $outsideOutput = Join-Path $temp 'outside-output'
        [IO.Directory]::CreateDirectory($outsideOutput) | Out-Null
        $linkedOutputParent = Join-Path $outputRoot 'linked-output'
        New-Item -ItemType SymbolicLink -Path $linkedOutputParent -Target $outsideOutput | Out-Null
        try {
            & $scriptPath -PacketPath $packetPath -OutputRoot $outputRoot -OutputDirectory (Join-Path $linkedOutputParent 'escaped-workbench') | Out-Null
            throw 'Linked output ancestor was accepted below the configured root.'
        }
        catch {
            if ($_.Exception.Message -ne 'ApplicationWorkbenchOutputOutsideRoot') { throw }
        }
    }

    if ($IsLinux) {
        $caseSibling = Join-Path $temp 'OUTPUT/case-escape'
        try {
            & $scriptPath -PacketPath $packetPath -OutputRoot $outputRoot -OutputDirectory $caseSibling | Out-Null
            throw 'Case-distinct output sibling was accepted outside the configured root.'
        }
        catch {
            if ($_.Exception.Message -ne 'ApplicationWorkbenchOutputOutsideRoot') { throw }
        }
    }

    $reviewSchema = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $scripts) 'docs/contracts/wits-webforms-application-review.v1.schema.json')) | ConvertFrom-Json -Depth 30
    if ($reviewSchema.'$defs'.decision.properties.pageId.pattern -ne '^page-[0-9]{3,4}$') { throw 'Review schema does not accept the generated page-1000 identifier.' }

    $reviewScript = Join-Path $scripts 'webforms-review/Invoke-WitsApplicationReview.ps1'
    $reviewPath = Join-Path $temp 'application-review.json'
    & $reviewScript -Mode Export -PacketPath $packetPath -ReviewPath $reviewPath | Out-Null
    $review = [IO.File]::ReadAllText($reviewPath) | ConvertFrom-Json -Depth 30
    $review.decisions[0].verdict = 'needs-review'; $review.decisions[0].migrationDisposition = 'defer'; $review.decisions[0].capabilityLabel = 'Crew meal review'; $review.decisions[0].correction = @{ category = 'business-intent'; statement = 'Owner correction retained' }
    $review.decisions[1].verdict = 'expected-ui-only'; $review.decisions[1].migrationDisposition = 'retain'
    [IO.File]::WriteAllText($reviewPath, (($review | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
    $reviewedWorkbench = Join-Path $outputRoot 'workbench-reviewed'
    & $scriptPath -PacketPath $packetPath -OutputRoot $outputRoot -OutputDirectory $reviewedWorkbench -ReviewPath $reviewPath | Out-Null
    $reviewedIndex = [IO.File]::ReadAllText((Join-Path $reviewedWorkbench 'index.html'))
    $reviewedPage = [IO.File]::ReadAllText((Join-Path $reviewedWorkbench 'page-001.html'))
    $reviewedSecondPage = [IO.File]::ReadAllText((Join-Path $reviewedWorkbench 'page-002.html'))
    if (!$reviewedIndex.Contains('needs-review', [StringComparison]::Ordinal) -or !$reviewedPage.Contains('Crew meal review', [StringComparison]::Ordinal) -or !$reviewedPage.Contains('Owner correction retained', [StringComparison]::Ordinal) -or !$reviewedSecondPage.Contains('expected-ui-only', [StringComparison]::Ordinal)) { throw 'Validated ordinal application review was not projected into HTML.' }

    $badCorpus = Join-Path $temp 'bad-corpus'
    [IO.Directory]::CreateDirectory($badCorpus) | Out-Null
    [IO.File]::Copy((Join-Path $corpus 'query-recipes.json'), (Join-Path $badCorpus 'query-recipes.json'))
    [IO.File]::Copy((Join-Path $corpus 'chunks.jsonl'), (Join-Path $badCorpus 'chunks.jsonl'))
    [IO.File]::WriteAllText((Join-Path $badCorpus 'manifest.json'), (@{ schemaVersion = 'tracemap-evidence-docs.v1'; tracemapGenerated = $true; inputs = @(@{ sourceRefs = @(@{ scanId = 'different'; commitSha = ('b' * 40) }) }) } | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    try {
        & $scriptPath -PacketPath $packetPath -OutputRoot $outputRoot -OutputDirectory (Join-Path $outputRoot 'bad-workbench') -EvidenceDocsRoot $badCorpus | Out-Null
        throw 'Expected mismatched corpus provenance to fail.'
    }
    catch {
        if ($_.Exception.Message -ne 'ApplicationWorkbenchInputValidationFailed') { throw }
    }
    Write-Host 'PASS focused Web Forms application workbench'
}
finally {
    Remove-Item Function:\dotnet -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
