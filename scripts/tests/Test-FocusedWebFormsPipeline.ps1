$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$setupPath = Join-Path $scripts 'Initialize-FocusedWebFormsReview.ps1'
$pipelinePath = Join-Path $scripts 'Invoke-FocusedWebFormsPipeline.ps1'
$summaryPath = Join-Path $scripts 'Show-FocusedWebFormsOutlierSummary.ps1'
$helperPath = Join-Path $scripts 'webforms-review/FocusedWebFormsPipelineConfig.ps1'

foreach ($path in @($setupPath, $pipelinePath, $summaryPath, $helperPath)) {
    $tokens = $null
    $errors = $null
    [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -ne 0) { throw "Pipeline script syntax invalid: $path" }
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-pipeline-' + [Guid]::NewGuid().ToString('N'))
try {
    $reviewRoot = Join-Path $temp 'review'
    & $setupPath -ReviewRoot $reviewRoot | Out-Null
    $configPath = Join-Path $reviewRoot 'config/webforms-review.json'
    foreach ($required in @($configPath, (Join-Path $reviewRoot 'README.md'), (Join-Path $reviewRoot 'logs')) ) {
        if (!(Test-Path -LiteralPath $required)) { throw "Setup omitted: $required" }
    }

    $generatedConfigText = [IO.File]::ReadAllText($configPath)
    $raw = $generatedConfigText | ConvertFrom-Json -Depth 10
    $seven = @('sourceRoot','webFormsFolder','backendFolder','controlsFolder','projectSelection','outputRoot','pageSelection')
    if (@($seven | Where-Object { $_ -notin $raw.PSObject.Properties.Name }).Count -ne 0) { throw 'Setup omitted one of seven operational settings.' }
    if ($raw.projectSelection.mode -ne 'solution' -or $raw.pageSelection.mode -ne 'all') { throw 'Setup defaults were not solution plus all-pages.' }

    . $helperPath

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

    $pipeline = [IO.File]::ReadAllText($pipelinePath)
    foreach ($required in @(
        'focused-webforms-review-run-receipt.v1',
        'generatorSha256',
        'configSha256',
        'WEBFORMS_PIPELINE_RESUME_PROVENANCE_MISMATCH',
        'WEBFORMS_PIPELINE_RESUME_ARTIFACT_MISMATCH',
        "pipelineStage=scan;state=completed",
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
