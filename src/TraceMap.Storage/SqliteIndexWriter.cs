using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Storage;

public static class SqliteIndexWriter
{
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

        if (fact.FactType == FactTypes.CallEdge && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
        {
            InsertCallEdge(connection, transaction, fact);
        }

        if (fact.FactType == FactTypes.ObjectCreated && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
        {
            InsertObjectCreation(connection, transaction, fact);
        }

        if (fact.FactType == FactTypes.ArgumentPassed && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
        {
            InsertArgumentFlow(connection, transaction, fact);
        }
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

    private static void InsertParameterForwardEdge(SqliteConnection connection, SqliteTransaction transaction, CodeFact fact)
    {
        if (!string.Equals(GetOptionalStringProperty(fact, "argumentSymbolKind"), "Parameter", StringComparison.Ordinal))
        {
            return;
        }

        var sourceMethodSymbol = fact.SourceSymbol;
        var sourceParameterSymbol = NormalizeParameterSymbol(GetOptionalStringProperty(fact, "argumentSymbol"));
        var targetMethodSymbol = fact.TargetSymbol;
        var targetParameterName = GetRequiredProperty(fact, "parameterName");
        var targetParameterType = GetOptionalStringProperty(fact, "parameterType");
        if (string.IsNullOrWhiteSpace(sourceMethodSymbol)
            || string.IsNullOrWhiteSpace(sourceParameterSymbol)
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
        command.Parameters.AddWithValue("$fact_id", fact.FactId);
        command.Parameters.AddWithValue("$scan_id", fact.ScanId);
        command.Parameters.AddWithValue("$repo", fact.Repo);
        command.Parameters.AddWithValue("$commit_sha", fact.CommitSha);
        command.Parameters.AddWithValue("$evidence_tier", fact.EvidenceTier);
        command.Parameters.AddWithValue("$rule_id", RuleIds.CSharpSemanticParameterForwarding);
        command.Parameters.AddWithValue("$source_method_symbol", sourceMethodSymbol);
        command.Parameters.AddWithValue("$source_parameter_symbol", sourceParameterSymbol);
        command.Parameters.AddWithValue("$source_node_key", CreateNodeKey(sourceMethodSymbol, sourceParameterSymbol));
        command.Parameters.AddWithValue("$target_method_symbol", targetMethodSymbol);
        command.Parameters.AddWithValue("$target_parameter_name", targetParameterName);
        command.Parameters.AddWithValue("$target_parameter_type", (object?)targetParameterType ?? DBNull.Value);
        command.Parameters.AddWithValue("$target_parameter_symbol", targetParameterSymbol);
        command.Parameters.AddWithValue("$target_node_key", CreateNodeKey(targetMethodSymbol, targetParameterSymbol));
        command.Parameters.AddWithValue("$target_assembly_name", GetOptionalProperty(fact, "calleeAssemblyName"));
        command.Parameters.AddWithValue("$target_assembly_version", GetOptionalProperty(fact, "calleeAssemblyVersion"));
        command.Parameters.AddWithValue("$file_path", fact.Evidence.FilePath);
        command.Parameters.AddWithValue("$start_line", fact.Evidence.StartLine);
        command.Parameters.AddWithValue("$end_line", fact.Evidence.EndLine);
        command.ExecuteNonQuery();
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

        InsertParameterForwardEdge(connection, transaction, fact);
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
}
