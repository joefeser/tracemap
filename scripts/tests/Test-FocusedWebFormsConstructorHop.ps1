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
    $applicationPath = Join-Path $workbench 'application-handoff.json'
    [IO.File]::WriteAllText($applicationPath,
        (@{ schemaVersion = 'webforms-application-handoff.v1';
            provenance = @{ inputSha256 = $packetHash };
            packet = @{ packetId = 'packet-test' };
            pages = @(@{ pageId = 'page-002'; surfaceId = 'surface-test' }) } |
            ConvertTo-Json -Depth 10))
    $applicationFile = Get-Item -LiteralPath $applicationPath
    $applicationHash = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $receiptPath = Join-Path $root 'run-receipt.json'
    $receipt = @{ schemaVersion = 'focused-webforms-review-run-receipt.v1';
        traceMap = @{ commitSha = ('a' * 40) };
        run = @{ state = 'completed' };
        stages = @{
            scan = @{ state = 'completed'; artifacts = @() };
            workbench = @{ state = 'completed'; artifacts = @(
                @{ path = 'workbench/page-002.handoff.json'; bytes = $pageFile.Length; sha256 = $pageHash },
                @{ path = 'workbench/application-handoff.json'; bytes = $applicationFile.Length; sha256 = $applicationHash }
            ) }
        }
    }
    [IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 10))

    $errorCode = ''
    try {
        & $subject -ReviewRoot $root -PageId page-002 -HandlerName Names_Init -CreatedTypeName ChoiceNames | Out-Null
    }
    catch { $errorCode = $_.Exception.Message }
    if ($errorCode -ne 'WEBFORMS_CONSTRUCTOR_HOP_INDEX_NOT_RECEIPTED') {
        throw "CONSTRUCTOR_HOP_SURFACE_MAPPING_REGRESSION:$errorCode"
    }
    Write-Output 'constructorHopSurfaceMapping=passed'

    [IO.File]::AppendAllText($applicationPath, ' ')
    $errorCode = ''
    try { & $subject -ReviewRoot $root -PageId page-002 -HandlerName Names_Init -CreatedTypeName ChoiceNames | Out-Null }
    catch { $errorCode = $_.Exception.Message }
    if ($errorCode -ne 'WEBFORMS_CONSTRUCTOR_HOP_APPLICATION_MISMATCH') {
        throw "CONSTRUCTOR_HOP_APPLICATION_RECEIPT_REGRESSION:$errorCode"
    }
    Write-Output 'constructorHopApplicationReceipt=passed'
    [IO.File]::WriteAllText($applicationPath,
        (@{ schemaVersion = 'webforms-application-handoff.v1';
            provenance = @{ inputSha256 = $packetHash };
            packet = @{ packetId = 'packet-test' };
            pages = @(@{ pageId = 'page-002'; surfaceId = 'surface-test' }) } |
            ConvertTo-Json -Depth 10))

    $indexDirectory = Join-Path $root 'combined'
    New-Item -ItemType Directory -Path $indexDirectory -Force | Out-Null
    $indexPath = Join-Path $indexDirectory 'index.sqlite'
    [IO.File]::WriteAllText($indexPath, 'synthetic-index')
    $indexFile = Get-Item -LiteralPath $indexPath
    $indexHash = (Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $receipt.stages.scan.artifacts = @(@{ path = 'combined/index.sqlite'; bytes = $indexFile.Length; sha256 = $indexHash })
    [IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 10))
    [IO.File]::AppendAllText($indexPath, ' ')
    $errorCode = ''
    try { & $subject -ReviewRoot $root -PageId page-002 -HandlerName Names_Init -CreatedTypeName ChoiceNames | Out-Null }
    catch { $errorCode = $_.Exception.Message }
    if ($errorCode -ne 'WEBFORMS_CONSTRUCTOR_HOP_INDEX_MISMATCH') {
        throw "CONSTRUCTOR_HOP_INDEX_RECEIPT_REGRESSION:$errorCode"
    }
    Write-Output 'constructorHopIndexReceipt=passed'

    [IO.File]::WriteAllText($indexPath, 'synthetic-index')
    $captured = [Collections.Generic.List[string]]::new()
    $errorCode = ''
    try {
        & $subject -ReviewRoot $root -PageId page-002 -HandlerName Names_Init -CreatedTypeName ChoiceNames |
            ForEach-Object { $captured.Add([string]$_) }
    }
    catch { $errorCode = $_.Exception.Message }
    if ($errorCode -ne 'WEBFORMS_CONSTRUCTOR_HOP_AUDIT_FAILED' -or
        -not $captured.Contains("traceMapCommitSha=$('a' * 40)")) {
        throw "CONSTRUCTOR_HOP_RECEIPTED_COMMIT_REGRESSION:$errorCode"
    }
    Write-Output 'constructorHopReceiptedCommit=passed'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
