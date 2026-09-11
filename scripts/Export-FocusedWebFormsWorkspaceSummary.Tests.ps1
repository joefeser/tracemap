[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path -Parent $PSScriptRoot))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot)
$script = Join-Path $TraceMapRoot 'scripts/Export-FocusedWebFormsWorkspaceSummary.ps1'
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($script, [ref]$tokens, [ref]$parseErrors) | Out-Null
Assert-True ($parseErrors.Count -eq 0) 'workspace summary script syntax is invalid'
$scriptContent = [IO.File]::ReadAllText($script)
Assert-True ($scriptContent.IndexOf('ConvertFrom-Json -Depth', [StringComparison]::OrdinalIgnoreCase) -lt 0) 'workspace summary must support JSON readers without the Depth parameter'
Assert-True ($scriptContent.IndexOf('$IsWindows', [StringComparison]::Ordinal) -lt 0) 'workspace summary must not require the PowerShell 6 platform variable'

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-workspace-test-' + [Guid]::NewGuid().ToString('N'))
$review = Join-Path $testRoot 'review'
$scan = Join-Path $review 'scan'
$output = Join-Path $testRoot 'summary'

try {
    New-Item -ItemType Directory -Path $scan -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $scan 'scan-manifest.json'), '{"analysisLevel":"Level1SemanticAnalysisReduced","buildStatus":"FailedOrPartial"}', [Text.UTF8Encoding]::new($false))
    $facts = @(
        @{ factType = 'AnalyzerCapabilityDiagnostic'; ruleId = 'analyzer.capability.semantic.v1'; evidenceTier = 'Tier4Unknown'; evidence = @{ filePath = 'private-web/private-backend/service.csproj' }; properties = @{ capabilityCode = 'CSharpSemanticCompilation'; capabilityState = 'reduced' } },
        @{ factType = 'BuildEnvironmentDiagnostic'; ruleId = 'build.environment.workspace-diagnostic.v1'; evidenceTier = 'Tier4Unknown'; evidence = @{ filePath = 'private-web/private-backend/service.csproj' }; properties = @{ diagnosticCode = 'LegacyWorkspacePrerequisitesUnresolved'; diagnosticKind = 'workspace'; guidanceCode = 'UseCompatibleMSBuildToolset'; originCategory = 'project-load'; originGapKind = 'ProjectLoadFailed'; diagnosticId = 'MSB4025'; occurrenceCount = '2' } },
        @{ factType = 'BuildEnvironmentDiagnostic'; ruleId = 'build.environment.workspace-diagnostic.v1'; evidenceTier = 'Tier4Unknown'; evidence = @{ filePath = 'private-web/default.aspx' }; properties = @{ diagnosticCode = 'UncategorizedWorkspaceFailure'; diagnosticKind = 'workspace'; guidanceCode = 'ReviewEnvironmentGap'; originCategory = 'workspace'; originGapKind = 'WorkspaceDiagnostic'; occurrenceCount = '1' } },
        @{ factType = 'AnalysisGap'; ruleId = 'csharp.semantic.workspace.v1'; evidenceTier = 'Tier4Unknown'; evidence = @{ filePath = 'private-controls/widget.cs' }; properties = @{ diagnosticCode = 'CompilerDiagnostic'; diagnosticKind = 'compilation'; guidanceCode = 'ReviewCompilerDiagnostic'; gapKind = 'CompilationDiagnostic'; diagnosticId = 'CS0103' } },
        @{ factType = 'BuildEnvironmentDiagnostic'; ruleId = 'build.environment.target-framework.v1'; evidenceTier = 'Tier2Structural'; evidence = @{ filePath = 'private-web/private-backend/service.csproj' }; properties = @{ diagnosticCode = 'LegacyTargetFramework'; diagnosticKind = 'target-framework'; guidanceCode = 'UseCompatibleReferenceAssemblies'; originCategory = 'static-project-inspection'; occurrenceCount = '1' } },
        @{ factType = 'CallEdge'; ruleId = 'csharp.semantic.call.v1'; evidenceTier = 'Tier1Semantic'; evidence = @{ filePath = 'private-controls/widget.cs' }; properties = @{} }
    )
    [IO.File]::WriteAllLines((Join-Path $scan 'facts.ndjson'), @($facts | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 10 }), [Text.UTF8Encoding]::new($false))

    $head = '0123456789abcdef0123456789abcdef01234567'
    $result = @(& $script -ReviewOutputPath $review -WebFormsFolder 'private-web' -BackendFolder 'private-web/private-backend' -ControlsFolder 'private-controls' -TraceMapHead $head -OutputDirectory $output)
    Assert-True ($result[0] -eq 'focused-webforms-workspace-summary-file=created') 'summary creation was not reported'
    $file = @(Get-ChildItem $output -File -Filter 'focused-webforms-workspace-*.txt')
    Assert-True ($file.Count -eq 1) 'workspace summary was not created'
    $content = [IO.File]::ReadAllText($file[0].FullName)
    Assert-True ($content.Contains('focused-webforms-workspace=partial')) 'reduced analysis was not labeled partial'
    Assert-True ($content.Contains("tracemapHead=$head")) 'TraceMap head missing'
    Assert-True ($content.Contains('semanticCompilation=reduced')) 'semantic state missing'
    Assert-True ($content.Contains('tier1FactCount=1')) 'Tier1 count incorrect'
    Assert-True ($content.Contains('uncategorizedWorkspaceFailureCount=1')) 'uncategorized count incorrect'
    Assert-True ($content.Contains('workspaceDiagnosticCount=3')) 'workspace occurrence count incorrect'
    Assert-True ($content.Contains('compilerDiagnosticCount=1')) 'compiler occurrence count incorrect'
    Assert-True ($content.Contains('staticDiagnosticCount=1')) 'static diagnostic count incorrect'
    Assert-True ($content.Contains('unknownDiagnosticOriginCount=0')) 'known origins were not preserved'
    Assert-True ($content.Contains('diagnostic=backend|project-load|ProjectLoadFailed|MSB4025|LegacyWorkspacePrerequisitesUnresolved|UseCompatibleMSBuildToolset|count=2')) 'backend diagnostic lineage missing'
    Assert-True ($content.Contains('diagnostic=webforms|workspace|WorkspaceDiagnostic|none|UncategorizedWorkspaceFailure|ReviewEnvironmentGap|count=1')) 'webforms diagnostic lineage missing'
    Assert-True ($content.Contains('diagnostic=controls|compilation|CompilationDiagnostic|CS0103|CompilerDiagnostic|ReviewCompilerDiagnostic|count=1')) 'compiler diagnostic lineage missing'
    Assert-True ($content.Contains('diagnostic=backend|static-project-inspection|unknown|none|LegacyTargetFramework|UseCompatibleReferenceAssemblies|count=1')) 'static diagnostic lineage missing'
    Assert-True ($content.Contains('nextAction=classify-bounded-workspace-failure')) 'next action incorrect'
    Assert-True (-not $content.Contains('private-web')) 'private Web Forms folder leaked'
    Assert-True (-not $content.Contains('private-backend')) 'private backend folder leaked'
    Assert-True (-not $content.Contains('private-controls')) 'private controls folder leaked'
    Assert-True (-not $content.Contains($testRoot)) 'local path leaked'

    [IO.File]::WriteAllText((Join-Path $scan 'scan-manifest.json'), '{"analysisLevel":"Level1SemanticAnalysis","buildStatus":"Succeeded"}', [Text.UTF8Encoding]::new($false))
    $facts[0].properties.capabilityState = 'available'
    [IO.File]::WriteAllLines((Join-Path $scan 'facts.ndjson'), @($facts | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 10 }), [Text.UTF8Encoding]::new($false))
    $fullOutput = Join-Path $testRoot 'full-summary'
    $fullResult = @(& $script -ReviewOutputPath $review -WebFormsFolder 'private-web' -BackendFolder 'private-web/private-backend' -ControlsFolder 'private-controls' -TraceMapHead $head -OutputDirectory $fullOutput)
    Assert-True ($fullResult[0] -eq 'focused-webforms-workspace-summary-file=created') 'full summary creation was not reported'
    $fullFile = @(Get-ChildItem $fullOutput -File -Filter 'focused-webforms-workspace-*.txt')
    Assert-True ($fullFile.Count -eq 1) 'full workspace summary was not created'
    $fullContent = [IO.File]::ReadAllText($fullFile[0].FullName)
    Assert-True ($fullContent.Contains('focused-webforms-workspace=completed')) 'full semantic analysis was not labeled completed'
    Assert-True ($fullContent.Contains('semanticCompilation=available')) 'available semantic state missing'

    $legacyFacts = @(
        @{ factType = 'BuildEnvironmentDiagnostic'; ruleId = 'build.environment.workspace-diagnostic.v1'; evidenceTier = 'Tier4Unknown'; evidence = @{ filePath = 'private-web/default.aspx' }; properties = @{ diagnosticCode = 'LegacyWorkspacePrerequisitesUnresolved'; diagnosticKind = 'workspace'; guidanceCode = 'UseCompatibleMSBuildToolset' } }
    )
    [IO.File]::WriteAllLines((Join-Path $scan 'facts.ndjson'), @($legacyFacts | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 10 }), [Text.UTF8Encoding]::new($false))
    $legacyOutput = Join-Path $testRoot 'legacy-summary'
    [void]@(& $script -ReviewOutputPath $review -WebFormsFolder 'private-web' -BackendFolder 'private-web/private-backend' -ControlsFolder 'private-controls' -TraceMapHead $head -OutputDirectory $legacyOutput)
    $legacyFile = @(Get-ChildItem $legacyOutput -File -Filter 'focused-webforms-workspace-*.txt')
    $legacyContent = [IO.File]::ReadAllText($legacyFile[0].FullName)
    Assert-True ($legacyContent.Contains('unknownDiagnosticOriginCount=1')) 'legacy lineage absence was not explicit'
    Assert-True ($legacyContent.Contains('nextAction=rerun-with-diagnostic-lineage')) 'legacy artifact did not request a lineage-aware rerun'
    'focused-webforms-workspace-summary-tests=passed'
}
finally {
    if (Test-Path $testRoot) { Remove-Item $testRoot -Recurse -Force }
}
