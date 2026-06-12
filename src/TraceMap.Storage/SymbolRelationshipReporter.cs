using System.Text;
using Microsoft.Data.Sqlite;

namespace TraceMap.Storage;

public sealed record RelationshipOptions(
    string IndexPath,
    string Symbol,
    string OutputPath,
    string Direction = "both",
    int MaxDepth = 5,
    int MaxPaths = 100);

public sealed record RelationshipReport(
    string ReportPath,
    int RelationshipCount,
    int PathCount);

internal sealed record SymbolRelationshipEdge(
    string RelationshipId,
    string SourceSymbolId,
    string SourceDisplayName,
    string TargetSymbolId,
    string TargetDisplayName,
    string RelationshipKind,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine);

public static class SymbolRelationshipReporter
{
    public static async Task<RelationshipReport> WriteAsync(RelationshipOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("Relationship report requires an index path.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Symbol))
        {
            throw new ArgumentException("Relationship report requires a symbol filter.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("Relationship report requires an output path.", nameof(options));
        }

        var direction = NormalizeDirection(options.Direction);
        var maxDepth = Math.Clamp(options.MaxDepth, 1, 20);
        var maxPaths = Math.Clamp(options.MaxPaths, 1, 1000);
        await using var connection = new SqliteConnection($"Data Source={options.IndexPath}");
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, "symbol_relationships", cancellationToken))
        {
            throw new InvalidDataException("TraceMap index does not contain symbol_relationships. Re-scan the repo with the current scanner.");
        }

        var manifest = await ReadManifestAsync(connection, cancellationToken);
        var edges = await ReadEdgesAsync(connection, cancellationToken);
        var paths = FindPaths(edges, options.Symbol, direction, maxDepth, maxPaths);
        var reportPath = GetReportPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(
            reportPath,
            RenderMarkdown(options, manifest, edges.Count, paths, direction, maxDepth, maxPaths),
            cancellationToken);

        return new RelationshipReport(reportPath, edges.Count, paths.Count);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value) > 0;
    }

    private static async Task<(string Repo, string CommitSha, string AnalysisLevel)> ReadManifestAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select repo, commit_sha, analysis_level
            from scan_manifest
            order by scanned_at desc
            limit 1;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException("TraceMap index does not contain a scan manifest.");
        }

        return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    private static async Task<IReadOnlyList<SymbolRelationshipEdge>> ReadEdgesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var edges = new List<SymbolRelationshipEdge>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              r.relationship_id,
              r.source_symbol_id,
              coalesce(source.display_name, r.source_symbol_id) as source_display_name,
              r.target_symbol_id,
              coalesce(target.display_name, r.target_symbol_id) as target_display_name,
              r.relationship_kind,
              r.rule_id,
              r.evidence_tier,
              r.file_path,
              r.start_line,
              r.end_line
            from symbol_relationships r
            left join symbols source on source.scan_id = r.scan_id and source.symbol_id = r.source_symbol_id
            left join symbols target on target.scan_id = r.scan_id and target.symbol_id = r.target_symbol_id
            order by source_display_name, relationship_kind, target_display_name, file_path, start_line, relationship_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            edges.Add(new SymbolRelationshipEdge(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10)));
        }

        return edges;
    }

    private static IReadOnlyList<IReadOnlyList<SymbolRelationshipEdge>> FindPaths(
        IReadOnlyList<SymbolRelationshipEdge> edges,
        string symbolFilter,
        string direction,
        int maxDepth,
        int maxPaths)
    {
        var outgoing = edges.GroupBy(edge => edge.SourceSymbolId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var incoming = edges.GroupBy(edge => edge.TargetSymbolId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var starts = edges.Where(edge => Matches(edge, symbolFilter, direction))
            .OrderBy(edge => edge.SourceDisplayName, StringComparer.Ordinal)
            .ThenBy(edge => edge.RelationshipKind, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetDisplayName, StringComparer.Ordinal)
            .ThenBy(edge => edge.RelationshipId, StringComparer.Ordinal)
            .ToArray();

        var paths = new List<IReadOnlyList<SymbolRelationshipEdge>>();
        foreach (var start in starts)
        {
            Search([start], new HashSet<string>(StringComparer.Ordinal) { start.RelationshipId }, NextNode(start));
            if (paths.Count >= maxPaths)
            {
                break;
            }
        }

        return paths;

        string NextNode(SymbolRelationshipEdge edge)
        {
            return direction == "incoming" ? edge.SourceSymbolId : edge.TargetSymbolId;
        }

        void Search(List<SymbolRelationshipEdge> path, HashSet<string> seenIds, string currentSymbolId)
        {
            if (paths.Count >= maxPaths || path.Count >= maxDepth)
            {
                paths.Add(path.ToArray());
                return;
            }

            var candidates = direction switch
            {
                "incoming" => incoming.GetValueOrDefault(currentSymbolId) ?? [],
                "outgoing" => outgoing.GetValueOrDefault(currentSymbolId) ?? [],
                _ => (outgoing.GetValueOrDefault(currentSymbolId) ?? [])
                    .Concat(incoming.GetValueOrDefault(currentSymbolId) ?? [])
                    .ToArray()
            };
            var extended = false;
            foreach (var next in candidates.OrderBy(edge => edge.RelationshipKind, StringComparer.Ordinal).ThenBy(edge => edge.RelationshipId, StringComparer.Ordinal))
            {
                if (!seenIds.Add(next.RelationshipId))
                {
                    continue;
                }

                extended = true;
                path.Add(next);
                Search(path, seenIds, direction == "incoming" ? next.SourceSymbolId : next.TargetSymbolId);
                path.RemoveAt(path.Count - 1);
                seenIds.Remove(next.RelationshipId);
            }

            if (!extended)
            {
                paths.Add(path.ToArray());
            }
        }
    }

    private static bool Matches(SymbolRelationshipEdge edge, string symbolFilter, string direction)
    {
        return direction switch
        {
            "incoming" => Contains(edge.TargetDisplayName, symbolFilter) || Contains(edge.TargetSymbolId, symbolFilter),
            "outgoing" => Contains(edge.SourceDisplayName, symbolFilter) || Contains(edge.SourceSymbolId, symbolFilter),
            _ => Contains(edge.SourceDisplayName, symbolFilter)
                || Contains(edge.SourceSymbolId, symbolFilter)
                || Contains(edge.TargetDisplayName, symbolFilter)
                || Contains(edge.TargetSymbolId, symbolFilter)
        };
    }

    private static bool Contains(string value, string expected)
    {
        return value.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirection(string direction)
    {
        var normalized = string.IsNullOrWhiteSpace(direction) ? "both" : direction.Trim().ToLowerInvariant();
        return normalized is "incoming" or "outgoing" or "both"
            ? normalized
            : throw new ArgumentException("--direction must be incoming, outgoing, or both.");
    }

    private static string RenderMarkdown(
        RelationshipOptions options,
        (string Repo, string CommitSha, string AnalysisLevel) manifest,
        int edgeCount,
        IReadOnlyList<IReadOnlyList<SymbolRelationshipEdge>> paths,
        string direction,
        int maxDepth,
        int maxPaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Relationship Report");
        builder.AppendLine();
        builder.AppendLine("## Repository");
        builder.AppendLine();
        builder.AppendLine($"- Repo: `{EscapeInline(manifest.Repo)}`");
        builder.AppendLine($"- Commit SHA: `{EscapeInline(manifest.CommitSha)}`");
        builder.AppendLine($"- Analysis level: `{EscapeInline(manifest.AnalysisLevel)}`");
        builder.AppendLine($"- Symbol filter: `{EscapeInline(options.Symbol)}`");
        builder.AppendLine($"- Direction: `{direction}`");
        builder.AppendLine($"- Relationship edges indexed: `{edgeCount}`");
        builder.AppendLine($"- Paths written: `{paths.Count}`");
        builder.AppendLine($"- Max depth: `{maxDepth}`");
        builder.AppendLine($"- Max paths: `{maxPaths}`");
        builder.AppendLine();
        builder.AppendLine("This report traverses compiler-resolved symbol relationships only. It does not prove runtime dispatch or reachability.");
        builder.AppendLine();
        builder.AppendLine("## Paths");
        builder.AppendLine();

        if (paths.Count == 0)
        {
            builder.AppendLine("- None found.");
            return builder.ToString();
        }

        for (var index = 0; index < paths.Count; index++)
        {
            builder.AppendLine($"### Path {index + 1}");
            builder.AppendLine();
            foreach (var edge in paths[index])
            {
                builder.AppendLine($"- `{EscapeInline(edge.SourceDisplayName)}` --`{EscapeInline(edge.RelationshipKind)}`--> `{EscapeInline(edge.TargetDisplayName)}`");
                builder.AppendLine($"  - Evidence: `{EscapeInline(edge.FilePath)}:{edge.StartLine}-{edge.EndLine}` via `{EscapeInline(edge.RuleId)}` ({EscapeInline(edge.EvidenceTier)})");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string GetReportPath(string outputPath)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        return string.Equals(Path.GetExtension(fullOutputPath), ".md", StringComparison.OrdinalIgnoreCase)
            ? fullOutputPath
            : Path.Combine(fullOutputPath, "relationship-report.md");
    }

    private static string EscapeInline(string value)
    {
        return value.Replace("`", "\\`", StringComparison.Ordinal);
    }
}
