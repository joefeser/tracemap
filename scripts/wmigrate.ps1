[CmdletBinding()]
param([string[]]$ReviewRoots = @())

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!$PSBoundParameters.ContainsKey('ReviewRoots')) {
    Write-Host 'Select the copied review folders. Nothing will be scanned or overwritten.'
    $first = Read-Host 'Copied Web Forms review folder (parent of config)'
    if ([string]::IsNullOrWhiteSpace($first)) { throw 'WEBFORMS_CONFIG_MIGRATION_REVIEW_ROOT_REQUIRED' }
    $second = Read-Host 'Copied backend review folder (leave blank to migrate only one)'
    $ReviewRoots = @($first, $second)
}
$ReviewRoots = @($ReviewRoots | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim().Trim('"') })
if ($ReviewRoots.Count -eq 0) { throw 'WEBFORMS_CONFIG_MIGRATION_REVIEW_ROOT_REQUIRED' }
# Check every selection before spending time building or producing any drafts.
foreach ($root in $ReviewRoots) {
    $configFolder = Join-Path $root 'config'
    $configs = @('webforms-review.json', 'webforms-review.jsonc' | ForEach-Object {
        $candidate = Join-Path $configFolder $_
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $candidate }
    })
    if ($configs.Count -ne 1) {
        Write-Host "Selected review folder: $root"
        throw "WEBFORMS_CONFIG_MIGRATION_CONFIG_SELECTION: expected exactly one config/webforms-review.json or .jsonc; found $($configs.Count). Select the parent of config."
    }
    if (Test-Path -LiteralPath (Join-Path $root 'native-config')) {
        throw 'WEBFORMS_CONFIG_MIGRATION_OUTPUT_EXISTS: native-config already exists; preserved unchanged.'
    }
}
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
