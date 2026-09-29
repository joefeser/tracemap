[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceRoot,
    [Parameter(Mandatory)][string]$DestinationRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try {
$SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
$DestinationRoot = [IO.Path]::GetFullPath($DestinationRoot)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
function Within([string]$Parent, [string]$Child) {
    return $Child.Equals($Parent, $comparison) -or
        $Child.StartsWith(([IO.Path]::TrimEndingDirectorySeparator($Parent) + [IO.Path]::DirectorySeparatorChar), $comparison)
}
if (!(Test-Path -LiteralPath $SourceRoot -PathType Container) -or
    (Test-Path -LiteralPath $DestinationRoot) -or
    (Within $SourceRoot $DestinationRoot) -or (Within $DestinationRoot $SourceRoot)) {
    throw 'WEBFORMS_TOOL_COPY_ROOT_INVALID'
}
$items = @(Get-ChildItem -LiteralPath $SourceRoot -Force -Recurse)
if ($items.Count -gt 4096 -or @($items | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -gt 0) {
    throw 'WEBFORMS_TOOL_COPY_INVENTORY_INVALID'
}
$files = @($items | Where-Object { !$_.PSIsContainer })
if ($files.Count -lt 1 -or $files.Count -gt 512 -or
    ($files | Measure-Object -Property Length -Sum).Sum -gt 4294967296) {
    throw 'WEBFORMS_TOOL_COPY_LIMIT'
}
$entry = Join-Path $SourceRoot 'tracemap.dll'
if (!(Test-Path -LiteralPath $entry -PathType Leaf)) { throw 'WEBFORMS_TOOL_COPY_ENTRY_UNAVAILABLE' }
[void][IO.Directory]::CreateDirectory($DestinationRoot)
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($SourceRoot, $file.FullName)
    $target = Join-Path $DestinationRoot $relative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    $before = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    [IO.File]::Copy($file.FullName, $target, $false)
    $after = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($before -cne $after -or $before -cne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
        throw 'WEBFORMS_TOOL_COPY_BYTES_CHANGED;partial-copy-preserved'
    }
}
$copied = @(Get-ChildItem -LiteralPath $DestinationRoot -Force -Recurse -File)
if ($copied.Count -ne $files.Count) { throw 'WEBFORMS_TOOL_COPY_FILE_SET_CHANGED;partial-copy-preserved' }
Write-Output "toolSnapshot=verified;files=$($files.Count);originalCheckoutPreserved=true"
}
catch {
    if ($_.Exception.Message -cmatch '^WEBFORMS_TOOL_COPY_[A-Z_]+(?:;[a-z-]+)?$') { throw $_.Exception.Message }
    throw 'WEBFORMS_TOOL_COPY_INPUT_OR_OUTPUT_INVALID;partial-copy-preserved'
}
