using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record PortfolioReportOptions(
    IReadOnlyList<PortfolioInputSpec> Inputs,
    string OutputPath,
    string Format = "markdown",
    string? ManifestPath = null,
    string? BeforeManifestPath = null,
    string? AfterManifestPath = null,
    string? Source = null,
    string? Group = null,
    string? Surface = null,
    string? SurfaceName = null,
    bool IncludeImpact = false,
    bool IncludePaths = false,
    bool IncludeReverse = false,
    int MaxSources = 200,
    int MaxSurfaceRows = 500,
    int MaxEndpointFindings = 500,
    int MaxSharedSurfaces = 200,
    int MaxEdgeRows = 500,
    int MaxDiffRows = 200,
    int MaxImpactItems = 100,
    int MaxPaths = 100,
    int MaxRoots = 100,
    int MaxDepth = 8,
    int MaxFrontier = 10000,
    int MaxGaps = 1000);

public sealed record PortfolioInputSpec(
    string Label,
    string IndexPath,
    string? ExpectedRepoIdentity = null,
    string? ExpectedCommitSha = null,
    string? Group = null,
    IReadOnlyList<string>? RoleTags = null);

public sealed record PortfolioReportResult(
    PortfolioReportDocument Report,
    string? MarkdownPath,
    string? JsonPath);

