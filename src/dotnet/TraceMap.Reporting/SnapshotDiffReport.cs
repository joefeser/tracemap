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
    IReadOnlyList<SnapshotDiffFileSpan> FileSpans,
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
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<SnapshotDiffFileSpan> FileSpans,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

internal sealed record SnapshotIndexInfo(string Kind, SnapshotDiffSnapshot Snapshot, IReadOnlyList<SnapshotDiffGap> Gaps);

internal sealed record SnapshotSourcePair(string Label, SnapshotDiffSourceInfo? Before, SnapshotDiffSourceInfo? After);

internal sealed record SnapshotFactRow(
    string FactId,
    string ScanId,
    string Repo,
    string CommitSha,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string? SourceSymbol,
    string? TargetSymbol,
    string? ContractElement,
    string FilePath,
    int StartLine,
    int EndLine,
    IReadOnlyDictionary<string, string> Properties);

internal sealed record SnapshotComparableRecord(
    string EvidenceKind,
    string StableKey,
    string MetadataHash,
    string EvidenceHash,
    SnapshotDiffEvidence Evidence,
    string RuleId,
    string EvidenceTier,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> CoverageCaveats,
    IReadOnlyList<string> Notes,
    bool NeedsReview);

internal sealed record SnapshotSingleDiffs(
    IReadOnlyList<SnapshotDiffRow> EndpointDiffs,
    IReadOnlyList<SnapshotDiffRow> SurfaceDiffs,
    IReadOnlyList<SnapshotDiffRow> GapDiffs);

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
        "Combined-index endpoint, surface, graph, and path evidence is delegated to the existing combined diff engine when requested; single-index graph and contract-shape projectors remain explicit availability gaps until deeper evidence readers are implemented.",
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
        var scopes = ParseScopes(options.Scope, options.IncludePaths);
        ValidateSelectorCompatibility(options, scopes);

        var before = await ReadIndexInfoAsync(options.BeforePath, "before", cancellationToken);
        var after = await ReadIndexInfoAsync(options.AfterPath, "after", cancellationToken);
        if (!string.Equals(before.Kind, after.Kind, StringComparison.Ordinal))
        {
            throw new InvalidDataException("snapshot-diff requires both inputs to be the same TraceMap index kind; mixed single and combined indexes are not supported.");
        }

        var allGaps = new List<SnapshotDiffGap>();
        allGaps.AddRange(FilterGapsBySource(before.Gaps, options.Source));
        allGaps.AddRange(FilterGapsBySource(after.Gaps, options.Source));
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

        var combinedReport = before.Kind == "combined"
            ? await BuildCombinedDelegatedReportAsync(options, scopes, cancellationToken)
            : null;
        var singleDiffs = before.Kind == "single"
            ? await BuildSingleDiffsAsync(options, scopes, sourcePairs, allGaps, cancellationToken)
            : null;

        IReadOnlyList<SnapshotDiffRow> sourceDiffs = combinedReport is not null && scopes.Contains("sources", StringComparer.Ordinal)
            ? MapCombinedRows(combinedReport.SourceDiffs, "source").ToArray()
            : scopes.Contains("sources", StringComparer.Ordinal)
            ? BuildSourceDiffs(sourcePairs, allGaps, options.MaxDiffRows)
            : [];
        IReadOnlyList<SnapshotDiffRow> coverageDiffs = combinedReport is not null && scopes.Contains("coverage", StringComparer.Ordinal)
            ? MapCombinedRows(combinedReport.CoverageDiffs, "coverage").ToArray()
            : scopes.Contains("coverage", StringComparer.Ordinal)
            ? BuildCoverageDiffs(sourcePairs, allGaps, options.MaxDiffRows)
            : [];
        IReadOnlyList<SnapshotDiffRow> endpointDiffs = combinedReport is not null && scopes.Contains("endpoints", StringComparer.Ordinal)
            ? MapCombinedRows(combinedReport.EndpointDiffs, "endpoint").ToArray()
            : singleDiffs is not null && scopes.Contains("endpoints", StringComparer.Ordinal)
            ? singleDiffs.EndpointDiffs
            : [];
        IReadOnlyList<SnapshotDiffRow> surfaceDiffs = combinedReport is not null && scopes.Contains("surfaces", StringComparer.Ordinal)
            ? MapCombinedRows(combinedReport.SurfaceDiffs, "surface").ToArray()
            : singleDiffs is not null && scopes.Contains("surfaces", StringComparer.Ordinal)
            ? singleDiffs.SurfaceDiffs
            : [];
        IReadOnlyList<SnapshotDiffRow> graphDiffs = combinedReport is not null && scopes.Contains("graph", StringComparer.Ordinal)
            ? MapCombinedRows(combinedReport.EdgeDiffs, "graph").ToArray()
            : [];
        IReadOnlyList<SnapshotDiffRow> pathDiffs = combinedReport is not null && scopes.Contains("paths", StringComparer.Ordinal)
            ? MapCombinedPathRows(combinedReport.PathDiffs).ToArray()
            : [];
        IReadOnlyList<SnapshotDiffRow> gapDiffs = singleDiffs is not null && scopes.Contains("gaps", StringComparer.Ordinal)
            ? singleDiffs.GapDiffs
            : [];
        IReadOnlyList<SnapshotDiffRow> extractorVersionDiffs = scopes.Contains("extractors", StringComparer.Ordinal)
            ? BuildExtractorDiffs(sourcePairs, allGaps, options.MaxDiffRows)
            : [];

        if (combinedReport is not null)
        {
            allGaps.AddRange(MapCombinedGaps(combinedReport.Gaps));
        }

        AddUnavailableGaps(scopes, before.Kind, options.IncludePaths, combinedReport is not null, singleDiffs is not null, allGaps);
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

        var diffCount = sourceDiffs.Count + coverageDiffs.Count + endpointDiffs.Count + surfaceDiffs.Count + graphDiffs.Count + gapDiffs.Count + pathDiffs.Count + extractorVersionDiffs.Count;
        var allRows = sourceDiffs.Concat(coverageDiffs).Concat(endpointDiffs).Concat(surfaceDiffs).Concat(graphDiffs).Concat(gapDiffs).Concat(pathDiffs).Concat(extractorVersionDiffs).ToArray();
        var rollup = Rollup(diffCount, allRows, gaps);
        var coverage = gaps.Any(gap => gap.Classification == SnapshotDiffClassifications.UnknownAnalysisGap || gap.GapKind == "UnavailableEvidence" || gap.GapKind == "ReducedCoverage")
            || allRows.Any(row => row.Classification == SnapshotDiffClassifications.UnknownAnalysisGap || row.CoverageCaveats.Contains("AnalysisGap", StringComparer.Ordinal))
            ? "Partial"
            : "Full";
        var summary = new SnapshotDiffSummary(
            rollup,
            EvidenceRuleId,
            sourcePairs.Count,
            sourceDiffs.Count,
            coverageDiffs.Count,
            endpointDiffs.Count,
            0,
            surfaceDiffs.Count,
            graphDiffs.Count,
            gapDiffs.Count,
            extractorVersionDiffs.Count,
            pathDiffs.Count,
            gaps.Length,
            gapsTruncated || gaps.Any(gap => gap.GapKind == "TruncatedByLimit"),
            diffCount == 0
                ? "No implemented snapshot-diff evidence rows changed for the selected scopes."
                : "Snapshot evidence changed for one or more selected scopes.");

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
            endpointDiffs,
            [],
            surfaceDiffs,
            graphDiffs,
            gapDiffs,
            extractorVersionDiffs,
            pathDiffs,
            gaps,
            DefaultLimitations.Concat(combinedReport?.Limitations ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
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

    private static IReadOnlyList<string> ParseScopes(string? scope, bool includePaths)
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
            return includePaths || scopes.Contains("paths", StringComparer.Ordinal)
                ? ["all", "sources", "coverage", "endpoints", "contract-shapes", "surfaces", "graph", "gaps", "extractors", "paths"]
                : ["all", "sources", "coverage", "endpoints", "contract-shapes", "surfaces", "graph", "gaps", "extractors"];
        }

        var unknown = scopes.FirstOrDefault(value => !ValidScopes.Contains(value));
        if (unknown is not null)
        {
            throw new ArgumentException($"snapshot-diff --scope contains unsupported value `{unknown}`.");
        }

        return scopes;
    }

    private static async Task<CombinedDependencyDiffReport?> BuildCombinedDelegatedReportAsync(SnapshotDiffOptions options, IReadOnlyList<string> scopes, CancellationToken cancellationToken)
    {
        var combinedScopes = MapCombinedScopes(scopes);
        if (combinedScopes.Count == 0)
        {
            return null;
        }

        return await CombinedDependencyDiffer.BuildReportAsync(
            new CombinedDependencyDiffOptions(
                options.BeforePath,
                options.AfterPath,
                options.OutputPath,
                "json",
                string.Join(",", combinedScopes),
                options.IncludePaths && combinedScopes.Contains("paths", StringComparer.Ordinal),
                options.AllowIdentityMismatch,
                options.ExitCode,
                options.Source,
                options.Endpoint,
                options.Surface,
                options.SurfaceName,
                options.MaxDepth,
                options.MaxPaths,
                options.MaxFrontier,
                options.MaxDiffRows,
                options.MaxGaps),
            cancellationToken);
    }

    private static IReadOnlyList<string> MapCombinedScopes(IReadOnlyList<string> scopes)
    {
        var mapped = new List<string>();
        if (scopes.Contains("sources", StringComparer.Ordinal) || scopes.Contains("coverage", StringComparer.Ordinal) || scopes.Contains("gaps", StringComparer.Ordinal))
        {
            mapped.Add("sources");
        }

        if (scopes.Contains("endpoints", StringComparer.Ordinal))
        {
            mapped.Add("endpoints");
        }

        if (scopes.Contains("surfaces", StringComparer.Ordinal))
        {
            mapped.Add("surfaces");
        }

        if (scopes.Contains("graph", StringComparer.Ordinal))
        {
            mapped.Add("edges");
        }

        if (scopes.Contains("paths", StringComparer.Ordinal))
        {
            mapped.Add("paths");
        }

        return mapped.Distinct(StringComparer.Ordinal).OrderBy(ScopeRank).ToArray();
    }

    private static int ScopeRank(string scope)
    {
        return scope switch
        {
            "sources" => 0,
            "endpoints" => 1,
            "surfaces" => 2,
            "edges" => 3,
            "paths" => 4,
            _ => 99
        };
    }

    private static void ValidateSelectorCompatibility(SnapshotDiffOptions options, IReadOnlyList<string> scopes)
    {
        if (scopes.Contains("paths", StringComparer.Ordinal) && !options.IncludePaths)
        {
            throw new ArgumentException("snapshot-diff --scope paths requires --include-paths.");
        }

        if (ParseEndpointSelector(options.Endpoint) is { } selector
            && (string.IsNullOrWhiteSpace(selector.Method) || string.IsNullOrWhiteSpace(selector.PathKey)))
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
            var gaps = new List<SnapshotDiffGap>();
            return new SnapshotIndexInfo("combined", await ReadCombinedSnapshotAsync(connection, side, gaps, cancellationToken), gaps);
        }

        if (hasScanManifest && hasFacts)
        {
            var gaps = new List<SnapshotDiffGap>();
            return new SnapshotIndexInfo("single", await ReadSingleSnapshotAsync(connection, side, gaps, cancellationToken), gaps);
        }

        var missing = !hasScanManifest && !hasSources ? "scan_manifest/index_sources" : !hasFacts && !hasCombinedFacts ? "facts/combined_facts" : "TraceMap index tables";
        throw new InvalidDataException($"{side} input is not a valid TraceMap index; missing {missing}.");
    }

    private static async Task<SnapshotDiffSnapshot> ReadCombinedSnapshotAsync(SqliteConnection connection, string side, List<SnapshotDiffGap> gaps, CancellationToken cancellationToken)
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
            var manifest = DeserializeManifest(StringOrNull(reader, 11), out var malformedManifest);
            var label = StringOrDefault(reader, 0, "unknown");
            if (malformedManifest)
            {
                gaps.Add(MalformedMetadataGap(side, "index_sources.manifest_json", label, null));
            }

            var scannerVersion = StringOrDefault(reader, 10, "unknown");
            extractorVersions[$"{label}:{scannerVersion}"] = scannerVersion;
            var knownGaps = manifest?.KnownGaps ?? [];
            var analysisLevel = StringOrDefault(reader, 8, "unknown");
            var buildStatus = StringOrDefault(reader, 9, "unknown");
            sources.Add(new SnapshotDiffSourceInfo(
                label,
                StringOrNull(reader, 1) ?? InferLanguage(scannerVersion),
                StringOrNull(reader, 2),
                NullIfUnknown(StringOrNull(reader, 3)),
                RepositoryIdentityHash(StringOrNull(reader, 5), StringOrNull(reader, 4)),
                StringOrNull(reader, 6),
                CoverageFrom(analysisLevel, buildStatus, knownGaps),
                buildStatus,
                analysisLevel,
                scannerVersion,
                knownGaps.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
        }

        var warnings = CoverageWarnings(sources);
        return new SnapshotDiffSnapshot(side, "combined", sources, warnings.Count == 0 ? "Full" : "Reduced", warnings, extractorVersions.ToArray());
    }

    private static async Task<SnapshotDiffSnapshot> ReadSingleSnapshotAsync(SqliteConnection connection, string side, List<SnapshotDiffGap> gaps, CancellationToken cancellationToken)
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

        var manifest = DeserializeManifest(StringOrNull(reader, 6), out var malformedManifest);
        if (malformedManifest)
        {
            gaps.Add(MalformedMetadataGap(side, "scan_manifest.manifest_json", "single", null));
        }

        var knownGaps = manifest?.KnownGaps ?? [];
        var scannerVersion = StringOrDefault(reader, 3, "unknown");
        var analysisLevel = StringOrDefault(reader, 4, "unknown");
        var buildStatus = StringOrDefault(reader, 5, "unknown");
        var source = new SnapshotDiffSourceInfo(
            "single",
            InferLanguage(scannerVersion),
            StringOrNull(reader, 0),
            NullIfUnknown(StringOrNull(reader, 2)),
            malformedManifest ? null : RepositoryIdentityHash(manifest?.RemoteUrl, StringOrNull(reader, 1)),
            manifest?.ScanRootPathHash,
            CoverageFrom(analysisLevel, buildStatus, knownGaps),
            buildStatus,
            analysisLevel,
            scannerVersion,
            knownGaps.OrderBy(value => value, StringComparer.Ordinal).ToArray());
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

    private static IReadOnlyList<SnapshotDiffGap> FilterGapsBySource(IReadOnlyList<SnapshotDiffGap> gaps, string? sourceSelector)
    {
        if (string.IsNullOrWhiteSpace(sourceSelector))
        {
            return gaps;
        }

        return gaps
            .Where(gap => string.IsNullOrWhiteSpace(gap.SourceLabel) || string.Equals(gap.SourceLabel, sourceSelector, StringComparison.Ordinal))
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
            if (string.IsNullOrWhiteSpace(pair.Before.RepositoryIdentityHash) || string.IsNullOrWhiteSpace(pair.After.RepositoryIdentityHash))
            {
                gaps.Add(Gap("identity", "SourceIdentityUnverified", "sources", pair.Label, IdentityRuleId, SnapshotDiffClassifications.UnknownAnalysisGap, $"Source `{pair.Label}` is missing repository identity metadata on one or both sides."));
            }

            if ((!string.IsNullOrWhiteSpace(pair.Before.RepositoryIdentityHash)
                    && !string.IsNullOrWhiteSpace(pair.After.RepositoryIdentityHash)
                    && !string.Equals(pair.Before.RepositoryIdentityHash, pair.After.RepositoryIdentityHash, StringComparison.Ordinal))
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

    private static async Task<SnapshotSingleDiffs> BuildSingleDiffsAsync(
        SnapshotDiffOptions options,
        IReadOnlyList<string> scopes,
        IReadOnlyList<SnapshotSourcePair> sourcePairs,
        List<SnapshotDiffGap> gaps,
        CancellationToken cancellationToken)
    {
        if (!sourcePairs.Any(pair => pair.Label == "single" && (pair.Before is not null || pair.After is not null)))
        {
            return new SnapshotSingleDiffs([], [], []);
        }

        var includeEndpoints = scopes.Contains("endpoints", StringComparer.Ordinal);
        var includeSurfaces = scopes.Contains("surfaces", StringComparer.Ordinal);
        var includeGaps = scopes.Contains("gaps", StringComparer.Ordinal);
        if (!includeEndpoints && !includeSurfaces && !includeGaps)
        {
            return new SnapshotSingleDiffs([], [], []);
        }

        var sourcePair = sourcePairs.First(pair => pair.Label == "single");
        var endpointSelector = includeEndpoints ? ParseEndpointSelector(options.Endpoint) : null;
        var beforeFacts = await ReadSingleFactsAsync(options.BeforePath, "before", gaps, cancellationToken);
        var afterFacts = await ReadSingleFactsAsync(options.AfterPath, "after", gaps, cancellationToken);

        var beforeEndpoints = includeEndpoints ? ProjectSingleEndpoints(beforeFacts, sourcePair.Before, endpointSelector) : [];
        var afterEndpoints = includeEndpoints ? ProjectSingleEndpoints(afterFacts, sourcePair.After, endpointSelector) : [];
        if (includeEndpoints && endpointSelector is not null && beforeEndpoints.Count == 0 && afterEndpoints.Count == 0)
        {
            gaps.Add(Gap("selector", "SelectorNoMatch", "endpointDiffs", sourcePair.Label, EvidenceRuleId, SnapshotDiffClassifications.SelectorNoMatch, $"Endpoint selector `{options.Endpoint}` matched no single-index endpoint evidence."));
        }

        var beforeSurfaces = includeSurfaces ? ProjectSingleSurfaces(beforeFacts, sourcePair.Before, options.Surface, options.SurfaceName) : [];
        var afterSurfaces = includeSurfaces ? ProjectSingleSurfaces(afterFacts, sourcePair.After, options.Surface, options.SurfaceName) : [];
        if (includeSurfaces
            && (!string.IsNullOrWhiteSpace(options.Surface) || !string.IsNullOrWhiteSpace(options.SurfaceName))
            && beforeSurfaces.Count == 0
            && afterSurfaces.Count == 0)
        {
            gaps.Add(Gap("selector", "SelectorNoMatch", "surfaceDiffs", sourcePair.Label, EvidenceRuleId, SnapshotDiffClassifications.SelectorNoMatch, "Surface selector matched no single-index surface evidence."));
        }

        var endpointDiffs = includeEndpoints
            ? CompareSingleRecords(beforeEndpoints, afterEndpoints, "endpointDiffs", sourcePair, gaps, options.MaxDiffRows)
            : [];
        var surfaceDiffs = includeSurfaces
            ? CompareSingleRecords(beforeSurfaces, afterSurfaces, "surfaceDiffs", sourcePair, gaps, options.MaxDiffRows)
            : [];
        var beforeGaps = includeGaps ? ProjectSingleAnalysisGaps(beforeFacts, sourcePair.Before) : [];
        var afterGaps = includeGaps ? ProjectSingleAnalysisGaps(afterFacts, sourcePair.After) : [];
        var gapDiffs = includeGaps
            ? CompareSingleRecords(beforeGaps, afterGaps, "gapDiffs", sourcePair, gaps, options.MaxDiffRows)
            : [];

        return new SnapshotSingleDiffs(endpointDiffs, surfaceDiffs, gapDiffs);
    }

    private static async Task<IReadOnlyList<SnapshotFactRow>> ReadSingleFactsAsync(string path, string side, List<SnapshotDiffGap> gaps, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ReadOnlyConnectionString(path));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select fact_id,
                   scan_id,
                   repo,
                   commit_sha,
                   fact_type,
                   rule_id,
                   evidence_tier,
                   source_symbol,
                   target_symbol,
                   contract_element,
                   file_path,
                   start_line,
                   end_line,
                   properties_json
            from facts
            order by file_path, start_line, fact_type, fact_id;
            """;
        var rows = new List<SnapshotFactRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var factId = reader.GetString(0);
            var properties = ParseProperties(StringOrDefault(reader, 13, "{}"), out var malformed);
            if (malformed)
            {
                gaps.Add(MalformedMetadataGap(side, "facts.properties_json", "single", factId));
            }

            rows.Add(new SnapshotFactRow(
                factId,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                StringOrNull(reader, 7),
                StringOrNull(reader, 8),
                StringOrNull(reader, 9),
                reader.GetString(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                properties));
        }

        return rows;
    }

    private static IReadOnlyList<SnapshotComparableRecord> ProjectSingleEndpoints(
        IReadOnlyList<SnapshotFactRow> facts,
        SnapshotDiffSourceInfo? source,
        (string Method, string PathKey)? endpointSelector)
    {
        if (source is null)
        {
            return [];
        }

        return facts
            .Where(fact => fact.FactType is FactTypes.HttpRouteBinding or FactTypes.HttpCallDetected)
            .Select(fact => ProjectSingleEndpoint(fact, source))
            .OfType<SnapshotComparableRecord>()
            .Where(record => endpointSelector is null || EndpointSelectorMatches(record, endpointSelector.Value))
            .OrderBy(record => record.StableKey, StringComparer.Ordinal)
            .ThenBy(record => record.EvidenceHash, StringComparer.Ordinal)
            .ToArray();
    }

    private static SnapshotComparableRecord? ProjectSingleEndpoint(SnapshotFactRow fact, SnapshotDiffSourceInfo source)
    {
        var endpointKind = fact.FactType == FactTypes.HttpRouteBinding ? "http-route" : "http-client";
        var method = NormalizeEndpointMethod(FirstValue(fact.Properties, "httpMethod", "httpMethods", "methodName") ?? fact.ContractElement);
        var rawPathKey = FirstValue(fact.Properties, "normalizedPathKey");
        var pathKey = NullIfUnknown(rawPathKey) ?? SafeFallbackIdentity("path", FirstValue(fact.Properties, "normalizedPathTemplate") ?? fact.TargetSymbol ?? fact.ContractElement ?? fact.FactId);
        var handlerIdentity = fact.SourceSymbol ?? fact.TargetSymbol;
        var handlerIdentityOrNone = NullIfUnknown(handlerIdentity) ?? "none";
        var stableKey = $"endpoint:{source.SourceLabel}:{endpointKind}:{method}:{pathKey}:{handlerIdentityOrNone}";
        var metadata = CombinedReportHelpers.SortedMetadata([
            Pair("endpointKind", endpointKind),
            Pair("httpMethod", method),
            Pair("normalizedPathKey", pathKey),
            Pair("handlerIdentity", handlerIdentityOrNone),
            Pair("pathIdentityFallback", rawPathKey is null ? "true" : null),
            Pair("factType", fact.FactType),
            Pair("ruleId", fact.RuleId),
            Pair("evidenceTier", fact.EvidenceTier)
        ]);
        var evidence = SingleEvidence(source, fact, metadata);
        return new SnapshotComparableRecord(
            "endpoint",
            stableKey,
            MetadataHash(metadata),
            EvidenceHash(evidence),
            evidence,
            fact.RuleId,
            fact.EvidenceTier,
            [fact.FactId],
            [],
            rawPathKey is null ? ["FallbackEndpointIdentity"] : [],
            rawPathKey is null ? ["FallbackEndpointIdentity: endpoint path identity used a deterministic hash because normalizedPathKey was unavailable."] : [],
            fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual || rawPathKey is null);
    }

    private static bool EndpointSelectorMatches(SnapshotComparableRecord record, (string Method, string PathKey) selector)
    {
        var method = record.Evidence.Metadata.FirstOrDefault(pair => pair.Key == "httpMethod").Value;
        var pathKey = record.Evidence.Metadata.FirstOrDefault(pair => pair.Key == "normalizedPathKey").Value;
        return CombinedDependencyReporter.MethodsCompatible(selector.Method, method)
            && string.Equals(pathKey, selector.PathKey, StringComparison.Ordinal);
    }

    private static IReadOnlyList<SnapshotComparableRecord> ProjectSingleSurfaces(
        IReadOnlyList<SnapshotFactRow> facts,
        SnapshotDiffSourceInfo? source,
        string? surfaceKind,
        string? surfaceName)
    {
        if (source is null)
        {
            return [];
        }

        var combinedFacts = facts
            .Select(fact => new CombinedFactRow(
                fact.FactId,
                "single",
                source.SourceLabel,
                fact.FactId,
                fact.ScanId,
                fact.Repo,
                fact.CommitSha,
                fact.FactType,
                fact.RuleId,
                fact.EvidenceTier,
                fact.SourceSymbol,
                fact.TargetSymbol,
                fact.ContractElement,
                fact.FilePath,
                fact.StartLine,
                fact.EndLine,
                fact.Properties))
            .ToArray();
        return CombinedDependencyReporter.BuildSurfaces(combinedFacts)
            .Where(surface => string.IsNullOrWhiteSpace(surfaceKind) || string.Equals(surface.SurfaceKind, surfaceKind.Trim(), StringComparison.Ordinal))
            .Where(surface => string.IsNullOrWhiteSpace(surfaceName) || SingleSurfaceNameMatches(surface, surfaceName.Trim()))
            .Select(surface => ProjectSingleSurface(surface, source))
            .OrderBy(record => record.StableKey, StringComparer.Ordinal)
            .ThenBy(record => record.EvidenceHash, StringComparer.Ordinal)
            .ToArray();
    }

    private static SnapshotComparableRecord ProjectSingleSurface(CombinedDependencySurfaceRow surface, SnapshotDiffSourceInfo source)
    {
        var metadata = SingleSurfaceMetadata(surface);
        var stableKey = $"surface:{source.SourceLabel}:{surface.SurfaceKind}:{MetadataHash(SingleSurfaceIdentityMetadata(surface))}";
        var caveats = SingleSurfaceCaveats(surface);
        var evidence = new SnapshotDiffEvidence(
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
            [new SnapshotDiffFileSpan(CombinedReportHelpers.SafePath(surface.FilePath), surface.StartLine, surface.EndLine, source.SourceLabel)],
            metadata);
        return new SnapshotComparableRecord(
            "surface",
            stableKey,
            MetadataHash(metadata),
            EvidenceHash(evidence),
            evidence,
            surface.RuleId,
            surface.EvidenceTier,
            [surface.OriginalFactId],
            [],
            caveats.Select(caveat => caveat.Code).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            caveats.Select(caveat => $"{caveat.Code}: {caveat.Message}").OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            surface.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual || caveats.Count > 0);
    }

    private static IReadOnlyList<SnapshotComparableRecord> ProjectSingleAnalysisGaps(
        IReadOnlyList<SnapshotFactRow> facts,
        SnapshotDiffSourceInfo? source)
    {
        if (source is null)
        {
            return [];
        }

        return facts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap)
            .Select(fact => ProjectSingleAnalysisGap(fact, source))
            .OrderBy(record => record.StableKey, StringComparer.Ordinal)
            .ThenBy(record => record.EvidenceHash, StringComparer.Ordinal)
            .ToArray();
    }

    private static SnapshotComparableRecord ProjectSingleAnalysisGap(SnapshotFactRow fact, SnapshotDiffSourceInfo source)
    {
        var gapKind = NullIfUnknown(FirstValue(fact.Properties, "gapKind", "gapCode", "kind", "reason", "dynamicReason", "sqlSourceKind")) ?? FactTypes.AnalysisGap;
        var safePath = CombinedReportHelpers.SafePath(fact.FilePath);
        var pathHash = CombinedReportHelpers.Hash(fact.FilePath, 24);
        var messageHash = FirstValue(fact.Properties, "message") is { } message
            ? CombinedReportHelpers.Hash(message, 24)
            : null;
        var metadata = CombinedReportHelpers.SortedMetadata([
            Pair("gapKind", gapKind),
            Pair("factType", fact.FactType),
            Pair("ruleId", fact.RuleId),
            Pair("evidenceTier", fact.EvidenceTier),
            Pair("safePath", safePath),
            Pair("pathHash", pathHash),
            Pair("startLine", fact.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Pair("endLine", fact.EndLine.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Pair("messageHash", messageHash)
        ]);
        var stableKey = $"gap:{source.SourceLabel}:{gapKind}:{safePath}:{fact.StartLine}:{fact.EndLine}:{fact.RuleId}";
        var evidence = SingleEvidence(source, fact, metadata);
        return new SnapshotComparableRecord(
            "gap",
            stableKey,
            MetadataHash(metadata),
            EvidenceHash(evidence),
            evidence,
            fact.RuleId,
            fact.EvidenceTier,
            [fact.FactId],
            [],
            ["AnalysisGap"],
            ["AnalysisGap: snapshot evidence includes an indexed analysis gap; conclusions remain review-oriented."],
            true);
    }

    private static IReadOnlyList<SnapshotDiffRow> CompareSingleRecords(
        IReadOnlyList<SnapshotComparableRecord> before,
        IReadOnlyList<SnapshotComparableRecord> after,
        string section,
        SnapshotSourcePair sourcePair,
        List<SnapshotDiffGap> gaps,
        int maxRows)
    {
        var rows = new List<SnapshotDiffRow>();
        var beforeGroups = before.GroupBy(record => record.StableKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var afterGroups = after.GroupBy(record => record.StableKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var duplicate in beforeGroups.Where(group => group.Value.Length > 1))
        {
            gaps.Add(DuplicateIdentityGap(section, "before", sourcePair.Label, duplicate.Key, duplicate.Value.SelectMany(record => record.SupportingFactIds)));
        }

        foreach (var duplicate in afterGroups.Where(group => group.Value.Length > 1))
        {
            gaps.Add(DuplicateIdentityGap(section, "after", sourcePair.Label, duplicate.Key, duplicate.Value.SelectMany(record => record.SupportingFactIds)));
        }

        foreach (var key in beforeGroups.Keys.Concat(afterGroups.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            beforeGroups.TryGetValue(key, out var beforeRows);
            afterGroups.TryGetValue(key, out var afterRows);
            var beforeRecord = beforeRows?.OrderBy(record => record.MetadataHash, StringComparer.Ordinal).FirstOrDefault();
            var afterRecord = afterRows?.OrderBy(record => record.MetadataHash, StringComparer.Ordinal).FirstOrDefault();
            if (beforeRecord is not null
                && afterRecord is not null
                && beforeRecord.MetadataHash == afterRecord.MetadataHash
                && beforeRecord.EvidenceHash == afterRecord.EvidenceHash
                && beforeRows!.Length == afterRows!.Length)
            {
                continue;
            }

            rows.Add(SingleRow(key, beforeRecord, afterRecord, sourcePair, gaps));
        }

        return TruncateRows(section, rows
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ThenBy(row => row.DiffId, StringComparer.Ordinal)
            .ToArray(), maxRows, gaps);
    }

    private static SnapshotDiffRow SingleRow(
        string stableKey,
        SnapshotComparableRecord? before,
        SnapshotComparableRecord? after,
        SnapshotSourcePair sourcePair,
        IReadOnlyList<SnapshotDiffGap> gaps)
    {
        var classification = SingleClassification(before, after, sourcePair, gaps);
        var sameSha = SameKnownCommitSha(sourcePair);
        var sameShaNote = sameSha
            ? "SameCommitShaDivergentEvidence: Indexed evidence changed while both snapshots report the same commit SHA; rule snapshot.diff.identity.v1."
            : null;
        var ruleIds = RuleIds(EvidenceRuleId, before?.RuleId, after?.RuleId, sameSha ? IdentityRuleId : null);
        var evidenceTiers = EvidenceTiersFor(before?.EvidenceTier, after?.EvidenceTier);
        var sourceGaps = gaps.Where(gap => gap.SourceLabel == sourcePair.Label).ToArray();
        var coverageCaveats = (before?.CoverageCaveats ?? [])
            .Concat(after?.CoverageCaveats ?? [])
            .Concat(sourceGaps.Select(gap => gap.GapKind))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var notes = (before?.Notes ?? [])
            .Concat(after?.Notes ?? [])
            .Concat(sourceGaps.Select(gap => gap.GapId))
            .Concat(sameShaNote is null ? [] : [sameShaNote])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new SnapshotDiffRow(
            StableId("diff", before?.EvidenceKind ?? after?.EvidenceKind, stableKey, classification),
            stableKey,
            ChangeType(before, after),
            classification,
            Confidence(classification),
            before?.EvidenceKind ?? after?.EvidenceKind ?? "evidence",
            sourcePair.Label,
            before?.Evidence,
            after?.Evidence,
            ruleIds,
            evidenceTiers,
            (before?.SupportingFactIds ?? []).Concat(after?.SupportingFactIds ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            (before?.SupportingEdgeIds ?? []).Concat(after?.SupportingEdgeIds ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            [],
            (before?.Evidence.FileSpans ?? []).Concat(after?.Evidence.FileSpans ?? []).GroupBy(span => $"{span.SourceLabel}:{span.FilePath}:{span.StartLine}:{span.EndLine}", StringComparer.Ordinal).Select(group => group.First()).OrderBy(span => span.FilePath, StringComparer.Ordinal).ThenBy(span => span.StartLine).ThenBy(span => span.EndLine).ToArray(),
            coverageCaveats,
            notes);
    }

    private static string SingleClassification(
        SnapshotComparableRecord? before,
        SnapshotComparableRecord? after,
        SnapshotSourcePair sourcePair,
        IReadOnlyList<SnapshotDiffGap> gaps)
    {
        var sourceClassification = Classify(sourcePair.Label, gaps);
        if ((before?.EvidenceKind ?? after?.EvidenceKind) == "gap")
        {
            return SnapshotDiffClassifications.UnknownAnalysisGap;
        }

        if (sourceClassification == SnapshotDiffClassifications.UnknownAnalysisGap)
        {
            return sourceClassification;
        }

        var sameSha = SameKnownCommitSha(sourcePair);
        var needsReview = before?.NeedsReview == true || after?.NeedsReview == true;
        if (before is null || after is null)
        {
            if (sameSha)
            {
                return needsReview ? SnapshotDiffClassifications.NeedsReview : SnapshotDiffClassifications.ChangedEvidence;
            }

            var coverageReduced = before is null
                ? !string.Equals(sourcePair.Before?.Coverage, "Full", StringComparison.Ordinal)
                : !string.Equals(sourcePair.After?.Coverage, "Full", StringComparison.Ordinal);
            if (coverageReduced)
            {
                return SnapshotDiffClassifications.ChangedWithReducedCoverage;
            }

            return needsReview
                ? SnapshotDiffClassifications.NeedsReview
                : before is null ? SnapshotDiffClassifications.Added : SnapshotDiffClassifications.Removed;
        }

        if (needsReview)
        {
            return SnapshotDiffClassifications.NeedsReview;
        }

        return sourceClassification;
    }

    private static bool SameKnownCommitSha(SnapshotSourcePair sourcePair)
    {
        return !string.IsNullOrWhiteSpace(sourcePair.Before?.CommitSha)
            && !string.IsNullOrWhiteSpace(sourcePair.After?.CommitSha)
            && string.Equals(sourcePair.Before.CommitSha, sourcePair.After.CommitSha, StringComparison.OrdinalIgnoreCase);
    }

    private static string ChangeType(SnapshotComparableRecord? before, SnapshotComparableRecord? after)
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

    private static SnapshotDiffEvidence SingleEvidence(
        SnapshotDiffSourceInfo source,
        SnapshotFactRow fact,
        IReadOnlyList<KeyValuePair<string, string>> metadata)
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
            [new SnapshotDiffFileSpan(CombinedReportHelpers.SafePath(fact.FilePath), fact.StartLine, fact.EndLine, source.SourceLabel)],
            metadata);
    }

    private static string NormalizeEndpointMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return "ANY";
        }

        return method.Trim().ToUpperInvariant();
    }

    private static (string Method, string PathKey)? ParseEndpointSelector(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var parts = endpoint.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException("snapshot-diff --endpoint must be formatted as `<METHOD> <normalized-path-key>`.");
        }

        return (NormalizeEndpointMethod(parts[0]), parts[1]);
    }

    private static string? FirstValue(IReadOnlyDictionary<string, string> properties, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string SafeFallbackIdentity(string prefix, string value)
    {
        return $"{prefix}-hash:{CombinedReportHelpers.Hash(value, 24)}";
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SingleSurfaceMetadata(CombinedDependencySurfaceRow surface)
    {
        return CombinedReportHelpers.SortedMetadata([
            Pair("surfaceKind", surface.SurfaceKind),
            Pair("httpMethod", surface.HttpMethod),
            Pair("normalizedPathKey", surface.NormalizedPathKey),
            Pair("operationName", surface.OperationName),
            Pair("tableName", surface.TableName),
            Pair("columnNames", surface.ColumnNames),
            Pair("sqlSourceKind", surface.SourceKind),
            Pair("queryShapeHash", surface.ShapeHash),
            Pair("textHash", surface.TextHash),
            Pair("textLength", surface.TextLength),
            Pair("ecosystem", surface.Ecosystem),
            Pair("manifestKind", surface.ManifestKind),
            Pair("packageName", surface.PackageName),
            Pair("packageManager", surface.PackageManager),
            Pair("dependencyScope", surface.DependencyScope),
            Pair("dependencyGroup", surface.DependencyGroup),
            Pair("version", surface.Version),
            Pair("versionHash", surface.VersionHash),
            Pair("redactionReason", surface.RedactionReason),
            Pair("configKey", surface.ConfigKey),
            Pair("factType", surface.FactType),
            Pair("ruleId", surface.RuleId),
            Pair("evidenceTier", surface.EvidenceTier),
            Pair("identityFallbackHash", IsVolatileSqlIdentity(surface) ? CombinedReportHelpers.Hash(surface.OriginalFactId ?? surface.CombinedFactId, 24) : null)
        ]);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SingleSurfaceIdentityMetadata(CombinedDependencySurfaceRow surface)
    {
        if (surface.SurfaceKind == "package-config" && !string.IsNullOrWhiteSpace(surface.PackageName))
        {
            return CombinedReportHelpers.SortedMetadata([
                Pair("surfaceKind", surface.SurfaceKind),
                Pair("ecosystem", surface.Ecosystem),
                Pair("manifestKind", surface.ManifestKind),
                Pair("packageName", surface.PackageName),
                Pair("manifestPath", surface.FilePath),
                Pair("configKey", surface.ConfigKey)
            ]);
        }

        return SingleSurfaceMetadata(surface);
    }

    private static IReadOnlyList<(string Code, string Message)> SingleSurfaceCaveats(CombinedDependencySurfaceRow surface)
    {
        if (surface.SurfaceKind == "package-config")
        {
            var caveats = new List<(string Code, string Message)>();
            if (!string.IsNullOrWhiteSpace(surface.VersionHash) && string.IsNullOrWhiteSpace(surface.Version))
            {
                caveats.Add(("HashOnlyEvidence", "Package version evidence is hash-only or redacted; diff classification remains review-tier."));
            }

            if (string.IsNullOrWhiteSpace(surface.PackageName) || string.IsNullOrWhiteSpace(surface.Ecosystem))
            {
                caveats.Add(("VolatileIdentity", "Package surface identity is missing package name or ecosystem metadata; diff classification remains review-tier."));
            }

            return caveats;
        }

        if (surface.SurfaceKind != "sql-query")
        {
            return [];
        }

        var sqlCaveats = new List<(string Code, string Message)>();
        if (IsHashOnlySqlEvidence(surface))
        {
            sqlCaveats.Add(("HashOnlyEvidence", "SQL surface has text hash evidence without credible shape metadata; diff classification remains review-tier."));
        }

        if (IsVolatileSqlIdentity(surface))
        {
            sqlCaveats.Add(("VolatileIdentity", "SQL surface identity fell back to a fact hash because stable SQL metadata was unavailable; diff classification remains review-tier."));
        }

        return sqlCaveats;
    }

    private static bool IsHashOnlySqlEvidence(CombinedDependencySurfaceRow surface)
    {
        return surface.SurfaceKind == "sql-query"
            && HasSqlValue(surface.TextHash)
            && !HasSqlValue(surface.ShapeHash)
            && !HasSqlValue(surface.OperationName)
            && !HasSqlValue(surface.TableName)
            && !HasSqlValue(surface.ColumnNames);
    }

    private static bool IsVolatileSqlIdentity(CombinedDependencySurfaceRow surface)
    {
        return surface.SurfaceKind == "sql-query"
            && !HasSqlValue(surface.ShapeHash)
            && !HasSqlValue(surface.TextHash)
            && !HasSqlValue(surface.OperationName)
            && !HasSqlValue(surface.TableName)
            && !HasSqlValue(surface.ColumnNames);
    }

    private static bool HasSqlValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && !value.Equals("n/a", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SingleSurfaceNameMatches(CombinedDependencySurfaceRow surface, string selector)
    {
        return string.Equals(surface.DisplayName, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(surface.PackageName, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(surface.ConfigKey, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(surface.TableName, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(surface.ShapeHash, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(surface.TextHash, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(surface.NormalizedPathKey, selector, StringComparison.OrdinalIgnoreCase);
    }

    private static string MetadataHash(IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        return CombinedReportHelpers.Hash(string.Join("\n", metadata.Select(pair => $"{pair.Key}={pair.Value}")));
    }

    private static string EvidenceHash(SnapshotDiffEvidence evidence)
    {
        var values = new List<string?>
        {
            evidence.SourceLabel,
            evidence.Language,
        };
        values.AddRange(evidence.FileSpans.Select(span => $"{span.SourceLabel}:{span.FilePath}:{span.StartLine}:{span.EndLine}"));
        values.AddRange(evidence.Metadata.Select(pair => $"{pair.Key}={pair.Value}"));
        return CombinedReportHelpers.Hash(string.Join("\n", values.Where(value => !string.IsNullOrWhiteSpace(value))));
    }

    private static SnapshotDiffGap DuplicateIdentityGap(string section, string side, string sourceLabel, string stableKey, IEnumerable<string> supportingFactIds)
    {
        return new SnapshotDiffGap(
            StableId("gap", "duplicate", section, side, sourceLabel, stableKey),
            "DuplicateIdentity",
            section,
            sourceLabel,
            EvidenceRuleId,
            EvidenceTiers.Tier4Unknown,
            SnapshotDiffClassifications.NeedsReview,
            $"Duplicate stable identity `{stableKey}` has multiple instances in one snapshot.",
            [],
            supportingFactIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            [],
            [],
            CombinedReportHelpers.SortedMetadata([
                Pair("section", section),
                Pair("side", side),
                Pair("sourceLabel", sourceLabel),
                Pair("stableKeyHash", CombinedReportHelpers.Hash(stableKey, 24))
            ]));
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
            [],
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
            SnapshotDiffClassifications.ChangedWithReducedCoverage => "medium",
            SnapshotDiffClassifications.NeedsReview => "review",
            _ => "unknown"
        };
    }

    private static IReadOnlyList<SnapshotDiffRow> MapCombinedRows(IReadOnlyList<CombinedDiffRow> rows, string evidenceKind)
    {
        return rows
            .Select(row =>
            {
                var beforeEvidence = row.Before is null ? null : MapCombinedEvidence(row.Before);
                var afterEvidence = row.After is null ? null : MapCombinedEvidence(row.After);
                var supportingFactIds = SupportingFactIds(row.Before, row.After);
                var supportingEdgeIds = SupportingEdgeIds(row.Before, row.After);
                var ruleIds = RuleIds(row.DiffRuleId, row.Before?.RuleId, row.After?.RuleId);
                var evidenceTiers = EvidenceTiersFor(row.Before?.EvidenceTier, row.After?.EvidenceTier);
                var fileSpans = FileSpans(row.Before, row.After);
                var coverageCaveats = row.CoverageCaveats
                    .Select(caveat => caveat.Code)
                    .Concat(row.Notes.Select(note => note.Code))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var notes = row.CoverageCaveats
                    .Select(caveat => $"{caveat.Code}: {caveat.Message}")
                    .Concat(row.Notes.Select(note => $"{note.Code}: {note.Message}"))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                return new SnapshotDiffRow(
                    StableId("diff", evidenceKind, row.StableKey, row.ChangeType, row.Classification),
                    row.StableKey,
                    row.ChangeType,
                    MapClassification(row.Classification),
                    row.Confidence,
                    evidenceKind,
                    row.Before?.SourceLabel ?? row.After?.SourceLabel,
                    beforeEvidence,
                    afterEvidence,
                    ruleIds,
                    evidenceTiers,
                    supportingFactIds,
                    supportingEdgeIds,
                    [],
                    fileSpans,
                    coverageCaveats,
                    notes);
            })
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ThenBy(row => row.DiffId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<SnapshotDiffRow> MapCombinedPathRows(IReadOnlyList<CombinedPathDiffRow> rows)
    {
        return rows
            .Select(row =>
            {
                var beforeEvidence = row.Before is null ? null : MapCombinedPathEvidence(row.Before);
                var afterEvidence = row.After is null ? null : MapCombinedPathEvidence(row.After);
                var supportingFactIds = (row.Before?.SupportingFactIds ?? Array.Empty<string>())
                    .Concat(row.After?.SupportingFactIds ?? Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var supportingEdgeIds = (row.Before?.SupportingEdgeIds ?? Array.Empty<string>())
                    .Concat(row.After?.SupportingEdgeIds ?? Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var supportingPathIds = new[] { row.Before?.PathId, row.After?.PathId }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var coverageCaveats = row.CoverageCaveats
                    .Select(caveat => caveat.Code)
                    .Concat(row.Notes.Select(note => note.Code))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var notes = row.CoverageCaveats
                    .Select(caveat => $"{caveat.Code}: {caveat.Message}")
                    .Concat(row.Notes.Select(note => $"{note.Code}: {note.Message}"))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var fileSpans = PathFileSpans(row.Before, row.After);
                return new SnapshotDiffRow(
                    StableId("diff", "path", row.PathSignature, row.ChangeType, row.Classification),
                    row.PathSignature,
                    row.ChangeType,
                    MapClassification(row.Classification),
                    row.Confidence,
                    "path",
                    row.Before?.SourceLabel ?? row.After?.SourceLabel,
                    beforeEvidence,
                    afterEvidence,
                    [row.DiffRuleId],
                    [EvidenceTiers.Tier2Structural],
                    supportingFactIds,
                    supportingEdgeIds,
                    supportingPathIds,
                    fileSpans,
                    coverageCaveats,
                    notes);
            })
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ThenBy(row => row.DiffId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<SnapshotDiffGap> MapCombinedGaps(IReadOnlyList<CombinedDiffGap> gaps)
    {
        return gaps
            .Select(gap => new SnapshotDiffGap(
                StableId("gap", "combined", gap.GapId, gap.GapKind, gap.SourceLabel ?? "all"),
                gap.GapKind,
                MapCombinedGapSection(gap),
                gap.SourceLabel,
                gap.RuleId,
                gap.EvidenceTier,
                MapClassification(gap.Classification),
                gap.Message,
                [],
                gap.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                gap.SupportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                [],
                CombinedReportHelpers.SortedMetadata([
                    Pair("combinedGapId", gap.GapId),
                    Pair("evidenceKind", gap.EvidenceKind)
                ])))
            .OrderBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string MapCombinedGapSection(CombinedDiffGap gap)
    {
        var ruleSection = CombinedRuleSection(gap.RuleId);
        if (ruleSection is not null)
        {
            return ruleSection;
        }

        var gapIdSection = CombinedRuleSectionFromText(gap.GapId);
        if (gapIdSection is not null)
        {
            return gapIdSection;
        }

        var evidenceKindSection = CombinedEvidenceKindSection(gap.EvidenceKind);
        if (evidenceKindSection is not null)
        {
            return evidenceKindSection;
        }

        return "combinedDiff";
    }

    private static string? CombinedRuleSection(string? ruleId)
    {
        return ruleId switch
        {
            "combined.diff.source.v1" => "sourceDiffs",
            "combined.diff.coverage.v1" => "coverageDiffs",
            "combined.diff.endpoint.v1" => "endpointDiffs",
            "combined.diff.surface.v1" => "surfaceDiffs",
            "combined.diff.edge.v1" => "graphDiffs",
            "combined.diff.path.v1" => "pathDiffs",
            _ => null
        };
    }

    private static string? CombinedRuleSectionFromText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var ruleId in new[] { "combined.diff.source.v1", "combined.diff.coverage.v1", "combined.diff.endpoint.v1", "combined.diff.surface.v1", "combined.diff.edge.v1", "combined.diff.path.v1" })
        {
            if (value.Contains(ruleId, StringComparison.Ordinal))
            {
                return CombinedRuleSection(ruleId);
            }
        }

        return null;
    }

    private static string? CombinedEvidenceKindSection(string? evidenceKind)
    {
        return evidenceKind switch
        {
            "source" => "sourceDiffs",
            "coverage" => "coverageDiffs",
            "endpoint" => "endpointDiffs",
            "surface" => "surfaceDiffs",
            "edge" => "graphDiffs",
            "path" => "pathDiffs",
            _ => null
        };
    }

    private static SnapshotDiffEvidence MapCombinedEvidence(CombinedDiffEvidence evidence)
    {
        return new SnapshotDiffEvidence(
            evidence.SourceLabel,
            evidence.CommitSha,
            evidence.ScanId,
            evidence.Language,
            null,
            null,
            null,
            null,
            null,
            null,
            FileSpans(evidence).ToArray(),
            CombinedReportHelpers.SortedMetadata(evidence.SafeMetadata.Select(pair => Pair(pair.Key, pair.Value)).Concat([
                Pair("displayName", evidence.DisplayName),
                Pair("evidenceKind", evidence.EvidenceKind),
                Pair("ruleId", evidence.RuleId),
                Pair("evidenceTier", evidence.EvidenceTier)
            ])));
    }

    private static SnapshotDiffEvidence MapCombinedPathEvidence(CombinedPathEvidence evidence)
    {
        return new SnapshotDiffEvidence(
            evidence.SourceLabel,
            evidence.CommitSha,
            evidence.ScanId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            PathFileSpans(evidence).ToArray(),
            CombinedReportHelpers.SortedMetadata(evidence.TerminalSurfaceMetadata.Select(pair => Pair(pair.Key, pair.Value)).Concat([
                Pair("pathId", evidence.PathId),
                Pair("pathClassification", evidence.PathClassification),
                Pair("startIdentity", evidence.StartIdentity),
                Pair("endIdentity", evidence.EndIdentity),
                Pair("sourceTransitions", string.Join(" -> ", evidence.SourceTransitions))
            ])));
    }

    private static string MapClassification(string classification)
    {
        return classification switch
        {
            CombinedDependencyDiffClassifications.NeedsReviewDiff => SnapshotDiffClassifications.NeedsReview,
            CombinedDependencyDiffClassifications.AddedWithBeforeGap => SnapshotDiffClassifications.ChangedWithReducedCoverage,
            CombinedDependencyDiffClassifications.RemovedWithAfterGap => SnapshotDiffClassifications.ChangedWithReducedCoverage,
            CombinedDependencyDiffClassifications.UnknownAnalysisGap => SnapshotDiffClassifications.UnknownAnalysisGap,
            CombinedDependencyDiffClassifications.SelectorNoMatch => SnapshotDiffClassifications.SelectorNoMatch,
            CombinedDependencyDiffClassifications.NoPathEvidence => SnapshotDiffClassifications.NeedsReview,
            CombinedDependencyDiffClassifications.NoDiffEvidence => SnapshotDiffClassifications.NoSnapshotDiffEvidence,
            _ => classification
        };
    }

    private static IReadOnlyList<string> SupportingFactIds(CombinedDiffEvidence? before, CombinedDiffEvidence? after)
    {
        return (before?.SupportingFactIds ?? [])
            .Concat(after?.SupportingFactIds ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> SupportingEdgeIds(CombinedDiffEvidence? before, CombinedDiffEvidence? after)
    {
        return (before?.SupportingEdgeIds ?? [])
            .Concat(after?.SupportingEdgeIds ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> RuleIds(params string?[] ruleIds)
    {
        return ruleIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> EvidenceTiersFor(params string?[] evidenceTiers)
    {
        var tiers = evidenceTiers
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return tiers.Length == 0 ? [EvidenceTiers.Tier4Unknown] : tiers;
    }

    private static IReadOnlyList<SnapshotDiffFileSpan> FileSpans(CombinedDiffEvidence? before, CombinedDiffEvidence? after)
    {
        return (before is null ? [] : FileSpans(before))
            .Concat(after is null ? [] : FileSpans(after))
            .GroupBy(span => $"{span.SourceLabel}:{span.FilePath}:{span.StartLine}:{span.EndLine}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(span => span.SourceLabel, StringComparer.Ordinal)
            .ThenBy(span => span.FilePath, StringComparer.Ordinal)
            .ThenBy(span => span.StartLine)
            .ThenBy(span => span.EndLine)
            .ToArray();
    }

    private static IReadOnlyList<SnapshotDiffFileSpan> FileSpans(CombinedDiffEvidence evidence)
    {
        return string.IsNullOrWhiteSpace(evidence.FilePath)
            ? []
            : [new SnapshotDiffFileSpan(CombinedReportHelpers.SafePath(evidence.FilePath), evidence.StartLine, evidence.EndLine, evidence.SourceLabel)];
    }

    private static IReadOnlyList<SnapshotDiffFileSpan> PathFileSpans(CombinedPathEvidence? before, CombinedPathEvidence? after)
    {
        return (before is null ? [] : PathFileSpans(before))
            .Concat(after is null ? [] : PathFileSpans(after))
            .GroupBy(span => $"{span.SourceLabel}:{span.FilePath}:{span.StartLine}:{span.EndLine}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(span => span.SourceLabel, StringComparer.Ordinal)
            .ThenBy(span => span.FilePath, StringComparer.Ordinal)
            .ThenBy(span => span.StartLine)
            .ThenBy(span => span.EndLine)
            .ToArray();
    }

    private static IReadOnlyList<SnapshotDiffFileSpan> PathFileSpans(CombinedPathEvidence evidence)
    {
        return evidence.FileSpans
            .Select(span => new SnapshotDiffFileSpan(CombinedReportHelpers.SafePath(span.FilePath), span.StartLine, span.EndLine, span.SourceLabel))
            .ToArray();
    }

    private static void AddUnavailableGaps(IReadOnlyList<string> scopes, string kind, bool includePaths, bool hasCombinedDelegation, bool hasSingleProjection, List<SnapshotDiffGap> gaps)
    {
        AddUnavailable(scopes, "contract-shapes", "contractShapeDiffs", gaps);
        if (!hasSingleProjection)
        {
            AddUnavailable(scopes, "gaps", "gapDiffs", gaps);
        }

        if (!hasCombinedDelegation && !hasSingleProjection)
        {
            AddUnavailable(scopes, "endpoints", "endpointDiffs", gaps);
            AddUnavailable(scopes, "surfaces", "surfaceDiffs", gaps);
        }

        if (!hasCombinedDelegation)
        {
            AddUnavailable(scopes, "graph", "graphDiffs", gaps);
        }

        if (includePaths && kind == "single")
        {
            throw new ArgumentException("snapshot-diff --include-paths requires combined indexes.");
        }

        if (scopes.Contains("paths", StringComparer.Ordinal) && !hasCombinedDelegation)
        {
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

        if (gaps.Any(gap => gap.Classification == SnapshotDiffClassifications.TruncatedByLimit || gap.GapKind == "TruncatedByLimit"))
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

    private static ScanManifest? DeserializeManifest(string? manifestJson, out bool malformed)
    {
        malformed = false;
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
            malformed = true;
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string json, out bool malformed)
    {
        malformed = false;
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var values = JsonSerializer.Deserialize<SortedDictionary<string, string>>(json, CombinedDependencyReporter.JsonOptions);
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (values is not null)
            {
                foreach (var pair in values.Where(pair => pair.Value is not null))
                {
                    result[pair.Key] = pair.Value;
                }
            }

            return result;
        }
        catch (JsonException)
        {
            malformed = true;
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static SnapshotDiffGap MalformedMetadataGap(string side, string field, string? sourceLabel, string? supportingFactId)
    {
        return new SnapshotDiffGap(
            StableId("gap", "malformed-metadata", side, field, sourceLabel ?? "all", supportingFactId),
            "MalformedMetadataGap",
            "metadata",
            sourceLabel,
            SchemaRuleId,
            EvidenceTiers.Tier4Unknown,
            SnapshotDiffClassifications.UnknownAnalysisGap,
            $"Malformed metadata was omitted from `{side}` `{field}` and analysis continued with reduced coverage.",
            [],
            string.IsNullOrWhiteSpace(supportingFactId) ? [] : [supportingFactId],
            [],
            [],
            CombinedReportHelpers.SortedMetadata([
                Pair("side", side),
                Pair("field", field),
                Pair("sourceLabel", sourceLabel)
            ]));
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
