using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class LegacyFlowCompositionTests
{
    [Fact]
    public async Task Legacy_paths_compose_webforms_event_to_sql_with_stable_redacted_outputs()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var outDir = Path.Combine(temp.Path, "legacy-flows");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var service = "Legacy.Services.OrderService.Save(System.Int32)";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, service, "Pages/Orders.aspx.cs", 27, EvidenceTiers.Tier1Semantic),
            QueryPatternFact(manifest, service, "Data/OrderRepository.cs", 43, tableName: "orders")
        ]);
        var beforeHash = await Sha256Async(index);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: outDir,
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            FromWebFormsEvent: "Submit/OnClick",
            ToSurface: "sql-query"));

        Assert.Equal(beforeHash, await Sha256Async(index));
        Assert.Equal(LegacyFlowReportConstants.SchemaVersion, result.Report.SchemaVersion);
        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.ProbableStaticPath, path.Classification);
        Assert.Equal("webforms-event", path.Nodes.First().NodeKind);
        Assert.Equal("sql-query", path.Nodes.Last().SurfaceKind);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "legacy-root-selection" && edge.RuleId == RuleIds.LegacyFlowRootSelection);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "calls");
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "surface-evidence");

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.json"));
        Assert.Contains("Legacy Static Flow Report", markdown);
        Assert.Contains("possible static paths", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("always reaches", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("executed query", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select * from orders", json, StringComparison.OrdinalIgnoreCase);

        var secondOut = Path.Combine(temp.Path, "legacy-flows-second");
        await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: secondOut,
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            FromWebFormsEvent: "Submit/OnClick",
            ToSurface: "sql-query"));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOut, "paths-report.json")));
    }

    [Fact]
    public async Task Legacy_paths_cap_syntax_only_webforms_paths_at_needs_review()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Status.Status_Click(System.Object,System.EventArgs)";
        var repository = "Legacy.Data.StatusRepository.GetStatus()";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Status.aspx", "Status", "OnClick", "Status_Click", 10, EvidenceTiers.Tier3SyntaxOrTextual),
            WebFormsHandler(manifest, "Pages/Status.aspx.cs", handler, "Status_Click", 20, EvidenceTiers.Tier3SyntaxOrTextual, "wf-binding"),
            CallFact(manifest, handler, repository, "Pages/Status.aspx.cs", 22, EvidenceTiers.Tier3SyntaxOrTextual),
            QueryPatternFact(manifest, repository, "Data/StatusRepository.cs", 30, tableName: "status")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "sql-query"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewStaticPath, path.Classification);
        Assert.DoesNotContain(result.Report.Paths, candidate => candidate.Classification == CombinedDependencyPathClassifications.StrongStaticPath);
    }

    [Fact]
    public async Task Legacy_paths_treat_wcf_operation_as_terminal()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var clientCall = "Legacy.ServiceReference.OrderClient.SubmitOrder(System.Int32)";
        var serviceImpl = "Legacy.Services.OrderService.SubmitOrder(System.Int32)";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, clientCall, "Pages/Orders.aspx.cs", 30, EvidenceTiers.Tier1Semantic),
            WcfMappingFact(manifest, clientCall, "SubmitOrder", "Service References/Order/Reference.cs", 5),
            CallFact(manifest, clientCall, serviceImpl, "Generated/Reference.cs", 40, EvidenceTiers.Tier1Semantic),
            QueryPatternFact(manifest, serviceImpl, "Data/OrderRepository.cs", 52, tableName: "orders")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "wcf-operation"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal("wcf-operation", path.Nodes.Last().SurfaceKind);
        Assert.Null(path.Nodes.Last().SurfaceSubtype);
        Assert.Contains(path.Nodes, node => node.DisplayName.Contains("OrderClient", StringComparison.Ordinal));
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "wcf-service-reference");
        Assert.DoesNotContain(path.Nodes, node => node.SymbolId == serviceImpl);
    }

    [Fact]
    public async Task Legacy_paths_report_availability_and_filter_gaps()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Empty.Button_Click(System.Object,System.EventArgs)";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Empty.aspx", "Button", "OnClick", "Button_Click", 12),
            WebFormsHandler(manifest, "Pages/Empty.aspx.cs", handler, "Button_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            QueryPatternFact(manifest, handler, "Data/EmptyRepository.cs", 30, tableName: "orders")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "sql-query",
            Classification: CombinedDependencyPathClassifications.StrongStaticPath));

        Assert.Empty(result.Report.Paths);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ExtractorUnavailable" && gap.RuleId == RuleIds.LegacyFlowInputAvailability);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ClassificationFilterNoMatch");
    }

    [Fact]
    public async Task Legacy_paths_neutralize_private_combined_labels_and_cli_options()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("private-repo");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var service = "Legacy.Services.OrderService.Save(System.Int32)";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, service, "Pages/Orders.aspx.cs", 27, EvidenceTiers.Tier1Semantic),
            QueryPatternFact(manifest, service, "Data/OrderRepository.cs", 43, tableName: "orders")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["unsafe-secret-label"]));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await TraceMapCommand.RunAsync([
            "paths",
            "--index", combined,
            "--out", Path.Combine(temp.Path, "flows"),
            "--include-legacy-roots",
            "--view", "legacy-flows",
            "--from-webforms-event", "Submit/OnClick",
            "--to-surface", "sql-query",
            "--classification", "ProbableStaticPath"
        ], output, error);

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, error.ToString());
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "flows", "paths-report.json"));
        Assert.DoesNotContain("unsafe-secret-label", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"sourceLabel\": \"source:", json);
    }

    [Fact]
    public async Task Legacy_paths_resolve_supporting_facts_with_source_scoped_original_ids()
    {
        using var temp = new TempDirectory();
        var alphaIndex = Path.Combine(temp.Path, "alpha.sqlite");
        var betaIndex = Path.Combine(temp.Path, "beta.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var service = "Legacy.Services.OrderService.Save(System.Int32)";
        var binding = WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12);
        var handlerFact = WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, binding.FactId);
        var facts = new[]
        {
            binding,
            handlerFact,
            CallFact(manifest, handler, service, "Pages/Orders.aspx.cs", 27, EvidenceTiers.Tier1Semantic),
            QueryPatternFact(manifest, service, "Data/OrderRepository.cs", 43, tableName: "orders")
        };
        SqliteIndexWriter.Write(alphaIndex, manifest, facts);
        SqliteIndexWriter.Write(betaIndex, manifest, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([alphaIndex, betaIndex], combined, ["alpha", "beta"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: combined,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            FromSource: "beta",
            FromWebFormsEvent: "Submit/OnClick",
            ToSurface: "sql-query"));

        var path = Assert.Single(result.Report.Paths);
        var betaSourceId = Assert.Single(path.Nodes.Select(node => node.SourceIndexId).Distinct(StringComparer.Ordinal));
        Assert.All(path.SupportingFactIds, factId => Assert.StartsWith($"{betaSourceId}:", factId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Legacy_paths_use_projection_edges_only_when_no_primitive_path_exists()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var service = "Legacy.Services.OrderService.Save(System.Int32)";
        var binding = WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12);
        var handlerFact = WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, binding.FactId);
        var call = CallFact(manifest, handler, service, "Pages/Orders.aspx.cs", 27, EvidenceTiers.Tier1Semantic);
        var query = QueryPatternFact(manifest, service, "Data/OrderRepository.cs", 43, tableName: "orders");
        SqliteIndexWriter.Write(index, manifest, [
            binding,
            handlerFact,
            call,
            query,
            ProjectionFact(manifest, handler, "Submit_Click", "sql-query", SqlShapeDisplayHash("orders"), [binding.FactId, handlerFact.FactId, call.FactId, query.FactId])
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            FromWebFormsEvent: "Submit/OnClick",
            ToSurface: "sql-query",
            MaxPaths: 1));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.ProbableStaticPath, path.Classification);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "calls");
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "surface-evidence");
        Assert.DoesNotContain(path.Edges, edge => edge.EdgeKind == "webforms-event-flow-projection");
    }

    [Fact]
    public async Task Legacy_paths_preserve_asmx_projection_terminal_surface_kind()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var binding = WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12);
        var handlerFact = WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier2Structural, binding.FactId);
        SqliteIndexWriter.Write(index, manifest, [
            binding,
            handlerFact,
            ProjectionFact(manifest, handler, "Submit_Click", "asmx-operation", FactFactory.Hash("Rate", 32), [binding.FactId, handlerFact.FactId])
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            FromWebFormsEvent: "Submit/OnClick",
            ToSurface: "asmx-operation"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal("asmx-operation", path.Nodes.Last().SurfaceKind);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "webforms-event-flow-projection"
            && edge.RuleId == RuleIds.LegacyFlowStaticTraversal);
        Assert.DoesNotContain(path.Nodes, node => node.SurfaceKind == "dependency-surface");
    }

    [Fact]
    public async Task Legacy_paths_apply_classification_filter_after_legacy_downgrade()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Status.Status_Click(System.Object,System.EventArgs)";
        var service = "Legacy.Services.StatusService.Read(System.Int32)";
        var binding = WebFormsBinding(manifest, "Pages/Status.aspx", "Status", "OnClick", "Status_Click", 12);
        var handlerFact = WebFormsHandler(manifest, "Pages/Status.aspx.cs", handler, "Status_Click", 24, EvidenceTiers.Tier1Semantic, binding.FactId);
        SqliteIndexWriter.Write(index, manifest, [
            binding,
            handlerFact,
            CallFact(manifest, handler, service, "Pages/Status.aspx.cs", 27, EvidenceTiers.Tier1Semantic),
            PackageConfigFact(manifest, service, "Data/StatusRepository.cs", 43, "status")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "package-config",
            Classification: CombinedDependencyPathClassifications.NeedsReviewStaticPath));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewStaticPath, path.Classification);
        Assert.Equal("status", path.Nodes.Last().SurfaceName);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "ClassificationFilterNoMatch");
    }

    [Fact]
    public async Task Legacy_paths_redact_coverage_warnings_and_cap_no_backend_paths()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("legacy-app", analysisLevel: "Level1SyntaxFallback", buildStatus: "Failed");
        var facts = NoBackendFacts(manifest);

        SqliteIndexWriter.Write(index, manifest, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["unsafe-secret-label"]));

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: combined,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "package-config",
            MaxPaths: 1));

        Assert.DoesNotContain(result.Report.CoverageWarnings, warning => warning.Contains("unsafe-secret-label", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.CoverageWarnings, warning => warning.Contains("redacted-hash:", StringComparison.Ordinal));
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "flows", "paths-report.json"));
        var markdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "flows", "paths-report.md"));
        Assert.DoesNotContain("unsafe-secret-label", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unsafe-secret-label", markdown, StringComparison.OrdinalIgnoreCase);

        var fullIndex = Path.Combine(temp.Path, "full.sqlite");
        var fullManifest = Manifest("legacy-app-full");
        SqliteIndexWriter.Write(fullIndex, fullManifest, NoBackendFacts(fullManifest));
        var fullResult = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: fullIndex,
            OutputPath: Path.Combine(temp.Path, "full-flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "package-config",
            MaxPaths: 1));

        Assert.Single(fullResult.Report.Paths);
        Assert.True(fullResult.Report.Summary.Truncated);
        Assert.Contains(fullResult.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "path");

        static List<CodeFact> NoBackendFacts(ScanManifest sourceManifest)
        {
            var unrelated = "Legacy.Services.Unrelated.Read()";
            var rows = new List<CodeFact>
            {
                PackageConfigFact(sourceManifest, unrelated, "App.config", 3, "orders")
            };
            for (var i = 0; i < 3; i++)
            {
                var handlerName = $"Button{i}_Click";
                var handler = $"Legacy.Pages.Empty.Button{i}_Click(System.Object,System.EventArgs)";
                var binding = WebFormsBinding(sourceManifest, $"Pages/Empty{i}.aspx", $"Button{i}", "OnClick", handlerName, 12 + i);
                rows.Add(binding);
                rows.Add(WebFormsHandler(sourceManifest, $"Pages/Empty{i}.aspx.cs", handler, handlerName, 24 + i, EvidenceTiers.Tier1Semantic, binding.FactId));
            }

            return rows;
        }
    }

    [Fact]
    public async Task Paths_omit_legacy_only_json_fields_outside_legacy_view()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("server");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        SqliteIndexWriter.Write(index, manifest, [
            CallFact(manifest, controller, repository, "Controllers/OrdersController.cs", 14, EvidenceTiers.Tier1Semantic),
            QueryPatternFact(manifest, repository, "Infrastructure/OrderRepository.cs", 31, tableName: "orders")
        ]);

        await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "paths"),
            FromSymbol: controller,
            ToSurface: "sql-query"));

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "paths", "paths-report.json"));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("schemaVersion", out _));
        Assert.False(root.TryGetProperty("view", out _));
        Assert.True(root.TryGetProperty("query", out var query));
        Assert.False(query.TryGetProperty("fromWebFormsEvent", out _));
        Assert.False(query.TryGetProperty("classification", out _));
        Assert.False(query.TryGetProperty("includeLegacyRoots", out _));
    }

    [Fact]
    public async Task Paths_infer_single_index_language_from_scanner_version()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, Manifest("web", scannerVersion: "tracemap-typescript/0.1.0"), []);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "paths"),
            FromSource: "source",
            ToSurface: "sql-query"));

        Assert.Equal("typescript", Assert.Single(result.Report.Sources).Language);
    }

    [Fact]
    public async Task Paths_preserve_symbol_display_names_for_single_index_relationships()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var interfaceDisplay = "Legacy.Contracts.IOrderService";
        var implementationDisplay = "Legacy.Services.OrderService";
        SqliteIndexWriter.Write(index, manifest, [
            SymbolRelationshipFact(manifest, "symbol:contract", interfaceDisplay, "symbol:service", implementationDisplay),
            PackageConfigFact(manifest, implementationDisplay, "App.config", 8, "Legacy.Remoting")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "paths"),
            FromSymbol: interfaceDisplay,
            ToSurface: "package-config"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(interfaceDisplay, path.Nodes.First().DisplayName);
        Assert.Contains(path.Nodes, node => node.DisplayName == implementationDisplay);
        Assert.DoesNotContain(path.Nodes, node => node.DisplayName is "symbol:contract" or "symbol:service");
    }

    [Fact]
    public async Task Legacy_paths_compose_webforms_root_to_remoting_config_endpoint_with_static_cap()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var configure = "Legacy.Remoting.RemotingHost.Configure()";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, configure, "Pages/Orders.aspx.cs", 30, EvidenceTiers.Tier1Semantic),
            RemotingConfigServiceFact(manifest, configure, "App.config", 16, "Legacy.Remoting.RemoteService", objectUriHash: "abcdef1234567890")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "remoting-endpoint"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.ProbableStaticPath, path.Classification);
        Assert.Equal("remoting-endpoint", path.Nodes.Last().SurfaceKind);
        Assert.Equal("objectUri-abcdef12", path.Nodes.Last().SurfaceName);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "remoting-evidence" && edge.RuleId == RuleIds.LegacyFlowStaticTraversal);
        AssertNoStrongStaticPath(result.Report.Paths);
        Assert.Contains(path.Notes, note => note.Code == "StaticRemotingEvidence");
    }

    [Fact]
    public async Task Legacy_paths_remoting_activation_selector_uses_display_hash_and_redacts_raw_values()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var outDir = Path.Combine(temp.Path, "flows");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var activate = "Legacy.Remoting.ClientFactory.Create()";
        const string rawUrl = "tcp://customer-prod.example.test:9000/RemoteService.rem";
        const string urlHash = "0123abcd99999999";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, activate, "Pages/Orders.aspx.cs", 31, EvidenceTiers.Tier1Semantic),
            RemotingActivationFact(manifest, activate, "RemotingClient.cs", 9, "Legacy.Remoting.IRemoteService", urlHash)
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: outDir,
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "remoting-endpoint",
            SurfaceName: "url-0123abcd"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewStaticPath, path.Classification);
        Assert.Equal("url-0123abcd", path.Nodes.Last().SurfaceName);
        AssertNoStrongStaticPath(result.Report.Paths);

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "paths-report.json"));
        Assert.DoesNotContain(rawUrl, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(rawUrl, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":9000", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("channel opened", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remote object activated", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cross-process", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deployed endpoint", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("proves reachability", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("impacted remote service", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime endpoint", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("url-0123abcd", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_paths_remoting_object_shape_is_selected_review_tier_only()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var inspect = "Legacy.Remoting.RemoteServiceFactory.Inspect()";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, inspect, "Pages/Orders.aspx.cs", 31, EvidenceTiers.Tier1Semantic),
            RemotingMarshalByRefFact(manifest, inspect, "RemoteService.cs", 7, "Legacy.Remoting.RemoteService")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "remoting-object"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewStaticPath, path.Classification);
        Assert.Equal("remoting-object", path.Nodes.Last().SurfaceKind);
        Assert.Contains(path.Notes, note => note.Code == "RemotingObjectShapeOnly");
        AssertNoStrongStaticPath(result.Report.Paths);
    }

    [Fact]
    public async Task Legacy_paths_remoting_registration_uses_callsite_evidence_without_source_symbol()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Submit_Click";
        var configure = "Configure";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, configure, "Pages/Orders.aspx.cs", 30, EvidenceTiers.Tier3SyntaxOrTextual),
            CallFact(manifest, configure, "RegisterWellKnownServiceType", "RemotingHost.cs", 15, EvidenceTiers.Tier3SyntaxOrTextual),
            RemotingRegistrationFactWithoutSource(manifest, "RemotingHost.cs", 15, "Legacy.Remoting.RemoteService", objectUriHash: "abcdef1234567890")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "remoting-registration"));

        Assert.Contains(result.Report.Paths, candidate => candidate.Nodes.Last().NodeKind == "remoting-registration");
        var path = result.Report.Paths
            .Where(candidate => candidate.Nodes.Last().NodeKind == "remoting-registration")
            .OrderBy(candidate => candidate.Length)
            .First();
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewStaticPath, path.Classification);
        Assert.Equal("remoting-registration", path.Nodes.Last().SurfaceKind);
        Assert.Equal("remoting-registration", path.Nodes.Last().NodeKind);
        Assert.True(result.Report.Inventory.NodesByKind.ContainsKey("remoting-registration"));
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "remoting-evidence" && edge.RuleId == RuleIds.LegacyFlowStaticTraversal);
        AssertNoStrongStaticPath(result.Report.Paths);
    }

    [Fact]
    public async Task Legacy_paths_remoting_config_endpoint_uses_configure_callsite_when_config_matches()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Submit_Click";
        var configure = "Configure";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, configure, "Pages/Orders.aspx.cs", 30, EvidenceTiers.Tier3SyntaxOrTextual),
            CallFact(manifest, configure, "Configure", "RemotingHost.cs", 15, EvidenceTiers.Tier3SyntaxOrTextual),
            RemotingConfigureApiFactWithoutSource(manifest, "RemotingHost.cs", 15, "App.config"),
            RemotingConfigServiceFactWithoutSource(manifest, "App.config", 16, "Legacy.Remoting.RemoteService", objectUriHash: "abcdef1234567890")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "remoting-endpoint"));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewStaticPath, path.Classification);
        Assert.Equal("remoting-endpoint", path.Nodes.Last().SurfaceKind);
        Assert.Equal("objectUri-abcdef12", path.Nodes.Last().SurfaceName);
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "remoting-evidence" && edge.RuleId == RuleIds.LegacyFlowStaticTraversal);
        AssertNoStrongStaticPath(result.Report.Paths);
    }

    [Fact]
    public async Task Legacy_paths_remoting_client_and_service_matching_hashes_stay_separate()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var host = "Legacy.Remoting.RemotingHost.Configure()";
        var activate = "Legacy.Remoting.ClientFactory.Create()";
        const string sharedHash = "abcdef1234567890";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, host, "Pages/Orders.aspx.cs", 30, EvidenceTiers.Tier1Semantic),
            CallFact(manifest, handler, activate, "Pages/Orders.aspx.cs", 31, EvidenceTiers.Tier1Semantic),
            RemotingConfigServiceFact(manifest, host, "App.config", 16, "Legacy.Remoting.RemoteService", sharedHash),
            RemotingActivationFact(manifest, activate, "RemotingClient.cs", 9, "Legacy.Remoting.IRemoteService", sharedHash)
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "remoting-endpoint"));

        Assert.Equal(2, result.Report.Paths.Count);
        Assert.All(result.Report.Paths, path => Assert.Single(path.Nodes, node => node.SurfaceKind == "remoting-endpoint"));
        Assert.DoesNotContain(result.Report.Paths, path => path.Nodes.Count(node => node.SurfaceKind == "remoting-endpoint") > 1);
        AssertNoStrongStaticPath(result.Report.Paths);
    }

    [Fact]
    public async Task Legacy_paths_remoting_channel_supporting_ids_are_source_scoped_and_deterministic()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Admin.Start_Click(System.Object,System.EventArgs)";
        var configure = "Legacy.Remoting.RemotingHost.Configure()";
        var channel = RemotingChannelDeclaredFact(manifest, configure, "RemotingHost.cs", 14, "TcpChannel");
        var registration = RemotingChannelRegisteredFact(manifest, configure, "RemotingHost.cs", 15, " ch-unused ; " + channel.FactId + " ; " + channel.FactId + " ");
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Admin.aspx", "Start", "OnClick", "Start_Click", 12),
            WebFormsHandler(manifest, "Pages/Admin.aspx.cs", handler, "Start_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, configure, "Pages/Admin.aspx.cs", 34, EvidenceTiers.Tier1Semantic),
            channel,
            registration
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "remoting-channel",
            SurfaceName: channel.FactId));

        var path = Assert.Single(result.Report.Paths);
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewStaticPath, path.Classification);
        Assert.Equal("remoting-channel", path.Nodes.Last().SurfaceKind);
        Assert.Contains(path.SupportingFactIds, id => id.EndsWith(":" + channel.FactId, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "MalformedSupportingFactIds");
        AssertNoStrongStaticPath(result.Report.Paths);
    }

    [Fact]
    public async Task Legacy_paths_remoting_mixed_supporting_id_delimiters_emit_gap()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Admin.Start_Click(System.Object,System.EventArgs)";
        var configure = "Legacy.Remoting.RemotingHost.Configure()";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Admin.aspx", "Start", "OnClick", "Start_Click", 12),
            WebFormsHandler(manifest, "Pages/Admin.aspx.cs", handler, "Start_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, configure, "Pages/Admin.aspx.cs", 34, EvidenceTiers.Tier1Semantic),
            RemotingChannelRegisteredFact(manifest, configure, "RemotingHost.cs", 15, "alpha,beta;gamma")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "remoting-channel"));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "MalformedSupportingFactIds" && gap.RuleId == RuleIds.LegacyFlowGapPropagation);
        AssertNoStrongStaticPath(result.Report.Paths);
    }

    [Fact]
    public async Task Legacy_paths_remoting_neutralizes_private_combined_source_labels()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var configure = "Legacy.Remoting.RemotingHost.Configure()";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, configure, "Pages/Orders.aspx.cs", 30, EvidenceTiers.Tier1Semantic),
            RemotingConfigServiceFact(manifest, configure, "App.config", 16, "Legacy.Remoting.RemoteService", objectUriHash: "abcdef1234567890")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["internal-remoting-prod"]));

        await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: combined,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View,
            ToSurface: "remoting-endpoint"));

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "flows", "paths-report.json"));
        Assert.DoesNotContain("internal-remoting-prod", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"sourceLabel\": \"source:", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_paths_keep_wcf_and_remoting_terminals_separate()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("legacy-app");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        var clientCall = "Legacy.ServiceReference.OrderClient.SubmitOrder(System.Int32)";
        var remotingConfigure = "Legacy.Remoting.RemotingHost.Configure()";
        SqliteIndexWriter.Write(index, manifest, [
            WebFormsBinding(manifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(manifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            CallFact(manifest, handler, clientCall, "Pages/Orders.aspx.cs", 30, EvidenceTiers.Tier1Semantic),
            CallFact(manifest, handler, remotingConfigure, "Pages/Orders.aspx.cs", 31, EvidenceTiers.Tier1Semantic),
            WcfMappingFact(manifest, clientCall, "SubmitOrder", "Service References/Order/Reference.cs", 5),
            RemotingConfigServiceFact(manifest, remotingConfigure, "App.config", 16, "Legacy.Remoting.RemoteService", objectUriHash: "abcdef1234567890")
        ]);

        var result = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: index,
            OutputPath: Path.Combine(temp.Path, "flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View));

        Assert.Contains(result.Report.Paths, path => path.Nodes.Last().SurfaceKind == "wcf-operation");
        Assert.Contains(result.Report.Paths, path => path.Nodes.Last().SurfaceKind == "remoting-endpoint");
        Assert.All(result.Report.Paths.Where(path => path.Nodes.Last().SurfaceKind is "wcf-operation" or "remoting-endpoint"),
            path => Assert.StartsWith(path.Nodes.Last().SurfaceKind == "wcf-operation" ? "legacy.wcf." : "legacy.remoting.", path.Nodes.Last().RuleId, StringComparison.Ordinal));
        AssertNoStrongStaticPath(result.Report.Paths.Where(path => path.Nodes.Last().SurfaceKind == "remoting-endpoint"));
    }

    [Fact]
    public async Task Legacy_paths_remoting_availability_and_gap_propagation_are_explicit()
    {
        using var temp = new TempDirectory();
        var oldIndex = Path.Combine(temp.Path, "old.sqlite");
        var gapIndex = Path.Combine(temp.Path, "gap.sqlite");
        var currentIndex = Path.Combine(temp.Path, "current.sqlite");
        var futureIndex = Path.Combine(temp.Path, "future.sqlite");
        var oldManifest = Manifest("legacy-app", scannerVersion: "tracemap-milestone15");
        var gapManifest = Manifest("legacy-app");
        var currentManifest = Manifest("legacy-app");
        var futureManifest = Manifest("legacy-app", scannerVersion: "tracemap-milestone17-dev");
        var handler = "Legacy.Pages.Orders.Submit_Click(System.Object,System.EventArgs)";
        SqliteIndexWriter.Write(currentIndex, currentManifest, [
            WebFormsBinding(currentManifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(currentManifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding")
        ]);
        SqliteIndexWriter.Write(oldIndex, oldManifest, [
            WebFormsBinding(oldManifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(oldManifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding")
        ]);
        SqliteIndexWriter.Write(gapIndex, gapManifest, [
            WebFormsBinding(gapManifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(gapManifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding"),
            AnalysisGapFact(gapManifest, RuleIds.LegacyRemotingConfig, "ExternalConfigInclude", "App.config", 8)
        ]);
        SqliteIndexWriter.Write(futureIndex, futureManifest, [
            WebFormsBinding(futureManifest, "Pages/Orders.aspx", "Submit", "OnClick", "Submit_Click", 12),
            WebFormsHandler(futureManifest, "Pages/Orders.aspx.cs", handler, "Submit_Click", 24, EvidenceTiers.Tier1Semantic, "wf-binding")
        ]);

        var oldResult = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: oldIndex,
            OutputPath: Path.Combine(temp.Path, "old-flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View));
        var currentResult = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: currentIndex,
            OutputPath: Path.Combine(temp.Path, "current-flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View));
        var gapResult = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: gapIndex,
            OutputPath: Path.Combine(temp.Path, "gap-flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View));
        var futureResult = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            IndexPath: futureIndex,
            OutputPath: Path.Combine(temp.Path, "future-flows"),
            IncludeLegacyRoots: true,
            View: LegacyFlowReportConstants.View));

        var schemaGap = Assert.Single(oldResult.Report.Gaps, gap => gap.GapKind == "SchemaMissing" && gap.Reason == "legacy-remoting");
        Assert.Equal("abc123", schemaGap.CommitSha);
        Assert.Equal("tracemap-milestone15", schemaGap.ExtractorVersion);
        Assert.Equal("source-manifest", schemaGap.EvidenceScope);

        var noEvidenceGap = Assert.Single(currentResult.Report.Gaps, gap => gap.GapKind == "NoRemotingEvidenceFound" && gap.Classification == CombinedDependencyPathClassifications.NoBackendEvidence);
        Assert.Equal("abc123", noEvidenceGap.CommitSha);
        Assert.Equal(ScannerVersions.TraceMap, noEvidenceGap.ExtractorVersion);
        Assert.Equal("source-manifest", noEvidenceGap.EvidenceScope);

        var currentMarkdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "current-flows", "paths-report.md"));
        Assert.Contains("No Remoting evidence found under available Remoting extractor coverage", currentMarkdown, StringComparison.Ordinal);
        Assert.Contains("commit:abc123", currentMarkdown, StringComparison.Ordinal);
        Assert.Contains("scope:source-manifest", currentMarkdown, StringComparison.Ordinal);
        Assert.Contains(futureResult.Report.Gaps, gap => gap.GapKind == "NoRemotingEvidenceFound" && gap.ExtractorVersion == "tracemap-milestone17-dev");
        Assert.DoesNotContain(futureResult.Report.Gaps, gap => gap.GapKind == "SchemaMissing" && gap.Reason == "legacy-remoting");
        Assert.Contains(gapResult.Report.Gaps, gap => gap.GapKind == "ExternalConfigInclude" && gap.RuleId == RuleIds.LegacyFlowGapPropagation);
    }

    private static void AssertNoStrongStaticPath(IEnumerable<CombinedPath> paths)
    {
        Assert.DoesNotContain(paths, candidate => candidate.Classification == CombinedDependencyPathClassifications.StrongStaticPath);
    }

    private static ScanManifest Manifest(string repo, string analysisLevel = "Level1SemanticAnalysis", string buildStatus = "Succeeded", string scannerVersion = ScannerVersions.TraceMap)
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

    private static CodeFact WebFormsBinding(ScanManifest manifest, string file, string controlId, string eventName, string handlerName, int line, string tier = EvidenceTiers.Tier2Structural)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsEventBindingDeclared,
            RuleIds.LegacyWebFormsEventBinding,
            tier,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: handlerName,
            contractElement: handlerName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["controlId"] = controlId,
                ["controlType"] = "asp:Button",
                ["eventName"] = eventName,
                ["handlerName"] = handlerName,
                ["pageTypeName"] = "Legacy.Pages.Orders"
            });
    }

    private static CodeFact WebFormsHandler(ScanManifest manifest, string file, string handlerSymbol, string handlerName, int line, string tier, string bindingFactId)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsHandlerResolved,
            RuleIds.LegacyWebFormsHandlerResolution,
            tier,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: "Legacy.Pages.Orders",
            targetSymbol: handlerSymbol,
            contractElement: handlerName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["controlId"] = handlerName.Split('_')[0],
                ["eventName"] = "OnClick",
                ["handlerName"] = handlerName,
                ["handlerSymbol"] = handlerSymbol,
                ["markupFile"] = file.Replace(".cs", string.Empty, StringComparison.Ordinal),
                ["pageTypeName"] = "Legacy.Pages.Orders",
                ["supportingFactIds"] = bindingFactId
            });
    }

    private static CodeFact CallFact(ScanManifest manifest, string caller, string callee, string file, int line, string tier)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.CallEdge,
            tier == EvidenceTiers.Tier1Semantic ? RuleIds.CSharpSemanticCallGraph : RuleIds.CSharpSyntaxCallGraph,
            tier,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: caller,
            targetSymbol: callee,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["callKind"] = "method"
            });
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string sourceSymbol, string file, int line, string tableName)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: "query-shape",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationName"] = "SELECT",
                ["tableName"] = tableName,
                ["columnNames"] = "id;state",
                ["sqlSourceKind"] = "literal-string",
                ["queryShapeHash"] = FactFactory.Hash($"select:{tableName}", 32)
            });
    }

    private static CodeFact PackageConfigFact(ScanManifest manifest, string sourceSymbol, string file, int line, string packageName)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.PackageReferenced,
            RuleIds.ProjectFile,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: packageName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["dependencyGroup"] = "PackageReference",
                ["dependencyScope"] = "runtime",
                ["ecosystem"] = "nuget",
                ["manifestKind"] = "csproj",
                ["packageName"] = packageName,
                ["packageManager"] = "nuget",
                ["surfaceKind"] = "package-config",
                ["version"] = "1.0.0"
            });
    }

    private static CodeFact SymbolRelationshipFact(
        ScanManifest manifest,
        string sourceSymbolId,
        string sourceDisplayName,
        string targetSymbolId,
        string targetDisplayName)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.SymbolRelationship,
            RuleIds.CSharpSemanticSymbolRelationship,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan("Contracts/IOrderService.cs", 3, 3, null, "test", "test/1.0"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["relationshipKind"] = "ImplementedBy",
                ["sourceSymbolDisplayName"] = sourceDisplayName,
                ["sourceSymbolId"] = sourceSymbolId,
                ["sourceSymbolKind"] = "Interface",
                ["sourceSymbolLanguage"] = "csharp",
                ["targetSymbolDisplayName"] = targetDisplayName,
                ["targetSymbolId"] = targetSymbolId,
                ["targetSymbolKind"] = "Class",
                ["targetSymbolLanguage"] = "csharp"
            });
    }

    private static CodeFact ProjectionFact(ScanManifest manifest, string handlerSymbol, string handlerName, string surfaceKind, string surfaceDisplayHash, IReadOnlyList<string> supportingFactIds)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsEventFlowProjected,
            RuleIds.LegacyWebFormsEventFlow,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("Pages/Orders.aspx.cs", 30, 30, null, "test", "test/1.0"),
            sourceSymbol: handlerSymbol,
            targetSymbol: "query-shape",
            contractElement: handlerName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["controlId"] = handlerName.Split('_')[0],
                ["eventName"] = "OnClick",
                ["flowClassification"] = "ProbableStaticEventFlow",
                ["handlerName"] = handlerName,
                ["handlerSymbolId"] = handlerSymbol,
                ["supportingFactIds"] = string.Join(",", supportingFactIds.OrderBy(value => value, StringComparer.Ordinal)),
                ["terminalSurfaceKind"] = surfaceKind,
                ["terminalSurfaceNameHash"] = surfaceDisplayHash
            });
    }

    private static string SqlShapeDisplayHash(string tableName)
    {
        return FactFactory.Hash($"shape:{FactFactory.Hash($"select:{tableName}", 32)}", 32);
    }

    private static CodeFact WcfMappingFact(ScanManifest manifest, string clientCall, string operation, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.WcfServiceReferenceMapping,
            RuleIds.LegacyWcfMapping,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: clientCall,
            targetSymbol: $"IOrderService.{operation}",
            contractElement: operation,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["normalizedOperationName"] = operation,
                ["mappingKind"] = "GeneratedClientToMetadataOperation",
                ["supportingFactIds"] = "wcf-client,wcf-operation"
            });
    }

    private static CodeFact RemotingConfigServiceFact(ScanManifest manifest, string sourceSymbol, string file, int line, string typeName, string objectUriHash)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingConfigServiceDeclared,
            RuleIds.LegacyRemotingConfig,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", ScannerVersions.LegacyRemotingExtractor),
            sourceSymbol: sourceSymbol,
            targetSymbol: typeName,
            contractElement: "well-known-service",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverage"] = "config-structural",
                ["limitation"] = "Static Remoting config service evidence only; runtime hosting, activation, reachability, deployment, and production usage are not proven.",
                ["objectUriHash"] = objectUriHash,
                ["registrationKind"] = "well-known-service",
                ["sourceKind"] = "config",
                ["typeName"] = typeName
            });
    }

    private static CodeFact RemotingConfigServiceFactWithoutSource(ScanManifest manifest, string file, int line, string typeName, string objectUriHash)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingConfigServiceDeclared,
            RuleIds.LegacyRemotingConfig,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", ScannerVersions.LegacyRemotingExtractor),
            targetSymbol: typeName,
            contractElement: "well-known-service",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["configKind"] = "service",
                ["coverage"] = "static-xml-config",
                ["limitation"] = "Checked-in XML config evidence only; values are hashed or omitted and runtime config selection, reachability, deployment, and production usage are not proven.",
                ["objectUriHash"] = objectUriHash,
                ["registrationKind"] = "well-known-service",
                ["sourceFormat"] = "xml-config",
                ["typeName"] = typeName
            });
    }

    private static CodeFact RemotingRegistrationFactWithoutSource(ScanManifest manifest, string file, int line, string targetTypeName, string objectUriHash)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingServiceTypeRegistered,
            RuleIds.LegacyRemotingRegistration,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", ScannerVersions.LegacyRemotingExtractor),
            targetSymbol: targetTypeName,
            contractElement: "well-known-service",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverage"] = "syntax-fallback",
                ["limitation"] = "Static registration call evidence only; dynamic arguments, runtime configuration, deployment, reachability, and production usage are not proven.",
                ["objectUriHash"] = objectUriHash,
                ["registrationKind"] = "well-known-service",
                ["sourceKind"] = "syntax",
                ["targetTypeName"] = targetTypeName
            });
    }

    private static CodeFact RemotingConfigureApiFactWithoutSource(ScanManifest manifest, string file, int line, string configFileName)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingApiUsageDeclared,
            RuleIds.LegacyRemotingRegistration,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", ScannerVersions.LegacyRemotingExtractor),
            targetSymbol: "RemotingConfiguration.Configure",
            contractElement: "Configure",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["configFileName"] = configFileName,
                ["coverage"] = "syntax-fallback",
                ["limitation"] = "Static registration call evidence only; dynamic arguments, runtime configuration, deployment, reachability, and production usage are not proven.",
                ["registrationKind"] = "configure",
                ["sourceKind"] = "syntax"
            });
    }

    private static CodeFact RemotingActivationFact(ScanManifest manifest, string sourceSymbol, string file, int line, string targetTypeName, string urlHash)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingClientActivationDeclared,
            RuleIds.LegacyRemotingRegistration,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", ScannerVersions.LegacyRemotingExtractor),
            sourceSymbol: sourceSymbol,
            targetSymbol: targetTypeName,
            contractElement: "Activator.GetObject",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverage"] = "syntax-fallback-remoting-context",
                ["limitation"] = "Static client activation evidence only; URL values are hashed and runtime reachability is not proven.",
                ["registrationKind"] = "client-activation",
                ["sourceKind"] = "syntax",
                ["targetTypeName"] = targetTypeName,
                ["urlHash"] = urlHash
            });
    }

    private static CodeFact RemotingChannelDeclaredFact(ScanManifest manifest, string sourceSymbol, string file, int line, string channelTypeName)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingChannelDeclared,
            RuleIds.LegacyRemotingChannel,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", ScannerVersions.LegacyRemotingExtractor),
            sourceSymbol: sourceSymbol,
            targetSymbol: channelTypeName,
            contractElement: channelTypeName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["channelDirection"] = "server",
                ["channelKind"] = "tcp",
                ["channelTypeName"] = channelTypeName,
                ["coverage"] = "syntax-fallback",
                ["limitation"] = "Static channel construction evidence only; runtime channel setup is not proven.",
                ["sourceKind"] = "syntax"
            });
    }

    private static CodeFact RemotingChannelRegisteredFact(ScanManifest manifest, string sourceSymbol, string file, int line, string supportingFactIds)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingChannelRegistered,
            RuleIds.LegacyRemotingChannel,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", ScannerVersions.LegacyRemotingExtractor),
            sourceSymbol: sourceSymbol,
            targetSymbol: "ChannelServices.RegisterChannel",
            contractElement: "RegisterChannel",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverage"] = "syntax-fallback",
                ["limitation"] = "Static channel registration evidence only; runtime registration is not proven.",
                ["linkKind"] = "same-method-single-local",
                ["registrationCall"] = "True",
                ["sourceKind"] = "syntax",
                ["supportingFactIds"] = supportingFactIds
            });
    }

    private static CodeFact RemotingMarshalByRefFact(ScanManifest manifest, string sourceSymbol, string file, int line, string typeName)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingMarshalByRefObjectDeclared,
            RuleIds.LegacyRemotingMarshalByRef,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", ScannerVersions.LegacyRemotingExtractor),
            sourceSymbol: sourceSymbol,
            targetSymbol: typeName,
            contractElement: typeName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverage"] = "semantic",
                ["limitation"] = "Inheritance is Remoting-capable object shape only; it does not prove hosting, activation, reachability, deployment, or production usage.",
                ["sourceKind"] = "semantic",
                ["typeName"] = typeName
            });
    }

    private static CodeFact AnalysisGapFact(ScanManifest manifest, string ruleId, string classification, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(file, line, line, null, "test", ScannerVersions.LegacyRemotingExtractor),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = classification,
                ["messageHash"] = FactFactory.Hash(classification, 32)
            });
    }

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
}
