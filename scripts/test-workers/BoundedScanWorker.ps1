param([ValidateSet('exit','sleep','allocate','child','orphan','phase-stall')][string]$Mode='exit',[string]$PidFile,[string]$PhaseFile)
$ErrorActionPreference='Stop'
if($Mode -eq 'exit'){ exit 0 }
if($Mode -eq 'phase-stall'){[IO.File]::WriteAllText($PhaseFile,'public-worker-paused');Start-Sleep -Seconds 120;exit 0}
if($Mode -eq 'sleep'){ Start-Sleep -Seconds 120; exit 0 }
if($Mode -eq 'allocate'){
    $blocks=[Collections.Generic.List[byte[]]]::new()
    try { while($true){ $block=[byte[]]::new(16MB); for($i=0;$i -lt $block.Length;$i+=4096){$block[$i]=1};$blocks.Add($block);Start-Sleep -Milliseconds 100} }
    catch { exit 23 }
}
$start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Process -Id $PID).Path;$start.UseShellExecute=$false
foreach($arg in @('-NoProfile','-File',$PSCommandPath,'-Mode',$(if($Mode -eq 'child'){'allocate'}else{'sleep'}))){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start)
if($PidFile){[IO.File]::WriteAllText($PidFile,([string]$process.Id+","+[string]$process.StartTime.ToUniversalTime().Ticks))}
if($Mode -eq 'orphan'){Start-Sleep -Seconds 2;exit 0}
$process.WaitForExit();exit $process.ExitCode
