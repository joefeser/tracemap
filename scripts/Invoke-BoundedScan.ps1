param(
    [string]$Executable,
    [string[]]$Arguments,
    [string]$CheckpointPath,
    [string]$PhaseCheckpointPath,
    [ValidateRange(1,120)][int]$TimeoutSeconds = 60,
    [ValidateRange(128,4096)][int]$MemoryLimitMiB = 2048,
    [switch]$PublicTestMode,
    [switch]$GuardWorker
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'guard-requires-powershell-7' }
function HashText([string]$value) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($value))).ToLowerInvariant() }
function SaveCheckpoint([string]$state, [string]$reason, [long]$peak, [int]$childPid, [string]$metric) {
    $record = [ordered]@{ schema='bounded-scan-guard/1'; generatorSha256=$script:generator; boundedInputSha256=$script:inputHash;
        inputProjection=$script:projection; state=$state; reason=$reason; elapsedMilliseconds=[long]$script:clock.ElapsedMilliseconds;
        peakBytes=$peak; peakWorkingSetBytes=$script:peakRss; metric=$metric; guardPid=$PID; childPid=$childPid;
        limitation='operational-only;not-scan-completion;private-argument-values-not-bound;no-causal-attribution' }
    $temporary = $script:checkpoint + '.tmp'
    [IO.File]::WriteAllText($temporary, ($record | ConvertTo-Json -Depth 6))
    [IO.File]::Move($temporary,$script:checkpoint,$true)
}
if (-not $GuardWorker) {
    if (-not $Executable -or -not $Arguments -or -not $CheckpointPath) { throw 'guard-required-argument-missing' }
    $checkpoint = [IO.Path]::GetFullPath($CheckpointPath)
    if (-not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($checkpoint)) -or [IO.File]::Exists($checkpoint) -or [IO.Directory]::Exists($checkpoint) -or [IO.File]::Exists($checkpoint+'.tmp')) { throw 'guard-checkpoint-must-be-fresh-with-existing-parent' }
    if ($PublicTestMode) {
        $worker = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'test-workers/BoundedScanWorker.ps1'))
        if ([IO.Path]::GetFileNameWithoutExtension($Executable) -ne 'pwsh' -or -not ($Arguments -ccontains $worker)) { throw 'public-test-mode-requires-repository-test-worker' }
    } else {
        if (-not $IsWindows) { throw 'production-guard-requires-windows-job-object' }
        $phaseIndex=[Array]::IndexOf($Arguments,'--diagnostic-progress')
        if($phaseIndex -lt 0 -or $phaseIndex+1 -ge $Arguments.Count){throw 'production-guard-requires-scan-phase-checkpoint'}
        $PhaseCheckpointPath=[IO.Path]::GetFullPath($Arguments[$phaseIndex+1])
    }
    if($PhaseCheckpointPath){
        $PhaseCheckpointPath=[IO.Path]::GetFullPath($PhaseCheckpointPath)
        if([IO.File]::Exists($PhaseCheckpointPath) -or $PhaseCheckpointPath -eq $checkpoint){throw 'phase-checkpoint-must-be-fresh-and-distinct'}
    }
    if (-not $IsWindows -and -not $PublicTestMode) { throw 'production-guard-requires-windows-job-object' }
    $payload = @{ executable=$Executable; arguments=$Arguments; checkpoint=$checkpoint; seconds=$TimeoutSeconds; memoryMiB=$MemoryLimitMiB; publicTest=[bool]$PublicTestMode; phaseCheckpoint=$PhaseCheckpointPath } | ConvertTo-Json -Compress
    if ($payload.Length -gt 12000) { throw 'guard-argument-transport-limit' }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Process -Id $PID).Path
    $start.UseShellExecute=$false
    foreach ($arg in @('-NoProfile','-File',$PSCommandPath,'-GuardWorker')) { $start.ArgumentList.Add($arg) }
    $start.Environment['TRACEMAP_GUARD_REQUEST']=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($payload))
    $process=[Diagnostics.Process]::new(); $process.StartInfo=$start
    try {
        if (-not $process.Start()) { throw 'guard-worker-start-failed' }
        if (-not $process.WaitForExit(($TimeoutSeconds+15)*1000)) { $process.Kill($true); [void]$process.WaitForExit(5000); throw 'guard-worker-timeout;output-incomplete' }
        exit $process.ExitCode
    } finally { if (-not $process.HasExited) { $process.Kill($true) }; $process.Dispose() }
}
# This dedicated worker joins its Windows job BEFORE starting the scanner.
# The job handle stays rooted until this worker exits, killing all remaining job children.
$request=$env:TRACEMAP_GUARD_REQUEST
[Environment]::SetEnvironmentVariable('TRACEMAP_GUARD_REQUEST',$null)
if (-not $request -or $request.Length -gt 16000) { throw 'guard-worker-request-invalid' }
$config=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($request)) | ConvertFrom-Json
$script:checkpoint=[IO.Path]::GetFullPath($config.checkpoint)
$script:generator=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$script:projection=[ordered]@{ timeoutSeconds=[int]$config.seconds; memoryLimitMiB=[int]$config.memoryMiB;
    argumentCount=$config.arguments.Count; phaseCheckpoint=[bool]$config.phaseCheckpoint; publicTest=[bool]$config.publicTest; platform=$(if($IsWindows){'windows-job'}else{'sampled-public-test-only'});
    executableKind=$(if([IO.Path]::GetFileNameWithoutExtension($config.executable) -in @('dotnet','pwsh')){[IO.Path]::GetFileNameWithoutExtension($config.executable)}else{'other'}) }
