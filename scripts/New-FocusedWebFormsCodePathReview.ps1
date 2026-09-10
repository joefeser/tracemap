param(
    [string]$SourceRoot = '',
    [string]$InspectionPath = '',
    [string]$OutputRoot = '',
    [string]$CaseId = 'case-001',
    [ValidateRange(0, 100)]
    [int]$TriggerContextLines = 12,
    [switch]$IncludeRawSource,
    [string]$ConfigPath = ''
)

$ErrorActionPreference = 'Stop'

if (!$ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.json' }
if (!$OutputRoot) {
    . (Join-Path $PSScriptRoot 'webforms-review/FocusedWebFormsConfig.ps1')
    $OutputRoot = (Read-FocusedWebFormsConfig -ConfigPath $ConfigPath).OutputRoot
}
if (!$SourceRoot) { $SourceRoot = (Read-Host 'Private source repository root').Trim() }
if (!$SourceRoot) { throw 'CodePathReviewSourceRootUnavailable' }
if (!$InspectionPath) {
    $latest = Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'local-inspection-private') -File -Filter 'webforms-batch-inspection-*.json' |
        Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1
    if ($null -eq $latest) { throw 'CodePathReviewInspectionUnavailable' }
    $InspectionPath = $latest.FullName
}

$directory = Join-Path $OutputRoot 'local-inspection-private'
$null = New-Item -ItemType Directory -Path $directory -Force
$reviewPath = Join-Path $directory ("webforms-code-path-review-$CaseId-$([Guid]::NewGuid().ToString('N')).private.html")
$project = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/RawWebFormsEvidence.csproj'
Write-Host 'Building local review helper; reading current working-tree source.'
$buildOutput = & dotnet build $project -c Release --nologo -v quiet 2>&1
if ($LASTEXITCODE -ne 0) { throw 'CodePathReviewHelperBuildFailed; inspect the helper build locally.' }
$dll = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/bin/Release/net10.0/RawWebFormsEvidence.dll'
$reviewArguments = @($dll, '--code-path-review', $InspectionPath, $SourceRoot, $CaseId, $reviewPath, $TriggerContextLines)
if ($IncludeRawSource) { $reviewArguments += '--include-raw-source' }
& dotnet @reviewArguments
if ($LASTEXITCODE -ne 0) { throw 'CodePathReviewFailed; no review report is available.' }
Write-Host "PRIVATE local code-path review: $reviewPath"
$shareablePath = $reviewPath -replace '\.private\.html$', '.shareable.html'
Write-Host "ANONYMOUS shareable review: $shareablePath"
if ($IsWindows) { Start-Process -FilePath $reviewPath }
