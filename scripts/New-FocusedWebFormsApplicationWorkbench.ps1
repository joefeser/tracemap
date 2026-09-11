[CmdletBinding()]
param(
    [string]$PacketPath = '',
    [string]$OutputRoot = '',
    [string]$OutputDirectory = '',
    [string]$EvidenceDocsRoot = '',
    [string]$ReviewPath = '',
    [string]$SourceRoot = '',
    [switch]$IncludeRawSource,
    [ValidateRange(0, 50)]
    [int]$SourceContextLines = 4,
    [string]$ConfigPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-HtmlText([object]$Value) { [Net.WebUtility]::HtmlEncode([string]$Value) }
function Values([object]$Value) { if ($null -eq $Value) { @() } else { @($Value) } }
function New-StableAlias([string]$Prefix, [int]$Number) { '{0}-{1:d3}' -f $Prefix, $Number }

function Evidence-List([object[]]$Evidence) {
    if ($Evidence.Count -eq 0) { return '<p class="muted">No retained evidence rows in this packet.</p>' }
    $rows = foreach ($item in $Evidence) {
        $path = ConvertTo-HtmlText $item.filePath
        $span = if ($null -ne $item.startLine) { "L$($item.startLine)-$($item.endLine)" } else { 'span unavailable' }
        '<li><code>{0}</code> <code>{1}</code> <code>{2}</code> — {3}:{4}</li>' -f (ConvertTo-HtmlText $item.factId), (ConvertTo-HtmlText $item.ruleId), (ConvertTo-HtmlText $item.evidenceTier), $path, (ConvertTo-HtmlText $span)
    }
    '<ul>{0}</ul>' -f ($rows -join '')
}

function Path-Evidence-List([object[]]$Evidence) {
    if ($Evidence.Count -eq 0) { return '<p class="muted">No retained path-location rows in this packet.</p>' }
    $rows = foreach ($item in $Evidence) {
        $span = if ($null -ne $item.startLine) { "L$($item.startLine)-$($item.endLine)" } else { 'span unavailable' }
        '<li><code>{0}</code> <code>{1}</code> <code>{2}</code> — {3}:{4}</li>' -f (ConvertTo-HtmlText $item.evidenceId), (ConvertTo-HtmlText $item.ruleId), (ConvertTo-HtmlText $item.evidenceKind), (ConvertTo-HtmlText $item.filePath), (ConvertTo-HtmlText $span)
    }
    '<ul>{0}</ul>' -f ($rows -join '')
}

function Source-Excerpt([object]$Evidence, [string]$Root, [int]$Context) {
    if (!$Root -or $null -eq $Evidence -or !$Evidence.filePath -or $null -eq $Evidence.startLine) { return '' }
    $relative = ([string]$Evidence.filePath).Replace('/', [IO.Path]::DirectorySeparatorChar).Replace('\', [IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)') { return '<p class="warning">Source path was not safe to resolve.</p>' }
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $candidate = [IO.Path]::GetFullPath((Join-Path $rootPath $relative))
    if (!$candidate.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        !(Test-Path -LiteralPath $candidate -PathType Leaf)) { return '<p class="warning">Working-tree source was unavailable.</p>' }
    $file = Get-Item -LiteralPath $candidate
    if ($file.Length -gt 4MB) { return '<p class="warning">Working-tree source exceeded the 4 MiB excerpt bound.</p>' }
    $lines = [IO.File]::ReadAllLines($candidate)
    $first = [Math]::Max(1, [int]$Evidence.startLine - $Context)
    $last = [Math]::Min($lines.Count, [int]$Evidence.endLine + $Context)
    if ($last -lt $first) { return '' }
    $selected = for ($line = $first; $line -le $last; $line++) { '{0,6}  {1}' -f $line, $lines[$line - 1] }
    '<pre><code>{0}</code></pre>' -f (ConvertTo-HtmlText ($selected -join "`n"))
}

if (!$ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.json' }
if ((!$OutputRoot -or !$PacketPath) -and (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    . (Join-Path $PSScriptRoot 'webforms-review/FocusedWebFormsConfig.ps1')
    $config = Read-FocusedWebFormsConfig -ConfigPath $ConfigPath
    if (!$OutputRoot) { $OutputRoot = $config.OutputRoot }
}
if (!$OutputRoot) { throw 'ApplicationWorkbenchOutputRootRequired' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (!(Test-Path -LiteralPath $OutputRoot -PathType Container)) { [IO.Directory]::CreateDirectory($OutputRoot) | Out-Null }

if (!$PacketPath) {
    $latest = Get-ChildItem -LiteralPath $OutputRoot -File -Recurse -Filter 'webforms-modernization.json' |
        Where-Object { $_.FullName -match '[\\/]webforms-page-list-[^\\/]+[\\/]webforms-modernization\.json$' } |
        Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1
    if ($null -eq $latest) { throw 'ApplicationWorkbenchPacketUnavailable' }
    $PacketPath = $latest.FullName
}
if (!(Test-Path -LiteralPath $PacketPath -PathType Leaf)) { throw 'ApplicationWorkbenchPacketUnavailable' }
$packetFile = Get-Item -LiteralPath $PacketPath
if ($packetFile.Length -le 0 -or $packetFile.Length -gt 128MB) { throw 'ApplicationWorkbenchPacketLimit' }
$packet = [IO.File]::ReadAllText($packetFile.FullName) | ConvertFrom-Json -Depth 100
if ($packet.schemaVersion -ne 'webforms-modernization-packet.v1') { throw 'ApplicationWorkbenchPacketSchemaMismatch' }
$sources = @(Values $packet.sources)
if ($sources.Count -ne 1 -or !$sources[0].scanId -or !$sources[0].commitSha) { throw 'ApplicationWorkbenchPacketProvenanceMismatch' }
$surfaces = @(Values $packet.surfaces)
if ($surfaces.Count -lt 1 -or $surfaces.Count -gt 1000) { throw 'ApplicationWorkbenchSurfaceLimit' }
if ($IncludeRawSource -and (!$SourceRoot -or !(Test-Path -LiteralPath $SourceRoot -PathType Container))) { throw 'ApplicationWorkbenchSourceRootUnavailable' }
$reviewBySurface = @{}
if ($ReviewPath) {
    if (!(Test-Path -LiteralPath $ReviewPath -PathType Leaf)) { throw 'ApplicationWorkbenchReviewUnavailable' }
    $reviewValidator = Join-Path $PSScriptRoot 'webforms-review/Invoke-WitsApplicationReview.ps1'
    & $reviewValidator -Mode Validate -PacketPath $packetFile.FullName -ReviewPath $ReviewPath | Out-Null
    $review = [IO.File]::ReadAllText([IO.Path]::GetFullPath($ReviewPath)) | ConvertFrom-Json -Depth 40
    foreach ($decision in @($review.decisions)) { $reviewBySurface[[string]$decision.surfaceId] = $decision }
}
if ($EvidenceDocsRoot) {
    if (!(Test-Path -LiteralPath $EvidenceDocsRoot -PathType Container)) { throw 'ApplicationWorkbenchCorpusUnavailable' }
    foreach ($name in @('manifest.json', 'query-recipes.json', 'chunks.jsonl')) {
        $required = Join-Path $EvidenceDocsRoot $name
        if (!(Test-Path -LiteralPath $required -PathType Leaf) -or (Get-Item -LiteralPath $required).Length -le 0) { throw 'ApplicationWorkbenchCorpusUnavailable' }
    }
    $manifestFile = Get-Item -LiteralPath (Join-Path $EvidenceDocsRoot 'manifest.json')
    if ($manifestFile.Length -gt 64MB) { throw 'ApplicationWorkbenchCorpusLimit' }
    $manifest = [IO.File]::ReadAllText($manifestFile.FullName) | ConvertFrom-Json -Depth 100
    $sourceRefs = @(Values $manifest.inputs | ForEach-Object { Values $_.sourceRefs })
    $provenanceMatch = @($sourceRefs | Where-Object {
        $_.scanId -eq $sources[0].scanId -and
        ([string]$_.commitSha).Equals([string]$sources[0].commitSha, [StringComparison]::OrdinalIgnoreCase)
    }).Count -gt 0
    if ($manifest.schemaVersion -ne 'tracemap-evidence-docs.v1' -or !$manifest.tracemapGenerated -or !$provenanceMatch) {
        throw 'ApplicationWorkbenchCorpusProvenanceMismatch'
    }
}

$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
if (!$OutputDirectory) { $OutputDirectory = Join-Path $OutputRoot "webforms-application-workbench-$stamp" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$outputPrefix = $OutputRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (!$OutputDirectory.StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'ApplicationWorkbenchOutputOutsideRoot' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'ApplicationWorkbenchOutputExists' }
$parent = Split-Path -Parent $OutputDirectory
if (!(Test-Path -LiteralPath $parent -PathType Container)) { [IO.Directory]::CreateDirectory($parent) | Out-Null }
$staging = Join-Path $parent ('.' + (Split-Path -Leaf $OutputDirectory) + '.staging-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($staging) | Out-Null

try {
    [IO.File]::Copy($packetFile.FullName, (Join-Path $staging 'webforms-modernization.snapshot.json'), $false)
    $ordered = @($surfaces | Sort-Object @{ Expression = { [string]$_.evidence.filePath } }, @{ Expression = { [string]$_.surfaceId } })
    $pageRows = [Collections.Generic.List[object]]::new()
    $applicationPages = [Collections.Generic.List[object]]::new()
    $ordinal = 0
    foreach ($surface in $ordered) {
        $ordinal++
        $pageId = New-StableAlias 'page' $ordinal
        $chains = @(Values $packet.eventChains | Where-Object { $_.surfaceId -eq $surface.surfaceId } | Sort-Object chainId)
        $chainIds = @($chains | ForEach-Object { [string]$_.chainId })
        $boundaries = @(Values $packet.downstreamBoundaries | Where-Object { $_.surfaceId -eq $surface.surfaceId } | Sort-Object boundaryId)
        $identity = @(Values $packet.identityStateInventory | Where-Object { $_.surfaceId -eq $surface.surfaceId } | Sort-Object identityStateId)
        $batch = @(Values $packet.batchDataMovementInventory | Where-Object { $_.projectId -eq $surface.projectId } | Sort-Object batchDataMovementId)
        $candidates = @(Values $packet.structuralSliceCandidates | Where-Object { @($_.surfaceIds) -contains $surface.surfaceId } | Sort-Object candidateId)
        $gaps = @(Values $packet.gaps | Where-Object { $_.scopeId -eq $surface.surfaceId -or $chainIds -contains $_.scopeId } | Sort-Object gapId)
        $evidence = @($surface.evidence) + @(Values $surface.supportingEvidence) + @($chains | ForEach-Object { Values $_.evidence }) + @($boundaries | ForEach-Object { Values $_.evidence })
        $evidence = @($evidence | Where-Object { $null -ne $_ } | Sort-Object factId, filePath, startLine -Unique)
        $pathEvidence = @($chains | ForEach-Object { Values $_.pathEvidence }) + @($boundaries | ForEach-Object { Values $_.pathEvidence })
        $pathEvidence = @($pathEvidence | Where-Object { $null -ne $_ } | Sort-Object evidenceId, filePath, startLine -Unique)
        $handlers = @($chains | ForEach-Object { if ($_.handlerSymbol) { [string]$_.handlerSymbol } elseif ($_.handlerId) { [string]$_.handlerId } } | Where-Object { $_ } | Sort-Object -Unique)
        $coverage = @($chains | ForEach-Object { Values $_.coverageLabels } | Sort-Object -Unique)
        if ($coverage.Count -eq 0) { $coverage = @([string]$surface.evidence.coverageLabel) }
        $decision = if ($reviewBySurface.ContainsKey([string]$surface.surfaceId)) { $reviewBySurface[[string]$surface.surfaceId] } else { [pscustomobject]@{ verdict = 'unreviewed'; migrationDisposition = 'unassigned'; capabilityLabel = $null; comment = $null } }
        $handoff = [ordered]@{
            schemaVersion = 'webforms-application-page-handoff.v1'
            ruleId = 'diagnostic.webforms.application-page-handoff.v1'
            claimLevel = 'local-only'
            pageId = $pageId
            packet = [ordered]@{ packetId = [string]$packet.packetId; scanId = [string]$sources[0].scanId; commitSha = [string]$sources[0].commitSha }
            subject = [ordered]@{ surfaceId = [string]$surface.surfaceId; surfaceKind = [string]$surface.surfaceKind; projectId = [string]$surface.projectId; filePath = [string]$surface.evidence.filePath }
            counts = [ordered]@{ controls = @(Values $surface.controlIds).Count; eventChains = $chains.Count; boundaries = $boundaries.Count; identityState = $identity.Count; batchDataMovement = $batch.Count; structuralCandidates = $candidates.Count; gaps = $gaps.Count }
            supportingIds = @(
                @($evidence | ForEach-Object { [string]$_.factId; Values $_.supportingFactIds; Values $_.supportingEdgeIds })
                @($pathEvidence | ForEach-Object { [string]$_.evidenceId; Values $_.supportingFactIds })
            ) | Where-Object { $_ } | Sort-Object -Unique
            retrievalHints = @(
                [ordered]@{ recipeId = 'webforms-surface-facts'; parameters = [ordered]@{ surface_id = [string]$surface.surfaceId; limit = 500 } },
                @($handlers | ForEach-Object { [ordered]@{ recipeId = 'calls-from-handler'; parameters = [ordered]@{ handler_symbol = $_; limit = 500 } } }),
                @($boundaries | ForEach-Object { [ordered]@{ recipeId = 'boundary-supporting-facts'; parameters = [ordered]@{ terminal_evidence_id = [string]$_.terminalEvidenceId; limit = 100 } } })
            )
            evidenceDocs = if ($EvidenceDocsRoot) { [ordered]@{ status = 'supplied-read-only'; locator = [IO.Path]::GetRelativePath($staging, [IO.Path]::GetFullPath($EvidenceDocsRoot)).Replace('\', '/'); chunks = 'chunks.jsonl'; manifest = 'manifest.json'; queryRecipes = 'query-recipes.json' } } else { [ordered]@{ status = 'not-supplied' } }
            humanReview = [ordered]@{ status = if ($ReviewPath) { 'validated-overlay' } else { 'not-supplied' }; verdict = [string]$decision.verdict; migrationDisposition = [string]$decision.migrationDisposition; capabilityLabel = $decision.capabilityLabel; comment = $decision.comment }
            limitations = @('Static evidence does not prove runtime execution, branch feasibility, successful binding, business intent, or migration correctness.', 'Human review must remain an overlay and must not rewrite this handoff or its source packet.')
        }
        $handoffPath = Join-Path $staging "$pageId.handoff.json"
        [IO.File]::WriteAllText($handoffPath, (($handoff | ConvertTo-Json -Depth 30) + "`n"), [Text.UTF8Encoding]::new($false))

        $chainRows = foreach ($chain in $chains) {
            '<tr><td><code>{0}</code></td><td>{1}</td><td><code>{2}</code></td><td><code>{3}</code></td><td>{4}</td></tr>' -f (ConvertTo-HtmlText $chain.chainId), (ConvertTo-HtmlText $chain.eventSourceId), (ConvertTo-HtmlText $(if ($chain.handlerSymbol) { $chain.handlerSymbol } else { $chain.handlerId })), (ConvertTo-HtmlText $chain.classification), (ConvertTo-HtmlText $(if ($chain.terminalKind) { $chain.terminalKind } else { $chain.traversalObservation.stopState }))
        }
        $boundaryRows = foreach ($boundary in $boundaries) { '<tr><td><code>{0}</code></td><td>{1}</td><td>{2}</td><td><code>{3}</code></td></tr>' -f (ConvertTo-HtmlText $boundary.boundaryId), (ConvertTo-HtmlText $boundary.boundaryCategory), (ConvertTo-HtmlText $boundary.boundaryKind), (ConvertTo-HtmlText $boundary.boundaryTargetId) }
        $gapRows = foreach ($gap in $gaps) { '<li><code>{0}</code> <code>{1}</code> — {2}</li>' -f (ConvertTo-HtmlText $gap.gapId), (ConvertTo-HtmlText $gap.ruleId), (ConvertTo-HtmlText $gap.classification) }
        $sourceHtml = if ($IncludeRawSource) { Source-Excerpt $surface.evidence $SourceRoot $SourceContextLines } else { '<p class="muted">Raw source omitted. Regenerate with <code>-IncludeRawSource -SourceRoot &lt;authorized-root&gt;</code> for a bounded private excerpt.</p>' }
        $html = @"
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>$(ConvertTo-HtmlText $pageId) Web Forms review</title><style>:root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}main{max-width:1200px;margin:auto;padding:24px}.private,.warning{padding:12px;border-left:5px solid #c62828;background:#fff1f0}.summary{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:10px}.card,details{background:white;border:1px solid #dbe2ee;border-radius:8px;padding:14px;margin:14px 0}.summary .card{margin:0}table{width:100%;border-collapse:collapse}th,td{padding:9px;border:1px solid #dbe2ee;text-align:left;vertical-align:top}th{background:#eaf1ff}code{background:#edf1f7;padding:.1rem .3rem;border-radius:4px;overflow-wrap:anywhere}pre{overflow:auto;background:#172033;color:#f8fafc;padding:14px;border-radius:6px}.muted{color:#566070}.button{display:inline-block;padding:7px 10px;background:#eaf1ff;border:1px solid #bed0ee;border-radius:6px;text-decoration:none}</style></head><body><main>
<p><a class="button" href="index.html">Return to application index</a></p><h1>$(ConvertTo-HtmlText $surface.evidence.filePath)</h1><p class="private">PRIVATE local evidence review. Human conclusions are review metadata, not scanner facts.</p>
<section class="summary"><div class="card"><strong>Controls</strong><br>$(@(Values $surface.controlIds).Count)</div><div class="card"><strong>Event chains</strong><br>$($chains.Count)</div><div class="card"><strong>Boundaries</strong><br>$($boundaries.Count)</div><div class="card"><strong>Gaps</strong><br>$($gaps.Count)</div></section>
<section class="card"><h2>Review status</h2><p><strong>Verdict:</strong> $(ConvertTo-HtmlText $decision.verdict) · <strong>Disposition:</strong> $(ConvertTo-HtmlText $decision.migrationDisposition)</p><p><strong>Capability:</strong> $(ConvertTo-HtmlText $decision.capabilityLabel)</p><p>$(ConvertTo-HtmlText $decision.comment)</p><p>Human review is a separate validated overlay, never scanner evidence.</p></section>
<section class="card"><h2>Surface</h2><p><code>$(ConvertTo-HtmlText $surface.surfaceId)</code> · $(ConvertTo-HtmlText $surface.surfaceKind) · project <code>$(ConvertTo-HtmlText $surface.projectId)</code></p><p><strong>Coverage:</strong> $(ConvertTo-HtmlText ($coverage -join ', '))</p><p><strong>Controls:</strong> $(ConvertTo-HtmlText ((Values $surface.controlIds) -join ', '))</p></section>
<section class="card"><h2>Trigger and retained call paths</h2><table><thead><tr><th>Chain</th><th>Event source</th><th>Handler</th><th>Classification</th><th>Terminal/stop</th></tr></thead><tbody>$($chainRows -join '')</tbody></table></section>
<details><summary><strong>Downstream boundaries ($($boundaries.Count))</strong></summary><table><thead><tr><th>ID</th><th>Category</th><th>Kind</th><th>Target</th></tr></thead><tbody>$($boundaryRows -join '')</tbody></table></details>
<details><summary><strong>Identity/state, data movement, and structural candidates</strong></summary><p>Identity/state: <code>$($identity.Count)</code>. Project data-movement candidates: <code>$($batch.Count)</code>. Structural candidates: <code>$($candidates.Count)</code>.</p></details>
<details><summary><strong>Explicit gaps ($($gaps.Count))</strong></summary><ul>$($gapRows -join '')</ul></details>
<details><summary><strong>Evidence citations ($($evidence.Count))</strong></summary>$(Evidence-List $evidence)</details>
<details><summary><strong>Retained call-path locations ($($pathEvidence.Count))</strong></summary>$(Path-Evidence-List $pathEvidence)</details>
<details><summary><strong>Working-tree source excerpt</strong></summary>$sourceHtml</details>
<details><summary><strong>Agent evidence handoff</strong></summary><p><a href="$pageId.handoff.json">Open $pageId.handoff.json</a>. It provides stable evidence IDs and closed TraceMap recipe hints; it is not a BRD.</p></details>
</main></body></html>
"@
        [IO.File]::WriteAllText((Join-Path $staging "$pageId.html"), $html, [Text.UTF8Encoding]::new($false))
        $pageRows.Add([pscustomobject]@{ PageId = $pageId; Path = [string]$surface.evidence.filePath; SurfaceKind = [string]$surface.surfaceKind; Chains = $chains.Count; Boundaries = $boundaries.Count; Gaps = $gaps.Count; Coverage = ($coverage -join ', '); Verdict = [string]$decision.verdict; Disposition = [string]$decision.migrationDisposition })
        $applicationPages.Add([ordered]@{ pageId = $pageId; surfaceId = [string]$surface.surfaceId; filePath = [string]$surface.evidence.filePath; report = "$pageId.html"; handoff = "$pageId.handoff.json"; counts = $handoff.counts })
    }

    $appHandoff = [ordered]@{
        schemaVersion = 'webforms-application-handoff.v1'; ruleId = 'diagnostic.webforms.application-handoff.v1'; claimLevel = 'local-only'
        packet = [ordered]@{ packetId = [string]$packet.packetId; scanId = [string]$sources[0].scanId; commitSha = [string]$sources[0].commitSha; snapshot = 'webforms-modernization.snapshot.json' }
        pageCount = $applicationPages.Count; pages = @($applicationPages)
        evidenceDocs = if ($EvidenceDocsRoot) { [ordered]@{ status = 'supplied-read-only'; locator = [IO.Path]::GetRelativePath($staging, [IO.Path]::GetFullPath($EvidenceDocsRoot)).Replace('\', '/'); chunks = 'chunks.jsonl'; manifest = 'manifest.json'; queryRecipes = 'query-recipes.json' } } else { [ordered]@{ status = 'not-supplied' } }
        humanReview = if ($ReviewPath) { [ordered]@{ status = 'validated-overlay'; schemaVersion = [string]$review.schemaVersion; overlayId = [string]$review.overlayId; reviewState = [string]$review.reviewState } } else { [ordered]@{ status = 'not-supplied' } }
        limitations = @('This is deterministic navigation metadata over one packet, not business intent, a BRD, or a modernization decision.', 'The evidence-docs corpus is an external read-only input and is never rewritten by this generator.')
    }
    [IO.File]::WriteAllText((Join-Path $staging 'application-handoff.json'), (($appHandoff | ConvertTo-Json -Depth 30) + "`n"), [Text.UTF8Encoding]::new($false))
    $tableRows = foreach ($row in $pageRows) { '<tr><td><a target="_blank" rel="noopener" href="{0}.html">{0}</a></td><td><code>{1}</code></td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td><a href="{0}.handoff.json">JSON</a></td></tr>' -f $row.PageId, (ConvertTo-HtmlText $row.Path), (ConvertTo-HtmlText $row.SurfaceKind), $row.Chains, $row.Boundaries, $row.Gaps, (ConvertTo-HtmlText $row.Verdict), (ConvertTo-HtmlText $row.Disposition) }
    $index = @"
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Web Forms application workbench</title><style>:root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}main{max-width:1400px;margin:auto;padding:24px}.private{padding:12px;border-left:5px solid #c62828;background:#fff1f0}table{width:100%;border-collapse:collapse;background:white}th,td{padding:10px;border:1px solid #dbe2ee;text-align:left;vertical-align:top}th{background:#eaf1ff}code{background:#edf1f7;padding:.1rem .3rem;border-radius:4px;overflow-wrap:anywhere}a{color:#1558b0}</style></head><body><main><h1>Private Web Forms application workbench</h1><p class="private">PRIVATE: $(ConvertTo-HtmlText $applicationPages.Count) selected surfaces from one retained packet. No source scan was run.</p><p>Packet <code>$(ConvertTo-HtmlText $packet.packetId)</code>. Coverage <code>$(ConvertTo-HtmlText $packet.coverage)</code>. <a href="application-handoff.json">Application handoff JSON</a> · <a href="webforms-modernization.snapshot.json">Packet snapshot</a>.</p><table><thead><tr><th>Page</th><th>Retained file</th><th>Kind</th><th>Chains</th><th>Boundaries</th><th>Gaps</th><th>Verdict</th><th>Disposition</th><th>Handoff</th></tr></thead><tbody>$($tableRows -join '')</tbody></table><p>Static evidence does not prove runtime execution, business intent, or migration correctness. Human review remains a separate overlay.</p></main></body></html>
"@
    [IO.File]::WriteAllText((Join-Path $staging 'index.html'), $index, [Text.UTF8Encoding]::new($false))
    [IO.Directory]::Move($staging, $OutputDirectory)
}
catch {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    throw
}

Write-Host "applicationWorkbench=completed;pages=$($surfaces.Count);rawSource=$($IncludeRawSource.ToString().ToLowerInvariant())"
Write-Host "applicationWorkbenchIndex=$(Join-Path $OutputDirectory 'index.html')"
Write-Host 'chunksCorpus=preserved-read-only'
if ($IsWindows) { Start-Process -FilePath (Join-Path $OutputDirectory 'index.html') }
