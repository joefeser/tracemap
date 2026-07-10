using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class PostgresArchiveLinkExtractorTests
{
    [Fact]
    public void Extract_classifies_archive_mechanisms_links_declared_context_and_reduces_prerequisites()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "archive.sql", """
            -- tracemap-sql-context: engine=postgresql server=admin database=admin schema=extension mode=manual step=extension-setup capabilities=create-extension stops=verify-active-connection
            CREATE EXTENSION postgres_fdw;
            -- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=fdw-server-setup capabilities=create-server stops=verify-active-connection
            CREATE SERVER fixture_remote FOREIGN DATA WRAPPER postgres_fdw;
            -- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=user-mapping capabilities=create-user-mapping stops=secret-owner-review,verify-active-connection
            CREATE USER MAPPING FOR fixture_role SERVER fixture_remote OPTIONS (password '${FIXTURE_PASSWORD}');
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=schema-import capabilities=import-schema stops=verify-active-connection
            IMPORT FOREIGN SCHEMA public FROM SERVER fixture_remote INTO archive;
            CREATE EXTENSION dblink;
            SELECT dblink('${FIXTURE_DBLINK_CONNECTION}', 'select 1');
            -- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=publication-setup capabilities=create-publication stops=verify-active-connection
            CREATE PUBLICATION fixture_publication FOR TABLE fixture_events;
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=subscription-setup capabilities=create-subscription stops=secret-owner-review,verify-active-connection
            CREATE SUBSCRIPTION fixture_subscription CONNECTION '${FIXTURE_SUBSCRIPTION_CONNECTION}' PUBLICATION fixture_publication;
            CREATE EXTENSION pg_cron;
            SELECT cron.schedule('fixture-job', '0 1 * * *', $$select 1$$);
            """);

        var facts = Extract(temp.Path);
        var surfaces = facts.Where(fact => fact.FactType == FactTypes.DatabaseLinkSurfaceDeclared).ToArray();

        Assert.Equal(10, surfaces.Length);
        Assert.Contains(surfaces, fact => fact.Properties["mechanism"] == "postgres-fdw" && fact.Properties["surfaceKind"] == "schema-import");
        Assert.Contains(surfaces, fact => fact.Properties["mechanism"] == "dblink" && fact.Properties["surfaceKind"] == "call");
        Assert.Contains(surfaces, fact => fact.Properties["mechanism"] == "logical-publication");
        Assert.Contains(surfaces, fact => fact.Properties["mechanism"] == "logical-subscription");
        Assert.Contains(surfaces, fact => fact.Properties["mechanism"] == "pg-cron-scheduled-operation");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.DatabaseLinkEdgeCandidate
            && fact.Properties.GetValueOrDefault("direction") == "source-to-archive");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.DatabasePrerequisiteCandidate
            && fact.Properties.GetValueOrDefault("prerequisiteCode") == "publication-declaration"
            && fact.Properties.GetValueOrDefault("satisfaction") == "established-static-evidence");
        var report = MarkdownReportWriter.Build(new ScanResult(CreateManifest(), facts, FileInventory.Collect(temp.Path)));
        Assert.Contains("### Archive-Link Edges", report);
        Assert.Contains("Supporting facts", report);
        Assert.Contains("source-to-archive", report);
        Assert.All(surfaces, fact =>
        {
            Assert.Equal("span-only", fact.Properties["identityPrecision"]);
            Assert.DoesNotContain(fact.Properties.Keys, key => key.Contains("server", StringComparison.OrdinalIgnoreCase)
                || key.Contains("host", StringComparison.OrdinalIgnoreCase)
                || key.Contains("connection", StringComparison.OrdinalIgnoreCase));
        });
        Assert.All(
            surfaces.Where(fact => fact.Properties["surfaceKind"] is "user-mapping" or "subscription" or "call"),
            fact => Assert.Null(fact.Evidence.SnippetHash));
        Assert.Contains(surfaces, fact => fact.Properties["surfaceKind"] == "extension" && fact.Evidence.SnippetHash is not null);
    }

    [Fact]
    public void Extract_emits_missing_evidence_and_unknown_context_without_runtime_claims()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "partial.sql", "IMPORT FOREIGN SCHEMA public FROM SERVER fixture_remote INTO archive;");

        var facts = Extract(temp.Path);
        var missing = facts.Where(fact => fact.RuleId == RuleIds.DatabasePostgresArchiveLinkGap
            && fact.Properties.GetValueOrDefault("gapKind")?.StartsWith("missing-evidence:", StringComparison.Ordinal) == true).ToArray();

        Assert.Equal(3, missing.Length);
        Assert.Contains(facts, fact => fact.RuleId == RuleIds.DatabasePostgresArchiveLinkGap
            && fact.Properties.GetValueOrDefault("gapKind") == "unknown-context");
        Assert.All(missing, fact => Assert.Contains("does not establish runtime failure", fact.Properties["limitation"], StringComparison.Ordinal));
        Assert.DoesNotContain(facts.SelectMany(fact => fact.Properties.Values), value => value.Contains("missing-at-runtime", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_keeps_conflicting_or_dynamic_direction_unknown()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "conflict.sql", """
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=publication-setup capabilities=create-publication stops=verify-active-connection
            CREATE PUBLICATION fixture_publication FOR TABLE fixture_events;
            -- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=subscription-setup capabilities=create-subscription stops=verify-active-connection
            CREATE SUBSCRIPTION fixture_subscription CONNECTION 'host=' || runtime_host PUBLICATION fixture_publication;
            """);

        var facts = Extract(temp.Path);

        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.DatabaseLinkEdgeCandidate
            && fact.Properties.GetValueOrDefault("direction") != "unknown");
        Assert.Contains(facts, fact => fact.RuleId == RuleIds.DatabasePostgresArchiveLinkGap
            && fact.Properties.GetValueOrDefault("gapKind") == "dynamic-or-unsupported-boundary");
        Assert.Contains(facts, fact => fact.RuleId == RuleIds.DatabasePostgresArchiveLinkGap
            && fact.Properties.GetValueOrDefault("gapKind") == "reduced-evidence:publication-declaration");
    }

    [Fact]
    public void Extract_does_not_use_an_unrelated_link_as_prerequisite_evidence()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "multiple.sql", """
            CREATE EXTENSION postgres_fdw;
            CREATE SERVER fixture_server_a FOREIGN DATA WRAPPER postgres_fdw;
            CREATE USER MAPPING FOR fixture_role SERVER fixture_server_b OPTIONS (password '${FIXTURE_PASSWORD}');
            IMPORT FOREIGN SCHEMA public FROM SERVER fixture_server_a INTO archive;
            CREATE PUBLICATION fixture_publication_a FOR TABLE fixture_events;
            CREATE SUBSCRIPTION fixture_subscription_b CONNECTION '${FIXTURE_CONNECTION}' PUBLICATION fixture_publication_b;
            """);

        var facts = Extract(temp.Path);

        Assert.Contains(facts, fact => fact.FactType == FactTypes.DatabasePrerequisiteCandidate
            && fact.Properties.GetValueOrDefault("prerequisiteCode") == "user-mapping-declaration"
            && fact.Properties.GetValueOrDefault("satisfaction") == "missing-evidence");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.DatabasePrerequisiteCandidate
            && fact.Properties.GetValueOrDefault("prerequisiteCode") == "publication-declaration"
            && fact.Properties.GetValueOrDefault("satisfaction") == "missing-evidence");
    }

    [Fact]
    public void Extract_is_deterministic_and_report_states_non_claims()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "z-cron.sql", "CREATE EXTENSION pg_cron; SELECT cron.schedule('fixture', '0 1 * * *', $$select 1$$);");
        WriteSql(temp.Path, "a-dblink.sql", "CREATE EXTENSION dblink; SELECT dblink('${FIXTURE_CONNECTION}', 'select 1');");

        var first = Extract(temp.Path);
        var second = Extract(temp.Path);
        var report = MarkdownReportWriter.Build(new ScanResult(CreateManifest(), first, FileInventory.Collect(temp.Path)));

        Assert.Equal(first.Select(fact => fact.FactId), second.Select(fact => fact.FactId));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Contains("## PostgreSQL Archive-Link Evidence", report);
        Assert.Contains("### Archive-Link Edges", report);
        Assert.Contains("None established", report);
        Assert.Contains("do not prove connectivity", report);
        Assert.Contains("replication health", report);
        Assert.Contains("reduced-static-evidence", report);
        Assert.DoesNotContain("FIXTURE_CONNECTION", report, StringComparison.Ordinal);
        Assert.DoesNotContain("cron.schedule", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_persists_safe_archive_rows_without_connection_or_scheduled_values()
    {
        using var temp = new TempDirectory();
        var outputPath = Path.Combine(temp.Path, "out");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var fixture = Path.Combine(FindRepoRoot(), "samples", "postgres-archive-link");

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", fixture, "--out", outputPath], output, error);

        Assert.Equal(0, exitCode);
        var serialized = string.Join("\n", Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("DatabaseLinkSurfaceDeclared", serialized, StringComparison.Ordinal);
        Assert.Contains("PostgreSQL Archive-Link Evidence", serialized, StringComparison.Ordinal);
        foreach (var unsafeValue in new[] { "FIXTURE_REMOTE_PASSWORD", "FIXTURE_DBLINK_CONNECTION", "FIXTURE_SUBSCRIPTION_CONNECTION", "select fixture_archive_work" })
        {
            Assert.DoesNotContain(unsafeValue, serialized, StringComparison.Ordinal);
        }

        using var connection = new SqliteConnection($"Data Source={Path.Combine(outputPath, "index.sqlite")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where fact_type = 'DatabaseLinkSurfaceDeclared'";
        Assert.True(Convert.ToInt32(command.ExecuteScalar()) >= 8);
    }

    [Fact]
    public void Rule_catalog_documents_archive_link_rules_and_limitations()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var rule in new[]
                 {
                     RuleIds.DatabasePostgresArchiveLink,
                     RuleIds.DatabasePostgresArchiveLinkPrerequisite,
                     RuleIds.DatabasePostgresArchiveLinkGap
                 })
        {
            Assert.Contains($"- id: {rule}", catalog, StringComparison.Ordinal);
        }
        Assert.Contains("does not prove connectivity", catalog, StringComparison.Ordinal);
        Assert.Contains("Missing-evidence does not mean", catalog, StringComparison.Ordinal);
    }

    private static IReadOnlyList<CodeFact> Extract(string repoPath) =>
        SqlExecutionContextExtractor.Extract(repoPath, CreateManifest(), FileInventory.Collect(repoPath));

    private static void WriteSql(string directory, string name, string text)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), text);
    }

    private static ScanManifest CreateManifest() => new(
        "scan-postgres-archive-test", "synthetic-postgres-archive", null, "test", "0123456789abcdef", "test",
        DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "rules", "rule-catalog.yml"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("Unable to find TraceMap repo root.");
    }
}
