# Read-only helpers. No checkpoint edits or relaxed native verification.
function Assert-WebFormsReceipt($Receipt, [string]$Code) {
    if ($Receipt -isnot [pscustomobject] -or $Receipt.schemaVersion -isnot [string] -or $Receipt.schemaVersion -cne 'webforms-handler-requery.v1' -or
        $Receipt.artifacts -isnot [array]) { throw $Code }
    foreach ($item in $Receipt.artifacts) {
        if ($item -isnot [pscustomobject] -or $item.relativePath -isnot [string] -or
            [string]::IsNullOrWhiteSpace($item.relativePath) -or
            $item.relativePath -cnotmatch '\A[a-zA-Z0-9][a-zA-Z0-9._/-]*\z' -or
            @($item.relativePath.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -ne 0 -or
            ($item.bytes -isnot [long] -and $item.bytes -isnot [int]) -or $item.bytes -lt 0 -or
            $item.sha256 -isnot [string] -or $item.sha256 -cnotmatch '\A[a-f0-9]{64}\z') { throw $Code }
    }
    # Older grouped receipts predate the discriminator. Graph receipts never
    # fall back to grouped interpretation, even when their discriminator is absent.
    $variant = if ($Receipt.PSObject.Properties['reportVariant']) { $Receipt.reportVariant } else { 'grouped-paths' }
    if ($variant -isnot [string] -or $variant -cnotin @('grouped-paths','method-graph')) { throw $Code }
    if ($Receipt.PSObject.Properties['reportVariant']) {
        if ($Receipt.pathEnumerationPerformed -isnot [bool] -or $Receipt.primaryReport -isnot [string] -or
            $Receipt.pathEnumerationPerformed -ne ($variant -ceq 'grouped-paths') -or
            $Receipt.primaryReport -cne $(if ($variant -ceq 'method-graph') { 'method-graph.local.html' } else { 'compiled-paths.local.html' })) { throw $Code }
        foreach ($field in @('exactChains','evidenceVariants')) {
            if (($Receipt.$field -isnot [int] -and $Receipt.$field -isnot [long]) -or $Receipt.$field -lt 0) { throw $Code }
        }
        $expected = if ($variant -ceq 'method-graph') { @('method-graph.local.html','method-graph.local.json') }
            else { @('compiled-paths.local.html','compiled-paths.handoff.local.json') }
        if ($Receipt.artifacts.Count -ne 2) { throw $Code }
        foreach ($name in $expected) { if (@($Receipt.artifacts | Where-Object relativePath -CEQ $name).Count -ne 1) { throw $Code } }
    }
    if ($variant -ceq 'method-graph') {
        if ($Receipt.pathEnumerationPerformed -isnot [bool] -or $Receipt.pathEnumerationPerformed -ne $false -or
            $Receipt.primaryReport -cne 'method-graph.local.html' -or $Receipt.exactChains -ne 0 -or $Receipt.evidenceVariants -ne 0 -or
            @($Receipt.artifacts | Where-Object relativePath -CEQ 'method-graph.local.json').Count -ne 1 -or
            @($Receipt.artifacts | Where-Object relativePath -CEQ 'compiled-paths.handoff.local.json').Count -ne 0) { throw $Code }
    } elseif (($Receipt.PSObject.Properties['pathEnumerationPerformed'] -and
        ($Receipt.pathEnumerationPerformed -isnot [bool] -or $Receipt.pathEnumerationPerformed -ne $true)) -or
        @($Receipt.artifacts | Where-Object relativePath -CEQ 'compiled-paths.handoff.local.json').Count -ne 1) { throw $Code }
    return $variant
}

function Assert-WebFormsSummaryTree($Value, [string]$Code, [int]$Depth = 0) {
    if ($Depth -gt 64) { throw $Code }
    if ($Value -is [array]) { foreach ($item in $Value) { Assert-WebFormsSummaryTree $item $Code ($Depth + 1) }; return }
    if ($Value -isnot [pscustomobject]) { return }
    foreach ($p in $Value.PSObject.Properties) {
        $v = $p.Value
        if ($null -eq $v) { continue }
        if ($p.Name -in @('gapKind','reason','cutoffCause','filePath','nodeId','ruleId','evidenceTier','message','fromNodeId','toNodeId','edgeKind','surfaceKind','surfaceName','state','kind','opcode') -and $v -isnot [string]) { throw $Code }
        if ($p.Name -in @('startLine','endLine','traversalWorkUnits','maxTraversalWork','maxDepth','targetArgumentSlot') -and
            ($v -isnot [int] -and $v -isnot [long])) { throw $Code }
        if ($p.Name -in @('prefixTruncated','lastEdgeNotTraversed') -and $v -isnot [bool]) { throw $Code }
        if ($p.Name -in @('gaps','edges','variants','nodeReferences','steps','returnSteps','cutoffs') -and $v -isnot [array]) { throw $Code }
        if ($p.Name -in @('edges','variants','steps','returnSteps')) {
            foreach ($record in $v) { if ($record -isnot [pscustomobject]) { throw $Code } }
        }
        if ($p.Name -eq 'nodeReferences') { foreach ($id in $v) { if ($id -isnot [string] -or [string]::IsNullOrWhiteSpace($id)) { throw $Code } } }
        if ($p.Name -in @('header','summary','query','cutoffWitness','commandBinding','commandTextFromPath','commandTypeFromPath','origin') -and $v -isnot [pscustomobject]) { throw $Code }
        if ($p.Name -in @('commandTextFromPath','commandTypeFromPath')) {
            if ($v.gaps -isnot [array] -or $v.steps -isnot [array] -or $v.origin -isnot [pscustomobject]) { throw $Code }
            foreach ($gap in $v.gaps) { if ($gap -isnot [string]) { throw $Code } }
        }
        Assert-WebFormsSummaryTree $v $Code ($Depth + 1)
    }
}

function Get-WebFormsVerifiedStatus([string]$Cli, [string]$Run) {
    $candidates = @($Run)
    if ($Run -cmatch '\A[A-Za-z]:[\\/]') {
        $alternate = if ([char]::IsUpper($Run[0])) { $Run.Substring(0,1).ToLowerInvariant() } else { $Run.Substring(0,1).ToUpperInvariant() }
        $candidates += $alternate + $Run.Substring(1)
    }
    foreach ($candidate in $candidates) {
        $text = @(& dotnet $Cli webforms-review status --run $candidate --json 2>&1)
        if ($LASTEXITCODE -eq 0) {
            return @{ Run=$candidate; Status=(($text -join "`n") | ConvertFrom-Json) }
        }
        # Only the known casing-sensitive checkpoint failure permits one
        # read-only retry of the same drive path. All native checks still run.
        if (($text -join "`n").Trim() -ne 'error: WEBFORMS_EXECUTION_CHECKPOINT_INVALID') { break }
    }
    throw 'WEBFORMS_HANDLER_STATUS_FAILED; native verification failed; originals preserved'
}

function Write-WebFormsTruncationSummary([string]$Directory) {
    try { Write-WebFormsTruncationSummaryCore $Directory }
    catch {
        if ($_.Exception.Message -cmatch '\AWEBFORMS_HANDLER_[A-Z_]+\z') { throw }
        throw 'WEBFORMS_HANDLER_SUMMARY_METADATA_INVALID'
    }
}
function Write-WebFormsTruncationSummaryCore([string]$Directory) {
    $receiptPath = Join-Path $Directory 'handler-requery.local.json'
    $handoffPath = Join-Path $Directory 'compiled-paths.handoff.local.json'
    # This is a bounded console projection, not a new evidence artifact.
    if ((Get-Item -LiteralPath $receiptPath).Length -gt 4194304) {
        Write-Output 'Truncation details unavailable: summary input exceeds its 64 MiB handoff / 4 MiB receipt bound. Reports preserved.'
        return
    }
    function Read-SummaryBytes([string]$Path, [int]$Maximum) {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            if ($stream.Length -gt $Maximum) { throw 'WEBFORMS_HANDLER_SUMMARY_INPUT_LIMIT' }
            $buffer = [byte[]]::new([int]$stream.Length)
            $offset = 0
            while ($offset -lt $buffer.Length) {
                $read = $stream.Read($buffer, $offset, $buffer.Length - $offset)
                if ($read -eq 0) { throw 'WEBFORMS_HANDLER_SUMMARY_INPUT_CHANGED' }
                $offset += $read
            }
            return ,$buffer
        } finally { $stream.Dispose() }
    }
    $receipt = [Text.Encoding]::UTF8.GetString((Read-SummaryBytes $receiptPath 4194304)).TrimStart([char]0xFEFF) | ConvertFrom-Json
    $variant = Assert-WebFormsReceipt $receipt 'WEBFORMS_HANDLER_SUMMARY_RECEIPT_INVALID'
    $artifactName = if ($variant -ceq 'method-graph') { 'method-graph.local.json' } else { 'compiled-paths.handoff.local.json' }
    $handoffPath = Join-Path $Directory $artifactName
    $artifact = @($receipt.artifacts | Where-Object { $_.relativePath -ceq $artifactName })
    $bytes = Read-SummaryBytes $handoffPath 67108864
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    if ($artifact.Count -ne 1 -or $artifact[0].bytes -ne $bytes.Length -or $artifact[0].sha256 -cne $hash) {
        throw 'WEBFORMS_HANDLER_SUMMARY_HANDOFF_CHANGED'
    }
    $handoff = [Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF) | ConvertFrom-Json
    Assert-WebFormsSummaryTree $handoff 'WEBFORMS_HANDLER_SUMMARY_METADATA_INVALID'
    if ($variant -ceq 'method-graph') {
        if ($handoff.schemaVersion -isnot [string] -or $handoff.schemaVersion -cne 'retained-method-graph.v1' -or $handoff.graph -isnot [pscustomobject] -or
            $handoff.graph.cutoffs -isnot [array] -or $handoff.graph.nodes -isnot [array] -or
            $handoff.graph.edges -isnot [array] -or $handoff.graph.calls -isnot [array]) { throw 'WEBFORMS_HANDLER_SUMMARY_SCHEMA_INVALID' }
        Write-Output "Method graph: nodes=$($handoff.graph.nodes.Count); edges=$($handoff.graph.edges.Count); calls=$($handoff.graph.calls.Count); cutoffs=$($handoff.graph.cutoffs.Count)."
        Write-Output 'Terminal path enumeration was not run. Graph cutoffs are not grouped path counts; inspect method-graph.local.html.'
        return
    }
    if ($handoff.schemaVersion -isnot [string] -or $handoff.schemaVersion -cne 'webforms-compiled-grouped-handoff.v1') { throw 'WEBFORMS_HANDLER_SUMMARY_SCHEMA_INVALID' }
    if (($handoff.PSObject.Properties['nodes'] -and $handoff.nodes -isnot [pscustomobject]) -or
        ($handoff.PSObject.Properties['variants'] -and $handoff.variants -isnot [array])) { throw 'WEBFORMS_HANDLER_SUMMARY_METADATA_INVALID' }
    if ($handoff.header -isnot [pscustomobject] -or $handoff.header.gaps -isnot [array] -or
        $receipt.truncated -isnot [bool]) { throw 'WEBFORMS_HANDLER_SUMMARY_METADATA_INVALID' }
    foreach ($gap in $handoff.header.gaps) {
        if ($gap -isnot [pscustomobject] -or $gap.gapKind -isnot [string]) { throw 'WEBFORMS_HANDLER_SUMMARY_METADATA_INVALID' }
        foreach ($field in @('reason','filePath','startLine','nodeId','ruleId','evidenceTier','message')) {
            if (!$gap.PSObject.Properties[$field]) { throw 'WEBFORMS_HANDLER_SUMMARY_METADATA_INVALID' }
        }
    }
    $gaps = @($handoff.header.gaps | Where-Object { $_.gapKind -ceq 'TruncatedByLimit' })
    Write-Output "Truncation: $($receipt.truncated); retained cutoff gaps: $($gaps.Count). Counts are retained gap records, not missing paths."
    function Short-Text($Value) {
        if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return 'unavailable' }
        $valueText = [regex]::Replace([string]$Value, '[\p{Cc}\p{Cf}]', ' ')
        if ($valueText.Length -gt 256) { return $valueText.Substring(0,256) + '…' }
        return $valueText
    }
    $summary = if ($handoff.header.PSObject.Properties['summary']) { $handoff.header.summary } else { $null }
    $query = if ($handoff.header.PSObject.Properties['query']) { $handoff.header.query } else { $null }
    $work = if ($null -ne $summary -and $summary.PSObject.Properties['traversalWorkUnits']) { $summary.traversalWorkUnits } else { $null }
    $workLimit = if ($null -ne $query -and $query.PSObject.Properties['maxTraversalWork']) { $query.maxTraversalWork } else { $null }
    $depthLimit = if ($null -ne $query -and $query.PSObject.Properties['maxDepth']) { $query.maxDepth } else { $null }
    Write-Output "Traversal depth: configuredLimit=$(Short-Text $depthLimit). Deeper bounded queries are not complete-coverage proof."
    Write-Output "Traversal work: measured=$(Short-Text $work); configuredLimit=$(Short-Text $workLimit). Work count alone does not identify the cutoff reason."
    foreach ($reason in @('work', 'depth', 'cycle', 'path', 'frontier')) {
        $count = @($gaps | Where-Object { $_.reason -ceq $reason }).Count
        Write-Output "  cutoffReason=${reason}; retainedGaps=$count"
    }
    if (@($gaps | Where-Object { $_.reason -ceq 'work' }).Count -eq 0) {
        Write-Output 'No work-limit cutoff is recorded in this handoff. Truncation alone is not evidence of work-budget exhaustion.'
    }
    $groups = @($gaps | Group-Object {
        $reason = $_.reason
        $cause = if ($_.PSObject.Properties['cutoffCause']) { $_.cutoffCause } else { $null }
        switch ($cause) {
            'candidate-identity-roundtrip' { 'identity round trips (not application recursion)' }
            'terminal-route-not-found-within-depth-bound' { 'bounded terminal-route uncertainty' }
            default { $reason }
        }
    } | Sort-Object Name)
    foreach ($group in @($groups | Select-Object -First 10)) {
        Write-Output "  $(Short-Text $group.Name): $($group.Count) retained gaps"
        $causes = @($group.Group | Group-Object { if ($_.PSObject.Properties['cutoffCause']) { $_.cutoffCause } else { 'unavailable-legacy-report' } } | Sort-Object Name)
        foreach ($cause in @($causes | Select-Object -First 10)) {
            Write-Output "    cause=$(Short-Text $cause.Name); retainedGaps=$($cause.Count)"
        }
        foreach ($gap in @($group.Group | Select-Object -First 3)) {
            Write-Output "    location=$(Short-Text $gap.filePath); line=$(Short-Text $gap.startLine); node=$(Short-Text $gap.nodeId)"
            Write-Output "    rule=$(Short-Text $gap.ruleId); tier=$(Short-Text $gap.evidenceTier); detail=$(Short-Text $gap.message)"
        }
        foreach ($gap in @($group.Group | Where-Object { $_.PSObject.Properties['cutoffWitness'] -and $null -ne $_.cutoffWitness } | Select-Object -First 3)) {
            $witness = $gap.cutoffWitness
            Write-Output "    Root-to-cutoff sample; prefixTruncated=$($witness.prefixTruncated); lastEdgeNotTraversed=$($witness.lastEdgeNotTraversed)"
            foreach ($edge in @($witness.edges | Select-Object -First 64)) {
                Write-Output "      $(Short-Text $edge.fromNodeId) -> $(Short-Text $edge.toNodeId)"
                Write-Output "      kind=$(Short-Text $edge.edgeKind); rule=$(Short-Text $edge.ruleId); tier=$(Short-Text $edge.evidenceTier); location=$(Short-Text $edge.filePath):$(Short-Text $edge.startLine)"
            }
        }
    }
    Write-Output 'Display bounded to 10 reasons, 3 examples each, 256 characters per field. Unavailable means not retained; no stopped branch is inferred.'
    Write-Output 'Database command bindings: retained endpoint evidence only; no SQL execution or resolved SQL text claim.'
    if (!$handoff.PSObject.Properties['nodes'] -or !$handoff.PSObject.Properties['variants']) {
        Write-Output 'Endpoint detail unavailable in this handoff.'
        return
    }
    $endpoints = @{}
    foreach ($variant in $handoff.variants) {
        $references = @($variant.nodeReferences)
        if ($references.Count -eq 0) { continue }
        $reference = [string]$references[-1]
        $property = $handoff.nodes.PSObject.Properties[$reference]
        if ($null -eq $property) { throw 'WEBFORMS_HANDLER_ENDPOINT_REFERENCE_INVALID' }
        $node = $property.Value
        if ($node.surfaceKind -ne 'database-api') { continue }
        if (!$endpoints.ContainsKey($reference)) { $endpoints[$reference] = @{ Node=$node; Count=0 } }
        $endpoints[$reference].Count++
    }
    Write-Output "Distinct retained endpoint records: $($endpoints.Count); displaying at most 20."
    foreach ($reference in @($endpoints.Keys | Sort-Object | Select-Object -First 20)) {
        $entry = $endpoints[$reference]
        $node = $entry.Node
        Write-Output "  endpoint=$(Short-Text $node.surfaceName); variants=$($entry.Count); node=$(Short-Text $node.nodeId)"
        if (!$node.PSObject.Properties['commandBinding'] -or $null -eq $node.commandBinding) {
            Write-Output '    command binding unavailable: endpoint retained, configuration/value evidence not retained.'
            continue
        }
        $binding = $node.commandBinding
        foreach ($field in @('commandTextFromPath', 'commandTypeFromPath')) {
            if (!$binding.PSObject.Properties[$field] -or $null -eq $binding.$field) {
                Write-Output "    ${field}: unavailable (not evidence of a resolved value)"
                continue
            }
            $value = $binding.$field
            Write-Output "    ${field}: state=$(Short-Text $value.state); originKind=$(Short-Text $value.origin.kind); rule=$(Short-Text $value.ruleId); tier=$(Short-Text $value.evidenceTier)"
            Write-Output "      argument steps=$(@($value.steps).Count); retained gaps=$(@($value.gaps).Count)"
            $steps = @($value.steps)
            if ($steps.Count -gt 0) {
                $last = $steps[-1]
                Write-Output "      last retained argument hop: caller=$(Short-Text $last.callerMethodFactId); target=$(Short-Text $last.targetMethodFactId); slot=$(Short-Text $last.targetArgumentSlot); opcode=$(Short-Text $last.opcode)"
            }
            $returnCount = if ($value.PSObject.Properties['returnSteps']) { @($value.returnSteps).Count } else { 0 }
            Write-Output "      retained return steps=$returnCount"
            foreach ($gap in @($value.gaps | Select-Object -First 8)) { Write-Output "      gap=$(Short-Text $gap)" }
        }
    }
    Write-Output 'Command values and SQL literals are not printed. Hash-only constants do not identify SQL text or a stored procedure.'
}
