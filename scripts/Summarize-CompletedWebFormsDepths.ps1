# Read-only: never invokes TraceMap, dotnet, a scan, or another script.
# EDIT ONLY THIS BLOCK if automatic discovery does not find your reports.
param(
    [string]$OutputRoot = 'C:\work\tracemap-output',
    [string]$ComparisonDirectory = '',
    [switch]$Details
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run this script in PowerShell 7.' }
if (-not $ComparisonDirectory) {
    $latest = Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'webforms-depth-comparison-*' |
        Where-Object {
            (Test-Path -LiteralPath (Join-Path $_.FullName 'depth-8/webforms-modernization.json') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'depth-10/webforms-modernization.json') -PathType Leaf)
        } | Sort-Object Name -Descending | Select-Object -First 1
    if ($null -eq $latest) { throw 'No comparison directory contains both completed depth-8 and depth-10 JSON files.' }
    $ComparisonDirectory = $latest.FullName
}

function Get-OptionalString([System.Text.Json.JsonElement]$Element, [string]$Name) {
    $value = [System.Text.Json.JsonElement]::new()
    if (-not $Element.TryGetProperty($Name, [ref]$value) -or $value.ValueKind -eq [System.Text.Json.JsonValueKind]::Null) { return $null }
    return $value.GetString()
}

function Read-SmallSummary([string]$Path) {
    if ((Get-Item -LiteralPath $Path).Length -gt 128MB) { throw 'Report exceeds the 128 MiB input safety limit. No traversal was started.' }
    $stream = [IO.File]::OpenRead($Path)
    $document = $null
    try {
        # One bounded JSON document at a time; do not expand into PowerShell objects.
        $document = [System.Text.Json.JsonDocument]::Parse($stream)
        $root = $document.RootElement
        $source = $root.GetProperty('sources').GetRawText()
        $selection = $root.GetProperty('surfaceSelection')
        $items = $selection.GetProperty('items')
        if ($items.GetArrayLength() -gt 1000) { throw 'Page safety limit exceeded.' }
        $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $pages = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $boundaries = $root.GetProperty('downstreamBoundaries')
        if ($boundaries.GetArrayLength() -gt 10000) { throw 'Boundary safety limit exceeded.' }
        foreach ($boundary in $boundaries.EnumerateArray()) {
            $parts = foreach ($name in @('boundaryKind', 'boundaryTargetId', 'terminalEvidenceId')) {
                $value = $boundary.GetProperty($name).GetString()
                if ([string]::IsNullOrWhiteSpace($value)) { throw 'Incomplete terminal identity.' }
                "$($value.Length):$value"
            }
            [void]$keys.Add(($parts -join ''))
            [void]$pages.Add($boundary.GetProperty('surfaceId').GetString())
        }
        $aliases = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($item in $items.EnumerateArray()) {
            foreach ($surface in $item.GetProperty('surfaceIds').EnumerateArray()) {
                if ($pages.Contains($surface.GetString())) {
                    $alias = $item.GetProperty('alias').GetString()
                    if ($alias -cnotmatch '^page-[0-9]+$') { throw 'Unexpected page alias; summary withheld.' }
                    [void]$aliases.Add($alias)
                }
            }
        }
        $chains = $root.GetProperty('eventChains')
        if ($chains.GetArrayLength() -gt 10000) { throw 'Chain safety limit exceeded.' }
        $missingHandler = 0; $missingTerminal = 0
        $missingBySurface = @{}
        foreach ($chain in $chains.EnumerateArray()) {
            if ([string]::IsNullOrWhiteSpace((Get-OptionalString $chain 'handlerFactId'))) {
                $missingHandler++
                $surface = $chain.GetProperty('surfaceId').GetString()
                $missingBySurface[$surface] = 1 + [int]$missingBySurface[$surface]
            }
            elseif ([string]::IsNullOrWhiteSpace((Get-OptionalString $chain 'terminalKind'))) { $missingTerminal++ }
        }
        $detailsRows = @()
        foreach ($item in $items.EnumerateArray()) {
            $alias = $item.GetProperty('alias').GetString()
            if ($alias -cnotmatch '^page-[0-9]+$') { throw 'Unexpected page alias; summary withheld.' }
            $missing = 0
            foreach ($surface in $item.GetProperty('surfaceIds').EnumerateArray()) { $missing += [int]$missingBySurface[$surface.GetString()] }
            if (-not $aliases.Contains($alias) -or $missing -gt 0) {
                $detailsRows += "page=$alias|hasTerminal=$($aliases.Contains($alias))|handlerUnavailableChains=$missing"
            }
        }
        $counts = @{ cycle=0; depth=0; frontier=0; path=0; work=0; unavailable=0; otherLimits=0 }
        foreach ($gap in $root.GetProperty('gaps').EnumerateArray()) {
            $classification = $gap.GetProperty('classification').GetString()
            if ($classification -eq 'TruncatedByLimit') {
                $reasonElement = [System.Text.Json.JsonElement]::new()
                $reason = if ($gap.TryGetProperty('truncationReason', [ref]$reasonElement)) { $reasonElement.GetString() } else { '' }
                if ($reason -cnotin @('cycle','depth','frontier','path','work')) { $reason = 'unavailable' }
                $counts[$reason]++
            }
            elseif ($classification -match 'LimitReached') { $counts.otherLimits++ }
        }
        return @{
            Source=$source; Selection=$selection.GetRawText(); Keys=$keys; Aliases=$aliases
            Truncated=$root.GetProperty('summary').GetProperty('truncated').GetBoolean()
            Boundaries=$boundaries.GetArrayLength(); Chains=$chains.GetArrayLength()
            MissingHandler=$missingHandler; MissingTerminal=$missingTerminal; Gaps=$counts; Details=$detailsRows
        }
    }
    finally {
        if ($null -ne $document) { $document.Dispose() }
        $stream.Dispose()
    }
}

