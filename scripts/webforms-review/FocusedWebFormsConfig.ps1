function Read-FocusedWebFormsConfig {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$ConfigPath)

    $file = Get-Item -LiteralPath $ConfigPath -ErrorAction SilentlyContinue
    if ($null -eq $file -or $file.PSIsContainer) {
        throw 'FocusedWebFormsConfigUnavailable; copy Run-FocusedWebFormsPageList.example.json to Run-FocusedWebFormsPageList.json and edit the local copy.'
    }
    if ($file.Length -gt 1MB) { throw 'FocusedWebFormsConfigLimitReached' }

    try { $config = [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json }
    catch { throw 'FocusedWebFormsConfigInvalidJson' }

    if ($null -eq $config -or $config -isnot [pscustomobject]) {
        throw 'FocusedWebFormsConfigPropertiesInvalid; expected only indexPath, outputRoot, and forms.'
    }

    $propertyNames = @($config.PSObject.Properties | ForEach-Object { $_.Name })
    $required = @('indexPath', 'outputRoot', 'forms')
    if ($propertyNames.Count -ne $required.Count -or
        @($required | Where-Object { $_ -notin $propertyNames }).Count -ne 0) {
        throw 'FocusedWebFormsConfigPropertiesInvalid; expected only indexPath, outputRoot, and forms.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$config.indexPath) -or
        [string]::IsNullOrWhiteSpace([string]$config.outputRoot)) {
        throw 'FocusedWebFormsConfigPathUnavailable'
    }
    if ($config.forms -is [string] -or $config.forms -isnot [System.Collections.IEnumerable]) {
        throw 'FocusedWebFormsConfigFormsInvalid'
    }

    $forms = @($config.forms | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
    if ($forms.Count -lt 1 -or $forms.Count -gt 10000 -or
        @($forms | Where-Object { -not $_.EndsWith('.aspx', [StringComparison]::OrdinalIgnoreCase) }).Count -ne 0) {
        throw 'FocusedWebFormsConfigFormsInvalid'
    }

    [pscustomobject]@{
        IndexPath = [string]$config.indexPath
        OutputRoot = [string]$config.outputRoot
        Forms = $forms
    }
}
