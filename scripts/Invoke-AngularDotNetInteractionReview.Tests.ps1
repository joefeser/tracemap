[CmdletBinding()]
param(
    [string]$TraceMapRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )
    if (-not $Condition) { throw $Message }
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )
    & git -C $Repository @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "fixture git command failed" }
}

$TraceMapRoot = [System.IO.Path]::GetFullPath($TraceMapRoot)
$runner = Join-Path $TraceMapRoot "scripts/Invoke-AngularDotNetInteractionReview.ps1"
$tokens = $null
$parseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$parseErrors) | Out-Null
Assert-True ($parseErrors.Count -eq 0) "runner PowerShell syntax is invalid"

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("tracemap-interaction-review-test-" + [System.Guid]::NewGuid().ToString("N"))
$angular = Join-Path $testRoot "angular"
$api = Join-Path $testRoot "api"
$configPath = Join-Path $testRoot "review.json"
$output1 = Join-Path $testRoot "output-1"
$output2 = Join-Path $testRoot "output-2"

try {
    New-Item -ItemType Directory -Path $angular, $api | Out-Null
    Copy-Item -Path (Join-Path $TraceMapRoot "samples/endpoint-client-angular/*") -Destination $angular -Recurse -Force
    Copy-Item -Path (Join-Path $TraceMapRoot "samples/endpoint-server-aspnet/*") -Destination $api -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $TraceMapRoot ".gitignore") -Destination (Join-Path $angular ".gitignore")
    Copy-Item -LiteralPath (Join-Path $TraceMapRoot ".gitignore") -Destination (Join-Path $api ".gitignore")

    foreach ($repository in @($angular, $api)) {
        Invoke-Git $repository @("init", "--quiet")
        Invoke-Git $repository @("config", "user.name", "TraceMap Fixture")
        Invoke-Git $repository @("config", "user.email", "fixture@example.invalid")
        Invoke-Git $repository @("add", ".")
        Invoke-Git $repository @("commit", "--quiet", "-m", "fixture")
    }

    $config = [ordered]@{
        schemaVersion = "angular-dotnet-interaction-run.v1"
        sources = @(
            [ordered]@{
                label = "angular-client"
                kind = "typescript"
                repositoryPath = $angular
                projects = @("tsconfig.json")
            },
            [ordered]@{
                label = "dotnet-api"
                kind = "dotnet"
                repositoryPath = $api
                projects = @("EndpointServerSample.csproj")
            }
        )
        endpointPairs = @(
            [ordered]@{
                name = "client-api"
                clientLabel = "angular-client"
                serverLabel = "dotnet-api"
            }
        )
        propertyFlows = @(
            [ordered]@{
                name = "runner-id"
                selector = "field:runnerId"
                sourceLabel = "angular-client"
                framework = "angular"
            }
        )
        routeFlows = @(
            [ordered]@{
                name = "runner-get"
                route = "GET /api/admin/runner/get-by-id/{}"
            }
        )
        paths = @(
            [ordered]@{
                name = "runner-to-sql"
                fromEndpoint = "GET /api/admin/runner/get-by-id/{}"
                toSurface = "sql-query"
                sourcePair = "angular-client:dotnet-api"
            }
        )
        reports = [ordered]@{
            combinedDependency = $true
            portfolio = $true
        }
    }
    [System.IO.File]::WriteAllText(
        $configPath,
        (($config | ConvertTo-Json -Depth 20) + "`n"),
        [System.Text.UTF8Encoding]::new($false))

    & pwsh -NoProfile -File $runner -ConfigPath $configPath -TraceMapRoot $TraceMapRoot -OutputRoot $output1 -ValidateOnly | Out-Null
    Assert-True ($LASTEXITCODE -eq 0) "configuration validation failed"

    $nativePreference = $PSNativeCommandUseErrorActionPreference
    $PSNativeCommandUseErrorActionPreference = $false

    $config.propertyFlows[0].framework = "webforms"
    [System.IO.File]::WriteAllText($configPath, (($config | ConvertTo-Json -Depth 20) + "`n"), [System.Text.UTF8Encoding]::new($false))
    & pwsh -NoProfile -File $runner -ConfigPath $configPath -TraceMapRoot $TraceMapRoot -OutputRoot $output1 -ValidateOnly 2>$null | Out-Null
    Assert-True ($LASTEXITCODE -ne 0) "unsupported Web Forms property-flow framework was accepted"
    $config.propertyFlows[0].framework = "angular"

    $config.endpointPairs = @(0..100 | ForEach-Object {
        [ordered]@{ name = "pair-$($_.ToString('000'))"; clientLabel = "angular-client"; serverLabel = "dotnet-api" }
    })
    [System.IO.File]::WriteAllText($configPath, (($config | ConvertTo-Json -Depth 20) + "`n"), [System.Text.UTF8Encoding]::new($false))
    & pwsh -NoProfile -File $runner -ConfigPath $configPath -TraceMapRoot $TraceMapRoot -OutputRoot $output1 -ValidateOnly 2>$null | Out-Null
    Assert-True ($LASTEXITCODE -ne 0) "endpoint-pair count above the contract limit was accepted"
    $config.endpointPairs = @(
        [ordered]@{ name = "client-api"; clientLabel = "angular-client"; serverLabel = "dotnet-api" }
    )
    [System.IO.File]::WriteAllText($configPath, (($config | ConvertTo-Json -Depth 20) + "`n"), [System.Text.UTF8Encoding]::new($false))

    & pwsh -NoProfile -File $runner -ConfigPath $configPath -TraceMapRoot $TraceMapRoot -OutputRoot (Join-Path $angular "unsafe-output") -ValidateOnly 2>$null | Out-Null
    $unsafeOutputExit = $LASTEXITCODE
    $PSNativeCommandUseErrorActionPreference = $nativePreference
    Assert-True ($unsafeOutputExit -ne 0) "output inside a source repository was accepted"

    & pwsh -NoProfile -File $runner -ConfigPath $configPath -TraceMapRoot $TraceMapRoot -OutputRoot $output1 | Out-Null
    Assert-True ($LASTEXITCODE -eq 0) "first interaction review failed"
    & pwsh -NoProfile -File $runner -ConfigPath $configPath -TraceMapRoot $TraceMapRoot -OutputRoot $output2 | Out-Null
    Assert-True ($LASTEXITCODE -eq 0) "second interaction review failed"

    foreach ($output in @($output1, $output2)) {
        foreach ($relative in @(
            "interaction-run-result.json",
            "feedback-summary.json",
            "feedback-summary.md",
            "combined.sqlite",
            "reports/dependency/dependency-report.json",
            "reports/portfolio/portfolio-report.json",
            "reports/endpoints/client-api/endpoint-report.json",
            "reports/property-flow/runner-id/property-flow-report.json",
            "reports/route-flow/runner-get/route-flow-report.json",
            "reports/paths/runner-to-sql/paths-report.json")) {
            Assert-True (Test-Path -LiteralPath (Join-Path $output $relative) -PathType Leaf) "missing output $relative"
        }
        $result = Get-Content -LiteralPath (Join-Path $output "interaction-run-result.json") -Raw | ConvertFrom-Json -Depth 100
        $feedback = Get-Content -LiteralPath (Join-Path $output "feedback-summary.json") -Raw | ConvertFrom-Json -Depth 100
        Assert-True ($result.schemaVersion -eq "angular-dotnet-interaction-run-result.v1") "unexpected result schema"
        Assert-True ($result.outcome -eq "succeeded") "unexpected result outcome"
        Assert-True ($result.sources.Count -eq 2) "unexpected source count"
        Assert-True ($result.reports.Count -eq 6) "unexpected report count"
        Assert-True ($feedback.schemaVersion -eq "angular-dotnet-interaction-feedback.v1") "unexpected feedback schema"
        Assert-True ($feedback.reportCount -eq 6) "unexpected feedback report count"
        $reportStateKeys = @($feedback.reportStates | ForEach-Object { "$($_.producer)|$($_.classification)|$($_.coverage)|$($_.truncated)" })
        $sortedReportStateKeys = @($feedback.reportStates |
            Sort-Object `
                { [string]$_.producer },
                { [string]$_.classification },
                { [string]$_.coverage },
                { [bool]$_.truncated } |
            ForEach-Object { "$($_.producer)|$($_.classification)|$($_.coverage)|$($_.truncated)" })
        Assert-True (($reportStateKeys -join "`n") -eq ($sortedReportStateKeys -join "`n")) "feedback report states are not canonically ordered"
        foreach ($signal in $feedback.unresolvedSignals) {
            Assert-True (-not [string]::IsNullOrWhiteSpace([string]$signal.ruleId)) "feedback signal omitted a rule ID"
            Assert-True ([string]$signal.evidenceTier -in @("Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown")) "feedback signal used an invalid evidence tier"
            Assert-True (-not [string]::IsNullOrWhiteSpace([string]$signal.coverage)) "feedback signal omitted coverage"
        }
        $feedbackText = Get-Content -LiteralPath (Join-Path $output "feedback-summary.json") -Raw
        Assert-True (-not $feedbackText.Contains($testRoot, [System.StringComparison]::Ordinal)) "feedback leaked a local path"
        Assert-True (-not $feedbackText.Contains("client-api", [System.StringComparison]::Ordinal)) "feedback leaked a configured query name"
        Assert-True (-not $feedbackText.Contains("runner-id", [System.StringComparison]::Ordinal)) "feedback leaked a property-flow query name"
        Assert-True (-not $feedbackText.Contains("runner-get", [System.StringComparison]::Ordinal)) "feedback leaked a route-flow query name"
        Assert-True (-not $feedbackText.Contains("runner-to-sql", [System.StringComparison]::Ordinal)) "feedback leaked a path query name"
    }

    $feedback1 = Get-FileHash -LiteralPath (Join-Path $output1 "feedback-summary.json") -Algorithm SHA256
    $feedback2 = Get-FileHash -LiteralPath (Join-Path $output2 "feedback-summary.json") -Algorithm SHA256
    $markdown1 = Get-FileHash -LiteralPath (Join-Path $output1 "feedback-summary.md") -Algorithm SHA256
    $markdown2 = Get-FileHash -LiteralPath (Join-Path $output2 "feedback-summary.md") -Algorithm SHA256
    Assert-True ($feedback1.Hash -eq $feedback2.Hash) "feedback JSON is not deterministic"
    Assert-True ($markdown1.Hash -eq $markdown2.Hash) "feedback Markdown is not deterministic"

    $catalog = Get-Content -LiteralPath (Join-Path $TraceMapRoot "rules/rule-catalog.yml") -Raw
    Assert-True ($catalog.Contains("- id: interaction.review.feedback.v1", [System.StringComparison]::Ordinal)) "feedback projection rule is missing from the catalog"

    Assert-True (@(& git -C $angular status --short).Count -eq 0) "Angular fixture changed"
    Assert-True (@(& git -C $api status --short).Count -eq 0) ".NET fixture changed"
    "interaction-review-tests=passed"
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
