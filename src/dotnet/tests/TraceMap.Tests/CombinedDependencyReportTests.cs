using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CombinedDependencyReportTests
{
    [Fact]
    public async Task Report_rejects_single_language_index()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outPath = Path.Combine(temp.Path, "report.md");
        SqliteIndexWriter.Write(indexPath, Manifest("single", "tracemap-milestone15"), []);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["report", "--index", indexPath, "--out", outPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("combined index", error.ToString());
        Assert.False(File.Exists(outPath));
    }

    [Fact]
    public async Task Report_writes_markdown_json_and_endpoint_surface_evidence_without_mutating_endpoint_matches()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverOneIndex = Path.Combine(temp.Path, "server-one.sqlite");
        var serverTwoIndex = Path.Combine(temp.Path, "server-two.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var clientManifest = Manifest("client", "tracemap-typescript/0.1.0", analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", knownGaps: ["tsconfig: dependency missing"]);
        var serverOneManifest = Manifest("server-one", "tracemap-milestone15");
        var serverTwoManifest = Manifest("server-two", "tracemap-milestone15");

        SqliteIndexWriter.Write(clientIndex, clientManifest, [
            HttpClientFact(clientManifest, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 8),
            HttpClientFact(clientManifest, "DELETE", "/api/orders/archive", "/api/orders/archive", "src/orders.ts", 12),
            HttpClientFact(clientManifest, "GET", "/api/client-only", "/api/client-only", "src/orders.ts", 16),
            DynamicClientFact(clientManifest, "src/orders.ts", 20, "HelperFunctionCall:http://private.example/api")
        ]);
        SqliteIndexWriter.Write(serverOneIndex, serverOneManifest, [
            RouteFact(serverOneManifest, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10),
            RouteFact(serverOneManifest, "POST", "/api/orders/archive", "/api/orders/archive", "Controllers/OrdersController.cs", 20),
            RouteFact(serverOneManifest, "GET", "/api/server-only", "/api/server-only", "Controllers/OrdersController.cs", 30),
            SqlTextFact(serverOneManifest, "Infrastructure/queries.sql", 3),
            QueryPatternFact(serverOneManifest, "Infrastructure/OrderRepository.cs", 14),
            ConfigFact(serverOneManifest, "appsettings.json", 4)
        ]);
        SqliteIndexWriter.Write(serverTwoIndex, serverTwoManifest, [
            RouteFact(serverTwoManifest, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10)
        ]);

        await CombinedIndexBuilder.CombineAsync(
            new CombineOptions(
                [clientIndex, serverOneIndex, serverTwoIndex],
                combinedPath,
                ["client", "server-one", "server-two"]));
        var endpointRowsBefore = await CountAsync(combinedPath, "endpoint_matches");

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        Assert.True(File.Exists(Path.Combine(outDir, "dependency-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "dependency-report.json")));
        Assert.Equal(endpointRowsBefore, await CountAsync(combinedPath, "endpoint_matches"));
        Assert.Contains(result.Report.CoverageWarnings, warning => warning.Contains("known gaps", StringComparison.Ordinal));
        Assert.Equal(2, result.Report.EndpointFindings.Count(finding => finding.Classification == CombinedEndpointClassifications.MatchedEndpoint && finding.NormalizedPathKey == "/api/orders/{}"));
        Assert.Contains(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.MethodMismatch);
        Assert.Contains(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.DynamicClientUrlNeedsReview);
        Assert.Contains(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.ClientCallNoServerEndpoint);
        Assert.Contains(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.ServerEndpointNoClientMatch);
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query" && surface.TableName == "orders" && surface.ColumnNames == "id;status");
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query" && surface.TextHash == "abc123" && surface.ColumnNames == "n/a");
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "package-config" && surface.ConfigKey == "ConnectionStrings:Default");

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.md"));
        Assert.Contains("TraceMap Dependency Report", markdown);
        Assert.Contains("static shape or hash evidence", markdown);
        Assert.DoesNotContain("select * from orders", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private.example", markdown, StringComparison.OrdinalIgnoreCase);

        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.json"));
        Assert.Contains("\"clientScanId\"", json);
        Assert.Contains("\"serverEndLine\"", json);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private.example", json, StringComparison.OrdinalIgnoreCase);
        var document = JsonSerializer.Deserialize<CombinedDependencyReportDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(document);
        Assert.Equal(result.Report.EndpointFindings.Count, document!.EndpointFindings.Count);

        var secondOutDir = Path.Combine(temp.Path, "report-second");
        await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, secondOutDir));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "dependency-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "dependency-report.json")));
    }

    [Fact]
    public async Task Report_includes_same_source_endpoint_matches_and_directory_row_caps()
    {
        using var temp = new TempDirectory();
        var bffIndex = Path.Combine(temp.Path, "bff.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "row-capped-report");
        var manifest = Manifest("bff", "tracemap-milestone15");
        var facts = new List<CodeFact>
        {
            HttpClientFact(manifest, "GET", "/api/self", "/api/self", "src/Self.cs", 8),
            RouteFact(manifest, "GET", "/api/self", "/api/self", "src/SelfController.cs", 10)
        };
        for (var index = 0; index < 205; index++)
        {
            facts.Add(PackageFact(manifest, index));
        }

        SqliteIndexWriter.Write(bffIndex, manifest, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([bffIndex], combinedPath, ["bff"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        Assert.Contains(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.MatchedEndpoint && finding.SameSource);
        Assert.Equal(205, result.Report.DependencySurfaces.Count(surface => surface.SurfaceKind == "package-config"));
        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.md"));
        Assert.Contains("Showing first 200 of", markdown);
    }

    [Fact]
    public async Task Report_matches_multi_method_any_and_optional_server_routes()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var clientManifest = Manifest("client", "tracemap-typescript/0.1.0");
        var serverManifest = Manifest("server", "tracemap-milestone15");

        SqliteIndexWriter.Write(clientIndex, clientManifest, [
            HttpClientFact(clientManifest, "GET", "/api/methods", "/api/methods", "src/client.ts", 5),
            HttpClientFact(clientManifest, "ANY", "/api/any", "/api/any", "src/client.ts", 10),
            HttpClientFact(clientManifest, "GET", "/api/items", "/api/items", "src/client.ts", 15)
        ]);
        SqliteIndexWriter.Write(serverIndex, serverManifest, [
            RouteFact(serverManifest, "GET,POST", "/api/methods", "/api/methods", "Controllers/ApiController.cs", 5),
            RouteFact(serverManifest, "POST", "/api/any", "/api/any", "Controllers/ApiController.cs", 10),
            RouteFact(serverManifest, "GET", "/api/items/{id?}", "/api/items/{?}", "Controllers/ApiController.cs", 15)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        Assert.Contains(result.Report.EndpointFindings, finding =>
            finding.Classification == CombinedEndpointClassifications.MatchedEndpoint
            && finding.NormalizedPathKey == "/api/methods"
            && finding.HttpMethod == "GET");
        Assert.Contains(result.Report.EndpointFindings, finding =>
            finding.Classification == CombinedEndpointClassifications.MatchedEndpoint
            && finding.NormalizedPathKey == "/api/any"
            && finding.HttpMethod == "ANY");
        Assert.Contains(result.Report.EndpointFindings, finding =>
            finding.Classification == CombinedEndpointClassifications.OptionalSegmentMatch
            && finding.NormalizedPathKey == "/api/items");
        Assert.DoesNotContain(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.MethodMismatch);
    }

    [Fact]
    public async Task Combine_infers_jvm_and_python_languages()
    {
        using var temp = new TempDirectory();
        var jvmIndex = Path.Combine(temp.Path, "jvm.sqlite");
        var pythonIndex = Path.Combine(temp.Path, "python.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(jvmIndex, Manifest("jvm", "tracemap-jvm-mvp"), []);
        SqliteIndexWriter.Write(pythonIndex, Manifest("python", "tracemap-python/0.1.0"), []);

        await CombinedIndexBuilder.CombineAsync(new CombineOptions([jvmIndex, pythonIndex], combinedPath, ["jvm", "python"]));

        var labels = await ReadLabelLanguagesAsync(combinedPath);
        Assert.Equal("jvm", labels["jvm"]);
        Assert.Equal("python", labels["python"]);
    }

    private static ScanManifest Manifest(
        string repo,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded",
        IReadOnlyList<string>? knownGaps = null)
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            null,
            "main",
            "abc123",
            scannerVersion,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            analysisLevel,
            buildStatus,
            [],
            [],
            [],
            knownGaps ?? [],
            ".",
            FactFactory.Hash(repo, 32),
            FactFactory.Hash("git-root", 32));
    }

    private static CodeFact HttpClientFact(ScanManifest manifest, string method, string template, string key, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
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

    private static CodeFact DynamicClientFact(ScanManifest manifest, string file, int line, string dynamicReason)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "dynamic",
            contractElement: "GET",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethod"] = "GET",
                ["methodName"] = "GET",
                ["urlKind"] = "dynamic",
                ["dynamicReason"] = dynamicReason
            });
    }

    private static CodeFact RouteFact(ScanManifest manifest, string method, string template, string key, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: $"{method} {template}",
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

    private static CodeFact SqlTextFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.SqlTextUsed,
            RuleIds.DatabaseSqlText,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "sql",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["textHash"] = "abc123",
                ["textLength"] = "21",
                ["sqlSourceKind"] = "sql-file"
            });
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "orders",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationName"] = "SELECT",
                ["tableName"] = "orders",
                ["columnNames"] = "id;status",
                ["sqlSourceKind"] = "literal-string",
                ["queryShapeHash"] = "shape123"
            });
    }

    private static CodeFact ConfigFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ConnectionStringDeclared,
            RuleIds.ConfigKey,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "ConnectionStrings:Default",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["keyPath"] = "ConnectionStrings:Default",
                ["valueHash"] = "hash-only"
            });
    }

    private static CodeFact PackageFact(ScanManifest manifest, int index)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.PackageReferenced,
            RuleIds.ProjectFile,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("src/App.csproj", index + 1, index + 1, null, "test", "test/1.0"),
            targetSymbol: $"Package.{index:000}",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["package"] = $"Package.{index:000}",
                ["version"] = "1.0.0"
            });
    }

    private static async Task<long> CountAsync(string sqlitePath, string table)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {table};";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<Dictionary<string, string>> ReadLabelLanguagesAsync(string sqlitePath)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select label, language from index_sources order by label;";
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }

        return result;
    }
}
