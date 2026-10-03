$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $scripts 'Show-FocusedWebFormsOutlierSummary.ps1'
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw 'Outlier summary script syntax is invalid.' }

$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-outlier-summary-' + [Guid]::NewGuid().ToString('N'))
try {
    $workbench = Join-Path $temp 'workbench'
    [IO.Directory]::CreateDirectory($workbench) | Out-Null
    $hashA = 'a' * 64
    $hashB = 'b' * 64
    $outlierPath = Join-Path $workbench 'application-outliers.shareable.json'
    $outliers = [ordered]@{
        schemaVersion = 'webforms-application-outliers.v1'
        ruleId = 'diagnostic.webforms.application-outlier-ranking.v1'
        privacy = 'anonymous-counts-only'
        provenance = [ordered]@{ generatorSha256 = $hashA; inputSha256 = $hashB }
        pageCount = 2
        pages = @(
            [ordered]@{ pageId = 'page-001'; counts = [ordered]@{ callProjections = 10; uniqueCallFacts = 8; normalizedCallSites = 7; gaps = 2 }; gapCategories = @([ordered]@{ classification = 'DownstreamWithoutSupportedTerminal'; count = 2 }) },
            [ordered]@{ pageId = 'page-002'; counts = [ordered]@{ callProjections = 4; uniqueCallFacts = 3; normalizedCallSites = 2; gaps = 1 }; gapCategories = @([ordered]@{ classification = 'NoBackendEvidence'; count = 1 }) }
        )
    }
    [IO.File]::WriteAllText($outlierPath, (($outliers | ConvertTo-Json -Depth 12) + "`n"), [Text.UTF8Encoding]::new($false))
    $file = Get-Item -LiteralPath $outlierPath
    $artifactSha = (Get-FileHash -LiteralPath $outlierPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $receipt = [ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        run = [ordered]@{ state = 'completed' }
        stages = [ordered]@{ workbench = [ordered]@{ state = 'completed'; artifacts = @([ordered]@{ path = 'workbench/application-outliers.shareable.json'; bytes = $file.Length; sha256 = $artifactSha }) } }
    }
    [IO.File]::WriteAllText((Join-Path $temp 'run-receipt.json'), (($receipt | ConvertTo-Json -Depth 12) + "`n"), [Text.UTF8Encoding]::new($false))

    $result = @(& $scriptPath -ReviewRoot $temp)
    foreach ($expected in @('outlierSummary=valid','pages=2','callProjections=14','uniqueCallFacts=11','normalizedCallSites=9','gaps=3','gap.DownstreamWithoutSupportedTerminal=2','gap.NoBackendEvidence=1',"generatorSha256=$hashA","inputSha256=$hashB")) {
        if ($result -notcontains $expected) { throw "Outlier summary omitted: $expected; actual=$($result -join ',')" }
    }

    [IO.File]::AppendAllText($outlierPath, 'tampered')
    $failure = $null
    try { & $scriptPath -ReviewRoot $temp | Out-Null } catch { $failure = $_.Exception.Message }
    if ($failure -ne 'WEBFORMS_OUTLIER_SUMMARY_ARTIFACT_MISMATCH') { throw 'Outlier summary did not reject a receipt hash mismatch.' }
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

Write-Host 'PASS focused Web Forms outlier summary'
