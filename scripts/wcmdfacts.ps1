#Requires -Version 7.0
[CmdletBinding()]
param([string]$RunFolder)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RunFolder)) {
    $RunFolder = Read-Host 'Verification folder (full path; Enter for verify-6 in your user profile)'
    if ([string]::IsNullOrWhiteSpace($RunFolder)) { $RunFolder = Join-Path $env:USERPROFILE 'verify-6' }
}
$RunFolder = $RunFolder.Trim().Trim('"')
if (Test-Path -LiteralPath (Join-Path $RunFolder 'review/run') -PathType Container) { $RunFolder = Join-Path $RunFolder 'review/run' }
if (!(Test-Path -LiteralPath $RunFolder -PathType Container)) { throw 'WEBFORMS_COMMAND_FACT_RUN_UNAVAILABLE' }
$files = @(Get-ChildItem -LiteralPath $RunFolder -Recurse -File -Filter facts.ndjson | Select-Object -First 33)
if ($files.Count -eq 0 -or $files.Count -gt 32) { throw 'WEBFORMS_COMMAND_FACT_FILE_COUNT_INVALID' }
# Count the native compact NDJSON fact-type markers using bounded buffers.
# This is a read-only diagnostic across retained attempts, not receipt validation.
$types = @('ManagedIlCallValuesObserved', 'ManagedIlDatabaseCommandCandidate')
$gaps = @('IlCommandBindingWorkLimit','IlCommandValueEvidenceUnavailable','IlCommandOperandsUnavailable',
    'IlCommandAdapterConstructorUnsupported','IlCommandAdapterBindingUnavailable','IlCommandTextBindingUnavailable',
    'IlCommandUnknownCallEffects','IlCommandTextCallerBindingRequired','IlCommandTextReturnBindingUnavailable',
    'IlCommandTypeBindingUnavailable','IlCommandParameterFlowUnavailable','IlValueControlFlowWorkLimit',
    'IlValueControlFlowInstructionLimit','IlValueControlFlowSlotLimit','IlValueStackUnavailable',
    'IlValueStackMergeUnavailable','IlValueInstructionUnavailable','IlValueCallShapeUnavailable')
$counts = [long[]]::new($types.Count + $gaps.Count)
$markers = @($types | ForEach-Object { '"factType":"' + $_ + '"' }) +
    @($gaps | ForEach-Object { '"gapKind":"' + $_ + '"' })
$keep = ($markers | ForEach-Object Length | Measure-Object -Maximum).Maximum - 1
$total = 0L
foreach ($file in $files) {
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'WEBFORMS_COMMAND_FACT_LINK_REJECTED' }
    $stream = [IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $total += $stream.Length
        if ($total -gt 64GB) { throw 'WEBFORMS_COMMAND_FACT_BYTE_LIMIT' }
        $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true, 32768, $true)
        try {
            $buffer = [char[]]::new(32768); $tail = ''
            while (($read = $reader.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $text = $tail + [string]::new($buffer, 0, $read)
                for ($i = 0; $i -lt $markers.Count; $i++) {
                    $start = 0
                    while (($found = $text.IndexOf($markers[$i], $start, [StringComparison]::Ordinal)) -ge 0) {
                        if ($found + $markers[$i].Length -gt $tail.Length) { $counts[$i]++ }
                        $start = $found + $markers[$i].Length
                    }
                }
                $tail = $text.Substring([Math]::Max(0, $text.Length - $keep))
            }
        } finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
}
Write-Output "commandFacts.files=$($files.Count)"
for ($i = 0; $i -lt $types.Count; $i++) { Write-Output "commandFacts.$($types[$i])=$($counts[$i])" }
for ($i = 0; $i -lt $gaps.Count; $i++) { Write-Output "commandFacts.gap.$($gaps[$i])=$($counts[$types.Count + $i])" }
Write-Output 'commandFacts=retained-native-text-counts;all-found-attempts;no-native-validation;no-scan;no-traversal;no-inputs-changed'
