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
$indexPath = Join-Path $temp 'index.sqlite'
$evidenceDocsRoot = Join-Path $temp 'evidence-docs'
$expectedHandoffIndex = $indexPath
$expectedEvidenceDocsRoot = $evidenceDocsRoot
[IO.Directory]::CreateDirectory($source) | Out-Null
[IO.Directory]::CreateDirectory($inspectionDirectory) | Out-Null
[IO.Directory]::CreateDirectory($evidenceDocsRoot) | Out-Null
[IO.File]::WriteAllBytes($indexPath, [byte[]](1))
$inspectionPath = Join-Path $inspectionDirectory 'webforms-batch-inspection-test.json'
[IO.File]::WriteAllText($inspectionPath, (@{
    schemaVersion = 'webforms-batch-inspection.v1'
    scanId = 'scan-one'
    commitSha = 'commit-one'
    cases = @(
        @{ caseId = 'case-001'; handler = 'Private.FirstHandler()'; handlerLocation = @{ filePath = 'source/First.aspx.cs' }; bindings = @(@{ surfaceId = 'webforms-surface:hash-one'; bindingLocation = @{ filePath = 'source/First.aspx' } }) }
        @{ caseId = 'case-002'; handler = 'Private.SecondHandler()'; handlerLocation = @{ filePath = 'source/First.aspx.cs' }; bindings = @(@{ surfaceId = 'webforms-surface:hash-one'; bindingLocation = @{ filePath = 'source/First.aspx' } }) }
        @{ caseId = 'case-003'; handler = 'Private.ThirdHandler()'; handlerLocation = @{ filePath = 'source/Second.aspx.cs' }; bindings = @(@{ surfaceId = 'webforms-surface:hash-two'; bindingLocation = @{ filePath = 'source/Second.aspx' } }) }
    )
} | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))

function dotnet {
    if ($args[0] -eq 'build') { $global:LASTEXITCODE = 0; return }
    if ($args[1] -eq '--code-path-review-set-handoff') {
        if ([string]$args[4] -ne $expectedHandoffIndex -or
            ($expectedEvidenceDocsRoot -and [string]$args[5] -ne $expectedEvidenceDocsRoot)) { $global:LASTEXITCODE = 1; return }
        [IO.File]::WriteAllText((Join-Path ([string]$args[3]) 'agent-evidence-handoff.json'), '{"schemaVersion":"tracemap-agent-evidence-handoff.v1"}')
        $global:LASTEXITCODE = 0
        return
    }
    if ($args[1] -ne '--code-path-review') { $global:LASTEXITCODE = 1; return }
    if (!(Test-Path -LiteralPath ([string]$args[2]) -PathType Leaf) -or (Split-Path -Leaf ([string]$args[2])) -ne 'inspection.snapshot.json') { $global:LASTEXITCODE = 1; return }
    $privatePath = [string]$args[5]
    if ($args[7] -ne 'index.html') { $global:LASTEXITCODE = 1; return }
    if ($args[8] -ne '--include-raw-source') { $global:LASTEXITCODE = 1; return }
    [IO.File]::WriteAllText($privatePath, 'private')
    [IO.File]::WriteAllText(($privatePath -replace '\.private\.html$', '.shareable.html'), 'shareable')
    [IO.File]::WriteAllText(($privatePath -replace '\.private\.html$', '.shareable.json'), '{}')
    [IO.File]::WriteAllText(($privatePath -replace '\.private\.html$', '.handoff.json'), '{}')
    $global:LASTEXITCODE = 0
}

