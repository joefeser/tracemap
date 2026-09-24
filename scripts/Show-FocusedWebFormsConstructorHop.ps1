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

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
$receipt = Read-BoundedJson (Join-Path $root 'run-receipt.json') 16MB 'WEBFORMS_CONSTRUCTOR_HOP_RECEIPT_UNAVAILABLE'
if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or
    $receipt.run.state -ne 'completed' -or $receipt.stages.workbench.state -ne 'completed') {
    throw 'WEBFORMS_CONSTRUCTOR_HOP_RUN_INCOMPLETE'
}
$relativePath = "workbench/$PageId.handoff.json"
$artifacts = @($receipt.stages.workbench.artifacts | Where-Object {
    ([string]$_.path).Replace('\', '/').Equals($relativePath, [StringComparison]::OrdinalIgnoreCase)
})
if ($artifacts.Count -ne 1) { throw 'WEBFORMS_CONSTRUCTOR_HOP_PAGE_NOT_RECEIPTED' }
$pagePath = Join-Path $root $relativePath
$pageFile = Get-Item -LiteralPath $pagePath -ErrorAction Stop
$pageHash = (Get-FileHash -LiteralPath $pagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($pageFile.Length -ne [long]$artifacts[0].bytes -or $pageHash -ne [string]$artifacts[0].sha256) {
    throw 'WEBFORMS_CONSTRUCTOR_HOP_PAGE_MISMATCH'
}
$page = Read-BoundedJson $pagePath 128MB 'WEBFORMS_CONSTRUCTOR_HOP_PAGE_UNAVAILABLE'
if ($page.schemaVersion -ne 'webforms-application-page-handoff.v1' -or
    $page.claimLevel -ne 'local-only' -or $page.pageId -ne $PageId) {
    throw 'WEBFORMS_CONSTRUCTOR_HOP_PAGE_INVALID'
}
$surfaceIds = @($page.eventChains | ForEach-Object { [string]$_.surfaceId } |
    Where-Object { $_ } | Sort-Object -Unique)
if ($surfaceIds.Count -ne 1) { throw 'WEBFORMS_CONSTRUCTOR_HOP_SURFACE_UNAVAILABLE' }
$applicationPath = Join-Path $root 'workbench/application-handoff.json'
$application = Read-BoundedJson $applicationPath 128MB 'WEBFORMS_CONSTRUCTOR_HOP_APPLICATION_UNAVAILABLE'
if ($application.schemaVersion -ne 'webforms-application-handoff.v1') {
    throw 'WEBFORMS_CONSTRUCTOR_HOP_APPLICATION_INVALID'
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
$index = Join-Path $root 'combined/index.sqlite'
if (!(Test-Path -LiteralPath $index -PathType Leaf)) { throw 'WEBFORMS_CONSTRUCTOR_HOP_INDEX_UNAVAILABLE' }
$project = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/RawWebFormsEvidence.csproj'
dotnet build $project -c Release --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_CONSTRUCTOR_HOP_BUILD_FAILED' }
$dll = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/bin/Release/net10.0/RawWebFormsEvidence.dll'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$commit = @(& git -C $repoRoot rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or $commit.Count -ne 1) { throw 'WEBFORMS_CONSTRUCTOR_HOP_COMMIT_UNAVAILABLE' }
Write-Output "traceMapCommitSha=$($commit[0])"
Write-Output "pageId=$PageId"
Write-Output "pageHandoffSha256=$pageHash"
Write-Output "packetSha256=$packetHash"
dotnet $dll --vb-constructor-hop-audit $index $packetPath $surfaceIds[0] $HandlerName $CreatedTypeName
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_CONSTRUCTOR_HOP_AUDIT_FAILED' }
