[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^page-[0-9]{3,4}$')][string]$PageId,
    [string]$CallSitePattern = '*'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_CALL_SITE_DEBUG_POWERSHELL_7_REQUIRED' }

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
$handoffPath = Join-Path (Join-Path $root 'workbench') "$PageId.handoff.json"
if (!(Test-Path -LiteralPath $handoffPath -PathType Leaf)) {
    throw "WEBFORMS_CALL_SITE_DEBUG_HANDOFF_UNAVAILABLE;path=$handoffPath"
}
$handoffFile = Get-Item -LiteralPath $handoffPath
if ($handoffFile.Length -le 0 -or $handoffFile.Length -gt 128MB) {
    throw 'WEBFORMS_CALL_SITE_DEBUG_HANDOFF_LIMIT'
}
try {
    $page = [IO.File]::ReadAllText($handoffFile.FullName, [Text.UTF8Encoding]::new($false, $true)) |
        ConvertFrom-Json -Depth 50
}
catch {
    throw 'WEBFORMS_CALL_SITE_DEBUG_HANDOFF_INVALID'
}
if ($page.schemaVersion -ne 'webforms-application-page-handoff.v1' -or
    $page.claimLevel -ne 'local-only' -or
    $page.pageId -ne $PageId) {
    throw 'WEBFORMS_CALL_SITE_DEBUG_HANDOFF_INVALID'
}

$chains = @($page.eventChains)
$calls = @($chains | ForEach-Object { @($_.callEvidence) })
$retainedCount = if ($null -ne $page.counts.retainedCalls) { [int]$page.counts.retainedCalls } else { $calls.Count }
$normalizedCount = if ($null -ne $page.counts.normalizedCallSites) { [int]$page.counts.normalizedCallSites } else { 0 }

Write-Output 'webformsPageCallSites=valid'
Write-Output "pageId=$PageId"
Write-Output "handoffPath=$($handoffFile.FullName)"
Write-Output "chainCount=$($chains.Count)"
Write-Output "retainedCalls=$retainedCount"
Write-Output "materializedCalls=$($calls.Count)"
Write-Output "normalizedCallSites=$normalizedCount"

foreach ($chain in $chains) {
    $chainCalls = @($chain.callEvidence)
    $handler = if ([string]::IsNullOrWhiteSpace([string]$chain.handlerSymbol)) { [string]$chain.handlerId } else { [string]$chain.handlerSymbol }
    Write-Output "chain=$($chain.chainId);calls=$($chainCalls.Count);stop=$($chain.traversalStopState);handler=$handler"
}

if ($calls.Count -eq 0) {
    Write-Output 'diagnosis=no-call-evidence-in-this-handoff;check-review-root-and-report-local-page-alias'
    return
}

$matchedCalls = @($calls | Where-Object {
    ([string]$_.callSiteId) -like $CallSitePattern
} | Sort-Object `
    @{ Expression = { [string]$_.evidence.filePath } }, `
    @{ Expression = { [int]$_.evidence.startLine } }, `
    @{ Expression = { [string]$_.calleeName } }, `
    @{ Expression = { [string]$_.callSiteId } })

Write-Output "callSitePattern=$CallSitePattern"
Write-Output "matchedCallFacts=$($matchedCalls.Count)"
foreach ($call in $matchedCalls) {
    Write-Output "callSite=$($call.callSiteId);callee=$($call.calleeName);kind=$($call.callKind);resolution=$($call.resolution);file=$($call.evidence.filePath);line=$($call.evidence.startLine)-$($call.evidence.endLine)"
}

if ($matchedCalls.Count -eq 0) {
    Write-Output 'diagnosis=call-site-pattern-not-found;verify-handoff-and-full-or-wildcarded-id'
}
