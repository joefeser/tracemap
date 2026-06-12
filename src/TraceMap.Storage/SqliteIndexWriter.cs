using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Storage;

public static class SqliteIndexWriter
{
    private sealed record SymbolRoleProperties(
        string Role,
        string SymbolId,
        string Language,
        string SymbolKind,
        string DisplayName,
        string? AssemblyName,
        string? AssemblyVersion,
        string? ContainingSymbolId);

    public static void Write(string path, ScanManifest manifest, IEnumerable<CodeFact> facts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        CreateSchema(connection);
        InsertManifest(connection, manifest);

        using var transaction = connection.BeginTransaction();
        foreach (var fact in facts)
        {
            InsertFact(connection, transaction, fact);
        }

        InsertParameterForwardEdges(connection, transaction);
        transaction.Commit();
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            create table scan_manifest (
              scan_id text primary key,
              repo text not null,
              commit_sha text not null,
              scanner_version text not null,
              scanned_at text not null,
              analysis_level text not null,
              build_status text not null,
              manifest_json text not null
            );

            create table facts (
              fact_id text primary key,
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
              properties_json text not null
            );

            create table symbols (
              scan_id text not null,
              symbol_id text not null,
              language text not null,
              symbol_kind text not null,
              display_name text not null,
              assembly_name text,
              assembly_version text,
              containing_symbol_id text,
              primary key (scan_id, symbol_id)
            );

            create table symbol_occurrences (
              occurrence_id text primary key,
              scan_id text not null,
              symbol_id text not null,
              fact_id text not null,
              role text not null,
              occurrence_kind text not null,
              evidence_tier text not null,
              rule_id text not null,
              file_path text not null,
              start_line integer not null,
              end_line integer not null
            );

            create table fact_symbols (
              fact_id text not null,
              scan_id text not null,
              symbol_id text not null,
              role text not null,
              primary key (fact_id, symbol_id, role)
            );

            create table symbol_relationships (
              relationship_id text primary key,
              scan_id text not null,
              source_symbol_id text not null,
              target_symbol_id text not null,
              relationship_kind text not null,
              rule_id text not null,
              evidence_tier text not null,
              file_path text not null,
              start_line integer not null,
              end_line integer not null
            );

            create table call_edges (
              fact_id text primary key,
              scan_id text not null,
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

            create table object_creations (
              fact_id text primary key,
              scan_id text not null,
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

            create table argument_flows (
              fact_id text primary key,
              scan_id text not null,
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

            create table local_aliases (
              fact_id text primary key,
              scan_id text not null,
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

            create table field_aliases (
              fact_id text primary key,
              scan_id text not null,
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

            create table parameter_forward_edges (
              fact_id text primary key,
              scan_id text not null,
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

            create index ix_facts_type on facts(fact_type);
            create index ix_facts_rule on facts(rule_id);
            create index ix_facts_target_symbol on facts(target_symbol);
            create index ix_facts_contract_element on facts(contract_element);
            create index ix_facts_file on facts(file_path);
            create index ix_symbols_display on symbols(display_name);
            create index ix_symbols_kind on symbols(symbol_kind);
            create index ix_symbols_assembly on symbols(assembly_name, display_name);
            create index ix_symbol_occurrences_symbol on symbol_occurrences(scan_id, symbol_id);
            create index ix_symbol_occurrences_file on symbol_occurrences(file_path, start_line);
            create index ix_fact_symbols_symbol on fact_symbols(scan_id, symbol_id);
            create index ix_symbol_relationships_source on symbol_relationships(scan_id, source_symbol_id);
            create index ix_symbol_relationships_target on symbol_relationships(scan_id, target_symbol_id);
            create index ix_call_edges_caller on call_edges(caller_symbol);
            create index ix_call_edges_callee on call_edges(callee_symbol);
            create index ix_call_edges_callee_assembly on call_edges(callee_assembly_name, callee_symbol);
            create index ix_call_edges_file on call_edges(file_path);
            create index ix_object_creations_type on object_creations(created_type);
            create index ix_object_creations_assembly on object_creations(created_type_assembly_name, created_type);
            create index ix_object_creations_caller on object_creations(caller_symbol);
            create index ix_argument_flows_callee on argument_flows(callee_symbol);
            create index ix_argument_flows_parameter on argument_flows(parameter_name, parameter_type);
            create index ix_argument_flows_argument_symbol on argument_flows(argument_symbol);
            create index ix_argument_flows_argument_source on argument_flows(argument_source_file, argument_source_start_line);
            create index ix_local_aliases_alias on local_aliases(containing_symbol, alias_symbol, start_line);
            create index ix_local_aliases_origin on local_aliases(origin_symbol);
            create index ix_field_aliases_field on field_aliases(containing_symbol, field_symbol, start_line);
            create index ix_field_aliases_origin on field_aliases(origin_symbol);
            create index ix_parameter_forward_edges_source on parameter_forward_edges(source_node_key);
            create index ix_parameter_forward_edges_target on parameter_forward_edges(target_node_key);
            create index ix_parameter_forward_edges_source_method on parameter_forward_edges(source_method_symbol);
            create index ix_parameter_forward_edges_target_method on parameter_forward_edges(target_method_symbol);
            """;
        command.ExecuteNonQuery();
    }

    private static void InsertManifest(SqliteConnection connection, ScanManifest manifest)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            insert into scan_manifest (
              scan_id,
              repo,
              commit_sha,
              scanner_version,
              scanned_at,
              analysis_level,
              build_status,
              manifest_json
            ) values (
              $scan_id,
              $repo,
              $commit_sha,
              $scanner_version,
              $scanned_at,
              $analysis_level,
              $build_status,
              $manifest_json
            );
            """;
        command.Parameters.AddWithValue("$scan_id", manifest.ScanId);
        command.Parameters.AddWithValue("$repo", manifest.RepoName);
        command.Parameters.AddWithValue("$commit_sha", manifest.CommitSha);
        command.Parameters.AddWithValue("$scanner_version", manifest.ScannerVersion);
        command.Parameters.AddWithValue("$scanned_at", manifest.ScannedAt.ToString("O"));
        command.Parameters.AddWithValue("$analysis_level", manifest.AnalysisLevel);
        command.Parameters.AddWithValue("$build_status", manifest.BuildStatus);
        command.Parameters.AddWithValue("$manifest_json", JsonSerializer.Serialize(manifest, JsonOptions.StableLine));
        command.ExecuteNonQuery();
    }

    private static void InsertFact(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into facts (
              fact_id,
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
              properties_json
            ) values (
              $fact_id,
              $scan_id,
              $repo,
              $commit_sha,
              $project_path,
              $fact_type,
              $rule_id,
              $evidence_tier,
              $source_symbol,
              $target_symbol,
              $contract_element,
              $file_path,
              $start_line,
              $end_line,
              $snippet_hash,
              $properties_json
            );
            """;
        command.Parameters.AddWithValue("$fact_id", fact.FactId);
        command.Parameters.AddWithValue("$scan_id", fact.ScanId);
        command.Parameters.AddWithValue("$repo", fact.Repo);
        command.Parameters.AddWithValue("$commit_sha", fact.CommitSha);
        command.Parameters.AddWithValue("$project_path", (object?)fact.ProjectPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$fact_type", fact.FactType);
        command.Parameters.AddWithValue("$rule_id", fact.RuleId);
        command.Parameters.AddWithValue("$evidence_tier", fact.EvidenceTier);
        command.Parameters.AddWithValue("$source_symbol", (object?)fact.SourceSymbol ?? DBNull.Value);
        command.Parameters.AddWithValue("$target_symbol", (object?)fact.TargetSymbol ?? DBNull.Value);
        command.Parameters.AddWithValue("$contract_element", (object?)fact.ContractElement ?? DBNull.Value);
        command.Parameters.AddWithValue("$file_path", fact.Evidence.FilePath);
        command.Parameters.AddWithValue("$start_line", fact.Evidence.StartLine);
        command.Parameters.AddWithValue("$end_line", fact.Evidence.EndLine);
        command.Parameters.AddWithValue("$snippet_hash", (object?)fact.Evidence.SnippetHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$properties_json", JsonSerializer.Serialize(fact.Properties, JsonOptions.StableLine));
        command.ExecuteNonQuery();

        InsertSymbolRows(connection, transaction, fact);

        if (fact.FactType == FactTypes.CallEdge && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
        {
            InsertCallEdge(connection, transaction, fact);
        }

        if (fact.FactType == FactTypes.ObjectCreated && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
        {
            InsertObjectCreation(connection, transaction, fact);
        }

        if (fact.FactType == FactTypes.LocalAlias && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
        {
            InsertLocalAlias(connection, transaction, fact);
        }

        if (fact.FactType == FactTypes.FieldAlias && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
        {
            InsertFieldAlias(connection, transaction, fact);
        }

        if (fact.FactType == FactTypes.ArgumentPassed && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
        {
            InsertArgumentFlow(connection, transaction, fact);
        }
    }

    private static void InsertSymbolRows(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact)
    {
        foreach (var role in EnumerateSymbolRoles(fact))
        {
            InsertSymbol(connection, transaction, fact, role);
            InsertFactSymbol(connection, transaction, fact, role);
            InsertSymbolOccurrence(connection, transaction, fact, role);
        }
    }

    private static IEnumerable<SymbolRoleProperties> EnumerateSymbolRoles(CodeFact fact)
    {
        foreach (var role in new[] { "source", "target", "argument", "parameter", "origin", "constructor" })
        {
            var symbolId = GetStringProperty(fact, $"{role}SymbolId");
            if (string.IsNullOrWhiteSpace(symbolId))
            {
                continue;
            }

            yield return new SymbolRoleProperties(
                role,
                symbolId,
                GetStringProperty(fact, $"{role}SymbolLanguage") ?? "unknown",
                GetStringProperty(fact, $"{role}SymbolKind") ?? "Unknown",
                GetStringProperty(fact, $"{role}SymbolDisplayName") ?? symbolId,
                GetStringProperty(fact, $"{role}SymbolAssemblyName"),
                GetStringProperty(fact, $"{role}SymbolAssemblyVersion"),
                GetStringProperty(fact, $"{role}ContainingSymbolId"));
        }
    }

    private static void InsertSymbol(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact, SymbolRoleProperties role)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert or ignore into symbols (
              scan_id,
              symbol_id,
              language,
              symbol_kind,
              display_name,
              assembly_name,
              assembly_version,
              containing_symbol_id
            ) values (
              $scan_id,
              $symbol_id,
              $language,
              $symbol_kind,
              $display_name,
              $assembly_name,
              $assembly_version,
              $containing_symbol_id
            );
            """;
        command.Parameters.AddWithValue("$scan_id", fact.ScanId);
        command.Parameters.AddWithValue("$symbol_id", role.SymbolId);
        command.Parameters.AddWithValue("$language", role.Language);
        command.Parameters.AddWithValue("$symbol_kind", role.SymbolKind);
        command.Parameters.AddWithValue("$display_name", role.DisplayName);
        command.Parameters.AddWithValue("$assembly_name", ToDbValue(role.AssemblyName));
        command.Parameters.AddWithValue("$assembly_version", ToDbValue(role.AssemblyVersion));
        command.Parameters.AddWithValue("$containing_symbol_id", ToDbValue(role.ContainingSymbolId));
        command.ExecuteNonQuery();
    }

    private static void InsertFactSymbol(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact, SymbolRoleProperties role)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert or ignore into fact_symbols (
              fact_id,
              scan_id,
              symbol_id,
              role
            ) values (
              $fact_id,
              $scan_id,
              $symbol_id,
              $role
            );
            """;
        command.Parameters.AddWithValue("$fact_id", fact.FactId);
        command.Parameters.AddWithValue("$scan_id", fact.ScanId);
        command.Parameters.AddWithValue("$symbol_id", role.SymbolId);
        command.Parameters.AddWithValue("$role", role.Role);
        command.ExecuteNonQuery();
    }

    private static void InsertSymbolOccurrence(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact, SymbolRoleProperties role)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert or ignore into symbol_occurrences (
              occurrence_id,
              scan_id,
              symbol_id,
              fact_id,
              role,
              occurrence_kind,
              evidence_tier,
              rule_id,
              file_path,
              start_line,
              end_line
            ) values (
              $occurrence_id,
              $scan_id,
              $symbol_id,
              $fact_id,
              $role,
              $occurrence_kind,
              $evidence_tier,
              $rule_id,
              $file_path,
              $start_line,
              $end_line
            );
            """;
        command.Parameters.AddWithValue("$occurrence_id", FactFactory.Hash($"{fact.FactId}|{role.Role}|{role.SymbolId}", 32));
        command.Parameters.AddWithValue("$scan_id", fact.ScanId);
        command.Parameters.AddWithValue("$symbol_id", role.SymbolId);
        command.Parameters.AddWithValue("$fact_id", fact.FactId);
        command.Parameters.AddWithValue("$role", role.Role);
        command.Parameters.AddWithValue("$occurrence_kind", GetOccurrenceKind(fact, role.Role));
        command.Parameters.AddWithValue("$evidence_tier", fact.EvidenceTier);
        command.Parameters.AddWithValue("$rule_id", fact.RuleId);
        command.Parameters.AddWithValue("$file_path", fact.Evidence.FilePath);
        command.Parameters.AddWithValue("$start_line", fact.Evidence.StartLine);
        command.Parameters.AddWithValue("$end_line", fact.Evidence.EndLine);
        command.ExecuteNonQuery();
    }

    private static void InsertCallEdge(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into call_edges (
              fact_id,
              scan_id,
              repo,
              commit_sha,
              evidence_tier,
              rule_id,
              caller_symbol,
              caller_assembly_name,
              caller_assembly_version,
              callee_symbol,
              callee_assembly_name,
              callee_assembly_version,
              callee_containing_type,
              call_kind,
              file_path,
              start_line,
              end_line
            ) values (
              $fact_id,
              $scan_id,
              $repo,
              $commit_sha,
              $evidence_tier,
              $rule_id,
              $caller_symbol,
              $caller_assembly_name,
              $caller_assembly_version,
              $callee_symbol,
              $callee_assembly_name,
              $callee_assembly_version,
              $callee_containing_type,
              $call_kind,
              $file_path,
              $start_line,
              $end_line
            );
            """;
        command.Parameters.AddWithValue("$fact_id", fact.FactId);
        command.Parameters.AddWithValue("$scan_id", fact.ScanId);
        command.Parameters.AddWithValue("$repo", fact.Repo);
        command.Parameters.AddWithValue("$commit_sha", fact.CommitSha);
        command.Parameters.AddWithValue("$evidence_tier", fact.EvidenceTier);
        command.Parameters.AddWithValue("$rule_id", fact.RuleId);
        command.Parameters.AddWithValue("$caller_symbol", (object?)fact.SourceSymbol ?? DBNull.Value);
        command.Parameters.AddWithValue("$caller_assembly_name", GetOptionalProperty(fact, "callerAssemblyName"));
        command.Parameters.AddWithValue("$caller_assembly_version", GetOptionalProperty(fact, "callerAssemblyVersion"));
        command.Parameters.AddWithValue("$callee_symbol", fact.TargetSymbol);
        command.Parameters.AddWithValue("$callee_assembly_name", GetOptionalProperty(fact, "calleeAssemblyName"));
        command.Parameters.AddWithValue("$callee_assembly_version", GetOptionalProperty(fact, "calleeAssemblyVersion"));
        command.Parameters.AddWithValue("$callee_containing_type", GetOptionalProperty(fact, "calleeContainingType"));
        command.Parameters.AddWithValue("$call_kind", GetOptionalProperty(fact, "callKind"));
        command.Parameters.AddWithValue("$file_path", fact.Evidence.FilePath);
        command.Parameters.AddWithValue("$start_line", fact.Evidence.StartLine);
        command.Parameters.AddWithValue("$end_line", fact.Evidence.EndLine);
        command.ExecuteNonQuery();
    }

    private sealed record ArgumentFlowRow(
        string FactId,
        string ScanId,
        string Repo,
        string CommitSha,
        string EvidenceTier,
        string? CallerSymbol,
        string CalleeSymbol,
        string? CalleeAssemblyName,
        string? CalleeAssemblyVersion,
        string ParameterName,
        string? ParameterType,
        string? ArgumentSymbol,
        string? ArgumentSymbolKind,
        string FilePath,
        int StartLine,
        int EndLine);

    private sealed record ResolvedForwardSource(
        string SourceMethodSymbol,
        string SourceParameterSymbol);

    private static void InsertParameterForwardEdges(SqliteConnection connection, SqliteTransaction transaction)
    {
        foreach (var argumentFlow in ReadArgumentFlows(connection, transaction))
        {
            InsertParameterForwardEdge(connection, transaction, argumentFlow);
        }
    }

    private static IReadOnlyList<ArgumentFlowRow> ReadArgumentFlows(SqliteConnection connection, SqliteTransaction transaction)
    {
        var rows = new List<ArgumentFlowRow>();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
              fact_id,
              scan_id,
              repo,
              commit_sha,
              evidence_tier,
              caller_symbol,
              callee_symbol,
              callee_assembly_name,
              callee_assembly_version,
              parameter_name,
              parameter_type,
              argument_symbol,
              argument_symbol_kind,
              file_path,
              start_line,
              end_line
            from argument_flows
            order by fact_id;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ArgumentFlowRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.GetString(13),
                reader.GetInt32(14),
                reader.GetInt32(15)));
        }

        return rows;
    }

    private static void InsertParameterForwardEdge(SqliteConnection connection, SqliteTransaction transaction, ArgumentFlowRow argumentFlow)
    {
        var source = ResolveForwardedSource(
            connection,
            transaction,
            argumentFlow.CallerSymbol,
            argumentFlow.ArgumentSymbolKind,
            argumentFlow.ArgumentSymbol,
            argumentFlow.StartLine);
        var targetMethodSymbol = argumentFlow.CalleeSymbol;
        var targetParameterName = argumentFlow.ParameterName;
        var targetParameterType = argumentFlow.ParameterType;
        if (source is null
            || string.IsNullOrWhiteSpace(targetMethodSymbol)
            || string.IsNullOrWhiteSpace(targetParameterName))
        {
            return;
        }

        var targetParameterSymbol = NormalizeParameterSymbol(CreateParameterSymbol(targetParameterType, targetParameterName));
        if (string.IsNullOrWhiteSpace(targetParameterSymbol))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into parameter_forward_edges (
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
            ) values (
              $fact_id,
              $scan_id,
              $repo,
              $commit_sha,
              $evidence_tier,
              $rule_id,
              $source_method_symbol,
              $source_parameter_symbol,
              $source_node_key,
              $target_method_symbol,
              $target_parameter_name,
              $target_parameter_type,
              $target_parameter_symbol,
              $target_node_key,
              $target_assembly_name,
              $target_assembly_version,
              $file_path,
              $start_line,
              $end_line
            );
            """;
        command.Parameters.AddWithValue("$fact_id", argumentFlow.FactId);
        command.Parameters.AddWithValue("$scan_id", argumentFlow.ScanId);
        command.Parameters.AddWithValue("$repo", argumentFlow.Repo);
        command.Parameters.AddWithValue("$commit_sha", argumentFlow.CommitSha);
        command.Parameters.AddWithValue("$evidence_tier", argumentFlow.EvidenceTier);
        command.Parameters.AddWithValue("$rule_id", RuleIds.CSharpSemanticParameterForwarding);
        command.Parameters.AddWithValue("$source_method_symbol", source.SourceMethodSymbol);
        command.Parameters.AddWithValue("$source_parameter_symbol", source.SourceParameterSymbol);
        command.Parameters.AddWithValue("$source_node_key", CreateNodeKey(source.SourceMethodSymbol, source.SourceParameterSymbol));
        command.Parameters.AddWithValue("$target_method_symbol", targetMethodSymbol);
        command.Parameters.AddWithValue("$target_parameter_name", targetParameterName);
        command.Parameters.AddWithValue("$target_parameter_type", (object?)targetParameterType ?? DBNull.Value);
        command.Parameters.AddWithValue("$target_parameter_symbol", targetParameterSymbol);
        command.Parameters.AddWithValue("$target_node_key", CreateNodeKey(targetMethodSymbol, targetParameterSymbol));
        command.Parameters.AddWithValue("$target_assembly_name", (object?)argumentFlow.CalleeAssemblyName ?? DBNull.Value);
        command.Parameters.AddWithValue("$target_assembly_version", (object?)argumentFlow.CalleeAssemblyVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$file_path", argumentFlow.FilePath);
        command.Parameters.AddWithValue("$start_line", argumentFlow.StartLine);
        command.Parameters.AddWithValue("$end_line", argumentFlow.EndLine);
        command.ExecuteNonQuery();
    }

    private static ResolvedForwardSource? ResolveForwardedSource(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string? callerSymbol,
        string? argumentSymbolKind,
        string? rawArgumentSymbol,
        int callStartLine)
    {
        var argumentSymbol = NormalizeParameterSymbol(rawArgumentSymbol);
        if (string.Equals(argumentSymbolKind, "Parameter", StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(callerSymbol) || string.IsNullOrWhiteSpace(argumentSymbol)
                ? null
                : new ResolvedForwardSource(callerSymbol, argumentSymbol);
        }

        if ((!string.Equals(argumentSymbolKind, "Local", StringComparison.Ordinal)
                && !string.Equals(argumentSymbolKind, "Field", StringComparison.Ordinal))
            || string.IsNullOrWhiteSpace(callerSymbol)
            || string.IsNullOrWhiteSpace(argumentSymbol))
        {
            return null;
        }

        var currentSymbol = argumentSymbol;
        var currentKind = argumentSymbolKind;
        var seen = new HashSet<string>(StringComparer.Ordinal) { $"{currentKind}:{currentSymbol}" };
        for (var depth = 0; depth < 8; depth++)
        {
            var origin = string.Equals(currentKind, "Field", StringComparison.Ordinal)
                ? ReadNearestFieldAliasOrigin(connection, transaction, callerSymbol, currentSymbol, callStartLine)
                : ReadNearestLocalAliasOrigin(connection, transaction, callerSymbol, currentSymbol, callStartLine);
            if (origin is null)
            {
                return string.Equals(currentKind, "Field", StringComparison.Ordinal)
                    ? ResolveConstructorFieldSource(connection, transaction, callerSymbol, currentSymbol)
                    : null;
            }

            var originSymbol = NormalizeParameterSymbol(origin.Value.Symbol);
            if (string.IsNullOrWhiteSpace(originSymbol))
            {
                return null;
            }

            if (string.Equals(origin.Value.Kind, "Parameter", StringComparison.Ordinal))
            {
                return new ResolvedForwardSource(callerSymbol, originSymbol);
            }

            if (!string.Equals(origin.Value.Kind, "Local", StringComparison.Ordinal)
                && !string.Equals(origin.Value.Kind, "Field", StringComparison.Ordinal))
            {
                return null;
            }

            var seenKey = $"{origin.Value.Kind}:{originSymbol}";
            if (!seen.Add(seenKey))
            {
                return null;
            }

            currentSymbol = originSymbol;
            currentKind = origin.Value.Kind;
        }

        return string.Equals(argumentSymbolKind, "Field", StringComparison.Ordinal)
            ? ResolveConstructorFieldSource(connection, transaction, callerSymbol, argumentSymbol)
            : null;
    }

    private static ResolvedForwardSource? ResolveConstructorFieldSource(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string callerSymbol,
        string fieldSymbol)
    {
        var callerContainingType = GetContainingTypeSymbol(callerSymbol);
        if (string.IsNullOrWhiteSpace(callerContainingType))
        {
            return null;
        }

        var candidates = ReadConstructorFieldAliasOrigins(connection, transaction, fieldSymbol)
            .Where(candidate => string.Equals(GetContainingTypeSymbol(candidate.ContainingSymbol), callerContainingType, StringComparison.Ordinal))
            .ToArray();
        if (candidates.Length != 1)
        {
            return null;
        }

        var candidate = candidates[0];
        var originSymbol = NormalizeParameterSymbol(candidate.OriginSymbol);
        return string.IsNullOrWhiteSpace(originSymbol)
            ? null
            : new ResolvedForwardSource(candidate.ContainingSymbol, originSymbol);
    }

    private static IReadOnlyList<(string ContainingSymbol, string OriginSymbol)> ReadConstructorFieldAliasOrigins(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string fieldSymbol)
    {
        var candidates = new List<(string ContainingSymbol, string OriginSymbol)>();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select containing_symbol, origin_symbol
            from field_aliases
            where field_symbol = $field_symbol
              and origin_symbol_kind = 'Parameter'
            order by containing_symbol, start_line, fact_id;
            """;
        command.Parameters.AddWithValue("$field_symbol", fieldSymbol);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            candidates.Add((reader.GetString(0), reader.GetString(1)));
        }

        return candidates
            .Where(candidate => IsConstructorSymbol(candidate.ContainingSymbol))
            .Distinct()
            .ToArray();
    }

    private static (string Symbol, string? Kind)? ReadNearestLocalAliasOrigin(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string containingSymbol,
        string aliasSymbol,
        int callStartLine)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select origin_symbol, origin_symbol_kind
            from local_aliases
            where containing_symbol = $containing_symbol
              and alias_symbol = $alias_symbol
              and start_line <= $call_start_line
            order by start_line desc, fact_id desc
            limit 1;
            """;
        command.Parameters.AddWithValue("$containing_symbol", containingSymbol);
        command.Parameters.AddWithValue("$alias_symbol", aliasSymbol);
        command.Parameters.AddWithValue("$call_start_line", callStartLine);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static (string Symbol, string? Kind)? ReadNearestFieldAliasOrigin(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string containingSymbol,
        string fieldSymbol,
        int callStartLine)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select origin_symbol, origin_symbol_kind
            from field_aliases
            where containing_symbol = $containing_symbol
              and field_symbol = $field_symbol
              and start_line <= $call_start_line
            order by start_line desc, fact_id desc
            limit 1;
            """;
        command.Parameters.AddWithValue("$containing_symbol", containingSymbol);
        command.Parameters.AddWithValue("$field_symbol", fieldSymbol);
        command.Parameters.AddWithValue("$call_start_line", callStartLine);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static void InsertArgumentFlow(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into argument_flows (
              fact_id,
              scan_id,
              repo,
              commit_sha,
              evidence_tier,
              rule_id,
              caller_symbol,
              caller_assembly_name,
              caller_assembly_version,
              callee_symbol,
              callee_assembly_name,
              callee_assembly_version,
              call_kind,
              parameter_ordinal,
              parameter_name,
              parameter_type,
              argument_ordinal,
              argument_expression_kind,
              argument_expression_hash,
              argument_symbol,
              argument_symbol_kind,
              argument_type,
              argument_assembly_name,
              argument_assembly_version,
              argument_source_file,
              argument_source_start_line,
              argument_source_end_line,
              file_path,
              start_line,
              end_line
            ) values (
              $fact_id,
              $scan_id,
              $repo,
              $commit_sha,
              $evidence_tier,
              $rule_id,
              $caller_symbol,
              $caller_assembly_name,
              $caller_assembly_version,
              $callee_symbol,
              $callee_assembly_name,
              $callee_assembly_version,
              $call_kind,
              $parameter_ordinal,
              $parameter_name,
              $parameter_type,
              $argument_ordinal,
              $argument_expression_kind,
              $argument_expression_hash,
              $argument_symbol,
              $argument_symbol_kind,
              $argument_type,
              $argument_assembly_name,
              $argument_assembly_version,
              $argument_source_file,
              $argument_source_start_line,
              $argument_source_end_line,
              $file_path,
              $start_line,
              $end_line
            );
            """;
        command.Parameters.AddWithValue("$fact_id", fact.FactId);
        command.Parameters.AddWithValue("$scan_id", fact.ScanId);
        command.Parameters.AddWithValue("$repo", fact.Repo);
        command.Parameters.AddWithValue("$commit_sha", fact.CommitSha);
        command.Parameters.AddWithValue("$evidence_tier", fact.EvidenceTier);
        command.Parameters.AddWithValue("$rule_id", fact.RuleId);
        command.Parameters.AddWithValue("$caller_symbol", (object?)fact.SourceSymbol ?? DBNull.Value);
        command.Parameters.AddWithValue("$caller_assembly_name", GetOptionalProperty(fact, "callerAssemblyName"));
        command.Parameters.AddWithValue("$caller_assembly_version", GetOptionalProperty(fact, "callerAssemblyVersion"));
        command.Parameters.AddWithValue("$callee_symbol", fact.TargetSymbol);
        command.Parameters.AddWithValue("$callee_assembly_name", GetOptionalProperty(fact, "calleeAssemblyName"));
        command.Parameters.AddWithValue("$callee_assembly_version", GetOptionalProperty(fact, "calleeAssemblyVersion"));
        command.Parameters.AddWithValue("$call_kind", GetOptionalProperty(fact, "callKind"));
        command.Parameters.AddWithValue("$parameter_ordinal", GetRequiredIntProperty(fact, "parameterOrdinal"));
        command.Parameters.AddWithValue("$parameter_name", GetRequiredProperty(fact, "parameterName"));
        command.Parameters.AddWithValue("$parameter_type", GetOptionalProperty(fact, "parameterType"));
        command.Parameters.AddWithValue("$argument_ordinal", GetRequiredIntProperty(fact, "argumentOrdinal"));
        command.Parameters.AddWithValue("$argument_expression_kind", GetOptionalProperty(fact, "argumentExpressionKind"));
        command.Parameters.AddWithValue("$argument_expression_hash", GetOptionalProperty(fact, "argumentExpressionHash"));
        command.Parameters.AddWithValue("$argument_symbol", GetOptionalProperty(fact, "argumentSymbol"));
        command.Parameters.AddWithValue("$argument_symbol_kind", GetOptionalProperty(fact, "argumentSymbolKind"));
        command.Parameters.AddWithValue("$argument_type", GetOptionalProperty(fact, "argumentType"));
        command.Parameters.AddWithValue("$argument_assembly_name", GetOptionalProperty(fact, "argumentAssemblyName"));
        command.Parameters.AddWithValue("$argument_assembly_version", GetOptionalProperty(fact, "argumentAssemblyVersion"));
        command.Parameters.AddWithValue("$argument_source_file", GetOptionalProperty(fact, "argumentSourceFile"));
        command.Parameters.AddWithValue("$argument_source_start_line", GetOptionalIntProperty(fact, "argumentSourceStartLine"));
        command.Parameters.AddWithValue("$argument_source_end_line", GetOptionalIntProperty(fact, "argumentSourceEndLine"));
        command.Parameters.AddWithValue("$file_path", fact.Evidence.FilePath);
        command.Parameters.AddWithValue("$start_line", fact.Evidence.StartLine);
        command.Parameters.AddWithValue("$end_line", fact.Evidence.EndLine);
        command.ExecuteNonQuery();
    }

    private static void InsertLocalAlias(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into local_aliases (
              fact_id,
              scan_id,
              repo,
              commit_sha,
              evidence_tier,
              rule_id,
              containing_symbol,
              alias_symbol,
              alias_symbol_kind,
              alias_type,
              origin_symbol,
              origin_symbol_kind,
              origin_type,
              file_path,
              start_line,
              end_line
            ) values (
              $fact_id,
              $scan_id,
              $repo,
              $commit_sha,
              $evidence_tier,
              $rule_id,
              $containing_symbol,
              $alias_symbol,
              $alias_symbol_kind,
              $alias_type,
              $origin_symbol,
              $origin_symbol_kind,
              $origin_type,
              $file_path,
              $start_line,
              $end_line
            );
            """;
        command.Parameters.AddWithValue("$fact_id", fact.FactId);
        command.Parameters.AddWithValue("$scan_id", fact.ScanId);
        command.Parameters.AddWithValue("$repo", fact.Repo);
        command.Parameters.AddWithValue("$commit_sha", fact.CommitSha);
        command.Parameters.AddWithValue("$evidence_tier", fact.EvidenceTier);
        command.Parameters.AddWithValue("$rule_id", fact.RuleId);
        command.Parameters.AddWithValue("$containing_symbol", (object?)fact.SourceSymbol ?? DBNull.Value);
        command.Parameters.AddWithValue("$alias_symbol", GetRequiredProperty(fact, "aliasSymbol"));
        command.Parameters.AddWithValue("$alias_symbol_kind", GetOptionalProperty(fact, "aliasSymbolKind"));
        command.Parameters.AddWithValue("$alias_type", GetOptionalProperty(fact, "aliasType"));
        command.Parameters.AddWithValue("$origin_symbol", GetRequiredProperty(fact, "originSymbol"));
        command.Parameters.AddWithValue("$origin_symbol_kind", GetOptionalProperty(fact, "originSymbolKind"));
        command.Parameters.AddWithValue("$origin_type", GetOptionalProperty(fact, "originType"));
        command.Parameters.AddWithValue("$file_path", fact.Evidence.FilePath);
        command.Parameters.AddWithValue("$start_line", fact.Evidence.StartLine);
        command.Parameters.AddWithValue("$end_line", fact.Evidence.EndLine);
        command.ExecuteNonQuery();
    }

    private static void InsertFieldAlias(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into field_aliases (
              fact_id,
              scan_id,
              repo,
              commit_sha,
              evidence_tier,
              rule_id,
              containing_symbol,
              field_symbol,
              field_symbol_kind,
              field_type,
              origin_symbol,
              origin_symbol_kind,
              origin_type,
              file_path,
              start_line,
              end_line
            ) values (
              $fact_id,
              $scan_id,
              $repo,
              $commit_sha,
              $evidence_tier,
              $rule_id,
              $containing_symbol,
              $field_symbol,
              $field_symbol_kind,
              $field_type,
              $origin_symbol,
              $origin_symbol_kind,
              $origin_type,
              $file_path,
              $start_line,
              $end_line
            );
            """;
        command.Parameters.AddWithValue("$fact_id", fact.FactId);
        command.Parameters.AddWithValue("$scan_id", fact.ScanId);
        command.Parameters.AddWithValue("$repo", fact.Repo);
        command.Parameters.AddWithValue("$commit_sha", fact.CommitSha);
        command.Parameters.AddWithValue("$evidence_tier", fact.EvidenceTier);
        command.Parameters.AddWithValue("$rule_id", fact.RuleId);
        command.Parameters.AddWithValue("$containing_symbol", (object?)fact.SourceSymbol ?? DBNull.Value);
        command.Parameters.AddWithValue("$field_symbol", GetRequiredProperty(fact, "fieldSymbol"));
        command.Parameters.AddWithValue("$field_symbol_kind", GetOptionalProperty(fact, "fieldSymbolKind"));
        command.Parameters.AddWithValue("$field_type", GetOptionalProperty(fact, "fieldType"));
        command.Parameters.AddWithValue("$origin_symbol", GetRequiredProperty(fact, "originSymbol"));
        command.Parameters.AddWithValue("$origin_symbol_kind", GetOptionalProperty(fact, "originSymbolKind"));
        command.Parameters.AddWithValue("$origin_type", GetOptionalProperty(fact, "originType"));
        command.Parameters.AddWithValue("$file_path", fact.Evidence.FilePath);
        command.Parameters.AddWithValue("$start_line", fact.Evidence.StartLine);
        command.Parameters.AddWithValue("$end_line", fact.Evidence.EndLine);
        command.ExecuteNonQuery();
    }

    private static void InsertObjectCreation(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into object_creations (
              fact_id,
              scan_id,
              repo,
              commit_sha,
              evidence_tier,
              rule_id,
              caller_symbol,
              caller_assembly_name,
              caller_assembly_version,
              created_type,
              created_type_assembly_name,
              created_type_assembly_version,
              constructor_symbol,
              assigned_to,
              file_path,
              start_line,
              end_line
            ) values (
              $fact_id,
              $scan_id,
              $repo,
              $commit_sha,
              $evidence_tier,
              $rule_id,
              $caller_symbol,
              $caller_assembly_name,
              $caller_assembly_version,
              $created_type,
              $created_type_assembly_name,
              $created_type_assembly_version,
              $constructor_symbol,
              $assigned_to,
              $file_path,
              $start_line,
              $end_line
            );
            """;
        command.Parameters.AddWithValue("$fact_id", fact.FactId);
        command.Parameters.AddWithValue("$scan_id", fact.ScanId);
        command.Parameters.AddWithValue("$repo", fact.Repo);
        command.Parameters.AddWithValue("$commit_sha", fact.CommitSha);
        command.Parameters.AddWithValue("$evidence_tier", fact.EvidenceTier);
        command.Parameters.AddWithValue("$rule_id", fact.RuleId);
        command.Parameters.AddWithValue("$caller_symbol", (object?)fact.SourceSymbol ?? DBNull.Value);
        command.Parameters.AddWithValue("$caller_assembly_name", GetOptionalProperty(fact, "callerAssemblyName"));
        command.Parameters.AddWithValue("$caller_assembly_version", GetOptionalProperty(fact, "callerAssemblyVersion"));
        command.Parameters.AddWithValue("$created_type", fact.TargetSymbol);
        command.Parameters.AddWithValue("$created_type_assembly_name", GetOptionalProperty(fact, "calleeAssemblyName"));
        command.Parameters.AddWithValue("$created_type_assembly_version", GetOptionalProperty(fact, "calleeAssemblyVersion"));
        command.Parameters.AddWithValue("$constructor_symbol", GetOptionalProperty(fact, "constructorSymbol"));
        command.Parameters.AddWithValue("$assigned_to", GetOptionalProperty(fact, "assignedTo"));
        command.Parameters.AddWithValue("$file_path", fact.Evidence.FilePath);
        command.Parameters.AddWithValue("$start_line", fact.Evidence.StartLine);
        command.Parameters.AddWithValue("$end_line", fact.Evidence.EndLine);
        command.ExecuteNonQuery();
    }

    private static object GetOptionalProperty(CodeFact fact, string key)
    {
        return fact.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : DBNull.Value;
    }

    private static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static string? GetStringProperty(CodeFact fact, string key)
    {
        return fact.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string GetOccurrenceKind(CodeFact fact, string role)
    {
        if (role == "target" && fact.FactType is FactTypes.TypeDeclared or FactTypes.FieldDeclared or FactTypes.ParameterDeclared)
        {
            return "Definition";
        }

        return role switch
        {
            "source" => "Enclosing",
            "argument" => "ValueReference",
            "parameter" => "ParameterReference",
            "origin" => "ProvenanceReference",
            "constructor" => "ConstructorReference",
            _ => "Reference"
        };
    }

    private static string? GetOptionalStringProperty(CodeFact fact, string key)
    {
        return fact.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static object GetOptionalIntProperty(CodeFact fact, string key)
    {
        return fact.Properties.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : DBNull.Value;
    }

    private static string GetRequiredProperty(CodeFact fact, string key)
    {
        return fact.Properties.TryGetValue(key, out var value)
            ? value
            : string.Empty;
    }

    private static int GetRequiredIntProperty(CodeFact fact, string key)
    {
        return fact.Properties.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : 0;
    }

    private static string CreateParameterSymbol(string? parameterType, string parameterName)
    {
        return string.IsNullOrWhiteSpace(parameterType)
            ? parameterName
            : $"{parameterType} {parameterName}";
    }

    private static string? NormalizeParameterSymbol(string? parameterSymbol)
    {
        if (string.IsNullOrWhiteSpace(parameterSymbol))
        {
            return null;
        }

        var defaultValueIndex = parameterSymbol.IndexOf(" = ", StringComparison.Ordinal);
        return defaultValueIndex < 0
            ? parameterSymbol
            : parameterSymbol[..defaultValueIndex];
    }

    private static string CreateNodeKey(string methodSymbol, string parameterSymbol)
    {
        return $"{methodSymbol}::{parameterSymbol}";
    }

    private static bool IsConstructorSymbol(string symbol)
    {
        var memberName = GetMemberName(symbol);
        var containingType = GetContainingTypeSymbol(symbol);
        var typeName = GetMemberName(containingType);
        return !string.IsNullOrWhiteSpace(memberName)
            && !string.IsNullOrWhiteSpace(typeName)
            && string.Equals(memberName, typeName, StringComparison.Ordinal);
    }

    private static string? GetContainingTypeSymbol(string? memberSymbol)
    {
        if (string.IsNullOrWhiteSpace(memberSymbol))
        {
            return null;
        }

        var openParenIndex = memberSymbol.IndexOf('(');
        var withoutParameters = openParenIndex < 0 ? memberSymbol : memberSymbol[..openParenIndex];
        var lastDotIndex = withoutParameters.LastIndexOf('.');
        return lastDotIndex <= 0 ? null : withoutParameters[..lastDotIndex];
    }

    private static string? GetMemberName(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var openParenIndex = symbol.IndexOf('(');
        var withoutParameters = openParenIndex < 0 ? symbol : symbol[..openParenIndex];
        var lastDotIndex = withoutParameters.LastIndexOf('.');
        return lastDotIndex < 0 ? withoutParameters : withoutParameters[(lastDotIndex + 1)..];
    }
}
