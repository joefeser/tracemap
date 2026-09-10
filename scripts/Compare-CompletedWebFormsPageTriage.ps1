# Read-only: never launches a scan, build, or graph traversal.
param([string]$OutputRoot = 'C:\work\tracemap-output', [string]$ComparisonDirectory = '')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 is required.' }
if (-not $ComparisonDirectory) {
    $latest = Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'webforms-depth-comparison-*' |
        Where-Object { (Test-Path -LiteralPath (Join-Path $_.FullName 'depth-8/webforms-modernization.json')) -and (Test-Path -LiteralPath (Join-Path $_.FullName 'depth-10/webforms-modernization.json')) } |
        Sort-Object Name -Descending | Select-Object -First 1
    if ($null -eq $latest) { throw 'No completed depth-8/10 pair found.' }
    $ComparisonDirectory = $latest.FullName
}
$signatures = @(); $output = [Collections.Generic.List[string]]::new()
foreach ($depth in @(8,10)) {
    $path = Join-Path $ComparisonDirectory "depth-$depth/webforms-modernization.json"
    if ((Get-Item -LiteralPath $path).Length -gt 128MB) { throw 'Input exceeds 128 MiB limit.' }
    $stream = [IO.File]::OpenRead($path); $doc = $null
    try {
        $doc = [System.Text.Json.JsonDocument]::Parse($stream)
        $signatures += @{
            source=$doc.RootElement.GetProperty('sources').GetRawText()
            selection=$doc.RootElement.GetProperty('surfaceSelection').GetRawText()
        }
    } finally { if ($null -ne $doc) { $doc.Dispose() }; $stream.Dispose() }
    $output.Add("depth=$depth")
    foreach ($line in (& (Join-Path $PSScriptRoot 'Triage-CompletedWebFormsPages.ps1') -ReportPath $path 6>&1)) { $output.Add([string]$line) }
}
if ($signatures[0].source -cne $signatures[1].source -or $signatures[0].selection -cne $signatures[1].selection) { throw 'Provenance mismatch; comparison withheld.' }
Write-Host 'completed-page-comparison=read-only|provenance=matched'
$output | ForEach-Object { Write-Host $_ }
Write-Host 'nonClaim=node-associated-reasons-are-not-exclusive-page-causes;unlinked-truncation-reasons-remain-unknown'
