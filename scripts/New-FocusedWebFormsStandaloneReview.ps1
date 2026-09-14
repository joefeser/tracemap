[CmdletBinding()]
param(
    [string]$PacketPath = '',
    [string]$OutputRoot = '',
    [string]$ConfigPath = '',
    [ValidatePattern('^$|^page-[0-9]{3,4}$')]
    [string]$PageId = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_STANDALONE_REVIEW_POWERSHELL_7_REQUIRED' }

if (!$ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.json' }
if (!$OutputRoot) {
    if (!(Test-Path -LiteralPath $ConfigPath -PathType Leaf)) { throw 'WEBFORMS_STANDALONE_REVIEW_CONFIG_UNAVAILABLE' }
    $configFile = Get-Item -LiteralPath $ConfigPath
    if ($configFile.Length -le 0 -or $configFile.Length -gt 1MB) { throw 'WEBFORMS_STANDALONE_REVIEW_CONFIG_LIMIT' }
    try { $config = [IO.File]::ReadAllText($configFile.FullName) | ConvertFrom-Json -Depth 10 }
    catch { throw 'WEBFORMS_STANDALONE_REVIEW_CONFIG_INVALID_JSON' }
    $outputRootProperty = $config.PSObject.Properties['outputRoot']
    if ($null -eq $outputRootProperty -or
        $outputRootProperty.Value -isnot [string] -or
        [string]::IsNullOrWhiteSpace([string]$outputRootProperty.Value)) {
        throw 'WEBFORMS_STANDALONE_REVIEW_OUTPUT_ROOT_REQUIRED'
    }
    $OutputRoot = ([string]$outputRootProperty.Value).Trim()
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (!(Test-Path -LiteralPath $OutputRoot -PathType Container)) { throw 'WEBFORMS_STANDALONE_REVIEW_OUTPUT_ROOT_UNAVAILABLE' }

if (!$PacketPath) {
    $latest = Get-ChildItem -LiteralPath $OutputRoot -File -Recurse -Filter 'webforms-modernization.json' |
        Where-Object { $_.FullName -match '[\\/]webforms-page-list-[^\\/]+[\\/]webforms-modernization\.json$' } |
        Sort-Object LastWriteTimeUtc, FullName -Descending |
        Select-Object -First 1
    if ($null -eq $latest) { throw 'WEBFORMS_STANDALONE_REVIEW_PACKET_UNAVAILABLE' }
    $PacketPath = $latest.FullName
}
$PacketPath = [IO.Path]::GetFullPath($PacketPath)
if (!(Test-Path -LiteralPath $PacketPath -PathType Leaf)) { throw 'WEBFORMS_STANDALONE_REVIEW_PACKET_UNAVAILABLE' }

$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$reviewRoot = Join-Path $OutputRoot "webforms-standalone-review-$stamp-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$workbench = Join-Path $reviewRoot 'workbench'
[IO.Directory]::CreateDirectory($reviewRoot) | Out-Null

try {
    & (Join-Path $PSScriptRoot 'New-FocusedWebFormsApplicationWorkbench.ps1') `
        -PacketPath $PacketPath `
        -OutputRoot $reviewRoot `
        -OutputDirectory $workbench

    $artifactNames = @(
        'index.html',
        'application-handoff.json',
        'application-outliers.shareable.html',
        'application-outliers.shareable.json'
    )
    $artifacts = @($artifactNames | ForEach-Object {
        $path = Join-Path $workbench $_
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'WEBFORMS_STANDALONE_REVIEW_WORKBENCH_INCOMPLETE' }
        $file = Get-Item -LiteralPath $path
        [ordered]@{
            path = "workbench/$($_)"
            bytes = [long]$file.Length
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            canonicalization = 'raw-file-bytes'
        }
    })
    $packetFile = Get-Item -LiteralPath $PacketPath
    $receipt = [ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        ruleId = 'diagnostic.webforms.standalone-review-receipt.v1'
        claimLevel = 'local-only'
        provenance = [ordered]@{
            generator = 'scripts/New-FocusedWebFormsStandaloneReview.ps1'
            generatorSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
            generatorCanonicalization = 'raw-file-bytes'
            inputKind = 'webforms-modernization-packet.v1'
            inputSha256 = (Get-FileHash -LiteralPath $packetFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            inputCanonicalization = 'raw-file-bytes'
        }
        run = [ordered]@{
            state = 'completed'
            createdUtc = [DateTime]::UtcNow.ToString('O')
        }
        stages = [ordered]@{
            workbench = [ordered]@{
                state = 'completed'
                artifacts = $artifacts
            }
        }
    }
    [IO.File]::WriteAllText(
        (Join-Path $reviewRoot 'run-receipt.json'),
        (($receipt | ConvertTo-Json -Depth 20) + "`n"),
        [Text.UTF8Encoding]::new($false))

    Write-Output "standaloneReviewRoot=$reviewRoot"
    Write-Output "standaloneReviewPacket=$PacketPath"
    if ($PageId) {
        & (Join-Path $PSScriptRoot 'Export-FocusedWebFormsPageShareable.ps1') `
            -ReviewRoot $reviewRoot `
            -PageId $PageId
    }
}
catch {
    if (Test-Path -LiteralPath $reviewRoot) { Remove-Item -LiteralPath $reviewRoot -Recurse -Force }
    throw
}
