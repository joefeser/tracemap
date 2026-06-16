using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record LegacyBaselineCreateOptions(
    string ScanOutputPath,
    string Label,
    string Purpose,
    string OutputPath,
    string Classification = LegacyBaselineClassifications.PublicSafe,
    string? CreatedAt = null,
    bool DryRun = false,
    bool PublicSourceIdentity = false);

public sealed record LegacyBaselineValidateOptions(string ManifestPath);

public sealed record LegacyBaselineCompareOptions(
    string BaselineManifestPath,
    string CandidateManifestPath,
    string OutputPath,
    string? MigrationMapPath = null,
    string? GeneratedAt = null);

public sealed record LegacyBaselineCreateResult(
    LegacyBaselineManifest Manifest,
    string? ManifestPath,
    string? SummaryPath,
    IReadOnlyList<LegacyBaselineValidationDiagnostic> Diagnostics);

public sealed record LegacyBaselineValidateResult(
    string Classification,
    bool IsValid,
    IReadOnlyList<LegacyBaselineValidationDiagnostic> Diagnostics);

public sealed record LegacyBaselineCompareResult(
    LegacyBaselineComparisonDocument Comparison,
    string JsonPath,
    string MarkdownPath,
    IReadOnlyList<LegacyBaselineValidationDiagnostic> Diagnostics);

public static class LegacyBaselineClassifications
{
    public const string PublicSafe = "public-safe";
    public const string LocalOnly = "local-only";
    public const string Rejected = "rejected";
}

public static class LegacyBaselineSchemas
{
    public const string Manifest = "legacy-baseline-manifest.v1";
    public const string Comparison = "legacy-baseline-comparison.v1";
    public const string MigrationMap = "legacy-baseline-migration-map.v1";
}

public sealed record LegacyBaselineManifest(
    string SchemaVersion,
    string BaselineId,
    string BaselinePurpose,
    string CreatedAt,
    LegacyBaselineSample Sample,
    LegacyBaselineSafety Safety,
    LegacyBaselineScan Scan,
    IReadOnlyDictionary<string, LegacyBaselineExtractor> Extractors,
    LegacyBaselineCounts Counts,
    LegacyBaselineCoverageCounts Coverage,
    IReadOnlyList<LegacyBaselineRuleCoverage> RuleCoverage,
    IReadOnlyList<LegacyBaselineFactCoverage> FactCoverage,
    IReadOnlyDictionary<string, string> Surfaces,
    IReadOnlyDictionary<string, LegacyBaselineArtifact> Artifacts,
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
    IReadOnlyDictionary<string, int> ByFactType,
    IReadOnlyDictionary<string, int> ByRuleId,
    IReadOnlyDictionary<string, int> ByEvidenceTier,
    IReadOnlyDictionary<string, int> ByExtractor,
    IReadOnlyDictionary<string, int> BySurface,
    IReadOnlyDictionary<string, int> ByKnownGap);

public sealed record LegacyBaselineCoverageCounts(
    int SemanticFacts,
    int StructuralFacts,
    int SyntaxOrTextualFacts,
    int UnknownOrGapFacts);

public sealed record LegacyBaselineRuleCoverage(
    string RuleId,
    int Count,
    IReadOnlyDictionary<string, int> EvidenceTiers,
    IReadOnlyDictionary<string, string> Extractors,
    IReadOnlyList<string> Limitations);

public sealed record LegacyBaselineFactCoverage(
    string FactType,
    int Count,
    IReadOnlyDictionary<string, int> EvidenceTiers,
    IReadOnlyList<string> RuleIds);

public sealed record LegacyBaselineArtifact(
    bool Present,
    string SizeBucket,
    string? Hash);

public sealed record LegacyBaselineValidationDiagnostic(
    string Category,
    string Path,
    string RuleId,
    string Message);

public sealed record LegacyBaselineComparisonDocument(
    string SchemaVersion,
    string BaselineId,
    string CandidateId,
    string GeneratedAt,
    string OverallStatus,
    LegacyBaselineSchemaCompatibility SchemaCompatibility,
    IReadOnlyDictionary<string, IReadOnlyList<LegacyBaselineMovementRow>> Dimensions,
    IReadOnlyList<LegacyBaselineReviewNeeded> ReviewNeeded,
    IReadOnlyList<string> Limitations);

public sealed record LegacyBaselineSchemaCompatibility(
    string Status,
    string? MigrationMap,
    IReadOnlyList<LegacyBaselineExtractorCompatibility> Extractors);

public sealed record LegacyBaselineExtractorCompatibility(
    string ExtractorId,
    string? BaselineVersion,
    string? CandidateVersion,
    string Movement);

public sealed record LegacyBaselineMovementRow(
    string Category,
    string BaselineValue,
    string CandidateValue,
    string Movement,
    bool ReviewNeeded,
    string RuleId);

public sealed record LegacyBaselineReviewNeeded(
    string Category,
    string Reason,
    string RuleId);

public sealed record LegacyBaselineMigrationMap(
    string SchemaVersion,
    string FromBaselineSchema,
    string ToCandidateSchema,
    IReadOnlyList<LegacyBaselineRuleRename>? RuleIdRenames,
    IReadOnlyList<LegacyBaselineFactTypeRename>? FactTypeRenames,
    IReadOnlyList<string>? Limitations);

public sealed record LegacyBaselineRuleRename(string FromRuleId, string ToRuleId, string Reason);

public sealed record LegacyBaselineFactTypeRename(string FromFactType, string ToFactType, string Reason);

public static class LegacyBaselineArtifacts
{
    public const string ManifestFileName = "baseline-manifest.json";
    public const string SummaryFileName = "baseline-summary.md";
    public const string ComparisonJsonFileName = "comparison.json";
    public const string ComparisonMarkdownFileName = "comparison.md";

