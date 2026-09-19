$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $scripts 'New-FocusedWebFormsApplicationWorkbench.ps1'
$exportScript = Join-Path $scripts 'Export-FocusedWebFormsPageShareable.ps1'
$latestExportScript = Join-Path $scripts 'Export-LatestFocusedWebFormsPageShareable.ps1'
$standaloneScript = Join-Path $scripts 'New-FocusedWebFormsStandaloneReview.ps1'
$pageGraphScript = Join-Path $scripts 'New-FocusedWebFormsPageGraphDump.ps1'
$shareableSchemaPath = Join-Path (Split-Path -Parent $scripts) 'docs/contracts/webforms-page-paths-shareable.v1.schema.json'
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw 'Application workbench script syntax is invalid.' }
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($exportScript, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw 'Page shareable exporter script syntax is invalid.' }
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($standaloneScript, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw 'Standalone review script syntax is invalid.' }
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($latestExportScript, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw 'Latest standalone page exporter script syntax is invalid.' }
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($pageGraphScript, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw 'Page graph dump script syntax is invalid.' }
$shareableSchema = [IO.File]::ReadAllText($shareableSchemaPath) | ConvertFrom-Json -Depth 30
if ($shareableSchema.properties.schemaVersion.const -ne 'webforms-page-paths-shareable.v1' -or $shareableSchema.properties.privacy.const -ne 'anonymous-structure-only') { throw 'Page shareable schema does not pin its version and privacy profile.' }

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
$httpSemanticCallEvidence = @{ factId = 'fact-http-call-semantic'; ruleId = 'vb.semantic.callgraph.v1'; evidenceTier = 'Tier1Semantic'; coverageLabel = 'bounded-semantic-callgraph'; commitSha = ('a' * 40); filePath = 'api/SaveAudit.ashx.vb'; startLine = 5; endLine = 5; extractorId = 'vb-semantic'; extractorVersion = '1'; supportingFactIds = @('fact-http-handler'); supportingEdgeIds = @(); limitations = @('compiler-resolved call evidence only') }
$missingHttpEvidence = @{ factId = 'fact-http-missing'; ruleId = 'legacy.webforms.inline-client-http-request.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; coverageLabel = 'bounded-static-webforms-inline-client-http'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx'; startLine = 32; endLine = 34; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @(); supportingEdgeIds = @(); limitations = @('static HTTP request only') }
$serverEvidence = @{ factId = 'fact-server-one'; ruleId = 'legacy.webforms.server-behavior.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; coverageLabel = 'reduced-static-webforms-server-behavior'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx.vb'; startLine = 30; endLine = 30; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @('fact-handler'); supportingEdgeIds = @(); limitations = @('static server behavior only') }
$inlineServerEvidence = @{ factId = 'fact-inline-server-one'; ruleId = 'legacy.webforms.inline-server-expression.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; coverageLabel = 'reduced-static-webforms-inline-server-expression'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx'; startLine = 40; endLine = 40; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @('fact-policy'); supportingEdgeIds = @(); limitations = @('static inline server reference only') }
$packet = [ordered]@{
    schemaVersion = 'webforms-modernization-packet.v1'; packetId = 'packet-one'; ruleId = 'legacy.webforms.modernization-packet.v1'; claimLevel = 'local-only'; coverage = 'reduced'
    sources = @(
        @{ sourceId = 'source-one'; repositoryId = 'repo-one'; scanId = 'scan-one'; commitSha = ('a' * 40); analysisLevel = 'semantic'; buildStatus = 'succeeded' },
        @{ sourceId = 'source-two'; repositoryId = 'repo-two'; scanId = 'scan-two'; commitSha = ('b' * 40); analysisLevel = 'semantic'; buildStatus = 'succeeded' }
    )
    summary = @{ projectCount = 1; surfaceCount = 2; eventChainCount = 5; downstreamBoundaryCount = 1; identityStateCount = 1; batchDataMovementCount = 1; structuralSliceCandidateCount = 1; clientBehaviorCount = 3; serverBehaviorCount = 2; gapCount = 2; truncated = $true; truncationReasons = @('legacy-flow:TruncatedByLimit:depth'); coverageReductionReasons = @('source-analysis-reduced','bounded-output-truncated') }
    projects = @(@{ projectId = 'project-one'; surfaceCount = 2; evidence = @(); supportingFactIds = @() })
    surfaces = @(
        @{ surfaceId = 'Surface-One'; surfaceKind = 'page'; projectId = 'project-one'; compositionTargetIds = @(); controlIds = @(); evidence = @{ factId = 'fact-surface-2'; ruleId = 'legacy.webforms.surface.v1'; evidenceTier = 'Tier2Structural'; coverageLabel = 'complete'; commitSha = ('a' * 40); filePath = 'Pages/Second.aspx'; startLine = 1; endLine = 1; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @(); supportingEdgeIds = @(); limitations = @() }; supportingEvidence = @(); supportingFactIds = @() },
        @{ surfaceId = 'surface-one'; surfaceKind = 'page'; projectId = 'project-one'; compositionTargetIds = @(); controlIds = @('webforms-control:go'); controls = @(@{ controlIdentity = 'webforms-control:go'; declaredId = 'Go'; controlType = 'Button'; supportingFactId = 'fact-control-go' }); evidence = $evidence; supportingEvidence = @(); supportingFactIds = @('fact-surface-1','fact-control-go') }
    )
    eventChains = @(
        @{ chainId = 'chain-one'; surfaceId = 'surface-one'; eventSourceId = 'Go.Click'; bindingFactId = 'binding-one'; handlerId = 'handler-one'; handlerFactId = 'fact-handler'; handlerSymbol = 'App.First.Go_Click()'; classification = 'StrongStaticPath'; terminalKind = 'sql-query'; evidence = @($handlerEvidence, $bindingEvidence); pathEvidence = @(); supportingFactIds = @('fact-handler'); supportingEdgeIds = @(); ruleIds = @('legacy.webforms.event-flow.v1'); evidenceTiers = @('Tier1Semantic'); coverageLabels = @('complete'); limitations = @() },
        @{ chainId = 'chain-http'; surfaceId = 'surface-one'; eventSourceId = 'webforms-client-http:one'; bindingFactId = 'fact-http-one'; handlerId = 'handler-http'; handlerFactId = 'fact-http-handler'; handlerSymbol = 'SaveAudit.ProcessRequest/1'; classification = 'NoBackendEvidence'; terminalKind = $null; evidence = @($httpEvidence, $httpHandlerEvidence); pathEvidence = @(); supportingFactIds = @('fact-http-one','fact-http-handler'); supportingEdgeIds = @('fact-http-call'); ruleIds = @('legacy.webforms.client-http-handler-resolution.v1'); evidenceTiers = @('Tier2Structural','Tier3SyntaxOrTextual'); coverageLabels = @('bounded-structural-webforms-client-http-handler'); limitations = @(); traversalObservation = @{ stopState = 'observed-downstream-without-supported-terminal'; truncated = $false; truncationReasons = @() }; nextEvidenceKind = 'semantic-call-resolution'; unresolvedCallTargets = @('Save'); nextEvidenceInputs = @('project-or-assembly-input-containing-the-listed-call-targets'); callEvidence = @(@{ callEvidenceId = 'fact-http-call'; callSiteId = 'call-site-one'; calleeName = 'Save'; callKind = 'SyntaxInvocation'; resolution = 'syntax-only'; technologyFamily = 'unresolved'; declaringType = $null; assemblyName = $null; evidence = $httpCallEvidence; limitations = @('static call evidence only') }, @{ callEvidenceId = 'fact-http-call-semantic'; callSiteId = 'call-site-one'; calleeName = 'Save'; callKind = 'SemanticMethodInvocation'; resolution = 'compiler-resolved'; technologyFamily = 'telerik'; declaringType = 'Telerik.Web.UI.SampleControl'; assemblyName = 'Telerik.Web.UI'; evidence = $httpSemanticCallEvidence; limitations = @('compiler-resolved call evidence only') }); callEvidenceTotalCount = 256; callEvidenceTruncated = $false },
        @{ chainId = 'chain-http-second-path'; surfaceId = 'surface-one'; eventSourceId = 'webforms-client-http:one'; bindingFactId = 'fact-http-one'; handlerId = 'handler-http'; handlerFactId = 'fact-http-handler'; handlerSymbol = 'SaveAudit.ProcessRequest/1'; classification = 'NoBackendEvidence'; terminalKind = $null; evidence = @($httpEvidence, $httpHandlerEvidence); pathEvidence = @(); supportingFactIds = @('fact-http-one','fact-http-handler'); supportingEdgeIds = @('fact-http-call'); ruleIds = @('legacy.webforms.client-http-handler-resolution.v1'); evidenceTiers = @('Tier2Structural','Tier3SyntaxOrTextual'); coverageLabels = @('bounded-structural-webforms-client-http-handler'); limitations = @(); traversalObservation = @{ stopState = 'observed-downstream-without-supported-terminal'; truncated = $false }; callEvidence = @(@{ callEvidenceId = 'fact-http-call'; callSiteId = 'call-site-one'; calleeName = 'Save'; callKind = 'SyntaxInvocation'; resolution = 'syntax-only'; technologyFamily = 'unresolved'; declaringType = $null; assemblyName = $null; evidence = $httpCallEvidence; limitations = @('static call evidence only') }); callEvidenceTotalCount = 1; callEvidenceTruncated = $false },
        @{ chainId = 'chain-other-incomplete'; surfaceId = 'surface-one'; eventSourceId = 'Go.Change'; bindingFactId = 'binding-one'; handlerId = 'handler-one'; handlerFactId = 'fact-handler'; handlerSymbol = 'App.First.Go_Click()'; classification = 'UnknownAnalysisGap'; terminalKind = $null; evidence = @($handlerEvidence, $bindingEvidence); pathEvidence = @(); supportingFactIds = @('fact-handler'); supportingEdgeIds = @(); ruleIds = @('legacy.webforms.event-flow.v1'); evidenceTiers = @('Tier1Semantic'); coverageLabels = @('complete'); limitations = @(); traversalObservation = @{ stopState = $null; truncated = $false }; callEvidence = @(); callEvidenceTotalCount = 0; callEvidenceTruncated = $false },
        @{ chainId = 'chain-http-missing'; surfaceId = 'surface-one'; eventSourceId = 'webforms-client-http:missing'; bindingFactId = 'fact-http-missing'; handlerId = $null; handlerFactId = $null; handlerSymbol = $null; handlerResolution = 'missing-linked-method'; classification = 'handler-unavailable'; terminalKind = $null; evidence = @($missingHttpEvidence); pathEvidence = @(); supportingFactIds = @('fact-http-missing'); supportingEdgeIds = @(); ruleIds = @('legacy.webforms.inline-client-http-request.v1'); evidenceTiers = @('Tier3SyntaxOrTextual'); coverageLabels = @('bounded-static-webforms-client-http'); limitations = @() }
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
    gaps = @(
        @{ gapId = 'gap-one'; classification = 'HandlerTerminalUnavailable'; scopeKind = 'event-chain'; scopeId = 'chain-one'; ruleId = 'legacy.gap.v1'; evidenceTier = 'Tier4Unknown'; coverageLabel = 'reduced'; commitSha = ('a' * 40); filePath = 'Pages/First.aspx'; startLine = 2; endLine = 2; extractorId = 'legacy-webforms'; extractorVersion = '1'; safeMetadata = @{}; supportingFactIds = @(); limitations = @('missing evidence is not absence') },
        @{ gapId = 'gap-control'; classification = 'UnresolvedWebFormsControlRegistration'; scopeKind = 'control-registration'; scopeId = 'registration-one'; ruleId = 'legacy.webforms.control-registration.v1'; evidenceTier = 'Tier4Unknown'; coverageLabel = 'reduced'; commitSha = ('a' * 40); filePath = 'web.config'; startLine = 4; endLine = 4; extractorId = 'legacy-webforms'; extractorVersion = '1'; safeMetadata = @{ controlPrefix = 'vendor'; controlType = 'Grid'; registrationAssembly = 'Vendor.Controls'; registrationNamespace = 'Vendor.UI'; registrationState = 'assembly-unavailable' }; supportingFactIds = @(); limitations = @('registration unresolved') }
    )
    ownerQuestions = @(); limitations = @()
}
[IO.File]::WriteAllText($packetPath, (($packet | ConvertTo-Json -Depth 30) + "`n"), [Text.UTF8Encoding]::new($false))

try {
    $workbench = Join-Path $outputRoot 'workbench'
    & $scriptPath -PacketPath $packetPath -OutputRoot $outputRoot -OutputDirectory $workbench -EvidenceDocsRoot $corpus | Out-Null
    foreach ($expected in @('index.html', 'application-handoff.json', 'application-outliers.html', 'application-outliers.shareable.html', 'application-outliers.shareable.json', 'webforms-modernization.snapshot.json', 'page-001.html', 'page-001.handoff.json', 'page-002.html', 'page-002.handoff.json')) {
        if (!(Test-Path -LiteralPath (Join-Path $workbench $expected) -PathType Leaf)) { throw "Missing workbench file: $expected" }
    }
    $index = [IO.File]::ReadAllText((Join-Path $workbench 'index.html'))
    foreach ($heading in @('<th>Page</th>','<th>Activity</th>','<th>Calls P/F/S</th>','<th>Retained flags</th>','<th>Review</th>','<th>Diagnostics</th>')) {
        if (!$index.Contains($heading, [StringComparison]::Ordinal)) { throw "Application index omitted compact heading: $heading" }
    }
    foreach ($removedHeading in @('<th>Retained file</th>','<th>Client behaviors</th>','<th>Server behaviors</th>','<th>Evidence gaps</th>')) {
        if ($index.Contains($removedHeading, [StringComparison]::Ordinal)) { throw "Application index retained wide diagnostic heading: $removedHeading" }
    }
    foreach ($detail in @('<dt>Retained route</dt>','<dt>Kind</dt>','<dt>Calls</dt>','<dt>Call evidence ceiling</dt>','<dt>Handler unavailable</dt>','<dt>Downstream / no terminal</dt>','<dt>No downstream</dt>','<dt>Terminal inventory incomplete</dt>','<dt>Path detail truncated</dt>','<dt>Other incomplete</dt>','<dt>Boundaries</dt>','<dt>Recorded gap facts</dt>','<dt>Review</dt>','<dt>Evidence handoff</dt>')) {
        if (!$index.Contains($detail, [StringComparison]::Ordinal)) { throw "Application index omitted expandable diagnostic: $detail" }
    }
    if (!$index.Contains('P = chain-associated projections; F = unique retained facts; S = normalized source sites', [StringComparison]::Ordinal)) { throw 'Application index omitted compact call-accounting legend.' }
    foreach ($context in @('Handler unavailable</strong> means the event source was retained but no usable handler fact/span was available', '<tr class="page-context"><th scope="row">Route and controls</th><td colspan="5">', '<strong>Route:</strong> <code>Pages/First.aspx</code>', '<strong>Controls:</strong> Go (Button)')) {
        if (!$index.Contains($context, [StringComparison]::Ordinal)) { throw "Application index omitted private route/control context: $context" }
    }
    foreach ($flag in @('ceiling 1','omitted 254','handler 1','no terminal 2','incomplete 1','gaps 1','boundaries 1')) {
        if (!$index.Contains($flag, [StringComparison]::Ordinal)) { throw "Application index omitted retained triage flag: $flag" }
    }
    if ($index.IndexOf('Pages/First.aspx', [StringComparison]::Ordinal) -gt $index.IndexOf('Pages/Second.aspx', [StringComparison]::Ordinal)) { throw 'Pages were not ordered by retained path.' }
    if (!$index.Contains('43', [StringComparison]::Ordinal) -and !$index.Contains('2 selected surfaces', [StringComparison]::Ordinal)) { throw 'Index did not report surface count.' }
    $first = [IO.File]::ReadAllText((Join-Path $workbench 'page-001.html'))
    if (!$first.Contains('<th>Call accounting</th><th>Chain conclusion</th><th>Terminal/stop</th>', [StringComparison]::Ordinal) -or $first.Contains('<th>Retained calls</th><th>Classification</th><th>Terminal/stop</th>', [StringComparison]::Ordinal)) { throw 'Page report did not distinguish call accounting, chain conclusion, and terminal state.' }
    foreach ($expected in @('Go.Click', 'App.First.Go_Click()', 'Pages/First.aspx:L2-2', 'Pages/First.aspx.cs:L10-14', 'SaveAudit.ProcessRequest/1', 'api/SaveAudit.ashx.vb:L3-8', 'Inline client behavior (3)', 'generated-client-id', 'Pages/First.aspx:L20-24', 'POST ashx', 'SaveAudit.ashx', 'api/SaveAudit.ashx', 'unique-repository-handler-file', 'Server behavior (2)', 'Go.enabled', 'Pages/First.aspx.vb:L30-30', 'Sample.Controls.OrderPolicy', 'App_Code/Controls/OrderPolicy.vb', 'Evidence-backed behavior summary', '1 of 1 client event binding(s) correlate to one retained server handler', '1 of 2 inline HTTP request(s) join through a handler declaration to one retained entry method', 'Chain outcomes include 1 unresolved handler(s), 2 with downstream calls but no supported terminal', '1 other incomplete chain(s)', '3 retained chain-associated call fact projection(s) represent 2 unique retained call fact(s) and 1 normalized source call site(s)', 'Retained call projections</strong><br>3', 'Unique call facts</strong><br>2', 'Normalized call sites</strong><br>1', 'Call evidence ceiling</strong><br>1 chain(s)', '254 explicitly omitted', 'Handler unavailable</strong><br>1', 'event source retained; usable handler fact unavailable', 'Downstream / no terminal</strong><br>2', 'Other incomplete chains</strong><br>1', 'Recorded gap facts</strong><br>1', 'Go (Button)', 'Gap categories (1)', 'Call accounting', 'Save', 'SemanticMethodInvocation', 'compiler-resolved / telerik', 'Telerik.Web.UI.SampleControl', 'semantic + syntax evidence retained', 'api/SaveAudit.ashx.vb:L5-5', 'Boundary status:</strong> 1 detected', 'stored-procedure-candidate', 'HandlerTerminalUnavailable', 'Tier4Unknown', ('a' * 40), 'legacy-webforms/1', 'identity-one', 'candidate-one', 'Raw source omitted')) {
        if (!$first.Contains($expected, [StringComparison]::Ordinal)) { throw "Page report missing: $expected" }
    }
    $second = [IO.File]::ReadAllText((Join-Path $workbench 'page-002.html'))
    if (!$second.Contains('not applicable; no retained event chains', [StringComparison]::Ordinal)) { throw 'Zero-boundary state did not distinguish a page without retained event chains.' }
    $handoff = [IO.File]::ReadAllText((Join-Path $workbench 'page-001.handoff.json')) | ConvertFrom-Json -Depth 30
    $expectedGeneratorSha = (Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $expectedPacketSha = (Get-FileHash -LiteralPath $packetPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($handoff.provenance.generatorSha256 -ne $expectedGeneratorSha -or $handoff.provenance.inputSha256 -ne $expectedPacketSha) { throw 'Page handoff omitted exact generator or packet provenance.' }
    if ($handoff.subject.filePath -ne 'Pages/First.aspx' -or $handoff.counts.eventChains -ne 5 -or $handoff.counts.clientBehaviors -ne 3 -or $handoff.counts.serverBehaviors -ne 2 -or $handoff.analysis.boundaryStatus -ne '1 detected' -or $handoff.evidenceDocs.status -ne 'supplied-read-only') { throw 'Page handoff projection was incomplete.' }
    if (!$handoff.analysis.packetTruncated -or $handoff.analysis.packetTruncationScope -ne 'application-packet' -or @($handoff.analysis.packetTruncationReasons | Where-Object { $_ -eq 'legacy-flow:TruncatedByLimit:depth' }).Count -ne 1 -or $handoff.analysis.pageTraversalTruncated) { throw 'Page handoff conflated application packet truncation with page traversal truncation.' }
    if ($handoff.counts.retainedCalls -ne 3 -or $handoff.counts.chainAssociatedRetainedCalls -ne 3 -or $handoff.counts.reportedCallProjections -ne 257 -or $handoff.counts.omittedCallProjections -ne 254 -or $handoff.counts.uniqueRetainedCallFacts -ne 2 -or $handoff.counts.normalizedCallSites -ne 1 -or $handoff.counts.callEvidenceCeilingChains -ne 1 -or $handoff.chainOutcomes.unresolvedHandlers -ne 1 -or $handoff.chainOutcomes.downstreamWithoutSupportedTerminal -ne 2 -or $handoff.chainOutcomes.otherIncomplete -ne 1) { throw 'Page handoff omitted chain-outcome or retained-call counts.' }
    if ($handoff.inventories.clientBehavior[0].id -ne 'client-one' -or $handoff.inventories.clientBehavior[0].evidenceFactId -ne 'fact-client-one') { throw 'Page handoff omitted inline client behavior evidence.' }
    if (@($handoff.inventories.clientBehavior | Where-Object { $_.id -eq 'client-http-one' -and $_.safeMetadata.endpointName -eq 'SaveAudit.ashx' }).Count -ne 1) { throw 'Page handoff omitted inline client HTTP evidence.' }
    if (@($handoff.inventories.serverBehavior | Where-Object { $_.id -eq 'server-one' -and $_.evidenceFactId -eq 'fact-server-one' }).Count -ne 1) { throw 'Page handoff omitted server behavior evidence.' }
    if (@($handoff.inventories.serverBehavior | Where-Object { $_.id -eq 'server-two' -and $_.safeMetadata.referencedTypeName -eq 'Sample.Controls.OrderPolicy' }).Count -ne 1) { throw 'Page handoff omitted inline server-expression evidence.' }
    $serverChain = @($handoff.eventChains | Where-Object { $_.chainId -eq 'chain-one' })[0]
    if ($null -eq $serverChain -or @($serverChain.evidence.factId | Where-Object { $_ -in @('binding-one', 'fact-handler') }).Count -ne 2 -or
        $handoff.downstreamBoundaries[0].boundaryId -ne 'boundary-one' -or $handoff.downstreamBoundaries[0].terminalEvidenceId -ne 'fact-db') { throw 'Page handoff omitted bounded chain or boundary evidence.' }
    $httpChain = @($handoff.eventChains | Where-Object { $_.chainId -eq 'chain-http' })[0]
    if (@($httpChain.callEvidence).Count -ne 2 -or @($httpChain.callEvidence | Where-Object { $_.technologyFamily -eq 'telerik' -and $_.declaringType -eq 'Telerik.Web.UI.SampleControl' -and $_.callSiteId -eq 'call-site-one' }).Count -ne 1) { throw 'Page handoff omitted bounded handler-call identity or technology evidence.' }
    if ($httpChain.nextEvidenceKind -ne 'semantic-call-resolution' -or @($httpChain.unresolvedCallTargets | Where-Object { $_ -eq 'Save' }).Count -ne 1 -or @($handoff.nextEvidenceSummary | Where-Object { $_.kind -eq 'semantic-call-resolution' -and $_.chainCount -eq 1 }).Count -ne 1) { throw 'Page handoff omitted concrete next-evidence guidance.' }
    if ($handoff.inventories.PSObject.Properties.Name -contains 'projectDataMovement') { throw 'Project-scoped data movement was duplicated into the page handoff.' }
    if (@($handoff.retrievalHints).Count -ne 3 -or @($handoff.retrievalHints | Where-Object { !$_.recipeId }).Count -ne 0) { throw 'Retrieval hints were not serialized as a flat recipe list.' }
    $applicationHandoff = [IO.File]::ReadAllText((Join-Path $workbench 'application-handoff.json')) | ConvertFrom-Json -Depth 30
    if ($applicationHandoff.provenance.generatorSha256 -ne $expectedGeneratorSha -or $applicationHandoff.provenance.inputSha256 -ne $expectedPacketSha) { throw 'Application handoff omitted exact generator or packet provenance.' }
    if (@($applicationHandoff.projectDataMovement).Count -ne 1 -or $applicationHandoff.projectDataMovement[0].id -ne 'batch-one' -or
        $applicationHandoff.projectDataMovement[0].evidence.factId -ne 'fact-surface-1') { throw 'Application handoff omitted project-scoped data movement evidence.' }
    if ($applicationHandoff.outlierReview.privateHtml -ne 'application-outliers.html' -or $applicationHandoff.outlierReview.json -ne 'application-outliers.shareable.json' -or $applicationHandoff.pages[0].chainOutcomes.otherIncomplete -ne 1 -or $applicationHandoff.pages[0].gapCategories[0].classification -ne 'HandlerTerminalUnavailable') { throw 'Application handoff omitted outlier-review navigation or page breakdowns.' }
    if (@($applicationHandoff.analysis.coverageReductionReasons | Where-Object { $_ -eq 'source-analysis-reduced' }).Count -ne 1 -or @($applicationHandoff.analysis.packetTruncationReasons | Where-Object { $_ -eq 'legacy-flow:TruncatedByLimit:depth' }).Count -ne 1) { throw 'Application handoff conflated coverage reduction and truncation reasons.' }
    if (@($applicationHandoff.nextEvidenceSummary | Where-Object { $_.kind -eq 'semantic-call-resolution' -and $_.targets -contains 'Save' }).Count -ne 1) { throw 'Application handoff omitted aggregated next-evidence guidance.' }
    if (@($applicationHandoff.controlRegistrationGaps | Where-Object { $_.safeMetadata.registrationAssembly -eq 'Vendor.Controls' -and $_.safeMetadata.registrationState -eq 'assembly-unavailable' }).Count -ne 1) { throw 'Application handoff omitted safe control-registration metadata.' }
    $outlierJsonText = [IO.File]::ReadAllText((Join-Path $workbench 'application-outliers.shareable.json'))
    $outlierHtml = [IO.File]::ReadAllText((Join-Path $workbench 'application-outliers.shareable.html'))
    $privateOutlierHtml = [IO.File]::ReadAllText((Join-Path $workbench 'application-outliers.html'))
    foreach ($expected in @('Private Web Forms outlier review','href="page-001.html"','title="Pages/First.aspx"','Pages/First.aspx','Return to application workbench')) {
        if (!$privateOutlierHtml.Contains($expected, [StringComparison]::Ordinal)) { throw "Private outlier HTML missing navigation or source identity: $expected" }
    }
    if (!$index.Contains('href="application-outliers.html">Private outlier review</a>', [StringComparison]::Ordinal)) { throw 'Private application index omitted private outlier navigation.' }
    $outliers = $outlierJsonText | ConvertFrom-Json -Depth 20
    $projectedPagesJson = ConvertTo-Json -InputObject @($outliers.pages) -Depth 12 -Compress
    $expectedProjectionSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.UTF8Encoding]::new($false).GetBytes($projectedPagesJson))).ToLowerInvariant()
    if ($outliers.provenance.generatorSha256 -ne $expectedGeneratorSha -or $outliers.provenance.inputSha256 -ne $expectedProjectionSha -or $outliers.provenance.inputKind -ne 'alias-only-page-count-projection' -or $outliers.provenance.generatorCanonicalization -ne 'raw-file-bytes' -or $outliers.provenance.inputCanonicalization -ne 'powershell-json-compact-depth-12-utf8-v1') { throw 'Alias-only outlier projection omitted exact privacy-safe provenance.' }
    if ($outliers.schemaVersion -ne 'webforms-application-outliers.v1' -or $outliers.ruleId -ne 'diagnostic.webforms.application-outlier-ranking.v1' -or $outliers.pages[0].pageId -ne 'page-001' -or $outliers.pages[0].chainOutcomes.otherIncomplete -ne 1 -or $outliers.pages[0].counts.projectionReuse -ne 1 -or $outliers.pages[0].counts.normalizedCallSites -ne 1 -or $outliers.pages[0].counts.callEvidenceCeilingChains -ne 1) { throw 'Alias-only outlier projection was incomplete or unstable.' }
    foreach ($expected in @('Alias-only Web Forms outlier review', 'Highest normalized source call-site counts', 'Call-evidence retention ceiling reached', 'Most other incomplete chains', 'Retained calls without an observed boundary')) {
        if (!$outlierHtml.Contains($expected, [StringComparison]::Ordinal)) { throw "Alias-only outlier HTML missing: $expected" }
    }
    foreach ($privateValue in @('Pages/First.aspx','App.First.Go_Click()','Go (Button)','surface-one','packet-one','scan-one',('a' * 40))) {
        if ($outlierJsonText.Contains($privateValue, [StringComparison]::OrdinalIgnoreCase) -or $outlierHtml.Contains($privateValue, [StringComparison]::OrdinalIgnoreCase)) { throw "Alias-only outlier artifact disclosed private value: $privateValue" }
    }

    $receiptArtifacts = @('application-handoff.json','application-outliers.shareable.json','page-001.handoff.json') | ForEach-Object {
        $artifactPath = Join-Path $workbench $_
        $file = Get-Item -LiteralPath $artifactPath
        [ordered]@{ path = "workbench/$_"; bytes = $file.Length; sha256 = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $receipt = [ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        run = [ordered]@{ state = 'completed' }
        stages = [ordered]@{ workbench = [ordered]@{ state = 'completed'; artifacts = @($receiptArtifacts) } }
    }
    [IO.File]::WriteAllText((Join-Path $outputRoot 'run-receipt.json'), (($receipt | ConvertTo-Json -Depth 10) + "`n"), [Text.UTF8Encoding]::new($false))
    $exportOutput = @(& $exportScript -ReviewRoot $outputRoot -PageId 'page-001')
    $shareablePath = Join-Path $workbench 'page-001.paths.shareable.json'
    $shareableZip = Join-Path $workbench 'page-001.paths.shareable.zip'
    if (!(Test-Path -LiteralPath $shareablePath -PathType Leaf) -or !(Test-Path -LiteralPath $shareableZip -PathType Leaf)) { throw 'Page shareable exporter omitted JSON or ZIP output.' }
    if (@($exportOutput | Where-Object { $_ -eq "zipPath=$shareableZip" }).Count -ne 1) { throw 'Page shareable exporter did not identify its ZIP output.' }
    $shareableText = [IO.File]::ReadAllText($shareablePath)
    $shareable = $shareableText | ConvertFrom-Json -Depth 30
    $shareableProjectionJson = ConvertTo-Json -InputObject $shareable.projection -Depth 20 -Compress
    $shareableProjectionHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.UTF8Encoding]::new($false).GetBytes($shareableProjectionJson))).ToLowerInvariant()
    if ($shareable.schemaVersion -ne 'webforms-page-paths-shareable.v1' -or $shareable.privacy -ne 'anonymous-structure-only' -or $shareable.projection.pageId -ne 'page-001' -or $shareable.provenance.inputSha256 -ne $shareableProjectionHash) { throw 'Page shareable artifact schema, privacy, alias, or sanitized-input provenance was invalid.' }
    if (@($shareable.projection.chains).Count -ne 5 -or @($shareable.projection.callSites).Count -ne 1 -or @($shareable.projection.callSites[0].structuralSignals | Where-Object { $_ -eq 'telerik-call' }).Count -ne 1) { throw 'Page shareable artifact omitted anonymous chain, normalized-site, or structural-signal detail.' }
    $expectedChainAliases = @('chain-001','chain-002','chain-003','chain-004','chain-005')
    if ((@($shareable.projection.chains.chainAlias) -join ',') -ne ($expectedChainAliases -join ',')) { throw 'Page shareable chain aliases did not preserve deterministic retained source-evidence order.' }
    if (@($shareable.projection.chains | Where-Object { $_.classification -eq 'terminal-reached' -and $_.terminalKind -eq 'database' }).Count -ne 1) { throw 'Page shareable artifact did not map packet path outcomes into its coarse public vocabulary.' }
    $clientHttpChains = @($shareable.projection.chains | Where-Object { $_.originKind -eq 'client-http' -and $_.handlerState -eq 'resolved-static-handler' })
    if ($clientHttpChains.Count -ne 2 -or $clientHttpChains[0].endpointAlias -eq 'unavailable' -or $clientHttpChains[0].endpointAlias -ne $clientHttpChains[1].endpointAlias -or $clientHttpChains[0].handlerAlias -ne $clientHttpChains[1].handlerAlias) { throw 'Page shareable artifact did not preserve anonymous shared endpoint/handler equality.' }
    $missingChain = @($shareable.projection.chains | Where-Object { $_.handlerState -eq 'handler-unavailable' })[0]
    if ($null -eq $missingChain -or $missingChain.handlerResolution -ne 'missing-linked-method') { throw 'Page shareable artifact omitted anonymous handler-resolution detail.' }
    if ($shareable.provenance.generatorSha256 -ne (Get-FileHash -LiteralPath $exportScript -Algorithm SHA256).Hash.ToLowerInvariant() -or $shareable.provenance.sourceWorkbenchGeneratorSha256 -ne $expectedGeneratorSha) { throw 'Page shareable artifact omitted exact generator provenance.' }
    foreach ($privateValue in @('Pages/First.aspx','Pages/First.aspx.cs','api/SaveAudit.ashx.vb','SaveAudit.ashx','App.First.Go_Click()','SaveAudit.ProcessRequest/1','Telerik.Web.UI.SampleControl','Telerik.Web.UI','Go.Click','surface-one','handler-http','fact-http-call','call-site-one','packet-one','scan-one',('a' * 40))) {
        if ($shareableText.Contains($privateValue, [StringComparison]::OrdinalIgnoreCase)) { throw "Page shareable artifact disclosed private value: $privateValue" }
    }
    $expanded = Join-Path $temp 'expanded-page-shareable'
    Expand-Archive -LiteralPath $shareableZip -DestinationPath $expanded
    if (@(Get-ChildItem -LiteralPath $expanded -File).Count -ne 1 -or !(Test-Path -LiteralPath (Join-Path $expanded 'page-001.paths.shareable.json') -PathType Leaf)) { throw 'Page shareable ZIP did not contain exactly the anonymous JSON artifact.' }

    $standaloneConfig = Join-Path $temp 'standalone-review.json'
    [IO.File]::WriteAllText($standaloneConfig, (([ordered]@{ outputRoot = $outputRoot } | ConvertTo-Json) + "`n"), [Text.UTF8Encoding]::new($false))
    $standaloneOutput = @(& $standaloneScript -PacketPath $packetPath -ConfigPath $standaloneConfig -PageId 'page-001')
    $standaloneRootLine = @($standaloneOutput | Where-Object { $_ -like 'standaloneReviewRoot=*' })
    if ($standaloneRootLine.Count -ne 1) { throw 'Standalone review did not report exactly one review root.' }
    $standaloneRoot = $standaloneRootLine[0].Substring('standaloneReviewRoot='.Length)
    $standaloneReceiptPath = Join-Path $standaloneRoot 'run-receipt.json'
    $standaloneZip = Join-Path $standaloneRoot 'workbench/page-001.paths.shareable.zip'
    if (!(Test-Path -LiteralPath $standaloneReceiptPath -PathType Leaf) -or !(Test-Path -LiteralPath $standaloneZip -PathType Leaf)) { throw 'Standalone review omitted its receipt or requested anonymous page export.' }
    $standaloneReceipt = [IO.File]::ReadAllText($standaloneReceiptPath) | ConvertFrom-Json -Depth 20
    if ($standaloneReceipt.ruleId -ne 'diagnostic.webforms.standalone-review-receipt.v1' -or
        $standaloneReceipt.run.state -ne 'completed' -or
        $standaloneReceipt.provenance.inputSha256 -ne (Get-FileHash -LiteralPath $packetPath -Algorithm SHA256).Hash.ToLowerInvariant()) {
        throw 'Standalone review receipt did not preserve packet provenance.'
    }
    if (@($standaloneOutput | Where-Object { $_ -eq "zipPath=$standaloneZip" }).Count -ne 1) { throw 'Standalone review export did not identify the ZIP created from its new workbench.' }
    $latestExportOutput = @(& $latestExportScript -ReviewRoot $outputRoot -PriorPageId 'page-001')
    if (@($latestExportOutput | Where-Object { $_ -eq 'priorPageId=page-001' }).Count -ne 1 -or
        @($latestExportOutput | Where-Object { $_ -eq 'latestPageId=page-001' }).Count -ne 1 -or
        @($latestExportOutput | Where-Object { $_ -eq "zipPath=$standaloneZip" }).Count -ne 1) {
        throw 'Latest standalone page exporter did not map and export the receipted page.'
    }

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
    $reviewedHandoff = [IO.File]::ReadAllText((Join-Path $reviewedWorkbench 'page-001.handoff.json')) | ConvertFrom-Json -Depth 30
    $expectedReviewHash = (Get-FileHash -LiteralPath $reviewPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($reviewedHandoff.provenance.reviewOverlayKind -ne 'wits-application-review.v1' -or $reviewedHandoff.provenance.reviewOverlaySha256 -ne $expectedReviewHash -or $reviewedHandoff.provenance.reviewOverlayCanonicalization -ne 'raw-file-bytes') { throw 'Validated review overlay was not included in exact handoff provenance.' }
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
