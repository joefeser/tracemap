[CmdletBinding()]
param([string]$ProofRoot, [switch]$AllowBaseIndex)

# Replays only bounded graph reporting and local HTML/JSON projection from a
# saved existing-publish proof. It does not rebuild, publish, scan, or combine.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_COMPILED_REPLAY_POWERSHELL_7_REQUIRED' }
$ilMaxWork = 30000000

function Test-HighWorkProof([string]$Root) {
    foreach ($relative in @('handler-paths.json', 'publish-receipt.local.json',
            "scan-ilwork-$ilMaxWork/facts.ndjson", "scan-ilwork-$ilMaxWork/scan-manifest.json",
            "combined-ilwork-$ilMaxWork.sqlite")) {
        if (!(Test-Path -LiteralPath (Join-Path $Root $relative) -PathType Leaf)) { return $false }
    }
    return $true
}

function Find-SavedApiReport([string]$Root) {
    $scanFolder = "scan-ilwork-$ilMaxWork"
    $combinedName = "combined-ilwork-$ilMaxWork.sqlite"
    $folders = @(Get-ChildItem -LiteralPath $Root -Directory -Filter 'path-recheck-*' |
        Sort-Object LastWriteTimeUtc -Descending)
    foreach ($folder in $folders) {
        $reportPath = Join-Path $folder.FullName 'handler-paths.local.json'
        $receiptPath = Join-Path $folder.FullName 'path-recheck.receipt.local.json'
        if (!(Test-Path -LiteralPath $reportPath -PathType Leaf) -or
            !(Test-Path -LiteralPath $receiptPath -PathType Leaf)) { continue }
        $reportFile = Get-Item -LiteralPath $reportPath
        if ($reportFile.Length -le 0 -or $reportFile.Length -gt 268435456) { continue }
        $receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json -Depth 10
        if ($receipt.schemaVersion -cne 'webforms-path-recheck.v1' -or
            $receipt.scanFolder -cne $scanFolder -or
            $receipt.combinedIndex -cne $combinedName -or
            [string]$receipt.boundedInputSha256 -cnotmatch '^[0-9a-f]{64}$') { continue }
        $report = [IO.File]::ReadAllText($reportPath) | ConvertFrom-Json -Depth 50
        if ($report.query.toSurface -cne 'database-api' -or
            $report.query.surfaceName -cne 'DbDataAdapter.Fill' -or
            [int]$report.summary.selectorCandidateCount -ne 1 -or
            [int]$report.summary.pathCount -ne @($report.paths).Count) { continue }
        $inputInventory = (Get-FileHash -LiteralPath (Join-Path $Root $combinedName) -Algorithm SHA256).Hash.ToLowerInvariant() + "`n" +
            (Get-FileHash -LiteralPath (Join-Path $Root 'handler-paths.json') -Algorithm SHA256).Hash.ToLowerInvariant() + "`n" +
            (Get-FileHash -LiteralPath (Join-Path $Root (Join-Path $scanFolder 'facts.ndjson')) -Algorithm SHA256).Hash.ToLowerInvariant()
        $inputSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
            [Text.Encoding]::UTF8.GetBytes($inputInventory))).ToLowerInvariant()
        if ($receipt.boundedInputSha256 -cne $inputSha) { continue }
        return [pscustomobject]@{ Path = $reportPath; Receipt = $receiptPath; Report = $report }
    }
    return $null
}

