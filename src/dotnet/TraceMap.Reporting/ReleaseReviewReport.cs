using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Reduction;

namespace TraceMap.Reporting;

public sealed record ReleaseReviewOptions(
    string BeforePath,
    string AfterPath,
    string OutputPath,
    string Format = "markdown",
    string? Scope = null,
    bool IncludePaths = false,
    bool IncludeReverse = false,
    bool AllowIdentityMismatch = false,
    string? Source = null,
    string? Endpoint = null,
    string? Surface = null,
    string? SurfaceName = null,
    string? ContractDeltaPath = null,
    string? SqlSchemaDeltaPath = null,
    string? PackageDeltaPath = null,
    int MaxFindings = 100,
    int MaxSurfaceRows = 50,
    int MaxPaths = 25,
    int MaxGaps = 1000,
    int MaxChecklistItems = 50);

public sealed record ReleaseReviewResult(
    ReleaseReviewDocument Report,
    string? MarkdownPath,
    string? JsonPath)
{
    public bool HasActionableFindings => Report.Summary.RollupClassification == ReleaseReviewClassifications.ActionableStaticEvidence;
}

public sealed record ReleaseReviewDocument(
    string ReportType,
    string Version,
    string Mode,
    ReleaseReviewQuery Query,
    ReleaseReviewSnapshot BeforeSnapshot,
    ReleaseReviewSnapshot AfterSnapshot,
    ReleaseReviewSummary Summary,
    IReadOnlyList<ReleaseReviewSourceCoverage> SourceCoverage,
    ReleaseReviewSection TopChangedSurfaces,
    ReleaseReviewSection ContractImpact,
    ReleaseReviewSection ApiDtoChanges,
    ReleaseReviewSection SqlSchemaImpact,
    ReleaseReviewSection PackageImpact,
    ReleaseReviewSection PathContext,
    ReleaseReviewSection ReverseContext,
    IReadOnlyList<ReleaseReviewGap> Gaps,
    IReadOnlyList<ReleaseReviewChecklistItem> ReviewerChecklist,
    IReadOnlyList<string> Limitations);

public sealed record ReleaseReviewQuery(
    IReadOnlyList<string> Scopes,
    bool IncludePaths,
    bool IncludeReverse,
    string? Source,
    string? Endpoint,
    string? Surface,
    string? SurfaceName,
    bool ContractDeltaProvided,
    bool SqlSchemaDeltaProvided,
    bool PackageDeltaProvided,
    int MaxFindings,
    int MaxSurfaceRows,
    int MaxPaths,
    int MaxGaps,
    int MaxChecklistItems,
    IReadOnlyList<string> IgnoredSelectors,
    string Algorithm,
    string AlgorithmVersion);

public sealed record ReleaseReviewSnapshot(
    string Side,
    string IndexKind,
    IReadOnlyList<ReleaseReviewSourceInfo> Sources,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    IReadOnlyList<KeyValuePair<string, string>> ExtractorVersions);

public sealed record ReleaseReviewSourceInfo(
    string SourceLabel,
    string? Language,
    string? ScanId,
    string? CommitSha,
    string? RepositoryIdentityHash,
    string? RootPathHash,
    string Coverage,
    string BuildStatus,
    string AnalysisLevel,
    IReadOnlyList<string> GapCodes);

public sealed record ReleaseReviewSummary(
    string RollupClassification,
    string RuleId,
    int SourceCount,
    int TopChangedSurfaceCount,
    int ContractFindingCount,
    int ApiDtoFindingCount,
    int SqlSchemaFindingCount,
    int PackageFindingCount,
    int PathFindingCount,
    int ReverseFindingCount,
    int ActionableFindingCount,
    int ReviewFindingCount,
    int GapCount,
    bool Truncated,
    string Message);

public sealed record ReleaseReviewSourceCoverage(
    string SourceLabel,
    string? BeforeCommitSha,
    string? AfterCommitSha,
    string? Language,
    string BeforeCoverage,
    string AfterCoverage,
    string BeforeBuildStatus,
    string AfterBuildStatus,
    string Classification,
    string RuleId,
    string EvidenceTier,
    IReadOnlyList<string> GapIds);

public sealed record ReleaseReviewSection(
    string Status,
    IReadOnlyList<ReleaseReviewFinding> Findings,
    IReadOnlyList<ReleaseReviewGap> Gaps,
    IReadOnlyList<string> Limitations,
    int OmittedCount = 0);

public sealed record ReleaseReviewFinding(
    string FindingId,
    string Section,
    string? SourceLabel,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string? CommitSha,
    string? DisplayName,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyList<KeyValuePair<string, string>> Metadata,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> Limitations);

