using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

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
        var gap = Assert.Single(facts, fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap);
        Assert.Equal("CreateTableClauseUnsupported", gap.Properties["classification"]);
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
    public void Extract_emits_enum_and_routine_identity_without_labels_signatures_or_bodies()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "routines.sql"), """
            CREATE TYPE archive.retention_state AS ENUM ('sentinel-private-label', 'ready');
            CREATE OR REPLACE FUNCTION archive.move_batch(private_limit integer)
            RETURNS integer
            LANGUAGE plpgsql
            AS $body$
            BEGIN
              PERFORM dblink_exec('sentinel-private-connection', 'sentinel-private-sql');
              RETURN private_limit;
            END
            $body$;
            CREATE PROCEDURE archive.refresh_archive(private_token text)
            LANGUAGE sql
            AS $$ SELECT 'sentinel-private-body' $$;
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);

        var enumFact = Assert.Single(facts, fact => fact.FactType == FactTypes.PostgresSchemaEnumDeclared);
        Assert.Equal("archive", enumFact.Properties["schemaName"]);
        Assert.Equal("retention_state", enumFact.Properties["enumName"]);
        Assert.Equal("true", enumFact.Properties["enumLabelsOmitted"]);

        var routines = facts.Where(fact => fact.FactType == FactTypes.PostgresSchemaRoutineDeclared)
            .OrderBy(fact => fact.Properties["routineName"], StringComparer.Ordinal).ToArray();
        Assert.Equal(["move_batch", "refresh_archive"], routines.Select(fact => fact.Properties["routineName"]).ToArray());
        Assert.Equal(["function", "procedure"], routines.Select(fact => fact.Properties["routineKind"]).ToArray());
        Assert.All(routines, fact =>
        {
            Assert.Equal("true", fact.Properties["routineSignatureOmitted"]);
            Assert.Equal("true", fact.Properties["routineBodyOmitted"]);
            Assert.Equal(RuleIds.DatabasePostgresSchemaMigration, fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
            Assert.Equal(ScannerVersions.PostgresSchemaMigrationExtractor, fact.Evidence.ExtractorVersion);
        });
        Assert.Equal(3, facts.Count(fact => fact.FactType == FactTypes.PostgresMigrationOperation));
        Assert.DoesNotContain("sentinel-private", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_limit", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RETURNS", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LANGUAGE", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extract_gaps_quoted_enum_and_routine_identity_without_leaking_names()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "quoted.sql"), """
            CREATE TYPE "private_enum" AS ENUM ('sentinel-private-label');
            CREATE FUNCTION "private_function"() RETURNS text AS $$ SELECT 'sentinel-private-body' $$ LANGUAGE sql;
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);

        Assert.Equal(2, facts.Count(fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap));
        Assert.DoesNotContain(facts, fact => fact.FactType is FactTypes.PostgresSchemaEnumDeclared or FactTypes.PostgresSchemaRoutineDeclared);
        Assert.DoesNotContain("private_enum", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_function", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-private", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_gaps_truncated_sql_standard_routine_body_without_emitting_routine_facts()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "atomic.sql"), """
            CREATE FUNCTION archive.move_batch() RETURNS integer
            BEGIN ATOMIC
              INSERT INTO archive.audit VALUES (1);
              RETURN 1;
            END;
            """);

        var facts = Extract(temp.Path);

        Assert.DoesNotContain(facts, fact => fact.FactType is FactTypes.PostgresMigrationOperation or FactTypes.PostgresSchemaRoutineDeclared);
        var gap = Assert.Single(facts, fact => fact.Properties.GetValueOrDefault("classification") == "IncompleteDdlStatement");
        Assert.Equal(1, gap.Evidence.StartLine);
        Assert.NotNull(gap.Evidence.SnippetHash);
    }

    [Fact]
    public void Extract_emits_bounded_drop_and_rename_operations()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "destructive.sql"), """
            ALTER TABLE archive.records RENAME COLUMN retention_state TO archive_state;
            ALTER TABLE IF EXISTS ONLY archive.records DROP COLUMN IF EXISTS legacy_payload RESTRICT;
            ALTER TABLE archive.records RENAME TO archived_records;
            DROP TABLE IF EXISTS archive.retired_records CASCADE;
            """);

        var facts = Extract(temp.Path);
        var operations = facts.Where(fact => fact.FactType == FactTypes.PostgresMigrationOperation)
            .OrderBy(fact => fact.Properties["statementOrdinal"], StringComparer.Ordinal).ToArray();

        Assert.Equal(4, operations.Length);
        Assert.Equal(
            ["rename-column", "drop-column", "rename-table", "drop-table"],
            operations.Select(fact => fact.Properties["operationKind"]).ToArray());

        Assert.Equal("records", operations[0].Properties["tableName"]);
        Assert.Equal("retention_state", operations[0].Properties["columnName"]);
        Assert.Equal("archive_state", operations[0].Properties["newColumnName"]);
        Assert.Equal("restrict", operations[1].Properties["dropBehavior"]);
        Assert.Equal("legacy_payload", operations[1].Properties["columnName"]);
        Assert.Equal("archived_records", operations[2].Properties["newTableName"]);
        Assert.Equal("retired_records", operations[3].Properties["tableName"]);
        Assert.Equal("cascade", operations[3].Properties["dropBehavior"]);
        Assert.All(operations, fact =>
        {
            Assert.Equal("migration-operation", fact.Properties["objectKind"]);
            Assert.Equal("archive", fact.Properties["schemaName"]);
            Assert.Equal("bounded-static-evidence", fact.Properties["coverageLabel"]);
            Assert.Equal(RuleIds.DatabasePostgresSchemaMigration, fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
            Assert.Equal(ScannerVersions.PostgresSchemaMigrationExtractor, fact.Evidence.ExtractorVersion);
            Assert.Equal("destructive.sql", fact.Evidence.FilePath);
            Assert.Equal("0123456789abcdef", fact.CommitSha);
            Assert.Contains("execution order", fact.Properties["limitations"], StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Extract_gaps_quoted_multi_object_and_multi_subcommand_destructive_shapes_without_leaking_identity()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "deferred-destructive.sql"), """
            DROP TABLE archive.private_one, archive.private_two;
            DROP TABLE "private_schema"."private_table";
            ALTER TABLE archive.records RENAME COLUMN "private_column" TO visible_column;
            ALTER TABLE archive.records DROP COLUMN first_private, DROP COLUMN second_private;
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);

        Assert.Equal(4, facts.Count(fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap));
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.PostgresMigrationOperation);
        Assert.DoesNotContain("private_one", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_two", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_schema", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_table", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_column", json, StringComparison.Ordinal);
        Assert.DoesNotContain("first_private", json, StringComparison.Ordinal);
        Assert.DoesNotContain("second_private", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_gaps_unsupported_destructive_ddl_without_leaking_identity()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "unsupported-destructive.sql"), """
            DROP INDEX private_index;
            DROP TYPE private_type;
            TRUNCATE TABLE private_records;
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);

        Assert.Single(facts, fact => fact.FactType == FactTypes.PostgresMigrationFileDeclared);
        Assert.Equal(3, facts.Count(fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap));
        Assert.All(facts.Where(fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap), fact =>
            Assert.Equal("UnsupportedSchemaDdlShape", fact.Properties["classification"]));
        Assert.DoesNotContain("private_index", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_type", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_records", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_emits_explicit_pg_dump_snapshot_evidence_without_comment_identity()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "snapshot.sql"), """
            -- tracemap-postgres-schema-snapshot: v1
            -- PostgreSQL database dump
            -- source database: sentinel-private-database
            -- server: sentinel-private-server
            CREATE TABLE archive.records (id bigint);
            CREATE INDEX records_id_idx ON archive.records (id);
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);
        var snapshot = Assert.Single(facts, fact => fact.FactType == FactTypes.PostgresSchemaSnapshotDeclared);

        Assert.Equal("pg-dump", snapshot.Properties["snapshotFormat"]);
        Assert.Equal("2", snapshot.Properties["recognizedDdlStatementCount"]);
        Assert.Equal("0", snapshot.Properties["unsupportedDdlStatementCount"]);
        Assert.Equal("true", snapshot.Properties["sourceDatabaseIdentityOmitted"]);
        Assert.Equal("bounded-static-evidence", snapshot.Properties["coverageLabel"]);
        Assert.Equal(RuleIds.DatabasePostgresSchemaMigration, snapshot.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, snapshot.EvidenceTier);
        Assert.Equal("snapshot.sql", snapshot.Evidence.FilePath);
        Assert.Equal(ScannerVersions.PostgresSchemaMigrationExtractor, snapshot.Evidence.ExtractorVersion);
        Assert.NotNull(snapshot.Evidence.SnippetHash);
        Assert.DoesNotContain(facts, fact =>
            fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap
            && fact.Properties.GetValueOrDefault("classification")?.StartsWith("Snapshot", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("sentinel-private", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_marks_directive_snapshot_reduced_for_unsupported_ddl_families_without_identity_leakage()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "partial.sql"), """
            -- tracemap-postgres-schema-snapshot: v1
            CREATE TABLE archive.records (id bigint);
            CREATE SEQUENCE archive.private_sequence;
            CREATE VIEW archive.private_view AS SELECT id FROM archive.records;
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);
        var snapshot = Assert.Single(facts, fact => fact.FactType == FactTypes.PostgresSchemaSnapshotDeclared);
        var gap = Assert.Single(facts, fact =>
            fact.Properties.GetValueOrDefault("classification") == "SnapshotDdlCoverageReduced");

        Assert.Equal("tracemap-directive-v1", snapshot.Properties["snapshotFormat"]);
        Assert.Equal("1", snapshot.Properties["recognizedDdlStatementCount"]);
        Assert.Equal("2", snapshot.Properties["unsupportedDdlStatementCount"]);
        Assert.Equal("reduced-static-evidence", snapshot.Properties["coverageLabel"]);
        Assert.Equal("2", gap.Properties["unsupportedDdlStatementCount"]);
        Assert.Equal("create-sequence,create-view", gap.Properties["unsupportedDdlFamilies"]);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.DoesNotContain("private_sequence", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_view", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_counts_only_projected_ddl_in_mixed_snapshot_coverage()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "mixed-snapshot.sql"), """
            -- tracemap-postgres-schema-snapshot: v1
            CREATE TABLE archive.records (id bigint);
            DROP INDEX private_index;
            TRUNCATE TABLE private_records;
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);
        var snapshot = Assert.Single(facts, fact => fact.FactType == FactTypes.PostgresSchemaSnapshotDeclared);
        var gap = Assert.Single(facts, fact => fact.Properties.GetValueOrDefault("classification") == "SnapshotDdlCoverageReduced");

        Assert.Equal("1", snapshot.Properties["recognizedDdlStatementCount"]);
        Assert.Equal("2", snapshot.Properties["unsupportedDdlStatementCount"]);
        Assert.Equal("drop-index,truncate-table", gap.Properties["unsupportedDdlFamilies"]);
        Assert.Equal(2, facts.Count(fact => fact.Properties.GetValueOrDefault("classification") == "UnsupportedSchemaDdlShape"));
        Assert.DoesNotContain("private_index", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_records", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_preserves_unsupported_only_snapshot_identity_without_migration_file_fact()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "unsupported-only-snapshot.sql"), """
            -- tracemap-postgres-schema-snapshot: v1
            DROP VIEW private_view;
            DROP TYPE private_type;
            TRUNCATE TABLE private_records;
            CREATE TABLE "private_schema"."private_table" (id bigint);
            ALTER TABLE ONLY archive.records ADD COLUMN private_column text;
            """);

        var facts = Extract(temp.Path);
        var json = JsonSerializer.Serialize(facts);
        var snapshot = Assert.Single(facts, fact => fact.FactType == FactTypes.PostgresSchemaSnapshotDeclared);
        var gap = Assert.Single(facts, fact => fact.Properties.GetValueOrDefault("classification") == "SnapshotRecognizedDdlUnavailable");

        Assert.Equal("0", snapshot.Properties["recognizedDdlStatementCount"]);
        Assert.Equal("5", snapshot.Properties["unsupportedDdlStatementCount"]);
        Assert.Equal("alter-table,create-table,drop-type,drop-view,truncate-table", gap.Properties["unsupportedDdlFamilies"]);
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.PostgresMigrationFileDeclared);
        Assert.Equal(5, facts.Count(fact => fact.RuleId == RuleIds.DatabasePostgresSchemaMigrationGap
            && fact.Properties.GetValueOrDefault("statementOrdinal") != "0"));
        Assert.DoesNotContain("private_view", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_type", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_records", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_schema", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_table", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_column", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_does_not_infer_snapshot_from_filename_or_marker_text_inside_sql_literals()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "schema.sql"), """
            SELECT '-- PostgreSQL database dump';
            CREATE TABLE archive.records (id bigint);
            """);

        var facts = Extract(temp.Path);

        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.PostgresSchemaSnapshotDeclared);
        Assert.Contains(facts, fact => fact.FactType == FactTypes.PostgresSchemaTableDeclared);
    }

    [Fact]
    public void Extract_preserves_snapshot_identity_with_gap_when_no_supported_ddl_is_available()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "unsupported-snapshot.sql"), """
            -- tracemap-postgres-schema-snapshot: v1
            SET statement_timeout = 0;
            CREATE SEQUENCE archive.private_sequence;
            """);

        var facts = Extract(temp.Path);
        var snapshot = Assert.Single(facts, fact => fact.FactType == FactTypes.PostgresSchemaSnapshotDeclared);
        var gap = Assert.Single(facts, fact =>
            fact.Properties.GetValueOrDefault("classification") == "SnapshotRecognizedDdlUnavailable");

        Assert.Equal("0", snapshot.Properties["recognizedDdlStatementCount"]);
        Assert.Equal("1", snapshot.Properties["unsupportedDdlStatementCount"]);
        Assert.Equal("reduced-static-evidence", snapshot.Properties["coverageLabel"]);
        Assert.Equal("create-sequence", gap.Properties["unsupportedDdlFamilies"]);
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.PostgresMigrationFileDeclared);
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
        Assert.Contains("CREATE TYPE", block, StringComparison.Ordinal);
        Assert.Contains("CREATE FUNCTION", block, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE", block, StringComparison.Ordinal);
        Assert.Contains("RENAME TABLE", block, StringComparison.Ordinal);
        Assert.Contains("routine signatures", block, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("referential integrity", block, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data loss", block, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("schema snapshot", block, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source database", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scan_engine_registers_schema_migration_evidence_deterministically()
    {
        using var repo = new TempDirectory();
        using var firstOutput = new TempDirectory();
        using var secondOutput = new TempDirectory();
        File.WriteAllText(Path.Combine(repo.Path, "migration.sql"), """
            -- PostgreSQL database dump
            CREATE TABLE archive.records (
              id bigint,
              CONSTRAINT records_pkey PRIMARY KEY (id)
            );
            CREATE INDEX records_id_idx ON archive.records (id);
            CREATE TYPE archive.retention_state AS ENUM ('ready', 'archived');
            CREATE FUNCTION archive.move_batch(batch_size integer)
            RETURNS integer LANGUAGE sql AS $$ SELECT batch_size $$;
            ALTER TABLE archive.records RENAME COLUMN id TO record_id;
            DROP TABLE archive.retired_records RESTRICT;
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
        Assert.Contains(first, fact => fact.FactType == FactTypes.PostgresSchemaEnumDeclared);
        Assert.Contains(first, fact => fact.FactType == FactTypes.PostgresSchemaRoutineDeclared);
        Assert.Contains(first, fact => fact.FactType == FactTypes.PostgresSchemaSnapshotDeclared);
        Assert.Contains(first, fact => fact.FactType == FactTypes.PostgresMigrationOperation
            && fact.Properties.GetValueOrDefault("operationKind") == "rename-column");
        Assert.Contains(first, fact => fact.FactType == FactTypes.PostgresMigrationOperation
            && fact.Properties.GetValueOrDefault("operationKind") == "drop-table");
    }

    [Fact]
    public async Task Release_review_projects_schema_migration_only_evidence()
    {
        using var repo = new TempDirectory();
        using var output = new TempDirectory();
        var indexPath = Path.Combine(output.Path, "index.sqlite");
        File.WriteAllText(Path.Combine(repo.Path, "migration.sql"), """
            -- tracemap-postgres-schema-snapshot: v1
            CREATE TABLE archive.records (id bigint);
            CREATE TYPE archive.retention_state AS ENUM ('sentinel-private-label');
            CREATE FUNCTION archive.move_batch(private_limit integer)
            RETURNS integer LANGUAGE sql AS $$ SELECT private_limit $$;
            ALTER TABLE archive.records RENAME COLUMN id TO record_id;
            DROP TABLE archive.retired_records RESTRICT;
            """);
        var manifest = Manifest();
        var facts = PostgresSchemaMigrationExtractor.Extract(repo.Path, manifest, FileInventory.Collect(repo.Path));
        SqliteIndexWriter.Write(indexPath, manifest, facts);

        var review = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            indexPath, indexPath, Path.Combine(output.Path, "review"), Scope: "sql-evidence"));

        Assert.Equal(ReleaseReviewStatuses.Available, review.SqlEvidence.Status);
        Assert.Contains(review.SqlEvidence.Findings, finding =>
            finding.RuleId == RuleIds.DatabasePostgresSchemaMigration
            && finding.Metadata.Any(pair => pair.Key == "factType" && pair.Value == FactTypes.PostgresSchemaTableDeclared));
        Assert.Contains(review.SqlEvidence.Findings, finding =>
            finding.Metadata.Any(pair => pair.Key == "factType" && pair.Value == FactTypes.PostgresSchemaSnapshotDeclared)
            && finding.Metadata.Any(pair => pair.Key == "snapshotFormat" && pair.Value == "tracemap-directive-v1")
            && finding.Metadata.Any(pair => pair.Key == "sourceDatabaseIdentityOmitted" && pair.Value == "true"));
        Assert.Contains(review.SqlEvidence.Findings, finding =>
            finding.Metadata.Any(pair => pair.Key == "factType" && pair.Value == FactTypes.PostgresSchemaEnumDeclared)
            && finding.Metadata.Any(pair => pair.Key == "enumLabelsOmitted" && pair.Value == "true"));
        Assert.Contains(review.SqlEvidence.Findings, finding =>
            finding.Metadata.Any(pair => pair.Key == "factType" && pair.Value == FactTypes.PostgresSchemaRoutineDeclared)
            && finding.Metadata.Any(pair => pair.Key == "routineKind" && pair.Value == "function")
            && finding.Metadata.Any(pair => pair.Key == "routineSignatureOmitted" && pair.Value == "true")
            && finding.Metadata.Any(pair => pair.Key == "routineBodyOmitted" && pair.Value == "true"));
        Assert.Contains(review.SqlEvidence.Findings, finding =>
            finding.Metadata.Any(pair => pair.Key == "operationKind" && pair.Value == "rename-column")
            && finding.Metadata.Any(pair => pair.Key == "columnName" && pair.Value == "id")
            && finding.Metadata.Any(pair => pair.Key == "newColumnName" && pair.Value == "record_id"));
        Assert.Contains(review.SqlEvidence.Findings, finding =>
            finding.Metadata.Any(pair => pair.Key == "operationKind" && pair.Value == "drop-table")
            && finding.Metadata.Any(pair => pair.Key == "dropBehavior" && pair.Value == "restrict"));
        Assert.All(review.SqlEvidence.Findings.Where(finding => finding.RuleId == RuleIds.DatabasePostgresSchemaMigration), finding =>
        {
            Assert.Equal(manifest.CommitSha, finding.CommitSha);
            Assert.Contains(finding.Limitations, limitation =>
                limitation.Contains("dialect validity", StringComparison.OrdinalIgnoreCase)
                && limitation.Contains("production state", StringComparison.OrdinalIgnoreCase));
        });
        Assert.DoesNotContain(review.SqlEvidence.Gaps, gap => gap.GapKind == "CompatibleEvidenceUnavailable");
        Assert.DoesNotContain("sentinel-private", JsonSerializer.Serialize(review), StringComparison.Ordinal);
        Assert.DoesNotContain("private_limit", JsonSerializer.Serialize(review), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Release_review_rejects_schema_migration_evidence_without_a_real_commit_sha()
    {
        using var repo = new TempDirectory();
        using var output = new TempDirectory();
        var indexPath = Path.Combine(output.Path, "index.sqlite");
        File.WriteAllText(Path.Combine(repo.Path, "migration.sql"),
            "CREATE TABLE archive.records (id bigint);\n");
        var manifest = Manifest() with { CommitSha = "unknown" };
        var facts = PostgresSchemaMigrationExtractor.Extract(repo.Path, manifest, FileInventory.Collect(repo.Path));
        SqliteIndexWriter.Write(indexPath, manifest, facts);

        var review = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            indexPath, indexPath, Path.Combine(output.Path, "review"), Scope: "sql-evidence"));

        Assert.Equal(ReleaseReviewStatuses.Unavailable, review.SqlEvidence.Status);
        Assert.Empty(review.SqlEvidence.Findings);
        Assert.Contains(review.SqlEvidence.Gaps, gap => gap.GapKind == "ExtractorProvenanceUnavailable");
    }

    private static IReadOnlyList<CodeFact> Extract(string root) => PostgresSchemaMigrationExtractor.Extract(root, Manifest(), FileInventory.Collect(root));

    private static ScanManifest Manifest() => new(
        "scan-schema-test", "synthetic-postgres-schema", null, "test", "0123456789abcdef", "test", DateTimeOffset.UnixEpoch,
        "Level3SyntaxAnalysis", "NotRun", [], [], [], []);

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
