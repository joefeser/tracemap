using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record CombinedReverseOptions(
    string IndexPath,
    string OutputPath,
    string Format = "markdown",
    string? Source = null,
    string? Surface = null,
    string? SurfaceName = null,
    string To = "endpoints",
    int MaxDepth = 8,
    int MaxFrontier = 10000,
    int MaxSurfaces = 200,
    int MaxRoots = 100,
    int MaxPathsPerRoot = 5,
    int MaxGaps = 1000,
    bool ExitCode = false,
    string? MessageDirection = null);

public sealed record CombinedReverseResult(
    CombinedReverseReport Report,
    string? MarkdownPath,
    string? JsonPath)
{
    public bool HasReverseEvidence => Report.ReverseRoots.Count > 0 || Report.Paths.Count > 0;
}

public sealed record CombinedReverseReport(
    string ReportType,
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    CombinedReverseQuery Query,
    CombinedReverseSnapshotInfo Snapshot,
    CombinedReverseSummary Summary,
    IReadOnlyList<CombinedReverseSurface> SelectedSurfaces,
    IReadOnlyList<CombinedReverseRoot> ReverseRoots,
    IReadOnlyList<CombinedReversePath> Paths,
    IReadOnlyList<CombinedReverseGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record CombinedReverseQuery(
    string IndexPath,
    string OutputPath,
    string Format,
    string? Source,
    string? SurfaceKind,
    string? SurfaceName,
    string SurfaceNameMatchMode,
    string To,
    int MaxDepth,
    int MaxFrontier,
    int MaxSurfaces,
    int MaxRoots,
    int MaxPathsPerRoot,
    int MaxGaps,
    bool ExitCode,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? MessageDirection);

public sealed record CombinedReverseSnapshotInfo(
    string IndexKind,
    int SourceCount,
    IReadOnlyList<CombinedReverseSourceInfo> Sources);

public sealed record CombinedReverseSourceInfo(
    string SourceLabel,
    string? SourceIndexId,
    string? ScanId,
    string? CommitSha,
    string Language,
    string AnalysisLevel,
    string BuildStatus,
    string RepositoryIdentityHash,
    bool IdentityVerified,
    IReadOnlyList<string> CoverageWarnings);

public sealed record CombinedReverseSummary(
    int SourceCount,
    int SelectedSurfaceCount,
    int ReverseRootCount,
    int PathCount,
    int GapCount,
    bool Truncated,
    string ReportCoverage,
    IReadOnlyDictionary<string, int> RootCountsByKind);

public sealed record CombinedReverseSurface(
    string SurfaceId,
    string SurfaceKind,
    string StableKey,
    string SourceLabel,
    string DisplayName,
    string Classification,
    string Confidence,
    string RuleId,
    string EvidenceTier,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> CoverageCaveats);

public sealed record CombinedReverseRoot(
    string RootId,
    string RootKind,
    string StableKey,
    string SourceLabel,
    string DisplayName,
    string Classification,
    string Confidence,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> PathIds,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> CoverageCaveats);

public sealed record CombinedReversePath(
    string PathId,
    string RootId,
    string SurfaceId,
    string Classification,
    string Confidence,
    int Depth,
    IReadOnlyList<CombinedPathNode> Nodes,
    IReadOnlyList<CombinedPathEdge> Edges,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<CombinedPathNote> Notes);

public sealed record CombinedReverseGap(
    string GapId,
    string GapKind,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string Message,
    string? SourceIndexId,
    string? SourceLabel,
    string? SurfaceId,
    string? RootId,
    string? PathId,
    string? NodeId,
    string? CombinedFactId,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? Reason,
    IReadOnlyDictionary<string, string> Metadata);

public static class CombinedReverseClassifications
{
    public const string SelectedSurfaceEvidence = nameof(SelectedSurfaceEvidence);
    public const string NeedsReviewSurfaceEvidence = nameof(NeedsReviewSurfaceEvidence);
    public const string StrongStaticReversePath = nameof(StrongStaticReversePath);
    public const string ProbableStaticReversePath = nameof(ProbableStaticReversePath);
    public const string NeedsReviewReversePath = nameof(NeedsReviewReversePath);
    public const string NoReversePathEvidence = nameof(NoReversePathEvidence);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
    public const string TruncatedByLimit = nameof(TruncatedByLimit);
}

public static class CombinedReverseReporter
{
    private const string ReportType = "combined-reverse-query";
    private const string Version = "1.0";
    private const string SurfaceRuleId = "combined.reverse.surface.v1";
    private const string RootRuleId = "combined.reverse.root.v1";
    private const string PathRuleId = "combined.reverse.path.v1";
    private const string SelectorRuleId = "combined.reverse.selector.v1";
    private const string TruncationRuleId = "combined.reverse.truncation.v1";
    private const string IdentityRuleId = "combined.reverse.identity.v1";
    private const int MarkdownRowLimit = 200;

    private static readonly HashSet<string> SurfaceKinds = new(StringComparer.Ordinal)
    {
        "sql-query",
        "sql-persistence",
        "http-route",
        "http-client",
        "package-config",
        "legacy-data",
        "message-queue",
        "message-topic",
        "message-subscription",
        "message-exchange",
        "message-stream",
        "message-event",
        "message-channel",
        "message-unknown"
    };

    private static readonly HashSet<string> TargetKinds = new(StringComparer.Ordinal)
    {
        "endpoints",
        "symbols",
        "sources",
        "all"
    };

    public static async Task<CombinedReverseResult> WriteAsync(CombinedReverseOptions options, CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(options, cancellationToken);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "reverse");
        var (markdownPath, jsonPath) = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "reverse-report.md",
            "reverse-report.json",
            report,
            RenderMarkdown,
            CombinedDependencyReporter.JsonOptions,
            cancellationToken);
        return new CombinedReverseResult(report, markdownPath, jsonPath);
    }

    public static async Task<CombinedReverseReport> BuildReportAsync(CombinedReverseOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "reverse");
        var target = NormalizeTarget(options.To);
        var sourceFilter = string.IsNullOrWhiteSpace(options.Source) ? null : options.Source.Trim();
        var surfaceKind = string.IsNullOrWhiteSpace(options.Surface) ? null : options.Surface.Trim();
        var surfaceName = string.IsNullOrWhiteSpace(options.SurfaceName) ? null : options.SurfaceName.Trim();
        var messageDirection = CombinedReportHelpers.NormalizeMessageDirection(options.MessageDirection, "reverse");
        var graph = await CombinedDependencyPathReporter.BuildGraphInventoryAsync(options.IndexPath, cancellationToken: cancellationToken);
        var sourcesById = graph.Sources.ToDictionary(source => source.SourceIndexId, StringComparer.Ordinal);
        var gaps = graph.Gaps.Select(FromPathGap).ToList();
        foreach (var source in graph.Sources.Where(SourceIdentityUnverified))
        {
            gaps.Add(new CombinedReverseGap(
                $"gap:identity:source:{source.SourceIndexId}",
                "IdentityUnverified",
                CombinedReverseClassifications.UnknownAnalysisGap,
                IdentityRuleId,
                "Tier4Unknown",
                $"Source `{source.Label}` has missing or unverified identity metadata.",
                source.SourceIndexId,
                source.Label,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "source-identity",
                EmptyMetadata()));
        }

        var selectedSurfaceNodes = SelectSurfaceNodes(graph.Nodes, sourceFilter, surfaceKind, surfaceName, messageDirection);
        var selectedSurfaceTotal = selectedSurfaceNodes.Length;
        var truncated = false;
        if (selectedSurfaceNodes.Length > options.MaxSurfaces)
        {
            truncated = true;
            gaps.Add(new CombinedReverseGap(
                $"gap:truncated:surfaces:{selectedSurfaceNodes.Length}",
                "TruncatedByLimit",
                CombinedReverseClassifications.TruncatedByLimit,
                TruncationRuleId,
                "Tier4Unknown",
                $"Surface selector matched {selectedSurfaceNodes.Length} surfaces; reverse traversal used the first {options.MaxSurfaces} deterministic candidates.",
                null,
                sourceFilter,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "surfaces",
                EmptyMetadata()));
            selectedSurfaceNodes = selectedSurfaceNodes.Take(options.MaxSurfaces).ToArray();
        }

        var duplicateSurfaceKeys = selectedSurfaceNodes
            .GroupBy(SurfaceStableKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var selectedSurfaces = selectedSurfaceNodes.Select(node => ToSurface(node, sourcesById, duplicateSurfaceKeys.Contains(SurfaceStableKey(node)))).ToArray();
        foreach (var duplicate in selectedSurfaces.GroupBy(surface => surface.StableKey, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            gaps.Add(new CombinedReverseGap(
                $"gap:identity:surface:{CombinedReportHelpers.Hash(duplicate.Key, 24)}",
                "DuplicateIdentity",
                CombinedReverseClassifications.UnknownAnalysisGap,
                IdentityRuleId,
                "Tier4Unknown",
                $"Multiple selected surfaces share stable identity hash `{CombinedReportHelpers.Hash(duplicate.Key, 24)}`; affected evidence is review-tier.",
                null,
                null,
                duplicate.First().SurfaceId,
                null,
                null,
                null,
                duplicate.First().SupportingFactIds.FirstOrDefault(),
                duplicate.First().FilePath,
                duplicate.First().StartLine,
                duplicate.First().EndLine,
                "duplicate-surface",
                EmptyMetadata()));
        }

        var paths = new List<CombinedReversePath>();
        var rootCandidates = new List<RootCandidate>();
        var contributingSourceIds = new HashSet<string>(selectedSurfaceNodes.Select(node => node.SourceIndexId), StringComparer.Ordinal);
        if (selectedSurfaceNodes.Length == 0)
        {
            gaps.Add(new CombinedReverseGap(
                "gap:selector:no-surface",
                "SelectorNoMatch",
                CombinedReverseClassifications.SelectorNoMatch,
                SelectorRuleId,
                "Tier4Unknown",
                "No dependency surfaces matched the reverse query selectors.",
                null,
                sourceFilter,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "selector",
                EmptyMetadata()));
        }
        else
        {
            var traversalTarget = target == "sources" ? "all" : target;
            var traversal = Traverse(graph, selectedSurfaceNodes, selectedSurfaces, traversalTarget, options, sourceFilter, sourcesById);
            if (target != "sources")
            {
                paths.AddRange(traversal.Paths);
                rootCandidates.AddRange(traversal.Roots);
            }

            if (target is "sources" or "all")
            {
                var sourceRoots = BuildSourceRootPaths(traversal.Paths, graph.Sources, options.MaxPathsPerRoot);
                paths.AddRange(sourceRoots.Paths);
                rootCandidates.AddRange(sourceRoots.Roots);
                gaps.AddRange(sourceRoots.Gaps);
                truncated = truncated || sourceRoots.Truncated;
            }

            truncated = truncated || traversal.Truncated;
            foreach (var sourceId in traversal.ContributingSourceIds)
            {
                contributingSourceIds.Add(sourceId);
            }

            gaps.AddRange(traversal.Gaps);
        }

        var sortedPaths = SortPaths(paths).ToArray();
        var duplicatePathNodeKeys = sortedPaths
            .SelectMany(path => path.Nodes)
            .GroupBy(NodeStableKey, StringComparer.Ordinal)
            .Where(group => group.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Skip(1).Any())
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        sortedPaths = DowngradePathsWithDuplicateNodes(sortedPaths, duplicatePathNodeKeys).ToArray();

        var roots = BuildRoots(rootCandidates, sortedPaths, sourcesById).ToList();
        var duplicateRootKeys = roots
            .GroupBy(root => root.StableKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        roots = roots
            .Select(root => duplicateRootKeys.Contains(root.StableKey)
                ? root with
                {
                    Classification = CombinedReverseClassifications.NeedsReviewReversePath,
                    Confidence = Confidence(CombinedReverseClassifications.NeedsReviewReversePath)
                }
                : root)
            .ToList();
        foreach (var duplicate in roots.GroupBy(root => root.StableKey, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            gaps.Add(new CombinedReverseGap(
                $"gap:identity:root:{CombinedReportHelpers.Hash(duplicate.Key, 24)}",
                "DuplicateIdentity",
                CombinedReverseClassifications.UnknownAnalysisGap,
                IdentityRuleId,
                "Tier4Unknown",
                $"Multiple reverse roots share stable identity hash `{CombinedReportHelpers.Hash(duplicate.Key, 24)}`; affected evidence is review-tier.",
                null,
                duplicate.First().SourceLabel,
                null,
                duplicate.First().RootId,
                null,
                null,
                duplicate.First().SupportingFactIds.FirstOrDefault(),
                null,
                null,
                null,
                "duplicate-root",
                EmptyMetadata()));
        }

        if (selectedSurfaces.Length > 0 && roots.Count == 0 && sortedPaths.Length == 0)
        {
            var reducedSource = graph.Sources.FirstOrDefault(source => contributingSourceIds.Contains(source.SourceIndexId) && SourceHasReducedCoverage(source));
            if (reducedSource is not null)
            {
                gaps.Add(new CombinedReverseGap(
                    $"gap:no-reverse-path:coverage:{reducedSource.SourceIndexId}",
                    "UnknownAnalysisGap",
                    CombinedReverseClassifications.UnknownAnalysisGap,
                    RootRuleId,
                    "Tier4Unknown",
                    $"No reverse path was found, but `{reducedSource.Label}` has reduced coverage; absence of evidence is coverage-relative.",
                    reducedSource.SourceIndexId,
                    reducedSource.Label,
                    selectedSurfaces[0].SurfaceId,
                    null,
                    null,
                    null,
                    selectedSurfaces[0].SupportingFactIds.FirstOrDefault(),
                    selectedSurfaces[0].FilePath,
                    selectedSurfaces[0].StartLine,
                    selectedSurfaces[0].EndLine,
                    "coverage",
                    EmptyMetadata()));
            }
            else
            {
                gaps.Add(new CombinedReverseGap(
                    $"gap:no-reverse-path:{selectedSurfaces[0].SurfaceId}",
                    "NoReversePathEvidence",
                    CombinedReverseClassifications.NoReversePathEvidence,
                    RootRuleId,
                    "Tier4Unknown",
                    "Selected surfaces had no reverse path to the requested root target within the current graph and bounds.",
                    null,
                    selectedSurfaces[0].SourceLabel,
                    selectedSurfaces[0].SurfaceId,
                    null,
                    null,
                    null,
                    selectedSurfaces[0].SupportingFactIds.FirstOrDefault(),
                    selectedSurfaces[0].FilePath,
                    selectedSurfaces[0].StartLine,
                    selectedSurfaces[0].EndLine,
                    "no-reverse-path",
                    EmptyMetadata()));
            }
        }

        var pathDepths = sortedPaths.ToDictionary(path => path.PathId, path => path.Depth, StringComparer.Ordinal);
        var sortedRoots = roots
            .OrderBy(root => ClassificationRank(root.Classification))
            .ThenBy(root => root.RootKind, StringComparer.Ordinal)
            .ThenBy(root => root.SourceLabel, StringComparer.Ordinal)
            .ThenBy(root => root.DisplayName, StringComparer.Ordinal)
            .ThenBy(root => root.PathIds.Count == 0 ? int.MaxValue : root.PathIds.Select(pathId => pathDepths.GetValueOrDefault(pathId, int.MaxValue)).DefaultIfEmpty(int.MaxValue).Min())
            .ThenByDescending(root => root.PathIds.Count)
            .ThenBy(root => root.RootId, StringComparer.Ordinal)
            .Take(options.MaxRoots)
            .ToArray();
        if (roots.Count > sortedRoots.Length)
        {
            truncated = true;
            gaps.Add(new CombinedReverseGap(
                $"gap:truncated:roots:{roots.Count}",
                "TruncatedByLimit",
                CombinedReverseClassifications.TruncatedByLimit,
                TruncationRuleId,
                "Tier4Unknown",
                $"Reverse query found {roots.Count} roots; output includes the first {sortedRoots.Length} deterministic roots.",
                null,
                sourceFilter,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "roots",
                EmptyMetadata()));
        }

        var keptPathIds = sortedRoots.SelectMany(root => root.PathIds).ToHashSet(StringComparer.Ordinal);
        sortedPaths = sortedPaths.Where(path => keptPathIds.Contains(path.PathId)).ToArray();
        foreach (var duplicate in sortedPaths
            .SelectMany(path => path.Nodes)
            .GroupBy(NodeStableKey, StringComparer.Ordinal)
            .Where(group => duplicatePathNodeKeys.Contains(group.Key))
            .Where(group => group.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Skip(1).Any()))
        {
            var node = duplicate.OrderBy(item => item.SourceLabel, StringComparer.Ordinal).ThenBy(item => item.DisplayName, StringComparer.Ordinal).First();
            gaps.Add(new CombinedReverseGap(
                $"gap:identity:node:{CombinedReportHelpers.Hash(duplicate.Key, 24)}",
                "DuplicateIdentity",
                CombinedReverseClassifications.UnknownAnalysisGap,
                IdentityRuleId,
                "Tier4Unknown",
                $"Multiple reverse path nodes share stable identity hash `{CombinedReportHelpers.Hash(duplicate.Key, 24)}`; affected evidence is review-tier.",
                node.SourceIndexId,
                node.SourceLabel,
                null,
                null,
                null,
                node.NodeId,
                node.CombinedFactId,
                node.FilePath,
                node.StartLine,
                node.EndLine,
                "duplicate-node",
                EmptyMetadata()));
        }

        var sortedGaps = gaps
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
            .ThenBy(gap => gap.FilePath, StringComparer.Ordinal)
            .ThenBy(gap => gap.StartLine ?? 0)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        if (sortedGaps.Length > options.MaxGaps)
        {
            truncated = true;
            var truncationGap = new CombinedReverseGap(
                $"gap:truncated:gaps:{gaps.Count}",
                "TruncatedByLimit",
                CombinedReverseClassifications.TruncatedByLimit,
                TruncationRuleId,
                "Tier4Unknown",
                $"Reverse query produced {gaps.Count} gaps; output is capped at {options.MaxGaps}.",
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
                "gaps",
                EmptyMetadata());
            sortedGaps = sortedGaps.Take(Math.Max(0, options.MaxGaps - 1)).Append(truncationGap).ToArray();
        }

        var warnings = graph.CoverageWarnings.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var reportCoverage = truncated || warnings.Length > 0 || sortedGaps.Any(gap => gap.Classification == CombinedReverseClassifications.UnknownAnalysisGap)
            ? "ReducedCoverage"
            : "FullEvidenceAvailable";
        return new CombinedReverseReport(
            ReportType,
            Version,
            reportCoverage,
            warnings,
            new CombinedReverseQuery(
                CombinedReportHelpers.SafePath(options.IndexPath),
                "output",
                format,
                sourceFilter,
                surfaceKind,
                surfaceName is null ? null : SafeDisplayName(surfaceName),
                "CaseInsensitiveExact",
                target,
                options.MaxDepth,
                options.MaxFrontier,
                options.MaxSurfaces,
                options.MaxRoots,
                options.MaxPathsPerRoot,
                options.MaxGaps,
                options.ExitCode,
                messageDirection),
            new CombinedReverseSnapshotInfo(
                "combined",
                graph.Sources.Count,
                graph.Sources.Select(source => ToSourceInfo(source, warnings)).OrderBy(source => source.SourceLabel, StringComparer.Ordinal).ThenBy(source => source.SourceIndexId, StringComparer.Ordinal).ToArray()),
            new CombinedReverseSummary(
                graph.Sources.Count,
                selectedSurfaces.Length,
                sortedRoots.Length,
                sortedPaths.Length,
                sortedGaps.Length,
                truncated,
                reportCoverage,
                sortedRoots
                    .GroupBy(root => root.RootKind, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)),
            selectedSurfaces,
            sortedRoots,
            sortedPaths,
            sortedGaps,
            CombinedDependencyReporter.Limitations.Concat([
                "Reverse query evidence is static graph evidence, not runtime usage proof.",
                "No reverse path under reduced coverage is an analysis gap, not proof of no callers."
            ]).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static void ValidateOptions(CombinedReverseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("reverse requires --index <combined.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("reverse requires --out <path>.");
        }

        if (!string.IsNullOrWhiteSpace(options.Surface) && !SurfaceKinds.Contains(options.Surface.Trim()))
        {
            if (string.Equals(options.Surface.Trim(), "message-publish-consume", StringComparison.Ordinal))
            {
                throw new ArgumentException("reverse --surface 'message-publish-consume' is an edge kind, not a dependency surface kind.");
            }

            throw new ArgumentException("reverse --surface must be one of sql-query, sql-persistence, http-route, http-client, package-config, legacy-data, message-queue, message-topic, message-subscription, message-exchange, message-stream, message-event, message-channel, or message-unknown.");
        }

        if (!TargetKinds.Contains(NormalizeTarget(options.To)))
        {
            throw new ArgumentException("reverse --to must be one of endpoints, symbols, sources, or all.");
        }

        if (options.MaxDepth <= 0 || options.MaxFrontier <= 0 || options.MaxSurfaces <= 0 || options.MaxRoots <= 0 || options.MaxPathsPerRoot <= 0 || options.MaxGaps <= 0)
        {
            throw new ArgumentException("reverse numeric caps must be positive integers.");
        }

        CombinedReportHelpers.NormalizeMessageDirection(options.MessageDirection, "reverse");
    }

    private static CombinedPathNode[] SelectSurfaceNodes(IReadOnlyList<CombinedPathNode> nodes, string? sourceFilter, string? surfaceKind, string? surfaceName, string? messageDirection)
    {
        return nodes
            .Where(node => node.SurfaceKind is not null)
            .Where(node => surfaceKind is null || string.Equals(node.SurfaceKind, surfaceKind, StringComparison.Ordinal))
            .Where(node => sourceFilter is null || string.Equals(node.SourceLabel, sourceFilter, StringComparison.OrdinalIgnoreCase))
            .Where(node => surfaceName is null || SurfaceNameMatches(node, surfaceName))
            .Where(node => messageDirection is null || !IsMessageSurfaceKind(node.SurfaceKind) || string.Equals(node.OperationDirection, messageDirection, StringComparison.Ordinal))
            .OrderBy(node => SurfaceClassificationRank(ClassifySurface(node)))
            .ThenBy(node => node.SourceLabel, StringComparer.Ordinal)
            .ThenBy(node => node.SurfaceKind, StringComparer.Ordinal)
            .ThenBy(node => node.DisplayName, StringComparer.Ordinal)
            .ThenBy(node => node.FilePath, StringComparer.Ordinal)
            .ThenBy(node => node.StartLine ?? 0)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool SurfaceNameMatches(CombinedPathNode node, string selector)
    {
        return string.Equals(node.DisplayName, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.SurfaceName, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.PackageName, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.ConfigKey, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.TableName, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.ShapeHash, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.TextHash, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.NormalizedPathKey, selector, StringComparison.OrdinalIgnoreCase);
    }

    private static CombinedReverseSurface ToSurface(CombinedPathNode node, IReadOnlyDictionary<string, CombinedReportSource> sourcesById, bool hasDuplicateIdentity)
    {
        var classification = ClassifySurface(node, hasDuplicateIdentity);
        var source = sourcesById.TryGetValue(node.SourceIndexId, out var found) ? found : null;
        var caveats = SurfaceCaveats(node, source);
        return new CombinedReverseSurface(
            SurfaceId(node),
            node.SurfaceKind ?? "unknown",
            SurfaceStableKey(node),
            node.SourceLabel,
            SafeDisplayName(node.DisplayName),
            classification,
            Confidence(classification),
            SurfaceRuleId,
            node.EvidenceTier ?? "Tier4Unknown",
            node.FilePath,
            node.StartLine,
            node.EndLine,
            SortedMetadata([
                new("surfaceKind", node.SurfaceKind),
                new("httpMethod", node.HttpMethod),
                new("normalizedPathKey", node.NormalizedPathKey),
                new("packageName", node.PackageName),
                new("configKey", node.ConfigKey),
                new("operationName", node.OperationName),
                new("tableName", node.TableName),
                new("columnNames", node.ColumnNames),
                new("sqlSourceKind", node.SourceKind),
                new("operationDirection", node.OperationDirection),
                new("shapeHash", node.ShapeHash),
                new("textHash", node.TextHash),
                new("textLength", node.TextLength),
                new("identityFallbackHash", IsVolatileSqlIdentity(node) ? CombinedReportHelpers.Hash(node.CombinedFactId ?? node.NodeId, 24) : null)
            ]),
            node.CombinedFactId is null ? [] : [node.CombinedFactId],
            caveats);
    }

    private static TraversalResult Traverse(
        CombinedPathGraphInventory graph,
        IReadOnlyList<CombinedPathNode> selectedSurfaceNodes,
        IReadOnlyList<CombinedReverseSurface> selectedSurfaces,
        string target,
        CombinedReverseOptions options,
        string? sourceFilter,
        IReadOnlyDictionary<string, CombinedReportSource> sourcesById)
    {
        var nodesById = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var edgesById = graph.Edges.ToDictionary(edge => edge.EdgeId, StringComparer.Ordinal);
        var surfaceByNodeId = selectedSurfaceNodes.Zip(selectedSurfaces).ToDictionary(pair => pair.First.NodeId, pair => pair.Second, StringComparer.Ordinal);
        var incoming = graph.Edges
            .GroupBy(edge => edge.ToNodeId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(EdgeRank)
                    .ThenBy(edge => nodesById.TryGetValue(edge.FromNodeId, out var from) ? from.DisplayName : string.Empty, StringComparer.Ordinal)
                    .ThenBy(edge => edge.FilePath, StringComparer.Ordinal)
                    .ThenBy(edge => edge.StartLine ?? 0)
                    .ThenBy(edge => edge.EdgeId, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        var paths = new List<CombinedReversePath>();
        var roots = new List<RootCandidate>();
        var gaps = new List<CombinedReverseGap>();
        var contributingSourceIds = new HashSet<string>(selectedSurfaceNodes.Select(node => node.SourceIndexId), StringComparer.Ordinal);
        var pathsByRoot = new Dictionary<string, int>(StringComparer.Ordinal);
        var truncated = false;
        foreach (var surfaceNode in selectedSurfaceNodes)
        {
            var queue = new Queue<ReverseState>();
            queue.Enqueue(new ReverseState(surfaceNode.NodeId, [surfaceNode.NodeId], []));
            var visitedStates = 0;
            while (queue.Count > 0)
            {
                if (queue.Count > options.MaxFrontier)
                {
                    truncated = true;
                    gaps.Add(TruncatedGap("frontier", surfaceByNodeId[surfaceNode.NodeId], surfaceNode));
                    break;
                }

                visitedStates++;
                if (visitedStates > options.MaxFrontier)
                {
                    truncated = true;
                    gaps.Add(TruncatedGap("frontier", surfaceByNodeId[surfaceNode.NodeId], surfaceNode));
                    break;
                }

                var state = queue.Dequeue();
                if (!nodesById.TryGetValue(state.NodeId, out var current))
                {
                    continue;
                }

                contributingSourceIds.Add(current.SourceIndexId);
                var isRoot = IsTargetRoot(current, target, sourceFilter);
                if (isRoot && state.EdgeIds.Count > 0)
                {
                    var rootKind = RootKind(current);
                    var rootId = RootId(current, rootKind);
                    pathsByRoot.TryGetValue(rootId, out var rootPathCount);
                    if (rootPathCount < options.MaxPathsPerRoot)
                    {
                        var path = ToReversePath(surfaceByNodeId[surfaceNode.NodeId], rootId, nodesById, edgesById, sourcesById, state);
                        paths.Add(path);
                        roots.Add(new RootCandidate(current, rootKind, rootId, path.PathId));
                        pathsByRoot[rootId] = rootPathCount + 1;
                    }
                    else if (rootPathCount == options.MaxPathsPerRoot)
                    {
                        truncated = true;
                        gaps.Add(TruncatedGap("paths-per-root", surfaceByNodeId[surfaceNode.NodeId], current));
                        pathsByRoot[rootId] = rootPathCount + 1;
                    }

                    if (target == "symbols")
                    {
                        continue;
                    }
                }

                if (state.EdgeIds.Count >= options.MaxDepth)
                {
                    truncated = true;
                    gaps.Add(TruncatedGap("depth", surfaceByNodeId[surfaceNode.NodeId], current));
                    continue;
                }

                if (!incoming.TryGetValue(current.NodeId, out var incomingEdges))
                {
                    continue;
                }

                foreach (var edge in incomingEdges)
                {
                    if (state.NodeIds.Contains(edge.FromNodeId, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    queue.Enqueue(new ReverseState(edge.FromNodeId, [.. state.NodeIds, edge.FromNodeId], [.. state.EdgeIds, edge.EdgeId]));
                }
            }
        }

        return new TraversalResult(paths, roots, gaps, contributingSourceIds, truncated);
    }

    private static SourceRootResult BuildSourceRootPaths(
        IReadOnlyList<CombinedReversePath> basePaths,
        IReadOnlyList<CombinedReportSource> sources,
        int maxPathsPerRoot)
    {
        var sourcesById = sources.ToDictionary(source => source.SourceIndexId, StringComparer.Ordinal);
        var paths = new List<CombinedReversePath>();
        var roots = new List<RootCandidate>();
        var gaps = new List<CombinedReverseGap>();
        var pathCountsByRoot = new Dictionary<string, int>(StringComparer.Ordinal);
        var truncated = false;
        foreach (var group in basePaths
            .SelectMany(path => path.Nodes.Select(node => node.SourceIndexId).Distinct(StringComparer.Ordinal).Select(sourceId => (Path: path, SourceId: sourceId)))
            .GroupBy(item => item.SourceId, StringComparer.Ordinal)
            .OrderBy(group => sourcesById.TryGetValue(group.Key, out var source) ? source.Label : group.Key, StringComparer.Ordinal))
        {
            var source = sourcesById.TryGetValue(group.Key, out var found) ? found : null;
            var sourceNode = SourceNode(group.Key, source, group.SelectMany(item => item.Path.Nodes).First(node => node.SourceIndexId == group.Key));
            var rootId = RootId(sourceNode, "Source");
            foreach (var path in group
                .Select(item => item.Path)
                .GroupBy(path => path.PathId, StringComparer.Ordinal)
                .Select(pathGroup => pathGroup.First())
                .OrderBy(path => path.Depth)
                .ThenBy(path => path.SurfaceId, StringComparer.Ordinal)
                .ThenBy(path => path.PathId, StringComparer.Ordinal))
            {
                pathCountsByRoot.TryGetValue(rootId, out var count);
                if (count >= maxPathsPerRoot)
                {
                    if (count == maxPathsPerRoot)
                    {
                        truncated = true;
                        gaps.Add(TruncatedGap("paths-per-root", path.SurfaceId, sourceNode));
                        pathCountsByRoot[rootId] = count + 1;
                    }

                    continue;
                }

                var sourcePath = path with
                {
                    PathId = SourcePathId(rootId, path),
                    RootId = rootId
                };
                paths.Add(sourcePath);
                roots.Add(new RootCandidate(sourceNode, "Source", rootId, sourcePath.PathId));
                pathCountsByRoot[rootId] = count + 1;
            }
        }

        return new SourceRootResult(paths, roots, gaps, truncated);
    }

    private static CombinedReversePath ToReversePath(
        CombinedReverseSurface surface,
        string rootId,
        IReadOnlyDictionary<string, CombinedPathNode> nodesById,
        IReadOnlyDictionary<string, CombinedPathEdge> edgesById,
        IReadOnlyDictionary<string, CombinedReportSource> sourcesById,
        ReverseState state)
    {
        var nodes = state.NodeIds.Reverse().Select(nodeId => SanitizeNode(nodesById[nodeId])).ToArray();
        var edges = state.EdgeIds.Reverse().Select(edgeId => edgesById[edgeId]).ToArray();
        var classification = ClassifyPath(edges, nodes, sourcesById);
        var pathId = $"reverse-path:{CombinedReportHelpers.Hash($"{rootId}\0{surface.SurfaceId}\0{string.Join("|", edges.Select(edge => edge.EdgeId))}", 32)}";
        return new CombinedReversePath(
            pathId,
            rootId,
            surface.SurfaceId,
            classification,
            Confidence(classification),
            edges.Length,
            nodes,
            edges,
            edges.Select(edge => edge.RuleId).Append(PathRuleId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            edges.Select(edge => edge.EvidenceTier).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            edges.SelectMany(edge => edge.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            edges.SelectMany(edge => edge.SupportingCombinedEdgeIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ReverseNotesFor(edges));
    }

    private static IReadOnlyList<CombinedPathNote> ReverseNotesFor(IReadOnlyList<CombinedPathEdge> edges)
    {
        var notes = new List<CombinedPathNote>();
        var valueOriginClassification = CombinedDependencyPathReporter.ClassifyValueOrigin(edges);
        if (valueOriginClassification is not null)
        {
            notes.Add(new CombinedPathNote(
                "ValueOriginClassification",
                $"{valueOriginClassification}: reverse value-origin context preserves supporting fact and edge IDs, but does not prove runtime execution or ordering."));
        }

        if (edges.Any(edge => edge.EdgeKind == "parameter-forward"))
        {
            notes.Add(new CombinedPathNote("ParameterForwardingBoundary", "Parameter-forwarding hops are direct static argument evidence, not full taint analysis or mutation tracking."));
        }

        if (edges.Any(edge => edge.EdgeKind == "symbol-reconciliation"))
        {
            notes.Add(new CombinedPathNote("SymbolReconciliationBoundary", "Symbol reconciliation hops are review-tier evidence, not compiler-resolved call evidence."));
        }

        return notes
            .OrderBy(note => note.Code, StringComparer.Ordinal)
            .ThenBy(note => note.Message, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<CombinedReversePath> SortPaths(IEnumerable<CombinedReversePath> paths)
    {
        return paths
            .GroupBy(path => path.PathId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(path => ClassificationRank(path.Classification))
            .ThenBy(path => path.Depth)
            .ThenBy(path => path.Nodes.FirstOrDefault()?.SourceLabel, StringComparer.Ordinal)
            .ThenBy(path => path.Nodes.FirstOrDefault()?.DisplayName, StringComparer.Ordinal)
            .ThenBy(path => path.Nodes.LastOrDefault()?.SurfaceKind, StringComparer.Ordinal)
            .ThenBy(path => path.Nodes.LastOrDefault()?.DisplayName, StringComparer.Ordinal)
            .ThenBy(path => path.Nodes.FirstOrDefault()?.FilePath, StringComparer.Ordinal)
            .ThenBy(path => path.Nodes.FirstOrDefault()?.StartLine ?? 0)
            .ThenBy(path => path.PathId, StringComparer.Ordinal);
    }

    private static IEnumerable<CombinedReversePath> DowngradePathsWithDuplicateNodes(
        IEnumerable<CombinedReversePath> paths,
        IReadOnlySet<string> duplicateNodeKeys)
    {
        foreach (var path in paths)
        {
            if (path.Nodes.Any(node => duplicateNodeKeys.Contains(NodeStableKey(node)))
                && ClassificationRank(path.Classification) < ClassificationRank(CombinedReverseClassifications.NeedsReviewReversePath))
            {
                yield return path with
                {
                    Classification = CombinedReverseClassifications.NeedsReviewReversePath,
                    Confidence = Confidence(CombinedReverseClassifications.NeedsReviewReversePath)
                };
                continue;
            }

            yield return path;
        }
    }

    private static IReadOnlyList<CombinedReverseRoot> BuildRoots(
        IReadOnlyList<RootCandidate> rootCandidates,
        IReadOnlyList<CombinedReversePath> paths,
        IReadOnlyDictionary<string, CombinedReportSource> sourcesById)
    {
        var pathsById = paths.ToDictionary(path => path.PathId, StringComparer.Ordinal);
        return rootCandidates
            .GroupBy(candidate => candidate.RootId, StringComparer.Ordinal)
            .Select(group =>
            {
                var candidate = group.OrderBy(item => item.Node.SourceLabel, StringComparer.Ordinal).ThenBy(item => item.Node.DisplayName, StringComparer.Ordinal).First();
                var pathIds = group.Select(item => item.PathId).Where(pathsById.ContainsKey).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var rootPaths = pathIds.Select(id => pathsById[id]).ToArray();
                var classification = rootPaths.Select(path => path.Classification).OrderBy(ClassificationRank).FirstOrDefault() ?? CombinedReverseClassifications.UnknownAnalysisGap;
                var source = sourcesById.TryGetValue(candidate.Node.SourceIndexId, out var found) ? found : null;
                var caveats = source is not null && SourceHasReducedCoverage(source) ? new[] { $"Source `{source.Label}` has reduced coverage." } : [];
                return new CombinedReverseRoot(
                    candidate.RootId,
                    candidate.RootKind,
                    RootStableKey(candidate.Node, candidate.RootKind),
                    candidate.Node.SourceLabel,
                    SafeDisplayName(candidate.Node.DisplayName),
                    classification,
                    Confidence(classification),
                    rootPaths.SelectMany(path => path.RuleIds).Append(RootRuleId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    rootPaths.SelectMany(path => path.EvidenceTiers).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    pathIds,
                    rootPaths.SelectMany(path => path.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    rootPaths.SelectMany(path => path.SupportingEdgeIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    caveats);
            })
            .ToArray();
    }

    private static bool IsTargetRoot(CombinedPathNode node, string target, string? sourceFilter)
    {
        if (!string.IsNullOrWhiteSpace(sourceFilter) && !string.Equals(node.SourceLabel, sourceFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return target switch
        {
            "endpoints" => IsEndpoint(node),
            "symbols" => IsSymbol(node),
            "sources" => false,
            "all" => IsEndpoint(node) || IsSymbol(node),
            _ => false
        };
    }

    private static string RootKind(CombinedPathNode node)
    {
        if (IsEndpoint(node))
        {
            return node.NodeKind == "EndpointClient" ? "EndpointClient" : "EndpointRoute";
        }

        if (IsSymbol(node))
        {
            return node.NodeKind;
        }

        return "Source";
    }

    private static bool IsEndpoint(CombinedPathNode node) => node.NodeKind is "EndpointClient" or "EndpointRoute";

    private static bool IsSymbol(CombinedPathNode node) => node.NodeKind is "Symbol" or "Method" or "Type";

    private static string ClassifySurface(CombinedPathNode node, bool hasDuplicateIdentity = false)
    {
        return hasDuplicateIdentity || node.EvidenceTier == "Tier3SyntaxOrTextual" || IsHashOnlySqlEvidence(node) || IsVolatileSqlIdentity(node)
            ? CombinedReverseClassifications.NeedsReviewSurfaceEvidence
            : CombinedReverseClassifications.SelectedSurfaceEvidence;
    }

    private static string ClassifyPath(
        IReadOnlyList<CombinedPathEdge> edges,
        IReadOnlyList<CombinedPathNode> nodes,
        IReadOnlyDictionary<string, CombinedReportSource> sourcesById)
    {
        if (edges.Any(edge => edge.Classification == CombinedEndpointClassifications.UnknownAnalysisGap))
        {
            return CombinedReverseClassifications.UnknownAnalysisGap;
        }

        if (nodes.Any(node => sourcesById.TryGetValue(node.SourceIndexId, out var source) && (SourceHasReducedCoverage(source) || SourceIdentityUnverified(source))))
        {
            return CombinedReverseClassifications.NeedsReviewReversePath;
        }

        if (edges.Any(edge => edge.EdgeKind == "endpoint-match" && (edge.Classification != CombinedEndpointClassifications.MatchedEndpoint || edge.EvidenceTier != "Tier2Structural"))
            || edges.Any(edge => edge.EvidenceTier == "Tier3SyntaxOrTextual" || edge.EdgeKind == "symbol-reconciliation"))
        {
            return CombinedReverseClassifications.NeedsReviewReversePath;
        }

        return edges.Any(edge => edge.EvidenceTier == "Tier2Structural")
            ? CombinedReverseClassifications.ProbableStaticReversePath
            : CombinedReverseClassifications.StrongStaticReversePath;
    }

    private static CombinedReverseGap FromPathGap(CombinedPathGap gap)
    {
        return new CombinedReverseGap(
            $"path-{gap.GapId}",
            gap.GapKind,
            gap.Classification,
            gap.RuleId ?? "combined.paths.query-gap.v1",
            gap.EvidenceTier ?? "Tier4Unknown",
            gap.Message,
            gap.SourceIndexId,
            gap.SourceLabel,
            null,
            null,
            null,
            gap.NodeId,
            gap.CombinedFactId,
            gap.FilePath,
            gap.StartLine,
            null,
            gap.Reason,
            EmptyMetadata());
    }

    private static CombinedReverseGap TruncatedGap(string reason, CombinedReverseSurface surface, CombinedPathNode node)
    {
        return TruncatedGap(reason, surface.SurfaceId, node);
    }

    private static CombinedReverseGap TruncatedGap(string reason, string? surfaceId, CombinedPathNode node)
    {
        return new CombinedReverseGap(
            $"gap:truncated:{reason}:{surfaceId ?? "n/a"}:{node.NodeId}",
            "TruncatedByLimit",
            CombinedReverseClassifications.TruncatedByLimit,
            TruncationRuleId,
            "Tier4Unknown",
            $"Reverse traversal was truncated by {reason} limit.",
            node.SourceIndexId,
            node.SourceLabel,
            surfaceId,
            null,
            null,
            node.NodeId,
            node.CombinedFactId,
            node.FilePath,
            node.StartLine,
            node.EndLine,
            reason,
            EmptyMetadata());
    }

    private static CombinedReverseSourceInfo ToSourceInfo(CombinedReportSource source, IReadOnlyList<string> allWarnings)
    {
        var identity = string.Join("|", new[] { source.RemoteUrl, source.RepoName, source.GitRootHash }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var warnings = allWarnings
            .Where(warning => warning.Contains(source.Label, StringComparison.Ordinal) || warning.Contains(source.SourceIndexId, StringComparison.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new CombinedReverseSourceInfo(
            source.Label,
            source.SourceIndexId,
            source.ScanId,
            source.CommitSha,
            source.Language ?? "unknown",
            source.AnalysisLevel,
            source.BuildStatus,
            string.IsNullOrWhiteSpace(identity) ? "unknown" : $"repo:{CombinedReportHelpers.Hash(identity, 24)}",
            !SourceIdentityUnverified(source),
            warnings);
    }

    private static bool SourceHasReducedCoverage(CombinedReportSource source)
    {
        return !source.AnalysisLevel.Equals("Level1SemanticAnalysis", StringComparison.Ordinal)
            || !source.BuildStatus.Equals("Succeeded", StringComparison.Ordinal)
            || source.CommitSha == "unknown";
    }

    private static bool SourceIdentityUnverified(CombinedReportSource source)
    {
        return !CombinedReportHelpers.SourceIdentityVerified(source);
    }

    private static string SurfaceId(CombinedPathNode node) => $"reverse-surface:{CombinedReportHelpers.Hash(SurfaceIdentity(node), 32)}";

    private static string SurfaceStableKey(CombinedPathNode node) => $"surface:{node.SourceLabel}:{node.SurfaceKind ?? "unknown"}:{CombinedReportHelpers.Hash(SurfaceIdentity(node), 32)}";

    private static string SurfaceIdentity(CombinedPathNode node)
    {
        return string.Join("|", [
            node.SourceLabel,
            node.SurfaceKind ?? "unknown",
            node.SurfaceName ?? node.DisplayName,
            node.HttpMethod ?? string.Empty,
            node.NormalizedPathKey ?? string.Empty,
            node.PackageName ?? string.Empty,
            node.ConfigKey ?? string.Empty,
            node.OperationName ?? string.Empty,
            node.TableName ?? string.Empty,
            node.ColumnNames ?? string.Empty,
            node.SourceKind ?? string.Empty,
            node.ShapeHash ?? string.Empty,
            node.TextHash ?? string.Empty,
            node.TextLength ?? string.Empty,
            IsVolatileSqlIdentity(node) ? CombinedReportHelpers.Hash(node.CombinedFactId ?? node.NodeId, 24) : string.Empty
        ]);
    }

    private static IReadOnlyList<string> SurfaceCaveats(CombinedPathNode node, CombinedReportSource? source)
    {
        var caveats = new List<string>();
        if (source is not null && SourceHasReducedCoverage(source))
        {
            caveats.Add($"Source `{source.Label}` has reduced coverage.");
        }

        if (IsHashOnlySqlEvidence(node))
        {
            caveats.Add("HashOnlyEvidence: SQL surface has text hash evidence without credible shape metadata; reverse evidence is review-tier.");
        }

        if (IsVolatileSqlIdentity(node))
        {
            caveats.Add("VolatileIdentity: SQL surface identity fell back to a fact hash because stable SQL metadata was unavailable; reverse evidence is review-tier.");
        }

        return caveats.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static bool IsHashOnlySqlEvidence(CombinedPathNode node)
    {
        return node.SurfaceKind == "sql-query"
            && HasSqlValue(node.TextHash)
            && !HasSqlValue(node.ShapeHash)
            && !HasSqlValue(node.OperationName)
            && !HasSqlValue(node.TableName)
            && !HasSqlValue(node.ColumnNames);
    }

    private static bool IsVolatileSqlIdentity(CombinedPathNode node)
    {
        return node.SurfaceKind == "sql-query"
            && !HasSqlValue(node.ShapeHash)
            && !HasSqlValue(node.TextHash)
            && !HasSqlValue(node.OperationName)
            && !HasSqlValue(node.TableName)
            && !HasSqlValue(node.ColumnNames);
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

    private static bool HasSqlValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && !value.Equals("n/a", StringComparison.OrdinalIgnoreCase);
    }

    private static string RootId(CombinedPathNode node, string rootKind) => $"reverse-root:{CombinedReportHelpers.Hash(RootIdentity(node, rootKind), 32)}";

    private static string RootStableKey(CombinedPathNode node, string rootKind) => $"{rootKind}:{node.SourceLabel}:{CombinedReportHelpers.Hash(RootIdentity(node, rootKind), 32)}";

    private static string RootIdentity(CombinedPathNode node, string rootKind)
    {
        return rootKind switch
        {
            "EndpointClient" or "EndpointRoute" => string.Join("|", [rootKind, node.SourceLabel, node.HttpMethod ?? "ANY", node.NormalizedPathKey ?? node.DisplayName]),
            "Source" => string.Join("|", [rootKind, node.SourceLabel, node.ScanId ?? string.Empty, node.CommitSha ?? string.Empty]),
            _ => string.Join("|", [rootKind, node.SourceIndexId, node.SymbolId ?? node.DisplayName])
        };
    }

    private static string SourcePathId(string sourceRootId, CombinedReversePath path)
    {
        return $"reverse-path:{CombinedReportHelpers.Hash($"{sourceRootId}\0{path.PathId}\0{path.SurfaceId}", 32)}";
    }

    private static CombinedPathNode SourceNode(string sourceIndexId, CombinedReportSource? source, CombinedPathNode fallback)
    {
        return new CombinedPathNode(
            $"source:{sourceIndexId}",
            "Source",
            source?.Label ?? fallback.SourceLabel,
            sourceIndexId,
            source?.Label ?? fallback.SourceLabel,
            source?.ScanId ?? fallback.ScanId,
            source?.CommitSha ?? fallback.CommitSha,
            null,
            null,
            RootRuleId,
            "Tier4Unknown",
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
            null,
            null,
            null,
            null);
    }

    private static string NodeStableKey(CombinedPathNode node)
    {
        var identity = string.Join("|", [
            node.NodeKind,
            node.SourceLabel,
            node.SymbolId ?? string.Empty,
            node.SurfaceKind ?? string.Empty,
            node.SurfaceName ?? node.DisplayName,
            node.HttpMethod ?? string.Empty,
            node.NormalizedPathKey ?? string.Empty,
            node.PackageName ?? string.Empty,
            node.ConfigKey ?? string.Empty,
            node.OperationName ?? string.Empty,
            node.TableName ?? string.Empty,
            node.ColumnNames ?? string.Empty,
            node.SourceKind ?? string.Empty,
            node.ShapeHash ?? string.Empty,
            node.TextHash ?? string.Empty,
            node.TextLength ?? string.Empty
        ]);
        return $"{node.NodeKind}:{node.SourceLabel}:{CombinedReportHelpers.Hash(identity, 32)}";
    }

    private static string NormalizeTarget(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "endpoints" : value.Trim().ToLowerInvariant();
    }

    private static string Confidence(string classification)
    {
        return classification switch
        {
            CombinedReverseClassifications.StrongStaticReversePath => "High",
            CombinedReverseClassifications.ProbableStaticReversePath => "Medium",
            CombinedReverseClassifications.SelectedSurfaceEvidence => "Medium",
            _ => "Low"
        };
    }

    private static int ClassificationRank(string classification)
    {
        return classification switch
        {
            CombinedReverseClassifications.StrongStaticReversePath => 0,
            CombinedReverseClassifications.ProbableStaticReversePath => 1,
            CombinedReverseClassifications.SelectedSurfaceEvidence => 2,
            CombinedReverseClassifications.NeedsReviewSurfaceEvidence => 3,
            CombinedReverseClassifications.NeedsReviewReversePath => 4,
            CombinedReverseClassifications.NoReversePathEvidence => 5,
            CombinedReverseClassifications.UnknownAnalysisGap => 6,
            CombinedReverseClassifications.SelectorNoMatch => 7,
            CombinedReverseClassifications.TruncatedByLimit => 8,
            _ => 99
        };
    }

    private static int SurfaceClassificationRank(string classification)
    {
        return classification == CombinedReverseClassifications.SelectedSurfaceEvidence ? 0 : 1;
    }

    private static int EdgeRank(CombinedPathEdge edge)
    {
        return edge.EdgeKind switch
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

    private static IReadOnlyDictionary<string, string> EmptyMetadata() => new SortedDictionary<string, string>(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> SortedMetadata(IEnumerable<KeyValuePair<string, string?>> values)
    {
        return CombinedReportHelpers.SortedMetadata(values).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static string SafeDisplayName(string value)
    {
        if (value.StartsWith("unknown-", StringComparison.Ordinal) && value.Contains(':', StringComparison.Ordinal))
        {
            return $"{value.Split(':', 2)[0]}:{CombinedReportHelpers.Hash(value, 16)}";
        }

        var urlIndex = value.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
        if (urlIndex < 0)
        {
            urlIndex = value.IndexOf("http://", StringComparison.OrdinalIgnoreCase);
        }

        if (urlIndex > 0)
        {
            var prefix = value[..urlIndex].TrimEnd();
            var url = value[urlIndex..];
            return string.IsNullOrWhiteSpace(prefix)
                ? $"url:{CombinedReportHelpers.Hash(url, 16)}"
                : $"{prefix} url:{CombinedReportHelpers.Hash(url, 16)}";
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return $"url:{CombinedReportHelpers.Hash(value, 16)}";
        }

        return value;
    }

    private static CombinedPathNode SanitizeNode(CombinedPathNode node)
    {
        return node with
        {
            DisplayName = SafeDisplayName(node.DisplayName),
            SurfaceName = node.SurfaceName is null ? null : SafeDisplayName(node.SurfaceName)
        };
    }

    private static string RenderMarkdown(CombinedReverseReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Reverse Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("- Reverse query evidence is static graph evidence, not runtime usage proof.");
        builder.AppendLine($"- Report coverage: `{report.ReportCoverage}`");
        builder.AppendLine($"- Sources: `{report.Summary.SourceCount}`");
        builder.AppendLine($"- Selected surfaces: `{report.Summary.SelectedSurfaceCount}`");
        builder.AppendLine($"- Reverse roots: `{report.Summary.ReverseRootCount}`");
        builder.AppendLine($"- Paths: `{report.Summary.PathCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        builder.AppendLine($"- Truncated: `{report.Summary.Truncated}`");
        AppendList(builder, "Coverage warnings", report.CoverageWarnings);
        builder.AppendLine();

        builder.AppendLine("## Query");
        builder.AppendLine();
        builder.AppendLine($"- Source: `{report.Query.Source ?? "all"}`");
        builder.AppendLine($"- Surface: `{report.Query.SurfaceKind ?? "all"}`");
        builder.AppendLine($"- Surface name: `{report.Query.SurfaceName ?? "n/a"}`");
        builder.AppendLine($"- Surface name match: `{report.Query.SurfaceNameMatchMode}`");
        builder.AppendLine($"- Message direction: `{report.Query.MessageDirection ?? "all"}`");
        builder.AppendLine($"- To: `{report.Query.To}`");
        builder.AppendLine($"- Bounds: surfaces `{report.Query.MaxSurfaces}`, depth `{report.Query.MaxDepth}`, frontier `{report.Query.MaxFrontier}`, roots `{report.Query.MaxRoots}`, paths per root `{report.Query.MaxPathsPerRoot}`, gaps `{report.Query.MaxGaps}`");
        builder.AppendLine();

        builder.AppendLine("## Snapshot Sources");
        builder.AppendLine();
        AppendRows(builder, report.Snapshot.Sources, "| Label | Language | Commit | Analysis | Build | Identity |", "| --- | --- | --- | --- | --- | --- |",
            source => $"| {Cell(source.SourceLabel)} | {Cell(source.Language)} | {Cell(source.CommitSha ?? "unknown")} | {Cell(source.AnalysisLevel)} | {Cell(source.BuildStatus)} | {Cell(source.IdentityVerified ? "verified" : "unverified")} |");

        builder.AppendLine("## Selected Surfaces");
        builder.AppendLine();
        AppendRows(builder, report.SelectedSurfaces, "| Surface | Source | Classification | Evidence |", "| --- | --- | --- | --- |",
            surface => $"| {Cell($"{surface.SurfaceKind} {surface.DisplayName}")} | {Cell(surface.SourceLabel)} | {Cell(surface.Classification)} `{Cell(surface.Confidence)}` | {Cell(Evidence(surface.RuleId, surface.EvidenceTier, surface.FilePath, surface.StartLine))} |");

        builder.AppendLine("## Reverse Roots");
        builder.AppendLine();
        AppendRows(builder, report.ReverseRoots, "| Root | Source | Classification | Paths | Rules |", "| --- | --- | --- | --- | --- |",
            root => $"| {Cell($"{root.RootKind} {root.DisplayName}")} | {Cell(root.SourceLabel)} | {Cell(root.Classification)} `{Cell(root.Confidence)}` | {root.PathIds.Count} | {Cell(string.Join(", ", root.RuleIds))} |");

        builder.AppendLine("## Paths");
        builder.AppendLine();
        if (report.Paths.Count == 0)
        {
            builder.AppendLine("No reverse paths found for this query.");
            builder.AppendLine();
        }
        else
        {
            foreach (var path in report.Paths.Take(MarkdownRowLimit))
            {
                builder.AppendLine($"### {Cell(path.PathId)} `{path.Classification}` `{path.Confidence}`");
                builder.AppendLine();
                builder.AppendLine("| Hop | Source | Node | Evidence | Edge Out |");
                builder.AppendLine("| --- | --- | --- | --- | --- |");
                for (var index = 0; index < path.Nodes.Count; index++)
                {
                    var node = path.Nodes[index];
                    var edge = index >= path.Edges.Count ? null : path.Edges[index];
                    builder.AppendLine($"| {index} | {Cell(node.SourceLabel)} | {Cell($"{node.NodeKind} {node.DisplayName}")} | {Cell(Evidence(node.RuleId, node.EvidenceTier, node.FilePath, node.StartLine))} | {Cell(edge?.EdgeKind ?? "terminal")} |");
                }

                AppendPathNotes(builder, path.Notes);
                builder.AppendLine();
            }
        }

        builder.AppendLine("## Gaps");
        builder.AppendLine();
        AppendRows(builder, report.Gaps, "| Kind | Classification | Source | Message | Evidence |", "| --- | --- | --- | --- | --- |",
            gap => $"| {Cell(gap.GapKind)} | {Cell(gap.Classification)} | {Cell(gap.SourceLabel ?? "n/a")} | {Cell(gap.Message)} | {Cell(Evidence(gap.RuleId, gap.EvidenceTier, gap.FilePath, gap.StartLine))} |");

        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations)
        {
            builder.AppendLine($"- {Cell(limitation)}");
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
        foreach (var note in notes.OrderBy(note => note.Code, StringComparer.Ordinal).ThenBy(note => note.Message, StringComparer.Ordinal))
        {
            builder.AppendLine($"- `{Cell(note.Code)}`: {Cell(note.Message)}");
        }
    }

    private static string Evidence(string? ruleId, string? evidenceTier, string? filePath, int? startLine)
    {
        return $"{ruleId ?? "n/a"} {evidenceTier ?? "n/a"} {filePath ?? "n/a"}:{startLine ?? 0}";
    }

    private static string Cell(string? value) => CombinedReportHelpers.Cell(value);

    private sealed record ReverseState(string NodeId, IReadOnlyList<string> NodeIds, IReadOnlyList<string> EdgeIds);

    private sealed record RootCandidate(CombinedPathNode Node, string RootKind, string RootId, string PathId);

    private sealed record TraversalResult(
        IReadOnlyList<CombinedReversePath> Paths,
        IReadOnlyList<RootCandidate> Roots,
        IReadOnlyList<CombinedReverseGap> Gaps,
        IReadOnlySet<string> ContributingSourceIds,
        bool Truncated);

    private sealed record SourceRootResult(
        IReadOnlyList<CombinedReversePath> Paths,
        IReadOnlyList<RootCandidate> Roots,
        IReadOnlyList<CombinedReverseGap> Gaps,
        bool Truncated);
}
