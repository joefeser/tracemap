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
Assert-True ($workspaceIndex -gt $evidenceIndex) "workspace readback must run after complete scan artifacts are verified"
Assert-True ($content.Contains('git -C $TraceMapRoot rev-parse HEAD', [StringComparison]::Ordinal)) "workspace readback must identify the TraceMap head"
Assert-True ($content.Contains('Solution path, relative to the source root', [StringComparison]::Ordinal)) "solution selection prompt is missing"
Assert-True ($content.Contains('$reviewArguments += @("--include", $SolutionRelativePath)', [StringComparison]::Ordinal)) "selected solution must survive inventory include filtering"
Assert-True ($content.Contains('$reviewArguments += @("--solution", $SolutionRelativePath)', [StringComparison]::Ordinal)) "selected solution is not passed to local review"
Assert-True ($content.Contains('if ($ProjectRelativePath.Count -eq 0 -and [string]::IsNullOrWhiteSpace($SolutionRelativePath))', [StringComparison]::Ordinal)) "solution-only invocation must not prompt for project paths"
Assert-True ($content.Contains('Get-InScopeSolutionProjects $solutionPath $SourceRoot $selectedFolders', [StringComparison]::Ordinal)) "solution-only invocation must derive the bounded project selection"
Assert-True ($content.Contains('SOLUTION_SCOPE_HAS_NO_IN_SCOPE_PROJECTS', [StringComparison]::Ordinal)) "solution scope must fail closed when it has no selected projects"
Assert-True ($content.Contains('SOLUTION_SCOPE_UNAVAILABLE', [StringComparison]::Ordinal)) "solution path availability is not validated"
Assert-True ($content.Contains('SOLUTION_SCOPE_INVALID', [StringComparison]::Ordinal)) "solution extension is not validated"

$selectionFunction = @($scriptAst.FindAll({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Get-InScopeSolutionProjects'
}, $true))
Assert-True ($selectionFunction.Count -eq 1) "solution project selector is missing"
Invoke-Expression $selectionFunction[0].Extent.Text

$selectionRoot = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-solution-selection-' + [Guid]::NewGuid().ToString('N'))
try {
    foreach ($folder in @('source/web', 'source/backend', 'source/controls', 'unrelated')) {
        New-Item -ItemType Directory -Path (Join-Path $selectionRoot $folder) -Force | Out-Null
    }
    $solutionPath = Join-Path $selectionRoot 'private.sln'
    [IO.File]::WriteAllText($solutionPath, '', [Text.UTF8Encoding]::new($false))

    function dotnet {
        $global:LASTEXITCODE = 0
        @(
            'source/web/web.csproj',
            'source/backend/backend.csproj',
            'source/controls/controls.csproj',
            'unrelated/unrelated.csproj'
        )
    }

    $selection = @(Get-InScopeSolutionProjects `
        $solutionPath `
        $selectionRoot `
        @('source/web', 'source/backend', 'source/controls'))
    Assert-True ($selection.Count -eq 3) "solution selector did not retain exactly the in-scope projects"
    Assert-True ($selection -contains 'source/web/web.csproj') "Web Forms solution project was not selected"
    Assert-True ($selection -contains 'source/backend/backend.csproj') "backend solution project was not selected"
    Assert-True ($selection -contains 'source/controls/controls.csproj') "controls solution project was not selected"
    Assert-True ($selection -notcontains 'unrelated/unrelated.csproj') "out-of-scope solution project was selected"
}
finally {
    Remove-Item Function:\dotnet -ErrorAction SilentlyContinue
    if (Test-Path $selectionRoot) { Remove-Item $selectionRoot -Recurse -Force }
}

foreach ($requiredArtifact in @('scan/facts.ndjson', 'scan/scan-manifest.json', 'local-review-result.json')) {
    Assert-True ($content.Contains($requiredArtifact, [StringComparison]::Ordinal)) "evidence summary guard is missing $requiredArtifact"
}
Assert-True ($content.Contains('focused-webforms-evidence-summary=skipped;reason=incomplete-review-artifacts', [StringComparison]::Ordinal)) "incomplete artifact outcome is not categorical"

Write-Output "focused-webforms-review-launcher-tests=passed"
