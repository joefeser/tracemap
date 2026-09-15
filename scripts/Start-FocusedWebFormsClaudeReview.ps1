[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
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
$root = $context.Root
$traceRoot = $context.TraceRoot
$packetPath = $context.PacketPath
$evidenceDocsRoot = $context.EvidenceDocsRoot
$workbenchRoot = $context.WorkbenchRoot
$agentReviewRoot = $context.AgentReviewRoot
$assessmentPath = Join-Path $agentReviewRoot 'claude-evidence-review.md'
$promptPath = Join-Path $traceRoot 'prompts/review-webforms-modernization-evidence.md'
if (!(Test-Path -LiteralPath $promptPath -PathType Leaf)) { throw "WEBFORMS_CLAUDE_INPUT_UNAVAILABLE;path=$promptPath" }
$prompt = [IO.File]::ReadAllText($promptPath, [Text.UTF8Encoding]::new($false, $true))
$claudeArguments = @(
    '--permission-mode', 'plan',
    '--add-dir', $evidenceDocsRoot,
    '--add-dir', $workbenchRoot,
    '--add-dir', (Split-Path $packetPath -Parent)
)
Write-Output 'webformsClaudeHandoff=validated'
Write-Output "reviewRoot=$root"
Write-Output 'permissionMode=plan'
Write-Output 'sourceAccess=not-granted'
if ($ClaudeLauncherPath) {
    $launcher = Resolve-FocusedWebFormsClaudeLauncher $ClaudeLauncherPath
    Write-Output 'promptTransport=stdin'
    Write-Output 'claudeMode=print'
    [IO.Directory]::CreateDirectory($agentReviewRoot) | Out-Null
    $prompt | & $launcher @claudeArguments --print | Tee-Object -FilePath $assessmentPath
}
else {
    if ($null -eq (Get-Command claude -ErrorAction SilentlyContinue)) { throw 'WEBFORMS_CLAUDE_CLI_UNAVAILABLE' }
    Write-Output 'promptTransport=argument'
    Write-Output 'claudeMode=interactive'
    & claude @claudeArguments $prompt
}
if ($LASTEXITCODE -ne 0) { throw "WEBFORMS_CLAUDE_CLI_FAILED;exitCode=$LASTEXITCODE" }
if ($ClaudeLauncherPath) {
    $assessment = Get-Item -LiteralPath $assessmentPath
    if ($assessment.Length -le 0) { throw 'WEBFORMS_CLAUDE_ASSESSMENT_EMPTY' }
    Write-Output 'claudeAssessment=agent-reviews/claude-evidence-review.md'
}
