param(
    [Parameter(Mandatory = $true)]
    [string]$DatabaseCopyPath,

    [Parameter(Mandatory = $true)]
    [string]$OriginalDatabasePath,

    [Parameter(Mandatory = $true)]
    [string]$FormReportMetadataDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$GenerationCanaryPath,

    [Parameter(Mandatory = $true)]
    [string]$ExtractionCanaryPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{64}$")]
    [string]$RepositoryIdentityHash,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{40}$")]
    [string]$CommitSha,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{64}$")]
    [string]$BaseScanManifestSha256,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{64}$")]
    [string]$DatabaseIdentityHash,

    [ValidateRange(30, 3600)]
    [int]$TimeoutSeconds = 300,

    [switch]$InternalWorker,

    [string]$WorkerProcessMarkerPath = ""
)

# This is a separately reviewed source-export boundary. It deliberately uses
# Access SaveAsText for module serialization and never accesses the VBE object
# model, component collection, source-line APIs, or a form/report instance.
# It is not part of the v0 product COM reader.
$ErrorActionPreference = "Stop"
$MaxModules = 10000
$MaxSourceBytes = 4MB
$MaxSourceLines = 100000
$Utf8NoBom = [Text.UTF8Encoding]::new($false)
$AcModule = 5

if ($InternalWorker -and -not ("TraceMapAccessVbaExportWindow" -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class TraceMapAccessVbaExportWindow {
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}
"@
}

function Stop-Export([string]$Classification) { throw $Classification }

function Close-ComObject([object]$Value) {
    if ($null -ne $Value) {
        try { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($Value) } catch { }
    }
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-BytesSha256([byte[]]$Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return (($algorithm.ComputeHash($Bytes) | ForEach-Object { $_.ToString("x2") }) -join "") }
    finally { $algorithm.Dispose() }
}

function Clear-ReadOnlyAttributes([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    foreach ($item in @(Get-ChildItem -LiteralPath $Path -Recurse -Force -ErrorAction Stop)) {
        if ($item.Attributes -band [IO.FileAttributes]::ReadOnly) {
            $item.Attributes = $item.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly)
        }
    }
}

function Remove-DirectoryWithRetry([string]$Path) {
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        if (-not (Test-Path -LiteralPath $Path)) { return $true }
        try {
            Clear-ReadOnlyAttributes $Path
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        }
        catch { }
        if (-not (Test-Path -LiteralPath $Path)) { return $true }
        if ($attempt -lt 30) { Start-Sleep -Milliseconds 200 }
    }
    return $false
}

function Test-HashUnchanged([string]$Path, [string]$ExpectedHash) {
    try {
        return (Get-Sha256 $Path) -eq $ExpectedHash
    }
    catch { return $false }
}

function Get-AccessProcess([object]$Application) {
    $processId = [uint32]0
    $windowHandle = [IntPtr][long]$Application.hWndAccessApp()
    [void][TraceMapAccessVbaExportWindow]::GetWindowThreadProcessId($windowHandle, [ref]$processId)
    if ($processId -eq 0 -or $processId -gt [uint32]([int]::MaxValue)) { return $null }
    return Get-Process -Id ([int]$processId) -ErrorAction SilentlyContinue
}

function Get-LoadedModuleCount([object]$Application) {
    # This is a count-only canary. It does not enumerate loaded module objects.
    return [int]$Application.Modules.Count
}

