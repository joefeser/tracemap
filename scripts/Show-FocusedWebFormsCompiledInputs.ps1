[CmdletBinding()]
param([string]$BinFolder)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_COMPILED_INPUTS_POWERSHELL_7_REQUIRED' }

if ([string]::IsNullOrWhiteSpace($BinFolder)) {
    $BinFolder = Read-Host 'Paste the built Web Forms bin folder path'
}
if ([string]::IsNullOrWhiteSpace($BinFolder) -or
    !(Test-Path -LiteralPath $BinFolder -PathType Container)) {
    throw 'WEBFORMS_COMPILED_INPUTS_FOLDER_UNAVAILABLE'
}

# This is a read-only, top-level inventory. Never print private filenames or
# the input path; the counts are only a feasibility check, not source binding.
$files = @(Get-ChildItem -LiteralPath $BinFolder -File -ErrorAction Stop)
if ($files.Count -gt 2048) { throw 'WEBFORMS_COMPILED_INPUTS_ENTRY_LIMIT_EXCEEDED' }
$dlls = @($files | Where-Object { $_.Extension -ieq '.dll' })
$pdbs = @($files | Where-Object { $_.Extension -ieq '.pdb' })
$portable = 0
$windows = 0
$other = 0
$paired = 0
foreach ($pdb in $pdbs) {
    if (($pdb.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'WEBFORMS_COMPILED_INPUTS_LINK_UNSUPPORTED'
    }
    $stream = [IO.File]::OpenRead($pdb.FullName)
    try {
        $header = [byte[]]::new(32)
        $read = $stream.Read($header, 0, $header.Length)
        $text = [Text.Encoding]::ASCII.GetString($header, 0, $read)
        if ($text.StartsWith('BSJB', [StringComparison]::Ordinal)) { $portable++ }
        elseif ($text.StartsWith('Microsoft C/C++ MSF', [StringComparison]::Ordinal)) { $windows++ }
        else { $other++ }
    }
    finally { $stream.Dispose() }
    if ($dlls.Where({ $_.BaseName.Equals($pdb.BaseName, [StringComparison]::OrdinalIgnoreCase) }).Count -eq 1) {
        $paired++
    }
}

Write-Output 'webFormsCompiledInputsStatus=valid'
Write-Output "dllCount=$($dlls.Count)"
Write-Output "pdbCount=$($pdbs.Count)"
Write-Output "portablePdbCount=$portable"
Write-Output "windowsPdbCount=$windows"
Write-Output "otherPdbCount=$other"
Write-Output "pairedDllPdbCount=$paired"
Write-Output 'scope=top-level-bin-only;read-only;no-source-binding-claim'
