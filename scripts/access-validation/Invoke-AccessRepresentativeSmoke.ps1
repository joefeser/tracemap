param(
    [Parameter(Mandatory = $true)]
    [string]$AccessCli,

    [Parameter(Mandatory = $true)]
    [string]$TraceMapCli,

    [Parameter(Mandatory = $true)]
    [string]$DatabasePath,

    [Parameter(Mandatory = $true)]
    [string]$ScratchRoot,

    [Parameter(Mandatory = $true)]
    [string]$CheckpointBasePath,

    [switch]$InputExplicitlyAuthorized
)

$ErrorActionPreference = "Stop"

function Write-RepresentativeCheckpoint {
    if ($null -eq $checkpoint) { return }
    $checkpoint.checkpointSequence++
    $directory = Split-Path -Parent $CheckpointBasePath
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $json = $checkpoint | ConvertTo-Json -Compress
    $encoding = New-Object Text.UTF8Encoding($false)

    $sequencePath = "$CheckpointBasePath.$($checkpoint.checkpointSequence)"
    $sequenceTemporary = "$sequencePath.tmp"
    [IO.File]::WriteAllText($sequenceTemporary, $json + [Environment]::NewLine, $encoding)
    [IO.File]::Move($sequenceTemporary, $sequencePath)

    $latestTemporary = "$CheckpointBasePath.tmp"
    [IO.File]::WriteAllText($latestTemporary, $json + [Environment]::NewLine, $encoding)
    try {
        if (Test-Path $CheckpointBasePath -PathType Leaf) {
            [IO.File]::Replace($latestTemporary, $CheckpointBasePath, $null)
        }
        else {
            [IO.File]::Move($latestTemporary, $CheckpointBasePath)
        }
    }
    catch {
        Remove-Item $latestTemporary -Force -ErrorAction SilentlyContinue
    }
}

function Set-RepresentativeStage([string]$Stage) {
    $checkpoint.stopStage = $Stage
    Write-RepresentativeCheckpoint
}

function Stop-Representative([string]$Stage, [string]$Message) {
    $checkpoint.stopStage = $Stage
    Write-RepresentativeCheckpoint
    throw $Message
}

function Find-ProtectedMarker {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$Markers
    )

    $encoding = [Text.Encoding]::UTF8
    $maxMarkerBytes = ($Markers | ForEach-Object { $encoding.GetByteCount($_) } | Measure-Object -Maximum).Maximum
    $buffer = New-Object byte[] 65536
    $tail = New-Object byte[] 0
    $stream = [IO.File]::OpenRead($Path)
    try {
        while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $window = New-Object byte[] ($tail.Length + $read)
            if ($tail.Length -gt 0) { [Buffer]::BlockCopy($tail, 0, $window, 0, $tail.Length) }
            [Buffer]::BlockCopy($buffer, 0, $window, $tail.Length, $read)
            $text = $encoding.GetString($window)
            foreach ($marker in $Markers) {
                if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true }
            }
            $tailLength = [Math]::Min([Math]::Max(0, $maxMarkerBytes - 1), $window.Length)
            $tail = New-Object byte[] $tailLength
            if ($tailLength -gt 0) { [Buffer]::BlockCopy($window, $window.Length - $tailLength, $tail, 0, $tailLength) }
        }
    }
    finally { $stream.Dispose() }
    return $false
}

function Get-PropertyText([object]$Properties, [string]$Name) {
    $property = $Properties.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return "unavailable" }
    return [string]$property.Value
}

function Test-AccessSurfaceVisible {
    return (@(
        Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero }
    ).Count -gt 0)
}

function Wait-AccessScanJobs([object[]]$Jobs) {
    while (@($Jobs | Where-Object { $_.State -in @("NotStarted", "Running", "Blocked") }).Count -gt 0) {
        if (Test-AccessSurfaceVisible) { $script:accessSurfaceObserved = $true }
        Start-Sleep -Milliseconds 100
    }
    if (Test-AccessSurfaceVisible) { $script:accessSurfaceObserved = $true }
}

