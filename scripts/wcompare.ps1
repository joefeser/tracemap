[CmdletBinding()]
param([string]$Historical, [string]$Current, [string]$Mixed, [string]$OutputPath, [switch]$Open)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!$Historical) {
    $candidates = @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'tracemap-existing-publish-*' |
        ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -File -Filter 'handler-database-api*.json' } |
        Sort-Object FullName | Select-Object -First 100)
    for ($i = 0; $i -lt $candidates.Count; $i++) { Write-Host "[$($i + 1)] $($candidates[$i].Directory.Name)/$($candidates[$i].Name)" }
    $choice = Read-Host 'Historical handler JSON: number above or full file path'
    $number = 0
    if ([int]::TryParse($choice, [ref]$number)) {
        if ($number -lt 1 -or $number -gt $candidates.Count) { throw 'WEBFORMS_COMPARE_SELECTION_INVALID' }
        $Historical = $candidates[$number - 1].FullName
    } else { $Historical = $choice }
}
if (!$Current) { $Current = Read-Host 'Current handler report folder or JSON file (full path)' }
if (Test-Path -LiteralPath $Current -PathType Container) { $Current = Join-Path $Current 'compiled-paths.handoff.local.json' }
if (!$Mixed) {
    $candidate = Join-Path (Split-Path (Split-Path $Current -Parent) -Parent) 'handler-requery-fill/compiled-paths.handoff.local.json'
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { $Mixed = $candidate }
}
if ($Mixed -and (Test-Path -LiteralPath $Mixed -PathType Container)) { $Mixed = Join-Path $Mixed 'compiled-paths.handoff.local.json' }
if (!$OutputPath) {
    $parent = Split-Path $Current -Parent
    $OutputPath = Join-Path $parent 'chain-comparison.local.html'
    for ($number = 2; (Test-Path -LiteralPath $OutputPath) -and $number -le 1000; $number++) {
        $OutputPath = Join-Path $parent "chain-comparison-$number.local.html"
    }
}
if (Test-Path -LiteralPath $OutputPath) { throw 'WEBFORMS_COMPARE_OUTPUT_EXISTS' }
function Value($object, [string]$name) {
    if ($object -is [Collections.IDictionary] -and $object.Contains($name)) { return ,($object[$name]) }
    return $null
}
function HashText([string]$text) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()
}
function Json($value) { return ConvertTo-Json -InputObject $value -Depth 100 -Compress }
function ReadReport([string]$path) {
    $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($stream.Length -gt 256MB) { throw 'WEBFORMS_COMPARE_INPUT_LIMIT' }
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
        $stream.Position = 0
        $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true, 16384, $true)
        try { $document = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable -Depth 100 }
        finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
    $paths = [Collections.Generic.List[object]]::new()
    $header = $document
    if ((Value $document 'schemaVersion') -eq 'webforms-compiled-grouped-handoff.v1') {
        $header = Value $document 'header'
        $variants = Value $document 'variants'
        $nodes = Value $document 'nodes'
        if ($null -eq $variants -or $nodes -isnot [Collections.IDictionary] -or $variants.Count -gt 10000) { throw 'WEBFORMS_COMPARE_SHAPE_INVALID' }
        foreach ($variant in $variants) {
            $references = Value $variant 'nodeReferences'
            if ($null -eq $references -or $references.Count -gt 2048) { throw 'WEBFORMS_COMPARE_REFERENCE_LIMIT' }
            $sequence = [Collections.Generic.List[object]]::new()
            foreach ($reference in $references) {
                if (!$nodes.Contains($reference)) { throw 'WEBFORMS_COMPARE_REFERENCE_MISSING' }
                $sequence.Add($nodes[$reference])
            }
            $edgeSequence = [Collections.Generic.List[object]]::new()
            $edgeReferences = Value $variant 'edgeReferences'
            if ($null -ne $edgeReferences) {
                $edgeRecords = Value $document 'edges'
                if ($edgeReferences.Count -gt 2048 -or $edgeRecords -isnot [Collections.IDictionary]) { throw 'WEBFORMS_COMPARE_EDGE_SHAPE_INVALID' }
                foreach ($reference in $edgeReferences) {
                    if (!$edgeRecords.Contains($reference)) { throw 'WEBFORMS_COMPARE_EDGE_REFERENCE_MISSING' }
                    $edgeSequence.Add($edgeRecords[$reference])
                }
            }
            $paths.Add(@{ nodes = $sequence.ToArray(); edges = $edgeSequence.ToArray() })
        }
    } else {
        $raw = Value $document 'paths'
        if ($null -eq $raw -or $null -eq (Value $document 'query') -or $raw.Count -gt 10000) { throw 'WEBFORMS_COMPARE_SHAPE_INVALID' }
        foreach ($row in $raw) { $paths.Add($row) }
    }
    $exact = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $symbols = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $totalNodes = 0
    $totalEdges = 0
    foreach ($row in $paths) {
        $nodes = Value $row 'nodes'
        if ($null -eq $nodes -or $nodes.Count -eq 0 -or $nodes.Count -gt 2048) { throw 'WEBFORMS_COMPARE_NODE_LIMIT' }
        $totalNodes += $nodes.Count
        if ($totalNodes -gt 500000) { throw 'WEBFORMS_COMPARE_NODE_LIMIT' }
        $identity = [Collections.Generic.List[object]]::new()
        $symbolSequence = [Collections.Generic.List[object]]::new()
        $labels = [Collections.Generic.List[string]]::new()
        foreach ($node in $nodes) {
            $symbol = Value $node 'symbolId'
            $display = Value $node 'displayName'
            $id = if ($null -ne $symbol) { $symbol } else { Value $node 'nodeId' }
            if ($node -isnot [Collections.IDictionary] -or [string]::IsNullOrWhiteSpace([string](Value $node 'nodeKind')) -or [string]::IsNullOrWhiteSpace([string]$id)) {
                throw 'WEBFORMS_COMPARE_NODE_IDENTITY_INVALID'
            }
            $identity.Add(@((Value $node 'nodeKind'), (Value $node 'sourceIndexId'), (Value $node 'scanId'), (Value $node 'commitSha'), $id, $display))
            $label = if ($null -ne $symbol) { [string]$symbol } else { [string]$display }
            $symbolSequence.Add(@((Value $node 'nodeKind'), $label))
            $labels.Add($label)
        }
        $key = HashText (Json $identity.ToArray())
        $symbolKey = HashText (Json $symbolSequence.ToArray())
        if (!$symbols.ContainsKey($symbolKey)) { $symbols[$symbolKey] = @{ count = 0; labels = $labels.ToArray(); evidence = [Collections.Generic.List[object]]::new() } }
        $edges = Value $row 'edges'
        $projection = [Collections.Generic.List[object]]::new()
        if ($null -ne $edges) {
            if ($edges.Count -gt 2048) { throw 'WEBFORMS_COMPARE_EDGE_LIMIT' }
            $totalEdges += $edges.Count
            if ($totalEdges -gt 500000) { throw 'WEBFORMS_COMPARE_EDGE_LIMIT' }
            foreach ($edge in $edges) {
                if ($edge -isnot [Collections.IDictionary] -or [string]::IsNullOrWhiteSpace([string](Value $edge 'edgeKind'))) { throw 'WEBFORMS_COMPARE_EDGE_SHAPE_INVALID' }
                $projection.Add(@{ kind = (Value $edge 'edgeKind'); rule = (Value $edge 'ruleId'); tier = (Value $edge 'evidenceTier'); from = (Value $edge 'fromNodeId'); to = (Value $edge 'toNodeId') })
            }
        }
        $symbols[$symbolKey].evidence.Add($projection.ToArray())
        $symbols[$symbolKey].count++
        if (!$exact.ContainsKey($key)) { $exact[$key] = @{ count = 0; labels = $labels.ToArray() } }
        $exact[$key].count++
    }
    return @{ hash = $hash; header = $header; indexHash = (Value $document 'inputIndexSha256'); exact = $exact; symbols = $symbols; variants = $paths.Count }
}
$left = ReadReport $Historical
$right = ReadReport $Current
$mixedReport = if ($Mixed) { ReadReport $Mixed } else { $null }
$leftOnly = @($left.exact.Keys | Where-Object { !$right.exact.ContainsKey($_) } | Sort-Object)
$rightOnly = @($right.exact.Keys | Where-Object { !$left.exact.ContainsKey($_) } | Sort-Object)
$shared = @($left.exact.Keys | Where-Object { $right.exact.ContainsKey($_) })
$variantDifferences = @($shared | Where-Object { $left.exact[$_].count -ne $right.exact[$_].count } | Sort-Object)
$symbolShared = @($left.symbols.Keys | Where-Object { $right.symbols.ContainsKey($_) } | Sort-Object)
$symbolMatches = $symbolShared.Count
$symbolLeftOnly = @($left.symbols.Keys | Where-Object { !$right.symbols.ContainsKey($_) } | Sort-Object)
$symbolRightOnly = @($right.symbols.Keys | Where-Object { !$left.symbols.ContainsKey($_) } | Sort-Object)
$symbolVariantDifferences = @($symbolShared | Where-Object { $left.symbols[$_].count -ne $right.symbols[$_].count })
$generator = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$mixedHash = if ($null -ne $mixedReport) { $mixedReport.hash } else { 'not-supplied' }
$bounded = HashText ("webforms-chain-comparison.v1`n$($left.hash)`n$($right.hash)`n$mixedHash`n$generator")
function Convert-ComparisonHtml([string]$text) { return [Net.WebUtility]::HtmlEncode($text) }
$html = [Text.StringBuilder]::new()
[void]$html.AppendLine('<!doctype html><html lang="en"><meta charset="utf-8"><title>Local chain comparison</title><style>body{font:16px/1.5 system-ui;margin:2rem;max-width:1200px}pre{white-space:pre-wrap;overflow-wrap:anywhere}td,th{border:1px solid #aaa;padding:.5rem;vertical-align:top}table{border-collapse:collapse}</style><h1>Local chain comparison</h1>')
[void]$html.AppendLine('<p>PRIVATE diagnostic. Input JSON is read as supplied; native receipts and hashes are not admitted by this helper. Rule workflow.webforms.chain-comparison.v1; Tier4Unknown. Exact sequence equality includes source, scan and commit identity. Symbol-only matches omit those identities and are comparison hints. No parity or runtime conclusion.</p>')
[void]$html.AppendLine("<p>Historical: $($left.exact.Count) exact sequences / $($left.variants) variants. Current: $($right.exact.Count) / $($right.variants). Shared exact sequences: $($shared.Count). Historical only: $($leftOnly.Count). Current only: $($rightOnly.Count). Shared sequences with variant-count differences: $($variantDifferences.Count). Symbol-only sequence matches: $symbolMatches.</p>")
[void]$html.AppendLine("<p>Symbol-only hints: $($left.symbols.Count) historical / $($right.symbols.Count) current unique sequences; $symbolMatches shared; $($symbolLeftOnly.Count) historical only; $($symbolRightOnly.Count) current only; $($symbolVariantDifferences.Count) shared sequences with variant-count differences. These omit provenance identity and are not exact route parity.</p>")
[void]$html.AppendLine('<h2>Query settings and coverage</h2><table><tr><th>Historical</th><th>Current</th></tr><tr>')
foreach ($report in @($left, $right)) {
    [void]$html.AppendLine('<td><pre>' + (Convert-ComparisonHtml (Json @{ query = (Value $report.header 'query'); summary = (Value $report.header 'summary'); coverage = (Value $report.header 'reportCoverage'); sources = (Value $report.header 'sources'); declaredIndexSha256 = $report.indexHash })) + '</pre></td>')
}
[void]$html.AppendLine('</tr></table>')
if ($null -ne $mixedReport) {
    [void]$html.AppendLine('<h2>Saved mixed-mode context</h2><pre>' + (Convert-ComparisonHtml (Json @{ query = (Value $mixedReport.header 'query'); summary = (Value $mixedReport.header 'summary'); coverage = (Value $mixedReport.header 'reportCoverage'); sources = (Value $mixedReport.header 'sources'); fileSha256 = $mixedHash })) + '</pre>')
} else { [void]$html.AppendLine('<p>Mixed-mode report not found. Supply -Mixed with its folder or JSON path; no graph query is started.</p>') }
$shown = 0
foreach ($side in @(
    @{ title = 'Historical-only symbol sequences (hints)'; keys = $symbolLeftOnly; entries = $left.symbols; left = $left.symbols; right = $right.symbols },
    @{ title = 'Current-only symbol sequences (hints)'; keys = $symbolRightOnly; entries = $right.symbols; left = $left.symbols; right = $right.symbols },
    @{ title = 'Shared symbol sequence, variant count changed (hint)'; keys = $symbolVariantDifferences; entries = $left.symbols; left = $left.symbols; right = $right.symbols },
    @{ title = 'Historical only (exact identity)'; keys = $leftOnly; entries = $left.exact; left = $left.exact; right = $right.exact },
    @{ title = 'Current only (exact identity)'; keys = $rightOnly; entries = $right.exact; left = $left.exact; right = $right.exact },
    @{ title = 'Shared exact sequence, variant count changed'; keys = $variantDifferences; entries = $left.exact; left = $left.exact; right = $right.exact })) {
    [void]$html.AppendLine('<h2>' + $side.title + '</h2>')
    foreach ($key in $side.keys) {
        if ($shown -ge 500) { break }
        $shown++
        $entry = $side.entries[$key]
        $counts = if ($side.right.ContainsKey($key) -and $side.left.ContainsKey($key)) { "$($side.left[$key].count) historical / $($side.right[$key].count) current" } else { "$($entry.count) variants" }
        [void]$html.AppendLine('<details><summary>' + (Convert-ComparisonHtml "$key — $counts") + '</summary><pre>' + (Convert-ComparisonHtml ($entry.labels -join "`n→ ")) + '</pre></details>')
        if ($entry.ContainsKey('evidence') -and $side.title -ne 'Current-only symbol sequences (hints)') {
            $mixedCount = if ($null -eq $mixedReport) { 'not supplied' } elseif ($mixedReport.symbols.ContainsKey($key)) { [string]$mixedReport.symbols[$key].count } else { '0' }
            [void]$html.AppendLine('<p>Saved mixed-mode variants for this symbol hint: ' + $mixedCount + '. Not parity proof. Empty edge arrays mean no edge evidence supplied; method labels alone do not prove a source bridge.</p><details><summary>Historical edge evidence (all variants)</summary><pre>' + (Convert-ComparisonHtml (Json $entry.evidence.ToArray())) + '</pre></details>')
        }
    }
}
[void]$html.AppendLine("<p>Displayed differences: $shown (limit 500).</p><pre>Generator SHA-256: $generator`nBounded input SHA-256: $bounded`nHistorical file SHA-256: $($left.hash)`nCurrent file SHA-256: $($right.hash)</pre></html>")
$bytes = [Text.Encoding]::UTF8.GetBytes($html.ToString())
if ($bytes.Length -gt 32MB) { throw 'WEBFORMS_COMPARE_OUTPUT_LIMIT' }
$output = [IO.File]::Open($OutputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $output.Write($bytes) } finally { $output.Dispose() }
Write-Output "compare.historicalChains=$($left.exact.Count);historicalVariants=$($left.variants)"
Write-Output "compare.currentChains=$($right.exact.Count);currentVariants=$($right.variants)"
Write-Output "compare.sharedExact=$($shared.Count);historicalOnly=$($leftOnly.Count);currentOnly=$($rightOnly.Count);variantCountDifferences=$($variantDifferences.Count);symbolSequenceMatches=$symbolMatches"
Write-Output "compare.symbolHistorical=$($left.symbols.Count);symbolCurrent=$($right.symbols.Count);symbolShared=$symbolMatches;symbolHistoricalOnly=$($symbolLeftOnly.Count);symbolCurrentOnly=$($symbolRightOnly.Count);symbolVariantCountDifferences=$($symbolVariantDifferences.Count)"
$mixedMatches = if ($null -ne $mixedReport) { @($symbolLeftOnly | Where-Object { $mixedReport.symbols.ContainsKey($_) }).Count } else { 0 }
Write-Output "compare.mixedReportPresent=$($null -ne $mixedReport);historicalOnlySymbolSequencesFoundInMixed=$mixedMatches"
Write-Output 'compare=local-diagnostic-written;unadmitted-inputs;no-scan;no-traversal'
if ($Open) { Invoke-Item -LiteralPath $OutputPath }
