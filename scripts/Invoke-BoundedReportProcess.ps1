param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string[]]$Arguments,
    [ValidateRange(1, 600)][int]$TimeoutSeconds = 300,
    [ValidateRange(16, 4096)][int]$MemoryLimitMiB = 4096
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 is required for process-tree cleanup.' }
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $Executable
$start.UseShellExecute = $false
foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
# Managed-heap hard limit supplements the sampled process-memory watchdog.
$start.Environment['DOTNET_GCHeapHardLimit'] = '80000000'
$start.Environment['COMPlus_GCHeapHardLimit'] = '80000000'
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
$started = $false
$clock = [Diagnostics.Stopwatch]::StartNew()
try {
    $started = $process.Start()
    if (-not $started) { throw 'Report process could not start.' }
    while (-not $process.WaitForExit(250)) {
        $process.Refresh()
        if ($clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            throw 'report-process=stopped;reason=timeout;output-status=incomplete'
        }
        if ([Math]::Max($process.WorkingSet64, $process.PrivateMemorySize64) -gt ([long]$MemoryLimitMiB * 1MB)) {
            throw 'report-process=stopped;reason=memory;output-status=incomplete'
        }
    }
    if ($process.ExitCode -ne 0) { throw 'report-process=failed;output-status=incomplete' }
}
finally {
    if ($started -and -not $process.HasExited) {
        $process.Kill($true)
        [void]$process.WaitForExit(5000)
    }
    $process.Dispose()
    $clock.Stop()
}
