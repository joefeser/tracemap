# Read-only retained JSON inspection. No build, scan, subprocess or traversal.
param([string]$OutputRoot = 'C:\work\tracemap-output', [string]$ReportPath = '')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 is required.' }
if (-not $ReportPath) {
    $latest = Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'webforms-depth-comparison-*' |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'depth-8/webforms-modernization.json') -PathType Leaf } |
        Sort-Object Name -Descending | Select-Object -First 1
    if ($null -eq $latest) { throw 'No completed depth-8 report found.' }
    $ReportPath = Join-Path $latest.FullName 'depth-8/webforms-modernization.json'
}
if ((Get-Item -LiteralPath $ReportPath).Length -gt 128MB) { throw 'Report exceeds 128 MiB safety limit.' }
function Optional([System.Text.Json.JsonElement]$Element, [string]$Name) {
    $value = [System.Text.Json.JsonElement]::new()
    if ($Element.TryGetProperty($Name, [ref]$value)) { return $value }
    return [System.Text.Json.JsonElement]::new()
}
function Text([System.Text.Json.JsonElement]$Element, [string]$Name) {
    $value = Optional $Element $Name
    if ($value.ValueKind -eq 'String') { return $value.GetString() }
    return ''
}
function Add-ClosedCount([hashtable]$Counts, [string]$Value, [string[]]$Allowed) {
    $key = if ($Value -cin $Allowed) { $Value } else { 'unavailable' }
    $Counts[$key] = 1 + [int]$Counts[$key]
}
function Format-Counts([hashtable]$Counts) {
    if (-not $Counts.Count) { return 'none' }
    return ((@($Counts.Keys) | Sort-Object | ForEach-Object { "$_`:$($Counts[$_])" }) -join ',')
}
$stream = [IO.File]::OpenRead($ReportPath); $doc = $null
try {
    $doc = [System.Text.Json.JsonDocument]::Parse($stream)
    $root = $doc.RootElement
    $items = $root.GetProperty('surfaceSelection').GetProperty('items')
    $chains = $root.GetProperty('eventChains'); $gaps = $root.GetProperty('gaps')
    if ($items.GetArrayLength() -gt 1000 -or $chains.GetArrayLength() -gt 10000 -or $gaps.GetArrayLength() -gt 10000) { throw 'Retained inspection safety limit exceeded.' }
    $bySurface = @{}; $terminalSurfaces = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($chain in $chains.EnumerateArray()) {
        $surface = Text $chain 'surfaceId'
        if (-not $bySurface.ContainsKey($surface)) { $bySurface[$surface] = [Collections.Generic.List[System.Text.Json.JsonElement]]::new() }
        $bySurface[$surface].Add($chain)
        if (Text $chain 'terminalKind') { [void]$terminalSurfaces.Add($surface) }
    }
    $lines = [Collections.Generic.List[string]]::new()
    $reasonsByNode = @{}
    foreach ($gap in $gaps.EnumerateArray()) {
        if ((Text $gap 'classification') -cne 'TruncatedByLimit' -or (Text $gap 'scopeKind') -cne 'legacy-flow') { continue }
        $node = Text $gap 'scopeId'
        if (-not $node) { continue }
        $reason = Text $gap 'truncationReason'
        if ($reason -cnotin @('depth','cycle','frontier','path','work')) { $reason = 'unavailable' }
        if (-not $reasonsByNode.ContainsKey($node)) { $reasonsByNode[$node] = [Collections.Generic.HashSet[string]]::new() }
        [void]$reasonsByNode[$node].Add($reason)
    }
    $nodeWork = 0
    $focusBindings = @{}
    foreach ($item in $items.EnumerateArray()) {
        $alias = Text $item 'alias'
        if ($alias -cnotmatch '^page-[0-9]+$') { throw 'Unexpected alias; output withheld.' }
        $pageChains = [Collections.Generic.List[System.Text.Json.JsonElement]]::new(); $hasTerminal = $false
        foreach ($surface in $item.GetProperty('surfaceIds').EnumerateArray()) {
            $id = $surface.GetString()
            if ($terminalSurfaces.Contains($id)) { $hasTerminal = $true }
            if ($bySurface.ContainsKey($id)) { $pageChains.AddRange($bySurface[$id]) }
        }
        if ($hasTerminal -and $alias -cnotin @('page-004','page-026')) { continue }
        $counts = @{terminal=0;missing=0;truncated=0;downstream=0;noEdge=0;unknown=0}
        $pageReasons = [Collections.Generic.HashSet[string]]::new()
        $stopStates = @{}; $callStates = @{}; $directReasons = @{}; $handlerOwnedCalls = 0
        foreach ($chain in $pageChains) {
            $pathEvidence = Optional $chain 'pathEvidence'
            if ($pathEvidence.ValueKind -eq 'Array') {
                foreach ($evidence in $pathEvidence.EnumerateArray()) {
                    if (++$nodeWork -gt 200000) { throw 'Node-link inspection budget exceeded; output withheld.' }
                    $node = Text $evidence 'evidenceId'
                    if ((Text $evidence 'evidenceKind') -ceq 'path-node' -and $reasonsByNode.ContainsKey($node)) {
                        foreach ($reason in $reasonsByNode[$node]) { [void]$pageReasons.Add($reason) }
                    }
                }
            }
            if (Text $chain 'terminalKind') { $counts.terminal++; continue }
            if (-not (Text $chain 'handlerFactId')) {
                $counts.missing++
                if ($alias -cin @('page-004','page-026')) {
                    $binding = Text $chain 'bindingFactId'
                    if ($binding) {
                        if (-not $focusBindings.ContainsKey($binding)) { $focusBindings[$binding] = [Collections.Generic.HashSet[string]]::new() }
                        [void]$focusBindings[$binding].Add($alias)
                    }
                }
                continue
            }
            $obs = Optional $chain 'traversalObservation'
            if ($obs.ValueKind -ne 'Object') { $counts.unknown++; continue }
            Add-ClosedCount $stopStates (Text $obs 'stopState') @(
                'bounded-traversal-truncated','supported-terminal-reached','no-observed-downstream-edge',
                'observed-downstream-without-supported-terminal','traversal-observation-unavailable')
            Add-ClosedCount $callStates (Text $obs 'callEvidenceState') @(
                'joined-downstream-edge-observed','handler-owned-call-evidence-unjoined',
                'call-evidence-observation-incomplete','no-handler-owned-call-evidence-retained','unavailable')
            $owned = Optional $obs 'handlerOwnedCallEvidenceCount'
            if ($owned.ValueKind -eq 'Number') { $handlerOwnedCalls += $owned.GetInt32() }
            $truncated = Optional $obs 'truncated'
            if ($truncated.ValueKind -eq 'True') {
                $counts.truncated++
                $retainedReasons = Optional $obs 'truncationReasons'
                $reasonCount = 0
                if ($retainedReasons.ValueKind -eq 'Array') {
                    foreach ($reasonElement in $retainedReasons.EnumerateArray()) {
                        if ($reasonElement.ValueKind -ne 'String') { continue }
                        Add-ClosedCount $directReasons $reasonElement.GetString() @('depth','cycle','frontier','path','work')
                        $reasonCount++
                    }
                }
                if ($reasonCount -eq 0) { $directReasons['not-retained'] = 1 + [int]$directReasons['not-retained'] }
                continue
            }
            $edges = Optional $obs 'downstreamEdgeCount'
            if ($edges.ValueKind -ne 'Number') { $counts.unknown++ }
            elseif ($edges.GetInt32() -gt 0) { $counts.downstream++ }
            else { $counts.noEdge++ }
        }
        $lines.Add("page=$alias|hasTerminal=$hasTerminal|chains=$($pageChains.Count)|terminal=$($counts.terminal)|noRetainedEvents=$($pageChains.Count -eq 0)|handlerUnavailable=$($counts.missing)|truncated=$($counts.truncated)|downstreamNoTerminal=$($counts.downstream)|noEdge=$($counts.noEdge)|observationUnavailable=$($counts.unknown)")
        $lines.Add("page=$alias|stopStates=$(Format-Counts $stopStates)|callEvidenceStates=$(Format-Counts $callStates)|handlerOwnedCallEvidence=$handlerOwnedCalls")
        $lines.Add("page=$alias|directTruncationReasons=$(Format-Counts $directReasons)|basis=per-chain-retained-observation")
        $reasonText = if ($pageReasons.Count) { ($pageReasons | Sort-Object) -join ',' } else { 'not-established' }
        $lines.Add("page=$alias|nodeAssociatedReasons=$reasonText|basis=exact-retained-node-not-proof-of-chain-stop")
    }
    $linked = @{}; $work = 0
    foreach ($gap in $gaps.EnumerateArray()) {
        $support = Optional $gap 'supportingFactIds'
        if ($support.ValueKind -ne 'Array') { continue }
        $aliases = [Collections.Generic.HashSet[string]]::new()
        foreach ($id in $support.EnumerateArray()) {
            if (++$work -gt 200000) { throw 'Gap-link inspection budget exceeded; output withheld.' }
            if ($focusBindings.ContainsKey($id.GetString())) {
                foreach ($alias in $focusBindings[$id.GetString()]) { [void]$aliases.Add($alias) }
            }
        }
        $kind = Text $gap 'classification'
        if ($kind -cnotin @('NoBackendEvidence','TruncatedByLimit','AmbiguousHandler','HandlerUnavailable','UnresolvedHandler','GeneratedFileMissing')) { $kind = 'other-retained-gap' }
        $rule = Text $gap 'ruleId'
        if ($rule -cnotin @('legacy.webforms.event-binding.v1','legacy.webforms.handler-resolution.v1','legacy.webforms.event-flow.v1')) { $rule = 'other-rule' }
        foreach ($alias in $aliases) {
            $key = "$alias|gap=$kind|rule=$rule"
            $linked[$key] = 1 + [int]$linked[$key]
        }
    }
    Write-Host 'targeted-page-triage=read-only|input=selected-retained-report'
    Write-Host "packetPartial=$($root.GetProperty('summary').GetProperty('truncated').GetBoolean())"
    foreach ($line in $lines) { Write-Host $line }
    foreach ($alias in @('page-004','page-026')) {
        $matches = @(@($linked.Keys) | Where-Object { $_.StartsWith("$alias|") } | Sort-Object)
        if (-not $matches.Count) { Write-Host "handlerFocus=$alias|exactBindingLinkedGaps=0|cause=not-established" }
        foreach ($key in $matches) { Write-Host "handlerFocus=$key|count=$($linked[$key])|link=exact-binding-support-not-proof-of-cause" }
    }
    Write-Host 'nonClaim=no-retained-events-is-not-no-runtime-events;missing-evidence-is-not-absence;other-rule-and-gap-values-withheld'
}
finally { if ($null -ne $doc) { $doc.Dispose() }; $stream.Dispose() }
