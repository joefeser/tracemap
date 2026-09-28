Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script = Join-Path (Split-Path $PSScriptRoot -Parent) 'wview.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-view-test-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
try {
    [IO.File]::Copy($script, (Join-Path $temp 'wview.ps1'))
    [IO.File]::WriteAllText((Join-Path $temp 'Replay-ExistingWebFormsCompiledPathReviews.ps1'), @'
param([string]$ProofRoot, [switch]$RecheckApi)
if ($RecheckApi) { Write-Output 'publicRecheckApi=forwarded' }
if ($ProofRoot -eq 'missing') { Write-Output 'compiledReplaySqlProjection=existing'; return }
Write-Output 'compiledReplayProofRoot=public-proof'
Write-Output 'compiledPathReviewHandoff=public-api.json'
Write-Output 'compiledPathReviewHandoff=public-empty-sql.json'
'@)
    [IO.File]::WriteAllText((Join-Path $temp 'New-FocusedWebFormsStandaloneReview.ps1'), @'
param([string]$CompiledPathHandoffPath, [string]$PacketPath, [string]$OutputRoot, [string]$ConfigPath)
if ($CompiledPathHandoffPath -ne 'public-api.json' -or $PacketPath -ne 'public-packet.json' -or $OutputRoot -ne 'public-output' -or $ConfigPath -ne 'public-config.json') { throw 'Wrong selected handoff or forwarded arguments' }
Write-Output 'publicView=passed'
'@)
    $result = @(& (Join-Path $temp 'wview.ps1') -ProofRoot public-proof -PacketPath public-packet.json -OutputRoot public-output -ConfigPath public-config.json)
    if (@($result | Where-Object { $_ -eq 'publicView=passed' }).Count -ne 1) { throw 'View did not create the standalone review' }
    $rejected = $false
    try { & (Join-Path $temp 'wview.ps1') -ProofRoot missing | Out-Null }
    catch { $rejected = $_.Exception.Message -eq 'WEBFORMS_VIEW_HANDOFF_UNAVAILABLE' }
    if (!$rejected) { throw 'View continued without a selected handoff' }
    [IO.File]::WriteAllText((Join-Path $temp 'New-SavedWebFormsProofPacket.ps1'), @'
param([string]$ProofRoot, [string]$CompiledPathHandoffPath)
if ($ProofRoot -ne 'public-proof' -or $CompiledPathHandoffPath -ne 'public-api.json') { throw 'Wrong saved proof input' }
Write-Output 'savedProofPacket=public-packet.json'
'@)
    $savedResult = @(& (Join-Path $temp 'wview.ps1') -FromSavedProof -RecheckApi -OutputRoot public-output -ConfigPath public-config.json)
    if (@($savedResult | Where-Object { $_ -eq 'publicView=passed' }).Count -ne 1 -or
        $savedResult -cnotcontains 'publicRecheckApi=forwarded') { throw 'View did not use the saved proof packet or forward explicit recheck' }
    Write-Output 'webFormsViewPublicTests=passed'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
