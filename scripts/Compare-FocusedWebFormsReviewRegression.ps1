[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [string]$OldReviewRoot = '',
    [ValidatePattern('^page-[0-9]{3,4}$')][string]$PriorPageId = 'page-011',
    [switch]$IncludePrivateReceiverIdentities,
    [switch]$SummaryOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_REVIEW_REGRESSION_POWERSHELL_7_REQUIRED' }

function Resolve-ReviewRoot([string]$Path, [string]$ErrorCode) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw $ErrorCode }
    $resolved = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    if (!(Test-Path -LiteralPath $resolved -PathType Container)) { throw $ErrorCode }
    return $resolved
}

function Read-KeyValueOutput([string]$Root) {
    $summaryScript = Join-Path $PSScriptRoot 'Show-FocusedWebFormsPageTraversalSummary.ps1'
    $arguments = @{
        ReviewRoot = $Root
        PriorPageId = $PriorPageId
        StandaloneReviewRoot = $Root
    }
    if ($IncludePrivateReceiverIdentities) { $arguments['IncludePrivateReceiverIdentities'] = $true }
    $lines = @(& $summaryScript @arguments)
    $values = [ordered]@{}
    foreach ($lineValue in $lines) {
        $line = [string]$lineValue
        if ($line -notmatch '^([A-Za-z][A-Za-z0-9_.-]{0,255})=(.*)$') { continue }
        $key = $Matches[1]
        if (!$values.Contains($key)) { $values[$key] = $Matches[2] }
    }
    if ($values['pageTraversalSummary'] -ne 'valid') { throw 'WEBFORMS_REVIEW_REGRESSION_SUMMARY_INVALID' }
    return $values
}

function Add-DirectoryMetrics([System.Collections.IDictionary]$Values, [string]$Root, [string]$RelativePath, [string]$Name) {
    $path = Join-Path $Root $RelativePath
    $files = @(if (Test-Path -LiteralPath $path -PathType Container) {
        Get-ChildItem -LiteralPath $path -File -Recurse -Force
    })
    $bytes = 0L
    foreach ($file in $files) { $bytes += [long]$file.Length }
    $Values["artifact.$Name.files"] = [string]$files.Count
    $Values["artifact.$Name.bytes"] = [string]$bytes
}

function Add-FileMetric([System.Collections.IDictionary]$Values, [string]$Root, [string]$RelativePath, [string]$Name) {
    $path = Join-Path $Root $RelativePath
    $Values["artifact.$Name.bytes"] = if (Test-Path -LiteralPath $path -PathType Leaf) {
        [string](Get-Item -LiteralPath $path).Length
    } else { '0' }
}

function Read-ReviewMetrics([string]$Root) {
    $values = Read-KeyValueOutput $Root
    Add-DirectoryMetrics $values $Root 'combined' 'combined'
    Add-DirectoryMetrics $values $Root 'evidence-docs' 'evidenceDocs'
    Add-DirectoryMetrics $values $Root 'packet' 'packet'
    Add-DirectoryMetrics $values $Root 'workbench' 'workbench'
    Add-FileMetric $values $Root 'combined/index.sqlite' 'combinedIndex'
    Add-FileMetric $values $Root 'packet/webforms-modernization.json' 'packetJson'
    Add-FileMetric $values $Root 'workbench/webforms-modernization.snapshot.json' 'workbenchSnapshot'
    Add-FileMetric $values $Root 'workbench/application-handoff.json' 'applicationHandoff'
    Add-FileMetric $values $Root "workbench/$PriorPageId.html" 'pageHtml'
    Add-FileMetric $values $Root "workbench/$PriorPageId.handoff.json" 'pageHandoff'
    return $values
}

function Write-PrefixedMetrics([string]$Prefix, [System.Collections.IDictionary]$Values) {
    foreach ($key in @($Values.Keys | Sort-Object)) {
        if ($SummaryOnly -and !(Test-HighSignalMetric $key)) { continue }
        Write-Output "$Prefix.$key=$($Values[$key])"
    }
}

function Test-HighSignalMetric([string]$Key) {
    return $Key -match '^(artifact\.|chains$|boundaries$|observations$|reachedNodes$|traversedEdges$|downstreamEdges$|terminalPaths$|truncatedObservations$|packetTruncated$|diagnosticShapesTruncated$|terminalKind\.|stopState\.|receiverBridgeGap\.|receiverBridgeStatus\.|receiverBridgePrivate\.(execProcReachableNodes$|execProc(Start|Leaf)-|graphGap-)|inputLimit\.|traversedEdge\.|traversedRule\.)'
}

$currentRoot = Resolve-ReviewRoot $ReviewRoot 'WEBFORMS_REVIEW_REGRESSION_CURRENT_ROOT_UNAVAILABLE'
$current = Read-ReviewMetrics $currentRoot
Write-Output 'reviewRegressionDiagnostic=valid'
Write-Output "pageId=$PriorPageId"
Write-PrefixedMetrics 'current' $current

if (![string]::IsNullOrWhiteSpace($OldReviewRoot)) {
    $oldRoot = Resolve-ReviewRoot $OldReviewRoot 'WEBFORMS_REVIEW_REGRESSION_OLD_ROOT_UNAVAILABLE'
    $old = Read-ReviewMetrics $oldRoot
    Write-PrefixedMetrics 'old' $old
    $keys = @($current.Keys + $old.Keys | Sort-Object -Unique)
    foreach ($key in $keys) {
        if ($SummaryOnly -and !(Test-HighSignalMetric $key)) { continue }
        $currentValue = if ($current.Contains($key)) { [string]$current[$key] } else { '<missing>' }
        $oldValue = if ($old.Contains($key)) { [string]$old[$key] } else { '<missing>' }
        if ($currentValue -eq $oldValue) { continue }
        $currentNumber = 0L
        $oldNumber = 0L
        if ([long]::TryParse($currentValue, [ref]$currentNumber) -and [long]::TryParse($oldValue, [ref]$oldNumber)) {
            $delta = $currentNumber - $oldNumber
            Write-Output "delta.$key=$delta"
        } else {
            Write-Output "change.$key=$oldValue -> $currentValue"
        }
    }
}
