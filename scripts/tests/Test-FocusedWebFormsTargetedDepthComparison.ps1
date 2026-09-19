$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$subject = Join-Path $scripts 'Compare-FocusedWebFormsTargetedDepthDiagnostic.ps1'
$repo = Split-Path -Parent $scripts
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-targeted-depth-compare-' + [Guid]::NewGuid().ToString('N'))

function Write-Artifact([string]$Root, [string]$RelativePath, [object]$Value) {
    $path = Join-Path $Root $RelativePath
    [IO.Directory]::CreateDirectory((Split-Path -Parent $path)) | Out-Null
    $text = if ($Value -is [string]) { $Value } else { ($Value | ConvertTo-Json -Depth 20) + "`n" }
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
    $file = Get-Item -LiteralPath $path
    return [ordered]@{
        path = $RelativePath.Replace('\', '/')
        bytes = [long]$file.Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
function New-Chain([string]$SurfaceId, [bool]$DepthTruncated, [int]$TerminalCount = 0) {
    return [ordered]@{
        surfaceId = $SurfaceId
        handlerFactId = "handler-$SurfaceId"
        traversalObservation = [ordered]@{
            truncationReasons = if ($DepthTruncated) { @('depth') } else { @() }
            terminalReachabilityComplete = $true
            distinctReachableTerminalCount = $TerminalCount
            minimumTerminalDistance = if ($TerminalCount -gt 0) { 11 } else { $null }
            terminalReachabilityLimitReasons = @()
            pathEnumerationTruncated = $DepthTruncated
            pathEnumerationTruncationReasons = if ($DepthTruncated) { @('depth') } else { @() }
        }
    }
}
function New-HandoffChain([bool]$DepthTruncated) {
    return [ordered]@{
        traversalTruncationReasons = if ($DepthTruncated) { @('depth') } else { @() }
    }
}
function New-Boundary([string]$Suffix, [string]$SurfaceId = '') {
    $value = [ordered]@{
        boundaryKind = 'sql-query'
        boundaryTargetId = "target-$Suffix"
        terminalEvidenceId = "evidence-$Suffix"
    }
    if ($SurfaceId) { $value.surfaceId = $SurfaceId }
    return $value
}

try {
    [IO.Directory]::CreateDirectory($temp) | Out-Null
    $source = [ordered]@{ sourceId = 'source'; scanId = 'scan-fixture'; commitSha = ('a' * 40) }
    $packet = Write-Artifact $temp 'packet/webforms-modernization.json' '{}'
    $manifest = Write-Artifact $temp 'evidence-docs/manifest.json' '{}'
    $recipes = Write-Artifact $temp 'evidence-docs/query-recipes.json' '{}'
    $chunks = Write-Artifact $temp 'evidence-docs/chunks.jsonl' "{}`n"
    $application = Write-Artifact $temp 'workbench/application-handoff.json' ([ordered]@{
        schemaVersion = 'webforms-application-handoff.v1'
    })
    $page002 = Write-Artifact $temp 'workbench/page-002.handoff.json' ([ordered]@{
        schemaVersion = 'webforms-application-page-handoff.v1'
        packet = [ordered]@{ sources = @($source) }
        eventChains = @((New-HandoffChain $true), (New-HandoffChain $false))
        downstreamBoundaries = @((New-Boundary 'a'))
    })
    $page003 = Write-Artifact $temp 'workbench/page-003.handoff.json' ([ordered]@{
        schemaVersion = 'webforms-application-page-handoff.v1'
        packet = [ordered]@{ sources = @($source) }
        eventChains = @((New-HandoffChain $true))
        downstreamBoundaries = @()
    })
    $receipt = [ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        run = [ordered]@{ state = 'completed' }
        stages = [ordered]@{
            packet = [ordered]@{ state = 'completed'; artifacts = @($packet) }
            evidenceDocs = [ordered]@{ state = 'completed'; artifacts = @($manifest, $recipes, $chunks) }
            workbench = [ordered]@{ state = 'completed'; artifacts = @($application, $page002, $page003) }
        }
    }
    [IO.File]::WriteAllText((Join-Path $temp 'run-receipt.json'), (($receipt | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))

    $diagnostic = Join-Path $temp 'diagnostics/targeted-depth-10-page-002-page-003-20260919T000000Z'
    [IO.Directory]::CreateDirectory($diagnostic) | Out-Null
    $diagnosticPacket = [ordered]@{
        schemaVersion = 'webforms-modernization-packet.v1'
        sources = @($source)
        surfaceSelection = [ordered]@{ items = @(
            [ordered]@{ alias = 'page-001'; status = 'matched'; surfaceIds = @('surface-002') }
            [ordered]@{ alias = 'page-002'; status = 'matched'; surfaceIds = @('surface-003') }
        ) }
        eventChains = @(
            (New-Chain 'surface-002' $false 2),
            (New-Chain 'surface-002' $false 2),
            (New-Chain 'surface-003' $true)
        )
        downstreamBoundaries = @(
            (New-Boundary 'a' 'surface-002'),
            (New-Boundary 'b' 'surface-002')
        )
    }
    [IO.File]::WriteAllText((Join-Path $diagnostic 'webforms-modernization.json'), (($diagnosticPacket | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
    $baselinePacket = [ordered]@{
        schemaVersion = 'webforms-modernization-packet.v1'
        sources = @($source)
        surfaceSelection = $diagnosticPacket.surfaceSelection
        eventChains = @(
            (New-Chain 'surface-002' $true 2),
            (New-Chain 'surface-002' $false 2),
            (New-Chain 'surface-003' $true)
        )
        downstreamBoundaries = @((New-Boundary 'a' 'surface-002'))
    }
    [IO.Directory]::CreateDirectory((Join-Path $diagnostic 'baseline-depth-8')) | Out-Null
    [IO.File]::WriteAllText((Join-Path $diagnostic 'baseline-depth-8/webforms-modernization.json'), (($baselinePacket | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))

    $output = @(& $subject -ReviewRoot $temp -TraceMapRoot $repo)
    foreach ($expected in @(
        'page=page-002|depth8Chains=2|depth10Chains=2|depth8DepthTruncated=1|depth10DepthTruncated=0|depth8TerminalEvidence=1|depth10TerminalEvidence=2|addedTerminalEvidence=1|lostTerminalEvidence=0',
        'page=page-003|depth8Chains=1|depth10Chains=1|depth8DepthTruncated=1|depth10DepthTruncated=1|depth8TerminalEvidence=0|depth10TerminalEvidence=0|addedTerminalEvidence=0|lostTerminalEvidence=0',
        'webformsTargetedDepthComparison=completed',
        'depth8DepthTruncated=2',
        'depth10DepthTruncated=1',
        'terminalDelta=added:1|lost:0')) {
        if ($expected -notin $output) { throw "Targeted comparison omitted: $expected" }
    }
    foreach ($expected in @(
        'reachability=page-002|depth8Available=True|depth8Complete=True|depth8DistinctTerminals=2|depth8MinimumDistance=11|depth8LimitReasons=|depth8PathDetailTruncated=True|depth8PathDetailReasons=depth|depth10Complete=True|depth10DistinctTerminals=2',
        'reachability=page-003|depth8Available=True|depth8Complete=True|depth8DistinctTerminals=0')) {
        if (@($output | Where-Object { $_.StartsWith($expected, [StringComparison]::Ordinal) }).Count -ne 1) { throw "Targeted comparison omitted reachability separation: $expected" }
    }
    if (@($output | Where-Object { $_ -match 'target-a|evidence-a|surface-002' }).Count -ne 0) {
        throw 'Targeted comparison disclosed a private evidence identity.'
    }
    Write-Host 'PASS focused Web Forms targeted depth comparison'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
