$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion.Major -lt 7){throw 'PowerShell 7 required'}
$root=Join-Path ([IO.Path]::GetTempPath()) ('tracemap-guard-selftest-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$guard=Join-Path $PSScriptRoot 'Invoke-BoundedScan.ps1'
$worker=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'test-workers/BoundedScanWorker.ps1'))
$command=@'
$c=$env:TRACEMAP_SELFTEST_SPEC | ConvertFrom-Json
& $c.guard -Executable (Get-Process -Id $PID).Path -Arguments @('-NoProfile','-File',$c.worker,'-Mode',$c.mode,'-PidFile',$c.pidFile,'-PhaseFile',$c.phase) -CheckpointPath $c.checkpoint -PhaseCheckpointPath $c.phase -TimeoutSeconds $c.seconds -MemoryLimitMiB 512 -PublicTestMode
exit $LASTEXITCODE
'@
function RunCase($name,$mode,$seconds,$hasPhase=$false){
    $checkpoint=Join-Path $root ($name+'.json');$pidFile=Join-Path $root ($name+'.pid')
    $info=[Diagnostics.ProcessStartInfo]::new();$info.FileName=(Get-Process -Id $PID).Path;$info.UseShellExecute=$false
    foreach($arg in @('-NoProfile','-EncodedCommand',[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command)))){$info.ArgumentList.Add($arg)}
    $info.Environment['TRACEMAP_SELFTEST_SPEC']=(@{guard=$guard;worker=$worker;mode=$mode;checkpoint=$checkpoint;pidFile=$pidFile;seconds=$seconds;phase=$(if($hasPhase){Join-Path $root ($name+'.phase')}else{''})}|ConvertTo-Json -Compress)
    $process=[Diagnostics.Process]::Start($info)
    if(-not $process.WaitForExit(($seconds+25)*1000)){$process.Kill($true);throw "selftest-$name-supervisor-hung"}
    $code=$process.ExitCode;$process.Dispose()
    if(-not (Test-Path -LiteralPath $checkpoint)){throw "selftest-$name-no-checkpoint"}
    $state=Get-Content -Raw -LiteralPath $checkpoint | ConvertFrom-Json
    if($state.generatorSha256 -ne (Get-FileHash $guard -Algorithm SHA256).Hash.ToLowerInvariant() -or $state.boundedInputSha256.Length -ne 64){throw 'selftest-provenance'}
    if(Test-Path -LiteralPath $pidFile){
        $identity=(Get-Content -Raw -LiteralPath $pidFile) -split ","
        $childPid=[int]$identity[0]; $childStart=[long]$identity[1]
        Start-Sleep -Milliseconds 500
        $remaining=Get-Process -Id $childPid -ErrorAction SilentlyContinue
        if($remaining -and -not $remaining.HasExited -and $remaining.StartTime.ToUniversalTime().Ticks -eq $childStart){
            $zombie=(!$IsWindows) -and ((& ps -o stat= -p $childPid) -match '^Z')
            if(-not $zombie){$remaining.Kill($true);throw "selftest-$name-descendant-survived"}
        }
    }
    [pscustomobject]@{Code=$code;State=$state}
}
$success=RunCase 'success' 'exit' 15
if($success.Code -ne 0 -or $success.State.state -ne 'child-exited'){throw 'selftest-success-failed'}
Write-Output 'PASS success; root exit is not scan-completion proof'
$timeout=RunCase 'timeout' 'sleep' 8
if($timeout.Code -ne 124 -or $timeout.State.reason -ne 'timeout'){throw 'selftest-timeout-failed'}
Write-Output 'PASS timeout; checkpoint retained'
$memory=RunCase 'memory' 'child' 20
if($memory.Code -eq 0 -or $memory.State.reason -eq 'timeout' -or $memory.State.childPid -le 0 -or $memory.State.peakBytes -lt 128MB){throw 'selftest-child-memory-failed'}
if($IsWindows -and ($memory.State.metric -ne 'job-commit' -or $memory.State.peakBytes -gt 512MB)){throw 'selftest-job-limit-failed'}
Write-Output 'PASS child memory; process-tree limit triggered; descendant cleanup checked'
$orphan=RunCase 'orphan' 'orphan' 15
if($orphan.Code -ne 0){throw 'selftest-parent-exit-failed'}
Write-Output 'PASS parent exit; surviving descendant cleanup checked'
$stale=RunCase 'phase-stall' 'phase-stall' 20 $true
if($stale.Code -ne 126 -or $stale.State.reason -ne 'phase-checkpoint-missing-or-stale'){throw 'selftest-phase-stale-failed'}
Write-Output 'PASS stale phase checkpoint; stopped with incomplete output'
if($IsWindows){Write-Output 'WINDOWS JOB SELFTEST PASSED; 512 MiB aggregate commit; 16 active-process limit'}else{Write-Output 'PUBLIC SAMPLED SELFTEST ONLY; Windows hard-job enforcement remains untested on this host'}
Write-Output ('Local selftest artifacts: '+$root)
