param(
    [string]$OutputRoot = 'C:\work\tracemap-output',
    [string]$ReportPath = ''
)

$ErrorActionPreference = 'Stop'
$maximumReportBytes = 128MB

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $latest = Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'webforms-page-list-*' |
        ForEach-Object {
            $candidate = Join-Path $_.FullName 'webforms-modernization.json'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { Get-Item -LiteralPath $candidate }
        } |
        Sort-Object -Property LastWriteTimeUtc, FullName -Descending |
        Select-Object -First 1
    if ($null -eq $latest) { throw 'No completed focused Web Forms report was found.' }
    $ReportPath = $latest.FullName
}

$report = Get-Item -LiteralPath $ReportPath -ErrorAction Stop
if ($report.Length -gt $maximumReportBytes) { throw 'FocusedWebFormsActionableSummaryInputLimitReached' }
$packet = Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json
if ($packet.schemaVersion -ne 'webforms-modernization-packet.v1') { throw 'FocusedWebFormsActionableSummarySchemaUnsupported' }

$aliasBySurface = @{}
foreach ($item in @($packet.surfaceSelection.items)) {
    $alias = if ([string]$item.alias -match '^page-[0-9]{3,}$') { [string]$item.alias } else { 'page-alias-unavailable' }
    foreach ($surfaceId in @($item.surfaceIds)) { $aliasBySurface[[string]$surfaceId] = $alias }
}

function Values([object]$value, [string]$propertyName) {
    $property = $value.PSObject.Properties[$propertyName]
    if ($null -eq $property -or $null -eq $property.Value) { return @() }
    return @($property.Value)
}

function Alias([object]$chain) {
    $surfaceId = [string]$chain.surfaceId
    if ($aliasBySurface.ContainsKey($surfaceId)) { return $aliasBySurface[$surfaceId] }
    return 'page-alias-unavailable'
}

function SafeRule([string]$value) {
    if ($value -cmatch '^[a-z0-9][a-z0-9._-]{0,127}$') { return $value }
    return 'unavailable'
}

function SafeTier([string]$value) {
    if ($value -cin @('Tier1Semantic', 'Tier2Structural', 'Tier3SyntaxOrTextual', 'Tier4Unknown')) { return $value }
    return 'unavailable'
}

function SafeClosedValue([string]$value) {
    if ($value -cmatch '^[A-Za-z][A-Za-z0-9-]{0,79}$') { return $value }
    return 'unavailable'
}

$gaps = @($packet.gaps)
$rows = foreach ($chain in @($packet.eventChains)) {
    if (-not [string]::IsNullOrWhiteSpace($chain.terminalKind)) { continue }
    $observation = $chain.traversalObservation
    $bucket = if ([string]::IsNullOrWhiteSpace($chain.handlerFactId)) {
        'handler-resolution-unavailable'
    }
    elseif ($null -ne $observation -and $observation.stopState -eq 'no-observed-downstream-edge') {
        'handler-call-evidence-missing'
    }
    elseif ($null -ne $observation -and $observation.stopState -eq 'observed-downstream-without-supported-terminal') {
        'terminal-coverage-review'
    }
    elseif ($null -ne $observation -and $observation.stopState -eq 'bounded-traversal-truncated') {
        'bounded-traversal-truncated'
    }
    else {
        'unresolved-state-unavailable'
    }

    $support = @{}
    foreach ($id in @(Values $chain 'supportingFactIds')) { $support[[string]$id] = $true }
    $linkedGaps = @($gaps | Where-Object {
        $linked = $false
        foreach ($id in @(Values $_ 'supportingFactIds')) {
            if ($support.ContainsKey([string]$id)) { $linked = $true; break }
        }
        $linked
    } | ForEach-Object { SafeClosedValue ([string]$_.classification) } | Sort-Object -Unique)

    [pscustomobject]@{
        Bucket = $bucket
        Alias = Alias $chain
        Rules = @((Values $chain 'ruleIds') | ForEach-Object { SafeRule ([string]$_) } | Sort-Object -Unique)
        Tiers = @((Values $chain 'evidenceTiers') | ForEach-Object { SafeTier ([string]$_) } | Sort-Object -Unique)
        Coverage = @((Values $chain 'coverageLabels') | ForEach-Object { SafeClosedValue ([string]$_) } | Sort-Object -Unique)
        LinkedGaps = $linkedGaps
    }
}

Write-Host 'focused-webforms-actionable-gap-summary=completed'
Write-Host "reportFile=$($report.Name)"
Write-Host "packetPartial=$([bool]$packet.summary.truncated)"
Write-Host 'countScope=retained-report-only'

foreach ($bucket in @('handler-resolution-unavailable', 'handler-call-evidence-missing', 'terminal-coverage-review', 'bounded-traversal-truncated', 'unresolved-state-unavailable')) {
    $selected = @($rows | Where-Object Bucket -eq $bucket)
    $aliases = @($selected.Alias | Sort-Object -Unique)
    Write-Host "bucket=$bucket|chains=$($selected.Count)|pages=$($aliases.Count)"
    Write-Host "bucketAliases-$bucket=$($aliases -join ',')"
    foreach ($definition in @(
        @{ Name = 'rule'; Property = 'Rules' },
        @{ Name = 'tier'; Property = 'Tiers' },
        @{ Name = 'coverage'; Property = 'Coverage' },
        @{ Name = 'linkedGap'; Property = 'LinkedGaps' }
    )) {
        $values = @($selected | ForEach-Object { @($_.($definition.Property)) } | Group-Object | Sort-Object Name)
        if ($values.Count -eq 0) {
            Write-Host "bucket$($definition.Name)-$bucket=none|chains=0"
        }
        else {
            foreach ($group in $values) {
                Write-Host "bucket$($definition.Name)-$bucket=$($group.Name)|chains=$($group.Count)"
            }
        }
    }
}

Write-Host 'priority01=terminal-coverage-review'
Write-Host 'priority02=handler-call-evidence-missing'
Write-Host 'priority03=handler-resolution-unavailable'
Write-Host 'defer=bounded-traversal-truncated'
Write-Host 'nonClaim=retained-static-evidence-does-not-prove-runtime-execution-or-absence'
