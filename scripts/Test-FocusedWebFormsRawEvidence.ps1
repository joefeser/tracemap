param([string]$IndexPath = '', [string]$ReportPath = '', [string]$OutputRoot = '', [string]$InspectionPath = '', [switch]$CreateLocalInspection, [string]$StartingMethodName = '', [switch]$DatabaseEvidence, [switch]$BatchInspection, [string]$ConfigPath = '')

$ErrorActionPreference = 'Stop'
$isWindowsPlatform = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
if ($BatchInspection -and ($DatabaseEvidence -or $StartingMethodName)) { throw 'Batch inspection requires report-handler selection.' }
if (!$ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.json' }
$requiresOutputRoot = (!$DatabaseEvidence -and !$ReportPath) -or
    $CreateLocalInspection -or
    $BatchInspection -or
    ($DatabaseEvidence -and !$InspectionPath)
if (!$IndexPath -or ($requiresOutputRoot -and !$OutputRoot)) {
    . (Join-Path $PSScriptRoot 'webforms-review/FocusedWebFormsConfig.ps1')
    $config = Read-FocusedWebFormsConfig -ConfigPath $ConfigPath
    if (!$IndexPath) { $IndexPath = $config.IndexPath }
    if ($requiresOutputRoot -and !$OutputRoot) { $OutputRoot = $config.OutputRoot }
}
if (!$ReportPath -and !$DatabaseEvidence) {
    $latest = Get-ChildItem -LiteralPath $OutputRoot -Directory -Filter 'webforms-page-list-*' |
        ForEach-Object { $p = Join-Path $_.FullName 'webforms-modernization.json'; if (Test-Path -LiteralPath $p) { Get-Item -LiteralPath $p } } |
        Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1
    if ($null -eq $latest) { throw 'RawAuditReportUnavailable' }
    $ReportPath = $latest.FullName
}
if (!(Test-Path -LiteralPath $IndexPath -PathType Leaf) -or (!$DatabaseEvidence -and !(Test-Path -LiteralPath $ReportPath -PathType Leaf))) {
    throw 'RawAuditInputUnavailable'
}
$project = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/RawWebFormsEvidence.csproj'
if ($CreateLocalInspection -or $BatchInspection) {
    $directory = Join-Path $OutputRoot 'local-inspection-private'
    $null = New-Item -ItemType Directory -Path $directory -Force
    $prefix = if ($BatchInspection) { 'webforms-batch-inspection-' } else { 'webforms-local-inspection-' }
    $InspectionPath = Join-Path $directory ($prefix + [Guid]::NewGuid().ToString('N') + '.json')
}
Write-Host 'Building diagnostic helper; reading existing scan evidence.'
$buildOutput = & dotnet build $project -c Release --nologo -v quiet 2>&1
if ($LASTEXITCODE -ne 0) { throw 'RawAuditHelperBuildFailed; inspect the helper build locally.' }
$dll = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/bin/Release/net10.0/RawWebFormsEvidence.dll'
if ($BatchInspection) {
    & dotnet $dll --batch-inspection $IndexPath $ReportPath $InspectionPath
}
elseif ($DatabaseEvidence) {
    if (!$InspectionPath) {
        $latestInspection = Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'local-inspection-private') -File -Filter 'webforms-local-inspection-*.json' |
            Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1
        if ($null -eq $latestInspection) { throw 'No local inspection file was found.' }
        $InspectionPath = $latestInspection.FullName
    }
    $inspectionItem = Get-Item -LiteralPath $InspectionPath
    $safeName = if ($inspectionItem.Name -cmatch '^webforms-local-inspection-[a-f0-9]{32}\.json$') { $inspectionItem.Name } else { 'custom-name-withheld' }
    Write-Host "selectedInspectionFile=$safeName"
    Write-Host "selectedInspectionModifiedUtc=$($inspectionItem.LastWriteTimeUtc.ToString('o'))"
    Write-Host "selectedInspectionSha256=$((Get-FileHash -LiteralPath $InspectionPath -Algorithm SHA256).Hash.ToLowerInvariant())"
    & dotnet $dll --database-evidence $IndexPath $InspectionPath
}
elseif ($StartingMethodName -and !$InspectionPath) { throw 'Method inspection requires a local output file.' }
elseif ($StartingMethodName) { & dotnet $dll $IndexPath $ReportPath $InspectionPath $StartingMethodName }
elseif ($InspectionPath) { & dotnet $dll $IndexPath $ReportPath $InspectionPath }
else { & dotnet $dll $IndexPath $ReportPath }
if ($LASTEXITCODE -ne 0) { throw 'RawAuditFailed; no evidence conclusion is available.' }
if ($BatchInspection) {
    $markdownPath = [System.IO.Path]::ChangeExtension($InspectionPath, '.md')
    Write-Host "PRIVATE local review: $(Split-Path -Leaf $markdownPath)"
    if ($isWindowsPlatform) { Start-Process -FilePath notepad.exe -ArgumentList ('"' + $markdownPath + '"') }
}
