[CmdletBinding()]
param([string]$Report, [string]$OutputPath, [switch]$Open)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!$Report) { $Report = Read-Host 'Saved mixed-mode handler report folder (full path)' }
if (Test-Path -LiteralPath $Report -PathType Container) { $Report = Join-Path $Report 'compiled-paths.handoff.local.json' }
if (!$OutputPath) {
    $parent = Split-Path $Report -Parent
    $OutputPath = Join-Path $parent 'handler-sql-evidence.local.html'
    for ($n = 2; (Test-Path -LiteralPath $OutputPath) -and $n -le 1000; $n++) { $OutputPath = Join-Path $parent "handler-sql-evidence-$n.local.html" }
}
if (Test-Path -LiteralPath $OutputPath) { throw 'WEBFORMS_SQL_ROUTE_OUTPUT_EXISTS' }
function Value($obj, [string]$name) {
    if ($obj -is [Collections.IDictionary] -and $obj.Contains($name)) { return ,($obj[$name]) }
    return $null
}
function Json($obj) { ConvertTo-Json -InputObject $obj -Depth 100 -Compress }
function Hash([string]$text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
function Html([string]$text) { [Net.WebUtility]::HtmlEncode($text) }
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
$groups = [Collections.Generic.SortedDictionary[string,object]]::new([StringComparer]::Ordinal)
$references = 0; $sqlNodes = 0; $databaseNodes = 0
foreach ($row in $rows) {
    $nodes = Value $row 'nodes'; $edges = Value $row 'edges'
    if ($null -eq $nodes -or $nodes.Count -eq 0 -or $nodes.Count -gt 2048 -or ($null -ne $edges -and $edges.Count -gt 2048)) { throw 'WEBFORMS_SQL_ROUTE_REFERENCE_LIMIT' }
    $references += $nodes.Count
    if ($null -ne $edges) { $references += $edges.Count }
    if ($references -gt 500000) { throw 'WEBFORMS_SQL_ROUTE_REFERENCE_LIMIT' }
    $identity = [Collections.Generic.List[object]]::new()
    $labels = [Collections.Generic.List[string]]::new()
    $endpoints = [Collections.Generic.List[object]]::new()
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
            foreach ($field in @('nodeId','surfaceKind','surfaceSubtype','sourceKind','surfaceName','operationName','tableName','columnNames','shapeHash','textHash','textLength','combinedFactId','ruleId','evidenceTier','filePath','startLine','endLine','sourceIndexId','scanId','commitSha','limitations')) { $evidence[$field] = Value $node $field }
            $endpoints.Add($evidence)
        }
    }
    $key = Hash (Json $identity.ToArray())
    if (!$groups.ContainsKey($key)) { $groups[$key] = @{ variants = 0; labels = $labels.ToArray(); endpoints = $endpoints.ToArray(); bridges = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal); edgeEvidenceMissing = $false } }
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
$bounded = Hash ("webforms-handler-sql-ledger.v1`n$inputHash`n$generator")
$html = [Text.StringBuilder]::new()
[void]$html.AppendLine('<!doctype html><html lang="en"><meta charset="utf-8"><title>Handler SQL evidence ledger</title><style>body{font:16px/1.5 system-ui;margin:2rem;max-width:1100px}pre{white-space:pre-wrap;overflow-wrap:anywhere}</style><h1>Handler SQL evidence ledger</h1><p>PRIVATE, partial, as-supplied static evidence. Rule workflow.webforms.handler-sql-ledger.v1; Tier4Unknown. Native input admission is not performed. No scan, graph walk, SQL execution, runtime dispatch or parity proof. Database API reachability does not identify command text or a stored procedure. No SQL is inferred from method names. Source bridges remain candidates, not IL calls.</p>')
[void]$html.AppendLine('<h2>Saved query and coverage</h2><pre>' + (Html (Json @{ query = (Value $header 'query'); coverage = (Value $header 'reportCoverage'); summary = (Value $header 'summary'); sources = (Value $header 'sources') })) + '</pre>')
[void]$html.AppendLine("<p>Retained route-record groups: $($groups.Count); retained variants: $($rows.Count); database surface occurrences: $databaseNodes; SQL surface occurrences: $sqlNodes. Groups use full node records; these counts are not chain-parity counts. Occurrences include repeated evidence variants.</p>")
[void]$html.AppendLine('<p>GAP: This projection does not establish command-text or parameter propagation from the handler. A sql-query surface can be a framework API terminal, including Fill; its presence is not proof of a resolved SQL statement. Null evidence fields remain unavailable. Any retained table/operation/hash is shown only as supplied, without proving its binding to an executed command.</p>')
if ($sqlNodes -eq 0) { [void]$html.AppendLine('<p>GAP: No retained SQL query/persistence surface in these paths. Command text, procedure identity and SQL parameter values are unresolved here. A Fill endpoint alone does not close this gap.</p>') }
$shown = 0
foreach ($key in $groups.Keys) {
    if ($shown -ge 500) { break }; $shown++
    $group = $groups[$key]
    [void]$html.AppendLine('<details><summary>' + (Html "$key — $($group.variants) variants") + '</summary><h3>Route labels (not SQL)</h3><pre>' + (Html ($group.labels -join "`n→ ")) + '</pre><h3>Retained database / SQL surface fields</h3><pre>' + (Html (Json $group.endpoints)) + '</pre><h3>Non-IL transitions</h3><pre>' + (Html (Json @($group.bridges))) + '</pre>')
    if ($group.endpoints.Count -eq 0) { [void]$html.AppendLine('<p>GAP: No recognized retained database surface for this route.</p>') }
    if ($group.edgeEvidenceMissing) { [void]$html.AppendLine('<p>GAP: Edge evidence absent in at least one variant; transition scope cannot be classified.</p>') }
    [void]$html.AppendLine('</details>')
}
[void]$html.AppendLine('<h2>Retained report gaps</h2><pre>' + (Html (Json (Value $header 'gaps'))) + '</pre>')
[void]$html.AppendLine("<p>Displayed groups: $shown (limit 500). All input groups contribute to counts.</p><pre>Generator SHA-256: $generator`nBounded input SHA-256: $bounded`nInput file SHA-256: $inputHash</pre></html>")
$bytes = [Text.Encoding]::UTF8.GetBytes($html.ToString())
if ($bytes.Length -gt 32MB) { throw 'WEBFORMS_SQL_ROUTE_OUTPUT_LIMIT' }
$output = [IO.File]::Open($OutputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $output.Write($bytes) } finally { $output.Dispose() }
Write-Output "sqlRoute.exactGroups=$($groups.Count);variants=$($rows.Count);databaseSurfaceOccurrences=$databaseNodes;sqlSurfaceOccurrences=$sqlNodes"
Write-Output 'sqlRoute=local-ledger-written;partial;unadmitted-input;no-scan;no-traversal;no-sql-executed'
if ($Open) { Invoke-Item -LiteralPath $OutputPath }
