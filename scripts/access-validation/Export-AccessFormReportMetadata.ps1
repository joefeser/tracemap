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
    [ValidatePattern("^(?:[0-9a-f]{40}|[0-9a-f]{64})$")]
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

    [string]$WorkerProcessMarkerPath = "",

    [string]$WorkerScratchDirectoryPath = ""
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

function Remove-PathChecked(
    [string]$Path,
    [bool]$Recurse,
    [string]$FailureClassification
) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $failed = $false
    try {
        Remove-Item -LiteralPath $Path -Force -Recurse:$Recurse -ErrorAction Stop
        if (Test-Path -LiteralPath $Path) { $failed = $true }
    }
    catch {
        $failed = $true
    }
    if ($failed) { Stop-Export $FailureClassification }
}

function Clear-ReadOnlyAttributes([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    foreach ($item in @(Get-ChildItem -LiteralPath $Path -Recurse -Force -ErrorAction Stop)) {
        if ($item.Attributes -band [IO.FileAttributes]::ReadOnly) {
            $item.Attributes = $item.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly)
        }
    }
    $rootItem = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($rootItem.Attributes -band [IO.FileAttributes]::ReadOnly) {
        $rootItem.Attributes = $rootItem.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly)
    }
}

function Remove-DirectoryWithRetry([string]$Path) {
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        if (-not (Test-Path -LiteralPath $Path)) { return $true }
        try {
            Clear-ReadOnlyAttributes $Path
            if (Test-Path -LiteralPath $Path) {
                Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            }
        }
        catch { }
        if (-not (Test-Path -LiteralPath $Path)) { return $true }
        if ($attempt -lt 30) { Start-Sleep -Milliseconds 200 }
    }
    return $false
}

