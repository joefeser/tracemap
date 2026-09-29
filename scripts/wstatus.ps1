[CmdletBinding()]
param(
    [string]$RunRoot,
    [string]$SearchRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Safe-Code([object]$Value) {
    $code = [string]$Value
    if ($code -cmatch '^[A-Za-z0-9_.-]{1,96}$') { return $code }
    return 'redacted'
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
    $selection = Microsoft.PowerShell.Utility\Read-Host 'Pinned run: enter its number'
    $number = 0
    if (![int]::TryParse($selection, [ref]$number) -or $number -lt 1 -or $number -gt $candidates.Count) {
        throw 'WEBFORMS_STATUS_RUN_SELECTION_INVALID'
    }
    $RunRoot = $candidates[$number - 1].Path
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
foreach ($file in @($files | Select-Object -Last 8)) {
    if ($file.Length -gt 4194304) { throw 'WEBFORMS_STATUS_CHECKPOINT_BYTES_LIMIT' }
    try { $item = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json }
    catch { throw 'WEBFORMS_STATUS_CHECKPOINT_JSON_INVALID' }
    $sequence = 0
    if (![int]::TryParse([string]$item.sequence, [ref]$sequence) -or $sequence -lt 1 -or $sequence -gt 256) {
        throw 'WEBFORMS_STATUS_CHECKPOINT_SEQUENCE_INVALID'
    }
    $state = Safe-Code $item.state
    $gaps = @($item.gaps | Select-Object -First 21)
    $retained = @($gaps | Select-Object -First 20 | ForEach-Object { Safe-Code $_ })
    $suffix = if ($gaps.Count -gt 20) { ',more' } else { '' }
    Write-Output "checkpoint=$sequence;state=$state;gaps=$($retained -join ',')$suffix"
}
Write-Output 'status=retained-checkpoints-only;no-resume;no-inputs-changed'
