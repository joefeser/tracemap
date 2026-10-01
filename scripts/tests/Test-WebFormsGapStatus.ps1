$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wstatus.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-gap-test-' + [Guid]::NewGuid().ToString('N'))
$run = Join-Path $temp 'verify-4/review/run'
[void][IO.Directory]::CreateDirectory((Join-Path $run 'checkpoints'))
[void][IO.Directory]::CreateDirectory((Join-Path $temp 'verify-4/tool'))
[IO.File]::WriteAllText((Join-Path $run 'run-manifest.json'), '{}')
[IO.File]::WriteAllText((Join-Path $run 'checkpoints/0001.json'), '{"sequence":1,"state":"reports-completed-review-only","gaps":[]}')
[IO.File]::WriteAllText((Join-Path $temp 'verify-4/tool/tracemap.dll'), 'synthetic')
$global:gapQueryFailure = $false
function global:dotnet {
    if (($args -join ' ') -notmatch 'webforms-review query --run .+ --document compiled --pointer /header/gaps --limit 50 --depth 2') { throw 'Wrong bounded query' }
    $global:LASTEXITCODE = if ($global:gapQueryFailure) { 1 } else { 0 }
    if ($global:gapQueryFailure) { 'private-error-do-not-print'; return }
    '{"private":"private-identity-do-not-print","result":{"omittedChildren":2,"children":[{"children":[{"pointer":"/header/gaps/0/gapKind","value":"GraphInputLimitReached"},{"pointer":"/header/gaps/0/reason","value":"graph-facts"},{"pointer":"/header/gaps/0/filePath","value":"private-path-do-not-print"}]},{"children":[{"pointer":"/header/gaps/1/gapKind","value":"Unknown/Private"},{"pointer":"/header/gaps/1/reason","value":null}]}]}}'
}
try {
    $before = @(Get-ChildItem $temp -File -Recurse | ForEach-Object { (Get-FileHash $_.FullName).Hash })
    $output = @(& $helper -RunRoot $run -Gaps)
    if ($output -notcontains 'compiledGap.kind=GraphInputLimitReached;reason=graph-facts') { throw 'Missing admission reason' }
    if ($output -notcontains 'compiledGap.kind=redacted;reason=none') { throw 'Unsafe value or null not handled' }
    if ($output -notcontains 'compiledGap.returned=2;omitted=2') { throw 'Missing truncation count' }
    if (($output -join "`n") -match 'private|tracemap-gap-test') { throw 'Private query content leaked' }
    $global:gapQueryFailure = $true
    try { & $helper -RunRoot $run -Gaps | Out-Null; throw 'Expected failure' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_STATUS_GAP_QUERY_FAILED;originals-preserved') { throw } }
    $after = @(Get-ChildItem $temp -File -Recurse | ForEach-Object { (Get-FileHash $_.FullName).Hash })
    if (Compare-Object $before $after) { throw 'Retained fixture changed' }
    Write-Output 'webFormsGapStatusPublicTests=passed'
} finally {
    Remove-Item Function:\dotnet
    Remove-Variable gapQueryFailure -Scope Global
    Remove-Item $temp -Recurse -Force
}
