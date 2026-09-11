[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Export', 'Validate')]
    [string]$Mode,
    [Parameter(Mandatory = $true)]
    [string]$PacketPath,
    [Parameter(Mandatory = $true)]
    [string]$ReviewPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Stop-ApplicationReview([string]$Code) { throw [IO.InvalidDataException]::new($Code) }
function Require-Text($Value, [int]$Maximum, [string]$Code) {
    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace($Value) -or $Value.Length -gt $Maximum) { Stop-ApplicationReview $Code }
    [string]$Value
}
function Assert-Fields($Value, [string[]]$Allowed, [string[]]$Required) {
    if ($null -eq $Value -or $Value -is [Array] -or $Value -is [string]) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_UNKNOWN_FIELD' }
    $present = @($Value.PSObject.Properties.Name)
    if (@($present | Where-Object { $_ -cnotin $Allowed }).Count -ne 0 -or @($Required | Where-Object { $_ -cnotin $present }).Count -ne 0) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_UNKNOWN_FIELD' }
}
function Test-Utc($Value) {
    if ($Value -is [DateTime]) { return $Value.Kind -eq [DateTimeKind]::Utc }
    if ($Value -is [DateTimeOffset]) { return $Value.Offset -eq [TimeSpan]::Zero }
    if ($Value -isnot [string] -or $Value -cnotmatch '^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\.[0-9]+)?Z$') { return $false }
    $parsed = [DateTimeOffset]::MinValue
    [DateTimeOffset]::TryParse($Value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind, [ref]$parsed)
}

$packetFile = Get-Item -LiteralPath $PacketPath -ErrorAction Stop
if ($packetFile.PSIsContainer -or $packetFile.Length -le 0 -or $packetFile.Length -gt 128MB) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_PACKET_UNAVAILABLE' }
$scriptsRoot = Split-Path -Parent $PSScriptRoot
$validatorProject = Join-Path $scriptsRoot 'diagnostics/RawWebFormsEvidence/RawWebFormsEvidence.csproj'
& dotnet build $validatorProject -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_PACKET_INVALID' }
$validatorDll = Join-Path $scriptsRoot 'diagnostics/RawWebFormsEvidence/bin/Release/net10.0/RawWebFormsEvidence.dll'
& dotnet $validatorDll '--validate-application-workbench-inputs' $packetFile.FullName '-'
if ($LASTEXITCODE -ne 0) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_PACKET_INVALID' }
$bytes = [IO.File]::ReadAllBytes($packetFile.FullName)
$packetSha = ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))).ToLowerInvariant()
$packet = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -Depth 100
if ($packet.schemaVersion -cne 'webforms-modernization-packet.v1' -or @($packet.sources).Count -ne 1 -or @($packet.surfaces).Count -lt 1 -or @($packet.surfaces).Count -gt 1000) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_PACKET_INVALID' }
$source = @($packet.sources)[0]
foreach ($value in @($packet.packetId, $packet.ruleId, $source.scanId, $source.commitSha)) { [void](Require-Text $value 1024 'WITS_APPLICATION_REVIEW_PACKET_INVALID') }
$expected = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
$ordinal = 0
foreach ($surface in @($packet.surfaces | Sort-Object @{ Expression = { [string]$_.evidence.filePath } }, @{ Expression = { [string]$_.surfaceId } })) {
    $ordinal++
    $surfaceId = Require-Text $surface.surfaceId 1024 'WITS_APPLICATION_REVIEW_PACKET_INVALID'
    $factId = Require-Text $surface.evidence.factId 256 'WITS_APPLICATION_REVIEW_PACKET_INVALID'
    if ($expected.ContainsKey($surfaceId)) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_PACKET_INVALID' }
    $expected.Add($surfaceId, [pscustomobject]@{ PageId = ('page-{0:d3}' -f $ordinal); FactId = $factId })
}
$verdicts = @('unreviewed', 'expected-ui-only', 'supported-backend-present', 'backend-evidence-missing', 'binding-or-source-mismatch', 'needs-review')
$dispositions = @('unassigned', 'retain', 'replace-angular', 'replace-dotnet', 'replace-postgresql', 'replace-multiple', 'retire', 'manual-redesign', 'defer')
$correctionCategories = @('binding', 'callee', 'business-intent', 'coverage', 'other')
$limitations = @('HUMAN_REVIEW_NOT_SCANNER_EVIDENCE', 'PARTIAL_COVERAGE_PRESERVED', 'STATIC_EVIDENCE_NOT_RUNTIME_EXECUTION')

