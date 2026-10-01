#Requires -Version 7.0
[CmdletBinding()]
param([string]$VerificationRoot, [switch]$NoOpen, [switch]$Unresolved)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!$VerificationRoot) {
    $homeRoot = if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }
    $VerificationRoot = Join-Path $homeRoot 'verify-8'
}
$cli = Join-Path $VerificationRoot 'tool/tracemap.dll'
if (!(Test-Path -LiteralPath $cli -PathType Leaf)) { throw 'WEBFORMS_CAP_PINNED_TOOL_MISSING' }
$statusJson = @(& dotnet $cli webforms-review status --run (Join-Path $VerificationRoot 'review/run') --json)
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_CAP_STATUS_FAILED' }
$status = ($statusJson -join "`n") | ConvertFrom-Json -AsHashtable
if ($status.schemaVersion -ne 'webforms-review-status.v1' -or !$status.readerMatchesOriginalGenerator -or
    $status.state -ne 'reports-completed-review-only' -or !$status.retainedArtifactsVerified) {
    throw 'WEBFORMS_CAP_RETAINED_STATE_NOT_ADMITTED'
}
$current = Join-Path $VerificationRoot 'handler-requery/compiled-paths.handoff.local.json'
$receiptPath = Join-Path $VerificationRoot 'handler-requery/handler-requery.local.json'
$stream = [IO.File]::OpenRead($receiptPath)
try {
    if ($stream.Length -gt 1MB) { throw 'WEBFORMS_CAP_RECEIPT_LIMIT' }
    $reader = [IO.StreamReader]::new($stream)
    try { $receipt = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable }
    finally { $reader.Dispose() }
} finally { $stream.Dispose() }
# Receipt supplies a selector only; wcompare remains unadmitted saved readback.
if ($receipt.schemaVersion -ne 'webforms-handler-requery.v1' -or
    $receipt.query.toSurface -ne 'database-api' -or $receipt.query.surfaceName) {
    throw 'WEBFORMS_CAP_ALL_TERMINALS_REQUIRED'
}
$symbol = [string]$receipt.root.symbolId
if ($symbol -cnotmatch '\.([A-Za-z_][A-Za-z0-9_]{0,127})\(') { throw 'WEBFORMS_CAP_HANDLER_INVALID' }
$handler = $Matches[1]
if ($Unresolved) {
    & (Join-Path $PSScriptRoot 'wsqlroute.ps1') -Report $current -Handler $handler -UnresolvedOnly -Open:(!$NoOpen)
    return
}
$old = Join-Path ([IO.Path]::GetDirectoryName([string]$status.workbenchPath)) 'compiled/compiled-paths.handoff.local.json'
Write-Output 'capCompare=original-vs-single-handler;all-database-api;no-scan;no-traversal'
& (Join-Path $PSScriptRoot 'wcompare.ps1') -Historical $old -Current $current -Mixed $current -Handler $handler -Open:(!$NoOpen)
