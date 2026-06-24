using System.Reflection;
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
    public async Task Portfolio_projects_legacy_data_model_surface_metadata_without_raw_descriptor_values()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "legacy.sqlite");
        var outDir = Path.Combine(temp.Path, "portfolio");
        var scan = Manifest("legacy", ScannerVersions.TraceMap, "git-legacy");
        SqliteIndexWriter.Write(indexPath, scan, [
            LegacyDataModelFact(
                scan,
                "ldm:nhibernate-private-entity",
                "reduced",
                "formula-redacted;filter-redacted;query-redacted;PrivateCustomer;TenantSecrets;syntheticsecret;select CardNumber from Vault.Customers")
        ]);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions([
            new PortfolioInputSpec("legacy", indexPath)
        ], outDir));

        var surface = Assert.Single(result.Report.DependencySurfaces.Rows, row => row.SurfaceKind == "legacy-data");
        Assert.True(HasMetadata(surface, "surfaceSubtype", "data-model"));
        Assert.True(HasMetadata(surface, "legacyDataMetadataFormat", "nhibernate-hbm"));
        Assert.True(HasMetadata(surface, "legacyDataDescriptorRole", "conceptual"));
        Assert.True(HasMetadata(surface, "legacyDataCoverageLabel", "reduced"));
        Assert.Contains(surface.Metadata, pair =>
            pair.Key == "legacyDataLimitations"
            && pair.Value.Contains("formula-redacted", StringComparison.Ordinal)
            && pair.Value.Contains("filter-redacted", StringComparison.Ordinal)
            && pair.Value.Contains("query-redacted", StringComparison.Ordinal)
            && pair.Value.Contains("limitation-hash:", StringComparison.Ordinal));
        Assert.DoesNotContain(surface.Metadata, pair => pair.Key == "legacyDataStableModelKey");
        Assert.Contains(surface.Metadata, pair => pair.Key == "legacyDataStableModelKeyHash" && !string.IsNullOrWhiteSpace(pair.Value));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.json"));
        Assert.Contains("limitation-hash:", json, StringComparison.Ordinal);
        foreach (var output in new[] { markdown, json })
        {
            Assert.Contains("surfaceSubtype", output, StringComparison.Ordinal);
            Assert.Contains("data-model", output, StringComparison.Ordinal);
            Assert.DoesNotContain("select CardNumber", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Vault.Customers", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tenant_id = 42", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private.example", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Server=prod-db", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("synthetic-secret", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("git@github.com:private", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:\\private", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TenantSecrets", output, StringComparison.Ordinal);
            Assert.DoesNotContain("PrivateCustomer", output, StringComparison.Ordinal);
            Assert.DoesNotContain("syntheticsecret", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ldm:nhibernate-private-entity", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Portfolio_shared_legacy_data_surfaces_use_hash_identity()
    {
        using var temp = new TempDirectory();
        var firstIndex = Path.Combine(temp.Path, "first.sqlite");
        var secondIndex = Path.Combine(temp.Path, "second.sqlite");
        var outDir = Path.Combine(temp.Path, "portfolio");
        var first = Manifest("legacy-one", ScannerVersions.TraceMap, "git-legacy-one");
        var second = Manifest("legacy-two", ScannerVersions.TraceMap, "git-legacy-two");
        SqliteIndexWriter.Write(firstIndex, first, [
            LegacyDataModelFact(first, "ldm:shared-portfolio-model", "full", "formula-redacted")
        ]);
        SqliteIndexWriter.Write(secondIndex, second, [
            LegacyDataModelFact(second, "ldm:shared-portfolio-model", "full", "formula-redacted")
        ]);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions([
            new PortfolioInputSpec("legacy-one", firstIndex),
            new PortfolioInputSpec("legacy-two", secondIndex)
        ], outDir));

        var shared = Assert.Single(result.Report.SharedSurfaces.Rows, row => row.SurfaceKind == "legacy-data");
        Assert.Equal(2, shared.SourceLabels.Count);
        Assert.Contains(shared.Metadata, pair => pair.Key == "groupKeyHash" && !string.IsNullOrWhiteSpace(pair.Value));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.json"));
        Assert.Contains("Shared Portfolio Surfaces", markdown, StringComparison.Ordinal);
        Assert.Contains("sharedSurfaces", json, StringComparison.Ordinal);
        foreach (var output in new[] { markdown, json })
        {
            Assert.DoesNotContain("ldm:shared-portfolio-model", output, StringComparison.Ordinal);
            Assert.DoesNotContain("PrivateCustomer", output, StringComparison.Ordinal);
            Assert.DoesNotContain("TenantSecrets", output, StringComparison.Ordinal);
        }
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
    public async Task Portfolio_before_after_manifests_project_surface_and_edge_diff_rows()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var outDir = Path.Combine(temp.Path, "comparison");
        var beforeScan = Manifest("api", ScannerVersions.TraceMap, "git-api");
        var afterScan = Manifest("api", ScannerVersions.TraceMap, "git-api");
        SqliteIndexWriter.Write(beforeIndex, beforeScan, [
            PackageFact(beforeScan, "Telemetry.Core", "nuget", "Api.csproj", "PackageReference", "1.0.0"),
            PackageFact(beforeScan, "Removed.Package", "nuget", "Api.csproj", "PackageReference", "1.0.0"),
            CallEdgeFact(beforeScan, line: 20)
        ]);
        SqliteIndexWriter.Write(afterIndex, afterScan, [
            PackageFact(afterScan, "Telemetry.Core", "nuget", "Api.csproj", "PackageReference", "2.0.0"),
            PackageFact(afterScan, "Added.Package", "nuget", "Api.csproj", "PackageReference", "1.0.0"),
            CallEdgeFact(afterScan, line: 21)
        ]);
        await WriteManifestAsync(beforeManifest, [("api", beforeIndex)]);
        await WriteManifestAsync(afterManifest, [("api", afterIndex)]);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions(
            [],
            outDir,
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest));

        Assert.Equal(PortfolioReportStatuses.Available, result.Report.PortfolioDiff.Status);
        Assert.Equal(PortfolioReportClassifications.ActionableStaticEvidence, result.Report.PortfolioDiff.RollupClassification);
        Assert.Contains(result.Report.PortfolioDiff.Rows, row => row.ChangeKind == "ChangedSurfaceEvidence" && HasMetadata(row, "displayName", "Telemetry.Core"));
        Assert.Contains(result.Report.PortfolioDiff.Rows, row => row.ChangeKind == "AddedSurfaceEvidence" && HasMetadata(row, "displayName", "Added.Package"));
        Assert.Contains(result.Report.PortfolioDiff.Rows, row => row.ChangeKind == "RemovedSurfaceEvidence" && HasMetadata(row, "displayName", "Removed.Package"));
        var changedSurface = Assert.Single(result.Report.PortfolioDiff.Rows, row => row.ChangeKind == "ChangedSurfaceEvidence" && HasMetadata(row, "displayName", "Telemetry.Core"));
        Assert.True(HasMetadata(changedSurface, "beforeFilePath", "Api.csproj"));
        Assert.True(HasMetadata(changedSurface, "afterFilePath", "Api.csproj"));
        Assert.True(HasMetadata(changedSurface, "beforeStartLine", "5"));
        Assert.True(HasMetadata(changedSurface, "afterStartLine", "5"));
        var changedEdge = Assert.Single(result.Report.PortfolioDiff.Rows, row => row.ChangeKind == "ChangedEdgeEvidence" && row.EvidenceTier == EvidenceTiers.Tier1Semantic);
        Assert.True(HasMetadata(changedEdge, "beforeFilePath", "Services/OrderService.cs"));
        Assert.True(HasMetadata(changedEdge, "afterFilePath", "Services/OrderService.cs"));
        Assert.True(HasMetadata(changedEdge, "beforeStartLine", "20"));
        Assert.True(HasMetadata(changedEdge, "afterStartLine", "21"));
        Assert.Equal(PortfolioReportClassifications.ActionableStaticEvidence, result.Report.Summary.RollupClassification);

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.md"));
        Assert.Contains("ChangedSurfaceEvidence", markdown);
        Assert.Contains("ChangedEdgeEvidence", markdown);
    }

    [Fact]
    public async Task Portfolio_comparison_projected_rows_downgrade_ambiguous_identity()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var outDir = Path.Combine(temp.Path, "comparison");
        var beforeScan = Manifest("api", ScannerVersions.TraceMap, "git-api-before");
        var afterScan = Manifest("api", ScannerVersions.TraceMap, "git-api-after");
        SqliteIndexWriter.Write(beforeIndex, beforeScan, [
            PackageFact(beforeScan, "Telemetry.Core", "nuget", "Api.csproj", "PackageReference", "1.0.0")
        ]);
        SqliteIndexWriter.Write(afterIndex, afterScan, [
            PackageFact(afterScan, "Telemetry.Core", "nuget", "Api.csproj", "PackageReference", "2.0.0")
        ]);
        await WriteManifestAsync(beforeManifest, [("api", beforeIndex)]);
        await WriteManifestAsync(afterManifest, [("api", afterIndex)]);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions(
            [],
            outDir,
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "IdentityAmbiguous" && gap.Section == "portfolioDiff");
        Assert.Contains(result.Report.PortfolioDiff.Rows, row =>
            row.ChangeKind == "ChangedSurfaceEvidence"
            && HasMetadata(row, "displayName", "Telemetry.Core")
            && row.Classification == PortfolioReportClassifications.ReviewRecommended);
    }

    [Fact]
    public async Task Portfolio_comparison_ignores_absolute_path_hashes_for_package_identity()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var outDir = Path.Combine(temp.Path, "comparison");
        var beforeScan = Manifest("api", ScannerVersions.TraceMap, "git-api");
        var afterScan = Manifest("api", ScannerVersions.TraceMap, "git-api");
        SqliteIndexWriter.Write(beforeIndex, beforeScan, [
            PackageFact(beforeScan, "Telemetry.Core", "nuget", Path.Combine(temp.Path, "before-root", "Api.csproj"), "PackageReference")
        ]);
        SqliteIndexWriter.Write(afterIndex, afterScan, [
            PackageFact(afterScan, "Telemetry.Core", "nuget", Path.Combine(temp.Path, "after-root", "Api.csproj"), "PackageReference")
        ]);
        await WriteManifestAsync(beforeManifest, [("api", beforeIndex)]);
        await WriteManifestAsync(afterManifest, [("api", afterIndex)]);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions(
            [],
            outDir,
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest));

        Assert.DoesNotContain(result.Report.PortfolioDiff.Rows, row => row.ChangeKind.EndsWith("SurfaceEvidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Portfolio_comparison_redacts_unsafe_surface_values_and_manifest_display_fields()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var outDir = Path.Combine(temp.Path, "comparison");
        var beforeScan = Manifest("api", ScannerVersions.TraceMap, "git-api");
        var afterScan = Manifest("api", ScannerVersions.TraceMap, "git-api");
        SqliteIndexWriter.Write(beforeIndex, beforeScan, [
            UnsafeSqlFact(beforeScan),
            UnsafeUrlFact(beforeScan),
            ConfigFact(beforeScan, "appsettings.json", "ConnectionStrings:Default", "Server=db;Password=before-secret;")
        ]);
        SqliteIndexWriter.Write(afterIndex, afterScan, [
            UnsafeSqlFact(afterScan),
            UnsafeUrlFact(afterScan),
            ConfigFact(afterScan, "appsettings.json", "ConnectionStrings:Default", "Server=db;Password=after-secret;"),
            PackageFact(afterScan, "Added.Package", "nuget", "Api.csproj", "PackageReference")
        ]);
        await WriteManifestWithDisplayFieldsAsync(beforeManifest, "api|[label](bad)", beforeIndex);
        await WriteManifestWithDisplayFieldsAsync(afterManifest, "api|[label](bad)", afterIndex);

        await PortfolioReporter.WriteAsync(new PortfolioReportOptions(
            [],
            outDir,
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.json"));
        foreach (var output in new[] { markdown, json })
        {
            Assert.DoesNotContain(temp.Path, output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RAW_SQL_SENTINEL", output, StringComparison.Ordinal);
            Assert.DoesNotContain("SNIPPET_SENTINEL", output, StringComparison.Ordinal);
            Assert.DoesNotContain("https://private.example.test", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("URL_SECRET", output, StringComparison.Ordinal);
            Assert.DoesNotContain("SUPER_SECRET_TOKEN", output, StringComparison.Ordinal);
            Assert.DoesNotContain("Password=before-secret", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password=after-secret", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("[portfolio](bad)", output, StringComparison.Ordinal);
            Assert.DoesNotContain("[snapshot](bad)", output, StringComparison.Ordinal);
            Assert.DoesNotContain("[label](bad)", output, StringComparison.Ordinal);
            Assert.DoesNotContain("[group](bad)", output, StringComparison.Ordinal);
            Assert.DoesNotContain("[role](bad)", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Portfolio_comparison_tracks_legacy_data_model_changes_by_safe_identity()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var outDir = Path.Combine(temp.Path, "comparison");
        var beforeScan = Manifest("legacy", ScannerVersions.TraceMap, "git-legacy");
        var afterScan = Manifest("legacy", ScannerVersions.TraceMap, "git-legacy");
        SqliteIndexWriter.Write(beforeIndex, beforeScan, [
            LegacyDataModelFact(beforeScan, "ldm:portfolio-model", "reduced", "formula-redacted")
        ]);
        SqliteIndexWriter.Write(afterIndex, afterScan, [
            LegacyDataModelFact(afterScan, "ldm:portfolio-model", "reduced", "formula-redacted;query-redacted;connection:prod-db;select CardNumber from Vault.Customers")
        ]);
        await WriteManifestAsync(beforeManifest, [("legacy", beforeIndex)]);
        await WriteManifestAsync(afterManifest, [("legacy", afterIndex)]);

        var result = await PortfolioReporter.WriteAsync(new PortfolioReportOptions(
            [],
            outDir,
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest));

        var row = Assert.Single(result.Report.PortfolioDiff.Rows, diff => diff.ChangeKind == "ChangedSurfaceEvidence");
        Assert.Equal(PortfolioReportClassifications.ReviewRecommended, row.Classification);
        Assert.True(HasMetadata(row, "surfaceKind", "legacy-data"));
        Assert.True(HasMetadata(row, "surfaceSubtype", "data-model"));
        Assert.True(HasMetadata(row, "legacyDataMetadataFormat", "nhibernate-hbm"));
        Assert.True(HasMetadata(row, "legacyDataCoverageLabel", "reduced"));
        Assert.Contains(row.Metadata, pair => pair.Key == "legacyDataStableModelKeyHash" && !string.IsNullOrWhiteSpace(pair.Value));
        Assert.Contains(row.Metadata, pair =>
            pair.Key == "legacyDataLimitations"
            && pair.Value.Contains("formula-redacted", StringComparison.Ordinal)
            && pair.Value.Contains("query-redacted", StringComparison.Ordinal)
            && pair.Value.Contains("limitation-hash:", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Report.PortfolioDiff.Rows, diff => diff.ChangeKind is "AddedSurfaceEvidence" or "RemovedSurfaceEvidence");

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "portfolio-report.json"));
        Assert.Contains("limitation-hash:", json, StringComparison.Ordinal);
        foreach (var output in new[] { markdown, json })
        {
            Assert.Contains("ChangedSurfaceEvidence", output, StringComparison.Ordinal);
            Assert.DoesNotContain("connection:prod-db", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("select CardNumber", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Vault.Customers", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Server=prod-db", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PrivateCustomer", output, StringComparison.Ordinal);
            Assert.DoesNotContain("ldm:portfolio-model", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Portfolio_legacy_data_diff_identity_uses_fallback_hash_when_keys_are_missing()
    {
        var first = LegacyDataSurfaceForIdentityFallback("fact-a");
        var second = LegacyDataSurfaceForIdentityFallback("fact-b");

        var firstMetadata = SurfaceDiffIdentityMetadataForTest(first);
        var secondMetadata = SurfaceDiffIdentityMetadataForTest(second);
        var firstFallback = Assert.Single(firstMetadata, pair => pair.Key == "identityFallbackHash").Value;
        var secondFallback = Assert.Single(secondMetadata, pair => pair.Key == "identityFallbackHash").Value;

        Assert.False(string.IsNullOrWhiteSpace(firstFallback));
        Assert.False(string.IsNullOrWhiteSpace(secondFallback));
        Assert.NotEqual(firstFallback, secondFallback);
        Assert.DoesNotContain(firstMetadata, pair => pair.Key is "legacyDataStableModelKeyHash" or "legacyDataDisplayNameHash");
    }

    [Fact]
    public void Portfolio_legacy_data_diff_identity_includes_model_kind_with_display_hash_keys()
    {
        var entity = LegacyDataSurfaceForIdentityFallback("fact-a", modelKind: "entity", displayNameHash: "same-display-hash");
        var column = LegacyDataSurfaceForIdentityFallback("fact-b", modelKind: "column", displayNameHash: "same-display-hash");

        var entityMetadata = SurfaceDiffIdentityMetadataForTest(entity);
        var columnMetadata = SurfaceDiffIdentityMetadataForTest(column);

        Assert.Contains(entityMetadata, pair => pair.Key == "legacyDataModelKind" && pair.Value == "entity");
        Assert.Contains(columnMetadata, pair => pair.Key == "legacyDataModelKind" && pair.Value == "column");
        Assert.NotEqual(JoinedMetadataForTest(entityMetadata), JoinedMetadataForTest(columnMetadata));
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

    private static CodeFact PackageFact(ScanManifest manifest, string name, string ecosystem, string file, string group, string version = "1.0.0")
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
                ["version"] = version
            });
    }

    private static CodeFact LegacyDataModelFact(ScanManifest manifest, string stableModelKey, string coverageLabel, string limitations)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.LegacyDataEntityDeclared,
            RuleIds.LegacyDataOrmNHibernate,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("Mappings/EntityModel.hbm.xml", 8, 16, null, "test", "test/1.0"),
            targetSymbol: "PrivateCustomer",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["connectionString"] = "Server=prod-db;Password=synthetic-secret",
                ["containerHash"] = "tenant-secrets-hash",
                ["containerName"] = "TenantSecrets",
                ["coverageLabel"] = coverageLabel,
                ["descriptorRole"] = "conceptual",
                ["displayName"] = "PrivateCustomer",
                ["displayNameHash"] = FactFactory.Hash("PrivateCustomer", 32),
                ["displayNameRedaction"] = "hashed-unsafe-identifier",
                ["filter"] = "tenant_id = 42",
                ["filterHash"] = "filter-hash",
                ["formula"] = "select CardNumber from Vault.Customers",
                ["formulaHash"] = "formula-hash",
                ["limitations"] = limitations,
                ["localPath"] = "C:\\private\\customer",
                ["metadataFormat"] = "nhibernate-hbm",
                ["metadataHash"] = "metadata-hash",
                ["metadataKind"] = "NHibernateHbm",
                ["modelKind"] = "entity",
                ["providerUrl"] = "https://private.example/nhibernate",
                ["query"] = "select CardNumber from Vault.Customers where tenant_id = 42",
                ["queryHash"] = "query-hash",
                ["remote"] = "git@github.com:private/customer.git",
                ["stableModelKey"] = stableModelKey
            });
    }

    private static CombinedDependencySurfaceRow LegacyDataSurfaceForIdentityFallback(
        string factId,
        string? modelKind = null,
        string? displayNameHash = null)
    {
        return new CombinedDependencySurfaceRow(
            SurfaceKind: "legacy-data",
            DisplayName: "unknown-legacy-data",
            SourceIndexId: "source",
            SourceLabel: "source",
            ScanId: "scan",
            CommitSha: "commit",
            CombinedFactId: factId,
            OriginalFactId: factId,
            FactType: FactTypes.LegacyDataEntityDeclared,
            RuleId: RuleIds.LegacyDataOrmNHibernate,
            EvidenceTier: EvidenceTiers.Tier2Structural,
            FilePath: "Mappings/EntityModel.hbm.xml",
            StartLine: 8,
            EndLine: 16,
            HttpMethod: null,
            NormalizedPathKey: null,
            OperationName: null,
            TableName: null,
            ColumnNames: null,
            SourceKind: null,
            ShapeHash: null,
            TextHash: null,
            TextLength: null,
            PackageName: null,
            Version: null,
            ConfigKey: null,
            LegacyDataMetadataFormat: "nhibernate-hbm",
            LegacyDataModelKind: modelKind,
            LegacyDataDescriptorRole: "conceptual",
            LegacyDataDisplayNameHash: displayNameHash,
            SurfaceSubtype: "data-model");
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

    private static CodeFact CallEdgeFact(ScanManifest manifest, string targetSymbol = "Api.Services.OrderService.Load()", int line = 20)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.CallEdge,
            RuleIds.CSharpSemanticMethodInvocation,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan("Services/OrderService.cs", line, line, null, "test", "test/1.0"),
            sourceSymbol: "Api.Controllers.Orders.Get()",
            targetSymbol: targetSymbol,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["callKind"] = "Invocation"
            });
    }

    private static CodeFact UnsafeSqlFact(ScanManifest manifest)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.SqlTextUsed,
            RuleIds.DatabaseSqlText,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan("Data/OrdersRepository.cs", 30, 30, null, "test", "test/1.0"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["sqlText"] = "select * from Users where password = 'RAW_SQL_SENTINEL'",
                ["sourceSnippet"] = "SNIPPET_SENTINEL",
                ["secretCandidate"] = "SUPER_SECRET_TOKEN",
                ["textHash"] = FactFactory.Hash("select * from Users where password = 'RAW_SQL_SENTINEL'", 32),
                ["textLength"] = "56",
                ["sqlSourceKind"] = "literal"
            });
    }

    private static CodeFact UnsafeUrlFact(ScanManifest manifest)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("Clients/OrdersClient.cs", 12, 12, null, "test", "test/1.0"),
            targetSymbol: "GET /api/orders/{}",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethod"] = "GET",
                ["methodName"] = "GET",
                ["normalizedPathKey"] = "/api/orders/{}",
                ["normalizedPathTemplate"] = "/api/orders/{}",
                ["rawUrl"] = "https://private.example.test/api/orders/1?token=URL_SECRET",
                ["urlKind"] = "template"
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

    private static Task WriteManifestWithDisplayFieldsAsync(string path, string label, string indexPath)
    {
        var json = JsonSerializer.Serialize(
            new
            {
                version = "1.0",
                portfolioId = "demo|[portfolio](bad)",
                snapshotId = "snap`[snapshot](bad)",
                inputs = new[]
                {
                    new
                    {
                        label,
                        indexPath = Path.GetFileName(indexPath),
                        group = "ops|[group](bad)",
                        roleTags = new[] { "[role](bad)" }
                    }
                }
            },
            new JsonSerializerOptions { WriteIndented = true });
        return File.WriteAllTextAsync(path, json);
    }

    private static bool HasMetadata(PortfolioDiffRow row, string key, string value)
    {
        return row.Metadata.Any(pair => pair.Key == key && pair.Value == value);
    }

    private static bool HasMetadata(PortfolioSurfaceRow row, string key, string value)
    {
        return row.Metadata.Any(pair => pair.Key == key && pair.Value == value);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> SurfaceDiffIdentityMetadataForTest(CombinedDependencySurfaceRow surface)
    {
        var method = typeof(PortfolioReporter).GetMethod(
            "SurfaceDiffIdentityMetadata",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IReadOnlyList<KeyValuePair<string, string>>>(
            method.Invoke(null, [surface]));
    }

    private static string JoinedMetadataForTest(IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        return string.Join(";", metadata.Select(pair => $"{pair.Key}={pair.Value}"));
    }
}