$a = Read-SmallSummary (Join-Path $ComparisonDirectory 'depth-8/webforms-modernization.json')
$b = Read-SmallSummary (Join-Path $ComparisonDirectory 'depth-10/webforms-modernization.json')
if ($a.Source -cne $b.Source -or $a.Selection -cne $b.Selection) { throw 'Source provenance or page selection differs; comparison withheld.' }
Write-Host 'completed-depth-summary=read-only;provenance=matched'
foreach ($entry in @(@{Depth=8; Data=$a}, @{Depth=10; Data=$b})) {
    $s=$entry.Data; $d=$entry.Depth
    Write-Host "depth=$d|partial=$($s.Truncated)|chains=$($s.Chains)|boundaryRecords=$($s.Boundaries)|distinctTerminalEvidence=$($s.Keys.Count)|pagesWithTerminal=$($s.Aliases.Count)"
    Write-Host "depth=$d|handlerUnavailable=$($s.MissingHandler)|resolvedWithoutTerminal=$($s.MissingTerminal)"
    Write-Host "depth=$d|cycle=$($s.Gaps.cycle)|depthGap=$($s.Gaps.depth)|frontier=$($s.Gaps.frontier)|path=$($s.Gaps.path)|work=$($s.Gaps.work)|unknownReason=$($s.Gaps.unavailable)|otherLimits=$($s.Gaps.otherLimits)"
}
$added = @(@($b.Keys) | Where-Object { -not $a.Keys.Contains($_) }).Count
$lost = @(@($a.Keys) | Where-Object { -not $b.Keys.Contains($_) }).Count
$gainedPages = @($b.Aliases | Where-Object { -not $a.Aliases.Contains($_) } | Sort-Object)
$lostPages = @($a.Aliases | Where-Object { -not $b.Aliases.Contains($_) } | Sort-Object)
Write-Host "terminalDelta=added:$added|lost:$lost"
Write-Host "pagesGainingTerminal=$($gainedPages -join ',')"
Write-Host "pagesLosingTerminal=$($lostPages -join ',')"
if ($Details) {
    Write-Host 'baseline-depth-8-targeted-pages;missing-terminal-is-not-proof-of-absence'
    foreach ($row in $a.Details) { Write-Host $row }
}
Write-Host 'nonClaim=distinct-evidence-tuples-not-runtime-operations;partial-results-do-not-prove-absence'
