$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$subject = Join-Path $scripts 'Show-FocusedWebFormsTerminalInventorySummary.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-terminal-summary-' + [Guid]::NewGuid().ToString('N'))
$workbench = Join-Path $root 'workbench'

function Write-Page([string]$PageId, [object[]]$Chains) {
    $path = Join-Path $workbench "$PageId.handoff.json"
    $page = [ordered]@{
        schemaVersion = 'webforms-application-page-handoff.v1'
        claimLevel = 'local-only'
        pageId = $PageId
        analysis = @{ status = 'partial'; packetTruncated = $true }
        eventChains = $Chains
    }
    [IO.File]::WriteAllText($path, (($page | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
    return [ordered]@{
        path = "workbench/$PageId.handoff.json"
        bytes = (Get-Item -LiteralPath $path).Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Assert-Output([string[]]$Output, [string]$Expected) {
    if ($Expected -notin $Output) { throw "Terminal summary omitted: $Expected" }
}

try {
    [IO.Directory]::CreateDirectory($workbench) | Out-Null
    $artifacts = @(
        (Write-Page 'page-002' @(
            @{ terminalReachabilityAvailable = $true; terminalReachabilityComplete = $true; distinctReachableTerminalCount = 1; reachableTerminalIds = @('terminal-one'); pathEnumerationTruncated = $true },
            @{ terminalReachabilityAvailable = $true; terminalReachabilityComplete = $true; distinctReachableTerminalCount = 1; reachableTerminalIds = @('terminal-one'); pathEnumerationTruncated = $false }
        )),
        (Write-Page 'page-003' @(
            @{ terminalReachabilityAvailable = $true; terminalReachabilityComplete = $true; distinctReachableTerminalCount = 0; reachableTerminalIds = @(); pathEnumerationTruncated = $true }
        )),
        (Write-Page 'page-011' @(
            @{ terminalReachabilityAvailable = $true; terminalReachabilityComplete = $false; distinctReachableTerminalCount = 1; reachableTerminalIds = @('terminal-two'); terminalReachabilityLimitReasons = @('work'); pathEnumerationTruncated = $false },
            @{ terminalReachabilityAvailable = $false; terminalReachabilityComplete = $null; distinctReachableTerminalCount = $null; reachableTerminalIds = @(); pathEnumerationTruncated = $false }
        ))
    )
    $receipt = [ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        run = @{ state = 'completed' }
        stages = @{ workbench = @{ state = 'completed'; artifacts = $artifacts } }
    }
    $receiptPath = Join-Path $root 'run-receipt.json'
    [IO.File]::WriteAllText($receiptPath, (($receipt | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))

    $output = @(& $subject -ReviewRoot $root)
    Assert-Output $output 'webFormsTerminalInventorySummary=valid'
    Assert-Output $output 'scope=receipted-retained-graph-only;runtime-absence-not-proven'
    Assert-Output $output 'pageId=page-002;analysisStatus=partial;packetTruncated=true;chains=2;available=2;complete=2;incomplete=0;unavailable=0;distinctTerminalIdsObserved=1;reportedTerminalCountSum=2;noSupportedTerminalComplete=0;pathDetailTruncated=1;limits=none'
    Assert-Output $output 'pageId=page-003;analysisStatus=partial;packetTruncated=true;chains=1;available=1;complete=1;incomplete=0;unavailable=0;distinctTerminalIdsObserved=0;reportedTerminalCountSum=0;noSupportedTerminalComplete=1;pathDetailTruncated=1;limits=none'
    Assert-Output $output 'pageId=page-011;analysisStatus=partial;packetTruncated=true;chains=2;available=1;complete=0;incomplete=1;unavailable=1;distinctTerminalIdsObserved=1;reportedTerminalCountSum=1;noSupportedTerminalComplete=0;pathDetailTruncated=0;limits=work'
    if (@($output | Where-Object { $_ -match '^traceMapCommitSha=[0-9a-f]{40}$' }).Count -ne 1) {
        throw 'Terminal summary omitted the TraceMap commit.'
    }

    $selected = @(& $subject -ReviewRoot $root -PageIds page-011,page-002)
    if (@($selected | Where-Object { $_ -match '^pageId=' }).Count -ne 2 -or
        $selected[-2] -notmatch '^pageId=page-011;' -or $selected[-1] -notmatch '^pageId=page-002;') {
        throw 'Selected page order or count changed.'
    }

    $caught = $false
    try { & $subject -ReviewRoot $root -PageIds page-002,page-002 | Out-Null }
    catch { $caught = $_.Exception.Message -match 'WEBFORMS_TERMINAL_SUMMARY_DUPLICATE_PAGE' }
    if (!$caught) { throw 'Duplicate page selection was accepted.' }

    [IO.File]::AppendAllText((Join-Path $workbench 'page-003.handoff.json'), ' ')
    $caught = $false
    try { & $subject -ReviewRoot $root | Out-Null }
    catch { $caught = $_.Exception.Message -match 'WEBFORMS_TERMINAL_SUMMARY_PAGE_ARTIFACT_MISMATCH' }
    if (!$caught) { throw 'Modified page handoff was accepted.' }

    Write-Host 'PASS focused Web Forms terminal inventory summary'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
