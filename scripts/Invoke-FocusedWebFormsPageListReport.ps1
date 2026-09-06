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
    [int]$MaxPaths = 2000
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
    '--max-boundaries', $MaxPaths.ToString()
)

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "TraceMap page-list report failed with exit code $LASTEXITCODE."
}

Write-Host "page-list-report=$OutputDirectory"
Write-Host "markdown=$(Join-Path $OutputDirectory 'webforms-modernization.md')"
Write-Host 'nonClaim=static-evidence-does-not-prove-runtime-execution-or-successful-binding'
