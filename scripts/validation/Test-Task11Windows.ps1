# Synthetic/public failure-path tests; no private checkout or toolchain required.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot 'Invoke-Task11Windows.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('task11-tests-' + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $root)
function Expect-Block([scriptblock]$Action, [string]$Code) {
    try { & $Action; throw "EXPECTED_BLOCK_MISSING:$Code" }
    catch { if (-not $_.Exception.Message.Contains($Code)) { throw } }
}
try {
    . $runner -TraceMapRoot $root -TraceMapCommit ('a' * 40) -OutputRoot (Join-Path $root 'unused')
    [void](New-Item -ItemType Directory -Path (Join-Path $root 'rules'))
    "rules:`n  - id: synthetic.valid.v1" | Set-Content -LiteralPath (Join-Path $root 'rules/rule-catalog.yml')
    $repo = Join-Path $root 'public-repo'
    [void](New-Item -ItemType Directory -Path $repo)
    & git -C $repo init -q
    'public fixture' | Set-Content -LiteralPath (Join-Path $repo 'README.txt')
    & git -C $repo add .
    & git -C $repo -c user.name=Task11 -c user.email=task11@example.invalid commit -qm fixture
    $sha = (& git -C $repo rev-parse HEAD).Trim()
    $fullOut = Join-Path $root 'full-without-optin'
    $null = & pwsh -NoProfile -File $runner -Lane FullCorpus -TraceMapRoot $repo -TraceMapCommit $sha -OutputRoot $fullOut 2>&1
    if ($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath $fullOut)) { throw 'FULL_CORPUS_OPT_IN_GUARD_FAILED' }
    $null = & pwsh -NoProfile -File $runner -Lane FullCorpus -EnableFullCorpus -TraceMapRoot $repo -TraceMapCommit $sha -OutputRoot $fullOut 2>&1
    if ($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath $fullOut)) { throw 'BOUNDED_RECEIPT_GUARD_FAILED' }
    Expect-Block { Assert-Task11Checkout $repo ('0' * 40) 'CORPUS' } 'CORPUS_COMMIT_MISMATCH'
    Assert-Task11Checkout $repo $sha 'CORPUS'
    'dirty' | Set-Content -LiteralPath (Join-Path $repo 'dirty.txt')
    Expect-Block { Assert-Task11Checkout $repo $sha 'CORPUS' } 'CORPUS_DIRTY'
    Remove-Item -LiteralPath (Join-Path $repo 'dirty.txt')
    Expect-Block { Assert-Task11Checkout (Join-Path $root 'missing') $sha 'CORPUS' } 'CORPUS_UNAVAILABLE'
    Expect-Block { Assert-Task11Tool (Join-Path $root 'no-tool.exe') 'MSBUILD' } 'MSBUILD_UNAVAILABLE'
    $out = Join-Path $root 'existing-output'
    [void](New-Item -ItemType Directory -Path $out)
    Expect-Block { Assert-Task11FreshOutput $out @($repo) } 'OUTPUT_REUSE'
    Expect-Block { Assert-Task11FreshOutput (Join-Path $repo 'nested') @($repo) } 'OUTPUT_OVERLAPS_INPUT'
    $script:Receipt = [ordered]@{ artifacts = @{}; provenance = $null }
    $scan = Join-Path $root 'scan'
    [void](New-Item -ItemType Directory -Path $scan)
    $generator = Join-Path $repo 'README.txt'
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'ARTIFACT_MISSING'
    foreach ($path in @('facts.ndjson','index.sqlite','report.md','logs/analyzer.log')) {
        $file = Join-Path $scan $path
        [void](New-Item -ItemType Directory -Path (Split-Path $file) -Force)
        '' | Set-Content -LiteralPath $file
    }
    @{ commitSha = $sha; scannerVersion = 'synthetic'; sourceSnapshotDigest = 'bad'; knownGaps = @() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $scan 'scan-manifest.json')
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'PROVENANCE_BOUNDED_INPUT_INVALID'
    @{ commitSha = $sha; scannerVersion = 'synthetic'; sourceSnapshotDigest = ('a' * 64); knownGaps = @() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $scan 'scan-manifest.json')
    @{ commitSha = $sha; factType = 'AnalysisGap'; ruleId = ''; evidenceTier = 'Tier4Unknown'; evidence = @{ extractorId = 'test'; extractorVersion = '1' } } | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $scan 'facts.ndjson')
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'FACT_PROVENANCE_INVALID'
    Write-Output 'Task 11 synthetic guards passed: full-corpus opt-in/prior receipt, wrong commit, dirty checkout, missing corpus/tool/artifact, invalid provenance, output reuse/overlap.'
} finally {
    Remove-Item -LiteralPath $root -Recurse -Force
}
