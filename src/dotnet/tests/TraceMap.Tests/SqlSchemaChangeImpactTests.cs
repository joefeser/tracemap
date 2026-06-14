using System.Globalization;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqlSchemaChangeImpactTests
{
    [Fact]
    public async Task Reduce_sql_schema_delta_matches_single_index_sql_evidence_and_writes_sql_reports()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteSqlSchemaDelta(temp.Path, """
            {
              "id": "chg-table-orders",
              "kind": "table",
              "changeType": "removed",
              "reference": {
                "tableName": "Orders"
              }
            },
            {
              "id": "chg-query-shape",
              "kind": "query-shape",
              "changeType": "shape_changed",
              "reference": {
                "queryShapeHash": "abc123"
              }
            },
            {
              "id": "chg-text-hash",
              "kind": "query-shape",
              "changeType": "shape_changed",
              "reference": {
                "textHash": "def456"
              }
            },
            {
              "id": "chg-mapping",
              "kind": "mapping",
              "changeType": "nullable_changed",
              "reference": {
                "tableName": "Orders",
                "columnName": "Status",
                "mappedName": "Status"
              }
            },
            {
              "id": "chg-sql-file",
              "kind": "sql-file",
              "changeType": "behavior_changed",
              "reference": {
                "sqlResourceName": "orders-report.sql"
              }
            }
            """);
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.QueryPatternDetected,
                RuleIds.DatabaseSqlShape,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/OrdersRepository.cs", 21, 21, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["operationName"] = "select",
                    ["queryShapeHash"] = "abc123",
                    ["tableName"] = "Orders",
                    ["textHash"] = "def456"
                }),
            FactFactory.Create(
                manifest,
                FactTypes.SqlTextUsed,
                RuleIds.DatabaseSqlText,
                EvidenceTiers.Tier3SyntaxOrTextual,
                new EvidenceSpan("src/OrdersRepository.cs", 20, 20, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["textHash"] = "def456",
                    ["textLength"] = "44"
                }),
            FactFactory.Create(
                manifest,
                FactTypes.DatabaseColumnMapping,
                RuleIds.DatabaseEntityFramework,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/Order.cs", 8, 8, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["columnName"] = "Status",
                    ["mappedName"] = "Status",
                    ["surfaceKind"] = "sql-persistence",
                    ["tableName"] = "Orders"
                }),
            FactFactory.Create(
                manifest,
                FactTypes.SqlFileDeclared,
                RuleIds.FileInventory,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("sql/orders-report.sql", 1, 1, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sqlResourceName"] = "orders-report.sql",
                    ["sqlSourceKind"] = "sql-file"
                })
        ]);

        var exitCode = await RunCliAsync([
            "reduce",
            "--index", indexPath,
            "--sql-schema-delta", deltaPath,
            "--out", outputPath
        ]);

        Assert.Equal(0, exitCode);
        var markdownPath = Path.Combine(outputPath, "sql-impact-report.md");
        var jsonPath = Path.Combine(outputPath, "sql-impact-report.json");
        Assert.True(File.Exists(markdownPath));
        Assert.True(File.Exists(jsonPath));
        Assert.False(File.Exists(Path.Combine(outputPath, "impact-report.md")));
        var markdown = await File.ReadAllTextAsync(markdownPath);
        var json = await File.ReadAllTextAsync(jsonPath);
        Assert.Contains("Report type: `SqlSchemaChangeImpactSingleV1`", markdown);
        Assert.Contains("Change kind: `table`", markdown);
        Assert.Contains("Classification: `NeedsReview`", markdown);
        Assert.Contains("Classification: `ProbableImpact`", markdown);
        Assert.Contains("\"reportType\": \"SqlSchemaChangeImpactSingleV1\"", json);
        Assert.Contains("\"evidenceKind\": \"sql-query-shape\"", json);
        Assert.Contains("\"evidenceKind\": \"sql-text-hash\"", json);
        Assert.Contains("\"evidenceKind\": \"sql-persistence-mapping\"", json);
        Assert.Contains("\"evidenceKind\": \"sql-resource\"", json);
        Assert.DoesNotContain("select * from", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reduce_sql_schema_delta_rejects_invalid_input_and_mutual_exclusion_without_echoing_unsafe_values()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        SqliteIndexWriter.Write(indexPath, Manifest("api", ScannerVersions.TraceMap), []);
        var unsafeDeltaPath = Path.Combine(temp.Path, "unsafe-sql-delta.json");
        await File.WriteAllTextAsync(unsafeDeltaPath, """
            {
              "version": "sql-schema-delta.v1",
              "source": { "name": "release" },
              "changes": [
                {
                  "id": "chg-unsafe",
                  "kind": "query-shape",
                  "changeType": "shape_changed",
                  "reference": {
                    "rawSql": "select * from Customers where Password='secret'"
                  }
                }
              ]
            }
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "reduce",
            "--index", indexPath,
            "--sql-schema-delta", unsafeDeltaPath,
            "--out", outputPath
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("safe selector", error.ToString());
        Assert.DoesNotContain("Password", error.ToString(), StringComparison.OrdinalIgnoreCase);

        var contractDeltaPath = WriteContractDelta(temp.Path);
        using var bothOutput = new StringWriter();
        using var bothError = new StringWriter();
        exitCode = await TraceMapCommand.RunAsync([
            "reduce",
            "--index", indexPath,
            "--contract-delta", contractDeltaPath,
            "--sql-schema-delta", unsafeDeltaPath,
            "--out", outputPath
        ], bothOutput, bothError);

        Assert.Equal(1, exitCode);
        Assert.Contains("not both", bothError.ToString());
    }

    [Fact]
    public async Task Reduce_sql_schema_delta_combined_index_uses_combined_classifications_and_source_labels()
    {
        using var temp = new TempDirectory();
        var apiIndex = Path.Combine(temp.Path, "api.sqlite");
        var workerIndex = Path.Combine(temp.Path, "worker.sqlite");
        var combinedIndex = Path.Combine(temp.Path, "combined.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteSqlSchemaDelta(temp.Path, """
            {
              "id": "chg-orders-status",
              "kind": "column",
              "changeType": "type_changed",
              "reference": {
                "tableName": "Orders",
                "columnName": "Status"
              }
            }
            """);
        var apiManifest = Manifest("api", ScannerVersions.TraceMap);
        var workerManifest = Manifest("worker", "tracemap-python/0.1.0");
        SqliteIndexWriter.Write(apiIndex, apiManifest, [
            FactFactory.Create(
                apiManifest,
                FactTypes.DatabaseColumnMapping,
                RuleIds.DatabaseEntityFramework,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/Order.cs", 8, 8, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["columnName"] = "Status",
                    ["surfaceKind"] = "sql-persistence",
                    ["tableName"] = "Orders"
                })
        ]);
        SqliteIndexWriter.Write(workerIndex, workerManifest, [
            FactFactory.Create(
                workerManifest,
                FactTypes.QueryPatternDetected,
                RuleIds.DatabaseSqlShape,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("app/orders.py", 12, 12, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["columnNames"] = "Status",
                    ["queryShapeHash"] = "shape123",
                    ["tableName"] = "Orders"
                })
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([apiIndex, workerIndex], combinedIndex, ["api", "worker"]));

        var exitCode = await RunCliAsync([
            "reduce",
            "--index", combinedIndex,
            "--sql-schema-delta", deltaPath,
            "--out", outputPath,
            "--include-paths",
            "--include-reverse"
        ]);

        Assert.Equal(0, exitCode);
        var markdown = await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-impact-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-impact-report.json"));
        Assert.Contains("Report type: `SqlSchemaChangeImpactCombinedV1`", markdown);
        Assert.Contains("Classification: `ProbableStaticImpact`", markdown);
        Assert.Contains("PathContextUnavailable", markdown);
        Assert.Contains("ReverseContextUnavailable", markdown);
        Assert.Contains("\"sourceLabel\": \"api\"", json);
        Assert.Contains("\"sourceLabel\": \"worker\"", json);
    }

    [Fact]
    public async Task Reduce_sql_schema_delta_empty_changes_and_json_directory_output_are_deterministic()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputOne = Path.Combine(temp.Path, "out-one");
        var outputTwo = Path.Combine(temp.Path, "out-two");
        var deltaPath = Path.Combine(temp.Path, "empty-sql-delta.json");
        await File.WriteAllTextAsync(deltaPath, """
            {
              "version": "sql-schema-delta.v1",
              "source": { "name": "release" },
              "changes": []
            }
            """);
        SqliteIndexWriter.Write(indexPath, Manifest("api", ScannerVersions.TraceMap), []);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--sql-schema-delta", deltaPath, "--out", outputOne, "--format", "json"
        ]));
        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--sql-schema-delta", deltaPath, "--out", outputTwo, "--format", "json"
        ]));

        Assert.False(File.Exists(Path.Combine(outputOne, "sql-impact-report.md")));
        var first = await File.ReadAllTextAsync(Path.Combine(outputOne, "sql-impact-report.json"));
        var second = await File.ReadAllTextAsync(Path.Combine(outputTwo, "sql-impact-report.json"));
        Assert.Equal(first, second);
        Assert.Contains("\"changeCount\": 0", first);
    }

    private static async Task<int> RunCliAsync(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(args, output, error);
        Assert.Equal(string.Empty, error.ToString());
        return exitCode;
    }

    private static string WriteSqlSchemaDelta(string directory, string changesJson)
    {
        var path = Path.Combine(directory, $"sql-schema-delta-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, $$"""
            {
              "version": "sql-schema-delta.v1",
              "source": {
                "name": "release",
                "kind": "migration-review"
              },
              "changes": [
                {{changesJson}}
              ]
            }
            """);
        return path;
    }

    private static string WriteContractDelta(string directory)
    {
        var path = Path.Combine(directory, $"contract-delta-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "version": "contract-delta-v2",
              "changes": [
                {
                  "id": "chg-contract",
                  "kind": "property",
                  "changeType": "removed",
                  "reference": {
                    "propertyName": "Status"
                  }
                }
              ]
            }
            """);
        return path;
    }

    private static ScanManifest Manifest(
        string repoName,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded")
    {
        return new ScanManifest(
            $"scan-{repoName}",
            repoName,
            "https://example.invalid/repo.git",
            "main",
            "abc123",
            scannerVersion,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            analysisLevel,
            buildStatus,
            [],
            ["src/Sample.csproj"],
            ["net10.0"],
            []);
    }
}
