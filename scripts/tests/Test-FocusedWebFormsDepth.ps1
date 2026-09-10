$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$temp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
try {
    $reports = @()
    foreach ($i in 0..2) {
        $boundary = @{ surfaceId = 'surface'; boundaryKind = 'sql-query'; boundaryTargetId = 'private-target'; terminalEvidenceId = 'evidence-a' }
        $boundaries = @($boundary, $boundary)
        if ($i -eq 1) { $boundaries += @{ surfaceId = 'surface'; boundaryKind = 'sql-query'; boundaryTargetId = 'private-target'; terminalEvidenceId = 'evidence-b' } }
        $packet = @{
            sources = @(@{ commitSha = 'fixture'; repositoryId = 'fixture' })
            summary = @{ truncated = $true }
            surfaceSelection = @{ items = @(@{ alias = 'page-001'; surfaceIds = @('surface') }) }
            downstreamBoundaries = $boundaries
            eventChains = @(@{ terminalKind = 'sql-query' })
            gaps = @(@{ classification = 'TruncatedByLimit'; truncationReason = 'depth' })
        }
        $path = Join-Path $temp "$i.json"
        [IO.File]::WriteAllText($path, ($packet | ConvertTo-Json -Depth 20))
        $reports += $path
    }
    $output = (& (Join-Path $scripts 'Compare-FocusedWebFormsDepth.ps1') -ReportPaths $reports 6>&1 | Out-String)
    foreach ($expected in @('boundaryRecords=2|distinctTerminals=1', 'depthDelta=10|addedTerminalIdentities=1|lostTerminalIdentities=0', 'depthDelta=12|addedTerminalIdentities=0|lostTerminalIdentities=1', 'depthPage=8|alias=page-001|distinctTerminals=1', 'depthGap=8|reason=depth|count=1')) {
        if (-not $output.Contains($expected)) { throw "Missing expected result: $expected" }
    }
    if ($output.Contains('private-target')) { throw 'Private identity leaked.' }
    $bad = Get-Content $reports[2] -Raw | ConvertFrom-Json
    $bad.sources[0].commitSha = 'different'
    [IO.File]::WriteAllText($reports[2], ($bad | ConvertTo-Json -Depth 20))
    $rejected = $false
    try { & (Join-Path $scripts 'Compare-FocusedWebFormsDepth.ps1') -ReportPaths $reports }
    catch { $rejected = $_.Exception.Message -like '*not comparable*' }
    if (-not $rejected) { throw 'Mismatched provenance was not rejected.' }

    $disabled = $false
    try { & (Join-Path $scripts 'Invoke-FocusedWebFormsPageListReport.ps1') -IndexPath $reports[0] -PageListPath $reports[1] -OutputDirectory (Join-Path $temp 'out') -MaxDepth 12 }
    catch { $disabled = $_.Exception.Message -like '*disabled*' }
    if (-not $disabled) { throw 'Depth 12 was not disabled.' }
    $disabled = $false
    try { & (Join-Path $scripts 'Compare-FocusedWebFormsDepth.ps1') }
    catch { $disabled = $_.Exception.Message -like '*disabled*' }
    if (-not $disabled) { throw 'Automatic comparison was not disabled.' }
    Write-Host 'PASS summaries and unsafe-depth launch rejection'
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
    Remove-Variable DepthTestArguments -Scope Global -ErrorAction SilentlyContinue
}
