$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $scripts 'New-FocusedWebFormsCodePathReviewSet.ps1'
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw 'Code-path review set script syntax is invalid.' }

$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-code-path-set-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $temp 'source'
$output = Join-Path $temp 'output'
$inspectionDirectory = Join-Path $output 'local-inspection-private'
[IO.Directory]::CreateDirectory($source) | Out-Null
[IO.Directory]::CreateDirectory($inspectionDirectory) | Out-Null
$inspectionPath = Join-Path $inspectionDirectory 'webforms-batch-inspection-test.json'
[IO.File]::WriteAllText($inspectionPath, (@{
    schemaVersion = 'webforms-batch-inspection.v1'
    cases = @(@{ caseId = 'case-001' }, @{ caseId = 'case-002' })
} | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))

function dotnet {
    if ($args[0] -eq 'build') { $global:LASTEXITCODE = 0; return }
    if ($args[1] -ne '--code-path-review') { $global:LASTEXITCODE = 1; return }
    $privatePath = [string]$args[5]
    [IO.File]::WriteAllText($privatePath, 'private')
    [IO.File]::WriteAllText(($privatePath -replace '\.private\.html$', '.shareable.html'), 'shareable')
    [IO.File]::WriteAllText(($privatePath -replace '\.private\.html$', '.shareable.json'), '{}')
    $global:LASTEXITCODE = 0
}

try {
    & $scriptPath -SourceRoot $source -InspectionPath $inspectionPath -OutputRoot $output -TriggerContextLines 50 | Out-Null
    $sets = @(Get-ChildItem -LiteralPath $inspectionDirectory -Directory -Filter 'webforms-code-path-review-set-*')
    if ($sets.Count -ne 1) { throw 'Expected one review-set directory.' }
    $queue = [IO.File]::ReadAllText((Join-Path $sets[0].FullName 'review-queue.md'))
    foreach ($expected in @(
        '| Evidence | Private path | Anonymous path | Human verdict | Comment |',
        '| case-001 | [case-001.private.html](case-001.private.html)',
        '| case-002 | [case-002.private.html](case-002.private.html)',
        'Trigger context: `50` lines before and after each retained binding span.',
        'A human verdict is review metadata, not scanner evidence.'
    )) {
        if (!$queue.Contains($expected, [StringComparison]::Ordinal)) { throw "Missing review queue text: $expected" }
    }
    if (@(Get-ChildItem -LiteralPath $sets[0].FullName -Filter '*.private.html').Count -ne 2) { throw 'Expected two private reports.' }
    Write-Host 'PASS focused Web Forms code-path review set'
}
finally {
    Remove-Item Function:\dotnet -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
