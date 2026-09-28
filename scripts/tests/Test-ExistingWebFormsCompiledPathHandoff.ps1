[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$generator = Join-Path $TraceMapRoot 'scripts/New-ExistingWebFormsCompiledPathHandoff.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-compiled-handoff-test-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory((Join-Path $root 'scan'))
try {
    $commit = 'a' * 40
    $manifest = [ordered]@{
        commitSha = $commit; scanId = 'scan-public'; repoName = 'PublicProof'; remoteUrl = 'https://example.invalid/public'
        compiledInputProvenance = [ordered]@{ outcomes = @([ordered]@{
            outcome = 'admitted'; safeLocator = 'compiled:public'; rawFileSha256 = ('b' * 64)
            assemblyIdentity = 'PublicProof'; provenanceState = 'bound'; gapKinds = @()
        }) }
    }
    $receipt = [ordered]@{ sourceCommitSha = $commit }
    $nodes = @(
        [ordered]@{ nodeId = 'source'; displayName = '<Names_Init>'; scanId = 'scan-public'; commitSha = $commit; surfaceKind = $null },
        [ordered]@{ nodeId = 'compiled'; displayName = 'assembly:name:11:PublicProof|type(namespace:11:PublicProof|names:10:LookupPage|arity:0)|method:10:Names_Init|arity:0'; scanId = 'scan-public'; commitSha = $commit; surfaceKind = $null },
        [ordered]@{ nodeId = 'query'; displayName = 'sql-query'; scanId = 'scan-public'; commitSha = $commit; surfaceKind = 'sql-query' }
    )
    $edges = @(
        [ordered]@{ edgeKind = 'projectless-publish-method-candidate'; fromNodeId = 'source'; toNodeId = 'compiled'; ruleId = 'combined.paths.projectless-publish-candidate.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; filePath = 'Pages/Lookup.aspx.vb'; startLine = 4; endLine = 4; supportingFactIds = @('fact-source','fact-method') },
        [ordered]@{ edgeKind = 'compiled-il-call'; fromNodeId = 'compiled'; toNodeId = 'query'; ruleId = 'dotnet.compiled.il-call.v1'; evidenceTier = 'Tier2Structural'; filePath = 'compiled:public'; startLine = 1; endLine = 1; supportingFactIds = @('fact-il') }
    )
    $gaps = @(1..300 | ForEach-Object { [ordered]@{
        gapId = "gap-$_"; gapKind = 'PublicGap'; ruleId = 'public.gap.v1'
        evidenceTier = 'Tier4Unknown'; message = 'Public synthetic gap'
        reason = 'MissingPublicInput'; commitSha = $commit; sourceIndexId = 'public-index'; filePath = 'Pages/Lookup.aspx.vb'; startLine = 4; endLine = 5; combinedFactId = 'public-gap-fact'
    } })
    $report = [ordered]@{
        version = '1.0'; reportCoverage = 'ReducedCoverage'; coverageWarnings = @('public test warning')
        sources = @([ordered]@{ scanId = 'scan-public'; commitSha = $commit; repoName = 'PublicProof'; remoteUrl = 'https://example.invalid/public' })
        query = [ordered]@{ fromSymbol = 'Names_Init'; toSurface = 'sql-query'; maxDepth = 20; maxPaths = 256 }
        summary = [ordered]@{ pathCount = 1; gapCount = 300; truncated = $false }
        paths = @([ordered]@{ pathId = 'path-public'; classification = 'NeedsReviewStaticPath'; nodes = $nodes; edges = $edges; supportingFactIds = @('fact-source','fact-method','fact-il'); notes = @() })
        gaps = $gaps
    }
    [IO.File]::WriteAllText((Join-Path $root 'scan/scan-manifest.json'), (($manifest | ConvertTo-Json -Depth 15) + "`n"))
    [IO.File]::WriteAllText((Join-Path $root 'publish-receipt.local.json'), (($receipt | ConvertTo-Json -Depth 5) + "`n"))
    $originalSqlReport = (($report | ConvertTo-Json -Depth 20) + "`n")
    [IO.File]::WriteAllText((Join-Path $root 'handler-paths.json'), $originalSqlReport)
    [IO.File]::WriteAllText((Join-Path $root 'combined.sqlite'), 'public-test-index')
    $replayFailure = $null
    try { & (Join-Path $TraceMapRoot 'scripts/Replay-ExistingWebFormsCompiledPathReviews.ps1') `
        -ProofRoot $root *> $null }
    catch { $replayFailure = $_.Exception.Message }
    if ($replayFailure -cne 'WEBFORMS_COMPILED_REPLAY_HIGH_WORK_PROOF_UNAVAILABLE') {
        throw "WEBFORMS_COMPILED_REPLAY_LOWER_WORK_ACCEPTED:$replayFailure"
    }
    $lines = @(& $generator -ProofRoot $root)
    $json = [IO.File]::ReadAllText((Join-Path $root 'compiled-path-review/handler.handoff.local.json')) | ConvertFrom-Json -Depth 40
    $html = [IO.File]::ReadAllText((Join-Path $root 'compiled-path-review/handler.local.html'))
    if ($lines -cnotcontains 'compiledPathReviewStatus=valid' -or
        $lines -cnotcontains 'compiledPathReviewReviewOnly=1' -or
        $json.schemaVersion -ne 'webforms-compiled-path-handoff.v1' -or
        $json.provenance.generatorSha256 -ne (Get-FileHash -LiteralPath $generator -Algorithm SHA256).Hash.ToLowerInvariant() -or
        $json.provenance.boundedInputSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $json.paths[0].hops.Count -ne 2 -or
        $json.paths[0].claim -ne 'review-only-static-path' -or
        $json.paths[0].hops[0].to.displayLabel -cne 'PublicProof.LookupPage.Names_Init()' -or
        $json.paths[0].hops[0].to.name -cne $nodes[1].displayName -or
        $json.coverage.gapCount -ne 300 -or
        @($json.coverage.gaps).Count -ne 256 -or
        $json.coverage.omittedGapDetailCount -ne 44 -or
        $json.coverage.gapCounts[0].count -ne 300 -or
        $json.coverage.gaps[0].combinedFactId -cne 'public-gap-fact' -or
        $json.coverage.gaps[0].sourceIndexId -cne 'public-index' -or
        !$html.Contains('Pages/Lookup.aspx.vb:4-5', [StringComparison]::Ordinal) -or
        $json.paths[0].hops[0].ruleId -ne 'combined.paths.projectless-publish-candidate.v1' -or
        $json.assemblies[0].rawFileSha256 -ne ('b' * 64) -or
        !$html.Contains('&lt;Names_Init&gt;', [StringComparison]::Ordinal) -or
        !$html.Contains('PublicProof.LookupPage.Names_Init()', [StringComparison]::Ordinal) -or
        !$html.Contains('Exact identities', [StringComparison]::Ordinal) -or
        $html.Contains('<Names_Init>', [StringComparison]::Ordinal) -or
        !$html.Contains('Showing 256 of 300 gap details', [StringComparison]::Ordinal) -or
        !$html.Contains('not runtime execution', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'WEBFORMS_COMPILED_HANDOFF_PUBLIC_PROJECTION_INVALID'
    }
    foreach ($attempt in 1..2) {
        $repeated = @(& (Join-Path $TraceMapRoot 'scripts/Replay-ExistingWebFormsCompiledPathReviews.ps1') -ProofRoot $root -AllowBaseIndex)
        if (@($repeated | Where-Object { $_ -cmatch '^compiledPathReviewHandoff=' }).Count -ne 1) { throw 'REPLAY_SQL_HANDOFF_MISSING' }
    }
    $report.gaps[0].ruleId = ''
    [IO.File]::WriteAllText((Join-Path $root 'handler-paths.json'), ($report | ConvertTo-Json -Depth 30))
    $captured = $null
    try { & $generator -ProofRoot $root -OutputDirectory (Join-Path $root 'invalid-gap') *> $null } catch { $captured = $_.Exception.Message }
    if ($captured -cne 'WEBFORMS_COMPILED_HANDOFF_GAP_INVALID') { throw "INVALID_GAP_ACCEPTED:$captured" }
    $report.gaps[0].ruleId = 'public.gap.v1'
    $report.gaps[0].evidenceTier = 'invalid-tier'
    [IO.File]::WriteAllText((Join-Path $root 'handler-paths.json'), ($report | ConvertTo-Json -Depth 30))
    $captured = $null
    try { & $generator -ProofRoot $root -OutputDirectory (Join-Path $root 'invalid-tier') *> $null } catch { $captured = $_.Exception.Message }
    if ($captured -cne 'WEBFORMS_COMPILED_HANDOFF_GAP_INVALID') { throw "INVALID_GAP_TIER_ACCEPTED:$captured" }
    $report.gaps[0].evidenceTier = 'Tier4Unknown'
    $report.sources[0].repoName = 'OtherPublicRepository'
    [IO.File]::WriteAllText((Join-Path $root 'handler-paths.json'), ($report | ConvertTo-Json -Depth 30))
    $captured = $null
    try { & $generator -ProofRoot $root -OutputDirectory (Join-Path $root 'invalid-repository') *> $null } catch { $captured = $_.Exception.Message }
    if ($captured -cne 'WEBFORMS_COMPILED_HANDOFF_REPOSITORY_MISMATCH') { throw "MIXED_REPOSITORY_ACCEPTED:$captured" }
    $report.sources[0].repoName = 'PublicProof'
    $report.paths[0].edges[0].toNodeId = 'wrong'
    [IO.File]::WriteAllText((Join-Path $root 'handler-paths.json'), (($report | ConvertTo-Json -Depth 20) + "`n"))
    $captured = $null
    try { & $generator -ProofRoot $root -OutputDirectory (Join-Path $root 'invalid') *> $null }
    catch { $captured = $_.Exception.Message }
    if ($captured -ne 'WEBFORMS_COMPILED_HANDOFF_PATH_INVALID') {
        throw "WEBFORMS_COMPILED_HANDOFF_INVALID_PATH_ACCEPTED:$captured"
    }
    [IO.File]::WriteAllText((Join-Path $root 'combined.sqlite'), '')
    $captured = $null
    try { & $generator -ProofRoot $root -OutputDirectory (Join-Path $root 'empty-index') *> $null }
    catch { $captured = $_.Exception.Message }
    if ($captured -ne 'WEBFORMS_COMPILED_HANDOFF_INPUT_LIMIT;slot=combinedIndex;bytes=0;max=4294967296') {
        throw "WEBFORMS_COMPILED_HANDOFF_LIMIT_DIAGNOSTIC_INVALID:$captured"
    }
    [IO.File]::WriteAllText((Join-Path $root 'combined.sqlite'), 'public-test-index')
    [IO.File]::WriteAllText((Join-Path $root 'handler-paths.json'), $originalSqlReport)
    $report.paths[0].edges[0].toNodeId = 'compiled'
    $report.paths[0].nodes[-1].surfaceKind = 'database-api'
    $report.paths[0].edges[-1].edgeKind = 'compiled-database-api-candidate'
    $report.query.toSurface = 'database-api'
    $apiPath = Join-Path $root 'handler-api-paths.json'
    $apiReceiptPath = Join-Path $root 'handler-api-paths.receipt.json'
    [IO.File]::WriteAllText((Join-Path $root 'scan/facts.ndjson'), 'public-facts')
    function Recheck-InputSha([string]$Index, [string]$Facts) {
        $inventory = (Get-FileHash $Index).Hash.ToLowerInvariant() + "`n" +
            (Get-FileHash (Join-Path $root 'handler-paths.json')).Hash.ToLowerInvariant() + "`n" +
            (Get-FileHash $Facts).Hash.ToLowerInvariant()
        return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($inventory))).ToLowerInvariant()
    }
    [IO.File]::WriteAllText($apiPath, (($report | ConvertTo-Json -Depth 20) + "`n"))
    [IO.File]::WriteAllText($apiReceiptPath, (([ordered]@{
        schemaVersion = 'webforms-path-recheck.v1'
        generatorSha256 = 'c' * 64
        boundedInputSha256 = Recheck-InputSha (Join-Path $root 'combined.sqlite') (Join-Path $root 'scan/facts.ndjson')
        scanFolder = 'scan'; combinedIndex = 'combined.sqlite'
        pathReportSha256 = (Get-FileHash $apiPath).Hash.ToLowerInvariant()
    } | ConvertTo-Json) + "`n"))
    & $generator -ProofRoot $root -PathReportPath $apiPath `
        -PathReportReceiptPath $apiReceiptPath -ToSurface database-api *> $null
    $apiHandoff = [IO.File]::ReadAllText((Join-Path $root 'compiled-api-review/handler.handoff.local.json')) |
        ConvertFrom-Json -Depth 40
    if ($apiHandoff.query.toSurface -ne 'database-api' -or
        $apiHandoff.provenance.pathReportGeneration.generatorSha256 -ne ('c' * 64) -or
        $apiHandoff.paths[0].claim -ne 'review-only-static-path' -or
        @($apiHandoff.paths[0].hops.edgeKind) -cnotcontains 'compiled-database-api-candidate') {
        throw 'WEBFORMS_COMPILED_HANDOFF_PUBLIC_API_PROJECTION_INVALID'
    }
    $highWork = 30000000
    $highScan = Join-Path $root "scan-ilwork-$highWork"
    [void][IO.Directory]::CreateDirectory($highScan)
    [IO.File]::WriteAllText((Join-Path $highScan 'scan-manifest.json'), (($manifest | ConvertTo-Json -Depth 15) + "`n"))
    [IO.File]::WriteAllText((Join-Path $root "combined-ilwork-$highWork.sqlite"), 'public-test-high-work-index')
    [IO.File]::WriteAllText((Join-Path $highScan 'facts.ndjson'), 'public-facts')
    $highReceiptPath = Join-Path $root 'handler-high-work.receipt.json'
    [IO.File]::WriteAllText($highReceiptPath, (([ordered]@{
        schemaVersion = 'webforms-path-recheck.v1'
        generatorSha256 = 'e' * 64
        boundedInputSha256 = Recheck-InputSha (Join-Path $root "combined-ilwork-$highWork.sqlite") (Join-Path $highScan 'facts.ndjson')
        pathReportSha256 = (Get-FileHash $apiPath).Hash.ToLowerInvariant()
        scanFolder = "scan-ilwork-$highWork"
        combinedIndex = "combined-ilwork-$highWork.sqlite"
    } | ConvertTo-Json) + "`n"))
    & $generator -ProofRoot $root -PathReportPath $apiPath `
        -PathReportReceiptPath $highReceiptPath -ToSurface database-api -IlMaxWork $highWork *> $null
    $highHandoff = [IO.File]::ReadAllText((Join-Path $root "compiled-api-review-ilwork-$highWork/handler.handoff.local.json")) |
        ConvertFrom-Json -Depth 40
    if ($highHandoff.provenance.scanFolder -cne "scan-ilwork-$highWork" -or
        $highHandoff.provenance.combinedIndex -cne "combined-ilwork-$highWork.sqlite" -or
        $highHandoff.provenance.inputSha256.combinedIndex -cne
            (Get-FileHash -LiteralPath (Join-Path $root "combined-ilwork-$highWork.sqlite") -Algorithm SHA256).Hash.ToLowerInvariant() -or
        $highHandoff.paths.Count -ne 1) {
        throw 'WEBFORMS_COMPILED_HANDOFF_HIGH_WORK_PROVENANCE_INVALID'
    }
    $highFactsPath = Join-Path $highScan 'facts.ndjson'
    [IO.File]::WriteAllText($highFactsPath, "{`"factType`":`"PublicSynthetic`"}`n")
    $savedFolder = Join-Path $root 'path-recheck-public'
    [void][IO.Directory]::CreateDirectory($savedFolder)
    $emptySql = $originalSqlReport | ConvertFrom-Json -Depth 30
    $emptySql.paths = @()
    $emptySql.summary.pathCount = 0
    [IO.File]::WriteAllText((Join-Path $root 'handler-paths.json'),
        (($emptySql | ConvertTo-Json -Depth 30) + "`n"))
    $report.query.surfaceName = 'DbDataAdapter.Fill'
    $report.summary.selectorCandidateCount = 1
    [IO.File]::WriteAllText((Join-Path $savedFolder 'handler-paths.local.json'),
        (($report | ConvertTo-Json -Depth 20) + "`n"))
    $inputInventory = (Get-FileHash -LiteralPath (Join-Path $root "combined-ilwork-$highWork.sqlite") -Algorithm SHA256).Hash.ToLowerInvariant() + "`n" +
        (Get-FileHash -LiteralPath (Join-Path $root 'handler-paths.json') -Algorithm SHA256).Hash.ToLowerInvariant() + "`n" +
        (Get-FileHash -LiteralPath $highFactsPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $inputSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($inputInventory))).ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $savedFolder 'path-recheck.receipt.local.json'),
        (([ordered]@{ schemaVersion = 'webforms-path-recheck.v1'; generatorSha256 = 'e' * 64
            boundedInputSha256 = $inputSha; scanFolder = "scan-ilwork-$highWork"
            combinedIndex = "combined-ilwork-$highWork.sqlite"
            pathReportSha256 = (Get-FileHash (Join-Path $savedFolder 'handler-paths.local.json')).Hash.ToLowerInvariant() } | ConvertTo-Json) + "`n"))
    $savedLines = @(& (Join-Path $TraceMapRoot 'scripts/Replay-ExistingWebFormsCompiledPathReviews.ps1') `
        -ProofRoot $root)
    if ($savedLines -cnotcontains 'compiledReplaySavedApi=True' -or
        $savedLines -cnotcontains 'compiledApiPaths=1' -or
        $savedLines -cnotcontains 'compiledPathReviewPaths=1' -or
        !(Test-Path -LiteralPath (Join-Path $savedFolder 'compiled-api-review/handler.local.html') -PathType Leaf)) {
        throw "WEBFORMS_COMPILED_REPLAY_SAVED_API_NOT_PROJECTED:$($savedLines -join ';')"
    }
    [IO.File]::AppendAllText((Join-Path $savedFolder 'handler-paths.local.json'), ' ')
    $captured = $null
    try { & (Join-Path $TraceMapRoot 'scripts/Replay-ExistingWebFormsCompiledPathReviews.ps1') -ProofRoot $root *> $null } catch { $captured = $_.Exception.Message }
    if ($captured -cne 'WEBFORMS_COMPILED_REPLAY_API_REPORT_HASH_MISMATCH') { throw "TAMPERED_REPORT_ACCEPTED:$captured" }
    $savedReportPath = Join-Path $savedFolder 'handler-paths.local.json'
    [IO.File]::WriteAllText($savedReportPath, (($report | ConvertTo-Json -Depth 20) + "`n"))
    $savedReceiptPath = Join-Path $savedFolder 'path-recheck.receipt.local.json'
    $legacyReceipt = [IO.File]::ReadAllText($savedReceiptPath) | ConvertFrom-Json -AsHashtable
    $legacyReceipt.Remove('pathReportSha256')
    [IO.File]::WriteAllText($savedReceiptPath, ($legacyReceipt | ConvertTo-Json))
    $captured = $null
    try { & (Join-Path $TraceMapRoot 'scripts/Replay-ExistingWebFormsCompiledPathReviews.ps1') -ProofRoot $root *> $null } catch { $captured = $_.Exception.Message }
    if ($captured -cne 'WEBFORMS_COMPILED_REPLAY_API_RECEIPT_RECHECK_REQUIRED') { throw "LEGACY_RECEIPT_ACCEPTED:$captured" }
    $badReceipt = Join-Path $root 'handler-high-work-bad.receipt.json'
    [IO.File]::WriteAllText($badReceipt, (([ordered]@{
        schemaVersion = 'webforms-path-recheck.v1'
        generatorSha256 = 'e' * 64
        boundedInputSha256 = 'f' * 64
        scanFolder = 'scan'
        combinedIndex = 'combined.sqlite'
    } | ConvertTo-Json) + "`n"))
    $captured = $null
    try { & $generator -ProofRoot $root -PathReportPath $apiPath `
        -PathReportReceiptPath $badReceipt -ToSurface database-api -IlMaxWork $highWork `
        -OutputDirectory (Join-Path $root 'bad-high-work') *> $null }
    catch { $captured = $_.Exception.Message }
    if ($captured -cne 'WEBFORMS_COMPILED_HANDOFF_API_RECEIPT_INVALID') {
        throw "WEBFORMS_COMPILED_HANDOFF_MIXED_INDEX_ACCEPTED:$captured"
    }
    Write-Output 'webFormsCompiledHandoffPublicTests=passed'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