if ($Mode -ieq 'Export') {
    if (Test-Path -LiteralPath $ReviewPath) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_OUTPUT_EXISTS' }
    $decisions = @($expected.GetEnumerator() | Sort-Object { $_.Value.PageId } | ForEach-Object {
        [ordered]@{ pageId = $_.Value.PageId; surfaceId = $_.Key; surfaceFactId = $_.Value.FactId; verdict = 'unreviewed'; migrationDisposition = 'unassigned'; capabilityLabel = $null; comment = $null; correction = $null }
    })
    $overlay = [ordered]@{
        schemaVersion = 'wits-webforms-application-review.v1'; overlayId = 'application-review-' + $packetSha.Substring(0, 20)
        evidence = [ordered]@{ packetSchemaVersion = [string]$packet.schemaVersion; packetSha256 = $packetSha; packetId = [string]$packet.packetId; scanId = [string]$source.scanId; commitSha = [string]$source.commitSha; ruleId = [string]$packet.ruleId }
        reviewState = 'draft'; reviewer = $null; reviewedAtUtc = $null; decisions = $decisions; limitations = $limitations
    }
    $parent = Split-Path -Parent ([IO.Path]::GetFullPath($ReviewPath))
    if (!(Test-Path -LiteralPath $parent -PathType Container)) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_OUTPUT_DIRECTORY_UNAVAILABLE' }
    $reviewBytes = [Text.UTF8Encoding]::new($false).GetBytes((($overlay | ConvertTo-Json -Depth 20) + "`n"))
    try { $stream = [IO.File]::Open([IO.Path]::GetFullPath($ReviewPath), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None) }
    catch [IO.IOException] { if (Test-Path -LiteralPath $ReviewPath) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_OUTPUT_EXISTS' }; throw }
    try { $stream.Write($reviewBytes, 0, $reviewBytes.Length) } finally { $stream.Dispose() }
    Write-Host "witsApplicationReview=created;pages=$($decisions.Count);state=draft"
    Write-Host 'editingGuide=scripts/webforms-review/WITS_APPLICATION_REVIEW_EDITING.md;one-verdict-and-one-disposition-per-page'
    return
}

$reviewFile = Get-Item -LiteralPath $ReviewPath -ErrorAction Stop
if ($reviewFile.PSIsContainer -or $reviewFile.Length -le 0 -or $reviewFile.Length -gt 8MB) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_INPUT_LIMIT' }
$review = [IO.File]::ReadAllText($reviewFile.FullName) | ConvertFrom-Json -Depth 40
Assert-Fields $review @('schemaVersion','overlayId','evidence','reviewState','reviewer','reviewedAtUtc','decisions','limitations') @('schemaVersion','overlayId','evidence','reviewState','reviewer','reviewedAtUtc','decisions','limitations')
Assert-Fields $review.evidence @('packetSchemaVersion','packetSha256','packetId','scanId','commitSha','ruleId') @('packetSchemaVersion','packetSha256','packetId','scanId','commitSha','ruleId')
if ($review.schemaVersion -cne 'wits-webforms-application-review.v1' -or $review.overlayId -cne ('application-review-' + $packetSha.Substring(0, 20)) -or
    $review.evidence.packetSchemaVersion -cne $packet.schemaVersion -or $review.evidence.packetSha256 -cne $packetSha -or $review.evidence.packetId -cne $packet.packetId -or
    $review.evidence.scanId -cne $source.scanId -or $review.evidence.commitSha -cne $source.commitSha -or $review.evidence.ruleId -cne $packet.ruleId) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_PROVENANCE_MISMATCH' }
if ($review.reviewState -cnotin @('draft','completed') -or $review.limitations -isnot [Array] -or (@($review.limitations | Sort-Object) -join '|') -cne (@($limitations | Sort-Object) -join '|')) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_STATE_INVALID' }
if ($review.decisions -isnot [Array] -or @($review.decisions).Count -ne $expected.Count) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_PAGE_SET_MISMATCH' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($decision in @($review.decisions)) {
    Assert-Fields $decision @('pageId','surfaceId','surfaceFactId','verdict','migrationDisposition','capabilityLabel','comment','correction') @('pageId','surfaceId','surfaceFactId','verdict','migrationDisposition','capabilityLabel','comment','correction')
    $surfaceId = Require-Text $decision.surfaceId 1024 'WITS_APPLICATION_REVIEW_PAGE_SET_MISMATCH'
    if (!$seen.Add($surfaceId) -or !$expected.ContainsKey($surfaceId) -or $decision.pageId -cne $expected[$surfaceId].PageId -or $decision.surfaceFactId -cne $expected[$surfaceId].FactId) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_REFERENCE_MISMATCH' }
    if ($decision.verdict -isnot [string] -or $decision.verdict -cnotin $verdicts -or $decision.migrationDisposition -isnot [string] -or $decision.migrationDisposition -cnotin $dispositions) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_DECISION_INVALID' }
    foreach ($field in @('capabilityLabel','comment')) { if ($null -ne $decision.$field -and ($decision.$field -isnot [string] -or $decision.$field.Length -gt 4000)) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_DECISION_INVALID' } }
    if ($null -ne $decision.correction) {
        Assert-Fields $decision.correction @('category','statement') @('category','statement')
        if ($decision.correction.category -isnot [string] -or $decision.correction.category -cnotin $correctionCategories) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_DECISION_INVALID' }
        [void](Require-Text $decision.correction.statement 4000 'WITS_APPLICATION_REVIEW_DECISION_INVALID')
    }
}
if ($review.reviewState -ceq 'completed') {
    [void](Require-Text $review.reviewer 128 'WITS_APPLICATION_REVIEW_COMPLETION_INVALID')
    if (!(Test-Utc $review.reviewedAtUtc) -or @($review.decisions | Where-Object { $_.verdict -ceq 'unreviewed' }).Count -ne 0) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_COMPLETION_INVALID' }
} elseif (($null -eq $review.reviewer) -ne ($null -eq $review.reviewedAtUtc)) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_COMPLETION_INVALID' }
elseif ($null -ne $review.reviewer) {
    [void](Require-Text $review.reviewer 128 'WITS_APPLICATION_REVIEW_COMPLETION_INVALID')
    if (!(Test-Utc $review.reviewedAtUtc)) { Stop-ApplicationReview 'WITS_APPLICATION_REVIEW_COMPLETION_INVALID' }
}
Write-Host "witsApplicationReview=valid;pages=$($expected.Count);state=$($review.reviewState)"
Write-Host 'nonClaim=human-review-is-not-scanner-evidence;validation-does-not-prove-runtime-or-migration-correctness'
