param(
    [Parameter(Mandatory = $true)]
    [string]$AccessCli,

    [Parameter(Mandatory = $true)]
    [string]$TraceMapCli,

    [Parameter(Mandatory = $true)]
    [string]$Generator,

    [string]$SmokeRoot = "C:\TraceMapAccessSmoke"
)

$ErrorActionPreference = "Stop"
$SmokeRoot = [IO.Path]::GetFullPath($SmokeRoot)
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

Remove-Item $SmokeRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path (Split-Path -Parent $database) -Force | Out-Null
& $Generator -DatabasePath $database -CanaryPath $canary
if (Test-Path $canary) { throw "startup canary fired during fixture generation cleanup" }

Push-Location $repo
try {
    & git init -b access-smoke | Out-Null
    & git config user.email "access-smoke@example.invalid"
    & git config user.name "TraceMap Access Smoke"
    & git add -- $databaseRelative
    if (Test-Path $mdb) { & git add -- $mdbRelative }
    [IO.File]::WriteAllBytes($invalidMdb, [byte[]](1..128))
    & git add -- $invalidMdbRelative
    & git commit -m "synthetic Access fixture" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "fixture Git commit failed" }
}
finally { Pop-Location }

$originalHash = (Get-FileHash $database -Algorithm SHA256).Hash.ToLowerInvariant()
Remove-Item $external -Force
Remove-Item $canary -Force -ErrorAction SilentlyContinue

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
    "Customer Note",
    $SmokeRoot
)
foreach ($file in Get-ChildItem $outA -File -Recurse) {
    $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($file.FullName))
    foreach ($marker in $markers) {
        if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "protected marker leaked into $($file.Name)"
        }
    }
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
& $TraceMapCli combine --index (Join-Path $outA "index.sqlite") --label access --out $combined
if ($LASTEXITCODE -ne 0) { throw "Access index combine failed" }
& $TraceMapCli report --index $combined --out $combinedReport --format markdown
if ($LASTEXITCODE -ne 0) { throw "combined report failed" }

[pscustomobject]@{
    Schema = "tracemap.access-smoke-result.v1"
    DatabaseHash = $originalHash
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
} | ConvertTo-Json -Compress
