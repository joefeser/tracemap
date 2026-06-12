using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Storage;

public sealed record IndexExportOptions(
    string IndexPath,
    string OutputPath,
    string Format = "json");

public sealed record IndexExportResult(
    string OutputPath,
    string Format,
    int FactCount,
    int RelationshipCount,
    int CallEdgeCount);

internal sealed record CountRow(string Name, int Count);

internal sealed record RelationshipExportRow(
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

internal sealed record CallEdgeExportRow(
    string? CallerSymbol,
    string CalleeSymbol,
    string? CalleeContainingType,
    string? CallKind,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine);

internal sealed record ObjectCreationExportRow(
    string? CallerSymbol,
    string CreatedType,
    string? ConstructorSymbol,
    string? AssignedTo,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine);

internal sealed record IndexExportDocument(
    string Version,
    DateTimeOffset GeneratedAt,
    ScanManifest Manifest,
    IReadOnlyList<CountRow> FactsByType,
    IReadOnlyList<CountRow> FactsByTier,
    IReadOnlyList<CountRow> FactsByRule,
    IReadOnlyList<RelationshipExportRow> Relationships,
    IReadOnlyList<CallEdgeExportRow> CallEdges,
    IReadOnlyList<ObjectCreationExportRow> ObjectCreations);

public static class IndexExporter
{
    public static async Task<IndexExportResult> WriteAsync(IndexExportOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("Index export requires an index path.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("Index export requires an output path.", nameof(options));
        }

        var format = NormalizeFormat(options.Format);
        await using var connection = new SqliteConnection($"Data Source={options.IndexPath}");
        await connection.OpenAsync(cancellationToken);

        var document = await ReadDocumentAsync(connection, cancellationToken);
        var outputPath = GetOutputPath(options.OutputPath, format);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (format == "json")
        {
            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(document, JsonOptions.Stable) + Environment.NewLine,
                cancellationToken);
        }
        else
        {
            await File.WriteAllTextAsync(outputPath, RenderMermaid(document), cancellationToken);
        }

        return new IndexExportResult(
            outputPath,
            format,
            document.FactsByType.Sum(row => row.Count),
            document.Relationships.Count,
            document.CallEdges.Count);
    }

    private static async Task<IndexExportDocument> ReadDocumentAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return new IndexExportDocument(
            "1.0",
            DateTimeOffset.UtcNow,
            await ReadManifestAsync(connection, cancellationToken),
            await ReadCountsAsync(connection, "fact_type", cancellationToken),
            await ReadCountsAsync(connection, "evidence_tier", cancellationToken),
            await ReadCountsAsync(connection, "rule_id", cancellationToken),
            await ReadRelationshipsAsync(connection, cancellationToken),
            await ReadCallEdgesAsync(connection, cancellationToken),
            await ReadObjectCreationsAsync(connection, cancellationToken));
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

        return JsonSerializer.Deserialize<ScanManifest>(json, JsonOptions.Stable)
            ?? throw new InvalidDataException("TraceMap scan manifest could not be parsed.");
    }

    private static async Task<IReadOnlyList<CountRow>> ReadCountsAsync(
        SqliteConnection connection,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"select {columnName}, count(*) from facts group by {columnName} order by {columnName};";
        var rows = new List<CountRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CountRow(reader.GetString(0), reader.GetInt32(1)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<RelationshipExportRow>> ReadRelationshipsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "symbol_relationships", cancellationToken))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
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
            order by source_display_name, relationship_kind, target_display_name, file_path, start_line;
            """;
        var rows = new List<RelationshipExportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new RelationshipExportRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetInt32(8),
                reader.GetInt32(9)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CallEdgeExportRow>> ReadCallEdgesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "call_edges", cancellationToken))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select caller_symbol,
                   callee_symbol,
                   callee_containing_type,
                   call_kind,
                   rule_id,
                   evidence_tier,
                   file_path,
                   start_line,
                   end_line
            from call_edges
            order by coalesce(caller_symbol, ''), callee_symbol, file_path, start_line;
            """;
        var rows = new List<CallEdgeExportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CallEdgeExportRow(
                reader.IsDBNull(0) ? null : reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetInt32(8)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ObjectCreationExportRow>> ReadObjectCreationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "object_creations", cancellationToken))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select caller_symbol,
                   created_type,
                   constructor_symbol,
                   assigned_to,
                   rule_id,
                   evidence_tier,
                   file_path,
                   start_line,
                   end_line
            from object_creations
            order by coalesce(caller_symbol, ''), created_type, file_path, start_line;
            """;
        var rows = new List<ObjectCreationExportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ObjectCreationExportRow(
                reader.IsDBNull(0) ? null : reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetInt32(8)));
        }

        return rows;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value) > 0;
    }

    private static string RenderMermaid(IndexExportDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("flowchart TD");
        builder.AppendLine($"  %% TraceMap export for {EscapeComment(document.Manifest.RepoName)} @ {EscapeComment(document.Manifest.CommitSha)}");

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in document.Relationships)
        {
            var source = NodeId(ids, row.SourceSymbolId);
            var target = NodeId(ids, row.TargetSymbolId);
            builder.AppendLine($"  {source}[\"{EscapeLabel(row.SourceDisplayName)}\"] -->|{EscapeLabel(row.RelationshipKind)}| {target}[\"{EscapeLabel(row.TargetDisplayName)}\"]");
        }

        foreach (var row in document.CallEdges.Take(500))
        {
            var callerKey = row.CallerSymbol ?? "(unknown caller)";
            var caller = NodeId(ids, callerKey);
            var callee = NodeId(ids, row.CalleeSymbol);
            builder.AppendLine($"  {caller}[\"{EscapeLabel(callerKey)}\"] -.->|calls| {callee}[\"{EscapeLabel(row.CalleeSymbol)}\"]");
        }

        if (document.Relationships.Count == 0 && document.CallEdges.Count == 0)
        {
            builder.AppendLine("  empty[\"No relationship or call-edge rows were exported\"]");
        }

        return builder.ToString();
    }

    private static string NodeId(Dictionary<string, string> ids, string key)
    {
        if (ids.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var next = $"n{ids.Count + 1}";
        ids[key] = next;
        return next;
    }

    private static string NormalizeFormat(string? format)
    {
        var normalized = string.IsNullOrWhiteSpace(format) ? "json" : format.Trim().ToLowerInvariant();
        return normalized switch
        {
            "json" => "json",
            "mermaid" or "mmd" => "mermaid",
            _ => throw new ArgumentException("Export format must be 'json' or 'mermaid'.")
        };
    }

    private static string GetOutputPath(string outputPath, string format)
    {
        var fullPath = Path.GetFullPath(outputPath);
        if (Path.HasExtension(fullPath))
        {
            return fullPath;
        }

        return Path.Combine(fullPath, format == "json" ? "index-export.json" : "relationships.mmd");
    }

    private static string EscapeLabel(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    private static string EscapeComment(string value)
    {
        return value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }
}
