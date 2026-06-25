using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    IReadOnlyList<RouteFlowTouchedFile> TouchedFiles,
    IReadOnlyList<RouteFlowTouchedSymbol> TouchedSymbols,
    IReadOnlyList<RouteFlowGap> Gaps,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<RouteFlowContextGroup>? ContextGroups = null);

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
    bool ExitCode,
    RouteFlowSelectorTrace? SelectorTrace = null);

public sealed record RouteFlowSelectorTrace(
    string SelectorKind,
    string MatchMode,
    string? SafeNormalizedKey,
    string? SafeSelector,
    string RedactionState,
    string Coverage,
    string RuleId,
    string EvidenceTier,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> Limitations);

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
    string ScannerVersion,
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
    string BridgeState,
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
    RouteFlowEvidenceRef Evidence,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SurfaceSubtype = null);

public sealed record RouteFlowTouchedFile(
    string FileId,
    string SourceLabel,
    string? CommitSha,
    string FilePath,
    int? FirstStartLine,
    int? LastEndLine,
    string Classification,
    string Coverage,
    IReadOnlyList<string> SupportingRowIds,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    RouteFlowEvidenceRef Evidence);

public sealed record RouteFlowTouchedSymbol(
    string SymbolId,
    string SourceLabel,
    string? CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string DisplayName,
    string SymbolKind,
    string Classification,
    string Coverage,
    IReadOnlyList<string> SupportingRowIds,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    RouteFlowEvidenceRef Evidence);

