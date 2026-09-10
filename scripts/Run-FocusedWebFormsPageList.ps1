param(
    [switch]$CompareDepths,
    [string]$OutputRootOverride = '',
    [string]$ConfigPath = ''
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'webforms-review/FocusedWebFormsConfig.ps1')
if (!$ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.json' }
$config = Read-FocusedWebFormsConfig -ConfigPath $ConfigPath
$IndexPath = $config.IndexPath
$OutputRoot = $config.OutputRoot
$pagePaths = @($config.Forms)

if ($OutputRootOverride) { $OutputRoot = $OutputRootOverride }
if ($CompareDepths) { throw 'Deeper comparison runs are disabled. Use Summarize-CompletedWebFormsDepths.ps1.' }

if (-not (Test-Path -LiteralPath $IndexPath -PathType Leaf)) {
    throw "The configured index.sqlite was not found: $IndexPath"
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputDirectory = Join-Path $OutputRoot "webforms-page-list-$timestamp"
$temporaryList = Join-Path ([System.IO.Path]::GetTempPath()) "tracemap-webforms-pages-$([Guid]::NewGuid().ToString('N')).txt"
$runner = Join-Path $PSScriptRoot 'Invoke-FocusedWebFormsPageListReport.ps1'

try {
    [System.IO.File]::WriteAllLines($temporaryList, $pagePaths, [System.Text.UTF8Encoding]::new($false))
    & $runner `
        -IndexPath $IndexPath `
        -PageListPath $temporaryList `
        -OutputDirectory $outputDirectory
}
finally {
    Remove-Item -LiteralPath $temporaryList -Force -ErrorAction SilentlyContinue
}