public sealed record PortfolioReportDocument(
    string ReportType,
    string Version,
    string Mode,
    PortfolioQuery Query,
    PortfolioSnapshot? PortfolioSnapshot,
    PortfolioSnapshot? BeforeSnapshot,
    PortfolioSnapshot? AfterSnapshot,
    PortfolioSummary Summary,
    IReadOnlyList<PortfolioInputRow> Inputs,
    IReadOnlyList<PortfolioSourceRow> Sources,
    PortfolioSection<PortfolioSourceCoverageRow> SourceCoverage,
    PortfolioSection<PortfolioEndpointFindingRow> EndpointAlignment,
    PortfolioSection<PortfolioSurfaceRow> DependencySurfaces,
    PortfolioSection<PortfolioEdgeRow> DependencyEdges,
    PortfolioSection<PortfolioSharedSurfaceRow> SharedSurfaces,
    PortfolioSection<PortfolioContextRow> PathContext,
    PortfolioSection<PortfolioContextRow> ReverseContext,
    PortfolioSection<PortfolioDiffRow> PortfolioDiff,
    PortfolioSection<PortfolioContextRow> PortfolioImpact,
    PortfolioSection<PortfolioContextRow> ReleaseReviewContext,
    IReadOnlyList<PortfolioGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record PortfolioQuery(
    string? Source,
    string? Group,
    string? Surface,
    string? SurfaceName,
    bool IncludeImpact,
    bool IncludePaths,
    bool IncludeReverse,
    int MaxSources,
    int MaxSurfaceRows,
    int MaxEndpointFindings,
    int MaxSharedSurfaces,
    int MaxEdgeRows,
    int MaxDiffRows,
    int MaxImpactItems,
    int MaxPaths,
    int MaxRoots,
    int MaxDepth,
    int MaxFrontier,
    int MaxGaps);

public sealed record PortfolioSnapshot(
    string? PortfolioId,
    string? SnapshotId,
    string SnapshotMode,
    IReadOnlyList<string> InputIds,
    IReadOnlyList<string> SourceIds,
    IReadOnlyList<string> CommitShas,
    string Coverage,
    IReadOnlyList<PortfolioGap> Gaps,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record PortfolioSummary(
    string ReportCoverage,
    string RollupClassification,
    int InputCount,
    int SourceCount,
    int EndpointFindingCount,
    int SurfaceCount,
    int EdgeCount,
    int SharedSurfaceCount,
    int GapCount,
    bool Truncated,
    IReadOnlyList<string> CoverageWarnings);

public sealed record PortfolioInputRow(
    string InputId,
    string Label,
    string IndexKind,
    string IndexPathHash,
    string? Group,
    IReadOnlyList<string> RoleTags,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record PortfolioSourceRow(
    string SourceId,
    string Label,
    string? ContainerLabel,
    string? OriginalSourceLabel,
    string? Group,
    IReadOnlyList<string> RoleTags,
    string? Language,
    string RepoName,
    string? RepoIdentityHash,
    string CommitSha,
    string ScanId,
    string ScannerVersion,
    string? ExtractorVersion,
    string AnalysisLevel,
    string BuildStatus,
    string CoverageStatus,
    IReadOnlyList<string> GapCategories,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record PortfolioSourceCoverageRow(
    string SourceId,
    string SourceLabel,
    string CoverageStatus,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string CommitSha,
    IReadOnlyList<string> Warnings);

public sealed record PortfolioSection<T>(
    string Status,
    string RollupClassification,
    IReadOnlyList<T> Rows,
    IReadOnlyList<PortfolioGap> Gaps,
    int OmittedCount,
    IReadOnlyList<string> Limitations);

public sealed record PortfolioEndpointFindingRow(
    string FindingId,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string HttpMethod,
    string? NormalizedPathKey,
    string? ClientSourceId,
    string? ClientSourceLabel,
    string? ClientCommitSha,
    string? ClientFilePath,
    int? ClientStartLine,
    int? ClientEndLine,
    string? ServerSourceId,
    string? ServerSourceLabel,
    string? ServerCommitSha,
    string? ServerFilePath,
    int? ServerStartLine,
    int? ServerEndLine,
    bool SameSource,
    string StaticMatchQuality,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record PortfolioSurfaceRow(
    string SurfaceId,
    string SurfaceKind,
    string DisplayName,
    string SourceId,
    string SourceLabel,
    string RuleId,
    string EvidenceTier,
    string CommitSha,
    string ExtractorVersion,
    string FilePath,
    int StartLine,
    int EndLine,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record PortfolioEdgeRow(
    string EdgeId,
    string EdgeKind,
    string SourceId,
    string SourceLabel,
    string RuleId,
    string EvidenceTier,
    string CommitSha,
    string ExtractorVersion,
    string FilePath,
    int StartLine,
    int EndLine,
    string? SourceSymbol,
    string? TargetSymbol,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record PortfolioSharedSurfaceRow(
    string GroupId,
    string SurfaceKind,
    string DisplayName,
    string Classification,
    string RuleId,
    string EvidenceTier,
    bool AllSourcesSame,
    IReadOnlyList<string> SourceLabels,
    IReadOnlyList<string> SupportingSurfaceIds,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record PortfolioContextRow(
    string ContextId,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string Message,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record PortfolioDiffRow(
    string DiffId,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string SourceLabel,
    string ChangeKind,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record PortfolioGap(
    string GapId,
    string GapKind,
    string Section,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string Message,
    string? SourceLabel = null,
    IReadOnlyList<KeyValuePair<string, string>>? Metadata = null);

public static class PortfolioReportStatuses
{
    public const string Available = "available";
    public const string NotRequested = "not_requested";
    public const string Unavailable = "unavailable";
    public const string Deferred = "deferred";
    public const string Truncated = "truncated";
}

public static class PortfolioReportClassifications
{
    public const string ActionableStaticEvidence = nameof(ActionableStaticEvidence);
    public const string ReviewRecommended = nameof(ReviewRecommended);
    public const string NoActionableEvidence = nameof(NoActionableEvidence);
    public const string PartialAnalysis = nameof(PartialAnalysis);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string TruncatedByLimit = nameof(TruncatedByLimit);
}

public static class PortfolioReporter
{
    private const string ReportType = "multi-index-portfolio-report";
    private const string Version = "1.0";
    private const string ManifestVersion = "1.0";
    private const string IdentityRuleId = "portfolio.identity.v1";
    private const string CoverageRuleId = "portfolio.coverage.v1";
    private const string SchemaRuleId = "portfolio.schema.v1";
    private const string EndpointRuleId = "portfolio.endpoint.alignment.v1";
    private const string SurfaceRuleId = "portfolio.surface.inventory.v1";
    private const string SurfaceGroupRuleId = "portfolio.surface.group.v1";
    private const string EdgeRuleId = "portfolio.edge.inventory.v1";
    private const string DiffRuleId = "portfolio.diff.v1";
    private const string ImpactRuleId = "portfolio.impact.context.v1";
    private const string OptionalContextRuleId = "portfolio.optional-context.v1";
    private const string SelectorRuleId = "portfolio.selector.v1";
    private const string TruncationRuleId = "portfolio.truncation.v1";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly IReadOnlyList<string> Limitations =
    [
        "Portfolio reports are static evidence summaries, not runtime topology, deployment, ownership, traffic, compatibility, vulnerability, license, or release-approval analysis.",
        "Endpoint alignment is static method/path evidence and does not prove runtime reachability, auth behavior, proxy behavior, base-path deployment, CORS behavior, or user exercise.",
        "Shared surfaces group safe static identifiers only. A shared package, table, route, config key, or symbol does not prove runtime coupling or ownership.",
        "SQL rows are static shape, hash, or mapping evidence and do not prove SQL execution, database schema existence, dialect validity, generated SQL equivalence, or branch feasibility.",
        "Package rows are static manifest/import/build metadata and do not prove restore success, runtime loading, lockfile resolution, compatibility, vulnerability, or license status.",
        "Reduced coverage means absence of evidence is not evidence of absence."
    ];

    public static async Task<PortfolioReportResult> WriteAsync(PortfolioReportOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "portfolio");
        EnsureExtensionlessOutputIsNotFile(options.OutputPath);
        var report = await BuildReportAsync(options, cancellationToken);
        var (markdownPath, jsonPath) = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "portfolio-report.md",
            "portfolio-report.json",
            report,
            RenderMarkdown,
            JsonOptions,
            cancellationToken);
        return new PortfolioReportResult(report, markdownPath, jsonPath);
    }

    public static async Task<PortfolioReportDocument> BuildReportAsync(PortfolioReportOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        if (!string.IsNullOrWhiteSpace(options.BeforeManifestPath) || !string.IsNullOrWhiteSpace(options.AfterManifestPath))
        {
            return await BuildComparisonReportAsync(options, cancellationToken);
        }

        var manifestInfo = string.IsNullOrWhiteSpace(options.ManifestPath)
            ? new PortfolioManifestInfo(null, null, "direct", options.Inputs)
            : ReadManifest(options.ManifestPath!);
        var read = await ReadSnapshotAsync(manifestInfo, options, "portfolio", cancellationToken);
        return BuildSingleSnapshotReport(options, manifestInfo, read);
    }

    private static async Task<PortfolioReportDocument> BuildComparisonReportAsync(PortfolioReportOptions options, CancellationToken cancellationToken)
    {
        var beforeManifest = ReadManifest(options.BeforeManifestPath!);
        var afterManifest = ReadManifest(options.AfterManifestPath!);
        var before = await ReadSnapshotAsync(beforeManifest, options, "before", cancellationToken);
        var after = await ReadSnapshotAsync(afterManifest, options, "after", cancellationToken);
        var gaps = before.Gaps.Concat(after.Gaps).ToList();
        var diffRows = BuildComparisonDiffRows(before, after, gaps, options.MaxDiffRows);
        var diffSection = DiffSection(
            diffRows.Rows,
            diffRows.OmittedCount,
            gaps.Where(gap => gap.Section == "portfolioDiff").ToArray(),
            ["Portfolio diff v1 compares source labels, source identity, projected safe dependency surfaces, and projected safe dependency edges."]);
        var endpointSection = UnavailableEndpointSection();
        var pathContext = OptionalContextSection(options.IncludePaths, "pathContext", "Path context requires a single portfolio snapshot in v1.");
        var reverseContext = OptionalContextSection(options.IncludeReverse, "reverseContext", "Reverse context requires a single portfolio snapshot in v1.");
        var portfolioImpact = options.IncludeImpact
            ? DeferredContextSection("portfolioImpact", ImpactRuleId, "Portfolio impact composition is deferred unless compatible combined before/after snapshots are provided in a follow-up.")
            : NotRequestedContextSection("portfolioImpact", ImpactRuleId, "Portfolio impact context is off by default.");
        var releaseReviewContext = NotRequestedContextSection("releaseReviewContext", OptionalContextRuleId, "Release-review import is deferred in portfolio v1.");
        gaps.AddRange(endpointSection.Gaps);
        gaps.AddRange(pathContext.Gaps);
        gaps.AddRange(reverseContext.Gaps);
        gaps.AddRange(portfolioImpact.Gaps);
        gaps.AddRange(releaseReviewContext.Gaps);

        var allGaps = CapGaps(gaps, options.MaxGaps);
        endpointSection = WithCappedGaps(endpointSection, allGaps);
        pathContext = WithCappedGaps(pathContext, allGaps);
        reverseContext = WithCappedGaps(reverseContext, allGaps);
        portfolioImpact = WithCappedGaps(portfolioImpact, allGaps);
        releaseReviewContext = WithCappedGaps(releaseReviewContext, allGaps);
        return new PortfolioReportDocument(
            ReportType,
            Version,
            "PortfolioComparisonV1",
            Query(options),
            null,
            Snapshot(beforeManifest, before, "before"),
            Snapshot(afterManifest, after, "after"),
            Summary(options, before.Inputs.Concat(after.Inputs).ToArray(), before.Sources.Concat(after.Sources).ToArray(), [], [], [], [], diffRows.Rows, diffRows.OmittedCount > 0, allGaps),
            before.Inputs.Concat(after.Inputs).OrderBy(input => input.Label, StringComparer.Ordinal).ThenBy(input => input.InputId, StringComparer.Ordinal).ToArray(),
            before.Sources.Concat(after.Sources).OrderBy(source => source.Label, StringComparer.Ordinal).ThenBy(source => source.SourceId, StringComparer.Ordinal).ToArray(),
            SourceCoverageSection(before.Sources.Concat(after.Sources).ToArray(), allGaps, 0),
            endpointSection,
            EmptySection<PortfolioSurfaceRow>("dependencySurfaces", "Portfolio surface inventory is not emitted for before/after comparison v1."),
            EmptySection<PortfolioEdgeRow>("dependencyEdges", "Portfolio edge inventory is not emitted for before/after comparison v1."),
            EmptySection<PortfolioSharedSurfaceRow>("sharedSurfaces", "Shared surface grouping is not emitted for before/after comparison v1."),
            pathContext,
            reverseContext,
            diffSection,
            portfolioImpact,
            releaseReviewContext,
            allGaps,
            Limitations);
    }

    private static PortfolioReportDocument BuildSingleSnapshotReport(PortfolioReportOptions options, PortfolioManifestInfo manifestInfo, PortfolioReadResult read)
    {
        var gaps = read.Gaps.ToList();
        var activeSourceIds = DuplicateSourceIds(read.Sources, gaps);
        var cappedSources = ApplySourceFilters(read.Sources, options, gaps);
        var filteredSources = cappedSources.Rows;
        var filteredSourceIds = filteredSources.Select(source => source.SourceId).ToHashSet(StringComparer.Ordinal);
        var activeFilteredSourceIds = filteredSourceIds.Where(activeSourceIds.Contains).ToHashSet(StringComparer.Ordinal);
        var activeSources = filteredSources.Where(source => activeFilteredSourceIds.Contains(source.SourceId)).ToArray();
        var sourceById = filteredSources.ToDictionary(source => source.SourceId, StringComparer.Ordinal);
        var filteredFacts = read.Facts
            .Where(fact => activeFilteredSourceIds.Contains(fact.SourceIndexId))
            .ToArray();
        if (!string.IsNullOrWhiteSpace(options.Source) && filteredSources.Count == 0)
        {
            gaps.Add(Gap("SelectorNoMatch", "sourceCoverage", SelectorRuleId, PortfolioReportClassifications.SelectorNoMatch, "Source selector matched no portfolio sources."));
        }

        var endpointFindings = CombinedDependencyReporter.MatchEndpoints(ToCombinedSources(activeSources), filteredFacts)
            .Where(finding => EndpointMatchesSelector(finding, options))
            .Select(finding => ToPortfolioEndpointFinding(finding, sourceById))
            .OrderBy(row => row.Classification, StringComparer.Ordinal)
            .ThenBy(row => row.HttpMethod, StringComparer.Ordinal)
            .ThenBy(row => row.NormalizedPathKey, StringComparer.Ordinal)
            .ThenBy(row => row.ClientSourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.ServerSourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.FindingId, StringComparer.Ordinal)
            .ToArray();
        var cappedEndpoints = Cap(endpointFindings, options.MaxEndpointFindings, gaps, "endpointAlignment");

        var surfaces = CombinedDependencyReporter.BuildSurfaces(filteredFacts)
            .Where(surface => SurfaceMatchesSelector(surface, options))
            .Select(surface => ToPortfolioSurface(surface, sourceById))
            .OrderBy(row => row.SurfaceKind, StringComparer.Ordinal)
            .ThenBy(row => row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
            .ThenBy(row => row.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.StartLine)
            .ThenBy(row => row.SurfaceId, StringComparer.Ordinal)
            .ToArray();
        if (!string.IsNullOrWhiteSpace(options.Surface) && surfaces.Length == 0)
        {
            gaps.Add(Gap("SelectorNoMatch", "dependencySurfaces", SelectorRuleId, PortfolioReportClassifications.SelectorNoMatch, "Surface selector matched no dependency surface rows."));
        }
        var cappedSurfaces = Cap(surfaces, options.MaxSurfaceRows, gaps, "dependencySurfaces");

        var edges = read.Edges
            .Where(edge => activeFilteredSourceIds.Contains(edge.SourceIndexId))
            .Select(edge => ToPortfolioEdge(edge, sourceById))
            .OrderBy(row => row.EdgeKind, StringComparer.Ordinal)
            .ThenBy(row => row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.SourceSymbol, StringComparer.Ordinal)
            .ThenBy(row => row.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(row => row.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.StartLine)
            .ThenBy(row => row.EdgeId, StringComparer.Ordinal)
            .ToArray();
        var cappedEdges = Cap(edges, options.MaxEdgeRows, gaps, "dependencyEdges");

        var shared = BuildSharedSurfaces(cappedSurfaces.Rows, activeSourceIds)
            .OrderBy(row => row.SurfaceKind, StringComparer.Ordinal)
            .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
            .ThenBy(row => row.GroupId, StringComparer.Ordinal)
            .ToArray();
        var cappedShared = Cap(shared, options.MaxSharedSurfaces, gaps, "sharedSurfaces");

        var pathContext = OptionalContextSection(options.IncludePaths, "pathContext", "Path traversal is deferred in portfolio v1; run tracemap paths on a combined index for bounded path evidence.");
        var reverseContext = OptionalContextSection(options.IncludeReverse, "reverseContext", "Reverse traversal is deferred in portfolio v1; run tracemap reverse on a combined index for bounded reverse evidence.");
        var portfolioImpact = options.IncludeImpact
            ? DeferredContextSection("portfolioImpact", ImpactRuleId, "Portfolio impact composition is deferred in v1; run tracemap impact or release-review for compatible before/after indexes.")
            : NotRequestedContextSection("portfolioImpact", ImpactRuleId, "Portfolio impact context is off by default.");
        var releaseReviewContext = NotRequestedContextSection("releaseReviewContext", OptionalContextRuleId, "Release-review import is deferred in portfolio v1.");
        gaps.AddRange(pathContext.Gaps);
        gaps.AddRange(reverseContext.Gaps);
        gaps.AddRange(portfolioImpact.Gaps);
        gaps.AddRange(releaseReviewContext.Gaps);

        var allGaps = CapGaps(gaps, options.MaxGaps);
        pathContext = WithCappedGaps(pathContext, allGaps);
        reverseContext = WithCappedGaps(reverseContext, allGaps);
        portfolioImpact = WithCappedGaps(portfolioImpact, allGaps);
        releaseReviewContext = WithCappedGaps(releaseReviewContext, allGaps);
        return new PortfolioReportDocument(
            ReportType,
            Version,
            "PortfolioSnapshotV1",
            Query(options),
            Snapshot(manifestInfo, read, "portfolio"),
            null,
            null,
            Summary(options, read.Inputs, filteredSources, cappedEndpoints.Rows, cappedSurfaces.Rows, cappedEdges.Rows, cappedShared.Rows, [], cappedSources.OmittedCount + cappedEndpoints.OmittedCount + cappedSurfaces.OmittedCount + cappedEdges.OmittedCount + cappedShared.OmittedCount > 0, allGaps),
            read.Inputs.OrderBy(input => input.Label, StringComparer.Ordinal).ThenBy(input => input.InputId, StringComparer.Ordinal).ToArray(),
            filteredSources.OrderBy(source => source.Label, StringComparer.Ordinal).ThenBy(source => source.SourceId, StringComparer.Ordinal).ToArray(),
            SourceCoverageSection(filteredSources, allGaps, cappedSources.OmittedCount),
            Section(cappedEndpoints.Rows, cappedEndpoints.OmittedCount, allGaps.Where(gap => gap.Section == "endpointAlignment").ToArray(), ["Endpoint alignment is static method/path matching."]),
            Section(cappedSurfaces.Rows, cappedSurfaces.OmittedCount, allGaps.Where(gap => gap.Section == "dependencySurfaces").ToArray(), ["Dependency surfaces preserve source provenance and safe metadata only."]),
            Section(cappedEdges.Rows, cappedEdges.OmittedCount, allGaps.Where(gap => gap.Section == "dependencyEdges").ToArray(), ["Dependency edges are static code evidence."]),
            Section(cappedShared.Rows, cappedShared.OmittedCount, allGaps.Where(gap => gap.Section == "sharedSurfaces").ToArray(), ["Shared surfaces are grouped by safe static identity and do not prove runtime coupling."]),
            pathContext,
            reverseContext,
            NotRequestedDiffSection(),
            portfolioImpact,
            releaseReviewContext,
            allGaps,
            Limitations);
    }

    private static async Task<PortfolioReadResult> ReadSnapshotAsync(PortfolioManifestInfo manifest, PortfolioReportOptions options, string side, CancellationToken cancellationToken)
    {
        RejectDuplicateLabels(manifest.Inputs);
        var inputs = new List<PortfolioInputRow>();
        var sources = new List<PortfolioSourceRow>();
        var facts = new List<CombinedFactRow>();
        var edges = new List<CombinedDependencyEdgeRow>();
        var gaps = new List<PortfolioGap>();
        foreach (var input in manifest.Inputs.OrderBy(input => input.Label, StringComparer.Ordinal))
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = input.IndexPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();
            await using var connection = new SqliteConnection(connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"portfolio could not read input `{SafeToken(input.Label)}`.", ex);
            }

            var isCombined = await TableExistsAsync(connection, "index_sources", cancellationToken)
                && await TableExistsAsync(connection, "combined_facts", cancellationToken);
            inputs.Add(new PortfolioInputRow(
                InputId(input.Label, side),
                SafeToken(input.Label),
                isCombined ? "combined" : "single",
                $"path-hash:{CombinedReportHelpers.Hash(input.IndexPath, 16)}",
                SafeOptional(input.Group),
                (input.RoleTags ?? []).Select(SafeToken).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                CombinedReportHelpers.SortedMetadata([
                    new("expectedCommitSha", SafeOptional(input.ExpectedCommitSha)),
                    new("expectedRepoIdentity", SafeOptional(input.ExpectedRepoIdentity))
                ])));

            if (isCombined)
            {
                var read = await CombinedDependencyReporter.ReadAsync(connection, cancellationToken);
                var knownGapsBySource = read.KnownGaps
                    .GroupBy(gap => gap.SourceIndexId, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(gap => gap.Category).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                        StringComparer.Ordinal);
                foreach (var source in read.Sources)
                {
                    knownGapsBySource.TryGetValue(source.SourceIndexId, out var knownGapCategories);
                    var row = ToPortfolioSource(source, input, side, knownGapCategories ?? []);
                    sources.Add(row);
                    AddExpectedIdentityGaps(input, row, gaps);
                }

                facts.AddRange(read.Facts.Select(fact => PrefixFact(fact, input.Label, side)));
                edges.AddRange(read.Edges.Select(edge => PrefixEdge(edge, input.Label, side)));
                foreach (var warning in read.CoverageWarnings)
                {
                    gaps.Add(Gap("ReducedCoverage", "sourceCoverage", CoverageRuleId, PortfolioReportClassifications.PartialAnalysis, SafeMessage(warning)));
                }
            }
            else
            {
                var read = await ReadSingleIndexAsync(connection, input, side, cancellationToken);
                sources.Add(read.Source);
                facts.AddRange(read.Facts);
                edges.AddRange(read.Edges);
                gaps.AddRange(read.Gaps);
                AddExpectedIdentityGaps(input, read.Source, gaps);
            }
        }

        return new PortfolioReadResult(inputs, sources, facts, edges, gaps);
    }

    private static async Task<SingleIndexReadResult> ReadSingleIndexAsync(SqliteConnection connection, PortfolioInputSpec input, string side, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "scan_manifest", cancellationToken)
            || !await TableExistsAsync(connection, "facts", cancellationToken))
        {
            throw new InvalidDataException($"portfolio input `{SafeToken(input.Label)}` is not a valid TraceMap index.");
        }

        await using var manifestCommand = connection.CreateCommand();
        manifestCommand.CommandText = """
            select scan_id, repo, commit_sha, scanner_version, analysis_level, build_status, manifest_json
            from scan_manifest
            order by scan_id
            limit 1;
            """;
        await using var manifestReader = await manifestCommand.ExecuteReaderAsync(cancellationToken);
        if (!await manifestReader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException($"portfolio input `{SafeToken(input.Label)}` has no scan manifest.");
        }

        var scanId = manifestReader.GetString(0);
        var repo = manifestReader.GetString(1);
        var commitSha = manifestReader.GetString(2);
        var scannerVersion = manifestReader.GetString(3);
        var analysisLevel = manifestReader.GetString(4);
        var buildStatus = manifestReader.GetString(5);
        var manifestJson = manifestReader.GetString(6);
        var manifest = ParseManifestJson(manifestJson);
        var language = CorrectLanguage(scannerVersion, null);
        var sourceId = SourceId(input.Label, side, input.Label);
        var source = new PortfolioSourceRow(
            sourceId,
            SafeToken(input.Label),
            null,
            null,
            SafeOptional(input.Group),
            (input.RoleTags ?? []).Select(SafeToken).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            language,
            SafeRepoName(repo),
            RepoHash(manifest.RemoteUrl, repo, manifest.GitRootHash),
            SafeCommit(commitSha),
            scanId,
            scannerVersion,
            scannerVersion,
            analysisLevel,
            buildStatus,
            CoverageStatus(analysisLevel, buildStatus, commitSha, manifest.KnownGaps),
            GapCategories(manifest.KnownGaps),
            CombinedReportHelpers.SortedMetadata([
                new("scanRootRelativePath", CombinedReportHelpers.SafePath(manifest.ScanRootRelativePath)),
                new("scanRootPathHash", manifest.ScanRootPathHash),
                new("gitRootHash", manifest.GitRootHash)
            ]));

        var gaps = SourceGaps(source);
        var facts = await ReadSingleFactsAsync(connection, input, source, side, cancellationToken);
        var edges = await ReadSingleEdgesAsync(connection, input, source, side, facts, cancellationToken);
        return new SingleIndexReadResult(source, facts, edges, gaps);
    }

    private static async Task<IReadOnlyList<CombinedFactRow>> ReadSingleFactsAsync(SqliteConnection connection, PortfolioInputSpec input, PortfolioSourceRow source, string side, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select fact_id, scan_id, repo, commit_sha, fact_type, rule_id, evidence_tier,
                   source_symbol, target_symbol, contract_element, file_path, start_line, end_line, properties_json
            from facts
            order by file_path, start_line, fact_type, fact_id;
            """;
        var rows = new List<CombinedFactRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var originalId = reader.GetString(0);
            rows.Add(new CombinedFactRow(
                FactId(input.Label, side, originalId),
                source.SourceId,
                source.Label,
                originalId,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                CombinedReportHelpers.SafePath(reader.GetString(10)),
                reader.GetInt32(11),
                reader.GetInt32(12),
                ParseProperties(reader.IsDBNull(13) ? string.Empty : reader.GetString(13))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CombinedDependencyEdgeRow>> ReadSingleEdgesAsync(
        SqliteConnection connection,
        PortfolioInputSpec input,
        PortfolioSourceRow source,
        string side,
        IReadOnlyList<CombinedFactRow> facts,
        CancellationToken cancellationToken)
    {
        var rows = new List<CombinedDependencyEdgeRow>();
        foreach (var fact in facts.Where(fact => fact.FactType is FactTypes.CallEdge or FactTypes.ObjectCreated or FactTypes.ArgumentPassed or FactTypes.SymbolRelationship))
        {
            rows.Add(new CombinedDependencyEdgeRow(
                EdgeKind(fact),
                source.SourceId,
                source.Label,
                $"edge:{fact.CombinedFactId}",
                fact.OriginalFactId,
                fact.SourceSymbol,
                fact.TargetSymbol,
                Property(fact.Properties, "targetAssemblyName", "calleeAssemblyName", "createdTypeAssemblyName"),
                Property(fact.Properties, "targetAssemblyVersion", "calleeAssemblyVersion", "createdTypeAssemblyVersion"),
                fact.RuleId,
                fact.EvidenceTier,
                fact.FilePath,
                fact.StartLine,
                fact.EndLine));
        }

        if (await TableExistsAsync(connection, "call_edges", cancellationToken))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select fact_id, evidence_tier, rule_id, caller_symbol, callee_symbol, callee_assembly_name, callee_assembly_version, file_path, start_line, end_line
                from call_edges
                order by file_path, start_line, fact_id;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var factId = reader.GetString(0);
                rows.Add(new CombinedDependencyEdgeRow(
                    "call",
                    source.SourceId,
                    source.Label,
                    $"edge:{FactId(input.Label, side, factId)}",
                    factId,
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetString(2),
                    reader.GetString(1),
                    CombinedReportHelpers.SafePath(reader.GetString(7)),
                    reader.GetInt32(8),
                    reader.GetInt32(9)));
            }
        }

        return rows
            .GroupBy(row => row.EdgeId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(row => row.EdgeKind, StringComparer.Ordinal)
            .ThenBy(row => row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.SourceSymbol, StringComparer.Ordinal)
            .ThenBy(row => row.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(row => row.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.StartLine)
            .ToArray();
    }

    private static PortfolioManifestInfo ReadManifest(string manifestPath)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(manifestPath), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("portfolio manifest could not be parsed.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            var version = root.TryGetProperty("version", out var versionElement) && versionElement.ValueKind == JsonValueKind.String ? versionElement.GetString() : null;
            if (!string.Equals(version, ManifestVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException("portfolio manifest version is unsupported.");
            }

            if (!root.TryGetProperty("inputs", out var inputsElement) || inputsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("portfolio manifest requires an inputs array.");
            }

            var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? Directory.GetCurrentDirectory();
            var inputs = new List<PortfolioInputSpec>();
            foreach (var entry in inputsElement.EnumerateArray())
            {
                var label = RequiredString(entry, "label", "portfolio manifest input requires label.");
                var indexPath = RequiredString(entry, "indexPath", "portfolio manifest input requires indexPath.");
                var resolved = Path.IsPathFullyQualified(indexPath) ? indexPath : Path.GetFullPath(Path.Combine(baseDirectory, indexPath));
                inputs.Add(new PortfolioInputSpec(
                    label,
                    resolved,
                    OptionalString(entry, "expectedRepoIdentity"),
                    OptionalString(entry, "expectedCommitSha"),
                    OptionalString(entry, "group"),
                    OptionalStrings(entry, "roleTags")));
            }

            RejectDuplicateLabels(inputs);
            return new PortfolioManifestInfo(
                OptionalString(root, "portfolioId"),
                OptionalString(root, "snapshotId"),
                "manifest",
                inputs);
        }
    }

    private static PortfolioSnapshot Snapshot(PortfolioManifestInfo manifest, PortfolioReadResult read, string mode)
    {
        return new PortfolioSnapshot(
            SafeOptional(manifest.PortfolioId),
            SafeOptional(manifest.SnapshotId),
            mode,
            read.Inputs.Select(input => input.InputId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            read.Sources.Select(source => source.SourceId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            read.Sources.Select(source => source.CommitSha).Where(value => value != "unknown").Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            read.Sources.Any(source => source.CoverageStatus != "FullEvidenceAvailable") || read.Gaps.Count > 0 ? "ReducedCoverage" : "FullEvidenceAvailable",
            read.Gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(),
            CombinedReportHelpers.SortedMetadata([
                new("inputMode", manifest.InputMode),
                new("sourceCount", read.Sources.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))
            ]));
    }

    private static PortfolioQuery Query(PortfolioReportOptions options)
    {
        return new PortfolioQuery(
            SafeOptional(options.Source),
            SafeOptional(options.Group),
            SafeOptional(options.Surface),
            SafeOptional(options.SurfaceName),
            options.IncludeImpact,
            options.IncludePaths,
            options.IncludeReverse,
            options.MaxSources,
            options.MaxSurfaceRows,
            options.MaxEndpointFindings,
            options.MaxSharedSurfaces,
            options.MaxEdgeRows,
            options.MaxDiffRows,
            options.MaxImpactItems,
            options.MaxPaths,
            options.MaxRoots,
            options.MaxDepth,
            options.MaxFrontier,
            options.MaxGaps);
    }

    private static PortfolioSummary Summary(
        PortfolioReportOptions options,
        IReadOnlyList<PortfolioInputRow> inputs,
        IReadOnlyList<PortfolioSourceRow> sources,
        IReadOnlyList<PortfolioEndpointFindingRow> endpoints,
        IReadOnlyList<PortfolioSurfaceRow> surfaces,
        IReadOnlyList<PortfolioEdgeRow> edges,
        IReadOnlyList<PortfolioSharedSurfaceRow> shared,
        IReadOnlyList<PortfolioDiffRow> diffs,
        bool truncated,
        IReadOnlyList<PortfolioGap> gaps)
    {
        var warnings = sources
            .Where(source => source.CoverageStatus != "FullEvidenceAvailable")
            .Select(source => $"Source `{source.Label}` has {source.CoverageStatus}.")
            .Concat(gaps.Where(gap => gap.Classification is PortfolioReportClassifications.PartialAnalysis or PortfolioReportClassifications.UnknownAnalysisGap).Select(gap => gap.Message))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var coverage = warnings.Length == 0 && !truncated ? "FullEvidenceAvailable" : "ReducedCoverage";
        var rowClassifications = diffs.Select(row => row.Classification).ToList();
        if (endpoints.Count + surfaces.Count + edges.Count + shared.Count > 0)
        {
            rowClassifications.Add(PortfolioReportClassifications.ActionableStaticEvidence);
        }

        return new PortfolioSummary(
            coverage,
            SelectRollup(gaps, rowClassifications, truncated),
            inputs.Count,
            sources.Count,
            endpoints.Count,
            surfaces.Count,
            edges.Count,
            shared.Count,
            gaps.Count,
            truncated,
            warnings);
    }

    private static PortfolioSection<PortfolioSourceCoverageRow> SourceCoverageSection(IReadOnlyList<PortfolioSourceRow> sources, IReadOnlyList<PortfolioGap> gaps, int omittedCount)
    {
        var rows = sources
            .OrderBy(source => source.Label, StringComparer.Ordinal)
            .ThenBy(source => source.SourceId, StringComparer.Ordinal)
            .Select(source => new PortfolioSourceCoverageRow(
                source.SourceId,
                source.Label,
                source.CoverageStatus,
                source.CoverageStatus == "FullEvidenceAvailable" ? PortfolioReportClassifications.NoActionableEvidence : PortfolioReportClassifications.PartialAnalysis,
                CoverageRuleId,
                source.CoverageStatus == "FullEvidenceAvailable" ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier4Unknown,
                source.CommitSha,
                source.GapCategories))
            .ToArray();
        return Section(rows, omittedCount, gaps.Where(gap => gap.Section == "sourceCoverage").ToArray(), ["Coverage is static scan coverage, not runtime exercise coverage."]);
    }

    private static PortfolioSection<PortfolioDiffRow> DiffSection(
        IReadOnlyList<PortfolioDiffRow> rows,
        int omittedCount,
        IReadOnlyList<PortfolioGap> gaps,
        IReadOnlyList<string> limitations)
    {
        var status = omittedCount > 0 ? PortfolioReportStatuses.Truncated : PortfolioReportStatuses.Available;
        return new PortfolioSection<PortfolioDiffRow>(
            status,
            SelectRollup(gaps, rows.Select(row => row.Classification).ToArray(), omittedCount > 0),
            rows,
            gaps,
            omittedCount,
            limitations);
    }

    private static PortfolioSection<T> Section<T>(IReadOnlyList<T> rows, int omittedCount, IReadOnlyList<PortfolioGap> gaps, IReadOnlyList<string> limitations)
    {
        var status = omittedCount > 0 ? PortfolioReportStatuses.Truncated : PortfolioReportStatuses.Available;
        if (rows.Count == 0 && gaps.Count == 0)
        {
            status = PortfolioReportStatuses.Available;
        }

        return new PortfolioSection<T>(status, SelectRollup(gaps, rows.Count > 0, omittedCount > 0), rows, gaps, omittedCount, limitations);
    }

    private static PortfolioSection<T> EmptySection<T>(string section, string message)
    {
        return new PortfolioSection<T>(PortfolioReportStatuses.Available, PortfolioReportClassifications.NoActionableEvidence, [], [], 0, [message]);
    }

    private static PortfolioSection<PortfolioEndpointFindingRow> UnavailableEndpointSection()
    {
        var gap = Gap("Unavailable", "endpointAlignment", EndpointRuleId, PortfolioReportClassifications.PartialAnalysis, "Endpoint alignment is unavailable for before/after portfolio comparison v1.");
        return new PortfolioSection<PortfolioEndpointFindingRow>(PortfolioReportStatuses.Unavailable, PortfolioReportClassifications.PartialAnalysis, [], [gap], 0, ["Endpoint alignment is available for single portfolio snapshots in v1."]);
    }

    private static PortfolioSection<PortfolioContextRow> OptionalContextSection(bool requested, string section, string message)
    {
        return requested
            ? DeferredContextSection(section, OptionalContextRuleId, message)
            : NotRequestedContextSection(section, OptionalContextRuleId, message);
    }

    private static PortfolioSection<PortfolioContextRow> DeferredContextSection(string section, string ruleId, string message)
    {
        var gap = Gap("Deferred", section, ruleId, PortfolioReportClassifications.PartialAnalysis, message);
        return new PortfolioSection<PortfolioContextRow>(PortfolioReportStatuses.Deferred, PortfolioReportClassifications.PartialAnalysis, [], [gap], 0, [message]);
    }

    private static PortfolioSection<PortfolioContextRow> NotRequestedContextSection(string section, string ruleId, string message)
    {
        return new PortfolioSection<PortfolioContextRow>(PortfolioReportStatuses.NotRequested, PortfolioReportClassifications.NoActionableEvidence, [], [], 0, [message]);
    }

    private static PortfolioSection<PortfolioDiffRow> NotRequestedDiffSection()
    {
        return new PortfolioSection<PortfolioDiffRow>(PortfolioReportStatuses.NotRequested, PortfolioReportClassifications.NoActionableEvidence, [], [], 0, ["Portfolio diff requires --before-manifest and --after-manifest."]);
    }

    private static PortfolioSection<T> WithCappedGaps<T>(PortfolioSection<T> section, IReadOnlyList<PortfolioGap> allGaps)
    {
        var visibleGapIds = allGaps.Select(gap => gap.GapId).ToHashSet(StringComparer.Ordinal);
        return section with
        {
            Gaps = section.Gaps.Where(gap => visibleGapIds.Contains(gap.GapId)).ToArray()
        };
    }

    private static Capped<T> Cap<T>(IReadOnlyList<T> rows, int maxRows, List<PortfolioGap> gaps, string section)
    {
        if (rows.Count <= maxRows)
        {
            return new Capped<T>(rows, 0);
        }

        var omitted = rows.Count - maxRows;
        gaps.Add(Gap("TruncatedByLimit", section, TruncationRuleId, PortfolioReportClassifications.TruncatedByLimit, $"{section} omitted {omitted} row(s) because a configured cap was reached."));
        return new Capped<T>(rows.Take(maxRows).ToArray(), omitted);
    }

    private static IReadOnlyList<PortfolioGap> CapGaps(IReadOnlyList<PortfolioGap> gaps, int maxGaps)
    {
        var ordered = gaps
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => GapRank(gap.Classification))
            .ThenBy(gap => gap.Section, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length <= maxGaps)
        {
            return ordered;
        }

        var omitted = ordered.Length - maxGaps;
        return ordered.Take(maxGaps)
            .Append(Gap("TruncatedByLimit", "gaps", TruncationRuleId, PortfolioReportClassifications.TruncatedByLimit, $"Gaps omitted {omitted} row(s) because --max-gaps was reached."))
            .ToArray();
    }

    private static IReadOnlyList<PortfolioSharedSurfaceRow> BuildSharedSurfaces(IReadOnlyList<PortfolioSurfaceRow> surfaces, IReadOnlySet<string> activeSourceIds)
    {
        return surfaces
            .Where(surface => activeSourceIds.Contains(surface.SourceId))
            .Where(surface => SharedKey(surface) is not null)
            .GroupBy(surface => SharedKey(surface)!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
            {
                var rows = group.OrderBy(surface => surface.SourceLabel, StringComparer.Ordinal).ThenBy(surface => surface.SurfaceId, StringComparer.Ordinal).ToArray();
                var first = rows[0];
                var tier = rows.Any(row => row.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual || row.EvidenceTier == EvidenceTiers.Tier4Unknown)
                    ? EvidenceTiers.Tier3SyntaxOrTextual
                    : rows.Select(row => row.EvidenceTier).OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? EvidenceTiers.Tier2Structural;
                return new PortfolioSharedSurfaceRow(
                    $"shared:{CombinedReportHelpers.Hash(group.Key, 20)}",
                    first.SurfaceKind,
                    first.DisplayName,
                    tier == EvidenceTiers.Tier1Semantic || tier == EvidenceTiers.Tier2Structural ? PortfolioReportClassifications.ActionableStaticEvidence : PortfolioReportClassifications.ReviewRecommended,
                    SurfaceGroupRuleId,
                    tier,
                    rows.Select(row => row.SourceLabel).Distinct(StringComparer.Ordinal).Count() == 1,
                    rows.Select(row => row.SourceLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    rows.Select(row => row.SurfaceId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    CombinedReportHelpers.SortedMetadata([
                        new("groupKeyHash", CombinedReportHelpers.Hash(group.Key, 16)),
                        new("supportingRowCount", rows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    ]));
            })
            .ToArray();
    }

    private static string? SharedKey(PortfolioSurfaceRow surface)
    {
        var kind = surface.SurfaceKind;
        var metadata = surface.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        string? value = kind switch
        {
            "http-client" or "http-route" => Metadata(metadata, "httpMethod") is { } method && Metadata(metadata, "normalizedPathKey") is { } path ? $"{method} {path}" : null,
            "sql-query" or "sql-persistence" => Metadata(metadata, "tableName") ?? Metadata(metadata, "shapeHash") ?? Metadata(metadata, "textHash"),
            "package-config" => Metadata(metadata, "ecosystem") is { } eco && Metadata(metadata, "packageName") is { } package ? $"{eco}:{package}" : Metadata(metadata, "packageName") ?? Metadata(metadata, "configKey"),
            _ => surface.DisplayName
        };
        return string.IsNullOrWhiteSpace(value) || value == "n/a" || value == "unknown" ? null : $"{kind}:{value}";
    }

    private static PortfolioSurfaceRow ToPortfolioSurface(CombinedDependencySurfaceRow surface, IReadOnlyDictionary<string, PortfolioSourceRow> sourceById)
    {
        sourceById.TryGetValue(surface.SourceIndexId, out var source);
        return new PortfolioSurfaceRow(
            $"surface:{CombinedReportHelpers.Hash($"{surface.SourceLabel}:{surface.SurfaceKind}:{surface.DisplayName}:{surface.CombinedFactId}", 20)}",
            surface.SurfaceKind,
            SafeToken(surface.DisplayName),
            surface.SourceIndexId,
            SafeToken(surface.SourceLabel),
            surface.RuleId,
            surface.EvidenceTier,
            SafeCommit(surface.CommitSha),
            source?.ExtractorVersion ?? source?.ScannerVersion ?? string.Empty,
            CombinedReportHelpers.SafePath(surface.FilePath),
            surface.StartLine,
            surface.EndLine,
            [surface.OriginalFactId],
            CombinedReportHelpers.SortedMetadata([
                new("factType", surface.FactType),
                new("httpMethod", surface.HttpMethod),
                new("normalizedPathKey", surface.NormalizedPathKey),
                new("operationName", surface.OperationName),
                new("tableName", surface.TableName),
                new("columnNames", surface.ColumnNames),
                new("sourceKind", surface.SourceKind),
                new("shapeHash", surface.ShapeHash),
                new("textHash", surface.TextHash),
                new("textLength", surface.TextLength),
                new("packageName", surface.PackageName),
                new("version", surface.Version),
                new("configKey", surface.ConfigKey),
                new("ecosystem", surface.Ecosystem),
                new("manifestKind", surface.ManifestKind),
                new("dependencyScope", surface.DependencyScope),
                new("dependencyGroup", surface.DependencyGroup),
                new("packageManager", surface.PackageManager),
                new("versionHash", surface.VersionHash),
                new("redactionReason", surface.RedactionReason)
            ]));
    }

    private static PortfolioEndpointFindingRow ToPortfolioEndpointFinding(CombinedEndpointFinding finding, IReadOnlyDictionary<string, PortfolioSourceRow> sourceById)
    {
        var factIds = new[] { finding.ClientOriginalFactId, finding.ServerOriginalFactId }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var clientSource = finding.ClientSourceIndexId is not null && sourceById.TryGetValue(finding.ClientSourceIndexId, out var client) ? client : null;
        var serverSource = finding.ServerSourceIndexId is not null && sourceById.TryGetValue(finding.ServerSourceIndexId, out var server) ? server : null;
        return new PortfolioEndpointFindingRow(
            $"endpoint:{CombinedReportHelpers.Hash($"{finding.Classification}:{finding.HttpMethod}:{finding.NormalizedPathKey}:{finding.ClientSourceLabel}:{finding.ServerSourceLabel}:{string.Join(":", factIds)}", 20)}",
            finding.Classification,
            EndpointRuleId,
            finding.ClientEvidenceTier ?? finding.ServerEvidenceTier ?? EvidenceTiers.Tier4Unknown,
            finding.HttpMethod,
            finding.NormalizedPathKey,
            finding.ClientSourceIndexId,
            finding.ClientSourceLabel is null ? null : SafeToken(finding.ClientSourceLabel),
            SafeOptional(finding.ClientCommitSha),
            CombinedReportHelpers.SafePath(finding.ClientFilePath),
            finding.ClientStartLine,
            finding.ClientEndLine,
            finding.ServerSourceIndexId,
            finding.ServerSourceLabel is null ? null : SafeToken(finding.ServerSourceLabel),
            SafeOptional(finding.ServerCommitSha),
            CombinedReportHelpers.SafePath(finding.ServerFilePath),
            finding.ServerStartLine,
            finding.ServerEndLine,
            finding.SameSource,
            finding.StaticMatchQuality,
            factIds,
            CombinedReportHelpers.SortedMetadata([
                new("clientRuleId", finding.ClientRuleId),
                new("serverRuleId", finding.ServerRuleId),
                new("clientExtractorVersion", clientSource?.ExtractorVersion ?? clientSource?.ScannerVersion),
                new("serverExtractorVersion", serverSource?.ExtractorVersion ?? serverSource?.ScannerVersion),
                new("notes", string.Join("; ", finding.Notes.Select(SafeToken)))
            ]));
    }

    private static PortfolioEdgeRow ToPortfolioEdge(CombinedDependencyEdgeRow edge, IReadOnlyDictionary<string, PortfolioSourceRow> sourceById)
    {
        sourceById.TryGetValue(edge.SourceIndexId, out var source);
        return new PortfolioEdgeRow(
            $"edge:{CombinedReportHelpers.Hash($"{edge.SourceLabel}:{edge.EdgeKind}:{edge.EdgeId}", 20)}",
            edge.EdgeKind,
            edge.SourceIndexId,
            SafeToken(edge.SourceLabel),
            edge.RuleId,
            edge.EvidenceTier,
            source?.CommitSha ?? "unknown",
            source?.ExtractorVersion ?? source?.ScannerVersion ?? string.Empty,
            CombinedReportHelpers.SafePath(edge.FilePath),
            edge.StartLine,
            edge.EndLine,
            SafeOptional(edge.SourceSymbol),
            SafeOptional(edge.TargetSymbol),
            [edge.OriginalFactId],
            CombinedReportHelpers.SortedMetadata([
                new("targetAssemblyName", edge.TargetAssemblyName),
                new("targetAssemblyVersion", edge.TargetAssemblyVersion)
            ]));
    }

    private static Capped<PortfolioDiffRow> BuildComparisonDiffRows(PortfolioReadResult before, PortfolioReadResult after, List<PortfolioGap> gaps, int maxRows)
    {
        var beforeByKey = SourceComparisonMap(before.Sources, "before", gaps);
        var afterByKey = SourceComparisonMap(after.Sources, "after", gaps);
        var sourceRows = BuildSourceDiffRows(beforeByKey, afterByKey, gaps);
        var surfaceRows = CompareProjectedEvidence(
            ProjectSurfaces(before.Facts, before.Sources),
            ProjectSurfaces(after.Facts, after.Sources),
            beforeByKey,
            afterByKey,
            "SurfaceEvidence",
            gaps);
        var edgeRows = CompareProjectedEvidence(
            ProjectEdges(before.Edges, before.Sources),
            ProjectEdges(after.Edges, after.Sources),
            beforeByKey,
            afterByKey,
            "EdgeEvidence",
            gaps);
        var rows = sourceRows
            .Concat(surfaceRows)
            .Concat(edgeRows)
            .OrderBy(row => DiffClassificationRank(row.Classification))
            .ThenBy(row => row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.ChangeKind, StringComparer.Ordinal)
            .ThenBy(row => row.DiffId, StringComparer.Ordinal)
            .ToArray();
        return Cap(rows, maxRows, gaps, "portfolioDiff");
    }

    private static IReadOnlyList<PortfolioDiffRow> BuildSourceDiffRows(
        IReadOnlyDictionary<string, PortfolioSourceRow> beforeByKey,
        IReadOnlyDictionary<string, PortfolioSourceRow> afterByKey,
        List<PortfolioGap> gaps)
    {
        var keys = beforeByKey.Keys.Concat(afterByKey.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal);
        var rows = new List<PortfolioDiffRow>();
        foreach (var key in keys)
        {
            var hasBefore = beforeByKey.TryGetValue(key, out var b);
            var hasAfter = afterByKey.TryGetValue(key, out var a);
            var label = DiffSourceLabel(b ?? a!);
            if (!hasBefore && hasAfter)
            {
                rows.Add(DiffRow(label, "AddedSource", a!, "after"));
            }
            else if (hasBefore && !hasAfter)
            {
                rows.Add(DiffRow(label, "RemovedSource", b!, "before"));
            }
            else if (b!.CommitSha != a!.CommitSha || b.RepoIdentityHash != a.RepoIdentityHash)
            {
                if (b.RepoIdentityHash != a.RepoIdentityHash)
                {
                    gaps.Add(Gap("IdentityAmbiguous", "portfolioDiff", IdentityRuleId, PortfolioReportClassifications.ReviewRecommended, $"Source `{SafeToken(label)}` has different repo identity across portfolio snapshots.", SafeToken(label)));
                }

                rows.Add(DiffRow(label, "ChangedSourceEvidence", a, "after"));
            }
        }

        return rows
            .OrderBy(row => row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.ChangeKind, StringComparer.Ordinal)
            .ThenBy(row => row.DiffId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PortfolioComparableDiffRecord> ProjectSurfaces(IReadOnlyList<CombinedFactRow> facts, IReadOnlyList<PortfolioSourceRow> sources)
    {
        var sourceById = SourceByIdMap(sources);
        return CombinedDependencyReporter.BuildSurfaces(facts)
            .Where(surface => sourceById.ContainsKey(surface.SourceIndexId))
            .Select(surface =>
            {
                var source = sourceById[surface.SourceIndexId];
                var metadata = SurfaceDiffMetadata(surface);
                var identity = SurfaceDiffIdentityMetadata(surface);
                return new PortfolioComparableDiffRecord(
                    "surface",
                    SourceComparisonKey(source),
                    DiffSourceLabel(source),
                    $"surface:{SourceComparisonKey(source)}:{MetadataHash(identity)}",
                    MetadataHash(metadata),
                    EvidenceHash(metadata, surface.RuleId, surface.EvidenceTier, surface.FilePath, surface.StartLine, surface.EndLine, [surface.OriginalFactId], []),
                    SafeToken(surface.DisplayName),
                    surface.RuleId,
                    surface.EvidenceTier,
                    source.CoverageStatus,
                    [surface.OriginalFactId],
                    [],
                    surface.FilePath,
                    surface.StartLine,
                    surface.EndLine,
                    metadata,
                    surface.EvidenceTier is EvidenceTiers.Tier3SyntaxOrTextual or EvidenceTiers.Tier4Unknown
                        || HasReviewTierSurfaceIdentity(surface));
            })
            .OrderBy(record => record.StableKey, StringComparer.Ordinal)
            .ThenBy(record => record.MetadataHash, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PortfolioComparableDiffRecord> ProjectEdges(IReadOnlyList<CombinedDependencyEdgeRow> edges, IReadOnlyList<PortfolioSourceRow> sources)
    {
        var sourceById = SourceByIdMap(sources);
        return edges
            .Where(edge => sourceById.ContainsKey(edge.SourceIndexId))
            .Select(edge =>
            {
                var source = sourceById[edge.SourceIndexId];
                var metadata = EdgeDiffMetadata(edge);
                return new PortfolioComparableDiffRecord(
                    "edge",
                    SourceComparisonKey(source),
                    DiffSourceLabel(source),
                    $"edge:{SourceComparisonKey(source)}:{MetadataHash(metadata)}",
                    MetadataHash(metadata),
                    EvidenceHash(metadata, edge.RuleId, edge.EvidenceTier, edge.FilePath, edge.StartLine, edge.EndLine, [], [edge.EdgeId]),
                    SafeToken($"{edge.SourceSymbol ?? "unknown"} -> {edge.TargetSymbol ?? "unknown"}"),
                    edge.RuleId,
                    edge.EvidenceTier,
                    source.CoverageStatus,
                    [],
                    [edge.EdgeId],
                    edge.FilePath,
                    edge.StartLine,
                    edge.EndLine,
                    metadata,
                    edge.EvidenceTier is EvidenceTiers.Tier3SyntaxOrTextual or EvidenceTiers.Tier4Unknown
                        || string.IsNullOrWhiteSpace(edge.SourceSymbol)
                        || string.IsNullOrWhiteSpace(edge.TargetSymbol));
            })
            .OrderBy(record => record.StableKey, StringComparer.Ordinal)
            .ThenBy(record => record.MetadataHash, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PortfolioDiffRow> CompareProjectedEvidence(
        IReadOnlyList<PortfolioComparableDiffRecord> before,
        IReadOnlyList<PortfolioComparableDiffRecord> after,
        IReadOnlyDictionary<string, PortfolioSourceRow> beforeSources,
        IReadOnlyDictionary<string, PortfolioSourceRow> afterSources,
        string changeKindSuffix,
        List<PortfolioGap> gaps)
    {
        var rows = new List<PortfolioDiffRow>();
        var beforeGroups = before.GroupBy(record => record.StableKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var afterGroups = after.GroupBy(record => record.StableKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var duplicate in beforeGroups.Where(group => group.Value.Length > 1).Concat(afterGroups.Where(group => group.Value.Length > 1)))
        {
            var first = duplicate.Value.OrderBy(record => record.SourceLabel, StringComparer.Ordinal).First();
            gaps.Add(Gap("DuplicateProjectedIdentity", "portfolioDiff", DiffRuleId, PortfolioReportClassifications.ReviewRecommended, $"{first.EvidenceKind} comparison identity matched multiple evidence rows; review provenance before treating it as one change.", first.SourceLabel));
        }

        foreach (var key in beforeGroups.Keys.Concat(afterGroups.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            beforeGroups.TryGetValue(key, out var beforeRows);
            afterGroups.TryGetValue(key, out var afterRows);
            var beforeRecord = beforeRows?.OrderBy(row => row.MetadataHash, StringComparer.Ordinal).ThenBy(row => row.EvidenceHash, StringComparer.Ordinal).FirstOrDefault();
            var afterRecord = afterRows?.OrderBy(row => row.MetadataHash, StringComparer.Ordinal).ThenBy(row => row.EvidenceHash, StringComparer.Ordinal).FirstOrDefault();
            if (beforeRecord is not null
                && afterRecord is not null
                && beforeRecord.MetadataHash == afterRecord.MetadataHash
                && beforeRecord.EvidenceHash == afterRecord.EvidenceHash
                && beforeRows!.Length == afterRows!.Length)
            {
                continue;
            }

            var changeKind = beforeRecord is null
                ? $"Added{changeKindSuffix}"
                : afterRecord is null
                    ? $"Removed{changeKindSuffix}"
                    : $"Changed{changeKindSuffix}";
            rows.Add(ProjectedDiffRow(
                changeKind,
                ClassifyProjectedDiff(beforeRecord, afterRecord, beforeRows?.Length ?? 0, afterRows?.Length ?? 0, beforeSources, afterSources),
                beforeRecord,
                afterRecord));
        }

        return rows
            .OrderBy(row => row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.ChangeKind, StringComparer.Ordinal)
            .ThenBy(row => row.DiffId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, PortfolioSourceRow> SourceByIdMap(IReadOnlyList<PortfolioSourceRow> sources)
    {
        return sources
            .GroupBy(source => source.SourceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(source => DiffSourceLabel(source), StringComparer.Ordinal)
                    .ThenBy(source => source.ScanId, StringComparer.Ordinal)
                    .ThenBy(source => source.CommitSha, StringComparer.Ordinal)
                    .First(),
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, PortfolioSourceRow> SourceComparisonMap(IReadOnlyList<PortfolioSourceRow> sources, string side, List<PortfolioGap> gaps)
    {
        var result = new SortedDictionary<string, PortfolioSourceRow>(StringComparer.Ordinal);
        foreach (var group in sources.GroupBy(SourceComparisonKey, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var rows = group.OrderBy(source => source.SourceId, StringComparer.Ordinal).ToArray();
            if (rows.Length > 1)
            {
                gaps.Add(Gap("DuplicateSourceLabel", "portfolioDiff", IdentityRuleId, PortfolioReportClassifications.ReviewRecommended, $"{side} portfolio snapshot has duplicate source label `{DiffSourceLabel(rows[0])}`.", DiffSourceLabel(rows[0])));
            }

            result[group.Key] = rows[0];
        }

        return result;
    }

    private static string SourceComparisonKey(PortfolioSourceRow source)
    {
        return $"{source.ContainerLabel}:{source.Label}";
    }

    private static string DiffSourceLabel(PortfolioSourceRow source)
    {
        return string.IsNullOrWhiteSpace(source.ContainerLabel)
            ? source.Label
            : $"{source.ContainerLabel}/{source.Label}";
    }

    private static PortfolioDiffRow DiffRow(string label, string changeKind, PortfolioSourceRow source, string side)
    {
        return new PortfolioDiffRow(
            $"diff:{CombinedReportHelpers.Hash($"{label}:{changeKind}:{source.CommitSha}:{source.RepoIdentityHash}", 20)}",
            source.CoverageStatus == "FullEvidenceAvailable" ? PortfolioReportClassifications.ActionableStaticEvidence : PortfolioReportClassifications.PartialAnalysis,
            DiffRuleId,
            source.CoverageStatus == "FullEvidenceAvailable" ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier4Unknown,
            SafeToken(label),
            changeKind,
            CombinedReportHelpers.SortedMetadata([
                new("side", side),
                new("commitSha", source.CommitSha),
                new("repoIdentityHash", source.RepoIdentityHash)
            ]));
    }

    private static PortfolioDiffRow ProjectedDiffRow(
        string changeKind,
        string classification,
        PortfolioComparableDiffRecord? before,
        PortfolioComparableDiffRecord? after)
    {
        var evidence = after ?? before!;
        var beforeFactIds = before is null ? string.Empty : string.Join(",", before.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal));
        var afterFactIds = after is null ? string.Empty : string.Join(",", after.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal));
        var beforeEdgeIds = before is null ? string.Empty : string.Join(",", before.SupportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal));
        var afterEdgeIds = after is null ? string.Empty : string.Join(",", after.SupportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal));
        return new PortfolioDiffRow(
            $"diff:{CombinedReportHelpers.Hash($"{changeKind}:{classification}:{evidence.StableKey}:{before?.EvidenceHash}:{after?.EvidenceHash}", 20)}",
            classification,
            DiffRuleId,
            DiffEvidenceTier(classification, before, after),
            SafeToken(evidence.SourceLabel),
            changeKind,
            CombinedReportHelpers.SortedMetadata([
                new("evidenceKind", evidence.EvidenceKind),
                new("displayName", evidence.DisplayName),
                new("stableKeyHash", CombinedReportHelpers.Hash(evidence.StableKey, 20)),
                new("beforeMetadataHash", before?.MetadataHash),
                new("afterMetadataHash", after?.MetadataHash),
                new("beforeRuleId", before?.RuleId),
                new("afterRuleId", after?.RuleId),
                new("beforeEvidenceTier", before?.EvidenceTier),
                new("afterEvidenceTier", after?.EvidenceTier),
                new("beforeFilePath", before?.FilePath),
                new("beforeStartLine", before?.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("beforeEndLine", before?.EndLine.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("afterFilePath", after?.FilePath),
                new("afterStartLine", after?.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("afterEndLine", after?.EndLine.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("beforeSupportingFactIds", beforeFactIds),
                new("afterSupportingFactIds", afterFactIds),
                new("beforeSupportingEdgeIds", beforeEdgeIds),
                new("afterSupportingEdgeIds", afterEdgeIds)
            ]));
    }

    private static string ClassifyProjectedDiff(
        PortfolioComparableDiffRecord? before,
        PortfolioComparableDiffRecord? after,
        int beforeCount,
        int afterCount,
        IReadOnlyDictionary<string, PortfolioSourceRow> beforeSources,
        IReadOnlyDictionary<string, PortfolioSourceRow> afterSources)
    {
        var sourceKey = before?.SourceKey ?? after?.SourceKey ?? string.Empty;
        var sourceCoverage = new[] { before?.CoverageStatus, after?.CoverageStatus }
            .Concat(beforeSources.TryGetValue(sourceKey, out var beforeSource) ? [beforeSource.CoverageStatus] : [])
            .Concat(afterSources.TryGetValue(sourceKey, out var afterSource) ? [afterSource.CoverageStatus] : [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (beforeSources.TryGetValue(sourceKey, out var beforeIdentity)
            && afterSources.TryGetValue(sourceKey, out var afterIdentity)
            && !string.Equals(beforeIdentity.RepoIdentityHash, afterIdentity.RepoIdentityHash, StringComparison.Ordinal))
        {
            return PortfolioReportClassifications.ReviewRecommended;
        }

        if (sourceCoverage.Any(value => value != "FullEvidenceAvailable"))
        {
            return PortfolioReportClassifications.PartialAnalysis;
        }

        if (beforeCount > 1 || afterCount > 1 || before?.NeedsReview == true || after?.NeedsReview == true)
        {
            return PortfolioReportClassifications.ReviewRecommended;
        }

        return PortfolioReportClassifications.ActionableStaticEvidence;
    }

    private static string DiffEvidenceTier(string classification, PortfolioComparableDiffRecord? before, PortfolioComparableDiffRecord? after)
    {
        if (classification == PortfolioReportClassifications.PartialAnalysis)
        {
            return EvidenceTiers.Tier4Unknown;
        }

        var tiers = new[] { before?.EvidenceTier, after?.EvidenceTier }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
        if (tiers.Contains(EvidenceTiers.Tier4Unknown, StringComparer.Ordinal))
        {
            return EvidenceTiers.Tier4Unknown;
        }

        if (classification == PortfolioReportClassifications.ReviewRecommended
            || tiers.Contains(EvidenceTiers.Tier3SyntaxOrTextual, StringComparer.Ordinal))
        {
            return EvidenceTiers.Tier3SyntaxOrTextual;
        }

        if (tiers.Contains(EvidenceTiers.Tier2Structural, StringComparer.Ordinal))
        {
            return EvidenceTiers.Tier2Structural;
        }

        return tiers.FirstOrDefault() ?? EvidenceTiers.Tier4Unknown;
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SurfaceDiffMetadata(CombinedDependencySurfaceRow surface)
    {
        return CombinedReportHelpers.SortedMetadata([
            new("surfaceKind", surface.SurfaceKind),
            new("httpMethod", surface.HttpMethod),
            new("normalizedPathKey", surface.NormalizedPathKey),
            new("operationName", surface.OperationName),
            new("tableName", surface.TableName),
            new("columnNames", surface.ColumnNames),
            new("sourceKind", surface.SourceKind),
            new("shapeHash", surface.ShapeHash),
            new("textHash", surface.TextHash),
            new("textLength", surface.TextLength),
            new("ecosystem", surface.Ecosystem),
            new("manifestKind", surface.ManifestKind),
            new("packageName", surface.PackageName),
            new("packageManager", surface.PackageManager),
            new("dependencyScope", surface.DependencyScope),
            new("dependencyGroup", surface.DependencyGroup),
            new("version", surface.Version),
            new("versionHash", surface.VersionHash),
            new("redactionReason", surface.RedactionReason),
            new("configKey", surface.ConfigKey),
            new("identityFallbackHash", IsVolatileSqlIdentity(surface) ? CombinedReportHelpers.Hash(surface.OriginalFactId ?? surface.CombinedFactId, 24) : null)
        ]);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SurfaceDiffIdentityMetadata(CombinedDependencySurfaceRow surface)
    {
        if (surface.SurfaceKind == "package-config" && !string.IsNullOrWhiteSpace(surface.PackageName))
        {
            return CombinedReportHelpers.SortedMetadata([
                new("surfaceKind", surface.SurfaceKind),
                new("ecosystem", surface.Ecosystem),
                new("manifestKind", surface.ManifestKind),
                new("packageName", surface.PackageName),
                new("manifestPath", StablePackageManifestPath(surface.FilePath)),
                new("configKey", surface.ConfigKey)
            ]);
        }

        if (surface.SurfaceKind is "http-client" or "http-route")
        {
            return CombinedReportHelpers.SortedMetadata([
                new("surfaceKind", surface.SurfaceKind),
                new("httpMethod", surface.HttpMethod),
                new("normalizedPathKey", surface.NormalizedPathKey)
            ]);
        }

        return SurfaceDiffMetadata(surface);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> EdgeDiffMetadata(CombinedDependencyEdgeRow edge)
    {
        return CombinedReportHelpers.SortedMetadata([
            new("edgeKind", NormalizeEdgeKind(edge.EdgeKind)),
            new("sourceSymbol", edge.SourceSymbol),
            new("targetSymbol", edge.TargetSymbol),
            new("targetAssemblyName", edge.TargetAssemblyName),
            new("targetAssemblyVersion", edge.TargetAssemblyVersion),
            new("ruleFamily", RuleFamily(edge.RuleId))
        ]);
    }

    private static bool HasReviewTierSurfaceIdentity(CombinedDependencySurfaceRow surface)
    {
        return (surface.SurfaceKind == "sql-query" && (IsHashOnlySqlEvidence(surface) || IsVolatileSqlIdentity(surface)))
            || (surface.SurfaceKind == "package-config" && (string.IsNullOrWhiteSpace(surface.PackageName) || !string.IsNullOrWhiteSpace(surface.VersionHash) && string.IsNullOrWhiteSpace(surface.Version)));
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

    private static string NormalizeEdgeKind(string edgeKind)
    {
        return edgeKind.Trim().ToLowerInvariant();
    }

    private static string? StablePackageManifestPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)
            || filePath == "n/a"
            || filePath.StartsWith("absolute-path-hash:", StringComparison.Ordinal))
        {
            return null;
        }

        return filePath;
    }

    private static string RuleFamily(string ruleId)
    {
        var index = ruleId.LastIndexOf(".", StringComparison.Ordinal);
        return index > 0 ? ruleId[..index] : ruleId;
    }

    private static string MetadataHash(IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        return CombinedReportHelpers.Hash(string.Join("\u001f", metadata.Select(pair => $"{pair.Key}={pair.Value}")), 24);
    }

    private static string EvidenceHash(
        IReadOnlyList<KeyValuePair<string, string>> metadata,
        string ruleId,
        string evidenceTier,
        string filePath,
        int startLine,
        int endLine,
        IReadOnlyList<string> supportingFactIds,
        IReadOnlyList<string> supportingEdgeIds)
    {
        var key = string.Join("\u001f", [
            MetadataHash(metadata),
            ruleId,
            evidenceTier,
            StableEvidencePath(filePath),
            startLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
            endLine.ToString(System.Globalization.CultureInfo.InvariantCulture)
        ]);
        return CombinedReportHelpers.Hash(key, 24);
    }

    private static string StableEvidencePath(string? filePath)
    {
        return StablePackageManifestPath(filePath) ?? string.Empty;
    }

    private static void AddExpectedIdentityGaps(PortfolioInputSpec input, PortfolioSourceRow source, List<PortfolioGap> gaps)
    {
        if (!string.IsNullOrWhiteSpace(input.ExpectedCommitSha) && !string.Equals(input.ExpectedCommitSha, source.CommitSha, StringComparison.OrdinalIgnoreCase))
        {
            gaps.Add(Gap("ExpectedCommitMismatch", "sourceCoverage", IdentityRuleId, PortfolioReportClassifications.PartialAnalysis, $"Source `{source.Label}` commit SHA did not match manifest expectation.", source.Label));
        }

        if (!string.IsNullOrWhiteSpace(input.ExpectedRepoIdentity) && !string.Equals(input.ExpectedRepoIdentity, source.RepoIdentityHash, StringComparison.OrdinalIgnoreCase))
        {
            gaps.Add(Gap("ExpectedRepoIdentityMismatch", "sourceCoverage", IdentityRuleId, PortfolioReportClassifications.PartialAnalysis, $"Source `{source.Label}` repo identity did not match manifest expectation.", source.Label));
        }
    }

    private static IReadOnlySet<string> DuplicateSourceIds(IReadOnlyList<PortfolioSourceRow> sources, List<PortfolioGap> gaps)
    {
        var duplicateIds = sources
            .Where(source => !string.Equals(source.CommitSha, "unknown", StringComparison.OrdinalIgnoreCase))
            .GroupBy(source => $"{source.RepoIdentityHash}:{source.CommitSha}", StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group =>
            {
                var rows = group.OrderBy(source => source.Label, StringComparer.Ordinal).ToArray();
                gaps.Add(Gap("DuplicateSourceIdentity", "sourceCoverage", IdentityRuleId, PortfolioReportClassifications.PartialAnalysis, $"Duplicate source identity appears in {rows.Length} portfolio sources.", rows[0].Label));
                return rows.Skip(1).Select(source => source.SourceId);
            })
            .ToHashSet(StringComparer.Ordinal);
        return sources.Select(source => source.SourceId).Where(id => !duplicateIds.Contains(id)).ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<PortfolioGap> SourceGaps(PortfolioSourceRow source)
    {
        var gaps = new List<PortfolioGap>();
        if (source.CommitSha == "unknown")
        {
            gaps.Add(Gap("UnknownCommitSha", "sourceCoverage", IdentityRuleId, PortfolioReportClassifications.PartialAnalysis, $"Source `{source.Label}` has unknown commit SHA.", source.Label));
        }

        if (source.CoverageStatus != "FullEvidenceAvailable")
        {
            gaps.Add(Gap("ReducedCoverage", "sourceCoverage", CoverageRuleId, PortfolioReportClassifications.PartialAnalysis, $"Source `{source.Label}` has reduced static analysis coverage.", source.Label));
        }

        if (string.IsNullOrWhiteSpace(source.Language))
        {
            gaps.Add(Gap("UnknownLanguage", "sourceCoverage", SchemaRuleId, PortfolioReportClassifications.PartialAnalysis, $"Source `{source.Label}` language could not be inferred.", source.Label));
        }

        return gaps;
    }

    private static PortfolioSourceRow ToPortfolioSource(CombinedReportSource source, PortfolioInputSpec input, string side, IReadOnlyList<string> knownGapCategories)
    {
        var id = SourceId(input.Label, side, source.Label);
        var row = new PortfolioSourceRow(
            id,
            SafeToken(source.Label),
            SafeToken(input.Label),
            SafeToken(source.Label),
            SafeOptional(input.Group),
            (input.RoleTags ?? []).Select(SafeToken).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            source.Language,
            SafeRepoName(source.RepoName),
            RepoHash(source.RemoteUrl, source.RepoName, source.GitRootHash),
            SafeCommit(source.CommitSha),
            source.ScanId,
            source.ScannerVersion,
            source.ScannerVersion,
            source.AnalysisLevel,
            source.BuildStatus,
            CoverageStatus(source.AnalysisLevel, source.BuildStatus, source.CommitSha, knownGapCategories),
            knownGapCategories.Select(SafeToken).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            CombinedReportHelpers.SortedMetadata([
                new("containerLabel", input.Label),
                new("originalSourceLabel", source.Label),
                new("scanRootRelativePath", CombinedReportHelpers.SafePath(source.ScanRootRelativePath)),
                new("scanRootPathHash", source.ScanRootPathHash),
                new("gitRootHash", source.GitRootHash)
            ]));
        var gapCategories = row.GapCategories
            .Concat(SourceGaps(row).Select(gap => gap.GapKind))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return row with { GapCategories = gapCategories };
    }

    private static CombinedFactRow PrefixFact(CombinedFactRow fact, string inputLabel, string side)
    {
        return fact with
        {
            CombinedFactId = FactId(inputLabel, side, fact.CombinedFactId),
            SourceIndexId = SourceId(inputLabel, side, fact.SourceLabel),
            SourceLabel = SafeToken(fact.SourceLabel),
            FilePath = CombinedReportHelpers.SafePath(fact.FilePath)
        };
    }

    private static CombinedDependencyEdgeRow PrefixEdge(CombinedDependencyEdgeRow edge, string inputLabel, string side)
    {
        return edge with
        {
            SourceIndexId = SourceId(inputLabel, side, edge.SourceLabel),
            SourceLabel = SafeToken(edge.SourceLabel),
            EdgeId = $"edge:{FactId(inputLabel, side, edge.EdgeId)}",
            FilePath = CombinedReportHelpers.SafePath(edge.FilePath)
        };
    }

    private static Capped<PortfolioSourceRow> ApplySourceFilters(IReadOnlyList<PortfolioSourceRow> sources, PortfolioReportOptions options, List<PortfolioGap> gaps)
    {
        var rows = sources
            .Where(source => string.IsNullOrWhiteSpace(options.Source) || string.Equals(source.Label, options.Source, StringComparison.Ordinal) || string.Equals(source.ContainerLabel, options.Source, StringComparison.Ordinal))
            .Where(source => string.IsNullOrWhiteSpace(options.Group) || string.Equals(source.Group, options.Group, StringComparison.Ordinal) || source.RoleTags.Contains(options.Group, StringComparer.Ordinal))
            .ToArray();
        return Cap(rows, options.MaxSources, gaps, "sourceCoverage");
    }

    private static bool EndpointMatchesSelector(CombinedEndpointFinding finding, PortfolioReportOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SurfaceName)
            && !(finding.NormalizedPathKey?.Contains(options.SurfaceName, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return false;
        }

        return true;
    }

    private static bool SurfaceMatchesSelector(CombinedDependencySurfaceRow surface, PortfolioReportOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Surface) && !string.Equals(surface.SurfaceKind, options.Surface, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.SurfaceName))
        {
            return true;
        }

        return new[] { surface.DisplayName, surface.PackageName, surface.TableName, surface.ConfigKey, surface.NormalizedPathKey, surface.ShapeHash, surface.TextHash }
            .Any(value => value?.Contains(options.SurfaceName, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static IReadOnlyList<CombinedReportSource> ToCombinedSources(IReadOnlyList<PortfolioSourceRow> sources)
    {
        return sources.Select(source => new CombinedReportSource(
            source.SourceId,
            source.Label,
            $"portfolio:{CombinedReportHelpers.Hash(source.SourceId, 16)}",
            source.ScanId,
            source.RepoName,
            null,
            null,
            source.CommitSha,
            source.ScannerVersion,
            source.Language,
            source.Language,
            false,
            null,
            null,
            source.RepoIdentityHash,
            source.AnalysisLevel,
            source.BuildStatus)).ToArray();
    }

    private static string RenderMarkdown(PortfolioReportDocument report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Portfolio Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"Report type: `{CombinedReportHelpers.Cell(report.ReportType)}`");
        builder.AppendLine($"Mode: `{CombinedReportHelpers.Cell(report.Mode)}`");
        builder.AppendLine($"Coverage: `{CombinedReportHelpers.Cell(report.Summary.ReportCoverage)}`");
        builder.AppendLine($"Rollup: `{CombinedReportHelpers.Cell(report.Summary.RollupClassification)}`");
        builder.AppendLine($"Inputs: `{report.Summary.InputCount}`");
        builder.AppendLine($"Sources: `{report.Summary.SourceCount}`");
        builder.AppendLine($"Endpoint findings: `{report.Summary.EndpointFindingCount}`");
        builder.AppendLine($"Dependency surfaces: `{report.Summary.SurfaceCount}`");
        builder.AppendLine($"Dependency edges: `{report.Summary.EdgeCount}`");
        builder.AppendLine($"Shared surfaces: `{report.Summary.SharedSurfaceCount}`");
        builder.AppendLine($"Gaps: `{report.Summary.GapCount}`");
        if (report.Summary.CoverageWarnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Coverage warnings:");
            foreach (var warning in report.Summary.CoverageWarnings)
            {
                builder.AppendLine($"- {CombinedReportHelpers.Cell(warning)}");
            }
        }

        RenderInputs(builder, report.Inputs);
        RenderSources(builder, report.Sources);
        RenderSourceCoverage(builder, report.SourceCoverage);
        RenderEndpoints(builder, report.EndpointAlignment);
        RenderSurfaces(builder, report.DependencySurfaces);
        RenderEdges(builder, report.DependencyEdges);
        RenderShared(builder, report.SharedSurfaces);
        RenderContext(builder, "Optional Path and Reverse Context", report.PathContext, report.ReverseContext);
        RenderDiffImpact(builder, report.PortfolioDiff, report.PortfolioImpact);
        RenderReleaseReview(builder, report.ReleaseReviewContext);
        RenderGaps(builder, report.Gaps);
        builder.AppendLine();
        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations)
        {
            builder.AppendLine($"- {CombinedReportHelpers.Cell(limitation)}");
        }

        return builder.ToString();
    }

    private static void RenderInputs(StringBuilder builder, IReadOnlyList<PortfolioInputRow> inputs)
    {
        builder.AppendLine();
        builder.AppendLine("## Portfolio Inputs");
        builder.AppendLine();
        AppendRows(builder, inputs, "| Label | Kind | Path | Group | Roles |", "| --- | --- | --- | --- | --- |",
            input => $"| {CombinedReportHelpers.Cell(input.Label)} | {CombinedReportHelpers.Cell(input.IndexKind)} | {CombinedReportHelpers.Cell(input.IndexPathHash)} | {CombinedReportHelpers.Cell(input.Group)} | {CombinedReportHelpers.Cell(string.Join(";", input.RoleTags))} |");
    }

    private static void RenderSources(StringBuilder builder, IReadOnlyList<PortfolioSourceRow> sources)
    {
        builder.AppendLine();
        builder.AppendLine("## Source Identity and Coverage");
        builder.AppendLine();
        AppendRows(builder, sources, "| Source | Container | Language | Repo | Commit | Coverage | Scanner |", "| --- | --- | --- | --- | --- | --- | --- |",
            source => $"| {CombinedReportHelpers.Cell(source.Label)} | {CombinedReportHelpers.Cell(source.ContainerLabel)} | {CombinedReportHelpers.Cell(source.Language)} | {CombinedReportHelpers.Cell(source.RepoName)} | {CombinedReportHelpers.Cell(source.CommitSha)} | {CombinedReportHelpers.Cell(source.CoverageStatus)} | {CombinedReportHelpers.Cell(source.ScannerVersion)} |");
    }

    private static void RenderSourceCoverage(StringBuilder builder, PortfolioSection<PortfolioSourceCoverageRow> section)
    {
        builder.AppendLine();
        builder.AppendLine("### Source Coverage Rows");
        builder.AppendLine();
        AppendRows(builder, section.Rows, "| Source | Coverage | Classification | Rule | Warnings |", "| --- | --- | --- | --- | --- |",
            row => $"| {CombinedReportHelpers.Cell(row.SourceLabel)} | {CombinedReportHelpers.Cell(row.CoverageStatus)} | {CombinedReportHelpers.Cell(row.Classification)} | {CombinedReportHelpers.Cell(row.RuleId)} | {CombinedReportHelpers.Cell(string.Join("; ", row.Warnings))} |");
    }

    private static void RenderEndpoints(StringBuilder builder, PortfolioSection<PortfolioEndpointFindingRow> section)
    {
        builder.AppendLine();
        builder.AppendLine("## Cross-Source Endpoint Alignment");
        builder.AppendLine();
        builder.AppendLine($"Status: `{section.Status}`");
        AppendRows(builder, section.Rows, "| Classification | Method | Path | Client | Server | Client evidence | Server evidence | Same source | Evidence |", "| --- | --- | --- | --- | --- | --- | --- | --- | --- |",
            row => $"| {CombinedReportHelpers.Cell(row.Classification)} | {CombinedReportHelpers.Cell(row.HttpMethod)} | {CombinedReportHelpers.Cell(row.NormalizedPathKey)} | {CombinedReportHelpers.Cell(row.ClientSourceLabel)} | {CombinedReportHelpers.Cell(row.ServerSourceLabel)} | {CombinedReportHelpers.Cell(LocationText(row.ClientCommitSha, row.ClientFilePath, row.ClientStartLine, row.ClientEndLine))} | {CombinedReportHelpers.Cell(LocationText(row.ServerCommitSha, row.ServerFilePath, row.ServerStartLine, row.ServerEndLine))} | {row.SameSource} | {CombinedReportHelpers.Cell(row.EvidenceTier)} `{CombinedReportHelpers.Cell(row.RuleId)}` |");
    }

    private static void RenderSurfaces(StringBuilder builder, PortfolioSection<PortfolioSurfaceRow> section)
    {
        builder.AppendLine();
        builder.AppendLine("## Dependency Surfaces");
        builder.AppendLine();
        builder.AppendLine($"Status: `{section.Status}`");
        AppendRows(builder, section.Rows, "| Kind | Source | Name | Evidence | Location | Metadata |", "| --- | --- | --- | --- | --- | --- |",
            row => $"| {CombinedReportHelpers.Cell(row.SurfaceKind)} | {CombinedReportHelpers.Cell(row.SourceLabel)} | {CombinedReportHelpers.Cell(row.DisplayName)} | {CombinedReportHelpers.Cell(row.EvidenceTier)} `{CombinedReportHelpers.Cell(row.RuleId)}` | {CombinedReportHelpers.Cell(row.FilePath)}:{row.StartLine} | {CombinedReportHelpers.Cell(MetadataText(row.Metadata))} |");
    }

    private static void RenderEdges(StringBuilder builder, PortfolioSection<PortfolioEdgeRow> section)
    {
        builder.AppendLine();
        builder.AppendLine("## Dependency Edges");
        builder.AppendLine();
        builder.AppendLine($"Status: `{section.Status}`");
        AppendRows(builder, section.Rows, "| Kind | Source | From | To | Evidence | Commit | Extractor | Location |", "| --- | --- | --- | --- | --- | --- | --- | --- |",
            row => $"| {CombinedReportHelpers.Cell(row.EdgeKind)} | {CombinedReportHelpers.Cell(row.SourceLabel)} | {CombinedReportHelpers.Cell(row.SourceSymbol)} | {CombinedReportHelpers.Cell(row.TargetSymbol)} | {CombinedReportHelpers.Cell(row.EvidenceTier)} `{CombinedReportHelpers.Cell(row.RuleId)}` | {CombinedReportHelpers.Cell(row.CommitSha)} | {CombinedReportHelpers.Cell(row.ExtractorVersion)} | {CombinedReportHelpers.Cell(row.FilePath)}:{row.StartLine} |");
    }

    private static void RenderShared(StringBuilder builder, PortfolioSection<PortfolioSharedSurfaceRow> section)
    {
        builder.AppendLine();
        builder.AppendLine("## Shared Portfolio Surfaces");
        builder.AppendLine();
        builder.AppendLine($"Status: `{section.Status}`");
        AppendRows(builder, section.Rows, "| Kind | Name | Classification | Sources | Evidence |", "| --- | --- | --- | --- | --- |",
            row => $"| {CombinedReportHelpers.Cell(row.SurfaceKind)} | {CombinedReportHelpers.Cell(row.DisplayName)} | {CombinedReportHelpers.Cell(row.Classification)} | {CombinedReportHelpers.Cell(string.Join(";", row.SourceLabels))} | {CombinedReportHelpers.Cell(row.EvidenceTier)} `{CombinedReportHelpers.Cell(row.RuleId)}` |");
    }

    private static void RenderContext(StringBuilder builder, string title, PortfolioSection<PortfolioContextRow> paths, PortfolioSection<PortfolioContextRow> reverse)
    {
        builder.AppendLine();
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        builder.AppendLine($"Path context: `{paths.Status}`");
        builder.AppendLine($"Reverse context: `{reverse.Status}`");
    }

    private static void RenderDiffImpact(StringBuilder builder, PortfolioSection<PortfolioDiffRow> diff, PortfolioSection<PortfolioContextRow> impact)
    {
        builder.AppendLine();
        builder.AppendLine("## Portfolio Diff and Impact");
        builder.AppendLine();
        builder.AppendLine($"Diff status: `{diff.Status}`");
        AppendRows(builder, diff.Rows, "| Change | Source | Classification | Evidence |", "| --- | --- | --- | --- |",
            row => $"| {CombinedReportHelpers.Cell(row.ChangeKind)} | {CombinedReportHelpers.Cell(row.SourceLabel)} | {CombinedReportHelpers.Cell(row.Classification)} | {CombinedReportHelpers.Cell(row.EvidenceTier)} `{CombinedReportHelpers.Cell(row.RuleId)}` |");
        builder.AppendLine($"Impact status: `{impact.Status}`");
    }

    private static void RenderReleaseReview(StringBuilder builder, PortfolioSection<PortfolioContextRow> section)
    {
        builder.AppendLine();
        builder.AppendLine("## Release Review Context");
        builder.AppendLine();
        builder.AppendLine($"Status: `{section.Status}`");
    }

    private static void RenderGaps(StringBuilder builder, IReadOnlyList<PortfolioGap> gaps)
    {
        builder.AppendLine();
        builder.AppendLine("## Gaps");
        builder.AppendLine();
        AppendRows(builder, gaps, "| Kind | Section | Classification | Rule | Message |", "| --- | --- | --- | --- | --- |",
            gap => $"| {CombinedReportHelpers.Cell(gap.GapKind)} | {CombinedReportHelpers.Cell(gap.Section)} | {CombinedReportHelpers.Cell(gap.Classification)} | `{CombinedReportHelpers.Cell(gap.RuleId)}` | {CombinedReportHelpers.Cell(gap.Message)} |");
    }

    private static void AppendRows<T>(StringBuilder builder, IReadOnlyList<T> rows, string header, string separator, Func<T, string> render)
    {
        if (rows.Count == 0)
        {
            builder.AppendLine("No evidence found under requested scope.");
            return;
        }

        builder.AppendLine(header);
        builder.AppendLine(separator);
        foreach (var row in rows)
        {
            builder.AppendLine(render(row));
        }
    }

    private static void ValidateOptions(PortfolioReportOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("portfolio requires --out <path>.");
        }

        var hasDirect = options.Inputs.Count > 0;
        var hasManifest = !string.IsNullOrWhiteSpace(options.ManifestPath);
        var hasBeforeAfter = !string.IsNullOrWhiteSpace(options.BeforeManifestPath) || !string.IsNullOrWhiteSpace(options.AfterManifestPath);
        if (hasBeforeAfter)
        {
            if (hasDirect || hasManifest)
            {
                throw new ArgumentException("portfolio before/after manifests cannot be mixed with direct inputs.");
            }

            if (string.IsNullOrWhiteSpace(options.BeforeManifestPath) || string.IsNullOrWhiteSpace(options.AfterManifestPath))
            {
                throw new ArgumentException("portfolio requires paired --before-manifest and --after-manifest.");
            }
        }
        else if (hasDirect == hasManifest)
        {
            throw new ArgumentException("portfolio requires either direct --index/--label inputs or --manifest.");
        }

        if (hasDirect)
        {
            RejectDuplicateLabels(options.Inputs);
            if (options.Inputs.Any(input => string.IsNullOrWhiteSpace(input.IndexPath) || string.IsNullOrWhiteSpace(input.Label)))
            {
                throw new ArgumentException("portfolio direct inputs require paired --index and --label values.");
            }
        }

        var caps = new[] { options.MaxSources, options.MaxSurfaceRows, options.MaxEndpointFindings, options.MaxSharedSurfaces, options.MaxEdgeRows, options.MaxDiffRows, options.MaxImpactItems, options.MaxPaths, options.MaxRoots, options.MaxDepth, options.MaxFrontier, options.MaxGaps };
        if (caps.Any(value => value <= 0))
        {
            throw new ArgumentException("portfolio caps must be positive integers.");
        }
    }

    private static void EnsureExtensionlessOutputIsNotFile(string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);
        if (File.Exists(fullPath) && string.IsNullOrWhiteSpace(Path.GetExtension(fullPath)))
        {
            throw new IOException("portfolio extensionless output path already exists as a file.");
        }
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type in ('table','view') and name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SortedDictionary<string, string>>(json, JsonOptions) ?? new SortedDictionary<string, string>(StringComparer.Ordinal);
        }
        catch
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static ScanManifest ParseManifestJson(string json)
    {
        return JsonSerializer.Deserialize<ScanManifest>(json, JsonOptions)
            ?? new ScanManifest("unknown", "unknown", null, null, "unknown", "unknown", DateTimeOffset.UnixEpoch, "unknown", "unknown", [], [], [], []);
    }

    private static string SelectRollup(IReadOnlyList<PortfolioGap> gaps, bool hasEvidence, bool truncated)
    {
        return SelectRollup(gaps, hasEvidence ? [PortfolioReportClassifications.ActionableStaticEvidence] : [], truncated);
    }

    private static string SelectRollup(IReadOnlyList<PortfolioGap> gaps, IReadOnlyList<string> rowClassifications, bool truncated)
    {
        if (gaps.Any(gap => gap.Classification == PortfolioReportClassifications.UnknownAnalysisGap))
        {
            return PortfolioReportClassifications.UnknownAnalysisGap;
        }

        if (truncated || gaps.Any(gap => gap.Classification == PortfolioReportClassifications.TruncatedByLimit))
        {
            return PortfolioReportClassifications.TruncatedByLimit;
        }

        if (rowClassifications.Contains(PortfolioReportClassifications.ActionableStaticEvidence, StringComparer.Ordinal))
        {
            return PortfolioReportClassifications.ActionableStaticEvidence;
        }

        if (rowClassifications.Contains(PortfolioReportClassifications.ReviewRecommended, StringComparer.Ordinal)
            || gaps.Any(gap => gap.Classification == PortfolioReportClassifications.ReviewRecommended))
        {
            return PortfolioReportClassifications.ReviewRecommended;
        }

        if (rowClassifications.Contains(PortfolioReportClassifications.PartialAnalysis, StringComparer.Ordinal)
            || gaps.Any(gap => gap.Classification == PortfolioReportClassifications.PartialAnalysis))
        {
            return PortfolioReportClassifications.PartialAnalysis;
        }

        if (gaps.Any(gap => gap.Classification == PortfolioReportClassifications.SelectorNoMatch))
        {
            return PortfolioReportClassifications.SelectorNoMatch;
        }

        return PortfolioReportClassifications.NoActionableEvidence;
    }

    private static string CoverageStatus(string analysisLevel, string buildStatus, string commitSha, IReadOnlyList<string> knownGaps)
    {
        if (string.IsNullOrWhiteSpace(commitSha) || commitSha == "unknown")
        {
            return "UnknownAnalysisGap";
        }

        return analysisLevel.Contains("Reduced", StringComparison.OrdinalIgnoreCase)
            || buildStatus.Contains("Failed", StringComparison.OrdinalIgnoreCase)
            || buildStatus.Contains("Partial", StringComparison.OrdinalIgnoreCase)
            || knownGaps.Count > 0
            ? "ReducedCoverage"
            : "FullEvidenceAvailable";
    }

    private static string CorrectLanguage(string scannerVersion, string? stored)
    {
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        var lower = scannerVersion.ToLowerInvariant();
        if (lower.Contains("typescript", StringComparison.Ordinal)) return "typescript";
        if (lower.Contains("python", StringComparison.Ordinal)) return "python";
        if (lower.Contains("jvm", StringComparison.Ordinal)) return "jvm";
        return "csharp";
    }

    private static string EdgeKind(CombinedFactRow fact)
    {
        return fact.FactType switch
        {
            FactTypes.CallEdge => "call",
            FactTypes.ObjectCreated => "object-creation",
            FactTypes.ArgumentPassed => "argument-flow",
            FactTypes.SymbolRelationship => Property(fact.Properties, "relationshipKind") ?? "symbol-relationship",
            _ => fact.FactType
        };
    }

    private static string? Property(IReadOnlyDictionary<string, string> props, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (props.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? Metadata(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    private static IReadOnlyList<string> GapCategories(IReadOnlyList<string> knownGaps)
    {
        return knownGaps
            .Select(gap => gap.Split(':', 2)[0])
            .Where(gap => !string.IsNullOrWhiteSpace(gap))
            .Select(SafeToken)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static PortfolioGap Gap(string kind, string section, string ruleId, string classification, string message, string? sourceLabel = null)
    {
        var safeSource = SafeOptional(sourceLabel);
        return new PortfolioGap(
            $"gap:{CombinedReportHelpers.Hash($"{kind}:{section}:{classification}:{message}:{safeSource}", 20)}",
            kind,
            section,
            classification,
            ruleId,
            classification == PortfolioReportClassifications.NoActionableEvidence ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier4Unknown,
            SafeMessage(message),
            safeSource,
            []);
    }

    private static string RepoHash(string? remoteUrl, string repoName, string? gitRootHash)
    {
        return gitRootHash is { Length: > 0 }
            ? $"git-root:{CombinedReportHelpers.Hash(gitRootHash, 16)}"
            : $"repo:{CombinedReportHelpers.Hash(remoteUrl ?? repoName, 16)}";
    }

    private static string SafeRepoName(string repo)
    {
        return repo.Contains("://", StringComparison.Ordinal) || Path.IsPathFullyQualified(repo)
            ? $"repo-hash:{CombinedReportHelpers.Hash(repo, 16)}"
            : SafeToken(repo);
    }

    private static string SafeCommit(string? commit)
    {
        return string.IsNullOrWhiteSpace(commit) ? "unknown" : SafeToken(commit);
    }

    private static string SafeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : SafeToken(value);
    }

    private static string SafeToken(string value)
    {
        var trimmed = value.Trim();
        if (Path.IsPathFullyQualified(trimmed) || trimmed.Contains("://", StringComparison.Ordinal) || trimmed.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            return $"value-hash:{CombinedReportHelpers.Hash(trimmed, 16)}";
        }

        return trimmed
            .ReplaceLineEndings(" ")
            .Replace("|", " ", StringComparison.Ordinal)
            .Replace("<", "", StringComparison.Ordinal)
            .Replace(">", "", StringComparison.Ordinal)
            .Replace("[", "", StringComparison.Ordinal)
            .Replace("]", "", StringComparison.Ordinal)
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal)
            .Replace("`", "'", StringComparison.Ordinal);
    }

    private static string SafeMessage(string value)
    {
        return SafeToken(value);
    }

    private static string MetadataText(IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        return string.Join("; ", metadata.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string LocationText(string? commitSha, string? filePath, int? startLine, int? endLine)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.IsNullOrWhiteSpace(commitSha) ? string.Empty : commitSha;
        }

        var line = startLine is null
            ? string.Empty
            : endLine is not null && endLine != startLine
                ? $":{startLine}-{endLine}"
                : $":{startLine}";
        return string.IsNullOrWhiteSpace(commitSha) ? $"{filePath}{line}" : $"{commitSha} {filePath}{line}";
    }

    private static string InputId(string label, string side)
    {
        return $"input:{side}:{CombinedReportHelpers.Hash(label, 16)}";
    }

    private static string SourceId(string inputLabel, string side, string sourceLabel)
    {
        return $"source:{side}:{CombinedReportHelpers.Hash($"{inputLabel}:{sourceLabel}", 16)}";
    }

    private static string FactId(string inputLabel, string side, string factId)
    {
        return $"fact:{side}:{CombinedReportHelpers.Hash($"{inputLabel}:{factId}", 20)}";
    }

    private static int GapRank(string classification)
    {
        return classification switch
        {
            PortfolioReportClassifications.UnknownAnalysisGap => 0,
            PortfolioReportClassifications.TruncatedByLimit => 1,
            PortfolioReportClassifications.PartialAnalysis => 2,
            PortfolioReportClassifications.SelectorNoMatch => 3,
            _ => 4
        };
    }

    private static int DiffClassificationRank(string classification)
    {
        return classification switch
        {
            PortfolioReportClassifications.UnknownAnalysisGap => 0,
            PortfolioReportClassifications.TruncatedByLimit => 1,
            PortfolioReportClassifications.ActionableStaticEvidence => 2,
            PortfolioReportClassifications.ReviewRecommended => 3,
            PortfolioReportClassifications.PartialAnalysis => 4,
            PortfolioReportClassifications.SelectorNoMatch => 5,
            _ => 6
        };
    }

    private static string RequiredString(JsonElement element, string property, string message)
    {
        if (element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new InvalidDataException(message);
    }

    private static string? OptionalString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static IReadOnlyList<string> OptionalStrings(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }

    private static void RejectDuplicateLabels(IReadOnlyList<PortfolioInputSpec> inputs)
    {
        var duplicate = inputs
            .GroupBy(input => input.Label, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"portfolio duplicate input label `{SafeToken(duplicate.Key)}`.");
        }
    }

    private sealed record PortfolioManifestInfo(string? PortfolioId, string? SnapshotId, string InputMode, IReadOnlyList<PortfolioInputSpec> Inputs);
    private sealed record PortfolioReadResult(IReadOnlyList<PortfolioInputRow> Inputs, IReadOnlyList<PortfolioSourceRow> Sources, IReadOnlyList<CombinedFactRow> Facts, IReadOnlyList<CombinedDependencyEdgeRow> Edges, IReadOnlyList<PortfolioGap> Gaps);
    private sealed record SingleIndexReadResult(PortfolioSourceRow Source, IReadOnlyList<CombinedFactRow> Facts, IReadOnlyList<CombinedDependencyEdgeRow> Edges, IReadOnlyList<PortfolioGap> Gaps);
    private sealed record Capped<T>(IReadOnlyList<T> Rows, int OmittedCount);
    private sealed record PortfolioComparableDiffRecord(
        string EvidenceKind,
        string SourceKey,
        string SourceLabel,
        string StableKey,
        string MetadataHash,
        string EvidenceHash,
        string DisplayName,
        string RuleId,
        string EvidenceTier,
        string CoverageStatus,
        IReadOnlyList<string> SupportingFactIds,
        IReadOnlyList<string> SupportingEdgeIds,
        string FilePath,
        int StartLine,
        int EndLine,
        IReadOnlyList<KeyValuePair<string, string>> Metadata,
        bool NeedsReview);
}
