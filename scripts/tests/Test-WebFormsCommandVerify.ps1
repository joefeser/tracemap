# Public orchestration tests; the fake native command is not private acceptance.
$ErrorActionPreference = 'Stop'
$folder = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-command-verify-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($folder)
$scripts = Split-Path $PSScriptRoot -Parent
foreach ($name in @('wcmdverify.ps1','wsqlroute.ps1')) { [IO.File]::Copy((Join-Path $scripts $name), (Join-Path $folder $name)) }
$fakeVerify = @'
param($ReviewRoot,$ProofRoot,$PublishedRoot,$SourceBase,$OutputRoot,[switch]$Run)
if (!$Run) { throw 'Test requires fresh run' }
[void][IO.Directory]::CreateDirectory((Join-Path $OutputRoot 'tool'))
[IO.File]::WriteAllText((Join-Path $OutputRoot 'tool/tracemap.dll'), 'public-fake-tool')
[void][IO.Directory]::CreateDirectory((Join-Path $OutputRoot 'review/run'))
if ($global:commandTestState -eq 'recovery') { throw 'WEBFORMS_VERIFY_RUN_FAILED;preserve-output-for-diagnostics' }
'@
[IO.File]::WriteAllText((Join-Path $folder 'wverify.ps1'), $fakeVerify)
$global:commandTestOutput = $null
$global:commandTestState = 'completed'
$global:commandTestChangedTool = $false
$global:commandTestRecoveryFails = $false
$global:commandTestRecoveryCalls = 0
$global:commandTestRequeryCalls = 0
function global:dotnet {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    $global:LASTEXITCODE = 0
    if ($Arguments[0] -cne (Join-Path $global:commandTestOutput 'tool/tracemap.dll')) { throw 'Unpinned tool used' }
    if ($Arguments[2] -ceq 'recover-reports') {
        $global:commandTestRecoveryCalls++
        if ($global:commandTestRecoveryFails) { $global:LASTEXITCODE = 1; return }
        if (Test-Path $Arguments[6]) { throw 'Recovery output reused' }
        [void][IO.Directory]::CreateDirectory($Arguments[6])
        return
    }
    if ($Arguments[2] -ceq 'requery-handler') {
        $global:commandTestRequeryCalls++
        if ($Arguments[8] -cne 'Selected' -or $Arguments[12] -cne 'DbDataAdapter.Fill' -or $Arguments[14] -cne 'compiled-il') { throw 'Wrong requery scope' }
        if (Test-Path $Arguments[10]) { throw 'Handler output reused' }
        [void][IO.Directory]::CreateDirectory($Arguments[10])
        $root = @{ nodeId = 'handler'; displayName = 'Synthetic.Selected(Object,EventArgs)' }
        $fill = @{ nodeId = 'fill'; surfaceKind = 'database-api'; surfaceName = 'DbDataAdapter.Fill'; displayName = 'Fill' }
        [IO.File]::WriteAllText((Join-Path $Arguments[10] 'compiled-paths.handoff.local.json'),
            (ConvertTo-Json -Depth 20 @{ query = @{}; paths = @(@{ nodes = @($root,$fill); edges = @() }) }))
        return
    }
    if ($Arguments[2] -cne 'status') { throw 'Unexpected native operation' }
    $report = Join-Path $global:commandTestOutput 'review/run/reports/synthetic'
    [void][IO.Directory]::CreateDirectory((Join-Path $report 'compiled'))
    $root = @{ nodeId = 'handler'; displayName = 'Synthetic.Selected(Object,EventArgs)' }
    $fill = @{ nodeId = 'fill'; surfaceKind = 'database-api'; surfaceName = 'DbDataAdapter.Fill'; displayName = 'Fill' }
    [IO.File]::WriteAllText((Join-Path $report 'compiled/compiled-paths.handoff.local.json'),
        (ConvertTo-Json -Depth 20 @{ query = @{}; paths = @(@{ nodes = @($root,$fill); edges = @() }) }))
    if ($global:commandTestChangedTool) { [IO.File]::WriteAllText($Arguments[0], 'changed-fake-tool') }
    ConvertTo-Json @{ schemaVersion = 'webforms-review-status.v1'; state = $(if ($global:commandTestState -eq 'completed') { 'reports-completed-review-only' } elseif ($global:commandTestState -eq 'recovery') { 'reports-failed' } else { 'scan-failed' });
        readerMatchesOriginalGenerator = $true; retainedArtifactsVerified = $true; workbenchPath = (Join-Path $report 'index.html'); checkpointGaps = @($(if ($global:commandTestState -eq 'recovery') { 'WEBFORMS_EVIDENCE_NODE_LIMIT' })) }
}
try {
    $helper = Join-Path $folder 'wcmdverify.ps1'
    $global:commandTestOutput = Join-Path $folder 'fresh-one'
    $result = @(& $helper -ReviewRoot $folder -ProofRoot $folder -PublishedRoot $folder -SourceBase Site `
        -OutputRoot $global:commandTestOutput -Handler Selected)
    if ($result -notcontains 'commandValidation.scope=retained-handler-filter;original-query-scope-preserved' -or
        !(Test-Path (Join-Path $global:commandTestOutput 'handler-command-evidence.local.html'))) { throw 'Completed workflow failed' }
    try { & $helper -ReviewRoot $folder -ProofRoot $folder -PublishedRoot $folder -SourceBase Site `
        -OutputRoot $global:commandTestOutput -Handler Selected; throw 'Existing output accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_COMMAND_OUTPUT_EXISTS;preserved-unchanged') { throw } }
    $global:commandTestOutput = Join-Path $folder 'failed-state'; $global:commandTestState = 'failed'
    try { & $helper -ReviewRoot $folder -ProofRoot $folder -PublishedRoot $folder -SourceBase Site `
        -OutputRoot $global:commandTestOutput -Handler Selected; throw 'Failed scan accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_COMMAND_RETAINED_STATE_NOT_ADMITTED;partial-output-preserved') { throw } }
    $global:commandTestOutput = Join-Path $folder 'changed-tool'; $global:commandTestState = 'completed'; $global:commandTestChangedTool = $true
    try { & $helper -ReviewRoot $folder -ProofRoot $folder -PublishedRoot $folder -SourceBase Site `
        -OutputRoot $global:commandTestOutput -Handler Selected; throw 'Changed tool accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_COMMAND_TOOL_CHANGED;outputs-preserved-not-admitted') { throw } }
    $global:commandTestOutput = Join-Path $folder 'recovery'; $global:commandTestState = 'recovery'; $global:commandTestChangedTool = $false
    $result = @(& $helper -ReviewRoot $folder -ProofRoot $folder -PublishedRoot $folder -SourceBase Site `
        -OutputRoot $global:commandTestOutput -Handler Selected)
    if ($result -notcontains 'commandValidation.start=failed;checking-retained-state;failure-not-upgraded' -or
        $result -notcontains 'commandValidation.scope=new-compiled-il-handler-fill-query;root-attachment-not-il-proof' -or
        $global:commandTestRecoveryCalls -ne 1 -or $global:commandTestRequeryCalls -ne 1 -or
        $global:commandTestState -ne 'recovery') { throw 'Recovery workflow failed or original failure upgraded' }
    $global:commandTestOutput = Join-Path $folder 'recovery-fails'; $global:commandTestRecoveryFails = $true
    try { & $helper -ReviewRoot $folder -ProofRoot $folder -PublishedRoot $folder -SourceBase Site `
        -OutputRoot $global:commandTestOutput -Handler Selected; throw 'Failed recovery accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_COMMAND_RECOVERY_FAILED;original-failure-preserved') { throw } }
    if ($global:commandTestRequeryCalls -ne 1) { throw 'Requery started after failed recovery' }
    Write-Output 'webFormsCommandVerifyPublicTests=passed;orchestration-only;not-private-acceptance'
} finally {
    Remove-Item Function:\dotnet
    [IO.Directory]::Delete($folder, $true)
}
