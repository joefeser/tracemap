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
    "rules:`r`n  - id: synthetic.valid.v1`r`n" | Set-Content -LiteralPath (Join-Path $root 'rules/rule-catalog.yml') -NoNewline
    if (-not (Get-Task11RuleIds "rules:`n  - id: synthetic.valid.v1`n").Contains('synthetic.valid.v1')) {
        throw 'LF_RULE_CATALOG_UNREADABLE'
    }
    $repo = Join-Path $root 'public-repo'
    [void](New-Item -ItemType Directory -Path $repo)
    & git -C $repo init -q
    'public fixture' | Set-Content -LiteralPath (Join-Path $repo 'README.txt')
    & git -C $repo add .
    & git -C $repo -c user.name=Task11 -c user.email=task11@example.invalid commit -qm fixture
    $sha = (& git -C $repo rev-parse HEAD).Trim()
    $bounded = Assert-Task11BoundedPaths $repo @('README.txt')
    if ($bounded.fileCount -ne 1 -or $bounded.sourceBytes -le 0) { throw 'BOUNDED_FILE_SELECTION_INVALID' }
    Expect-Block { Assert-Task11BoundedPaths $repo @('*') } 'BOUNDED_PATH_NOT_EXACT_FILE'
    Expect-Block { Assert-Task11BoundedPaths $repo @('README.txt','README.txt') } 'BOUNDED_PATH_INVALID_OR_DUPLICATE'
    Expect-Block { Assert-Task11BoundedPaths $repo @('missing.txt') } 'BOUNDED_FILE_UNAVAILABLE'
    Expect-Block { Assert-Task11BoundedPaths $repo @('..\outside.txt') } 'BOUNDED_PATH_INVALID_OR_DUPLICATE'
    $script:MaxBoundedFiles = 0
    Expect-Block { Assert-Task11BoundedPaths $repo @('README.txt') } 'BOUNDED_FILE_COUNT_LIMIT'
    $script:MaxBoundedFiles = 256
    $script:MaxBoundedBytes = 1
    Expect-Block { Assert-Task11BoundedPaths $repo @('README.txt') } 'BOUNDED_SOURCE_BYTES_LIMIT'
    $script:MaxBoundedBytes = 64MB
    if ($IsWindows) {
        $fullOut = Join-Path $root 'full-without-optin'
        $null = & pwsh -NoProfile -File $runner -Lane FullCorpus -TraceMapRoot $repo -TraceMapCommit $sha -OutputRoot $fullOut 2>&1
        if ($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath $fullOut)) { throw 'FULL_CORPUS_OPT_IN_GUARD_FAILED' }
        $null = & pwsh -NoProfile -File $runner -Lane FullCorpus -EnableFullCorpus -TraceMapRoot $repo -TraceMapCommit $sha -OutputRoot $fullOut 2>&1
        if ($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath $fullOut)) { throw 'BOUNDED_RECEIPT_GUARD_FAILED' }
        $oldReceipt = Join-Path $root 'old-bounded-receipt.json'
        @{ kind = 'Bounded'; status = 'passed'; traceMapCommit = $sha; corpusCommit = 'db8c3359badfec620ccdc6df062b1756ef9607f8'; provenance = @{ generatorSha256 = ('a' * 64); boundedInputSha256 = ('b' * 64) } } |
            ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $oldReceipt
        $null = & pwsh -NoProfile -File $runner -Lane FullCorpus -EnableFullCorpus -BoundedReceiptPath $oldReceipt -TraceMapRoot $repo -TraceMapCommit $sha -OutputRoot $fullOut 2>&1
        if ($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath $fullOut)) { throw 'UNBOUNDED_PRIOR_RECEIPT_ACCEPTED' }
    }
    Expect-Block { Assert-Task11Checkout $repo ('0' * 40) 'CORPUS' } 'CORPUS_COMMIT_MISMATCH'
    Assert-Task11Checkout $repo $sha 'CORPUS'
    'dirty' | Set-Content -LiteralPath (Join-Path $repo 'dirty.txt')
    Expect-Block { Assert-Task11Checkout $repo $sha 'CORPUS' } 'CORPUS_DIRTY'
    Remove-Item -LiteralPath (Join-Path $repo 'dirty.txt')
    Expect-Block { Assert-Task11Checkout (Join-Path $root 'missing') $sha 'CORPUS' } 'CORPUS_UNAVAILABLE'
    Expect-Block { Assert-Task11Tool (Join-Path $root 'no-tool.exe') 'MSBUILD' } 'MSBUILD_UNAVAILABLE'
    if ($IsWindows) {
        $out = Join-Path $root 'existing-output'
        [void](New-Item -ItemType Directory -Path $out)
        Expect-Block { Assert-Task11FreshOutput $out @($repo) } 'OUTPUT_REUSE'
        Expect-Block { Assert-Task11FreshOutput (Join-Path $repo 'nested') @($repo) } 'OUTPUT_OVERLAPS_INPUT'
    }
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
    $validEvidence = @{ extractorId = 'test'; extractorVersion = '1'; filePath = 'README.txt'; startLine = 1; endLine = 1 }
    @{ commitSha = $sha; factId = 'synthetic-fact'; factType = 'AnalysisGap'; ruleId = 'synthetic.valid'; evidenceTier = 'Tier4Unknown'; evidence = $validEvidence } | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $scan 'facts.ndjson')
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'FACT_RULE_UNREGISTERED'
    @{ commitSha = $sha; factId = 'synthetic-fact'; factType = 'AnalysisGap'; ruleId = 'synthetic.valid.v1'; evidenceTier = 'Tier4Unknown'; evidence = $validEvidence } | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $scan 'facts.ndjson')
    Assert-Task11Artifacts $scan $sha $generator
    $windowsOnly = if ($IsWindows) { 'full-corpus opt-in/prior receipt and output reuse/overlap passed' } else { 'Windows-only full-corpus and output guards not run' }
    Write-Output "Task 11 synthetic guards passed: bounded exact tracked file/count/bytes selection, exact rule ID, wrong commit, dirty checkout, missing corpus/tool/artifact, invalid provenance; $windowsOnly."
} finally {
    Remove-Item -LiteralPath $root -Recurse -Force
}
