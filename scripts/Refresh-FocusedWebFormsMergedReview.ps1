[CmdletBinding()]
param(
    [string]$WebReviewRoot = '',
    [string]$BackendReviewRoot = '',
    [string]$MergedReviewRoot = '',
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_REFRESH_POWERSHELL_7_REQUIRED' }

function Read-CallerPath([string]$Name, [string]$Value) {
    if (![string]::IsNullOrWhiteSpace($Value)) { return $Value }
    $variable = Get-Variable -Name $Name -Scope 2 -ErrorAction SilentlyContinue
    if ($null -eq $variable -or [string]::IsNullOrWhiteSpace([string]$variable.Value)) {
        throw "WEBFORMS_REFRESH_VALUE_REQUIRED;name=$Name"
    }
    return [string]$variable.Value
}

$web = [IO.Path]::GetFullPath((Read-CallerPath 'WebReviewRoot' $WebReviewRoot)).TrimEnd('\', '/')
$backend = [IO.Path]::GetFullPath((Read-CallerPath 'BackendReviewRoot' $BackendReviewRoot)).TrimEnd('\', '/')
$merged = [IO.Path]::GetFullPath((Read-CallerPath 'MergedReviewRoot' $MergedReviewRoot)).TrimEnd('\', '/')
if ($merged.Equals($web, [StringComparison]::OrdinalIgnoreCase) -or
    $merged.Equals($backend, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'WEBFORMS_REFRESH_OUTPUT_MUST_BE_DISTINCT'
}

$suffix = [Guid]::NewGuid().ToString('N')
$refresh = "$merged.refresh-$suffix"
$previous = "$merged.previous-$suffix"
try {
    & (Join-Path $PSScriptRoot 'Merge-FocusedWebFormsReview.ps1') `
        -WebReviewRoot $web `
        -BackendReviewRoot $backend `
        -OutputRoot $refresh `
        -TraceMapRoot $TraceMapRoot

    if (Test-Path -LiteralPath $merged) { Move-Item -LiteralPath $merged -Destination $previous }
    try {
        Move-Item -LiteralPath $refresh -Destination $merged
    }
    catch {
        if (Test-Path -LiteralPath $previous) { Move-Item -LiteralPath $previous -Destination $merged }
        throw
    }
    if (Test-Path -LiteralPath $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
    Write-Output 'webformsMergedReviewRefresh=completed'
    Write-Output "mergedReviewRoot=$merged"
}
finally {
    if (Test-Path -LiteralPath $refresh) { Remove-Item -LiteralPath $refresh -Recurse -Force }
}