if (!$ProofRoot) {
    $candidate = Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory |
        Where-Object { $_.Name -cmatch '^tracemap-existing-publish-[0-9a-f]{32}$' -and
            (Test-HighWorkProof $_.FullName) } |
        Sort-Object @{ Expression = { (Get-Item -LiteralPath (Join-Path $_.FullName "combined-ilwork-$ilMaxWork.sqlite")).LastWriteTimeUtc }; Descending = $true } |
        Select-Object -First 1
    if (!$candidate) { throw 'WEBFORMS_COMPILED_REPLAY_HIGH_WORK_PROOF_UNAVAILABLE' }
    $ProofRoot = $candidate.FullName
}
$ProofRoot = [IO.Path]::GetFullPath($ProofRoot).TrimEnd('\', '/')
$useHighWork = Test-HighWorkProof $ProofRoot
if (!$useHighWork -and !$AllowBaseIndex) { throw 'WEBFORMS_COMPILED_REPLAY_HIGH_WORK_PROOF_UNAVAILABLE' }
$sqlPath = Join-Path $ProofRoot 'handler-paths.json'
if (!(Test-Path -LiteralPath $sqlPath -PathType Leaf) -or
    (!$useHighWork -and !(Test-Path -LiteralPath (Join-Path $ProofRoot 'combined.sqlite') -PathType Leaf))) {
    throw 'WEBFORMS_COMPILED_REPLAY_INPUT_UNAVAILABLE'
}
Write-Output "compiledReplayProofRoot=$ProofRoot"
$sql = [IO.File]::ReadAllText($sqlPath) | ConvertFrom-Json -Depth 50
if ($sql.query.toSurface -cne 'sql-query' -or
    [int]$sql.summary.pathCount -ne @($sql.paths).Count) {
    throw 'WEBFORMS_COMPILED_REPLAY_SQL_REPORT_INVALID'
}
Write-Output "compiledReplaySqlPaths=$(@($sql.paths).Count)"
if (@($sql.paths).Count -eq 0) {
    Write-Output "compiledReplayHighWorkAvailable=$useHighWork"
    $saved = if ($useHighWork) { Find-SavedApiReport $ProofRoot } else { $null }
    Write-Output "compiledReplaySavedApi=$($null -ne $saved)"
    if ($saved) {
        $apiPath = $saved.Path
        $apiReceipt = $saved.Receipt
        Write-Output "compiledApiPaths=$($saved.Report.summary.pathCount)"
        Write-Output "compiledApiTruncated=$($saved.Report.summary.truncated)"
    } else {
        $apiArgs = @{ OutputRoot = $ProofRoot; RecheckCompiledApi = $true }
        if ($useHighWork) { $apiArgs.IlMaxWork = $ilMaxWork; $apiArgs.FillOnly = $true }
        $apiLines = @(& (Join-Path $PSScriptRoot 'wp.ps1') @apiArgs)
        $apiStatus = @($apiLines | Where-Object { $_ -cmatch '^compiledApiStatus=' })
        $apiReportLine = @($apiLines | Where-Object { $_ -cmatch '^compiledApiPathReport=' })
        $apiReceiptLine = @($apiLines | Where-Object { $_ -cmatch '^compiledApiPathReceipt=' })
        foreach ($line in @($apiLines | Where-Object {
            $_ -cmatch '^compiledApi(?:Paths|Status|SelectorCandidates|Truncated|ReachedNodes|TraversedEdges|TerminalCallers|ReachableTerminalCallers|ReachableFillMemberRefs|ReachableUnresolvedIlCalls)=' -or
            $_ -cmatch '^compiledApi(?:Truncation|Traversed|ReachableIlGap)\.[A-Za-z0-9-]+='
        })) { Write-Output $line }
        if ($apiStatus.Count -ne 1) { throw 'WEBFORMS_COMPILED_REPLAY_API_STATUS_UNAVAILABLE' }
        if ($apiStatus[0] -cne 'compiledApiStatus=unique-handler') {
            throw 'WEBFORMS_COMPILED_REPLAY_HANDLER_NOT_UNIQUE'
        }
        if ($apiReportLine.Count -ne 1 -or $apiReceiptLine.Count -ne 1) {
            throw 'WEBFORMS_COMPILED_REPLAY_API_REPORT_UNAVAILABLE'
        }
        $apiPath = [string]$apiReportLine[0].Substring('compiledApiPathReport='.Length)
        $apiReceipt = [string]$apiReceiptLine[0].Substring('compiledApiPathReceipt='.Length)
    }
    $reviewDirectory = Join-Path (Split-Path -Parent $apiPath) 'compiled-api-review'
    if (Test-Path -LiteralPath $reviewDirectory) {
        $reviewDirectory = Join-Path (Split-Path -Parent $apiPath) ('compiled-api-review-readable-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    }
    & (Join-Path $PSScriptRoot 'New-ExistingWebFormsCompiledPathHandoff.ps1') `
        -ProofRoot $ProofRoot -PathReportPath $apiPath -PathReportReceiptPath $apiReceipt `
        -ToSurface database-api -IlMaxWork $(if ($useHighWork) { $ilMaxWork } else { 0 }) `
        -OutputDirectory $reviewDirectory
}
$sqlProjection = Join-Path $ProofRoot 'compiled-path-review'
if (Test-Path -LiteralPath $sqlProjection) {
    Write-Output 'compiledReplaySqlProjection=existing'
} else {
    & (Join-Path $PSScriptRoot 'New-ExistingWebFormsCompiledPathHandoff.ps1') -ProofRoot $ProofRoot
}
