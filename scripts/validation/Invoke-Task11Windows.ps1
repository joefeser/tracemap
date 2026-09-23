# Task 11 local-only Windows validation. Never publish the receipt or output tree.
[CmdletBinding()]
param(
    [ValidateSet('PublicSmoke', 'Bounded', 'FullCorpus')][string]$Lane = 'Bounded',
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
$CorpusCommit = 'db8c3359badfec620ccdc6df062b1756ef9607f8'
$DigestPattern = '^[0-9a-f]{64}$'
$RequiredCategories = @('branch','switch','exception-region','leave','instrumentation','nested-generic','duplicate-identity')

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
    Assert-Task11 (-not (Get-Task11Git $Root @('status','--porcelain','--untracked-files=all'))) "${Label}_DIRTY"
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
function Assert-Task11Tool([string]$Path, [string]$Label) {
    Assert-Task11 (-not [string]::IsNullOrWhiteSpace($Path) -and (Test-Path -LiteralPath $Path -PathType Leaf)) "${Label}_UNAVAILABLE"
    $item = Get-Item -LiteralPath $Path
    Assert-Task11 (-not [string]::IsNullOrWhiteSpace($item.VersionInfo.FileVersion)) "${Label}_VERSION_UNAVAILABLE"
    $script:Receipt.tools[$Label] = [ordered]@{ path = $item.FullName; fileVersion = $item.VersionInfo.FileVersion; productVersion = $item.VersionInfo.ProductVersion }
}
function Assert-Task11Artifacts([string]$ScanDir, [string]$ExpectedCommit, [string]$GeneratorPath) {
    foreach ($relative in @('scan-manifest.json','facts.ndjson','index.sqlite','report.md','logs/analyzer.log')) {
        $path = Join-Path $ScanDir $relative
        Assert-Task11 (Test-Path -LiteralPath $path -PathType Leaf) "ARTIFACT_MISSING:$relative"
        $script:Receipt.artifacts[$relative] = $path
    }
    $manifest = Get-Content -LiteralPath (Join-Path $ScanDir 'scan-manifest.json') -Raw | ConvertFrom-Json
    Assert-Task11 ($manifest.commitSha -ceq $ExpectedCommit) 'PROVENANCE_COMMIT_INVALID'
    Assert-Task11 (-not [string]::IsNullOrWhiteSpace($manifest.scannerVersion)) 'PROVENANCE_SCANNER_VERSION_MISSING'
    Assert-Task11 ([string]$manifest.sourceSnapshotDigest -cmatch $DigestPattern) 'PROVENANCE_BOUNDED_INPUT_INVALID'
    $facts = @(Get-Content -LiteralPath (Join-Path $ScanDir 'facts.ndjson') | Where-Object { $_ } | ForEach-Object { $_ | ConvertFrom-Json })
    Assert-Task11 ($facts.Count -gt 0) 'FACTS_EMPTY'
    $extractors = @{}
    $catalog = Get-Content -LiteralPath (Join-Path $TraceMapRoot 'rules/rule-catalog.yml') -Raw
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
        Assert-Task11 ($catalog.Contains("- id: $($fact.ruleId)")) 'FACT_RULE_UNREGISTERED'
        $extractors[[string]$fact.evidence.extractorId] = [string]$fact.evidence.extractorVersion
        if ($fact.factType -eq 'AnalysisGap') { $gaps += [ordered]@{ ruleId = $fact.ruleId; factId = $fact.factId } }
    }
    $script:Receipt.provenance = [ordered]@{
        generatorSha256 = (Get-FileHash -LiteralPath $GeneratorPath -Algorithm SHA256).Hash.ToLowerInvariant()
        boundedInputSha256 = $manifest.sourceSnapshotDigest
        commit = $manifest.commitSha
        scannerVersion = $manifest.scannerVersion
        extractors = $extractors
        knownGaps = @($manifest.knownGaps)
        analysisGaps = $gaps
        factCount = $facts.Count
        claim = 'pinned-baseline-only; identity edges require rule-specific assertions'
    }
}

# Dot-source only for public synthetic guard tests; command execution stays in the entry point.
if ($MyInvocation.InvocationName -eq '.') { return }

