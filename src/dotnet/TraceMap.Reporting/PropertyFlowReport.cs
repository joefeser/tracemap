using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record PropertyFlowOptions(
    string IndexPath,
    string OutputPath,
    string PropertySelector,
    string Format = "markdown",
    string? Source = null,
    string Framework = "any",
    int MaxRoots = 25,
    int MaxDepth = 10,
    int MaxPaths = 100,
    int MaxFrontier = 10000,
    int MaxInventory = 1000,
    int MaxGaps = 1000,
    string? ObservedEvidencePath = null);

public sealed record PropertyFlowResult(PropertyFlowReport Report, string? MarkdownPath, string? JsonPath);

public sealed record PropertyFlowReport(
    string ReportType,
    string Version,
    string ReportCoverage,
    IReadOnlyList<PropertyFlowCoverageWarning> CoverageWarnings,
    PropertyFlowQuery Query,
    PropertyFlowSnapshot Snapshot,
    PropertyFlowSummary Summary,
    IReadOnlyList<PropertyFlowSource> Sources,
    IReadOnlyList<PropertyFlowRoot> SelectedRoots,
    IReadOnlyList<PropertyFlowPath> LineagePaths,
    IReadOnlyList<PropertyFlowGap> Gaps,
    PropertyFlowInventory Inventory,
    IReadOnlyList<PropertyFlowObservedEvidence> ObservedEvidence,
    IReadOnlyList<string> Limitations);

public sealed record PropertyFlowQuery(
    string SelectorKind,
    string NormalizedSelector,
    string? SourceFilter,
    string FrameworkFilter,
    int MaxRoots,
    int MaxPaths,
    int MaxDepth,
    int MaxFrontier,
    int MaxInventoryRows,
    int MaxGaps,
    string Algorithm,
    string AlgorithmVersion);

public sealed record PropertyFlowSnapshot(
    string InputKind,
    string? CombinedIndexHash,
    string? RepositoryIdentityHash,
    int SourceCount,
    IReadOnlyList<PropertyFlowSource> Sources,
    PropertyFlowSchemaSummary Schema);

public sealed record PropertyFlowSchemaSummary(
    bool RequiredObjectsPresent,
    IReadOnlyList<string> MissingOptionalObjects,
    string RouteFlowSignal);

public sealed record PropertyFlowSummary(
    string Classification,
    string ReportCoverage,
    int SelectedRootCount,
    int TotalCandidateCount,
    int PathCount,
    int GapCount,
    bool Truncated,
    IReadOnlyDictionary<string, int> RootsByKind,
    IReadOnlyDictionary<string, int> PathsByClassification,
    IReadOnlyDictionary<string, int> GapsByKind);

public sealed record PropertyFlowSource(
    string SourceIndexId,
    string SourceLabel,
    string? RepositoryIdentityHash,
    string ScanId,
    string CommitSha,
    string ScannerVersion,
    IReadOnlyDictionary<string, string> ExtractorVersions,
    string AnalysisLevel,
    string BuildStatus,
    IReadOnlyList<string> CoverageLabels);

public sealed record PropertyFlowLineSpan(int? StartLine, int? EndLine);

public sealed record PropertyFlowCoverageWarning(
    string WarningId,
    string Message,
    string RuleId,
    string EvidenceTier,
    IReadOnlyList<string> SupportingSourceIds,
    IReadOnlyList<string> SourceLabels,
    IReadOnlyList<string> CommitShas,
    string ExtractorId,
    string ExtractorVersion,
    IReadOnlyList<string> Limitations);

public sealed record PropertyFlowRoot(
    string RootId,
    string RootKind,
    string Classification,
    string SourceLabel,
    string SourceIndexId,
    string? RepositoryIdentityHash,
    string ScanId,
    string CommitSha,
    string CombinedFactId,
    string? SymbolId,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine,
    string ExtractorId,
    string ExtractorVersion,
    IReadOnlyDictionary<string, string> SafeDisplay,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> Limitations);

public sealed record PropertyFlowPath(
    string PathId,
    string Classification,
    string Confidence,
    int Length,
    string StartRootId,
    string EndNodeId,
    IReadOnlyList<PropertyFlowNode> Nodes,
    IReadOnlyList<PropertyFlowEdge> Edges,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> Notes);

public sealed record PropertyFlowNode(
    string NodeId,
    string NodeKind,
    string DisplayName,
    string SourceIndexId,
    string SourceLabel,
    string? RepositoryIdentityHash,
    string? ScanId,
    string? CommitSha,
    string? SymbolId,
    string? CombinedFactId,
    string? RuleId,
    string? EvidenceTier,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyDictionary<string, string> SafeMetadata);

public sealed record PropertyFlowEdge(
    string EdgeId,
    string EdgeKind,
    string FromNodeId,
    string ToNodeId,
    string Classification,
    string RuleId,
    string EvidenceTier,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> SupportingCombinedEdgeIds,
    string? FilePath,
    int? StartLine,
    int? EndLine)
{
    public string? SourceIndexId { get; init; }
    public string? SourceLabel { get; init; }
    public string? ScanId { get; init; }
    public string? CommitSha { get; init; }
    public string ExtractorId { get; init; } = "property-flow";
    public string ExtractorVersion { get; init; } = "property-flow/1.0";
    public PropertyFlowLineSpan? LineSpan { get; init; } = StartLine is null && EndLine is null ? null : new PropertyFlowLineSpan(StartLine, EndLine);
}

public sealed record PropertyFlowGap(
    string GapId,
    string GapKind,
    string Classification,
    string Message,
    string RuleId,
    string EvidenceTier,
    string? SourceLabel,
    string? CombinedFactId,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> Limitations)
{
    public string? SourceIndexId { get; init; }
    public string? ScanId { get; init; }
    public string? CommitSha { get; init; }
    public IReadOnlyList<string> CommitShas { get; init; } = [];
    public IReadOnlyList<string> SupportingSourceIds { get; init; } = [];
    public string ExtractorId { get; init; } = "property-flow";
    public string ExtractorVersion { get; init; } = "property-flow/1.0";
    public PropertyFlowLineSpan? LineSpan { get; init; } = StartLine is null && EndLine is null ? null : new PropertyFlowLineSpan(StartLine, EndLine);
}

public sealed record PropertyFlowInventory(
    IReadOnlyDictionary<string, int> RootsByKind,
    IReadOnlyDictionary<string, int> NodesByKind,
    IReadOnlyDictionary<string, int> EdgesByKind,
    IReadOnlyList<PropertyFlowRoot> RootRows,
    IReadOnlyList<PropertyFlowNode> EvidenceNodes,
    IReadOnlyList<PropertyFlowEdge> EvidenceEdges);

public sealed record PropertyFlowObservedEvidence(
    string EvidenceId,
    string Label,
    string Classification,
    string RuleId,
    string EvidenceTier,
    IReadOnlyDictionary<string, string> SafeMetadata);

internal sealed record PropertySelector(string Kind, string Value, string Normalized);

