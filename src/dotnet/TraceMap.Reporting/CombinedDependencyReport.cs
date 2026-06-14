using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record CombinedDependencyReportOptions(
    string IndexPath,
    string OutputPath,
    string Format = "markdown");

public sealed record CombinedDependencyReportResult(
    CombinedDependencyReportDocument Report,
    string? MarkdownPath,
    string? JsonPath);

public sealed record CombinedDependencyReportDocument(
    string Version,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    IReadOnlyList<CombinedReportSource> Sources,
    CombinedReportSummary Summary,
    IReadOnlyList<CombinedEndpointFinding> EndpointFindings,
    IReadOnlyList<CombinedDependencySurfaceRow> DependencySurfaces,
    IReadOnlyList<CombinedDependencyEdgeRow> DependencyEdges,
    IReadOnlyList<CombinedNeedsReviewRow> NeedsReview,
    IReadOnlyList<CombinedKnownGapRow> KnownGaps,
    IReadOnlyList<string> Limitations);

public sealed record CombinedReportSummary(
    int SourceCount,
    int FactCount,
    int DependencyEdgeCount,
    int EndpointFindingCount,
    IReadOnlyDictionary<string, int> EndpointFindingsByClassification,
    IReadOnlyDictionary<string, int> SurfacesByKind,
    IReadOnlyDictionary<string, int> EdgesByKind);

public sealed record CombinedReportSource(
    string SourceIndexId,
    string Label,
    string IndexPathHash,
    string ScanId,
    string RepoName,
    string? RemoteUrl,
    string? Branch,
    string CommitSha,
    string ScannerVersion,
    string? Language,
    string? StoredLanguage,
    bool LanguageCorrected,
    string? ScanRootRelativePath,
    string? ScanRootPathHash,
    string? GitRootHash,
    string AnalysisLevel,
    string BuildStatus);

public sealed record CombinedEndpointFinding(
    string Classification,
    string HttpMethod,
    string? NormalizedPathKey,
    string? ClientSourceIndexId,
    string? ClientSourceLabel,
    string? ClientScanId,
    string? ClientCommitSha,
    string? ClientCombinedFactId,
    string? ClientOriginalFactId,
    string? ClientRuleId,
    string? ClientEvidenceTier,
    string? ClientFilePath,
    int? ClientStartLine,
    int? ClientEndLine,
    string? ServerSourceIndexId,
    string? ServerSourceLabel,
    string? ServerScanId,
    string? ServerCommitSha,
    string? ServerCombinedFactId,
    string? ServerOriginalFactId,
    string? ServerRuleId,
    string? ServerEvidenceTier,
    string? ServerFilePath,
    int? ServerStartLine,
    int? ServerEndLine,
    string StaticMatchQuality,
    bool SameSource,
    IReadOnlyList<string> Notes);

public sealed record CombinedDependencySurfaceRow(
    string SurfaceKind,
    string DisplayName,
    string SourceIndexId,
    string SourceLabel,
    string ScanId,
    string CommitSha,
    string CombinedFactId,
    string OriginalFactId,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine,
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
    string? Version,
    string? ConfigKey);

public sealed record CombinedDependencyEdgeRow(
    string EdgeKind,
    string SourceIndexId,
    string SourceLabel,
    string EdgeId,
    string OriginalFactId,
    string? SourceSymbol,
    string? TargetSymbol,
    string? TargetAssemblyName,
    string? TargetAssemblyVersion,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine);

public sealed record CombinedNeedsReviewRow(
    string ReviewKind,
    string Message,
    string? SourceIndexId,
    string? SourceLabel,
    string? CombinedFactId,
    string? FilePath,
    int? StartLine);

public sealed record CombinedKnownGapRow(
    string SourceIndexId,
    string SourceLabel,
    string Category,
    int Count,
    string Example);