    private const string ManifestRuleId = "legacy.baseline.redacted-manifest.v1";
    private const string CoverageRuleId = "legacy.baseline.coverage-snapshot.v1";
    private const string ComparisonRuleId = "legacy.baseline.regression-comparison.v1";
    private const string SafetyRuleId = "legacy.baseline.safety-validation.v1";
    private const int MaxFactsBytes = 25 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Regex NeutralSlug = new("^[a-z0-9][a-z0-9-]{2,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UriScheme = new("[a-z][a-z0-9+.-]*://", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex WindowsDrive = new(@"(^|[^a-zA-Z])[a-zA-Z]:[\\/]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RawHostname = new(@"(^|[^a-z0-9-])([a-z0-9-]+\.)+(com|net|org|io|dev|local|internal)([^a-z0-9-]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex AbsoluteUnixPath = new(@"(^|[\s""'`=:(])/(Users|home|var|tmp|private|opt)/[^\s""'`<>]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SecretLike = new("(password|passwd|pwd|secret|token|credential|connectionstring|api[_-]?key|private key|bearer\\s+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RawSql = new("\\b(select|insert|update|delete|merge)\\b[\\s\\S]{0,80}\\b(from|into|set|where)\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SourceLike = new(@"\b(public|private|protected|internal|class|namespace|using)\b\s+[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
        ValidateCreateOptions(options);
        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var scanOutput = ResolvePath(options.ScanOutputPath, repoRoot);
        var outputPath = ResolvePath(options.OutputPath, repoRoot);
        var createdAt = NormalizeCreatedAt(options.CreatedAt, options.Classification);
        var baselineId = BuildBaselineId(options.Label, options.Purpose, createdAt);

        var diagnostics = new List<LegacyBaselineValidationDiagnostic>();
        if (!IsAllowedScanInput(scanOutput, repoRoot))
        {
            diagnostics.Add(Diagnostic("input-storage", options.ScanOutputPath, "scan output must be under an ignored local baseline directory or checked-in synthetic samples"));
        }

        if (!IsAllowedOutput(outputPath, repoRoot, options.Classification))
        {
            diagnostics.Add(Diagnostic("output-storage", options.OutputPath, "baseline output path is outside the allowed storage boundary"));
        }

        var scanManifest = ReadObject(Path.Combine(scanOutput, "scan-manifest.json"));
        var factsPath = Path.Combine(scanOutput, "facts.ndjson");
        var factsPresent = File.Exists(factsPath);
        var facts = await ReadFactsAsync(factsPath, cancellationToken);
        var factsTruncated = factsPresent && new FileInfo(factsPath).Length > MaxFactsBytes;

        var aggregation = AggregateFacts(facts);
        var knownGaps = new SortedSet<string>(ReadStringArray(scanManifest, "knownGaps"), StringComparer.Ordinal);
        if (!factsPresent)
        {
            knownGaps.Add("FactsArtifactMissing");
        }

        var knownGapCounts = aggregation.KnownGapCounts.ToSortedDictionary();
        if (!factsPresent)
        {
            knownGapCounts["FactsArtifactMissing"] = Math.Max(knownGapCounts.GetValueOrDefault("FactsArtifactMissing"), 1);
        }

        var knownGapList = knownGaps.ToArray();
        var limitations = BuildLimitations(scanManifest, factsTruncated, !factsPresent, knownGapList, aggregation);
        var safetyLimitations = new List<string>
        {
            "Baseline summaries are counts-only and omit raw facts, paths, snippets, SQL, config values, remotes, and analyzer output."
        };

        var catalogDiagnostics = ValidateRuleCatalog(aggregation.RuleCounts.Keys);
        diagnostics.AddRange(catalogDiagnostics);

        var rejectedReasons = diagnostics.Select(item => item.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var classification = rejectedReasons.Length > 0 ? LegacyBaselineClassifications.Rejected : options.Classification;
        if (classification == LegacyBaselineClassifications.LocalOnly)
        {
            safetyLimitations.Add("Local-only baselines may include comparison-useful nonpublic context in future versions and must remain under ignored storage.");
        }

        var manifest = new LegacyBaselineManifest(
            LegacyBaselineSchemas.Manifest,
            baselineId,
            options.Purpose,
            createdAt,
            BuildSample(options, scanManifest),
            new LegacyBaselineSafety(
                classification,
                "counts-only",
                rejectedReasons,
                safetyLimitations.Order(StringComparer.Ordinal).ToArray()),
            new LegacyBaselineScan(
                ReadString(scanManifest, "scannerVersion", "unknown"),
                NormalizeScanStartedAt(ReadString(scanManifest, "scannedAt", createdAt), classification),
                ReadString(scanManifest, "analysisLevel", "unknown"),
                ReadString(scanManifest, "buildStatus", "unknown"),
                ScanStatus(scanManifest, facts.Count, factsTruncated, !factsPresent),
                IsPartial(scanManifest, factsTruncated, !factsPresent),
                factsTruncated || HasKnownGap(knownGapList, "truncated"),
                HasKnownGap(knownGapList, "timeout"),
                HasKnownGap(knownGapList, "deferred")),
            aggregation.Extractors,
            new LegacyBaselineCounts(
                facts.Count,
                aggregation.GapCount,
                aggregation.FactTypeCounts,
                aggregation.RuleCounts,
                aggregation.EvidenceTierCounts,
                aggregation.ExtractorCounts,
                aggregation.SurfaceCounts,
                knownGapCounts),
            new LegacyBaselineCoverageCounts(
                aggregation.EvidenceTierCounts.GetValueOrDefault(EvidenceTiers.Tier1Semantic),
                aggregation.EvidenceTierCounts.GetValueOrDefault(EvidenceTiers.Tier2Structural),
                aggregation.EvidenceTierCounts.GetValueOrDefault(EvidenceTiers.Tier3SyntaxOrTextual),
                aggregation.EvidenceTierCounts.GetValueOrDefault(EvidenceTiers.Tier4Unknown)),
            aggregation.RuleCoverage,
            aggregation.FactCoverage,
            SurfaceStatuses(aggregation.SurfaceCounts, scanManifest, File.Exists(factsPath)),
            BuildArtifacts(scanOutput),
            knownGapList,
            limitations);

        var validation = ValidateManifest(manifest, options.OutputPath);
        diagnostics.AddRange(validation);
        if (validation.Count > 0 && manifest.Safety.Classification != LegacyBaselineClassifications.Rejected)
        {
            manifest = manifest with
            {
                Safety = manifest.Safety with
                {
                    Classification = LegacyBaselineClassifications.Rejected,
                    RejectedReasons = diagnostics.Select(item => item.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                }
            };
        }

        if (options.DryRun)
        {
            return new LegacyBaselineCreateResult(manifest, null, null, diagnostics);
        }

        if (manifest.Safety.Classification == LegacyBaselineClassifications.Rejected)
        {
            return new LegacyBaselineCreateResult(manifest, null, null, diagnostics);
        }

        Directory.CreateDirectory(outputPath);
        var summary = RenderBaselineSummary(manifest);
        var summaryPath = Path.Combine(outputPath, SummaryFileName);
        await File.WriteAllTextAsync(summaryPath, summary, cancellationToken);
        var summaryInfo = new FileInfo(summaryPath);
        var artifacts = manifest.Artifacts.ToSortedDictionary();
        artifacts["baselineSummary"] = new LegacyBaselineArtifact(true, SizeBucket(summaryInfo.Length), "sha256:" + Sha256Hex(await File.ReadAllBytesAsync(summaryPath, cancellationToken)));
        manifest = manifest with { Artifacts = artifacts };
        var manifestPath = Path.Combine(outputPath, ManifestFileName);
        await WriteJsonAsync(manifestPath, manifest, cancellationToken);
        return new LegacyBaselineCreateResult(manifest, manifestPath, summaryPath, diagnostics);
    }

    public static async Task<LegacyBaselineValidateResult> ValidateAsync(LegacyBaselineValidateOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            throw new ArgumentException("baseline validate requires --manifest <path>.", nameof(options));
        }

        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var manifestPath = ResolvePath(options.ManifestPath, repoRoot);
        var manifest = await ReadJsonAsync<LegacyBaselineManifest>(manifestPath, cancellationToken);
        var diagnostics = ValidateManifest(manifest, options.ManifestPath).ToList();
        diagnostics.AddRange(ValidateRuleCatalog(manifest.Counts.ByRuleId.Keys));
        var classification = diagnostics.Count == 0 ? manifest.Safety.Classification : LegacyBaselineClassifications.Rejected;
        return new LegacyBaselineValidateResult(classification, diagnostics.Count == 0 && classification != LegacyBaselineClassifications.Rejected, diagnostics);
    }

    public static async Task<LegacyBaselineCompareResult> CompareAsync(LegacyBaselineCompareOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.BaselineManifestPath) || string.IsNullOrWhiteSpace(options.CandidateManifestPath) || string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("baseline compare requires --baseline, --candidate, and --out.");
        }

        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var outputPath = ResolvePath(options.OutputPath, repoRoot);
        var baselinePath = ResolvePath(options.BaselineManifestPath, repoRoot);
        var candidatePath = ResolvePath(options.CandidateManifestPath, repoRoot);
        var migrationPath = options.MigrationMapPath is null ? null : ResolvePath(options.MigrationMapPath, repoRoot);
        var diagnostics = new List<LegacyBaselineValidationDiagnostic>();
        if (!IsUnderRepoRelative(outputPath, repoRoot, ".tmp/legacy-baselines"))
        {
            throw new ArgumentException("baseline compare --out must stay under ignored .tmp/legacy-baselines storage.");
        }

