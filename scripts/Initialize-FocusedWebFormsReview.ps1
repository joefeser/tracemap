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

$configPath = Join-Path $configDirectory 'webforms-review.jsonc'
$jsonRoot = $root.Replace('\', '/')
$config = @"
{
  "schemaVersion": "focused-webforms-review-config.v1",

  // Absolute root of the authorized, committed source checkout.
  "sourceRoot": "C:/path/to/authorized-source",

  // Relative folder scopes. Each folder may contain any number of projects.
  // Use "." when the repository root itself is the correct scope.
  "webFormsFolder": "Web",
  "backendFolder": "Backend",
  "controlsFolder": "SharedControls",

  // Modes: solution, projects, discover, or projectless.
  // projectRelativePaths is used only by projects mode.
  "projectSelection": {
    "mode": "solution",
    "solutionRelativePath": "Application.sln",
    "projectRelativePaths": []
  },

  // Must remain exactly equal to this review root.
  "outputRoot": "$jsonRoot",

  // Modes: all, or selected with one or more .aspx routes in forms.
  "pageSelection": {
    "mode": "all",
    "forms": []
  }
}
"@
[IO.File]::WriteAllText($configPath, ($config + "`n"), [Text.UTF8Encoding]::new($false))

$readmePath = Join-Path $root 'README.md'
$readme = @"
# Focused Web Forms review run

This folder is private, local working state. Do not commit or share it as a
whole. Edit `config/webforms-review.jsonc`, then run from the TraceMap checkout:

```powershell
.\scripts\Test-FocusedWebFormsReviewConfig.ps1 -ReviewRoot '$($root.Replace("'", "''"))'
.\scripts\Invoke-FocusedWebFormsPipeline.ps1 -ReviewRoot '$($root.Replace("'", "''"))'
```

After completion, print the receipt-validated alias-only totals:

```powershell
.\scripts\Show-FocusedWebFormsOutlierSummary.ps1 -ReviewRoot '$($root.Replace("'", "''"))'
```

Keep `config/`, `run-receipt.json`, `scan/`, `packet/`, `evidence-docs/`, and
`workbench/`. The receipt binds their exact paths and hashes. `logs/` contains
diagnostic progress and summaries and may be archived or deleted only after the
run is accepted. Do not mix artifacts from different review roots.

Project selection modes are `solution`, `projects`, `discover`, and
`projectless`. `discover` searches for `.csproj` and `.vbproj` only beneath the
three configured folders. Page selection modes are `all` and `selected`. Use
forward slashes in every JSON path, for example `C:/source/application`; a
single Windows backslash is an unsafe JSON escape and is rejected.

First-run recovery: if `solution` has no in-scope projects, correct the folder
boundaries or try `discover`. If discovery is empty, use `projectless`. After
editing a config that produced only a failed pre-scan receipt, remove only
`run-receipt.json` and rerun. Never remove a receipt when a retained scan,
packet, evidence-docs, or workbench stage completed; restore the original config
or start a new empty review root instead.
"@
[IO.File]::WriteAllText($readmePath, $readme, [Text.UTF8Encoding]::new($false))

Write-Host "webformsReviewSetup=completed"
Write-Host "reviewRoot=$root"
Write-Host "configPath=$configPath"
Write-Host 'nextAction=edit-config-then-run-pipeline'