function Invoke-AccessScan([string[]]$ScanArguments) {
    $scanScript = {
        param($Executable, $ScanArguments, $InheritedPath)
        $env:PATH = $InheritedPath
        & $Executable @ScanArguments *> $null
        $LASTEXITCODE
    }
    $job = Start-Job -ScriptBlock $scanScript -ArgumentList $AccessCli, (, $ScanArguments), $env:PATH
    try {
        Wait-AccessScanJobs @($job)
        $result = @(Receive-Job $job -Wait)
        if ($job.State -ne "Completed" -or $result.Count -ne 1 -or -not ($result[0] -is [int])) {
            return 1
        }
        return [int]$result[0]
    }
    finally {
        Remove-Job $job -Force -ErrorAction SilentlyContinue
    }
}

$DatabasePath = [IO.Path]::GetFullPath($DatabasePath)
$ScratchRoot = [IO.Path]::GetFullPath($ScratchRoot)
$CheckpointBasePath = [IO.Path]::GetFullPath($CheckpointBasePath)
$AccessCli = [IO.Path]::GetFullPath($AccessCli)
$TraceMapCli = [IO.Path]::GetFullPath($TraceMapCli)
$scriptPath = [IO.Path]::GetFullPath($PSCommandPath)
$extension = [IO.Path]::GetExtension($DatabasePath).ToLowerInvariant()
$inputKind = if ($extension -eq ".accdb") { "accdb" } elseif ($extension -eq ".mdb") { "mdb" } else { "unknown" }
$accessSurfaceObserved = $false

$checkpoint = [ordered]@{
    schemaVersion = "tracemap.access-phase9-representative-checkpoint.v1"
    phase95Representative = "boundary-stop"
    stopStage = "input-authorization"
    checkpointSequence = 0
    inputExplicitlyAuthorized = [bool]$InputExplicitlyAuthorized
    inputKind = $inputKind
    originalUnchanged = $false
    disposableRepoNoRemote = $false
    standardArtifacts = $false
    sequentialDeterministic = $false
    concurrentDeterministic = $false
    schemaFacts = 0
    relationshipFacts = 0
    queryFacts = 0
    externalBoundaryFacts = 0
    formCount = "unavailable"
    reportCount = "unavailable"
    vbaModuleCount = "unavailable"
    namedMacroCount = "unavailable"
    gapCount = 0
    uiIdentityFactsZero = $false
    vbaIdentityFlowFactsZero = $false
    macroIdentityFactsZero = $false
    rowDataReadFalse = $false
    executionPerformedFalse = $false
    reportContractCorrect = $false
    combineContractCorrect = $false
    docsContractCorrect = $false
    vaultContractCorrect = $false
    releaseReviewContractCorrect = $false
    promptUiCanariesFalse = $false
    protectedOutputMatchCount = 0
}
Write-RepresentativeCheckpoint

if (-not $InputExplicitlyAuthorized) {
    Stop-Representative "input-authorization" "representative input authorization is required"
}
if ($inputKind -eq "unknown" -or -not (Test-Path $DatabasePath -PathType Leaf)) {
    Stop-Representative "input-authorization" "authorized representative input is unavailable"
}

