using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CombinedChangeImpactTests
{
    [Fact]
    public async Task Impact_writes_markdown_json_and_reports_endpoint_surface_without_raw_values()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var outDir = Path.Combine(temp.Path, "impact");
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

        var result = await CombinedChangeImpactReporter.WriteAsync(new CombinedChangeImpactOptions(beforeCombined, afterCombined, outDir, Format: "json"));

        Assert.True(File.Exists(Path.Combine(outDir, "impact-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "impact-report.json")));
        Assert.True(result.HasImpactItems);
        Assert.Contains(result.Report.ImpactItems, item => item.EvidenceKind == "endpoint");
        Assert.Contains(result.Report.ImpactItems, item => item.EvidenceKind == "surface" && item.Classification == CombinedImpactClassifications.ProbableStaticImpact);
        Assert.All(result.Report.ImpactItems, item => Assert.NotEqual(CombinedImpactClassifications.StaticImpactEvidence, item.Classification));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "impact-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "impact-report.json"));
        Assert.Contains("TraceMap Change Impact Report", markdown);
        Assert.Contains("Path context: `not requested`", markdown);
        Assert.Contains("\"reportType\": \"combined-change-impact\"", json);
        Assert.DoesNotContain("https://example.invalid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select * from orders", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);

        var document = JsonSerializer.Deserialize<CombinedChangeImpactReport>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(document);
        Assert.Equal("combined-change-impact", document!.ReportType);

        var secondOutDir = Path.Combine(temp.Path, "impact-second");
        await CombinedChangeImpactReporter.WriteAsync(new CombinedChangeImpactOptions(beforeCombined, afterCombined, secondOutDir, Format: "json"));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "impact-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "impact-report.json")));
    }

    [Fact]
    public async Task Impact_identical_inputs_emit_no_impact_evidence()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        await WriteSimpleCombinedAsync(temp, beforeCombined, "before");
        await WriteSimpleCombinedAsync(temp, afterCombined, "after");

        var result = await CombinedChangeImpactReporter.WriteAsync(new CombinedChangeImpactOptions(beforeCombined, afterCombined, Path.Combine(temp.Path, "impact")));

        Assert.Empty(result.Report.ImpactItems);
        Assert.False(result.HasImpactItems);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == CombinedImpactClassifications.NoImpactEvidence);
    }

    [Fact]
    public async Task Impact_coverage_scope_maps_to_source_diff_and_filters_coverage_items()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var fullManifest = Manifest("api", "tracemap-milestone15");
        var reducedManifest = Manifest("api", "tracemap-milestone15", analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial");

        await WriteSingleCombinedAsync(temp, beforeCombined, "before-full", fullManifest, []);
        await WriteSingleCombinedAsync(temp, afterCombined, "after-reduced", reducedManifest, []);

        var result = await CombinedChangeImpactReporter.WriteAsync(new CombinedChangeImpactOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "impact"),
            Scope: "coverage"));

        Assert.Equal(["coverage"], result.Report.Query.Scopes);
        Assert.Equal(["sources"], result.Report.Query.DelegatedDiffScopes);
        Assert.NotEmpty(result.Report.ImpactItems);
        Assert.All(result.Report.ImpactItems, item => Assert.Equal("coverage", item.EvidenceKind));
    }

    [Fact]
    public async Task Impact_rejects_paths_scope_without_include_paths_and_exit_code_is_opt_in()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Manifest("api", "tracemap-milestone15");
        var afterManifest = Manifest("api", "tracemap-milestone15");
        await WriteSingleCombinedAsync(temp, beforeCombined, "before", beforeManifest, []);
        await WriteSingleCombinedAsync(temp, afterCombined, "after", afterManifest, [
            RouteFact(afterManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "impact",
            "--before", beforeCombined,
            "--after", afterCombined,
            "--out", Path.Combine(temp.Path, "paths"),
            "--scope", "paths"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("requires --include-paths", error.ToString());

        error.GetStringBuilder().Clear();
        output.GetStringBuilder().Clear();
        exitCode = await TraceMapCommand.RunAsync([
            "impact",
            "--before", beforeCombined,
            "--after", afterCombined,
            "--out", Path.Combine(temp.Path, "impact-no-exit")
        ], output, error);

        Assert.Equal(0, exitCode);

        error.GetStringBuilder().Clear();
        output.GetStringBuilder().Clear();
        exitCode = await TraceMapCommand.RunAsync([
            "impact",
            "--before", beforeCombined,
            "--after", afterCombined,
            "--out", Path.Combine(temp.Path, "impact-exit"),
            "--exit-code"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("TraceMap impact completed:", output.ToString());
    }

    [Fact]
    public async Task Impact_downgrades_reduced_coverage_added_removed_to_needs_review()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterReducedCombined = Path.Combine(temp.Path, "after-reduced.sqlite");
        var beforeReducedCombined = Path.Combine(temp.Path, "before-reduced.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var fullManifest = Manifest("api", "tracemap-milestone15");
        var reducedManifest = Manifest("api", "tracemap-milestone15", analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial");

        await WriteSingleCombinedAsync(temp, beforeCombined, "before-full", fullManifest, [
            RouteFact(fullManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);
        await WriteSingleCombinedAsync(temp, afterReducedCombined, "after-reduced", reducedManifest, []);
        await WriteSingleCombinedAsync(temp, beforeReducedCombined, "before-reduced", reducedManifest, []);
        await WriteSingleCombinedAsync(temp, afterCombined, "after-full", fullManifest, [
            RouteFact(fullManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);

        var removed = await CombinedChangeImpactReporter.WriteAsync(new CombinedChangeImpactOptions(
            beforeCombined,
            afterReducedCombined,
            Path.Combine(temp.Path, "removed"),
            Scope: "endpoints"));
        var added = await CombinedChangeImpactReporter.WriteAsync(new CombinedChangeImpactOptions(
            beforeReducedCombined,
            afterCombined,
            Path.Combine(temp.Path, "added"),
            Scope: "endpoints"));

        Assert.Contains(removed.Report.ImpactItems, item => item.EvidenceKind == "endpoint" && item.Classification == CombinedImpactClassifications.NeedsReviewImpact);
        Assert.DoesNotContain(removed.Report.ImpactItems, item => item.EvidenceKind == "endpoint" && item.Classification == CombinedImpactClassifications.ProbableStaticImpact);
        Assert.Equal("ReducedCoverage", removed.Report.ReportCoverage);
        Assert.Contains(added.Report.ImpactItems, item => item.EvidenceKind == "endpoint" && item.Classification == CombinedImpactClassifications.NeedsReviewImpact);
        Assert.DoesNotContain(added.Report.ImpactItems, item => item.EvidenceKind == "endpoint" && item.Classification == CombinedImpactClassifications.ProbableStaticImpact);
        Assert.Equal("ReducedCoverage", added.Report.ReportCoverage);
    }

    [Fact]
    public async Task Impact_item_cap_emits_truncation_gap()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", "tracemap-milestone15");
        await WriteSingleCombinedAsync(temp, beforeCombined, "before", manifest, []);
        await WriteSingleCombinedAsync(temp, afterCombined, "after", manifest, [
            RouteFact(manifest, "GET", "/api/orders/1", "/api/orders/1", "Controllers/OrdersController.cs", 10),
            RouteFact(manifest, "GET", "/api/orders/2", "/api/orders/2", "Controllers/OrdersController.cs", 11),
            RouteFact(manifest, "GET", "/api/orders/3", "/api/orders/3", "Controllers/OrdersController.cs", 12)
        ]);

        var result = await CombinedChangeImpactReporter.WriteAsync(new CombinedChangeImpactOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "impact"),
            Scope: "endpoints",
            MaxImpactItems: 1));

        Assert.Single(result.Report.ImpactItems);
        Assert.True(result.Report.Summary.Truncated);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == CombinedImpactClassifications.TruncatedByLimit);
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

    private static async Task WriteSingleCombinedAsync(TempDirectory temp, string combinedPath, string prefix, ScanManifest manifest, IReadOnlyList<CodeFact> facts)
    {
        var indexPath = Path.Combine(temp.Path, $"{prefix}-index.sqlite");
        SqliteIndexWriter.Write(indexPath, manifest, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([indexPath], combinedPath, ["api"]));
    }

    private static ScanManifest Manifest(
        string repo,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded",
        string? gitRootHash = null)
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            "https://example.invalid/repo.git",
            "main",
            "abc1234567890",
            scannerVersion,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            analysisLevel,
            buildStatus,
            [],
            [],
            [],
            [],
            ".",
            FactFactory.Hash(repo, 32),
            gitRootHash ?? FactFactory.Hash($"{repo}-git-root", 32));
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
