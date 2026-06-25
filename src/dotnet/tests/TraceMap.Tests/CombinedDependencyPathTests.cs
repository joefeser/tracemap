using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CombinedDependencyPathTests
{
    [Fact]
    public async Task Paths_writes_endpoint_to_sql_markdown_and_json_without_mutating_combined_index()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "paths");
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
        var before = await Sha256Async(combinedPath);

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                outDir,
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "client",
                ToSurface: "sql-query"));

        Assert.Equal(before, await Sha256Async(combinedPath));
        Assert.True(File.Exists(Path.Combine(outDir, "paths-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "paths-report.json")));
        var path = Assert.Single(result.Report.Paths);
        Assert.Equal("sql-query", path.Nodes.Last().SurfaceKind);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "endpoint-match");
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "calls");
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "surface-evidence");
        Assert.Equal("NeedsReviewPath", path.Classification);

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.md"));
        Assert.Contains("TraceMap Paths Report", markdown);
        Assert.Contains("source transition:", markdown);
        Assert.Contains("sql-query", markdown);
        Assert.DoesNotContain("Inventory rows are capped", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select * from orders", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);

        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.json"));
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);
        var document = JsonSerializer.Deserialize<CombinedDependencyPathReport>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(document);
        Assert.Equal(result.Report.Paths.Count, document!.Paths.Count);

        var secondOutDir = Path.Combine(temp.Path, "paths-second");
        await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                secondOutDir,
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "client",
                ToSurface: "sql-query"));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "paths-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "paths-report.json")));
    }

    [Fact]
    public async Task Paths_traverse_static_interface_dispatch_candidates_as_needs_review()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "paths");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.IOrderService.Get(System.Int32)";
        var implementation = "Server.OrderService.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            SymbolRelationshipFact(server, implementation, service, "Services/OrderService.cs", 18),
            CallFact(server, implementation, repository, "Services/OrderService.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                outDir,
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "server",
                ToSurface: "sql-query"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewPath, path.Classification);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "calls"
            && edge.FilePath == "Controllers/OrdersController.cs"
            && edge.StartLine == 14);
        var candidate = Assert.Single(path.Edges, edge => edge.EdgeKind == "interface-candidate");
        Assert.Equal("combined.dispatch-candidate.v1", candidate.RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, candidate.EvidenceTier);
        Assert.NotEmpty(candidate.SupportingCombinedEdgeIds);
        Assert.Contains(path.Notes, note => note.Code == "StaticDispatchCandidate");
        Assert.Equal("sql-query", path.Nodes.Last().SurfaceKind);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit"
            && gap.Reason == "cycle"
            && (gap.NodeId == candidate.FromNodeId || gap.NodeId == candidate.ToNodeId));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.md"));
        Assert.Contains("interface-candidate", markdown);
        Assert.Contains("StaticDispatchCandidate", markdown);
        Assert.DoesNotContain("runtime target", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Paths_traverse_explicit_interface_dispatch_candidates_from_relationship_identity()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.IOrderService.Get(System.Int32)";
        var explicitImplementation = "Server.OrderService.Server.IOrderService.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            SymbolRelationshipFact(server, explicitImplementation, service, "Services/OrderService.cs", 18),
            CallFact(server, explicitImplementation, repository, "Services/OrderService.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "server",
                ToSurface: "sql-query"));

        var path = result.Report.Paths.First(candidatePath => candidatePath.Edges.Any(edge => edge.EdgeKind == "interface-candidate"));
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewPath, path.Classification);
        var candidate = Assert.Single(path.Edges, edge => edge.EdgeKind == "interface-candidate");
        Assert.Equal("combined.dispatch-candidate.v1", candidate.RuleId);
        Assert.Contains(path.Nodes, node => node.DisplayName == explicitImplementation);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "MemberCandidateUnavailable");
    }

    [Fact]
    public async Task Paths_traverse_static_override_dispatch_candidates_as_needs_review()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var abstraction = "Server.OrderProcessor.Process(System.Int32)";
        var overrideMethod = "Server.PriorityOrderProcessor.Process(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, abstraction, "Controllers/OrdersController.cs", 14),
            SymbolRelationshipFact(server, overrideMethod, abstraction, "Services/PriorityOrderProcessor.cs", 18, relationshipKind: "Overrides"),
            CallFact(server, overrideMethod, repository, "Services/PriorityOrderProcessor.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "server",
                ToSurface: "sql-query"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewPath, path.Classification);
        var candidate = Assert.Single(path.Edges, edge => edge.EdgeKind == "override-candidate");
        Assert.Equal("combined.dispatch-candidate.v1", candidate.RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, candidate.EvidenceTier);
        Assert.NotEmpty(candidate.SupportingCombinedEdgeIds);
        Assert.Contains(path.Notes, note => note.Code == "StaticDispatchCandidate");
        Assert.Equal("sql-query", path.Nodes.Last().SurfaceKind);
    }

    [Fact]
    public async Task Paths_traverse_static_override_chain_candidates_with_stable_output()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var abstraction = "Server.OrderProcessor.Process(System.Int32)";
        var intermediate = "Server.MidOrderProcessor.Process(System.Int32)";
        var overrideMethod = "Server.APriorityOrderProcessor.Process(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, abstraction, "Controllers/OrdersController.cs", 14),
            SymbolRelationshipFact(server, intermediate, abstraction, "Services/MidOrderProcessor.cs", 18, relationshipKind: "Overrides"),
            SymbolRelationshipFact(server, overrideMethod, intermediate, "Services/APriorityOrderProcessor.cs", 19, relationshipKind: "Overrides"),
            CallFact(server, overrideMethod, repository, "Services/APriorityOrderProcessor.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths-first"),
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "server",
                ToSurface: "sql-query",
                MaxDepth: 5));
        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths-second"),
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "server",
                ToSurface: "sql-query",
                MaxDepth: 5));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewPath, path.Classification);
        var candidate = Assert.Single(path.Edges, edge => edge.EdgeKind == "override-candidate");
        Assert.Equal("combined.dispatch-candidate.v1", candidate.RuleId);
        Assert.True(candidate.SupportingCombinedEdgeIds.Count >= 2);
        Assert.Contains(path.Nodes, node => node.DisplayName == overrideMethod);
        Assert.DoesNotContain(path.Nodes, node => node.DisplayName == intermediate);
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(temp.Path, "paths-first", "paths-report.md")),
            await File.ReadAllTextAsync(Path.Combine(temp.Path, "paths-second", "paths-report.md")));
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(temp.Path, "paths-first", "paths-report.json")),
            await File.ReadAllTextAsync(Path.Combine(temp.Path, "paths-second", "paths-report.json")));
    }

    [Fact]
    public void Static_dispatch_override_chain_respects_depth_bound_and_cycle_protection()
    {
        var nodes = Enumerable.Range(0, 7)
            .ToDictionary(
                index => $"method-{index}",
                index => new StaticDispatchCandidateNode(
                    $"method-{index}",
                    "Method",
                    $"Server.Processor{index}.Process(System.Int32)",
                    "server",
                    "server",
                    "commit",
                    $"Services/Processor{index}.cs",
                    10 + index,
                    10 + index),
                StringComparer.Ordinal);
        var relationships = Enumerable.Range(1, 6)
            .Select(index => new StaticDispatchRelationshipEdge(
                $"rel-{index}",
                "overrides",
                "Overrides",
                $"method-{index}",
                $"method-{index - 1}",
                EvidenceTiers.Tier1Semantic,
                [$"fact-{index}"],
                [$"edge-{index}"],
                $"Services/Processor{index}.cs",
                20 + index,
                20 + index))
            .Append(new StaticDispatchRelationshipEdge(
                "rel-cycle",
                "overrides",
                "Overrides",
                "method-0",
                "method-2",
                EvidenceTiers.Tier1Semantic,
                ["fact-cycle"],
                ["edge-cycle"],
                "Services/Processor0.cs",
                40,
                40))
            .Reverse()
            .ToArray();

        var first = StaticDispatchCandidateBuilder.Build(nodes, relationships, options: new StaticDispatchCandidateBuildOptions(CandidateLimit: 20));
        var second = StaticDispatchCandidateBuilder.Build(nodes, relationships, options: new StaticDispatchCandidateBuildOptions(CandidateLimit: 20));

        var rootCandidates = first.Edges
            .Where(edge => edge.AbstractionSymbolId == "method-0")
            .ToArray();
        Assert.Equal(StaticDispatchCandidateBuilder.DefaultMaxOverrideDepth, rootCandidates.Length);
        Assert.DoesNotContain(rootCandidates, edge => edge.CandidateSymbolId == "method-6");
        Assert.All(rootCandidates, edge => Assert.Equal(StaticDispatchBridgeKinds.OverrideMember, edge.BridgeKind));
        Assert.Equal(
            first.Edges.Select(edge => edge.CandidateId).ToArray(),
            second.Edges.Select(edge => edge.CandidateId).ToArray());
    }

    [Fact]
    public async Task Paths_static_dispatch_candidate_output_is_byte_stable()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.IOrderService.Get(System.Int32)";
        var implementation = "Server.OrderService.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            SymbolRelationshipFact(server, implementation, service, "Services/OrderService.cs", 18),
            CallFact(server, implementation, repository, "Services/OrderService.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths-first"),
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "server",
                ToSurface: "sql-query"));
        await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths-second"),
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "server",
                ToSurface: "sql-query"));

        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(temp.Path, "paths-first", "paths-report.md")),
            await File.ReadAllTextAsync(Path.Combine(temp.Path, "paths-second", "paths-report.md")));
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(temp.Path, "paths-first", "paths-report.json")),
            await File.ReadAllTextAsync(Path.Combine(temp.Path, "paths-second", "paths-report.json")));
    }

    [Fact]
    public async Task Paths_cap_static_interface_dispatch_candidate_fanout_with_gap()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.StatusController.Get(System.Int32)";
        var service = "Server.IStatusService.Status(System.Int32)";
        var repository = "Server.StatusRepository.Query(System.Int32)";
        var facts = new List<CodeFact>
        {
            RouteFact(server, "GET", "/api/status", "/api/status", controller, "Controllers/StatusController.cs", 10),
            CallFact(server, controller, service, "Controllers/StatusController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/StatusRepository.cs", 31)
        };
        for (var index = 0; index < 11; index++)
        {
            var implementation = $"Server.StatusService{index}.Status(System.Int32)";
            facts.Add(SymbolRelationshipFact(server, implementation, service, $"Services/StatusService{index}.cs", 18 + index));
            facts.Add(CallFact(server, implementation, repository, $"Services/StatusService{index}.cs", 40 + index));
        }

        SqliteIndexWriter.Write(serverIndex, server, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromEndpoint: "GET /api/status",
                FromSource: "server",
                ToSurface: "sql-query"));

        Assert.Equal(10, result.Report.Paths.Count);
        Assert.All(result.Report.Paths, path =>
        {
            Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewPath, path.Classification);
            Assert.Contains(path.Edges, edge => edge.EdgeKind == "interface-candidate");
        });
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DispatchCandidateFanOut");
        Assert.Equal("combined.dispatch-gap.v1", gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal("Services/StatusService10.cs", gap.FilePath);
        Assert.Equal(28, gap.StartLine);
        Assert.Equal(28, gap.EndLine);
        Assert.Equal(server.CommitSha, gap.CommitSha);
        Assert.Equal(server.ScannerVersion, gap.ExtractorVersion);
        Assert.Equal("combined-symbol-relationships", gap.EvidenceScope);
    }

    [Fact]
    public async Task Paths_do_not_fan_out_from_concrete_implementation_through_interface_relationship()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var service = "Server.IOrderService.Get(System.Int32)";
        var implementationA = "Server.OrderServiceA.Get(System.Int32)";
        var implementationB = "Server.OrderServiceB.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            SymbolRelationshipFact(server, implementationA, service, "Services/OrderServiceA.cs", 18),
            SymbolRelationshipFact(server, implementationB, service, "Services/OrderServiceB.cs", 28),
            CallFact(server, implementationB, repository, "Services/OrderServiceB.cs", 31),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 40)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromSymbol: implementationA,
                ToSurface: "sql-query"));

        Assert.DoesNotContain(result.Report.Paths, path => path.Edges.Any(edge => edge.EdgeKind == "interface-candidate"));
        Assert.DoesNotContain(result.Report.Paths, path => path.Nodes.Any(node => node.DisplayName == implementationB));
    }

    [Fact]
    public void Paths_dispatch_candidate_rule_ids_are_documented()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var ruleId in new[] { "combined.dispatch-candidate.v1", "combined.dispatch-gap.v1" })
        {
            var start = catalog.IndexOf($"- id: {ruleId}", StringComparison.Ordinal);
            Assert.True(start >= 0, $"Missing rule catalog entry for {ruleId}.");
            var block = catalog[start..Math.Min(catalog.Length, start + 900)];
            Assert.Contains("limitations:", block);
            Assert.Contains("evidenceTier:", block);
        }

        var gapStart = catalog.IndexOf("- id: combined.dispatch-gap.v1", StringComparison.Ordinal);
        Assert.True(gapStart >= 0, "Missing rule catalog entry for combined.dispatch-gap.v1.");
        var gapBlock = catalog[gapStart..Math.Min(catalog.Length, gapStart + 900)];
        Assert.Contains("DispatchCandidateFanOut gap", gapBlock);
    }

    [Fact]
    public async Task Paths_build_report_does_not_write_outputs()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "paths-build-only");
        var server = Manifest("server", "tracemap-milestone15");

        SqliteIndexWriter.Write(serverIndex, server, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var report = await CombinedDependencyPathReporter.BuildReportAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                outDir,
                FromSource: "server",
                ToSurface: "sql-query"));

        Assert.Empty(report.Paths);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task Paths_project_legacy_data_descriptor_nodes_and_exclude_analysis_gaps()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "paths");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Read(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            LegacyDataEntityFact(server, repository, "CustomerLedger", "Models/Store.dbml", 21),
            LegacyDataGapFact(server, "Models/Store.dbml", 30)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                outDir,
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "server",
                ToSurface: "legacy-data"));

        var path = Assert.Single(result.Report.Paths);
        var terminal = path.Nodes.Last();
        Assert.Equal("legacy-data", terminal.SurfaceKind);
        Assert.Equal("data-model", terminal.SurfaceSubtype);
        Assert.StartsWith("entity:hash:", terminal.DisplayName, StringComparison.Ordinal);
        Assert.Equal("dbml", terminal.OperationName);
        Assert.Equal("dbml", terminal.SourceKind);
        Assert.Equal("ldm:path-model-key", terminal.ShapeHash);
        Assert.DoesNotContain("CustomerLedger", terminal.DisplayName, StringComparison.Ordinal);
        Assert.DoesNotContain(path.Nodes, node => node.CombinedFactId is not null && node.RuleId == RuleIds.LegacyDataDbml && node.NodeKind == FactTypes.AnalysisGap);

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.json"));
        Assert.Contains("legacy-data", markdown);
        Assert.Contains("\"surfaceSubtype\": \"data-model\"", json);
        Assert.DoesNotContain("CustomerLedger", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerLedger", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Paths_link_legacy_data_descriptor_target_symbols_without_displaying_descriptor_names()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "paths");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var generatedModel = "Server.LegacyModels.ModelType";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, generatedModel, "Controllers/OrdersController.cs", 14),
            LegacyDataEntityFact(server, null, "CustomerLedger", "Models/Store.dbml", 21, generatedModel)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                outDir,
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "server",
                ToSurface: "legacy-data"));

        var path = Assert.Single(result.Report.Paths);
        var terminal = path.Nodes.Last();
        Assert.Equal("legacy-data", terminal.SurfaceKind);
        Assert.Null(terminal.SymbolId);
        Assert.StartsWith("entity:hash:", terminal.DisplayName, StringComparison.Ordinal);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "surface-evidence");
        Assert.DoesNotContain("CustomerLedger", terminal.DisplayName, StringComparison.Ordinal);

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.json"));
        Assert.DoesNotContain("CustomerLedger", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerLedger", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_paths_report_labels_missing_winforms_precision_as_availability_gap_for_older_indexes()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "old.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "legacy-paths");
        var manifest = Manifest("legacy", "tracemap-before-winforms");

        SqliteIndexWriter.Write(index, manifest, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["legacy"]));

        var report = await CombinedDependencyPathReporter.BuildReportAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                outDir,
                View: LegacyFlowReportConstants.View));

        Assert.Empty(report.Paths);
        Assert.Contains(report.Gaps, gap =>
            gap.GapKind == "NoRootsFound"
            && gap.Message.Contains("WinForms", StringComparison.Ordinal)
            && gap.RuleId == RuleIds.LegacyFlowInputAvailability);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task Paths_adds_deterministic_value_origin_notes_without_replacing_canonical_classification()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "paths");
        var manifest = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.String)";
        var service = "Server.OrderService.Query(System.String)";
        var sourceParameter = $"{controller}:System.String request";
        var targetParameter = $"{service}:System.String id";

        SqliteIndexWriter.Write(index, manifest, [
            ArgumentPassedFact(manifest, controller, service, "System.String request", "id", "System.String", "Controllers/OrdersController.cs", 12),
            QueryPatternFact(manifest, targetParameter, "Infrastructure/OrderService.cs", 24)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            combinedPath,
            outDir,
            FromSymbol: sourceParameter,
            ToSurface: "sql-query"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.ProbableStaticPath, path.Classification);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "parameter-forward");
        Assert.Contains(path.Notes, note => note.Code == "ValueOriginClassification" && note.Message.Contains("ProbableStaticValuePath", StringComparison.Ordinal));
        Assert.Contains(path.Notes, note => note.Code == "ParameterForwardingBoundary");

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.json"));
        Assert.Contains("ValueOriginClassification", markdown);
        Assert.Contains("ProbableStaticValuePath", markdown);
        Assert.Contains("\"notes\"", json);
        Assert.Contains("ProbableStaticValuePath", json);

        var secondOutDir = Path.Combine(temp.Path, "paths-second");
        await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            combinedPath,
            secondOutDir,
            FromSymbol: sourceParameter,
            ToSurface: "sql-query"));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "paths-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "paths-report.json")));
    }

    [Fact]
    public void Value_origin_classification_preserves_unknown_analysis_gap()
    {
        var method = typeof(CombinedDependencyPathReporter).GetMethod(
            "ClassifyValueOrigin",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        var edges = new CombinedPathEdge[]
        {
            new CombinedPathEdge(
                "edge-endpoint",
                "endpoint-match",
                "client",
                "server",
                CombinedEndpointClassifications.UnknownAnalysisGap,
                "combined.paths.endpoint-match.v1",
                EvidenceTiers.Tier4Unknown,
                ["fact-client"],
                [],
                "src/client.ts",
                10,
                10),
            new CombinedPathEdge(
                "edge-value",
                "parameter-forward",
                "server",
                "service",
                "EvidenceEdge",
                RuleIds.CSharpSemanticParameterForwarding,
                EvidenceTiers.Tier1Semantic,
                ["fact-flow"],
                ["edge-flow"],
                "Controllers/OrdersController.cs",
                12,
                12)
        };
        var classification = method.Invoke(null, [edges]);

        Assert.Equal(CombinedValueOriginClassifications.UnknownAnalysisGap, classification);
    }

    [Fact]
    public async Task Paths_from_endpoint_matches_multi_method_route_without_stored_path_key()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Upsert(System.Int32)";
        var repository = "Server.OrderRepository.Save(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFactWithoutPathKey(server, "GET,POST", "/api/orders/{id:int}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromEndpoint: "GET /api/orders/{id}",
                ToSurface: "sql-query"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal("GET,POST", path.Nodes.First().HttpMethod);
        Assert.Equal("/api/orders/{}", path.Nodes.First().NormalizedPathKey);
        Assert.Equal("sql-query", path.Nodes.Last().SurfaceKind);
    }

    [Fact]
    public async Task Paths_from_client_symbol_can_traverse_http_fact_to_server_surface()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var clientMethod = "Client.OrderService.loadOrder(System.Int32)";
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5, clientMethod)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromSymbol: clientMethod,
                ToSurface: "sql-query"));

        var path = Assert.Single(result.Report.Paths, candidate => candidate.Edges.Any(edge => edge.EdgeKind == "endpoint-match"));
        Assert.Equal(clientMethod, path.Nodes.First().SymbolId);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "fact-attached-to-symbol" && edge.FromNodeId == path.Nodes.First().NodeId);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "endpoint-match");
        Assert.Equal("sql-query", path.Nodes.Last().SurfaceKind);
    }

    [Fact]
    public async Task Paths_from_endpoint_and_symbol_narrows_duplicate_endpoint_facts()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var clientMethodA = "Client.OrderService.loadOrderA(System.Int32)";
        var clientMethodB = "Client.OrderService.loadOrderB(System.Int32)";
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5, clientMethodA),
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 7, clientMethodB)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromEndpoint: "GET /api/orders/{}",
                FromSymbol: clientMethodB,
                ToSurface: "sql-query"));

        var path = Assert.Single(result.Report.Paths, candidate => candidate.Edges.Any(edge => edge.EdgeKind == "endpoint-match"));
        Assert.Equal("client", path.Nodes.First().SourceLabel);
        Assert.Equal(7, path.Nodes.First().StartLine);
        Assert.DoesNotContain(result.Report.Paths, candidate => candidate.Nodes.First().StartLine == 5);
    }

    [Fact]
    public async Task Paths_without_surface_filter_does_not_stop_at_endpoint_surfaces()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.HealthController.Get()";

        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/health", "/health", "src/health.ts", 3)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/health", "/health", controller, "HealthController.cs", 6)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromEndpoint: "GET /health"));

        Assert.Empty(result.Report.Paths);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch");
    }

    [Fact]
    public async Task Paths_reconciles_source_local_symbols_to_connect_scanned_route_call_and_query_shapes()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "global::EndpointServerSample.Controllers.RunnerController.GetById(string runnerId)";
        var syntaxController = "EndpointServerSample.Controllers.RunnerController.GetById";
        var repository = "global::EndpointServerSample.Services.RunnerRepository.Query(string runnerId)";

        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/admin/runner/get-by-id/{runnerId}", "/api/admin/runner/get-by-id/{}", "src/runner.ts", 5)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/admin/runner/get-by-id/{runnerId:guid}", "/api/admin/runner/get-by-id/{}", syntaxController, "Controllers/RunnerController.cs", 10),
            CallFact(server, controller, repository, "Controllers/RunnerController.cs", 14),
            QueryPatternFact(server, "Query", "Services/RunnerRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromEndpoint: "GET /api/admin/runner/get-by-id/{}",
                ToSurface: "sql-query"));

        var path = Assert.Single(result.Report.Paths, candidate => candidate.Edges.Any(edge => edge.EdgeKind == "endpoint-match"));
        Assert.Equal("sql-query", path.Nodes.Last().SurfaceKind);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "endpoint-match");
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "calls");
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "symbol-reconciliation" && edge.RuleId == "combined.paths.symbol-reconciliation.v1");
        Assert.Equal("NeedsReviewPath", path.Classification);
    }

    [Fact]
    public async Task Paths_does_not_reconcile_distinct_overload_signatures()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.RunnerController.Get()";
        var queryByNumber = "Server.RunnerRepository.Query(int runnerNumber)";
        var queryById = "Server.RunnerRepository.Query(string runnerId)";

        SqliteIndexWriter.Write(serverIndex, server, [
            CallFact(server, controller, queryByNumber, "Controllers/RunnerController.cs", 14),
            QueryPatternFact(server, queryById, "Infrastructure/RunnerRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromSymbol: queryByNumber,
                ToSurface: "sql-query"));

        Assert.Empty(result.Report.Paths);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "NoPathFound");
    }

    [Fact]
    public async Task Paths_supports_package_config_surface_and_symbol_selector()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        var startup = "Server.Startup.Configure()";
        SqliteIndexWriter.Write(index, manifest, [
            ConfigFact(manifest, startup, "appsettings.json", 4),
            PackageFact(manifest, startup, "Server.csproj", 7)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths.json"),
                Format: "json",
                FromSymbol: startup,
                ToSurface: "package-config",
                SurfaceName: "ConnectionStrings:Default"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(startup, path.Nodes.First().SymbolId);
        Assert.Equal("package-config", path.Nodes.Last().SurfaceKind);
        Assert.Equal("ConnectionStrings:Default", path.Nodes.Last().ConfigKey);
        Assert.Null(result.MarkdownPath);
        Assert.True(File.Exists(result.JsonPath));
    }

    [Fact]
    public async Task Paths_treats_database_column_mapping_as_persistence_not_query_execution()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        var model = "Server.Models.Order";
        SqliteIndexWriter.Write(index, manifest, [
            MethodFact(manifest, model, "Models/Order.cs", 3),
            DatabaseColumnMappingFact(manifest, model, "Models/Order.cs", 7)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var queryResult = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "query-paths"),
                FromSymbol: model,
                ToSurface: "sql-query"));

        Assert.Empty(queryResult.Report.Paths);

        var persistenceResult = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "persistence-paths"),
                FromSymbol: model,
                ToSurface: "sql-persistence"));

        var path = Assert.Single(persistenceResult.Report.Paths);
        Assert.Equal("sql-persistence", path.Nodes.Last().SurfaceKind);
        Assert.Equal("SqlPersistenceSurface", path.Nodes.Last().NodeKind);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "surface-evidence");
    }

    [Fact]
    public async Task Paths_reports_selector_no_match_and_rejects_edge_kind_surface()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        SqliteIndexWriter.Write(index, manifest, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromSymbol: "Missing.Symbol",
                ToSurface: "sql-query"));

        Assert.Empty(result.Report.Paths);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch");
        Assert.All(result.Report.Gaps, gap =>
        {
            Assert.False(string.IsNullOrWhiteSpace(gap.RuleId));
            Assert.False(string.IsNullOrWhiteSpace(gap.EvidenceTier));
        });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "bad"),
                ToSurface: "calls")));
        Assert.Contains("edge kind", ex.Message);
    }

    [Fact]
    public async Task Paths_reports_unknown_gap_for_no_path_with_reduced_contributing_source()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15", analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial");
        var method = "Server.Orphan.Run()";
        SqliteIndexWriter.Write(index, manifest, [
            MethodFact(manifest, method, "Orphan.cs", 4),
            QueryPatternFact(manifest, null, "Infrastructure/UnlinkedRepository.cs", 12)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromSymbol: method,
                ToSurface: "sql-query"));

        Assert.Empty(result.Report.Paths);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "UnknownAnalysisGap" && gap.Classification == "UnknownAnalysisGap");
    }

    [Fact]
    public async Task Paths_reports_unknown_gap_when_reachable_server_has_reduced_coverage()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15", analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial");
        var controller = "Server.OrdersController.Get(System.Int32)";
        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            QueryPatternFact(server, null, "Infrastructure/UnlinkedRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromEndpoint: "GET /api/orders/{}",
                FromSource: "client",
                ToSurface: "sql-query"));

        Assert.Empty(result.Report.Paths);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "UnknownAnalysisGap" && gap.SourceLabel == "server");
    }

    [Fact]
    public async Task Paths_does_not_treat_surface_target_symbol_as_code_symbol()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        SqliteIndexWriter.Write(index, manifest, [
            QueryPatternFact(manifest, null, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromSymbol: "orders",
                ToSurface: "sql-query"));

        Assert.Empty(result.Report.Paths);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch");
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "UnlinkedSurface");
    }

    [Fact]
    public async Task Paths_cli_writes_summary_and_parses_escaped_source_pair()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.HealthController.Get()";
        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/health", "/health", "src/health.ts", 3)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/health", "/health", controller, "HealthController.cs", 6),
            ConfigFact(server, controller, "appsettings.json", 2)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client:v1", "server:v1"]));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(
            [
                "paths",
                "--index", combinedPath,
                "--out", Path.Combine(temp.Path, "paths"),
                "--source-pair", " client\\:v1 : server\\:v1 ",
                "--to-surface", "package-config"
            ],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("TraceMap paths completed:", output.ToString());
        Assert.Contains("Graph nodes:", output.ToString());
        Assert.True(File.Exists(Path.Combine(temp.Path, "paths", "paths-report.md")));
    }

    [Fact]
    public async Task Paths_preserves_selector_truncation_when_search_is_not_truncated()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        var facts = Enumerable.Range(0, 260)
            .Select(index => PackageFact(manifest, $"Server.Startup.Configure{index}()", "Server.csproj", index + 1))
            .ToArray();
        SqliteIndexWriter.Write(index, manifest, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                combinedPath,
                Path.Combine(temp.Path, "paths"),
                FromSource: "server",
                ToSurface: "package-config"));

        Assert.True(result.Report.Summary.Truncated);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "selector-candidates");
    }

    private static ScanManifest Manifest(
        string repo,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded")
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

    private static CodeFact RouteFactWithoutPathKey(ScanManifest manifest, string method, string template, string methodSymbol, string file, int line)
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
                ["routeTemplates"] = template
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

    private static CodeFact SymbolRelationshipFact(
        ScanManifest manifest,
        string implementationMethodSymbol,
        string interfaceMethodSymbol,
        string file,
        int line,
        string evidenceTier = EvidenceTiers.Tier1Semantic,
        string relationshipKind = "ImplementsInterfaceMember")
    {
        return FactFactory.Create(
            manifest,
            FactTypes.SymbolRelationship,
            RuleIds.CSharpSemanticSymbolRelationship,
            evidenceTier,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: implementationMethodSymbol,
            targetSymbol: interfaceMethodSymbol,
            contractElement: relationshipKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["relationshipKind"] = relationshipKind,
                ["sourceSymbolDisplayName"] = implementationMethodSymbol,
                ["sourceSymbolId"] = implementationMethodSymbol,
                ["targetSymbolDisplayName"] = interfaceMethodSymbol,
                ["targetSymbolId"] = interfaceMethodSymbol
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

    private static CodeFact QueryPatternFact(ScanManifest manifest, string? sourceSymbol, string file, int line)
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
                ["sqlSourceKind"] = "literal-string",
                ["queryShapeHash"] = "shape123"
            });
    }

    private static CodeFact LegacyDataEntityFact(ScanManifest manifest, string? sourceSymbol, string displayName, string file, int line, string? targetSymbol = null)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.LegacyDataEntityDeclared,
            RuleIds.LegacyDataDbml,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: targetSymbol ?? displayName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverageLabel"] = "full",
                ["descriptorRole"] = "conceptual",
                ["displayName"] = displayName,
                ["metadataFormat"] = "dbml",
                ["metadataHash"] = "metadata-hash",
                ["metadataKind"] = "Dbml",
                ["modelKind"] = "entity",
                ["stableModelKey"] = "ldm:path-model-key"
            });
    }

    private static CodeFact LegacyDataGapFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.LegacyDataDbml,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "Legacy data descriptor gap requires review.",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = "AmbiguousLegacyDataModelIdentity",
                ["metadataFormat"] = "dbml"
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

    private static CodeFact DatabaseColumnMappingFact(ScanManifest manifest, string sourceSymbol, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.DatabaseColumnMapping,
            RuleIds.CSharpSemanticContractMapping,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: $"{sourceSymbol}.Status",
            contractElement: "status",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["mappingKind"] = "DatabaseColumnMapping",
                ["mappedName"] = "status",
                ["columnName"] = "status",
                ["containingType"] = sourceSymbol
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

    private static CodeFact MethodFact(ScanManifest manifest, string methodSymbol, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.MethodDeclared,
            RuleIds.CSharpSemanticDeclarations,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: methodSymbol);
    }

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
