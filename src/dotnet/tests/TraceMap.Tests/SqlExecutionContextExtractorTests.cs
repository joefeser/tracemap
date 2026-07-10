using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class SqlExecutionContextExtractorTests
{
    [Fact]
    public void Extract_classifies_postgresql_steps_and_emits_missing_context_gaps()
    {
        using var temp = new TempDirectory();
        var sql = """
            CREATE EXTENSION postgres_fdw;
            CREATE SERVER archive_source FOREIGN DATA WRAPPER postgres_fdw;
            CREATE USER MAPPING FOR app_user SERVER archive_source OPTIONS (user 'u', password 'p');
            IMPORT FOREIGN SCHEMA public FROM SERVER archive_source INTO archive;
            CREATE FOREIGN TABLE archive.orders (id bigint) SERVER archive_source;
            GRANT USAGE ON FOREIGN SERVER archive_source TO app_user;
            CREATE PUBLICATION archive_publication FOR TABLE orders;
            CREATE SUBSCRIPTION archive_subscription CONNECTION 'host=sentinel.invalid password=hidden' PUBLICATION archive_publication;
            SELECT cron.schedule('nightly', '0 1 * * *', $$DELETE FROM archive.orders$$);
            SELECT count(*) FROM archive.orders;
            DROP TABLE archive.old_orders;
            VACUUM archive.orders;
            """;
        WriteSql(temp.Path, "setup.sql", sql);

        var facts = Extract(temp.Path);
        var contexts = facts.Where(fact => fact.FactType == FactTypes.SqlExecutionContextCandidate).ToArray();

        Assert.Equal(12, contexts.Length);
        Assert.Equal(
            [
                "extension-setup", "fdw-server-setup", "user-mapping", "schema-import",
                "foreign-table-setup", "grant-permission", "publication-setup",
                "subscription-setup", "scheduled-job", "validation-query",
                "destructive-operation", "unknown-sql-step"
            ],
            contexts.Select(fact => fact.Properties["stepKind"]).ToArray());
        Assert.Contains(facts, fact => IsGap(fact, "missing-context-evidence"));
        Assert.Contains(facts, fact => IsGap(fact, "unknown-sql-step"));
        Assert.All(contexts.Take(11), fact => Assert.Equal("postgresql", fact.Properties["engineFamily"]));
        Assert.Equal("unknown", contexts[^1].Properties["engineFamily"]);
        Assert.DoesNotContain(facts.SelectMany(fact => fact.Properties.Values), value => value.Contains("sentinel.invalid", StringComparison.Ordinal));
        Assert.DoesNotContain(facts.SelectMany(fact => fact.Properties.Values), value => value.Contains("DELETE FROM", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_uses_sidecar_over_directive_and_reports_conflicts_without_values()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "setup.sql", """
            -- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=extension-setup capabilities=create-extension stops=verify-active-connection
            CREATE EXTENSION postgres_fdw;
            """);
        File.WriteAllText(Path.Combine(temp.Path, "setup.sql" + SqlExecutionContextExtractor.SidecarSuffix), """
            {
              "schemaVersion": "sql-execution-context/v1",
              "steps": [
                {
                  "statementOrdinal": 1,
                  "engineFamily": "postgresql",
                  "serverRole": "admin",
                  "databaseRole": "admin",
                  "schemaRole": "extension",
                  "executionMode": "manual",
                  "stepKind": "extension-setup",
                  "requiredCapabilities": ["create-extension"],
                  "stopConditions": ["verify-active-connection"]
                }
              ]
            }
            """);

        var facts = Extract(temp.Path);
        var declared = Assert.Single(facts, fact => fact.FactType == FactTypes.SqlExecutionContextDeclared);

        Assert.Equal("sidecar", declared.Properties["declarationSource"]);
        Assert.Equal("admin", declared.Properties["serverRole"]);
        Assert.Equal("conflicting", declared.Properties["contextClassification"]);
        Assert.Contains(facts, fact => IsGap(fact, "conflicting-declarations"));
        Assert.DoesNotContain(facts.SelectMany(fact => fact.Properties.Values), value => value.Contains("source-data", StringComparison.Ordinal) && value.Contains("admin", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_rejects_unknown_sidecar_fields_and_invalid_directives()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "setup.sql", """
            -- tracemap-sql-context: engine=postgresql password=must-not-render
            CREATE EXTENSION postgres_fdw;
            """);
        File.WriteAllText(Path.Combine(temp.Path, "setup.sql" + SqlExecutionContextExtractor.SidecarSuffix), """
            { "schemaVersion": "sql-execution-context/v1", "steps": [], "connectionString": "must-not-render" }
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);

        Assert.Contains(facts, fact => IsGap(fact, "invalid-context-directive"));
        Assert.Contains(facts, fact => IsGap(fact, "invalid-context-sidecar"));
        Assert.DoesNotContain("must-not-render", json, StringComparison.Ordinal);
        Assert.DoesNotContain("connectionString", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_merges_partial_declaration_with_destructive_syntax_candidate()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "setup.sql", """
            -- ordinary banner comment
            -- tracemap-sql-context: engine=postgresql
            DROP TABLE archive.old_records;
            """);

        var facts = Extract(temp.Path);
        var declared = Assert.Single(facts, fact => fact.FactType == FactTypes.SqlExecutionContextDeclared);

        Assert.Equal("destructive-operation", declared.Properties["stepKind"]);
        Assert.Equal("manual", declared.Properties["executionMode"]);
        Assert.Equal("unknown", declared.Properties["serverRole"]);
        Assert.Equal("reduced", declared.Properties["coverage"]);
        Assert.Contains(facts, fact => IsGap(fact, "missing-context-evidence"));
        Assert.Equal(2, declared.Evidence.StartLine);
        Assert.Equal(2, declared.Evidence.EndLine);
    }

    [Fact]
    public void Extract_ignores_directive_like_text_inside_block_comments_strings_and_dollar_bodies()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "setup.sql", """
            /*
            -- tracemap-sql-context: engine=postgresql server=admin database=admin schema=extension mode=manual step=extension-setup
            */
            SELECT '-- tracemap-sql-context: engine=postgresql server=admin';
            SELECT $$-- tracemap-sql-context: engine=postgresql server=admin$$;
            DROP TABLE archive.old_records;
            """);

        var facts = Extract(temp.Path);

        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.SqlExecutionContextDeclared);
        Assert.Contains(facts, fact => fact.FactType == FactTypes.SqlExecutionContextCandidate
            && fact.Properties.GetValueOrDefault("stepKind") == "destructive-operation");
    }

    [Fact]
    public void Extract_preserves_token_boundaries_across_block_comments()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "setup.sql", "CREATE/* disabled note */EXTENSION postgres_fdw;");

        var facts = Extract(temp.Path);
        var candidate = Assert.Single(facts, fact => fact.FactType == FactTypes.SqlExecutionContextCandidate);

        Assert.Equal("extension-setup", candidate.Properties["stepKind"]);
        Assert.Equal("admin", candidate.Properties["databaseRole"]);
    }

    [Fact]
    public void Extract_is_deterministic_for_same_manifest_and_files()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "setup.sql", """
            -- tracemap-sql-context: engine=postgresql server=admin database=admin schema=extension mode=manual step=extension-setup capabilities=create-extension stops=verify-active-connection
            CREATE EXTENSION postgres_fdw;
            SELECT count(*) FROM archive.orders;
            """);

        var first = Extract(temp.Path);
        var second = Extract(temp.Path);

        Assert.Equal(first.Select(fact => fact.FactId), second.Select(fact => fact.FactId));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Report_groups_effective_context_and_marks_transitions_and_manual_verification()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "setup.sql", """
            CREATE EXTENSION postgres_fdw;
            CREATE SERVER archive_source FOREIGN DATA WRAPPER postgres_fdw;
            CREATE PUBLICATION archive_publication FOR TABLE orders;
            SELECT count(*) FROM orders;
            """);
        var manifest = CreateManifest();
        var inventory = FileInventory.Collect(temp.Path);
        var facts = SqlExecutionContextExtractor.Extract(temp.Path, manifest, inventory);

        var report = MarkdownReportWriter.Build(new ScanResult(manifest, facts, inventory));

        Assert.Contains("## SQL Execution Context", report);
        Assert.Contains("context-change", report);
        Assert.Contains("setup.sql:1-1 -> setup.sql:2-2", report);
        Assert.Contains("independently verify the active client connection", report);
        Assert.Contains("does not certify that a step is safe to run", report);
        Assert.Contains("missing-context-evidence", report);
        Assert.DoesNotContain("CREATE EXTENSION", report);
    }

    [Fact]
    public async Task Cli_scan_persists_context_facts_and_safe_report_without_sentinels()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        WriteSql(repo, "setup.sql", """
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=user-mapping capabilities=create-user-mapping stops=secret-owner-review,verify-active-connection
            CREATE USER MAPPING FOR app_user SERVER archive_source OPTIONS (host 'unique-host-sentinel.invalid', user 'fixture_user', password 'unique-password-sentinel');
            SELECT cron.schedule('fixture-job', '0 1 * * *', $$DELETE FROM private_archive.orders WHERE token = 'unique-token-sentinel'$$);
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var factsText = await File.ReadAllTextAsync(Path.Combine(outputPath, "facts.ndjson"));
        var report = await File.ReadAllTextAsync(Path.Combine(outputPath, "report.md"));
        var log = await File.ReadAllTextAsync(Path.Combine(outputPath, "logs", "analyzer.log"));
        Assert.Contains("SqlExecutionContextDeclared", factsText);
        Assert.Contains("scheduled-job", factsText);
        Assert.Contains("SQL Execution Context", report);
        Assert.DoesNotContain("unique-password-sentinel", factsText + report + log, StringComparison.Ordinal);
        Assert.DoesNotContain("unique-token-sentinel", factsText + report + log, StringComparison.Ordinal);
        Assert.DoesNotContain("unique-host-sentinel.invalid", factsText + report + log, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM private_archive", factsText + report + log, StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, factsText + report + log, StringComparison.Ordinal);

        using var connection = new SqliteConnection($"Data Source={Path.Combine(outputPath, "index.sqlite")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where fact_type in ('SqlExecutionContextDeclared', 'SqlExecutionContextCandidate')";
        Assert.True(Convert.ToInt32(command.ExecuteScalar()) >= 2);
        command.CommandText = "select group_concat(properties_json, char(10)) from facts where rule_id like 'database.sql.context.%'";
        var properties = Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        Assert.DoesNotContain("unique-password-sentinel", properties, StringComparison.Ordinal);
        Assert.DoesNotContain("unique-token-sentinel", properties, StringComparison.Ordinal);
        Assert.DoesNotContain("unique-host-sentinel.invalid", properties, StringComparison.Ordinal);
    }

    [Fact]
    public void Rule_catalog_documents_every_sql_context_rule_and_limitation()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));

        foreach (var rule in new[]
                 {
                     RuleIds.DatabaseSqlContextDeclaration,
                     RuleIds.DatabaseSqlContextSyntax,
                     RuleIds.DatabaseSqlContextGap
                 })
        {
            Assert.Contains($"- id: {rule}", catalog, StringComparison.Ordinal);
        }
        Assert.Contains("active connection", catalog, StringComparison.Ordinal);
        Assert.Contains("does not prove", catalog, StringComparison.Ordinal);
        Assert.Contains("AnalysisGap", catalog, StringComparison.Ordinal);
        var syntaxBlock = RuleBlock(catalog, RuleIds.DatabaseSqlContextSyntax);
        Assert.Contains("evidenceTier: Tier2Structural", syntaxBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("evidenceTier: Tier2Structural or", syntaxBlock, StringComparison.Ordinal);
    }

    private static IReadOnlyList<CodeFact> Extract(string repoPath)
    {
        return SqlExecutionContextExtractor.Extract(repoPath, CreateManifest(), FileInventory.Collect(repoPath));
    }

    private static bool IsGap(CodeFact fact, string kind)
    {
        return fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.DatabaseSqlContextGap
            && fact.Properties.GetValueOrDefault("gapKind") == kind;
    }

    private static void WriteSql(string directory, string name, string text)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), text);
    }

    private static ScanManifest CreateManifest()
    {
        return new ScanManifest(
            "scan-sql-context-test",
            "synthetic-sql-context",
            null,
            "test",
            "0123456789abcdef",
            "test",
            DateTimeOffset.UnixEpoch,
            "Level3SyntaxAnalysis",
            "NotRun",
            [],
            [],
            [],
            []);
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "rules", "rule-catalog.yml")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Unable to find TraceMap repo root.");
    }

    private static string RuleBlock(string catalog, string ruleId)
    {
        var start = catalog.IndexOf($"  - id: {ruleId}", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing rule block for {ruleId}.");
        var end = catalog.IndexOf("\n  - id: ", start + 1, StringComparison.Ordinal);
        return end < 0 ? catalog[start..] : catalog[start..end];
    }
}
