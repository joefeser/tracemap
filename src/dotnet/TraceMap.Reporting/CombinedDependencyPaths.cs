using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record CombinedDependencyPathOptions(
    string IndexPath,
    string OutputPath,
    string Format = "markdown",
    string? FromEndpoint = null,
    string? FromSymbol = null,
    string? FromSource = null,
    string? FromWebFormsEvent = null,
    string? ToSurface = null,
    string? SurfaceName = null,
    string? SourcePair = null,
    string? Classification = null,
    string? View = null,
    bool IncludeLegacyRoots = false,
    int MaxDepth = 8,
    int MaxPaths = 100,
    int MaxFrontier = 10000);

public sealed record CombinedDependencyPathResult(
    CombinedDependencyPathReport Report,
    string? MarkdownPath,
    string? JsonPath);

public sealed record CombinedDependencyPathReport(
    string Version,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SchemaVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? View,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    CombinedPathQuery Query,
    IReadOnlyList<CombinedReportSource> Sources,
    CombinedPathSummary Summary,
    IReadOnlyList<CombinedPath> Paths,
    IReadOnlyList<CombinedPathGap> Gaps,
    CombinedPathInventory Inventory,
    IReadOnlyList<string> Limitations);

public sealed record CombinedPathQuery(
    string? FromEndpoint,
    string? FromSymbol,
    string? FromSource,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FromWebFormsEvent,
    string? ToSurface,
    string? SurfaceName,
    string? SourcePair,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Classification,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool IncludeLegacyRoots,
    int MaxDepth,
    int MaxPaths,
    int MaxFrontier,
    string Algorithm,
    string AlgorithmVersion);

public sealed record CombinedPathSummary(
    int SourceCount,
    int GraphNodeCount,
    int GraphEdgeCount,
    int PathCount,
    int GapCount,
    int SelectorCandidateCount,
    bool Truncated);

public sealed record CombinedPath(
    string PathId,
    string Classification,
    string Confidence,
    int Length,
    string StartNodeId,
    string EndNodeId,
    IReadOnlyList<CombinedPathNode> Nodes,
    IReadOnlyList<CombinedPathEdge> Edges,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<CombinedPathNote> Notes);

public sealed record CombinedPathNode(
    string NodeId,
    string NodeKind,
    string DisplayName,
    string SourceIndexId,
    string SourceLabel,
    string? ScanId,
    string? CommitSha,
    string? SymbolId,
    string? CombinedFactId,
    string? RuleId,
    string? EvidenceTier,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? SurfaceKind,
    string? SurfaceName,
    string? HttpMethod,
    string? NormalizedPathKey,
    string? OperationName,
    string? TableName,
    string? ColumnNames,
    string? SourceKind,
    string? ShapeHash,
    string? TextHash,
    string? TextLength,
    string? PackageName,
    string? ConfigKey);

public sealed record CombinedPathEdge(
    string EdgeId,
    string EdgeKind,
    string FromNodeId,
    string ToNodeId,
    string Classification,
    string RuleId,
    string EvidenceTier,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingCombinedEdgeIds,
    string? FilePath,
    int? StartLine,
    int? EndLine);

public sealed record CombinedPathNote(string Code, string Message);

public sealed record CombinedPathGap(
    string GapId,
    string GapKind,
    string Classification,
    string Message,
    string? SourceIndexId,
    string? SourceLabel,
    string? NodeId,
    string? CombinedFactId,
    string? RuleId,
    string? EvidenceTier,
    string? FilePath,
    int? StartLine,
    string? Reason,
    string? CommitSha = null,
    string? ExtractorVersion = null,
    string? EvidenceScope = null);

public sealed record CombinedPathInventory(
    IReadOnlyDictionary<string, int> NodesByKind,
    IReadOnlyDictionary<string, int> EdgesByKind,
    IReadOnlyDictionary<string, int> NodesBySource,
    IReadOnlyDictionary<string, int> SurfacesByKind,
    IReadOnlyDictionary<string, int> GapsByKind,
    IReadOnlyList<CombinedPathNode> EvidenceNodes,
    IReadOnlyList<CombinedPathEdge> EvidenceEdges);

internal sealed record CombinedPathGraphInventory(
    IReadOnlyList<CombinedReportSource> Sources,
    IReadOnlyList<string> CoverageWarnings,
    IReadOnlyList<CombinedPathNode> Nodes,
    IReadOnlyList<CombinedPathEdge> Edges,
    IReadOnlyList<CombinedPathGap> Gaps);

public static class CombinedDependencyPathClassifications
{
    public const string StrongStaticPath = nameof(StrongStaticPath);
    public const string ProbableStaticPath = nameof(ProbableStaticPath);
    public const string NeedsReviewPath = nameof(NeedsReviewPath);
    public const string NeedsReviewStaticPath = nameof(NeedsReviewStaticPath);
    public const string ReducedCoverage = nameof(ReducedCoverage);
    public const string AnalysisGap = nameof(AnalysisGap);
    public const string NoBackendEvidence = nameof(NoBackendEvidence);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string NoPathFound = nameof(NoPathFound);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
    public const string ClassificationFilterNoMatch = nameof(ClassificationFilterNoMatch);
}

public static class LegacyFlowReportConstants
{
    public const string SchemaVersion = "legacy-flow.v1";
    public const string View = "legacy-flows";
}

public static class CombinedValueOriginClassifications
{
    public const string StrongStaticValuePath = nameof(StrongStaticValuePath);
    public const string ProbableStaticValuePath = nameof(ProbableStaticValuePath);
    public const string NeedsReviewValuePath = nameof(NeedsReviewValuePath);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string NoValuePathEvidence = nameof(NoValuePathEvidence);
}

public static class CombinedDependencyPathReporter
{
    private const string Version = "1.0";
    private const string Algorithm = "bounded-bfs";
    private const string AlgorithmVersion = "1.0";
    private const int MarkdownPathLimit = 100;
    private const int MarkdownInventoryLimit = 200;
    private const string EndpointMatchRuleId = "combined.paths.endpoint-match.v1";
    private const string FactAttachedRuleId = "combined.paths.fact-attached-to-symbol.v1";
    private const string SurfaceEvidenceRuleId = "combined.paths.surface-evidence.v1";
    private const string QueryGapRuleId = "combined.paths.query-gap.v1";
    private const string TruncationGapRuleId = "combined.paths.truncation-gap.v1";
    private const string SymbolReconciliationRuleId = "combined.paths.symbol-reconciliation.v1";
    private const int SelectorCandidateLimit = 250;

    private static readonly HashSet<string> TerminalSurfaceKinds = new(StringComparer.Ordinal)
    {
        "sql-query",
        "sql-persistence",
        "http-route",
        "http-client",
        "package-config",
        "wcf-operation",
        "remoting-endpoint",
        "remoting-registration",
        "remoting-channel",
        "remoting-object",
        "remoting-api",
        "legacy-data",
        "dependency-surface",
        "message-queue",
        "message-topic",
        "message-subscription",
        "message-exchange",
        "message-stream",
        "message-event",
        "message-channel",
        "message-unknown"
    };

    private static readonly HashSet<string> EdgeKindTerms = new(StringComparer.Ordinal)
    {
        "calls",
        "creates",
        "inherits",
        "implements",
        "overrides",
        "argument-passed",
        "parameter-forward",
        "fact-attached-to-symbol",
        "surface-evidence",
        "symbol-reconciliation",
        "message-publish-consume"
    };

    private static readonly HashSet<string> LegacyTerminalSurfaceKinds = new(StringComparer.Ordinal)
    {
        "sql-query",
        "sql-persistence",
        "http-client",
        "wcf-operation",
        "remoting-endpoint",
        "remoting-registration",
        "remoting-channel",
        "remoting-object",
        "remoting-api",
        "legacy-data",
        "dependency-surface",
        "package-config"
    };

    public static async Task<CombinedDependencyPathResult> WriteAsync(CombinedDependencyPathOptions options, CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(options, cancellationToken);
        var format = NormalizeFormat(options.Format);
        var (markdownPath, jsonPath) = await WriteOutputsAsync(options.OutputPath, format, report, cancellationToken);
        return new CombinedDependencyPathResult(report, markdownPath, jsonPath);
    }

    public static async Task<CombinedDependencyPathReport> BuildReportAsync(CombinedDependencyPathOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var sourcePair = ParseSourcePair(options.SourcePair);
        var (read, graph) = await BuildGraphAsync(options.IndexPath, sourcePair, options.IncludeLegacyRoots || IsLegacyView(options.View), allowSingleIndex: true, cancellationToken);
        return BuildReport(options, read, graph, sourcePair);
    }

    internal static async Task<CombinedPathGraphInventory> BuildGraphInventoryAsync(
        string indexPath,
        string? sourcePair = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(indexPath))
        {
            throw new ArgumentException("paths requires --index <index.sqlite|combined.sqlite>.");
        }

