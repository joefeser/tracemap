[CmdletBinding()]
param(
    [string]$SourceRoot,
    [string]$WebFormsFolder,
    [string]$BackendFolder,
    [string]$ControlsFolder,
    [string]$SolutionRelativePath,
    [string[]]$ProjectRelativePath = @(),
    [switch]$DiscoverProjects,
    [switch]$Projectless,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent),
    [int]$TimeoutSeconds = 7200,
    [string]$OutputDirectory = '',
    [string]$ProgressPath = '',
    [string]$SummaryDirectory = '',
    [switch]$SkipBuild,
    [switch]$NoExit
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

function Get-BoundedRelativeCandidates {
    param(
        [string]$Root,
        [string[]]$Extensions,
        [string[]]$SearchFolders = @('.'),
        [int]$Limit = 10
    )

    $rootPrefix = $Root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $results = [Collections.Generic.List[string]]::new()
    foreach ($folder in @($SearchFolders | Select-Object -Unique)) {
        $searchRoot = if ($folder -eq '.') { $Root } else { Join-Path $Root $folder }
        if (!(Test-Path -LiteralPath $searchRoot -PathType Container)) { continue }
        foreach ($extension in $Extensions) {
            foreach ($candidate in Get-ChildItem -LiteralPath $searchRoot -File -Recurse -Filter "*$extension" -ErrorAction SilentlyContinue) {
                if ($candidate.FullName -match '[\\/](bin|obj)[\\/]') { continue }
                if (!$candidate.FullName.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { continue }
                $results.Add($candidate.FullName.Substring($rootPrefix.Length).Replace('\', '/'))
                if ($results.Count -ge $Limit) { break }
            }
            if ($results.Count -ge $Limit) { break }
        }
        if ($results.Count -ge $Limit) { break }
    }
    return @($results | Sort-Object -Unique)
}

function Get-InScopeFolderProjects {
    param([string]$SourceRoot, [string[]]$SelectedFolders)

    return @(Get-BoundedRelativeCandidates `
        -Root $SourceRoot `
        -Extensions @('.csproj', '.vbproj') `
        -SearchFolders $SelectedFolders `
        -Limit 1000)
}

function Get-InScopeSolutionProjects {
    param(
        [string]$SolutionPath,
        [string]$SourceRoot,
        [string[]]$SelectedFolders
    )

    $solutionDirectory = Split-Path -Parent $SolutionPath
    $projectRows = @(dotnet sln $SolutionPath list)
    if ($LASTEXITCODE -ne 0) { throw "SOLUTION_PROJECT_LIST_FAILED" }

    $scopeRoots = @($SelectedFolders | ForEach-Object {
        [IO.Path]::GetFullPath((Join-Path $SourceRoot $_)).TrimEnd('\', '/')
    })
    $sourcePrefix = $SourceRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $projects = [Collections.Generic.List[string]]::new()
    foreach ($row in $projectRows) {
        $listedPath = ([string]$row).Trim()
        $isSupportedProject =
            $listedPath.EndsWith('.csproj', [StringComparison]::OrdinalIgnoreCase) -or
            $listedPath.EndsWith('.vbproj', [StringComparison]::OrdinalIgnoreCase)
        if (-not $isSupportedProject) { continue }
        $candidate = if ([IO.Path]::IsPathRooted($listedPath)) {
            [IO.Path]::GetFullPath($listedPath)
        } else {
            [IO.Path]::GetFullPath((Join-Path $solutionDirectory $listedPath))
        }
        if (-not $candidate.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase)) { continue }
        foreach ($scopeRoot in $scopeRoots) {
            if ($candidate.StartsWith($scopeRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                $projects.Add($candidate.Substring($sourcePrefix.Length).Replace('\', '/'))
                break
            }
        }
    }
    $selected = @($projects | Sort-Object -Unique)
    if ($selected.Count -eq 0) { throw "SOLUTION_SCOPE_HAS_NO_IN_SCOPE_PROJECTS" }
    return $selected
}

function Assert-ProjectsBelongToSolution {
    param(
        [string[]]$ExplicitProjects,
        [string[]]$SolutionProjects,
        [string]$SourceRoot
    )

    $sourcePrefix = $SourceRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $solutionProjectSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($project in $SolutionProjects) { [void]$solutionProjectSet.Add($project.Replace('\', '/')) }
    foreach ($project in $ExplicitProjects) {
        $platformRelativePath = $project.Replace('\', [IO.Path]::DirectorySeparatorChar).Replace('/', [IO.Path]::DirectorySeparatorChar)
        $candidate = [IO.Path]::GetFullPath((Join-Path $SourceRoot $platformRelativePath))
        if (-not $candidate.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "PROJECT_NOT_IN_SELECTED_SOLUTION"
        }
        $normalizedProject = $candidate.Substring($sourcePrefix.Length).Replace('\', '/')
        if (-not $solutionProjectSet.Contains($normalizedProject)) {
            throw "PROJECT_NOT_IN_SELECTED_SOLUTION"
        }
    }
}

$SourceRoot = Read-RequiredValue $SourceRoot "Private source repository root"
$WebFormsFolder = Read-RequiredValue $WebFormsFolder "Web Forms folder, relative to the source root"
$BackendFolder = Read-RequiredValue $BackendFolder "Backend folder, relative to the source root"
$ControlsFolder = Read-RequiredValue $ControlsFolder "Shared controls folder, relative to the source root"
if ($Projectless -and
    (-not [string]::IsNullOrWhiteSpace($SolutionRelativePath) -or $ProjectRelativePath.Count -ne 0 -or $DiscoverProjects)) {
    throw "PROJECTLESS_SCOPE_CONFLICT"
}
if ($DiscoverProjects -and
    (-not [string]::IsNullOrWhiteSpace($SolutionRelativePath) -or $ProjectRelativePath.Count -ne 0)) {
    throw "PROJECT_DISCOVERY_SCOPE_CONFLICT"
}
if (-not $Projectless -and -not $DiscoverProjects -and [string]::IsNullOrWhiteSpace($SolutionRelativePath) -and $ProjectRelativePath.Count -eq 0) {
    $SolutionRelativePath = (Read-Host "Solution path, relative to the source root (blank if unavailable)").Trim()
}
if (-not $Projectless -and -not $DiscoverProjects -and $ProjectRelativePath.Count -eq 0 -and [string]::IsNullOrWhiteSpace($SolutionRelativePath)) {
    $projectInput = Read-Host "Comma-separated in-scope project paths, relative to the source root (blank for a projectless scan)"
    if (-not [string]::IsNullOrWhiteSpace($projectInput)) {
        $ProjectRelativePath = @($projectInput.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    }
}

$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot).TrimEnd('\', '/')
$SourceRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $SourceRoot)).TrimEnd('\', '/')
$traceMapStatus = @(git -C $TraceMapRoot status --porcelain --untracked-files=all)
$traceMapStatusExit = $LASTEXITCODE
if ($traceMapStatusExit -ne 0) { throw "TRACEMAP_STATUS_UNAVAILABLE" }
if ($traceMapStatus.Count -ne 0) { throw "TRACEMAP_WORKTREE_DIRTY" }
$sourceStatus = @(git -C $SourceRoot status --porcelain --untracked-files=all)
$sourceStatusExit = $LASTEXITCODE
if ($sourceStatusExit -ne 0) { throw "SOURCE_STATUS_UNAVAILABLE" }
if ($sourceStatus.Count -ne 0) { throw "SOURCE_WORKTREE_DIRTY" }

$gitPrefix = @(git -C $SourceRoot rev-parse --show-prefix)
if ($LASTEXITCODE -ne 0 -or ($gitPrefix -join '').Length -ne 0) {
    throw "SOURCE_ROOT_NOT_GIT_ROOT"
}
$gitRoot = (git -C $SourceRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitRoot)) {
    throw "SOURCE_ROOT_NOT_GIT_ROOT"
}
$SourceRoot = [IO.Path]::GetFullPath($gitRoot).TrimEnd('\', '/')

$selectedFolders = @(@($WebFormsFolder, $BackendFolder, $ControlsFolder) | Select-Object -Unique)
foreach ($folder in $selectedFolders) {
    if ($folder -eq '.') { continue }
    try {
        [void](Resolve-RelativeChild $SourceRoot $folder "FOLDER_SCOPE_UNAVAILABLE" $false)
    }
    catch {
        if ($_.Exception.Message -ne 'FOLDER_SCOPE_UNAVAILABLE') { throw }
        throw "FOLDER_SCOPE_UNAVAILABLE;requested=$folder;sourceRoot=$SourceRoot"
    }
}
if (-not [string]::IsNullOrWhiteSpace($SolutionRelativePath)) {
    try {
        $solutionPath = Resolve-RelativeChild $SourceRoot $SolutionRelativePath "SOLUTION_SCOPE_UNAVAILABLE" $true
    }
    catch {
        if ($_.Exception.Message -ne 'SOLUTION_SCOPE_UNAVAILABLE') { throw }
        $candidates = @(Get-BoundedRelativeCandidates -Root $SourceRoot -Extensions @('.sln') -Limit 10)
        $candidateText = if ($candidates.Count -gt 0) { $candidates -join ',' } else { 'none' }
        throw "SOLUTION_SCOPE_UNAVAILABLE;requested=$SolutionRelativePath;candidates=$candidateText"
    }
    if ([IO.Path]::GetExtension($solutionPath) -ne '.sln') { throw "SOLUTION_SCOPE_INVALID" }
    $solutionProjects = @(Get-InScopeSolutionProjects $solutionPath $SourceRoot $selectedFolders)
    if ($ProjectRelativePath.Count -eq 0) {
        $ProjectRelativePath = $solutionProjects
    }
    else {
        Assert-ProjectsBelongToSolution `
            -ExplicitProjects $ProjectRelativePath `
            -SolutionProjects $solutionProjects `
            -SourceRoot $SourceRoot
    }
}
if ($DiscoverProjects) {
    $ProjectRelativePath = @(Get-InScopeFolderProjects -SourceRoot $SourceRoot -SelectedFolders $selectedFolders)
    if ($ProjectRelativePath.Count -eq 0) {
        $searchedFolderText = $selectedFolders -join ','
        throw "PROJECT_DISCOVERY_EMPTY;searchedFolders=$searchedFolderText;use-projectless=true"
    }
}
foreach ($project in $ProjectRelativePath) {
    try {
        $projectPath = Resolve-RelativeChild $SourceRoot $project "PROJECT_SCOPE_UNAVAILABLE" $true
    }
    catch {
        if ($_.Exception.Message -ne 'PROJECT_SCOPE_UNAVAILABLE') { throw }
        $candidates = @(Get-BoundedRelativeCandidates -Root $SourceRoot -Extensions @('.csproj', '.vbproj') -SearchFolders $selectedFolders -Limit 10)
        $candidateText = if ($candidates.Count -gt 0) { $candidates -join ',' } else { 'none' }
        throw "PROJECT_SCOPE_UNAVAILABLE;requested=$project;candidates=$candidateText"
    }
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
if (!$SkipBuild) {
    dotnet build (Join-Path $TraceMapRoot 'src/dotnet/TraceMap.sln')
    if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_BUILD_FAILED" }
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputParent = "C:\work\tracemap-output"
$progressParent = "C:\work\tracemap-progress"
$summaryParent = if ($SummaryDirectory) { [IO.Path]::GetFullPath($SummaryDirectory) } else { "C:\work\tracemap-summary" }
$outRoot = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $outputParent "focused-webforms-$stamp" }
$progressPath = if ($ProgressPath) { [IO.Path]::GetFullPath($ProgressPath) } else { Join-Path $progressParent "focused-webforms-$stamp.json" }
$requiredDirectories = @((Split-Path -Parent $outRoot), (Split-Path -Parent $progressPath), $summaryParent) | Sort-Object -Unique
New-Item -ItemType Directory -Path $requiredDirectories -Force | Out-Null

$reviewArguments = @("run", "--repo", $SourceRoot, "--out", $outRoot)
foreach ($folder in $selectedFolders) {
    $includePattern = if ($folder -eq '.') { '**' } else { $folder.TrimEnd('/', '\') + '/**' }
    $reviewArguments += @("--include", $includePattern)
}
if (-not [string]::IsNullOrWhiteSpace($SolutionRelativePath)) {
    # Explicit selection does not bypass inventory scope filtering. Preserve the
    # solution itself while the derived project list bounds semantic loading.
    $reviewArguments += @("--include", $SolutionRelativePath)
    $reviewArguments += @("--solution", $SolutionRelativePath)
}
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
$scanArtifactsAvailable =
    (Test-Path -LiteralPath $factsPath -PathType Leaf) -and
    (Test-Path -LiteralPath $manifestPath -PathType Leaf)
$completeReviewArtifacts =
    $scanArtifactsAvailable -and
    (Test-Path -LiteralPath $resultPath -PathType Leaf)

if ($scanArtifactsAvailable) {
    $traceMapHead = (git -C $TraceMapRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $traceMapHead -notmatch '^[0-9a-fA-F]{40}$') {
        throw "TRACEMAP_HEAD_UNAVAILABLE"
    }
    & "$TraceMapRoot\scripts\Export-FocusedWebFormsWorkspaceSummary.ps1" `
        -ReviewOutputPath $outRoot `
        -WebFormsFolder $WebFormsFolder `
        -BackendFolder $BackendFolder `
        -ControlsFolder $ControlsFolder `
        -TraceMapHead $traceMapHead `
        -OutputDirectory $summaryParent
}
else {
    "focused-webforms-workspace-summary=skipped;reason=incomplete-scan-artifacts"
}

if ($completeReviewArtifacts) {
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
else {
    "focused-webforms-evidence-summary=skipped;reason=incomplete-review-artifacts"
}

"retained-output-directory=$([IO.Path]::GetFileName($outRoot))"
"retained-progress-file=$([IO.Path]::GetFileName($progressPath))"
if ($NoExit) {
    if ($reviewExitCode -ne 0) { throw "FOCUSED_REVIEW_PROCESS_FAILED;exitCode=$reviewExitCode" }
    return
}
exit $reviewExitCode
