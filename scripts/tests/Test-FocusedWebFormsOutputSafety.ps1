$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
. (Join-Path $scripts 'webforms-review/FocusedWebFormsPipelineConfig.ps1')
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-output-safety-' + [Guid]::NewGuid().ToString('N'))
# Resolve the OS temporary-directory alias before testing operator-created links.
if ($IsMacOS -and $temp.StartsWith('/var/')) { $temp = '/private' + $temp }
if ($IsMacOS -and $temp.StartsWith('/tmp/')) { $temp = '/private' + $temp }
try {
    $root = Join-Path $temp 'review'
    $config = Join-Path $root 'config'
    [IO.Directory]::CreateDirectory($config) | Out-Null
    $destination = Join-Path $config 'selected-pages.txt'
    Write-FocusedWebFormsPageList $destination @('One.aspx', 'Two.aspx')
    Write-FocusedWebFormsPageList $destination @('Three.aspx')
    if ([IO.File]::ReadAllText($destination).Trim() -ne 'Three.aspx') { throw 'Regular page-list replacement failed.' }
    [IO.File]::Delete($destination)
    $outside = Join-Path $temp 'outside.txt'
    [IO.File]::WriteAllText($outside, 'must remain unchanged')
    New-Item -ItemType HardLink -Path $destination -Target $outside | Out-Null
    Write-FocusedWebFormsPageList $destination @('Replacement.aspx')
    if ([IO.File]::ReadAllText($outside) -ne 'must remain unchanged') { throw 'Hard-linked external file changed.' }
    [IO.File]::Delete($destination)
    [IO.File]::CreateSymbolicLink($destination, $outside) | Out-Null
    foreach ($action in @(
        { Write-FocusedWebFormsPageList $destination @('Bad.aspx') },
        { & (Join-Path $scripts 'Invoke-FocusedWebFormsPipeline.ps1') -ReviewRoot $root }
    )) {
        $failure = $null
        try { & $action | Out-Null } catch { $failure = $_.Exception.Message }
        if ($failure -ne 'WEBFORMS_PIPELINE_LINKED_OUTPUT') { throw "Linked output was not rejected: $failure" }
        if ([IO.File]::ReadAllText($outside) -ne 'must remain unchanged') { throw 'External file changed.' }
    }
    [IO.File]::Delete($destination)
    $missing = Join-Path $temp 'missing.txt'
    [IO.File]::CreateSymbolicLink($destination, $missing) | Out-Null
    $failure = $null
    try { Write-FocusedWebFormsPageList $destination @('Bad.aspx') } catch { $failure = $_.Exception.Message }
    if ($failure -ne 'WEBFORMS_PIPELINE_LINKED_OUTPUT' -or [IO.File]::Exists($missing)) { throw 'Dangling link was followed.' }
    [IO.File]::Delete($destination)
    $alias = Join-Path $temp 'alias'
    [IO.Directory]::CreateSymbolicLink($alias, $root) | Out-Null
    foreach ($path in @($alias, (Join-Path $alias 'config/selected-pages.txt'))) {
        $failure = $null
        try { Assert-FocusedWebFormsUnlinkedPath $path } catch { $failure = $_.Exception.Message }
        if ($failure -ne 'WEBFORMS_PIPELINE_LINKED_OUTPUT') { throw 'Linked ancestor was not rejected.' }
    }
    [IO.Directory]::Delete($alias)
    if (@(Get-ChildItem -LiteralPath $config -Filter '*.tmp-*').Count -ne 0) { throw 'Temporary files leaked.' }
}
finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force } }
Write-Host 'PASS focused Web Forms linked-output safety'
