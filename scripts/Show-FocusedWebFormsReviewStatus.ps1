[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$ReviewRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'webforms-review/FocusedWebFormsPipelineConfig.ps1')

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'WEBFORMS_STATUS_REVIEW_ROOT_UNAVAILABLE' }
$configPath = Resolve-FocusedWebFormsPipelineConfigPath -ReviewRoot $root
$receiptPath = Join-Path $root 'run-receipt.json'
$receiptState = 'unavailable'
$runId = 'unavailable'
if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
    try {
        $receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 20
        $receiptState = [string]$receipt.run.state
        $runIdProperty = $receipt.run.PSObject.Properties['runId']
        if ($null -ne $runIdProperty -and ![string]::IsNullOrWhiteSpace([string]$runIdProperty.Value)) {
            $runId = [string]$runIdProperty.Value
        }
    }
    catch { throw 'WEBFORMS_STATUS_RECEIPT_INVALID' }
}

$pageLists = @(Get-ChildItem -LiteralPath $root -File -Recurse -Filter 'webforms-modernization.json' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '[\\/]webforms-page-list-[^\\/]+[\\/]webforms-modernization\.json$' })
$standaloneReceipts = @(Get-ChildItem -LiteralPath $root -File -Recurse -Filter 'run-receipt.json' -ErrorAction SilentlyContinue |
    Where-Object { $_.Directory.Name -like 'webforms-standalone-review-*' })

Write-Output 'webformsReviewStatus=valid'
Write-Output "runState=$receiptState"
Write-Output "runId=$runId"
Write-Output "configPath=$configPath"
Write-Output "scanPath=$(Join-Path $root 'scan')"
Write-Output "packetPath=$(Join-Path $root 'packet/webforms-modernization.json')"
Write-Output "evidenceDocsPath=$(Join-Path $root 'evidence-docs')"
Write-Output "workbenchIndex=$(Join-Path $root 'workbench/index.html')"
Write-Output "refreshPageLists=$($pageLists.Count)"
Write-Output "refreshStandaloneReviews=$($standaloneReceipts.Count)"
