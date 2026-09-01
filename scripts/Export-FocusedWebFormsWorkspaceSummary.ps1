[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewOutputPath,
    [Parameter(Mandatory = $true)][string]$WebFormsFolder,
    [Parameter(Mandatory = $true)][string]$BackendFolder,
    [Parameter(Mandatory = $true)][string]$ControlsFolder,
    [Parameter(Mandatory = $true)][string]$TraceMapHead,
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$culture = [Globalization.CultureInfo]::InvariantCulture
$safeToken = '^[A-Za-z][A-Za-z0-9._-]{0,99}$'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$isWindowsPlatform = [IO.Path]::DirectorySeparatorChar -eq '\'

function Get-OptionalProperty {
    param([AllowNull()][object]$Value, [string]$Name)
    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Normalize-RelativePath {
    param([string]$Value)
    return $Value.Trim().TrimStart('/', '\').Replace('\', '/').TrimEnd('/')
}

function Get-ScopeRole {
    param([AllowNull()][string]$FilePath)
    if ([string]::IsNullOrWhiteSpace($FilePath)) { return 'unknown' }
    $normalized = Normalize-RelativePath $FilePath
    foreach ($scope in @($script:scopes | Sort-Object { $_.Prefix.Length } -Descending)) {
        if ($normalized.Equals($scope.Prefix, [StringComparison]::OrdinalIgnoreCase) -or
            $normalized.StartsWith($scope.Prefix + '/', [StringComparison]::OrdinalIgnoreCase)) {
            return $scope.Role
        }
    }
    return 'other'
}

function Test-WithinPath {
    param([string]$Candidate, [string]$Parent)
    $comparison = if ($script:isWindowsPlatform) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    $separator = [IO.Path]::DirectorySeparatorChar
    $parentPath = $Parent.TrimEnd($separator)
    return $Candidate.Equals($parentPath, $comparison) -or $Candidate.StartsWith($parentPath + $separator, $comparison)
}

function Add-Count {
    param($Counts, [string]$Key)
    if ($Counts.ContainsKey($Key)) { $Counts[$Key]++ } else { $Counts.Add($Key, 1L) }
}

try {
    if ($TraceMapHead -notmatch '^[0-9a-fA-F]{40}$') { throw 'TraceMapHeadInvalid' }
    $folders = @($WebFormsFolder, $BackendFolder, $ControlsFolder) | ForEach-Object { Normalize-RelativePath $_ }
    if (@($folders | Select-Object -Unique).Count -ne 3 -or
        @($folders | Where-Object { [string]::IsNullOrWhiteSpace($_) -or [IO.Path]::IsPathRooted($_) -or $_ -match '(^|/)\.\.($|/)' }).Count -ne 0) {
        throw 'WorkspaceScopeInvalid'
    }
    $script:scopes = @(
        [pscustomobject]@{ Role = 'webforms'; Prefix = $folders[0] },
        [pscustomobject]@{ Role = 'backend'; Prefix = $folders[1] },
        [pscustomobject]@{ Role = 'controls'; Prefix = $folders[2] }
    )

    $reviewRoot = [IO.Path]::GetFullPath($ReviewOutputPath)
    if (-not (Test-Path -LiteralPath $reviewRoot -PathType Container)) { throw 'RetainedOutputUnavailable' }
    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $OutputDirectory = if ($isWindowsPlatform) { 'C:\work\tracemap-summary' } else { Join-Path ([IO.Path]::GetTempPath()) 'tracemap-summary' }
    }
    $summaryRoot = [IO.Path]::GetFullPath($OutputDirectory)
    if ((Test-WithinPath $summaryRoot $reviewRoot) -or (Test-WithinPath $summaryRoot $repoRoot)) { throw 'SummaryOutputUnsafe' }

    $factsPath = Join-Path $reviewRoot 'scan/facts.ndjson'
    $manifestPath = Join-Path $reviewRoot 'scan/scan-manifest.json'
    if (-not (Test-Path $factsPath -PathType Leaf) -or -not (Test-Path $manifestPath -PathType Leaf)) { throw 'RetainedOutputIncomplete' }
    try { $manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json } catch { throw 'ManifestMalformed' }
    $analysisLevel = [string](Get-OptionalProperty $manifest 'analysisLevel')
    $buildStatus = [string](Get-OptionalProperty $manifest 'buildStatus')
    if ($analysisLevel -notin @('Level1SemanticAnalysis', 'Level1SemanticAnalysisReduced', 'Level3SyntaxAnalysis', 'Level3SyntaxAnalysisReduced') -or
        $buildStatus -notin @('Succeeded', 'FailedOrPartial', 'NotRun')) { throw 'ManifestMalformed' }

    [long]$tier1Count = 0
    [long]$workspaceDiagnosticCount = 0
    [long]$uncategorizedCount = 0
    [long]$legacyPrerequisiteCount = 0
    $capabilityStates = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $diagnosticCounts = [Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)

    foreach ($line in [IO.File]::ReadLines($factsPath)) {
        try { $fact = $line | ConvertFrom-Json } catch { throw 'FactsParseFailed' }
        if ([string](Get-OptionalProperty $fact 'evidenceTier') -eq 'Tier1Semantic') { $tier1Count++ }
        $factType = [string](Get-OptionalProperty $fact 'factType')
        $properties = Get-OptionalProperty $fact 'properties'
        if ($factType -eq 'AnalyzerCapabilityDiagnostic' -and
            [string](Get-OptionalProperty $properties 'capabilityCode') -eq 'CSharpSemanticCompilation') {
            $state = [string](Get-OptionalProperty $properties 'capabilityState')
            if ($state -in @('Available', 'Reduced', 'Unavailable', 'NotRequested', 'Unknown', 'NotApplicable')) {
                [void]$capabilityStates.Add($state)
            }
        }
        if ($factType -ne 'BuildEnvironmentDiagnostic' -or
            [string](Get-OptionalProperty $properties 'diagnosticKind') -ne 'workspace') { continue }

        $code = [string](Get-OptionalProperty $properties 'diagnosticCode')
        $guidance = [string](Get-OptionalProperty $properties 'guidanceCode')
        if ($code -notmatch $safeToken -or $guidance -notmatch $safeToken) { continue }
        $evidence = Get-OptionalProperty $fact 'evidence'
        $scope = Get-ScopeRole ([string](Get-OptionalProperty $evidence 'filePath'))
        Add-Count $diagnosticCounts "$scope|$code|$guidance"
        $workspaceDiagnosticCount++
        if ($code -eq 'UncategorizedWorkspaceFailure') { $uncategorizedCount++ }
        if ($code -eq 'LegacyWorkspacePrerequisitesUnresolved') { $legacyPrerequisiteCount++ }
    }

    $semanticState = if ($capabilityStates.Contains('Available')) {
        'available'
    } elseif ($capabilityStates.Contains('Reduced')) {
        'reduced'
    } elseif ($capabilityStates.Contains('Unavailable')) {
        'unavailable'
    } else {
        'unknown'
    }
    $nextAction = if ($uncategorizedCount -gt 0) {
        'classify-bounded-workspace-failure'
    } elseif ($legacyPrerequisiteCount -gt 0) {
        'inspect-compatible-legacy-workspace'
    } elseif ($workspaceDiagnosticCount -gt 0) {
        'follow-typed-workspace-guidance'
    } elseif ($semanticState -eq 'available' -and $tier1Count -gt 0) {
        'compare-semantic-evidence'
    } else {
        'inspect-capability-gaps'
    }

    $workspaceState = if ($analysisLevel -eq 'Level1SemanticAnalysis' -and $buildStatus -eq 'Succeeded') {
        'completed'
    } else {
        'partial'
    }
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($line in @(
        "focused-webforms-workspace=$workspaceState",
        'failureCode=none',
        "tracemapHead=$($TraceMapHead.ToLowerInvariant())",
        "analysisLevel=$analysisLevel",
        "buildStatus=$buildStatus",
        "semanticCompilation=$semanticState",
        "tier1FactCount=$($tier1Count.ToString($culture))",
        "workspaceDiagnosticCount=$($workspaceDiagnosticCount.ToString($culture))",
        "uncategorizedWorkspaceFailureCount=$($uncategorizedCount.ToString($culture))",
        "legacyWorkspacePrerequisitesUnresolvedCount=$($legacyPrerequisiteCount.ToString($culture))"
    )) { $lines.Add($line) }
    foreach ($row in @($diagnosticCounts.GetEnumerator() | Sort-Object Name)) {
        $lines.Add("workspaceDiagnostic=$($row.Key)|count=$($row.Value.ToString($culture))")
    }
    $lines.Add("nextAction=$nextAction")
    $lines.Add('nonClaim=legacy-msbuild-success-does-not-prove-roslyn-workspace-admission')

    New-Item -ItemType Directory -Path $summaryRoot -Force | Out-Null
    $summaryName = "focused-webforms-workspace-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff', $culture)).txt"
    [IO.File]::WriteAllLines((Join-Path $summaryRoot $summaryName), $lines, [Text.UTF8Encoding]::new($false))
    'focused-webforms-workspace-summary-file=created'
    'summaryDirectory=tracemap-summary'
    "summaryFile=$summaryName"
}
catch {
    $known = @('TraceMapHeadInvalid', 'WorkspaceScopeInvalid', 'RetainedOutputUnavailable', 'SummaryOutputUnsafe', 'RetainedOutputIncomplete', 'ManifestMalformed', 'FactsParseFailed')
    $classification = if ($_.Exception.Message -in $known) { $_.Exception.Message } else { 'UnexpectedFailure' }
    throw "FocusedWebFormsWorkspaceSummaryFailed:$classification"
}
