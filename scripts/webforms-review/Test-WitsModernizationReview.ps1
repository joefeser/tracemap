$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'Invoke-WitsModernizationReview.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-wits-review-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
$inspectionPath = Join-Path $temp 'inspection.snapshot.json'
$privateSentinel = 'PRIVATE_SOURCE_PATH_SENTINEL'
$inspection = [ordered]@{
    schemaVersion = 'webforms-batch-inspection.v1'
    ruleId = 'diagnostic.webforms.raw-exact-call-evidence.v1'
    scanId = 'scan-test'
    commitSha = ('a' * 40)
    sourceReport = $privateSentinel
    cases = @(
        [ordered]@{ caseId='case-001'; handlerFactId='handler-1'; bindings=@([ordered]@{ surfaceId='surface-1'; bindingLocation=[ordered]@{ factId='binding-1' } }) },
        [ordered]@{ caseId='case-002'; handlerFactId='handler-2'; bindings=@([ordered]@{ surfaceId='surface-1'; bindingLocation=[ordered]@{ factId='binding-2' } }) }
    )
}
[IO.File]::WriteAllText($inspectionPath, ($inspection | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
$before = (Get-FileHash -LiteralPath $inspectionPath -Algorithm SHA256).Hash

function Expect-Failure([string]$Code, [scriptblock]$Action) {
    try { & $Action; throw "Expected failure $Code" }
    catch { if (!$_.Exception.Message.Contains($Code, [StringComparison]::Ordinal)) { throw } }
}
function Write-Variant($Value, [string]$Name) {
    $path = Join-Path $temp $Name
    [IO.File]::WriteAllText($path, ($Value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    return $path
}

try {
    $first = Join-Path $temp 'review-one.json'
    $second = Join-Path $temp 'review-two.json'
    $lowercaseMode = Join-Path $temp 'review-lowercase-mode.json'
    & $scriptPath -Mode Export -InspectionPath $inspectionPath -ReviewPath $first | Out-Null
    & $scriptPath -Mode Export -InspectionPath $inspectionPath -ReviewPath $second | Out-Null
    & $scriptPath -Mode export -InspectionPath $inspectionPath -ReviewPath $lowercaseMode | Out-Null
    if ((Get-FileHash $first).Hash -ne (Get-FileHash $second).Hash) { throw 'Review export is not deterministic.' }
    if ((Get-FileHash $first).Hash -ne (Get-FileHash $lowercaseMode).Hash) { throw 'Accepted mode casing changed export behavior.' }
    if ([IO.File]::ReadAllText($first).Contains($privateSentinel, [StringComparison]::Ordinal)) { throw 'Review export leaked unrelated private inspection content.' }
    & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath $first | Out-Null
    $existingHash = (Get-FileHash $first).Hash
    Expect-Failure 'WITS_REVIEW_OUTPUT_EXISTS' { & $scriptPath -Mode Export -InspectionPath $inspectionPath -ReviewPath $first }
    if ((Get-FileHash $first).Hash -ne $existingHash) { throw 'No-overwrite failure modified the existing review.' }

    $valid = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $valid.reviewState = 'completed'
    $valid.reviewer = 'reviewer-001'
    $valid.reviewedAtUtc = '2026-09-10T18:45:00Z'
    foreach ($decision in $valid.decisions) { $decision.verdict = 'needs-review'; $decision.migrationDisposition = 'defer' }
    $completed = Write-Variant $valid 'completed.json'
    & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath $completed | Out-Null
    & $scriptPath -Mode validate -InspectionPath $inspectionPath -ReviewPath $completed | Out-Null

    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].verdict = 'approved-by-magic'
    Expect-Failure 'WITS_REVIEW_DECISION_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'bad-code.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].verdict = 'NEEDS-REVIEW'
    Expect-Failure 'WITS_REVIEW_DECISION_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'case-changed-code.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.reviewState = $true
    Expect-Failure 'WITS_REVIEW_STATE_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'non-string-state.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].verdict = $true
    Expect-Failure 'WITS_REVIEW_DECISION_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'non-string-verdict.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].migrationDisposition = $true
    Expect-Failure 'WITS_REVIEW_DECISION_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'non-string-disposition.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].correction = [pscustomobject]@{ category = $true; statement = 'corrected statement' }
    Expect-Failure 'WITS_REVIEW_DECISION_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'non-string-correction-category.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[1].caseId = 'case-001'
    Expect-Failure 'WITS_REVIEW_CASE_SET_MISMATCH' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'duplicate.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.evidence.inspectionSha256 = ('0' * 64)
    Expect-Failure 'WITS_REVIEW_PROVENANCE_MISMATCH' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'provenance.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].handlerFactId = 'different-handler'
    Expect-Failure 'WITS_REVIEW_REFERENCE_MISMATCH' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'reference.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].handlerFactId = 'Handler-1'
    Expect-Failure 'WITS_REVIEW_REFERENCE_MISMATCH' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'case-changed-reference.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].bindingFactIds = @('binding-1', 'binding-1')
    Expect-Failure 'WITS_REVIEW_REFERENCE_MISMATCH' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'duplicate-reference.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].bindingFactIds = 'binding-1'
    Expect-Failure 'WITS_REVIEW_REFERENCE_MISMATCH' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'scalar-reference.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].bindingFactIds = @(1)
    Expect-Failure 'WITS_REVIEW_REFERENCE_MISMATCH' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'numeric-reference.json') }
    $bad = [IO.File]::ReadAllText($completed) | ConvertFrom-Json -Depth 32
    $bad.reviewer = $null
    Expect-Failure 'WITS_REVIEW_COMPLETION_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'completion.json') }
    $bad = [IO.File]::ReadAllText($completed) | ConvertFrom-Json -Depth 32
    $bad.reviewer = '   '
    Expect-Failure 'WITS_REVIEW_COMPLETION_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'whitespace-reviewer.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.reviewer = 'reviewer-001'
    $bad.reviewedAtUtc = 'not-a-timestamp'
    Expect-Failure 'WITS_REVIEW_COMPLETION_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'draft-metadata.json') }
    $bad = [IO.File]::ReadAllText($completed) | ConvertFrom-Json -Depth 32
    $bad.reviewedAtUtc = '2026-09-10T20:45:00+02:00'
    Expect-Failure 'WITS_REVIEW_COMPLETION_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'non-utc-metadata.json') }
    $bad = [IO.File]::ReadAllText($completed) | ConvertFrom-Json -Depth 32
    $bad.reviewedAtUtc = '09/10/2026 18:45:00Z'
    Expect-Failure 'WITS_REVIEW_COMPLETION_INVALID' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'non-rfc3339-metadata.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.PSObject.Properties.Remove('reviewer')
    Expect-Failure 'WITS_REVIEW_UNKNOWN_FIELD' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'missing-root-field.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad.decisions[0].PSObject.Properties.Remove('comment')
    Expect-Failure 'WITS_REVIEW_UNKNOWN_FIELD' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'missing-decision-field.json') }
    $bad = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 32
    $bad | Add-Member -NotePropertyName unexpected -NotePropertyValue $true
    Expect-Failure 'WITS_REVIEW_UNKNOWN_FIELD' { & $scriptPath -Mode Validate -InspectionPath $inspectionPath -ReviewPath (Write-Variant $bad 'unknown.json') }

    $schemaPath = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'docs/contracts/wits-modernization-review.v1.schema.json'
    $schema = [IO.File]::ReadAllText($schemaPath) | ConvertFrom-Json -Depth 32
    $reviewerPatterns = @(
        $schema.properties.reviewer.pattern,
        $schema.allOf[0].oneOf[1].properties.reviewer.pattern,
        $schema.allOf[1].then.properties.reviewer.pattern
    )
    if (@($reviewerPatterns | Where-Object { $_ -cne '\S' }).Count -ne 0) { throw 'Reviewer schemas do not consistently reject whitespace-only values.' }

    if ((Get-FileHash -LiteralPath $inspectionPath -Algorithm SHA256).Hash -ne $before) { throw 'Review workflow modified the inspection.' }
    Write-Host 'PASS WITS modernization review overlay'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
