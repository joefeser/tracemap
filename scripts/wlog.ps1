[CmdletBinding()]
param([ValidateRange(1, 50)][int]$Tail = 15, [string]$SearchRoot = ([IO.Path]::GetTempPath()))

# Read only the latest focused packet-generation failure log. Never rerun it.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$logs = foreach ($proof in @(Get-ChildItem -LiteralPath $SearchRoot -Directory -Filter 'tracemap-existing-publish-*')) {
    foreach ($run in @(Get-ChildItem -LiteralPath $proof.FullName -Directory -Filter 'focused-proof-packet-*')) {
        $path = Join-Path $run.FullName 'packet.local.log'
        if (Test-Path -LiteralPath $path -PathType Leaf) { Get-Item -LiteralPath $path }
    }
}
$latest = @($logs | Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1)
if ($latest.Count -ne 1) { throw 'WEBFORMS_PACKET_LOG_UNAVAILABLE' }
Write-Output 'savedPacketLog=latest;read-only;may-contain-private-paths'
Get-Content -LiteralPath $latest[0].FullName -Tail $Tail
