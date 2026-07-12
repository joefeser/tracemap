using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Combine;

public static class CombinedIndexBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<CombineResult> CombineAsync(CombineOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        var outputPath = Path.GetFullPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        await using var connection = new SqliteConnection($"Data Source={outputPath}");
        await connection.OpenAsync(cancellationToken);
        await CreateSchemaAsync(connection, cancellationToken);

        var sources = new List<CombinedIndexSource>();
        var labels = options.Labels ?? [];
        var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var factCount = 0;
        var symbolCount = 0;
        var relationshipCount = 0;
        var callEdgeCount = 0;

        for (var index = 0; index < options.IndexPaths.Count; index++)
        {
            var indexPath = Path.GetFullPath(options.IndexPaths[index]);
            var alias = $"source_{index}";
            await AttachAsync(connection, alias, indexPath, cancellationToken);
            try
            {
                var (manifest, manifestJson) = await ReadManifestAsync(connection, alias, cancellationToken);
                var label = ResolveLabel(labels, index, manifest);
                if (!usedLabels.Add(label))
                {
                    if (labels.Count > 0)
                    {
                        throw new InvalidOperationException($"Duplicate combine label: {label}");
                    }

                    label = $"{label}-{FactFactory.Hash($"{manifest.ScanId}|{manifest.CommitSha}|{index}", 8)}";
                    if (!usedLabels.Add(label))
                    {
                        label = $"{label}-{index + 1}";
                        if (!usedLabels.Add(label))
                        {
                            throw new InvalidOperationException($"Duplicate combine label: {label}");
                        }
                    }
                }

                var sourceIndexId = FactFactory.Hash($"{label}|{manifest.ScanId}|{manifest.CommitSha}", 24);
                var source = new CombinedIndexSource(
                    sourceIndexId,
                    label,
                    indexPath,
                    FactFactory.Hash(indexPath, 32),
                    manifest.ScanId,
                    manifest.RepoName,
                    manifest.RemoteUrl,
                    manifest.Branch,
                    manifest.CommitSha,
                    manifest.ScannerVersion,
                    InferLanguage(manifest.ScannerVersion),
                    manifest.ScanRootRelativePath,
                    manifest.ScanRootPathHash,
                    manifest.GitRootHash,
                    manifest.AnalysisLevel,
                    manifest.BuildStatus);

                using var transaction = connection.BeginTransaction();
                await InsertSourceAsync(connection, transaction, source, manifestJson, ReadScannedAt(manifestJson, manifest), cancellationToken);
                factCount += await ImportFactsAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                symbolCount += await ImportSymbolsAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                await ImportSymbolOccurrencesAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                await ImportFactSymbolsAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                relationshipCount += await ImportSymbolRelationshipsAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                callEdgeCount += await ImportCallEdgesAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                await ImportObjectCreationsAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                await ImportArgumentFlowsAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                await ImportLocalAliasesAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                await ImportFieldAliasesAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                await ImportParameterForwardEdgesAsync(connection, transaction, alias, sourceIndexId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                sources.Add(source);
            }
            finally
            {
                await DetachAsync(connection, alias, cancellationToken);
            }
        }

        return new CombineResult(outputPath, sources, factCount, symbolCount, relationshipCount, callEdgeCount);
    }

    private static void ValidateOptions(CombineOptions options)
    {
        if (options.IndexPaths.Count == 0)
        {
            throw new ArgumentException("combine requires at least one --index <path>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("combine requires --out <path>.");
        }

        if (options.Labels is { Count: > 0 } && options.Labels.Count != options.IndexPaths.Count)
        {
            throw new ArgumentException("combine requires either no --label values or one --label value per --index.");
        }

        foreach (var label in options.Labels ?? [])
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("combine --label values cannot be empty.");
            }
        }

        var outputPath = Path.GetFullPath(options.OutputPath);
        var seenIndexPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var indexPath in options.IndexPaths)
        {
            var fullIndexPath = Path.GetFullPath(indexPath);
            if (!File.Exists(fullIndexPath))
            {
                throw new FileNotFoundException("TraceMap index does not exist.", indexPath);
            }

            if (!seenIndexPaths.Add(fullIndexPath))
            {
                throw new ArgumentException($"duplicate --index path detected: {fullIndexPath}");
            }

            if (string.Equals(outputPath, fullIndexPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"combine --out must not overwrite an input index: {fullIndexPath}");
            }
        }
    }

    private static async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table index_sources (
              source_index_id text primary key,
              label text not null unique,
              index_path_hash text not null,
              scan_id text not null,
              repo_name text not null,
              remote_url text,
              branch text,
              commit_sha text not null,
              scanner_version text not null,
              language text,
              scan_root_relative_path text,
              scan_root_path_hash text,
              git_root_hash text,
              analysis_level text not null,
              build_status text not null,
              manifest_json text not null,
              imported_at text not null
            );

            create table combined_facts (
              combined_fact_id text primary key,
              source_index_id text not null,
              original_fact_id text not null,
              original_scan_id text not null,
              scan_id text not null,
              repo text not null,
              commit_sha text not null,
              project_path text,
              fact_type text not null,
              rule_id text not null,
              evidence_tier text not null,
              source_symbol text,
              target_symbol text,
              contract_element text,
              file_path text not null,
              start_line integer not null,
              end_line integer not null,
              snippet_hash text,
              extractor_id text,
              extractor_version text,
              properties_json text not null,
              payload_json text not null
            );

            create table combined_symbols (
              combined_symbol_id text primary key,
              source_index_id text not null,
              original_scan_id text not null,
              original_symbol_id text not null,
              language text not null,
              symbol_kind text not null,
              display_name text not null,
              assembly_name text,
              assembly_version text,
              containing_combined_symbol_id text,
              containing_symbol_id text
            );

            create table combined_symbol_occurrences (
              combined_occurrence_id text primary key,
              source_index_id text not null,
              original_scan_id text not null,
              combined_symbol_id text not null,
              original_symbol_id text not null,
              combined_fact_id text not null,
              original_fact_id text not null,
              role text not null,
              occurrence_kind text not null,
              evidence_tier text not null,
              rule_id text not null,
              file_path text not null,
              start_line integer not null,
              end_line integer not null
            );

            create table combined_fact_symbols (
              combined_fact_id text not null,
              combined_symbol_id text not null,
              source_index_id text not null,
              original_fact_id text not null,
              original_symbol_id text not null,
              role text not null,
              primary key (combined_fact_id, combined_symbol_id, role)
            );

            create table combined_symbol_relationships (
              combined_relationship_id text primary key,
              source_index_id text not null,
              original_relationship_id text not null,
              original_scan_id text not null,
              source_combined_symbol_id text not null,
              target_combined_symbol_id text not null,
              source_symbol_id text not null,
              target_symbol_id text not null,
              relationship_kind text not null,
              rule_id text not null,
              evidence_tier text not null,
              file_path text not null,
              start_line integer not null,
              end_line integer not null
            );

            create table combined_call_edges (
              combined_fact_id text primary key,
              source_index_id text not null,
              original_fact_id text not null,
              original_scan_id text not null,
              repo text not null,
              commit_sha text not null,
              evidence_tier text not null,
              rule_id text not null,
              caller_symbol text,
              caller_assembly_name text,
              caller_assembly_version text,
              callee_symbol text not null,
              callee_assembly_name text,
              callee_assembly_version text,
              callee_containing_type text,
              call_kind text,
              file_path text not null,
              start_line integer not null,
              end_line integer not null
            );

            create table combined_object_creations (
              combined_fact_id text primary key,
              source_index_id text not null,
              original_fact_id text not null,
              original_scan_id text not null,
              repo text not null,
              commit_sha text not null,
              evidence_tier text not null,
              rule_id text not null,
              caller_symbol text,
              caller_assembly_name text,
              caller_assembly_version text,
              created_type text not null,
              created_type_assembly_name text,
              created_type_assembly_version text,
              constructor_symbol text,
              assigned_to text,
              file_path text not null,
              start_line integer not null,
              end_line integer not null
            );

            create table combined_argument_flows (
              combined_fact_id text primary key,
              source_index_id text not null,
              original_fact_id text not null,
              original_scan_id text not null,
              repo text not null,
              commit_sha text not null,
              evidence_tier text not null,
              rule_id text not null,
              caller_symbol text,
              caller_assembly_name text,
              caller_assembly_version text,
              callee_symbol text not null,
              callee_assembly_name text,
              callee_assembly_version text,
              call_kind text,
              parameter_ordinal integer not null,
              parameter_name text not null,
              parameter_type text,
              argument_ordinal integer not null,
              argument_expression_kind text,
              argument_expression_hash text,
              argument_symbol text,
              argument_symbol_kind text,
              argument_type text,
              argument_assembly_name text,
              argument_assembly_version text,
              argument_source_file text,
              argument_source_start_line integer,
              argument_source_end_line integer,
              file_path text not null,
              start_line integer not null,
              end_line integer not null
            );

            create table combined_local_aliases (
              combined_fact_id text primary key,
              source_index_id text not null,
              original_fact_id text not null,
              original_scan_id text not null,
              repo text not null,
              commit_sha text not null,
              evidence_tier text not null,
              rule_id text not null,
              containing_symbol text,
              alias_symbol text not null,
              alias_symbol_kind text,
              alias_type text,
              origin_symbol text not null,
              origin_symbol_kind text,
              origin_type text,
              file_path text not null,
              start_line integer not null,
              end_line integer not null
            );

            create table combined_field_aliases (
              combined_fact_id text primary key,
              source_index_id text not null,
              original_fact_id text not null,
              original_scan_id text not null,
              repo text not null,
              commit_sha text not null,
              evidence_tier text not null,
              rule_id text not null,
              containing_symbol text,
              field_symbol text not null,
              field_symbol_kind text,
              field_type text,
              origin_symbol text not null,
              origin_symbol_kind text,
              origin_type text,
              file_path text not null,
              start_line integer not null,
              end_line integer not null
            );

            create table combined_parameter_forward_edges (
              combined_fact_id text primary key,
              source_index_id text not null,
              original_fact_id text not null,
              original_scan_id text not null,
              repo text not null,
              commit_sha text not null,
              evidence_tier text not null,
              rule_id text not null,
              source_method_symbol text not null,
              source_parameter_symbol text not null,
              source_node_key text not null,
              target_method_symbol text not null,
              target_parameter_name text not null,
              target_parameter_type text,
              target_parameter_symbol text not null,
              target_node_key text not null,
              target_assembly_name text,
              target_assembly_version text,
              file_path text not null,
              start_line integer not null,
              end_line integer not null
            );

            create table endpoint_matches (
              endpoint_match_id text primary key,
              client_source_index_id text not null,
              server_source_index_id text not null,
              client_combined_fact_id text,
              server_combined_fact_id text,
              classification text not null,
              http_method text,
              normalized_path_key text,
              static_match_quality text not null,
              evidence_json text not null
            );

            create index ix_index_sources_label on index_sources(label);
            create index ix_combined_facts_source on combined_facts(source_index_id);
            create index ix_combined_facts_type on combined_facts(fact_type);
            create index ix_combined_facts_rule on combined_facts(rule_id);
            create index ix_combined_facts_target_symbol on combined_facts(target_symbol);
            create index ix_combined_facts_contract_element on combined_facts(contract_element);
            create index ix_combined_facts_file on combined_facts(file_path);
            create index ix_combined_symbols_source on combined_symbols(source_index_id);
            create index ix_combined_symbols_display on combined_symbols(display_name);
            create index ix_combined_symbols_assembly on combined_symbols(assembly_name, display_name);
            create index ix_combined_relationships_source_symbol on combined_symbol_relationships(source_combined_symbol_id);
            create index ix_combined_relationships_target_symbol on combined_symbol_relationships(target_combined_symbol_id);
            create index ix_combined_call_edges_caller on combined_call_edges(caller_symbol);
            create index ix_combined_call_edges_callee on combined_call_edges(callee_symbol);
            create index ix_combined_call_edges_callee_assembly on combined_call_edges(callee_assembly_name, callee_symbol);
            create index ix_combined_fact_symbols_source_symbol on combined_fact_symbols(source_index_id, combined_symbol_id);
            create index ix_combined_fact_symbols_source_fact on combined_fact_symbols(source_index_id, combined_fact_id);
            create index ix_combined_argument_flows_source_pair on combined_argument_flows(source_index_id, caller_symbol, callee_symbol);
            create index ix_combined_argument_flows_argument_symbol on combined_argument_flows(argument_symbol);
            create index ix_combined_parameter_forward_edges_source on combined_parameter_forward_edges(source_node_key);
            create index ix_combined_parameter_forward_edges_target on combined_parameter_forward_edges(target_node_key);

            create view combined_dependency_edges as
            select
              sources.source_index_id,
              sources.label as source_label,
              'calls' as edge_kind,
              edges.combined_fact_id as edge_id,
              edges.original_fact_id,
              edges.caller_symbol as source_symbol,
              edges.callee_symbol as target_symbol,
              edges.callee_assembly_name as target_assembly_name,
              edges.callee_assembly_version as target_assembly_version,
              edges.rule_id,
              edges.evidence_tier,
              edges.file_path,
              edges.start_line,
              edges.end_line
            from combined_call_edges edges
            join index_sources sources on sources.source_index_id = edges.source_index_id
            union all
            select
              sources.source_index_id,
              sources.label as source_label,
              'creates' as edge_kind,
              creations.combined_fact_id as edge_id,
              creations.original_fact_id,
              creations.caller_symbol as source_symbol,
              creations.created_type as target_symbol,
              creations.created_type_assembly_name as target_assembly_name,
              creations.created_type_assembly_version as target_assembly_version,
              creations.rule_id,
              creations.evidence_tier,
              creations.file_path,
              creations.start_line,
              creations.end_line
            from combined_object_creations creations
            join index_sources sources on sources.source_index_id = creations.source_index_id
            union all
            select
              sources.source_index_id,
              sources.label as source_label,
              relationships.relationship_kind as edge_kind,
              relationships.combined_relationship_id as edge_id,
              relationships.original_relationship_id as original_fact_id,
              coalesce(source_symbols.display_name, relationships.source_symbol_id) as source_symbol,
              coalesce(target_symbols.display_name, relationships.target_symbol_id) as target_symbol,
              target_symbols.assembly_name as target_assembly_name,
              target_symbols.assembly_version as target_assembly_version,
              relationships.rule_id,
              relationships.evidence_tier,
              relationships.file_path,
              relationships.start_line,
              relationships.end_line
            from combined_symbol_relationships relationships
            join index_sources sources on sources.source_index_id = relationships.source_index_id
            left join combined_symbols source_symbols on source_symbols.combined_symbol_id = relationships.source_combined_symbol_id
            left join combined_symbols target_symbols on target_symbols.combined_symbol_id = relationships.target_combined_symbol_id
            union all
            select
              sources.source_index_id,
              sources.label as source_label,
              'parameter-forward' as edge_kind,
              forwards.combined_fact_id as edge_id,
              forwards.original_fact_id,
              forwards.source_method_symbol || ':' || forwards.source_parameter_symbol as source_symbol,
              forwards.target_method_symbol || ':' || forwards.target_parameter_symbol as target_symbol,
              forwards.target_assembly_name as target_assembly_name,
              forwards.target_assembly_version as target_assembly_version,
              forwards.rule_id,
              forwards.evidence_tier,
              forwards.file_path,
              forwards.start_line,
              forwards.end_line
            from combined_parameter_forward_edges forwards
            join index_sources sources on sources.source_index_id = forwards.source_index_id;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AttachAsync(SqliteConnection connection, string alias, string indexPath, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"attach database $path as {alias};";
        command.Parameters.AddWithValue("$path", indexPath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DetachAsync(SqliteConnection connection, string alias, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"detach database {alias};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(ScanManifest Manifest, string Json)> ReadManifestAsync(SqliteConnection connection, string alias, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"select manifest_json from {alias}.scan_manifest order by scanned_at desc limit 1;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string json)
        {
            throw new InvalidDataException("TraceMap index does not contain a scan manifest.");
        }

        var manifest = JsonSerializer.Deserialize<ScanManifest>(json, JsonOptions)
            ?? throw new InvalidDataException("TraceMap scan manifest could not be parsed.");
        return (manifest, json);
    }

    private static string ResolveLabel(IReadOnlyList<string> labels, int index, ScanManifest manifest)
    {
        if (labels.Count > 0)
        {
            return labels[index];
        }

        if (!string.IsNullOrWhiteSpace(manifest.ScanRootRelativePath))
        {
            var scanRoot = manifest.ScanRootRelativePath.Trim();
            if (scanRoot is not "." && !scanRoot.Equals("./", StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(manifest.RepoName)
                    ? scanRoot
                    : $"{manifest.RepoName}:{scanRoot}";
            }
        }

        if (!string.IsNullOrWhiteSpace(manifest.RepoName))
        {
            return manifest.RepoName;
        }

        return $"index-{index + 1}";
    }

    private static string? InferLanguage(string? scannerVersion)
    {
        if (string.IsNullOrWhiteSpace(scannerVersion))
        {
            return null;
        }

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

        if (scannerVersion.Contains("swift", StringComparison.OrdinalIgnoreCase))
        {
            return "swift";
        }

        if (scannerVersion.Contains("tracemap", StringComparison.OrdinalIgnoreCase))
        {
            return "csharp";
        }

        return null;
    }

    private static string ReadScannedAt(string manifestJson, ScanManifest manifest)
    {
        using var document = JsonDocument.Parse(manifestJson);
        if (document.RootElement.TryGetProperty("scannedAt", out var scannedAt) && scannedAt.ValueKind == JsonValueKind.String)
        {
            var value = scannedAt.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return manifest.ScannedAt.ToString("O");
    }

    private static async Task InsertSourceAsync(SqliteConnection connection, SqliteTransaction transaction, CombinedIndexSource source, string manifestJson, string importedAt, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into index_sources (
              source_index_id, label, index_path_hash, scan_id, repo_name, remote_url, branch,
              commit_sha, scanner_version, language, scan_root_relative_path, scan_root_path_hash,
              git_root_hash, analysis_level, build_status, manifest_json, imported_at
            ) values (
              $source_index_id, $label, $index_path_hash, $scan_id, $repo_name, $remote_url, $branch,
              $commit_sha, $scanner_version, $language, $scan_root_relative_path, $scan_root_path_hash,
              $git_root_hash, $analysis_level, $build_status, $manifest_json, $imported_at
            );
            """;
        command.Parameters.AddWithValue("$source_index_id", source.SourceIndexId);
        command.Parameters.AddWithValue("$label", source.Label);
        command.Parameters.AddWithValue("$index_path_hash", source.IndexPathHash);
        command.Parameters.AddWithValue("$scan_id", source.ScanId);
        command.Parameters.AddWithValue("$repo_name", source.RepoName);
        command.Parameters.AddWithValue("$remote_url", ToDb(source.RemoteUrl));
        command.Parameters.AddWithValue("$branch", ToDb(source.Branch));
        command.Parameters.AddWithValue("$commit_sha", source.CommitSha);
        command.Parameters.AddWithValue("$scanner_version", source.ScannerVersion);
        command.Parameters.AddWithValue("$language", ToDb(source.Language));
        command.Parameters.AddWithValue("$scan_root_relative_path", ToDb(source.ScanRootRelativePath));
        command.Parameters.AddWithValue("$scan_root_path_hash", ToDb(source.ScanRootPathHash));
        command.Parameters.AddWithValue("$git_root_hash", ToDb(source.GitRootHash));
        command.Parameters.AddWithValue("$analysis_level", source.AnalysisLevel);
        command.Parameters.AddWithValue("$build_status", source.BuildStatus);
        command.Parameters.AddWithValue("$manifest_json", manifestJson);
        command.Parameters.AddWithValue("$imported_at", importedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ImportFactsAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        ValidateInternalIdentifier(alias, nameof(alias));
        var hasExtractorId = await ColumnExistsAsync(connection, alias, "facts", "extractor_id", cancellationToken);
        var hasExtractorVersion = await ColumnExistsAsync(connection, alias, "facts", "extractor_version", cancellationToken);
        var extractorIdExpression = hasExtractorId ? "extractor_id" : "null";
        var extractorVersionExpression = hasExtractorVersion ? "extractor_version" : "null";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        const string importFactsSql = """
            insert into combined_facts (
              combined_fact_id, source_index_id, original_fact_id, original_scan_id, scan_id, repo, commit_sha,
              project_path, fact_type, rule_id, evidence_tier, source_symbol, target_symbol, contract_element,
              file_path, start_line, end_line, snippet_hash, extractor_id, extractor_version, properties_json, payload_json
            )
            select
              $source_index_id || ':' || fact_id,
              $source_index_id,
              fact_id,
              scan_id,
              scan_id,
              repo,
              commit_sha,
              project_path,
              fact_type,
              rule_id,
              evidence_tier,
              source_symbol,
              target_symbol,
              contract_element,
              file_path,
              start_line,
              end_line,
              snippet_hash,
              __EXTRACTOR_ID_EXPRESSION__,
              __EXTRACTOR_VERSION_EXPRESSION__,
              properties_json,
              json_object(
                'factId', fact_id,
                'scanId', scan_id,
                'repo', repo,
                'commitSha', commit_sha,
                'factType', fact_type,
                'ruleId', rule_id,
                'evidenceTier', evidence_tier,
                'properties', json(properties_json)
              )
            from __SOURCE_ALIAS__.facts
            order by file_path, start_line, fact_type, fact_id;
            """;
        // SQLite cannot parameterize attached-schema identifiers. Each replacement is either a
        // validated internal identifier or one of the two closed SQL expressions above.
        command.CommandText = importFactsSql
            .Replace("__SOURCE_ALIAS__", alias, StringComparison.Ordinal)
            .Replace("__EXTRACTOR_ID_EXPRESSION__", extractorIdExpression, StringComparison.Ordinal)
            .Replace("__EXTRACTOR_VERSION_EXPRESSION__", extractorVersionExpression, StringComparison.Ordinal); // nosemgrep: csharp.lang.security.sqli.csharp-sqli -- alias is validated; expressions are closed literals.
        command.Parameters.AddWithValue("$source_index_id", sourceIndexId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ImportSymbolsAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, alias, "symbols", cancellationToken))
        {
            return 0;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            insert into combined_symbols (
              combined_symbol_id, source_index_id, original_scan_id, original_symbol_id, language, symbol_kind,
              display_name, assembly_name, assembly_version, containing_combined_symbol_id, containing_symbol_id
            )
            select
              $source_index_id || ':' || symbol_id,
              $source_index_id,
              scan_id,
              symbol_id,
              language,
              symbol_kind,
              display_name,
              assembly_name,
              assembly_version,
              case when containing_symbol_id is null then null else $source_index_id || ':' || containing_symbol_id end,
              containing_symbol_id
            from {alias}.symbols
            order by symbol_id;
            """;
        command.Parameters.AddWithValue("$source_index_id", sourceIndexId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ImportSymbolOccurrencesAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, alias, "symbol_occurrences", cancellationToken))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            insert into combined_symbol_occurrences (
              combined_occurrence_id, source_index_id, original_scan_id, combined_symbol_id, original_symbol_id,
              combined_fact_id, original_fact_id, role, occurrence_kind, evidence_tier, rule_id,
              file_path, start_line, end_line
            )
            select
              $source_index_id || ':' || occurrence_id,
              $source_index_id,
              scan_id,
              $source_index_id || ':' || symbol_id,
              symbol_id,
              $source_index_id || ':' || fact_id,
              fact_id,
              role,
              occurrence_kind,
              evidence_tier,
              rule_id,
              file_path,
              start_line,
              end_line
            from {alias}.symbol_occurrences
            order by file_path, start_line, occurrence_id;
            """;
        command.Parameters.AddWithValue("$source_index_id", sourceIndexId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ImportFactSymbolsAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, alias, "fact_symbols", cancellationToken))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            insert into combined_fact_symbols (
              combined_fact_id, combined_symbol_id, source_index_id, original_fact_id, original_symbol_id, role
            )
            select
              $source_index_id || ':' || fact_id,
              $source_index_id || ':' || symbol_id,
              $source_index_id,
              fact_id,
              symbol_id,
              role
            from {alias}.fact_symbols
            order by fact_id, symbol_id, role;
            """;
        command.Parameters.AddWithValue("$source_index_id", sourceIndexId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ImportSymbolRelationshipsAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, alias, "symbol_relationships", cancellationToken))
        {
            return 0;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            insert into combined_symbol_relationships (
              combined_relationship_id, source_index_id, original_relationship_id, original_scan_id,
              source_combined_symbol_id, target_combined_symbol_id, source_symbol_id, target_symbol_id,
              relationship_kind, rule_id, evidence_tier, file_path, start_line, end_line
            )
            select
              $source_index_id || ':' || relationship_id,
              $source_index_id,
              relationship_id,
              scan_id,
              $source_index_id || ':' || source_symbol_id,
              $source_index_id || ':' || target_symbol_id,
              source_symbol_id,
              target_symbol_id,
              relationship_kind,
              rule_id,
              evidence_tier,
              file_path,
              start_line,
              end_line
            from {alias}.symbol_relationships
            order by relationship_kind, source_symbol_id, target_symbol_id, relationship_id;
            """;
        command.Parameters.AddWithValue("$source_index_id", sourceIndexId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ImportCallEdgesAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        return await ImportDependencyTableAsync(connection, transaction, alias, sourceIndexId, "call_edges", "combined_call_edges", """
            repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version,
            callee_symbol, callee_assembly_name, callee_assembly_version, callee_containing_type, call_kind,
            file_path, start_line, end_line
            """, cancellationToken);
    }

    private static async Task ImportObjectCreationsAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        await ImportDependencyTableAsync(connection, transaction, alias, sourceIndexId, "object_creations", "combined_object_creations", """
            repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version,
            created_type, created_type_assembly_name, created_type_assembly_version, constructor_symbol, assigned_to,
            file_path, start_line, end_line
            """, cancellationToken);
    }

    private static async Task ImportArgumentFlowsAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        await ImportDependencyTableAsync(connection, transaction, alias, sourceIndexId, "argument_flows", "combined_argument_flows", """
            repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version,
            callee_symbol, callee_assembly_name, callee_assembly_version, call_kind, parameter_ordinal, parameter_name,
            parameter_type, argument_ordinal, argument_expression_kind, argument_expression_hash, argument_symbol,
            argument_symbol_kind, argument_type, argument_assembly_name, argument_assembly_version, argument_source_file,
            argument_source_start_line, argument_source_end_line, file_path, start_line, end_line
            """, cancellationToken);
    }

    private static async Task ImportLocalAliasesAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        await ImportDependencyTableAsync(connection, transaction, alias, sourceIndexId, "local_aliases", "combined_local_aliases", """
            repo, commit_sha, evidence_tier, rule_id, containing_symbol, alias_symbol, alias_symbol_kind, alias_type,
            origin_symbol, origin_symbol_kind, origin_type, file_path, start_line, end_line
            """, cancellationToken);
    }

    private static async Task ImportFieldAliasesAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        await ImportDependencyTableAsync(connection, transaction, alias, sourceIndexId, "field_aliases", "combined_field_aliases", """
            repo, commit_sha, evidence_tier, rule_id, containing_symbol, field_symbol, field_symbol_kind, field_type,
            origin_symbol, origin_symbol_kind, origin_type, file_path, start_line, end_line
            """, cancellationToken);
    }

    private static async Task ImportParameterForwardEdgesAsync(SqliteConnection connection, SqliteTransaction transaction, string alias, string sourceIndexId, CancellationToken cancellationToken)
    {
        await ImportDependencyTableAsync(connection, transaction, alias, sourceIndexId, "parameter_forward_edges", "combined_parameter_forward_edges", """
            repo, commit_sha, evidence_tier, rule_id, source_method_symbol, source_parameter_symbol, source_node_key,
            target_method_symbol, target_parameter_name, target_parameter_type, target_parameter_symbol, target_node_key,
            target_assembly_name, target_assembly_version, file_path, start_line, end_line
            """, cancellationToken);
    }

    private static async Task<int> ImportDependencyTableAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string alias,
        string sourceIndexId,
        string sourceTable,
        string targetTable,
        string columns,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, alias, sourceTable, cancellationToken))
        {
            return 0;
        }

        var normalizedColumns = columns.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            insert into {targetTable} (
              combined_fact_id, source_index_id, original_fact_id, original_scan_id, {normalizedColumns}
            )
            select
              $source_index_id || ':' || fact_id,
              $source_index_id,
              fact_id,
              scan_id,
              {normalizedColumns}
            from {alias}.{sourceTable}
            order by file_path, start_line, fact_id;
            """;
        command.Parameters.AddWithValue("$source_index_id", sourceIndexId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string alias, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select 1 from pragma_table_list where schema = $schema_name and name = $table_name limit 1;";
        command.Parameters.AddWithValue("$schema_name", alias);
        command.Parameters.AddWithValue("$table_name", tableName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null;
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string alias, string tableName, string columnName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select 1 from pragma_table_info($table_name, $schema_name) where name = $column_name limit 1;";
        command.Parameters.AddWithValue("$table_name", tableName);
        command.Parameters.AddWithValue("$schema_name", alias);
        command.Parameters.AddWithValue("$column_name", columnName);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static void ValidateInternalIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("SQLite internal identifiers may contain only ASCII letters, digits, and underscores.", parameterName);
        }
    }

    private static object ToDb(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }
}
