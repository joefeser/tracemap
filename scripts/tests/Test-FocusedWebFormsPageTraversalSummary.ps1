$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$subject = Join-Path $scripts 'Show-FocusedWebFormsPageTraversalSummary.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-page-traversal-summary-' + [Guid]::NewGuid().ToString('N'))
$prior = Join-Path $temp 'review'
$current = Join-Path $prior 'webforms-standalone-review-20260914-000000-fixture'

function Write-Json([string]$Path, [object]$Value, [int]$Depth = 20) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    [IO.File]::WriteAllText($Path, (($Value | ConvertTo-Json -Depth $Depth) + "`n"), [Text.UTF8Encoding]::new($false))
}

function Write-Application([string]$Root, [string]$InputHash) {
    $applicationPath = Join-Path $Root 'workbench/application-handoff.json'
    Write-Json $applicationPath ([ordered]@{
        schemaVersion = 'webforms-application-handoff.v1'
        provenance = [ordered]@{ inputSha256 = $InputHash }
        pages = @([ordered]@{ pageId = 'page-001'; surfaceId = 'surface-fixture'; filePath = 'Pages/Fixture.aspx' })
    })
    $file = Get-Item -LiteralPath $applicationPath
    $hash = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Json (Join-Path $Root 'run-receipt.json') ([ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        run = [ordered]@{ state = 'completed' }
        stages = [ordered]@{ workbench = [ordered]@{
            state = 'completed'
            artifacts = @([ordered]@{ path = 'workbench/application-handoff.json'; bytes = $file.Length; sha256 = $hash })
        } }
    })
}

try {
    [IO.Directory]::CreateDirectory($temp) | Out-Null
    Write-Application $prior 'prior-input'
    $packetPath = Join-Path $current 'workbench/webforms-modernization.snapshot.json'
    Write-Json $packetPath ([ordered]@{
        schemaVersion = 'webforms-modernization-packet.v1'
        summary = [ordered]@{ truncated = $false }
        eventChains = @([ordered]@{
            surfaceId = 'surface-fixture'
            traversalObservation = [ordered]@{
                stopState = 'observed-downstream-without-supported-terminal'
                reachedNodeCount = 4
                traversedEdgeCount = 3
                downstreamEdgeCount = 2
                terminalPathCount = 0
                truncated = $false
                callEvidenceState = 'joined-downstream-edge-observed'
                leafReconciliationStates = @('exact-symbol')
                leafCallEvidenceStates = @('retained-call-edge')
                truncationReasons = @()
            }
        })
        downstreamBoundaries = @()
    })
    $packetHash = (Get-FileHash -LiteralPath $packetPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Application $current $packetHash

    $output = @(& $subject -ReviewRoot $prior -PriorPageId page-001 -StandaloneReviewRoot $current)
    foreach ($expected in @(
        'pageTraversalSummary=valid', 'chains=1', 'boundaries=0', 'observations=1',
        'reachedNodes=4', 'traversedEdges=3', 'downstreamEdges=2', 'terminalPaths=0',
        'stopState.observed-downstream-without-supported-terminal=1',
        'callEvidenceState.joined-downstream-edge-observed=1')) {
        if ($expected -notin $output) { throw "Traversal summary omitted: $expected" }
    }
    if (@($output | Where-Object { $_ -match 'Fixture|surface-fixture' }).Count -gt 0) {
        throw 'Traversal summary disclosed private fixture identities.'
    }
    Write-Host 'PASS focused Web Forms page traversal summary'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
