$ErrorActionPreference = 'Stop'
$guard = Join-Path (Split-Path -Parent $PSScriptRoot) 'Invoke-BoundedReportProcess.ps1'
$pwsh = (Get-Process -Id $PID).Path
& $guard -Executable $pwsh -Arguments @('-NoProfile','-Command','exit 0') -TimeoutSeconds 10
foreach ($case in @(
    @{ Code='exit 7'; Seconds=10; Memory=4096; Expected='*report-process=failed*' },
    @{ Code='Start-Sleep -Seconds 30'; Seconds=1; Memory=4096; Expected='*reason=timeout*' },
    @{ Code='Start-Sleep -Seconds 30'; Seconds=10; Memory=16; Expected='*reason=memory*' }
)) {
    $matched = $false
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try { & $guard -Executable $pwsh -Arguments @('-NoProfile','-Command',$case.Code) -TimeoutSeconds $case.Seconds -MemoryLimitMiB $case.Memory }
    catch { $matched = $_.Exception.Message -like $case.Expected }
    if (-not $matched -or $timer.Elapsed.TotalSeconds -gt 15) { throw 'Process guard regression failed.' }
}
Write-Host 'PASS success, failure, timeout, and sampled memory stop'
