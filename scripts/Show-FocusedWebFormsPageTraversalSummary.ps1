[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^page-[0-9]{3,4}$')][string]$PriorPageId,
    [string]$StandaloneReviewRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_POWERSHELL_7_REQUIRED' }

function Read-BoundedJson([string]$Path, [long]$MaximumBytes, [string]$ErrorCode) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw $ErrorCode }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $MaximumBytes) { throw $ErrorCode }
    try { return [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -Depth 50 }
    catch { throw $ErrorCode }
}

function Read-ReceiptedApplication([string]$Root) {
    $receipt = Read-BoundedJson (Join-Path $Root 'run-receipt.json') 16MB 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_RECEIPT_UNAVAILABLE'
    if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or
        $receipt.run.state -ne 'completed' -or $receipt.stages.workbench.state -ne 'completed') {
        throw 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_RUN_INCOMPLETE'
    }
    $artifact = @($receipt.stages.workbench.artifacts | Where-Object {
        ([string]$_.path).Replace('\', '/').Equals('workbench/application-handoff.json', [StringComparison]::OrdinalIgnoreCase)
    })
    if ($artifact.Count -ne 1) { throw 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_ARTIFACT_NOT_RECEIPTED' }
    $path = Join-Path $Root 'workbench/application-handoff.json'
    $file = Get-Item -LiteralPath $path -ErrorAction Stop
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($file.Length -ne [long]$artifact[0].bytes -or $hash -ne [string]$artifact[0].sha256) {
        throw 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_ARTIFACT_MISMATCH'
    }
    $application = Read-BoundedJson $path 128MB 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_APPLICATION_UNAVAILABLE'
    if ($application.schemaVersion -ne 'webforms-application-handoff.v1') { throw 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_APPLICATION_INVALID' }
    return $application
}

function Sum-Property([object[]]$Items, [string]$Name) {
    $sum = @($Items | ForEach-Object {
        $property = $_.PSObject.Properties[$Name]
        if ($null -ne $property -and $null -ne $property.Value) { [long]$property.Value } else { 0L }
    } | Measure-Object -Sum).Sum
    if ($null -eq $sum) { return 0L }
    return [long]$sum
}

function Property-Value([object]$Value, [string]$Name) {
    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Write-Groups([string]$Prefix, [object[]]$Values) {
    foreach ($group in @($Values | ForEach-Object { [string]$_ } | Where-Object { $_ -match '^[A-Za-z0-9.-]{1,128}$' } | Group-Object | Sort-Object Name)) {
        Write-Output "$Prefix.$($group.Name)=$($group.Count)"
    }
}

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
if (!$StandaloneReviewRoot) {
    $selected = @(Get-ChildItem -LiteralPath $root -Directory -Filter 'webforms-standalone-review-*' |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'run-receipt.json') -PathType Leaf } |
        Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1)
    if ($selected.Count -ne 1) { throw 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_STANDALONE_REVIEW_UNAVAILABLE' }
    $StandaloneReviewRoot = $selected[0].FullName
}
$standalone = [IO.Path]::GetFullPath($StandaloneReviewRoot).TrimEnd('\', '/')
$priorApplication = Read-ReceiptedApplication $root
$currentApplication = Read-ReceiptedApplication $standalone
$priorPage = @($priorApplication.pages | Where-Object { $_.pageId -eq $PriorPageId })
if ($priorPage.Count -ne 1) { throw 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_PRIOR_PAGE_UNAVAILABLE' }
$priorRoute = [string]$priorPage[0].filePath
$currentPage = @($currentApplication.pages | Where-Object {
    ([string]$_.filePath).Replace('\', '/').Equals($priorRoute.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase)
})
if ($currentPage.Count -ne 1) { throw 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_ROUTE_MATCH_UNAVAILABLE' }

$snapshotPath = Join-Path $standalone 'workbench/webforms-modernization.snapshot.json'
$snapshotFile = Get-Item -LiteralPath $snapshotPath -ErrorAction Stop
$snapshotHash = (Get-FileHash -LiteralPath $snapshotPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($snapshotHash -ne [string]$currentApplication.provenance.inputSha256) {
    throw 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_PACKET_MISMATCH'
}
$packet = Read-BoundedJson $snapshotPath 512MB 'WEBFORMS_PAGE_TRAVERSAL_SUMMARY_PACKET_UNAVAILABLE'
$surfaceId = [string]$currentPage[0].surfaceId
$chains = @($packet.eventChains | Where-Object { [string]$_.surfaceId -eq $surfaceId })
$boundaries = @($packet.downstreamBoundaries | Where-Object { [string]$_.surfaceId -eq $surfaceId })
$observations = @($chains | ForEach-Object { Property-Value $_ 'traversalObservation' } | Where-Object { $null -ne $_ })
$inputLimits = @($packet.gaps | Where-Object {
    [string](Property-Value $_ 'classification') -eq 'WebFormsModernizationInputLimitReached'
} | ForEach-Object { Property-Value $_ 'scopeId' })

Write-Output 'pageTraversalSummary=valid'
Write-Output "priorPageId=$PriorPageId"
Write-Output "currentPageId=$([string]$currentPage[0].pageId)"
Write-Output "packetSha256=$snapshotHash"
Write-Output "packetBytes=$($snapshotFile.Length)"
Write-Output "packetTruncated=$([bool]$packet.summary.truncated)"
Write-Output "chains=$($chains.Count)"
Write-Output "boundaries=$($boundaries.Count)"
Write-Output "observations=$($observations.Count)"
Write-Output "reachedNodes=$(Sum-Property $observations 'reachedNodeCount')"
Write-Output "traversedEdges=$(Sum-Property $observations 'traversedEdgeCount')"
Write-Output "downstreamEdges=$(Sum-Property $observations 'downstreamEdgeCount')"
Write-Output "terminalPaths=$(Sum-Property $observations 'terminalPathCount')"
Write-Output "truncatedObservations=$(@($observations | Where-Object { [bool](Property-Value $_ 'truncated') }).Count)"
Write-Groups 'inputLimit' $inputLimits
Write-Groups 'stopState' @($observations | ForEach-Object { Property-Value $_ 'stopState' })
Write-Groups 'callEvidenceState' @($observations | ForEach-Object { Property-Value $_ 'callEvidenceState' })
Write-Groups 'leafReconciliation' @($observations | ForEach-Object { @(Property-Value $_ 'leafReconciliationStates') })
Write-Groups 'leafCallEvidence' @($observations | ForEach-Object { @(Property-Value $_ 'leafCallEvidenceStates') })
Write-Groups 'truncationReason' @($observations | ForEach-Object { @(Property-Value $_ 'truncationReasons') })