foreach ($path in @($AccessCli, $TraceMapCli, $scriptPath)) {
    if (-not (Test-Path $path -PathType Leaf)) {
        Stop-Representative "input-authorization" "representative validation tool is unavailable"
    }
    if ($path.StartsWith($ScratchRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-Representative "input-authorization" "representative validation tools must be outside scratch"
    }
}
if ($DatabasePath.StartsWith($ScratchRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    Stop-Representative "input-authorization" "representative original must be outside scratch"
}
if ($CheckpointBasePath.StartsWith($ScratchRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    Stop-Representative "input-authorization" "representative checkpoint must be outside scratch"
}
$scratchFilesystemRoot = [IO.Path]::GetPathRoot($ScratchRoot)
if ([string]::Equals($ScratchRoot, $scratchFilesystemRoot, [StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path $ScratchRoot)) {
    Stop-Representative "input-authorization" "representative scratch must be a new non-root path"
}

$repo = Join-Path $ScratchRoot "repo"
$databaseRelative = "representative$extension"
$databaseCopy = Join-Path $repo $databaseRelative
$outA = Join-Path $ScratchRoot "scan-a"
$outB = Join-Path $ScratchRoot "scan-b"
$outConcurrentA = Join-Path $ScratchRoot "scan-concurrent-a"
$outConcurrentB = Join-Path $ScratchRoot "scan-concurrent-b"
$combined = Join-Path $ScratchRoot "combined.sqlite"
$combinedReport = Join-Path $ScratchRoot "combined-report.md"
$export = Join-Path $ScratchRoot "index-export.json"
$docsOutput = Join-Path $ScratchRoot "evidence-docs"
$vaultOutput = Join-Path $ScratchRoot "vault"
$releaseReviewOutput = Join-Path $ScratchRoot "release-review"

Set-RepresentativeStage "copy-provenance"
$originalHash = (Get-FileHash $DatabasePath -Algorithm SHA256).Hash.ToLowerInvariant()
New-Item -ItemType Directory -Path $repo -Force | Out-Null
$source = $null
$destination = $null
try {
    $source = [IO.File]::Open($DatabasePath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $destination = [IO.File]::Open($databaseCopy, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $source.CopyTo($destination)
    $destination.Flush($true)
}
finally {
    if ($null -ne $destination) { $destination.Dispose() }
    if ($null -ne $source) { $source.Dispose() }
}
if ((Get-FileHash $databaseCopy -Algorithm SHA256).Hash.ToLowerInvariant() -ne $originalHash) {
    Stop-Representative "copy-provenance" "representative working copy verification failed"
}

Push-Location $repo
try {
    & git init -b access-representative *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Representative "copy-provenance" "representative Git initialization failed" }
    & git config user.email "access-representative@example.invalid" *> $null
    & git config user.name "TraceMap Access Representative" *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Representative "copy-provenance" "representative Git configuration failed" }
    & git add -- $databaseRelative *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Representative "copy-provenance" "representative Git staging failed" }
    & git commit -m "authorized local Access representative" *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Representative "copy-provenance" "representative Git commit failed" }
    if (@(& git remote).Count -ne 0) { Stop-Representative "copy-provenance" "representative repository unexpectedly has a remote" }
    $disposableCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($disposableCommit)) {
        Stop-Representative "copy-provenance" "representative Git provenance is unavailable"
    }
}
finally { Pop-Location }
$checkpoint.disposableRepoNoRemote = $true
Write-RepresentativeCheckpoint

Set-RepresentativeStage "product-scan"
if (@(Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue).Count -ne 0) {
    Stop-Representative "product-scan" "Access must not already be running"
}
$scanArgumentsA = @("scan", "--repo", $repo, "--database", $databaseRelative, "--out", $outA, "--timeout-seconds", "120")
$scanArgumentsB = @("scan", "--repo", $repo, "--database", $databaseRelative, "--out", $outB, "--timeout-seconds", "120")
if ((Invoke-AccessScan $scanArgumentsA) -ne 0) { Stop-Representative "product-scan" "first representative scan failed" }
if ((Invoke-AccessScan $scanArgumentsB) -ne 0) { Stop-Representative "product-scan" "second representative scan failed" }

$required = @("scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs\analyzer.log")
foreach ($relative in $required) {
    if (-not (Test-Path (Join-Path $outA $relative) -PathType Leaf)) {
        Stop-Representative "artifact-validation" "representative standard artifact is missing"
    }
}
foreach ($relative in @("facts.ndjson", "report.md", "logs\analyzer.log")) {
    if ((Get-FileHash (Join-Path $outA $relative) -Algorithm SHA256).Hash -ne
        (Get-FileHash (Join-Path $outB $relative) -Algorithm SHA256).Hash) {
        Stop-Representative "artifact-validation" "representative sequential determinism failed"
    }
}
$checkpoint.standardArtifacts = $true
$checkpoint.sequentialDeterministic = $true
Write-RepresentativeCheckpoint

$manifest = Get-Content (Join-Path $outA "scan-manifest.json") -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace([string]$manifest.repoName) -or
    [string]::IsNullOrWhiteSpace([string]$manifest.commitSha) -or
    $manifest.commitSha -ne $disposableCommit -or
    [string]::IsNullOrWhiteSpace([string]$manifest.scannerVersion)) {
    Stop-Representative "artifact-validation" "representative manifest provenance failed"
}

