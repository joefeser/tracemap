using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record LegacyBaselineCreateOptions(
    string ScanOutputPath,
    string Label,
    string Purpose,
    string OutputPath,
    bool DryRun = false,
    DateTimeOffset? CreatedAt = null,
    bool LocalOnly = false,
    bool PublicSource = false,
    string? RuleCatalogPath = null);

public sealed record LegacyBaselineValidateOptions(string ManifestPath);

public sealed record LegacyBaselineCompareOptions(
    string BaselineManifestPath,
    string CandidateManifestPath,
    string OutputPath,
    DateTimeOffset? GeneratedAt = null,
    string? MigrationMapPath = null,
    bool DryRun = false);

public sealed record LegacyBaselineCreateResult(
    LegacyBaselineManifest Manifest,
    LegacyBaselineSafetyValidation Validation,
    string? ManifestPath,
    string? SummaryPath);

public sealed record LegacyBaselineValidateResult(LegacyBaselineManifest Manifest, LegacyBaselineSafetyValidation Validation);

public sealed record LegacyBaselineCompareResult(
    LegacyBaselineComparison Comparison,
    LegacyBaselineSafetyValidation Validation,
    string? JsonPath,
    string? MarkdownPath);

public sealed record LegacyBaselineManifest(
    string SchemaVersion,
    string BaselineId,
    string BaselinePurpose,
    string CreatedAt,
    LegacyBaselineSample Sample,
    LegacyBaselineSafety Safety,
    LegacyBaselineScan Scan,
    SortedDictionary<string, LegacyBaselineExtractor> Extractors,
    LegacyBaselineCounts Counts,
    LegacyBaselineCoverage Coverage,
    LegacyBaselineCoverageSnapshot CoverageSnapshot,
    SortedDictionary<string, LegacyBaselineArtifact> Artifacts,
    IReadOnlyList<string> KnownGaps,
    IReadOnlyList<string> Limitations);

public sealed record LegacyBaselineSample(
    string Label,
    string IdentityKind,
    string? RepoIdentityHash,
    LegacyBaselineCommitIdentity CommitIdentity);

public sealed record LegacyBaselineCommitIdentity(string Kind, string? Value);

public sealed record LegacyBaselineSafety(
    string Classification,
    string RedactionProfile,
    IReadOnlyList<string> RejectedReasons,
    IReadOnlyList<string> Limitations);

public sealed record LegacyBaselineScan(
    string TraceMapVersion,
    string ScanStartedAt,
    string CoverageLabel,
    string BuildStatus,
    string ScanStatus,
    bool Partial,
    bool Truncated,
    bool Timeout,
    bool Deferred);

public sealed record LegacyBaselineExtractor(string Version);

public sealed record LegacyBaselineCounts(
    int FactsTotal,
    int GapsTotal,
    SortedDictionary<string, int> ByFactType,
    SortedDictionary<string, int> ByRuleId,
    SortedDictionary<string, int> ByEvidenceTier,
    SortedDictionary<string, int> ByExtractor,
    SortedDictionary<string, int> BySurface,
    SortedDictionary<string, int> ByKnownGap,
    SortedDictionary<string, int> ByPathClass,
    SortedDictionary<string, int> ByFileExtension,
    SortedDictionary<string, int> ByLanguage);

public sealed record LegacyBaselineCoverage(
    int SemanticFacts,
    int StructuralFacts,
    int SyntaxOrTextualFacts,
    int UnknownOrGapFacts);

public sealed record LegacyBaselineCoverageSnapshot(
    IReadOnlyList<LegacyRuleCoverage> Rules,
    IReadOnlyList<LegacyFactCoverage> Facts,
    IReadOnlyList<LegacySurfaceCoverage> Surfaces,
    LegacyFallbackCoverage FallbackCounts);

public sealed record LegacyRuleCoverage(
    string RuleId,
    SortedDictionary<string, int> EvidenceTiers,
    IReadOnlyList<string> ExtractorVersions,
    IReadOnlyList<string> Limitations);

public sealed record LegacyFactCoverage(
    string FactType,
    int Count,
    SortedDictionary<string, int> EvidenceTiers,
    IReadOnlyList<string> RuleIds);

public sealed record LegacySurfaceCoverage(
    string Surface,
    string Status,
    int Count,
    IReadOnlyList<string> RuleIds);

public sealed record LegacyFallbackCoverage(
    int SemanticFacts,
    int StructuralFacts,
    int SyntaxOrTextualFacts,
    int UnknownOrGapFacts);

public sealed record LegacyBaselineArtifact(bool Present, string SizeBucket, string? Hash);

public sealed record LegacyBaselineSafetyValidation(
    string Classification,
    IReadOnlyList<LegacyBaselineSafetyDiagnostic> Diagnostics);

public sealed record LegacyBaselineSafetyDiagnostic(string Category, string FilePath);

public sealed record LegacyBaselineComparison(
    string SchemaVersion,
    string BaselineId,
    string CandidateId,
    string GeneratedAt,
    string OverallStatus,
    LegacySchemaCompatibility SchemaCompatibility,
    LegacyComparisonDimensions Dimensions,
    IReadOnlyList<LegacyReviewNeeded> ReviewNeeded,
    IReadOnlyList<string> Limitations);

public sealed record LegacySchemaCompatibility(string Status, string? MigrationMap);

