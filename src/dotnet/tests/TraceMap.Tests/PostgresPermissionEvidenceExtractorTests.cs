using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class PostgresPermissionEvidenceExtractorTests
{
    [Fact]
    public void Extract_classifies_supported_permission_statements_without_names()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "permissions.sql", """
            GRANT USAGE ON FOREIGN SERVER private_server_sentinel TO private_role_sentinel;
            REVOKE SELECT ON TABLE private_table_sentinel FROM private_role_sentinel;
            ALTER TABLE private_table_sentinel OWNER TO private_owner_sentinel;
            ALTER DEFAULT PRIVILEGES GRANT SELECT ON TABLES TO private_role_sentinel;
            GRANT private_parent_role_sentinel TO private_member_role_sentinel;
            """);

        var facts = Extract(temp.Path);
        var permissions = facts.Where(fact => fact.FactType == FactTypes.DatabasePermissionDeclared).ToArray();
        var serialized = JsonSerializer.Serialize(permissions);

        Assert.Equal(5, permissions.Length);
        Assert.Contains(permissions, fact => fact.Properties["actionKind"] == "grant" && fact.Properties["capabilityCode"] == "usage-foreign-server");
        Assert.Contains(permissions, fact => fact.Properties["actionKind"] == "revoke" && fact.Properties["capabilityCode"] == "table-access");
        Assert.Contains(permissions, fact => fact.Properties["actionKind"] == "owner-change" && fact.Properties["capabilityCode"] == "ownership");
        Assert.Contains(permissions, fact => fact.Properties["actionKind"] == "default-privilege");
        Assert.Contains(permissions, fact => fact.Properties["actionKind"] == "role-membership");
        Assert.DoesNotContain("private_server_sentinel", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private_role_sentinel", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private_table_sentinel", serialized, StringComparison.Ordinal);
        Assert.All(permissions, fact =>
        {
            Assert.Equal(PostgresPermissionEvidenceExtractor.RegistryVersion, fact.Properties["registryVersion"]);
            Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
        });
    }

    [Fact]
    public void Reduce_reports_present_missing_owner_review_and_conflicting_evidence()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "setup.sql", """
            CREATE EXTENSION postgres_fdw;
            CREATE SERVER fixture_server FOREIGN DATA WRAPPER postgres_fdw;
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=grant-permission capabilities=grant-permission stops=verify-active-connection
            GRANT USAGE ON FOREIGN SERVER fixture_server TO fixture_operator;
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=user-mapping capabilities=create-user-mapping stops=secret-owner-review,verify-active-connection
            CREATE USER MAPPING FOR fixture_operator SERVER fixture_server OPTIONS (password '${FIXTURE_PASSWORD}');
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=grant-permission capabilities=grant-permission stops=verify-active-connection
            REVOKE USAGE ON FOREIGN SERVER fixture_server FROM fixture_operator;
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=schema-import capabilities=import-schema stops=verify-active-connection
            IMPORT FOREIGN SCHEMA public FROM SERVER fixture_server INTO archive;
            """);

        var facts = Extract(temp.Path);
        var evidence = facts.Where(fact => fact.FactType == FactTypes.DatabasePrerequisiteEvidence).ToArray();

        Assert.Contains(evidence, fact => fact.Properties["candidateCapability"] == "create-user-mapping"
            && fact.Properties["evidenceStatus"] == "needs-owner-review");
        Assert.Contains(evidence, fact => fact.Properties["candidateCapability"] == "usage-foreign-server"
            && fact.Properties["evidenceStatus"] == "conflicting-evidence"
            && fact.Properties.ContainsKey("supportingFactIds")
            && fact.Properties.ContainsKey("contradictingFactIds"));
        Assert.Contains(evidence, fact => fact.Properties["candidateCapability"] == "create-schema-object"
            && fact.Properties["evidenceStatus"] == "unknown"
            && fact.Properties["reasonCode"] == "operation-identity-unknown");
        Assert.DoesNotContain(evidence, fact => fact.Properties["evidenceStatus"].Contains("effective", StringComparison.Ordinal));
    }

    [Fact]
    public void Reduce_caps_cross_file_and_permission_after_operation_at_owner_review()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "a-operation.sql", """
            -- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=user-mapping capabilities=create-user-mapping stops=verify-active-connection
            CREATE USER MAPPING FOR fixture_operator SERVER fixture_server OPTIONS (password '${FIXTURE_PASSWORD}');
            """);
        WriteSql(temp.Path, "z-permission.sql", "GRANT USAGE ON FOREIGN SERVER fixture_server TO fixture_operator;");

        var facts = Extract(temp.Path);
        var row = Assert.Single(facts, fact => fact.FactType == FactTypes.DatabasePrerequisiteEvidence
            && fact.Properties.GetValueOrDefault("candidateCapability") == "usage-foreign-server");

        Assert.Equal("needs-owner-review", row.Properties["evidenceStatus"]);
        Assert.Equal("cross-file-order-unknown", row.Properties["reasonCode"]);
    }

    [Fact]
    public void Extract_emits_gap_for_unsupported_dynamic_permission_and_is_deterministic()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "dynamic.sql", "GRANT USAGE ON FOREIGN SERVER " + '"' + "${DYNAMIC_SERVER}" + '"' + " TO fixture_operator;");

        var first = Extract(temp.Path);
        var second = Extract(temp.Path);

        Assert.Contains(first, fact => fact.RuleId == RuleIds.DatabasePostgresPermissionGap
            && fact.Properties.GetValueOrDefault("gapKind") == "unsupported-or-dynamic-permission-statement");
        Assert.Equal(first.Select(fact => fact.FactId), second.Select(fact => fact.FactId));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Extract_handles_owner_clauses_and_ignores_non_owner_alter_ddl()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "alter.sql", """
            ALTER TABLE IF EXISTS ONLY fixture_table OWNER TO fixture_owner;
            ALTER TABLE fixture_table ADD COLUMN fixture_value integer;
            """);

        var facts = Extract(temp.Path);
        var owner = Assert.Single(facts, fact => fact.FactType == FactTypes.DatabasePermissionDeclared);

        Assert.Equal(FactFactory.Hash("permission-object:table|FIXTURE_TABLE", 24), owner.Properties["objectIdentity"]);
        Assert.DoesNotContain(facts, fact => fact.RuleId == RuleIds.DatabasePostgresPermissionGap
            && fact.Properties.GetValueOrDefault("gapKind") == "unsupported-or-dynamic-permission-statement");
    }

    [Fact]
    public void Reduce_requires_matching_object_and_role_identities()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "identity.sql", """
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=grant-permission capabilities=grant-permission stops=verify-active-connection
            GRANT USAGE ON FOREIGN SERVER fixture_server TO unrelated_role;
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=user-mapping capabilities=create-user-mapping stops=verify-active-connection
            CREATE USER MAPPING FOR intended_role SERVER fixture_server OPTIONS (password '${FIXTURE_PASSWORD}');
            """);

        var facts = Extract(temp.Path);
        var row = Assert.Single(facts, fact => fact.FactType == FactTypes.DatabasePrerequisiteEvidence
            && fact.Properties.GetValueOrDefault("candidateCapability") == "usage-foreign-server");

        Assert.Equal("missing-evidence", row.Properties["evidenceStatus"]);
        Assert.Equal("no-compatible-permission-statement", row.Properties["reasonCode"]);
    }

    [Fact]
    public void Reduce_treats_grant_all_as_evidence_for_a_specific_prerequisite()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "all.sql", """
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=grant-permission capabilities=grant-permission stops=verify-active-connection
            GRANT ALL PRIVILEGES ON FOREIGN SERVER fixture_server TO fixture_operator;
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=user-mapping capabilities=create-user-mapping stops=secret-owner-review,verify-active-connection
            CREATE USER MAPPING FOR fixture_operator SERVER fixture_server OPTIONS (password '${FIXTURE_PASSWORD}');
            """);

        var facts = Extract(temp.Path);
        var row = Assert.Single(facts, fact => fact.FactType == FactTypes.DatabasePrerequisiteEvidence
            && fact.Properties.GetValueOrDefault("candidateCapability") == "usage-foreign-server");

        Assert.Equal("present-in-scripts", row.Properties["evidenceStatus"]);
        Assert.Equal("compatible-grant-in-scripts", row.Properties["reasonCode"]);
    }

    [Fact]
    public void Extract_does_not_treat_grant_option_only_revoke_as_a_base_privilege_revoke()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "grant-option.sql", """
            GRANT USAGE ON FOREIGN SERVER fixture_server TO fixture_operator;
            REVOKE GRANT OPTION FOR USAGE ON FOREIGN SERVER fixture_server FROM fixture_operator;
            """);

        var facts = Extract(temp.Path);
        var permissions = facts.Where(fact => fact.FactType == FactTypes.DatabasePermissionDeclared).ToArray();

        Assert.Single(permissions);
        Assert.Equal("grant", permissions[0].Properties["actionKind"]);
        Assert.Contains(facts, fact => fact.RuleId == RuleIds.DatabasePostgresPermissionGap
            && fact.Properties.GetValueOrDefault("gapKind") == "unsupported-or-dynamic-permission-statement");
    }

    [Fact]
    public void Reduce_labels_reduced_operation_unknown_instead_of_missing()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "reduced.sql", "CREATE USER MAPPING FOR fixture_role SERVER fixture_server OPTIONS (password '${FIXTURE_PASSWORD}');");

        var facts = Extract(temp.Path);
        var row = Assert.Single(facts, fact => fact.FactType == FactTypes.DatabasePrerequisiteEvidence
            && fact.Properties.GetValueOrDefault("candidateCapability") == "usage-foreign-server");

        Assert.Equal("unknown", row.Properties["evidenceStatus"]);
        Assert.Equal("reduced-operation-evidence", row.Properties["reasonCode"]);
    }

    [Fact]
    public async Task Cli_persists_safe_permission_evidence_and_report_non_claims()
    {
        using var temp = new TempDirectory();
        var outputPath = Path.Combine(temp.Path, "out");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var fixture = Path.Combine(FindRepoRoot(), "samples", "postgres-permission-evidence");

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", fixture, "--out", outputPath], output, error);

        Assert.Equal(0, exitCode);
        var serialized = string.Join("\n", Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("DatabasePermissionDeclared", serialized, StringComparison.Ordinal);
        Assert.Contains("DatabasePrerequisiteEvidence", serialized, StringComparison.Ordinal);
        Assert.Contains("present-in-scripts", serialized, StringComparison.Ordinal);
        Assert.Contains("does not mean a permission is active or sufficient", serialized, StringComparison.Ordinal);
        foreach (var sentinel in new[] { "PRIVATE_ROLE_SENTINEL", "PRIVATE_SERVER_SENTINEL", "PRIVATE_SCHEMA_SENTINEL", "FIXTURE_PASSWORD" })
            Assert.DoesNotContain(sentinel, serialized, StringComparison.Ordinal);

        using var connection = new SqliteConnection($"Data Source={Path.Combine(outputPath, "index.sqlite")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where fact_type = 'DatabasePrerequisiteEvidence'";
        Assert.True(Convert.ToInt32(command.ExecuteScalar()) > 0);
    }

    [Fact]
    public void Report_and_catalog_document_permission_rules_and_limitations()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "permissions.sql", "GRANT USAGE ON FOREIGN SERVER fixture_server TO fixture_role;");
        var facts = Extract(temp.Path);
        var report = MarkdownReportWriter.Build(new ScanResult(CreateManifest(), facts, FileInventory.Collect(temp.Path)));
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));

        Assert.Contains("## PostgreSQL Permission Prerequisite Evidence", report);
        Assert.Contains("does not mean a permission is active or sufficient", report);
        Assert.DoesNotContain("GRANT USAGE", report, StringComparison.Ordinal);
        foreach (var rule in new[] { RuleIds.DatabasePostgresPermissionStatement, RuleIds.DatabasePostgresPermissionPrerequisite, RuleIds.DatabasePostgresPermissionCoverage, RuleIds.DatabasePostgresPermissionGap })
            Assert.Contains($"- id: {rule}", catalog, StringComparison.Ordinal);
    }

    private static IReadOnlyList<CodeFact> Extract(string repoPath) =>
        SqlExecutionContextExtractor.Extract(repoPath, CreateManifest(), FileInventory.Collect(repoPath));

    private static void WriteSql(string directory, string name, string text)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), text);
    }

    private static ScanManifest CreateManifest() => new(
        "scan-postgres-permission-test", "synthetic-postgres-permission", null, "test", "0123456789abcdef", "test",
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
