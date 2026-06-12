using System.Text;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Storage;

public sealed record FlowOptions(
    string IndexPath,
    string Symbol,
    string OutputPath,
    int MaxDepth = 5,
    int MaxPaths = 50);

public sealed record FlowReport(
    string ReportPath,
    int EdgeCount,
    int PathCount);

internal sealed record ParameterForwardEdge(
    string FactId,
    string ScanId,
    string Repo,
    string CommitSha,
    string EvidenceTier,
    string RuleId,
    string SourceMethodSymbol,
    string SourceParameterSymbol,
    string SourceNodeKey,
    string TargetMethodSymbol,
    string TargetParameterName,
    string? TargetParameterType,
    string TargetParameterSymbol,
    string TargetNodeKey,
    string? TargetAssemblyName,
    string? TargetAssemblyVersion,
    string FilePath,
    int StartLine,
    int EndLine);

public static class FlowPathReporter
{
    public static async Task<FlowReport> WriteAsync(FlowOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("Flow requires an index path.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Symbol))
        {
            throw new ArgumentException("Flow requires a symbol filter.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("Flow requires an output path.", nameof(options));
        }

        var maxDepth = Math.Clamp(options.MaxDepth, 1, 20);
        var maxPaths = Math.Clamp(options.MaxPaths, 1, 500);
        await using var connection = new SqliteConnection($"Data Source={options.IndexPath}");
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, "parameter_forward_edges", cancellationToken))
        {
            throw new InvalidDataException("TraceMap index does not contain parameter_forward_edges. Re-scan the repo with the current scanner.");
        }

        var manifest = await ReadManifestAsync(connection, cancellationToken);
        var edges = await ReadEdgesAsync(connection, cancellationToken);
        var paths = FindPaths(edges, options.Symbol, maxDepth, maxPaths);
        var reportPath = GetReportPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(
            reportPath,
            RenderMarkdown(options, manifest, edges.Count, paths, maxDepth, maxPaths),
            cancellationToken);

        return new FlowReport(reportPath, edges.Count, paths.Count);
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

    private static async Task<IReadOnlyList<ParameterForwardEdge>> ReadEdgesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var edges = new List<ParameterForwardEdge>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              fact_id,
              scan_id,
              repo,
              commit_sha,
              evidence_tier,
              rule_id,
              source_method_symbol,
              source_parameter_symbol,
              source_node_key,
              target_method_symbol,
              target_parameter_name,
              target_parameter_type,
              target_parameter_symbol,
              target_node_key,
              target_assembly_name,
              target_assembly_version,
              file_path,
              start_line,
              end_line
            from parameter_forward_edges
            order by source_node_key, target_node_key, file_path, start_line, fact_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            edges.Add(new ParameterForwardEdge(
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
                reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetString(12),
                reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.IsDBNull(15) ? null : reader.GetString(15),
                reader.GetString(16),
                reader.GetInt32(17),
                reader.GetInt32(18)));
        }

