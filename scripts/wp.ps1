[CmdletBinding()]
param([string]$OutputRoot, [switch]$RecheckPathReasons, [switch]$RecheckCompiledApi)

# Summarize an existing local Web Forms compiled probe. Never starts a scan.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_PROBE_POWERSHELL_7_REQUIRED' }

if (!$OutputRoot) {
    $latest = Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory |
        Where-Object { $_.Name -cmatch '^tracemap-existing-publish-[0-9a-f]{32}$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'probe/scan-manifest.json') -PathType Leaf) } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if (!$latest) { throw 'WEBFORMS_PROBE_MANIFEST_UNAVAILABLE' }
    $OutputRoot = $latest.FullName
}
$manifestPath = Join-Path $OutputRoot 'probe/scan-manifest.json'
$receiptPath = Join-Path $OutputRoot 'publish-receipt.local.json'
if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
    !(Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
    throw 'WEBFORMS_PROBE_INPUT_UNAVAILABLE'
}
$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 30
$receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 20
$outcomes = @($manifest.compiledInputProvenance.outcomes)
$inventory = @($receipt.assemblyInventory | Where-Object { $_.disposition -ne 'operator-declared-out-of-scope' })
$omitted = [int]$manifest.compiledInputProvenance.omittedInputCount
$admitted = @($outcomes | Where-Object { $_.outcome -ceq 'admitted' }).Count
$nonadmitted = $outcomes.Count - $admitted
$missingLocator = @($outcomes | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.safeLocator) }).Count
$missingHash = @($outcomes | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.rawFileSha256) }).Count
$missingIdentity = @($outcomes | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.assemblyIdentity) }).Count
$ready = $outcomes.Count -eq $inventory.Count -and $omitted -eq 0 -and
    $nonadmitted -eq 0 -and $missingLocator -eq 0 -and $missingHash -eq 0 -and $missingIdentity -eq 0
$failedSelected = 0
$failedContext = 0
$failedUnknown = 0
foreach ($item in @($outcomes | Where-Object { $_.outcome -cne 'admitted' })) {
    $matches = @($inventory | Where-Object {
        ![string]::IsNullOrWhiteSpace([string]$item.rawFileSha256) -and
        [string]$_.sha256 -ceq [string]$item.rawFileSha256
    })
    if ($matches.Count -ne 1) { $failedUnknown++; continue }
    if ($matches[0].disposition -ceq 'selected') { $failedSelected++ }
    elseif ($matches[0].disposition -ceq 'artifact-context-no-source-commit') { $failedContext++ }
    else { $failedUnknown++ }
}

Write-Output "probeSelected=$($inventory.Count)"
Write-Output "probeOutcomes=$($outcomes.Count)"
Write-Output "probeOmitted=$omitted"
Write-Output "probeAdmitted=$admitted"
Write-Output "probeNonadmitted=$nonadmitted"
Write-Output "probeNonadmittedSelected=$failedSelected"
Write-Output "probeNonadmittedContext=$failedContext"
Write-Output "probeNonadmittedUnknown=$failedUnknown"
Write-Output "probeTextLimit=$($manifest.compiledInputProvenance.effectiveLimits.maxTextLength)"
Write-Output "probeMissingLocator=$missingLocator"
Write-Output "probeMissingHash=$missingHash"
Write-Output "probeMissingIdentity=$missingIdentity"
Write-Output "probeReady=$ready"
foreach ($group in @($outcomes | Group-Object -Property outcome | Sort-Object Name)) {
    $label = if ([string]$group.Name -cmatch '^[a-z][a-z0-9-]{0,63}$') { $group.Name } else { 'other' }
    Write-Output "probeOutcome.$label=$($group.Count)"
}
$kinds = @($outcomes | ForEach-Object { @($_.gapKinds) } |
    Where-Object { $_ -is [string] -and $_ -cmatch '^[A-Za-z][A-Za-z0-9]{0,79}$' })