function Test-DesignBundle([string]$Directory) {
    $manifestPath = Join-Path $Directory "access-design-manifest.json"
    $recordsPath = Join-Path $Directory "access-design-records.ndjson"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $recordsPath -PathType Leaf)) {
        Stop-Export "AccessVbaMetadataBundleUnavailable"
    }
    $members = @(Get-ChildItem -LiteralPath $Directory -Force)
    if ($members.Count -ne 2) { Stop-Export "AccessVbaMetadataBundleInvalid" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schema -ne "tracemap.access-design-evidence.v1" -or
        $manifest.repository.identityHash -ne $RepositoryIdentityHash -or
        $manifest.repository.commitSha -ne $CommitSha -or
        $manifest.baseScan.manifestSha256 -ne $BaseScanManifestSha256 -or
        $manifest.baseScan.databaseIdentityHash -ne $DatabaseIdentityHash -or
        $manifest.sourceCopy.binding -ne "hash-identical") {
        Stop-Export "AccessVbaMetadataBundleBindingMismatch"
    }
    $recordsHash = Get-Sha256 $recordsPath
    if ($recordsHash -ne $manifest.records.sha256) { Stop-Export "AccessVbaMetadataBundleHashMismatch" }
    return [pscustomobject]@{
        Manifest = $manifest
        RecordsPath = $recordsPath
    }
}

function Add-Record(
    [System.Collections.Generic.List[object]]$Records,
    [string]$Kind,
    [string]$Id,
    [string]$DocumentHash,
    [int]$LineCount,
    [object]$Payload
) {
    $Records.Add([ordered]@{
        schema = "tracemap.access-design-evidence.record.v1"
        kind = $Kind
        recordId = $Id
        parentRecordId = $null
        source = [ordered]@{
            documentRole = if ($Kind -eq "vba-module") { "vba-module-export" } else { "producer-gap" }
            coordinateStatus = if ($Kind -eq "vba-module") { "exact-lines" } else { "unavailable" }
            documentSha256 = if ($Kind -eq "vba-module") { $DocumentHash } else { $null }
            startLine = if ($Kind -eq "vba-module") { 1 } else { $null }
            endLine = if ($Kind -eq "vba-module") { $LineCount } else { $null }
        }
        completeness = if ($Kind -eq "vba-module") { "complete" } else { "partial" }
        payload = $Payload
    })
}

function Get-CodeBehindModule([string]$DesignText, [string]$SurfaceKind) {
    $marker = if ($SurfaceKind -eq "form") { "CodeBehindForm" } else { "CodeBehindReport" }
    $lines = [regex]::Split($DesignText, "`r`n|`n|`r")
    $markerIndex = -1
    for ($index = 0; $index -lt $lines.Length; $index++) {
        if ($lines[$index].Trim() -eq $marker) {
            $markerIndex = $index
            break
        }
    }
    if ($markerIndex -lt 0) {
        return [pscustomobject]@{ Declared = $false; Source = ""; StartLine = 0; HasProcedure = $false }
    }
    $sourceLines = if ($markerIndex + 1 -lt $lines.Length) {
        @($lines[($markerIndex + 1)..($lines.Length - 1)])
    }
    else {
        @()
    }
    $source = [string]::Join("`n", $sourceLines)
    $hasProcedure = [regex]::IsMatch(
        $source,
        "(?im)^\s*(?:(?:Public|Private|Friend|Static)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\s+[A-Za-z_][A-Za-z0-9_]*")
    return [pscustomobject]@{
        Declared = $true
        Source = $source
        StartLine = $markerIndex + 2
        HasProcedure = $hasProcedure
    }
}

$copy = [IO.Path]::GetFullPath($DatabaseCopyPath)
$original = [IO.Path]::GetFullPath($OriginalDatabasePath)
$metadata = [IO.Path]::GetFullPath($FormReportMetadataDirectory)
$output = [IO.Path]::GetFullPath($OutputDirectory)
$generationCanary = [IO.Path]::GetFullPath($GenerationCanaryPath)
$extractionCanary = [IO.Path]::GetFullPath($ExtractionCanaryPath)
if (-not (Test-Path -LiteralPath $copy -PathType Leaf) -or -not (Test-Path -LiteralPath $original -PathType Leaf)) {
    Stop-Export "AccessVbaInputUnavailable"
}
if ([string]::Equals($copy, $original, [StringComparison]::OrdinalIgnoreCase)) {
    Stop-Export "AccessVbaDisposableCopyRequired"
}
if (Test-Path -LiteralPath $output) { Stop-Export "AccessVbaOutputExists" }
if ((Test-Path -LiteralPath $generationCanary) -or (Test-Path -LiteralPath $extractionCanary)) {
    Stop-Export "AccessVbaPreexistingCanary"
}
if (Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue) { Stop-Export "AccessVbaPreexistingProcess" }

