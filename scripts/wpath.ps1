[CmdletBinding()]
param(
    [string]$OutputRoot,
    [long]$IlMaxWork = 30000000
)

# Requery the saved combined replay from the exact resolved handler symbol.
# No source scan, publish rebuild, or combine is performed.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_PATH_POWERSHELL_7_REQUIRED' }
if ($IlMaxWork -lt 2000001 -or $IlMaxWork -gt 100000000) { throw 'WEBFORMS_PATH_IL_WORK_LIMIT_INVALID' }
if (!$OutputRoot) {
    $latest = Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory |
        Where-Object { $_.Name -cmatch '^tracemap-existing-publish-[0-9a-f]{32}$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName "combined-ilwork-$IlMaxWork.sqlite") -PathType Leaf) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (!$latest) { throw 'WEBFORMS_PATH_REPLAY_UNAVAILABLE' }
    $OutputRoot = $latest.FullName
}
$factsPath = Join-Path $OutputRoot "scan-ilwork-$IlMaxWork/facts.ndjson"
$combinedPath = Join-Path $OutputRoot "combined-ilwork-$IlMaxWork.sqlite"
$receiptPath = Join-Path $OutputRoot 'publish-receipt.local.json'
$previousPath = Join-Path $OutputRoot 'handler-paths.json'
foreach ($path in @($factsPath, $combinedPath, $receiptPath, $previousPath)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'WEBFORMS_PATH_INPUT_UNAVAILABLE' }
}
$receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 30
$previous = [IO.File]::ReadAllText($previousPath) | ConvertFrom-Json -Depth 50
if (@($receipt.pages).Count -ne 1 -or $previous.query.toSurface -cne 'sql-query' -or
    [string]::IsNullOrWhiteSpace([string]$previous.query.fromSymbol)) {
    throw 'WEBFORMS_PATH_HANDLER_INPUT_UNEXPECTED'
}
$pageSource = [string]$receipt.pages[0].sourcePath
$handlerName = [string]$previous.query.fromSymbol
$symbols = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in [IO.File]::ReadLines($factsPath)) {
    if (!$line.Contains('"factType":"WebFormsHandlerResolved"', [StringComparison]::Ordinal)) { continue }
    $fact = $line | ConvertFrom-Json -Depth 30
    if ([string]$fact.properties.handlerName -ieq $handlerName -and
        [string]$fact.properties.markupFile -ieq $pageSource -and
        ![string]::IsNullOrWhiteSpace([string]$fact.properties.handlerSymbol)) {
        [void]$symbols.Add([string]$fact.properties.handlerSymbol)
    }
}
Write-Output "pathHandlerSymbols=$($symbols.Count)"
if ($symbols.Count -ne 1) { throw 'WEBFORMS_PATH_HANDLER_NOT_UNIQUE' }
$handlerSymbol = $symbols.GetEnumerator() | Select-Object -First 1
$reportPath = Join-Path $OutputRoot "handler-database-api-ilwork-$IlMaxWork.json"
$logPath = Join-Path $OutputRoot "paths-ilwork-$IlMaxWork.local.log"
if (Test-Path -LiteralPath $reportPath) {
    $reportPath = Join-Path $OutputRoot "handler-database-api-exact-ilwork-$IlMaxWork.json"
    $logPath = Join-Path $OutputRoot "paths-exact-ilwork-$IlMaxWork.local.log"
}
if (Test-Path -LiteralPath $reportPath) { throw 'WEBFORMS_PATH_OUTPUT_EXISTS' }
$project = Join-Path $PSScriptRoot '../src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
& dotnet run --project $project -- paths --index $combinedPath --out $reportPath `
    --format json --from-symbol $handlerSymbol --exact-from-symbol `
    --to-surface database-api --max-depth 20 --max-paths 256 *> $logPath
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_PATH_QUERY_FAILED' }
$report = [IO.File]::ReadAllText($reportPath) | ConvertFrom-Json -Depth 50
Write-Output "exactPathSelectorCandidates=$($report.summary.selectorCandidateCount)"
Write-Output "exactPathCount=$(@($report.paths).Count)"
Write-Output "exactPathTruncated=$($report.summary.truncated)"
if ($report.rootTraversal) {
    Write-Output "exactReachedNodes=$($report.rootTraversal.reachedNodeCount)"
    Write-Output "exactTraversedEdges=$($report.rootTraversal.traversedEdgeCount)"
    Write-Output "exactTerminalCallers=$($report.rootTraversal.terminalCallerCount)"
    Write-Output "exactReachableTerminalCallers=$($report.rootTraversal.reachableTerminalCallerCount)"
    Write-Output "exactReachableFillMemberRefs=$($report.rootTraversal.reachableFillMemberRefCount)"
    Write-Output "exactReachableUnrecognizedFillMemberRefs=$($report.rootTraversal.reachableUnrecognizedFillMemberRefCount)"
    foreach ($kind in @($report.rootTraversal.traversedEdgeKinds)) {
        if ([string]$kind -cmatch '^[a-z][a-z0-9-]{0,79}$') { Write-Output "exactTraversed.$kind=True" }
    }
}
$candidatePaths = @($report.paths | Where-Object {
    @($_.edges | Where-Object { $_.edgeKind -ceq 'compiled-database-api-candidate' }).Count -gt 0
})
Write-Output "exactDatabaseApiCandidatePaths=$($candidatePaths.Count)"
