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
              callee_symbol text not null,
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
            create index ix_call_edges_file on call_edges(file_path);
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
              callee_symbol,
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
              $callee_symbol,
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
        command.Parameters.AddWithValue("$callee_symbol", fact.TargetSymbol);
        command.Parameters.AddWithValue("$file_path", fact.Evidence.FilePath);
        command.Parameters.AddWithValue("$start_line", fact.Evidence.StartLine);
        command.Parameters.AddWithValue("$end_line", fact.Evidence.EndLine);
        command.ExecuteNonQuery();
    }
}
