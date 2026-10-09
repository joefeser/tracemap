#Requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('graph-share-test-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
try {
    $helper = Join-Path $PSScriptRoot '../wgraph-share.ps1'
    $inputFile = Join-Path $temp 'private.json'
    $outputFile = Join-Path $temp 'safe.json'
    $secret = 'CUSTOMER_SECRET'
    $nodes = @(1..150 | ForEach-Object { @{nodeId="$secret-node-$_";symbolId="$secret.Method$_";
        displayName="$secret.Method$_";sourceIndexId="$secret-source";filePath="C:\$secret\file.vb";
        ruleId="$secret-rule";evidenceTier='Tier3SyntaxOrTextual';nodeKind='Method'} })
    $edges = @(1..149 | ForEach-Object { @{edgeId="$secret-edge-$_";fromNodeId="$secret-node-$_";
        toNodeId=($secret + '-node-' + ($_+1));edgeKind='compiled-il-call';ruleId="$secret-rule";
        evidenceTier='Tier3SyntaxOrTextual';supportingFactIds=@("$secret-fact")} })
    $calls = @(@{callerNodeId="$secret-node-1";factId="$secret-fact";bodyFactId="$secret-body";
        offset="$secret-offset";opcode='callvirt';encodedTarget="$secret-target";ruleId="$secret-rule";
        evidenceTier='Tier3SyntaxOrTextual';state='no-admitted-method-target-edge';
        gapReasons=@('admitted-memberref-target-not-unique',"$secret-free-text")})
    @{schemaVersion='retained-method-graph.v1';indexSha256="$secret-hash";
      graph=@{nodes=$nodes;edges=$edges;calls=$calls;cutoffs=@("$secret-cutoff");roots=@("$secret-node-1")}} |
        ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $inputFile
    $before = (Get-FileHash $inputFile).Hash
    & $helper $temp -GraphPath $inputFile -Method "$secret.Method1" -OutputPath $outputFile | Out-Null
    # Broad substring above deliberately matches too many; use an exact terminal suffix below.
} catch {
    if ($_.Exception.Message -notlike 'GRAPH_SHARE_FAILED:*') { throw }
}
try {
    $nodes[0].symbolId = "$secret.Focus()"; $nodes[0].displayName = "$secret.Focus()"
    @{schemaVersion='retained-method-graph.v1';indexSha256="$secret-hash";
      graph=@{nodes=$nodes;edges=$edges;calls=$calls;cutoffs=@("$secret-cutoff");roots=@("$secret-node-1")}} |
        ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $inputFile
    $before = (Get-FileHash $inputFile).Hash
    & $helper $temp -GraphPath $inputFile -Method 'Focus()' -OutputPath $outputFile | Out-Null
    $raw = Get-Content -LiteralPath $outputFile -Raw
    if ($raw.Contains($secret) -or $raw.Contains('C:\')) { throw 'Private marker leaked.' }
    $result = $raw | ConvertFrom-Json -Depth 24
    if (!$result.graph.sliceLimited -or !$result.graph.sourceHadCutoffs -or $result.graph.nodes.Count -ne 4) { throw "Bounds not retained: limited=$($result.graph.sliceLimited), cutoffs=$($result.graph.sourceHadCutoffs), nodes=$($result.graph.nodes.Count)" }
    if ($result.graph.calls[0].fact -cne $result.graph.edges[0].facts[0]) { throw 'Fact aliases lost identity.' }
    if ($result.graph.calls[0].reasons[1] -cne 'other-or-unavailable') { throw 'Unknown reason not suppressed.' }
    $projection = [Text.Encoding]::UTF8.GetBytes(($result.graph | ConvertTo-Json -Depth 20 -Compress))
    $digest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($projection)).ToLowerInvariant()
    if ($result.boundedInputSha256 -cne $digest) { throw 'Projected-input hash mismatch.' }
    if ($result.generatorSha256 -ine (Get-FileHash $helper).Hash) { throw 'Generator hash mismatch.' }
    if ((Get-FileHash $inputFile).Hash -cne $before) { throw 'Private graph changed.' }
    $again = Join-Path $temp 'again.json'
    & $helper $temp -GraphPath $inputFile -Method 'Focus()' -OutputPath $again | Out-Null
    if ((Get-FileHash $outputFile).Hash -cne (Get-FileHash $again).Hash) { throw 'Nondeterministic export.' }
    $renamedInput = Join-Path $temp 'renamed-private.json'
    (Get-Content -LiteralPath $inputFile -Raw).Replace($secret,'DIFFERENT_PRIVATE_NAME') | Set-Content -LiteralPath $renamedInput
    $renamedOutput = Join-Path $temp 'renamed-safe.json'
    & $helper $temp -GraphPath $renamedInput -Method 'Focus()' -OutputPath $renamedOutput | Out-Null
    if ((Get-FileHash $outputFile).Hash -cne (Get-FileHash $renamedOutput).Hash) { throw 'Private names influenced shared hashes or aliases.' }
    $failed = $false
    try { & $helper $temp -GraphPath $inputFile -Method 'Focus()' -OutputPath $again | Out-Null } catch { $failed = $true }
    if (!$failed) { throw 'Overwrote an existing export.' }
    $reportFolder = Join-Path $temp ('handler-requery-sample-' + ('a' * 32))
    [void][IO.Directory]::CreateDirectory($reportFolder)
    Copy-Item -LiteralPath $inputFile -Destination (Join-Path $reportFolder 'method-graph.local.json')
    $selectedOutput = Join-Path $temp 'selected.json'
    & $helper $temp -Method 'Focus()' -OutputPath $selectedOutput | Out-Null
    if ((Get-FileHash $outputFile).Hash -cne (Get-FileHash $selectedOutput).Hash) { throw 'Latest graph selection changed projection.' }
    Write-Output 'Aliased graph exporter tests passed.'
} finally { Remove-Item -LiteralPath $temp -Recurse -Force }