try {
    & $scriptPath -SourceRoot $source -InspectionPath $inspectionPath -OutputRoot $output -IndexPath $indexPath -EvidenceDocsRoot $evidenceDocsRoot -TriggerContextLines 50 -IncludeRawSource | Out-Null
    $sets = @(Get-ChildItem -LiteralPath $inspectionDirectory -Directory -Filter 'webforms-code-path-review-set-*')
    if ($sets.Count -ne 1) { throw 'Expected one review-set directory.' }
    $indexPath = Join-Path $sets[0].FullName 'index.md'
    if (!(Test-Path -LiteralPath $indexPath -PathType Leaf)) { throw 'Expected index.md in the review-set folder.' }
    if (!(Test-Path -LiteralPath (Join-Path $sets[0].FullName 'inspection.snapshot.json') -PathType Leaf)) { throw 'Expected the private inspection snapshot in the review-set folder.' }
    $htmlIndexPath = Join-Path $sets[0].FullName 'index.html'
    if (!(Test-Path -LiteralPath $htmlIndexPath -PathType Leaf)) { throw 'Expected index.html in the review-set folder.' }
    $htmlIndex = [IO.File]::ReadAllText($htmlIndexPath)
    foreach ($expected in @(
        'case-001.private.html',
        'case-002.shareable.html',
        'case-003.private.html',
        'Items: <code>2</code>. Handler cases: <code>3</code>.',
        'agent-evidence-handoff.json',
        'Agent evidence handoff',
        'target="_blank"'
    )) {
        if (!$htmlIndex.Contains($expected, [StringComparison]::Ordinal)) { throw "HTML index is missing: $expected" }
    }
    if ([regex]::Matches($htmlIndex, '<tr class="item">').Count -ne 2) { throw 'HTML index did not group cases by item.' }
    if ($htmlIndex.Contains('webforms-surface:', [StringComparison]::Ordinal)) { throw 'HTML index exposed the machine surface identity instead of the retained file path.' }
    if (!$htmlIndex.Contains('tbody tr:not(.item) td:first-child{white-space:nowrap}', [StringComparison]::Ordinal)) { throw 'HTML index allows evidence IDs to wrap.' }
    $queue = [IO.File]::ReadAllText($indexPath)
    foreach ($expected in @(
        '## Item: `source/First.aspx`',
        '## Item: `source/Second.aspx`',
        '| Evidence | Handler | Private path | Anonymous path | Human verdict | Comment |',
        '| case-001 | Private.FirstHandler() | [case-001.private.html](case-001.private.html)',
        '| case-002 | Private.SecondHandler() | [case-002.private.html](case-002.private.html)',
        '| case-003 | Private.ThirdHandler() | [case-003.private.html](case-003.private.html)',
        'Trigger context: `50` lines before and after each retained binding span.',
        '[`agent-evidence-handoff.json`](agent-evidence-handoff.json)',
        'A human verdict is review metadata, not scanner evidence.'
    )) {
        if (!$queue.Contains($expected, [StringComparison]::Ordinal)) { throw "Missing review queue text: $expected" }
    }
    if (@(Get-ChildItem -LiteralPath $sets[0].FullName -Filter '*.private.html').Count -ne 3) { throw 'Expected three private reports.' }
    if (@(Get-ChildItem -LiteralPath $sets[0].FullName -Filter 'case-*.handoff.json').Count -ne 3) { throw 'Expected three private case handoffs.' }

    $optionalOutput = Join-Path $temp 'optional-output'
    $optionalInspectionDirectory = Join-Path $optionalOutput 'local-inspection-private'
    [IO.Directory]::CreateDirectory($optionalInspectionDirectory) | Out-Null
    $optionalInspection = Join-Path $optionalInspectionDirectory 'webforms-batch-inspection-test.json'
    [IO.File]::Copy($inspectionPath, $optionalInspection)
    $configPath = Join-Path $temp 'review-config.json'
    [IO.File]::WriteAllText($configPath, (@{
        indexPath = (Join-Path $temp 'deleted-index.sqlite')
        outputRoot = $optionalOutput
        forms = @('source/First.aspx')
    } | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    $expectedHandoffIndex = '-'
    $expectedEvidenceDocsRoot = ''

    & $scriptPath -SourceRoot $source -InspectionPath $optionalInspection -ConfigPath $configPath -IncludeRawSource | Out-Null

    $optionalSets = @(Get-ChildItem -LiteralPath $optionalInspectionDirectory -Directory -Filter 'webforms-code-path-review-set-*')
    if ($optionalSets.Count -ne 1) { throw 'Expected review-set generation without the unavailable configured index.' }

    try {
        & $scriptPath -SourceRoot $source -InspectionPath $optionalInspection -OutputRoot $optionalOutput -IndexPath (Join-Path $temp 'explicitly-missing.sqlite') | Out-Null
        throw 'Expected an explicitly supplied unavailable index to fail.'
    }
    catch {
        if ($_.Exception.Message -ne 'CodePathReviewSetIndexUnavailable') { throw }
    }
    Write-Host 'PASS focused Web Forms code-path review set'
}
finally {
    Remove-Item Function:\dotnet -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
