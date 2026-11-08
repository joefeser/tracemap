# Diagnose the existing Windows scan processes after the launcher crash

Use this dispatch before starting or retrying a scan. Collect one local, read-only snapshot series. Keep command lines, paths, identifiers, stage logs and precise timestamps on the authorized machine. Return one sanitized table. No process termination, dumps, installations, output cleanup or new scans.

1. Identify the processes and launch roles locally. Look for the native CLI, dotnet executing tracemap.dll or dotnet run, and any children that actually do the scan work. Check command lines privately for the command, repo, output and diagnostic checkpoint options. Distinguish a wrapper and its child from two independent scans. Associate each process with a target only if the command line or local run records establish it. If the commands are gone, check existing stderr, receipts and Windows Application Error / .NET Runtime events near the reported crash. Distinguish a launcher crash from a scanner crash.

2. Use the following in PowerShell. Inspect the matching rows locally, then substitute the actual relevant PIDs (including known children) for the placeholders before running the sampling block. A creation time and PID together identify an instance. A parent with a later creation time than the child has a reused PID. If process details are inaccessible, report that.

   ```powershell
   $processes = Get-CimInstance Win32_Process
   $processes | Where-Object {
       $_.Name -match 'tracemap' -or
       ($_.Name -match '^dotnet(\.exe)?$' -and $_.CommandLine -match 'tracemap')
   } | Select-Object Name,ProcessId,ParentProcessId,CreationDate,CommandLine | Format-List
   ```

   ```powershell
   $targets = @(1111, 2222) # Replace both example values with the identified PIDs.
   for ($round = 1; $round -le 3; $round++) {
       foreach ($target in $targets) {
           $p = Get-CimInstance Win32_Process -Filter "ProcessId=$target"
           if ($null -eq $p) { "PID $target absent at sample $round"; continue }
           [pscustomobject]@{
               Sample = $round; TimeUTC = [datetime]::UtcNow; PID = $p.ProcessId
               Started = $p.CreationDate; Parent = $p.ParentProcessId
               User100ns = $p.UserModeTime; Kernel100ns = $p.KernelModeTime
               ReadBytes = $p.ReadTransferCount; WrittenBytes = $p.WriteTransferCount
               WorkingSet = $p.WorkingSetSize
           } | Format-List
       }
       if ($round -lt 3) { Start-Sleep -Seconds 30 }
   }
   ```

3. During the same interval check the exact checkpoint, stdout/stderr, receipt and output paths that the command or local run record identifies. Compare stage, size and UTC modification time. Check the last completed stage, any terminal status and whether more than one live writer targets the same output. Final artifacts may only appear at the end of a scan. A missing checkpoint is expected when diagnostics were off. Do not query an actively written database for integrity.

4. For each process report its verified age, role, parent status, CPU and I/O deltas, last advancing stage, and classification. Distinguish observed progress, activity without demonstrated progress, no progress observed during the sample, and exited. If no process survives, report the most recent durable stage or error. Do not infer a five-day hang from age or a deadlock from a flat minute. Identify the next discriminating observation and whether any completed artifacts can already be used. If the stages remain flat, state whether an existing approved managed stack sampling tool is available for a separate follow-up.
