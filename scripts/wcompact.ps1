[CmdletBinding()]
param([string]$HandoffPath = '', [string]$SearchRoot = [IO.Path]::GetTempPath(), [switch]$NoOpen)

# A separate local-only grouped view. The authoritative handoff is never rewritten.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_COMPACT_POWERSHELL_7_REQUIRED' }
function Html([object]$Value) { [Net.WebUtility]::HtmlEncode([string]$Value) }
function Hash-Text([string]$Value) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Value))).ToLowerInvariant()
}
function Read-Bounded([string]$Path, [long]$Limit) {
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $Limit) { throw 'WEBFORMS_COMPACT_INPUT_LIMIT' }
    [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -Depth 50
}
function Label([object]$Node) {
    $property = $Node.PSObject.Properties['displayLabel']
    if ($null -ne $property -and ![string]::IsNullOrWhiteSpace([string]$property.Value)) { return [string]$property.Value }
    return [string]$Node.name
}
if (!$HandoffPath) {
    $candidates = @(
        foreach ($folder in @(Get-ChildItem -LiteralPath $SearchRoot -Directory -Filter 'tracemap-report-compare-*' | Sort-Object Name)) {
            $receiptPath = Join-Path $folder.FullName 'comparison.receipt.local.json'
            if (!(Test-Path -LiteralPath $receiptPath -PathType Leaf)) { continue }
            $receipt = Read-Bounded $receiptPath 1MB
            if ($receipt.schemaVersion -cne 'webforms-report-comparison.v1') { continue }
            $path = [IO.Path]::GetFullPath((Join-Path $receipt.reviews.after.root 'workbench/compiled-paths.local.json'))
            $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
            if (!$path.StartsWith($folder.FullName + [IO.Path]::DirectorySeparatorChar, $comparison)) { throw 'WEBFORMS_COMPACT_HANDOFF_OUTSIDE_COMPARISON' }
            if (Test-Path -LiteralPath $path -PathType Leaf) { [pscustomobject]@{ Name = $folder.Name; Path = $path } }
        }
    )
    if ($candidates.Count -eq 0) { throw 'WEBFORMS_COMPACT_COMPARISON_UNAVAILABLE' }
    for ($i = 0; $i -lt $candidates.Count; $i++) { Write-Host "[$($i + 1)] $($candidates[$i].Name)" }
    $choice = if ($candidates.Count -eq 1) { '1' } else { Read-Host 'Choose the comparison number' }
    $number = 0
    if (![int]::TryParse($choice, [ref]$number) -or $number -lt 1 -or $number -gt $candidates.Count) { throw 'WEBFORMS_COMPACT_SELECTION_INVALID' }
    $HandoffPath = $candidates[$number - 1].Path
}
$HandoffPath = [IO.Path]::GetFullPath($HandoffPath)
$inputHash = (Get-FileHash -LiteralPath $HandoffPath -Algorithm SHA256).Hash.ToLowerInvariant()
$handoff = Read-Bounded $HandoffPath 64MB
if ($handoff.schemaVersion -cne 'webforms-compiled-path-handoff.v1' -or
    $handoff.ruleId -cne 'diagnostic.webforms.compiled-path-handoff.v1' -or
    $handoff.claimLevel -cne 'review-only-static-evidence' -or
    @($handoff.paths).Count -lt 1 -or @($handoff.paths).Count -gt 256) { throw 'WEBFORMS_COMPACT_HANDOFF_INVALID' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$rows = @(
    foreach ($path in @($handoff.paths)) {
        $hops = @($path.hops)
        if (!$seen.Add([string]$path.pathId) -or [string]::IsNullOrWhiteSpace([string]$path.pathId) -or
            $path.claim -cne 'review-only-static-path' -or $hops.Count -lt 1 -or $hops.Count -gt 20) { throw 'WEBFORMS_COMPACT_PATH_INVALID' }
        for ($i = 0; $i -lt $hops.Count; $i++) {
            if ([string]::IsNullOrWhiteSpace([string]$hops[$i].from.name) -or
                [string]::IsNullOrWhiteSpace([string]$hops[$i].to.name) -or
                [string]::IsNullOrWhiteSpace([string]$hops[$i].ruleId) -or
                $hops[$i].evidenceTier -cnotin @('Tier1Semantic','Tier2Structural','Tier3SyntaxOrTextual','Tier4Unknown') -or
                ($i -gt 0 -and $hops[$i].from.name -cne $hops[$i - 1].to.name)) { throw 'WEBFORMS_COMPACT_PATH_INVALID' }
        }
        $nodes = @($hops[0].from) + @($hops | ForEach-Object { $_.to })
        $key = [ordered]@{ classification = $path.classification; claim = $path.claim; terminalKind = $path.terminalKind;
            nodes = @($nodes | ForEach-Object { [ordered]@{ name = $_.name; scanId = $_.scanId; commitSha = $_.commitSha } }) }
        [pscustomobject]@{ Key = Hash-Text ($key | ConvertTo-Json -Depth 15 -Compress); Path = $path }
    }
)
$groups = @($rows | Group-Object Key | Sort-Object Name)
$number = 0
$sections = foreach ($group in $groups) {
    $number++
    $paths = @($group.Group | ForEach-Object { $_.Path })
    $representative = $paths[0]
    $methodRows = foreach ($hop in @($representative.hops)) {
        '<tr><td>{0}</td><td>{1} → {2}<details><summary>Exact method signatures and source identities</summary><pre>{3}</pre><pre>{4}</pre><p>From scan {5}; commit {6}. To scan {7}; commit {8}.</p></details></td></tr>' -f `
            (Html $hop.ordinal), (Html (Label $hop.from)), (Html (Label $hop.to)),
            (Html $hop.from.name), (Html $hop.to.name), (Html $hop.from.scanId), (Html $hop.from.commitSha),
            (Html $hop.to.scanId), (Html $hop.to.commitSha)
    }
    $variants = foreach ($path in $paths) {
        $evidenceRows = foreach ($hop in @($path.hops)) {
            '<tr><td>{0}</td><td>{1}<br>{2}<br>{3}</td><td>{4}:{5}-{6}</td><td>{7}</td></tr>' -f `
                (Html $hop.ordinal), (Html $hop.edgeKind), (Html $hop.ruleId), (Html $hop.evidenceTier),
                (Html $hop.filePath), (Html $hop.startLine), (Html $hop.endLine), (Html (@($hop.supportingFactIds) -join ', '))
        }
        '<details data-path-id="{0}"><summary>{0} — evidence variant</summary><p>Path supporting facts: {1}</p><div class="table-wrap"><table><thead><tr><th>Hop</th><th>Edge / rule / tier</th><th>Location</th><th>Supporting facts</th></tr></thead><tbody>{2}</tbody></table></div></details>' -f `
            (Html $path.pathId), (Html (@($path.supportingFactIds) -join ', ')), ($evidenceRows -join '')
    }
    '<section><h2>Chain {0}: {1} → {2}</h2><p>{3} retained evidence paths: {4}</p><p>{5}; {6}; terminal {7}. No tier is promoted or selected for this group.</p><details><summary>Method sequence ({8} hops)</summary><table><thead><tr><th>#</th><th>Method transition</th></tr></thead><tbody>{9}</tbody></table></details><details><summary>All evidence variants ({3})</summary>{10}</details></section>' -f `
        $number, (Html (Label $representative.hops[0].from)), (Html (Label $representative.hops[-1].to)),
        $paths.Count, (Html (@($paths.pathId) -join ', ')), (Html $representative.classification),
        (Html $representative.claim), (Html $representative.terminalKind), @($representative.hops).Count,
        ($methodRows -join ''), ($variants -join '')
}
$sourceUri = ([Uri]$HandoffPath).AbsoluteUri
$generatorHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$page = @"
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Grouped compiled method chains</title><style>body{font:16px system-ui,sans-serif;color:#172033;max-width:1400px;margin:24px auto;padding:0 16px}section{border:1px solid #cbd5e1;border-radius:8px;padding:16px;margin:16px 0}details{margin:12px 0}summary{cursor:pointer}table{border-collapse:collapse;width:100%;table-layout:fixed}th,td{border:1px solid #cbd5e1;padding:8px;text-align:left;vertical-align:top;overflow-wrap:anywhere}th:first-child{width:45px}pre{white-space:pre-wrap;overflow-wrap:anywhere}.warning{background:#fff0d8;padding:12px;border-left:4px solid #a05a00}.table-wrap{overflow-x:auto}</style></head><body><h1>Grouped compiled method chains</h1><p class="warning">LOCAL ONLY · $($groups.Count) distinct exact method chains; $($rows.Count) retained evidence paths. Review-only static evidence, not independent runtime executions or proof that SQL ran.</p><p>Rule diagnostic.webforms.compact-path-review.v1. Same exact signatures, scan/commit identities, classification and terminal are grouped. Distinct source/IL bridge routes remain separate. Every original path has an evidence-variant entry; the original handoff is unchanged.</p><p>Coverage truncated: $(Html $handoff.coverage.truncated); retained gap count: $(Html $handoff.coverage.gapCount). <a href="$(Html $sourceUri)">Original exact handoff JSON, DLL provenance and complete retained gaps</a>.</p>$($sections -join '')<details><summary>Generator and input provenance</summary><p>Generator SHA-256: $generatorHash</p><p>Bounded input SHA-256: $inputHash</p></details></body></html>
"@
if ((Get-FileHash -LiteralPath $HandoffPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $inputHash) { throw 'WEBFORMS_COMPACT_INPUT_CHANGED' }
$output = Join-Path (Split-Path $HandoffPath -Parent) ('compiled-paths.grouped-' + [Guid]::NewGuid().ToString('N') + '.local.html')
# CreateNew prevents overwriting any retained artifact.
$stream = [IO.FileStream]::new($output, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
try {
    $bytes = [Text.Encoding]::UTF8.GetBytes($page)
    $stream.Write($bytes, 0, $bytes.Length)
} finally { $stream.Dispose() }
Write-Output "compactChains=$($groups.Count)"
Write-Output "compactEvidencePaths=$($rows.Count)"
Write-Output "compactHtml=$output"
if (!$NoOpen) { Start-Process $output }
