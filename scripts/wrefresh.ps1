#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Root,
    [string]$Project
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Read-Locator([string]$Path, [string]$Schema) {
    if ((Get-Item -LiteralPath $Path).Length -gt 1048576) { throw 'WEBFORMS_REFRESH_LOCATOR_TOO_LARGE' }
    $document = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($document.schemaVersion -cne $Schema) { throw 'WEBFORMS_REFRESH_SCHEMA_INVALID' }
    return $document
}
$Root = [IO.Path]::GetFullPath($Root)
$rootDocument = Read-Locator (Join-Path $Root 'root.config.json') 'webforms-wizard-root.v1'
$projects = @($rootDocument.configuration.projects)
if (!$Project) {
    if ($projects.Count -ne 1) { throw 'Specify -Project for a multi-project root.' }
    $Project = [string]$projects[0].id
}
if ($Project -cnotmatch '\A[a-z][a-z0-9-]{0,47}\z') { throw 'WEBFORMS_REFRESH_PROJECT_INVALID' }
$reference = @($projects | Where-Object { $_.id -ceq $Project })
if ($reference.Count -ne 1) { throw 'WEBFORMS_REFRESH_PROJECT_NOT_FOUND' }
$projectPath = Join-Path $Root "$Project/project.config.json"
if ((Get-FileHash -LiteralPath $projectPath).Hash -ine $reference[0].configSha256) { throw 'WEBFORMS_REFRESH_PROJECT_HASH_MISMATCH' }
$saved = Read-Locator $projectPath 'webforms-wizard-project.v1'
if ($saved.configuration.id -cne $Project -or $saved.configuration.step -cne 'completed') { throw 'WEBFORMS_REFRESH_PROJECT_NOT_COMPLETED' }
$relative = [string]$saved.configuration.run.relativeRoot
if ($relative -cnotmatch ('\Aruns/' + [regex]::Escape($Project) + '-[a-f0-9]{32}\z')) { throw 'WEBFORMS_REFRESH_RUN_LOCATOR_INVALID' }
$config = Join-Path (Join-Path $Root $relative) 'evidence/review-config.local.json'
if (!(Test-Path -LiteralPath $config -PathType Leaf)) { throw "Prepared configuration missing: $config" }
# Native start owns configuration, receipt and source validation. Never infer a
# new source attestation, rewrite wizard state, or resume the old scan.
$repo = Split-Path $PSScriptRoot -Parent
& dotnet build (Join-Path $repo 'src/dotnet/TraceMap.Cli') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_REFRESH_BUILD_FAILED' }
$cli = Join-Path $repo 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
$fresh = Join-Path $Root ('refresh-' + $Project + '-' + [guid]::NewGuid().ToString('N'))
Write-Output "Fresh review root: $fresh"
& dotnet $cli webforms-review start --config $config --out $fresh
if ($LASTEXITCODE -ne 0) { throw "WEBFORMS_REFRESH_FAILED; retained output (if created): $fresh" }
Write-Output "Fresh review completed: $fresh"
Write-Output 'Old run and wizard configuration preserved. Use this new review root for subsequent diagnostics.'
