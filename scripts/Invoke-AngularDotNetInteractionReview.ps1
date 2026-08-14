[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath,

    [Parameter(Mandatory = $true)]
    [string]$TraceMapRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [switch]$BuildTools,

    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
# Native exit codes are checked after every invocation so the retained result
# receives the operation-specific categorical failure code.
$PSNativeCommandUseErrorActionPreference = $false

$SchemaVersion = "angular-dotnet-interaction-run.v1"
$ResultSchemaVersion = "angular-dotnet-interaction-run-result.v1"
$FeedbackSchemaVersion = "angular-dotnet-interaction-feedback.v1"
$script:FeedbackRuleId = "interaction.review.feedback.v1"
$SafeNamePattern = '^[a-z0-9][a-z0-9._-]{0,63}$'
$MaximumSources = 100
$MaximumEndpointPairs = 100
$MaximumQueries = 500
$AllowedEvidenceTiers = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@("Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"),
    [System.StringComparer]::Ordinal)
$AllowedTerminalSurfaces = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(
        "sql-query", "sql-persistence", "http-route", "http-client", "package-config",
        "wcf-operation", "asmx-service", "asmx-operation", "asmx-client", "asmx-config",
        "asmx-metadata", "remoting-endpoint", "remoting-registration", "remoting-channel",
        "remoting-object", "remoting-api", "legacy-data", "dependency-surface", "message-queue",
        "message-topic", "message-subscription", "message-exchange", "message-stream",
        "message-event", "message-channel", "message-unknown"),
    [System.StringComparer]::Ordinal)

function Stop-InteractionReview {
    param([Parameter(Mandatory = $true)][string]$Code)
    throw [System.InvalidOperationException]::new($Code)
}

function Get-PropertyValue {
    param(
        [Parameter(Mandatory = $true)]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name,
        $DefaultValue = $null
    )

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) { return $DefaultValue }
    return $property.Value
}

function Assert-KnownProperties {
    param(
        [Parameter(Mandatory = $true)]$InputObject,
        [Parameter(Mandatory = $true)][string[]]$Allowed,
        [Parameter(Mandatory = $true)][string]$Code
    )

    foreach ($property in $InputObject.PSObject.Properties.Name) {
        if ($property -notin $Allowed) { Stop-InteractionReview $Code }
    }
}

function ConvertTo-Array {
    param($Value)
    if ($null -eq $Value) { return @() }
    return @($Value)
}

function Resolve-ConfiguredPath {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$ConfiguredPath
    )

    if ([System.IO.Path]::IsPathRooted($ConfiguredPath)) {
        return [System.IO.Path]::GetFullPath($ConfiguredPath)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $ConfiguredPath))
}

function Resolve-RepositoryChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryPath,
        [Parameter(Mandatory = $true)][string]$ConfiguredPath
    )

    $candidate = Resolve-ConfiguredPath $RepositoryPath $ConfiguredPath
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        Stop-InteractionReview "INTERACTION_RUN_SOURCE_SELECTION_UNAVAILABLE"
    }
    $candidate = [string](Resolve-Path -LiteralPath $candidate).ProviderPath
    if (-not (Test-PathWithinRoot $candidate $RepositoryPath)) {
        Stop-InteractionReview "INTERACTION_RUN_SOURCE_SELECTION_OUTSIDE_REPOSITORY"
    }
    return $candidate
}

function Resolve-FuturePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $candidate = [System.IO.Path]::GetFullPath($Path)
    $missing = [System.Collections.Generic.Stack[string]]::new()
    while (-not (Test-Path -LiteralPath $candidate)) {
        $leaf = [System.IO.Path]::GetFileName($candidate)
        if ([string]::IsNullOrWhiteSpace($leaf)) { Stop-InteractionReview "INTERACTION_RUN_OUTPUT_INVALID" }
        $missing.Push($leaf)
        $parent = [System.IO.Path]::GetDirectoryName($candidate)
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $candidate) { Stop-InteractionReview "INTERACTION_RUN_OUTPUT_INVALID" }
        $candidate = $parent
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Container)) { Stop-InteractionReview "INTERACTION_RUN_OUTPUT_INVALID" }
    $resolved = [string](Resolve-Path -LiteralPath $candidate).ProviderPath
    while ($missing.Count -gt 0) { $resolved = Join-Path $resolved $missing.Pop() }
    return [System.IO.Path]::GetFullPath($resolved)
}

function Test-PathWithinRoot {
    param(
        [Parameter(Mandatory = $true)][string]$CandidatePath,
        [Parameter(Mandatory = $true)][string]$RootPath
    )

    $candidate = [System.IO.Path]::GetFullPath($CandidatePath)
    $root = [System.IO.Path]::GetFullPath($RootPath)
    $relative = [System.IO.Path]::GetRelativePath($root, $candidate)
    if ($relative -eq ".") { return $true }
    if ([System.IO.Path]::IsPathRooted($relative) -or $relative -eq "..") { return $false }
    $parentPrefix = ".." + [System.IO.Path]::DirectorySeparatorChar
    return -not $relative.StartsWith($parentPrefix, [System.StringComparison]::Ordinal)
}

function Get-FirstNonBlank {
    param(
        [AllowNull()][object[]]$Values,
        [Parameter(Mandatory = $true)][string]$Fallback
    )

    foreach ($value in $Values) {
        $text = [string]$value
        if (-not [string]::IsNullOrWhiteSpace($text)) { return $text }
    }
    return $Fallback
}

