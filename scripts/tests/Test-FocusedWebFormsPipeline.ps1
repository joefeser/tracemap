$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$setupPath = Join-Path $scripts 'Initialize-FocusedWebFormsReview.ps1'
$pipelinePath = Join-Path $scripts 'Invoke-FocusedWebFormsPipeline.ps1'
$mergePath = Join-Path $scripts 'Merge-FocusedWebFormsReview.ps1'
$summaryPath = Join-Path $scripts 'Show-FocusedWebFormsOutlierSummary.ps1'
$helperPath = Join-Path $scripts 'webforms-review/FocusedWebFormsPipelineConfig.ps1'
$preflightPath = Join-Path $scripts 'Test-FocusedWebFormsReviewConfig.ps1'
$statusPath = Join-Path $scripts 'Show-FocusedWebFormsReviewStatus.ps1'
$claudePath = Join-Path $scripts 'Start-FocusedWebFormsClaudeReview.ps1'

foreach ($path in @($setupPath, $pipelinePath, $mergePath, $summaryPath, $helperPath, $preflightPath, $statusPath, $claudePath)) {
    $tokens = $null
    $errors = $null
    [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -ne 0) { throw "Pipeline script syntax invalid: $path" }
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-pipeline-' + [Guid]::NewGuid().ToString('N'))
try {
    $reviewRoot = Join-Path $temp 'review'
    & $setupPath -ReviewRoot $reviewRoot | Out-Null
    $configPath = Join-Path $reviewRoot 'config/webforms-review.jsonc'
    foreach ($required in @($configPath, (Join-Path $reviewRoot 'README.md'), (Join-Path $reviewRoot 'logs')) ) {
        if (!(Test-Path -LiteralPath $required)) { throw "Setup omitted: $required" }
    }

    $generatedConfigText = [IO.File]::ReadAllText($configPath)
    if (!$generatedConfigText.Contains('// Relative folder scopes.', [StringComparison]::Ordinal)) { throw 'Generated JSONC omitted field guidance.' }
    . $helperPath
    $raw = (Remove-FocusedWebFormsJsonComments -Text $generatedConfigText) | ConvertFrom-Json -Depth 10
    $seven = @('sourceRoot','webFormsFolder','backendFolder','controlsFolder','projectSelection','outputRoot','pageSelection')
    if (@($seven | Where-Object { $_ -notin $raw.PSObject.Properties.Name }).Count -ne 0) { throw 'Setup omitted one of seven operational settings.' }
    if ($raw.projectSelection.mode -ne 'solution' -or $raw.pageSelection.mode -ne 'all') { throw 'Setup defaults were not solution plus all-pages.' }

    foreach ($unsafePath in @('C:\work\source', 'C:\temp\source')) {
        $unsafeConfigText = $generatedConfigText.Replace('C:/path/to/authorized-source', $unsafePath)
        [IO.File]::WriteAllText($configPath, $unsafeConfigText, [Text.UTF8Encoding]::new($false))
        $failure = $null
        try { Read-FocusedWebFormsPipelineConfig $configPath | Out-Null } catch { $failure = $_.Exception.Message }
        if ($failure -ne 'WEBFORMS_PIPELINE_CONFIG_UNESCAPED_BACKSLASH;use-forward-slashes-in-paths-example=C:/work/review') {
            throw "Unescaped Windows path did not receive actionable preflight guidance: $unsafePath"
        }
    }
    [IO.File]::WriteAllText($configPath, $generatedConfigText, [Text.UTF8Encoding]::new($false))

    $preflightConfig = (Remove-FocusedWebFormsJsonComments -Text $generatedConfigText) | ConvertFrom-Json -Depth 10
    $preflightConfig.sourceRoot = (Split-Path -Parent $scripts)
    $preflightConfig.webFormsFolder = 'scripts'
    $preflightConfig.backendFolder = 'docs'
    $preflightConfig.controlsFolder = 'prompts'
    $preflightConfig.projectSelection = [ordered]@{ mode = 'solution'; solutionRelativePath = 'src/dotnet/TraceMap.sln'; projectRelativePaths = @() }
    [IO.File]::WriteAllText($configPath, (($preflightConfig | ConvertTo-Json -Depth 10) + "`n"), [Text.UTF8Encoding]::new($false))
    function global:git {
        if ($args -contains '--show-toplevel') { $global:LASTEXITCODE = 0; return (Split-Path -Parent $scripts) }
        if ($args -contains 'status') { $global:LASTEXITCODE = 0; return }
        $global:LASTEXITCODE = 0
    }
    try {
        $preflightOutput = @(& $preflightPath -ReviewRoot $reviewRoot -TraceMapRoot (Split-Path -Parent $scripts) 6>&1 | ForEach-Object { $_.ToString() })
    }
    finally { Remove-Item Function:\global:git -ErrorAction SilentlyContinue }
    if ($preflightOutput -notcontains 'webformsPreflight=valid' -or $preflightOutput -notcontains 'nextAction=run-focused-webforms-pipeline') {
        throw 'Valid review config did not pass the operator preflight.'
    }
    $statusOutput = @(& $statusPath -ReviewRoot $reviewRoot)
    if ($statusOutput -notcontains 'webformsReviewStatus=valid' -or $statusOutput -notcontains 'runState=unavailable') {
        throw 'Review status did not describe an initialized pre-run root.'
    }
    [IO.File]::WriteAllText($configPath, $generatedConfigText, [Text.UTF8Encoding]::new($false))

    $parsed = Read-FocusedWebFormsPipelineConfig $configPath
    if ($parsed.PageMode -ne 'all' -or $parsed.ProjectMode -ne 'solution' -or $parsed.OutputRoot -ne $reviewRoot) { throw 'Generated config did not round-trip.' }

    $raw.projectSelection = [ordered]@{ mode = 'discover'; solutionRelativePath = ''; projectRelativePaths = @() }
    $raw.pageSelection = [ordered]@{ mode = 'selected'; forms = @('Web/One.aspx','Web/Two.aspx') }
    [IO.File]::WriteAllText($configPath, (($raw | ConvertTo-Json -Depth 10) + "`n"), [Text.UTF8Encoding]::new($false))
    $parsed = Read-FocusedWebFormsPipelineConfig $configPath
    if ($parsed.ProjectMode -ne 'discover' -or $parsed.Forms.Count -ne 2) { throw 'Discovery/selected config did not round-trip.' }

    $raw.projectSelection = [ordered]@{ mode = 'projects'; solutionRelativePath = ''; projectRelativePaths = @('Web/One.csproj') }
    [IO.File]::WriteAllText($configPath, (($raw | ConvertTo-Json -Depth 10) + "`n"), [Text.UTF8Encoding]::new($false))
    $parsed = Read-FocusedWebFormsPipelineConfig $configPath
    if ($parsed.ProjectMode -ne 'projects' -or $parsed.ProjectRelativePaths.Count -ne 1 -or
        $parsed.ProjectRelativePaths[0] -ne 'Web/One.csproj') { throw 'One-project C# config did not round-trip.' }

    $raw.projectSelection.projectRelativePaths = @('Web/One.csproj','Backend/Two.vbproj')
    [IO.File]::WriteAllText($configPath, (($raw | ConvertTo-Json -Depth 10) + "`n"), [Text.UTF8Encoding]::new($false))
    $parsed = Read-FocusedWebFormsPipelineConfig $configPath
    if ($parsed.ProjectRelativePaths.Count -ne 2 -or
        $parsed.ProjectRelativePaths -notcontains 'Web/One.csproj' -or
        $parsed.ProjectRelativePaths -notcontains 'Backend/Two.vbproj') { throw 'Mixed multi-project config did not round-trip.' }

    $raw.projectSelection = [ordered]@{ mode = 'projectless'; solutionRelativePath = ''; projectRelativePaths = @() }
    $raw.pageSelection = [ordered]@{ mode = 'all'; forms = @() }
    [IO.File]::WriteAllText($configPath, (($raw | ConvertTo-Json -Depth 10) + "`n"), [Text.UTF8Encoding]::new($false))
    $parsed = Read-FocusedWebFormsPipelineConfig $configPath
    if ($parsed.ProjectMode -ne 'projectless' -or $parsed.PageMode -ne 'all') { throw 'Projectless all-pages config did not round-trip.' }

    $jsonCopy = Join-Path $reviewRoot 'config/webforms-review.json'
    [IO.File]::WriteAllText($jsonCopy, (($raw | ConvertTo-Json -Depth 10) + "`n"), [Text.UTF8Encoding]::new($false))
    $failure = $null
    try { Resolve-FocusedWebFormsPipelineConfigPath -ReviewRoot $reviewRoot | Out-Null } catch { $failure = $_.Exception.Message }
    if (!$failure.StartsWith('WEBFORMS_PIPELINE_CONFIG_CONFLICT', [StringComparison]::Ordinal)) { throw 'Dual JSON/JSONC config was not rejected.' }
    Remove-Item -LiteralPath $jsonCopy

    $trailingComma = $generatedConfigText.Replace('"forms": []', '"forms": [],')
    [IO.File]::WriteAllText($configPath, $trailingComma, [Text.UTF8Encoding]::new($false))
    $failure = $null
    try { Read-FocusedWebFormsPipelineConfig $configPath | Out-Null } catch { $failure = $_.Exception.Message }
    if (!$failure.StartsWith('WEBFORMS_PIPELINE_CONFIG_TRAILING_COMMA', [StringComparison]::Ordinal)) { throw 'JSONC trailing comma was not diagnosed.' }
    [IO.File]::WriteAllText($configPath, $generatedConfigText, [Text.UTF8Encoding]::new($false))

    $pipeline = [IO.File]::ReadAllText($pipelinePath)
    foreach ($required in @(
        'focused-webforms-review-run-receipt.v1',
        'generatorSha256',
        'configSha256',
        'WEBFORMS_PIPELINE_RESUME_PROVENANCE_MISMATCH',
        'WEBFORMS_PIPELINE_RESUME_ARTIFACT_MISMATCH',
        'oversize-scan-artifact-limit-recovery',
        'pipelineStage=scan;state=recovered;reason=prior-artifact-limit',
        '17179869184',
        "pipelineStage=scan;state=completed",
        "webformsPipeline=scan-only-completed",
        "runMode = `$runMode",
        "pipelineStage=packet;state=completed",
        "pipelineStage=evidenceDocs;state=completed",
        "pipelineStage=workbench;state=completed",
        "'solution' { `$scanArgs.SolutionRelativePath",
        "'projects' { `$scanArgs.ProjectRelativePath",
        "'discover' { `$scanArgs.DiscoverProjects",
        "'projectless' { `$scanArgs.Projectless",
        "`$config.PageMode -eq 'selected'",
        "`$config.PageMode",
        "shareableOnlyWhenExplicitlyNamed"
    )) {
        if (!$pipeline.Contains($required, [StringComparison]::Ordinal)) { throw "Pipeline contract omitted: $required" }
    }
    if ($pipeline.Contains('Sort-Object LastWriteTime', [StringComparison]::Ordinal)) { throw 'Pipeline must not guess artifacts by timestamp.' }

    $merge = [IO.File]::ReadAllText($mergePath)
    foreach ($required in @(
        'WEBFORMS_MERGE_SCAN_INDEX_MISMATCH',
        'WEBFORMS_MERGE_SOURCE_COMMIT_MISMATCH',
        'two-focused-webforms-scan-receipts',
        'dotnet `$cliDll combine',
        'dotnet `$cliDll webforms-modernization',
        'New-FocusedWebFormsApplicationWorkbench.ps1',
        'webformsPipeline=merged-completed'
    )) {
        if (!$merge.Contains($required.Replace('`$', '$'), [StringComparison]::Ordinal)) { throw "Merge pipeline contract omitted: $required" }
    }

    $secondRoot = Join-Path $temp 'nonempty'
    [IO.Directory]::CreateDirectory($secondRoot) | Out-Null
    [IO.File]::WriteAllText((Join-Path $secondRoot 'existing.txt'), 'x')
    $failure = $null
    try { & $setupPath -ReviewRoot $secondRoot | Out-Null } catch { $failure = $_.Exception.Message }
    if ($failure -ne 'WEBFORMS_SETUP_ROOT_NOT_EMPTY') { throw 'Setup did not protect an existing review root.' }
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

Write-Host 'PASS focused Web Forms pipeline setup/config contract'