$Receipt = [ordered]@{
    schemaVersion = 1; kind = $Lane; status = 'running'; startedUtc = (Get-Date).ToUniversalTime().ToString('o')
    traceMapCommit = $TraceMapCommit; corpusCommit = $CorpusCommit
    host = [ordered]@{ os = [Environment]::OSVersion.VersionString; architecture = $env:PROCESSOR_ARCHITECTURE; psVersion = $PSVersionTable.PSVersion.ToString(); imageOS = $env:ImageOS; imageVersion = $env:ImageVersion }
    tools = @{}; selectedTests = @($RepresentativeCases); boundedPaths = @($BoundedPaths); stages = [Collections.Generic.List[object]]::new()
    commands = [Collections.Generic.List[object]]::new(); artifacts = @{}; provenance = $null; blocker = $null
}
$receiptPath = $null
try {
    Assert-Task11 ($IsWindows) 'WINDOWS_REQUIRED'
    Assert-Task11 ($Lane -ne 'FullCorpus' -or $EnableFullCorpus) 'FULL_CORPUS_OPT_IN_REQUIRED'
    if ($Lane -eq 'FullCorpus') {
        Assert-Task11 (-not [string]::IsNullOrWhiteSpace($BoundedReceiptPath) -and (Test-Path -LiteralPath $BoundedReceiptPath -PathType Leaf)) 'BOUNDED_RECEIPT_REQUIRED'
        $boundedReceipt = Get-Content -LiteralPath $BoundedReceiptPath -Raw | ConvertFrom-Json
        Assert-Task11 ($boundedReceipt.kind -ceq 'Bounded' -and $boundedReceipt.status -ceq 'passed' -and
            $boundedReceipt.traceMapCommit -ceq $TraceMapCommit -and $boundedReceipt.corpusCommit -ceq $CorpusCommit -and
            [string]$boundedReceipt.provenance.generatorSha256 -cmatch $DigestPattern -and
            [string]$boundedReceipt.provenance.boundedInputSha256 -cmatch $DigestPattern) 'BOUNDED_RECEIPT_INVALID'
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
        Assert-Task11 ($CorpusRemoteSha256 -cmatch $DigestPattern) 'CORPUS_REMOTE_DIGEST_REQUIRED'
        $remote = Get-Task11Git $CorpusRoot @('remote','get-url','origin')
        $remoteDigest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($remote))).ToLowerInvariant()
        Assert-Task11 ($remoteDigest -ceq $CorpusRemoteSha256) 'CORPUS_REMOTE_MISMATCH'
        Assert-Task11 (-not [string]::IsNullOrWhiteSpace($SliceProject) -and (Test-Path -LiteralPath $SliceProject -PathType Leaf)) 'SLICE_PROJECT_UNAVAILABLE'
        $corpusFull = [IO.Path]::GetFullPath($CorpusRoot).TrimEnd('\','/')
        $projectFull = [IO.Path]::GetFullPath($SliceProject)
        Assert-Task11 ($projectFull.StartsWith($corpusFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) 'SLICE_PROJECT_OUTSIDE_CORPUS'
        Assert-Task11 (-not [string]::IsNullOrWhiteSpace($TestAssembly)) 'TEST_ASSEMBLY_REQUIRED'
        $cases = @{}
        foreach ($case in $RepresentativeCases) {
            Assert-Task11 ($case -cmatch '^([a-z-]+)=([A-Za-z_][A-Za-z0-9_.+`]*)$') 'REPRESENTATIVE_CASE_INVALID'
            Assert-Task11 (-not $cases.ContainsKey($Matches[1])) 'REPRESENTATIVE_CATEGORY_DUPLICATE'
            $cases[$Matches[1]] = $Matches[2]
        }
        foreach ($category in $RequiredCategories) { Assert-Task11 ($cases.ContainsKey($category)) "REPRESENTATIVE_CATEGORY_MISSING:$category" }
        Assert-Task11 ($Lane -eq 'FullCorpus' -or ($BoundedPaths.Count -gt 0 -and @($BoundedPaths | Where-Object { [string]::IsNullOrWhiteSpace($_) -or $_.Contains('..') -or [IO.Path]::IsPathRooted($_) }).Count -eq 0)) 'BOUNDED_PATHS_REQUIRED'
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
        [void](Invoke-Task11Command $MsBuildPath @($SliceProject,'/t:Build','/p:Configuration=Debug',"/p:BaseOutputPath=$sliceDir\build\") $CorpusRoot)
        $Receipt.stages.Add([ordered]@{ name = 'smallest-project-build'; status = 'passed' })
        $assemblyFull = [IO.Path]::GetFullPath($TestAssembly)
        $buildFull = [IO.Path]::GetFullPath((Join-Path $sliceDir 'build')).TrimEnd('\','/')
        Assert-Task11 ($assemblyFull.StartsWith($buildFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) 'TEST_ASSEMBLY_OUTSIDE_SLICE_OUTPUT'
        Assert-Task11 (Test-Path -LiteralPath $TestAssembly -PathType Leaf) 'TEST_ASSEMBLY_UNAVAILABLE_AFTER_BUILD'
        foreach ($category in $RequiredCategories) {
            $test = $cases[$category]
            $trx = Join-Path $sliceDir (([guid]::NewGuid().ToString('N')) + '.trx')
            [void](Invoke-Task11Command $TestRunnerPath @($TestAssembly,"/Tests:$test", "/Logger:trx;LogFileName=$trx") $CorpusRoot)
            Assert-Task11 (Test-Path -LiteralPath $trx -PathType Leaf) 'TEST_RESULT_MISSING'
            [xml]$result = Get-Content -LiteralPath $trx -Raw
            $passed = @($result.SelectNodes("//*[local-name()='UnitTestResult' and @outcome='Passed']")).Count
            Assert-Task11 ($passed -gt 0) 'SELECTED_TEST_NOT_EXECUTED'
            $Receipt.stages.Add([ordered]@{ name = "test:$category"; status = 'passed'; fullyQualifiedName = $test; passed = $passed; trx = $trx })
        }
        $Receipt.stages.Add([ordered]@{ name = 'representative-tests'; status = 'passed' })
        Assert-Task11Checkout $CorpusRoot $CorpusCommit 'CORPUS_AFTER_TESTS'
        $cliProject = Join-Path $TraceMapRoot 'src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
        [void](Invoke-Task11Command $dotnet @('build',$cliProject,'-c','Release','--nologo') $TraceMapRoot)
        Assert-Task11Checkout $TraceMapRoot $TraceMapCommit 'TRACEMAP_AFTER_BUILD'
        $generator = Join-Path $TraceMapRoot 'src/dotnet/TraceMap.Cli/bin/Release/net10.0/tracemap.dll'
        Assert-Task11 (Test-Path -LiteralPath $generator -PathType Leaf) 'GENERATOR_MISSING'
        $scanDir = Join-Path $safeOutput $(if ($Lane -eq 'FullCorpus') { 'full-corpus-scan' } else { 'bounded-scan' })
        $scanArgs = @($generator,'scan','--repo',$CorpusRoot,'--out',$scanDir)
        if ($Lane -eq 'Bounded') { foreach ($path in $BoundedPaths) { $scanArgs += @('--include',$path) } }
        [void](Invoke-Task11Command $dotnet $scanArgs $TraceMapRoot)
        $Receipt.stages.Add([ordered]@{ name = 'pinned-scan'; status = 'passed' })
        Assert-Task11Artifacts $scanDir $CorpusCommit $generator
        $Receipt.stages.Add([ordered]@{ name = 'artifact-provenance'; status = 'passed' })
    }
    $Receipt.status = 'passed'
} catch {
    $Receipt.status = 'blocked'
    $Receipt.blocker = $_.Exception.Message
    $Receipt.stages.Add([ordered]@{ name = 'halt'; status = 'blocked'; reason = $Receipt.blocker })
} finally {
    $Receipt.finishedUtc = (Get-Date).ToUniversalTime().ToString('o')
    if ($receiptPath) { $Receipt | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $receiptPath -Encoding utf8 }
}
if ($Receipt.status -ne 'passed') { throw $Receipt.blocker }
Write-Output "TASK11_$($Lane.ToUpperInvariant())_PASSED receipt=$receiptPath"
