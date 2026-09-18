$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$subject = Join-Path $scripts 'Compare-FocusedWebFormsReviewRegression.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-review-regression-' + [Guid]::NewGuid().ToString('N'))
$old = Join-Path $temp 'old'
$current = Join-Path $temp 'current'

function Write-Json([string]$Path, [object]$Value, [int]$Depth = 20) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    [IO.File]::WriteAllText($Path, (($Value | ConvertTo-Json -Depth $Depth) + "`n"), [Text.UTF8Encoding]::new($false))
}

function Write-Fixture([string]$Root, [int]$ReachedNodes, [int]$TerminalPaths) {
    $packetPath = Join-Path $Root 'workbench/webforms-modernization.snapshot.json'
    Write-Json $packetPath ([ordered]@{
        schemaVersion = 'webforms-modernization-packet.v1'
        summary = [ordered]@{ truncated = $false }
        eventChains = @([ordered]@{
            surfaceId = 'surface-fixture'
            terminalKind = $(if ($TerminalPaths -gt 0) { 'sql-query' } else { 'none' })
            traversalObservation = [ordered]@{
                stopState = $(if ($TerminalPaths -gt 0) { 'supported-terminal-reached' } else { 'no-observed-downstream-edge' })
                reachedNodeCount = $ReachedNodes
                traversedEdgeCount = $ReachedNodes - 1
                downstreamEdgeCount = $ReachedNodes - 2
                terminalPathCount = $TerminalPaths
                truncated = $false
                callEvidenceState = 'joined-downstream-edge-observed'
                traversedEdgeKinds = @('projectless-vb-receiver-bridge')
                traversedRuleIds = @('combined.paths.projectless-vb-receiver-bridge.v1')
                leafNodeKinds = @()
                leafSurfaceKinds = @()
                leafRuleIds = @()
                leafReconciliationStates = @()
                leafCallEvidenceStates = @()
                leafSourceAvailabilityStates = @()
                frontierNodeKinds = @()
                frontierSurfaceKinds = @()
                frontierRuleIds = @()
                truncationReasons = @()
                diagnosticShapesTruncated = $false
            }
        })
        downstreamBoundaries = @()
        gaps = @()
    })
    $packetHash = (Get-FileHash -LiteralPath $packetPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $applicationPath = Join-Path $Root 'workbench/application-handoff.json'
    Write-Json $applicationPath ([ordered]@{
        schemaVersion = 'webforms-application-handoff.v1'
        provenance = [ordered]@{ inputSha256 = $packetHash }
        pages = @([ordered]@{ pageId = 'page-011'; surfaceId = 'surface-fixture'; filePath = 'Pages/Fixture.aspx' })
    })
    $applicationFile = Get-Item -LiteralPath $applicationPath
    $applicationHash = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Json (Join-Path $Root 'run-receipt.json') ([ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        run = [ordered]@{ state = 'completed' }
        stages = [ordered]@{ workbench = [ordered]@{
            state = 'completed'
            artifacts = @([ordered]@{
                path = 'workbench/application-handoff.json'
                bytes = $applicationFile.Length
                sha256 = $applicationHash
            })
        } }
    })
    [IO.File]::WriteAllText((Join-Path $Root 'workbench/page-011.html'), ('x' * $ReachedNodes))
}

try {
    Write-Fixture $old 4 0
    Write-Fixture $current 9 1
    $output = @(& $subject -ReviewRoot $current -OldReviewRoot $old)
    foreach ($expected in @(
        'reviewRegressionDiagnostic=valid',
        'current.reachedNodes=9',
        'old.reachedNodes=4',
        'delta.reachedNodes=5',
        'current.terminalPaths=1',
        'old.terminalPaths=0',
        'delta.terminalPaths=1',
        'delta.artifact.pageHtml.bytes=5')) {
        if ($expected -notin $output) { throw "Review regression diagnostic omitted: $expected" }
    }
    Write-Host 'PASS focused Web Forms review regression diagnostic'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
