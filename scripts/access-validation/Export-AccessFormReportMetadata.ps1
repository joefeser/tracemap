param(
    [Parameter(Mandatory = $true)]
    [string]$DatabaseCopyPath,

    [Parameter(Mandatory = $true)]
    [string]$OriginalDatabasePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$CanaryPath,

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

$ErrorActionPreference = "Stop"
$MaxObjects = 10000
$MaxTextBytes = 4MB
$MaxTextLines = 100000
$Utf8NoBom = [Text.UTF8Encoding]::new($false)

if ($InternalWorker -and -not ("TraceMapAccessWindowProcess" -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class TraceMapAccessWindowProcess
{
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}
"@
}

function Stop-Export([string]$Classification) {
    throw $Classification
}

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
    try {
        return (($algorithm.ComputeHash($Bytes) | ForEach-Object { $_.ToString("x2") }) -join "")
    }
    finally {
        $algorithm.Dispose()
    }
}

function New-AccessProcessIdentity([object]$Process) {
    return [pscustomobject]@{
        processId = [int]$Process.Id
        startTimeUtcTicks = [long]$Process.StartTime.ToUniversalTime().Ticks
    }
}

function ConvertTo-AccessProcessIdentities([object[]]$Candidates) {
    $result = [System.Collections.Generic.List[object]]::new()
    foreach ($candidate in $Candidates) {
        $processId = 0
        $startTimeUtcTicks = [long]0
        if ($null -eq $candidate -or
            -not [int]::TryParse([string]$candidate.processId, [ref]$processId) -or
            -not [long]::TryParse([string]$candidate.startTimeUtcTicks, [ref]$startTimeUtcTicks) -or
            $processId -le 0 -or
            $startTimeUtcTicks -le 0) {
            continue
        }
        $result.Add([pscustomobject]@{
            processId = $processId
            startTimeUtcTicks = $startTimeUtcTicks
        })
    }
    return @($result)
}

function Get-OwnedAccessProcesses([object[]]$ProcessIdentities) {
    $result = [System.Collections.Generic.List[object]]::new()
    foreach ($identity in @(ConvertTo-AccessProcessIdentities $ProcessIdentities)) {
        $process = Get-Process -Id ([int]$identity.processId) -ErrorAction SilentlyContinue
        if ($null -eq $process -or
            -not [string]::Equals($process.ProcessName, "MSACCESS", [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }
        try {
            if ([long]$process.StartTime.ToUniversalTime().Ticks -eq [long]$identity.startTimeUtcTicks) {
                $result.Add($process)
            }
        }
        catch { }
    }
    return @($result)
}

function Get-AccessApplicationProcess([object]$Application) {
    $processId = [uint32]0
    # Access exposes hWndAccessApp as a method. PowerShell 7 returns a PSMethod
    # object when it is read like a property, which cannot be converted to the
    # native window handle expected by GetWindowThreadProcessId.
    $windowHandle = [IntPtr][long]$Application.hWndAccessApp()
    [void][TraceMapAccessWindowProcess]::GetWindowThreadProcessId($windowHandle, [ref]$processId)
    if ($processId -eq 0 -or $processId -gt [uint32]([int]::MaxValue)) { return $null }
    return Get-Process -Id ([int]$processId) -ErrorAction SilentlyContinue
}

function Get-LoadedState([object]$Application) {
    $forms = 0
    $reports = 0
    $currentProject = $null
    $allForms = $null
    $allReports = $null
    try {
        $currentProject = $Application.CurrentProject
        $allForms = $currentProject.AllForms
        if ([int]$allForms.Count -gt $MaxObjects) { Stop-Export "AccessMetadataObjectLimitReached" }
        for ($index = 0; $index -lt [int]$allForms.Count; $index++) {
            $item = $null
            try {
                $item = $allForms.Item($index)
                if ([bool]$item.IsLoaded) { $forms++ }
            }
            finally { Close-ComObject $item }
        }
        $allReports = $currentProject.AllReports
        if ([int]$allReports.Count -gt $MaxObjects) { Stop-Export "AccessMetadataObjectLimitReached" }
        for ($index = 0; $index -lt [int]$allReports.Count; $index++) {
            $item = $null
            try {
                $item = $allReports.Item($index)
                if ([bool]$item.IsLoaded) { $reports++ }
            }
            finally { Close-ComObject $item }
        }
        return "$forms`:$reports"
    }
    finally {
        Close-ComObject $allReports
        Close-ComObject $allForms
        Close-ComObject $currentProject
    }
}

function New-Source([string]$Role, [string]$Status, [string]$Hash, [int]$Start, [int]$End) {
    $source = [ordered]@{
        documentRole = $Role
        coordinateStatus = $Status
    }
    if ($Hash) { $source.documentSha256 = $Hash }
    if ($Start -gt 0) { $source.startLine = $Start }
    if ($End -gt 0) { $source.endLine = $End }
    return $source
}

function Add-Record(
    [System.Collections.Generic.List[object]]$Records,
    [string]$Kind,
    [string]$Id,
    [string]$ParentId,
    [string]$DocumentRole,
    [string]$CoordinateStatus,
    [string]$DocumentHash,
    [int]$StartLine,
    [int]$EndLine,
    [string]$Completeness,
    [object]$Payload
) {
    $Records.Add([ordered]@{
        schema = "tracemap.access-design-evidence.record.v1"
        kind = $Kind
        recordId = $Id
        parentRecordId = if ($ParentId) { $ParentId } else { $null }
        source = New-Source $DocumentRole $CoordinateStatus $DocumentHash $StartLine $EndLine
        completeness = $Completeness
        payload = $Payload
    })
}

function Get-StaticQueryOutputNames([string]$Sql) {
    if ($Sql.Length -gt $MaxTextBytes) { return @() }
    $match = [regex]::Match(
        $Sql,
        "(?is)^\s*(?:PARAMETERS\b.*?;\s*)?SELECT\s+(?:(?:DISTINCT|DISTINCTROW|TOP\s+\d+(?:\s+PERCENT)?)\s+)*(?<list>.*?)\s+\bFROM\b")
    if (-not $match.Success -or $match.Groups["list"].Value.Contains("*")) { return @() }
    $result = [System.Collections.Generic.List[string]]::new()
    foreach ($rawItem in $match.Groups["list"].Value.Split(",")) {
        $item = $rawItem.Trim()
        if ($item.Contains("(") -and $item -notmatch "(?is)\s+AS\s+(?:\[(?<alias>[^\]]+)\]|(?<alias>[A-Za-z_][A-Za-z0-9_ ]*))\s*$") {
            continue
        }
        $alias = [regex]::Match($item, "(?is)\s+AS\s+(?:\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][A-Za-z0-9_ ]*))\s*$")
        if ($alias.Success) {
            $result.Add($alias.Groups["name"].Value.Trim())
            continue
        }
        $direct = [regex]::Match(
            $item,
            "(?is)^(?:(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_.$]*)\s*\.\s*)?(?:\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][A-Za-z0-9_ ]*))$")
        if ($direct.Success) { $result.Add($direct.Groups["name"].Value.Trim()) }
    }
    return @($result | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
}

$copy = [IO.Path]::GetFullPath($DatabaseCopyPath)
$original = [IO.Path]::GetFullPath($OriginalDatabasePath)
$output = [IO.Path]::GetFullPath($OutputDirectory)
$canary = [IO.Path]::GetFullPath($CanaryPath)
if (-not (Test-Path -LiteralPath $copy -PathType Leaf) -or
    -not (Test-Path -LiteralPath $original -PathType Leaf)) {
    Stop-Export "AccessMetadataInputUnavailable"
}
if ([string]::Equals($copy, $original, [StringComparison]::OrdinalIgnoreCase)) {
    Stop-Export "AccessMetadataDisposableCopyRequired"
}
if (Test-Path -LiteralPath $output) {
    Stop-Export "AccessMetadataOutputExists"
}
if (Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue) {
    Stop-Export "AccessMetadataPreexistingProcess"
}

# Keep the public producer invocation bounded even when Access blocks inside a
# synchronous COM call such as SaveAsText. The internal worker owns the normal
# COM cleanup path; the supervising invocation additionally kills a timed-out
# Access process and removes any partial output/scratch state before failing.
if (-not $InternalWorker) {
    $originalBeforeSupervision = Get-Sha256 $original
    $copyBeforeSupervision = Get-Sha256 $copy
    if (-not [string]::Equals(
        $copyBeforeSupervision,
        $originalBeforeSupervision,
        [StringComparison]::OrdinalIgnoreCase)) {
        Stop-Export "AccessMetadataCopyBindingMismatch"
    }
    $outputParent = Split-Path -Parent $output
    New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
    $workerProcessMarker = Join-Path $outputParent ".$([IO.Path]::GetFileName($output)).worker-$([Guid]::NewGuid().ToString('N')).process.json"
    $workerHostMarker = "$workerProcessMarker.host"
    $workerParameters = @{}
    foreach ($entry in $PSBoundParameters.GetEnumerator()) {
        if ($entry.Key -notin @("InternalWorker", "WorkerProcessMarkerPath")) {
            $workerParameters[$entry.Key] = $entry.Value
        }
    }
    $workerParameters["WorkerProcessMarkerPath"] = $workerProcessMarker
    $workerJob = Start-Job -ScriptBlock {
        param([string]$ScriptPath, [hashtable]$Parameters, [string]$HostMarkerPath)
        [IO.File]::WriteAllText($HostMarkerPath, [string]$PID)
        & $ScriptPath @Parameters -InternalWorker
    } -ArgumentList $PSCommandPath, $workerParameters, $workerHostMarker
    $completedJob = Wait-Job -Job $workerJob -Timeout $TimeoutSeconds
    if ($null -eq $completedJob) {
        Stop-Job -Job $workerJob -ErrorAction SilentlyContinue
        $ownedAccessProcessIdentities = @()
        if (Test-Path -LiteralPath $workerProcessMarker) {
            try {
                $parsedIdentities = @(
                    [IO.File]::ReadAllText($workerProcessMarker) | ConvertFrom-Json
                )
                $ownedAccessProcessIdentities = @(
                    ConvertTo-AccessProcessIdentities $parsedIdentities
                )
            }
            catch {
                $ownedAccessProcessIdentities = @()
            }
        }
        if ($ownedAccessProcessIdentities.Count -eq 0 -and (Test-Path -LiteralPath $workerHostMarker)) {
            $workerHostId = 0
            if ([int]::TryParse(
                [IO.File]::ReadAllText($workerHostMarker),
                [ref]$workerHostId)) {
                $ownedAccessProcessIdentities = @(
                    Get-CimInstance -ClassName Win32_Process -Filter "Name = 'MSACCESS.EXE'" -ErrorAction SilentlyContinue |
                        Where-Object { [int]$_.ParentProcessId -eq $workerHostId } |
                        ForEach-Object { Get-Process -Id ([int]$_.ProcessId) -ErrorAction SilentlyContinue } |
                        ForEach-Object { New-AccessProcessIdentity $_ }
                )
            }
        }
        $remainingOwnedAccess = @(Get-OwnedAccessProcesses $ownedAccessProcessIdentities)
        if ($remainingOwnedAccess.Count -gt 0) {
            $remainingOwnedAccess | Stop-Process -Force -ErrorAction SilentlyContinue
        }
        $remainingOwnedAccess = @(Get-OwnedAccessProcesses $ownedAccessProcessIdentities)
        $unattributedAccess = @(Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue)
        $processCleanupFailed = $remainingOwnedAccess.Count -gt 0 -or $unattributedAccess.Count -gt 0
        $scratchPattern = ".$([IO.Path]::GetFileName($output)).metadata-*"
        try {
            if (Test-Path -LiteralPath $output) {
                Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction Stop
            }
            Get-ChildItem -LiteralPath $outputParent -Directory -Filter $scratchPattern -ErrorAction SilentlyContinue |
                Remove-Item -Recurse -Force -ErrorAction Stop
            if (Test-Path -LiteralPath $workerProcessMarker) {
                Remove-Item -LiteralPath $workerProcessMarker -Force -ErrorAction Stop
            }
            if (Test-Path -LiteralPath $workerHostMarker) {
                Remove-Item -LiteralPath $workerHostMarker -Force -ErrorAction Stop
            }
        }
        catch {
            Remove-Job -Job $workerJob -Force -ErrorAction SilentlyContinue
            Stop-Export "AccessMetadataTimeoutCleanupFailed"
        }
        Remove-Job -Job $workerJob -Force -ErrorAction SilentlyContinue
        if ($processCleanupFailed) {
            Stop-Export "AccessMetadataTimeoutCleanupFailed"
        }
        if ((Get-Sha256 $original) -ne $originalBeforeSupervision -or
            (Get-Sha256 $copy) -ne $copyBeforeSupervision) {
            Stop-Export "AccessMetadataSourceChanged"
        }
        Stop-Export "AccessMetadataTimeout"
    }

    $workerErrors = @()
    $workerOutput = @(
        Receive-Job -Job $workerJob -ErrorVariable +workerErrors -ErrorAction SilentlyContinue
    )
    $workerFailure = [string]$workerJob.ChildJobs[0].JobStateInfo.Reason.Message
    $ownedAccessProcessIdentities = @()
    if (Test-Path -LiteralPath $workerProcessMarker) {
        try {
            $parsedIdentities = @(
                [IO.File]::ReadAllText($workerProcessMarker) | ConvertFrom-Json
            )
            $ownedAccessProcessIdentities = @(
                ConvertTo-AccessProcessIdentities $parsedIdentities
            )
        }
        catch {
            $ownedAccessProcessIdentities = @()
        }
    }
    Remove-Job -Job $workerJob -Force -ErrorAction SilentlyContinue
    $remainingOwnedAccess = @()
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        $remainingOwnedAccess = @(Get-OwnedAccessProcesses $ownedAccessProcessIdentities)
        if ($remainingOwnedAccess.Count -eq 0) { break }
        Start-Sleep -Milliseconds 250
    }
    if ($remainingOwnedAccess.Count -gt 0) {
        try {
            $remainingOwnedAccess | Stop-Process -Force -ErrorAction Stop
        }
        catch { }
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            $remainingOwnedAccess = @(Get-OwnedAccessProcesses $ownedAccessProcessIdentities)
            if ($remainingOwnedAccess.Count -eq 0) { break }
            Start-Sleep -Milliseconds 250
        }
    }
    $unattributedAccess = @(Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue)
    $processCleanupFailed = $remainingOwnedAccess.Count -gt 0 -or $unattributedAccess.Count -gt 0
    $scratchPattern = ".$([IO.Path]::GetFileName($output)).metadata-*"
    $scratchCleanupFailed = $false
    try {
        Get-ChildItem -LiteralPath $outputParent -Directory -Filter $scratchPattern -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force -ErrorAction Stop
    }
    catch {
        $scratchCleanupFailed = $true
    }
    if (Test-Path -LiteralPath $workerProcessMarker) {
        Remove-Item -LiteralPath $workerProcessMarker -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $workerHostMarker) {
        Remove-Item -LiteralPath $workerHostMarker -Force -ErrorAction SilentlyContinue
    }
    if ($processCleanupFailed -or $scratchCleanupFailed) {
        if (Test-Path -LiteralPath $output) {
            Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue
        }
        Stop-Export "AccessMetadataProcessCleanupFailed"
    }
    if ((Get-Sha256 $original) -ne $originalBeforeSupervision -or
        (Get-Sha256 $copy) -ne $copyBeforeSupervision) {
        if (Test-Path -LiteralPath $output) {
            Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue
        }
        Stop-Export "AccessMetadataSourceChanged"
    }
    if (-not [string]::IsNullOrWhiteSpace($workerFailure)) {
        Stop-Export $workerFailure
    }
    if ($workerErrors.Count -gt 0) {
        Stop-Export ([string]$workerErrors[0].Exception.Message)
    }
    $workerOutput | Write-Output
    return
}

$outputParent = Split-Path -Parent $output
New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
$scratch = Join-Path $outputParent ".$([IO.Path]::GetFileName($output)).metadata-$([Guid]::NewGuid().ToString('N'))"
$originalBefore = Get-Sha256 $original
$copyBefore = Get-Sha256 $copy
if (-not [string]::Equals($copyBefore, $originalBefore, [StringComparison]::OrdinalIgnoreCase)) {
    Stop-Export "AccessMetadataCopyBindingMismatch"
}
$access = $null
$database = $null
$guardDatabase = $null
$ownedAccessProcessIdentities = @()
$records = [System.Collections.Generic.List[object]]::new()
$catalogPartial = $false
$succeeded = $false
$cleanupFailure = ""
try {
    New-Item -ItemType Directory -Path $scratch | Out-Null
    $workingCopy = Join-Path $scratch "working.accdb"
    Copy-Item -LiteralPath $copy -Destination $workingCopy
    $access = New-Object -ComObject Access.Application
    $ownedAccessProcess = Get-AccessApplicationProcess $access
    if ($null -eq $ownedAccessProcess) {
        Stop-Export "AccessMetadataProcessOwnershipAmbiguous"
    }
    $ownedAccessProcessIdentities = @(
        New-AccessProcessIdentity $ownedAccessProcess
    )
    if (-not [string]::IsNullOrWhiteSpace($WorkerProcessMarkerPath)) {
        [IO.File]::WriteAllText(
            [IO.Path]::GetFullPath($WorkerProcessMarkerPath),
            ($ownedAccessProcessIdentities | ConvertTo-Json -Compress))
    }
    $access.AutomationSecurity = 3
    $access.Visible = $false
    $dbEngine = $null
    try {
        $dbEngine = $access.DBEngine
        $guardDatabase = $dbEngine.OpenDatabase($workingCopy)
        try { $guardDatabase.Properties.Delete("StartupForm") } catch { }
        $guardDatabase.Close()
    }
    finally {
        Close-ComObject $guardDatabase
        Close-ComObject $dbEngine
        $guardDatabase = $null
        $dbEngine = $null
    }
    $access.OpenCurrentDatabase($workingCopy, $true)
    if ([bool]$access.Visible -or (Test-Path -LiteralPath $canary)) {
        Stop-Export "AccessMetadataStartupCanaryFired"
    }
    $loadedBaseline = Get-LoadedState $access
    if ($loadedBaseline -ne "0:0") {
        Stop-Export "AccessMetadataLoadedStateChanged"
    }
    $database = $access.CurrentDb()

    $catalogOrdinal = 0
    $tableDefs = $null
    try {
        $tableDefs = $database.TableDefs
        if ([int]$tableDefs.Count -gt $MaxObjects) { Stop-Export "AccessMetadataObjectLimitReached" }
        for ($index = 0; $index -lt [int]$tableDefs.Count; $index++) {
            $table = $null
            $fields = $null
            try {
                $table = $tableDefs.Item($index)
                $name = [string]$table.Name
                if ($name.StartsWith("MSys", [StringComparison]::OrdinalIgnoreCase)) { continue }
                if (-not [string]::IsNullOrWhiteSpace([string]$table.Connect)) {
                    $catalogPartial = $true
                    continue
                }
                $tableId = "catalog-$($catalogOrdinal.ToString('D6'))"
                $catalogOrdinal++
                Add-Record $records "catalog-object" $tableId "" "catalog-export" "container-only" "" 0 0 "complete" ([ordered]@{
                    objectRole = "table"
                    identity = $name
                    ordinal = $index
                })
                $fields = $table.Fields
                if ([int]$fields.Count -gt $MaxObjects) { Stop-Export "AccessMetadataChildLimitReached" }
                for ($fieldIndex = 0; $fieldIndex -lt [int]$fields.Count; $fieldIndex++) {
                    $field = $null
                    try {
                        $field = $fields.Item($fieldIndex)
                        Add-Record $records "catalog-object" "$tableId-field-$($fieldIndex.ToString('D6'))" $tableId "catalog-export" "container-only" "" 0 0 "complete" ([ordered]@{
                            objectRole = "table-field"
                            identity = [string]$field.Name
                            parentRole = "table"
                            ordinal = $fieldIndex
                        })
                    }
                    finally { Close-ComObject $field }
                }
            }
            finally {
                Close-ComObject $fields
                Close-ComObject $table
            }
        }
    }
    finally { Close-ComObject $tableDefs }

    $queryDefs = $null
    try {
        $queryDefs = $database.QueryDefs
        if ([int]$queryDefs.Count -gt $MaxObjects) { Stop-Export "AccessMetadataObjectLimitReached" }
        for ($index = 0; $index -lt [int]$queryDefs.Count; $index++) {
            $query = $null
            try {
                $query = $queryDefs.Item($index)
                $name = [string]$query.Name
                if ($name.StartsWith("~", [StringComparison]::Ordinal)) { continue }
                $queryId = "catalog-$($catalogOrdinal.ToString('D6'))"
                $catalogOrdinal++
                Add-Record $records "catalog-object" $queryId "" "catalog-export" "container-only" "" 0 0 "complete" ([ordered]@{
                    objectRole = "saved-query"
                    identity = $name
                    ordinal = $index
                })
                if ([int]$query.Type -eq 0) {
                    $outputNames = @(Get-StaticQueryOutputNames ([string]$query.SQL))
                    if ($outputNames.Count -eq 0) {
                        $catalogPartial = $true
                        Add-Record $records "source-gap" "gap-query-$($index.ToString('D6'))" "" "producer-gap" "unavailable" "" 0 0 "partial" ([ordered]@{
                            classification = "source-unavailable"
                            affectedScope = "catalog"
                            coverageCategory = "source-unavailable"
                        })
                    }
                    for ($fieldIndex = 0; $fieldIndex -lt $outputNames.Count; $fieldIndex++) {
                        Add-Record $records "catalog-object" "$queryId-field-$($fieldIndex.ToString('D6'))" $queryId "catalog-export" "container-only" "" 0 0 "complete" ([ordered]@{
                            objectRole = "query-field"
                            identity = $outputNames[$fieldIndex]
                            parentRole = "saved-query"
                            ordinal = $fieldIndex
                        })
                    }
                }
            }
            finally {
                Close-ComObject $query
            }
        }
    }
    finally { Close-ComObject $queryDefs }

    $surfaceProject = $null
    $formCollection = $null
    $reportCollection = $null
    try {
        $surfaceProject = $access.CurrentProject
        $formCollection = $surfaceProject.AllForms
        $reportCollection = $surfaceProject.AllReports
        foreach ($surfaceSpec in @(
            [pscustomobject]@{ Kind = "form"; ObjectType = 2; Collection = $formCollection; Role = "form-design-export" },
            [pscustomobject]@{ Kind = "report"; ObjectType = 3; Collection = $reportCollection; Role = "report-design-export" }
        )) {
            $collection = $surfaceSpec.Collection
            if ([int]$collection.Count -gt $MaxObjects) { Stop-Export "AccessMetadataObjectLimitReached" }
            for ($index = 0; $index -lt [int]$collection.Count; $index++) {
                $item = $null
                try {
                    $item = $collection.Item($index)
                    $name = [string]$item.Name
                    $surfaceId = "surface-$($surfaceSpec.Kind)-$($index.ToString('D6'))"
                    Add-Record $records "catalog-object" $surfaceId "" "catalog-export" "container-only" "" 0 0 "complete" ([ordered]@{
                        objectRole = $surfaceSpec.Kind
                        identity = $name
                        ordinal = $index
                    })
                    $textPath = Join-Path $scratch "$surfaceId.txt"
                    $access.SaveAsText($surfaceSpec.ObjectType, $name, $textPath)
                    if ([bool]$access.Visible -or (Test-Path -LiteralPath $canary)) {
                        Stop-Export "AccessMetadataDesignExportCanaryFired"
                    }
                    if ((Get-LoadedState $access) -ne $loadedBaseline) {
                        Stop-Export "AccessMetadataLoadedStateChanged"
                    }
                    $textBytes = [IO.File]::ReadAllBytes($textPath)
                    if ($textBytes.LongLength -gt $MaxTextBytes) { Stop-Export "AccessMetadataTextLimitReached" }
                    $text = [IO.File]::ReadAllText($textPath)
                    $lineCount = if ($text.Length -eq 0) { 0 } else { [regex]::Matches($text, "`n").Count + 1 }
                    if ($lineCount -gt $MaxTextLines) { Stop-Export "AccessMetadataTextLimitReached" }
                    $documentHash = Get-BytesSha256 ($Utf8NoBom.GetBytes($text))
                    Add-Record $records "ui-design-document" "$surfaceId-document" $surfaceId $surfaceSpec.Role "exact-lines" $documentHash 1 $lineCount "complete" ([ordered]@{
                        documentRole = $surfaceSpec.Kind
                        designText = $text
                        documentSha256 = $documentHash
                        lineCount = $lineCount
                    })
                    Remove-Item -LiteralPath $textPath -Force
                }
                finally { Close-ComObject $item }
            }
        }
    }
    finally {
        Close-ComObject $reportCollection
        Close-ComObject $formCollection
        Close-ComObject $surfaceProject
    }

    $access.CloseCurrentDatabase()
    if (Test-Path -LiteralPath $canary) { Stop-Export "AccessMetadataCloseCanaryFired" }
    if ((Get-Sha256 $original) -ne $originalBefore -or (Get-Sha256 $copy) -ne $copyBefore) {
        Stop-Export "AccessMetadataSourceChanged"
    }

    $orderedRecords = @($records | Sort-Object { $_["kind"] }, { $_["recordId"] })
    $recordLines = @($orderedRecords | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 20 })
    $recordsText = if ($recordLines.Count -eq 0) { "" } else { ($recordLines -join "`n") + "`n" }
    $recordsBytes = $Utf8NoBom.GetBytes($recordsText)
    $recordsHash = Get-BytesSha256 $recordsBytes
    $counts = [ordered]@{}
    foreach ($group in $orderedRecords | Group-Object { $_["kind"] } | Sort-Object Name) {
        $counts[$group.Name] = $group.Count
    }
    New-Item -ItemType Directory -Path $output | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $output "access-design-records.ndjson"), $recordsBytes)
    $manifest = [ordered]@{
        schema = "tracemap.access-design-evidence.v1"
        producer = [ordered]@{
            id = "tracemap-access-windows-export"
            version = "1.0.0"
            mechanism = "access-save-as-text-metadata"
        }
        repository = [ordered]@{
            identityHash = $RepositoryIdentityHash
            commitSha = $CommitSha
        }
        baseScan = [ordered]@{
            manifestSha256 = $BaseScanManifestSha256
            databaseIdentityHash = $DatabaseIdentityHash
        }
        sourceCopy = [ordered]@{
            sha256 = $copyBefore
            binding = "hash-identical"
        }
        records = [ordered]@{
            sha256 = $recordsHash
            count = $orderedRecords.Count
            countsByKind = $counts
        }
        capabilities = [ordered]@{
            coordinates = "mixed"
            catalogCompleteness = if ($catalogPartial) { "declared-partial" } else { "complete" }
            identityDisclosure = "hash-only"
        }
    }
    [IO.File]::WriteAllText(
        (Join-Path $output "access-design-manifest.json"),
        ($manifest | ConvertTo-Json -Compress -Depth 20),
        $Utf8NoBom)
    $succeeded = $true
}
finally {
    Close-ComObject $guardDatabase
    Close-ComObject $database
    if ($null -ne $access) {
        try { $access.CloseCurrentDatabase() } catch { }
        try { $access.Quit(2) } catch { $cleanupFailure = "AccessMetadataQuitFailed" }
    }
    Close-ComObject $access
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    # PowerShell's out-of-process job can retain the Access COM apartment until
    # its worker host exits. Leave process and scratch verification to the
    # supervising invocation, which removes the worker first and then verifies
    # the exact recorded Access process identity before publishing the output.
    if ((-not $succeeded -or $cleanupFailure) -and (Test-Path -LiteralPath $output)) {
        try { Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction Stop }
        catch { $cleanupFailure = "AccessMetadataOutputCleanupFailed" }
    }
    if ($cleanupFailure) {
        throw $cleanupFailure
    }
}

Write-Output "access-metadata-export=completed;objects=$($records.Count);loadedStateUnchanged=true;sourceUnchanged=true;canariesClear=true;scratchClean=true"
