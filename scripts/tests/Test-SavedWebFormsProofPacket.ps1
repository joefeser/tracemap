Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-saved-packet-test-' + [Guid]::NewGuid().ToString('N'))
$scripts = Join-Path $temp 'scripts'
$proof = Join-Path $temp 'proof'
$cli = Join-Path $temp 'src/dotnet/TraceMap.Cli/bin/Release/net10.0/tracemap.dll'
[void][IO.Directory]::CreateDirectory($scripts)
[void][IO.Directory]::CreateDirectory($proof)
[void][IO.Directory]::CreateDirectory((Split-Path $cli -Parent))
[IO.File]::Copy((Join-Path (Split-Path $PSScriptRoot -Parent) 'New-SavedWebFormsProofPacket.ps1'), (Join-Path $scripts 'New-SavedWebFormsProofPacket.ps1'))
[IO.File]::WriteAllText($cli, 'public-mocked-cli')
$index = Join-Path $proof 'combined-ilwork-30000000.sqlite'
[IO.File]::WriteAllText($index, 'public-mocked-index')
$indexSha = (Get-FileHash $index -Algorithm SHA256).Hash.ToLowerInvariant()
$handoff = Join-Path $proof 'public-handoff.json'
$input = @{ schemaVersion = 'webforms-compiled-path-handoff.v1'; ruleId = 'diagnostic.webforms.compiled-path-handoff.v1'; claimLevel = 'review-only-static-evidence'; provenance = @{ combinedIndex = 'combined-ilwork-30000000.sqlite'; sourceCommitSha = ('a' * 40); inputSha256 = @{ combinedIndex = $indexSha } } }
[IO.File]::WriteAllText($handoff, ($input | ConvertTo-Json -Depth 10))
$global:savedPacketTestInvocations = 0
function dotnet {
    $global:savedPacketTestInvocations++
    if ($args -contains 'webforms-modernization') {
        $outPosition = [Array]::IndexOf($args, '--out')
        $output = [string]$args[$outPosition + 1]
        [void][IO.Directory]::CreateDirectory($output)
        [IO.File]::WriteAllText((Join-Path $output 'webforms-modernization.json'), ((@{ schemaVersion = 'webforms-modernization-packet.v1'; coverage = 'reduced'; surfaces = @(@{ surfaceId = 'public-page' }); summary = @{ truncated = $true }; sources = @(@{ commitSha = ('a' * 40) }) } | ConvertTo-Json -Depth 10)))
        [IO.File]::WriteAllText((Join-Path $output 'webforms-modernization.md'), 'public-mocked-report')
    } elseif ($args[0] -ne 'build') { throw 'Wrapper attempted a scan, publish, or combine' }
    $global:LASTEXITCODE = 0
}
try {
    $result = @(& (Join-Path $scripts 'New-SavedWebFormsProofPacket.ps1') -ProofRoot $proof -CompiledPathHandoffPath $handoff)
    $packetLine = @($result | Where-Object { $_ -like 'savedProofPacket=*' })
    if ($packetLine.Count -ne 1 -or $global:savedPacketTestInvocations -ne 2) { throw 'Saved packet generation did not use only build and report' }
    $packetPath = $packetLine[0].Substring('savedProofPacket='.Length)
    $receiptPath = Join-Path (Split-Path (Split-Path $packetPath -Parent) -Parent) 'packet.receipt.local.json'
    $receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 20
    if ($receipt.provenance.inputSha256.combinedIndex -ne $indexSha -or
        $receipt.provenance.cliSha256 -ne (Get-FileHash $cli -Algorithm SHA256).Hash.ToLowerInvariant() -or
        $receipt.provenance.generatorSha256 -ne (Get-FileHash (Join-Path $scripts 'New-SavedWebFormsProofPacket.ps1') -Algorithm SHA256).Hash.ToLowerInvariant() -or
        @($receipt.artifacts).Count -ne 2) { throw 'Saved packet receipt lost exact generator or input provenance' }
    [IO.File]::WriteAllText($index, 'changed-index')
    $rejected = $false
    try { & (Join-Path $scripts 'New-SavedWebFormsProofPacket.ps1') -ProofRoot $proof -CompiledPathHandoffPath $handoff | Out-Null }
    catch { $rejected = $_.Exception.Message -eq 'WEBFORMS_SAVED_PACKET_INDEX_HASH_MISMATCH' }
    if (!$rejected -or $global:savedPacketTestInvocations -ne 2) { throw 'Changed saved index was admitted' }
    Write-Output 'savedWebFormsProofPacketPublicTests=passed'
}
finally { Remove-Item Function:\dotnet -ErrorAction SilentlyContinue; Remove-Variable savedPacketTestInvocations -Scope Global -ErrorAction SilentlyContinue; Remove-Item -LiteralPath $temp -Recurse -Force }
