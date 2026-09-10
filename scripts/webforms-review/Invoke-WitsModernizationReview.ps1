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
function Assert-Properties($Value, [string[]]$Allowed, [string[]]$Required, [string]$Code) {
    if ($null -eq $Value -or $Value -is [Array] -or $Value -is [string]) { Stop-Review $Code }
    $present = @($Value.PSObject.Properties.Name)
    foreach ($name in $present) {
        if ($name -cnotin $Allowed) { Stop-Review $Code }
    }
    foreach ($name in $Required) {
        if ($name -cnotin $present) { Stop-Review $Code }
    }
}
function Require-String($Value, [int]$Maximum, [string]$Code) {
    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace($Value) -or $Value.Length -gt $Maximum) { Stop-Review $Code }
    return [string]$Value
}
function Get-SortedStrings($Values, [int]$Minimum, [int]$Maximum, [int]$MaximumLength, [string]$Code) {
    if ($Values -isnot [Array]) { Stop-Review $Code }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($value in $Values) {
        $text = Require-String $value $MaximumLength $Code
        if (!$seen.Add($text)) { Stop-Review $Code }
    }
    if ($seen.Count -lt $Minimum -or $seen.Count -gt $Maximum) { Stop-Review $Code }
    [string[]]$sorted = @($seen)
    [Array]::Sort($sorted, [StringComparer]::Ordinal)
    return $sorted
}
function Test-OrdinalSequence($Left, $Right) {
    $leftValues = @($Left)
    $rightValues = @($Right)
    if ($leftValues.Count -ne $rightValues.Count) { return $false }
    for ($index = 0; $index -lt $leftValues.Count; $index++) {
        if (![string]::Equals($leftValues[$index], $rightValues[$index], [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}
function Test-UtcTimestamp($Value) {
    $timestamp = [DateTimeOffset]::MinValue
    if ($Value -is [DateTime]) { return $Value.Kind -eq [DateTimeKind]::Utc }
    if ($Value -isnot [string] -or $Value -cnotmatch 'Z$') { return $false }
    return [DateTimeOffset]::TryParse($Value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind, [ref]$timestamp)
}

$inspectionFile = Get-Item -LiteralPath $InspectionPath -ErrorAction Stop
if (!$inspectionFile.PSIsContainer -and $inspectionFile.Length -le 32MB) {
    $inspectionBytes = [IO.File]::ReadAllBytes($inspectionFile.FullName)
} else { Stop-Review 'WITS_REVIEW_INSPECTION_UNAVAILABLE' }
$inspectionSha = ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inspectionBytes))).ToLowerInvariant()
$inspection = [Text.Encoding]::UTF8.GetString($inspectionBytes) | ConvertFrom-Json -Depth 64
if ($inspection.schemaVersion -cne 'webforms-batch-inspection.v1' -or @($inspection.cases).Count -lt 1 -or @($inspection.cases).Count -gt 64) {
    Stop-Review 'WITS_REVIEW_INSPECTION_INVALID'
}
[void](Require-String $inspection.scanId 256 'WITS_REVIEW_INSPECTION_INVALID')
$inspectionCommitSha = Require-String $inspection.commitSha 64 'WITS_REVIEW_INSPECTION_INVALID'
$inspectionRuleId = Require-String $inspection.ruleId 128 'WITS_REVIEW_INSPECTION_INVALID'
if ($inspectionCommitSha -cnotmatch '^[0-9a-fA-F]{7,64}$' -or $inspectionRuleId -cnotmatch '^[a-z0-9][a-z0-9.-]{0,127}$') {
    Stop-Review 'WITS_REVIEW_INSPECTION_INVALID'
}

$expected = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($case in @($inspection.cases)) {
    $caseId = Require-String $case.caseId 8 'WITS_REVIEW_INSPECTION_INVALID'
    if ($caseId -cnotmatch '^case-[0-9]{3}$' -or $expected.ContainsKey($caseId)) { Stop-Review 'WITS_REVIEW_INSPECTION_INVALID' }
    $handlerFactId = Require-String $case.handlerFactId 256 'WITS_REVIEW_INSPECTION_INVALID'
    $bindings = @($case.bindings)
    if ($bindings.Count -lt 1 -or $bindings.Count -gt 16) { Stop-Review 'WITS_REVIEW_INSPECTION_INVALID' }
    $bindingIdList = [Collections.Generic.List[string]]::new()
    $surfaceIdSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($binding in $bindings) {
        $bindingIdList.Add((Require-String $binding.bindingLocation.factId 256 'WITS_REVIEW_INSPECTION_INVALID'))
        if ($null -ne $binding.surfaceId) {
            [void]$surfaceIdSet.Add((Require-String $binding.surfaceId 1024 'WITS_REVIEW_INSPECTION_INVALID'))
        }
    }
    $bindingIds = @(Get-SortedStrings @($bindingIdList) 1 16 256 'WITS_REVIEW_INSPECTION_INVALID')
    [string[]]$surfaceIds = @($surfaceIdSet)
    [Array]::Sort($surfaceIds, [StringComparer]::Ordinal)
    $expected.Add($caseId, [pscustomobject]@{ HandlerFactId = $handlerFactId; BindingFactIds = $bindingIds; SurfaceIds = $surfaceIds })
}

$limitations = @('HUMAN_REVIEW_NOT_SCANNER_EVIDENCE', 'PARTIAL_COVERAGE_PRESERVED', 'STATIC_EVIDENCE_NOT_RUNTIME_EXECUTION')
$verdicts = @('unreviewed', 'expected-ui-only', 'supported-backend-present', 'backend-evidence-missing', 'binding-or-source-mismatch', 'needs-review')
$dispositions = @('unassigned', 'retain', 'replace-angular', 'replace-dotnet', 'replace-postgresql', 'replace-multiple', 'retire', 'manual-redesign', 'defer')
$correctionCategories = @('binding', 'callee', 'business-intent', 'coverage', 'other')

if ($Mode -ceq 'Export') {
    if (Test-Path -LiteralPath $ReviewPath) { Stop-Review 'WITS_REVIEW_OUTPUT_EXISTS' }
    [string[]]$caseIds = @($expected.Keys)
    [Array]::Sort($caseIds, [StringComparer]::Ordinal)
    $decisions = @($caseIds | ForEach-Object {
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
    $reviewFullPath = [IO.Path]::GetFullPath($ReviewPath)
    $reviewBytes = [Text.UTF8Encoding]::new($false).GetBytes(($overlay | ConvertTo-Json -Depth 12) + "`n")
    try {
        $stream = [IO.File]::Open($reviewFullPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    }
    catch [IO.IOException] {
        if (Test-Path -LiteralPath $reviewFullPath) { Stop-Review 'WITS_REVIEW_OUTPUT_EXISTS' }
        throw
    }
    try {
        $stream.Write($reviewBytes, 0, $reviewBytes.Length)
    }
    finally {
        $stream.Dispose()
    }
    Write-Host "witsModernizationReview=created;cases=$($decisions.Count);state=draft"
    return
}

$reviewFile = Get-Item -LiteralPath $ReviewPath -ErrorAction Stop
if ($reviewFile.PSIsContainer -or $reviewFile.Length -gt 4MB) { Stop-Review 'WITS_REVIEW_INPUT_LIMIT' }
$review = [IO.File]::ReadAllText($reviewFile.FullName) | ConvertFrom-Json -Depth 32
Assert-Properties $review @('schemaVersion','overlayId','evidence','reviewState','reviewer','reviewedAtUtc','decisions','limitations') @('schemaVersion','overlayId','evidence','reviewState','reviewer','reviewedAtUtc','decisions','limitations') 'WITS_REVIEW_UNKNOWN_FIELD'
if (![string]::Equals($review.schemaVersion, 'wits-modernization-review.v1', [StringComparison]::Ordinal) -or
    ![string]::Equals($review.overlayId, ('review-' + $inspectionSha.Substring(0, 20)), [StringComparison]::Ordinal)) { Stop-Review 'WITS_REVIEW_PROVENANCE_MISMATCH' }
Assert-Properties $review.evidence @('inspectionSchemaVersion','inspectionSha256','scanId','commitSha','ruleId') @('inspectionSchemaVersion','inspectionSha256','scanId','commitSha','ruleId') 'WITS_REVIEW_UNKNOWN_FIELD'
if (![string]::Equals($review.evidence.inspectionSchemaVersion, $inspection.schemaVersion, [StringComparison]::Ordinal) -or
    ![string]::Equals($review.evidence.inspectionSha256, $inspectionSha, [StringComparison]::Ordinal) -or
    ![string]::Equals($review.evidence.scanId, $inspection.scanId, [StringComparison]::Ordinal) -or
    ![string]::Equals($review.evidence.commitSha, $inspection.commitSha, [StringComparison]::Ordinal) -or
    ![string]::Equals($review.evidence.ruleId, $inspection.ruleId, [StringComparison]::Ordinal)) {
    Stop-Review 'WITS_REVIEW_PROVENANCE_MISMATCH'
}
if ($review.limitations -isnot [Array]) { Stop-Review 'WITS_REVIEW_LIMITATION_INVALID' }
$actualLimitations = @(Get-SortedStrings $review.limitations 3 3 64 'WITS_REVIEW_LIMITATION_INVALID')
if (!(Test-OrdinalSequence $actualLimitations $limitations)) {
    Stop-Review 'WITS_REVIEW_LIMITATION_INVALID'
}
if ($review.reviewState -cnotin @('draft','completed')) { Stop-Review 'WITS_REVIEW_STATE_INVALID' }
if ($review.decisions -isnot [Array]) { Stop-Review 'WITS_REVIEW_CASE_SET_MISMATCH' }
$decisions = @($review.decisions)
if ($decisions.Count -ne $expected.Count) { Stop-Review 'WITS_REVIEW_CASE_SET_MISMATCH' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($decision in $decisions) {
    Assert-Properties $decision @('caseId','surfaceIds','handlerFactId','bindingFactIds','verdict','comment','correction','migrationDisposition') @('caseId','surfaceIds','handlerFactId','bindingFactIds','verdict','comment','correction','migrationDisposition') 'WITS_REVIEW_UNKNOWN_FIELD'
    $caseId = Require-String $decision.caseId 8 'WITS_REVIEW_CASE_SET_MISMATCH'
    if (!$seen.Add($caseId) -or !$expected.ContainsKey($caseId)) { Stop-Review 'WITS_REVIEW_CASE_SET_MISMATCH' }
    $item = $expected[$caseId]
    $handlerFactId = Require-String $decision.handlerFactId 256 'WITS_REVIEW_REFERENCE_MISMATCH'
    $sortedDecisionBindingIds = @(Get-SortedStrings $decision.bindingFactIds 1 16 256 'WITS_REVIEW_REFERENCE_MISMATCH')
    $sortedDecisionSurfaceIds = @(Get-SortedStrings $decision.surfaceIds 0 16 1024 'WITS_REVIEW_REFERENCE_MISMATCH')
    if (![string]::Equals($handlerFactId, $item.HandlerFactId, [StringComparison]::Ordinal) -or
        !(Test-OrdinalSequence $sortedDecisionBindingIds $item.BindingFactIds) -or
        !(Test-OrdinalSequence $sortedDecisionSurfaceIds $item.SurfaceIds)) { Stop-Review 'WITS_REVIEW_REFERENCE_MISMATCH' }
    if ($decision.verdict -cnotin $verdicts -or $decision.migrationDisposition -cnotin $dispositions) { Stop-Review 'WITS_REVIEW_DECISION_INVALID' }
    if ($null -ne $decision.comment -and ($decision.comment -isnot [string] -or $decision.comment.Length -gt 4000)) { Stop-Review 'WITS_REVIEW_DECISION_INVALID' }
    if ($null -ne $decision.correction) {
        Assert-Properties $decision.correction @('category','statement') @('category','statement') 'WITS_REVIEW_UNKNOWN_FIELD'
        if ($decision.correction.category -cnotin $correctionCategories) { Stop-Review 'WITS_REVIEW_DECISION_INVALID' }
        [void](Require-String $decision.correction.statement 4000 'WITS_REVIEW_DECISION_INVALID')
    }
}
if ($review.reviewState -ceq 'completed') {
    [void](Require-String $review.reviewer 128 'WITS_REVIEW_COMPLETION_INVALID')
    if (!(Test-UtcTimestamp $review.reviewedAtUtc) -or
        @($decisions | Where-Object { $_.verdict -ceq 'unreviewed' }).Count -ne 0) { Stop-Review 'WITS_REVIEW_COMPLETION_INVALID' }
}
elseif (($null -eq $review.reviewer) -ne ($null -eq $review.reviewedAtUtc)) { Stop-Review 'WITS_REVIEW_COMPLETION_INVALID' }
elseif ($null -ne $review.reviewer) {
    [void](Require-String $review.reviewer 128 'WITS_REVIEW_COMPLETION_INVALID')
    if (!(Test-UtcTimestamp $review.reviewedAtUtc)) { Stop-Review 'WITS_REVIEW_COMPLETION_INVALID' }
}

Write-Host "witsModernizationReview=valid;cases=$($decisions.Count);state=$($review.reviewState)"
Write-Host 'nonClaim=human-review-is-not-scanner-evidence;validation-does-not-prove-runtime-or-migration-correctness'
