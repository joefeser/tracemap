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
            ALTER TABLE archive.records ADD CONSTRAINT private_constraint UNIQUE (id);
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
    }

    [Fact]
    public void Scan_engine_registers_schema_migration_evidence_deterministically()
    {
        using var repo = new TempDirectory();
        using var firstOutput = new TempDirectory();
        using var secondOutput = new TempDirectory();
        File.WriteAllText(Path.Combine(repo.Path, "migration.sql"), "CREATE TABLE archive.records (id bigint);\n");

        var first = ScanEngine.Scan(new ScanOptions(repo.Path, firstOutput.Path)).Facts
            .Where(IsSchemaMigrationFact).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();
        var second = ScanEngine.Scan(new ScanOptions(repo.Path, secondOutput.Path)).Facts
            .Where(IsSchemaMigrationFact).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(first);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.All(first, fact => Assert.Equal("migration.sql", fact.Evidence.FilePath));
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
