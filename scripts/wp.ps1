[CmdletBinding()]
param([string]$OutputRoot)

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

Write-Output "probeSelected=$($inventory.Count)"
Write-Output "probeOutcomes=$($outcomes.Count)"
Write-Output "probeOmitted=$omitted"
Write-Output "probeAdmitted=$admitted"
Write-Output "probeNonadmitted=$nonadmitted"
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
