[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$ClaudeLauncherPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_CLAUDE_POWERSHELL_7_REQUIRED' }

function Read-BoundedJson([string]$Path, [long]$MaximumBytes, [string]$Failure) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw $Failure }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $MaximumBytes) { throw $Failure }
    try { return [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -Depth 30 }
    catch { throw $Failure }
}

function Assert-ReceiptedArtifact([object]$Receipt, [string]$StageName, [string]$Root, [string]$RelativePath) {
    $normalized = $RelativePath.Replace('\', '/')
    $matches = @($Receipt.stages.PSObject.Properties[$StageName].Value.artifacts | Where-Object {
        ([string]$_.path).Replace('\', '/').Equals($normalized, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($matches.Count -ne 1) { throw "WEBFORMS_CLAUDE_ARTIFACT_NOT_RECEIPTED;path=$normalized" }
    $path = Join-Path $Root $RelativePath
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "WEBFORMS_CLAUDE_INPUT_UNAVAILABLE;path=$path" }
    $file = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($file.Length -ne [long]$matches[0].bytes -or $hash -ne [string]$matches[0].sha256) {
        throw "WEBFORMS_CLAUDE_ARTIFACT_MISMATCH;path=$normalized"
    }
}

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
$traceRoot = [IO.Path]::GetFullPath($TraceMapRoot).TrimEnd('\', '/')
$receiptPath = Join-Path $root 'run-receipt.json'
$packetPath = Join-Path $root 'packet/webforms-modernization.json'
$evidenceDocsRoot = Join-Path $root 'evidence-docs'
$workbenchRoot = Join-Path $root 'workbench'
$promptPath = Join-Path $traceRoot 'prompts/review-webforms-modernization-evidence.md'
$receipt = Read-BoundedJson $receiptPath 16MB 'WEBFORMS_CLAUDE_RECEIPT_UNAVAILABLE'
if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or $receipt.run.state -ne 'completed') {
    throw 'WEBFORMS_CLAUDE_RUN_INCOMPLETE'
}
foreach ($stageName in @('packet','evidenceDocs','workbench')) {
    if ($receipt.stages.PSObject.Properties[$stageName].Value.state -ne 'completed') {
        throw "WEBFORMS_CLAUDE_STAGE_INCOMPLETE;stage=$stageName"
    }
}
Assert-ReceiptedArtifact $receipt 'packet' $root 'packet/webforms-modernization.json'
Assert-ReceiptedArtifact $receipt 'evidenceDocs' $root 'evidence-docs/manifest.json'
Assert-ReceiptedArtifact $receipt 'evidenceDocs' $root 'evidence-docs/query-recipes.json'
Assert-ReceiptedArtifact $receipt 'evidenceDocs' $root 'evidence-docs/chunks.jsonl'
Assert-ReceiptedArtifact $receipt 'workbench' $root 'workbench/application-handoff.json'
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
    $launcherInput = $ClaudeLauncherPath.Trim()
    if ($IsWindows -and $launcherInput -match '^[A-Za-z]:/') {
        $launcherInput = $launcherInput.Replace('/', '\')
    }
    if (!(Test-Path -LiteralPath $launcherInput -PathType Leaf)) { throw 'WEBFORMS_CLAUDE_LAUNCHER_UNAVAILABLE;use-resolve-path-with-a-native-windows-path' }
    $launcher = (Resolve-Path -LiteralPath $launcherInput).Path
    Write-Output 'promptTransport=stdin'
    Write-Output 'claudeMode=print'
    $prompt | & $launcher @claudeArguments --print
}
else {
    if ($null -eq (Get-Command claude -ErrorAction SilentlyContinue)) { throw 'WEBFORMS_CLAUDE_CLI_UNAVAILABLE' }
    Write-Output 'promptTransport=argument'
    Write-Output 'claudeMode=interactive'
    & claude @claudeArguments $prompt
}
if ($LASTEXITCODE -ne 0) { throw "WEBFORMS_CLAUDE_CLI_FAILED;exitCode=$LASTEXITCODE" }
