[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^page-[0-9]{3,4}$')][string]$PageId,
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9_.]{1,128}$')][string]$HandlerName,
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9_.]{1,128}$')][string]$CreatedTypeName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_CONSTRUCTOR_HOP_POWERSHELL_7_REQUIRED' }

function Read-BoundedJson([string]$Path, [long]$MaximumBytes, [string]$ErrorCode) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw $ErrorCode }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $MaximumBytes) { throw $ErrorCode }
    try { return [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -Depth 50 }
    catch { throw $ErrorCode }
}

function Assert-ReceiptedArtifact([object[]]$Artifacts, [string]$RelativePath, [long]$MaximumBytes, [string]$ErrorPrefix) {
    $matches = @($Artifacts | Where-Object {
        ([string]$_.path).Replace('\', '/').Equals($RelativePath, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($matches.Count -ne 1) { throw "${ErrorPrefix}_NOT_RECEIPTED" }
    $path = Join-Path $root $RelativePath
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "${ErrorPrefix}_UNAVAILABLE" }
    $file = Get-Item -LiteralPath $path
    if ($file.Length -le 0 -or $file.Length -gt $MaximumBytes -or
        $file.Length -ne [long]$matches[0].bytes -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne [string]$matches[0].sha256) {
        throw "${ErrorPrefix}_MISMATCH"
    }
    return $path
}

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
$receipt = Read-BoundedJson (Join-Path $root 'run-receipt.json') 16MB 'WEBFORMS_CONSTRUCTOR_HOP_RECEIPT_UNAVAILABLE'
if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or
    $receipt.run.state -ne 'completed' -or $receipt.stages.workbench.state -ne 'completed') {
    throw 'WEBFORMS_CONSTRUCTOR_HOP_RUN_INCOMPLETE'
}
$commit = [string]$receipt.traceMap.commitSha
if ($commit -cnotmatch '^[0-9a-f]{40}$') { throw 'WEBFORMS_CONSTRUCTOR_HOP_COMMIT_UNAVAILABLE' }
if ($receipt.stages.scan.state -ne 'completed') { throw 'WEBFORMS_CONSTRUCTOR_HOP_RUN_INCOMPLETE' }
$relativePath = "workbench/$PageId.handoff.json"
$pagePath = Assert-ReceiptedArtifact @($receipt.stages.workbench.artifacts) $relativePath 128MB 'WEBFORMS_CONSTRUCTOR_HOP_PAGE'
$pageHash = (Get-FileHash -LiteralPath $pagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$page = Read-BoundedJson $pagePath 128MB 'WEBFORMS_CONSTRUCTOR_HOP_PAGE_UNAVAILABLE'
if ($page.schemaVersion -ne 'webforms-application-page-handoff.v1' -or
    $page.claimLevel -ne 'local-only' -or $page.pageId -ne $PageId) {
    throw 'WEBFORMS_CONSTRUCTOR_HOP_PAGE_INVALID'
}
$surfaceId = [string]$page.subject.surfaceId
if ([string]::IsNullOrWhiteSpace($surfaceId)) { throw 'WEBFORMS_CONSTRUCTOR_HOP_SURFACE_UNAVAILABLE' }
$applicationPath = Assert-ReceiptedArtifact @($receipt.stages.workbench.artifacts) 'workbench/application-handoff.json' 128MB 'WEBFORMS_CONSTRUCTOR_HOP_APPLICATION'
$application = Read-BoundedJson $applicationPath 128MB 'WEBFORMS_CONSTRUCTOR_HOP_APPLICATION_UNAVAILABLE'
if ($application.schemaVersion -ne 'webforms-application-handoff.v1') {
    throw 'WEBFORMS_CONSTRUCTOR_HOP_APPLICATION_INVALID'
}
$applicationPages = @($application.pages | Where-Object { [string]$_.pageId -eq $PageId })
if ($applicationPages.Count -ne 1 -or [string]$applicationPages[0].surfaceId -ne $surfaceId) {
    throw 'WEBFORMS_CONSTRUCTOR_HOP_SURFACE_MISMATCH'
}
$packetPath = Join-Path $root 'workbench/webforms-modernization.snapshot.json'
$packet = Read-BoundedJson $packetPath 512MB 'WEBFORMS_CONSTRUCTOR_HOP_PACKET_UNAVAILABLE'
if ($packet.schemaVersion -ne 'webforms-modernization-packet.v1') {
    throw 'WEBFORMS_CONSTRUCTOR_HOP_PACKET_INVALID'
}
$packetHash = (Get-FileHash -LiteralPath $packetPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($packetHash -ne [string]$application.provenance.inputSha256 -or
    [string]$packet.packetId -ne [string]$application.packet.packetId) {
    throw 'WEBFORMS_CONSTRUCTOR_HOP_PACKET_MISMATCH'
}
$index = Assert-ReceiptedArtifact @($receipt.stages.scan.artifacts) 'combined/index.sqlite' 16GB 'WEBFORMS_CONSTRUCTOR_HOP_INDEX'
$project = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/RawWebFormsEvidence.csproj'
dotnet build $project -c Release --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_CONSTRUCTOR_HOP_BUILD_FAILED' }
$dll = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/bin/Release/net10.0/RawWebFormsEvidence.dll'
Write-Output "traceMapCommitSha=$commit"
Write-Output "pageId=$PageId"
Write-Output "pageHandoffSha256=$pageHash"
Write-Output "packetSha256=$packetHash"
dotnet $dll --vb-constructor-hop-audit $index $packetPath $surfaceId $HandlerName $CreatedTypeName
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_CONSTRUCTOR_HOP_AUDIT_FAILED' }
