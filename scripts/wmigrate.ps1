[CmdletBinding()]
param([string[]]$ReviewRoots = @())

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($ReviewRoots.Count -eq 0) {
    $ReviewRoots = @(
        (Read-Host 'Copied Web Forms review folder (the folder containing config)'),
        (Read-Host 'Copied backend review folder (leave blank to migrate only one)')
    ) | Where-Object { ![string]::IsNullOrWhiteSpace($_) }
}
if ($ReviewRoots.Count -eq 0) { throw 'WEBFORMS_CONFIG_MIGRATION_REVIEW_ROOT_REQUIRED' }
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
dotnet build $project --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_CONFIG_MIGRATION_BUILD_FAILED' }
$cli = Join-Path $repo 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
foreach ($root in $ReviewRoots) {
    $root = [IO.Path]::GetFullPath($root)
    $destination = Join-Path $root 'native-config'
    dotnet $cli webforms-review migrate-config --review-root $root --out $destination
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_CONFIG_MIGRATION_FAILED;previous-successful-drafts-preserved' }
    Write-Host "draftConfig=$(Join-Path $destination 'review.draft.json')"
    Write-Host "missingInputChecklist=$(Join-Path $destination 'migration.local.json')"
}
Write-Host 'Migration produced drafts only. Original configs and scans are unchanged. No scan, merge, publish or attestation was performed.'
