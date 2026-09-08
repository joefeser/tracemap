param([switch]$CompareDepths)

# EDIT ONLY THIS BLOCK.
$IndexPath = 'C:\work\tracemap-output\focused-webforms-20260903-145829\scan\index.sqlite'
$OutputRoot = 'C:\work\tracemap-output'
$Forms = @'
# Put one repository-relative .aspx path on each line below.
# Example: source/CCS/Area/Orders.aspx
'@
# END EDIT BLOCK.

$ErrorActionPreference = 'Stop'
$pagePaths = @($Forms -split '\r?\n' | ForEach-Object { $_.Trim() } | Where-Object {
    $_ -and -not $_.StartsWith('#')
})

if ($pagePaths.Count -eq 0) {
    throw 'Add at least one .aspx path to the $Forms block at the top of this file.'
}

$invalidPages = @($pagePaths | Where-Object { -not $_.EndsWith('.aspx', [StringComparison]::OrdinalIgnoreCase) })
if ($invalidPages.Count -gt 0) {
    throw 'Every non-comment line in the $Forms block must end with .aspx.'
}

if (-not (Test-Path -LiteralPath $IndexPath -PathType Leaf)) {
    throw "The configured index.sqlite was not found: $IndexPath"
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputDirectory = Join-Path $OutputRoot "webforms-page-list-$timestamp"
$temporaryList = Join-Path ([System.IO.Path]::GetTempPath()) "tracemap-webforms-pages-$([Guid]::NewGuid().ToString('N')).txt"
$runner = Join-Path $PSScriptRoot 'Invoke-FocusedWebFormsPageListReport.ps1'

try {
    [System.IO.File]::WriteAllLines($temporaryList, $pagePaths, [System.Text.UTF8Encoding]::new($false))
    if ($CompareDepths) {
        $reports = @()
        $comparisonDirectory = Join-Path $OutputRoot "webforms-depth-comparison-$timestamp-$([Guid]::NewGuid().ToString('N'))"
        foreach ($depth in @(8, 10, 12)) {
            $depthOutput = Join-Path $comparisonDirectory "depth-$depth"
            & $runner -IndexPath $IndexPath -PageListPath $temporaryList -OutputDirectory $depthOutput -MaxDepth $depth
            $reports += Join-Path $depthOutput 'webforms-modernization.json'
        }
        & (Join-Path $PSScriptRoot 'Compare-FocusedWebFormsDepth.ps1') -ReportPaths $reports
    }
    else {
        & $runner `
            -IndexPath $IndexPath `
            -PageListPath $temporaryList `
            -OutputDirectory $outputDirectory
    }
}
finally {
    Remove-Item -LiteralPath $temporaryList -Force -ErrorAction SilentlyContinue
}
