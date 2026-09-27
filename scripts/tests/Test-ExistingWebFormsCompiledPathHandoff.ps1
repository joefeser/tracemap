[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$generator = Join-Path $TraceMapRoot 'scripts/New-ExistingWebFormsCompiledPathHandoff.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-compiled-handoff-test-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory((Join-Path $root 'scan'))
try {
    $commit = 'a' * 40
    $manifest = [ordered]@{
        commitSha = $commit; scanId = 'scan-public'
        compiledInputProvenance = [ordered]@{ outcomes = @([ordered]@{
            outcome = 'admitted'; safeLocator = 'compiled:public'; rawFileSha256 = ('b' * 64)
            assemblyIdentity = 'PublicProof'; provenanceState = 'bound'; gapKinds = @()
        }) }
    }
    $receipt = [ordered]@{ sourceCommitSha = $commit }
    $nodes = @(
        [ordered]@{ nodeId = 'source'; displayName = '<Names_Init>'; scanId = 'scan-public'; commitSha = $commit; surfaceKind = $null },
        [ordered]@{ nodeId = 'compiled'; displayName = 'LookupPage.Names_Init'; scanId = 'scan-public'; commitSha = $commit; surfaceKind = $null },
        [ordered]@{ nodeId = 'query'; displayName = 'sql-query'; scanId = 'scan-public'; commitSha = $commit; surfaceKind = 'sql-query' }
    )
    $edges = @(
        [ordered]@{ edgeKind = 'projectless-publish-method-candidate'; fromNodeId = 'source'; toNodeId = 'compiled'; ruleId = 'combined.paths.projectless-publish-candidate.v1'; evidenceTier = 'Tier3SyntaxOrTextual'; filePath = 'Pages/Lookup.aspx.vb'; startLine = 4; endLine = 4; supportingFactIds = @('fact-source','fact-method') },
        [ordered]@{ edgeKind = 'compiled-il-call'; fromNodeId = 'compiled'; toNodeId = 'query'; ruleId = 'dotnet.compiled.il-call.v1'; evidenceTier = 'Tier2Structural'; filePath = 'compiled:public'; startLine = 1; endLine = 1; supportingFactIds = @('fact-il') }
    )
    $report = [ordered]@{
        version = '1.0'; reportCoverage = 'ReducedCoverage'; coverageWarnings = @('public test warning')
        sources = @([ordered]@{ scanId = 'scan-public'; commitSha = $commit })
        query = [ordered]@{ fromSymbol = 'Names_Init'; toSurface = 'sql-query'; maxDepth = 20; maxPaths = 256 }
        summary = [ordered]@{ pathCount = 1; gapCount = 0; truncated = $false }
        paths = @([ordered]@{ pathId = 'path-public'; classification = 'NeedsReviewStaticPath'; nodes = $nodes; edges = $edges; supportingFactIds = @('fact-source','fact-method','fact-il'); notes = @() })
        gaps = @()
    }
    [IO.File]::WriteAllText((Join-Path $root 'scan/scan-manifest.json'), (($manifest | ConvertTo-Json -Depth 15) + "`n"))
    [IO.File]::WriteAllText((Join-Path $root 'publish-receipt.local.json'), (($receipt | ConvertTo-Json -Depth 5) + "`n"))
    [IO.File]::WriteAllText((Join-Path $root 'handler-paths.json'), (($report | ConvertTo-Json -Depth 20) + "`n"))
    [IO.File]::WriteAllText((Join-Path $root 'combined.sqlite'), 'public-test-index')
    $lines = @(& $generator -ProofRoot $root)
    $json = [IO.File]::ReadAllText((Join-Path $root 'compiled-path-review/handler.handoff.local.json')) | ConvertFrom-Json -Depth 40
    $html = [IO.File]::ReadAllText((Join-Path $root 'compiled-path-review/handler.local.html'))
    if ($lines -cnotcontains 'compiledPathReviewStatus=valid' -or
        $lines -cnotcontains 'compiledPathReviewReviewOnly=1' -or
        $json.schemaVersion -ne 'webforms-compiled-path-handoff.v1' -or
        $json.provenance.generatorSha256 -ne (Get-FileHash -LiteralPath $generator -Algorithm SHA256).Hash.ToLowerInvariant() -or
        $json.provenance.boundedInputSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $json.paths[0].hops.Count -ne 2 -or
        $json.paths[0].claim -ne 'review-only-static-path' -or
        $json.paths[0].hops[0].ruleId -ne 'combined.paths.projectless-publish-candidate.v1' -or
        $json.assemblies[0].rawFileSha256 -ne ('b' * 64) -or
        !$html.Contains('&lt;Names_Init&gt;', [StringComparison]::Ordinal) -or
        $html.Contains('<Names_Init>', [StringComparison]::Ordinal) -or
        !$html.Contains('not runtime execution', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'WEBFORMS_COMPILED_HANDOFF_PUBLIC_PROJECTION_INVALID'
    }
    $report.paths[0].edges[0].toNodeId = 'wrong'
    [IO.File]::WriteAllText((Join-Path $root 'handler-paths.json'), (($report | ConvertTo-Json -Depth 20) + "`n"))
    $captured = $null
    try { & $generator -ProofRoot $root -OutputDirectory (Join-Path $root 'invalid') *> $null }
    catch { $captured = $_.Exception.Message }
    if ($captured -ne 'WEBFORMS_COMPILED_HANDOFF_PATH_INVALID') {
        throw "WEBFORMS_COMPILED_HANDOFF_INVALID_PATH_ACCEPTED:$captured"
    }
    Write-Output 'webFormsCompiledHandoffPublicTests=passed'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
