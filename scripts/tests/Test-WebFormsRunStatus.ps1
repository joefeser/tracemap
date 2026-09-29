$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wstatus.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-status-helper-test-' + [Guid]::NewGuid().ToString('N'))
$run = Join-Path $temp 'review-1/verify-2/review/run'
$checkpoints = Join-Path $run 'checkpoints'
[void][IO.Directory]::CreateDirectory($checkpoints)
[IO.File]::WriteAllText((Join-Path $run 'run-manifest.json'), '{"private":"do-not-print"}')
[IO.File]::WriteAllText((Join-Path $checkpoints '0001.json'), '{"sequence":1,"state":"scan-started","gaps":["Private/DoNotPrint"]}')
[IO.File]::WriteAllText((Join-Path $checkpoints '0002.json'), '{"sequence":2,"state":"scan-failed","gaps":["ScanOrArtifactValidationFailed"]}')
try {
    $before = Get-ChildItem -LiteralPath $temp -File -Recurse | ForEach-Object { "$($_.FullName):$([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($_.FullName))))" }
    $output = @(& $helper -RunRoot $run)
    if ($output -notcontains 'checkpoint=2;state=scan-failed;gaps=ScanOrArtifactValidationFailed') { throw 'Missing retained failure state' }
    if ($output -notcontains 'checkpoint=1;state=scan-started;gaps=redacted') { throw 'Unsafe gap was not redacted' }
    if (($output -join "`n") -match 'do-not-print|DoNotPrint|tracemap-status-helper-test') { throw 'Private diagnostic content leaked' }
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh).Source)
    $start.UseShellExecute = $false
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-File', $helper, '-SearchRoot', $temp)) { [void]$start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $process.StandardInput.WriteLine('1')
    $process.StandardInput.Close()
    $discovered = $process.StandardOutput.ReadToEnd()
    $diagnosticError = $process.StandardError.ReadToEnd()
    if (!$process.WaitForExit(10000) -or $process.ExitCode -ne 0) { throw "Discovery failed: $diagnosticError" }
    if ($discovered -notmatch 'checkpoint=2;state=scan-failed;gaps=ScanOrArtifactValidationFailed') { throw 'Numeric run selection failed' }
    if ($discovered -match 'do-not-print|DoNotPrint|tracemap-status-helper-test') { throw 'Discovery leaked private content' }
    $after = Get-ChildItem -LiteralPath $temp -File -Recurse | ForEach-Object { "$($_.FullName):$([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($_.FullName))))" }
    if (Compare-Object $before $after) { throw 'Status helper changed retained bytes' }
    Write-Output 'webFormsRunStatusHelperPublicTests=passed'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
