[CmdletBinding()]
param(
    [string]$PacketPath = '',
    [string]$OutputRoot = '',
    [string]$ConfigPath = '',
    [string]$CompiledPathHandoffPath = '',
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
    $candidates = @(Get-ChildItem -LiteralPath $OutputRoot -File -Recurse -Filter 'webforms-modernization.json' |
        Where-Object { $_.FullName -match '[\\/]webforms-page-list-[^\\/]+[\\/]webforms-modernization\.json$' } |
        Sort-Object LastWriteTimeUtc, FullName -Descending)
    $latest = $candidates | Select-Object -First 1
    if ($CompiledPathHandoffPath) {
        if (!(Test-Path -LiteralPath $CompiledPathHandoffPath -PathType Leaf)) { throw 'WEBFORMS_STANDALONE_REVIEW_COMPILED_HANDOFF_UNAVAILABLE' }
        $handoffFile = Get-Item -LiteralPath $CompiledPathHandoffPath
        if ($handoffFile.Length -le 0 -or $handoffFile.Length -gt 16MB) { throw 'WEBFORMS_STANDALONE_REVIEW_COMPILED_HANDOFF_LIMIT' }
        $handoff = [IO.File]::ReadAllText($handoffFile.FullName) | ConvertFrom-Json -Depth 50
        $sourceCommit = [string]$handoff.provenance.sourceCommitSha
        if ($sourceCommit -cnotmatch '^[0-9a-f]{40}$') { throw 'WEBFORMS_STANDALONE_REVIEW_COMPILED_COMMIT_INVALID' }
        $repositoryProperty = $handoff.provenance.PSObject.Properties['packetRepositoryId']
        if ($null -eq $repositoryProperty -or [string]$repositoryProperty.Value -cnotmatch '^repository-[0-9a-f]{24}$') {
            throw 'WEBFORMS_STANDALONE_REVIEW_COMPILED_REPOSITORY_INVALID'
        }
        $sourceRepository = [string]$repositoryProperty.Value
        if ($candidates.Count -gt 64) { throw 'WEBFORMS_STANDALONE_REVIEW_PACKET_DISCOVERY_LIMIT' }
        $latest = $null
        $discoveryBytes = 0L
        foreach ($candidate in $candidates) {
            $discoveryBytes += $candidate.Length
            if ($candidate.Length -le 0 -or $candidate.Length -gt 128MB -or $discoveryBytes -gt 512MB) { throw 'WEBFORMS_STANDALONE_REVIEW_PACKET_DISCOVERY_LIMIT' }
            $candidatePacket = [IO.File]::ReadAllText($candidate.FullName) | ConvertFrom-Json -Depth 100
            if ($candidatePacket.schemaVersion -cne 'webforms-modernization-packet.v1') { continue }
            if (@($candidatePacket.sources | Where-Object {
                $identity = $_.PSObject.Properties['repositoryId']
                [string]$_.commitSha -ceq $sourceCommit -and $null -ne $identity -and
                    [string]$identity.Value -ceq $sourceRepository
            }).Count -eq 1) {
                $latest = $candidate
                break
            }
        }
        Write-Output "compiledPathAttachmentSavedPacketCandidates=$($candidates.Count)"
        Write-Output "compiledPathAttachmentCompatiblePacket=$($null -ne $latest)"
        if ($null -eq $latest) { throw 'WEBFORMS_STANDALONE_REVIEW_COMPATIBLE_PACKET_UNAVAILABLE' }
    }
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
        -OutputDirectory $workbench `
        -CompiledPathHandoffPath $CompiledPathHandoffPath

    $artifactNames = @(
        'index.html'
        'application-handoff.json'
        'application-outliers.shareable.html'
        'application-outliers.shareable.json'
        Get-ChildItem -LiteralPath $workbench -File -Filter '*.handoff.json' | ForEach-Object { $_.Name }
    )
    if ($CompiledPathHandoffPath) { $artifactNames += @('compiled-paths.local.html', 'compiled-paths.local.json') }
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
            supplementalCompiledPathKind = if ($CompiledPathHandoffPath) { 'webforms-compiled-path-handoff.v1' } else { 'not-supplied' }
            supplementalCompiledPathSha256 = if ($CompiledPathHandoffPath) { (Get-FileHash -LiteralPath $CompiledPathHandoffPath -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
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
