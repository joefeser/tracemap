$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'whandler.ps1'
$global:handlerFail = $false
function global:dotnet {
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'build') { return }
    if ($args -contains 'requery-handler') {
        if (($args -join ' ') -notmatch 'requery-handler --run .+review/run --bundle synthetic/recovered --handler BidGroupNamesDDL_Init --out .+handler-requery') { throw 'Wrong independent requery' }
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
    $global:handlerFail = $true
    try { & $helper -Bundle synthetic | Out-Null; throw 'Expected failure' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_HANDLER_QUERY_FAILED;originals-preserved') { throw } }
    try { & $helper -Bundle synthetic/recovered -Requery | Out-Null; throw 'Expected failure' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_HANDLER_REQUERY_FAILED;originals-preserved;partial-output-preserved') { throw } }
    Write-Output 'webFormsHandlerSummaryPublicTests=passed'
} finally {
    Remove-Item Function:\dotnet
    Remove-Variable handlerFail -Scope Global
}
