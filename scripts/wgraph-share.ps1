#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position=0)][string]$Root,
    [string]$Method,
    [string]$GraphPath,
    [string]$OutputPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Deliberately project an allowlist, never redact a serialized private document.
try {
    if (!$GraphPath) {
        $candidates = @(Get-ChildItem -LiteralPath $Root -Directory | Where-Object {
            $_.Name -cmatch '\Ahandler-requery-[a-z][a-z0-9-]*-[a-f0-9]{32}\z' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'method-graph.local.json'))
        } | Select-Object -First 129)
        if ($candidates.Count -eq 0 -or $candidates.Count -gt 128) { throw 'selection' }
        $latest = $candidates | Sort-Object LastWriteTimeUtc,Name -Descending | Select-Object -First 1
        $GraphPath = Join-Path $latest.FullName 'method-graph.local.json'
    }
    $file = Get-Item -LiteralPath $GraphPath
    if ($file.Length -gt 16777216) { throw 'input-limit' }
    $document = Get-Content -LiteralPath $GraphPath -Raw | ConvertFrom-Json -Depth 64
    if ($document.schemaVersion -cne 'retained-method-graph.v1') { throw 'schema' }
    $graph = $document.graph
    if (@($graph.nodes).Count -gt 50000 -or @($graph.edges).Count -gt 50000 -or @($graph.calls).Count -gt 50000) { throw 'records' }
    if (!$Method) { $Method = Read-Host 'Private method name to inspect (used locally only)' }
    if (!$Method -or $Method.Length -gt 256) { throw 'method' }
    $nodes = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($node in $graph.nodes) { $nodes.Add([string]$node.nodeId, $node) }
    # Literal matching only. Include both source and compiled representations.
    $focus = @($graph.nodes | Where-Object {
        ([string]$_.symbolId).Contains($Method, [StringComparison]::Ordinal) -or
        ([string]$_.displayName).Contains($Method, [StringComparison]::Ordinal)
    })
    if ($focus.Count -eq 0 -or $focus.Count -gt 16) { throw 'focus-not-bounded' }
    $outgoing = @{}; $incoming = @{}
    foreach ($edge in $graph.edges) {
        if (!$nodes.ContainsKey([string]$edge.fromNodeId) -or !$nodes.ContainsKey([string]$edge.toNodeId)) { throw 'edge' }
        if (!$outgoing.ContainsKey($edge.fromNodeId)) { $outgoing[$edge.fromNodeId] = [Collections.Generic.List[object]]::new() }
        if (!$incoming.ContainsKey($edge.toNodeId)) { $incoming[$edge.toNodeId] = [Collections.Generic.List[object]]::new() }
        $outgoing[$edge.fromNodeId].Add($edge); $incoming[$edge.toNodeId].Add($edge)
    }
    $selected = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $queue = [Collections.Generic.Queue[object]]::new()
    foreach ($node in $focus) { if ($selected.Add($node.nodeId)) { $queue.Enqueue(@($node.nodeId,0)) } }
    $limited = $false
    # Both directions show callers and callees without pretending this is a complete subtree.
    while ($queue.Count -gt 0) {
        $item = $queue.Dequeue(); $id = [string]$item[0]; $depth = [int]$item[1]
        foreach ($edge in @($outgoing[$id]) + @($incoming[$id])) {
            if ($null -eq $edge) { continue }
            $other = if ($edge.fromNodeId -ceq $id) { $edge.toNodeId } else { $edge.fromNodeId }
            if ($selected.Contains($other)) { continue }
            if ($depth -ge 3 -or $selected.Count -ge 120) { $limited = $true; continue }
            [void]$selected.Add($other); $queue.Enqueue(@($other,($depth+1)))
        }
    }
    $aliases = @{}
    function Alias([string]$Kind, [string]$Value) {
        if (!$Value) { return $null }
        if (!$aliases.ContainsKey($Kind)) { $aliases[$Kind] = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal) }
        $map = $aliases[$Kind]
        if (!$map.ContainsKey($Value)) { $map.Add($Value, ($Kind + ($map.Count + 1))) }
        return $map[$Value]
    }
    function Code([string]$Value, [string[]]$Allowed) {
        if ($Allowed -ccontains $Value) { return $Value }
        return 'other-or-unavailable'
    }
    $tiers = @('Tier1Semantic','Tier2Structural','Tier3SyntaxOrTextual','Tier4Unknown')
    $kinds = @('calls','creates','compiled-il-call','compiled-il-callvirt-candidate','compiled-source-identity',
        'compiled-database-api-candidate','symbol-reconciliation','projectless-publish-member-candidate',
        'projectless-publish-method-candidate')
    $reasons = @('same-assembly-methoddef-target-not-unique','admitted-memberref-target-not-unique',
        'target-assembly-present-without-bound-provenance','exact-il-target-present-without-source-commit-binding',
        'command-binding-join-invalid-or-ambiguous')
    $projectedNodes = @($graph.nodes | Where-Object { $selected.Contains($_.nodeId) } | ForEach-Object {
        [ordered]@{id=(Alias 'N' $_.nodeId); symbol=(Alias 'S' $_.symbolId); source=(Alias 'I' $_.sourceIndexId);
            file=(Alias 'P' $_.filePath); rule=(Alias 'R' $_.ruleId); tier=(Code $_.evidenceTier $tiers);
            kind=(Code $_.nodeKind @('Symbol','Method','Type','Surface')); focus=($focus.nodeId -ccontains $_.nodeId)}
    })
    $edges = @($graph.edges | Where-Object { $selected.Contains($_.fromNodeId) -and $selected.Contains($_.toNodeId) })
    if ($edges.Count -gt 240) { $limited = $true }
    $projectedEdges = @($edges | Select-Object -First 240 | ForEach-Object {
        if (@($_.supportingFactIds).Count -gt 8) { $limited = $true }
        [ordered]@{id=(Alias 'E' $_.edgeId); from=(Alias 'N' $_.fromNodeId); to=(Alias 'N' $_.toNodeId);
            kind=(Code $_.edgeKind $kinds); rule=(Alias 'R' $_.ruleId); tier=(Code $_.evidenceTier $tiers);
            facts=@($_.supportingFactIds | Select-Object -First 8 | ForEach-Object { Alias 'F' $_ })}
    })
    $calls = @($graph.calls | Where-Object { $selected.Contains($_.callerNodeId) })
    if ($calls.Count -gt 120) { $limited = $true }
    $projectedCalls = @($calls | Select-Object -First 120 | ForEach-Object {
        if (@($_.gapReasons).Count -gt 8) { $limited = $true }
        [ordered]@{caller=(Alias 'N' $_.callerNodeId); fact=(Alias 'F' $_.factId); body=(Alias 'F' $_.bodyFactId);
            target=(Alias 'T' $_.encodedTarget); opcode=(Code $_.opcode @('call','callvirt','newobj'));
            state=(Code $_.state @('retained-target-edge','no-admitted-method-target-edge'));
            rule=(Alias 'R' $_.ruleId); tier=(Code $_.evidenceTier $tiers);
            reasons=@($_.gapReasons | Select-Object -First 8 | ForEach-Object { Code $_ $reasons })}
    })
    $projection = [ordered]@{nodes=$projectedNodes; edges=$projectedEdges; calls=$projectedCalls;
        sliceLimited=$limited; sourceHadCutoffs=(@($graph.cutoffs).Count -gt 0);
        limits=@{radius=3;nodes=120;edges=240;calls=120}}
    $bytes = [Text.Encoding]::UTF8.GetBytes(($projection | ConvertTo-Json -Depth 20 -Compress))
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    $result = [ordered]@{schemaVersion='aliased-method-graph.v1';ruleId='diagnostics.graph.aliased-slice.v1';
        generatorSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant();
        boundedInputSha256=$hash; claim='unverified-local-diagnostic-projection-not-evidence-authentication';
        limitations=@('Names, paths, identities, rules and targets use per-export sequential aliases; no alias map is exported.',
            'Topology and allowlisted technical categories remain visible. Review before sharing; sanitization is not organizational approval.',
            'Target aliases identify equal encoded strings only, not resolved methods. Unknown codes are not copied.',
            'No private input hash, source hash, SQL, free-form gaps, offsets, line numbers or original provenance is exported.');graph=$projection}
    $outputBytes = [Text.Encoding]::UTF8.GetBytes(($result | ConvertTo-Json -Depth 24))
    if ($outputBytes.Length -gt 262144) { throw 'output-limit' }
    if (!$OutputPath) { $OutputPath = Join-Path $Root ('graph-share-' + [guid]::NewGuid().ToString('N') + '.json') }
    $stream = [IO.FileStream]::new($OutputPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($outputBytes) } finally { $stream.Dispose() }
    Write-Output "Aliased diagnostic saved: $OutputPath"
    Write-Output "Bytes=$($outputBytes.Length); focus representations=$($focus.Count); sliceLimited=$limited. Review before sharing. Private graph unchanged."
} catch {
    # Never surface parser errors or exception messages that may contain private input.
    throw 'GRAPH_SHARE_FAILED: check method selection, input schema/size, and a fresh writable output path. Private graph unchanged.'
}
