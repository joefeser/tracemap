# Synthetic/public failure-path tests; no private checkout or toolchain required.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot 'Invoke-Task11Windows.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('task11-tests-' + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $root)
function Expect-Block([scriptblock]$Action, [string]$Code) {
    $threw = $false
    try { & $Action }
    catch {
        $threw = $true
        if (-not $_.Exception.Message.Contains($Code)) { throw }
    }
    if (-not $threw) { throw "EXPECTED_BLOCK_MISSING:$Code" }
}
try {
    $helperRejectedSuccess = $false
    try { Expect-Block { } 'SELF_TEST' }
    catch { $helperRejectedSuccess = $_.Exception.Message -ceq 'EXPECTED_BLOCK_MISSING:SELF_TEST' }
    if (-not $helperRejectedSuccess) { throw 'EXPECT_BLOCK_SELF_TEST_FAILED' }
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
    & git -C $repo -c core.autocrlf=false add .
    & git -C $repo -c user.name=Task11 -c user.email=task11@example.invalid commit -qm fixture
    $sha = (& git -C $repo rev-parse HEAD).Trim()
    if ($IsWindows) {
        $fallbackReceipt = New-Task11FallbackReceiptPath @($repo)
        if (-not (Test-Path -LiteralPath (Split-Path $fallbackReceipt) -PathType Container)) {
            throw 'PREFLIGHT_RECEIPT_LOCATION_MISSING'
        }
        Remove-Item -LiteralPath (Split-Path $fallbackReceipt) -Recurse -Force
    }
    $projectPath = Join-Path $repo 'nested/project.csproj'
    if ((Resolve-Task11CorpusPath $repo 'nested/project.csproj') -cne [IO.Path]::GetFullPath($projectPath)) {
        throw 'CORPUS_RELATIVE_PATH_RESOLUTION_FAILED'
    }
    Expect-Block { Resolve-Task11CorpusPath $repo '../outside.csproj' } 'CORPUS_PATH_OUTSIDE_ROOT'
    Expect-Block { Assert-Task11BoundedProjectSelection $repo $projectPath @('README.txt') } 'BOUNDED_PROJECT_NOT_SELECTED'
    Assert-Task11BoundedProjectSelection $repo $projectPath @('nested/project.csproj')
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
        $wrongProfileReceipt = Join-Path $root 'wrong-profile-receipt.json'
        $profileReceipt = @{
            schemaVersion = 2; kind = 'Bounded'; status = 'passed'; runnerSha256 = $RunnerSha256; traceMapCommit = $sha
            corpusProfile = 'HistoricalMaster'; corpusCommit = '642bdaede0b97a400c24266e30670ed5c1c98689'
            admissionPolicy = @{ maxFiles = 427; maxBytes = 64MB; maxCandidateEntries = 4096 }
            boundedSelection = @{
                fileCount = $bounded.fileCount; sourceBytes = $bounded.sourceBytes
                maxFiles = 427; maxBytes = 64MB; maxCandidateEntries = 4096
                candidateEntries = (Get-Task11CandidateEntryCount $repo)
                candidateEntriesAfterTests = (Get-Task11CandidateEntryCount $repo)
            }
            boundedPaths = @('README.txt')
            provenance = @{ generatorSha256 = ('a' * 64); generatorPayloadSha256 = ('b' * 64); boundedInputSha256 = ('c' * 64) }
        }
        $profileReceipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $wrongProfileReceipt
        $wrongProfileOutput = & pwsh -NoProfile -File $runner -Lane FullCorpus -CorpusProfile BuildableFix -EnableFullCorpus -BoundedReceiptPath $wrongProfileReceipt -TraceMapRoot $repo -TraceMapCommit $sha -OutputRoot $fullOut 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath $fullOut) -or
            -not $wrongProfileOutput.Contains('BOUNDED_RECEIPT_INVALID')) { throw 'CROSS_PROFILE_RECEIPT_ACCEPTED' }
        $validProfileReceipt = Join-Path $root 'valid-profile-receipt.json'
        $profileReceipt.corpusProfile = 'BuildableFix'
        $profileReceipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $validProfileReceipt
        $validProfileOut = Join-Path $root 'valid-profile-output'
        $missingIlAsm = Join-Path $root 'missing-ilasm.exe'
        $validProfileOutput = & pwsh -NoProfile -File $runner -Lane FullCorpus -CorpusProfile buildablefix -EnableFullCorpus -BoundedReceiptPath $validProfileReceipt -TraceMapRoot $repo -TraceMapCommit $sha -OutputRoot $validProfileOut -IlAsmPath $missingIlAsm 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -or -not (Test-Path -LiteralPath $validProfileOut) -or
            -not $validProfileOutput.Contains('ILASM_UNAVAILABLE') -or
            $validProfileOutput.Contains('BOUNDED_RECEIPT_INVALID')) { throw 'VALID_PROFILE_RECEIPT_REJECTED' }
    }
    Expect-Block { Assert-Task11Checkout $repo ('0' * 40) 'CORPUS' } 'CORPUS_COMMIT_MISMATCH'
    Assert-Task11Checkout $repo $sha 'CORPUS'
    'dirty' | Set-Content -LiteralPath (Join-Path $repo 'dirty.txt')
    Expect-Block { Assert-Task11Checkout $repo $sha 'CORPUS' } 'CORPUS_DIRTY'
    Remove-Item -LiteralPath (Join-Path $repo 'dirty.txt')
    'ignored.cs' | Add-Content -LiteralPath (Join-Path $repo '.git/info/exclude')
    'ignored source' | Set-Content -LiteralPath (Join-Path $repo 'ignored.cs')
    Expect-Block { Assert-Task11Checkout $repo $sha 'CORPUS' } 'CORPUS_DIRTY'
    Remove-Item -LiteralPath (Join-Path $repo 'ignored.cs')
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
    $generator = Join-Path $scan 'tracemap.dll'
    'generator' | Set-Content -LiteralPath $generator
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'ARTIFACT_MISSING'
    foreach ($path in @('facts.ndjson','index.sqlite','report.md','logs/analyzer.log')) {
        $file = Join-Path $scan $path
        [void](New-Item -ItemType Directory -Path (Split-Path $file) -Force)
        [IO.File]::WriteAllBytes($file, [byte[]]::new(0))
    }
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'ARTIFACT_MISSING:scan-manifest.json'
    @{ commitSha = $sha; scannerVersion = 'synthetic'; sourceSnapshotDigest = 'bad'; knownGaps = @(); analysisLevel = 'Level1SemanticAnalysis'; buildStatus = 'Succeeded' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $scan 'scan-manifest.json')
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'ARTIFACT_EMPTY:facts.ndjson'
    'invalid fact' | Set-Content -LiteralPath (Join-Path $scan 'facts.ndjson')
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'ARTIFACT_EMPTY:index.sqlite'
    [IO.File]::WriteAllBytes((Join-Path $scan 'index.sqlite'), [Text.Encoding]::ASCII.GetBytes("SQLite format 3`0"))
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'ARTIFACT_EMPTY:report.md'
    'Synthetic report' | Set-Content -LiteralPath (Join-Path $scan 'report.md')
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'PROVENANCE_BOUNDED_INPUT_INVALID'
    [IO.File]::WriteAllBytes((Join-Path $scan 'index.sqlite'), [Text.Encoding]::ASCII.GetBytes('not a sqlite file'))
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'INDEX_SQLITE_HEADER_INVALID'
    [IO.File]::WriteAllBytes((Join-Path $scan 'index.sqlite'), [Text.Encoding]::ASCII.GetBytes("SQLite format 3`0"))
    @{ commitSha = $sha; scannerVersion = 'synthetic'; sourceSnapshotDigest = ('a' * 64); knownGaps = @(); analysisLevel = 'Level1SemanticAnalysis'; buildStatus = 'Succeeded' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $scan 'scan-manifest.json')
    @{ commitSha = $sha; factType = 'AnalysisGap'; ruleId = ''; evidenceTier = 'Tier4Unknown'; evidence = @{ extractorId = 'test'; extractorVersion = '1' } } | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $scan 'facts.ndjson')
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'FACT_PROVENANCE_INVALID'
    $validEvidence = @{ extractorId = 'test'; extractorVersion = '1'; filePath = 'README.txt'; startLine = 1; endLine = 1 }
    @{ commitSha = $sha; factId = 'synthetic-fact'; factType = 'AnalysisGap'; ruleId = 'synthetic.valid'; evidenceTier = 'Tier4Unknown'; evidence = $validEvidence } | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $scan 'facts.ndjson')
    Expect-Block { Assert-Task11Artifacts $scan $sha $generator } 'FACT_RULE_UNREGISTERED'
    @{ commitSha = $sha; factId = 'synthetic-fact'; factType = 'AnalysisGap'; ruleId = 'synthetic.valid.v1'; evidenceTier = 'Tier4Unknown'; evidence = $validEvidence } | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $scan 'facts.ndjson')
    if ((Assert-Task11Artifacts $scan $sha $generator) -cne 'partial') { throw 'ANALYSIS_GAP_NOT_PARTIAL' }
    @{ commitSha = $sha; factId = 'synthetic-fact'; factType = 'Project'; ruleId = 'synthetic.valid.v1'; evidenceTier = 'Tier2Structural'; evidence = $validEvidence } | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $scan 'facts.ndjson')
    if ((Assert-Task11Artifacts $scan $sha $generator) -cne 'passed') { throw 'CLEAN_SCAN_NOT_PASSED' }
    @{ commitSha = $sha; scannerVersion = 'synthetic'; sourceSnapshotDigest = ('a' * 64); knownGaps = @(); analysisLevel = 'Level3SyntaxAnalysis'; buildStatus = 'NotRun' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $scan 'scan-manifest.json')
    if ((Assert-Task11Artifacts $scan $sha $generator) -cne 'partial') { throw 'REDUCED_BUILD_NOT_PARTIAL' }
    @{ commitSha = $sha; scannerVersion = 'synthetic'; sourceSnapshotDigest = ('a' * 64); knownGaps = @('synthetic-gap'); analysisLevel = 'Level1SemanticAnalysis'; buildStatus = 'Succeeded' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $scan 'scan-manifest.json')
    if ((Assert-Task11Artifacts $scan $sha $generator) -cne 'partial') { throw 'KNOWN_GAP_NOT_PARTIAL' }
    $beforePayload = Get-Task11GeneratorPayloadSha256 $generator
    'dependency' | Set-Content -LiteralPath (Join-Path $scan 'TraceMap.Core.dll')
    $afterPayload = Get-Task11GeneratorPayloadSha256 $generator
    if ($beforePayload -ceq $afterPayload) { throw 'GENERATOR_DEPENDENCY_NOT_HASHED' }
    $generatorDigest = (Get-FileHash -LiteralPath $generator -Algorithm SHA256).Hash.ToLowerInvariant()
    $priorReceipt = @{ provenance = @{ generatorSha256 = $generatorDigest; generatorPayloadSha256 = $beforePayload } }
    Expect-Block { Assert-Task11GeneratorMatchesReceipt $afterPayload $generatorDigest $priorReceipt } 'BOUNDED_GENERATOR_MISMATCH'
    Assert-Task11GeneratorMatchesReceipt $beforePayload $generatorDigest $priorReceipt
    if ($CorpusProfile -cne 'HistoricalMaster' -or
        $CorpusCommit -cne 'db8c3359badfec620ccdc6df062b1756ef9607f8' -or
        $MaxBoundedFiles -ne 256) { throw 'HISTORICAL_PROFILE_CHANGED' }
    $countRepo = Join-Path $root 'complete-inventory'
    [void](New-Item -ItemType Directory -Path $countRepo)
    & git -C $countRepo init -q
    $paths = @(0..426 | ForEach-Object { "file$_.cs" })
    foreach ($path in $paths) { 'public fixture' | Set-Content -LiteralPath (Join-Path $countRepo $path) }
    & git -C $countRepo -c core.autocrlf=false add .
    & git -C $countRepo -c user.name=Task11 -c user.email=task11@example.invalid commit -qm complete-427
    if (@(& git -C $countRepo ls-files).Count -ne 427) { throw 'SYNTHETIC_INVENTORY_NOT_COMPLETE' }
    Expect-Block { Assert-Task11BoundedPaths $countRepo $paths } 'BOUNDED_FILE_COUNT_LIMIT'
    . $runner -CorpusProfile BuildableFix -TraceMapRoot $root -TraceMapCommit ('a' * 40) -OutputRoot (Join-Path $root 'unused')
    if ($CorpusCommit -cne '642bdaede0b97a400c24266e30670ed5c1c98689' -or
        $MaxBoundedFiles -ne 427 -or $MaxBoundedBytes -ne 64MB -or $MaxCandidateEntries -ne 4096) {
        throw 'BUILDABLE_FIX_PROFILE_INVALID'
    }
    $fixSelection = Assert-Task11BoundedPaths $countRepo $paths
    if ($fixSelection.fileCount -ne 427 -or $fixSelection.maxFiles -ne 427 -or
        $fixSelection.maxCandidateEntries -ne 4096) { throw 'COMPLETE_427_NOT_ADMITTED' }
    $candidateEntries = Get-Task11CandidateEntryCount $countRepo
    if ($candidateEntries -lt 427 -or $candidateEntries -gt 4096) { throw 'CANDIDATE_COUNT_INVALID' }
    $script:MaxCandidateEntries = $candidateEntries - 1
    Expect-Block { Get-Task11CandidateEntryCount $countRepo } 'BOUNDED_CANDIDATE_ENTRY_LIMIT'
    $script:MaxCandidateEntries = 4096
    'public fixture' | Set-Content -LiteralPath (Join-Path $countRepo 'file427.cs')
    & git -C $countRepo -c core.autocrlf=false add .
    & git -C $countRepo -c user.name=Task11 -c user.email=task11@example.invalid commit -qm complete-428
    Expect-Block { Assert-Task11BoundedPaths $countRepo @($paths + 'file427.cs') } 'BOUNDED_FILE_COUNT_LIMIT'
    [void](New-Item -ItemType Directory -Path (Join-Path $countRepo 'bin'))
    'bin/' | Add-Content -LiteralPath (Join-Path $countRepo '.git/info/exclude')
    'ignored generated source' | Set-Content -LiteralPath (Join-Path $countRepo 'bin/ignored.cs')
    Assert-Task11Checkout $countRepo ((& git -C $countRepo rev-parse HEAD).Trim()) 'CORPUS'
    Expect-Block { Assert-Task11BoundedPaths $countRepo @('bin/ignored.cs') } 'GIT_FAILED'
    $windowsOnly = if ($IsWindows) { 'full-corpus opt-in/prior receipt and output reuse/overlap passed' } else { 'Windows-only full-corpus and output guards not run' }
    Write-Output "Task 11 synthetic guards passed: historical and fix profiles, complete 427-file admission, 428-file rejection, candidate-entry limit, ignored generated source rejection, exact rule ID, wrong commit, dirty checkout, missing corpus/tool/artifact, invalid provenance; $windowsOnly."
} finally {
    Remove-Item -LiteralPath $root -Recurse -Force
}
