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
[Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors) | Out-Null
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

foreach ($requiredArtifact in @('scan/facts.ndjson', 'scan/scan-manifest.json', 'local-review-result.json')) {
    Assert-True ($content.Contains($requiredArtifact, [StringComparison]::Ordinal)) "evidence summary guard is missing $requiredArtifact"
}
Assert-True ($content.Contains('focused-webforms-evidence-summary=skipped;reason=incomplete-review-artifacts', [StringComparison]::Ordinal)) "incomplete artifact outcome is not categorical"

Write-Output "focused-webforms-review-launcher-tests=passed"
