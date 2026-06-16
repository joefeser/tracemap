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

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
}