        var baseline = await ReadJsonAsync<LegacyBaselineManifest>(baselinePath, cancellationToken);
        var candidate = await ReadJsonAsync<LegacyBaselineManifest>(candidatePath, cancellationToken);
        diagnostics.AddRange(ValidateManifest(baseline, options.BaselineManifestPath));
        diagnostics.AddRange(ValidateManifest(candidate, options.CandidateManifestPath));
        ThrowIfFatalBaselineDiagnostics(diagnostics, "baseline compare inputs failed validation; no comparison files were written.");

        var migrationMap = migrationPath is null
            ? null
            : await ReadJsonAsync<LegacyBaselineMigrationMap>(migrationPath, cancellationToken);

        var generatedAt = NormalizeCreatedAt(options.GeneratedAt, LegacyBaselineClassifications.PublicSafe);
        var comparison = BuildComparison(baseline, candidate, migrationMap, options.MigrationMapPath, generatedAt, diagnostics);
        diagnostics.AddRange(ValidateComparison(comparison, options.OutputPath));
        ThrowIfFatalBaselineDiagnostics(diagnostics, "baseline compare output failed validation; no comparison files were written.");

        Directory.CreateDirectory(outputPath);
        var jsonPath = Path.Combine(outputPath, ComparisonJsonFileName);
        var markdownPath = Path.Combine(outputPath, ComparisonMarkdownFileName);
        await WriteJsonAsync(jsonPath, comparison, cancellationToken);
        await File.WriteAllTextAsync(markdownPath, RenderComparisonMarkdown(comparison), cancellationToken);
        return new LegacyBaselineCompareResult(comparison, jsonPath, markdownPath, diagnostics);
    }

    public static string BuildBaselineId(string label, string purpose, string createdAt)
    {
        ValidateNeutralToken(label, "label");
        ValidateNeutralToken(purpose, "purpose");
        return $"{label}__{purpose}__{createdAt}";
    }

    public static string RedactionHash(string fieldName, string label, string value)
    {
        var builder = new StringBuilder();
        AppendLengthPrefixed(builder, "legacy-baseline", "");
        AppendLengthPrefixed(builder, "field", fieldName);
        AppendLengthPrefixed(builder, "label", label);
        AppendLengthPrefixed(builder, "value", value);
        return "sha256:" + Sha256Hex(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static void ValidateCreateOptions(LegacyBaselineCreateOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ScanOutputPath))
        {
            throw new ArgumentException("baseline create requires --scan-output <path>.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("baseline create requires --out <path>.", nameof(options));
        }

        ValidateNeutralToken(options.Label, "label");
        ValidateNeutralToken(options.Purpose, "purpose");
        if (options.Classification is not LegacyBaselineClassifications.PublicSafe and not LegacyBaselineClassifications.LocalOnly)
        {
            throw new ArgumentException("baseline create --classification must be public-safe or local-only.", nameof(options));
        }
    }

    private static void ValidateNeutralToken(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !NeutralSlug.IsMatch(value)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('@', StringComparison.Ordinal)
            || value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            || value.Contains('~', StringComparison.Ordinal)
            || UriScheme.IsMatch(value)
            || WindowsDrive.IsMatch(value)
            || RawHostname.IsMatch(value)
            || value.Count(ch => ch == '-') > 5)
        {
            throw new ArgumentException($"baseline {name} must be a short neutral slug.", name);
        }
    }

    private static LegacyBaselineSample BuildSample(LegacyBaselineCreateOptions options, JsonElement scanManifest)
    {
        if (options.PublicSourceIdentity)
        {
            var repoName = ReadString(scanManifest, "repoName", "");
            var remote = ReadString(scanManifest, "remoteUrl", "");
            var commit = ReadString(scanManifest, "commitSha", "");
            if (SecretLike.IsMatch(repoName) || SecretLike.IsMatch(remote))
            {
                return new LegacyBaselineSample(options.Label, "private-category-only", null, new LegacyBaselineCommitIdentity("omitted-secret-like", null));
            }

            return new LegacyBaselineSample(
                options.Label,
                "public-repo-sha",
                string.IsNullOrWhiteSpace(repoName + remote) ? null : RedactionHash("public-repo-identity", options.Label, repoName + "\n" + remote),
                new LegacyBaselineCommitIdentity("sha", string.IsNullOrWhiteSpace(commit) ? null : commit));
        }

        return options.Classification == LegacyBaselineClassifications.LocalOnly
            ? new LegacyBaselineSample(options.Label, "neutral-label", null, new LegacyBaselineCommitIdentity("category-only", null))
            : new LegacyBaselineSample(options.Label, "private-category-only", null, new LegacyBaselineCommitIdentity("omitted-private", null));
    }

    private static LegacyBaselineAggregation AggregateFacts(IReadOnlyList<JsonElement> facts)
    {
        var factTypes = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var ruleIds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var evidenceTiers = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var extractorCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var extractorVersions = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var surfaces = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var knownGaps = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var ruleTierCounts = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.Ordinal);
        var ruleExtractors = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var factTierCounts = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.Ordinal);
        var factRules = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var gapCount = 0;

        foreach (var fact in facts)
        {
            var factType = ReadString(fact, "factType", "unknown");
            var ruleId = ReadString(fact, "ruleId", "unknown");
            var evidenceTier = ReadString(fact, "evidenceTier", EvidenceTiers.Tier4Unknown);
            var evidence = TryGetProperty(fact, "evidence", out var evidenceElement) ? evidenceElement : default;
            var extractorId = evidence.ValueKind == JsonValueKind.Object ? ReadString(evidence, "extractorId", "unknown") : "unknown";
            var extractorVersion = evidence.ValueKind == JsonValueKind.Object ? ReadString(evidence, "extractorVersion", "unknown") : "unknown";
            var surface = SurfaceFor(factType, ruleId);

            Increment(factTypes, factType);
            Increment(ruleIds, ruleId);
            Increment(evidenceTiers, evidenceTier);
            Increment(extractorCounts, extractorId);
            Increment(surfaces, surface);
            AddVersion(extractorVersions, extractorId, extractorVersion);
            IncrementNested(ruleTierCounts, ruleId, evidenceTier);
            SetNested(ruleExtractors, ruleId, extractorId, extractorVersion);
            IncrementNested(factTierCounts, factType, evidenceTier);
            AddSet(factRules, factType, ruleId);

            if (factType == FactTypes.AnalysisGap || evidenceTier == EvidenceTiers.Tier4Unknown)
            {
                gapCount++;
                var gapKind = GapKind(fact);
                Increment(knownGaps, gapKind);
            }
        }

        var extractors = extractorVersions.ToDictionary(
            item => item.Key,
            item => new LegacyBaselineExtractor(string.Join(",", item.Value.Order(StringComparer.Ordinal))),
            StringComparer.Ordinal);

        var ruleCoverage = ruleIds
            .Select(item => new LegacyBaselineRuleCoverage(
                item.Key,
                item.Value,
                ruleTierCounts.GetValueOrDefault(item.Key)?.ToSortedDictionary() ?? EmptyIntMap(),
                ruleExtractors.GetValueOrDefault(item.Key)?.ToSortedDictionary() ?? EmptyStringMap(),
                RuleLimitations(item.Key)))
            .OrderBy(item => item.RuleId, StringComparer.Ordinal)
            .ToArray();

        var factCoverage = factTypes
            .Select(item => new LegacyBaselineFactCoverage(
                item.Key,
                item.Value,
                factTierCounts.GetValueOrDefault(item.Key)?.ToSortedDictionary() ?? EmptyIntMap(),
                factRules.GetValueOrDefault(item.Key)?.Order(StringComparer.Ordinal).ToArray() ?? []))
            .OrderBy(item => item.FactType, StringComparer.Ordinal)
            .ToArray();

        return new LegacyBaselineAggregation(
            factTypes.ToSortedDictionary(),
            ruleIds.ToSortedDictionary(),
            evidenceTiers.ToSortedDictionary(),
            extractorCounts.ToSortedDictionary(),
            surfaces.ToSortedDictionary(),
            knownGaps.ToSortedDictionary(),
            extractors.ToSortedDictionary(),
            ruleCoverage,
            factCoverage,
            gapCount);
    }

    private static string GapKind(JsonElement fact)
    {
        if (TryGetProperty(fact, "properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "gapKind", "kind", "reason", "classification" })
            {
                if (TryGetProperty(properties, key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString()!;
                }
            }
        }

        return "UnknownAnalysisGap";
    }

    private static IReadOnlyDictionary<string, string> SurfaceStatuses(IReadOnlyDictionary<string, int> observed, JsonElement manifest, bool factsArtifactPresent)
    {
        var statuses = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var scoped = ScopedSurfaces(manifest, observed, factsArtifactPresent);
        foreach (var surface in ImportantSurfaces.Order(StringComparer.Ordinal))
        {
            statuses[surface] = observed.ContainsKey(surface)
                ? "observed"
                : !factsArtifactPresent ? "unknown"
                : scoped.Contains(surface) ? "not-observed" : "not-in-scope";
        }

        return statuses;
    }

    private static SortedSet<string> ScopedSurfaces(JsonElement manifest, IReadOnlyDictionary<string, int> observed, bool factsArtifactPresent)
    {
        var scoped = new SortedSet<string>(observed.Keys, StringComparer.Ordinal);
        if (!factsArtifactPresent)
        {
            return scoped;
        }

        if (ReadStringArray(manifest, "projects").Count > 0
            || ReadStringArray(manifest, "solutions").Count > 0
            || ReadStringArray(manifest, "targetFrameworks").Count > 0)
        {
            scoped.Add("build-environment");
            scoped.Add("csharp");
            scoped.Add("packages");
        }

        return scoped;
    }

    private static IReadOnlyDictionary<string, LegacyBaselineArtifact> BuildArtifacts(string scanOutput)
    {
        var artifacts = new SortedDictionary<string, LegacyBaselineArtifact>(StringComparer.Ordinal)
        {
            ["analyzerLog"] = RawArtifact(Path.Combine(scanOutput, "logs", "analyzer.log")),
            ["baselineSummary"] = new(false, "missing", null),
            ["facts"] = RawArtifact(Path.Combine(scanOutput, "facts.ndjson")),
            ["index"] = RawArtifact(Path.Combine(scanOutput, "index.sqlite")),
            ["report"] = RawArtifact(Path.Combine(scanOutput, "report.md")),
            ["scanManifest"] = RawArtifact(Path.Combine(scanOutput, "scan-manifest.json"))
        };
        return artifacts;
    }

    private static LegacyBaselineArtifact RawArtifact(string path)
    {
        if (!File.Exists(path))
        {
            return new LegacyBaselineArtifact(false, "missing", null);
        }

        return new LegacyBaselineArtifact(true, SizeBucket(new FileInfo(path).Length), null);
    }

    private static IReadOnlyList<string> BuildLimitations(JsonElement manifest, bool truncated, bool factsMissing, IReadOnlyList<string> knownGaps, LegacyBaselineAggregation aggregation)
    {
        var limitations = new SortedSet<string>(StringComparer.Ordinal)
        {
            "Baseline manifests preserve aggregate static evidence counts only and do not store raw scan artifacts.",
            "Comparisons describe count and coverage movement only; they do not prove runtime behavior."
        };

        var buildStatus = ReadString(manifest, "buildStatus", "unknown");
        if (!buildStatus.Equals("Succeeded", StringComparison.Ordinal))
        {
            limitations.Add("Build or project load did not fully succeed; baseline coverage is partial.");
        }

        var coverage = ReadString(manifest, "analysisLevel", "unknown");
        if (coverage.Contains("Reduced", StringComparison.OrdinalIgnoreCase) || aggregation.EvidenceTierCounts.ContainsKey(EvidenceTiers.Tier4Unknown))
        {
            limitations.Add("Reduced or unknown evidence tiers are preserved separately from semantic evidence.");
        }

        if (truncated)
        {
            limitations.Add("facts.ndjson exceeded the baseline size bound; parsed counts may be partial.");
        }

        if (factsMissing)
        {
            limitations.Add("facts.ndjson is missing; baseline counts are partial and preserve the missing artifact as a known gap.");
        }

        foreach (var gap in knownGaps)
        {
            limitations.Add($"Known gap preserved as category `{gap}`.");
        }

        return limitations.ToArray();
    }

    private static IReadOnlyList<string> RuleLimitations(string ruleId)
    {
        var limitations = new List<string>();
        if (ruleId.StartsWith("csharp.semantic.", StringComparison.Ordinal))
        {
            limitations.Add("Semantic evidence depends on successful compiler/project loading.");
        }
        else if (ruleId.StartsWith("csharp.syntax.", StringComparison.Ordinal))
        {
            limitations.Add("Syntax evidence does not prove compiler-resolved symbols.");
        }
        else if (ruleId.StartsWith("legacy.", StringComparison.Ordinal))
        {
            limitations.Add("Legacy static evidence does not prove runtime execution or deployment.");
        }
        else if (ruleId.StartsWith("database.", StringComparison.Ordinal) || ruleId.StartsWith("config.", StringComparison.Ordinal))
        {
            limitations.Add("Configuration and data evidence is category-level and omits raw values.");
        }

        return limitations.Order(StringComparer.Ordinal).ToArray();
    }

    private static string SurfaceFor(string factType, string ruleId)
    {
        if (ruleId.StartsWith("legacy.webforms.", StringComparison.Ordinal) || factType.StartsWith("WebForms", StringComparison.Ordinal) || factType.Contains("WinForms", StringComparison.Ordinal))
        {
            return "ui-events";
        }

        if (ruleId.StartsWith("legacy.wcf.", StringComparison.Ordinal) || factType.StartsWith("Wcf", StringComparison.Ordinal))
        {
            return "wcf-service-reference";
        }

        if (factType.StartsWith("Http", StringComparison.Ordinal) || ruleId.Contains("aspnetroute", StringComparison.Ordinal) || ruleId.StartsWith("http.", StringComparison.Ordinal))
        {
            return "http";
        }

        if (factType.Contains("Sql", StringComparison.Ordinal) || factType.Contains("Database", StringComparison.Ordinal) || factType.Contains("Dapper", StringComparison.Ordinal) || ruleId.StartsWith("database.", StringComparison.Ordinal))
        {
            return "sql-data";
        }

        if (factType.StartsWith("Config", StringComparison.Ordinal) || factType.Contains("ConnectionString", StringComparison.Ordinal) || ruleId.StartsWith("config.", StringComparison.Ordinal))
        {
            return "config";
        }

        if (factType.Contains("Package", StringComparison.Ordinal) || ruleId.Contains("package", StringComparison.Ordinal))
        {
            return "packages";
        }

        if (factType.StartsWith("BuildEnvironment", StringComparison.Ordinal) || factType is FactTypes.ProjectDeclared or FactTypes.SolutionDeclared or FactTypes.TargetFrameworkDeclared or FactTypes.FileInventoried || ruleId.StartsWith("build.environment.", StringComparison.Ordinal) || ruleId == RuleIds.ProjectFile)
        {
            return "build-environment";
        }

        if (ruleId.StartsWith("csharp.", StringComparison.Ordinal) || factType is FactTypes.TypeDeclared or FactTypes.MethodDeclared or FactTypes.PropertyDeclared or FactTypes.InvocationName or FactTypes.CallEdge or FactTypes.MethodInvoked)
        {
            return "csharp";
        }

        return "other";
    }

    private static LegacyBaselineComparisonDocument BuildComparison(
        LegacyBaselineManifest baseline,
        LegacyBaselineManifest candidate,
        LegacyBaselineMigrationMap? migrationMap,
        string? migrationMapPath,
        string generatedAt,
        IReadOnlyList<LegacyBaselineValidationDiagnostic> diagnostics)
    {
        var review = new List<LegacyBaselineReviewNeeded>();
        var dimensions = new SortedDictionary<string, IReadOnlyList<LegacyBaselineMovementRow>>(StringComparer.Ordinal);
        var schemaStatus = SchemaStatus(baseline, candidate, migrationMap, migrationMapPath, review);

        var baselineRuleCounts = ApplyRuleRenames(baseline.Counts.ByRuleId, migrationMap);
        var candidateRuleCounts = candidate.Counts.ByRuleId.ToSortedDictionary();
        var baselineFactCounts = ApplyFactRenames(baseline.Counts.ByFactType, migrationMap);
        var candidateFactCounts = candidate.Counts.ByFactType.ToSortedDictionary();

        dimensions["totals"] =
        [
            CountRow("factsTotal", baseline.Counts.FactsTotal, candidate.Counts.FactsTotal, review),
            CountRow("gapsTotal", baseline.Counts.GapsTotal, candidate.Counts.GapsTotal, review)
        ];
        dimensions["byFactType"] = CountRows(baselineFactCounts, candidateFactCounts, review);
        dimensions["byRuleId"] = CountRows(baselineRuleCounts, candidateRuleCounts, review);
        dimensions["byEvidenceTier"] = CountRows(baseline.Counts.ByEvidenceTier, candidate.Counts.ByEvidenceTier, review);
        dimensions["byExtractor"] = CountRows(baseline.Counts.ByExtractor, candidate.Counts.ByExtractor, review);
        dimensions["bySurface"] = CountRows(baseline.Counts.BySurface, candidate.Counts.BySurface, review);
        dimensions["knownGaps"] = CountRows(baseline.Counts.ByKnownGap, candidate.Counts.ByKnownGap, review);
        dimensions["coverage"] =
        [
            StringRow("coverageLabel", baseline.Scan.CoverageLabel, candidate.Scan.CoverageLabel, review),
            StringRow("buildStatus", baseline.Scan.BuildStatus, candidate.Scan.BuildStatus, review),
            StringRow("scanStatus", baseline.Scan.ScanStatus, candidate.Scan.ScanStatus, review)
        ];

        if (baseline.Safety.Classification == LegacyBaselineClassifications.Rejected || candidate.Safety.Classification == LegacyBaselineClassifications.Rejected)
        {
            review.Add(new LegacyBaselineReviewNeeded("safety-classification", "Rejected baseline classification prevents direct promotion.", SafetyRuleId));
        }

        foreach (var diagnostic in diagnostics)
        {
            review.Add(new LegacyBaselineReviewNeeded(diagnostic.Category, diagnostic.Message, diagnostic.RuleId));
        }

        var extractorCompatibility = ExtractorCompatibility(baseline, candidate);
        foreach (var extractor in extractorCompatibility.Where(item => item.Movement != "unchanged"))
        {
            review.Add(new LegacyBaselineReviewNeeded($"extractor:{extractor.ExtractorId}", $"Extractor version movement `{extractor.Movement}` needs review.", ComparisonRuleId));
        }

        var limitations = new SortedSet<string>(StringComparer.Ordinal)
        {
            "Comparison output reports static evidence count and coverage movement only.",
            "Additional static evidence can reflect scanner coverage changes, schema changes, or extractor changes.",
            "Reduced coverage, decreases, removed categories, rejected classifications, and unmapped schema changes require human review."
        };
        if (migrationMap?.Limitations is not null)
        {
            foreach (var limitation in migrationMap.Limitations)
            {
                limitations.Add(limitation);
            }
        }

        var distinctReview = review
            .GroupBy(item => (item.Category, item.Reason, item.RuleId))
            .Select(group => group.First())
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Reason, StringComparer.Ordinal)
            .ToArray();

        return new LegacyBaselineComparisonDocument(
            LegacyBaselineSchemas.Comparison,
            baseline.BaselineId,
            candidate.BaselineId,
            generatedAt,
            distinctReview.Length > 0 ? "review-needed" : "unchanged-or-increase-only",
            new LegacyBaselineSchemaCompatibility(schemaStatus, migrationMapPath is null ? null : Path.GetFileName(migrationMapPath), extractorCompatibility),
            dimensions,
            distinctReview,
            limitations.ToArray());
    }

    private static string SchemaStatus(
        LegacyBaselineManifest baseline,
        LegacyBaselineManifest candidate,
        LegacyBaselineMigrationMap? migrationMap,
        string? migrationMapPath,
        List<LegacyBaselineReviewNeeded> review)
    {
        if (baseline.SchemaVersion == candidate.SchemaVersion)
        {
            return migrationMapPath is null ? "comparable" : "comparable-with-migration-map";
        }

        if (migrationMap is not null
            && migrationMap.SchemaVersion == LegacyBaselineSchemas.MigrationMap
            && migrationMap.FromBaselineSchema == baseline.SchemaVersion
            && migrationMap.ToCandidateSchema == candidate.SchemaVersion)
        {
            return "comparable-with-migration-map";
        }

        review.Add(new LegacyBaselineReviewNeeded("schema", "Schema versions differ without a matching migration map.", ComparisonRuleId));
        return "not-comparable";
    }

    private static IReadOnlyList<LegacyBaselineExtractorCompatibility> ExtractorCompatibility(LegacyBaselineManifest baseline, LegacyBaselineManifest candidate)
    {
        return baseline.Extractors.Keys
            .Concat(candidate.Extractors.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(id =>
            {
                var baselineVersion = baseline.Extractors.TryGetValue(id, out var before) ? before.Version : null;
                var candidateVersion = candidate.Extractors.TryGetValue(id, out var after) ? after.Version : null;
                var movement = (baselineVersion, candidateVersion) switch
                {
                    (null, not null) => "new-category",
                    (not null, null) => "removed-category",
                    _ when baselineVersion == candidateVersion => "unchanged",
                    _ => "coverage-changed"
                };
                return new LegacyBaselineExtractorCompatibility(id, baselineVersion, candidateVersion, movement);
            })
            .ToArray();
    }

    private static LegacyBaselineMovementRow CountRow(string category, int baseline, int candidate, List<LegacyBaselineReviewNeeded> review)
    {
        var movement = baseline == candidate
            ? "unchanged"
            : baseline == 0 ? "new-category"
            : candidate == 0 ? "removed-category"
            : candidate > baseline ? "increase" : "decrease";
        var needsReview = movement is "decrease" or "removed-category";
        if (needsReview)
        {
            review.Add(new LegacyBaselineReviewNeeded(category, $"Count movement `{movement}` needs review.", ComparisonRuleId));
        }

        return new LegacyBaselineMovementRow(category, baseline.ToString(CultureInfo.InvariantCulture), candidate.ToString(CultureInfo.InvariantCulture), movement, needsReview, ComparisonRuleId);
    }

    private static IReadOnlyList<LegacyBaselineMovementRow> CountRows(IReadOnlyDictionary<string, int> baseline, IReadOnlyDictionary<string, int> candidate, List<LegacyBaselineReviewNeeded> review)
    {
        return baseline.Keys
            .Concat(candidate.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(key => CountRow(key, baseline.GetValueOrDefault(key), candidate.GetValueOrDefault(key), review))
            .ToArray();
    }

    private static LegacyBaselineMovementRow StringRow(string category, string baseline, string candidate, List<LegacyBaselineReviewNeeded> review)
    {
        var movement = baseline == candidate ? "unchanged" : "coverage-changed";
        var needsReview = movement == "coverage-changed" && IsReducedValue(candidate);
        if (needsReview)
        {
            review.Add(new LegacyBaselineReviewNeeded(category, "Coverage or status moved to a reduced value.", ComparisonRuleId));
        }

        return new LegacyBaselineMovementRow(category, baseline, candidate, movement, needsReview, ComparisonRuleId);
    }

    private static bool IsReducedValue(string value)
    {
        return value.Contains("Reduced", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Failed", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Partial", StringComparison.OrdinalIgnoreCase)
            || value.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || value.Contains("deferred", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, int> ApplyRuleRenames(IReadOnlyDictionary<string, int> counts, LegacyBaselineMigrationMap? migrationMap)
    {
        var renames = migrationMap?.RuleIdRenames?.ToDictionary(item => item.FromRuleId, item => item.ToRuleId, StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        return ApplyRenames(counts, renames);
    }

    private static IReadOnlyDictionary<string, int> ApplyFactRenames(IReadOnlyDictionary<string, int> counts, LegacyBaselineMigrationMap? migrationMap)
    {
        var renames = migrationMap?.FactTypeRenames?.ToDictionary(item => item.FromFactType, item => item.ToFactType, StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        return ApplyRenames(counts, renames);
    }

    private static IReadOnlyDictionary<string, int> ApplyRenames(IReadOnlyDictionary<string, int> counts, IReadOnlyDictionary<string, string> renames)
    {
        var normalized = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, count) in counts)
        {
            var normalizedKey = renames.GetValueOrDefault(key, key);
            normalized[normalizedKey] = normalized.GetValueOrDefault(normalizedKey) + count;
        }

        return normalized;
    }

    private static IReadOnlyList<LegacyBaselineValidationDiagnostic> ValidateManifest(LegacyBaselineManifest manifest, string path)
    {
        var diagnostics = new List<LegacyBaselineValidationDiagnostic>();
        if (manifest.SchemaVersion != LegacyBaselineSchemas.Manifest)
        {
            diagnostics.Add(Diagnostic("schema-version", path, "manifest schema version is not supported"));
        }

        if (manifest.Safety.Classification is not LegacyBaselineClassifications.PublicSafe and not LegacyBaselineClassifications.LocalOnly)
        {
            diagnostics.Add(Diagnostic("safety-classification", path, "manifest safety classification is rejected or unsupported"));
        }

        var text = JsonSerializer.Serialize(manifest, JsonOptions);
        diagnostics.AddRange(ScanUnsafeText(text, path));
        return diagnostics;
    }

    private static IReadOnlyList<LegacyBaselineValidationDiagnostic> ValidateComparison(LegacyBaselineComparisonDocument comparison, string path)
    {
        var text = JsonSerializer.Serialize(comparison, JsonOptions);
        return ScanUnsafeText(text, path);
    }

    private static IReadOnlyList<LegacyBaselineValidationDiagnostic> ScanUnsafeText(string text, string path)
    {
        var diagnostics = new List<LegacyBaselineValidationDiagnostic>();
        AddIf(AbsoluteUnixPath.IsMatch(text), "absolute-path");
        AddIf(WindowsDrive.IsMatch(text), "absolute-path");
        AddIf(UriScheme.IsMatch(text), "raw-remote-or-url");
        AddIf(SecretLike.IsMatch(text), "secret-like-value");
        AddIf(RawSql.IsMatch(text), "raw-sql");
        AddIf(SourceLike.IsMatch(text), "source-like-snippet");
        AddIf(text.Contains(".git", StringComparison.OrdinalIgnoreCase), "raw-remote-or-url");

        return diagnostics
            .GroupBy(item => item.Category)
            .Select(group => group.First())
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ToArray();

        void AddIf(bool condition, string category)
        {
            if (condition)
            {
                diagnostics.Add(Diagnostic(category, path, "unsafe baseline content category detected"));
            }
        }
    }

    private static IReadOnlyList<LegacyBaselineValidationDiagnostic> ValidateRuleCatalog(IEnumerable<string> ruleIds)
    {
        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var catalogPath = Path.Combine(repoRoot, "rules", "rule-catalog.yml");
        if (!File.Exists(catalogPath))
        {
            return [Diagnostic("rule-catalog-missing", catalogPath, "rule catalog is unavailable")];
        }

        var text = File.ReadAllText(catalogPath);
        return ruleIds
            .Where(ruleId => ruleId != "unknown" && !RuleCatalogContains(text, ruleId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(ruleId => Diagnostic("rule-catalog-entry-missing", catalogPath, $"observed rule ID lacks catalog entry: {ruleId}"))
            .ToArray();
    }

    private static bool RuleCatalogContains(string catalogText, string ruleId)
    {
        var pattern = @"^\s*-\s*id:\s*" + Regex.Escape(ruleId) + @"\s*$";
        return Regex.IsMatch(catalogText, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
    }

    private static string RenderBaselineSummary(LegacyBaselineManifest manifest)
    {
        var lines = new List<string>
        {
            "# Legacy Baseline Summary",
            "",
            $"Baseline ID: `{manifest.BaselineId}`",
            $"Schema: `{manifest.SchemaVersion}`",
            $"Classification: `{manifest.Safety.Classification}`",
            $"Coverage: `{manifest.Scan.CoverageLabel}`",
            $"Build status: `{manifest.Scan.BuildStatus}`",
            $"Scan status: `{manifest.Scan.ScanStatus}`",
            $"Facts: `{manifest.Counts.FactsTotal}`",
            $"Gaps: `{manifest.Counts.GapsTotal}`",
            "",
            "## Evidence Tiers",
            ""
        };
        foreach (var (tier, count) in manifest.Counts.ByEvidenceTier)
        {
            lines.Add($"- `{tier}`: `{count}`");
        }

        lines.Add("");
        lines.Add("## Surfaces");
        lines.Add("");
        foreach (var (surface, status) in manifest.Surfaces)
        {
            lines.Add($"- `{surface}`: `{status}`");
        }

        lines.Add("");
        lines.Add("## Limitations");
        lines.Add("");
        foreach (var limitation in manifest.Limitations)
        {
            lines.Add($"- {limitation}");
        }

        lines.Add("");
        lines.Add("Raw scan artifacts are not copied into this summary.");
        lines.Add("");
        return string.Join('\n', lines);
    }

    private static string RenderComparisonMarkdown(LegacyBaselineComparisonDocument comparison)
    {
        var lines = new List<string>
        {
            "# Legacy Baseline Comparison",
            "",
            $"Baseline: `{comparison.BaselineId}`",
            $"Candidate: `{comparison.CandidateId}`",
            $"Generated: `{comparison.GeneratedAt}`",
            $"Overall status: `{comparison.OverallStatus}`",
            $"Schema compatibility: `{comparison.SchemaCompatibility.Status}`",
            "",
            "This report compares deterministic static evidence counts and coverage labels only.",
            "",
            "## Movements",
            ""
        };

        foreach (var (dimension, rows) in comparison.Dimensions)
        {
            lines.Add($"### {dimension}");
            lines.Add("");
            lines.Add("| Category | Baseline | Candidate | Movement | Review |");
            lines.Add("| --- | ---: | ---: | --- | --- |");
            foreach (var row in rows.OrderBy(row => row.Category, StringComparer.Ordinal))
            {
                lines.Add($"| `{Cell(row.Category)}` | `{Cell(row.BaselineValue)}` | `{Cell(row.CandidateValue)}` | `{Cell(row.Movement)}` | `{(row.ReviewNeeded ? "yes" : "no")}` |");
            }

            lines.Add("");
        }

        if (comparison.ReviewNeeded.Count > 0)
        {
            lines.Add("## Review Needed");
            lines.Add("");
            foreach (var item in comparison.ReviewNeeded)
            {
                lines.Add($"- `{Cell(item.Category)}` ({Cell(item.RuleId)}): {Cell(item.Reason)}");
            }

            lines.Add("");
        }

        lines.Add("## Limitations");
        lines.Add("");
        foreach (var limitation in comparison.Limitations)
        {
            lines.Add($"- {limitation}");
        }

        lines.Add("");
        return string.Join('\n', lines);
    }

    private static string Cell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<JsonElement>> ReadFactsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var info = new FileInfo(path);
        if (info.Length > MaxFactsBytes)
        {
            return [];
        }

        var facts = new List<JsonElement>();
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            facts.Add(document.RootElement.Clone());
        }

        return facts;
    }

    private static JsonElement ReadObject(string path)
    {
        if (!File.Exists(path))
        {
            using var empty = JsonDocument.Parse("{}");
            return empty.RootElement.Clone();
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        return value ?? throw new InvalidOperationException($"Unable to read JSON file: {path}");
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
    }

    private static string ReadString(JsonElement element, string propertyName, string defaultValue)
    {
        return TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : defaultValue;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            .Select(item => item.GetString()!)
            .ToArray();
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string NormalizeCreatedAt(string? value, string classification)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTimeOffset.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            return classification == LegacyBaselineClassifications.PublicSafe
                ? timestamp.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                : timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        if (Regex.IsMatch(value, "^\\d{4}-\\d{2}$", RegexOptions.CultureInvariant))
        {
            return value;
        }

        throw new ArgumentException("baseline time must be ISO timestamp or yyyy-MM.");
    }

    private static string NormalizeScanStartedAt(string value, string classification)
    {
        return NormalizeCreatedAt(value, classification);
    }

    private static string ScanStatus(JsonElement manifest, int factCount, bool truncated, bool factsMissing)
    {
        if (factsMissing)
        {
            return "artifact-missing-partial";
        }

        if (truncated)
        {
            return "truncated";
        }

        var build = ReadString(manifest, "buildStatus", "unknown");
        if (build.Contains("Failed", StringComparison.OrdinalIgnoreCase))
        {
            return factCount > 0 ? "completed-partial" : "failed-partial";
        }

        return "completed";
    }

    private static bool IsPartial(JsonElement manifest, bool truncated, bool factsMissing)
    {
        var build = ReadString(manifest, "buildStatus", "unknown");
        var coverage = ReadString(manifest, "analysisLevel", "unknown");
        return factsMissing
            || truncated
            || !build.Equals("Succeeded", StringComparison.Ordinal)
            || coverage.Contains("Reduced", StringComparison.OrdinalIgnoreCase)
            || coverage.Equals("unknown", StringComparison.Ordinal);
    }

    private static bool HasKnownGap(IEnumerable<string> knownGaps, string token)
    {
        return knownGaps.Any(gap => gap.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string SizeBucket(long size)
    {
        return size switch
        {
            <= 0 => "empty",
            < 16 * 1024 => "small",
            < 1024 * 1024 => "medium",
            _ => "large"
        };
    }

    private static bool IsAllowedScanInput(string path, string repoRoot)
    {
        return IsUnderRepoRelative(path, repoRoot, "samples")
            || IsUnderRepoRelative(path, repoRoot, ".tmp/legacy-baselines")
            || IsUnderRepoRelative(path, repoRoot, ".tmp/legacy-codebase-validation")
            || GitCheckIgnore(path, repoRoot);
    }

    private static bool IsAllowedOutput(string path, string repoRoot, string classification)
    {
        if (classification == LegacyBaselineClassifications.LocalOnly)
        {
            return IsUnderRepoRelative(path, repoRoot, ".tmp/legacy-baselines");
        }

        return IsUnderRepoRelative(path, repoRoot, ".tmp/legacy-baselines")
            || IsUnderRepoRelative(path, repoRoot, ".kiro/baselines/legacy");
    }

    private static string ResolvePath(string path, string repoRoot)
    {
        return Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private static bool IsUnderRepoRelative(string path, string repoRoot, string relativeRoot)
    {
        var relative = Path.GetRelativePath(repoRoot, Path.GetFullPath(path));
        return !relative.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative)
            && (relative == relativeRoot || relative.StartsWith(relativeRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relative.StartsWith(relativeRoot + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static bool GitCheckIgnore(string path, string repoRoot)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                ArgumentList = { "check-ignore", path },
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string FindRepoRoot(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(start);
    }

    private static void Increment(SortedDictionary<string, int> counts, string key)
    {
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }

    private static void IncrementNested(SortedDictionary<string, SortedDictionary<string, int>> counts, string outer, string inner)
    {
        if (!counts.TryGetValue(outer, out var nested))
        {
            nested = new SortedDictionary<string, int>(StringComparer.Ordinal);
            counts[outer] = nested;
        }

        Increment(nested, inner);
    }

    private static void SetNested(SortedDictionary<string, SortedDictionary<string, string>> values, string outer, string inner, string value)
    {
        if (!values.TryGetValue(outer, out var nested))
        {
            nested = new SortedDictionary<string, string>(StringComparer.Ordinal);
            values[outer] = nested;
        }

        nested[inner] = value;
    }

    private static void AddVersion(SortedDictionary<string, SortedSet<string>> versions, string extractorId, string version)
    {
        if (!versions.TryGetValue(extractorId, out var set))
        {
            set = new SortedSet<string>(StringComparer.Ordinal);
            versions[extractorId] = set;
        }

        set.Add(version);
    }

    private static void AddSet(SortedDictionary<string, SortedSet<string>> values, string key, string value)
    {
        if (!values.TryGetValue(key, out var set))
        {
            set = new SortedSet<string>(StringComparer.Ordinal);
            values[key] = set;
        }

        set.Add(value);
    }

    private static SortedDictionary<string, int> EmptyIntMap()
    {
        return new SortedDictionary<string, int>(StringComparer.Ordinal);
    }

    private static SortedDictionary<string, string> EmptyStringMap()
    {
        return new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    private static SortedDictionary<string, TValue> ToSortedDictionary<TValue>(this IEnumerable<KeyValuePair<string, TValue>> values)
    {
        return new SortedDictionary<string, TValue>(values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private static void AppendLengthPrefixed(StringBuilder builder, string key, string value)
    {
        builder.Append(key);
        builder.Append(':');
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
        builder.Append('\n');
    }

    private static string Sha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static LegacyBaselineValidationDiagnostic Diagnostic(string category, string path, string message)
    {
        return new LegacyBaselineValidationDiagnostic(category, path, SafetyRuleId, message);
    }

    private static void ThrowIfFatalBaselineDiagnostics(IReadOnlyCollection<LegacyBaselineValidationDiagnostic> diagnostics, string message)
    {
        if (diagnostics.Any(diagnostic => diagnostic.Category is "absolute-path" or "raw-remote-or-url" or "secret-like-value" or "raw-sql" or "source-like-snippet"))
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record LegacyBaselineAggregation(
        IReadOnlyDictionary<string, int> FactTypeCounts,
        IReadOnlyDictionary<string, int> RuleCounts,
        IReadOnlyDictionary<string, int> EvidenceTierCounts,
        IReadOnlyDictionary<string, int> ExtractorCounts,
        IReadOnlyDictionary<string, int> SurfaceCounts,
        IReadOnlyDictionary<string, int> KnownGapCounts,
        IReadOnlyDictionary<string, LegacyBaselineExtractor> Extractors,
        IReadOnlyList<LegacyBaselineRuleCoverage> RuleCoverage,
        IReadOnlyList<LegacyBaselineFactCoverage> FactCoverage,
        int GapCount);
}