foreach ($group in @($kinds | Group-Object | Sort-Object Name)) {
    Write-Output "probeGap.$($group.Name)=$($group.Count)"
}

$scanManifestPath = Join-Path $OutputRoot 'scan/scan-manifest.json'
if (Test-Path -LiteralPath $scanManifestPath -PathType Leaf) {
    $scan = [IO.File]::ReadAllText($scanManifestPath) | ConvertFrom-Json -Depth 30
    $boundIl = @($scan.ilBodyProvenance.outcomes | Where-Object { $_.provenanceState -ceq 'bound' })
    $boundIlWithGaps = @($boundIl | Where-Object { @($_.gapKinds).Count -gt 0 })
    Write-Output "scanIlCoverage=$($scan.ilBodyProvenance.coverageState)"
    Write-Output "scanBoundIlInputs=$($boundIl.Count)"
    Write-Output "scanBoundIlInputsWithGaps=$($boundIlWithGaps.Count)"
    Write-Output "scanIlTextLimit=$($scan.ilBodyProvenance.effectiveLimits.maxTextLength)"
    foreach ($group in @($boundIl | Group-Object -Property outcome | Sort-Object Name)) {
        $label = if ([string]$group.Name -cmatch '^[a-z][a-z0-9-]{0,63}$') { $group.Name } else { 'other' }
        Write-Output "scanBoundIlOutcome.$label=$($group.Count)"
    }
    $boundGapKinds = @($boundIlWithGaps | ForEach-Object { @($_.gapKinds) } |
        Where-Object { $_ -is [string] -and $_ -cmatch '^[A-Za-z][A-Za-z0-9]{0,79}$' })
    foreach ($group in @($boundGapKinds | Group-Object | Sort-Object Name)) {
        Write-Output "scanBoundIlGap.$($group.Name)=$($group.Count)"
    }
}

$pathReportPath = Join-Path $OutputRoot 'handler-paths.json'
if (Test-Path -LiteralPath $pathReportPath -PathType Leaf) {
    $report = [IO.File]::ReadAllText($pathReportPath) | ConvertFrom-Json -Depth 50
    $pathGaps = @($report.gaps)
    if ($pathGaps.Count -ne [int]$report.summary.gapCount) {
        throw 'WEBFORMS_PROBE_PATH_SUMMARY_MISMATCH'
    }
    Write-Output "pathSelectorCandidates=$($report.summary.selectorCandidateCount)"
    Write-Output "pathGraphNodes=$($report.summary.graphNodeCount)"
    Write-Output "pathGraphEdges=$($report.summary.graphEdgeCount)"
    Write-Output "pathCount=$($report.summary.pathCount)"
    Write-Output "pathGapCount=$($pathGaps.Count)"
    Write-Output "pathTruncated=$($report.summary.truncated)"
    foreach ($group in @($pathGaps | Group-Object -Property gapKind | Sort-Object Name)) {
        $label = if ([string]$group.Name -cmatch '^[A-Za-z][A-Za-z0-9]{0,79}$') {
            $group.Name
        } else { 'other' }
        Write-Output "pathGap.$label=$($group.Count)"
    }
    $truncations = @($pathGaps | Where-Object { $_.gapKind -ceq 'TruncatedByLimit' })
    foreach ($reason in @('selector-candidates', 'depth', 'frontier', 'work', 'path', 'cycle')) {
        Write-Output "pathTruncation.$reason=$(@($truncations | Where-Object { $_.reason -ceq $reason }).Count)"
    }
    $memberGaps = @($pathGaps | Where-Object { $_.gapKind -ceq 'ProjectlessPublishMemberAmbiguous' })
    $missingMembers = @($memberGaps | Where-Object { [int]$_.candidateCount -eq 0 }).Count
    $multipleMembers = @($memberGaps | Where-Object { [int]$_.candidateCount -gt 1 }).Count
    if ($missingMembers + $multipleMembers -ne $memberGaps.Count) {
        throw 'WEBFORMS_PROBE_MEMBER_CANDIDATE_SUMMARY_MISMATCH'
    }
    Write-Output "pathPublishMemberMissing=$missingMembers"
    Write-Output "pathPublishMemberMultiple=$multipleMembers"
}

