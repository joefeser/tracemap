[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_SETUP_POWERSHELL_7_REQUIRED' }

$root = [IO.Path]::GetFullPath($ReviewRoot)
if (Test-Path -LiteralPath $root) {
    if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'WEBFORMS_SETUP_ROOT_NOT_DIRECTORY' }
    if (@(Get-ChildItem -LiteralPath $root -Force).Count -gt 0) { throw 'WEBFORMS_SETUP_ROOT_NOT_EMPTY' }
}
else {
    [IO.Directory]::CreateDirectory($root) | Out-Null
}

$configDirectory = Join-Path $root 'config'
$logsDirectory = Join-Path $root 'logs'
[IO.Directory]::CreateDirectory($configDirectory) | Out-Null
[IO.Directory]::CreateDirectory($logsDirectory) | Out-Null

$configPath = Join-Path $configDirectory 'webforms-review.json'
$config = [ordered]@{
    schemaVersion = 'focused-webforms-review-config.v1'
    sourceRoot = 'C:/path/to/authorized-source'
    webFormsFolder = 'Web'
    backendFolder = 'Backend'
    controlsFolder = 'SharedControls'
    projectSelection = [ordered]@{
        mode = 'solution'
        solutionRelativePath = 'Application.sln'
        projectRelativePaths = @()
    }
    outputRoot = $root.Replace('\', '/')
    pageSelection = [ordered]@{
        mode = 'all'
        forms = @()
    }
}
[IO.File]::WriteAllText($configPath, (($config | ConvertTo-Json -Depth 8) + "`n"), [Text.UTF8Encoding]::new($false))

$readmePath = Join-Path $root 'README.md'
$readme = @"
# Focused Web Forms review run

This folder is private, local working state. Do not commit or share it as a
whole. Edit `config/webforms-review.json`, then run from the TraceMap checkout:

```powershell
.\scripts\Invoke-FocusedWebFormsPipeline.ps1 -ReviewRoot '$($root.Replace("'", "''"))'
```

Keep `config/`, `run-receipt.json`, `scan/`, `packet/`, `evidence-docs/`, and
`workbench/`. The receipt binds their exact paths and hashes. `logs/` contains
diagnostic progress and summaries and may be archived or deleted only after the
run is accepted. Do not mix artifacts from different review roots.

Project selection modes are `solution`, `projects`, `discover`, and
`projectless`. `discover` searches for `.csproj` and `.vbproj` only beneath the
three configured folders. Page selection modes are `all` and `selected`.
"@
[IO.File]::WriteAllText($readmePath, $readme, [Text.UTF8Encoding]::new($false))

Write-Host "webformsReviewSetup=completed"
Write-Host "reviewRoot=$root"
Write-Host "configPath=$configPath"
Write-Host 'nextAction=edit-config-then-run-pipeline'

