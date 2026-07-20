param(
    [Parameter(Mandatory = $true)]
    [string]$AccessCli,

    [Parameter(Mandatory = $true)]
    [string]$TraceMapCli,

    [Parameter(Mandatory = $true)]
    [string]$Generator,

    [string]$SmokeRoot = "C:\TraceMapAccessSmoke",

    [string]$Phase9CheckpointPath
)

$ErrorActionPreference = "Stop"

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
                if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $marker }
            }
            $tailLength = [Math]::Min([Math]::Max(0, $maxMarkerBytes - 1), $window.Length)
            $tail = New-Object byte[] $tailLength
            if ($tailLength -gt 0) { [Buffer]::BlockCopy($window, $window.Length - $tailLength, $tail, 0, $tailLength) }
        }
    }
    finally { $stream.Dispose() }
    return $null
}

function Assert-DisposableSmokeRoot {
    param([Parameter(Mandatory = $true)][string]$Path)

    $pathRoot = [IO.Path]::GetPathRoot($Path)
    if ([string]::IsNullOrWhiteSpace($pathRoot) -or
        [string]::Equals($Path.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar), $pathRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Smoke root must be a non-root disposable directory"
    }
    if (Test-Path $Path) {
        if (-not (Test-Path $Path -PathType Container)) {
            throw "Smoke root must be a directory"
        }
        $marker = Join-Path $Path ".tracemap-smoke-root"
        if (-not (Test-Path $marker -PathType Leaf)) {
            throw "Existing smoke root is missing the TraceMap disposable marker"
        }
    }
}

