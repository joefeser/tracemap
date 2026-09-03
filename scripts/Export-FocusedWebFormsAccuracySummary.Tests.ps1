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

    $cases = @(
        @{ Codes = @('LegacyTargetFramework', 'NonSdkStyleProject', 'WebApplicationProjectTargets'); Rule = 'build.environment.project-format.v1'; Effect = 'caps-to-structural'; Expected = 'inspect-highest-count-accuracy-gap' },
        @{ Codes = @('LegacyTargetFramework', 'NonSdkStyleProject', 'WebApplicationProjectTargets'); Rule = 'build.environment.workspace-diagnostic.v1'; Effect = 'caps-to-structural'; Expected = 'inspect-highest-count-accuracy-gap' },
        @{ Codes = @('SdkResolutionFailed'); Rule = 'build.environment.workspace-diagnostic.v1'; Effect = 'informational'; Expected = 'inspect-highest-count-accuracy-gap' },
        @{ Codes = @('MissingReferenceAssemblies'); Rule = 'unrelated.rule.v1'; Effect = 'reduces-semantic-coverage'; Expected = 'inspect-highest-count-accuracy-gap' },
        @{ Codes = @('UncategorizedWorkspaceFailure'); Expected = 'classify-bounded-workspace-failure' },
        @{ Codes = @('MSBuildTaskHostIncompatible'); Expected = 'use-compatible-msbuild-task-host' },
        @{ Codes = @('MissingReferenceAssemblies', 'MSBuildTaskHostIncompatible'); Expected = 'restore-compatible-reference-assemblies' },
        @{ Codes = @('SdkResolutionFailed'); Expected = 'recreate-compatible-legacy-msbuild-workspace' },
        @{ Codes = @('MSBuildRegistrationFailed'); Expected = 'recreate-compatible-legacy-msbuild-workspace' },
        @{ Codes = @('LegacyWorkspacePrerequisitesUnresolved'); Expected = 'recreate-compatible-legacy-msbuild-workspace' },
        @{ Codes = @('WebApplicationTargetsUnavailable'); Expected = 'recreate-compatible-legacy-msbuild-workspace' },
        @{ Codes = @('ImportedTargetsUnavailable'); Expected = 'recreate-compatible-legacy-msbuild-workspace' },
        @{ Codes = @('LegacyProjectEvaluationFailed'); Expected = 'recreate-compatible-legacy-msbuild-workspace' },
        @{ Codes = @(); Expected = 'inspect-highest-count-accuracy-gap' }
    )
    foreach ($case in $cases) {
        $caseFacts = @($case.Codes | ForEach-Object {
            @{ factType = 'BuildEnvironmentDiagnostic';
                ruleId = $(if ($case.ContainsKey('Rule')) { $case.Rule } else { 'build.environment.workspace-diagnostic.v1' });
                evidenceTier = 'Tier4Unknown'; evidence = @{ filePath = 'private-web/site.csproj' };
                properties = @{ diagnosticCode = $_; diagnosticKind = 'workspace';
                    coverageEffect = $(if ($case.ContainsKey('Effect')) { $case.Effect } else { 'reduces-semantic-coverage' }) } }
        })
        [IO.File]::WriteAllLines((Join-Path $scan 'facts.ndjson'), [string[]]@($caseFacts | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 10 }), [Text.UTF8Encoding]::new($false))
        $caseOutput = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
        $caseResult = @(& $script -ReviewOutputPath $review -WebFormsFolder 'private-web' -BackendFolder 'private-web/private-backend' -ControlsFolder 'private-controls' -OutputDirectory $caseOutput)
        Assert-True ($caseResult[0] -eq 'focused-webforms-accuracy-summary-file=created') 'priority fixture did not run'
        $caseFile = @(Get-ChildItem $caseOutput -File -Filter 'focused-webforms-accuracy-*.txt')
        Assert-True ($caseFile.Count -eq 1) 'priority fixture output missing'
        $caseContent = [IO.File]::ReadAllText($caseFile[0].FullName)
        Assert-True ($caseContent.Contains("priority01=$($case.Expected)")) "incorrect priority for $($case.Codes -join ',')"
        Assert-True (-not $caseContent.Contains('private-web')) 'priority fixture leaked private path'
    }
    'focused-webforms-accuracy-summary-tests=passed'
}
finally {
    if (Test-Path $testRoot) { Remove-Item $testRoot -Recurse -Force }
}
