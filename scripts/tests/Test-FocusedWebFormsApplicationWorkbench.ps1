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
$packet = [ordered]@{
    schemaVersion = 'webforms-modernization-packet.v1'; packetId = 'packet-one'; ruleId = 'legacy.webforms.modernization-packet.v1'; claimLevel = 'local-only'; coverage = 'reduced'
    sources = @(@{ sourceId = 'source-one'; repositoryId = 'repo-one'; scanId = 'scan-one'; commitSha = ('a' * 40); analysisLevel = 'semantic'; buildStatus = 'succeeded' })
    summary = @{ projectCount = 1; surfaceCount = 2; eventChainCount = 1; downstreamBoundaryCount = 1; identityStateCount = 1; batchDataMovementCount = 1; structuralSliceCandidateCount = 1; gapCount = 1; truncated = $false }
    projects = @(@{ projectId = 'project-one'; surfaceCount = 2; evidence = @(); supportingFactIds = @() })
    surfaces = @(
        @{ surfaceId = 'Surface-One'; surfaceKind = 'page'; projectId = 'project-one'; compositionTargetIds = @(); controlIds = @(); evidence = @{ factId = 'fact-surface-2'; ruleId = 'legacy.webforms.surface.v1'; evidenceTier = 'Tier2Structural'; coverageLabel = 'complete'; commitSha = ('a' * 40); filePath = 'Pages/Second.aspx'; startLine = 1; endLine = 1; extractorId = 'legacy-webforms'; extractorVersion = '1'; supportingFactIds = @(); supportingEdgeIds = @(); limitations = @() }; supportingEvidence = @(); supportingFactIds = @() },
        @{ surfaceId = 'surface-one'; surfaceKind = 'page'; projectId = 'project-one'; compositionTargetIds = @(); controlIds = @('Go'); evidence = $evidence; supportingEvidence = @(); supportingFactIds = @('fact-surface-1') }
    )
    eventChains = @(@{ chainId = 'chain-one'; surfaceId = 'surface-one'; eventSourceId = 'Go.Click'; bindingFactId = 'binding-one'; handlerId = 'handler-one'; handlerFactId = 'fact-handler'; handlerSymbol = 'App.First.Go_Click()'; classification = 'terminal-reached'; legacyPathId = 'path-one'; terminalKind = 'database'; evidence = @($evidence); pathEvidence = @(); supportingFactIds = @('fact-handler'); supportingEdgeIds = @(); ruleIds = @('legacy.webforms.event-flow.v1'); evidenceTiers = @('Tier1Semantic'); coverageLabels = @('complete'); limitations = @(); traversalObservation = @{ stopState = 'supported-terminal-reached' } })
    downstreamBoundaries = @(@{ boundaryId = 'boundary-one'; chainId = 'chain-one'; surfaceId = 'surface-one'; handlerId = 'handler-one'; boundaryCategory = 'database'; boundaryKind = 'stored-procedure-candidate'; boundaryTargetId = 'target-one'; terminalEvidenceId = 'fact-db'; classification = 'retained'; legacyPathId = 'path-one'; evidence = @($evidence); pathEvidence = @(); supportingFactIds = @('fact-db'); supportingEdgeIds = @(); ruleIds = @('legacy.boundary.v1'); evidenceTiers = @('Tier2Structural'); coverageLabels = @('complete'); limitations = @() })
    identityStateInventory = @(
        @{ identityStateId = 'identity-one'; identityKind = 'session'; classification = 'observed'; surfaceId = 'surface-one'; safeMetadata = @{}; evidence = $evidence; supportingFactIds = @(); limitations = @() },
        @{ identityStateId = 'identity-unassociated'; identityKind = 'principal'; classification = 'observed'; surfaceId = $null; safeMetadata = @{}; evidence = $evidence; supportingFactIds = @(); limitations = @() }
    )
    batchDataMovementInventory = @(
        @{ batchDataMovementId = 'batch-one'; surfaceKind = 'file-data-movement'; mechanism = 'system-io'; operationKind = 'read'; ownerStatus = 'member-declared'; projectResolution = 'resolved'; projectId = 'project-one'; safeMetadata = @{}; evidence = $evidence; supportingFactIds = @(); limitations = @() },
        @{ batchDataMovementId = 'batch-unassociated'; surfaceKind = 'database-operation'; mechanism = 'ado-net'; operationKind = 'read'; ownerStatus = 'member-declared'; projectResolution = 'unmatched'; projectId = 'project-missing'; safeMetadata = @{}; evidence = $evidence; supportingFactIds = @(); limitations = @() }
    )
    structuralSliceCandidates = @(@{ candidateId = 'candidate-one'; classification = 'structural'; ruleId = 'legacy.slice.v1'; evidenceTier = 'Tier2Structural'; ownerNamingRequired = $true; surfaceIds = @('surface-one'); evidence = @($evidence); supportingFactIds = @(); coverageLabels = @('complete'); limitations = @() })
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
    if ($index.IndexOf('Pages/First.aspx', [StringComparison]::Ordinal) -gt $index.IndexOf('Pages/Second.aspx', [StringComparison]::Ordinal)) { throw 'Pages were not ordered by retained path.' }
    if (!$index.Contains('43', [StringComparison]::Ordinal) -and !$index.Contains('2 selected surfaces', [StringComparison]::Ordinal)) { throw 'Index did not report surface count.' }
    $first = [IO.File]::ReadAllText((Join-Path $workbench 'page-001.html'))
    foreach ($expected in @('Go.Click', 'App.First.Go_Click()', 'stored-procedure-candidate', 'HandlerTerminalUnavailable', 'Tier4Unknown', ('a' * 40), 'legacy-webforms/1', 'identity-one', 'batch-one', 'candidate-one', 'Raw source omitted')) {
        if (!$first.Contains($expected, [StringComparison]::Ordinal)) { throw "Page report missing: $expected" }
    }
    $handoff = [IO.File]::ReadAllText((Join-Path $workbench 'page-001.handoff.json')) | ConvertFrom-Json -Depth 30
    if ($handoff.subject.filePath -ne 'Pages/First.aspx' -or $handoff.counts.eventChains -ne 1 -or $handoff.evidenceDocs.status -ne 'supplied-read-only') { throw 'Page handoff projection was incomplete.' }
    if (@($handoff.retrievalHints).Count -ne 2 -or @($handoff.retrievalHints | Where-Object { !$_.recipeId }).Count -ne 0) { throw 'Retrieval hints were not serialized as a flat recipe list.' }
    $applicationHandoff = [IO.File]::ReadAllText((Join-Path $workbench 'application-handoff.json')) | ConvertFrom-Json -Depth 30
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
