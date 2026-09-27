[CmdletBinding()]
param(
    [string]$OutputRoot,
    [Parameter(Mandatory)][string]$TypeName,
    [Parameter(Mandatory)][string]$MethodName
)

# Inspect one compiled method family in an existing Web Forms scan. Print counts only.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_PROBE_POWERSHELL_7_REQUIRED' }
if (!$OutputRoot) {
    $latest = Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory |
        Where-Object { $_.Name -cmatch '^tracemap-existing-publish-[0-9a-f]{32}$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'scan/facts.ndjson') -PathType Leaf) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (!$latest) { throw 'WEBFORMS_PROBE_SCAN_UNAVAILABLE' }
    $OutputRoot = $latest.FullName
}
$factsPath = Join-Path $OutputRoot 'scan/facts.ndjson'
$manifestPath = Join-Path $OutputRoot 'scan/scan-manifest.json'
if (!(Test-Path -LiteralPath $factsPath -PathType Leaf) -or
    !(Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'WEBFORMS_PROBE_SCAN_UNAVAILABLE'
}
$typeMarker = '|names:' + $TypeName.Length + ':' + $TypeName + '|arity:'
$methodMarker = '|method:' + $MethodName.Length + ':' + $MethodName + '|'
$methods = [System.Collections.Generic.List[object]]::new()
$bodies = [System.Collections.Generic.List[object]]::new()
foreach ($line in [IO.File]::ReadLines($factsPath)) {
    if ($line.Contains('"factType":"ManagedMethodDeclared"', [StringComparison]::Ordinal)) {
        $fact = $line | ConvertFrom-Json -Depth 30
        if (([string]$fact.targetSymbol).Contains($typeMarker, [StringComparison]::Ordinal) -and
            ([string]$fact.targetSymbol).Contains($methodMarker, [StringComparison]::Ordinal)) {
            $methods.Add($fact)
        }
    } elseif ($line.Contains('"factType":"ManagedIlBodyDeclared"', [StringComparison]::Ordinal)) {
        $bodies.Add(($line | ConvertFrom-Json -Depth 30))
    }
}
$hashes = @($methods | ForEach-Object { [string]$_.properties.rawFileSha256 } |
    Where-Object { $_ } | Sort-Object -Unique)
$methodIds = @($methods | ForEach-Object { [string]$_.factId })
$methodKeys = @($methods | ForEach-Object {
    [string]$_.properties.rawFileSha256 + '|' + [string]$_.properties.metadataToken
})
$linkedBodies = @($bodies | Where-Object {
    $link = $_.properties.PSObject.Properties['compiledFactId']
    $null -ne $link -and [string]$link.Value -in $methodIds
})
$matchedBodies = @($bodies | Where-Object {
    $hash = $_.properties.PSObject.Properties['rawFileSha256']
    $token = $_.properties.PSObject.Properties['metadataToken']
    ($null -ne $hash -and $null -ne $token -and
        ([string]$hash.Value + '|' + [string]$token.Value) -in $methodKeys)
})
$bodyIds = @($matchedBodies | ForEach-Object { [string]$_.factId })
$callCount = 0
$fillCalls = 0
if ($bodyIds.Count -gt 0) {
    foreach ($line in [IO.File]::ReadLines($factsPath)) {
        if (!$line.Contains('"factType":"ManagedIlCallObserved"', [StringComparison]::Ordinal)) { continue }
        $fact = $line | ConvertFrom-Json -Depth 30
        if ([string]$fact.properties.ilBodyFactId -notin $bodyIds) { continue }
        $callCount++
        if ([string]$fact.properties.targetIdentity -match '\|method:4:Fill\|') { $fillCalls++ }
    }
}
$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 30
$ilOutcomes = @($manifest.ilBodyProvenance.outcomes | Where-Object {
    $hashProperty = $_.PSObject.Properties['rawFileSha256']
    $null -ne $hashProperty -and [string]$hashProperty.Value -in $hashes
})
Write-Output "methodAssemblyCount=$($hashes.Count)"
Write-Output "methodCount=$($methods.Count)"
Write-Output "methodBodyCount=$($matchedBodies.Count)"
Write-Output "methodLinkedBodyCount=$($linkedBodies.Count)"
Write-Output "methodCallCount=$callCount"
Write-Output "methodFillCallCount=$fillCalls"
Write-Output "methodIlOutcomeCount=$($ilOutcomes.Count)"
foreach ($group in @($ilOutcomes | Group-Object -Property outcome | Sort-Object Name)) {
    $label = if ([string]$group.Name -cmatch '^[a-z][a-z0-9-]{0,63}$') { $group.Name } else { 'other' }
    Write-Output "methodIlOutcome.$label=$($group.Count)"
}
$gapKinds = @($ilOutcomes | ForEach-Object { @($_.gapKinds) } |
    Where-Object { $_ -is [string] -and $_ -cmatch '^[A-Za-z][A-Za-z0-9]{0,79}$' })
foreach ($group in @($gapKinds | Group-Object | Sort-Object Name)) {
    Write-Output "methodIlGap.$($group.Name)=$($group.Count)"
}
