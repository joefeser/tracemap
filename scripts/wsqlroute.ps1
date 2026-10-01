[CmdletBinding()]
param([string]$Report, [string]$VerificationRoot, [string]$OutputPath, [string]$Handler, [switch]$UnresolvedOnly, [switch]$Open)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Handler -and $Handler -cnotmatch '^[A-Za-z0-9_]{1,128}$') { throw 'WEBFORMS_SQL_ROUTE_HANDLER_INVALID' }
if ($VerificationRoot) {
    if ($Report) { throw 'WEBFORMS_SQL_ROUTE_INPUT_CONFLICT' }
    $cli = Join-Path $VerificationRoot 'tool/tracemap.dll'
    if (!(Test-Path -LiteralPath $cli -PathType Leaf)) { throw 'WEBFORMS_SQL_ROUTE_PINNED_TOOL_MISSING' }
    $statusJson = @(& dotnet $cli webforms-review status --run (Join-Path $VerificationRoot 'review/run') --json)
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_SQL_ROUTE_STATUS_FAILED' }
    $status = ($statusJson -join "`n") | ConvertFrom-Json -AsHashtable
    if ($status.schemaVersion -ne 'webforms-review-status.v1' -or !$status.readerMatchesOriginalGenerator -or
        $status.state -ne 'reports-completed-review-only' -or !$status.retainedArtifactsVerified) {
        throw 'WEBFORMS_SQL_ROUTE_RETAINED_STATE_NOT_ADMITTED'
    }
    $Report = Join-Path ([IO.Path]::GetDirectoryName([string]$status.workbenchPath)) 'compiled/compiled-paths.handoff.local.json'
}
if (!$Report) { $Report = Read-Host 'Saved mixed-mode handler report folder (full path)' }
if (Test-Path -LiteralPath $Report -PathType Container) { $Report = Join-Path $Report 'compiled-paths.handoff.local.json' }
if (!$OutputPath) {
    # Never add diagnostics to a checkpoint-owned native report tree.
    $parent = if ($VerificationRoot) { $VerificationRoot } else { Split-Path $Report -Parent }
    $stem = if ($UnresolvedOnly) { 'handler-unresolved-command-evidence' } else { 'handler-sql-evidence' }
    $OutputPath = Join-Path $parent "$stem.local.html"
    for ($n = 2; (Test-Path -LiteralPath $OutputPath) -and $n -le 1000; $n++) { $OutputPath = Join-Path $parent "$stem-$n.local.html" }
}
if (Test-Path -LiteralPath $OutputPath) { throw 'WEBFORMS_SQL_ROUTE_OUTPUT_EXISTS' }
function Value($obj, [string]$name) {
    if ($obj -is [Collections.IDictionary] -and $obj.Contains($name)) { return ,($obj[$name]) }
    return $null
}
function Json($obj) { ConvertTo-Json -InputObject $obj -Depth 100 -Compress }
function Hash([string]$text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
function Html([string]$text) { [Net.WebUtility]::HtmlEncode($text) }
function BoundedText([string]$text, [int]$limit = 1024) {
    if ($text.Length -gt $limit) { return $text.Substring(0, $limit) + ' [display truncated]' }
    return $text
}
function JsonHtml($obj, [int]$limit = 65536) { Html (BoundedText (Json $obj) $limit) }
$stream = [IO.File]::Open($Report, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try {
    if ($stream.Length -gt 256MB) { throw 'WEBFORMS_SQL_ROUTE_INPUT_LIMIT' }
    $inputHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
    $stream.Position = 0
    $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true, 16384, $true)
    try { $document = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable -Depth 100 } finally { $reader.Dispose() }
} finally { $stream.Dispose() }
$header = $document
$rows = [Collections.Generic.List[object]]::new()
if ((Value $document 'schemaVersion') -eq 'webforms-compiled-grouped-handoff.v1') {
    $header = Value $document 'header'
    $variants = Value $document 'variants'
    $nodes = Value $document 'nodes'; $edges = Value $document 'edges'
    if ($null -eq $variants -or $variants.Count -gt 10000 -or $nodes -isnot [Collections.IDictionary] -or $edges -isnot [Collections.IDictionary]) { throw 'WEBFORMS_SQL_ROUTE_SHAPE_INVALID' }
    foreach ($variant in $variants) {
        $projection = @{}
        foreach ($pair in @(@{ refs = 'nodeReferences'; records = $nodes; target = 'nodes' }, @{ refs = 'edgeReferences'; records = $edges; target = 'edges' })) {
            $refs = Value $variant $pair.refs
            if ($null -eq $refs -or $refs.Count -gt 2048) { throw 'WEBFORMS_SQL_ROUTE_REFERENCE_LIMIT' }
            $items = [Collections.Generic.List[object]]::new()
            foreach ($ref in $refs) {
                if (!$pair.records.Contains($ref)) { throw 'WEBFORMS_SQL_ROUTE_REFERENCE_MISSING' }
                $items.Add($pair.records[$ref])
            }
            $projection[$pair.target] = $items.ToArray()
        }
        $rows.Add($projection)
    }
} else {
    $paths = Value $document 'paths'
    if ($null -eq $paths -or $paths.Count -gt 10000 -or $null -eq (Value $document 'query')) { throw 'WEBFORMS_SQL_ROUTE_SHAPE_INVALID' }
    foreach ($row in $paths) { $rows.Add($row) }
}
if ($Handler) {
    $selected = [Collections.Generic.List[object]]::new()
    $roots = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($row in $rows) {
        $nodes = Value $row 'nodes'
        if ($null -eq $nodes -or $nodes.Count -eq 0) { throw 'WEBFORMS_SQL_ROUTE_NODE_INVALID' }
        $label = [string](Value $nodes[0] 'displayName')
        $symbol = [string](Value $nodes[0] 'symbolId')
        if ($label.Split('(', 2)[0].EndsWith('.' + $Handler, [StringComparison]::Ordinal) -or
            $symbol.Contains("|method:$($Handler.Length):$Handler|", [StringComparison]::Ordinal)) {
            [void]$roots.Add([string](Value $nodes[0] 'nodeId')); $selected.Add($row)
        }
    }
    if ($roots.Count -ne 1) { throw 'WEBFORMS_SQL_ROUTE_HANDLER_MISSING_OR_AMBIGUOUS' }
    $rows = $selected
}
$groups = [Collections.Generic.SortedDictionary[string,object]]::new([StringComparer]::Ordinal)
$references = 0; $sqlNodes = 0; $databaseNodes = 0
$commandBindings = 0; $constantTexts = 0; $procedureTypes = 0; $unresolvedTexts = 0
foreach ($row in $rows) {
    $nodes = Value $row 'nodes'; $edges = Value $row 'edges'
    if ($null -eq $nodes -or $nodes.Count -eq 0 -or $nodes.Count -gt 2048 -or ($null -ne $edges -and $edges.Count -gt 2048)) { throw 'WEBFORMS_SQL_ROUTE_REFERENCE_LIMIT' }
    $references += $nodes.Count
    if ($null -ne $edges) { $references += $edges.Count }
    if ($references -gt 500000) { throw 'WEBFORMS_SQL_ROUTE_REFERENCE_LIMIT' }
    $identity = [Collections.Generic.List[object]]::new()
    $labels = [Collections.Generic.List[string]]::new()
    $endpoints = [Collections.Generic.List[object]]::new()
    $hasUnresolved = $false
    foreach ($node in $nodes) {
        if ($node -isnot [Collections.IDictionary] -or [string]::IsNullOrWhiteSpace([string](Value $node 'nodeId'))) { throw 'WEBFORMS_SQL_ROUTE_NODE_INVALID' }
        # Keep differing evidence fields separate rather than discarding them
        # when two records happen to carry the same node identity.
        $identity.Add($node)
        $labels.Add([string](Value $node 'displayName'))
        $kind = Value $node 'surfaceKind'
        if ($kind -in @('database-api', 'sql-query', 'sql-persistence')) {
            $databaseNodes++
            if ($kind -in @('sql-query', 'sql-persistence')) { $sqlNodes++ }
            $evidence = @{}
            foreach ($field in @('nodeId','surfaceKind','surfaceSubtype','sourceKind','surfaceName','operationName','tableName','columnNames','shapeHash','textHash','textLength','combinedFactId','ruleId','evidenceTier','filePath','startLine','endLine','sourceIndexId','scanId','commitSha','limitations','commandBinding')) { $evidence[$field] = Value $node $field }
            $endpoints.Add($evidence)
            $binding = Value $node 'commandBinding'
            if ($binding -is [Collections.IDictionary] -and (Value $binding 'schema') -eq 'il-command-binding.v1') {
                $commandBindings++
                $text = Value $binding 'commandTextFromPath'; $type = Value $binding 'commandTypeFromPath'
                if ((Value $text 'state') -in @('method-local-constant','constant-on-encoded-call-path') -and
                    (Value (Value $text 'origin') 'kind') -eq 'constant-string-hash') { $constantTexts++ } else { $unresolvedTexts++; $hasUnresolved = $true }
                if ((Value $type 'state') -in @('method-local-constant','constant-on-encoded-call-path') -and
                    (Value (Value $type 'origin') 'kind') -eq 'constant-int32' -and
                    (Value (Value $type 'origin') 'identity') -eq '4') { $procedureTypes++ }
            }
        }
    }
    $key = Hash (Json $identity.ToArray())
    if (!$groups.ContainsKey($key)) { $groups[$key] = @{ variants = 0; labels = $labels.ToArray(); endpoints = $endpoints.ToArray(); bridges = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal); edgeEvidenceMissing = $false; unresolved = $hasUnresolved } }
    $group = $groups[$key]; $group.variants++
    if ($null -eq $edges -or $edges.Count -eq 0) { $group.edgeEvidenceMissing = $true }
    foreach ($edge in @($edges)) {
        if ($null -eq $edge) { continue }
        $kind = Value $edge 'edgeKind'
        if ([string]::IsNullOrWhiteSpace([string]$kind)) { throw 'WEBFORMS_SQL_ROUTE_EDGE_INVALID' }
        if ($kind -notin @('compiled-il-call','compiled-il-callvirt-candidate','compiled-database-api-candidate','legacy-root-selection')) { [void]$group.bridges.Add([string]$kind) }
    }
}
$generator = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$bounded = Hash ("webforms-handler-sql-ledger.v1`n$inputHash`n$generator`nhandler:$Handler`nunresolvedOnly:$([bool]$UnresolvedOnly)")
$html = [Text.StringBuilder]::new()
[void]$html.AppendLine('<!doctype html><html lang="en"><meta charset="utf-8"><title>Handler SQL evidence ledger</title><style>body{font:16px/1.5 system-ui;margin:2rem;max-width:1100px}pre{white-space:pre-wrap;overflow-wrap:anywhere}</style><h1>Handler SQL evidence ledger</h1><p>PRIVATE, partial, as-supplied static evidence. Rule workflow.webforms.handler-sql-ledger.v1; Tier4Unknown. Native input admission is not performed. No scan, graph walk, SQL execution, runtime dispatch or parity proof. Database API reachability does not identify command text or a stored procedure. No SQL is inferred from method names. Source bridges remain candidates, not IL calls.</p>')
[void]$html.AppendLine('<h2>Saved query and coverage</h2><pre>' + (JsonHtml @{ query = (Value $header 'query'); coverage = (Value $header 'reportCoverage'); summary = (Value $header 'summary'); sources = (Value $header 'sources') }) + '</pre>')
[void]$html.AppendLine('<p>Handler filter: ' + (Html $(if ($Handler) { $Handler } else { 'none; all retained roots' })) + '. This selects retained rows only, not a new compiled-only traversal.</p>')
[void]$html.AppendLine("<p>Retained route-record groups: $($groups.Count); retained variants: $($rows.Count); database surface occurrences: $databaseNodes; SQL surface occurrences: $sqlNodes. Groups use full node records; these counts are not chain-parity counts. Occurrences include repeated evidence variants.</p>")
[void]$html.AppendLine("<h2>Retained command-binding candidates</h2><p>Binding occurrences: $commandBindings; constant command-text fingerprints: $constantTexts; StoredProcedure type candidates: $procedureTypes; unresolved command-text candidates: $unresolvedTexts. Counts include repeated evidence variants and do not count distinct procedures. Text fingerprints retain hashes rather than readable command text.</p>")
if ($commandBindings -gt 0) {
    [void]$html.AppendLine('<p>Retained commandBinding fields carry static operand candidates, including method-local or encoded-call-path origins where supplied. Expand a route to inspect those fields and its non-IL transitions. This ledger displays supplied evidence without independently validating the binding. Runtime command selection, SQL parameter values and execution remain unverified.</p>')
} else {
    [void]$html.AppendLine('<p>GAP: No retained command-binding candidates in these paths. This projection does not establish command-text or parameter propagation from the handler.</p>')
}
[void]$html.AppendLine('<p>A sql-query surface can be a framework API terminal, including Fill; its presence alone does not prove a resolved SQL statement. Null evidence fields remain unavailable. Any retained table/operation/hash is shown only as supplied, without proving its binding to an executed command.</p>')
if ($sqlNodes -eq 0) { [void]$html.AppendLine('<p>GAP: No retained SQL query/persistence surface in these paths. Database API nodes may still carry command-binding candidates counted above. Readable procedure names, SQL statement bodies and SQL parameter values are not established by this summary.</p>') }
$shown = 0
$routeDisplayBytes = 0
$unresolvedGroups = @($groups.Values | Where-Object { $_.unresolved }).Count
if ($UnresolvedOnly) { [void]$html.AppendLine("<p>Display filter: unresolved command bindings only; matching route groups: $unresolvedGroups. Counts above include all selected-handler variants. Saved query limits and gap counts describe the original query, including its other roots. This filter performs no new traversal and cannot recover omitted routes.</p>") }
foreach ($key in $groups.Keys) {
    if ($shown -ge 500) { break }
    $group = $groups[$key]
    if ($UnresolvedOnly -and !$group.unresolved) { continue }
    $section = '<details><summary>' + (Html "$key — $($group.variants) variants") + '</summary><h3>Route labels (not SQL)</h3><pre>' + (Html (BoundedText ($group.labels -join "`n→ ") 65536)) + '</pre><h3>Retained database / SQL surface fields</h3><pre>' + (JsonHtml $group.endpoints) + '</pre><h3>Non-IL transitions</h3><pre>' + (JsonHtml @($group.bridges)) + '</pre>'
    $sectionBytes = [Text.Encoding]::UTF8.GetByteCount($section)
    if ($routeDisplayBytes + $sectionBytes -gt 8MB) { break }
    $routeDisplayBytes += $sectionBytes; $shown++
    [void]$html.AppendLine($section)
    if ($group.endpoints.Count -eq 0) { [void]$html.AppendLine('<p>GAP: No recognized retained database surface for this route.</p>') }
    if ($group.edgeEvidenceMissing) { [void]$html.AppendLine('<p>GAP: Edge evidence absent in at least one variant; transition scope cannot be classified.</p>') }
    [void]$html.AppendLine('</details>')
}
[void]$html.AppendLine('<h2>Retained report gaps (bounded summary)</h2>')
$gapGroups = [Collections.Generic.SortedDictionary[string,object]]::new([StringComparer]::Ordinal)
$gapCount = 0; $ungroupedGaps = 0
foreach ($gap in (Value $header 'gaps')) {
    if ($gap -isnot [Collections.IDictionary]) { throw 'WEBFORMS_SQL_ROUTE_GAP_INVALID' }
    $gapCount++
    $kind = [string](Value $gap 'gapKind'); $reason = [string](Value $gap 'reason')
    $gapKey = Hash (Json @($kind, $reason))
    if (!$gapGroups.ContainsKey($gapKey)) {
        if ($gapGroups.Count -ge 1000) { $ungroupedGaps++; continue }
        $gapGroups[$gapKey] = @{ kind = (BoundedText $kind); reason = (BoundedText $reason); count = 0; samples = [Collections.Generic.List[object]]::new() }
    }
    $bucket = $gapGroups[$gapKey]; $bucket.count++
    if ($bucket.samples.Count -lt 3) {
        $sample = @{}
        foreach ($field in @('gapId','ruleId','evidenceTier','nodeId','combinedFactId','filePath','startLine','endLine','commitSha','extractorVersion','evidenceScope','message')) { $sample[$field] = BoundedText ([string](Value $gap $field)) }
        $bucket.samples.Add($sample)
    }
}
$gapShown = 0
foreach ($key in $gapGroups.Keys) {
    if ($gapShown -ge 200) { break }; $gapShown++
    $bucket = $gapGroups[$key]
    [void]$html.AppendLine('<details><summary>' + (Html "$($bucket.kind) / $($bucket.reason) — $($bucket.count) gaps") + '</summary><pre>' + (JsonHtml $bucket.samples.ToArray() 8192) + '</pre></details>')
}
[void]$html.AppendLine("<p>Retained gaps: $gapCount; grouped categories: $($gapGroups.Count); displayed categories: $gapShown; overflow-category gaps: $ungroupedGaps. At most 3 samples per category, fields limited to 1,024 characters. This is a display projection, not complete gap evidence; originals remain unchanged.</p>")
[void]$html.AppendLine("<p>Displayed groups: $shown (limit 500). All input groups contribute to counts.</p><pre>Generator SHA-256: $generator`nBounded input SHA-256: $bounded`nInput file SHA-256: $inputHash</pre></html>")
$bytes = [Text.Encoding]::UTF8.GetBytes($html.ToString())
if ($bytes.Length -gt 32MB) { throw 'WEBFORMS_SQL_ROUTE_OUTPUT_LIMIT' }
$output = [IO.File]::Open($OutputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $output.Write($bytes) } finally { $output.Dispose() }
Write-Output "sqlRoute.exactGroups=$($groups.Count);variants=$($rows.Count);databaseSurfaceOccurrences=$databaseNodes;sqlSurfaceOccurrences=$sqlNodes"
Write-Output "sqlRoute.retainedGaps=$gapCount;gapCategories=$($gapGroups.Count);displayedGapCategories=$gapShown;overflowCategoryGaps=$ungroupedGaps;displayedRouteGroups=$shown"
Write-Output "sqlRoute.commandBindings=$commandBindings;constantTextCandidates=$constantTexts;storedProcedureTypeCandidates=$procedureTypes;unresolvedTextCandidates=$unresolvedTexts;asSupplied=true"
if ($UnresolvedOnly) { Write-Output "sqlRoute.unresolvedOnly=true;unresolvedGroups=$unresolvedGroups;displayedGroups=$shown" }
Write-Output 'sqlRoute=local-ledger-written;partial;unadmitted-input;no-scan;no-traversal;no-sql-executed'
if ($Open) { Invoke-Item -LiteralPath $OutputPath }