public sealed record RouteFlowContextGroup(
    string GroupId,
    string GroupKind,
    string DisplayName,
    string MatchKind,
    string ValueSafety,
    string Classification,
    string Coverage,
    string EvidenceTier,
    IReadOnlyDictionary<string, string> SafeMetadata,
    IReadOnlyList<string> SupportingRowIds,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> Limitations,
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
    IReadOnlyList<string> Limitations,
    string? FilePath = null,
    int? StartLine = null,
    int? EndLine = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CommitSha = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ExtractorName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ExtractorVersion = null);

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
    private const string ArgumentProjectionRuleId = "combined.route-flow.argument-projection.v1";
    private const string FactSymbolProjectionRuleId = "combined.route-flow.fact-symbol-projection.v1";
    private const string ClassificationRuleId = "combined.route-flow.classification.v1";
    private const string GapRuleId = "combined.route-flow.gap.v1";
    private const string RedactionRuleId = "combined.route-flow.redaction.v1";
    private const string ReportRuleId = "combined.route-flow.report.v1";
    private const int MarkdownRowLimit = 200;
    private const int AmbiguitySupportingFactIdLimit = 20;
    private const int AmbiguityLargeRootNoticeThreshold = 50;

    private static readonly HashSet<string> AllowedClassifications = new(StringComparer.Ordinal)
    {
        RouteFlowClassifications.StrongStaticRouteFlow,
        RouteFlowClassifications.ProbableStaticRouteFlow,
        RouteFlowClassifications.NeedsReviewStaticRouteFlow,
        RouteFlowClassifications.NoRouteFlowEvidence,
        RouteFlowClassifications.UnknownAnalysisGap
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
        var inventory = await CombinedDependencyPathReporter.BuildGraphInventoryAsync(options.IndexPath, cancellationToken: cancellationToken);
        var selectedPaths = FilterPathsForSelectorSide(pathReport.Paths, routeSelector, clientSelector).ToArray();
        var symbolKinds = await ReadCombinedSymbolKindsAsync(options.IndexPath, cancellationToken);
        var endpointComposition = clientSelector is null
            ? BuildEndpointCompositionPaths(options, routeSelector, clientSelector, endpointSelector, inventory, symbolKinds)
            : new EndpointCompositionResult([], [], false);
        var routePaths = endpointComposition.Paths.Count > 0
            ? endpointComposition.Paths
            : selectedPaths;

        var schemaGaps = await ReadRouteFlowSchemaGapsAsync(options.IndexPath, cancellationToken);
        var sources = ToSources(inventory.Sources, inventory.CoverageWarnings);

        var entryEvidence = SelectEntryEvidence(options, routeSelector, clientSelector, endpointSelector, inventory.Nodes, inventory.Edges, sources)
            .OrderBy(row => row.EntryKind, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.NormalizedPathKey, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.StartLine ?? 0)
            .ThenBy(row => row.EntryId, StringComparer.Ordinal)
            .ToArray();

        var sourceIdentityGaps = SourceIdentityGaps(sources).ToList();
        var endpointMissingRouteRoot = endpointComposition.Gaps.Any(gap => gap.GapKind == "MissingRouteRoot");
        var endpointCompositionHasPaths = endpointComposition.Paths.Count > 0;
        var endpointCompositionHasBlockingGaps = endpointComposition.Gaps.Any(IsNoEvidenceBlockingCompositionGap);
        var pathGaps = pathReport.Gaps
            .Select(gap => FromPathGap(gap, pathReport.ReportCoverage))
            .Where(gap => !endpointMissingRouteRoot || gap.GapKind != "SelectorNoMatch")
            .Where(gap => !(endpointCompositionHasPaths || endpointCompositionHasBlockingGaps) || gap.GapKind != "NoRouteFlowEvidence");
        var gaps = pathGaps
            .Concat(schemaGaps)
            .Concat(sourceIdentityGaps)
            .Concat(endpointComposition.Gaps)
            .ToList();
        RemoveDuplicateDispatchCandidateFanOutGaps(gaps);
        RemoveCleanNoEvidenceGapsWhenBlocked(gaps);
        if (entryEvidence.Length == 0 && !endpointMissingRouteRoot)
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

        var flowRows = BuildFlowRows(routePaths, sources, gaps)
            .OrderBy(row => row.Sequence)
            .ThenBy(row => row.Evidence.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.StartLine ?? 0)
            .ThenBy(row => row.RowId, StringComparer.Ordinal)
            .ToList();
        var dependencySurfaces = BuildDependencySurfaces(routePaths, sources)
            .OrderBy(surface => surface.SurfaceKind, StringComparer.Ordinal)
            .ThenBy(surface => surface.StableKey, StringComparer.Ordinal)
            .ThenBy(surface => surface.SurfaceId, StringComparer.Ordinal)
            .ToList();
        var selectedSourceIndexIds = selectedPaths
            .SelectMany(path => path.Nodes)
            .Concat(routePaths.SelectMany(path => path.Nodes))
            .Select(node => node.SourceIndexId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var projections = await BuildProjectionRowsAsync(options.IndexPath, routePaths, selectedSourceIndexIds, flowRows, sources, cancellationToken);
        gaps.AddRange(projections.Gaps);
        RemoveCleanNoEvidenceGapsWhenBlocked(gaps);
        var allLogicRows = BuildLogicRows(routePaths, flowRows, sources)
            .Concat(projections.Rows)
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

        if (entryEvidence.Length > 0
            && !HasDownstreamFlowEvidence(flowRows)
            && dependencySurfaces.Count == 0
            && !gaps.Any(BlocksCleanNoEvidenceGap))
        {
            gaps.Add(new RouteFlowGap(
                "gap:no-route-flow-evidence",
                "NoRouteFlowEvidence",
                "Entry evidence matched, but no downstream route-flow path or terminal surface was found under available coverage.",
                ClassificationRuleId,
                EvidenceTiers.Tier4Unknown,
                pathReport.ReportCoverage == "FullEvidenceAvailable" && sourceIdentityGaps.Count == 0 && !endpointComposition.Gaps.Any(IsNoEvidenceBlockingCompositionGap) ? "FullEvidenceAvailable" : "ReducedCoverage",
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

        var contextGroups = BuildContextGroups(flowRows, logicRows, dependencySurfaces, gaps);
        var touchedFiles = BuildTouchedFiles(entryEvidence, flowRows, logicRows, dependencySurfaces, gaps);
        var touchedSymbols = BuildTouchedSymbols(entryEvidence, flowRows, logicRows, dependencySurfaces, routePaths, sources);
        var coverageWarnings = pathReport.CoverageWarnings
            .Concat(sources.SelectMany(source => source.CoverageWarnings))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var reportCoverage = coverageWarnings.Length == 0 && gaps.All(gap => !IsBlockingGap(gap)) ? "FullEvidenceAvailable" : "ReducedCoverage";
        var summary = BuildSummary(reportCoverage, entryEvidence, flowRows, logicRows, dependencySurfaces, gaps, pathReport.Summary.Truncated || endpointComposition.Truncated || gaps.Any(gap => gap.GapKind is "TruncatedByLimit" or "TraversalBounds"));
        var query = new RouteFlowQuery(
            CombinedReportHelpers.SafePath(options.IndexPath),
            CombinedReportHelpers.SafePath(options.OutputPath),
            format,
            SafeSelector(routeSelector),
            SafeSelector(clientSelector),
            SafeSelector(endpointSelector),
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
            options.ExitCode,
            BuildSelectorTrace(options, routeSelector, clientSelector, endpointSelector, entryEvidence, gaps, reportCoverage));

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
            touchedFiles,
            touchedSymbols,
            gaps,
            Limitations(),
            contextGroups);
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

        if (!string.IsNullOrWhiteSpace(options.ToSurface) && !CombinedTerminalSurfaceKinds.AllSet.Contains(options.ToSurface.Trim()))
        {
            throw new ArgumentException($"route-flow --to-surface must be one of {CombinedTerminalSurfaceKinds.ValidationList}.");
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

        return gaps;
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadCombinedSymbolKindsAsync(string indexPath, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = indexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        if (!await TableExistsAsync(connection, "combined_symbols", cancellationToken))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select source_index_id, display_name, symbol_kind
            from combined_symbols
            where source_index_id is not null
              and display_name is not null
              and symbol_kind is not null
            order by source_index_id, display_name, symbol_kind;
            """;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = SymbolKindKey(reader.GetString(0), reader.GetString(1));
            result.TryAdd(key, reader.GetString(2));
        }

        return result;
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
                    SafeCommitSha(source.CommitSha),
                    source.Language ?? "unknown",
                    source.AnalysisLevel,
                    source.BuildStatus,
                    IdentityVerified(source),
                    SafeSelector(source.ScannerVersion) ?? "unknown",
                    sourceWarnings);
            })
            .ToArray();
    }

    private static IReadOnlyList<RouteFlowEntryEvidence> SelectEntryEvidence(
        CombinedRouteFlowOptions options,
        string? routeSelector,
        string? clientSelector,
        string? endpointSelector,
        IReadOnlyList<CombinedPathNode> nodes,
        IReadOnlyList<CombinedPathEdge> edges,
        IReadOnlyList<RouteFlowSource> sources)
    {
        var bridgeStates = EndpointMethodBridgeStates(nodes, edges, sources);
        var rows = new List<RouteFlowEntryEvidence>();
        if (routeSelector is not null)
        {
            rows.AddRange(EndpointEntries(nodes, routeSelector, "EndpointRoute", "route-root", bridgeStates, sources));
        }

        if (clientSelector is not null)
        {
            rows.AddRange(EndpointEntries(nodes, clientSelector, "EndpointClient", "client-call-root", bridgeStates, sources));
        }

        if (endpointSelector is not null)
        {
            rows.AddRange(EndpointEntries(nodes, endpointSelector, null, "endpoint-root", bridgeStates, sources));
        }

        if (!string.IsNullOrWhiteSpace(options.FromWebFormsEvent))
        {
            rows.AddRange(nodes
                .Where(node => node.NodeKind is "webforms-event" or "webforms-lifecycle" && NodeMatches(node, options.FromWebFormsEvent!))
                .Select(node => EntryFromNode(node, "webforms-event-root", BridgeStateForEntry(node, "webforms-event-root", bridgeStates), sources)));
        }

        if (!string.IsNullOrWhiteSpace(options.FromSymbol))
        {
            rows.AddRange(nodes
                .Where(node => node.NodeKind is "Symbol" or "Method" or "Type" or "EndpointRoute" or "EndpointClient" && NodeMatches(node, options.FromSymbol!))
                .Select(node => EntryFromNode(node, "symbol-root", BridgeStateForEntry(node, "symbol-root", bridgeStates), sources)));
        }

        if (!string.IsNullOrWhiteSpace(options.FromSource))
        {
            rows.AddRange(nodes
                .Where(node => string.Equals(node.SourceLabel, options.FromSource, StringComparison.OrdinalIgnoreCase))
                .Select(node => EntryFromNode(node, "source-root", BridgeStateForEntry(node, "source-root", bridgeStates), sources)));
        }

        var aligned = AlignedEntry(nodes, routeSelector ?? clientSelector ?? endpointSelector, bridgeStates, sources);
        if (aligned is not null)
        {
            rows.Add(aligned);
        }

        return rows
            .GroupBy(row => $"{row.EntryKind}\0{row.EntryId}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> EndpointMethodBridgeStates(
        IReadOnlyList<CombinedPathNode> nodes,
        IReadOnlyList<CombinedPathEdge> edges,
        IReadOnlyList<RouteFlowSource> sources)
    {
        var nodesById = nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var sourcesByLabel = sources
            .GroupBy(source => source.SourceLabel, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var bridgeEdgesByEndpointNodeId = edges
            .Where(edge => edge.EdgeKind == "fact-attached-to-symbol")
            .Where(edge => nodesById.TryGetValue(edge.FromNodeId, out var endpoint)
                && endpoint.NodeKind is "EndpointRoute" or "EndpointClient"
                && nodesById.TryGetValue(edge.ToNodeId, out var target)
                && IsMethodSymbolNode(target)
                && string.Equals(target.SourceIndexId, endpoint.SourceIndexId, StringComparison.Ordinal))
            .GroupBy(edge => edge.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var states = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var endpointNode in nodes
            .Where(node => node.NodeKind is "EndpointRoute" or "EndpointClient")
            .OrderBy(node => node.SourceLabel, StringComparer.Ordinal)
            .ThenBy(node => node.NodeKind, StringComparer.Ordinal)
            .ThenBy(node => node.FilePath, StringComparer.Ordinal)
            .ThenBy(node => node.StartLine ?? 0)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal))
        {
            var bridgeCount = bridgeEdgesByEndpointNodeId.GetValueOrDefault(endpointNode.NodeId, []).Length;
            states[endpointNode.NodeId] = bridgeCount switch
            {
                0 when EndpointBridgeCoverageReduced(endpointNode, sourcesByLabel) => "reduced-coverage",
                0 => "missing",
                1 => "method-symbol",
                _ => "ambiguous"
            };
        }

        return states;
    }

    private static bool EndpointBridgeCoverageReduced(
        CombinedPathNode endpointNode,
        IReadOnlyDictionary<string, RouteFlowSource> sourcesByLabel)
    {
        if (CoverageFor(endpointNode.EvidenceTier) == "ReducedCoverage")
        {
            return true;
        }

        if (!sourcesByLabel.TryGetValue(endpointNode.SourceLabel, out var source))
        {
            return true;
        }

        return !source.IdentityVerified
            || !KnownCommit(source.CommitSha)
            || string.Equals(source.AnalysisLevel, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source.BuildStatus, "failed", StringComparison.OrdinalIgnoreCase)
            || source.CoverageWarnings.Count > 0;
    }

    private sealed record EndpointCompositionResult(
        IReadOnlyList<CombinedPath> Paths,
        IReadOnlyList<RouteFlowGap> Gaps,
        bool Truncated);

    private sealed record EndpointTraversalState(
        IReadOnlyList<CombinedPathNode> Nodes,
        IReadOnlyList<CombinedPathEdge> Edges);

    private static EndpointCompositionResult BuildEndpointCompositionPaths(
        CombinedRouteFlowOptions options,
        string? routeSelector,
        string? clientSelector,
        string? endpointSelector,
        CombinedPathGraphInventory inventory,
        IReadOnlyDictionary<string, string> symbolKinds)
    {
        var selector = routeSelector ?? clientSelector ?? endpointSelector;
        if (selector is null)
        {
            return new EndpointCompositionResult([], [], false);
        }

        var requiredNodeKind = routeSelector is not null
            ? "EndpointRoute"
            : clientSelector is not null
                ? "EndpointClient"
                : null;
        var parsed = ParseNormalizedEndpoint(selector);
        var roots = inventory.Nodes
            .Where(node => node.NodeKind is "EndpointRoute" or "EndpointClient")
            .Where(node => SourceMatches(node, options.FromSource))
            .Where(node => requiredNodeKind is null || node.NodeKind == requiredNodeKind)
            .Where(node => string.Equals(node.NormalizedPathKey, parsed.PathKey, StringComparison.Ordinal)
                && CombinedDependencyReporter.MethodsCompatible(parsed.Method, node.HttpMethod ?? "ANY"))
            .OrderBy(node => node.SourceLabel, StringComparer.Ordinal)
            .ThenBy(node => node.NodeKind, StringComparer.Ordinal)
            .ThenBy(node => node.FilePath, StringComparer.Ordinal)
            .ThenBy(node => node.StartLine ?? 0)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        if (roots.Length == 0)
        {
            var oppositeEndpointNodes = requiredNodeKind is null
                ? []
                : inventory.Nodes
                    .Where(node => node.NodeKind is "EndpointRoute" or "EndpointClient")
                    .Where(node => SourceMatches(node, options.FromSource))
                    .Where(node => node.NodeKind != requiredNodeKind)
                    .Where(node => string.Equals(node.NormalizedPathKey, parsed.PathKey, StringComparison.Ordinal)
                        && CombinedDependencyReporter.MethodsCompatible(parsed.Method, node.HttpMethod ?? "ANY"))
                    .OrderBy(node => node.SourceLabel, StringComparer.Ordinal)
                    .ThenBy(node => node.NodeKind, StringComparer.Ordinal)
                    .ThenBy(node => node.FilePath, StringComparer.Ordinal)
                    .ThenBy(node => node.StartLine ?? 0)
                    .ThenBy(node => node.NodeId, StringComparer.Ordinal)
                    .ToArray();
            if (oppositeEndpointNodes.Length > 0)
            {
                var firstOppositeEndpoint = oppositeEndpointNodes[0];
                var supportingFactIds = oppositeEndpointNodes
                    .Select(node => node.CombinedFactId)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                return new EndpointCompositionResult([], [
                    new RouteFlowGap(
                        $"gap:endpoint-composition:MissingRouteRoot:{CombinedReportHelpers.Hash($"{requiredNodeKind}:{parsed.Method}:{parsed.PathKey}", 16)}",
                        "MissingRouteRoot",
                        "Matching endpoint context exists, but the requested route root evidence needed for endpoint composition is unavailable.",
                        GapRuleId,
                        EvidenceTiers.Tier4Unknown,
                        "ReducedCoverage",
                        SafeLabel(firstOppositeEndpoint.SourceLabel),
                        firstOppositeEndpoint.NodeId,
                        supportingFactIds,
                        ["Missing route-root evidence is an availability gap and does not prove endpoint absence."],
                        CombinedReportHelpers.SafePath(firstOppositeEndpoint.FilePath),
                        firstOppositeEndpoint.StartLine,
                        firstOppositeEndpoint.EndLine)
                ], false);
            }

            return new EndpointCompositionResult([], [], false);
        }

        var scannerVersionBySourceIndexId = inventory.Sources
            .GroupBy(source => source.SourceIndexId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(source => source.Label, StringComparer.Ordinal).First().ScannerVersion, StringComparer.Ordinal);
        var rootAmbiguityGaps = EndpointRootAmbiguityGaps(requiredNodeKind, parsed.Method, parsed.PathKey, roots, scannerVersionBySourceIndexId);
        var nodesById = inventory.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var outgoing = inventory.Edges
            .GroupBy(edge => edge.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(EndpointEdgeSortKey, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var endpointOutgoing = MergeEndpointOutgoing(
            outgoing,
            BuildParameterForwardSeedEdgesByMethodNodeId(inventory.Edges, nodesById));
        var callLikeEdgesByFromNodeId = inventory.Edges
            .Where(edge => edge.EdgeKind is "calls" or "creates" or "argument-passed" or "argument-flow" or "parameter-forward")
            .GroupBy(edge => edge.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var implementationRelationships = inventory.Edges
            .Where(IsImplementationRelationshipEdge)
            .Where(edge => nodesById.ContainsKey(edge.FromNodeId) && nodesById.ContainsKey(edge.ToNodeId))
            .ToArray();
        var staticDispatchCandidates = BuildRouteFlowStaticDispatchCandidates(nodesById, implementationRelationships, scannerVersionBySourceIndexId);
        var implementationCandidatesByInterface = staticDispatchCandidates.Edges
            .GroupBy(edge => edge.AbstractionSymbolId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(StaticDispatchCandidateSortKey, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var implementationCandidateGapsByInterface = staticDispatchCandidates.Gaps
            .Where(gap => !string.IsNullOrWhiteSpace(gap.NodeId))
            .GroupBy(gap => gap.NodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key!, group => group.OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var terminalNodeIds = ResolveEndpointCompositionTerminalNodeIds(options, inventory.Nodes, roots);
        if (terminalNodeIds.Count == 0)
        {
            return new EndpointCompositionResult(
                [],
                rootAmbiguityGaps
                    .Concat(TerminalSurfaceMissingGaps(roots, outgoing, callLikeEdgesByFromNodeId, nodesById))
                    .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
                    .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
                    .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
                    .ToArray(),
                false);
        }

        var gaps = new List<RouteFlowGap>(rootAmbiguityGaps);
        var paths = new List<CombinedPath>();
        var queue = new Queue<EndpointTraversalState>();
        var emittedPathsByRoot = roots.ToDictionary(root => root.NodeId, _ => 0, StringComparer.Ordinal);
        var bridgedMethodIdsByRoot = roots.ToDictionary(root => root.NodeId, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var root in roots)
        {
            var bridgeEdges = outgoing.GetValueOrDefault(root.NodeId, [])
                .Where(edge => edge.EdgeKind == "fact-attached-to-symbol")
                .Where(edge => nodesById.TryGetValue(edge.ToNodeId, out var target) && IsMethodSymbolNode(target) && string.Equals(target.SourceIndexId, root.SourceIndexId, StringComparison.Ordinal))
                .ToArray();
            if (bridgeEdges.Length == 0)
            {
                gaps.Add(RouteBridgeGap(
                    "MissingMethodSymbolBridge",
                    root,
                    "Endpoint route evidence could not be tied to a source-local method symbol.",
                    root.CombinedFactId is null ? [] : [root.CombinedFactId]));
                continue;
            }

            if (bridgeEdges.Length > 1)
            {
                gaps.Add(RouteBridgeGap(
                    "IdentityGap",
                    root,
                    "Endpoint route evidence matched multiple method-symbol bridge candidates; route-flow kept deterministic review-tier traversal.",
                    bridgeEdges.SelectMany(edge => edge.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()));
            }

            foreach (var edge in bridgeEdges)
            {
                bridgedMethodIdsByRoot[root.NodeId].Add(edge.ToNodeId);
                queue.Enqueue(new EndpointTraversalState([root, nodesById[edge.ToNodeId]], [edge]));
            }
        }

        var truncated = false;
        var emittedSequence = 0;

        while (queue.Count > 0 && paths.Count < options.MaxPaths)
        {
            if (queue.Count > options.MaxFrontier)
            {
                truncated = true;
                gaps.Add(TraversalBoundsGap("frontier", queue.Peek().Nodes[^1]));
                break;
            }

            var state = queue.Dequeue();
            var current = state.Nodes[^1];
            if (terminalNodeIds.Contains(current.NodeId) && state.Edges.Count > 0)
            {
                emittedSequence++;
                paths.Add(ToEndpointCompositionPath(emittedSequence, state));
                emittedPathsByRoot[state.Nodes[0].NodeId]++;
                continue;
            }

            if (state.Edges.Count >= options.MaxDepth)
            {
                truncated = true;
                gaps.Add(TraversalBoundsGap("depth", current));
                continue;
            }

            var expanded = false;
            var skippedCycleEdges = new List<CombinedPathEdge>();
            foreach (var edge in endpointOutgoing.GetValueOrDefault(current.NodeId, []))
            {
                if (!IsEndpointTraversableEdge(edge, current, state.Nodes[0])
                    || !nodesById.TryGetValue(edge.ToNodeId, out var target)
                    || !EndpointSourcesCompatible(current, target))
                {
                    continue;
                }

                if (state.Nodes.Any(node => node.NodeId == target.NodeId))
                {
                    skippedCycleEdges.Add(edge);
                    continue;
                }

                expanded = true;
                queue.Enqueue(new EndpointTraversalState([.. state.Nodes, target], [.. state.Edges, edge]));
            }

            var candidateEdges = new List<StaticDispatchCandidateEdge>();
            List<StaticDispatchCandidateEdge>? incompatibleCandidateEdges = null;
            foreach (var gap in implementationCandidateGapsByInterface.GetValueOrDefault(current.NodeId, []))
            {
                gaps.Add(RouteFlowStaticDispatchGap(gap, current));
            }

            foreach (var edge in implementationCandidatesByInterface.GetValueOrDefault(current.NodeId, []))
            {
                if (!nodesById.TryGetValue(edge.CandidateSymbolId, out var candidate))
                {
                    continue;
                }

                if (EndpointSourcesCompatible(current, candidate))
                {
                    candidateEdges.Add(edge);
                }
                else
                {
                    incompatibleCandidateEdges ??= [];
                    incompatibleCandidateEdges.Add(edge);
                }
            }

            var hasAnyImplementationCandidateEvidence = candidateEdges.Count > 0 || incompatibleCandidateEdges is { Count: > 0 };
            if (incompatibleCandidateEdges is { Count: > 0 })
            {
                gaps.Add(RuntimeBindingNotProvenGap(current, incompatibleCandidateEdges, scannerVersionBySourceIndexId));
            }

            if (candidateEdges.Count > 1)
            {
                var supportingFactIds = candidateEdges
                    .SelectMany(edge => edge.SupportingFactIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var candidateNodeIds = candidateEdges
                    .Select(edge => edge.CandidateSymbolId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                gaps.Add(new RouteFlowGap(
                    $"gap:endpoint-composition:AmbiguousImplementationCandidates:{HashCandidateSet("candidate-ambiguity", current.NodeId, candidateNodeIds, supportingFactIds, 16)}",
                    "AmbiguousImplementationCandidates",
                    "Interface member call has multiple static implementation candidates; candidate-dependent rows are review-tier.",
                    GapRuleId,
                    EvidenceTiers.Tier4Unknown,
                    "ReducedCoverage",
                    SafeLabel(current.SourceLabel),
                    current.NodeId,
                    supportingFactIds,
                    ["Endpoint route-flow composition uses bounded source-local static evidence and does not infer runtime behavior."],
                    CombinedReportHelpers.SafePath(current.FilePath),
                    current.StartLine,
                    current.EndLine));
            }

            foreach (var relationship in candidateEdges.Take(10))
            {
                var candidate = nodesById[relationship.CandidateSymbolId];
                if (state.Nodes.Any(node => node.NodeId == candidate.NodeId))
                {
                    continue;
                }

                expanded = true;
                queue.Enqueue(new EndpointTraversalState(
                    [.. state.Nodes, candidate],
                    [.. state.Edges, ReverseImplementationCandidateEdge(relationship, current.NodeId, candidate.NodeId)]));
            }

            if (!expanded && skippedCycleEdges.Count > 0)
            {
                gaps.Add(TraversalCycleGap(current, skippedCycleEdges, scannerVersionBySourceIndexId));
            }

            if (!expanded
                && callLikeEdgesByFromNodeId.TryGetValue(current.NodeId, out var currentCallEdges)
                && currentCallEdges.Length > 0)
            {
                gaps.Add(RouteBridgeGap(
                    "MissingCallEdge",
                    current,
                    "Route-flow traversal reached a static dead end before a terminal surface; no source-local downstream call edge was available from this method.",
                    currentCallEdges.SelectMany(edge => edge.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(20).ToArray()));
            }

            if (!expanded && !hasAnyImplementationCandidateEvidence && IsInterfaceMemberSymbol(current, symbolKinds))
            {
                var support = current.CombinedFactId is null ? [] : new[] { current.CombinedFactId };
                gaps.Add(RouteBridgeGap(
                    "MissingImplementationBridge",
                    current,
                    "Interface member call could not be bridged to a source-local implementation candidate.",
                    support));
                gaps.Add(RouteBridgeGap(
                    "ImplementationCandidateUnavailable",
                    current,
                    "No static implementation candidate was available for this interface member under source-local relationship evidence.",
                    support));
            }
        }

        if (paths.Count >= options.MaxPaths && queue.Count > 0)
        {
            truncated = true;
            gaps.Add(TraversalBoundsGap("path", queue.Peek().Nodes[^1]));
        }

        foreach (var root in roots.Where(root => emittedPathsByRoot.GetValueOrDefault(root.NodeId) == 0))
        {
            var bridgedMethodCallEdges = bridgedMethodIdsByRoot.GetValueOrDefault(root.NodeId, [])
                .SelectMany(methodNodeId => callLikeEdgesByFromNodeId.GetValueOrDefault(methodNodeId, []))
                .ToArray();
            if (bridgedMethodCallEdges.Length > 0)
            {
                var supportingFactIds = bridgedMethodCallEdges
                    .SelectMany(edge => edge.SupportingFactIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Take(20)
                    .ToArray();
                gaps.Add(RouteBridgeGap(
                    "MissingCallEdge",
                    root,
                    "Downstream call evidence exists on the bridged endpoint method, but route-flow could not connect it under static traversal rules.",
                    supportingFactIds));
                gaps.Add(RouteBridgeGap(
                    "DataSurfaceAttachmentMissing",
                    root,
                    "Route-flow found source-local downstream method evidence, but no matching terminal dependency/data surface could be connected.",
                    supportingFactIds));
            }
        }

        return new EndpointCompositionResult(
            paths
                .GroupBy(path => path.PathId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(path => ClassificationRank(ClassificationFromPath(path.Classification)))
                .ThenBy(path => path.Length)
                .ThenBy(path => path.Nodes.First().SourceLabel, StringComparer.Ordinal)
                .ThenBy(path => string.Join("|", path.Nodes.Select(node => node.DisplayName)), StringComparer.Ordinal)
                .ThenBy(path => path.PathId, StringComparer.Ordinal)
                .ToArray(),
            gaps
                .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
                .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
                .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
                .ToArray(),
            truncated);
    }

    private static IReadOnlyList<RouteFlowGap> EndpointRootAmbiguityGaps(
        string? requiredNodeKind,
        string method,
        string pathKey,
        IReadOnlyList<CombinedPathNode> roots,
        IReadOnlyDictionary<string, string> scannerVersionBySourceIndexId)
    {
        if (roots.Count <= 1)
        {
            return [];
        }

        var supportingFactIds = BoundedSortedDistinctValues(
            roots.Select(root => root.CombinedFactId),
            AmbiguitySupportingFactIdLimit,
            out var supportingFactReferenceCount);
        var firstRoot = roots[0];
        scannerVersionBySourceIndexId.TryGetValue(firstRoot.SourceIndexId, out var extractorVersion);
        var limitations = new List<string>
        {
            "Duplicate route roots make selector ownership ambiguous and do not prove runtime routing, traffic, or handler selection."
        };
        if (supportingFactReferenceCount > supportingFactIds.Length)
        {
            limitations.Add($"Supporting fact IDs are capped at {AmbiguitySupportingFactIdLimit} of {supportingFactReferenceCount} non-empty fact references; use source filters to narrow the selector.");
        }

        if (roots.Count > AmbiguityLargeRootNoticeThreshold)
        {
            limitations.Add($"Ambiguity spans {roots.Count} endpoint roots; the stable gap identity hashes the full deterministic root sequence without emitting every root ID.");
        }

        return [
            new RouteFlowGap(
                $"gap:endpoint-composition:SelectorNoMatch:{HashAmbiguousRootSet(requiredNodeKind, method, pathKey, roots, 16)}",
                "SelectorNoMatch",
                "Route-flow selector matched multiple endpoint roots; downstream rows remain static review context until the root is narrowed.",
                SelectorRuleId,
                EvidenceTiers.Tier4Unknown,
                "ReducedCoverage",
                SafeLabel(firstRoot.SourceLabel),
                firstRoot.NodeId,
                supportingFactIds,
                limitations,
                CombinedReportHelpers.SafePath(firstRoot.FilePath),
                firstRoot.StartLine,
                firstRoot.EndLine,
                SafeCommitSha(firstRoot.CommitSha),
                ExtractorName(firstRoot.RuleId),
                SafeSelector(extractorVersion) ?? "unknown")
        ];
    }

    private static string[] BoundedSortedDistinctValues(IEnumerable<string?> values, int limit, out int nonEmptyValueCount)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var selected = new List<string>();
        nonEmptyValueCount = 0;
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            nonEmptyValueCount++;
            if (selected.Count >= limit || !seen.Add(value))
            {
                continue;
            }

            selected.Add(value);
        }

        selected.Sort(StringComparer.Ordinal);
        return selected.ToArray();
    }

    private static string HashAmbiguousRootSet(
        string? requiredNodeKind,
        string method,
        string pathKey,
        IReadOnlyList<CombinedPathNode> roots,
        int length)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var separator = new byte[] { 0 };
        AppendHashValue(hash, separator, requiredNodeKind ?? "endpoint");
        AppendHashValue(hash, separator, method);
        AppendHashValue(hash, separator, pathKey);
        AppendHashValue(hash, separator, roots.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var root in roots)
        {
            AppendHashValue(hash, separator, root.NodeId);
        }

        var bytes = hash.GetHashAndReset();
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return text[..Math.Min(length, text.Length)];
    }

    private static void AppendHashValue(IncrementalHash hash, byte[] separator, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(value));
        }

        hash.AppendData(separator);
    }

    private static string HashCandidateSet(
        string scope,
        string nodeId,
        IReadOnlyList<string> candidateNodeIds,
        IReadOnlyList<string> supportingFactIds,
        int length)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var separator = new byte[] { 0 };
        AppendHashValue(hash, separator, scope);
        AppendHashValue(hash, separator, nodeId);
        AppendHashValue(hash, separator, candidateNodeIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var candidateNodeId in candidateNodeIds)
        {
            AppendHashValue(hash, separator, candidateNodeId);
        }

        AppendHashValue(hash, separator, supportingFactIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var supportingFactId in supportingFactIds)
        {
            AppendHashValue(hash, separator, supportingFactId);
        }

        var bytes = hash.GetHashAndReset();
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return text[..Math.Min(length, text.Length)];
    }

    private static IReadOnlyDictionary<string, CombinedPathEdge[]> BuildParameterForwardSeedEdgesByMethodNodeId(
        IReadOnlyList<CombinedPathEdge> edges,
        IReadOnlyDictionary<string, CombinedPathNode> nodesById)
    {
        var methodNodesBySourceAndDisplay = nodesById.Values
            .Where(IsMethodSymbolNode)
            .GroupBy(node => $"{node.SourceIndexId}\0{node.DisplayName}", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(node => node.NodeId, StringComparer.Ordinal).First(), StringComparer.Ordinal);
        var seeded = new Dictionary<string, List<CombinedPathEdge>>(StringComparer.Ordinal);
        foreach (var edge in edges.OrderBy(EndpointEdgeSortKey, StringComparer.Ordinal))
        {
            if (edge.EdgeKind != "parameter-forward"
                || !nodesById.TryGetValue(edge.FromNodeId, out var sourceParameter)
                || !TryParameterForwardMethodSymbol(sourceParameter.SymbolId ?? sourceParameter.DisplayName, out var methodSymbol)
                || !methodNodesBySourceAndDisplay.TryGetValue($"{sourceParameter.SourceIndexId}\0{methodSymbol}", out var methodNode)
                || methodNode.NodeId == sourceParameter.NodeId)
            {
                continue;
            }

            if (!seeded.TryGetValue(methodNode.NodeId, out var values))
            {
                values = [];
                seeded[methodNode.NodeId] = values;
            }

            values.Add(new CombinedPathEdge(
                $"route-flow-parameter-seed:{CombinedReportHelpers.Hash($"{methodNode.NodeId}:{edge.EdgeId}", 16)}",
                "argument-flow",
                methodNode.NodeId,
                sourceParameter.NodeId,
                edge.Classification,
                edge.RuleId,
                edge.EvidenceTier,
                edge.SupportingFactIds,
                edge.SupportingCombinedEdgeIds.Append(edge.EdgeId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                edge.FilePath,
                edge.StartLine,
                edge.EndLine));
        }

        return seeded.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.OrderBy(EndpointEdgeSortKey, StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, CombinedPathEdge[]> MergeEndpointOutgoing(
        IReadOnlyDictionary<string, CombinedPathEdge[]> outgoing,
        IReadOnlyDictionary<string, CombinedPathEdge[]> additional)
    {
        if (additional.Count == 0)
        {
            return outgoing;
        }

        var merged = outgoing.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.Ordinal);
        foreach (var pair in additional)
        {
            if (!merged.TryGetValue(pair.Key, out var values))
            {
                values = [];
                merged[pair.Key] = values;
            }

            values.AddRange(pair.Value);
        }

        return merged.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .GroupBy(edge => edge.EdgeId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(EndpointEdgeSortKey, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static bool TryParameterForwardMethodSymbol(string value, out string methodSymbol)
    {
        var separator = value.IndexOf("):", StringComparison.Ordinal);
        if (separator < 0)
        {
            methodSymbol = string.Empty;
            return false;
        }

        methodSymbol = value[..(separator + 1)];
        return !string.IsNullOrWhiteSpace(methodSymbol);
    }

    private static string EndpointEdgeSortKey(CombinedPathEdge edge)
    {
        return $"{EndpointEdgeRank(edge.EdgeKind):000}|{edge.FilePath}|{edge.StartLine:000000}|{edge.ToNodeId}|{edge.EdgeId}";
    }

    private static string StaticDispatchCandidateSortKey(StaticDispatchCandidateEdge edge)
    {
        return $"{EndpointEdgeRank(edge.ConsumerEdgeKind):000}|{edge.FilePath}|{edge.StartLine:000000}|{edge.CandidateSymbolId}|{edge.CandidateId}";
    }

    private static StaticDispatchCandidateBuildResult BuildRouteFlowStaticDispatchCandidates(
        IReadOnlyDictionary<string, CombinedPathNode> nodesById,
        IReadOnlyList<CombinedPathEdge> relationships,
        IReadOnlyDictionary<string, string> scannerVersionBySourceIndexId)
    {
        var relationshipNodeIds = relationships
            .Select(edge => edge.FromNodeId)
            .Concat(relationships.Select(edge => edge.ToNodeId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var relationshipNodes = relationshipNodeIds
            .Where(nodesById.ContainsKey)
            .ToDictionary(
                id => id,
                id =>
                {
                    var node = nodesById[id];
                    return new StaticDispatchCandidateNode(
                        node.NodeId,
                        node.NodeKind,
                        node.DisplayName ?? node.SymbolId ?? node.NodeId,
                        node.SourceIndexId,
                        node.SourceLabel,
                        node.CommitSha,
                        node.FilePath,
                        node.StartLine,
                        node.EndLine);
                },
                StringComparer.Ordinal);

        return StaticDispatchCandidateBuilder.Build(
            relationshipNodes,
            relationships.Select(edge => new StaticDispatchRelationshipEdge(
                edge.EdgeId,
                edge.EdgeKind,
                NormalizeStaticDispatchRelationshipKind(edge),
                edge.FromNodeId,
                edge.ToNodeId,
                edge.EvidenceTier,
                edge.SupportingFactIds,
                edge.SupportingCombinedEdgeIds,
                edge.FilePath,
                edge.StartLine,
                edge.EndLine)),
            sourceIndexId => !string.IsNullOrWhiteSpace(sourceIndexId) && scannerVersionBySourceIndexId.TryGetValue(sourceIndexId, out var version) ? version : null);
    }

    private static string? NormalizeStaticDispatchRelationshipKind(CombinedPathEdge edge)
    {
        return edge.EdgeKind switch
        {
            "implements" => "ImplementsInterfaceMember",
            "overrides" => "Overrides",
            _ when edge.EdgeKind.Contains("implement", StringComparison.OrdinalIgnoreCase) => "ImplementsInterfaceMember",
            _ when edge.EdgeKind.Contains("override", StringComparison.OrdinalIgnoreCase) => "Overrides",
            _ => edge.EdgeKind
        };
    }

    private static IReadOnlyList<RouteFlowGap> TerminalSurfaceMissingGaps(
        IReadOnlyList<CombinedPathNode> roots,
        IReadOnlyDictionary<string, CombinedPathEdge[]> outgoing,
        IReadOnlyDictionary<string, CombinedPathEdge[]> callLikeEdgesByFromNodeId,
        IReadOnlyDictionary<string, CombinedPathNode> nodesById)
    {
        var gaps = new List<RouteFlowGap>();
        foreach (var root in roots)
        {
            var bridgeEdges = outgoing.GetValueOrDefault(root.NodeId, [])
                .Where(edge => edge.EdgeKind == "fact-attached-to-symbol")
                .Where(edge => nodesById.TryGetValue(edge.ToNodeId, out var target) && IsMethodSymbolNode(target) && string.Equals(target.SourceIndexId, root.SourceIndexId, StringComparison.Ordinal))
                .ToArray();
            if (bridgeEdges.Length == 0)
            {
                gaps.Add(RouteBridgeGap(
                    "MissingMethodSymbolBridge",
                    root,
                    "Endpoint route evidence could not be tied to a source-local method symbol.",
                    root.CombinedFactId is null ? [] : [root.CombinedFactId]));
                continue;
            }

            var downstreamEdges = bridgeEdges
                .SelectMany(edge => callLikeEdgesByFromNodeId.GetValueOrDefault(edge.ToNodeId, []))
                .Where(edge => nodesById.TryGetValue(edge.ToNodeId, out var target) && EndpointSourcesCompatible(root, target))
                .ToArray();
            if (downstreamEdges.Length == 0)
            {
                continue;
            }

            gaps.Add(RouteBridgeGap(
                "DataSurfaceAttachmentMissing",
                root,
                "Route-flow found source-local downstream method evidence, but no matching terminal dependency/data surface could be connected.",
                downstreamEdges
                    .SelectMany(edge => edge.SupportingFactIds)
                    .Append(root.CombinedFactId)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Take(20)
                    .ToArray()));
        }

        return gaps
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool SourceMatches(CombinedPathNode node, string? sourceFilter)
    {
        return string.IsNullOrWhiteSpace(sourceFilter)
            || string.Equals(node.SourceLabel, sourceFilter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static int EndpointEdgeRank(string edgeKind)
    {
        return edgeKind switch
        {
            "fact-attached-to-symbol" => 0,
            "calls" => 1,
            "creates" => 2,
            "argument-passed" or "argument-flow" => 3,
            "parameter-forward" => 4,
            "surface-evidence" => 5,
            "symbol-reconciliation" => 6,
            "implements" or "overrides" => 7,
            _ => 99
        };
    }

    private static IReadOnlySet<string> ResolveEndpointCompositionTerminalNodeIds(CombinedRouteFlowOptions options, IReadOnlyList<CombinedPathNode> nodes, IReadOnlyList<CombinedPathNode> roots)
    {
        var surfaceKind = string.IsNullOrWhiteSpace(options.ToSurface) ? null : options.ToSurface.Trim();
        var surfaceName = string.IsNullOrWhiteSpace(options.SurfaceName) ? null : options.SurfaceName.Trim();
        var startFactIds = roots
            .Select(root => root.CombinedFactId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
        return nodes
            .Where(node => node.SurfaceKind is not null)
            .Where(node => surfaceKind is not null || IsDefaultEndpointCompositionTerminal(node, startFactIds))
            .Where(node => surfaceKind is null || string.Equals(node.SurfaceKind, surfaceKind, StringComparison.Ordinal))
            .Where(node => surfaceName is null || EndpointSurfaceNameMatches(node, surfaceName))
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsDefaultEndpointCompositionTerminal(CombinedPathNode node, IReadOnlySet<string> startFactIds)
    {
        if (node.SurfaceKind is "http-route" or "http-client")
        {
            return node.CombinedFactId is null || !startFactIds.Contains(node.CombinedFactId);
        }

        return node.SurfaceKind is not ("remoting-channel" or "remoting-object" or "remoting-api");
    }

    private static bool EndpointSurfaceNameMatches(CombinedPathNode node, string selector)
    {
        var values = new[]
        {
            node.SurfaceName ?? node.DisplayName,
            node.CombinedFactId,
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

    private static bool IsEndpointTraversableEdge(CombinedPathEdge edge, CombinedPathNode current, CombinedPathNode root)
    {
        if (edge.EdgeKind == "fact-attached-to-symbol")
        {
            return current.NodeId == root.NodeId;
        }

        return edge.EdgeKind is "calls"
            or "creates"
            or "argument-passed"
            or "argument-flow"
            or "parameter-forward"
            or "surface-evidence"
            or "symbol-reconciliation";
    }

    private static bool IsImplementationRelationshipEdge(CombinedPathEdge edge)
    {
        return edge.EdgeKind is "implements" or "overrides"
            || edge.EdgeKind.Contains("implement", StringComparison.OrdinalIgnoreCase)
            || edge.EdgeKind.Contains("override", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMethodSymbolNode(CombinedPathNode node)
    {
        return (node.NodeKind is "Method" or "Symbol") && !string.IsNullOrWhiteSpace(node.SymbolId ?? node.DisplayName);
    }

    private static bool EndpointSourcesCompatible(CombinedPathNode left, CombinedPathNode right)
    {
        return !string.IsNullOrWhiteSpace(left.SourceIndexId)
            && !string.IsNullOrWhiteSpace(right.SourceIndexId)
            && string.Equals(left.SourceIndexId, right.SourceIndexId, StringComparison.Ordinal);
    }

    private static bool IsInterfaceMemberSymbol(CombinedPathNode node, IReadOnlyDictionary<string, string> symbolKinds)
    {
        if (string.IsNullOrWhiteSpace(node.SourceIndexId))
        {
            return false;
        }

        foreach (var symbol in new[] { node.SymbolId, node.DisplayName }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!))
        {
            if (symbolKinds.TryGetValue(SymbolKindKey(node.SourceIndexId, symbol), out var kind)
                && kind.Contains("Interface", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string SymbolKindKey(string sourceIndexId, string displayName)
    {
        return $"{sourceIndexId}\0{displayName}";
    }

    private static CombinedPathEdge ReverseImplementationCandidateEdge(StaticDispatchCandidateEdge relationship, string interfaceNodeId, string implementationNodeId)
    {
        var supportingEdges = relationship.SupportingEdgeIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new CombinedPathEdge(
            $"route-flow-interface-bridge:{CombinedReportHelpers.Hash($"{relationship.CandidateId}:{interfaceNodeId}:{implementationNodeId}", 16)}",
            relationship.ConsumerEdgeKind == "override-candidate" ? "overrides" : "implements",
            interfaceNodeId,
            implementationNodeId,
            CombinedDependencyPathClassifications.NeedsReviewStaticPath,
            relationship.RuleId,
            relationship.EvidenceTier,
            relationship.SupportingFactIds,
            supportingEdges,
            relationship.FilePath,
            relationship.StartLine,
            relationship.EndLine);
    }

    private static CombinedPath ToEndpointCompositionPath(int sequence, EndpointTraversalState state)
    {
        var classification = EndpointPathClassification(state.Edges);
        return new CombinedPath(
            $"route-flow:path:{sequence:0000}:{CombinedReportHelpers.Hash(string.Join("|", state.Edges.Select(edge => edge.EdgeId)), 16)}",
            classification,
            classification == CombinedDependencyPathClassifications.StrongStaticPath ? "High" : "Medium",
            state.Edges.Count,
            state.Nodes[0].NodeId,
            state.Nodes[^1].NodeId,
            state.Nodes,
            state.Edges,
            state.Edges.SelectMany(edge => edge.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            state.Edges.SelectMany(edge => edge.SupportingCombinedEdgeIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            state.Edges.Any(edge => edge.EdgeId.StartsWith("route-flow-interface-bridge:", StringComparison.Ordinal))
                ? [new CombinedPathNote("StaticImplementationCandidate", "Interface implementation candidate rows are static review evidence and do not prove runtime dependency-injection target selection.")]
                : []);
    }

    private static string EndpointPathClassification(IReadOnlyList<CombinedPathEdge> edges)
    {
        if (edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier4Unknown))
        {
            return CombinedDependencyPathClassifications.UnknownAnalysisGap;
        }

        if (edges.Any(edge => edge.EdgeId.StartsWith("route-flow-interface-bridge:", StringComparison.Ordinal))
            || edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual)
            || edges.Any(edge => edge.EdgeKind == "symbol-reconciliation"))
        {
            return CombinedDependencyPathClassifications.NeedsReviewStaticPath;
        }

        return edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier2Structural)
            ? CombinedDependencyPathClassifications.ProbableStaticPath
            : CombinedDependencyPathClassifications.StrongStaticPath;
    }

    private static RouteFlowGap RouteBridgeGap(string gapKind, CombinedPathNode node, string message, IReadOnlyList<string> supportingFactIds)
    {
        return new RouteFlowGap(
            $"gap:endpoint-composition:{gapKind}:{CombinedReportHelpers.Hash($"{node.NodeId}:{message}:{string.Join("|", supportingFactIds)}", 16)}",
            gapKind,
            message,
            GapRuleId,
            EvidenceTiers.Tier4Unknown,
            "ReducedCoverage",
            SafeLabel(node.SourceLabel),
            node.NodeId,
            supportingFactIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ["Endpoint route-flow composition uses bounded source-local static evidence and does not infer runtime behavior."],
            CombinedReportHelpers.SafePath(node.FilePath),
            node.StartLine,
            node.EndLine);
    }

    private static RouteFlowGap RuntimeBindingNotProvenGap(
        CombinedPathNode node,
        IReadOnlyList<StaticDispatchCandidateEdge> incompatibleCandidateEdges,
        IReadOnlyDictionary<string, string> scannerVersionBySourceIndexId)
    {
        var edgeIds = incompatibleCandidateEdges
            .Select(edge => edge.CandidateId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var supportingFactIds = incompatibleCandidateEdges
            .SelectMany(edge => edge.SupportingFactIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(20)
            .ToArray();
        var firstEdge = incompatibleCandidateEdges
            .OrderBy(StaticDispatchCandidateSortKey, StringComparer.Ordinal)
            .First();
        return new RouteFlowGap(
            $"gap:runtime-binding:cross-source:{HashCandidateSet("runtime-binding", node.NodeId, edgeIds, supportingFactIds, 16)}",
            "RuntimeBindingNotProven",
            "Cross-source or cross-language implementation candidate evidence is blocked in route-flow v1; runtime binding is not proven.",
            GapRuleId,
            EvidenceTiers.Tier4Unknown,
            "ReducedCoverage",
            SafeLabel(node.SourceLabel),
            node.NodeId,
            supportingFactIds,
            ["Cross-source, cross-language, dependency-injection, service-locator, factory, reflection, and dynamic-dispatch implementation bindings require a future deterministic rule."],
            CombinedReportHelpers.SafePath(node.FilePath),
            node.StartLine,
            node.EndLine,
            SafeCommitSha(node.CommitSha),
            ExtractorName(firstEdge.RuleId),
            scannerVersionBySourceIndexId.TryGetValue(node.SourceIndexId, out var extractorVersion)
                ? SafeSelector(extractorVersion) ?? "unknown"
                : "unknown");
    }

    private static RouteFlowGap RouteFlowStaticDispatchGap(StaticDispatchCandidateGap gap, CombinedPathNode node)
    {
        return new RouteFlowGap(
            $"gap:endpoint-composition:{gap.GapKind}:{CombinedReportHelpers.Hash($"{gap.GapId}:{node.NodeId}", 16)}",
            gap.GapKind,
            gap.Message,
            GapRuleId,
            gap.EvidenceTier,
            "ReducedCoverage",
            SafeLabel(gap.SourceLabel ?? node.SourceLabel),
            node.NodeId,
            node.CombinedFactId is null ? [] : [node.CombinedFactId],
            [
                "Static dispatch candidate fan-out is deterministically capped and remains review-tier.",
                "Candidate implementation rows identify compiler-known candidates only and do not prove runtime dependency-injection targets."
            ],
            CombinedReportHelpers.SafePath(gap.FilePath ?? node.FilePath),
            gap.StartLine ?? node.StartLine,
            gap.EndLine ?? node.EndLine,
            SafeCommitSha(gap.CommitSha ?? node.CommitSha),
            ExtractorName(gap.RuleId),
            SafeSelector(gap.ExtractorVersion) ?? "unknown");
    }

    private static RouteFlowGap TraversalBoundsGap(string reason, CombinedPathNode node)
    {
        return new RouteFlowGap(
            $"gap:endpoint-composition:TraversalBounds:{CombinedReportHelpers.Hash($"{reason}:{node.NodeId}", 16)}",
            "TraversalBounds",
            $"Endpoint route-flow traversal stopped at the {reason} bound; returned rows are partial.",
            GapRuleId,
            EvidenceTiers.Tier4Unknown,
            "ReducedCoverage",
            SafeLabel(node.SourceLabel),
            node.NodeId,
            node.CombinedFactId is null ? [] : [node.CombinedFactId],
            ["Traversal bounds make route-flow output partial and cap clean absence conclusions."],
            CombinedReportHelpers.SafePath(node.FilePath),
            node.StartLine,
            node.EndLine);
    }

    private static RouteFlowGap TraversalCycleGap(
        CombinedPathNode node,
        IReadOnlyList<CombinedPathEdge> skippedCycleEdges,
        IReadOnlyDictionary<string, string> scannerVersionBySourceIndexId)
    {
        var supportingFactIds = skippedCycleEdges
            .SelectMany(edge => edge.SupportingFactIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(AmbiguitySupportingFactIdLimit)
            .ToArray();
        var firstEdge = skippedCycleEdges
            .OrderBy(EndpointEdgeSortKey, StringComparer.Ordinal)
            .First();
        return new RouteFlowGap(
            $"gap:endpoint-composition:TraversalBounds:{HashTraversalCycleEdges(node.NodeId, skippedCycleEdges, 16)}",
            "TraversalBounds",
            "Endpoint route-flow traversal skipped cycle edges before reaching a terminal surface; returned rows are partial.",
            GapRuleId,
            EvidenceTiers.Tier4Unknown,
            "ReducedCoverage",
            SafeLabel(node.SourceLabel),
            node.NodeId,
            supportingFactIds,
            ["Traversal cycle detection prevents infinite static expansion and caps clean absence conclusions."],
            CombinedReportHelpers.SafePath(firstEdge.FilePath),
            firstEdge.StartLine,
            firstEdge.EndLine,
            SafeCommitSha(node.CommitSha),
            ExtractorName(firstEdge.RuleId),
            scannerVersionBySourceIndexId.TryGetValue(node.SourceIndexId, out var extractorVersion)
                ? SafeSelector(extractorVersion) ?? "unknown"
                : "unknown");
    }

    private static string HashTraversalCycleEdges(string nodeId, IReadOnlyList<CombinedPathEdge> skippedCycleEdges, int length)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var separator = new byte[] { 0 };
        var edgeIds = skippedCycleEdges
            .Select(edge => edge.EdgeId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        AppendHashValue(hash, separator, "cycle");
        AppendHashValue(hash, separator, nodeId);
        AppendHashValue(hash, separator, edgeIds.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var edgeId in edgeIds)
        {
            AppendHashValue(hash, separator, edgeId);
        }

        var bytes = hash.GetHashAndReset();
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return text[..Math.Min(length, text.Length)];
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

    private static RouteFlowEntryEvidence? AlignedEntry(
        IReadOnlyList<CombinedPathNode> nodes,
        string? selector,
        IReadOnlyDictionary<string, string> bridgeStates,
        IReadOnlyList<RouteFlowSource> sources)
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
        return EntryFromNode(first, "aligned-route-pair", BridgeStateForEntry(first, "aligned-route-pair", bridgeStates), sources) with
        {
            EntryId = $"entry:aligned:{CombinedReportHelpers.Hash($"{parsed.Method}\0{parsed.PathKey}", 16)}",
            Classification = RouteFlowClassifications.ProbableStaticRouteFlow
        };
    }

    private static IEnumerable<RouteFlowEntryEvidence> EndpointEntries(
        IReadOnlyList<CombinedPathNode> nodes,
        string selector,
        string? nodeKind,
        string entryKind,
        IReadOnlyDictionary<string, string> bridgeStates,
        IReadOnlyList<RouteFlowSource> sources)
    {
        var parsed = ParseNormalizedEndpoint(selector);
        return nodes
            .Where(node => node.NodeKind is "EndpointRoute" or "EndpointClient")
            .Where(node => nodeKind is null || node.NodeKind == nodeKind)
            .Where(node => string.Equals(node.NormalizedPathKey, parsed.PathKey, StringComparison.Ordinal)
                && CombinedDependencyReporter.MethodsCompatible(parsed.Method, node.HttpMethod ?? "ANY"))
            .Select(node => EntryFromNode(node, entryKind, BridgeStateForEntry(node, entryKind, bridgeStates), sources));
    }

    private static RouteFlowEntryEvidence EntryFromNode(CombinedPathNode node, string entryKind, string bridgeState, IReadOnlyList<RouteFlowSource> sources)
    {
        var method = node.HttpMethod ?? string.Empty;
        var pathKey = node.NormalizedPathKey ?? string.Empty;
        return new RouteFlowEntryEvidence(
            $"entry:{entryKind}:{CombinedReportHelpers.Hash(node.NodeId, 16)}",
            entryKind,
            method,
            pathKey,
            pathKey,
            SafeSelector(node.SymbolId ?? node.DisplayName),
            ClassificationForTier(node.EvidenceTier),
            CoverageFor(node.EvidenceTier),
            bridgeState,
            EvidenceFromNode(EntryRuleId, node, node.CombinedFactId is null ? [] : [node.CombinedFactId], [], [node.RuleId ?? EntryRuleId], sources));
    }

    private static string BridgeStateForEntry(CombinedPathNode node, string entryKind, IReadOnlyDictionary<string, string> bridgeStates)
    {
        if (bridgeStates.TryGetValue(node.NodeId, out var bridgeState))
        {
            return bridgeState;
        }

        if (entryKind is "webforms-event-root" or "source-root")
        {
            return "path-node";
        }

        if (IsMethodSymbolNode(node))
        {
            return "path-node";
        }

        return "symbol-fallback";
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
                var rowKind = edge is null ? "entry" : RowKind(edge, previous);
                if (edge is not null && rowKind == "interface-implementation-candidate" && previous is not null && !EndpointSourcesCompatible(previous, node))
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

                var classification = WeakestClassification(ClassifyRouteRow(edge, node, sources), ClassificationFromPath(path.Classification));
                rows.Add(new RouteFlowRow(
                    StableFlowRowId(path, index, node, edge),
                    sequence,
                    rowKind,
                    edge is null ? "none" : EdgeKind(edge, previous),
                    FlowDisplaySymbol(index == 0 ? node : path.Nodes[index - 1]),
                    FlowDisplaySymbol(node),
                    classification,
                    CoverageFor(edge?.EvidenceTier ?? node.EvidenceTier),
                    edge?.FromNodeId,
                    edge?.ToNodeId ?? node.NodeId,
                    edge is null
                        ? EvidenceFromNode(PathRuleId, node, node.CombinedFactId is null ? [] : [node.CombinedFactId], [], [node.RuleId ?? PathRuleId], sources)
                        : EvidenceFromEdge(RouteRuleIdForRowKind(rowKind), edge, [edge.RuleId], sources, previous?.SourceLabel, previous?.CommitSha)));

            }
        }

        return rows
            .GroupBy(row => row.RowId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static string RouteRuleIdForRowKind(string rowKind)
    {
        return rowKind switch
        {
            "endpoint-method-bridge" => EntryRuleId,
            "interface-implementation-candidate" => InterfaceBridgeRuleId,
            _ => PathRuleId
        };
    }

    private static string FlowDisplaySymbol(CombinedPathNode node)
    {
        var preferred = node.DisplayName;
        var safe = SafeSelector(preferred);
        if (safe is not null && !safe.StartsWith("redacted-hash:", StringComparison.Ordinal))
        {
            return safe;
        }

        if (!string.IsNullOrWhiteSpace(node.SurfaceKind))
        {
            foreach (var candidate in new[]
            {
                node.SurfaceName,
                string.IsNullOrWhiteSpace(node.ShapeHash) ? null : $"shape:{node.ShapeHash}",
                string.IsNullOrWhiteSpace(node.TextHash) ? null : $"text-hash:{node.TextHash}",
                node.OperationName is null ? null : $"{node.SurfaceKind}:{node.OperationName}",
                node.SurfaceKind
            })
            {
                safe = SafeSelector(candidate);
                if (safe is not null && !safe.StartsWith("redacted-hash:", StringComparison.Ordinal))
                {
                    return safe;
                }
            }
        }

        return safe ?? "redacted";
    }

    private static string StableFlowRowId(CombinedPath path, int index, CombinedPathNode node, CombinedPathEdge? edge)
    {
        if (edge is null)
        {
            return $"row:entry:{CombinedReportHelpers.Hash($"{node.SourceIndexId}:{node.NodeId}:{node.CombinedFactId}:{node.FilePath}:{node.StartLine}", 24)}";
        }

        var key = string.Join("|", new[]
        {
            edge.EdgeKind,
            edge.FromNodeId,
            edge.ToNodeId,
            edge.RuleId,
            edge.EvidenceTier,
            edge.FilePath,
            edge.StartLine?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            edge.EndLine?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string.Join(",", edge.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal)),
            string.Join(",", edge.SupportingCombinedEdgeIds.OrderBy(value => value, StringComparer.Ordinal))
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        _ = path;
        _ = index;
        return $"row:edge:{CombinedReportHelpers.Hash(key, 24)}";
    }

    private static IReadOnlyList<RouteFlowDependencySurface> BuildDependencySurfaces(IReadOnlyList<CombinedPath> paths, IReadOnlyList<RouteFlowSource> sources)
    {
        return paths
            .SelectMany(path => path.Nodes.Select(node => (Path: path, Node: node)))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Node.SurfaceKind))
            .Select(pair =>
            {
                var node = pair.Node;
                var stable = StableSurfaceKey(node);
                return new RouteFlowDependencySurface(
                    $"surface:{CombinedReportHelpers.Hash(stable, 16)}",
                    node.SurfaceKind!,
                    node.SurfaceName ?? node.DisplayName,
                    stable,
                    WeakestClassification(ClassifyRouteRow(null, node, sources), ClassificationFromPath(pair.Path.Classification)),
                    CoverageFor(node.EvidenceTier),
                    Metadata(
                        ("operationName", node.OperationName),
                        ("tableNameHash", string.IsNullOrWhiteSpace(node.TableName) ? null : CombinedReportHelpers.Hash(node.TableName!, 16)),
                        ("columnNamesHash", string.IsNullOrWhiteSpace(node.ColumnNames) ? null : CombinedReportHelpers.Hash(node.ColumnNames!, 16)),
                        ("sourceKind", node.SourceKind),
                        ("shapeHash", node.ShapeHash),
                        ("textHash", node.TextHash),
                        ("packageName", node.PackageName),
                        ("configKeyHash", string.IsNullOrWhiteSpace(node.ConfigKey) ? null : CombinedReportHelpers.Hash(node.ConfigKey!, 16)),
                        ("surfaceSubtype", node.SurfaceSubtype)),
                    EvidenceFromNode(DependencySurfaceRuleId, node, node.CombinedFactId is null ? [] : [node.CombinedFactId], [], [node.RuleId ?? DependencySurfaceRuleId], sources),
                    node.SurfaceSubtype);
            })
            .GroupBy(surface => surface.StableKey, StringComparer.Ordinal)
            .Select(group => group.OrderBy(surface => ClassificationRank(surface.Classification)).ThenBy(surface => surface.SurfaceId, StringComparer.Ordinal).First())
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
                    ("surfaceSubtype", node.SurfaceSubtype),
                    ("operationName", node.OperationName),
                    ("sourceKind", node.SourceKind),
                    ("shapeHash", node.ShapeHash)),
                EvidenceFromNode(LogicSurfaceRuleId, node, node.CombinedFactId is null ? [] : [node.CombinedFactId], [], [node.RuleId ?? LogicSurfaceRuleId], sources)));
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
                EvidenceFromEdge(LogicSurfaceRuleId, edge, [edge.RuleId], sources)));
        }

        return rows
            .GroupBy(row => row.LogicRowId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private sealed record ProjectionResult(
        IReadOnlyList<RouteFlowLogicRow> Rows,
        IReadOnlyList<RouteFlowGap> Gaps);

    private sealed record ArgumentFlowProjectionRow(
        string CombinedFactId,
        string SourceIndexId,
        string SourceLabel,
        string CommitSha,
        string EvidenceTier,
        string RuleId,
        string? CallerSymbol,
        string CalleeSymbol,
        int ParameterOrdinal,
        string ParameterName,
        string? ParameterType,
        int ArgumentOrdinal,
        string? ArgumentExpressionKind,
        string? ArgumentExpressionHash,
        string? ArgumentSymbol,
        string? ArgumentSymbolKind,
        string FilePath,
        int StartLine,
        int EndLine);

    private sealed record FactSymbolProjectionRow(
        string CombinedFactId,
        string CombinedSymbolId,
        string SourceIndexId,
        string SourceLabel,
        string CommitSha,
        string FactType,
        string RuleId,
        string EvidenceTier,
        string? SourceSymbol,
        string? TargetSymbol,
        string SymbolDisplayName,
        string SymbolKind,
        string Role,
        string FilePath,
        int StartLine,
        int EndLine,
        IReadOnlyDictionary<string, string> Properties);

    private static async Task<ProjectionResult> BuildProjectionRowsAsync(
        string indexPath,
        IReadOnlyList<CombinedPath> selectedPaths,
        IReadOnlyList<string> selectedSourceIndexIds,
        IReadOnlyList<RouteFlowRow> flowRows,
        IReadOnlyList<RouteFlowSource> sources,
        CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = indexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var rows = new List<RouteFlowLogicRow>();
        var gaps = new List<RouteFlowGap>();
        var pathModel = BuildSelectedPathModel(selectedPaths, flowRows);

        if (await TableExistsAsync(connection, "combined_argument_flows", cancellationToken))
        {
            var argumentRows = selectedSourceIndexIds.Count == 0 || pathModel.PairCandidates.Count == 0
                ? []
                : await ReadArgumentFlowProjectionRowsAsync(connection, pathModel.PairCandidates, cancellationToken);
            var projected = 0;
            foreach (var row in argumentRows)
            {
                var attached = pathModel.FlowRowForPair(row.SourceIndexId, row.CallerSymbol, row.CalleeSymbol);
                if (attached is null)
                {
                    continue;
                }

                projected++;
                var parameterName = SafeParameterName(row.ParameterName, out var parameterNameRedacted);
                rows.Add(new RouteFlowLogicRow(
                    $"logic:argument-projection:{CombinedReportHelpers.Hash($"{row.CombinedFactId}\0{attached.RowId}", 16)}",
                    "argument-flow",
                    $"parameter:{parameterName}",
                    "argument-projection",
                    attached.RowId,
                    WeakestClassification(ClassificationForTier(row.EvidenceTier), RouteFlowClassifications.ProbableStaticRouteFlow),
                    CoverageFor(row.EvidenceTier),
                    Metadata(
                        ("parameterName", parameterName),
                        ("parameterType", row.ParameterType),
                        ("parameterOrdinal", row.ParameterOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        ("argumentOrdinal", row.ArgumentOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        ("argumentExpressionKind", row.ArgumentExpressionKind),
                        ("argumentExpressionHash", row.ArgumentExpressionHash),
                        ("argumentSymbolKind", row.ArgumentSymbolKind),
                        ("argumentSymbolHash", string.IsNullOrWhiteSpace(row.ArgumentSymbol) ? null : CombinedReportHelpers.Hash(row.ArgumentSymbol!, 16))),
                    EvidenceFromProjection(
                        ArgumentProjectionRuleId,
                        row.EvidenceTier,
                        row.SourceLabel,
                        row.CommitSha,
                        row.FilePath,
                        row.StartLine,
                        row.EndLine,
                        row.RuleId,
                        [row.CombinedFactId],
                        attached.Evidence.SupportingEdgeIds,
                        sources,
                        redactionApplied: parameterNameRedacted || !string.IsNullOrWhiteSpace(row.ArgumentSymbol))));
            }

            var unprojectedArgumentFactIds = argumentRows.Count > 0
                ? argumentRows.Select(row => row.CombinedFactId).ToArray()
                : await ReadArgumentFlowProjectionFactIdsAsync(connection, selectedSourceIndexIds, cancellationToken);
            if (unprojectedArgumentFactIds.Count > 0 && projected == 0)
            {
                gaps.Add(ProjectionUnavailableGap(
                    "argument",
                    "ArgumentProjectionUnavailable",
                    "Argument-flow rows were present, but none could be connected to the selected route-flow path by direct static call evidence.",
                    ArgumentProjectionRuleId,
                    unprojectedArgumentFactIds));
            }
        }

        if (await TableExistsAsync(connection, "combined_fact_symbols", cancellationToken))
        {
            var factSymbolRows = selectedSourceIndexIds.Count == 0 || pathModel.SymbolCandidates.Count == 0
                ? []
                : await ReadFactSymbolProjectionRowsAsync(connection, pathModel.SymbolCandidates, cancellationToken);
            var projected = 0;
            var projectableFactIds = new List<string>();
            var unsupportedAttachedFactIds = new List<string>();
            foreach (var row in factSymbolRows)
            {
                var attached = pathModel.FlowRowForSymbol(row.SourceIndexId, row.CombinedSymbolId, row.SymbolDisplayName, row.SourceSymbol);
                if (attached is null)
                {
                    continue;
                }

                if (!ShouldProjectFactSymbol(row))
                {
                    unsupportedAttachedFactIds.Add(row.CombinedFactId);
                    continue;
                }

                projectableFactIds.Add(row.CombinedFactId);
                projected++;
                var kind = FactSymbolLogicKind(row);
                rows.Add(new RouteFlowLogicRow(
                    $"logic:fact-symbol-projection:{CombinedReportHelpers.Hash($"{row.CombinedFactId}\0{row.CombinedSymbolId}\0{row.Role}", 16)}",
                    kind,
                    FactSymbolDisplayName(row, kind),
                    "fact-symbol-projection",
                    attached.RowId,
                    WeakestClassification(ClassificationForTier(row.EvidenceTier), RouteFlowClassifications.ProbableStaticRouteFlow),
                    CoverageFor(row.EvidenceTier),
                    FactSymbolMetadata(row, kind),
                    EvidenceFromProjection(
                        FactSymbolProjectionRuleId,
                        row.EvidenceTier,
                        row.SourceLabel,
                        row.CommitSha,
                        row.FilePath,
                        row.StartLine,
                        row.EndLine,
                        row.RuleId,
                        [row.CombinedFactId],
                        attached.Evidence.SupportingEdgeIds,
                        sources,
                        redactionApplied: FactSymbolRedactionApplied(row))));
            }

            var sourceFactSymbolIds = factSymbolRows.Count == 0
                ? await ReadFactSymbolProjectionFactIdsAsync(connection, selectedSourceIndexIds, cancellationToken)
                : [];
            var factSymbolGapIds = projectableFactIds.Count > 0
                ? projectableFactIds
                : sourceFactSymbolIds;
            if (factSymbolGapIds.Count > 0 && projected == 0)
            {
                gaps.Add(ProjectionUnavailableGap(
                    "fact-symbol",
                    "FactSymbolProjectionUnavailable",
                    "Fact-symbol rows were present, but none could be connected to the selected route-flow path by source-local symbol evidence.",
                    FactSymbolProjectionRuleId,
                    factSymbolGapIds));
            }

            if (unsupportedAttachedFactIds.Count > 0)
            {
                gaps.Add(ProjectionUnavailableGap(
                    "fact-symbol-unsupported",
                    "FactSymbolUnsupportedTypeSkipped",
                    "Fact-symbol rows were present, but this route-flow slice does not project their fact types directly.",
                    FactSymbolProjectionRuleId,
                    unsupportedAttachedFactIds));
            }
        }

        return new ProjectionResult(
            rows
                .GroupBy(row => row.LogicRowId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(row => row.LogicKind, StringComparer.Ordinal)
                .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
                .ThenBy(row => row.LogicRowId, StringComparer.Ordinal)
                .ToArray(),
            gaps);
    }

    private sealed class SelectedPathModel
    {
        private readonly Dictionary<string, RouteFlowRow> _flowRowsByPair;
        private readonly Dictionary<string, RouteFlowRow> _flowRowsBySymbol;

        public SelectedPathModel(
            Dictionary<string, RouteFlowRow> flowRowsByPair,
            Dictionary<string, RouteFlowRow> flowRowsBySymbol,
            IReadOnlyList<SymbolPairCandidate> pairCandidates,
            IReadOnlyList<SymbolCandidate> symbolCandidates)
        {
            _flowRowsByPair = flowRowsByPair;
            _flowRowsBySymbol = flowRowsBySymbol;
            PairCandidates = pairCandidates;
            SymbolCandidates = symbolCandidates;
        }

        public IReadOnlyList<SymbolPairCandidate> PairCandidates { get; }

        public IReadOnlyList<SymbolCandidate> SymbolCandidates { get; }

        public RouteFlowRow? FlowRowForPair(string sourceIndexId, string? callerSymbol, string? calleeSymbol)
        {
            if (string.IsNullOrWhiteSpace(callerSymbol) || string.IsNullOrWhiteSpace(calleeSymbol))
            {
                return null;
            }

            return _flowRowsByPair.GetValueOrDefault(SymbolPairKey(sourceIndexId, callerSymbol!, calleeSymbol!));
        }

        public RouteFlowRow? FlowRowForSymbol(string sourceIndexId, params string?[] symbols)
        {
            foreach (var symbol in symbols)
            {
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    continue;
                }

                if (_flowRowsBySymbol.TryGetValue(SymbolKey(sourceIndexId, symbol!), out var row))
                {
                    return row;
                }
            }

            return null;
        }
    }

    private static SelectedPathModel BuildSelectedPathModel(IReadOnlyList<CombinedPath> selectedPaths, IReadOnlyList<RouteFlowRow> flowRows)
    {
        var nodesById = selectedPaths
            .SelectMany(path => path.Nodes)
            .GroupBy(node => node.NodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var flowRowsByNode = flowRows
            .Where(row => !string.IsNullOrWhiteSpace(row.ToNodeId))
            .GroupBy(row => row.ToNodeId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.Sequence).First(), StringComparer.Ordinal);
        var byPair = new Dictionary<string, RouteFlowRow>(StringComparer.Ordinal);
        var bySymbol = new Dictionary<string, RouteFlowRow>(StringComparer.Ordinal);
        var pairCandidates = new List<SymbolPairCandidate>();
        var symbolCandidates = new List<SymbolCandidate>();

        foreach (var path in selectedPaths)
        {
            foreach (var node in path.Nodes)
            {
                var attached = flowRowsByNode.GetValueOrDefault(node.NodeId);
                if (attached is null)
                {
                    continue;
                }

                AddSymbol(bySymbol, symbolCandidates, node.SourceIndexId, node.SymbolId, attached);
                if (string.IsNullOrWhiteSpace(node.SymbolId))
                {
                    AddSymbol(bySymbol, symbolCandidates, node.SourceIndexId, node.DisplayName, attached);
                }
            }

            foreach (var edge in path.Edges)
            {
                if (!nodesById.TryGetValue(edge.FromNodeId, out var from) || !nodesById.TryGetValue(edge.ToNodeId, out var to))
                {
                    continue;
                }

                var attached = flowRows.FirstOrDefault(row => row.Evidence.SupportingEdgeIds.Contains(edge.EdgeId, StringComparer.Ordinal))
                    ?? flowRowsByNode.GetValueOrDefault(edge.ToNodeId);
                if (attached is null)
                {
                    continue;
                }

                AddPair(byPair, pairCandidates, from.SourceIndexId, from.SymbolId ?? from.DisplayName, to.SymbolId ?? to.DisplayName, attached);
                AddPair(byPair, pairCandidates, from.SourceIndexId, from.DisplayName, to.DisplayName, attached);
                AddSymbol(bySymbol, symbolCandidates, from.SourceIndexId, from.SymbolId, attached);
                AddSymbol(bySymbol, symbolCandidates, to.SourceIndexId, to.SymbolId, attached);
                if (string.IsNullOrWhiteSpace(from.SymbolId))
                {
                    AddSymbol(bySymbol, symbolCandidates, from.SourceIndexId, from.DisplayName, attached);
                }

                if (string.IsNullOrWhiteSpace(to.SymbolId))
                {
                    AddSymbol(bySymbol, symbolCandidates, to.SourceIndexId, to.DisplayName, attached);
                }
            }
        }

        return new SelectedPathModel(byPair, bySymbol, pairCandidates, symbolCandidates);
    }

    private sealed record SymbolPairCandidate(string SourceIndexId, string CallerSymbol, string CalleeSymbol);

    private sealed record SymbolCandidate(string SourceIndexId, string Symbol);

    private static void AddPair(
        Dictionary<string, RouteFlowRow> values,
        List<SymbolPairCandidate> candidates,
        string sourceIndexId,
        string? sourceSymbol,
        string? targetSymbol,
        RouteFlowRow row)
    {
        if (string.IsNullOrWhiteSpace(sourceSymbol) || string.IsNullOrWhiteSpace(targetSymbol))
        {
            return;
        }

        if (values.TryAdd(SymbolPairKey(sourceIndexId, sourceSymbol!, targetSymbol!), row))
        {
            candidates.Add(new SymbolPairCandidate(sourceIndexId, sourceSymbol!, targetSymbol!));
        }
    }

    private static void AddSymbol(
        Dictionary<string, RouteFlowRow> values,
        List<SymbolCandidate> candidates,
        string sourceIndexId,
        string? symbol,
        RouteFlowRow row)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        if (values.TryAdd(SymbolKey(sourceIndexId, symbol!), row))
        {
            candidates.Add(new SymbolCandidate(sourceIndexId, symbol!));
        }
    }

    private static string SymbolPairKey(string sourceIndexId, string sourceSymbol, string targetSymbol)
    {
        return $"{sourceIndexId}\0{sourceSymbol}\0{targetSymbol}";
    }

    private static string SymbolKey(string sourceIndexId, string symbol)
    {
        return $"{sourceIndexId}\0{symbol}";
    }

    private static async Task<IReadOnlyList<ArgumentFlowProjectionRow>> ReadArgumentFlowProjectionRowsAsync(SqliteConnection connection, IReadOnlyList<SymbolPairCandidate> pairCandidates, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var pairFilter = AddArgumentPairFilterParameters(command, pairCandidates);
        command.CommandText = """
            select flows.combined_fact_id,
                   flows.source_index_id,
                   sources.label,
                   flows.commit_sha,
                   flows.evidence_tier,
                   flows.rule_id,
                   flows.caller_symbol,
                   flows.callee_symbol,
                   flows.parameter_ordinal,
                   flows.parameter_name,
                   flows.parameter_type,
                   flows.argument_ordinal,
                   flows.argument_expression_kind,
                   flows.argument_expression_hash,
                   flows.argument_symbol,
                   flows.argument_symbol_kind,
                   flows.file_path,
                   flows.start_line,
                   flows.end_line
            from combined_argument_flows flows
            join index_sources sources on sources.source_index_id = flows.source_index_id
            where
            """ + pairFilter + """
            order by flows.source_index_id, flows.caller_symbol, flows.callee_symbol, flows.parameter_ordinal, flows.combined_fact_id;
            """;
        var rows = new List<ArgumentFlowProjectionRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ArgumentFlowProjectionRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.GetInt32(8),
                reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.IsDBNull(15) ? null : reader.GetString(15),
                reader.GetString(16),
                reader.GetInt32(17),
                reader.GetInt32(18)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<FactSymbolProjectionRow>> ReadFactSymbolProjectionRowsAsync(SqliteConnection connection, IReadOnlyList<SymbolCandidate> symbolCandidates, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var symbolFilter = AddFactSymbolFilterParameters(command, symbolCandidates);
        command.CommandText = """
            select links.combined_fact_id,
                   links.combined_symbol_id,
                   links.source_index_id,
                   sources.label,
                   facts.commit_sha,
                   facts.fact_type,
                   facts.rule_id,
                   facts.evidence_tier,
                   facts.source_symbol,
                   facts.target_symbol,
                   symbols.display_name,
                   symbols.symbol_kind,
                   links.role,
                   facts.file_path,
                   facts.start_line,
                   facts.end_line,
                   facts.properties_json
            from combined_fact_symbols links
            join combined_facts facts on facts.combined_fact_id = links.combined_fact_id
            join index_sources sources on sources.source_index_id = links.source_index_id
            left join combined_symbols symbols on symbols.combined_symbol_id = links.combined_symbol_id
            where
            """ + symbolFilter + """
            order by links.source_index_id, links.combined_symbol_id, links.role, links.combined_fact_id;
            """;
        var rows = new List<FactSymbolProjectionRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new FactSymbolProjectionRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? reader.GetString(1) : reader.GetString(10),
                reader.IsDBNull(11) ? "Unknown" : reader.GetString(11),
                reader.GetString(12),
                reader.GetString(13),
                reader.GetInt32(14),
                reader.GetInt32(15),
                ParseMetadataProperties(reader.GetString(16))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> ReadArgumentFlowProjectionFactIdsAsync(SqliteConnection connection, IReadOnlyList<string> sourceIndexIds, CancellationToken cancellationToken)
    {
        if (sourceIndexIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        var sourceFilter = AddSourceFilterParameters(command, sourceIndexIds);
        command.CommandText = """
            select combined_fact_id
            from combined_argument_flows
            where source_index_id in (
            """ + sourceFilter + """
            )
            order by source_index_id, caller_symbol, callee_symbol, parameter_ordinal, combined_fact_id
            limit 20;
            """;
        return await ReadFactIdsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ReadFactSymbolProjectionFactIdsAsync(SqliteConnection connection, IReadOnlyList<string> sourceIndexIds, CancellationToken cancellationToken)
    {
        if (sourceIndexIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        var sourceFilter = AddSourceFilterParameters(command, sourceIndexIds);
        command.CommandText = """
            select distinct combined_fact_id
            from combined_fact_symbols
            where source_index_id in (
            """ + sourceFilter + """
            )
            order by combined_fact_id
            limit 20;
            """;
        return await ReadFactIdsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ReadFactIdsAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private sealed record TouchedFileCandidate(
        string RowId,
        string Classification,
        string Coverage,
        RouteFlowEvidenceRef Evidence);

    private sealed record TouchedSymbolCandidate(
        string RowId,
        string SymbolIdentity,
        string DisplayName,
        string SymbolKind,
        string Classification,
        string Coverage,
        RouteFlowEvidenceRef Evidence);

    private sealed record ContextGroupCandidate(
        string GroupKey,
        string RowId,
        string GroupKind,
        string DisplayName,
        string MatchKind,
        string Classification,
        string Coverage,
        IReadOnlyDictionary<string, string> SafeMetadata,
        RouteFlowEvidenceRef Evidence);

    private static IReadOnlyList<RouteFlowContextGroup> BuildContextGroups(
        IReadOnlyList<RouteFlowRow> flowRows,
        IReadOnlyList<RouteFlowLogicRow> logicRows,
        IReadOnlyList<RouteFlowDependencySurface> dependencySurfaces,
        IReadOnlyList<RouteFlowGap> gaps)
    {
        var commitBySourceAndPath = BuildCommitBySourceAndPath(
            flowRows.Select(row => row.Evidence)
                .Concat(logicRows.Select(row => row.Evidence))
                .Concat(dependencySurfaces.Select(row => row.Evidence)));
        var candidates = flowRows.Select(FlowContextCandidate)
            .Concat(logicRows.Select(LogicContextCandidate))
            .Concat(dependencySurfaces.Select(SurfaceContextCandidate))
            .Concat(gaps.Select(gap => GapContextCandidate(gap, CommitShaForGap(gap, commitBySourceAndPath))))
            .ToArray();

        return candidates
            .GroupBy(candidate => candidate.GroupKey, StringComparer.Ordinal)
            .Select(group =>
            {
                var rows = group
                    .OrderBy(candidate => candidate.GroupKind, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.RowId, StringComparer.Ordinal)
                    .ToArray();
                var first = rows[0];
                var supportingRowIds = rows.Select(row => row.RowId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var tiers = rows.Select(row => row.Evidence.EvidenceTier).Distinct(StringComparer.Ordinal).OrderBy(EvidenceTierRank).ThenBy(value => value, StringComparer.Ordinal).ToArray();
                var classification = rows.Select(row => row.Classification).Aggregate(RouteFlowClassifications.StrongStaticRouteFlow, WeakestClassification);
                var coverage = rows.Select(row => row.Coverage).Aggregate("FullEvidenceAvailable", WeakestCoverage);
                var evidenceTier = WeakestEvidenceTier(rows.Select(row => row.Evidence.EvidenceTier));
                var valueSafety = ValueSafetyFor(rows.Select(row => row.Evidence), rows.Select(row => row.SafeMetadata), first.DisplayName);
                var ruleIds = ContextGroupRuleIds(rows.Select(row => row.Evidence), valueSafety);
                var sourceLabel = rows.Select(row => row.Evidence.SourceLabel).OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? "unknown";
                var location = ContextGroupLocation(rows.Select(row => row.Evidence));
                var facts = rows.SelectMany(row => row.Evidence.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var edges = rows.SelectMany(row => row.Evidence.SupportingEdgeIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var limitations = rows.SelectMany(row => row.Evidence.Limitations)
                    .Append("Context groups summarize already-selected route-flow rows and do not prove runtime execution, dependency-injection target selection, branch feasibility, SQL execution, or production use.")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var metadata = Metadata(
                    ("groupKind", first.GroupKind),
                    ("matchKind", first.MatchKind),
                    ("valueSafety", valueSafety),
                    ("rowCount", rows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("sourceLabel", sourceLabel));
                return new RouteFlowContextGroup(
                    $"context:{first.GroupKind}:{CombinedReportHelpers.Hash($"{first.GroupKind}\0{first.MatchKind}\0{sourceLabel}\0{first.DisplayName}\0{string.Join(",", supportingRowIds)}", 24)}",
                    first.GroupKind,
                    first.DisplayName,
                    first.MatchKind,
                    valueSafety,
                    classification,
                    coverage,
                    evidenceTier,
                    MergeMetadata(metadata, rows.Select(row => row.SafeMetadata)),
                    supportingRowIds,
                    ruleIds,
                    tiers,
                    limitations,
                    new RouteFlowEvidenceRef(
                        ReportRuleId,
                        evidenceTier,
                        sourceLabel,
                        SafeCommitSha(location.CommitSha),
                        location.FilePath,
                        location.StartLine,
                        location.EndLine,
                        "route-flow",
                        Version,
                        facts,
                        edges,
                        ruleIds,
                        limitations));
            })
            .OrderBy(group => ContextGroupKindRank(group.GroupKind))
            .ThenBy(group => ClassificationRank(group.Classification))
            .ThenBy(group => group.DisplayName, StringComparer.Ordinal)
            .ThenBy(group => group.GroupId, StringComparer.Ordinal)
            .ToArray();
    }

    private static ContextGroupCandidate FlowContextCandidate(RouteFlowRow row)
    {
        var groupKind = FlowContextGroupKind(row);
        var displayName = row.TargetSymbol ?? row.SourceSymbol;
        var sourceLabel = row.Evidence.SourceLabel;
        return new ContextGroupCandidate(
            $"flow:{groupKind}:{sourceLabel}:{NormalizeSymbolIdentity(displayName)}",
            row.RowId,
            groupKind,
            displayName,
            FlowMatchKind(row),
            row.Classification,
            row.Coverage,
            Metadata(
                ("edgeKind", row.EdgeKind),
                ("rowKind", row.RowKind),
                ("sourceLabel", sourceLabel)),
            row.Evidence);
    }

    private static ContextGroupCandidate LogicContextCandidate(RouteFlowLogicRow row)
    {
        var groupKind = LogicContextGroupKind(row);
        var sourceLabel = row.Evidence.SourceLabel;
        return new ContextGroupCandidate(
            $"logic:{groupKind}:{sourceLabel}:{row.LogicKind}:{NormalizeSymbolIdentity(row.DisplayName)}",
            row.LogicRowId,
            groupKind,
            row.DisplayName,
            LogicMatchKind(row),
            row.Classification,
            row.Coverage,
            MergeMetadata(
                Metadata(
                    ("attachmentKind", row.AttachmentKind),
                    ("logicKind", row.LogicKind),
                    ("sourceLabel", sourceLabel)),
                [row.SafeMetadata]),
            row.Evidence);
    }

    private static ContextGroupCandidate SurfaceContextCandidate(RouteFlowDependencySurface surface)
    {
        var groupKind = SurfaceContextGroupKind(surface.SurfaceKind);
        var sourceLabel = surface.Evidence.SourceLabel;
        return new ContextGroupCandidate(
            $"surface:{groupKind}:{sourceLabel}:{surface.StableKey}",
            surface.SurfaceId,
            groupKind,
            surface.DisplayName,
            "dependency-surface",
            surface.Classification,
            surface.Coverage,
            MergeMetadata(
                Metadata(
                    ("sourceLabel", sourceLabel),
                    ("surfaceKind", surface.SurfaceKind)),
                [surface.SafeMetadata]),
            surface.Evidence);
    }

    private static ContextGroupCandidate GapContextCandidate(RouteFlowGap gap, string commitSha)
    {
        var displayName = gap.AffectedRowId ?? gap.GapKind;
        var evidence = EvidenceFromGap(gap, commitSha);
        return new ContextGroupCandidate(
            $"gap:{gap.GapKind}:{gap.SourceLabel ?? "unknown"}:{gap.AffectedRowId ?? gap.GapId}",
            gap.GapId,
            "gap",
            displayName,
            "gap",
            GapClassification(gap),
            gap.Coverage,
            Metadata(
                ("gapKind", gap.GapKind),
                ("sourceLabel", gap.SourceLabel)),
            evidence);
    }

    private static string FlowContextGroupKind(RouteFlowRow row)
    {
        if (row.RowKind == "interface-implementation-candidate")
        {
            return "interface-candidate";
        }

        if (row.EdgeKind is "argument-flow" or "parameter-forward")
        {
            return "value-origin";
        }

        var target = row.TargetSymbol ?? string.Empty;
        if (target.Contains("Repository", StringComparison.OrdinalIgnoreCase)
            || target.Contains("DbContext", StringComparison.OrdinalIgnoreCase)
            || target.Contains("Dao", StringComparison.OrdinalIgnoreCase))
        {
            return "repository";
        }

        if (target.Contains("Service", StringComparison.OrdinalIgnoreCase))
        {
            return "service";
        }

        return "method";
    }

    private static string FlowMatchKind(RouteFlowRow row)
    {
        return row.RowKind == "interface-implementation-candidate"
            ? "interface-candidate"
            : row.EdgeKind switch
            {
                "argument-flow" or "parameter-forward" => "argument-flow",
                "object-creation" => "object-creation",
                "route-bound-to-symbol" => "route-bound-to-symbol",
                "direct-call" => "direct-call",
                "terminal-surface" => "dependency-surface",
                _ => "path-context"
            };
    }

    private static string LogicContextGroupKind(RouteFlowLogicRow row)
    {
        return row.LogicKind switch
        {
            "argument-flow" or "flow-boundary" => "value-origin",
            "query-filter-sort-selection" or "query-shape" => "query",
            "projection-or-object-shape" => "data-surface",
            _ => "method"
        };
    }

    private static string LogicMatchKind(RouteFlowLogicRow row)
    {
        return row.AttachmentKind switch
        {
            "argument-projection" => "argument-flow",
            "fact-symbol-projection" => "fact-symbol",
            _ => "path-context"
        };
    }

    private static string SurfaceContextGroupKind(string surfaceKind)
    {
        return surfaceKind switch
        {
            "sql-query" => "query",
            "sql-persistence" => "data-surface",
            "legacy-data" => "legacy-data",
            "package-config" => "dependency",
            "http-route" or "http-client" or "wcf-operation" or "remoting-endpoint" or "remoting-registration" or "remoting-channel" or "remoting-object" or "remoting-api" => "dependency",
            _ => "dependency"
        };
    }

    private static int ContextGroupKindRank(string groupKind)
    {
        return groupKind switch
        {
            "method" => 0,
            "service" => 1,
            "interface-candidate" => 2,
            "repository" => 3,
            "value-origin" => 4,
            "query" => 5,
            "data-surface" => 6,
            "legacy-data" => 7,
            "dependency" => 8,
            "gap" => 9,
            _ => 10
        };
    }

    private static string ValueSafetyFor(
        IEnumerable<RouteFlowEvidenceRef> evidence,
        IEnumerable<IReadOnlyDictionary<string, string>?> metadata,
        string displayName)
    {
        if (string.Equals(displayName, "redacted", StringComparison.Ordinal)
            || string.Equals(displayName, "n/a", StringComparison.Ordinal))
        {
            return "omitted";
        }

        if (displayName.StartsWith("redacted-hash:", StringComparison.Ordinal)
            || evidence.Any(item => item.SupportingRuleIds.Contains(RedactionRuleId, StringComparer.Ordinal))
            || metadata.Where(item => item is not null).SelectMany(item => item!).Any(pair => pair.Key.Contains("Hash", StringComparison.Ordinal) || pair.Value.Contains("-hash:", StringComparison.Ordinal)))
        {
            return "hashed";
        }

        return "safe";
    }

    private static IReadOnlyList<string> ContextGroupRuleIds(IEnumerable<RouteFlowEvidenceRef> evidence, string valueSafety)
    {
        var ruleIds = SummaryRuleIds(evidence);
        return valueSafety is "hashed" or "omitted"
            ? ruleIds.Append(RedactionRuleId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            : ruleIds;
    }

    private sealed record ContextGroupEvidenceLocation(
        string? CommitSha,
        string? FilePath,
        int? StartLine,
        int? EndLine);

    private static ContextGroupEvidenceLocation ContextGroupLocation(IEnumerable<RouteFlowEvidenceRef> evidence)
    {
        var locations = evidence
            .Where(item => !string.IsNullOrWhiteSpace(item.FilePath))
            .GroupBy(item => $"{item.SourceLabel}\0{item.CommitSha}\0{item.FilePath}", StringComparer.Ordinal)
            .ToArray();
        if (locations.Length != 1)
        {
            return new ContextGroupEvidenceLocation("unknown", null, null, null);
        }

        var rows = locations[0].ToArray();
        var first = rows[0];
        var startLine = rows.Select(row => row.StartLine).OfType<int>().DefaultIfEmpty().Min();
        var endLine = rows.Select(row => row.EndLine).OfType<int>().DefaultIfEmpty().Max();
        return new ContextGroupEvidenceLocation(
            first.CommitSha,
            first.FilePath,
            startLine == 0 ? null : startLine,
            endLine == 0 ? null : endLine);
    }

    private static IReadOnlyDictionary<string, string> MergeMetadata(
        IReadOnlyDictionary<string, string>? baseMetadata,
        IEnumerable<IReadOnlyDictionary<string, string>?> metadataRows)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (baseMetadata is not null)
        {
            foreach (var pair in baseMetadata)
            {
                values[pair.Key] = pair.Value;
            }
        }

        foreach (var row in metadataRows)
        {
            if (row is null)
            {
                continue;
            }

            foreach (var pair in row)
            {
                if (values.ContainsKey(pair.Key))
                {
                    continue;
                }

                values[pair.Key] = pair.Value;
            }
        }

        return values;
    }

    private static IReadOnlyList<RouteFlowTouchedFile> BuildTouchedFiles(
        IReadOnlyList<RouteFlowEntryEvidence> entryEvidence,
        IReadOnlyList<RouteFlowRow> flowRows,
        IReadOnlyList<RouteFlowLogicRow> logicRows,
        IReadOnlyList<RouteFlowDependencySurface> dependencySurfaces,
        IReadOnlyList<RouteFlowGap> gaps)
    {
        var rowCandidates = entryEvidence.Select(row => new TouchedFileCandidate(row.EntryId, row.Classification, row.Coverage, row.Evidence))
            .Concat(flowRows.Select(row => new TouchedFileCandidate(row.RowId, row.Classification, row.Coverage, row.Evidence)))
            .Concat(logicRows.Select(row => new TouchedFileCandidate(row.LogicRowId, row.Classification, row.Coverage, row.Evidence)))
            .Concat(dependencySurfaces.Select(row => new TouchedFileCandidate(row.SurfaceId, row.Classification, row.Coverage, row.Evidence)))
            .Where(row => !string.IsNullOrWhiteSpace(row.Evidence.FilePath))
            .ToArray();
        var commitBySourceAndPath = BuildCommitBySourceAndPath(rowCandidates.Select(row => row.Evidence));
        var gapCandidates = gaps.Select(row => new TouchedFileCandidate(row.GapId, GapClassification(row), row.Coverage, EvidenceFromGap(
                row,
                CommitShaForGap(row, commitBySourceAndPath))))
            .Where(row => !string.IsNullOrWhiteSpace(row.Evidence.FilePath))
            .ToArray();
        var candidates = rowCandidates.Concat(gapCandidates).ToArray();

        return candidates
            .GroupBy(row => $"{row.Evidence.SourceLabel}\0{row.Evidence.CommitSha}\0{row.Evidence.FilePath}", StringComparer.Ordinal)
            .Select(group =>
            {
                var rows = group.ToArray();
                var first = rows[0].Evidence;
                var supportingRowIds = rows.Select(row => row.RowId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var ruleIds = SummaryRuleIds(rows.Select(row => row.Evidence));
                var tiers = rows.Select(row => row.Evidence.EvidenceTier).Distinct(StringComparer.Ordinal).OrderBy(EvidenceTierRank).ThenBy(value => value, StringComparer.Ordinal).ToArray();
                var startLine = rows.Select(row => row.Evidence.StartLine).Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty().Min();
                var endLine = rows.Select(row => row.Evidence.EndLine).Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty().Max();
                int? firstStartLine = startLine == 0 ? null : startLine;
                int? lastEndLine = endLine == 0 ? null : endLine;
                var classification = rows.Select(row => row.Classification).Aggregate(RouteFlowClassifications.StrongStaticRouteFlow, WeakestClassification);
                var coverage = rows.Select(row => row.Coverage).Aggregate("FullEvidenceAvailable", WeakestCoverage);
                var facts = rows.SelectMany(row => row.Evidence.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var edges = rows.SelectMany(row => row.Evidence.SupportingEdgeIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var limitations = rows.SelectMany(row => row.Evidence.Limitations)
                    .Append("Touched-file summaries are grouped static evidence locations and do not prove runtime request execution or business impact.")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var filePath = first.FilePath!;
                return new RouteFlowTouchedFile(
                    $"touched-file:{CombinedReportHelpers.Hash($"{first.SourceLabel}\0{first.CommitSha}\0{filePath}", 24)}",
                    first.SourceLabel,
                    first.CommitSha,
                    filePath,
                    firstStartLine,
                    lastEndLine,
                    classification,
                    coverage,
                    supportingRowIds,
                    ruleIds,
                    tiers,
                    new RouteFlowEvidenceRef(
                        ReportRuleId,
                        WeakestEvidenceTier(rows.Select(row => row.Evidence.EvidenceTier)),
                        first.SourceLabel,
                        first.CommitSha,
                        filePath,
                        firstStartLine,
                        lastEndLine,
                        "route-flow",
                        Version,
                        facts,
                        edges,
                        ruleIds,
                        limitations));
            })
            .OrderBy(row => row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => ClassificationRank(row.Classification))
            .ThenBy(row => row.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.FirstStartLine ?? 0)
            .ThenBy(row => row.LastEndLine ?? 0)
            .ThenBy(row => row.FileId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<RouteFlowTouchedSymbol> BuildTouchedSymbols(
        IReadOnlyList<RouteFlowEntryEvidence> entryEvidence,
        IReadOnlyList<RouteFlowRow> flowRows,
        IReadOnlyList<RouteFlowLogicRow> logicRows,
        IReadOnlyList<RouteFlowDependencySurface> dependencySurfaces,
        IReadOnlyList<CombinedPath> routePaths,
        IReadOnlyList<RouteFlowSource> sources)
    {
        var candidates = entryEvidence
            .Where(row => !string.IsNullOrWhiteSpace(row.DisplaySymbol))
            .Select(row => new TouchedSymbolCandidate(row.EntryId, row.EntryId, row.DisplaySymbol!, row.EntryKind, row.Classification, row.Coverage, row.Evidence))
            .Concat(PathNodeSymbolCandidates(routePaths, flowRows, sources))
            .Concat(logicRows.Select(row => new TouchedSymbolCandidate(row.LogicRowId, row.LogicRowId, row.DisplayName, row.LogicKind, row.Classification, row.Coverage, row.Evidence)))
            .Concat(dependencySurfaces.Select(row => new TouchedSymbolCandidate(row.SurfaceId, row.SurfaceId, row.DisplayName, row.SurfaceKind, row.Classification, row.Coverage, row.Evidence)))
            .Where(row => !string.IsNullOrWhiteSpace(row.DisplayName))
            .ToArray();

        return candidates
            .GroupBy(row => $"{row.Evidence.SourceLabel}\0{NormalizeSymbolIdentity(row.SymbolIdentity)}\0{row.DisplayName}\0{row.Evidence.FilePath}", StringComparer.Ordinal)
            .Select(group =>
            {
                var rows = group.ToArray();
                var first = rows[0].Evidence;
                var symbolIdentity = rows.Select(row => NormalizeSymbolIdentity(row.SymbolIdentity)).OrderBy(value => value, StringComparer.Ordinal).First();
                var displayName = rows.Select(row => row.DisplayName).OrderBy(value => value, StringComparer.Ordinal).First();
                var supportingRowIds = rows.Select(row => row.RowId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var ruleIds = SummaryRuleIds(rows.Select(row => row.Evidence));
                var tiers = rows.Select(row => row.Evidence.EvidenceTier).Distinct(StringComparer.Ordinal).OrderBy(EvidenceTierRank).ThenBy(value => value, StringComparer.Ordinal).ToArray();
                var startLine = rows.Select(row => row.Evidence.StartLine).Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty().Min();
                var endLine = rows.Select(row => row.Evidence.EndLine).Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty().Max();
                int? firstStartLine = startLine == 0 ? null : startLine;
                int? lastEndLine = endLine == 0 ? null : endLine;
                var classification = rows.Select(row => row.Classification).Aggregate(RouteFlowClassifications.StrongStaticRouteFlow, WeakestClassification);
                var coverage = rows.Select(row => row.Coverage).Aggregate("FullEvidenceAvailable", WeakestCoverage);
                var facts = rows.SelectMany(row => row.Evidence.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var edges = rows.SelectMany(row => row.Evidence.SupportingEdgeIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var limitations = rows.SelectMany(row => row.Evidence.Limitations)
                    .Append("Touched-symbol summaries are grouped static evidence identities and do not prove runtime dispatch, runtime dependency-injection target selection, or production use.")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var symbolKind = rows.Select(row => row.SymbolKind).OrderBy(value => value, StringComparer.Ordinal).First();
                return new RouteFlowTouchedSymbol(
                    symbolIdentity,
                    first.SourceLabel,
                    first.CommitSha,
                    first.FilePath,
                    firstStartLine,
                    lastEndLine,
                    displayName,
                    symbolKind,
                    classification,
                    coverage,
                    supportingRowIds,
                    ruleIds,
                    tiers,
                    new RouteFlowEvidenceRef(
                        ReportRuleId,
                        WeakestEvidenceTier(rows.Select(row => row.Evidence.EvidenceTier)),
                        first.SourceLabel,
                        first.CommitSha,
                        first.FilePath,
                        firstStartLine,
                        lastEndLine,
                        "route-flow",
                        Version,
                        facts,
                        edges,
                        ruleIds,
                        limitations));
            })
            .OrderBy(row => row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => ClassificationRank(row.Classification))
            .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
            .ThenBy(row => row.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.StartLine ?? 0)
            .ThenBy(row => row.EndLine ?? 0)
            .ThenBy(row => row.SymbolId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<TouchedSymbolCandidate> PathNodeSymbolCandidates(
        IReadOnlyList<CombinedPath> routePaths,
        IReadOnlyList<RouteFlowRow> flowRows,
        IReadOnlyList<RouteFlowSource> sources)
    {
        var rowByNodeId = flowRows
            .Where(row => !string.IsNullOrWhiteSpace(row.ToNodeId))
            .GroupBy(row => row.ToNodeId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.Sequence).First(), StringComparer.Ordinal);
        foreach (var node in routePaths.SelectMany(path => path.Nodes).OrderBy(node => node.SourceLabel, StringComparer.Ordinal).ThenBy(node => node.NodeId, StringComparer.Ordinal))
        {
            var displayName = FlowDisplaySymbol(node);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            var attachedRow = rowByNodeId.GetValueOrDefault(node.NodeId);
            var classification = attachedRow?.Classification ?? ClassifyRouteRow(null, node, sources);
            yield return new TouchedSymbolCandidate(
                attachedRow?.RowId ?? $"node:{CombinedReportHelpers.Hash(node.NodeId, 16)}",
                node.SymbolId ?? node.NodeId,
                displayName,
                node.NodeKind,
                classification,
                CoverageFor(node.EvidenceTier),
                EvidenceFromNode(PathRuleId, node, node.CombinedFactId is null ? [] : [node.CombinedFactId], [], [node.RuleId ?? PathRuleId], sources));
        }
    }

    private static string NormalizeSymbolIdentity(string? symbolIdentity)
    {
        var safe = SafeSelector(symbolIdentity);
        return safe is null || safe.StartsWith("redacted-hash:", StringComparison.Ordinal)
            ? $"unavailable-symbol:{CombinedReportHelpers.Hash(symbolIdentity ?? "unknown", 24)}"
            : safe;
    }

    private static RouteFlowEvidenceRef EvidenceFromGap(RouteFlowGap gap, string commitSha)
    {
        return new RouteFlowEvidenceRef(
            gap.RuleId,
            gap.EvidenceTier,
            gap.SourceLabel ?? "unknown",
            SafeCommitSha(gap.CommitSha ?? commitSha),
            gap.FilePath,
            gap.StartLine,
            gap.EndLine,
            gap.ExtractorName ?? "route-flow",
            gap.ExtractorVersion ?? Version,
            gap.SupportingFactIds,
            [],
            [gap.RuleId],
            gap.Limitations);
    }

    private static IReadOnlyDictionary<string, string> BuildCommitBySourceAndPath(IEnumerable<RouteFlowEvidenceRef> evidence)
    {
        return evidence
            .Where(row => !string.IsNullOrWhiteSpace(row.FilePath) && KnownCommit(row.CommitSha))
            .GroupBy(row => $"{row.SourceLabel}\0{row.FilePath}", StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.CommitSha!).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).First(),
                StringComparer.Ordinal);
    }

    private static string CommitShaForGap(RouteFlowGap gap, IReadOnlyDictionary<string, string> commitBySourceAndPath)
    {
        return gap.SourceLabel is not null
            && gap.FilePath is not null
            && commitBySourceAndPath.TryGetValue($"{gap.SourceLabel}\0{gap.FilePath}", out var commitSha)
            ? commitSha
            : "unknown";
    }

    private static IReadOnlyList<string> SummaryRuleIds(IEnumerable<RouteFlowEvidenceRef> evidence)
    {
        return evidence
            .SelectMany(item => item.SupportingRuleIds.Append(item.RuleId))
            .Append(ReportRuleId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string WeakestEvidenceTier(IEnumerable<string> tiers)
    {
        return tiers
            .OrderByDescending(EvidenceTierRank)
            .ThenBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? EvidenceTiers.Tier4Unknown;
    }

    private static int EvidenceTierRank(string? tier)
    {
        return tier switch
        {
            EvidenceTiers.Tier1Semantic => 0,
            EvidenceTiers.Tier2Structural => 1,
            EvidenceTiers.Tier3SyntaxOrTextual => 2,
            EvidenceTiers.Tier4Unknown => 3,
            _ => 3
        };
    }

    private static string WeakestCoverage(string left, string right)
    {
        return CoverageRank(left) >= CoverageRank(right) ? left : right;
    }

    private static int CoverageRank(string? coverage)
    {
        return coverage switch
        {
            "FullEvidenceAvailable" => 0,
            "CoverageRelative" => 1,
            "ReducedCoverage" => 2,
            _ => 2
        };
    }

    private static string AddArgumentPairFilterParameters(SqliteCommand command, IReadOnlyList<SymbolPairCandidate> pairCandidates)
    {
        var clauses = new List<string>();
        for (var index = 0; index < pairCandidates.Count; index++)
        {
            var sourceParameter = $"$pairSource{index}";
            var callerParameter = $"$pairCaller{index}";
            var calleeParameter = $"$pairCallee{index}";
            command.Parameters.AddWithValue(sourceParameter, pairCandidates[index].SourceIndexId);
            command.Parameters.AddWithValue(callerParameter, pairCandidates[index].CallerSymbol);
            command.Parameters.AddWithValue(calleeParameter, pairCandidates[index].CalleeSymbol);
            clauses.Add($"(flows.source_index_id = {sourceParameter} and flows.caller_symbol = {callerParameter} and flows.callee_symbol = {calleeParameter})");
        }

        return clauses.Count == 0
            ? "0 = 1"
            : string.Join($"{Environment.NewLine}               or ", clauses);
    }

    private static string AddFactSymbolFilterParameters(SqliteCommand command, IReadOnlyList<SymbolCandidate> symbolCandidates)
    {
        var clauses = new List<string>();
        for (var index = 0; index < symbolCandidates.Count; index++)
        {
            var sourceParameter = $"$symbolSource{index}";
            var symbolParameter = $"$symbol{index}";
            command.Parameters.AddWithValue(sourceParameter, symbolCandidates[index].SourceIndexId);
            command.Parameters.AddWithValue(symbolParameter, symbolCandidates[index].Symbol);
            clauses.Add($"(links.source_index_id = {sourceParameter} and (links.combined_symbol_id = {symbolParameter} or symbols.display_name = {symbolParameter}))");
        }

        return clauses.Count == 0
            ? "0 = 1"
            : string.Join($"{Environment.NewLine}               or ", clauses);
    }

    private static string AddSourceFilterParameters(SqliteCommand command, IReadOnlyList<string> sourceIndexIds)
    {
        var parameterNames = new List<string>();
        for (var index = 0; index < sourceIndexIds.Count; index++)
        {
            var parameterName = $"$source{index}";
            parameterNames.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, sourceIndexIds[index]);
        }

        return string.Join(", ", parameterNames);
    }

    private static bool ShouldProjectFactSymbol(FactSymbolProjectionRow row)
    {
        return row.FactType is FactTypes.ObjectShapeInferred
            or FactTypes.QueryPatternDetected
            or FactTypes.SqlTextUsed
            or FactTypes.SqlCommandDetected
            or FactTypes.DapperCallDetected
            or FactTypes.DatabaseColumnMapping
            or FactTypes.PackageReferenced;
    }

    private static string FactSymbolLogicKind(FactSymbolProjectionRow row)
    {
        return row.FactType switch
        {
            FactTypes.ObjectShapeInferred => "object-shape",
            FactTypes.QueryPatternDetected or FactTypes.SqlTextUsed or FactTypes.SqlCommandDetected or FactTypes.DapperCallDetected => "query-shape",
            FactTypes.DatabaseColumnMapping => "data-surface",
            FactTypes.PackageReferenced => "dependency-surface",
            _ => "fact-symbol-attachment"
        };
    }

    private static string FactSymbolDisplayName(FactSymbolProjectionRow row, string kind)
    {
        var shapeHash = FirstProperty(row.Properties, "queryShapeHash", "shapeHash", "textHash", "tableHash", "objectShapeHash");
        if (!string.IsNullOrWhiteSpace(shapeHash))
        {
            return $"{kind}:{SafeSelector(shapeHash) ?? CombinedReportHelpers.Hash(shapeHash!, 16)}";
        }

        return $"{kind}:fact-hash:{CombinedReportHelpers.Hash(row.CombinedFactId, 16)}";
    }

    private static IReadOnlyDictionary<string, string> FactSymbolMetadata(FactSymbolProjectionRow row, string evidenceKind)
    {
        return Metadata(
            ("evidenceKind", evidenceKind),
            ("symbolKind", row.SymbolKind),
            ("role", row.Role),
            ("operationName", FirstProperty(row.Properties, "operationName")),
            ("sourceKind", FirstProperty(row.Properties, "sqlSourceKind", "sourceKind")),
            ("shapeHash", FirstProperty(row.Properties, "queryShapeHash", "shapeHash", "objectShapeHash")),
            ("textHash", FirstProperty(row.Properties, "textHash")),
            ("tableNameHash", HashProperty(row.Properties, "tableName")),
            ("targetSymbolHash", string.IsNullOrWhiteSpace(row.TargetSymbol) ? null : CombinedReportHelpers.Hash(row.TargetSymbol!, 16)),
            ("sourceSymbolHash", string.IsNullOrWhiteSpace(row.SourceSymbol) ? null : CombinedReportHelpers.Hash(row.SourceSymbol!, 16)));
    }

    private static string SafeParameterName(string value, out bool redacted)
    {
        var safe = SafeSelector(value);
        redacted = safe is null
            || safe.StartsWith("redacted-hash:", StringComparison.Ordinal)
            || LooksSensitiveIdentifier(safe);
        return redacted ? $"parameter-name-hash:{CombinedReportHelpers.Hash(value, 16)}" : safe!;
    }

    private static bool LooksSensitiveIdentifier(string value)
    {
        return value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("connection", StringComparison.OrdinalIgnoreCase)
            || value.Contains("connstr", StringComparison.OrdinalIgnoreCase);
    }

    private static bool FactSymbolRedactionApplied(FactSymbolProjectionRow row)
    {
        return !string.IsNullOrWhiteSpace(row.SourceSymbol)
            || !string.IsNullOrWhiteSpace(row.TargetSymbol)
            || !string.IsNullOrWhiteSpace(FirstProperty(row.Properties, "tableName"));
    }

    private static string? FirstProperty(IReadOnlyDictionary<string, string> properties, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? HashProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? CombinedReportHelpers.Hash(value, 16)
            : null;
    }

    private static RouteFlowGap ProjectionUnavailableGap(
        string scope,
        string gapKind,
        string message,
        string ruleId,
        IReadOnlyList<string> supportingFactIds)
    {
        var support = supportingFactIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(20)
            .ToArray();
        return new RouteFlowGap(
            $"gap:projection:{scope}:{CombinedReportHelpers.Hash(string.Join("|", support), 16)}",
            gapKind,
            message,
            GapRuleId,
            EvidenceTiers.Tier4Unknown,
            "ReducedCoverage",
            null,
            null,
            support,
            [$"{ruleId} requires source-local static joins; unjoined projection evidence is reported as a gap, not inferred flow."]);
    }

    private static RouteFlowEvidenceRef EvidenceFromProjection(
        string routeRuleId,
        string evidenceTier,
        string sourceLabel,
        string commitSha,
        string filePath,
        int startLine,
        int endLine,
        string supportingRuleId,
        IReadOnlyList<string> facts,
        IReadOnlyList<string> edges,
        IReadOnlyList<RouteFlowSource> sources,
        bool redactionApplied = false)
    {
        var supportingRuleIds = new[] { routeRuleId, supportingRuleId }
            .Concat(redactionApplied ? [RedactionRuleId] : [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new RouteFlowEvidenceRef(
            routeRuleId,
            evidenceTier,
            SafeLabel(sourceLabel),
            SafeCommitSha(commitSha),
            CombinedReportHelpers.SafePath(filePath),
            startLine,
            endLine,
            ExtractorName(supportingRuleId),
            ExtractorVersionFor(sourceLabel, sources),
            facts.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            edges.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            supportingRuleIds,
            LimitationsFor(routeRuleId));
    }

    private static IReadOnlyDictionary<string, string> ParseMetadataProperties(string json)
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
                ["propertiesHash"] = CombinedReportHelpers.Hash(json, 32)
            };
        }
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
            || gaps.Any(gap => gap.GapKind is "SelectorNoMatch" or "SchemaMissing" or "ExtractorUnavailable" or "UnknownCommitSha" or "ReducedCoverage" or "UnknownAnalysisGap" or "MissingRouteRoot" or "MissingMethodSymbolBridge" or "MissingCallEdge" or "IdentityGap" or "TraversalBounds")
            || flowRows.Concat<object>(logicRows).Concat(surfaces).Any(RowIsUnknown)
            || entries.Count == 0)
        {
            classification = RouteFlowClassifications.UnknownAnalysisGap;
            reasons.Add("Selector, schema, source identity, or coverage gaps prevent a clean route-flow conclusion.");
        }
        else if (truncated
            || flowRows.Concat<object>(logicRows).Concat(surfaces).Any(RowNeedsReview)
            || gaps.Any(gap => gap.GapKind is "RuntimeBindingNotProven" or "ImplementationCandidateUnavailable" or "MissingImplementationBridge" or "AmbiguousImplementationCandidates" or "DataSurfaceAttachmentMissing" or "ArgumentProjectionUnavailable" or "FactSymbolProjectionUnavailable" or "FactSymbolUnsupportedTypeSkipped" or "DynamicClientUrlNeedsReview" or "TruncatedByLimit"))
        {
            classification = RouteFlowClassifications.NeedsReviewStaticRouteFlow;
            reasons.Add("Review-tier, weak, implementation-candidate, dynamic, or truncated static evidence is present.");
        }
        else if (!HasDownstreamFlowEvidence(flowRows)
            && surfaces.Count == 0)
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

    private static bool HasDownstreamFlowEvidence(IReadOnlyList<RouteFlowRow> flowRows)
    {
        return flowRows.Any(row => row.RowKind is not ("entry" or "endpoint-method-bridge"));
    }

    private static void RemoveCleanNoEvidenceGapsWhenBlocked(List<RouteFlowGap> gaps)
    {
        if (!gaps.Any(BlocksCleanNoEvidenceGap))
        {
            return;
        }

        gaps.RemoveAll(gap => gap.GapKind == "NoRouteFlowEvidence");
    }

    private static void RemoveDuplicateDispatchCandidateFanOutGaps(List<RouteFlowGap> gaps)
    {
        var inheritedPathFanOutNodeIds = gaps
            .Where(gap => gap.GapKind == "DispatchCandidateFanOut")
            .Where(gap => gap.GapId.StartsWith("gap:path:", StringComparison.Ordinal))
            .Select(gap => gap.AffectedRowId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
        if (inheritedPathFanOutNodeIds.Count == 0)
        {
            return;
        }

        gaps.RemoveAll(gap => gap.GapKind == "DispatchCandidateFanOut"
            && gap.GapId.StartsWith("gap:endpoint-composition:", StringComparison.Ordinal)
            && gap.AffectedRowId is not null
            && inheritedPathFanOutNodeIds.Contains(gap.AffectedRowId));
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
        gaps.RemoveAll(gap => !IsBlockingGap(gap)
            && !string.Equals(GapClassification(gap), requested, StringComparison.Ordinal)
            && !string.Equals(gap.GapKind, requested, StringComparison.Ordinal));
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

    private static RouteFlowGap FromPathGap(CombinedPathGap gap, string pathReportCoverage)
    {
        var gapKind = NormalizeGapKind(gap.GapKind);
        var preserveGapSpan = gapKind == "DispatchCandidateFanOut";
        return new RouteFlowGap(
            $"gap:path:{CombinedReportHelpers.Hash(gap.GapId, 24)}",
            gapKind,
            SafeSelector(gap.Message) ?? "Route-flow analysis gap.",
            GapRuleId,
            gap.EvidenceTier ?? EvidenceTiers.Tier4Unknown,
            CoverageForPathGap(gap, gapKind, pathReportCoverage),
            SafeSelector(gap.SourceLabel),
            gap.NodeId,
            gap.CombinedFactId is null ? [] : [gap.CombinedFactId],
            PathGapLimitations(gapKind, pathReportCoverage),
            preserveGapSpan ? CombinedReportHelpers.SafePath(gap.FilePath) : null,
            preserveGapSpan ? gap.StartLine : null,
            preserveGapSpan ? gap.EndLine : null,
            preserveGapSpan ? SafeCommitSha(gap.CommitSha) : null,
            preserveGapSpan ? ExtractorName(gap.RuleId) : null,
            preserveGapSpan ? SafeSelector(gap.ExtractorVersion) ?? "unknown" : null);
    }

    private static IReadOnlyList<string> PathGapLimitations(string gapKind, string pathReportCoverage)
    {
        if (gapKind == "DispatchCandidateFanOut")
        {
            return [
                "Static dispatch candidate fan-out is deterministically capped and remains review-tier.",
                "Candidate implementation rows identify compiler-known candidates only and do not prove runtime dependency-injection targets."
            ];
        }

        if (gapKind == "NoRouteFlowEvidence" && pathReportCoverage == "FullEvidenceAvailable")
        {
            return ["Inherited full-coverage no-evidence path gaps are static absence-of-evidence rows and do not prove runtime absence."];
        }

        return ["Path gaps are inherited as route-flow analysis gaps and remain coverage-relative."];
    }

    private static string CoverageForPathGap(CombinedPathGap gap, string gapKind, string pathReportCoverage)
    {
        if (gapKind == "NoRouteFlowEvidence")
        {
            return pathReportCoverage == "FullEvidenceAvailable" ? "FullEvidenceAvailable" : "ReducedCoverage";
        }

        if (gapKind == "DispatchCandidateFanOut")
        {
            return "ReducedCoverage";
        }

        return GapClassification(gap) == RouteFlowClassifications.UnknownAnalysisGap ? "ReducedCoverage" : "CoverageRelative";
    }

    private static IEnumerable<RouteFlowGap> SourceIdentityGaps(IReadOnlyList<RouteFlowSource> sources)
    {
        return sources
            .Where(source => !source.IdentityVerified)
            .Select(source => new RouteFlowGap(
                $"gap:identity:{CombinedReportHelpers.Hash($"{source.SourceLabel}:{source.SourceIndexId}", 16)}",
                KnownCommit(source.CommitSha) ? "ReducedCoverage" : "UnknownCommitSha",
                $"Source `{source.SourceLabel}` has missing or unverified identity metadata.",
                GapRuleId,
                EvidenceTiers.Tier4Unknown,
                "ReducedCoverage",
                source.SourceLabel,
                null,
                [],
                ["Unknown or unverified source identity caps route-flow conclusions."]));
    }

    private static RouteFlowEvidenceRef EvidenceFromNode(string routeRuleId, CombinedPathNode node, IReadOnlyList<string> facts, IReadOnlyList<string> edges, IReadOnlyList<string?> supportingRules, IReadOnlyList<RouteFlowSource> sources)
    {
        return new RouteFlowEvidenceRef(
            routeRuleId,
            node.EvidenceTier ?? EvidenceTiers.Tier4Unknown,
            SafeLabel(node.SourceLabel),
            SafeCommitSha(node.CommitSha),
            CombinedReportHelpers.SafePath(node.FilePath),
            node.StartLine,
            node.EndLine,
            ExtractorName(node.RuleId),
            ExtractorVersionFor(node.SourceLabel, sources),
            facts.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            edges.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            supportingRules.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).Append(routeRuleId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            LimitationsFor(routeRuleId));
    }

    private static RouteFlowEvidenceRef EvidenceFromEdge(string routeRuleId, CombinedPathEdge edge, IReadOnlyList<string?> supportingRules, IReadOnlyList<RouteFlowSource> sources, string? sourceLabel = null, string? commitSha = null)
    {
        return new RouteFlowEvidenceRef(
            routeRuleId,
            edge.EvidenceTier,
            SafeLabel(sourceLabel ?? "unknown"),
            SafeCommitSha(commitSha),
            CombinedReportHelpers.SafePath(edge.FilePath),
            edge.StartLine,
            edge.EndLine,
            ExtractorName(edge.RuleId),
            ExtractorVersionFor(sourceLabel, sources),
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

    private static RouteFlowSelectorTrace? BuildSelectorTrace(
        CombinedRouteFlowOptions options,
        string? routeSelector,
        string? clientSelector,
        string? endpointSelector,
        IReadOnlyList<RouteFlowEntryEvidence> entryEvidence,
        IReadOnlyList<RouteFlowGap> gaps,
        string reportCoverage)
    {
        var (selectorKind, rawSelector, safeNormalizedKey) = SelectorTraceInputs(options, routeSelector, clientSelector, endpointSelector);
        var safeSelector = SafeSelector(safeNormalizedKey ?? rawSelector);
        var coverage = reportCoverage == "FullEvidenceAvailable" && gaps.All(gap => gap.GapKind != "SelectorNoMatch")
            ? "FullEvidenceAvailable"
            : "ReducedCoverage";
        var supportingFactIds = entryEvidence
            .SelectMany(entry => entry.Evidence.SupportingFactIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (supportingFactIds.Length == 0)
        {
            return null;
        }

        var limitations = new List<string>
        {
            "Selector metadata is static query context and does not prove runtime execution.",
            "Selector values are normalized or redacted before rendering."
        };
        if (entryEvidence.Count == 0)
        {
            limitations.Add("No selector entry evidence matched under available coverage.");
        }

        if (coverage != "FullEvidenceAvailable")
        {
            limitations.Add("Reduced coverage caps selector-trace conclusions.");
        }

        return new RouteFlowSelectorTrace(
            selectorKind,
            RouteMatchMode(routeSelector, clientSelector, endpointSelector, options.FromSymbol, options.FromSource),
            safeNormalizedKey,
            safeSelector,
            SelectorRedactionState(rawSelector, safeNormalizedKey, safeSelector),
            coverage,
            SelectorRuleId,
            entryEvidence.Count == 0 ? EvidenceTiers.Tier4Unknown : WeakestEvidenceTier(entryEvidence.Select(entry => entry.Evidence.EvidenceTier)),
            supportingFactIds,
            limitations.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static (string SelectorKind, string? RawSelector, string? SafeNormalizedKey) SelectorTraceInputs(
        CombinedRouteFlowOptions options,
        string? routeSelector,
        string? clientSelector,
        string? endpointSelector)
    {
        if (!string.IsNullOrWhiteSpace(options.Route))
        {
            return ("route", options.Route, SafeSelector(routeSelector));
        }

        if (!string.IsNullOrWhiteSpace(options.ClientCall))
        {
            return ("client-call", options.ClientCall, SafeSelector(clientSelector));
        }

        if (!string.IsNullOrWhiteSpace(options.FromEndpoint))
        {
            return ("from-endpoint", options.FromEndpoint, SafeSelector(endpointSelector));
        }

        if (!string.IsNullOrWhiteSpace(options.FromWebFormsEvent))
        {
            return ("from-webforms-event", options.FromWebFormsEvent, SafeSelector(options.FromWebFormsEvent));
        }

        if (!string.IsNullOrWhiteSpace(options.FromSymbol))
        {
            return ("from-symbol", options.FromSymbol, SafeSelector(options.FromSymbol));
        }

        if (!string.IsNullOrWhiteSpace(options.FromSource))
        {
            return ("from-source", options.FromSource, SafeSelector(options.FromSource));
        }

        return ("selector", null, null);
    }

    private static string SelectorRedactionState(string? rawSelector, string? safeNormalizedKey, string? safeSelector)
    {
        if (string.IsNullOrWhiteSpace(rawSelector))
        {
            return "none";
        }

        var trimmed = rawSelector.Trim();
        var rawSafe = SafeSelector(trimmed);
        var rawRedacted = rawSafe is not null && rawSafe.StartsWith("redacted-hash:", StringComparison.Ordinal);
        var renderedRedacted = safeSelector is not null && safeSelector.StartsWith("redacted-hash:", StringComparison.Ordinal);
        if (rawRedacted && !renderedRedacted && !string.Equals(trimmed, safeNormalizedKey, StringComparison.Ordinal))
        {
            return "normalized-redacted";
        }

        if (rawRedacted || renderedRedacted)
        {
            return "redacted";
        }

        return string.Equals(trimmed, safeNormalizedKey ?? safeSelector, StringComparison.Ordinal)
            ? "none"
            : "normalized";
    }

    private static string RowKind(CombinedPathEdge edge, CombinedPathNode? previous)
    {
        if (edge.EdgeKind == "fact-attached-to-symbol" && previous?.NodeKind is "EndpointRoute" or "EndpointClient")
        {
            return "endpoint-method-bridge";
        }

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

    private static string EdgeKind(CombinedPathEdge edge, CombinedPathNode? previous)
    {
        if (edge.EdgeKind == "fact-attached-to-symbol" && previous?.NodeKind is "EndpointRoute" or "EndpointClient")
        {
            return "route-bound-to-symbol";
        }

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

        if (node is not null && sources.Any(source => string.Equals(source.SourceLabel, node.SourceLabel, StringComparison.Ordinal) && !source.IdentityVerified))
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

    // Lower rank is stronger. This helper intentionally returns the weaker
    // classification so composed route-flow rows never upgrade source evidence.
    private static string WeakestClassification(string left, string right)
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
        return gap.GapKind is "SelectorNoMatch" or "SchemaMissing" or "ExtractorUnavailable" or "UnknownCommitSha" or "UnknownAnalysisGap" or "ReducedCoverage" or "TruncatedByLimit" or "MissingRouteRoot" or "MissingMethodSymbolBridge" or "MissingCallEdge" or "IdentityGap" or "TraversalBounds";
    }

    private static bool IsNoEvidenceBlockingCompositionGap(RouteFlowGap gap)
    {
        return gap.GapKind is "MissingRouteRoot"
            or "MissingMethodSymbolBridge"
            or "MissingCallEdge"
            or "MissingImplementationBridge"
            or "ImplementationCandidateUnavailable"
            or "DataSurfaceAttachmentMissing"
            or "IdentityGap"
            or "TraversalBounds";
    }

    private static bool BlocksCleanNoEvidenceGap(RouteFlowGap gap)
    {
        return gap.GapKind != "NoRouteFlowEvidence"
            && (IsNoEvidenceBlockingCompositionGap(gap)
                || gap.GapKind is "SelectorNoMatch"
                || gap.GapKind is "ArgumentProjectionUnavailable"
                    or "FactSymbolProjectionUnavailable"
                    or "SchemaMissing"
                    or "ExtractorUnavailable"
                    or "UnknownCommitSha"
                    or "UnknownAnalysisGap"
                    or "TruncatedByLimit"
                    or "ReducedCoverage");
    }

    private static string GapClassification(RouteFlowGap gap)
    {
        return gap.GapKind is "NoRouteFlowEvidence"
            ? RouteFlowClassifications.NoRouteFlowEvidence
            : gap.GapKind is "SelectorNoMatch" or "SchemaMissing" or "ExtractorUnavailable" or "UnknownCommitSha" or "UnknownAnalysisGap" or "ReducedCoverage" or "MissingRouteRoot" or "MissingMethodSymbolBridge" or "MissingCallEdge" or "IdentityGap" or "TraversalBounds"
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
            "MissingRouteRoot" => "MissingRouteRoot",
            "MissingMethodSymbolBridge" => "MissingMethodSymbolBridge",
            "MissingCallEdge" => "MissingCallEdge",
            "MissingImplementationBridge" => "MissingImplementationBridge",
            "ImplementationCandidateUnavailable" => "ImplementationCandidateUnavailable",
            "AmbiguousImplementationCandidates" => "AmbiguousImplementationCandidates",
            "DispatchCandidateFanOut" => "DispatchCandidateFanOut",
            "DataSurfaceAttachmentMissing" => "DataSurfaceAttachmentMissing",
            "IdentityGap" => "IdentityGap",
            "TraversalBounds" => "TraversalBounds",
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
            SafeSelector(node.SurfaceKind) ?? "dependency-surface",
            SafeSelector(node.SurfaceSubtype),
            SafeLabel(node.SourceLabel),
            SafeSelector(node.NormalizedPathKey),
            SafeSelector(node.PackageName),
            SafeSelector(node.ConfigKey),
            SafeSelector(node.OperationName),
            SafeSelector(node.TableName),
            SafeSelector(node.ShapeHash),
            SafeSelector(node.TextHash),
            SafeSelector(node.SurfaceName ?? node.DisplayName)
        }.Where(item => !string.IsNullOrWhiteSpace(item)));
        return $"surface-key-hash:{CombinedReportHelpers.Hash(value, 32)}";
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

    private static string SafeCommitSha(string? value)
    {
        return KnownCommit(value)
            ? value!.Trim()
            : "unknown";
    }

    private static string? ExtractorVersionFor(string? sourceLabel, IReadOnlyList<RouteFlowSource> sources)
    {
        if (string.IsNullOrWhiteSpace(sourceLabel))
        {
            return null;
        }

        var safeLabel = SafeLabel(sourceLabel);
        return sources
            .Where(source => string.Equals(source.SourceLabel, safeLabel, StringComparison.Ordinal))
            .Select(source => SafeSelector(source.ScannerVersion))
            .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version));
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
            || (trimmed.StartsWith("/", StringComparison.Ordinal) && !IsSafeNormalizedPathKey(trimmed))
            || trimmed.Contains("SELECT ", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("TOKEN", StringComparison.OrdinalIgnoreCase))
        {
            return $"redacted-hash:{CombinedReportHelpers.Hash(trimmed, 16)}";
        }

        return trimmed.ReplaceLineEndings(" ");
    }

    private static bool IsSafeNormalizedPathKey(string value)
    {
        if (!value.StartsWith("/", StringComparison.Ordinal)
            || value.Contains("://", StringComparison.Ordinal)
            || value.Contains("..", StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('?', StringComparison.Ordinal)
            || value.Contains('#', StringComparison.Ordinal))
        {
            return false;
        }

        return value.All(ch => char.IsLetterOrDigit(ch)
            || ch is '/' or '-' or '_' or '.' or '{' or '}' or ':' or '~');
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
            ArgumentProjectionRuleId => ["Argument projection rows are direct static argument evidence only and do not prove full taint, mutation, alias, branch feasibility, or runtime values."],
            FactSymbolProjectionRuleId => ["Fact-symbol projection rows attach source-local facts to route-flow symbols only and do not prove runtime execution, database schema state, or dependency binding."],
            RedactionRuleId => ["Redaction records mean unsafe values are hashed or omitted and do not make the output public-approved."],
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
            "Argument-flow projection rows describe direct static argument evidence and do not prove full taint, mutation tracking, alias behavior, branch feasibility, or runtime values.",
            "Fact-symbol projection rows preserve source-local static attachments and do not prove runtime execution, database schema state, or dependency binding.",
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
        IReadOnlyList<RouteFlowTouchedFile> touchedFiles = report.TouchedFiles ?? [];
        IReadOnlyList<RouteFlowTouchedSymbol> touchedSymbols = report.TouchedSymbols ?? [];
        IReadOnlyList<RouteFlowContextGroup> contextGroups = report.ContextGroups ?? [];
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
        builder.AppendLine($"- Context groups: `{contextGroups.Count}`");
        builder.AppendLine($"- Touched files: `{touchedFiles.Count}`");
        builder.AppendLine($"- Touched symbols: `{touchedSymbols.Count}`");
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
        if (report.Query.SelectorTrace is { } selectorTrace)
        {
            builder.AppendLine($"- Selector trace: kind `{selectorTrace.SelectorKind}`, key `{selectorTrace.SafeNormalizedKey ?? "n/a"}`, match `{selectorTrace.MatchMode}`, redaction `{selectorTrace.RedactionState}`, coverage `{selectorTrace.Coverage}`, rule `{selectorTrace.RuleId}`");
        }
        else
        {
            builder.AppendLine("- Selector trace: `unavailable`");
        }

        builder.AppendLine($"- Bounds: depth `{report.Query.MaxDepth}`, paths `{report.Query.MaxPaths}`, frontier `{report.Query.MaxFrontier}`, logic rows `{report.Query.MaxLogicRows}`, gaps `{report.Query.MaxGaps}`");
        builder.AppendLine();

        builder.AppendLine("## Snapshot Sources");
        builder.AppendLine();
        AppendRows(builder, report.Snapshot.Sources, "| Source | Language | Commit | Analysis | Build | Identity |", "| --- | --- | --- | --- | --- | --- |",
            source => $"| {Cell(source.SourceLabel)} | {Cell(source.Language)} | {Cell(source.CommitSha ?? "unknown")} | {Cell(source.AnalysisLevel)} | {Cell(source.BuildStatus)} | {Cell(source.IdentityVerified.ToString())} |");

        builder.AppendLine("## Entry Evidence");
        builder.AppendLine();
        AppendRows(builder, report.EntryEvidence, "| Kind | Method | Path key | Bridge | Classification | Evidence |", "| --- | --- | --- | --- | --- | --- |",
            row => $"| {Cell(row.EntryKind)} | {Cell(row.Method)} | {Cell(row.NormalizedPathKey)} | {Cell(row.BridgeState)} | {Cell(row.Classification)} | {Cell(Evidence(row.Evidence))} |");

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
        AppendRows(builder, report.DependencySurfaces, "| Kind | Subtype | Name | Stable key | Classification | Evidence |", "| --- | --- | --- | --- | --- | --- |",
            row => $"| {Cell(row.SurfaceKind)} | {Cell(row.SurfaceSubtype ?? "n/a")} | {Cell(row.DisplayName)} | {Cell(row.StableKey)} | {Cell(row.Classification)} | {Cell(Evidence(row.Evidence))} |");

        builder.AppendLine("## Context Groups");
        builder.AppendLine();
        AppendRows(builder, contextGroups, "| Kind | Name | Match | Value safety | Classification | Coverage | Supporting rows | Evidence |", "| --- | --- | --- | --- | --- | --- | --- | --- |",
            row => $"| {Cell(row.GroupKind)} | {Cell(row.DisplayName)} | {Cell(row.MatchKind)} | {Cell(row.ValueSafety)} | {Cell(row.Classification)} | {Cell(row.Coverage)} | {Cell(string.Join(", ", row.SupportingRowIds))} | {Cell(Evidence(row.Evidence))} |");

        builder.AppendLine("## Touched Files");
        builder.AppendLine();
        AppendRows(builder, touchedFiles, "| Source | File | Lines | Classification | Coverage | Supporting rows | Evidence |", "| --- | --- | --- | --- | --- | --- | --- |",
            row => $"| {Cell(row.SourceLabel)} | {Cell(row.FilePath)} | {Cell(LineSpan(row.FirstStartLine, row.LastEndLine))} | {Cell(row.Classification)} | {Cell(row.Coverage)} | {Cell(string.Join(", ", row.SupportingRowIds))} | {Cell(Evidence(row.Evidence))} |");

        builder.AppendLine("## Touched Symbols");
        builder.AppendLine();
        AppendRows(builder, touchedSymbols, "| Source | Symbol | Kind | File | Lines | Classification | Coverage | Supporting rows | Evidence |", "| --- | --- | --- | --- | --- | --- | --- | --- | --- |",
            row => $"| {Cell(row.SourceLabel)} | {Cell(row.DisplayName)} | {Cell(row.SymbolKind)} | {Cell(row.FilePath ?? "n/a")} | {Cell(LineSpan(row.StartLine, row.EndLine))} | {Cell(row.Classification)} | {Cell(row.Coverage)} | {Cell(string.Join(", ", row.SupportingRowIds))} | {Cell(Evidence(row.Evidence))} |");

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

    private static string LineSpan(int? startLine, int? endLine)
    {
        return startLine.HasValue || endLine.HasValue
            ? $"{startLine?.ToString() ?? "?"}-{endLine?.ToString() ?? "?"}"
            : "n/a";
    }

    private static string Cell(string? value)
    {
        return CombinedReportHelpers.Cell(value);
    }
}
