[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_OUTLIER_SUMMARY_POWERSHELL_7_REQUIRED' }

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
$receiptPath = Join-Path $root 'run-receipt.json'
if (!(Test-Path -LiteralPath $receiptPath -PathType Leaf)) { throw 'WEBFORMS_OUTLIER_SUMMARY_RECEIPT_UNAVAILABLE' }

$receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 20
if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1') { throw 'WEBFORMS_OUTLIER_SUMMARY_RECEIPT_SCHEMA_UNSUPPORTED' }
if ($receipt.run.state -ne 'completed' -or $receipt.stages.workbench.state -ne 'completed') { throw 'WEBFORMS_OUTLIER_SUMMARY_RUN_INCOMPLETE' }

$relativePath = 'workbench/application-outliers.shareable.json'
$artifact = @($receipt.stages.workbench.artifacts | Where-Object {
    ([string]$_.path).Replace('\', '/').Equals($relativePath, [StringComparison]::OrdinalIgnoreCase)
}) | Select-Object -First 1
if ($null -eq $artifact) { throw 'WEBFORMS_OUTLIER_SUMMARY_ARTIFACT_NOT_RECEIPTED' }

$outlierPath = Join-Path $root $relativePath
if (!(Test-Path -LiteralPath $outlierPath -PathType Leaf)) { throw 'WEBFORMS_OUTLIER_SUMMARY_ARTIFACT_UNAVAILABLE' }
$file = Get-Item -LiteralPath $outlierPath
$sha256 = (Get-FileHash -LiteralPath $outlierPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($file.Length -ne [long]$artifact.bytes -or $sha256 -ne [string]$artifact.sha256) { throw 'WEBFORMS_OUTLIER_SUMMARY_ARTIFACT_MISMATCH' }

$outliers = [IO.File]::ReadAllText($outlierPath) | ConvertFrom-Json -Depth 20
if ($outliers.schemaVersion -ne 'webforms-application-outliers.v1' -or $outliers.privacy -ne 'anonymous-counts-only') {
    throw 'WEBFORMS_OUTLIER_SUMMARY_SCHEMA_UNSUPPORTED'
}
$pages = @($outliers.pages)
if ([int]$outliers.pageCount -ne $pages.Count) { throw 'WEBFORMS_OUTLIER_SUMMARY_PAGE_COUNT_MISMATCH' }
foreach ($hash in @($outliers.provenance.generatorSha256, $outliers.provenance.inputSha256)) {
    if ([string]$hash -notmatch '^[0-9a-f]{64}$') { throw 'WEBFORMS_OUTLIER_SUMMARY_PROVENANCE_UNAVAILABLE' }
}

function Sum-PageCount {
    param([string]$Group, [string]$Name)
    return [long](($pages | ForEach-Object { [long]$_.PSObject.Properties[$Group].Value.PSObject.Properties[$Name].Value } | Measure-Object -Sum).Sum)
}

Write-Output 'outlierSummary=valid'
Write-Output "outlierPath=$outlierPath"
Write-Output "pages=$($pages.Count)"
Write-Output "callProjections=$(Sum-PageCount 'counts' 'callProjections')"
Write-Output "uniqueCallFacts=$(Sum-PageCount 'counts' 'uniqueCallFacts')"
Write-Output "normalizedCallSites=$(Sum-PageCount 'counts' 'normalizedCallSites')"
Write-Output "gaps=$(Sum-PageCount 'counts' 'gaps')"
Write-Output "generatorSha256=$($outliers.provenance.generatorSha256)"
Write-Output "inputSha256=$($outliers.provenance.inputSha256)"

$gapGroups = @($pages | ForEach-Object { @($_.gapCategories) } | Group-Object classification | Sort-Object Name)
foreach ($group in $gapGroups) {
    $count = [long](($group.Group | ForEach-Object { [long]$_.count } | Measure-Object -Sum).Sum)
    Write-Output "gap.$($group.Name)=$count"
}
