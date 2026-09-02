[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReviewOutputPath,

    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$safeExtractorIdPattern = '^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$'
$safeExtractorVersionPattern = '^[A-Za-z0-9][A-Za-z0-9._-]{0,79}(?:/[A-Za-z0-9][A-Za-z0-9._+-]{0,39})?$'
$invariantCulture = [Globalization.CultureInfo]::InvariantCulture
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

function Get-OptionalProperty {
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Test-WithinPath {
    param(
        [Parameter(Mandatory = $true)][string]$Candidate,
        [Parameter(Mandatory = $true)][string]$Parent
    )

    $comparison = if ($IsWindows) {
        [StringComparison]::OrdinalIgnoreCase
    }
    else {
        [StringComparison]::Ordinal
    }
    $separator = [IO.Path]::DirectorySeparatorChar
    $normalizedParent = $Parent.TrimEnd($separator)
    return $Candidate.Equals($normalizedParent, $comparison) -or
        $Candidate.StartsWith($normalizedParent + $separator, $comparison)
}

function Add-Count {
    param(
        [Parameter(Mandatory = $true)]$Counts,
        [Parameter(Mandatory = $true)][string]$Key
    )

    if ($Counts.ContainsKey($Key)) {
        $Counts[$Key] = $Counts[$Key] + 1L
    }
    else {
        $Counts.Add($Key, 1L)
    }
}

function Format-Count {
    param([Parameter(Mandatory = $true)][long]$Value)
    return $Value.ToString($invariantCulture)
}

try {
    $reviewRoot = [IO.Path]::GetFullPath($ReviewOutputPath)
    if (-not (Test-Path -LiteralPath $reviewRoot -PathType Container)) {
        throw "RetainedOutputUnavailable"
    }

    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $OutputDirectory = if ($IsWindows) {
            "C:\work\tracemap-summary"
        }
        else {
            Join-Path ([IO.Path]::GetTempPath()) "tracemap-summary"
        }
    }

    $summaryRoot = [IO.Path]::GetFullPath($OutputDirectory)
    if ((Test-WithinPath $summaryRoot $reviewRoot) -or (Test-WithinPath $summaryRoot $repoRoot)) {
        throw "SummaryOutputUnsafe"
    }

    $resultPath = Join-Path $reviewRoot "local-review-result.json"
    $factsPath = Join-Path $reviewRoot "scan/facts.ndjson"
    $manifestPath = Join-Path $reviewRoot "scan/scan-manifest.json"
    foreach ($requiredPath in @($resultPath, $factsPath, $manifestPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "RetainedOutputIncomplete"
        }
    }

    try {
        $result = [IO.File]::ReadAllText($resultPath) | ConvertFrom-Json
    }
    catch {
        throw "RetainedResultMalformed"
    }

    if ((Get-OptionalProperty $result "schemaVersion") -ne "local-review-result.v1") {
        throw "RetainedResultMalformed"
    }
    $outcome = [string](Get-OptionalProperty $result "outcome")
    if ($outcome -notin @("succeeded", "partial")) {
        throw "ResultNotCompleted"
    }

    $catalogPath = Join-Path $repoRoot "rules/rule-catalog.yml"
    if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
        throw "RuleCatalogUnavailable"
    }
    $catalogRuleIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in [IO.File]::ReadLines($catalogPath)) {
        if ($line -match '^\s*- id:\s*([^\s#]+)\s*$') {
            [void]$catalogRuleIds.Add($Matches[1])
        }
    }
    if ($catalogRuleIds.Count -eq 0) {
        throw "RuleCatalogUnavailable"
    }

    $gapCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    $gapReasonCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    $extractorFactCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    $extractorGapCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    [long]$factTotal = 0
    [long]$analysisGapTotal = 0
    [long]$uncataloguedGapRuleIdCount = 0
    [long]$unavailableGapReasonCount = 0
    [long]$unavailableExtractorIdentityFactCount = 0

    foreach ($line in [IO.File]::ReadLines($factsPath)) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            throw "FactsParseFailed"
        }
        try {
            $fact = $line | ConvertFrom-Json
        }
        catch {
            throw "FactsParseFailed"
        }

        $factTotal++
        $factType = [string](Get-OptionalProperty $fact "factType")
        $isGap = $factType -eq "AnalysisGap"
        if ($isGap) {
            $analysisGapTotal++
            $ruleId = [string](Get-OptionalProperty $fact "ruleId")
            if ($catalogRuleIds.Contains($ruleId)) {
                Add-Count $gapCounts $ruleId
                $properties = Get-OptionalProperty $fact "properties"
                $classification = [string](Get-OptionalProperty $properties "classification")
                $gapKind = [string](Get-OptionalProperty $properties "gapKind")
                $reasonField = if ($classification -match $safeExtractorIdPattern) {
                    "classification"
                }
                elseif ($gapKind -match $safeExtractorIdPattern) {
                    "gapKind"
                }
                else {
                    $null
                }
                if ($null -eq $reasonField) {
                    $unavailableGapReasonCount++
                }
                else {
                    $reason = if ($reasonField -eq "classification") { $classification } else { $gapKind }
                    Add-Count $gapReasonCounts "$ruleId|$reasonField|$reason"
                }
            }
            else {
                $uncataloguedGapRuleIdCount++
                $unavailableGapReasonCount++
            }
        }

        $evidence = Get-OptionalProperty $fact "evidence"
        $extractorId = [string](Get-OptionalProperty $evidence "extractorId")
        $extractorVersion = [string](Get-OptionalProperty $evidence "extractorVersion")
        if ($extractorId -notmatch $safeExtractorIdPattern -or $extractorVersion -notmatch $safeExtractorVersionPattern) {
            $unavailableExtractorIdentityFactCount++
            continue
        }

        $extractorKey = "$extractorId|$extractorVersion"
        Add-Count $extractorFactCounts $extractorKey
        if ($isGap) {
            Add-Count $extractorGapCounts $extractorKey
        }
    }

    $gapRows = @($gapCounts.GetEnumerator() | ForEach-Object {
        [pscustomobject]@{ RuleId = $_.Key; Count = $_.Value }
    } | Sort-Object -Property @(
        @{ Expression = { $_.Count }; Descending = $true },
        @{ Expression = { $_.RuleId }; Descending = $false }
    ))

    $extractorRows = @($extractorFactCounts.GetEnumerator() | ForEach-Object {
        $parts = $_.Key.Split('|', 2)
        $gapCount = if ($extractorGapCounts.ContainsKey($_.Key)) { $extractorGapCounts[$_.Key] } else { 0L }
        [pscustomobject]@{
            ExtractorId = $parts[0]
            ExtractorVersion = $parts[1]
            FactCount = $_.Value
            GapCount = $gapCount
        }
    } | Sort-Object -Property @(
        @{ Expression = { $_.FactCount }; Descending = $true },
        @{ Expression = { $_.ExtractorId }; Descending = $false },
        @{ Expression = { $_.ExtractorVersion }; Descending = $false }
    ))

    $gapReasonRows = @($gapReasonCounts.GetEnumerator() | ForEach-Object {
        $parts = $_.Key.Split('|', 3)
        [pscustomobject]@{
            RuleId = $parts[0]
            Field = $parts[1]
            Reason = $parts[2]
            Count = $_.Value
        }
    } | Sort-Object -Property @(
        @{ Expression = { $_.Count }; Descending = $true },
        @{ Expression = { $_.RuleId }; Descending = $false },
        @{ Expression = { $_.Field }; Descending = $false },
        @{ Expression = { $_.Reason }; Descending = $false }
    ))

    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add("focused-webforms-evidence-summary=completed")
    $lines.Add("failureCode=none")
    $lines.Add("factTotal=$(Format-Count $factTotal)")
    $lines.Add("analysisGapTotal=$(Format-Count $analysisGapTotal)")
    $lines.Add("cataloguedGapRuleKinds=$(Format-Count $gapCounts.Count)")
    $lines.Add("uncataloguedGapRuleIdCount=$(Format-Count $uncataloguedGapRuleIdCount)")
    $lines.Add("gapReasonKinds=$(Format-Count $gapReasonCounts.Count)")
    $lines.Add("unavailableGapReasonCount=$(Format-Count $unavailableGapReasonCount)")
    $lines.Add("extractorKinds=$(Format-Count $extractorFactCounts.Count)")
    $lines.Add("unavailableExtractorIdentityFactCount=$(Format-Count $unavailableExtractorIdentityFactCount)")

    for ($index = 0; $index -lt 10; $index++) {
        $label = ($index + 1).ToString("D2", $invariantCulture)
        if ($index -lt $gapRows.Count) {
            $row = $gapRows[$index]
            $lines.Add("topGapRule$label=$($row.RuleId)|count=$(Format-Count $row.Count)")
        }
        else {
            $lines.Add("topGapRule$label=unavailable")
        }
    }

    for ($index = 0; $index -lt 10; $index++) {
        $label = ($index + 1).ToString("D2", $invariantCulture)
        if ($index -lt $gapReasonRows.Count) {
            $row = $gapReasonRows[$index]
            $lines.Add("topGapReason$label=$($row.RuleId)|field=$($row.Field)|reason=$($row.Reason)|count=$(Format-Count $row.Count)")
        }
        else {
            $lines.Add("topGapReason$label=unavailable")
        }
    }

    for ($index = 0; $index -lt 10; $index++) {
        $label = ($index + 1).ToString("D2", $invariantCulture)
        if ($index -lt $extractorRows.Count) {
            $row = $extractorRows[$index]
            $lines.Add("topExtractor$label=$($row.ExtractorId)|version=$($row.ExtractorVersion)|facts=$(Format-Count $row.FactCount)|gaps=$(Format-Count $row.GapCount)")
        }
        else {
            $lines.Add("topExtractor$label=unavailable")
        }
    }

    New-Item -ItemType Directory -Path $summaryRoot -Force | Out-Null
    $stamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss-fff", $invariantCulture)
    $summaryName = "focused-webforms-gap-extractor-$stamp.txt"
    $summaryPath = Join-Path $summaryRoot $summaryName
    if (Test-Path -LiteralPath $summaryPath) {
        throw "SummaryOutputExists"
    }
    $temporaryPath = "$summaryPath.tmp"
    [IO.File]::WriteAllLines($temporaryPath, $lines, [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temporaryPath, $summaryPath)

    $readback = [IO.File]::ReadAllLines($summaryPath)
    if ($readback.Count -ne $lines.Count) {
        throw "SummaryVerificationFailed"
    }
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($readback[$index] -cne $lines[$index]) {
            throw "SummaryVerificationFailed"
        }
    }

    Write-Output "focused-webforms-evidence-summary-file=created"
    Write-Output "summaryDirectory=tracemap-summary"
    Write-Output "summaryFile=$summaryName"
}
catch {
    $known = @(
        "RetainedOutputUnavailable",
        "SummaryOutputUnsafe",
        "RetainedOutputIncomplete",
        "RetainedResultMalformed",
        "ResultNotCompleted",
        "RuleCatalogUnavailable",
        "FactsParseFailed",
        "SummaryOutputExists",
        "SummaryVerificationFailed"
    )
    $classification = if ($_.Exception.Message -in $known) { $_.Exception.Message } else { "UnexpectedFailure" }
    throw "FocusedWebFormsEvidenceSummaryFailed:$classification"
}
