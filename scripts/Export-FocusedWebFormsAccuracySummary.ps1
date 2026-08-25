[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewOutputPath,
    [Parameter(Mandatory = $true)][string]$WebFormsFolder,
    [Parameter(Mandatory = $true)][string]$BackendFolder,
    [Parameter(Mandatory = $true)][string]$ControlsFolder,
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$culture = [Globalization.CultureInfo]::InvariantCulture
$safeToken = '^[A-Za-z][A-Za-z0-9._-]{0,99}$'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

function Get-OptionalProperty {
    param([AllowNull()][object]$Value, [string]$Name)
    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Add-Count {
    param($Counts, [string]$Key)
    if ($Counts.ContainsKey($Key)) { $Counts[$Key]++ } else { $Counts.Add($Key, 1L) }
}

function Read-Count {
    param($Counts, [string]$Key)
    if ($Counts.ContainsKey($Key)) { return [long]$Counts[$Key] }
    return 0L
}

function Format-Count {
    param([long]$Value)
    return $Value.ToString($culture)
}

function Normalize-RelativePath {
    param([string]$Value)
    return $Value.Trim().TrimStart('/', '\').Replace('\', '/').TrimEnd('/')
}

function Get-ScopeRole {
    param([AllowNull()][string]$FilePath)
    if ([string]::IsNullOrWhiteSpace($FilePath)) { return "unknown" }
    $normalized = Normalize-RelativePath $FilePath
    foreach ($scope in @($script:scopes | Sort-Object { $_.Prefix.Length } -Descending)) {
        if ($normalized.Equals($scope.Prefix, [StringComparison]::OrdinalIgnoreCase) -or
            $normalized.StartsWith($scope.Prefix + "/", [StringComparison]::OrdinalIgnoreCase)) {
            return $scope.Role
        }
    }
    return "other"
}

function Get-ArtifactKind {
    param([AllowNull()][string]$FilePath)
    if ([string]::IsNullOrWhiteSpace($FilePath)) { return "unknown" }
    $value = $FilePath.Replace('\', '/').ToLowerInvariant()
    foreach ($suffix in @('aspx', 'ascx', 'master', 'ashx', 'asmx')) {
        if ($value.EndsWith(".$suffix.cs")) { return "$suffix-codebehind" }
    }
    if ($value.EndsWith('.designer.cs')) { return "designer-cs" }
    $kind = switch ([IO.Path]::GetExtension($value)) {
        '.cs' { 'cs' }
        '.aspx' { 'aspx' }
        '.ascx' { 'ascx' }
        '.master' { 'master' }
        '.ashx' { 'ashx' }
        '.asmx' { 'asmx' }
        '.config' { 'config' }
        '.csproj' { 'csproj' }
        '.sln' { 'solution' }
        '.resx' { 'resx' }
        default { 'other' }
    }
    return $kind
}

function Test-WithinPath {
    param([string]$Candidate, [string]$Parent)
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    $separator = [IO.Path]::DirectorySeparatorChar
    $parentPath = $Parent.TrimEnd($separator)
    return $Candidate.Equals($parentPath, $comparison) -or $Candidate.StartsWith($parentPath + $separator, $comparison)
}

try {
    $folders = @($WebFormsFolder, $BackendFolder, $ControlsFolder) | ForEach-Object { Normalize-RelativePath $_ }
    if (@($folders | Select-Object -Unique).Count -ne 3 -or
        @($folders | Where-Object { [string]::IsNullOrWhiteSpace($_) -or [IO.Path]::IsPathRooted($_) -or $_ -match '(^|/)\.\.($|/)' }).Count -ne 0) {
        throw "AccuracyScopeInvalid"
    }
    $script:scopes = @(
        [pscustomobject]@{ Role = 'webforms'; Prefix = $folders[0] },
        [pscustomobject]@{ Role = 'backend'; Prefix = $folders[1] },
        [pscustomobject]@{ Role = 'controls'; Prefix = $folders[2] }
    )

    $reviewRoot = [IO.Path]::GetFullPath($ReviewOutputPath)
    if (-not (Test-Path -LiteralPath $reviewRoot -PathType Container)) { throw "RetainedOutputUnavailable" }
    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $OutputDirectory = if ($IsWindows) { "C:\work\tracemap-summary" } else { Join-Path ([IO.Path]::GetTempPath()) "tracemap-summary" }
    }
    $summaryRoot = [IO.Path]::GetFullPath($OutputDirectory)
    if ((Test-WithinPath $summaryRoot $reviewRoot) -or (Test-WithinPath $summaryRoot $repoRoot)) { throw "SummaryOutputUnsafe" }

    $factsPath = Join-Path $reviewRoot "scan/facts.ndjson"
    $manifestPath = Join-Path $reviewRoot "scan/scan-manifest.json"
    if (-not (Test-Path $factsPath -PathType Leaf) -or -not (Test-Path $manifestPath -PathType Leaf)) { throw "RetainedOutputIncomplete" }
    try { $manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 30 } catch { throw "ManifestMalformed" }
    $analysisLevel = [string](Get-OptionalProperty $manifest 'analysisLevel')
    $buildStatus = [string](Get-OptionalProperty $manifest 'buildStatus')
    if ($analysisLevel -notin @('Level1SemanticAnalysis', 'Level1SemanticAnalysisReduced', 'Level3SyntaxAnalysis', 'Level3SyntaxAnalysisReduced') -or
        $buildStatus -notin @('Succeeded', 'FailedOrPartial', 'NotRun')) { throw "ManifestMalformed" }

    $tierCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    $scopeCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    $artifactCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    $capabilityCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    $diagnosticCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    $gapCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    $registrationShapeCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
    [long]$factTotal = 0
    [long]$inventoryTotal = 0

    foreach ($line in [IO.File]::ReadLines($factsPath)) {
        try { $fact = $line | ConvertFrom-Json -Depth 50 } catch { throw "FactsParseFailed" }
        $factTotal++
        $factType = [string](Get-OptionalProperty $fact 'factType')
        $ruleId = [string](Get-OptionalProperty $fact 'ruleId')
        $tier = [string](Get-OptionalProperty $fact 'evidenceTier')
        $evidence = Get-OptionalProperty $fact 'evidence'
        $filePath = [string](Get-OptionalProperty $evidence 'filePath')
        $scope = Get-ScopeRole $filePath
        $artifact = Get-ArtifactKind $filePath
        $isGap = $factType -eq 'AnalysisGap'
        if ($tier -notin @('Tier1Semantic', 'Tier2Structural', 'Tier3SyntaxOrTextual', 'Tier4Unknown')) { $tier = 'unclassified' }

        Add-Count $tierCounts $tier
        Add-Count $scopeCounts "$scope|facts"
        Add-Count $scopeCounts "$scope|$tier"
        Add-Count $artifactCounts "$artifact|facts"
        Add-Count $artifactCounts "$artifact|$tier"
        if ($isGap) { Add-Count $scopeCounts "$scope|gaps"; Add-Count $artifactCounts "$artifact|gaps" }
        if ($factType -eq 'FileInventoried') { $inventoryTotal++; Add-Count $artifactCounts "$artifact|inventory" }

        $properties = Get-OptionalProperty $fact 'properties'
        if ($factType -eq 'AnalyzerCapabilityDiagnostic') {
            $code = [string](Get-OptionalProperty $properties 'capabilityCode')
            $state = [string](Get-OptionalProperty $properties 'capabilityState')
            $effect = [string](Get-OptionalProperty $properties 'coverageEffect')
            if ($code -match $safeToken -and $state -in @('Available', 'Reduced', 'Unavailable', 'NotRequested', 'Unknown', 'NotApplicable') -and $effect -match $safeToken) {
                Add-Count $capabilityCounts "$scope|$code|$state|$effect"
            }
        }
        if ($factType -eq 'BuildEnvironmentDiagnostic') {
            $code = [string](Get-OptionalProperty $properties 'diagnosticCode')
            $kind = [string](Get-OptionalProperty $properties 'diagnosticKind')
            $effect = [string](Get-OptionalProperty $properties 'coverageEffect')
            if ($code -match $safeToken -and $kind -match $safeToken -and $effect -match $safeToken) {
                Add-Count $diagnosticCounts "$scope|$code|$kind|$effect"
            }
        }
        if ($factType -eq 'WebFormsUserControlRegistered') {
            $shape = [string](Get-OptionalProperty $properties 'registrationShape')
            if ($shape -in @('src', 'assembly-namespace', 'unsupported')) {
                Add-Count $registrationShapeCounts "$scope|$shape"
            }
        }
        if ($isGap -and $ruleId -match '^(?:legacy\.(?:webforms|aspnet|asmx)|database\.(?:operation|sql))\.[a-z0-9.-]+\.v1$') {
            $reason = [string](Get-OptionalProperty $properties 'classification')
            if ($reason -notmatch $safeToken) { $reason = [string](Get-OptionalProperty $properties 'gapKind') }
            if ($reason -match $safeToken) { Add-Count $gapCounts "$scope|$artifact|$ruleId|$reason" }
        }
    }

    $lines = [Collections.Generic.List[string]]::new()
    foreach ($line in @(
        'focused-webforms-accuracy=completed', 'failureCode=none', "analysisLevel=$analysisLevel", "buildStatus=$buildStatus",
        "factTotal=$(Format-Count $factTotal)", "inventoryTotal=$(Format-Count $inventoryTotal)"
    )) { $lines.Add($line) }
    foreach ($tier in @('Tier1Semantic', 'Tier2Structural', 'Tier3SyntaxOrTextual', 'Tier4Unknown', 'unclassified')) {
        $lines.Add("evidence$tier=$(Format-Count (Read-Count $tierCounts $tier))")
    }
    foreach ($scope in @('webforms', 'backend', 'controls', 'other', 'unknown')) {
        $lines.Add("scope-$scope=facts:$(Format-Count (Read-Count $scopeCounts "$scope|facts"))|tier1:$(Format-Count (Read-Count $scopeCounts "$scope|Tier1Semantic"))|tier2:$(Format-Count (Read-Count $scopeCounts "$scope|Tier2Structural"))|tier3:$(Format-Count (Read-Count $scopeCounts "$scope|Tier3SyntaxOrTextual"))|tier4:$(Format-Count (Read-Count $scopeCounts "$scope|Tier4Unknown"))|gaps:$(Format-Count (Read-Count $scopeCounts "$scope|gaps"))")
    }

    $artifactKinds = @($artifactCounts.Keys | ForEach-Object { $_.Split('|', 2)[0] } | Sort-Object -Unique)
    $artifactRows = @($artifactKinds | ForEach-Object {
        [pscustomobject]@{ Kind = $_; Facts = Read-Count $artifactCounts "$_|facts"; Inventory = Read-Count $artifactCounts "$_|inventory"; Tier1 = Read-Count $artifactCounts "$_|Tier1Semantic"; Tier3 = Read-Count $artifactCounts "$_|Tier3SyntaxOrTextual"; Gaps = Read-Count $artifactCounts "$_|gaps" }
    } | Sort-Object @{ Expression = { $_.Facts }; Descending = $true }, Kind)
    for ($index = 0; $index -lt [Math]::Min(15, $artifactRows.Count); $index++) {
        $row = $artifactRows[$index]
        $lines.Add("artifact$($index + 1)=$($row.Kind)|inventory=$(Format-Count $row.Inventory)|facts=$(Format-Count $row.Facts)|tier1=$(Format-Count $row.Tier1)|tier3=$(Format-Count $row.Tier3)|gaps=$(Format-Count $row.Gaps)")
    }
    foreach ($row in @($capabilityCounts.GetEnumerator() | Sort-Object Name)) { $lines.Add("capability=$($row.Key)|count=$(Format-Count $row.Value)") }
    foreach ($row in @($diagnosticCounts.GetEnumerator() | Sort-Object @{ Expression = { $_.Value }; Descending = $true }, Name)) { $lines.Add("diagnostic=$($row.Key)|count=$(Format-Count $row.Value)") }
    foreach ($row in @($registrationShapeCounts.GetEnumerator() | Sort-Object Name)) { $lines.Add("registrationShape=$($row.Key)|count=$(Format-Count $row.Value)") }
    foreach ($row in @($gapCounts.GetEnumerator() | Sort-Object @{ Expression = { $_.Value }; Descending = $true }, Name | Select-Object -First 20)) { $lines.Add("accuracyGap=$($row.Key)|count=$(Format-Count $row.Value)") }

    $referenceGap = @($diagnosticCounts.Keys | Where-Object { $_ -match '\|MissingReferenceAssemblies\|' }).Count -gt 0
    $toolsetGap = @($diagnosticCounts.Keys | Where-Object { $_ -match '\|(LegacyTargetFramework|NonSdkStyleProject|WebApplicationProjectTargets|SdkResolutionFailed|MSBuildRegistrationFailed|LegacyWorkspacePrerequisitesUnresolved|UncategorizedWorkspaceFailure)\|' }).Count -gt 0
    $lines.Add("priority01=$(if ($referenceGap) { 'restore-compatible-reference-assemblies' } elseif ($toolsetGap) { 'recreate-compatible-legacy-msbuild-workspace' } else { 'inspect-highest-count-accuracy-gap' })")
    $lines.Add('priority02=extend-only-proven-webforms-gap-shapes')
    $lines.Add('nonClaim=runtime-behavior-unproven')

    New-Item -ItemType Directory -Path $summaryRoot -Force | Out-Null
    $summaryName = "focused-webforms-accuracy-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff', $culture)).txt"
    [IO.File]::WriteAllLines((Join-Path $summaryRoot $summaryName), $lines, [Text.UTF8Encoding]::new($false))
    'focused-webforms-accuracy-summary-file=created'
    'summaryDirectory=tracemap-summary'
    "summaryFile=$summaryName"
}
catch {
    $known = @('AccuracyScopeInvalid', 'RetainedOutputUnavailable', 'SummaryOutputUnsafe', 'RetainedOutputIncomplete', 'ManifestMalformed', 'FactsParseFailed')
    $classification = if ($_.Exception.Message -in $known) { $_.Exception.Message } else { 'UnexpectedFailure' }
    throw "FocusedWebFormsAccuracySummaryFailed:$classification"
}
