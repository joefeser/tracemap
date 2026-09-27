[CmdletBinding()]
param([string]$ProofRoot)

# Replays only bounded graph reporting and local HTML/JSON projection from a
# saved existing-publish proof. It does not rebuild, publish, scan, or combine.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_COMPILED_REPLAY_POWERSHELL_7_REQUIRED' }

if (!$ProofRoot) {
    $candidate = Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory |
        Where-Object { $_.Name -cmatch '^tracemap-existing-publish-[0-9a-f]{32}$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'handler-paths.json') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'combined.sqlite') -PathType Leaf) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (!$candidate) { throw 'WEBFORMS_COMPILED_REPLAY_PROOF_UNAVAILABLE' }
    $ProofRoot = $candidate.FullName
}
$ProofRoot = [IO.Path]::GetFullPath($ProofRoot).TrimEnd('\', '/')
$sqlPath = Join-Path $ProofRoot 'handler-paths.json'
$combinedPath = Join-Path $ProofRoot 'combined.sqlite'
if (!(Test-Path -LiteralPath $sqlPath -PathType Leaf) -or
    !(Test-Path -LiteralPath $combinedPath -PathType Leaf)) {
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
    $ilMaxWork = 30000000
    $highWorkIndex = Join-Path $ProofRoot "combined-ilwork-$ilMaxWork.sqlite"
    $highWorkFacts = Join-Path $ProofRoot "scan-ilwork-$ilMaxWork/facts.ndjson"
    $highWorkManifest = Join-Path $ProofRoot "scan-ilwork-$ilMaxWork/scan-manifest.json"
    $useHighWork = (Test-Path -LiteralPath $highWorkIndex -PathType Leaf) -and
        (Test-Path -LiteralPath $highWorkFacts -PathType Leaf) -and
        (Test-Path -LiteralPath $highWorkManifest -PathType Leaf)
    Write-Output "compiledReplayHighWorkAvailable=$useHighWork"
    $apiArgs = @{ OutputRoot = $ProofRoot; RecheckCompiledApi = $true }
    if ($useHighWork) { $apiArgs.IlMaxWork = $ilMaxWork; $apiArgs.FillOnly = $true }
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
        & (Join-Path $PSScriptRoot 'New-ExistingWebFormsCompiledPathHandoff.ps1') `
            -ProofRoot $ProofRoot -PathReportPath $apiPath -PathReportReceiptPath $apiReceipt `
            -ToSurface database-api -IlMaxWork $(if ($useHighWork) { $ilMaxWork } else { 0 })
    }
}
$sqlProjection = Join-Path $ProofRoot 'compiled-path-review'
if (Test-Path -LiteralPath $sqlProjection) {
    Write-Output 'compiledReplaySqlProjection=existing'
} else {
    & (Join-Path $PSScriptRoot 'New-ExistingWebFormsCompiledPathHandoff.ps1') -ProofRoot $ProofRoot
}
