function Read-FocusedWebFormsPipelineConfig {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$ConfigPath)

    if (!(Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
        throw 'WEBFORMS_PIPELINE_CONFIG_UNAVAILABLE'
    }
    $file = Get-Item -LiteralPath $ConfigPath
    if ($file.Length -le 0 -or $file.Length -gt 1MB) {
        throw 'WEBFORMS_PIPELINE_CONFIG_LIMIT'
    }
    try {
        $configText = [IO.File]::ReadAllText($file.FullName, [Text.UTF8Encoding]::new($false, $true))
    }
    catch { throw 'WEBFORMS_PIPELINE_CONFIG_INVALID_JSON' }

    # A single Windows path separator can either make JSON invalid (for example,
    # \w) or silently become an escape character (for example, \t). Reject odd
    # runs before parsing so both cases receive the same actionable failure.
    if ([regex]::IsMatch($configText, '(?<!\\)(?:\\\\)*\\(?!\\)')) {
        throw 'WEBFORMS_PIPELINE_CONFIG_UNESCAPED_BACKSLASH;use-forward-slashes-in-paths-example=C:/work/review'
    }

    try { $config = $configText | ConvertFrom-Json -Depth 20 }
    catch { throw 'WEBFORMS_PIPELINE_CONFIG_INVALID_JSON' }

    $expected = @('schemaVersion','sourceRoot','webFormsFolder','backendFolder','controlsFolder','projectSelection','outputRoot','pageSelection')
    $actual = @($config.PSObject.Properties.Name)
    if ($config.schemaVersion -ne 'focused-webforms-review-config.v1' -or
        $actual.Count -ne $expected.Count -or
        @($expected | Where-Object { $_ -notin $actual }).Count -ne 0) {
        throw 'WEBFORMS_PIPELINE_CONFIG_SHAPE_INVALID'
    }
    foreach ($name in @('sourceRoot','webFormsFolder','backendFolder','controlsFolder','outputRoot')) {
        if ((Property-Value $config $name) -isnot [string] -or [string]::IsNullOrWhiteSpace([string](Property-Value $config $name))) {
            throw "WEBFORMS_PIPELINE_CONFIG_VALUE_REQUIRED;property=$name"
        }
    }

    $projectProperties = @($config.projectSelection.PSObject.Properties.Name)
    if ($projectProperties.Count -ne 3 -or @('mode','solutionRelativePath','projectRelativePaths' | Where-Object { $_ -notin $projectProperties }).Count -ne 0) {
        throw 'WEBFORMS_PIPELINE_PROJECT_SELECTION_INVALID'
    }
    $projectMode = ([string]$config.projectSelection.mode).Trim().ToLowerInvariant()
    if ($projectMode -notin @('solution','projects','discover','projectless')) {
        throw 'WEBFORMS_PIPELINE_PROJECT_SELECTION_INVALID'
    }
    $solutionRelativePath = ([string]$config.projectSelection.solutionRelativePath).Trim()
    $projectRelativePaths = @($config.projectSelection.projectRelativePaths)
    if ($projectMode -eq 'solution' -and [string]::IsNullOrWhiteSpace($solutionRelativePath)) {
        throw 'WEBFORMS_PIPELINE_SOLUTION_REQUIRED'
    }
    if ($projectMode -ne 'solution' -and $solutionRelativePath) {
        throw 'WEBFORMS_PIPELINE_SOLUTION_CONFLICT'
    }
    if ($projectMode -eq 'projects' -and $projectRelativePaths.Count -eq 0) {
        throw 'WEBFORMS_PIPELINE_PROJECTS_REQUIRED'
    }
    if ($projectMode -ne 'projects' -and $projectRelativePaths.Count -ne 0) {
        throw 'WEBFORMS_PIPELINE_PROJECTS_CONFLICT'
    }
    foreach ($project in $projectRelativePaths) {
        if ($project -isnot [string] -or [string]::IsNullOrWhiteSpace($project) -or
            !($project.EndsWith('.csproj', [StringComparison]::OrdinalIgnoreCase) -or $project.EndsWith('.vbproj', [StringComparison]::OrdinalIgnoreCase))) {
            throw 'WEBFORMS_PIPELINE_PROJECT_SELECTION_INVALID'
        }
    }

    $pageProperties = @($config.pageSelection.PSObject.Properties.Name)
    if ($pageProperties.Count -ne 2 -or @('mode','forms' | Where-Object { $_ -notin $pageProperties }).Count -ne 0) {
        throw 'WEBFORMS_PIPELINE_PAGE_SELECTION_INVALID'
    }
    $pageMode = ([string]$config.pageSelection.mode).Trim().ToLowerInvariant()
    $forms = @($config.pageSelection.forms)
    if ($pageMode -notin @('all','selected')) { throw 'WEBFORMS_PIPELINE_PAGE_SELECTION_INVALID' }
    if ($pageMode -eq 'all' -and $forms.Count -ne 0) { throw 'WEBFORMS_PIPELINE_PAGE_SELECTION_CONFLICT' }
    if ($pageMode -eq 'selected' -and ($forms.Count -lt 1 -or $forms.Count -gt 10000)) { throw 'WEBFORMS_PIPELINE_FORMS_REQUIRED' }
    foreach ($form in $forms) {
        if ($form -isnot [string] -or [string]::IsNullOrWhiteSpace($form) -or
            !$form.EndsWith('.aspx', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'WEBFORMS_PIPELINE_PAGE_SELECTION_INVALID'
        }
    }

    return [pscustomobject]@{
        ConfigPath = $file.FullName
        SourceRoot = ([string]$config.sourceRoot).Trim()
        WebFormsFolder = ([string]$config.webFormsFolder).Trim()
        BackendFolder = ([string]$config.backendFolder).Trim()
        ControlsFolder = ([string]$config.controlsFolder).Trim()
        ProjectMode = $projectMode
        SolutionRelativePath = $solutionRelativePath
        ProjectRelativePaths = @($projectRelativePaths | ForEach-Object { $_.Trim() })
        OutputRoot = ([string]$config.outputRoot).Trim()
        PageMode = $pageMode
        Forms = @($forms | ForEach-Object { $_.Trim() })
    }
}

function Property-Value([object]$Value, [string]$Name) {
    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}
