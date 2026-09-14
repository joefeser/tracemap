[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent),
    [ValidateRange(30, 86400)][int]$TimeoutSeconds = 14400
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_PIPELINE_POWERSHELL_7_REQUIRED' }

. (Join-Path $PSScriptRoot 'webforms-review/FocusedWebFormsPipelineConfig.ps1')

function Get-BoundedFileHash {
    param([string]$Path, [long]$MaximumBytes = 2147483648)
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "WEBFORMS_PIPELINE_ARTIFACT_UNAVAILABLE;path=$Path" }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -lt 0 -or $file.Length -gt $MaximumBytes) { throw "WEBFORMS_PIPELINE_ARTIFACT_LIMIT;path=$Path" }
    return [pscustomobject]@{
        Path = $file.FullName
        Bytes = [long]$file.Length
        Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function New-ArtifactReceipt {
    param([string]$Root, [string[]]$Paths)
    return @($Paths | ForEach-Object {
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

function Assert-CompletedStage {
    param([string]$Root, [object]$Stage, [string]$Name)
    if ($Stage.state -ne 'completed') { return $false }
    foreach ($artifact in @($Stage.artifacts)) {
        $path = Join-Path $Root ([string]$artifact.path)
        $hash = Get-BoundedFileHash $path
        if ($hash.Bytes -ne [long]$artifact.bytes -or $hash.Sha256 -ne [string]$artifact.sha256) {
            throw "WEBFORMS_PIPELINE_RESUME_ARTIFACT_MISMATCH;stage=$Name;path=$($artifact.path)"
        }
    }
    Write-Host "pipelineStage=$Name;state=reused"
    return $true
}

function Set-StageState {
    param([object]$Receipt, [string]$Name, [string]$State, [object[]]$Artifacts = @(), [string]$Failure = '')
    $stage = $Receipt.stages.PSObject.Properties[$Name].Value
    $stage.state = $State
    $stage.artifacts = @($Artifacts)
    $stage.failure = $Failure
    if ($State -eq 'completed') { $stage.completedUtc = [DateTime]::UtcNow.ToString('O') }
}

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
$traceRoot = [IO.Path]::GetFullPath($TraceMapRoot).TrimEnd('\', '/')
$configPath = Join-Path $root 'config/webforms-review.json'
$receiptPath = Join-Path $root 'run-receipt.json'
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'WEBFORMS_PIPELINE_REVIEW_ROOT_UNAVAILABLE' }
$config = Read-FocusedWebFormsPipelineConfig -ConfigPath $configPath
$configuredRoot = [IO.Path]::GetFullPath($config.OutputRoot).TrimEnd('\', '/')
if (!$configuredRoot.Equals($root, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'WEBFORMS_PIPELINE_OUTPUT_ROOT_MUST_EQUAL_REVIEW_ROOT'
}

$configHash = Get-BoundedFileHash $configPath 1MB
$generatorHash = Get-BoundedFileHash $PSCommandPath 4MB
$sourceRoot = [IO.Path]::GetFullPath($config.SourceRoot).TrimEnd('\', '/')
if (!(Test-Path -LiteralPath $sourceRoot -PathType Container)) { throw 'WEBFORMS_PIPELINE_SOURCE_ROOT_UNAVAILABLE' }
$sourceCommit = ([string](git -C $sourceRoot rev-parse HEAD)).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') { throw 'WEBFORMS_PIPELINE_SOURCE_COMMIT_UNAVAILABLE' }
$traceCommit = ([string](git -C $traceRoot rev-parse HEAD)).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $traceCommit -notmatch '^[0-9a-f]{40}$') { throw 'WEBFORMS_PIPELINE_TRACEMAP_COMMIT_UNAVAILABLE' }

if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
    $receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 20
    if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or
        $receipt.provenance.configSha256 -ne $configHash.Sha256 -or
        $receipt.provenance.generatorSha256 -ne $generatorHash.Sha256 -or
        $receipt.source.commitSha -ne $sourceCommit -or
        $receipt.traceMap.commitSha -ne $traceCommit) {
        throw 'WEBFORMS_PIPELINE_RESUME_PROVENANCE_MISMATCH'
    }
    Write-Host "pipelineRun=$($receipt.run.runId);state=resuming"
}
else {
    $started = [DateTime]::UtcNow
    $runId = 'webforms-review-{0}-{1}' -f $started.ToString('yyyyMMddTHHmmssZ'), $configHash.Sha256.Substring(0, 8)
    $receipt = [pscustomobject][ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        ruleId = 'diagnostic.webforms.review-pipeline-receipt.v1'
        claimLevel = 'local-only'
        provenance = [pscustomobject][ordered]@{
            generator = 'scripts/Invoke-FocusedWebFormsPipeline.ps1'
            generatorSha256 = $generatorHash.Sha256
            generatorCanonicalization = 'raw-file-bytes'
            inputKind = 'focused-webforms-review-config.v1'
            configSha256 = $configHash.Sha256
            inputCanonicalization = 'raw-file-bytes'
        }
        run = [pscustomobject][ordered]@{ runId = $runId; startedUtc = $started.ToString('O'); updatedUtc = $started.ToString('O'); state = 'running'; failure = '' }
        source = [pscustomobject][ordered]@{ root = $sourceRoot; commitSha = $sourceCommit }
        traceMap = [pscustomobject][ordered]@{ root = $traceRoot; commitSha = $traceCommit }
        layout = [pscustomobject][ordered]@{ config = 'config/webforms-review.json'; scan = 'scan'; packet = 'packet'; evidenceDocs = 'evidence-docs'; workbench = 'workbench'; logs = 'logs' }
        selection = [pscustomobject][ordered]@{ projectMode = $config.ProjectMode; pageMode = $config.PageMode }
        stages = [pscustomobject][ordered]@{
            build = [pscustomobject][ordered]@{ state = 'pending'; completedUtc = $null; failure = ''; artifacts = @() }
            scan = [pscustomobject][ordered]@{ state = 'pending'; completedUtc = $null; failure = ''; artifacts = @() }
            packet = [pscustomobject][ordered]@{ state = 'pending'; completedUtc = $null; failure = ''; artifacts = @() }
            evidenceDocs = [pscustomobject][ordered]@{ state = 'pending'; completedUtc = $null; failure = ''; artifacts = @() }
            workbench = [pscustomobject][ordered]@{ state = 'pending'; completedUtc = $null; failure = ''; artifacts = @() }
        }
        retention = [pscustomobject][ordered]@{
            required = @('config','run-receipt.json','scan','packet','evidence-docs','workbench')
            diagnostic = @('logs')
            shareableOnlyWhenExplicitlyNamed = @('*.shareable.html','*.shareable.json')
        }
    }
    Write-RunReceipt $receiptPath $receipt
    Write-Host "pipelineRun=$runId;state=started"
}

$activeStage = ''
try {
    $solution = Join-Path $traceRoot 'src/dotnet/TraceMap.sln'
    if (!(Assert-CompletedStage $root $receipt.stages.build 'build')) {
        $activeStage = 'build'
        Set-StageState $receipt $activeStage 'running'
        Write-RunReceipt $receiptPath $receipt
        dotnet build $solution --nologo
        if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_PIPELINE_BUILD_FAILED' }
        Set-StageState $receipt 'build' 'completed'
        Write-RunReceipt $receiptPath $receipt
        Write-Host 'pipelineStage=build;state=completed'
    }

    $scanPath = Join-Path $root 'scan'
    $localReviewResult = Join-Path $root 'logs/local-review-result.json'
    if (!(Assert-CompletedStage $root $receipt.stages.scan 'scan')) {
        $activeStage = 'scan'
        Set-StageState $receipt $activeStage 'running'
        Write-RunReceipt $receiptPath $receipt
        if (Test-Path -LiteralPath $scanPath) { throw 'WEBFORMS_PIPELINE_SCAN_OUTPUT_ALREADY_EXISTS' }
        $scanStaging = Join-Path $root '.scan-stage'
        if (Test-Path -LiteralPath $scanStaging) { throw 'WEBFORMS_PIPELINE_SCAN_STAGING_REMAINS' }
        $scanArgs = @{
            SourceRoot = $sourceRoot
            WebFormsFolder = $config.WebFormsFolder
            BackendFolder = $config.BackendFolder
            ControlsFolder = $config.ControlsFolder
            TraceMapRoot = $traceRoot
            TimeoutSeconds = $TimeoutSeconds
            OutputDirectory = $scanStaging
            ProgressPath = (Join-Path $root 'logs/progress.json')
            SummaryDirectory = (Join-Path $root 'logs')
            SkipBuild = $true
            NoExit = $true
        }
        switch ($config.ProjectMode) {
            'solution' { $scanArgs.SolutionRelativePath = $config.SolutionRelativePath }
            'projects' { $scanArgs.ProjectRelativePath = $config.ProjectRelativePaths }
            'discover' { $scanArgs.DiscoverProjects = $true }
            'projectless' { $scanArgs.Projectless = $true }
        }
        & (Join-Path $PSScriptRoot 'Invoke-FocusedWebFormsReview.ps1') @scanArgs
        [IO.Directory]::Move((Join-Path $scanStaging 'scan'), $scanPath)
        foreach ($name in @('local-review-result.json','README.md')) {
            $candidate = Join-Path $scanStaging $name
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $destinationName = if ($name -eq 'README.md') { 'local-review.README.md' } else { $name }
                [IO.File]::Move($candidate, (Join-Path $root "logs/$destinationName"), $true)
            }
        }
        if (Test-Path -LiteralPath $scanStaging) { Remove-Item -LiteralPath $scanStaging -Recurse -Force }
        $scanArtifacts = @(
            (Join-Path $scanPath 'scan-manifest.json'),
            (Join-Path $scanPath 'facts.ndjson'),
            (Join-Path $scanPath 'index.sqlite'),
            (Join-Path $scanPath 'report.md'),
            (Join-Path $scanPath 'logs/analyzer.log'),
            $localReviewResult
        )
        Set-StageState $receipt 'scan' 'completed' (New-ArtifactReceipt $root $scanArtifacts)
        Write-RunReceipt $receiptPath $receipt
        Write-Host 'pipelineStage=scan;state=completed'
    }

    $cliDll = Join-Path $traceRoot 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
    $indexPath = Join-Path $scanPath 'index.sqlite'
    $packetDirectory = Join-Path $root 'packet'
    $packetPath = Join-Path $packetDirectory 'webforms-modernization.json'
    if (!(Assert-CompletedStage $root $receipt.stages.packet 'packet')) {
        $activeStage = 'packet'
        Set-StageState $receipt $activeStage 'running'
        Write-RunReceipt $receiptPath $receipt
        if (Test-Path -LiteralPath $packetDirectory) { throw 'WEBFORMS_PIPELINE_PACKET_OUTPUT_ALREADY_EXISTS' }
        $packetArguments = @($cliDll, 'webforms-modernization', '--index', $indexPath, '--out', $packetDirectory,
            '--max-surfaces', '1000', '--max-event-chains', '10000', '--max-paths', '10000', '--max-boundaries', '10000',
            '--max-gaps', '20000', '--max-identity-state', '10000', '--max-batch-data-movement', '10000', '--max-traversal-work', '1000000')
        if ($config.PageMode -eq 'selected') {
            $pageListPath = Join-Path $root 'config/selected-pages.txt'
            [IO.File]::WriteAllLines($pageListPath, $config.Forms, [Text.UTF8Encoding]::new($false))
            $packetArguments += @('--surface-list', $pageListPath)
        }
        dotnet @packetArguments
        if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_PIPELINE_PACKET_FAILED' }
        Set-StageState $receipt 'packet' 'completed' (New-ArtifactReceipt $root @($packetPath, (Join-Path $packetDirectory 'webforms-modernization.md')))
        Write-RunReceipt $receiptPath $receipt
        Write-Host 'pipelineStage=packet;state=completed'
    }

    $docsDirectory = Join-Path $root 'evidence-docs'
    if (!(Assert-CompletedStage $root $receipt.stages.evidenceDocs 'evidenceDocs')) {
        $activeStage = 'evidenceDocs'
        Set-StageState $receipt $activeStage 'running'
        Write-RunReceipt $receiptPath $receipt
        if (Test-Path -LiteralPath $docsDirectory) { throw 'WEBFORMS_PIPELINE_EVIDENCE_DOCS_OUTPUT_ALREADY_EXISTS' }
        dotnet $cliDll docs-export --index $indexPath --webforms-packet $packetPath --families 'webforms-modernization,gap,limitation' --out $docsDirectory --format 'markdown,jsonl'
        if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_PIPELINE_EVIDENCE_DOCS_FAILED' }
        Set-StageState $receipt 'evidenceDocs' 'completed' (New-ArtifactReceipt $root @(
            (Join-Path $docsDirectory 'manifest.json'),
            (Join-Path $docsDirectory 'query-recipes.json'),
            (Join-Path $docsDirectory 'chunks.jsonl')))
        Write-RunReceipt $receiptPath $receipt
        Write-Host 'pipelineStage=evidenceDocs;state=completed'
    }

    $workbenchDirectory = Join-Path $root 'workbench'
    if (!(Assert-CompletedStage $root $receipt.stages.workbench 'workbench')) {
        $activeStage = 'workbench'
        Set-StageState $receipt $activeStage 'running'
        Write-RunReceipt $receiptPath $receipt
        if (Test-Path -LiteralPath $workbenchDirectory) { throw 'WEBFORMS_PIPELINE_WORKBENCH_OUTPUT_ALREADY_EXISTS' }
        & (Join-Path $PSScriptRoot 'New-FocusedWebFormsApplicationWorkbench.ps1') `
            -PacketPath $packetPath `
            -OutputRoot $root `
            -OutputDirectory $workbenchDirectory `
            -EvidenceDocsRoot $docsDirectory
        Set-StageState $receipt 'workbench' 'completed' (New-ArtifactReceipt $root @(
            (Join-Path $workbenchDirectory 'index.html'),
            (Join-Path $workbenchDirectory 'application-handoff.json'),
            (Join-Path $workbenchDirectory 'application-outliers.shareable.html'),
            (Join-Path $workbenchDirectory 'application-outliers.shareable.json')))
        Write-RunReceipt $receiptPath $receipt
        Write-Host 'pipelineStage=workbench;state=completed'
    }

    $receipt.run.state = 'completed'
    $receipt.run.failure = ''
    Write-RunReceipt $receiptPath $receipt
    Write-Host "webformsPipeline=completed;runId=$($receipt.run.runId)"
    Write-Host "workbenchIndex=$(Join-Path $workbenchDirectory 'index.html')"
    Write-Host "runReceipt=$receiptPath"
}
catch {
    $receipt.run.state = 'failed'
    $receipt.run.failure = $_.Exception.Message
    if ($activeStage) { Set-StageState $receipt $activeStage 'failed' @() $_.Exception.Message }
    Write-RunReceipt $receiptPath $receipt
    throw
}