if (-not $InternalWorker) {
    $originalBefore = Get-Sha256 $original
    $copyBefore = Get-Sha256 $copy
    if ($originalBefore -ne $copyBefore) { Stop-Export "AccessVbaCopyBindingMismatch" }
    [void](Test-DesignBundle $metadata)
    $marker = Join-Path (Split-Path -Parent $output) ".access-vba-$([Guid]::NewGuid().ToString('N')).process.json"
    $parameters = @{}
    foreach ($entry in $PSBoundParameters.GetEnumerator()) {
        if ($entry.Key -notin @("InternalWorker", "WorkerProcessMarkerPath")) { $parameters[$entry.Key] = $entry.Value }
    }
    $parameters["WorkerProcessMarkerPath"] = $marker
    $job = Start-Job -ScriptBlock {
        param([string]$ScriptPath, [hashtable]$Parameters)
        & $ScriptPath @Parameters -InternalWorker
    } -ArgumentList $PSCommandPath, $parameters
    $completed = Wait-Job -Job $job -Timeout $TimeoutSeconds
    if ($null -eq $completed) {
        Stop-Job -Job $job -ErrorAction SilentlyContinue
        Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $output) { Remove-DirectoryWithRetry $output | Out-Null }
        Remove-Item -LiteralPath $marker -Force -ErrorAction SilentlyContinue
        if (-not (Test-HashUnchanged $original $originalBefore)) { Stop-Export "AccessVbaOriginalSourceChanged" }
        if (-not (Test-HashUnchanged $copy $copyBefore)) { Stop-Export "AccessVbaSuppliedCopyChanged" }
        Stop-Export "AccessVbaTimeout"
    }
    $errors = @()
    $result = @(Receive-Job -Job $job -ErrorVariable +errors -ErrorAction SilentlyContinue)
    $workerReason = [string]$job.ChildJobs[0].JobStateInfo.Reason.Message
    Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        if (-not (Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 250
    }
    if (Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue) {
        Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $output) { Remove-DirectoryWithRetry $output | Out-Null }
        Remove-Item -LiteralPath $marker -Force -ErrorAction SilentlyContinue
        Stop-Export "AccessVbaProcessCleanupFailed"
    }
    Remove-Item -LiteralPath $marker -Force -ErrorAction SilentlyContinue
    if (-not (Test-HashUnchanged $original $originalBefore)) {
        if (Test-Path -LiteralPath $output) { Remove-DirectoryWithRetry $output | Out-Null }
        Stop-Export "AccessVbaOriginalSourceChanged"
    }
    if (-not (Test-HashUnchanged $copy $copyBefore)) {
        if (Test-Path -LiteralPath $output) { Remove-DirectoryWithRetry $output | Out-Null }
        Stop-Export "AccessVbaSuppliedCopyChanged"
    }
    if ((Test-Path -LiteralPath $generationCanary) -or (Test-Path -LiteralPath $extractionCanary)) {
        if (Test-Path -LiteralPath $output) { Remove-DirectoryWithRetry $output | Out-Null }
        Stop-Export "AccessVbaCanaryFired"
    }
    if (-not [string]::IsNullOrWhiteSpace($workerReason)) { Stop-Export $workerReason }
    if ($errors.Count -gt 0) { Stop-Export ([string]$errors[0].Exception.Message) }
    $result | Write-Output
    return
}

