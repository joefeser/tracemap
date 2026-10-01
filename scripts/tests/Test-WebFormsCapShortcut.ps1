$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wcap.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-cap-' + [guid]::NewGuid().ToString('N'))
function Save($value, $path) { [IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject $value -Depth 20)) }
try {
    foreach ($part in @('tool', 'reports/compiled', 'handler-requery')) { [void][IO.Directory]::CreateDirectory((Join-Path $root $part)) }
    [IO.File]::WriteAllText((Join-Path $root 'tool/tracemap.dll'), '')
    $global:capStatus = @{ schemaVersion = 'webforms-review-status.v1'; readerMatchesOriginalGenerator = $true;
        state = 'reports-completed-review-only'; retainedArtifactsVerified = $true; workbenchPath = (Join-Path $root 'reports/workbench.html') }
    function global:dotnet {
        if ($args[1] -ne 'webforms-review' -or $args[2] -ne 'status') { throw 'Unexpected command' }
        $global:LASTEXITCODE = 0
        $global:capStatus | ConvertTo-Json -Compress
    }
    $node = @{ symbolId = 'Synthetic.Selected(Object,EventArgs)'; displayName = 'Synthetic.Selected(Object,EventArgs)'; nodeId = 'root' }
    $fill = @{ nodeId = 'fill'; displayName = 'Fill'; surfaceName = 'DbDataAdapter.Fill' }
    $scalar = @{ nodeId = 'scalar'; displayName = 'Scalar'; surfaceName = 'SqlCommand.ExecuteScalar' }
    foreach ($item in @($node, $fill, $scalar)) {
        $item.sourceIndexId = 'source'; $item.scanId = 'scan'; $item.commitSha = 'abc'; $item.nodeKind = 'Method'
        if (!$item.ContainsKey('symbolId')) { $item.symbolId = $item.displayName }
    }
    $old = Join-Path $root 'reports/compiled/compiled-paths.handoff.local.json'
    $new = Join-Path $root 'handler-requery/compiled-paths.handoff.local.json'
    Save @{ query = @{}; paths = @(@{nodes=@($node,$fill)}) } $old
    Save @{ query = @{}; paths = @(@{nodes=@($node,$fill)},@{nodes=@($node,$scalar)}) } $new
    $receipt = @{schemaVersion='webforms-handler-requery.v1'; root=@{symbolId=$node.symbolId}; query=@{toSurface='database-api';surfaceName=$null}}
    $receiptPath = Join-Path $root 'handler-requery/handler-requery.local.json'
    Save $receipt $receiptPath
    $hash = (Get-FileHash $old).Hash
    $result = @(& $helper -VerificationRoot $root -NoOpen)
    if ($result -notcontains 'compare.currentChains=2;currentVariants=2') { throw 'Scalar excluded' }
    if (!(Test-Path (Join-Path $root 'handler-requery/chain-comparison.local.html'))) { throw 'Report missing' }
    if ((Get-FileHash $old).Hash -ne $hash) { throw 'Original changed' }
    $receipt.query.surfaceName = 'DbDataAdapter.Fill'; Save $receipt $receiptPath
    try { & $helper -VerificationRoot $root -NoOpen; throw 'Fill-only accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_CAP_ALL_TERMINALS_REQUIRED') { throw } }
    $global:capStatus.retainedArtifactsVerified = $false
    try { & $helper -VerificationRoot $root -NoOpen; throw 'Unverified status accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_CAP_RETAINED_STATE_NOT_ADMITTED') { throw } }
    Write-Output 'webFormsCapShortcutTests=passed'
} finally {
    Remove-Item Function:global:dotnet -ErrorAction SilentlyContinue
    Remove-Variable capStatus -Scope Global -ErrorAction SilentlyContinue
    [IO.Directory]::Delete($root, $true)
}
