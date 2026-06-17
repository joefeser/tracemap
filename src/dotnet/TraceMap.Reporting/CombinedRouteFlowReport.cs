using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record CombinedRouteFlowOptions(
    string IndexPath,
    string OutputPath,
    string Format = "markdown",
    string? Route = null,
    string? ClientCall = null,
    string? FromEndpoint = null,
    string? FromWebFormsEvent = null,
    string? FromSymbol = null,
    string? FromSource = null,
    string? ToSurface = null,
    string? SurfaceName = null,
    string? Classification = null,
    int MaxDepth = 8,
    int MaxPaths = 100,
    int MaxFrontier = 10000,
    int MaxLogicRows = 200,
    int MaxGaps = 1000,
    bool ExitCode = false);

public sealed record CombinedRouteFlowResult(
    RouteFlowReport Report,
    string? MarkdownPath,
    string? JsonPath)
{
    public bool ExitCodeWouldBeNonZero => Report.Summary.ExitCodeWouldBeNonZero;
}

public sealed record RouteFlowReport(
    string ReportType,
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    RouteFlowQuery Query,
    RouteFlowSnapshot Snapshot,
    RouteFlowSummary Summary,
    IReadOnlyList<RouteFlowEntryEvidence> EntryEvidence,
    IReadOnlyList<RouteFlowRow> FlowRows,
    IReadOnlyList<RouteFlowLogicRow> LogicRows,
    IReadOnlyList<RouteFlowDependencySurface> DependencySurfaces,
    IReadOnlyList<RouteFlowGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record RouteFlowQuery(
    string IndexPath,
    string OutputPath,
    string Format,
    string? Route,
    string? ClientCall,
    string? FromEndpoint,
    string? FromWebFormsEvent,
    string? FromSymbol,
    string? FromSource,
    string? ToSurface,
    string? SurfaceName,
    string? Classification,
    string RouteMatchMode,
    int MaxDepth,
    int MaxPaths,
    int MaxFrontier,
    int MaxLogicRows,
    int MaxGaps,
    bool ExitCode);

public sealed record RouteFlowSnapshot(
    string IndexKind,
    int SourceCount,
    IReadOnlyList<RouteFlowSource> Sources);

public sealed record RouteFlowSource(
    string SourceLabel,
    string? SourceIndexId,
    string? ScanId,
    string? CommitSha,
    string Language,
    string AnalysisLevel,
    string BuildStatus,
    bool IdentityVerified,
    IReadOnlyList<string> CoverageWarnings);

public sealed record RouteFlowSummary(
    string Classification,
    string ReportCoverage,
    int EntryEvidenceCount,
    int FlowRowCount,
    int LogicRowCount,
    int DependencySurfaceCount,
    int GapCount,
    bool HasBlockingGaps,
    bool Truncated,
    bool ExitCodeWouldBeNonZero,
    IReadOnlyList<string> ClassificationReasons);

public sealed record RouteFlowEvidenceRef(
    string RuleId,
    string EvidenceTier,
    string SourceLabel,
    string? CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? ExtractorName,
    string? ExtractorVersion,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> SupportingRuleIds,
    IReadOnlyList<string> Limitations);

public sealed record RouteFlowEntryEvidence(
    string EntryId,
    string EntryKind,
    string Method,
    string NormalizedPathTemplate,
    string NormalizedPathKey,
    string? DisplaySymbol,
    string Classification,
    string Coverage,
    RouteFlowEvidenceRef Evidence);

public sealed record RouteFlowRow(
    string RowId,
    int Sequence,
    string RowKind,
    string EdgeKind,
    string SourceSymbol,
    string? TargetSymbol,
    string Classification,
    string Coverage,
    string? FromNodeId,
    string? ToNodeId,
    RouteFlowEvidenceRef Evidence);

public sealed record RouteFlowLogicRow(
    string LogicRowId,
    string LogicKind,
    string DisplayName,
    string AttachmentKind,
    string? AttachedFlowRowId,
    string Classification,
    string Coverage,
    IReadOnlyDictionary<string, string> SafeMetadata,
    RouteFlowEvidenceRef Evidence);

public sealed record RouteFlowDependencySurface(
    string SurfaceId,
    string SurfaceKind,
    string DisplayName,
    string StableKey,
    string Classification,
    string Coverage,
    IReadOnlyDictionary<string, string> SafeMetadata,
    RouteFlowEvidenceRef Evidence);

public sealed record RouteFlowGap(
    string GapId,
    string GapKind,
    string Message,
    string RuleId,
    string EvidenceTier,
    string Coverage,
    string? SourceLabel,
    string? AffectedRowId,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> Limitations);

public static class RouteFlowClassifications
{
    public const string StrongStaticRouteFlow = nameof(StrongStaticRouteFlow);
    public const string ProbableStaticRouteFlow = nameof(ProbableStaticRouteFlow);
    public const string NeedsReviewStaticRouteFlow = nameof(NeedsReviewStaticRouteFlow);
    public const string NoRouteFlowEvidence = nameof(NoRouteFlowEvidence);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
}

public static class CombinedRouteFlowReporter
{
    private const string ReportType = "route-flow";
    private const string Version = "1.0";
    private const string SelectorRuleId = "combined.route-flow.selector.v1";
    private const string EntryRuleId = "combined.route-flow.entry.v1";
    private const string PathRuleId = "combined.route-flow.path.v1";
    private const string InterfaceBridgeRuleId = "combined.route-flow.interface-bridge.v1";
    private const string LogicSurfaceRuleId = "combined.route-flow.logic-surface.v1";
    private const string DependencySurfaceRuleId = "combined.route-flow.dependency-surface.v1";
    private const string ClassificationRuleId = "combined.route-flow.classification.v1";
    private const string GapRuleId = "combined.route-flow.gap.v1";
    private const int MarkdownRowLimit = 200;

    private static readonly HashSet<string> AllowedClassifications = new(StringComparer.Ordinal)
    {
        RouteFlowClassifications.StrongStaticRouteFlow,
        RouteFlowClassifications.ProbableStaticRouteFlow,
        RouteFlowClassifications.NeedsReviewStaticRouteFlow,
        RouteFlowClassifications.NoRouteFlowEvidence,
        RouteFlowClassifications.UnknownAnalysisGap
    };

    private static readonly HashSet<string> SurfaceKinds = new(StringComparer.Ordinal)
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
        "dependency-surface"
    };

    public static async Task<CombinedRouteFlowResult> WriteAsync(CombinedRouteFlowOptions options, CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(options, cancellationToken);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "route-flow");
        var (markdownPath, jsonPath) = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "route-flow-report.md",
            "route-flow-report.json",
            report,
            RenderMarkdown,
            CombinedDependencyReporter.JsonOptions,
            cancellationToken);
        return new CombinedRouteFlowResult(report, markdownPath, jsonPath);
    }

    public static async Task<RouteFlowReport> BuildReportAsync(CombinedRouteFlowOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "route-flow");
        await ValidateCombinedOnlyAsync(options.IndexPath, cancellationToken);

        var routeSelector = NormalizeEndpointSelector(options.Route);
        var clientSelector = NormalizeEndpointSelector(options.ClientCall);
        var endpointSelector = NormalizeEndpointSelector(options.FromEndpoint);
        var pathEndpointSelector = routeSelector ?? clientSelector ?? endpointSelector;
        var pathReport = await CombinedDependencyPathReporter.BuildReportAsync(new CombinedDependencyPathOptions(
            options.IndexPath,
            options.OutputPath,
            "json",
            FromEndpoint: pathEndpointSelector,
            FromSymbol: options.FromSymbol,
            FromSource: options.FromSource,
            FromWebFormsEvent: options.FromWebFormsEvent,
            ToSurface: options.ToSurface,
            SurfaceName: options.SurfaceName,
            MaxDepth: options.MaxDepth,
            MaxPaths: options.MaxPaths,
            MaxFrontier: options.MaxFrontier), cancellationToken);
        var selectedPaths = FilterPathsForSelectorSide(pathReport.Paths, routeSelector, clientSelector).ToArray();
        var inventory = await CombinedDependencyPathReporter.BuildGraphInventoryAsync(options.IndexPath, cancellationToken: cancellationToken);

        var schemaGaps = await ReadRouteFlowSchemaGapsAsync(options.IndexPath, cancellationToken);
        var sources = ToSources(inventory.Sources, inventory.CoverageWarnings);
        var query = new RouteFlowQuery(
            CombinedReportHelpers.SafePath(options.IndexPath),
            CombinedReportHelpers.SafePath(options.OutputPath),
            format,
            routeSelector,
            clientSelector,
            endpointSelector,
            SafeSelector(options.FromWebFormsEvent),
            SafeSelector(options.FromSymbol),
            SafeSelector(options.FromSource),
            SafeSelector(options.ToSurface),
            SafeSelector(options.SurfaceName),
            SafeSelector(options.Classification),
            RouteMatchMode(routeSelector, clientSelector, endpointSelector, options.FromSymbol, options.FromSource),
            options.MaxDepth,
            options.MaxPaths,
            options.MaxFrontier,
            options.MaxLogicRows,
            options.MaxGaps,
            options.ExitCode);

        var entryEvidence = SelectEntryEvidence(options, routeSelector, clientSelector, endpointSelector, inventory.Nodes)
            .OrderBy(row => row.EntryKind, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.NormalizedPathKey, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.StartLine ?? 0)
            .ThenBy(row => row.EntryId, StringComparer.Ordinal)
            .ToArray();

        var sourceIdentityGaps = SourceIdentityGaps(sources).ToList();
        var gaps = pathReport.Gaps.Select(FromPathGap)
            .Concat(schemaGaps)
            .Concat(sourceIdentityGaps)
            .ToList();
        if (entryEvidence.Length == 0)
        {
            gaps.Add(new RouteFlowGap(
                "gap:selector:no-entry",
                "SelectorNoMatch",
                "No route-flow entry evidence matched the selector under available combined-index evidence.",
                SelectorRuleId,
                EvidenceTiers.Tier4Unknown,
                "ReducedCoverage",
                SafeSelector(options.FromSource),
                null,
                [],
                ["Selector matching is static and coverage-relative."]));
        }

        var flowRows = BuildFlowRows(selectedPaths, sources, gaps)
            .OrderBy(row => row.Sequence)
            .ThenBy(row => row.Evidence.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.StartLine ?? 0)
            .ThenBy(row => row.RowId, StringComparer.Ordinal)
            .ToList();
        var dependencySurfaces = BuildDependencySurfaces(selectedPaths, sources)
            .OrderBy(surface => surface.SurfaceKind, StringComparer.Ordinal)
            .ThenBy(surface => surface.StableKey, StringComparer.Ordinal)
            .ThenBy(surface => surface.SurfaceId, StringComparer.Ordinal)
            .ToList();
        var allLogicRows = BuildLogicRows(selectedPaths, flowRows, sources)
            .OrderBy(row => row.LogicKind, StringComparer.Ordinal)
            .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
            .ThenBy(row => row.LogicRowId, StringComparer.Ordinal)
            .ToList();
        var logicRows = allLogicRows.Take(options.MaxLogicRows).ToList();
        if (allLogicRows.Count > options.MaxLogicRows)
        {
            gaps.Add(new RouteFlowGap(
                $"gap:truncated:logic:{options.MaxLogicRows}",
                "TruncatedByLimit",
                "Business/data logic rows were truncated by --max-logic-rows.",
                GapRuleId,
                EvidenceTiers.Tier4Unknown,
                "ReducedCoverage",
                null,
                null,
                [],
                ["Truncated output is partial."]));
        }

        if (entryEvidence.Length > 0 && flowRows.Count == 0 && dependencySurfaces.Count == 0)
        {
            gaps.Add(new RouteFlowGap(
                "gap:no-route-flow-evidence",
                "NoRouteFlowEvidence",
                "Entry evidence matched, but no downstream route-flow path or terminal surface was found under available coverage.",
                ClassificationRuleId,
                EvidenceTiers.Tier4Unknown,
                pathReport.ReportCoverage == "FullEvidenceAvailable" && sourceIdentityGaps.Count == 0 ? "FullEvidenceAvailable" : "ReducedCoverage",
                null,
                null,
                entryEvidence.SelectMany(entry => entry.Evidence.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ["No downstream static evidence is coverage-relative and is not proof of runtime absence."]));
        }

        var sortedGaps = gaps
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToList();
        var truncatedByGapCap = sortedGaps.Count > options.MaxGaps;
        gaps = sortedGaps.Take(options.MaxGaps).ToList();
        if (truncatedByGapCap && gaps.All(gap => gap.GapKind != "TruncatedByLimit"))
        {
            gaps[^1] = new RouteFlowGap(
                $"gap:truncated:gaps:{options.MaxGaps}",
                "TruncatedByLimit",
                "Gap rows were truncated by --max-gaps.",
                GapRuleId,
                EvidenceTiers.Tier4Unknown,
                "ReducedCoverage",
                null,
                null,
                [],
                ["Truncated output is partial."]);
        }

        ApplyClassificationFilter(options.Classification, flowRows, logicRows, dependencySurfaces, gaps);

        var coverageWarnings = pathReport.CoverageWarnings
            .Concat(sources.SelectMany(source => source.CoverageWarnings))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var reportCoverage = coverageWarnings.Length == 0 && gaps.All(gap => !IsBlockingGap(gap)) ? "FullEvidenceAvailable" : "ReducedCoverage";
        var summary = BuildSummary(reportCoverage, entryEvidence, flowRows, logicRows, dependencySurfaces, gaps, pathReport.Summary.Truncated || gaps.Any(gap => gap.GapKind == "TruncatedByLimit"));

        return new RouteFlowReport(
            ReportType,
            Version,
            summary.ReportCoverage,
            coverageWarnings,
            query,
            new RouteFlowSnapshot("combined", sources.Count, sources),
            summary,
            entryEvidence,
            flowRows,
            logicRows,
            dependencySurfaces,
            gaps,
            Limitations());
    }

    private static void ValidateOptions(CombinedRouteFlowOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("route-flow requires --index <combined.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("route-flow requires --out <path>.");
        }

        var selectorCount = new[] { options.Route, options.ClientCall, options.FromEndpoint, options.FromWebFormsEvent, options.FromSymbol, options.FromSource }
            .Count(value => !string.IsNullOrWhiteSpace(value));
        if (selectorCount == 0)
        {
            throw new ArgumentException("route-flow requires one selector: --route, --client-call, --from-endpoint, --from-webforms-event, --from-symbol, or --from-source.");
        }

        if (!string.IsNullOrWhiteSpace(options.ToSurface) && !SurfaceKinds.Contains(options.ToSurface.Trim()))
        {
            throw new ArgumentException("route-flow --to-surface must be one of sql-query, sql-persistence, http-route, http-client, package-config, wcf-operation, remoting-endpoint, remoting-registration, remoting-channel, remoting-object, remoting-api, legacy-data, or dependency-surface.");
        }

        if (!string.IsNullOrWhiteSpace(options.Classification) && !AllowedClassifications.Contains(options.Classification.Trim()))
        {
            throw new ArgumentException("route-flow --classification must be one of StrongStaticRouteFlow, ProbableStaticRouteFlow, NeedsReviewStaticRouteFlow, NoRouteFlowEvidence, or UnknownAnalysisGap.");
        }

        if (options.MaxDepth <= 0 || options.MaxPaths <= 0 || options.MaxFrontier <= 0 || options.MaxLogicRows <= 0 || options.MaxGaps <= 0)
        {
            throw new ArgumentException("route-flow caps must be positive integers.");
        }
    }

    private static async Task ValidateCombinedOnlyAsync(string indexPath, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = indexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        if (!await TableExistsAsync(connection, "index_sources", cancellationToken)
            || !await TableExistsAsync(connection, "combined_facts", cancellationToken)
            || !await ViewExistsAsync(connection, "combined_dependency_edges", cancellationToken))
        {
            throw new InvalidDataException("tracemap route-flow requires a combined index produced by tracemap combine; single-language indexes are partial inputs and are rejected.");
        }

        await CombinedDependencyReporter.ValidateCombinedIndexAsync(connection, cancellationToken);
    }

    private static async Task<IReadOnlyList<RouteFlowGap>> ReadRouteFlowSchemaGapsAsync(string indexPath, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = indexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var gaps = new List<RouteFlowGap>();
        foreach (var table in new[] { "combined_fact_symbols", "combined_argument_flows", "combined_symbol_relationships" })
        {
            if (!await TableExistsAsync(connection, table, cancellationToken))
            {
                gaps.Add(new RouteFlowGap(
                    $"gap:schema:{table}",
                    "SchemaMissing",
                    $"Combined schema table `{table}` is unavailable; route-flow preserves available path evidence and marks this report partial.",
                    GapRuleId,
                    EvidenceTiers.Tier4Unknown,
                    "ReducedCoverage",
                    null,
                    null,
                    [],
                    ["Missing optional route-flow detail tables cap clean absence conclusions."]));
            }
        }

        foreach (var table in new[] { "combined_fact_symbols", "combined_argument_flows" })
        {
            if (await TableExistsAsync(connection, table, cancellationToken)
                && await CountRowsAsync(connection, table, cancellationToken) > 0)
            {
                gaps.Add(new RouteFlowGap(
                    $"gap:extractor-unavailable:{table}",
                    "ExtractorUnavailable",
                    $"Route-flow v1 detected `{table}` evidence but does not yet project the table's detail rows directly; report rows remain limited to shared combined path graph evidence.",
                    GapRuleId,
                    EvidenceTiers.Tier4Unknown,
                    "ReducedCoverage",
                    null,
                    null,
                    [],
                    ["Present-but-unprojected route-flow detail tables cap clean absence conclusions."]));
            }
        }

        return gaps;
    }

    private static IReadOnlyList<RouteFlowSource> ToSources(IReadOnlyList<CombinedReportSource> sources, IReadOnlyList<string> warnings)
    {
        return sources
            .OrderBy(source => source.Label, StringComparer.Ordinal)
            .ThenBy(source => source.SourceIndexId, StringComparer.Ordinal)
            .Select(source =>
            {
                var sourceWarnings = warnings
                    .Where(warning => warning.Contains(source.Label, StringComparison.OrdinalIgnoreCase))
                    .Select(SafeSelector)
                    .OfType<string>()
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                return new RouteFlowSource(
                    SafeLabel(source.Label),
                    source.SourceIndexId,
                    source.ScanId,
                    KnownCommit(source.CommitSha) ? source.CommitSha : null,
                    source.Language ?? "unknown",
                    source.AnalysisLevel,
                    source.BuildStatus,
                    IdentityVerified(source),
                    sourceWarnings);
            })
            .ToArray();
    }

    private static IReadOnlyList<RouteFlowEntryEvidence> SelectEntryEvidence(
        CombinedRouteFlowOptions options,
        string? routeSelector,
        string? clientSelector,
        string? endpointSelector,
        IReadOnlyList<CombinedPathNode> nodes)
    {
        var rows = new List<RouteFlowEntryEvidence>();
        if (routeSelector is not null)
        {
            rows.AddRange(EndpointEntries(nodes, routeSelector, "EndpointRoute", "route-root"));
        }

        if (clientSelector is not null)
        {
            rows.AddRange(EndpointEntries(nodes, clientSelector, "EndpointClient", "client-call-root"));
        }

        if (endpointSelector is not null)
        {
            rows.AddRange(EndpointEntries(nodes, endpointSelector, null, "endpoint-root"));
        }

        if (!string.IsNullOrWhiteSpace(options.FromWebFormsEvent))
        {
            rows.AddRange(nodes
                .Where(node => node.NodeKind is "webforms-event" or "webforms-lifecycle" && NodeMatches(node, options.FromWebFormsEvent!))
                .Select(node => EntryFromNode(node, "webforms-event-root")));
        }

        if (!string.IsNullOrWhiteSpace(options.FromSymbol))
        {
            rows.AddRange(nodes
                .Where(node => node.NodeKind is "Symbol" or "Method" or "Type" or "EndpointRoute" or "EndpointClient" && NodeMatches(node, options.FromSymbol!))
                .Select(node => EntryFromNode(node, "symbol-root")));
        }

        if (!string.IsNullOrWhiteSpace(options.FromSource))
        {
            rows.AddRange(nodes
                .Where(node => string.Equals(node.SourceLabel, options.FromSource, StringComparison.OrdinalIgnoreCase))
                .Select(node => EntryFromNode(node, "source-root")));
        }

        var aligned = AlignedEntry(nodes, routeSelector ?? clientSelector ?? endpointSelector);
        if (aligned is not null)
        {
            rows.Add(aligned);
        }

        return rows
            .GroupBy(row => $"{row.EntryKind}\0{row.EntryId}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static IEnumerable<CombinedPath> FilterPathsForSelectorSide(IReadOnlyList<CombinedPath> paths, string? routeSelector, string? clientSelector)
    {
        if (routeSelector is null && clientSelector is null)
        {
            return paths;
        }

        var requiredNodeKind = clientSelector is not null ? "EndpointClient" : "EndpointRoute";
        return paths.Where(path => string.Equals(path.Nodes.FirstOrDefault()?.NodeKind, requiredNodeKind, StringComparison.Ordinal));
    }

    private static RouteFlowEntryEvidence? AlignedEntry(IReadOnlyList<CombinedPathNode> nodes, string? selector)
    {
        if (selector is null)
        {
            return null;
        }

        var parsed = ParseNormalizedEndpoint(selector);
        var matching = nodes
            .Where(node => node.NodeKind is "EndpointRoute" or "EndpointClient")
            .Where(node => string.Equals(node.NormalizedPathKey, parsed.PathKey, StringComparison.Ordinal)
                && CombinedDependencyReporter.MethodsCompatible(parsed.Method, node.HttpMethod ?? "ANY"))
            .OrderBy(node => node.SourceLabel, StringComparer.Ordinal)
            .ThenBy(node => node.NodeKind, StringComparer.Ordinal)
            .ThenBy(node => node.FilePath, StringComparer.Ordinal)
            .ThenBy(node => node.StartLine ?? 0)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        if (!matching.Any(node => node.NodeKind == "EndpointRoute") || !matching.Any(node => node.NodeKind == "EndpointClient"))
        {
            return null;
        }

        var first = matching.First();
        return EntryFromNode(first, "aligned-route-pair") with
        {
            EntryId = $"entry:aligned:{CombinedReportHelpers.Hash($"{parsed.Method}\0{parsed.PathKey}", 16)}",
            Classification = RouteFlowClassifications.ProbableStaticRouteFlow
        };
    }

    private static IEnumerable<RouteFlowEntryEvidence> EndpointEntries(IReadOnlyList<CombinedPathNode> nodes, string selector, string? nodeKind, string entryKind)
    {
        var parsed = ParseNormalizedEndpoint(selector);
        return nodes
            .Where(node => node.NodeKind is "EndpointRoute" or "EndpointClient")
            .Where(node => nodeKind is null || node.NodeKind == nodeKind)
            .Where(node => string.Equals(node.NormalizedPathKey, parsed.PathKey, StringComparison.Ordinal)
                && CombinedDependencyReporter.MethodsCompatible(parsed.Method, node.HttpMethod ?? "ANY"))
            .Select(node => EntryFromNode(node, entryKind));
    }

    private static RouteFlowEntryEvidence EntryFromNode(CombinedPathNode node, string entryKind)
    {
        var method = node.HttpMethod ?? string.Empty;
        var pathKey = node.NormalizedPathKey ?? string.Empty;
        return new RouteFlowEntryEvidence(
            $"entry:{entryKind}:{CombinedReportHelpers.Hash(node.NodeId, 16)}",
            entryKind,
            method,
            pathKey,
            pathKey,
            node.SymbolId ?? node.DisplayName,
            ClassificationForTier(node.EvidenceTier),
            CoverageFor(node.EvidenceTier),
            EvidenceFromNode(EntryRuleId, node, node.CombinedFactId is null ? [] : [node.CombinedFactId], [], [node.RuleId ?? EntryRuleId]));
    }

    private static IReadOnlyList<RouteFlowRow> BuildFlowRows(IReadOnlyList<CombinedPath> paths, IReadOnlyList<RouteFlowSource> sources, List<RouteFlowGap> gaps)
    {
        var rows = new List<RouteFlowRow>();
        var sequence = 0;
        foreach (var path in paths.OrderBy(path => path.PathId, StringComparer.Ordinal))
        {
            for (var index = 0; index < path.Nodes.Count; index++)
            {
                var node = path.Nodes[index];
                var edge = index == 0 ? null : path.Edges[index - 1];
                var previous = index == 0 ? null : path.Nodes[index - 1];
                sequence++;
                var rowKind = edge is null ? "entry" : RowKind(edge);
                if (edge is not null && rowKind == "interface-implementation-candidate" && previous is not null && !string.Equals(previous.SourceLabel, node.SourceLabel, StringComparison.Ordinal))
                {
                    gaps.Add(new RouteFlowGap(
                        $"gap:runtime-binding:cross-source:{CombinedReportHelpers.Hash(edge.EdgeId, 16)}",
                        "RuntimeBindingNotProven",
                        "Cross-source implementation candidate evidence is blocked in route-flow v1; runtime binding is not proven.",
                        GapRuleId,
                        EvidenceTiers.Tier4Unknown,
                        "ReducedCoverage",
                        SafeLabel(previous.SourceLabel),
                        null,
                        edge.SupportingFactIds,
                        ["Cross-source and cross-language implementation bridges require a future deterministic rule."]));
                    continue;
                }

                var classification = MaxClassification(ClassifyRouteRow(edge, node, sources), ClassificationFromPath(path.Classification));
                rows.Add(new RouteFlowRow(
                    $"row:{path.PathId}:{index:000}",
                    sequence,
                    rowKind,
                    edge is null ? "none" : EdgeKind(edge),
                    index == 0 ? node.DisplayName : path.Nodes[index - 1].DisplayName,
                    edge is null ? node.DisplayName : node.DisplayName,
                    classification,
                    CoverageFor(edge?.EvidenceTier ?? node.EvidenceTier),
                    edge?.FromNodeId,
                    edge?.ToNodeId ?? node.NodeId,
                    edge is null
                        ? EvidenceFromNode(PathRuleId, node, node.CombinedFactId is null ? [] : [node.CombinedFactId], [], [node.RuleId ?? PathRuleId])
                        : EvidenceFromEdge(rowKind == "interface-implementation-candidate" ? InterfaceBridgeRuleId : PathRuleId, edge, [edge.RuleId], previous?.SourceLabel, previous?.CommitSha)));

            }
        }

        return rows
            .GroupBy(row => row.RowId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<RouteFlowDependencySurface> BuildDependencySurfaces(IReadOnlyList<CombinedPath> paths, IReadOnlyList<RouteFlowSource> sources)
    {
        return paths
            .SelectMany(path => path.Nodes)
            .Where(node => !string.IsNullOrWhiteSpace(node.SurfaceKind))
            .Select(node =>
            {
                var stable = StableSurfaceKey(node);
                return new RouteFlowDependencySurface(
                    $"surface:{CombinedReportHelpers.Hash(stable, 16)}",
                    node.SurfaceKind!,
                    node.SurfaceName ?? node.DisplayName,
                    stable,
                    ClassifyRouteRow(null, node, sources),
                    CoverageFor(node.EvidenceTier),
                    Metadata(
                        ("operationName", node.OperationName),
                        ("tableName", node.TableName),
                        ("columnNames", node.ColumnNames),
                        ("sourceKind", node.SourceKind),
                        ("shapeHash", node.ShapeHash),
                        ("textHash", node.TextHash),
                        ("packageName", node.PackageName),
                        ("configKey", node.ConfigKey)),
                    EvidenceFromNode(DependencySurfaceRuleId, node, node.CombinedFactId is null ? [] : [node.CombinedFactId], [], [node.RuleId ?? DependencySurfaceRuleId]));
            })
            .GroupBy(surface => surface.StableKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<RouteFlowLogicRow> BuildLogicRows(IReadOnlyList<CombinedPath> paths, IReadOnlyList<RouteFlowRow> flowRows, IReadOnlyList<RouteFlowSource> sources)
    {
        var attachedByNode = flowRows
            .Where(row => !string.IsNullOrWhiteSpace(row.ToNodeId))
            .GroupBy(row => row.ToNodeId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.Sequence).First().RowId, StringComparer.Ordinal);
        var rows = new List<RouteFlowLogicRow>();
        foreach (var node in paths.SelectMany(path => path.Nodes).OrderBy(node => node.NodeId, StringComparer.Ordinal))
        {
            var kind = LogicKind(node);
            if (kind is null)
            {
                continue;
            }

            rows.Add(new RouteFlowLogicRow(
                $"logic:{CombinedReportHelpers.Hash($"{node.NodeId}:{kind}", 16)}",
                kind,
                node.SurfaceName ?? node.DisplayName,
                "path-context",
                attachedByNode.GetValueOrDefault(node.NodeId),
                ClassifyRouteRow(null, node, sources),
                CoverageFor(node.EvidenceTier),
                Metadata(
                    ("nodeKind", node.NodeKind),
                    ("surfaceKind", node.SurfaceKind),
                    ("operationName", node.OperationName),
                    ("sourceKind", node.SourceKind),
                    ("shapeHash", node.ShapeHash)),
                EvidenceFromNode(LogicSurfaceRuleId, node, node.CombinedFactId is null ? [] : [node.CombinedFactId], [], [node.RuleId ?? LogicSurfaceRuleId])));
        }

        foreach (var edge in paths.SelectMany(path => path.Edges).OrderBy(edge => edge.EdgeId, StringComparer.Ordinal))
        {
            if (edge.EdgeKind is not ("parameter-forward" or "argument-passed" or "argument-flow"))
            {
                continue;
            }

            rows.Add(new RouteFlowLogicRow(
                $"logic:{CombinedReportHelpers.Hash(edge.EdgeId, 16)}",
                "flow-boundary",
                edge.EdgeKind,
                "path-context",
                flowRows.FirstOrDefault(row => row.Evidence.SupportingEdgeIds.Contains(edge.EdgeId, StringComparer.Ordinal))?.RowId,
                ClassifyRouteRow(edge, null, sources),
                CoverageFor(edge.EvidenceTier),
                Metadata(("edgeKind", edge.EdgeKind)),
                EvidenceFromEdge(LogicSurfaceRuleId, edge, [edge.RuleId])));
        }

        return rows
            .GroupBy(row => row.LogicRowId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static RouteFlowSummary BuildSummary(
        string reportCoverage,
        IReadOnlyList<RouteFlowEntryEvidence> entries,
        IReadOnlyList<RouteFlowRow> flowRows,
        IReadOnlyList<RouteFlowLogicRow> logicRows,
        IReadOnlyList<RouteFlowDependencySurface> surfaces,
        IReadOnlyList<RouteFlowGap> gaps,
        bool truncated)
    {
        var reasons = new SortedSet<string>(StringComparer.Ordinal);
        var hasBlocking = gaps.Any(IsBlockingGap);
        string classification;
        if (reportCoverage != "FullEvidenceAvailable"
            || gaps.Any(gap => gap.GapKind is "SelectorNoMatch" or "SchemaMissing" or "ExtractorUnavailable" or "UnknownCommitSha" or "ReducedCoverage" or "UnknownAnalysisGap")
            || flowRows.Concat<object>(logicRows).Concat(surfaces).Any(RowIsUnknown)
            || entries.Count == 0)
        {
            classification = RouteFlowClassifications.UnknownAnalysisGap;
            reasons.Add("Selector, schema, source identity, or coverage gaps prevent a clean route-flow conclusion.");
        }
        else if (truncated
            || flowRows.Concat<object>(logicRows).Concat(surfaces).Any(RowNeedsReview)
            || gaps.Any(gap => gap.GapKind is "RuntimeBindingNotProven" or "ImplementationCandidateUnavailable" or "DynamicClientUrlNeedsReview" or "TruncatedByLimit"))
        {
            classification = RouteFlowClassifications.NeedsReviewStaticRouteFlow;
            reasons.Add("Review-tier, weak, implementation-candidate, dynamic, or truncated static evidence is present.");
        }
        else if (flowRows.Count == 0 && surfaces.Count == 0)
        {
            classification = RouteFlowClassifications.NoRouteFlowEvidence;
            reasons.Add("Entry evidence matched but no route-flow path or terminal surface remained after filtering.");
        }
        else if (surfaces.Count > 0
            && flowRows.All(row => row.Evidence.EvidenceTier is EvidenceTiers.Tier1Semantic or EvidenceTiers.Tier2Structural)
            && flowRows.Any(row => row.Evidence.EvidenceTier == EvidenceTiers.Tier1Semantic))
        {
            classification = RouteFlowClassifications.StrongStaticRouteFlow;
            reasons.Add("Selected static route-flow evidence includes semantic path links and no blocking gaps.");
        }
        else
        {
            classification = RouteFlowClassifications.ProbableStaticRouteFlow;
            reasons.Add("Selected static route-flow evidence is structural and coverage-relative.");
        }

        if (surfaces.Count == 0 && flowRows.Count > 0)
        {
            reasons.Add("No terminal dependency/data surface was reached.");
        }

        var exitWouldBeNonZero = classification is RouteFlowClassifications.NeedsReviewStaticRouteFlow
            or RouteFlowClassifications.NoRouteFlowEvidence
            or RouteFlowClassifications.UnknownAnalysisGap
            || hasBlocking;
        return new RouteFlowSummary(
            classification,
            reportCoverage,
            entries.Count,
            flowRows.Count,
            logicRows.Count,
            surfaces.Count,
            gaps.Count,
            hasBlocking,
            truncated,
            exitWouldBeNonZero,
            reasons.ToArray());
    }

    private static void ApplyClassificationFilter(
        string? classification,
        List<RouteFlowRow> flowRows,
        List<RouteFlowLogicRow> logicRows,
        List<RouteFlowDependencySurface> surfaces,
        List<RouteFlowGap> gaps)
    {
        if (string.IsNullOrWhiteSpace(classification))
        {
            return;
        }

        var requested = classification.Trim();
        flowRows.RemoveAll(row => !string.Equals(row.Classification, requested, StringComparison.Ordinal));
        logicRows.RemoveAll(row => !string.Equals(row.Classification, requested, StringComparison.Ordinal));
        surfaces.RemoveAll(row => !string.Equals(row.Classification, requested, StringComparison.Ordinal));
        gaps.RemoveAll(gap => !string.Equals(GapClassification(gap), requested, StringComparison.Ordinal) && !string.Equals(gap.GapKind, requested, StringComparison.Ordinal));
        if (flowRows.Count == 0 && logicRows.Count == 0 && surfaces.Count == 0 && gaps.Count == 0)
        {
            gaps.Add(new RouteFlowGap(
                $"gap:selector:classification:{CombinedReportHelpers.Hash(requested, 16)}",
                "SelectorNoMatch",
                "Classification filtering removed every route-flow row.",
                SelectorRuleId,
                EvidenceTiers.Tier4Unknown,
                "ReducedCoverage",
                null,
                null,
                [],
                ["Classification filters are applied to derived static route-flow rows."]));
        }
    }

    private static RouteFlowGap FromPathGap(CombinedPathGap gap)
    {
        return new RouteFlowGap(
            $"gap:path:{CombinedReportHelpers.Hash(gap.GapId, 24)}",
            NormalizeGapKind(gap.GapKind),
            SafeSelector(gap.Message) ?? "Route-flow analysis gap.",
            GapRuleId,
            gap.EvidenceTier ?? EvidenceTiers.Tier4Unknown,
            GapClassification(gap) == RouteFlowClassifications.UnknownAnalysisGap ? "ReducedCoverage" : "CoverageRelative",
            SafeSelector(gap.SourceLabel),
            gap.NodeId,
            gap.CombinedFactId is null ? [] : [gap.CombinedFactId],
            ["Path gaps are inherited as route-flow analysis gaps and remain coverage-relative."]);
    }

    private static IEnumerable<RouteFlowGap> SourceIdentityGaps(IReadOnlyList<RouteFlowSource> sources)
    {
        return sources
            .Where(source => !source.IdentityVerified)
            .Select(source => new RouteFlowGap(
                $"gap:identity:{CombinedReportHelpers.Hash($"{source.SourceLabel}:{source.SourceIndexId}", 16)}",
                string.IsNullOrWhiteSpace(source.CommitSha) ? "UnknownCommitSha" : "ReducedCoverage",
                $"Source `{source.SourceLabel}` has missing or unverified identity metadata.",
                GapRuleId,
                EvidenceTiers.Tier4Unknown,
                "ReducedCoverage",
                source.SourceLabel,
                null,
                [],
                ["Unknown or unverified source identity caps route-flow conclusions."]));
    }

    private static RouteFlowEvidenceRef EvidenceFromNode(string routeRuleId, CombinedPathNode node, IReadOnlyList<string> facts, IReadOnlyList<string> edges, IReadOnlyList<string?> supportingRules)
    {
        return new RouteFlowEvidenceRef(
            routeRuleId,
            node.EvidenceTier ?? EvidenceTiers.Tier4Unknown,
            SafeLabel(node.SourceLabel),
            KnownCommit(node.CommitSha) ? node.CommitSha : null,
            CombinedReportHelpers.SafePath(node.FilePath),
            node.StartLine,
            node.EndLine,
            ExtractorName(node.RuleId),
            null,
            facts.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            edges.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            supportingRules.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).Append(routeRuleId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            LimitationsFor(routeRuleId));
    }

    private static RouteFlowEvidenceRef EvidenceFromEdge(string routeRuleId, CombinedPathEdge edge, IReadOnlyList<string?> supportingRules, string? sourceLabel = null, string? commitSha = null)
    {
        return new RouteFlowEvidenceRef(
            routeRuleId,
            edge.EvidenceTier,
            SafeLabel(sourceLabel ?? "unknown"),
            KnownCommit(commitSha) ? commitSha : null,
            CombinedReportHelpers.SafePath(edge.FilePath),
            edge.StartLine,
            edge.EndLine,
            ExtractorName(edge.RuleId),
            null,
            edge.SupportingFactIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            edge.SupportingCombinedEdgeIds.Append(edge.EdgeId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            supportingRules.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).Append(routeRuleId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            LimitationsFor(routeRuleId));
    }

    private static string? NormalizeEndpointSelector(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return null;
        }

        var parsed = ParseNormalizedEndpoint(selector);
        return $"{parsed.Method} {parsed.PathKey}";
    }

    private static (string Method, string PathKey) ParseNormalizedEndpoint(string selector)
    {
        var trimmed = selector.Trim();
        var split = trimmed.IndexOf(' ');
        var method = split > 0 ? trimmed[..split].Trim().ToUpperInvariant() : "ANY";
        var path = split > 0 ? trimmed[(split + 1)..].Trim() : trimmed;
        if (path.Contains("%2f", StringComparison.OrdinalIgnoreCase) || path.Contains("..", StringComparison.Ordinal))
        {
            return (method, EndpointRouteNormalizer.Normalize("/selector-review").PathKey);
        }

        return (method, EndpointRouteNormalizer.Normalize(path).PathKey);
    }

    private static string RouteMatchMode(string? route, string? clientCall, string? endpoint, string? symbol, string? source)
    {
        if (route is not null || endpoint is not null)
        {
            return "NormalizedMethodPath";
        }

        if (clientCall is not null)
        {
            return "NormalizedMethodPath";
        }

        if (!string.IsNullOrWhiteSpace(symbol))
        {
            return "SymbolSelector";
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            return "SourceSelector";
        }

        return "SelectorNoMatch";
    }

    private static string RowKind(CombinedPathEdge edge)
    {
        return edge.EdgeKind switch
        {
            "endpoint-match" => "client-server-alignment",
            "calls" => "call-edge",
            "creates" => "object-creation",
            "argument-passed" or "argument-flow" => "argument-flow",
            "parameter-forward" => "parameter-forward",
            "implements" or "overrides" => "interface-implementation-candidate",
            "inherits" => "symbol-relationship",
            "surface-evidence" => "terminal-surface",
            "fact-attached-to-symbol" => "path-context",
            _ => "path-context"
        };
    }

    private static string EdgeKind(CombinedPathEdge edge)
    {
        return edge.EdgeKind switch
        {
            "endpoint-match" => "client-server-alignment",
            "calls" => "direct-call",
            "creates" => "object-creation",
            "argument-passed" or "argument-flow" => "argument-flow",
            "parameter-forward" => "parameter-forward",
            "implements" or "overrides" => "interface-implementation-candidate",
            "inherits" => "symbol-relationship",
            "surface-evidence" => "terminal-surface",
            "fact-attached-to-symbol" => "fact-symbol-attachment",
            _ => "unknown"
        };
    }

    private static string? LogicKind(CombinedPathNode node)
    {
        if (node.SurfaceKind is "sql-query")
        {
            return "query-filter-sort-selection";
        }

        if (node.SurfaceKind is "sql-persistence" or "legacy-data")
        {
            return "projection-or-object-shape";
        }

        if (!string.IsNullOrWhiteSpace(node.ShapeHash))
        {
            return "projection-or-object-shape";
        }

        if (node.RuleId?.Contains("async", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "async-boundary";
        }

        if (node.RuleId?.Contains("validation", StringComparison.OrdinalIgnoreCase) == true
            || node.DisplayName?.Contains("guard", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "validation-or-guard";
        }

        return null;
    }

    private static string ClassifyRouteRow(CombinedPathEdge? edge, CombinedPathNode? node, IReadOnlyList<RouteFlowSource> sources)
    {
        var tier = edge?.EvidenceTier ?? node?.EvidenceTier;
        if (edge?.EdgeKind is "implements" or "overrides")
        {
            return RouteFlowClassifications.NeedsReviewStaticRouteFlow;
        }

        if (tier == EvidenceTiers.Tier4Unknown)
        {
            return RouteFlowClassifications.UnknownAnalysisGap;
        }

        if (tier == EvidenceTiers.Tier3SyntaxOrTextual)
        {
            return RouteFlowClassifications.NeedsReviewStaticRouteFlow;
        }

        if (node is not null && sources.Any(source => source.SourceLabel == node.SourceLabel && !source.IdentityVerified))
        {
            return RouteFlowClassifications.NeedsReviewStaticRouteFlow;
        }

        return tier == EvidenceTiers.Tier1Semantic
            ? RouteFlowClassifications.StrongStaticRouteFlow
            : RouteFlowClassifications.ProbableStaticRouteFlow;
    }

    private static string ClassificationFromPath(string pathClassification)
    {
        return pathClassification switch
        {
            CombinedDependencyPathClassifications.StrongStaticPath => RouteFlowClassifications.StrongStaticRouteFlow,
            CombinedDependencyPathClassifications.ProbableStaticPath => RouteFlowClassifications.ProbableStaticRouteFlow,
            CombinedDependencyPathClassifications.NoBackendEvidence or CombinedDependencyPathClassifications.NoPathFound => RouteFlowClassifications.NoRouteFlowEvidence,
            CombinedDependencyPathClassifications.AnalysisGap
                or CombinedDependencyPathClassifications.UnknownAnalysisGap
                or CombinedDependencyPathClassifications.ReducedCoverage
                or CombinedDependencyPathClassifications.SelectorNoMatch
                or CombinedDependencyPathClassifications.ClassificationFilterNoMatch => RouteFlowClassifications.UnknownAnalysisGap,
            _ => RouteFlowClassifications.NeedsReviewStaticRouteFlow
        };
    }

    private static string MaxClassification(string left, string right)
    {
        return ClassificationRank(left) >= ClassificationRank(right) ? left : right;
    }

    private static int ClassificationRank(string classification)
    {
        return classification switch
        {
            RouteFlowClassifications.StrongStaticRouteFlow => 0,
            RouteFlowClassifications.ProbableStaticRouteFlow => 1,
            RouteFlowClassifications.NeedsReviewStaticRouteFlow => 2,
            RouteFlowClassifications.NoRouteFlowEvidence => 3,
            RouteFlowClassifications.UnknownAnalysisGap => 4,
            _ => 4
        };
    }

    private static string ClassificationForTier(string? tier)
    {
        return tier switch
        {
            EvidenceTiers.Tier1Semantic => RouteFlowClassifications.StrongStaticRouteFlow,
            EvidenceTiers.Tier2Structural => RouteFlowClassifications.ProbableStaticRouteFlow,
            EvidenceTiers.Tier3SyntaxOrTextual => RouteFlowClassifications.NeedsReviewStaticRouteFlow,
            _ => RouteFlowClassifications.UnknownAnalysisGap
        };
    }

    private static string CoverageFor(string? tier)
    {
        return tier == EvidenceTiers.Tier4Unknown ? "ReducedCoverage" : "CoverageRelative";
    }

    private static bool RowNeedsReview(object row)
    {
        return row switch
        {
            RouteFlowRow flow => flow.Classification is RouteFlowClassifications.NeedsReviewStaticRouteFlow,
            RouteFlowLogicRow logic => logic.Classification is RouteFlowClassifications.NeedsReviewStaticRouteFlow,
            RouteFlowDependencySurface surface => surface.Classification is RouteFlowClassifications.NeedsReviewStaticRouteFlow,
            _ => false
        };
    }

    private static bool RowIsUnknown(object row)
    {
        return row switch
        {
            RouteFlowRow flow => flow.Classification is RouteFlowClassifications.UnknownAnalysisGap,
            RouteFlowLogicRow logic => logic.Classification is RouteFlowClassifications.UnknownAnalysisGap,
            RouteFlowDependencySurface surface => surface.Classification is RouteFlowClassifications.UnknownAnalysisGap,
            _ => false
        };
    }

    private static bool IsBlockingGap(RouteFlowGap gap)
    {
        return gap.GapKind is "SelectorNoMatch" or "SchemaMissing" or "ExtractorUnavailable" or "UnknownCommitSha" or "UnknownAnalysisGap" or "TruncatedByLimit";
    }

    private static string GapClassification(RouteFlowGap gap)
    {
        return gap.GapKind is "NoRouteFlowEvidence"
            ? RouteFlowClassifications.NoRouteFlowEvidence
            : gap.GapKind is "SelectorNoMatch" or "SchemaMissing" or "UnknownCommitSha" or "UnknownAnalysisGap" or "ReducedCoverage"
                ? RouteFlowClassifications.UnknownAnalysisGap
                : RouteFlowClassifications.NeedsReviewStaticRouteFlow;
    }

    private static string GapClassification(CombinedPathGap gap)
    {
        return gap.Classification is CombinedDependencyPathClassifications.NoBackendEvidence or CombinedDependencyPathClassifications.NoPathFound
            ? RouteFlowClassifications.NoRouteFlowEvidence
            : gap.Classification is CombinedDependencyPathClassifications.SelectorNoMatch
                or CombinedDependencyPathClassifications.ClassificationFilterNoMatch
                or CombinedDependencyPathClassifications.AnalysisGap
                or CombinedDependencyPathClassifications.UnknownAnalysisGap
                or CombinedDependencyPathClassifications.ReducedCoverage
                    ? RouteFlowClassifications.UnknownAnalysisGap
                    : RouteFlowClassifications.NeedsReviewStaticRouteFlow;
    }

    private static string NormalizeGapKind(string kind)
    {
        return kind switch
        {
            "SelectorNoMatch" or "ClassificationFilterNoMatch" => "SelectorNoMatch",
            "TruncatedByLimit" => "TruncatedByLimit",
            "NoPathFound" or "NoBackendEvidence" => "NoRouteFlowEvidence",
            "ExtractorUnavailable" => "ExtractorUnavailable",
            _ when kind.Contains("dynamic", StringComparison.OrdinalIgnoreCase) => "DynamicClientUrlNeedsReview",
            _ => "UnknownAnalysisGap"
        };
    }

    private static string StableSurfaceKey(CombinedPathNode node)
    {
        var value = string.Join("|", new[]
        {
            node.SurfaceKind ?? "dependency-surface",
            node.SourceLabel,
            node.NormalizedPathKey,
            node.PackageName,
            node.ConfigKey,
            node.OperationName,
            node.TableName,
            node.ShapeHash,
            node.TextHash,
            node.SurfaceName ?? node.DisplayName
        }.Where(item => !string.IsNullOrWhiteSpace(item)));
        return value.Length <= 160 ? value : $"surface-key-hash:{CombinedReportHelpers.Hash(value, 32)}";
    }

    private static IReadOnlyDictionary<string, string> Metadata(params (string Key, string? Value)[] values)
    {
        return values
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => new KeyValuePair<string, string>(item.Key, SafeSelector(item.Value) ?? "redacted"))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ThenBy(item => item.Value, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }

    private static bool IdentityVerified(CombinedReportSource source)
    {
        return CombinedReportHelpers.SourceIdentityVerified(source)
            && !string.Equals(source.AnalysisLevel, "failed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(source.BuildStatus, "failed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool KnownCommit(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            && value.Trim('0').Length > 0;
    }

    private static string SafeLabel(string? value)
    {
        return SafeSelector(value) ?? $"source-label-hash:{CombinedReportHelpers.Hash(value ?? "unknown", 16)}";
    }

    private static string? SafeSelector(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains("://", StringComparison.Ordinal)
            || trimmed.Contains(":\\", StringComparison.Ordinal)
            || trimmed.StartsWith("/", StringComparison.Ordinal)
            || trimmed.Contains("SELECT ", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("TOKEN", StringComparison.OrdinalIgnoreCase))
        {
            return $"redacted-hash:{CombinedReportHelpers.Hash(trimmed, 16)}";
        }

        return trimmed.ReplaceLineEndings(" ");
    }

    private static bool NodeMatches(CombinedPathNode node, string selector)
    {
        return string.Equals(node.SymbolId, selector, StringComparison.Ordinal)
            || string.Equals(node.DisplayName, selector, StringComparison.Ordinal)
            || node.DisplayName?.Contains(selector, StringComparison.OrdinalIgnoreCase) == true
            || string.Equals(node.CombinedFactId, selector, StringComparison.Ordinal);
    }

    private static string ExtractorName(string? ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            return "unknown";
        }

        var index = ruleId.IndexOf('.', StringComparison.Ordinal);
        return index > 0 ? ruleId[..index] : ruleId;
    }

    private static IReadOnlyList<string> LimitationsFor(string ruleId)
    {
        return ruleId switch
        {
            InterfaceBridgeRuleId => ["Candidate implementation evidence is not runtime dependency-injection target proof."],
            DependencySurfaceRuleId => ["Dependency/data surface rows are static evidence only and do not prove runtime persistence, traffic, or production use."],
            LogicSurfaceRuleId => ["Business/data logic rows are static path context and do not prove branch feasibility or business impact."],
            _ => ["Route-flow rows are static evidence and do not prove runtime execution."]
        };
    }

    private static IReadOnlyList<string> Limitations()
    {
        return
        [
            "Route-flow rows are deterministic static evidence, not runtime execution proof.",
            "Endpoint alignment does not prove live traffic, deployment, auth, middleware, proxy, CORS, or reachability behavior.",
            "Call edges may miss reflection, dynamic dispatch, delegates, generated code, dependency injection, and branch feasibility.",
            "Candidate implementation rows identify compiler-known candidates only and do not prove runtime dependency-injection targets.",
            "Query and dependency/data rows do not prove SQL execution, database existence, schema compatibility, persistence, or production use.",
            "Business/data logic rows are static review context, not proof of business intent or business impact.",
            "Reduced coverage, missing extractors, missing schema, unknown commit SHA, truncation, dynamic URLs, and ambiguous candidates cap classifications.",
            "Unsafe paths, remotes, snippets, URLs, SQL/config values, connection strings, private labels, and secret-like values are omitted or hashed."
        ];
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

    private static string RenderMarkdown(RouteFlowReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Route Flow Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("- Results are static route-flow evidence and coverage-relative.");
        builder.AppendLine($"- Classification: `{report.Summary.Classification}`");
        builder.AppendLine($"- Report coverage: `{report.ReportCoverage}`");
        builder.AppendLine($"- Entry evidence: `{report.Summary.EntryEvidenceCount}`");
        builder.AppendLine($"- Static flow rows: `{report.Summary.FlowRowCount}`");
        builder.AppendLine($"- Business/data logic rows: `{report.Summary.LogicRowCount}`");
        builder.AppendLine($"- Dependency surfaces: `{report.Summary.DependencySurfaceCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        builder.AppendLine($"- Exit code would be non-zero: `{report.Summary.ExitCodeWouldBeNonZero}`");
        AppendList(builder, "Classification reasons", report.Summary.ClassificationReasons);
        AppendList(builder, "Coverage warnings", report.CoverageWarnings);
        builder.AppendLine();

        builder.AppendLine("## Query");
        builder.AppendLine();
        builder.AppendLine($"- Route: `{report.Query.Route ?? "n/a"}`");
        builder.AppendLine($"- Client call: `{report.Query.ClientCall ?? "n/a"}`");
        builder.AppendLine($"- From endpoint: `{report.Query.FromEndpoint ?? "n/a"}`");
        builder.AppendLine($"- From WebForms event: `{report.Query.FromWebFormsEvent ?? "n/a"}`");
        builder.AppendLine($"- From symbol: `{report.Query.FromSymbol ?? "n/a"}`");
        builder.AppendLine($"- From source: `{report.Query.FromSource ?? "n/a"}`");
        builder.AppendLine($"- To surface: `{report.Query.ToSurface ?? "all"}`");
        builder.AppendLine($"- Surface name: `{report.Query.SurfaceName ?? "n/a"}`");
        builder.AppendLine($"- Classification: `{report.Query.Classification ?? "all"}`");
        builder.AppendLine($"- Route match mode: `{report.Query.RouteMatchMode}`");
        builder.AppendLine($"- Bounds: depth `{report.Query.MaxDepth}`, paths `{report.Query.MaxPaths}`, frontier `{report.Query.MaxFrontier}`, logic rows `{report.Query.MaxLogicRows}`, gaps `{report.Query.MaxGaps}`");
        builder.AppendLine();

        builder.AppendLine("## Snapshot Sources");
        builder.AppendLine();
        AppendRows(builder, report.Snapshot.Sources, "| Source | Language | Commit | Analysis | Build | Identity |", "| --- | --- | --- | --- | --- | --- |",
            source => $"| {Cell(source.SourceLabel)} | {Cell(source.Language)} | {Cell(source.CommitSha ?? "unknown")} | {Cell(source.AnalysisLevel)} | {Cell(source.BuildStatus)} | {Cell(source.IdentityVerified.ToString())} |");

        builder.AppendLine("## Entry Evidence");
        builder.AppendLine();
        AppendRows(builder, report.EntryEvidence, "| Kind | Method | Path key | Classification | Evidence |", "| --- | --- | --- | --- | --- |",
            row => $"| {Cell(row.EntryKind)} | {Cell(row.Method)} | {Cell(row.NormalizedPathKey)} | {Cell(row.Classification)} | {Cell(Evidence(row.Evidence))} |");

        builder.AppendLine("## Static Flow");
        builder.AppendLine();
        AppendRows(builder, report.FlowRows, "| Seq | Kind | Edge | Source | Target | Classification | Evidence |", "| --- | --- | --- | --- | --- | --- | --- |",
            row => $"| {row.Sequence} | {Cell(row.RowKind)} | {Cell(row.EdgeKind)} | {Cell(row.SourceSymbol)} | {Cell(row.TargetSymbol ?? "n/a")} | {Cell(row.Classification)} | {Cell(Evidence(row.Evidence))} |");

        builder.AppendLine("## Business/Data Logic");
        builder.AppendLine();
        AppendRows(builder, report.LogicRows, "| Kind | Name | Attachment | Classification | Evidence |", "| --- | --- | --- | --- | --- |",
            row => $"| {Cell(row.LogicKind)} | {Cell(row.DisplayName)} | {Cell(row.AttachmentKind)} | {Cell(row.Classification)} | {Cell(Evidence(row.Evidence))} |");

        builder.AppendLine("## Dependency Surfaces");
        builder.AppendLine();
        AppendRows(builder, report.DependencySurfaces, "| Kind | Name | Stable key | Classification | Evidence |", "| --- | --- | --- | --- | --- |",
            row => $"| {Cell(row.SurfaceKind)} | {Cell(row.DisplayName)} | {Cell(row.StableKey)} | {Cell(row.Classification)} | {Cell(Evidence(row.Evidence))} |");

        builder.AppendLine("## Gaps");
        builder.AppendLine();
        AppendRows(builder, report.Gaps, "| Kind | Coverage | Source | Message | Evidence tier |", "| --- | --- | --- | --- | --- |",
            gap => $"| {Cell(gap.GapKind)} | {Cell(gap.Coverage)} | {Cell(gap.SourceLabel ?? "n/a")} | {Cell(gap.Message)} | {Cell(gap.EvidenceTier)} |");

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

        if (rows.Count > MarkdownRowLimit)
        {
            builder.AppendLine($"Showing first {MarkdownRowLimit} of {rows.Count} rows. JSON contains all returned rows.");
            builder.AppendLine();
        }

        builder.AppendLine(header);
        builder.AppendLine(separator);
        foreach (var row in rows.Take(MarkdownRowLimit))
        {
            builder.AppendLine(render(row));
        }

        builder.AppendLine();
    }

    private static void AppendList(StringBuilder builder, string title, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        builder.AppendLine($"- {title}: {string.Join("; ", values.Select(value => $"`{Cell(value)}`"))}");
    }

    private static string Evidence(RouteFlowEvidenceRef evidence)
    {
        return $"{evidence.RuleId} {evidence.EvidenceTier} {evidence.FilePath ?? "n/a"}:{evidence.StartLine?.ToString() ?? "?"}";
    }

    private static string Cell(string? value)
    {
        return CombinedReportHelpers.Cell(value);
    }
}
