using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record ApiDtoContractDiffOptions(
    string BeforePath,
    string AfterPath,
    string OutputPath,
    string Format = "markdown",
    string? Scope = null,
    string? Source = null,
    string? Endpoint = null,
    string? Type = null,
    string? Property = null,
    string? ChangeKind = null,
    int MaxDiffRows = 1000,
    int MaxEvidenceRows = 500,
    int MaxGaps = 1000,
    bool ExitCode = false);

public sealed record ApiDtoContractDiffResult(
    ApiDtoContractDiffReport Report,
    string? MarkdownPath,
    string? JsonPath,
    bool HasActionableDiffs);

public sealed record ApiDtoContractDiffReport(
    string ReportType,
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    ApiDtoContractDiffQuery Query,
    ApiDtoContractDiffSnapshot BeforeSnapshot,
    ApiDtoContractDiffSnapshot AfterSnapshot,
    ApiDtoContractDiffSummary Summary,
    IReadOnlyList<ApiDtoContractSourcePair> SourcePairs,
    IReadOnlyList<ApiDtoContractDiffRow> EndpointDiffs,
    IReadOnlyList<ApiDtoContractDiffRow> DtoTypeDiffs,
    IReadOnlyList<ApiDtoContractDiffRow> DtoPropertyDiffs,
    IReadOnlyList<ApiDtoContractDiffRow> MethodDiffs,
    IReadOnlyList<ApiDtoContractDiffRow> RequestResponseDiffs,
    IReadOnlyList<ApiDtoContractDiffRow> RouteShapeDiffs,
    IReadOnlyList<ApiDtoContractDiffGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record ApiDtoContractDiffQuery(
    IReadOnlyList<string> Scopes,
    string? Source,
    string? Endpoint,
    string? Type,
    string? Property,
    string? ChangeKind,
    int MaxDiffRows,
    int MaxEvidenceRows,
    int MaxGaps,
    bool ExitCode,
    IReadOnlyList<string> IgnoredSelectors,
    string Algorithm,
    string AlgorithmVersion);

public sealed record ApiDtoContractDiffSnapshot(
    string Side,
    string IndexKind,
    IReadOnlyList<ApiDtoContractSourceInfo> Sources,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    IReadOnlyList<KeyValuePair<string, string>> ExtractorVersions);

public sealed record ApiDtoContractSourceInfo(
    string SourceLabel,
    string? Language,
    string? ScanId,
    string? CommitSha,
    string? RepositoryIdentityHash,
    string? RootPathHash,
    string Coverage,
    string BuildStatus,
    string AnalysisLevel,
    string? ScannerVersion,
    IReadOnlyList<string> GapCodes);

public sealed record ApiDtoContractSourcePair(
    string SourceLabel,
    ApiDtoContractSourceInfo? Before,
    ApiDtoContractSourceInfo? After,
    string Classification,
    IReadOnlyList<string> Caveats);

public sealed record ApiDtoContractDiffSummary(
    int SourcePairCount,
    int EndpointDiffCount,
    int DtoTypeDiffCount,
    int DtoPropertyDiffCount,
    int MethodDiffCount,
    int RequestResponseDiffCount,
    int RouteShapeDiffCount,
    int GapCount,
    bool Truncated,
    string RollupClassification,
    string Message);

public sealed record ApiDtoContractDiffRow(
    string DiffId,
    string RowKind,
    string ChangeType,
    string Classification,
    string Confidence,
    string StableKey,
    string RuleId,
    string? SourceLabel,
    ApiDtoContractEvidence? Before,
    ApiDtoContractEvidence? After,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> FactRuleIds,
    IReadOnlyList<ApiDtoContractFileSpan> FileSpans,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> CoverageCaveats,
    IReadOnlyList<string> Notes);

public sealed record ApiDtoContractEvidence(
    string? SourceLabel,
    string? CommitSha,
    string? ScanId,
    string? Language,
    string? DisplayName,
    string? EvidenceTier,
    string? FactRuleId,
    IReadOnlyList<ApiDtoContractFileSpan> FileSpans,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record ApiDtoContractFileSpan(
    string FilePath,
    int? StartLine,
    int? EndLine,
    string? SourceLabel);

public sealed record ApiDtoContractDiffGap(
    string GapId,
    string GapKind,
    string Section,
    string? SourceLabel,
    string RuleId,
    string EvidenceTier,
    string Classification,
    string Message,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<ApiDtoContractFileSpan> FileSpans,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

internal sealed record ApiDtoIndexRead(
    string Kind,
    ApiDtoContractDiffSnapshot Snapshot,
    IReadOnlyList<ApiDtoFactRow> Facts);

internal sealed record ApiDtoFactRow(
    string FactId,
    string? SourceIndexId,
    string SourceLabel,
    string? Language,
    string? ScanId,
    string? CommitSha,
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

internal sealed record ApiDtoComparableRow(
    string RowKind,
    string StableKey,
    string MetadataHash,
    ApiDtoContractEvidence Evidence,
    bool NeedsReview,
    IReadOnlyList<string> Notes);

public static class ApiDtoContractDiffClassifications
{
    public const string Added = nameof(Added);
    public const string Removed = nameof(Removed);
    public const string ChangedEvidence = nameof(ChangedEvidence);
    public const string AddedWithBeforeGap = nameof(AddedWithBeforeGap);
    public const string RemovedWithAfterGap = nameof(RemovedWithAfterGap);
    public const string NeedsReviewDiff = nameof(NeedsReviewDiff);
    public const string NoDiffEvidence = nameof(NoDiffEvidence);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
    public const string TruncatedByLimit = nameof(TruncatedByLimit);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
}

public static class ApiDtoContractDiffReporter
{
    private const string Version = "1.0";
    private const string SingleReportType = "api-dto-contract-diff-single";
    private const string CombinedReportType = "api-dto-contract-diff-combined";
    private const string Algorithm = "api-dto-contract-stable-evidence-diff";
    private const string AlgorithmVersion = "1.0";

    private const string EndpointRuleId = "api.dto.contract.diff.endpoint.v1";
    private const string DtoRuleId = "api.dto.contract.diff.dto.v1";
    private const string AttachmentRuleId = "api.dto.contract.diff.attachment.v1";
    private const string IdentityRuleId = "api.dto.contract.diff.identity.v1";
    private const string CoverageRuleId = "api.dto.contract.diff.coverage.v1";
    private const string SchemaRuleId = "api.dto.contract.diff.schema.v1";
    private const string SelectorRuleId = "api.dto.contract.diff.selector.v1";
    private const string TruncationRuleId = "api.dto.contract.diff.truncation.v1";

    private static readonly HashSet<string> ValidScopes = new(StringComparer.Ordinal)
    {
        "all",
        "endpoints",
        "dto-types",
        "dto-properties",
        "methods",
        "request-response",
        "route-shapes"
    };

    private static readonly HashSet<string> ValidChangeKinds = new(StringComparer.Ordinal)
    {
        "endpoint",
        "dto-type",
        "dto-property",
        "method",
        "request-response",
        "route-shape"
    };

    private static readonly IReadOnlyList<string> DefaultScopes =
    [
        "endpoints",
        "dto-types",
        "dto-properties",
        "methods",
        "request-response",
        "route-shapes"
    ];

    private static readonly IReadOnlyList<string> Limitations =
    [
        "API/DTO contract diff compares static TraceMap evidence only; it is not OpenAPI completeness, binary compatibility analysis, runtime traffic analysis, deployment reachability, auth behavior, or source-code diffing.",
        "Endpoint route evidence does not prove runtime base paths, proxies, auth, CORS, traffic, or handler execution.",
        "DTO type and member evidence is limited to indexed declarations, object shapes, and explicit serializer/schema facts; runtime serializer aliases, reflection, dynamic dispatch, and generated contracts are not inferred.",
        "Request/response attachment rows are emitted only when adapters provide credible endpoint-to-DTO attachment facts. Otherwise the report emits explicit attachment-evidence gaps.",
        "Reduced coverage, missing commit SHAs, source identity conflicts, duplicate identities, syntax-only evidence, or high fan-out property-only identity downgrade conclusions to review-tier or coverage-relative rows.",
        "Reports omit or hash raw URLs, local absolute paths, source snippets, raw SQL, config values, connection strings, and secret-looking values."
    ];

    private static readonly HashSet<string> GenericPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "type",
        "name",
        "status"
    };

    public static async Task<ApiDtoContractDiffResult> WriteAsync(ApiDtoContractDiffOptions options, CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(options, cancellationToken);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "contract-diff");
        var outputs = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "contract-diff-report.md",
            "contract-diff-report.json",
            report,
            RenderMarkdown,
            CombinedDependencyReporter.JsonOptions,
            cancellationToken);
        return new ApiDtoContractDiffResult(report, outputs.MarkdownPath, outputs.JsonPath, HasActionableDiffs(report));
    }

    public static async Task<ApiDtoContractDiffReport> BuildReportAsync(ApiDtoContractDiffOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var scopes = ParseScopes(options.Scope);
        var ignoredSelectors = IgnoredSelectors(options, scopes);
        var before = await ReadIndexAsync(options.BeforePath, "before", cancellationToken);
        var after = await ReadIndexAsync(options.AfterPath, "after", cancellationToken);
        if (!string.Equals(before.Kind, after.Kind, StringComparison.Ordinal))
        {
            throw new InvalidDataException("contract-diff mixed single and combined indexes are not supported in v1.");
        }

        var gaps = new List<ApiDtoContractDiffGap>();
        if (!await TableExistsInIndexAsync(options.BeforePath, RequiredFactTable(before.Kind), cancellationToken))
        {
            gaps.Add(Gap("schema", "SchemaGap", "schema", null, SchemaRuleId, ApiDtoContractDiffClassifications.UnknownAnalysisGap, "before input is missing required fact table."));
        }

        var sourcePairs = PairSources(before.Snapshot, after.Snapshot, options.Source, gaps);
        AddIdentityAndCoverageGaps(sourcePairs, gaps);

        var beforeRows = ProjectRows(before, scopes, options, gaps);
        var afterRows = ProjectRows(after, scopes, options, gaps);

        var endpointDiffs = scopes.Contains("endpoints") ? DiffRows("endpointDiffs", "endpoint", EndpointRuleId, beforeRows, afterRows, sourcePairs, gaps, options.MaxDiffRows) : [];
        var dtoTypeDiffs = scopes.Contains("dto-types") ? DiffRows("dtoTypeDiffs", "dto-type", DtoRuleId, beforeRows, afterRows, sourcePairs, gaps, options.MaxDiffRows) : [];
        var dtoPropertyDiffs = scopes.Contains("dto-properties") ? DiffRows("dtoPropertyDiffs", "dto-property", DtoRuleId, beforeRows, afterRows, sourcePairs, gaps, options.MaxDiffRows) : [];
        var methodDiffs = scopes.Contains("methods") ? DiffRows("methodDiffs", "method", DtoRuleId, beforeRows, afterRows, sourcePairs, gaps, options.MaxDiffRows) : [];
        var routeShapeDiffs = scopes.Contains("route-shapes") ? DiffRows("routeShapeDiffs", "route-shape", EndpointRuleId, beforeRows, afterRows, sourcePairs, gaps, options.MaxDiffRows) : [];
        var requestResponseDiffs = scopes.Contains("request-response")
            ? DiffRows("requestResponseDiffs", "request-response", AttachmentRuleId, beforeRows, afterRows, sourcePairs, gaps, options.MaxDiffRows)
            : [];

        if (scopes.Contains("request-response") && requestResponseDiffs.Count == 0)
        {
            gaps.Add(Gap("attachment", "AttachmentEvidenceUnavailable", "requestResponseDiffs", options.Source, AttachmentRuleId, ApiDtoContractDiffClassifications.UnknownAnalysisGap, "No credible endpoint-to-DTO request/response attachment facts were available for this comparison."));
        }

        if (HasAnySelector(options)
            && endpointDiffs.Count + dtoTypeDiffs.Count + dtoPropertyDiffs.Count + methodDiffs.Count + routeShapeDiffs.Count + requestResponseDiffs.Count == 0
            && !gaps.Any(gap => gap.GapKind == "SelectorNoMatch"))
        {
            gaps.Add(Gap("selector", "SelectorNoMatch", "query", options.Source, SelectorRuleId, ApiDtoContractDiffClassifications.SelectorNoMatch, "Selectors matched no comparable API/DTO contract evidence in either snapshot."));
        }

        var allDiffCount = endpointDiffs.Count + dtoTypeDiffs.Count + dtoPropertyDiffs.Count + methodDiffs.Count + requestResponseDiffs.Count + routeShapeDiffs.Count;
        if (allDiffCount == 0 && !gaps.Any(gap => gap.GapKind == "SelectorNoMatch"))
        {
            gaps.Add(Gap("no-diff", "NoDiffEvidence", "summary", options.Source, CoverageRuleId, ApiDtoContractDiffClassifications.NoDiffEvidence, "No API/DTO contract diff evidence matched the selected scope."));
        }

        var cappedGaps = CapGaps(gaps, options.MaxGaps);
        var reportCoverage = ReportCoverage(before.Snapshot.CoverageWarnings.Concat(after.Snapshot.CoverageWarnings).ToArray(), cappedGaps);
        var coverageWarnings = before.Snapshot.CoverageWarnings
            .Concat(after.Snapshot.CoverageWarnings)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var summary = new ApiDtoContractDiffSummary(
            sourcePairs.Count,
            endpointDiffs.Count,
            dtoTypeDiffs.Count,
            dtoPropertyDiffs.Count,
            methodDiffs.Count,
            requestResponseDiffs.Count,
            routeShapeDiffs.Count,
            cappedGaps.Count,
            cappedGaps.Count < gaps.Count
                || cappedGaps.Any(gap => gap.GapKind == "TruncatedByLimit")
                || endpointDiffs.Any(row => row.Classification == ApiDtoContractDiffClassifications.TruncatedByLimit)
                || dtoTypeDiffs.Any(row => row.Classification == ApiDtoContractDiffClassifications.TruncatedByLimit)
                || dtoPropertyDiffs.Any(row => row.Classification == ApiDtoContractDiffClassifications.TruncatedByLimit)
                || methodDiffs.Any(row => row.Classification == ApiDtoContractDiffClassifications.TruncatedByLimit)
                || requestResponseDiffs.Any(row => row.Classification == ApiDtoContractDiffClassifications.TruncatedByLimit)
                || routeShapeDiffs.Any(row => row.Classification == ApiDtoContractDiffClassifications.TruncatedByLimit),
            RollupClassification(endpointDiffs, dtoTypeDiffs, dtoPropertyDiffs, methodDiffs, requestResponseDiffs, routeShapeDiffs, cappedGaps),
            SummaryMessage(allDiffCount, cappedGaps));

        return new ApiDtoContractDiffReport(
            before.Kind == "combined" ? CombinedReportType : SingleReportType,
            Version,
            reportCoverage,
            coverageWarnings,
            new ApiDtoContractDiffQuery(scopes, options.Source, options.Endpoint, options.Type, options.Property, options.ChangeKind, options.MaxDiffRows, options.MaxEvidenceRows, options.MaxGaps, options.ExitCode, ignoredSelectors, Algorithm, AlgorithmVersion),
            before.Snapshot,
            after.Snapshot,
            summary,
            sourcePairs,
            endpointDiffs,
            dtoTypeDiffs,
            dtoPropertyDiffs,
            methodDiffs,
            requestResponseDiffs,
            routeShapeDiffs,
            cappedGaps,
            Limitations);
    }

    private static void ValidateOptions(ApiDtoContractDiffOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BeforePath))
        {
            throw new ArgumentException("contract-diff requires --before <index.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.AfterPath))
        {
            throw new ArgumentException("contract-diff requires --after <index.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("contract-diff requires --out <path>.");
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            _ = ParseEndpointSelector(options.Endpoint);
        }

        if (!string.IsNullOrWhiteSpace(options.ChangeKind) && !ValidChangeKinds.Contains(options.ChangeKind.Trim()))
        {
            throw new ArgumentException($"contract-diff --change-kind unsupported value `{CombinedReportHelpers.Cell(options.ChangeKind)}`.");
        }
    }

    private static IReadOnlyList<string> ParseScopes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultScopes;
        }

        var scopes = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(scope => scope.ToLowerInvariant())
            .ToArray();
        foreach (var scope in scopes)
        {
            if (!ValidScopes.Contains(scope))
            {
                throw new ArgumentException($"contract-diff --scope unsupported value `{CombinedReportHelpers.Cell(scope)}`.");
            }
        }

        return scopes.Contains("all", StringComparer.Ordinal) ? DefaultScopes : scopes.Distinct(StringComparer.Ordinal).OrderBy(ScopeOrder).ToArray();
    }

    private static int ScopeOrder(string scope) => scope switch
    {
        "endpoints" => 0,
        "dto-types" => 1,
        "dto-properties" => 2,
        "methods" => 3,
        "request-response" => 4,
        "route-shapes" => 5,
        _ => 99
    };

    private static IReadOnlyList<string> IgnoredSelectors(ApiDtoContractDiffOptions options, IReadOnlyList<string> scopes)
    {
        var ignored = new List<string>();
        if (!scopes.Contains("endpoints") && !scopes.Contains("route-shapes") && !scopes.Contains("request-response") && !string.IsNullOrWhiteSpace(options.Endpoint))
        {
            ignored.Add("--endpoint has no enabled endpoint, route-shape, or request-response scope.");
        }

        if (!scopes.Contains("dto-types") && !scopes.Contains("dto-properties") && !scopes.Contains("request-response") && !string.IsNullOrWhiteSpace(options.Type))
        {
            ignored.Add("--type has no enabled DTO or request-response scope.");
        }

        if (!scopes.Contains("dto-properties") && !string.IsNullOrWhiteSpace(options.Property))
        {
            ignored.Add("--property has no enabled dto-properties scope.");
        }

        return ignored.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static async Task<ApiDtoIndexRead> ReadIndexAsync(string path, string side, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ReadOnlyConnectionString(path));
        await connection.OpenAsync(cancellationToken);
        var hasScanManifest = await TableExistsAsync(connection, "scan_manifest", cancellationToken);
        var hasFacts = await TableExistsAsync(connection, "facts", cancellationToken);
        var hasSources = await TableExistsAsync(connection, "index_sources", cancellationToken);
        var hasCombinedFacts = await TableExistsAsync(connection, "combined_facts", cancellationToken);
        if (hasSources && hasCombinedFacts)
        {
            var snapshot = await ReadCombinedSnapshotAsync(connection, side, cancellationToken);
            var facts = await ReadCombinedFactsAsync(connection, cancellationToken);
            return new ApiDtoIndexRead("combined", snapshot, facts);
        }

        if (hasScanManifest && hasFacts)
        {
            var snapshot = await ReadSingleSnapshotAsync(connection, side, cancellationToken);
            var facts = await ReadSingleFactsAsync(connection, snapshot.Sources.Single(), cancellationToken);
            return new ApiDtoIndexRead("single", snapshot, facts);
        }

        var missing = !hasScanManifest && !hasSources ? "scan_manifest/index_sources" : !hasFacts && !hasCombinedFacts ? "facts/combined_facts" : "TraceMap index tables";
        throw new InvalidDataException($"{side} input is not a valid TraceMap index; missing {missing}.");
    }

    private static async Task<ApiDtoContractDiffSnapshot> ReadCombinedSnapshotAsync(SqliteConnection connection, string side, CancellationToken cancellationToken)
    {
        var sources = new List<ApiDtoContractSourceInfo>();
        var extractorVersions = new SortedDictionary<string, string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select label, language, scan_id, commit_sha, repo_name, remote_url, scan_root_path_hash, git_root_hash,
                   analysis_level, build_status, scanner_version, manifest_json
            from index_sources
            order by label, source_index_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var manifest = DeserializeManifest(StringOrNull(reader, 11));
            var label = StringOrDefault(reader, 0, "unknown");
            var scannerVersion = StringOrDefault(reader, 10, "unknown");
            extractorVersions[$"{label}:{scannerVersion}"] = scannerVersion;
            var gaps = manifest?.KnownGaps ?? [];
            var analysisLevel = StringOrDefault(reader, 8, "unknown");
            var buildStatus = StringOrDefault(reader, 9, "unknown");
            sources.Add(new ApiDtoContractSourceInfo(
                label,
                StringOrNull(reader, 1) ?? InferLanguage(scannerVersion),
                StringOrNull(reader, 2),
                NullIfUnknown(StringOrNull(reader, 3)),
                RepositoryIdentityHash(StringOrNull(reader, 5), StringOrNull(reader, 4)),
                StringOrNull(reader, 6),
                CoverageFrom(analysisLevel, buildStatus, gaps),
                buildStatus,
                analysisLevel,
                scannerVersion,
                gaps.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
        }

        var warnings = CoverageWarnings(sources);
        return new ApiDtoContractDiffSnapshot(side, "combined", sources, warnings.Count == 0 ? "FullEvidenceAvailable" : "ReducedCoverage", warnings, extractorVersions.ToArray());
    }

    private static async Task<ApiDtoContractDiffSnapshot> ReadSingleSnapshotAsync(SqliteConnection connection, string side, CancellationToken cancellationToken)
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
            throw new InvalidDataException($"{side} input is not a valid TraceMap index; missing scan_manifest row.");
        }

        var manifest = DeserializeManifest(StringOrNull(reader, 6));
        var gaps = manifest?.KnownGaps ?? [];
        var scannerVersion = StringOrDefault(reader, 3, "unknown");
        var analysisLevel = StringOrDefault(reader, 4, "unknown");
        var buildStatus = StringOrDefault(reader, 5, "unknown");
        var source = new ApiDtoContractSourceInfo(
            "self",
            InferLanguage(scannerVersion),
            StringOrNull(reader, 0),
            NullIfUnknown(StringOrNull(reader, 2)),
            RepositoryIdentityHash(manifest?.RemoteUrl, StringOrNull(reader, 1)),
            manifest?.ScanRootPathHash,
            CoverageFrom(analysisLevel, buildStatus, gaps),
            buildStatus,
            analysisLevel,
            scannerVersion,
            gaps.OrderBy(value => value, StringComparer.Ordinal).ToArray());
        var warnings = CoverageWarnings([source]);
        return new ApiDtoContractDiffSnapshot(side, "single", [source], warnings.Count == 0 ? "FullEvidenceAvailable" : "ReducedCoverage", warnings, [new KeyValuePair<string, string>($"self:{scannerVersion}", scannerVersion)]);
    }

    private static async Task<IReadOnlyList<ApiDtoFactRow>> ReadSingleFactsAsync(SqliteConnection connection, ApiDtoContractSourceInfo source, CancellationToken cancellationToken)
    {
        var rows = new List<ApiDtoFactRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select fact_id, scan_id, commit_sha, fact_type, rule_id, evidence_tier, source_symbol, target_symbol, contract_element,
                   file_path, start_line, end_line, properties_json
            from facts
            order by file_path, start_line, fact_type, fact_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ApiDtoFactRow(
                StringOrDefault(reader, 0, "unknown"),
                null,
                "self",
                source.Language,
                StringOrNull(reader, 1),
                NullIfUnknown(StringOrNull(reader, 2)),
                StringOrDefault(reader, 3, "unknown"),
                StringOrDefault(reader, 4, "unknown"),
                StringOrDefault(reader, 5, EvidenceTiers.Tier4Unknown),
                StringOrNull(reader, 6),
                StringOrNull(reader, 7),
                StringOrNull(reader, 8),
                StringOrDefault(reader, 9, "unknown"),
                IntOrZero(reader, 10),
                IntOrZero(reader, 11),
                ReadProperties(StringOrNull(reader, 12))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ApiDtoFactRow>> ReadCombinedFactsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ApiDtoFactRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select cf.combined_fact_id, cf.source_index_id, s.label, s.language, cf.scan_id, cf.commit_sha, cf.fact_type,
                   cf.rule_id, cf.evidence_tier, cf.source_symbol, cf.target_symbol, cf.contract_element,
                   cf.file_path, cf.start_line, cf.end_line, cf.properties_json
            from combined_facts cf
            join index_sources s on s.source_index_id = cf.source_index_id
            order by s.label, cf.file_path, cf.start_line, cf.fact_type, cf.combined_fact_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ApiDtoFactRow(
                StringOrDefault(reader, 0, "unknown"),
                StringOrNull(reader, 1),
                StringOrDefault(reader, 2, "unknown"),
                StringOrNull(reader, 3),
                StringOrNull(reader, 4),
                NullIfUnknown(StringOrNull(reader, 5)),
                StringOrDefault(reader, 6, "unknown"),
                StringOrDefault(reader, 7, "unknown"),
                StringOrDefault(reader, 8, EvidenceTiers.Tier4Unknown),
                StringOrNull(reader, 9),
                StringOrNull(reader, 10),
                StringOrNull(reader, 11),
                StringOrDefault(reader, 12, "unknown"),
                IntOrZero(reader, 13),
                IntOrZero(reader, 14),
                ReadProperties(StringOrNull(reader, 15))));
        }

        return rows;
    }

    private static IReadOnlyList<ApiDtoContractSourcePair> PairSources(ApiDtoContractDiffSnapshot before, ApiDtoContractDiffSnapshot after, string? sourceSelector, List<ApiDtoContractDiffGap> gaps)
    {
        var beforeSources = before.Sources.ToLookup(source => source.SourceLabel, StringComparer.Ordinal);
        var afterSources = after.Sources.ToLookup(source => source.SourceLabel, StringComparer.Ordinal);
        var pairs = beforeSources.Select(group => group.Key)
            .Concat(afterSources.Select(group => group.Key))
            .Distinct(StringComparer.Ordinal)
            .Where(label => string.IsNullOrWhiteSpace(sourceSelector) || label.Equals(sourceSelector, StringComparison.Ordinal))
            .OrderBy(label => label, StringComparer.Ordinal)
            .Select(label => BuildSourcePair(label, beforeSources[label].FirstOrDefault(), afterSources[label].FirstOrDefault()))
            .ToArray();
        if (!string.IsNullOrWhiteSpace(sourceSelector) && pairs.Length == 0)
        {
            gaps.Add(Gap("selector", "SelectorNoMatch", "sourcePairs", sourceSelector, SelectorRuleId, ApiDtoContractDiffClassifications.SelectorNoMatch, $"No source label matched `{sourceSelector}`."));
        }

        return pairs;
    }

    private static ApiDtoContractSourcePair BuildSourcePair(string label, ApiDtoContractSourceInfo? before, ApiDtoContractSourceInfo? after)
    {
        var caveats = new List<string>();
        var classification = "Comparable";
        if (before is null || after is null)
        {
            classification = ApiDtoContractDiffClassifications.NeedsReviewDiff;
            caveats.Add("Source exists on only one side.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(before.CommitSha) || string.IsNullOrWhiteSpace(after.CommitSha))
            {
                classification = ApiDtoContractDiffClassifications.UnknownAnalysisGap;
                caveats.Add("Unknown commit SHA on one or both sides.");
            }

            if (!string.Equals(before.RepositoryIdentityHash, after.RepositoryIdentityHash, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(before.Language)
                    && !string.IsNullOrWhiteSpace(after.Language)
                    && !string.Equals(before.Language, after.Language, StringComparison.OrdinalIgnoreCase)))
            {
                classification = ApiDtoContractDiffClassifications.UnknownAnalysisGap;
                caveats.Add("Source identity differs between snapshots.");
            }

            if (!string.Equals(before.Coverage, "Full", StringComparison.Ordinal)
                || !string.Equals(after.Coverage, "Full", StringComparison.Ordinal))
            {
                caveats.Add("Reduced coverage on one or both sides.");
            }
        }

        return new ApiDtoContractSourcePair(label, before, after, classification, caveats.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static void AddIdentityAndCoverageGaps(IReadOnlyList<ApiDtoContractSourcePair> pairs, List<ApiDtoContractDiffGap> gaps)
    {
        foreach (var pair in pairs)
        {
            if (pair.Before is null || pair.After is null)
            {
                gaps.Add(Gap("identity", "SourceOnlyOnOneSide", "sourcePairs", pair.SourceLabel, IdentityRuleId, ApiDtoContractDiffClassifications.NeedsReviewDiff, $"Source `{pair.SourceLabel}` exists on only one side of the comparison."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(pair.Before.CommitSha) || string.IsNullOrWhiteSpace(pair.After.CommitSha))
            {
                gaps.Add(Gap("identity", "UnknownCommitSha", "sourcePairs", pair.SourceLabel, IdentityRuleId, ApiDtoContractDiffClassifications.UnknownAnalysisGap, $"Source `{pair.SourceLabel}` has an unknown commit SHA on one or both sides."));
            }

            if (!string.Equals(pair.Before.RepositoryIdentityHash, pair.After.RepositoryIdentityHash, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(pair.Before.Language)
                    && !string.IsNullOrWhiteSpace(pair.After.Language)
                    && !string.Equals(pair.Before.Language, pair.After.Language, StringComparison.OrdinalIgnoreCase)))
            {
                gaps.Add(Gap("identity", "SourceIdentityConflict", "sourcePairs", pair.SourceLabel, IdentityRuleId, ApiDtoContractDiffClassifications.UnknownAnalysisGap, $"Source `{pair.SourceLabel}` identity differs between snapshots."));
            }

            if (!string.Equals(pair.Before.Coverage, "Full", StringComparison.Ordinal)
                || !string.Equals(pair.After.Coverage, "Full", StringComparison.Ordinal))
            {
                gaps.Add(Gap("coverage", "ReducedCoverage", "sourcePairs", pair.SourceLabel, CoverageRuleId, ApiDtoContractDiffClassifications.NeedsReviewDiff, $"Source `{pair.SourceLabel}` has reduced static analysis coverage."));
            }
        }
    }

    private static IReadOnlyList<ApiDtoComparableRow> ProjectRows(ApiDtoIndexRead index, IReadOnlyList<string> scopes, ApiDtoContractDiffOptions options, List<ApiDtoContractDiffGap> gaps)
    {
        var rows = new List<ApiDtoComparableRow>();
        foreach (var fact in index.Facts)
        {
            if (!string.IsNullOrWhiteSpace(options.Source) && !fact.SourceLabel.Equals(options.Source, StringComparison.Ordinal))
            {
                continue;
            }

            if (scopes.Contains("endpoints") && TryProjectEndpoint(fact, options, out var endpoint))
            {
                rows.Add(endpoint);
            }

            if (scopes.Contains("route-shapes") && TryProjectRouteShape(fact, options, out var routeShape))
            {
                rows.Add(routeShape);
            }

            if (scopes.Contains("dto-types") && TryProjectDtoType(fact, options, out var dtoType))
            {
                rows.Add(dtoType);
            }

            if (scopes.Contains("dto-properties") && TryProjectDtoProperty(fact, options, out var dtoProperty))
            {
                rows.Add(dtoProperty);
            }

            if (scopes.Contains("methods") && TryProjectMethod(fact, options, out var method))
            {
                rows.Add(method);
            }

            if (scopes.Contains("request-response") && TryProjectAttachment(fact, options, out var attachment))
            {
                rows.Add(attachment);
            }
        }

        AddDuplicateIdentityGaps(rows, gaps);
        return rows
            .OrderBy(row => row.RowKind, StringComparer.Ordinal)
            .ThenBy(row => row.StableKey, StringComparer.Ordinal)
            .ThenBy(row => row.Evidence.SupportingFactIds.FirstOrDefault(), StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryProjectEndpoint(ApiDtoFactRow fact, ApiDtoContractDiffOptions options, out ApiDtoComparableRow row)
    {
        row = default!;
        if (fact.FactType is not FactTypes.HttpRouteBinding)
        {
            return false;
        }

        var method = FirstValue(fact.Properties, "httpMethod", "httpMethods", "method") ?? HttpMethodFromEndpointKey(FirstValue(fact.Properties, "normalizedPathKey", "pathKey", "routeTemplate"));
        var pathKey = NormalizeEndpointPath(FirstValue(fact.Properties, "normalizedPathKey", "pathKey", "path", "routeTemplate", "normalizedPathTemplate"));
        if (!EndpointSelectorMatches(options.Endpoint, method, pathKey))
        {
            return false;
        }

        if (!KindMatches(options, "endpoint"))
        {
            return false;
        }

        var strongIdentity = !string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(pathKey);
        var handler = FirstValue(fact.Properties, "handlerSymbol", "actionSymbol", "methodSymbol") ?? fact.TargetSymbol ?? fact.SourceSymbol;
        var display = $"{method ?? "ANY"} {pathKey ?? SafeHashDisplay(FirstValue(fact.Properties, "routeTemplate", "path"))}";
        var metadata = CombinedReportHelpers.SortedMetadata([
            Pair("endpointKind", FirstValue(fact.Properties, "endpointKind", "surfaceKind") ?? "http-route"),
            Pair("httpMethod", method),
            Pair("normalizedPathKey", pathKey),
            Pair("routeTemplateHash", SafeMaybeHash(FirstValue(fact.Properties, "routeTemplate", "path"))),
            Pair("routeParameters", RouteParameters(fact)),
            Pair("handlerSymbol", SafeSymbol(handler)),
            Pair("containingType", SafeSymbol(FirstValue(fact.Properties, "containingType", "controllerName"))),
            Pair("framework", FirstValue(fact.Properties, "framework", "serverFramework"))
        ]);
        var stable = strongIdentity
            ? $"endpoint:{fact.SourceLabel}:{method!.ToUpperInvariant()}:{pathKey}"
            : $"endpoint-review:{fact.SourceLabel}:{MetadataHash(metadata)}";
        row = Row("endpoint", stable, metadata, display, fact, !strongIdentity || IsReviewTier(fact), strongIdentity ? [] : ["Endpoint identity lacks method/path key and is review-tier."]);
        return true;
    }

    private static bool TryProjectRouteShape(ApiDtoFactRow fact, ApiDtoContractDiffOptions options, out ApiDtoComparableRow row)
    {
        row = default!;
        if (fact.FactType is not FactTypes.HttpRouteBinding)
        {
            return false;
        }

        var method = FirstValue(fact.Properties, "httpMethod", "httpMethods", "method") ?? HttpMethodFromEndpointKey(FirstValue(fact.Properties, "normalizedPathKey", "pathKey", "routeTemplate"));
        var pathKey = NormalizeEndpointPath(FirstValue(fact.Properties, "normalizedPathKey", "pathKey", "path", "routeTemplate", "normalizedPathTemplate"));
        if (!EndpointSelectorMatches(options.Endpoint, method, pathKey) || !KindMatches(options, "route-shape"))
        {
            return false;
        }

        var parameters = RouteParameters(fact);
        var strongIdentity = !string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(pathKey);
        var metadata = CombinedReportHelpers.SortedMetadata([
            Pair("httpMethod", method),
            Pair("normalizedPathKey", pathKey),
            Pair("routeParameters", parameters),
            Pair("routeParameterCount", string.IsNullOrWhiteSpace(parameters) ? "0" : parameters.Split(',', StringSplitOptions.RemoveEmptyEntries).Length.ToString())
        ]);
        var stable = strongIdentity
            ? $"route-shape:{fact.SourceLabel}:{method!.ToUpperInvariant()}:{pathKey}"
            : $"route-shape-review:{fact.SourceLabel}:{MetadataHash(metadata)}";
        row = Row("route-shape", stable, metadata, $"{method ?? "ANY"} {pathKey ?? "unknown"}", fact, !strongIdentity || IsReviewTier(fact), strongIdentity ? [] : ["Route shape identity lacks method/path key and is review-tier."]);
        return true;
    }

    private static bool TryProjectDtoType(ApiDtoFactRow fact, ApiDtoContractDiffOptions options, out ApiDtoComparableRow row)
    {
        row = default!;
        if (fact.FactType is not (FactTypes.TypeDeclared or FactTypes.EnumDeclared or FactTypes.ObjectShapeInferred or FactTypes.DeserializedObject))
        {
            return false;
        }

        if (!KindMatches(options, "dto-type"))
        {
            return false;
        }

        var typeName = FirstValue(fact.Properties, "typeName", "fullyQualifiedTypeName", "displayName", "schemaName", "shapeName")
            ?? fact.TargetSymbol
            ?? fact.SourceSymbol
            ?? fact.ContractElement;
        if (!TextSelectorMatches(options.Type, typeName, fact.ContractElement))
        {
            return false;
        }

        var module = FirstValue(fact.Properties, "assemblyName", "packageName", "moduleName", "namespace");
        var strongIdentity = !string.IsNullOrWhiteSpace(typeName) && fact.FactType != FactTypes.ObjectShapeInferred;
        var metadata = CombinedReportHelpers.SortedMetadata([
            Pair("language", fact.Language),
            Pair("typeName", SafeSymbol(typeName)),
            Pair("namespace", SafeSymbol(FirstValue(fact.Properties, "namespace", "package", "module"))),
            Pair("assemblyOrModule", SafeSymbol(module)),
            Pair("typeKind", FirstValue(fact.Properties, "typeKind", "symbolKind") ?? fact.FactType),
            Pair("serializerSourceKind", FirstValue(fact.Properties, "serializerSourceKind", "schemaSourceKind"))
        ]);
        var stable = strongIdentity
            ? $"dto-type:{fact.SourceLabel}:{fact.Language ?? "unknown"}:{module ?? "no-module"}:{typeName}"
            : $"dto-type-review:{fact.SourceLabel}:{MetadataHash(metadata)}";
        row = Row("dto-type", stable, metadata, typeName ?? "unknown DTO type", fact, !strongIdentity || IsReviewTier(fact), strongIdentity ? [] : ["DTO type identity is syntax/object-shape or missing semantic type identity."]);
        return true;
    }

    private static bool TryProjectDtoProperty(ApiDtoFactRow fact, ApiDtoContractDiffOptions options, out ApiDtoComparableRow row)
    {
        row = default!;
        if (fact.FactType is not (FactTypes.PropertyDeclared or FactTypes.FieldDeclared or FactTypes.SerializerContractMember or FactTypes.DatabaseColumnMapping))
        {
            return false;
        }

        if (!KindMatches(options, "dto-property"))
        {
            return false;
        }

        var propertyName = FirstValue(fact.Properties, "propertyName", "memberName", "fieldName", "name", "columnName") ?? fact.ContractElement ?? MemberName(fact.TargetSymbol);
        if (!TextSelectorMatches(options.Property, propertyName))
        {
            return false;
        }

        var containingType = FirstValue(fact.Properties, "containingType", "declaringType", "typeName", "entityName", "tableName") ?? ContainingType(fact.TargetSymbol) ?? fact.SourceSymbol;
        if (!TextSelectorMatches(options.Type, containingType))
        {
            return false;
        }

        var alias = FirstValue(fact.Properties, "jsonName", "jsonPropertyName", "schemaAlias", "alias");
        var declaredType = FirstValue(fact.Properties, "declaredType", "propertyType", "fieldType", "type");
        var strongIdentity = !string.IsNullOrWhiteSpace(containingType) && !string.IsNullOrWhiteSpace(propertyName);
        var genericOnly = string.IsNullOrWhiteSpace(containingType) && !string.IsNullOrWhiteSpace(propertyName) && GenericPropertyNames.Contains(propertyName);
        var metadata = CombinedReportHelpers.SortedMetadata([
            Pair("containingType", SafeSymbol(containingType)),
            Pair("propertyName", propertyName),
            Pair("declaredType", SafeSymbol(declaredType)),
            Pair("nullability", FirstValue(fact.Properties, "nullability", "nullable")),
            Pair("requiredness", FirstValue(fact.Properties, "required", "requiredness", "isRequired")),
            Pair("jsonOrSchemaAlias", alias),
            Pair("memberKind", fact.FactType)
        ]);
        var stable = strongIdentity
            ? $"dto-property:{fact.SourceLabel}:{containingType}:{propertyName}"
            : $"dto-property-review:{fact.SourceLabel}:{propertyName ?? MetadataHash(metadata)}";
        row = Row("dto-property", stable, metadata, $"{containingType ?? "unknown"}.{propertyName ?? "unknown"}", fact, !strongIdentity || genericOnly || IsReviewTier(fact), strongIdentity && !genericOnly ? [] : ["DTO property lacks containing type identity or uses generic property-only identity."]);
        return true;
    }

    private static bool TryProjectMethod(ApiDtoFactRow fact, ApiDtoContractDiffOptions options, out ApiDtoComparableRow row)
    {
        row = default!;
        if (fact.FactType != FactTypes.MethodDeclared || !KindMatches(options, "method"))
        {
            return false;
        }

        var methodName = FirstValue(fact.Properties, "methodName", "name") ?? MemberName(fact.TargetSymbol) ?? fact.ContractElement;
        var containingType = FirstValue(fact.Properties, "containingType", "declaringType") ?? ContainingType(fact.TargetSymbol) ?? fact.SourceSymbol;
        if (!TextSelectorMatches(options.Type, containingType) || !TextSelectorMatches(options.Property, methodName))
        {
            return false;
        }

        var parameters = FirstValue(fact.Properties, "parameterTypes", "parameters", "signatureParameters");
        var returnType = FirstValue(fact.Properties, "returnType", "declaredReturnType");
        var strongIdentity = !string.IsNullOrWhiteSpace(containingType) && !string.IsNullOrWhiteSpace(methodName);
        var metadata = CombinedReportHelpers.SortedMetadata([
            Pair("containingType", SafeSymbol(containingType)),
            Pair("methodName", methodName),
            Pair("arity", FirstValue(fact.Properties, "arity", "parameterCount")),
            Pair("parameterTypesHash", SafeMaybeHash(parameters)),
            Pair("returnType", SafeSymbol(returnType)),
            Pair("methodSymbol", SafeSymbol(fact.TargetSymbol))
        ]);
        var stable = strongIdentity
            ? $"method:{fact.SourceLabel}:{containingType}:{methodName}:{FirstValue(fact.Properties, "arity", "parameterCount") ?? "unknown"}:{CombinedReportHelpers.Hash(parameters ?? "", 16)}:{returnType ?? "unknown"}"
            : $"method-review:{fact.SourceLabel}:{MetadataHash(metadata)}";
        row = Row("method", stable, metadata, $"{containingType ?? "unknown"}.{methodName ?? "unknown"}", fact, !strongIdentity || IsReviewTier(fact), strongIdentity ? [] : ["Method identity lacks containing type or method name."]);
        return true;
    }

    private static bool TryProjectAttachment(ApiDtoFactRow fact, ApiDtoContractDiffOptions options, out ApiDtoComparableRow row)
    {
        row = default!;
        var attachmentKind = FirstValue(fact.Properties, "attachmentKind", "requestResponseKind", "bodyKind");
        if (string.IsNullOrWhiteSpace(attachmentKind)
            || !attachmentKind.Contains("request", StringComparison.OrdinalIgnoreCase)
               && !attachmentKind.Contains("response", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!KindMatches(options, "request-response"))
        {
            return false;
        }

        var method = FirstValue(fact.Properties, "httpMethod", "method");
        var pathKey = NormalizeEndpointPath(FirstValue(fact.Properties, "normalizedPathKey", "pathKey", "routeTemplate", "path"));
        if (!EndpointSelectorMatches(options.Endpoint, method, pathKey))
        {
            return false;
        }

        var dtoType = FirstValue(fact.Properties, "dtoType", "requestType", "responseType", "typeName") ?? fact.TargetSymbol;
        if (!TextSelectorMatches(options.Type, dtoType))
        {
            return false;
        }

        var metadata = CombinedReportHelpers.SortedMetadata([
            Pair("httpMethod", method),
            Pair("normalizedPathKey", pathKey),
            Pair("attachmentKind", attachmentKind),
            Pair("statusCode", FirstValue(fact.Properties, "statusCode", "responseStatus")),
            Pair("responseKind", FirstValue(fact.Properties, "responseKind")),
            Pair("dtoType", SafeSymbol(dtoType))
        ]);
        var strongIdentity = !string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(pathKey) && !string.IsNullOrWhiteSpace(dtoType);
        var stable = strongIdentity
            ? $"request-response:{fact.SourceLabel}:{method!.ToUpperInvariant()}:{pathKey}:{attachmentKind}:{FirstValue(fact.Properties, "statusCode", "responseStatus") ?? "default"}:{dtoType}"
            : $"request-response-review:{fact.SourceLabel}:{MetadataHash(metadata)}";
        row = Row("request-response", stable, metadata, $"{method ?? "ANY"} {pathKey ?? "unknown"} {attachmentKind}", fact, !strongIdentity || IsReviewTier(fact), strongIdentity ? [] : ["Request/response attachment lacks stable endpoint or DTO identity."]);
        return true;
    }

    private static ApiDtoComparableRow Row(string kind, string stable, IReadOnlyList<KeyValuePair<string, string>> metadata, string display, ApiDtoFactRow fact, bool needsReview, IReadOnlyList<string> notes)
    {
        var evidence = new ApiDtoContractEvidence(
            fact.SourceLabel,
            fact.CommitSha,
            fact.ScanId,
            fact.Language,
            SafeDisplay(display),
            fact.EvidenceTier,
            fact.RuleId,
            [new ApiDtoContractFileSpan(CombinedReportHelpers.SafePath(fact.FilePath), fact.StartLine, fact.EndLine, fact.SourceLabel)],
            [fact.FactId],
            metadata);
        return new ApiDtoComparableRow(kind, stable, MetadataHash(metadata), evidence, needsReview || IsReviewTier(fact), notes);
    }

    private static IReadOnlyList<ApiDtoContractDiffRow> DiffRows(
        string section,
        string rowKind,
        string ruleId,
        IReadOnlyList<ApiDtoComparableRow> beforeRows,
        IReadOnlyList<ApiDtoComparableRow> afterRows,
        IReadOnlyList<ApiDtoContractSourcePair> sourcePairs,
        List<ApiDtoContractDiffGap> gaps,
        int maxRows)
    {
        var before = beforeRows.Where(row => row.RowKind == rowKind).GroupBy(row => row.StableKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var after = afterRows.Where(row => row.RowKind == rowKind).GroupBy(row => row.StableKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var rows = new List<ApiDtoContractDiffRow>();
        foreach (var key in before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            before.TryGetValue(key, out var beforeGroup);
            after.TryGetValue(key, out var afterGroup);
            var beforeRecord = beforeGroup?.OrderBy(row => row.Evidence.SupportingFactIds.FirstOrDefault(), StringComparer.Ordinal).FirstOrDefault();
            var afterRecord = afterGroup?.OrderBy(row => row.Evidence.SupportingFactIds.FirstOrDefault(), StringComparer.Ordinal).FirstOrDefault();
            if (beforeGroup is { Length: > 1 } || afterGroup is { Length: > 1 })
            {
                rows.Add(ToDiffRow(rowKind, key, "duplicate", ApiDtoContractDiffClassifications.NeedsReviewDiff, ruleId, beforeRecord, afterRecord, ["Duplicate stable identity; review provenance before treating as a single contract."]));
                continue;
            }

            if (beforeRecord is not null && afterRecord is not null)
            {
                if (beforeRecord.MetadataHash == afterRecord.MetadataHash)
                {
                    continue;
                }

                var classification = beforeRecord.NeedsReview || afterRecord.NeedsReview || HasBlockingGap(beforeRecord.Evidence.SourceLabel, sourcePairs)
                    ? ApiDtoContractDiffClassifications.NeedsReviewDiff
                    : ApiDtoContractDiffClassifications.ChangedEvidence;
                rows.Add(ToDiffRow(rowKind, key, "changed", classification, ruleId, beforeRecord, afterRecord, beforeRecord.Notes.Concat(afterRecord.Notes).Distinct(StringComparer.Ordinal).ToArray()));
                continue;
            }

            if (beforeRecord is null && afterRecord is not null)
            {
                var classification = ClassifyOneSided(afterRecord.Evidence.SourceLabel, sourcePairs, added: true, afterRecord.NeedsReview);
                rows.Add(ToDiffRow(rowKind, key, "added", classification, ruleId, null, afterRecord, afterRecord.Notes));
            }
            else if (beforeRecord is not null)
            {
                var classification = ClassifyOneSided(beforeRecord.Evidence.SourceLabel, sourcePairs, added: false, beforeRecord.NeedsReview);
                rows.Add(ToDiffRow(rowKind, key, "removed", classification, ruleId, beforeRecord, null, beforeRecord.Notes));
            }
        }

        var ordered = rows
            .OrderBy(row => ClassificationOrder(row.Classification))
            .ThenBy(row => row.RowKind, StringComparer.Ordinal)
            .ThenBy(row => row.StableKey, StringComparer.Ordinal)
            .ToArray();
        return Truncate(section, ordered, maxRows, gaps);
    }

    private static ApiDtoContractDiffRow ToDiffRow(string rowKind, string stableKey, string changeType, string classification, string ruleId, ApiDtoComparableRow? before, ApiDtoComparableRow? after, IReadOnlyList<string> notes)
    {
        var tiers = new[] { before?.Evidence.EvidenceTier, after?.Evidence.EvidenceTier }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var factRuleIds = new[] { before?.Evidence.FactRuleId, after?.Evidence.FactRuleId }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var spans = new[] { before?.Evidence, after?.Evidence }
            .OfType<ApiDtoContractEvidence>()
            .SelectMany(evidence => evidence.FileSpans)
            .OrderBy(span => span.SourceLabel, StringComparer.Ordinal)
            .ThenBy(span => span.FilePath, StringComparer.Ordinal)
            .ThenBy(span => span.StartLine)
            .ToArray();
        var facts = new[] { before?.Evidence, after?.Evidence }
            .OfType<ApiDtoContractEvidence>()
            .SelectMany(evidence => evidence.SupportingFactIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var sourceLabel = before?.Evidence.SourceLabel ?? after?.Evidence.SourceLabel;
        return new ApiDtoContractDiffRow(
            $"api-dto-diff:{CombinedReportHelpers.Hash($"{rowKind}\n{stableKey}\n{changeType}\n{classification}", 32)}",
            rowKind,
            changeType,
            classification,
            Confidence(classification),
            stableKey,
            ruleId,
            sourceLabel,
            before?.Evidence,
            after?.Evidence,
            tiers,
            factRuleIds,
            spans,
            facts,
            [],
            CoverageCaveats(classification),
            notes.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<ApiDtoContractDiffRow> Truncate(string section, IReadOnlyList<ApiDtoContractDiffRow> rows, int maxRows, List<ApiDtoContractDiffGap> gaps)
    {
        if (rows.Count <= maxRows)
        {
            return rows;
        }

        gaps.Add(Gap("truncation", "TruncatedByLimit", section, null, TruncationRuleId, ApiDtoContractDiffClassifications.TruncatedByLimit, $"{section} was truncated at --max-diff-rows {maxRows}; {rows.Count - maxRows} rows were omitted."));
        return rows.Take(maxRows).ToArray();
    }

    private static void AddDuplicateIdentityGaps(IReadOnlyList<ApiDtoComparableRow> rows, List<ApiDtoContractDiffGap> gaps)
    {
        foreach (var duplicate in rows.GroupBy(row => $"{row.RowKind}:{row.StableKey}", StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            var factIds = duplicate.SelectMany(row => row.Evidence.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            gaps.Add(Gap("identity", "DuplicateContractIdentity", duplicate.First().RowKind, duplicate.First().Evidence.SourceLabel, IdentityRuleId, ApiDtoContractDiffClassifications.NeedsReviewDiff, $"Duplicate stable API/DTO contract identity `{duplicate.Key}` has {duplicate.Count()} instances.", factIds));
        }
    }

    private static string ClassifyOneSided(string? sourceLabel, IReadOnlyList<ApiDtoContractSourcePair> pairs, bool added, bool needsReview)
    {
        if (needsReview || HasBlockingGap(sourceLabel, pairs))
        {
            return ApiDtoContractDiffClassifications.NeedsReviewDiff;
        }

        var pair = pairs.FirstOrDefault(pair => string.Equals(pair.SourceLabel, sourceLabel, StringComparison.Ordinal));
        if (pair?.Before is null || pair.After is null)
        {
            return ApiDtoContractDiffClassifications.NeedsReviewDiff;
        }

        if (added && !string.Equals(pair.Before.Coverage, "Full", StringComparison.Ordinal))
        {
            return ApiDtoContractDiffClassifications.AddedWithBeforeGap;
        }

        if (!added && !string.Equals(pair.After.Coverage, "Full", StringComparison.Ordinal))
        {
            return ApiDtoContractDiffClassifications.RemovedWithAfterGap;
        }

        return added ? ApiDtoContractDiffClassifications.Added : ApiDtoContractDiffClassifications.Removed;
    }

    private static bool HasBlockingGap(string? sourceLabel, IReadOnlyList<ApiDtoContractSourcePair> pairs)
    {
        var pair = pairs.FirstOrDefault(pair => string.Equals(pair.SourceLabel, sourceLabel, StringComparison.Ordinal));
        return pair is null
            || pair.Before is null
            || pair.After is null
            || pair.Classification == ApiDtoContractDiffClassifications.UnknownAnalysisGap;
    }

    private static IReadOnlyList<string> CoverageCaveats(string classification)
    {
        return classification switch
        {
            ApiDtoContractDiffClassifications.AddedWithBeforeGap => ["Before snapshot had reduced coverage; addition is coverage-relative."],
            ApiDtoContractDiffClassifications.RemovedWithAfterGap => ["After snapshot had reduced coverage; removal is coverage-relative."],
            ApiDtoContractDiffClassifications.NeedsReviewDiff => ["Identity, syntax-only evidence, duplicate evidence, or source gaps limit this row to review-tier."],
            _ => []
        };
    }

    private static string RollupClassification(params object[] groups)
    {
        var rows = groups.OfType<IReadOnlyList<ApiDtoContractDiffRow>>().SelectMany(group => group).ToArray();
        var gaps = groups.OfType<IReadOnlyList<ApiDtoContractDiffGap>>().SelectMany(group => group).ToArray();
        if (rows.Any(row => row.Classification is ApiDtoContractDiffClassifications.Added or ApiDtoContractDiffClassifications.Removed or ApiDtoContractDiffClassifications.ChangedEvidence))
        {
            return ApiDtoContractDiffClassifications.ChangedEvidence;
        }

        if (rows.Any())
        {
            return ApiDtoContractDiffClassifications.NeedsReviewDiff;
        }

        if (gaps.Any(gap => gap.Classification == ApiDtoContractDiffClassifications.UnknownAnalysisGap))
        {
            return ApiDtoContractDiffClassifications.UnknownAnalysisGap;
        }

        return ApiDtoContractDiffClassifications.NoDiffEvidence;
    }

    private static bool HasActionableDiffs(ApiDtoContractDiffReport report)
    {
        return report.EndpointDiffs
            .Concat(report.DtoTypeDiffs)
            .Concat(report.DtoPropertyDiffs)
            .Concat(report.MethodDiffs)
            .Concat(report.RequestResponseDiffs)
            .Concat(report.RouteShapeDiffs)
            .Any(row => row.Classification is ApiDtoContractDiffClassifications.Added or ApiDtoContractDiffClassifications.Removed or ApiDtoContractDiffClassifications.ChangedEvidence);
    }

    private static string ReportCoverage(IReadOnlyList<string> warnings, IReadOnlyList<ApiDtoContractDiffGap> gaps)
    {
        if (gaps.Any(gap => gap.Classification == ApiDtoContractDiffClassifications.UnknownAnalysisGap))
        {
            return "UnknownAnalysisGap";
        }

        return warnings.Count == 0 ? "FullEvidenceAvailable" : "ReducedCoverage";
    }

    private static string SummaryMessage(int rowCount, IReadOnlyList<ApiDtoContractDiffGap> gaps)
    {
        if (rowCount > 0)
        {
            return $"API/DTO contract diff emitted {rowCount} static evidence row(s).";
        }

        return gaps.Any(gap => gap.GapKind == "SelectorNoMatch")
            ? "Selectors matched no comparable API/DTO contract evidence."
            : "No API/DTO contract diff evidence matched the selected scope.";
    }

    private static IReadOnlyList<ApiDtoContractDiffGap> CapGaps(IReadOnlyList<ApiDtoContractDiffGap> gaps, int maxGaps)
    {
        var ordered = gaps
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(gap => gap.Message, StringComparer.Ordinal).First())
            .OrderBy(gap => gap.Section, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length <= maxGaps)
        {
            return ordered;
        }

        return ordered.Take(maxGaps).ToArray();
    }

    private static ApiDtoContractDiffGap Gap(string idSeed, string kind, string section, string? sourceLabel, string ruleId, string classification, string message, IReadOnlyList<string>? supportingFactIds = null)
    {
        return new ApiDtoContractDiffGap(
            $"gap:api-dto:{CombinedReportHelpers.Hash($"{idSeed}\n{kind}\n{section}\n{sourceLabel}\n{message}", 24)}",
            kind,
            section,
            sourceLabel,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            classification,
            message,
            supportingFactIds?.OrderBy(value => value, StringComparer.Ordinal).ToArray() ?? [],
            [],
            CombinedReportHelpers.SortedMetadata([Pair("limitation", message)]));
    }

    private static string RenderMarkdown(ApiDtoContractDiffReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap API/DTO Contract Diff Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Report type: `{report.ReportType}`");
        builder.AppendLine($"- Report coverage: `{report.ReportCoverage}`");
        builder.AppendLine($"- Rollup: `{report.Summary.RollupClassification}`");
        builder.AppendLine($"- Endpoint diffs: `{report.Summary.EndpointDiffCount}`");
        builder.AppendLine($"- DTO type diffs: `{report.Summary.DtoTypeDiffCount}`");
        builder.AppendLine($"- DTO property diffs: `{report.Summary.DtoPropertyDiffCount}`");
        builder.AppendLine($"- Method signature diffs: `{report.Summary.MethodDiffCount}`");
        builder.AppendLine($"- Request/response attachment diffs: `{report.Summary.RequestResponseDiffCount}`");
        builder.AppendLine($"- Route shape diffs: `{report.Summary.RouteShapeDiffCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        builder.AppendLine($"- Message: {Cell(report.Summary.Message)}");
        builder.AppendLine();
        builder.AppendLine("## Compared Snapshots");
        builder.AppendLine();
        AppendSnapshot(builder, report.BeforeSnapshot);
        AppendSnapshot(builder, report.AfterSnapshot);
        builder.AppendLine("## Sources and Coverage");
        builder.AppendLine();
        AppendRows(builder, report.SourcePairs, "| Source | Classification | Before commit | After commit | Caveats |", "| --- | --- | --- | --- | --- |",
            pair => $"| {Cell(pair.SourceLabel)} | {Cell(pair.Classification)} | {Cell(pair.Before?.CommitSha ?? "n/a")} | {Cell(pair.After?.CommitSha ?? "n/a")} | {Cell(string.Join("; ", pair.Caveats))} |");
        builder.AppendLine();
        AppendDiffSection(builder, "Endpoint Contract Diffs", report.EndpointDiffs);
        AppendDiffSection(builder, "DTO Type Diffs", report.DtoTypeDiffs);
        AppendDiffSection(builder, "DTO Property Diffs", report.DtoPropertyDiffs);
        AppendDiffSection(builder, "Method Signature Diffs", report.MethodDiffs);
        AppendDiffSection(builder, "Request/Response Attachment Diffs", report.RequestResponseDiffs);
        AppendDiffSection(builder, "Route Shape Diffs", report.RouteShapeDiffs);
        builder.AppendLine("## Gaps");
        builder.AppendLine();
        AppendRows(builder, report.Gaps, "| Gap | Section | Source | Classification | Rule | Message |", "| --- | --- | --- | --- | --- | --- |",
            gap => $"| {Cell(gap.GapKind)} | {Cell(gap.Section)} | {Cell(gap.SourceLabel ?? "n/a")} | {Cell(gap.Classification)} | {Cell(gap.RuleId)} | {Cell(gap.Message)} |");
        builder.AppendLine();
        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations)
        {
            builder.AppendLine($"- {Cell(limitation)}");
        }

        return builder.ToString();
    }

    private static void AppendSnapshot(StringBuilder builder, ApiDtoContractDiffSnapshot snapshot)
    {
        builder.AppendLine($"### {Cell(snapshot.Side)}");
        builder.AppendLine();
        builder.AppendLine($"- Index kind: `{Cell(snapshot.IndexKind)}`");
        builder.AppendLine($"- Coverage: `{Cell(snapshot.ReportCoverage)}`");
        builder.AppendLine($"- Sources: `{snapshot.Sources.Count}`");
        builder.AppendLine();
    }

    private static void AppendDiffSection(StringBuilder builder, string title, IReadOnlyList<ApiDtoContractDiffRow> rows)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        AppendRows(builder, rows, "| Classification | Change | Kind | Source | Stable key | Before | After | Rule |", "| --- | --- | --- | --- | --- | --- | --- | --- |",
            row => $"| {Cell(row.Classification)} | {Cell(row.ChangeType)} | {Cell(row.RowKind)} | {Cell(row.SourceLabel ?? "n/a")} | {Cell(row.StableKey)} | {Cell(EvidenceLabel(row.Before))} | {Cell(EvidenceLabel(row.After))} | {Cell(row.RuleId)} |");
        builder.AppendLine();
    }

    private static void AppendRows<T>(StringBuilder builder, IReadOnlyList<T> rows, string header, string separator, Func<T, string> render)
    {
        if (rows.Count == 0)
        {
            builder.AppendLine("_None._");
            return;
        }

        builder.AppendLine(header);
        builder.AppendLine(separator);
        foreach (var row in rows)
        {
            builder.AppendLine(render(row));
        }
    }

    private static string EvidenceLabel(ApiDtoContractEvidence? evidence)
    {
        if (evidence is null)
        {
            return "n/a";
        }

        var span = evidence.FileSpans.FirstOrDefault();
        return $"{evidence.DisplayName ?? "evidence"} ({span?.FilePath ?? "n/a"}:{span?.StartLine?.ToString() ?? "?"})";
    }

    private static string Confidence(string classification) => classification switch
    {
        ApiDtoContractDiffClassifications.Added or ApiDtoContractDiffClassifications.Removed or ApiDtoContractDiffClassifications.ChangedEvidence => "high",
        ApiDtoContractDiffClassifications.AddedWithBeforeGap or ApiDtoContractDiffClassifications.RemovedWithAfterGap => "medium",
        ApiDtoContractDiffClassifications.NeedsReviewDiff => "review",
        ApiDtoContractDiffClassifications.NoDiffEvidence or ApiDtoContractDiffClassifications.SelectorNoMatch => "none",
        _ => "unknown"
    };

    private static int ClassificationOrder(string classification) => classification switch
    {
        ApiDtoContractDiffClassifications.ChangedEvidence => 0,
        ApiDtoContractDiffClassifications.Added => 1,
        ApiDtoContractDiffClassifications.Removed => 2,
        ApiDtoContractDiffClassifications.AddedWithBeforeGap => 3,
        ApiDtoContractDiffClassifications.RemovedWithAfterGap => 4,
        ApiDtoContractDiffClassifications.NeedsReviewDiff => 5,
        _ => 99
    };

    private static bool IsReviewTier(ApiDtoFactRow fact)
    {
        return fact.EvidenceTier is EvidenceTiers.Tier3SyntaxOrTextual or EvidenceTiers.Tier4Unknown
            || fact.FactType == FactTypes.ObjectShapeInferred;
    }

    private static bool KindMatches(ApiDtoContractDiffOptions options, string kind)
    {
        return string.IsNullOrWhiteSpace(options.ChangeKind) || options.ChangeKind.Trim().Equals(kind, StringComparison.Ordinal);
    }

    private static bool EndpointSelectorMatches(string? selectorText, string? method, string? pathKey)
    {
        if (string.IsNullOrWhiteSpace(selectorText))
        {
            return true;
        }

        var selector = ParseEndpointSelector(selectorText);
        return !string.IsNullOrWhiteSpace(method)
            && !string.IsNullOrWhiteSpace(pathKey)
            && selector.Method.Equals(method, StringComparison.OrdinalIgnoreCase)
            && selector.PathKey.Equals(pathKey, StringComparison.Ordinal);
    }

    private static (string Method, string PathKey) ParseEndpointSelector(string selector)
    {
        var parts = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]) || parts[1].Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("contract-diff --endpoint must be formatted as '<METHOD> <PATH_KEY>'.");
        }

        return (parts[0].ToUpperInvariant(), NormalizeEndpointPath(parts[1]) ?? parts[1]);
    }

    private static bool TextSelectorMatches(string? selector, params string?[] values)
    {
        return string.IsNullOrWhiteSpace(selector)
            || values.Any(value => !string.IsNullOrWhiteSpace(value) && value.Equals(selector, StringComparison.Ordinal));
    }

    private static bool HasAnySelector(ApiDtoContractDiffOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.Endpoint)
            || !string.IsNullOrWhiteSpace(options.Type)
            || !string.IsNullOrWhiteSpace(options.Property)
            || !string.IsNullOrWhiteSpace(options.ChangeKind);
    }

    private static string? NormalizeEndpointPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && IsHttpMethod(parts[0]))
        {
            trimmed = parts[1];
        }

        if (!trimmed.StartsWith("/", StringComparison.Ordinal) && !trimmed.StartsWith("hash:", StringComparison.Ordinal))
        {
            trimmed = "/" + trimmed;
        }

        return trimmed.Replace('\\', '/').TrimEnd('/');
    }

    private static string? HttpMethodFromEndpointKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && IsHttpMethod(parts[0]) ? parts[0].ToUpperInvariant() : null;
    }

    private static bool IsHttpMethod(string value)
    {
        return value.Equals("GET", StringComparison.OrdinalIgnoreCase)
            || value.Equals("POST", StringComparison.OrdinalIgnoreCase)
            || value.Equals("PUT", StringComparison.OrdinalIgnoreCase)
            || value.Equals("PATCH", StringComparison.OrdinalIgnoreCase)
            || value.Equals("DELETE", StringComparison.OrdinalIgnoreCase)
            || value.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
            || value.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase);
    }

    private static string RouteParameters(ApiDtoFactRow fact)
    {
        return FirstValue(fact.Properties, "routeParameters", "parameters")
            ?? string.Join(",", SplitList(FirstValue(fact.Properties, "normalizedPathKey", "routeTemplate", "path"))
                .Where(value => value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal))
                .Select(value => value.Trim('{', '}', '?', '*'))
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static IEnumerable<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var item in value.Split([',', '/', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return item;
        }
    }

    private static string? MemberName(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var trimmed = symbol.Trim();
        var paren = trimmed.IndexOf('(');
        if (paren >= 0)
        {
            trimmed = trimmed[..paren];
        }

        var lastDot = trimmed.LastIndexOf('.');
        return lastDot >= 0 && lastDot + 1 < trimmed.Length ? trimmed[(lastDot + 1)..] : trimmed;
    }

    private static string? ContainingType(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var trimmed = symbol.Trim();
        var paren = trimmed.IndexOf('(');
        if (paren >= 0)
        {
            trimmed = trimmed[..paren];
        }

        var lastDot = trimmed.LastIndexOf('.');
        return lastDot > 0 ? trimmed[..lastDot] : null;
    }

    private static string SafeDisplay(string value)
    {
        if (value.Contains("://", StringComparison.Ordinal)
            || Path.IsPathFullyQualified(value)
            || value.Contains("password=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("select ", StringComparison.OrdinalIgnoreCase))
        {
            return $"display-hash:{CombinedReportHelpers.Hash(value, 16)}";
        }

        return value;
    }

    private static string SafeHashDisplay(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : $"display-hash:{CombinedReportHelpers.Hash(value, 16)}";
    }

    private static string? SafeMaybeHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Contains("://", StringComparison.Ordinal)
            || Path.IsPathFullyQualified(value)
            || value.Contains("select ", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password=", StringComparison.OrdinalIgnoreCase)
            ? $"hash:{CombinedReportHelpers.Hash(value, 24)}"
            : value;
    }

    private static string? SafeSymbol(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Contains("://", StringComparison.Ordinal) || Path.IsPathFullyQualified(value)
            ? $"symbol-hash:{CombinedReportHelpers.Hash(value, 24)}"
            : value;
    }

    private static string MetadataHash(IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        return CombinedReportHelpers.Hash(string.Join("\n", metadata.Select(pair => $"{pair.Key}={pair.Value}")), 32);
    }

    private static KeyValuePair<string, string?> Pair(string key, string? value) => new(key, value);

    private static string? FirstValue(IReadOnlyDictionary<string, string> properties, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> ReadProperties(string? json)
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

    private static IReadOnlyList<string> CoverageWarnings(IReadOnlyList<ApiDtoContractSourceInfo> sources)
    {
        return sources
            .Where(source => !string.Equals(source.Coverage, "Full", StringComparison.Ordinal))
            .Select(source => $"{source.SourceLabel}: {source.AnalysisLevel}/{source.BuildStatus}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string CoverageFrom(string analysisLevel, string buildStatus, IReadOnlyList<string> gaps)
    {
        return analysisLevel.Contains("Reduced", StringComparison.OrdinalIgnoreCase)
            || buildStatus.Contains("Failed", StringComparison.OrdinalIgnoreCase)
            || buildStatus.Contains("Partial", StringComparison.OrdinalIgnoreCase)
            || gaps.Count > 0
            ? "Reduced"
            : "Full";
    }

    private static string? RepositoryIdentityHash(string? remoteUrl, string? repoName)
    {
        var identity = !string.IsNullOrWhiteSpace(remoteUrl) ? remoteUrl : repoName;
        return string.IsNullOrWhiteSpace(identity) ? null : $"repo-hash:{CombinedReportHelpers.Hash(identity, 24)}";
    }

    private static string? InferLanguage(string? scannerVersion)
    {
        if (string.IsNullOrWhiteSpace(scannerVersion))
        {
            return null;
        }

        var value = scannerVersion.ToLowerInvariant();
        if (value.Contains("typescript", StringComparison.Ordinal) || value.Contains("ts-", StringComparison.Ordinal))
        {
            return "typescript";
        }

        if (value.Contains("python", StringComparison.Ordinal))
        {
            return "python";
        }

        if (value.Contains("jvm", StringComparison.Ordinal) || value.Contains("java", StringComparison.Ordinal) || value.Contains("kotlin", StringComparison.Ordinal))
        {
            return "jvm";
        }

        return value.Contains("tracemap", StringComparison.Ordinal) || value.Contains("csharp", StringComparison.Ordinal) ? "csharp" : null;
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

    private static string? NullIfUnknown(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || value.Equals("unknown", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private static string StringOrDefault(SqliteDataReader reader, int ordinal, string fallback)
    {
        return reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal);
    }

    private static string? StringOrNull(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int IntOrZero(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }

    private static async Task<bool> TableExistsInIndexAsync(string path, string table, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ReadOnlyConnectionString(path));
        await connection.OpenAsync(cancellationToken);
        return await TableExistsAsync(connection, table, cancellationToken);
    }

    private static string RequiredFactTable(string kind) => kind == "combined" ? "combined_facts" : "facts";

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private static string ReadOnlyConnectionString(string path)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
    }

    private static string Cell(string? value) => CombinedReportHelpers.Cell(value);
}
