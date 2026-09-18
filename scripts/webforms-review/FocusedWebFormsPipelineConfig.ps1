function Remove-FocusedWebFormsJsonComments {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Text)

    $result = [Text.StringBuilder]::new($Text.Length)
    $inString = $false
    $escaped = $false
    $lineComment = $false
    $blockComment = $false
    for ($index = 0; $index -lt $Text.Length; $index++) {
        $character = $Text[$index]
        $next = if ($index + 1 -lt $Text.Length) { $Text[$index + 1] } else { [char]0 }

        if ($lineComment) {
            if ($character -eq "`r" -or $character -eq "`n") {
                $lineComment = $false
                [void]$result.Append($character)
            }
            else { [void]$result.Append(' ') }
            continue
        }
        if ($blockComment) {
            if ($character -eq '*' -and $next -eq '/') {
                [void]$result.Append(' ')
                [void]$result.Append(' ')
                $index++
                $blockComment = $false
            }
            elseif ($character -eq "`r" -or $character -eq "`n") { [void]$result.Append($character) }
            else { [void]$result.Append(' ') }
            continue
        }
        if ($inString) {
            [void]$result.Append($character)
            if ($escaped) { $escaped = $false }
            elseif ($character -eq '\') { $escaped = $true }
            elseif ($character -eq '"') { $inString = $false }
            continue
        }
        if ($character -eq '"') {
            $inString = $true
            [void]$result.Append($character)
        }
        elseif ($character -eq '/' -and $next -eq '/') {
            [void]$result.Append(' ')
            [void]$result.Append(' ')
            $index++
            $lineComment = $true
        }
        elseif ($character -eq '/' -and $next -eq '*') {
            [void]$result.Append(' ')
            [void]$result.Append(' ')
            $index++
            $blockComment = $true
        }
        else { [void]$result.Append($character) }
    }
    if ($blockComment) { throw 'WEBFORMS_PIPELINE_CONFIG_UNTERMINATED_COMMENT' }
    return $result.ToString()
}

function Resolve-FocusedWebFormsPipelineConfigPath {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$ReviewRoot)

    $configRoot = Join-Path ([IO.Path]::GetFullPath($ReviewRoot)) 'config'
    $jsonc = Join-Path $configRoot 'webforms-review.jsonc'
    $json = Join-Path $configRoot 'webforms-review.json'
    $hasJsonc = Test-Path -LiteralPath $jsonc -PathType Leaf
    $hasJson = Test-Path -LiteralPath $json -PathType Leaf
    if ($hasJsonc -and $hasJson) { throw 'WEBFORMS_PIPELINE_CONFIG_CONFLICT;keep-only-one=webforms-review.jsonc-or-webforms-review.json' }
    if ($hasJsonc) { return $jsonc }
    if ($hasJson) { return $json }
    throw 'WEBFORMS_PIPELINE_CONFIG_UNAVAILABLE;expected=config/webforms-review.jsonc'
}

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

    $jsonText = Remove-FocusedWebFormsJsonComments -Text $configText

    # A single Windows path separator can either make JSON invalid (for example,
    # \w) or silently become an escape character (for example, \t). Reject odd
    # runs before parsing so both cases receive the same actionable failure.
    if ([regex]::IsMatch($jsonText, '(?<!\\)(?:\\\\)*\\(?!\\)')) {
        throw 'WEBFORMS_PIPELINE_CONFIG_UNESCAPED_BACKSLASH;use-forward-slashes-in-paths-example=C:/work/review'
    }
    if ([regex]::IsMatch($jsonText, ',\s*[}\]]')) {
        throw 'WEBFORMS_PIPELINE_CONFIG_TRAILING_COMMA;remove-the-comma-before-closing-brace-or-bracket'
    }

    try { $config = $jsonText | ConvertFrom-Json -Depth 20 }
    catch {
        throw 'WEBFORMS_PIPELINE_CONFIG_INVALID_JSON'
    }

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

