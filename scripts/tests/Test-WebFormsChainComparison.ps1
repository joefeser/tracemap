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
    $html = [IO.File]::ReadAllText($output)
    if ($html.Contains('<script>private</script>') -or !$html.Contains('&lt;script&gt;private&lt;/script&gt;')) { throw 'HTML escaping failed' }
    if (!$html.Contains('100000') -or !$html.Contains('2000000') -or !$html.Contains('Bounded input SHA-256')) { throw 'Context missing' }
    if (($result -join "`n").Contains('private')) { throw 'Private symbols printed' }
    if ((Get-FileHash $old).Hash -ne $oldHash -or (Get-FileHash $current).Hash -ne $currentHash) { throw 'Input changed' }
    try { & $helper -Historical $old -Current $current -OutputPath $output; throw 'Overwrite accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_COMPARE_OUTPUT_EXISTS') { throw } }
    # Same symbols across a different scan must not become exact identity matches.
    Save @{ query = @{}; paths = @(@{ nodes = @((Node 'A()' 'scan-b')) }) } $current
    $result = @(& $helper -Historical $old -Current $current -OutputPath (Join-Path $folder 'scan.html'))
    if ($result -notcontains 'compare.sharedExact=0;historicalOnly=2;currentOnly=1;variantCountDifferences=0;symbolSequenceMatches=1') { throw 'Scan identity collapsed' }
    Save @{ query = @{}; paths = @() } $current
    $result = @(& $helper -Historical $old -Current $current -OutputPath (Join-Path $folder 'empty.html'))
    if ($result -notcontains 'compare.currentChains=0;currentVariants=0') { throw 'Empty report rejected' }
    Save @{ schemaVersion = 'webforms-compiled-grouped-handoff.v1'; header = @{}; nodes = @{}; variants = @(@{ nodeReferences = @('missing') }) } $current
    try { & $helper -Historical $old -Current $current -OutputPath (Join-Path $folder 'bad.html'); throw 'Missing reference accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_COMPARE_REFERENCE_MISSING') { throw } }
    if (Test-Path (Join-Path $folder 'bad.html')) { throw 'Invalid output written' }
    Write-Output 'webFormsChainComparisonPublicTests=passed'
} finally { [IO.Directory]::Delete($folder, $true) }
