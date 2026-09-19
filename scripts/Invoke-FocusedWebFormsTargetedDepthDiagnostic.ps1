[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [ValidateCount(1, 2)][string[]]$PageId = @('page-002', 'page-003'),
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_TARGETED_DEPTH_POWERSHELL_7_REQUIRED' }

$commonPath = Join-Path $PSScriptRoot 'webforms-review/ClaudeReview.Common.ps1'
if (!(Test-Path -LiteralPath $commonPath -PathType Leaf)) { throw 'WEBFORMS_TARGETED_DEPTH_COMMON_UNAVAILABLE' }
. $commonPath

$requestedPageIds = @($PageId | ForEach-Object { $_.Trim() } | Select-Object -Unique)
if ($requestedPageIds.Count -lt 1 -or $requestedPageIds.Count -gt 2 -or
    @($requestedPageIds | Where-Object { $_ -cnotmatch '^page-[0-9]{3,}$' }).Count -ne 0) {
    throw 'WEBFORMS_TARGETED_DEPTH_PAGE_SELECTION_INVALID'
}

$context = Get-FocusedWebFormsClaudeEvidenceContext $ReviewRoot $TraceMapRoot
$root = $context.Root
$receiptPath = Join-Path $root 'run-receipt.json'
$receipt = Read-FocusedWebFormsBoundedJson $receiptPath 16MB 'WEBFORMS_TARGETED_DEPTH_RECEIPT_UNAVAILABLE'
if ($receipt.run.state -ne 'completed') { throw 'WEBFORMS_TARGETED_DEPTH_RUN_INCOMPLETE' }

$scanRelativeRoot = ([string]$receipt.layout.scan).Replace('\', '/').Trim('/')
if ([string]::IsNullOrWhiteSpace($scanRelativeRoot) -or [IO.Path]::IsPathRooted($scanRelativeRoot) -or
    $scanRelativeRoot.Split('/', [StringSplitOptions]::RemoveEmptyEntries) -contains '..') {
    throw 'WEBFORMS_TARGETED_DEPTH_SCAN_LAYOUT_INVALID'
}
$indexRelativePath = "$scanRelativeRoot/index.sqlite"
Assert-FocusedWebFormsReceiptedArtifact $receipt 'scan' $root $indexRelativePath
Assert-FocusedWebFormsReceiptedArtifact $receipt 'workbench' $root 'workbench/application-handoff.json'
$indexPath = Join-Path $root $indexRelativePath

$handoffPath = Join-Path $root 'workbench/application-handoff.json'
$handoff = Read-FocusedWebFormsBoundedJson $handoffPath 32MB 'WEBFORMS_TARGETED_DEPTH_HANDOFF_UNAVAILABLE'
if ($handoff.schemaVersion -ne 'webforms-application-handoff.v1') { throw 'WEBFORMS_TARGETED_DEPTH_HANDOFF_INVALID' }
$selectedPages = @($requestedPageIds | ForEach-Object {
    $requested = $_
    $matches = @($handoff.pages | Where-Object { [string]$_.pageId -ceq $requested })
    if ($matches.Count -ne 1 -or [string]::IsNullOrWhiteSpace([string]$matches[0].filePath)) {
        throw "WEBFORMS_TARGETED_DEPTH_PAGE_UNAVAILABLE;pageId=$requested"
    }
    $matches[0]
})

$diagnosticRoot = Join-Path $root 'diagnostics'
[IO.Directory]::CreateDirectory($diagnosticRoot) | Out-Null
$runName = 'targeted-depth-10-{0}-{1}' -f ($requestedPageIds -join '-'), ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
$outputDirectory = Join-Path $diagnosticRoot $runName
$baselineOutputDirectory = Join-Path $outputDirectory 'baseline-depth-8'
$temporaryList = Join-Path ([IO.Path]::GetTempPath()) "tracemap-targeted-depth-$([Guid]::NewGuid().ToString('N')).txt"
try {
    [IO.File]::WriteAllLines($temporaryList, @($selectedPages.filePath), [Text.UTF8Encoding]::new($false))
    & (Join-Path $PSScriptRoot 'Invoke-FocusedWebFormsPageListReport.ps1') `
        -IndexPath $indexPath `
        -PageListPath $temporaryList `
        -OutputDirectory $baselineOutputDirectory `
        -MaxDepth 8
    & (Join-Path $PSScriptRoot 'Invoke-FocusedWebFormsPageListReport.ps1') `
        -IndexPath $indexPath `
        -PageListPath $temporaryList `
        -OutputDirectory $outputDirectory `
        -MaxDepth 10 `
        -TargetedDepthDiagnostic
}
finally {
    Remove-Item -LiteralPath $temporaryList -Force -ErrorAction SilentlyContinue
}

$packetPath = Join-Path $outputDirectory 'webforms-modernization.json'
$packet = Read-FocusedWebFormsBoundedJson $packetPath 128MB 'WEBFORMS_TARGETED_DEPTH_OUTPUT_UNAVAILABLE'
$depthGapCount = @($packet.gaps | Where-Object {
    $_.classification -eq 'TruncatedByLimit' -and $_.truncationReason -eq 'depth'
}).Count
$otherTruncationCount = @($packet.gaps | Where-Object {
    $_.classification -eq 'TruncatedByLimit' -and $_.truncationReason -ne 'depth'
}).Count

Write-Output 'webformsTargetedDepthDiagnostic=completed'
Write-Output "reviewRoot=$root"
Write-Output "pageIds=$($requestedPageIds -join ',')"
Write-Output 'maxDepth=10'
Write-Output 'maxTraversalWork=100000'
Write-Output "eventChains=$(@($packet.eventChains).Count)"
Write-Output "boundaryRecords=$(@($packet.downstreamBoundaries).Count)"
Write-Output "depthGaps=$depthGapCount"
Write-Output "otherTruncationGaps=$otherTruncationCount"
Write-Output "packetTruncated=$([bool]$packet.summary.truncated)"
Write-Output "baselinePacket=$(Join-Path $baselineOutputDirectory 'webforms-modernization.json')"
Write-Output "diagnosticPacket=$packetPath"
Write-Output 'nonClaim=deeper-bounded-static-traversal-does-not-prove-runtime-execution-completeness-or-distinct-operations'
