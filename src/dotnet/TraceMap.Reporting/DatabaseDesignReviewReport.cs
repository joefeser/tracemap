using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record DatabaseDesignReviewOptions(
    string IndexPath,
    string OutputPath,
    string Format = "markdown",
    int MaxObjects = 500,
    int MaxEvidence = 2000,
    int MaxRouteReferences = 500,
    int MaxGaps = 1000);

public sealed record DatabaseDesignReviewResult(
    DatabaseDesignReviewDocument Report,
    string? MarkdownPath,
    string? JsonPath);

public sealed record DatabaseDesignReviewDocument(
    string Version,
    string RuleId,
    string ClaimLevel,
    string Coverage,
    IReadOnlyList<DatabaseDesignSource> Sources,
    DatabaseDesignSummary Summary,
    IReadOnlyList<DatabaseDesignTableGroup> Tables,
    IReadOnlyList<DatabaseDesignEvidenceItem> GlobalObjects,
    IReadOnlyList<DatabaseDesignEvidenceItem> UnlinkedQueries,
    IReadOnlyList<DatabaseDesignGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record DatabaseDesignSource(
    string SourceLabel,
    string CommitSha,
    string Language,
    string AnalysisLevel,
    string BuildStatus,
    bool IdentityVerified,
    IReadOnlyList<string> CoverageWarnings);

public sealed record DatabaseDesignSummary(
    int SourceCount,
    int TableCount,
    int DeclarationCount,
    int OperationCount,
    int QueryReferenceCount,
    int RouteReferenceCount,
    int GlobalObjectCount,
    int UnlinkedQueryCount,
    int GapCount,
    int OmittedObjectCount,
    int OmittedEvidenceCount,
    int OmittedRouteReferenceCount,
    int OmittedGapCount);

public sealed record DatabaseDesignTableGroup(
    string GroupId,
    string SourceLabel,
    string SchemaName,
    string TableName,
    string SchemaResolution,
    string Coverage,
    IReadOnlyList<DatabaseDesignEvidenceItem> Declarations,
    IReadOnlyList<DatabaseDesignEvidenceItem> Operations,
    IReadOnlyList<DatabaseDesignEvidenceItem> QueryReferences,
    IReadOnlyList<DatabaseDesignRouteReference> RouteReferences,
    IReadOnlyList<string> Limitations);

public sealed record DatabaseDesignEvidenceItem(
    string ItemId,
    string EvidenceKind,
    string DisplayName,
    string Classification,
    IReadOnlyList<KeyValuePair<string, string>> Metadata,
    DatabaseDesignEvidenceRef Evidence);

public sealed record DatabaseDesignRouteReference(
    string RouteReferenceId,
    string EntryKind,
    string Method,
    string NormalizedPathKey,
    string PathClassification,
    string TableMatchKind,
    DatabaseDesignEvidenceRef Evidence);

public sealed record DatabaseDesignGap(
    string GapId,
    string GapKind,
    string Classification,
    string Message,
    string? SourceLabel,
    string RuleId,
    string EvidenceTier,
    string Coverage,
    string? CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? ExtractorId,
    string? ExtractorVersion,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> SupportingRuleIds,
    IReadOnlyList<KeyValuePair<string, string>> Metadata,
    IReadOnlyList<string> Limitations);

public sealed record DatabaseDesignEvidenceRef(
    string RuleId,
    string EvidenceTier,
    string SourceLabel,
    string CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? ExtractorId,
    string? ExtractorVersion,
    string CoverageLabel,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> SupportingRuleIds,
    IReadOnlyList<string> Limitations);

public static class DatabaseDesignReviewReporter
{
    public const string PacketRuleId = "database.design-review.packet.v1";
    public const string GapRuleId = "database.design-review.gap.v1";
    private const string Version = "1.0";
    private const string ClaimLevel = "static-evidence";
    private const string DefaultSchema = "default-schema";

    private static readonly IReadOnlyList<string> Limitations =
    [
        "The packet describes bounded static repository evidence only; it does not prove database existence, current schema state, schema completeness, or schema freshness.",
        "Migration operations are repository evidence, not proof that migrations were ordered, applied, compatible, reversible, or safe to run.",
        "Query/table correlation is an exact source-scoped static name match; it does not prove runtime provider, connection, search-path, generated SQL, branch feasibility, or query execution.",
        "EF table and column mappings are bounded compiler-resolved repository evidence; they do not reconstruct the runtime EF model, conventions, provider behavior, generated SQL, or database correspondence.",
        "Route references preserve existing bounded static path evidence; they do not prove runtime reachability, traffic, authorization, deployment, or user exercise.",
        "The packet does not prove data correctness, effective permissions, production state, rollback, release approval, or operational success.",
        "Raw SQL, source snippets, query hashes, connection material, credentials, scheduled command bodies, local paths, private server names, validation output, and arbitrary fact properties are not rendered."
    ];

    private static readonly HashSet<string> DeclarationFactTypes = new(StringComparer.Ordinal)
    {
        FactTypes.PostgresSchemaTableDeclared,
        FactTypes.PostgresSchemaColumnDeclared,
        FactTypes.PostgresSchemaConstraintDeclared,
        FactTypes.PostgresSchemaIndexDeclared
    };

    private static readonly HashSet<string> GlobalFactTypes = new(StringComparer.Ordinal)
    {
        FactTypes.PostgresSchemaSnapshotDeclared,
        FactTypes.PostgresSchemaEnumDeclared,
        FactTypes.PostgresSchemaRoutineDeclared,
        FactTypes.PostgresMigrationFileDeclared
    };

    public static async Task<DatabaseDesignReviewResult> WriteAsync(
        DatabaseDesignReviewOptions options,
        CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(options, cancellationToken);
        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "database-design-review");
        var (markdownPath, jsonPath) = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "database-design-review.md",
            "database-design-review.json",
            report,
            RenderMarkdown,
            CombinedDependencyReporter.JsonOptions,
            cancellationToken);
        return new DatabaseDesignReviewResult(report, markdownPath, jsonPath);
    }

    public static async Task<DatabaseDesignReviewDocument> BuildReportAsync(
        DatabaseDesignReviewOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.IndexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var designInput = await ReadInputAsync(connection, options.IndexPath, cancellationToken);
        var sqlInputs = designInput.SqlInputs;
        var extractorVersions = designInput.IndexKind == "combined"
            ? await ReadExtractorVersionsAsync(connection, cancellationToken)
            : sqlInputs.SelectMany(row => row.Result.Facts)
                .ToDictionary(
                    fact => fact.FactId,
                    fact => (SafeTokenOrNull(fact.Evidence.ExtractorId), SafeTokenOrNull(fact.Evidence.ExtractorVersion)),
                    StringComparer.Ordinal);
        var gaps = new List<DatabaseDesignGap>();
        foreach (var knownGap in designInput.KnownGaps.OrderBy(row => row.SourceLabel, StringComparer.Ordinal).ThenBy(row => row.Category, StringComparer.Ordinal))
        {
            gaps.Add(Gap(
                "SourceCoverageReduced",
                knownGap.SourceLabel,
                $"Source coverage includes `{SafeToken(knownGap.Category, "known-gap")}`; absence conclusions remain coverage-relative.",
                [Pair("count", knownGap.Count.ToString(CultureInfo.InvariantCulture))]));
        }

        var builders = new Dictionary<string, TableBuilder>(StringComparer.Ordinal);
        var globalObjects = new List<DatabaseDesignEvidenceItem>();
        foreach (var input in sqlInputs.OrderBy(row => row.SourceLabel, StringComparer.Ordinal))
        {
            foreach (var fact in input.Result.Facts
                         .Where(fact => fact.RuleId is RuleIds.DatabasePostgresSchemaMigration or RuleIds.DatabasePostgresSchemaMigrationGap)
                         .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
                         .ThenBy(fact => fact.Evidence.StartLine)
                         .ThenBy(fact => fact.FactId, StringComparer.Ordinal))
            {
                if (!CompatibleProvenance(input.Result.Manifest, fact))
                {
                    gaps.Add(Gap(
                        "PostgresEvidenceProvenanceUnavailable",
                        input.SourceLabel,
                        "A PostgreSQL schema or migration fact lacks compatible commit/extractor provenance and was not projected.",
                        [Pair("factType", SafeToken(fact.FactType, "unknown"))],
                        [fact.FactId],
                        commitSha: fact.CommitSha,
                        filePath: fact.Evidence.FilePath,
                        startLine: fact.Evidence.StartLine,
                        endLine: fact.Evidence.EndLine,
                        extractorId: fact.Evidence.ExtractorId,
                        extractorVersion: fact.Evidence.ExtractorVersion,
                        supportingRuleIds: [fact.RuleId]));
                    continue;
                }

                if (fact.FactType == FactTypes.AnalysisGap)
                {
                    gaps.Add(FromFactGap(input.SourceLabel, fact));
                    continue;
                }

                if (GlobalFactTypes.Contains(fact.FactType))
                {
                    globalObjects.Add(FromFact(input.SourceLabel, fact, GlobalKind(fact.FactType)));
                    continue;
                }

                if (!DeclarationFactTypes.Contains(fact.FactType)
                    && fact.FactType != FactTypes.PostgresMigrationOperation)
                {
                    continue;
                }

                if (!TryFactTableKey(input.SourceLabel, fact, out var key))
                {
                    if (fact.FactType == FactTypes.PostgresMigrationOperation
                        && (fact.Properties.ContainsKey("enumName") || fact.Properties.ContainsKey("routineName")))
                    {
                        globalObjects.Add(FromFact(input.SourceLabel, fact, "migration-operation"));
                        continue;
                    }
                    gaps.Add(Gap(
                        "TableIdentityUnavailable",
                        input.SourceLabel,
                        "A PostgreSQL table-scoped fact lacks a bounded table identity and was not grouped.",
                        [Pair("factType", SafeToken(fact.FactType, "unknown"))],
                        [fact.FactId],
                        commitSha: fact.CommitSha,
                        filePath: fact.Evidence.FilePath,
                        startLine: fact.Evidence.StartLine,
                        endLine: fact.Evidence.EndLine,
                        extractorId: fact.Evidence.ExtractorId,
                        extractorVersion: fact.Evidence.ExtractorVersion,
                        supportingRuleIds: [fact.RuleId]));
                    continue;
                }

                var builder = GetBuilder(builders, key);
                var item = FromFact(input.SourceLabel, fact, fact.FactType == FactTypes.PostgresMigrationOperation ? "migration-operation" : DeclarationKind(fact.FactType));
                if (fact.FactType == FactTypes.PostgresMigrationOperation)
                    builder.Operations.Add(item);
                else
                    builder.Declarations.Add(item);
            }
        }

        var tableKeysByEntity = AddEntityFrameworkMappings(sqlInputs, builders, gaps);
        var operationTableByFactId = AddApplicationOperations(
            sqlInputs,
            builders,
            tableKeysByEntity,
            globalObjects,
            gaps);

        var surfaces = CombinedDependencyReporter.BuildSurfaces(designInput.Facts, designInput.Sources)
            .Where(surface => surface.SurfaceKind == "sql-query")
            .OrderBy(surface => surface.SourceLabel, StringComparer.Ordinal)
            .ThenBy(surface => surface.FilePath, StringComparer.Ordinal)
            .ThenBy(surface => surface.StartLine)
            .ThenBy(surface => surface.CombinedFactId, StringComparer.Ordinal)
            .ToArray();
        var queryByFactId = new Dictionary<string, (TableKey Key, DatabaseDesignEvidenceItem Item)>(StringComparer.Ordinal);
        var unlinkedQueries = new List<DatabaseDesignEvidenceItem>();
        foreach (var surface in surfaces)
        {
            if (!TrySurfaceTableKey(surface, out var key) || !builders.TryGetValue(key.StableKey, out var builder))
            {
                unlinkedQueries.Add(FromQuerySurface(
                    surface,
                    extractorVersions.GetValueOrDefault(surface.CombinedFactId),
                    classification: "UnlinkedQuery",
                    matchKind: "none"));
                gaps.Add(Gap(
                    "QueryTableUnmatched",
                    surface.SourceLabel,
                    "A SQL/query surface could not be matched to a bounded PostgreSQL table declaration in the same source.",
                    [
                        Pair("tableIdentity", string.IsNullOrWhiteSpace(surface.TableName) ? "unavailable" : "present"),
                        Pair("matchKind", "none")
                    ],
                    [surface.CombinedFactId],
                    commitSha: surface.CommitSha,
                    filePath: surface.FilePath,
                    startLine: surface.StartLine,
                    endLine: surface.EndLine,
                    extractorId: extractorVersions.GetValueOrDefault(surface.CombinedFactId).Id,
                    extractorVersion: extractorVersions.GetValueOrDefault(surface.CombinedFactId).Version,
                    supportingRuleIds: [surface.RuleId]));
                continue;
            }

            var item = FromQuerySurface(
                surface,
                extractorVersions.GetValueOrDefault(surface.CombinedFactId),
                classification: "StaticNameMatch",
                matchKind: "static-name-match");
            builder.QueryReferences.Add(item);
            queryByFactId[surface.CombinedFactId] = (key, item);
        }

        CombinedDependencyPathReport? pathReport = null;
        if (designInput.IndexKind == "combined")
        {
            try
            {
                pathReport = await CombinedDependencyPathReporter.BuildReportAsync(
                    new CombinedDependencyPathOptions(
                        options.IndexPath,
                        "database-design-review",
                        "json",
                        ToSurface: "sql-query",
                        IncludeLegacyRoots: true,
                        MaxDepth: 8,
                        MaxPaths: options.MaxRouteReferences == int.MaxValue
                            ? int.MaxValue
                            : options.MaxRouteReferences + 1,
                        MaxFrontier: 10000),
                    cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                gaps.Add(Gap("RouteEvidenceUnavailable", null, "Bounded path evidence is unavailable for the combined index.", [Pair("reason", SafeReason(exception.Message))]));
            }
        }

        var routeReferences = new List<(TableKey Key, DatabaseDesignRouteReference Row)>();
        var routedQueryFactIds = new HashSet<string>(StringComparer.Ordinal);
        if (pathReport is not null)
        {
            foreach (var path in pathReport.Paths.OrderBy(row => row.PathId, StringComparer.Ordinal))
            {
                var terminal = path.Nodes.LastOrDefault(node => node.SurfaceKind == "sql-query");
                var entry = path.Nodes.FirstOrDefault(node => node.NodeKind is "EndpointRoute" or "EndpointClient" or "webforms-event" or "webforms-lifecycle" or "winforms-event");
                if (terminal?.CombinedFactId is null || entry is null)
                {
                    continue;
                }

                if (!queryByFactId.TryGetValue(terminal.CombinedFactId, out var query))
                {
                    var extractor = extractorVersions.GetValueOrDefault(terminal.CombinedFactId);
                    gaps.Add(Gap(
                        "RouteQueryTableUnmatched",
                        terminal.SourceLabel,
                        "A proven static path reaches a SQL/query surface that is not linked to a bounded PostgreSQL table group.",
                        [Pair("pathClassification", SafeToken(path.Classification, "unknown"))],
                        path.SupportingFactIds,
                        path.SupportingEdgeIds,
                        terminal.CommitSha,
                        terminal.FilePath,
                        terminal.StartLine,
                        terminal.EndLine,
                        extractor.Id,
                        extractor.Version,
                        path.Edges.Select(edge => edge.RuleId)
                            .Concat(path.Nodes.Select(node => node.RuleId).OfType<string>())
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray()));
                    continue;
                }

                routeReferences.Add((
                    query.Key,
                    FromPath(
                        path,
                        entry,
                        entry.CombinedFactId is null
                            ? default
                            : extractorVersions.GetValueOrDefault(entry.CombinedFactId))));
                routedQueryFactIds.Add(terminal.CombinedFactId);
            }

            foreach (var gap in pathReport.Gaps.Where(row => row.GapKind is "TruncatedByLimit" or "TraversalBounds" or "SchemaMissing"))
            {
                gaps.Add(new DatabaseDesignGap(
                    StableId("gap", "path", gap.GapId),
                    gap.GapKind,
                    "PartialAnalysis",
                    gap.Message,
                    gap.SourceLabel,
                    gap.RuleId ?? GapRuleId,
                    NormalizeTier(gap.EvidenceTier),
                    "reduced",
                    gap.CommitSha,
                    SafePath(gap.FilePath),
                    gap.StartLine,
                    gap.EndLine,
                    null,
                    SafeTokenOrNull(gap.ExtractorVersion),
                    gap.CombinedFactId is null ? [] : [gap.CombinedFactId],
                    [],
                    gap.RuleId is null ? [GapRuleId] : [gap.RuleId],
                    [],
                    gap.Reason is null ? Limitations : [gap.Reason, .. Limitations]));
            }
        }

        foreach (var (factId, query) in queryByFactId.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            if (designInput.IndexKind == "single" || routedQueryFactIds.Contains(factId))
                continue;
            gaps.Add(Gap(
                "QueryRoutePathUnavailable",
                query.Item.Evidence.SourceLabel,
                "A bounded SQL/query reference matches a PostgreSQL table, but no existing static route path reaches that query under available graph coverage.",
                [Pair("matchKind", "static-name-match")],
                [factId],
                commitSha: query.Item.Evidence.CommitSha,
                filePath: query.Item.Evidence.FilePath,
                startLine: query.Item.Evidence.StartLine,
                endLine: query.Item.Evidence.EndLine,
                extractorId: query.Item.Evidence.ExtractorId,
                extractorVersion: query.Item.Evidence.ExtractorVersion,
                supportingRuleIds: [query.Item.Evidence.RuleId]));
        }

        CombinedDependencyPathReport? operationPathReport = null;
        var operationPathCoverageReduced = false;
        if (designInput.IndexKind == "combined" && operationTableByFactId.Count > 0)
        {
            try
            {
                operationPathReport = await CombinedDependencyPathReporter.BuildReportAsync(
                    new CombinedDependencyPathOptions(
                        options.IndexPath,
                        "database-design-review-operations",
                        "json",
                        ToSurface: "sql-persistence",
                        IncludeLegacyRoots: true,
                        MaxDepth: 8,
                        MaxPaths: options.MaxRouteReferences == int.MaxValue
                            ? int.MaxValue
                            : options.MaxRouteReferences + 1,
                        MaxFrontier: 10000),
                    cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                operationPathCoverageReduced = true;
                var anchor = operationTableByFactId
                    .OrderBy(row => row.Key, StringComparer.Ordinal)
                    .First();
                gaps.Add(Gap(
                    "OperationRouteEvidenceUnavailable",
                    anchor.Value.Key.SourceLabel,
                    "Bounded path evidence is unavailable for application database-operation candidates.",
                    [Pair("reason", SafeReason(exception.Message))],
                    [anchor.Key],
                    commitSha: anchor.Value.Item.Evidence.CommitSha,
                    filePath: anchor.Value.Item.Evidence.FilePath,
                    startLine: anchor.Value.Item.Evidence.StartLine,
                    endLine: anchor.Value.Item.Evidence.EndLine,
                    extractorId: anchor.Value.Item.Evidence.ExtractorId,
                    extractorVersion: anchor.Value.Item.Evidence.ExtractorVersion,
                    supportingRuleIds: [anchor.Value.Item.Evidence.RuleId]));
            }
        }

        var routedOperationFactIds = new HashSet<string>(StringComparer.Ordinal);
        if (operationPathReport is not null)
        {
            foreach (var path in operationPathReport.Paths.OrderBy(row => row.PathId, StringComparer.Ordinal))
            {
                var terminal = path.Nodes.LastOrDefault(node => node.SurfaceKind == "sql-persistence");
                var entry = path.Nodes.FirstOrDefault(node => node.NodeKind is "EndpointRoute" or "EndpointClient" or "webforms-event" or "webforms-lifecycle" or "winforms-event");
                if (terminal?.CombinedFactId is null
                    || entry is null
                    || !operationTableByFactId.TryGetValue(terminal.CombinedFactId, out var operation))
                {
                    continue;
                }

                routeReferences.Add((
                    operation.Key,
                    FromPath(
                        path,
                        entry,
                        entry.CombinedFactId is null
                            ? default
                            : extractorVersions.GetValueOrDefault(entry.CombinedFactId))));
                routedOperationFactIds.Add(terminal.CombinedFactId);
            }

            foreach (var gap in operationPathReport.Gaps
                         .Where(row => row.GapKind is "TruncatedByLimit" or "TraversalBounds" or "SchemaMissing"))
            {
                operationPathCoverageReduced = true;
                gaps.Add(new DatabaseDesignGap(
                    StableId("gap", "operation-path", gap.GapId),
                    gap.GapKind,
                    "PartialAnalysis",
                    gap.Message,
                    gap.SourceLabel,
                    gap.RuleId ?? GapRuleId,
                    NormalizeTier(gap.EvidenceTier),
                    "reduced",
                    gap.CommitSha,
                    SafePath(gap.FilePath),
                    gap.StartLine,
                    gap.EndLine,
                    null,
                    SafeTokenOrNull(gap.ExtractorVersion),
                    gap.CombinedFactId is null ? [] : [gap.CombinedFactId],
                    [],
                    gap.RuleId is null ? [GapRuleId] : [gap.RuleId],
                    [Pair("pathScope", "application-database-operations")],
                    gap.Reason is null ? Limitations : [gap.Reason, .. Limitations]));
            }
        }

        foreach (var (factId, operation) in operationTableByFactId.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            if (designInput.IndexKind == "single" || routedOperationFactIds.Contains(factId))
                continue;
            gaps.Add(Gap(
                operationPathCoverageReduced
                    ? "OperationRoutePathCoverageReduced"
                    : "OperationRoutePathUnavailable",
                operation.Key.SourceLabel,
                operationPathCoverageReduced
                    ? "A bounded application database-operation candidate matches a PostgreSQL table, but the route-path search was truncated or reduced before reachability could be established."
                    : "A bounded application database-operation candidate matches a PostgreSQL table, but no existing static route path reaches that operation under available graph coverage.",
                [Pair("matchKind", "bounded-operation-table-match")],
                [factId],
                commitSha: operation.Item.Evidence.CommitSha,
                filePath: operation.Item.Evidence.FilePath,
                startLine: operation.Item.Evidence.StartLine,
                endLine: operation.Item.Evidence.EndLine,
                extractorId: operation.Item.Evidence.ExtractorId,
                extractorVersion: operation.Item.Evidence.ExtractorVersion,
                supportingRuleIds: [operation.Item.Evidence.RuleId]));
        }

        var omittedRoutes = Math.Max(0, routeReferences.Count - options.MaxRouteReferences);
        foreach (var route in routeReferences
                     .OrderBy(row => row.Key.StableKey, StringComparer.Ordinal)
                     .ThenBy(row => row.Row.RouteReferenceId, StringComparer.Ordinal)
                     .Take(options.MaxRouteReferences))
        {
            GetBuilder(builders, route.Key).RouteReferences.Add(route.Row);
        }
        if (omittedRoutes > 0)
            gaps.Add(TruncationGap("route-references", omittedRoutes));

        var allTables = builders.Values
            .Select(builder => builder.Build())
            .OrderBy(row => row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.SchemaName, StringComparer.Ordinal)
            .ThenBy(row => row.TableName, StringComparer.Ordinal)
            .ThenBy(row => row.GroupId, StringComparer.Ordinal)
            .ToArray();
        var omittedObjects = Math.Max(0, allTables.Length + globalObjects.Count - options.MaxObjects);
        var tableTake = Math.Min(allTables.Length, options.MaxObjects);
        var tables = allTables.Take(tableTake).ToArray();
        var globalTake = Math.Max(0, options.MaxObjects - tableTake);
        var globals = globalObjects
            .OrderBy(row => row.Evidence.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.EvidenceKind, StringComparer.Ordinal)
            .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
            .ThenBy(row => row.ItemId, StringComparer.Ordinal)
            .Take(globalTake)
            .ToArray();
        if (omittedObjects > 0)
            gaps.Add(TruncationGap("design-objects", omittedObjects));

        var evidenceCount = tables.Sum(TableEvidenceCount) + globals.Length + unlinkedQueries.Count;
        var omittedEvidence = Math.Max(0, evidenceCount - options.MaxEvidence);
        if (omittedEvidence > 0)
        {
            (tables, globals, unlinkedQueries) = ApplyEvidenceCap(tables, globals, unlinkedQueries, options.MaxEvidence);
            gaps.Add(TruncationGap("evidence-rows", omittedEvidence));
        }

        var sortedUnlinked = unlinkedQueries
            .OrderBy(row => row.Evidence.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.ItemId, StringComparer.Ordinal)
            .ToArray();
        var sources = designInput.Sources
            .OrderBy(source => source.Label, StringComparer.Ordinal)
            .ThenBy(source => source.SourceIndexId, StringComparer.Ordinal)
            .Select(source => new DatabaseDesignSource(
                SafeLabel(source.Label),
                SafeCommit(source.CommitSha),
                SafeToken(source.Language, "unknown"),
                SafeToken(source.AnalysisLevel, "unknown"),
                SafeToken(source.BuildStatus, "unknown"),
                CombinedReportHelpers.SourceIdentityVerified(source),
                designInput.CoverageWarnings
                    .Where(warning => warning.Contains(source.Label, StringComparison.OrdinalIgnoreCase))
                    .Select(SafeReason)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
        foreach (var source in sources.Where(source => source.CoverageWarnings.Count > 0))
        {
            gaps.Add(Gap(
                "SourceCoverageWarning",
                source.SourceLabel,
                "The source reports reduced analysis, build, language, or identity coverage; packet conclusions remain partial.",
                [Pair("warningCount", source.CoverageWarnings.Count.ToString(CultureInfo.InvariantCulture))]));
        }
        if (designInput.IndexKind == "single")
        {
            var anchor = sources.FirstOrDefault();
            gaps.Add(Gap(
                "SingleIndexRoutePathUnavailable",
                anchor?.SourceLabel,
                "Single-index input does not contain the combined graph/path contract; route references were not evaluated.",
                [
                    Pair("indexKind", "single"),
                    Pair("routeReferenceCount", "0")
                ],
                commitSha: anchor?.CommitSha));
        }

        var compatibleDesignCount = tables.Sum(TableEvidenceCount) + globals.Length;
        if (compatibleDesignCount == 0
            && gaps.All(gap => gap.GapKind != "CompatiblePostgresEvidenceUnavailable"))
        {
            gaps.Add(Gap(
                "CompatiblePostgresEvidenceUnavailable",
                null,
                $"No compatible bounded PostgreSQL schema, migration, or snapshot evidence is present in the {designInput.IndexKind} index."));
        }

        var sortedGaps = gaps
            .GroupBy(row => row.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(row => row.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(row => row.GapKind, StringComparer.Ordinal)
            .ThenBy(row => row.GapId, StringComparer.Ordinal)
            .ToArray();
        int omittedGaps;
        List<DatabaseDesignGap> cappedGaps;
        if (sortedGaps.Length <= options.MaxGaps)
        {
            omittedGaps = 0;
            cappedGaps = sortedGaps.ToList();
        }
        else
        {
            var retainedOriginalCount = options.MaxGaps - 1;
            omittedGaps = sortedGaps.Length - retainedOriginalCount;
            cappedGaps = sortedGaps.Take(retainedOriginalCount).ToList();
            cappedGaps.Add(TruncationGap("gaps", omittedGaps));
        }

        var coverage = compatibleDesignCount == 0
            ? "unavailable"
            : cappedGaps.Count == 0 && omittedObjects == 0 && omittedEvidence == 0 && omittedRoutes == 0 && omittedGaps == 0
                ? "available"
                : "partial";

        var summary = new DatabaseDesignSummary(
            sources.Length,
            tables.Length,
            tables.Sum(row => row.Declarations.Count),
            tables.Sum(row => row.Operations.Count),
            tables.Sum(row => row.QueryReferences.Count),
            tables.Sum(row => row.RouteReferences.Count),
            globals.Length,
            sortedUnlinked.Length,
            cappedGaps.Count,
            omittedObjects,
            omittedEvidence,
            omittedRoutes,
            omittedGaps);
        return new DatabaseDesignReviewDocument(
            Version,
            PacketRuleId,
            ClaimLevel,
            coverage,
            sources,
            summary,
            tables,
            globals,
            sortedUnlinked,
            cappedGaps.OrderBy(row => row.SourceLabel ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(row => row.GapKind, StringComparer.Ordinal)
                .ThenBy(row => row.GapId, StringComparer.Ordinal)
                .ToArray(),
            Limitations);
    }

    private static void ValidateOptions(DatabaseDesignReviewOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
            throw new ArgumentException("database-design-review requires --index <index.sqlite|combined.sqlite>.");
        if (string.IsNullOrWhiteSpace(options.OutputPath))
            throw new ArgumentException("database-design-review requires --out <path>.");
        if (options.MaxObjects <= 0 || options.MaxEvidence <= 0 || options.MaxRouteReferences <= 0 || options.MaxGaps <= 0)
            throw new ArgumentException("database-design-review caps must be positive integers.");
        _ = CombinedReportHelpers.NormalizeFormat(options.Format, "database-design-review");
    }

    private static bool CompatibleProvenance(ScanManifest manifest, CodeFact fact) =>
        KnownCommit(manifest.CommitSha)
        && KnownCommit(fact.CommitSha)
        && string.Equals(manifest.CommitSha, fact.CommitSha, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(fact.Evidence.ExtractorId)
        && !fact.Evidence.ExtractorId.Equals("unknown", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(fact.Evidence.ExtractorVersion)
        && !fact.Evidence.ExtractorVersion.Equals("unknown", StringComparison.OrdinalIgnoreCase);

    private static bool KnownCommit(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 7 and <= 64
        && value.All(Uri.IsHexDigit)
        && value.Trim('0').Length > 0;

    private static DatabaseDesignEvidenceItem FromFact(string sourceLabel, CodeFact fact, string kind)
    {
        var metadata = SafeFactMetadata(fact);
        var displayName = kind switch
        {
            "column" => fact.Properties.GetValueOrDefault("columnName") ?? "column",
            "constraint" => fact.Properties.GetValueOrDefault("constraintName") ?? "constraint",
            "index" => fact.Properties.GetValueOrDefault("indexName") ?? "index",
            "enum" => fact.Properties.GetValueOrDefault("enumName") ?? "enum",
            "routine" => fact.Properties.GetValueOrDefault("routineName") ?? "routine",
            "snapshot" => "checked-in-schema-snapshot",
            "migration-file" => "migration-file",
            "migration-operation" => fact.Properties.GetValueOrDefault("operationKind") ?? "migration-operation",
            _ => fact.Properties.GetValueOrDefault("tableName") ?? kind
        };
        var limitations = SplitLimitations(fact.Properties.GetValueOrDefault("limitations"))
            .Concat(Limitations)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var safeDisplayName = kind is "migration-operation" or "snapshot" or "migration-file"
            ? SafeToken(displayName, kind)
            : SafeIdentifier(displayName, kind);
        return new DatabaseDesignEvidenceItem(
            StableId("item", sourceLabel, kind, fact.FactId),
            kind,
            safeDisplayName,
            kind == "migration-operation" ? "ReviewRecommended" : "StaticEvidence",
            metadata,
            new DatabaseDesignEvidenceRef(
                fact.RuleId,
                NormalizeTier(fact.EvidenceTier),
                SafeLabel(sourceLabel),
                SafeCommit(fact.CommitSha),
                SafePath(fact.Evidence.FilePath),
                fact.Evidence.StartLine,
                fact.Evidence.EndLine,
                SafeTokenOrNull(fact.Evidence.ExtractorId),
                SafeTokenOrNull(fact.Evidence.ExtractorVersion),
                SafeToken(fact.Properties.GetValueOrDefault("coverageLabel"), "bounded-static-evidence"),
                [fact.FactId],
                [],
                [fact.RuleId],
                limitations));
    }

    private static DatabaseDesignGap FromFactGap(string sourceLabel, CodeFact fact)
    {
        var kind = SafeToken(fact.Properties.GetValueOrDefault("classification"), "PostgresSchemaMigrationGap");
        return new DatabaseDesignGap(
            StableId("gap", sourceLabel, fact.FactId),
            kind,
            "PartialAnalysis",
            "PostgreSQL schema/migration evidence has an explicit upstream coverage gap.",
            SafeLabel(sourceLabel),
            fact.RuleId,
            NormalizeTier(fact.EvidenceTier),
            "reduced",
            SafeCommit(fact.CommitSha),
            SafePath(fact.Evidence.FilePath),
            fact.Evidence.StartLine,
            fact.Evidence.EndLine,
            SafeTokenOrNull(fact.Evidence.ExtractorId),
            SafeTokenOrNull(fact.Evidence.ExtractorVersion),
            [fact.FactId],
            [],
            [fact.RuleId],
            SafeFactMetadata(fact),
            SplitLimitations(fact.Properties.GetValueOrDefault("limitations")).Concat(Limitations).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static Dictionary<string, List<TableKey>> AddEntityFrameworkMappings(
        IReadOnlyList<SqlEvidenceInput> inputs,
        Dictionary<string, TableBuilder> builders,
        List<DatabaseDesignGap> gaps)
    {
        var tableKeysByEntity = new Dictionary<string, List<TableKey>>(StringComparer.Ordinal);
        var columnMappings = new List<(string SourceLabel, CodeFact Fact)>();
        foreach (var input in inputs.OrderBy(row => row.SourceLabel, StringComparer.Ordinal))
        {
            foreach (var fact in input.Result.Facts
                         .Where(fact =>
                             (fact.RuleId == RuleIds.DatabaseEntityFramework
                                 && (fact.FactType == FactTypes.DatabaseColumnMapping
                                     || fact.FactType == FactTypes.AnalysisGap))
                             || (fact.RuleId == RuleIds.CSharpSemanticContractMapping
                                 && fact.FactType == FactTypes.DatabaseColumnMapping))
                         .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
                         .ThenBy(fact => fact.Evidence.StartLine)
                         .ThenBy(fact => fact.FactId, StringComparer.Ordinal))
            {
                if (!CompatibleProvenance(input.Result.Manifest, fact))
                {
                    gaps.Add(Gap(
                        "EntityFrameworkEvidenceProvenanceUnavailable",
                        input.SourceLabel,
                        "An EF mapping fact lacks compatible commit/extractor provenance and was not projected.",
                        [Pair("factType", SafeToken(fact.FactType, "unknown"))],
                        [fact.FactId],
                        commitSha: fact.CommitSha,
                        filePath: fact.Evidence.FilePath,
                        startLine: fact.Evidence.StartLine,
                        endLine: fact.Evidence.EndLine,
                        extractorId: fact.Evidence.ExtractorId,
                        extractorVersion: fact.Evidence.ExtractorVersion,
                        supportingRuleIds: [fact.RuleId]));
                    continue;
                }

                if (fact.FactType == FactTypes.AnalysisGap)
                {
                    gaps.Add(FromEntityFrameworkGap(input.SourceLabel, fact));
                    continue;
                }

                if (fact.Properties.GetValueOrDefault("mappingKind") == "DatabaseTableMapping")
                {
                    if (!TryFindEntityFrameworkTable(
                            input.SourceLabel,
                            fact.Properties.GetValueOrDefault("schemaName"),
                            fact.Properties.GetValueOrDefault("mappedName"),
                            builders,
                            out var key,
                            out var matchKind))
                    {
                        gaps.Add(EntityFrameworkMappingGap(
                            "EntityFrameworkTableMappingUnmatched",
                            input.SourceLabel,
                            "An explicit EF table mapping could not be linked to one bounded PostgreSQL table declaration in the same source.",
                            fact));
                        continue;
                    }

                    builders[key.StableKey].Declarations.Add(FromEntityFrameworkMapping(
                        input.SourceLabel,
                        fact,
                        "ef-table-mapping",
                        matchKind));
                    var entityType = fact.Properties.GetValueOrDefault("entityType");
                    if (!string.IsNullOrWhiteSpace(entityType))
                    {
                        var entityKey = $"{SafeLabel(input.SourceLabel)}\0{entityType}";
                        if (!tableKeysByEntity.TryGetValue(entityKey, out var keys))
                        {
                            keys = [];
                            tableKeysByEntity.Add(entityKey, keys);
                        }
                        keys.Add(key);
                    }
                    continue;
                }

                if (fact.Properties.GetValueOrDefault("mappingKind") == "DatabaseColumnMapping")
                {
                    columnMappings.Add((input.SourceLabel, fact));
                }
            }
        }

        foreach (var (sourceLabel, fact) in columnMappings)
        {
            var entityType = fact.Properties.GetValueOrDefault("entityType");
            var entityKey = $"{SafeLabel(sourceLabel)}\0{entityType}";
            var keys = tableKeysByEntity.GetValueOrDefault(entityKey)?
                .DistinctBy(key => key.StableKey)
                .ToArray() ?? [];
            if (string.IsNullOrWhiteSpace(entityType) || keys.Length != 1)
            {
                gaps.Add(EntityFrameworkMappingGap(
                    keys.Length > 1
                        ? "EntityFrameworkColumnTableMappingAmbiguous"
                        : "EntityFrameworkColumnTableMappingUnavailable",
                    sourceLabel,
                    keys.Length > 1
                        ? "An explicit EF column mapping has more than one bounded entity-to-table candidate and was not assigned."
                        : "An explicit EF column mapping lacks one bounded entity-to-table match and was not assigned.",
                    fact));
                continue;
            }

            builders[keys[0].StableKey].Declarations.Add(FromEntityFrameworkMapping(
                sourceLabel,
                fact,
                "ef-column-mapping",
                "entity-table-static-match"));
        }
        return tableKeysByEntity;
    }

    private static Dictionary<string, (TableKey Key, DatabaseDesignEvidenceItem Item)> AddApplicationOperations(
        IReadOnlyList<SqlEvidenceInput> inputs,
        Dictionary<string, TableBuilder> builders,
        IReadOnlyDictionary<string, List<TableKey>> tableKeysByEntity,
        List<DatabaseDesignEvidenceItem> globalObjects,
        List<DatabaseDesignGap> gaps)
    {
        var operationTableByFactId = new Dictionary<string, (TableKey Key, DatabaseDesignEvidenceItem Item)>(StringComparer.Ordinal);
        foreach (var input in inputs.OrderBy(row => row.SourceLabel, StringComparer.Ordinal))
        {
            foreach (var fact in input.Result.Facts
                         .Where(fact => fact.RuleId == RuleIds.DatabaseOperationCallPattern)
                         .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
                         .ThenBy(fact => fact.Evidence.StartLine)
                         .ThenBy(fact => fact.FactId, StringComparer.Ordinal))
            {
                if (!CompatibleProvenance(input.Result.Manifest, fact))
                {
                    gaps.Add(OperationGap(
                        "DatabaseOperationEvidenceProvenanceUnavailable",
                        input.SourceLabel,
                        "An application database-operation fact lacks compatible commit/extractor provenance and was not projected.",
                        fact));
                    continue;
                }

                if (fact.FactType == FactTypes.AnalysisGap)
                {
                    gaps.Add(FromOperationGap(input.SourceLabel, fact));
                    continue;
                }
                if (fact.FactType != FactTypes.DatabaseOperationCandidate)
                    continue;

                var operationKind = fact.Properties.GetValueOrDefault("operationKind");
                if (operationKind is "save-boundary"
                    or "transaction-begin"
                    or "transaction-commit"
                    or "transaction-rollback")
                {
                    globalObjects.Add(FromApplicationOperation(input.SourceLabel, fact, "boundary-only"));
                    continue;
                }

                var keys = ResolveOperationTableKeys(input.SourceLabel, fact, builders, tableKeysByEntity);
                if (keys.Length != 1)
                {
                    gaps.Add(OperationGap(
                        keys.Length > 1
                            ? "DatabaseOperationTableMappingAmbiguous"
                            : "DatabaseOperationTableMappingUnavailable",
                        input.SourceLabel,
                        keys.Length > 1
                            ? "A static application database-operation candidate has more than one bounded table match and was not assigned."
                            : "A static application database-operation candidate lacks one bounded PostgreSQL table match and was not assigned.",
                        fact));
                    continue;
                }

                var item = FromApplicationOperation(input.SourceLabel, fact, OperationMatchKind(fact));
                builders[keys[0].StableKey].Operations.Add(item);
                operationTableByFactId[fact.FactId] = (keys[0], item);
            }
        }
        return operationTableByFactId;
    }

    private static TableKey[] ResolveOperationTableKeys(
        string sourceLabel,
        CodeFact fact,
        Dictionary<string, TableBuilder> builders,
        IReadOnlyDictionary<string, List<TableKey>> tableKeysByEntity)
    {
        var tableIdentity = fact.Properties.GetValueOrDefault("tableName");
        if (!string.IsNullOrWhiteSpace(tableIdentity))
        {
            var parts = tableIdentity.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && TryFindEntityFrameworkTable(sourceLabel, parts[0], parts[1], builders, out var exact, out _))
                return [exact];
            if (parts.Length == 1
                && TryFindEntityFrameworkTable(sourceLabel, null, parts[0], builders, out var unique, out _))
                return [unique];
            return [];
        }

        var entityType = fact.Properties.GetValueOrDefault("entityType");
        if (string.IsNullOrWhiteSpace(entityType))
            return [];
        var entityKey = $"{SafeLabel(sourceLabel)}\0{entityType}";
        return tableKeysByEntity.GetValueOrDefault(entityKey)?
            .DistinctBy(key => key.StableKey)
            .ToArray() ?? [];
    }

    private static string OperationMatchKind(CodeFact fact) =>
        !string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("tableName"))
            ? "static-table-name-match"
            : "entity-table-static-match";

    private static DatabaseDesignEvidenceItem FromApplicationOperation(
        string sourceLabel,
        CodeFact fact,
        string matchKind)
    {
        var operationKind = SafeToken(fact.Properties.GetValueOrDefault("operationKind"), "operation-candidate");
        var metadata = SortMetadata(
        [
            Pair("frameworkFamily", SafeToken(fact.Properties.GetValueOrDefault("frameworkFamily"), "unknown")),
            Pair("methodName", SafeToken(fact.Properties.GetValueOrDefault("methodName"), "unknown")),
            Pair("operationKind", operationKind),
            Pair("targetIdentityStatus", SafeToken(fact.Properties.GetValueOrDefault("targetIdentityStatus"), "unavailable")),
            Pair("entityType", SafeTypeName(fact.Properties.GetValueOrDefault("entityType"))),
            Pair("tableName", SafeIdentifier(fact.Properties.GetValueOrDefault("tableName"), "unavailable")),
            Pair("sqlOperationName", SafeToken(fact.Properties.GetValueOrDefault("sqlOperationName"), "unavailable")),
            Pair("matchKind", matchKind)
        ]);
        return new DatabaseDesignEvidenceItem(
            StableId("item", sourceLabel, "application-operation", fact.FactId),
            "application-operation",
            operationKind,
            "CandidateOnly",
            metadata,
            new DatabaseDesignEvidenceRef(
                fact.RuleId,
                NormalizeTier(fact.EvidenceTier),
                SafeLabel(sourceLabel),
                SafeCommit(fact.CommitSha),
                SafePath(fact.Evidence.FilePath),
                fact.Evidence.StartLine,
                fact.Evidence.EndLine,
                SafeTokenOrNull(fact.Evidence.ExtractorId),
                SafeTokenOrNull(fact.Evidence.ExtractorVersion),
                SafeToken(fact.Properties.GetValueOrDefault("coverageLabel"), "bounded-static-call"),
                [fact.FactId],
                [],
                [fact.RuleId],
                SplitLimitations(fact.Properties.GetValueOrDefault("limitations"))
                    .Concat(Limitations)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()));
    }

    private static DatabaseDesignGap FromOperationGap(string sourceLabel, CodeFact fact)
    {
        var kind = SafeToken(fact.Properties.GetValueOrDefault("classification"), "DatabaseOperationCoverageGap");
        return OperationGap(
            kind,
            sourceLabel,
            "Application database-operation evidence has an explicit upstream coverage gap.",
            fact);
    }

    private static DatabaseDesignGap OperationGap(
        string kind,
        string sourceLabel,
        string message,
        CodeFact fact) =>
        new(
            StableId("gap", sourceLabel, kind, fact.FactId),
            kind,
            "PartialAnalysis",
            message,
            SafeLabel(sourceLabel),
            fact.RuleId,
            NormalizeTier(fact.EvidenceTier),
            "reduced",
            SafeCommit(fact.CommitSha),
            SafePath(fact.Evidence.FilePath),
            fact.Evidence.StartLine,
            fact.Evidence.EndLine,
            SafeTokenOrNull(fact.Evidence.ExtractorId),
            SafeTokenOrNull(fact.Evidence.ExtractorVersion),
            [fact.FactId],
            [],
            [fact.RuleId],
            SortMetadata(
            [
                Pair("frameworkFamily", SafeToken(fact.Properties.GetValueOrDefault("frameworkFamily"), "unknown")),
                Pair("methodName", SafeToken(fact.Properties.GetValueOrDefault("methodName"), "unknown")),
                Pair("operationKind", SafeToken(fact.Properties.GetValueOrDefault("operationKind"), "unknown"))
            ]),
            SplitLimitations(fact.Properties.GetValueOrDefault("limitations"))
                .Concat(Limitations)
                .Distinct(StringComparer.Ordinal)
                .ToArray());

    private static bool TryFindEntityFrameworkTable(
        string sourceLabel,
        string? schemaName,
        string? tableName,
        Dictionary<string, TableBuilder> builders,
        out TableKey key,
        out string matchKind)
    {
        matchKind = string.Empty;
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            if (TryTableKey(sourceLabel, schemaName, tableName, out key)
                && builders.ContainsKey(key.StableKey))
            {
                matchKind = "exact-schema-table-match";
                return true;
            }
            return Fail(out key);
        }

        var normalizedTable = NormalizeIdentifier(tableName);
        if (normalizedTable is null)
            return Fail(out key);
        var candidates = builders.Values
            .Select(builder => builder.Key)
            .Where(candidate => candidate.SourceLabel.Equals(SafeLabel(sourceLabel), StringComparison.Ordinal)
                && candidate.TableName.Equals(normalizedTable, StringComparison.Ordinal))
            .DistinctBy(candidate => candidate.StableKey)
            .ToArray();
        if (candidates.Length != 1)
            return Fail(out key);
        key = candidates[0];
        matchKind = "unique-table-name-match-schema-unspecified";
        return true;
    }

    private static DatabaseDesignEvidenceItem FromEntityFrameworkMapping(
        string sourceLabel,
        CodeFact fact,
        string kind,
        string matchKind)
    {
        var metadata = SortMetadata(
        [
            Pair("configurationKind", SafeToken(fact.Properties.GetValueOrDefault("configurationKind"), "unknown")),
            Pair("entityType", SafeTypeName(fact.Properties.GetValueOrDefault("entityType"))),
            Pair("mappedName", SafeIdentifier(fact.Properties.GetValueOrDefault("mappedName"), "unavailable")),
            Pair("memberName", SafeIdentifier(fact.Properties.GetValueOrDefault("memberName"), "unavailable")),
            Pair("schemaName", SafeIdentifier(fact.Properties.GetValueOrDefault("schemaName"), "unspecified")),
            Pair("matchKind", matchKind)
        ]);
        var limitations = SplitLimitations(fact.Properties.GetValueOrDefault("limitations"))
            .Concat(Limitations)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new DatabaseDesignEvidenceItem(
            StableId("item", sourceLabel, kind, fact.FactId),
            kind,
            SafeIdentifier(fact.Properties.GetValueOrDefault("mappedName"), kind),
            "StaticEvidence",
            metadata,
            new DatabaseDesignEvidenceRef(
                fact.RuleId,
                NormalizeTier(fact.EvidenceTier),
                SafeLabel(sourceLabel),
                SafeCommit(fact.CommitSha),
                SafePath(fact.Evidence.FilePath),
                fact.Evidence.StartLine,
                fact.Evidence.EndLine,
                SafeTokenOrNull(fact.Evidence.ExtractorId),
                SafeTokenOrNull(fact.Evidence.ExtractorVersion),
                SafeToken(fact.Properties.GetValueOrDefault("coverageLabel"), "bounded-static-evidence"),
                [fact.FactId],
                [],
                [fact.RuleId],
                limitations));
    }

    private static DatabaseDesignGap FromEntityFrameworkGap(string sourceLabel, CodeFact fact)
    {
        var kind = SafeToken(fact.Properties.GetValueOrDefault("classification"), "EntityFrameworkMappingGap");
        return new DatabaseDesignGap(
            StableId("gap", sourceLabel, fact.FactId),
            kind,
            "PartialAnalysis",
            "EF model mapping evidence has an explicit upstream coverage gap.",
            SafeLabel(sourceLabel),
            fact.RuleId,
            NormalizeTier(fact.EvidenceTier),
            "reduced",
            SafeCommit(fact.CommitSha),
            SafePath(fact.Evidence.FilePath),
            fact.Evidence.StartLine,
            fact.Evidence.EndLine,
            SafeTokenOrNull(fact.Evidence.ExtractorId),
            SafeTokenOrNull(fact.Evidence.ExtractorVersion),
            [fact.FactId],
            [],
            [fact.RuleId],
            SortMetadata(
            [
                Pair("classification", kind),
                Pair("configurationMethod", SafeToken(fact.Properties.GetValueOrDefault("configurationMethod"), "unknown"))
            ]),
            SplitLimitations(fact.Properties.GetValueOrDefault("limitations")).Concat(Limitations).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static DatabaseDesignGap EntityFrameworkMappingGap(
        string kind,
        string sourceLabel,
        string message,
        CodeFact fact) =>
        Gap(
            kind,
            sourceLabel,
            message,
            [
                Pair("configurationKind", SafeToken(fact.Properties.GetValueOrDefault("configurationKind"), "unknown")),
                Pair("mappingKind", SafeToken(fact.Properties.GetValueOrDefault("mappingKind"), "unknown"))
            ],
            [fact.FactId],
            commitSha: fact.CommitSha,
            filePath: fact.Evidence.FilePath,
            startLine: fact.Evidence.StartLine,
            endLine: fact.Evidence.EndLine,
            extractorId: fact.Evidence.ExtractorId,
            extractorVersion: fact.Evidence.ExtractorVersion,
            supportingRuleIds: [fact.RuleId]);

    private static DatabaseDesignEvidenceItem FromQuerySurface(
        CombinedDependencySurfaceRow surface,
        (string? Id, string? Version) extractor,
        string classification,
        string matchKind)
    {
        var metadata = SortMetadata(
        [
            Pair("operationName", SafeToken(surface.OperationName, "unknown")),
            Pair("tableName", SafeIdentifier(surface.TableName, "unavailable")),
            Pair("columnNames", SafeIdentifierList(surface.ColumnNames)),
            Pair("sourceKind", SafeToken(surface.SourceKind, "unknown")),
            Pair("matchKind", matchKind)
        ]);
        return new DatabaseDesignEvidenceItem(
            StableId("query", surface.SourceLabel, surface.CombinedFactId),
            "query-reference",
            SafeIdentifier(surface.TableName, "unlinked-query"),
            classification,
            metadata,
            new DatabaseDesignEvidenceRef(
                surface.RuleId,
                NormalizeTier(surface.EvidenceTier),
                SafeLabel(surface.SourceLabel),
                SafeCommit(surface.CommitSha),
                SafePath(surface.FilePath),
                surface.StartLine,
                surface.EndLine,
                SafeTokenOrNull(extractor.Id),
                SafeTokenOrNull(extractor.Version),
                "bounded-static-evidence",
                [surface.CombinedFactId],
                [],
                [surface.RuleId],
                Limitations));
    }

    private static DatabaseDesignRouteReference FromPath(
        CombinedPath path,
        CombinedPathNode entry,
        (string? Id, string? Version) extractor)
    {
        var rules = path.Edges.Select(edge => edge.RuleId)
            .Concat(path.Nodes.Select(node => node.RuleId).OfType<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var tiers = path.Edges.Select(edge => edge.EvidenceTier)
            .Concat(path.Nodes.Select(node => node.EvidenceTier).OfType<string>())
            .ToArray();
        var tier = WeakestTier(tiers);
        return new DatabaseDesignRouteReference(
            StableId("route", path.PathId),
            SafeToken(entry.NodeKind, "entry"),
            SafeToken(entry.HttpMethod, "ANY"),
            SafeRoute(entry.NormalizedPathKey),
            SafeToken(path.Classification, "UnknownAnalysisGap"),
            "static-name-match",
            new DatabaseDesignEvidenceRef(
                PacketRuleId,
                tier,
                SafeLabel(entry.SourceLabel),
                SafeCommit(entry.CommitSha),
                SafePath(entry.FilePath),
                entry.StartLine,
                entry.EndLine,
                SafeTokenOrNull(extractor.Id),
                SafeTokenOrNull(extractor.Version),
                "bounded-static-path",
                path.SupportingFactIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                path.SupportingEdgeIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                rules.Length == 0 ? [PacketRuleId] : rules,
                Limitations));
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SafeFactMetadata(CodeFact fact)
    {
        var keys = fact.FactType switch
        {
            FactTypes.PostgresSchemaTableDeclared => new[] { "objectKind", "operationKind", "schemaName", "tableName", "coverageLabel" },
            FactTypes.PostgresSchemaColumnDeclared => new[] { "objectKind", "operationKind", "schemaName", "tableName", "columnName", "coverageLabel" },
            FactTypes.PostgresSchemaConstraintDeclared => new[] { "objectKind", "operationKind", "schemaName", "tableName", "constraintName", "constraintKind", "columnNames", "referencedSchemaName", "referencedTableName", "referencedColumnNames", "coverageLabel" },
            FactTypes.PostgresSchemaIndexDeclared => new[] { "objectKind", "operationKind", "schemaName", "tableName", "indexName", "indexKind", "accessMethod", "columnNames", "coverageLabel" },
            FactTypes.PostgresSchemaEnumDeclared => new[] { "objectKind", "operationKind", "schemaName", "enumName", "enumLabelsOmitted", "coverageLabel" },
            FactTypes.PostgresSchemaRoutineDeclared => new[] { "objectKind", "operationKind", "schemaName", "routineName", "routineKind", "routineSignatureOmitted", "routineBodyOmitted", "coverageLabel" },
            FactTypes.PostgresSchemaSnapshotDeclared => new[] { "objectKind", "snapshotFormat", "recognizedDdlStatementCount", "unsupportedDdlStatementCount", "sourceDatabaseIdentityOmitted", "coverageLabel" },
            FactTypes.PostgresMigrationOperation => new[] { "objectKind", "operationKind", "schemaName", "tableName", "columnName", "newTableName", "newColumnName", "dropBehavior", "constraintName", "constraintKind", "indexName", "indexKind", "enumName", "routineName", "routineKind", "coverageLabel" },
            FactTypes.PostgresMigrationFileDeclared => new[] { "objectKind", "coverageLabel" },
            FactTypes.AnalysisGap => new[] { "classification", "coverageLabel", "unsupportedDdlStatementCount", "unsupportedDdlFamilies" },
            _ => Array.Empty<string>()
        };
        return SortMetadata(keys
            .Select(key => fact.Properties.TryGetValue(key, out var value) ? Pair(key, SafeMetadataValue(key, value)) : default)
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToArray());
    }

    private static string SafeMetadataValue(string key, string value) =>
        key.EndsWith("Name", StringComparison.Ordinal) || key.EndsWith("Names", StringComparison.Ordinal) || key.EndsWith("Families", StringComparison.Ordinal)
            ? SafeIdentifierList(value)
            : SafeToken(value, "unknown");

    private static bool TryFactTableKey(string sourceLabel, CodeFact fact, out TableKey key) =>
        TryTableKey(sourceLabel, fact.Properties.GetValueOrDefault("schemaName"), fact.Properties.GetValueOrDefault("tableName"), out key);

    private static bool TrySurfaceTableKey(CombinedDependencySurfaceRow surface, out TableKey key)
    {
        var tableIdentity = surface.TableName;
        if (string.IsNullOrWhiteSpace(tableIdentity))
        {
            key = default;
            return false;
        }
        var parts = tableIdentity.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            1 => TryTableKey(surface.SourceLabel, null, parts[0], out key),
            2 => TryTableKey(surface.SourceLabel, parts[0], parts[1], out key),
            _ => Fail(out key)
        };
    }

    private static bool TryTableKey(string sourceLabel, string? schema, string? table, out TableKey key)
    {
        var normalizedTable = NormalizeIdentifier(table);
        var normalizedSchema = string.IsNullOrWhiteSpace(schema) ? DefaultSchema : NormalizeIdentifier(schema);
        if (normalizedTable is null || normalizedSchema is null)
        {
            key = default;
            return false;
        }
        key = new TableKey(
            $"{SafeLabel(sourceLabel)}\0{normalizedSchema}\0{normalizedTable}",
            SafeLabel(sourceLabel),
            normalizedSchema,
            normalizedTable,
            normalizedSchema == DefaultSchema ? "schema-unresolved" : "explicit-schema");
        return true;
    }

    private static bool Fail(out TableKey key)
    {
        key = default;
        return false;
    }

    private static string? NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (!(char.IsLetter(trimmed[0]) || trimmed[0] == '_')
            || trimmed.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '$')))
            return null;
        return trimmed.ToLowerInvariant();
    }

    private static TableBuilder GetBuilder(Dictionary<string, TableBuilder> builders, TableKey key)
    {
        if (!builders.TryGetValue(key.StableKey, out var builder))
        {
            builder = new TableBuilder(key);
            builders.Add(key.StableKey, builder);
        }
        return builder;
    }

    private static async Task<DesignReadInput> ReadInputAsync(
        SqliteConnection connection,
        string indexPath,
        CancellationToken cancellationToken)
    {
        var hasCombinedSources = await TableExistsAsync(connection, "index_sources", cancellationToken);
        var hasCombinedFacts = await TableExistsAsync(connection, "combined_facts", cancellationToken);
        if (hasCombinedSources && hasCombinedFacts)
        {
            await CombinedDependencyReporter.ValidateCombinedIndexAsync(connection, cancellationToken);
            var combined = await CombinedDependencyReporter.ReadAsync(connection, cancellationToken);
            var combinedSqlInputs = await ReleaseReviewReporter.ReadSqlEvidenceInputsAsync(
                indexPath,
                "combined",
                cancellationToken,
                includeModelMappings: true,
                includeQuerySurfaces: true);
            return new DesignReadInput(
                "combined",
                combined.Sources,
                combined.KnownGaps,
                combined.CoverageWarnings,
                combined.Facts,
                combinedSqlInputs);
        }

        var hasManifest = await TableExistsAsync(connection, "scan_manifest", cancellationToken);
        var hasFacts = await TableExistsAsync(connection, "facts", cancellationToken);
        if (!hasManifest || !hasFacts)
        {
            throw new InvalidDataException(
                "database-design-review input is not a valid TraceMap index; expected scan_manifest/facts or index_sources/combined_facts.");
        }

        await using var manifestCommand = connection.CreateCommand();
        manifestCommand.CommandText = "select manifest_json from scan_manifest order by scan_id limit 2;";
        var manifestRows = new List<string>();
        await using (var reader = await manifestCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                manifestRows.Add(reader.GetString(0));
        }
        if (manifestRows.Count != 1)
            throw new InvalidDataException("TraceMap single index must contain exactly one scan_manifest row.");

        var manifest = JsonSerializer.Deserialize<ScanManifest>(
            manifestRows[0],
            CombinedDependencyReporter.JsonOptions)
            ?? throw new InvalidDataException("TraceMap single index contains an invalid scan manifest.");
        var source = new CombinedReportSource(
            "single",
            "single",
            CombinedReportHelpers.Hash("single-index", 16),
            manifest.ScanId,
            manifest.RepoName,
            manifest.RemoteUrl,
            manifest.Branch,
            manifest.CommitSha,
            manifest.ScannerVersion,
            InferLanguage(manifest.ScannerVersion),
            null,
            false,
            manifest.ScanRootRelativePath,
            manifest.ScanRootPathHash,
            manifest.GitRootHash,
            manifest.AnalysisLevel,
            manifest.BuildStatus);
        var warnings = SingleSourceCoverageWarnings(source);
        var knownGaps = manifest.KnownGaps
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => SafeToken(value, "known-gap"), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CombinedKnownGapRow(
                source.SourceIndexId,
                source.Label,
                group.Key,
                group.Count(),
                group.Key))
            .ToArray();
        var singleSqlInputs = await ReleaseReviewReporter.ReadSqlEvidenceInputsAsync(
            indexPath,
            "single",
            cancellationToken,
            includeModelMappings: true,
            includeQuerySurfaces: true);
        var facts = singleSqlInputs
            .SelectMany(row => row.Result.Facts)
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(fact => new CombinedFactRow(
                fact.FactId,
                source.SourceIndexId,
                source.Label,
                fact.FactId,
                fact.ScanId,
                fact.Repo,
                fact.CommitSha,
                fact.FactType,
                fact.RuleId,
                fact.EvidenceTier,
                fact.SourceSymbol,
                fact.TargetSymbol,
                fact.ContractElement,
                fact.Evidence.FilePath,
                fact.Evidence.StartLine,
                fact.Evidence.EndLine,
                fact.Properties))
            .ToArray();
        return new DesignReadInput("single", [source], knownGaps, warnings, facts, singleSqlInputs);
    }

    private static IReadOnlyList<string> SingleSourceCoverageWarnings(CombinedReportSource source)
    {
        var warnings = new List<string>();
        if (!source.AnalysisLevel.Equals("Level1SemanticAnalysis", StringComparison.Ordinal)
            || !source.BuildStatus.Equals("Succeeded", StringComparison.Ordinal))
        {
            warnings.Add($"{source.Label} reports reduced analysis or build coverage.");
        }
        if (!CombinedReportHelpers.SourceIdentityVerified(source))
            warnings.Add($"{source.Label} source identity is not fully verified.");
        if (string.IsNullOrWhiteSpace(source.Language))
            warnings.Add($"{source.Label} language is unknown.");
        return warnings;
    }

    private static string? InferLanguage(string? scannerVersion)
    {
        if (string.IsNullOrWhiteSpace(scannerVersion))
            return null;
        if (scannerVersion.Contains("typescript", StringComparison.OrdinalIgnoreCase))
            return "typescript";
        if (scannerVersion.Contains("python", StringComparison.OrdinalIgnoreCase))
            return "python";
        if (scannerVersion.Contains("jvm", StringComparison.OrdinalIgnoreCase))
            return "jvm";
        if (scannerVersion.Contains("swift", StringComparison.OrdinalIgnoreCase))
            return "swift";
        return scannerVersion.Contains("tracemap", StringComparison.OrdinalIgnoreCase) ? "csharp" : null;
    }

    private static async Task<IReadOnlyDictionary<string, (string? Id, string? Version)>> ReadExtractorVersionsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var hasId = await ColumnExistsAsync(connection, "combined_facts", "extractor_id", cancellationToken);
        var hasVersion = await ColumnExistsAsync(connection, "combined_facts", "extractor_version", cancellationToken);
        if (!hasId && !hasVersion)
            return new Dictionary<string, (string?, string?)>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select combined_fact_id, {(hasId ? "extractor_id" : "null")}, {(hasVersion ? "extractor_version" : "null")}
            from combined_facts
            order by combined_fact_id;
            """;
        var result = new Dictionary<string, (string?, string?)>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = (
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2));
        }
        return result;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select 1
            from sqlite_master
            where type = 'table' and name = $table collate nocase
            limit 1;
            """;
        command.Parameters.AddWithValue("$table", table);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select 1
            from pragma_table_info($table)
            where name = $column collate nocase
            limit 1;
            """;
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static (DatabaseDesignTableGroup[] Tables, DatabaseDesignEvidenceItem[] Globals, List<DatabaseDesignEvidenceItem> Unlinked) ApplyEvidenceCap(
        DatabaseDesignTableGroup[] tables,
        DatabaseDesignEvidenceItem[] globals,
        List<DatabaseDesignEvidenceItem> unlinked,
        int cap)
    {
        var remaining = cap;
        var cappedTables = new List<DatabaseDesignTableGroup>();
        foreach (var table in tables)
        {
            var declarations = table.Declarations.Take(remaining).ToArray();
            remaining -= declarations.Length;
            var operations = table.Operations.Take(remaining).ToArray();
            remaining -= operations.Length;
            var queries = table.QueryReferences.Take(remaining).ToArray();
            remaining -= queries.Length;
            cappedTables.Add(table with { Declarations = declarations, Operations = operations, QueryReferences = queries });
        }
        var cappedGlobals = globals.Take(remaining).ToArray();
        remaining -= cappedGlobals.Length;
        var cappedUnlinked = unlinked.Take(remaining).ToList();
        return (cappedTables.ToArray(), cappedGlobals, cappedUnlinked);
    }

    private static int TableEvidenceCount(DatabaseDesignTableGroup table) =>
        table.Declarations.Count + table.Operations.Count + table.QueryReferences.Count;

    private static DatabaseDesignGap TruncationGap(string kind, int omitted) =>
        Gap(
            "TruncatedByLimit",
            null,
            $"Database design review omitted {omitted.ToString(CultureInfo.InvariantCulture)} `{kind}` rows because the configured cap was reached.",
            [Pair("omittedKind", kind), Pair("omittedCount", omitted.ToString(CultureInfo.InvariantCulture))]);

    private static DatabaseDesignGap Gap(
        string kind,
        string? sourceLabel,
        string message,
        IReadOnlyList<KeyValuePair<string, string>>? metadata = null,
        IReadOnlyList<string>? supportingFactIds = null,
        IReadOnlyList<string>? supportingEdgeIds = null,
        string? commitSha = null,
        string? filePath = null,
        int? startLine = null,
        int? endLine = null,
        string? extractorId = null,
        string? extractorVersion = null,
        IReadOnlyList<string>? supportingRuleIds = null) =>
        new(
            StableId("gap", kind, sourceLabel ?? string.Empty, message, string.Join(';', supportingFactIds ?? [])),
            kind,
            kind == "CompatiblePostgresEvidenceUnavailable" ? "UnknownAnalysisGap" : "PartialAnalysis",
            message,
            sourceLabel is null ? null : SafeLabel(sourceLabel),
            GapRuleId,
            EvidenceTiers.Tier4Unknown,
            "reduced",
            commitSha is null ? null : SafeCommit(commitSha),
            SafePath(filePath),
            startLine,
            endLine,
            SafeTokenOrNull(extractorId),
            SafeTokenOrNull(extractorVersion),
            (supportingFactIds ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            (supportingEdgeIds ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            (supportingRuleIds ?? [GapRuleId]).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            SortMetadata(metadata ?? []),
            Limitations);

    private static string DeclarationKind(string factType) => factType switch
    {
        FactTypes.PostgresSchemaTableDeclared => "table",
        FactTypes.PostgresSchemaColumnDeclared => "column",
        FactTypes.PostgresSchemaConstraintDeclared => "constraint",
        FactTypes.PostgresSchemaIndexDeclared => "index",
        _ => "schema-object"
    };

    private static string GlobalKind(string factType) => factType switch
    {
        FactTypes.PostgresSchemaSnapshotDeclared => "snapshot",
        FactTypes.PostgresSchemaEnumDeclared => "enum",
        FactTypes.PostgresSchemaRoutineDeclared => "routine",
        FactTypes.PostgresMigrationFileDeclared => "migration-file",
        _ => "database-object"
    };

    private static string RenderMarkdown(DatabaseDesignReviewDocument report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Database Design Review");
        builder.AppendLine();
        builder.AppendLine($"- Version: `{report.Version}`");
        builder.AppendLine($"- Rule: `{report.RuleId}`");
        builder.AppendLine($"- Claim level: `{report.ClaimLevel}`");
        builder.AppendLine($"- Coverage: `{report.Coverage}`");
        builder.AppendLine($"- Sources: {report.Summary.SourceCount}");
        builder.AppendLine($"- Tables: {report.Summary.TableCount}");
        builder.AppendLine($"- Gaps: {report.Summary.GapCount}");
        builder.AppendLine();
        builder.AppendLine("## Sources");
        builder.AppendLine();
        builder.AppendLine("| Source | Commit | Analysis | Build | Identity |");
        builder.AppendLine("|---|---|---|---|---|");
        foreach (var source in report.Sources)
            builder.AppendLine($"| {Md(source.SourceLabel)} | `{Md(source.CommitSha)}` | `{Md(source.AnalysisLevel)}` | `{Md(source.BuildStatus)}` | `{(source.IdentityVerified ? "verified" : "unverified")}` |");

        builder.AppendLine();
        builder.AppendLine("## Table design");
        foreach (var table in report.Tables)
        {
            builder.AppendLine();
            builder.AppendLine($"### `{Md(table.SchemaName)}.{Md(table.TableName)}`");
            builder.AppendLine();
            builder.AppendLine($"Source: `{Md(table.SourceLabel)}` · schema: `{Md(table.SchemaResolution)}` · coverage: `{Md(table.Coverage)}`");
            AppendItems(builder, "Declarations", table.Declarations);
            AppendItems(builder, "Database operations", table.Operations);
            AppendItems(builder, "Query references", table.QueryReferences);
            if (table.RouteReferences.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("#### Route references");
                builder.AppendLine();
                builder.AppendLine("| Entry | Classification | Rule | Tier | Support |");
                builder.AppendLine("|---|---|---|---|---|");
                foreach (var route in table.RouteReferences)
                    builder.AppendLine($"| `{Md(route.Method)} {Md(route.NormalizedPathKey)}` | `{Md(route.PathClassification)}` | `{Md(route.Evidence.RuleId)}` | `{Md(route.Evidence.EvidenceTier)}` | facts {route.Evidence.SupportingFactIds.Count}, edges {route.Evidence.SupportingEdgeIds.Count} |");
            }
        }

        AppendItems(builder, "Global database evidence", report.GlobalObjects, headingLevel: 2);
        AppendItems(builder, "Unlinked query evidence", report.UnlinkedQueries, headingLevel: 2);
        builder.AppendLine();
        builder.AppendLine("## Gaps");
        builder.AppendLine();
        if (report.Gaps.Count == 0)
        {
            builder.AppendLine("None recorded.");
        }
        else
        {
            builder.AppendLine("| Kind | Source | Rule | Tier | Message |");
            builder.AppendLine("|---|---|---|---|---|");
            foreach (var gap in report.Gaps)
                builder.AppendLine($"| `{Md(gap.GapKind)}` | `{Md(gap.SourceLabel ?? "all")}` | `{Md(gap.RuleId)}` | `{Md(gap.EvidenceTier)}` | {Md(gap.Message)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations)
            builder.AppendLine($"- {Md(limitation)}");
        return builder.ToString();
    }

    private static void AppendItems(StringBuilder builder, string title, IReadOnlyList<DatabaseDesignEvidenceItem> items, int headingLevel = 4)
    {
        builder.AppendLine();
        builder.AppendLine($"{new string('#', headingLevel)} {title}");
        builder.AppendLine();
        if (items.Count == 0)
        {
            builder.AppendLine("No bounded evidence.");
            return;
        }
        builder.AppendLine("| Kind | Name | Rule | Tier | Source span | Coverage |");
        builder.AppendLine("|---|---|---|---|---|---|");
        foreach (var item in items)
        {
            var span = item.Evidence.FilePath is null ? "n/a" : $"{item.Evidence.FilePath}:{item.Evidence.StartLine}-{item.Evidence.EndLine}";
            builder.AppendLine($"| `{Md(item.EvidenceKind)}` | `{Md(item.DisplayName)}` | `{Md(item.Evidence.RuleId)}` | `{Md(item.Evidence.EvidenceTier)}` | `{Md(span)}` | `{Md(item.Evidence.CoverageLabel)}` |");
        }
    }

    private static string? SafePath(string? path)
    {
        var safe = CombinedReportHelpers.SafePath(path);
        return safe == "n/a" ? null : safe;
    }

    private static string SafeLabel(string? value) => SafeToken(value, "unknown-source");
    private static string SafeCommit(string? value) => KnownCommit(value) ? value!.Trim() : "unknown";
    private static string SafeReason(string? value) => SafeToken(value, "unavailable");
    private static string SafeRoute(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unavailable" : value.Replace("|", "%7C", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);

    private static string SafeIdentifier(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length is 1 or 2 && parts.All(part => NormalizeIdentifier(part) is not null)
            ? string.Join('.', parts.Select(part => NormalizeIdentifier(part)!))
            : fallback;
    }

    private static string SafeTypeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unavailable";
        var trimmed = value.Trim().Replace("global::", string.Empty, StringComparison.Ordinal);
        return trimmed.Length <= 160
            && trimmed.Any(char.IsLetter)
            && trimmed.All(character =>
                char.IsLetterOrDigit(character)
                || char.IsWhiteSpace(character)
                || character is '_' or '.' or '<' or '>' or ',' or '+' or '?' or '[' or ']')
            ? trimmed
            : "unavailable";
    }

    private static string SafeIdentifierList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "none";
        var parts = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => SafeIdentifier(part, string.Empty))
            .Where(part => part.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(part => part, StringComparer.Ordinal)
            .Take(64)
            .ToArray();
        return parts.Length == 0 ? "unavailable" : string.Join(',', parts);
    }

    private static string SafeToken(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var trimmed = value.Trim();
        if (trimmed.Length > 160 || trimmed.Any(character => char.IsControl(character) || character is '|' or '`'))
            return fallback;
        return trimmed;
    }

    private static string? SafeTokenOrNull(string? value)
    {
        var safe = SafeToken(value, string.Empty);
        return safe.Length == 0 || safe.Equals("unknown", StringComparison.OrdinalIgnoreCase) ? null : safe;
    }

    private static IReadOnlyList<string> SplitLimitations(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(SafeReason)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();

    private static string WeakestTier(IEnumerable<string> tiers)
    {
        var rank = tiers.Select(tier => tier switch
        {
            EvidenceTiers.Tier4Unknown => 4,
            EvidenceTiers.Tier3SyntaxOrTextual => 3,
            EvidenceTiers.Tier2Structural => 2,
            EvidenceTiers.Tier1Semantic => 1,
            _ => 4
        }).DefaultIfEmpty(4).Max();
        return rank switch
        {
            1 => EvidenceTiers.Tier1Semantic,
            2 => EvidenceTiers.Tier2Structural,
            3 => EvidenceTiers.Tier3SyntaxOrTextual,
            _ => EvidenceTiers.Tier4Unknown
        };
    }

    private static string NormalizeTier(string? tier) => tier switch
    {
        EvidenceTiers.Tier1Semantic => EvidenceTiers.Tier1Semantic,
        EvidenceTiers.Tier2Structural => EvidenceTiers.Tier2Structural,
        EvidenceTiers.Tier3SyntaxOrTextual => EvidenceTiers.Tier3SyntaxOrTextual,
        EvidenceTiers.Tier4Unknown => EvidenceTiers.Tier4Unknown,
        _ => EvidenceTiers.Tier4Unknown
    };

    private static string StableId(params string[] parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', parts)));
        return $"{parts[0]}:{Convert.ToHexString(bytes).ToLowerInvariant()[..24]}";
    }

    private static KeyValuePair<string, string> Pair(string key, string value) => new(key, value);
    private static IReadOnlyList<KeyValuePair<string, string>> SortMetadata(IEnumerable<KeyValuePair<string, string>> values) =>
        values.Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .ToArray();
    private static string Md(string value) => CombinedReportHelpers.Cell(value);

    private sealed record DesignReadInput(
        string IndexKind,
        IReadOnlyList<CombinedReportSource> Sources,
        IReadOnlyList<CombinedKnownGapRow> KnownGaps,
        IReadOnlyList<string> CoverageWarnings,
        IReadOnlyList<CombinedFactRow> Facts,
        IReadOnlyList<SqlEvidenceInput> SqlInputs);

    private readonly record struct TableKey(string StableKey, string SourceLabel, string SchemaName, string TableName, string SchemaResolution);

    private sealed class TableBuilder
    {
        private readonly TableKey key;

        public TableBuilder(TableKey key)
        {
            this.key = key;
        }

        public TableKey Key => key;
        public List<DatabaseDesignEvidenceItem> Declarations { get; } = [];
        public List<DatabaseDesignEvidenceItem> Operations { get; } = [];
        public List<DatabaseDesignEvidenceItem> QueryReferences { get; } = [];
        public List<DatabaseDesignRouteReference> RouteReferences { get; } = [];

        public DatabaseDesignTableGroup Build()
        {
            var declarations = Declarations.OrderBy(row => row.EvidenceKind, StringComparer.Ordinal).ThenBy(row => row.DisplayName, StringComparer.Ordinal).ThenBy(row => row.ItemId, StringComparer.Ordinal).ToArray();
            var operations = Operations.OrderBy(row => row.DisplayName, StringComparer.Ordinal).ThenBy(row => row.ItemId, StringComparer.Ordinal).ToArray();
            var queries = QueryReferences.OrderBy(row => row.ItemId, StringComparer.Ordinal).ToArray();
            var routes = RouteReferences.GroupBy(row => row.RouteReferenceId, StringComparer.Ordinal).Select(group => group.First()).OrderBy(row => row.Method, StringComparer.Ordinal).ThenBy(row => row.NormalizedPathKey, StringComparer.Ordinal).ThenBy(row => row.RouteReferenceId, StringComparer.Ordinal).ToArray();
            var coverage = declarations.Length + operations.Length > 0 ? "bounded-static-evidence" : "reduced";
            return new DatabaseDesignTableGroup(
                StableId("table", key.StableKey),
                key.SourceLabel,
                key.SchemaName,
                key.TableName,
                key.SchemaResolution,
                coverage,
                declarations,
                operations,
                queries,
                routes,
                Limitations);
        }
    }
}
