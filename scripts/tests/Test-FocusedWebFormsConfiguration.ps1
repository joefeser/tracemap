$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$helperPath = Join-Path $scripts 'webforms-review/FocusedWebFormsConfig.ps1'
. $helperPath

$parsePaths = @(
    $helperPath,
    (Join-Path $scripts 'Run-FocusedWebFormsPageList.ps1'),
    (Join-Path $scripts 'Run-AndTriage-FocusedWebFormsPageList.ps1'),
    (Join-Path $scripts 'Test-FocusedWebFormsRawEvidence.ps1'),
    (Join-Path $scripts 'New-FocusedWebFormsBatchInspection.ps1'),
    (Join-Path $scripts 'New-FocusedWebFormsLocalInspection.ps1'),
    (Join-Path $scripts 'New-FocusedWebFormsMethodInspection.ps1'),
    (Join-Path $scripts 'Test-FocusedWebFormsDatabaseEvidence.ps1'),
    (Join-Path $scripts 'New-FocusedWebFormsCodePathReview.ps1'),
    (Join-Path $scripts 'New-FocusedWebFormsCodePathReviewSet.ps1'),
    (Join-Path $scripts 'New-FocusedWebFormsApplicationWorkbench.ps1'),
    (Join-Path $scripts 'webforms-review/Invoke-WitsApplicationReview.ps1')
)
foreach ($path in $parsePaths) {
    $tokens = $null
    $parseErrors = $null
    [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors) | Out-Null
    if ($parseErrors.Count -ne 0) { throw "PowerShell syntax is invalid: $(Split-Path -Leaf $path)" }
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-config-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null

function Write-TestConfig([object]$Value) {
    $path = Join-Path $temp ([Guid]::NewGuid().ToString('N') + '.json')
    if ($Value -is [string]) {
        [IO.File]::WriteAllText($path, $Value, [Text.UTF8Encoding]::new($false))
    }
    else {
        [IO.File]::WriteAllText($path, ($Value | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    }
    return $path
}

function Assert-ConfigFailure([string]$Path, [string]$ExpectedCode) {
    try {
        Read-FocusedWebFormsConfig -ConfigPath $Path | Out-Null
        throw "Expected configuration failure: $ExpectedCode"
    }
    catch {
        if (!$_.Exception.Message.StartsWith($ExpectedCode, [StringComparison]::Ordinal)) { throw }
    }
}

try {
    $validPath = Write-TestConfig ([ordered]@{
        indexPath = 'C:/private-output/scan/index.sqlite'
        outputRoot = 'C:/private-output'
        forms = @('source/WebApp/First.aspx', ' source/WebApp/Second.ASPX ')
    })
    $config = Read-FocusedWebFormsConfig -ConfigPath $validPath
    if ($config.IndexPath -ne 'C:/private-output/scan/index.sqlite') { throw 'Index path was not retained.' }
    if ($config.OutputRoot -ne 'C:/private-output') { throw 'Output root was not retained.' }
    if ($config.Forms.Count -ne 2 -or $config.Forms[1] -ne 'source/WebApp/Second.ASPX') { throw 'Form paths were not normalized.' }

    Assert-ConfigFailure -Path (Write-TestConfig '{') -ExpectedCode 'FocusedWebFormsConfigInvalidJson'
    Assert-ConfigFailure -Path (Write-TestConfig '["not","an","object"]') -ExpectedCode 'FocusedWebFormsConfigPropertiesInvalid'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = 'index.sqlite'; outputRoot = 'output'; forms = @('One.aspx'); extra = $true })) -ExpectedCode 'FocusedWebFormsConfigPropertiesInvalid'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = 'index.sqlite'; outputRoot = 'output'; forms = 'One.aspx' })) -ExpectedCode 'FocusedWebFormsConfigFormsInvalid'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = 'index.sqlite'; outputRoot = 'output'; forms = @('One.txt') })) -ExpectedCode 'FocusedWebFormsConfigFormsInvalid'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = 42; outputRoot = 'output'; forms = @('One.aspx') })) -ExpectedCode 'FocusedWebFormsConfigPathUnavailable'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = 'index.sqlite'; outputRoot = $true; forms = @('One.aspx') })) -ExpectedCode 'FocusedWebFormsConfigPathUnavailable'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = @('index.sqlite'); outputRoot = 'output'; forms = @('One.aspx') })) -ExpectedCode 'FocusedWebFormsConfigPathUnavailable'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = [ordered]@{ path = 'index.sqlite' }; outputRoot = 'output'; forms = @('One.aspx') })) -ExpectedCode 'FocusedWebFormsConfigPathUnavailable'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = $null; outputRoot = 'output'; forms = @('One.aspx') })) -ExpectedCode 'FocusedWebFormsConfigPathUnavailable'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = ' '; outputRoot = 'output'; forms = @('One.aspx') })) -ExpectedCode 'FocusedWebFormsConfigPathUnavailable'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = 'index.sqlite'; outputRoot = 'output'; forms = @('One.aspx', 42) })) -ExpectedCode 'FocusedWebFormsConfigFormsInvalid'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = 'index.sqlite'; outputRoot = 'output'; forms = @('One.aspx', $null) })) -ExpectedCode 'FocusedWebFormsConfigFormsInvalid'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = 'index.sqlite'; outputRoot = 'output'; forms = @('One.aspx', ' ') })) -ExpectedCode 'FocusedWebFormsConfigFormsInvalid'
    Assert-ConfigFailure -Path (Write-TestConfig ([ordered]@{ indexPath = 'index.sqlite'; outputRoot = 'output'; forms = @("One.aspx`nTwo.aspx") })) -ExpectedCode 'FocusedWebFormsConfigFormsInvalid'
    Assert-ConfigFailure -Path (Join-Path $temp 'missing.json') -ExpectedCode 'FocusedWebFormsConfigUnavailable'

    $oversizedPath = Join-Path $temp 'oversized.json'
    [IO.File]::WriteAllBytes($oversizedPath, [byte[]]::new(1MB + 1))
    Assert-ConfigFailure -Path $oversizedPath -ExpectedCode 'FocusedWebFormsConfigLimitReached'

    $indexPath = Join-Path $temp 'index.sqlite'
    $reportPath = Join-Path $temp 'webforms-modernization.json'
    $inspectionPath = Join-Path $temp 'webforms-local-inspection.json'
    [IO.File]::WriteAllText($indexPath, '')
    [IO.File]::WriteAllText($reportPath, '{}')
    [IO.File]::WriteAllText($inspectionPath, '{}')
    function dotnet { $global:LASTEXITCODE = 0 }
    $missingConfigPath = Join-Path $temp 'explicit-paths-do-not-need-config.json'
    & (Join-Path $scripts 'Test-FocusedWebFormsRawEvidence.ps1') -IndexPath $indexPath -ReportPath $reportPath -ConfigPath $missingConfigPath
    & (Join-Path $scripts 'Test-FocusedWebFormsRawEvidence.ps1') -DatabaseEvidence -IndexPath $indexPath -InspectionPath $inspectionPath -ConfigPath $missingConfigPath

    $gitignore = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $scripts) '.gitignore'))
    if (!$gitignore.Contains('/scripts/Run-FocusedWebFormsPageList.json', [StringComparison]::Ordinal)) {
        throw 'The private local configuration is not ignored.'
    }
    Write-Host 'PASS focused Web Forms local configuration'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
