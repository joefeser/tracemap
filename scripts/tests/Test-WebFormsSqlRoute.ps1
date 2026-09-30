$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wsqlroute.ps1'
$folder = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-sql-route-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($folder)
function Save($obj, $path) { [IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject $obj -Depth 50)) }
try {
    $inputFile = Join-Path $folder 'compiled-paths.handoff.local.json'
    $root = @{ nodeId = 'root'; displayName = '<script>private()</script>'; nodeKind = 'Method' }
    $fill = @{ nodeId = 'fill'; displayName = 'Fill'; surfaceKind = 'database-api'; surfaceName = 'DbDataAdapter.Fill'; ruleId = 'test.fill'; evidenceTier = 'Tier3SyntaxOrTextual'; commandBinding = @{ schema = 'il-command-binding.v1'; commandTextOrigin = @{ kind = 'argument-slot'; identity = '0' }; generatorSha256 = ('a' * 64); boundedInputSha256 = ('b' * 64) } }
    $bridge = @{ edgeKind = 'projectless-vb-receiver-bridge' }
    Save @{ schemaVersion = 'webforms-compiled-grouped-handoff.v1'; header = @{ query = @{}; gaps = @(@{ gapKind = 'ParameterEvidenceUnavailable' }) }; nodes = @{ r = $root; f = $fill }; edges = @{ e = $bridge }; variants = @(@{ nodeReferences = @('r','f'); edgeReferences = @('e') }) } $inputFile
    $before = (Get-FileHash $inputFile).Hash
    $result = @(& $helper -Report $folder)
    if ($result -notcontains 'sqlRoute.exactGroups=1;variants=1;databaseSurfaceOccurrences=1;sqlSurfaceOccurrences=0') { throw 'Grouped counts incorrect' }
    $output = Join-Path $folder 'handler-sql-evidence.local.html'
    $html = [IO.File]::ReadAllText($output)
    foreach ($expected in @('No retained SQL query/persistence surface','projectless-vb-receiver-bridge','test.fill','ParameterEvidenceUnavailable','Bounded input SHA-256','&lt;script&gt;private()&lt;/script&gt;','il-command-binding.v1','commandTextOrigin','argument-slot')) {
        if (!$html.Contains($expected)) { throw "Missing $expected" }
    }
    if ($html.Contains('<script>') -or ($result -join '').Contains('private()')) { throw 'Private text escaping failed' }
    if ((Get-FileHash $inputFile).Hash -ne $before) { throw 'Input changed' }
    $outputHash = (Get-FileHash $output).Hash
    $null = & $helper -Report $folder
    if (!(Test-Path (Join-Path $folder 'handler-sql-evidence-2.local.html')) -or (Get-FileHash $output).Hash -ne $outputHash) { throw 'Repeat overwrote output' }
    try { & $helper -Report $folder -OutputPath $output; throw 'Overwrite accepted' } catch { if ($_.Exception.Message -ne 'WEBFORMS_SQL_ROUTE_OUTPUT_EXISTS') { throw } }
    $sql = @{ nodeId = 'sql'; displayName = 'sql'; surfaceKind = 'sql-query'; operationName = 'SELECT'; tableName = 'Synthetic'; textHash = 'abc'; ruleId = 'test.sql' }
    Save @{ query = @{}; paths = @(@{ nodes = @($root,$sql); edges = @() }) } $inputFile
    $result = @(& $helper -Report $folder -OutputPath (Join-Path $folder 'sql.html'))
    if ($result -notcontains 'sqlRoute.exactGroups=1;variants=1;databaseSurfaceOccurrences=1;sqlSurfaceOccurrences=1') { throw 'SQL surface missing' }
    $html = [IO.File]::ReadAllText((Join-Path $folder 'sql.html'))
    if (!$html.Contains('Synthetic') -or !$html.Contains('Edge evidence absent') -or $html.Contains('No retained SQL query/persistence surface')) { throw 'SQL evidence/gap incorrect' }
    $otherSql = $sql.Clone(); $otherSql.tableName = 'OtherSynthetic'
    Save @{ query = @{}; paths = @(@{ nodes = @($root,$sql); edges = @() }, @{ nodes = @($root,$otherSql); edges = @() }) } $inputFile
    $result = @(& $helper -Report $folder -OutputPath (Join-Path $folder 'different-evidence.html'))
    if ($result -notcontains 'sqlRoute.exactGroups=2;variants=2;databaseSurfaceOccurrences=2;sqlSurfaceOccurrences=2') { throw 'Differing evidence collapsed' }
    # The old helper copied this >32 MiB gap payload into HTML and failed.
    $largeGaps = @(1..6000 | ForEach-Object { @{ gapKind = 'LargeSyntheticGap'; reason = 'repeated'; message = ('<' * 6000); ruleId = 'test.large'; evidenceTier = 'Tier4Unknown' } })
    Save @{ query = @{}; paths = @(@{ nodes = @($root,$fill); edges = @($bridge) }); gaps = $largeGaps } $inputFile
    $largeOutput = Join-Path $folder 'large-gaps.html'
    $result = @(& $helper -Report $folder -OutputPath $largeOutput)
    if ($result -notcontains 'sqlRoute.retainedGaps=6000;gapCategories=1;displayedGapCategories=1;overflowCategoryGaps=0;displayedRouteGroups=1') { throw 'Large gap counts wrong' }
    if ((Get-Item $largeOutput).Length -gt 100000) { throw 'Gap output not bounded' }
    $html = [IO.File]::ReadAllText($largeOutput)
    if (!$html.Contains('display truncated') -or !$html.Contains('6000 gaps')) { throw 'Gap truncation not explicit' }
    Save @{ schemaVersion = 'webforms-compiled-grouped-handoff.v1'; header = @{}; nodes = @{}; edges = @{}; variants = @(@{ nodeReferences = @('missing'); edgeReferences = @() }) } $inputFile
    try { & $helper -Report $folder -OutputPath (Join-Path $folder 'bad.html'); throw 'Missing ref accepted' } catch { if ($_.Exception.Message -ne 'WEBFORMS_SQL_ROUTE_REFERENCE_MISSING') { throw } }
    if (Test-Path (Join-Path $folder 'bad.html')) { throw 'Invalid output written' }
    $selectedRoot = @{ nodeId = 'handler-a'; displayName = 'Synthetic.Selected(Object,EventArgs)'; nodeKind = 'Method' }
    $otherRoot = @{ nodeId = 'handler-b'; displayName = 'Synthetic.Other(Object,EventArgs)'; nodeKind = 'Method' }
    $fill.commandBinding.commandTextFromPath = @{ state = 'constant-on-encoded-call-path'; origin = @{ kind = 'constant-string-hash'; identity = ('a' * 64) } }
    $fill.commandBinding.commandTypeFromPath = @{ state = 'method-local-constant'; origin = @{ kind = 'constant-int32'; identity = '4' } }
    Save @{ query = @{}; paths = @(@{ nodes = @($selectedRoot,$fill); edges = @($bridge) }, @{ nodes = @($otherRoot,$fill); edges = @($bridge) }) } $inputFile
    $result = @(& $helper -Report $folder -Handler Selected -OutputPath (Join-Path $folder 'selected.html'))
    if ($result -notcontains 'sqlRoute.exactGroups=1;variants=1;databaseSurfaceOccurrences=1;sqlSurfaceOccurrences=0' -or
        $result -notcontains 'sqlRoute.commandBindings=1;constantTextCandidates=1;storedProcedureTypeCandidates=1;unresolvedTextCandidates=0;asSupplied=true') { throw 'Handler command counts incorrect' }
    $html = [IO.File]::ReadAllText((Join-Path $folder 'selected.html'))
    foreach ($expected in @('Binding occurrences: 1; constant command-text fingerprints: 1; StoredProcedure type candidates: 1; unresolved command-text candidates: 0', 'do not count distinct procedures', 'encoded-call-path origins', 'without independently validating the binding')) {
        if (!$html.Contains($expected)) { throw "Missing command summary: $expected" }
    }
    if ($html.Contains('Command text, procedure identity and SQL parameter values are unresolved here') -or $html.Contains('No retained command-binding candidates')) { throw 'Resolved candidates contradicted by blanket gap' }
    $unboundFill = $fill.Clone(); $unboundFill.Remove('commandBinding')
    Save @{ query = @{}; paths = @(@{ nodes = @($selectedRoot,$unboundFill); edges = @($bridge) }) } $inputFile
    $null = & $helper -Report $folder -Handler Selected -OutputPath (Join-Path $folder 'unbound.html')
    $html = [IO.File]::ReadAllText((Join-Path $folder 'unbound.html'))
    if (!$html.Contains('No retained command-binding candidates') -or !$html.Contains('Binding occurrences: 0; constant command-text fingerprints: 0')) { throw 'Missing unbound gap' }
    try { & $helper -Report $folder -Handler Missing -OutputPath (Join-Path $folder 'missing.html'); throw 'Missing handler accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_SQL_ROUTE_HANDLER_MISSING_OR_AMBIGUOUS') { throw } }
    $duplicateRoot = $selectedRoot.Clone(); $duplicateRoot.nodeId = 'handler-duplicate'
    Save @{ query = @{}; paths = @(@{ nodes = @($selectedRoot,$fill); edges = @($bridge) }, @{ nodes = @($duplicateRoot,$fill); edges = @($bridge) }) } $inputFile
    try { & $helper -Report $folder -Handler Selected -OutputPath (Join-Path $folder 'ambiguous.html'); throw 'Ambiguous handler accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_SQL_ROUTE_HANDLER_MISSING_OR_AMBIGUOUS') { throw } }
    if ((Test-Path (Join-Path $folder 'missing.html')) -or (Test-Path (Join-Path $folder 'ambiguous.html'))) { throw 'Rejected selection wrote output' }
    Write-Output 'webFormsSqlRoutePublicTests=passed'
} finally { [IO.Directory]::Delete($folder, $true) }