function Test-PropertySelector {
    param([Parameter(Mandatory = $true)][string]$Selector)

    $trimmed = $Selector.Trim()
    if (
        $trimmed.Contains("`n", [System.StringComparison]::Ordinal) -or
        $trimmed.Contains("`r", [System.StringComparison]::Ordinal) -or
        $trimmed.Contains("://", [System.StringComparison]::Ordinal) -or
        $trimmed -match '(^|[,\s])(/|~/|[A-Za-z]:\\)' -or
        $trimmed -match '(?i)(password|secret|token|apikey|api_key|connectionstring)\s*[=:]'
    ) {
        return $false
    }

    $separator = $trimmed.IndexOf(':', [System.StringComparison]::Ordinal)
    if ($separator -le 0) { return $false }
    $kind = $trimmed.Substring(0, $separator)
    $value = $trimmed.Substring($separator + 1).Trim()
    if ($kind -cnotin @("field", "control", "binding", "model", "dto", "symbol", "fact")) { return $false }
    if ($kind -cin @("model", "dto") -and -not $value.Contains('.', [System.StringComparison]::Ordinal)) { return $false }
    return $true
}

function Get-ContainingGitRoot {
    param([Parameter(Mandatory = $true)][string]$Path)

    $candidate = [System.IO.Path]::GetFullPath($Path)
    while (-not (Test-Path -LiteralPath $candidate -PathType Container)) {
        $parent = [System.IO.Path]::GetDirectoryName($candidate)
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $candidate) { return $null }
        $candidate = $parent
    }

    $output = @(& git -C $candidate rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or $output.Count -eq 0) { return $null }
    return [System.IO.Path]::GetFullPath(($output -join "`n").Trim())
}

function Get-Sha256File {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-Sha256Text {
    param([Parameter(Mandatory = $true)][string]$Value)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    $hash = [System.Security.Cryptography.SHA256]::HashData($bytes)
    return [System.Convert]::ToHexString($hash).ToLowerInvariant()
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$FailureCode
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { Stop-InteractionReview $FailureCode }
}

function Invoke-TraceMap {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$FailureCode
    )

    Invoke-CheckedCommand "dotnet" (@($script:DotNetCli) + $Arguments) $FailureCode
}

function Get-GitValue {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryPath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$FailureCode
    )

    $output = @(& git -C $RepositoryPath @Arguments)
    if ($LASTEXITCODE -ne 0 -or $output.Count -eq 0) { Stop-InteractionReview $FailureCode }
    return ($output -join "`n").Trim()
}

function Add-Count {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Counts,
        [Parameter(Mandatory = $true)][string]$Key,
        [int]$Amount = 1
    )

    if ($Counts.ContainsKey($Key)) { $Counts[$Key] += $Amount }
    else { $Counts[$Key] = $Amount }
}

function Add-UnresolvedSignal {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Signals,
        [Parameter(Mandatory = $true)][string]$Producer,
        [Parameter(Mandatory = $true)][string]$Kind,
        [Parameter(Mandatory = $true)][string]$Classification,
        [string]$RuleId = $script:FeedbackRuleId,
        [string]$EvidenceTier = "Tier4Unknown",
        [string]$Coverage = "unknown",
        [int]$Count = 1
    )

    $normalizedKind = if ([string]::IsNullOrWhiteSpace($Kind)) { "unknown" } else { $Kind }
    $normalizedClassification = if ([string]::IsNullOrWhiteSpace($Classification)) { "UnknownAnalysisGap" } else { $Classification }
    $normalizedRule = if ([string]::IsNullOrWhiteSpace($RuleId)) { $script:FeedbackRuleId } else { $RuleId }
    $normalizedTier = if ($script:AllowedEvidenceTiers.Contains($EvidenceTier)) { $EvidenceTier } else { "Tier4Unknown" }
    $normalizedCoverage = if ([string]::IsNullOrWhiteSpace($Coverage)) { "unknown" } else { $Coverage }
    Add-Count $Signals "$Producer|$normalizedKind|$normalizedClassification|$normalizedRule|$normalizedTier|$normalizedCoverage" $Count
}

function Add-ScanGapSignals {
    param(
        [Parameter(Mandatory = $true)][string]$FactsPath,
        [Parameter(Mandatory = $true)][hashtable]$Signals
    )

    foreach ($line in [System.IO.File]::ReadLines($FactsPath)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line.IndexOf('"AnalysisGap"', [System.StringComparison]::Ordinal) -lt 0) { continue }
        $fact = $line | ConvertFrom-Json -Depth 30
        if ((Get-PropertyValue $fact "factType" "") -ne "AnalysisGap") { continue }
        $ruleId = [string](Get-PropertyValue $fact "ruleId" $script:FeedbackRuleId)
        $properties = Get-PropertyValue $fact "properties" $null
        $classification = if ($null -eq $properties) {
            "AnalysisGap"
        }
        else {
            [string](Get-PropertyValue $properties "classification" (Get-PropertyValue $properties "gapKind" "AnalysisGap"))
        }
        $tier = [string](Get-PropertyValue $fact "evidenceTier" "Tier4Unknown")
        $coverage = if ($null -eq $properties) { "unknown" } else { [string](Get-PropertyValue $properties "coverageLabel" "unknown") }
        Add-UnresolvedSignal $Signals "scan" "analysis-gap" $classification $ruleId $tier $coverage
    }
}

