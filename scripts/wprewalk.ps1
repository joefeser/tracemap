[CmdletBinding()]
param([string]$ReportFolder)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ReportFolder)) {
    $ReportFolder = Read-Host 'Compiled-only handler report folder (full path)'
}
$path = Join-Path $ReportFolder 'compiled-paths.local.html'
$marker = 'TerminalReachabilityPrewalk'
$present = $false
$stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try {
    if ($stream.Length -gt 512MB) { throw 'WEBFORMS_PREWALK_INPUT_LIMIT' }
    $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true, 4096, $true)
    try {
        $buffer = [char[]]::new(4096)
        $tail = ''
        while (($count = $reader.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $text = $tail + [string]::new($buffer, 0, $count)
            if ($text.Contains($marker, [StringComparison]::Ordinal)) { $present = $true; break }
            $keep = [Math]::Min($marker.Length - 1, $text.Length)
            $tail = $text.Substring($text.Length - $keep)
        }
    } finally { $reader.Dispose() }
} finally { $stream.Dispose() }
# Presence in rendered text is a diagnostic, not native artifact validation.
Write-Output "baseline.prewalkPresent=$($present.ToString().ToLowerInvariant())"
Write-Output 'baselineCheck=rendered-marker-presence-only;no-native-validation;no-scan;no-traversal;no-inputs-changed'
