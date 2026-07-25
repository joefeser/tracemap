using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class DatabaseDesignReviewTests
{
    [Fact]
    public async Task Packet_groups_postgres_design_and_preserves_proven_query_route_evidence_safely()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var firstOutput = Path.Combine(temp.Path, "first");
        var secondOutput = Path.Combine(temp.Path, "second");
        var manifest = Manifest("server");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var protectedValue = "Server=private.internal;Password=do-not-render";

        SqliteIndexWriter.Write(index, manifest,
        [
            RouteFact(manifest, controller),
            CallFact(manifest, controller, repository),
            QueryFact(manifest, repository, "public.orders", protectedValue),
            PostgresFact(manifest, FactTypes.PostgresSchemaTableDeclared, 20,
                ("objectKind", "table"), ("operationKind", "create-table"), ("schemaName", "public"), ("tableName", "orders")),
            PostgresFact(manifest, FactTypes.PostgresSchemaColumnDeclared, 21,
                ("objectKind", "column"), ("operationKind", "create-table"), ("schemaName", "public"), ("tableName", "orders"), ("columnName", "status")),
            PostgresFact(manifest, FactTypes.PostgresSchemaConstraintDeclared, 22,
                ("objectKind", "constraint"), ("schemaName", "public"), ("tableName", "orders"), ("constraintName", "pk_orders"), ("constraintKind", "primary-key"), ("columnNames", "id")),
            PostgresFact(manifest, FactTypes.PostgresSchemaIndexDeclared, 23,
                ("objectKind", "index"), ("schemaName", "public"), ("tableName", "orders"), ("indexName", "ix_orders_status"), ("indexKind", "index"), ("columnNames", "status")),
            PostgresFact(manifest, FactTypes.PostgresMigrationOperation, 24,
                ("objectKind", "migration-operation"), ("operationKind", "drop-column"), ("schemaName", "public"), ("tableName", "orders"), ("columnName", "obsolete"), ("dropBehavior", "restrict")),
            PostgresFact(manifest, FactTypes.PostgresSchemaEnumDeclared, 30,
                ("objectKind", "enum"), ("schemaName", "public"), ("enumName", "order_state"), ("enumLabelsOmitted", "true")),
            PostgresFact(manifest, FactTypes.PostgresSchemaRoutineDeclared, 31,
                ("objectKind", "routine"), ("schemaName", "public"), ("routineName", "archive_orders"), ("routineKind", "function"), ("routineBodyOmitted", "true")),
            PostgresFact(manifest, FactTypes.PostgresMigrationOperation, 32,
                ("objectKind", "migration-operation"), ("operationKind", "create-enum"), ("schemaName", "public"), ("enumName", "order_state")),
            PostgresFact(manifest, FactTypes.PostgresMigrationOperation, 33,
                ("objectKind", "migration-operation"), ("operationKind", "create-routine"), ("schemaName", "public"), ("routineName", "archive_orders"), ("routineKind", "function")),
            PostgresFact(manifest, FactTypes.PostgresSchemaSnapshotDeclared, 1,
                ("objectKind", "schema-snapshot"), ("snapshotFormat", "pg-dump"), ("recognizedDdlStatementCount", "6"), ("unsupportedDdlStatementCount", "1"), ("sourceDatabaseIdentityOmitted", "true")),
            PostgresGap(manifest, 40, "SnapshotDdlCoverageReduced")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["server"]));

        var first = await DatabaseDesignReviewReporter.WriteAsync(new DatabaseDesignReviewOptions(combined, firstOutput));
        var second = await DatabaseDesignReviewReporter.WriteAsync(new DatabaseDesignReviewOptions(combined, secondOutput));

        var table = Assert.Single(first.Report.Tables);
        Assert.Equal("public", table.SchemaName);
        Assert.Equal("orders", table.TableName);
        Assert.Contains(table.Declarations, row => row.EvidenceKind == "column" && row.DisplayName == "status");
        Assert.Contains(table.Declarations, row => row.EvidenceKind == "constraint" && row.DisplayName == "pk_orders");
        Assert.Contains(table.Declarations, row => row.EvidenceKind == "index" && row.DisplayName == "ix_orders_status");
        Assert.DoesNotContain(table.Declarations, row => row.EvidenceKind == "migration-operation");
        Assert.Contains(table.Operations, row => row.DisplayName == "drop-column");
        var query = Assert.Single(table.QueryReferences);
        Assert.Equal("StaticNameMatch", query.Classification);
        var route = Assert.Single(table.RouteReferences);
        Assert.Equal("static-name-match", route.TableMatchKind);
        Assert.NotEmpty(route.Evidence.SupportingFactIds);
        Assert.NotEmpty(route.Evidence.SupportingEdgeIds);
        Assert.Contains(route.Evidence.SupportingRuleIds, rule => rule == RuleIds.CSharpSemanticCallGraph);
        Assert.Equal("test-route", route.Evidence.ExtractorId);
        Assert.Equal("test-route/1.0", route.Evidence.ExtractorVersion);
        Assert.Contains(first.Report.GlobalObjects, row => row.EvidenceKind == "snapshot");
        Assert.Contains(first.Report.GlobalObjects, row => row.EvidenceKind == "enum");
        Assert.Contains(first.Report.GlobalObjects, row => row.EvidenceKind == "routine");
        Assert.Contains(first.Report.GlobalObjects, row => row.EvidenceKind == "migration-operation"
            && row.Metadata.Any(pair => pair.Key == "enumName" && pair.Value == "order_state"));
        Assert.Contains(first.Report.GlobalObjects, row => row.EvidenceKind == "migration-operation"
            && row.Metadata.Any(pair => pair.Key == "routineName" && pair.Value == "archive_orders"));
        Assert.Contains(first.Report.Gaps, gap => gap.GapKind == "SnapshotDdlCoverageReduced");
        Assert.DoesNotContain(first.Report.Gaps, gap => gap.GapKind == "QueryRoutePathUnavailable");
        Assert.All(table.Declarations.Concat(table.Operations).Concat(table.QueryReferences), row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Evidence.RuleId));
            Assert.Equal(manifest.CommitSha, row.Evidence.CommitSha);
            Assert.False(string.IsNullOrWhiteSpace(row.Evidence.ExtractorId));
            Assert.False(string.IsNullOrWhiteSpace(row.Evidence.ExtractorVersion));
            Assert.NotNull(row.Evidence.FilePath);
            Assert.False(Path.IsPathFullyQualified(row.Evidence.FilePath!));
        });

        var firstJson = await File.ReadAllTextAsync(Path.Combine(firstOutput, "database-design-review.json"));
        var firstMarkdown = await File.ReadAllTextAsync(Path.Combine(firstOutput, "database-design-review.md"));
        Assert.Equal(firstJson, await File.ReadAllTextAsync(Path.Combine(secondOutput, "database-design-review.json")));
        Assert.Equal(firstMarkdown, await File.ReadAllTextAsync(Path.Combine(secondOutput, "database-design-review.md")));
        Assert.DoesNotContain(protectedValue, firstJson, StringComparison.Ordinal);
        Assert.DoesNotContain("shape-secret", firstJson, StringComparison.Ordinal);
        Assert.DoesNotContain("select *", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, firstJson, StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, firstMarkdown, StringComparison.Ordinal);
        Assert.Contains("does not prove", firstMarkdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is safe to run", firstMarkdown, StringComparison.OrdinalIgnoreCase);

        var parsed = JsonSerializer.Deserialize<DatabaseDesignReviewDocument>(
            firstJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(parsed);
        Assert.Equal(DatabaseDesignReviewReporter.PacketRuleId, parsed.RuleId);
        Assert.Equal(DatabaseDesignReviewReporter.PacketRuleId, route.Evidence.RuleId);
    }

    [Fact]
    public async Task Packet_keeps_unmatched_queries_and_incompatible_schema_provenance_as_gaps()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server");
        var incompatible = FactFactory.Create(
            manifest,
            FactTypes.PostgresSchemaTableDeclared,
            RuleIds.DatabasePostgresSchemaMigration,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("db/schema.sql", 1, 1, null, "unknown", "unknown"),
            properties: Properties(("objectKind", "table"), ("schemaName", "public"), ("tableName", "orders")));

        SqliteIndexWriter.Write(index, manifest,
        [
            incompatible,
            QueryFact(manifest, "Server.Repository.Query()", "public.invoices", "not-rendered")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["server"]));

        var report = await DatabaseDesignReviewReporter.BuildReportAsync(
            new DatabaseDesignReviewOptions(combined, Path.Combine(temp.Path, "out")));

        Assert.Equal("unavailable", report.Coverage);
        Assert.Empty(report.Tables);
        Assert.Single(report.UnlinkedQueries);
        Assert.Equal("UnlinkedQuery", report.UnlinkedQueries[0].Classification);
        Assert.Contains(report.UnlinkedQueries[0].Metadata, pair => pair.Key == "matchKind" && pair.Value == "none");
        Assert.Contains(report.Gaps, gap => gap.GapKind == "PostgresEvidenceProvenanceUnavailable");
        Assert.Contains(report.Gaps, gap => gap.GapKind == "QueryTableUnmatched");
        Assert.Contains(report.Gaps, gap => gap.GapKind == "CompatiblePostgresEvidenceUnavailable");
        var provenanceGap = Assert.Single(report.Gaps, gap => gap.GapKind == "PostgresEvidenceProvenanceUnavailable");
        Assert.Equal(manifest.CommitSha, provenanceGap.CommitSha);
        Assert.Equal("db/schema.sql", provenanceGap.FilePath);
        Assert.Equal(1, provenanceGap.StartLine);
        Assert.Equal(1, provenanceGap.EndLine);
        Assert.Null(provenanceGap.ExtractorId);
        Assert.Null(provenanceGap.ExtractorVersion);
        Assert.Contains(provenanceGap.SupportingFactIds, id => id.EndsWith($":{incompatible.FactId}", StringComparison.Ordinal));
        Assert.Contains(incompatible.RuleId, provenanceGap.SupportingRuleIds);

        var capped = await DatabaseDesignReviewReporter.BuildReportAsync(
            new DatabaseDesignReviewOptions(
                combined,
                Path.Combine(temp.Path, "capped"),
                MaxObjects: 10,
                MaxEvidence: 10,
                MaxRouteReferences: 10,
                MaxGaps: 1));
        var truncation = Assert.Single(capped.Gaps);
        Assert.Equal("TruncatedByLimit", truncation.GapKind);
        Assert.Equal(report.Gaps.Count, capped.Summary.OmittedGapCount);
        Assert.Contains(truncation.Metadata, pair => pair.Key == "omittedCount"
            && pair.Value == report.Gaps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Packet_labels_matching_query_without_existing_route_as_an_explicit_gap()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server");
        var query = QueryFact(manifest, "Server.Repository.Query()", "public.orders", "not-rendered") with
        {
            EvidenceTier = "unexpected-tier"
        };
        SqliteIndexWriter.Write(index, manifest,
        [
            PostgresFact(manifest, FactTypes.PostgresSchemaTableDeclared, 1,
                ("objectKind", "table"), ("schemaName", "public"), ("tableName", "orders")),
            query
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["server"]));

        var report = await DatabaseDesignReviewReporter.BuildReportAsync(
            new DatabaseDesignReviewOptions(combined, Path.Combine(temp.Path, "out")));

        var table = Assert.Single(report.Tables);
        Assert.Equal(EvidenceTiers.Tier4Unknown, Assert.Single(table.QueryReferences).Evidence.EvidenceTier);
        Assert.Empty(table.RouteReferences);
        var gap = Assert.Single(report.Gaps, row => row.GapKind == "QueryRoutePathUnavailable");
        Assert.Equal(manifest.CommitSha, gap.CommitSha);
        Assert.Equal("Data/OrderRepository.cs", gap.FilePath);
        Assert.Equal(12, gap.StartLine);
        Assert.Equal("test-query", gap.ExtractorId);
        Assert.Equal("test-query/1.0", gap.ExtractorVersion);
        Assert.Contains(gap.SupportingFactIds, id => id.EndsWith($":{query.FactId}", StringComparison.Ordinal));
        Assert.Contains(query.RuleId, gap.SupportingRuleIds);
    }

    [Fact]
    public async Task Packet_marks_source_warnings_partial_and_counts_observed_route_omissions()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server") with
        {
            AnalysisLevel = "Level3SyntaxAnalysis",
            BuildStatus = "Failed"
        };
        var firstController = "Server.OrdersController.Get(System.Int32)";
        var secondController = "Server.OrdersController.List()";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        SqliteIndexWriter.Write(index, manifest,
        [
            RouteFact(manifest, firstController),
            RouteFact(manifest, secondController),
            CallFact(manifest, firstController, repository),
            CallFact(manifest, secondController, repository),
            QueryFact(manifest, repository, "public.orders", "not-rendered"),
            PostgresFact(manifest, FactTypes.PostgresSchemaTableDeclared, 20,
                ("objectKind", "table"), ("schemaName", "public"), ("tableName", "orders"))
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["server"]));

        var report = await DatabaseDesignReviewReporter.BuildReportAsync(
            new DatabaseDesignReviewOptions(
                combined,
                Path.Combine(temp.Path, "out"),
                MaxObjects: 10,
                MaxEvidence: 10,
                MaxRouteReferences: 1,
                MaxGaps: 20));

        Assert.Equal("partial", report.Coverage);
        Assert.Contains(report.Gaps, gap => gap.GapKind == "SourceCoverageWarning");
        Assert.Equal(1, report.Summary.RouteReferenceCount);
        Assert.Equal(1, report.Summary.OmittedRouteReferenceCount);
        Assert.Contains(report.Gaps, gap => gap.GapKind == "TruncatedByLimit"
            && gap.Metadata.Any(pair => pair.Key == "omittedKind" && pair.Value == "route-references"));
    }

    [Fact]
    public async Task Packet_emits_bounded_truncation_gaps()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server");
        SqliteIndexWriter.Write(index, manifest,
        [
            PostgresFact(manifest, FactTypes.PostgresSchemaTableDeclared, 1, ("objectKind", "table"), ("tableName", "first")),
            PostgresFact(manifest, FactTypes.PostgresSchemaTableDeclared, 2, ("objectKind", "table"), ("tableName", "second")),
            PostgresFact(manifest, FactTypes.PostgresSchemaTableDeclared, 3, ("objectKind", "table"), ("tableName", "third"))
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["server"]));

        var report = await DatabaseDesignReviewReporter.BuildReportAsync(
            new DatabaseDesignReviewOptions(combined, Path.Combine(temp.Path, "out"), MaxObjects: 1, MaxEvidence: 1, MaxRouteReferences: 1, MaxGaps: 10));

        Assert.Equal("partial", report.Coverage);
        Assert.Single(report.Tables);
        Assert.Equal(2, report.Summary.OmittedObjectCount);
        Assert.Contains(report.Gaps, gap => gap.GapKind == "TruncatedByLimit"
            && gap.Metadata.Any(pair => pair.Key == "omittedKind" && pair.Value == "design-objects"));
    }

    [Fact]
    public async Task Cli_exposes_help_and_rejects_single_index_input()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["database-design-review", "--help"], output, error));
        Assert.Contains("database-design-review --index", output.ToString(), StringComparison.Ordinal);

        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "single.sqlite");
        SqliteIndexWriter.Write(index, Manifest("server"), []);
        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        var exit = await TraceMapCommand.RunAsync(
            ["database-design-review", "--index", index, "--out", Path.Combine(temp.Path, "out")],
            output,
            error);
        Assert.Equal(1, exit);
        Assert.Contains("combined index", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Packet_rules_are_cataloged_with_limitations()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var rule in new[] { DatabaseDesignReviewReporter.PacketRuleId, DatabaseDesignReviewReporter.GapRuleId })
        {
            var start = catalog.IndexOf($"  - id: {rule}", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var next = catalog.IndexOf("\n  - id:", start + 1, StringComparison.Ordinal);
            var block = next < 0 ? catalog[start..] : catalog[start..next];
            Assert.Contains("limitations:", block, StringComparison.Ordinal);
        }
    }

    private static ScanManifest Manifest(string repo) =>
        new(
            $"scan-{repo}",
            repo,
            null,
            "main",
            "abcdef1234567890",
            "tracemap-test/1.0",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            "Level1SemanticAnalysis",
            "Succeeded",
            [],
            [],
            [],
            [],
            ".",
            FactFactory.Hash(repo, 32),
            FactFactory.Hash($"git-root:{repo}", 32));

    private static CodeFact PostgresFact(ScanManifest manifest, string factType, int line, params (string Key, string Value)[] properties) =>
        FactFactory.Create(
            manifest,
            factType,
            RuleIds.DatabasePostgresSchemaMigration,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("db/schema.sql", line, line, FactFactory.Hash($"line:{line}", 32), nameof(PostgresSchemaMigrationExtractor), ScannerVersions.PostgresSchemaMigrationExtractor),
            properties: Properties(properties.Concat([("coverageLabel", "bounded-static-evidence"), ("limitations", "Static PostgreSQL evidence only; runtime state is not proven.")]).ToArray()));

    private static CodeFact PostgresGap(ScanManifest manifest, int line, string classification) =>
        FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DatabasePostgresSchemaMigrationGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan("db/schema.sql", line, line, FactFactory.Hash($"gap:{line}", 32), nameof(PostgresSchemaMigrationExtractor), ScannerVersions.PostgresSchemaMigrationExtractor),
            properties: Properties(("classification", classification), ("coverageLabel", "reduced"), ("limitations", "Coverage is partial.")));

    private static CodeFact QueryFact(ScanManifest manifest, string sourceSymbol, string tableName, string protectedValue) =>
        FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("Data/OrderRepository.cs", 12, 12, null, "test-query", "test-query/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: tableName,
            properties: Properties(
                ("operationName", "SELECT"),
                ("tableName", tableName),
                ("columnNames", "id;status"),
                ("sqlSourceKind", "literal-string"),
                ("queryShapeHash", "shape-secret"),
                ("protectedValue", protectedValue)));

    private static CodeFact RouteFact(ScanManifest manifest, string methodSymbol) =>
        FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan("Controllers/OrdersController.cs", 5, 5, null, "test-route", "test-route/1.0"),
            sourceSymbol: methodSymbol,
            targetSymbol: methodSymbol,
            contractElement: "/api/orders/{id}",
            properties: Properties(
                ("httpMethods", "GET"),
                ("methodName", "GET"),
                ("normalizedPathTemplate", "/api/orders/{id}"),
                ("normalizedPathKey", "/api/orders/{}"),
                ("routeTemplates", "/api/orders/{id}")));

    private static CodeFact CallFact(ScanManifest manifest, string caller, string callee) =>
        FactFactory.Create(
            manifest,
            FactTypes.CallEdge,
            RuleIds.CSharpSemanticCallGraph,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan("Controllers/OrdersController.cs", 8, 8, null, "test-call", "test-call/1.0"),
            sourceSymbol: caller,
            targetSymbol: callee,
            properties: Properties(("callKind", "method")));

    private static IReadOnlyDictionary<string, string> Properties(params (string Key, string Value)[] pairs) =>
        new SortedDictionary<string, string>(
            pairs.GroupBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TraceMap.sln"))
                && Directory.Exists(Path.Combine(current.FullName, "..", "..", "rules")))
                return Path.GetFullPath(Path.Combine(current.FullName, "..", ".."));
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(current.FullName, "rules")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
