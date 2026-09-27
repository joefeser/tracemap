[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TypeName,
    [Parameter(Mandatory)][string]$MethodName,
    [string]$OutputRoot,
    [long]$IlMaxWork = 20000000
)

# Diagnose one already-copied published assembly with both IL readers.
# Only fixed categorical fields leave this process; no method or path is printed.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_IL_DIAG_POWERSHELL_7_REQUIRED' }
if ($IlMaxWork -lt 1 -or $IlMaxWork -gt 100000000) { throw 'WEBFORMS_IL_DIAG_WORK_LIMIT_INVALID' }
if (!$OutputRoot) {
    $latest = Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory |
        Where-Object { $_.Name -cmatch '^tracemap-existing-publish-[0-9a-f]{32}$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName "scan-ilwork-$IlMaxWork/facts.ndjson") -PathType Leaf) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (!$latest) { throw 'WEBFORMS_IL_DIAG_INPUT_UNAVAILABLE' }
    $OutputRoot = $latest.FullName
}
$factsPath = Join-Path $OutputRoot "scan-ilwork-$IlMaxWork/facts.ndjson"
$receiptPath = Join-Path $OutputRoot 'publish-receipt.local.json'
if (!(Test-Path -LiteralPath $factsPath -PathType Leaf) -or
    !(Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
    throw 'WEBFORMS_IL_DIAG_INPUT_UNAVAILABLE'
}
$typeMarker = '|names:' + $TypeName.Length + ':' + $TypeName + '|arity:'
$methodMarker = '|method:' + $MethodName.Length + ':' + $MethodName + '|'
$hashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$tokens = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($line in [IO.File]::ReadLines($factsPath)) {
    if (!$line.Contains('"factType":"ManagedMethodDeclared"', [StringComparison]::Ordinal)) { continue }
    $fact = $line | ConvertFrom-Json -Depth 30
    if (!([string]$fact.targetSymbol).Contains($typeMarker, [StringComparison]::Ordinal) -or
        !([string]$fact.targetSymbol).Contains($methodMarker, [StringComparison]::Ordinal)) { continue }
    [void]$hashes.Add([string]$fact.properties.rawFileSha256)
    [void]$tokens.Add([string]$fact.properties.metadataToken)
}
if ($hashes.Count -ne 1 -or $tokens.Count -lt 1 -or $tokens.Count -gt 64) {
    throw 'WEBFORMS_IL_DIAG_METHOD_ASSEMBLY_NOT_UNIQUE'
}
$hash = @($hashes)[0]
$receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 30
$rows = @($receipt.assemblyInventory | Where-Object { [string]$_.sha256 -ceq $hash })
if ($rows.Count -ne 1 -or [string]$rows[0].path -cnotmatch '^bin/[^/\\]+\.dll$') {
    throw 'WEBFORMS_IL_DIAG_ASSEMBLY_NOT_UNIQUE'
}
$path = Join-Path $OutputRoot ([string]$rows[0].path)
if (!(Test-Path -LiteralPath $path -PathType Leaf) -or
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $hash) {
    throw 'WEBFORMS_IL_DIAG_ASSEMBLY_CHANGED'
}
$project = Join-Path $PSScriptRoot 'diagnostics/TraceMap.IlReaderProbe/TraceMap.IlReaderProbe.csproj'
$tokenList = (@($tokens) | Sort-Object) -join ','
$raw = @(& dotnet run --project $project -- $path 16384 $IlMaxWork $tokenList 2>&1)
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_IL_DIAG_RUN_FAILED' }
$lines = @($raw | ForEach-Object { [string]$_ } | Where-Object {
    $_ -cmatch '^ilReaderProbe[A-Za-z]+=(?:[A-Za-z0-9-]+|[0-9]+)$'
})
if (@($lines | Where-Object { $_ -cmatch '^ilReaderProbeStatus=' }).Count -ne 1) {
    throw 'WEBFORMS_IL_DIAG_RESULT_UNAVAILABLE'
}
foreach ($line in $lines) { Write-Output $line }
