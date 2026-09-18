$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$entry = Join-Path $scripts 'Start-FocusedWebFormsClaudeReview.ps1'
$continueEntry = Join-Path $scripts 'Continue-FocusedWebFormsClaudeReview.ps1'
$repo = Split-Path -Parent $scripts
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-claude-' + [Guid]::NewGuid().ToString('N'))

function New-Artifact([string]$Root, [string]$RelativePath, [string]$Text) {
    $path = Join-Path $Root $RelativePath
    [IO.Directory]::CreateDirectory((Split-Path -Parent $path)) | Out-Null
    [IO.File]::WriteAllText($path, $Text, [Text.UTF8Encoding]::new($false))
    $file = Get-Item -LiteralPath $path
    return [ordered]@{
        path = $RelativePath.Replace('\', '/')
        bytes = [long]$file.Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

try {
    [IO.Directory]::CreateDirectory($temp) | Out-Null
    $packet = New-Artifact $temp 'packet/webforms-modernization.json' '{}'
    $manifest = New-Artifact $temp 'evidence-docs/manifest.json' '{}'
    $recipes = New-Artifact $temp 'evidence-docs/query-recipes.json' '{}'
    $chunks = New-Artifact $temp 'evidence-docs/chunks.jsonl' "{}\n"
    $handoff = New-Artifact $temp 'workbench/application-handoff.json' '{}'
    $receipt = [ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        run = [ordered]@{ state = 'completed' }
        stages = [ordered]@{
            packet = [ordered]@{ state = 'completed'; artifacts = @($packet) }
            evidenceDocs = [ordered]@{ state = 'completed'; artifacts = @($manifest, $recipes, $chunks) }
            workbench = [ordered]@{ state = 'completed'; artifacts = @($handoff) }
        }
    }
    [IO.File]::WriteAllText((Join-Path $temp 'run-receipt.json'), ($receipt | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))

    $global:capturedClaudeArguments = @()
    function global:claude { $global:capturedClaudeArguments = @($args); $global:capturedClaudeLocation = (Get-Location).Path; $global:LASTEXITCODE = 0 }
    $output = @(& $entry -ReviewRoot $temp -TraceMapRoot $repo)
    if ($output -notcontains 'webformsClaudeHandoff=validated' -or $output -notcontains 'sourceAccess=not-granted') {
        throw 'Claude handoff did not report its validated access boundary.'
    }
    if ($global:capturedClaudeArguments -notcontains '--permission-mode' -or
        $global:capturedClaudeArguments -notcontains 'plan' -or
        $global:capturedClaudeArguments -notcontains '--session-id' -or
        @($global:capturedClaudeArguments | Where-Object { $_ -eq '--add-dir' }).Count -ne 3) {
        throw 'Claude handoff did not retain plan mode and three bounded evidence directories.'
    }
    $defaultConversationRoot = Join-Path $temp 'claude-workspace'
    if ($global:capturedClaudeLocation -ne $defaultConversationRoot) { throw 'Claude handoff did not launch from the private conversation root.' }
    $interactiveSession = [IO.File]::ReadAllText((Join-Path $temp 'agent-reviews/claude-session.json')) | ConvertFrom-Json
    if ($interactiveSession.state -ne 'ready' -or $interactiveSession.turnCount -ne 1 -or $interactiveSession.conversationRoot -ne $defaultConversationRoot) { throw 'Claude handoff did not persist resumable session state.' }

    $launcherPath = Join-Path $temp 'corporate-launcher.ps1'
    $launcherArgumentsPath = Join-Path $temp 'launcher-arguments.json'
    $launcherPromptPath = Join-Path $temp 'launcher-prompt.txt'
    $launcherLocationPath = Join-Path $temp 'launcher-location.txt'
$launcher = @'
[IO.File]::WriteAllText($env:TRACEMAP_TEST_LAUNCHER_ARGUMENTS, ($args | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($env:TRACEMAP_TEST_LAUNCHER_PROMPT, (@($input) -join "`n"), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($env:TRACEMAP_TEST_LAUNCHER_LOCATION, (Get-Location).Path, [Text.UTF8Encoding]::new($false))
Write-Output '# Complete evidence assessment'
$global:LASTEXITCODE = 0
'@
    [IO.File]::WriteAllText($launcherPath, $launcher, [Text.UTF8Encoding]::new($false))
    $env:TRACEMAP_TEST_LAUNCHER_ARGUMENTS = $launcherArgumentsPath
    $env:TRACEMAP_TEST_LAUNCHER_PROMPT = $launcherPromptPath
    $env:TRACEMAP_TEST_LAUNCHER_LOCATION = $launcherLocationPath
    $customConversationRoot = Join-Path $temp 'private-conversation'
    $launcherOutput = @(& $entry -ReviewRoot $temp -TraceMapRoot $repo -ClaudeLauncherPath $launcherPath -ConversationRoot $customConversationRoot)
    if ($launcherOutput -notcontains 'promptTransport=stdin' -or $launcherOutput -notcontains 'claudeMode=print') {
        throw 'Corporate launcher handoff did not report stdin print mode.'
    }
    $launcherArguments = @([IO.File]::ReadAllText($launcherArgumentsPath) | ConvertFrom-Json)
    if ($launcherArguments -notcontains '--print' -or
        $launcherArguments -notcontains '--permission-mode' -or
        $launcherArguments -notcontains 'plan' -or
        $launcherArguments -notcontains '--session-id' -or
        @($launcherArguments | Where-Object { $_ -eq '--add-dir' }).Count -ne 3) {
        throw 'Corporate launcher did not receive the bounded Claude arguments.'
    }
    if ([IO.File]::ReadAllText($launcherLocationPath) -ne $customConversationRoot) { throw 'Corporate launcher did not use the configured conversation root.' }
    $launcherPrompt = [IO.File]::ReadAllText($launcherPromptPath)
    if (!$launcherPrompt.Contains('# Review Web Forms modernization evidence', [StringComparison]::Ordinal)) {
        throw 'Corporate launcher did not receive the multiline prompt through stdin.'
    }
    $assessmentPath = Join-Path $temp 'agent-reviews/claude-evidence-review.md'
    $assessmentExists = Test-Path -LiteralPath $assessmentPath -PathType Leaf
    $assessmentRetained = $assessmentExists -and [IO.File]::ReadAllText($assessmentPath).Contains('# Complete evidence assessment', [StringComparison]::Ordinal)
    if (!$assessmentRetained -or $launcherOutput -notcontains 'claudeAssessment=agent-reviews/claude-evidence-review.md') {
        throw 'Corporate launcher did not retain its completed assessment under the review root.'
    }
    $sessionPath = Join-Path $temp 'agent-reviews/claude-session.json'
    $session = [IO.File]::ReadAllText($sessionPath) | ConvertFrom-Json
    $sessionTurnRoot = Join-Path $customConversationRoot "sessions/$($session.sessionId)"
    if ($session.state -ne 'ready' -or $session.turnCount -ne 1 -or $session.conversationRoot -ne $customConversationRoot -or
        !(Test-Path -LiteralPath (Join-Path $sessionTurnRoot 'turn-0001-prompt.md') -PathType Leaf) -or
        !(Test-Path -LiteralPath (Join-Path $sessionTurnRoot 'turn-0001-response.md') -PathType Leaf)) {
        throw 'Corporate launcher did not persist its session and initial turn outside the repository.'
    }
    $followupOutput = @(& $continueEntry -ReviewRoot $temp -TraceMapRoot $repo -ClaudeLauncherPath $launcherPath -Question 'Explain the highest-value remaining unknown.')
    $followupArguments = @([IO.File]::ReadAllText($launcherArgumentsPath) | ConvertFrom-Json)
    $followupPrompt = [IO.File]::ReadAllText($launcherPromptPath)
    if ($followupOutput -notcontains 'webformsClaudeContinuation=validated' -or
        $followupArguments -notcontains '--resume' -or
        $followupArguments -notcontains $session.sessionId -or
        $followupArguments -notcontains '--print' -or
        $followupPrompt -notlike '*Continue the existing Claude Code session*' -or
        $followupPrompt -notlike '*Explain the highest-value remaining unknown.*') {
        throw 'Claude follow-up did not resume the retained session with the continuation prompt.'
    }
    $resumedSession = [IO.File]::ReadAllText($sessionPath) | ConvertFrom-Json
    if ($resumedSession.turnCount -ne 2 -or
        !(Test-Path -LiteralPath (Join-Path $sessionTurnRoot 'turn-0002-prompt.md') -PathType Leaf) -or
        !(Test-Path -LiteralPath (Join-Path $sessionTurnRoot 'turn-0002-response.md') -PathType Leaf)) {
        throw 'Claude follow-up did not retain the second prompt and response.'
    }

    [IO.File]::AppendAllText((Join-Path $temp 'workbench/application-handoff.json'), 'changed')
    $failure = $null
    try { & $entry -ReviewRoot $temp -TraceMapRoot $repo | Out-Null } catch { $failure = $_.Exception.Message }
    if (!$failure.StartsWith('WEBFORMS_CLAUDE_ARTIFACT_MISMATCH', [StringComparison]::Ordinal)) {
        throw 'Claude handoff accepted an artifact that no longer matched the receipt.'
    }
    Write-Host 'PASS focused Web Forms Claude handoff'
}
finally {
    Remove-Item Function:\global:claude -ErrorAction SilentlyContinue
    Remove-Variable capturedClaudeArguments -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable capturedClaudeLocation -Scope Global -ErrorAction SilentlyContinue
    Remove-Item Env:\TRACEMAP_TEST_LAUNCHER_ARGUMENTS -ErrorAction SilentlyContinue
    Remove-Item Env:\TRACEMAP_TEST_LAUNCHER_PROMPT -ErrorAction SilentlyContinue
    Remove-Item Env:\TRACEMAP_TEST_LAUNCHER_LOCATION -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
