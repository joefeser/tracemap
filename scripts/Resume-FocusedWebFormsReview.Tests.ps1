[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path -Parent $PSScriptRoot))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot)
$script = Join-Path $TraceMapRoot 'scripts/Resume-FocusedWebFormsReview.ps1'
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($script, [ref]$tokens, [ref]$parseErrors) | Out-Null
Assert-True ($parseErrors.Count -eq 0) 'resume script syntax is invalid'

$content = [IO.File]::ReadAllText($script)
Assert-True ($content.Contains("'focused-webforms-*'")) 'resume script does not discover retained scans'
Assert-True ($content.Contains("'scan/facts.ndjson'")) 'resume script does not require retained facts'
Assert-True ($content.Contains("'scan/scan-manifest.json'")) 'resume script does not require a retained manifest'
Assert-True ($content.Contains("'Export-FocusedWebFormsWorkspaceSummary.ps1'")) 'resume script does not export the workspace summary'
Assert-True ($content.Contains("'Export-FocusedWebFormsEvidenceSummary.ps1'")) 'resume script does not export the evidence summary'
Assert-True ($content.Contains("'Export-FocusedWebFormsAccuracySummary.ps1'")) 'resume script does not export the accuracy summary'
Assert-True ($content.IndexOf('Remove-Item', [StringComparison]::OrdinalIgnoreCase) -lt 0) 'resume script must not delete retained output'

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-resume-test-' + [Guid]::NewGuid().ToString('N'))
$review = Join-Path $testRoot 'focused-webforms-retained'
$scan = Join-Path $review 'scan'
$summary = Join-Path $testRoot 'summary'
try {
    New-Item -ItemType Directory -Path $scan -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $scan 'scan-manifest.json'), '{"analysisLevel":"Level3SyntaxAnalysisReduced","buildStatus":"FailedOrPartial"}', [Text.UTF8Encoding]::new($false))
    $fact = @{ factType = 'AnalysisGap'; ruleId = 'vb.semantic.workspace.v1'; evidenceTier = 'Tier4Unknown'; evidence = @{ filePath = 'Default.aspx.vb' }; properties = @{ diagnosticCode = 'CompilerDiagnostic'; guidanceCode = 'ReviewCompilerDiagnostic'; gapKind = 'CompilationDiagnostic'; diagnosticId = 'BC30002' } }
    [IO.File]::WriteAllText((Join-Path $scan 'facts.ndjson'), ($fact | ConvertTo-Json -Compress -Depth 10), [Text.UTF8Encoding]::new($false))

    $result = @(& $script -OutputRoot $testRoot -TraceMapRoot $TraceMapRoot -SummaryDirectory $summary)
    Assert-True ($result -contains 'focused-webforms-workspace-summary-file=created') 'resume did not create the workspace summary'
    Assert-True ($result -contains 'focused-webforms-review-summaries=partial;reason=local-review-result-unavailable') 'resume did not report the missing optional review result'
    Assert-True ($result -contains 'resumed-output-directory=focused-webforms-retained') 'resume selected the wrong retained scan'
    Assert-True (@(Get-ChildItem $summary -File -Filter 'focused-webforms-workspace-*.txt').Count -eq 1) 'resume workspace summary is missing'
    'focused-webforms-resume-tests=passed'
}
finally {
    if (Test-Path $testRoot) { Remove-Item $testRoot -Recurse -Force }
}
