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
if ($CaseId.Count -eq 0) { $selectedCases = @($availableCases) }
else { $selectedCases = @($CaseId | Sort-Object -Unique) }
if (@($selectedCases | Where-Object { $_ -notmatch '^case-[0-9]{3}$' -or $_ -notin $availableCases }).Count -ne 0) {
    throw 'CodePathReviewSetCaseUnavailable'
}
$reviewCases = @($selectedCases | ForEach-Object {
    $selectedCaseId = $_
    $case = @($inspection.cases | Where-Object { [string]$_.caseId -eq $selectedCaseId })[0]
    $bindingPaths = @($case.bindings | ForEach-Object { [string]$_.bindingLocation.filePath } |
        Where-Object { $_ } | Sort-Object -Unique)
    $surfaceIds = @($case.bindings | ForEach-Object { [string]$_.surfaceId } |
        Where-Object { $_ } | Sort-Object -Unique)
    $item = if ($bindingPaths.Count -gt 0) { $bindingPaths -join '; ' }
        elseif ($case.handlerLocation.filePath) { [string]$case.handlerLocation.filePath }
        elseif ($surfaceIds.Count -gt 0) { $surfaceIds -join '; ' }
        else { 'item-unavailable' }
    [pscustomobject]@{
        CaseId = $selectedCaseId
        Item = $item
        Handler = if ($case.handler) { [string]$case.handler } else { 'handler-unavailable' }
    }
} | Sort-Object Item, CaseId)
$itemCount = @($reviewCases | Select-Object -ExpandProperty Item -Unique).Count

$setDirectory = Join-Path $inspectionDirectory ("webforms-code-path-review-set-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))-$([Guid]::NewGuid().ToString('N').Substring(0, 8))")
$queuePath = Join-Path $setDirectory 'index.md'
$project = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/RawWebFormsEvidence.csproj'
Write-Host "Building local review helper once for $($selectedCases.Count) case(s)."
$buildOutput = & dotnet build $project -c Release --nologo -v quiet 2>&1
if ($LASTEXITCODE -ne 0) { throw 'CodePathReviewSetHelperBuildFailed; inspect the helper build locally.' }
$dll = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/bin/Release/net10.0/RawWebFormsEvidence.dll'
$null = New-Item -ItemType Directory -Path $setDirectory
$inspectionSnapshotPath = Join-Path $setDirectory 'inspection.snapshot.json'

