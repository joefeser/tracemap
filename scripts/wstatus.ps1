[CmdletBinding()]
param(
    [string]$RunRoot,
    [string]$SearchRoot,
    [switch]$Probe,
    [switch]$ProbeWriter
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Safe-Code([object]$Value) {
    $code = [string]$Value
    if ($code -cmatch '^[A-Za-z0-9_.-]{1,96}$') { return $code }
    return 'redacted'
}
function Safe-Count([object]$Value) {
    $number = [long]0
    if ($null -ne $Value -and [long]::TryParse([string]$Value, [ref]$number) -and $number -ge 0) {
        return [string]$number
    }
    return 'unavailable'
}

if ([string]::IsNullOrWhiteSpace($RunRoot)) {
    if ([string]::IsNullOrWhiteSpace($SearchRoot)) {
        $repo = Split-Path $PSScriptRoot -Parent
        $SearchRoot = Join-Path (Split-Path $repo -Parent) 'tracemap-output'
    }
    if (!(Test-Path -LiteralPath $SearchRoot -PathType Container)) {
        throw 'WEBFORMS_STATUS_SEARCH_ROOT_UNAVAILABLE;pass-RunRoot'
    }
    # Bounded immediate children only. No timestamp or "latest" authority.
    $projects = @(Get-ChildItem -LiteralPath $SearchRoot -Directory | Select-Object -First 101)
    if ($projects.Count -gt 100) { throw 'WEBFORMS_STATUS_TOO_MANY_REVIEW_FOLDERS;pass-RunRoot' }
    $candidates = [Collections.Generic.List[object]]::new()
    foreach ($project in $projects) {
        $folders = @(Get-ChildItem -LiteralPath $project.FullName -Directory | Select-Object -First 101)
        if ($folders.Count -gt 100) { throw 'WEBFORMS_STATUS_TOO_MANY_RUN_FOLDERS;pass-RunRoot' }
        foreach ($folder in $folders) {
            $candidate = Join-Path $folder.FullName 'review/run'
            if (Test-Path -LiteralPath (Join-Path $candidate 'run-manifest.json') -PathType Leaf) {
                [void]$candidates.Add([pscustomobject]@{ Path = $candidate; Label = $folder.Name })
                if ($candidates.Count -gt 100) { throw 'WEBFORMS_STATUS_TOO_MANY_RUNS;pass-RunRoot' }
            }
        }
    }
    if ($candidates.Count -eq 0) { throw 'WEBFORMS_STATUS_NO_PINNED_RUN;pass-RunRoot' }
    $candidates = @($candidates | Sort-Object Path)
    for ($i = 0; $i -lt $candidates.Count; $i++) {
        Write-Host "[$($i + 1)] runFolder=$(Safe-Code $candidates[$i].Label)"
    }
    $selection = Microsoft.PowerShell.Utility\Read-Host 'Pinned run: enter its number or unique run folder name'
    $number = 0
    if (![int]::TryParse($selection, [ref]$number)) {
        $named = @($candidates | Where-Object { $_.Label -ceq $selection })
        if ($named.Count -ne 1) { throw 'WEBFORMS_STATUS_RUN_SELECTION_INVALID' }
        $RunRoot = $named[0].Path
    } else {
        if ($number -lt 1 -or $number -gt $candidates.Count) { throw 'WEBFORMS_STATUS_RUN_SELECTION_INVALID' }
        $RunRoot = $candidates[$number - 1].Path
    }
}

$RunRoot = [IO.Path]::GetFullPath($RunRoot.Trim().Trim('"'))
if (!(Test-Path -LiteralPath (Join-Path $RunRoot 'run-manifest.json') -PathType Leaf)) {
    throw 'WEBFORMS_STATUS_RUN_MANIFEST_UNAVAILABLE'
}
$checkpointRoot = Join-Path $RunRoot 'checkpoints'
if (!(Test-Path -LiteralPath $checkpointRoot -PathType Container)) {
    Write-Output 'checkpointCount=0;state=preflight-only;no-scan-result-admitted'
    return
}
$files = @(Get-ChildItem -LiteralPath $checkpointRoot -File -Filter '*.json' | Sort-Object Name | Select-Object -First 257)
if ($files.Count -gt 256 -or @($files | Where-Object { $_.Name -cnotmatch '^[0-9]{4}\.json$' }).Count -ne 0) {
    throw 'WEBFORMS_STATUS_CHECKPOINT_SET_INVALID'
}
Write-Output "checkpointCount=$($files.Count);readOnly=true;no-scan-started"
$last = $null
foreach ($file in @($files | Select-Object -Last 8)) {
    if ($file.Length -gt 4194304) { throw 'WEBFORMS_STATUS_CHECKPOINT_BYTES_LIMIT' }
    try { $item = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json }
    catch { throw 'WEBFORMS_STATUS_CHECKPOINT_JSON_INVALID' }
    $sequence = 0
    if (![int]::TryParse([string]$item.sequence, [ref]$sequence) -or $sequence -lt 1 -or $sequence -gt 256) {
        throw 'WEBFORMS_STATUS_CHECKPOINT_SEQUENCE_INVALID'
    }
    $state = Safe-Code $item.state
    $last = $item
    $gaps = @($item.gaps | Select-Object -First 21)
    $retained = @($gaps | Select-Object -First 20 | ForEach-Object { Safe-Code $_ })
    $suffix = if ($gaps.Count -gt 20) { ',more' } else { '' }
    Write-Output "checkpoint=$sequence;state=$state;gaps=$($retained -join ',')$suffix"
    if ($state -in @('scan-completed-reports-pending', 'scan-failed', 'reports-failed', 'reports-cancelled', 'reports-completed-review-only')) {
        $usage = if ($null -ne $item.PSObject.Properties['phaseUsage']) { $item.phaseUsage } else { $null }
        if ($null -ne $usage -and (Safe-Code $usage.phase) -in @('scan', 'reports')) {
            Write-Output "phaseUsage.$sequence=$($usage.phase);elapsedMs=$(Safe-Count $usage.elapsedMilliseconds);maxObservedWorkingSetBytes=$(Safe-Count $usage.maximumObservedWorkingSetBytes);samples=$(Safe-Count $usage.successfulMemorySamples);sampled-parent-process-only"
        }
    }
}
if ($null -ne $last -and (Safe-Code $last.state) -eq 'reports-failed') {
    $attempt = [string]$last.reports.reportAttempt
    if ($attempt -cnotmatch '^reports/[0-9a-f]{32}$') { throw 'WEBFORMS_STATUS_REPORT_ATTEMPT_INVALID' }
    $report = Join-Path $RunRoot $attempt
    $compiledDirectory = Join-Path $report 'compiled'
    $compiledState = 'missing'
    if (Test-Path -LiteralPath $compiledDirectory -PathType Container) {
        $directory = Get-Item -LiteralPath $compiledDirectory
        $compiledState = if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { 'linked' } else { 'present' }
    }
    Write-Output "reportPartial.compiledDirectory=$compiledState;pathChars=$($compiledDirectory.Length)"
    try {
        $volume = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($report))
        Write-Output "reportPartial.volumeAvailableBytes=$(Safe-Count $volume.AvailableFreeSpace);sampled-now-not-at-failure"
    } catch {
        Write-Output 'reportPartial.volumeAvailableBytes=unavailable;sampled-now-not-at-failure'
    }
    $expected = [ordered]@{
        combined = 'combined.sqlite'
        selectedPages = 'selected-pages.local.txt'
        compiledJson = 'compiled/compiled-paths.handoff.local.json'
        compiledHtml = 'compiled/compiled-paths.local.html'
        handoff = 'handoff.local.json'
        html = 'index.html'
        evidenceIndex = 'review-evidence.sqlite'
    }
    foreach ($name in $expected.Keys) {
        $file = Join-Path $report $expected[$name]
        if (!(Test-Path -LiteralPath $file -PathType Leaf)) {
            Write-Output "reportPartial.$name=missing"
            continue
        }
        $entry = Get-Item -LiteralPath $file
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            Write-Output "reportPartial.$name=linked"
            continue
        }
        Write-Output "reportPartial.$name=present;bytes=$($entry.Length)"
    }
    Write-Output 'reportPartial=unadmitted-file-presence-only;no-content-read'
}
if ($Probe) {
    if ($null -eq $last -or (Safe-Code $last.state) -ne 'reports-failed') {
        throw 'WEBFORMS_STATUS_PROBE_REQUIRES_FAILED_REPORT'
    }
    $verificationRoot = Split-Path (Split-Path $RunRoot -Parent) -Parent
    $configuration = Join-Path $verificationRoot 'configuration/review-config.local.json'
    if (!(Test-Path -LiteralPath $configuration -PathType Leaf)) {
        throw 'WEBFORMS_STATUS_PROBE_CONFIG_UNAVAILABLE'
    }
    $probeProject = Join-Path $PSScriptRoot '../src/dotnet/TraceMap.ReportProbe/TraceMap.ReportProbe.csproj'
    & dotnet build $probeProject --nologo --verbosity quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_STATUS_PROBE_BUILD_FAILED' }
    $probeDll = Join-Path $PSScriptRoot '../src/dotnet/TraceMap.ReportProbe/bin/Debug/net10.0/TraceMap.ReportProbe.dll'
    $probeArgs = @($configuration, $report)
    if ($ProbeWriter) { $probeArgs += '--writer' }
    & dotnet $probeDll @probeArgs
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_STATUS_PROBE_STAGE_FAILED;originals-preserved' }
}
elseif ($ProbeWriter) { throw 'WEBFORMS_STATUS_PROBE_WRITER_REQUIRES_PROBE' }
Write-Output 'status=retained-checkpoints-only;no-resume;no-inputs-changed'
