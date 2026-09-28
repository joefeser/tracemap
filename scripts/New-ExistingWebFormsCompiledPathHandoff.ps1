[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProofRoot,
    [string]$OutputDirectory,
    [string]$PathReportPath,
    [string]$PathReportReceiptPath,
    [ValidateSet('sql-query', 'database-api')][string]$ToSurface = 'sql-query',
    [long]$IlMaxWork = 0
)

# Private projection of an already-generated, bounded compiled path report.
# The scan facts/index and paths report remain the evidence sources.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_COMPILED_HANDOFF_POWERSHELL_7_REQUIRED' }
if ($IlMaxWork -ne 0 -and ($IlMaxWork -lt 2000001 -or $IlMaxWork -gt 100000000)) {
    throw 'WEBFORMS_COMPILED_HANDOFF_IL_WORK_LIMIT_INVALID'
}

function Hash-Input([string]$Path, [long]$Limit, [string]$Slot) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "WEBFORMS_COMPILED_HANDOFF_INPUT_UNAVAILABLE;slot=$Slot" }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $Limit) {
        throw "WEBFORMS_COMPILED_HANDOFF_INPUT_LIMIT;slot=$Slot;bytes=$($file.Length);max=$Limit"
    }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Html([object]$Value) { return [Net.WebUtility]::HtmlEncode([string]$Value) }
function Values([object]$Value) { if ($null -eq $Value) { return @() }; return @($Value) }
function Optional([object]$Value, [string]$Name) {
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }; return $property.Value
}
function Display-Label([string]$Identity) {
    # A display-only projection. Never use this label for matching or evidence identity.
    $member = [regex]::Match($Identity, '\|(?:method|constructor):[0-9]+:([^|]+)')
    if ($member.Success) {
        $prefix = $Identity.Substring(0, $member.Index)
        $types = [regex]::Matches($prefix, '\|names:[0-9]+:([^|]+)')
        $namespaces = [regex]::Matches($prefix, 'namespace:[0-9]+:([^|]*)')
        $typeName = if ($types.Count -gt 0) { $types[-1].Groups[1].Value } else { 'unknown type' }
        $namespace = if ($namespaces.Count -gt 0) { $namespaces[-1].Groups[1].Value } else { '' }
        $memberName = $member.Groups[1].Value
        if ($memberName -eq '.ctor') { $memberName = 'New' }
        $qualifiedType = if ($namespace) { "$namespace.$typeName" } else { $typeName }
        return "$qualifiedType.$memberName()"
    }
    if ($Identity.Length -gt 160) { return $Identity.Substring(0, 157) + '…' }
    return $Identity
}

