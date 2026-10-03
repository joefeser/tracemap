[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$IndexPath,

    [Parameter(Mandatory = $true)]
    [string]$PageListPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [ValidateRange(1, 10000)]
    [int]$MaxEventChains = 2000,

    [ValidateRange(1, 10000)]
    [int]$MaxPaths = 2000,

    [ValidateRange(1, 10000)]
    [int]$MaxGaps = 5000,

    [ValidateRange(1, 16)]
    [int]$MaxDepth = 8,

    [switch]$TargetedDepthDiagnostic
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 is required.' }
if ($MaxDepth -gt 8) {
    if (!$TargetedDepthDiagnostic -or $MaxDepth -ne 10) {
        throw 'Depths above 8 are disabled except for the bounded targeted depth-10 diagnostic.'
    }
    $pageListFile = Get-Item -LiteralPath $PageListPath -ErrorAction Stop
    if ($pageListFile.Length -le 0 -or $pageListFile.Length -gt 64KB) {
        throw 'Targeted depth-10 diagnostic requires a bounded page list.'
    }
    $targetedPages = @([IO.File]::ReadAllLines($pageListFile.FullName, [Text.UTF8Encoding]::new($false, $true)) |
        ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -Unique)
    if ($targetedPages.Count -lt 1 -or $targetedPages.Count -gt 2) {
        throw 'Targeted depth-10 diagnostic requires one or two selected pages.'
    }
    if ($MaxEventChains -gt 2000 -or $MaxPaths -gt 2000 -or $MaxGaps -gt 5000) {
        throw 'Targeted depth-10 diagnostic caps exceed the validated bounds.'
    }
}
elseif ($TargetedDepthDiagnostic) {
    throw 'TargetedDepthDiagnostic is only valid with MaxDepth 10.'
}

foreach ($required in @($IndexPath, $PageListPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required input file was not found: $required"
    }
}

$guard = Join-Path $PSScriptRoot 'Invoke-BoundedReportProcess.ps1'
$dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
# Launch the built CLI directly so the watchdog observes the report process,
# not a dotnet-run parent. Build failure never falls through to stale binaries.
& $guard -Executable $dotnet -Arguments @('build', $project, '--no-restore', '--verbosity', 'quiet')
$dll = Join-Path $repoRoot 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Built CLI not found.' }
$arguments = @(
    $dll,
    'webforms-modernization',
    '--index', $IndexPath,
    '--surface-list', $PageListPath,
    '--out', $OutputDirectory,
    '--max-surfaces', '500',
    '--max-event-chains', $MaxEventChains.ToString(),
    '--max-paths', $MaxPaths.ToString(),
    '--max-boundaries', $MaxPaths.ToString(),
    '--max-gaps', $MaxGaps.ToString(),
    '--max-depth', $MaxDepth.ToString(),
    '--max-traversal-work', '100000'
)

& $guard -Executable $dotnet -Arguments $arguments

Write-Host "page-list-report=$OutputDirectory"
Write-Host "targeted-depth-diagnostic=$($TargetedDepthDiagnostic.ToString().ToLowerInvariant())"
Write-Host "markdown=$(Join-Path $OutputDirectory 'webforms-modernization.md')"
$jsonPath = Join-Path $OutputDirectory 'webforms-modernization.json'
if (Test-Path -LiteralPath $jsonPath -PathType Leaf) {
    $packet = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
    Write-Host "truncated=$($packet.summary.truncated.ToString().ToLowerInvariant())"
    $limitGroups = @($packet.gaps |
        Where-Object { $_.classification -match '(LimitReached|TruncatedByLimit)' } |
        Group-Object -Property classification |
        Sort-Object -Property Name)
    if ($limitGroups.Count -eq 0) {
        Write-Host 'truncationGap=none'
    }
    else {
        foreach ($group in $limitGroups) {
            Write-Host "truncationGap=$($group.Name)|count=$($group.Count)"
            if ($group.Name -eq 'TruncatedByLimit') {
                foreach ($reasonGroup in @($group.Group | ForEach-Object {
                    if ($_.truncationReason -cin @('depth', 'frontier', 'path', 'cycle', 'work')) { $_.truncationReason }
                    else { 'unavailable' }
                } | Group-Object | Sort-Object Name)) {
                    Write-Host "truncationReason=$($reasonGroup.Name)|count=$($reasonGroup.Count)"
                }
            }
        }
    }
}
Write-Host 'nonClaim=static-evidence-does-not-prove-runtime-execution-or-successful-binding'
