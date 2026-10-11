Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../WebFormsGapClassification.ps1')
$entry = Join-Path $PSScriptRoot '../wgapclassify.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('gap-classification-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
function Gap($node, $kind, $cause = $null) {
    [pscustomobject]@{ nodeId=$node; gapKind=$kind; cutoffCause=$cause; ruleId='synthetic.rule'; evidenceTier='Tier4Unknown'; message='private-message-marker' }
}
function Save-Fixture($handoff) {
    $file = Join-Path $temp 'compiled-paths.handoff.local.json'
    $handoff | ConvertTo-Json -Depth 10 | Set-Content $file
    @{ schemaVersion='webforms-handler-requery.v1'; artifacts=@(@{
        relativePath='compiled-paths.handoff.local.json';bytes=(Get-Item $file).Length;
        sha256=(Get-FileHash $file).Hash.ToLowerInvariant() }) } | ConvertTo-Json -Depth 5 |
        Set-Content (Join-Path $temp 'handler-requery.local.json')
}
function Expect-Failure([scriptblock]$Action, [string]$Code) {
    try { & $Action | Out-Null } catch { if ($_.Exception.Message -ne $Code) { throw }; return }
    throw "Expected $Code"
}
try {
    $handoff = [pscustomobject]@{ schemaVersion='webforms-compiled-grouped-handoff.v1';header=@{gaps=@(
        (Gap 'private-node-a' 'TruncatedByLimit' 'terminal-route-not-found-within-depth-bound'),
        (Gap 'private-node-a' 'TruncatedByLimit' 'terminal-route-not-found-within-depth-bound'),
        (Gap 'private-node-b' 'TruncatedByLimit' 'terminal-route-not-found-within-depth-bound'),
        (Gap 'private-node-c' 'TruncatedByLimit' 'terminal-route-not-found-within-depth-bound'),
        (Gap $null 'TruncatedByLimit' 'terminal-route-not-found-within-depth-bound'),
        (Gap 'private-node-a' 'CompiledIlTargetUnavailable'),
        (Gap 'private-node-a' 'CompiledIlTargetUnavailable'),
        (Gap 'private-node-a' 'CompiledIlVirtualDispatchUnproven'),
        (Gap 'private-node-b' 'NewUnknownKind'),
        (Gap 'PRIVATE-NODE-C' 'CompiledIlTargetUnavailable'),
        (Gap 'unrelated' 'CompiledIlTargetAmbiguous'),
        (Gap 'identity' 'TruncatedByLimit' 'candidate-identity-roundtrip'),
        (Gap 'identity' 'TruncatedByLimit' 'candidate-identity-roundtrip')) } }
    $result = Get-WebFormsGapClassification $handoff
    if ($result.DepthGapRecords -ne 5 -or $result.UniqueDepthNodes -ne 3 -or $result.DepthRecordsWithoutNode -ne 1 -or
        $result.IdentityGapRecords -ne 2 -or $result.UniqueIdentityNodes -ne 1 -or $result.UnclassifiedDepthNodes -ne 1) { throw 'Incorrect deduplication or identity handling.' }
    if ($result.Categories.Count -ne 3 -or @($result.Categories | Where-Object UniqueNodes -ne 1).Count -ne 0) { throw 'Association must be exact-node, nonexclusive and deduplicated.' }
    Save-Fixture $handoff
    $before = @(Get-ChildItem $temp -File | ForEach-Object { (Get-FileHash $_.FullName).Hash }) -join ','
    $output = @(& $entry $temp) -join "`n"
    $replay = @(& $entry $temp) -join "`n"
    if ($output -cne $replay -or $output -match 'private-|PRIVATE-|unrelated' -or !$output.Contains('unclassifiedDepthNodes=1')) { throw 'Output is nondeterministic, leaked identifiers, or omitted unknowns.' }
    $after = @(Get-ChildItem $temp -File | ForEach-Object { (Get-FileHash $_.FullName).Hash }) -join ','
    if ($before -ne $after) { throw 'Original inputs changed.' }
    Add-Content (Join-Path $temp 'compiled-paths.handoff.local.json') ' '
    Expect-Failure { & $entry $temp } 'WEBFORMS_GAP_CLASSIFICATION_HANDOFF_CHANGED'
    $handoff.header.gaps = @((Gap 'n' 'TruncatedByLimit' 'terminal-route-not-found-within-depth-bound'),
        [pscustomobject]@{nodeId='n';gapKind='CompiledIlTargetUnavailable'})
    if ((Get-WebFormsGapClassification $handoff).UnclassifiedDepthNodes -ne 1) { throw 'Unruled evidence must not classify a node.' }
    $handoff.header.gaps = @()
    Save-Fixture $handoff
    if ((Get-WebFormsGapClassification $handoff).UniqueDepthNodes -ne 0) { throw 'Empty input must remain empty.' }
    $receiptPath = Join-Path $temp 'handler-requery.local.json'
    $receipt = Get-Content $receiptPath -Raw | ConvertFrom-Json
    $receipt.artifacts = @($receipt.artifacts[0], $receipt.artifacts[0])
    $receipt | ConvertTo-Json -Depth 5 | Set-Content $receiptPath
    Expect-Failure { & $entry $temp } 'WEBFORMS_GAP_CLASSIFICATION_HANDOFF_CHANGED'
    Save-Fixture $handoff
    $stream = [IO.File]::OpenWrite($receiptPath)
    try { $stream.SetLength(4194305) } finally { $stream.Dispose() }
    Expect-Failure { & $entry $temp } 'WEBFORMS_GAP_CLASSIFICATION_INPUT_LIMIT'
    $oversized = [pscustomobject]@{header=@{gaps=[object[]]::new(200001)}}
    Expect-Failure { Get-WebFormsGapClassification $oversized } 'WEBFORMS_GAP_CLASSIFICATION_RECORD_LIMIT'
    $handoff.schemaVersion = 'invalid'
    Save-Fixture $handoff
    Expect-Failure { & $entry $temp } 'WEBFORMS_GAP_CLASSIFICATION_SCHEMA_INVALID'
    Expect-Failure { & $entry (Join-Path $temp 'private-missing-path') } 'WEBFORMS_GAP_CLASSIFICATION_INPUT_INVALID'
    Write-Output 'Web Forms gap classification tests passed.'
} finally { Remove-Item $temp -Recurse -Force }
