$ErrorActionPreference = 'Stop'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-log-test-' + [Guid]::NewGuid().ToString('N'))
$folder = Join-Path $temp 'tracemap-existing-publish-public/focused-proof-packet-public'
[void][IO.Directory]::CreateDirectory($folder)
$log = Join-Path $folder 'packet.local.log'
[IO.File]::WriteAllText($log, "public-line-one`npublic-error-two`n")
$before = (Get-FileHash -LiteralPath $log).Hash
try {
    $result = @(& (Join-Path (Split-Path $PSScriptRoot -Parent) 'wlog.ps1') -SearchRoot $temp -Tail 1)
    if ($result[-1] -ne 'public-error-two' -or $result.Count -ne 2 -or (Get-FileHash -LiteralPath $log).Hash -ne $before) { throw 'Log helper did not preserve read-only tail behavior' }
    Write-Output 'webFormsPacketLogPublicTests=passed'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
