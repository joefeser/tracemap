[CmdletBinding()]
param([Parameter(Mandatory)][string]$ProofRoot, [Parameter(Mandatory)][string]$CompiledPathHandoffPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_SAVED_PACKET_POWERSHELL_7_REQUIRED' }
$ProofRoot = [IO.Path]::GetFullPath($ProofRoot).TrimEnd('\', '/')
$handoffFile = Get-Item -LiteralPath $CompiledPathHandoffPath
if ($handoffFile.Length -le 0 -or $handoffFile.Length -gt 16MB) { throw 'WEBFORMS_SAVED_PACKET_HANDOFF_LIMIT' }
$handoff = [IO.File]::ReadAllText($handoffFile.FullName) | ConvertFrom-Json -Depth 50
$indexName = [string]$handoff.provenance.combinedIndex
if ($handoff.schemaVersion -cne 'webforms-compiled-path-handoff.v1' -or
    $handoff.ruleId -cne 'diagnostic.webforms.compiled-path-handoff.v1' -or
    $handoff.claimLevel -cne 'review-only-static-evidence' -or
    [string]$handoff.provenance.sourceCommitSha -cnotmatch '^[0-9a-f]{40}$' -or
    $indexName -cnotmatch '^combined(?:-ilwork-[0-9]+)?\.sqlite$' -or
    [string]$handoff.provenance.inputSha256.combinedIndex -cnotmatch '^[0-9a-f]{64}$') { throw 'WEBFORMS_SAVED_PACKET_HANDOFF_INVALID' }
$indexPath = Join-Path $ProofRoot $indexName
$indexFile = Get-Item -LiteralPath $indexPath
if ($indexFile.Length -le 0 -or $indexFile.Length -gt 4GB) { throw 'WEBFORMS_SAVED_PACKET_INDEX_LIMIT' }
$indexSha = (Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($indexSha -cne [string]$handoff.provenance.inputSha256.combinedIndex) { throw 'WEBFORMS_SAVED_PACKET_INDEX_HASH_MISMATCH' }
$handoffSha = (Get-FileHash -LiteralPath $handoffFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$toolRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $toolRoot 'src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
$cli = Join-Path $toolRoot 'src/dotnet/TraceMap.Cli/bin/Release/net10.0/tracemap.dll'
$runRoot = Join-Path $ProofRoot ('focused-proof-packet-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($runRoot)
$packetDirectory = Join-Path $runRoot 'packet'
& dotnet build $project -c Release --nologo -v quiet *> (Join-Path $runRoot 'build.local.log')
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_SAVED_PACKET_BUILD_FAILED' }
$cliSha = (Get-FileHash -LiteralPath $cli -Algorithm SHA256).Hash.ToLowerInvariant()
& dotnet $cli webforms-modernization --index $indexPath --out $packetDirectory *> (Join-Path $runRoot 'packet.local.log')
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_SAVED_PACKET_GENERATION_FAILED' }
if ((Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $indexSha -or
    (Get-FileHash -LiteralPath $cli -Algorithm SHA256).Hash.ToLowerInvariant() -cne $cliSha -or
    (Get-FileHash -LiteralPath $handoffFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -cne $handoffSha) { throw 'WEBFORMS_SAVED_PACKET_INPUT_CHANGED' }
$packetPath = Join-Path $packetDirectory 'webforms-modernization.json'
$packetFile = Get-Item -LiteralPath $packetPath
if ($packetFile.Length -le 0 -or $packetFile.Length -gt 128MB) { throw 'WEBFORMS_SAVED_PACKET_OUTPUT_LIMIT' }
$packet = [IO.File]::ReadAllText($packetPath) | ConvertFrom-Json -Depth 100
if ($packet.schemaVersion -cne 'webforms-modernization-packet.v1' -or
    @($packet.sources | Where-Object { [string]$_.commitSha -ceq [string]$handoff.provenance.sourceCommitSha }).Count -ne 1) { throw 'WEBFORMS_SAVED_PACKET_SOURCE_MISMATCH' }
$boundedInputSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes("index:$indexSha`nhandoff:$handoffSha`n"))).ToLowerInvariant()
$receipt = [ordered]@{
    schemaVersion = 'webforms-saved-proof-packet-receipt.v1'; ruleId = 'diagnostic.webforms.saved-proof-packet.v1'; claimLevel = 'local-only-focused-static-projection'
    provenance = [ordered]@{ generatorSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant(); cliSha256 = $cliSha; boundedInputSha256 = $boundedInputSha; inputSha256 = [ordered]@{ combinedIndex = $indexSha; compiledHandoff = $handoffSha }; canonicalization = 'index-and-handoff-sha256-labeled-lf-v1'; sourceCommitSha = [string]$handoff.provenance.sourceCommitSha }
    artifacts = @('webforms-modernization.json', 'webforms-modernization.md' | ForEach-Object { [ordered]@{ path = "packet/$_"; sha256 = (Get-FileHash -LiteralPath (Join-Path $packetDirectory $_) -Algorithm SHA256).Hash.ToLowerInvariant() } })
    limitations = @('Only the saved focused publish-proof corpus is represented, not the full-site or multi-repository scan.', 'Default packet bounds may reduce coverage; no scan, combine, publish, runtime, or SQL execution claim is made.')
}
[IO.File]::WriteAllText((Join-Path $runRoot 'packet.receipt.local.json'), (($receipt | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
Write-Output 'savedProofPacketScope=focused-saved-corpus-not-full-site'
Write-Output "savedProofPacketCoverage=$($packet.coverage)"
Write-Output "savedProofPacketTruncated=$($packet.summary.truncated)"
Write-Output "savedProofPacketSurfaces=$(@($packet.surfaces).Count)"
Write-Output "savedProofPacket=$packetPath"
