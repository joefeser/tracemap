$ErrorActionPreference = 'Stop'
$temp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
$scriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'Summarize-CompletedWebFormsDepths.ps1'
try {
    foreach ($depth in @(8,10)) {
        $dir = Join-Path $temp "webforms-depth-comparison-fixture/depth-$depth"
        [void][IO.Directory]::CreateDirectory($dir)
        $boundary = @{surfaceId='s';boundaryKind='sql-query';boundaryTargetId='PRIVATE';terminalEvidenceId='a'}
        $boundaries = @($boundary, $boundary)
        if ($depth -eq 10) { $boundaries += @{surfaceId='s';boundaryKind='sql-query';boundaryTargetId='PRIVATE';terminalEvidenceId='b'} }
        $packet = @{
            sources=@(@{commitSha='fixture'}); summary=@{truncated=$true}
            surfaceSelection=@{items=@(@{alias='page-001';surfaceIds=@('s')},@{alias='page-002';surfaceIds=@('s2')})}
            downstreamBoundaries=$boundaries
            # Production JSON omits null fields; also retain explicit-null coverage.
            eventChains=@(@{surfaceId='s'},@{surfaceId='s';handlerFactId='h'},@{surfaceId='s';handlerFactId=$null;terminalKind=$null},@{surfaceId='s';handlerFactId='h';terminalKind='sql-query'})
            gaps=@(@{classification='TruncatedByLimit';truncationReason='depth'},@{classification='TruncatedByLimit'})
        }
        [IO.File]::WriteAllText((Join-Path $dir 'webforms-modernization.json'), ($packet | ConvertTo-Json -Depth 20))
    }
    $output = (& $scriptPath -OutputRoot $temp -Details 6>&1 | Out-String)
    foreach ($expected in @('boundaryRecords=2|distinctTerminalEvidence=1', 'boundaryRecords=3|distinctTerminalEvidence=2', 'terminalDelta=added:1|lost:0', 'handlerUnavailable=2|resolvedWithoutTerminal=1', 'unknownReason=1')) {
        if (-not $output.Contains($expected)) { throw "Missing: $expected" }
    }
    if ($output.Contains('PRIVATE')) { throw 'Private identity disclosed.' }
    if (-not $output.Contains('page=page-001|hasTerminal=True|handlerUnavailableChains=2')) { throw 'Missing targeted page details.' }
    if (-not $output.Contains('page=page-002|hasTerminal=False|handlerUnavailableChains=0')) { throw 'Missing terminal-free page.' }
    $path = Join-Path $temp 'webforms-depth-comparison-fixture/depth-10/webforms-modernization.json'
    $packet.sources[0].commitSha='different'
    [IO.File]::WriteAllText($path, ($packet | ConvertTo-Json -Depth 20))
    $rejected=$false
    try { & $scriptPath -OutputRoot $temp } catch { $rejected=$_.Exception.Message -like '*comparison withheld*' }
    if (-not $rejected) { throw 'Provenance mismatch accepted.' }
    Write-Host 'PASS retained-only summary: discovery, duplicate identities, delta, gaps, privacy, provenance'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
