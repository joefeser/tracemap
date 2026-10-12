Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot '../wrefresh.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('refresh-test-' + [guid]::NewGuid().ToString('N'))
$global:RefreshCalls = [Collections.Generic.List[object]]::new()
$global:RefreshBuildExit = 0
$global:RefreshStartExit = 0
function global:dotnet {
    $global:RefreshCalls.Add(@($args))
    $global:LASTEXITCODE = if ($args[0] -eq 'build') { $global:RefreshBuildExit } else { $global:RefreshStartExit }
}
function Expect-Failure([string]$Pattern) {
    try { & $helper $temp | Out-Null } catch {
        if ($_.Exception.Message -notlike "*$Pattern*") { throw }
        return
    }
    throw 'Expected failure'
}
try {
    $relative = 'runs/sample-' + ('a' * 32)
    $evidence = Join-Path $temp "$relative/evidence"
    New-Item -ItemType Directory -Path $evidence -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $temp 'sample') | Out-Null
    $config = Join-Path $evidence 'review-config.local.json'
    '{}' | Set-Content $config
    $projectPath = Join-Path $temp 'sample/project.config.json'
    @{schemaVersion='webforms-wizard-project.v1';configuration=@{id='sample';step='completed';run=@{relativeRoot=$relative}}} |
        ConvertTo-Json -Depth 8 | Set-Content $projectPath
    @{schemaVersion='webforms-wizard-root.v1';configuration=@{projects=@(@{id='sample';configSha256=(Get-FileHash $projectPath).Hash})}} |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $temp 'root.config.json')
    $before = (Get-FileHash $projectPath).Hash
    & $helper $temp | Out-Null
    if ($global:RefreshCalls.Count -ne 2 -or $global:RefreshCalls[0][0] -ne 'build') { throw 'Expected build then start' }
    $call = $global:RefreshCalls[1]
    if ($call[2] -cne 'start' -or $call[4] -cne $config -or $call -contains '--attest-exact-source-commit') { throw 'Wrong native invocation' }
    $first = $call[6]
    & $helper $temp | Out-Null
    if ($global:RefreshCalls[3][6] -ceq $first -or (Get-FileHash $projectPath).Hash -cne $before) { throw 'Output reused or configuration changed' }
    $global:RefreshCalls.Clear()
    $global:RefreshBuildExit = 1
    Expect-Failure 'BUILD_FAILED'
    if ($global:RefreshCalls.Count -ne 1) { throw 'Start followed failed build' }
    $global:RefreshBuildExit = 0
    $global:RefreshStartExit = 1
    Expect-Failure 'REFRESH_FAILED'
    $global:RefreshCalls.Clear()
    Add-Content $projectPath ' '
    Expect-Failure 'HASH_MISMATCH'
    if ($global:RefreshCalls.Count -ne 0) { throw 'Invalid locator executed' }
    Write-Output 'Fresh review helper tests passed.'
} finally {
    Remove-Item Function:\dotnet
    Remove-Variable RefreshCalls,RefreshBuildExit,RefreshStartExit -Scope Global
    Remove-Item -LiteralPath $temp -Recurse -Force
}
