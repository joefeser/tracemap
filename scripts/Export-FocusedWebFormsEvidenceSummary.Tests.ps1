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
$script = Join-Path $TraceMapRoot "scripts/Export-FocusedWebFormsEvidenceSummary.ps1"
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($script, [ref]$tokens, [ref]$parseErrors) | Out-Null
Assert-True ($parseErrors.Count -eq 0) "summary script syntax is invalid"

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("tracemap-webforms-summary-test-" + [Guid]::NewGuid().ToString("N"))
$review = Join-Path $testRoot "review"
$scan = Join-Path $review "scan"
$output = Join-Path $testRoot "summary"

try {
    New-Item -ItemType Directory -Path $scan -Force | Out-Null
    [IO.File]::WriteAllText(
        (Join-Path $review "local-review-result.json"),
        '{"schemaVersion":"local-review-result.v1","outcome":"partial"}',
        [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText(
        (Join-Path $scan "scan-manifest.json"),
        '{}',
        [Text.UTF8Encoding]::new($false))

    $facts = @(
        @{ factType = "AnalysisGap"; ruleId = "message.surface.gap.v1"; evidence = @{ extractorId = "FixtureExtractor"; extractorVersion = "fixture-extractor/1.0.0" }; properties = @{ classification = "TargetUnavailable" } },
        @{ factType = "AnalysisGap"; ruleId = "message.surface.gap.v1"; evidence = @{ extractorId = "FixtureExtractor"; extractorVersion = "fixture-extractor/1.0.0" }; properties = @{ gapKind = "BindingUnavailable" } },
        @{ factType = "WebFormsPageDeclared"; ruleId = "legacy.webforms.inventory.v1"; evidence = @{ extractorId = "FixtureExtractor"; extractorVersion = "fixture-extractor/1.0.0" } },
        @{ factType = "AnalysisGap"; ruleId = "not.catalogued.v1"; evidence = @{ extractorId = "unsafe extractor"; extractorVersion = "fixture-1.0.0" } }
    )
    $factLines = @($facts | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 10 })
    [IO.File]::WriteAllLines(
        (Join-Path $scan "facts.ndjson"),
        $factLines,
        [Text.UTF8Encoding]::new($false))

    $firstResult = @(& $script -ReviewOutputPath $review -OutputDirectory $output)
    Assert-True ($firstResult[0] -eq "focused-webforms-evidence-summary-file=created") "summary creation was not reported"
    $firstFile = @(Get-ChildItem -LiteralPath $output -File -Filter "focused-webforms-gap-extractor-*.txt")
    Assert-True ($firstFile.Count -eq 1) "first summary file was not created"
    $firstContent = [IO.File]::ReadAllText($firstFile[0].FullName)
    Assert-True ($firstContent.Contains("factTotal=4")) "fact total is incorrect"
    Assert-True ($firstContent.Contains("analysisGapTotal=3")) "gap total is incorrect"
    Assert-True ($firstContent.Contains("uncataloguedGapRuleIdCount=1")) "uncatalogued count is incorrect"
    Assert-True ($firstContent.Contains("gapReasonKinds=2")) "gap reason kind count is incorrect"
    Assert-True ($firstContent.Contains("unavailableGapReasonCount=1")) "unavailable gap reason count is incorrect"
    Assert-True ($firstContent.Contains("unavailableExtractorIdentityFactCount=1")) "unsafe extractor count is incorrect"
    Assert-True ($firstContent.Contains("topGapRule01=message.surface.gap.v1|count=2")) "top gap row is incorrect"
    Assert-True ($firstContent.Contains("topGapReason01=message.surface.gap.v1|field=classification|reason=TargetUnavailable|count=1")) "top gap reason row is incorrect"
    Assert-True ($firstContent.Contains("topExtractor01=FixtureExtractor|version=fixture-extractor/1.0.0|facts=3|gaps=2")) "extractor row is incorrect"
    Assert-True (-not $firstContent.Contains($testRoot)) "summary leaked its local root"
    Assert-True (-not $firstContent.Contains("not.catalogued.v1")) "summary leaked an uncatalogued rule"
    Assert-True (-not $firstContent.Contains("unsafe extractor")) "summary leaked an unsafe extractor"

    Start-Sleep -Milliseconds 5
    [void]@(& $script -ReviewOutputPath $review -OutputDirectory $output)
    $files = @(Get-ChildItem -LiteralPath $output -File -Filter "focused-webforms-gap-extractor-*.txt" | Sort-Object Name)
    Assert-True ($files.Count -eq 2) "second summary file was not created"
    $secondContent = [IO.File]::ReadAllText($files[1].FullName)
    Assert-True ($firstContent -ceq $secondContent) "repeated summary content is not deterministic"

    Write-Output "focused-webforms-evidence-summary-tests=passed"
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
