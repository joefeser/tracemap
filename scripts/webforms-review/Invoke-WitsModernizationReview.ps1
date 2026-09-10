[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Export', 'Validate')]
    [string]$Mode,
    [Parameter(Mandatory = $true)]
    [string]$InspectionPath,
    [Parameter(Mandatory = $true)]
    [string]$ReviewPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Stop-Review([string]$Code) { throw [IO.InvalidDataException]::new($Code) }
function Assert-Properties($Value, [string[]]$Allowed, [string]$Code) {
    foreach ($name in $Value.PSObject.Properties.Name) {
        if ($name -notin $Allowed) { Stop-Review $Code }
    }
}
function Require-String($Value, [int]$Maximum, [string]$Code) {
    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace($Value) -or $Value.Length -gt $Maximum) { Stop-Review $Code }
    return [string]$Value
}
function Sorted-Strings($Values) { return @($Values | ForEach-Object { [string]$_ } | Sort-Object -Unique) }
function Test-UtcTimestamp($Value) {
    $timestamp = [DateTimeOffset]::MinValue
    if ($Value -is [DateTime]) { return $Value.Kind -eq [DateTimeKind]::Utc }
    if ($Value -isnot [string] -or $Value -notmatch 'Z$') { return $false }
    return [DateTimeOffset]::TryParse($Value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind, [ref]$timestamp)
}

$inspectionFile = Get-Item -LiteralPath $InspectionPath -ErrorAction Stop
if (!$inspectionFile.PSIsContainer -and $inspectionFile.Length -le 32MB) {
    $inspectionBytes = [IO.File]::ReadAllBytes($inspectionFile.FullName)
} else { Stop-Review 'WITS_REVIEW_INSPECTION_UNAVAILABLE' }
$inspectionSha = ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inspectionBytes))).ToLowerInvariant()
$inspection = [Text.Encoding]::UTF8.GetString($inspectionBytes) | ConvertFrom-Json -Depth 64
if ($inspection.schemaVersion -ne 'webforms-batch-inspection.v1' -or @($inspection.cases).Count -lt 1 -or @($inspection.cases).Count -gt 64) {
    Stop-Review 'WITS_REVIEW_INSPECTION_INVALID'
}
[void](Require-String $inspection.scanId 256 'WITS_REVIEW_INSPECTION_INVALID')
$inspectionCommitSha = Require-String $inspection.commitSha 64 'WITS_REVIEW_INSPECTION_INVALID'
$inspectionRuleId = Require-String $inspection.ruleId 128 'WITS_REVIEW_INSPECTION_INVALID'
if ($inspectionCommitSha -notmatch '^[0-9a-fA-F]{7,64}$' -or $inspectionRuleId -notmatch '^[a-z0-9][a-z0-9.-]{0,127}$') {
    Stop-Review 'WITS_REVIEW_INSPECTION_INVALID'
}

$expected = [ordered]@{}
foreach ($case in @($inspection.cases)) {
    $caseId = Require-String $case.caseId 8 'WITS_REVIEW_INSPECTION_INVALID'
    if ($caseId -notmatch '^case-[0-9]{3}$' -or $expected.Contains($caseId)) { Stop-Review 'WITS_REVIEW_INSPECTION_INVALID' }
    $handlerFactId = Require-String $case.handlerFactId 256 'WITS_REVIEW_INSPECTION_INVALID'
    $bindings = @($case.bindings)
    $bindingIds = @(Sorted-Strings ($bindings | ForEach-Object { $_.bindingLocation.factId }))
    if ($bindingIds.Count -lt 1 -or $bindingIds.Count -gt 16 -or @($bindingIds | Where-Object { [string]::IsNullOrWhiteSpace($_) -or $_.Length -gt 256 }).Count -ne 0) {
        Stop-Review 'WITS_REVIEW_INSPECTION_INVALID'
    }
    $surfaceIds = @(Sorted-Strings ($bindings | ForEach-Object { $_.surfaceId } | Where-Object { $_ }))
    if ($surfaceIds.Count -gt 16 -or @($surfaceIds | Where-Object { $_.Length -gt 1024 }).Count -ne 0) { Stop-Review 'WITS_REVIEW_INSPECTION_INVALID' }
    $expected[$caseId] = [pscustomobject]@{ HandlerFactId = $handlerFactId; BindingFactIds = $bindingIds; SurfaceIds = $surfaceIds }
}

