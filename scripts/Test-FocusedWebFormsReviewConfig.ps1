[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'webforms-review/FocusedWebFormsPipelineConfig.ps1')

function Resolve-ScopedPath {
    param([string]$Root, [string]$RelativePath, [bool]$Leaf, [string]$Property)

    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match '(^|/|\\)\.\.($|/|\\)') {
        throw "WEBFORMS_PREFLIGHT_PATH_OUTSIDE_SOURCE;property=$Property;value=$RelativePath"
    }
    $candidate = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    $prefix = $Root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (!$candidate.Equals($Root, [StringComparison]::OrdinalIgnoreCase) -and
        !$candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "WEBFORMS_PREFLIGHT_PATH_OUTSIDE_SOURCE;property=$Property;value=$RelativePath"
    }
    $pathType = if ($Leaf) { 'Leaf' } else { 'Container' }
    if (!(Test-Path -LiteralPath $candidate -PathType $pathType)) {
        throw "WEBFORMS_PREFLIGHT_PATH_UNAVAILABLE;property=$Property;value=$RelativePath"
    }
    return $candidate
}

try {
    if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_PIPELINE_POWERSHELL_7_REQUIRED' }
    $root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
    if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'WEBFORMS_PIPELINE_REVIEW_ROOT_UNAVAILABLE' }
    $configPath = Resolve-FocusedWebFormsPipelineConfigPath -ReviewRoot $root
    $config = Read-FocusedWebFormsPipelineConfig -ConfigPath $configPath
    $configuredRoot = [IO.Path]::GetFullPath($config.OutputRoot).TrimEnd('\', '/')
    if (!$configuredRoot.Equals($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'WEBFORMS_PIPELINE_OUTPUT_ROOT_MUST_EQUAL_REVIEW_ROOT'
    }

    $sourceRoot = [IO.Path]::GetFullPath($config.SourceRoot).TrimEnd('\', '/')
    if (!(Test-Path -LiteralPath $sourceRoot -PathType Container)) { throw 'WEBFORMS_PIPELINE_SOURCE_ROOT_UNAVAILABLE' }
    $sourceGitRoot = ([string](git -C $sourceRoot rev-parse --show-toplevel)).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceGitRoot) -or
        ![IO.Path]::GetFullPath($sourceGitRoot).TrimEnd('\', '/').Equals($sourceRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'SOURCE_ROOT_NOT_GIT_ROOT'
    }
    $sourceStatus = @(git -C $sourceRoot status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'SOURCE_STATUS_UNAVAILABLE' }
    if ($sourceStatus.Count -ne 0) { throw 'SOURCE_WORKTREE_DIRTY' }
    foreach ($entry in @(
        @{ Name = 'webFormsFolder'; Value = $config.WebFormsFolder },
        @{ Name = 'backendFolder'; Value = $config.BackendFolder },
        @{ Name = 'controlsFolder'; Value = $config.ControlsFolder }
    )) {
        [void](Resolve-ScopedPath -Root $sourceRoot -RelativePath $entry.Value -Leaf $false -Property $entry.Name)
    }
    if ($config.ProjectMode -eq 'solution') {
        [void](Resolve-ScopedPath -Root $sourceRoot -RelativePath $config.SolutionRelativePath -Leaf $true -Property 'solutionRelativePath')
    }
    elseif ($config.ProjectMode -eq 'projects') {
        foreach ($project in $config.ProjectRelativePaths) {
            [void](Resolve-ScopedPath -Root $sourceRoot -RelativePath $project -Leaf $true -Property 'projectRelativePaths')
        }
    }
    if ($config.PageMode -eq 'selected') {
        foreach ($form in $config.Forms) {
            [void](Resolve-ScopedPath -Root $sourceRoot -RelativePath $form -Leaf $true -Property 'pageSelection.forms')
        }
    }

    $traceRoot = [IO.Path]::GetFullPath($TraceMapRoot).TrimEnd('\', '/')
    if (!(Test-Path -LiteralPath (Join-Path $traceRoot 'src/dotnet/TraceMap.sln') -PathType Leaf)) {
        throw 'WEBFORMS_PREFLIGHT_TRACEMAP_SOLUTION_UNAVAILABLE'
    }
    $traceStatus = @(git -C $traceRoot status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'TRACEMAP_STATUS_UNAVAILABLE' }
    if ($traceStatus.Count -ne 0) { throw 'TRACEMAP_WORKTREE_DIRTY' }

    Write-Host 'webformsPreflight=valid'
    Write-Host "configPath=$configPath"
    Write-Host "projectMode=$($config.ProjectMode)"
    Write-Host "pageMode=$($config.PageMode)"
    Write-Host "selectedForms=$($config.Forms.Count)"
    Write-Host 'nextAction=run-focused-webforms-pipeline'
}
catch {
    Write-FocusedWebFormsFailure -Failure $_.Exception.Message
    exit 1
}
