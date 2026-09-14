$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$pipelinePath = Join-Path $scripts 'Invoke-FocusedWebFormsPipeline.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-oversize-recovery-' + [Guid]::NewGuid().ToString('N'))
$originalDotnet = Get-Command dotnet -ErrorAction Stop

try {
    $source = Join-Path $temp 'source'
    $review = Join-Path $temp 'review'
    [IO.Directory]::CreateDirectory($source) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $review 'config')) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $review 'scan/logs')) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $review 'logs')) | Out-Null
    git -C $source init -q
    git -C $source config user.email 'fixture@example.invalid'
    git -C $source config user.name 'Fixture'
    [IO.File]::WriteAllText((Join-Path $source 'README.md'), 'fixture')
    git -C $source add README.md
    git -C $source commit -qm 'fixture'
    $sourceCommit = ([string](git -C $source rev-parse HEAD)).Trim().ToLowerInvariant()

    $configPath = Join-Path $review 'config/webforms-review.json'
    $config = [ordered]@{
        schemaVersion = 'focused-webforms-review-config.v1'
        sourceRoot = $source.Replace('\', '/')
        webFormsFolder = '.'
        backendFolder = '.'
        controlsFolder = '.'
        projectSelection = [ordered]@{ mode = 'projectless'; solutionRelativePath = ''; projectRelativePaths = @() }
        outputRoot = $review.Replace('\', '/')
        pageSelection = [ordered]@{ mode = 'all'; forms = @() }
    }
    [IO.File]::WriteAllText($configPath, (($config | ConvertTo-Json -Depth 12) + "`n"), [Text.UTF8Encoding]::new($false))
    $configSha = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash.ToLowerInvariant()

    $manifest = [ordered]@{ schemaVersion = 'scan-manifest.v1'; commitSha = $sourceCommit }
    [IO.File]::WriteAllText((Join-Path $review 'scan/scan-manifest.json'), (($manifest | ConvertTo-Json) + "`n"))
    foreach ($relative in @('scan/facts.ndjson','scan/index.sqlite','scan/report.md','scan/logs/analyzer.log','logs/local-review-result.json')) {
        [IO.File]::WriteAllText((Join-Path $review $relative), 'retained')
    }

    $traceRoot = Split-Path -Parent $scripts
    $currentCommit = ([string](git -C $traceRoot rev-parse HEAD)).Trim().ToLowerInvariant()
    $priorCommit = ([string](git -C $traceRoot rev-parse HEAD^)).Trim().ToLowerInvariant()
    $receipt = [ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        provenance = [ordered]@{ configSha256 = $configSha; generatorSha256 = ('c' * 64) }
        run = [ordered]@{ runId = 'fixture-recovery'; state = 'failed'; failure = 'WEBFORMS_PIPELINE_ARTIFACT_LIMIT;path=scan/facts.ndjson'; updatedUtc = [DateTime]::UtcNow.ToString('O') }
        source = [ordered]@{ root = $source; commitSha = $sourceCommit }
        traceMap = [ordered]@{ root = $traceRoot; commitSha = $priorCommit }
        stages = [ordered]@{
            build = [ordered]@{ state = 'completed'; completedUtc = [DateTime]::UtcNow.ToString('O'); artifacts = @(); failure = '' }
            scan = [ordered]@{ state = 'failed'; completedUtc = $null; artifacts = @(); failure = 'WEBFORMS_PIPELINE_ARTIFACT_LIMIT;path=scan/facts.ndjson' }
            packet = [ordered]@{ state = 'pending'; completedUtc = $null; artifacts = @(); failure = '' }
            evidenceDocs = [ordered]@{ state = 'pending'; completedUtc = $null; artifacts = @(); failure = '' }
            workbench = [ordered]@{ state = 'pending'; completedUtc = $null; artifacts = @(); failure = '' }
        }
    }
    $receiptPath = Join-Path $review 'run-receipt.json'
    [IO.File]::WriteAllText($receiptPath, (($receipt | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))

    function global:dotnet { $global:LASTEXITCODE = 1 }
    $failure = $null
    $output = @()
    try { $output = @(& $pipelinePath -ReviewRoot $review -TraceMapRoot $traceRoot 6>&1 | ForEach-Object { [string]$_ }) } catch { $failure = $_.Exception.Message }
    if ($failure -ne 'WEBFORMS_PIPELINE_PACKET_FAILED') { throw "Recovery did not continue through packet stage: $failure" }
    $recovered = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 20
    if ($recovered.stages.scan.state -ne 'completed' -or @($recovered.stages.scan.artifacts).Count -ne 6) { throw 'Recovery did not receipt all retained scan artifacts.' }
    if ($recovered.traceMap.commitSha -ne $currentCommit -or @($recovered.provenance.migrations).Count -ne 1) { throw 'Recovery did not preserve the tool provenance migration.' }
    if ($recovered.provenance.migrations[0].priorTraceMapCommitSha -ne $priorCommit -or
        $recovered.provenance.migrations[0].reason -ne 'oversize-scan-artifact-limit-recovery') {
        throw 'Recovery migration omitted the prior tool identity or reason.'
    }
}
finally {
    Remove-Item Function:\dotnet -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

Write-Host 'PASS focused Web Forms oversize scan recovery'
