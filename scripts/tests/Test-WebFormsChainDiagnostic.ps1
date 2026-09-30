$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wchain.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-chain-test-' + [guid]::NewGuid())
[void][IO.Directory]::CreateDirectory($root)
try {
    $packet = @{
        schemaVersion='webforms-compiled-grouped-handoff.v1'; visibility='local-only'
        chains=@(@{variantIndexes=@(0)})
        variants=@(@{path=@{pathId='path:1'; classification='Candidate'; confidence='Reduced'}; nodeReferences=@('n1','n2'); edgeReferences=@('e1')})
        nodes=@{
            n1=@{nodeId='one'; displayName='redacted-hash:one'; symbolId='Method<Unsafe>'; sourceIndexId='s1'; scanId='scan'; commitSha='commit'}
            n2=@{nodeId='two'; displayName='Target()'; symbolId='Target()'; sourceIndexId='s1'; scanId='scan'; commitSha='commit'}
        }
        edges=@{e1=@{fromNodeId='one'; toNodeId='two'; edgeKind='calls'; ruleId='synthetic.calls.v1'; evidenceTier='Tier2Structural'; supportingFactIds=@('fact:one')}}
    }
    $inputPath = Join-Path $root 'compiled-paths.handoff.local.json'
    [IO.File]::WriteAllText($inputPath, ($packet | ConvertTo-Json -Depth 30))
    $before = (Get-FileHash $inputPath).Hash
    $result = @(& $helper -ReportFolder $root -Chain 1)
    $html = [IO.File]::ReadAllText((Join-Path $root 'chain-1.diagnostic.local.html'))
    if ($html -notmatch 'Method&lt;Unsafe&gt;' -or $html -match 'Method<Unsafe>' -or $html -notmatch 'synthetic.calls.v1' -or $html -notmatch 'fact:one') { throw 'Evidence or HTML escaping missing' }
    if ($html -notmatch 'Generator SHA-256' -or $html -notmatch 'Bounded input SHA-256' -or $html -notmatch 'unadmitted') { throw 'Provenance/limitations missing' }
    if ($before -ne (Get-FileHash $inputPath).Hash) { throw 'Input changed' }
    try { & $helper -ReportFolder $root -Chain 1; throw 'Expected existing output rejection' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_CHAIN_OUTPUT_EXISTS;originals-preserved') { throw } }
    try { & $helper -ReportFolder $root -Chain 2; throw 'Expected invalid selection rejection' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_CHAIN_SELECTION_INVALID') { throw } }
    $packet.edges.e1.toNodeId = 'missing'
    [IO.File]::WriteAllText($inputPath, ($packet | ConvertTo-Json -Depth 30))
    Remove-Item (Join-Path $root 'chain-1.diagnostic.local.html')
    try { & $helper -ReportFolder $root -Chain 1; throw 'Expected invalid endpoint rejection' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_CHAIN_ENDPOINT_AMBIGUOUS') { throw } }
    if (Test-Path (Join-Path $root 'chain-1.diagnostic.local.html')) { throw 'Failure wrote output' }
    'webFormsChainDiagnosticPublicTests=passed'
} finally { Remove-Item -LiteralPath $root -Recurse -Force }
