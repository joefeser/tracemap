using System.Text;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record CombinedChangeImpactOptions(
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
    int MaxImpactItems = 100,
    int MaxPathsPerItem = 5,
    int MaxPathQueries = 50,
    int MaxDepth = 8,
    int MaxFrontier = 10000,
    int MaxGaps = 1000);

public sealed record CombinedChangeImpactResult(
    CombinedChangeImpactReport Report,
    string? MarkdownPath,
    string? JsonPath,
    bool HasImpactItems);

public sealed record CombinedChangeImpactReport(
    string ReportType,
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    CombinedImpactQuery Query,
    CombinedDiffSnapshotInfo BeforeSnapshot,
    CombinedDiffSnapshotInfo AfterSnapshot,
    CombinedImpactSummary Summary,
    IReadOnlyList<CombinedImpactItem> ImpactItems,
    IReadOnlyList<CombinedImpactGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record CombinedImpactQuery(
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> DelegatedDiffScopes,
    bool IncludePaths,
    string? Source,
    string? Endpoint,
    string? Surface,
    string? SurfaceName,
    int MaxImpactItems,
    int MaxPathsPerItem,
    int MaxPathQueries,
    int MaxDepth,
    int MaxFrontier,
    int MaxGaps,
    bool AllowIdentityMismatch,
    bool ExitCode,
    IReadOnlyList<string> IgnoredSelectors,
    string Algorithm,
    string AlgorithmVersion);

public sealed record CombinedImpactSummary(
    int DiffCount,
    int ImpactItemCount,
    int SourceImpactCount,
    int CoverageImpactCount,
    int EndpointImpactCount,
    int SurfaceImpactCount,
    int EdgeImpactCount,
    int PathImpactCount,
    int GapCount,
    bool Truncated);

public sealed record CombinedImpactItem(
    string ImpactId,
    string ChangeType,
    string Classification,
    string Confidence,
    string EvidenceKind,
    string SourceLabel,
    string StableKey,
    string DiffRuleId,
    string ImpactRuleId,
    string? EvidenceTier,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    CombinedDiffEvidence? Before,
    CombinedDiffEvidence? After,
    CombinedImpactPathContext PathContext,
    IReadOnlyList<CombinedCoverageCaveat> CoverageCaveats,
    IReadOnlyList<CombinedImpactNote> Notes);

public sealed record CombinedImpactPathContext(
    string Classification,
    IReadOnlyList<CombinedImpactPathSummary> BeforePaths,
    IReadOnlyList<CombinedImpactPathSummary> AfterPaths,
    IReadOnlyList<CombinedImpactGap> Gaps);

public sealed record CombinedImpactPathSummary(
    string PathId,
    string Classification,
    IReadOnlyList<string> SourceTransitions,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<KeyValuePair<string, string>> TerminalSurfaceMetadata);

public sealed record CombinedImpactGap(
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

public sealed record CombinedImpactNote(string Code, string Message);

internal sealed record PathContextSelector(
    string? FromEndpoint,
    string? FromSymbol,
    string? FromSource,
    string? ToSurface,
    string? SurfaceName);

internal sealed record PathContextQueryResult(
    IReadOnlyList<CombinedPath> Paths,
    IReadOnlyList<CombinedPathGap> Gaps,
    string ReportCoverage);

public static class CombinedImpactClassifications
{
    public const string StaticImpactEvidence = nameof(StaticImpactEvidence);
    public const string ProbableStaticImpact = nameof(ProbableStaticImpact);
    public const string NeedsReviewImpact = nameof(NeedsReviewImpact);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string NoImpactEvidence = nameof(NoImpactEvidence);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
    public const string TruncatedByLimit = nameof(TruncatedByLimit);
    public const string NotRequested = nameof(NotRequested);
    public const string PathContextUnavailable = nameof(PathContextUnavailable);
    public const string ReachabilityChanged = nameof(ReachabilityChanged);
    public const string ReachabilityEvidenceChanged = nameof(ReachabilityEvidenceChanged);
    public const string ReachabilityUnchanged = nameof(ReachabilityUnchanged);
    public const string NoPathEvidence = nameof(NoPathEvidence);
}

public static class CombinedChangeImpactReporter
{
    private const int DelegatedDiffRowCap = 1000;
    private const string Version = "1.0";
    private const string ReportType = "combined-change-impact";
    private const string Algorithm = "diff-to-static-impact";
    private const string AlgorithmVersion = "1.0";
    private const string SourceRuleId = "combined.impact.source.v1";
    private const string CoverageRuleId = "combined.impact.coverage.v1";
    private const string EndpointRuleId = "combined.impact.endpoint.v1";
    private const string SurfaceRuleId = "combined.impact.surface.v1";
    private const string EdgeRuleId = "combined.impact.edge.v1";
    private const string PathRuleId = "combined.impact.path.v1";
    private const string PathContextRuleId = "combined.impact.path-context.v1";
    private const string SelectorRuleId = "combined.impact.selector.v1";
    private const string TruncationRuleId = "combined.impact.truncation.v1";

    private static readonly HashSet<string> ValidScopes = new(StringComparer.Ordinal)
    {
        "all",
        "sources",
        "coverage",
        "endpoints",
        "surfaces",
        "edges",
        "paths"
    };

    private static readonly IReadOnlyList<string> Limitations =
    [
        "Impact rows describe static change evidence, not runtime or business impact.",
        "Path context is opt-in, bounded static graph evidence and must not be treated as complete dependency coverage when truncated.",
        "Endpoint evidence does not prove runtime traffic, auth behavior, proxies, deployment base paths, CORS behavior, or reachability.",
        "SQL/query evidence does not prove runtime execution, schema existence, generated SQL equivalence, dialect validity, or branch feasibility.",
        "Reduced scan coverage makes absence of evidence coverage-relative."
    ];

    public static async Task<CombinedChangeImpactResult> WriteAsync(CombinedChangeImpactOptions options, CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(options, cancellationToken);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "impact");
        var (markdownPath, jsonPath) = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "impact-report.md",
            "impact-report.json",
            report,
            RenderMarkdown,
            CombinedDependencyReporter.JsonOptions,
            cancellationToken);
        return new CombinedChangeImpactResult(report, markdownPath, jsonPath, report.ImpactItems.Count > 0);
    }

    public static async Task<CombinedChangeImpactReport> BuildReportAsync(CombinedChangeImpactOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var scopes = NormalizeScopes(options.Scope, options.IncludePaths);
        var delegatedScopes = DelegatedDiffScopes(scopes, options.IncludePaths);
        var ignoredSelectors = IgnoredSelectors(options, scopes);
        var diffReport = await CombinedDependencyDiffer.BuildReportAsync(new CombinedDependencyDiffOptions(
            options.BeforePath,
            options.AfterPath,
            options.OutputPath,
            "json",
            string.Join(",", delegatedScopes),
            options.IncludePaths && delegatedScopes.Contains("paths"),
            options.AllowIdentityMismatch,
            options.ExitCode,
            options.Source,
            options.Endpoint,
            options.Surface,
            options.SurfaceName,
            options.MaxDepth,
            options.MaxPathsPerItem,
            options.MaxFrontier,
            DelegatedDiffRowCap,
            options.MaxGaps), cancellationToken);

        var gaps = diffReport.Gaps.Select(FromDiffGap).ToList();
        gaps.AddRange(ignoredSelectors.Select((message, index) => new CombinedImpactGap(
            $"gap:impact:ignored-selector:{CombinedReportHelpers.Hash($"{index}:{message}", 16)}",
            "IgnoredSelector",
            options.Source,
            null,
            SelectorRuleId,
            EvidenceTiers.Tier4Unknown,
            CombinedImpactClassifications.NeedsReviewImpact,
            message,
            [],
            [])));

        var items = new List<CombinedImpactItem>();
        if (scopes.Contains("sources"))
        {
            items.AddRange(diffReport.SourceDiffs.Select(row => FromDiffRow(row, "source", SourceRuleId)));
        }

        if (scopes.Contains("coverage"))
        {
            items.AddRange(diffReport.CoverageDiffs.Select(row => FromDiffRow(row, "coverage", CoverageRuleId)));
        }

        if (scopes.Contains("endpoints"))
        {
            items.AddRange(diffReport.EndpointDiffs.Select(row => FromDiffRow(row, "endpoint", EndpointRuleId)));
        }

        if (scopes.Contains("surfaces"))
        {
            items.AddRange(diffReport.SurfaceDiffs.Select(row => FromDiffRow(row, "surface", SurfaceRuleId)));
        }

        if (scopes.Contains("edges"))
        {
            items.AddRange(diffReport.EdgeDiffs.Select(row => FromDiffRow(row, "edge", EdgeRuleId)));
        }

        if (scopes.Contains("paths"))
        {
            items.AddRange(diffReport.PathDiffs.Select(row => FromPathDiffRow(row, options.IncludePaths)));
        }

        var sortedItems = SortAndCapItems(items, options.MaxImpactItems, gaps, out var itemsTruncated);
        var contextualItems = await AddPathContextAsync(sortedItems, options, gaps, cancellationToken);
        if (contextualItems.Count == 0
            && !gaps.Any(gap => gap.Classification is CombinedImpactClassifications.SelectorNoMatch or CombinedImpactClassifications.NoImpactEvidence))
        {
            gaps.Add(new CombinedImpactGap(
                "gap:impact:no-evidence",
                "NoImpactEvidence",
                options.Source,
                null,
                SourceRuleId,
                EvidenceTiers.Tier4Unknown,
                CombinedImpactClassifications.NoImpactEvidence,
                "No static impact evidence was found for the selected combined snapshots and scopes.",
                [],
                []));
        }

        var sortedGaps = SortAndCapGaps(gaps, options.MaxGaps, out var gapsTruncated);
        var reportCoverage = ReportCoverage(diffReport.ReportCoverage, contextualItems, sortedGaps);
        return new CombinedChangeImpactReport(
            ReportType,
            Version,
            reportCoverage,
            diffReport.CoverageWarnings,
            new CombinedImpactQuery(
                scopes,
                delegatedScopes,
                options.IncludePaths,
                options.Source,
                options.Endpoint,
                options.Surface,
                options.SurfaceName,
                options.MaxImpactItems,
                options.MaxPathsPerItem,
                options.MaxPathQueries,
                options.MaxDepth,
                options.MaxFrontier,
                options.MaxGaps,
                options.AllowIdentityMismatch,
                options.ExitCode,
                ignoredSelectors,
                Algorithm,
                AlgorithmVersion),
            diffReport.BeforeSnapshot,
            diffReport.AfterSnapshot,
            new CombinedImpactSummary(
                diffReport.Summary.SourceDiffCount + diffReport.Summary.CoverageDiffCount + diffReport.Summary.EndpointDiffCount + diffReport.Summary.SurfaceDiffCount + diffReport.Summary.EdgeDiffCount + diffReport.Summary.PathDiffCount,
                contextualItems.Count,
                contextualItems.Count(item => item.EvidenceKind == "source"),
                contextualItems.Count(item => item.EvidenceKind == "coverage"),
                contextualItems.Count(item => item.EvidenceKind == "endpoint"),
                contextualItems.Count(item => item.EvidenceKind == "surface"),
                contextualItems.Count(item => item.EvidenceKind == "edge"),
                contextualItems.Count(item => item.EvidenceKind == "path"),
                sortedGaps.Count,
                itemsTruncated || gapsTruncated || sortedGaps.Any(gap => gap.GapKind == "TruncatedByLimit")),
            contextualItems,
            sortedGaps,
            Limitations);
    }

    private static void ValidateOptions(CombinedChangeImpactOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BeforePath))
        {
            throw new ArgumentException("impact requires --before <combined.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.AfterPath))
        {
            throw new ArgumentException("impact requires --after <combined.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("impact requires --out <path>.");
        }

        if (options.MaxImpactItems <= 0 || options.MaxPathsPerItem <= 0 || options.MaxPathQueries <= 0 || options.MaxDepth <= 0 || options.MaxFrontier <= 0 || options.MaxGaps <= 0)
        {
            throw new ArgumentException("impact numeric limits must be positive integers.");
        }

        _ = NormalizeScopes(options.Scope, options.IncludePaths);
    }

    private static IReadOnlyList<string> NormalizeScopes(string? value, bool includePaths)
    {
        var rawScopes = string.IsNullOrWhiteSpace(value)
            ? ["sources", "coverage", "endpoints", "surfaces", "edges"]
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scopes = rawScopes.Select(scope => scope.ToLowerInvariant()).ToArray();
        foreach (var scope in scopes)
        {
            if (!ValidScopes.Contains(scope))
            {
                throw new ArgumentException("impact --scope must be one of all, sources, coverage, endpoints, surfaces, edges, or paths.");
            }
        }

        if (scopes.Contains("all"))
        {
            scopes = includePaths
                ? ["sources", "coverage", "endpoints", "surfaces", "edges", "paths"]
                : ["sources", "coverage", "endpoints", "surfaces", "edges"];
        }

        if (!includePaths && scopes.Contains("paths"))
        {
            throw new ArgumentException("impact --scope paths requires --include-paths.");
        }

        return scopes.Distinct(StringComparer.Ordinal).OrderBy(ScopeRank).ToArray();
    }

    private static IReadOnlyList<string> DelegatedDiffScopes(IReadOnlyList<string> scopes, bool includePaths)
    {
        var delegated = scopes
            .Select(scope => scope == "coverage" ? "sources" : scope)
            .Where(scope => scope != "paths" || includePaths)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ScopeRank)
            .ToArray();
        return delegated.Length == 0 ? ["sources"] : delegated;
    }

    private static int ScopeRank(string scope)
    {
        return scope switch
        {
            "sources" => 0,
            "coverage" => 1,
            "endpoints" => 2,
            "surfaces" => 3,
            "edges" => 4,
            "paths" => 5,
            _ => 99
        };
    }

    private static IReadOnlyList<string> IgnoredSelectors(CombinedChangeImpactOptions options, IReadOnlyList<string> scopes)
    {
        var ignored = new List<string>();
        if (!scopes.Contains("endpoints") && !scopes.Contains("paths") && !string.IsNullOrWhiteSpace(options.Endpoint))
        {
            ignored.Add("--endpoint has no enabled endpoint or path scope.");
        }

        if (!scopes.Contains("surfaces") && !scopes.Contains("paths") && !string.IsNullOrWhiteSpace(options.Surface))
        {
            ignored.Add("--surface has no enabled surface or path scope.");
        }

        if (!scopes.Contains("surfaces") && !scopes.Contains("paths") && !string.IsNullOrWhiteSpace(options.SurfaceName))
        {
            ignored.Add("--surface-name has no enabled surface or path scope.");
        }

        if (!options.IncludePaths && (options.MaxPathsPerItem != 5 || options.MaxPathQueries != 50))
        {
            ignored.Add("Path context limits were provided but --include-paths is disabled.");
        }

        return ignored.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static async Task<IReadOnlyList<CombinedImpactItem>> AddPathContextAsync(
        IReadOnlyList<CombinedImpactItem> items,
        CombinedChangeImpactOptions options,
        List<CombinedImpactGap> globalGaps,
        CancellationToken cancellationToken)
    {
        if (!options.IncludePaths || items.Count == 0)
        {
            return items;
        }

        var queryCount = 0;
        var contextual = new List<CombinedImpactItem>(items.Count);
        foreach (var item in items)
        {
            if (item.EvidenceKind is "source" or "coverage" or "path")
            {
                contextual.Add(item);
                continue;
            }

            var selector = TryCreatePathSelector(item);
            if (selector is null)
            {
                var gap = PathContextGap(
                    item,
                    "PathContextUnavailable",
                    CombinedImpactClassifications.PathContextUnavailable,
                    "No safe path selector could be derived for this impact item.",
                    "selector");
                globalGaps.Add(gap);
                contextual.Add(item with
                {
                    PathContext = new CombinedImpactPathContext(CombinedImpactClassifications.PathContextUnavailable, [], [], [gap])
                });
                continue;
            }

            if (queryCount + 2 > options.MaxPathQueries)
            {
                var gap = PathContextGap(
                    item,
                    "TruncatedByLimit",
                    CombinedImpactClassifications.TruncatedByLimit,
                    $"Path context query output was capped at {options.MaxPathQueries} before/after queries.",
                    "max-path-queries");
                globalGaps.Add(gap);
                contextual.Add(item with
                {
                    PathContext = new CombinedImpactPathContext(CombinedImpactClassifications.PathContextUnavailable, [], [], [gap])
                });
                continue;
            }

            queryCount += 2;
            var beforeTask = QueryPathContextAsync(options.BeforePath, selector, options, cancellationToken);
            var afterTask = QueryPathContextAsync(options.AfterPath, selector, options, cancellationToken);
            await Task.WhenAll(beforeTask, afterTask);
            var before = await beforeTask;
            var after = await afterTask;
            var pathContext = ClassifyPathContext(item, before, after);
            globalGaps.AddRange(pathContext.Gaps);
            contextual.Add(item with { PathContext = pathContext });
        }

        return contextual;
    }

    private static PathContextSelector? TryCreatePathSelector(CombinedImpactItem item)
    {
        var evidence = item.After ?? item.Before;
        if (evidence is null)
        {
            return null;
        }

        if (item.EvidenceKind == "endpoint")
        {
            var displayNameParts = evidence.DisplayName?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var method = MetadataValue(evidence, "httpMethod") ?? displayNameParts?.FirstOrDefault();
            var pathKey = MetadataValue(evidence, "normalizedPathKey")
                ?? (displayNameParts?.Length == 2 ? displayNameParts[1] : null);
            if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(pathKey))
            {
                return null;
            }

            return new PathContextSelector($"{method.Trim()} {pathKey.Trim()}", null, evidence.SourceLabel, null, null);
        }

        if (item.EvidenceKind == "surface")
        {
            var surfaceKind = MetadataValue(evidence, "surfaceKind");
            if (string.IsNullOrWhiteSpace(surfaceKind))
            {
                return null;
            }

            var surfaceName = MetadataValue(evidence, "tableName")
                ?? MetadataValue(evidence, "configKey")
                ?? MetadataValue(evidence, "packageName")
                ?? MetadataValue(evidence, "normalizedPathKey")
                ?? evidence.DisplayName;
            return new PathContextSelector(null, null, evidence.SourceLabel, surfaceKind.Trim(), surfaceName);
        }

        if (item.EvidenceKind == "edge")
        {
            var sourceSymbol = MetadataValue(evidence, "sourceSymbol");
            if (string.IsNullOrWhiteSpace(sourceSymbol))
            {
                return null;
            }

            return new PathContextSelector(null, sourceSymbol.Trim(), evidence.SourceLabel, null, null);
        }

        return null;
    }

    private static async Task<PathContextQueryResult> QueryPathContextAsync(
        string indexPath,
        PathContextSelector selector,
        CombinedChangeImpactOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await CombinedDependencyPathReporter.BuildReportAsync(
                new CombinedDependencyPathOptions(
                    indexPath,
                    options.OutputPath,
                    "json",
                    selector.FromEndpoint,
                    selector.FromSymbol,
                    selector.FromSource,
                    selector.ToSurface,
                    selector.SurfaceName,
                    null,
                    options.MaxDepth,
                    options.MaxPathsPerItem,
                    options.MaxFrontier),
                cancellationToken);
            return new PathContextQueryResult(report.Paths, report.Gaps, report.ReportCoverage);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new PathContextQueryResult(
                [],
                [
                    new CombinedPathGap(
                        $"gap:impact:path-context:error:{CombinedReportHelpers.Hash(exception.Message, 16)}",
                        "UnknownAnalysisGap",
                        CombinedDependencyPathClassifications.UnknownAnalysisGap,
                        exception.Message,
                        null,
                        selector.FromSource,
                        null,
                        null,
                        PathContextRuleId,
                        EvidenceTiers.Tier4Unknown,
                        null,
                        null,
                        "path-context")
                ],
                "UnknownAnalysisGap");
        }
    }

    private static CombinedImpactPathContext ClassifyPathContext(
        CombinedImpactItem item,
        PathContextQueryResult before,
        PathContextQueryResult after)
    {
        var beforePaths = before.Paths.Select(PathSummary).ToArray();
        var afterPaths = after.Paths.Select(PathSummary).ToArray();
        var gaps = before.Gaps.Select(gap => FromPathGap(item, "before", gap))
            .Concat(after.Gaps.Select(gap => FromPathGap(item, "after", gap)))
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.Message, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        var classification = PathContextClassification(before, after, gaps);
        return new CombinedImpactPathContext(classification, beforePaths, afterPaths, gaps);
    }

    private static string PathContextClassification(PathContextQueryResult before, PathContextQueryResult after, IReadOnlyList<CombinedImpactGap> gaps)
    {
        var hasAnyPathEvidence = before.Paths.Count > 0 || after.Paths.Count > 0;
        if (!hasAnyPathEvidence)
        {
            if (gaps.Any(gap => gap.Classification == CombinedImpactClassifications.TruncatedByLimit))
            {
                return CombinedImpactClassifications.PathContextUnavailable;
            }

            if (before.ReportCoverage == "UnknownAnalysisGap"
                || after.ReportCoverage == "UnknownAnalysisGap"
                || gaps.Any(gap => gap.Classification == CombinedImpactClassifications.UnknownAnalysisGap))
            {
                return CombinedImpactClassifications.UnknownAnalysisGap;
            }

            if (before.ReportCoverage != "FullEvidenceAvailable" || after.ReportCoverage != "FullEvidenceAvailable")
            {
                return CombinedImpactClassifications.UnknownAnalysisGap;
            }

            if (gaps.Any(gap => gap.Classification == CombinedImpactClassifications.PathContextUnavailable))
            {
                return CombinedImpactClassifications.PathContextUnavailable;
            }

            return CombinedImpactClassifications.NoPathEvidence;
        }

        var beforeIdentity = before.Paths.Select(PathIdentity).ToHashSet(StringComparer.Ordinal);
        var afterIdentity = after.Paths.Select(PathIdentity).ToHashSet(StringComparer.Ordinal);
        if (!beforeIdentity.SetEquals(afterIdentity))
        {
            return CombinedImpactClassifications.ReachabilityChanged;
        }

        var beforeEvidence = before.Paths.Select(PathEvidenceSignature).ToHashSet(StringComparer.Ordinal);
        var afterEvidence = after.Paths.Select(PathEvidenceSignature).ToHashSet(StringComparer.Ordinal);
        return beforeEvidence.SetEquals(afterEvidence)
            ? CombinedImpactClassifications.ReachabilityUnchanged
            : CombinedImpactClassifications.ReachabilityEvidenceChanged;
    }

    private static CombinedImpactPathSummary PathSummary(CombinedPath path)
    {
        return new CombinedImpactPathSummary(
            path.PathId,
            path.Classification,
            SourceTransitions(path),
            path.SupportingFactIds,
            path.SupportingEdgeIds,
            TerminalMetadata(path));
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
            Pair("operationName", terminal.OperationName),
            Pair("tableName", terminal.TableName),
            Pair("columnNames", terminal.ColumnNames),
            Pair("sqlSourceKind", terminal.SourceKind),
            Pair("shapeHash", terminal.ShapeHash),
            Pair("textHash", terminal.TextHash),
            Pair("textLength", terminal.TextLength),
            Pair("packageName", terminal.PackageName),
            Pair("configKey", terminal.ConfigKey)
        ]);
    }

    private static CombinedImpactGap FromPathGap(CombinedImpactItem item, string side, CombinedPathGap gap)
    {
        var classification = gap.GapKind switch
        {
            "TruncatedByLimit" => CombinedImpactClassifications.TruncatedByLimit,
            "NoPathFound" => CombinedImpactClassifications.NoPathEvidence,
            "SelectorNoMatch" => CombinedImpactClassifications.PathContextUnavailable,
            _ when gap.Classification == CombinedDependencyPathClassifications.UnknownAnalysisGap => CombinedImpactClassifications.UnknownAnalysisGap,
            _ => CombinedImpactClassifications.NeedsReviewImpact
        };
        return new CombinedImpactGap(
            $"gap:impact:path-context:{side}:{item.ImpactId}:{CombinedReportHelpers.Hash(gap.GapId, 16)}",
            gap.GapKind,
            gap.SourceLabel ?? item.SourceLabel,
            item.EvidenceKind,
            gap.RuleId == "combined.paths.truncation-gap.v1" ? TruncationRuleId : PathContextRuleId,
            gap.EvidenceTier ?? EvidenceTiers.Tier4Unknown,
            classification,
            $"{side}: {gap.Message}",
            item.SupportingFactIds,
            item.SupportingEdgeIds);
    }

    private static CombinedImpactGap PathContextGap(CombinedImpactItem item, string gapKind, string classification, string message, string reason)
    {
        return new CombinedImpactGap(
            $"gap:impact:path-context:{item.ImpactId}:{reason}",
            gapKind,
            item.SourceLabel,
            item.EvidenceKind,
            gapKind == "TruncatedByLimit" ? TruncationRuleId : PathContextRuleId,
            EvidenceTiers.Tier4Unknown,
            classification,
            message,
            item.SupportingFactIds,
            item.SupportingEdgeIds);
    }

    private static string PathIdentity(CombinedPath path)
    {
        return string.Join("\u001f", [
            string.Join(">", path.Nodes.Select(node => $"{node.NodeKind}:{node.SourceLabel}:{node.DisplayName}:{node.SurfaceKind}:{node.SurfaceName}:{node.HttpMethod}:{node.NormalizedPathKey}:{node.OperationName}:{node.TableName}:{node.ColumnNames}:{node.SourceKind}:{node.ShapeHash}:{node.TextHash}:{node.TextLength}")),
            string.Join(">", path.Edges.Select(edge => edge.EdgeKind))
        ]);
    }

    private static string PathEvidenceSignature(CombinedPath path)
    {
        return string.Join("\u001f", [
            PathIdentity(path),
            string.Join(">", path.Edges.Select(edge => $"{edge.RuleId}:{edge.EvidenceTier}:{edge.FilePath}:{edge.StartLine}:{edge.EndLine}")),
            string.Join(">", path.SupportingFactIds),
            string.Join(">", path.SupportingEdgeIds)
        ]);
    }

    private static string? MetadataValue(CombinedDiffEvidence evidence, string key)
    {
        return evidence.SafeMetadata.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.Ordinal)).Value;
    }

    private static KeyValuePair<string, string?> Pair(string key, string? value) => new(key, value);

    private static CombinedImpactItem FromDiffRow(CombinedDiffRow row, string evidenceKind, string impactRuleId)
    {
        var evidence = row.After ?? row.Before;
        var classification = ImpactClassification(row, evidence);
        var supportingFactIds = SortedIds((row.Before?.SupportingFactIds ?? []).Concat(row.After?.SupportingFactIds ?? []));
        var supportingEdgeIds = SortedIds((row.Before?.SupportingEdgeIds ?? []).Concat(row.After?.SupportingEdgeIds ?? []));
        return new CombinedImpactItem(
            ImpactId(row.ChangeType, classification, evidenceKind, row.StableKey, row.DiffRuleId, impactRuleId),
            row.ChangeType,
            classification,
            Confidence(classification),
            evidenceKind,
            evidence?.SourceLabel ?? "unknown",
            row.StableKey,
            row.DiffRuleId,
            impactRuleId,
            evidence?.EvidenceTier,
            evidence?.FilePath,
            evidence?.StartLine,
            evidence?.EndLine,
            supportingFactIds,
            supportingEdgeIds,
            row.Before,
            row.After,
            new CombinedImpactPathContext(CombinedImpactClassifications.NotRequested, [], [], []),
            row.CoverageCaveats,
            NotesFor(row));
    }

    private static CombinedImpactItem FromPathDiffRow(CombinedPathDiffRow row, bool includePaths)
    {
        var classification = row.Classification switch
        {
            CombinedDependencyDiffClassifications.UnknownAnalysisGap => CombinedImpactClassifications.UnknownAnalysisGap,
            CombinedDependencyDiffClassifications.NeedsReviewDiff or CombinedDependencyDiffClassifications.AddedWithBeforeGap or CombinedDependencyDiffClassifications.RemovedWithAfterGap => CombinedImpactClassifications.NeedsReviewImpact,
            _ => includePaths ? CombinedImpactClassifications.ProbableStaticImpact : CombinedImpactClassifications.NeedsReviewImpact
        };
        var supportingFactIds = SortedIds((row.Before?.SupportingFactIds ?? []).Concat(row.After?.SupportingFactIds ?? []));
        var supportingEdgeIds = SortedIds((row.Before?.SupportingEdgeIds ?? []).Concat(row.After?.SupportingEdgeIds ?? []));
        return new CombinedImpactItem(
            ImpactId(row.ChangeType, classification, "path", row.PathSignature, row.DiffRuleId, PathRuleId),
            row.ChangeType,
            classification,
            Confidence(classification),
            "path",
            row.After?.SourceTransitions.FirstOrDefault() ?? row.Before?.SourceTransitions.FirstOrDefault() ?? "unknown",
            row.PathSignature,
            row.DiffRuleId,
            PathRuleId,
            EvidenceTiers.Tier2Structural,
            null,
            null,
            null,
            supportingFactIds,
            supportingEdgeIds,
            null,
            null,
            new CombinedImpactPathContext(
                includePaths
                    ? row.Before is null || row.After is null
                        ? CombinedImpactClassifications.ReachabilityChanged
                        : CombinedImpactClassifications.ReachabilityEvidenceChanged
                    : CombinedImpactClassifications.NotRequested,
                row.Before is null ? [] : [PathSummary(row.Before)],
                row.After is null ? [] : [PathSummary(row.After)],
                []),
            row.CoverageCaveats,
            row.Notes.Select(note => new CombinedImpactNote(note.Code, note.Message)).ToArray());
    }

    private static CombinedImpactPathSummary PathSummary(CombinedPathEvidence evidence)
    {
        return new CombinedImpactPathSummary(
            evidence.PathId,
            evidence.PathClassification,
            evidence.SourceTransitions,
            evidence.SupportingFactIds,
            evidence.SupportingEdgeIds,
            evidence.TerminalSurfaceMetadata);
    }

    private static string ImpactClassification(CombinedDiffRow row, CombinedDiffEvidence? evidence)
    {
        if (row.Classification == CombinedDependencyDiffClassifications.UnknownAnalysisGap)
        {
            return CombinedImpactClassifications.UnknownAnalysisGap;
        }

        if (row.Classification is CombinedDependencyDiffClassifications.NeedsReviewDiff
            or CombinedDependencyDiffClassifications.AddedWithBeforeGap
            or CombinedDependencyDiffClassifications.RemovedWithAfterGap)
        {
            return CombinedImpactClassifications.NeedsReviewImpact;
        }

        if (string.Equals(evidence?.EvidenceTier, EvidenceTiers.Tier3SyntaxOrTextual, StringComparison.Ordinal))
        {
            return CombinedImpactClassifications.NeedsReviewImpact;
        }

        if (string.Equals(evidence?.EvidenceTier, EvidenceTiers.Tier4Unknown, StringComparison.Ordinal))
        {
            return CombinedImpactClassifications.UnknownAnalysisGap;
        }

        return CombinedImpactClassifications.ProbableStaticImpact;
    }

    private static IReadOnlyList<CombinedImpactNote> NotesFor(CombinedDiffRow row)
    {
        return row.Notes
            .Select(note => new CombinedImpactNote(note.Code, note.Message))
            .OrderBy(note => note.Code, StringComparer.Ordinal)
            .ThenBy(note => note.Message, StringComparer.Ordinal)
            .ToArray();
    }

    private static CombinedImpactGap FromDiffGap(CombinedDiffGap gap)
    {
        var classification = gap.GapKind == "TruncatedByLimit"
            ? CombinedImpactClassifications.TruncatedByLimit
            : gap.Classification switch
        {
            CombinedDependencyDiffClassifications.SelectorNoMatch => CombinedImpactClassifications.SelectorNoMatch,
            CombinedDependencyDiffClassifications.NoDiffEvidence => CombinedImpactClassifications.NoImpactEvidence,
            CombinedDependencyDiffClassifications.UnknownAnalysisGap => CombinedImpactClassifications.UnknownAnalysisGap,
            CombinedDependencyDiffClassifications.NeedsReviewDiff => CombinedImpactClassifications.NeedsReviewImpact,
            _ => gap.Classification
        };
        var ruleId = gap.GapKind == "TruncatedByLimit"
            ? TruncationRuleId
            : gap.Classification switch
            {
                CombinedDependencyDiffClassifications.SelectorNoMatch => SelectorRuleId,
                CombinedDependencyDiffClassifications.NoDiffEvidence => SourceRuleId,
                _ => gap.RuleId
            };
        return new CombinedImpactGap(
            $"impact:{gap.GapId}",
            gap.GapKind,
            gap.SourceLabel,
            gap.EvidenceKind,
            ruleId,
            gap.EvidenceTier,
            classification,
            gap.Message,
            gap.SupportingFactIds,
            gap.SupportingEdgeIds);
    }

    private static IReadOnlyList<CombinedImpactItem> SortAndCapItems(IReadOnlyList<CombinedImpactItem> items, int maxItems, List<CombinedImpactGap> gaps, out bool truncated)
    {
        var sorted = items
            .OrderBy(item => ClassificationRank(item.Classification))
            .ThenBy(item => item.EvidenceKind, StringComparer.Ordinal)
            .ThenBy(item => item.SourceLabel, StringComparer.Ordinal)
            .ThenBy(item => item.StableKey, StringComparer.Ordinal)
            .ThenBy(item => item.ImpactId, StringComparer.Ordinal)
            .ToArray();
        truncated = sorted.Length > maxItems;
        if (!truncated)
        {
            return sorted;
        }

        gaps.Add(new CombinedImpactGap(
            "gap:impact:truncated:items",
            "TruncatedByLimit",
            null,
            null,
            TruncationRuleId,
            EvidenceTiers.Tier4Unknown,
            CombinedImpactClassifications.TruncatedByLimit,
            $"Impact item output was capped at {maxItems} rows from {sorted.Length} rows.",
            [],
            []));
        return sorted.Take(maxItems).ToArray();
    }

    private static IReadOnlyList<CombinedImpactGap> SortAndCapGaps(IReadOnlyList<CombinedImpactGap> gaps, int maxGaps, out bool truncated)
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

        return [.. sorted.Take(maxGaps - 1), new CombinedImpactGap("gap:impact:truncated:gaps", "TruncatedByLimit", null, null, TruncationRuleId, EvidenceTiers.Tier4Unknown, CombinedImpactClassifications.TruncatedByLimit, $"Gap output was capped at {maxGaps}.", [], [])];
    }

    private static string ReportCoverage(string diffCoverage, IReadOnlyList<CombinedImpactItem> items, IReadOnlyList<CombinedImpactGap> gaps)
    {
        if (gaps.Any(gap => gap.Classification == CombinedImpactClassifications.UnknownAnalysisGap) || diffCoverage == "UnknownAnalysisGap")
        {
            return "UnknownAnalysisGap";
        }

        if (gaps.Any(gap => gap.GapKind == "TruncatedByLimit") || diffCoverage == "ReducedCoverage" || items.Any(item => item.Classification == CombinedImpactClassifications.NeedsReviewImpact))
        {
            return "ReducedCoverage";
        }

        return "FullEvidenceAvailable";
    }

    private static string ImpactId(string changeType, string classification, string evidenceKind, string stableKey, string diffRuleId, string impactRuleId)
    {
        return $"impact:{CombinedReportHelpers.Hash($"{changeType}\n{classification}\n{evidenceKind}\n{stableKey}\n{diffRuleId}\n{impactRuleId}")}";
    }

    private static string Confidence(string classification)
    {
        return classification switch
        {
            CombinedImpactClassifications.StaticImpactEvidence => "High",
            CombinedImpactClassifications.ProbableStaticImpact
                or CombinedImpactClassifications.ReachabilityChanged
                or CombinedImpactClassifications.ReachabilityEvidenceChanged
                or CombinedImpactClassifications.ReachabilityUnchanged => "Medium",
            _ => "Low"
        };
    }

    private static int ClassificationRank(string classification)
    {
        return classification switch
        {
            CombinedImpactClassifications.StaticImpactEvidence => 0,
            CombinedImpactClassifications.ProbableStaticImpact => 1,
            CombinedImpactClassifications.ReachabilityChanged => 2,
            CombinedImpactClassifications.ReachabilityEvidenceChanged => 3,
            CombinedImpactClassifications.ReachabilityUnchanged => 4,
            CombinedImpactClassifications.NeedsReviewImpact => 5,
            CombinedImpactClassifications.UnknownAnalysisGap => 6,
            CombinedImpactClassifications.NoImpactEvidence => 7,
            CombinedImpactClassifications.SelectorNoMatch => 8,
            CombinedImpactClassifications.TruncatedByLimit => 9,
            _ => 99
        };
    }

    private static IReadOnlyList<string> SortedIds(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RenderMarkdown(CombinedChangeImpactReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Change Impact Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("- This report describes static change evidence, not runtime or business impact.");
        builder.AppendLine($"- Report coverage: `{report.ReportCoverage}`");
        builder.AppendLine($"- Path context: `{(report.Query.IncludePaths ? "path diff evidence only" : "not requested")}`");
        builder.AppendLine($"- Diff rows considered: `{report.Summary.DiffCount}`");
        builder.AppendLine($"- Impact items: `{report.Summary.ImpactItemCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        builder.AppendLine($"- Truncated: `{report.Summary.Truncated}`");
        AppendList(builder, "Coverage warnings", report.CoverageWarnings);
        builder.AppendLine();
        builder.AppendLine("## Query");
        builder.AppendLine();
        builder.AppendLine($"- Scopes: `{string.Join(",", report.Query.Scopes)}`");
        builder.AppendLine($"- Delegated diff scopes: `{string.Join(",", report.Query.DelegatedDiffScopes)}`");
        builder.AppendLine($"- Source: `{report.Query.Source ?? "any"}`");
        builder.AppendLine($"- Endpoint: `{report.Query.Endpoint ?? "any"}`");
        builder.AppendLine($"- Surface: `{report.Query.Surface ?? "any"}`");
        builder.AppendLine($"- Surface name: `{report.Query.SurfaceName ?? "any"}`");
        AppendList(builder, "Ignored selectors", report.Query.IgnoredSelectors);
        builder.AppendLine();
        builder.AppendLine("## Snapshot Sources");
        builder.AppendLine();
        AppendSnapshot(builder, report.BeforeSnapshot);
        AppendSnapshot(builder, report.AfterSnapshot);
        builder.AppendLine("## Impact Items");
        builder.AppendLine();
        AppendItems(builder, report.ImpactItems);
        builder.AppendLine("## Path Context");
        builder.AppendLine();
        AppendPathContext(builder, report);
        builder.AppendLine("## Gaps");
        builder.AppendLine();
        AppendGaps(builder, report.Gaps);
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

    private static void AppendItems(StringBuilder builder, IReadOnlyList<CombinedImpactItem> items)
    {
        if (items.Count == 0)
        {
            builder.AppendLine("No static impact items found.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Classification | Change | Kind | Source | Evidence | Rule | Tier | Span |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var item in items.Take(200))
        {
            builder.AppendLine($"| {Cell(item.Classification)} | {Cell(item.ChangeType)} | {Cell(item.EvidenceKind)} | {Cell(item.SourceLabel)} | {Cell(item.After?.DisplayName ?? item.Before?.DisplayName ?? item.StableKey)} | {Cell(item.ImpactRuleId)} | {Cell(item.EvidenceTier ?? "n/a")} | {Cell(Span(item))} |");
        }

        builder.AppendLine();
    }

    private static void AppendPathContext(StringBuilder builder, CombinedChangeImpactReport report)
    {
        if (!report.Query.IncludePaths)
        {
            builder.AppendLine("Path context was not requested.");
            builder.AppendLine();
            return;
        }

        var rows = report.ImpactItems
            .Where(item => item.PathContext.Classification != CombinedImpactClassifications.NotRequested)
            .ToArray();
        if (rows.Length == 0)
        {
            builder.AppendLine("No path-context queries were run for the returned impact items.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Classification | Kind | Source | Evidence | Before Paths | After Paths | Gaps |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var item in rows.Take(200))
        {
            builder.AppendLine($"| {Cell(item.PathContext.Classification)} | {Cell(item.EvidenceKind)} | {Cell(item.SourceLabel)} | {Cell(item.After?.DisplayName ?? item.Before?.DisplayName ?? item.StableKey)} | {item.PathContext.BeforePaths.Count} | {item.PathContext.AfterPaths.Count} | {item.PathContext.Gaps.Count} |");
        }

        builder.AppendLine();
    }

    private static void AppendGaps(StringBuilder builder, IReadOnlyList<CombinedImpactGap> gaps)
    {
        if (gaps.Count == 0)
        {
            builder.AppendLine("No gaps found.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Kind | Classification | Rule | Source | Message |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var gap in gaps.Take(200))
        {
            builder.AppendLine($"| {Cell(gap.GapKind)} | {Cell(gap.Classification)} | {Cell(gap.RuleId)} | {Cell(gap.SourceLabel ?? "n/a")} | {Cell(gap.Message)} |");
        }

        builder.AppendLine();
    }

    private static string Span(CombinedImpactItem item)
    {
        if (string.IsNullOrWhiteSpace(item.FilePath))
        {
            return "n/a";
        }

        if (item.StartLine is null)
        {
            return item.FilePath;
        }

        return $"{item.FilePath}:{item.StartLine}";
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

    private static string Cell(string? value) => CombinedReportHelpers.Cell(value);
}
