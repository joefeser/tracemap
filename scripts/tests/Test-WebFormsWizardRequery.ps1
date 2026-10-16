Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot '../wrequery.ps1'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('wizard-requery-test-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory((Join-Path $temporary 'sample')) | Out-Null
$global:WizardRequeryCalls = [Collections.Generic.List[object]]::new()
$global:WizardRequeryState = 'reports-completed-review-only'
$global:WizardRunId = 'a' * 32
$global:WizardRequeryExit = 0
$global:WizardRequeryOriginalReader = $true
$global:WizardRequeryCause = $null
$global:WizardRequeryReason = 'depth'
$global:WizardRequeryWork = $null
$global:WizardRequeryWorkLimit = $null
$global:WizardRequeryWorkbench = Join-Path $temporary 'reports/index.html'
function global:dotnet {
    $global:WizardRequeryCalls.Add(@($args))
    $global:LASTEXITCODE = 0
    if ($args -contains 'status') {
        @{ schemaVersion='webforms-review-status.v1'; state=$global:WizardRequeryState;
           runId=$global:WizardRunId;preflightSha256=('b'*64);checkpointSha256=('c'*64);combinedIndexSha256=('d'*64);
           readerMatchesOriginalGenerator=$global:WizardRequeryOriginalReader; retainedArtifactsVerified=$true;
           workbenchPath=$global:WizardRequeryWorkbench } | ConvertTo-Json -Compress
    } elseif ($args -contains 'requery-handler') {
        $global:LASTEXITCODE = $global:WizardRequeryExit
        if ($global:WizardRequeryExit -eq 0) {
            $destination = $args[10]
            [IO.Directory]::CreateDirectory($destination) | Out-Null
            if ($args -contains 'method-graph') {
                $graphPath = Join-Path $destination 'method-graph.local.json'
                @{schemaVersion='retained-method-graph.v1';indexSha256=('d'*64);graph=@{nodes=@();edges=@();calls=@();cutoffs=@()}} |
                    ConvertTo-Json -Depth 5 | Set-Content $graphPath
                $graphHtml = Join-Path $destination 'method-graph.local.html'
                Set-Content $graphHtml '<html>synthetic graph</html>'
                @{schemaVersion='webforms-handler-requery.v1';reportVariant='method-graph';truncated=$false;pathEnumerationPerformed=$false;
                    runId=('a'*32);sourcePreflightSha256=('b'*64);sourceCheckpointSha256=('c'*64);combinedIndexSha256=('d'*64);
                    exactChains=0;evidenceVariants=0;primaryReport='method-graph.local.html';artifacts=@(@{
                        relativePath='method-graph.local.json';bytes=(Get-Item $graphPath).Length;sha256=(Get-FileHash $graphPath).Hash.ToLowerInvariant()},@{
                        relativePath='method-graph.local.html';bytes=(Get-Item $graphHtml).Length;sha256=(Get-FileHash $graphHtml).Hash.ToLowerInvariant()})} | ConvertTo-Json -Depth 5 |
                    Set-Content (Join-Path $destination 'handler-requery.local.json')
                return
            }
            $handoffPath = Join-Path $destination 'compiled-paths.handoff.local.json'
            @{schemaVersion='webforms-compiled-grouped-handoff.v1';inputIndexSha256=('d'*64);header=@{
                summary=@{traversalWorkUnits=$global:WizardRequeryWork};query=@{maxTraversalWork=$global:WizardRequeryWorkLimit};gaps=@(
                @{gapKind='TruncatedByLimit';reason=$global:WizardRequeryReason;cutoffCause=$global:WizardRequeryCause;filePath='synthetic/Page.vb';startLine=12;
                  nodeId='node-1';ruleId='synthetic.rule';evidenceTier='Tier4Unknown';message='Depth limit reached.'})};
                variants=@(@{nodeReferences=@('endpoint')}); nodes=@{endpoint=@{
                    nodeId='endpoint-id';surfaceKind='database-api';surfaceName='SqlCommand.ExecuteScalar';
                    commandBinding=@{commandTextFromPath=@{state='unresolved-operand';origin=@{kind='call-result';identity='private-sql-marker'};
                        ruleId='synthetic.command';evidenceTier='Tier3SyntaxOrTextual';steps=@();gaps=@('IlCommandOperandValueUnresolved')}}}}} |
                ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $handoffPath
            @{schemaVersion='webforms-handler-requery.v1';truncated=$true;artifacts=@(@{
                relativePath='compiled-paths.handoff.local.json';bytes=(Get-Item $handoffPath).Length;
                sha256=(Get-FileHash $handoffPath).Hash.ToLowerInvariant()});
                runId=('a'*32);sourcePreflightSha256=('b'*64);sourceCheckpointSha256=('c'*64);combinedIndexSha256=('d'*64)} |
                ConvertTo-Json -Depth 8 | Set-Content (Join-Path $destination 'handler-requery.local.json')
        }
    } else { throw 'Unexpected dotnet operation (no builds or scans allowed).' }
}
function Save-Fixture([string]$Relative = ('runs/sample-' + ('a' * 32))) {
    $project = @{schemaVersion='webforms-wizard-project.v1'; configuration=@{
        id='sample'; step='completed'; run=@{relativeRoot=$Relative}}}
    $path = Join-Path $temporary 'sample/project.config.json'
    $project | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path
    @{schemaVersion='webforms-wizard-root.v1'; configuration=@{projects=@(@{
        id='sample'; configSha256=(Get-FileHash $path -Algorithm SHA256).Hash})}} |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $temporary 'root.config.json')
}
function Expect-Failure([scriptblock]$Action, [string]$Message) {
    try { & $Action | Out-Null } catch {
        if ($_.Exception.Message -notlike "*$Message*") { throw }
        return
    }
    throw "Expected failure: $Message"
}
try {
    Save-Fixture
    $output = @(& $helper $temporary -Handler Page_Load)
    if (($output -join "`n") -notmatch 'depth: 1 retained gaps' -or
        ($output -join "`n") -notmatch 'location=synthetic/Page.vb; line=12') { throw 'Missing bounded depth details.' }
    if (($output -join "`n") -notmatch 'IlCommandOperandValueUnresolved' -or
        ($output -join "`n") -notmatch 'commandTypeFromPath: unavailable' -or
        ($output -join "`n") -match 'private-sql-marker') { throw 'Command diagnostic missing or leaked value.' }
    if ($global:WizardRequeryCalls.Count -ne 2) { throw 'Expected status then requery.' }
    $call = $global:WizardRequeryCalls[1]
    $expectedRun = Join-Path (Join-Path $temporary ('runs/sample-' + ('a' * 32))) 'run'
    if ($call[4] -ne $expectedRun -or $call[6] -ne (Split-Path $global:WizardRequeryWorkbench -Parent) -or
        $call[8] -ne 'Page_Load') { throw 'Saved nested run or verified bundle was not used.' }
    $firstOutput = $call[10]
    . (Join-Path $PSScriptRoot '../WebFormsRequeryDiagnostics.ps1')
    Add-Content (Join-Path $firstOutput 'compiled-paths.handoff.local.json') ' '
    Expect-Failure { Write-WebFormsTruncationSummary $firstOutput } 'SUMMARY_HANDOFF_CHANGED'
    & $helper $temporary -Project sample -Handler Page_Load | Out-Null
    if ($global:WizardRequeryCalls[3][10] -eq $firstOutput) { throw 'Output must be fresh.' }
    $global:WizardRequeryCause = 'candidate-identity-roundtrip'
    $display = @(& $helper $temporary -Handler Page_Load)
    if (($display -join "`n") -notmatch 'identity round trips \(not application recursion\): 1 retained gaps') { throw 'Identity diagnostics not separated.' }
    $global:WizardRequeryCause = 'terminal-route-not-found-within-depth-bound'
    $display = @(& $helper $temporary -Handler Page_Load)
    if (($display -join "`n") -notmatch 'bounded terminal-route uncertainty: 1 retained gaps') { throw 'Bounded uncertainty not separated.' }
    $global:WizardRequeryCause = $null
    foreach ($reason in @('work', 'depth', 'cycle', 'path', 'frontier')) {
        $global:WizardRequeryReason = $reason
        $global:WizardRequeryWork = 37
        $global:WizardRequeryWorkLimit = 100
        $display = @(& $helper $temporary -Handler Page_Load) -join "`n"
        if (!$display.Contains("cutoffReason=${reason}; retainedGaps=1") -or
            !$display.Contains('Traversal work: measured=37; configuredLimit=100')) { throw 'Missing actual work/cutoff evidence.' }
        if ($display.Contains('No work-limit cutoff is recorded') -eq ($reason -eq 'work')) { throw 'Work exhaustion inferred from the wrong evidence.' }
    }
    $global:WizardRequeryReason = 'depth'
    $global:WizardRequeryWork = $null
    $global:WizardRequeryWorkLimit = $null
    $display = @(& $helper $temporary -Handler Page_Load) -join "`n"
    if (!$display.Contains('measured=unavailable; configuredLimit=unavailable')) { throw 'Unrecorded work must remain unknown.' }
    $graphDisplay = @(& $helper $temporary -Handler Page_Load -MethodGraph)
    $graphCall = $global:WizardRequeryCalls[$global:WizardRequeryCalls.Count - 1]
    if ($graphCall[-2] -ne '--view' -or $graphCall[-1] -ne 'method-graph' -or
        ($graphDisplay -join "`n") -notmatch 'method-graph.local.html' -or
        ($graphDisplay -join "`n") -notmatch 'Terminal path enumeration was not run') { throw 'Graph mode was not forwarded or labeled.' }
    Expect-Failure { & $helper $temporary -MethodGraph -InspectLatest } 'cannot be combined'
    $graphInspection = @(& $helper $temporary -InspectLatest) -join "`n"
    if (!$graphInspection.Contains('Method graph: nodes=0') -or !$graphInspection.Contains('Terminal path enumeration was not run')) { throw 'Latest graph report excluded or misinterpreted.' }
    $graphDirectory = $graphCall[10]
    $graphReceiptPath = Join-Path $graphDirectory 'handler-requery.local.json'
    $validReceipt = Get-Content $graphReceiptPath -Raw
    foreach ($statusRunId in @(('a'*32), ('A'*32))) {
        $global:WizardRunId = $statusRunId
        foreach ($receiptRunId in @(('a'*32), ('A'*32), ('aA'*16))) {
            $cased = $validReceipt | ConvertFrom-Json
            $cased.runId = $receiptRunId
            $cased | ConvertTo-Json -Depth 8 | Set-Content $graphReceiptPath
            & $helper $temporary -InspectLatest | Out-Null
        }
    }
    $global:WizardRunId = 'a' * 32
    foreach ($invalid in @(('a'*31), ('g'*32), ' aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')) {
        $bad = $validReceipt | ConvertFrom-Json
        $bad.runId = $invalid
        $bad | ConvertTo-Json -Depth 8 | Set-Content $graphReceiptPath
        Expect-Failure { & $helper $temporary -InspectLatest } 'SUMMARY_RUN_MISMATCH'
    }
    foreach ($field in @('sourcePreflightSha256','sourceCheckpointSha256','combinedIndexSha256')) {
        $bad = $validReceipt | ConvertFrom-Json
        $bad.$field = $bad.$field.ToUpperInvariant()
        $bad | ConvertTo-Json -Depth 8 | Set-Content $graphReceiptPath
        Expect-Failure { & $helper $temporary -InspectLatest } 'SUMMARY_RUN_MISMATCH'
    }
    foreach ($field in @('runId','sourcePreflightSha256','sourceCheckpointSha256','combinedIndexSha256')) {
        $bad = $validReceipt | ConvertFrom-Json
        $bad.$field = 'e' * $bad.$field.Length
        $bad | ConvertTo-Json -Depth 8 | Set-Content $graphReceiptPath
        Expect-Failure { & $helper $temporary -InspectLatest } 'SUMMARY_RUN_MISMATCH'
    }
    Set-Content $graphReceiptPath $validReceipt
    $graphPath = Join-Path $graphDirectory 'method-graph.local.json'
    $validGraph = Get-Content $graphPath -Raw
    $badGraph = $validGraph | ConvertFrom-Json
    $badGraph.indexSha256 = 'e' * 64
    $badGraph | ConvertTo-Json -Depth 8 | Set-Content $graphPath
    $bad = $validReceipt | ConvertFrom-Json
    $artifact = $bad.artifacts | Where-Object relativePath -CEQ 'method-graph.local.json'
    $artifact.bytes = (Get-Item $graphPath).Length
    $artifact.sha256 = (Get-FileHash $graphPath).Hash.ToLowerInvariant()
    $bad | ConvertTo-Json -Depth 8 | Set-Content $graphReceiptPath
    Expect-Failure { & $helper $temporary -InspectLatest } 'SUMMARY_RUN_MISMATCH'
    [IO.File]::WriteAllText($graphPath, $validGraph)
    Set-Content $graphReceiptPath $validReceipt
    foreach ($mutation in @(
        { param($r) $r.schemaVersion = @('webforms-handler-requery.v1') },
        { param($r) $r.primaryReport = @('method-graph.local.html') },
        { param($r) $r.reportVariant = 'unknown' },
        { param($r) $r.exactChains = '0' },
        { param($r) $r.pathEnumerationPerformed = 'false' },
        { param($r) $r.artifacts = $r.artifacts[0] },
        { param($r) $r.artifacts[0].bytes = [string]$r.artifacts[0].bytes },
        { param($r) $r.artifacts[0].relativePath = 12 },
        { param($r) $r.artifacts[0].sha256 = @('hash') }
    )) {
        $bad = $validReceipt | ConvertFrom-Json
        & $mutation $bad
        $bad | ConvertTo-Json -Depth 8 | Set-Content $graphReceiptPath
        Expect-Failure { Write-WebFormsTruncationSummary $graphDirectory } 'SUMMARY_RECEIPT_INVALID'
    }
    Set-Content $graphReceiptPath $validReceipt
    . (Join-Path $PSScriptRoot '../WebFormsGapClassification.ps1')
    Expect-Failure { Read-WebFormsClassificationHandoff $graphDirectory } 'REQUIRES_GROUPED_PATHS'
    foreach ($depth in @(1, 32, 48, 64)) {
        & $helper $temporary -Handler Page_Load -MaxDepth $depth | Out-Null
        $depthCall = $global:WizardRequeryCalls[$global:WizardRequeryCalls.Count - 1]
        if ($depthCall[-2] -ne '--max-depth' -or $depthCall[-1] -ne [string]$depth) { throw 'Depth override not forwarded.' }
    }
    Expect-Failure { & $helper $temporary -Handler Page_Load -MaxDepth 0 } 'MaxDepth'
    Expect-Failure { & $helper $temporary -Handler Page_Load -MaxDepth 65 } 'MaxDepth'
    Expect-Failure { & $helper $temporary -MaxDepth 32 -InspectLatest } 'cannot be combined'
    $beforeInspect = $global:WizardRequeryCalls.Count
    $inspection = @(& $helper $temporary -InspectLatest)
    if ($global:WizardRequeryCalls.Count -ne $beforeInspect + 1 -or
        ($inspection -join "`n") -notmatch 'Inspecting latest completed report') { throw 'Inspection must only call status, not traverse.' }
    $groupedDirectory = $depthCall[10]
    $groupedReceiptPath = Join-Path $groupedDirectory 'handler-requery.local.json'
    $groupedPath = Join-Path $groupedDirectory 'compiled-paths.handoff.local.json'
    $groupedReceipt = Get-Content $groupedReceiptPath -Raw
    # Legacy grouped receipts omit reportVariant, not their original run identities.
    $legacy = $groupedReceipt | ConvertFrom-Json
    if ($legacy.PSObject.Properties['reportVariant']) { throw 'Fixture must exercise legacy grouped receipt admission.' }
    foreach ($field in @('runId','sourcePreflightSha256','sourceCheckpointSha256','combinedIndexSha256')) {
        $stripped = $groupedReceipt | ConvertFrom-Json
        $stripped.PSObject.Properties.Remove($field)
        $stripped | ConvertTo-Json -Depth 8 | Set-Content $groupedReceiptPath
        Expect-Failure { & $helper $temporary -InspectLatest } 'SUMMARY_RUN_MISMATCH'
    }
    Set-Content $groupedReceiptPath $groupedReceipt
    $groupedJson = Get-Content $groupedPath -Raw
    $wrongIndex = $groupedJson | ConvertFrom-Json -Depth 32
    $wrongIndex.inputIndexSha256 = 'e' * 64
    $wrongIndex | ConvertTo-Json -Depth 32 | Set-Content $groupedPath
    $boundReceipt = $groupedReceipt | ConvertFrom-Json
    $boundReceipt.artifacts[0].bytes = (Get-Item $groupedPath).Length
    $boundReceipt.artifacts[0].sha256 = (Get-FileHash $groupedPath).Hash.ToLowerInvariant()
    $boundReceipt | ConvertTo-Json -Depth 8 | Set-Content $groupedReceiptPath
    Expect-Failure { & $helper $temporary -InspectLatest } 'SUMMARY_RUN_MISMATCH'
    [IO.File]::WriteAllText($groupedPath, $groupedJson)
    Set-Content $groupedReceiptPath $groupedReceipt
    $global:WizardRequeryState = 'failed'
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'COMPLETED_STATE_NOT_ADMITTED'
    $global:WizardRequeryState = 'reports-completed-review-only'
    $global:WizardRequeryOriginalReader = $false
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'COMPLETED_STATE_NOT_ADMITTED'
    $updated = @(& $helper $temporary -Handler Page_Load -AllowUpdatedReader)
    if (($updated -join "`n") -notmatch 'Updated reader explicitly allowed') { throw 'Missing reader provenance notice.' }
    $global:WizardRequeryState = 'failed'
    Expect-Failure { & $helper $temporary -Handler Page_Load -AllowUpdatedReader } 'COMPLETED_STATE_NOT_ADMITTED'
    $global:WizardRequeryState = 'reports-completed-review-only'
    $global:WizardRequeryOriginalReader = $true
    $global:WizardRequeryExit = 1
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'REQUERY_FAILED'
    $global:WizardRequeryExit = 0
    Expect-Failure { & $helper $temporary -Handler Page_Load -LatestRefresh } 'REFRESH_SELECTION_UNAVAILABLE'
    $refresh = Join-Path $temporary ('refresh-sample-' + ('b' * 32))
    [IO.Directory]::CreateDirectory((Join-Path $refresh 'run')) | Out-Null
    $global:WizardRequeryCalls.Clear()
    & $helper $temporary -Handler Page_Load -LatestRefresh | Out-Null
    if ($global:WizardRequeryCalls[0][4] -cne (Join-Path $refresh 'run') -or
        $global:WizardRequeryCalls[1][4] -cne (Join-Path $refresh 'run')) { throw 'Refresh query used old run.' }
    $global:WizardRequeryState = 'failed'
    $global:WizardRequeryCalls.Clear()
    Expect-Failure { & $helper $temporary -Handler Page_Load -LatestRefresh } 'COMPLETED_STATE_NOT_ADMITTED'
    if ($global:WizardRequeryCalls.Count -ne 1) { throw 'Failed refresh fell back or queried.' }
    $global:WizardRequeryState = 'reports-completed-review-only'
    Expect-Failure { & $helper $temporary -Project absent -Handler Page_Load } 'PROJECT_NOT_FOUND'
    Expect-Failure { & $helper $temporary -Handler "Page_Load`n" } 'HANDLER_INVALID'
    Save-Fixture '../outside'
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'RUN_LOCATOR_INVALID'
    Save-Fixture
    Add-Content (Join-Path $temporary 'sample/project.config.json') ' '
    Expect-Failure { & $helper $temporary -Handler Page_Load } 'PROJECT_HASH_MISMATCH'
    Write-Output 'Web Forms wizard requery helper tests passed.'
} finally {
    Remove-Item Function:\dotnet
    Remove-Variable WizardRequeryCalls,WizardRequeryState,WizardRequeryExit,WizardRequeryWorkbench,WizardRequeryOriginalReader,WizardRequeryCause,WizardRequeryReason,WizardRequeryWork,WizardRequeryWorkLimit,WizardRunId -Scope Global
    [IO.Directory]::Delete($temporary, $true)
}
