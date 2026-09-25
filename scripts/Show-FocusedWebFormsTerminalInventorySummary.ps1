[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [ValidateCount(1, 25)][ValidatePattern('^page-[0-9]{3,4}$')]
    [string[]]$PageIds = @('page-002', 'page-003', 'page-011')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_TERMINAL_SUMMARY_POWERSHELL_7_REQUIRED' }
if (@($PageIds | Sort-Object -Unique).Count -ne $PageIds.Count) { throw 'WEBFORMS_TERMINAL_SUMMARY_DUPLICATE_PAGE' }

function Read-BoundedJson([string]$Path, [long]$MaximumBytes, [string]$ErrorCode) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw $ErrorCode }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $MaximumBytes) { throw $ErrorCode }
    try { return [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -Depth 100 }
    catch { throw $ErrorCode }
}

function Property-Value([object]$Value, [string]$Name) {
    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
$receipt = Read-BoundedJson (Join-Path $root 'run-receipt.json') 16MB 'WEBFORMS_TERMINAL_SUMMARY_RECEIPT_UNAVAILABLE'
if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or
    $receipt.run.state -ne 'completed' -or $receipt.stages.workbench.state -ne 'completed') {
    throw 'WEBFORMS_TERMINAL_SUMMARY_RUN_INCOMPLETE'
}

$traceMapCommit = [string](Property-Value (Property-Value $receipt 'traceMap') 'commitSha')
if ($traceMapCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'WEBFORMS_TERMINAL_SUMMARY_TRACEMAP_COMMIT_UNAVAILABLE'
}

$lines = [Collections.Generic.List[string]]::new()
foreach ($pageId in $PageIds) {
    $relativePath = "workbench/$pageId.handoff.json"
    $artifacts = @($receipt.stages.workbench.artifacts | Where-Object {
        ([string]$_.path).Replace('\', '/').Equals($relativePath, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($artifacts.Count -ne 1) { throw 'WEBFORMS_TERMINAL_SUMMARY_PAGE_NOT_RECEIPTED' }

    $path = Join-Path $root $relativePath
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'WEBFORMS_TERMINAL_SUMMARY_PAGE_UNAVAILABLE' }
    $file = Get-Item -LiteralPath $path
    if ($file.Length -le 0 -or $file.Length -gt 128MB) { throw 'WEBFORMS_TERMINAL_SUMMARY_PAGE_UNAVAILABLE' }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($file.Length -ne [long]$artifacts[0].bytes -or $hash -ne [string]$artifacts[0].sha256) {
        throw 'WEBFORMS_TERMINAL_SUMMARY_PAGE_ARTIFACT_MISMATCH'
    }
    $page = Read-BoundedJson $path 128MB 'WEBFORMS_TERMINAL_SUMMARY_PAGE_UNAVAILABLE'
    if ($page.schemaVersion -ne 'webforms-application-page-handoff.v1' -or
        $page.claimLevel -ne 'local-only' -or $page.pageId -ne $pageId) {
        throw 'WEBFORMS_TERMINAL_SUMMARY_PAGE_INVALID'
    }
    $analysis = Property-Value $page 'analysis'
    $analysisStatus = [string](Property-Value $analysis 'status')
    if ($analysisStatus -notin @('complete', 'partial')) { $analysisStatus = 'unavailable' }
    $packetTruncationValue = Property-Value $analysis 'packetTruncated'
    $packetTruncated = if ($null -eq $packetTruncationValue) { 'unavailable' } elseif ($packetTruncationValue -eq $true) { 'true' } else { 'false' }

    $chains = @($page.eventChains)
    if ($chains.Count -gt 10000) { throw 'WEBFORMS_TERMINAL_SUMMARY_CHAIN_LIMIT' }
    $available = 0
    $complete = 0
    $incomplete = 0
    $noSupportedTerminalComplete = 0
    $pathDetailTruncated = 0
    $reportedTerminalCountSum = 0L
    $terminalIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $limits = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($chain in $chains) {
        $isAvailable = (Property-Value $chain 'terminalReachabilityAvailable') -eq $true
        $isComplete = Property-Value $chain 'terminalReachabilityComplete'
        if ($isAvailable) {
            if ($null -eq $isComplete) { throw 'WEBFORMS_TERMINAL_SUMMARY_INVENTORY_STATE_INVALID' }
            $available++
            if ($isComplete -eq $true) { $complete++ } else { $incomplete++ }
            $count = Property-Value $chain 'distinctReachableTerminalCount'
            $parsedCount = 0L
            if ($null -eq $count -or
                ![long]::TryParse([string]$count, [ref]$parsedCount) -or
                $parsedCount -lt 0) { throw 'WEBFORMS_TERMINAL_SUMMARY_TERMINAL_COUNT_INVALID' }
            $reportedTerminalCountSum += $parsedCount
            if ($isComplete -eq $true -and $parsedCount -eq 0) { $noSupportedTerminalComplete++ }
            foreach ($id in @(Property-Value $chain 'reachableTerminalIds')) {
                if ($id) { [void]$terminalIds.Add([string]$id) }
            }
            if ($isComplete -eq $false) {
                $reasons = @(Property-Value $chain 'terminalReachabilityLimitReasons' | Where-Object { $_ })
                if ($reasons.Count -eq 0) { [void]$limits.Add('reason-unavailable') }
                foreach ($reason in $reasons) {
                    $safeReason = if ([string]$reason -in @('work', 'frontier', 'path')) { [string]$reason } else { 'reason-unavailable' }
                    [void]$limits.Add($safeReason)
                }
            }
        }
        if ((Property-Value $chain 'pathEnumerationTruncated') -eq $true) { $pathDetailTruncated++ }
    }
    $limitText = if ($limits.Count -eq 0) { 'none' } else { @($limits) -join ',' }
    $lines.Add("pageId=$pageId;analysisStatus=$analysisStatus;packetTruncated=$packetTruncated;chains=$($chains.Count);available=$available;complete=$complete;incomplete=$incomplete;unavailable=$($chains.Count - $available);distinctTerminalIdsObserved=$($terminalIds.Count);reportedTerminalCountSum=$reportedTerminalCountSum;noSupportedTerminalComplete=$noSupportedTerminalComplete;pathDetailTruncated=$pathDetailTruncated;limits=$limitText")
}

Write-Output 'webFormsTerminalInventorySummary=valid'
Write-Output "traceMapCommitSha=$traceMapCommit"
Write-Output 'scope=receipted-retained-graph-only;runtime-absence-not-proven'
foreach ($line in $lines) { Write-Output $line }