function Add-ReportSignals {
    param(
        [Parameter(Mandatory = $true)][string]$JsonPath,
        [Parameter(Mandatory = $true)][string]$Producer,
        [Parameter(Mandatory = $true)][hashtable]$Signals,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.List[object]]$ReportStates
    )

    $report = Get-Content -LiteralPath $JsonPath -Raw | ConvertFrom-Json -Depth 100
    $reportCoverage = [string](Get-PropertyValue $report "reportCoverage" "unknown")
    $summary = Get-PropertyValue $report "summary" $null
    if ($null -ne $summary) {
        $classification = [string](Get-PropertyValue $summary "classification" (Get-PropertyValue $summary "rollupClassification" "unavailable"))
        $coverage = [string](Get-PropertyValue $summary "reportCoverage" (Get-PropertyValue $report "reportCoverage" "unknown"))
        $truncated = [bool](Get-PropertyValue $summary "truncated" $false)
        $ReportStates.Add([ordered]@{
            producer = $Producer
            classification = $classification
            coverage = $coverage
            truncated = $truncated
        })
        if ($truncated) { Add-UnresolvedSignal $Signals $Producer "limit" "TruncatedByLimit" $script:FeedbackRuleId "Tier4Unknown" $coverage }
    }
    else {
        $ReportStates.Add([ordered]@{
            producer = $Producer
            classification = [string](Get-PropertyValue $report "classification" "unavailable")
            coverage = [string](Get-PropertyValue $report "reportCoverage" "unknown")
            truncated = $false
        })
    }

    foreach ($gap in (ConvertTo-Array (Get-PropertyValue $report "gaps" @()))) {
        $kind = [string](Get-PropertyValue $gap "gapKind" (Get-PropertyValue $gap "category" "gap"))
        $classification = [string](Get-PropertyValue $gap "classification" $kind)
        $ruleId = [string](Get-PropertyValue $gap "ruleId" $script:FeedbackRuleId)
        $tier = [string](Get-PropertyValue $gap "evidenceTier" "Tier4Unknown")
        $coverage = [string](Get-PropertyValue $gap "coverage" (Get-PropertyValue $gap "coverageLabel" $reportCoverage))
        Add-UnresolvedSignal $Signals $Producer $kind $classification $ruleId $tier $coverage
    }

    foreach ($gap in (ConvertTo-Array (Get-PropertyValue $report "knownGaps" @()))) {
        $kind = [string](Get-PropertyValue $gap "category" "known-gap")
        if ($kind.EndsWith(":available", [System.StringComparison]::OrdinalIgnoreCase)) { continue }
        $count = [int](Get-PropertyValue $gap "count" 1)
        Add-UnresolvedSignal $Signals $Producer $kind "UnknownAnalysisGap" $script:FeedbackRuleId "Tier4Unknown" $reportCoverage $count
    }

    foreach ($review in (ConvertTo-Array (Get-PropertyValue $report "needsReview" @()))) {
        $kind = [string](Get-PropertyValue $review "reviewKind" "needs-review")
        $ruleId = [string](Get-PropertyValue $review "ruleId" $script:FeedbackRuleId)
        $tier = [string](Get-PropertyValue $review "evidenceTier" "Tier4Unknown")
        Add-UnresolvedSignal $Signals $Producer $kind "NeedsReview" $ruleId $tier $reportCoverage
    }

    $endpointRows = ConvertTo-Array (Get-PropertyValue $report "endpointFindings" (Get-PropertyValue $report "findings" @()))
    foreach ($finding in $endpointRows) {
        $classification = [string](Get-PropertyValue $finding "classification" "unknown")
        if ($classification -in @("MatchedEndpoint", "OptionalSegmentMatch")) { continue }
        $ruleId = Get-FirstNonBlank @(
            (Get-PropertyValue $finding "ruleId" $null),
            (Get-PropertyValue $finding "clientRuleId" $null),
            (Get-PropertyValue $finding "serverRuleId" $null)) $script:FeedbackRuleId
        $tier = Get-FirstNonBlank @(
            (Get-PropertyValue $finding "evidenceTier" $null),
            (Get-PropertyValue $finding "clientEvidenceTier" $null),
            (Get-PropertyValue $finding "serverEvidenceTier" $null)) "Tier4Unknown"
        Add-UnresolvedSignal $Signals $Producer "endpoint-alignment" $classification $ruleId $tier $reportCoverage
    }
}

function Convert-CountsToRows {
    param([Parameter(Mandatory = $true)][hashtable]$Counts)
    return @($Counts.GetEnumerator() |
        Sort-Object Name |
        ForEach-Object { [ordered]@{ value = [string]$_.Name; count = [int]$_.Value } })
}

function Convert-SignalsToRows {
    param([Parameter(Mandatory = $true)][hashtable]$Signals)
    return @($Signals.GetEnumerator() |
        Sort-Object Name |
        ForEach-Object {
            $parts = ([string]$_.Name).Split('|', 6)
            [ordered]@{
                producer = $parts[0]
                kind = $parts[1]
                classification = $parts[2]
                ruleId = $parts[3]
                evidenceTier = $parts[4]
                coverage = $parts[5]
                count = [int]$_.Value
            }
        })
}