$concurrentArgumentsA = @("scan", "--repo", $repo, "--database", $databaseRelative, "--out", $outConcurrentA, "--timeout-seconds", "120")
$concurrentArgumentsB = @("scan", "--repo", $repo, "--database", $databaseRelative, "--out", $outConcurrentB, "--timeout-seconds", "120")
$concurrentScript = { param($Executable, $Arguments, $InheritedPath) $env:PATH = $InheritedPath; & $Executable @Arguments *> $null; $LASTEXITCODE }
$concurrentA = Start-Job -ScriptBlock $concurrentScript -ArgumentList $AccessCli, (, $concurrentArgumentsA), $env:PATH
$concurrentB = Start-Job -ScriptBlock $concurrentScript -ArgumentList $AccessCli, (, $concurrentArgumentsB), $env:PATH
Wait-AccessScanJobs @($concurrentA, $concurrentB)
$concurrentStateA = $concurrentA.State
$concurrentStateB = $concurrentB.State
$concurrentResultA = @(Receive-Job $concurrentA)
$concurrentResultB = @(Receive-Job $concurrentB)
Remove-Job $concurrentA, $concurrentB -Force
if ($concurrentStateA -ne "Completed" -or $concurrentStateB -ne "Completed" -or
    $concurrentResultA.Count -ne 1 -or $concurrentResultB.Count -ne 1 -or
    -not ($concurrentResultA[0] -is [int]) -or -not ($concurrentResultB[0] -is [int]) -or
    $concurrentResultA[0] -ne 0 -or $concurrentResultB[0] -ne 0) {
    Stop-Representative "product-scan" "representative concurrent scan failed"
}
foreach ($output in @($outB, $outConcurrentA, $outConcurrentB)) {
    foreach ($relative in $required) {
        if (-not (Test-Path (Join-Path $output $relative) -PathType Leaf)) {
            Stop-Representative "artifact-validation" "representative repeated standard artifact is missing"
        }
    }
}
foreach ($relative in @("facts.ndjson", "report.md", "logs\analyzer.log")) {
    $expectedHash = (Get-FileHash (Join-Path $outA $relative) -Algorithm SHA256).Hash
    foreach ($output in @($outConcurrentA, $outConcurrentB)) {
        if ((Get-FileHash (Join-Path $output $relative) -Algorithm SHA256).Hash -ne $expectedHash) {
            Stop-Representative "artifact-validation" "representative concurrent determinism failed"
        }
    }
}
$checkpoint.concurrentDeterministic = $true
Write-RepresentativeCheckpoint

