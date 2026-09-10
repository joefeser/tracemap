param([string]$IndexPath = '', [string]$ReportPath = '', [string]$OutputRoot = '', [string]$InspectionPath = '', [switch]$CreateLocalInspection, [string]$StartingMethodName = '', [switch]$DatabaseEvidence)

$ErrorActionPreference = 'Stop'
# Reuse only literal path settings; never execute the form-list runner.
$runner = Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.ps1'
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$errors)
function Read-LiteralSetting([string]$name) {
    $assignments = @($ast.FindAll({ param($node)
        $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and
        $node.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and
        $node.Left.VariablePath.UserPath -eq $name
    }, $true))
    if ($assignments.Count -ne 1) { throw 'RawAuditPathSettingUnavailable' }
    $right = $assignments[0].Right
    $literal = @($right.FindAll({ param($node) $node -is [System.Management.Automation.Language.StringConstantExpressionAst] }, $true))
    if ($literal.Count -ne 1 -or $right.Extent.Text.Trim() -ne $literal[0].Extent.Text) {
        throw 'RawAuditPathMustBeLiteral; supply the path parameter explicitly.'
    }
    return [string]$literal[0].Value
}
if (!$IndexPath) { $IndexPath = Read-LiteralSetting 'IndexPath' }
if (!$OutputRoot) { $OutputRoot = Read-LiteralSetting 'OutputRoot' }
if (!$ReportPath -and !$DatabaseEvidence) {
    $latest = Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'webforms-page-list-*' |
        ForEach-Object { $p = Join-Path $_.FullName 'webforms-modernization.json'; if (Test-Path -LiteralPath $p) { Get-Item -LiteralPath $p } } |
        Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1
    if ($null -eq $latest) { throw 'RawAuditReportUnavailable' }
    $ReportPath = $latest.FullName
}
if (!(Test-Path -LiteralPath $IndexPath -PathType Leaf) -or (!$DatabaseEvidence -and !(Test-Path -LiteralPath $ReportPath -PathType Leaf))) {
    throw 'RawAuditInputUnavailable'
}
$project = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/RawWebFormsEvidence.csproj'
if ($CreateLocalInspection) {
    $directory = Join-Path $OutputRoot 'local-inspection-private'
    $null = New-Item -ItemType Directory -Path $directory -Force
    $InspectionPath = Join-Path $directory ('webforms-local-inspection-' + [Guid]::NewGuid().ToString('N') + '.json')
}
Write-Host 'Building diagnostic helper only; no application build, scan, or report generation.'
$buildOutput = & dotnet build $project -c Release --nologo -v quiet 2>&1
if ($LASTEXITCODE -ne 0) { throw 'RawAuditHelperBuildFailed; inspect the helper build locally.' }
$dll = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/bin/Release/net10.0/RawWebFormsEvidence.dll'
if ($DatabaseEvidence) {
    if (!$InspectionPath) {
        $latestInspection = Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'local-inspection-private') -File -Filter 'webforms-local-inspection-*.json' |
            Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1
        if ($null -eq $latestInspection) { throw 'No local inspection file was found.' }
        $InspectionPath = $latestInspection.FullName
    }
    & dotnet $dll --database-evidence $IndexPath $InspectionPath
}
elseif ($StartingMethodName -and !$InspectionPath) { throw 'Method inspection requires a local output file.' }
elseif ($StartingMethodName) { & dotnet $dll $IndexPath $ReportPath $InspectionPath $StartingMethodName }
elseif ($InspectionPath) { & dotnet $dll $IndexPath $ReportPath $InspectionPath }
else { & dotnet $dll $IndexPath $ReportPath }
if ($LASTEXITCODE -ne 0) { throw 'RawAuditFailed; no evidence conclusion is available.' }