if ($RecheckPathReasons -or $RecheckCompiledApi) {
    $combinedPath = Join-Path $OutputRoot 'combined.sqlite'
    if (!(Test-Path -LiteralPath $pathReportPath -PathType Leaf) -or
        !(Test-Path -LiteralPath $combinedPath -PathType Leaf)) {
        throw 'WEBFORMS_PROBE_PATH_RECHECK_INPUT_UNAVAILABLE'
    }
    $previous = [IO.File]::ReadAllText($pathReportPath) | ConvertFrom-Json -Depth 50
    if ([string]::IsNullOrWhiteSpace([string]$previous.query.fromSymbol) -or
        $previous.query.toSurface -cne 'sql-query' -or
        [int]$previous.query.maxDepth -ne 20 -or [int]$previous.query.maxPaths -ne 256) {
        throw 'WEBFORMS_PROBE_PATH_RECHECK_QUERY_UNEXPECTED'
    }
    $scratch = Join-Path $OutputRoot ('path-recheck-' + [guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($scratch)
    $recheckPath = Join-Path $scratch 'handler-paths.local.json'
    $project = Join-Path $PSScriptRoot '../src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
    $terminalSurface = if ($RecheckCompiledApi) { 'database-api' } else { 'sql-query' }
    $fromSymbol = [string]$previous.query.fromSymbol
    if ($RecheckCompiledApi) {
        $factsPath = Join-Path $OutputRoot 'scan/facts.ndjson'
        if (!(Test-Path -LiteralPath $factsPath -PathType Leaf) -or @($receipt.pages).Count -ne 1) {
            throw 'WEBFORMS_PROBE_HANDLER_FACTS_UNAVAILABLE'
        }
        $pageSource = [string]$receipt.pages[0].sourcePath
        $handlerSymbols = [Collections.Generic.List[string]]::new()
        foreach ($line in [IO.File]::ReadLines($factsPath)) {
            if (!$line.Contains('"factType":"WebFormsHandlerResolved"', [StringComparison]::Ordinal)) { continue }
            $fact = $line | ConvertFrom-Json -Depth 30
            if ([string]$fact.properties.handlerName -ieq $fromSymbol -and
                [string]$fact.properties.markupFile -ieq $pageSource -and
                ![string]::IsNullOrWhiteSpace([string]$fact.properties.handlerSymbol)) {
                $handlerSymbols.Add([string]$fact.properties.handlerSymbol)
                if ($handlerSymbols.Count -gt 16) { break }
            }
        }
        Write-Output "compiledApiHandlerMatches=$($handlerSymbols.Count)"
        if ($handlerSymbols.Count -ne 1) {
            Write-Output 'compiledApiStatus=handler-not-unique'
            return
        }
        $fromSymbol = $handlerSymbols[0]
    }
    $pathArgs = @('run', '--project', $project, '--', 'paths', '--index', $combinedPath,
        '--out', $recheckPath, '--format', 'json', '--from-symbol', $fromSymbol,
        '--to-surface', $terminalSurface, '--max-depth', '20', '--max-paths', '256')
    if ($RecheckCompiledApi) { $pathArgs += '--exact-from-symbol' }
    & dotnet @pathArgs *> (Join-Path $scratch 'paths.local.log')
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $recheckPath -PathType Leaf)) {
        throw 'WEBFORMS_PROBE_PATH_RECHECK_FAILED'
    }
    $generatorDirectory = Join-Path $PSScriptRoot '../src/dotnet/TraceMap.Cli/bin/Debug/net10.0'
    $assemblies = @(Get-ChildItem -LiteralPath $generatorDirectory -File -Filter 'TraceMap.*.dll' |
        Sort-Object Name)
    if ($assemblies.Count -lt 3) {
        throw 'WEBFORMS_PROBE_PATH_RECHECK_GENERATOR_UNAVAILABLE'
    }
    $generatorFiles = @($PSCommandPath) + @($assemblies.FullName)
    $generatorInventory = @($generatorFiles | ForEach-Object {
        (Split-Path $_ -Leaf) + ':' + (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    }) -join "`n"
    $generatorSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($generatorInventory))).ToLowerInvariant()
    $inputInventory = (Get-FileHash -LiteralPath $combinedPath -Algorithm SHA256).Hash.ToLowerInvariant() + "`n" +
        (Get-FileHash -LiteralPath $pathReportPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $inputSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($inputInventory))).ToLowerInvariant()
    $receipt = [ordered]@{ schemaVersion = 'webforms-path-recheck.v1'; visibility = 'local-only'
        generatorSha256 = $generatorSha; boundedInputSha256 = $inputSha }
    [IO.File]::WriteAllText((Join-Path $scratch 'path-recheck.receipt.local.json'),
        (($receipt | ConvertTo-Json -Depth 5) + "`n"), [Text.UTF8Encoding]::new($false))
    $recheck = [IO.File]::ReadAllText($recheckPath) | ConvertFrom-Json -Depth 50
    if ($RecheckCompiledApi) {
        $selectorCount = [int]$recheck.summary.selectorCandidateCount
        $apiPaths = @($recheck.paths | Where-Object {
            @($_.nodes).Count -gt 0 -and $_.nodes[-1].surfaceKind -ceq 'database-api' -and
            @($_.edges | Where-Object { $_.edgeKind -ceq 'compiled-database-api-candidate' }).Count -gt 0
        })
        Write-Output "compiledApiPaths=$($(if ($selectorCount -eq 1) { $apiPaths.Count } else { 'withheld' }))"
        Write-Output "compiledApiSelectorCandidates=$selectorCount"
        Write-Output "compiledApiStatus=$($(if ($selectorCount -eq 1) { 'unique-handler' } else { 'selector-not-unique' }))"
        Write-Output "compiledApiTruncated=$($recheck.summary.truncated)"
        foreach ($reason in @('selector-candidates', 'depth', 'frontier', 'work', 'path', 'cycle')) {
            Write-Output "compiledApiTruncation.$reason=$(@($recheck.gaps | Where-Object {
                $_.gapKind -ceq 'TruncatedByLimit' -and $_.reason -ceq $reason
            }).Count)"
        }
        Write-Output "compiledApiNoTerminal=$(@($recheck.gaps | Where-Object {
            $_.gapKind -ceq 'SelectorNoMatch' -and $_.reason -ceq 'selector'
        }).Count)"
        return
    }
    $memberGaps = @($recheck.gaps | Where-Object { $_.gapKind -ceq 'ProjectlessPublishMemberAmbiguous' })
    $artifactIlGaps = @($recheck.gaps | Where-Object { $_.gapKind -ceq 'CompiledIlArtifactContext' })
    Write-Output "pathRecheckPaths=$($recheck.summary.pathCount)"
    Write-Output "pathRecheckPublishMemberGaps=$($memberGaps.Count)"
    Write-Output "pathRecheckArtifactIlCalls=$($artifactIlGaps.Count)"
    foreach ($reason in @('bound-method-name-unavailable',
            'method-absent-from-receipt-bound-assemblies', 'qualified-containing-type-unmatched',
            'parameter-shape-unmatched', 'multiple-qualified-compatible-members')) {
        Write-Output "pathRecheckReason.$reason=$(@($memberGaps | Where-Object { $_.reason -ceq $reason }).Count)"
    }
    Write-Output "pathRecheckReason.other=$(@($memberGaps | Where-Object {
        $_.reason -cnotin @('bound-method-name-unavailable',
            'method-absent-from-receipt-bound-assemblies', 'qualified-containing-type-unmatched',
            'parameter-shape-unmatched', 'multiple-qualified-compatible-members') }).Count)"
}
