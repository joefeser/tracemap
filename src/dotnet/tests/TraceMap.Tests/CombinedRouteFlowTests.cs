using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CombinedRouteFlowTests
{
    [Fact]
    public async Task Route_flow_writes_route_centered_markdown_and_json_without_mutating_combined_index()
    {
        using var temp = new TempDirectory();
        var (combinedPath, controller, _) = await CreateRouteFlowCombinedIndexAsync(temp);
        var outDir = Path.Combine(temp.Path, "route-flow");
        var before = await CombinedIndexFingerprintAsync(combinedPath);

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            outDir,
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Equal(before, await CombinedIndexFingerprintAsync(combinedPath));
        Assert.True(File.Exists(Path.Combine(outDir, "route-flow-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "route-flow-report.json")));
        Assert.Equal("route-flow", result.Report.ReportType);
        Assert.Equal(result.Report.ReportCoverage, result.Report.Summary.ReportCoverage);
        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root");
        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "aligned-route-pair");
        Assert.DoesNotContain(result.Report.FlowRows, row => row.EdgeKind == "client-server-alignment");
        Assert.Contains(result.Report.FlowRows, row => row.EdgeKind == "direct-call" && row.SourceSymbol.Contains(controller, StringComparison.Ordinal));
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query");
        Assert.Contains(result.Report.LogicRows, row => row.LogicKind == "query-filter-sort-selection");
        Assert.Contains(result.Report.LogicRows, row => row.LogicKind == "argument-flow" && row.Evidence.RuleId == "combined.route-flow.argument-projection.v1");
        Assert.Contains(result.Report.LogicRows, row => row.LogicKind == "query-shape" && row.Evidence.RuleId == "combined.route-flow.fact-symbol-projection.v1");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "ExtractorUnavailable");
        Assert.Contains(result.Report.Snapshot.Sources, source => source.ScannerVersion == "tracemap-milestone15");
        Assert.All(result.Report.FlowRows, row =>
        {
            Assert.StartsWith("combined.route-flow.", row.Evidence.RuleId, StringComparison.Ordinal);
            Assert.NotEmpty(row.Evidence.SupportingRuleIds);
            Assert.False(string.IsNullOrWhiteSpace(row.Evidence.CommitSha));
            Assert.False(string.IsNullOrWhiteSpace(row.Evidence.ExtractorVersion));
        });
        Assert.DoesNotContain(result.Report.DependencySurfaces, surface => surface.StableKey.Contains("fact-", StringComparison.OrdinalIgnoreCase));
        Assert.All(result.Report.DependencySurfaces, surface =>
        {
            Assert.StartsWith("surface-key-hash:", surface.StableKey, StringComparison.Ordinal);
            Assert.DoesNotContain("orders", surface.StableKey, StringComparison.OrdinalIgnoreCase);
        });

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "route-flow-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "route-flow-report.json"));
        Assert.Contains("TraceMap Route Flow Report", markdown);
        Assert.Contains("static route-flow evidence", markdown);
        Assert.Contains("candidate implementation", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select * from", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=private", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);
        AssertForbiddenRuntimeWording(markdown);
        AssertForbiddenRuntimeWording(json);

        var parsed = JsonSerializer.Deserialize<RouteFlowReport>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(parsed);
        var sqlSurface = parsed!.DependencySurfaces.Single(surface => surface.SurfaceKind == "sql-query");
        Assert.NotEmpty(sqlSurface.Evidence.SupportingFactIds);
        Assert.Contains(RuleIds.CSharpSyntaxQueryPattern, sqlSurface.Evidence.SupportingRuleIds);
        Assert.Equal("tracemap-milestone15", sqlSurface.Evidence.ExtractorVersion);
        Assert.Contains("tableNameHash", sqlSurface.SafeMetadata.Keys);
        Assert.Contains("columnNamesHash", sqlSurface.SafeMetadata.Keys);
        Assert.DoesNotContain("tableName", sqlSurface.SafeMetadata.Keys);
        Assert.DoesNotContain("columnNames", sqlSurface.SafeMetadata.Keys);
        Assert.DoesNotContain("orders", JsonSerializer.Serialize(sqlSurface.SafeMetadata), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id;status", JsonSerializer.Serialize(sqlSurface.SafeMetadata), StringComparison.OrdinalIgnoreCase);
        var argumentProjection = parsed.LogicRows.Single(row => row.Evidence.RuleId == "combined.route-flow.argument-projection.v1");
        Assert.Equal("argument-projection", argumentProjection.AttachmentKind);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, argumentProjection.Classification);
        Assert.Contains("parameterName", argumentProjection.SafeMetadata.Keys);
        Assert.StartsWith("parameter-name-hash:", argumentProjection.SafeMetadata["parameterName"], StringComparison.Ordinal);
        Assert.DoesNotContain("apiSecret", JsonSerializer.Serialize(argumentProjection.SafeMetadata), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("argumentSymbol", argumentProjection.SafeMetadata.Keys);
        Assert.Contains(argumentProjection.Evidence.SupportingRuleIds, rule => rule == RuleIds.CSharpSemanticValueFlow);
        Assert.Contains(argumentProjection.Evidence.SupportingRuleIds, rule => rule == "combined.route-flow.redaction.v1");
        var factSymbolProjection = parsed.LogicRows.Single(row => row.Evidence.RuleId == "combined.route-flow.fact-symbol-projection.v1");
        Assert.Equal("fact-symbol-projection", factSymbolProjection.AttachmentKind);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, factSymbolProjection.Classification);
        Assert.Contains(factSymbolProjection.Evidence.SupportingRuleIds, rule => rule == RuleIds.CSharpSyntaxQueryPattern);
        Assert.Contains(factSymbolProjection.Evidence.SupportingRuleIds, rule => rule == "combined.route-flow.redaction.v1");
        Assert.Contains("tableNameHash", factSymbolProjection.SafeMetadata.Keys);
        Assert.DoesNotContain("orders", JsonSerializer.Serialize(factSymbolProjection.SafeMetadata), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(parsed.LogicRows, row => row.SafeMetadata.TryGetValue("factType", out var factType)
            && factType == FactTypes.ConnectionStringDeclared);

        var secondOutDir = outDir;
        await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            secondOutDir,
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "route-flow-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "route-flow-report.json")));
    }

    [Fact]
    public async Task Route_flow_json_file_output_and_classification_filter_empty_rows_yields_selector_gap()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp);
        var outputPath = Path.Combine(temp.Path, "route-flow.json");

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            outputPath,
            Format: "json",
            Route: "GET /api/orders/%7Bid%7D",
            ToSurface: "sql-query",
            Classification: RouteFlowClassifications.NoRouteFlowEvidence));

        Assert.Null(result.MarkdownPath);
        Assert.Equal(outputPath, result.JsonPath);
        Assert.Empty(result.Report.FlowRows);
        Assert.Empty(result.Report.DependencySurfaces);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch");
        var json = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("\"reportType\": \"route-flow\"", json);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Route_flow_query_omits_raw_route_selector_values()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp);
        const string rawSelector = "GET https://example.test/api/orders/{id}?token=secret-token";

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: rawSelector,
            ToSurface: "sql-query"));

        Assert.Equal("GET /api/orders/{}", result.Report.Query.Route);
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "route-flow", "route-flow-report.json"));
        Assert.DoesNotContain(rawSelector, json, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Route_flow_rejects_single_language_index()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "server.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        SqliteIndexWriter.Write(indexPath, manifest, [
            RouteFact(manifest, "GET", "/api/orders/{id}", "/api/orders/{}", "Server.OrdersController.Get(System.Int32)", "Controllers/OrdersController.cs", 10)
        ]);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CombinedRouteFlowReporter.BuildReportAsync(new CombinedRouteFlowOptions(
            indexPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}")));
        Assert.Contains("requires a combined index", error.Message);
    }

    [Fact]
    public async Task Route_flow_identity_reduced_coverage_gap_reduces_report_coverage()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp, serverBuildStatus: "Failed");

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Equal("ReducedCoverage", result.Report.ReportCoverage);
        Assert.Equal("ReducedCoverage", result.Report.Summary.ReportCoverage);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ReducedCoverage" && gap.SourceLabel == "server");
    }

    [Fact]
    public async Task Route_flow_cli_exit_code_follows_summary_mapping()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp);
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync([
            "route-flow",
            "--index", combinedPath,
            "--out", Path.Combine(temp.Path, "route-flow"),
            "--route", "GET /api/orders/{id}",
            "--to-surface", "sql-query",
            "--exit-code"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Classification:", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Route_flow_optional_projection_tables_emit_scoped_gaps_when_rows_cannot_join_selected_path()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateUnjoinableProjectionCombinedIndexAsync(temp);

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}"));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ArgumentProjectionUnavailable");
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "FactSymbolProjectionUnavailable");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "ExtractorUnavailable");
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.RuleId is "combined.route-flow.argument-projection.v1" or "combined.route-flow.fact-symbol-projection.v1");
    }

    [Fact]
    public void Route_flow_rule_catalog_resolves_projection_rule_ids()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var ruleId in new[]
        {
            "combined.route-flow.selector.v1",
            "combined.route-flow.entry.v1",
            "combined.route-flow.path.v1",
            "combined.route-flow.interface-bridge.v1",
            "combined.route-flow.logic-surface.v1",
            "combined.route-flow.dependency-surface.v1",
            "combined.route-flow.argument-projection.v1",
            "combined.route-flow.fact-symbol-projection.v1",
            "combined.route-flow.classification.v1",
            "combined.route-flow.gap.v1",
            "combined.route-flow.redaction.v1",
            "combined.route-flow.report.v1"
        })
        {
            Assert.Contains($"- id: {ruleId}", catalog);
        }

        Assert.DoesNotContain("- id: route.flow.", catalog, StringComparison.Ordinal);
    }

    private static async Task<(string CombinedPath, string Controller, string Repository)> CreateRouteFlowCombinedIndexAsync(TempDirectory temp, string serverBuildStatus = "Succeeded")
    {
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15", buildStatus: serverBuildStatus);
        var clientMethod = "Client.OrderService.loadOrder(System.Int32)";
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.IOrderService.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5, clientMethod)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            ArgumentPassedFact(server, controller, service, "apiSecret", "apiSecret", "System.String", "Controllers/OrdersController.cs", 14),
            CallFact(server, service, repository, "Services/OrderService.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true),
            ConnectionStringFact(server, repository, "Infrastructure/OrderRepository.cs", 32)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));
        return (combinedPath, controller, repository);
    }

    private static async Task<string> CreateUnjoinableProjectionCombinedIndexAsync(TempDirectory temp)
    {
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var unrelatedCaller = "Server.Unrelated.Start(System.Int32)";
        var unrelatedCallee = "Server.Unrelated.Finish(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31),
            ArgumentPassedFact(server, unrelatedCaller, unrelatedCallee, "id", "id", "System.Int32", "Services/Unrelated.cs", 20),
            QueryPatternFact(server, unrelatedCallee, "Infrastructure/UnrelatedRepository.cs", 41, attachSymbol: true),
            QueryPatternFact(server, unrelatedCallee, "Infrastructure/MisleadingTarget.cs", 42, attachSymbol: true, targetSymbol: repository)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));
        return combinedPath;
    }

    private static ScanManifest Manifest(string repo, string scannerVersion, string buildStatus = "Succeeded")
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            null,
            "main",
            "abc123",
            scannerVersion,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            "Level1SemanticAnalysis",
            buildStatus,
            [],
            [],
            [],
            [],
            ".",
            FactFactory.Hash(repo, 32),
            FactFactory.Hash("git-root", 32));
    }

    private static CodeFact HttpClientFact(ScanManifest manifest, string method, string template, string key, string file, int line, string? sourceSymbol = null)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: $"{method} {template}",
            contractElement: method,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethod"] = method,
                ["methodName"] = method,
                ["normalizedPathTemplate"] = template,
                ["normalizedPathKey"] = key,
                ["urlKind"] = "template",
                ["clientFramework"] = "test"
            });
    }

    private static CodeFact RouteFact(ScanManifest manifest, string method, string template, string key, string methodSymbol, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: methodSymbol,
            targetSymbol: methodSymbol,
            contractElement: template,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethods"] = method,
                ["methodName"] = method,
                ["normalizedPathTemplate"] = template,
                ["normalizedPathKey"] = key,
                ["routeTemplates"] = template
            });
    }

    private static CodeFact CallFact(ScanManifest manifest, string caller, string callee, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.CallEdge,
            RuleIds.CSharpSemanticCallGraph,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: caller,
            targetSymbol: callee,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["callKind"] = "method"
            });
    }

    private static CodeFact ArgumentPassedFact(
        ScanManifest manifest,
        string caller,
        string callee,
        string argumentSymbol,
        string parameterName,
        string parameterType,
        string file,
        int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ArgumentPassed,
            RuleIds.CSharpSemanticValueFlow,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: caller,
            targetSymbol: callee,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["argumentExpressionHash"] = "arg-hash",
                ["argumentExpressionKind"] = "IdentifierName",
                ["argumentOrdinal"] = "0",
                ["argumentSymbol"] = argumentSymbol,
                ["argumentSymbolKind"] = "Parameter",
                ["argumentType"] = parameterType,
                ["callKind"] = "method",
                ["parameterName"] = parameterName,
                ["parameterOrdinal"] = "0",
                ["parameterType"] = parameterType
            });
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string? sourceSymbol, string file, int line, bool attachSymbol = false, string targetSymbol = "orders")
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["operationName"] = "SELECT",
            ["tableName"] = "orders",
            ["columnNames"] = "id;status",
            ["sqlSourceKind"] = "literal-string",
            ["queryShapeHash"] = "shape123"
        };
        if (attachSymbol && sourceSymbol is not null)
        {
            properties["sourceSymbolId"] = sourceSymbol;
            properties["sourceSymbolDisplayName"] = sourceSymbol;
            properties["sourceSymbolKind"] = "Method";
            properties["sourceSymbolLanguage"] = "csharp";
        }

        return FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: targetSymbol,
            properties: properties);
    }

    private static CodeFact ConnectionStringFact(ScanManifest manifest, string sourceSymbol, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ConnectionStringDeclared,
            RuleIds.ConfigKey,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: "ConnectionStrings:Orders",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["configKey"] = "ConnectionStrings:Orders",
                ["connectionName"] = "Orders",
                ["connectionStringHash"] = "conn-shape-hash",
                ["sourceSymbolId"] = sourceSymbol,
                ["sourceSymbolDisplayName"] = sourceSymbol,
                ["sourceSymbolKind"] = "Method",
                ["sourceSymbolLanguage"] = "csharp",
                ["value"] = "Server=private;Password=secret;"
            });
    }

    private static async Task<string> CombinedIndexFingerprintAsync(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var parts = new List<string>
        {
            $"integrity:{await ScalarAsync(connection, "pragma integrity_check;")}",
            $"schema:{await ScalarAsync(connection, "select group_concat(type || ':' || name || ':' || coalesce(sql, ''), char(10)) from sqlite_master where name not like 'sqlite_%' order by type, name;")}"
        };

        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "select name from sqlite_master where type = 'table' and name not like 'sqlite_%' order by name;";
        await using var reader = await tableCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            parts.Add($"{table}:{await CountRowsAsync(connection, table)}");
        }

        return string.Join("\n", parts);
    }

    private static async Task<string> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {tableName};";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static void AssertForbiddenRuntimeWording(string value)
    {
        Assert.DoesNotContain("executed", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("impacted", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("called at runtime", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorized", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("used in production", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("query runs", value, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