        var parsedSourcePair = ParseSourcePair(sourcePair);
        var (read, graph) = await BuildGraphAsync(indexPath, parsedSourcePair, includeLegacyRoots: false, allowSingleIndex: false, cancellationToken);
        return new CombinedPathGraphInventory(
            read.Sources.OrderBy(source => source.Label, StringComparer.Ordinal).ThenBy(source => source.SourceIndexId, StringComparer.Ordinal).ToArray(),
            read.CoverageWarnings.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            graph.Nodes.Values
                .Select(node => node.ToReportNode())
                .OrderBy(node => node.SourceLabel, StringComparer.Ordinal)
                .ThenBy(node => node.NodeKind, StringComparer.Ordinal)
                .ThenBy(node => node.DisplayName, StringComparer.Ordinal)
                .ThenBy(node => node.NodeId, StringComparer.Ordinal)
                .ToArray(),
            graph.Edges
                .Select(edge => edge.ToReportEdge())
                .ToArray(),
            graph.Gaps
                .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
                .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
                .ThenBy(gap => gap.FilePath, StringComparer.Ordinal)
                .ThenBy(gap => gap.StartLine ?? 0)
                .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
                .ToArray());
    }

    private static async Task<(CombinedReadResult Read, EvidenceGraph Graph)> BuildGraphAsync(
        string indexPath,
        (string Client, string Server)? sourcePair,
        bool includeLegacyRoots,
        bool allowSingleIndex,
        CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = indexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var read = await ReadPathIndexAsync(connection, indexPath, allowSingleIndex, cancellationToken);
        var endpointFindings = CombinedDependencyReporter.MatchEndpoints(read.Sources, read.Facts);
        var surfaces = CombinedDependencyReporter.BuildSurfaces(read.Facts, read.Sources);
        var graph = BuildGraph(read, endpointFindings, surfaces, sourcePair, includeLegacyRoots);
        return (read, graph);
    }

    private static void ValidateOptions(CombinedDependencyPathOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("paths requires --index <index.sqlite|combined.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("paths requires --out <path>.");
        }

        if (options.MaxDepth <= 0)
        {
            throw new ArgumentException("--max-depth must be a positive integer.");
        }

        if (options.MaxPaths <= 0)
        {
            throw new ArgumentException("--max-paths must be a positive integer.");
        }

        if (options.MaxFrontier <= 0)
        {
            throw new ArgumentException("--max-frontier must be a positive integer.");
        }

        if (!string.IsNullOrWhiteSpace(options.ToSurface))
        {
            var surfaceKind = options.ToSurface.Trim();
            if (EdgeKindTerms.Contains(surfaceKind))
            {
                throw new ArgumentException($"paths --to-surface '{surfaceKind}' is an edge kind, not a terminal surface.");
            }

            if (!TerminalSurfaceKinds.Contains(surfaceKind))
            {
                throw new ArgumentException("paths --to-surface must be one of sql-query, sql-persistence, http-route, http-client, package-config, wcf-operation, remoting-endpoint, remoting-registration, remoting-channel, remoting-object, remoting-api, legacy-data, dependency-surface, message-queue, message-topic, message-subscription, message-exchange, message-stream, message-event, message-channel, or message-unknown.");
            }
        }

        if (!string.IsNullOrWhiteSpace(options.Classification)
            && ClassificationRank(NormalizeClassification(options.Classification.Trim(), options.IncludeLegacyRoots || IsLegacyView(options.View))) == 99)
        {
            throw new ArgumentException("paths --classification must be one of StrongStaticPath, ProbableStaticPath, NeedsReviewStaticPath, NoBackendEvidence, ReducedCoverage, or AnalysisGap.");
        }
    }

    private static CombinedDependencyPathReport BuildReport(
        CombinedDependencyPathOptions options,
        CombinedReadResult read,
        EvidenceGraph graph,
        (string Client, string Server)? sourcePair)
    {
        var legacyMode = options.IncludeLegacyRoots || IsLegacyView(options.View);
        var sourceFilter = string.IsNullOrWhiteSpace(options.FromSource) ? null : options.FromSource.Trim();
        var resolvedStarts = ResolveStartNodes(options, graph, sourceFilter);
        var startNodes = resolvedStarts.Nodes;
        var terminalNodes = ResolveTerminalNodes(options, graph, startNodes);
        var gaps = new List<CombinedPathGap>(graph.Gaps);
        var selectorCandidateCount = resolvedStarts.TotalMatchCount;
        var paths = new List<CombinedPath>();
        var truncated = false;

        if (resolvedStarts.TotalMatchCount > startNodes.Count)
        {
            gaps.Add(new CombinedPathGap(
                $"gap:truncated:selector-candidates:{resolvedStarts.TotalMatchCount}",
                "TruncatedByLimit",
                CombinedDependencyPathClassifications.NeedsReviewPath,
                $"Selector matched {resolvedStarts.TotalMatchCount} starting nodes; traversal used the first {startNodes.Count} deterministic candidates.",
                null,
                sourceFilter,
                null,
                null,
                TruncationGapRuleId,
                EvidenceTiers.Tier4Unknown,
                null,
                null,
                "selector-candidates"));
            truncated = true;
        }

        if (startNodes.Count == 0)
        {
            gaps.Add(CreateSelectorGap(read, options, sourceFilter));
        }
        else if (terminalNodes.Count == 0)
        {
            gaps.Add(new CombinedPathGap(
                "gap:selector:no-terminal-surface",
                "SelectorNoMatch",
                CombinedDependencyPathClassifications.SelectorNoMatch,
                "No terminal dependency surfaces matched the query.",
                null,
                null,
                null,
                null,
                QueryGapRuleId,
                EvidenceTiers.Tier4Unknown,
                null,
                null,
                "selector"));
        }
        else
        {
            var messageTerminalNode = terminalNodes
                .Select(id => graph.Nodes.TryGetValue(id, out var node) ? node : null)
                .OfType<GraphNode>()
                .FirstOrDefault(node => IsMessageSurfaceKind(node.SurfaceKind));
            if (messageTerminalNode is not null)
            {
                gaps.Add(new CombinedPathGap(
                    $"gap:message-direction-filter:{messageTerminalNode.NodeId}",
                    "DirectionFilterNotSupported",
                    CombinedDependencyPathClassifications.AnalysisGap,
                    "Message surface direction filtering is not supported in this path-query slice; publisher, consumer, and binding evidence may be selected together.",
                    messageTerminalNode.SourceIndexId,
                    messageTerminalNode.SourceLabel,
                    messageTerminalNode.NodeId,
                    messageTerminalNode.CombinedFactId,
                    RuleIds.MessageSurfaceGap,
                    EvidenceTiers.Tier4Unknown,
                    messageTerminalNode.FilePath,
                    messageTerminalNode.StartLine,
                    "direction-filter-not-supported"));
            }

            var search = Search(graph, startNodes, terminalNodes, options.MaxDepth, options.MaxPaths, options.MaxFrontier);
            paths.AddRange(search.Paths);
            gaps.AddRange(search.Gaps);
            truncated = truncated || search.Truncated;

            if (paths.Count == 0)
            {
                var gap = CreateNoPathGap(read, graph, startNodes, options.MaxDepth, options.MaxFrontier, legacyMode);
                if (legacyMode && gap.Classification == CombinedDependencyPathClassifications.NoBackendEvidence)
                {
                    paths.AddRange(startNodes
                        .Take(options.MaxPaths)
                        .Select((node, index) => ToNoBackendEvidencePath($"path:no-backend:{index + 1:0000}", node)));
                    if (startNodes.Count > options.MaxPaths)
                    {
                        truncated = true;
                        gaps.Add(TruncatedGap("path", startNodes[options.MaxPaths].NodeId, graph));
                    }
                }
                else
                {
                    gaps.Add(gap);
                }
            }
        }

        var reportPaths = legacyMode
            ? paths.Select(path => ToLegacyPath(path, graph)).ToList()
            : paths;

        if (!string.IsNullOrWhiteSpace(options.Classification))
        {
            var requested = NormalizeClassification(options.Classification.Trim(), legacyMode);
            var beforeFilterCount = reportPaths.Count;
            reportPaths = reportPaths.Where(path => string.Equals(NormalizeClassification(path.Classification, legacyMode), requested, StringComparison.Ordinal)).ToList();
            if (beforeFilterCount > 0 && reportPaths.Count == 0)
            {
                gaps.Add(new CombinedPathGap(
                    $"gap:selector:classification:{Hash(requested, 16)}",
                    "ClassificationFilterNoMatch",
                    CombinedDependencyPathClassifications.ClassificationFilterNoMatch,
                    "Static paths existed before classification filtering, but none matched the requested classification.",
                    null,
                    sourceFilter,
                    null,
                    null,
                    RuleIds.LegacyFlowClassification,
                    EvidenceTiers.Tier4Unknown,
                    null,
                    null,
                    "classification"));
            }
        }

        var sortedPaths = reportPaths
            .OrderBy(path => ClassificationRank(path.Classification))
            .ThenBy(path => path.Length)
            .ThenBy(path => path.Nodes.FirstOrDefault()?.SourceLabel, StringComparer.Ordinal)
            .ThenBy(path => path.Nodes.LastOrDefault()?.SourceLabel, StringComparer.Ordinal)
            .ThenBy(path => string.Join("|", path.Nodes.Select(node => node.DisplayName)), StringComparer.Ordinal)
            .ThenBy(path => string.Join("|", path.Nodes.Select(node => node.FilePath)), StringComparer.Ordinal)
            .ThenBy(path => path.Nodes.FirstOrDefault()?.StartLine ?? 0)
            .ThenBy(path => path.PathId, StringComparer.Ordinal)
            .ToArray();
        var sortedGaps = gaps
            .Select(gap => legacyMode ? SanitizeGap(gap) : gap)
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
            .ThenBy(gap => gap.FilePath, StringComparer.Ordinal)
            .ThenBy(gap => gap.StartLine ?? 0)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        var warnings = read.CoverageWarnings
            .Select(value => legacyMode ? SafeDisplay(value) ?? "redacted" : value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var participatingNodes = sortedPaths.SelectMany(path => path.Nodes)
            .Concat(sortedGaps.Select(gap => gap.NodeId is not null && graph.Nodes.TryGetValue(gap.NodeId, out var node) ? node.ToReportNode() : null).OfType<CombinedPathNode>())
            .Select(node => legacyMode ? SanitizeNode(node) : node)
            .GroupBy(node => node.NodeId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(node => node.SourceLabel, StringComparer.Ordinal)
            .ThenBy(node => node.NodeKind, StringComparer.Ordinal)
            .ThenBy(node => node.DisplayName, StringComparer.Ordinal)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        var participatingEdges = sortedPaths.SelectMany(path => path.Edges)
            .GroupBy(edge => edge.EdgeId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(edge => edge.EdgeKind, StringComparer.Ordinal)
            .ThenBy(edge => edge.FilePath, StringComparer.Ordinal)
            .ThenBy(edge => edge.StartLine ?? 0)
            .ThenBy(edge => edge.EdgeId, StringComparer.Ordinal)
            .ToArray();

        return new CombinedDependencyPathReport(
            Version,
            legacyMode ? LegacyFlowReportConstants.SchemaVersion : null,
            legacyMode ? LegacyFlowReportConstants.View : null,
            warnings.Length == 0 && !sortedGaps.Any(gap => gap.Classification is CombinedDependencyPathClassifications.AnalysisGap or CombinedDependencyPathClassifications.ReducedCoverage) ? "FullEvidenceAvailable" : "ReducedCoverage",
            warnings,
            new CombinedPathQuery(
                legacyMode ? SafeDisplay(options.FromEndpoint) : options.FromEndpoint,
                legacyMode ? SafeDisplay(options.FromSymbol) : options.FromSymbol,
                legacyMode && sourceFilter is not null ? SafeSourceLabel(sourceFilter) : sourceFilter,
                legacyMode ? SafeDisplay(options.FromWebFormsEvent) : options.FromWebFormsEvent,
                options.ToSurface,
                legacyMode ? SafeDisplay(options.SurfaceName) : options.SurfaceName,
                sourcePair is null ? null : $"{EscapeSourcePairLabel(sourcePair.Value.Client)}:{EscapeSourcePairLabel(sourcePair.Value.Server)}",
                options.Classification,
                legacyMode,
                options.MaxDepth,
                options.MaxPaths,
                options.MaxFrontier,
                Algorithm,
                AlgorithmVersion),
            read.Sources.Select(source => legacyMode ? SanitizeSource(source) : source).OrderBy(source => source.Label, StringComparer.Ordinal).ThenBy(source => source.SourceIndexId, StringComparer.Ordinal).ToArray(),
            new CombinedPathSummary(
                read.Sources.Count,
                graph.Nodes.Count,
                graph.Edges.Count,
                sortedPaths.Length,
                sortedGaps.Length,
                selectorCandidateCount,
                truncated),
            sortedPaths,
            sortedGaps,
            new CombinedPathInventory(
                CountBy(participatingNodes, node => node.NodeKind),
                CountBy(participatingEdges, edge => edge.EdgeKind),
                CountBy(participatingNodes, node => node.SourceLabel),
                CountBy(participatingNodes.Where(node => node.SurfaceKind is not null), node => node.SurfaceKind!),
                CountBy(sortedGaps, gap => gap.GapKind),
                participatingNodes,
                participatingEdges),
            ReportLimitations(legacyMode, participatingNodes, sortedGaps));
    }

    private static EvidenceGraph BuildGraph(
        CombinedReadResult read,
        IReadOnlyList<CombinedEndpointFinding> endpointFindings,
        IReadOnlyList<CombinedDependencySurfaceRow> surfaces,
        (string Client, string Server)? sourcePair,
        bool includeLegacyRoots)
    {
        var graph = new EvidenceGraph(read.Sources);
        var factsById = read.Facts.ToDictionary(fact => fact.CombinedFactId, StringComparer.Ordinal);
        foreach (var fact in read.Facts.OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            if (fact.FactType is FactTypes.HttpCallDetected or FactTypes.HttpRouteBinding)
            {
                graph.AddNode(ToEndpointNode(fact));
            }

            if (includeLegacyRoots && fact.FactType is FactTypes.WebFormsHandlerResolved or FactTypes.WinFormsHandlerResolved)
            {
                graph.AddNode(fact.FactType == FactTypes.WinFormsHandlerResolved ? ToWinFormsRootNode(fact) : ToWebFormsRootNode(fact));
                continue;
            }

            if (includeLegacyRoots && fact.FactType == FactTypes.WcfGeneratedClientDeclared)
            {
                graph.AddNode(ToWcfClientNode(fact));
            }

            AddSymbolNodeAndAttachment(graph, fact, fact.SourceSymbol, fact.CombinedFactId);
            if (!IsDependencySurfaceFact(fact))
            {
                AddSymbolNodeAndAttachment(graph, fact, fact.TargetSymbol, fact.CombinedFactId);
            }
        }

        foreach (var edge in read.Edges)
        {
            if (string.IsNullOrWhiteSpace(edge.SourceSymbol) || string.IsNullOrWhiteSpace(edge.TargetSymbol))
            {
                continue;
            }

            var from = graph.GetOrAddSymbolNode(edge.SourceIndexId, edge.SourceLabel, edge.SourceSymbol, edge.FilePath, edge.StartLine, edge.EndLine, edge.RuleId, edge.EvidenceTier);
            var to = graph.GetOrAddSymbolNode(edge.SourceIndexId, edge.SourceLabel, edge.TargetSymbol, edge.FilePath, edge.StartLine, edge.EndLine, edge.RuleId, edge.EvidenceTier);
            graph.AddEdge(new GraphEdge(
                $"edge:{edge.EdgeId}:{NormalizeEdgeKind(edge.EdgeKind)}",
                NormalizeEdgeKind(edge.EdgeKind),
                from.NodeId,
                to.NodeId,
                "EvidenceEdge",
                edge.RuleId,
                edge.EvidenceTier,
                [],
                [edge.EdgeId],
                SafePath(edge.FilePath),
                edge.StartLine,
                edge.EndLine));
        }

        foreach (var surface in surfaces)
        {
            if (!factsById.TryGetValue(surface.CombinedFactId, out var fact))
            {
                continue;
            }

            var surfaceNode = ToSurfaceNode(surface);
            graph.AddNode(surfaceNode);
            var attached = false;
            foreach (var symbol in SurfaceAttachmentSymbols(fact))
            {
                if (IsLegacyDataFact(fact)
                    && !string.Equals(symbol, fact.SourceSymbol?.Trim(), StringComparison.Ordinal)
                    && !graph.Nodes.ContainsKey(SymbolNodeId(fact.SourceIndexId, symbol)))
                {
                    continue;
                }

                var symbolNode = graph.GetOrAddSymbolNode(fact.SourceIndexId, fact.SourceLabel, symbol, fact.FilePath, fact.StartLine, fact.EndLine, fact.RuleId, fact.EvidenceTier);
                graph.AddEdge(new GraphEdge(
                    $"surface:{surface.CombinedFactId}:{symbolNode.NodeId}",
                    "surface-evidence",
                    symbolNode.NodeId,
                    surfaceNode.NodeId,
                    "EvidenceEdge",
                    SurfaceEvidenceRuleId,
                    fact.EvidenceTier,
                    [surface.CombinedFactId],
                    [],
                    SafePath(fact.FilePath),
                    fact.StartLine,
                    fact.EndLine));
                attached = true;
            }

            if (!attached)
            {
                graph.Gaps.Add(new CombinedPathGap(
                    $"gap:unlinked-surface:{surface.CombinedFactId}",
                    "UnlinkedSurface",
                    CombinedDependencyPathClassifications.NeedsReviewPath,
                    $"Surface `{surface.DisplayName}` is discoverable but could not be attached to a symbol with current evidence.",
                    surface.SourceIndexId,
                    surface.SourceLabel,
                    surfaceNode.NodeId,
                    surface.CombinedFactId,
                    surface.RuleId,
                    surface.EvidenceTier,
                    SafePath(surface.FilePath),
                    surface.StartLine,
                    "surface-link"));
            }
        }

        if (includeLegacyRoots)
        {
            AddLegacyFlowNodesAndEdges(graph, read.Facts, surfaces);
            AddLegacyAvailabilityGaps(graph, read);
        }

        foreach (var finding in endpointFindings)
        {
            if (sourcePair is not null
                && (!string.Equals(finding.ClientSourceLabel, sourcePair.Value.Client, StringComparison.Ordinal)
                    || !string.Equals(finding.ServerSourceLabel, sourcePair.Value.Server, StringComparison.Ordinal)))
            {
                continue;
            }

            if (finding.ClientCombinedFactId is null || finding.ServerCombinedFactId is null)
            {
                continue;
            }

            var clientId = FactNodeId(finding.ClientCombinedFactId);
            var serverId = FactNodeId(finding.ServerCombinedFactId);
            if (!graph.Nodes.ContainsKey(clientId) || !graph.Nodes.ContainsKey(serverId))
            {
                continue;
            }

            graph.AddEdge(new GraphEdge(
                $"endpoint-match:{finding.ClientCombinedFactId}:{finding.ServerCombinedFactId}",
                "endpoint-match",
                clientId,
                serverId,
                finding.Classification,
                EndpointMatchRuleId,
                finding.StaticMatchQuality == "High" ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier3SyntaxOrTextual,
                [finding.ClientCombinedFactId, finding.ServerCombinedFactId],
                [],
                SafePath(finding.ServerFilePath ?? finding.ClientFilePath ?? string.Empty),
                finding.ServerStartLine ?? finding.ClientStartLine,
                finding.ServerEndLine ?? finding.ClientEndLine));
        }

        AddSymbolReconciliationEdges(graph);
        graph.Sort();
        return graph;
    }

    private static async Task<CombinedReadResult> ReadPathIndexAsync(SqliteConnection connection, string indexPath, bool allowSingleIndex, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(connection, "index_sources", cancellationToken)
            && await TableExistsAsync(connection, "combined_facts", cancellationToken)
            && await ViewExistsAsync(connection, "combined_dependency_edges", cancellationToken))
        {
            await CombinedDependencyReporter.ValidateCombinedIndexAsync(connection, cancellationToken);
            return await CombinedDependencyReporter.ReadAsync(connection, cancellationToken);
        }

        if (allowSingleIndex
            && await TableExistsAsync(connection, "scan_manifest", cancellationToken)
            && await TableExistsAsync(connection, "facts", cancellationToken))
        {
            return await ReadSingleIndexAsync(connection, indexPath, cancellationToken);
        }

        throw new InvalidDataException(allowSingleIndex
            ? "tracemap paths requires a TraceMap index.sqlite or combined.sqlite file."
            : "tracemap paths graph inventory requires a combined index produced by tracemap combine.");
    }

    private static async Task<CombinedReadResult> ReadSingleIndexAsync(SqliteConnection connection, string indexPath, CancellationToken cancellationToken)
    {
        var (source, manifestJson) = await ReadSingleSourceAsync(connection, indexPath, cancellationToken);
        var warnings = new List<string>();
        AddSingleCoverageWarnings(source, warnings);
        var facts = await ReadSingleFactsAsync(connection, source, cancellationToken);
        var edges = await ReadSingleEdgesAsync(connection, source, cancellationToken);
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        if (await TableExistsAsync(connection, "parameter_forward_edges", cancellationToken))
        {
            counts["parameter-forward-edges"] = await CountRowsAsync(connection, "parameter_forward_edges", cancellationToken);
        }

        counts["async-boundaries"] = facts.Count(fact => fact.FactType == FactTypes.AsyncBoundary);
        counts["callback-boundaries"] = facts.Count(fact => fact.FactType == FactTypes.CallbackBoundary);
        var knownGaps = facts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap)
            .GroupBy(fact => fact.RuleId, StringComparer.Ordinal)
            .Select(group => new CombinedKnownGapRow(source.SourceIndexId, source.Label, group.Key, group.Count(), "Analysis gap evidence is present."))
            .OrderBy(gap => gap.Category, StringComparer.Ordinal)
            .ToArray();
        _ = manifestJson;
        return new CombinedReadResult(
            [source],
            knownGaps,
            warnings.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            facts,
            edges,
            counts.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    private static async Task<(CombinedReportSource Source, string ManifestJson)> ReadSingleSourceAsync(SqliteConnection connection, string indexPath, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select scan_id, repo, commit_sha, scanner_version, analysis_level, build_status, manifest_json
            from scan_manifest
            order by scanned_at desc
            limit 1;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException("TraceMap index does not contain a scan manifest.");
        }

        var scannerVersion = reader.GetString(3);
        var source = new CombinedReportSource(
            "single",
            "single",
            Hash(Path.GetFullPath(indexPath), 32),
            reader.GetString(0),
            "source",
            null,
            null,
            reader.GetString(2),
            scannerVersion,
            InferLanguage(scannerVersion),
            null,
            false,
            ".",
            null,
            null,
            reader.GetString(4),
            reader.GetString(5));
        return (source, reader.GetString(6));
    }

    private static async Task<IReadOnlyList<CombinedFactRow>> ReadSingleFactsAsync(SqliteConnection connection, CombinedReportSource source, CancellationToken cancellationToken)
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
            var factId = reader.GetString(0);
            rows.Add(new CombinedFactRow(
                $"{source.SourceIndexId}:{factId}",
                source.SourceIndexId,
                source.Label,
                factId,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetString(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                ParseProperties(reader.GetString(13))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CombinedDependencyEdgeRow>> ReadSingleEdgesAsync(SqliteConnection connection, CombinedReportSource source, CancellationToken cancellationToken)
    {
        var edges = new List<CombinedDependencyEdgeRow>();
        if (await TableExistsAsync(connection, "call_edges", cancellationToken))
        {
            await ReadSingleEdgeQueryAsync(connection, source, edges, """
                select 'calls', fact_id, fact_id, caller_symbol, callee_symbol, callee_assembly_name, callee_assembly_version, rule_id, evidence_tier, file_path, start_line, end_line
                from call_edges
                order by file_path, start_line, fact_id;
                """, cancellationToken);
        }

        if (await TableExistsAsync(connection, "object_creations", cancellationToken))
        {
            await ReadSingleEdgeQueryAsync(connection, source, edges, """
                select 'creates', fact_id, fact_id, caller_symbol, created_type, created_type_assembly_name, created_type_assembly_version, rule_id, evidence_tier, file_path, start_line, end_line
                from object_creations
                order by file_path, start_line, fact_id;
                """, cancellationToken);
        }

        if (await TableExistsAsync(connection, "symbol_relationships", cancellationToken))
        {
            await ReadSingleEdgeQueryAsync(connection, source, edges, """
                select relationships.relationship_kind,
                       relationships.relationship_id,
                       relationships.relationship_id,
                       coalesce(source_symbols.display_name, relationships.source_symbol_id),
                       coalesce(target_symbols.display_name, relationships.target_symbol_id),
                       source_symbols.assembly_name,
                       source_symbols.assembly_version,
                       relationships.rule_id,
                       relationships.evidence_tier,
                       relationships.file_path,
                       relationships.start_line,
                       relationships.end_line
                from symbol_relationships relationships
                left join symbols source_symbols on source_symbols.scan_id = relationships.scan_id and source_symbols.symbol_id = relationships.source_symbol_id
                left join symbols target_symbols on target_symbols.scan_id = relationships.scan_id and target_symbols.symbol_id = relationships.target_symbol_id
                order by relationships.file_path, relationships.start_line, relationships.relationship_id;
                """, cancellationToken);
        }

        if (await TableExistsAsync(connection, "parameter_forward_edges", cancellationToken))
        {
            await ReadSingleEdgeQueryAsync(connection, source, edges, """
                select 'parameter-forward', fact_id, fact_id,
                       source_method_symbol || ':' || source_parameter_symbol,
                       target_method_symbol || ':' || target_parameter_symbol,
                       target_assembly_name, target_assembly_version, rule_id, evidence_tier, file_path, start_line, end_line
                from parameter_forward_edges
                order by file_path, start_line, fact_id;
                """, cancellationToken);
        }

        return edges
            .OrderBy(edge => edge.EdgeKind, StringComparer.Ordinal)
            .ThenBy(edge => edge.SourceSymbol, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(edge => edge.FilePath, StringComparer.Ordinal)
            .ThenBy(edge => edge.StartLine)
            .ThenBy(edge => edge.EdgeId, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task ReadSingleEdgeQueryAsync(
        SqliteConnection connection,
        CombinedReportSource source,
        List<CombinedDependencyEdgeRow> edges,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var originalId = reader.GetString(1);
            edges.Add(new CombinedDependencyEdgeRow(
                reader.GetString(0),
                source.SourceIndexId,
                source.Label,
                $"{source.SourceIndexId}:{reader.GetString(1)}",
                originalId,
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetInt32(10),
                reader.GetInt32(11)));
        }
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<bool> ViewExistsAsync(SqliteConnection connection, string viewName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'view' and name = $name;";
        command.Parameters.AddWithValue("$name", viewName);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {tableName};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static void AddSingleCoverageWarnings(CombinedReportSource source, List<string> warnings)
    {
        if (SourceHasReducedCoverage(source))
        {
            warnings.Add($"single has reduced coverage ({source.AnalysisLevel}, build {source.BuildStatus}).");
        }
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<SortedDictionary<string, string>>(json, CombinedDependencyReporter.JsonOptions)
                ?? new SortedDictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["propertiesHash"] = Hash(json, 32)
            };
        }
    }

    private static void AddLegacyFlowNodesAndEdges(EvidenceGraph graph, IReadOnlyList<CombinedFactRow> facts, IReadOnlyList<CombinedDependencySurfaceRow> surfaces)
    {
        var factsBySourceOriginalId = facts
            .GroupBy(fact => SourceFactKey(fact.SourceIndexId, fact.OriginalFactId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal).First(), StringComparer.Ordinal);
        var surfacesByKind = surfaces
            .GroupBy(surface => surface.SurfaceKind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        foreach (var handler in facts.Where(fact => fact.FactType is FactTypes.WebFormsHandlerResolved or FactTypes.WinFormsHandlerResolved).OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            var root = handler.FactType == FactTypes.WinFormsHandlerResolved ? ToWinFormsRootNode(handler) : ToWebFormsRootNode(handler);
            graph.AddNode(root);
            var handlerSymbol = HandlerSymbol(handler);
            if (!string.IsNullOrWhiteSpace(handlerSymbol))
            {
                var symbol = graph.GetOrAddSymbolNode(handler.SourceIndexId, handler.SourceLabel, handlerSymbol!, handler.FilePath, handler.StartLine, handler.EndLine, handler.RuleId, handler.EvidenceTier);
                graph.AddEdge(new GraphEdge(
                    $"legacy-root:{handler.CombinedFactId}:{symbol.NodeId}",
                    "legacy-root-selection",
                    root.NodeId,
                    symbol.NodeId,
                    "EvidenceEdge",
                    RuleIds.LegacyFlowRootSelection,
                    handler.EvidenceTier,
                    SupportingFacts(handler, factsBySourceOriginalId),
                    [],
                    SafePath(handler.FilePath),
                    handler.StartLine,
                    handler.EndLine));
            }
        }

        AddUnresolvedRootGaps(graph, facts);

        foreach (var mapping in facts.Where(fact => fact.FactType == FactTypes.WcfServiceReferenceMapping).OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            var operation = ToWcfOperationSurfaceNode(mapping);
            graph.AddNode(operation);
            var sourceSymbol = mapping.SourceSymbol ?? CombinedDependencyReporter.FirstValue(mapping.Properties, "clientOperationSymbol", "generatedClientOperation");
            if (!string.IsNullOrWhiteSpace(sourceSymbol))
            {
                var source = graph.GetOrAddSymbolNode(mapping.SourceIndexId, mapping.SourceLabel, sourceSymbol!, mapping.FilePath, mapping.StartLine, mapping.EndLine, mapping.RuleId, mapping.EvidenceTier);
                graph.AddEdge(new GraphEdge(
                    $"legacy-wcf:{mapping.CombinedFactId}:{source.NodeId}",
                    "wcf-service-reference",
                    source.NodeId,
                    operation.NodeId,
                    "EvidenceEdge",
                    RuleIds.LegacyFlowStaticTraversal,
                    mapping.EvidenceTier,
                    SupportingFacts(mapping, factsBySourceOriginalId),
                    [],
                    SafePath(mapping.FilePath),
                    mapping.StartLine,
                    mapping.EndLine));
            }
        }

        foreach (var legacyData in facts.Where(IsLegacyDataFact).OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            var node = ToLegacyDataSurfaceNode(legacyData);
            if (node is null)
            {
                continue;
            }

            graph.AddNode(node);
            foreach (var symbolValue in LegacyDataAttachmentSymbols(legacyData))
            {
                var symbol = graph.GetOrAddSymbolNode(legacyData.SourceIndexId, legacyData.SourceLabel, symbolValue, legacyData.FilePath, legacyData.StartLine, legacyData.EndLine, legacyData.RuleId, legacyData.EvidenceTier);
                graph.AddEdge(new GraphEdge(
                    $"legacy-data:{legacyData.CombinedFactId}:{symbol.NodeId}",
                    "legacy-data-link",
                    symbol.NodeId,
                    node.NodeId,
                    "EvidenceEdge",
                    RuleIds.LegacyFlowStaticTraversal,
                    legacyData.EvidenceTier,
                    SupportingFacts(legacyData, factsBySourceOriginalId),
                    [],
                    SafePath(legacyData.FilePath),
                    legacyData.StartLine,
                    legacyData.EndLine));
            }
        }

        foreach (var projection in facts.Where(fact => fact.FactType is FactTypes.WebFormsEventFlowProjected or FactTypes.WinFormsHandlerFlowProjected).OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            AddProjectionEdge(graph, projection, factsBySourceOriginalId, surfacesByKind);
        }

        AddRemotingNodesAndEdges(graph, facts, factsBySourceOriginalId);
    }

    private static void AddUnresolvedRootGaps(EvidenceGraph graph, IReadOnlyList<CombinedFactRow> facts)
    {
        var resolvedBindingIds = facts
            .Where(fact => fact.FactType is FactTypes.WebFormsHandlerResolved or FactTypes.WinFormsHandlerResolved)
            .SelectMany(fact => SplitList(CombinedDependencyReporter.FirstValue(fact.Properties, "supportingFactIds"))
                .Select(id => SourceFactKey(fact.SourceIndexId, id)))
            .ToHashSet(StringComparer.Ordinal);
        var resolvedHandlerKeys = facts
            .Where(fact => fact.FactType is FactTypes.WebFormsHandlerResolved or FactTypes.WinFormsHandlerResolved)
            .Select(fact => UiBindingKey(fact))
            .Where(key => key is not null)
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var binding in facts.Where(fact => fact.FactType is FactTypes.WebFormsEventBindingDeclared or FactTypes.WinFormsEventBindingDeclared).OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            var bindingKey = UiBindingKey(binding);
            if (resolvedBindingIds.Contains(SourceFactKey(binding.SourceIndexId, binding.OriginalFactId))
                || (bindingKey is not null && resolvedHandlerKeys.Contains(bindingKey)))
            {
                continue;
            }

            graph.Gaps.Add(new CombinedPathGap(
                $"gap:legacy-root:unresolved:{binding.CombinedFactId}",
                "UnresolvedRoot",
                CombinedDependencyPathClassifications.AnalysisGap,
                "Legacy UI event binding evidence had no resolved handler under available static evidence.",
                binding.SourceIndexId,
                binding.SourceLabel,
                null,
                binding.CombinedFactId,
                RuleIds.LegacyFlowRootSelection,
                EvidenceTiers.Tier4Unknown,
                SafePath(binding.FilePath),
                binding.StartLine,
                "UnresolvedRoot"));
        }
    }

    private static void AddLegacyAvailabilityGaps(EvidenceGraph graph, CombinedReadResult read)
    {
        var first = read.Sources.OrderBy(source => source.Label, StringComparer.Ordinal).FirstOrDefault();
        if (!read.Facts.Any(fact => fact.FactType is FactTypes.WebFormsEventBindingDeclared or FactTypes.WebFormsHandlerResolved or FactTypes.WinFormsEventBindingDeclared or FactTypes.WinFormsHandlerResolved or FactTypes.HttpRouteBinding or FactTypes.WcfServiceHostDeclared or FactTypes.WcfOperationContractDeclared))
        {
            graph.Gaps.Add(new CombinedPathGap(
                "gap:legacy:no-roots-found",
                "NoRootsFound",
                CombinedDependencyPathClassifications.AnalysisGap,
                "No credible WinForms, WebForms, API, or service root evidence was available in the index.",
                first?.SourceIndexId,
                first?.Label,
                null,
                null,
                RuleIds.LegacyFlowInputAvailability,
                EvidenceTiers.Tier4Unknown,
                null,
                null,
                "NoRootsFound"));
        }

        if (!read.Edges.Any(edge => edge.EdgeKind == "parameter-forward"))
        {
            graph.Gaps.Add(new CombinedPathGap(
                "gap:legacy:parameter-forward-unavailable",
                "ExtractorUnavailable",
                CombinedDependencyPathClassifications.AnalysisGap,
                "Parameter-forward evidence was unavailable or empty; value-flow conclusions use available call/object/symbol evidence only.",
                first?.SourceIndexId,
                first?.Label,
                null,
                null,
                RuleIds.LegacyFlowParameterForwardUnavailable,
                EvidenceTiers.Tier4Unknown,
                null,
                null,
                "ParameterForwardEvidenceUnavailable"));
        }

        if (!read.Facts.Any(IsLegacyDataFact))
        {
            graph.Gaps.Add(new CombinedPathGap(
                "gap:legacy:data-metadata-unavailable",
                "ExtractorUnavailable",
                CombinedDependencyPathClassifications.AnalysisGap,
                "Legacy data metadata evidence was unavailable; no-backend conclusions are capped by this missing optional extractor family.",
                first?.SourceIndexId,
                first?.Label,
                null,
                null,
                RuleIds.LegacyFlowInputAvailability,
                EvidenceTiers.Tier4Unknown,
                null,
                null,
                "legacy-data-metadata"));
        }

        AddRemotingAvailabilityGaps(graph, read);
    }

    private static void AddRemotingNodesAndEdges(
        EvidenceGraph graph,
        IReadOnlyList<CombinedFactRow> facts,
        IReadOnlyDictionary<string, CombinedFactRow> factsBySourceOriginalId)
    {
        var remotingFacts = facts
            .Where(IsRemotingFact)
            .OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
            .ToArray();
        var remotingBySourceOriginalId = remotingFacts
            .GroupBy(fact => SourceFactKey(fact.SourceIndexId, fact.OriginalFactId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal).First(), StringComparer.Ordinal);
        var callFacts = facts
            .Where(fact => fact.FactType == FactTypes.CallEdge)
            .OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
            .ToArray();
        var configureCallersByConfig = RemotingConfigureCallersByConfig(remotingFacts, callFacts);

        foreach (var fact in remotingFacts)
        {
            var node = ToRemotingNode(fact);
            graph.AddNode(node);
            foreach (var symbol in RemotingAttachmentSymbols(fact, callFacts, configureCallersByConfig))
            {
                var symbolNode = graph.GetOrAddSymbolNode(fact.SourceIndexId, fact.SourceLabel, symbol, fact.FilePath, fact.StartLine, fact.EndLine, fact.RuleId, fact.EvidenceTier);
                graph.AddEdge(new GraphEdge(
                    $"remoting-evidence:{fact.CombinedFactId}:{symbolNode.NodeId}",
                    "remoting-evidence",
                    symbolNode.NodeId,
                    node.NodeId,
                    "EvidenceEdge",
                    RuleIds.LegacyFlowStaticTraversal,
                    fact.EvidenceTier,
                    RemotingSupportingFacts(fact, factsBySourceOriginalId),
                    [],
                    SafePath(fact.FilePath),
                    fact.StartLine,
                    fact.EndLine));
            }
        }

        foreach (var registration in remotingFacts.Where(fact => fact.FactType == FactTypes.RemotingChannelRegistered))
        {
            var supportingIds = ParseRemotingSupportingFactIds(registration, out var malformed);
            if (malformed)
            {
                graph.Gaps.Add(RemotingGap(
                    registration,
                    "MalformedSupportingFactIds",
                    "Remoting supportingFactIds used mixed delimiters; the field was ignored for static edge construction.",
                    RuleIds.LegacyFlowGapPropagation,
                    CombinedDependencyPathClassifications.AnalysisGap));
                continue;
            }

            var linked = false;
            foreach (var supportingId in supportingIds)
            {
                if (!remotingBySourceOriginalId.TryGetValue(SourceFactKey(registration.SourceIndexId, supportingId), out var declaration)
                    || declaration.FactType != FactTypes.RemotingChannelDeclared)
                {
                    continue;
                }

                graph.AddEdge(new GraphEdge(
                    $"remoting-channel-link:{declaration.CombinedFactId}:{registration.CombinedFactId}",
                    "remoting-channel-link",
                    ToRemotingNode(declaration).NodeId,
                    ToRemotingNode(registration).NodeId,
                    "EvidenceEdge",
                    RuleIds.LegacyFlowStaticTraversal,
                    MaxEvidenceTier(declaration.EvidenceTier, registration.EvidenceTier),
                    [declaration.CombinedFactId, registration.CombinedFactId],
                    [],
                    SafePath(registration.FilePath),
                    registration.StartLine,
                    registration.EndLine));
                linked = true;
            }

            var linkKind = CombinedDependencyReporter.FirstValue(registration.Properties, "linkKind");
            if (!linked && (string.IsNullOrWhiteSpace(linkKind) || string.Equals(linkKind, "unsupported-dynamic-or-nonlocal", StringComparison.Ordinal)))
            {
                graph.Gaps.Add(RemotingGap(
                    registration,
                    "UnsupportedRemotingChannelLink",
                    "Remoting channel registration could not be linked to a channel declaration using deterministic same-source evidence.",
                    registration.RuleId,
                    CombinedDependencyPathClassifications.NeedsReviewStaticPath));
            }
        }

        foreach (var gapFact in facts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap && fact.RuleId.StartsWith("legacy.remoting.", StringComparison.Ordinal))
            .OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            var code = CombinedDependencyReporter.FirstValue(gapFact.Properties, "classification", "gapKind", "reason") ?? "RemotingAnalysisGap";
            graph.Gaps.Add(RemotingGap(
                gapFact,
                code,
                $"Remoting analysis gap preserved from `{gapFact.RuleId}`.",
                RuleIds.LegacyFlowGapPropagation,
                CombinedDependencyPathClassifications.AnalysisGap));
        }
    }

    private static void AddRemotingAvailabilityGaps(EvidenceGraph graph, CombinedReadResult read)
    {
        foreach (var source in read.Sources.OrderBy(source => source.Label, StringComparer.Ordinal).ThenBy(source => source.SourceIndexId, StringComparer.Ordinal))
        {
            if (!IsCSharpSource(source))
            {
                continue;
            }

            var sourceFacts = read.Facts.Where(fact => fact.SourceIndexId == source.SourceIndexId).ToArray();
            if (sourceFacts.Any(IsRemotingFact) || sourceFacts.Any(fact => fact.FactType == FactTypes.AnalysisGap && fact.RuleId.StartsWith("legacy.remoting.", StringComparison.Ordinal)))
            {
                continue;
            }

            if (SourceSupportsRemotingExtraction(source))
            {
                graph.Gaps.Add(new CombinedPathGap(
                    $"gap:legacy-remoting:none:{source.SourceIndexId}",
                    "NoRemotingEvidenceFound",
                    CombinedDependencyPathClassifications.NoBackendEvidence,
                    "No Remoting evidence found under available Remoting extractor coverage; this does not prove Remoting is unused at runtime.",
                    source.SourceIndexId,
                    source.Label,
                    null,
                    null,
                    RuleIds.LegacyFlowInputAvailability,
                    EvidenceTiers.Tier4Unknown,
                    null,
                    null,
                    "legacy-remoting",
                    source.CommitSha,
                    source.ScannerVersion,
                    "source-manifest"));
            }
            else
            {
                graph.Gaps.Add(new CombinedPathGap(
                    $"gap:legacy-remoting:schema:{source.SourceIndexId}",
                    "SchemaMissing",
                    CombinedDependencyPathClassifications.AnalysisGap,
                    "Remoting extractor availability could not be proven from this index; absence is an availability gap, not clean absence.",
                    source.SourceIndexId,
                    source.Label,
                    null,
                    null,
                    RuleIds.LegacyFlowInputAvailability,
                    EvidenceTiers.Tier4Unknown,
                    null,
                    null,
                    "legacy-remoting",
                    source.CommitSha,
                    source.ScannerVersion,
                    "source-manifest"));
            }
        }
    }

    private static void AddProjectionEdge(
        EvidenceGraph graph,
        CombinedFactRow projection,
        IReadOnlyDictionary<string, CombinedFactRow> factsBySourceOriginalId,
        IReadOnlyDictionary<string, CombinedDependencySurfaceRow[]> surfacesByKind)
    {
        var handlerSymbol = projection.SourceSymbol ?? CombinedDependencyReporter.FirstValue(projection.Properties, "handlerSymbolId", "handlerName");
        var surfaceKind = CombinedDependencyReporter.FirstValue(projection.Properties, "terminalSurfaceKind");
        if (string.IsNullOrWhiteSpace(handlerSymbol) || string.IsNullOrWhiteSpace(surfaceKind))
        {
            return;
        }

        var source = graph.GetOrAddSymbolNode(projection.SourceIndexId, projection.SourceLabel, handlerSymbol!, projection.FilePath, projection.StartLine, projection.EndLine, projection.RuleId, projection.EvidenceTier);
        GraphNode terminal;
        var terminalHash = CombinedDependencyReporter.FirstValue(projection.Properties, "terminalSurfaceNameHash");
        if (surfacesByKind.TryGetValue(surfaceKind!, out var candidates) && !string.IsNullOrWhiteSpace(terminalHash))
        {
            var matched = candidates.FirstOrDefault(surface => string.Equals(Hash(surface.DisplayName, 32), terminalHash, StringComparison.Ordinal));
            terminal = matched is null ? ToProjectionTerminalNode(projection, surfaceKind!, terminalHash) : ToSurfaceNode(matched);
        }
        else
        {
            terminal = ToProjectionTerminalNode(projection, surfaceKind!, terminalHash);
        }

        graph.AddNode(terminal);
        if (HasExistingNonProjectionPath(graph, source.NodeId, terminal.NodeId, maxDepth: 12))
        {
            return;
        }

        graph.AddEdge(new GraphEdge(
            $"legacy-projection:{projection.CombinedFactId}:{terminal.NodeId}",
            projection.FactType == FactTypes.WinFormsHandlerFlowProjected ? "winforms-handler-flow-projection" : "webforms-event-flow-projection",
            source.NodeId,
            terminal.NodeId,
            "EvidenceEdge",
            RuleIds.LegacyFlowStaticTraversal,
            projection.EvidenceTier,
            SupportingFacts(projection, factsBySourceOriginalId),
            [],
            SafePath(projection.FilePath),
            projection.StartLine,
            projection.EndLine));
    }

    private static GraphNode ToWebFormsRootNode(CombinedFactRow fact)
    {
        var handlerName = CombinedDependencyReporter.FirstValue(fact.Properties, "handlerName") ?? fact.ContractElement ?? fact.TargetSymbol ?? "handler";
        var eventName = CombinedDependencyReporter.FirstValue(fact.Properties, "eventName") ?? "event";
        var controlId = CombinedDependencyReporter.FirstValue(fact.Properties, "controlId") ?? "control";
        var page = CombinedDependencyReporter.FirstValue(fact.Properties, "markupFile", "pageTypeName") ?? fact.SourceSymbol ?? "page";
        var isLifecycle = IsWebFormsLifecycle(handlerName);
        var label = isLifecycle
            ? $"webforms-lifecycle {SafeDisplay(handlerName)}"
            : $"webforms-event {SafeDisplay(page)}/{SafeDisplay(controlId)}/{SafeDisplay(eventName)}";
        return new GraphNode(
            FactNodeId(fact.CombinedFactId),
            isLifecycle ? "webforms-lifecycle" : "webforms-event",
            label,
            fact.SourceIndexId,
            SafeSourceLabel(fact.SourceLabel),
            fact.ScanId,
            fact.CommitSha,
            HandlerSymbol(fact),
            fact.CombinedFactId,
            RuleIds.LegacyFlowRootSelection,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static GraphNode ToWinFormsRootNode(CombinedFactRow fact)
    {
        var handlerName = CombinedDependencyReporter.FirstValue(fact.Properties, "handlerName") ?? fact.ContractElement ?? fact.TargetSymbol ?? "handler";
        var eventName = CombinedDependencyReporter.FirstValue(fact.Properties, "eventName") ?? "event";
        var controlId = CombinedDependencyReporter.FirstValue(fact.Properties, "controlId") ?? "control";
        var form = CombinedDependencyReporter.FirstValue(fact.Properties, "formTypeName") ?? fact.SourceSymbol ?? "form";
        var label = $"winforms-event {SafeDisplay(form)}/{SafeDisplay(controlId)}/{SafeDisplay(eventName)}";
        return new GraphNode(
            FactNodeId(fact.CombinedFactId),
            "winforms-event",
            label,
            fact.SourceIndexId,
            SafeSourceLabel(fact.SourceLabel),
            fact.ScanId,
            fact.CommitSha,
            HandlerSymbol(fact),
            fact.CombinedFactId,
            RuleIds.LegacyFlowRootSelection,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static GraphNode ToWcfClientNode(CombinedFactRow fact)
    {
        var label = SafeDisplay(fact.TargetSymbol ?? fact.ContractElement ?? "wcf-client") ?? "wcf-client";
        return new GraphNode(
            FactNodeId(fact.CombinedFactId),
            "wcf-client",
            label,
            fact.SourceIndexId,
            SafeSourceLabel(fact.SourceLabel),
            fact.ScanId,
            fact.CommitSha,
            fact.TargetSymbol,
            fact.CombinedFactId,
            fact.RuleId,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static GraphNode ToWcfOperationSurfaceNode(CombinedFactRow fact)
    {
        var operationName = CombinedDependencyReporter.FirstValue(fact.Properties, "normalizedOperationName", "operationName")
            ?? fact.ContractElement
            ?? fact.TargetSymbol
            ?? "operation";
        var safeOperation = SafeDisplay(operationName);
        return new GraphNode(
            SurfaceNodeId(fact.CombinedFactId),
            "wcf-operation",
            $"wcf-operation:{safeOperation}",
            fact.SourceIndexId,
            SafeSourceLabel(fact.SourceLabel),
            fact.ScanId,
            fact.CommitSha,
            fact.TargetSymbol,
            fact.CombinedFactId,
            fact.RuleId,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            "wcf-operation",
            safeOperation,
            null,
            null,
            safeOperation,
            null,
            null,
            null,
            CombinedDependencyReporter.FirstValue(fact.Properties, "mappingHash", "metadataHash"),
            null,
            null,
            null,
            null);
    }

    private static GraphNode? ToLegacyDataSurfaceNode(CombinedFactRow fact)
    {
        var descriptor = LegacyDataModelDescriptorProjection.TryProject(ToSurfaceProjectionInput(fact));
        if (descriptor is null)
        {
            return null;
        }

        return new GraphNode(
            SurfaceNodeId(fact.CombinedFactId),
            "legacy-data",
            descriptor.DisplayName,
            fact.SourceIndexId,
            SafeSourceLabel(fact.SourceLabel),
            fact.ScanId,
            fact.CommitSha,
            fact.TargetSymbol ?? fact.SourceSymbol,
            fact.CombinedFactId,
            fact.RuleId,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            "legacy-data",
            descriptor.DisplayName,
            null,
            null,
            descriptor.MetadataFormat,
            descriptor.DisplayClearance ? descriptor.ContainerName : null,
            null,
            descriptor.SourceArtifactType,
            descriptor.StableModelKey ?? descriptor.DisplayNameHash,
            descriptor.DisplayNameHash,
            CombinedDependencyReporter.FirstValue(fact.Properties, "textLength"),
            null,
            null);
    }

    private static CombinedSurfaceFactInput ToSurfaceProjectionInput(CombinedFactRow fact)
    {
        return new CombinedSurfaceFactInput(
            fact.CombinedFactId,
            fact.SourceIndexId,
            fact.SourceLabel,
            fact.OriginalFactId,
            fact.ScanId,
            fact.CommitSha,
            fact.FactType,
            fact.RuleId,
            fact.EvidenceTier,
            fact.FilePath,
            fact.StartLine,
            fact.EndLine,
            fact.Properties);
    }

    private static GraphNode ToProjectionTerminalNode(CombinedFactRow fact, string surfaceKind, string? terminalHash)
    {
        var safeKind = LegacyTerminalSurfaceKinds.Contains(surfaceKind) ? surfaceKind : "dependency-surface";
        var display = string.IsNullOrWhiteSpace(terminalHash) ? $"{safeKind}:projection:{Hash(fact.CombinedFactId, 16)}" : $"{safeKind}:hash:{terminalHash}";
        return new GraphNode(
            $"projection:{fact.CombinedFactId}:{Hash(display, 16)}",
            safeKind switch
            {
                "wcf-operation" => "wcf-operation",
                "remoting-endpoint" => "remoting-endpoint",
                "remoting-registration" => "remoting-registration",
                "remoting-channel" => "remoting-channel",
                "remoting-object" => "remoting-object",
                "remoting-api" => "remoting-api",
                "legacy-data" => "legacy-data",
                "sql-query" => "SqlSurface",
                "http-client" => "HttpClientSurface",
                _ => "DependencySurface"
            },
            display,
            fact.SourceIndexId,
            SafeSourceLabel(fact.SourceLabel),
            fact.ScanId,
            fact.CommitSha,
            null,
            fact.CombinedFactId,
            fact.RuleId,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            safeKind,
            display,
            null,
            null,
            null,
            null,
            null,
            "projection",
            terminalHash,
            null,
            null,
            null,
            null);
    }

    private static IReadOnlyList<string> SupportingFacts(CombinedFactRow fact, IReadOnlyDictionary<string, CombinedFactRow> factsBySourceOriginalId)
    {
        return SplitList(CombinedDependencyReporter.FirstValue(fact.Properties, "supportingFactIds"))
            .Select(id => factsBySourceOriginalId.TryGetValue(SourceFactKey(fact.SourceIndexId, id), out var supporting) ? supporting.CombinedFactId : id)
            .Append(fact.CombinedFactId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> RemotingSupportingFacts(CombinedFactRow fact, IReadOnlyDictionary<string, CombinedFactRow> factsBySourceOriginalId)
    {
        return ParseRemotingSupportingFactIds(fact, out var malformed)
            .Select(id => factsBySourceOriginalId.TryGetValue(SourceFactKey(fact.SourceIndexId, id), out var supporting) ? supporting.CombinedFactId : id)
            .Append(fact.CombinedFactId)
            .Append(malformed ? $"malformed-supportingFactIds:{Hash(fact.CombinedFactId, 12)}" : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> LegacyDataAttachmentSymbols(CombinedFactRow fact)
    {
        return new[]
            {
                fact.SourceSymbol,
                fact.TargetSymbol,
                CombinedDependencyReporter.FirstValue(fact.Properties, "typeName", "entityName", "generatedTypeName", "generatedSymbol", "tableAdapterTypeName")
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);
    }

    private static string? HandlerSymbol(CombinedFactRow fact)
    {
        return CombinedDependencyReporter.FirstValue(fact.Properties, "handlerSymbol", "handlerSymbolId")
            ?? fact.TargetSymbol
            ?? fact.ContractElement;
    }

    private static string? UiBindingKey(CombinedFactRow fact)
    {
        var controlId = CombinedDependencyReporter.FirstValue(fact.Properties, "controlId");
        var eventName = CombinedDependencyReporter.FirstValue(fact.Properties, "eventName");
        var handlerName = CombinedDependencyReporter.FirstValue(fact.Properties, "handlerName") ?? fact.ContractElement ?? fact.TargetSymbol;
        if (string.IsNullOrWhiteSpace(controlId) || string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(handlerName))
        {
            return null;
        }

        return $"{fact.SourceIndexId}\0{controlId}\0{eventName}\0{handlerName}";
    }

    private static string SourceFactKey(string sourceIndexId, string originalFactId)
    {
        return $"{sourceIndexId}\0{originalFactId}";
    }

    private static bool HasExistingNonProjectionPath(EvidenceGraph graph, string startNodeId, string terminalNodeId, int maxDepth)
    {
        var queue = new Queue<(string NodeId, int Depth)>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
        queue.Enqueue((startNodeId, 0));

        while (queue.Count > 0)
        {
            var (nodeId, depth) = queue.Dequeue();
            if (nodeId == terminalNodeId && depth > 0)
            {
                return true;
            }

            if (depth >= maxDepth || !graph.Outgoing.TryGetValue(nodeId, out var outgoing))
            {
                continue;
            }

            foreach (var edge in outgoing)
            {
                if (IsLegacyFlowProjectionEdge(edge.EdgeKind))
                {
                    continue;
                }

                if (seen.Add(edge.ToNodeId))
                {
                    queue.Enqueue((edge.ToNodeId, depth + 1));
                }
            }
        }

        return false;
    }

    private static bool IsWebFormsLifecycle(string value)
    {
        return value is "Page_Load" or "Page_Init" or "Page_PreRender" or "Application_Start";
    }

    private static bool IsLegacyDataFact(CombinedFactRow fact)
    {
        return LegacyDataModelDescriptorProjection.IsTerminalLegacyDataDescriptor(ToSurfaceProjectionInput(fact));
    }

    private static bool IsRemotingFact(CombinedFactRow fact)
    {
        return fact.FactType.StartsWith("Remoting", StringComparison.Ordinal)
            || fact.RuleId.StartsWith("legacy.remoting.", StringComparison.Ordinal) && fact.FactType != FactTypes.AnalysisGap;
    }

    private static bool IsCSharpSource(CombinedReportSource source)
    {
        return string.Equals(source.Language, "csharp", StringComparison.OrdinalIgnoreCase)
            || source.ScannerVersion.Contains("tracemap", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SourceSupportsRemotingExtraction(CombinedReportSource source)
    {
        return string.Equals(source.ScannerVersion, ScannerVersions.TraceMap, StringComparison.Ordinal)
            || source.ScannerVersion.Contains("legacy-remoting", StringComparison.OrdinalIgnoreCase)
            || TryGetTraceMapMilestone(source.ScannerVersion, out var milestone) && milestone >= 16;
    }

    private static bool TryGetTraceMapMilestone(string value, out int milestone)
    {
        milestone = 0;
        const string token = "milestone";
        var index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var start = index + token.Length;
        var end = start;
        while (end < value.Length && char.IsDigit(value[end]))
        {
            end++;
        }

        return end > start && int.TryParse(value[start..end], out milestone);
    }

    private static IReadOnlyList<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddSymbolNodeAndAttachment(EvidenceGraph graph, CombinedFactRow fact, string? symbol, string combinedFactId)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        var factNodeId = FactNodeId(combinedFactId);
        var symbolNode = graph.GetOrAddSymbolNode(fact.SourceIndexId, fact.SourceLabel, symbol, fact.FilePath, fact.StartLine, fact.EndLine, fact.RuleId, fact.EvidenceTier);
        if (graph.Nodes.ContainsKey(factNodeId))
        {
            graph.AddEdge(new GraphEdge(
                $"fact-symbol:{combinedFactId}:{symbolNode.NodeId}",
                "fact-attached-to-symbol",
                factNodeId,
                symbolNode.NodeId,
                "EvidenceEdge",
                FactAttachedRuleId,
                fact.EvidenceTier,
                [combinedFactId],
                [],
                SafePath(fact.FilePath),
                fact.StartLine,
                fact.EndLine));
            if (fact.FactType is FactTypes.HttpCallDetected or FactTypes.HttpRouteBinding)
            {
                graph.AddEdge(new GraphEdge(
                    $"symbol-fact:{symbolNode.NodeId}:{combinedFactId}",
                    "fact-attached-to-symbol",
                    symbolNode.NodeId,
                    factNodeId,
                    "EvidenceEdge",
                    FactAttachedRuleId,
                    fact.EvidenceTier,
                    [combinedFactId],
                    [],
                    SafePath(fact.FilePath),
                    fact.StartLine,
                    fact.EndLine));
            }
        }
    }

    private static void AddSymbolReconciliationEdges(EvidenceGraph graph)
    {
        var symbolNodes = graph.Nodes.Values
            .Where(node => node.NodeKind is "Symbol" or "Method" or "Type")
            .Select(node => (Node: node, Alias: TryCreateSymbolAlias(node.DisplayName)))
            .Where(pair => pair.Alias is not null)
            .Select(pair => (pair.Node, Alias: pair.Alias!))
            .GroupBy(pair => $"{pair.Node.SourceIndexId}\0{pair.Alias.MemberKey}", pair => pair, StringComparer.Ordinal)
            .ToArray();

        foreach (var group in symbolNodes)
        {
            var qualified = group
                .Where(pair => pair.Alias.TypeKey is not null)
                .GroupBy(pair => $"{pair.Alias.TypeKey}\0{pair.Alias.SignatureKey ?? string.Empty}", StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key!, pair => pair.Select(item => item.Node).DistinctBy(node => node.NodeId).ToArray(), StringComparer.Ordinal);
            var allQualified = qualified.Values.SelectMany(nodes => nodes).DistinctBy(node => node.NodeId).ToArray();
            var bare = group
                .Where(pair => pair.Alias.TypeKey is null)
                .Select(pair => pair.Node)
                .DistinctBy(node => node.NodeId)
                .ToArray();

            foreach (var pair in group.Where(pair => pair.Alias.TypeKey is not null))
            {
                if (!qualified.TryGetValue($"{pair.Alias.TypeKey}\0{pair.Alias.SignatureKey ?? string.Empty}", out var sameType))
                {
                    continue;
                }

                foreach (var target in sameType)
                {
                    AddSymbolReconciliationEdge(graph, pair.Node, target);
                }
            }

            var signedQualifiedByType = group
                .Where(pair => pair.Alias.TypeKey is not null && pair.Alias.SignatureKey is not null)
                .GroupBy(pair => pair.Alias.TypeKey, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key!, pair => pair.Select(item => item.Node).DistinctBy(node => node.NodeId).ToArray(), StringComparer.Ordinal);
            foreach (var unsigned in group.Where(pair => pair.Alias.TypeKey is not null && pair.Alias.SignatureKey is null))
            {
                if (!signedQualifiedByType.TryGetValue(unsigned.Alias.TypeKey!, out var signedSameType) || signedSameType.Length != 1)
                {
                    continue;
                }

                AddSymbolReconciliationEdge(graph, unsigned.Node, signedSameType[0]);
                AddSymbolReconciliationEdge(graph, signedSameType[0], unsigned.Node);
            }

            if (bare.Length == 0 || allQualified.Length != 1)
            {
                continue;
            }

            foreach (var bareNode in bare)
            {
                AddSymbolReconciliationEdge(graph, allQualified[0], bareNode);
                AddSymbolReconciliationEdge(graph, bareNode, allQualified[0]);
            }
        }
    }

    private static void AddSymbolReconciliationEdge(EvidenceGraph graph, GraphNode from, GraphNode to)
    {
        if (from.NodeId == to.NodeId || from.SourceIndexId != to.SourceIndexId)
        {
            return;
        }

        graph.AddEdge(new GraphEdge(
            $"symbol-reconciliation:{from.NodeId}:{to.NodeId}",
            "symbol-reconciliation",
            from.NodeId,
            to.NodeId,
            "EvidenceEdge",
            SymbolReconciliationRuleId,
            EvidenceTiers.Tier3SyntaxOrTextual,
            [],
            [],
            from.FilePath ?? to.FilePath,
            from.StartLine ?? to.StartLine,
            from.EndLine ?? to.EndLine));
    }

    private static SymbolAlias? TryCreateSymbolAlias(string displayName)
    {
        var normalized = displayName.Trim();
        if (normalized.Length == 0 || normalized.Contains("):", StringComparison.Ordinal))
        {
            return null;
        }

        string? signature = null;
        var parenIndex = normalized.IndexOf('(', StringComparison.Ordinal);
        if (parenIndex >= 0)
        {
            var closeParenIndex = normalized.LastIndexOf(')');
            if (closeParenIndex > parenIndex)
            {
                signature = CleanSignature(normalized[(parenIndex + 1)..closeParenIndex]);
            }

            normalized = normalized[..parenIndex];
        }

        normalized = normalized.Trim().TrimEnd('.');
        if (normalized.Length == 0 || normalized.Contains(' '))
        {
            return null;
        }

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var member = CleanSymbolPart(parts[^1]);
        if (member.Length == 0 || !char.IsLetter(member[0]))
        {
            return null;
        }

        var type = parts.Length >= 2 ? CleanSymbolPart(parts[^2]) : null;
        if (string.Equals(type, "global::", StringComparison.Ordinal))
        {
            type = null;
        }

        return new SymbolAlias(
            MemberKey: member.ToLowerInvariant(),
            TypeKey: string.IsNullOrWhiteSpace(type) ? null : type.ToLowerInvariant(),
            SignatureKey: signature);
    }

    private static string CleanSymbolPart(string value)
    {
        var cleaned = value.Trim();
        var globalIndex = cleaned.LastIndexOf("global::", StringComparison.Ordinal);
        if (globalIndex >= 0)
        {
            cleaned = cleaned[(globalIndex + "global::".Length)..];
        }

        var genericIndex = cleaned.IndexOf('<', StringComparison.Ordinal);
        if (genericIndex >= 0)
        {
            cleaned = cleaned[..genericIndex];
        }

        return cleaned;
    }

    private static string? CleanSignature(string value)
    {
        var cleaned = string.Join(",", value
            .Split(',', StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .Select(part => string.Join(" ", part.Split(' ', StringSplitOptions.RemoveEmptyEntries))));
        return cleaned.Length == 0 ? null : cleaned.ToLowerInvariant();
    }

    private static SearchResult Search(EvidenceGraph graph, IReadOnlyList<GraphNode> starts, IReadOnlySet<string> terminalNodeIds, int maxDepth, int maxPaths, int maxFrontier)
    {
        var queue = new Queue<PathState>();
        foreach (var start in starts.OrderBy(node => node.SourceLabel, StringComparer.Ordinal).ThenBy(node => node.DisplayName, StringComparer.Ordinal).ThenBy(node => node.NodeId, StringComparer.Ordinal))
        {
            queue.Enqueue(new PathState([start.NodeId], []));
        }

        var paths = new List<CombinedPath>();
        var gaps = new List<CombinedPathGap>();
        var truncated = false;
        var sequence = 0;
        while (queue.Count > 0 && paths.Count < maxPaths)
        {
            if (queue.Count > maxFrontier)
            {
                truncated = true;
                gaps.Add(TruncatedGap("frontier", queue.Peek().NodeIds[0], graph));
                break;
            }

            var state = queue.Dequeue();
            var currentNodeId = state.NodeIds[^1];
            if (terminalNodeIds.Contains(currentNodeId) && state.EdgeIds.Count > 0)
            {
                sequence++;
                paths.Add(ToPath($"path:{sequence:0000}", graph, state));
                continue;
            }

            if (state.EdgeIds.Count >= maxDepth)
            {
                truncated = true;
                gaps.Add(TruncatedGap("depth", currentNodeId, graph));
                continue;
            }

            if (!graph.Outgoing.TryGetValue(currentNodeId, out var outgoing))
            {
                continue;
            }

            foreach (var edge in outgoing)
            {
                if (state.NodeIds.Contains(edge.ToNodeId, StringComparer.Ordinal))
                {
                    truncated = true;
                    gaps.Add(TruncatedGap("cycle", edge.ToNodeId, graph));
                    continue;
                }

                queue.Enqueue(new PathState([.. state.NodeIds, edge.ToNodeId], [.. state.EdgeIds, edge.EdgeId]));
            }
        }

        if (paths.Count >= maxPaths && queue.Count > 0)
        {
            truncated = true;
            gaps.Add(TruncatedGap("path", queue.Peek().NodeIds[0], graph));
        }

        return new SearchResult(paths, gaps, truncated);
    }

    private static CombinedPath ToPath(string pathId, EvidenceGraph graph, PathState state)
    {
        var nodes = state.NodeIds.Select(nodeId => graph.Nodes[nodeId].ToReportNode()).ToArray();
        var edges = state.EdgeIds.Select(edgeId => graph.EdgesById[edgeId].ToReportEdge()).ToArray();
        var classification = Classify(edges);
        return new CombinedPath(
            pathId,
            classification,
            Confidence(classification),
            edges.Length,
            nodes[0].NodeId,
            nodes[^1].NodeId,
            nodes,
            edges,
            edges.SelectMany(edge => edge.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            edges.SelectMany(edge => edge.SupportingCombinedEdgeIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            NotesFor(edges));
    }

    private static CombinedPath ToNoBackendEvidencePath(string pathId, GraphNode root)
    {
        var node = root.ToReportNode();
        return new CombinedPath(
            pathId,
            CombinedDependencyPathClassifications.NoBackendEvidence,
            "Low",
            0,
            node.NodeId,
            node.NodeId,
            [SanitizeNode(node)],
            [],
            node.CombinedFactId is null ? [] : [node.CombinedFactId],
            [],
            [
                new CombinedPathNote("NoBackendEvidence", "No backend evidence found under available full coverage; absence is not proven."),
                new CombinedPathNote("StaticEvidenceOnly", "Legacy flow results are static evidence and do not prove runtime execution.")
            ]);
    }

    private static CombinedPath ToLegacyPath(CombinedPath path, EvidenceGraph graph)
    {
        var classification = NormalizeClassification(ClassifyLegacy(path, graph), legacyMode: true);
        var notes = path.Notes
            .Append(new CombinedPathNote("StaticEvidenceOnly", "Possible static path evidence only; this does not prove runtime execution, branch feasibility, service reachability, SQL execution, or production use."))
            .Concat(path.Nodes.Any(node => node.NodeKind == "remoting-object")
                ? [new CombinedPathNote("RemotingObjectShapeOnly", "MarshalByRefObject evidence is object-shape evidence only; it does not prove hosting, activation, reachability, deployment, process boundary, lifetime, or production use.")]
                : [])
            .Append(new CombinedPathNote("LegacyFlowRules", string.Join(",", LegacyFlowRuleIdsFor(path).OrderBy(value => value, StringComparer.Ordinal))))
            .DistinctBy(note => $"{note.Code}\0{note.Message}")
            .OrderBy(note => note.Code, StringComparer.Ordinal)
            .ThenBy(note => note.Message, StringComparer.Ordinal)
            .ToArray();
        return path with
        {
            Classification = classification,
            Confidence = Confidence(classification),
            Nodes = path.Nodes.Select(SanitizeNode).ToArray(),
            Edges = path.Edges.Select(SanitizeEdge).ToArray(),
            Notes = notes
        };
    }

    private static string ClassifyLegacy(CombinedPath path, EvidenceGraph graph)
    {
        if (path.Classification == CombinedDependencyPathClassifications.NoBackendEvidence)
        {
            return CombinedDependencyPathClassifications.NoBackendEvidence;
        }

        if (path.Edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier4Unknown)
            || path.Edges.Any(edge => edge.Classification == CombinedEndpointClassifications.UnknownAnalysisGap))
        {
            return CombinedDependencyPathClassifications.AnalysisGap;
        }

        if (path.Nodes.Any(node => node.EvidenceTier == EvidenceTiers.Tier4Unknown))
        {
            return CombinedDependencyPathClassifications.ReducedCoverage;
        }

        var terminal = path.Nodes.LastOrDefault();
        if (terminal is not null && IsRemotingSurface(terminal.SurfaceKind))
        {
            if (path.Edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual)
                || path.Edges.Any(edge => edge.EdgeKind == "symbol-reconciliation" || IsLegacyFlowProjectionEdge(edge.EdgeKind))
                || path.Nodes.Any(node => node.NodeKind == "remoting-object")
                || terminal.SurfaceKind is "remoting-channel" or "remoting-object" or "remoting-api")
            {
                return CombinedDependencyPathClassifications.NeedsReviewStaticPath;
            }

            return CombinedDependencyPathClassifications.ProbableStaticPath;
        }

        var genericTerminal = terminal is not null && IsGenericTerminalKey(terminal.SurfaceName ?? terminal.DisplayName);
        var highFanOut = terminal is not null
            && graph.Edges.Count(edge => edge.ToNodeId == terminal.NodeId) >= 5;
        if (path.Edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual)
            || path.Edges.Any(edge => edge.EdgeKind == "symbol-reconciliation" || IsLegacyFlowProjectionEdge(edge.EdgeKind))
            || path.Edges.Any(edge => edge.EdgeKind == "endpoint-match" && edge.Classification != CombinedEndpointClassifications.MatchedEndpoint)
            || genericTerminal
            || highFanOut)
        {
            return CombinedDependencyPathClassifications.NeedsReviewStaticPath;
        }

        if (path.Edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier2Structural)
            || path.Nodes.Any(node => node.EvidenceTier == EvidenceTiers.Tier2Structural)
            || path.Edges.Any(edge => edge.EdgeKind is "wcf-service-reference" or "legacy-data-link"))
        {
            return CombinedDependencyPathClassifications.ProbableStaticPath;
        }

        return CombinedDependencyPathClassifications.StrongStaticPath;
    }

    private static bool IsRemotingSurface(string? surfaceKind)
    {
        return surfaceKind is "remoting-endpoint" or "remoting-registration" or "remoting-channel" or "remoting-object" or "remoting-api";
    }

    private static bool IsLegacyFlowProjectionEdge(string? edgeKind)
    {
        return edgeKind is "webforms-event-flow-projection" or "winforms-handler-flow-projection";
    }

    private static IReadOnlyList<string> LegacyFlowRuleIdsFor(CombinedPath path)
    {
        var rules = new SortedSet<string>(StringComparer.Ordinal)
        {
            RuleIds.LegacyFlowClassification,
            RuleIds.LegacyFlowReport
        };
        foreach (var node in path.Nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.RuleId))
            {
                rules.Add(node.RuleId);
            }
        }

        foreach (var edge in path.Edges)
        {
            rules.Add(edge.RuleId);
        }

        return rules.ToArray();
    }

    private static CombinedPathNode SanitizeNode(CombinedPathNode node)
    {
        return node with
        {
            DisplayName = SafeDisplay(node.DisplayName) ?? "redacted",
            SourceLabel = SafeSourceLabel(node.SourceLabel),
            FilePath = node.FilePath is null ? null : SafePath(node.FilePath),
            SurfaceName = SafeDisplay(node.SurfaceName),
            OperationName = SafeDisplay(node.OperationName),
            TableName = SafeDisplay(node.TableName),
            ColumnNames = SafeDisplay(node.ColumnNames),
            PackageName = SafeDisplay(node.PackageName),
            ConfigKey = SafeDisplay(node.ConfigKey)
        };
    }

    private static CombinedPathEdge SanitizeEdge(CombinedPathEdge edge)
    {
        return edge with { FilePath = edge.FilePath is null ? null : SafePath(edge.FilePath) };
    }

    private static CombinedPathGap SanitizeGap(CombinedPathGap gap)
    {
        return gap with
        {
            Classification = NormalizeClassification(gap.Classification, legacyMode: true),
            SourceLabel = gap.SourceLabel is null ? null : SafeSourceLabel(gap.SourceLabel),
            Message = SafeDisplay(gap.Message) ?? "redacted",
            FilePath = gap.FilePath is null ? null : SafePath(gap.FilePath)
        };
    }

    private static string Classify(IReadOnlyList<CombinedPathEdge> edges)
    {
        if (edges.Any(edge => edge.Classification == CombinedEndpointClassifications.UnknownAnalysisGap))
        {
            return CombinedDependencyPathClassifications.UnknownAnalysisGap;
        }

        if (edges.Any(edge => edge.EdgeKind == "endpoint-match" && (edge.Classification != CombinedEndpointClassifications.MatchedEndpoint || edge.EvidenceTier != EvidenceTiers.Tier2Structural))
            || edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual))
        {
            return CombinedDependencyPathClassifications.NeedsReviewPath;
        }

        if (edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier2Structural))
        {
            return CombinedDependencyPathClassifications.ProbableStaticPath;
        }

        return CombinedDependencyPathClassifications.StrongStaticPath;
    }

    private static IReadOnlyList<CombinedPathNote> NotesFor(IReadOnlyList<CombinedPathEdge> edges)
    {
        var notes = new List<CombinedPathNote>();
        var valueOriginClassification = ClassifyValueOrigin(edges);
        if (valueOriginClassification is not null)
        {
            notes.Add(new CombinedPathNote(
                "ValueOriginClassification",
                $"{valueOriginClassification}: value-origin context is additive and does not replace the canonical path classification."));
        }

        if (edges.Any(edge => edge.EdgeKind == "endpoint-match"))
        {
            notes.Add(new CombinedPathNote("StaticEndpointEvidence", "Endpoint hops are static method/path evidence and do not prove runtime traffic or reachability."));
        }

        if (edges.Any(edge => edge.EdgeKind == "parameter-forward"))
        {
            notes.Add(new CombinedPathNote("ParameterForwardingBoundary", "Parameter-forwarding hops are direct static argument evidence, not full taint analysis or mutation tracking."));
        }

        if (edges.Any(edge => edge.EdgeKind is "calls" or "creates" or "inherits" or "implements" or "overrides"))
        {
            notes.Add(new CombinedPathNote("StaticCodeEvidence", "Code relationship hops do not prove dynamic dispatch, runtime DI, reflection, branch feasibility, collection contents, or serializer behavior."));
        }

        if (edges.Any(edge => edge.EdgeKind == "symbol-reconciliation"))
        {
            notes.Add(new CombinedPathNote("SymbolReconciliationBoundary", "Symbol reconciliation hops connect source-local symbol names when deterministic aliases match; they are review-tier evidence, not compiler-resolved call evidence."));
        }

        if (edges.Any(edge => edge.EdgeKind is "remoting-evidence" or "remoting-channel-link"))
        {
            notes.Add(new CombinedPathNote("StaticRemotingEvidence", "Static Remoting evidence marks a boundary candidate only; it does not prove runtime channel setup, object activation, process boundary, deployment, reachability, lifetime, or production use."));
        }

        return notes
            .OrderBy(note => note.Code, StringComparer.Ordinal)
            .ThenBy(note => note.Message, StringComparer.Ordinal)
            .ToArray();
    }

    internal static string? ClassifyValueOrigin(IReadOnlyList<CombinedPathEdge> edges)
    {
        var valueEdges = edges
            .Where(edge => edge.EdgeKind is "parameter-forward" or "argument-passed")
            .ToArray();
        if (valueEdges.Length == 0)
        {
            return null;
        }

        if (valueEdges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier4Unknown)
            || Classify(edges) == CombinedDependencyPathClassifications.UnknownAnalysisGap)
        {
            return CombinedValueOriginClassifications.UnknownAnalysisGap;
        }

        if (edges.Any(edge => edge.EdgeKind == "symbol-reconciliation")
            || valueEdges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual)
            || edges.Any(edge => edge.EdgeKind == "endpoint-match" && edge.Classification != CombinedEndpointClassifications.MatchedEndpoint))
        {
            return CombinedValueOriginClassifications.NeedsReviewValuePath;
        }

        if (edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier2Structural)
            || valueEdges.Any(edge => edge.EvidenceTier != EvidenceTiers.Tier1Semantic))
        {
            return CombinedValueOriginClassifications.ProbableStaticValuePath;
        }

        return CombinedValueOriginClassifications.StrongStaticValuePath;
    }

    private static SelectorResolution ResolveStartNodes(CombinedDependencyPathOptions options, EvidenceGraph graph, string? sourceFilter)
    {
        var legacyMode = options.IncludeLegacyRoots || IsLegacyView(options.View);
        IEnumerable<GraphNode> candidates;
        if (!string.IsNullOrWhiteSpace(options.FromEndpoint))
        {
            var endpoint = ParseEndpointSelector(options.FromEndpoint);
            candidates = graph.Nodes.Values.Where(node =>
                node.NodeKind is "EndpointClient" or "EndpointRoute"
                && CombinedDependencyReporter.MethodsCompatible(endpoint.Method, node.HttpMethod ?? "ANY")
                && string.Equals(node.NormalizedPathKey, endpoint.PathKey, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(options.FromSymbol))
            {
                var selector = options.FromSymbol.Trim();
                candidates = candidates.Where(node => EndpointNodeMatchesSymbol(graph, node, selector));
            }
        }
        else if (!string.IsNullOrWhiteSpace(options.FromWebFormsEvent))
        {
            var selector = options.FromWebFormsEvent.Trim();
            candidates = graph.Nodes.Values.Where(node =>
                node.NodeKind is "webforms-event" or "webforms-lifecycle"
                && WebFormsRootMatches(node, selector));
        }
        else if (!string.IsNullOrWhiteSpace(options.FromSymbol))
        {
            var selector = options.FromSymbol.Trim();
            candidates = graph.Nodes.Values.Where(node =>
                node.NodeKind is "Symbol" or "Method" or "Type" or "webforms-event" or "webforms-lifecycle" or "EndpointRoute" or "wcf-operation"
                && NodeMatchesSymbol(node, selector));
        }
        else if (!string.IsNullOrWhiteSpace(sourceFilter))
        {
            candidates = graph.Nodes.Values.Where(node => string.Equals(node.SourceLabel, sourceFilter, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            var matchedClientIds = graph.Edges
                .Where(edge => edge.EdgeKind == "endpoint-match")
                .Select(edge => edge.FromNodeId)
                .ToHashSet(StringComparer.Ordinal);
            candidates = graph.Nodes.Values.Where(node =>
                matchedClientIds.Contains(node.NodeId)
                || (legacyMode && node.NodeKind is "webforms-event" or "webforms-lifecycle" or "winforms-event" or "EndpointRoute"));
        }

        if (!string.IsNullOrWhiteSpace(sourceFilter))
        {
            candidates = candidates.Where(node => string.Equals(node.SourceLabel, sourceFilter, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = candidates
            .OrderBy(node => node.SourceLabel, StringComparer.Ordinal)
            .ThenBy(node => node.DisplayName, StringComparer.Ordinal)
            .ThenBy(node => node.FilePath, StringComparer.Ordinal)
            .ThenBy(node => node.StartLine ?? 0)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        return new SelectorResolution(ordered.Take(SelectorCandidateLimit).ToArray(), ordered.Length);
    }

    private static bool WebFormsRootMatches(GraphNode node, string selector)
    {
        return string.Equals(node.CombinedFactId, selector, StringComparison.Ordinal)
            || string.Equals(node.DisplayName, selector, StringComparison.OrdinalIgnoreCase)
            || node.DisplayName.Contains(selector, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(node.SymbolId) && string.Equals(node.SymbolId, selector, StringComparison.Ordinal));
    }

    private static bool EndpointNodeMatchesSymbol(EvidenceGraph graph, GraphNode endpointNode, string selector)
    {
        return graph.Outgoing.TryGetValue(endpointNode.NodeId, out var outgoing)
            && outgoing
                .Where(edge => edge.EdgeKind == "fact-attached-to-symbol")
                .Select(edge => graph.Nodes.TryGetValue(edge.ToNodeId, out var node) ? node : null)
                .OfType<GraphNode>()
                .Any(node => NodeMatchesSymbol(node, selector));
    }

    private static bool NodeMatchesSymbol(GraphNode node, string selector)
    {
        return string.Equals(node.SymbolId, selector, StringComparison.Ordinal)
            || string.Equals(node.DisplayName, selector, StringComparison.Ordinal)
            || node.DisplayName.Contains(selector, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlySet<string> ResolveTerminalNodes(CombinedDependencyPathOptions options, EvidenceGraph graph, IReadOnlyList<GraphNode> startNodes)
    {
        var surfaceKind = string.IsNullOrWhiteSpace(options.ToSurface) ? null : options.ToSurface.Trim();
        var surfaceName = string.IsNullOrWhiteSpace(options.SurfaceName) ? null : options.SurfaceName.Trim();
        var explicitSurfaceKind = surfaceKind is not null;
        var startFactIds = startNodes
            .Select(node => node.CombinedFactId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
        return graph.Nodes.Values
            .Where(node => node.SurfaceKind is not null)
            .Where(node => explicitSurfaceKind || IsDefaultTerminalSurface(node, startFactIds))
            .Where(node => surfaceKind is null || string.Equals(node.SurfaceKind, surfaceKind, StringComparison.Ordinal))
            .Where(node => surfaceName is null || SurfaceNameMatches(node, surfaceName))
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsDefaultTerminalSurface(GraphNode node, IReadOnlySet<string> startFactIds)
    {
        if (node.SurfaceKind == "http-route")
        {
            return false;
        }

        if (node.SurfaceKind is "remoting-channel" or "remoting-object" or "remoting-api")
        {
            return false;
        }

        return node.SurfaceKind != "http-client"
            || node.CombinedFactId is null
            || !startFactIds.Contains(node.CombinedFactId);
    }

    private static bool SurfaceNameMatches(GraphNode node, string selector)
    {
        var values = new[]
        {
            node.SurfaceName ?? node.DisplayName,
            node.CombinedFactId,
            OriginalFactIdFromCombined(node.CombinedFactId),
            node.TableName,
            node.ShapeHash,
            node.TextHash
        }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToArray();
        if (selector == "*")
        {
            return true;
        }

        var starts = selector.StartsWith('*');
        var ends = selector.EndsWith('*');
        var trimmed = selector.Trim('*');
        if (starts && ends)
        {
            return values.Any(value => value.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        if (starts)
        {
            return values.Any(value => value.EndsWith(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        if (ends)
        {
            return values.Any(value => value.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        return values.Any(value => string.Equals(value, selector, StringComparison.OrdinalIgnoreCase));
    }

    private static CombinedPathGap CreateSelectorGap(CombinedReadResult read, CombinedDependencyPathOptions options, string? sourceFilter)
    {
        var message = "No starting evidence matched the query selectors.";
        if (!string.IsNullOrWhiteSpace(options.FromSource) && !read.Sources.Any(source => string.Equals(source.Label, sourceFilter, StringComparison.OrdinalIgnoreCase)))
        {
            message = $"No source matched `{sourceFilter}`.";
        }

        return new CombinedPathGap(
            "gap:selector:no-start-node",
            "SelectorNoMatch",
            CombinedDependencyPathClassifications.SelectorNoMatch,
            message,
            null,
            sourceFilter,
            null,
            null,
            QueryGapRuleId,
            EvidenceTiers.Tier4Unknown,
            null,
            null,
            "selector");
    }

    private static CombinedPathGap CreateNoPathGap(CombinedReadResult read, EvidenceGraph graph, IReadOnlyList<GraphNode> starts, int maxDepth, int maxFrontier, bool legacyMode)
    {
        var sourceIds = ReachableSourceIds(graph, starts, maxDepth, maxFrontier);
        var reducedSource = read.Sources
            .Where(source => sourceIds.Contains(source.SourceIndexId))
            .FirstOrDefault(SourceHasReducedCoverage);
        if (reducedSource is not null)
        {
            return new CombinedPathGap(
                $"gap:no-path:coverage:{reducedSource.SourceIndexId}",
                legacyMode ? "ReducedCoverage" : "UnknownAnalysisGap",
                legacyMode ? CombinedDependencyPathClassifications.ReducedCoverage : CombinedDependencyPathClassifications.UnknownAnalysisGap,
                $"No path was found, but `{reducedSource.Label}` has reduced coverage; absence of evidence is coverage-relative.",
                reducedSource.SourceIndexId,
                reducedSource.Label,
                starts.FirstOrDefault(node => node.SourceIndexId == reducedSource.SourceIndexId)?.NodeId,
                null,
                QueryGapRuleId,
                EvidenceTiers.Tier4Unknown,
                null,
                null,
                "coverage");
        }

        var first = starts.First();
        return new CombinedPathGap(
            $"gap:no-path:{first.NodeId}",
            legacyMode ? "NoBackendEvidence" : "NoPathFound",
            legacyMode ? CombinedDependencyPathClassifications.NoBackendEvidence : CombinedDependencyPathClassifications.NoPathFound,
            legacyMode
                ? "No backend evidence found under available full coverage; absence is not proven."
                : "Selectors matched starting evidence, but no path reached a terminal dependency surface within the current graph and bounds.",
            first.SourceIndexId,
            first.SourceLabel,
            first.NodeId,
            first.CombinedFactId,
            first.RuleId,
            first.EvidenceTier,
            first.FilePath,
            first.StartLine,
            "no-path");
    }

    private static bool SourceHasReducedCoverage(CombinedReportSource source)
    {
        return !source.AnalysisLevel.Equals("Level1SemanticAnalysis", StringComparison.Ordinal)
            || !source.BuildStatus.Equals("Succeeded", StringComparison.Ordinal)
            || source.CommitSha == "unknown";
    }

    private static IReadOnlySet<string> ReachableSourceIds(EvidenceGraph graph, IReadOnlyList<GraphNode> starts, int maxDepth, int maxFrontier)
    {
        var sourceIds = new HashSet<string>(starts.Select(node => node.SourceIndexId), StringComparer.Ordinal);
        var queue = new Queue<(string NodeId, int Depth)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in starts)
        {
            queue.Enqueue((start.NodeId, 0));
            seen.Add(start.NodeId);
        }

        var visitedStates = 0;
        while (queue.Count > 0 && visitedStates < maxFrontier)
        {
            visitedStates++;
            var (nodeId, depth) = queue.Dequeue();
            if (graph.Nodes.TryGetValue(nodeId, out var node))
            {
                sourceIds.Add(node.SourceIndexId);
            }

            if (depth >= maxDepth || !graph.Outgoing.TryGetValue(nodeId, out var outgoing))
            {
                continue;
            }

            foreach (var edge in outgoing)
            {
                if (seen.Add(edge.ToNodeId))
                {
                    queue.Enqueue((edge.ToNodeId, depth + 1));
                }
            }
        }

        return sourceIds;
    }

    private static IReadOnlyList<string> SurfaceAttachmentSymbols(CombinedFactRow fact)
    {
        if (IsLegacyDataFact(fact))
        {
            return LegacyDataAttachmentSymbols(fact).ToArray();
        }

        return new[]
            {
                fact.SourceSymbol,
                CombinedDependencyReporter.FirstValue(
                    fact.Properties,
                    "methodSymbol",
                    "containingMethod",
                    "containingSymbol",
                    "containingType",
                    "targetContainingType")
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static GraphNode ToRemotingNode(CombinedFactRow fact)
    {
        var surfaceKind = RemotingSurfaceKind(fact);
        var nodeKind = RemotingNodeKind(fact);
        var displayHash = RemotingDisplayHash(fact);
        var safeName = SafeDisplay(CombinedDependencyReporter.FirstValue(
                fact.Properties,
                "targetTypeName",
                "typeName",
                "channelTypeName",
                "channelKind",
                "providerKind",
                "apiName",
                "registrationKind")
            ?? fact.TargetSymbol
            ?? fact.ContractElement
            ?? displayHash
            ?? fact.OriginalFactId)
            ?? displayHash
            ?? fact.OriginalFactId;
        var display = displayHash is null || string.Equals(safeName, displayHash, StringComparison.Ordinal)
            ? $"{nodeKind}:{safeName}"
            : $"{nodeKind}:{safeName}:{displayHash}";
        var surfaceName = displayHash ?? safeName;
        return new GraphNode(
            SurfaceNodeId(fact.CombinedFactId),
            nodeKind,
            display,
            fact.SourceIndexId,
            SafeSourceLabel(fact.SourceLabel),
            fact.ScanId,
            fact.CommitSha,
            fact.TargetSymbol ?? fact.SourceSymbol,
            fact.CombinedFactId,
            fact.RuleId,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            surfaceKind,
            surfaceName,
            null,
            null,
            SafeDisplay(CombinedDependencyReporter.FirstValue(fact.Properties, "registrationKind", "apiKind")),
            null,
            null,
            "remoting-static-evidence",
            displayHash,
            null,
            null,
            null,
            null);
    }

    private static string? RemotingSurfaceKind(CombinedFactRow fact)
    {
        return fact.FactType switch
        {
            FactTypes.RemotingServiceTypeRegistered or FactTypes.RemotingClientTypeRegistered => "remoting-registration",
            FactTypes.RemotingClientActivationDeclared or FactTypes.RemotingConfigServiceDeclared or FactTypes.RemotingConfigClientDeclared => "remoting-endpoint",
            FactTypes.RemotingChannelDeclared or FactTypes.RemotingChannelRegistered or FactTypes.RemotingConfigChannelDeclared or FactTypes.RemotingConfigProviderDeclared => "remoting-channel",
            FactTypes.RemotingMarshalByRefObjectDeclared => "remoting-object",
            FactTypes.RemotingApiUsageDeclared or FactTypes.RemotingConfigSectionDeclared => "remoting-api",
            _ => null
        };
    }

    private static string RemotingNodeKind(CombinedFactRow fact)
    {
        return fact.FactType switch
        {
            FactTypes.RemotingServiceTypeRegistered
                or FactTypes.RemotingClientTypeRegistered => "remoting-registration",
            FactTypes.RemotingClientActivationDeclared
                or FactTypes.RemotingConfigServiceDeclared
                or FactTypes.RemotingConfigClientDeclared => "remoting-endpoint",
            FactTypes.RemotingChannelDeclared
                or FactTypes.RemotingChannelRegistered
                or FactTypes.RemotingConfigChannelDeclared
                or FactTypes.RemotingConfigProviderDeclared => "remoting-channel",
            FactTypes.RemotingMarshalByRefObjectDeclared => "remoting-object",
            FactTypes.RemotingApiUsageDeclared or FactTypes.RemotingConfigSectionDeclared => "remoting-api",
            _ => "remoting-api"
        };
    }

    private static IEnumerable<string> RemotingAttachmentSymbols(
        CombinedFactRow fact,
        IReadOnlyList<CombinedFactRow> callFacts,
        IReadOnlyDictionary<string, IReadOnlyList<string>> configureCallersByConfig)
    {
        var symbols = new List<string?>
            {
                fact.SourceSymbol,
                fact.TargetSymbol,
                CombinedDependencyReporter.FirstValue(
                    fact.Properties,
                    "methodSymbol",
                    "containingMethod",
                    "containingSymbol",
                    "targetTypeName",
                    "typeName",
                    "channelTypeName")
            };
        symbols.AddRange(RemotingCallSiteAttachmentSymbols(fact, callFacts));
        if (IsRemotingConfigFact(fact))
        {
            var configFile = Path.GetFileName(fact.FilePath);
            if (!string.IsNullOrWhiteSpace(configFile)
                && configureCallersByConfig.TryGetValue(SourceFactKey(fact.SourceIndexId, configFile), out var configureCallers))
            {
                symbols.AddRange(configureCallers);
            }
        }

        return symbols
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> RemotingConfigureCallersByConfig(
        IReadOnlyList<CombinedFactRow> remotingFacts,
        IReadOnlyList<CombinedFactRow> callFacts)
    {
        var result = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var fact in remotingFacts.Where(fact => fact.FactType == FactTypes.RemotingApiUsageDeclared))
        {
            if (!IsConfigureRemotingApiFact(fact))
            {
                continue;
            }

            var configFileName = CombinedDependencyReporter.FirstValue(fact.Properties, "configFileName");
            if (string.IsNullOrWhiteSpace(configFileName))
            {
                continue;
            }

            var key = SourceFactKey(fact.SourceIndexId, configFileName);
            if (!result.TryGetValue(key, out var callers))
            {
                callers = [];
                result[key] = callers;
            }

            foreach (var symbol in RemotingCallSiteAttachmentSymbols(fact, callFacts))
            {
                callers.Add(symbol);
            }
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static IEnumerable<string> RemotingCallSiteAttachmentSymbols(CombinedFactRow fact, IReadOnlyList<CombinedFactRow> callFacts)
    {
        foreach (var call in callFacts)
        {
            if (!RemotingCallSiteMatches(fact, call))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(call.SourceSymbol))
            {
                yield return call.SourceSymbol!;
            }

            if (!string.IsNullOrWhiteSpace(call.TargetSymbol))
            {
                yield return call.TargetSymbol!;
            }
        }
    }

    private static bool RemotingCallSiteMatches(CombinedFactRow remotingFact, CombinedFactRow callFact)
    {
        return string.Equals(remotingFact.SourceIndexId, callFact.SourceIndexId, StringComparison.Ordinal)
            && string.Equals(SafePath(remotingFact.FilePath), SafePath(callFact.FilePath), StringComparison.Ordinal)
            && callFact.StartLine >= remotingFact.StartLine
            && callFact.StartLine <= remotingFact.EndLine
            && RemotingCallTargetMatches(remotingFact, callFact.TargetSymbol ?? CombinedDependencyReporter.FirstValue(callFact.Properties, "calleeName"));
    }

    private static bool RemotingCallTargetMatches(CombinedFactRow fact, string? targetSymbol)
    {
        if (string.IsNullOrWhiteSpace(targetSymbol))
        {
            return false;
        }

        return ExpectedRemotingCallNames(fact).Any(expected => SymbolNameMatches(targetSymbol, expected));
    }

    private static IEnumerable<string> ExpectedRemotingCallNames(CombinedFactRow fact)
    {
        if (fact.FactType == FactTypes.RemotingChannelRegistered)
        {
            yield return "RegisterChannel";
            yield break;
        }

        if (fact.FactType == FactTypes.RemotingClientActivationDeclared)
        {
            yield return "GetObject";
            yield break;
        }

        if (fact.FactType == FactTypes.RemotingApiUsageDeclared)
        {
            foreach (var candidate in new[] { fact.TargetSymbol, fact.ContractElement, CombinedDependencyReporter.FirstValue(fact.Properties, "apiName") })
            {
                var name = LastSymbolPart(candidate);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    yield return name!;
                }
            }

            yield break;
        }

        var registrationKind = CombinedDependencyReporter.FirstValue(fact.Properties, "registrationKind") ?? fact.ContractElement;
        switch (registrationKind)
        {
            case "well-known-service":
                yield return "RegisterWellKnownServiceType";
                break;
            case "well-known-client":
                yield return "RegisterWellKnownClientType";
                break;
            case "activated-service":
                yield return "RegisterActivatedServiceType";
                break;
            case "activated-client":
                yield return "RegisterActivatedClientType";
                break;
            case "configure":
                yield return "Configure";
                break;
        }
    }

    private static bool SymbolNameMatches(string candidate, string expected)
    {
        return string.Equals(candidate, expected, StringComparison.Ordinal)
            || candidate.EndsWith("." + expected, StringComparison.Ordinal)
            || candidate.Contains("." + expected + "(", StringComparison.Ordinal);
    }

    private static string? LastSymbolPart(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var paren = symbol.IndexOf('(', StringComparison.Ordinal);
        var end = paren >= 0 ? paren : symbol.Length;
        var dot = symbol.LastIndexOf('.', end - 1, end);
        return dot >= 0 ? symbol[(dot + 1)..end] : symbol[..end];
    }

    private static bool IsConfigureRemotingApiFact(CombinedFactRow fact)
    {
        return fact.FactType == FactTypes.RemotingApiUsageDeclared
            && ExpectedRemotingCallNames(fact).Any(name => string.Equals(name, "Configure", StringComparison.Ordinal));
    }

    private static bool IsRemotingConfigFact(CombinedFactRow fact)
    {
        return fact.FactType is FactTypes.RemotingConfigServiceDeclared
            or FactTypes.RemotingConfigClientDeclared
            or FactTypes.RemotingConfigChannelDeclared
            or FactTypes.RemotingConfigProviderDeclared;
    }

    private static string? RemotingDisplayHash(CombinedFactRow fact)
    {
        foreach (var (property, prefix) in new[]
        {
            ("urlHash", "url"),
            ("objectUriHash", "objectUri"),
            ("valueHash", "value"),
            ("applicationNameHash", "application"),
            ("configValueHash", "value")
        })
        {
            var value = CombinedDependencyReporter.FirstValue(fact.Properties, property);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var hash = new string(value.Trim().Where(UriHashCharacter).Take(8).ToArray()).ToLowerInvariant();
            if (hash.Length > 0)
            {
                return $"{prefix}-{hash}";
            }
        }

        return null;
    }

    private static bool UriHashCharacter(char value)
    {
        return value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }

    private static IReadOnlyList<string> ParseRemotingSupportingFactIds(CombinedFactRow fact, out bool malformed)
    {
        malformed = false;
        var value = CombinedDependencyReporter.FirstValue(fact.Properties, "supportingFactIds");
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var hasSemicolon = value.Contains(';', StringComparison.Ordinal);
        var hasComma = value.Contains(',', StringComparison.Ordinal);
        if (hasSemicolon && hasComma)
        {
            malformed = true;
            return [];
        }

        var tokens = hasSemicolon
            ? value.Split(';')
            : hasComma
                ? value.Split(',')
                : [value];
        return tokens
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();
    }

    private static CombinedPathGap RemotingGap(
        CombinedFactRow fact,
        string code,
        string message,
        string ruleId,
        string classification)
    {
        return new CombinedPathGap(
            $"gap:legacy-remoting:{code}:{fact.CombinedFactId}",
            code,
            classification,
            message,
            fact.SourceIndexId,
            fact.SourceLabel,
            IsRemotingFact(fact) ? ToRemotingNode(fact).NodeId : null,
            fact.CombinedFactId,
            ruleId,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            "legacy-remoting");
    }

    private static string MaxEvidenceTier(string left, string right)
    {
        return EvidenceTierRank(left) >= EvidenceTierRank(right) ? left : right;
    }

    private static int EvidenceTierRank(string tier)
    {
        return tier switch
        {
            EvidenceTiers.Tier1Semantic => 1,
            EvidenceTiers.Tier2Structural => 2,
            EvidenceTiers.Tier3SyntaxOrTextual => 3,
            EvidenceTiers.Tier4Unknown => 4,
            _ => 4
        };
    }

    private static string? OriginalFactIdFromCombined(string? combinedFactId)
    {
        if (string.IsNullOrWhiteSpace(combinedFactId))
        {
            return null;
        }

        var index = combinedFactId.IndexOf(':', StringComparison.Ordinal);
        return index >= 0 && index < combinedFactId.Length - 1 ? combinedFactId[(index + 1)..] : combinedFactId;
    }

    private static bool IsDependencySurfaceFact(CombinedFactRow fact)
    {
        if (fact.Properties.TryGetValue("surfaceKind", out var surfaceKind) && !string.IsNullOrWhiteSpace(surfaceKind))
        {
            return true;
        }

        if (IsLegacyDataFact(fact))
        {
            return true;
        }

        return fact.FactType is FactTypes.QueryPatternDetected
            or FactTypes.SqlTextUsed
            or FactTypes.DatabaseColumnMapping
            or FactTypes.DapperCallDetected
            or FactTypes.SqlCommandDetected
            or FactTypes.HttpCallDetected
            or FactTypes.PackageReferenced
            or FactTypes.ConfigBinding
            or FactTypes.ConfigKeyDeclared
            or FactTypes.ConnectionStringDeclared;
    }

    private static CombinedPathGap TruncatedGap(string reason, string nodeId, EvidenceGraph graph)
    {
        graph.Nodes.TryGetValue(nodeId, out var node);
        return new CombinedPathGap(
            $"gap:truncated:{reason}:{nodeId}",
            "TruncatedByLimit",
            CombinedDependencyPathClassifications.NeedsReviewPath,
            $"Path search was truncated by {reason} limit.",
            node?.SourceIndexId,
            node?.SourceLabel,
            nodeId,
            node?.CombinedFactId,
            TruncationGapRuleId,
            EvidenceTiers.Tier4Unknown,
            node?.FilePath,
            node?.StartLine,
            reason);
    }

    private static GraphNode ToEndpointNode(CombinedFactRow fact)
    {
        var isClient = fact.FactType == FactTypes.HttpCallDetected;
        var method = string.Join(",", CombinedDependencyReporter.SplitMethods(CombinedDependencyReporter.FirstValue(fact.Properties, "httpMethod", "httpMethods", "methodName") ?? fact.ContractElement));
        var normalized = CombinedDependencyReporter.TryNormalizeEndpoint(fact.Properties);
        var key = CombinedDependencyReporter.FirstValue(fact.Properties, "normalizedPathKey")
            ?? normalized?.PathKey
            ?? CombinedDependencyReporter.FirstValue(fact.Properties, "normalizedPathTemplate", "routeTemplate", "path")
            ?? fact.TargetSymbol
            ?? "unknown";
        return new GraphNode(
            FactNodeId(fact.CombinedFactId),
            isClient ? "EndpointClient" : "EndpointRoute",
            $"{method} {key}",
            fact.SourceIndexId,
            fact.SourceLabel,
            fact.ScanId,
            fact.CommitSha,
            null,
            fact.CombinedFactId,
            fact.RuleId,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            null,
            null,
            method,
            key,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static GraphNode ToSurfaceNode(CombinedDependencySurfaceRow surface)
    {
        return new GraphNode(
            SurfaceNodeId(surface.CombinedFactId),
            surface.SurfaceKind switch
            {
                "sql-query" => "SqlSurface",
                "sql-persistence" => "SqlPersistenceSurface",
                "http-client" => "HttpClientSurface",
                "http-route" => "HttpRouteSurface",
                "package-config" => surface.ConfigKey is not null ? "ConfigSurface" : "PackageSurface",
                "legacy-data" => "legacy-data",
                "message-queue" or "message-topic" or "message-subscription" or "message-exchange" or "message-stream" or "message-event" or "message-channel" or "message-unknown" => "MessageSurface",
                _ => "DependencySurface"
            },
            surface.DisplayName,
            surface.SourceIndexId,
            surface.SourceLabel,
            surface.ScanId,
            surface.CommitSha,
            null,
            surface.CombinedFactId,
            surface.RuleId,
            surface.EvidenceTier,
            SafePath(surface.FilePath),
            surface.StartLine,
            surface.EndLine,
            surface.SurfaceKind,
            surface.DisplayName,
            surface.HttpMethod,
            surface.NormalizedPathKey,
            surface.OperationName,
            surface.TableName,
            surface.ColumnNames,
            surface.SourceKind,
            surface.ShapeHash,
            surface.TextHash,
            surface.TextLength,
            surface.PackageName,
            surface.ConfigKey);
    }

    private static bool IsMessageSurfaceKind(string? surfaceKind)
    {
        return surfaceKind is "message-queue"
            or "message-topic"
            or "message-subscription"
            or "message-exchange"
            or "message-stream"
            or "message-event"
            or "message-channel"
            or "message-unknown";
    }

    private static (string Method, string PathKey) ParseEndpointSelector(string value)
    {
        var trimmed = value.Trim();
        var separator = trimmed.IndexOf(' ', StringComparison.Ordinal);
        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            throw new ArgumentException("--from-endpoint must be '<METHOD> <PATH_KEY>'.");
        }

        var path = trimmed[(separator + 1)..].Trim();
        var normalized = EndpointRouteNormalizer.Normalize(path);
        return (trimmed[..separator].Trim().ToUpperInvariant(), normalized.PathKey);
    }

    internal static (string Client, string Server)? ParseSourcePair(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder();
        string? client = null;
        var escaped = false;
        foreach (var character in value)
        {
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == ':' && client is null)
            {
                client = builder.ToString();
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        if (escaped)
        {
            builder.Append('\\');
        }

        if (client is null)
        {
            throw new ArgumentException("paths --source-pair must be '<client>:<server>'; escape literal colons as \\:.");
        }

        client = client.Trim();
        var server = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(client) || string.IsNullOrWhiteSpace(server))
        {
            throw new ArgumentException("paths --source-pair client and server labels cannot be empty.");
        }

        return (client, server);
    }

    private static string EscapeSourcePairLabel(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(":", "\\:", StringComparison.Ordinal);
    }

    private static async Task ValidatePathSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await CombinedDependencyReporter.ValidateCombinedIndexAsync(connection, cancellationToken);
    }

    private static async Task<(string? MarkdownPath, string? JsonPath)> WriteOutputsAsync(string outputPath, string format, CombinedDependencyPathReport report, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(outputPath);
        if (Directory.Exists(fullPath) || string.IsNullOrWhiteSpace(Path.GetExtension(fullPath)))
        {
            Directory.CreateDirectory(fullPath);
            var markdownPath = Path.Combine(fullPath, "paths-report.md");
            var jsonPath = Path.Combine(fullPath, "paths-report.json");
            await File.WriteAllTextAsync(markdownPath, RenderMarkdown(report), cancellationToken);
            await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, CombinedDependencyReporter.JsonOptions) + Environment.NewLine, cancellationToken);
            return (markdownPath, jsonPath);
        }

        var directoryName = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        if (format == "json")
        {
            await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, CombinedDependencyReporter.JsonOptions) + Environment.NewLine, cancellationToken);
            return (null, fullPath);
        }

        await File.WriteAllTextAsync(fullPath, RenderMarkdown(report), cancellationToken);
        return (fullPath, null);
    }

    private static string RenderMarkdown(CombinedDependencyPathReport report)
    {
        var legacyMode = report.SchemaVersion == LegacyFlowReportConstants.SchemaVersion || report.View == LegacyFlowReportConstants.View;
        var builder = new StringBuilder();
        builder.AppendLine(legacyMode ? "# Legacy Static Flow Report" : "# TraceMap Paths Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine(legacyMode
            ? "- Results are static evidence views of possible static paths; they do not prove runtime execution, reachability, SQL execution, production dependency, or impact."
            : "- Paths are static evidence trails, not runtime execution traces.");
        builder.AppendLine($"- Report coverage: `{report.ReportCoverage}`");
        if (legacyMode)
        {
            builder.AppendLine($"- Schema version: `{report.SchemaVersion}`");
        }
        builder.AppendLine($"- Sources: `{report.Summary.SourceCount}`");
        builder.AppendLine($"- Graph nodes: `{report.Summary.GraphNodeCount}`");
        builder.AppendLine($"- Graph edges: `{report.Summary.GraphEdgeCount}`");
        builder.AppendLine($"- Paths: `{report.Summary.PathCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        AppendList(builder, "Coverage warnings", report.CoverageWarnings);
        builder.AppendLine();

        builder.AppendLine("## Query");
        builder.AppendLine();
        builder.AppendLine($"- From endpoint: `{report.Query.FromEndpoint ?? "default"}`");
        builder.AppendLine($"- From symbol: `{report.Query.FromSymbol ?? "n/a"}`");
        builder.AppendLine($"- From WebForms event: `{report.Query.FromWebFormsEvent ?? "n/a"}`");
        builder.AppendLine($"- From source: `{report.Query.FromSource ?? "n/a"}`");
        builder.AppendLine($"- To surface: `{report.Query.ToSurface ?? "all"}`");
        builder.AppendLine($"- Surface name: `{report.Query.SurfaceName ?? "n/a"}`");
        builder.AppendLine($"- Source pair: `{report.Query.SourcePair ?? "n/a"}`");
        builder.AppendLine($"- Classification: `{report.Query.Classification ?? "all"}`");
        builder.AppendLine($"- Bounds: depth `{report.Query.MaxDepth}`, paths `{report.Query.MaxPaths}`, frontier `{report.Query.MaxFrontier}`");
        builder.AppendLine();

        builder.AppendLine("## Sources");
        builder.AppendLine();
        AppendRows(builder, report.Sources, "| Label | Language | Repo | Commit | Analysis | Build |", "| --- | --- | --- | --- | --- | --- |",
            source => $"| {Cell(source.Label)} | {Cell(source.Language ?? "unknown")} | {Cell(source.RepoName)} | {Cell(source.CommitSha)} | {Cell(source.AnalysisLevel)} | {Cell(source.BuildStatus)} |");

        builder.AppendLine(legacyMode ? "## Representative Static Paths" : "## Paths");
        builder.AppendLine();
        if (report.Paths.Count == 0)
        {
            builder.AppendLine(legacyMode ? "No possible static paths found for this query." : "No dependency paths found for this query.");
            builder.AppendLine();
        }
        else
        {
            if (report.Paths.Count > MarkdownPathLimit)
            {
                builder.AppendLine($"Showing first {MarkdownPathLimit} of {report.Paths.Count} paths.");
                builder.AppendLine();
            }

            foreach (var path in report.Paths.Take(MarkdownPathLimit))
            {
                builder.AppendLine($"### {Cell(path.PathId)} `{path.Classification}` `{path.Confidence}`");
                builder.AppendLine();
                builder.AppendLine("| Hop | Source | Node | Evidence | Edge In |");
                builder.AppendLine("| --- | --- | --- | --- | --- |");
                for (var index = 0; index < path.Nodes.Count; index++)
                {
                    var node = path.Nodes[index];
                    var edge = index == 0 ? null : path.Edges[index - 1];
                    var boundary = index > 0 && path.Nodes[index - 1].SourceIndexId != node.SourceIndexId ? "source transition: " : string.Empty;
                    builder.AppendLine($"| {index} | {Cell(node.SourceLabel)} | {Cell($"{node.NodeKind} {node.DisplayName}")} | {Cell(Evidence(node.RuleId, node.EvidenceTier, node.FilePath, node.StartLine))} | {Cell(boundary + (edge?.EdgeKind ?? "start"))} |");
                }

                AppendPathNotes(builder, path.Notes);
                builder.AppendLine();
            }
        }

        builder.AppendLine(legacyMode ? "## Analysis Gaps" : "## Path Gaps");
        builder.AppendLine();
        AppendRows(builder, report.Gaps, "| Kind | Classification | Source | Message | Evidence |", "| --- | --- | --- | --- | --- |",
            gap => $"| {Cell(gap.GapKind)} | {Cell(gap.Classification)} | {Cell(gap.SourceLabel ?? "n/a")} | {Cell(gap.Message)} | {Cell(Evidence(gap))} |");

        builder.AppendLine("## Evidence Inventory");
        builder.AppendLine();
        AppendDictionary(builder, "Nodes by kind", report.Inventory.NodesByKind);
        AppendDictionary(builder, "Edges by kind", report.Inventory.EdgesByKind);
        AppendDictionary(builder, "Nodes by source", report.Inventory.NodesBySource);
        AppendDictionary(builder, "Surfaces by kind", report.Inventory.SurfacesByKind);
        AppendDictionary(builder, "Gaps by kind", report.Inventory.GapsByKind);
        builder.AppendLine();

        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations)
        {
            builder.AppendLine($"- {limitation}");
        }

        return builder.ToString();
    }

    private static void AppendRows<T>(StringBuilder builder, IReadOnlyList<T> rows, string header, string separator, Func<T, string> render)
    {
        if (rows.Count == 0)
        {
            builder.AppendLine("No evidence found.");
            builder.AppendLine();
            return;
        }

        if (rows.Count > MarkdownInventoryLimit)
        {
            builder.AppendLine($"Showing first {MarkdownInventoryLimit} of {rows.Count} rows. JSON contains all returned rows.");
            builder.AppendLine();
        }

        builder.AppendLine(header);
        builder.AppendLine(separator);
        foreach (var row in rows.Take(MarkdownInventoryLimit))
        {
            builder.AppendLine(render(row));
        }

        builder.AppendLine();
    }

    private static void AppendDictionary(StringBuilder builder, string title, IReadOnlyDictionary<string, int> counts)
    {
        if (counts.Count == 0)
        {
            return;
        }

        builder.AppendLine($"- {title}: {string.Join(", ", counts.Select(pair => $"`{Cell(pair.Key)}` {pair.Value}"))}");
    }

    private static void AppendList(StringBuilder builder, string title, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        builder.AppendLine($"- {title}:");
        foreach (var value in values)
        {
            builder.AppendLine($"  - {Cell(value)}");
        }
    }

    private static void AppendPathNotes(StringBuilder builder, IReadOnlyList<CombinedPathNote> notes)
    {
        if (notes.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Notes:");
        foreach (var note in notes.OrderBy(note => note.Code, StringComparer.Ordinal))
        {
            builder.AppendLine($"- `{Cell(note.Code)}`: {Cell(note.Message)}");
        }
    }

    private static string Evidence(string? ruleId, string? evidenceTier, string? filePath, int? startLine)
    {
        return $"{ruleId ?? "n/a"} {evidenceTier ?? "n/a"} {filePath ?? "n/a"}:{startLine ?? 0}";
    }

    private static string Evidence(CombinedPathGap gap)
    {
        var evidence = Evidence(gap.RuleId, gap.EvidenceTier, gap.FilePath, gap.StartLine);
        if (string.IsNullOrWhiteSpace(gap.CommitSha) && string.IsNullOrWhiteSpace(gap.ExtractorVersion) && string.IsNullOrWhiteSpace(gap.EvidenceScope))
        {
            return evidence;
        }

        return $"{evidence} commit:{gap.CommitSha ?? "n/a"} extractor:{gap.ExtractorVersion ?? "n/a"} scope:{gap.EvidenceScope ?? "n/a"}";
    }

    private static string Cell(string value)
    {
        return CombinedDependencyReporter.Cell(value)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> CountBy<T>(IEnumerable<T> rows, Func<T, string> selector)
    {
        return rows
            .GroupBy(selector, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static string Confidence(string classification)
    {
        return classification switch
        {
            CombinedDependencyPathClassifications.StrongStaticPath => "High",
            CombinedDependencyPathClassifications.ProbableStaticPath => "Medium",
            CombinedDependencyPathClassifications.NeedsReviewStaticPath => "Low",
            CombinedDependencyPathClassifications.NoBackendEvidence => "Low",
            CombinedDependencyPathClassifications.ReducedCoverage => "Low",
            CombinedDependencyPathClassifications.AnalysisGap => "Low",
            _ => "Low"
        };
    }

    private static int ClassificationRank(string classification)
    {
        return classification switch
        {
            CombinedDependencyPathClassifications.StrongStaticPath => 0,
            CombinedDependencyPathClassifications.ProbableStaticPath => 1,
            CombinedDependencyPathClassifications.NeedsReviewStaticPath => 2,
            CombinedDependencyPathClassifications.NeedsReviewPath => 2,
            CombinedDependencyPathClassifications.NoBackendEvidence => 3,
            CombinedDependencyPathClassifications.ReducedCoverage => 4,
            CombinedDependencyPathClassifications.AnalysisGap => 5,
            CombinedDependencyPathClassifications.UnknownAnalysisGap => 5,
            CombinedDependencyPathClassifications.NoPathFound => 6,
            CombinedDependencyPathClassifications.SelectorNoMatch => 7,
            CombinedDependencyPathClassifications.ClassificationFilterNoMatch => 8,
            _ => 99
        };
    }

    private static string NormalizeClassification(string value, bool legacyMode)
    {
        if (!legacyMode)
        {
            return value;
        }

        return value switch
        {
            CombinedDependencyPathClassifications.NeedsReviewPath => CombinedDependencyPathClassifications.NeedsReviewStaticPath,
            CombinedDependencyPathClassifications.UnknownAnalysisGap => CombinedDependencyPathClassifications.AnalysisGap,
            CombinedDependencyPathClassifications.NoPathFound => CombinedDependencyPathClassifications.NoBackendEvidence,
            "NoBackendEvidenceFound" => CombinedDependencyPathClassifications.NoBackendEvidence,
            _ => value
        };
    }

    private static bool IsLegacyView(string? view)
    {
        return string.Equals(view, LegacyFlowReportConstants.View, StringComparison.OrdinalIgnoreCase);
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

        if (scannerVersion.Contains("jvm", StringComparison.OrdinalIgnoreCase))
        {
            return "jvm";
        }

        if (scannerVersion.Contains("python", StringComparison.OrdinalIgnoreCase))
        {
            return "python";
        }

        if (scannerVersion.Contains("tracemap", StringComparison.OrdinalIgnoreCase))
        {
            return "csharp";
        }

        return null;
    }

    private static int EdgeRank(string edgeKind)
    {
        return edgeKind switch
        {
            "endpoint-match" => 0,
            "calls" => 1,
            "creates" => 2,
            "parameter-forward" => 3,
            "argument-passed" => 4,
            "surface-evidence" => 5,
            "remoting-evidence" => 5,
            "remoting-channel-link" => 5,
            "fact-attached-to-symbol" => 6,
            "symbol-reconciliation" => 7,
            "inherits" => 8,
            "implements" => 9,
            "overrides" => 10,
            _ => 99
        };
    }

    private static string NormalizeEdgeKind(string value)
    {
        var normalized = value.Trim().Replace("_", "-", StringComparison.Ordinal).Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "call" => "calls",
            "create" => "creates",
            "parameter-forwarded" => "parameter-forward",
            "argumentpassed" => "argument-passed",
            "parameterforwarded" => "parameter-forward",
            "factattachedtosymbol" => "fact-attached-to-symbol",
            "surfaceevidence" => "surface-evidence",
            _ => normalized
        };
    }

    private static string NormalizeFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "markdown" or "md" => "markdown",
            "json" => "json",
            _ => throw new ArgumentException("paths --format must be markdown or json.")
        };
    }

    private static string FactNodeId(string combinedFactId) => $"fact:{combinedFactId}";

    private static string SurfaceNodeId(string combinedFactId) => $"surface:{combinedFactId}";

    private static string SymbolNodeId(string sourceIndexId, string displayName)
    {
        return $"symbol:{sourceIndexId}:{Hash(NormalizeSymbol(displayName), 24)}";
    }

    private static string NormalizeSymbol(string value)
    {
        return value.Trim().ReplaceLineEndings(" ");
    }

    private static string SafePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "n/a";
        }

        return Path.IsPathFullyQualified(filePath)
            ? $"absolute-path-hash:{Hash(filePath, 16)}"
            : filePath.Replace('\\', '/');
    }

    private static string? SafeDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim().ReplaceLineEndings(" ");
        if (LooksUnsafe(trimmed))
        {
            return $"redacted-hash:{Hash(trimmed, 16)}";
        }

        return trimmed.Length > 160 ? $"{trimmed[..120]}...hash:{Hash(trimmed, 16)}" : trimmed;
    }

    private static string SafeSourceLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "single")
        {
            return string.IsNullOrWhiteSpace(value) ? "source" : value;
        }

        return LooksUnsafeSourceLabel(value) || value.Contains('/', StringComparison.Ordinal) || value.Contains('\\', StringComparison.Ordinal) || value.Contains(':', StringComparison.Ordinal)
            ? $"source:{Hash(value, 16)}"
            : value;
    }

    private static CombinedReportSource SanitizeSource(CombinedReportSource source)
    {
        return source with
        {
            Label = SafeSourceLabel(source.Label),
            RepoName = "source",
            RemoteUrl = null,
            ScanRootRelativePath = source.ScanRootRelativePath is null ? null : SafeDisplay(source.ScanRootRelativePath),
        };
    }

    private static bool LooksUnsafe(string value)
    {
        return Path.IsPathFullyQualified(value)
            || value.Contains("://", StringComparison.Ordinal)
            || value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("User ID=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("select ", StringComparison.OrdinalIgnoreCase)
            || value.Contains(" from ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("git@", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".git", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("private", StringComparison.OrdinalIgnoreCase)
            || value.Contains("internal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksUnsafeSourceLabel(string value)
    {
        return LooksUnsafe(value)
            || value.Contains("corp", StringComparison.OrdinalIgnoreCase)
            || value.Contains("customer", StringComparison.OrdinalIgnoreCase)
            || value.Contains("prod", StringComparison.OrdinalIgnoreCase)
            || value.Contains("client", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ReportLimitations(bool legacyMode, IReadOnlyList<CombinedPathNode> participatingNodes, IReadOnlyList<CombinedPathGap> gaps)
    {
        var limitations = new SortedSet<string>(CombinedDependencyReporter.Limitations, StringComparer.Ordinal);
        if (legacyMode && (participatingNodes.Any(node => node.NodeKind.StartsWith("remoting-", StringComparison.Ordinal))
            || gaps.Any(gap => string.Equals(gap.Reason, "legacy-remoting", StringComparison.Ordinal))))
        {
            limitations.Add("Static Remoting evidence is a boundary candidate only; it does not prove runtime channel setup, remote object activation, process boundary, object lifetime, deployment, endpoint reachability, exploitability, impact, or production usage.");
            limitations.Add("Remoting activation, registration, URL, object URI, channel, provider, and config values are displayed only through safe names or stable hashes.");
        }

        return limitations.ToArray();
    }

    private static bool IsGenericTerminalKey(string value)
    {
        var normalized = value.Trim().Trim('`', '"', '\'').ToLowerInvariant();
        return normalized is "status" or "id" or "name" or "value" or "result" or "response"
            || normalized.EndsWith(":status", StringComparison.Ordinal)
            || normalized.EndsWith(":id", StringComparison.Ordinal)
            || normalized.EndsWith(":name", StringComparison.Ordinal)
            || normalized.EndsWith(":value", StringComparison.Ordinal)
            || normalized.EndsWith(":result", StringComparison.Ordinal)
            || normalized.EndsWith(":response", StringComparison.Ordinal);
    }

    private static string Hash(string value, int length)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return text[..Math.Min(length, text.Length)];
    }

    private sealed class EvidenceGraph(IReadOnlyList<CombinedReportSource> sources)
    {
        public Dictionary<string, GraphNode> Nodes { get; } = new(StringComparer.Ordinal);
        public List<GraphEdge> Edges { get; } = [];
        public Dictionary<string, GraphEdge> EdgesById { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<GraphEdge>> Outgoing { get; } = new(StringComparer.Ordinal);
        public List<CombinedPathGap> Gaps { get; } = [];
        private readonly Dictionary<string, CombinedReportSource> sourcesById = sources.ToDictionary(source => source.SourceIndexId, StringComparer.Ordinal);

        public void AddNode(GraphNode node)
        {
            Nodes.TryAdd(node.NodeId, node);
        }

        public GraphNode GetOrAddSymbolNode(string sourceIndexId, string sourceLabel, string displayName, string filePath, int startLine, int endLine, string ruleId, string evidenceTier)
        {
            var nodeId = SymbolNodeId(sourceIndexId, displayName);
            if (Nodes.TryGetValue(nodeId, out var existing))
            {
                return existing;
            }

            sourcesById.TryGetValue(sourceIndexId, out var source);
            var node = new GraphNode(
                nodeId,
                displayName.Contains('(') ? "Method" : displayName.Contains('.') ? "Type" : "Symbol",
                displayName,
                sourceIndexId,
                sourceLabel,
                source?.ScanId,
                source?.CommitSha,
                displayName,
                null,
                ruleId,
                evidenceTier,
                SafePath(filePath),
                startLine,
                endLine,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            Nodes[nodeId] = node;
            return node;
        }

        public void AddEdge(GraphEdge edge)
        {
            if (EdgesById.ContainsKey(edge.EdgeId))
            {
                return;
            }

            if (!Nodes.ContainsKey(edge.FromNodeId) || !Nodes.ContainsKey(edge.ToNodeId))
            {
                return;
            }

            Edges.Add(edge);
            EdgesById[edge.EdgeId] = edge;
            if (!Outgoing.TryGetValue(edge.FromNodeId, out var list))
            {
                list = [];
                Outgoing[edge.FromNodeId] = list;
            }

            list.Add(edge);
        }

        public void Sort()
        {
            Edges.Sort(CompareEdges);
            foreach (var pair in Outgoing)
            {
                pair.Value.Sort(CompareEdges);
            }
        }

        private int CompareEdges(GraphEdge left, GraphEdge right)
        {
            var rank = EdgeRank(left.EdgeKind).CompareTo(EdgeRank(right.EdgeKind));
            if (rank != 0)
            {
                return rank;
            }

            var leftDisplayName = Nodes.TryGetValue(left.ToNodeId, out var leftNode) ? leftNode.DisplayName : string.Empty;
            var rightDisplayName = Nodes.TryGetValue(right.ToNodeId, out var rightNode) ? rightNode.DisplayName : string.Empty;
            var toName = string.Compare(leftDisplayName, rightDisplayName, StringComparison.Ordinal);
            if (toName != 0)
            {
                return toName;
            }

            var file = string.Compare(left.FilePath, right.FilePath, StringComparison.Ordinal);
            if (file != 0)
            {
                return file;
            }

            var line = (left.StartLine ?? 0).CompareTo(right.StartLine ?? 0);
            return line != 0 ? line : string.Compare(left.EdgeId, right.EdgeId, StringComparison.Ordinal);
        }
    }

    private sealed record SearchResult(IReadOnlyList<CombinedPath> Paths, IReadOnlyList<CombinedPathGap> Gaps, bool Truncated);

    private sealed record PathState(IReadOnlyList<string> NodeIds, IReadOnlyList<string> EdgeIds);

    private sealed record SelectorResolution(IReadOnlyList<GraphNode> Nodes, int TotalMatchCount);

    private sealed record SymbolAlias(string MemberKey, string? TypeKey, string? SignatureKey);

    private sealed record GraphNode(
        string NodeId,
        string NodeKind,
        string DisplayName,
        string SourceIndexId,
        string SourceLabel,
        string? ScanId,
        string? CommitSha,
        string? SymbolId,
        string? CombinedFactId,
        string? RuleId,
        string? EvidenceTier,
        string? FilePath,
        int? StartLine,
        int? EndLine,
        string? SurfaceKind,
        string? SurfaceName,
        string? HttpMethod,
        string? NormalizedPathKey,
        string? OperationName,
        string? TableName,
        string? ColumnNames,
        string? SourceKind,
        string? ShapeHash,
        string? TextHash,
        string? TextLength,
        string? PackageName,
        string? ConfigKey)
    {
        public CombinedPathNode ToReportNode()
        {
            return new CombinedPathNode(
                NodeId,
                NodeKind,
                DisplayName,
                SourceIndexId,
                SourceLabel,
                ScanId,
                CommitSha,
                SymbolId,
                CombinedFactId,
                RuleId,
                EvidenceTier,
                FilePath,
                StartLine,
                EndLine,
                SurfaceKind,
                SurfaceName,
                HttpMethod,
                NormalizedPathKey,
                OperationName,
                TableName,
                ColumnNames,
                SourceKind,
                ShapeHash,
                TextHash,
                TextLength,
                PackageName,
                ConfigKey);
        }
    }

    private sealed record GraphEdge(
        string EdgeId,
        string EdgeKind,
        string FromNodeId,
        string ToNodeId,
        string Classification,
        string RuleId,
        string EvidenceTier,
        IReadOnlyList<string> SupportingFactIds,
        IReadOnlyList<string> SupportingCombinedEdgeIds,
        string? FilePath,
        int? StartLine,
        int? EndLine)
    {
        public CombinedPathEdge ToReportEdge()
        {
            return new CombinedPathEdge(
                EdgeId,
                EdgeKind,
                FromNodeId,
                ToNodeId,
                Classification,
                RuleId,
                EvidenceTier,
                SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                SupportingCombinedEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                FilePath,
                StartLine,
                EndLine);
        }
    }
}
