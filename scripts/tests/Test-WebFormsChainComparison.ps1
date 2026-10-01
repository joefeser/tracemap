$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wcompare.ps1'
$folder = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-compare-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($folder)
function Node([string]$symbol, [string]$scan = 'scan-a') {
    return @{ nodeKind = 'Method'; sourceIndexId = 'source'; scanId = $scan; commitSha = 'abc'; symbolId = $symbol; nodeId = $symbol; displayName = $symbol }
}
function Save($object, $path) { [IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject $object -Depth 30)) }
try {
    $old = Join-Path $folder 'old.json'
    $current = Join-Path $folder 'compiled-paths.handoff.local.json'
    $output = Join-Path $folder 'diff.html'
    $a = Node 'A()'; $b = Node 'B()'; $c = Node '<script>private</script>'
    Save @{ query = @{ maxTraversalWork = 100000 }; paths = @(@{ nodes = @($a) }, @{ nodes = @($a) }, @{ nodes = @($b) }) } $old
    Save @{ schemaVersion = 'webforms-compiled-grouped-handoff.v1'; header = @{ query = @{ maxTraversalWork = 2000000 } }; nodes = @{ a = $a; c = $c }; variants = @(@{ nodeReferences = @('a') }, @{ nodeReferences = @('c') }) } $current
    $oldHash = (Get-FileHash $old).Hash; $currentHash = (Get-FileHash $current).Hash
    $result = @(& $helper -Historical $old -Current $folder -OutputPath $output)
    if ($result -notcontains 'compare.historicalChains=2;historicalVariants=3') { throw 'Historical counts wrong' }
    if ($result -notcontains 'compare.currentChains=2;currentVariants=2') { throw 'Current counts wrong' }
    if ($result -notcontains 'compare.sharedExact=1;historicalOnly=1;currentOnly=1;variantCountDifferences=1;symbolSequenceMatches=1') { throw 'Comparison wrong' }
    if ($result -notcontains 'compare.symbolHistorical=2;symbolCurrent=2;symbolShared=1;symbolHistoricalOnly=1;symbolCurrentOnly=1;symbolVariantCountDifferences=1') { throw 'Symbol comparison wrong' }
    $html = [IO.File]::ReadAllText($output)
    if ($html.Contains('<script>private</script>') -or !$html.Contains('&lt;script&gt;private&lt;/script&gt;')) { throw 'HTML escaping failed' }
    if (!$html.Contains('100000') -or !$html.Contains('2000000') -or !$html.Contains('Bounded input SHA-256')) { throw 'Context missing' }
    if (($result -join "`n").Contains('private')) { throw 'Private symbols printed' }
    if (!$html.Contains('Historical-only symbol sequences (hints)') -or !$html.Contains('2 historical / 1 current')) { throw 'Symbol details missing' }
    if ((Get-FileHash $old).Hash -ne $oldHash -or (Get-FileHash $current).Hash -ne $currentHash) { throw 'Input changed' }
    try { & $helper -Historical $old -Current $current -OutputPath $output; throw 'Overwrite accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_COMPARE_OUTPUT_EXISTS') { throw } }
    # Same symbols across a different scan must not become exact identity matches.
    Save @{ query = @{}; paths = @(@{ nodes = @((Node 'A()' 'scan-b')) }) } $current
    $result = @(& $helper -Historical $old -Current $current -OutputPath (Join-Path $folder 'scan.html'))
    if ($result -notcontains 'compare.sharedExact=0;historicalOnly=2;currentOnly=1;variantCountDifferences=0;symbolSequenceMatches=1') { throw 'Scan identity collapsed' }
    if ($result -notcontains 'compare.symbolHistorical=2;symbolCurrent=1;symbolShared=1;symbolHistoricalOnly=1;symbolCurrentOnly=0;symbolVariantCountDifferences=1') { throw 'Cross-scan hints wrong' }
    # Multiple exact identities for a single symbol sequence count as one hint,
    # retaining all variants rather than treating provenance as missing routes.
    Save @{ query = @{}; paths = @(@{ nodes = @($a) }, @{ nodes = @((Node 'A()' 'scan-b')) }) } $current
    $result = @(& $helper -Historical $old -Current $current)
    if ($result -notcontains 'compare.symbolHistorical=2;symbolCurrent=1;symbolShared=1;symbolHistoricalOnly=1;symbolCurrentOnly=0;symbolVariantCountDifferences=0') { throw 'Symbol aggregation wrong' }
    $default = Join-Path $folder 'chain-comparison.local.html'
    $defaultHash = (Get-FileHash $default).Hash
    $null = & $helper -Historical $old -Current $current
    if (!(Test-Path (Join-Path $folder 'chain-comparison-2.local.html')) -or (Get-FileHash $default).Hash -ne $defaultHash) { throw 'Default repeat overwrote comparison' }
    # A saved mixed report can contain a historical-only symbol sequence.
    $mixed = Join-Path $folder 'mixed.json'
    Save @{ query = @{ traversalScope = 'mixed' }; paths = @(@{ nodes = @((Node 'B()' 'scan-b')); edges = @(@{ edgeKind = 'calls'; ruleId = 'test.calls'; evidenceTier = 'Tier3SyntaxOrTextual' }) }) } $mixed
    Save @{ query = @{}; paths = @(@{ nodes = @($a) }, @{ nodes = @($a) }, @{ nodes = @($b); edges = @(@{ edgeKind = 'compiled-source-identity'; ruleId = 'test.bridge'; evidenceTier = 'Tier2Structural' }) }) } $old
    $mixedOutput = Join-Path $folder 'mixed.html'
    $result = @(& $helper -Historical $old -Current $current -Mixed $mixed -OutputPath $mixedOutput)
    if ($result -notcontains 'compare.mixedReportPresent=True;historicalOnlySymbolSequencesFoundInMixed=1') { throw 'Mixed overlap missing' }
    $html = [IO.File]::ReadAllText($mixedOutput)
    if (!$html.Contains('compiled-source-identity') -or !$html.Contains('test.bridge') -or !$html.Contains('Saved mixed-mode variants for this symbol hint: 1')) { throw 'Historical edge evidence missing' }
    if (!$html.Contains((Get-FileHash $mixed).Hash.ToLowerInvariant())) { throw 'Mixed input hash missing' }
    Save @{ schemaVersion = 'webforms-compiled-grouped-handoff.v1'; header = @{}; nodes = @{ b = $b }; edges = @{ e = @{ edgeKind = 'calls'; ruleId = 'test.calls'; evidenceTier = 'Tier3SyntaxOrTextual' } }; variants = @(@{ nodeReferences = @('b'); edgeReferences = @('e') }) } $mixed
    $result = @(& $helper -Historical $old -Current $current -Mixed $mixed -OutputPath (Join-Path $folder 'mixed-grouped.html'))
    if ($result -notcontains 'compare.mixedReportPresent=True;historicalOnlySymbolSequencesFoundInMixed=1') { throw 'Grouped mixed overlap missing' }
    Save @{ query = @{}; paths = @() } $current
    $result = @(& $helper -Historical $old -Current $current -OutputPath (Join-Path $folder 'empty.html'))
    if ($result -notcontains 'compare.currentChains=0;currentVariants=0') { throw 'Empty report rejected' }
    Save @{ schemaVersion = 'webforms-compiled-grouped-handoff.v1'; header = @{}; nodes = @{}; variants = @(@{ nodeReferences = @('missing') }) } $current
    try { & $helper -Historical $old -Current $current -OutputPath (Join-Path $folder 'bad.html'); throw 'Missing reference accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_COMPARE_REFERENCE_MISSING') { throw } }
    if (Test-Path (Join-Path $folder 'bad.html')) { throw 'Invalid output written' }
    Save @{ schemaVersion = 'webforms-compiled-grouped-handoff.v1'; header = @{}; nodes = @{ a = $a }; edges = @{}; variants = @(@{ nodeReferences = @('a'); edgeReferences = @('missing') }) } $current
    try { & $helper -Historical $old -Current $current -OutputPath (Join-Path $folder 'bad-edge.html'); throw 'Missing edge accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_COMPARE_EDGE_REFERENCE_MISSING') { throw } }
    if (Test-Path (Join-Path $folder 'bad-edge.html')) { throw 'Invalid edge output written' }
    $selected = Node 'Synthetic.Selected(Object,EventArgs)'; $other = Node 'Synthetic.Other(Object,EventArgs)'
    $fill = Node 'compiled:DbDataAdapter.Fill'; $fill.surfaceName = 'DbDataAdapter.Fill'
    $execute = Node 'compiled:ExecuteNonQuery'; $execute.surfaceName = 'ExecuteNonQuery'
    Save @{ query = @{ maxPaths = 256 }; paths = @(@{ nodes = @($selected,$fill) }, @{ nodes = @($other,$fill) }, @{ nodes = @($selected,$execute) }) } $old
    Save @{ schemaVersion = 'webforms-compiled-grouped-handoff.v1'; header = @{ query = @{ maxPaths = 256 } }; nodes = @{ a = $selected; f = $fill }; variants = @(@{ nodeReferences = @('a','f') }) } $current
    Save @{ query = @{}; paths = @() } $mixed
    $result = @(& $helper -Historical $old -Current $current -Mixed $mixed -Handler Selected -SurfaceName DbDataAdapter.Fill -OutputPath (Join-Path $folder 'filtered.html'))
    if ($result -notcontains 'compare.historicalChains=1;historicalVariants=1' -or $result -notcontains 'compare.currentChains=1;currentVariants=1' -or
        $result -notcontains 'compare.sharedExact=1;historicalOnly=0;currentOnly=0;variantCountDifferences=0;symbolSequenceMatches=1') { throw 'Scoped comparison included other roots/terminals' }
    $html = [IO.File]::ReadAllText((Join-Path $folder 'filtered.html'))
    if (!$html.Contains('Original variants: 3 historical / 1 current') -or !$html.Contains('still describe each original query')) { throw 'Filter scope missing' }
    $result = @(& $helper -Historical $old -Current $current -Mixed $mixed -Handler Absent -OutputPath (Join-Path $folder 'absent.html'))
    if ($result -notcontains 'compare.historicalChains=0;historicalVariants=0') { throw 'Empty filtered report rejected' }
    $ambiguous = Node 'Synthetic.Selected(Object,EventArgs)' 'scan-other'
    Save @{ query = @{}; paths = @(@{ nodes = @($selected,$fill) }, @{ nodes = @($ambiguous,$fill) }) } $old
    try { & $helper -Historical $old -Current $current -Handler Selected -OutputPath (Join-Path $folder 'ambiguous.html'); throw 'Ambiguous handler accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_COMPARE_HANDLER_AMBIGUOUS') { throw } }
    if (Test-Path (Join-Path $folder 'ambiguous.html')) { throw 'Ambiguous output written' }
    Write-Output 'webFormsChainComparisonPublicTests=passed'
} finally { [IO.Directory]::Delete($folder, $true) }