internal sealed record PropertyFactRow(
    string CombinedFactId,
    string SourceIndexId,
    string SourceLabel,
    string ScanId,
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

public static class PropertyFlowClassifications
{
    public const string StrongStaticLineage = nameof(StrongStaticLineage);
    public const string ProbableStaticLineage = nameof(ProbableStaticLineage);
    public const string NeedsReviewLineage = nameof(NeedsReviewLineage);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string NoLineageEvidence = nameof(NoLineageEvidence);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
    public const string TruncatedByLimit = nameof(TruncatedByLimit);
    public const string ObservedDemoContext = nameof(ObservedDemoContext);
}

public static class PropertyFlowReporter
{
    private const string ReportType = "property-flow";
    private const string Version = "1.0";
    private const string Algorithm = "bounded-bfs";
    private const string AlgorithmVersion = "1.0";
    private const string RootRuleId = "property-flow.root.v1";
    private const string EdgeRuleId = "property-flow.edge.v1";
    private const string PathRuleId = "property-flow.path.v1";
    private const string SelectorRuleId = "property-flow.selector.v1";
    private const string CoverageRuleId = "property-flow.coverage.v1";
    private const string SchemaRuleId = "property-flow.schema.v1";
    private const string TruncationRuleId = "property-flow.truncation.v1";
    private const string ObservedRuleId = "property-flow.observed-evidence.v1";
    private const int MaxObservedEvidenceBytes = 256 * 1024;
    private const int MaxObservedEvidenceRows = 50;

    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "name", "type", "value", "state", "status"
    };

    private static readonly HashSet<string> RequiredObjects = new(StringComparer.Ordinal)
    {
        "index_sources", "combined_facts", "combined_dependency_edges"
    };

    private static readonly HashSet<string> OptionalObjects = new(StringComparer.Ordinal)
    {
        "combined_symbols",
        "combined_fact_symbols",
        "combined_call_edges",
        "combined_object_creations",
        "combined_symbol_relationships",
        "combined_argument_flows",
        "combined_local_aliases",
        "combined_field_aliases",
        "combined_parameter_forward_edges",
        "combined_route_flow_edges"
    };

    private static readonly HashSet<string> ObservedMetadataKeys = new(StringComparer.Ordinal)
    {
        "artifactHash",
        "bindingKind",
        "captureMode",
        "controlName",
        "evidenceHash",
        "field",
        "label",
        "noteCode",
        "observationKind",
        "propertyName",
        "propertyPath",
        "runId",
        "selector",
        "sourceLabel",
        "tool"
    };

    private static readonly IReadOnlyList<string> Limitations =
    [
        "Property-flow is deterministic static evidence. It does not prove runtime rendering, user visibility, submission, execution, authorization, deployment, or production use.",
        "Same-name, syntax-only, generic-property, dynamic-template, serializer, runtime DI, reflection, callback, mutation, and branch-feasibility boundaries are review-tier or gap evidence.",
        "Reports omit raw source snippets, raw SQL, raw URLs, remotes, connection strings, credentials, local absolute paths, and unsafe literal values by default."
    ];

    public static async Task<PropertyFlowResult> WriteAsync(PropertyFlowOptions options, CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(options, cancellationToken);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "property-flow");
        var (markdownPath, jsonPath) = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "property-flow-report.md",
            "property-flow-report.json",
            report,
            RenderMarkdown,
            CombinedDependencyReporter.JsonOptions,
            cancellationToken);
        return new PropertyFlowResult(report, markdownPath, jsonPath);
    }

    public static async Task<PropertyFlowReport> BuildReportAsync(PropertyFlowOptions options, CancellationToken cancellationToken = default)
    {
        var selector = ParseSelector(options.PropertySelector);
        ValidateOptions(options, selector);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "property-flow");
        _ = format;
        var framework = NormalizeFramework(options.Framework);
        var sourceFilter = SafeFilter(options.Source);
        var observedEvidence = await ReadObservedEvidenceAsync(options.ObservedEvidencePath, cancellationToken);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.IndexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ValidateCombinedIndexAsync(connection, cancellationToken);

        var read = await CombinedDependencyReporter.ReadAsync(connection, cancellationToken);
        var graph = await CombinedDependencyPathReporter.BuildGraphInventoryAsync(options.IndexPath, cancellationToken: cancellationToken);
        var facts = await ReadFactsAsync(connection, cancellationToken);
        var missingOptional = await MissingOptionalObjectsAsync(connection, cancellationToken);
        var routeFlowSignal = await RouteFlowSignalAsync(connection, missingOptional, cancellationToken);
        var sources = read.Sources.Select(ToSource).OrderBy(source => source.SourceLabel, StringComparer.Ordinal).ThenBy(source => source.SourceIndexId, StringComparer.Ordinal).ToArray();
        var sourceIds = sources.Select(source => source.SourceIndexId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var sourceCommitShas = sources.Select(source => source.CommitSha).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

        var allCandidates = MatchCandidates(facts, selector, sourceFilter, framework).ToArray();
        var totalCandidateCount = allCandidates.Length;
        var selectedFacts = allCandidates
            .OrderBy(fact => fact.SourceLabel, StringComparer.Ordinal)
            .ThenBy(fact => RootKind(fact), StringComparer.Ordinal)
            .ThenBy(fact => SafeDisplayName(fact), StringComparer.Ordinal)
            .ThenBy(fact => fact.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.StartLine)
            .ThenBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
            .Take(options.MaxRoots)
            .ToArray();
        var roots = selectedFacts.Select((fact, index) => ToRoot(fact, selector, SourceIdentityHash(read.Sources, fact.SourceIndexId), index)).ToArray();
        var gaps = new List<PropertyFlowGap>();

        foreach (var missing in missingOptional.Where(item => item != "combined_route_flow_edges").OrderBy(item => item, StringComparer.Ordinal))
        {
            gaps.Add(new PropertyFlowGap(
                $"gap:schema:{missing}",
                "MissingOptionalSchema",
                PropertyFlowClassifications.UnknownAnalysisGap,
                $"Optional precision table or view `{missing}` is missing; lineage precision may be reduced.",
                SchemaRuleId,
                EvidenceTiers.Tier4Unknown,
                null,
                null,
                null,
                null,
                null,
                [],
                ["Missing optional schema is an availability gap, not proof of no lineage."])
            {
                SupportingSourceIds = sourceIds,
                CommitShas = sourceCommitShas
            });
        }

        if (routeFlowSignal != "available")
        {
            gaps.Add(new PropertyFlowGap(
                "gap:schema:route-flow-unavailable",
                "RouteFlowUnavailable",
                PropertyFlowClassifications.UnknownAnalysisGap,
                routeFlowSignal == "empty"
                    ? "Route-flow schema signal was available but contained no rows; route-flow-specific downstream traversal is not promoted by property-flow."
                    : "No route-flow schema signal was available; route-flow-specific downstream traversal is not promoted by property-flow.",
                SchemaRuleId,
                EvidenceTiers.Tier4Unknown,
                null,
                null,
                null,
                null,
                null,
                [],
                ["Route-flow absence limits downstream route-specific conclusions. Existing combined path evidence may still be shown."])
            {
                SupportingSourceIds = sourceIds,
                CommitShas = sourceCommitShas
            });
        }

        if (totalCandidateCount == 0)
        {
            gaps.Add(new PropertyFlowGap(
                "gap:selector:no-match",
                "SelectorNoMatch",
                PropertyFlowClassifications.SelectorNoMatch,
                "No property-flow roots matched the selector under available combined-index evidence.",
                SelectorRuleId,
                EvidenceTiers.Tier4Unknown,
                sourceFilter,
                null,
                null,
                null,
                null,
                [],
                ["Selector matching is static and coverage-relative."])
            {
                SupportingSourceIds = sourceIds,
                CommitShas = sourceCommitShas
            });
        }
        else if (totalCandidateCount > roots.Length)
        {
            gaps.Add(new PropertyFlowGap(
                "gap:selector:root-cap",
                "AmbiguousSelector",
                PropertyFlowClassifications.NeedsReviewLineage,
                $"Selector matched {totalCandidateCount} candidate roots; only the first {roots.Length} deterministic roots are reported.",
                SelectorRuleId,
                EvidenceTiers.Tier3SyntaxOrTextual,
                sourceFilter,
                null,
                null,
                null,
                null,
                selectedFacts.Select(fact => fact.CombinedFactId).ToArray(),
                ["Use --source, model:/dto:, symbol:, or fact: selectors to narrow ambiguous properties."])
            {
                SupportingSourceIds = selectedFacts.Select(fact => fact.SourceIndexId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                CommitShas = selectedFacts.Select(fact => fact.CommitSha).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            });
        }

        if (selector.Kind is "field" or "control" or "binding"
            && GenericNames.Contains(selector.Value)
            && totalCandidateCount >= 10)
        {
            gaps.Add(new PropertyFlowGap(
                $"gap:selector:generic-fan-out:{selector.Kind}",
                "GenericPropertyFanOut",
                PropertyFlowClassifications.NeedsReviewLineage,
                "A generic property selector matched the v1 high fan-out threshold; lineage is capped at review-tier.",
                SelectorRuleId,
                EvidenceTiers.Tier3SyntaxOrTextual,
                sourceFilter,
                null,
                null,
                null,
                null,
                selectedFacts.Select(fact => fact.CombinedFactId).ToArray(),
                ["Generic/common property names require exact fact, symbol, source, type, or alias evidence before stronger lineage is reported."])
            {
                SupportingSourceIds = selectedFacts.Select(fact => fact.SourceIndexId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                CommitShas = selectedFacts.Select(fact => fact.CommitSha).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            });
        }

        var paths = BuildPaths(roots, selectedFacts, facts, graph, options, gaps);
        var routeFlowMatchesSelectedEndpoint = await RouteFlowMatchesSelectedEndpointAsync(connection, routeFlowSignal, paths.Paths, cancellationToken);
        AddRouteFlowContextGaps(routeFlowSignal, routeFlowMatchesSelectedEndpoint, roots, paths.Paths, sourceIds, sourceCommitShas, gaps);
        var truncated = roots.Length < totalCandidateCount || paths.Truncated || gaps.Count > options.MaxGaps;
        if (truncated)
        {
            gaps.Add(new PropertyFlowGap(
                "gap:truncated",
                "TruncatedByLimit",
                PropertyFlowClassifications.TruncatedByLimit,
                "Property-flow output was truncated by configured bounds.",
                TruncationRuleId,
                EvidenceTiers.Tier4Unknown,
                null,
                null,
                null,
                null,
                null,
                [],
                ["Increase max roots, depth, paths, frontier, inventory, or gaps for a wider static search."])
            {
                SupportingSourceIds = sourceIds,
                CommitShas = sourceCommitShas
            });
        }

        var coverageWarnings = ToCoverageWarnings(read.CoverageWarnings, sources);
        var sourceReduced = coverageWarnings.Length > 0 || sources.Any(source => source.CoverageLabels.Contains("ReducedCoverage", StringComparer.Ordinal));
        if (roots.Length > 0 && paths.Paths.Count == 0)
        {
            gaps.Add(new PropertyFlowGap(
                "gap:coverage:no-path",
                sourceReduced ? "ReducedCoverage" : "NoLineageEvidence",
                sourceReduced ? PropertyFlowClassifications.UnknownAnalysisGap : PropertyFlowClassifications.NoLineageEvidence,
                sourceReduced
                    ? "Selected roots had no downstream path under reduced coverage; this is not proof that no lineage exists."
                    : "Selected roots had no downstream path under available full static coverage.",
                CoverageRuleId,
                sourceReduced ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
                null,
                null,
                null,
                null,
                null,
                roots.Select(root => root.CombinedFactId).ToArray(),
                ["No-lineage conclusions are coverage-relative."])
            {
                SupportingSourceIds = roots.Select(root => root.SourceIndexId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                CommitShas = roots.Select(root => root.CommitSha).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            });
        }

        var cappedGaps = gaps
            .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
            .ThenBy(gap => gap.FilePath, StringComparer.Ordinal)
            .ThenBy(gap => gap.StartLine ?? 0)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .Take(options.MaxGaps)
            .ToArray();
        var evidenceNodes = graph.Nodes.Select(ToNode).Take(options.MaxInventory).ToArray();
        var graphNodesById = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var evidenceEdges = graph.Edges.Select(edge => ToEdge(edge, graphNodesById)).Take(options.MaxInventory).ToArray();
        var reportCoverage = CoverageLabel(coverageWarnings.Length > 0, truncated, cappedGaps);
        var classification = SummaryClassification(roots, paths.Paths, cappedGaps);

        return new PropertyFlowReport(
            ReportType,
            Version,
            reportCoverage,
            coverageWarnings,
            new PropertyFlowQuery(
                selector.Kind,
                selector.Normalized,
                sourceFilter,
                framework,
                options.MaxRoots,
                options.MaxPaths,
                options.MaxDepth,
                options.MaxFrontier,
                options.MaxInventory,
                options.MaxGaps,
                Algorithm,
                AlgorithmVersion),
            new PropertyFlowSnapshot(
                "combined-index",
                CombinedReportHelpers.Hash(Path.GetFullPath(options.IndexPath), 16),
                RepositoryIdentityHash(sources),
                sources.Length,
                sources,
                new PropertyFlowSchemaSummary(true, missingOptional.OrderBy(item => item, StringComparer.Ordinal).ToArray(), routeFlowSignal)),
            new PropertyFlowSummary(
                classification,
                reportCoverage,
                roots.Length,
                totalCandidateCount,
                paths.Paths.Count,
                cappedGaps.Length,
                truncated,
                CountBy(roots, root => root.RootKind),
                CountBy(paths.Paths, path => path.Classification),
                CountBy(cappedGaps, gap => gap.GapKind)),
            sources,
            roots,
            paths.Paths,
            cappedGaps,
            new PropertyFlowInventory(
                CountBy(allCandidates.Select(fact => RootKind(fact)), value => value),
                graph.InventoryNodesByKind(options.MaxInventory),
                graph.InventoryEdgesByKind(options.MaxInventory),
                roots.Take(options.MaxInventory).ToArray(),
                evidenceNodes,
                evidenceEdges),
            observedEvidence,
            Limitations);
    }

    private static async Task<IReadOnlyList<PropertyFlowObservedEvidence>> ReadObservedEvidenceAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        if (!File.Exists(path))
        {
            throw new ArgumentException("property-flow observed evidence file was not found.");
        }

        if (new FileInfo(path).Length > MaxObservedEvidenceBytes)
        {
            throw new ArgumentException("property-flow observed evidence file exceeds the supported size limit.");
        }

        await using var stream = OpenObservedEvidenceFile(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var rowsElement = document.RootElement;
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            if (!document.RootElement.TryGetProperty("observedEvidence", out rowsElement))
            {
                throw new ArgumentException("property-flow observed evidence JSON must contain observedEvidence.");
            }
        }

        if (rowsElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("property-flow observedEvidence must be an array.");
        }

        var rows = new List<(string Label, IReadOnlyDictionary<string, string> Metadata)>();
        foreach (var row in rowsElement.EnumerateArray())
        {
            if (rows.Count >= MaxObservedEvidenceRows)
            {
                throw new ArgumentException("property-flow observed evidence row limit exceeded.");
            }

            if (row.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("property-flow observed evidence rows must be objects.");
            }

            var label = RequiredString(row, "label", "observed evidence label");
            if (!IsSafeObservedEvidenceString(label))
            {
                throw new ArgumentException("property-flow rejected unsafe observed evidence metadata.");
            }

            var metadata = ReadObservedMetadata(row);
            if (metadata.Count == 0)
            {
                throw new ArgumentException("property-flow observed evidence requires non-empty safeMetadata.");
            }

            rows.Add((label, metadata));
        }

        return rows
            .OrderBy(row => row.Label, StringComparer.Ordinal)
            .ThenBy(row => string.Join("|", row.Metadata.Select(pair => $"{pair.Key}={pair.Value}")), StringComparer.Ordinal)
            .Take(50)
            .Select((row, index) =>
            {
                var stable = string.Join("|", row.Metadata.Select(pair => $"{pair.Key}={pair.Value}"));
                return new PropertyFlowObservedEvidence(
                    $"observed-{index + 1:D4}-{CombinedReportHelpers.Hash(row.Label + ":" + stable, 12)}",
                    row.Label,
                    PropertyFlowClassifications.ObservedDemoContext,
                    ObservedRuleId,
                    EvidenceTiers.Tier4Unknown,
                    row.Metadata);
            })
            .ToArray();
    }

    private static FileStream OpenObservedEvidenceFile(string path)
    {
        try
        {
            return File.OpenRead(path);
        }
        catch (IOException exception)
        {
            throw new ArgumentException("property-flow observed evidence file could not be read.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ArgumentException("property-flow observed evidence file could not be read.", exception);
        }
    }

    private static string RequiredString(JsonElement row, string propertyName, string label)
    {
        if (!row.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new ArgumentException($"property-flow observed evidence requires {label}.");
        }

        return value.GetString()!.Trim();
    }

    private static IReadOnlyDictionary<string, string> ReadObservedMetadata(JsonElement row)
    {
        if (!row.TryGetProperty("safeMetadata", out var metadataElement))
        {
            throw new ArgumentException("property-flow observed evidence requires non-empty safeMetadata.");
        }

        if (metadataElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("property-flow observed evidence safeMetadata must be an object.");
        }

        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in metadataElement.EnumerateObject())
        {
            if (!ObservedMetadataKeys.Contains(property.Name) || SensitiveObservedToken(property.Name))
            {
                throw new ArgumentException("property-flow rejected unsafe observed evidence metadata.");
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("property-flow observed evidence safeMetadata values must be strings.");
            }

            var value = property.Value.GetString()?.Trim() ?? string.Empty;
            if (!IsSafeObservedEvidenceString(value))
            {
                throw new ArgumentException("property-flow rejected unsafe observed evidence metadata.");
            }

            values[property.Name] = value;
        }

        return values;
    }

    private static bool SensitiveObservedToken(string value)
    {
        return Regex.IsMatch(value, "(secret|token|password|cookie|credential|authorization|bearer|connectionstring|rawsql|snippet|hostname|remoteurl|apikey|api_key|api-key|production|prod-|live-|live_|runtime|http)", RegexOptions.IgnoreCase);
    }

    private static bool IsSafeObservedEvidenceString(string value)
    {
        if (!IsSafeValue(value) || SensitiveObservedToken(value))
        {
            return false;
        }

        return !value.Contains('/', StringComparison.Ordinal)
            && !value.Contains('\\', StringComparison.Ordinal);
    }

    private static void ValidateOptions(PropertyFlowOptions options, PropertySelector selector)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("property-flow requires --index <combined.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("property-flow requires --out <path>.");
        }

        if (selector.Value.Length == 0)
        {
            throw new ArgumentException("property-flow requires a non-empty --property selector value.");
        }
    }

    private static PropertySelector ParseSelector(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("property-flow requires --property <selector>.");
        }

        var trimmed = raw.Trim();
        if (IsUnsafeSelector(trimmed))
        {
            throw new ArgumentException("property-flow rejected an unsafe selector. Use a safe field/control/binding/model/dto/symbol/fact selector.");
        }

        var separator = trimmed.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
        {
            throw new ArgumentException("property-flow --property must start with field:, control:, binding:, model:, dto:, symbol:, or fact:.");
        }

        var kind = trimmed[..separator].ToLowerInvariant();
        var value = trimmed[(separator + 1)..].Trim();
        if (kind is not ("field" or "control" or "binding" or "model" or "dto" or "symbol" or "fact"))
        {
            throw new ArgumentException("property-flow --property must start with field:, control:, binding:, model:, dto:, symbol:, or fact:.");
        }

        if ((kind is "model" or "dto") && !value.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException($"property-flow {kind}: selectors require <type>.<property>.");
        }

        return new PropertySelector(kind, value, $"{kind}:{value}");
    }

    private static bool IsUnsafeSelector(string value)
    {
        return value.Contains('\n', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal)
            || value.Contains("://", StringComparison.Ordinal)
            || Regex.IsMatch(value, @"(^|[,\s])(/|~\/|[A-Za-z]:\\)")
            || Regex.IsMatch(value, @"(?i)(password|secret|token|apikey|api_key|connectionstring)\s*[=:]");
    }

    private static string NormalizeFramework(string? framework)
    {
        return (framework ?? "any").Trim().ToLowerInvariant() switch
        {
            "" or "any" => "any",
            "angular" => "angular",
            "razor" => "razor",
            _ => throw new ArgumentException("property-flow --framework must be angular, razor, or any.")
        };
    }

    private static string? SafeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return IsUnsafeSelector(trimmed) ? $"unsafe-filter-hash:{CombinedReportHelpers.Hash(trimmed, 16)}" : trimmed;
    }

    private static async Task ValidateCombinedIndexAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        if (!await TableExistsAsync(connection, "index_sources", cancellationToken))
        {
            missing.Add("index_sources");
        }

        if (!await TableExistsAsync(connection, "combined_facts", cancellationToken))
        {
            missing.Add("combined_facts");
        }

        if (!await ViewExistsAsync(connection, "combined_dependency_edges", cancellationToken))
        {
            missing.Add("combined_dependency_edges");
        }

        if (missing.Count > 0)
        {
            throw new InvalidDataException($"property-flow requires a combined index produced by tracemap combine; missing required object(s): {string.Join(", ", missing)}.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from index_sources;";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 0)
        {
            throw new InvalidDataException("property-flow requires a combined index with at least one index_sources row.");
        }
    }

    private static async Task<IReadOnlyList<string>> MissingOptionalObjectsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        foreach (var name in OptionalObjects.OrderBy(value => value, StringComparer.Ordinal))
        {
            var exists = name == "combined_route_flow_edges"
                ? await TableExistsAsync(connection, name, cancellationToken) || await ViewExistsAsync(connection, name, cancellationToken)
                : await TableExistsAsync(connection, name, cancellationToken);
            if (!exists)
            {
                missing.Add(name);
            }
        }

        return missing;
    }

    private static async Task<string> RouteFlowSignalAsync(SqliteConnection connection, IReadOnlyList<string> missingOptional, CancellationToken cancellationToken)
    {
        if (missingOptional.Contains("combined_route_flow_edges", StringComparer.Ordinal))
        {
            return "unavailable";
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from combined_route_flow_edges;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 0 ? "empty" : "available";
    }

    private static async Task<IReadOnlyList<PropertyFactRow>> ReadFactsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select cf.combined_fact_id,
                   cf.source_index_id,
                   s.label,
                   cf.scan_id,
                   cf.commit_sha,
                   cf.fact_type,
                   cf.rule_id,
                   cf.evidence_tier,
                   cf.source_symbol,
                   cf.target_symbol,
                   cf.contract_element,
                   cf.file_path,
                   cf.start_line,
                   cf.end_line,
                   cf.properties_json
            from combined_facts cf
            join index_sources s on s.source_index_id = cf.source_index_id
            order by s.label, cf.file_path, cf.start_line, cf.fact_type, cf.combined_fact_id;
            """;
        var rows = new List<PropertyFactRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PropertyFactRow(
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
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetString(11),
                reader.GetInt32(12),
                reader.GetInt32(13),
                ParseProperties(reader.GetString(14))));
        }

        return rows;
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string json)
    {
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

    private static IEnumerable<PropertyFactRow> MatchCandidates(IEnumerable<PropertyFactRow> facts, PropertySelector selector, string? sourceFilter, string framework)
    {
        foreach (var fact in facts)
        {
            if (!IsRootCandidate(fact, selector.Kind))
            {
                continue;
            }

            if (sourceFilter is not null && !fact.SourceLabel.Equals(sourceFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (framework != "any" && IsUiFact(fact) && !Framework(fact).Equals(framework, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (MatchesSelector(fact, selector))
            {
                yield return fact;
            }
        }
    }

    private static bool IsRootCandidate(PropertyFactRow fact, string selectorKind)
    {
        return selectorKind switch
        {
            "field" or "control" or "binding" => fact.FactType is "UiTemplateBinding" or "UiFormControlBinding" or "UiEventBinding" or "UiTemplateVariable" or "RazorBinding" or "RazorFormTarget",
            "model" => fact.FactType is "RazorBinding" or "RazorModelBindingTarget" or "PropertyDeclared" or "SerializerContractMember",
            "dto" => fact.FactType is "RazorModelBindingTarget" or "PropertyDeclared" or "SerializerContractMember" or "ParameterDeclared",
            "symbol" => true,
            "fact" => true,
            _ => false
        };
    }

    private static bool MatchesSelector(PropertyFactRow fact, PropertySelector selector)
    {
        if (selector.Kind == "fact")
        {
            return fact.CombinedFactId.Equals(selector.Value, StringComparison.Ordinal);
        }

        if (selector.Kind == "symbol")
        {
            return StringEqualsAny(selector.Value, fact.SourceSymbol, fact.TargetSymbol, CombinedDependencyReporter.FirstValue(fact.Properties, "symbolId", "targetSymbolId", "memberSymbolId", "componentSymbolId", "targetSymbolDisplayName"));
        }

        if (selector.Kind is "model" or "dto")
        {
            var (typeName, propertyName) = SplitTypeProperty(selector.Value);
            var typeMatch = StringEqualsAny(typeName, CombinedDependencyReporter.FirstValue(fact.Properties, "modelType", "typeName", "containingType", "declaringType", "dtoType"));
            var propMatch = StringEqualsAny(propertyName, CombinedDependencyReporter.FirstValue(fact.Properties, "propertyName", "memberName", "name"), fact.ContractElement, fact.TargetSymbol?.Split('.').LastOrDefault());
            if (!typeMatch || !propMatch)
            {
                return false;
            }

            var families = FamilyTokens(fact);
            return selector.Kind == "model"
                ? !families.SetEquals(["dto"])
                : families.Contains("dto") || fact.FactType == "SerializerContractMember";
        }

        var value = selector.Value;
        return selector.Kind switch
        {
            "field" => StringEqualsAny(value,
                CombinedDependencyReporter.FirstValue(fact.Properties, "fieldName", "controlName", "formControlName", "propertyName", "memberName", "name"),
                LastPathSegment(CombinedDependencyReporter.FirstValue(fact.Properties, "propertyPath")),
                fact.ContractElement),
            "control" => StringEqualsAny(value,
                CombinedDependencyReporter.FirstValue(fact.Properties, "controlName", "formControlName", "name", "fieldName"),
                fact.ContractElement),
            "binding" => StringEqualsAny(value,
                CombinedDependencyReporter.FirstValue(fact.Properties, "propertyPath", "bindingName", "propertyName", "memberName", "name"),
                fact.ContractElement,
                fact.TargetSymbol),
            _ => false
        };
    }

    private static (string TypeName, string PropertyName) SplitTypeProperty(string value)
    {
        var index = value.LastIndexOf('.');
        return (value[..index], value[(index + 1)..]);
    }

    private static bool StringEqualsAny(string expected, params string?[] values)
    {
        return values.Any(value => value is not null && value.Equals(expected, StringComparison.OrdinalIgnoreCase));
    }

    private static string? LastPathSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.LastOrDefault();
    }

    private static string Framework(PropertyFactRow fact)
    {
        return CombinedDependencyReporter.FirstValue(fact.Properties, "uiFramework") ?? (fact.FactType.StartsWith("Razor", StringComparison.Ordinal) ? "razor" : "any");
    }

    private static bool IsUiFact(PropertyFactRow fact)
    {
        return fact.FactType.StartsWith("Ui", StringComparison.Ordinal) || fact.FactType.StartsWith("Razor", StringComparison.Ordinal);
    }

    private static string RootKind(PropertyFactRow fact)
    {
        return fact.FactType switch
        {
            "UiTemplateBinding" => "TemplateBinding",
            "UiFormControlBinding" => "UiControl",
            "UiEventBinding" => "EventBinding",
            "UiTemplateVariable" => "TemplateBinding",
            "RazorBinding" => "ViewModelProperty",
            "RazorFormTarget" => "EndpointRoute",
            "RazorModelBindingTarget" => "ViewModelProperty",
            "PropertyDeclared" => "ModelProperty",
            "SerializerContractMember" => "DtoProperty",
            "HttpCallDetected" => "HttpClientCall",
            _ => "ModelProperty"
        };
    }

    private static PropertyFlowRoot ToRoot(PropertyFactRow fact, PropertySelector selector, string? repositoryIdentityHash, int index)
    {
        var generic = selector.Kind is "field" or "control" or "binding" && GenericNames.Contains(selector.Value);
        var classification = generic || fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            ? PropertyFlowClassifications.NeedsReviewLineage
            : fact.EvidenceTier == EvidenceTiers.Tier1Semantic
                ? PropertyFlowClassifications.StrongStaticLineage
                : PropertyFlowClassifications.ProbableStaticLineage;
        var safeDisplay = SafeDisplay(fact);
        var limitations = RootLimitations(fact, generic);
        return new PropertyFlowRoot(
            $"root-{index + 1:D4}-{CombinedReportHelpers.Hash(fact.CombinedFactId, 12)}",
            RootKind(fact),
            classification,
            SafeSourceLabel(fact.SourceLabel),
            fact.SourceIndexId,
            repositoryIdentityHash,
            fact.ScanId,
            fact.CommitSha,
            fact.CombinedFactId,
            CombinedDependencyReporter.FirstValue(fact.Properties, "symbolId", "memberSymbolId", "targetSymbolId"),
            fact.RuleId,
            fact.EvidenceTier,
            CombinedReportHelpers.SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            ExtractorId(fact),
            ExtractorVersion(fact),
            safeDisplay,
            [fact.CombinedFactId],
            limitations);
    }

    private static IReadOnlyDictionary<string, string> SafeDisplay(PropertyFactRow fact)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in new[] { "uiFramework", "bindingKind", "controlKind", "controlName", "formControlName", "propertyPath", "propertyName", "memberName", "modelType", "dtoType", "contractFamily", "propertyFamily", "modelKind", "typeRole", "handlerName", "actionName", "controllerName", "httpMethod", "normalizedPathKey", "shapeHash" })
        {
            if (fact.Properties.TryGetValue(key, out var value) && IsSafeValue(value))
            {
                values[key] = value;
            }
        }

        var display = SafeDisplayName(fact);
        if (IsSafeValue(display))
        {
            values["displayName"] = display;
        }

        return values;
    }

    private static string SafeDisplayName(PropertyFactRow fact)
    {
        return CombinedDependencyReporter.FirstValue(fact.Properties, "propertyPath", "controlName", "formControlName", "propertyName", "memberName", "name", "normalizedPathKey")
            ?? fact.ContractElement
            ?? fact.TargetSymbol
            ?? fact.FactType;
    }

    private static bool IsSafeValue(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Contains("://", StringComparison.Ordinal)
            && !value.StartsWith("/", StringComparison.Ordinal)
            && !Regex.IsMatch(value, @"^[A-Za-z]:\\");
    }

    private static IReadOnlyList<string> RootLimitations(PropertyFactRow fact, bool generic)
    {
        var result = new List<string>();
        if (generic)
        {
            result.Add("Generic property names are review-tier without source/type/fact context.");
        }

        if (IsUiFact(fact))
        {
            result.Add("UI binding evidence does not prove runtime rendering, visibility, submitted values, or handler execution.");
        }

        if (fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual)
        {
            result.Add("Syntax-only evidence requires review before treating names as a precise lineage link.");
        }

        return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static (IReadOnlyList<PropertyFlowPath> Paths, bool Truncated) BuildPaths(
        IReadOnlyList<PropertyFlowRoot> roots,
        IReadOnlyList<PropertyFactRow> rootFacts,
        IReadOnlyList<PropertyFactRow> allFacts,
        CombinedPathGraphInventory graph,
        PropertyFlowOptions options,
        List<PropertyFlowGap> gaps)
    {
        var nodesById = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var edgesByFrom = graph.Edges
            .GroupBy(edge => edge.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(edge => edge.EdgeKind, StringComparer.Ordinal).ThenBy(edge => edge.ToNodeId, StringComparer.Ordinal).ThenBy(edge => edge.EdgeId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var paths = new List<PropertyFlowPath>();
        var frontierCount = 0;
        var truncated = false;

        for (var index = 0; index < roots.Count && paths.Count < options.MaxPaths; index++)
        {
            var root = roots[index];
            var fact = rootFacts[index];
            var startNodes = FindStartNodes(root, fact, graph.Nodes)
                .DistinctBy(node => node.NodeId)
                .ToArray();
            if (startNodes.Length == 0)
            {
                gaps.Add(new PropertyFlowGap(
                    $"gap:path:no-start:{root.RootId}",
                    "UnknownAnalysisGap",
                    PropertyFlowClassifications.UnknownAnalysisGap,
                    "Selected root did not attach to existing combined path graph nodes under current evidence.",
                    EdgeRuleId,
                    EvidenceTiers.Tier4Unknown,
                    root.SourceLabel,
                    root.CombinedFactId,
                    root.FilePath,
                    root.StartLine,
                    root.EndLine,
                    [root.CombinedFactId],
                    ["A future scanner or route/value-flow slice may provide a stronger root-to-graph edge."])
                {
                    SourceIndexId = root.SourceIndexId,
                    ScanId = root.ScanId,
                    CommitSha = root.CommitSha,
                    CommitShas = [root.CommitSha],
                    SupportingSourceIds = [root.SourceIndexId],
                    ExtractorId = root.ExtractorId,
                    ExtractorVersion = root.ExtractorVersion
                });
                continue;
            }

            foreach (var start in startNodes)
            {
                var queue = new Queue<(CombinedPathNode Node, List<CombinedPathNode> Nodes, List<CombinedPathEdge> Edges)>();
                queue.Enqueue((start, [start], []));
                var visited = new HashSet<string>(StringComparer.Ordinal) { start.NodeId };
                while (queue.Count > 0 && paths.Count < options.MaxPaths)
                {
                    frontierCount++;
                    if (frontierCount > options.MaxFrontier)
                    {
                        truncated = true;
                        break;
                    }

                    var current = queue.Dequeue();
                    if (current.Edges.Count > 0 && IsTerminalNode(current.Node))
                    {
                        paths.Add(ToPath(root, current.Nodes, current.Edges, paths.Count + 1));
                        continue;
                    }

                    if (current.Edges.Count >= options.MaxDepth)
                    {
                        truncated = true;
                        continue;
                    }

                    if (!edgesByFrom.TryGetValue(current.Node.NodeId, out var nextEdges))
                    {
                        if (current.Edges.Count > 0)
                        {
                            paths.Add(ToPath(root, current.Nodes, current.Edges, paths.Count + 1));
                        }
                        continue;
                    }

                    foreach (var edge in nextEdges)
                    {
                        if (!nodesById.TryGetValue(edge.ToNodeId, out var next) || !visited.Add(next.NodeId))
                        {
                            continue;
                        }

                        queue.Enqueue((next, [.. current.Nodes, next], [.. current.Edges, edge]));
                    }
                }
            }
        }

        if (paths.Count < options.MaxPaths)
        {
            var derived = BuildDerivedPaths(roots, rootFacts, allFacts, options, gaps, paths.Count + 1);
            paths.AddRange(derived.Paths.Take(options.MaxPaths - paths.Count));
            truncated |= derived.Truncated;
        }

        if (paths.Count >= options.MaxPaths)
        {
            truncated = true;
        }

        return (paths.OrderBy(path => path.PathId, StringComparer.Ordinal).ToArray(), truncated);
    }

    private static (IReadOnlyList<PropertyFlowPath> Paths, bool Truncated) BuildDerivedPaths(
        IReadOnlyList<PropertyFlowRoot> roots,
        IReadOnlyList<PropertyFactRow> rootFacts,
        IReadOnlyList<PropertyFactRow> allFacts,
        PropertyFlowOptions options,
        List<PropertyFlowGap> gaps,
        int firstOrdinal)
    {
        var paths = new List<PropertyFlowPath>();
        var ordinal = firstOrdinal;
        for (var index = 0; index < roots.Count && paths.Count < options.MaxPaths; index++)
        {
            var root = roots[index];
            var fact = rootFacts[index];
            var derived = fact.FactType.StartsWith("Razor", StringComparison.Ordinal)
                ? BuildRazorDerivedPaths(root, fact, allFacts, gaps)
                : BuildAngularDerivedPaths(root, fact, allFacts, gaps);
            foreach (var path in derived)
            {
                paths.Add(path with
                {
                    PathId = $"path-{ordinal:D4}-{CombinedReportHelpers.Hash(root.RootId + ":" + string.Join(">", path.Edges.Select(edge => edge.EdgeId)), 12)}"
                });
                ordinal++;
                if (paths.Count >= options.MaxPaths)
                {
                    return (paths, true);
                }
            }
        }

        return (paths.OrderBy(path => path.PathId, StringComparer.Ordinal).ToArray(), false);
    }

    private static IReadOnlyList<PropertyFlowPath> BuildRazorDerivedPaths(
        PropertyFlowRoot root,
        PropertyFactRow rootFact,
        IReadOnlyList<PropertyFactRow> allFacts,
        List<PropertyFlowGap> gaps)
    {
        var paths = new List<PropertyFlowPath>();
        var propertyName = PropertyName(rootFact);
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return paths;
        }

        if (rootFact.FactType == FactTypes.RazorBinding)
        {
            var modelType = CombinedDependencyReporter.FirstValue(rootFact.Properties, "modelType");
            var exactPropertyTargets = allFacts
                .Where(fact => fact.SourceIndexId == rootFact.SourceIndexId
                    && IsModelPropertyFact(fact)
                    && StringEqualsAny(propertyName, PropertyName(fact))
                    && !string.IsNullOrWhiteSpace(modelType)
                    && StringEqualsAny(modelType!, ModelType(fact)))
                .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
                .ThenBy(fact => fact.FilePath, StringComparer.Ordinal)
                .ThenBy(fact => fact.StartLine)
                .ThenBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
                .Take(3)
                .ToArray();
            foreach (var target in exactPropertyTargets)
            {
                paths.Add(DerivedPath(root, [rootFact, target], ["razor-binding-binds-property"], PropertyFlowClassifications.ProbableStaticLineage));
            }

            var formTargets = allFacts
                .Where(fact => fact.SourceIndexId == rootFact.SourceIndexId
                    && fact.FactType == FactTypes.RazorFormTarget
                    && fact.FilePath.Equals(rootFact.FilePath, StringComparison.Ordinal)
                    && fact.StartLine <= rootFact.StartLine)
                .OrderByDescending(fact => fact.StartLine)
                .ThenBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
                .Take(1)
                .ToArray();
            foreach (var formTarget in formTargets)
            {
                var modelTargets = allFacts
                    .Where(fact => fact.SourceIndexId == rootFact.SourceIndexId
                        && fact.FactType == FactTypes.RazorModelBindingTarget
                        && StringEqualsAny(propertyName, PropertyName(fact))
                        && FormMatchesModelBinding(formTarget, fact))
                    .OrderBy(fact => fact.FilePath, StringComparer.Ordinal)
                    .ThenBy(fact => fact.StartLine)
                    .ThenBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
                    .Take(3)
                    .ToArray();
                foreach (var modelTarget in modelTargets)
                {
                    paths.Add(DerivedPath(root, [rootFact, formTarget, modelTarget], ["razor-binding-in-form", "form-target-binds-model"], PropertyFlowClassifications.ProbableStaticLineage));
                }
            }

            if (paths.Count == 0)
            {
                AddPropertyIdentityGap(root, rootFact, gaps);
            }
        }
        else if (rootFact.FactType == FactTypes.RazorFormTarget)
        {
            var targets = allFacts
                .Where(fact => fact.SourceIndexId == rootFact.SourceIndexId
                    && fact.FactType == FactTypes.RazorModelBindingTarget
                    && FormMatchesModelBinding(rootFact, fact))
                .OrderBy(fact => PropertyName(fact), StringComparer.Ordinal)
                .ThenBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
                .Take(5)
                .ToArray();
            foreach (var target in targets)
            {
                paths.Add(DerivedPath(root, [rootFact, target], ["form-target-binds-model"], PropertyFlowClassifications.ProbableStaticLineage));
            }
            if (targets.Length == 0)
            {
                AddEndpointAlignmentGap(root, rootFact, gaps);
            }
        }

        var sameNameTargets = paths.Count == 0
            ? allFacts.Where(fact => fact.SourceIndexId == rootFact.SourceIndexId && IsModelPropertyFact(fact) && StringEqualsAny(propertyName, PropertyName(fact))).Take(2).ToArray()
            : [];
        foreach (var target in sameNameTargets)
        {
            AddSameNameGap(root, rootFact, target, gaps);
            paths.Add(DerivedPath(root, [rootFact, target], ["same-name-property-match"], PropertyFlowClassifications.NeedsReviewLineage));
        }

        return paths;
    }

    private static IReadOnlyList<PropertyFlowPath> BuildAngularDerivedPaths(
        PropertyFlowRoot root,
        PropertyFactRow rootFact,
        IReadOnlyList<PropertyFactRow> allFacts,
        List<PropertyFlowGap> gaps)
    {
        if (!rootFact.FactType.StartsWith("Ui", StringComparison.Ordinal))
        {
            return [];
        }

        var propertyName = PropertyName(rootFact);
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return [];
        }

        var handlerName = CombinedDependencyReporter.FirstValue(rootFact.Properties, "handlerName") ?? rootFact.TargetSymbol;
        var payloads = allFacts
            .Where(fact => fact.SourceIndexId == rootFact.SourceIndexId
                && fact.FactType == FactTypes.ObjectShapeInferred
                && FieldNames(fact).Contains(propertyName!, StringComparer.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(handlerName) || StringEqualsAny(handlerName!, fact.SourceSymbol, CombinedDependencyReporter.FirstValue(fact.Properties, "sourceMethod"))))
            .OrderBy(fact => fact.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.StartLine)
            .ThenBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        if (payloads.Length == 0)
        {
            return [];
        }

        var paths = new List<PropertyFlowPath>();
        foreach (var payload in payloads)
        {
            var httpCalls = allFacts
                .Where(fact => fact.SourceIndexId == rootFact.SourceIndexId
                    && fact.FactType == FactTypes.HttpCallDetected
                    && (string.IsNullOrWhiteSpace(handlerName)
                        || StringEqualsAny(handlerName!, fact.SourceSymbol, CombinedDependencyReporter.FirstValue(fact.Properties, "sourceMethod"))
                        || fact.FilePath.Equals(payload.FilePath, StringComparison.Ordinal)))
                .OrderBy(fact => fact.FilePath, StringComparer.Ordinal)
                .ThenBy(fact => fact.StartLine)
                .ThenBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
                .Take(2)
                .ToArray();

            foreach (var http in httpCalls)
            {
                var route = allFacts
                    .Where(fact => fact.FactType == FactTypes.HttpRouteBinding && EndpointMatches(http, fact))
                    .OrderBy(fact => fact.SourceLabel, StringComparer.Ordinal)
                    .ThenBy(fact => fact.FilePath, StringComparer.Ordinal)
                    .ThenBy(fact => fact.StartLine)
                    .ThenBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (route is null)
                {
                    AddEndpointAlignmentGap(root, http, gaps);
                    paths.Add(DerivedPath(root, [rootFact, payload, http], ["control-value-assigned", "payload-field-sent-by-http"], PropertyFlowClassifications.NeedsReviewLineage));
                    continue;
                }

                var modelTarget = allFacts
                    .Where(fact => fact.SourceIndexId == route.SourceIndexId
                        && fact.FactType == FactTypes.RazorModelBindingTarget
                        && StringEqualsAny(propertyName, PropertyName(fact))
                        && RouteMatchesModelBinding(route, fact))
                    .OrderBy(fact => fact.FilePath, StringComparer.Ordinal)
                    .ThenBy(fact => fact.StartLine)
                    .ThenBy(fact => fact.CombinedFactId, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (modelTarget is null)
                {
                    AddPropertyIdentityGap(root, route, gaps);
                    paths.Add(DerivedPath(root, [rootFact, payload, http, route], ["control-value-assigned", "payload-field-sent-by-http", "endpoint-aligned"], PropertyFlowClassifications.NeedsReviewLineage));
                    continue;
                }

                paths.Add(DerivedPath(root, [rootFact, payload, http, route, modelTarget], ["control-value-assigned", "payload-field-sent-by-http", "endpoint-aligned", "endpoint-binds-model"], PropertyFlowClassifications.NeedsReviewLineage));
            }
        }

        return paths;
    }

    private static PropertyFlowPath DerivedPath(PropertyFlowRoot root, IReadOnlyList<PropertyFactRow> facts, IReadOnlyList<string> edgeKinds, string classification)
    {
        var nodes = facts.Select((fact, index) => ToFactNode(fact, index == 0 ? RootKind(fact) : DerivedNodeKind(fact))).ToArray();
        var edges = new List<PropertyFlowEdge>();
        for (var index = 0; index < nodes.Length - 1; index++)
        {
            var fromFact = facts[index];
            var toFact = facts[index + 1];
            var edgeKind = edgeKinds[Math.Min(index, edgeKinds.Count - 1)];
            edges.Add(DerivedEdge(edgeKind, nodes[index], nodes[index + 1], classification, fromFact, toFact));
        }

        return new PropertyFlowPath(
            "path-derived-pending",
            classification,
            Confidence(classification),
            edges.Count,
            root.RootId,
            nodes.Last().NodeId,
            nodes,
            edges,
            facts.Select(fact => fact.CombinedFactId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            [],
            []);
    }

    private static PropertyFlowNode ToFactNode(PropertyFactRow fact, string nodeKind)
    {
        return new PropertyFlowNode(
            $"fact-node:{CombinedReportHelpers.Hash(fact.CombinedFactId + ":" + nodeKind, 16)}",
            nodeKind,
            SafeDisplay(SafeDisplayName(fact)),
            fact.SourceIndexId,
            SafeSourceLabel(fact.SourceLabel),
            null,
            fact.ScanId,
            fact.CommitSha,
            CombinedDependencyReporter.FirstValue(fact.Properties, "symbolId", "memberSymbolId", "targetSymbolId"),
            fact.CombinedFactId,
            fact.RuleId,
            fact.EvidenceTier,
            CombinedReportHelpers.SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            SortedMetadata(fact.Properties
                .Where(pair => pair.Key is "uiFramework" or "bindingKind" or "propertyName" or "propertyPath" or "modelType" or "modelKind" or "parameterSource" or "actionName" or "controllerName" or "handlerName" or "httpMethod" or "normalizedPathKey" or "shapeHash")
                .Select(pair => Pair(pair.Key, IsSafeValue(pair.Value) ? pair.Value : HashUnsafe(pair.Value)))));
    }

    private static PropertyFlowEdge DerivedEdge(string edgeKind, PropertyFlowNode from, PropertyFlowNode to, string classification, PropertyFactRow fromFact, PropertyFactRow toFact)
    {
        return new PropertyFlowEdge(
            $"derived-edge:{CombinedReportHelpers.Hash(edgeKind + ":" + from.NodeId + ":" + to.NodeId, 16)}",
            edgeKind,
            from.NodeId,
            to.NodeId,
            classification,
            EdgeRuleId,
            StrongestReviewTier(fromFact.EvidenceTier, toFact.EvidenceTier),
            [fromFact.CombinedFactId, toFact.CombinedFactId],
            [],
            [],
            CombinedReportHelpers.SafePath(fromFact.FilePath),
            fromFact.StartLine,
            fromFact.EndLine)
        {
            SourceIndexId = fromFact.SourceIndexId,
            SourceLabel = SafeSourceLabel(fromFact.SourceLabel),
            ScanId = fromFact.ScanId,
            CommitSha = fromFact.CommitSha,
            ExtractorId = "property-flow",
            ExtractorVersion = "property-flow/1.0"
        };
    }

    private static string StrongestReviewTier(string left, string right)
    {
        if (left == EvidenceTiers.Tier4Unknown || right == EvidenceTiers.Tier4Unknown)
        {
            return EvidenceTiers.Tier4Unknown;
        }

        if (left == EvidenceTiers.Tier3SyntaxOrTextual || right == EvidenceTiers.Tier3SyntaxOrTextual)
        {
            return EvidenceTiers.Tier3SyntaxOrTextual;
        }

        if (left == EvidenceTiers.Tier2Structural || right == EvidenceTiers.Tier2Structural)
        {
            return EvidenceTiers.Tier2Structural;
        }

        return EvidenceTiers.Tier1Semantic;
    }

    private static string DerivedNodeKind(PropertyFactRow fact)
    {
        return fact.FactType switch
        {
            FactTypes.ObjectShapeInferred => "PayloadField",
            FactTypes.HttpCallDetected => "HttpClientCall",
            FactTypes.HttpRouteBinding => "EndpointRoute",
            FactTypes.RazorModelBindingTarget => FamilyTokens(fact).Contains("dto") ? "DtoProperty" : "ModelBindingTarget",
            FactTypes.PropertyDeclared => "ModelProperty",
            FactTypes.SerializerContractMember => "DtoProperty",
            FactTypes.RazorFormTarget => "FormAction",
            _ => RootKind(fact)
        };
    }

    private static bool IsModelPropertyFact(PropertyFactRow fact)
    {
        return fact.FactType is FactTypes.RazorModelBindingTarget or FactTypes.PropertyDeclared or FactTypes.SerializerContractMember;
    }

    private static string? PropertyName(PropertyFactRow fact)
    {
        return CombinedDependencyReporter.FirstValue(fact.Properties, "propertyName", "memberName", "name")
            ?? fact.ContractElement
            ?? LastPathSegment(CombinedDependencyReporter.FirstValue(fact.Properties, "propertyPath"))
            ?? fact.TargetSymbol?.Split('.').LastOrDefault();
    }

    private static string? ModelType(PropertyFactRow fact)
    {
        return CombinedDependencyReporter.FirstValue(fact.Properties, "modelType", "typeName", "containingType", "declaringType", "dtoType")
            ?? ContainingType(fact.TargetSymbol);
    }

    private static string? ContainingType(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol) || !symbol.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        return symbol[..symbol.LastIndexOf('.')];
    }

    private static IReadOnlyList<string> FieldNames(PropertyFactRow fact)
    {
        return (CombinedDependencyReporter.FirstValue(fact.Properties, "fieldNames", "bodyFieldNames", "queryParameterNames") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool FormMatchesModelBinding(PropertyFactRow formTarget, PropertyFactRow binding)
    {
        var formMethod = CombinedDependencyReporter.FirstValue(formTarget.Properties, "httpMethod");
        var bindingMethod = CombinedDependencyReporter.FirstValue(binding.Properties, "httpMethod");
        if (!string.IsNullOrWhiteSpace(formMethod)
            && !string.IsNullOrWhiteSpace(bindingMethod)
            && !bindingMethod.Equals("ANY", StringComparison.OrdinalIgnoreCase)
            && !formMethod.Equals(bindingMethod, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var formAction = CombinedDependencyReporter.FirstValue(formTarget.Properties, "actionName");
        var bindingAction = CombinedDependencyReporter.FirstValue(binding.Properties, "actionName");
        var formController = CombinedDependencyReporter.FirstValue(formTarget.Properties, "controllerName");
        var bindingController = CombinedDependencyReporter.FirstValue(binding.Properties, "controllerName");
        var formHandler = CombinedDependencyReporter.FirstValue(formTarget.Properties, "handlerName");
        var bindingHandler = CombinedDependencyReporter.FirstValue(binding.Properties, "handlerName");
        var actionMatches = !string.IsNullOrWhiteSpace(formAction)
            && StringEqualsAny(formAction!, bindingAction)
            && (string.IsNullOrWhiteSpace(formController) || StringEqualsAny(formController!, bindingController));
        var handlerMatches = !string.IsNullOrWhiteSpace(formHandler)
            && RazorPageIdentitiesAlign(formTarget, binding)
            && HandlerNamesAlign(formHandler, bindingHandler);
        return actionMatches || handlerMatches;
    }

    private static bool EndpointMatches(PropertyFactRow httpCall, PropertyFactRow route)
    {
        var callKey = CombinedDependencyReporter.FirstValue(httpCall.Properties, "normalizedPathKey");
        var routeKey = CombinedDependencyReporter.FirstValue(route.Properties, "normalizedPathKey");
        if (string.IsNullOrWhiteSpace(callKey) || string.IsNullOrWhiteSpace(routeKey) || !callKey.Equals(routeKey, StringComparison.Ordinal))
        {
            return false;
        }

        var callMethod = CombinedDependencyReporter.FirstValue(httpCall.Properties, "httpMethod", "methodName") ?? httpCall.ContractElement;
        var routeMethods = (CombinedDependencyReporter.FirstValue(route.Properties, "httpMethods", "methodName") ?? route.ContractElement ?? string.Empty)
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return routeMethods.Length == 0
            || routeMethods.Contains("ANY", StringComparer.OrdinalIgnoreCase)
            || routeMethods.Contains(callMethod ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static bool RouteMatchesModelBinding(PropertyFactRow route, PropertyFactRow binding)
    {
        var routeAction = CombinedDependencyReporter.FirstValue(route.Properties, "actionName");
        var bindingAction = CombinedDependencyReporter.FirstValue(binding.Properties, "actionName");
        var routeController = CombinedDependencyReporter.FirstValue(route.Properties, "controllerName");
        var bindingController = CombinedDependencyReporter.FirstValue(binding.Properties, "controllerName");
        var routeHandler = CombinedDependencyReporter.FirstValue(route.Properties, "handlerName");
        var bindingHandler = CombinedDependencyReporter.FirstValue(binding.Properties, "handlerName");
        var actionMatches = !string.IsNullOrWhiteSpace(routeAction)
            && StringEqualsAny(routeAction!, bindingAction)
            && (string.IsNullOrWhiteSpace(routeController) || StringEqualsAny(routeController!, bindingController));
        var handlerMatches = !string.IsNullOrWhiteSpace(routeHandler)
            && RouteBindingMethodsAlign(route, binding)
            && HandlerNamesAlign(routeHandler, bindingHandler);
        return actionMatches || handlerMatches;
    }

    private static bool RouteBindingMethodsAlign(PropertyFactRow route, PropertyFactRow binding)
    {
        var bindingMethod = CombinedDependencyReporter.FirstValue(binding.Properties, "httpMethod");
        if (string.IsNullOrWhiteSpace(bindingMethod) || bindingMethod.Equals("ANY", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var routeMethods = (CombinedDependencyReporter.FirstValue(route.Properties, "httpMethods", "methodName") ?? route.ContractElement ?? string.Empty)
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return routeMethods.Length == 0
            || routeMethods.Contains("ANY", StringComparer.OrdinalIgnoreCase)
            || routeMethods.Contains(bindingMethod, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HandlerNamesAlign(string? expected, string? actual)
    {
        var expectedToken = HandlerComparisonToken(expected);
        var actualToken = HandlerComparisonToken(actual);
        return !string.IsNullOrWhiteSpace(expectedToken)
            && expectedToken.Equals(actualToken, StringComparison.OrdinalIgnoreCase);
    }

    private static string HandlerComparisonToken(string? handlerName)
    {
        var value = (handlerName ?? string.Empty).Trim();
        if (value.EndsWith("Async", StringComparison.Ordinal))
        {
            value = value[..^"Async".Length];
        }

        foreach (var prefix in new[] { "OnGet", "OnPost", "OnPut", "OnDelete", "OnPatch" })
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return value[prefix.Length..];
            }
        }

        return value;
    }

    private static bool RazorPageIdentitiesAlign(PropertyFactRow formTarget, PropertyFactRow binding)
    {
        var formPage = RazorPageIdentity(formTarget);
        var bindingPage = RazorPageIdentity(binding);
        return !string.IsNullOrWhiteSpace(formPage)
            && formPage.Equals(bindingPage, StringComparison.OrdinalIgnoreCase);
    }

    private static string RazorPageIdentity(PropertyFactRow fact)
    {
        var path = fact.FilePath.Replace('\\', '/').Trim();
        if (path.EndsWith(".cshtml.cs", StringComparison.OrdinalIgnoreCase))
        {
            return path[..^".cshtml.cs".Length];
        }

        if (path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
        {
            return path[..^".cshtml".Length];
        }

        return string.Empty;
    }

    private static HashSet<string> FamilyTokens(PropertyFactRow fact)
    {
        var raw = CombinedDependencyReporter.FirstValue(fact.Properties, "contractFamily", "propertyFamily", "modelKind", "typeRole");
        var tokens = (raw ?? string.Empty)
            .Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (tokens.Count == 0)
        {
            tokens.Add(fact.FactType == FactTypes.SerializerContractMember ? "dto" : "model");
        }

        return tokens;
    }

    private static void AddPropertyIdentityGap(PropertyFlowRoot root, PropertyFactRow fact, List<PropertyFlowGap> gaps)
    {
        gaps.Add(new PropertyFlowGap(
            $"gap:property-identity:{CombinedReportHelpers.Hash(root.RootId + ":" + fact.CombinedFactId, 12)}",
            "PropertyIdentityUnavailable",
            PropertyFlowClassifications.UnknownAnalysisGap,
            "No DTO/model property identity could be proven for this static hop.",
            EdgeRuleId,
            EvidenceTiers.Tier4Unknown,
            root.SourceLabel,
            fact.CombinedFactId,
            CombinedReportHelpers.SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            [root.CombinedFactId, fact.CombinedFactId],
            ["Property identity requires exact type, fact, symbol, endpoint/model-binding, or rule-backed value-origin evidence."])
        {
            SourceIndexId = root.SourceIndexId,
            ScanId = root.ScanId,
            CommitSha = root.CommitSha,
            CommitShas = [root.CommitSha],
            SupportingSourceIds = [root.SourceIndexId]
        });
    }

    private static void AddEndpointAlignmentGap(PropertyFlowRoot root, PropertyFactRow fact, List<PropertyFlowGap> gaps)
    {
        gaps.Add(new PropertyFlowGap(
            $"gap:endpoint-alignment:{CombinedReportHelpers.Hash(root.RootId + ":" + fact.CombinedFactId, 12)}",
            "EndpointAlignmentUnavailable",
            PropertyFlowClassifications.UnknownAnalysisGap,
            "Static form or HTTP evidence did not align to an action/handler/model-binding target.",
            EdgeRuleId,
            EvidenceTiers.Tier4Unknown,
            root.SourceLabel,
            fact.CombinedFactId,
            CombinedReportHelpers.SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            [root.CombinedFactId, fact.CombinedFactId],
            ["Endpoint alignment absence is a static evidence gap, not proof of no runtime route."])
        {
            SourceIndexId = root.SourceIndexId,
            ScanId = root.ScanId,
            CommitSha = root.CommitSha,
            CommitShas = [root.CommitSha],
            SupportingSourceIds = [root.SourceIndexId]
        });
    }

    private static void AddSameNameGap(PropertyFlowRoot root, PropertyFactRow rootFact, PropertyFactRow targetFact, List<PropertyFlowGap> gaps)
    {
        gaps.Add(new PropertyFlowGap(
            $"gap:same-name:{CombinedReportHelpers.Hash(rootFact.CombinedFactId + ":" + targetFact.CombinedFactId, 12)}",
            "SameNameOnlyPropertyMatch",
            PropertyFlowClassifications.NeedsReviewLineage,
            "A same-name property match exists, but stronger type, symbol, alias, or value-origin evidence is absent.",
            EdgeRuleId,
            EvidenceTiers.Tier3SyntaxOrTextual,
            root.SourceLabel,
            rootFact.CombinedFactId,
            CombinedReportHelpers.SafePath(rootFact.FilePath),
            rootFact.StartLine,
            rootFact.EndLine,
            [rootFact.CombinedFactId, targetFact.CombinedFactId],
            ["Same-name-only joins are capped at review-tier."])
        {
            SourceIndexId = root.SourceIndexId,
            ScanId = root.ScanId,
            CommitSha = root.CommitSha,
            CommitShas = [root.CommitSha],
            SupportingSourceIds = [root.SourceIndexId]
        });
    }

    private static IEnumerable<CombinedPathNode> FindStartNodes(PropertyFlowRoot root, PropertyFactRow fact, IReadOnlyList<CombinedPathNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.CombinedFactId == root.CombinedFactId)
            {
                yield return node;
            }
        }

        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in new[]
                 {
                     fact.SourceSymbol,
                     fact.TargetSymbol,
                     fact.ContractElement,
                     CombinedDependencyReporter.FirstValue(fact.Properties, "componentSymbolId", "memberSymbolId", "handlerName", "sourceMethod", "sourceClass", "modelType")
                 })
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                symbols.Add(value!);
            }
        }

        foreach (var node in nodes)
        {
            if (node.SourceIndexId == fact.SourceIndexId
                && (symbols.Contains(node.SymbolId ?? string.Empty) || symbols.Contains(node.DisplayName) || symbols.Any(symbol => BoundedDisplayMatch(node.DisplayName, symbol))))
            {
                yield return node;
            }
        }
    }

    private static bool BoundedDisplayMatch(string displayName, string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return false;
        }

        var index = displayName.IndexOf(symbol, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var before = index == 0 ? '\0' : displayName[index - 1];
            var afterIndex = index + symbol.Length;
            var after = afterIndex >= displayName.Length ? '\0' : displayName[afterIndex];
            if (!IsIdentifierChar(before) && !IsIdentifierChar(after))
            {
                return true;
            }

            index = displayName.IndexOf(symbol, index + symbol.Length, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsIdentifierChar(char value)
    {
        return value == '_' || char.IsLetterOrDigit(value);
    }

    private static bool IsTerminalNode(CombinedPathNode node)
    {
        return !string.IsNullOrWhiteSpace(node.SurfaceKind)
            || node.NodeKind is "EndpointRoute" or "EndpointClient"
            || node.NodeKind.Contains("Surface", StringComparison.OrdinalIgnoreCase)
            || node.NodeKind.Contains("sql", StringComparison.OrdinalIgnoreCase);
    }

    private static PropertyFlowPath ToPath(PropertyFlowRoot root, IReadOnlyList<CombinedPathNode> nodes, IReadOnlyList<CombinedPathEdge> edges, int ordinal)
    {
        var pathNodes = nodes.Select(ToNode).ToArray();
        var nodeMap = nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var pathEdges = edges.Select(edge => ToEdge(edge, nodeMap)).ToArray();
        var classification = ClassifyPath(root, pathNodes, pathEdges);
        var notes = PathNotes(pathEdges);
        return new PropertyFlowPath(
            $"path-{ordinal:D4}-{CombinedReportHelpers.Hash(root.RootId + ":" + string.Join(">", pathEdges.Select(edge => edge.EdgeId)), 12)}",
            classification,
            Confidence(classification),
            pathEdges.Length,
            root.RootId,
            pathNodes.Last().NodeId,
            pathNodes,
            pathEdges,
            new[] { root.CombinedFactId }.Concat(pathNodes.Select(node => node.CombinedFactId).OfType<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            pathEdges.SelectMany(edge => edge.SupportingEdgeIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            notes);
    }

    private static IReadOnlyList<string> PathNotes(IReadOnlyList<PropertyFlowEdge>? edges)
    {
        if (edges is null)
        {
            return [];
        }

        var notes = new List<string>();
        if (edges.Any(IsRouteFlowSpecificEdge))
        {
            notes.Add("StaticRouteFlowContext: route-flow hops are static endpoint-centered evidence and do not prove runtime execution, authorization, production traffic, dependency-injection target selection, branch feasibility, or persistence.");
        }

        return notes
            .OrderBy(note => note, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsRouteFlowSpecificEdge(PropertyFlowEdge? edge)
    {
        return edge is not null
            && ((edge.RuleId?.StartsWith("combined.route-flow.", StringComparison.Ordinal) ?? false)
                || (edge.EdgeId?.StartsWith("route-flow", StringComparison.Ordinal) ?? false)
                || (edge.EdgeKind?.Contains("route-flow", StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static bool HasRouteFlowSpecificContext(PropertyFlowPath? path)
    {
        return path is not null
            && ((path.Edges?.Any(IsRouteFlowSpecificEdge) ?? false)
                || (path.Notes?.Any(note => note?.StartsWith("StaticRouteFlowContext:", StringComparison.Ordinal) ?? false) ?? false));
    }

    private static void AddRouteFlowContextGaps(
        string routeFlowSignal,
        bool routeFlowMatchesSelectedEndpoint,
        IReadOnlyList<PropertyFlowRoot>? roots,
        IReadOnlyList<PropertyFlowPath>? paths,
        IReadOnlyList<string> sourceIds,
        IReadOnlyList<string> sourceCommitShas,
        List<PropertyFlowGap>? gaps)
    {
        if (routeFlowSignal != "available"
            || !routeFlowMatchesSelectedEndpoint
            || roots is null
            || roots.Count == 0
            || paths is null
            || paths.Any(HasRouteFlowSpecificContext)
            || gaps is null)
        {
            return;
        }

        gaps.Add(new PropertyFlowGap(
            "gap:route-flow:no-property-context",
            "RouteFlowNoPropertyContext",
            PropertyFlowClassifications.UnknownAnalysisGap,
            "Route-flow evidence exists in the combined index, but property-flow did not attach route-flow rows to the selected property trail because no rule-backed property-specific bridge was available.",
            EdgeRuleId,
            EvidenceTiers.Tier4Unknown,
            null,
            null,
            null,
            null,
            null,
            roots
                .Where(root => root is not null)
                .Select(root => root.CombinedFactId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            ["Route-flow evidence is additive context only when tied to the selected property through rule-backed value-origin, payload, model-binding, assignment, mapping, or equivalent property-specific evidence."])
        {
            SupportingSourceIds = sourceIds,
            CommitShas = sourceCommitShas
        });
    }

    private static async Task<bool> RouteFlowMatchesSelectedEndpointAsync(
        SqliteConnection connection,
        string routeFlowSignal,
        IReadOnlyList<PropertyFlowPath> paths,
        CancellationToken cancellationToken)
    {
        if (routeFlowSignal != "available")
        {
            return false;
        }

        var endpointScopes = EndpointScopes(paths);
        if (endpointScopes.Count == 0)
        {
            return false;
        }

        var columns = await RouteFlowColumnNamesAsync(connection, cancellationToken);
        var pathColumn = FirstColumn(columns, "normalizedPathKey", "normalized_path_key", "routeKey", "route_key", "pathKey", "path_key");
        if (pathColumn is null)
        {
            return false;
        }

        var methodColumn = FirstColumn(columns, "httpMethod", "http_method", "method");
        await using var command = connection.CreateCommand();
        command.CommandText = "select * from combined_route_flow_edges;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var pathOrdinal = reader.GetOrdinal(pathColumn);
        var methodOrdinal = methodColumn is null ? -1 : reader.GetOrdinal(methodColumn);
        while (await reader.ReadAsync(cancellationToken))
        {
            var pathKey = Convert.ToString(reader.GetValue(pathOrdinal));
            var method = methodOrdinal < 0 ? null : Convert.ToString(reader.GetValue(methodOrdinal));
            if (string.IsNullOrWhiteSpace(pathKey))
            {
                continue;
            }

            if (endpointScopes.Any(scope => EndpointScopeMatches(scope, method, pathKey)))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record EndpointScope(string? HttpMethod, string NormalizedPathKey);

    private static IReadOnlyList<EndpointScope> EndpointScopes(IReadOnlyList<PropertyFlowPath>? paths)
    {
        if (paths is null)
        {
            return [];
        }

        return paths
            .Where(path => path is not null)
            .SelectMany(path => path.Nodes ?? [])
            .Where(node => node?.SafeMetadata is not null)
            .Select(node =>
            {
                node.SafeMetadata.TryGetValue("normalizedPathKey", out var normalizedPathKey);
                node.SafeMetadata.TryGetValue("httpMethod", out var httpMethod);
                return string.IsNullOrWhiteSpace(normalizedPathKey)
                    ? null
                    : new EndpointScope(NormalizeEndpointMethod(httpMethod), normalizedPathKey);
            })
            .OfType<EndpointScope>()
            .Distinct()
            .OrderBy(scope => scope.NormalizedPathKey, StringComparer.Ordinal)
            .ThenBy(scope => scope.HttpMethod, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool EndpointScopeMatches(EndpointScope scope, string? method, string pathKey)
    {
        return string.Equals(scope.NormalizedPathKey, pathKey, StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(scope.HttpMethod)
                || string.IsNullOrWhiteSpace(method)
                || string.Equals(scope.HttpMethod, NormalizeEndpointMethod(method), StringComparison.Ordinal));
    }

    private static string? NormalizeEndpointMethod(string? method)
    {
        return string.IsNullOrWhiteSpace(method) ? null : method.Trim().ToUpperInvariant();
    }

    private static async Task<IReadOnlySet<string>> RouteFlowColumnNamesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "pragma table_info(\"combined_route_flow_edges\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static string? FirstColumn(IReadOnlySet<string> columns, params string[] names)
    {
        return names.FirstOrDefault(columns.Contains);
    }

    private static string ClassifyPath(PropertyFlowRoot root, IReadOnlyList<PropertyFlowNode> nodes, IReadOnlyList<PropertyFlowEdge> edges)
    {
        if (root.Classification == PropertyFlowClassifications.NeedsReviewLineage
            || nodes.Any(node => node.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual)
            || edges.Any(edge => edge.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual || edge.Classification.Contains("Review", StringComparison.OrdinalIgnoreCase)))
        {
            return PropertyFlowClassifications.NeedsReviewLineage;
        }

        if (nodes.All(node => node.EvidenceTier == EvidenceTiers.Tier1Semantic) && edges.All(edge => edge.EvidenceTier == EvidenceTiers.Tier1Semantic))
        {
            return PropertyFlowClassifications.StrongStaticLineage;
        }

        return PropertyFlowClassifications.ProbableStaticLineage;
    }

    private static string Confidence(string classification)
    {
        return classification switch
        {
            PropertyFlowClassifications.StrongStaticLineage => "High",
            PropertyFlowClassifications.ProbableStaticLineage => "Medium",
            _ => "Low"
        };
    }

    private static PropertyFlowNode ToNode(CombinedPathNode node)
    {
        return new PropertyFlowNode(
            node.NodeId,
            node.NodeKind,
            SafeDisplay(node.DisplayName),
            node.SourceIndexId,
            SafeSourceLabel(node.SourceLabel),
            null,
            node.ScanId,
            node.CommitSha,
            node.SymbolId,
            node.CombinedFactId,
            node.RuleId,
            node.EvidenceTier,
            node.FilePath is null ? null : CombinedReportHelpers.SafePath(node.FilePath),
            node.StartLine,
            node.EndLine,
            SortedMetadata([
                Pair("surfaceKind", node.SurfaceKind),
                Pair("surfaceName", node.SurfaceName),
                Pair("httpMethod", node.HttpMethod),
                Pair("normalizedPathKey", node.NormalizedPathKey),
                Pair("operationName", node.OperationName),
                Pair("tableName", HashUnsafe(node.TableName)),
                Pair("shapeHash", node.ShapeHash),
                Pair("textHash", node.TextHash),
                Pair("packageName", node.PackageName),
                Pair("configKey", HashUnsafe(node.ConfigKey))
            ]));
    }

    private static PropertyFlowEdge ToEdge(CombinedPathEdge edge, IReadOnlyDictionary<string, CombinedPathNode>? nodesById = null)
    {
        var attribution = AttributionNode(edge, nodesById);
        return new PropertyFlowEdge(
            edge.EdgeId,
            edge.EdgeKind,
            edge.FromNodeId,
            edge.ToNodeId,
            edge.Classification,
            edge.RuleId,
            edge.EvidenceTier,
            edge.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            [],
            edge.SupportingCombinedEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            edge.FilePath is null ? null : CombinedReportHelpers.SafePath(edge.FilePath),
            edge.StartLine,
            edge.EndLine)
        {
            SourceIndexId = attribution?.SourceIndexId,
            SourceLabel = attribution?.SourceLabel is null ? null : SafeSourceLabel(attribution.SourceLabel),
            ScanId = attribution?.ScanId,
            CommitSha = attribution?.CommitSha,
            ExtractorId = "combined-path-edge",
            ExtractorVersion = "combined-path-edge/1.0"
        };
    }

    private static CombinedPathNode? AttributionNode(CombinedPathEdge edge, IReadOnlyDictionary<string, CombinedPathNode>? nodesById)
    {
        if (nodesById is null)
        {
            return null;
        }

        if (nodesById.TryGetValue(edge.FromNodeId, out var from))
        {
            return from;
        }

        return nodesById.TryGetValue(edge.ToNodeId, out var to) ? to : null;
    }

    private static KeyValuePair<string, string?> Pair(string key, string? value) => new(key, value);

    private static IReadOnlyDictionary<string, string> SortedMetadata(IEnumerable<KeyValuePair<string, string?>> pairs)
    {
        return CombinedReportHelpers.SortedMetadata(pairs)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static string? HashUnsafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsSafeValue(value))
        {
            return value;
        }

        return $"value-hash:{CombinedReportHelpers.Hash(value, 16)}";
    }

    private static PropertyFlowSource ToSource(CombinedReportSource source)
    {
        var labels = new List<string>();
        if (!CombinedReportHelpers.SourceIdentityVerified(source))
        {
            labels.Add("IdentityUnverified");
        }

        if (source.AnalysisLevel.Contains("Reduced", StringComparison.OrdinalIgnoreCase)
            || source.BuildStatus.Contains("Failed", StringComparison.OrdinalIgnoreCase)
            || source.BuildStatus.Contains("Partial", StringComparison.OrdinalIgnoreCase)
            || source.CommitSha.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("ReducedCoverage");
        }

        return new PropertyFlowSource(
            source.SourceIndexId,
            SafeSourceLabel(source.Label),
            source.GitRootHash,
            source.ScanId,
            source.CommitSha,
            source.ScannerVersion,
            ExtractorVersions(source.ScannerVersion),
            source.AnalysisLevel,
            source.BuildStatus,
            labels.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyDictionary<string, string> ExtractorVersions(string scannerVersion)
    {
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["scanner"] = scannerVersion
        };
    }

    private static PropertyFlowCoverageWarning[] ToCoverageWarnings(IEnumerable<string> warnings, IReadOnlyList<PropertyFlowSource> sources)
    {
        var sourceIds = sources.Select(source => source.SourceIndexId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var sourceLabels = sources.Select(source => source.SourceLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var commitShas = sources.Select(source => source.CommitSha).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return warnings
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select((warning, index) => new PropertyFlowCoverageWarning(
                $"coverage-warning-{index + 1:D4}-{CombinedReportHelpers.Hash(warning, 12)}",
                warning,
                CoverageRuleId,
                EvidenceTiers.Tier4Unknown,
                sourceIds,
                sourceLabels,
                commitShas,
                "property-flow",
                "property-flow/1.0",
                ["Coverage warnings are report-level evidence; file-level spans are only present on supporting facts, roots, paths, edges, and gaps."]))
            .ToArray();
    }

    private static string? RepositoryIdentityHash(IReadOnlyList<PropertyFlowSource> sources)
    {
        var values = sources.Select(source => source.RepositoryIdentityHash).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return values.Length == 0 ? null : CombinedReportHelpers.Hash(string.Join("|", values), 16);
    }

    private static string? SourceIdentityHash(IEnumerable<CombinedReportSource> sources, string sourceIndexId)
    {
        return sources.FirstOrDefault(source => source.SourceIndexId == sourceIndexId)?.GitRootHash;
    }

    private static string ExtractorId(PropertyFactRow fact)
    {
        return fact.FactType.StartsWith("Ui", StringComparison.Ordinal) ? "typescript-angular-template" :
            fact.FactType.StartsWith("Razor", StringComparison.Ordinal) ? "csharp-razor-binding" :
            "combined-fact";
    }

    private static string ExtractorVersion(PropertyFactRow fact)
    {
        return fact.FactType.StartsWith("Ui", StringComparison.Ordinal) ? "typescript-angular-template/0.1.0" :
            fact.FactType.StartsWith("Razor", StringComparison.Ordinal) ? ScannerVersions.RazorBindingExtractor :
            "combined-fact/1.0";
    }

    private static string SafeSourceLabel(string value)
    {
        return IsSafeValue(value) ? value : $"source-label-hash:{CombinedReportHelpers.Hash(value, 16)}";
    }

    private static string SafeDisplay(string value)
    {
        return IsSafeValue(value) ? value : $"value-hash:{CombinedReportHelpers.Hash(value, 16)}";
    }

    private static string CoverageLabel(bool reduced, bool truncated, IReadOnlyList<PropertyFlowGap> gaps)
    {
        if (truncated)
        {
            return "Partial";
        }

        if (reduced || gaps.Any(gap => gap.Classification == PropertyFlowClassifications.UnknownAnalysisGap))
        {
            return "Reduced";
        }

        return "Full";
    }

    private static string SummaryClassification(IReadOnlyList<PropertyFlowRoot> roots, IReadOnlyList<PropertyFlowPath> paths, IReadOnlyList<PropertyFlowGap> gaps)
    {
        if (gaps.Any(gap => gap.GapKind == "TruncatedByLimit"))
        {
            return PropertyFlowClassifications.TruncatedByLimit;
        }

        if (roots.Count == 0)
        {
            return PropertyFlowClassifications.SelectorNoMatch;
        }

        if (paths.Count == 0)
        {
            return gaps.Any(gap => gap.Classification == PropertyFlowClassifications.UnknownAnalysisGap)
                ? PropertyFlowClassifications.UnknownAnalysisGap
                : PropertyFlowClassifications.NoLineageEvidence;
        }

        if (paths.Any(path => path.Classification == PropertyFlowClassifications.NeedsReviewLineage))
        {
            return PropertyFlowClassifications.NeedsReviewLineage;
        }

        if (paths.All(path => path.Classification == PropertyFlowClassifications.StrongStaticLineage))
        {
            return PropertyFlowClassifications.StrongStaticLineage;
        }

        return PropertyFlowClassifications.ProbableStaticLineage;
    }

    private static IReadOnlyDictionary<string, int> CountBy<T>(IEnumerable<T> values, Func<T, string> selector)
    {
        return values
            .GroupBy(selector, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> InventoryNodesByKind(this CombinedPathGraphInventory graph, int maxRows)
    {
        return graph.Nodes
            .Take(maxRows)
            .GroupBy(node => node.NodeKind, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> InventoryEdgesByKind(this CombinedPathGraphInventory graph, int maxRows)
    {
        return graph.Edges
            .Take(maxRows)
            .GroupBy(edge => edge.EdgeKind, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
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

    private static string RenderMarkdown(PropertyFlowReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Property Flow Report");
        builder.AppendLine();
        builder.AppendLine("This report summarizes deterministic static property-lineage evidence. It does not prove runtime rendering, execution, submission, authorization, deployment, or production use.");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Classification: `{Cell(report.Summary.Classification)}`");
        builder.AppendLine($"- Report coverage: `{Cell(report.ReportCoverage)}`");
        builder.AppendLine($"- Selected roots: `{report.Summary.SelectedRootCount}` of `{report.Summary.TotalCandidateCount}` candidates");
        builder.AppendLine($"- Paths: `{report.Summary.PathCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        builder.AppendLine($"- Truncated: `{report.Summary.Truncated}`");
        builder.AppendLine();
        builder.AppendLine("## Query");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Selector | `{Cell(report.Query.NormalizedSelector)}` |");
        builder.AppendLine($"| Source filter | `{Cell(report.Query.SourceFilter ?? "any")}` |");
        builder.AppendLine($"| Framework filter | `{Cell(report.Query.FrameworkFilter)}` |");
        builder.AppendLine($"| Bounds | roots `{report.Query.MaxRoots}`, depth `{report.Query.MaxDepth}`, paths `{report.Query.MaxPaths}`, frontier `{report.Query.MaxFrontier}` |");
        builder.AppendLine();
        builder.AppendLine("## Sources and Coverage");
        builder.AppendLine();
        builder.AppendLine("| Source | Commit | Analysis | Build | Coverage labels |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var source in report.Sources)
        {
            builder.AppendLine($"| `{Cell(source.SourceLabel)}` | `{Cell(source.CommitSha)}` | `{Cell(source.AnalysisLevel)}` | `{Cell(source.BuildStatus)}` | `{Cell(string.Join(", ", source.CoverageLabels))}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Coverage Warnings");
        builder.AppendLine();
        if (report.CoverageWarnings.Count == 0)
        {
            builder.AppendLine("No coverage warnings were emitted.");
        }
        else
        {
            builder.AppendLine("| Warning | Rule | Tier | Supporting sources | Commits | Message |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (var warning in report.CoverageWarnings)
            {
                builder.AppendLine($"| `{Cell(warning.WarningId)}` | `{Cell(warning.RuleId)}` | `{Cell(warning.EvidenceTier)}` | `{Cell(string.Join(", ", warning.SourceLabels))}` | `{Cell(string.Join(", ", warning.CommitShas))}` | {Cell(warning.Message)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Selected Roots");
        builder.AppendLine();
        if (report.SelectedRoots.Count == 0)
        {
            builder.AppendLine("No selected roots.");
        }
        else
        {
            builder.AppendLine("| Root | Kind | Classification | Source | Rule | Tier | File | Display |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var root in report.SelectedRoots)
            {
                builder.AppendLine($"| `{Cell(root.RootId)}` | `{Cell(root.RootKind)}` | `{Cell(root.Classification)}` | `{Cell(root.SourceLabel)}` | `{Cell(root.RuleId)}` | `{Cell(root.EvidenceTier)}` | `{Cell(root.FilePath)}:{root.StartLine}-{root.EndLine}` | `{Cell(Display(root.SafeDisplay))}` |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Lineage Paths");
        builder.AppendLine();
        if (report.LineagePaths.Count == 0)
        {
            builder.AppendLine("No lineage paths were found under available static evidence.");
        }
        else
        {
            foreach (var path in report.LineagePaths)
            {
                builder.AppendLine($"### {Cell(path.PathId)}");
                builder.AppendLine();
                builder.AppendLine($"Classification: `{Cell(path.Classification)}`; confidence: `{Cell(path.Confidence)}`.");
                if (path.Notes.Count > 0)
                {
                    builder.AppendLine();
                    foreach (var note in path.Notes)
                    {
                        builder.AppendLine($"- {Cell(note)}");
                    }
                }

                builder.AppendLine();
                builder.AppendLine("| # | Node | Edge from previous | Source | Rule | Tier | File |");
                builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
                for (var index = 0; index < path.Nodes.Count; index++)
                {
                    var node = path.Nodes[index];
                    var edge = index == 0 ? null : path.Edges[index - 1];
                    builder.AppendLine($"| {index + 1} | `{Cell(node.NodeKind)} {Cell(node.DisplayName)}` | `{Cell(edge?.EdgeKind ?? "start")}` | `{Cell(node.SourceLabel)}` | `{Cell(edge?.RuleId ?? node.RuleId ?? RootRuleId)}` | `{Cell(edge?.EvidenceTier ?? node.EvidenceTier ?? string.Empty)}` | `{Cell(node.FilePath ?? string.Empty)}:{node.StartLine?.ToString() ?? ""}-{node.EndLine?.ToString() ?? ""}` |");
                }

                builder.AppendLine();
            }
        }

        builder.AppendLine("## Gaps");
        builder.AppendLine();
        if (report.Gaps.Count == 0)
        {
            builder.AppendLine("No gaps were emitted.");
        }
        else
        {
            builder.AppendLine("| Gap | Classification | Rule | Tier | Source | Evidence | Message |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var gap in report.Gaps)
            {
                var evidence = gap.LineSpan is null
                    ? string.Join(", ", gap.SupportingSourceIds.Concat(gap.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
                    : $"{gap.FilePath}:{gap.LineSpan.StartLine}-{gap.LineSpan.EndLine}";
                builder.AppendLine($"| `{Cell(gap.GapKind)}` | `{Cell(gap.Classification)}` | `{Cell(gap.RuleId)}` | `{Cell(gap.EvidenceTier)}` | `{Cell(gap.SourceLabel ?? string.Empty)}` | `{Cell(evidence)}` | {Cell(gap.Message)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Evidence Inventory");
        builder.AppendLine();
        builder.AppendLine("| Kind | Count |");
        builder.AppendLine("| --- | --- |");
        foreach (var pair in report.Inventory.RootsByKind.Concat(report.Inventory.NodesByKind).Concat(report.Inventory.EdgesByKind).OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"| `{Cell(pair.Key)}` | `{pair.Value}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Optional Observed Evidence");
        builder.AppendLine();
        if (report.ObservedEvidence.Count == 0)
        {
            builder.AppendLine("No browser/computer-use observed evidence was supplied. Static report completeness is independent of observed demo metadata.");
        }
        else
        {
            builder.AppendLine("Observed evidence is demo/validation metadata only and does not upgrade static classifications.");
            builder.AppendLine();
            builder.AppendLine("| Evidence | Label | Classification | Rule | Tier | Metadata |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (var evidence in report.ObservedEvidence)
            {
                builder.AppendLine($"| `{Cell(evidence.EvidenceId)}` | `{Cell(evidence.Label)}` | `{Cell(evidence.Classification)}` | `{Cell(evidence.RuleId)}` | `{Cell(evidence.EvidenceTier)}` | `{Cell(Display(evidence.SafeMetadata))}` |");
            }
        }
        builder.AppendLine();
        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations)
        {
            builder.AppendLine($"- {Cell(limitation)}");
        }

        return builder.ToString();
    }

    private static string Display(IReadOnlyDictionary<string, string> metadata)
    {
        return string.Join("; ", metadata.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string Cell(string? value) => CombinedReportHelpers.Cell(value);
}
