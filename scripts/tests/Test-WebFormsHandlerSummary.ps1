$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'whandler.ps1'
$global:handlerFail = $false
$global:handlerBundle = 'synthetic/recovered'
$folder = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-handler-completed-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory((Join-Path $folder 'tool'))
[IO.File]::WriteAllText((Join-Path $folder 'tool/tracemap.dll'), 'synthetic')
$global:handlerStatus = @{ schemaVersion = 'webforms-review-status.v1'; state = 'reports-completed-review-only'; readerMatchesOriginalGenerator = $true; retainedArtifactsVerified = $true; workbenchPath = (Join-Path $folder 'review/run/reports/synthetic/index.html') }
function global:dotnet {
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'build') { return }
    if ($args -contains 'status') { $global:handlerStatus | ConvertTo-Json -Compress; return }
    if ($args -contains 'requery-handler') {
        if ($args[([Array]::IndexOf($args, '--bundle') + 1)] -cne $global:handlerBundle -or
            ($args -join ' ') -notmatch 'requery-handler --run .+review/run --bundle .+ --handler BidGroupNamesDDL_Init --out .+handler-requery') { throw 'Wrong independent requery' }
        if ($args -contains '--surface-name' -and ($args -join ' ') -notmatch 'handler-requery-(compiled-)?fill --surface-name DbDataAdapter.Fill( --traversal-scope compiled-il)?$') { throw 'Wrong Fill query' }
        if ($global:handlerFail) { $global:LASTEXITCODE = 1; return }
        'handlerRequery=completed-separate-report;no-scan;no-combine'
        return
    }
    if (($args -join ' ') -notmatch 'query-recovery --bundle synthetic --handler BidGroupNamesDDL_Init') { throw 'Wrong query' }
    if ($global:handlerFail) { $global:LASTEXITCODE = 1; 'private-error'; return }
    '{"private":"do-not-print","result":{"children":[{"pointer":"/handler-summary/exactChains","value":13},{"pointer":"/handler-summary/evidenceVariants","value":41},{"pointer":"/handler-summary/rootIdentities","value":1},{"pointer":"/handler-summary/retainedVariantsInspected","value":256},{"pointer":"/handler-summary/compiledTruncated","value":true},{"pointer":"/handler-summary/retainedOnly","value":true}]}}'
}
try {
    $output = @(& $helper -Bundle synthetic)
    if ($output -notcontains 'handler.exactChains=13' -or $output -notcontains 'handler.evidenceVariants=41') { throw 'Missing counts' }
    if (($output -join "`n") -match 'private|do-not-print') { throw 'Privacy leak' }
    $requery = @(& $helper -Bundle synthetic/recovered -Requery)
    if ($requery -notcontains 'handlerRequery=completed-separate-report;no-scan;no-combine') { throw 'Missing requery' }
    $fill = @(& $helper -Bundle synthetic/recovered -Requery -FillOnly)
    if ($fill -notcontains 'handlerRequery=completed-separate-report;no-scan;no-combine') { throw 'Missing Fill requery' }
    $compiled = @(& $helper -Bundle synthetic/recovered -Requery -FillOnly -CompiledOnly)
    if ($compiled -notcontains 'handlerRequery=completed-separate-report;no-scan;no-combine') { throw 'Missing compiled requery' }
    $global:handlerBundle = Split-Path $global:handlerStatus.workbenchPath -Parent
    $completed = @(& $helper -VerificationRoot $folder -Requery -FillOnly -CompiledOnly)
    if ($completed -notcontains 'handlerRequery=completed-separate-report;no-scan;no-combine') { throw 'Missing completed requery' }
    $global:handlerStatus.retainedArtifactsVerified = $false
    try { & $helper -VerificationRoot $folder -Requery; throw 'Unverified completed run accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_HANDLER_COMPLETED_STATE_NOT_ADMITTED') { throw } }
    $global:handlerBundle = 'synthetic/recovered'
    try { & $helper -Bundle synthetic -CompiledOnly; throw 'Expected compiled guard' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_HANDLER_COMPILED_REQUIRES_REQUERY') { throw } }
    try { & $helper -Bundle synthetic -FillOnly; throw 'Expected Fill guard' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_HANDLER_FILL_REQUIRES_REQUERY') { throw } }
    $global:handlerFail = $true
    try { & $helper -Bundle synthetic | Out-Null; throw 'Expected failure' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_HANDLER_QUERY_FAILED;originals-preserved') { throw } }
    try { & $helper -Bundle synthetic/recovered -Requery | Out-Null; throw 'Expected failure' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_HANDLER_REQUERY_FAILED;originals-preserved;partial-output-preserved') { throw } }
    Write-Output 'webFormsHandlerSummaryPublicTests=passed'
} finally {
    Remove-Item Function:\dotnet
    Remove-Variable handlerFail -Scope Global
    Remove-Variable handlerBundle -Scope Global
    Remove-Variable handlerStatus -Scope Global
    [IO.Directory]::Delete($folder, $true)
}
