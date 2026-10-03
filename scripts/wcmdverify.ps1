#Requires -Version 7.0
[CmdletBinding()]
param([string]$ReviewRoot, [string]$ProofRoot, [string]$PublishedRoot,
    [string]$SourceBase, [string]$OutputRoot, [string]$Handler, [switch]$Open)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Required([string]$value, [string]$prompt) {
    if ([string]::IsNullOrWhiteSpace($value)) { $value = Read-Host $prompt }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'WEBFORMS_COMMAND_SELECTION_REQUIRED' }
    return $value.Trim().Trim('"')
}
$ReviewRoot = Required $ReviewRoot 'Copied review folder (contains native-config)'
$ProofRoot = Required $ProofRoot 'Retained proof folder (contains publish-receipt and compiled-binding)'
$PublishedRoot = Required $PublishedRoot 'Original published website folder (parent of bin)'
$SourceBase = Required $SourceBase 'Website folder relative to source repository root'
$OutputRoot = [IO.Path]::GetFullPath((Required $OutputRoot 'NEW verification output folder (must not exist)'))
$Handler = Required $Handler 'Exact handler method name (not a path or full signature)'
if ($Handler -cnotmatch '^[A-Za-z0-9_]{1,128}$') { throw 'WEBFORMS_COMMAND_HANDLER_INVALID' }
if (Test-Path -LiteralPath $OutputRoot) { throw 'WEBFORMS_COMMAND_OUTPUT_EXISTS;preserved-unchanged' }
Write-Host 'Fresh retained-proof verification. No website rebuild, SQL execution, cleanup, old-run resume or parity claim.'
try {
    & (Join-Path $PSScriptRoot 'wverify.ps1') -ReviewRoot $ReviewRoot -ProofRoot $ProofRoot -PublishedRoot $PublishedRoot `
        -SourceBase $SourceBase -OutputRoot $OutputRoot -Run
} catch {
    if ($_.Exception.Message -cne 'WEBFORMS_VERIFY_RUN_FAILED;preserve-output-for-diagnostics') { throw }
    Write-Output 'commandValidation.start=failed;checking-retained-state;failure-not-upgraded'
}
$run = Join-Path $OutputRoot 'review/run'
$tool = Join-Path $OutputRoot 'tool'
$cli = Join-Path $tool 'tracemap.dll'
function ToolFingerprint {
    $items = @(Get-ChildItem -LiteralPath $tool -Force -Recurse | Select-Object -First 4097)
    if ($items.Count -gt 4096 -or @($items | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count) {
        throw 'WEBFORMS_COMMAND_TOOL_INVENTORY_INVALID'
    }
    $files = @($items | Where-Object { !$_.PSIsContainer } | Sort-Object FullName)
    if ($files.Count -lt 1 -or $files.Count -gt 512) { throw 'WEBFORMS_COMMAND_TOOL_INVENTORY_INVALID' }
    $roster = foreach ($file in $files) {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'WEBFORMS_COMMAND_TOOL_LINK_INVALID' }
        [IO.Path]::GetRelativePath($tool, $file.FullName) + ':' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($roster -join "`n"))))
}
$fingerprint = ToolFingerprint
$json = @(& dotnet $cli webforms-review status --run $run --json)
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_COMMAND_STATUS_FAILED;partial-output-preserved' }
$status = ($json -join "`n") | ConvertFrom-Json -AsHashtable
if ($status.schemaVersion -ne 'webforms-review-status.v1' -or !$status.readerMatchesOriginalGenerator) {
    throw 'WEBFORMS_COMMAND_PINNED_TOOL_MISMATCH;partial-output-preserved'
}
$scope = 'retained-handler-filter;original-query-scope-preserved'
if ($status.state -eq 'reports-completed-review-only' -and $status.retainedArtifactsVerified) {
    $bundle = [IO.Path]::GetDirectoryName([string]$status.workbenchPath)
    $report = Join-Path $bundle 'compiled/compiled-paths.handoff.local.json'
} elseif ($status.state -eq 'reports-failed' -and 'WEBFORMS_EVIDENCE_NODE_LIMIT' -in $status.checkpointGaps) {
    $bundle = Join-Path $OutputRoot 'recovered-command-reports'
    & dotnet $cli webforms-review recover-reports --run $run --out $bundle
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_COMMAND_RECOVERY_FAILED;original-failure-preserved' }
    $handlerReport = Join-Path $OutputRoot 'handler-command-compiled-fill'
    & dotnet $cli webforms-review requery-handler --run $run --bundle $bundle --handler $Handler --out $handlerReport `
        --surface-name DbDataAdapter.Fill --traversal-scope compiled-il
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_COMMAND_REQUERY_FAILED;original-failure-preserved' }
    $report = Join-Path $handlerReport 'compiled-paths.handoff.local.json'
    $scope = 'new-compiled-il-handler-fill-query;root-attachment-not-il-proof'
} else { throw 'WEBFORMS_COMMAND_RETAINED_STATE_NOT_ADMITTED;partial-output-preserved' }
$ledger = Join-Path $OutputRoot 'handler-command-evidence.local.html'
& (Join-Path $PSScriptRoot 'wsqlroute.ps1') -Report $report -Handler $Handler -OutputPath $ledger
if ($fingerprint -cne (ToolFingerprint)) { throw 'WEBFORMS_COMMAND_TOOL_CHANGED;outputs-preserved-not-admitted' }
Write-Output "commandValidation.scope=$scope"
Write-Output 'commandValidation=partial-static-candidates;fresh-scan;original-runs-preserved;no-sql-executed;not-parity-proof'
if ($Open) { Invoke-Item -LiteralPath $ledger }
