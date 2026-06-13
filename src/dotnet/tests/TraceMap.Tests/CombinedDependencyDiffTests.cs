using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CombinedDependencyDiffTests
{
    [Fact]
    public async Task Diff_writes_markdown_json_and_reports_added_endpoint_surface_without_raw_values()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var outDir = Path.Combine(temp.Path, "diff");
        var beforeClient = Path.Combine(temp.Path, "before-client.sqlite");
        var beforeServer = Path.Combine(temp.Path, "before-server.sqlite");
        var afterClient = Path.Combine(temp.Path, "after-client.sqlite");
        var afterServer = Path.Combine(temp.Path, "after-server.sqlite");
        var clientManifest = Manifest("client", "tracemap-typescript/0.1.0");
        var serverManifest = Manifest("server", "tracemap-milestone15");

        SqliteIndexWriter.Write(beforeClient, clientManifest, [
            HttpClientFact(clientManifest, "GET", "/api/orders", "/api/orders", "src/orders.ts", 10)
        ]);
        SqliteIndexWriter.Write(beforeServer, serverManifest, [
            RouteFact(serverManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);
        SqliteIndexWriter.Write(afterClient, clientManifest, [
            HttpClientFact(clientManifest, "GET", "/api/orders", "/api/orders", "src/orders.ts", 10)
        ]);
        SqliteIndexWriter.Write(afterServer, serverManifest, [
            RouteFact(serverManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10),
            RouteFact(serverManifest, "POST", "/api/orders/archive", "/api/orders/archive", "Controllers/OrdersController.cs", 20),
            QueryPatternFact(serverManifest, "Infrastructure/OrderRepository.cs", 30)
        ]);

        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeClient, beforeServer], beforeCombined, ["client", "server"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterClient, afterServer], afterCombined, ["client", "server"]));

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(beforeCombined, afterCombined, outDir, Format: "json"));

        Assert.True(File.Exists(Path.Combine(outDir, "diff-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "diff-report.json")));
        Assert.Contains(result.Report.EndpointDiffs, row => row.Classification == CombinedDependencyDiffClassifications.Added);
        Assert.Contains(result.Report.SurfaceDiffs, row => row.Classification == CombinedDependencyDiffClassifications.Added && row.After?.EvidenceKind == "surface");

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "diff-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "diff-report.json"));
        Assert.Contains("TraceMap Dependency Diff Report", markdown);
        Assert.Contains("Path comparison: `not requested`", markdown);
        Assert.Contains("\"reportType\": \"combined-dependency-diff\"", json);
        Assert.Contains("\"edgeDiffs\": []", json);
        Assert.DoesNotContain("select * from orders", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);

        var document = JsonSerializer.Deserialize<CombinedDependencyDiffReport>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(document);
        Assert.Equal("combined-dependency-diff", document!.ReportType);

        var secondOutDir = Path.Combine(temp.Path, "diff-second");
        await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(beforeCombined, afterCombined, secondOutDir, Format: "json"));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "diff-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "diff-report.json")));
    }

    [Fact]
    public async Task Diff_rejects_paths_scope_without_include_paths_and_bad_endpoint_selector()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        await WriteSimpleCombinedAsync(temp, beforeCombined, "before");
        await WriteSimpleCombinedAsync(temp, afterCombined, "after");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "diff",
            "--before", beforeCombined,
            "--after", afterCombined,
            "--out", Path.Combine(temp.Path, "diff"),
            "--scope", "paths"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("requires --include-paths", error.ToString());

        error.GetStringBuilder().Clear();
        exitCode = await TraceMapCommand.RunAsync([
            "diff",
            "--before", beforeCombined,
            "--after", afterCombined,
            "--out", Path.Combine(temp.Path, "diff2"),
            "--endpoint", "/api/orders"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("--endpoint must be", error.ToString());
    }

    [Fact]
    public async Task Diff_exit_code_is_opt_in_and_identity_mismatch_requires_flag()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var beforeIndex = Path.Combine(temp.Path, "before-api.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after-api.sqlite");
        var beforeManifest = Manifest("api", "tracemap-milestone15");
        var afterManifest = Manifest("different-api", "tracemap-milestone15");
        SqliteIndexWriter.Write(beforeIndex, beforeManifest, [RouteFact(beforeManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)]);
        SqliteIndexWriter.Write(afterIndex, afterManifest, [RouteFact(afterManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeIndex], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterIndex], afterCombined, ["api"]));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "diff",
            "--before", beforeCombined,
            "--after", afterCombined,
            "--out", Path.Combine(temp.Path, "identity")
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("identity differs", error.ToString());

        error.GetStringBuilder().Clear();
        output.GetStringBuilder().Clear();
        exitCode = await TraceMapCommand.RunAsync([
            "diff",
            "--before", beforeCombined,
            "--after", afterCombined,
            "--out", Path.Combine(temp.Path, "identity-allowed"),
            "--allow-identity-mismatch",
            "--exit-code"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("TraceMap diff completed:", output.ToString());
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "identity-allowed", "diff-report.json"));
        Assert.Contains("NeedsReviewDiff", json);
    }

    [Fact]
    public async Task Diff_identical_inputs_emit_no_diff_evidence()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        await WriteSimpleCombinedAsync(temp, beforeCombined, "before");
        await WriteSimpleCombinedAsync(temp, afterCombined, "after");

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(beforeCombined, afterCombined, Path.Combine(temp.Path, "diff")));

        Assert.Empty(result.Report.EndpointDiffs);
        Assert.Empty(result.Report.SurfaceDiffs);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == CombinedDependencyDiffClassifications.NoDiffEvidence);
    }

    private static async Task WriteSimpleCombinedAsync(TempDirectory temp, string combinedPath, string prefix)
    {
        var clientIndex = Path.Combine(temp.Path, $"{prefix}-client.sqlite");
        var serverIndex = Path.Combine(temp.Path, $"{prefix}-server.sqlite");
        var clientManifest = Manifest("client", "tracemap-typescript/0.1.0");
        var serverManifest = Manifest("server", "tracemap-milestone15");
        SqliteIndexWriter.Write(clientIndex, clientManifest, [HttpClientFact(clientManifest, "GET", "/api/orders", "/api/orders", "src/orders.ts", 10)]);
        SqliteIndexWriter.Write(serverIndex, serverManifest, [RouteFact(serverManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));
    }

    private static ScanManifest Manifest(string repo, string scannerVersion)
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            "https://example.invalid/repo.git",
            "main",
            "abc1234567890",
            scannerVersion,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            "Level1SemanticAnalysis",
            "Succeeded",
            [],
            [],
            [],
            [],
            ".",
            FactFactory.Hash(repo, 32),
            FactFactory.Hash($"{repo}-git-root", 32));
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
                ["queryShapeHash"] = "shape123",
                ["rawSql"] = "select * from orders"
            });
    }
}
