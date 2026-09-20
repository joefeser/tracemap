[CmdletBinding(DefaultParameterSetName = 'Question')]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [Parameter(Mandatory = $true, ParameterSetName = 'Question')][string]$Question,
    [Parameter(Mandatory = $true, ParameterSetName = 'PromptPath')][string]$PromptPath,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$ClaudeLauncherPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_CLAUDE_POWERSHELL_7_REQUIRED' }
$commonPath = Join-Path $PSScriptRoot 'webforms-review/ClaudeReview.Common.ps1'
if (!(Test-Path -LiteralPath $commonPath -PathType Leaf)) { throw 'WEBFORMS_CLAUDE_COMMON_UNAVAILABLE' }
. $commonPath
$context = Get-FocusedWebFormsClaudeEvidenceContext $ReviewRoot $TraceMapRoot
$sessionStatePath = Join-Path $context.AgentReviewRoot 'claude-session.json'
$session = Read-FocusedWebFormsBoundedJson $sessionStatePath 1MB 'WEBFORMS_CLAUDE_SESSION_UNAVAILABLE'
if ($session.schemaVersion -ne 'focused-webforms-claude-session.v1' -or $session.state -ne 'ready') {
    throw 'WEBFORMS_CLAUDE_SESSION_INVALID'
}
$parsedSessionId = [Guid]::Empty
if (![Guid]::TryParse([string]$session.sessionId, [ref]$parsedSessionId)) { throw 'WEBFORMS_CLAUDE_SESSION_INVALID' }
$conversationRoot = [IO.Path]::GetFullPath([string]$session.conversationRoot).TrimEnd('\', '/')
if (!(Test-Path -LiteralPath $conversationRoot -PathType Container)) { throw 'WEBFORMS_CLAUDE_CONVERSATION_ROOT_UNAVAILABLE' }
$questionText = if ($PSCmdlet.ParameterSetName -eq 'PromptPath') {
    $questionFile = Get-Item -LiteralPath $PromptPath -ErrorAction Stop
    if ($questionFile.Length -le 0 -or $questionFile.Length -gt 1MB) { throw 'WEBFORMS_CLAUDE_FOLLOWUP_PROMPT_INVALID' }
    [IO.File]::ReadAllText($questionFile.FullName, [Text.UTF8Encoding]::new($false, $true))
} else {
    $Question
}
if ([string]::IsNullOrWhiteSpace($questionText) -or [Text.Encoding]::UTF8.GetByteCount($questionText) -gt 1MB) {
    throw 'WEBFORMS_CLAUDE_FOLLOWUP_PROMPT_INVALID'
}
$basePromptPath = Join-Path $context.TraceRoot 'prompts/continue-webforms-modernization-review.md'
if (!(Test-Path -LiteralPath $basePromptPath -PathType Leaf)) { throw "WEBFORMS_CLAUDE_INPUT_UNAVAILABLE;path=$basePromptPath" }
$basePrompt = [IO.File]::ReadAllText($basePromptPath, [Text.UTF8Encoding]::new($false, $true))
$followupPrompt = $basePrompt.TrimEnd() + "`n`n" + $questionText.Trim() + "`n"
$turnNumber = [int]$session.turnCount + 1
$turnName = 'turn-{0:d4}' -f $turnNumber
$expectedTurnRoot = "sessions/$($session.sessionId)"
if ([string]$session.turnRoot -ne $expectedTurnRoot) { throw 'WEBFORMS_CLAUDE_SESSION_INVALID' }
$turnRoot = Join-Path $conversationRoot $expectedTurnRoot
[IO.Directory]::CreateDirectory($turnRoot) | Out-Null
$turnPromptPath = Join-Path $turnRoot "$turnName-prompt.md"
$turnResponsePath = Join-Path $turnRoot "$turnName-response.md"
[IO.File]::WriteAllText($turnPromptPath, $followupPrompt, [Text.UTF8Encoding]::new($false))
$claudeArguments = @(
    '--permission-mode', 'plan',
    '--resume', [string]$session.sessionId,
    '--add-dir', $context.EvidenceDocsRoot,
    '--add-dir', $context.WorkbenchRoot,
    '--add-dir', (Split-Path $context.PacketPath -Parent)
)
Write-Output 'webformsClaudeContinuation=validated'
Write-Output "reviewRoot=$($context.Root)"
Write-Output "conversationRoot=$conversationRoot"
Write-Output "claudeSessionId=$($session.sessionId)"
Write-Output "turn=$turnNumber"
Write-Output 'permissionMode=plan'
Write-Output 'sourceAccess=not-granted'
$launcher = if ($ClaudeLauncherPath) { Resolve-FocusedWebFormsClaudeLauncher $ClaudeLauncherPath } else { $null }
Push-Location -LiteralPath $conversationRoot
try {
    if ($ClaudeLauncherPath) {
        Write-Output 'promptTransport=stdin'
        Write-Output 'claudeMode=print'
        $claudeExitCode = 0
        Invoke-FocusedWebFormsClaudePrint `
            -Launcher $launcher `
            -Arguments @($claudeArguments + '--print') `
            -Prompt $followupPrompt `
            -OutputPath $turnResponsePath `
            -ExitCode ([ref]$claudeExitCode)
    }
    else {
        if ($null -eq (Get-Command claude -ErrorAction SilentlyContinue)) { throw 'WEBFORMS_CLAUDE_CLI_UNAVAILABLE' }
        Write-Output 'promptTransport=argument'
        Write-Output 'claudeMode=interactive'
        & claude @claudeArguments $followupPrompt
        $claudeExitCode = $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
if ($claudeExitCode -ne 0) { throw "WEBFORMS_CLAUDE_CLI_FAILED;exitCode=$claudeExitCode" }
if ($ClaudeLauncherPath) {
    $response = Get-Item -LiteralPath $turnResponsePath
    if ($response.Length -le 0) { throw 'WEBFORMS_CLAUDE_ASSESSMENT_EMPTY' }
}
$session.turnCount = $turnNumber
$session.lastPrompt = "$expectedTurnRoot/$turnName-prompt.md"
$session.lastResponse = if ($ClaudeLauncherPath) { "$expectedTurnRoot/$turnName-response.md" } else { $null }
[IO.File]::WriteAllText($sessionStatePath, (($session | ConvertTo-Json -Depth 6) + "`n"), [Text.UTF8Encoding]::new($false))
Write-Output "claudeFollowupPrompt=$turnPromptPath"
if ($ClaudeLauncherPath) { Write-Output "claudeFollowupResponse=$turnResponsePath" }