function Test-SourceHashesUnchanged(
    [string]$OriginalPath,
    [string]$OriginalHash,
    [string]$CopyPath,
    [string]$CopyHash
) {
    try {
        return (Get-Sha256 $OriginalPath) -eq $OriginalHash -and
            (Get-Sha256 $CopyPath) -eq $CopyHash
    }
    catch {
        return $false
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

function Split-StaticQueryProjectionList([string]$ProjectionList, [ref]$Complete) {
    $Complete.Value = $false
    $items = [System.Collections.Generic.List[string]]::new()
    $builder = [Text.StringBuilder]::new()
    $singleQuote = [char]39
    $doubleQuote = [char]34
    $inSingleQuote = $false
    $inDoubleQuote = $false
    $inBracket = $false
    $parenthesisDepth = 0
    $invalid = $false

    for ($index = 0; $index -lt $ProjectionList.Length; $index++) {
        $character = $ProjectionList[$index]
        if ($inSingleQuote) {
            [void]$builder.Append($character)
            if ($character -eq $singleQuote) {
                if ($index + 1 -lt $ProjectionList.Length -and $ProjectionList[$index + 1] -eq $singleQuote) {
                    [void]$builder.Append($ProjectionList[++$index])
                }
                else { $inSingleQuote = $false }
            }
            continue
        }
        if ($inDoubleQuote) {
            [void]$builder.Append($character)
            if ($character -eq $doubleQuote) {
                if ($index + 1 -lt $ProjectionList.Length -and $ProjectionList[$index + 1] -eq $doubleQuote) {
                    [void]$builder.Append($ProjectionList[++$index])
                }
                else { $inDoubleQuote = $false }
            }
            continue
        }
        if ($inBracket) {
            [void]$builder.Append($character)
            if ($character -eq ']') { $inBracket = $false }
            continue
        }

        if ($character -eq $singleQuote) { $inSingleQuote = $true }
        elseif ($character -eq $doubleQuote) { $inDoubleQuote = $true }
        elseif ($character -eq '[') { $inBracket = $true }
        elseif ($character -eq '(') { $parenthesisDepth++ }
        elseif ($character -eq ')') {
            if ($parenthesisDepth -eq 0) { $invalid = $true }
            else { $parenthesisDepth-- }
        }
        elseif ($character -eq ',' -and $parenthesisDepth -eq 0) {
            $item = $builder.ToString().Trim()
            if ([string]::IsNullOrWhiteSpace($item)) { $invalid = $true }
            else { $items.Add($item) }
            [void]$builder.Clear()
            continue
        }
        [void]$builder.Append($character)
    }

    $finalItem = $builder.ToString().Trim()
    if ([string]::IsNullOrWhiteSpace($finalItem)) { $invalid = $true }
    else { $items.Add($finalItem) }
    $Complete.Value = -not $invalid -and -not $inSingleQuote -and -not $inDoubleQuote -and
        -not $inBracket -and $parenthesisDepth -eq 0 -and $items.Count -gt 0
    return @($items)
}

function Get-UnquotedSqlExpression([string]$Expression, [ref]$Complete) {
    $Complete.Value = $false
    $builder = [Text.StringBuilder]::new()
    $singleQuote = [char]39
    $doubleQuote = [char]34
    $inSingleQuote = $false
    $inDoubleQuote = $false

    for ($index = 0; $index -lt $Expression.Length; $index++) {
        $character = $Expression[$index]
        if ($inSingleQuote) {
            [void]$builder.Append(' ')
            if ($character -eq $singleQuote) {
                if ($index + 1 -lt $Expression.Length -and $Expression[$index + 1] -eq $singleQuote) {
                    [void]$builder.Append(' ')
                    $index++
                }
                else { $inSingleQuote = $false }
            }
            continue
        }
        if ($inDoubleQuote) {
            [void]$builder.Append(' ')
            if ($character -eq $doubleQuote) {
                if ($index + 1 -lt $Expression.Length -and $Expression[$index + 1] -eq $doubleQuote) {
                    [void]$builder.Append(' ')
                    $index++
                }
                else { $inDoubleQuote = $false }
            }
            continue
        }

        if ($character -eq $singleQuote) {
            $inSingleQuote = $true
            [void]$builder.Append(' ')
        }
        elseif ($character -eq $doubleQuote) {
            $inDoubleQuote = $true
            [void]$builder.Append(' ')
        }
        else { [void]$builder.Append($character) }
    }

    $Complete.Value = -not $inSingleQuote -and -not $inDoubleQuote
    return $builder.ToString()
}

function Get-StaticQuerySelectList([string]$Sql, [ref]$Complete) {
    $Complete.Value = $false
    $unquotedComplete = $false
    $unquotedSql = Get-UnquotedSqlExpression $Sql ([ref]$unquotedComplete)
    if (-not $unquotedComplete) { return "" }

    $prefix = [regex]::Match(
        $unquotedSql,
        "(?is)^\s*(?:PARAMETERS\b.*?;\s*)?SELECT\b")
    if (-not $prefix.Success) { return "" }

    $listStart = $prefix.Index + $prefix.Length
    while ($listStart -lt $Sql.Length -and [char]::IsWhiteSpace($Sql[$listStart])) { $listStart++ }
    while ($listStart -lt $Sql.Length) {
        $modifier = [regex]::Match(
            $Sql.Substring($listStart),
            "(?is)^(?:(?:DISTINCT|DISTINCTROW)\b|TOP\s+\d+(?:\s+PERCENT)?\b)\s+")
        if (-not $modifier.Success) { break }
        $listStart += $modifier.Length
    }
    if ($listStart -ge $Sql.Length) { return "" }

    $inBracket = $false
    $parenthesisDepth = 0
    for ($index = $listStart; $index -lt $unquotedSql.Length; $index++) {
        $character = $unquotedSql[$index]
        if ($inBracket) {
            if ($character -eq ']') {
                if ($index + 1 -lt $unquotedSql.Length -and $unquotedSql[$index + 1] -eq ']') { $index++ }
                else { $inBracket = $false }
            }
            continue
        }
        if ($character -eq '[') {
            $inBracket = $true
            continue
        }
        if ($character -eq '(') {
            $parenthesisDepth++
            continue
        }
        if ($character -eq ')') {
            if ($parenthesisDepth -eq 0) { return "" }
            $parenthesisDepth--
            continue
        }
        if ($parenthesisDepth -ne 0 -or $index + 4 -gt $unquotedSql.Length) { continue }
        if (-not [string]::Equals($unquotedSql.Substring($index, 4), "FROM", [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $beforeIsWord = $index -gt $listStart -and
            ([char]::IsLetterOrDigit($unquotedSql[$index - 1]) -or $unquotedSql[$index - 1] -eq '_')
        $afterIndex = $index + 4
        $afterIsWord = $afterIndex -lt $unquotedSql.Length -and
            ([char]::IsLetterOrDigit($unquotedSql[$afterIndex]) -or $unquotedSql[$afterIndex] -eq '_')
        if ($beforeIsWord -or $afterIsWord) { continue }

        $selectList = $Sql.Substring($listStart, $index - $listStart).Trim()
        if ([string]::IsNullOrWhiteSpace($selectList)) { return "" }
        $Complete.Value = $true
        return $selectList
    }
    return ""
}

function Test-UnquotedSqlContainsStructuralParenthesis([string]$Expression) {
    $inBracket = $false
    for ($index = 0; $index -lt $Expression.Length; $index++) {
        $character = $Expression[$index]
        if ($inBracket) {
            if ($character -eq ']') {
                if ($index + 1 -lt $Expression.Length -and $Expression[$index + 1] -eq ']') { $index++ }
                else { $inBracket = $false }
            }
            continue
        }
        if ($character -eq '[') { $inBracket = $true }
        elseif ($character -eq '(') { return $true }
    }
    return $false
}

function Get-StaticQueryOutputNames([string]$Sql, [ref]$Complete) {
    $Complete.Value = $false
    if ($Sql.Length -gt $MaxTextBytes) { return @() }
    $boundaryComplete = $false
    $selectList = Get-StaticQuerySelectList $Sql ([ref]$boundaryComplete)
    if (-not $boundaryComplete) { return @() }
    $unquotedListComplete = $false
    $unquotedSelectList = Get-UnquotedSqlExpression $selectList ([ref]$unquotedListComplete)
    if (-not $unquotedListComplete -or $unquotedSelectList.Contains("*")) { return @() }
    $result = [System.Collections.Generic.List[string]]::new()
    $splitComplete = $false
    $projectionItems = @(Split-StaticQueryProjectionList $selectList ([ref]$splitComplete))
    $parsedCount = 0
    foreach ($rawItem in $projectionItems) {
        $item = $rawItem.Trim()
        $expressionComplete = $false
        $unquotedItem = Get-UnquotedSqlExpression $item ([ref]$expressionComplete)
        if (-not $expressionComplete) { continue }
        if ((Test-UnquotedSqlContainsStructuralParenthesis $unquotedItem) -and
            $unquotedItem -notmatch "(?is)\s+AS\s+(?:\[(?<alias>[^\]]+)\]|(?<alias>[A-Za-z_][A-Za-z0-9_ ]*))\s*$") {
            continue
        }
        $alias = [regex]::Match($unquotedItem, "(?is)\s+AS\s+(?:\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][A-Za-z0-9_ ]*))\s*$")
        if ($alias.Success) {
            $result.Add($alias.Groups["name"].Value.Trim())
            $parsedCount++
            continue
        }
        $direct = [regex]::Match(
            $unquotedItem,
            "(?is)^(?:(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_.$]*)\s*\.\s*)?(?:\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][A-Za-z0-9_ ]*))$")
        if ($direct.Success) {
            $result.Add($direct.Groups["name"].Value.Trim())
            $parsedCount++
        }
    }
    $unique = @($result | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $Complete.Value = $boundaryComplete -and $splitComplete -and
        $parsedCount -eq $projectionItems.Count -and $unique.Count -eq $parsedCount
    return $unique
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
    $outputClaimPath = Join-Path $outputParent ".$([IO.Path]::GetFileName($output)).claim"
    $outputClaimStream = $null
    $outputClaimCreated = $false
    try {
        try {
            $outputClaimStream = [IO.File]::Open(
                $outputClaimPath,
                [IO.FileMode]::CreateNew,
                [IO.FileAccess]::Write,
                [IO.FileShare]::None)
            $outputClaimCreated = $true
        }
        catch {
            Stop-Export "AccessMetadataOutputClaimUnavailable"
        }
        if (Test-Path -LiteralPath $output) {
            Stop-Export "AccessMetadataOutputExists"
        }
    $workerProcessMarker = Join-Path $outputParent ".$([IO.Path]::GetFileName($output)).worker-$([Guid]::NewGuid().ToString('N')).process.json"
    $workerHostMarker = "$workerProcessMarker.host"
    $workerScratchDirectory = Join-Path $outputParent ".$([IO.Path]::GetFileName($output)).metadata-$([Guid]::NewGuid().ToString('N'))"
    $workerParameters = @{}
    foreach ($entry in $PSBoundParameters.GetEnumerator()) {
        if ($entry.Key -notin @("InternalWorker", "WorkerProcessMarkerPath", "WorkerScratchDirectoryPath")) {
            $workerParameters[$entry.Key] = $entry.Value
        }
    }
    $workerParameters["WorkerProcessMarkerPath"] = $workerProcessMarker
    $workerParameters["WorkerScratchDirectoryPath"] = $workerScratchDirectory
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
        try {
            if (Test-Path -LiteralPath $output) {
                Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction Stop
            }
            if (-not (Remove-DirectoryWithRetry $workerScratchDirectory)) {
                throw "AccessMetadataTimeoutCleanupFailed"
            }
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
        if (-not (Test-SourceHashesUnchanged $original $originalBeforeSupervision $copy $copyBeforeSupervision)) {
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
    try { $workerJob.Dispose() } catch { }
    $completedJob = $null
    $workerJob = $null
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    $remainingOwnedAccess = @()
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        $remainingOwnedAccess = @(Get-OwnedAccessProcesses $ownedAccessProcessIdentities)
        $observedAccess = @(Get-Process -Name "MSACCESS" -ErrorAction SilentlyContinue)
        if ($observedAccess.Count -eq 0) { break }
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
    $scratchCleanupFailed = $false
    try {
        if (-not (Remove-DirectoryWithRetry $workerScratchDirectory)) {
            $scratchCleanupFailed = $true
        }
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
        Remove-PathChecked $output $true "AccessMetadataOutputCleanupFailed"
        Stop-Export "AccessMetadataProcessCleanupFailed"
    }
    if (-not (Test-SourceHashesUnchanged $original $originalBeforeSupervision $copy $copyBeforeSupervision)) {
        Remove-PathChecked $output $true "AccessMetadataOutputCleanupFailed"
        Stop-Export "AccessMetadataSourceChanged"
    }
    if (-not [string]::IsNullOrWhiteSpace($workerFailure)) {
        Remove-PathChecked $output $true "AccessMetadataOutputCleanupFailed"
        Stop-Export $workerFailure
    }
    if ($workerErrors.Count -gt 0) {
        Remove-PathChecked $output $true "AccessMetadataOutputCleanupFailed"
        Stop-Export ([string]$workerErrors[0].Exception.Message)
    }
    $workerOutput | Write-Output
    return
    }
    finally {
        if ($null -ne $outputClaimStream) {
            try { $outputClaimStream.Dispose() } catch { }
        }
        if ($outputClaimCreated) {
            Remove-PathChecked $outputClaimPath $false "AccessMetadataOutputClaimCleanupFailed"
        }
    }
}

$outputParent = Split-Path -Parent $output
New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
if ([string]::IsNullOrWhiteSpace($WorkerScratchDirectoryPath)) {
    Stop-Export "AccessMetadataScratchBindingMissing"
}
$scratch = [IO.Path]::GetFullPath($WorkerScratchDirectoryPath)
$expectedScratchPrefix = ".$([IO.Path]::GetFileName($output)).metadata-"
if (-not [string]::Equals(
        [IO.Path]::GetDirectoryName($scratch),
        $outputParent,
        [StringComparison]::OrdinalIgnoreCase) -or
    -not [IO.Path]::GetFileName($scratch).StartsWith(
        $expectedScratchPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    Stop-Export "AccessMetadataScratchBindingMismatch"
}
if ([string]::IsNullOrWhiteSpace($WorkerProcessMarkerPath)) {
    Stop-Export "AccessMetadataProcessMarkerBindingMissing"
}
$workerProcessMarkerFullPath = [IO.Path]::GetFullPath($WorkerProcessMarkerPath)
$expectedProcessMarkerPrefix = ".$([IO.Path]::GetFileName($output)).worker-"
if (-not [string]::Equals(
        [IO.Path]::GetDirectoryName($workerProcessMarkerFullPath),
        $outputParent,
        [StringComparison]::OrdinalIgnoreCase) -or
    -not [IO.Path]::GetFileName($workerProcessMarkerFullPath).StartsWith(
        $expectedProcessMarkerPrefix,
        [StringComparison]::OrdinalIgnoreCase) -or
    -not [IO.Path]::GetFileName($workerProcessMarkerFullPath).EndsWith(
        ".process.json",
        [StringComparison]::OrdinalIgnoreCase)) {
    Stop-Export "AccessMetadataProcessMarkerBindingMismatch"
}
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
$createdOutput = $false
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
    [IO.File]::WriteAllText(
        $workerProcessMarkerFullPath,
        ($ownedAccessProcessIdentities | ConvertTo-Json -Compress))
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
                    $outputNamesComplete = $false
                    $outputNames = @(Get-StaticQueryOutputNames ([string]$query.SQL) ([ref]$outputNamesComplete))
                    $outputCompleteness = if ($outputNamesComplete) { "complete" } else { "partial" }
                    if (-not $outputNamesComplete) {
                        $catalogPartial = $true
                        Add-Record $records "source-gap" "gap-query-$($index.ToString('D6'))" "" "producer-gap" "unavailable" "" 0 0 "partial" ([ordered]@{
                            classification = "source-unavailable"
                            affectedScope = "catalog"
                            coverageCategory = "source-unavailable"
                        })
                    }
                    for ($fieldIndex = 0; $fieldIndex -lt $outputNames.Count; $fieldIndex++) {
                        Add-Record $records "catalog-object" "$queryId-field-$($fieldIndex.ToString('D6'))" $queryId "catalog-export" "container-only" "" 0 0 $outputCompleteness ([ordered]@{
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
    $createdOutput = $true
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
    if ($createdOutput -and (-not $succeeded -or $cleanupFailure) -and (Test-Path -LiteralPath $output)) {
        try { Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction Stop }
        catch { $cleanupFailure = "AccessMetadataOutputCleanupFailed" }
    }
    if ($cleanupFailure) {
        throw $cleanupFailure
    }
}

Write-Output "access-metadata-export=completed;objects=$($records.Count);loadedStateUnchanged=true;sourceUnchanged=true;canariesClear=true;scratchClean=true"
