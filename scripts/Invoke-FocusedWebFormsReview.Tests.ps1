[CmdletBinding()]
param(
    [string]$TraceMapRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )
    if (-not $Condition) { throw $Message }
}

$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot)
$scriptPath = Join-Path $TraceMapRoot "scripts/Invoke-FocusedWebFormsReview.ps1"
$tokens = $null
$parseErrors = $null
$scriptAst = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
Assert-True ($parseErrors.Count -eq 0) "focused review script syntax is invalid"

$content = [IO.File]::ReadAllText($scriptPath)
$progressIndex = $content.IndexOf('Export-FocusedWebFormsProgressSummary.ps1', [StringComparison]::Ordinal)
$performanceIndex = $content.IndexOf('Export-FocusedWebFormsPerformanceSummary.ps1', [StringComparison]::Ordinal)
$evidenceIndex = $content.IndexOf('Export-FocusedWebFormsEvidenceSummary.ps1', [StringComparison]::Ordinal)
$workspaceIndex = $content.IndexOf('Export-FocusedWebFormsWorkspaceSummary.ps1', [StringComparison]::Ordinal)
Assert-True ($progressIndex -ge 0 -and $progressIndex -lt $evidenceIndex) "progress diagnostics must precede evidence summaries"
Assert-True ($performanceIndex -ge 0 -and $performanceIndex -lt $evidenceIndex) "performance diagnostics must precede evidence summaries"
Assert-True ($workspaceIndex -ge 0 -and $workspaceIndex -lt $evidenceIndex) "workspace readback must precede result-gated evidence summaries"
Assert-True ($content.Contains('$scanArtifactsAvailable', [StringComparison]::Ordinal)) "workspace readback must have a scan-artifact-only guard"
Assert-True ($content.Contains('git -C $TraceMapRoot rev-parse HEAD', [StringComparison]::Ordinal)) "workspace readback must identify the TraceMap head"
Assert-True ($content.Contains('Solution path, relative to the source root', [StringComparison]::Ordinal)) "solution selection prompt is missing"
Assert-True ($content.Contains('if (-not $Projectless -and -not $DiscoverProjects -and [string]::IsNullOrWhiteSpace($SolutionRelativePath) -and $ProjectRelativePath.Count -eq 0)', [StringComparison]::Ordinal)) "project-only, discovery, and projectless invocation must not prompt for a solution"
Assert-True ($content.Contains('[switch]$Projectless', [StringComparison]::Ordinal)) "explicit projectless mode is missing"
Assert-True ($content.Contains('[switch]$DiscoverProjects', [StringComparison]::Ordinal)) "folder-scoped project discovery mode is missing"
Assert-True ($content.Contains('PROJECTLESS_SCOPE_CONFLICT', [StringComparison]::Ordinal)) "projectless mode must reject solution or project inputs"
Assert-True ($content.Contains("if (`$folder -eq '.') { '**' }", [StringComparison]::Ordinal)) "repository-root folder scope is not normalized to a valid include glob"
Assert-True (-not $content.Contains('THREE_FOLDER_SCOPE_INVALID', [StringComparison]::Ordinal)) "single-folder scope must not be rejected"
Assert-True ($content.Contains('$reviewArguments += @("--include", $SolutionRelativePath)', [StringComparison]::Ordinal)) "selected solution must survive inventory include filtering"
Assert-True ($content.Contains('$reviewArguments += @("--solution", $SolutionRelativePath)', [StringComparison]::Ordinal)) "selected solution is not passed to local review"
Assert-True ($content.Contains('if (-not $Projectless -and -not $DiscoverProjects -and $ProjectRelativePath.Count -eq 0 -and [string]::IsNullOrWhiteSpace($SolutionRelativePath))', [StringComparison]::Ordinal)) "solution-only, discovery, and projectless invocation must not prompt for project paths"
Assert-True ($content.Contains('Get-InScopeSolutionProjects $solutionPath $SourceRoot $selectedFolders', [StringComparison]::Ordinal)) "solution-only invocation must derive the bounded project selection"
Assert-True ($content.Contains('Assert-ProjectsBelongToSolution', [StringComparison]::Ordinal)) "explicit project selection must be checked against the solution"
Assert-True ($content.Contains('PROJECT_NOT_IN_SELECTED_SOLUTION', [StringComparison]::Ordinal)) "solution membership failure must be categorical"
Assert-True ($content.Contains('SOLUTION_SCOPE_HAS_NO_IN_SCOPE_PROJECTS', [StringComparison]::Ordinal)) "solution scope must fail closed when it has no selected projects"
Assert-True ($content.Contains('SOLUTION_SCOPE_UNAVAILABLE', [StringComparison]::Ordinal)) "solution path availability is not validated"
Assert-True ($content.Contains('candidates=$candidateText', [StringComparison]::Ordinal)) "missing solution/project paths must include bounded candidate guidance"
Assert-True ($content.Contains("-SearchFolders `$selectedFolders", [StringComparison]::Ordinal)) "project candidates must remain inside the three selected folders"
Assert-True ($content.Contains('SOLUTION_SCOPE_INVALID', [StringComparison]::Ordinal)) "solution extension is not validated"
Assert-True ($content.Contains("[IO.Path]::GetExtension(`$solutionPath) -ne '.sln'", [StringComparison]::Ordinal)) "unsupported solution formats must fail closed"
Assert-True (-not $content.Contains("'.slnx'", [StringComparison]::Ordinal)) "slnx must not be accepted before scanner inventory support exists"

