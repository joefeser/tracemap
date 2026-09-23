# Task 11 local-only Windows validation. Never publish the receipt or output tree.
[CmdletBinding()]
param(
    [ValidateSet('PublicSmoke', 'Bounded', 'FullCorpus')][string]$Lane = 'Bounded',
    [ValidateSet('HistoricalMaster', 'BuildableFix')][string]$CorpusProfile = 'HistoricalMaster',
    [Parameter(Mandatory)][string]$TraceMapRoot,
    [string]$CorpusRoot,
    [string]$CorpusRemoteSha256,
    [switch]$AuthorizedCorpus,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$TraceMapCommit,
    [Parameter(Mandatory)][string]$OutputRoot,
    [string]$SliceProject,
    [string]$TestAssembly,
    [string[]]$RepresentativeCases = @(),
    [string[]]$BoundedPaths = @(),
    [string]$IlAsmPath = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\ilasm.exe",
    [string]$IlDasmPath,
    [string]$VisualStudioPath,
    [string]$FrameworkPath = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll",
    [string]$MsBuildPath,
    [string]$TestRunnerPath,
    [switch]$EnableFullCorpus,
    [string]$BoundedReceiptPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$DigestPattern = '^[0-9a-f]{64}$'
$RequiredCategories = @('branch','switch','exception-region','leave','instrumentation','nested-generic','duplicate-identity')
$CorpusProfiles = @{
    HistoricalMaster = [ordered]@{ commit = 'db8c3359badfec620ccdc6df062b1756ef9607f8'; maxFiles = 256 }
    BuildableFix = [ordered]@{ commit = '642bdaede0b97a400c24266e30670ed5c1c98689'; maxFiles = 427 }
}
$CorpusCommit = $CorpusProfiles[$CorpusProfile].commit
$MaxBoundedFiles = $CorpusProfiles[$CorpusProfile].maxFiles
$MaxBoundedBytes = 64MB
$MaxCandidateEntries = 4096
$RunnerSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()

function Assert-Task11([bool]$Condition, [string]$Code) {
    if (-not $Condition) { throw $Code }
}
function Invoke-Task11Command([string]$File, [string[]]$Arguments, [string]$WorkingDirectory) {
    $old = Get-Location
    try {
        Set-Location -LiteralPath $WorkingDirectory
        $output = & $File @Arguments 2>&1 | Out-String
        $code = $LASTEXITCODE
        $script:Receipt.commands.Add([ordered]@{ executable = $File; arguments = $Arguments; cwd = $WorkingDirectory; exitCode = $code; output = $output })
        Assert-Task11 ($code -eq 0) "COMMAND_FAILED:${code}:$File"
        return $output.Trim()
    } finally { Set-Location $old }
}
function Get-Task11Git([string]$Root, [string[]]$Arguments) {
    $output = & git -C $Root @Arguments 2>&1 | Out-String
    Assert-Task11 ($LASTEXITCODE -eq 0) "GIT_FAILED:$($Arguments -join ',')"
    return $output.Trim()
}
function Assert-Task11Checkout([string]$Root, [string]$Expected, [string]$Label) {
    Assert-Task11 (Test-Path -LiteralPath (Join-Path $Root '.git')) "${Label}_UNAVAILABLE"
    Assert-Task11 ((Get-Task11Git $Root @('rev-parse','HEAD')) -ceq $Expected) "${Label}_COMMIT_MISMATCH"
    $status = Get-Task11Git $Root @('status','--porcelain','--untracked-files=all','--ignored')
    foreach ($line in @($status -split "`r?`n" | Where-Object { $_ })) {
        if ($line.StartsWith('!! ')) {
            $relative = $line.Substring(3).Replace('\','/')
            # Scanner inventory excludes generated bin/obj trees. Other ignored inputs are not admitted.
            if ($relative -match '(^|/)(bin|obj)/') {
                $generated = Get-Item -LiteralPath (Join-Path $Root $relative.TrimEnd('/')) -Force
                if (-not ($generated.Attributes -band [IO.FileAttributes]::ReparsePoint)) { continue }
            }
        }
        throw "${Label}_DIRTY"
    }
}
function Assert-Task11FreshOutput([string]$Path, [string[]]$Inputs) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    Assert-Task11 ($null -eq (Get-Item -LiteralPath $full -Force -ErrorAction SilentlyContinue)) 'OUTPUT_REUSE'
    $ancestor = [IO.DirectoryInfo]([IO.Path]::GetDirectoryName($full))
    while ($null -ne $ancestor) {
        Assert-Task11 (-not $ancestor.Exists -or -not ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'OUTPUT_SYMLINK_PARENT'
        $ancestor = $ancestor.Parent
    }
    foreach ($input in $Inputs) {
        if (-not $input) { continue }
        $resolved = [IO.Path]::GetFullPath($input).TrimEnd('\','/')
        Assert-Task11 (-not ($full.Equals($resolved, [StringComparison]::OrdinalIgnoreCase) -or
            $full.StartsWith($resolved + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            $resolved.StartsWith($full + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) 'OUTPUT_OVERLAPS_INPUT'
    }
    return $full
}
function New-Task11FallbackReceiptPath([string[]]$Inputs) {
    $directory = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-task11-preflight-' + [guid]::NewGuid().ToString('N'))
    $safe = Assert-Task11FreshOutput $directory $Inputs
    [void](New-Item -ItemType Directory -Path $safe)
    return (Join-Path $safe 'private-receipt.json')
}
function Assert-Task11Tool([string]$Path, [string]$Label) {
    Assert-Task11 (-not [string]::IsNullOrWhiteSpace($Path) -and (Test-Path -LiteralPath $Path -PathType Leaf)) "${Label}_UNAVAILABLE"
    $item = Get-Item -LiteralPath $Path
    Assert-Task11 (-not [string]::IsNullOrWhiteSpace($item.VersionInfo.FileVersion)) "${Label}_VERSION_UNAVAILABLE"
    $script:Receipt.tools[$Label] = [ordered]@{ path = $item.FullName; fileVersion = $item.VersionInfo.FileVersion; productVersion = $item.VersionInfo.ProductVersion }
}
function Resolve-Task11CorpusPath([string]$Root, [string]$Path) {
    Assert-Task11 (-not [string]::IsNullOrWhiteSpace($Path)) 'CORPUS_PATH_REQUIRED'
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $full = if ([IO.Path]::IsPathRooted($Path)) {
        [IO.Path]::GetFullPath($Path)
    } else {
        [IO.Path]::GetFullPath((Join-Path $rootFull $Path))
    }
    Assert-Task11 ($full.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) 'CORPUS_PATH_OUTSIDE_ROOT'
    return $full
}
function Assert-Task11BoundedProjectSelection([string]$Root, [string]$ProjectFull, [string[]]$Paths) {
    $relativeProject = [IO.Path]::GetRelativePath([IO.Path]::GetFullPath($Root), $ProjectFull).Replace('\','/')
    $selected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $Paths) { [void]$selected.Add($path.Replace('\','/')) }
    Assert-Task11 ($selected.Contains($relativeProject)) 'BOUNDED_PROJECT_NOT_SELECTED'
}
function Assert-Task11BoundedPaths([string]$Root, [string[]]$Paths) {
    Assert-Task11 ($Paths.Count -gt 0 -and $Paths.Count -le $MaxBoundedFiles) 'BOUNDED_FILE_COUNT_LIMIT'
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    [long]$totalBytes = 0
    foreach ($path in $Paths) {
        Assert-Task11 (-not [string]::IsNullOrWhiteSpace($path) -and
            -not [IO.Path]::IsPathRooted($path) -and $path.IndexOfAny([char[]]'*?[]:') -lt 0) 'BOUNDED_PATH_NOT_EXACT_FILE'
        $relative = $path.Replace('\','/')
        Assert-Task11 (-not ($relative.Split('/') | Where-Object { $_ -in @('','.', '..') }) -and
            $seen.Add($relative)) 'BOUNDED_PATH_INVALID_OR_DUPLICATE'
        $full = [IO.Path]::GetFullPath((Join-Path $rootFull $relative))
        Assert-Task11 ($full.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $full -PathType Leaf)) 'BOUNDED_FILE_UNAVAILABLE'
        $file = Get-Item -LiteralPath $full -Force
        Assert-Task11 (-not ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'BOUNDED_FILE_REPARSE_POINT'
        [void](Get-Task11Git $Root @('--literal-pathspecs','ls-files','--error-unmatch','--',$relative))
        $totalBytes += $file.Length
        Assert-Task11 ($totalBytes -le $MaxBoundedBytes) 'BOUNDED_SOURCE_BYTES_LIMIT'
    }
    return [ordered]@{ fileCount = $Paths.Count; sourceBytes = $totalBytes; maxFiles = $MaxBoundedFiles; maxBytes = $MaxBoundedBytes; maxCandidateEntries = $MaxCandidateEntries }
}
function Get-Task11CandidateEntryCount([string]$Root) {
    # Match FileInventory's non-recursive directory-then-file enumeration. Count
    # excluded child directories before skipping them, as the scanner does.
    $options = [IO.EnumerationOptions]::new()
    $options.RecurseSubdirectories = $false
    $options.IgnoreInaccessible = $false
    $options.AttributesToSkip = [IO.FileAttributes]::Hidden -bor [IO.FileAttributes]::System
    $excluded = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @('.git','.tracemap','.nuget','bin','node_modules','obj')) { [void]$excluded.Add($name) }
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push([IO.Path]::GetFullPath($Root))
    $count = 0
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($child in [IO.Directory]::EnumerateDirectories($directory, '*', $options)) {
            if (++$count -gt $MaxCandidateEntries) { throw 'BOUNDED_CANDIDATE_ENTRY_LIMIT' }
            $parts = [IO.Path]::GetRelativePath($Root, $child).Split([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
            if (($parts.Count -gt 1 -and $parts[0].Equals('packages', [StringComparison]::OrdinalIgnoreCase)) -or
                @($parts | Where-Object { $excluded.Contains($_) }).Count -gt 0) { continue }
            if ([IO.File]::GetAttributes($child) -band [IO.FileAttributes]::ReparsePoint) { continue }
            $pending.Push($child)
        }
        foreach ($file in [IO.Directory]::EnumerateFiles($directory, '*', $options)) {
            if (++$count -gt $MaxCandidateEntries) { throw 'BOUNDED_CANDIDATE_ENTRY_LIMIT' }
        }
    }
    return $count
}
function Get-Task11RuleIds([string]$Catalog) {
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($match in [regex]::Matches($Catalog, '(?m)^  - id: (?<id>[A-Za-z0-9._-]+)[ \t]*(?:#.*)?\r?$')) {
        [void]$ids.Add($match.Groups['id'].Value)
    }
    Assert-Task11 ($ids.Count -gt 0) 'RULE_CATALOG_EMPTY'
    return ,$ids
}
function Get-Task11GeneratorPayloadSha256([string]$GeneratorPath) {
    Assert-Task11 (Test-Path -LiteralPath $GeneratorPath -PathType Leaf) 'GENERATOR_MISSING'
    $directory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($GeneratorPath))
    $files = @(Get-ChildItem -LiteralPath $directory -File -Recurse -Force |
        Sort-Object { [IO.Path]::GetRelativePath($directory, $_.FullName).Replace('\','/') })
    Assert-Task11 ($files.Count -gt 0) 'GENERATOR_PAYLOAD_EMPTY'
    $entries = foreach ($file in $files) {
        Assert-Task11 (-not ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'GENERATOR_PAYLOAD_REPARSE_POINT'
        $relative = [IO.Path]::GetRelativePath($directory, $file.FullName).Replace('\','/')
        $digest = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$relative`t$digest"
    }
    $payload = [Text.Encoding]::UTF8.GetBytes(($entries -join "`n") + "`n")
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($payload)).ToLowerInvariant()
}
function Assert-Task11GeneratorMatchesReceipt([string]$CurrentPayloadDigest, [string]$CurrentGeneratorDigest, [object]$BoundedReceipt) {
    Assert-Task11 ($CurrentGeneratorDigest -ceq [string]$BoundedReceipt.provenance.generatorSha256 -and
        $CurrentPayloadDigest -ceq [string]$BoundedReceipt.provenance.generatorPayloadSha256) 'BOUNDED_GENERATOR_MISMATCH'
}
function Assert-Task11Artifacts([string]$ScanDir, [string]$ExpectedCommit, [string]$GeneratorPath) {
    foreach ($relative in @('scan-manifest.json','facts.ndjson','index.sqlite','report.md','logs/analyzer.log')) {
        $path = Join-Path $ScanDir $relative
        Assert-Task11 (Test-Path -LiteralPath $path -PathType Leaf) "ARTIFACT_MISSING:$relative"
        $script:Receipt.artifacts[$relative] = $path
    }
    foreach ($relative in @('scan-manifest.json','facts.ndjson','index.sqlite','report.md')) {
        Assert-Task11 ((Get-Item -LiteralPath (Join-Path $ScanDir $relative)).Length -gt 0) "ARTIFACT_EMPTY:$relative"
    }
    $indexPath = Join-Path $ScanDir 'index.sqlite'
    $stream = [IO.File]::OpenRead($indexPath)
    try {
        $header = [byte[]]::new(16)
        Assert-Task11 ($stream.Read($header, 0, $header.Length) -eq 16 -and
            [Text.Encoding]::ASCII.GetString($header) -ceq "SQLite format 3`0") 'INDEX_SQLITE_HEADER_INVALID'
    } finally { $stream.Dispose() }
    $manifest = Get-Content -LiteralPath (Join-Path $ScanDir 'scan-manifest.json') -Raw | ConvertFrom-Json
    Assert-Task11 ($manifest.commitSha -ceq $ExpectedCommit) 'PROVENANCE_COMMIT_INVALID'
    Assert-Task11 (-not [string]::IsNullOrWhiteSpace($manifest.scannerVersion)) 'PROVENANCE_SCANNER_VERSION_MISSING'
    Assert-Task11 ([string]$manifest.sourceSnapshotDigest -cmatch $DigestPattern) 'PROVENANCE_BOUNDED_INPUT_INVALID'
    Assert-Task11 (-not [string]::IsNullOrWhiteSpace([string]$manifest.analysisLevel) -and
        -not [string]::IsNullOrWhiteSpace([string]$manifest.buildStatus)) 'PROVENANCE_COVERAGE_MISSING'
    $facts = @(Get-Content -LiteralPath (Join-Path $ScanDir 'facts.ndjson') | Where-Object { $_ } | ForEach-Object { $_ | ConvertFrom-Json })
    Assert-Task11 ($facts.Count -gt 0) 'FACTS_EMPTY'
    $extractors = @{}
    $catalog = Get-Content -LiteralPath (Join-Path $TraceMapRoot 'rules/rule-catalog.yml') -Raw
    $ruleIds = Get-Task11RuleIds $catalog
    $gaps = @()
    foreach ($fact in $facts) {
        Assert-Task11 ($fact.commitSha -ceq $ExpectedCommit -and
            -not [string]::IsNullOrWhiteSpace($fact.ruleId) -and
            [string]$fact.evidenceTier -cin @('Tier1Semantic','Tier2Structural','Tier3SyntaxOrTextual','Tier4Unknown') -and
            -not [string]::IsNullOrWhiteSpace($fact.evidence.extractorId) -and
            -not [string]::IsNullOrWhiteSpace($fact.evidence.extractorVersion) -and
            -not [string]::IsNullOrWhiteSpace($fact.evidence.filePath) -and
            [int]$fact.evidence.startLine -ge 0 -and
            [int]$fact.evidence.endLine -ge [int]$fact.evidence.startLine) 'FACT_PROVENANCE_INVALID'
        Assert-Task11 ($ruleIds.Contains([string]$fact.ruleId)) 'FACT_RULE_UNREGISTERED'
        $extractors[[string]$fact.evidence.extractorId] = [string]$fact.evidence.extractorVersion
        if ($fact.factType -eq 'AnalysisGap') { $gaps += [ordered]@{ ruleId = $fact.ruleId; factId = $fact.factId } }
    }
    $script:Receipt.provenance = [ordered]@{
        generatorSha256 = (Get-FileHash -LiteralPath $GeneratorPath -Algorithm SHA256).Hash.ToLowerInvariant()
        generatorPayloadSha256 = Get-Task11GeneratorPayloadSha256 $GeneratorPath
        boundedInputSha256 = $manifest.sourceSnapshotDigest
        commit = $manifest.commitSha
        scannerVersion = $manifest.scannerVersion
        analysisLevel = $manifest.analysisLevel
        buildStatus = $manifest.buildStatus
        extractors = $extractors
        knownGaps = @($manifest.knownGaps)
        analysisGaps = $gaps
        factCount = $facts.Count
        claim = 'pinned-baseline-only; identity edges require rule-specific assertions'
    }
    if ($manifest.buildStatus -cne 'Succeeded' -or
        $manifest.analysisLevel -cne 'Level1SemanticAnalysis' -or
        $gaps.Count -gt 0 -or @($manifest.knownGaps).Count -gt 0) {
        return 'partial'
    }
    return 'passed'
}

# Dot-source only for public synthetic guard tests; command execution stays in the entry point.
if ($MyInvocation.InvocationName -eq '.') { return }

$Receipt = [ordered]@{
    schemaVersion = 2; kind = $Lane; status = 'running'; startedUtc = (Get-Date).ToUniversalTime().ToString('o')
    runnerSha256 = $RunnerSha256; traceMapCommit = $TraceMapCommit; corpusProfile = $CorpusProfile; corpusCommit = $CorpusCommit
    admissionPolicy = [ordered]@{ maxFiles = $MaxBoundedFiles; maxBytes = $MaxBoundedBytes; maxCandidateEntries = $MaxCandidateEntries }
    host = [ordered]@{ os = [Environment]::OSVersion.VersionString; architecture = $env:PROCESSOR_ARCHITECTURE; psVersion = $PSVersionTable.PSVersion.ToString(); imageOS = $env:ImageOS; imageVersion = $env:ImageVersion }
    tools = @{}; selectedTests = @($RepresentativeCases); boundedPaths = @($BoundedPaths); boundedSelection = $null; stages = [Collections.Generic.List[object]]::new()
    commands = [Collections.Generic.List[object]]::new(); artifacts = @{}; provenance = $null; blocker = $null
}
$receiptPath = $null
try {
    Assert-Task11 ($IsWindows) 'WINDOWS_REQUIRED'
    Assert-Task11 ($Lane -ne 'FullCorpus' -or $EnableFullCorpus) 'FULL_CORPUS_OPT_IN_REQUIRED'
    if ($Lane -eq 'FullCorpus') {
        Assert-Task11 (-not [string]::IsNullOrWhiteSpace($BoundedReceiptPath) -and (Test-Path -LiteralPath $BoundedReceiptPath -PathType Leaf)) 'BOUNDED_RECEIPT_REQUIRED'
        $boundedReceipt = Get-Content -LiteralPath $BoundedReceiptPath -Raw | ConvertFrom-Json
        Assert-Task11 ($null -ne $boundedReceipt.PSObject.Properties['schemaVersion'] -and
            [int]$boundedReceipt.schemaVersion -eq 2 -and
            $boundedReceipt.kind -ceq 'Bounded' -and $boundedReceipt.status -ceq 'passed' -and
            $null -ne $boundedReceipt.PSObject.Properties['runnerSha256'] -and
            $boundedReceipt.runnerSha256 -ceq $RunnerSha256 -and
            $boundedReceipt.traceMapCommit -ceq $TraceMapCommit -and
            $null -ne $boundedReceipt.PSObject.Properties['corpusProfile'] -and
            $boundedReceipt.corpusProfile -ceq $CorpusProfile -and $boundedReceipt.corpusCommit -ceq $CorpusCommit -and
            $null -ne $boundedReceipt.PSObject.Properties['admissionPolicy'] -and
            [int]$boundedReceipt.admissionPolicy.maxFiles -eq $MaxBoundedFiles -and
            [long]$boundedReceipt.admissionPolicy.maxBytes -eq $MaxBoundedBytes -and
            [int]$boundedReceipt.admissionPolicy.maxCandidateEntries -eq $MaxCandidateEntries -and
            [string]$boundedReceipt.provenance.generatorSha256 -cmatch $DigestPattern -and
            $null -ne $boundedReceipt.provenance.PSObject.Properties['generatorPayloadSha256'] -and
            [string]$boundedReceipt.provenance.generatorPayloadSha256 -cmatch $DigestPattern -and
            [string]$boundedReceipt.provenance.boundedInputSha256 -cmatch $DigestPattern -and
            $null -ne $boundedReceipt.PSObject.Properties['boundedSelection'] -and
            $null -ne $boundedReceipt.PSObject.Properties['boundedPaths'] -and
            $null -ne $boundedReceipt.boundedSelection -and
            $null -ne $boundedReceipt.boundedSelection.PSObject.Properties['candidateEntries'] -and
            $null -ne $boundedReceipt.boundedSelection.PSObject.Properties['candidateEntriesAfterTests'] -and
            [int]$boundedReceipt.boundedSelection.fileCount -gt 0 -and
            [int]$boundedReceipt.boundedSelection.fileCount -le $MaxBoundedFiles -and
            [long]$boundedReceipt.boundedSelection.sourceBytes -le $MaxBoundedBytes -and
            [int]$boundedReceipt.boundedSelection.maxFiles -eq $MaxBoundedFiles -and
            [long]$boundedReceipt.boundedSelection.maxBytes -eq $MaxBoundedBytes -and
            [int]$boundedReceipt.boundedSelection.candidateEntries -gt 0 -and
            [int]$boundedReceipt.boundedSelection.candidateEntries -le $MaxCandidateEntries -and
            [int]$boundedReceipt.boundedSelection.candidateEntriesAfterTests -gt 0 -and
            [int]$boundedReceipt.boundedSelection.candidateEntriesAfterTests -le $MaxCandidateEntries -and
            [int]$boundedReceipt.boundedSelection.maxCandidateEntries -eq $MaxCandidateEntries) 'BOUNDED_RECEIPT_INVALID'
    }
    $inputs = @($TraceMapRoot, $CorpusRoot, $BoundedReceiptPath)
    $safeOutput = Assert-Task11FreshOutput $OutputRoot $inputs
    [void](New-Item -ItemType Directory -Path $safeOutput -Force)
    $receiptPath = Join-Path $safeOutput 'private-receipt.json'
    Assert-Task11Checkout $TraceMapRoot $TraceMapCommit 'TRACEMAP'
    $Receipt.stages.Add([ordered]@{ name = 'tracemap-preflight'; status = 'passed' })
    Assert-Task11Tool $IlAsmPath 'ILASM'
    $dotnet = (Get-Command dotnet.exe -ErrorAction SilentlyContinue).Source
    Assert-Task11Tool $dotnet 'DOTNET'
    $Receipt.tools['DOTNET'].sdkVersion = Invoke-Task11Command $dotnet @('--version') $TraceMapRoot
    if ($Lane -ne 'PublicSmoke') {
        Assert-Task11 ($AuthorizedCorpus) 'CORPUS_AUTHORIZATION_REQUIRED'
        Assert-Task11 (-not [string]::IsNullOrWhiteSpace($CorpusRoot)) 'CORPUS_UNAVAILABLE'
        Assert-Task11Checkout $CorpusRoot $CorpusCommit 'CORPUS'
        if ($Lane -eq 'FullCorpus') {
            $priorSelection = Assert-Task11BoundedPaths $CorpusRoot @($boundedReceipt.boundedPaths)
            Assert-Task11 ($priorSelection.fileCount -eq [int]$boundedReceipt.boundedSelection.fileCount -and
                $priorSelection.sourceBytes -eq [long]$boundedReceipt.boundedSelection.sourceBytes) 'BOUNDED_RECEIPT_SELECTION_MISMATCH'
        }
        Assert-Task11 ($CorpusRemoteSha256 -cmatch $DigestPattern) 'CORPUS_REMOTE_DIGEST_REQUIRED'
        $remote = Get-Task11Git $CorpusRoot @('remote','get-url','origin')
        $remoteDigest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($remote))).ToLowerInvariant()
        Assert-Task11 ($remoteDigest -ceq $CorpusRemoteSha256) 'CORPUS_REMOTE_MISMATCH'
        Assert-Task11 (-not [string]::IsNullOrWhiteSpace($SliceProject)) 'SLICE_PROJECT_UNAVAILABLE'
        $projectFull = Resolve-Task11CorpusPath $CorpusRoot $SliceProject
        Assert-Task11 (Test-Path -LiteralPath $projectFull -PathType Leaf) 'SLICE_PROJECT_UNAVAILABLE'
        Assert-Task11 (-not [string]::IsNullOrWhiteSpace($TestAssembly)) 'TEST_ASSEMBLY_REQUIRED'
        $cases = @{}
        foreach ($case in $RepresentativeCases) {
            Assert-Task11 ($case -cmatch '^([a-z-]+)=([A-Za-z_][A-Za-z0-9_.+`]*)$') 'REPRESENTATIVE_CASE_INVALID'
            Assert-Task11 (-not $cases.ContainsKey($Matches[1])) 'REPRESENTATIVE_CATEGORY_DUPLICATE'
            $cases[$Matches[1]] = $Matches[2]
        }
        foreach ($category in $RequiredCategories) { Assert-Task11 ($cases.ContainsKey($category)) "REPRESENTATIVE_CATEGORY_MISSING:$category" }
        if ($Lane -eq 'Bounded') {
            $Receipt.boundedSelection = Assert-Task11BoundedPaths $CorpusRoot $BoundedPaths
            $Receipt.boundedSelection.candidateEntries = Get-Task11CandidateEntryCount $CorpusRoot
            Assert-Task11BoundedProjectSelection $CorpusRoot $projectFull $BoundedPaths
        }
        Assert-Task11Tool $MsBuildPath 'MSBUILD'
        Assert-Task11Tool $TestRunnerPath 'TEST_RUNNER'
        Assert-Task11Tool $IlDasmPath 'ILDASM'
        Assert-Task11Tool $FrameworkPath 'FRAMEWORK'
        Assert-Task11 (-not [string]::IsNullOrWhiteSpace($VisualStudioPath) -and (Test-Path -LiteralPath $VisualStudioPath -PathType Container)) 'VISUAL_STUDIO_UNAVAILABLE'
        Assert-Task11Tool (Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe') 'VISUAL_STUDIO'
        $Receipt.stages.Add([ordered]@{ name = 'private-preflight'; status = 'passed' })
    }
    $smokeDir = Join-Path $safeOutput 'public-ilasm'
    [void](New-Item -ItemType Directory -Path $smokeDir)
    $il = Join-Path $smokeDir 'Task11Public.il'
    @'
.assembly extern mscorlib {}
.assembly Task11Public {}
.module Task11Public.dll
.class public auto ansi Task11Public extends [mscorlib]System.Object {
  .method public static int32 Value() cil managed {
    .maxstack 1
    ldc.i4.7
    ret
  }
}
'@ | Set-Content -LiteralPath $il -Encoding ascii
    $dll = Join-Path $smokeDir 'Task11Public.dll'
    [void](Invoke-Task11Command $IlAsmPath @('/NOLOGO','/DLL',"/OUTPUT:$dll",$il) $smokeDir)
    Assert-Task11 (Test-Path -LiteralPath $dll -PathType Leaf) 'PUBLIC_ILASM_OUTPUT_MISSING'
    $Receipt.stages.Add([ordered]@{ name = 'public-ilasm-smoke'; status = 'passed'; artifact = $dll })
    if ($Lane -ne 'PublicSmoke') {
        $sliceDir = Join-Path $safeOutput 'slice'
        [void](New-Item -ItemType Directory -Path $sliceDir)
        [void](Invoke-Task11Command $MsBuildPath @($projectFull,'/t:Rebuild','/p:Configuration=Debug',"/p:BaseOutputPath=$sliceDir\build\") $CorpusRoot)
        $Receipt.stages.Add([ordered]@{ name = 'smallest-project-build'; status = 'passed' })
        $buildFull = [IO.Path]::GetFullPath((Join-Path $sliceDir 'build')).TrimEnd('\','/')
        $assemblyFull = if ([IO.Path]::IsPathRooted($TestAssembly)) {
            [IO.Path]::GetFullPath($TestAssembly)
        } else {
            [IO.Path]::GetFullPath((Join-Path $buildFull $TestAssembly))
        }
        Assert-Task11 ($assemblyFull.StartsWith($buildFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) 'TEST_ASSEMBLY_OUTSIDE_SLICE_OUTPUT'
        Assert-Task11 (Test-Path -LiteralPath $assemblyFull -PathType Leaf) 'TEST_ASSEMBLY_UNAVAILABLE_AFTER_BUILD'
        foreach ($category in $RequiredCategories) {
            $test = $cases[$category]
            $trx = Join-Path $sliceDir (([guid]::NewGuid().ToString('N')) + '.trx')
            [void](Invoke-Task11Command $TestRunnerPath @($assemblyFull,"/Tests:$test", "/Logger:trx;LogFileName=$trx") $CorpusRoot)
            Assert-Task11 (Test-Path -LiteralPath $trx -PathType Leaf) 'TEST_RESULT_MISSING'
            [xml]$result = Get-Content -LiteralPath $trx -Raw
            $passed = @($result.SelectNodes("//*[local-name()='UnitTestResult' and @outcome='Passed']")).Count
            Assert-Task11 ($passed -gt 0) 'SELECTED_TEST_NOT_EXECUTED'
            $Receipt.stages.Add([ordered]@{ name = "test:$category"; status = 'passed'; fullyQualifiedName = $test; passed = $passed; trx = $trx })
        }
        $Receipt.stages.Add([ordered]@{ name = 'representative-tests'; status = 'passed' })
        Assert-Task11Checkout $CorpusRoot $CorpusCommit 'CORPUS_AFTER_TESTS'
        if ($Lane -eq 'Bounded') {
            $Receipt.boundedSelection.candidateEntriesAfterTests = Get-Task11CandidateEntryCount $CorpusRoot
        }
        $cliProject = Join-Path $TraceMapRoot 'src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
        [void](Invoke-Task11Command $dotnet @('build',$cliProject,'-c','Release','--no-incremental','--nologo') $TraceMapRoot)
        Assert-Task11Checkout $TraceMapRoot $TraceMapCommit 'TRACEMAP_AFTER_BUILD'
        $generator = Join-Path $TraceMapRoot 'src/dotnet/TraceMap.Cli/bin/Release/net10.0/tracemap.dll'
        Assert-Task11 (Test-Path -LiteralPath $generator -PathType Leaf) 'GENERATOR_MISSING'
        $generatorPayloadSha256 = Get-Task11GeneratorPayloadSha256 $generator
        if ($Lane -eq 'FullCorpus') {
            $generatorSha256 = (Get-FileHash -LiteralPath $generator -Algorithm SHA256).Hash.ToLowerInvariant()
            Assert-Task11GeneratorMatchesReceipt $generatorPayloadSha256 $generatorSha256 $boundedReceipt
        }
        $scanDir = Join-Path $safeOutput $(if ($Lane -eq 'FullCorpus') { 'full-corpus-scan' } else { 'bounded-scan' })
        $scanArgs = @($generator,'scan','--repo',$CorpusRoot,'--out',$scanDir)
        if ($Lane -eq 'Bounded') {
            $scanArgs += @('--exact-source-scope','--exact-source-max-files',[string]$MaxBoundedFiles,
                '--exact-source-max-bytes',[string]$MaxBoundedBytes)
            foreach ($path in $BoundedPaths) { $scanArgs += @('--include',$path) }
        }
        [void](Invoke-Task11Command $dotnet $scanArgs $TraceMapRoot)
        $Receipt.stages.Add([ordered]@{ name = 'pinned-scan'; status = 'passed' })
        $scanStatus = Assert-Task11Artifacts $scanDir $CorpusCommit $generator
        [void](Invoke-Task11Command $dotnet @($generator,'validate-index','--index',(Join-Path $scanDir 'index.sqlite'),
            '--commit',$CorpusCommit,'--facts',[string]$Receipt.provenance.factCount) $TraceMapRoot)
        $Receipt.stages.Add([ordered]@{ name = 'artifact-provenance'; status = $scanStatus })
    }
    $Receipt.status = if ($Lane -eq 'PublicSmoke') { 'passed' } else { $scanStatus }
} catch {
    $Receipt.status = 'blocked'
    $Receipt.blocker = $_.Exception.Message
    $Receipt.stages.Add([ordered]@{ name = 'halt'; status = 'blocked'; reason = $Receipt.blocker })
} finally {
    $Receipt.finishedUtc = (Get-Date).ToUniversalTime().ToString('o')
    if (-not $receiptPath) {
        try { $receiptPath = New-Task11FallbackReceiptPath @($TraceMapRoot, $CorpusRoot, $BoundedReceiptPath) }
        catch { $Receipt.stages.Add([ordered]@{ name = 'failure-receipt'; status = 'unavailable'; reason = 'NO_SAFE_FALLBACK_LOCATION' }) }
    }
    if ($receiptPath) { $Receipt | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $receiptPath -Encoding utf8 }
}
if ($Receipt.status -eq 'blocked') { throw "$($Receipt.blocker) receipt=$receiptPath" }
if ($Receipt.status -eq 'partial') {
    Write-Output "TASK11_$($Lane.ToUpperInvariant())_PARTIAL receipt=$receiptPath"
    exit 2
}
Write-Output "TASK11_$($Lane.ToUpperInvariant())_PASSED receipt=$receiptPath"
