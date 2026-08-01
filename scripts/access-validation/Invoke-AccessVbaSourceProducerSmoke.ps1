param(
    [Parameter(Mandatory = $true)]
    [string]$Generator,

    [Parameter(Mandatory = $true)]
    [string]$MetadataProducer,

    [Parameter(Mandatory = $true)]
    [string]$VbaProducer,

    [Parameter(Mandatory = $true)]
    [string]$SmokeRoot
)

# Windows-only synthetic validation for the separately reviewed VBA exporter.
# It never accepts a representative database and deletes all protected output.
$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath($SmokeRoot)
$original = Join-Path $root "fixture.accdb"
$metadataCopy = Join-Path $root "metadata-copy.accdb"
$vbaCopy = Join-Path $root "vba-copy.accdb"
$metadata = Join-Path $root "metadata"
$output = Join-Path $root "vba-output"
$generationCanary = Join-Path $root "generation-canary.txt"
$metadataCanary = Join-Path $root "metadata-canary.txt"
$vbaCanary = Join-Path $root "vba-canary.txt"
$zeroHash = "0" * 64
$commit = "0" * 40
try {
    if (Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue) { throw "AccessVbaPreexistingProcess" }
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    & $Generator -DatabasePath $original -CanaryPath $generationCanary *> $null
    if (Test-Path -LiteralPath $generationCanary) { throw "AccessVbaGenerationCanaryFired" }
    Copy-Item -LiteralPath $original -Destination $metadataCopy
    & $MetadataProducer `
        -DatabaseCopyPath $metadataCopy `
        -OriginalDatabasePath $original `
        -OutputDirectory $metadata `
        -CanaryPath $metadataCanary `
        -RepositoryIdentityHash $zeroHash `
        -CommitSha $commit `
        -BaseScanManifestSha256 $zeroHash `
        -DatabaseIdentityHash $zeroHash `
        -TimeoutSeconds 240 *> $null
    if (Test-Path -LiteralPath $metadataCanary) { throw "AccessVbaMetadataCanaryFired" }
    Copy-Item -LiteralPath $original -Destination $vbaCopy
    $originalHash = (Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
    $vbaCopyHash = (Get-FileHash -LiteralPath $vbaCopy -Algorithm SHA256).Hash
    & $VbaProducer `
        -DatabaseCopyPath $vbaCopy `
        -OriginalDatabasePath $original `
        -FormReportMetadataDirectory $metadata `
        -OutputDirectory $output `
        -GenerationCanaryPath $generationCanary `
        -ExtractionCanaryPath $vbaCanary `
        -RepositoryIdentityHash $zeroHash `
        -CommitSha $commit `
        -BaseScanManifestSha256 $zeroHash `
        -DatabaseIdentityHash $zeroHash `
        -TimeoutSeconds 240 *> $null
    if (Test-Path -LiteralPath $generationCanary -or Test-Path -LiteralPath $vbaCanary) { throw "AccessVbaCanaryFired" }
    if ((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash -ne $originalHash) {
        throw "AccessVbaOriginalSourceChanged"
    }
    $normalized = Join-Path $output "normalized-design-evidence"
    $raw = Join-Path $output "private-access-source"
    $manifestPath = Join-Path $normalized "access-design-manifest.json"
    $recordsPath = Join-Path $normalized "access-design-records.ndjson"
    if (-not (Test-Path -LiteralPath $manifestPath) -or -not (Test-Path -LiteralPath $recordsPath) -or
        -not (Test-Path -LiteralPath (Join-Path $raw "source-manifest.json"))) { throw "AccessVbaOutputMissing" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $records = @(Get-Content -LiteralPath $recordsPath | ForEach-Object { $_ | ConvertFrom-Json })
    if ($manifest.producer.mechanism -ne "access-save-as-text-vba" -or
        @($records | Where-Object kind -eq "vba-module").Count -le 0 -or
        (Get-Content -LiteralPath (Join-Path $raw "source-manifest.json") -Raw | ConvertFrom-Json).formReportDesignFileCount -le 0) { throw "AccessVbaOutputInvalid" }
    $copyAfter = (Get-FileHash -LiteralPath $vbaCopy -Algorithm SHA256).Hash
    $expectedWorkingCopyOutcome = if ($copyAfter -eq $vbaCopyHash) {
        "AccessVbaWorkingCopyUnchanged"
    }
    else {
        "AccessVbaWorkingCopyChanged"
    }
    if ($manifest.sourceCopy.sha256 -ne $vbaCopyHash -or
        $manifest.workingCopy.preExportSha256 -ne $vbaCopyHash -or
        $manifest.workingCopy.postExportSha256 -ne $copyAfter -or
        $manifest.workingCopy.mutationOutcome -ne $expectedWorkingCopyOutcome) {
        throw "AccessVbaWorkingCopyOutcomeInvalid"
    }
    if (Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue) { throw "AccessVbaProcessCleanupFailed" }
    Write-Output "access-vba-source-smoke=completed;originalSourceUnchanged=true;workingCopyOutcome=$expectedWorkingCopyOutcome;canariesClear=true;processCleanup=true"
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
}
