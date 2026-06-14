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
        Assert.DoesNotContain("https://example.invalid", json, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task Diff_source_scope_only_does_not_emit_false_selector_no_match()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        await WriteSimpleCombinedAsync(temp, beforeCombined, "before");
        await WriteSimpleCombinedAsync(temp, afterCombined, "after");

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "diff"),
            Scope: "sources",
            Source: "client"));

        Assert.DoesNotContain(result.Report.Gaps, gap => gap.Classification == CombinedDependencyDiffClassifications.SelectorNoMatch);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == CombinedDependencyDiffClassifications.NoDiffEvidence);
    }

    [Fact]
    public async Task Diff_downgrades_one_sided_evidence_when_opposite_snapshot_has_reduced_coverage()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterReducedCombined = Path.Combine(temp.Path, "after-reduced.sqlite");
        var beforeReducedCombined = Path.Combine(temp.Path, "before-reduced.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var fullManifest = Manifest("api", "tracemap-milestone15");
        var reducedManifest = Manifest("api", "tracemap-milestone15", analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial");

        await WriteSingleCombinedAsync(temp, beforeCombined, "before-full", fullManifest, [RouteFact(fullManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)]);
        await WriteSingleCombinedAsync(temp, afterReducedCombined, "after-reduced", reducedManifest, []);
        await WriteSingleCombinedAsync(temp, beforeReducedCombined, "before-reduced", reducedManifest, []);
        await WriteSingleCombinedAsync(temp, afterCombined, "after-full", fullManifest, [RouteFact(fullManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)]);

        var removed = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterReducedCombined,
            Path.Combine(temp.Path, "removed"),
            Scope: "endpoints"));
        var added = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeReducedCombined,
            afterCombined,
            Path.Combine(temp.Path, "added"),
            Scope: "endpoints"));

        Assert.Contains(removed.Report.EndpointDiffs, row => row.Classification == CombinedDependencyDiffClassifications.RemovedWithAfterGap);
        Assert.DoesNotContain(removed.Report.EndpointDiffs, row => row.Classification == CombinedDependencyDiffClassifications.Removed);
        Assert.Contains(added.Report.EndpointDiffs, row => row.Classification == CombinedDependencyDiffClassifications.AddedWithBeforeGap);
        Assert.DoesNotContain(added.Report.EndpointDiffs, row => row.Classification == CombinedDependencyDiffClassifications.Added);
    }

    [Fact]
    public async Task Diff_scope_all_does_not_require_path_opt_in()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        await WriteSimpleCombinedAsync(temp, beforeCombined, "before");
        await WriteSimpleCombinedAsync(temp, afterCombined, "after");

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "diff"),
            Scope: "all"));

        Assert.DoesNotContain("paths", result.Report.Query.Scopes);
        Assert.Empty(result.Report.PathDiffs);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == CombinedDependencyDiffClassifications.NoDiffEvidence);
    }

    [Fact]
    public async Task Diff_reports_changed_evidence_when_span_moves_but_metadata_is_same()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", "tracemap-milestone15");

        await WriteSingleCombinedAsync(temp, beforeCombined, "before", manifest, [RouteFact(manifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)]);
        await WriteSingleCombinedAsync(temp, afterCombined, "after", manifest, [RouteFact(manifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 22)]);

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "diff"),
            Scope: "endpoints"));

        var row = Assert.Single(result.Report.EndpointDiffs);
        Assert.Equal(CombinedDependencyDiffClassifications.ChangedEvidence, row.Classification);
        Assert.Equal(10, row.Before?.StartLine);
        Assert.Equal(22, row.After?.StartLine);
    }

    [Fact]
    public async Task Diff_warns_but_does_not_block_when_only_checkout_root_hash_changes()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Manifest("api", "tracemap-milestone15", gitRootHash: "git-root-before");
        var afterManifest = Manifest("api", "tracemap-milestone15", gitRootHash: "git-root-after");

        await WriteSingleCombinedAsync(temp, beforeCombined, "before", beforeManifest, [RouteFact(beforeManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)]);
        await WriteSingleCombinedAsync(temp, afterCombined, "after", afterManifest, [RouteFact(afterManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)]);

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "diff"),
            Scope: "sources"));

        Assert.Contains(result.Report.CoverageWarnings, warning => warning.Contains("different checkout root", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "SourceIdentityChanged");
    }

    [Fact]
    public async Task Diff_downgrades_path_removal_when_after_snapshot_has_reduced_coverage()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var reducedServer = Manifest("server", "tracemap-milestone15", analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var beforeClient = Path.Combine(temp.Path, "before-client.sqlite");
        var beforeServer = Path.Combine(temp.Path, "before-server.sqlite");
        var afterClient = Path.Combine(temp.Path, "after-client.sqlite");
        var afterServer = Path.Combine(temp.Path, "after-server.sqlite");

        SqliteIndexWriter.Write(beforeClient, client, [HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5)]);
        SqliteIndexWriter.Write(beforeServer, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10, controller),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        SqliteIndexWriter.Write(afterClient, client, [HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5)]);
        SqliteIndexWriter.Write(afterServer, reducedServer, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeClient, beforeServer], beforeCombined, ["client", "server"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterClient, afterServer], afterCombined, ["client", "server"]));

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "diff"),
            Scope: "paths",
            IncludePaths: true,
            Source: "client",
            Endpoint: "GET /api/orders/{}",
            Surface: "sql-query"));

        Assert.Contains(result.Report.PathDiffs, row => row.Classification == CombinedDependencyDiffClassifications.RemovedWithAfterGap);
        Assert.DoesNotContain(result.Report.PathDiffs, row => row.Classification == CombinedDependencyDiffClassifications.Removed);
    }

    [Fact]
    public async Task Diff_does_not_collapse_same_sql_shape_with_different_source_kind()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", "tracemap-milestone15");

        await WriteSingleCombinedAsync(temp, beforeCombined, "before", manifest, []);
        await WriteSingleCombinedAsync(temp, afterCombined, "after", manifest, [
            QueryPatternFact(manifest, null, "Infrastructure/Orders.cs", 10, "literal-string", "same-shape"),
            QueryPatternFact(manifest, null, "Infrastructure/orders.sql", 1, "sql-file", "same-shape")
        ]);

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "diff"),
            Scope: "surfaces",
            Surface: "sql-query"));

        Assert.Equal(2, result.Report.SurfaceDiffs.Count(row => row.Classification == CombinedDependencyDiffClassifications.Added));
        Assert.Equal(2, result.Report.SurfaceDiffs.Select(row => row.StableKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(result.Report.SurfaceDiffs, row => row.After!.SafeMetadata.Any(pair => pair.Key == "sqlSourceKind" && pair.Value == "literal-string"));
        Assert.Contains(result.Report.SurfaceDiffs, row => row.After!.SafeMetadata.Any(pair => pair.Key == "sqlSourceKind" && pair.Value == "sql-file"));
    }

    [Fact]
    public async Task Diff_marks_hash_only_and_volatile_sql_identity_as_review_tier()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", "tracemap-milestone15");

        await WriteSingleCombinedAsync(temp, beforeCombined, "before", manifest, []);
        await WriteSingleCombinedAsync(temp, afterCombined, "after", manifest, [
            HashOnlySqlTextFact(manifest, "Infrastructure/orders.sql", 1),
            VolatileSqlTextFact(manifest, "Infrastructure/unknown.sql", 2)
        ]);

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "diff"),
            Scope: "surfaces",
            Surface: "sql-query"));

        Assert.Contains(result.Report.SurfaceDiffs, row =>
            row.Classification == CombinedDependencyDiffClassifications.NeedsReviewDiff
            && row.CoverageCaveats.Any(caveat => caveat.Code == "HashOnlyEvidence"));
        Assert.Contains(result.Report.SurfaceDiffs, row =>
            row.Classification == CombinedDependencyDiffClassifications.NeedsReviewDiff
            && row.CoverageCaveats.Any(caveat => caveat.Code == "VolatileIdentity"));
    }

    [Fact]
    public async Task Diff_treats_package_version_and_scope_as_changed_metadata_not_identity()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", "tracemap-milestone15");

        await WriteSingleCombinedAsync(temp, beforeCombined, "before", manifest, [
            PackageFact(manifest, "Newtonsoft.Json", "13.0.1", "runtime", "src/App.csproj", 12)
        ]);
        await WriteSingleCombinedAsync(temp, afterCombined, "after", manifest, [
            PackageFact(manifest, "Newtonsoft.Json", "13.0.3", "development", "src/App.csproj", 12)
        ]);

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "diff"),
            Scope: "surfaces",
            Surface: "package-config"));

        var row = Assert.Single(result.Report.SurfaceDiffs);
        Assert.Equal(CombinedDependencyDiffClassifications.ChangedEvidence, row.Classification);
        Assert.Contains(row.After!.SafeMetadata, pair => pair.Key == "version" && pair.Value == "13.0.3");
        Assert.Contains(row.After.SafeMetadata, pair => pair.Key == "dependencyScope" && pair.Value == "development");
        Assert.DoesNotContain(result.Report.SurfaceDiffs, diff => diff.Classification == CombinedDependencyDiffClassifications.Added);
        Assert.DoesNotContain(result.Report.SurfaceDiffs, diff => diff.Classification == CombinedDependencyDiffClassifications.Removed);
    }

    [Fact]
    public async Task Diff_keeps_same_package_name_in_different_ecosystems_distinct()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", "tracemap-milestone15");

        await WriteSingleCombinedAsync(temp, beforeCombined, "before", manifest, []);
        await WriteSingleCombinedAsync(temp, afterCombined, "after", manifest, [
            PackageFact(manifest, "logging", "1.0.0", "runtime", "src/App.csproj", 12, ecosystem: "nuget", manifestKind: "csproj"),
            PackageFact(manifest, "logging", "1.0.0", "runtime", "package.json", 4, ecosystem: "npm", manifestKind: "package.json")
        ]);

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "diff"),
            Scope: "surfaces",
            Surface: "package-config"));

        Assert.Equal(2, result.Report.SurfaceDiffs.Count(row => row.Classification == CombinedDependencyDiffClassifications.Added));
        Assert.Equal(2, result.Report.SurfaceDiffs.Select(row => row.StableKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(result.Report.SurfaceDiffs, row => row.After!.SafeMetadata.Any(pair => pair.Key == "ecosystem" && pair.Value == "nuget"));
        Assert.Contains(result.Report.SurfaceDiffs, row => row.After!.SafeMetadata.Any(pair => pair.Key == "ecosystem" && pair.Value == "npm"));
    }

    [Fact]
    public async Task Diff_keeps_same_package_in_different_manifests_distinct()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", "tracemap-milestone15");

        await WriteSingleCombinedAsync(temp, beforeCombined, "before", manifest, []);
        await WriteSingleCombinedAsync(temp, afterCombined, "after", manifest, [
            PackageFact(manifest, "Newtonsoft.Json", "13.0.3", "runtime", "src/App/App.csproj", 12),
            PackageFact(manifest, "Newtonsoft.Json", "13.0.3", "runtime", "tests/App.Tests/App.Tests.csproj", 9)
        ]);

        var result = await CombinedDependencyDiffer.WriteAsync(new CombinedDependencyDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "diff"),
            Scope: "surfaces",
            Surface: "package-config"));

        Assert.Equal(2, result.Report.SurfaceDiffs.Count(row => row.Classification == CombinedDependencyDiffClassifications.Added));
        Assert.Equal(2, result.Report.SurfaceDiffs.Select(row => row.StableKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(result.Report.SurfaceDiffs, row => row.After!.FilePath == "src/App/App.csproj");
        Assert.Contains(result.Report.SurfaceDiffs, row => row.After!.FilePath == "tests/App.Tests/App.Tests.csproj");
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

    private static CodeFact RouteFact(ScanManifest manifest, string method, string template, string key, string file, int line, string? methodSymbol = null)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: methodSymbol,
            targetSymbol: methodSymbol ?? $"{method} {template}",
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

    private static CodeFact QueryPatternFact(ScanManifest manifest, string file, int line)
    {
        return QueryPatternFact(manifest, null, file, line);
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string? sourceSymbol, string file, int line)
    {
        return QueryPatternFact(manifest, sourceSymbol, file, line, "literal-string", "shape123");
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string? sourceSymbol, string file, int line, string sourceKind, string shapeHash)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: "orders",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationName"] = "SELECT",
                ["tableName"] = "orders",
                ["columnNames"] = "id;status",
                ["sqlSourceKind"] = sourceKind,
                ["queryShapeHash"] = shapeHash,
                ["rawSql"] = "select * from orders"
            });
    }

    private static CodeFact HashOnlySqlTextFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.SqlTextUsed,
            RuleIds.DatabaseSqlText,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "sql",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["textHash"] = "text-only-hash",
                ["textLength"] = "42"
            });
    }

    private static CodeFact VolatileSqlTextFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.SqlTextUsed,
            RuleIds.DatabaseSqlText,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "sql",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal));
    }

    private static CodeFact PackageFact(
        ScanManifest manifest,
        string packageName,
        string version,
        string dependencyScope,
        string file,
        int line,
        string ecosystem = "nuget",
        string manifestKind = "csproj")
    {
        return FactFactory.Create(
            manifest,
            FactTypes.PackageReferenced,
            RuleIds.ProjectFile,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: packageName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["dependencyGroup"] = manifestKind == "package.json" ? "dependencies" : "PackageReference",
                ["dependencyScope"] = dependencyScope,
                ["ecosystem"] = ecosystem,
                ["manifestKind"] = manifestKind,
                ["packageManager"] = ecosystem,
                ["packageName"] = packageName,
                ["surfaceKind"] = "package-config",
                ["version"] = version
            });
    }
}
