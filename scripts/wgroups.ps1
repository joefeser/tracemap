[CmdletBinding()]
param([string]$HandoffPath = '', [string]$SearchRoot = [IO.Path]::GetTempPath())

# Local console diagnostic only. No scan, report rewrite, upload or cleanup.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_GROUP_POWERSHELL_7_REQUIRED' }
function Hash-Text([string]$Value) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Value))).ToLowerInvariant()
}
function Read-Bounded([string]$Path, [long]$Limit) {
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $Limit) { throw 'WEBFORMS_GROUP_INPUT_LIMIT' }
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
            if (!$path.StartsWith($folder.FullName + [IO.Path]::DirectorySeparatorChar, $comparison)) { throw 'WEBFORMS_GROUP_HANDOFF_OUTSIDE_COMPARISON' }
            if (Test-Path -LiteralPath $path -PathType Leaf) { [pscustomobject]@{ Name = $folder.Name; Path = $path } }
        }
    )
    if ($candidates.Count -eq 0) { throw 'WEBFORMS_GROUP_COMPARISON_UNAVAILABLE' }
    for ($i = 0; $i -lt $candidates.Count; $i++) { Write-Host "[$($i + 1)] $($candidates[$i].Name)" }
    $choice = if ($candidates.Count -eq 1) { '1' } else { Read-Host 'Choose the comparison number' }
    $number = 0
    if (![int]::TryParse($choice, [ref]$number) -or $number -lt 1 -or $number -gt $candidates.Count) { throw 'WEBFORMS_GROUP_SELECTION_INVALID' }
    $HandoffPath = $candidates[$number - 1].Path
}
$HandoffPath = [IO.Path]::GetFullPath($HandoffPath)
$inputHash = (Get-FileHash -LiteralPath $HandoffPath -Algorithm SHA256).Hash.ToLowerInvariant()
$handoff = Read-Bounded $HandoffPath 64MB
if ($handoff.schemaVersion -cne 'webforms-compiled-path-handoff.v1' -or
    $handoff.ruleId -cne 'diagnostic.webforms.compiled-path-handoff.v1' -or
    $handoff.claimLevel -cne 'review-only-static-evidence' -or
    @($handoff.paths).Count -lt 1 -or @($handoff.paths).Count -gt 256) { throw 'WEBFORMS_GROUP_HANDOFF_INVALID' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$rows = @(
    foreach ($path in @($handoff.paths)) {
        $hops = @($path.hops)
        if (!$seen.Add([string]$path.pathId) -or [string]::IsNullOrWhiteSpace([string]$path.pathId) -or
            $path.claim -cne 'review-only-static-path' -or $hops.Count -lt 1 -or $hops.Count -gt 20) { throw 'WEBFORMS_GROUP_PATH_INVALID' }
        $nodes = @($hops[0].from) + @($hops | ForEach-Object { $_.to })
        for ($i = 0; $i -lt $hops.Count; $i++) {
            if ([string]::IsNullOrWhiteSpace([string]$hops[$i].from.name) -or
                [string]::IsNullOrWhiteSpace([string]$hops[$i].to.name) -or
                ($i -gt 0 -and $hops[$i].from.name -cne $hops[$i - 1].to.name)) { throw 'WEBFORMS_GROUP_PATH_INVALID' }
        }
        $exact = [ordered]@{ classification = $path.classification; claim = $path.claim; terminalKind = $path.terminalKind;
            nodes = @($nodes | ForEach-Object { [ordered]@{ name = $_.name; scanId = $_.scanId; commitSha = $_.commitSha } }) }
        $labels = [ordered]@{ classification = $path.classification; claim = $path.claim; terminalKind = $path.terminalKind;
            nodes = @($nodes | ForEach-Object { Label $_ }) }
        $evidence = [ordered]@{ hops = @($hops | ForEach-Object {
            [ordered]@{ edgeKind = $_.edgeKind; ruleId = $_.ruleId; evidenceTier = $_.evidenceTier;
                filePath = $_.filePath; startLine = $_.startLine; endLine = $_.endLine;
                supportingFactIds = @($_.supportingFactIds | Sort-Object -CaseSensitive) }
        }); supportingFactIds = @($path.supportingFactIds | Sort-Object -CaseSensitive) }
        [pscustomobject]@{ Id = [string]$path.pathId;
            Exact = Hash-Text ($exact | ConvertTo-Json -Depth 15 -Compress);
            Labels = Hash-Text ($labels | ConvertTo-Json -Depth 15 -Compress);
            Evidence = Hash-Text ($evidence | ConvertTo-Json -Depth 15 -Compress) }
    }
)
if ((Get-FileHash -LiteralPath $HandoffPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $inputHash) { throw 'WEBFORMS_GROUP_INPUT_CHANGED' }
$exactGroups = @($rows | Group-Object -Property Exact | Sort-Object Name)
$labelGroups = @($rows | Group-Object -Property Labels | Sort-Object Name)
Write-Output 'LOCAL diagnostic.webforms.path-grouping.v1: retained review-only evidence; no methods or file paths printed.'
Write-Output "Paths=$($rows.Count); exact method chains=$($exactGroups.Count); displayed chains=$($labelGroups.Count)"
Write-Output 'Repeated exact chains (same signatures and source identities; evidence may differ):'
foreach ($group in @($exactGroups | Where-Object Count -gt 1)) {
    $variants = @($group.Group.Evidence | Sort-Object -Unique -CaseSensitive).Count
    Write-Output "  $($group.Group.Id -join ', '): $($group.Count) paths; evidence variants=$variants"
}
Write-Output 'Repeated displayed chains (compact labels can hide overloads or source identities):'
foreach ($group in @($labelGroups | Where-Object Count -gt 1)) {
    $variants = @($group.Group.Exact | Sort-Object -Unique -CaseSensitive).Count
    Write-Output "  $($group.Group.Id -join ', '): $($group.Count) paths; exact chain variants=$variants"
}
Write-Output 'No evidence removed. Different source/IL bridge routes remain separate. This is not runtime deduplication.'
