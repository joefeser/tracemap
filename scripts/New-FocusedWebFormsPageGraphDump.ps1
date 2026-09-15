[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^page-[0-9]{3,4}$')][string]$PriorPageId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_POWERSHELL_7_REQUIRED' }

function Read-BoundedJson([string]$Path, [long]$MaximumBytes, [string]$ErrorCode) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw $ErrorCode }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $MaximumBytes) { throw $ErrorCode }
    try { return [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -Depth 40 }
    catch { throw $ErrorCode }
}

function Get-ReceiptedApplication([string]$Root) {
    $receipt = Read-BoundedJson (Join-Path $Root 'run-receipt.json') 16MB 'WEBFORMS_PAGE_GRAPH_DUMP_RECEIPT_UNAVAILABLE'
    if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or
        $receipt.run.state -ne 'completed' -or $receipt.stages.workbench.state -ne 'completed') {
        throw 'WEBFORMS_PAGE_GRAPH_DUMP_RUN_INCOMPLETE'
    }
    $relativePath = 'workbench/application-handoff.json'
    $artifacts = @($receipt.stages.workbench.artifacts | Where-Object {
        ([string]$_.path).Replace('\', '/').Equals($relativePath, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($artifacts.Count -ne 1) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_ARTIFACT_NOT_RECEIPTED' }
    $path = Join-Path $Root $relativePath
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_APPLICATION_UNAVAILABLE' }
    $file = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($file.Length -ne [long]$artifacts[0].bytes -or $hash -ne [string]$artifacts[0].sha256) {
        throw 'WEBFORMS_PAGE_GRAPH_DUMP_ARTIFACT_MISMATCH'
    }
    $application = Read-BoundedJson $path 128MB 'WEBFORMS_PAGE_GRAPH_DUMP_APPLICATION_UNAVAILABLE'
    if ($application.schemaVersion -ne 'webforms-application-handoff.v1') { throw 'WEBFORMS_PAGE_GRAPH_DUMP_APPLICATION_INVALID' }
    return [pscustomobject]@{ Application = $application; Path = $path }
}

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_REVIEW_ROOT_UNAVAILABLE' }
$indexPath = Join-Path $root 'scan/index.sqlite'
if (!(Test-Path -LiteralPath $indexPath -PathType Leaf)) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_INDEX_UNAVAILABLE' }

$latest = @(Get-ChildItem -LiteralPath $root -Directory -Filter 'webforms-standalone-review-*' |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'run-receipt.json') -PathType Leaf } |
    Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1)
if ($latest.Count -ne 1) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_STANDALONE_REVIEW_UNAVAILABLE' }

