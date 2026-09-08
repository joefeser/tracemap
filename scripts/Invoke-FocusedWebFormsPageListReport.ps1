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
    [int]$MaxGaps = 5000
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'

foreach ($required in @($IndexPath, $PageListPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required input file was not found: $required"
    }
}

$arguments = @(
    'run', '--project', $project, '--',
    'webforms-modernization',
    '--index', $IndexPath,
    '--surface-list', $PageListPath,
    '--out', $OutputDirectory,
    '--max-surfaces', '500',
    '--max-event-chains', $MaxEventChains.ToString(),
    '--max-paths', $MaxPaths.ToString(),
    '--max-boundaries', $MaxPaths.ToString(),
    '--max-gaps', $MaxGaps.ToString()
)

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "TraceMap page-list report failed with exit code $LASTEXITCODE."
}

Write-Host "page-list-report=$OutputDirectory"
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
                    if ($_.truncationReason -cin @('depth', 'frontier', 'path', 'cycle')) { $_.truncationReason }
                    else { 'unavailable' }
                } | Group-Object | Sort-Object Name)) {
                    Write-Host "truncationReason=$($reasonGroup.Name)|count=$($reasonGroup.Count)"
                }
            }
        }
    }
}
Write-Host 'nonClaim=static-evidence-does-not-prove-runtime-execution-or-successful-binding'
