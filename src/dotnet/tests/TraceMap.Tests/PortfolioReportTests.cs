using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class PortfolioReportTests
{
    [Fact]
    public async Task Portfolio_direct_inputs_write_deterministic_markdown_json_and_group_shared_surfaces()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var outDir = Path.Combine(temp.Path, "portfolio");
        var client = Manifest("client", "tracemap-typescript/0.1.0", "git-client");
        var server = Manifest("server", ScannerVersions.TraceMap, "git-server");
        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{}", "src/orders.ts", 8),
            PackageFact(client, "Telemetry.Core", "nuget", "Client.csproj", "PackageReference")
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{}", "Controllers/OrdersController.cs", 12),
            PackageFact(server, "Telemetry.Core", "nuget", "Api.csproj", "PackageReference"),
            CallEdgeFact(server)
        ]);

        var exitCode = await TraceMapCommand.RunAsync([
            "portfolio",
            "--index", clientIndex,
            "--label", "web",
            "--index", serverIndex,
            "--label", "api",
            "--out", outDir
        ], new StringWriter(), new StringWriter());

        Assert.Equal(0, exitCode);
        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.json"));
        Assert.Contains("TraceMap Portfolio Report", markdown);
        Assert.Contains("MatchedEndpoint", markdown);
        Assert.Contains("package-config", markdown);
        Assert.Contains("Shared Portfolio Surfaces", markdown);
        Assert.Contains("\"reportType\": \"multi-index-portfolio-report\"", json);
        Assert.Contains("\"endpointFindingCount\": 1", json);
        Assert.Contains("\"sharedSurfaceCount\": 1", json);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);

        var secondOut = Path.Combine(temp.Path, "portfolio-second");
        Assert.Equal(0, await TraceMapCommand.RunAsync([
            "portfolio",
            "--index", clientIndex,
            "--label", "web",
            "--index", serverIndex,
            "--label", "api",
            "--out", secondOut
        ], new StringWriter(), new StringWriter()));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOut, "portfolio-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOut, "portfolio-report.json")));
    }

    [Fact]
    public async Task Portfolio_manifest_resolves_relative_paths_and_keeps_unsafe_values_out()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "api.sqlite");
        var manifestPath = Path.Combine(temp.Path, "portfolio.json");
        var outDir = Path.Combine(temp.Path, "out");
        var scan = Manifest("api", ScannerVersions.TraceMap, "git-api", analysisLevel: "Level1SemanticAnalysisReduced", knownGaps: ["SemanticLoadFailed: sample"]);
        SqliteIndexWriter.Write(indexPath, scan, [
            ConfigFact(scan, "appsettings.json", "ConnectionStrings:Default", "Server=db;Password=secret;")
        ]);
        await File.WriteAllTextAsync(manifestPath, """
            {
              "version": "1.0",
              "portfolioId": "demo|[bad](link)",
              "snapshotId": "snap`one",
              "inputs": [
                {
                  "label": "api|bad",
                  "indexPath": "api.sqlite",
                  "expectedCommitSha": "different",
                  "group": "backend|group",
                  "roleTags": ["server", "[api](bad)"]
                }
              ]
            }
            """);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions([], outDir, ManifestPath: manifestPath));

        Assert.Equal("ReducedCoverage", result.Report.Summary.ReportCoverage);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ExpectedCommitMismatch");
        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.json"));
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=secret", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[bad](link)", markdown, StringComparison.Ordinal);
        Assert.Contains("api bad", markdown);
    }

    [Fact]
    public async Task Portfolio_expands_combined_input_sources()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "portfolio");
        SqliteIndexWriter.Write(clientIndex, Manifest("client", "tracemap-typescript/0.1.0", "git-client"), []);
        SqliteIndexWriter.Write(serverIndex, Manifest("server", ScannerVersions.TraceMap, "git-server"), []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combined, ["web", "api"]));

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions([
            new PortfolioInputSpec("stack", combined)
        ], outDir));

        Assert.Single(result.Report.Inputs);
        Assert.Equal(2, result.Report.Sources.Count);
        Assert.Contains(result.Report.Sources, source => source.ContainerLabel == "stack" && source.Label == "web");
        Assert.Contains(result.Report.Sources, source => source.ContainerLabel == "stack" && source.Label == "api");
    }

    [Fact]
    public async Task Portfolio_source_selector_filters_duplicate_inner_source_labels_by_container()
    {
        using var temp = new TempDirectory();
        var firstIndex = Path.Combine(temp.Path, "first-api.sqlite");
        var secondIndex = Path.Combine(temp.Path, "second-api.sqlite");
        var firstCombined = Path.Combine(temp.Path, "first-combined.sqlite");
        var secondCombined = Path.Combine(temp.Path, "second-combined.sqlite");
        var outDir = Path.Combine(temp.Path, "portfolio");
        var first = Manifest("first", ScannerVersions.TraceMap, "git-first");
        var second = Manifest("second", ScannerVersions.TraceMap, "git-second");
        SqliteIndexWriter.Write(firstIndex, first, [
            PackageFact(first, "StackA.Only", "nuget", "First.csproj", "PackageReference")
        ]);
        SqliteIndexWriter.Write(secondIndex, second, [
            PackageFact(second, "StackB.Only", "nuget", "Second.csproj", "PackageReference")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([firstIndex], firstCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([secondIndex], secondCombined, ["api"]));

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions([
            new PortfolioInputSpec("stack-a", firstCombined),
            new PortfolioInputSpec("stack-b", secondCombined)
        ], outDir, Source: "stack-a"));

        var source = Assert.Single(result.Report.Sources);
        Assert.Equal("stack-a", source.ContainerLabel);
        Assert.Equal("api", source.Label);
        var surface = Assert.Single(result.Report.DependencySurfaces.Rows);
        Assert.Equal(source.SourceId, surface.SourceId);
        Assert.Equal("StackA.Only", surface.DisplayName);
        Assert.DoesNotContain(result.Report.DependencySurfaces.Rows, row => row.DisplayName == "StackB.Only");
    }

    [Fact]
    public async Task Portfolio_source_cap_emits_truncation_gap()
    {
        using var temp = new TempDirectory();
        var firstIndex = Path.Combine(temp.Path, "first.sqlite");
        var secondIndex = Path.Combine(temp.Path, "second.sqlite");
        var outDir = Path.Combine(temp.Path, "portfolio");
        SqliteIndexWriter.Write(firstIndex, Manifest("first", ScannerVersions.TraceMap, "git-first"), []);
        SqliteIndexWriter.Write(secondIndex, Manifest("second", ScannerVersions.TraceMap, "git-second"), []);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions([
            new PortfolioInputSpec("first", firstIndex),
            new PortfolioInputSpec("second", secondIndex)
        ], outDir, MaxSources: 1));

        Assert.True(result.Report.Summary.Truncated);
        Assert.Equal(PortfolioReportStatuses.Truncated, result.Report.SourceCoverage.Status);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Section == "sourceCoverage");
    }

    [Fact]
    public async Task Portfolio_requested_deferred_context_gaps_are_in_top_level_rollup()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "api.sqlite");
        var outDir = Path.Combine(temp.Path, "portfolio");
        SqliteIndexWriter.Write(indexPath, Manifest("api", ScannerVersions.TraceMap, "git-api"), []);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions([
            new PortfolioInputSpec("api", indexPath)
        ], outDir, IncludeImpact: true, IncludePaths: true, IncludeReverse: true));

        Assert.Equal(PortfolioReportStatuses.Deferred, result.Report.PathContext.Status);
        Assert.Equal(PortfolioReportStatuses.Deferred, result.Report.ReverseContext.Status);
        Assert.Equal(PortfolioReportStatuses.Deferred, result.Report.PortfolioImpact.Status);
        Assert.Contains(result.Report.Gaps, gap => gap.Section == "pathContext" && gap.GapKind == "Deferred");
        Assert.Contains(result.Report.Gaps, gap => gap.Section == "reverseContext" && gap.GapKind == "Deferred");
        Assert.Contains(result.Report.Gaps, gap => gap.Section == "portfolioImpact" && gap.GapKind == "Deferred");
        Assert.Equal(result.Report.Gaps.Count, result.Report.Summary.GapCount);
        Assert.Contains(result.Report.Summary.CoverageWarnings, warning => warning.Contains("Path traversal is deferred", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Portfolio_requested_deferred_context_section_gaps_obey_top_level_gap_cap()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "api.sqlite");
        var outDir = Path.Combine(temp.Path, "portfolio");
        SqliteIndexWriter.Write(indexPath, Manifest("api", ScannerVersions.TraceMap, "git-api"), []);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions([
            new PortfolioInputSpec("api", indexPath)
        ], outDir, IncludeImpact: true, IncludePaths: true, IncludeReverse: true, MaxGaps: 1));

        var topLevelGapIds = result.Report.Gaps.Select(gap => gap.GapId).ToHashSet(StringComparer.Ordinal);
        var sectionGaps = result.Report.PathContext.Gaps
            .Concat(result.Report.ReverseContext.Gaps)
            .Concat(result.Report.PortfolioImpact.Gaps)
            .Concat(result.Report.ReleaseReviewContext.Gaps)
            .ToArray();

        Assert.All(sectionGaps, gap => Assert.Contains(gap.GapId, topLevelGapIds));
        Assert.Equal(result.Report.Gaps.Count, result.Report.Summary.GapCount);
    }

    [Fact]
    public async Task Portfolio_before_after_manifests_compare_duplicate_inner_source_labels_by_container()
    {
        using var temp = new TempDirectory();
        var beforeFirstIndex = Path.Combine(temp.Path, "before-first.sqlite");
        var beforeSecondIndex = Path.Combine(temp.Path, "before-second.sqlite");
        var afterFirstIndex = Path.Combine(temp.Path, "after-first.sqlite");
        var afterSecondIndex = Path.Combine(temp.Path, "after-second.sqlite");
        var beforeFirstCombined = Path.Combine(temp.Path, "before-first-combined.sqlite");
        var beforeSecondCombined = Path.Combine(temp.Path, "before-second-combined.sqlite");
        var afterFirstCombined = Path.Combine(temp.Path, "after-first-combined.sqlite");
        var afterSecondCombined = Path.Combine(temp.Path, "after-second-combined.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var outDir = Path.Combine(temp.Path, "comparison");
        SqliteIndexWriter.Write(beforeFirstIndex, Manifest("first-before", ScannerVersions.TraceMap, "git-first-before"), []);
        SqliteIndexWriter.Write(beforeSecondIndex, Manifest("second", ScannerVersions.TraceMap, "git-second"), []);
        SqliteIndexWriter.Write(afterFirstIndex, Manifest("first-after", ScannerVersions.TraceMap, "git-first-after"), []);
        SqliteIndexWriter.Write(afterSecondIndex, Manifest("second", ScannerVersions.TraceMap, "git-second"), []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeFirstIndex], beforeFirstCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeSecondIndex], beforeSecondCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterFirstIndex], afterFirstCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterSecondIndex], afterSecondCombined, ["api"]));
        await WriteManifestAsync(beforeManifest, [
            ("stack-a", beforeFirstCombined),
            ("stack-b", beforeSecondCombined)
        ]);
        await WriteManifestAsync(afterManifest, [
            ("stack-a", afterFirstCombined),
            ("stack-b", afterSecondCombined)
        ]);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions(
            [],
            outDir,
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest));

        var row = Assert.Single(result.Report.PortfolioDiff.Rows);
        Assert.Equal("ChangedSourceEvidence", row.ChangeKind);
        Assert.Equal("stack-a/api", row.SourceLabel);
    }

    [Fact]
    public async Task Portfolio_before_after_manifests_emit_source_diff_rows()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var outDir = Path.Combine(temp.Path, "comparison");
        SqliteIndexWriter.Write(beforeIndex, Manifest("api", ScannerVersions.TraceMap, "git-api-before"), []);
        SqliteIndexWriter.Write(afterIndex, Manifest("api", ScannerVersions.TraceMap, "git-api-after"), []);
        await File.WriteAllTextAsync(beforeManifest, $$"""
            {
              "version": "1.0",
              "portfolioId": "demo",
              "snapshotId": "before",
              "inputs": [
                { "label": "api", "indexPath": "{{Path.GetFileName(beforeIndex)}}" }
              ]
            }
            """);
        await File.WriteAllTextAsync(afterManifest, $$"""
            {
              "version": "1.0",
              "portfolioId": "demo",
              "snapshotId": "after",
              "inputs": [
                { "label": "api", "indexPath": "{{Path.GetFileName(afterIndex)}}" }
              ]
            }
            """);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions(
            [],
            outDir,
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest));

        Assert.Equal("PortfolioComparisonV1", result.Report.Mode);
        Assert.Contains(result.Report.PortfolioDiff.Rows, row => row.ChangeKind == "ChangedSourceEvidence" && row.SourceLabel == "api");
        Assert.Equal(PortfolioReportStatuses.NotRequested, result.Report.PathContext.Status);
        Assert.Equal(PortfolioReportStatuses.NotRequested, result.Report.ReverseContext.Status);
    }

    [Fact]
    public async Task Portfolio_comparison_requested_deferred_context_gaps_are_in_top_level_rollup()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var outDir = Path.Combine(temp.Path, "comparison");
        SqliteIndexWriter.Write(beforeIndex, Manifest("api", ScannerVersions.TraceMap, "git-api-before"), []);
        SqliteIndexWriter.Write(afterIndex, Manifest("api", ScannerVersions.TraceMap, "git-api-after"), []);
        await WriteManifestAsync(beforeManifest, [("api", beforeIndex)]);
        await WriteManifestAsync(afterManifest, [("api", afterIndex)]);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions(
            [],
            outDir,
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest,
            IncludeImpact: true,
            IncludePaths: true,
            IncludeReverse: true));

        Assert.Equal(PortfolioReportStatuses.Deferred, result.Report.PathContext.Status);
        Assert.Equal(PortfolioReportStatuses.Deferred, result.Report.ReverseContext.Status);
        Assert.Equal(PortfolioReportStatuses.Deferred, result.Report.PortfolioImpact.Status);
        Assert.Contains(result.Report.Gaps, gap => gap.Section == "pathContext" && gap.GapKind == "Deferred");
        Assert.Contains(result.Report.Gaps, gap => gap.Section == "reverseContext" && gap.GapKind == "Deferred");
        Assert.Contains(result.Report.Gaps, gap => gap.Section == "portfolioImpact" && gap.GapKind == "Deferred");
        Assert.Equal(result.Report.Gaps.Count, result.Report.Summary.GapCount);
    }

    [Fact]
    public async Task Portfolio_comparison_section_gaps_obey_top_level_gap_cap()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var outDir = Path.Combine(temp.Path, "comparison");
        SqliteIndexWriter.Write(beforeIndex, Manifest("api", ScannerVersions.TraceMap, "git-api-before"), []);
        SqliteIndexWriter.Write(afterIndex, Manifest("api", ScannerVersions.TraceMap, "git-api-after"), []);
        await WriteManifestAsync(beforeManifest, [("api", beforeIndex)]);
        await WriteManifestAsync(afterManifest, [("api", afterIndex)]);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions(
            [],
            outDir,
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest,
            IncludeImpact: true,
            IncludePaths: true,
            IncludeReverse: true,
            MaxGaps: 1));

        var topLevelGapIds = result.Report.Gaps.Select(gap => gap.GapId).ToHashSet(StringComparer.Ordinal);
        var sectionGaps = result.Report.EndpointAlignment.Gaps
            .Concat(result.Report.PathContext.Gaps)
            .Concat(result.Report.ReverseContext.Gaps)
            .Concat(result.Report.PortfolioImpact.Gaps)
            .Concat(result.Report.ReleaseReviewContext.Gaps)
            .ToArray();

        Assert.All(sectionGaps, gap => Assert.Contains(gap.GapId, topLevelGapIds));
        Assert.Equal(result.Report.Gaps.Count, result.Report.Summary.GapCount);
    }

    [Fact]
    public async Task Portfolio_rejects_extensionless_existing_file()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "api.sqlite");
        var outputPath = Path.Combine(temp.Path, "existing");
        SqliteIndexWriter.Write(indexPath, Manifest("api", ScannerVersions.TraceMap, "git-api"), []);
        await File.WriteAllTextAsync(outputPath, "not a directory");

        var error = await Assert.ThrowsAsync<IOException>(() => PortfolioReporter.WriteAsync(new PortfolioReportOptions([
            new PortfolioInputSpec("api", indexPath)
        ], outputPath)));

        Assert.Contains("extensionless output path", error.Message);
    }

    private static ScanManifest Manifest(
        string repo,
        string scannerVersion,
        string gitRootHash,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded",
        IReadOnlyList<string>? knownGaps = null)
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            null,
            "main",
            $"{repo}-commit",
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
            FactFactory.Hash(gitRootHash, 32));
    }

    private static CodeFact HttpClientFact(ScanManifest manifest, string method, string pathKey, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: $"{method} {pathKey}",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethod"] = method,
                ["methodName"] = method,
                ["normalizedPathKey"] = pathKey,
                ["normalizedPathTemplate"] = pathKey,
                ["urlKind"] = "template"
            });
    }

    private static CodeFact RouteFact(ScanManifest manifest, string method, string pathKey, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: $"{method} {pathKey}",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethods"] = method,
                ["methodName"] = method,
                ["normalizedPathKey"] = pathKey,
                ["normalizedPathTemplate"] = pathKey,
                ["routeTemplates"] = pathKey
            });
    }

    private static CodeFact PackageFact(ScanManifest manifest, string name, string ecosystem, string file, string group)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.PackageReferenced,
            RuleIds.ProjectFile,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, 5, 5, null, "test", "test/1.0"),
            targetSymbol: name,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["dependencyGroup"] = group,
                ["dependencyScope"] = "runtime",
                ["ecosystem"] = ecosystem,
                ["manifestKind"] = file.EndsWith(".json", StringComparison.Ordinal) ? "package.json" : "csproj",
                ["packageName"] = name,
                ["surfaceKind"] = "package-config",
                ["version"] = "1.0.0"
            });
    }

    private static CodeFact ConfigFact(ScanManifest manifest, string file, string key, string value)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ConnectionStringDeclared,
            RuleIds.ConfigKey,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, 3, 3, null, "test", "test/1.0"),
            targetSymbol: value,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["keyPath"] = key,
                ["valueHash"] = FactFactory.Hash(value, 32)
            });
    }

    private static CodeFact CallEdgeFact(ScanManifest manifest)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.CallEdge,
            RuleIds.CSharpSemanticMethodInvocation,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan("Services/OrderService.cs", 20, 20, null, "test", "test/1.0"),
            sourceSymbol: "Api.Controllers.Orders.Get()",
            targetSymbol: "Api.Services.OrderService.Load()",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["callKind"] = "Invocation"
            });
    }

    private static Task WriteManifestAsync(string path, IReadOnlyList<(string Label, string IndexPath)> inputs)
    {
        var json = JsonSerializer.Serialize(
            new
            {
                version = "1.0",
                portfolioId = "demo",
                snapshotId = Path.GetFileNameWithoutExtension(path),
                inputs = inputs.Select(input => new { label = input.Label, indexPath = Path.GetFileName(input.IndexPath) }).ToArray()
            },
            new JsonSerializerOptions { WriteIndented = true });
        return File.WriteAllTextAsync(path, json);
    }
}