$prior = Get-ReceiptedApplication $root
$current = Get-ReceiptedApplication $latest[0].FullName
$priorPages = @($prior.Application.pages | Where-Object { $_.pageId -eq $PriorPageId })
if ($priorPages.Count -ne 1) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_PRIOR_PAGE_UNAVAILABLE' }
$priorPath = [string]$priorPages[0].filePath
$currentPages = @($current.Application.pages | Where-Object {
    ([string]$_.filePath).Replace('\', '/').Equals($priorPath.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase)
})
if ($currentPages.Count -ne 1) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_ROUTE_MATCH_UNAVAILABLE' }
$pageId = [string]$currentPages[0].pageId
$pagePath = Join-Path $latest[0].FullName "workbench/$pageId.handoff.json"
$page = Read-BoundedJson $pagePath 128MB 'WEBFORMS_PAGE_GRAPH_DUMP_PAGE_UNAVAILABLE'
if ($page.schemaVersion -ne 'webforms-application-page-handoff.v1' -or $page.claimLevel -ne 'local-only' -or
    $page.pageId -ne $pageId -or $page.provenance.inputSha256 -ne $current.Application.provenance.inputSha256 -or
    $page.packet.scanId -ne $current.Application.packet.scanId -or $page.packet.commitSha -ne $current.Application.packet.commitSha) {
    throw 'WEBFORMS_PAGE_GRAPH_DUMP_PAGE_PROVENANCE_MISMATCH'
}

$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$outputRoot = Join-Path $latest[0].FullName "private-diagnostics/page-graph-$stamp-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$inputPath = Join-Path $outputRoot 'page-graph-input.private.json'
$outputPath = Join-Path $outputRoot 'page-graph.private.json'

try {
    $chains = @($page.eventChains | ForEach-Object {
        [ordered]@{
            chainId = [string]$_.chainId
            surfaceId = [string]$page.subject.surfaceId
            eventSourceId = [string]$_.eventSourceId
            bindingFactId = [string]$_.bindingFactId
            handlerFactId = [string]$_.handlerFactId
            handlerSymbol = $_.handlerSymbol
            terminalKind = $_.terminalKind
            traversalObservation = [ordered]@{ stopState = [string]$_.traversalStopState }
        }
    })
    $input = [ordered]@{
        schemaVersion = 'webforms-modernization-packet.v1'
        diagnosticProjection = 'private-page-all-handler-selection.v1'
        sources = @([ordered]@{ scanId = [string]$page.packet.scanId; commitSha = [string]$page.packet.commitSha })
        eventChains = $chains
    }
    [IO.File]::WriteAllText($inputPath, (($input | ConvertTo-Json -Depth 12) + "`n"), [Text.UTF8Encoding]::new($false))

    $project = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/RawWebFormsEvidence.csproj'
    $buildOutput = & dotnet build $project -c Release --nologo -v quiet 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_HELPER_BUILD_FAILED' }
    $dll = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/bin/Release/net10.0/RawWebFormsEvidence.dll'
    & dotnet $dll --page-graph-dump $indexPath $inputPath $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_FAILED' }

    $markdownPath = [IO.Path]::ChangeExtension($outputPath, '.md')
    foreach ($required in @($outputPath, $markdownPath)) {
        if (!(Test-Path -LiteralPath $required -PathType Leaf)) { throw 'WEBFORMS_PAGE_GRAPH_DUMP_INCOMPLETE' }
    }
    $artifacts = @($inputPath, $outputPath, $markdownPath) | ForEach-Object {
        $item = Get-Item -LiteralPath $_
        [ordered]@{ path = $item.Name; bytes = [long]$item.Length; sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); canonicalization = 'raw-file-bytes' }
    }
    $receipt = [ordered]@{
        schemaVersion = 'focused-webforms-page-graph-dump-receipt.v1'
        ruleId = 'diagnostic.webforms.raw-exact-call-evidence.v1'
        privacy = 'LOCAL ONLY: private paths and symbols; do not share this directory or photographs of its contents.'
        provenance = [ordered]@{
            generator = 'scripts/New-FocusedWebFormsPageGraphDump.ps1'
            generatorSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
            helperSha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()
            sourcePageHandoffSha256 = (Get-FileHash -LiteralPath $pagePath -Algorithm SHA256).Hash.ToLowerInvariant()
            sourcePacketSha256 = [string]$page.provenance.inputSha256
            scanId = [string]$page.packet.scanId
            commitSha = [string]$page.packet.commitSha
        }
        selection = [ordered]@{ priorPageId = $PriorPageId; currentPageId = $pageId; resolvedHandlerCount = @($chains | Where-Object { $_.handlerFactId }).Count }
        artifacts = $artifacts
    }
    $receiptPath = Join-Path $outputRoot 'run-receipt.json'
    [IO.File]::WriteAllText($receiptPath, (($receipt | ConvertTo-Json -Depth 12) + "`n"), [Text.UTF8Encoding]::new($false))

    Write-Output 'pageGraphDump=completed'
    Write-Output "priorPageId=$PriorPageId"
    Write-Output "currentPageId=$pageId"
    Write-Output "resolvedHandlers=$($receipt.selection.resolvedHandlerCount)"
    Write-Output "privateReport=$markdownPath"
    Write-Output "receipt=$receiptPath"
}
catch {
    if (Test-Path -LiteralPath $outputRoot) { Remove-Item -LiteralPath $outputRoot -Recurse -Force }
    throw
}
