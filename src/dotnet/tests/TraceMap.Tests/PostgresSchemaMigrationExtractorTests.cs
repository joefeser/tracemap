using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class PostgresSchemaMigrationExtractorTests
{
    [Fact]
    public void Extract_emits_bounded_table_column_operation_and_file_evidence()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "001_archive.sql"), """
            CREATE TABLE archive.records (
              id bigint PRIMARY KEY,
              status text NOT NULL,
              created_at timestamp(6),
              CONSTRAINT records_status CHECK (status <> '')
            );
            ALTER TABLE archive.records ADD COLUMN IF NOT EXISTS archived_at timestamp;
            """);

        var facts = Extract(temp.Path);

        Assert.Single(facts, fact => fact.FactType == FactTypes.PostgresMigrationFileDeclared);
        var table = Assert.Single(facts, fact => fact.FactType == FactTypes.PostgresSchemaTableDeclared);
        Assert.Equal("archive", table.Properties["schemaName"]);
        Assert.Equal("records", table.Properties["tableName"]);
        Assert.Equal(1, table.Evidence.StartLine);
        Assert.Equal(6, table.Evidence.EndLine);
        Assert.Equal(["archived_at", "created_at", "id", "status"], facts
            .Where(fact => fact.FactType == FactTypes.PostgresSchemaColumnDeclared)
            .Select(fact => fact.Properties["columnName"]).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(2, facts.Count(fact => fact.FactType == FactTypes.PostgresMigrationOperation));
        Assert.All(facts, fact => Assert.True(fact.RuleId is RuleIds.DatabasePostgresSchemaMigration or RuleIds.DatabasePostgresSchemaMigrationGap));
        Assert.All(facts.Where(fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigration), fact => Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier));
        Assert.DoesNotContain(facts, fact => fact.Properties.Values.Any(value => value.Contains("CHECK", StringComparison.Ordinal)));
    }

    [Fact]
    public void Extract_gaps_unsupported_or_incomplete_supported_ddl_without_leaking_text()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "broken.sql"), """
            CREATE TABLE "private_schema"."private_table" ("secret_column" text);
            ALTER TABLE archive.records ADD CONSTRAINT private_constraint CHECK (id > 0);
            CREATE TABLE archive.unfinished (id bigint
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);

        Assert.Equal(3, facts.Count(fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap));
        Assert.DoesNotContain(facts, fact => fact.FactType is FactTypes.PostgresSchemaTableDeclared or FactTypes.PostgresSchemaColumnDeclared);
        Assert.DoesNotContain("private_schema", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_table", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_column", json, StringComparison.Ordinal);
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("classification") == "IncompleteDdlStatement");
        Assert.All(facts.Where(fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap), fact => Assert.NotNull(fact.Evidence.SnippetHash));
    }

    [Fact]
    public void Extract_rejects_multi_subcommand_alter_table_without_partial_column_facts()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "multi.sql"),
            "ALTER TABLE archive.records ADD COLUMN first_value numeric(10, 2), ADD COLUMN second_value text;\n");

        var facts = Extract(temp.Path);

        Assert.DoesNotContain(facts, fact => fact.FactType is FactTypes.PostgresMigrationOperation or FactTypes.PostgresSchemaColumnDeclared);
        var gap = Assert.Single(facts, fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap);
        Assert.Equal("AlterTableMultipleSubcommandsUnsupported", gap.Properties["classification"]);
        Assert.NotNull(gap.Evidence.SnippetHash);
    }

    [Fact]
    public void Extract_emits_named_constraint_evidence_without_constraint_bodies()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "constraints.sql"), """
            CREATE TABLE archive.accounts (
              id bigint,
              tenant_id bigint,
              code text,
              CONSTRAINT accounts_pkey PRIMARY KEY (id),
              CONSTRAINT accounts_tenant_unique UNIQUE (tenant_id, code),
              CONSTRAINT accounts_tenant_fk FOREIGN KEY (tenant_id) REFERENCES archive.tenants (id)
            );
            ALTER TABLE archive.accounts ADD CONSTRAINT accounts_alt_unique UNIQUE (id);
            """);

        var facts = Extract(temp.Path);
        var constraints = facts.Where(fact => fact.FactType == FactTypes.PostgresSchemaConstraintDeclared)
            .OrderBy(fact => fact.Properties["constraintName"], StringComparer.Ordinal).ToArray();

        Assert.Equal(4, constraints.Length);
        Assert.Equal(["accounts_alt_unique", "accounts_pkey", "accounts_tenant_fk", "accounts_tenant_unique"],
            constraints.Select(fact => fact.Properties["constraintName"]).ToArray());
        var foreignKey = Assert.Single(constraints, fact => fact.Properties["constraintKind"] == "foreign-key");
        Assert.Equal("tenant_id", foreignKey.Properties["columnNames"]);
        Assert.Equal("archive", foreignKey.Properties["referencedSchemaName"]);
        Assert.Equal("tenants", foreignKey.Properties["referencedTableName"]);
        Assert.Equal("id", foreignKey.Properties["referencedColumnNames"]);
        Assert.Equal(2, facts.Count(fact => fact.FactType == FactTypes.PostgresMigrationOperation));
        Assert.DoesNotContain("REFERENCES", JsonSerializer.Serialize(facts), StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_emits_simple_index_evidence_with_safe_structural_identity()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "indexes.sql"), """
            CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS records_status_idx
              ON archive.records USING btree (status, created_at DESC);
            CREATE INDEX records_id_idx ON ONLY archive.records (id NULLS LAST);
            """);

        var facts = Extract(temp.Path);
        var indexes = facts.Where(fact => fact.FactType == FactTypes.PostgresSchemaIndexDeclared)
            .OrderBy(fact => fact.Properties["indexName"], StringComparer.Ordinal).ToArray();

        Assert.Equal(2, indexes.Length);
        Assert.Equal("id", indexes[0].Properties["columnNames"]);
        Assert.Equal("non-unique", indexes[0].Properties["indexKind"]);
        Assert.Equal("status,created_at", indexes[1].Properties["columnNames"]);
        Assert.Equal("unique", indexes[1].Properties["indexKind"]);
        Assert.All(indexes, fact =>
        {
            Assert.Equal("archive", fact.Properties["schemaName"]);
            Assert.Equal("records", fact.Properties["tableName"]);
            Assert.Equal("btree", fact.Properties["accessMethod"]);
            Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
        });
        Assert.Equal(2, facts.Count(fact => fact.FactType == FactTypes.PostgresMigrationOperation));
    }

    [Fact]
    public void Extract_gaps_unsafe_or_deferred_constraint_and_index_shapes_without_leaking_identity()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "deferred.sql"), """
            CREATE INDEX "private_index" ON archive.records ((lower(secret_column)));
            CREATE INDEX partial_index ON archive.records (status) WHERE private_tenant = 'sentinel-secret';
            ALTER TABLE archive.records ADD CONSTRAINT private_check CHECK (status <> 'sentinel-secret');
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);

        Assert.Equal(3, facts.Count(fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap));
        Assert.DoesNotContain(facts, fact => fact.FactType is FactTypes.PostgresSchemaConstraintDeclared or FactTypes.PostgresSchemaIndexDeclared);
        Assert.DoesNotContain("private_index", json, StringComparison.Ordinal);
        Assert.DoesNotContain("partial_index", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_tenant", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_check", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-secret", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_labels_mixed_supported_and_unsupported_create_table_clauses_as_reduced()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "mixed.sql"),
            "CREATE TABLE archive.records (visible_column text, \"private_column\" text);\n");

        var facts = Extract(temp.Path);
        var column = Assert.Single(facts, fact => fact.FactType == FactTypes.PostgresSchemaColumnDeclared);
        Assert.Equal("visible_column", column.Properties["columnName"]);
        var gap = Assert.Single(facts, fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap);
        Assert.Equal("CreateTableClauseUnsupported", gap.Properties["classification"]);
        Assert.Equal("reduced-static-evidence", gap.Properties["coverageLabel"]);
        Assert.DoesNotContain("private_column", JsonSerializer.Serialize(facts), StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_keeps_inline_constraint_identity_deferred_and_marks_partial_coverage()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "inline.sql"),
            "CREATE TABLE archive.records (id bigint PRIMARY KEY, parent_id bigint REFERENCES archive.parents(id));\n");

        var facts = Extract(temp.Path);

        Assert.Equal(["id", "parent_id"], facts
            .Where(fact => fact.FactType == FactTypes.PostgresSchemaColumnDeclared)
            .Select(fact => fact.Properties["columnName"]).Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.PostgresSchemaConstraintDeclared);
        var gap = Assert.Single(facts, fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap);
        Assert.Equal("CreateTableClauseUnsupported", gap.Properties["classification"]);
    }

    [Fact]
    public void Rule_catalog_documents_schema_migration_limitations()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        var block = catalog[catalog.IndexOf($"  - id: {RuleIds.DatabasePostgresSchemaMigration}", StringComparison.Ordinal)..];
        block = block[..block.IndexOf("\n  - id:", StringComparison.Ordinal)];
        Assert.Contains("does not prove migration execution", block, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quoted identifiers", block, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE", block, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE", block, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX", block, StringComparison.Ordinal);
        Assert.Contains("referential integrity", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scan_engine_registers_schema_migration_evidence_deterministically()
    {
        using var repo = new TempDirectory();
        using var firstOutput = new TempDirectory();
        using var secondOutput = new TempDirectory();
        File.WriteAllText(Path.Combine(repo.Path, "migration.sql"), """
            CREATE TABLE archive.records (
              id bigint,
              CONSTRAINT records_pkey PRIMARY KEY (id)
            );
            CREATE INDEX records_id_idx ON archive.records (id);
            """);

        var first = ScanEngine.Scan(new ScanOptions(repo.Path, firstOutput.Path)).Facts
            .Where(IsSchemaMigrationFact).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();
        var second = ScanEngine.Scan(new ScanOptions(repo.Path, secondOutput.Path)).Facts
            .Where(IsSchemaMigrationFact).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(first);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.All(first, fact => Assert.Equal("migration.sql", fact.Evidence.FilePath));
        Assert.Contains(first, fact => fact.FactType == FactTypes.PostgresSchemaConstraintDeclared);
        Assert.Contains(first, fact => fact.FactType == FactTypes.PostgresSchemaIndexDeclared);
    }

    private static IReadOnlyList<CodeFact> Extract(string root) => PostgresSchemaMigrationExtractor.Extract(root, new ScanManifest(
        "scan-schema-test", "synthetic-postgres-schema", null, "test", "0123456789abcdef", "test", DateTimeOffset.UnixEpoch,
        "Level3SyntaxAnalysis", "NotRun", [], [], [], []), FileInventory.Collect(root));

    private static bool IsSchemaMigrationFact(CodeFact fact) =>
        fact.RuleId is RuleIds.DatabasePostgresSchemaMigration or RuleIds.DatabasePostgresSchemaMigrationGap;

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
