# EDIT ONLY THIS BLOCK.
$OutputRoot = 'C:\work\tracemap-output'
$ReportPath = '' # Optional: full path to one webforms-modernization.json file.
# END EDIT BLOCK.

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    if (-not (Test-Path -LiteralPath $OutputRoot -PathType Container)) {
        throw "The configured output root was not found: $OutputRoot"
    }

    $latest = Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'webforms-page-list-*' |
        ForEach-Object {
            $candidate = Join-Path $_.FullName 'webforms-modernization.json'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                Get-Item -LiteralPath $candidate
            }
        } |
        Sort-Object -Property LastWriteTimeUtc, FullName -Descending |
        Select-Object -First 1
    if ($null -eq $latest) {
        throw "No webforms-page-list-*/webforms-modernization.json report was found under: $OutputRoot"
    }
    $ReportPath = $latest.FullName
}

if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
    throw "The configured report was not found: $ReportPath"
}

$packet = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
$chains = @($packet.eventChains)
$selectionItems = @($packet.surfaceSelection.items)

$handlerUnavailable = @($chains | Where-Object { [string]::IsNullOrWhiteSpace($_.handlerFactId) })
$handlerResolvedTerminalUnavailable = @($chains | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_.handlerFactId) -and
    [string]::IsNullOrWhiteSpace($_.terminalKind)
})
$terminalResolved = @($chains | Where-Object { -not [string]::IsNullOrWhiteSpace($_.terminalKind) })
$exclusiveTotal = $handlerUnavailable.Count + $handlerResolvedTerminalUnavailable.Count + $terminalResolved.Count

Write-Host 'focused-webforms-exclusive-summary=completed'
Write-Host "reportFile=$([System.IO.Path]::GetFileName($ReportPath))"
Write-Host "truncated=$($packet.summary.truncated.ToString().ToLowerInvariant())"
Write-Host "requestedPages=$($packet.surfaceSelection.requestedCount)"
Write-Host "matchedPages=$($packet.surfaceSelection.matchedCount)"
Write-Host "totalEventChains=$($chains.Count)"
Write-Host "handlerUnavailableChains=$($handlerUnavailable.Count)"
Write-Host "handlerResolvedTerminalUnavailableChains=$($handlerResolvedTerminalUnavailable.Count)"
Write-Host "terminalResolvedChains=$($terminalResolved.Count)"
Write-Host "exclusiveCountCheck=$exclusiveTotal/$($chains.Count)"

$pageRows = foreach ($item in $selectionItems) {
    $surfaceIds = @($item.surfaceIds)
    $pageChains = @($chains | Where-Object { $surfaceIds -contains $_.surfaceId })
    $category = if ($item.status -ne 'matched') {
        $item.status
    }
    elseif ($pageChains.Count -eq 0) {
        'no-static-event-binding'
    }
    elseif (@($pageChains | Where-Object { -not [string]::IsNullOrWhiteSpace($_.terminalKind) }).Count -gt 0) {
        'terminal-resolved'
    }
    elseif (@($pageChains | Where-Object { -not [string]::IsNullOrWhiteSpace($_.handlerFactId) }).Count -gt 0) {
        'handler-resolved-terminal-unavailable'
    }
    else {
        'handler-unavailable'
    }

    [pscustomobject]@{
        Alias = $item.alias
        Category = $category
    }
}

foreach ($group in @($pageRows | Group-Object -Property Category | Sort-Object -Property Name)) {
    Write-Host "pageCategory=$($group.Name)|count=$($group.Count)"
    Write-Host "pageAliases-$($group.Name)=$((@($group.Group.Alias) -join ','))"
}

$terminalKinds = @($terminalResolved |
    Group-Object -Property terminalKind |
    Sort-Object -Property Name)
if ($terminalKinds.Count -eq 0) {
    Write-Host 'terminalKind=none|count=0'
}
else {
    foreach ($group in $terminalKinds) {
        Write-Host "terminalKind=$($group.Name)|count=$($group.Count)"
    }
}

Write-Host 'nonClaim=static-evidence-does-not-prove-runtime-execution-reachability-or-successful-binding'
