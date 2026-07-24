using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Reduction;
using TraceMap.Reporting.ReviewPriority;

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
    int MaxChecklistItems = 50,
    bool IncludePriority = false,
    IReadOnlyList<string>? SqlValidationSummaryPaths = null,
    DateTimeOffset? SqlValidationAsOf = null);

public sealed record ReleaseReviewResult(
    ReleaseReviewDocument Report,
    string? MarkdownPath,
    string? JsonPath,
    ReviewPrioritySummary? ReviewPriority = null,
    IReadOnlyList<ReviewPriorityRow>? ReviewPriorityRows = null)
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
    ReleaseReviewSection SqlEvidence,
    ReleaseReviewSection SqlValidationObservations,
    ReleaseReviewSection AccessEvidence,
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
    bool SqlValidationSummaryProvided,
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
    IReadOnlyList<string> GapCodes,
    IReadOnlyList<ReleaseReviewCapabilitySummary>? AnalyzerCapabilities = null);

public sealed record ReleaseReviewCapabilitySummary(
    string CapabilityCode,
    string CapabilityState,
    string CoverageEffect,
    string RuleId,
    string EvidenceTier,
    string SchemaVersion,
    IReadOnlyList<string> SupportingFactIds);

public sealed record ReleaseReviewSummary(
    string RollupClassification,
    string RuleId,
    int SourceCount,
    int TopChangedSurfaceCount,
    int ContractFindingCount,
    int ApiDtoFindingCount,
    int SqlSchemaFindingCount,
    int SqlEvidenceFindingCount,
    int SqlValidationObservationFindingCount,
    int AccessEvidenceFindingCount,
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
    IReadOnlyList<string> Limitations,
    string ExtractorId = "not-recorded",
    string ExtractorVersion = "not-recorded",
    string CoverageLabel = "not-recorded");

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

internal sealed record AccessEvidencePresence(long FactCount, IReadOnlyList<string> SupportingFactIds);

internal sealed record SingleComparableFact(
    string StableKey,
    string EvidenceHash,
    ReleaseReviewFinding Finding);