$SmokeRoot = [IO.Path]::GetFullPath($SmokeRoot)
Assert-DisposableSmokeRoot -Path $SmokeRoot
$phase9Coverage = "named-count-observed-loaded-state-unavailable-other-categories-identities-bodies-unavailable"
$phase9Checkpoint = $null
if (-not [string]::IsNullOrWhiteSpace($Phase9CheckpointPath)) {
    $Phase9CheckpointPath = [IO.Path]::GetFullPath($Phase9CheckpointPath)
    if ([string]::Equals($Phase9CheckpointPath, $SmokeRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $Phase9CheckpointPath.StartsWith($SmokeRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Phase 9 checkpoint must be outside the disposable smoke root"
    }
    $phase9Checkpoint = [ordered]@{
        schemaVersion = "tracemap.access-phase9-checkpoint.v1"
        phase9ConsumerContracts = "boundary-stop"
        stopStage = "generation"
        failureClassification = "none"
        checkpointSequence = 0
        namedFixtureShapeAvailable = $false
        namedMacroCountObserved = $false
        productContractCorrect = $false
        reportContractCorrect = $false
        combineContractCorrect = $false
        docsContractCorrect = $false
        vaultContractCorrect = $false
        releaseReviewContractCorrect = $false
        macroIdentityFactsZero = $false
        deterministic = $false
        generationCanariesFalse = $false
        extractionCanariesFalse = $false
        protectedOutputMatchCount = 0
        baselineFixtureUnchanged = $false
    }
}

function Save-Phase9Checkpoint {
    if ($null -eq $phase9Checkpoint) { return }
    $phase9Checkpoint.checkpointSequence++
    $directory = Split-Path -Parent $Phase9CheckpointPath
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $checkpointJson = $phase9Checkpoint | ConvertTo-Json -Compress
    $encoding = New-Object Text.UTF8Encoding($false)

    # Immutable sequence snapshots are authoritative. A best-effort latest-file
    # replacement cannot erase the last proven gate on filesystems where
    # File.Replace is unavailable or transiently blocked.
    $sequencePath = "$Phase9CheckpointPath.$($phase9Checkpoint.checkpointSequence)"
    $sequenceTemporary = "$sequencePath.tmp"
    [IO.File]::WriteAllText($sequenceTemporary, $checkpointJson + [Environment]::NewLine, $encoding)
    [IO.File]::Move($sequenceTemporary, $sequencePath)

    $latestTemporary = "$Phase9CheckpointPath.tmp"
    [IO.File]::WriteAllText($latestTemporary, $checkpointJson + [Environment]::NewLine, $encoding)
    try {
        if (Test-Path $Phase9CheckpointPath -PathType Leaf) {
            [IO.File]::Replace($latestTemporary, $Phase9CheckpointPath, $null)
        }
        else {
            [IO.File]::Move($latestTemporary, $Phase9CheckpointPath)
        }
    }
    catch {
        Remove-Item $latestTemporary -Force -ErrorAction SilentlyContinue
    }
}

function Set-Phase9Stage([string]$Stage) {
    if ($null -eq $phase9Checkpoint) { return }
    $phase9Checkpoint.stopStage = $Stage
    Save-Phase9Checkpoint
}

Save-Phase9Checkpoint

function Stop-Phase9([string]$Classification, [string]$Message) {
    if ($null -ne $phase9Checkpoint) {
        $phase9Checkpoint.failureClassification = $Classification
        Save-Phase9Checkpoint
    }
    throw $Message
}

$toolPaths = [ordered]@{
    accessCli = [IO.Path]::GetFullPath($AccessCli)
    traceMapCli = [IO.Path]::GetFullPath($TraceMapCli)
    generator = [IO.Path]::GetFullPath($Generator)
    harness = [IO.Path]::GetFullPath($PSCommandPath)
    powerShellHost = [IO.Path]::GetFullPath((Get-Process -Id $PID).Path)
}
foreach ($tool in $toolPaths.GetEnumerator()) {
    if (-not (Test-Path $tool.Value -PathType Leaf)) {
        Stop-Phase9 "tool-missing" "required validation tool is missing"
    }
    if ($tool.Value.StartsWith($SmokeRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-Phase9 "tool-inside-disposable-root" "validation tools must be staged outside the disposable smoke root"
    }
}
$AccessCli = $toolPaths.accessCli
$TraceMapCli = $toolPaths.traceMapCli
$Generator = $toolPaths.generator

$repo = Join-Path $SmokeRoot "repo"
$databaseRelative = "fixture\synthetic-tracemap.accdb"
$database = Join-Path $repo $databaseRelative
$external = Join-Path $repo "fixture\PrivateWarehouse_92817.accdb"
$mdbRelative = "fixture\synthetic-tracemap.mdb"
$mdb = Join-Path $repo $mdbRelative
$invalidMdbRelative = "fixture\provider-incompatible.mdb"
$invalidMdb = Join-Path $repo $invalidMdbRelative
$canary = Join-Path $SmokeRoot "STARTUP_CANARY_FIRED_92817.txt"
$outA = Join-Path $SmokeRoot "scan-a"
$outB = Join-Path $SmokeRoot "scan-b"
$outConcurrentA = Join-Path $SmokeRoot "scan-concurrent-a"
$outConcurrentB = Join-Path $SmokeRoot "scan-concurrent-b"
$outMdb = Join-Path $SmokeRoot "scan-mdb"
$outInvalidMdb = Join-Path $SmokeRoot "scan-invalid-mdb"
$combined = Join-Path $SmokeRoot "combined.sqlite"
$combinedReport = Join-Path $SmokeRoot "combined-report.md"
$export = Join-Path $SmokeRoot "index-export.json"
$docsOutput = Join-Path $SmokeRoot "evidence-docs"
$vaultOutput = Join-Path $SmokeRoot "vault"
$releaseReviewOutput = Join-Path $SmokeRoot "release-review"

if (Test-Path $SmokeRoot -PathType Container) {
    Remove-Item $SmokeRoot -Recurse -Force -ErrorAction Stop
}
New-Item -ItemType Directory -Path (Split-Path -Parent $database) -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $SmokeRoot ".tracemap-smoke-root"), "TraceMap disposable Access smoke root" + [Environment]::NewLine)
try {
    $generatorArguments = @(
        "-NoProfile",
        "-NonInteractive",
        "-ExecutionPolicy", "Bypass",
        "-Command",
        '& $env:TRACEMAP_ACCESS_GENERATOR -DatabasePath $env:TRACEMAP_ACCESS_DATABASE -CanaryPath $env:TRACEMAP_ACCESS_CANARY'
    )
    $previousGenerator = $env:TRACEMAP_ACCESS_GENERATOR
    $previousDatabase = $env:TRACEMAP_ACCESS_DATABASE
    $previousCanary = $env:TRACEMAP_ACCESS_CANARY
    try {
        $env:TRACEMAP_ACCESS_GENERATOR = $Generator
        $env:TRACEMAP_ACCESS_DATABASE = $database
        $env:TRACEMAP_ACCESS_CANARY = $canary
        & $toolPaths.powerShellHost @generatorArguments *> $null
        if ($LASTEXITCODE -ne 0) {
            Stop-Phase9 "generator-process-failed" "synthetic fixture generator returned a failure"
        }
    }
    finally {
        $env:TRACEMAP_ACCESS_GENERATOR = $previousGenerator
        $env:TRACEMAP_ACCESS_DATABASE = $previousDatabase
        $env:TRACEMAP_ACCESS_CANARY = $previousCanary
    }
}
catch {
    if ($null -eq $phase9Checkpoint -or $phase9Checkpoint.failureClassification -eq "none") {
        Stop-Phase9 "generator-process-failed" "synthetic fixture generation failed"
    }
    throw "synthetic fixture generation failed"
}
if (-not (Test-Path $database -PathType Leaf)) {
    Stop-Phase9 "fixture-database-missing" "synthetic fixture generator produced no database"
}
if (Test-Path $canary) {
    Stop-Phase9 "generation-canary-fired" "startup canary fired during fixture generation cleanup"
}
if ($null -ne $phase9Checkpoint) {
    $phase9Checkpoint.generationCanariesFalse = $true
    $phase9Checkpoint.stopStage = "fixture-provenance"
    Save-Phase9Checkpoint
}

Push-Location $repo
try {
    & git init -b access-smoke *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Phase9 "fixture-git-init-failed" "fixture provenance initialization failed" }
    & git config user.email "access-smoke@example.invalid" *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Phase9 "fixture-git-config-failed" "fixture provenance configuration failed" }
    & git config user.name "TraceMap Access Smoke" *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Phase9 "fixture-git-config-failed" "fixture provenance configuration failed" }
    & git add -- $databaseRelative *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Phase9 "fixture-git-stage-failed" "fixture provenance staging failed" }
    if (Test-Path $mdb) {
        & git add -- $mdbRelative *> $null
        if ($LASTEXITCODE -ne 0) { Stop-Phase9 "fixture-git-stage-failed" "fixture provenance staging failed" }
    }
    try {
        [IO.File]::WriteAllBytes($invalidMdb, [byte[]](1..128))
    }
    catch {
        Stop-Phase9 "fixture-incompatible-input-failed" "bounded incompatible fixture creation failed"
    }
    & git add -- $invalidMdbRelative *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Phase9 "fixture-git-stage-failed" "fixture provenance staging failed" }
    & git commit -m "synthetic Access fixture" *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Phase9 "fixture-git-commit-failed" "fixture provenance commit failed" }
}
finally { Pop-Location }

