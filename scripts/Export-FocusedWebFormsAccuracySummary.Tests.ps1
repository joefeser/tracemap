[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path -Parent $PSScriptRoot))

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot)
$script = Join-Path $TraceMapRoot "scripts/Export-FocusedWebFormsAccuracySummary.ps1"
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($script, [ref]$tokens, [ref]$parseErrors) | Out-Null
Assert-True ($parseErrors.Count -eq 0) "accuracy summary script syntax is invalid"

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("tracemap-webforms-accuracy-test-" + [Guid]::NewGuid().ToString("N"))
$review = Join-Path $testRoot "review"
$scan = Join-Path $review "scan"
$output = Join-Path $testRoot "summary"

try {
    New-Item -ItemType Directory -Path $scan -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $scan "scan-manifest.json"), '{"analysisLevel":"Level1SemanticAnalysisReduced","buildStatus":"FailedOrPartial"}', [Text.UTF8Encoding]::new($false))
    $facts = @(
        @{ factType = 'FileInventoried'; ruleId = 'file.inventory.v1'; evidenceTier = 'Tier2Structural'; evidence = @{ filePath = 'private-web/default.aspx' }; properties = @{} },
        @{ factType = 'WebFormsUserControlRegistered'; ruleId = 'legacy.webforms.inventory.v1'; evidenceTier = 'Tier2Structural'; evidence = @{ filePath = 'private-web/web.config' }; properties = @{ registrationShape = 'assembly-namespace' } },
        @{ factType = 'CallEdge'; ruleId = 'csharp.semantic.call.v1'; evidenceTier = 'Tier1Semantic'; evidence = @{ filePath = 'private-web/private-backend/service.cs' }; properties = @{} },
        @{ factType = 'AnalyzerCapabilityDiagnostic'; ruleId = 'analyzer.capability.semantic.v1'; evidenceTier = 'Tier4Unknown'; evidence = @{ filePath = 'private-web/private-backend/service.csproj' }; properties = @{ capabilityCode = 'CSharpSemanticCompilation'; capabilityState = 'Reduced'; coverageEffect = 'reduces-semantic-coverage' } },
        @{ factType = 'BuildEnvironmentDiagnostic'; ruleId = 'build.environment.workspace-diagnostic.v1'; evidenceTier = 'Tier4Unknown'; evidence = @{ filePath = 'private-web/private-backend/service.csproj' }; properties = @{ diagnosticCode = 'MissingReferenceAssemblies'; diagnosticKind = 'workspace'; coverageEffect = 'reduces-semantic-coverage' } },
        @{ factType = 'AnalysisGap'; ruleId = 'legacy.webforms.event-binding.v1'; evidenceTier = 'Tier4Unknown'; evidence = @{ filePath = 'private-controls/widget.ascx' }; properties = @{ gapKind = 'UnsupportedWebFormsEventAttribute' } }
    )
    [IO.File]::WriteAllLines((Join-Path $scan "facts.ndjson"), @($facts | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 10 }), [Text.UTF8Encoding]::new($false))

    $result = @(& $script -ReviewOutputPath $review -WebFormsFolder 'private-web' -BackendFolder 'private-web/private-backend' -ControlsFolder 'private-controls' -OutputDirectory $output)
    Assert-True ($result[0] -eq 'focused-webforms-accuracy-summary-file=created') "summary creation was not reported"
    $file = @(Get-ChildItem $output -File -Filter 'focused-webforms-accuracy-*.txt')
    Assert-True ($file.Count -eq 1) "accuracy summary was not created"
    $content = [IO.File]::ReadAllText($file[0].FullName)
    Assert-True ($content.Contains('analysisLevel=Level1SemanticAnalysisReduced')) "analysis level missing"
    Assert-True ($content.Contains('scope-webforms=facts:2|tier1:0|tier2:2|tier3:0|tier4:0|gaps:0')) "webforms scope count incorrect"
    Assert-True ($content.Contains('scope-backend=facts:3|tier1:1|tier2:0|tier3:0|tier4:2|gaps:0')) "backend scope count incorrect"
    Assert-True ($content.Contains('scope-controls=facts:1|tier1:0|tier2:0|tier3:0|tier4:1|gaps:1')) "controls scope count incorrect"
    Assert-True ($content.Contains('priority01=restore-compatible-reference-assemblies')) "priority signal incorrect"
    Assert-True ($content.Contains('registrationShape=webforms|assembly-namespace|count=1')) "registration shape missing"
    Assert-True (-not $content.Contains('private-web')) "private Web Forms folder leaked"
    Assert-True (-not $content.Contains('private-backend')) "private backend folder leaked"
    Assert-True (-not $content.Contains('private-controls')) "private controls folder leaked"
    Assert-True (-not $content.Contains($testRoot)) "local root leaked"
    'focused-webforms-accuracy-summary-tests=passed'
}
finally {
    if (Test-Path $testRoot) { Remove-Item $testRoot -Recurse -Force }
}
