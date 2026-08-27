[CmdletBinding()]
param(
    [string]$SourceRoot,
    [string]$WebFormsFolder,
    [string]$BackendFolder,
    [string]$ControlsFolder,
    [string]$SolutionRelativePath,
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
if ([string]::IsNullOrWhiteSpace($SolutionRelativePath)) {
    $SolutionRelativePath = (Read-Host "Solution path, relative to the source root (blank if unavailable)").Trim()
}
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
if (-not [string]::IsNullOrWhiteSpace($SolutionRelativePath)) {
    $solutionPath = Resolve-RelativeChild $SourceRoot $SolutionRelativePath "SOLUTION_SCOPE_UNAVAILABLE" $true
    if ([IO.Path]::GetExtension($solutionPath) -notin @('.sln', '.slnx')) { throw "SOLUTION_SCOPE_INVALID" }
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
if (-not [string]::IsNullOrWhiteSpace($SolutionRelativePath)) { $reviewArguments += @("--solution", $SolutionRelativePath) }
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

# Progress and performance receipts are the primary diagnostics for failed or
# timed-out runs. Export them before attempting summaries that require a
# complete scan artifact set.
& "$TraceMapRoot\scripts\Export-FocusedWebFormsProgressSummary.ps1" -ProgressPath $progressPath
& "$TraceMapRoot\scripts\Export-FocusedWebFormsPerformanceSummary.ps1" -ProgressPath $progressPath

$factsPath = Join-Path $outRoot "scan/facts.ndjson"
$manifestPath = Join-Path $outRoot "scan/scan-manifest.json"
$resultPath = Join-Path $outRoot "local-review-result.json"
$completeReviewArtifacts =
    (Test-Path -LiteralPath $factsPath -PathType Leaf) -and
    (Test-Path -LiteralPath $manifestPath -PathType Leaf) -and
    (Test-Path -LiteralPath $resultPath -PathType Leaf)

if ($completeReviewArtifacts) {
    $traceMapHead = (git -C $TraceMapRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $traceMapHead -notmatch '^[0-9a-fA-F]{40}$') {
        throw "TRACEMAP_HEAD_UNAVAILABLE"
    }
    & "$TraceMapRoot\scripts\Export-FocusedWebFormsEvidenceSummary.ps1" `
        -ReviewOutputPath $outRoot `
        -OutputDirectory $summaryParent
    & "$TraceMapRoot\scripts\Export-FocusedWebFormsAccuracySummary.ps1" `
        -ReviewOutputPath $outRoot `
        -WebFormsFolder $WebFormsFolder `
        -BackendFolder $BackendFolder `
        -ControlsFolder $ControlsFolder `
        -OutputDirectory $summaryParent
    & "$TraceMapRoot\scripts\Export-FocusedWebFormsWorkspaceSummary.ps1" `
        -ReviewOutputPath $outRoot `
        -WebFormsFolder $WebFormsFolder `
        -BackendFolder $BackendFolder `
        -ControlsFolder $ControlsFolder `
        -TraceMapHead $traceMapHead `
        -OutputDirectory $summaryParent
}
else {
    "focused-webforms-evidence-summary=skipped;reason=incomplete-review-artifacts"
}

"retained-output-directory=$([IO.Path]::GetFileName($outRoot))"
"retained-progress-file=$([IO.Path]::GetFileName($progressPath))"
exit $reviewExitCode