function Get-FocusedWebFormsFailureGuidance {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Failure)

    $code = ($Failure -split ';', 2)[0]
    $guidance = switch ($code) {
        'WEBFORMS_PIPELINE_CONFIG_UNAVAILABLE' { 'Run Initialize-FocusedWebFormsReview.ps1, or restore config/webforms-review.jsonc.' }
        'WEBFORMS_PIPELINE_CONFIG_CONFLICT' { 'Keep one config file: prefer webforms-review.jsonc and remove or rename webforms-review.json.' }
        'WEBFORMS_PIPELINE_CONFIG_UNESCAPED_BACKSLASH' { 'Use forward slashes in JSONC paths, for example C:/work/source.' }
        'WEBFORMS_PIPELINE_CONFIG_TRAILING_COMMA' { 'Remove the comma immediately before the closing brace or bracket.' }
        'WEBFORMS_PIPELINE_CONFIG_INVALID_JSON' { 'Fix the JSONC structure. Comments are allowed; trailing commas are not.' }
        'WEBFORMS_PIPELINE_CONFIG_SHAPE_INVALID' { 'Keep exactly the generated top-level fields; initialize a fresh root to compare with the template.' }
        'WEBFORMS_PIPELINE_PROJECT_SELECTION_INVALID' { 'Set mode to solution, projects, discover, or projectless and keep the matching fields valid.' }
        'WEBFORMS_PIPELINE_SOLUTION_REQUIRED' { 'For solution mode, set solutionRelativePath to one .sln path beneath sourceRoot.' }
        'WEBFORMS_PIPELINE_SOLUTION_CONFLICT' { 'Clear solutionRelativePath unless projectSelection.mode is solution.' }
        'WEBFORMS_PIPELINE_PROJECTS_REQUIRED' { 'For projects mode, add one or more .csproj/.vbproj values to projectRelativePaths.' }
        'WEBFORMS_PIPELINE_PROJECTS_CONFLICT' { 'Clear projectRelativePaths unless projectSelection.mode is projects.' }
        'WEBFORMS_PIPELINE_PAGE_SELECTION_CONFLICT' { 'For pageSelection.mode=all, leave forms as an empty array.' }
        'WEBFORMS_PIPELINE_FORMS_REQUIRED' { 'For pageSelection.mode=selected, add one or more .aspx routes to forms.' }
        'WEBFORMS_PIPELINE_PAGE_SELECTION_INVALID' { 'Set pageSelection.mode to all or selected and use only .aspx routes in forms.' }
        'WEBFORMS_PIPELINE_OUTPUT_ROOT_MUST_EQUAL_REVIEW_ROOT' { 'Set outputRoot to the exact review root. All retained artifacts belong under that one folder.' }
        'WEBFORMS_PIPELINE_SOURCE_ROOT_UNAVAILABLE' { 'Correct sourceRoot so it names the authorized source repository directory.' }
        'WEBFORMS_PREFLIGHT_PATH_OUTSIDE_SOURCE' { 'Use a relative path beneath sourceRoot; do not use an absolute path or .. in scoped fields.' }
        'WEBFORMS_PREFLIGHT_PATH_UNAVAILABLE' { 'Correct the named field so it exists beneath sourceRoot.' }
        'WEBFORMS_PREFLIGHT_TRACEMAP_SOLUTION_UNAVAILABLE' { 'Run the command from a TraceMap checkout, or pass its root with -TraceMapRoot.' }
        'SOURCE_ROOT_NOT_GIT_ROOT' { 'Set sourceRoot to the top-level directory of the source Git checkout.' }
        'SOURCE_STATUS_UNAVAILABLE' { 'Confirm Git can read the source checkout, then rerun preflight.' }
        'SOURCE_WORKTREE_DIRTY' { 'Commit or deliberately set aside source changes so the recorded commit identifies the scanned state.' }
        'TRACEMAP_STATUS_UNAVAILABLE' { 'Confirm Git can read the TraceMap checkout, then rerun preflight.' }
        'TRACEMAP_WORKTREE_DIRTY' { 'Update and clean the TraceMap checkout before starting a receipted run.' }
        'FOLDER_SCOPE_UNAVAILABLE' { 'Correct the requested folder so it is an existing directory beneath sourceRoot.' }
        'SOLUTION_SCOPE_UNAVAILABLE' { 'Correct solutionRelativePath; use one of the bounded candidates printed in errorDetail.' }
        'PROJECT_SCOPE_UNAVAILABLE' { 'Correct projectRelativePaths; use only existing .csproj/.vbproj files beneath sourceRoot.' }
        'PROJECT_OUTSIDE_THREE_FOLDER_SCOPE' { 'Keep each explicit project beneath webFormsFolder, backendFolder, or controlsFolder.' }
        'PROJECT_DISCOVERY_SCOPE_CONFLICT' { 'For discover mode, clear solutionRelativePath and projectRelativePaths.' }
        'PROJECTLESS_SCOPE_CONFLICT' { 'For projectless mode, clear solutionRelativePath and projectRelativePaths.' }
        'WEBFORMS_PIPELINE_ARTIFACT_LIMIT' { 'If facts.ndjson completed, update TraceMap and rerun the same review root to use guarded oversized-artifact recovery.' }
        'WEBFORMS_PIPELINE_RESUME_ARTIFACT_MISMATCH' { 'Do not mix or edit retained artifacts. Restore the receipted file or initialize a new review root.' }
        'SOLUTION_SCOPE_HAS_NO_IN_SCOPE_PROJECTS' { 'Correct the three folder scopes or change mode to discover; use projectless only when no project files exist.' }
        'PROJECT_DISCOVERY_EMPTY' { 'No project files were found under the three scopes. Change mode to projectless for an old Web Site.' }
        'WEBFORMS_PIPELINE_RESUME_PROVENANCE_MISMATCH' { 'Restore the original config/source state, or initialize a new empty review root. Remove only a receipt from a pre-scan failure.' }
        default { 'Read docs/WEBFORMS_REVIEW_QUICKSTART.md and preserve the errorCode when reporting the failure.' }
    }
    return [pscustomobject]@{ Code = $code; Message = $Failure; NextAction = $guidance }
}

function Write-FocusedWebFormsFailure {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Failure)

    $item = Get-FocusedWebFormsFailureGuidance -Failure $Failure
    Write-Host 'webformsPreflight=failed'
    Write-Host "errorCode=$($item.Code)"
    Write-Host "errorDetail=$($item.Message)"
    Write-Host "nextAction=$($item.NextAction)"
}

function Property-Value([object]$Value, [string]$Name) {
    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}
