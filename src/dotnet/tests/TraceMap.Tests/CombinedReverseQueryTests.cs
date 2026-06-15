using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CombinedReverseQueryTests
{
    [Fact]
    public async Task Reverse_writes_endpoint_roots_to_sql_markdown_and_json_without_mutating_index()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "reverse");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 34, "invoices")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));
        var before = await Sha256Async(combinedPath);

        var result = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            outDir,
            Surface: "sql-query",
            SurfaceName: "orders",
            To: "endpoints"));

        Assert.Equal(before, await Sha256Async(combinedPath));
        Assert.True(File.Exists(Path.Combine(outDir, "reverse-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "reverse-report.json")));
        Assert.Contains(result.Report.SelectedSurfaces, surface => surface.SurfaceKind == "sql-query" && surface.DisplayName == "shape:shape123");
        Assert.Contains(result.Report.ReverseRoots, root => root.RootKind is "EndpointClient" or "EndpointRoute");
        Assert.Contains(result.Report.Paths, path => path.Nodes.First().NodeKind is "EndpointClient" or "EndpointRoute" && path.Nodes.Last().SurfaceKind == "sql-query");
        AssertRootPathLinks(result.Report);
        Assert.All(result.Report.Paths, path =>
        {
            Assert.False(string.IsNullOrWhiteSpace(path.PathId));
            Assert.Contains("combined.reverse.path.v1", path.RuleIds);
        });

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "reverse-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "reverse-report.json"));
        Assert.Contains("TraceMap Reverse Report", markdown);
        Assert.Contains("sql-query", markdown);
        Assert.DoesNotContain("select * from orders", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);

        var document = JsonSerializer.Deserialize<CombinedReverseReport>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(document);
        Assert.Equal("combined-reverse-query", document!.ReportType);

        var secondOutDir = Path.Combine(temp.Path, "reverse-second");
        await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            secondOutDir,
            Surface: "sql-query",
            SurfaceName: "orders",
            To: "endpoints"));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "reverse-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "reverse-report.json")));
    }

    [Fact]
    public async Task Reverse_selectors_caps_and_source_matching_are_deterministic()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        SqliteIndexWriter.Write(index, manifest, [
            QueryPatternFact(manifest, "Server.Repo.One()", "Repo.cs", 10),
            ConfigFact(manifest, "Server.Startup.Configure()", "appsettings.json", 4),
            PackageFact(manifest, "Server.Startup.Configure()", "Server.csproj", 5)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse"),
            Source: "SERVER",
            SurfaceName: "orders",
            MaxSurfaces: 1));

        Assert.Single(result.Report.SelectedSurfaces);
        Assert.Equal("shape:shape123", result.Report.SelectedSurfaces[0].DisplayName);
        Assert.Equal("SERVER", result.Report.Query.Source);
        Assert.Equal("CaseInsensitiveExact", result.Report.Query.SurfaceNameMatchMode);

        var broad = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse-broad"),
            SurfaceName: "ConnectionStrings:Default",
            To: "all"));
        Assert.Contains(broad.Report.SelectedSurfaces, surface => surface.SurfaceKind == "package-config");
        Assert.Equal("all", broad.Report.Query.To);

        var symbols = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse-symbols"),
            Surface: "sql-query",
            SurfaceName: "orders",
            To: "symbols"));
        Assert.Contains(symbols.Report.ReverseRoots, root => root.RootKind is "Symbol" or "Method" or "Type");
        AssertRootPathLinks(symbols.Report);

        var sources = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse-sources"),
            Surface: "sql-query",
            SurfaceName: "orders",
            To: "sources"));
        Assert.All(sources.Report.ReverseRoots, root => Assert.Equal("Source", root.RootKind));
        Assert.Single(sources.Report.ReverseRoots);
        Assert.Contains(sources.Report.ReverseRoots, root => root.DisplayName == "server");
        Assert.All(sources.Report.Paths, path => Assert.Equal("sql-query", path.Nodes.Last().SurfaceKind));
        AssertRootPathLinks(sources.Report);

        var all = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse-all"),
            Surface: "sql-query",
            SurfaceName: "orders",
            To: "all"));
        Assert.Contains(all.Report.ReverseRoots, root => root.RootKind == "Source");
        Assert.Contains(all.Report.ReverseRoots, root => root.RootKind is "Symbol" or "Method" or "Type");
        AssertRootPathLinks(all.Report);
    }

    [Fact]
    public async Task Reverse_preserves_sql_source_kind_in_surface_identity()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        SqliteIndexWriter.Write(index, manifest, [
            QueryPatternFact(manifest, "Server.Repo.One()", "Repo.cs", 10, "orders", "literal-string", "same-shape"),
            QueryPatternFact(manifest, "Server.Repo.Two()", "queries/orders.sql", 1, "orders", "sql-file", "same-shape")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse"),
            Surface: "sql-query",
            SurfaceName: "same-shape",
            To: "sources"));

        Assert.Equal(2, result.Report.SelectedSurfaces.Count);
        Assert.Equal(2, result.Report.SelectedSurfaces.Select(surface => surface.StableKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(result.Report.SelectedSurfaces, surface => surface.Metadata.TryGetValue("sqlSourceKind", out var value) && value == "literal-string");
        Assert.Contains(result.Report.SelectedSurfaces, surface => surface.Metadata.TryGetValue("sqlSourceKind", out var value) && value == "sql-file");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "DuplicateIdentity");
    }

    [Fact]
    public async Task Reverse_selector_no_match_and_reduced_no_path_are_rule_backed_gaps()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15", analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial");
        SqliteIndexWriter.Write(index, manifest, [
            QueryPatternFact(manifest, null, "Repo.cs", 10)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var selector = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "selector"),
            Surface: "sql-query",
            SurfaceName: "missing"));
        Assert.Empty(selector.Report.SelectedSurfaces);
        Assert.Contains(selector.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch" && gap.RuleId == "combined.reverse.selector.v1");

        var reduced = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reduced"),
            Surface: "sql-query",
            SurfaceName: "orders"));
        Assert.Empty(reduced.Report.Paths);
        Assert.Contains(reduced.Report.Gaps, gap => gap.GapKind == "UnknownAnalysisGap" && gap.RuleId == "combined.reverse.root.v1");
    }

    [Fact]
    public async Task Reverse_full_coverage_no_path_emits_no_reverse_path_evidence()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        SqliteIndexWriter.Write(index, manifest, [
            QueryPatternFact(manifest, null, "Repo.cs", 10)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse"),
            Surface: "sql-query",
            SurfaceName: "orders"));

        Assert.Empty(result.Report.Paths);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "NoReversePathEvidence" && gap.Classification == "NoReversePathEvidence");
    }

    [Fact]
    public async Task Reverse_preserves_value_origin_notes_and_supporting_edge_ids()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "reverse");
        var manifest = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.String)";
        var service = "Server.OrderService.Query(System.String)";
        var targetParameter = $"{service}:System.String id";

        SqliteIndexWriter.Write(index, manifest, [
            ArgumentPassedFact(manifest, controller, service, "System.String request", "id", "System.String", "Controllers/OrdersController.cs", 12),
            QueryPatternFact(manifest, targetParameter, "Infrastructure/OrderService.cs", 24)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            outDir,
            Surface: "sql-query",
            SurfaceName: "orders",
            To: "all"));

        var valueOriginPaths = result.Report.Paths
            .Where(candidate => candidate.Edges.Any(edge => edge.EdgeKind == "parameter-forward"))
            .ToArray();
        Assert.NotEmpty(valueOriginPaths);
        var path = valueOriginPaths[0];
        Assert.NotEmpty(path.SupportingEdgeIds);
        Assert.All(valueOriginPaths, candidate =>
        {
            Assert.Contains(candidate.Notes, note => note.Code == "ValueOriginClassification" && note.Message.Contains("ProbableStaticValuePath", StringComparison.Ordinal));
            Assert.Contains(candidate.Notes, note => note.Code == "ParameterForwardingBoundary");
        });

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "reverse-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "reverse-report.json"));
        Assert.Contains("ValueOriginClassification", markdown);
        Assert.Contains("ProbableStaticValuePath", json);

        var secondOutDir = Path.Combine(temp.Path, "reverse-second");
        await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            secondOutDir,
            Surface: "sql-query",
            SurfaceName: "orders",
            To: "all"));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "reverse-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "reverse-report.json")));
    }

    [Fact]
    public async Task Reverse_traversal_caps_emit_rule_backed_truncation_gaps()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var auditRepository = "Server.OrderRepository.QueryAudit(System.Int32)";
        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            CallFact(server, controller, auditRepository, "Controllers/OrdersController.cs", 15),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31),
            QueryPatternFact(server, auditRepository, "Infrastructure/OrderRepository.cs", 34, "invoices")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var depth = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "depth"),
            Surface: "sql-query",
            SurfaceName: "orders",
            MaxDepth: 1));
        Assert.Contains(depth.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "depth" && gap.RuleId == "combined.reverse.truncation.v1");

        var frontier = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "frontier"),
            Surface: "sql-query",
            SurfaceName: "orders",
            MaxFrontier: 1));
        Assert.Contains(frontier.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "frontier" && gap.RuleId == "combined.reverse.truncation.v1");

        var roots = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "roots"),
            Surface: "sql-query",
            SurfaceName: "orders",
            MaxRoots: 1));
        Assert.Single(roots.Report.ReverseRoots);
        Assert.Contains(roots.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "roots" && gap.RuleId == "combined.reverse.truncation.v1");

        var gaps = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "gaps"),
            Surface: "sql-query",
            SurfaceName: "orders",
            MaxDepth: 1,
            MaxGaps: 1));
        Assert.Single(gaps.Report.Gaps);
        Assert.Contains(gaps.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "gaps" && gap.RuleId == "combined.reverse.truncation.v1");

        var pathsPerRoot = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "paths-per-root"),
            Surface: "sql-query",
            To: "endpoints",
            MaxPathsPerRoot: 1));
        Assert.All(pathsPerRoot.Report.ReverseRoots, root => Assert.True(root.PathIds.Count <= 1));
        Assert.Contains(pathsPerRoot.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "paths-per-root" && gap.RuleId == "combined.reverse.truncation.v1");
    }

    [Fact]
    public async Task Reverse_file_output_hashes_raw_surface_values_and_reports_identity_gaps()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outputPath = Path.Combine(temp.Path, "reverse.json");
        var manifest = Manifest("server", "tracemap-milestone15", commitSha: "unknown", gitRootHash: string.Empty);
        const string rawUrl = "https://secret.example.test/api/orders?token=abc123";
        SqliteIndexWriter.Write(index, manifest, [
            HttpClientFact(manifest, "GET", rawUrl, null, "Services/RemoteOrders.cs", 19, "Server.RemoteOrders.Fetch()")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            outputPath,
            Format: "json",
            Surface: "http-client",
            SurfaceName: rawUrl,
            To: "endpoints"));

        Assert.Null(result.MarkdownPath);
        Assert.Equal(outputPath, result.JsonPath);
        Assert.Contains(result.Report.SelectedSurfaces, surface => surface.DisplayName.StartsWith("url:", StringComparison.Ordinal));
        Assert.Contains(result.Report.Paths, path => path.Nodes.Any(node => node.NodeKind == "EndpointClient"));
        Assert.All(result.Report.Paths.SelectMany(path => path.Nodes), node => Assert.DoesNotContain(rawUrl, node.DisplayName, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "IdentityUnverified" && gap.RuleId == "combined.reverse.identity.v1");

        var json = await File.ReadAllTextAsync(outputPath);
        Assert.DoesNotContain(rawUrl, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret.example", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc123", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reverse_duplicate_surface_identity_emits_identity_gap()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        SqliteIndexWriter.Write(index, manifest, [
            QueryPatternFact(manifest, null, "RepoOne.cs", 10),
            QueryPatternFact(manifest, null, "RepoTwo.cs", 20)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse"),
            Surface: "sql-query",
            SurfaceName: "orders"));

        Assert.True(result.Report.SelectedSurfaces.Count >= 2);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "DuplicateIdentity" && gap.RuleId == "combined.reverse.identity.v1");
        Assert.All(result.Report.SelectedSurfaces, surface => Assert.Equal("NeedsReviewSurfaceEvidence", surface.Classification));
    }

    [Fact]
    public async Task Reverse_cli_summary_exit_code_and_validation_errors()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "reverse",
            "--index", combinedPath,
            "--out", Path.Combine(temp.Path, "reverse"),
            "--surface", "sql-query",
            "--surface-name", "orders",
            "--exit-code"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("TraceMap reverse completed:", output.ToString());
        Assert.Contains("Reverse roots:", output.ToString());

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        exitCode = await TraceMapCommand.RunAsync([
            "reverse",
            "--index", combinedPath,
            "--out", Path.Combine(temp.Path, "bad"),
            "--surface", "calls",
            "--exit-code"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("--surface", error.ToString());
    }

    [Fact]
    public async Task Reverse_rejects_single_language_index()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(indexPath, Manifest("single", "tracemap-milestone15"), []);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => CombinedReverseReporter.WriteAsync(
            new CombinedReverseOptions(indexPath, Path.Combine(temp.Path, "reverse"))));
        Assert.Contains("combined index", ex.Message);
    }

    private static ScanManifest Manifest(
        string repo,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded",
        string commitSha = "abc123",
        string? gitRootHash = null)
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            null,
            "main",
            commitSha,
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
            gitRootHash ?? FactFactory.Hash("git-root", 32));
    }

    private static CodeFact HttpClientFact(ScanManifest manifest, string method, string template, string? key, string file, int line, string? sourceSymbol = null)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["httpMethod"] = method,
            ["methodName"] = method,
            ["normalizedPathTemplate"] = template,
            ["urlKind"] = "template",
            ["clientFramework"] = "test"
        };
        if (!string.IsNullOrWhiteSpace(key))
        {
            properties["normalizedPathKey"] = key;
        }

        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: $"{method} {template}",
            contractElement: method,
            properties: properties);
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

    private static CodeFact QueryPatternFact(ScanManifest manifest, string? sourceSymbol, string file, int line, string tableName = "orders")
    {
        return QueryPatternFact(manifest, sourceSymbol, file, line, tableName, "literal-string", "shape123");
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string? sourceSymbol, string file, int line, string tableName, string sourceKind, string shapeHash)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: tableName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationName"] = "SELECT",
                ["tableName"] = tableName,
                ["columnNames"] = "id;status",
                ["sqlSourceKind"] = sourceKind,
                ["queryShapeHash"] = shapeHash
            });
    }

    private static CodeFact ConfigFact(ScanManifest manifest, string sourceSymbol, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ConnectionStringDeclared,
            RuleIds.ConfigKey,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: "ConnectionStrings:Default",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["keyPath"] = "ConnectionStrings:Default",
                ["valueHash"] = "hash-only"
            });
    }

    private static CodeFact PackageFact(ScanManifest manifest, string sourceSymbol, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.PackageReferenced,
            RuleIds.ProjectFile,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: "Package.TraceMap.Test",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["package"] = "Package.TraceMap.Test",
                ["version"] = "1.0.0"
            });
    }

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    private static void AssertRootPathLinks(CombinedReverseReport report)
    {
        var pathsById = report.Paths.ToDictionary(path => path.PathId, StringComparer.Ordinal);
        foreach (var root in report.ReverseRoots)
        {
            foreach (var pathId in root.PathIds)
            {
                Assert.True(pathsById.TryGetValue(pathId, out var path), $"Missing path {pathId} referenced by root {root.RootId}.");
                Assert.Equal(root.RootId, path!.RootId);
            }
        }
    }
}
