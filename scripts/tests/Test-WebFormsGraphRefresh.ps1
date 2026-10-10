Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('graph-refresh-test-' + [guid]::NewGuid().ToString('N'))
$global:GraphRefreshCalls = [Collections.Generic.List[object]]::new()
$global:GraphRefreshFailure = ''
try {
    New-Item -ItemType Directory -Path $temp | Out-Null
    Copy-Item (Join-Path $PSScriptRoot '../wgraph-refresh.ps1') $temp
    foreach ($name in @('wrefresh','wrequery','wgraph-share')) {
        @'
param($Root,$Project,$Handler,$Method,[switch]$LatestRefresh,[switch]$MethodGraph,[switch]$PrivateMap)
$stage = [IO.Path]::GetFileNameWithoutExtension($PSCommandPath)
$global:GraphRefreshCalls.Add(@{stage=$stage;root=$Root;project=$Project;handler=$Handler;method=$Method;latest=[bool]$LatestRefresh;graph=[bool]$MethodGraph;map=[bool]$PrivateMap})
if ($global:GraphRefreshFailure -eq $stage) { throw 'expected-stage-failure' }
'@ | Set-Content (Join-Path $temp "$name.ps1")
    }
    $helper = Join-Path $temp 'wgraph-refresh.ps1'
    & $helper $temp -Project public -Handler Public_Click -Method PublicLookup | Out-Null
    if (($global:GraphRefreshCalls.stage -join ',') -ne 'wrefresh,wrequery,wgraph-share') { throw 'Wrong stage order.' }
    if (!$global:GraphRefreshCalls[1].latest -or !$global:GraphRefreshCalls[1].graph -or
        !$global:GraphRefreshCalls[2].map -or $global:GraphRefreshCalls[1].handler -ne 'Public_Click' -or
        $global:GraphRefreshCalls[2].method -ne 'PublicLookup') { throw 'Wrong query or export arguments.' }
    foreach ($stage in @('wrefresh','wrequery')) {
        $global:GraphRefreshCalls.Clear()
        $global:GraphRefreshFailure = $stage
        $failed = $false
        try { & $helper $temp -Handler Public_Click -Method PublicLookup | Out-Null } catch { $failed = $true }
        if (!$failed -or $global:GraphRefreshCalls[-1].stage -ne $stage) { throw 'Failure did not stop the batch.' }
    }
    Write-Output 'Graph refresh batch helper tests passed.'
} finally {
    Remove-Variable GraphRefreshCalls,GraphRefreshFailure -Scope Global
    Remove-Item -LiteralPath $temp -Recurse -Force
}
