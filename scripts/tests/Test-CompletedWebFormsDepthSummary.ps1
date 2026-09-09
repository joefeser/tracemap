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
            surfaceSelection=@{items=@(@{alias='page-001';surfaceIds=@('s')})}
            downstreamBoundaries=$boundaries
            # Production JSON omits null fields; also retain explicit-null coverage.
            eventChains=@(@{},@{handlerFactId='h'},@{handlerFactId=$null;terminalKind=$null},@{handlerFactId='h';terminalKind='sql-query'})
            gaps=@(@{classification='TruncatedByLimit';truncationReason='depth'},@{classification='TruncatedByLimit'})
        }
        [IO.File]::WriteAllText((Join-Path $dir 'webforms-modernization.json'), ($packet | ConvertTo-Json -Depth 20))
    }
    $output = (& $scriptPath -OutputRoot $temp 6>&1 | Out-String)
    foreach ($expected in @('boundaryRecords=2|distinctTerminalEvidence=1', 'boundaryRecords=3|distinctTerminalEvidence=2', 'terminalDelta=added:1|lost:0', 'handlerUnavailable=2|resolvedWithoutTerminal=1', 'unknownReason=1')) {
        if (-not $output.Contains($expected)) { throw "Missing: $expected" }
    }
    if ($output.Contains('PRIVATE')) { throw 'Private identity disclosed.' }
    $path = Join-Path $temp 'webforms-depth-comparison-fixture/depth-10/webforms-modernization.json'
    $packet.sources[0].commitSha='different'
    [IO.File]::WriteAllText($path, ($packet | ConvertTo-Json -Depth 20))
    $rejected=$false
    try { & $scriptPath -OutputRoot $temp } catch { $rejected=$_.Exception.Message -like '*comparison withheld*' }
    if (-not $rejected) { throw 'Provenance mismatch accepted.' }
    Write-Host 'PASS retained-only summary: discovery, duplicate identities, delta, gaps, privacy, provenance'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
