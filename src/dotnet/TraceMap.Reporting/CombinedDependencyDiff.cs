using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record CombinedDependencyDiffOptions(
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

public sealed record CombinedDependencyDiffResult(
    CombinedDependencyDiffReport Report,
    string? MarkdownPath,
    string? JsonPath,
    bool HasDiffs);

public sealed record CombinedDependencyDiffReport(
    string ReportType,
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    CombinedDiffQuery Query,
    CombinedDiffSnapshotInfo BeforeSnapshot,
    CombinedDiffSnapshotInfo AfterSnapshot,
    CombinedDiffSummary Summary,
    IReadOnlyList<CombinedDiffRow> SourceDiffs,
    IReadOnlyList<CombinedDiffRow> CoverageDiffs,
    IReadOnlyList<CombinedDiffRow> EndpointDiffs,
    IReadOnlyList<CombinedDiffRow> SurfaceDiffs,
    IReadOnlyList<CombinedDiffRow> EdgeDiffs,
    IReadOnlyList<CombinedPathDiffRow> PathDiffs,
    IReadOnlyList<CombinedDiffGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record CombinedDiffQuery(
    IReadOnlyList<string> Scopes,
    bool IncludePaths,
    string? Source,
    string? Endpoint,
    string? Surface,
    string? SurfaceName,
    int MaxDepth,
    int MaxPaths,
    int MaxFrontier,
    int MaxDiffRows,
    int MaxGaps,
    bool AllowIdentityMismatch,
    bool ExitCode,
    IReadOnlyList<string> IgnoredSelectors,
    string Algorithm,
    string AlgorithmVersion);

public sealed record CombinedDiffSnapshotInfo(
    string Side,
    IReadOnlyList<CombinedDiffSourceInfo> Sources,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    IReadOnlyList<KeyValuePair<string, string>> ExtractorVersions);

public sealed record CombinedDiffSourceInfo(
    string SourceLabel,
    string? Language,
    string? ScanId,
    string? CommitSha,
    string? RepositoryIdentity,
    string? RootPathHash,
    string Coverage,
    IReadOnlyList<string> GapCodes);

public sealed record CombinedDiffSummary(
    int SourceDiffCount,
    int CoverageDiffCount,
    int EndpointDiffCount,
    int SurfaceDiffCount,
    int EdgeDiffCount,
    int PathDiffCount,
    int GapCount,
    bool Truncated);

public sealed record CombinedDiffRow(
    string DiffId,
    string ChangeType,
    string Classification,
    string Confidence,
    string StableKey,
    string DiffRuleId,
    CombinedDiffEvidence? Before,
    CombinedDiffEvidence? After,
    IReadOnlyList<CombinedCoverageCaveat> CoverageCaveats,
    IReadOnlyList<CombinedDiffNote> Notes);

public sealed record CombinedPathDiffRow(
    string DiffId,
    string ChangeType,
    string Classification,
    string Confidence,
    string PathSignature,
    string DiffRuleId,
    CombinedPathEvidence? Before,
    CombinedPathEvidence? After,
    IReadOnlyList<CombinedCoverageCaveat> CoverageCaveats,
    IReadOnlyList<CombinedDiffNote> Notes);

public sealed record CombinedDiffEvidence(
    string SourceLabel,
    string? Language,
    string? ScanId,
    string? CommitSha,
    string? EvidenceKind,
    string? DisplayName,
    string? RuleId,
    string? EvidenceTier,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyList<KeyValuePair<string, string>> SafeMetadata,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds);

public sealed record CombinedPathEvidence(
    string PathId,
    string PathClassification,
    string StartIdentity,
    string EndIdentity,
    IReadOnlyList<string> SourceTransitions,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<KeyValuePair<string, string>> TerminalSurfaceMetadata);

public sealed record CombinedCoverageCaveat(string SourceLabel, string Code, string Message);

public sealed record CombinedDiffNote(string Code, string Message);

public sealed record CombinedDiffGap(
    string GapId,
    string GapKind,
    string? SourceLabel,
    string? EvidenceKind,
    string RuleId,
    string EvidenceTier,
    string Classification,
    string Message,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds);

internal sealed record CombinedDiffSnapshot(
    CombinedReadResult Read,
    IReadOnlyList<ComparableDiffRecord> Sources,
    IReadOnlyList<ComparableDiffRecord> Coverages,
    IReadOnlyList<ComparableDiffRecord> Endpoints,
    IReadOnlyList<ComparableDiffRecord> Surfaces,
    IReadOnlyList<ComparableDiffRecord> Edges,
    IReadOnlyList<ComparablePathRecord> Paths,
    IReadOnlyList<CombinedDiffGap> Gaps);

internal sealed record ComparableDiffRecord(
    string Kind,
    string StableKey,
    string MetadataHash,
    string EvidenceHash,
    CombinedDiffEvidence Evidence,
    bool NeedsReview = false);

internal sealed record ComparablePathRecord(
    string Signature,
    string MetadataHash,
    CombinedPathEvidence Evidence,
    string Classification,
    bool NeedsReview = false);

public static class CombinedDependencyDiffClassifications
{
    public const string Added = nameof(Added);
    public const string Removed = nameof(Removed);
    public const string ChangedEvidence = nameof(ChangedEvidence);
    public const string AddedWithBeforeGap = nameof(AddedWithBeforeGap);
    public const string RemovedWithAfterGap = nameof(RemovedWithAfterGap);
    public const string NeedsReviewDiff = nameof(NeedsReviewDiff);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
    public const string NoPathEvidence = nameof(NoPathEvidence);
    public const string NoDiffEvidence = nameof(NoDiffEvidence);
}

public static class CombinedDependencyDiffer
{
    private const string Version = "1.0";
    private const string ReportType = "combined-dependency-diff";
    private const string Algorithm = "stable-evidence-keyset-diff";
    private const string AlgorithmVersion = "1.0";
    private const string SourceRuleId = "combined.diff.source.v1";
    private const string CoverageRuleId = "combined.diff.coverage.v1";
    private const string EndpointRuleId = "combined.diff.endpoint.v1";
    private const string SurfaceRuleId = "combined.diff.surface.v1";
    private const string EdgeRuleId = "combined.diff.edge.v1";
    private const string PathRuleId = "combined.diff.path.v1";
    private const string SelectorRuleId = "combined.diff.selector.v1";
    private const string SchemaRuleId = "combined.diff.schema.v1";
    private const string TruncationRuleId = "combined.diff.truncation.v1";

    private static readonly HashSet<string> ValidScopes = new(StringComparer.Ordinal)
    {
        "all",
        "sources",
        "endpoints",
        "surfaces",
        "edges",
        "paths"
    };

    private static readonly HashSet<string> ValidSurfaceKinds = new(StringComparer.Ordinal)
    {
        "sql-query",
        "http-route",
        "http-client",
        "package-config"
    };

    private static readonly IReadOnlyList<string> Limitations =
    [
        "Diff rows compare static TraceMap evidence, not runtime behavior.",
        "Endpoint diffs do not prove runtime traffic, auth behavior, proxies, deployment base paths, or reachability.",
        "Path diffs are static evidence trails and are not full taint analysis, runtime DI resolution, dynamic dispatch resolution, reflection resolution, serializer mapping, or branch feasibility analysis.",
        "SQL/query diffs do not prove runtime execution, schema existence, generated SQL equivalence, dialect validity, or branch feasibility.",
        "Reduced scan coverage makes absence of evidence coverage-relative."
    ];

    public static async Task<CombinedDependencyDiffResult> WriteAsync(CombinedDependencyDiffOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "diff");
        var scopes = NormalizeScopes(options.Scope, options.IncludePaths);
        (string Method, string PathKey)? endpointSelector = string.IsNullOrWhiteSpace(options.Endpoint) ? null : ParseEndpointSelector(options.Endpoint);
        var ignoredSelectors = IgnoredSelectors(options, scopes);

        var before = await ReadSnapshotAsync("before", options.BeforePath, options, scopes, endpointSelector, cancellationToken);
        var after = await ReadSnapshotAsync("after", options.AfterPath, options, scopes, endpointSelector, cancellationToken);

        var gaps = before.Gaps.Concat(after.Gaps).ToList();
        var sourceIdentity = ValidateSourceIdentity(before.Read.Sources, after.Read.Sources, options.AllowIdentityMismatch, gaps);
        var sourceDiffs = CompareRecords(before.Sources, after.Sources, SourceRuleId, options.MaxDiffRows, sourceIdentity, gaps);
        var coverageDiffs = CompareRecords(before.Coverages, after.Coverages, CoverageRuleId, options.MaxDiffRows, sourceIdentity, gaps);
        var endpointDiffs = scopes.Contains("endpoints") ? CompareRecords(before.Endpoints, after.Endpoints, EndpointRuleId, options.MaxDiffRows, sourceIdentity, gaps) : [];
        var surfaceDiffs = scopes.Contains("surfaces") ? CompareRecords(before.Surfaces, after.Surfaces, SurfaceRuleId, options.MaxDiffRows, sourceIdentity, gaps) : [];
        var edgeDiffs = scopes.Contains("edges") ? CompareRecords(before.Edges, after.Edges, EdgeRuleId, options.MaxDiffRows, sourceIdentity, gaps) : [];
        var pathDiffs = scopes.Contains("paths") ? ComparePathRecords(before.Paths, after.Paths, options.MaxPaths, sourceIdentity, gaps) : [];

        if (HasAnySelector(options)
            && ComparableEvidenceCount(before) == 0
            && ComparableEvidenceCount(after) == 0)
        {
            gaps.Add(new CombinedDiffGap("gap:selector:no-match", "SelectorNoMatch", options.Source, null, SelectorRuleId, EvidenceTiers.Tier4Unknown, CombinedDependencyDiffClassifications.SelectorNoMatch, "Selectors matched no comparable evidence in either snapshot.", [], []));
        }

        if (scopes.Contains("paths") && before.Paths.Count == 0 && after.Paths.Count == 0)
        {
            gaps.Add(new CombinedDiffGap("gap:path:no-evidence", "NoPathEvidence", options.Source, "path", PathRuleId, EvidenceTiers.Tier4Unknown, CombinedDependencyDiffClassifications.NoPathEvidence, "Path comparison was requested but no path evidence was returned in either snapshot.", [], []));
        }

        var diffCount = sourceDiffs.Count + coverageDiffs.Count + endpointDiffs.Count + surfaceDiffs.Count + edgeDiffs.Count + pathDiffs.Count;
        if (diffCount == 0 && gaps.All(gap => gap.Classification != CombinedDependencyDiffClassifications.SelectorNoMatch))
        {
            gaps.Add(new CombinedDiffGap("gap:no-diff-evidence", "NoDiffEvidence", null, null, SourceRuleId, EvidenceTiers.Tier4Unknown, CombinedDependencyDiffClassifications.NoDiffEvidence, "Comparable evidence was found, but no diff was detected.", [], []));
        }

        var sortedGaps = SortAndCapGaps(gaps, options.MaxGaps, out var gapsTruncated);
        var outputTruncated = gapsTruncated || sortedGaps.Any(gap => gap.GapKind == "TruncatedByLimit");
        var warnings = before.Read.CoverageWarnings
            .Concat(after.Read.CoverageWarnings)
            .Concat(sourceIdentity.Values.SelectMany(value => value.Caveats.Select(caveat => caveat.Message)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var report = new CombinedDependencyDiffReport(
            ReportType,
            Version,
            warnings.Length == 0 ? "FullEvidenceAvailable" : warnings.Any(warning => warning.Contains("unknown", StringComparison.OrdinalIgnoreCase)) ? "UnknownAnalysisGap" : "ReducedCoverage",
            warnings,
            new CombinedDiffQuery(
                scopes,
                options.IncludePaths,
                options.Source,
                options.Endpoint,
                options.Surface,
                options.SurfaceName,
                options.MaxDepth,
                options.MaxPaths,
                options.MaxFrontier,
                options.MaxDiffRows,
                options.MaxGaps,
                options.AllowIdentityMismatch,
                options.ExitCode,
                ignoredSelectors,
                Algorithm,
                AlgorithmVersion),
            ToSnapshotInfo("before", before.Read),
            ToSnapshotInfo("after", after.Read),
            new CombinedDiffSummary(sourceDiffs.Count, coverageDiffs.Count, endpointDiffs.Count, surfaceDiffs.Count, edgeDiffs.Count, pathDiffs.Count, sortedGaps.Count, outputTruncated),
            sourceDiffs,
            coverageDiffs,
            endpointDiffs,
            surfaceDiffs,
            edgeDiffs,
            pathDiffs,
            sortedGaps,
            Limitations);

        var (markdownPath, jsonPath) = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "diff-report.md",
            "diff-report.json",
            report,
            RenderMarkdown,
            CombinedDependencyReporter.JsonOptions,
            cancellationToken);
        return new CombinedDependencyDiffResult(report, markdownPath, jsonPath, diffCount > 0);
    }

    private static void ValidateOptions(CombinedDependencyDiffOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BeforePath))
        {
            throw new ArgumentException("diff requires --before <combined.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.AfterPath))
        {
            throw new ArgumentException("diff requires --after <combined.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("diff requires --out <path>.");
        }

        if (options.MaxDepth <= 0 || options.MaxPaths <= 0 || options.MaxFrontier <= 0 || options.MaxDiffRows <= 0 || options.MaxGaps <= 0)
        {
            throw new ArgumentException("diff numeric limits must be positive integers.");
        }

        if (!string.IsNullOrWhiteSpace(options.Surface) && !ValidSurfaceKinds.Contains(options.Surface.Trim()))
        {
            throw new ArgumentException("diff --surface must be one of sql-query, http-route, http-client, or package-config.");
        }

        var scopes = NormalizeScopes(options.Scope, options.IncludePaths);
        if (scopes.Contains("paths") && !options.IncludePaths)
        {
            throw new ArgumentException("diff --scope paths requires --include-paths.");
        }
    }

    private static IReadOnlyList<string> NormalizeScopes(string? value, bool includePaths)
    {
        var rawScopes = string.IsNullOrWhiteSpace(value)
            ? ["sources", "endpoints", "surfaces", "edges"]
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scopes = rawScopes.Select(scope => scope.ToLowerInvariant()).ToArray();
        foreach (var scope in scopes)
        {
            if (!ValidScopes.Contains(scope))
            {
                throw new ArgumentException("diff --scope must be one of all, sources, endpoints, surfaces, edges, or paths.");
            }
        }

        if (scopes.Contains("all"))
        {
            scopes = includePaths
                ? ["sources", "endpoints", "surfaces", "edges", "paths"]
                : ["sources", "endpoints", "surfaces", "edges"];
        }

        if (!scopes.Contains("sources"))
        {
            scopes = [.. scopes, "sources"];
        }

        return scopes.Distinct(StringComparer.Ordinal).OrderBy(scope => ScopeRank(scope)).ToArray();
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

    private static (string Method, string PathKey) ParseEndpointSelector(string value)
    {
        var trimmed = value.Trim();
        var parts = trimmed.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]) || parts[0].StartsWith('/'))
        {
            throw new ArgumentException("diff --endpoint must be '<METHOD> <PATH_KEY>'.");
        }

        return (parts[0].ToUpperInvariant(), EndpointRouteNormalizer.Normalize(parts[1]).PathKey);
    }

    private static IReadOnlyList<string> IgnoredSelectors(CombinedDependencyDiffOptions options, IReadOnlyList<string> scopes)
    {
        var ignored = new List<string>();
        if (!scopes.Contains("paths") && options.IncludePaths)
        {
            ignored.Add("--include-paths requested but path scope is disabled.");
        }

        if (!scopes.Contains("surfaces") && !scopes.Contains("paths") && !string.IsNullOrWhiteSpace(options.SurfaceName))
        {
            ignored.Add("--surface-name has no enabled surface or path scope.");
        }

        return ignored.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static bool HasAnySelector(CombinedDependencyDiffOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.Source)
            || !string.IsNullOrWhiteSpace(options.Endpoint)
            || !string.IsNullOrWhiteSpace(options.Surface)
            || !string.IsNullOrWhiteSpace(options.SurfaceName);
    }

    private static int ComparableEvidenceCount(CombinedDiffSnapshot snapshot)
    {
        return snapshot.Sources.Count
            + snapshot.Coverages.Count
            + snapshot.Endpoints.Count
            + snapshot.Surfaces.Count
            + snapshot.Edges.Count
            + snapshot.Paths.Count;
    }

    private static async Task<CombinedDiffSnapshot> ReadSnapshotAsync(
        string side,
        string indexPath,
        CombinedDependencyDiffOptions options,
        IReadOnlyList<string> scopes,
        (string Method, string PathKey)? endpointSelector,
        CancellationToken cancellationToken)
    {
        var gaps = new List<CombinedDiffGap>();
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = indexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await CombinedDependencyReporter.ValidateCombinedIndexAsync(connection, cancellationToken);
        await AddSchemaGapsAsync(side, connection, gaps, cancellationToken);

        var read = await CombinedDependencyReporter.ReadAsync(connection, cancellationToken);
        var sourceFilter = string.IsNullOrWhiteSpace(options.Source) ? null : options.Source.Trim();
        var sources = ProjectSources(read, sourceFilter);
        var coverage = ProjectCoverage(read, sourceFilter);
        var endpointFindings = scopes.Contains("endpoints") || scopes.Contains("paths")
            ? CombinedDependencyReporter.MatchEndpoints(read.Sources, read.Facts)
            : [];
        var surfaces = scopes.Contains("surfaces") || scopes.Contains("paths")
            ? CombinedDependencyReporter.BuildSurfaces(read.Facts)
            : [];
        var endpoints = scopes.Contains("endpoints")
            ? ProjectEndpoints(read, endpointFindings, sourceFilter, endpointSelector)
            : [];
        var surfaceRecords = scopes.Contains("surfaces")
            ? ProjectSurfaces(read, surfaces, sourceFilter, options.Surface, options.SurfaceName)
            : [];
        var edges = scopes.Contains("edges")
            ? ProjectEdges(read, sourceFilter)
            : [];
        var paths = scopes.Contains("paths") && options.IncludePaths
            ? await ProjectPathsAsync(side, indexPath, options, sourceFilter, endpointSelector, cancellationToken)
            : [];

        return new CombinedDiffSnapshot(read, sources, coverage, endpoints, surfaceRecords, edges, paths, gaps);
    }

    private static async Task AddSchemaGapsAsync(string side, SqliteConnection connection, List<CombinedDiffGap> gaps, CancellationToken cancellationToken)
    {
        foreach (var table in new[] { "combined_fact_symbols", "combined_call_edges", "combined_object_creations", "combined_symbol_relationships", "combined_argument_flows", "combined_parameter_forward_edges" })
        {
            if (!await TableExistsAsync(connection, table, cancellationToken))
            {
                gaps.Add(new CombinedDiffGap(
                    $"gap:schema:{side}:{table}",
                    "SchemaMissing",
                    null,
                    table,
                    SchemaRuleId,
                    EvidenceTiers.Tier4Unknown,
                    CombinedDependencyDiffClassifications.UnknownAnalysisGap,
                    $"{side} combined index is missing optional precision table `{table}`; fallback comparison may be reduced.",
                    [],
                    []));
            }
        }
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static IReadOnlyList<ComparableDiffRecord> ProjectSources(CombinedReadResult read, string? sourceFilter)
    {
        return read.Sources
            .Where(source => MatchesSource(source, sourceFilter))
            .Select(source =>
            {
                var metadata = CombinedReportHelpers.SortedMetadata([
                    Pair("label", source.Label),
                    Pair("language", source.Language),
                    Pair("repoName", source.RepoName),
                    Pair("remoteUrlHash", HashOrNull(source.RemoteUrl)),
                    Pair("rootPathHash", source.ScanRootPathHash),
                    Pair("gitRootHash", source.GitRootHash),
                    Pair("commitSha", source.CommitSha),
                    Pair("scannerVersion", source.ScannerVersion)
                ]);
                var evidence = Evidence(source, "source", source.Label, SourceRuleId, EvidenceTiers.Tier2Structural, null, null, null, metadata, [], []);
                return new ComparableDiffRecord(
                    "source",
                    $"source:{source.Label}",
                    MetadataHash(metadata),
                    EvidenceHash(evidence),
                    evidence);
            })
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ComparableDiffRecord> ProjectCoverage(CombinedReadResult read, string? sourceFilter)
    {
        return read.Sources
            .Where(source => MatchesSource(source, sourceFilter))
            .Select(source =>
            {
                var metadata = CombinedReportHelpers.SortedMetadata([
                    Pair("analysisLevel", source.AnalysisLevel),
                    Pair("buildStatus", source.BuildStatus),
                    Pair("commitSha", source.CommitSha)
                ]);
                var evidence = Evidence(source, "coverage", $"{source.AnalysisLevel}/{source.BuildStatus}", CoverageRuleId, EvidenceTiers.Tier4Unknown, null, null, null, metadata, [], []);
                return new ComparableDiffRecord(
                    "coverage",
                    $"coverage:{source.Label}",
                    MetadataHash(metadata),
                    EvidenceHash(evidence),
                    evidence);
            })
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ComparableDiffRecord> ProjectEndpoints(
        CombinedReadResult read,
        IReadOnlyList<CombinedEndpointFinding> findings,
        string? sourceFilter,
        (string Method, string PathKey)? endpointSelector)
    {
        return findings
            .Where(finding => EndpointMatchesSource(finding, sourceFilter))
            .Where(finding => endpointSelector is null || (CombinedDependencyReporter.MethodsCompatible(endpointSelector.Value.Method, finding.HttpMethod) && string.Equals(finding.NormalizedPathKey, endpointSelector.Value.PathKey, StringComparison.Ordinal)))
            .Select(finding =>
            {
                var source = SourceFor(read, finding.ClientSourceIndexId ?? finding.ServerSourceIndexId);
                var key = EndpointStableKey(finding);
                var metadata = CombinedReportHelpers.SortedMetadata([
                    Pair("classification", finding.Classification),
                    Pair("httpMethod", finding.HttpMethod),
                    Pair("normalizedPathKey", finding.NormalizedPathKey),
                    Pair("clientSource", finding.ClientSourceLabel),
                    Pair("serverSource", finding.ServerSourceLabel),
                    Pair("staticMatchQuality", finding.StaticMatchQuality)
                ]);
                var evidence = Evidence(
                    source,
                    "endpoint",
                    $"{finding.HttpMethod} {finding.NormalizedPathKey ?? "unknown"}",
                    finding.ClientRuleId ?? finding.ServerRuleId ?? EndpointRuleId,
                    finding.ClientEvidenceTier ?? finding.ServerEvidenceTier ?? EvidenceTiers.Tier4Unknown,
                    finding.ClientFilePath ?? finding.ServerFilePath,
                    finding.ClientStartLine ?? finding.ServerStartLine,
                    finding.ClientEndLine ?? finding.ServerEndLine,
                    metadata,
                    SortedIds([finding.ClientCombinedFactId, finding.ServerCombinedFactId]),
                    []);
                return new ComparableDiffRecord(
                    "endpoint",
                    key,
                    MetadataHash(metadata),
                    EvidenceHash(evidence),
                    evidence,
                    finding.Classification is CombinedEndpointClassifications.AmbiguousMatch or CombinedEndpointClassifications.DynamicClientUrlNeedsReview or CombinedEndpointClassifications.MethodMismatch or CombinedEndpointClassifications.UnknownAnalysisGap);
            })
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string EndpointStableKey(CombinedEndpointFinding finding)
    {
        var clientKey = finding.ClientSourceLabel is null
            ? "client:none"
            : $"client:{finding.ClientSourceLabel}:{finding.HttpMethod}:{finding.NormalizedPathKey ?? "unknown"}";
        var serverKey = finding.ServerSourceLabel is null
            ? "server:none"
            : $"server:{finding.ServerSourceLabel}:{finding.HttpMethod}:{finding.NormalizedPathKey ?? "unknown"}";
        return $"endpoint:{clientKey}:{serverKey}";
    }

    private static IReadOnlyList<ComparableDiffRecord> ProjectSurfaces(
        CombinedReadResult read,
        IReadOnlyList<CombinedDependencySurfaceRow> surfaces,
        string? sourceFilter,
        string? surfaceKind,
        string? surfaceName)
    {
        return surfaces
            .Where(surface => MatchesSourceLabel(surface.SourceLabel, sourceFilter))
            .Where(surface => string.IsNullOrWhiteSpace(surfaceKind) || string.Equals(surface.SurfaceKind, surfaceKind.Trim(), StringComparison.Ordinal))
            .Where(surface => string.IsNullOrWhiteSpace(surfaceName) || SurfaceNameMatches(surface, surfaceName.Trim()))
            .Select(surface =>
            {
                var source = SourceFor(read, surface.SourceIndexId);
                var metadata = SurfaceMetadata(surface);
                var stable = $"surface:{surface.SourceLabel}:{surface.SurfaceKind}:{MetadataHash(metadata)}";
                var evidence = Evidence(
                    source,
                    "surface",
                    surface.DisplayName,
                    surface.RuleId,
                    surface.EvidenceTier,
                    surface.FilePath,
                    surface.StartLine,
                    surface.EndLine,
                    metadata,
                    [surface.CombinedFactId],
                    []);
                return new ComparableDiffRecord(
                    "surface",
                    stable,
                    MetadataHash(metadata),
                    EvidenceHash(evidence),
                    evidence,
                    surface.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual);
            })
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SurfaceMetadata(CombinedDependencySurfaceRow surface)
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
            Pair("packageName", surface.PackageName),
            Pair("version", surface.Version),
            Pair("configKey", surface.ConfigKey)
        ]);
    }

    private static bool SurfaceNameMatches(CombinedDependencySurfaceRow surface, string selector)
    {
        return string.Equals(surface.DisplayName, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(surface.PackageName, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(surface.ConfigKey, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(surface.TableName, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(surface.NormalizedPathKey, selector, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ComparableDiffRecord> ProjectEdges(CombinedReadResult read, string? sourceFilter)
    {
        return read.Edges
            .Where(edge => MatchesSourceLabel(edge.SourceLabel, sourceFilter))
            .Select(edge =>
            {
                var source = SourceFor(read, edge.SourceIndexId);
                var metadata = CombinedReportHelpers.SortedMetadata([
                    Pair("edgeKind", NormalizeEdgeKind(edge.EdgeKind)),
                    Pair("sourceSymbol", edge.SourceSymbol),
                    Pair("targetSymbol", edge.TargetSymbol),
                    Pair("targetAssemblyName", edge.TargetAssemblyName),
                    Pair("targetAssemblyVersion", edge.TargetAssemblyVersion),
                    Pair("ruleFamily", RuleFamily(edge.RuleId))
                ]);
                var stable = $"edge:{edge.SourceLabel}:{MetadataHash(metadata)}";
                var evidence = Evidence(
                    source,
                    "edge",
                    $"{edge.SourceSymbol ?? "unknown"} -> {edge.TargetSymbol ?? "unknown"}",
                    edge.RuleId,
                    edge.EvidenceTier,
                    edge.FilePath,
                    edge.StartLine,
                    edge.EndLine,
                    metadata,
                    [],
                    [edge.EdgeId]);
                return new ComparableDiffRecord(
                    "edge",
                    stable,
                    MetadataHash(metadata),
                    EvidenceHash(evidence),
                    evidence,
                    edge.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual || string.IsNullOrWhiteSpace(edge.SourceSymbol) || string.IsNullOrWhiteSpace(edge.TargetSymbol));
            })
            .OrderBy(row => row.StableKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<IReadOnlyList<ComparablePathRecord>> ProjectPathsAsync(
        string side,
        string indexPath,
        CombinedDependencyDiffOptions options,
        string? sourceFilter,
        (string Method, string PathKey)? endpointSelector,
        CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tracemap-diff-paths", $"{Guid.NewGuid():N}");
        try
        {
            var pathResult = await CombinedDependencyPathReporter.WriteAsync(
                new CombinedDependencyPathOptions(
                    indexPath,
                    tempDir,
                    "json",
                    endpointSelector is null ? null : $"{endpointSelector.Value.Method} {endpointSelector.Value.PathKey}",
                    FromSource: sourceFilter,
                    ToSurface: string.IsNullOrWhiteSpace(options.Surface) ? null : options.Surface,
                    SurfaceName: string.IsNullOrWhiteSpace(options.SurfaceName) ? null : options.SurfaceName,
                    MaxDepth: options.MaxDepth,
                    MaxPaths: options.MaxPaths,
                    MaxFrontier: options.MaxFrontier),
                cancellationToken);
            return pathResult.Report.Paths
                .Select(path =>
                {
                    var signature = PathSignature(path);
                    return new ComparablePathRecord(
                        signature,
                        MetadataHash(CombinedReportHelpers.SortedMetadata([
                            Pair("classification", path.Classification),
                            Pair("length", path.Length.ToString())
                        ])),
                        new CombinedPathEvidence(
                            path.PathId,
                            path.Classification,
                            path.StartNodeId,
                            path.EndNodeId,
                            SourceTransitions(path),
                            path.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                            path.SupportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                            TerminalMetadata(path)),
                        path.Classification,
                        path.Classification != CombinedDependencyPathClassifications.StrongStaticPath);
                })
                .GroupBy(path => path.Signature, StringComparer.Ordinal)
                .Select(group => group.OrderBy(path => path.Evidence.PathId, StringComparer.Ordinal).First())
                .OrderBy(path => path.Signature, StringComparer.Ordinal)
                .ToArray();
        }
        catch (ArgumentException exception)
        {
            return
            [
                new ComparablePathRecord(
                    $"path-error:{side}:{CombinedReportHelpers.Hash(exception.Message, 16)}",
                    CombinedReportHelpers.Hash(exception.Message),
                    new CombinedPathEvidence("path-error", CombinedDependencyPathClassifications.UnknownAnalysisGap, "n/a", "n/a", [], [], [], []),
                    CombinedDependencyPathClassifications.UnknownAnalysisGap,
                    true)
            ];
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static IReadOnlyList<string> SourceTransitions(CombinedPath path)
    {
        return path.Nodes
            .Select(node => node.SourceLabel)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<KeyValuePair<string, string>> TerminalMetadata(CombinedPath path)
    {
        var terminal = path.Nodes.LastOrDefault();
        if (terminal is null)
        {
            return [];
        }

        return CombinedReportHelpers.SortedMetadata([
            Pair("surfaceKind", terminal.SurfaceKind),
            Pair("surfaceName", terminal.SurfaceName),
            Pair("httpMethod", terminal.HttpMethod),
            Pair("normalizedPathKey", terminal.NormalizedPathKey),
            Pair("shapeHash", terminal.ShapeHash),
            Pair("textHash", terminal.TextHash),
            Pair("packageName", terminal.PackageName),
            Pair("configKey", terminal.ConfigKey)
        ]);
    }

    private static string PathSignature(CombinedPath path)
    {
        var nodes = path.Nodes.Select(node =>
            string.Join("\u001f", node.NodeKind, node.SourceLabel, node.CombinedFactId ?? node.SymbolId ?? node.DisplayName, node.SurfaceKind ?? string.Empty, node.NormalizedPathKey ?? string.Empty));
        var edges = path.Edges.Select(edge =>
            string.Join("\u001f", edge.EdgeKind, edge.RuleId, edge.Classification));
        return $"path:{CombinedReportHelpers.Hash(string.Join("\n", nodes.Concat(edges)))}";
    }

    private static IReadOnlyList<CombinedDiffRow> CompareRecords(
        IReadOnlyList<ComparableDiffRecord> before,
        IReadOnlyList<ComparableDiffRecord> after,
        string ruleId,
        int maxRows,
        IReadOnlyDictionary<string, SourceIdentityAssessment> sourceIdentity,
        List<CombinedDiffGap> gaps)
    {
        var rows = new List<CombinedDiffRow>();
        var beforeGroups = before.GroupBy(record => record.StableKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var afterGroups = after.GroupBy(record => record.StableKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var duplicate in beforeGroups.Where(group => group.Value.Length > 1).Concat(afterGroups.Where(group => group.Value.Length > 1)))
        {
            gaps.Add(new CombinedDiffGap($"gap:duplicate:{ruleId}:{CombinedReportHelpers.Hash(duplicate.Key, 16)}", "DuplicateIdentity", duplicate.Value[0].Evidence.SourceLabel, duplicate.Value[0].Kind, "combined.diff.identity.v1", EvidenceTiers.Tier4Unknown, CombinedDependencyDiffClassifications.NeedsReviewDiff, $"Duplicate stable identity `{duplicate.Key}` has {duplicate.Value.Length} instances.", duplicate.Value.SelectMany(row => row.Evidence.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(), duplicate.Value.SelectMany(row => row.Evidence.SupportingEdgeIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()));
        }

        foreach (var key in beforeGroups.Keys.Concat(afterGroups.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            beforeGroups.TryGetValue(key, out var beforeRows);
            afterGroups.TryGetValue(key, out var afterRows);
            var beforeRecord = beforeRows?.OrderBy(row => row.MetadataHash, StringComparer.Ordinal).FirstOrDefault();
            var afterRecord = afterRows?.OrderBy(row => row.MetadataHash, StringComparer.Ordinal).FirstOrDefault();
            if (beforeRecord is not null && afterRecord is not null && beforeRecord.MetadataHash == afterRecord.MetadataHash && beforeRecord.EvidenceHash == afterRecord.EvidenceHash && beforeRows!.Length == afterRows!.Length)
            {
                continue;
            }

            var classification = ClassifyRecord(beforeRecord, afterRecord, beforeRows?.Length ?? 0, afterRows?.Length ?? 0, sourceIdentity);
            rows.Add(CreateRow(ruleId, key, classification, beforeRecord, afterRecord));
        }

        return SortAndCapRows(rows, maxRows, ruleId, gaps);
    }

    private static IReadOnlyList<CombinedPathDiffRow> ComparePathRecords(
        IReadOnlyList<ComparablePathRecord> before,
        IReadOnlyList<ComparablePathRecord> after,
        int maxPaths,
        IReadOnlyDictionary<string, SourceIdentityAssessment> sourceIdentity,
        List<CombinedDiffGap> gaps)
    {
        var rows = new List<CombinedPathDiffRow>();
        var beforeByKey = before.ToDictionary(record => record.Signature, StringComparer.Ordinal);
        var afterByKey = after.ToDictionary(record => record.Signature, StringComparer.Ordinal);
        foreach (var key in beforeByKey.Keys.Concat(afterByKey.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            beforeByKey.TryGetValue(key, out var beforeRecord);
            afterByKey.TryGetValue(key, out var afterRecord);
            if (beforeRecord is not null && afterRecord is not null && beforeRecord.MetadataHash == afterRecord.MetadataHash)
            {
                continue;
            }

            var beforeEvidence = beforeRecord?.Evidence;
            var afterEvidence = afterRecord?.Evidence;
            var classification = ClassifyPathRecord(beforeRecord, afterRecord, sourceIdentity);
            var caveats = PathCaveats(beforeRecord, afterRecord, sourceIdentity);

            var changeType = ChangeType(classification);
            rows.Add(new CombinedPathDiffRow(
                DiffId(changeType, classification, key, PathRuleId),
                changeType,
                classification,
                Confidence(classification),
                key,
                PathRuleId,
                beforeEvidence,
                afterEvidence,
                caveats,
                []));
        }

        var sorted = rows
            .OrderBy(row => ClassificationRank(row.Classification))
            .ThenBy(row => row.PathSignature, StringComparer.Ordinal)
            .ToArray();
        var maxRows = maxPaths * 3;
        if (sorted.Length <= maxRows)
        {
            return sorted;
        }

        gaps.Add(new CombinedDiffGap($"gap:truncated:{PathRuleId}", "TruncatedByLimit", null, "path", TruncationRuleId, EvidenceTiers.Tier4Unknown, CombinedDependencyDiffClassifications.NeedsReviewDiff, $"{PathRuleId} produced {sorted.Length} rows; output was capped at {maxRows}.", [], []));
        return sorted.Take(maxRows).ToArray();
    }

    private static string ClassifyPathRecord(
        ComparablePathRecord? before,
        ComparablePathRecord? after,
        IReadOnlyDictionary<string, SourceIdentityAssessment> sourceIdentity)
    {
        var assessments = PathSourceAssessments(before, after, sourceIdentity).ToArray();
        if (assessments.Any(assessment => assessment.BlocksStrongClaims))
        {
            return assessments.Any(assessment => assessment.Unknown)
                ? CombinedDependencyDiffClassifications.UnknownAnalysisGap
                : CombinedDependencyDiffClassifications.NeedsReviewDiff;
        }

        if (before is null && assessments.Any(assessment => assessment.BeforeReduced))
        {
            return CombinedDependencyDiffClassifications.AddedWithBeforeGap;
        }

        if (after is null && assessments.Any(assessment => assessment.AfterReduced))
        {
            return CombinedDependencyDiffClassifications.RemovedWithAfterGap;
        }

        if (before?.NeedsReview == true || after?.NeedsReview == true)
        {
            return CombinedDependencyDiffClassifications.NeedsReviewDiff;
        }

        return before is null
            ? CombinedDependencyDiffClassifications.Added
            : after is null
                ? CombinedDependencyDiffClassifications.Removed
                : CombinedDependencyDiffClassifications.ChangedEvidence;
    }

    private static IReadOnlyList<CombinedCoverageCaveat> PathCaveats(
        ComparablePathRecord? before,
        ComparablePathRecord? after,
        IReadOnlyDictionary<string, SourceIdentityAssessment> sourceIdentity)
    {
        return PathSourceAssessments(before, after, sourceIdentity)
            .SelectMany(assessment => assessment.Caveats)
            .GroupBy(caveat => $"{caveat.SourceLabel}\u001f{caveat.Code}\u001f{caveat.Message}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(caveat => caveat.SourceLabel, StringComparer.Ordinal)
            .ThenBy(caveat => caveat.Code, StringComparer.Ordinal)
            .ThenBy(caveat => caveat.Message, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<SourceIdentityAssessment> PathSourceAssessments(
        ComparablePathRecord? before,
        ComparablePathRecord? after,
        IReadOnlyDictionary<string, SourceIdentityAssessment> sourceIdentity)
    {
        return (before?.Evidence.SourceTransitions ?? [])
            .Concat(after?.Evidence.SourceTransitions ?? [])
            .Distinct(StringComparer.Ordinal)
            .Select(label => sourceIdentity.TryGetValue(label, out var assessment) ? assessment : null)
            .Where(assessment => assessment is not null)!;
    }

    private static string ClassifyRecord(ComparableDiffRecord? before, ComparableDiffRecord? after, int beforeCount, int afterCount, IReadOnlyDictionary<string, SourceIdentityAssessment> sourceIdentity)
    {
        var sourceLabel = before?.Evidence.SourceLabel ?? after?.Evidence.SourceLabel ?? string.Empty;
        if (sourceIdentity.TryGetValue(sourceLabel, out var identity) && identity.BlocksStrongClaims)
        {
            return identity.Unknown ? CombinedDependencyDiffClassifications.UnknownAnalysisGap : CombinedDependencyDiffClassifications.NeedsReviewDiff;
        }

        if (beforeCount > 1 || afterCount > 1 || before?.NeedsReview == true || after?.NeedsReview == true)
        {
            return CombinedDependencyDiffClassifications.NeedsReviewDiff;
        }

        if (before is null)
        {
            return sourceIdentity.TryGetValue(sourceLabel, out var afterIdentity) && afterIdentity.BeforeReduced
                ? CombinedDependencyDiffClassifications.AddedWithBeforeGap
                : CombinedDependencyDiffClassifications.Added;
        }

        if (after is null)
        {
            return sourceIdentity.TryGetValue(sourceLabel, out var beforeIdentity) && beforeIdentity.AfterReduced
                ? CombinedDependencyDiffClassifications.RemovedWithAfterGap
                : CombinedDependencyDiffClassifications.Removed;
        }

        return CombinedDependencyDiffClassifications.ChangedEvidence;
    }

    private static CombinedDiffRow CreateRow(string ruleId, string stableKey, string classification, ComparableDiffRecord? before, ComparableDiffRecord? after)
    {
        var changeType = ChangeType(classification);
        return new CombinedDiffRow(
            DiffId(changeType, classification, stableKey, ruleId),
            changeType,
            classification,
            Confidence(classification),
            stableKey,
            ruleId,
            before?.Evidence,
            after?.Evidence,
            [],
            []);
    }

    private static IReadOnlyList<CombinedDiffRow> SortAndCapRows(IReadOnlyList<CombinedDiffRow> rows, int maxRows, string ruleId, List<CombinedDiffGap> gaps)
    {
        var sorted = rows
            .OrderBy(row => ClassificationRank(row.Classification))
            .ThenBy(row => row.Before?.EvidenceKind ?? row.After?.EvidenceKind, StringComparer.Ordinal)
            .ThenBy(row => row.Before?.SourceLabel ?? row.After?.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.StableKey, StringComparer.Ordinal)
            .ThenBy(row => row.DiffId, StringComparer.Ordinal)
            .ToArray();
        if (sorted.Length <= maxRows)
        {
            return sorted;
        }

        gaps.Add(new CombinedDiffGap($"gap:truncated:{ruleId}", "TruncatedByLimit", null, null, TruncationRuleId, EvidenceTiers.Tier4Unknown, CombinedDependencyDiffClassifications.NeedsReviewDiff, $"{ruleId} produced {sorted.Length} rows; output was capped at {maxRows}.", [], []));
        return sorted.Take(maxRows).ToArray();
    }

    private static IReadOnlyList<CombinedDiffGap> SortAndCapGaps(IReadOnlyList<CombinedDiffGap> gaps, int maxGaps, out bool truncated)
    {
        var sorted = gaps
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
            .ThenBy(gap => gap.EvidenceKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.RuleId, StringComparer.Ordinal)
            .ThenBy(gap => gap.Message, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        truncated = sorted.Length > maxGaps;
        if (!truncated)
        {
            return sorted;
        }

        return [.. sorted.Take(maxGaps - 1), new CombinedDiffGap("gap:truncated:gaps", "TruncatedByLimit", null, null, TruncationRuleId, EvidenceTiers.Tier4Unknown, CombinedDependencyDiffClassifications.NeedsReviewDiff, $"Gap output was capped at {maxGaps}.", [], [])];
    }

    private static IReadOnlyDictionary<string, SourceIdentityAssessment> ValidateSourceIdentity(
        IReadOnlyList<CombinedReportSource> beforeSources,
        IReadOnlyList<CombinedReportSource> afterSources,
        bool allowMismatch,
        List<CombinedDiffGap> gaps)
    {
        var result = new Dictionary<string, SourceIdentityAssessment>(StringComparer.Ordinal);
        var beforeByLabel = beforeSources.ToDictionary(source => source.Label, StringComparer.Ordinal);
        var afterByLabel = afterSources.ToDictionary(source => source.Label, StringComparer.Ordinal);
        foreach (var label in beforeByLabel.Keys.Concat(afterByLabel.Keys).Distinct(StringComparer.Ordinal))
        {
            beforeByLabel.TryGetValue(label, out var before);
            afterByLabel.TryGetValue(label, out var after);
            var caveats = new List<CombinedCoverageCaveat>();
            var beforeReduced = before is not null && SourceReduced(before);
            var afterReduced = after is not null && SourceReduced(after);
            var blocks = false;
            var unknown = false;
            if (before is not null && after is not null)
            {
                if (IdentityConflict(before, after))
                {
                    var message = $"Source `{label}` identity differs between snapshots.";
                    if (!allowMismatch)
                    {
                        throw new InvalidDataException($"{message} Use --allow-identity-mismatch to continue with review-tier classifications.");
                    }

                    blocks = true;
                    caveats.Add(new CombinedCoverageCaveat(label, "SourceIdentityChanged", message));
                    gaps.Add(new CombinedDiffGap($"gap:identity:{CombinedReportHelpers.Hash(label, 16)}", "SourceIdentityChanged", label, "source", SourceRuleId, EvidenceTiers.Tier4Unknown, CombinedDependencyDiffClassifications.NeedsReviewDiff, message, [], []));
                }
                else if (IdentityUnverified(before, after))
                {
                    blocks = true;
                    caveats.Add(new CombinedCoverageCaveat(label, "SourceIdentityUnverified", $"Source `{label}` identity could not be fully verified."));
                }

                if (UnknownSha(before) || UnknownSha(after))
                {
                    blocks = true;
                    unknown = true;
                    caveats.Add(new CombinedCoverageCaveat(label, "UnknownCommitSha", $"Source `{label}` has unknown commit SHA on at least one side."));
                }

                if (CheckoutRootChanged(before, after))
                {
                    caveats.Add(new CombinedCoverageCaveat(label, "SourceCheckoutRootChanged", $"Source `{label}` was scanned from different checkout root paths; source identity matched, so diffing continued."));
                }
            }

            result[label] = new SourceIdentityAssessment(beforeReduced, afterReduced, blocks, unknown, caveats);
        }

        return result;
    }

    private static bool IdentityConflict(CombinedReportSource before, CombinedReportSource after)
    {
        return BothPresentDifferent(before.Language, after.Language)
            || BothPresentDifferent(before.RepoName, after.RepoName)
            || BothPresentDifferent(before.RemoteUrl, after.RemoteUrl);
    }

    private static bool IdentityUnverified(CombinedReportSource before, CombinedReportSource after)
    {
        return string.IsNullOrWhiteSpace(before.Language)
            || string.IsNullOrWhiteSpace(after.Language)
            || (string.IsNullOrWhiteSpace(before.RemoteUrl) && string.IsNullOrWhiteSpace(before.GitRootHash) && string.IsNullOrWhiteSpace(before.RepoName))
            || (string.IsNullOrWhiteSpace(after.RemoteUrl) && string.IsNullOrWhiteSpace(after.GitRootHash) && string.IsNullOrWhiteSpace(after.RepoName));
    }

    private static bool CheckoutRootChanged(CombinedReportSource before, CombinedReportSource after)
    {
        return BothPresentDifferent(before.GitRootHash, after.GitRootHash)
            || BothPresentDifferent(before.ScanRootPathHash, after.ScanRootPathHash);
    }

    private static bool BothPresentDifferent(string? before, string? after)
    {
        return !string.IsNullOrWhiteSpace(before)
            && !string.IsNullOrWhiteSpace(after)
            && !string.Equals(before, after, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SourceReduced(CombinedReportSource source)
    {
        return !source.AnalysisLevel.Equals("Level1SemanticAnalysis", StringComparison.Ordinal)
            || !source.BuildStatus.Equals("Succeeded", StringComparison.Ordinal)
            || UnknownSha(source);
    }

    private static bool UnknownSha(CombinedReportSource source)
    {
        return string.IsNullOrWhiteSpace(source.CommitSha) || source.CommitSha.Equals("unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static CombinedDiffSnapshotInfo ToSnapshotInfo(string side, CombinedReadResult read)
    {
        var warnings = read.CoverageWarnings.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return new CombinedDiffSnapshotInfo(
            side,
            read.Sources
                .OrderBy(source => source.Label, StringComparer.Ordinal)
                .Select(source => new CombinedDiffSourceInfo(
                    source.Label,
                    source.Language,
                    source.ScanId,
                    source.CommitSha,
                    $"repo-hash:{CombinedReportHelpers.Hash(source.RemoteUrl ?? source.RepoName ?? "unknown", 24)}",
                    source.ScanRootPathHash ?? source.GitRootHash,
                    $"{source.AnalysisLevel}/{source.BuildStatus}",
                    read.KnownGaps.Where(gap => gap.SourceIndexId == source.SourceIndexId).Select(gap => gap.Category).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()))
                .ToArray(),
            warnings.Length == 0 ? "FullEvidenceAvailable" : "ReducedCoverage",
            warnings,
            read.Sources
                .GroupBy(source => source.ScannerVersion, StringComparer.Ordinal)
                .Select(group => new KeyValuePair<string, string>(group.Key, group.Count().ToString()))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray());
    }

    private static string RenderMarkdown(CombinedDependencyDiffReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Dependency Diff Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("- This report compares static evidence, not runtime behavior.");
        builder.AppendLine($"- Report coverage: `{report.ReportCoverage}`");
        builder.AppendLine($"- Path comparison: `{(report.Query.IncludePaths ? "requested" : "not requested")}`");
        builder.AppendLine($"- Source diffs: `{report.Summary.SourceDiffCount}`");
        builder.AppendLine($"- Coverage diffs: `{report.Summary.CoverageDiffCount}`");
        builder.AppendLine($"- Endpoint diffs: `{report.Summary.EndpointDiffCount}`");
        builder.AppendLine($"- Surface diffs: `{report.Summary.SurfaceDiffCount}`");
        builder.AppendLine($"- Edge diffs: `{report.Summary.EdgeDiffCount}`");
        builder.AppendLine($"- Path diffs: `{report.Summary.PathDiffCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        AppendList(builder, "Coverage warnings", report.CoverageWarnings);
        builder.AppendLine();
        builder.AppendLine("## Compared Snapshots");
        builder.AppendLine();
        AppendSnapshot(builder, report.BeforeSnapshot);
        AppendSnapshot(builder, report.AfterSnapshot);
        builder.AppendLine("## Sources");
        builder.AppendLine();
        AppendRows(builder, report.SourceDiffs, "| Classification | Key | Before | After |", "| --- | --- | --- | --- |");
        builder.AppendLine("## Coverage Changes");
        builder.AppendLine();
        AppendRows(builder, report.CoverageDiffs, "| Classification | Key | Before | After |", "| --- | --- | --- | --- |");
        builder.AppendLine("## Endpoint Diffs");
        builder.AppendLine();
        AppendRows(builder, report.EndpointDiffs, "| Classification | Key | Before | After |", "| --- | --- | --- | --- |");
        builder.AppendLine("## Surface Diffs");
        builder.AppendLine();
        AppendRows(builder, report.SurfaceDiffs, "| Classification | Key | Before | After |", "| --- | --- | --- | --- |");
        builder.AppendLine("## Edge Diffs");
        builder.AppendLine();
        AppendRows(builder, report.EdgeDiffs, "| Classification | Key | Before | After |", "| --- | --- | --- | --- |");
        builder.AppendLine("## Path Diffs");
        builder.AppendLine();
        if (!report.Query.IncludePaths)
        {
            builder.AppendLine("Path comparison was not run.");
            builder.AppendLine();
        }
        else
        {
            AppendPathRows(builder, report.PathDiffs);
        }

        builder.AppendLine("## Gaps");
        builder.AppendLine();
        if (report.Gaps.Count == 0)
        {
            builder.AppendLine("No gaps found.");
            builder.AppendLine();
        }
        else
        {
            builder.AppendLine("| Kind | Classification | Source | Message |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var gap in report.Gaps.Take(200))
            {
                builder.AppendLine($"| {Cell(gap.GapKind)} | {Cell(gap.Classification)} | {Cell(gap.SourceLabel ?? "n/a")} | {Cell(gap.Message)} |");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations)
        {
            builder.AppendLine($"- {limitation}");
        }

        return builder.ToString();
    }

    private static void AppendSnapshot(StringBuilder builder, CombinedDiffSnapshotInfo snapshot)
    {
        builder.AppendLine($"### {snapshot.Side}");
        builder.AppendLine();
        builder.AppendLine($"- Coverage: `{snapshot.ReportCoverage}`");
        builder.AppendLine($"- Sources: `{snapshot.Sources.Count}`");
        builder.AppendLine();
    }

    private static void AppendRows(StringBuilder builder, IReadOnlyList<CombinedDiffRow> rows, string header, string separator)
    {
        if (rows.Count == 0)
        {
            builder.AppendLine("No evidence found.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine(header);
        builder.AppendLine(separator);
        foreach (var row in rows.Take(200))
        {
            builder.AppendLine($"| {Cell(row.Classification)} | {Cell(row.StableKey)} | {Cell(EvidenceLabel(row.Before))} | {Cell(EvidenceLabel(row.After))} |");
        }

        builder.AppendLine();
    }

    private static void AppendPathRows(StringBuilder builder, IReadOnlyList<CombinedPathDiffRow> rows)
    {
        if (rows.Count == 0)
        {
            builder.AppendLine("No path diffs found.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Classification | Signature | Before | After |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var row in rows.Take(100))
        {
            builder.AppendLine($"| {Cell(row.Classification)} | {Cell(row.PathSignature)} | {Cell(row.Before?.PathClassification ?? "n/a")} | {Cell(row.After?.PathClassification ?? "n/a")} |");
        }

        builder.AppendLine();
    }

    private static void AppendList(StringBuilder builder, string title, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        builder.AppendLine($"- {title}:");
        foreach (var value in values.OrderBy(value => value, StringComparer.Ordinal))
        {
            builder.AppendLine($"  - {Cell(value)}");
        }
    }

    private static string EvidenceLabel(CombinedDiffEvidence? evidence)
    {
        if (evidence is null)
        {
            return "n/a";
        }

        return $"{evidence.SourceLabel} {evidence.DisplayName ?? evidence.EvidenceKind ?? "evidence"} {evidence.FilePath ?? "n/a"}:{evidence.StartLine ?? 0}";
    }

    private static CombinedDiffEvidence Evidence(
        CombinedReportSource? source,
        string evidenceKind,
        string? displayName,
        string? ruleId,
        string? evidenceTier,
        string? filePath,
        int? startLine,
        int? endLine,
        IReadOnlyList<KeyValuePair<string, string>> safeMetadata,
        IReadOnlyList<string> supportingFactIds,
        IReadOnlyList<string> supportingEdgeIds)
    {
        return new CombinedDiffEvidence(
            source?.Label ?? "unknown",
            source?.Language,
            source?.ScanId,
            source?.CommitSha,
            evidenceKind,
            displayName,
            ruleId,
            evidenceTier,
            CombinedReportHelpers.SafePath(filePath),
            startLine,
            endLine,
            safeMetadata,
            supportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            supportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static CombinedReportSource? SourceFor(CombinedReadResult read, string? sourceIndexId)
    {
        return read.Sources.FirstOrDefault(source => string.Equals(source.SourceIndexId, sourceIndexId, StringComparison.Ordinal));
    }

    private static bool MatchesSource(CombinedReportSource source, string? sourceFilter)
    {
        return MatchesSourceLabel(source.Label, sourceFilter);
    }

    private static bool MatchesSourceLabel(string? sourceLabel, string? sourceFilter)
    {
        return string.IsNullOrWhiteSpace(sourceFilter) || string.Equals(sourceLabel, sourceFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EndpointMatchesSource(CombinedEndpointFinding finding, string? sourceFilter)
    {
        return string.IsNullOrWhiteSpace(sourceFilter)
            || string.Equals(finding.ClientSourceLabel, sourceFilter, StringComparison.OrdinalIgnoreCase)
            || string.Equals(finding.ServerSourceLabel, sourceFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static KeyValuePair<string, string?> Pair(string key, string? value) => new(key, value);

    private static string? HashOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : CombinedReportHelpers.Hash(value, 24);
    }

    private static IReadOnlyList<string> SortedIds(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string MetadataHash(IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        return CombinedReportHelpers.Hash(string.Join("\n", metadata.Select(pair => $"{pair.Key}={pair.Value}")));
    }

    private static string EvidenceHash(CombinedDiffEvidence evidence)
    {
        var values = new[]
        {
            evidence.SourceLabel,
            evidence.Language ?? string.Empty,
            evidence.ScanId ?? string.Empty,
            evidence.CommitSha ?? string.Empty,
            evidence.EvidenceKind ?? string.Empty,
            evidence.DisplayName ?? string.Empty,
            evidence.RuleId ?? string.Empty,
            evidence.EvidenceTier ?? string.Empty,
            evidence.FilePath ?? string.Empty,
            (evidence.StartLine ?? 0).ToString(),
            (evidence.EndLine ?? 0).ToString(),
            string.Join("\u001f", evidence.SafeMetadata.Select(pair => $"{pair.Key}={pair.Value}")),
            string.Join("\u001f", evidence.SupportingFactIds),
            string.Join("\u001f", evidence.SupportingEdgeIds)
        };
        return CombinedReportHelpers.Hash(string.Join("\n", values));
    }

    private static string RuleFamily(string? ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            return "unknown";
        }

        var index = ruleId.LastIndexOf(".v", StringComparison.Ordinal);
        return index > 0 && ruleId[(index + 2)..].All(char.IsDigit) ? ruleId[..index] : ruleId;
    }

    private static string NormalizeEdgeKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        return value.Trim().Replace("_", "-", StringComparison.Ordinal).Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant();
    }

    private static string DiffId(string changeType, string classification, string stableKey, string ruleId)
    {
        return $"diff:{CombinedReportHelpers.Hash($"{changeType}\n{classification}\n{stableKey}\n{ruleId}")}";
    }

    private static string ChangeType(string classification)
    {
        return classification switch
        {
            CombinedDependencyDiffClassifications.Added or CombinedDependencyDiffClassifications.AddedWithBeforeGap => "Added",
            CombinedDependencyDiffClassifications.Removed or CombinedDependencyDiffClassifications.RemovedWithAfterGap => "Removed",
            CombinedDependencyDiffClassifications.ChangedEvidence or CombinedDependencyDiffClassifications.NeedsReviewDiff => "Changed",
            CombinedDependencyDiffClassifications.NoDiffEvidence => "None",
            _ => "Gap"
        };
    }

    private static string Confidence(string classification)
    {
        return classification switch
        {
            CombinedDependencyDiffClassifications.Added or CombinedDependencyDiffClassifications.Removed => "High",
            CombinedDependencyDiffClassifications.ChangedEvidence or CombinedDependencyDiffClassifications.NoDiffEvidence => "Medium",
            _ => "Low"
        };
    }

    private static int ClassificationRank(string classification)
    {
        return classification switch
        {
            CombinedDependencyDiffClassifications.Removed => 0,
            CombinedDependencyDiffClassifications.Added => 1,
            CombinedDependencyDiffClassifications.ChangedEvidence => 2,
            CombinedDependencyDiffClassifications.RemovedWithAfterGap => 3,
            CombinedDependencyDiffClassifications.AddedWithBeforeGap => 4,
            CombinedDependencyDiffClassifications.NeedsReviewDiff => 5,
            CombinedDependencyDiffClassifications.UnknownAnalysisGap => 6,
            CombinedDependencyDiffClassifications.SelectorNoMatch => 7,
            CombinedDependencyDiffClassifications.NoPathEvidence => 8,
            CombinedDependencyDiffClassifications.NoDiffEvidence => 9,
            _ => 99
        };
    }

    private static string Cell(string? value) => CombinedReportHelpers.Cell(value);

    private sealed record SourceIdentityAssessment(
        bool BeforeReduced,
        bool AfterReduced,
        bool BlocksStrongClaims,
        bool Unknown,
        IReadOnlyList<CombinedCoverageCaveat> Caveats);
}
