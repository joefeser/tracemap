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
    [string]$DatabaseIdentityHash
)

$ErrorActionPreference = "Stop"
$MaxObjects = 10000
$MaxTextBytes = 4MB
$MaxTextLines = 100000
$Utf8NoBom = [Text.UTF8Encoding]::new($false)

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

function Get-LoadedState([object]$Application) {
    $forms = 0
    $reports = 0
    $allForms = $null
    $allReports = $null
    try {
        $allForms = $Application.CurrentProject.AllForms
        if ([int]$allForms.Count -gt $MaxObjects) { Stop-Export "AccessMetadataObjectLimitReached" }
        for ($index = 0; $index -lt [int]$allForms.Count; $index++) {
            $item = $null
            try {
                $item = $allForms.Item($index)
                if ([bool]$item.IsLoaded) { $forms++ }
            }
            finally { Close-ComObject $item }
        }
        $allReports = $Application.CurrentProject.AllReports
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
$outputParent = Split-Path -Parent $output
New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
$scratch = Join-Path $outputParent ".$([IO.Path]::GetFileName($output)).metadata-$([Guid]::NewGuid().ToString('N'))"
$originalBefore = Get-Sha256 $original
$copyBefore = Get-Sha256 $copy
$access = $null
$database = $null
$records = [System.Collections.Generic.List[object]]::new()
$catalogPartial = $false
$succeeded = $false
$cleanupFailed = $false
try {
    New-Item -ItemType Directory -Path $scratch | Out-Null
    $access = New-Object -ComObject Access.Application
    $access.AutomationSecurity = 3
    $access.Visible = $false
    $access.OpenCurrentDatabase($copy, $true)
    if ([bool]$access.Visible -or (Test-Path -LiteralPath $canary)) {
        Stop-Export "AccessMetadataCanaryFired"
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

    foreach ($surfaceSpec in @(
        [pscustomobject]@{ Kind = "form"; ObjectType = 2; Collection = $access.CurrentProject.AllForms; Role = "form-design-export" },
        [pscustomobject]@{ Kind = "report"; ObjectType = 3; Collection = $access.CurrentProject.AllReports; Role = "report-design-export" }
    )) {
        $collection = $surfaceSpec.Collection
        try {
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
                        Stop-Export "AccessMetadataCanaryFired"
                    }
                    if ((Get-LoadedState $access) -ne $loadedBaseline) {
                        Stop-Export "AccessMetadataLoadedStateChanged"
                    }
                    $textBytes = [IO.File]::ReadAllBytes($textPath)
                    if ($textBytes.LongLength -gt $MaxTextBytes) { Stop-Export "AccessMetadataTextLimitReached" }
                    $text = [IO.File]::ReadAllText($textPath)
                    $lineCount = if ($text.Length -eq 0) { 0 } else { [regex]::Matches($text, "`n").Count + 1 }
                    if ($lineCount -gt $MaxTextLines) { Stop-Export "AccessMetadataTextLimitReached" }
                    $documentHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Utf8NoBom.GetBytes($text))).ToLowerInvariant()
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
        finally { Close-ComObject $collection }
    }

    $access.CloseCurrentDatabase()
    if (Test-Path -LiteralPath $canary) { Stop-Export "AccessMetadataCanaryFired" }
    if ((Get-Sha256 $original) -ne $originalBefore -or (Get-Sha256 $copy) -ne $copyBefore) {
        Stop-Export "AccessMetadataSourceChanged"
    }

    $orderedRecords = @($records | Sort-Object kind, recordId)
    $recordLines = @($orderedRecords | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 20 })
    $recordsText = if ($recordLines.Count -eq 0) { "" } else { ($recordLines -join "`n") + "`n" }
    $recordsBytes = $Utf8NoBom.GetBytes($recordsText)
    $recordsHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($recordsBytes)).ToLowerInvariant()
    $counts = [ordered]@{}
    foreach ($group in $orderedRecords | Group-Object kind | Sort-Object Name) {
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
    Close-ComObject $database
    if ($null -ne $access) {
        try { $access.CloseCurrentDatabase() } catch { }
        try { $access.Quit(2) } catch { $cleanupFailed = $true }
    }
    Close-ComObject $access
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    $remainingAccess = @(Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue)
    if ($remainingAccess.Count -gt 0) {
        $remainingAccess | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
        $remainingAccess = @(Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue)
    }
    if ($remainingAccess.Count -gt 0) {
        $cleanupFailed = $true
        $remainingAccess | Stop-Process -Force -ErrorAction SilentlyContinue
    }
    try {
        if (Test-Path -LiteralPath $scratch) {
            Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction Stop
        }
    }
    catch {
        $cleanupFailed = $true
    }
    if (Test-Path -LiteralPath $scratch) { $cleanupFailed = $true }
    if ((-not $succeeded -or $cleanupFailed) -and (Test-Path -LiteralPath $output)) {
        try { Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction Stop }
        catch { $cleanupFailed = $true }
    }
    if ($cleanupFailed) {
        throw "AccessMetadataCleanupFailed"
    }
}

Write-Output "access-metadata-export=completed;objects=$($records.Count);loadedStateUnchanged=true;sourceUnchanged=true;canariesClear=true;scratchClean=true"
