using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class EvidenceDocsExportTests
{
    [Fact]
    public async Task Docs_export_writes_deterministic_markdown_jsonl_and_manifest()
    {
        using var temp = new TempDirectory();
        var indexPath = CreateCombinedIndex(temp.Path, reverseFactOrder: false);
        var firstOut = Path.Combine(temp.Path, "docs-a");
        var secondOut = Path.Combine(temp.Path, "docs-b");

        var first = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(indexPath, firstOut));
        var second = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(CreateCombinedIndex(temp.Path, reverseFactOrder: true), secondOut));

        Assert.Equal("tracemap-evidence-docs.v1", first.Manifest.SchemaVersion);
        Assert.Equal("local-only", first.Manifest.Generator.GeneratedAt);
        Assert.Contains(first.Chunks, chunk => chunk.ChunkFamily == "source-overview");
        Assert.Contains(first.Chunks, chunk => chunk.ChunkFamily == "endpoint");
        Assert.Contains(first.Chunks, chunk => chunk.ChunkFamily == "query-sql-shape");
        Assert.All(first.Chunks.Where(chunk => chunk.ChunkType == "claim"), chunk =>
        {
            Assert.NotEmpty(chunk.RuleIds);
            Assert.NotEmpty(chunk.EvidenceTiers);
            Assert.NotEmpty(chunk.SourceRefs);
            Assert.NotEmpty(chunk.Citations);
            Assert.NotEmpty(chunk.CoverageLabels);
        });
        Assert.True(EvidenceDocsExporter.IsSelfConsistentManifest(await File.ReadAllTextAsync(Path.Combine(firstOut, "manifest.json"))));
        Assert.True(EvidenceDocsExporter.IsSelfConsistentMarkdown(await File.ReadAllTextAsync(Path.Combine(firstOut, "README.md"))));
        Assert.True(EvidenceDocsExporter.IsSelfConsistentMarkdown(await File.ReadAllTextAsync(Path.Combine(firstOut, "index.md"))));
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(firstOut, "chunks.jsonl")),
            await File.ReadAllTextAsync(Path.Combine(secondOut, "chunks.jsonl")));
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(firstOut, "README.md")),
            await File.ReadAllTextAsync(Path.Combine(secondOut, "README.md")));

        var jsonl = await File.ReadAllLinesAsync(Path.Combine(firstOut, "chunks.jsonl"));
        Assert.All(jsonl, line =>
        {
            using var document = JsonDocument.Parse(line);
            Assert.Equal("tracemap-evidence-docs.v1", document.RootElement.GetProperty("schemaVersion").GetString());
            Assert.True(document.RootElement.TryGetProperty("bodyMarkdown", out _));
        });

        var allText = string.Join('\n', Directory.EnumerateFiles(firstOut, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        var rawSqlMarker = string.Concat("sel", "ect *");
        Assert.DoesNotContain(temp.Path, allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/Users/", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git@", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(rawSqlMarker, allText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Docs_export_formats_dry_run_and_cli_argument_errors_are_bounded()
    {
        using var temp = new TempDirectory();
        var indexPath = CreateSingleIndex(temp.Path);
        var outDir = Path.Combine(temp.Path, "dry-run-docs");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(
            ["docs-export", "--index", indexPath, "--out", outDir, "--format", "jsonl,markdown", "--dry-run"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("TraceMap docs-export dry run:", output.ToString());
        Assert.False(Directory.Exists(outDir));

        var realOut = Path.Combine(temp.Path, "cli-docs");
        using var realOutput = new StringWriter();
        using var realError = new StringWriter();
        var realExitCode = await TraceMapCommand.RunAsync(
            ["docs-export", "--index", indexPath, "--out", realOut, "--format", "jsonl,markdown"],
            realOutput,
            realError);

        Assert.Equal(0, realExitCode);
        Assert.Equal(string.Empty, realError.ToString());
        Assert.True(File.Exists(Path.Combine(realOut, "chunks.jsonl")));
        Assert.True(File.Exists(Path.Combine(realOut, "README.md")));

        using var repeatedOutput = new StringWriter();
        using var repeatedError = new StringWriter();
        var repeated = await TraceMapCommand.RunAsync(
            ["docs-export", "--index", indexPath, "--out", outDir, "--format", "markdown", "--format", "jsonl"],
            repeatedOutput,
            repeatedError);
        Assert.Equal(1, repeated);
        Assert.Contains("one --format", repeatedError.ToString());

        using var familyOutput = new StringWriter();
        using var familyError = new StringWriter();
        var badFamily = await TraceMapCommand.RunAsync(
            ["docs-export", "--index", indexPath, "--out", outDir, "--families", "endpoint,not-a-family"],
            familyOutput,
            familyError);
        Assert.Equal(1, badFamily);
        Assert.Contains("unsupported family token", familyError.ToString());
    }

    [Fact]
    public async Task Docs_export_claim_filter_promotes_by_stable_source_identity_and_requires_date()
    {
        using var temp = new TempDirectory();
        var indexPath = CreateCombinedIndex(temp.Path);
        var catalogPath = Path.Combine(temp.Path, "claims.json");
        await File.WriteAllTextAsync(catalogPath, """
            {
              "schemaVersion": "source-claim-catalog.v1",
              "entries": [
                {
                  "sourceIdentity": {
                    "kind": "combined-source",
                    "sourceIndexId": "source-api",
                    "commitSha": "1111111111111111111111111111111111111111"
                  },
                  "claimLevel": "public-safe",
                  "proofId": "proof:docs",
                  "proofPathCategory": "reviewed-public-fixture",
                  "reviewer": "reviewer",
                  "reviewedAt": "2026-06",
                  "limitations": []
                }
              ]
            }
            """);

        var missingDate = await Assert.ThrowsAsync<ArgumentException>(() =>
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(
                indexPath,
                Path.Combine(temp.Path, "missing-date"),
                SourceClaimCatalogPath: catalogPath,
                MinimumClaimLevel: "public-safe")));
        Assert.Contains("--date", missingDate.Message);

        var result = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(
            indexPath,
            Path.Combine(temp.Path, "public-docs"),
            SourceClaimCatalogPath: catalogPath,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));

        Assert.Equal("public-safe", result.Manifest.ClaimLevel);
        Assert.All(result.Chunks.Where(chunk => chunk.ChunkType == "claim"), chunk => Assert.Equal("public-safe", chunk.ClaimLevel));
        Assert.Contains(result.Manifest.Gaps, gap => gap.Reason == "claim-level-hidden");
    }

    [Fact]
    public async Task Docs_export_accepts_vault_claim_catalog_shape_and_requires_reviewed_proof()
    {
        using var temp = new TempDirectory();
        var indexPath = CreateCombinedIndex(temp.Path);
        var vaultCatalog = Path.Combine(temp.Path, "vault-claims.json");
        await File.WriteAllTextAsync(vaultCatalog, """
            {
              "schemaVersion": "source-claim-catalog.v1",
              "sources": [
                {
                  "sourceIndexId": "source-api",
                  "claimLevel": "public-safe",
                  "proofId": "proof-source-api"
                }
              ]
            }
            """);

        var promoted = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(
            indexPath,
            Path.Combine(temp.Path, "vault-catalog-docs"),
            SourceClaimCatalogPath: vaultCatalog,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));
        Assert.Contains(promoted.Chunks, chunk => chunk.ChunkType == "claim" && chunk.ClaimLevel == "public-safe");

        var weakCatalog = Path.Combine(temp.Path, "weak-claims.json");
        await File.WriteAllTextAsync(weakCatalog, """
            {
              "schemaVersion": "source-claim-catalog.v1",
              "entries": [
                {
                  "sourceIdentity": { "sourceIndexId": "source-api" },
                  "claimLevel": "public-safe"
                }
              ]
            }
            """);
        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(
                indexPath,
                Path.Combine(temp.Path, "weak-catalog-docs"),
                SourceClaimCatalogPath: weakCatalog,
                MinimumClaimLevel: "public-safe",
                Date: "2026-06")));
        Assert.Contains("NoVisibleEvidenceAfterFiltering", rejected.Message);
    }

    [Fact]
    public async Task Docs_export_parses_vault_graph_and_emits_limitation_family_chunks()
    {
        using var temp = new TempDirectory();
        var indexPath = CreateSingleIndex(temp.Path);
        var graphPath = Path.Combine(temp.Path, "graph.json");
        await File.WriteAllTextAsync(graphPath, """
            {
              "schemaVersion": "evidence-graph-vault-export.v1",
              "classification": "hidden",
              "nodes": [{ "id": "node:one" }],
              "edges": [],
              "gaps": [],
              "limitations": []
            }
            """);

        var result = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(
            indexPath,
            Path.Combine(temp.Path, "vault-graph-docs"),
            VaultGraphPaths: [graphPath],
            Families: "dependency-surface,limitation,gap"));

        Assert.Contains(result.Chunks, chunk => chunk.ChunkFamily == "dependency-surface" && chunk.SupportingIds.Any(id => id.StartsWith("vault-graph:", StringComparison.Ordinal)));
        Assert.Contains(result.Chunks, chunk => chunk.ChunkFamily == "limitation");
        Assert.DoesNotContain(result.Manifest.Gaps, gap => gap.Reason == "schema-incompatible" && gap.SupportingIds.Contains("vault-graph"));
    }

    [Fact]
    public async Task Docs_export_rejects_unsafe_values_without_echoing_them()
    {
        using var temp = new TempDirectory();
        var localPath = string.Concat("/", "Users", "/private/source/Controller.cs");
        var indexPath = CreateSingleIndex(temp.Path, filePath: localPath);

        var result = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(indexPath, Path.Combine(temp.Path, "docs")));
        Assert.Contains(result.Chunks.SelectMany(chunk => chunk.Gaps), gap => gap.Reason == "missing-provenance");

        var rawSql = string.Concat("sel", "ect * fr", "om Customers wh", "ere Pass", "word = 'secret'");
        var sqlIndex = CreateSingleIndex(temp.Path, propertiesJson: $$"""{"query":"{{rawSql}}"}""");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(sqlIndex, Path.Combine(temp.Path, "sql-docs"))));
        Assert.Contains("UnsafeValueRejected", failure.Message);
        Assert.DoesNotContain("Customers", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", failure.Message, StringComparison.OrdinalIgnoreCase);

        var nullPropertiesIndex = CreateSingleIndex(temp.Path, propertiesJson: null);
        var nullPropertiesResult = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(nullPropertiesIndex, Path.Combine(temp.Path, "null-properties-docs")));
        Assert.Contains(nullPropertiesResult.Chunks, chunk => chunk.ChunkFamily == "endpoint");
    }

    [Fact]
    public async Task Docs_export_generated_file_collisions_respect_force_boundaries()
    {
        using var temp = new TempDirectory();
        var indexPath = CreateSingleIndex(temp.Path);
        var outDir = Path.Combine(temp.Path, "docs");

        await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(indexPath, outDir));
        var readme = Path.Combine(outDir, "README.md");
        await File.AppendAllTextAsync(readme, "\nmanual edit\n");

        var stale = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(indexPath, outDir)));
        Assert.Contains("GeneratedFileStale", stale.Message);

        await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(indexPath, outDir, Force: true));
        Assert.True(EvidenceDocsExporter.IsSelfConsistentMarkdown(await File.ReadAllTextAsync(readme)));

        var userOut = Path.Combine(temp.Path, "user-docs");
        Directory.CreateDirectory(userOut);
        await File.WriteAllTextAsync(Path.Combine(userOut, "README.md"), "# user file\n");
        var collision = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(indexPath, userOut, Force: true)));
        Assert.Contains("UserFileCollision", collision.Message);
    }

    [Fact]
    public async Task Docs_export_sanitizes_non_tracemap_sqlite_failures_and_exposes_stable_id_record_format()
    {
        using var temp = new TempDirectory();
        var sqlitePath = Path.Combine(temp.Path, "not-tracemap.sqlite");
        await using (var connection = new SqliteConnection($"Data Source={sqlitePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "create table unrelated(id integer primary key);";
            await command.ExecuteNonQueryAsync();
        }

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(sqlitePath, Path.Combine(temp.Path, "out"))));
        Assert.Contains("TraceMap", failure.Message);
        Assert.DoesNotContain(sqlitePath, failure.Message, StringComparison.OrdinalIgnoreCase);

        var record = EvidenceDocsExporter.StableIdInputRecord([new("chunkFamily", "endpoint"), new("missing", null)]);
        Assert.Equal("11:chunkFamily=8:endpoint\n7:missing=0:\n", record);
    }

    private static string CreateSingleIndex(string root, string filePath = "src/Api/Controller.cs", string? propertiesJson = """{"method":"GET","route":"/api/orders/{}"}""")
    {
        var path = Path.Combine(root, $"single-{Guid.NewGuid():N}.sqlite");
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
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
              properties_json text
            );
            insert into scan_manifest values (
              'scan-single',
              'sample',
              '1111111111111111111111111111111111111111',
              'tracemap-tests',
              '2026-06-01T00:00:00Z',
              'Level1SemanticAnalysis',
              'Succeeded',
              '{}'
            );
            insert into facts values (
              'fact-route',
              'scan-single',
              'sample',
              '1111111111111111111111111111111111111111',
              null,
              'HttpRouteBinding',
              'csharp.syntax.aspnetroute.v1',
              'Tier2Structural',
              null,
              'OrdersController.Get',
              'GET /api/orders/{}',
              $file_path,
              10,
              12,
              'hash-route',
              $properties_json
            );
            """;
        command.Parameters.AddWithValue("$file_path", filePath);
        command.Parameters.AddWithValue("$properties_json", (object?)propertiesJson ?? DBNull.Value);
        command.ExecuteNonQuery();
        return path;
    }

    private static string CreateCombinedIndex(string root, bool reverseFactOrder = false)
    {
        var path = Path.Combine(root, $"combined-{Guid.NewGuid():N}.sqlite");
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
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
              properties_json text not null,
              payload_json text not null
            );
            insert into index_sources values (
              'source-api',
              'api',
              'indexhash',
              'scan-api',
              'sample',
              null,
              null,
              '1111111111111111111111111111111111111111',
              'tracemap-tests',
              'csharp',
              null,
              null,
              null,
              'Level1SemanticAnalysisReduced',
              'Succeeded',
              '{}',
              '2026-06-01T00:00:00Z'
            );
            insert into index_sources values (
              'source-client',
              'client',
              'indexhash2',
              'scan-client',
              'sample',
              null,
              null,
              '2222222222222222222222222222222222222222',
              'tracemap-tests',
              'typescript',
              null,
              null,
              null,
              'Level3SyntaxAnalysis',
              'Failed',
              '{}',
              '2026-06-01T00:00:00Z'
            );
            """;
        command.ExecuteNonQuery();

        var facts = new[]
        {
            new object[]
            {
                "source-api:fact-route", "source-api", "fact-route", "scan-api", "scan-api",
                "1111111111111111111111111111111111111111", "HttpRouteBinding", "csharp.syntax.aspnetroute.v1",
                "Tier2Structural", null!, "OrdersController.Get", "GET /api/orders/{}", "src/Api/OrdersController.cs", 20, 22,
                """{"method":"GET","routeKey":"/api/orders/{}"}"""
            },
            new object[]
            {
                "source-api:fact-sql", "source-api", "fact-sql", "scan-api", "scan-api",
                "1111111111111111111111111111111111111111", "SqlTextUsed", "sql.shape.query.v1",
                "Tier3SyntaxOrTextual", null!, "OrdersRepository.Find", "query-shape:select-by-id", "src/Api/OrdersRepository.cs", 40, 42,
                """{"operation":"select","table":"Orders","shapeHash":"abcdef123456"}"""
            },
            new object[]
            {
                "source-client:fact-gap", "source-client", "fact-gap", "scan-client", "scan-client",
                "2222222222222222222222222222222222222222", "AnalysisGap", "docs.fixture.gap.v1",
                "Tier4Unknown", null!, null!, null!, "src/Client/app.ts", 5, 5,
                """{"reason":"reduced-coverage"}"""
            }
        };

        foreach (var fact in reverseFactOrder ? facts.Reverse() : facts)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                insert into combined_facts values (
                  $combined_fact_id, $source_index_id, $original_fact_id, $original_scan_id, $scan_id, 'sample',
                  $commit_sha, null, $fact_type, $rule_id, $evidence_tier, $source_symbol, $target_symbol,
                  $contract_element, $file_path, $start_line, $end_line, 'snippet-hash', $properties_json, '{}'
                );
                """;
            insert.Parameters.AddWithValue("$combined_fact_id", fact[0]);
            insert.Parameters.AddWithValue("$source_index_id", fact[1]);
            insert.Parameters.AddWithValue("$original_fact_id", fact[2]);
            insert.Parameters.AddWithValue("$original_scan_id", fact[3]);
            insert.Parameters.AddWithValue("$scan_id", fact[4]);
            insert.Parameters.AddWithValue("$commit_sha", fact[5]);
            insert.Parameters.AddWithValue("$fact_type", fact[6]);
            insert.Parameters.AddWithValue("$rule_id", fact[7]);
            insert.Parameters.AddWithValue("$evidence_tier", fact[8]);
            insert.Parameters.AddWithValue("$source_symbol", fact[9] ?? DBNull.Value);
            insert.Parameters.AddWithValue("$target_symbol", fact[10] ?? DBNull.Value);
            insert.Parameters.AddWithValue("$contract_element", fact[11] ?? DBNull.Value);
            insert.Parameters.AddWithValue("$file_path", fact[12]);
            insert.Parameters.AddWithValue("$start_line", fact[13]);
            insert.Parameters.AddWithValue("$end_line", fact[14]);
            insert.Parameters.AddWithValue("$properties_json", fact[15]);
            insert.ExecuteNonQuery();
        }

        return path;
    }
}
