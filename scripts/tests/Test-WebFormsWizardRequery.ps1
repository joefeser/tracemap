Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot '../wrequery.ps1'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('wizard-requery-test-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory((Join-Path $temporary 'sample')) | Out-Null
$global:WizardRequeryCalls = [Collections.Generic.List[object]]::new()
$global:WizardRequeryState = 'reports-completed-review-only'
$global:WizardRequeryExit = 0
$global:WizardRequeryOriginalReader = $true
$global:WizardRequeryCause = $null
$global:WizardRequeryWorkbench = Join-Path $temporary 'reports/index.html'
function global:dotnet {
    $global:WizardRequeryCalls.Add(@($args))
    $global:LASTEXITCODE = 0
    if ($args -contains 'status') {
        @{ schemaVersion='webforms-review-status.v1'; state=$global:WizardRequeryState;
           readerMatchesOriginalGenerator=$global:WizardRequeryOriginalReader; retainedArtifactsVerified=$true;
           workbenchPath=$global:WizardRequeryWorkbench } | ConvertTo-Json -Compress
    } elseif ($args -contains 'requery-handler') {
        $global:LASTEXITCODE = $global:WizardRequeryExit
        if ($global:WizardRequeryExit -eq 0) {
            $destination = $args[10]
            [IO.Directory]::CreateDirectory($destination) | Out-Null
            if ($args -contains 'method-graph') {
                @{schemaVersion='webforms-handler-requery.v1';truncated=$false;pathEnumerationPerformed=$false;
                    primaryReport='method-graph.local.html';artifacts=@()} | ConvertTo-Json |
                    Set-Content (Join-Path $destination 'handler-requery.local.json')
                return
            }
            $handoffPath = Join-Path $destination 'compiled-paths.handoff.local.json'
            @{schemaVersion='webforms-compiled-grouped-handoff.v1';header=@{gaps=@(
                @{gapKind='TruncatedByLimit';reason='depth';cutoffCause=$global:WizardRequeryCause;filePath='synthetic/Page.vb';startLine=12;
                  nodeId='node-1';ruleId='synthetic.rule';evidenceTier='Tier4Unknown';message='Depth limit reached.'})};
                variants=@(@{nodeReferences=@('endpoint')}); nodes=@{endpoint=@{
                    nodeId='endpoint-id';surfaceKind='database-api';surfaceName='SqlCommand.ExecuteScalar';
                    commandBinding=@{commandTextFromPath=@{state='unresolved-operand';origin=@{kind='call-result';identity='private-sql-marker'};
                        ruleId='synthetic.command';evidenceTier='Tier3SyntaxOrTextual';steps=@();gaps=@('IlCommandOperandValueUnresolved')}}}}} |
                ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $handoffPath
            @{schemaVersion='webforms-handler-requery.v1';truncated=$true;artifacts=@(@{
                relativePath='compiled-paths.handoff.local.json';bytes=(Get-Item $handoffPath).Length;
                sha256=(Get-FileHash $handoffPath).Hash.ToLowerInvariant()})} |
                ConvertTo-Json -Depth 8 | Set-Content (Join-Path $destination 'handler-requery.local.json')
        }
    } else { throw 'Unexpected dotnet operation (no builds or scans allowed).' }
}
function Save-Fixture([string]$Relative = ('runs/sample-' + ('a' * 32))) {
    $project = @{schemaVersion='webforms-wizard-project.v1'; configuration=@{
        id='sample'; step='completed'; run=@{relativeRoot=$Relative}}}
    $path = Join-Path $temporary 'sample/project.config.json'
    $project | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path
    @{schemaVersion='webforms-wizard-root.v1'; configuration=@{projects=@(@{
        id='sample'; configSha256=(Get-FileHash $path -Algorithm SHA256).Hash})}} |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $temporary 'root.config.json')
}
function Expect-Failure([scriptblock]$Action, [string]$Message) {
    try { & $Action | Out-Null } catch {
        if ($_.Exception.Message -notlike "*$Message*") { throw }
        return
    }
    throw "Expected failure: $Message"
}
try {
    Save-Fixture
    $output = @(& $helper $temporary -Handler Page_Load)
    if (($output -join "`n") -notmatch 'depth: 1 retained gaps' -or
        ($output -join "`n") -notmatch 'location=synthetic/Page.vb; line=12') { throw 'Missing bounded depth details.' }
    if (($output -join "`n") -notmatch 'IlCommandOperandValueUnresolved' -or
        ($output -join "`n") -notmatch 'commandTypeFromPath: unavailable' -or
        ($output -join "`n") -match 'private-sql-marker') { throw 'Command diagnostic missing or leaked value.' }
    if ($global:WizardRequeryCalls.Count -ne 2) { throw 'Expected status then requery.' }
    $call = $global:WizardRequeryCalls[1]
    $expectedRun = Join-Path (Join-Path $temporary ('runs/sample-' + ('a' * 32))) 'run'
    if ($call[4] -ne $expectedRun -or $call[6] -ne (Split-Path $global:WizardRequeryWorkbench -Parent) -or
        $call[8] -ne 'Page_Load') { throw 'Saved nested run or verified bundle was not used.' }
    $firstOutput = $call[10]
    . (Join-Path $PSScriptRoot '../WebFormsRequeryDiagnostics.ps1')
    Add-Content (Join-Path $firstOutput 'compiled-paths.handoff.local.json') ' '
    Expect-Failure { Write-WebFormsTruncationSummary $firstOutput } 'SUMMARY_HANDOFF_CHANGED'
    & $helper $temporary -Project sample -Handler Page_Load | Out-Null
    if ($global:WizardRequeryCalls[3][10] -eq $firstOutput) { throw 'Output must be fresh.' }
    $global:WizardRequeryCause = 'candidate-identity-roundtrip'
    $display = @(& $helper $temporary -Handler Page_Load)
    if (($display -join "`n") -notmatch 'identity round trips \(not application recursion\): 1 retained gaps') { throw 'Identity diagnostics not separated.' }
    $global:WizardRequeryCause = 'terminal-route-not-found-within-depth-bound'
    $display = @(& $helper $temporary -Handler Page_Load)
    if (($display -join "`n") -notmatch 'bounded terminal-route uncertainty: 1 retained gaps') { throw 'Bounded uncertainty not separated.' }
    $global:WizardRequeryCause = $null
    $graphDisplay = @(& $helper $temporary -Handler Page_Load -MethodGraph)
    $graphCall = $global:WizardRequeryCalls[$global:WizardRequeryCalls.Count - 1]
    if ($graphCall[-2] -ne '--view' -or $graphCall[-1] -ne 'method-graph' -or
        ($graphDisplay -join "`n") -notmatch 'method-graph.local.html' -or
        ($graphDisplay -join "`n") -notmatch 'Terminal path enumeration was not run') { throw 'Graph mode was not forwarded or labeled.' }
    Expect-Failure { & $helper $temporary -MethodGraph -InspectLatest } 'cannot be combined'
    $beforeInspect = $global:WizardRequeryCalls.Count
    $inspection = @(& $helper $temporary -InspectLatest)
    if ($global:WizardRequeryCalls.Count -ne $beforeInspect + 1 -or
        ($inspection -join "`n") -notmatch 'Inspecting latest completed report') { throw 'Inspection must only call status, not traverse.' }
    $global:WizardRequeryState = 'failed'
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'COMPLETED_STATE_NOT_ADMITTED'
    $global:WizardRequeryState = 'reports-completed-review-only'
    $global:WizardRequeryOriginalReader = $false
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'COMPLETED_STATE_NOT_ADMITTED'
    $updated = @(& $helper $temporary -Handler Page_Load -AllowUpdatedReader)
    if (($updated -join "`n") -notmatch 'Updated reader explicitly allowed') { throw 'Missing reader provenance notice.' }
    $global:WizardRequeryState = 'failed'
    Expect-Failure { & $helper $temporary -Handler Page_Load -AllowUpdatedReader } 'COMPLETED_STATE_NOT_ADMITTED'
    $global:WizardRequeryState = 'reports-completed-review-only'
    $global:WizardRequeryOriginalReader = $true
    $global:WizardRequeryExit = 1
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'REQUERY_FAILED'
    $global:WizardRequeryExit = 0
    Expect-Failure { & $helper $temporary -Handler Page_Load -LatestRefresh } 'REFRESH_SELECTION_UNAVAILABLE'
    $refresh = Join-Path $temporary ('refresh-sample-' + ('b' * 32))
    [IO.Directory]::CreateDirectory((Join-Path $refresh 'run')) | Out-Null
    $global:WizardRequeryCalls.Clear()
    & $helper $temporary -Handler Page_Load -LatestRefresh | Out-Null
    if ($global:WizardRequeryCalls[0][4] -cne (Join-Path $refresh 'run') -or
        $global:WizardRequeryCalls[1][4] -cne (Join-Path $refresh 'run')) { throw 'Refresh query used old run.' }
    $global:WizardRequeryState = 'failed'
    $global:WizardRequeryCalls.Clear()
    Expect-Failure { & $helper $temporary -Handler Page_Load -LatestRefresh } 'COMPLETED_STATE_NOT_ADMITTED'
    if ($global:WizardRequeryCalls.Count -ne 1) { throw 'Failed refresh fell back or queried.' }
    $global:WizardRequeryState = 'reports-completed-review-only'
    Expect-Failure { & $helper $temporary -Project absent -Handler Page_Load } 'PROJECT_NOT_FOUND'
    Expect-Failure { & $helper $temporary -Handler "Page_Load`n" } 'HANDLER_INVALID'
    Save-Fixture '../outside'
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'RUN_LOCATOR_INVALID'
    Save-Fixture
    Add-Content (Join-Path $temporary 'sample/project.config.json') ' '
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'PROJECT_HASH_MISMATCH'
    Write-Output 'Web Forms wizard requery helper tests passed.'
} finally {
    Remove-Item Function:\dotnet
    Remove-Variable WizardRequeryCalls,WizardRequeryState,WizardRequeryExit,WizardRequeryWorkbench,WizardRequeryOriginalReader,WizardRequeryCause -Scope Global
    [IO.Directory]::Delete($temporary, $true)
}