public sealed record LegacyComparisonDimensions(
    IReadOnlyList<LegacyComparisonRow> Totals,
    IReadOnlyList<LegacyComparisonRow> ByRuleId,
    IReadOnlyList<LegacyComparisonRow> ByFactType,
    IReadOnlyList<LegacyComparisonRow> ByEvidenceTier,
    IReadOnlyList<LegacyComparisonRow> ByExtractor,
    IReadOnlyList<LegacyComparisonRow> BySurface,
    IReadOnlyList<LegacyComparisonRow> ByKnownGap,
    IReadOnlyList<LegacyComparisonRow> Coverage);

public sealed record LegacyComparisonRow(
    string Dimension,
    string Name,
    int? BaselineCount,
    int? CandidateCount,
    string Movement,
    string RuleId,
    string EvidenceTier);

public sealed record LegacyReviewNeeded(string Category, string Reason, string RuleId);

public static class LegacyBaselineArtifacts
{
    public const string ManifestSchemaVersion = "legacy-baseline-manifest.v1";
    public const string ComparisonSchemaVersion = "legacy-baseline-comparison.v1";
    public const string MigrationMapSchemaVersion = "legacy-baseline-migration-map.v1";
    public const string RedactedManifestRuleId = "legacy.baseline.redacted-manifest.v1";
    public const string CoverageSnapshotRuleId = "legacy.baseline.coverage-snapshot.v1";
    public const string RegressionComparisonRuleId = "legacy.baseline.regression-comparison.v1";
    public const string SafetyValidationRuleId = "legacy.baseline.safety-validation.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex NeutralLabelPattern = new("^[a-z0-9][a-z0-9-]{2,63}$", RegexOptions.Compiled);
    private static readonly Regex UriSchemePattern = new("[a-zA-Z][a-zA-Z0-9+.-]*://", RegexOptions.Compiled);
    private static readonly Regex WindowsDrivePattern = new("^[a-zA-Z]:[\\\\/]", RegexOptions.Compiled);
    private static readonly Regex HostLikePattern = new(@"(^|[.-])(?:com|org|net|io|dev|tools|local)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ShaPattern = new("^[a-fA-F0-9]{7,64}$", RegexOptions.Compiled);
    private static readonly Regex RuleCatalogIdPattern = new(@"^\s*-\s+id:\s*(?<id>[A-Za-z0-9_.-]+)\s*$", RegexOptions.Compiled);

    private static readonly IReadOnlyList<(Regex Pattern, string Category)> UnsafePatterns =
    [
        (new Regex(@"(\x2FUsers\x2F|/home/|/private/var/|[A-Za-z]:\\|\\\\)", RegexOptions.Compiled), "absolute-path"),
        (new Regex(@"(\bhttps?://|\bssh://|\bgit@|\.git\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase), "remote-or-url"),
        (new Regex(@"\b(select\s+.+\s+from|insert\s+into|update\s+\w+\s+set|delete\s+from|create\s+table)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline), "raw-sql"),
        (new Regex(@"\b(connectionString|Server=|Data Source=|User Id=|Password=|Pwd=|Initial Catalog=)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "config-or-connection-string"),
        (new Regex(@"\b(password|passwd|pwd|secret|token|credential)\s*[:=]", RegexOptions.Compiled | RegexOptions.IgnoreCase), "secret-or-credential"),
        (new Regex(@"-----BEGIN (?:RSA |EC |OPENSSH |)PRIVATE KEY-----", RegexOptions.Compiled), "secret-or-credential"),
        (new Regex(@"\b(public|private|internal|protected)\s+(class|record|struct|interface|void|string|int)\b", RegexOptions.Compiled), "source-like-snippet")
    ];

    private static readonly IReadOnlyList<string> ImportantSurfaces =
    [
        "csharp",
        "ui-events",
        "http",
        "wcf-service-reference",
        "sql-data",
        "config",
        "packages",
        "build-environment",
        "other"
    ];

    public static async Task<LegacyBaselineCreateResult> CreateAsync(LegacyBaselineCreateOptions options, CancellationToken cancellationToken = default)
    {
        ValidateLabel(options.Label);
        ValidatePurpose(options.Purpose);
        ValidateInputBoundary(options.ScanOutputPath);

        var createdAt = ToYearMonth(options.CreatedAt ?? DateTimeOffset.UtcNow);
        var baselineId = BaselineId(options.Label, options.Purpose, createdAt);
        var manifestPath = Path.Combine(options.ScanOutputPath, "scan-manifest.json");
        var factsPath = Path.Combine(options.ScanOutputPath, "facts.ndjson");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("scan-manifest.json is required.", manifestPath);
        }

        if (!File.Exists(factsPath))
        {
            throw new FileNotFoundException("facts.ndjson is required.", factsPath);
        }

        var scanManifest = await ReadScanManifestAsync(manifestPath, cancellationToken);
        var facts = await ReadFactsAsync(factsPath, cancellationToken);
        var catalogIds = ReadRuleCatalog(options.RuleCatalogPath);
        var unknownRuleIds = facts
            .Select(fact => fact.RuleId)
            .Where(ruleId => !catalogIds.Contains(ruleId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal)
            .ToArray();
        if (unknownRuleIds.Length > 0)
        {
            throw new InvalidOperationException($"Observed rule IDs are absent from the rule catalog: {string.Join(", ", unknownRuleIds)}.");
        }

        var safeGapCodes = scanManifest.KnownGaps
            .Concat(facts.Where(fact => fact.FactType == FactTypes.AnalysisGap).Select(fact => SanitizedGapCode(fact.Properties.GetValueOrDefault("message") ?? fact.FactType)))
            .Select(SanitizedGapCode)
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        var artifactMap = BuildInputArtifacts(options.ScanOutputPath);
        var classification = options.LocalOnly ? "local-only" : "public-safe";
        var safetyLimitations = options.LocalOnly
            ? new[] { "Local-only baseline output must remain under ignored .tmp/legacy-baselines storage." }
            : new[] { "Public-safe classification is counts-only and still depends on reviewer judgment before promotion." };
        var limitations = BuildLimitations(scanManifest, safeGapCodes);
        var counts = BuildCounts(facts, safeGapCodes);
        var coverage = BuildCoverage(counts.ByEvidenceTier);
        var snapshot = BuildCoverageSnapshot(facts, counts.BySurface, counts.ByFileExtension);
        var extractors = facts
            .GroupBy(fact => fact.Evidence.ExtractorId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new LegacyBaselineExtractor(group.Select(fact => fact.Evidence.ExtractorVersion).OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? "unknown"),
                StringComparer.Ordinal);

        var commitIdentity = options.PublicSource && ShaPattern.IsMatch(scanManifest.CommitSha)
            ? new LegacyBaselineCommitIdentity("public-sha", scanManifest.CommitSha.ToLowerInvariant())
            : new LegacyBaselineCommitIdentity("category-only", null);

        var manifest = new LegacyBaselineManifest(
            ManifestSchemaVersion,
            baselineId,
            options.Purpose,
            createdAt,
            new LegacyBaselineSample(
                options.Label,
                options.PublicSource ? "public-repo-sha" : "neutral-label",
                options.PublicSource && !string.IsNullOrWhiteSpace(scanManifest.RemoteUrl)
                    ? RedactionHash("repo-identity", options.Label, scanManifest.RemoteUrl)
                    : null,
                commitIdentity),
            new LegacyBaselineSafety(classification, "counts-only", [], safetyLimitations),
            new LegacyBaselineScan(
                scanManifest.ScannerVersion,
                ToYearMonth(scanManifest.ScannedAt),
                scanManifest.AnalysisLevel,
                scanManifest.BuildStatus,
                "completed",
                IsPartial(scanManifest, safeGapCodes),
                safeGapCodes.Any(code => code.Contains("Truncated", StringComparison.OrdinalIgnoreCase)),
                safeGapCodes.Any(code => code.Contains("Timeout", StringComparison.OrdinalIgnoreCase)),
                safeGapCodes.Any(code => code.Contains("Deferred", StringComparison.OrdinalIgnoreCase))),
            new SortedDictionary<string, LegacyBaselineExtractor>(extractors, StringComparer.Ordinal),
            counts,
            coverage,
            snapshot,
            artifactMap,
            safeGapCodes,
            limitations);

        var summary = RenderSummary(manifest);
        manifest.Artifacts["baselineSummary"] = new LegacyBaselineArtifact(true, SizeBucket(Encoding.UTF8.GetByteCount(summary)), Sha256(summary));
        var manifestText = Serialize(manifest);
        var validation = ValidateText(manifestText, "baseline-manifest.json", manifest.Safety.Classification);
        if (validation.Diagnostics.Count > 0)
        {
            manifest = manifest with
            {
                Safety = manifest.Safety with
                {
                    Classification = "rejected",
                    RejectedReasons = validation.Diagnostics.Select(d => d.Category).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
                }
            };
            manifestText = Serialize(manifest);
            validation = validation with { Classification = "rejected" };
        }

        if (options.DryRun)
        {
            return new LegacyBaselineCreateResult(manifest, validation, null, null);
        }

        ValidateOutputBoundary(options.OutputPath, manifest.Safety.Classification);
        Directory.CreateDirectory(options.OutputPath);
        var outputManifestPath = Path.Combine(options.OutputPath, "baseline-manifest.json");
        var outputSummaryPath = Path.Combine(options.OutputPath, "baseline-summary.md");
        await File.WriteAllTextAsync(outputManifestPath, manifestText, cancellationToken);
        await File.WriteAllTextAsync(outputSummaryPath, RenderSummary(manifest), cancellationToken);
        return new LegacyBaselineCreateResult(manifest, validation, outputManifestPath, outputSummaryPath);
    }

    public static async Task<LegacyBaselineValidateResult> ValidateAsync(LegacyBaselineValidateOptions options, CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(options.ManifestPath, cancellationToken);
        var manifest = JsonSerializer.Deserialize<LegacyBaselineManifest>(text, ReadOptions)
            ?? throw new InvalidOperationException("Could not parse baseline manifest.");
        var validation = ValidateText(text, Path.GetFileName(options.ManifestPath), manifest.Safety.Classification);
        return new LegacyBaselineValidateResult(manifest, validation);
    }

    public static async Task<LegacyBaselineCompareResult> CompareAsync(LegacyBaselineCompareOptions options, CancellationToken cancellationToken = default)
    {
        var baseline = await ReadBaselineManifestAsync(options.BaselineManifestPath, cancellationToken);
        var candidate = await ReadBaselineManifestAsync(options.CandidateManifestPath, cancellationToken);
        var migrationMap = await ReadMigrationMapAsync(options.MigrationMapPath, cancellationToken);
        var migrationMapApplies = migrationMap is not null
            && string.Equals(migrationMap.FromBaselineSchema, baseline.SchemaVersion, StringComparison.Ordinal)
            && string.Equals(migrationMap.ToCandidateSchema, candidate.SchemaVersion, StringComparison.Ordinal);
        var generatedAt = ToYearMonth(options.GeneratedAt ?? DateTimeOffset.UtcNow);
        var rows = new List<LegacyComparisonRow>();
        var review = new List<LegacyReviewNeeded>();

        var schemaCompatible = migrationMap is null
            ? string.Equals(baseline.SchemaVersion, candidate.SchemaVersion, StringComparison.Ordinal)
            : migrationMapApplies;
        if (!schemaCompatible)
        {
            rows.Add(Row("schema", migrationMap is null ? "schemaVersion" : "migrationMap", null, null, "not-comparable"));
            review.Add(new LegacyReviewNeeded(
                "schema",
                migrationMap is null
                    ? "Schema versions differ without a migration map."
                    : "Migration map schema pair does not match compared manifests.",
                RegressionComparisonRuleId));
        }

        rows.Add(Row("totals", "factsTotal", baseline.Counts.FactsTotal, candidate.Counts.FactsTotal));
        rows.Add(Row("totals", "gapsTotal", baseline.Counts.GapsTotal, candidate.Counts.GapsTotal));
        rows.AddRange(CompareMap("byRuleId", ApplyRenames(baseline.Counts.ByRuleId, migrationMapApplies ? migrationMap?.RuleIdRenames : null, r => r.FromRuleId, r => r.ToRuleId), candidate.Counts.ByRuleId));
        rows.AddRange(CompareMap("byFactType", ApplyRenames(baseline.Counts.ByFactType, migrationMapApplies ? migrationMap?.FactTypeRenames : null, r => r.FromFactType, r => r.ToFactType), candidate.Counts.ByFactType));
        rows.AddRange(CompareMap("byEvidenceTier", baseline.Counts.ByEvidenceTier, candidate.Counts.ByEvidenceTier));
        rows.AddRange(CompareMap("byExtractor", baseline.Counts.ByExtractor, candidate.Counts.ByExtractor));
        rows.AddRange(CompareMap("bySurface", baseline.Counts.BySurface, candidate.Counts.BySurface));
        rows.AddRange(CompareMap("byKnownGap", baseline.Counts.ByKnownGap, candidate.Counts.ByKnownGap));
        rows.Add(CompareLabel("coverage", "coverageLabel", baseline.Scan.CoverageLabel, candidate.Scan.CoverageLabel));
        rows.Add(CompareLabel("coverage", "buildStatus", baseline.Scan.BuildStatus, candidate.Scan.BuildStatus));
        rows.Add(CompareLabel("coverage", "schemaVersion", baseline.SchemaVersion, candidate.SchemaVersion, schemaCompatible ? "unchanged" : "not-comparable"));

        foreach (var row in rows)
        {
            if (row.Movement is "decrease" or "removed-category" or "coverage-changed" or "not-comparable")
            {
                review.Add(new LegacyReviewNeeded(row.Dimension, $"{row.Dimension}/{row.Name} movement is {row.Movement}.", RegressionComparisonRuleId));
            }
        }

        if (baseline.Safety.Classification == "rejected" || candidate.Safety.Classification == "rejected")
        {
            review.Add(new LegacyReviewNeeded("safety", "A compared manifest was rejected by safety validation.", SafetyValidationRuleId));
        }

        var comparison = new LegacyBaselineComparison(
            ComparisonSchemaVersion,
            baseline.BaselineId,
            candidate.BaselineId,
            generatedAt,
            review.Count == 0 ? "ok" : "review-needed",
            new LegacySchemaCompatibility(schemaCompatible ? "comparable" : "not-comparable", migrationMapApplies ? "provided" : options.MigrationMapPath is null ? null : "not-applicable"),
            new LegacyComparisonDimensions(
                rows.Where(row => row.Dimension == "totals").OrderRows(),
                rows.Where(row => row.Dimension == "byRuleId").OrderRows(),
                rows.Where(row => row.Dimension == "byFactType").OrderRows(),
                rows.Where(row => row.Dimension == "byEvidenceTier").OrderRows(),
                rows.Where(row => row.Dimension == "byExtractor").OrderRows(),
                rows.Where(row => row.Dimension == "bySurface").OrderRows(),
                rows.Where(row => row.Dimension == "byKnownGap").OrderRows(),
                rows.Where(row => row.Dimension == "coverage").OrderRows()),
            review
                .GroupBy(item => $"{item.Category}\n{item.Reason}", StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => item.Category, StringComparer.Ordinal)
                .ThenBy(item => item.Reason, StringComparer.Ordinal)
                .ToArray(),
            [
                "Comparison reports changed static evidence counts only.",
                "Reduced coverage or schema changes require human review before interpreting movement."
            ]);

        var json = Serialize(comparison);
        var markdown = RenderComparison(comparison);
        var validation = ValidateText(json + "\n" + markdown, "comparison", "local-only");
        if (options.DryRun)
        {
            return new LegacyBaselineCompareResult(comparison, validation, null, null);
        }

        ValidateOutputBoundary(options.OutputPath, "local-only");
        Directory.CreateDirectory(options.OutputPath);
        var jsonPath = Path.Combine(options.OutputPath, "comparison.json");
        var markdownPath = Path.Combine(options.OutputPath, "comparison.md");
        await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
        await File.WriteAllTextAsync(markdownPath, markdown, cancellationToken);
        return new LegacyBaselineCompareResult(comparison, validation, jsonPath, markdownPath);
    }

    public static string BaselineId(string label, string purpose, string yearMonth)
    {
        return $"{label}__{purpose}__{yearMonth}";
    }

    public static string RedactionHash(string fieldName, string label, string value)
    {
        var builder = new StringBuilder();
        builder.AppendLine("legacy-baseline");
        AppendLengthPrefixed(builder, "field", fieldName);
        AppendLengthPrefixed(builder, "label", label);
        AppendLengthPrefixed(builder, "value", value);
        return Sha256(builder.ToString());
    }

    public static LegacyBaselineSafetyValidation ValidateText(string text, string filePath, string requestedClassification)
    {
        var diagnostics = UnsafePatterns
            .Where(pattern => pattern.Pattern.IsMatch(text))
            .Select(pattern => new LegacyBaselineSafetyDiagnostic(pattern.Category, filePath))
            .GroupBy(diagnostic => diagnostic.Category, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(diagnostic => diagnostic.Category, StringComparer.Ordinal)
            .ToArray();
        return new LegacyBaselineSafetyValidation(diagnostics.Length == 0 ? requestedClassification : "rejected", diagnostics);
    }

    private static void ValidateLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || !NeutralLabelPattern.IsMatch(label))
        {
            throw new ArgumentException("Baseline label must be a neutral lowercase slug.");
        }

        if (label.Contains('/') || label.Contains('\\') || label.Contains('@') || label.Contains('~')
            || label.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            || UriSchemePattern.IsMatch(label)
            || WindowsDrivePattern.IsMatch(label)
            || HostLikePattern.IsMatch(label)
            || label.Count(ch => ch == '-') > 5)
        {
            throw new ArgumentException("Baseline label looks like an unsafe path, remote, hostname, or private identifier.");
        }
    }

    private static void ValidatePurpose(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose) || !NeutralLabelPattern.IsMatch(purpose))
        {
            throw new ArgumentException("Baseline purpose must be a neutral lowercase slug.");
        }
    }

    private static void ValidateInputBoundary(string scanOutputPath)
    {
        var normalized = NormalizePath(scanOutputPath);
        if (normalized.Contains("/.tmp/legacy-baselines/", StringComparison.Ordinal)
            || normalized.Contains("/.tmp/legacy-codebase-validation/", StringComparison.Ordinal)
            || normalized.EndsWith("/samples/synthetic-legacy-scan", StringComparison.Ordinal)
            || normalized.Contains("/samples/synthetic-legacy-scan/", StringComparison.Ordinal))
        {
            return;
        }

        throw new ArgumentException("Baseline input must come from ignored .tmp legacy storage or the checked-in synthetic fixture.");
    }

    private static void ValidateOutputBoundary(string outputPath, string classification)
    {
        var normalized = NormalizePath(outputPath);
        if (classification == "local-only" && !normalized.Contains("/.tmp/legacy-baselines/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Local-only baseline output must stay under .tmp/legacy-baselines/.");
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/');
    }

    private static async Task<ScanManifest> ReadScanManifestAsync(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<ScanManifest>(text, ReadOptions)
            ?? throw new InvalidOperationException("Could not parse scan-manifest.json.");
    }

    private static async Task<IReadOnlyList<CodeFact>> ReadFactsAsync(string path, CancellationToken cancellationToken)
    {
        var facts = new List<CodeFact>();
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            facts.Add(JsonSerializer.Deserialize<CodeFact>(line, ReadOptions)
                ?? throw new InvalidOperationException("Could not parse a facts.ndjson row."));
        }

        return facts;
    }

    private static async Task<LegacyBaselineManifest> ReadBaselineManifestAsync(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<LegacyBaselineManifest>(text, ReadOptions)
            ?? throw new InvalidOperationException("Could not parse baseline manifest.");
    }

    private static async Task<LegacyMigrationMap?> ReadMigrationMapAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        var map = JsonSerializer.Deserialize<LegacyMigrationMap>(text, ReadOptions)
            ?? throw new InvalidOperationException("Could not parse migration map.");
        if (!string.Equals(map.SchemaVersion, MigrationMapSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Migration map schema version is unsupported.");
        }

        return map;
    }

    private static HashSet<string> ReadRuleCatalog(string? explicitPath)
    {
        var path = explicitPath ?? FindRuleCatalog();
        if (path is null || !File.Exists(path))
        {
            throw new FileNotFoundException("rules/rule-catalog.yml could not be found.");
        }

        return File.ReadLines(path)
            .Select(line => RuleCatalogIdPattern.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? FindRuleCatalog()
    {
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(current))
        {
            var candidate = Path.Combine(current, "rules", "rule-catalog.yml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        return null;
    }

    private static SortedDictionary<string, LegacyBaselineArtifact> BuildInputArtifacts(string scanOutputPath)
    {
        var artifacts = new SortedDictionary<string, LegacyBaselineArtifact>(StringComparer.Ordinal)
        {
            ["analyzerLog"] = Artifact(Path.Combine(scanOutputPath, "logs", "analyzer.log"), hashRawContent: false),
            ["baselineSummary"] = new(false, "missing", null),
            ["factsNdjson"] = Artifact(Path.Combine(scanOutputPath, "facts.ndjson"), hashRawContent: false),
            ["indexSqlite"] = Artifact(Path.Combine(scanOutputPath, "index.sqlite"), hashRawContent: false),
            ["report"] = Artifact(Path.Combine(scanOutputPath, "report.md"), hashRawContent: false),
            ["scanManifest"] = Artifact(Path.Combine(scanOutputPath, "scan-manifest.json"), hashRawContent: false)
        };
        return artifacts;
    }

    private static LegacyBaselineArtifact Artifact(string path, bool hashRawContent)
    {
        if (!File.Exists(path))
        {
            return new LegacyBaselineArtifact(false, "missing", null);
        }

        var info = new FileInfo(path);
        return new LegacyBaselineArtifact(true, SizeBucket(info.Length), hashRawContent ? Sha256(File.ReadAllText(path)) : null);
    }

    private static LegacyBaselineCounts BuildCounts(IReadOnlyList<CodeFact> facts, IReadOnlyList<string> knownGaps)
    {
        var pathClasses = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var extensions = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var languages = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var fact in facts)
        {
            Increment(pathClasses, PathClass(fact.Evidence.FilePath));
            Increment(extensions, ExtensionClass(fact.Evidence.FilePath));
            Increment(languages, LanguageClass(fact.Evidence.FilePath));
        }

        return new LegacyBaselineCounts(
            facts.Count,
            facts.Count(fact => fact.FactType == FactTypes.AnalysisGap || fact.EvidenceTier == EvidenceTiers.Tier4Unknown),
            CountBy(facts, fact => fact.FactType),
            CountBy(facts, fact => fact.RuleId),
            CountBy(facts, fact => fact.EvidenceTier),
            CountBy(facts, fact => fact.Evidence.ExtractorId),
            CountBy(facts, SurfaceForFact),
            knownGaps.GroupBy(gap => gap, StringComparer.Ordinal).ToSortedCountDictionary(),
            pathClasses,
            extensions,
            languages);
    }

    private static LegacyBaselineCoverage BuildCoverage(IReadOnlyDictionary<string, int> byEvidenceTier)
    {
        return new LegacyBaselineCoverage(
            byEvidenceTier.GetValueOrDefault(EvidenceTiers.Tier1Semantic),
            byEvidenceTier.GetValueOrDefault(EvidenceTiers.Tier2Structural),
            byEvidenceTier.GetValueOrDefault(EvidenceTiers.Tier3SyntaxOrTextual),
            byEvidenceTier.GetValueOrDefault(EvidenceTiers.Tier4Unknown));
    }

    private static LegacyBaselineCoverageSnapshot BuildCoverageSnapshot(
        IReadOnlyList<CodeFact> facts,
        IReadOnlyDictionary<string, int> bySurface,
        IReadOnlyDictionary<string, int> byExtension)
    {
        var ruleCoverage = facts
            .GroupBy(fact => fact.RuleId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new LegacyRuleCoverage(
                group.Key,
                CountBy(group, fact => fact.EvidenceTier),
                group.Select(fact => fact.Evidence.ExtractorVersion).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ["Counts-only snapshot; rule limitations are documented in rules/rule-catalog.yml."]))
            .ToArray();
        var factCoverage = facts
            .GroupBy(fact => fact.FactType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new LegacyFactCoverage(
                group.Key,
                group.Count(),
                CountBy(group, fact => fact.EvidenceTier),
                group.Select(fact => fact.RuleId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()))
            .ToArray();

        var surfaces = ImportantSurfaces
            .Select(surface => new LegacySurfaceCoverage(
                surface,
                SurfaceStatus(surface, bySurface.GetValueOrDefault(surface), byExtension),
                bySurface.GetValueOrDefault(surface),
                facts.Where(fact => SurfaceForFact(fact) == surface).Select(fact => fact.RuleId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()))
            .ToArray();

        var coverage = BuildCoverage(CountBy(facts, fact => fact.EvidenceTier));
        return new LegacyBaselineCoverageSnapshot(
            ruleCoverage,
            factCoverage,
            surfaces,
            new LegacyFallbackCoverage(coverage.SemanticFacts, coverage.StructuralFacts, coverage.SyntaxOrTextualFacts, coverage.UnknownOrGapFacts));
    }

    private static IReadOnlyList<string> BuildLimitations(ScanManifest manifest, IReadOnlyList<string> safeGapCodes)
    {
        var limitations = new SortedSet<string>(StringComparer.Ordinal)
        {
            "Baseline manifest stores aggregate counts only and omits raw facts, source snippets, raw SQL, config values, remotes, analyzer logs, and local paths.",
            "Baseline comparison measures static evidence movement only."
        };

        if (!string.Equals(manifest.BuildStatus, "Succeeded", StringComparison.Ordinal))
        {
            limitations.Add("Build status is not succeeded; baseline is partial.");
        }

        if (!string.Equals(manifest.AnalysisLevel, "Level1SemanticAnalysis", StringComparison.Ordinal))
        {
            limitations.Add("Semantic coverage is reduced or unavailable; syntax/config fallback counts are preserved separately.");
        }

        foreach (var gap in safeGapCodes)
        {
            limitations.Add($"Known gap category preserved: {gap}.");
        }

        return limitations.ToArray();
    }

    private static bool IsPartial(ScanManifest manifest, IReadOnlyList<string> gapCodes)
    {
        return !string.Equals(manifest.BuildStatus, "Succeeded", StringComparison.Ordinal)
            || !string.Equals(manifest.AnalysisLevel, "Level1SemanticAnalysis", StringComparison.Ordinal)
            || gapCodes.Count > 0;
    }

    private static string SurfaceForFact(CodeFact fact)
    {
        var combined = $"{fact.FactType} {fact.RuleId} {fact.Evidence.ExtractorId}";
        if (ContainsAny(combined, "winforms", "webforms", "ui", "event"))
        {
            return "ui-events";
        }

        if (ContainsAny(combined, "wcf", "service-reference", "servicereference"))
        {
            return "wcf-service-reference";
        }

        if (ContainsAny(combined, "http", "route", "endpoint"))
        {
            return "http";
        }

        if (ContainsAny(combined, "sql", "database", "dapper", "dbcontext", "dbset"))
        {
            return "sql-data";
        }

        if (ContainsAny(combined, "config", "connectionstring"))
        {
            return "config";
        }

        if (ContainsAny(combined, "package"))
        {
            return "packages";
        }

        if (ContainsAny(combined, "buildstatus", "analysisgap", "targetframework", "project", "solution", "manifest", "inventory"))
        {
            return "build-environment";
        }

        if (ContainsAny(combined, "csharp", "method", "property", "field", "type", "call", "symbol"))
        {
            return "csharp";
        }

        return "other";
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string SurfaceStatus(string surface, int count, IReadOnlyDictionary<string, int> byExtension)
    {
        if (count > 0)
        {
            return "observed";
        }

        return surface switch
        {
            "csharp" when byExtension.GetValueOrDefault(".cs") > 0 => "not-observed",
            "http" when byExtension.GetValueOrDefault(".cs") > 0 => "not-observed",
            "config" when byExtension.Keys.Any(ext => ext is ".config" or ".json" or ".yml" or ".yaml") => "not-observed",
            "sql-data" when byExtension.GetValueOrDefault(".sql") > 0 => "not-observed",
            "packages" when byExtension.Keys.Any(ext => ext is ".csproj" or ".props" or ".targets") => "not-observed",
            "build-environment" when byExtension.Keys.Any(ext => ext is ".sln" or ".csproj") => "not-observed",
            "ui-events" or "wcf-service-reference" => "not-in-scope",
            "other" => "not-observed",
            _ => "unknown"
        };
    }

    private static string PathClass(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == ".")
        {
            return "repo-root";
        }

        if (path.Contains('\\') || Path.IsPathRooted(path))
        {
            return "unsafe-path-omitted";
        }

        var first = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "file";
        return first switch
        {
            "src" => "source-tree",
            "test" or "tests" => "test-tree",
            "samples" => "sample-tree",
            _ => "relative-file"
        };
    }

    private static string ExtensionClass(string path)
    {
        var ext = Path.GetExtension(path);
        return string.IsNullOrWhiteSpace(ext) ? "none" : ext.ToLowerInvariant();
    }

    private static string LanguageClass(string path)
    {
        return ExtensionClass(path) switch
        {
            ".cs" or ".csproj" or ".sln" => "csharp",
            ".sql" => "sql",
            ".json" or ".config" or ".xml" or ".yml" or ".yaml" => "config",
            _ => "other"
        };
    }

    private static string SanitizedGapCode(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var stop = trimmed.IndexOfAny([':', ';', '\n', '\r']);
        var code = stop >= 0 ? trimmed[..stop] : trimmed;
        code = Regex.Replace(code, @"[^A-Za-z0-9_.-]", "");
        return code.Length == 0 ? "UnknownAnalysisGap" : code;
    }

    private static SortedDictionary<string, int> CountBy(IEnumerable<CodeFact> facts, Func<CodeFact, string> selector)
    {
        return facts.GroupBy(selector, StringComparer.Ordinal).ToSortedCountDictionary();
    }

    private static void Increment(SortedDictionary<string, int> map, string key)
    {
        map[key] = map.GetValueOrDefault(key) + 1;
    }

    private static IReadOnlyList<LegacyComparisonRow> CompareMap(
        string dimension,
        IReadOnlyDictionary<string, int> baseline,
        IReadOnlyDictionary<string, int> candidate)
    {
        var names = baseline.Keys.Concat(candidate.Keys).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal);
        return names.Select(name =>
        {
            var hasBaseline = baseline.TryGetValue(name, out var baselineCount);
            var hasCandidate = candidate.TryGetValue(name, out var candidateCount);
            if (!hasBaseline)
            {
                return Row(dimension, name, null, candidateCount, "new-category");
            }

            if (!hasCandidate)
            {
                return Row(dimension, name, baselineCount, null, "removed-category");
            }

            return Row(dimension, name, baselineCount, candidateCount);
        }).ToArray();
    }

    private static LegacyComparisonRow Row(string dimension, string name, int? baselineCount, int? candidateCount, string? movement = null)
    {
        return new LegacyComparisonRow(
            dimension,
            name,
            baselineCount,
            candidateCount,
            movement ?? Movement(baselineCount.GetValueOrDefault(), candidateCount.GetValueOrDefault()),
            RegressionComparisonRuleId,
            EvidenceTiers.Tier2Structural);
    }

    private static LegacyComparisonRow CompareLabel(string dimension, string name, string baseline, string candidate, string? fixedMovement = null)
    {
        var movement = fixedMovement ?? (string.Equals(baseline, candidate, StringComparison.Ordinal) ? "unchanged" : "coverage-changed");
        return new LegacyComparisonRow(dimension, name, null, null, movement, RegressionComparisonRuleId, EvidenceTiers.Tier4Unknown);
    }

    private static string Movement(int baseline, int candidate)
    {
        if (candidate > baseline)
        {
            return "increase";
        }

        if (candidate < baseline)
        {
            return "decrease";
        }

        return "unchanged";
    }

    private static SortedDictionary<string, int> ApplyRenames<TRename>(
        IReadOnlyDictionary<string, int> source,
        IReadOnlyList<TRename>? renames,
        Func<TRename, string> from,
        Func<TRename, string> to)
    {
        var renameMap = renames?.ToDictionary(from, to, StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            var key = renameMap.GetValueOrDefault(pair.Key, pair.Key);
            result[key] = result.GetValueOrDefault(key) + pair.Value;
        }

        return result;
    }

    private static string RenderSummary(LegacyBaselineManifest manifest)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Legacy Baseline Summary");
        builder.AppendLine();
        builder.AppendLine($"- Baseline ID: `{manifest.BaselineId}`");
        builder.AppendLine($"- Schema: `{manifest.SchemaVersion}`");
        builder.AppendLine($"- Classification: `{manifest.Safety.Classification}`");
        builder.AppendLine($"- Coverage label: `{manifest.Scan.CoverageLabel}`");
        builder.AppendLine($"- Build status: `{manifest.Scan.BuildStatus}`");
        builder.AppendLine($"- Partial: `{manifest.Scan.Partial}`");
        builder.AppendLine($"- Facts: `{manifest.Counts.FactsTotal}`");
        builder.AppendLine($"- Gaps: `{manifest.Counts.GapsTotal}`");
        builder.AppendLine();
        builder.AppendLine("## Rule Counts");
        foreach (var pair in manifest.Counts.ByRuleId)
        {
            builder.AppendLine($"- `{pair.Key}`: `{pair.Value}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Limitations");
        foreach (var limitation in manifest.Limitations)
        {
            builder.AppendLine($"- {limitation}");
        }

        return builder.ToString();
    }

    private static string RenderComparison(LegacyBaselineComparison comparison)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Legacy Baseline Comparison");
        builder.AppendLine();
        builder.AppendLine($"- Baseline ID: `{comparison.BaselineId}`");
        builder.AppendLine($"- Candidate ID: `{comparison.CandidateId}`");
        builder.AppendLine($"- Overall status: `{comparison.OverallStatus}`");
        builder.AppendLine("- Claim boundary: changed static evidence counts only.");
        builder.AppendLine();
        builder.AppendLine("## Movements");
        builder.AppendLine("| Dimension | Name | Baseline | Candidate | Movement |");
        builder.AppendLine("| --- | --- | ---: | ---: | --- |");
        foreach (var row in comparison.Dimensions.Totals
            .Concat(comparison.Dimensions.ByRuleId)
            .Concat(comparison.Dimensions.ByFactType)
            .Concat(comparison.Dimensions.ByEvidenceTier)
            .Concat(comparison.Dimensions.ByExtractor)
            .Concat(comparison.Dimensions.BySurface)
            .Concat(comparison.Dimensions.ByKnownGap)
            .Concat(comparison.Dimensions.Coverage)
            .OrderRows())
        {
            builder.AppendLine($"| `{Cell(row.Dimension)}` | `{Cell(row.Name)}` | {row.BaselineCount?.ToString() ?? "n/a"} | {row.CandidateCount?.ToString() ?? "n/a"} | `{row.Movement}` |");
        }

        if (comparison.ReviewNeeded.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Review Needed");
            foreach (var item in comparison.ReviewNeeded)
            {
                builder.AppendLine($"- `{Cell(item.Category)}`: {Cell(item.Reason)}");
            }
        }

        return builder.ToString();
    }

    private static string Cell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal);
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions) + "\n";
    }

    private static string ToYearMonth(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string SizeBucket(long bytes)
    {
        return bytes switch
        {
            0 => "empty",
            < 10_000 => "small",
            < 1_000_000 => "medium",
            _ => "large"
        };
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void AppendLengthPrefixed(StringBuilder builder, string key, string value)
    {
        builder.Append(key).Append(':').Append(value.Length).Append(':').Append(value).AppendLine();
    }

    private sealed record LegacyMigrationMap(
        string SchemaVersion,
        string FromBaselineSchema,
        string ToCandidateSchema,
        IReadOnlyList<LegacyRuleRename> RuleIdRenames,
        IReadOnlyList<LegacyFactTypeRename> FactTypeRenames,
        IReadOnlyList<string> Limitations);

    private sealed record LegacyRuleRename(string FromRuleId, string ToRuleId, string Reason);

    private sealed record LegacyFactTypeRename(string FromFactType, string ToFactType, string Reason);
}

internal static class LegacyBaselineEnumerableExtensions
{
    public static SortedDictionary<string, int> ToSortedCountDictionary<T>(this IEnumerable<IGrouping<string, T>> groups)
    {
        return new SortedDictionary<string, int>(
            groups
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    public static IReadOnlyList<LegacyComparisonRow> OrderRows(this IEnumerable<LegacyComparisonRow> rows)
    {
        return rows
            .OrderBy(row => row.Dimension, StringComparer.Ordinal)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ThenBy(row => row.Movement, StringComparer.Ordinal)
            .ToArray();
    }
}