$facts = @(Get-Content (Join-Path $outA "facts.ndjson") | ForEach-Object { $_ | ConvertFrom-Json })
if ($facts.Count -eq 0 -or @($facts | Where-Object {
    [string]::IsNullOrWhiteSpace([string]$_.ruleId) -or
    [string]::IsNullOrWhiteSpace([string]$_.evidenceTier) -or
    $_.commitSha -ne $disposableCommit -or
    $null -eq $_.evidence -or
    [string]::IsNullOrWhiteSpace([string]$_.evidence.extractorId) -or
    [string]::IsNullOrWhiteSpace([string]$_.evidence.extractorVersion)
}).Count -ne 0) {
    Stop-Representative "artifact-validation" "representative fact provenance failed"
}
$metadata = @($facts | Where-Object { $_.factType -eq "LegacyDataMetadataDeclared" })
if ($metadata.Count -ne 1) { Stop-Representative "artifact-validation" "representative Access metadata fact is unavailable" }
$databaseMetadata = $metadata[0]
$checkpoint.schemaFacts = @($facts | Where-Object { $_.factType -eq "LegacyDataStorageObjectDeclared" }).Count
$checkpoint.relationshipFacts = @($facts | Where-Object { $_.factType -eq "LegacyDataMappingDeclared" -and $_.properties.mappingKind -eq "declared-relationship" }).Count
$checkpoint.queryFacts = @($facts | Where-Object { $_.factType -eq "AccessQueryDeclared" }).Count
$checkpoint.externalBoundaryFacts = @($facts | Where-Object { $_.factType -eq "AccessExternalLinkDeclared" }).Count
$checkpoint.formCount = Get-PropertyText $databaseMetadata.properties "formCount"
$checkpoint.reportCount = Get-PropertyText $databaseMetadata.properties "reportCount"
$checkpoint.vbaModuleCount = Get-PropertyText $databaseMetadata.properties "vbaModuleCount"
$checkpoint.namedMacroCount = Get-PropertyText $databaseMetadata.properties "namedMacroCount"
$checkpoint.gapCount = @($facts | Where-Object { $_.factType -eq "AnalysisGap" }).Count
$checkpoint.uiIdentityFactsZero = @($facts | Where-Object { $_.factType -in @("AccessFormDeclared", "AccessReportDeclared", "AccessControlDeclared", "AccessBindingDeclared") }).Count -eq 0
$checkpoint.vbaIdentityFlowFactsZero = @($facts | Where-Object { $_.factType -in @("AccessVbaModuleDeclared", "AccessVbaProcedureDeclared", "AccessNavigationCandidate", "AccessEventBindingCandidate") }).Count -eq 0
$checkpoint.macroIdentityFactsZero = @($facts | Where-Object { $_.factType -eq "AccessMacroDeclared" }).Count -eq 0
$rowCapability = @($facts | Where-Object { $_.factType -eq "AnalyzerCapabilityDiagnostic" -and $_.properties.capability -eq "rowDataRead" })
$executionCapability = @($facts | Where-Object { $_.factType -eq "AnalyzerCapabilityDiagnostic" -and $_.properties.capability -eq "executionPerformed" })
$checkpoint.rowDataReadFalse = $rowCapability.Count -eq 1 -and $rowCapability[0].properties.status -eq "false"
$checkpoint.executionPerformedFalse = $executionCapability.Count -eq 1 -and $executionCapability[0].properties.status -eq "false"
if (-not $checkpoint.uiIdentityFactsZero -or -not $checkpoint.vbaIdentityFlowFactsZero -or -not $checkpoint.macroIdentityFactsZero -or
    -not $checkpoint.rowDataReadFalse -or -not $checkpoint.executionPerformedFalse) {
    Write-RepresentativeCheckpoint
    Stop-Representative "artifact-validation" "representative count-only or non-execution contract failed"
}
Write-RepresentativeCheckpoint

Set-RepresentativeStage "downstream-validation"
$reportText = Get-Content (Join-Path $outA "report.md") -Raw
if ($reportText -notmatch [Regex]::Escape("## Access Design Evidence Summary") -or
    $reportText -notmatch [Regex]::Escape("Public claim level: ``hidden``") -or
    $reportText -notmatch [Regex]::Escape("No table rows, row counts, attachment/OLE contents, or query resultsets were read.")) {
    Stop-Representative "downstream-validation" "representative Access report contract failed"
}
$checkpoint.reportContractCorrect = $true
Write-RepresentativeCheckpoint

& $TraceMapCli export --index (Join-Path $outA "index.sqlite") --out $export --format json *> $null
if ($LASTEXITCODE -ne 0) { Stop-Representative "downstream-validation" "representative export failed" }
& $TraceMapCli combine --index (Join-Path $outA "index.sqlite") --label access --out $combined *> $null
if ($LASTEXITCODE -ne 0) { Stop-Representative "downstream-validation" "representative combine failed" }
& $TraceMapCli report --index $combined --out $combinedReport --format markdown *> $null
if ($LASTEXITCODE -ne 0) { Stop-Representative "downstream-validation" "representative combined report failed" }
$checkpoint.combineContractCorrect = $true
Write-RepresentativeCheckpoint

& $TraceMapCli docs-export --index $combined --out $docsOutput --families legacy,gap,limitation --format markdown,jsonl *> $null
if ($LASTEXITCODE -ne 0) { Stop-Representative "downstream-validation" "representative evidence docs failed" }
$docsJsonl = Join-Path $docsOutput "chunks.jsonl"
if (-not (Test-Path $docsJsonl -PathType Leaf)) { Stop-Representative "downstream-validation" "representative evidence docs are missing" }
$docsText = Get-Content $docsJsonl -Raw
if ($docsText -notmatch '"claimLevel":"hidden"' -or $docsText -notmatch [Regex]::Escape("legacy.access.")) {
    Stop-Representative "downstream-validation" "representative evidence docs contract failed"
}
$checkpoint.docsContractCorrect = $true
Write-RepresentativeCheckpoint

