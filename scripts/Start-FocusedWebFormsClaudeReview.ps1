[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$ClaudeLauncherPath = '',
    [string]$ConversationRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_CLAUDE_POWERSHELL_7_REQUIRED' }
$commonPath = Join-Path $PSScriptRoot 'webforms-review/ClaudeReview.Common.ps1'
if (!(Test-Path -LiteralPath $commonPath -PathType Leaf)) { throw 'WEBFORMS_CLAUDE_COMMON_UNAVAILABLE' }
. $commonPath
$context = Get-FocusedWebFormsClaudeEvidenceContext $ReviewRoot $TraceMapRoot
$root = $context.Root
$traceRoot = $context.TraceRoot
$packetPath = $context.PacketPath
$evidenceDocsRoot = $context.EvidenceDocsRoot
$workbenchRoot = $context.WorkbenchRoot
$agentReviewRoot = $context.AgentReviewRoot
$assessmentPath = Join-Path $agentReviewRoot 'claude-evidence-review.md'
$conversationRootPath = if ([string]::IsNullOrWhiteSpace($ConversationRoot)) {
    Join-Path $root 'claude-workspace'
} else {
    [IO.Path]::GetFullPath($ConversationRoot).TrimEnd('\', '/')
}
[IO.Directory]::CreateDirectory($agentReviewRoot) | Out-Null
[IO.Directory]::CreateDirectory($conversationRootPath) | Out-Null
$sessionId = [Guid]::NewGuid().ToString()
$sessionRelativeRoot = "sessions/$sessionId"
$turnRoot = Join-Path $conversationRootPath $sessionRelativeRoot
[IO.Directory]::CreateDirectory($turnRoot) | Out-Null
$sessionStatePath = Join-Path $agentReviewRoot 'claude-session.json'
$promptPath = Join-Path $traceRoot 'prompts/review-webforms-modernization-evidence.md'
if (!(Test-Path -LiteralPath $promptPath -PathType Leaf)) { throw "WEBFORMS_CLAUDE_INPUT_UNAVAILABLE;path=$promptPath" }
$prompt = [IO.File]::ReadAllText($promptPath, [Text.UTF8Encoding]::new($false, $true))
[IO.File]::WriteAllText((Join-Path $turnRoot 'turn-0001-prompt.md'), $prompt, [Text.UTF8Encoding]::new($false))
$sessionState = [ordered]@{
    schemaVersion = 'focused-webforms-claude-session.v1'
    state = 'starting'
    sessionId = $sessionId
    conversationRoot = $conversationRootPath
    reviewRoot = $root
    turnRoot = $sessionRelativeRoot
    initialPrompt = "$sessionRelativeRoot/turn-0001-prompt.md"
    initialResponse = if ($ClaudeLauncherPath) { "$sessionRelativeRoot/turn-0001-response.md" } else { $null }
    lastPrompt = "$sessionRelativeRoot/turn-0001-prompt.md"
    lastResponse = if ($ClaudeLauncherPath) { "$sessionRelativeRoot/turn-0001-response.md" } else { $null }
    turnCount = 0
}
[IO.File]::WriteAllText($sessionStatePath, (($sessionState | ConvertTo-Json -Depth 6) + "`n"), [Text.UTF8Encoding]::new($false))
$claudeArguments = @(
    '--permission-mode', 'plan',
    '--session-id', $sessionId,
    '--add-dir', $evidenceDocsRoot,
    '--add-dir', $workbenchRoot,
    '--add-dir', (Split-Path $packetPath -Parent)
)
Write-Output 'webformsClaudeHandoff=validated'
Write-Output "reviewRoot=$root"
Write-Output "conversationRoot=$conversationRootPath"
Write-Output "claudeSessionId=$sessionId"
Write-Output 'permissionMode=plan'
Write-Output 'sourceAccess=not-granted'
$launcher = if ($ClaudeLauncherPath) { Resolve-FocusedWebFormsClaudeLauncher $ClaudeLauncherPath } else { $null }
Push-Location -LiteralPath $conversationRootPath
try {
    if ($ClaudeLauncherPath) {
        Write-Output 'promptTransport=stdin'
        Write-Output 'claudeMode=print'
        $claudeExitCode = 0
        Invoke-FocusedWebFormsClaudePrint `
            -Launcher $launcher `
            -Arguments @($claudeArguments + '--print') `
            -Prompt $prompt `
            -OutputPath $assessmentPath `
            -ExitCode ([ref]$claudeExitCode)
    }
    else {
        if ($null -eq (Get-Command claude -ErrorAction SilentlyContinue)) { throw 'WEBFORMS_CLAUDE_CLI_UNAVAILABLE' }
        Write-Output 'promptTransport=argument'
        Write-Output 'claudeMode=interactive'
        & claude @claudeArguments $prompt
        $claudeExitCode = $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
if ($claudeExitCode -ne 0) { throw "WEBFORMS_CLAUDE_CLI_FAILED;exitCode=$claudeExitCode" }
if ($ClaudeLauncherPath) {
    $assessment = Get-Item -LiteralPath $assessmentPath
    if ($assessment.Length -le 0) { throw 'WEBFORMS_CLAUDE_ASSESSMENT_EMPTY' }
    [IO.File]::Copy($assessmentPath, (Join-Path $turnRoot 'turn-0001-response.md'), $true)
    Write-Output 'claudeAssessment=agent-reviews/claude-evidence-review.md'
}
$sessionState.state = 'ready'
$sessionState.turnCount = 1
[IO.File]::WriteAllText($sessionStatePath, (($sessionState | ConvertTo-Json -Depth 6) + "`n"), [Text.UTF8Encoding]::new($false))
Write-Output 'claudeSession=agent-reviews/claude-session.json'