try {
    [IO.File]::Copy($inspectionFile.FullName, $inspectionSnapshotPath, $false)

    foreach ($reviewCase in $reviewCases) {
        $reviewPath = Join-Path $setDirectory "$($reviewCase.CaseId).private.html"
        $reviewArguments = @($dll, '--code-path-review', $inspectionSnapshotPath, $SourceRoot, $reviewCase.CaseId, $reviewPath, $TriggerContextLines, 'index.html')
        & dotnet @reviewArguments
        if ($LASTEXITCODE -ne 0) { throw 'CodePathReviewSetCaseFailed' }
    }

    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# Private Web Forms review queue')
    $lines.Add('')
    $lines.Add('> PRIVATE: report links resolve to working-tree source identities and evidence. Keep this folder on the work machine.')
    $lines.Add('')
    $lines.Add("- Inspection snapshot: ``inspection.snapshot.json`` (selected from ``$($inspectionFile.Name)``)")
    $lines.Add('- Source mode: `working-tree`; Git equality is not established.')
    $lines.Add(('- Trigger context: `{0}` lines before and after each retained binding span.' -f $TriggerContextLines))
    $lines.Add('- Allowed verdicts: `unreviewed`, `expected-ui-only`, `supported-backend-present`, `backend-evidence-missing`, `binding-or-source-mismatch`, `needs-review`.')
    $lines.Add('')
    foreach ($group in @($reviewCases | Group-Object Item)) {
        $markdownItem = ([string]$group.Name).Replace('|', '&#124;').Replace('`', '&#96;').Replace("`r", ' ').Replace("`n", ' ')
        $lines.Add("## Item: ``$markdownItem``")
        $lines.Add('')
        $lines.Add('| Evidence | Handler | Private path | Anonymous path | Human verdict | Comment |')
        $lines.Add('|---|---|---|---|---|---|')
        foreach ($reviewCase in $group.Group) {
            $markdownHandler = ([string]$reviewCase.Handler).Replace('|', '&#124;').Replace('`', '&#96;').Replace("`r", ' ').Replace("`n", ' ')
            $lines.Add("| $($reviewCase.CaseId) | $markdownHandler | [$($reviewCase.CaseId).private.html]($($reviewCase.CaseId).private.html) | [$($reviewCase.CaseId).shareable.html]($($reviewCase.CaseId).shareable.html) | unreviewed |  |")
        }
        $lines.Add('')
    }
    $lines.Add('Static retained calls do not prove runtime order, branch feasibility, or source completeness. A human verdict is review metadata, not scanner evidence.')
    [IO.File]::WriteAllLines($queuePath, $lines, [Text.UTF8Encoding]::new($false))

    $htmlPath = Join-Path $setDirectory 'index.html'
    $html = [Collections.Generic.List[string]]::new()
    $html.Add('<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">')
    $html.Add('<title>Private Web Forms review index</title><style>:root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}main{max-width:1100px;margin:auto;padding:24px}.private{background:#fff1f0;border-left:5px solid #c62828;padding:12px}table{width:100%;border-collapse:collapse;background:white}th,td{padding:12px;border:1px solid #dbe2ee;text-align:left}th{background:#eaf1ff}tbody tr:not(.item) td:first-child{white-space:nowrap}.item th{background:#dce8fb;font-size:1.05rem}a{color:#1558b0}.button{display:inline-block;padding:7px 10px;background:#eaf1ff;border:1px solid #bed0ee;border-radius:6px;text-decoration:none}code{background:#edf1f7;padding:.1rem .3rem;border-radius:4px}</style></head><body><main>')
    $html.Add('<h1>Private Web Forms review index</h1>')
    $html.Add('<p class="private">PRIVATE: links include working-tree source reports. Keep this folder on the work machine.</p>')
    $html.Add("<p>Items: <code>$itemCount</code>. Handler cases: <code>$($selectedCases.Count)</code>. Trigger context: <code>$TriggerContextLines lines before and after</code>.</p>")
    $html.Add('<p><a class="button" href="index.md">Open editable verdict/comment queue</a></p>')
    $html.Add('<table><thead><tr><th>Evidence</th><th>Handler</th><th>Private review</th><th>Anonymous review</th><th>Verdict</th></tr></thead><tbody>')
    foreach ($group in @($reviewCases | Group-Object Item)) {
        $html.Add(('<tr class="item"><th colspan="5">Item: <code>{0}</code></th></tr>' -f [Net.WebUtility]::HtmlEncode([string]$group.Name)))
        foreach ($reviewCase in $group.Group) {
            $html.Add(('<tr><td><code>{0}</code></td><td><code>{1}</code></td><td><a target="_blank" rel="noopener" href="{0}.private.html">Open private report</a></td><td><a target="_blank" rel="noopener" href="{0}.shareable.html">Open anonymous report</a></td><td>unreviewed</td></tr>' -f $reviewCase.CaseId, [Net.WebUtility]::HtmlEncode([string]$reviewCase.Handler)))
        }
    }
    $html.Add('</tbody></table><p>Static retained calls do not prove runtime order, branch feasibility, or source completeness. Human verdicts are review metadata.</p></main></body></html>')
    [IO.File]::WriteAllLines($htmlPath, $html, [Text.UTF8Encoding]::new($false))
}
catch {
    if (Test-Path -LiteralPath $queuePath) { Remove-Item -LiteralPath $queuePath -Force }
    if (Test-Path -LiteralPath $setDirectory) { Remove-Item -LiteralPath $setDirectory -Recurse -Force }
    throw
}

Write-Host "codePathReviewSet=completed;cases=$($selectedCases.Count);triggerContextLines=$TriggerContextLines"
Write-Host "PRIVATE review queue: $queuePath"
Write-Host "PRIVATE review index: $htmlPath"
Write-Host 'Edit the Human verdict and Comment cells in index.md; an internal AI can read that file and follow the private report links.'
if ($IsWindows) { Start-Process -FilePath $htmlPath }
