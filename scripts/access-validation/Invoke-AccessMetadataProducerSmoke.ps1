param(
    [Parameter(Mandatory = $true)]
    [string]$Generator,

    [Parameter(Mandatory = $true)]
    [string]$Producer,

    [Parameter(Mandatory = $true)]
    [string]$SmokeRoot
)

$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath($SmokeRoot)
$original = Join-Path $root "fixture.accdb"
$copy = Join-Path $root "fixture-copy.accdb"
$canary = Join-Path $root "metadata-canary.txt"
$generationCanary = Join-Path $root "generation-canary.txt"
$bundle = Join-Path $root "design-evidence"
$zeroHash = "0" * 64
$commit = "0" * 40
try {
    if (Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue) {
        throw "AccessMetadataPreexistingProcess"
    }
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    & $Generator -DatabasePath $original -CanaryPath $generationCanary *> $null
    if (Test-Path -LiteralPath $generationCanary) { throw "AccessMetadataGenerationCanaryFired" }
    Copy-Item -LiteralPath $original -Destination $copy
    $originalHash = (Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
    $copyHash = (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash
    if ($originalHash -ne $copyHash) { throw "AccessMetadataCopyMismatch" }

    $producerJob = Start-Job -ScriptBlock {
        param($Script, $Copy, $Original, $Bundle, $Canary, $RepositoryHash, $Commit, $BaseHash, $DatabaseHash)
        & $Script `
            -DatabaseCopyPath $Copy `
            -OriginalDatabasePath $Original `
            -OutputDirectory $Bundle `
            -CanaryPath $Canary `
            -RepositoryIdentityHash $RepositoryHash `
            -CommitSha $Commit `
            -BaseScanManifestSha256 $BaseHash `
            -DatabaseIdentityHash $DatabaseHash *> $null
    } -ArgumentList $Producer, $copy, $original, $bundle, $canary, $zeroHash, $commit, $zeroHash, $zeroHash
    try {
        if ($null -eq (Wait-Job -Job $producerJob -Timeout 300)) {
            Stop-Job -Job $producerJob -ErrorAction SilentlyContinue
            Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue |
                Stop-Process -Force -ErrorAction SilentlyContinue
            throw "AccessMetadataProducerTimeout"
        }
        Receive-Job -Job $producerJob -ErrorAction Stop *> $null
        if ($producerJob.State -ne "Completed") { throw "AccessMetadataProducerFailed" }
    }
    finally {
        Remove-Job -Job $producerJob -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $canary) { throw "AccessMetadataCanaryFired" }
    if ((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash -ne $originalHash -or
        (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $copyHash) {
        throw "AccessMetadataSourceChanged"
    }
    $manifestPath = Join-Path $bundle "access-design-manifest.json"
    $recordsPath = Join-Path $bundle "access-design-records.ndjson"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $recordsPath -PathType Leaf)) {
        throw "AccessMetadataBundleMissing"
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schema -ne "tracemap.access-design-evidence.v1" -or
        $manifest.producer.mechanism -ne "access-save-as-text-metadata" -or
        [int]$manifest.records.count -le 0 -or
        [int]$manifest.records.countsByKind.'ui-design-document' -lt 3) {
        throw "AccessMetadataBundleInvalid"
    }
    $recordsHash = (Get-FileHash -LiteralPath $recordsPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($recordsHash -ne $manifest.records.sha256) { throw "AccessMetadataBundleHashMismatch" }
    $records = @(Get-Content -LiteralPath $recordsPath | ForEach-Object { $_ | ConvertFrom-Json })
    if (@($records | Where-Object kind -eq "ui-design-document").Count -lt 3 -or
        @($records | Where-Object {
            $_.kind -in @("vba-module", "macro-inventory") -or
            $_.payload.PSObject.Properties.Name -contains "sourceText"
        }).Count -ne 0) {
        throw "AccessMetadataBoundaryInvalid"
    }
    if (Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue) {
        throw "AccessMetadataProcessCleanupFailed"
    }
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}
