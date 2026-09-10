[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$InspectionPath = '',
    [string]$OutputRoot = '',
    [string[]]$CaseId = @(),
    [ValidateRange(0, 100)]
    [int]$TriggerContextLines = 12
)

Set-StrictMode -Version Latest
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
    if ($assignments.Count -ne 1) { throw 'CodePathReviewSetPathSettingUnavailable' }
    $right = $assignments[0].Right
    $literal = @($right.FindAll({ param($node) $node -is [System.Management.Automation.Language.StringConstantExpressionAst] }, $true))
    if ($literal.Count -ne 1 -or $right.Extent.Text.Trim() -ne $literal[0].Extent.Text) {
        throw 'CodePathReviewSetPathMustBeLiteral; supply the path parameter explicitly.'
    }
    return [string]$literal[0].Value
}

if (!$OutputRoot) { $OutputRoot = Read-LiteralSetting 'OutputRoot' }
if (!$SourceRoot) { $SourceRoot = (Read-Host 'Private source repository root').Trim() }
if (!$SourceRoot -or !(Test-Path -LiteralPath $SourceRoot -PathType Container)) { throw 'CodePathReviewSetSourceRootUnavailable' }
$inspectionDirectory = Join-Path $OutputRoot 'local-inspection-private'
if (!$InspectionPath) {
    $latest = Get-ChildItem -LiteralPath $inspectionDirectory -File -Filter 'webforms-batch-inspection-*.json' |
        Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1
    if ($null -eq $latest) { throw 'CodePathReviewSetInspectionUnavailable' }
    $InspectionPath = $latest.FullName
}
$inspectionFile = Get-Item -LiteralPath $InspectionPath
if ($inspectionFile.Length -gt 32MB) { throw 'CodePathReviewSetInspectionLimit' }
$inspection = [IO.File]::ReadAllText($inspectionFile.FullName) | ConvertFrom-Json -Depth 64
if ($inspection.schemaVersion -ne 'webforms-batch-inspection.v1') { throw 'CodePathReviewSetSchemaMismatch' }
$availableCases = @($inspection.cases | ForEach-Object { [string]$_.caseId } | Sort-Object -Unique)
if ($availableCases.Count -lt 1 -or $availableCases.Count -gt 64) { throw 'CodePathReviewSetCaseLimit' }
$selectedCases = if ($CaseId.Count -eq 0) { $availableCases } else { @($CaseId | Sort-Object -Unique) }
if (@($selectedCases | Where-Object { $_ -notmatch '^case-[0-9]{3}$' -or $_ -notin $availableCases }).Count -ne 0) {
    throw 'CodePathReviewSetCaseUnavailable'
}

$setDirectory = Join-Path $inspectionDirectory ("webforms-code-path-review-set-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))-$([Guid]::NewGuid().ToString('N').Substring(0, 8))")
$queuePath = Join-Path $setDirectory 'index.md'
$project = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/RawWebFormsEvidence.csproj'
Write-Host "Building local review helper once for $($selectedCases.Count) case(s)."
$buildOutput = & dotnet build $project -c Release --nologo -v quiet 2>&1
if ($LASTEXITCODE -ne 0) { throw 'CodePathReviewSetHelperBuildFailed; inspect the helper build locally.' }
$dll = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/bin/Release/net10.0/RawWebFormsEvidence.dll'
$null = New-Item -ItemType Directory -Path $setDirectory

try {
    foreach ($selectedCase in $selectedCases) {
        $reviewPath = Join-Path $setDirectory "$selectedCase.private.html"
        & dotnet $dll --code-path-review $inspectionFile.FullName $SourceRoot $selectedCase $reviewPath $TriggerContextLines
        if ($LASTEXITCODE -ne 0) { throw 'CodePathReviewSetCaseFailed' }
    }

    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# Private Web Forms review queue')
    $lines.Add('')
    $lines.Add('> PRIVATE: report links resolve to working-tree source identities and evidence. Keep this folder on the work machine.')
    $lines.Add('')
    $lines.Add("- Inspection: ``$($inspectionFile.Name)``")
    $lines.Add('- Source mode: `working-tree`; Git equality is not established.')
    $lines.Add(('- Trigger context: `{0}` lines before and after each retained binding span.' -f $TriggerContextLines))
    $lines.Add('- Allowed verdicts: `unreviewed`, `expected-ui-only`, `supported-backend-present`, `backend-evidence-missing`, `binding-or-source-mismatch`, `needs-review`.')
    $lines.Add('')
    $lines.Add('| Evidence | Private path | Anonymous path | Human verdict | Comment |')
    $lines.Add('|---|---|---|---|---|')
    foreach ($selectedCase in $selectedCases) {
        $lines.Add("| $selectedCase | [$selectedCase.private.html]($selectedCase.private.html) | [$selectedCase.shareable.html]($selectedCase.shareable.html) | unreviewed |  |")
    }
    $lines.Add('')
    $lines.Add('Static retained calls do not prove runtime order, branch feasibility, or source completeness. A human verdict is review metadata, not scanner evidence.')
    [IO.File]::WriteAllLines($queuePath, $lines, [Text.UTF8Encoding]::new($false))
}
catch {
    if (Test-Path -LiteralPath $queuePath) { Remove-Item -LiteralPath $queuePath -Force }
    if (Test-Path -LiteralPath $setDirectory) { Remove-Item -LiteralPath $setDirectory -Recurse -Force }
    throw
}

Write-Host "codePathReviewSet=completed;cases=$($selectedCases.Count);triggerContextLines=$TriggerContextLines"
Write-Host "PRIVATE review queue: $queuePath"
Write-Host 'Edit the Human verdict and Comment cells in index.md; an internal AI can read that file and follow the private report links.'
if ($IsWindows) { Start-Process -FilePath $queuePath }
