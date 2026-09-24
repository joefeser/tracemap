[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$subject = Join-Path (Split-Path $PSScriptRoot -Parent) 'Show-FocusedWebFormsConstructorHop.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-constructor-hop-test-' + [guid]::NewGuid().ToString('N'))
try {
    $workbench = Join-Path $root 'workbench'
    New-Item -ItemType Directory -Path $workbench -Force | Out-Null
    $pagePath = Join-Path $workbench 'page-002.handoff.json'
    [IO.File]::WriteAllText($pagePath,
        '{"schemaVersion":"webforms-application-page-handoff.v1","claimLevel":"local-only","pageId":"page-002","subject":{"surfaceId":"surface-test"},"eventChains":[{"chainId":"chain-test"}]}')
    $pageFile = Get-Item -LiteralPath $pagePath
    $pageHash = (Get-FileHash -LiteralPath $pagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $packetPath = Join-Path $workbench 'webforms-modernization.snapshot.json'
    [IO.File]::WriteAllText($packetPath,
        '{"schemaVersion":"webforms-modernization-packet.v1","packetId":"packet-test"}')
    $packetHash = (Get-FileHash -LiteralPath $packetPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $workbench 'application-handoff.json'),
        (@{ schemaVersion = 'webforms-application-handoff.v1';
            provenance = @{ inputSha256 = $packetHash };
            packet = @{ packetId = 'packet-test' };
            pages = @(@{ pageId = 'page-002'; surfaceId = 'surface-test' }) } |
            ConvertTo-Json -Depth 10))
    [IO.File]::WriteAllText((Join-Path $root 'run-receipt.json'),
        (@{ schemaVersion = 'focused-webforms-review-run-receipt.v1';
            run = @{ state = 'completed' };
            stages = @{ workbench = @{ state = 'completed'; artifacts = @(@{
                path = 'workbench/page-002.handoff.json'; bytes = $pageFile.Length; sha256 = $pageHash
            }) } } } | ConvertTo-Json -Depth 10))

    $errorCode = ''
    try {
        & $subject -ReviewRoot $root -PageId page-002 -HandlerName Names_Init -CreatedTypeName ChoiceNames | Out-Null
    }
    catch { $errorCode = $_.Exception.Message }
    if ($errorCode -ne 'WEBFORMS_CONSTRUCTOR_HOP_INDEX_UNAVAILABLE') {
        throw "CONSTRUCTOR_HOP_SURFACE_MAPPING_REGRESSION:$errorCode"
    }
    Write-Output 'constructorHopSurfaceMapping=passed'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