public sealed record ReleaseReviewGap(
    string GapId,
    string GapKind,
    string Section,
    string? SourceLabel,
    string RuleId,
    string EvidenceTier,
    string Classification,
    string Message,
    IReadOnlyList<string> SupportingFindingIds,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record ReleaseReviewChecklistItem(
    string ChecklistId,
    string Section,
    string Severity,
    string RuleId,
    string Text,
    IReadOnlyList<string> FindingIds,
    IReadOnlyList<string> GapIds);

internal sealed record ReleaseIndexInfo(string Kind, ReleaseReviewSnapshot Snapshot);

internal sealed record SingleComparableFact(
    string StableKey,
    string EvidenceHash,
    ReleaseReviewFinding Finding);

public static class ReleaseReviewStatuses
{
    public const string Available = "available";
    public const string NotRequested = "not_requested";
    public const string Unavailable = "unavailable";
    public const string Deferred = "deferred";
    public const string Truncated = "truncated";
}

public static class ReleaseReviewClassifications
{
    public const string ActionableStaticEvidence = nameof(ActionableStaticEvidence);
    public const string ReviewRecommended = nameof(ReviewRecommended);
    public const string NoActionableEvidence = nameof(NoActionableEvidence);
    public const string PartialAnalysis = nameof(PartialAnalysis);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string TruncatedByLimit = nameof(TruncatedByLimit);
}

public static class ReleaseReviewReporter
{
    private const string ReportType = "release-review";
    private const string Version = "1.0";
    private const string Algorithm = "release-review-composition";
    private const string AlgorithmVersion = "1.0";
    private const string RollupRuleId = "release.review.rollup.v1";
    private const string ChecklistRuleId = "release.review.checklist.v1";
    private const string SourceRuleId = "release.review.source.v1";
    private const string SectionRuleId = "release.review.section.v1";
    private const string SelectorRuleId = "release.review.selector.v1";
    private const string TruncationRuleId = "release.review.truncation.v1";

    private static readonly HashSet<string> ValidScopes = new(StringComparer.Ordinal)
    {
        "all",
        "sources",
        "coverage",
        "surfaces",
        "contracts",
        "api-dto",
        "sql-schema",
        "packages",
        "paths",
        "reverse",
        "gaps",
        "checklist"
    };

    private static readonly HashSet<string> StrongClassifications = new(StringComparer.Ordinal)
    {
        "DefiniteImpact",
        "ProbableImpact",
        CombinedImpactClassifications.StaticImpactEvidence,
        CombinedImpactClassifications.ProbableStaticImpact,
        CombinedDependencyDiffClassifications.Added,
        CombinedDependencyDiffClassifications.Removed,
        CombinedDependencyDiffClassifications.ChangedEvidence
    };

    private static readonly IReadOnlyList<string> DefaultLimitations =
    [
        "Release review is static evidence context, not approval, CI policy, runtime risk prediction, release readiness, production usage, or deployment verification.",
        "Underlying scanner coverage, source identity, missing precision tables, and unavailable workflows limit conclusions.",
        "Path and reverse context are bounded static graph evidence and are not complete runtime reachability.",
        "SQL, HTTP, package, serializer, and config evidence does not prove runtime execution, external service behavior, schema existence, compatibility, or branch feasibility.",
        "Reports omit or hash raw SQL, source snippets, config values, connection strings, raw URLs, local absolute paths, and secret-looking values."
    ];

    private static readonly IReadOnlyList<string> FutureWorkflowLimitations =
    [
        "This section depends on a future or unavailable workflow and is included only as an explicit gap."
    ];

    public static async Task<ReleaseReviewResult> WriteAsync(ReleaseReviewOptions options, CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(options, cancellationToken);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "release-review");
        var (markdownPath, jsonPath) = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "release-review.md",
            "release-review.json",
            report,
            RenderMarkdown,
            CombinedDependencyReporter.JsonOptions,
            cancellationToken);
        return new ReleaseReviewResult(report, markdownPath, jsonPath);
    }

    public static async Task<ReleaseReviewDocument> BuildReportAsync(ReleaseReviewOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        ValidateReadableFile(options.ContractDeltaPath, "--contract-delta");
        ValidateReadableFile(options.SqlSchemaDeltaPath, "--sql-schema-delta");
        ValidateReadableFile(options.PackageDeltaPath, "--package-delta");

        var scopes = NormalizeScopes(options.Scope);
        var ignoredSelectors = IgnoredSelectors(options, scopes);
        var beforeInfo = await ReadIndexInfoAsync(options.BeforePath, "before", cancellationToken);
        var afterInfo = await ReadIndexInfoAsync(options.AfterPath, "after", cancellationToken);
        if (!string.Equals(beforeInfo.Kind, afterInfo.Kind, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"release-review mixed input modes are deferred: before is {beforeInfo.Kind}, after is {afterInfo.Kind}.");
        }

        var mode = beforeInfo.Kind == "combined" ? "ReleaseReviewCombinedV1" : "ReleaseReviewSingleV1";
        var gaps = new List<ReleaseReviewGap>();
        gaps.AddRange(SourceIdentityAndCoverageGaps(beforeInfo.Snapshot, afterInfo.Snapshot));
        gaps.AddRange(ignoredSelectors.Select((message, index) => Gap(
            "selector",
            "SelectorIgnored",
            "summary",
            null,
            SelectorRuleId,
            ReleaseReviewClassifications.PartialAnalysis,
            message,
            metadata: [new KeyValuePair<string, string>("selectorIndex", index.ToString())])));

        var sourceCoverage = PairSourceCoverage(beforeInfo.Snapshot, afterInfo.Snapshot, gaps);
        ReleaseReviewSection topChangedSurfaces;
        ReleaseReviewSection pathContext;
        ReleaseReviewSection reverseContext;
        if (beforeInfo.Kind == "combined")
        {
            var impact = await BuildCombinedImpactAsync(options, scopes, cancellationToken);
            topChangedSurfaces = BuildTopChangedSurfacesSection(impact, options.MaxSurfaceRows);
            pathContext = BuildPathContextSection(impact, options.IncludePaths, options.MaxPaths);
            reverseContext = await BuildReverseContextSectionAsync(options, cancellationToken);
            gaps.AddRange(topChangedSurfaces.Gaps);
            gaps.AddRange(pathContext.Gaps);
            gaps.AddRange(reverseContext.Gaps);
        }
        else
        {
            topChangedSurfaces = await BuildSingleSurfaceDiffSectionAsync(options, scopes, cancellationToken);
            pathContext = BuildSingleUnavailableContextSection("pathContext", options.IncludePaths, "Path context requires combined indexes in release-review v1.");
            reverseContext = BuildSingleUnavailableContextSection("reverseContext", options.IncludeReverse, "Reverse context requires combined indexes in release-review v1.");
            gaps.AddRange(topChangedSurfaces.Gaps);
            gaps.AddRange(pathContext.Gaps);
            gaps.AddRange(reverseContext.Gaps);
        }

        var contractImpact = await BuildContractImpactSectionAsync(options, cancellationToken);
        var apiDtoChanges = BuildUnavailableSection(
            "apiDtoChanges",
            "API/DTO contract diff workflow is not implemented in this release-review slice.",
            requested: scopes.Contains("api-dto", StringComparer.Ordinal));
        var sqlSchemaImpact = BuildSqlSchemaSection(options, scopes);
        var packageImpact = BuildPackageSection(options, scopes, topChangedSurfaces);
        gaps.AddRange(contractImpact.Gaps);
        gaps.AddRange(apiDtoChanges.Gaps);
        gaps.AddRange(sqlSchemaImpact.Gaps);
        gaps.AddRange(packageImpact.Gaps);

        var allFindings = new[]
            {
                topChangedSurfaces,
                contractImpact,
                apiDtoChanges,
                sqlSchemaImpact,
                packageImpact,
                pathContext,
                reverseContext
            }
            .SelectMany(section => section.Findings)
            .OrderBy(FindingSeverityRank)
            .ThenBy(finding => finding.Section, StringComparer.Ordinal)
            .ThenBy(finding => finding.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(finding => finding.FindingId, StringComparer.Ordinal)
            .ToArray();

        var cappedFindings = ApplyFindingCap(allFindings, options.MaxFindings, gaps);
        topChangedSurfaces = FilterSectionFindings(topChangedSurfaces, cappedFindings);
        contractImpact = FilterSectionFindings(contractImpact, cappedFindings);
        apiDtoChanges = FilterSectionFindings(apiDtoChanges, cappedFindings);
        sqlSchemaImpact = FilterSectionFindings(sqlSchemaImpact, cappedFindings);
        packageImpact = FilterSectionFindings(packageImpact, cappedFindings);
        pathContext = FilterSectionFindings(pathContext, cappedFindings);
        reverseContext = FilterSectionFindings(reverseContext, cappedFindings);

        var cappedGaps = gaps
            .DistinctBy(gap => gap.GapId)
            .OrderBy(GapSeverityRank)
            .ThenBy(gap => gap.Section, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .Take(options.MaxGaps)
            .ToArray();
        var truncated = gaps.Count > cappedGaps.Length
            || topChangedSurfaces.Status == ReleaseReviewStatuses.Truncated
            || cappedFindings.Length < allFindings.Length;
        var summary = BuildSummary(
            beforeInfo.Snapshot,
            topChangedSurfaces,
            contractImpact,
            apiDtoChanges,
            sqlSchemaImpact,
            packageImpact,
            pathContext,
            reverseContext,
            cappedGaps,
            truncated);
        var checklist = BuildChecklist(summary, cappedFindings, cappedGaps, options.MaxChecklistItems);

        return new ReleaseReviewDocument(
            ReportType,
            Version,
            mode,
            new ReleaseReviewQuery(
                scopes,
                options.IncludePaths,
                options.IncludeReverse,
                options.Source,
                options.Endpoint,
                options.Surface,
                options.SurfaceName,
                !string.IsNullOrWhiteSpace(options.ContractDeltaPath),
                !string.IsNullOrWhiteSpace(options.SqlSchemaDeltaPath),
                !string.IsNullOrWhiteSpace(options.PackageDeltaPath),
                options.MaxFindings,
                options.MaxSurfaceRows,
                options.MaxPaths,
                options.MaxGaps,
                options.MaxChecklistItems,
                ignoredSelectors,
                Algorithm,
                AlgorithmVersion),
            beforeInfo.Snapshot,
            afterInfo.Snapshot,
            summary,
            sourceCoverage,
            topChangedSurfaces,
            contractImpact,
            apiDtoChanges,
            sqlSchemaImpact,
            packageImpact,
            pathContext,
            reverseContext,
            cappedGaps,
            checklist,
            DefaultLimitations);
    }

    private static CombinedChangeImpactOptions ToImpactOptions(ReleaseReviewOptions options, IReadOnlyList<string> scopes)
    {
        var impactScope = ImpactScope(scopes, options.IncludePaths);
        return new CombinedChangeImpactOptions(
            options.BeforePath,
            options.AfterPath,
            options.OutputPath,
            "json",
            impactScope,
            options.IncludePaths,
            options.AllowIdentityMismatch,
            false,
            options.Source,
            options.Endpoint,
            options.Surface,
            options.SurfaceName,
            options.MaxFindings,
            Math.Max(1, options.MaxPaths),
            Math.Max(1, options.MaxPaths),
            8,
            10000,
            options.MaxGaps);
    }

    private static async Task<CombinedChangeImpactReport> BuildCombinedImpactAsync(ReleaseReviewOptions options, IReadOnlyList<string> scopes, CancellationToken cancellationToken)
    {
        return await CombinedChangeImpactReporter.BuildReportAsync(ToImpactOptions(options, scopes), cancellationToken);
    }

    private static ReleaseReviewSection BuildTopChangedSurfacesSection(CombinedChangeImpactReport impact, int maxSurfaceRows)
    {
        var findings = impact.ImpactItems
            .Where(item => item.EvidenceKind is "endpoint" or "surface" or "edge" or "source" or "coverage")
            .Select(FromImpactItem)
            .OrderBy(FindingSeverityRank)
            .ThenBy(finding => finding.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(finding => finding.DisplayName ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(finding => finding.FindingId, StringComparer.Ordinal)
            .ToArray();
        var capped = findings.Take(maxSurfaceRows).ToArray();
        var gaps = impact.Gaps.Select(gap => FromImpactGap(gap, "topChangedSurfaces")).ToArray();
        var omitted = findings.Length - capped.Length;
        var status = omitted > 0 ? ReleaseReviewStatuses.Truncated : ReleaseReviewStatuses.Available;
        if (omitted > 0)
        {
            gaps = gaps.Append(Gap(
                "truncation",
                "TruncatedByLimit",
                "topChangedSurfaces",
                null,
                TruncationRuleId,
                ReleaseReviewClassifications.TruncatedByLimit,
                $"Top changed surfaces omitted {omitted} rows because --max-surface-rows was reached.")).ToArray();
        }

        return new ReleaseReviewSection(status, capped, gaps, ["Top changed surfaces reuse combined change-impact classifications and coverage caveats."], omitted);
    }

    private static ReleaseReviewSection BuildPathContextSection(CombinedChangeImpactReport impact, bool includePaths, int maxPaths)
    {
        if (!includePaths)
        {
            return new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["Path context is off by default; pass --include-paths to request bounded path evidence."]);
        }

        var pathFindings = impact.ImpactItems
            .Where(item => item.PathContext.BeforePaths.Count + item.PathContext.AfterPaths.Count > 0)
            .SelectMany(item => item.PathContext.BeforePaths.Concat(item.PathContext.AfterPaths).Select(path => FromImpactPath(item, path)))
            .OrderBy(FindingSeverityRank)
            .ThenBy(finding => finding.FindingId, StringComparer.Ordinal)
            .ToArray();
        var capped = pathFindings.Take(maxPaths).ToArray();
        var gaps = impact.ImpactItems
            .SelectMany(item => item.PathContext.Gaps)
            .Select(gap => FromImpactGap(gap, "pathContext"))
            .ToList();
        var omitted = pathFindings.Length - capped.Length;
        var status = omitted > 0 ? ReleaseReviewStatuses.Truncated : ReleaseReviewStatuses.Available;
        if (omitted > 0)
        {
            gaps.Add(Gap(
                "truncation",
                "TruncatedByLimit",
                "pathContext",
                null,
                TruncationRuleId,
                ReleaseReviewClassifications.TruncatedByLimit,
                $"Path context omitted {omitted} rows because --max-paths was reached."));
        }

        return new ReleaseReviewSection(status, capped, gaps, ["Path context is bounded static evidence and is not complete runtime reachability."], omitted);
    }

    private static async Task<ReleaseReviewSection> BuildReverseContextSectionAsync(ReleaseReviewOptions options, CancellationToken cancellationToken)
    {
        if (!options.IncludeReverse)
        {
            return new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["Reverse context is off by default; pass --include-reverse to request bounded reverse evidence."]);
        }

        var reverse = await CombinedReverseReporter.BuildReportAsync(new CombinedReverseOptions(
            options.AfterPath,
            options.OutputPath,
            "json",
            options.Source,
            options.Surface,
            options.SurfaceName,
            "endpoints",
            8,
            10000,
            options.MaxSurfaceRows,
            options.MaxFindings,
            Math.Max(1, options.MaxPaths),
            options.MaxGaps), cancellationToken);
        var findings = reverse.ReverseRoots.Select(FromReverseRoot)
            .Concat(reverse.Paths.Select(FromReversePath))
            .OrderBy(FindingSeverityRank)
            .ThenBy(finding => finding.FindingId, StringComparer.Ordinal)
            .Take(options.MaxPaths)
            .ToArray();
        var gaps = reverse.Gaps.Select(gap => FromReverseGap(gap)).ToArray();
        var omitted = reverse.ReverseRoots.Count + reverse.Paths.Count - findings.Length;
        var status = omitted > 0 || reverse.Summary.Truncated ? ReleaseReviewStatuses.Truncated : ReleaseReviewStatuses.Available;
        if (omitted > 0)
        {
            gaps = gaps.Append(Gap(
                "truncation",
                "TruncatedByLimit",
                "reverseContext",
                null,
                TruncationRuleId,
                ReleaseReviewClassifications.TruncatedByLimit,
                $"Reverse context omitted {omitted} rows because --max-paths was reached.")).ToArray();
        }

        return new ReleaseReviewSection(status, findings, gaps, ["Reverse context is bounded static evidence from the after snapshot only."], omitted);
    }

    private static ReleaseReviewSection BuildSingleUnavailableContextSection(string section, bool requested, string message)
    {
        if (!requested)
        {
            return new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["Context is off by default for release review."]);
        }

        var gap = Gap("section", "UnsupportedMode", section, null, SectionRuleId, ReleaseReviewClassifications.PartialAnalysis, message);
        return new ReleaseReviewSection(ReleaseReviewStatuses.Unavailable, [], [gap], ["Single-index path and reverse context are deferred in release-review v1."]);
    }

    private static async Task<ReleaseReviewSection> BuildSingleSurfaceDiffSectionAsync(ReleaseReviewOptions options, IReadOnlyList<string> scopes, CancellationToken cancellationToken)
    {
        if (!ScopeEnabled(scopes, "surfaces"))
        {
            return new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["Surface diff scope was not requested."]);
        }

        var before = await ReadSingleComparableFactsAsync(options.BeforePath, "before", options.Source, cancellationToken);
        var after = await ReadSingleComparableFactsAsync(options.AfterPath, "after", options.Source, cancellationToken);
        var findings = DiffSingleFacts(before, after)
            .OrderBy(FindingSeverityRank)
            .ThenBy(finding => finding.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(finding => finding.FindingId, StringComparer.Ordinal)
            .ToArray();
        var capped = findings.Take(options.MaxSurfaceRows).ToArray();
        var gaps = new List<ReleaseReviewGap>();
        var omitted = findings.Length - capped.Length;
        var status = omitted > 0 ? ReleaseReviewStatuses.Truncated : ReleaseReviewStatuses.Available;
        if (omitted > 0)
        {
            gaps.Add(Gap(
                "truncation",
                "TruncatedByLimit",
                "topChangedSurfaces",
                null,
                TruncationRuleId,
                ReleaseReviewClassifications.TruncatedByLimit,
                $"Single-index surface diff omitted {omitted} rows because --max-surface-rows was reached."));
        }

        return new ReleaseReviewSection(status, capped, gaps, ["Single-index surface diffs compare indexed fact evidence only, not source text or runtime behavior."], omitted);
    }

    private static async Task<ReleaseReviewSection> BuildContractImpactSectionAsync(ReleaseReviewOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ContractDeltaPath))
        {
            return new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["No --contract-delta input was provided."]);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"tracemap-release-contract-{Guid.NewGuid():N}");
        try
        {
            var reduce = await ContractDeltaReducer.ReduceAsync(new ReduceOptions(
                options.AfterPath,
                options.ContractDeltaPath,
                tempDir,
                "json",
                "all",
                options.Source,
                null,
                null,
                options.Surface,
                options.Endpoint,
                false,
                false,
                false,
                options.MaxFindings,
                500,
                5,
                50,
                options.MaxGaps), cancellationToken);
            var findings = reduce.Report.Findings.Select(FromContractFinding)
                .OrderBy(FindingSeverityRank)
                .ThenBy(finding => finding.FindingId, StringComparer.Ordinal)
                .ToArray();
            var gaps = reduce.Report.Gaps.Select(FromContractGap).ToArray();
            return new ReleaseReviewSection(ReleaseReviewStatuses.Available, findings, gaps, reduce.Report.Limitations);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static ReleaseReviewSection BuildSqlSchemaSection(ReleaseReviewOptions options, IReadOnlyList<string> scopes)
    {
        if (string.IsNullOrWhiteSpace(options.SqlSchemaDeltaPath) && !ScopeEnabled(scopes, "sql-schema"))
        {
            return new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["No --sql-schema-delta input was provided."]);
        }

        var gap = Gap(
            "section",
            "WorkflowUnavailable",
            "sqlSchemaImpact",
            options.Source,
            SectionRuleId,
            string.IsNullOrWhiteSpace(options.SqlSchemaDeltaPath) ? ReleaseReviewClassifications.PartialAnalysis : ReleaseReviewClassifications.UnknownAnalysisGap,
            "SQL/schema change impact workflow is not implemented in this release-review slice.");
        return new ReleaseReviewSection(ReleaseReviewStatuses.Unavailable, [], [gap], FutureWorkflowLimitations);
    }

    private static ReleaseReviewSection BuildPackageSection(ReleaseReviewOptions options, IReadOnlyList<string> scopes, ReleaseReviewSection topChangedSurfaces)
    {
        var packageFindings = topChangedSurfaces.Findings
            .Where(finding => finding.Metadata.Any(pair => pair.Key == "surfaceKind" && pair.Value == "package-config")
                || string.Equals(finding.Metadata.FirstOrDefault(pair => pair.Key == "factType").Value, FactTypes.PackageReferenced, StringComparison.Ordinal))
            .OrderBy(FindingSeverityRank)
            .ThenBy(finding => finding.FindingId, StringComparer.Ordinal)
            .ToArray();
        if (packageFindings.Length > 0)
        {
            var gaps = new List<ReleaseReviewGap>();
            var status = ReleaseReviewStatuses.Available;
            if (!string.IsNullOrWhiteSpace(options.PackageDeltaPath))
            {
                status = ReleaseReviewStatuses.Deferred;
                gaps.Add(Gap(
                    "section",
                    "WorkflowDeferred",
                    "packageImpact",
                    options.Source,
                    SectionRuleId,
                    ReleaseReviewClassifications.PartialAnalysis,
                    "--package-delta was received, but package-upgrade impact is deferred; indexed package evidence is shown without compatibility claims."));
            }

            return new ReleaseReviewSection(status, packageFindings, gaps, ["Package evidence is static manifest/import/surface evidence, not compatibility, vulnerability, or lockfile-resolution proof."]);
        }

        if (string.IsNullOrWhiteSpace(options.PackageDeltaPath) && !ScopeEnabled(scopes, "packages"))
        {
            return new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["No package delta input was provided and no package surface evidence was selected."]);
        }

        var gap = Gap(
            "section",
            string.IsNullOrWhiteSpace(options.PackageDeltaPath) ? "WorkflowDeferred" : "WorkflowUnavailable",
            "packageImpact",
            options.Source,
            SectionRuleId,
            string.IsNullOrWhiteSpace(options.PackageDeltaPath) ? ReleaseReviewClassifications.PartialAnalysis : ReleaseReviewClassifications.UnknownAnalysisGap,
            "Package-upgrade impact workflow is deferred; release review does not claim package compatibility or vulnerability impact.");
        return new ReleaseReviewSection(ReleaseReviewStatuses.Deferred, [], [gap], FutureWorkflowLimitations);
    }

    private static ReleaseReviewSection BuildUnavailableSection(string section, string message, bool requested)
    {
        var classification = requested ? ReleaseReviewClassifications.UnknownAnalysisGap : ReleaseReviewClassifications.PartialAnalysis;
        var gap = Gap("section", "WorkflowUnavailable", section, null, SectionRuleId, classification, message);
        return new ReleaseReviewSection(ReleaseReviewStatuses.Unavailable, [], [gap], FutureWorkflowLimitations);
    }

    private static ReleaseReviewSummary BuildSummary(
        ReleaseReviewSnapshot before,
        ReleaseReviewSection topChangedSurfaces,
        ReleaseReviewSection contractImpact,
        ReleaseReviewSection apiDtoChanges,
        ReleaseReviewSection sqlSchemaImpact,
        ReleaseReviewSection packageImpact,
        ReleaseReviewSection pathContext,
        ReleaseReviewSection reverseContext,
        IReadOnlyList<ReleaseReviewGap> gaps,
        bool truncated)
    {
        var allFindings = new[]
            {
                topChangedSurfaces,
                contractImpact,
                apiDtoChanges,
                sqlSchemaImpact,
                packageImpact,
                pathContext,
                reverseContext
            }
            .SelectMany(section => section.Findings)
            .ToArray();
        var actionableCount = allFindings.Count(IsActionableFinding);
        var reviewCount = allFindings.Length - actionableCount;
        var rollup = SelectRollup(gaps, allFindings, truncated);
        var message = rollup switch
        {
            ReleaseReviewClassifications.ActionableStaticEvidence => "Actionable static evidence is present; review the cited findings and limitations.",
            ReleaseReviewClassifications.ReviewRecommended => "Review-tier static evidence is present; no stronger conclusion is made.",
            ReleaseReviewClassifications.NoActionableEvidence => "No actionable static findings under requested scope.",
            ReleaseReviewClassifications.PartialAnalysis => "No actionable static findings under requested scope; gaps remain.",
            ReleaseReviewClassifications.SelectorNoMatch => "Selectors matched no static evidence under requested scope.",
            ReleaseReviewClassifications.UnknownAnalysisGap => "Analysis gaps prevent a credible conclusion for at least one requested section.",
            ReleaseReviewClassifications.TruncatedByLimit => "Report output was truncated by configured caps.",
            _ => "Release review completed with static evidence and limitations."
        };
        return new ReleaseReviewSummary(
            rollup,
            RollupRuleId,
            before.Sources.Count,
            topChangedSurfaces.Findings.Count,
            contractImpact.Findings.Count,
            apiDtoChanges.Findings.Count,
            sqlSchemaImpact.Findings.Count,
            packageImpact.Findings.Count,
            pathContext.Findings.Count,
            reverseContext.Findings.Count,
            actionableCount,
            reviewCount,
            gaps.Count,
            truncated,
            message);
    }

    private static IReadOnlyList<ReleaseReviewChecklistItem> BuildChecklist(
        ReleaseReviewSummary summary,
        IReadOnlyList<ReleaseReviewFinding> findings,
        IReadOnlyList<ReleaseReviewGap> gaps,
        int maxChecklistItems)
    {
        var items = new List<ReleaseReviewChecklistItem>();
        foreach (var gap in gaps)
        {
            var severity = gap.Classification switch
            {
                ReleaseReviewClassifications.UnknownAnalysisGap or ReleaseReviewClassifications.TruncatedByLimit => "must_review",
                ReleaseReviewClassifications.SelectorNoMatch or ReleaseReviewClassifications.NoActionableEvidence => "informational",
                _ => "should_review"
            };
            items.Add(new ReleaseReviewChecklistItem(
                StableId("checklist", gap.Section, gap.GapId),
                gap.Section,
                severity,
                ChecklistRuleId,
                $"{gap.Section}: {gap.GapKind} - {gap.Message}",
                [],
                [gap.GapId]));
        }

        foreach (var finding in findings)
        {
            var severity = IsActionableFinding(finding) ? "must_review" : "should_review";
            items.Add(new ReleaseReviewChecklistItem(
                StableId("checklist", finding.Section, finding.FindingId),
                finding.Section,
                severity,
                ChecklistRuleId,
                $"{finding.Section}: review {finding.Classification} evidence `{finding.FindingId}`.",
                [finding.FindingId],
                []));
        }

        if (items.Count == 0)
        {
            items.Add(new ReleaseReviewChecklistItem(
                StableId("checklist", "summary", summary.RollupClassification),
                "summary",
                "informational",
                ChecklistRuleId,
                $"Summary rollup is {summary.RollupClassification}; no finding or gap checklist items were generated.",
                [],
                []));
        }

        return items
            .OrderBy(SeverityRank)
            .ThenBy(item => item.Section, StringComparer.Ordinal)
            .ThenBy(item => item.ChecklistId, StringComparer.Ordinal)
            .Take(maxChecklistItems)
            .ToArray();
    }

    private static ReleaseReviewFinding FromImpactItem(CombinedImpactItem item)
    {
        var evidence = item.After ?? item.Before;
        return new ReleaseReviewFinding(
            StableId("finding", "topChangedSurfaces", item.ImpactId),
            "topChangedSurfaces",
            item.SourceLabel,
            item.Classification,
            item.ImpactRuleId,
            item.EvidenceTier ?? EvidenceTiers.Tier4Unknown,
            evidence?.CommitSha,
            evidence?.DisplayName,
            SafeReportPath(evidence?.FilePath),
            evidence?.StartLine,
            evidence?.EndLine,
            CombinedReportHelpers.SortedMetadata([
                Pair("changeType", item.ChangeType),
                Pair("evidenceKind", item.EvidenceKind),
                Pair("stableKeyHash", CombinedReportHelpers.Hash(item.StableKey, 16)),
                .. SafeMetadata(evidence?.SafeMetadata ?? []).Select(pair => Pair(pair.Key, pair.Value))
            ]),
            item.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            item.SupportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            item.Notes.Select(note => $"{note.Code}: {note.Message}").OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static ReleaseReviewFinding FromImpactPath(CombinedImpactItem item, CombinedImpactPathSummary path)
    {
        return new ReleaseReviewFinding(
            StableId("finding", "pathContext", item.ImpactId, path.PathId),
            "pathContext",
            item.SourceLabel,
            path.Classification,
            "combined.impact.path-context.v1",
            EvidenceTiers.Tier2Structural,
            (item.After ?? item.Before)?.CommitSha,
            path.PathId,
            null,
            null,
            null,
            CombinedReportHelpers.SortedMetadata([
                Pair("changeType", item.ChangeType),
                Pair("pathId", path.PathId),
                Pair("sourceTransitions", string.Join(">", path.SourceTransitions.OrderBy(value => value, StringComparer.Ordinal))),
                .. SafeMetadata(path.TerminalSurfaceMetadata).Select(pair => Pair(pair.Key, pair.Value))
            ]),
            path.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            path.SupportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ["Path context is bounded static evidence."]);
    }

    private static ReleaseReviewFinding FromReverseRoot(CombinedReverseRoot root)
    {
        return new ReleaseReviewFinding(
            StableId("finding", "reverseContext", root.RootId),
            "reverseContext",
            root.SourceLabel,
            root.Classification,
            root.RuleIds.OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? "combined.reverse.root.v1",
            root.EvidenceTiers.OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? EvidenceTiers.Tier4Unknown,
            null,
            root.DisplayName,
            null,
            null,
            null,
            CombinedReportHelpers.SortedMetadata([
                Pair("rootKind", root.RootKind),
                Pair("stableKeyHash", CombinedReportHelpers.Hash(root.StableKey, 16))
            ]),
            root.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            root.SupportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            root.CoverageCaveats.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static ReleaseReviewFinding FromReversePath(CombinedReversePath path)
    {
        return new ReleaseReviewFinding(
            StableId("finding", "reverseContext", path.PathId),
            "reverseContext",
            null,
            path.Classification,
            path.RuleIds.OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? "combined.reverse.path.v1",
            path.EvidenceTiers.OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? EvidenceTiers.Tier4Unknown,
            null,
            path.PathId,
            null,
            null,
            null,
            CombinedReportHelpers.SortedMetadata([
                Pair("rootId", path.RootId),
                Pair("surfaceId", path.SurfaceId),
                Pair("depth", path.Depth.ToString())
            ]),
            path.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            path.SupportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ["Reverse paths are bounded static evidence."]);
    }

    private static ReleaseReviewFinding FromContractFinding(ImpactFinding finding)
    {
        var evidence = finding.Evidence
            .OrderBy(row => row.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.StartLine)
            .ThenBy(row => row.FactId, StringComparer.Ordinal)
            .FirstOrDefault();
        return new ReleaseReviewFinding(
            StableId("finding", "contractImpact", finding.FindingId),
            "contractImpact",
            finding.SourceLabel,
            finding.Classification,
            finding.RuleId,
            finding.EvidenceTier,
            evidence?.CommitSha,
            finding.Element,
            SafeReportPath(evidence?.FilePath),
            evidence?.StartLine,
            evidence?.EndLine,
            CombinedReportHelpers.SortedMetadata([
                Pair("changeId", finding.ChangeId),
                Pair("changeKind", finding.ChangeKind),
                Pair("changeType", finding.ChangeType),
                Pair("confidence", finding.Confidence)
            ]),
            finding.Evidence.Select(row => row.FactId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            [],
            finding.Limitations.Concat(finding.Warnings).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static ReleaseReviewGap FromImpactGap(CombinedImpactGap gap, string section)
    {
        return new ReleaseReviewGap(
            StableId("gap", section, gap.GapId),
            gap.GapKind,
            section,
            gap.SourceLabel,
            gap.RuleId,
            gap.EvidenceTier,
            MapGapClassification(gap.Classification),
            gap.Message,
            [],
            gap.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            gap.SupportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            CombinedReportHelpers.SortedMetadata([
                Pair("evidenceKind", gap.EvidenceKind)
            ]));
    }

    private static ReleaseReviewGap FromReverseGap(CombinedReverseGap gap)
    {
        return new ReleaseReviewGap(
            StableId("gap", "reverseContext", gap.GapId),
            gap.GapKind,
            "reverseContext",
            gap.SourceLabel,
            gap.RuleId,
            gap.EvidenceTier,
            MapGapClassification(gap.Classification),
            gap.Message,
            [],
            gap.CombinedFactId is null ? [] : [gap.CombinedFactId],
            [],
            SafeMetadata(gap.Metadata));
    }

    private static ReleaseReviewGap FromContractGap(ContractDeltaImpactGap gap)
    {
        return new ReleaseReviewGap(
            StableId("gap", "contractImpact", gap.GapId),
            gap.GapKind,
            "contractImpact",
            gap.SourceLabel,
            gap.RuleId,
            gap.EvidenceTier,
            MapGapClassification(gap.Classification),
            gap.Message,
            [],
            gap.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            [],
            CombinedReportHelpers.SortedMetadata([
                Pair("changeId", gap.ChangeId)
            ]));
    }

    private static async Task<ReleaseIndexInfo> ReadIndexInfoAsync(string path, string side, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ReadOnlyConnectionString(path));
        await connection.OpenAsync(cancellationToken);
        var hasScanManifest = await TableExistsAsync(connection, "scan_manifest", cancellationToken);
        var hasFacts = await TableExistsAsync(connection, "facts", cancellationToken);
        var hasSources = await TableExistsAsync(connection, "index_sources", cancellationToken);
        var hasCombinedFacts = await TableExistsAsync(connection, "combined_facts", cancellationToken);
        if (hasSources && hasCombinedFacts)
        {
            return new ReleaseIndexInfo("combined", await ReadCombinedSnapshotAsync(connection, side, cancellationToken));
        }

        if (hasScanManifest && hasFacts)
        {
            return new ReleaseIndexInfo("single", await ReadSingleSnapshotAsync(connection, side, cancellationToken));
        }

        var missing = !hasScanManifest && !hasSources ? "scan_manifest/index_sources" : !hasFacts && !hasCombinedFacts ? "facts/combined_facts" : "TraceMap index tables";
        throw new InvalidDataException($"{side} input is not a valid TraceMap index; missing {missing}.");
    }

    private static async Task<ReleaseReviewSnapshot> ReadCombinedSnapshotAsync(SqliteConnection connection, string side, CancellationToken cancellationToken)
    {
        var sources = new List<ReleaseReviewSourceInfo>();
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
            var manifest = DeserializeManifest(reader.GetString(11));
            var label = reader.GetString(0);
            var scannerVersion = reader.GetString(10);
            extractorVersions[$"{label}:{scannerVersion}"] = scannerVersion;
            var gaps = manifest?.KnownGaps ?? [];
            var analysisLevel = reader.GetString(8);
            var buildStatus = reader.GetString(9);
            sources.Add(new ReleaseReviewSourceInfo(
                label,
                reader.IsDBNull(1) ? InferLanguage(scannerVersion) : reader.GetString(1),
                reader.GetString(2),
                NullIfUnknown(reader.GetString(3)),
                RepositoryIdentityHash(reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(4)),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                CoverageFrom(analysisLevel, buildStatus, gaps),
                buildStatus,
                analysisLevel,
                gaps.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
        }

        var warnings = CoverageWarnings(sources);
        return new ReleaseReviewSnapshot(side, "combined", sources, warnings.Count == 0 ? "Full" : "Reduced", warnings, extractorVersions.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
    }

    private static async Task<ReleaseReviewSnapshot> ReadSingleSnapshotAsync(SqliteConnection connection, string side, CancellationToken cancellationToken)
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

        var manifest = DeserializeManifest(reader.GetString(6));
        var gaps = manifest?.KnownGaps ?? [];
        var scannerVersion = reader.GetString(3);
        var source = new ReleaseReviewSourceInfo(
            "single",
            InferLanguage(scannerVersion),
            reader.GetString(0),
            NullIfUnknown(reader.GetString(2)),
            RepositoryIdentityHash(manifest?.RemoteUrl, reader.GetString(1)),
            manifest?.ScanRootPathHash,
            CoverageFrom(reader.GetString(4), reader.GetString(5), gaps),
            reader.GetString(5),
            reader.GetString(4),
            gaps.OrderBy(value => value, StringComparer.Ordinal).ToArray());
        var warnings = CoverageWarnings([source]);
        return new ReleaseReviewSnapshot(side, "single", [source], warnings.Count == 0 ? "Full" : "Reduced", warnings, [new KeyValuePair<string, string>($"single:{scannerVersion}", scannerVersion)]);
    }

    private static IReadOnlyList<ReleaseReviewGap> SourceIdentityAndCoverageGaps(ReleaseReviewSnapshot before, ReleaseReviewSnapshot after)
    {
        var gaps = new List<ReleaseReviewGap>();
        foreach (var source in before.Sources.Concat(after.Sources).GroupBy(source => source.SourceLabel, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var beforeSource = before.Sources.FirstOrDefault(row => row.SourceLabel == source.Key);
            var afterSource = after.Sources.FirstOrDefault(row => row.SourceLabel == source.Key);
            if (beforeSource is null || afterSource is null)
            {
                gaps.Add(Gap("source", "SourceOnlyOnOneSide", "sourceCoverage", source.Key, SourceRuleId, ReleaseReviewClassifications.PartialAnalysis, $"Source `{source.Key}` exists on only one side of the comparison."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(beforeSource.CommitSha) || string.IsNullOrWhiteSpace(afterSource.CommitSha))
            {
                gaps.Add(Gap("source", "UnknownCommitSha", "sourceCoverage", source.Key, SourceRuleId, ReleaseReviewClassifications.PartialAnalysis, $"Source `{source.Key}` has an unknown commit SHA on one or both sides."));
            }

            if (!string.Equals(beforeSource.RepositoryIdentityHash, afterSource.RepositoryIdentityHash, StringComparison.Ordinal)
                || !string.Equals(beforeSource.Language, afterSource.Language, StringComparison.OrdinalIgnoreCase))
            {
                gaps.Add(Gap("source", "SourceIdentityConflict", "sourceCoverage", source.Key, SourceRuleId, ReleaseReviewClassifications.UnknownAnalysisGap, $"Source `{source.Key}` identity differs between snapshots."));
            }

            if (!string.Equals(beforeSource.Coverage, "Full", StringComparison.Ordinal)
                || !string.Equals(afterSource.Coverage, "Full", StringComparison.Ordinal))
            {
                gaps.Add(Gap("source", "ReducedCoverage", "sourceCoverage", source.Key, SourceRuleId, ReleaseReviewClassifications.PartialAnalysis, $"Source `{source.Key}` has reduced static analysis coverage."));
            }
        }

        return gaps;
    }

    private static IReadOnlyList<ReleaseReviewSourceCoverage> PairSourceCoverage(ReleaseReviewSnapshot before, ReleaseReviewSnapshot after, IReadOnlyList<ReleaseReviewGap> gaps)
    {
        return before.Sources.Select(source => source.SourceLabel)
            .Concat(after.Sources.Select(source => source.SourceLabel))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(label => label, StringComparer.Ordinal)
            .Select(label =>
            {
                var beforeSource = before.Sources.FirstOrDefault(row => row.SourceLabel == label);
                var afterSource = after.Sources.FirstOrDefault(row => row.SourceLabel == label);
                var rowGaps = gaps.Where(gap => gap.Section == "sourceCoverage" && gap.SourceLabel == label).Select(gap => gap.GapId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var classification = rowGaps.Length == 0 ? ReleaseReviewClassifications.NoActionableEvidence : ReleaseReviewClassifications.PartialAnalysis;
                if (gaps.Any(gap => gap.SourceLabel == label && gap.Classification == ReleaseReviewClassifications.UnknownAnalysisGap))
                {
                    classification = ReleaseReviewClassifications.UnknownAnalysisGap;
                }

                return new ReleaseReviewSourceCoverage(
                    label,
                    beforeSource?.CommitSha,
                    afterSource?.CommitSha,
                    beforeSource?.Language ?? afterSource?.Language,
                    beforeSource?.Coverage ?? "Missing",
                    afterSource?.Coverage ?? "Missing",
                    beforeSource?.BuildStatus ?? "Missing",
                    afterSource?.BuildStatus ?? "Missing",
                    classification,
                    SourceRuleId,
                    EvidenceTiers.Tier4Unknown,
                    rowGaps);
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<SingleComparableFact>> ReadSingleComparableFactsAsync(string path, string side, string? sourceFilter, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sourceFilter) && !sourceFilter.Equals("single", StringComparison.Ordinal))
        {
            return [];
        }

        await using var connection = new SqliteConnection(ReadOnlyConnectionString(path));
        await connection.OpenAsync(cancellationToken);
        await using var manifestCommand = connection.CreateCommand();
        manifestCommand.CommandText = "select commit_sha from scan_manifest order by scanned_at desc limit 1;";
        var commitSha = NullIfUnknown((string?)await manifestCommand.ExecuteScalarAsync(cancellationToken));
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select fact_id,
                   fact_type,
                   rule_id,
                   evidence_tier,
                   file_path,
                   start_line,
                   end_line,
                   target_symbol,
                   contract_element,
                   properties_json
            from facts
            where fact_type in (
              'HttpRouteBinding',
              'HttpCallDetected',
              'PackageReferenced',
              'QueryPatternDetected',
              'SqlTextUsed',
              'SqlCommandDetected',
              'DapperCallDetected',
              'ConfigKeyDeclared',
              'ConfigBinding'
            )
            order by fact_type, file_path, start_line, fact_id;
            """;
        var rows = new List<SingleComparableFact>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var factId = reader.GetString(0);
            var factType = reader.GetString(1);
            var ruleId = reader.GetString(2);
            var evidenceTier = reader.GetString(3);
            var filePath = reader.GetString(4);
            var startLine = reader.GetInt32(5);
            var endLine = reader.GetInt32(6);
            var targetSymbol = reader.IsDBNull(7) ? null : reader.GetString(7);
            var contractElement = reader.IsDBNull(8) ? null : reader.GetString(8);
            var properties = ParseProperties(reader.GetString(9));
            var metadata = SafeFactMetadata(factType, properties);
            var stableInput = string.Join("|", factType, ruleId, targetSymbol, contractElement, string.Join(";", metadata.Select(pair => $"{pair.Key}={pair.Value}")));
            var evidenceInput = string.Join("|", stableInput, CombinedReportHelpers.SafePath(filePath), startLine, endLine, side);
            var finding = new ReleaseReviewFinding(
                StableId("finding", "topChangedSurfaces", side, factId),
                "topChangedSurfaces",
                "single",
                side == "after" ? CombinedDependencyDiffClassifications.Added : CombinedDependencyDiffClassifications.Removed,
                ruleId,
                EvidenceTiers.Tier3SyntaxOrTextual,
                commitSha,
                DisplayName(factType, targetSymbol, contractElement, metadata),
                SafeReportPath(filePath),
                startLine,
                endLine,
                CombinedReportHelpers.SortedMetadata([Pair("factType", factType), .. metadata.Select(pair => Pair(pair.Key, pair.Value))]),
                [factId],
                [],
                ["Single-index release review compares indexed fact evidence only; added/removed evidence is coverage-relative and review-tier."]);
            rows.Add(new SingleComparableFact(StableId("single-fact", stableInput), CombinedReportHelpers.Hash(evidenceInput, 32), finding));
        }

        return rows;
    }

    private static IReadOnlyList<ReleaseReviewFinding> DiffSingleFacts(IReadOnlyList<SingleComparableFact> before, IReadOnlyList<SingleComparableFact> after)
    {
        var beforeMap = before.GroupBy(row => row.StableKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.OrderBy(row => row.EvidenceHash, StringComparer.Ordinal).First(), StringComparer.Ordinal);
        var afterMap = after.GroupBy(row => row.StableKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.OrderBy(row => row.EvidenceHash, StringComparer.Ordinal).First(), StringComparer.Ordinal);
        var keys = beforeMap.Keys.Concat(afterMap.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal);
        var rows = new List<ReleaseReviewFinding>();
        foreach (var key in keys)
        {
            var hasBefore = beforeMap.TryGetValue(key, out var beforeRow);
            var hasAfter = afterMap.TryGetValue(key, out var afterRow);
            if (hasBefore && hasAfter)
            {
                if (beforeRow!.EvidenceHash == afterRow!.EvidenceHash)
                {
                    continue;
                }

                rows.Add(afterRow.Finding with
                {
                    FindingId = StableId("finding", "topChangedSurfaces", "changed", key),
                    Classification = CombinedDependencyDiffClassifications.ChangedEvidence
                });
                continue;
            }

            rows.Add((hasAfter ? afterRow : beforeRow)!.Finding);
        }

        return rows;
    }

    private static ReleaseReviewFinding[] ApplyFindingCap(ReleaseReviewFinding[] findings, int cap, List<ReleaseReviewGap> gaps)
    {
        if (findings.Length <= cap)
        {
            return findings;
        }

        gaps.Add(Gap(
            "truncation",
            "TruncatedByLimit",
            "summary",
            null,
            TruncationRuleId,
            ReleaseReviewClassifications.TruncatedByLimit,
            $"Release review omitted {findings.Length - cap} findings because --max-findings was reached."));
        return findings.Take(cap).ToArray();
    }

    private static ReleaseReviewSection FilterSectionFindings(ReleaseReviewSection section, IReadOnlyList<ReleaseReviewFinding> allowed)
    {
        var allowedIds = allowed.Select(finding => finding.FindingId).ToHashSet(StringComparer.Ordinal);
        var findings = section.Findings.Where(finding => allowedIds.Contains(finding.FindingId)).ToArray();
        return section with { Findings = findings };
    }

    private static string SelectRollup(IReadOnlyList<ReleaseReviewGap> gaps, IReadOnlyList<ReleaseReviewFinding> findings, bool truncated)
    {
        if (gaps.Any(gap => gap.Classification == ReleaseReviewClassifications.UnknownAnalysisGap))
        {
            return ReleaseReviewClassifications.UnknownAnalysisGap;
        }

        if (truncated || gaps.Any(gap => gap.Classification == ReleaseReviewClassifications.TruncatedByLimit))
        {
            return ReleaseReviewClassifications.TruncatedByLimit;
        }

        if (findings.Any(IsActionableFinding))
        {
            return ReleaseReviewClassifications.ActionableStaticEvidence;
        }

        if (findings.Count > 0)
        {
            return ReleaseReviewClassifications.ReviewRecommended;
        }

        if (gaps.Any(gap => gap.Classification == ReleaseReviewClassifications.PartialAnalysis))
        {
            return ReleaseReviewClassifications.PartialAnalysis;
        }

        if (gaps.Any(gap => gap.Classification == ReleaseReviewClassifications.SelectorNoMatch))
        {
            return ReleaseReviewClassifications.SelectorNoMatch;
        }

        return ReleaseReviewClassifications.NoActionableEvidence;
    }

    private static bool IsActionableFinding(ReleaseReviewFinding finding)
    {
        return StrongClassifications.Contains(finding.Classification)
            && finding.EvidenceTier is EvidenceTiers.Tier1Semantic or EvidenceTiers.Tier2Structural;
    }

    private static string? SafeReportPath(string? filePath)
    {
        return string.IsNullOrWhiteSpace(filePath) ? null : CombinedReportHelpers.SafePath(filePath);
    }

    private static int FindingSeverityRank(ReleaseReviewFinding finding)
    {
        if (IsActionableFinding(finding))
        {
            return 0;
        }

        return finding.Classification switch
        {
            ReleaseReviewClassifications.UnknownAnalysisGap => 1,
            CombinedImpactClassifications.NeedsReviewImpact => 2,
            CombinedDependencyDiffClassifications.NeedsReviewDiff => 2,
            _ => 3
        };
    }

    private static int GapSeverityRank(ReleaseReviewGap gap)
    {
        return gap.Classification switch
        {
            ReleaseReviewClassifications.UnknownAnalysisGap => 0,
            ReleaseReviewClassifications.TruncatedByLimit => 1,
            ReleaseReviewClassifications.PartialAnalysis => 2,
            ReleaseReviewClassifications.SelectorNoMatch => 3,
            _ => 4
        };
    }

    private static int SeverityRank(ReleaseReviewChecklistItem item)
    {
        return item.Severity switch
        {
            "must_review" => 0,
            "should_review" => 1,
            _ => 2
        };
    }

    private static ReleaseReviewGap Gap(
        string stablePrefix,
        string gapKind,
        string section,
        string? sourceLabel,
        string ruleId,
        string classification,
        string message,
        IReadOnlyList<string>? supportingFindingIds = null,
        IReadOnlyList<string>? supportingFactIds = null,
        IReadOnlyList<string>? supportingEdgeIds = null,
        IReadOnlyList<KeyValuePair<string, string>>? metadata = null)
    {
        return new ReleaseReviewGap(
            StableId("gap", stablePrefix, section, sourceLabel ?? string.Empty, gapKind, message),
            gapKind,
            section,
            sourceLabel,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            classification,
            message,
            supportingFindingIds ?? [],
            supportingFactIds ?? [],
            supportingEdgeIds ?? [],
            metadata ?? []);
    }

    private static string MapGapClassification(string classification)
    {
        if (classification == CombinedImpactClassifications.UnknownAnalysisGap)
        {
            return ReleaseReviewClassifications.UnknownAnalysisGap;
        }

        if (classification == CombinedImpactClassifications.TruncatedByLimit)
        {
            return ReleaseReviewClassifications.TruncatedByLimit;
        }

        if (classification == CombinedImpactClassifications.SelectorNoMatch)
        {
            return ReleaseReviewClassifications.SelectorNoMatch;
        }

        return ReleaseReviewClassifications.PartialAnalysis;
    }

    private static IReadOnlyList<string> NormalizeScopes(string? scope)
    {
        var scopes = string.IsNullOrWhiteSpace(scope)
            ? ["all"]
            : scope.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var normalized = scopes.Select(value => value.ToLowerInvariant()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        foreach (var value in normalized)
        {
            if (!ValidScopes.Contains(value))
            {
                throw new ArgumentException($"release-review --scope contains unsupported value `{value}`.");
            }
        }

        return normalized.Length == 0 ? ["all"] : normalized;
    }

    private static string ImpactScope(IReadOnlyList<string> scopes, bool includePaths)
    {
        if (scopes.Contains("all", StringComparer.Ordinal))
        {
            return includePaths ? "sources,coverage,endpoints,surfaces,edges,paths" : "sources,coverage,endpoints,surfaces,edges";
        }

        var mapped = new List<string>();
        if (ScopeEnabled(scopes, "sources"))
        {
            mapped.Add("sources");
        }

        if (ScopeEnabled(scopes, "coverage"))
        {
            mapped.Add("coverage");
        }

        if (ScopeEnabled(scopes, "surfaces"))
        {
            mapped.AddRange(["endpoints", "surfaces", "edges"]);
        }

        if (includePaths && ScopeEnabled(scopes, "paths"))
        {
            mapped.Add("paths");
        }

        return mapped.Count == 0 ? "sources,coverage,endpoints,surfaces,edges" : string.Join(",", mapped.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));
    }

    private static bool ScopeEnabled(IReadOnlyList<string> scopes, string scope)
    {
        if (scopes.Contains("all", StringComparer.Ordinal))
        {
            return true;
        }

        return scope switch
        {
            "sources" => scopes.Contains("sources", StringComparer.Ordinal),
            "coverage" => scopes.Contains("coverage", StringComparer.Ordinal) || scopes.Contains("sources", StringComparer.Ordinal),
            "surfaces" => scopes.Contains("surfaces", StringComparer.Ordinal),
            "contracts" => scopes.Contains("contracts", StringComparer.Ordinal),
            "api-dto" => scopes.Contains("api-dto", StringComparer.Ordinal),
            "sql-schema" => scopes.Contains("sql-schema", StringComparer.Ordinal),
            "packages" => scopes.Contains("packages", StringComparer.Ordinal),
            "paths" => scopes.Contains("paths", StringComparer.Ordinal),
            "reverse" => scopes.Contains("reverse", StringComparer.Ordinal),
            _ => scopes.Contains(scope, StringComparer.Ordinal)
        };
    }

    private static IReadOnlyList<string> IgnoredSelectors(ReleaseReviewOptions options, IReadOnlyList<string> scopes)
    {
        var ignored = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.Endpoint) && !ScopeEnabled(scopes, "surfaces") && !ScopeEnabled(scopes, "paths"))
        {
            ignored.Add("--endpoint has no enabled surface or path scope.");
        }

        if (!string.IsNullOrWhiteSpace(options.Surface) && !ScopeEnabled(scopes, "surfaces") && !ScopeEnabled(scopes, "paths") && !ScopeEnabled(scopes, "reverse") && !ScopeEnabled(scopes, "packages"))
        {
            ignored.Add("--surface has no enabled surface, path, reverse, or package scope.");
        }

        if (!string.IsNullOrWhiteSpace(options.SurfaceName) && !ScopeEnabled(scopes, "surfaces") && !ScopeEnabled(scopes, "paths") && !ScopeEnabled(scopes, "reverse") && !ScopeEnabled(scopes, "packages"))
        {
            ignored.Add("--surface-name has no enabled surface, path, reverse, or package scope.");
        }

        return ignored.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static string RenderMarkdown(ReleaseReviewDocument report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Release Review Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Mode: `{report.Mode}`");
        builder.AppendLine($"- Rollup: `{report.Summary.RollupClassification}`");
        builder.AppendLine($"- Rule: `{report.Summary.RuleId}`");
        builder.AppendLine($"- Message: {CombinedReportHelpers.Cell(report.Summary.Message)}");
        builder.AppendLine($"- Sources: `{report.Summary.SourceCount}`");
        builder.AppendLine($"- Actionable findings: `{report.Summary.ActionableFindingCount}`");
        builder.AppendLine($"- Review findings: `{report.Summary.ReviewFindingCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        builder.AppendLine($"- Path context requested: `{report.Query.IncludePaths}`");
        builder.AppendLine($"- Reverse context requested: `{report.Query.IncludeReverse}`");
        builder.AppendLine();
        builder.AppendLine("## Compared Snapshots");
        builder.AppendLine();
        RenderSnapshot(builder, report.BeforeSnapshot);
        RenderSnapshot(builder, report.AfterSnapshot);
        builder.AppendLine("## Source Identity and Coverage");
        builder.AppendLine();
        builder.AppendLine("| Source | Before commit | After commit | Language | Before coverage | After coverage | Classification | Gaps |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var source in report.SourceCoverage)
        {
            builder.AppendLine($"| {Cell(source.SourceLabel)} | {Cell(source.BeforeCommitSha)} | {Cell(source.AfterCommitSha)} | {Cell(source.Language)} | `{Cell(source.BeforeCoverage)}` | `{Cell(source.AfterCoverage)}` | `{Cell(source.Classification)}` | {Cell(string.Join(", ", source.GapIds))} |");
        }

        builder.AppendLine();
        RenderSection(builder, "Top Changed Surfaces", report.TopChangedSurfaces);
        RenderSection(builder, "Contract Delta Impact", report.ContractImpact);
        RenderSection(builder, "API and DTO Changes", report.ApiDtoChanges);
        RenderSection(builder, "SQL and Schema Impact", report.SqlSchemaImpact);
        RenderSection(builder, "Package Impact", report.PackageImpact);
        builder.AppendLine("## Path and Reverse Context");
        builder.AppendLine();
        RenderSectionBody(builder, "Path Context", report.PathContext);
        RenderSectionBody(builder, "Reverse Context", report.ReverseContext);
        builder.AppendLine("## Analysis Gaps");
        builder.AppendLine();
        RenderGaps(builder, report.Gaps);
        builder.AppendLine("## Reviewer Checklist");
        builder.AppendLine();
        if (report.ReviewerChecklist.Count == 0)
        {
            builder.AppendLine("No checklist items generated.");
        }
        else
        {
            builder.AppendLine("| Severity | Section | Rule | Item | Findings | Gaps |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (var item in report.ReviewerChecklist)
            {
                builder.AppendLine($"| `{Cell(item.Severity)}` | {Cell(item.Section)} | `{Cell(item.RuleId)}` | {Cell(item.Text)} | {Cell(string.Join(", ", item.FindingIds))} | {Cell(string.Join(", ", item.GapIds))} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations)
        {
            builder.AppendLine($"- {CombinedReportHelpers.Cell(limitation)}");
        }

        return builder.ToString();
    }

    private static void RenderSnapshot(StringBuilder builder, ReleaseReviewSnapshot snapshot)
    {
        builder.AppendLine($"### {snapshot.Side}");
        builder.AppendLine();
        builder.AppendLine($"- Index kind: `{snapshot.IndexKind}`");
        builder.AppendLine($"- Report coverage: `{snapshot.ReportCoverage}`");
        builder.AppendLine($"- Source count: `{snapshot.Sources.Count}`");
        builder.AppendLine();
        builder.AppendLine("| Source | Language | Commit | Coverage | Build | Analysis | Gaps |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var source in snapshot.Sources)
        {
            builder.AppendLine($"| {Cell(source.SourceLabel)} | {Cell(source.Language)} | {Cell(source.CommitSha)} | `{Cell(source.Coverage)}` | `{Cell(source.BuildStatus)}` | `{Cell(source.AnalysisLevel)}` | {Cell(string.Join(", ", source.GapCodes))} |");
        }

        builder.AppendLine();
    }

    private static void RenderSection(StringBuilder builder, string title, ReleaseReviewSection section)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        RenderSectionBody(builder, title, section);
    }

    private static void RenderSectionBody(StringBuilder builder, string title, ReleaseReviewSection section)
    {
        builder.AppendLine($"Status: `{section.Status}`");
        if (section.OmittedCount > 0)
        {
            builder.AppendLine($"Omitted rows: `{section.OmittedCount}`");
        }

        builder.AppendLine();
        if (section.Findings.Count == 0)
        {
            builder.AppendLine("No findings in this section.");
            builder.AppendLine();
        }
        else
        {
            builder.AppendLine("| Finding | Source | Classification | Rule | Tier | Location | Metadata |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var finding in section.Findings)
            {
                var location = finding.FilePath is null ? "n/a" : $"{finding.FilePath}:{finding.StartLine ?? 0}-{finding.EndLine ?? 0}";
                builder.AppendLine($"| `{Cell(finding.FindingId)}` | {Cell(finding.SourceLabel)} | `{Cell(finding.Classification)}` | `{Cell(finding.RuleId)}` | `{Cell(finding.EvidenceTier)}` | `{Cell(location)}` | {Cell(MetadataText(finding.Metadata))} |");
            }

            builder.AppendLine();
        }

        if (section.Gaps.Count > 0)
        {
            builder.AppendLine($"### {title} Gaps");
            builder.AppendLine();
            RenderGaps(builder, section.Gaps);
        }
    }

    private static void RenderGaps(StringBuilder builder, IReadOnlyList<ReleaseReviewGap> gaps)
    {
        if (gaps.Count == 0)
        {
            builder.AppendLine("No analysis gaps.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Gap | Section | Source | Kind | Classification | Rule | Tier | Message |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var gap in gaps)
        {
            builder.AppendLine($"| `{Cell(gap.GapId)}` | {Cell(gap.Section)} | {Cell(gap.SourceLabel)} | {Cell(gap.GapKind)} | `{Cell(gap.Classification)}` | `{Cell(gap.RuleId)}` | `{Cell(gap.EvidenceTier)}` | {Cell(gap.Message)} |");
        }

        builder.AppendLine();
    }

    private static string MetadataText(IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        return string.Join("; ", metadata.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string Cell(string? value)
    {
        return CombinedReportHelpers.Cell(value);
    }

    private static IReadOnlyList<string> CoverageWarnings(IReadOnlyList<ReleaseReviewSourceInfo> sources)
    {
        return sources
            .Where(source => !string.Equals(source.Coverage, "Full", StringComparison.Ordinal))
            .Select(source => $"{source.SourceLabel} has reduced analysis coverage.")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string CoverageFrom(string analysisLevel, string buildStatus, IReadOnlyList<string> knownGaps)
    {
        return analysisLevel.Contains("Reduced", StringComparison.OrdinalIgnoreCase)
            || buildStatus is "FailedOrPartial" or "Failed"
            || knownGaps.Count > 0
            ? "Reduced"
            : "Full";
    }

    private static string? RepositoryIdentityHash(string? remoteUrl, string repoName)
    {
        var identity = string.IsNullOrWhiteSpace(remoteUrl) ? repoName : remoteUrl;
        return string.IsNullOrWhiteSpace(identity) ? null : $"repo:{CombinedReportHelpers.Hash(identity, 24)}";
    }

    private static string? NullIfUnknown(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    private static string InferLanguage(string scannerVersion)
    {
        if (scannerVersion.Contains("typescript", StringComparison.OrdinalIgnoreCase))
        {
            return "typescript";
        }

        if (scannerVersion.Contains("python", StringComparison.OrdinalIgnoreCase))
        {
            return "python";
        }

        if (scannerVersion.Contains("jvm", StringComparison.OrdinalIgnoreCase))
        {
            return "jvm";
        }

        return "csharp";
    }

    private static ScanManifest? DeserializeManifest(string manifestJson)
    {
        try
        {
            return JsonSerializer.Deserialize<ScanManifest>(manifestJson, CombinedDependencyReporter.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
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
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SafeFactMetadata(string factType, IReadOnlyDictionary<string, string> properties)
    {
        var allowed = new[]
        {
            "httpMethod",
            "normalizedPathKey",
            "normalizedPathTemplate",
            "routePatternHash",
            "operation",
            "tables",
            "columns",
            "queryShapeHash",
            "textHash",
            "sourceKind",
            "ecosystem",
            "packageName",
            "packageVersion",
            "version",
            "versionRange",
            "configKeyHash",
            "keyHash"
        };
        var values = allowed
            .Where(properties.ContainsKey)
            .Select(key => Pair(key, SafeMetadataValue(key, properties[key])))
            .Append(Pair("surfaceKind", SurfaceKindForFact(factType)))
            .Where(pair => pair.Value is not null)
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value!));
        return CombinedReportHelpers.SortedMetadata(values.Select(pair => Pair(pair.Key, pair.Value)));
    }

    private static string? SafeMetadataValue(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (key.EndsWith("Hash", StringComparison.Ordinal) || key is "operation" or "sourceKind" or "ecosystem" or "httpMethod" or "surfaceKind")
        {
            return value;
        }

        if (value.Contains("://", StringComparison.Ordinal)
            || value.Contains("/", StringComparison.Ordinal)
            || value.Contains("\\", StringComparison.Ordinal)
            || value.Contains(";", StringComparison.Ordinal)
            || value.Length > 96)
        {
            return $"value-hash:{CombinedReportHelpers.Hash(value, 16)}";
        }

        return value;
    }

    private static string SurfaceKindForFact(string factType)
    {
        return factType switch
        {
            FactTypes.HttpRouteBinding => "http-route",
            FactTypes.HttpCallDetected => "http-client",
            FactTypes.QueryPatternDetected or FactTypes.SqlTextUsed or FactTypes.SqlCommandDetected or FactTypes.DapperCallDetected => "sql-query",
            FactTypes.PackageReferenced => "package-config",
            FactTypes.ConfigKeyDeclared or FactTypes.ConfigBinding => "config",
            _ => "fact"
        };
    }

    private static string DisplayName(string factType, string? targetSymbol, string? contractElement, IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        var path = metadata.FirstOrDefault(pair => pair.Key == "normalizedPathKey").Value;
        if (!string.IsNullOrWhiteSpace(path))
        {
            return $"{metadata.FirstOrDefault(pair => pair.Key == "httpMethod").Value} {path}".Trim();
        }

        var package = metadata.FirstOrDefault(pair => pair.Key == "packageName").Value;
        if (!string.IsNullOrWhiteSpace(package))
        {
            return package;
        }

        return contractElement ?? targetSymbol ?? factType;
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SafeMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
    {
        return CombinedReportHelpers.SortedMetadata(metadata.Select(pair => Pair(pair.Key, SafeMetadataValue(pair.Key, pair.Value))));
    }

    private static KeyValuePair<string, string?> Pair(string key, string? value)
    {
        return new KeyValuePair<string, string?>(key, value);
    }

    private static string StableId(params string[] parts)
    {
        return $"{parts[0]}:{CombinedReportHelpers.Hash(string.Join("|", parts), 24)}";
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select 1 from sqlite_master where type = 'table' and name = $name limit 1;";
        command.Parameters.AddWithValue("$name", tableName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null;
    }

    private static string ReadOnlyConnectionString(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        };
        return builder.ToString();
    }

    private static void ValidateOptions(ReleaseReviewOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BeforePath))
        {
            throw new ArgumentException("release-review requires --before <path>.");
        }

        if (string.IsNullOrWhiteSpace(options.AfterPath))
        {
            throw new ArgumentException("release-review requires --after <path>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("release-review requires --out <path>.");
        }

        if (options.MaxFindings <= 0 || options.MaxSurfaceRows <= 0 || options.MaxPaths <= 0 || options.MaxGaps <= 0 || options.MaxChecklistItems <= 0)
        {
            throw new ArgumentException("release-review caps must be positive integers.");
        }
    }

    private static void ValidateReadableFile(string? path, string optionName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            using var _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"release-review could not read {optionName} input.");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
