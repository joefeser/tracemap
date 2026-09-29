$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wstatus.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-recovery-helper-' + [Guid]::NewGuid().ToString('N'))
$run = Join-Path $temp 'verify-5/review/run'
[void][IO.Directory]::CreateDirectory((Join-Path $run 'checkpoints'))
[IO.File]::WriteAllText((Join-Path $run 'run-manifest.json'), '{}')
[IO.File]::WriteAllText((Join-Path $run 'checkpoints/0001.json'), '{"sequence":1,"state":"reports-failed","gaps":["WEBFORMS_EVIDENCE_NODE_LIMIT"],"reports":{"reportAttempt":"reports/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}')
$global:recoveryHelperCalls = @()
function global:dotnet {
    $global:recoveryHelperCalls += ,@($args)
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'build') { return }
    if (($args -join ' ') -notmatch 'webforms-review recover-reports --run .+ --out .+') { throw 'Unexpected recovery command' }
    'reportRecovery=completed-separate-bundle;exactChains=13;evidenceVariants=41;originalRunUnchanged=true;no-scan'
}
try {
    $before = @(Get-ChildItem $run -File -Recurse | ForEach-Object { (Get-FileHash $_.FullName).Hash })
    $output = @(& $helper -RunRoot $run -Recover)
    if ($global:recoveryHelperCalls.Count -ne 2) { throw 'Recovery did not build then invoke recovery exactly once' }
    if (($global:recoveryHelperCalls[1] -join ' ') -match ' resume | scan ') { throw 'Recovery launched scan/resume' }
    if ($output -notcontains 'recovery=separate-bundle;original-run-still-failed;no-scan;no-graph-traversal') { throw 'Missing recovery scope' }
    if (Compare-Object $before @(Get-ChildItem $run -File -Recurse | ForEach-Object { (Get-FileHash $_.FullName).Hash })) { throw 'Original run changed' }
    Write-Output 'webFormsRecoveryStatusPublicTests=passed'
} finally {
    Remove-Item Function:\dotnet
    Remove-Variable recoveryHelperCalls -Scope Global
    Remove-Item $temp -Recurse -Force
}
