#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position=0)][string]$Root,
    [string]$Method,
    [string]$GraphPath,
    [string]$OutputPath,
    [switch]$PrivateMap
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
    function TargetShape([string]$Target) {
        # Derived categories only: never copy a matched identifier or signature fragment.
        $member = 'method-or-unknown'
        if ($Target -cmatch '\|(?:member|method):[0-9]+:get_') { $member = 'property-getter' }
        elseif ($Target -cmatch '\|(?:member|method):[0-9]+:set_') { $member = 'property-setter' }
        elseif ($Target -cmatch '\|(?:member|constructor):5:\.ctor\|') { $member = 'constructor' }
        $returns = 'other-or-unknown'
        if ($Target -cmatch '->type\(namespace:6:System\|names:6:String\)$') { $returns = 'string' }
        elseif ($Target -cmatch '->type\(namespace:6:System\|names:5:Int32\)$') { $returns = 'int32' }
        elseif ($Target -cmatch '->type\(namespace:6:System\|names:4:Void\)$') { $returns = 'void' }
        $reference = if ($Target.StartsWith('memberref|',[StringComparison]::Ordinal)) { 'memberref' } else { 'other-or-unknown' }
        return [ordered]@{reference=$reference;member=$member;returns=$returns}
    }
    $focusIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($node in $focus) { [void]$focusIds.Add($node.nodeId) }
    if (!$graph.PSObject.Properties['commandTraces']) { throw 'command-traces-missing-regenerate-graph' }
    $traceCandidates = @($graph.commandTraces | Where-Object {
        @($_.pathNodeIds | Where-Object { $focusIds.Contains([string]$_) }).Count -gt 0
    })
    if ($traceCandidates.Count -gt 16) { $limited = $true }
    $traceCandidates = @($traceCandidates | Select-Object -First 16)
    $producerIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($trace in $traceCandidates) { foreach ($id in $trace.producerCallFactIds) { [void]$producerIds.Add([string]$id) } }
    # Focus records first, then round-robin other callers. No caller can consume
    # the whole neighborhood budget before the focus has been represented.
    function Prioritize([object[]]$Records, [string]$CallerField, [int]$Limit) {
        $ordered = [Collections.Generic.List[object]]::new()
        foreach ($record in $Records) {
            if ($focusIds.Contains([string]$record.$CallerField)) { $ordered.Add($record) }
        }
        $groups = @($Records | Where-Object { !$focusIds.Contains([string]$_.$CallerField) } | Group-Object -Property $CallerField)
        $round = 0
        while ($ordered.Count -lt $Limit) {
            $added = $false
            foreach ($group in $groups) {
                if ($round -lt $group.Count -and $ordered.Count -lt $Limit) { $ordered.Add($group.Group[$round]); $added = $true }
            }
            if (!$added) { break }
            $round++
        }
        return @($ordered | Select-Object -First $Limit)
    }
    $projectedNodes = @($graph.nodes | Where-Object { $selected.Contains($_.nodeId) } | ForEach-Object {
        [ordered]@{id=(Alias 'N' $_.nodeId); symbol=(Alias 'S' $_.symbolId); source=(Alias 'I' $_.sourceIndexId);
            file=(Alias 'P' $_.filePath); rule=(Alias 'R' $_.ruleId); tier=(Code $_.evidenceTier $tiers);
            kind=(Code $_.nodeKind @('Symbol','Method','Type','Surface')); focus=($focus.nodeId -ccontains $_.nodeId)}
    })
    $edges = @($graph.edges | Where-Object { $selected.Contains($_.fromNodeId) -and $selected.Contains($_.toNodeId) })
    if ($edges.Count -gt 240) { $limited = $true }
    $keptEdges = @(Prioritize $edges 'fromNodeId' 240)
    $projectedEdges = @($keptEdges | ForEach-Object {
        if (@($_.supportingFactIds).Count -gt 8) { $limited = $true }
        [ordered]@{id=(Alias 'E' $_.edgeId); from=(Alias 'N' $_.fromNodeId); to=(Alias 'N' $_.toNodeId);
            kind=(Code $_.edgeKind $kinds); rule=(Alias 'R' $_.ruleId); tier=(Code $_.evidenceTier $tiers);
            facts=@($_.supportingFactIds | Select-Object -First 8 | ForEach-Object { Alias 'F' $_ })}
    })
    $calls = @($graph.calls | Where-Object { $selected.Contains($_.callerNodeId) -or $producerIds.Contains($_.factId) })
    if ($calls.Count -gt 120) { $limited = $true }
    $producerCalls = @($calls | Where-Object { $producerIds.Contains($_.factId) })
    $otherCalls = @($calls | Where-Object { !$producerIds.Contains($_.factId) })
    $keptCalls = @(@($producerCalls | Select-Object -First 120) + @(Prioritize $otherCalls 'callerNodeId' ([Math]::Max(0,120-$producerCalls.Count))))
    $projectedCalls = @($keptCalls | ForEach-Object {
        if (@($_.gapReasons).Count -gt 8) { $limited = $true }
        [ordered]@{caller=(Alias 'N' $_.callerNodeId); fact=(Alias 'F' $_.factId); body=(Alias 'F' $_.bodyFactId);
            target=(Alias 'T' $_.encodedTarget); opcode=(Code $_.opcode @('call','callvirt','newobj'));
            targetShape=(TargetShape ([string]$_.encodedTarget));
            state=(Code $_.state @('retained-target-edge','no-admitted-method-target-edge'));
            rule=(Alias 'R' $_.ruleId); tier=(Code $_.evidenceTier $tiers);
            reasons=@($_.gapReasons | Select-Object -First 8 | ForEach-Object { Code $_ $reasons })}
    })
    $callerCoverage = @($graph.nodes | Where-Object { $selected.Contains($_.nodeId) } | ForEach-Object {
        $id = $_.nodeId
        $totalCalls = @($calls | Where-Object { $_.callerNodeId -ceq $id }).Count
        $retainedCalls = @($keptCalls | Where-Object { $_.callerNodeId -ceq $id }).Count
        $totalEdges = @($graph.edges | Where-Object { $_.fromNodeId -ceq $id }).Count
        $retainedEdges = @($keptEdges | Where-Object { $_.fromNodeId -ceq $id }).Count
        [ordered]@{caller=(Alias 'N' $id);focus=$focusIds.Contains($id);availableCalls=$totalCalls;
            exportedCalls=$retainedCalls;omittedCalls=($totalCalls-$retainedCalls);
            availableOutgoingEdges=$totalEdges;exportedOutgoingEdges=$retainedEdges;omittedOutgoingEdges=($totalEdges-$retainedEdges)}
    })
    $focusComplete = @($callerCoverage | Where-Object { $_.focus -and ($_.omittedCalls -gt 0 -or $_.omittedOutgoingEdges -gt 0) }).Count -eq 0
    $traceGaps = @('IlCommandReturnTargetEdgeMissing','IlCommandReturnTargetEdgesAmbiguous','IlCommandReturnTargetMethodMissing',
        'IlCommandReturnTargetMethodsAmbiguous','IlCommandReturnTargetMissingOrAmbiguous','IlCommandReturnProducerMissingOrAmbiguous',
        'IlCommandVirtualDispatchUnproven','IlCommandReturnVirtualDispatchUnproven','IlCommandOperandValueUnresolved',
        'IlCommandNonIlCallerBridge','IlCommandRootArgumentUnresolved','IlCommandReturnBodyMissingOrAmbiguous',
        'IlCommandReturnEvidenceMissingOrAmbiguous','IlCommandReturnSignatureUnsupported','IlCommandReturnProvenanceUnavailable',
        'IlCommandCallerOperandMissingOrAmbiguous','IlCommandCallerOperandProvenanceUnavailable','IlCommandReturnCycle',
        'IlCommandReturnWorkLimit','IlCommandCallerHopLimit','IlCommandCompositionValueNotMaterialized',
        'IlCommandCompositionOperandsUnavailable','IlCommandCompositionProvenanceUnavailable')
    function Project-OperandChecks($value) {
        if (!$value.PSObject.Properties['operandCheckFailures']) { return }
        $allowed = @('operand-rule','operand-tier','operand-schema','operand-state','call-shape',
            'operand-body-link','operand-call-offset','caller-body-binary-match',
            'body-binary-present','body-generator-present','body-input-present',
            'operand-body-binary-match','operand-body-generator-match','operand-body-input-match',
            'call-body-binary-match','call-body-generator-match','call-body-input-match')
        $value.operandCheckFailures | Select-Object -First 1 | ForEach-Object {
            [ordered]@{edge=(Alias 'E' $_.edgeId);call=(Alias 'F' $_.callFactId);
                operand=(Alias 'F' $_.operandFactId);body=(Alias 'F' $_.bodyFactId);
                failedChecks=@($_.failedChecks | Select-Object -First 17 | ForEach-Object { Code $_ $allowed })}
        }
    }
    $projectedTraces = @($traceCandidates | ForEach-Object {
        $trace = $_; $value = $trace.commandText
        if ($null -eq $value) {
            [ordered]@{endpoint=(Alias 'N' $trace.endpointNodeId);state='command-binding-unavailable'}
            return
        }
        [ordered]@{endpoint=(Alias 'N' $trace.endpointNodeId);path=@($trace.pathNodeIds | ForEach-Object { Alias 'N' $_ });
            state=(Code $value.state @('unresolved-operand','unresolved-root-argument','unresolved-non-il-bridge','unresolved-call-evidence','unresolved-slot','limit','method-local-constant','constant-on-encoded-call-path','symbolic-string-composition'));
            originKind=(Code $value.origin.kind @('call-result','argument-slot','constant-string-hash','constant-int32','unknown','null','allocation-site'));
            originBody=(Alias 'F' $value.originBodyFactId);rule=(Alias 'R' $value.ruleId);
            producers=@($trace.producerCallFactIds | ForEach-Object { Alias 'F' $_ });
            missingProducerRecords=@($trace.producerCallFactIds | Where-Object { $id = $_; @($keptCalls | Where-Object { $_.factId -ceq $id }).Count -eq 0 }).Count;
            steps=@($value.steps | ForEach-Object { [ordered]@{call=(Alias 'F' $_.callFactId);operand=(Alias 'F' $_.operandFactId);
                body=(Alias 'F' $_.callerBodyFactId);callerMethod=(Alias 'F' $_.callerMethodFactId);targetMethod=(Alias 'F' $_.targetMethodFactId)} });
            returnSteps=@(if ($value.PSObject.Properties['returnSteps'] -and $null -ne $value.returnSteps) { $value.returnSteps | ForEach-Object {
                [ordered]@{producer=(Alias 'F' $_.producerCallFactId);body=(Alias 'F' $_.calleeBodyFactId);returnFact=(Alias 'F' $_.returnFactId)} } });
            composition=$(if ($value.PSObject.Properties['composition'] -and $null -ne $value.composition) {
                $c = $value.composition
                [ordered]@{operation=(Code $c.operation @('System.String.Concat'));
                    producer=(Alias 'F' $c.producerCallFactId);operand=(Alias 'F' $c.operandFactId);body=(Alias 'F' $c.bodyFactId);
                    operandKinds=@($c.operands | Select-Object -First 3 | ForEach-Object {
                        Code $_.kind @('call-result','argument-slot','constant-string-hash','unknown','null') });
                    operandBindings=@(if ($c.PSObject.Properties['operandBindings']) { $c.operandBindings | Select-Object -First 3 | ForEach-Object {
                        $b = $_
                        [ordered]@{state=(Code $b.state @('method-local-constant','constant-on-encoded-call-path','unresolved-operand','unresolved-root-argument','unresolved-non-il-bridge','unresolved-call-evidence','unresolved-slot','limit'));
                            originKind=(Code $b.origin.kind @('call-result','argument-slot','constant-string-hash','unknown','null'));
                            originBody=(Alias 'F' $b.originBodyFactId);
                            method=$(if ($b.PSObject.Properties['originMethodIdentity']) { Alias 'S' $b.originMethodIdentity });
                            argumentSlot=$(if ($b.origin.kind -ceq 'argument-slot' -and [string]$b.origin.identity -cmatch '\A(?:0|[1-9][0-9]{0,2})\z') { [int]$b.origin.identity });
                            steps=@($b.steps | Select-Object -First 64 | ForEach-Object { [ordered]@{
                                call=(Alias 'F' $_.callFactId);operand=(Alias 'F' $_.operandFactId);body=(Alias 'F' $_.callerBodyFactId);
                                callerMethod=(Alias 'F' $_.callerMethodFactId);targetMethod=(Alias 'F' $_.targetMethodFactId)} });
                            operandCheckFailures=@(Project-OperandChecks $b);
                            gaps=@($b.gaps | Select-Object -First 16 | ForEach-Object { Code $_ $traceGaps })}
                    } })}
            });
            limitations=@(if ($value.PSObject.Properties['limitations']) { $value.limitations | Select-Object -First 8 | ForEach-Object { Code $_ @('SymbolicStringValueNotMaterialized') } });
            operandCheckFailures=@(Project-OperandChecks $value);
            gaps=@($value.gaps | ForEach-Object { Code $_ $traceGaps })}
    })
    $projection = [ordered]@{nodes=$projectedNodes; edges=$projectedEdges; calls=$projectedCalls;
        commandTraces=$projectedTraces;traceScope='one-shortest-retained-witness-per-endpoint-not-all-variants';
        callerCoverage=$callerCoverage;focusRecordsComplete=$focusComplete;
        sliceLimited=$limited; sourceHadCutoffs=(@($graph.cutoffs).Count -gt 0);
        limits=@{radius=3;nodes=120;edges=240;calls=120}}
    $bytes = [Text.Encoding]::UTF8.GetBytes(($projection | ConvertTo-Json -Depth 20 -Compress))
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    $result = [ordered]@{schemaVersion='aliased-method-graph.v3';ruleId='diagnostics.graph.aliased-slice.v3';
        generatorSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant();
        boundedInputSha256=$hash; claim='unverified-local-diagnostic-projection-not-evidence-authentication';
        limitations=@('Names, paths, identities, rules and targets use per-export sequential aliases; no alias map is included in this shared file.',
            'Topology and allowlisted technical categories remain visible. Review before sharing; sanitization is not organizational approval.',
            'Target aliases identify equal encoded strings only, not resolved methods. Unknown codes are not copied.',
            'No private input hash, source hash, SQL, free-form gaps, offsets, line numbers or original provenance is exported.');graph=$projection}
    $outputBytes = [Text.Encoding]::UTF8.GetBytes(($result | ConvertTo-Json -Depth 24))
    if ($outputBytes.Length -gt 262144) { throw 'output-limit' }
    if (!$OutputPath) { $OutputPath = Join-Path $Root ('graph-share-' + [guid]::NewGuid().ToString('N') + '.json') }
    $mapBytes = $null
    if ($PrivateMap) {
        $reverse = [ordered]@{}
        foreach ($kind in @($aliases.Keys | Sort-Object)) {
            foreach ($entry in @($aliases[$kind].GetEnumerator() | Sort-Object Value)) { $reverse[$entry.Value] = $entry.Key }
        }
        $shareHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($outputBytes)).ToLowerInvariant()
        $mapInput = [ordered]@{shareSha256=$shareHash;aliases=$reverse}
        $mapInputBytes = [Text.Encoding]::UTF8.GetBytes(($mapInput | ConvertTo-Json -Depth 8 -Compress))
        $map = [ordered]@{schemaVersion='private-graph-aliases.v1';visibility='PRIVATE-DO-NOT-SHARE';
            ruleId='diagnostics.graph.private-alias-map.v1';generatorSha256=$result.generatorSha256;
            boundedInputSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($mapInputBytes)).ToLowerInvariant();
            shareFileName=[IO.Path]::GetFileName($OutputPath);mapping=$mapInput}
        $mapBytes = [Text.Encoding]::UTF8.GetBytes(($map | ConvertTo-Json -Depth 12))
        if ($mapBytes.Length -gt 8388608) { throw 'map-limit' }
        $mapDirectory = Join-Path $Root 'private-graph-maps'
        [void][IO.Directory]::CreateDirectory($mapDirectory)
        $mapPath = Join-Path $mapDirectory ([IO.Path]::GetFileNameWithoutExtension($OutputPath) + '.aliases.private.json')
    }
    $stream = [IO.FileStream]::new($OutputPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try {
        if ($PrivateMap) {
            $mapStream = [IO.FileStream]::new($mapPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
            try { $mapStream.Write($mapBytes) } finally { $mapStream.Dispose() }
        }
        $stream.Write($outputBytes)
    } finally { $stream.Dispose() }
    Write-Output "Aliased diagnostic saved: $OutputPath"
    Write-Output "Bytes=$($outputBytes.Length); focus representations=$($focus.Count); focusRecordsComplete=$focusComplete; sliceLimited=$limited. Review before sharing. Private graph unchanged."
    Write-Output "Command traces=$($projectedTraces.Count); scope=one-shortest-retained-witness-per-endpoint."
    if ($PrivateMap) { Write-Output "PRIVATE alias map saved separately: $mapPath — DO NOT UPLOAD. Use wgraph-alias.ps1 with this map and an alias." }
} catch {
    if ($_.Exception.Message -ceq 'command-traces-missing-regenerate-graph') {
        throw 'GRAPH_SHARE_READER_UPDATE_REQUIRED: rebuild the CLI and rerun wrequery.ps1 -MethodGraph against the retained run, then export again. No source rescan required.'
    }
    # Never surface parser errors or exception messages that may contain private input.
    throw 'GRAPH_SHARE_FAILED: check method selection, input schema/size, and a fresh writable output path. Private graph unchanged.'
}