internal sealed record CombinedFactRow(
    string CombinedFactId,
    string SourceIndexId,
    string SourceLabel,
    string OriginalFactId,
    string ScanId,
    string Repo,
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

internal sealed record EndpointCandidate(
    CombinedFactRow Fact,
    string Method,
    string? NormalizedPathTemplate,
    string? NormalizedPathKey,
    IReadOnlyList<string> ExpandedPathKeys,
    bool IsDynamic,
    string? DynamicReason,
    bool IsClient,
    bool IsServer);

internal sealed record CombinedReadResult(
    IReadOnlyList<CombinedReportSource> Sources,
    IReadOnlyList<CombinedKnownGapRow> KnownGaps,
    IReadOnlyList<string> CoverageWarnings,
    IReadOnlyList<CombinedFactRow> Facts,
    IReadOnlyList<CombinedDependencyEdgeRow> Edges);

internal sealed record CombinedSourceReadRow(
    CombinedReportSource Source,
    string ManifestJson);

public static class CombinedEndpointClassifications
{
    public const string MatchedEndpoint = nameof(MatchedEndpoint);
    public const string OptionalSegmentMatch = nameof(OptionalSegmentMatch);
    public const string MethodMismatch = nameof(MethodMismatch);
    public const string ClientCallNoServerEndpoint = nameof(ClientCallNoServerEndpoint);
    public const string ServerEndpointNoClientMatch = nameof(ServerEndpointNoClientMatch);
    public const string AmbiguousMatch = nameof(AmbiguousMatch);
    public const string DynamicClientUrlNeedsReview = nameof(DynamicClientUrlNeedsReview);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
}

public static class CombinedDependencyReporter
{
    private const string Version = "1.0";
    private const int MarkdownRowLimit = 200;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    internal static readonly IReadOnlyList<string> Limitations =
    [
        "Endpoint alignment is static method/path evidence. It does not prove runtime traffic, runtime reachability, auth behavior, proxy behavior, deployment base paths, CORS behavior, or user exercise.",
        "SQL/query rows are static shape or hash evidence. They do not prove runtime execution, database schema existence, dialect validity, generated SQL equivalence, or branch feasibility.",
        "Call and creation edges are static code evidence. They do not prove dynamic dispatch targets, runtime DI registrations, reflection targets, branch feasibility, or collection contents.",
        "Parameter-forwarding rows are direct static argument-to-parameter evidence, not full taint analysis.",
        "Reduced coverage means absence of evidence is not evidence of absence."
    ];

    public static async Task<CombinedDependencyReportResult> WriteAsync(CombinedDependencyReportOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("Combined dependency report requires --index <path>.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("Combined dependency report requires --out <path>.", nameof(options));
        }

        var format = NormalizeFormat(options.Format);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.IndexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ValidateCombinedIndexAsync(connection, cancellationToken);

        var read = await ReadAsync(connection, cancellationToken);
        var endpointFindings = MatchEndpoints(read.Sources, read.Facts);
        var surfaces = BuildSurfaces(read.Facts);
        var needsReview = BuildNeedsReview(endpointFindings, read.Facts);
        var warnings = read.CoverageWarnings
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var report = new CombinedDependencyReportDocument(
            Version,
            warnings.Length == 0 ? "FullEvidenceAvailable" : "ReducedCoverage",
            warnings,
            read.Sources.OrderBy(source => source.Label, StringComparer.Ordinal).ThenBy(source => source.SourceIndexId, StringComparer.Ordinal).ToArray(),
            new CombinedReportSummary(
                read.Sources.Count,
                read.Facts.Count,
                read.Edges.Count,
                endpointFindings.Count,
                CountBy(endpointFindings, finding => finding.Classification),
                CountBy(surfaces, surface => surface.SurfaceKind),
                CountBy(read.Edges, edge => edge.EdgeKind)),
            endpointFindings,
            surfaces,
            read.Edges
                .Select(SanitizeEdge)
                .OrderBy(edge => edge.EdgeKind, StringComparer.Ordinal)
                .ThenBy(edge => edge.SourceLabel, StringComparer.Ordinal)
                .ThenBy(edge => edge.SourceSymbol, StringComparer.Ordinal)
                .ThenBy(edge => edge.TargetSymbol, StringComparer.Ordinal)
                .ThenBy(edge => edge.FilePath, StringComparer.Ordinal)
                .ThenBy(edge => edge.StartLine)
                .ToArray(),
            needsReview,
            read.KnownGaps,
            Limitations);

        var (markdownPath, jsonPath) = await WriteOutputsAsync(options.OutputPath, format, report, cancellationToken);
        return new CombinedDependencyReportResult(report, markdownPath, jsonPath);
    }

    internal static async Task ValidateCombinedIndexAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "index_sources", cancellationToken)
            || !await TableExistsAsync(connection, "combined_facts", cancellationToken)
            || !await ViewExistsAsync(connection, "combined_dependency_edges", cancellationToken))
        {
            throw new InvalidDataException("tracemap report currently requires a combined index produced by tracemap combine.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from index_sources;";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        if (count == 0)
        {
            throw new InvalidDataException("TraceMap combined index does not contain any index_sources rows.");
        }
    }

    internal static async Task<CombinedReadResult> ReadAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sourceRows = await ReadSourcesAsync(connection, cancellationToken);
        var sources = sourceRows.Select(row => row.Source).ToArray();
        var knownGaps = new List<CombinedKnownGapRow>();
        var warnings = new List<string>();
        foreach (var row in sourceRows)
        {
            AddCoverageWarnings(row.Source, warnings);
            knownGaps.AddRange(ReadKnownGaps(row.Source, row.ManifestJson, warnings));
        }

        var facts = await ReadFactsAsync(connection, cancellationToken);
        var edges = await ReadEdgesAsync(connection, cancellationToken);
        return new CombinedReadResult(sources, knownGaps, warnings.Distinct(StringComparer.Ordinal).ToArray(), facts, edges);
    }

    private static async Task<IReadOnlyList<CombinedSourceReadRow>> ReadSourcesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select source_index_id,
                   label,
                   index_path_hash,
                   scan_id,
                   repo_name,
                   remote_url,
                   branch,
                   commit_sha,
                   scanner_version,
                   language,
                   scan_root_relative_path,
                   scan_root_path_hash,
                   git_root_hash,
                   analysis_level,
                   build_status,
                   manifest_json
            from index_sources
            order by label, source_index_id;
            """;
        var rows = new List<CombinedSourceReadRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var storedLanguage = reader.IsDBNull(9) ? null : reader.GetString(9);
            var correctedLanguage = CorrectLanguage(reader.GetString(8), storedLanguage);
            rows.Add(new CombinedSourceReadRow(
                new CombinedReportSource(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    correctedLanguage,
                    storedLanguage,
                    !string.Equals(storedLanguage, correctedLanguage, StringComparison.Ordinal),
                    reader.IsDBNull(10) ? null : reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    reader.GetString(13),
                    reader.GetString(14)),
                reader.GetString(15)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CombinedFactRow>> ReadFactsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select facts.combined_fact_id,
                   facts.source_index_id,
                   sources.label,
                   facts.original_fact_id,
                   facts.scan_id,
                   facts.repo,
                   facts.commit_sha,
                   facts.fact_type,
                   facts.rule_id,
                   facts.evidence_tier,
                   facts.source_symbol,
                   facts.target_symbol,
                   facts.contract_element,
                   facts.file_path,
                   facts.start_line,
                   facts.end_line,
                   facts.properties_json
            from combined_facts facts
            join index_sources sources on sources.source_index_id = facts.source_index_id
            order by facts.file_path, facts.start_line, facts.fact_type, facts.combined_fact_id;
            """;
        var rows = new List<CombinedFactRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CombinedFactRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.GetString(13),
                reader.GetInt32(14),
                reader.GetInt32(15),
                ParseProperties(reader.GetString(16))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CombinedDependencyEdgeRow>> ReadEdgesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select source_index_id,
                   source_label,
                   edge_kind,
                   edge_id,
                   original_fact_id,
                   source_symbol,
                   target_symbol,
                   target_assembly_name,
                   target_assembly_version,
                   rule_id,
                   evidence_tier,
                   file_path,
                   start_line,
                   end_line
            from combined_dependency_edges
            order by edge_kind, source_label, coalesce(source_symbol, ''), coalesce(target_symbol, ''), file_path, start_line;
            """;
        var rows = new List<CombinedDependencyEdgeRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CombinedDependencyEdgeRow(
                reader.GetString(2),
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetString(11),
                reader.GetInt32(12),
                reader.GetInt32(13)));
        }

        return rows;
    }

    internal static IReadOnlyList<CombinedEndpointFinding> MatchEndpoints(IReadOnlyList<CombinedReportSource> sources, IReadOnlyList<CombinedFactRow> facts)
    {
        var sourceById = sources.ToDictionary(source => source.SourceIndexId, StringComparer.Ordinal);
        var candidates = facts
            .Where(fact => fact.FactType is FactTypes.HttpCallDetected or FactTypes.HttpRouteBinding)
            .Select(ToEndpointCandidate)
            .Where(candidate => candidate.IsClient || candidate.IsServer)
            .ToArray();
        var clients = candidates.Where(candidate => candidate.IsClient).ToArray();
        var servers = candidates.Where(candidate => candidate.IsServer).ToArray();
        var findings = new List<CombinedEndpointFinding>();
        var comparedClientIds = new HashSet<string>(StringComparer.Ordinal);
        var comparedServerIds = new HashSet<string>(StringComparer.Ordinal);
        var serverSources = servers.Select(server => server.Fact.SourceIndexId).Distinct(StringComparer.Ordinal).ToArray();

        foreach (var client in clients)
        {
            if (client.IsDynamic || string.IsNullOrWhiteSpace(client.NormalizedPathKey))
            {
                findings.Add(CreateEndpointFinding(
                    CombinedEndpointClassifications.DynamicClientUrlNeedsReview,
                    client,
                    null,
                    "Low",
                    false,
                    [SanitizedDynamicReason(client)]));
                continue;
            }

            foreach (var serverSourceId in serverSources)
            {
                var sourceServers = servers
                    .Where(server => server.Fact.SourceIndexId == serverSourceId && server.Fact.CombinedFactId != client.Fact.CombinedFactId)
                    .ToArray();
                var pathMatches = sourceServers
                    .Where(server => server.ExpandedPathKeys.Contains(client.NormalizedPathKey, StringComparer.Ordinal))
                    .ToArray();
                var methodMatches = pathMatches
                    .Where(server => MethodsCompatible(client.Method, server.Method))
                    .ToArray();

                if (methodMatches.Length == 1)
                {
                    var server = methodMatches[0];
                    comparedClientIds.Add(client.Fact.CombinedFactId);
                    comparedServerIds.Add(server.Fact.CombinedFactId);
                    var optional = server.NormalizedPathKey != client.NormalizedPathKey;
                    findings.Add(CreateEndpointFinding(
                        optional ? CombinedEndpointClassifications.OptionalSegmentMatch : CombinedEndpointClassifications.MatchedEndpoint,
                        client,
                        server,
                        optional ? "Medium" : "High",
                        client.Fact.SourceIndexId == server.Fact.SourceIndexId,
                        optional ? ["Matched through a server optional route segment."] : []));
                    continue;
                }

                if (methodMatches.Length > 1)
                {
                    foreach (var server in methodMatches)
                    {
                        comparedServerIds.Add(server.Fact.CombinedFactId);
                    }
                    comparedClientIds.Add(client.Fact.CombinedFactId);
                    findings.Add(CreateEndpointFinding(
                        CombinedEndpointClassifications.AmbiguousMatch,
                        client,
                        methodMatches.OrderBy(server => server.Fact.FilePath, StringComparer.Ordinal).ThenBy(server => server.Fact.StartLine).First(),
                        "Medium",
                        client.Fact.SourceIndexId == serverSourceId,
                        [$"More than one server endpoint matched {client.Method} {client.NormalizedPathKey}."]));
                    continue;
                }

                if (pathMatches.Length > 0)
                {
                    foreach (var server in pathMatches)
                    {
                        comparedServerIds.Add(server.Fact.CombinedFactId);
                    }
                    comparedClientIds.Add(client.Fact.CombinedFactId);
                    var first = pathMatches.OrderBy(server => server.Method, StringComparer.Ordinal).ThenBy(server => server.Fact.FilePath, StringComparer.Ordinal).First();
                    findings.Add(CreateEndpointFinding(
                        CombinedEndpointClassifications.MethodMismatch,
                        client,
                        first,
                        "Medium",
                        client.Fact.SourceIndexId == serverSourceId,
                        [$"Client method {client.Method} did not match server method(s): {string.Join(", ", pathMatches.Select(server => server.Method).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))}."]));
                }
            }
        }

        foreach (var client in clients.Where(client => !client.IsDynamic && !string.IsNullOrWhiteSpace(client.NormalizedPathKey) && !comparedClientIds.Contains(client.Fact.CombinedFactId)))
        {
            findings.Add(CreateEndpointFinding(
                CombinedEndpointClassifications.ClientCallNoServerEndpoint,
                client,
                null,
                "Medium",
                false,
                ["Coverage-relative finding only: this is not proof of a broken client call."]));
        }

        foreach (var server in servers.Where(server => !comparedServerIds.Contains(server.Fact.CombinedFactId)))
        {
            findings.Add(CreateEndpointFinding(
                CombinedEndpointClassifications.ServerEndpointNoClientMatch,
                null,
                server,
                "Medium",
                false,
                ["Coverage-relative finding only: this is not proof of dead code or an unused endpoint."]));
        }

        foreach (var source in sources.Where(source => SourceHasCredibilityGap(source)))
        {
            var gap = facts
                .Where(fact => fact.SourceIndexId == source.SourceIndexId && fact.FactType == FactTypes.AnalysisGap)
                .OrderBy(fact => fact.FilePath, StringComparer.Ordinal)
                .ThenBy(fact => fact.StartLine)
                .FirstOrDefault();
            if (gap is not null)
            {
                findings.Add(CreateEndpointFinding(
                    CombinedEndpointClassifications.UnknownAnalysisGap,
                    ToEndpointCandidate(gap),
                    null,
                    "Low",
                    false,
                    [$"{source.Label} has analysis gaps; endpoint conclusions are coverage-relative."]));
            }
        }

        return findings
            .OrderBy(finding => ClassificationPriority(finding.Classification))
            .ThenBy(finding => finding.HttpMethod, StringComparer.Ordinal)
            .ThenBy(finding => finding.NormalizedPathKey, StringComparer.Ordinal)
            .ThenBy(finding => finding.ClientSourceLabel, StringComparer.Ordinal)
            .ThenBy(finding => finding.ServerSourceLabel, StringComparer.Ordinal)
            .ThenBy(finding => finding.ClientFilePath ?? finding.ServerFilePath, StringComparer.Ordinal)
            .ThenBy(finding => finding.ClientStartLine ?? finding.ServerStartLine ?? 0)
            .ToArray();
    }

    private static EndpointCandidate ToEndpointCandidate(CombinedFactRow fact)
    {
        var isClient = fact.FactType == FactTypes.HttpCallDetected || HasAny(fact.Properties, "clientFramework", "urlKind", "dynamicReason");
        var isServer = fact.FactType == FactTypes.HttpRouteBinding || HasAny(fact.Properties, "controllerName", "actionName", "routeTemplates");
        var method = FirstValue(fact.Properties, "httpMethod", "httpMethods", "methodName") ?? fact.TargetSymbol ?? "ANY";
        var normalized = TryNormalizeEndpoint(fact.Properties);
        var key = FirstValue(fact.Properties, "normalizedPathKey") ?? normalized?.PathKey;
        var template = FirstValue(fact.Properties, "normalizedPathTemplate", "routeTemplate", "path") ?? normalized?.PathTemplate;
        var expanded = new SortedSet<string>(SplitList(FirstValue(fact.Properties, "expandedPathKeys", "expandedRouteKeys")), StringComparer.Ordinal);
        if (normalized is not null)
        {
            foreach (var expandedKey in EndpointRouteNormalizer.ExpandOptionalPathKeys(normalized))
            {
                expanded.Add(expandedKey);
            }
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            expanded.Add(key);
        }

        var urlKind = FirstValue(fact.Properties, "urlKind");
        var dynamicReason = FirstValue(fact.Properties, "dynamicReason");
        return new EndpointCandidate(
            fact,
            method.ToUpperInvariant(),
            template,
            key,
            expanded.ToArray(),
            string.Equals(urlKind, "dynamic", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(dynamicReason),
            dynamicReason,
            isClient,
            isServer);
    }

    private static CombinedEndpointFinding CreateEndpointFinding(
        string classification,
        EndpointCandidate? client,
        EndpointCandidate? server,
        string staticMatchQuality,
        bool sameSource,
        IReadOnlyList<string> notes)
    {
        return new CombinedEndpointFinding(
            classification,
            client?.Method ?? server?.Method ?? "ANY",
            client?.NormalizedPathKey ?? server?.NormalizedPathKey,
            client?.Fact.SourceIndexId,
            client?.Fact.SourceLabel,
            client?.Fact.ScanId,
            client?.Fact.CommitSha,
            client?.Fact.CombinedFactId,
            client?.Fact.OriginalFactId,
            client?.Fact.RuleId,
            client?.Fact.EvidenceTier,
            client is null ? null : SafePath(client.Fact.FilePath),
            client?.Fact.StartLine,
            client?.Fact.EndLine,
            server?.Fact.SourceIndexId,
            server?.Fact.SourceLabel,
            server?.Fact.ScanId,
            server?.Fact.CommitSha,
            server?.Fact.CombinedFactId,
            server?.Fact.OriginalFactId,
            server?.Fact.RuleId,
            server?.Fact.EvidenceTier,
            server is null ? null : SafePath(server.Fact.FilePath),
            server?.Fact.StartLine,
            server?.Fact.EndLine,
            staticMatchQuality,
            sameSource,
            notes);
    }

    internal static IReadOnlyList<CombinedDependencySurfaceRow> BuildSurfaces(IReadOnlyList<CombinedFactRow> facts)
    {
        return facts
            .Select(ToSurface)
            .OfType<CombinedDependencySurfaceRow>()
            .OrderBy(surface => surface.SurfaceKind, StringComparer.Ordinal)
            .ThenBy(surface => surface.SourceLabel, StringComparer.Ordinal)
            .ThenBy(surface => surface.DisplayName, StringComparer.Ordinal)
            .ThenBy(surface => surface.FilePath, StringComparer.Ordinal)
            .ThenBy(surface => surface.StartLine)
            .ToArray();
    }

    private static CombinedDependencySurfaceRow? ToSurface(CombinedFactRow fact)
    {
        var surfaceKind = SurfaceKind(fact);
        if (surfaceKind is null)
        {
            return null;
        }

        var httpMethod = FirstValue(fact.Properties, "httpMethod", "httpMethods", "methodName");
        var normalizedPathKey = FirstValue(fact.Properties, "normalizedPathKey");
        var operationName = FirstValue(fact.Properties, "operationName");
        var tableName = SafeSqlIdentifierList(FirstValue(fact.Properties, "tableName", "tableNames"), 100, allowSpaces: true);
        var columns = SafeSqlIdentifierList(FirstValue(fact.Properties, "columnNames", "fieldNames"), 80, allowSpaces: false);
        var sourceKind = FirstValue(fact.Properties, "sqlSourceKind", "sourceKind");
        var shapeHash = FirstValue(fact.Properties, "queryShapeHash", "patternHash");
        var textHash = FirstValue(fact.Properties, "textHash");
        var textLength = FirstValue(fact.Properties, "textLength");
        var packageName = FirstValue(fact.Properties, "packageName", "package", "dependencyName", "moduleName", "name");
        var version = FirstValue(fact.Properties, "version", "packageVersion");
        var configKey = FirstValue(fact.Properties, "keyPath", "configKey", "connectionStringName", "environmentVariableName");
        var displayName = surfaceKind switch
        {
            "http-client" or "http-route" => normalizedPathKey ?? FirstValue(fact.Properties, "normalizedPathTemplate") ?? $"{httpMethod ?? "ANY"} unknown",
            "sql-query" => SqlSurfaceDisplayName(fact, operationName, tableName, columns, sourceKind, shapeHash, textHash),
            "package-config" => packageName ?? configKey ?? $"unknown-package-config:{fact.CombinedFactId}",
            _ => $"unknown-surface:{fact.CombinedFactId}"
        };

        return new CombinedDependencySurfaceRow(
            surfaceKind,
            displayName,
            fact.SourceIndexId,
            fact.SourceLabel,
            fact.ScanId,
            fact.CommitSha,
            fact.CombinedFactId,
            fact.OriginalFactId,
            fact.FactType,
            fact.RuleId,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            httpMethod,
            normalizedPathKey,
            operationName,
            tableName,
            columns ?? (surfaceKind == "sql-query" ? "n/a" : null),
            sourceKind,
            shapeHash,
            textHash,
            textLength,
            packageName,
            version,
            configKey);
    }

    private static string? SurfaceKind(CombinedFactRow fact)
    {
        if (fact.FactType == FactTypes.HttpCallDetected)
        {
            return "http-client";
        }

        if (fact.FactType == FactTypes.HttpRouteBinding)
        {
            return "http-route";
        }

        if (fact.FactType is FactTypes.QueryPatternDetected or FactTypes.SqlTextUsed or FactTypes.DatabaseColumnMapping or FactTypes.DapperCallDetected or FactTypes.SqlCommandDetected)
        {
            return "sql-query";
        }

        if (fact.FactType.Contains("Package", StringComparison.Ordinal)
            || fact.FactType.Contains("Dependency", StringComparison.Ordinal)
            || fact.FactType.Contains("ProjectReference", StringComparison.Ordinal)
            || fact.FactType.Contains("Config", StringComparison.Ordinal)
            || fact.FactType.Contains("ConnectionString", StringComparison.Ordinal)
            || fact.FactType.Contains("EnvironmentVariable", StringComparison.Ordinal))
        {
            return "package-config";
        }

        return null;
    }

    private static IReadOnlyList<CombinedNeedsReviewRow> BuildNeedsReview(IReadOnlyList<CombinedEndpointFinding> endpointFindings, IReadOnlyList<CombinedFactRow> facts)
    {
        var rows = new List<CombinedNeedsReviewRow>();
        rows.AddRange(endpointFindings
            .Where(finding => finding.Classification is CombinedEndpointClassifications.DynamicClientUrlNeedsReview or CombinedEndpointClassifications.AmbiguousMatch or CombinedEndpointClassifications.UnknownAnalysisGap or CombinedEndpointClassifications.MethodMismatch)
            .Select(finding => new CombinedNeedsReviewRow(
                finding.Classification,
                string.Join(" ", finding.Notes.DefaultIfEmpty(finding.Classification)),
                finding.ClientSourceIndexId ?? finding.ServerSourceIndexId,
                finding.ClientSourceLabel ?? finding.ServerSourceLabel,
                finding.ClientCombinedFactId ?? finding.ServerCombinedFactId,
                finding.ClientFilePath ?? finding.ServerFilePath,
                finding.ClientStartLine ?? finding.ServerStartLine)));

        rows.AddRange(facts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap)
            .Select(fact => new CombinedNeedsReviewRow(
                FactTypes.AnalysisGap,
                fact.TargetSymbol ?? "Analysis gap evidence is present.",
                fact.SourceIndexId,
                fact.SourceLabel,
                fact.CombinedFactId,
                SafePath(fact.FilePath),
                fact.StartLine)));

        return rows
            .OrderBy(row => row.ReviewKind, StringComparer.Ordinal)
            .ThenBy(row => row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(row => row.FilePath, StringComparer.Ordinal)
            .ThenBy(row => row.StartLine ?? 0)
            .ToArray();
    }

    private static CombinedDependencyEdgeRow SanitizeEdge(CombinedDependencyEdgeRow edge)
    {
        return edge with { FilePath = SafePath(edge.FilePath) };
    }

    private static async Task<(string? MarkdownPath, string? JsonPath)> WriteOutputsAsync(string outputPath, string format, CombinedDependencyReportDocument report, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(outputPath);
        if (Directory.Exists(fullPath) || string.IsNullOrWhiteSpace(Path.GetExtension(fullPath)))
        {
            Directory.CreateDirectory(fullPath);
            var markdownPath = Path.Combine(fullPath, "dependency-report.md");
            var jsonPath = Path.Combine(fullPath, "dependency-report.json");
            await File.WriteAllTextAsync(markdownPath, RenderMarkdown(report), cancellationToken);
            await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine, cancellationToken);
            return (markdownPath, jsonPath);
        }

        var directoryName = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        if (format == "json")
        {
            await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine, cancellationToken);
            return (null, fullPath);
        }

        await File.WriteAllTextAsync(fullPath, RenderMarkdown(report), cancellationToken);
        return (fullPath, null);
    }

    private static string RenderMarkdown(CombinedDependencyReportDocument report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Dependency Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Report coverage: `{report.ReportCoverage}`");
        builder.AppendLine($"- Sources: `{report.Summary.SourceCount}`");
        builder.AppendLine($"- Facts: `{report.Summary.FactCount}`");
        builder.AppendLine($"- Dependency edges: `{report.Summary.DependencyEdgeCount}`");
        builder.AppendLine($"- Endpoint findings: `{report.Summary.EndpointFindingCount}`");
        AppendDictionary(builder, "Endpoint findings by classification", report.Summary.EndpointFindingsByClassification);
        AppendDictionary(builder, "Dependency surfaces by kind", report.Summary.SurfacesByKind);
        AppendDictionary(builder, "Dependency edges by kind", report.Summary.EdgesByKind);
        AppendList(builder, "Coverage warnings", report.CoverageWarnings);

        builder.AppendLine("## Sources");
        builder.AppendLine();
        AppendRows(builder, report.Sources, "| Label | Language | Repo | Scan root | Commit | Analysis | Build |", "| --- | --- | --- | --- | --- | --- | --- |",
            source => $"| {Cell(source.Label)} | {Cell(source.Language ?? "unknown")} | {Cell(source.RepoName)} | {Cell(source.ScanRootRelativePath ?? ".")} | {Cell(source.CommitSha)} | {Cell(source.AnalysisLevel)} | {Cell(source.BuildStatus)} |");

        builder.AppendLine("## Endpoint Alignment");
        builder.AppendLine();
        AppendRows(builder, report.EndpointFindings, "| Classification | Method | Path | Client | Server | Quality | Evidence |", "| --- | --- | --- | --- | --- | --- | --- |",
            finding => $"| {Cell(finding.Classification)} | {Cell(finding.HttpMethod)} | {Cell(finding.NormalizedPathKey ?? "unknown")} | {Cell(EvidenceLabel(finding.ClientSourceLabel, finding.ClientFilePath, finding.ClientStartLine))} | {Cell(EvidenceLabel(finding.ServerSourceLabel, finding.ServerFilePath, finding.ServerStartLine))} | {Cell(finding.StaticMatchQuality)} | {Cell(string.Join(" ", finding.Notes))} |");

        builder.AppendLine("## Dependency Surfaces");
        builder.AppendLine();
        builder.AppendLine("- SQL query rows are static shape or hash evidence only; they do not prove runtime execution, database schema existence, dialect validity, generated SQL equivalence, or branch feasibility.");
        builder.AppendLine();
        AppendRows(builder, report.DependencySurfaces, "| Kind | Source | Name | Details | Evidence |", "| --- | --- | --- | --- | --- |",
            surface => $"| {Cell(surface.SurfaceKind)} | {Cell(surface.SourceLabel)} | {Cell(surface.DisplayName)} | {Cell(SurfaceDetails(surface))} | {Cell($"{surface.RuleId} {surface.EvidenceTier} {surface.FilePath}:{surface.StartLine}")} |");

        builder.AppendLine("## Dependency Edges");
        builder.AppendLine();
        AppendRows(builder, report.DependencyEdges, "| Kind | Source | From | To | Evidence |", "| --- | --- | --- | --- | --- |",
            edge => $"| {Cell(edge.EdgeKind)} | {Cell(edge.SourceLabel)} | {Cell(edge.SourceSymbol ?? "unknown")} | {Cell(edge.TargetSymbol ?? "unknown")} | {Cell($"{edge.RuleId} {edge.EvidenceTier} {edge.FilePath}:{edge.StartLine}")} |");

        builder.AppendLine("## Needs Review");
        builder.AppendLine();
        AppendRows(builder, report.NeedsReview, "| Kind | Source | Message | Evidence |", "| --- | --- | --- | --- |",
            row => $"| {Cell(row.ReviewKind)} | {Cell(row.SourceLabel ?? "unknown")} | {Cell(row.Message)} | {Cell(EvidenceLabel(null, row.FilePath, row.StartLine))} |");

        builder.AppendLine("## Known Gaps");
        builder.AppendLine();
        AppendRows(builder, report.KnownGaps, "| Source | Category | Count | Example |", "| --- | --- | --- | --- |",
            gap => $"| {Cell(gap.SourceLabel)} | {Cell(gap.Category)} | {gap.Count} | {Cell(gap.Example)} |");

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
            builder.AppendLine("No evidence found in the combined index.");
            builder.AppendLine();
            return;
        }

        if (rows.Count > MarkdownRowLimit)
        {
            builder.AppendLine($"Showing first {MarkdownRowLimit} of {rows.Count} rows. JSON contains all rows.");
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

    private static void AppendDictionary(StringBuilder builder, string title, IReadOnlyDictionary<string, int> counts)
    {
        if (counts.Count == 0)
        {
            return;
        }

        builder.AppendLine($"- {title}: {string.Join(", ", counts.Select(pair => $"`{pair.Key}` {pair.Value}"))}");
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
            builder.AppendLine($"  - {value}");
        }
    }

    internal static string SurfaceDetails(CombinedDependencySurfaceRow surface)
    {
        return surface.SurfaceKind switch
        {
            "http-client" or "http-route" => $"{surface.HttpMethod ?? "ANY"} {surface.NormalizedPathKey ?? "unknown"}",
            "sql-query" => $"op {surface.OperationName ?? "unknown"} table {surface.TableName ?? "n/a"} columns {surface.ColumnNames ?? "n/a"} source {surface.SourceKind ?? "unknown"} shape {surface.ShapeHash ?? surface.TextHash ?? "n/a"}",
            "package-config" => $"package {surface.PackageName ?? "n/a"} version {surface.Version ?? "n/a"} key {surface.ConfigKey ?? "n/a"}",
            _ => string.Empty
        };
    }

    private static string SqlSurfaceDisplayName(
        CombinedFactRow fact,
        string? operationName,
        string? tableName,
        string? columns,
        string? sourceKind,
        string? shapeHash,
        string? textHash)
    {
        if (!string.IsNullOrWhiteSpace(shapeHash))
        {
            return $"shape:{shapeHash}";
        }

        if (!string.IsNullOrWhiteSpace(operationName)
            || !string.IsNullOrWhiteSpace(tableName)
            || !string.IsNullOrWhiteSpace(columns))
        {
            return string.Join(
                " ",
                new[]
                {
                    operationName,
                    tableName is null ? null : $"table:{tableName}",
                    columns is null ? null : $"columns:{columns}",
                    sourceKind is null ? null : $"source:{sourceKind}"
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        if (!string.IsNullOrWhiteSpace(textHash))
        {
            return $"text:{textHash}";
        }

        return $"unknown-sql:{Hash(fact.OriginalFactId ?? fact.CombinedFactId, 16)}";
    }

    private static string? SafeSqlIdentifierList(string? value, int maxIdentifierLength, bool allowSpaces)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var identifiers = value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(identifier => IsSafeSqlIdentifier(identifier, maxIdentifierLength, allowSpaces))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(identifier => identifier, StringComparer.Ordinal)
            .ToArray();
        return identifiers.Length == 0 ? null : string.Join(';', identifiers);
    }

    private static bool IsSafeSqlIdentifier(string value, int maxIdentifierLength, bool allowSpaces)
    {
        if (value.Length == 0 || value.Length > maxIdentifierLength)
        {
            return false;
        }

        if (value.Contains("://", StringComparison.Ordinal)
            || value.Contains("--", StringComparison.Ordinal)
            || value.Contains("/*", StringComparison.Ordinal)
            || value.Contains("*/", StringComparison.Ordinal))
        {
            return false;
        }

        var pattern = allowSpaces
            ? "^[A-Za-z0-9_. -]+$"
            : "^[A-Za-z0-9_.-]+$";
        if (!Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
        {
            return false;
        }

        var token = value.Trim().ToUpperInvariant();
        return token is not ("SELECT" or "INSERT" or "UPDATE" or "DELETE" or "CREATE" or "ALTER" or "DROP" or "TRUNCATE" or "MERGE" or "CALL" or "EXEC" or "EXECUTE" or "WHERE" or "FROM" or "JOIN");
    }

    private static string EvidenceLabel(string? sourceLabel, string? filePath, int? startLine)
    {
        var location = filePath is null ? "n/a" : $"{filePath}:{startLine ?? 0}";
        return sourceLabel is null ? location : $"{sourceLabel} {location}";
    }

    internal static string Cell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
    }

    internal static IReadOnlyDictionary<string, int> CountBy<T>(IEnumerable<T> rows, Func<T, string> selector)
    {
        return rows
            .GroupBy(selector, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static IReadOnlyList<CombinedKnownGapRow> ReadKnownGaps(CombinedReportSource source, string manifestJson, List<string> warnings)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<ScanManifest>(manifestJson, JsonOptions);
            if (manifest?.KnownGaps is null || manifest.KnownGaps.Count == 0)
            {
                return [];
            }

            warnings.Add($"{source.Label} manifest contains known gaps; dependency conclusions are reduced coverage.");
            return manifest.KnownGaps
                .GroupBy(GapCategory, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new CombinedKnownGapRow(
                    source.SourceIndexId,
                    source.Label,
                    group.Key,
                    group.Count(),
                    group.OrderBy(value => value, StringComparer.Ordinal).First()))
                .ToArray();
        }
        catch (JsonException)
        {
            warnings.Add($"{source.Label} manifest JSON could not be parsed; source coverage is reduced.");
            return [];
        }
    }

    private static void AddCoverageWarnings(CombinedReportSource source, List<string> warnings)
    {
        if (!source.AnalysisLevel.Equals("Level1SemanticAnalysis", StringComparison.Ordinal) || !source.BuildStatus.Equals("Succeeded", StringComparison.Ordinal))
        {
            warnings.Add($"{source.Label} reports {source.AnalysisLevel}/{source.BuildStatus}; dependency conclusions are reduced coverage.");
        }

        if (string.IsNullOrWhiteSpace(source.CommitSha) || source.CommitSha == "unknown")
        {
            warnings.Add($"{source.Label} commit SHA is unknown; long-term snapshot comparisons are not credible.");
        }

        if (string.IsNullOrWhiteSpace(source.Language))
        {
            warnings.Add($"{source.Label} language is unknown; dependency grouping is reduced coverage.");
        }

        if (source.LanguageCorrected)
        {
            warnings.Add($"{source.Label} language was corrected from `{source.StoredLanguage ?? "unknown"}` to `{source.Language ?? "unknown"}` based on scanner version.");
        }
    }

    private static string? CorrectLanguage(string scannerVersion, string? storedLanguage)
    {
        if (scannerVersion.Contains("typescript", StringComparison.OrdinalIgnoreCase))
        {
            return "typescript";
        }

        if (scannerVersion.Contains("jvm", StringComparison.OrdinalIgnoreCase))
        {
            return "jvm";
        }

        if (scannerVersion.Contains("python", StringComparison.OrdinalIgnoreCase))
        {
            return "python";
        }

        if (scannerVersion.Contains("tracemap", StringComparison.OrdinalIgnoreCase))
        {
            return "csharp";
        }

        return string.IsNullOrWhiteSpace(storedLanguage) ? null : storedLanguage;
    }

    internal static bool SourceHasCredibilityGap(CombinedReportSource source)
    {
        return source.CommitSha == "unknown" || (!source.AnalysisLevel.Equals("Level1SemanticAnalysis", StringComparison.Ordinal) && !source.AnalysisLevel.Contains("Reduced", StringComparison.OrdinalIgnoreCase));
    }

    internal static string? FirstValue(IReadOnlyDictionary<string, string> properties, params string[] keys)
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

    private static bool HasAny(IReadOnlyDictionary<string, string> properties, params string[] keys)
    {
        return keys.Any(key => properties.ContainsKey(key));
    }

    private static IReadOnlyList<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    internal static bool MethodsCompatible(string clientMethod, string serverMethod)
    {
        var clientMethods = SplitMethods(clientMethod);
        var serverMethods = SplitMethods(serverMethod);
        return clientMethods.Contains("ANY", StringComparer.Ordinal)
            || serverMethods.Contains("ANY", StringComparer.Ordinal)
            || clientMethods.Intersect(serverMethods, StringComparer.Ordinal).Any();
    }

    internal static IReadOnlyList<string> SplitMethods(string? value)
    {
        var methods = SplitList(value)
            .Select(method => method.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(method => method, StringComparer.Ordinal)
            .ToArray();
        return methods.Length == 0 ? ["ANY"] : methods;
    }

    internal static NormalizedEndpointRoute? TryNormalizeEndpoint(IReadOnlyDictionary<string, string> properties)
    {
        var normalizedTemplate = FirstValue(properties, "normalizedPathTemplate");
        if (!string.IsNullOrWhiteSpace(normalizedTemplate))
        {
            return EndpointRouteNormalizer.Normalize(normalizedTemplate);
        }

        var route = FirstValue(properties, "routeTemplate", "routePattern", "routeTemplates", "path", "urlPath");
        if (string.IsNullOrWhiteSpace(route))
        {
            return null;
        }

        return EndpointRouteNormalizer.Normalize(
            CombineRouteTemplates(route),
            FirstValue(properties, "basePathPrefix"),
            RouteTokens(properties));
    }

    private static string CombineRouteTemplates(string route)
    {
        var templates = route
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        return templates.Length == 0 ? route : string.Join("/", templates.Select(template => template.Trim('/')));
    }

    private static IReadOnlyDictionary<string, string> RouteTokens(IReadOnlyDictionary<string, string> properties)
    {
        var controller = FirstValue(properties, "controllerName", "controller", "containingType", "targetContainingType");
        if (string.IsNullOrWhiteSpace(controller))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var name = controller.Split('.').Last().Trim();
        if (name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^"Controller".Length];
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["controller"] = name };
    }

    private static string SanitizedDynamicReason(EndpointCandidate candidate)
    {
        var reason = string.IsNullOrWhiteSpace(candidate.DynamicReason) ? "Unknown" : candidate.DynamicReason.Trim();
        reason = reason switch
        {
            "TemplateExpressionNotResolvable"
                or "VariableConcatenation"
                or "HelperFunctionCall"
                or "IndirectReceiver"
                or "ComplexExpression"
                or "Unknown" => reason,
            _ => "Unknown"
        };

        return $"Client URL dynamic reason: {reason}.";
    }

    internal static int ClassificationPriority(string classification)
    {
        return classification switch
        {
            CombinedEndpointClassifications.UnknownAnalysisGap => 0,
            CombinedEndpointClassifications.DynamicClientUrlNeedsReview => 1,
            CombinedEndpointClassifications.AmbiguousMatch => 2,
            CombinedEndpointClassifications.MethodMismatch => 3,
            CombinedEndpointClassifications.MatchedEndpoint => 4,
            CombinedEndpointClassifications.OptionalSegmentMatch => 5,
            CombinedEndpointClassifications.ClientCallNoServerEndpoint => 6,
            CombinedEndpointClassifications.ServerEndpointNoClientMatch => 7,
            _ => 99
        };
    }

    private static string GapCategory(string gap)
    {
        var trimmed = gap.Trim();
        if (trimmed.Length == 0)
        {
            return "General";
        }

        var colonIndex = trimmed.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex > 0)
        {
            return trimmed[..colonIndex].Trim();
        }

        var sentenceIndex = trimmed.IndexOf('.', StringComparison.Ordinal);
        if (sentenceIndex > 0)
        {
            return trimmed[..sentenceIndex].Trim();
        }

        return trimmed.Length == 0 ? "General" : trimmed;
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            return values is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(values.Where(pair => pair.Value is not null), StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string NormalizeFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "markdown" or "md" => "markdown",
            "json" => "json",
            _ => throw new ArgumentException("report --format must be markdown or json.")
        };
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
}