$limitations = @('HUMAN_REVIEW_NOT_SCANNER_EVIDENCE', 'PARTIAL_COVERAGE_PRESERVED', 'STATIC_EVIDENCE_NOT_RUNTIME_EXECUTION')
$verdicts = @('unreviewed', 'expected-ui-only', 'supported-backend-present', 'backend-evidence-missing', 'binding-or-source-mismatch', 'needs-review')
$dispositions = @('unassigned', 'retain', 'replace-angular', 'replace-dotnet', 'replace-postgresql', 'replace-multiple', 'retire', 'manual-redesign', 'defer')
$correctionCategories = @('binding', 'callee', 'business-intent', 'coverage', 'other')

if ($Mode -eq 'Export') {
    if (Test-Path -LiteralPath $ReviewPath) { Stop-Review 'WITS_REVIEW_OUTPUT_EXISTS' }
    $decisions = @($expected.Keys | Sort-Object | ForEach-Object {
        $item = $expected[$_]
        [ordered]@{
            caseId = $_
            surfaceIds = @($item.SurfaceIds)
            handlerFactId = $item.HandlerFactId
            bindingFactIds = @($item.BindingFactIds)
            verdict = 'unreviewed'
            comment = $null
            correction = $null
            migrationDisposition = 'unassigned'
        }
    })
    $overlay = [ordered]@{
        schemaVersion = 'wits-modernization-review.v1'
        overlayId = 'review-' + $inspectionSha.Substring(0, 20)
        evidence = [ordered]@{
            inspectionSchemaVersion = [string]$inspection.schemaVersion
            inspectionSha256 = $inspectionSha
            scanId = [string]$inspection.scanId
            commitSha = [string]$inspection.commitSha
            ruleId = [string]$inspection.ruleId
        }
        reviewState = 'draft'
        reviewer = $null
        reviewedAtUtc = $null
        decisions = $decisions
        limitations = $limitations
    }
    $parent = Split-Path -Parent ([IO.Path]::GetFullPath($ReviewPath))
    if (!(Test-Path -LiteralPath $parent -PathType Container)) { Stop-Review 'WITS_REVIEW_OUTPUT_DIRECTORY_UNAVAILABLE' }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($ReviewPath), ($overlay | ConvertTo-Json -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "witsModernizationReview=created;cases=$($decisions.Count);state=draft"
    return
}

$reviewFile = Get-Item -LiteralPath $ReviewPath -ErrorAction Stop
if ($reviewFile.PSIsContainer -or $reviewFile.Length -gt 4MB) { Stop-Review 'WITS_REVIEW_INPUT_LIMIT' }
$review = [IO.File]::ReadAllText($reviewFile.FullName) | ConvertFrom-Json -Depth 32
Assert-Properties $review @('schemaVersion','overlayId','evidence','reviewState','reviewer','reviewedAtUtc','decisions','limitations') 'WITS_REVIEW_UNKNOWN_FIELD'
if ($review.schemaVersion -ne 'wits-modernization-review.v1' -or $review.overlayId -ne ('review-' + $inspectionSha.Substring(0, 20))) { Stop-Review 'WITS_REVIEW_PROVENANCE_MISMATCH' }
Assert-Properties $review.evidence @('inspectionSchemaVersion','inspectionSha256','scanId','commitSha','ruleId') 'WITS_REVIEW_UNKNOWN_FIELD'
if ($review.evidence.inspectionSchemaVersion -ne $inspection.schemaVersion -or $review.evidence.inspectionSha256 -ne $inspectionSha -or
    $review.evidence.scanId -ne $inspection.scanId -or $review.evidence.commitSha -ne $inspection.commitSha -or $review.evidence.ruleId -ne $inspection.ruleId) {
    Stop-Review 'WITS_REVIEW_PROVENANCE_MISMATCH'
}
$reviewLimitations = @($review.limitations)
if ($review.limitations -isnot [Array]) { Stop-Review 'WITS_REVIEW_LIMITATION_INVALID' }
$actualLimitations = @(Sorted-Strings @($review.limitations))
if ($actualLimitations.Count -ne $reviewLimitations.Count -or ($actualLimitations -join "`n") -ne ($limitations -join "`n")) {
    Stop-Review 'WITS_REVIEW_LIMITATION_INVALID'
}
if ($review.reviewState -notin @('draft','completed')) { Stop-Review 'WITS_REVIEW_STATE_INVALID' }
if ($review.decisions -isnot [Array]) { Stop-Review 'WITS_REVIEW_CASE_SET_MISMATCH' }
$decisions = @($review.decisions)
if ($decisions.Count -ne $expected.Count) { Stop-Review 'WITS_REVIEW_CASE_SET_MISMATCH' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($decision in $decisions) {
    Assert-Properties $decision @('caseId','surfaceIds','handlerFactId','bindingFactIds','verdict','comment','correction','migrationDisposition') 'WITS_REVIEW_UNKNOWN_FIELD'
    $caseId = Require-String $decision.caseId 8 'WITS_REVIEW_CASE_SET_MISMATCH'
    if (!$seen.Add($caseId) -or !$expected.Contains($caseId)) { Stop-Review 'WITS_REVIEW_CASE_SET_MISMATCH' }
    $item = $expected[$caseId]
    if ($decision.bindingFactIds -isnot [Array] -or $decision.surfaceIds -isnot [Array]) { Stop-Review 'WITS_REVIEW_REFERENCE_MISMATCH' }
    $decisionBindingIds = @($decision.bindingFactIds)
    $decisionSurfaceIds = @($decision.surfaceIds)
    $sortedDecisionBindingIds = @(Sorted-Strings $decisionBindingIds)
    $sortedDecisionSurfaceIds = @(Sorted-Strings $decisionSurfaceIds)
    if ($decision.handlerFactId -ne $item.HandlerFactId -or
        $sortedDecisionBindingIds.Count -ne $decisionBindingIds.Count -or
        $sortedDecisionSurfaceIds.Count -ne $decisionSurfaceIds.Count -or
        ($sortedDecisionBindingIds -join "`n") -ne (@($item.BindingFactIds) -join "`n") -or
        ($sortedDecisionSurfaceIds -join "`n") -ne (@($item.SurfaceIds) -join "`n")) { Stop-Review 'WITS_REVIEW_REFERENCE_MISMATCH' }
    if ($decision.verdict -notin $verdicts -or $decision.migrationDisposition -notin $dispositions) { Stop-Review 'WITS_REVIEW_DECISION_INVALID' }
    if ($null -ne $decision.comment -and ($decision.comment -isnot [string] -or $decision.comment.Length -gt 4000)) { Stop-Review 'WITS_REVIEW_DECISION_INVALID' }
    if ($null -ne $decision.correction) {
        Assert-Properties $decision.correction @('category','statement') 'WITS_REVIEW_UNKNOWN_FIELD'
        if ($decision.correction.category -notin $correctionCategories) { Stop-Review 'WITS_REVIEW_DECISION_INVALID' }
        [void](Require-String $decision.correction.statement 4000 'WITS_REVIEW_DECISION_INVALID')
    }
}
if ($review.reviewState -eq 'completed') {
    [void](Require-String $review.reviewer 128 'WITS_REVIEW_COMPLETION_INVALID')
    if (!(Test-UtcTimestamp $review.reviewedAtUtc) -or
        @($decisions | Where-Object { $_.verdict -eq 'unreviewed' }).Count -ne 0) { Stop-Review 'WITS_REVIEW_COMPLETION_INVALID' }
}
elseif (($null -eq $review.reviewer) -ne ($null -eq $review.reviewedAtUtc)) { Stop-Review 'WITS_REVIEW_COMPLETION_INVALID' }
elseif ($null -ne $review.reviewer) {
    [void](Require-String $review.reviewer 128 'WITS_REVIEW_COMPLETION_INVALID')
    if (!(Test-UtcTimestamp $review.reviewedAtUtc)) { Stop-Review 'WITS_REVIEW_COMPLETION_INVALID' }
}

Write-Host "witsModernizationReview=valid;cases=$($decisions.Count);state=$($review.reviewState)"
Write-Host 'nonClaim=human-review-is-not-scanner-evidence;validation-does-not-prove-runtime-or-migration-correctness'