        return edges;
    }

    private static IReadOnlyList<IReadOnlyList<ParameterForwardEdge>> FindPaths(
        IReadOnlyList<ParameterForwardEdge> edges,
        string symbolFilter,
        int maxDepth,
        int maxPaths)
    {
        var bySource = edges
            .GroupBy(edge => edge.SourceNodeKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(edge => edge.TargetNodeKey, StringComparer.Ordinal)
                    .ThenBy(edge => edge.FilePath, StringComparer.Ordinal)
                    .ThenBy(edge => edge.StartLine)
                    .ThenBy(edge => edge.FactId, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        var starts = edges
            .Where(edge => Matches(edge, symbolFilter))
            .OrderBy(edge => edge.SourceNodeKey, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetNodeKey, StringComparer.Ordinal)
            .ThenBy(edge => edge.FilePath, StringComparer.Ordinal)
            .ThenBy(edge => edge.StartLine)
            .ThenBy(edge => edge.FactId, StringComparer.Ordinal);

        var paths = new List<IReadOnlyList<ParameterForwardEdge>>();
        foreach (var start in starts)
        {
            Search([start], new HashSet<string>(StringComparer.Ordinal) { start.FactId });
            if (paths.Count >= maxPaths)
            {
                break;
            }
        }

        return RemoveStrictSuffixPaths(paths);

        void Search(List<ParameterForwardEdge> path, HashSet<string> seenFactIds)
        {
            if (paths.Count >= maxPaths)
            {
                return;
            }

            var current = path[^1];
            if (path.Count >= maxDepth || !bySource.TryGetValue(current.TargetNodeKey, out var nextEdges))
            {
                paths.Add(path.ToArray());
                return;
            }

            var extended = false;
            foreach (var next in nextEdges)
            {
                if (!seenFactIds.Add(next.FactId))
                {
                    continue;
                }

                extended = true;
                path.Add(next);
                Search(path, seenFactIds);
                path.RemoveAt(path.Count - 1);
                seenFactIds.Remove(next.FactId);
            }

            if (!extended)
            {
                paths.Add(path.ToArray());
            }
        }
    }

    private static IReadOnlyList<IReadOnlyList<ParameterForwardEdge>> RemoveStrictSuffixPaths(
        IReadOnlyList<IReadOnlyList<ParameterForwardEdge>> paths)
    {
        var result = new List<IReadOnlyList<ParameterForwardEdge>>();
        foreach (var path in paths.OrderByDescending(path => path.Count).ThenBy(path => path[0].FactId, StringComparer.Ordinal))
        {
            if (result.Any(existing => IsStrictSuffix(path, existing)))
            {
                continue;
            }

            result.Add(path);
        }

        return result
            .OrderBy(path => path[0].SourceNodeKey, StringComparer.Ordinal)
            .ThenBy(path => path[^1].TargetNodeKey, StringComparer.Ordinal)
            .ThenBy(path => path.Count)
            .ToArray();
    }

    private static bool IsStrictSuffix(IReadOnlyList<ParameterForwardEdge> possibleSuffix, IReadOnlyList<ParameterForwardEdge> possibleLongerPath)
    {
        if (possibleSuffix.Count >= possibleLongerPath.Count)
        {
            return false;
        }

        var offset = possibleLongerPath.Count - possibleSuffix.Count;
        for (var index = 0; index < possibleSuffix.Count; index++)
        {
            if (!string.Equals(possibleSuffix[index].FactId, possibleLongerPath[index + offset].FactId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Matches(ParameterForwardEdge edge, string symbolFilter)
    {
        return Contains(edge.SourceNodeKey, symbolFilter)
            || Contains(edge.SourceMethodSymbol, symbolFilter)
            || Contains(edge.SourceParameterSymbol, symbolFilter);
    }

    private static bool Contains(string value, string filter)
    {
        return value.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static string RenderMarkdown(
        FlowOptions options,
        (string Repo, string CommitSha, string AnalysisLevel) manifest,
        int edgeCount,
        IReadOnlyList<IReadOnlyList<ParameterForwardEdge>> paths,
        int maxDepth,
        int maxPaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Flow Report");
        builder.AppendLine();
        builder.AppendLine($"- Repo: `{EscapeInline(manifest.Repo)}`");
        builder.AppendLine($"- Commit SHA: `{EscapeInline(manifest.CommitSha)}`");
        builder.AppendLine($"- Analysis level: `{EscapeInline(manifest.AnalysisLevel)}`");
        builder.AppendLine($"- Symbol filter: `{EscapeInline(options.Symbol)}`");
        builder.AppendLine($"- Max depth: `{maxDepth}`");
        builder.AppendLine($"- Max paths: `{maxPaths}`");
        builder.AppendLine($"- Indexed parameter-forward edges: `{edgeCount}`");
        builder.AppendLine($"- Rule ID: `{RuleIds.CSharpSemanticParameterForwarding}`");
        builder.AppendLine();
        builder.AppendLine("This report follows parameter-to-parameter forwarding evidence, bounded same-method aliases, and unique constructor field initialization. It does not infer runtime execution, arbitrary cross-method field lifetime, mutations, dependency injection, reflection, dynamic dispatch, or serializer-created values.");
        builder.AppendLine();
        builder.AppendLine("## Parameter Forwarding Paths");
        builder.AppendLine();

        if (paths.Count == 0)
        {
            builder.AppendLine("No parameter-forwarding paths matched the symbol filter.");
            return builder.ToString();
        }

        for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
        {
            builder.AppendLine($"### Path {pathIndex + 1}");
            builder.AppendLine();
            builder.AppendLine("| Step | From | To | Evidence | Rule |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");
            var path = paths[pathIndex];
            for (var stepIndex = 0; stepIndex < path.Count; stepIndex++)
            {
                var edge = path[stepIndex];
                builder.Append("| ")
                    .Append(stepIndex + 1)
                    .Append(" | ")
                    .Append(EscapeCell($"{edge.SourceMethodSymbol} :: {edge.SourceParameterSymbol}"))
                    .Append(" | ")
                    .Append(EscapeCell($"{edge.TargetMethodSymbol} :: {edge.TargetParameterSymbol}"))
                    .Append(" | ")
                    .Append(EscapeCell($"{edge.FilePath}:{edge.StartLine}-{edge.EndLine}"))
                    .Append(" | `")
                    .Append(EscapeInline(edge.RuleId))
                    .AppendLine("` |");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string GetReportPath(string outputPath)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        return Path.GetExtension(fullOutputPath).Equals(".md", StringComparison.OrdinalIgnoreCase)
            ? fullOutputPath
            : Path.Combine(fullOutputPath, "flow-report.md");
    }

    private static string EscapeCell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string EscapeInline(string value)
    {
        return value.Replace("`", "'", StringComparison.Ordinal);
    }
}
