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
    $start.ArgumentList.Clear()
    foreach ($argument in @('-NoProfile', '-File', $helper, '-SearchRoot', $temp)) { [void]$start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $process.StandardInput.WriteLine('verify-2')
    $process.StandardInput.Close()
    $namedOutput = $process.StandardOutput.ReadToEnd()
    $namedError = $process.StandardError.ReadToEnd()
    if (!$process.WaitForExit(10000) -or $process.ExitCode -ne 0) { throw "Named selection failed: $namedError" }
    if ($namedOutput -notmatch 'checkpoint=2;state=scan-failed;gaps=ScanOrArtifactValidationFailed') { throw 'Unique run name failed' }
    $afterSelection = Get-ChildItem -LiteralPath $temp -File -Recurse | ForEach-Object { "$($_.FullName):$([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($_.FullName))))" }
    if (Compare-Object $before $afterSelection) { throw 'Selection changed retained bytes' }
    $reportAttempt = 'reports/' + [Guid]::NewGuid().ToString('N')
    $report = Join-Path $run $reportAttempt
    [void][IO.Directory]::CreateDirectory((Join-Path $report 'compiled'))
    [IO.File]::WriteAllText((Join-Path $report 'combined.sqlite'), 'private combined bytes')
    [IO.File]::WriteAllText((Join-Path $report 'selected-pages.local.txt'), 'private page names')
    [IO.File]::WriteAllText((Join-Path $checkpoints '0003.json'), '{"sequence":3,"state":"reports-failed","gaps":["ReportOrArtifactValidationFailed"],"phaseUsage":{"phase":"reports","elapsedMilliseconds":1234,"maximumObservedWorkingSetBytes":987654,"successfulMemorySamples":2},"reports":{"reportAttempt":"' + $reportAttempt + '"}}')
    $before = Get-ChildItem -LiteralPath $temp -File -Recurse | ForEach-Object { "$($_.FullName):$([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($_.FullName))))" }
    $reportOutput = @(& $helper -RunRoot $run)
    if ($reportOutput -notcontains 'reportPartial.combined=present;bytes=22') { throw 'Report footprint missing combined index count' }
    if ($reportOutput -notcontains 'reportPartial.selectedPages=present;bytes=18') { throw 'Report footprint missing selected pages count' }
    if ($reportOutput -notcontains 'reportPartial.compiledJson=missing') { throw 'Report footprint missing later-stage absence' }
    if ($reportOutput -notcontains 'phaseUsage.3=reports;elapsedMs=1234;maxObservedWorkingSetBytes=987654;samples=2;sampled-parent-process-only') { throw 'Safe phase usage missing' }
    if (($reportOutput -join "`n") -match 'private|tracemap-status-helper-test') { throw 'Report footprint leaked private content' }
    $configDir = Join-Path (Split-Path (Split-Path $run -Parent) -Parent) 'configuration'
    [void][IO.Directory]::CreateDirectory($configDir)
    $config = Join-Path $configDir 'review-config.local.json'
    [IO.File]::WriteAllText($config, '{"pageMode":"selected","budgets":{"graphMaxDepth":20,"graphMaxPaths":256,"graphMaxWork":2000000}}')
    $before = Get-ChildItem -LiteralPath $temp -File -Recurse | ForEach-Object { "$($_.FullName):$([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($_.FullName))))" }
    $repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $project = Join-Path $repo 'src/dotnet/TraceMap.ReportProbe/TraceMap.ReportProbe.csproj'
    & dotnet build $project --nologo --verbosity quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
    $probe = Join-Path $repo 'src/dotnet/TraceMap.ReportProbe/bin/Debug/net10.0/TraceMap.ReportProbe.dll'
    $probeOutput = @(& dotnet $probe $config $report)
    if ($LASTEXITCODE -ne 1 -or ($probeOutput -join "`n") -notmatch 'probe.failureStage=packet;exceptionType=') {
        throw 'Probe did not identify malformed public fixture at packet stage'
    }
    if (($probeOutput -join "`n") -match 'private combined|private page|tracemap-status-helper-test') { throw 'Probe leaked private fixture content' }
    $start.ArgumentList.Clear()
    foreach ($argument in @('-NoProfile', '-File', $helper, '-RunRoot', $run, '-Probe')) { [void]$start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $helperProbeOutput = $process.StandardOutput.ReadToEnd()
    $helperProbeError = $process.StandardError.ReadToEnd()
    if (!$process.WaitForExit(20000) -or $process.ExitCode -eq 0 -or
        $helperProbeOutput -notmatch 'probe.failureStage=packet;exceptionType=') {
        throw "Status probe selection failed: $helperProbeError"
    }
    if ($helperProbeOutput -match 'private combined|private page|tracemap-status-helper-test') {
        throw 'Status probe leaked private fixture content'
    }
    $after = Get-ChildItem -LiteralPath $temp -File -Recurse | ForEach-Object { "$($_.FullName):$([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($_.FullName))))" }
    if (Compare-Object $before $after) { throw 'Status helper changed retained bytes' }
    Write-Output 'webFormsRunStatusHelperPublicTests=passed'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