& $TraceMapCli vault export --combined-index $combined --out $vaultOutput --minimum-claim-level hidden --format json *> $null
if ($LASTEXITCODE -ne 0) { Stop-Representative "downstream-validation" "representative vault export failed" }
$vault = Get-Content (Join-Path $vaultOutput "graph.json") -Raw | ConvertFrom-Json
$vaultGaps = @($vault.gaps | Where-Object {
    $_.classification -eq "AccessEvidenceConsumerUnsupported" -and
    $_.ruleId -eq "vault-export.gap.access-evidence-consumer-unsupported.v1" -and
    @($_.supportingFactIds).Count -gt 0
})
if ($vaultGaps.Count -ne 1) { Stop-Representative "downstream-validation" "representative vault contract failed" }
$checkpoint.vaultContractCorrect = $true
Write-RepresentativeCheckpoint

& $TraceMapCli release-review --before $combined --after $combined --out $releaseReviewOutput --format json *> $null
if ($LASTEXITCODE -ne 0) { Stop-Representative "downstream-validation" "representative release review failed" }
$releaseReview = Get-Content (Join-Path $releaseReviewOutput "release-review.json") -Raw | ConvertFrom-Json
$releaseFindings = @($releaseReview.accessEvidence.findings | Where-Object {
    $_.section -eq "accessEvidence" -and
    $_.ruleId -like "legacy.access.*" -and
    -not [string]::IsNullOrWhiteSpace([string]$_.extractorId) -and
    -not [string]::IsNullOrWhiteSpace([string]$_.extractorVersion) -and
    -not [string]::IsNullOrWhiteSpace([string]$_.coverageLabel) -and
    @($_.supportingFactIds).Count -gt 0
})
$releaseGaps = @($releaseReview.accessEvidence.gaps | Where-Object {
    $_.section -eq "accessEvidence" -and
    $_.ruleId -like "legacy.access.*" -and
    @($_.supportingFactIds).Count -gt 0
})
if ($releaseReview.accessEvidence.status -ne "available" -or $releaseFindings.Count -eq 0 -or $releaseGaps.Count -eq 0) {
    Stop-Representative "downstream-validation" "representative release-review contract failed"
}
$checkpoint.releaseReviewContractCorrect = $true
Write-RepresentativeCheckpoint

Set-RepresentativeStage "safety-check"
$markers = [System.Collections.Generic.List[string]]::new()
$markers.Add($DatabasePath)
$originalName = [IO.Path]::GetFileName($DatabasePath)
if (-not [string]::Equals($originalName, $databaseRelative, [StringComparison]::OrdinalIgnoreCase)) {
    $markers.Add($originalName)
}
$protectedOutputs = @($outA, $outB, $outConcurrentA, $outConcurrentB, $export, $combined, $combinedReport, $docsOutput, $vaultOutput, $releaseReviewOutput)
$matches = 0
foreach ($outputItem in $protectedOutputs) {
    if (-not (Test-Path $outputItem)) { continue }
    $files = if (Test-Path $outputItem -PathType Container) { Get-ChildItem $outputItem -File -Recurse } else { Get-Item $outputItem }
    foreach ($file in $files) {
        if (Find-ProtectedMarker -Path $file.FullName -Markers $markers.ToArray()) { $matches++ }
    }
}
$checkpoint.protectedOutputMatchCount = $matches
$checkpoint.originalUnchanged = (Get-FileHash $DatabasePath -Algorithm SHA256).Hash.ToLowerInvariant() -eq $originalHash
$checkpoint.promptUiCanariesFalse = -not $accessSurfaceObserved
Write-RepresentativeCheckpoint
if ($matches -ne 0 -or -not $checkpoint.originalUnchanged -or -not $checkpoint.promptUiCanariesFalse) {
    Stop-Representative "safety-check" "representative protected-output or original-integrity check failed"
}

$checkpoint.phase95Representative = "completed"
$checkpoint.stopStage = "none"
Write-RepresentativeCheckpoint

[pscustomobject]@{
    Schema = "tracemap.access-phase9-representative-result.v1"
    Status = "completed"
    Checkpoint = "written"
} | ConvertTo-Json -Compress
