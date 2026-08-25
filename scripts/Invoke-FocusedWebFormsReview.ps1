[CmdletBinding()]
param(
    [string]$SourceRoot,
    [string]$WebFormsFolder,
    [string]$BackendFolder,
    [string]$ControlsFolder,
    [string[]]$ProjectRelativePath = @(),
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent),
    [int]$TimeoutSeconds = 7200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-RequiredValue {
    param([string]$Value, [string]$Prompt)

    if (-not [string]::IsNullOrWhiteSpace($Value)) { return $Value.Trim() }
    $answer = Read-Host $Prompt
    if ([string]::IsNullOrWhiteSpace($answer)) { throw "FOCUSED_REVIEW_VALUE_REQUIRED" }
    return $answer.Trim()
}

function Resolve-RelativeChild {
    param([string]$Root, [string]$RelativePath, [string]$FailureCode, [bool]$RequireFile)

    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match '(^|/|\\)\.\.($|/|\\)') {
        throw $FailureCode
    }
    $candidate = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    if (-not $candidate.StartsWith($Root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw $FailureCode
    }
    $pathType = if ($RequireFile) { "Leaf" } else { "Container" }
    if (-not (Test-Path -LiteralPath $candidate -PathType $pathType)) { throw $FailureCode }
    return $candidate
}

$SourceRoot = Read-RequiredValue $SourceRoot "Private source repository root"
$WebFormsFolder = Read-RequiredValue $WebFormsFolder "Web Forms folder, relative to the source root"
$BackendFolder = Read-RequiredValue $BackendFolder "Backend folder, relative to the source root"
$ControlsFolder = Read-RequiredValue $ControlsFolder "Shared controls folder, relative to the source root"
if ($ProjectRelativePath.Count -eq 0) {
    $projectInput = Read-Host "Comma-separated in-scope project paths, relative to the source root (blank for a projectless scan)"
    if (-not [string]::IsNullOrWhiteSpace($projectInput)) {
        $ProjectRelativePath = @($projectInput.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    }
}

$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot).TrimEnd('\', '/')
$SourceRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $SourceRoot)).TrimEnd('\', '/')
if (git -C $TraceMapRoot status --porcelain) { throw "TRACEMAP_WORKTREE_DIRTY" }
if (git -C $SourceRoot status --porcelain) { throw "SOURCE_WORKTREE_DIRTY" }

$gitRoot = (git -C $SourceRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($gitRoot).TrimEnd('\', '/') -ne $SourceRoot) {
    throw "SOURCE_ROOT_NOT_GIT_ROOT"
}

$selectedFolders = @($WebFormsFolder, $BackendFolder, $ControlsFolder)
if (($selectedFolders | Select-Object -Unique).Count -ne 3) { throw "THREE_FOLDER_SCOPE_INVALID" }
foreach ($folder in $selectedFolders) {
    [void](Resolve-RelativeChild $SourceRoot $folder "THREE_FOLDER_SCOPE_UNAVAILABLE" $false)
}
foreach ($project in $ProjectRelativePath) {
    $projectPath = Resolve-RelativeChild $SourceRoot $project "PROJECT_SCOPE_UNAVAILABLE" $true
    $allowed = $false
    foreach ($folder in $selectedFolders) {
        $folderPath = [IO.Path]::GetFullPath((Join-Path $SourceRoot $folder)).TrimEnd('\', '/')
        if ($projectPath.StartsWith($folderPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            $allowed = $true
            break
        }
    }
    if (-not $allowed) { throw "PROJECT_OUTSIDE_THREE_FOLDER_SCOPE" }
}

Set-Location $TraceMapRoot
git fetch origin codex/work-dump-edge-case-triage
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_FETCH_FAILED" }
git switch --detach origin/codex/work-dump-edge-case-triage
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_CHECKOUT_FAILED" }
dotnet build "$TraceMapRoot\src\dotnet\TraceMap.sln"
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_BUILD_FAILED" }

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputParent = "C:\work\tracemap-output"
$progressParent = "C:\work\tracemap-progress"
$summaryParent = "C:\work\tracemap-summary"
$outRoot = Join-Path $outputParent "focused-webforms-$stamp"
$progressPath = Join-Path $progressParent "focused-webforms-$stamp.json"
New-Item -ItemType Directory -Path $outputParent, $progressParent, $summaryParent -Force | Out-Null

$reviewArguments = @("run", "--repo", $SourceRoot, "--out", $outRoot)
foreach ($folder in $selectedFolders) { $reviewArguments += @("--include", ($folder.TrimEnd('/', '\') + "/**")) }
foreach ($project in $ProjectRelativePath) { $reviewArguments += @("--project", $project) }
foreach ($pattern in @(
    ".vs/**", "**/bin/**", "**/obj/**", "**/node_modules/**", "**/dist/**",
    "**/coverage/**", "**/TestResults/**", "**/.angular/**", "**/.next/**"
)) { $reviewArguments += @("--exclude", $pattern) }
$reviewArguments += @(
    "--webforms-modernization",
    "--diagnostic-progress", $progressPath,
    "--timeout-seconds", $TimeoutSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)
)

dotnet run --project "$TraceMapRoot\src\dotnet\TraceMap.Cli" -- local-review @reviewArguments
$reviewExitCode = $LASTEXITCODE
"focused-review-process-exit=$reviewExitCode"

if (Test-Path -LiteralPath $outRoot -PathType Container) {
    & "$TraceMapRoot\scripts\Export-FocusedWebFormsEvidenceSummary.ps1" `
        -ReviewOutputPath $outRoot `
        -OutputDirectory $summaryParent
    & "$TraceMapRoot\scripts\Export-FocusedWebFormsAccuracySummary.ps1" `
        -ReviewOutputPath $outRoot `
        -WebFormsFolder $WebFormsFolder `
        -BackendFolder $BackendFolder `
        -ControlsFolder $ControlsFolder `
        -OutputDirectory $summaryParent
}

& "$TraceMapRoot\scripts\Export-FocusedWebFormsProgressSummary.ps1" -ProgressPath $progressPath
& "$TraceMapRoot\scripts\Export-FocusedWebFormsPerformanceSummary.ps1" -ProgressPath $progressPath

"retained-output-directory=$([IO.Path]::GetFileName($outRoot))"
"retained-progress-file=$([IO.Path]::GetFileName($progressPath))"
exit $reviewExitCode
