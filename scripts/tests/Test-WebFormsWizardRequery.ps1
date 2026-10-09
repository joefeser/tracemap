Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot '../wrequery.ps1'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('wizard-requery-test-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory((Join-Path $temporary 'sample')) | Out-Null
$global:WizardRequeryCalls = [Collections.Generic.List[object]]::new()
$global:WizardRequeryState = 'reports-completed-review-only'
$global:WizardRequeryExit = 0
$global:WizardRequeryWorkbench = Join-Path $temporary 'reports/index.html'
function global:dotnet {
    $global:WizardRequeryCalls.Add(@($args))
    $global:LASTEXITCODE = 0
    if ($args -contains 'status') {
        @{ schemaVersion='webforms-review-status.v1'; state=$global:WizardRequeryState;
           readerMatchesOriginalGenerator=$true; retainedArtifactsVerified=$true;
           workbenchPath=$global:WizardRequeryWorkbench } | ConvertTo-Json -Compress
    } elseif ($args -contains 'requery-handler') {
        $global:LASTEXITCODE = $global:WizardRequeryExit
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
    & $helper $temporary -Handler Page_Load | Out-Null
    if ($global:WizardRequeryCalls.Count -ne 2) { throw 'Expected status then requery.' }
    $call = $global:WizardRequeryCalls[1]
    $expectedRun = Join-Path (Join-Path $temporary ('runs/sample-' + ('a' * 32))) 'run'
    if ($call[4] -ne $expectedRun -or $call[6] -ne (Split-Path $global:WizardRequeryWorkbench -Parent) -or
        $call[8] -ne 'Page_Load') { throw 'Saved nested run or verified bundle was not used.' }
    $firstOutput = $call[10]
    & $helper $temporary -Project sample -Handler Page_Load | Out-Null
    if ($global:WizardRequeryCalls[3][10] -eq $firstOutput) { throw 'Output must be fresh.' }
    $global:WizardRequeryState = 'failed'
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'COMPLETED_STATE_NOT_ADMITTED'
    $global:WizardRequeryState = 'reports-completed-review-only'
    $global:WizardRequeryExit = 1
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'REQUERY_FAILED'
    $global:WizardRequeryExit = 0
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
    Remove-Variable WizardRequeryCalls,WizardRequeryState,WizardRequeryExit,WizardRequeryWorkbench -Scope Global
    [IO.Directory]::Delete($temporary, $true)
}
