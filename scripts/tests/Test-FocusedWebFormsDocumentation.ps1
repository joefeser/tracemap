$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$repo = Split-Path -Parent $scripts
$readmePath = Join-Path $scripts 'webforms-review/README.md'
$readme = [IO.File]::ReadAllText($readmePath)

$operatorScripts = @(Get-ChildItem -LiteralPath $scripts -File -Filter '*.ps1' |
    Where-Object {
        $_.Name -notlike '*.Tests.ps1' -and
        $_.Name -match '(?:FocusedWebForms|CompletedWebForms)'
    } |
    Sort-Object Name)

foreach ($script in $operatorScripts) {
    if (!$readme.Contains(('`{0}`' -f $script.Name), [StringComparison]::Ordinal)) {
        throw "WEBFORMS_DOCUMENTATION_SCRIPT_MAP_INCOMPLETE;script=$($script.Name)"
    }
}

$documentationPaths = @(
    Get-ChildItem -LiteralPath (Join-Path $repo 'docs') -File -Filter 'WEBFORMS*.md'
    Get-Item -LiteralPath $readmePath
    Get-Item -LiteralPath (Join-Path $scripts 'webforms-review/VBNET_BATTLE_TEST.md')
)
$scriptReferencePattern = '(?<![A-Za-z0-9_.*-])(?<name>[A-Za-z][A-Za-z0-9_-]+\.ps1)'
foreach ($documentation in $documentationPaths) {
    $content = [IO.File]::ReadAllText($documentation.FullName)
    foreach ($match in [regex]::Matches($content, $scriptReferencePattern)) {
        $name = $match.Groups['name'].Value
        $candidates = @(
            (Join-Path $scripts $name),
            (Join-Path $scripts "tests/$name"),
            (Join-Path $scripts "webforms-review/$name")
        )
        if (!($candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1)) {
            throw "WEBFORMS_DOCUMENTATION_SCRIPT_REFERENCE_UNAVAILABLE;document=$($documentation.Name);script=$name"
        }
    }
}

Write-Output 'PASS focused Web Forms documentation inventory'
