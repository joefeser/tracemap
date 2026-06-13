using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.EndpointAlignment;

internal sealed record EndpointIndex(EndpointSource Source, IReadOnlyList<EndpointCandidate> Endpoints, IReadOnlyList<EndpointCandidate> AnalysisGaps);

internal static class EndpointIndexReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<EndpointIndex> ReadClientAsync(string indexPath, string? label, CancellationToken cancellationToken)
    {
        var index = await ReadAsync(indexPath, label, cancellationToken);
        return index with
        {
            Endpoints = index.Endpoints
                .Where(endpoint => endpoint.FactType == FactTypes.HttpCallDetected)
                .OrderBy(endpoint => endpoint.FilePath, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.StartLine)
                .ThenBy(endpoint => endpoint.FactId, StringComparer.Ordinal)
                .ToArray()
        };
    }

    public static async Task<EndpointIndex> ReadServerAsync(string indexPath, string? label, CancellationToken cancellationToken)
    {
        var index = await ReadAsync(indexPath, label, cancellationToken);
        return index with
        {
            Endpoints = index.Endpoints
                .Where(endpoint => endpoint.FactType == FactTypes.HttpRouteBinding)
                .GroupBy(endpoint => $"{endpoint.Method}|{endpoint.NormalizedPathKey}", StringComparer.Ordinal)
                .Select(group => group.OrderBy(endpoint => EvidenceRank(endpoint.EvidenceTier)).First())
                .OrderBy(endpoint => endpoint.NormalizedPathKey, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.FactId, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static async Task<EndpointIndex> ReadAsync(string indexPath, string? label, CancellationToken cancellationToken)
    {
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException("TraceMap endpoint index does not exist.", indexPath);
        }

        await using var connection = new SqliteConnection($"Data Source={indexPath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);
        var manifest = await ReadManifestAsync(connection, cancellationToken);
        var source = new EndpointSource(
            label ?? manifest.ScanRootRelativePath ?? manifest.RepoName,
            FactFactory.Hash(Path.GetFullPath(indexPath), 32),
            manifest.ScanId,
            manifest.RepoName,
            manifest.RemoteUrl,
            manifest.Branch,
            manifest.CommitSha,
            manifest.ScannerVersion,
            manifest.AnalysisLevel,
            manifest.BuildStatus,
            manifest.ScanRootRelativePath,
            manifest.ScanRootPathHash,
            manifest.GitRootHash);
        var endpoints = new List<EndpointCandidate>();
        var gaps = new List<EndpointCandidate>();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select fact_id,
                   scan_id,
                   commit_sha,
                   fact_type,
                   rule_id,
                   evidence_tier,
                   source_symbol,
                   target_symbol,
                   contract_element,
                   file_path,
                   start_line,
                   end_line,
                   properties_json
            from facts
            where fact_type in ('HttpCallDetected', 'HttpRouteBinding', 'AnalysisGap')
            order by file_path, start_line, fact_type, fact_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var properties = ReadProperties(reader.GetString(12));
            var candidate = ToEndpointCandidate(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                properties);
            if (candidate.FactType == FactTypes.AnalysisGap)
            {
                gaps.Add(candidate);
            }
            else
            {
                endpoints.Add(candidate);
            }
        }

        return new EndpointIndex(source, endpoints, gaps);
    }

    private static EndpointCandidate ToEndpointCandidate(
        string factId,
        string scanId,
        string commitSha,
        string factType,
        string ruleId,
        string evidenceTier,
        string? sourceSymbol,
        string? targetSymbol,
        string? contractElement,
        string filePath,
        int startLine,
        int endLine,
        IReadOnlyDictionary<string, string> properties)
    {
        var method = Get(properties, "httpMethod", "methodName", "httpMethods").Split(';', ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "ANY";
        var normalized = TryNormalize(properties);
        var isDynamic = properties.TryGetValue("urlKind", out var urlKind) && urlKind.Equals("dynamic", StringComparison.OrdinalIgnoreCase);
        return new EndpointCandidate(
            factId,
            scanId,
            commitSha,
            factType,
            ruleId,
            evidenceTier,
            filePath,
            startLine,
            endLine,
            sourceSymbol,
            targetSymbol,
            contractElement,
            method.ToUpperInvariant(),
            normalized?.PathTemplate,
            normalized?.PathKey,
            normalized is null ? [] : EndpointRouteNormalizer.ExpandOptionalPathKeys(normalized),
            normalized?.ParameterNames ?? [],
            normalized?.OptionalParameterNames ?? [],
            normalized?.QueryParameterNames ?? [],
            normalized?.HasQueryParameters ?? false,
            isDynamic || normalized is null && factType == FactTypes.HttpCallDetected,
            Get(properties, "dynamicReason"),
            properties);
    }

    private static NormalizedEndpointRoute? TryNormalize(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("normalizedPathTemplate", out var template) && properties.TryGetValue("normalizedPathKey", out _))
        {
            return EndpointRouteNormalizer.Normalize(template);
        }

        var route = Get(properties, "routeTemplate", "routePattern", "routeTemplates", "path", "urlPath");
        if (string.IsNullOrWhiteSpace(route))
        {
            return null;
        }

        var selectedRoute = route.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? route;
        return EndpointRouteNormalizer.Normalize(selectedRoute, properties.TryGetValue("basePathPrefix", out var basePath) ? basePath : null);
    }

    private static async Task<ScanManifest> ReadManifestAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select manifest_json from scan_manifest order by scanned_at desc limit 1;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string json)
        {
            throw new InvalidDataException("TraceMap index does not contain a scan manifest.");
        }

        return JsonSerializer.Deserialize<ScanManifest>(json, JsonOptions)
            ?? throw new InvalidDataException("TraceMap scan manifest could not be parsed.");
    }

    private static IReadOnlyDictionary<string, string> ReadProperties(string json)
    {
        return JsonSerializer.Deserialize<SortedDictionary<string, string>>(json, JsonOptions)
            ?? new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    private static string Get(IReadOnlyDictionary<string, string> properties, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (properties.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static int EvidenceRank(string tier)
    {
        return tier switch
        {
            EvidenceTiers.Tier1Semantic => 0,
            EvidenceTiers.Tier2Structural => 1,
            EvidenceTiers.Tier3SyntaxOrTextual => 2,
            _ => 3
        };
    }
}
