[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceSiteRoot,
    [string]$OutputRoot,
    [long]$IlMaxWork = 20000000,
    [string]$TypeName,
    [string]$MethodName
)

# Replay only the scan and handler query against an existing copied publish.
# The original scan, receipt, binding, and publish bytes remain untouched.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_REPLAY_POWERSHELL_7_REQUIRED' }
if ($IlMaxWork -lt 2000001 -or $IlMaxWork -gt 100000000) {
    throw 'WEBFORMS_REPLAY_IL_WORK_LIMIT_INVALID'
}
if ([bool]$TypeName -ne [bool]$MethodName) { throw 'WEBFORMS_REPLAY_METHOD_PAIR_REQUIRED' }
if (!$OutputRoot) {
    $latest = Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory |
        Where-Object { $_.Name -cmatch '^tracemap-existing-publish-[0-9a-f]{32}$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'scan/scan-manifest.json') -PathType Leaf) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (!$latest) { throw 'WEBFORMS_REPLAY_INPUT_UNAVAILABLE' }
    $OutputRoot = $latest.FullName
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$SourceSiteRoot = [IO.Path]::GetFullPath($SourceSiteRoot)
$receiptPath = Join-Path $OutputRoot 'publish-receipt.local.json'
$bindingPath = Join-Path $OutputRoot 'compiled-binding.local.json'
$oldManifestPath = Join-Path $OutputRoot 'scan/scan-manifest.json'
$oldPathsPath = Join-Path $OutputRoot 'handler-paths.json'
foreach ($path in @($receiptPath, $bindingPath, $oldManifestPath, $oldPathsPath)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'WEBFORMS_REPLAY_INPUT_UNAVAILABLE' }
}
$receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 30
$old = [IO.File]::ReadAllText($oldManifestPath) | ConvertFrom-Json -Depth 30
$paths = [IO.File]::ReadAllText($oldPathsPath) | ConvertFrom-Json -Depth 50
if ($receipt.schemaVersion -cne 'webforms-publish-binding.v1' -or
    $old.webFormsPublishProvenance.status -cne 'bound' -or
    [string]::IsNullOrWhiteSpace([string]$paths.query.fromSymbol) -or
    $paths.query.toSurface -cne 'sql-query') {
    throw 'WEBFORMS_REPLAY_PROVENANCE_UNAVAILABLE'
}
$head = ([string](& git -C $SourceSiteRoot rev-parse HEAD)).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $head -cne [string]$receipt.sourceCommitSha) {
    throw 'WEBFORMS_REPLAY_SOURCE_COMMIT_MISMATCH'
}
$dirty = @(& git -C $SourceSiteRoot status --porcelain --untracked-files=all -- .)
if ($LASTEXITCODE -ne 0 -or $dirty.Count -ne 0) { throw 'WEBFORMS_REPLAY_SOURCE_DIRTY' }
$assemblies = @($receipt.assemblyInventory | Where-Object {
    $_.disposition -in @('selected', 'artifact-context-no-source-commit')
})
if ($assemblies.Count -lt 1 -or $assemblies.Count -gt 64 -or
    $assemblies.Count -ne @($old.compiledInputProvenance.outcomes).Count) {
    throw 'WEBFORMS_REPLAY_ASSEMBLY_INVENTORY_MISMATCH'
}
$assemblyPaths = [Collections.Generic.List[string]]::new()
foreach ($item in $assemblies) {
    $relative = [string]$item.path
    if ($relative -cnotmatch '^bin/[^/\\]+\.dll$') { throw 'WEBFORMS_REPLAY_ASSEMBLY_PATH_INVALID' }
    $full = Join-Path $OutputRoot $relative
    if (!(Test-Path -LiteralPath $full -PathType Leaf) -or
        (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant() -cne [string]$item.sha256) {
        throw 'WEBFORMS_REPLAY_ASSEMBLY_HASH_MISMATCH'
    }
    $assemblyPaths.Add($full)
}
$scanPath = Join-Path $OutputRoot ('scan-ilwork-' + $IlMaxWork)
$combinedPath = Join-Path $OutputRoot ('combined-ilwork-' + $IlMaxWork + '.sqlite')
$reportPath = Join-Path $OutputRoot ('handler-database-api-ilwork-' + $IlMaxWork + '.json')
if (Test-Path -LiteralPath $scanPath) { throw 'WEBFORMS_REPLAY_OUTPUT_EXISTS' }
$project = Join-Path $PSScriptRoot '../src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
$arguments = [Collections.Generic.List[string]]::new()
foreach ($arg in @('run', '--project', $project, '--', 'scan', '--repo', $SourceSiteRoot,
        '--out', $scanPath, '--webforms-publish-receipt', $receiptPath,
        '--compiled-binding-receipt', $bindingPath, '--compiled-max-artifacts', '64',
        '--compiled-max-text', '8192', '--il-body-evidence', '--il-max-text', '16384',
        '--il-max-work', [string]$IlMaxWork)) { $arguments.Add($arg) }
foreach ($assembly in $assemblyPaths) { $arguments.Add('--compiled-input'); $arguments.Add($assembly) }
foreach ($source in @($receipt.sourceFiles)) {
    $relative = [string]$source.path
    if ([string]::IsNullOrWhiteSpace($relative) -or $relative.StartsWith('/') -or
        [IO.Path]::IsPathRooted($relative) -or
        @($relative.Replace('\','/').Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
        throw 'WEBFORMS_REPLAY_SOURCE_PATH_INVALID'
    }
    $arguments.Add('--include'); $arguments.Add($relative)
}
Write-Output "replayAssemblies=$($assemblyPaths.Count)"
Write-Output "replayIlMaxWork=$IlMaxWork"
& dotnet @arguments *> (Join-Path $OutputRoot ('scan-ilwork-' + $IlMaxWork + '.local.log'))
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_REPLAY_SCAN_FAILED' }
$manifest = [IO.File]::ReadAllText((Join-Path $scanPath 'scan-manifest.json')) | ConvertFrom-Json -Depth 30
Write-Output "replayPublishStatus=$($manifest.webFormsPublishProvenance.status)"
Write-Output "replayIlCoverage=$($manifest.ilBodyProvenance.coverageState)"
$ilGaps = @($manifest.ilBodyProvenance.outcomes | Where-Object { @($_.gapKinds).Count -gt 0 })
Write-Output "replayIlInputsWithGaps=$($ilGaps.Count)"
foreach ($group in @($ilGaps | ForEach-Object { @($_.gapKinds) } | Group-Object | Sort-Object Name)) {
    if ([string]$group.Name -cmatch '^[A-Za-z][A-Za-z0-9]{0,79}$') {
        Write-Output "replayIlGap.$($group.Name)=$($group.Count)"
    }
}
if ($TypeName) {
    & (Join-Path $PSScriptRoot 'wm.ps1') -OutputRoot $OutputRoot `
        -ScanFolder ('scan-ilwork-' + $IlMaxWork) -TypeName $TypeName -MethodName $MethodName
}
if ($manifest.webFormsPublishProvenance.status -cne 'bound') { return }
& dotnet run --project $project -- combine --index (Join-Path $scanPath 'index.sqlite') `
    --out $combinedPath --label existing-publish *> (Join-Path $OutputRoot ('combine-ilwork-' + $IlMaxWork + '.local.log'))
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_REPLAY_COMBINE_FAILED' }
& (Join-Path $PSScriptRoot 'wpath.ps1') -OutputRoot $OutputRoot -IlMaxWork $IlMaxWork