$selectionFunction = @($scriptAst.FindAll({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Get-InScopeSolutionProjects'
}, $true))
Assert-True ($selectionFunction.Count -eq 1) "solution project selector is missing"
Invoke-Expression $selectionFunction[0].Extent.Text

$membershipFunction = @($scriptAst.FindAll({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Assert-ProjectsBelongToSolution'
}, $true))
Assert-True ($membershipFunction.Count -eq 1) "solution membership validator is missing"
Invoke-Expression $membershipFunction[0].Extent.Text

$candidateFunction = @($scriptAst.FindAll({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Get-BoundedRelativeCandidates'
}, $true))
Assert-True ($candidateFunction.Count -eq 1) "bounded candidate selector is missing"
Invoke-Expression $candidateFunction[0].Extent.Text

$folderProjectFunction = @($scriptAst.FindAll({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Get-InScopeFolderProjects'
}, $true))
Assert-True ($folderProjectFunction.Count -eq 1) "folder-scoped project discovery is missing"
Invoke-Expression $folderProjectFunction[0].Extent.Text

$selectionRoot = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-solution-selection-' + [Guid]::NewGuid().ToString('N'))
try {
    foreach ($folder in @('source/web', 'source/backend', 'source/controls', 'unrelated')) {
        New-Item -ItemType Directory -Path (Join-Path $selectionRoot $folder) -Force | Out-Null
    }
    $solutionPath = Join-Path $selectionRoot 'private.sln'
    [IO.File]::WriteAllText($solutionPath, '', [Text.UTF8Encoding]::new($false))
    foreach ($project in @('source/web/web.vbproj','source/backend/backend.csproj','source/controls/controls.vbproj','unrelated/unrelated.vbproj')) {
        [IO.File]::WriteAllText((Join-Path $selectionRoot $project), '', [Text.UTF8Encoding]::new($false))
    }

    function dotnet {
        $global:LASTEXITCODE = 0
        @(
            'source/web/web.vbproj',
            'source/backend/backend.csproj',
            'source/controls/controls.vbproj',
            'unrelated/unrelated.vbproj',
            'unrelated/readme.txt'
        )
    }

    $selection = @(Get-InScopeSolutionProjects `
        $solutionPath `
        $selectionRoot `
        @('source/web', 'source/backend', 'source/controls'))
    Assert-True ($selection.Count -eq 3) "solution selector did not retain exactly the in-scope projects"
    Assert-True ($selection -contains 'source/web/web.vbproj') "VB.NET Web Forms solution project was not selected"
    Assert-True ($selection -contains 'source/backend/backend.csproj') "backend solution project was not selected"
    Assert-True ($selection -contains 'source/controls/controls.vbproj') "VB.NET controls solution project was not selected"
    Assert-True ($selection -notcontains 'unrelated/unrelated.vbproj') "out-of-scope solution project was selected"

    $discovered = @(Get-InScopeFolderProjects $selectionRoot @('source/web', 'source/backend', 'source/controls'))
    Assert-True ($discovered.Count -eq 3) "folder discovery did not retain exactly the in-scope projects"
    Assert-True ($discovered -notcontains 'unrelated/unrelated.vbproj') "folder discovery escaped the three selected roots"

    Assert-ProjectsBelongToSolution `
        -ExplicitProjects @('source\backend\backend.csproj') `
        -SolutionProjects $selection `
        -SourceRoot $selectionRoot
    $membershipFailure = $null
    try {
        Assert-ProjectsBelongToSolution `
            -ExplicitProjects @('source/backend/not-in-solution.csproj') `
            -SolutionProjects $selection `
            -SourceRoot $selectionRoot
    }
    catch { $membershipFailure = $_.Exception.Message }
    Assert-True ($membershipFailure -eq 'PROJECT_NOT_IN_SELECTED_SOLUTION') "explicit nonmember project did not fail closed"
}
finally {
    Remove-Item Function:\dotnet -ErrorAction SilentlyContinue
    if (Test-Path $selectionRoot) { Remove-Item $selectionRoot -Recurse -Force }
}

foreach ($requiredArtifact in @('scan/facts.ndjson', 'scan/scan-manifest.json', 'local-review-result.json')) {
    Assert-True ($content.Contains($requiredArtifact, [StringComparison]::Ordinal)) "evidence summary guard is missing $requiredArtifact"
}
Assert-True ($content.Contains('focused-webforms-evidence-summary=skipped;reason=incomplete-review-artifacts', [StringComparison]::Ordinal)) "incomplete artifact outcome is not categorical"
Assert-True ($content.Contains('focused-webforms-workspace-summary=skipped;reason=incomplete-scan-artifacts', [StringComparison]::Ordinal)) "incomplete scan artifact outcome is not categorical"

Write-Output "focused-webforms-review-launcher-tests=passed"
