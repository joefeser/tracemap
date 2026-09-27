[CmdletBinding()]
param(
    [string]$OutputRoot,
    [Parameter(Mandatory)][string]$TypeName,
    [Parameter(Mandatory)][string]$MethodName,
    [long]$IlMaxWork = 30000000
)

# Diagnose the saved replay without rescanning, combining, or querying paths.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_POST_POWERSHELL_7_REQUIRED' }
if ($IlMaxWork -lt 2000001 -or $IlMaxWork -gt 100000000) { throw 'WEBFORMS_POST_IL_WORK_LIMIT_INVALID' }
if (!$OutputRoot) {
    $latest = Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory |
        Where-Object { $_.Name -cmatch '^tracemap-existing-publish-[0-9a-f]{32}$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName "scan-ilwork-$IlMaxWork/facts.ndjson") -PathType Leaf) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (!$latest) { throw 'WEBFORMS_POST_REPLAY_UNAVAILABLE' }
    $OutputRoot = $latest.FullName
}
$reportPath = Join-Path $OutputRoot "handler-database-api-ilwork-$IlMaxWork.json"
if (!(Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw 'WEBFORMS_POST_REPORT_UNAVAILABLE' }
& (Join-Path $PSScriptRoot 'wm.ps1') -OutputRoot $OutputRoot `
    -ScanFolder "scan-ilwork-$IlMaxWork" -TypeName $TypeName -MethodName $MethodName
$report = [IO.File]::ReadAllText($reportPath) | ConvertFrom-Json -Depth 50
Write-Output "postPaths=$(@($report.paths).Count)"
Write-Output "postGaps=$(@($report.gaps).Count)"
if ($report.rootTraversal) {
    foreach ($name in @('terminalCallerCount', 'reachableTerminalCallerCount', 'reachableFillMemberRefCount',
            'reachableUnrecognizedFillMemberRefCount', 'reachableUnresolvedIlCallCount')) {
        $property = $report.rootTraversal.PSObject.Properties[$name]
        if ($null -ne $property) { Write-Output "post.$name=$($property.Value)" }
    }
    foreach ($kind in @($report.rootTraversal.traversedEdgeKinds)) {
        if ([string]$kind -cmatch '^[a-z][a-z0-9-]{0,79}$') { Write-Output "postTraversed.$kind=True" }
    }
    $unresolvedReasons = $report.rootTraversal.PSObject.Properties['reachableUnresolvedIlCallsByReason']
    if ($null -ne $unresolvedReasons -and $null -ne $unresolvedReasons.Value) {
        foreach ($reason in $unresolvedReasons.Value.PSObject.Properties) {
            if ($reason.Name -cmatch '^[a-z][a-z0-9-]{0,79}$') {
                Write-Output "postUnresolved.$($reason.Name)=$($reason.Value)"
            }
        }
    }
}
