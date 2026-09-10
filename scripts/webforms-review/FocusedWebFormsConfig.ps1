function Read-FocusedWebFormsConfig {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$ConfigPath)

    try {
        $stream = [IO.File]::Open(
            $ConfigPath,
            [IO.FileMode]::Open,
            [IO.FileAccess]::Read,
            [IO.FileShare]::Read)
    }
    catch {
        throw 'FocusedWebFormsConfigUnavailable; copy Run-FocusedWebFormsPageList.example.json to Run-FocusedWebFormsPageList.json and edit the local copy.'
    }

    try {
        $maximumBytes = 1MB
        $buffer = [byte[]]::new($maximumBytes + 1)
        $bytesRead = 0
        while ($bytesRead -lt $buffer.Length) {
            $read = $stream.Read($buffer, $bytesRead, $buffer.Length - $bytesRead)
            if ($read -eq 0) { break }
            $bytesRead += $read
        }
        if ($bytesRead -gt $maximumBytes) { throw 'FocusedWebFormsConfigLimitReached' }
    }
    finally {
        $stream.Dispose()
    }

    try {
        $json = [Text.UTF8Encoding]::new($false, $true).GetString($buffer, 0, $bytesRead)
        $config = $json | ConvertFrom-Json
    }
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
    if ($config.indexPath -isnot [string] -or
        $config.outputRoot -isnot [string] -or
        [string]::IsNullOrWhiteSpace($config.indexPath) -or
        [string]::IsNullOrWhiteSpace($config.outputRoot)) {
        throw 'FocusedWebFormsConfigPathUnavailable'
    }
    if ($config.forms -isnot [array]) {
        throw 'FocusedWebFormsConfigFormsInvalid'
    }

    if ($config.forms.Count -lt 1 -or $config.forms.Count -gt 10000) {
        throw 'FocusedWebFormsConfigFormsInvalid'
    }
    $forms = [Collections.Generic.List[string]]::new($config.forms.Count)
    foreach ($form in $config.forms) {
        if ($form -isnot [string] -or [string]::IsNullOrWhiteSpace($form) -or
            $form.Contains("`r", [StringComparison]::Ordinal) -or
            $form.Contains("`n", [StringComparison]::Ordinal)) {
            throw 'FocusedWebFormsConfigFormsInvalid'
        }
        $normalized = $form.Trim()
        if (!$normalized.EndsWith('.aspx', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'FocusedWebFormsConfigFormsInvalid'
        }
        $forms.Add($normalized)
    }

    [pscustomobject]@{
        IndexPath = $config.indexPath.Trim()
        OutputRoot = $config.outputRoot.Trim()
        Forms = $forms.ToArray()
    }
}
