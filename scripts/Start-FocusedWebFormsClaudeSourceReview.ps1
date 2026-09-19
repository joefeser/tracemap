[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [Parameter(Mandatory = $true)][string]$SourceRoot,
    [Parameter(Mandatory = $true)][string[]]$SourceRelativePath,
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
$sourceRootPath = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\', '/')
if (!(Test-Path -LiteralPath $sourceRootPath -PathType Container)) { throw 'WEBFORMS_CLAUDE_SOURCE_ROOT_UNAVAILABLE' }
$sourceRootItem = Get-Item -LiteralPath $sourceRootPath -Force
if (($sourceRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'WEBFORMS_CLAUDE_SOURCE_ROOT_REPARSE_POINT_UNSUPPORTED' }
$requested = @($SourceRelativePath | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() } | Select-Object -Unique)
if ($requested.Count -lt 1 -or $requested.Count -gt 12) { throw 'WEBFORMS_CLAUDE_SOURCE_SELECTION_INVALID;allowedFiles=1-12' }
$allowedExtensions = @('.aspx','.ascx','.master','.ashx','.cs','.vb')
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$rootPrefix = $sourceRootPath + [IO.Path]::DirectorySeparatorChar
$selected = @()
$totalBytes = 0L
foreach ($relativePath in $requested) {
    if ([IO.Path]::IsPathRooted($relativePath) -or $relativePath.Replace('\', '/').Split('/') -contains '..') {
        throw 'WEBFORMS_CLAUDE_SOURCE_SELECTION_INVALID;relative-path-required'
    }
    $extension = [IO.Path]::GetExtension($relativePath)
    if ($allowedExtensions -notcontains $extension.ToLowerInvariant()) { throw 'WEBFORMS_CLAUDE_SOURCE_SELECTION_INVALID;unsupported-extension' }
    $fullPath = [IO.Path]::GetFullPath((Join-Path $sourceRootPath $relativePath))
    if (!$fullPath.StartsWith($rootPrefix, $comparison) -or !(Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw 'WEBFORMS_CLAUDE_SOURCE_FILE_UNAVAILABLE'
    }
    $relativeSegments = $relativePath.Replace('\', '/').Split('/', [StringSplitOptions]::RemoveEmptyEntries)
    $walkPath = $sourceRootPath
    foreach ($segment in $relativeSegments) {
        $walkPath = Join-Path $walkPath $segment
        $walkItem = Get-Item -LiteralPath $walkPath -Force
        if (($walkItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'WEBFORMS_CLAUDE_SOURCE_SELECTION_INVALID;reparse-point-unsupported'
        }
    }
    $file = Get-Item -LiteralPath $fullPath
    if ($file.Length -le 0 -or $file.Length -gt 2MB) { throw 'WEBFORMS_CLAUDE_SOURCE_FILE_LIMIT;maximumBytes=2097152' }
    $totalBytes += $file.Length
    if ($totalBytes -gt 8MB) { throw 'WEBFORMS_CLAUDE_SOURCE_TOTAL_LIMIT;maximumBytes=8388608' }
    $selected += [pscustomobject]@{ RelativePath = $relativePath.Replace('\', '/'); FullPath = $fullPath; Bytes = [long]$file.Length }
}

$stagingRoot = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-source-review-' + [Guid]::NewGuid().ToString('N'))
$assessmentPath = Join-Path $context.AgentReviewRoot 'claude-selected-source-review.md'
try {
    [IO.Directory]::CreateDirectory($stagingRoot) | Out-Null
    $manifestFiles = @()
    $ordinal = 0
    foreach ($item in $selected) {
        $ordinal++
        $alias = "source-{0:D3}{1}" -f $ordinal, [IO.Path]::GetExtension($item.RelativePath).ToLowerInvariant()
        $destination = Join-Path $stagingRoot $alias
        [IO.File]::Copy($item.FullPath, $destination, $false)
        $manifestFiles += [ordered]@{
            alias = $alias
            relativePath = $item.RelativePath
            bytes = $item.Bytes
            sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $selectionManifest = [ordered]@{
        schemaVersion = 'focused-webforms-selected-source.v1'
        rawSource = $true
        fileCount = $manifestFiles.Count
        totalBytes = $totalBytes
        files = $manifestFiles
    }
    [IO.File]::WriteAllText((Join-Path $stagingRoot 'selection-manifest.json'), ($selectionManifest | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))

    $promptPath = Join-Path $context.TraceRoot 'prompts/review-webforms-selected-source.md'
    if (!(Test-Path -LiteralPath $promptPath -PathType Leaf)) { throw 'WEBFORMS_CLAUDE_SOURCE_PROMPT_UNAVAILABLE' }
    $prompt = [IO.File]::ReadAllText($promptPath, [Text.UTF8Encoding]::new($false, $true))
    $arguments = @(
        '--permission-mode', 'plan',
        '--add-dir', $context.EvidenceDocsRoot,
        '--add-dir', $context.WorkbenchRoot,
        '--add-dir', (Split-Path $context.PacketPath -Parent),
        '--add-dir', $stagingRoot,
        '--print'
    )
    $launcher = if ($ClaudeLauncherPath) {
        Resolve-FocusedWebFormsClaudeLauncher $ClaudeLauncherPath
    } else {
        if ($null -eq (Get-Command claude -ErrorAction SilentlyContinue)) { throw 'WEBFORMS_CLAUDE_CLI_UNAVAILABLE' }
        'claude'
    }
    [IO.Directory]::CreateDirectory($context.AgentReviewRoot) | Out-Null
    Write-Output 'webformsClaudeSourceHandoff=validated'
    Write-Output "selectedSourceFiles=$($manifestFiles.Count)"
    Write-Output "selectedSourceBytes=$totalBytes"
    Write-Output 'sourceAccess=selected-files-explicitly-granted'
    Write-Output 'permissionMode=plan'
    Write-Output 'promptTransport=stdin'
    $claudeExitCode = 0
    Invoke-FocusedWebFormsClaudePrint `
        -Launcher $launcher `
        -Arguments $arguments `
        -Prompt $prompt `
        -OutputPath $assessmentPath `
        -ExitCode ([ref]$claudeExitCode)
    if ($claudeExitCode -ne 0) { throw "WEBFORMS_CLAUDE_CLI_FAILED;exitCode=$claudeExitCode" }
    if ((Get-Item -LiteralPath $assessmentPath).Length -le 0) { throw 'WEBFORMS_CLAUDE_ASSESSMENT_EMPTY' }
    Write-Output 'claudeAssessment=agent-reviews/claude-selected-source-review.md'
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
}