try {
    $originalHash = (Get-FileHash $database -Algorithm SHA256).Hash.ToLowerInvariant()
}
catch {
    Stop-Phase9 "fixture-hash-failed" "fixture baseline hash failed"
}
try {
    Remove-Item $external -Force -ErrorAction Stop
    Remove-Item $canary -Force -ErrorAction SilentlyContinue
}
catch {
    Stop-Phase9 "fixture-boundary-cleanup-failed" "fixture boundary cleanup failed"
}

try {
$productScanStage = "product-scan"
Set-Phase9Stage $productScanStage
& $AccessCli scan --repo $repo --database ($databaseRelative.Replace('\', '/')) --out $outA --timeout-seconds 120
if ($LASTEXITCODE -ne 0) { throw "first Access scan failed" }
& $AccessCli scan --repo $repo --database ($databaseRelative.Replace('\', '/')) --out $outB --timeout-seconds 120
if ($LASTEXITCODE -ne 0) { throw "second Access scan failed" }

foreach ($relative in @("facts.ndjson", "report.md", "logs\analyzer.log")) {
    $a = Join-Path $outA $relative
    $b = Join-Path $outB $relative
    if ((Get-FileHash $a -Algorithm SHA256).Hash -ne (Get-FileHash $b -Algorithm SHA256).Hash) {
        throw "determinism mismatch: $relative"
    }
}
if (Test-Path $canary) { throw "startup canary fired during scan" }
if ((Get-FileHash $database -Algorithm SHA256).Hash.ToLowerInvariant() -ne $originalHash) { throw "original database changed" }

$required = @("scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs\analyzer.log")
foreach ($relative in $required) {
    if (-not (Test-Path (Join-Path $outA $relative) -PathType Leaf)) { throw "missing artifact: $relative" }
}

$factRows = Get-Content (Join-Path $outA "facts.ndjson") | ForEach-Object { $_ | ConvertFrom-Json }
$databaseMetadata = @($factRows | Where-Object { $_.factType -eq "LegacyDataMetadataDeclared" })
if ($databaseMetadata.Count -ne 1) { throw "expected one Access database metadata fact" }
$macroCount = $databaseMetadata[0].properties.namedMacroCount
if ($null -eq $macroCount) { throw "named macro count was not observed" }
if ($null -ne $databaseMetadata[0].properties.macroLoadedCountUnchanged) { throw "unsupported loaded-macro field was emitted" }
if ($databaseMetadata[0].properties.macroCoverage -ne $phase9Coverage) { throw "macro coverage label mismatch" }
$macroCapability = @($factRows | Where-Object {
    $_.factType -eq "AnalyzerCapabilityDiagnostic" -and $_.properties.capability -eq "macros"
})
if ($macroCapability.Count -ne 1 -or $macroCapability[0].properties.status -ne $phase9Coverage) {
    throw "macro capability label mismatch"
}
$requiredMacroGaps = @(
    "AccessMacroInventoryUnavailable",
    "AccessMacroLoadedStateUnavailable",
    "AccessMacroIdentityUnavailable",
    "AccessMacroEmbeddedInventoryUnavailable",
    "AccessMacroDataInventoryUnavailable",
    "AccessMacroStartupInventoryUnavailable"
)
foreach ($classification in $requiredMacroGaps) {
    if (@($factRows | Where-Object {
        $_.factType -eq "AnalysisGap" -and
        $_.ruleId -eq "legacy.access.macro-gap.v1" -and
        $_.properties.classification -eq $classification
    }).Count -ne 1) { throw "required macro coverage gap missing" }
}
if (@($factRows | Where-Object { $_.factType -eq "AccessMacroDeclared" }).Count -ne 0) {
    throw "count-only product emitted macro identity facts"
}
$declaredRelationships = @($factRows | Where-Object {
    $_.factType -eq "LegacyDataMappingDeclared" -and $_.properties.mappingKind -eq "declared-relationship"
})
if ($declaredRelationships.Count -ne 2) { throw "expected two declared relationship facts" }
if (@($factRows | Where-Object { $_.properties.classification -eq "AccessSchemaAmbiguous" }).Count -ne 0) {
    throw "system relationships produced schema ambiguity gaps"
}
$protectedNameIndex = @($factRows | Where-Object {
    $_.factType -eq "LegacyDataMappingDeclared" -and
    $_.properties.mappingKind -eq "table-index" -and
    $_.properties.objectName -eq "IX_Customers_CustomerNote"
})
if ($protectedNameIndex.Count -ne 1 -or [string]::IsNullOrWhiteSpace($protectedNameIndex[0].properties.fieldStableKeys)) {
    throw "index membership for a redacted field name was not preserved"
}

$markers = @(
    "FixturePassword_92817",
    "private-sql-92817.invalid",
    "PRIVATE_SQL_MARKER_92817",
    "restricted_warehouse",
    "STARTUP_CANARY_FIRED_92817",
    "PHASE7_EVENT_CANARY_FIRED_92817",
    "FORM_CAPTION_MARKER_92817",
    "FORM_EXPRESSION_MARKER_92817",
    "FORM_BUTTON_MARKER_92817",
    "REPORT_CAPTION_MARKER_92817",
    "REPORT_EXPRESSION_MARKER_92817",
    "PHASE8_VBA_CANARY_FIRED_92817",
    "VBA_BUTTON_MARKER_92817",
    "VBA_COMMENT_MARKER_92817",
    "VBA_LITERAL_MARKER_92817",
    "VBA_SQL_MARKER_92817",
    "VBA_PATH_MARKER_92817",
    "VBA_EVAL_MARKER_92817",
    "VBA_RUN_MARKER_92817",
    "VBA_COMMAND_MARKER_92817",
    "EMBEDDED_MACRO_CAPTION_MARKER_92817",
    "Customer Note",
    $SmokeRoot
)
foreach ($file in Get-ChildItem $outA -File -Recurse) {
    $foundMarker = Find-ProtectedMarker -Path $file.FullName -Markers $markers
    if ($null -ne $foundMarker) { throw "protected marker leaked into $($file.Name)" }
}

$concurrentArgumentsA = @("scan", "--repo", $repo, "--database", $databaseRelative.Replace('\', '/'), "--out", $outConcurrentA, "--timeout-seconds", "120")
$concurrentArgumentsB = @("scan", "--repo", $repo, "--database", $databaseRelative.Replace('\', '/'), "--out", $outConcurrentB, "--timeout-seconds", "120")
$concurrentScript = { param($Executable, $Arguments, $InheritedPath) $env:PATH = $InheritedPath; & $Executable @Arguments; $LASTEXITCODE }
$concurrentA = Start-Job -ScriptBlock $concurrentScript -ArgumentList $AccessCli, (, $concurrentArgumentsA), $env:PATH
$concurrentB = Start-Job -ScriptBlock $concurrentScript -ArgumentList $AccessCli, (, $concurrentArgumentsB), $env:PATH
Wait-Job $concurrentA, $concurrentB | Out-Null
$concurrentResultA = @(Receive-Job $concurrentA)
$concurrentResultB = @(Receive-Job $concurrentB)
Remove-Job $concurrentA, $concurrentB -Force
if ($concurrentResultA[-1] -ne 0 -or $concurrentResultB[-1] -ne 0) { throw "concurrent Access scan failed" }
if ((Get-FileHash (Join-Path $outConcurrentA "facts.ndjson") -Algorithm SHA256).Hash -ne (Get-FileHash (Join-Path $outConcurrentB "facts.ndjson") -Algorithm SHA256).Hash) {
    throw "concurrent scan determinism mismatch"
}
if ((Get-FileHash $database -Algorithm SHA256).Hash.ToLowerInvariant() -ne $originalHash) { throw "original database changed during concurrent scans" }
if ($null -ne $phase9Checkpoint) {
    $phase9Checkpoint.namedMacroCountObserved = $true
    $phase9Checkpoint.productContractCorrect = $true
    $phase9Checkpoint.macroIdentityFactsZero = $true
    $phase9Checkpoint.deterministic = $true
    $phase9Checkpoint.extractionCanariesFalse = (-not (Test-Path $canary))
    $phase9Checkpoint.baselineFixtureUnchanged = $true
    Save-Phase9Checkpoint
}

if (Test-Path $mdb) {
    & $AccessCli scan --repo $repo --database ($mdbRelative.Replace('\', '/')) --out $outMdb --timeout-seconds 120
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $outMdb "index.sqlite"))) { throw "provider-compatible MDB scan failed" }
}
$invalidMdbOutput = (& $AccessCli scan --repo $repo --database ($invalidMdbRelative.Replace('\', '/')) --out $outInvalidMdb --timeout-seconds 30 2>&1 | Out-String)
$invalidMdbExit = $LASTEXITCODE
if ($invalidMdbExit -eq 0) {
    $invalidFacts = Get-Content (Join-Path $outInvalidMdb "facts.ndjson") | ForEach-Object { $_ | ConvertFrom-Json }
    if (@($invalidFacts | Where-Object { $_.factType -eq "AnalysisGap" }).Count -eq 0) {
        throw "provider-incompatible MDB produced no failure or coverage gap"
    }
    if (@($invalidFacts | Where-Object { $_.factType -eq "LegacyDataStorageObjectDeclared" }).Count -ne 0) {
        throw "provider-incompatible MDB produced unsupported schema claims"
    }
}
elseif ($invalidMdbOutput -notmatch "Access(DatabaseOpenOrCatalogFailed|WorkerTimeout|WorkerHeartbeatTimeout)") {
    throw "provider-incompatible MDB returned an unexpected failure classification"
}

& $TraceMapCli export --index (Join-Path $outA "index.sqlite") --out $export --format json
if ($LASTEXITCODE -ne 0) { throw "Access index export failed" }
Set-Phase9Stage "report-validation"
$accessReportText = Get-Content (Join-Path $outA "report.md") -Raw
if ($accessReportText -notmatch [Regex]::Escape("Named macro catalog count: ``$macroCount``") -or
    $accessReportText -notmatch [Regex]::Escape("Macro coverage: ``$phase9Coverage``") -or
    $accessReportText -notmatch "AccessMacroLoadedStateUnavailable") {
    throw "Access report omitted Phase 9 count or coverage evidence"
}
if ($null -ne $phase9Checkpoint) {
    $phase9Checkpoint.reportContractCorrect = $true
    Save-Phase9Checkpoint
}

Set-Phase9Stage "combine-validation"
& $TraceMapCli combine --index (Join-Path $outA "index.sqlite") --label access --out $combined
if ($LASTEXITCODE -ne 0) { throw "Access index combine failed" }
& $TraceMapCli report --index $combined --out $combinedReport --format markdown
if ($LASTEXITCODE -ne 0) { throw "combined report failed" }

Set-Phase9Stage "docs-validation"
& $TraceMapCli docs-export --index $combined --out $docsOutput --families legacy,gap,limitation --format markdown,jsonl
if ($LASTEXITCODE -ne 0) { throw "Access evidence docs export failed" }
$docsJsonl = Join-Path $docsOutput "chunks.jsonl"
if (-not (Test-Path $docsJsonl -PathType Leaf)) { throw "Access evidence docs JSONL missing" }
$docsText = Get-Content $docsJsonl -Raw
if ($docsText -notmatch [Regex]::Escape("namedMacroCount:$macroCount") -or
    $docsText -notmatch [Regex]::Escape("macroCoverage:$phase9Coverage") -or
    $docsText -notmatch [Regex]::Escape("legacy.access.macro-gap.v1") -or
    $docsText -notmatch '"claimLevel":"hidden"') {
    throw "Access evidence docs omitted Phase 9 metadata or hidden rule evidence"
}
if ($null -ne $phase9Checkpoint) {
    $phase9Checkpoint.combineContractCorrect = $true
    $phase9Checkpoint.docsContractCorrect = $true
    Save-Phase9Checkpoint
}

Set-Phase9Stage "vault-validation"
& $TraceMapCli vault export --combined-index $combined --out $vaultOutput --minimum-claim-level hidden --format json
if ($LASTEXITCODE -ne 0) { throw "Access vault export failed" }
$vaultJson = Join-Path $vaultOutput "graph.json"
if (-not (Test-Path $vaultJson -PathType Leaf)) { throw "Access vault graph missing" }
$vault = Get-Content $vaultJson -Raw | ConvertFrom-Json
$accessVaultGaps = @($vault.gaps | Where-Object {
    $_.classification -eq "AccessEvidenceConsumerUnsupported" -and
    $_.ruleId -eq "vault-export.gap.access-evidence-consumer-unsupported.v1" -and
    @($_.supportingFactIds).Count -gt 0
})
if ($accessVaultGaps.Count -ne 1) {
    throw "Access vault omitted structured unsupported-consumer evidence"
}
if ($null -ne $phase9Checkpoint) {
    $phase9Checkpoint.vaultContractCorrect = $true
    Save-Phase9Checkpoint
}

Set-Phase9Stage "release-review-validation"
& $TraceMapCli release-review --before $combined --after $combined --out $releaseReviewOutput --format json
if ($LASTEXITCODE -ne 0) { throw "Access release review failed" }
$releaseReviewJson = Join-Path $releaseReviewOutput "release-review.json"
if (-not (Test-Path $releaseReviewJson -PathType Leaf)) { throw "Access release review JSON missing" }
$releaseReview = Get-Content $releaseReviewJson -Raw | ConvertFrom-Json
$accessConsumerGaps = @($releaseReview.gaps | Where-Object {
    $_.gapKind -eq "AccessEvidenceConsumerUnsupported" -and
    $_.ruleId -eq "release.review.section.v1" -and
    $_.classification -eq "PartialAnalysis"
})
if ($accessConsumerGaps.Count -ne 1 -or @($accessConsumerGaps[0].supportingFactIds).Count -eq 0) {
    throw "Access release review omitted structured unsupported-consumer evidence"
}
if ($null -ne $phase9Checkpoint) {
    $phase9Checkpoint.releaseReviewContractCorrect = $true
    Save-Phase9Checkpoint
}

Set-Phase9Stage "safety-check"
$protectedOutputs = @($outA, $outB, $outConcurrentA, $outConcurrentB, $outMdb, $outInvalidMdb, $export, $combined, $combinedReport, $docsOutput, $vaultOutput, $releaseReviewOutput)
foreach ($outputItem in $protectedOutputs) {
    if (-not (Test-Path $outputItem)) { continue }
    $files = if (Test-Path $outputItem -PathType Container) { Get-ChildItem $outputItem -File -Recurse } else { Get-Item $outputItem }
    foreach ($file in $files) {
        $foundMarker = Find-ProtectedMarker -Path $file.FullName -Markers $markers
        if ($foundMarker) { throw "protected marker leaked into downstream output" }
    }
}
if (Test-Path $canary) { throw "Access canary fired during downstream validation" }
if ((Get-FileHash $database -Algorithm SHA256).Hash.ToLowerInvariant() -ne $originalHash) { throw "original database changed during downstream validation" }
if ($null -ne $phase9Checkpoint) {
    $phase9Checkpoint.phase9ConsumerContracts = "completed"
    $phase9Checkpoint.stopStage = "none"
    $phase9Checkpoint.protectedOutputMatchCount = 0
    $phase9Checkpoint.extractionCanariesFalse = $true
    $phase9Checkpoint.baselineFixtureUnchanged = $true
    Save-Phase9Checkpoint
}
}
catch {
    if ($null -ne $phase9Checkpoint -and
        $phase9Checkpoint.phase9ConsumerContracts -ne "completed" -and
        $phase9Checkpoint.failureClassification -eq "none") {
        $failureClassification = switch ($phase9Checkpoint.stopStage) {
            "product-scan" { "product-scan-failed" }
            "report-validation" { "report-validation-failed" }
            "combine-validation" { "combine-validation-failed" }
            "docs-validation" { "docs-validation-failed" }
            "vault-validation" { "vault-validation-failed" }
            "release-review-validation" { "release-review-validation-failed" }
            "safety-check" { "safety-check-failed" }
            default { "phase9-validation-failed" }
        }
        Stop-Phase9 $failureClassification "Phase 9 validation failed at a recorded stage"
    }
    throw
}

$smokeResult = [pscustomobject]@{
    Schema = "tracemap.access-smoke-result.v1"
    OriginalUnchanged = $true
    CanaryFired = $false
    DeterministicFacts = $true
    DeterministicReport = $true
    DeterministicLog = $true
    ConcurrentScans = "passed"
    MdbCompatible = if (Test-Path $mdb) { "passed" } else { "provider-unavailable" }
    MdbIncompatible = "bounded-failure"
    ProtectedMarkersFound = 0
    Export = "passed"
    Combine = "passed"
    CombinedReport = "passed"
    Phase9Checkpoint = if ($null -ne $phase9Checkpoint) { "written" } else { "not-requested" }
}
$smokeResult | ConvertTo-Json -Compress
