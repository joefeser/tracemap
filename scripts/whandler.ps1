[CmdletBinding()]
param([string]$Bundle, [string]$Handler = 'BidGroupNamesDDL_Init')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Bundle)) {
    $Bundle = Microsoft.PowerShell.Utility\Read-Host 'Recovered report folder (for example verify-5/recovered-reports full path)'
}
if ($Handler -cnotmatch '^[A-Za-z0-9_]{1,128}$') { throw 'WEBFORMS_HANDLER_INVALID' }
$repo = Split-Path $PSScriptRoot -Parent
& dotnet build (Join-Path $repo 'src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj') --nologo --verbosity quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_HANDLER_BUILD_FAILED' }
$cli = Join-Path $repo 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
$json = @(& dotnet $cli webforms-review query-recovery --bundle $Bundle --handler $Handler 2>&1)
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_HANDLER_QUERY_FAILED;originals-preserved' }
try {
    $query = ($json -join "`n") | ConvertFrom-Json
    $values = @{}
    foreach ($item in $query.result.children) { $values[$item.pointer.Split('/')[-1]] = $item.value }
    foreach ($name in @('exactChains', 'evidenceVariants', 'rootIdentities', 'retainedVariantsInspected')) {
        $number = [long]0
        if (![long]::TryParse([string]$values[$name], [ref]$number) -or $number -lt 0) { throw 'invalid-count' }
        Write-Output "handler.$name=$number"
    }
    if ($values.compiledTruncated -isnot [bool] -or $values.retainedOnly -ne $true) { throw 'invalid-claim' }
    Write-Output "handler.compiledTruncated=$($values.compiledTruncated)"
} catch { throw 'WEBFORMS_HANDLER_RESPONSE_INVALID' }
Write-Output 'handler=retained-results-only;not-parity-proof;no-scan;no-graph-traversal;no-inputs-changed'
