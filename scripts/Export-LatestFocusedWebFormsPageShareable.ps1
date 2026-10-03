[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^page-[0-9]{3,4}$')][string]$PriorPageId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_POWERSHELL_7_REQUIRED' }

function Read-BoundedJson([string]$Path, [long]$MaximumBytes, [string]$ErrorCode) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw $ErrorCode }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $MaximumBytes) { throw $ErrorCode }
    try { return [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -Depth 40 }
    catch { throw $ErrorCode }
}

function Get-ReceiptedApplication([string]$Root) {
    $receiptPath = Join-Path $Root 'run-receipt.json'
    $receipt = Read-BoundedJson $receiptPath 16MB 'WEBFORMS_LATEST_PAGE_SHAREABLE_RECEIPT_UNAVAILABLE'
    if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or
        $receipt.run.state -ne 'completed' -or
        $receipt.stages.workbench.state -ne 'completed') {
        throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_RUN_INCOMPLETE'
    }
    $relativePath = 'workbench/application-handoff.json'
    $artifacts = @($receipt.stages.workbench.artifacts | Where-Object {
        ([string]$_.path).Replace('\', '/').Equals($relativePath, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($artifacts.Count -ne 1) { throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_ARTIFACT_NOT_RECEIPTED' }
    $applicationPath = Join-Path $Root $relativePath
    if (!(Test-Path -LiteralPath $applicationPath -PathType Leaf)) { throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_APPLICATION_UNAVAILABLE' }
    $file = Get-Item -LiteralPath $applicationPath
    $hash = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($file.Length -ne [long]$artifacts[0].bytes -or $hash -ne [string]$artifacts[0].sha256) {
        throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_ARTIFACT_MISMATCH'
    }
    $application = Read-BoundedJson $applicationPath 128MB 'WEBFORMS_LATEST_PAGE_SHAREABLE_APPLICATION_UNAVAILABLE'
    if ($application.schemaVersion -ne 'webforms-application-handoff.v1') { throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_APPLICATION_INVALID' }
    return $application
}

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_REVIEW_ROOT_UNAVAILABLE' }
$latest = @(Get-ChildItem -LiteralPath $root -Directory -Filter 'webforms-standalone-review-*' |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'run-receipt.json') -PathType Leaf } |
    Sort-Object LastWriteTimeUtc, FullName -Descending |
    Select-Object -First 1)
if ($latest.Count -ne 1) { throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_STANDALONE_REVIEW_UNAVAILABLE' }

$priorApplication = Get-ReceiptedApplication $root
$latestApplication = Get-ReceiptedApplication $latest[0].FullName
$priorPages = @($priorApplication.pages | Where-Object { $_.pageId -eq $PriorPageId })
if ($priorPages.Count -ne 1) { throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_PRIOR_PAGE_UNAVAILABLE' }
$priorPath = [string]$priorPages[0].filePath
if ([string]::IsNullOrWhiteSpace($priorPath)) { throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_PRIOR_ROUTE_UNAVAILABLE' }
$latestPages = @($latestApplication.pages | Where-Object {
    ([string]$_.filePath).Replace('\', '/').Equals($priorPath.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase)
})
if ($latestPages.Count -ne 1) { throw 'WEBFORMS_LATEST_PAGE_SHAREABLE_ROUTE_MATCH_UNAVAILABLE' }
$latestPageId = [string]$latestPages[0].pageId

Write-Output "priorPageId=$PriorPageId"
Write-Output "latestPageId=$latestPageId"
Write-Output "standaloneReviewRoot=$($latest[0].FullName)"
& (Join-Path $PSScriptRoot 'Export-FocusedWebFormsPageShareable.ps1') `
    -ReviewRoot $latest[0].FullName `
    -PageId $latestPageId
