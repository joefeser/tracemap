param(
    [string]$SourceRoot = '',
    [string]$InspectionPath = '',
    [string]$OutputRoot = '',
    [string]$CaseId = 'case-001',
    [ValidateRange(0, 100)]
    [int]$TriggerContextLines = 12,
    [switch]$IncludeRawSource
)

$ErrorActionPreference = 'Stop'

function Read-LiteralSetting([string]$Name) {
    $runner = Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.ps1'
    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$errors)
    $assignments = @($ast.FindAll({ param($node)
        $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and
        $node.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and
        $node.Left.VariablePath.UserPath -eq $Name
    }, $true))
    if ($assignments.Count -ne 1) { throw 'CodePathReviewPathSettingUnavailable' }
    $right = $assignments[0].Right
    $literal = @($right.FindAll({ param($node) $node -is [System.Management.Automation.Language.StringConstantExpressionAst] }, $true))
    if ($literal.Count -ne 1 -or $right.Extent.Text.Trim() -ne $literal[0].Extent.Text) {
        throw 'CodePathReviewPathMustBeLiteral; supply the path parameter explicitly.'
    }
    return [string]$literal[0].Value
}

if (!$OutputRoot) { $OutputRoot = Read-LiteralSetting 'OutputRoot' }
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
