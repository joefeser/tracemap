$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wsqlroute.ps1'
$folder = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-sql-route-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($folder)
function Save($obj, $path) { [IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject $obj -Depth 50)) }
try {
    $inputFile = Join-Path $folder 'compiled-paths.handoff.local.json'
    $root = @{ nodeId = 'root'; displayName = '<script>private()</script>'; nodeKind = 'Method' }
    $fill = @{ nodeId = 'fill'; displayName = 'Fill'; surfaceKind = 'database-api'; surfaceName = 'DbDataAdapter.Fill'; ruleId = 'test.fill'; evidenceTier = 'Tier3SyntaxOrTextual' }
    $bridge = @{ edgeKind = 'projectless-vb-receiver-bridge' }
    Save @{ schemaVersion = 'webforms-compiled-grouped-handoff.v1'; header = @{ query = @{}; gaps = @(@{ gapKind = 'ParameterEvidenceUnavailable' }) }; nodes = @{ r = $root; f = $fill }; edges = @{ e = $bridge }; variants = @(@{ nodeReferences = @('r','f'); edgeReferences = @('e') }) } $inputFile
    $before = (Get-FileHash $inputFile).Hash
    $result = @(& $helper -Report $folder)
    if ($result -notcontains 'sqlRoute.exactGroups=1;variants=1;databaseSurfaceOccurrences=1;sqlSurfaceOccurrences=0') { throw 'Grouped counts incorrect' }
    $output = Join-Path $folder 'handler-sql-evidence.local.html'
    $html = [IO.File]::ReadAllText($output)
    foreach ($expected in @('No retained SQL query/persistence surface','projectless-vb-receiver-bridge','test.fill','ParameterEvidenceUnavailable','Bounded input SHA-256','&lt;script&gt;private()&lt;/script&gt;')) {
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
    Save @{ schemaVersion = 'webforms-compiled-grouped-handoff.v1'; header = @{}; nodes = @{}; edges = @{}; variants = @(@{ nodeReferences = @('missing'); edgeReferences = @() }) } $inputFile
    try { & $helper -Report $folder -OutputPath (Join-Path $folder 'bad.html'); throw 'Missing ref accepted' } catch { if ($_.Exception.Message -ne 'WEBFORMS_SQL_ROUTE_REFERENCE_MISSING') { throw } }
    if (Test-Path (Join-Path $folder 'bad.html')) { throw 'Invalid output written' }
    Write-Output 'webFormsSqlRoutePublicTests=passed'
} finally { [IO.Directory]::Delete($folder, $true) }
