#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory,Position=0)][string]$MapPath,
    [Parameter(Mandatory,Position=1)][ValidatePattern('\A[NSIPREFT][1-9][0-9]*\z')][string]$Alias)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try {
    if (Test-Path -LiteralPath $MapPath -PathType Container) {
        $folder = Join-Path $MapPath 'private-graph-maps'
        $maps = @(Get-ChildItem -LiteralPath $folder -File -Filter '*.aliases.private.json' | Select-Object -First 129)
        if ($maps.Count -eq 0 -or $maps.Count -gt 128) { throw 'selection' }
        $MapPath = ($maps | Sort-Object LastWriteTimeUtc,Name -Descending | Select-Object -First 1).FullName
    }
    $file = Get-Item -LiteralPath $MapPath
    if ($file.Length -gt 8388608) { throw 'limit' }
    $map = Get-Content -LiteralPath $MapPath -Raw | ConvertFrom-Json -Depth 16
    if ($map.schemaVersion -cne 'private-graph-aliases.v1' -or $map.visibility -cne 'PRIVATE-DO-NOT-SHARE') { throw 'schema' }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($map.mapping | ConvertTo-Json -Depth 8 -Compress))
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    if ($hash -cne $map.boundedInputSha256) { throw 'changed' }
    $property = $map.mapping.aliases.PSObject.Properties[$Alias]
    if ($null -eq $property -or $property.Value -isnot [string]) { throw 'missing' }
} catch { throw 'GRAPH_ALIAS_LOOKUP_FAILED: map missing, changed, invalid, or alias absent.' }
Write-Output 'PRIVATE LOCAL LOOKUP — do not upload this output without reviewing it.'
Write-Output "Mapping for share: $($map.shareFileName) (aliases apply only to this export)"
Write-Output "Share SHA-256: $($map.mapping.shareSha256)"
Write-Output "$Alias = $($property.Value)"