function Write-StableJson {
    param(
        [Parameter(Mandatory = $true)]$Value,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $json = $Value | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($Path, $json + "`n", [System.Text.UTF8Encoding]::new($false))
}

function ConvertTo-MarkdownCell {
    param([Parameter(Mandatory = $true)][string]$Value)
    return $Value.Replace('|', '/').Replace("`r", ' ').Replace("`n", ' ')
}

function Write-FeedbackMarkdown {
    param(
        [Parameter(Mandatory = $true)]$Feedback,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# TraceMap Interaction Feedback Summary")
    $lines.Add("")
    $lines.Add("- Run: ``$($Feedback.runId)``")
    $lines.Add("- Outcome: ``$($Feedback.outcome)``")
    $lines.Add("- Sources: $($Feedback.sourceCount)")
    $lines.Add("- Reports: $($Feedback.reportCount)")
    if ($null -ne $Feedback.failureCode) { $lines.Add("- Failure code: ``$($Feedback.failureCode)``") }
    $lines.Add("")
    $lines.Add("## Source Kinds")
    $lines.Add("")
    $lines.Add("| Kind | Count |")
    $lines.Add("| --- | ---: |")
    foreach ($row in $Feedback.sourceKinds) { $lines.Add("| $(ConvertTo-MarkdownCell $row.value) | $($row.count) |") }
    $lines.Add("")
    $lines.Add("## Scan States")
    $lines.Add("")
    $lines.Add("| Analysis/build state | Count |")
    $lines.Add("| --- | ---: |")
    foreach ($row in $Feedback.scanStates) { $lines.Add("| $(ConvertTo-MarkdownCell $row.value) | $($row.count) |") }
    $lines.Add("")
    $lines.Add("## Unresolved Signals")
    $lines.Add("")
    $lines.Add("| Producer | Kind | Classification | Rule ID | Tier | Coverage | Count |")
    $lines.Add("| --- | --- | --- | --- | --- | --- | ---: |")
    foreach ($row in $Feedback.unresolvedSignals) {
        $lines.Add("| $(ConvertTo-MarkdownCell $row.producer) | $(ConvertTo-MarkdownCell $row.kind) | $(ConvertTo-MarkdownCell $row.classification) | ``$(ConvertTo-MarkdownCell $row.ruleId)`` | $(ConvertTo-MarkdownCell $row.evidenceTier) | $(ConvertTo-MarkdownCell $row.coverage) | $($row.count) |")
    }
    if ($Feedback.unresolvedSignals.Count -eq 0) { $lines.Add("| none recorded | — | — | — | — | — | 0 |") }
    $lines.Add("")
    $lines.Add("## Limitations")
    $lines.Add("")
    foreach ($limitation in $Feedback.limitations) { $lines.Add("- $limitation") }
    $lines.Add("")
    [System.IO.File]::WriteAllLines($Path, $lines, [System.Text.UTF8Encoding]::new($false))
}

if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) { Stop-InteractionReview "INTERACTION_RUN_CONFIG_UNAVAILABLE" }
if (-not (Test-Path -LiteralPath $TraceMapRoot -PathType Container)) { Stop-InteractionReview "INTERACTION_RUN_TRACEMAP_UNAVAILABLE" }
$ConfigPath = [string](Resolve-Path -LiteralPath $ConfigPath).ProviderPath
$TraceMapRoot = [string](Resolve-Path -LiteralPath $TraceMapRoot).ProviderPath
$OutputRoot = Resolve-FuturePath $OutputRoot

$configText = Get-Content -LiteralPath $ConfigPath -Raw
$configSha256 = Get-Sha256File $ConfigPath
$config = $configText | ConvertFrom-Json -Depth 100
Assert-KnownProperties $config @("schemaVersion", "sources", "endpointPairs", "propertyFlows", "routeFlows", "paths", "reports") "INTERACTION_RUN_CONFIG_PROPERTY_UNSUPPORTED"
if ((Get-PropertyValue $config "schemaVersion" "") -ne $SchemaVersion) { Stop-InteractionReview "INTERACTION_RUN_CONFIG_SCHEMA_UNSUPPORTED" }

$sources = ConvertTo-Array (Get-PropertyValue $config "sources" @())
if ($sources.Count -lt 2 -or $sources.Count -gt $MaximumSources) { Stop-InteractionReview "INTERACTION_RUN_SOURCE_COUNT_INVALID" }
$configDirectory = Split-Path -Parent $ConfigPath
$sourceLabels = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$resolvedSources = [System.Collections.Generic.List[object]]::new()

foreach ($source in $sources) {
    Assert-KnownProperties $source @("label", "kind", "repositoryPath", "projects", "solutions", "include", "exclude", "targetFramework") "INTERACTION_RUN_SOURCE_PROPERTY_UNSUPPORTED"
    $label = [string](Get-PropertyValue $source "label" "")
    $kind = [string](Get-PropertyValue $source "kind" "")
    if ($label -notmatch $SafeNamePattern -or -not $sourceLabels.Add($label)) { Stop-InteractionReview "INTERACTION_RUN_SOURCE_LABEL_INVALID" }
    if ($kind -notin @("typescript", "dotnet")) { Stop-InteractionReview "INTERACTION_RUN_SOURCE_KIND_UNSUPPORTED" }
    $repositoryPath = Resolve-ConfiguredPath $configDirectory ([string](Get-PropertyValue $source "repositoryPath" ""))
    if (-not (Test-Path -LiteralPath $repositoryPath -PathType Container)) { Stop-InteractionReview "INTERACTION_RUN_SOURCE_REPOSITORY_UNAVAILABLE" }
    $gitRoot = Get-GitValue $repositoryPath @("rev-parse", "--show-toplevel") "INTERACTION_RUN_SOURCE_GIT_ROOT_UNAVAILABLE"
    $gitPrefixOutput = @(& git -C $repositoryPath rev-parse --show-prefix)
    if ($LASTEXITCODE -ne 0) { Stop-InteractionReview "INTERACTION_RUN_SOURCE_GIT_ROOT_UNAVAILABLE" }
    if (($gitPrefixOutput -join "").Length -ne 0) {
        Stop-InteractionReview "INTERACTION_RUN_SOURCE_REPOSITORY_NOT_GIT_ROOT"
    }
    $repositoryPath = [System.IO.Path]::GetFullPath($gitRoot)
    $commitSha = Get-GitValue $repositoryPath @("rev-parse", "HEAD") "INTERACTION_RUN_SOURCE_COMMIT_UNAVAILABLE"
    $workingTree = @(& git -C $repositoryPath status --short)
    if ($LASTEXITCODE -ne 0) { Stop-InteractionReview "INTERACTION_RUN_SOURCE_STATUS_UNAVAILABLE" }
    if ($workingTree.Count -gt 0) { Stop-InteractionReview "INTERACTION_RUN_SOURCE_DIRTY" }

    $projects = @((ConvertTo-Array (Get-PropertyValue $source "projects" @())) | ForEach-Object { Resolve-RepositoryChildPath $repositoryPath ([string]$_) })
    $solutions = @((ConvertTo-Array (Get-PropertyValue $source "solutions" @())) | ForEach-Object { Resolve-RepositoryChildPath $repositoryPath ([string]$_) })
    $targetFramework = [string](Get-PropertyValue $source "targetFramework" "")
    if ($kind -eq "typescript" -and ($solutions.Count -gt 0 -or -not [string]::IsNullOrWhiteSpace($targetFramework))) {
        Stop-InteractionReview "INTERACTION_RUN_TYPESCRIPT_SELECTION_INVALID"
    }

    $resolvedSources.Add([ordered]@{
        label = $label
        kind = $kind
        repositoryPath = $repositoryPath
        commitSha = $commitSha
        projects = $projects
        solutions = $solutions
        include = @((ConvertTo-Array (Get-PropertyValue $source "include" @())) | ForEach-Object { [string]$_ })
        exclude = @((ConvertTo-Array (Get-PropertyValue $source "exclude" @())) | ForEach-Object { [string]$_ })
        targetFramework = $targetFramework
    })
}

$outputGitRoot = Get-ContainingGitRoot $OutputRoot
$traceMapGitRoot = Get-GitValue $TraceMapRoot @("rev-parse", "--show-toplevel") "INTERACTION_RUN_TRACEMAP_GIT_ROOT_UNAVAILABLE"
if (
    (Test-PathWithinRoot $OutputRoot $TraceMapRoot) -or
    ($null -ne $outputGitRoot -and $outputGitRoot.Equals($traceMapGitRoot, [System.StringComparison]::OrdinalIgnoreCase))
) {
    Stop-InteractionReview "INTERACTION_RUN_OUTPUT_INSIDE_TRACEMAP"
}
foreach ($source in $resolvedSources) {
    if (
        (Test-PathWithinRoot $OutputRoot $source.repositoryPath) -or
        ($null -ne $outputGitRoot -and $outputGitRoot.Equals($source.repositoryPath, [System.StringComparison]::OrdinalIgnoreCase))
    ) {
        Stop-InteractionReview "INTERACTION_RUN_OUTPUT_INSIDE_SOURCE"
    }
}

$endpointPairs = @(ConvertTo-Array (Get-PropertyValue $config "endpointPairs" @()))
$propertyFlows = @(ConvertTo-Array (Get-PropertyValue $config "propertyFlows" @()))
$routeFlows = @(ConvertTo-Array (Get-PropertyValue $config "routeFlows" @()))
$pathQueries = @(ConvertTo-Array (Get-PropertyValue $config "paths" @()))
if ($endpointPairs.Count -gt $MaximumEndpointPairs) { Stop-InteractionReview "INTERACTION_RUN_ENDPOINT_PAIR_COUNT_INVALID" }
foreach ($array in @($propertyFlows, $routeFlows, $pathQueries)) {
    if (@($array).Count -gt $MaximumQueries) { Stop-InteractionReview "INTERACTION_RUN_QUERY_COUNT_INVALID" }
}

$queryNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($pair in $endpointPairs) {
    Assert-KnownProperties $pair @("name", "clientLabel", "serverLabel") "INTERACTION_RUN_ENDPOINT_PAIR_PROPERTY_UNSUPPORTED"
    $name = [string](Get-PropertyValue $pair "name" "")
    $client = [string](Get-PropertyValue $pair "clientLabel" "")
    $server = [string](Get-PropertyValue $pair "serverLabel" "")
    if ($name -notmatch $SafeNamePattern -or -not $queryNames.Add("endpoint:$name")) { Stop-InteractionReview "INTERACTION_RUN_QUERY_NAME_INVALID" }
    if (-not $sourceLabels.Contains($client) -or -not $sourceLabels.Contains($server)) { Stop-InteractionReview "INTERACTION_RUN_ENDPOINT_SOURCE_UNAVAILABLE" }
}

foreach ($query in $propertyFlows) {
    Assert-KnownProperties $query @("name", "selector", "sourceLabel", "framework") "INTERACTION_RUN_PROPERTY_FLOW_PROPERTY_UNSUPPORTED"
    $name = [string](Get-PropertyValue $query "name" "")
    $selector = [string](Get-PropertyValue $query "selector" "")
    $sourceLabel = [string](Get-PropertyValue $query "sourceLabel" "")
    $framework = [string](Get-PropertyValue $query "framework" "any")
    if ($name -notmatch $SafeNamePattern -or -not $queryNames.Add("property:$name") -or -not (Test-PropertySelector $selector)) { Stop-InteractionReview "INTERACTION_RUN_QUERY_INVALID" }
    if (-not [string]::IsNullOrWhiteSpace($sourceLabel) -and -not $sourceLabels.Contains($sourceLabel)) { Stop-InteractionReview "INTERACTION_RUN_QUERY_SOURCE_UNAVAILABLE" }
    if ($framework -notin @("angular", "razor", "any")) { Stop-InteractionReview "INTERACTION_RUN_QUERY_FRAMEWORK_UNSUPPORTED" }
}

foreach ($query in $routeFlows) {
    Assert-KnownProperties $query @("name", "route") "INTERACTION_RUN_ROUTE_FLOW_PROPERTY_UNSUPPORTED"
    $name = [string](Get-PropertyValue $query "name" "")
    if ($name -notmatch $SafeNamePattern -or -not $queryNames.Add("route:$name") -or [string]::IsNullOrWhiteSpace([string](Get-PropertyValue $query "route" ""))) {
        Stop-InteractionReview "INTERACTION_RUN_QUERY_INVALID"
    }
}

foreach ($query in $pathQueries) {
    Assert-KnownProperties $query @("name", "fromEndpoint", "toSurface", "sourcePair") "INTERACTION_RUN_PATH_PROPERTY_UNSUPPORTED"
    $name = [string](Get-PropertyValue $query "name" "")
    $fromEndpoint = [string](Get-PropertyValue $query "fromEndpoint" "")
    $toSurface = [string](Get-PropertyValue $query "toSurface" "")
    if ($name -notmatch $SafeNamePattern -or -not $queryNames.Add("path:$name") -or [string]::IsNullOrWhiteSpace($fromEndpoint) -or -not $script:AllowedTerminalSurfaces.Contains($toSurface.Trim())) {
        Stop-InteractionReview "INTERACTION_RUN_QUERY_INVALID"
    }
    $sourcePair = [string](Get-PropertyValue $query "sourcePair" "")
    if (-not [string]::IsNullOrWhiteSpace($sourcePair)) {
        $parts = $sourcePair.Split(':')
        if ($parts.Count -ne 2 -or -not $sourceLabels.Contains($parts[0]) -or -not $sourceLabels.Contains($parts[1])) {
            Stop-InteractionReview "INTERACTION_RUN_QUERY_SOURCE_PAIR_INVALID"
        }
    }
}

$reports = Get-PropertyValue $config "reports" ([pscustomobject]@{})
Assert-KnownProperties $reports @("combinedDependency", "portfolio") "INTERACTION_RUN_REPORT_PROPERTY_UNSUPPORTED"
$combinedDependencyEnabled = [bool](Get-PropertyValue $reports "combinedDependency" $true)
$portfolioEnabled = [bool](Get-PropertyValue $reports "portfolio" $true)

$script:DotNetCli = Join-Path $TraceMapRoot "src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll"
$typeScriptCli = Join-Path $TraceMapRoot "src/typescript/dist/src/cli.js"
if ($BuildTools) {
    Invoke-CheckedCommand "dotnet" @("build", (Join-Path $TraceMapRoot "src/dotnet/TraceMap.sln")) "INTERACTION_RUN_DOTNET_BUILD_FAILED"
    Invoke-CheckedCommand "npm" @("--prefix", (Join-Path $TraceMapRoot "src/typescript"), "ci") "INTERACTION_RUN_TYPESCRIPT_INSTALL_FAILED"
    Invoke-CheckedCommand "npm" @("--prefix", (Join-Path $TraceMapRoot "src/typescript"), "run", "build") "INTERACTION_RUN_TYPESCRIPT_BUILD_FAILED"
}
if (-not (Test-Path -LiteralPath $script:DotNetCli -PathType Leaf)) { Stop-InteractionReview "INTERACTION_RUN_DOTNET_CLI_UNAVAILABLE" }
if (($resolvedSources.kind -contains "typescript") -and -not (Test-Path -LiteralPath $typeScriptCli -PathType Leaf)) { Stop-InteractionReview "INTERACTION_RUN_TYPESCRIPT_CLI_UNAVAILABLE" }

if ($ValidateOnly) {
    [ordered]@{
        schemaVersion = $SchemaVersion
        sourceCount = $resolvedSources.Count
        endpointPairCount = $endpointPairs.Count
        propertyFlowCount = $propertyFlows.Count
        routeFlowCount = $routeFlows.Count
        pathQueryCount = $pathQueries.Count
        combinedDependency = $combinedDependencyEnabled
        portfolio = $portfolioEnabled
    } | ConvertTo-Json -Depth 10
    exit 0
}

if (Test-Path -LiteralPath $OutputRoot) { Stop-InteractionReview "INTERACTION_RUN_OUTPUT_EXISTS" }
$outputParent = Split-Path -Parent $OutputRoot
if ([string]::IsNullOrWhiteSpace($outputParent)) { Stop-InteractionReview "INTERACTION_RUN_OUTPUT_INVALID" }
New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
$staging = Join-Path $outputParent ("." + [System.IO.Path]::GetFileName($OutputRoot) + ".interaction-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $staging | Out-Null

$sourceResults = [System.Collections.Generic.List[object]]::new()
$reportResults = [System.Collections.Generic.List[object]]::new()
$reportStates = [System.Collections.Generic.List[object]]::new()
$signals = @{}
$sourceKindCounts = @{}
$scanStateCounts = @{}
$failureCode = $null
$outcome = "succeeded"

try {
    $scanRoot = Join-Path $staging "scans"
    New-Item -ItemType Directory -Path $scanRoot | Out-Null

    foreach ($source in $resolvedSources) {
        $scanOutput = Join-Path $scanRoot $source.label
        if ($source.kind -eq "typescript") {
            $arguments = [System.Collections.Generic.List[string]]::new()
            foreach ($value in @("scan", "--repo", $source.repositoryPath, "--out", $scanOutput)) { $arguments.Add($value) }
            foreach ($project in $source.projects) { $arguments.Add("--project"); $arguments.Add($project) }
            foreach ($include in $source.include) { $arguments.Add("--include"); $arguments.Add($include) }
            foreach ($exclude in $source.exclude) { $arguments.Add("--exclude"); $arguments.Add($exclude) }
            Invoke-CheckedCommand "node" (@($typeScriptCli) + $arguments.ToArray()) "INTERACTION_RUN_TYPESCRIPT_SCAN_FAILED"
        }
        else {
            $arguments = [System.Collections.Generic.List[string]]::new()
            foreach ($value in @("scan", "--repo", $source.repositoryPath, "--out", $scanOutput)) { $arguments.Add($value) }
            foreach ($solution in $source.solutions) { $arguments.Add("--solution"); $arguments.Add($solution) }
            foreach ($project in $source.projects) { $arguments.Add("--project"); $arguments.Add($project) }
            foreach ($include in $source.include) { $arguments.Add("--include"); $arguments.Add($include) }
            foreach ($exclude in $source.exclude) { $arguments.Add("--exclude"); $arguments.Add($exclude) }
            if (-not [string]::IsNullOrWhiteSpace($source.targetFramework)) { $arguments.Add("--target-framework"); $arguments.Add($source.targetFramework) }
            Invoke-TraceMap $arguments.ToArray() "INTERACTION_RUN_DOTNET_SCAN_FAILED"
        }

        $postScanHead = Get-GitValue $source.repositoryPath @("rev-parse", "HEAD") "INTERACTION_RUN_SOURCE_COMMIT_UNAVAILABLE"
        $postScanStatus = @(& git -C $source.repositoryPath status --short)
        if ($LASTEXITCODE -ne 0) { Stop-InteractionReview "INTERACTION_RUN_SOURCE_STATUS_UNAVAILABLE" }
        if ($postScanHead -ne $source.commitSha -or $postScanStatus.Count -gt 0) {
            Stop-InteractionReview "INTERACTION_RUN_SOURCE_CHANGED_DURING_SCAN"
        }

        $manifestPath = Join-Path $scanOutput "scan-manifest.json"
        $factsPath = Join-Path $scanOutput "facts.ndjson"
        $indexPath = Join-Path $scanOutput "index.sqlite"
        foreach ($required in @($manifestPath, $factsPath, $indexPath, (Join-Path $scanOutput "report.md"), (Join-Path $scanOutput "logs/analyzer.log"))) {
            if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { Stop-InteractionReview "INTERACTION_RUN_SCAN_ARTIFACT_MISSING" }
        }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 50
        if ([string](Get-PropertyValue $manifest "commitSha" "") -ne $source.commitSha) { Stop-InteractionReview "INTERACTION_RUN_SCAN_COMMIT_MISMATCH" }
        $analysisLevel = [string](Get-PropertyValue $manifest "analysisLevel" "unknown")
        $buildStatus = [string](Get-PropertyValue $manifest "buildStatus" "unknown")
        Add-Count $sourceKindCounts $source.kind
        Add-Count $scanStateCounts "$analysisLevel|$buildStatus"
        Add-ScanGapSignals $factsPath $signals
        $sourceResults.Add([ordered]@{
            label = $source.label
            kind = $source.kind
            commitSha = $source.commitSha
            analysisLevel = $analysisLevel
            buildStatus = $buildStatus
            scanPath = "scans/$($source.label)"
            indexSha256 = Get-Sha256File $indexPath
        })
    }

    $combinedIndex = Join-Path $staging "combined.sqlite"
    $combineArguments = [System.Collections.Generic.List[string]]::new()
    $combineArguments.Add("combine")
    foreach ($source in ($resolvedSources | Sort-Object label)) {
        $combineArguments.Add("--index")
        $combineArguments.Add((Join-Path $scanRoot "$($source.label)/index.sqlite"))
        $combineArguments.Add("--label")
        $combineArguments.Add($source.label)
    }
    $combineArguments.Add("--out")
    $combineArguments.Add($combinedIndex)
    Invoke-TraceMap $combineArguments.ToArray() "INTERACTION_RUN_COMBINE_FAILED"

    $reportsRoot = Join-Path $staging "reports"
    New-Item -ItemType Directory -Path $reportsRoot | Out-Null

    if ($combinedDependencyEnabled) {
        $reportOut = Join-Path $reportsRoot "dependency"
        Invoke-TraceMap @("report", "--index", $combinedIndex, "--out", $reportOut, "--format", "json") "INTERACTION_RUN_DEPENDENCY_REPORT_FAILED"
        $jsonPath = Join-Path $reportOut "dependency-report.json"
        Add-ReportSignals $jsonPath "dependency" $signals $reportStates
        $reportResults.Add([ordered]@{ name = "dependency"; kind = "combined-dependency"; relativePath = "reports/dependency/dependency-report.json"; sha256 = Get-Sha256File $jsonPath })
    }

    if ($portfolioEnabled) {
        $reportOut = Join-Path $reportsRoot "portfolio"
        $arguments = [System.Collections.Generic.List[string]]::new()
        foreach ($value in @("portfolio", "--out", $reportOut, "--format", "json")) { $arguments.Add($value) }
        foreach ($source in ($resolvedSources | Sort-Object label)) {
            $arguments.Add("--index")
            $arguments.Add((Join-Path $scanRoot "$($source.label)/index.sqlite"))
            $arguments.Add("--label")
            $arguments.Add($source.label)
        }
        Invoke-TraceMap $arguments.ToArray() "INTERACTION_RUN_PORTFOLIO_REPORT_FAILED"
        $jsonPath = Join-Path $reportOut "portfolio-report.json"
        Add-ReportSignals $jsonPath "portfolio" $signals $reportStates
        $reportResults.Add([ordered]@{ name = "portfolio"; kind = "portfolio"; relativePath = "reports/portfolio/portfolio-report.json"; sha256 = Get-Sha256File $jsonPath })
    }

    foreach ($pair in $endpointPairs) {
        $name = [string]$pair.name
        $client = [string]$pair.clientLabel
        $server = [string]$pair.serverLabel
        $reportOut = Join-Path $reportsRoot "endpoints/$name"
        Invoke-TraceMap @(
            "endpoints",
            "--client-index", (Join-Path $scanRoot "$client/index.sqlite"),
            "--server-index", (Join-Path $scanRoot "$server/index.sqlite"),
            "--client-label", $client,
            "--server-label", $server,
            "--out", $reportOut,
            "--format", "json") "INTERACTION_RUN_ENDPOINT_REPORT_FAILED"
        $jsonPath = Join-Path $reportOut "endpoint-report.json"
        Add-ReportSignals $jsonPath "endpoint-alignment" $signals $reportStates
        $reportResults.Add([ordered]@{ name = $name; kind = "endpoint-alignment"; relativePath = "reports/endpoints/$name/endpoint-report.json"; sha256 = Get-Sha256File $jsonPath })
    }

    foreach ($query in $propertyFlows) {
        $name = [string]$query.name
        $reportOut = Join-Path $reportsRoot "property-flow/$name"
        $arguments = [System.Collections.Generic.List[string]]::new()
        foreach ($value in @("property-flow", "--index", $combinedIndex, "--property", [string]$query.selector, "--out", $reportOut, "--format", "json")) { $arguments.Add($value) }
        $sourceLabel = [string](Get-PropertyValue $query "sourceLabel" "")
        if (-not [string]::IsNullOrWhiteSpace($sourceLabel)) { $arguments.Add("--source"); $arguments.Add($sourceLabel) }
        $framework = [string](Get-PropertyValue $query "framework" "any")
        $arguments.Add("--framework"); $arguments.Add($framework)
        Invoke-TraceMap $arguments.ToArray() "INTERACTION_RUN_PROPERTY_FLOW_FAILED"
        $jsonPath = Join-Path $reportOut "property-flow-report.json"
        Add-ReportSignals $jsonPath "property-flow" $signals $reportStates
        $reportResults.Add([ordered]@{ name = $name; kind = "property-flow"; relativePath = "reports/property-flow/$name/property-flow-report.json"; sha256 = Get-Sha256File $jsonPath })
    }

    foreach ($query in $routeFlows) {
        $name = [string]$query.name
        $reportOut = Join-Path $reportsRoot "route-flow/$name"
        Invoke-TraceMap @("route-flow", "--index", $combinedIndex, "--route", [string]$query.route, "--out", $reportOut, "--format", "json") "INTERACTION_RUN_ROUTE_FLOW_FAILED"
        $jsonPath = Join-Path $reportOut "route-flow-report.json"
        Add-ReportSignals $jsonPath "route-flow" $signals $reportStates
        $reportResults.Add([ordered]@{ name = $name; kind = "route-flow"; relativePath = "reports/route-flow/$name/route-flow-report.json"; sha256 = Get-Sha256File $jsonPath })
    }

    foreach ($query in $pathQueries) {
        $name = [string]$query.name
        $reportOut = Join-Path $reportsRoot "paths/$name"
        $arguments = [System.Collections.Generic.List[string]]::new()
        foreach ($value in @("paths", "--index", $combinedIndex, "--from-endpoint", [string]$query.fromEndpoint, "--to-surface", [string]$query.toSurface, "--out", $reportOut, "--format", "json")) { $arguments.Add($value) }
        $sourcePair = [string](Get-PropertyValue $query "sourcePair" "")
        if (-not [string]::IsNullOrWhiteSpace($sourcePair)) { $arguments.Add("--source-pair"); $arguments.Add($sourcePair) }
        Invoke-TraceMap $arguments.ToArray() "INTERACTION_RUN_PATH_QUERY_FAILED"
        $jsonPath = Join-Path $reportOut "paths-report.json"
        Add-ReportSignals $jsonPath "dependency-path" $signals $reportStates
        $reportResults.Add([ordered]@{ name = $name; kind = "dependency-path"; relativePath = "reports/paths/$name/paths-report.json"; sha256 = Get-Sha256File $jsonPath })
    }
}
catch {
    $outcome = "failed"
    $candidate = $_.Exception.Message
    $failureCode = if ($candidate -match '^INTERACTION_RUN_[A-Z_]+$') { $candidate } else { "INTERACTION_RUN_UNEXPECTED_FAILURE" }
    if ($VerbosePreference -ne "SilentlyContinue") {
        $safeMessage = ($_.Exception.Message -replace '[\r\n]+', ' ')
        Write-Verbose "interaction runner failure type=$($_.Exception.GetType().FullName);line=$($_.InvocationInfo.ScriptLineNumber);message=$safeMessage"
    }
}

$sourceCommits = @($resolvedSources | Sort-Object label | ForEach-Object { $_.commitSha })
$runId = "interaction-" + (Get-Sha256Text ($configSha256 + "|" + ($sourceCommits -join "|"))).Substring(0, 20)
$feedback = [ordered]@{
    schemaVersion = $FeedbackSchemaVersion
    runId = $runId
    outcome = $outcome
    sourceCount = $resolvedSources.Count
    sourceKinds = @(Convert-CountsToRows $sourceKindCounts)
    scanStates = @(Convert-CountsToRows $scanStateCounts)
    reportCount = $reportResults.Count
    reportStates = @($reportStates | Sort-Object `
        { [string]$_.producer },
        { [string]$_.classification },
        { [string]$_.coverage },
        { [bool]$_.truncated })
    unresolvedSignals = @(Convert-SignalsToRows $signals)
    failureCode = $failureCode
    limitations = @(
        "Counts are categorical projections and may include the same underlying gap in more than one report.",
        "This summary omits repository paths, source identifiers, routes, symbols, SQL, configuration values, and source content.",
        "A missing or unresolved static link is not proof that runtime behavior or a dependency is absent."
    )
}
Write-StableJson $feedback (Join-Path $staging "feedback-summary.json")
Write-FeedbackMarkdown $feedback (Join-Path $staging "feedback-summary.md")

$artifacts = @(
    Get-ChildItem -LiteralPath $staging -File -Recurse |
        Where-Object { $_.Name -notin @("interaction-run-result.json") } |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($staging, $_.FullName).Replace('\', '/')
            [ordered]@{ relativePath = $relative; sha256 = Get-Sha256File $_.FullName }
        } |
        Sort-Object relativePath
)
$result = [ordered]@{
    schemaVersion = $ResultSchemaVersion
    runId = $runId
    configSha256 = $configSha256
    outcome = $outcome
    failureCode = $failureCode
    combinedIndexAvailable = Test-Path -LiteralPath (Join-Path $staging "combined.sqlite") -PathType Leaf
    sources = @($sourceResults | Sort-Object label)
    reports = @($reportResults | Sort-Object kind, name)
    artifacts = $artifacts
    nextAction = if ($outcome -eq "succeeded" -and $signals.Count -eq 0) { "review-generated-reports" } elseif ($outcome -eq "succeeded") { "review-feedback-summary" } else { "inspect-failure-code-and-retained-stage-output" }
    limitations = @(
        "This run composes deterministic static evidence and does not prove runtime execution, reachability, correctness, ownership, or migration safety.",
        "Generated reports can contain private repository-relative identifiers and must remain in owner-controlled storage."
    )
}
Write-StableJson $result (Join-Path $staging "interaction-run-result.json")

Move-Item -LiteralPath $staging -Destination $OutputRoot
Write-Output "interaction-review=$outcome;runId=$runId;sourceCount=$($resolvedSources.Count);reportCount=$($reportResults.Count);unresolvedSignalKinds=$($signals.Count)"
if ($null -ne $failureCode) { Write-Output "failureCode=$failureCode" }
Write-Output "output=$OutputRoot"

if ($outcome -ne "succeeded") { exit 1 }
