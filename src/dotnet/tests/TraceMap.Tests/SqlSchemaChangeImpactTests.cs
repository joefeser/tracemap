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
    public async Task Reduce_sql_schema_delta_matches_postgres_constraint_and_index_metadata_as_review_tier()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteSqlSchemaDelta(temp.Path, """
            {
              "id": "chg-records-index",
              "kind": "table",
              "changeType": "index_changed",
              "reference": {
                "tableName": "records"
              }
            },
            {
              "id": "chg-records-constraint",
              "kind": "column",
              "changeType": "constraint_changed",
              "reference": {
                "tableName": "records",
                "columnName": "archive_key"
              }
            }
            """);
        var manifest = Manifest("schema", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.PostgresSchemaIndexDeclared,
                RuleIds.DatabasePostgresSchemaMigration,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("sql/schema.sql", 10, 11, null, "postgres-schema-migration", ScannerVersions.PostgresSchemaMigrationExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["columnNames"] = "archive_key",
                    ["indexName"] = "records_archive_key_idx",
                    ["tableName"] = "records"
                }),
            FactFactory.Create(
                manifest,
                FactTypes.PostgresSchemaConstraintDeclared,
                RuleIds.DatabasePostgresSchemaMigration,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("sql/schema.sql", 4, 4, null, "postgres-schema-migration", ScannerVersions.PostgresSchemaMigrationExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["columnNames"] = "archive_key",
                    ["constraintName"] = "records_archive_key_unique",
                    ["tableName"] = "records"
                })
        ]);

        var exitCode = await RunCliAsync([
            "reduce",
            "--index", indexPath,
            "--sql-schema-delta", deltaPath,
            "--out", outputPath
        ]);

        Assert.Equal(0, exitCode);
        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-impact-report.json"));
        Assert.Contains("\"factType\": \"PostgresSchemaIndexDeclared\"", json);
        Assert.Contains("\"factType\": \"PostgresSchemaConstraintDeclared\"", json);
        Assert.Contains("\"evidenceKind\": \"sql-schema-metadata\"", json);
        Assert.Equal(2, CountOccurrences(json, "\"classification\": \"NeedsReview\""));
        Assert.DoesNotContain("\"classification\": \"ProbableImpact\"", json);
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
    public async Task Reduce_sql_schema_delta_combined_index_matches_projected_sql_query_and_persistence_surfaces()
    {
        using var temp = new TempDirectory();
        var apiIndex = Path.Combine(temp.Path, "api.sqlite");
        var workerIndex = Path.Combine(temp.Path, "worker.sqlite");
        var combinedIndex = Path.Combine(temp.Path, "combined.sqlite");
        var outputOne = Path.Combine(temp.Path, "out-one");
        var outputTwo = Path.Combine(temp.Path, "out-two");
        var deltaPath = WriteSqlSchemaDelta(temp.Path, """
            {
              "id": "chg-query-shape",
              "kind": "query-shape",
              "changeType": "shape_changed",
              "reference": {
                "queryShapeHash": "shape-orders-status",
                "sqlSourceKind": "inline",
                "tableName": "Orders",
                "columnNames": "Status"
              }
            },
            {
              "id": "chg-orders-sql-file",
              "kind": "sql-file",
              "changeType": "behavior_changed",
              "reference": {
                "sqlResourceName": "orders-report.sql",
                "sqlSourceKind": "sql-file"
              }
            },
            {
              "id": "chg-orders-status-column",
              "kind": "column",
              "changeType": "type_changed",
              "reference": {
                "tableName": "Orders",
                "columnName": "Status"
              }
            },
            {
              "id": "chg-orders-status-mapping",
              "kind": "mapping",
              "changeType": "nullable_changed",
              "reference": {
                "tableName": "Orders",
                "columnName": "Status",
                "mappedName": "OrderStatus"
              }
            }
            """);
        var apiManifest = Manifest("api", ScannerVersions.TraceMap);
        var workerManifest = Manifest("worker", "tracemap-python/0.1.0");
        SqliteIndexWriter.Write(apiIndex, apiManifest, [
            FactFactory.Create(
                apiManifest,
                FactTypes.QueryPatternDetected,
                RuleIds.DatabaseSqlShape,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/OrdersRepository.cs", 21, 21, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["columnNames"] = "Status;Id",
                    ["operationName"] = "select",
                    ["queryShapeHash"] = "shape-orders-status",
                    ["rawSql"] = "select Status from Orders where Password = 'secret'",
                    ["sqlSourceKind"] = "inline",
                    ["tableName"] = "Orders",
                    ["textHash"] = "text-orders-status"
                }),
            FactFactory.Create(
                apiManifest,
                FactTypes.SqlFileDeclared,
                RuleIds.FileInventory,
                EvidenceTiers.Tier3SyntaxOrTextual,
                new EvidenceSpan("sql/orders-report.sql", 1, 1, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sqlResourceName"] = "orders-report.sql",
                    ["sqlSourceKind"] = "sql-file"
                })
        ]);
        SqliteIndexWriter.Write(workerIndex, workerManifest, [
            FactFactory.Create(
                workerManifest,
                FactTypes.DatabaseColumnMapping,
                RuleIds.DatabaseEntityFramework,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("app/models.py", 7, 7, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["columnName"] = "Status",
                    ["mappedName"] = "OrderStatus",
                    ["surfaceKind"] = "sql-persistence",
                    ["tableName"] = "Orders"
                })
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([apiIndex, workerIndex], combinedIndex, ["api", "worker"]));

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", combinedIndex, "--sql-schema-delta", deltaPath, "--out", outputOne
        ]));
        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", combinedIndex, "--sql-schema-delta", deltaPath, "--out", outputTwo
        ]));

        var first = await File.ReadAllTextAsync(Path.Combine(outputOne, "sql-impact-report.json"));
        var second = await File.ReadAllTextAsync(Path.Combine(outputTwo, "sql-impact-report.json"));
        Assert.Equal(first, second);
        Assert.Contains("\"reportType\": \"SqlSchemaChangeImpactCombinedV1\"", first);
        Assert.Contains("\"changeId\": \"chg-orders-sql-file\"", first);
        Assert.Contains("\"changeId\": \"chg-query-shape\"", first);
        Assert.Contains("\"changeId\": \"chg-orders-status-column\"", first);
        Assert.Contains("\"changeId\": \"chg-orders-status-mapping\"", first);
        Assert.Contains("\"evidenceKind\": \"sql-query-shape\"", first);
        Assert.Contains("\"evidenceKind\": \"sql-resource\"", first);
        Assert.Contains("\"evidenceKind\": \"sql-persistence-mapping\"", first);
        Assert.Contains("\"queryShapeHash\": \"shape-orders-status\"", first);
        Assert.Contains("\"textHash\": \"text-orders-status\"", first);
        Assert.Contains("\"sqlSourceKind\": \"inline\"", first);
        Assert.Contains("\"sqlResourceName\": \"orders-report.sql\"", first);
        Assert.Contains("\"mappedName\": \"OrderStatus\"", first);
        Assert.Contains("\"sourceLabel\": \"api\"", first);
        Assert.Contains("\"sourceLabel\": \"worker\"", first);
        Assert.Contains("\"scanId\": \"scan-api\"", first);
        Assert.Contains("\"scanId\": \"scan-worker\"", first);
        Assert.DoesNotContain("Password", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select Status", first, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reduce_sql_schema_delta_combined_query_selector_does_not_overclaim_mapping_only_evidence()
    {
        using var temp = new TempDirectory();
        var apiIndex = Path.Combine(temp.Path, "api.sqlite");
        var combinedIndex = Path.Combine(temp.Path, "combined.sqlite");
        var queryOutput = Path.Combine(temp.Path, "query-out");
        var mappingOutput = Path.Combine(temp.Path, "mapping-out");
        var queryDeltaPath = WriteSqlSchemaDelta(temp.Path, """
            {
              "id": "chg-query-table-only",
              "kind": "query-shape",
              "changeType": "shape_changed",
              "reference": {
                "sqlSourceKind": "inline",
                "tableName": "Orders"
              }
            }
            """);
        var mappingDeltaPath = WriteSqlSchemaDelta(temp.Path, """
            {
              "id": "chg-mapping-only",
              "kind": "mapping",
              "changeType": "nullable_changed",
              "reference": {
                "mappedName": "OrderStatus"
              }
            }
            """);
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(apiIndex, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.DatabaseColumnMapping,
                RuleIds.DatabaseEntityFramework,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/Order.cs", 8, 8, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["columnName"] = "Status",
                    ["mappedName"] = "OrderStatus",
                    ["surfaceKind"] = "sql-persistence",
                    ["tableName"] = "Orders"
                }),
            FactFactory.Create(
                manifest,
                FactTypes.QueryPatternDetected,
                RuleIds.DatabaseSqlShape,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/CustomerRepository.cs", 18, 18, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["queryShapeHash"] = "shape-customers",
                    ["sqlSourceKind"] = "inline",
                    ["tableName"] = "Customers"
                })
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([apiIndex], combinedIndex, ["api"]));

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", combinedIndex, "--sql-schema-delta", queryDeltaPath, "--out", queryOutput
        ]));
        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", combinedIndex, "--sql-schema-delta", mappingDeltaPath, "--out", mappingOutput
        ]));

        var queryJson = await File.ReadAllTextAsync(Path.Combine(queryOutput, "sql-impact-report.json"));
        var mappingJson = await File.ReadAllTextAsync(Path.Combine(mappingOutput, "sql-impact-report.json"));
        Assert.Contains("\"classification\": \"NoImpactEvidence\"", queryJson);
        Assert.DoesNotContain("\"evidenceKind\": \"sql-persistence-mapping\"", queryJson);
        Assert.DoesNotContain("\"evidenceKind\": \"sql-query-shape\"", queryJson);
        Assert.Contains("\"classification\": \"NeedsReviewImpact\"", mappingJson);
        Assert.Contains("\"evidenceKind\": \"sql-persistence-mapping\"", mappingJson);
        Assert.DoesNotContain("\"evidenceKind\": \"sql-query-shape\"", mappingJson);
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

    [Fact]
    public async Task Reduce_sql_schema_delta_kind_filter_keeps_sql_input_kinds_distinct()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteSqlSchemaDelta(temp.Path, """
            {
              "id": "chg-query-shape",
              "kind": "query-shape",
              "changeType": "shape_changed",
              "reference": {
                "queryShapeHash": "shape123"
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
                    ["queryShapeHash"] = "shape123"
                }),
            FactFactory.Create(
                manifest,
                FactTypes.SqlFileDeclared,
                RuleIds.FileInventory,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("sql/orders-report.sql", 1, 1, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sqlResourceName"] = "orders-report.sql"
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--sql-schema-delta", deltaPath, "--out", outputPath, "--kind", "query-shape"
        ]));

        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-impact-report.json"));
        Assert.Contains("\"changeCount\": 1", json);
        Assert.Contains("\"changeId\": \"chg-query-shape\"", json);
        Assert.DoesNotContain("\"changeId\": \"chg-sql-file\"", json);
    }

    [Fact]
    public async Task Reduce_sql_schema_delta_matches_accepted_table_column_aliases()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteSqlSchemaDelta(temp.Path, """
            {
              "id": "chg-table-alias",
              "kind": "table",
              "changeType": "removed",
              "reference": {
                "tableNames": "Invoices;Orders"
              }
            },
            {
              "id": "chg-column-alias",
              "kind": "column",
              "changeType": "type_changed",
              "reference": {
                "tableName": "Orders",
                "columnNames": "State;Status"
              }
            },
            {
              "id": "chg-mapped-alias",
              "kind": "column",
              "changeType": "nullable_changed",
              "reference": {
                "tableName": "Orders",
                "mappedName": "Status"
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
                    ["columnNames"] = "Id;Status",
                    ["tableNames"] = "Orders;OrderLines"
                }),
            FactFactory.Create(
                manifest,
                FactTypes.DatabaseColumnMapping,
                RuleIds.DatabaseEntityFramework,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/Order.cs", 8, 8, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["mappedName"] = "Status",
                    ["tableName"] = "Orders"
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--sql-schema-delta", deltaPath, "--out", outputPath
        ]));

        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-impact-report.json"));
        Assert.Contains("\"changeId\": \"chg-table-alias\"", json);
        Assert.Contains("\"changeId\": \"chg-column-alias\"", json);
        Assert.Contains("\"changeId\": \"chg-mapped-alias\"", json);
        Assert.DoesNotContain("\"classification\": \"NoEvidenceFullCoverage\"", json);
    }

    [Fact]
    public async Task Reduce_sql_schema_delta_persistence_surface_requires_all_supplied_mapping_keys()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteSqlSchemaDelta(temp.Path, """
            {
              "id": "chg-orders-status",
              "kind": "mapping",
              "changeType": "nullable_changed",
              "reference": {
                "tableName": "Orders",
                "columnName": "Status"
              }
            }
            """);
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.DatabaseColumnMapping,
                RuleIds.DatabaseEntityFramework,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/Order.cs", 8, 8, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["columnName"] = "Total",
                    ["surfaceKind"] = "sql-persistence",
                    ["tableName"] = "Orders"
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--sql-schema-delta", deltaPath, "--out", outputPath
        ]));

        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-impact-report.json"));
        Assert.Contains("\"classification\": \"NoEvidenceFullCoverage\"", json);
        Assert.DoesNotContain("\"factType\": \"DatabaseColumnMapping\"", json);
    }

    [Fact]
    public async Task Reduce_sql_schema_delta_exit_code_does_not_fail_for_review_tier_only()
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
                    ["tableName"] = "Orders"
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--sql-schema-delta", deltaPath, "--out", outputPath, "--exit-code"
        ]));

        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-impact-report.json"));
        Assert.Contains("\"classification\": \"NeedsReview\"", json);
    }

    [Fact]
    public async Task Reduce_contract_delta_keeps_legacy_confidence_strings()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteContractDelta(temp.Path);
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.PropertyAccessed,
                RuleIds.CSharpSemanticPropertyAccess,
                EvidenceTiers.Tier1Semantic,
                new EvidenceSpan("src/OrderReader.cs", 10, 10, null, "test", "test/1.0"),
                contractElement: "Status",
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["propertyName"] = "Status"
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outputPath
        ]));

        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.json"));
        Assert.Contains("\"confidence\": \"high\"", json);
        Assert.DoesNotContain("\"confidence\": \"High\"", json);
    }

    private static async Task<int> RunCliAsync(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(args, output, error);
        Assert.Equal(string.Empty, error.ToString());
        return exitCode;
    }

    private static int CountOccurrences(string value, string expected) =>
        value.Split(expected, StringSplitOptions.None).Length - 1;

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
