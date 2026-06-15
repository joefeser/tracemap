using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    string? ToSurface = null,
    string? SurfaceName = null,
    string? SourcePair = null,
    int MaxDepth = 8,
    int MaxPaths = 100,
    int MaxFrontier = 10000);

public sealed record CombinedDependencyPathResult(
    CombinedDependencyPathReport Report,
    string? MarkdownPath,
    string? JsonPath);

public sealed record CombinedDependencyPathReport(
    string Version,
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
    string? ToSurface,
    string? SurfaceName,
    string? SourcePair,
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
    string? Reason);

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
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string NoPathFound = nameof(NoPathFound);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
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

    private static readonly HashSet<string> TerminalSurfaceKinds = new(StringComparer.Ordinal)
    {
        "sql-query",
        "sql-persistence",
        "http-route",
        "http-client",
        "package-config"
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
        "symbol-reconciliation"
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
        var (read, graph) = await BuildGraphAsync(options.IndexPath, sourcePair, cancellationToken);
        return BuildReport(options, read, graph, sourcePair);
    }

    internal static async Task<CombinedPathGraphInventory> BuildGraphInventoryAsync(
        string indexPath,
        string? sourcePair = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(indexPath))
        {
            throw new ArgumentException("paths requires --index <combined.sqlite>.");
        }

        var parsedSourcePair = ParseSourcePair(sourcePair);
        var (read, graph) = await BuildGraphAsync(indexPath, parsedSourcePair, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = indexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ValidatePathSchemaAsync(connection, cancellationToken);

        var read = await CombinedDependencyReporter.ReadAsync(connection, cancellationToken);
        var endpointFindings = CombinedDependencyReporter.MatchEndpoints(read.Sources, read.Facts);
        var surfaces = CombinedDependencyReporter.BuildSurfaces(read.Facts);
        var graph = BuildGraph(read, endpointFindings, surfaces, sourcePair);
        return (read, graph);
    }

    private static void ValidateOptions(CombinedDependencyPathOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("paths requires --index <combined.sqlite>.");
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
                throw new ArgumentException("paths --to-surface must be one of sql-query, sql-persistence, http-route, http-client, or package-config.");
            }
        }
    }

    private static CombinedDependencyPathReport BuildReport(
        CombinedDependencyPathOptions options,
        CombinedReadResult read,
        EvidenceGraph graph,
        (string Client, string Server)? sourcePair)
    {
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
            var search = Search(graph, startNodes, terminalNodes, options.MaxDepth, options.MaxPaths, options.MaxFrontier);
            paths.AddRange(search.Paths);
            gaps.AddRange(search.Gaps);
            truncated = truncated || search.Truncated;

            if (paths.Count == 0)
            {
                gaps.Add(CreateNoPathGap(read, graph, startNodes, options.MaxDepth, options.MaxFrontier));
            }
        }

        var sortedPaths = paths
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
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
            .ThenBy(gap => gap.FilePath, StringComparer.Ordinal)
            .ThenBy(gap => gap.StartLine ?? 0)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        var warnings = read.CoverageWarnings
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var participatingNodes = sortedPaths.SelectMany(path => path.Nodes)
            .Concat(sortedGaps.Select(gap => gap.NodeId is not null && graph.Nodes.TryGetValue(gap.NodeId, out var node) ? node.ToReportNode() : null).OfType<CombinedPathNode>())
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
            warnings.Length == 0 ? "FullEvidenceAvailable" : "ReducedCoverage",
            warnings,
            new CombinedPathQuery(
                options.FromEndpoint,
                options.FromSymbol,
                sourceFilter,
                options.ToSurface,
                options.SurfaceName,
                sourcePair is null ? null : $"{EscapeSourcePairLabel(sourcePair.Value.Client)}:{EscapeSourcePairLabel(sourcePair.Value.Server)}",
                options.MaxDepth,
                options.MaxPaths,
                options.MaxFrontier,
                Algorithm,
                AlgorithmVersion),
            read.Sources.OrderBy(source => source.Label, StringComparer.Ordinal).ThenBy(source => source.SourceIndexId, StringComparer.Ordinal).ToArray(),
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
            CombinedDependencyReporter.Limitations);
    }

    private static EvidenceGraph BuildGraph(
        CombinedReadResult read,
        IReadOnlyList<CombinedEndpointFinding> endpointFindings,
        IReadOnlyList<CombinedDependencySurfaceRow> surfaces,
        (string Client, string Server)? sourcePair)
    {
        var graph = new EvidenceGraph(read.Sources);
        var factsById = read.Facts.ToDictionary(fact => fact.CombinedFactId, StringComparer.Ordinal);
        foreach (var fact in read.Facts.OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            if (fact.FactType is FactTypes.HttpCallDetected or FactTypes.HttpRouteBinding)
            {
                graph.AddNode(ToEndpointNode(fact));
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
            || edges.Any(edge => edge.Classification == CombinedDependencyPathClassifications.UnknownAnalysisGap))
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
        else if (!string.IsNullOrWhiteSpace(options.FromSymbol))
        {
            var selector = options.FromSymbol.Trim();
            candidates = graph.Nodes.Values.Where(node =>
                node.NodeKind is "Symbol" or "Method" or "Type"
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
            candidates = graph.Nodes.Values.Where(node => matchedClientIds.Contains(node.NodeId));
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
        return new SelectorResolution(ordered.Take(250).ToArray(), ordered.Length);
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

        return node.SurfaceKind != "http-client"
            || node.CombinedFactId is null
            || !startFactIds.Contains(node.CombinedFactId);
    }

    private static bool SurfaceNameMatches(GraphNode node, string selector)
    {
        var values = new[]
        {
            node.SurfaceName ?? node.DisplayName,
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

    private static CombinedPathGap CreateNoPathGap(CombinedReadResult read, EvidenceGraph graph, IReadOnlyList<GraphNode> starts, int maxDepth, int maxFrontier)
    {
        var sourceIds = ReachableSourceIds(graph, starts, maxDepth, maxFrontier);
        var reducedSource = read.Sources
            .Where(source => sourceIds.Contains(source.SourceIndexId))
            .FirstOrDefault(SourceHasReducedCoverage);
        if (reducedSource is not null)
        {
            return new CombinedPathGap(
                $"gap:no-path:coverage:{reducedSource.SourceIndexId}",
                "UnknownAnalysisGap",
                CombinedDependencyPathClassifications.UnknownAnalysisGap,
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
            "NoPathFound",
            CombinedDependencyPathClassifications.NoPathFound,
            "Selectors matched starting evidence, but no path reached a terminal dependency surface within the current graph and bounds.",
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

    private static bool IsDependencySurfaceFact(CombinedFactRow fact)
    {
        if (fact.Properties.TryGetValue("surfaceKind", out var surfaceKind) && !string.IsNullOrWhiteSpace(surfaceKind))
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
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Paths Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("- Paths are static evidence trails, not runtime execution traces.");
        builder.AppendLine($"- Report coverage: `{report.ReportCoverage}`");
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
        builder.AppendLine($"- From source: `{report.Query.FromSource ?? "n/a"}`");
        builder.AppendLine($"- To surface: `{report.Query.ToSurface ?? "all"}`");
        builder.AppendLine($"- Surface name: `{report.Query.SurfaceName ?? "n/a"}`");
        builder.AppendLine($"- Source pair: `{report.Query.SourcePair ?? "n/a"}`");
        builder.AppendLine($"- Bounds: depth `{report.Query.MaxDepth}`, paths `{report.Query.MaxPaths}`, frontier `{report.Query.MaxFrontier}`");
        builder.AppendLine();

        builder.AppendLine("## Sources");
        builder.AppendLine();
        AppendRows(builder, report.Sources, "| Label | Language | Repo | Commit | Analysis | Build |", "| --- | --- | --- | --- | --- | --- |",
            source => $"| {Cell(source.Label)} | {Cell(source.Language ?? "unknown")} | {Cell(source.RepoName)} | {Cell(source.CommitSha)} | {Cell(source.AnalysisLevel)} | {Cell(source.BuildStatus)} |");

        builder.AppendLine("## Paths");
        builder.AppendLine();
        if (report.Paths.Count == 0)
        {
            builder.AppendLine("No dependency paths found for this query.");
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

        builder.AppendLine("## Path Gaps");
        builder.AppendLine();
        AppendRows(builder, report.Gaps, "| Kind | Classification | Source | Message | Evidence |", "| --- | --- | --- | --- | --- |",
            gap => $"| {Cell(gap.GapKind)} | {Cell(gap.Classification)} | {Cell(gap.SourceLabel ?? "n/a")} | {Cell(gap.Message)} | {Cell(Evidence(gap.RuleId, gap.EvidenceTier, gap.FilePath, gap.StartLine))} |");

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
            _ => "Low"
        };
    }

    private static int ClassificationRank(string classification)
    {
        return classification switch
        {
            CombinedDependencyPathClassifications.StrongStaticPath => 0,
            CombinedDependencyPathClassifications.ProbableStaticPath => 1,
            CombinedDependencyPathClassifications.NeedsReviewPath => 2,
            CombinedDependencyPathClassifications.UnknownAnalysisGap => 3,
            CombinedDependencyPathClassifications.NoPathFound => 4,
            CombinedDependencyPathClassifications.SelectorNoMatch => 5,
            _ => 99
        };
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