internal sealed record SqlEvidenceInput(string SourceLabel, ScanResult Result, bool ProvenanceCompatible);
internal sealed record SqlEvidenceFactRow(string SourceLabel, ScanManifest Manifest, CodeFact Fact, bool ProvenanceCompatible);

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
    private const string Version = "1.2";
    private const string Algorithm = "release-review-composition";
    private const string AlgorithmVersion = "1.0";
    private const string RollupRuleId = "release.review.rollup.v1";
    private const string ChecklistRuleId = "release.review.checklist.v1";
    private const string SourceRuleId = "release.review.source.v1";
    private const string SectionRuleId = "release.review.section.v1";
    private const string SelectorRuleId = "release.review.selector.v1";
    private const string TruncationRuleId = "release.review.truncation.v1";

    private static readonly HashSet<string> SqlRunwayRuleIds = new(StringComparer.Ordinal)
    {
        RuleIds.DatabaseSqlContextDeclaration,
        RuleIds.DatabaseSqlContextSyntax,
        RuleIds.DatabaseSqlContextGap,
        RuleIds.DatabaseSqlSecretBearingStep,
        RuleIds.DatabaseSqlSecretTextCandidate,
        RuleIds.DatabaseSqlSecretSafetyGap,
        RuleIds.DatabasePostgresArchiveLink,
        RuleIds.DatabasePostgresArchiveLinkPrerequisite,
        RuleIds.DatabasePostgresArchiveLinkGap,
        RuleIds.DatabasePostgresPermissionStatement,
        RuleIds.DatabasePostgresPermissionPrerequisite,
        RuleIds.DatabasePostgresPermissionCoverage,
        RuleIds.DatabasePostgresPermissionGap
    };

    private static readonly HashSet<string> ValidScopes = new(StringComparer.Ordinal)
    {
        "all",
        "sources",
        "coverage",
        "surfaces",
        "contracts",
        "api-dto",
        "sql-schema",
        "sql-evidence",
        "access-evidence",
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
        if (options.IncludePriority)
        {
            var priority = ReleaseReviewPriorityScorer.Score(report);
            var (scoredMarkdownPath, scoredJsonPath) = await WriteScoredOutputsAsync(
                options.OutputPath,
                format,
                report,
                priority,
                cancellationToken);
            return new ReleaseReviewResult(report, scoredMarkdownPath, scoredJsonPath, priority.Summary, priority.Rows);
        }

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

    private static async Task<(string? MarkdownPath, string? JsonPath)> WriteScoredOutputsAsync(
        string outputPath,
        string format,
        ReleaseReviewDocument report,
        ReleaseReviewPriorityResult priority,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var markdown = RenderMarkdown(report, priority);
        var json = ToScoredJson(report, priority);
        if (Directory.Exists(fullPath) || string.IsNullOrWhiteSpace(Path.GetExtension(fullPath)))
        {
            Directory.CreateDirectory(fullPath);
            var markdownPath = Path.Combine(fullPath, "release-review.md");
            var jsonPath = Path.Combine(fullPath, "release-review.json");
            await File.WriteAllTextAsync(markdownPath, markdown, cancellationToken);
            await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
            return (markdownPath, jsonPath);
        }

        var directoryName = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        if (format == "json")
        {
            await File.WriteAllTextAsync(fullPath, json, cancellationToken);
            return (null, fullPath);
        }

        await File.WriteAllTextAsync(fullPath, markdown, cancellationToken);
        return (fullPath, null);
    }

    private static string ToScoredJson(ReleaseReviewDocument report, ReleaseReviewPriorityResult priority)
    {
        var node = JsonSerializer.SerializeToNode(report, CombinedDependencyReporter.JsonOptions) as JsonObject
            ?? throw new InvalidOperationException("release-review JSON root was not an object.");
        node["reviewPriority"] = JsonSerializer.SerializeToNode(priority.Summary, CombinedDependencyReporter.JsonOptions);
        node["reviewPriorityRows"] = JsonSerializer.SerializeToNode(priority.Rows, CombinedDependencyReporter.JsonOptions);
        return node.ToJsonString(CombinedDependencyReporter.JsonOptions) + Environment.NewLine;
    }

    public static async Task<ReleaseReviewDocument> BuildReportAsync(ReleaseReviewOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        ValidateReadableFile(options.BeforePath, "--before");
        ValidateReadableFile(options.AfterPath, "--after");
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
            if (ImpactContextRequested(scopes, options.IncludePaths))
            {
                var impact = await BuildCombinedImpactAsync(options, scopes, cancellationToken);
                topChangedSurfaces = BuildTopChangedSurfacesSection(impact, options.MaxSurfaceRows);
                pathContext = BuildPathContextSection(impact, options.IncludePaths, options.MaxPaths);
            }
            else
            {
                topChangedSurfaces = new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["Top changed surface impact scope was not requested."]);
                pathContext = new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["Path context is off by default or outside the requested scope."]);
            }

            reverseContext = await BuildReverseContextSectionAsync(options, afterInfo.Snapshot, cancellationToken);
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
        var sqlEvidence = await BuildSqlEvidenceSectionAsync(options, scopes, afterInfo.Kind, cancellationToken);
        var sqlValidationObservations = await BuildSqlValidationObservationSectionAsync(options, scopes, afterInfo.Kind, cancellationToken);
        var accessEvidence = await AccessDesignReviewComposer.BuildSectionAsync(
            options.AfterPath,
            afterInfo.Kind,
            ScopeEnabled(scopes, "access-evidence"),
            options.Source,
            cancellationToken);
        var packageImpact = BuildPackageSection(options, scopes, topChangedSurfaces);
        gaps.AddRange(contractImpact.Gaps);
        gaps.AddRange(apiDtoChanges.Gaps);
        gaps.AddRange(sqlSchemaImpact.Gaps);
        gaps.AddRange(sqlEvidence.Gaps);
        gaps.AddRange(sqlValidationObservations.Gaps);
        gaps.AddRange(accessEvidence.Gaps);
        gaps.AddRange(packageImpact.Gaps);

        var allFindings = new[]
            {
                topChangedSurfaces,
                contractImpact,
                apiDtoChanges,
                sqlSchemaImpact,
                sqlEvidence,
                sqlValidationObservations,
                accessEvidence,
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
        sqlEvidence = FilterSectionFindings(sqlEvidence, cappedFindings);
        sqlValidationObservations = FilterSectionFindings(sqlValidationObservations, cappedFindings);
        accessEvidence = FilterSectionFindings(accessEvidence, cappedFindings);
        packageImpact = FilterSectionFindings(packageImpact, cappedFindings);
        pathContext = FilterSectionFindings(pathContext, cappedFindings);
        reverseContext = FilterSectionFindings(reverseContext, cappedFindings);

        AddChecklistTruncationGapIfNeeded(gaps, cappedFindings, options.MaxChecklistItems);
        var cappedGaps = CapGaps(gaps, options.MaxGaps);
        var truncated = gaps.DistinctBy(gap => gap.GapId).Count() > cappedGaps.Length
            || new[]
            {
                topChangedSurfaces,
                contractImpact,
                apiDtoChanges,
                sqlSchemaImpact,
                sqlEvidence,
                sqlValidationObservations,
                accessEvidence,
                packageImpact,
                pathContext,
                reverseContext
            }.Any(section => section.Status == ReleaseReviewStatuses.Truncated)
            || cappedFindings.Length < allFindings.Length;
        topChangedSurfaces = FilterSectionGaps(topChangedSurfaces, cappedGaps);
        contractImpact = FilterSectionGaps(contractImpact, cappedGaps);
        apiDtoChanges = FilterSectionGaps(apiDtoChanges, cappedGaps);
        sqlSchemaImpact = FilterSectionGaps(sqlSchemaImpact, cappedGaps);
        sqlEvidence = FilterSectionGaps(sqlEvidence, cappedGaps);
        sqlValidationObservations = FilterSectionGaps(sqlValidationObservations, cappedGaps);
        accessEvidence = FilterSectionGaps(accessEvidence, cappedGaps);
        packageImpact = FilterSectionGaps(packageImpact, cappedGaps);
        pathContext = FilterSectionGaps(pathContext, cappedGaps);
        reverseContext = FilterSectionGaps(reverseContext, cappedGaps);

        var summary = BuildSummary(
            sourceCoverage.Count,
            topChangedSurfaces,
            contractImpact,
            apiDtoChanges,
            sqlSchemaImpact,
            sqlEvidence,
            sqlValidationObservations,
            accessEvidence,
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
                options.SqlValidationSummaryPaths is { Count: > 0 },
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
            sqlEvidence,
            sqlValidationObservations,
            accessEvidence,
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

    private static async Task<ReleaseReviewSection> BuildReverseContextSectionAsync(ReleaseReviewOptions options, ReleaseReviewSnapshot afterSnapshot, CancellationToken cancellationToken)
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
        var commitBySource = afterSnapshot.Sources.ToDictionary(source => source.SourceLabel, source => source.CommitSha, StringComparer.Ordinal);
        var findings = reverse.ReverseRoots.Select(root => FromReverseRoot(root, commitBySource))
            .Concat(reverse.Paths.Select(path => FromReversePath(path, commitBySource)))
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

        var before = await ReadSingleComparableFactsAsync(options.BeforePath, "before", options, cancellationToken);
        var after = await ReadSingleComparableFactsAsync(options.AfterPath, "after", options, cancellationToken);
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

    private static async Task<ReleaseReviewSection> BuildSqlEvidenceSectionAsync(
        ReleaseReviewOptions options,
        IReadOnlyList<string> scopes,
        string indexKind,
        CancellationToken cancellationToken)
    {
        if (!ScopeEnabled(scopes, "sql-evidence"))
        {
            return new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["SQL runway evidence is outside the requested release-review scope."]);
        }

        var inputs = await ReadSqlEvidenceInputsAsync(options.AfterPath, indexKind, cancellationToken);
        if (!string.IsNullOrWhiteSpace(options.Source))
        {
            inputs = inputs
                .Where(input => string.Equals(input.SourceLabel, options.Source, StringComparison.Ordinal))
                .ToArray();
        }
        if (inputs.Count == 0)
        {
            var noEvidenceGap = Gap(
                "sql-evidence",
                "CompatibleEvidenceUnavailable",
                "sqlEvidence",
                options.Source,
                SectionRuleId,
                ReleaseReviewClassifications.PartialAnalysis,
                "No compatible SQL runway evidence was present in the selected after-index; this does not establish absence of SQL risk or database changes.",
                metadata: CombinedReportHelpers.SortedMetadata([
                    Pair("evidenceScope", "selected-after-index-sql-catalog"),
                    Pair("indexKind", indexKind),
                    Pair("sourceSelector", options.Source ?? "all-sources")
                ]));
            return new ReleaseReviewSection(ReleaseReviewStatuses.Deferred, [], [noEvidenceGap], SqlEvidenceLimitations());
        }

        if (inputs.Any(input => !input.ProvenanceCompatible))
        {
            var incompatible = inputs.Where(input => !input.ProvenanceCompatible).Select(input => input.SourceLabel).Order(StringComparer.Ordinal).ToArray();
            var provenanceGap = Gap(
                "sql-evidence",
                "ExtractorProvenanceUnavailable",
                "sqlEvidence",
                options.Source,
                SectionRuleId,
                ReleaseReviewClassifications.PartialAnalysis,
                "SQL runway facts were present, but their persisted extractor provenance was unavailable; release-review did not project incomplete evidence.",
                metadata: [new KeyValuePair<string, string>("sourceLabels", string.Join(',', incompatible))]);
            return new ReleaseReviewSection(ReleaseReviewStatuses.Unavailable, [], [provenanceGap], SqlEvidenceLimitations());
        }

        var findings = new List<ReleaseReviewFinding>();
        var gaps = new List<ReleaseReviewGap>();
        foreach (var input in inputs.OrderBy(input => input.SourceLabel, StringComparer.Ordinal))
        {
            var packet = SqlRunbookPacketBuilder.Build(input.Result);
            foreach (var group in packet.StepGroups)
                foreach (var step in group.Steps)
                    findings.Add(SqlEvidenceFinding(input.SourceLabel, "context-step", step.StepKind, ReleaseReviewClassifications.NoActionableEvidence, step.Evidence,
                        [Pair("engine", group.Engine), Pair("serverRole", group.ServerRole), Pair("databaseRole", group.DatabaseRole), Pair("schemaRole", group.SchemaRole), Pair("executionMode", group.ExecutionMode), Pair("contextClassification", step.ContextClassification), Pair("stopConditions", string.Join(',', step.StopConditions))]));
            foreach (var milestone in packet.Milestones)
                findings.Add(SqlEvidenceFinding(input.SourceLabel, "milestone", milestone.Kind, ReleaseReviewClassifications.NoActionableEvidence, milestone.Evidence,
                    [Pair("state", milestone.State), Pair("validationState", milestone.ValidationState)]));
            foreach (var prerequisite in packet.Prerequisites)
                findings.Add(SqlEvidenceFinding(input.SourceLabel, "prerequisite", prerequisite.Capability,
                    prerequisite.Status == "present-in-scripts" ? ReleaseReviewClassifications.NoActionableEvidence : ReleaseReviewClassifications.ReviewRecommended,
                    prerequisite.Evidence, [Pair("operationKind", prerequisite.OperationKind), Pair("status", prerequisite.Status), Pair("contextRole", prerequisite.ContextRole)]));
            foreach (var protectedStep in packet.ProtectedSteps)
            {
                findings.Add(SqlEvidenceFinding(input.SourceLabel, "protected-step", protectedStep.Classification, ReleaseReviewClassifications.ReviewRecommended, protectedStep.Evidence,
                    [Pair("categories", string.Join(',', protectedStep.Categories)), Pair("ownerHandling", protectedStep.OwnerHandling)]));
            }
            foreach (var gap in packet.Gaps)
                gaps.Add(SqlEvidenceGap(input.SourceLabel, gap.Code, $"Upstream SQL {gap.Category} evidence recorded a bounded static-analysis gap.", gap.Evidence));
            foreach (var question in packet.OwnerQuestions)
                gaps.Add(SqlEvidenceGap(input.SourceLabel, "OwnerQuestion", question.Question, question.Evidence));
        }

        var orderedFindings = findings.DistinctBy(finding => finding.FindingId).OrderBy(FindingSeverityRank).ThenBy(finding => finding.FindingId, StringComparer.Ordinal).ToArray();
        var orderedGaps = gaps.DistinctBy(gap => gap.GapId).OrderBy(GapSeverityRank).ThenBy(gap => gap.GapId, StringComparer.Ordinal).ToArray();
        return new ReleaseReviewSection(ReleaseReviewStatuses.Available, orderedFindings, orderedGaps, SqlEvidenceLimitations());
    }

    private static async Task<ReleaseReviewSection> BuildSqlValidationObservationSectionAsync(
        ReleaseReviewOptions options,
        IReadOnlyList<string> scopes,
        string indexKind,
        CancellationToken cancellationToken)
    {
        var paths = options.SqlValidationSummaryPaths ?? [];
        if (!ScopeEnabled(scopes, "sql-evidence") || paths.Count == 0)
        {
            return new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [],
                ["Observed SQL validation is included only when --sql-validation-summary is explicitly supplied within SQL evidence scope."]);
        }

        var inputs = await ReadSqlEvidenceInputsAsync(options.AfterPath, indexKind, cancellationToken);
        if (!string.IsNullOrWhiteSpace(options.Source))
            inputs = inputs.Where(input => string.Equals(input.SourceLabel, options.Source, StringComparison.Ordinal)).ToArray();

        var compatible = inputs.Where(input => input.ProvenanceCompatible).OrderBy(input => input.SourceLabel, StringComparer.Ordinal).ToArray();
        var expected = compatible.Select(input =>
        {
            var packet = SqlRunbookPacketBuilder.Build(input.Result);
            return SqlRunbookPacketBuilder.ValidationExpectedSource(input.Result, packet, input.SourceLabel, options.SqlValidationAsOf);
        }).ToArray();
        var composition = await SqlValidationSummaryReader.ReadAsync(paths, expected, cancellationToken);

        var labels = expected
            .SelectMany(source => source.Contexts.Select(context => new
            {
                Key = SqlValidationSourceKey(source.Repository, source.CommitSha, context),
                source.SourceLabel
            }))
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SourceLabel, StringComparer.Ordinal).First().SourceLabel, StringComparer.Ordinal);
        var findings = composition.Observations.Select(observation => new ReleaseReviewFinding(
                StableId("finding", "sqlValidationObservations", observation.ObservationId),
                "sqlValidationObservations",
                labels.GetValueOrDefault(SqlValidationSourceKey(observation.Repository, observation.CommitSha, observation.TargetContext)),
                ReleaseReviewClassifications.ReviewRecommended,
                observation.RuleId,
                EvidenceTiers.Tier4Unknown,
                observation.CommitSha,
                observation.AssertionCode,
                $"validation-summary/{observation.ArtifactId}.json",
                0,
                0,
                CombinedReportHelpers.SortedMetadata([
                    Pair("evidenceKind", "observed-validation"),
                    Pair("staticEvidenceTier", "not-applicable"),
                    Pair("spanKind", "safe-artifact-placeholder"),
                    Pair("status", observation.Status),
                    Pair("artifactId", observation.ArtifactId),
                    Pair("artifactDigest", observation.ArtifactDigest),
                    Pair("observedAt", observation.ObservedAt.ToString("O")),
                    Pair("expiresAt", observation.ExpiresAt.ToString("O")),
                    Pair("evaluatedAt", observation.EvaluatedAt.ToString("O")),
                    Pair("validatorId", observation.ValidatorId),
                    Pair("validatorVersion", observation.ValidatorVersion),
                    Pair("engine", observation.TargetContext.Engine),
                    Pair("serverRole", observation.TargetContext.ServerRole),
                    Pair("databaseRole", observation.TargetContext.DatabaseRole),
                    Pair("schemaRole", observation.TargetContext.SchemaRole),
                    Pair("executionMode", observation.TargetContext.ExecutionMode)
                ]),
                [],
                [],
                observation.Limitations.Concat(SqlValidationObservationLimitations()).Distinct(StringComparer.Ordinal).ToArray(),
                nameof(SqlValidationSummaryReader),
                SqlValidationSummaryReader.SchemaVersion,
                "observed-validation"))
            .OrderBy(finding => finding.FindingId, StringComparer.Ordinal)
            .ToArray();
        var gaps = composition.Gaps.Select(gap => new ReleaseReviewGap(
                StableId("gap", "sqlValidationObservations", gap.GapId),
                gap.Code,
                "sqlValidationObservations",
                null,
                gap.RuleId,
                EvidenceTiers.Tier4Unknown,
                ReleaseReviewClassifications.PartialAnalysis,
                gap.Message,
                [],
                [],
                [],
                CombinedReportHelpers.SortedMetadata(gap.Metadata
                    .Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))
                    .Append(Pair("evidenceKind", "observed-validation-gap")))))
            .OrderBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();

        var status = findings.Length > 0 ? ReleaseReviewStatuses.Available : ReleaseReviewStatuses.Unavailable;
        return new ReleaseReviewSection(status, findings, gaps, SqlValidationObservationLimitations());
    }

    private static IReadOnlyList<string> SqlValidationObservationLimitations() =>
    [
        "Observed SQL validation is point-in-time, assertion-specific validator evidence and is not a static evidence tier.",
        "Observed-pass does not establish continuing state, safe execution, complete procedure success, release approval, or DBA attestation.",
        "TraceMap does not connect to a database, execute SQL, or ingest raw validation output."
    ];

    private static string SqlValidationSourceKey(string repository, string commitSha, SqlValidationTargetContext context) =>
        string.Join('\u001f', repository, commitSha, context.Engine, context.ServerRole, context.DatabaseRole, context.SchemaRole, context.ExecutionMode);

    private static ReleaseReviewFinding SqlEvidenceFinding(
        string sourceLabel,
        string kind,
        string displayName,
        string classification,
        SqlRunbookEvidence evidence,
        IEnumerable<KeyValuePair<string, string?>> metadata)
    {
        var safeMetadata = SqlEvidenceMetadata(evidence, metadata.Append(Pair("evidenceKind", kind)));
        return new ReleaseReviewFinding(
            StableId("finding", "sqlEvidence", sourceLabel, kind, evidence.RuleId, evidence.FilePath,
                evidence.LineSpan.StartLine.ToString(), evidence.LineSpan.EndLine.ToString(), displayName,
                string.Join(',', evidence.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal))),
            "sqlEvidence",
            sourceLabel,
            classification,
            evidence.RuleId,
            evidence.EvidenceTier,
            evidence.CommitSha,
            displayName,
            evidence.FilePath,
            evidence.LineSpan.StartLine,
            evidence.LineSpan.EndLine,
            safeMetadata,
            evidence.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            [],
            evidence.Limitations.Concat(SqlEvidenceLimitations()).Distinct(StringComparer.Ordinal).ToArray(),
            evidence.ExtractorId,
            evidence.ExtractorVersion,
            evidence.Coverage);
    }

    private static ReleaseReviewGap SqlEvidenceGap(
        string sourceLabel,
        string kind,
        string message,
        SqlRunbookEvidence evidence,
        IReadOnlyList<string>? supportingFindingIds = null)
    {
        return new ReleaseReviewGap(
            StableId("gap", "sqlEvidence", sourceLabel, kind, evidence.RuleId, evidence.FilePath,
                evidence.LineSpan.StartLine.ToString(), evidence.LineSpan.EndLine.ToString(), message,
                string.Join(',', evidence.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal))),
            kind,
            "sqlEvidence",
            sourceLabel,
            evidence.RuleId,
            evidence.EvidenceTier,
            evidence.EvidenceTier == EvidenceTiers.Tier4Unknown ? ReleaseReviewClassifications.PartialAnalysis : ReleaseReviewClassifications.ReviewRecommended,
            message,
            supportingFindingIds ?? [],
            evidence.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            [],
            SqlEvidenceMetadata(evidence, []));
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SqlEvidenceMetadata(
        SqlRunbookEvidence evidence,
        IEnumerable<KeyValuePair<string, string?>> metadata)
    {
        return CombinedReportHelpers.SortedMetadata(metadata
            .Append(Pair("coverage", evidence.Coverage))
            .Append(Pair("extractorId", evidence.ExtractorId))
            .Append(Pair("extractorVersion", evidence.ExtractorVersion))
            .Append(Pair("commitSha", evidence.CommitSha))
            .Append(Pair("filePath", evidence.FilePath))
            .Append(Pair("startLine", evidence.LineSpan.StartLine.ToString()))
            .Append(Pair("endLine", evidence.LineSpan.EndLine.ToString()))
            .Append(Pair("upstreamLimitations", evidence.Limitations.Count == 0 ? null : string.Join(" | ", evidence.Limitations))));
    }

    private static IReadOnlyList<string> SqlEvidenceLimitations() =>
    [
        "TraceMap does not execute SQL or establish runtime reachability, production database state, effective permissions, deployment, or release approval.",
        "SQL runway evidence does not provide an execution-safety conclusion or replace DBA/operator judgment.",
        "Raw SQL, connection strings, credentials, scheduled command bodies, private infrastructure identities, and local absolute paths are omitted."
    ];

    private static ReleaseReviewSection BuildPackageSection(ReleaseReviewOptions options, IReadOnlyList<string> scopes, ReleaseReviewSection topChangedSurfaces)
    {
        var packageFindings = topChangedSurfaces.Findings
            .Where(finding => finding.Metadata.Any(pair => pair.Key == "surfaceKind" && pair.Value == "package-config")
                || string.Equals(finding.Metadata.FirstOrDefault(pair => pair.Key == "factType").Value, FactTypes.PackageReferenced, StringComparison.Ordinal))
            .Select(ToPackageFinding)
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
        if (!requested)
        {
            return new ReleaseReviewSection(ReleaseReviewStatuses.NotRequested, [], [], ["Section is outside the requested release-review scope."]);
        }

        var gap = Gap("section", "WorkflowUnavailable", section, null, SectionRuleId, ReleaseReviewClassifications.UnknownAnalysisGap, message);
        return new ReleaseReviewSection(ReleaseReviewStatuses.Unavailable, [], [gap], FutureWorkflowLimitations);
    }

    private static ReleaseReviewSummary BuildSummary(
        int sourceCount,
        ReleaseReviewSection topChangedSurfaces,
        ReleaseReviewSection contractImpact,
        ReleaseReviewSection apiDtoChanges,
        ReleaseReviewSection sqlSchemaImpact,
        ReleaseReviewSection sqlEvidence,
        ReleaseReviewSection sqlValidationObservations,
        ReleaseReviewSection accessEvidence,
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
                sqlEvidence,
                sqlValidationObservations,
                accessEvidence,
                packageImpact,
                pathContext,
                reverseContext
            }
            .SelectMany(section => section.Findings)
            .ToArray();
        var actionableCount = allFindings.Count(IsActionableFinding);
        var reviewCount = allFindings.Count(finding => !IsActionableFinding(finding)
            && finding.Classification != ReleaseReviewClassifications.NoActionableEvidence);
        var rollupFindings = allFindings
            .Where(finding => finding.Classification != ReleaseReviewClassifications.NoActionableEvidence)
            .ToArray();
        var rollup = SelectRollup(gaps, rollupFindings, truncated);
        var message = rollup switch
        {
            ReleaseReviewClassifications.ActionableStaticEvidence => "Actionable static evidence is present; review the cited findings and limitations.",
            ReleaseReviewClassifications.ReviewRecommended => "Review-tier evidence is present; inspect whether each cited row is static or separately observed and apply its limitations.",
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
            sourceCount,
            topChangedSurfaces.Findings.Count,
            contractImpact.Findings.Count,
            apiDtoChanges.Findings.Count,
            sqlSchemaImpact.Findings.Count,
            sqlEvidence.Findings.Count,
            sqlValidationObservations.Findings.Count,
            accessEvidence.Findings.Count,
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
        return BuildChecklistItems(summary, findings, gaps)
            .Take(maxChecklistItems)
            .ToArray();
    }

    private static IReadOnlyList<ReleaseReviewChecklistItem> BuildChecklistItems(
        ReleaseReviewSummary summary,
        IReadOnlyList<ReleaseReviewFinding> findings,
        IReadOnlyList<ReleaseReviewGap> gaps)
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
            var severity = IsActionableFinding(finding)
                ? "must_review"
                : finding.Classification == ReleaseReviewClassifications.NoActionableEvidence
                    ? "informational"
                    : "should_review";
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

    private static ReleaseReviewFinding FromReverseRoot(CombinedReverseRoot root, IReadOnlyDictionary<string, string?> commitBySource)
    {
        return new ReleaseReviewFinding(
            StableId("finding", "reverseContext", root.RootId),
            "reverseContext",
            root.SourceLabel,
            root.Classification,
            root.RuleIds.OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? "combined.reverse.root.v1",
            root.EvidenceTiers.OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? EvidenceTiers.Tier4Unknown,
            CommitForSource(root.SourceLabel, commitBySource),
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

    private static ReleaseReviewFinding FromReversePath(CombinedReversePath path, IReadOnlyDictionary<string, string?> commitBySource)
    {
        var sourceLabel = path.Nodes.FirstOrDefault()?.SourceLabel;
        return new ReleaseReviewFinding(
            StableId("finding", "reverseContext", path.PathId),
            "reverseContext",
            sourceLabel,
            path.Classification,
            path.RuleIds.OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? "combined.reverse.path.v1",
            path.EvidenceTiers.OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? EvidenceTiers.Tier4Unknown,
            CommitForSource(sourceLabel, commitBySource),
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

    private static string? CommitForSource(string? sourceLabel, IReadOnlyDictionary<string, string?> commitBySource)
    {
        if (!string.IsNullOrWhiteSpace(sourceLabel) && commitBySource.TryGetValue(sourceLabel, out var commitSha))
        {
            return commitSha;
        }

        var commits = commitBySource.Values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
        return commits.Length == 1 ? commits[0] : null;
    }

    private static ReleaseReviewFinding ToPackageFinding(ReleaseReviewFinding finding)
    {
        return finding with
        {
            FindingId = StableId("finding", "packageImpact", finding.FindingId),
            Section = "packageImpact",
            Metadata = CombinedReportHelpers.SortedMetadata([
                Pair("derivedFromFindingHash", CombinedReportHelpers.Hash(finding.FindingId, 16)),
                .. finding.Metadata.Select(pair => Pair(pair.Key, pair.Value))
            ]),
            Limitations = finding.Limitations
                .Concat(["Package impact reuses static package surface evidence and does not claim compatibility, vulnerability, or lockfile-resolution impact."])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray()
        };
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

    internal static async Task<IReadOnlyList<SqlEvidenceInput>> ReadSqlEvidenceInputsAsync(
        string path,
        string indexKind,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ReadOnlyConnectionString(path));
        await connection.OpenAsync(cancellationToken);
        var table = indexKind == "combined" ? "combined_facts" : "facts";
        var hasExtractorId = await ColumnExistsAsync(connection, table, "extractor_id", cancellationToken);
        var hasExtractorVersion = await ColumnExistsAsync(connection, table, "extractor_version", cancellationToken);
        var extractorIdColumn = hasExtractorId ? "facts.extractor_id" : "null";
        var extractorVersionColumn = hasExtractorVersion ? "facts.extractor_version" : "null";
        var rows = new List<SqlEvidenceFactRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = indexKind == "combined"
            ? $"""
                select sources.label, sources.manifest_json,
                       facts.combined_fact_id, facts.scan_id, facts.repo, facts.commit_sha, facts.project_path,
                       facts.fact_type, facts.rule_id, facts.evidence_tier, facts.source_symbol, facts.target_symbol,
                       facts.contract_element, facts.file_path, facts.start_line, facts.end_line, facts.snippet_hash,
                       {extractorIdColumn}, {extractorVersionColumn}, facts.properties_json
                from combined_facts facts
                join index_sources sources on sources.source_index_id = facts.source_index_id
                where facts.rule_id like 'database.sql.%' or facts.rule_id like 'database.postgres.%'
                order by sources.label, facts.file_path, facts.start_line, facts.combined_fact_id;
                """
            : $"""
                select manifest.repo, manifest.manifest_json,
                       facts.fact_id, facts.scan_id, facts.repo, facts.commit_sha, facts.project_path,
                       facts.fact_type, facts.rule_id, facts.evidence_tier, facts.source_symbol, facts.target_symbol,
                       facts.contract_element, facts.file_path, facts.start_line, facts.end_line, facts.snippet_hash,
                       {extractorIdColumn}, {extractorVersionColumn}, facts.properties_json
                from facts
                cross join scan_manifest manifest
                where facts.rule_id like 'database.sql.%' or facts.rule_id like 'database.postgres.%'
                order by facts.file_path, facts.start_line, facts.fact_id;
                """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var manifest = DeserializeManifest(StringOrNull(reader, 1))
                ?? throw new InvalidDataException("SQL evidence input has an invalid scan manifest.");
            var extractorId = StringOrNull(reader, 17);
            var extractorVersion = StringOrNull(reader, 18);
            var properties = ParseProperties(StringOrNull(reader, 19));
            var evidence = new EvidenceSpan(
                StringOrDefault(reader, 13, "unknown"),
                reader.GetInt32(14),
                reader.GetInt32(15),
                StringOrNull(reader, 16),
                extractorId ?? "unknown",
                extractorVersion ?? "unknown");
            var fact = new CodeFact(
                StringOrDefault(reader, 2, "unknown"),
                StringOrDefault(reader, 3, manifest.ScanId),
                StringOrDefault(reader, 4, manifest.RepoName),
                StringOrDefault(reader, 5, manifest.CommitSha),
                StringOrNull(reader, 6),
                StringOrDefault(reader, 7, FactTypes.AnalysisGap),
                StringOrDefault(reader, 8, RuleIds.DatabaseSqlContextGap),
                StringOrDefault(reader, 9, EvidenceTiers.Tier4Unknown),
                StringOrNull(reader, 10),
                StringOrNull(reader, 11),
                StringOrNull(reader, 12),
                evidence,
                properties);
            if (!SqlRunwayRuleIds.Contains(fact.RuleId))
            {
                continue;
            }
            var sourceLabel = indexKind == "single" ? "single" : StringOrDefault(reader, 0, manifest.RepoName);
            rows.Add(new SqlEvidenceFactRow(sourceLabel, manifest, fact,
                !string.IsNullOrWhiteSpace(extractorId) && !string.IsNullOrWhiteSpace(extractorVersion)));
        }

        return rows
            .GroupBy(row => row.SourceLabel, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SqlEvidenceInput(
                group.Key,
                new ScanResult(group.First().Manifest, group.Select(row => row.Fact).ToArray(), []),
                group.All(row => row.ProvenanceCompatible)))
            .ToArray();
    }

    internal static async Task<AccessEvidencePresence> ReadAccessEvidencePresenceAsync(
        string path,
        string indexKind,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ReadOnlyConnectionString(path));
        await connection.OpenAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = indexKind == "combined"
            ? "select count(*) from combined_facts where fact_type like 'Access%' or rule_id like 'legacy.access.%';"
            : "select count(*) from facts where fact_type like 'Access%' or rule_id like 'legacy.access.%';";
        var count = Convert.ToInt64(await countCommand.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);

        var ids = new List<string>();
        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = indexKind == "combined"
            ? "select combined_fact_id from combined_facts where fact_type like 'Access%' or rule_id like 'legacy.access.%' order by combined_fact_id limit 12;"
            : "select fact_id from facts where fact_type like 'Access%' or rule_id like 'legacy.access.%' order by fact_id limit 12;";
        await using var reader = await idCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            ids.Add(StringOrDefault(reader, 0, "unknown"));
        return new AccessEvidencePresence(count, ids);
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
        var capabilitiesBySource = await ReadCombinedCapabilitySummariesAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select source_index_id,
                   label,
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
            var manifest = DeserializeManifest(StringOrNull(reader, 12));
            var sourceIndexId = StringOrDefault(reader, 0, "unknown");
            var label = StringOrDefault(reader, 1, "unknown");
            var scannerVersion = StringOrDefault(reader, 11, "unknown");
            extractorVersions[$"{label}:{scannerVersion}"] = scannerVersion;
            var gaps = manifest?.KnownGaps ?? [];
            var analysisLevel = StringOrDefault(reader, 9, "unknown");
            var buildStatus = StringOrDefault(reader, 10, "unknown");
            capabilitiesBySource.TryGetValue(sourceIndexId, out var capabilities);
            var sourceGaps = gaps
                .Concat(CapabilityGapCodes(capabilities ?? []))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            sources.Add(new ReleaseReviewSourceInfo(
                label,
                StringOrNull(reader, 2) ?? InferLanguage(scannerVersion),
                StringOrNull(reader, 3),
                NullIfUnknown(StringOrNull(reader, 4)),
                RepositoryIdentityHash(StringOrNull(reader, 6), StringOrNull(reader, 5)),
                StringOrNull(reader, 7),
                CoverageFrom(analysisLevel, buildStatus, sourceGaps),
                buildStatus,
                analysisLevel,
                sourceGaps,
                capabilities));
        }

        var warnings = CoverageWarnings(sources);
        return new ReleaseReviewSnapshot(side, "combined", sources, warnings.Count == 0 ? "Full" : "Reduced", warnings, extractorVersions.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
    }

    private static async Task<ReleaseReviewSnapshot> ReadSingleSnapshotAsync(SqliteConnection connection, string side, CancellationToken cancellationToken)
    {
        var capabilities = await ReadSingleCapabilitySummariesAsync(connection, cancellationToken);
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

        var manifest = DeserializeManifest(StringOrNull(reader, 6));
        var gaps = manifest?.KnownGaps ?? [];
        var scannerVersion = StringOrDefault(reader, 3, "unknown");
        var analysisLevel = StringOrDefault(reader, 4, "unknown");
        var buildStatus = StringOrDefault(reader, 5, "unknown");
        var sourceGaps = gaps
            .Concat(CapabilityGapCodes(capabilities))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var source = new ReleaseReviewSourceInfo(
            "single",
            InferLanguage(scannerVersion),
            StringOrNull(reader, 0),
            NullIfUnknown(StringOrNull(reader, 2)),
            RepositoryIdentityHash(manifest?.RemoteUrl, StringOrNull(reader, 1)),
            manifest?.ScanRootPathHash,
            CoverageFrom(analysisLevel, buildStatus, sourceGaps),
            buildStatus,
            analysisLevel,
            sourceGaps,
            capabilities);
        var warnings = CoverageWarnings([source]);
        return new ReleaseReviewSnapshot(side, "single", [source], warnings.Count == 0 ? "Full" : "Reduced", warnings, [new KeyValuePair<string, string>($"single:{scannerVersion}", scannerVersion)]);
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<ReleaseReviewCapabilitySummary>>> ReadCombinedCapabilitySummariesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var result = new SortedDictionary<string, List<ReleaseReviewCapabilitySummary>>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select facts.source_index_id,
                   facts.original_fact_id,
                   facts.rule_id,
                   facts.evidence_tier,
                   facts.properties_json
            from combined_facts facts
            join index_sources sources on sources.source_index_id = facts.source_index_id
            where facts.fact_type = 'AnalyzerCapabilityDiagnostic'
            order by facts.source_index_id, facts.file_path, facts.start_line, facts.original_fact_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var sourceIndexId = StringOrDefault(reader, 0, "unknown");
            var factId = StringOrDefault(reader, 1, "unknown");
            var summary = CapabilitySummaryFromProperties(
                factId,
                StringOrDefault(reader, 2, RuleIds.AnalyzerCapabilityDownstreamCoverage),
                StringOrDefault(reader, 3, EvidenceTiers.Tier4Unknown),
                ParseProperties(StringOrNull(reader, 4)));
            if (!result.TryGetValue(sourceIndexId, out var list))
            {
                list = [];
                result[sourceIndexId] = list;
            }

            list.Add(summary);
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ReleaseReviewCapabilitySummary>)pair.Value
                .OrderBy(item => item.CapabilityCode, StringComparer.Ordinal)
                .ThenBy(item => item.CapabilityState, StringComparer.Ordinal)
                .ThenBy(item => item.RuleId, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyList<ReleaseReviewCapabilitySummary>> ReadSingleCapabilitySummariesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var result = new List<ReleaseReviewCapabilitySummary>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select fact_id,
                   rule_id,
                   evidence_tier,
                   properties_json
            from facts
            where fact_type = 'AnalyzerCapabilityDiagnostic'
            order by file_path, start_line, fact_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(CapabilitySummaryFromProperties(
                StringOrDefault(reader, 0, "unknown"),
                StringOrDefault(reader, 1, RuleIds.AnalyzerCapabilityDownstreamCoverage),
                StringOrDefault(reader, 2, EvidenceTiers.Tier4Unknown),
                ParseProperties(StringOrNull(reader, 3))));
        }

        return result
            .OrderBy(item => item.CapabilityCode, StringComparer.Ordinal)
            .ThenBy(item => item.CapabilityState, StringComparer.Ordinal)
            .ThenBy(item => item.RuleId, StringComparer.Ordinal)
            .ToArray();
    }

    private static ReleaseReviewCapabilitySummary CapabilitySummaryFromProperties(string factId, string ruleId, string evidenceTier, IReadOnlyDictionary<string, string> properties)
    {
        var support = SplitSafeList(properties.GetValueOrDefault("supportingFactIds"));
        var supportingFactIds = new[] { factId }
            .Concat(support)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(12)
            .ToArray();
        return new ReleaseReviewCapabilitySummary(
            properties.GetValueOrDefault("capabilityCode") ?? "unknown",
            properties.GetValueOrDefault("capabilityState") ?? "unknown",
            properties.GetValueOrDefault("coverageEffect") ?? "unknown-gap",
            ruleId,
            evidenceTier,
            properties.GetValueOrDefault("schemaVersion") ?? "unknown",
            supportingFactIds);
    }

    private static IEnumerable<string> CapabilityGapCodes(IReadOnlyList<ReleaseReviewCapabilitySummary> capabilities)
    {
        foreach (var item in capabilities)
        {
            if (!string.Equals(item.SchemaVersion, AnalyzerCapabilityDiagnosticExtractor.SchemaVersion, StringComparison.Ordinal))
            {
                yield return $"AnalyzerCapabilitySchemaUnsupported:{item.SchemaVersion}";
            }

            if (item.CapabilityState is "reduced" or "unavailable" or "unknown"
                || item.CoverageEffect is "reduced-semantic" or "syntax-only" or "structural-only" or "config-only" or "unknown-gap")
            {
                yield return $"AnalyzerCapability:{item.CapabilityCode}:{item.CapabilityState}:{item.CoverageEffect}";
            }
        }
    }

    private static IReadOnlyList<string> SplitSafeList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => item.StartsWith("fact-", StringComparison.Ordinal))
                .OrderBy(item => item, StringComparer.Ordinal)
                .Take(12)
                .ToArray();
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

            foreach (var group in (beforeSource.AnalyzerCapabilities ?? []).Concat(afterSource.AnalyzerCapabilities ?? [])
                .Where(IsReducedCapability)
                .GroupBy(item => $"{item.CapabilityCode}|{item.CapabilityState}|{item.CoverageEffect}|{item.RuleId}|{item.EvidenceTier}", StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var item = group.First();
                var classification = item.CapabilityState == "unknown" || item.CoverageEffect == "unknown-gap"
                    ? ReleaseReviewClassifications.UnknownAnalysisGap
                    : ReleaseReviewClassifications.PartialAnalysis;
                var message = $"Source `{source.Key}` has `{item.CapabilityCode}` analyzer capability `{item.CapabilityState}` with `{item.CoverageEffect}` coverage; no-evidence conclusions remain coverage-relative.";
                gaps.Add(new ReleaseReviewGap(
                    StableId("gap", "sourceCoverage", source.Key, item.CapabilityCode, item.CapabilityState, item.CoverageEffect),
                    "ToolchainCapabilityReducedCoverage",
                    "sourceCoverage",
                    source.Key,
                    item.RuleId,
                    item.EvidenceTier,
                    classification,
                    message,
                    [],
                    group.SelectMany(summary => summary.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(12).ToArray(),
                    [],
                    CombinedReportHelpers.SortedMetadata([
                        Pair("capabilityCode", item.CapabilityCode),
                        Pair("capabilityState", item.CapabilityState),
                        Pair("coverageEffect", item.CoverageEffect),
                        Pair("schemaVersion", item.SchemaVersion)
                    ])));
            }

            foreach (var group in (beforeSource.AnalyzerCapabilities ?? []).Concat(afterSource.AnalyzerCapabilities ?? [])
                .Where(item => !string.Equals(item.SchemaVersion, AnalyzerCapabilityDiagnosticExtractor.SchemaVersion, StringComparison.Ordinal))
                .GroupBy(item => $"{item.SchemaVersion}|{item.RuleId}", StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var item = group.First();
                gaps.Add(new ReleaseReviewGap(
                    StableId("gap", "sourceCoverage", source.Key, "CapabilitySchema", item.SchemaVersion),
                    "AnalyzerCapabilitySchemaUnsupported",
                    "sourceCoverage",
                    source.Key,
                    item.RuleId,
                    EvidenceTiers.Tier4Unknown,
                    ReleaseReviewClassifications.UnknownAnalysisGap,
                    $"Source `{source.Key}` has analyzer capability schema `{item.SchemaVersion}`; release review preserved the fact and emitted a compatibility gap instead of silently dropping it.",
                    [],
                    group.SelectMany(summary => summary.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(12).ToArray(),
                    [],
                    CombinedReportHelpers.SortedMetadata([
                        Pair("schemaVersion", item.SchemaVersion),
                        Pair("expectedSchemaVersion", AnalyzerCapabilityDiagnosticExtractor.SchemaVersion)
                    ])));
            }
        }

        return gaps;
    }

    private static bool IsReducedCapability(ReleaseReviewCapabilitySummary item)
    {
        return item.CapabilityState is "reduced" or "unavailable" or "unknown"
            || item.CoverageEffect is "reduced-semantic" or "syntax-only" or "structural-only" or "config-only" or "unknown-gap";
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

    private static async Task<IReadOnlyList<SingleComparableFact>> ReadSingleComparableFactsAsync(string path, string side, ReleaseReviewOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.Source) && !options.Source.Equals("single", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        await using var connection = new SqliteConnection(ReadOnlyConnectionString(path));
        await connection.OpenAsync(cancellationToken);
        await using var manifestCommand = connection.CreateCommand();
        manifestCommand.CommandText = "select commit_sha from scan_manifest order by scanned_at desc limit 1;";
        var commitSha = NullIfUnknown(await manifestCommand.ExecuteScalarAsync(cancellationToken) as string);
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
              'ConfigBinding',
              'AnalyzerCapabilityDiagnostic'
            )
            order by fact_type, file_path, start_line, fact_id;
            """;
        var rows = new List<SingleComparableFact>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var factId = StringOrDefault(reader, 0, "unknown");
            var factType = StringOrDefault(reader, 1, "unknown");
            var ruleId = StringOrDefault(reader, 2, "release.review.section.v1");
            var evidenceTier = StringOrDefault(reader, 3, EvidenceTiers.Tier4Unknown);
            var filePath = StringOrNull(reader, 4);
            var startLine = IntOrNull(reader, 5);
            var endLine = IntOrNull(reader, 6);
            var targetSymbol = StringOrNull(reader, 7);
            var contractElement = StringOrNull(reader, 8);
            var properties = ParseProperties(StringOrNull(reader, 9));
            var metadata = SafeFactMetadata(factType, properties);
            if (!SingleFactMatchesSelectors(factType, targetSymbol, contractElement, metadata, options.Endpoint, options.Surface, options.SurfaceName))
            {
                continue;
            }

            var stableInput = string.Join("|", factType, ruleId, targetSymbol, contractElement, string.Join(";", metadata.Select(pair => $"{pair.Key}={pair.Value}")));
            var evidenceInput = string.Join("|", stableInput, CombinedReportHelpers.SafePath(filePath), startLine, endLine);
            var finding = new ReleaseReviewFinding(
                StableId("finding", "topChangedSurfaces", side, factId),
                "topChangedSurfaces",
                "single",
                side == "after" ? CombinedDependencyDiffClassifications.Added : CombinedDependencyDiffClassifications.Removed,
                ruleId,
                evidenceTier,
                commitSha,
                DisplayName(factType, targetSymbol, contractElement, metadata),
                SafeReportPath(filePath),
                startLine,
                endLine,
                CombinedReportHelpers.SortedMetadata([Pair("coverageRelative", "true"), Pair("factType", factType), .. metadata.Select(pair => Pair(pair.Key, pair.Value))]),
                [factId],
                [],
                ["Single-index release review compares indexed fact evidence only; added/removed evidence is coverage-relative."]);
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

    private static void AddChecklistTruncationGapIfNeeded(List<ReleaseReviewGap> gaps, IReadOnlyList<ReleaseReviewFinding> findings, int maxChecklistItems)
    {
        var provisionalSummary = new ReleaseReviewSummary(
            RollupClassification: ReleaseReviewClassifications.NoActionableEvidence,
            RuleId: RollupRuleId,
            SourceCount: 0,
            TopChangedSurfaceCount: 0,
            ContractFindingCount: 0,
            ApiDtoFindingCount: 0,
            SqlSchemaFindingCount: 0,
            SqlEvidenceFindingCount: 0,
            SqlValidationObservationFindingCount: 0,
            AccessEvidenceFindingCount: 0,
            PackageFindingCount: 0,
            PathFindingCount: 0,
            ReverseFindingCount: 0,
            ActionableFindingCount: 0,
            ReviewFindingCount: 0,
            GapCount: gaps.Count,
            Truncated: false,
            Message: "Checklist truncation preflight.");
        var checklistCount = BuildChecklistItems(provisionalSummary, findings, SortGaps(gaps)).Count;
        if (checklistCount <= maxChecklistItems)
        {
            return;
        }

        gaps.Add(Gap(
            "truncation",
            "TruncatedByLimit",
            "checklist",
            null,
            TruncationRuleId,
            ReleaseReviewClassifications.TruncatedByLimit,
            $"Reviewer checklist omitted {checklistCount - maxChecklistItems} rows because --max-checklist-items was reached."));
    }

    private static ReleaseReviewGap[] CapGaps(List<ReleaseReviewGap> gaps, int maxGaps)
    {
        var sorted = SortGaps(gaps);
        if (sorted.Length <= maxGaps)
        {
            return sorted;
        }

        var omitted = sorted.Length - maxGaps + 1;
        var truncationGap = Gap(
            "truncation",
            "TruncatedByLimit",
            "gaps",
            null,
            TruncationRuleId,
            ReleaseReviewClassifications.TruncatedByLimit,
            $"Analysis gaps omitted {omitted} rows because --max-gaps was reached.");
        return new[] { truncationGap }
            .Concat(sorted.Where(gap => gap.GapId != truncationGap.GapId).Take(maxGaps - 1))
            .OrderBy(GapSeverityRank)
            .ThenBy(gap => gap.Section, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
    }

    private static ReleaseReviewGap[] SortGaps(IEnumerable<ReleaseReviewGap> gaps)
    {
        return gaps
            .DistinctBy(gap => gap.GapId)
            .OrderBy(GapSeverityRank)
            .ThenBy(gap => gap.Section, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
    }

    private static ReleaseReviewSection FilterSectionFindings(ReleaseReviewSection section, IReadOnlyList<ReleaseReviewFinding> allowed)
    {
        var allowedIds = allowed.Select(finding => finding.FindingId).ToHashSet(StringComparer.Ordinal);
        var findings = section.Findings.Where(finding => allowedIds.Contains(finding.FindingId)).ToArray();
        var omitted = section.Findings.Count - findings.Length;
        return section with
        {
            Status = omitted > 0 ? ReleaseReviewStatuses.Truncated : section.Status,
            Findings = findings,
            OmittedCount = section.OmittedCount + omitted
        };
    }

    private static ReleaseReviewSection FilterSectionGaps(ReleaseReviewSection section, IReadOnlyList<ReleaseReviewGap> allowed)
    {
        var allowedIds = allowed.Select(gap => gap.GapId).ToHashSet(StringComparer.Ordinal);
        var gaps = section.Gaps.Where(gap => allowedIds.Contains(gap.GapId)).ToArray();
        var omitted = section.Gaps.Count - gaps.Length;
        return section with
        {
            Status = omitted > 0 && section.Status == ReleaseReviewStatuses.Available
                ? ReleaseReviewStatuses.Truncated
                : section.Status,
            Gaps = gaps,
            OmittedCount = section.OmittedCount + omitted
        };
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
        if (finding.Metadata.Any(pair => pair.Key == "coverageRelative" && pair.Value == "true"))
        {
            return false;
        }

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

        return string.Join(",", mapped.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));
    }

    private static bool ImpactContextRequested(IReadOnlyList<string> scopes, bool includePaths)
    {
        return ScopeEnabled(scopes, "sources")
            || ScopeEnabled(scopes, "coverage")
            || ScopeEnabled(scopes, "surfaces")
            || (includePaths && ScopeEnabled(scopes, "paths"));
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
        return RenderMarkdown(report, null);
    }

    private static string RenderMarkdown(ReleaseReviewDocument report, ReleaseReviewPriorityResult? priority)
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
        if (priority is not null)
        {
            RenderReviewPriority(builder, priority);
        }

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
        RenderSection(builder, "SQL Runway Evidence", report.SqlEvidence);
        RenderSection(builder, "SQL Validation Observations", report.SqlValidationObservations);
        RenderSection(builder, "Access Design Evidence", report.AccessEvidence);
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

    private static void RenderReviewPriority(StringBuilder builder, ReleaseReviewPriorityResult priority)
    {
        builder.AppendLine("## Review Priority");
        builder.AppendLine();
        builder.AppendLine($"- Status: `{Cell(priority.Summary.Status)}`");
        builder.AppendLine($"- Model version: `{Cell(priority.Summary.ModelVersion)}`");
        builder.AppendLine($"- Attention level: `{Cell(priority.Summary.AttentionLevel)}`");
        builder.AppendLine("- Priority score: `n/a`");
        builder.AppendLine($"- Complete: `{priority.Summary.Complete}`");
        builder.AppendLine($"- Contributing sections: {Cell(string.Join(", ", priority.Summary.ContributingSections))}");
        builder.AppendLine($"- Limited sections: {Cell(string.Join(", ", priority.Summary.LimitedSections))}");
        builder.AppendLine();

        if (priority.Rows.Count == 0)
        {
            builder.AppendLine("No priority rows generated.");
            builder.AppendLine();
        }
        else
        {
            builder.AppendLine("| Severity hint | Row | Kind | Section | Source | Classification | Components | Limitations |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var row in priority.Rows.Take(25))
            {
                builder.AppendLine($"| `{Cell(row.SeverityHint)}` | `{Cell(row.RowId)}` | {Cell(row.RowKind)} | {Cell(row.Section)} | {Cell(row.SourceLabel)} | `{Cell(row.Classification)}` | {Cell(ComponentSummary(row.Components))} | {Cell(string.Join("; ", row.Limitations.Take(3)))} |");
            }

            if (priority.Rows.Count > 25)
            {
                builder.AppendLine();
                builder.AppendLine($"Priority rows omitted from Markdown table: `{priority.Rows.Count - 25}`");
            }

            builder.AppendLine();
        }

        builder.AppendLine("### Review Priority Limitations");
        builder.AppendLine();
        foreach (var limitation in priority.Summary.Limitations)
        {
            builder.AppendLine($"- {Cell(limitation)}");
        }

        builder.AppendLine();
    }

    private static string ComponentSummary(IReadOnlyList<ReviewPriorityComponent> components)
    {
        return string.Join(
            "; ",
            components.Select(component => $"{component.ComponentKind}:{component.Direction}:{component.RuleId}:{component.EvidenceTier}"));
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

    private static string CoverageFrom(string? analysisLevel, string? buildStatus, IReadOnlyList<string> knownGaps)
    {
        return (analysisLevel?.Contains("Reduced", StringComparison.OrdinalIgnoreCase) ?? false)
            || buildStatus is "FailedOrPartial" or "Failed"
            || knownGaps.Count > 0
            ? "Reduced"
            : "Full";
    }

    private static string? RepositoryIdentityHash(string? remoteUrl, string? repoName)
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

    private static string InferLanguage(string? scannerVersion)
    {
        if (string.IsNullOrWhiteSpace(scannerVersion))
        {
            return "csharp";
        }

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

    private static ScanManifest? DeserializeManifest(string? manifestJson)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScanManifest>(manifestJson, CombinedDependencyReporter.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string? json)
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
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string? StringOrNull(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static string StringOrDefault(SqliteDataReader reader, int ordinal, string fallback)
    {
        return StringOrNull(reader, ordinal) ?? fallback;
    }

    private static int? IntOrNull(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SafeFactMetadata(string factType, IReadOnlyDictionary<string, string> properties)
    {
        var allowed = new[]
        {
            "httpMethod",
            "httpMethods",
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
        if (factType == FactTypes.AnalyzerCapabilityDiagnostic)
        {
            allowed =
            [
                "capabilityCode",
                "capabilityKind",
                "capabilityState",
                "coverageEffect",
                "guidanceCode",
                "limitationCode",
                "schemaVersion",
                "sourceScope"
            ];
        }

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

        if (key.EndsWith("Hash", StringComparison.Ordinal)
            || key is "operation" or "sourceKind" or "ecosystem" or "httpMethod" or "httpMethods" or "surfaceKind" or "normalizedPathKey" or "normalizedPathTemplate"
                or "capabilityCode" or "capabilityKind" or "capabilityState" or "coverageEffect" or "guidanceCode" or "limitationCode" or "schemaVersion" or "sourceScope")
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
            FactTypes.AnalyzerCapabilityDiagnostic => "analyzer-capability",
            _ => "fact"
        };
    }

    private static bool SingleFactMatchesSelectors(
        string factType,
        string? targetSymbol,
        string? contractElement,
        IReadOnlyList<KeyValuePair<string, string>> metadata,
        string? endpoint,
        string? surface,
        string? surfaceName)
    {
        if (!string.IsNullOrWhiteSpace(endpoint) && !SingleEndpointMatches(metadata, targetSymbol, contractElement, endpoint))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(surface) && !string.Equals(SurfaceKindForFact(factType), surface.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(surfaceName) && !SingleSurfaceNameMatches(metadata, targetSymbol, contractElement, surfaceName))
        {
            return false;
        }

        return true;
    }

    private static bool SingleEndpointMatches(IReadOnlyList<KeyValuePair<string, string>> metadata, string? targetSymbol, string? contractElement, string endpoint)
    {
        var selector = ParseEndpointSelector(endpoint);
        var method = MetadataValue(metadata, "httpMethod") ?? MetadataValue(metadata, "httpMethods") ?? contractElement;
        var pathKey = MetadataValue(metadata, "normalizedPathKey")
            ?? MetadataValue(metadata, "normalizedPathTemplate")
            ?? targetSymbol;
        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(pathKey))
        {
            return false;
        }

        var normalized = EndpointRouteNormalizer.Normalize(pathKey).PathKey;
        return string.Equals(method, selector.Method, StringComparison.OrdinalIgnoreCase)
            && string.Equals(normalized, selector.PathKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SingleSurfaceNameMatches(IReadOnlyList<KeyValuePair<string, string>> metadata, string? targetSymbol, string? contractElement, string selector)
    {
        var trimmed = selector.Trim();
        return string.Equals(DisplayName("fact", targetSymbol, contractElement, metadata), trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(targetSymbol, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(contractElement, trimmed, StringComparison.OrdinalIgnoreCase)
            || metadata.Any(pair => string.Equals(pair.Value, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static (string Method, string PathKey) ParseEndpointSelector(string value)
    {
        var trimmed = value.Trim();
        var parts = trimmed.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]) || parts[0].StartsWith('/'))
        {
            throw new ArgumentException("release-review --endpoint must be '<METHOD> <PATH_KEY>'.");
        }

        return (parts[0].ToUpperInvariant(), EndpointRouteNormalizer.Normalize(parts[1]).PathKey);
    }

    private static string? MetadataValue(IReadOnlyList<KeyValuePair<string, string>> metadata, string key)
    {
        return metadata.FirstOrDefault(pair => pair.Key == key).Value;
    }

    private static string DisplayName(string factType, string? targetSymbol, string? contractElement, IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        var path = metadata.FirstOrDefault(pair => pair.Key == "normalizedPathKey").Value;
        if (!string.IsNullOrWhiteSpace(path))
        {
            var method = metadata.FirstOrDefault(pair => pair.Key == "httpMethod").Value
                ?? metadata.FirstOrDefault(pair => pair.Key == "httpMethods").Value;
            return $"{method} {path}".Trim();
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

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select 1 from pragma_table_info($table_name) where name = $column_name limit 1;";
        command.Parameters.AddWithValue("$table_name", tableName);
        command.Parameters.AddWithValue("$column_name", columnName);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
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

        if (options.SqlValidationSummaryPaths is { Count: > 0 } && options.SqlValidationAsOf is null)
        {
            throw new ArgumentException("release-review SQL validation summaries require an explicit deterministic as-of instant.");
        }

        if (options.SqlValidationSummaryPaths is not { Count: > 0 } && options.SqlValidationAsOf is not null)
        {
            throw new ArgumentException("release-review SQL validation as-of requires at least one summary.");
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
