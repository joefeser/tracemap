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

internal sealed record CombinedSourceExportRow(
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
    string? ScanRootRelativePath,
    string? ScanRootPathHash,
    string? GitRootHash,
    string AnalysisLevel,
    string BuildStatus);

internal sealed record CombinedFactCountRow(
    string SourceIndexId,
    string Label,
    string FactType,
    int Count);

internal sealed record CombinedCallEdgeExportRow(
    string SourceIndexId,
    string SourceLabel,
    string CombinedFactId,
    string OriginalFactId,
    string? CallerSymbol,
    string CalleeSymbol,
    string? CalleeContainingType,
    string? CallKind,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine);

internal sealed record CombinedRelationshipExportRow(
    string SourceIndexId,
    string SourceLabel,
    string CombinedRelationshipId,
    string OriginalRelationshipId,
    string SourceCombinedSymbolId,
    string SourceDisplayName,
    string TargetCombinedSymbolId,
    string TargetDisplayName,
    string RelationshipKind,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine);

internal sealed record CombinedObjectCreationExportRow(
    string SourceIndexId,
    string SourceLabel,
    string CombinedFactId,
    string OriginalFactId,
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

internal sealed record CombinedIndexExportDocument(
    string Version,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<CombinedSourceExportRow> Sources,
    IReadOnlyList<CountRow> FactsByType,
    IReadOnlyList<CountRow> FactsByTier,
    IReadOnlyList<CountRow> FactsByRule,
    IReadOnlyList<CombinedFactCountRow> FactsBySourceAndType,
    IReadOnlyList<CombinedRelationshipExportRow> Relationships,
    IReadOnlyList<CombinedCallEdgeExportRow> CallEdges,
    IReadOnlyList<CombinedObjectCreationExportRow> ObjectCreations);

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

        if (await IsCombinedIndexAsync(connection, cancellationToken))
        {
            var combinedDocument = await ReadCombinedDocumentAsync(connection, cancellationToken);
            var combinedOutputPath = GetOutputPath(options.OutputPath, format);
            Directory.CreateDirectory(Path.GetDirectoryName(combinedOutputPath)!);
            if (format == "json")
            {
                await File.WriteAllTextAsync(
                    combinedOutputPath,
                    JsonSerializer.Serialize(combinedDocument, JsonOptions.Stable) + Environment.NewLine,
                    cancellationToken);
            }
            else
            {
                await File.WriteAllTextAsync(combinedOutputPath, RenderCombinedMermaid(combinedDocument), cancellationToken);
            }

            return new IndexExportResult(
                combinedOutputPath,
                format,
                combinedDocument.FactsByType.Sum(row => row.Count),
                combinedDocument.Relationships.Count,
                combinedDocument.CallEdges.Count);
        }

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

    private static async Task<CombinedIndexExportDocument> ReadCombinedDocumentAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return new CombinedIndexExportDocument(
            "1.0",
            DateTimeOffset.UtcNow,
            await ReadCombinedSourcesAsync(connection, cancellationToken),
            await ReadCombinedCountsAsync(connection, "fact_type", cancellationToken),
            await ReadCombinedCountsAsync(connection, "evidence_tier", cancellationToken),
            await ReadCombinedCountsAsync(connection, "rule_id", cancellationToken),
            await ReadCombinedFactCountsBySourceAsync(connection, cancellationToken),
            await ReadCombinedRelationshipsAsync(connection, cancellationToken),
            await ReadCombinedCallEdgesAsync(connection, cancellationToken),
            await ReadCombinedObjectCreationsAsync(connection, cancellationToken));
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

    private static async Task<IReadOnlyList<CombinedSourceExportRow>> ReadCombinedSourcesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
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
                   build_status
            from index_sources
            order by label, source_index_id;
            """;
        var rows = new List<CombinedSourceExportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CombinedSourceExportRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.GetString(13),
                reader.GetString(14)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CountRow>> ReadCombinedCountsAsync(
        SqliteConnection connection,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"select {columnName}, count(*) from combined_facts group by {columnName} order by {columnName};";
        var rows = new List<CountRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CountRow(reader.GetString(0), reader.GetInt32(1)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CombinedFactCountRow>> ReadCombinedFactCountsBySourceAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select sources.source_index_id,
                   sources.label,
                   facts.fact_type,
                   count(*)
            from combined_facts facts
            join index_sources sources on sources.source_index_id = facts.source_index_id
            group by sources.source_index_id, sources.label, facts.fact_type
            order by sources.label, facts.fact_type;
            """;
        var rows = new List<CombinedFactCountRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CombinedFactCountRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)));
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

    private static async Task<IReadOnlyList<CombinedCallEdgeExportRow>> ReadCombinedCallEdgesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "combined_call_edges", cancellationToken))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select sources.source_index_id,
                   sources.label,
                   edges.combined_fact_id,
                   edges.original_fact_id,
                   edges.caller_symbol,
                   edges.callee_symbol,
                   edges.callee_containing_type,
                   edges.call_kind,
                   edges.rule_id,
                   edges.evidence_tier,
                   edges.file_path,
                   edges.start_line,
                   edges.end_line
            from combined_call_edges edges
            join index_sources sources on sources.source_index_id = edges.source_index_id
            order by sources.label, coalesce(edges.caller_symbol, ''), edges.callee_symbol, edges.file_path, edges.start_line;
            """;
        var rows = new List<CombinedCallEdgeExportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CombinedCallEdgeExportRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetInt32(11),
                reader.GetInt32(12)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CombinedRelationshipExportRow>> ReadCombinedRelationshipsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "combined_symbol_relationships", cancellationToken))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select sources.source_index_id,
                   sources.label,
                   relationships.combined_relationship_id,
                   relationships.original_relationship_id,
                   relationships.source_combined_symbol_id,
                   coalesce(source_symbols.display_name, relationships.source_symbol_id) as source_display_name,
                   relationships.target_combined_symbol_id,
                   coalesce(target_symbols.display_name, relationships.target_symbol_id) as target_display_name,
                   relationships.relationship_kind,
                   relationships.rule_id,
                   relationships.evidence_tier,
                   relationships.file_path,
                   relationships.start_line,
                   relationships.end_line
            from combined_symbol_relationships relationships
            join index_sources sources on sources.source_index_id = relationships.source_index_id
            left join combined_symbols source_symbols on source_symbols.combined_symbol_id = relationships.source_combined_symbol_id
            left join combined_symbols target_symbols on target_symbols.combined_symbol_id = relationships.target_combined_symbol_id
            order by sources.label, source_display_name, relationships.relationship_kind, target_display_name, relationships.file_path, relationships.start_line;
            """;
        var rows = new List<CombinedRelationshipExportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CombinedRelationshipExportRow(
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
                reader.GetString(11),
                reader.GetInt32(12),
                reader.GetInt32(13)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CombinedObjectCreationExportRow>> ReadCombinedObjectCreationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "combined_object_creations", cancellationToken))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select sources.source_index_id,
                   sources.label,
                   creations.combined_fact_id,
                   creations.original_fact_id,
                   creations.caller_symbol,
                   creations.created_type,
                   creations.constructor_symbol,
                   creations.assigned_to,
                   creations.rule_id,
                   creations.evidence_tier,
                   creations.file_path,
                   creations.start_line,
                   creations.end_line
            from combined_object_creations creations
            join index_sources sources on sources.source_index_id = creations.source_index_id
            order by sources.label, coalesce(creations.caller_symbol, ''), creations.created_type, creations.file_path, creations.start_line;
            """;
        var rows = new List<CombinedObjectCreationExportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CombinedObjectCreationExportRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetInt32(11),
                reader.GetInt32(12)));
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

    private static async Task<bool> IsCombinedIndexAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return await TableExistsAsync(connection, "index_sources", cancellationToken)
            && await TableExistsAsync(connection, "combined_facts", cancellationToken);
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

    private static string RenderCombinedMermaid(CombinedIndexExportDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("flowchart TD");
        builder.AppendLine("  %% TraceMap combined index export");

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in document.Sources)
        {
            var sourceNode = NodeId(ids, $"source:{source.SourceIndexId}");
            builder.AppendLine($"  {sourceNode}[\"{EscapeLabel(source.Label)}\"]");
        }

        foreach (var row in document.CallEdges.Take(500))
        {
            var sourceNode = NodeId(ids, $"source:{row.SourceIndexId}");
            var callerKey = $"{row.SourceLabel}:{row.CallerSymbol ?? "(unknown caller)"}";
            var calleeKey = $"{row.SourceLabel}:{row.CalleeSymbol}";
            var caller = NodeId(ids, callerKey);
            var callee = NodeId(ids, calleeKey);
            builder.AppendLine($"  {sourceNode} --> {caller}[\"{EscapeLabel(row.CallerSymbol ?? "(unknown caller)")}\"]");
            builder.AppendLine($"  {caller} -.->|calls| {callee}[\"{EscapeLabel(row.CalleeSymbol)}\"]");
        }

        foreach (var row in document.Relationships.Take(500))
        {
            var sourceNode = NodeId(ids, $"source:{row.SourceIndexId}");
            var sourceKey = $"{row.SourceLabel}:{row.SourceCombinedSymbolId}";
            var targetKey = $"{row.SourceLabel}:{row.TargetCombinedSymbolId}";
            var source = NodeId(ids, sourceKey);
            var target = NodeId(ids, targetKey);
            builder.AppendLine($"  {sourceNode} --> {source}[\"{EscapeLabel(row.SourceDisplayName)}\"]");
            builder.AppendLine($"  {source} -->|{EscapeLabel(row.RelationshipKind)}| {target}[\"{EscapeLabel(row.TargetDisplayName)}\"]");
        }

        foreach (var row in document.ObjectCreations.Take(500))
        {
            var sourceNode = NodeId(ids, $"source:{row.SourceIndexId}");
            var callerKey = $"{row.SourceLabel}:{row.CallerSymbol ?? "(unknown caller)"}";
            var createdKey = $"{row.SourceLabel}:new:{row.CreatedType}";
            var caller = NodeId(ids, callerKey);
            var created = NodeId(ids, createdKey);
            builder.AppendLine($"  {sourceNode} --> {caller}[\"{EscapeLabel(row.CallerSymbol ?? "(unknown caller)")}\"]");
            builder.AppendLine($"  {caller} -.->|creates| {created}[\"{EscapeLabel(row.CreatedType)}\"]");
        }

        if (document.Sources.Count == 0 && document.CallEdges.Count == 0 && document.ObjectCreations.Count == 0 && document.Relationships.Count == 0)
        {
            builder.AppendLine("  empty[\"No combined rows were exported\"]");
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