$ProofRoot = [IO.Path]::GetFullPath($ProofRoot).TrimEnd('\', '/')
$pathsPath = if ($PathReportPath) { [IO.Path]::GetFullPath($PathReportPath) } else { Join-Path $ProofRoot 'handler-paths.json' }
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (!$pathsPath.StartsWith($ProofRoot + [IO.Path]::DirectorySeparatorChar, $comparison)) {
    throw 'WEBFORMS_COMPILED_HANDOFF_PATH_REPORT_OUTSIDE_PROOF'
}
$scanFolder = if ($IlMaxWork -eq 0) { 'scan' } else { 'scan-ilwork-' + $IlMaxWork }
$combinedName = if ($IlMaxWork -eq 0) { 'combined.sqlite' } else { 'combined-ilwork-' + $IlMaxWork + '.sqlite' }
$manifestPath = Join-Path $ProofRoot (Join-Path $scanFolder 'scan-manifest.json')
$receiptPath = Join-Path $ProofRoot 'publish-receipt.local.json'
$indexPath = Join-Path $ProofRoot $combinedName
$inputHashes = [ordered]@{
    paths = Hash-Input $pathsPath 268435456 'paths'
    manifest = Hash-Input $manifestPath 4194304 'manifest'
    publishReceipt = Hash-Input $receiptPath 4194304 'publishReceipt'
    combinedIndex = Hash-Input $indexPath 4294967296 'combinedIndex'
}
if ($ToSurface -eq 'database-api') {
    if (!$PathReportReceiptPath) { throw 'WEBFORMS_COMPILED_HANDOFF_API_RECEIPT_REQUIRED' }
    $PathReportReceiptPath = [IO.Path]::GetFullPath($PathReportReceiptPath)
    if (!$PathReportReceiptPath.StartsWith($ProofRoot + [IO.Path]::DirectorySeparatorChar, $comparison)) {
        throw 'WEBFORMS_COMPILED_HANDOFF_API_RECEIPT_OUTSIDE_PROOF'
    }
    $inputHashes.pathReportReceipt = Hash-Input $PathReportReceiptPath 1048576 'pathReportReceipt'
    $pathReportReceipt = [IO.File]::ReadAllText($PathReportReceiptPath) | ConvertFrom-Json -Depth 10
    if ($pathReportReceipt.schemaVersion -cne 'webforms-path-recheck.v1' -or
        [string]$pathReportReceipt.generatorSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        [string]$pathReportReceipt.boundedInputSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $pathReportReceipt.scanFolder -cne $scanFolder -or
        $pathReportReceipt.combinedIndex -cne $combinedName -or
        [string](Optional $pathReportReceipt 'pathReportSha256') -cne $inputHashes.paths) {
        throw 'WEBFORMS_COMPILED_HANDOFF_API_RECEIPT_INVALID'
    }
    $baseReportHash = Hash-Input (Join-Path $ProofRoot 'handler-paths.json') 268435456 'basePathReport'
    $factsHash = Hash-Input (Join-Path $ProofRoot "$scanFolder/facts.ndjson") 17179869184 'facts'
    $generationDigest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes("$($inputHashes.combinedIndex)`n$baseReportHash`n$factsHash"))).ToLowerInvariant()
    if ($generationDigest -cne $pathReportReceipt.boundedInputSha256) {
        throw 'WEBFORMS_COMPILED_HANDOFF_API_RECEIPT_INVALID'
    }
}
$paths = [IO.File]::ReadAllText($pathsPath) | ConvertFrom-Json -Depth 60
$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 40
$receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 30
if ([string]::IsNullOrWhiteSpace([string](Optional $manifest 'repoName')) -or
    @($paths.sources | Where-Object { $_.scanId -ceq $manifest.scanId -and
        $_.commitSha -ceq $manifest.commitSha -and
        [string](Optional $_ 'repoName') -ceq $manifest.repoName -and
        [string](Optional $_ 'remoteUrl') -ceq [string](Optional $manifest 'remoteUrl') }).Count -ne 1) {
    throw 'WEBFORMS_COMPILED_HANDOFF_REPOSITORY_MISMATCH'
}
$repositorySha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes([string]$manifest.repoName))).ToLowerInvariant()
$packetRepositoryId = 'repository-' + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes("webforms-modernization/repository/v1`0$($manifest.repoName)"))).ToLowerInvariant().Substring(0,24)
if ([string]$paths.version -ne '1.0' -or [string]$paths.query.toSurface -ne $ToSurface -or
    [string]$manifest.commitSha -cnotmatch '^[0-9a-f]{40}$' -or
    [string]$receipt.sourceCommitSha -cne [string]$manifest.commitSha -or
    [int]$paths.query.maxDepth -gt 20 -or [int]$paths.query.maxPaths -gt 256 -or
    @($paths.sources | Where-Object { $_.scanId -ceq $manifest.scanId -and $_.commitSha -ceq $manifest.commitSha }).Count -ne 1 -or
    [int]$paths.summary.pathCount -ne @(Values $paths.paths).Count -or
    [int]$paths.summary.gapCount -ne @(Values $paths.gaps).Count -or
    @(Values $paths.paths).Count -gt 256 -or
    @(Values $paths.gaps).Count -gt 250000) {
    throw 'WEBFORMS_COMPILED_HANDOFF_INPUT_INVALID'
}
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$retained = [Collections.Generic.List[object]]::new()
foreach ($path in @(Values $paths.paths)) {
    $nodes = @(Values $path.nodes)
    $edges = @(Values $path.edges)
    if (!$seen.Add([string]$path.pathId) -or $edges.Count -gt 20 -or
        $nodes.Count -ne $edges.Count + 1) { throw 'WEBFORMS_COMPILED_HANDOFF_PATH_INVALID' }
    $hops = [Collections.Generic.List[object]]::new()
    for ($i = 0; $i -lt $edges.Count; $i++) {
        $edge = $edges[$i]
        if ([string]$edge.fromNodeId -cne [string]$nodes[$i].nodeId -or
            [string]$edge.toNodeId -cne [string]$nodes[$i + 1].nodeId -or
            [string]::IsNullOrWhiteSpace([string]$edge.ruleId) -or
            [string]$edge.evidenceTier -cnotin @('Tier1Semantic','Tier2Structural','Tier3SyntaxOrTextual','Tier4Unknown')) {
            throw 'WEBFORMS_COMPILED_HANDOFF_PATH_INVALID'
        }
        $hops.Add([ordered]@{
            ordinal = $i + 1
            from = [ordered]@{ name = [string]$nodes[$i].displayName; displayLabel = Display-Label ([string]$nodes[$i].displayName); scanId = [string]$nodes[$i].scanId; commitSha = [string]$nodes[$i].commitSha }
            to = [ordered]@{ name = [string]$nodes[$i + 1].displayName; displayLabel = Display-Label ([string]$nodes[$i + 1].displayName); scanId = [string]$nodes[$i + 1].scanId; commitSha = [string]$nodes[$i + 1].commitSha }
            edgeKind = [string]$edge.edgeKind
            ruleId = [string]$edge.ruleId
            evidenceTier = [string]$edge.evidenceTier
            filePath = $edge.filePath
            startLine = $edge.startLine
            endLine = $edge.endLine
            supportingFactIds = @(Values $edge.supportingFactIds)
        })
    }
    $retained.Add([ordered]@{
        pathId = [string]$path.pathId
        classification = [string]$path.classification
        claim = 'review-only-static-path'
        terminalKind = [string]$nodes[-1].surfaceKind
        hops = @($hops)
        supportingFactIds = @(Values $path.supportingFactIds)
        notes = @(Values $path.notes | ForEach-Object { [ordered]@{ code = [string]$_.code; message = [string]$_.message } })
    })
}
$digestLines = @($inputHashes.GetEnumerator() | ForEach-Object { "$($_.Key):$($_.Value)" })
$inputDigest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes(($digestLines -join "`n") + "`n"))).ToLowerInvariant()
$generatorHash = Hash-Input $PSCommandPath 1048576 'generator'
$allGaps = @(Values $paths.gaps)
$retainedGaps = @($allGaps | Select-Object -First 256)
$gapGroups = [Collections.Generic.SortedDictionary[string, int]]::new([StringComparer]::Ordinal)
foreach ($gap in $allGaps) {
    if ([string]::IsNullOrWhiteSpace([string]$gap.ruleId) -or
        [string]$gap.evidenceTier -cnotin @('Tier1Semantic','Tier2Structural','Tier3SyntaxOrTextual','Tier4Unknown')) {
        throw 'WEBFORMS_COMPILED_HANDOFF_GAP_INVALID'
    }
    $key = "$( [string]$gap.gapKind )`t$( [string]$gap.ruleId )"
    if (!$gapGroups.ContainsKey($key)) { $gapGroups[$key] = 0 }
    $gapGroups[$key]++
}
$gapCounts = @($gapGroups.GetEnumerator() | Select-Object -First 128 | ForEach-Object {
    $parts = $_.Key.Split([char]9, 2)
    [ordered]@{ gapKind = $parts[0]; ruleId = $parts[1]; count = $_.Value }
})
$handoff = [ordered]@{
    schemaVersion = 'webforms-compiled-path-handoff.v1'
    ruleId = 'diagnostic.webforms.compiled-path-handoff.v1'
    visibility = 'local-only'
    claimLevel = 'review-only-static-evidence'
    provenance = [ordered]@{ generatorSha256 = $generatorHash; boundedInputSha256 = $inputDigest; inputSha256 = $inputHashes; sourceCommitSha = [string]$manifest.commitSha; scanId = [string]$manifest.scanId; scanFolder = $scanFolder; combinedIndex = $combinedName; pathReportGeneration = if ($ToSurface -eq 'database-api') { [ordered]@{ generatorSha256 = [string]$pathReportReceipt.generatorSha256; boundedInputSha256 = [string]$pathReportReceipt.boundedInputSha256 } } else { $null } }
    query = [ordered]@{ fromSymbol = [string]$paths.query.fromSymbol; toSurface = $ToSurface; maxDepth = [int]$paths.query.maxDepth; maxPaths = [int]$paths.query.maxPaths }
    coverage = [ordered]@{
        reportCoverage = [string]$paths.reportCoverage
        warnings = @(Values $paths.coverageWarnings)
        truncated = [bool]$paths.summary.truncated
        gapCount = $allGaps.Count
        gapCounts = $gapCounts
        omittedGapGroupCount = [Math]::Max(0, $gapGroups.Count - $gapCounts.Count)
        omittedGapDetailCount = $allGaps.Count - $retainedGaps.Count
        gaps = @($retainedGaps | ForEach-Object {
            $projection = [ordered]@{}
            foreach ($name in @('gapId','gapKind','classification','ruleId','evidenceTier','message','reason','commitSha',
                'sourceIndexId','sourceLabel','nodeId','combinedFactId','filePath','startLine','endLine','extractorVersion','evidenceScope','supportingFactIds',
                'candidateCount','candidateLimit','groupedEvidenceCount','supportingFactLimit','callEvidenceCount','relationshipEvidenceCount','registrationEvidenceCount')) {
                $projection[$name] = Optional $_ $name
            }
            $projection
        })
    }
    assemblies = @(Values $manifest.compiledInputProvenance.outcomes | Where-Object { $_.outcome -eq 'admitted' } | ForEach-Object {
        [ordered]@{ safeLocator = [string]$_.safeLocator; rawFileSha256 = [string]$_.rawFileSha256; assemblyIdentity = [string]$_.assemblyIdentity; provenanceState = [string]$_.provenanceState; gapKinds = @(Values $_.gapKinds) }
    })
    paths = @($retained)
    limitations = @(
        'The ordered paths are derived from retained scanner facts and a bounded graph query; they are not additional extracted facts.',
        'A mapless source-to-compiled join and source member joins are Tier3 review-only candidates, not source-line or exact runtime identity.',
        'Static calls, SQL text evidence, and a database API candidate do not prove event firing, dispatch, database execution, or branch feasibility.'
    )
}
$handoff.provenance.sourceRepositorySha256 = $repositorySha
$handoff.provenance.packetRepositoryId = $packetRepositoryId
$OutputDirectory = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else {
    Join-Path $ProofRoot $(if ($ToSurface -eq 'database-api') {
        if ($IlMaxWork -ne 0) { "compiled-api-review-ilwork-$IlMaxWork" } else { 'compiled-api-review' }
    } else { 'compiled-path-review' })
}
if (Test-Path -LiteralPath $OutputDirectory) { throw 'WEBFORMS_COMPILED_HANDOFF_OUTPUT_NOT_FRESH' }
$outputParent = Split-Path -Parent $OutputDirectory
if (!(Test-Path -LiteralPath $outputParent -PathType Container)) { throw 'WEBFORMS_COMPILED_HANDOFF_OUTPUT_PARENT_UNAVAILABLE' }
$proofPrefix = $ProofRoot + [IO.Path]::DirectorySeparatorChar
if (!$OutputDirectory.StartsWith($proofPrefix, $(if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }))) {
    throw 'WEBFORMS_COMPILED_HANDOFF_OUTPUT_OUTSIDE_PROOF'
}
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$jsonPath = Join-Path $OutputDirectory 'handler.handoff.local.json'
$htmlPath = Join-Path $OutputDirectory 'handler.local.html'
[IO.File]::WriteAllText($jsonPath, (($handoff | ConvertTo-Json -Depth 30) + "`n"), [Text.UTF8Encoding]::new($false))
$pathNumber = 0
$sections = foreach ($path in $retained) {
    $pathNumber++
    $hopRows = foreach ($hop in $path.hops) {
        '<tr><th scope="row">{0}</th><td><strong>{1}</strong><span class="arrow">→</span><strong>{2}</strong><details class="identity"><summary>Exact identities</summary><p>From: <code>{3}</code></p><p>To: <code>{4}</code></p></details></td><td><code>{5}</code><br><small>{6}; {7}</small></td><td><small>{8}:{9}-{10}</small><details class="identity"><summary>Supporting facts</summary><code>{11}</code></details></td></tr>' -f `
            $hop.ordinal, (Html $hop.from.displayLabel), (Html $hop.to.displayLabel),
            (Html $hop.from.name), (Html $hop.to.name), (Html $hop.edgeKind),
            (Html $hop.ruleId), (Html $hop.evidenceTier), (Html $hop.filePath),
            (Html $hop.startLine), (Html $hop.endLine), (Html (($hop.supportingFactIds) -join ', '))
    }
    '<section><h2>Path {0:D2}: {1} → {2}</h2><p><small>Path ID <code>{3}</code></small><br>Classification: <code>{4}</code>; claim: <strong>{5}</strong>; terminal: <code>{6}</code></p><div class="table-wrap"><table><thead><tr><th>#</th><th>Method transition</th><th>Edge / rule / tier</th><th>Location / evidence</th></tr></thead><tbody>{7}</tbody></table></div></section>' -f `
        $pathNumber, (Html $path.hops[0].from.displayLabel), (Html $path.hops[-1].to.displayLabel),
        (Html $path.pathId), (Html $path.classification), (Html $path.claim),
        (Html $path.terminalKind), ($hopRows -join '')
}
$assemblyRows = foreach ($assembly in $handoff.assemblies) {
    '<li><code>{0}</code> — SHA-256 <code>{1}</code>; provenance <code>{2}</code></li>' -f `
        (Html $assembly.safeLocator), (Html $assembly.rawFileSha256), (Html $assembly.provenanceState)
}
$gapRows = foreach ($gap in $handoff.coverage.gaps) {
    '<li><code>{0}</code> — <code>{1}</code> ({2}); {3}; commit <code>{4}</code>; location <code>{5}:{6}-{7}</code>; source <code>{8}</code>; fact <code>{9}</code></li>' -f `
        (Html $gap.gapKind), (Html $gap.ruleId), (Html $gap.evidenceTier),
        (Html $gap.message), (Html $gap.commitSha), (Html $gap.filePath), (Html $gap.startLine),
        (Html $gap.endLine), (Html $gap.sourceIndexId), (Html $gap.combinedFactId)
}
$html = @"
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Web Forms compiled path review</title><style>body{font:16px system-ui,sans-serif;max-width:1450px;margin:2rem auto;padding:0 1rem;color:#172033}section{margin:2rem 0;padding:1rem;border:1px solid #ccd5e0;border-radius:8px}.table-wrap{overflow-x:auto}table{border-collapse:collapse;width:100%;table-layout:fixed;margin:1rem 0}th,td{border:1px solid #ccd5e0;padding:.65rem;text-align:left;vertical-align:top;overflow-wrap:anywhere}th{background:#eaf1ff}th:first-child{width:3rem}th:nth-child(2){width:44%}th:nth-child(3){width:23%}code{overflow-wrap:anywhere}td strong{display:block}.arrow{display:block;color:#526078}.identity{font-size:.85rem;margin-top:.4rem}.identity p{margin:.35rem 0}.identity code{word-break:break-all}p.warning{background:#fff1df;border-left:4px solid #a85a00;padding:1rem}small{color:#526078}</style></head><body><h1>Web Forms compiled path review</h1><p class="warning">LOCAL ONLY. These are bounded static, review-only candidates—not runtime execution, page activation, source-line identity, or proof that SQL ran.</p><p>Terminal <code>$(Html $ToSurface)</code>; source commit <code>$(Html $manifest.commitSha)</code>; scan <code>$(Html $manifest.scanId)</code>; paths $($retained.Count); gaps $($allGaps.Count); coverage <code>$(Html $paths.reportCoverage)</code>; truncated <code>$(Html $paths.summary.truncated)</code>. <a href="handler.handoff.local.json">Machine-readable handoff</a>.</p><p>Compact method labels are for reading only. Expand “Exact identities” for the preserved compiler/IL identity; rule IDs and evidence tiers remain on each hop.</p>$($sections -join '')<details><summary>Admitted DLL provenance ($($handoff.assemblies.Count))</summary><ul>$($assemblyRows -join '')</ul></details><details><summary>Explicit graph gaps ($($allGaps.Count))</summary><p>Showing $($retainedGaps.Count) of $($allGaps.Count) gap details; $($handoff.coverage.omittedGapDetailCount) omitted from this display. Exact counts by kind and rule remain in the JSON; the complete bounded report is committed by its input SHA-256.</p><ul>$($gapRows -join '')</ul></details><p>Generator SHA-256 <code>$generatorHash</code>; bounded input SHA-256 <code>$inputDigest</code>.</p></body></html>
"@
[IO.File]::WriteAllText($htmlPath, $html, [Text.UTF8Encoding]::new($false))
Write-Output "compiledPathReviewStatus=valid"
Write-Output "compiledPathReviewPaths=$($retained.Count)"
Write-Output "compiledPathReviewGaps=$(@(Values $paths.gaps).Count)"
Write-Output "compiledPathReviewReviewOnly=$(@($retained | Where-Object { $_.claim -eq 'review-only-static-path' }).Count)"
Write-Output "compiledPathReviewHtml=$htmlPath"
Write-Output "compiledPathReviewHandoff=$jsonPath"
