[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WebReviewRoot,
    [Parameter(Mandatory = $true)][string]$BackendReviewRoot,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_MERGE_POWERSHELL_7_REQUIRED' }

function Get-BoundedFileHash {
    param([string]$Path, [long]$MaximumBytes = 17179869184)
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "WEBFORMS_MERGE_ARTIFACT_UNAVAILABLE;path=$Path" }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -lt 0 -or $file.Length -gt $MaximumBytes) { throw "WEBFORMS_MERGE_ARTIFACT_LIMIT;path=$Path" }
    [pscustomobject]@{
        Path = $file.FullName
        Bytes = [long]$file.Length
        Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function New-ArtifactReceipt {
    param([string]$Root, [string[]]$Paths)
    @($Paths | ForEach-Object {
        $hash = Get-BoundedFileHash $_
        [ordered]@{
            path = [IO.Path]::GetRelativePath($Root, $hash.Path).Replace('\', '/')
            bytes = $hash.Bytes
            sha256 = $hash.Sha256
            canonicalization = 'raw-file-bytes'
        }
    })
}

function Write-RunReceipt {
    param([string]$Path, [object]$Value)
    $Value.run.updatedUtc = [DateTime]::UtcNow.ToString('O')
    $temporary = "$Path.tmp-$([Guid]::NewGuid().ToString('N'))"
    [IO.File]::WriteAllText($temporary, (($Value | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temporary, $Path, $true)
}

function Read-ScanInput {
    param([string]$ReviewRoot, [string]$Label)
    $root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
    $receiptPath = Join-Path $root 'run-receipt.json'
    $receiptHash = Get-BoundedFileHash $receiptPath 16MB
    $receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 20
    if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or
        $receipt.run.state -notin @('completed', 'failed') -or
        $receipt.stages.scan.state -ne 'completed') {
        throw "WEBFORMS_MERGE_SCAN_RECEIPT_INVALID;label=$Label"
    }
    $indexArtifact = @($receipt.stages.scan.artifacts | Where-Object { [string]$_.path -eq 'scan/index.sqlite' })
    if ($indexArtifact.Count -ne 1) { throw "WEBFORMS_MERGE_SCAN_INDEX_RECEIPT_INVALID;label=$Label" }
    $indexPath = Join-Path $root 'scan/index.sqlite'
    $indexHash = Get-BoundedFileHash $indexPath
    if ($indexHash.Bytes -ne [long]$indexArtifact[0].bytes -or $indexHash.Sha256 -ne [string]$indexArtifact[0].sha256) {
        throw "WEBFORMS_MERGE_SCAN_INDEX_MISMATCH;label=$Label"
    }
    $sourceRoot = [IO.Path]::GetFullPath([string]$receipt.source.root).TrimEnd('\', '/')
    $currentCommit = ([string](git -C $sourceRoot rev-parse HEAD)).Trim().ToLowerInvariant()
    if ($LASTEXITCODE -ne 0 -or $currentCommit -ne [string]$receipt.source.commitSha) {
        throw "WEBFORMS_MERGE_SOURCE_COMMIT_MISMATCH;label=$Label"
    }
    [pscustomobject]@{
        Label = $Label
        ReviewRoot = $root
        ReceiptPath = $receiptPath
        ReceiptSha256 = $receiptHash.Sha256
        IndexPath = $indexPath
        IndexSha256 = $indexHash.Sha256
        IndexBytes = $indexHash.Bytes
        SourceRoot = $sourceRoot
        CommitSha = [string]$receipt.source.commitSha
    }
}

$web = Read-ScanInput $WebReviewRoot 'web'
$backend = Read-ScanInput $BackendReviewRoot 'backend'
if ($web.ReviewRoot.Equals($backend.ReviewRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'WEBFORMS_MERGE_INPUTS_MUST_BE_DISTINCT' }
$root = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\', '/')
if ($root.Equals($web.ReviewRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $root.Equals($backend.ReviewRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'WEBFORMS_MERGE_OUTPUT_MUST_BE_DISTINCT' }
if (Test-Path -LiteralPath $root) {
    if (!(Test-Path -LiteralPath $root -PathType Container) -or @(Get-ChildItem -LiteralPath $root -Force).Count -ne 0) {
        throw 'WEBFORMS_MERGE_OUTPUT_MUST_BE_EMPTY'
    }
}
else { [void][IO.Directory]::CreateDirectory($root) }

$traceRoot = [IO.Path]::GetFullPath($TraceMapRoot).TrimEnd('\', '/')
$traceCommit = ([string](git -C $traceRoot rev-parse HEAD)).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $traceCommit -notmatch '^[0-9a-f]{40}$') { throw 'WEBFORMS_MERGE_TRACEMAP_COMMIT_UNAVAILABLE' }
$generatorHash = Get-BoundedFileHash $PSCommandPath 4MB
$started = [DateTime]::UtcNow
$receiptPath = Join-Path $root 'run-receipt.json'
$receipt = [pscustomobject][ordered]@{
    schemaVersion = 'focused-webforms-review-run-receipt.v1'
    ruleId = 'diagnostic.webforms.review-pipeline-receipt.v1'
    claimLevel = 'local-only'
    provenance = [pscustomobject][ordered]@{
        generator = 'scripts/Merge-FocusedWebFormsReview.ps1'
        generatorSha256 = $generatorHash.Sha256
        generatorCanonicalization = 'raw-file-bytes'
        inputKind = 'two-focused-webforms-scan-receipts'
        inputCanonicalization = 'raw-file-bytes'
    }
    run = [pscustomobject][ordered]@{ runId = "webforms-merged-$($started.ToString('yyyyMMddTHHmmssZ'))"; startedUtc = $started.ToString('O'); updatedUtc = $started.ToString('O'); state = 'running'; failure = '' }
    source = [pscustomobject][ordered]@{ root = $web.SourceRoot; commitSha = $web.CommitSha }
    sources = @(
        [pscustomobject][ordered]@{ label = 'web'; root = $web.SourceRoot; commitSha = $web.CommitSha; receiptSha256 = $web.ReceiptSha256; indexSha256 = $web.IndexSha256 }
        [pscustomobject][ordered]@{ label = 'backend'; root = $backend.SourceRoot; commitSha = $backend.CommitSha; receiptSha256 = $backend.ReceiptSha256; indexSha256 = $backend.IndexSha256 }
    )
    traceMap = [pscustomobject][ordered]@{ root = $traceRoot; commitSha = $traceCommit }
    layout = [pscustomobject][ordered]@{ config = ''; scan = 'combined'; packet = 'packet'; evidenceDocs = 'evidence-docs'; workbench = 'workbench'; logs = 'logs' }
    selection = [pscustomobject][ordered]@{ projectMode = 'combined'; pageMode = 'all'; runMode = 'merge' }
    stages = [pscustomobject][ordered]@{
        build = [pscustomobject][ordered]@{ state = 'pending'; completedUtc = $null; failure = ''; artifacts = @() }
        scan = [pscustomobject][ordered]@{ state = 'pending'; completedUtc = $null; failure = ''; artifacts = @() }
        packet = [pscustomobject][ordered]@{ state = 'pending'; completedUtc = $null; failure = ''; artifacts = @() }
        evidenceDocs = [pscustomobject][ordered]@{ state = 'pending'; completedUtc = $null; failure = ''; artifacts = @() }
        workbench = [pscustomobject][ordered]@{ state = 'pending'; completedUtc = $null; failure = ''; artifacts = @() }
    }
    retention = [pscustomobject][ordered]@{ required = @('run-receipt.json','combined','packet','evidence-docs','workbench'); diagnostic = @('logs'); shareableOnlyWhenExplicitlyNamed = @('*.shareable.html','*.shareable.json') }
}
Write-RunReceipt $receiptPath $receipt
$activeStage = ''
try {
    $activeStage = 'build'
    $receipt.stages.build.state = 'running'; Write-RunReceipt $receiptPath $receipt
    dotnet build (Join-Path $traceRoot 'src/dotnet/TraceMap.sln') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_MERGE_BUILD_FAILED' }
    $receipt.stages.build.state = 'completed'; $receipt.stages.build.completedUtc = [DateTime]::UtcNow.ToString('O'); Write-RunReceipt $receiptPath $receipt

    $cliDll = Join-Path $traceRoot 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
    $combinedDirectory = Join-Path $root 'combined'
    [void][IO.Directory]::CreateDirectory($combinedDirectory)
    $combinedIndex = Join-Path $combinedDirectory 'index.sqlite'
    $activeStage = 'scan'
    $receipt.stages.scan.state = 'running'; Write-RunReceipt $receiptPath $receipt
    dotnet $cliDll combine --index $web.IndexPath --label web --index $backend.IndexPath --label backend --out $combinedIndex
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_MERGE_COMBINE_FAILED' }
    $receipt.stages.scan.state = 'completed'; $receipt.stages.scan.completedUtc = [DateTime]::UtcNow.ToString('O'); $receipt.stages.scan.artifacts = New-ArtifactReceipt $root @($combinedIndex); Write-RunReceipt $receiptPath $receipt

    $packetDirectory = Join-Path $root 'packet'
    $packetPath = Join-Path $packetDirectory 'webforms-modernization.json'
    $activeStage = 'packet'
    $receipt.stages.packet.state = 'running'; Write-RunReceipt $receiptPath $receipt
    dotnet $cliDll webforms-modernization --index $combinedIndex --out $packetDirectory --max-surfaces 1000 --max-event-chains 10000 --max-paths 10000 --max-boundaries 10000 --max-gaps 20000 --max-identity-state 10000 --max-batch-data-movement 10000 --max-traversal-work 1000000
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_MERGE_PACKET_FAILED' }
    $receipt.stages.packet.state = 'completed'; $receipt.stages.packet.completedUtc = [DateTime]::UtcNow.ToString('O'); $receipt.stages.packet.artifacts = New-ArtifactReceipt $root @($packetPath, (Join-Path $packetDirectory 'webforms-modernization.md')); Write-RunReceipt $receiptPath $receipt

    $docsDirectory = Join-Path $root 'evidence-docs'
    $activeStage = 'evidenceDocs'
    $receipt.stages.evidenceDocs.state = 'running'; Write-RunReceipt $receiptPath $receipt
    dotnet $cliDll docs-export --index $combinedIndex --webforms-packet $packetPath --families 'webforms-modernization,gap,limitation' --out $docsDirectory --format 'markdown,jsonl'
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_MERGE_EVIDENCE_DOCS_FAILED' }
    $receipt.stages.evidenceDocs.state = 'completed'; $receipt.stages.evidenceDocs.completedUtc = [DateTime]::UtcNow.ToString('O'); $receipt.stages.evidenceDocs.artifacts = New-ArtifactReceipt $root @((Join-Path $docsDirectory 'manifest.json'), (Join-Path $docsDirectory 'query-recipes.json'), (Join-Path $docsDirectory 'chunks.jsonl')); Write-RunReceipt $receiptPath $receipt

    $workbenchDirectory = Join-Path $root 'workbench'
    $activeStage = 'workbench'
    $receipt.stages.workbench.state = 'running'; Write-RunReceipt $receiptPath $receipt
    & (Join-Path $PSScriptRoot 'New-FocusedWebFormsApplicationWorkbench.ps1') -PacketPath $packetPath -OutputRoot $root -OutputDirectory $workbenchDirectory -EvidenceDocsRoot $docsDirectory
    $receipt.stages.workbench.state = 'completed'; $receipt.stages.workbench.completedUtc = [DateTime]::UtcNow.ToString('O'); $receipt.stages.workbench.artifacts = New-ArtifactReceipt $root @((Join-Path $workbenchDirectory 'index.html'), (Join-Path $workbenchDirectory 'application-handoff.json'), (Join-Path $workbenchDirectory 'application-outliers.shareable.html'), (Join-Path $workbenchDirectory 'application-outliers.shareable.json')); Write-RunReceipt $receiptPath $receipt

    $receipt.run.state = 'completed'; $receipt.run.failure = ''; Write-RunReceipt $receiptPath $receipt
    Write-Host "webformsPipeline=merged-completed;runId=$($receipt.run.runId)"
    Write-Host "workbenchIndex=$(Join-Path $workbenchDirectory 'index.html')"
    Write-Host "runReceipt=$receiptPath"
}
catch {
    $receipt.run.state = 'failed'; $receipt.run.failure = $_.Exception.Message
    if ($activeStage) {
        $stage = $receipt.stages.PSObject.Properties[$activeStage].Value
        $stage.state = 'failed'; $stage.failure = $_.Exception.Message
    }
    Write-RunReceipt $receiptPath $receipt
    throw
}
