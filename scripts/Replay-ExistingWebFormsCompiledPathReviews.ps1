[CmdletBinding()]
param([string]$ProofRoot)

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
if (!(Test-HighWorkProof $ProofRoot)) { throw 'WEBFORMS_COMPILED_REPLAY_HIGH_WORK_PROOF_UNAVAILABLE' }
$sqlPath = Join-Path $ProofRoot 'handler-paths.json'
Write-Output "compiledReplayProofRoot=$ProofRoot"
$sql = [IO.File]::ReadAllText($sqlPath) | ConvertFrom-Json -Depth 50
if ($sql.query.toSurface -cne 'sql-query' -or
    [int]$sql.summary.pathCount -ne @($sql.paths).Count) {
    throw 'WEBFORMS_COMPILED_REPLAY_SQL_REPORT_INVALID'
}
Write-Output "compiledReplaySqlPaths=$(@($sql.paths).Count)"
if (@($sql.paths).Count -eq 0) {
    Write-Output 'compiledReplayHighWorkAvailable=True'
    $apiArgs = @{ OutputRoot = $ProofRoot; RecheckCompiledApi = $true
        IlMaxWork = $ilMaxWork; FillOnly = $true }
    $apiLines = @(& (Join-Path $PSScriptRoot 'wp.ps1') @apiArgs)
    $apiStatus = @($apiLines | Where-Object { $_ -cmatch '^compiledApiStatus=' })
    $apiReportLine = @($apiLines | Where-Object { $_ -cmatch '^compiledApiPathReport=' })
    $apiReceiptLine = @($apiLines | Where-Object { $_ -cmatch '^compiledApiPathReceipt=' })
    foreach ($line in @($apiLines | Where-Object {
        $_ -cmatch '^compiledApi(?:Paths|Status|SelectorCandidates|Truncated|ReachedNodes|TraversedEdges|TerminalCallers|ReachableTerminalCallers|ReachableFillMemberRefs|ReachableUnresolvedIlCalls)=' -or
        $_ -cmatch '^compiledApi(?:Truncation|Traversed|ReachableIlGap)\.[A-Za-z0-9-]+='
    })) {
        Write-Output $line
    }
    if ($apiStatus.Count -ne 1) { throw 'WEBFORMS_COMPILED_REPLAY_API_STATUS_UNAVAILABLE' }
    if ($apiStatus[0] -ceq 'compiledApiStatus=unique-handler') {
        if ($apiReportLine.Count -ne 1 -or $apiReceiptLine.Count -ne 1) {
            throw 'WEBFORMS_COMPILED_REPLAY_API_REPORT_UNAVAILABLE'
        }
        $apiPath = [string]$apiReportLine[0].Substring('compiledApiPathReport='.Length)
        $apiReceipt = [string]$apiReceiptLine[0].Substring('compiledApiPathReceipt='.Length)
        $reviewDirectory = Join-Path (Split-Path -Parent $apiPath) 'compiled-api-review'
        & (Join-Path $PSScriptRoot 'New-ExistingWebFormsCompiledPathHandoff.ps1') `
            -ProofRoot $ProofRoot -PathReportPath $apiPath -PathReportReceiptPath $apiReceipt `
            -ToSurface database-api -IlMaxWork $ilMaxWork -OutputDirectory $reviewDirectory
    }
}
$sqlProjection = Join-Path $ProofRoot 'compiled-path-review'
if (Test-Path -LiteralPath $sqlProjection) {
    Write-Output 'compiledReplaySqlProjection=existing'
} else {
    & (Join-Path $PSScriptRoot 'New-ExistingWebFormsCompiledPathHandoff.ps1') -ProofRoot $ProofRoot
}