$script:inputHash=HashText ($script:projection | ConvertTo-Json -Compress)
$script:peakRss=0L
$script:clock=[Diagnostics.Stopwatch]::StartNew()
SaveCheckpoint 'starting' 'not-launched' 0 0 'not-observed'
if ($IsWindows) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class TraceMapScanJob {
    private static SafeFileHandle job;
    [StructLayout(LayoutKind.Sequential)] struct Basic { public long UserTime,JobTime; public uint Flags; public UIntPtr Min,Max; public uint Active; public UIntPtr Affinity; public uint Priority,Scheduling; }
    [StructLayout(LayoutKind.Sequential)] struct Io { public ulong A,B,C,D,E,F; }
    [StructLayout(LayoutKind.Sequential)] struct Extended { public Basic Basic; public Io Io; public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob; }
    [DllImport("kernel32.dll",SetLastError=true)] static extern SafeFileHandle CreateJobObject(IntPtr attributes,string name);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(SafeFileHandle handle,int type,ref Extended info,uint length);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool QueryInformationJobObject(SafeFileHandle handle,int type,out Extended info,uint length,IntPtr returned);
    [DllImport("kernel32.dll",SetLastError=true,EntryPoint="QueryInformationJobObject")] static extern bool QueryProcessIds(SafeFileHandle handle,int type,IntPtr info,uint length,IntPtr returned);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(SafeFileHandle handle,IntPtr process);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    public static void AssignSelf(ulong bytes) {
        if(IntPtr.Size!=8) throw new InvalidOperationException("guard-requires-64-bit");
        job=CreateJobObject(IntPtr.Zero,null);
        if(job.IsInvalid) throw new InvalidOperationException("guard-job-create-failed");
        var limits=new Extended { Basic=new Basic { Flags=0x2000|0x200|0x8,Active=16 },JobMemory=new UIntPtr(bytes) };
        if(!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf<Extended>()) || !AssignProcessToJobObject(job,GetCurrentProcess()))
            throw new InvalidOperationException("guard-job-assignment-failed");
    }
    public static long WorkingSet() {
        IntPtr buffer=Marshal.AllocHGlobal(8+64*IntPtr.Size);
        try {
            if(!QueryProcessIds(job,3,buffer,(uint)(8+64*IntPtr.Size),IntPtr.Zero)) throw new InvalidOperationException("guard-job-process-query-failed");
            int count=Marshal.ReadInt32(buffer,4);
            if(count<0 || count>64) throw new InvalidOperationException("guard-job-process-limit");
            long total=0;
            for(int i=0;i<count;i++) {
                int id=checked((int)Marshal.ReadIntPtr(buffer,8+i*IntPtr.Size).ToInt64());
                try { using(var process=System.Diagnostics.Process.GetProcessById(id)) total=checked(total+process.WorkingSet64); }
                catch(ArgumentException) {} catch(InvalidOperationException) {}
            }
            return total;
        } finally { Marshal.FreeHGlobal(buffer); }
    }
    public static ulong Peak() {
        Extended info;
        if(!QueryInformationJobObject(job,9,out info,(uint)Marshal.SizeOf<Extended>(),IntPtr.Zero)) throw new InvalidOperationException("guard-job-query-failed");
        return info.PeakJob.ToUInt64();
    }
}
'@
    try { [TraceMapScanJob]::AssignSelf([ulong]$config.memoryMiB*1MB) }
    catch { SaveCheckpoint 'failed' 'job-setup-no-child-launched' 0 0 'job-commit'; exit 125 }
} elseif (-not $config.publicTest) { SaveCheckpoint 'failed' 'unsupported-platform-no-child-launched' 0 0 'not-observed'; exit 125 }
$start=[Diagnostics.ProcessStartInfo]::new(); $start.FileName=$config.executable; $start.UseShellExecute=$false
foreach($argument in $config.arguments) { $start.ArgumentList.Add([string]$argument) }
$start.Environment['DOTNET_GCHeapHardLimit']='80000000'; $start.Environment['COMPlus_GCHeapHardLimit']='80000000'
$process=[Diagnostics.Process]::new(); $process.StartInfo=$start
$known=@{}; $peak=0L; $childPid=0; $exitCode=125; $metric=$(if($IsWindows){'job-commit'}else{'sampled-tree-rss'})
try {
    if($script:clock.Elapsed.TotalSeconds -ge $config.seconds) { throw 'guard-startup-exhausted-time-budget' }
    if(-not $process.Start()) { throw 'guard-scan-start-failed' }
    $childPid=$process.Id; $known[$childPid]=$process.StartTime.ToUniversalTime().Ticks
    SaveCheckpoint 'running' 'child-launched' 0 $childPid $metric
    while($true) {
        if($IsWindows) {
            $peak=[Math]::Max($peak,[long][TraceMapScanJob]::Peak())
            $rss=[TraceMapScanJob]::WorkingSet(); $script:peakRss=[Math]::Max($script:peakRss,$rss)
            if($rss -gt ([long]$config.memoryMiB*1MB)){SaveCheckpoint 'stopped' 'sampled-job-working-set' $peak $childPid $metric; $exitCode=123;break}
        }
        else {
            $rows=@(& ps -axo 'pid=,ppid=,rss=' | ForEach-Object { $p=$_.Trim() -split '\s+'; if($p.Count -eq 3){ ,@([int]$p[0],[int]$p[1],[long]$p[2]) } })
            do {
                $changed=$false
                foreach($row in $rows) {
                    if($known.ContainsKey($row[1]) -and -not $known.ContainsKey($row[0])) {
                        try { $candidate=Get-Process -Id $row[0] -ErrorAction Stop; $known[$row[0]]=$candidate.StartTime.ToUniversalTime().Ticks; $changed=$true; $candidate.Dispose() } catch { }
                    }
                }
            } while($changed)
            $rss=0L
            foreach($row in $rows) { if($known.ContainsKey($row[0])) { $rss += $row[2]*1KB } }
            $peak=[Math]::Max($peak,$rss); $script:peakRss=$peak
            if($known.Count -gt 64 -or $rss -gt ([long]$config.memoryMiB*1MB)) { SaveCheckpoint 'stopped' 'sampled-public-test-memory' $peak $childPid $metric; $exitCode=123; break }
        }
        if($config.phaseCheckpoint -and $script:clock.Elapsed.TotalSeconds -gt 10) {
            $phase=Get-Item -LiteralPath $config.phaseCheckpoint -ErrorAction SilentlyContinue
            if(-not $phase -or ([DateTime]::UtcNow-$phase.LastWriteTimeUtc).TotalSeconds -gt 10) {
                SaveCheckpoint 'stopped' 'phase-checkpoint-missing-or-stale' $peak $childPid $metric; $exitCode=126; break
            }
        }
        if($process.HasExited) { $exitCode=$process.ExitCode; SaveCheckpoint $(if($exitCode -eq 0){'child-exited'}else{'failed'}) $(if($exitCode -eq 0){'child-exit-zero-not-scan-proof'}else{'child-exit-nonzero'}) $peak $childPid $metric; break }
        if($script:clock.Elapsed.TotalSeconds -ge $config.seconds) { SaveCheckpoint 'stopped' 'timeout' $peak $childPid $metric; $exitCode=124; break }
        SaveCheckpoint 'running' 'observing' $peak $childPid $metric
        Start-Sleep -Milliseconds 250
    }
} catch { SaveCheckpoint 'failed' 'guard-or-child-error' $peak $childPid $metric; $exitCode=125 }
finally {
    if($childPid -gt 0 -and -not $process.HasExited) { $process.Kill($true); [void]$process.WaitForExit(5000) }
    if(-not $IsWindows) {
        foreach($key in @($known.Keys)) {
            try { $remaining=Get-Process -Id $key -ErrorAction Stop; if($remaining.StartTime.ToUniversalTime().Ticks -eq $known[$key]){ $remaining.Kill($true); [void]$remaining.WaitForExit(5000) }; $remaining.Dispose() } catch { }
        }
    }
    $process.Dispose()
}
exit $exitCode
