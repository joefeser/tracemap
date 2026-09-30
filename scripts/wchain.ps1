param(
    [string]$ReportFolder,
    [ValidateRange(1,100000)][int]$Chain = 7,
    [switch]$Open
)
$ErrorActionPreference = 'Stop'
if (-not $ReportFolder) { $ReportFolder = Read-Host 'Handler requery folder (full path)' }
$inputPath = Join-Path $ReportFolder 'compiled-paths.handoff.local.json'
$outputPath = Join-Path $ReportFolder "chain-$Chain.diagnostic.local.html"
if (Test-Path -LiteralPath $outputPath) { throw 'WEBFORMS_CHAIN_OUTPUT_EXISTS;originals-preserved' }
# This is an explicitly unadmitted local readback, not native checkpoint validation.
# Read once under a write-denying lock; never open the multi-gigabyte combined index.
$stream = [IO.File]::Open($inputPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try {
    if ($stream.Length -gt 64MB) { throw 'WEBFORMS_CHAIN_INPUT_LIMIT' }
    $bytes = [byte[]]::new([int]$stream.Length)
    $stream.ReadExactly($bytes)
} finally { $stream.Dispose() }
$inputHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
$packet = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -AsHashtable -Depth 100
if ($packet.schemaVersion -ne 'webforms-compiled-grouped-handoff.v1' -or $packet.visibility -ne 'local-only') {
    throw 'WEBFORMS_CHAIN_SCHEMA_INVALID'
}
if ($Chain -gt $packet.chains.Count) { throw 'WEBFORMS_CHAIN_SELECTION_INVALID' }
$group = $packet.chains[$Chain - 1]
if ($group.variantIndexes.Count -gt 4096) { throw 'WEBFORMS_CHAIN_VARIANT_LIMIT' }
function Encode-ChainHtml($value) { [Net.WebUtility]::HtmlEncode([string]$value) }
function Label($node) {
    # Exact retained symbol is preferable to an already-redacted display label.
    if ($node.symbolId) { return [string]$node.symbolId }
    return [string]$node.displayName
}
$body = [Text.StringBuilder]::new()
foreach ($variantIndex in $group.variantIndexes) {
    if ($variantIndex -lt 0 -or $variantIndex -ge $packet.variants.Count) { throw 'WEBFORMS_CHAIN_REFERENCE_INVALID' }
    $variant = $packet.variants[$variantIndex]
    if ($variant.nodeReferences.Count -gt 256 -or $variant.edgeReferences.Count -gt 256) { throw 'WEBFORMS_CHAIN_REFERENCE_LIMIT' }
    $nodes = @($variant.nodeReferences | ForEach-Object {
        if (-not $packet.nodes.ContainsKey($_)) { throw 'WEBFORMS_CHAIN_REFERENCE_INVALID' }
        $packet.nodes[$_]
    })
    [void]$body.Append("<section><h2>Variant $(Encode-ChainHtml $variant.path.pathId)</h2><p>$(Encode-ChainHtml $variant.path.classification) / $(Encode-ChainHtml $variant.path.confidence)</p>")
    [void]$body.Append('<ol>')
    foreach ($node in $nodes) {
        [void]$body.Append("<li><code>$(Encode-ChainHtml (Label $node))</code><br>source $(Encode-ChainHtml $node.sourceIndexId); scan $(Encode-ChainHtml $node.scanId); commit $(Encode-ChainHtml $node.commitSha)<br>rule $(Encode-ChainHtml $node.ruleId); tier $(Encode-ChainHtml $node.evidenceTier)</li>")
    }
    [void]$body.Append('</ol><h3>Ordered connecting evidence</h3>')
    foreach ($reference in $variant.edgeReferences) {
        if (-not $packet.edges.ContainsKey($reference)) { throw 'WEBFORMS_CHAIN_REFERENCE_INVALID' }
        $edge = $packet.edges[$reference]
        # Resolve within this variant only: never conflate nodes from another source/variant.
        $from = @($nodes | Where-Object { $_.nodeId -ceq $edge.fromNodeId })
        $to = @($nodes | Where-Object { $_.nodeId -ceq $edge.toNodeId })
        if ($from.Count -ne 1 -or $to.Count -ne 1) { throw 'WEBFORMS_CHAIN_ENDPOINT_AMBIGUOUS' }
        [void]$body.Append("<article><p><code>$(Encode-ChainHtml (Label $from[0]))</code> → <code>$(Encode-ChainHtml (Label $to[0]))</code></p><p>Kind $(Encode-ChainHtml $edge.edgeKind); rule $(Encode-ChainHtml $edge.ruleId); tier $(Encode-ChainHtml $edge.evidenceTier); classification $(Encode-ChainHtml $edge.classification)</p><pre>$(Encode-ChainHtml ($edge | ConvertTo-Json -Depth 20))</pre></article>")
    }
    [void]$body.Append('</section>')
    if ($body.Length -gt 8MB) { throw 'WEBFORMS_CHAIN_OUTPUT_LIMIT' }
}
$generatorHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$bounded = [Text.Encoding]::UTF8.GetBytes("webforms-chain-diagnostic.v1`n$inputHash`n$Chain")
$boundedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bounded)).ToLowerInvariant()
$html = "<!doctype html><html lang='en'><meta charset='utf-8'><title>Chain $Chain local diagnostic</title><style>body{font:16px/1.5 system-ui;max-width:1100px;margin:2rem auto;padding:1rem}code,pre{overflow-wrap:anywhere;white-space:pre-wrap}article,section{border:1px solid #aaa;padding:1rem;margin:1rem 0}li{margin:.5rem 0}</style><h1>Chain $Chain connecting evidence</h1><p>PRIVATE LOCAL DIAGNOSTIC — unadmitted retained readback, not validated native evidence, graph correctness, parity or runtime execution. No scan or traversal. Exact symbols may contain private identities. Do not publish.</p><p>Rule workflow.webforms.chain-diagnostic.v1; tier Tier4Unknown</p><p>Generator SHA-256 <code>$generatorHash</code><br>Bounded input SHA-256 <code>$boundedHash</code><br>Input file SHA-256 <code>$inputHash</code>; chain $Chain; variants $($group.variantIndexes.Count)</p>$body</html>"
$output = [IO.File]::Open($outputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $output.Write([Text.Encoding]::UTF8.GetBytes($html)) } finally { $output.Dispose() }
Write-Output "chainDiagnostic=written;chain=$Chain;variants=$($group.variantIndexes.Count);unadmitted-local-only;no-scan;no-traversal;originals-preserved"
if ($Open) { Start-Process $outputPath }