$originalBeforeWorker = Get-Sha256 $original
$copyBeforeWorker = Get-Sha256 $copy
$metadataBundle = Test-DesignBundle $metadata
$outputParent = Split-Path -Parent $output
$innerScratch = Join-Path $outputParent ".access-vba-inner-$([Guid]::NewGuid().ToString('N'))"
$workingCopy = Join-Path $innerScratch "working.accdb"
$access = $null
$modules = $null
$project = $null
$dbEngine = $null
$guardDatabase = $null
$createdOutput = $false
$succeeded = $false
try {
    if (Test-Path -LiteralPath $innerScratch) { Stop-Export "AccessVbaInnerScratchExists" }
    New-Item -ItemType Directory -Path $innerScratch -ErrorAction Stop | Out-Null
    Copy-Item -LiteralPath $copy -Destination $workingCopy -ErrorAction Stop
    $workingCopyBeforeWorker = Get-Sha256 $workingCopy
    $access = New-Object -ComObject Access.Application
    $access.AutomationSecurity = 3
    $access.Visible = $false
    $process = Get-AccessProcess $access
    if ($null -eq $process) { Stop-Export "AccessVbaProcessIdentityUnavailable" }
    if (-not [string]::IsNullOrWhiteSpace($WorkerProcessMarkerPath)) {
        [IO.File]::WriteAllText($WorkerProcessMarkerPath, ([ordered]@{
            processId = [int]$process.Id
            startTimeUtcTicks = [long]$process.StartTime.ToUniversalTime().Ticks
        } | ConvertTo-Json -Compress), $Utf8NoBom)
    }
    try {
        # StartupForm is removed only from the bounded inner scratch copy before
        # OpenCurrentDatabase. DAO does not open or render that surface.
        $dbEngine = $access.DBEngine
        $guardDatabase = $dbEngine.OpenDatabase($workingCopy)
        try { $guardDatabase.Properties.Delete("StartupForm") } catch { }
        $guardDatabase.Close()
    }
    finally {
        Close-ComObject $guardDatabase; $guardDatabase = $null
        Close-ComObject $dbEngine; $dbEngine = $null
    }
    $access.OpenCurrentDatabase($workingCopy, $true)
    if ([bool]$access.Visible) { Stop-Export "AccessVbaVisibleUiDetected" }
    if (Test-Path -LiteralPath $generationCanary) { Stop-Export "AccessVbaGenerationCanaryFired" }
    if (Test-Path -LiteralPath $extractionCanary) { Stop-Export "AccessVbaExtractionCanaryFired" }
    if (-not (Test-HashUnchanged $original $originalBeforeWorker)) { Stop-Export "AccessVbaOriginalSourceChanged" }
    if (-not (Test-HashUnchanged $copy $copyBeforeWorker)) { Stop-Export "AccessVbaSuppliedCopyChanged" }
    $loadedBaseline = Get-LoadedModuleCount $access
    $project = $access.CurrentProject
    $modules = $project.AllModules
    if ([int]$modules.Count -gt $MaxModules) { Stop-Export "AccessVbaModuleLimitReached" }
    $moduleCount = [int]$modules.Count

    $privateRoot = Join-Path $output "private-access-source"
    $normalizedRoot = Join-Path $output "normalized-design-evidence"
    New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
    $records = [System.Collections.Generic.List[object]]::new()
    $privateArtifacts = [System.Collections.Generic.List[object]]::new()
    $metadataRecords = [System.Collections.Generic.List[object]]::new()
    $surfaceByRecordId = @{}
    $moduleNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $vbaModuleCount = 0
    $standaloneModuleFileCount = 0
    $codeBehindOrdinal = 0
    $sourceGapOrdinal = 0
    $designOrdinal = 0
    foreach ($line in @(Get-Content -LiteralPath $metadataBundle.RecordsPath)) {
        if ([string]::IsNullOrWhiteSpace($line)) { Stop-Export "AccessVbaMetadataBundleInvalid" }
        # Windows PowerShell 5.1 lacks the hashtable conversion switch.
        # PSCustomObject preserves property access and input property order here.
        $record = $line | ConvertFrom-Json
        $metadataRecords.Add($record)
        $records.Add($record)
        if ($record.kind -eq "catalog-object" -and $record.payload.objectRole -in @("form", "report")) {
            $surfaceByRecordId[[string]$record.recordId] = [pscustomobject]@{
                Name = [string]$record.payload.identity
                Kind = [string]$record.payload.objectRole
            }
        }
        if ($record.kind -eq "ui-design-document") {
            $designText = [string]$record.payload.designText
            $designHash = [string]$record.payload.documentSha256
            $designBytes = $Utf8NoBom.GetBytes($designText)
            if ((Get-BytesSha256 $designBytes) -ne $designHash) { Stop-Export "AccessVbaMetadataBundleHashMismatch" }
            $artifactName = "design-$($designOrdinal.ToString('D6')).txt"
            [IO.File]::WriteAllBytes((Join-Path $privateRoot $artifactName), $designBytes)
            $privateArtifacts.Add([ordered]@{
                artifact = $artifactName
                documentRole = [string]$record.payload.documentRole
                sha256 = $designHash
                lineCount = [int]$record.payload.lineCount
            })
            $designOrdinal++
        }
    }

    foreach ($record in @($metadataRecords | Where-Object kind -eq "ui-design-document" | Sort-Object recordId)) {
        $surface = $surfaceByRecordId[[string]$record.parentRecordId]
        $surfaceKind = [string]$record.payload.documentRole
        if ($null -eq $surface -or $surface.Kind -ne $surfaceKind) {
            Add-Record $records "source-gap" "gap-code-behind-owner-$($sourceGapOrdinal.ToString('D6'))" "" 0 ([ordered]@{
                classification = "AccessVbaCodeBehindOwnerUnavailable"
                affectedScope = "ui-surface"
                coverageCategory = "source-unavailable"
            })
            $sourceGapOrdinal++
            continue
        }
        $codeBehind = Get-CodeBehindModule ([string]$record.payload.designText) $surfaceKind
        if (-not $codeBehind.Declared) { continue }
        $moduleName = if ($surfaceKind -eq "form") { "Form_$($surface.Name)" } else { "Report_$($surface.Name)" }
        if ([string]::IsNullOrWhiteSpace($codeBehind.Source)) {
            Add-Record $records "source-gap" "gap-code-behind-source-$($sourceGapOrdinal.ToString('D6'))" "" 0 ([ordered]@{
                classification = "AccessVbaCodeBehindSourceUnavailable"
                affectedScope = $surfaceKind
                coverageCategory = "source-unavailable"
            })
            $sourceGapOrdinal++
            continue
        }
        if (-not $codeBehind.HasProcedure) {
            Add-Record $records "source-gap" "gap-code-behind-procedure-$($sourceGapOrdinal.ToString('D6'))" "" 0 ([ordered]@{
                classification = "AccessVbaCodeBehindProcedureUnavailable"
                affectedScope = $surfaceKind
                coverageCategory = "source-unavailable"
            })
            $sourceGapOrdinal++
        }
        if (-not $moduleNames.Add($moduleName)) { continue }
        $sourceBytes = $Utf8NoBom.GetBytes([string]$codeBehind.Source)
        if ($sourceBytes.LongLength -gt $MaxSourceBytes) { Stop-Export "AccessVbaSourceLimitReached" }
        $lineCount = if ($codeBehind.Source.Length -eq 0) { 0 } else { [regex]::Matches($codeBehind.Source, "`n").Count + 1 }
        if ($lineCount -gt $MaxSourceLines) { Stop-Export "AccessVbaSourceLimitReached" }
        $sourceHash = Get-BytesSha256 $sourceBytes
        Add-Record $records "vba-module" "vba-code-behind-$($codeBehindOrdinal.ToString('D6'))" $sourceHash $lineCount ([ordered]@{
            moduleRole = $surfaceKind
            identity = $moduleName
            moduleKind = $surfaceKind
            sourceText = [string]$codeBehind.Source
            sourceSha256 = $sourceHash
            lineCount = $lineCount
            coordinateBasis = "module-relative"
            sourceDocumentSha256 = [string]$record.payload.documentSha256
            sourceDocumentStartLine = [int]$codeBehind.StartLine
            extractionMechanism = "save-as-text-code-behind"
        })
        $codeBehindOrdinal++
        $vbaModuleCount++
    }
    for ($index = 0; $index -lt $moduleCount; $index++) {
        $module = $null
        try {
            $module = $modules.Item($index)
            $name = [string]$module.Name
            if ([string]::IsNullOrWhiteSpace($name)) { Stop-Export "AccessVbaModuleIdentityUnavailable" }
            if (-not $moduleNames.Add($name)) { continue }
            $recordId = "vba-module-$($index.ToString('D6'))"
            $rawPath = Join-Path $privateRoot "$recordId.txt"
            $access.SaveAsText($AcModule, $name, $rawPath)
            if ([bool]$access.Visible) { Stop-Export "AccessVbaVisibleUiDetected" }
            if (Test-Path -LiteralPath $generationCanary) { Stop-Export "AccessVbaGenerationCanaryFired" }
            if (Test-Path -LiteralPath $extractionCanary) { Stop-Export "AccessVbaExtractionCanaryFired" }
            if ((Get-LoadedModuleCount $access) -ne $loadedBaseline) { Stop-Export "AccessVbaLoadedStateChanged" }
            $bytes = [IO.File]::ReadAllBytes($rawPath)
            if ($bytes.LongLength -gt $MaxSourceBytes) { Stop-Export "AccessVbaSourceLimitReached" }
            $source = [IO.File]::ReadAllText($rawPath)
            $lineCount = if ($source.Length -eq 0) { 0 } else { [regex]::Matches($source, "`n").Count + 1 }
            if ($lineCount -gt $MaxSourceLines) { Stop-Export "AccessVbaSourceLimitReached" }
            $hash = Get-BytesSha256 ($Utf8NoBom.GetBytes($source))
            $privateArtifacts.Add([ordered]@{
                artifact = "$recordId.txt"
                documentRole = "vba-module"
                sha256 = $hash
                lineCount = $lineCount
            })
            $standaloneModuleFileCount++
            $moduleKind = if ($name.StartsWith("Form_", [StringComparison]::OrdinalIgnoreCase)) { "form" }
                elseif ($name.StartsWith("Report_", [StringComparison]::OrdinalIgnoreCase)) { "report" }
                else { "standard" }
            Add-Record $records "vba-module" $recordId $hash $lineCount ([ordered]@{
                moduleRole = $moduleKind
                identity = $name
                moduleKind = $moduleKind
                sourceText = $source
                sourceSha256 = $hash
                lineCount = $lineCount
                coordinateBasis = "module-relative"
            })
            $vbaModuleCount++
        }
        finally { Close-ComObject $module }
    }
    Close-ComObject $modules; $modules = $null
    Close-ComObject $project; $project = $null
    $access.CloseCurrentDatabase()
    $access.Quit(2)
    Close-ComObject $access; $access = $null
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    if (-not (Test-HashUnchanged $original $originalBeforeWorker)) { Stop-Export "AccessVbaOriginalSourceChanged" }
    if (-not (Test-HashUnchanged $copy $copyBeforeWorker)) { Stop-Export "AccessVbaSuppliedCopyChanged" }
    $copyAfterWorker = Get-Sha256 $copy
    $suppliedCopyMutationOutcome = if ($copyAfterWorker -eq $copyBeforeWorker) {
        "AccessVbaWorkingCopyUnchanged"
    }
    else {
        "AccessVbaWorkingCopyChanged"
    }
    $workingCopyAfterWorker = Get-Sha256 $workingCopy
    $workingCopyMutationOutcome = if ($workingCopyAfterWorker -eq $workingCopyBeforeWorker) {
        "AccessVbaWorkingCopyUnchanged"
    }
    else {
        "AccessVbaWorkingCopyChanged"
    }
    if (-not (Remove-DirectoryWithRetry $innerScratch)) { Stop-Export "AccessVbaInnerScratchCleanupFailed" }

    $ordered = @($records | Sort-Object { $_.kind }, { $_.recordId })
    $lines = @($ordered | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 20 })
    $text = if ($lines.Count -eq 0) { "" } else { ($lines -join "`n") + "`n" }
    $recordBytes = $Utf8NoBom.GetBytes($text)
    $counts = [ordered]@{}
    foreach ($group in $ordered | Group-Object { $_.kind } | Sort-Object Name) { $counts[$group.Name] = $group.Count }
    New-Item -ItemType Directory -Path $normalizedRoot -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $normalizedRoot "access-design-records.ndjson"), $recordBytes)
    $manifest = [ordered]@{
        schema = "tracemap.access-design-evidence.v1"
        producer = [ordered]@{ id = "tracemap-access-windows-export"; version = "1.2.0"; mechanism = "access-save-as-text-vba" }
        repository = [ordered]@{ identityHash = $RepositoryIdentityHash; commitSha = $CommitSha }
        baseScan = [ordered]@{ manifestSha256 = $BaseScanManifestSha256; databaseIdentityHash = $DatabaseIdentityHash }
        sourceCopy = [ordered]@{ sha256 = $copyBeforeWorker; binding = "hash-identical" }
        records = [ordered]@{ sha256 = (Get-BytesSha256 $recordBytes); count = $ordered.Count; countsByKind = $counts }
        capabilities = [ordered]@{ coordinates = "mixed"; catalogCompleteness = "declared-partial"; identityDisclosure = "hash-only" }
    }
    [IO.File]::WriteAllText((Join-Path $normalizedRoot "access-design-manifest.json"), ($manifest | ConvertTo-Json -Compress -Depth 20), $Utf8NoBom)
    $rawManifest = [ordered]@{
        schema = "tracemap.access-vba-private-source.v1"
        exporterVersion = "1.2.0"
        sourceCopySha256 = $copyBeforeWorker
        suppliedCopyPreExportSha256 = $copyBeforeWorker
        suppliedCopyPostExportSha256 = $copyAfterWorker
        suppliedCopyMutationOutcome = $suppliedCopyMutationOutcome
        workingCopyPreExportSha256 = $workingCopyBeforeWorker
        workingCopyPostExportSha256 = $workingCopyAfterWorker
        workingCopyMutationOutcome = $workingCopyMutationOutcome
        artifactCount = $privateArtifacts.Count
        moduleFileCount = $standaloneModuleFileCount
        vbaModuleRecordCount = $vbaModuleCount
        standaloneModuleFileCount = $standaloneModuleFileCount
        codeBehindModuleCount = $codeBehindOrdinal
        formReportDesignFileCount = $designOrdinal
        artifacts = @($privateArtifacts | Sort-Object artifact)
        sourceArtifactOnly = $true
        limitations = @("private-source", "not-a-standard-tracemap-artifact", "no-runtime-claim")
    }
    [IO.File]::WriteAllText((Join-Path $privateRoot "source-manifest.json"), ($rawManifest | ConvertTo-Json -Compress -Depth 20), $Utf8NoBom)
    $createdOutput = $true
    $succeeded = $true
}
finally {
    Close-ComObject $modules
    Close-ComObject $project
    Close-ComObject $guardDatabase
    Close-ComObject $dbEngine
    if ($null -ne $access) {
        try { $access.CloseCurrentDatabase() } catch { }
        try { $access.Quit(2) } catch { }
    }
    Close-ComObject $access
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    if (Test-Path -LiteralPath $innerScratch) { Remove-DirectoryWithRetry $innerScratch | Out-Null }
    if (-not $succeeded -and (Test-Path -LiteralPath $output)) { Remove-DirectoryWithRetry $output | Out-Null }
}
