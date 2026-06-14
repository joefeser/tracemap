using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record SnapshotDiffOptions(
    string BeforePath,
    string AfterPath,
    string OutputPath,
    string Format = "markdown",
    string? Scope = null,
    bool IncludePaths = false,
    bool AllowIdentityMismatch = false,
    bool ExitCode = false,
    string? Source = null,
    string? Endpoint = null,
    string? Surface = null,
    string? SurfaceName = null,
    int MaxDepth = 8,
    int MaxPaths = 100,
    int MaxFrontier = 10000,
    int MaxDiffRows = 1000,
    int MaxGaps = 1000);

public sealed record SnapshotDiffResult(
    SnapshotDiffDocument Report,
    string? MarkdownPath,
    string? JsonPath)
{
    public bool HasDiffs =>
        Report.Summary.SourceDiffCount
        + Report.Summary.CoverageDiffCount
        + Report.Summary.EndpointDiffCount
        + Report.Summary.ContractShapeDiffCount
        + Report.Summary.SurfaceDiffCount
        + Report.Summary.GraphDiffCount
        + Report.Summary.GapDiffCount
        + Report.Summary.ExtractorVersionDiffCount
        + Report.Summary.PathDiffCount > 0;
}

public sealed record SnapshotDiffDocument(
    string ReportType,
    string Version,
    string ReportCoverage,
    SnapshotDiffQuery Query,
    SnapshotDiffSnapshot BeforeSnapshot,
    SnapshotDiffSnapshot AfterSnapshot,
    SnapshotDiffSummary Summary,
    IReadOnlyList<SnapshotDiffRow> SourceDiffs,
    IReadOnlyList<SnapshotDiffRow> CoverageDiffs,
    IReadOnlyList<SnapshotDiffRow> EndpointDiffs,
    IReadOnlyList<SnapshotDiffRow> ContractShapeDiffs,
    IReadOnlyList<SnapshotDiffRow> SurfaceDiffs,
    IReadOnlyList<SnapshotDiffRow> GraphDiffs,
    IReadOnlyList<SnapshotDiffRow> GapDiffs,
    IReadOnlyList<SnapshotDiffRow> ExtractorVersionDiffs,
    IReadOnlyList<SnapshotDiffRow> PathDiffs,
    IReadOnlyList<SnapshotDiffGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record SnapshotDiffQuery(
    IReadOnlyList<string> Scopes,
    bool IncludePaths,
    bool AllowIdentityMismatch,
    string? Source,
    string? Endpoint,
    string? Surface,
    string? SurfaceName,
    int MaxDepth,
    int MaxPaths,
    int MaxFrontier,
    int MaxDiffRows,
    int MaxGaps,
    string Algorithm,
    string AlgorithmVersion);

public sealed record SnapshotDiffSnapshot(
    string Side,
    string IndexKind,
    IReadOnlyList<SnapshotDiffSourceInfo> Sources,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    IReadOnlyList<KeyValuePair<string, string>> ExtractorVersions);

public sealed record SnapshotDiffSourceInfo(
    string SourceLabel,
    string? Language,
    string? ScanId,
    string? CommitSha,
    string? RepositoryIdentityHash,
    string? RootPathHash,
    string Coverage,
    string BuildStatus,
    string AnalysisLevel,
    string? ScannerVersion,
    IReadOnlyList<string> GapCodes);

public sealed record SnapshotDiffSummary(
    string RollupClassification,
    string RuleId,
    int SourceCount,
    int SourceDiffCount,
    int CoverageDiffCount,
    int EndpointDiffCount,
    int ContractShapeDiffCount,
    int SurfaceDiffCount,
    int GraphDiffCount,
    int GapDiffCount,
    int ExtractorVersionDiffCount,
    int PathDiffCount,
    int GapCount,
    bool Truncated,
    string Message);

public sealed record SnapshotDiffRow(
    string DiffId,
    string StableKey,
    string ChangeType,
    string Classification,
    string Confidence,
    string EvidenceKind,
    string? SourceLabel,
    SnapshotDiffEvidence? Before,
    SnapshotDiffEvidence? After,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> SupportingPathIds,
    IReadOnlyList<SnapshotDiffFileSpan> FileSpans,
    IReadOnlyList<string> CoverageCaveats,
    IReadOnlyList<string> Notes);

public sealed record SnapshotDiffFileSpan(
    string FilePath,
    int? StartLine,
    int? EndLine,
    string? SourceLabel);

public sealed record SnapshotDiffEvidence(
    string? SourceLabel,
    string? CommitSha,
    string? ScanId,
    string? Language,
    string? RepositoryIdentityHash,
    string? RootPathHash,
    string? Coverage,
    string? AnalysisLevel,
    string? BuildStatus,
    string? ScannerVersion,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record SnapshotDiffGap(
    string GapId,
    string GapKind,
    string Section,
    string? SourceLabel,
    string RuleId,
    string EvidenceTier,
    string Classification,
    string Message,
    IReadOnlyList<string> SupportingDiffIds,
    IReadOnlyList<SnapshotDiffFileSpan> FileSpans,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

internal sealed record SnapshotIndexInfo(string Kind, SnapshotDiffSnapshot Snapshot);

internal sealed record SnapshotSourcePair(string Label, SnapshotDiffSourceInfo? Before, SnapshotDiffSourceInfo? After);

public static class SnapshotDiffClassifications
{
    public const string Added = nameof(Added);
    public const string Removed = nameof(Removed);
    public const string ChangedEvidence = nameof(ChangedEvidence);
    public const string ChangedWithReducedCoverage = nameof(ChangedWithReducedCoverage);
    public const string NoSnapshotDiffEvidence = nameof(NoSnapshotDiffEvidence);
    public const string NeedsReview = nameof(NeedsReview);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
    public const string TruncatedByLimit = nameof(TruncatedByLimit);
}

public static class SnapshotDiffReporter
{
    private const string ReportType = "snapshot-diff";
    private const string Version = "1.0";
    private const string Algorithm = "snapshot-diff-shell";
    private const string AlgorithmVersion = "1.0";
    private const string SourceRuleId = "snapshot.diff.source.v1";
    private const string CoverageRuleId = "snapshot.diff.coverage.v1";
    private const string EvidenceRuleId = "snapshot.diff.evidence.v1";
    private const string IdentityRuleId = "snapshot.diff.identity.v1";
    private const string SchemaRuleId = "snapshot.diff.schema.v1";

    private static readonly HashSet<string> ValidScopes = new(StringComparer.Ordinal)
    {
        "all",
        "sources",
        "coverage",
        "endpoints",
        "surfaces",
        "graph",
        "paths",
        "gaps",
        "extractors",
        "contract-shapes"
    };

    private static readonly IReadOnlyList<string> DefaultScopes =
    [
        "sources",
        "coverage",
        "endpoints",
        "contract-shapes",
        "surfaces",
        "graph",
        "gaps",
        "extractors"
    ];

    private static readonly IReadOnlyList<string> DefaultLimitations =
    [
        "Snapshot diff compares deterministic static evidence between TraceMap indexes; it does not prove runtime behavior, deployment behavior, traffic, compatibility, or business impact.",
        "Source pairing is exact-label based and does not infer repository renames, label renames, branch topology, or merge ancestry.",
        "Missing, unknown, or conflicting source identity and commit metadata downgrade conclusions to review-tier gaps.",
        "Endpoint, contract-shape, surface, graph, gap, and path evidence are represented as explicit availability gaps in this shell slice until deeper evidence readers are implemented.",
        "Reports omit or hash raw URLs, local absolute paths, source snippets, raw SQL, config values, connection strings, and secret-looking values."
    ];

    public static async Task<SnapshotDiffResult> WriteAsync(SnapshotDiffOptions options, CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(options, cancellationToken);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "snapshot-diff");
        var outputs = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "snapshot-diff-report.md",
            "snapshot-diff-report.json",
            report,
            RenderMarkdown,
            CombinedDependencyReporter.JsonOptions,
            cancellationToken);
        return new SnapshotDiffResult(report, outputs.MarkdownPath, outputs.JsonPath);
    }

    public static async Task<SnapshotDiffDocument> BuildReportAsync(SnapshotDiffOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var scopes = ParseScopes(options.Scope);
        ValidateSelectorCompatibility(options, scopes);

        var before = await ReadIndexInfoAsync(options.BeforePath, "before", cancellationToken);
        var after = await ReadIndexInfoAsync(options.AfterPath, "after", cancellationToken);
        if (!string.Equals(before.Kind, after.Kind, StringComparison.Ordinal))
        {
            throw new InvalidDataException("snapshot-diff requires both inputs to be the same TraceMap index kind; mixed single and combined indexes are not supported.");
        }

        var allGaps = new List<SnapshotDiffGap>();
        var sourcePairs = PairSources(before.Snapshot, after.Snapshot, options.Source);
        AddIdentityGaps(sourcePairs, allGaps, before.Kind == "combined");
        if (allGaps.Any(gap => gap.GapKind == "SourceIdentityConflict") && !options.AllowIdentityMismatch)
        {
            var label = allGaps.First(gap => gap.GapKind == "SourceIdentityConflict").SourceLabel ?? "unknown";
            throw new InvalidDataException($"snapshot-diff source identity conflict for source label `{label}`; use --allow-identity-mismatch to produce review-tier output.");
        }

        if (!string.IsNullOrWhiteSpace(options.Source) && sourcePairs.Count == 0)
        {
            allGaps.Add(Gap("selector", "SelectorNoMatch", "sources", options.Source, EvidenceRuleId, SnapshotDiffClassifications.SelectorNoMatch, $"Source selector `{options.Source}` matched no comparable source evidence."));
        }

        var sourceDiffs = scopes.Contains("sources", StringComparer.Ordinal)
            ? BuildSourceDiffs(sourcePairs, allGaps, options.MaxDiffRows)
            : [];
        var coverageDiffs = scopes.Contains("coverage", StringComparer.Ordinal)
            ? BuildCoverageDiffs(sourcePairs, allGaps, options.MaxDiffRows)
            : [];
        var extractorVersionDiffs = scopes.Contains("extractors", StringComparer.Ordinal)
            ? BuildExtractorDiffs(sourcePairs, allGaps, options.MaxDiffRows)
            : [];

        AddUnavailableGaps(scopes, before.Kind, options.IncludePaths, allGaps);
        var gaps = allGaps
            .OrderBy(gap => gap.Section, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .Take(options.MaxGaps)
            .ToArray();
        var gapsTruncated = allGaps.Count > gaps.Length;
        if (gapsTruncated)
        {
            gaps = gaps.Append(Gap("truncation", "TruncatedByLimit", "gaps", null, EvidenceRuleId, SnapshotDiffClassifications.TruncatedByLimit, $"Gap output was truncated at --max-gaps {options.MaxGaps}.")).ToArray();
        }

        var diffCount = sourceDiffs.Count + coverageDiffs.Count + extractorVersionDiffs.Count;
        var allRows = sourceDiffs.Concat(coverageDiffs).Concat(extractorVersionDiffs).ToArray();
        var rollup = Rollup(diffCount, allRows, gaps);
        var coverage = gaps.Any(gap => gap.Classification == SnapshotDiffClassifications.UnknownAnalysisGap || gap.GapKind == "UnavailableEvidence" || gap.GapKind == "ReducedCoverage")
            ? "Partial"
            : "Full";
        var summary = new SnapshotDiffSummary(
            rollup,
            EvidenceRuleId,
            sourcePairs.Count,
            sourceDiffs.Count,
            coverageDiffs.Count,
            0,
            0,
            0,
            0,
            0,
            extractorVersionDiffs.Count,
            0,
            gaps.Length,
            gapsTruncated || gaps.Any(gap => gap.GapKind == "TruncatedByLimit"),
            diffCount == 0
                ? "No source, coverage, or extractor-version differences were found in the currently implemented snapshot-diff shell."
                : "Snapshot source, coverage, or extractor-version evidence changed.");

        return new SnapshotDiffDocument(
            ReportType,
            Version,
            coverage,
            new SnapshotDiffQuery(scopes, options.IncludePaths, options.AllowIdentityMismatch, options.Source, options.Endpoint, options.Surface, options.SurfaceName, options.MaxDepth, options.MaxPaths, options.MaxFrontier, options.MaxDiffRows, options.MaxGaps, Algorithm, AlgorithmVersion),
            before.Snapshot,
            after.Snapshot,
            summary,
            sourceDiffs,
            coverageDiffs,
            [],
            [],
            [],
            [],
            [],
            extractorVersionDiffs,
            [],
            gaps,
            DefaultLimitations);
    }

    private static void ValidateOptions(SnapshotDiffOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BeforePath))
        {
            throw new ArgumentException("snapshot-diff requires --before <index.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.AfterPath))
        {
            throw new ArgumentException("snapshot-diff requires --after <index.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("snapshot-diff requires --out <path>.");
        }

        if (!File.Exists(options.BeforePath))
        {
            throw new FileNotFoundException("snapshot-diff before index was not found.");
        }

        if (!File.Exists(options.AfterPath))
        {
            throw new FileNotFoundException("snapshot-diff after index was not found.");
        }

        _ = CombinedReportHelpers.NormalizeFormat(options.Format, "snapshot-diff");
        if (options.MaxDepth <= 0 || options.MaxPaths <= 0 || options.MaxFrontier <= 0 || options.MaxDiffRows <= 0 || options.MaxGaps <= 0)
        {
            throw new ArgumentException("snapshot-diff numeric bounds must be positive integers.");
        }
    }

    private static IReadOnlyList<string> ParseScopes(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return DefaultScopes;
        }

        var scopes = scope
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (scopes.Contains("all", StringComparer.Ordinal))
        {
            return ["all", "sources", "coverage", "endpoints", "contract-shapes", "surfaces", "graph", "gaps", "extractors"];
        }

        var unknown = scopes.FirstOrDefault(value => !ValidScopes.Contains(value));
        if (unknown is not null)
        {
            throw new ArgumentException($"snapshot-diff --scope contains unsupported value `{unknown}`.");
        }

        return scopes;
    }

    private static void ValidateSelectorCompatibility(SnapshotDiffOptions options, IReadOnlyList<string> scopes)
    {
        if (scopes.Contains("paths", StringComparer.Ordinal) && !options.IncludePaths)
        {
            throw new ArgumentException("snapshot-diff --scope paths requires --include-paths.");
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint) && !options.Endpoint.Contains(' ', StringComparison.Ordinal))
        {
            throw new ArgumentException("snapshot-diff --endpoint must be formatted as `<METHOD> <normalized-path-key>`.");
        }
    }

    private static async Task<SnapshotIndexInfo> ReadIndexInfoAsync(string path, string side, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ReadOnlyConnectionString(path));
        await connection.OpenAsync(cancellationToken);
        var hasScanManifest = await TableExistsAsync(connection, "scan_manifest", cancellationToken);
        var hasFacts = await TableExistsAsync(connection, "facts", cancellationToken);
        var hasSources = await TableExistsAsync(connection, "index_sources", cancellationToken);
        var hasCombinedFacts = await TableExistsAsync(connection, "combined_facts", cancellationToken);
        if (hasSources && hasCombinedFacts)
        {
            return new SnapshotIndexInfo("combined", await ReadCombinedSnapshotAsync(connection, side, cancellationToken));
        }

        if (hasScanManifest && hasFacts)
        {
            return new SnapshotIndexInfo("single", await ReadSingleSnapshotAsync(connection, side, cancellationToken));
        }

        var missing = !hasScanManifest && !hasSources ? "scan_manifest/index_sources" : !hasFacts && !hasCombinedFacts ? "facts/combined_facts" : "TraceMap index tables";
        throw new InvalidDataException($"{side} input is not a valid TraceMap index; missing {missing}.");
    }

    private static async Task<SnapshotDiffSnapshot> ReadCombinedSnapshotAsync(SqliteConnection connection, string side, CancellationToken cancellationToken)
    {
        var sources = new List<SnapshotDiffSourceInfo>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select label,
                   language,
                   scan_id,
                   commit_sha,
                   repo_name,
                   remote_url,
                   scan_root_path_hash,
                   git_root_hash,
                   analysis_level,
                   build_status,
                   scanner_version,
                   manifest_json
            from index_sources
            order by label, source_index_id;
            """;
        var extractorVersions = new SortedDictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var manifest = DeserializeManifest(StringOrNull(reader, 11));
            var label = StringOrDefault(reader, 0, "unknown");
            var scannerVersion = StringOrDefault(reader, 10, "unknown");
            extractorVersions[$"{label}:{scannerVersion}"] = scannerVersion;
            var gaps = manifest?.KnownGaps ?? [];
            var analysisLevel = StringOrDefault(reader, 8, "unknown");
            var buildStatus = StringOrDefault(reader, 9, "unknown");
            sources.Add(new SnapshotDiffSourceInfo(
                label,
                StringOrNull(reader, 1) ?? InferLanguage(scannerVersion),
                StringOrNull(reader, 2),
                NullIfUnknown(StringOrNull(reader, 3)),
                RepositoryIdentityHash(StringOrNull(reader, 5), StringOrNull(reader, 4)),
                StringOrNull(reader, 6),
                CoverageFrom(analysisLevel, buildStatus, gaps),
                buildStatus,
                analysisLevel,
                scannerVersion,
                gaps.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
        }

        var warnings = CoverageWarnings(sources);
        return new SnapshotDiffSnapshot(side, "combined", sources, warnings.Count == 0 ? "Full" : "Reduced", warnings, extractorVersions.ToArray());
    }

    private static async Task<SnapshotDiffSnapshot> ReadSingleSnapshotAsync(SqliteConnection connection, string side, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select scan_id,
                   repo,
                   commit_sha,
                   scanner_version,
                   analysis_level,
                   build_status,
                   manifest_json
            from scan_manifest
            order by scanned_at desc
            limit 1;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException($"{side} input is not a valid TraceMap index; missing scan_manifest row.");
        }

        var manifest = DeserializeManifest(StringOrNull(reader, 6));
        var gaps = manifest?.KnownGaps ?? [];
        var scannerVersion = StringOrDefault(reader, 3, "unknown");
        var analysisLevel = StringOrDefault(reader, 4, "unknown");
        var buildStatus = StringOrDefault(reader, 5, "unknown");
        var source = new SnapshotDiffSourceInfo(
            "single",
            InferLanguage(scannerVersion),
            StringOrNull(reader, 0),
            NullIfUnknown(StringOrNull(reader, 2)),
            RepositoryIdentityHash(manifest?.RemoteUrl, StringOrNull(reader, 1)),
            manifest?.ScanRootPathHash,
            CoverageFrom(analysisLevel, buildStatus, gaps),
            buildStatus,
            analysisLevel,
            scannerVersion,
            gaps.OrderBy(value => value, StringComparer.Ordinal).ToArray());
        var warnings = CoverageWarnings([source]);
        return new SnapshotDiffSnapshot(side, "single", [source], warnings.Count == 0 ? "Full" : "Reduced", warnings, [new KeyValuePair<string, string>($"single:{scannerVersion}", scannerVersion)]);
    }

    private static IReadOnlyList<SnapshotSourcePair> PairSources(SnapshotDiffSnapshot before, SnapshotDiffSnapshot after, string? sourceSelector)
    {
        var beforeSources = before.Sources.ToLookup(source => source.SourceLabel, StringComparer.Ordinal);
        var afterSources = after.Sources.ToLookup(source => source.SourceLabel, StringComparer.Ordinal);

        return beforeSources.Select(group => group.Key)
            .Concat(afterSources.Select(group => group.Key))
            .Distinct(StringComparer.Ordinal)
            .Where(label => string.IsNullOrWhiteSpace(sourceSelector) || label.Equals(sourceSelector, StringComparison.Ordinal))
            .OrderBy(label => label, StringComparer.Ordinal)
            .Select(label => new SnapshotSourcePair(
                label,
                beforeSources[label].FirstOrDefault(),
                afterSources[label].FirstOrDefault()))
            .ToArray();
    }

    private static void AddIdentityGaps(IReadOnlyList<SnapshotSourcePair> pairs, List<SnapshotDiffGap> gaps, bool strictLanguage)
    {
        foreach (var pair in pairs)
        {
            if (pair.Before is null || pair.After is null)
            {
                gaps.Add(Gap("identity", "SourceOnlyOnOneSide", "sources", pair.Label, IdentityRuleId, SnapshotDiffClassifications.NeedsReview, $"Source `{pair.Label}` exists on only one side of the comparison."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(pair.Before.CommitSha) || string.IsNullOrWhiteSpace(pair.After.CommitSha))
            {
                gaps.Add(Gap("identity", "UnknownCommitSha", "sources", pair.Label, IdentityRuleId, SnapshotDiffClassifications.UnknownAnalysisGap, $"Source `{pair.Label}` has an unknown commit SHA on one or both sides."));
            }

            var languageConflict = strictLanguage
                ? !string.Equals(pair.Before.Language, pair.After.Language, StringComparison.OrdinalIgnoreCase)
                : !string.IsNullOrWhiteSpace(pair.Before.Language)
                  && !string.IsNullOrWhiteSpace(pair.After.Language)
                  && !string.Equals(pair.Before.Language, pair.After.Language, StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(pair.Before.RepositoryIdentityHash, pair.After.RepositoryIdentityHash, StringComparison.Ordinal)
                || languageConflict)
            {
                gaps.Add(Gap("identity", "SourceIdentityConflict", "sources", pair.Label, IdentityRuleId, SnapshotDiffClassifications.UnknownAnalysisGap, $"Source `{pair.Label}` identity differs between snapshots."));
            }

            if (!string.Equals(pair.Before.Coverage, "Full", StringComparison.Ordinal)
                || !string.Equals(pair.After.Coverage, "Full", StringComparison.Ordinal))
            {
                gaps.Add(Gap("coverage", "ReducedCoverage", "coverage", pair.Label, CoverageRuleId, SnapshotDiffClassifications.NeedsReview, $"Source `{pair.Label}` has reduced static analysis coverage."));
            }
        }
    }

    private static IReadOnlyList<SnapshotDiffRow> BuildSourceDiffs(IReadOnlyList<SnapshotSourcePair> pairs, List<SnapshotDiffGap> gaps, int maxRows)
    {
        var rows = pairs
            .Select(pair => SourceDiff(pair, gaps))
            .OfType<SnapshotDiffRow>()
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ToArray();
        return TruncateRows("sourceDiffs", rows, maxRows, gaps);
    }

    private static SnapshotDiffRow? SourceDiff(SnapshotSourcePair pair, IReadOnlyList<SnapshotDiffGap> gaps)
    {
        if (pair.Before is null)
        {
            return Row("source", pair.Label, SnapshotDiffClassifications.Added, "source", null, pair.After, SourceRuleId, EvidenceTiers.Tier2Structural, gaps);
        }

        if (pair.After is null)
        {
            return Row("source", pair.Label, SnapshotDiffClassifications.Removed, "source", pair.Before, null, SourceRuleId, EvidenceTiers.Tier2Structural, gaps);
        }

        var changed = !string.Equals(pair.Before.CommitSha, pair.After.CommitSha, StringComparison.Ordinal)
            || !string.Equals(pair.Before.ScanId, pair.After.ScanId, StringComparison.Ordinal)
            || !string.Equals(pair.Before.RepositoryIdentityHash, pair.After.RepositoryIdentityHash, StringComparison.Ordinal)
            || !string.Equals(pair.Before.RootPathHash, pair.After.RootPathHash, StringComparison.Ordinal)
            || !string.Equals(pair.Before.Language, pair.After.Language, StringComparison.OrdinalIgnoreCase);
        if (!changed)
        {
            return null;
        }

        return Row("source", pair.Label, Classify(pair.Label, gaps), "source", pair.Before, pair.After, SourceRuleId, EvidenceTiers.Tier2Structural, gaps);
    }

    private static IReadOnlyList<SnapshotDiffRow> BuildCoverageDiffs(IReadOnlyList<SnapshotSourcePair> pairs, List<SnapshotDiffGap> gaps, int maxRows)
    {
        var rows = pairs
            .Where(pair => pair.Before is not null && pair.After is not null)
            .Where(pair =>
                !string.Equals(pair.Before!.Coverage, pair.After!.Coverage, StringComparison.Ordinal)
                || !string.Equals(pair.Before.AnalysisLevel, pair.After.AnalysisLevel, StringComparison.Ordinal)
                || !string.Equals(pair.Before.BuildStatus, pair.After.BuildStatus, StringComparison.Ordinal)
                || !pair.Before.GapCodes.SequenceEqual(pair.After.GapCodes, StringComparer.Ordinal))
            .Select(pair => Row("coverage", pair.Label, Classify(pair.Label, gaps), "coverage", pair.Before, pair.After, CoverageRuleId, EvidenceTiers.Tier4Unknown, gaps))
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ToArray();
        return TruncateRows("coverageDiffs", rows, maxRows, gaps);
    }

    private static IReadOnlyList<SnapshotDiffRow> BuildExtractorDiffs(IReadOnlyList<SnapshotSourcePair> pairs, List<SnapshotDiffGap> gaps, int maxRows)
    {
        var rows = pairs
            .Where(pair => pair.Before is not null && pair.After is not null)
            .Where(pair => !string.Equals(pair.Before!.ScannerVersion, pair.After!.ScannerVersion, StringComparison.Ordinal))
            .Select(pair => Row("extractor", pair.Label, Classify(pair.Label, gaps), "extractor-version", pair.Before, pair.After, EvidenceRuleId, EvidenceTiers.Tier2Structural, gaps))
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ToArray();
        return TruncateRows("extractorVersionDiffs", rows, maxRows, gaps);
    }

    private static IReadOnlyList<SnapshotDiffRow> TruncateRows(string section, IReadOnlyList<SnapshotDiffRow> rows, int maxRows, List<SnapshotDiffGap> gaps)
    {
        if (rows.Count <= maxRows)
        {
            return rows;
        }

        gaps.Add(Gap("truncation", "TruncatedByLimit", section, null, EvidenceRuleId, SnapshotDiffClassifications.TruncatedByLimit, $"{section} output was truncated at --max-diff-rows {maxRows}; {rows.Count - maxRows} rows were omitted."));
        return rows.Take(maxRows).ToArray();
    }

    private static SnapshotDiffRow Row(
        string kind,
        string label,
        string classification,
        string evidenceKind,
        SnapshotDiffSourceInfo? before,
        SnapshotDiffSourceInfo? after,
        string ruleId,
        string evidenceTier,
        IReadOnlyList<SnapshotDiffGap> gaps)
    {
        var stableKey = $"{kind}:{label}";
        var sourceGaps = gaps
            .Where(gap => gap.SourceLabel == label)
            .OrderBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        return new SnapshotDiffRow(
            StableId("diff", stableKey, classification),
            stableKey,
            ChangeType(before, after),
            classification,
            Confidence(classification),
            evidenceKind,
            label,
            before is null ? null : Evidence(before),
            after is null ? null : Evidence(after),
            [ruleId],
            [evidenceTier],
            [],
            [],
            [],
            [],
            sourceGaps.Select(gap => gap.GapKind).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            sourceGaps.Select(gap => gap.GapId).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static SnapshotDiffEvidence Evidence(SnapshotDiffSourceInfo source)
    {
        return new SnapshotDiffEvidence(
            source.SourceLabel,
            source.CommitSha,
            source.ScanId,
            source.Language,
            source.RepositoryIdentityHash,
            source.RootPathHash,
            source.Coverage,
            source.AnalysisLevel,
            source.BuildStatus,
            source.ScannerVersion,
            CombinedReportHelpers.SortedMetadata(source.GapCodes.Select(gap => Pair("gapCode", gap))));
    }

    private static string Classify(string label, IReadOnlyList<SnapshotDiffGap> gaps)
    {
        var labelGaps = gaps.Where(gap => gap.SourceLabel == label).ToArray();
        if (labelGaps.Length == 0)
        {
            return SnapshotDiffClassifications.ChangedEvidence;
        }

        if (labelGaps.Any(gap => gap.Classification == SnapshotDiffClassifications.UnknownAnalysisGap))
        {
            return SnapshotDiffClassifications.UnknownAnalysisGap;
        }

        if (labelGaps.Any(gap => gap.GapKind == "ReducedCoverage"))
        {
            return SnapshotDiffClassifications.ChangedWithReducedCoverage;
        }

        return SnapshotDiffClassifications.NeedsReview;
    }

    private static string ChangeType(SnapshotDiffSourceInfo? before, SnapshotDiffSourceInfo? after)
    {
        if (before is null)
        {
            return "added";
        }

        if (after is null)
        {
            return "removed";
        }

        return "changed";
    }

    private static string Confidence(string classification)
    {
        return classification switch
        {
            SnapshotDiffClassifications.Added or SnapshotDiffClassifications.Removed or SnapshotDiffClassifications.ChangedEvidence => "high",
            SnapshotDiffClassifications.ChangedWithReducedCoverage or SnapshotDiffClassifications.NeedsReview => "medium",
            _ => "low"
        };
    }

    private static void AddUnavailableGaps(IReadOnlyList<string> scopes, string kind, bool includePaths, List<SnapshotDiffGap> gaps)
    {
        AddUnavailable(scopes, "endpoints", "endpointDiffs", gaps);
        AddUnavailable(scopes, "contract-shapes", "contractShapeDiffs", gaps);
        AddUnavailable(scopes, "surfaces", "surfaceDiffs", gaps);
        AddUnavailable(scopes, "graph", "graphDiffs", gaps);
        AddUnavailable(scopes, "gaps", "gapDiffs", gaps);
        if (includePaths || scopes.Contains("paths", StringComparer.Ordinal))
        {
            if (kind == "single")
            {
                throw new ArgumentException("snapshot-diff --include-paths requires combined indexes.");
            }

            gaps.Add(Gap("schema", "UnavailableEvidence", "pathDiffs", null, SchemaRuleId, SnapshotDiffClassifications.UnknownAnalysisGap, "Path diff evidence is reserved for a later snapshot-diff implementation slice."));
        }
    }

    private static void AddUnavailable(IReadOnlyList<string> scopes, string scope, string section, List<SnapshotDiffGap> gaps)
    {
        if (scopes.Contains(scope, StringComparer.Ordinal))
        {
            gaps.Add(Gap("schema", "UnavailableEvidence", section, null, SchemaRuleId, SnapshotDiffClassifications.UnknownAnalysisGap, $"{section} evidence is reserved for a later snapshot-diff implementation slice."));
        }
    }

    private static string Rollup(int diffCount, IReadOnlyList<SnapshotDiffRow> rows, IReadOnlyList<SnapshotDiffGap> gaps)
    {
        if (gaps.Any(gap => gap.Classification == SnapshotDiffClassifications.UnknownAnalysisGap)
            || rows.Any(row => row.Classification == SnapshotDiffClassifications.UnknownAnalysisGap))
        {
            return SnapshotDiffClassifications.UnknownAnalysisGap;
        }

        if (gaps.Any(gap => gap.Classification == SnapshotDiffClassifications.TruncatedByLimit))
        {
            return SnapshotDiffClassifications.TruncatedByLimit;
        }

        if (gaps.Any(gap => gap.Classification == SnapshotDiffClassifications.NeedsReview || gap.Classification == SnapshotDiffClassifications.SelectorNoMatch)
            || rows.Any(row => row.Classification == SnapshotDiffClassifications.NeedsReview || row.Classification == SnapshotDiffClassifications.ChangedWithReducedCoverage))
        {
            return SnapshotDiffClassifications.NeedsReview;
        }

        return diffCount == 0
            ? SnapshotDiffClassifications.NoSnapshotDiffEvidence
            : SnapshotDiffClassifications.ChangedEvidence;
    }

    private static SnapshotDiffGap Gap(string idKind, string gapKind, string section, string? sourceLabel, string ruleId, string classification, string message)
    {
        return new SnapshotDiffGap(
            StableId("gap", idKind, gapKind, section, sourceLabel ?? "all"),
            gapKind,
            section,
            sourceLabel,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            classification,
            message,
            [],
            [],
            CombinedReportHelpers.SortedMetadata([
                Pair("section", section),
                Pair("sourceLabel", sourceLabel)
            ]));
    }

    private static string RenderMarkdown(SnapshotDiffDocument report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Snapshot Diff Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Report coverage: `{report.ReportCoverage}`");
        builder.AppendLine($"- Rollup: `{report.Summary.RollupClassification}`");
        builder.AppendLine($"- Sources compared: `{report.Summary.SourceCount}`");
        builder.AppendLine($"- Source diffs: `{report.Summary.SourceDiffCount}`");
        builder.AppendLine($"- Coverage diffs: `{report.Summary.CoverageDiffCount}`");
        builder.AppendLine($"- Extractor version diffs: `{report.Summary.ExtractorVersionDiffCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        builder.AppendLine();
        builder.AppendLine("## Query");
        builder.AppendLine();
        builder.AppendLine($"- Scopes: `{string.Join(",", report.Query.Scopes)}`");
        builder.AppendLine($"- Include paths: `{report.Query.IncludePaths}`");
        builder.AppendLine($"- Source selector: `{CombinedReportHelpers.Cell(report.Query.Source ?? "n/a")}`");
        builder.AppendLine();
        AppendSnapshot(builder, report.BeforeSnapshot);
        AppendSnapshot(builder, report.AfterSnapshot);
        AppendRows(builder, "Source And Coverage Changes", report.SourceDiffs.Concat(report.CoverageDiffs).ToArray());
        AppendRows(builder, "Endpoint Changes", report.EndpointDiffs);
        AppendRows(builder, "Contract Shape Changes", report.ContractShapeDiffs);
        AppendRows(builder, "Surface Changes", report.SurfaceDiffs);
        AppendRows(builder, "Graph Changes", report.GraphDiffs);
        AppendRows(builder, "Analysis Gap Changes", report.GapDiffs);
        AppendRows(builder, "Extractor Version Changes", report.ExtractorVersionDiffs);
        AppendRows(builder, "Path Changes", report.PathDiffs);
        AppendGaps(builder, report.Gaps);
        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations)
        {
            builder.AppendLine($"- {CombinedReportHelpers.Cell(limitation)}");
        }

        return builder.ToString();
    }

    private static void AppendSnapshot(StringBuilder builder, SnapshotDiffSnapshot snapshot)
    {
        builder.AppendLine($"## {snapshot.Side} Snapshot Identity");
        builder.AppendLine();
        builder.AppendLine("| Source | Language | Commit | Coverage | Build | Analysis | Repo identity |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var source in snapshot.Sources.OrderBy(source => source.SourceLabel, StringComparer.Ordinal))
        {
            builder.AppendLine($"| {CombinedReportHelpers.Cell(source.SourceLabel)} | {CombinedReportHelpers.Cell(source.Language ?? "unknown")} | {CombinedReportHelpers.Cell(source.CommitSha ?? "unknown")} | {CombinedReportHelpers.Cell(source.Coverage)} | {CombinedReportHelpers.Cell(source.BuildStatus)} | {CombinedReportHelpers.Cell(source.AnalysisLevel)} | {CombinedReportHelpers.Cell(source.RepositoryIdentityHash ?? "unknown")} |");
        }

        builder.AppendLine();
    }

    private static void AppendRows(StringBuilder builder, string title, IReadOnlyList<SnapshotDiffRow> rows)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        if (rows.Count == 0)
        {
            builder.AppendLine("_No rows emitted for this section._");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Diff | Source | Change | Classification | Evidence | Rules | Caveats |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var row in rows)
        {
            builder.AppendLine($"| {CombinedReportHelpers.Cell(row.DiffId)} | {CombinedReportHelpers.Cell(row.SourceLabel ?? "n/a")} | {CombinedReportHelpers.Cell(row.ChangeType)} | {CombinedReportHelpers.Cell(row.Classification)} | {CombinedReportHelpers.Cell(row.EvidenceKind)} | {CombinedReportHelpers.Cell(string.Join(",", row.RuleIds))} | {CombinedReportHelpers.Cell(string.Join(",", row.CoverageCaveats))} |");
        }

        builder.AppendLine();
    }

    private static void AppendGaps(StringBuilder builder, IReadOnlyList<SnapshotDiffGap> gaps)
    {
        builder.AppendLine("## Gaps");
        builder.AppendLine();
        if (gaps.Count == 0)
        {
            builder.AppendLine("_No gaps emitted._");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Gap | Section | Source | Classification | Rule | Message |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var gap in gaps)
        {
            builder.AppendLine($"| {CombinedReportHelpers.Cell(gap.GapKind)} | {CombinedReportHelpers.Cell(gap.Section)} | {CombinedReportHelpers.Cell(gap.SourceLabel ?? "n/a")} | {CombinedReportHelpers.Cell(gap.Classification)} | {CombinedReportHelpers.Cell(gap.RuleId)} | {CombinedReportHelpers.Cell(gap.Message)} |");
        }

        builder.AppendLine();
    }

    private static IReadOnlyList<string> CoverageWarnings(IReadOnlyList<SnapshotDiffSourceInfo> sources)
    {
        return sources
            .Where(source => !string.Equals(source.Coverage, "Full", StringComparison.Ordinal))
            .Select(source => $"{source.SourceLabel}: {source.AnalysisLevel}/{source.BuildStatus}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string CoverageFrom(string analysisLevel, string buildStatus, IReadOnlyList<string> gaps)
    {
        return analysisLevel.Contains("Reduced", StringComparison.OrdinalIgnoreCase)
            || buildStatus.Contains("Failed", StringComparison.OrdinalIgnoreCase)
            || buildStatus.Contains("Partial", StringComparison.OrdinalIgnoreCase)
            || gaps.Count > 0
            ? "Reduced"
            : "Full";
    }

    private static string? RepositoryIdentityHash(string? remoteUrl, string? repoName)
    {
        var value = NullIfUnknown(remoteUrl) ?? NullIfUnknown(repoName);
        return value is null ? null : $"repo-hash:{CombinedReportHelpers.Hash(value, 24)}";
    }

    private static string? NullIfUnknown(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            || value.Equals("n/a", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    private static string? InferLanguage(string? scannerVersion)
    {
        if (string.IsNullOrWhiteSpace(scannerVersion))
        {
            return null;
        }

        if (scannerVersion.Contains("typescript", StringComparison.OrdinalIgnoreCase))
        {
            return "typescript";
        }

        if (scannerVersion.Contains("jvm", StringComparison.OrdinalIgnoreCase) || scannerVersion.Contains("java", StringComparison.OrdinalIgnoreCase))
        {
            return "jvm";
        }

        if (scannerVersion.Contains("python", StringComparison.OrdinalIgnoreCase))
        {
            return "python";
        }

        return scannerVersion.Contains("tracemap", StringComparison.OrdinalIgnoreCase) ? "csharp" : null;
    }

    private static ScanManifest? DeserializeManifest(string? manifestJson)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScanManifest>(manifestJson, CombinedDependencyReporter.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = $table;";
        command.Parameters.AddWithValue("$table", tableName);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static string ReadOnlyConnectionString(string path)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        }.ToString();
    }

    private static string? StringOrNull(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static string StringOrDefault(SqliteDataReader reader, int ordinal, string defaultValue)
    {
        return StringOrNull(reader, ordinal) ?? defaultValue;
    }

    private static string StableId(params string?[] parts)
    {
        return CombinedReportHelpers.Hash(string.Join(":", parts.Where(part => !string.IsNullOrWhiteSpace(part))), 16);
    }

    private static KeyValuePair<string, string?> Pair(string key, string? value)
    {
        return new KeyValuePair<string, string?>(key, value);
    }
}
