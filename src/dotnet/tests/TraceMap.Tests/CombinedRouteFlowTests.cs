using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CombinedRouteFlowTests
{
    [Fact]
    public async Task Route_flow_writes_route_centered_markdown_and_json_without_mutating_combined_index()
    {
        using var temp = new TempDirectory();
        var (combinedPath, controller, _) = await CreateRouteFlowCombinedIndexAsync(temp);
        var outDir = Path.Combine(temp.Path, "route-flow");
        var before = await CombinedIndexFingerprintAsync(combinedPath);

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            outDir,
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Equal(before, await CombinedIndexFingerprintAsync(combinedPath));
        Assert.True(File.Exists(Path.Combine(outDir, "route-flow-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "route-flow-report.json")));
        Assert.Equal("route-flow", result.Report.ReportType);
        Assert.Equal(result.Report.ReportCoverage, result.Report.Summary.ReportCoverage);
        Assert.NotNull(result.Report.Query.SelectorTrace);
        Assert.Equal("route", result.Report.Query.SelectorTrace!.SelectorKind);
        Assert.Equal("GET /api/orders/{}", result.Report.Query.SelectorTrace.SafeNormalizedKey);
        Assert.Equal("NormalizedMethodPath", result.Report.Query.SelectorTrace.MatchMode);
        Assert.Equal("normalized", result.Report.Query.SelectorTrace.RedactionState);
        Assert.Equal("combined.route-flow.selector.v1", result.Report.Query.SelectorTrace.RuleId);
        Assert.NotEmpty(result.Report.Query.SelectorTrace.SupportingFactIds);
        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root");
        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "aligned-route-pair");
        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root"
            && row.BridgeState == "method-symbol"
            && row.Evidence.RuleId == "combined.route-flow.entry.v1");
        Assert.DoesNotContain(result.Report.FlowRows, row => row.EdgeKind == "client-server-alignment");
        Assert.Contains(result.Report.FlowRows, row => row.RowKind == "endpoint-method-bridge" && row.EdgeKind == "route-bound-to-symbol");
        Assert.Contains(result.Report.FlowRows, row => row.EdgeKind == "direct-call" && row.SourceSymbol.Contains(controller, StringComparison.Ordinal));
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query");
        Assert.Contains(result.Report.LogicRows, row => row.LogicKind == "query-filter-sort-selection");
        Assert.Contains(result.Report.LogicRows, row => row.LogicKind == "argument-flow" && row.Evidence.RuleId == "combined.route-flow.argument-projection.v1");
        Assert.Contains(result.Report.LogicRows, row => row.LogicKind == "query-shape" && row.Evidence.RuleId == "combined.route-flow.fact-symbol-projection.v1");
        Assert.Contains(result.Report.TouchedFiles, row => row.FilePath == "Controllers/OrdersController.cs"
            && row.FirstStartLine == 10
            && row.LastEndLine == 14
            && row.Evidence.RuleId == "combined.route-flow.report.v1"
            && row.RuleIds.Contains("combined.route-flow.entry.v1")
            && row.RuleIds.Contains("combined.route-flow.path.v1")
            && row.SupportingRowIds.Any(id => id.StartsWith("entry:", StringComparison.Ordinal))
            && row.SupportingRowIds.Any(id => id.StartsWith("row:", StringComparison.Ordinal)));
        Assert.Contains(result.Report.TouchedFiles, row => row.FilePath == "Infrastructure/OrderRepository.cs"
            && row.RuleIds.Contains("combined.route-flow.dependency-surface.v1")
            && row.RuleIds.Contains("combined.route-flow.fact-symbol-projection.v1"));
        Assert.Contains(result.Report.TouchedSymbols, row => row.DisplayName.Contains(controller, StringComparison.Ordinal)
            && row.FilePath == "Controllers/OrdersController.cs"
            && !row.SymbolId.StartsWith("touched-symbol:", StringComparison.Ordinal)
            && row.Evidence.RuleId == "combined.route-flow.report.v1"
            && row.RuleIds.Contains("combined.route-flow.path.v1")
            && row.SupportingRowIds.Any(id => id.StartsWith("row:", StringComparison.Ordinal)));
        Assert.Contains(result.Report.TouchedSymbols, row => row.DisplayName == "GET /api/orders/{}"
            && row.FilePath == "Controllers/OrdersController.cs"
            && row.RuleIds.Contains("combined.route-flow.entry.v1"));
        Assert.Contains(result.Report.TouchedSymbols, row => row.DisplayName.Contains("parameter:", StringComparison.Ordinal)
            && row.SymbolKind == "argument-flow");
        Assert.NotNull(result.Report.ContextGroups);
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "service"
            && group.DisplayName.Contains("IOrderService", StringComparison.Ordinal)
            && group.MatchKind == "direct-call"
            && group.RuleIds.Contains("combined.route-flow.path.v1")
            && group.Evidence.RuleId == "combined.route-flow.report.v1"
            && group.SupportingRowIds.Any(id => id.StartsWith("row:", StringComparison.Ordinal)));
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "repository"
            && group.DisplayName.Contains("OrderRepository", StringComparison.Ordinal)
            && group.MatchKind == "direct-call");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "query"
            && group.MatchKind == "dependency-surface"
            && group.ValueSafety == "hashed"
            && group.RuleIds.Contains("combined.route-flow.dependency-surface.v1")
            && group.SafeMetadata.ContainsKey("surfaceKind")
            && group.SafeMetadata.ContainsKey("tableNameHash"));
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "value-origin"
            && group.MatchKind == "argument-flow"
            && group.ValueSafety == "hashed"
            && group.RuleIds.Contains("combined.route-flow.argument-projection.v1")
            && group.RuleIds.Contains("combined.route-flow.redaction.v1"));
        Assert.All(result.Report.ContextGroups!, group =>
        {
            Assert.NotEmpty(group.SupportingRowIds);
            Assert.NotEmpty(group.RuleIds);
            Assert.NotEmpty(group.EvidenceTiers);
            Assert.Equal("combined.route-flow.report.v1", group.Evidence.RuleId);
            Assert.Contains(group.ValueSafety, new[] { "safe", "hashed", "omitted" });
            Assert.Contains(group.GroupKind, new[] { "method", "service", "interface-candidate", "repository", "query", "data-surface", "dependency", "legacy-data", "value-origin", "gap" });
        });
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "ExtractorUnavailable");
        Assert.Contains(result.Report.Snapshot.Sources, source => source.ScannerVersion == "tracemap-milestone15");
        Assert.All(result.Report.FlowRows, row =>
        {
            Assert.StartsWith("combined.route-flow.", row.Evidence.RuleId, StringComparison.Ordinal);
            Assert.NotEmpty(row.Evidence.SupportingRuleIds);
            Assert.False(string.IsNullOrWhiteSpace(row.Evidence.CommitSha));
            Assert.False(string.IsNullOrWhiteSpace(row.Evidence.ExtractorVersion));
        });
        Assert.DoesNotContain(result.Report.DependencySurfaces, surface => surface.StableKey.Contains("fact-", StringComparison.OrdinalIgnoreCase));
        Assert.All(result.Report.DependencySurfaces, surface =>
        {
            Assert.StartsWith("surface-key-hash:", surface.StableKey, StringComparison.Ordinal);
            Assert.DoesNotContain("orders", surface.StableKey, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "FactSymbolUnsupportedTypeSkipped");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "FactSymbolProjectionUnavailable");

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "route-flow-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "route-flow-report.json"));
        Assert.Contains("TraceMap Route Flow Report", markdown);
        Assert.Contains("static route-flow evidence", markdown);
        Assert.Contains("candidate implementation", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## Touched Files", markdown);
        Assert.Contains("## Touched Symbols", markdown);
        Assert.Contains("## Context Groups", markdown);
        Assert.Contains("| Kind | Name | Match | Value safety | Classification | Coverage | Supporting rows | Evidence |", markdown);
        Assert.Contains("| Kind | Method | Path key | Bridge | Classification | Evidence |", markdown);
        Assert.Contains("method-symbol", markdown);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select * from", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=private", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=secret", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=private", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);
        AssertForbiddenRuntimeWording(markdown);
        AssertForbiddenRuntimeWording(json);

        var parsed = JsonSerializer.Deserialize<RouteFlowReport>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(parsed);
        var parsedJson = JsonNode.Parse(json);
        var gapNodes = parsedJson?["gaps"]?.AsArray();
        Assert.NotNull(gapNodes);
        Assert.NotEmpty(gapNodes!);
        Assert.All(gapNodes!, node =>
        {
            Assert.False(string.IsNullOrWhiteSpace(node?["ruleId"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(node?["evidenceTier"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(node?["classification"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(node?["coverage"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(node?["message"]?.GetValue<string>()));
            Assert.True(node?["limitations"]?.AsArray().Count > 0);
        });
        Assert.All(parsed!.Gaps, gap =>
        {
            Assert.True(gap.Classification is RouteFlowClassifications.NeedsReviewStaticRouteFlow
                or RouteFlowClassifications.NoRouteFlowEvidence
                or RouteFlowClassifications.UnknownAnalysisGap);
            Assert.StartsWith("combined.route-flow.", gap.RuleId, StringComparison.Ordinal);
            Assert.NotEmpty(gap.EvidenceTier);
            Assert.NotEmpty(gap.Coverage);
            Assert.NotEmpty(gap.Message);
            Assert.NotEmpty(gap.Limitations);
        });
        Assert.Contains(parsed!.EntryEvidence, row => row.EntryKind == "route-root"
            && row.BridgeState == "method-symbol"
            && row.Evidence.RuleId == "combined.route-flow.entry.v1");
        Assert.NotNull(parsed.ContextGroups);
        Assert.Contains(parsed.ContextGroups!, group => group.GroupKind == "query"
            && group.MatchKind == "dependency-surface"
            && group.ValueSafety == "hashed");
        Assert.DoesNotContain("select * from", JsonSerializer.Serialize(parsed.ContextGroups), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(parsed.TouchedFiles, row => row.Evidence.SupportingRuleIds.Contains("combined.route-flow.report.v1"));
        Assert.Contains(parsed.TouchedSymbols, row => row.Evidence.SupportingRuleIds.Contains("combined.route-flow.report.v1"));
        Assert.All(parsed.TouchedFiles, row =>
        {
            Assert.NotEmpty(row.SupportingRowIds);
            Assert.NotEmpty(row.RuleIds);
            Assert.NotEmpty(row.EvidenceTiers);
            Assert.Equal(row.FilePath, row.Evidence.FilePath);
        });
        Assert.All(parsed.TouchedSymbols, row =>
        {
            Assert.NotEmpty(row.SupportingRowIds);
            Assert.NotEmpty(row.RuleIds);
            Assert.NotEmpty(row.EvidenceTiers);
            Assert.False(string.IsNullOrWhiteSpace(row.CommitSha));
            Assert.False(string.IsNullOrWhiteSpace(row.DisplayName));
        });
        var sqlSurface = parsed.DependencySurfaces.Single(surface => surface.SurfaceKind == "sql-query");
        Assert.NotEmpty(sqlSurface.Evidence.SupportingFactIds);
        Assert.Contains(RuleIds.CSharpSyntaxQueryPattern, sqlSurface.Evidence.SupportingRuleIds);
        Assert.Equal("tracemap-milestone15", sqlSurface.Evidence.ExtractorVersion);
        Assert.Contains("tableNameHash", sqlSurface.SafeMetadata.Keys);
        Assert.Contains("columnNamesHash", sqlSurface.SafeMetadata.Keys);
        Assert.DoesNotContain("tableName", sqlSurface.SafeMetadata.Keys);
        Assert.DoesNotContain("columnNames", sqlSurface.SafeMetadata.Keys);
        Assert.DoesNotContain("orders", JsonSerializer.Serialize(sqlSurface.SafeMetadata), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id;status", JsonSerializer.Serialize(sqlSurface.SafeMetadata), StringComparison.OrdinalIgnoreCase);
        var argumentProjection = parsed.LogicRows.Single(row => row.Evidence.RuleId == "combined.route-flow.argument-projection.v1");
        Assert.Equal("argument-projection", argumentProjection.AttachmentKind);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, argumentProjection.Classification);
        Assert.Contains("parameterName", argumentProjection.SafeMetadata.Keys);
        Assert.StartsWith("parameter-name-hash:", argumentProjection.SafeMetadata["parameterName"], StringComparison.Ordinal);
        Assert.DoesNotContain("apiSecret", JsonSerializer.Serialize(argumentProjection.SafeMetadata), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("argumentSymbol", argumentProjection.SafeMetadata.Keys);
        Assert.Contains(argumentProjection.Evidence.SupportingRuleIds, rule => rule == RuleIds.CSharpSemanticValueFlow);
        Assert.Contains(argumentProjection.Evidence.SupportingRuleIds, rule => rule == "combined.route-flow.redaction.v1");
        var factSymbolProjection = parsed.LogicRows.Single(row => row.Evidence.RuleId == "combined.route-flow.fact-symbol-projection.v1"
            && row.LogicKind == "query-shape");
        Assert.Equal("fact-symbol-projection", factSymbolProjection.AttachmentKind);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, factSymbolProjection.Classification);
        Assert.Contains(factSymbolProjection.Evidence.SupportingRuleIds, rule => rule == RuleIds.CSharpSyntaxQueryPattern);
        Assert.Contains(factSymbolProjection.Evidence.SupportingRuleIds, rule => rule == "combined.route-flow.redaction.v1");
        Assert.Contains("tableNameHash", factSymbolProjection.SafeMetadata.Keys);
        Assert.Contains("evidenceKind", factSymbolProjection.SafeMetadata.Keys);
        Assert.Contains(factSymbolProjection.SafeMetadata["evidenceKind"], new[] { "object-shape", "query-shape", "data-surface", "dependency-surface", "fact-symbol-attachment" });
        Assert.DoesNotContain("factType", factSymbolProjection.SafeMetadata.Keys);
        Assert.DoesNotContain("orders", JsonSerializer.Serialize(factSymbolProjection.SafeMetadata), StringComparison.OrdinalIgnoreCase);

        var secondOutDir = outDir;
        await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            secondOutDir,
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "route-flow-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "route-flow-report.json")));
    }

    [Fact]
    public async Task Route_flow_attaches_legacy_data_storage_surfaces_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "route-flow");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Read(System.Int32)";
        var generatedModel = "Server.LegacyModels.GeneratedOrderRow";
        var unrelatedGeneratedModel = "Server.LegacyModels.GeneratedAuditRow";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            CallFact(server, repository, generatedModel, "Infrastructure/OrderRepository.cs", 18, targetSymbolKind: "NamedType"),
            LegacyDataEntityFact(server, null, "CustomerLedger", "Models/Store.dbml", 21, targetSymbol: generatedModel),
            LegacyDataStorageObjectFact(server, null, "CustomerLedgerTable", "Models/Store.dbml", 26, targetSymbol: generatedModel),
            LegacyDataStorageObjectFact(server, null, "AuditTrailTable", "Models/Audit.dbml", 31, "9f83c6a1d047e2b4", "ldm:route-flow-audit-storage-key", targetSymbol: unrelatedGeneratedModel)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            outDir,
            Route: "GET /api/orders/{id}",
            ToSurface: "legacy-data"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "legacy-data"));

        Assert.Equal(2, result.Report.DependencySurfaces.Count);
        Assert.All(result.Report.DependencySurfaces, surface =>
        {
            Assert.Equal("legacy-data", surface.SurfaceKind);
            Assert.Equal("data-model", surface.SurfaceSubtype);
            Assert.Equal("data-model", surface.SafeMetadata["surfaceSubtype"]);
            Assert.Equal("combined.route-flow.dependency-surface.v1", surface.Evidence.RuleId);
            Assert.Contains(RuleIds.LegacyDataDbml, surface.Evidence.SupportingRuleIds);
            Assert.StartsWith("surface-key-hash:", surface.StableKey, StringComparison.Ordinal);
        });
        Assert.Equal(
            result.Report.DependencySurfaces.Select(surface => surface.StableKey).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            repeated.Report.DependencySurfaces.Select(surface => surface.StableKey).OrderBy(value => value, StringComparer.Ordinal).ToArray());

        var terminalRows = result.Report.FlowRows.Where(row => row.RowKind == "terminal-surface").ToArray();
        Assert.Equal(2, terminalRows.Length);
        Assert.All(terminalRows, row => Assert.Contains(generatedModel, row.SourceSymbol, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Report.FlowRows, row => row.SourceSymbol.Contains("AuditTrail", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Report.DependencySurfaces, surface => surface.Evidence.FilePath == "Models/Audit.dbml");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "DataSurfaceAttachmentMissing" or "NoRouteFlowEvidence");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "legacy-data"
            && group.MatchKind == "dependency-surface"
            && group.SupportingRowIds.Any(rowId => result.Report.DependencySurfaces.Any(surface => surface.SurfaceId == rowId)));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "route-flow-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "route-flow-report.json"));
        Assert.Contains("| legacy-data | data-model |", markdown);
        Assert.Contains("\"surfaceSubtype\": \"data-model\"", json);
        Assert.DoesNotContain("CustomerLedger", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerLedger", json, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerLedgerTable", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerLedgerTable", json, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditTrailTable", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditTrailTable", json, StringComparison.Ordinal);
        Assert.DoesNotContain("customer-ledger", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customer-ledger", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audit-trail", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audit-trail", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_legacy_data_storage_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Read(System.Int32)";
        var unrelatedGeneratedModel = "Server.LegacyModels.GeneratedAuditRow";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            LegacyDataStorageObjectFact(server, null, "AuditTrailTable", "Models/Audit.dbml", 31, "9f83c6a1d047e2b4", "ldm:route-flow-audit-storage-key", targetSymbol: unrelatedGeneratedModel)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "legacy-data"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "legacy-data"));

        Assert.Empty(result.Report.DependencySurfaces);
        Assert.DoesNotContain(result.Report.FlowRows, row => row.RowKind == "terminal-surface");
        Assert.DoesNotContain(result.Report.FlowRows, row => row.SourceSymbol.Contains("AuditTrail", StringComparison.Ordinal));
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "DataSurfaceAttachmentMissing").GapId);
        Assert.Equal("server", gap.SourceLabel);
        Assert.Equal("Controllers/OrdersController.cs", gap.FilePath);
        Assert.Contains("no matching terminal dependency/data surface", gap.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_attaches_selected_sql_surface_with_path_context_and_stable_ids()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var unrelatedRepository = "Server.AuditRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true, tableName: "orders", columnNames: "id;status", queryShapeHash: "orders-shape"),
            QueryPatternFact(server, unrelatedRepository, "Infrastructure/AuditRepository.cs", 41, attachSymbol: true, tableName: "audit_events", columnNames: "id;actor", queryShapeHash: "audit-shape")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var surface = Assert.Single(result.Report.DependencySurfaces);
        var repeatedSurface = Assert.Single(repeated.Report.DependencySurfaces);
        Assert.Equal("sql-query", surface.SurfaceKind);
        Assert.Equal(surface.SurfaceId, repeatedSurface.SurfaceId);
        Assert.Equal(surface.StableKey, repeatedSurface.StableKey);
        Assert.StartsWith("surface-key-hash:", surface.StableKey, StringComparison.Ordinal);
        Assert.Equal("combined.route-flow.dependency-surface.v1", surface.Evidence.RuleId);
        Assert.Contains(RuleIds.CSharpSyntaxQueryPattern, surface.Evidence.SupportingRuleIds);
        Assert.Equal("orders-shape", surface.SafeMetadata["shapeHash"]);
        Assert.Equal(CombinedReportHelpers.Hash("orders", 16), surface.SafeMetadata["tableNameHash"]);
        Assert.DoesNotContain("audit-shape", JsonSerializer.Serialize(result.Report.DependencySurfaces), StringComparison.OrdinalIgnoreCase);

        var terminal = Assert.Single(result.Report.FlowRows, row => row.RowKind == "terminal-surface");
        var logic = Assert.Single(result.Report.LogicRows, row => row.LogicKind == "query-filter-sort-selection");
        Assert.Equal("path-context", logic.AttachmentKind);
        Assert.Equal(terminal.RowId, logic.AttachedFlowRowId);
        Assert.Equal("combined.route-flow.logic-surface.v1", logic.Evidence.RuleId);
        Assert.Contains(RuleIds.CSharpSyntaxQueryPattern, logic.Evidence.SupportingRuleIds);
        Assert.DoesNotContain(result.Report.LogicRows, row => row.SafeMetadata.TryGetValue("shapeHash", out var shapeHash) && shapeHash == "audit-shape");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "DataSurfaceAttachmentMissing" or "NoRouteFlowEvidence");
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_sql_surface_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var unrelatedRepository = "Server.AuditRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, unrelatedRepository, "Infrastructure/AuditRepository.cs", 41, attachSymbol: true, tableName: "audit_events", columnNames: "id;actor", queryShapeHash: "audit-shape")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Empty(result.Report.DependencySurfaces);
        Assert.DoesNotContain(result.Report.FlowRows, row => row.RowKind == "terminal-surface");
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.FilePath == "Infrastructure/AuditRepository.cs");
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "DataSurfaceAttachmentMissing").GapId);
        Assert.Equal("server", gap.SourceLabel);
        Assert.Equal("Controllers/OrdersController.cs", gap.FilePath);
        Assert.Contains("no matching terminal dependency/data surface", gap.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_attaches_http_client_surface_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var gateway = "Server.PaymentGateway.Charge(System.Int32)";
        var unrelatedGateway = "Server.AuditGateway.Send(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/charge", "/api/orders/{}/charge", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, gateway, "Controllers/OrdersController.cs", 15),
            HttpClientFact(server, "POST", "/billing/{id}", "/billing/{}", "Services/PaymentGateway.cs", 24, gateway),
            HttpClientFact(server, "POST", "/audit/{id}", "/audit/{}", "Services/AuditGateway.cs", 31, unrelatedGateway)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/charge",
            ToSurface: "http-client"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/charge",
            ToSurface: "http-client"));

        var surface = Assert.Single(result.Report.DependencySurfaces);
        Assert.Equal("http-client", surface.SurfaceKind);
        Assert.Equal("/billing/{}", surface.DisplayName);
        Assert.Equal("combined.route-flow.dependency-surface.v1", surface.Evidence.RuleId);
        Assert.Contains(RuleIds.HttpClientInvocation, surface.Evidence.SupportingRuleIds);
        Assert.Equal(surface.SurfaceId, Assert.Single(repeated.Report.DependencySurfaces).SurfaceId);
        Assert.Equal(surface.StableKey, Assert.Single(repeated.Report.DependencySurfaces).StableKey);
        Assert.StartsWith("surface-key-hash:", surface.StableKey, StringComparison.Ordinal);

        var terminal = Assert.Single(result.Report.FlowRows, row => row.EdgeKind == "terminal-surface");
        Assert.Equal("terminal-surface", terminal.RowKind);
        Assert.Contains(gateway, terminal.SourceSymbol, StringComparison.Ordinal);
        Assert.Contains("/billing/{}", terminal.TargetSymbol!, StringComparison.Ordinal);
        Assert.Equal(terminal.RowId, Assert.Single(repeated.Report.FlowRows, row => row.EdgeKind == "terminal-surface").RowId);

        Assert.DoesNotContain(result.Report.FlowRows, row => row.SourceSymbol.Contains("AuditGateway", StringComparison.Ordinal)
            || row.TargetSymbol?.Contains("/audit/{}", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.DependencySurfaces, item => item.Evidence.FilePath == "Services/AuditGateway.cs");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "DataSurfaceAttachmentMissing" or "NoRouteFlowEvidence");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "dependency"
            && group.MatchKind == "dependency-surface"
            && group.SupportingRowIds.Contains(surface.SurfaceId));
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_http_client_surface_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var service = "Server.OrderService.Charge(System.Int32)";
        var unrelatedGateway = "Server.AuditGateway.Send(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/charge", "/api/orders/{}/charge", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 15),
            HttpClientFact(server, "POST", "/audit/{id}", "/audit/{}", "Services/AuditGateway.cs", 31, unrelatedGateway)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/charge",
            ToSurface: "http-client"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/charge",
            ToSurface: "http-client"));

        Assert.Empty(result.Report.DependencySurfaces);
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "DataSurfaceAttachmentMissing").GapId);
        Assert.DoesNotContain(result.Report.FlowRows, row => row.EdgeKind == "terminal-surface"
            && row.TargetSymbol?.Contains("/audit/{}", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_attaches_package_config_surfaces_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var unrelatedRepository = "Server.AuditRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            PackageConfigFact(server, repository, FactTypes.PackageReferenced, RuleIds.ProjectFile, "Dapper", null, "Infrastructure/OrderRepository.cs", 31),
            PackageConfigFact(server, repository, FactTypes.ConfigKeyDeclared, RuleIds.ConfigKey, null, "Features:Orders:Enabled", "Infrastructure/OrderRepository.cs", 32),
            PackageConfigFact(server, repository, FactTypes.ConnectionStringDeclared, RuleIds.ConfigKey, null, "OrdersDb", "Infrastructure/OrderRepository.cs", 33),
            PackageConfigFact(server, repository, FactTypes.ConfigBinding, RuleIds.CSharpSemanticContractMapping, null, "Customers", "Infrastructure/OrderRepository.cs", 34),
            PackageConfigFact(server, unrelatedRepository, FactTypes.PackageReferenced, RuleIds.ProjectFile, "Newtonsoft.Json", null, "Infrastructure/AuditRepository.cs", 41)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "package-config"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "package-config"));

        Assert.Equal(4, result.Report.DependencySurfaces.Count);
        Assert.All(result.Report.DependencySurfaces, surface =>
        {
            Assert.Equal("package-config", surface.SurfaceKind);
            Assert.Equal("combined.route-flow.dependency-surface.v1", surface.Evidence.RuleId);
            Assert.StartsWith("surface-key-hash:", surface.StableKey, StringComparison.Ordinal);
        });
        Assert.Contains(result.Report.DependencySurfaces, surface =>
            surface.DisplayName == "Dapper"
            && surface.SafeMetadata.TryGetValue("packageName", out var packageName)
            && packageName == "Dapper"
            && surface.Evidence.SupportingRuleIds.Contains(RuleIds.ProjectFile, StringComparer.Ordinal));
        Assert.Contains(result.Report.DependencySurfaces, surface =>
            surface.DisplayName == "Features:Orders:Enabled"
            && surface.SafeMetadata.TryGetValue("configKeyHash", out var configKeyHash)
            && configKeyHash == CombinedReportHelpers.Hash("Features:Orders:Enabled", 16)
            && surface.Evidence.SupportingRuleIds.Contains(RuleIds.ConfigKey, StringComparer.Ordinal));
        Assert.Contains(result.Report.DependencySurfaces, surface =>
            surface.DisplayName == "OrdersDb"
            && surface.SafeMetadata.TryGetValue("configKeyHash", out var configKeyHash)
            && configKeyHash == CombinedReportHelpers.Hash("OrdersDb", 16)
            && surface.Evidence.SupportingRuleIds.Contains(RuleIds.ConfigKey, StringComparer.Ordinal));
        Assert.Contains(result.Report.DependencySurfaces, surface =>
            surface.DisplayName == "Customers"
            && surface.SafeMetadata.TryGetValue("configKeyHash", out var configKeyHash)
            && configKeyHash == CombinedReportHelpers.Hash("Customers", 16)
            && surface.Evidence.SupportingRuleIds.Contains(RuleIds.CSharpSemanticContractMapping, StringComparer.Ordinal));
        Assert.Equal(
            result.Report.DependencySurfaces.Select(surface => surface.StableKey).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            repeated.Report.DependencySurfaces.Select(surface => surface.StableKey).OrderBy(value => value, StringComparer.Ordinal).ToArray());
        Assert.Contains(result.Report.LogicRows, row =>
            row.LogicKind == "dependency-surface"
            && row.AttachmentKind == "fact-symbol-projection"
            && row.SafeMetadata.TryGetValue("packageName", out var packageName)
            && packageName == "Dapper");
        Assert.Contains(result.Report.LogicRows, row =>
            row.LogicKind == "dependency-surface"
            && row.AttachmentKind == "fact-symbol-projection"
            && row.SafeMetadata.TryGetValue("configKeyHash", out var configKeyHash)
            && configKeyHash == CombinedReportHelpers.Hash("Features:Orders:Enabled", 16));
        Assert.Contains(result.Report.LogicRows, row =>
            row.LogicKind == "dependency-surface"
            && row.AttachmentKind == "fact-symbol-projection"
            && row.SafeMetadata.TryGetValue("configKeyHash", out var configKeyHash)
            && configKeyHash == CombinedReportHelpers.Hash("OrdersDb", 16));
        Assert.Contains(result.Report.LogicRows, row =>
            row.LogicKind == "dependency-surface"
            && row.AttachmentKind == "fact-symbol-projection"
            && row.SafeMetadata.TryGetValue("configKeyHash", out var configKeyHash)
            && configKeyHash == CombinedReportHelpers.Hash("Customers", 16));

        Assert.DoesNotContain(result.Report.DependencySurfaces, surface => surface.DisplayName == "Newtonsoft.Json");
        Assert.DoesNotContain(result.Report.FlowRows, row => row.SourceSymbol.Contains("AuditRepository", StringComparison.Ordinal)
            || row.TargetSymbol?.Contains("Newtonsoft.Json", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "DataSurfaceAttachmentMissing" or "NoRouteFlowEvidence");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "dependency"
            && group.MatchKind == "dependency-surface"
            && group.SupportingRowIds.Any(rowId => result.Report.DependencySurfaces.Any(surface => surface.SurfaceId == rowId)));
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_package_config_surface_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var unrelatedRepository = "Server.AuditRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            PackageConfigFact(server, unrelatedRepository, FactTypes.PackageReferenced, RuleIds.ProjectFile, "Newtonsoft.Json", null, "Infrastructure/AuditRepository.cs", 41),
            PackageConfigFact(server, unrelatedRepository, FactTypes.ConfigKeyDeclared, RuleIds.ConfigKey, null, "Audit:Sink:Enabled", "Infrastructure/AuditRepository.cs", 42),
            PackageConfigFact(server, unrelatedRepository, FactTypes.ConnectionStringDeclared, RuleIds.ConfigKey, null, "AuditDb", "Infrastructure/AuditRepository.cs", 43),
            PackageConfigFact(server, unrelatedRepository, FactTypes.ConfigBinding, RuleIds.CSharpSemanticContractMapping, null, "AuditOptions", "Infrastructure/AuditRepository.cs", 44)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "package-config"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "package-config"));

        Assert.Empty(result.Report.DependencySurfaces);
        Assert.DoesNotContain(result.Report.FlowRows, row => row.RowKind == "terminal-surface");
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "DataSurfaceAttachmentMissing").GapId);
        Assert.Equal("server", gap.SourceLabel);
        Assert.Equal("Controllers/OrdersController.cs", gap.FilePath);
        Assert.Contains("no matching terminal dependency/data surface", gap.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "FactSymbolUnsupportedTypeSkipped" or "NoRouteFlowEvidence");
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_markdown_renderer_treats_missing_additive_touched_lists_as_empty()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp);
        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query",
            Classification: RouteFlowClassifications.StrongStaticRouteFlow));
        var json = JsonSerializer.Serialize(result.Report, CombinedDependencyReporter.JsonOptions);
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("touchedFiles");
        node.Remove("touchedSymbols");
        node.Remove("contextGroups");
        node["query"]!.AsObject().Remove("selectorTrace");
        var oldReport = JsonSerializer.Deserialize<RouteFlowReport>(node.ToJsonString(), CombinedDependencyReporter.JsonOptions);

        var render = typeof(CombinedRouteFlowReporter).GetMethod("RenderMarkdown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(render);
        var markdown = Assert.IsType<string>(render!.Invoke(null, [oldReport!]));

        Assert.Contains("- Touched files: `0`", markdown);
        Assert.Contains("- Touched symbols: `0`", markdown);
        Assert.Contains("- Context groups: `0`", markdown);
        Assert.Contains("- Selector trace: `unavailable`", markdown);
        Assert.Contains("## Context Groups", markdown);
        Assert.Contains("## Touched Files", markdown);
        Assert.Contains("## Touched Symbols", markdown);
    }

    [Fact]
    public async Task Client_call_selector_preserves_generic_client_server_paths()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var clientMethod = "Client.OrderService.loadOrder(System.Int32)";
        var clientCache = "Client.OrderCache.read(System.Int32)";
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5, clientMethod),
            CallFact(client, clientMethod, clientCache, "src/orders.ts", 8),
            QueryPatternFact(client, clientCache, "src/cache.ts", 13, attachSymbol: true)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            ClientCall: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.FlowRows, row => row.RowKind == "client-server-alignment");
        Assert.Contains(result.Report.FlowRows, row => row.SourceSymbol.Contains(controller, StringComparison.Ordinal));
        Assert.Contains(result.Report.FlowRows, row => row.SourceSymbol.Contains(clientCache, StringComparison.Ordinal));
        Assert.Contains(result.Report.TouchedSymbols, row => row.DisplayName.Contains(controller, StringComparison.Ordinal)
            && row.SourceLabel == "server"
            && row.CommitSha == server.CommitSha
            && row.FilePath == "Controllers/OrdersController.cs");
        Assert.DoesNotContain(result.Report.TouchedSymbols, row => row.DisplayName.Contains(controller, StringComparison.Ordinal)
            && row.SourceLabel == "client");
    }

    [Fact]
    public async Task Route_selector_composition_honors_from_source_scope()
    {
        using var temp = new TempDirectory();
        var firstIndex = Path.Combine(temp.Path, "server-a.sqlite");
        var secondIndex = Path.Combine(temp.Path, "server-b.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var first = Manifest("server-a", "tracemap-milestone15");
        var second = Manifest("server-b", "tracemap-milestone15");
        var firstController = "ServerA.OrdersController.Get(System.Int32)";
        var firstRepository = "ServerA.OrderRepository.Query(System.Int32)";
        var secondController = "ServerB.OrdersController.Get(System.Int32)";
        var secondRepository = "ServerB.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(firstIndex, first, [
            RouteFact(first, "GET", "/api/orders/{id}", "/api/orders/{}", firstController, "Controllers/OrdersController.cs", 10),
            CallFact(first, firstController, firstRepository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(first, firstRepository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        SqliteIndexWriter.Write(secondIndex, second, [
            RouteFact(second, "GET", "/api/orders/{id}", "/api/orders/{}", secondController, "Controllers/OrdersController.cs", 10),
            CallFact(second, secondController, secondRepository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(second, secondRepository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([firstIndex, secondIndex], combinedPath, ["server-a", "server-b"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            FromSource: "server-b",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.FlowRows, row => row.SourceSymbol.Contains(secondController, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Report.FlowRows, row => row.SourceSymbol.Contains(firstController, StringComparison.Ordinal)
            || row.TargetSymbol?.Contains(firstRepository, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Route_flow_emits_selector_gap_for_duplicate_route_roots_without_claiming_clean_flow()
    {
        using var temp = new TempDirectory();
        var firstIndex = Path.Combine(temp.Path, "server-a.sqlite");
        var secondIndex = Path.Combine(temp.Path, "server-b.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var first = Manifest("server-a", "tracemap-milestone15");
        var second = Manifest("server-b", "tracemap-milestone15");
        var firstController = "ServerA.OrdersController.Get(System.Int32)";
        var firstRepository = "ServerA.OrderRepository.Query(System.Int32)";
        var secondController = "ServerB.OrdersController.Get(System.Int32)";
        var secondRepository = "ServerB.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(firstIndex, first, [
            RouteFact(first, "GET", "/api/orders/{id}", "/api/orders/{}", firstController, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(first, firstController, firstRepository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(first, firstRepository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        SqliteIndexWriter.Write(secondIndex, second, [
            RouteFact(second, "GET", "/api/orders/{id}", "/api/orders/{}", secondController, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(second, secondController, secondRepository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(second, secondRepository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([firstIndex, secondIndex], combinedPath, ["server-a", "server-b"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var ambiguityGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch");
        Assert.Equal("combined.route-flow.selector.v1", ambiguityGap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, ambiguityGap.EvidenceTier);
        Assert.Equal("ReducedCoverage", ambiguityGap.Coverage);
        Assert.NotEmpty(ambiguityGap.SupportingFactIds);
        Assert.NotNull(ambiguityGap.AffectedRowId);
        Assert.Contains("multiple endpoint roots", ambiguityGap.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first.CommitSha, ambiguityGap.CommitSha);
        Assert.Equal("csharp", ambiguityGap.ExtractorName);
        Assert.Equal(first.ScannerVersion, ambiguityGap.ExtractorVersion);
        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root" && row.Evidence.SourceLabel == "server-a");
        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root" && row.Evidence.SourceLabel == "server-b");
        Assert.Contains(result.Report.FlowRows, row => row.SourceSymbol.Contains(firstController, StringComparison.Ordinal));
        Assert.Contains(result.Report.FlowRows, row => row.SourceSymbol.Contains(secondController, StringComparison.Ordinal));
        Assert.Equal("ReducedCoverage", result.Report.ReportCoverage);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
        Assert.NotNull(result.Report.Query.SelectorTrace);
        Assert.Equal("ReducedCoverage", result.Report.Query.SelectorTrace!.Coverage);

        var narrowed = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-narrowed"),
            Route: "GET /api/orders/{id}",
            FromSource: "server-b",
            ToSurface: "sql-query"));

        Assert.DoesNotContain(narrowed.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch");
        Assert.Contains(narrowed.Report.FlowRows, row => row.SourceSymbol.Contains(secondController, StringComparison.Ordinal));
        Assert.DoesNotContain(narrowed.Report.FlowRows, row => row.SourceSymbol.Contains(firstController, StringComparison.Ordinal));

        var noTerminal = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-no-terminal"),
            Route: "GET /api/orders/{id}",
            ToSurface: "wcf-operation"));

        var noTerminalAmbiguityGap = Assert.Single(noTerminal.Report.Gaps, gap => gap.GapId == ambiguityGap.GapId);
        Assert.Equal(ambiguityGap.GapId, noTerminalAmbiguityGap.GapId);
        Assert.Equal(first.CommitSha, noTerminalAmbiguityGap.CommitSha);
        Assert.Equal(first.ScannerVersion, noTerminalAmbiguityGap.ExtractorVersion);
        Assert.Contains(noTerminal.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, noTerminal.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_caps_duplicate_route_root_gap_supporting_facts()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var routeFacts = Enumerable.Range(0, 25)
            .Select(index => RouteFact(
                server,
                "GET",
                "/api/orders/{id}",
                "/api/orders/{}",
                $"Server.OrdersController{index:00}.Get(System.Int32)",
                $"Controllers/OrdersController{index:00}.cs",
                10 + index,
                EvidenceTiers.Tier1Semantic))
            .ToArray();

        SqliteIndexWriter.Write(serverIndex, server, routeFacts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var ambiguityGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch" && gap.RuleId == "combined.route-flow.selector.v1");
        var repeatedAmbiguityGap = Assert.Single(repeated.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch" && gap.RuleId == "combined.route-flow.selector.v1");
        var expectedSupportingFactIds = await CombinedRouteFactIdsInCanonicalOrderAsync(combinedPath, 20);
        Assert.Equal(20, ambiguityGap.SupportingFactIds.Count);
        Assert.Equal(expectedSupportingFactIds.OrderBy(value => value, StringComparer.Ordinal), ambiguityGap.SupportingFactIds);
        Assert.Equal(ambiguityGap.GapId, repeatedAmbiguityGap.GapId);
        Assert.Contains(ambiguityGap.Limitations, limitation => limitation.Contains("capped at 20 of 25", StringComparison.Ordinal));
        Assert.DoesNotContain(ambiguityGap.SupportingFactIds, string.IsNullOrWhiteSpace);
    }

    [Fact]
    public async Task Route_flow_emits_missing_method_symbol_bridge_for_route_roots_without_source_local_handler_symbol()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFactWithoutMethodSymbol(server, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10),
            CallFact(server, "Server.Unrelated.Start(System.Int32)", "Server.OrderRepository.Query(System.Int32)", "Services/Unrelated.cs", 20),
            QueryPatternFact(server, "Server.OrderRepository.Query(System.Int32)", "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root");
        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root"
            && row.BridgeState == "missing"
            && row.Evidence.RuleId == "combined.route-flow.entry.v1");
        Assert.Empty(result.Report.FlowRows);
        var bridgeGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "MissingMethodSymbolBridge" && gap.RuleId == "combined.route-flow.gap.v1");
        Assert.Equal("Controllers/OrdersController.cs", bridgeGap.FilePath);
        Assert.Equal(10, bridgeGap.StartLine);
        var touchedControllerFile = Assert.Single(result.Report.TouchedFiles, file => file.FilePath == "Controllers/OrdersController.cs");
        Assert.NotEqual("unknown", touchedControllerFile.CommitSha);
        Assert.Contains(bridgeGap.GapId, touchedControllerFile.SupportingRowIds);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, touchedControllerFile.Classification);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "MissingCallEdge");
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_marks_missing_endpoint_bridge_as_reduced_coverage_when_source_is_reduced()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15", buildStatus: "Failed");

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFactWithoutMethodSymbol(server, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}"));

        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root"
            && row.BridgeState == "reduced-coverage"
            && row.Evidence.RuleId == "combined.route-flow.entry.v1");
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "MissingMethodSymbolBridge");
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ReducedCoverage" && gap.SourceLabel == "server");
        Assert.Equal("ReducedCoverage", result.Report.ReportCoverage);
    }

    [Fact]
    public async Task Route_flow_emits_missing_route_root_when_only_matching_client_endpoint_context_exists()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");

        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5, "Client.OrderService.loadOrder(System.Int32)")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex], combinedPath, ["client"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}"));

        var missingRoot = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "MissingRouteRoot");
        Assert.NotEmpty(missingRoot.SupportingFactIds);
        Assert.Equal("src/orders.ts", missingRoot.FilePath);
        Assert.Equal(5, missingRoot.StartLine);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch");
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_bridges_interface_call_to_static_implementation_candidate_and_downstream_surface()
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
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14, targetSymbolKind: "InterfaceMember"),
            SymbolRelationshipFact(server, implementation, service, "Services/OrderService.cs", 18),
            CallFact(server, implementation, repository, "Services/OrderService.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.FlowRows, row => row.RowKind == "endpoint-method-bridge" && row.EdgeKind == "route-bound-to-symbol");
        Assert.Contains(result.Report.FlowRows, row => row.RowKind == "interface-implementation-candidate"
            && row.SourceSymbol.Contains(service, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains(implementation, StringComparison.Ordinal) == true
            && row.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow
            && row.Evidence.RuleId == "combined.route-flow.interface-bridge.v1");
        Assert.Contains(result.Report.FlowRows, row => row.EdgeKind == "direct-call"
            && row.SourceSymbol.Contains(implementation, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains(repository, StringComparison.Ordinal) == true
            && row.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow);
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query"
            && surface.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "MissingImplementationBridge");
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
        Assert.NotEqual(RouteFlowClassifications.ProbableStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_marks_multiple_interface_candidates_ambiguous_but_keeps_direct_concrete_edge_stronger()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.IOrderService.Get(System.Int32)";
        var implementation = "Server.OrderService.Get(System.Int32)";
        var alternate = "Server.CachedOrderService.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14, targetSymbolKind: "InterfaceMember"),
            CallFact(server, controller, implementation, "Controllers/OrdersController.cs", 15),
            SymbolRelationshipFact(server, implementation, service, "Services/OrderService.cs", 18),
            SymbolRelationshipFact(server, alternate, service, "Services/CachedOrderService.cs", 19),
            CallFact(server, implementation, repository, "Services/OrderService.cs", 21),
            CallFact(server, alternate, repository, "Services/CachedOrderService.cs", 22),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "AmbiguousImplementationCandidates");
        var ambiguousGapId = result.Report.Gaps.Single(gap => gap.GapKind == "AmbiguousImplementationCandidates").GapId;
        Assert.True(result.Report.FlowRows.Count(row => row.RowKind == "interface-implementation-candidate") >= 2);
        Assert.Contains(result.Report.FlowRows, row => row.EdgeKind == "direct-call"
            && row.SourceSymbol.Contains(controller, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains(implementation, StringComparison.Ordinal) == true
            && row.Classification == RouteFlowClassifications.ProbableStaticRouteFlow);
        Assert.All(result.Report.FlowRows.Where(row => row.RowKind == "interface-implementation-candidate"),
            row => Assert.Equal(RouteFlowClassifications.NeedsReviewStaticRouteFlow, row.Classification));
        Assert.True(result.Report.ContextGroups!.Count(group => group.GroupKind == "interface-candidate") >= 2);
        Assert.All(result.Report.ContextGroups!.Where(group => group.GroupKind == "interface-candidate"),
            group =>
            {
                Assert.Equal(RouteFlowClassifications.NeedsReviewStaticRouteFlow, group.Classification);
                Assert.Equal("interface-candidate", group.MatchKind);
                Assert.Contains("combined.route-flow.interface-bridge.v1", group.RuleIds);
            });
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "gap"
            && group.SafeMetadata.TryGetValue("gapKind", out var gapKind)
            && gapKind == "AmbiguousImplementationCandidates"
            && group.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow);

        var second = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-second"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        Assert.Equal(ambiguousGapId, second.Report.Gaps.Single(gap => gap.GapKind == "AmbiguousImplementationCandidates").GapId);
    }

    [Fact]
    public async Task Route_flow_caps_syntax_only_name_only_interface_candidate_at_needs_review()
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
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14, targetSymbolKind: "InterfaceMember"),
            SymbolRelationshipFact(server, implementation, service, "Services/OrderService.cs", 18, EvidenceTiers.Tier3SyntaxOrTextual, "NameOnlyCandidate"),
            CallFact(server, implementation, repository, "Services/OrderService.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var candidate = Assert.Single(result.Report.FlowRows, row => row.RowKind == "interface-implementation-candidate");
        Assert.Equal(RouteFlowClassifications.NeedsReviewStaticRouteFlow, candidate.Classification);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.Evidence.EvidenceTier);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_caps_high_fan_out_interface_candidates_at_needs_review()
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
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/StatusController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/StatusController.cs", 14, targetSymbolKind: "InterfaceMember"),
            QueryPatternFact(server, repository, "Infrastructure/StatusRepository.cs", 31, attachSymbol: true)
        };

        for (var index = 0; index < 11; index++)
        {
            var implementation = $"Server.StatusService{index}.Status(System.Int32)";
            facts.Add(SymbolRelationshipFact(server, implementation, service, $"Services/StatusService{index}.cs", 18 + index));
            facts.Add(CallFact(server, implementation, repository, $"Services/StatusService{index}.cs", 40 + index));
        }

        SqliteIndexWriter.Write(serverIndex, server, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "AmbiguousImplementationCandidates");
        var fanOutGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DispatchCandidateFanOut");
        Assert.Equal("combined.route-flow.gap.v1", fanOutGap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, fanOutGap.EvidenceTier);
        Assert.Equal("ReducedCoverage", fanOutGap.Coverage);
        Assert.Equal("combined", fanOutGap.ExtractorName);
        Assert.False(string.IsNullOrWhiteSpace(fanOutGap.AffectedRowId));
        Assert.Contains(fanOutGap.Limitations, limitation => limitation.Contains("deterministically capped", StringComparison.OrdinalIgnoreCase));
        var candidates = result.Report.FlowRows.Where(row => row.RowKind == "interface-implementation-candidate").ToArray();
        Assert.Equal(10, candidates.Length);
        Assert.All(candidates, row => Assert.Equal(RouteFlowClassifications.NeedsReviewStaticRouteFlow, row.Classification));
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_caps_mixed_tier_call_edges_at_needs_review()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14, EvidenceTiers.Tier3SyntaxOrTextual),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.FlowRows, row => row.EdgeKind == "direct-call"
            && row.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow
            && row.Evidence.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
        Assert.NotEqual(RouteFlowClassifications.ProbableStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_caps_tier3_downstream_service_edge_even_with_strong_route_root()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.OrderService.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14, EvidenceTiers.Tier1Semantic),
            CallFact(server, service, repository, "Services/OrderService.cs", 21, EvidenceTiers.Tier3SyntaxOrTextual),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.FlowRows, row => row.EdgeKind == "direct-call"
            && row.SourceSymbol.Contains(controller, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains(service, StringComparison.Ordinal) == true
            && row.Evidence.EvidenceTier == EvidenceTiers.Tier1Semantic);
        Assert.Contains(result.Report.FlowRows, row => row.EdgeKind == "direct-call"
            && row.SourceSymbol.Contains(service, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains(repository, StringComparison.Ordinal) == true
            && row.Evidence.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && row.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow);
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query"
            && surface.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_preserves_interface_call_and_emits_candidate_unavailable_gap()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.IOrderService.Get(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14, targetSymbolKind: "InterfaceMember"),
            QueryPatternFact(server, "Server.Unrelated.Query(System.Int32)", "Infrastructure/UnrelatedRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "MissingImplementationBridge");
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ImplementationCandidateUnavailable");
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "MissingCallEdge");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "gap"
            && group.SafeMetadata.TryGetValue("gapKind", out var gapKind)
            && gapKind == "ImplementationCandidateUnavailable"
            && group.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow);
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "gap"
            && group.SafeMetadata.TryGetValue("gapKind", out var gapKind)
            && gapKind == "MissingImplementationBridge"
            && group.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_does_not_treat_runtime_adjacent_facts_as_implementation_dispatch_proof()
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
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14, targetSymbolKind: "InterfaceMember"),
            RuntimeEvidenceFact(server, FactTypes.DependencyRegistered, "DependencyInjectionRegistration", service, implementation, "Startup/CompositionRoot.cs", 20),
            RuntimeEvidenceFact(server, FactTypes.DependencyResolved, "ServiceLocatorResolution", service, implementation, "Services/ServiceLocator.cs", 21),
            RuntimeEvidenceFact(server, FactTypes.ReflectionTarget, "ReflectionTarget", implementation, repository, "Services/ReflectionFactory.cs", 22),
            RuntimeEvidenceFact(server, FactTypes.DynamicDispatchCandidate, "DynamicDispatchCandidate", implementation, repository, "Services/DynamicFactory.cs", 23),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ImplementationCandidateUnavailable");
        Assert.DoesNotContain(result.Report.FlowRows, row => row.RowKind == "interface-implementation-candidate");
        Assert.DoesNotContain(result.Report.FlowRows, row => row.TargetSymbol?.Contains(implementation, StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.FlowRows, row => row.TargetSymbol?.Contains(repository, StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query");
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
        Assert.NotEqual(RouteFlowClassifications.ProbableStaticRouteFlow, result.Report.Summary.Classification);

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "route-flow", "route-flow-report.json"));
        var markdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "route-flow", "route-flow-report.md"));
        AssertForbiddenRuntimeWording(json);
        AssertForbiddenRuntimeWording(markdown);
    }

    [Fact]
    public void Route_flow_runtime_binding_gap_preserves_commit_and_extractor_metadata()
    {
        var service = "Server.IOrderService.Get(System.Int32)";
        var node = new CombinedPathNode(
            NodeId: "node:server:service",
            NodeKind: "Method",
            DisplayName: service,
            SourceIndexId: "server",
            SourceLabel: "server",
            ScanId: "scan-server",
            CommitSha: "abc123",
            SymbolId: service,
            CombinedFactId: "server:fact-service",
            RuleId: RuleIds.CSharpSemanticCallGraph,
            EvidenceTier: EvidenceTiers.Tier1Semantic,
            FilePath: "Services/IOrderService.cs",
            StartLine: 14,
            EndLine: 14,
            SurfaceKind: null,
            SurfaceName: null,
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
            ConfigKey: null);
        var edge = new StaticDispatchCandidateEdge(
            "dispatch-candidate:other",
            StaticDispatchCandidateBuilder.AlgorithmId,
            StaticDispatchCandidateStates.SymbolBackedCandidate,
            "other",
            "other",
            null,
            node.NodeId,
            "node:other:implementation",
            "node:other:implementation",
            null,
            "ImplementsInterfaceMember",
            StaticDispatchBridgeKinds.InterfaceMember,
            "interface-candidate",
            EvidenceTiers.Tier1Semantic,
            StaticDispatchCandidateBuilder.CandidateRuleId,
            ["other:relationship-fact"],
            ["other:relationship-edge"],
            ["edge:other:relationship"],
            [],
            "none",
            "Services/OrderService.cs",
            20,
            20,
            ["Static candidate evidence does not prove runtime dispatch or dependency-injection binding."],
            []);

        var method = typeof(CombinedRouteFlowReporter).GetMethod(
            "RuntimeBindingNotProvenGap",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var gap = Assert.IsType<RouteFlowGap>(method!.Invoke(null, [
            node,
            new[] { edge },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["server"] = "tracemap-milestone15" }
        ]));

        Assert.Equal("RuntimeBindingNotProven", gap.GapKind);
        Assert.Equal("abc123", gap.CommitSha);
        Assert.Equal("combined", gap.ExtractorName);
        Assert.Equal("tracemap-milestone15", gap.ExtractorVersion);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal("ReducedCoverage", gap.Coverage);
        Assert.Contains("other:relationship-fact", gap.SupportingFactIds);
    }

    [Fact]
    public async Task Route_flow_caps_syntax_route_root_bridge_even_with_stronger_downstream_edges()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier3SyntaxOrTextual),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14, EvidenceTiers.Tier1Semantic),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.FlowRows, row => row.RowKind == "endpoint-method-bridge"
            && row.Evidence.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && row.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow);
        Assert.Contains(result.Report.FlowRows, row => row.EdgeKind == "direct-call"
            && row.Evidence.EvidenceTier == EvidenceTiers.Tier1Semantic
            && row.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow);
        Assert.Contains(result.Report.TouchedFiles, row => row.FilePath == "Controllers/OrdersController.cs"
            && row.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow
            && row.Coverage == "CoverageRelative"
            && row.EvidenceTiers.Contains(EvidenceTiers.Tier3SyntaxOrTextual)
            && row.Evidence.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual);
        Assert.Contains(result.Report.TouchedSymbols, row => row.DisplayName == "GET /api/orders/{}"
            && row.Classification == RouteFlowClassifications.NeedsReviewStaticRouteFlow
            && row.Evidence.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual);
    }

    [Fact]
    public async Task Route_flow_does_not_emit_clean_no_evidence_gap_when_path_truncation_blocks_absence()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            QueryPatternFact(server, "Server.OtherRepository.Query(System.Int32)", "Infrastructure/OtherRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root");
        Assert.Empty(result.Report.FlowRows);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "MissingRouteRoot" or "MissingMethodSymbolBridge" or "MissingCallEdge");
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit"
            && gap.Classification == RouteFlowClassifications.UnknownAnalysisGap);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.All(result.Report.ContextGroups!, group => Assert.Equal("gap", group.GroupKind));
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_does_not_emit_clean_no_evidence_gap_when_no_direct_call_under_reduced_coverage()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15", buildStatus: "Failed");
        var controller = "Server.OrdersController.Get(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root");
        Assert.Empty(result.Report.FlowRows);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ReducedCoverage"
            && gap.RuleId == "combined.route-flow.gap.v1"
            && gap.EvidenceTier == EvidenceTiers.Tier4Unknown
            && gap.Coverage == "ReducedCoverage"
            && gap.SourceLabel == "server");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "MissingCallEdge");
        Assert.Equal("ReducedCoverage", result.Report.ReportCoverage);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_suppresses_clean_no_evidence_when_no_direct_call_has_full_coverage_selector_blocker()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));
        await SetCombinedSourceLanguageAsync(combinedPath, "csharp");

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}"));

        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root"
            && row.BridgeState == "method-symbol");
        Assert.Empty(result.Report.FlowRows);
        var selectorGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch");
        Assert.Equal("combined.route-flow.gap.v1", selectorGap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, selectorGap.EvidenceTier);
        Assert.Equal("ReducedCoverage", selectorGap.Coverage);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "MissingCallEdge" or "ReducedCoverage");
        Assert.Equal("ReducedCoverage", result.Report.ReportCoverage);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_orders_direct_service_call_paths_deterministically()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var alphaService = "Server.AlphaOrderService.Get(System.Int32)";
        var betaService = "Server.BetaOrderService.Get(System.Int32)";
        var alphaRepository = "Server.AlphaOrderRepository.Query(System.Int32)";
        var betaRepository = "Server.BetaOrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, betaService, "Controllers/OrdersController.cs", 16),
            CallFact(server, controller, alphaService, "Controllers/OrdersController.cs", 14),
            CallFact(server, betaService, betaRepository, "Services/BetaOrderService.cs", 24),
            CallFact(server, alphaService, alphaRepository, "Services/AlphaOrderService.cs", 22),
            QueryPatternFact(server, betaRepository, "Infrastructure/BetaOrderRepository.cs", 34, attachSymbol: true),
            QueryPatternFact(server, alphaRepository, "Infrastructure/AlphaOrderRepository.cs", 32, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));
        await SetCombinedSourceLanguageAsync(combinedPath, "csharp");

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var controllerCalls = result.Report.FlowRows
            .Where(row => row.EdgeKind == "direct-call"
                && row.SourceSymbol.Contains(controller, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal([alphaService, betaService], controllerCalls.Select(row => Assert.IsType<string>(row.TargetSymbol)).ToArray());
        Assert.Equal(controllerCalls.Select(row => row.RowId), repeated.Report.FlowRows
            .Where(row => row.EdgeKind == "direct-call"
                && row.SourceSymbol.Contains(controller, StringComparison.Ordinal))
            .Select(row => row.RowId));
        Assert.All(controllerCalls, row =>
        {
            Assert.Equal("combined.route-flow.path.v1", row.Evidence.RuleId);
            Assert.Equal(EvidenceTiers.Tier1Semantic, row.Evidence.EvidenceTier);
        });
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "MissingCallEdge" or "NoRouteFlowEvidence");
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query");
    }

    [Fact]
    public async Task Route_flow_emits_data_surface_attachment_gap_when_downstream_calls_have_no_terminal_surface()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.OrderService.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            CallFact(server, service, repository, "Services/OrderService.cs", 21)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.NotEmpty(gap.SupportingFactIds);
        Assert.Equal("Controllers/OrdersController.cs", gap.FilePath);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "ControllerToServiceBridgeMissing");
        Assert.NotEqual(RouteFlowClassifications.NoRouteFlowEvidence, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_marks_service_call_cycles_as_traversal_bounds()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.OrderService.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            CallFact(server, service, controller, "Services/OrderService.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var traversalGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "TraversalBounds");
        var repeatedTraversalGap = Assert.Single(repeated.Report.Gaps, gap => gap.GapKind == "TraversalBounds");
        Assert.Equal("combined.route-flow.gap.v1", traversalGap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, traversalGap.EvidenceTier);
        Assert.Equal("ReducedCoverage", traversalGap.Coverage);
        Assert.Equal("Services/OrderService.cs", traversalGap.FilePath);
        Assert.Equal(21, traversalGap.StartLine);
        Assert.Equal(server.CommitSha, traversalGap.CommitSha);
        Assert.Equal("csharp", traversalGap.ExtractorName);
        Assert.Equal(server.ScannerVersion, traversalGap.ExtractorVersion);
        Assert.True(traversalGap.SupportingFactIds.Count <= 20);
        Assert.Contains("cycle", traversalGap.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(traversalGap.Limitations, limitation => limitation.Contains("cycle detection", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(traversalGap.GapId, repeatedTraversalGap.GapId);
        Assert.Contains(result.Report.EntryEvidence, row => row.EntryKind == "route-root");
        Assert.Empty(result.Report.FlowRows);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "MissingCallEdge");
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.Equal("ReducedCoverage", result.Report.ReportCoverage);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_does_not_emit_cycle_gap_when_candidate_expansion_continues()
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
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14, targetSymbolKind: "InterfaceMember"),
            CallFact(server, service, controller, "Services/IOrderService.cs", 15, targetSymbolKind: "Method"),
            SymbolRelationshipFact(server, implementation, service, "Services/OrderService.cs", 18),
            CallFact(server, implementation, repository, "Services/OrderService.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "TraversalBounds");
        Assert.Contains(result.Report.FlowRows, row => row.RowKind == "interface-implementation-candidate"
            && row.SourceSymbol.Contains(service, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains(implementation, StringComparison.Ordinal) == true);
        Assert.Contains(result.Report.FlowRows, row => row.EdgeKind == "direct-call"
            && row.SourceSymbol.Contains(implementation, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains(repository, StringComparison.Ordinal) == true);
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query");
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_emits_object_creation_flow_row_for_creates_edge()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repositoryType = "Server.OrderRepository";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            ObjectCreationFact(server, controller, repositoryType, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repositoryType, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.FlowRows, row => row.RowKind == "object-creation" && row.EdgeKind == "object-creation");
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query");
    }

    [Fact]
    public async Task Route_flow_attaches_object_shape_projection_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.OrderService.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var unrelated = "Server.AuditService.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            CallFact(server, service, repository, "Services/OrderService.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true),
            ObjectShapeFact(server, repository, "anonymous-object", "Infrastructure/OrderRepository.cs", 32, "order-shape-hash", ["ShapeFieldAlpha", "Status"]),
            ObjectShapeFact(server, unrelated, "anonymous-object", "Infrastructure/AuditRepository.cs", 41, "audit-shape-hash", ["ShapeFieldBeta", "Status"])
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var objectShape = Assert.Single(result.Report.LogicRows, row => row.LogicKind == "object-shape"
            && row.AttachmentKind == "fact-symbol-projection");
        var repeatedObjectShape = Assert.Single(repeated.Report.LogicRows, row => row.LogicKind == "object-shape"
            && row.AttachmentKind == "fact-symbol-projection");
        Assert.Equal(objectShape.LogicRowId, repeatedObjectShape.LogicRowId);
        Assert.Equal(objectShape.DisplayName, repeatedObjectShape.DisplayName);
        Assert.StartsWith("object-shape:order-shape-hash", objectShape.DisplayName, StringComparison.Ordinal);
        Assert.Equal(RouteFlowClassifications.NeedsReviewStaticRouteFlow, objectShape.Classification);
        Assert.Equal("combined.route-flow.fact-symbol-projection.v1", objectShape.Evidence.RuleId);
        Assert.Contains(RuleIds.CSharpSyntaxObjectShape, objectShape.Evidence.SupportingRuleIds);
        Assert.Contains("combined.route-flow.redaction.v1", objectShape.Evidence.SupportingRuleIds);
        Assert.Equal("object-shape", objectShape.SafeMetadata["evidenceKind"]);
        Assert.Equal("order-shape-hash", objectShape.SafeMetadata["shapeHash"]);
        Assert.DoesNotContain("fieldNames", objectShape.SafeMetadata.Keys);
        Assert.DoesNotContain("ShapeFieldAlpha", JsonSerializer.Serialize(result.Report), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ShapeFieldBeta", JsonSerializer.Serialize(result.Report), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.FilePath == "Infrastructure/AuditRepository.cs");
        Assert.Contains(result.Report.ContextGroups ?? [], group => group.GroupKind == "data-surface"
            && group.MatchKind == "fact-symbol"
            && group.RuleIds.Contains("combined.route-flow.fact-symbol-projection.v1")
            && group.SupportingRowIds.Contains(objectShape.LogicRowId));
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "FactSymbolProjectionUnavailable" or "NoRouteFlowEvidence");
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_object_shape_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.OrderService.Get(System.Int32)";
        var unrelated = "Server.AuditService.Query(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, service, "Services/OrderService.cs", 31),
            ObjectShapeFact(server, unrelated, "anonymous-object", "Infrastructure/AuditRepository.cs", 41, "audit-shape-hash", ["ShapeFieldBeta", "Status"])
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.DoesNotContain(result.Report.LogicRows, row => row.LogicKind == "object-shape"
            && row.AttachmentKind == "fact-symbol-projection");
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "FactSymbolProjectionUnavailable");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Contains(gap.Limitations, limitation => limitation.Contains("combined.route-flow.fact-symbol-projection.v1", StringComparison.Ordinal));
        Assert.Equal("Infrastructure/AuditRepository.cs", gap.FilePath);
        Assert.Equal("server", gap.SourceLabel);
        Assert.Equal("abc123", gap.CommitSha);
        Assert.Equal("tracemap-milestone15", gap.ExtractorVersion);
        Assert.Contains("none could be connected to the selected route-flow path", gap.Message, StringComparison.Ordinal);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "FactSymbolProjectionUnavailable").GapId);
        Assert.DoesNotContain("ShapeFieldBeta", JsonSerializer.Serialize(result.Report), StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
    }

    [Fact]
    public async Task Route_flow_traverses_parameter_forward_bridge_to_data_surface()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.String)";
        var service = "Server.OrderService.Query(System.String)";
        var controllerParameter = $"{controller}:System.String request";
        var serviceParameter = $"{service}:System.String request";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            ArgumentPassedFact(server, controller, service, "System.String request", "request", "System.String", "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, serviceParameter, "Services/OrderService.cs", 24, attachSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.FlowRows, row => row.RowKind == "argument-flow"
            && row.EdgeKind == "argument-flow"
            && row.SourceSymbol.Contains(controller, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains(controllerParameter, StringComparison.Ordinal) == true
            && row.Evidence.SupportingRuleIds.Contains(RuleIds.CSharpSemanticParameterForwarding));
        Assert.Contains(result.Report.FlowRows, row => row.RowKind == "parameter-forward"
            && row.EdgeKind == "parameter-forward"
            && row.SourceSymbol.Contains(controllerParameter, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains(serviceParameter, StringComparison.Ordinal) == true
            && row.Evidence.SupportingRuleIds.Contains(RuleIds.CSharpSemanticParameterForwarding));
        Assert.Contains(result.Report.LogicRows, row => row.LogicKind == "flow-boundary"
            && row.SafeMetadata.TryGetValue("edgeKind", out var edgeKind)
            && edgeKind == "parameter-forward"
            && row.Evidence.SupportingRuleIds.Contains(RuleIds.CSharpSemanticParameterForwarding));
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query");
        Assert.Contains(result.Report.TouchedSymbols, row => row.DisplayName.Contains("System.String request", StringComparison.Ordinal)
            && row.SupportingRowIds.Any(id => id.StartsWith("row:", StringComparison.Ordinal)));
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "DataSurfaceAttachmentMissing" or "NoRouteFlowEvidence");

        var markdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "route-flow", "route-flow-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "route-flow", "route-flow-report.json"));
        Assert.Contains("parameter-forward", markdown);
        Assert.Contains("\"edgeKind\": \"parameter-forward\"", json);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Route_flow_attaches_value_origin_rows_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.String)";
        var service = "Server.OrderService.Query(System.String)";
        var controllerParameter = $"{controller}:System.String request";
        var serviceParameter = $"{service}:System.String request";
        var unrelatedCaller = "Server.Unrelated.Start(System.String)";
        var unrelatedCallee = "Server.Unrelated.Finish(System.String)";
        var unrelatedParameter = $"{unrelatedCaller}:System.String request";
        var otherUnrelatedCaller = "Server.OtherUnrelated.Start(System.String)";
        var otherUnrelatedCallee = "Server.OtherUnrelated.Finish(System.String)";
        var otherUnrelatedParameter = $"{otherUnrelatedCaller}:System.String request";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            ArgumentPassedFact(server, controller, service, controllerParameter, "request", "System.String", "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, service, "Services/OrderService.cs", 23, attachSymbol: true),
            QueryPatternFact(server, serviceParameter, "Services/OrderService.cs", 24, attachSymbol: true),
            ArgumentPassedFact(server, unrelatedCaller, unrelatedCallee, unrelatedParameter, "request", "System.String", "Services/Unrelated.cs", 40),
            ArgumentPassedFact(server, otherUnrelatedCaller, otherUnrelatedCallee, otherUnrelatedParameter, "request", "System.String", "Services/OtherUnrelated.cs", 42)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));
        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = combinedPath
        }.ToString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                update combined_argument_flows
                set caller_symbol = null
                where file_path = 'Services/Unrelated.cs';
                """;
            await command.ExecuteNonQueryAsync();
        }

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var argumentProjection = Assert.Single(result.Report.LogicRows, row => row.AttachmentKind == "argument-projection");
        Assert.Equal("argument-flow", argumentProjection.LogicKind);
        Assert.NotNull(argumentProjection.AttachedFlowRowId);
        Assert.Contains(result.Report.FlowRows, row => row.RowId == argumentProjection.AttachedFlowRowId
            && row.EdgeKind == "direct-call"
            && row.SourceSymbol.Contains(controller, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains(service, StringComparison.Ordinal) == true);
        Assert.Equal(
            argumentProjection.LogicRowId,
            Assert.Single(repeated.Report.LogicRows, row => row.AttachmentKind == "argument-projection").LogicRowId);

        var parameterForward = Assert.Single(result.Report.FlowRows, row => row.EdgeKind == "parameter-forward");
        Assert.Contains(controllerParameter, parameterForward.SourceSymbol, StringComparison.Ordinal);
        Assert.Contains(serviceParameter, parameterForward.TargetSymbol!, StringComparison.Ordinal);
        var parameterBoundary = Assert.Single(result.Report.LogicRows, row => row.LogicKind == "flow-boundary"
            && row.SafeMetadata.TryGetValue("edgeKind", out var edgeKind)
            && edgeKind == "parameter-forward");
        Assert.NotNull(parameterBoundary.AttachedFlowRowId);
        Assert.Contains(result.Report.FlowRows, row => row.RowId == parameterBoundary.AttachedFlowRowId);
        Assert.Equal(
            parameterForward.RowId,
            Assert.Single(repeated.Report.FlowRows, row => row.EdgeKind == "parameter-forward").RowId);
        Assert.Equal(
            parameterBoundary.LogicRowId,
            Assert.Single(repeated.Report.LogicRows, row => row.LogicKind == "flow-boundary"
                && row.SafeMetadata.TryGetValue("edgeKind", out var edgeKind)
                && edgeKind == "parameter-forward").LogicRowId);

        var argumentGaps = result.Report.Gaps
            .Where(row => row.GapKind == "ArgumentProjectionUnavailable")
            .OrderBy(row => row.FilePath, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, argumentGaps.Length);
        var gap = Assert.Single(argumentGaps, row => row.FilePath == "Services/Unrelated.cs");
        var otherGap = Assert.Single(argumentGaps, row => row.FilePath == "Services/OtherUnrelated.cs");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Equal("server", gap.SourceLabel);
        Assert.Equal("abc123", gap.CommitSha);
        Assert.Equal("tracemap-milestone15", gap.ExtractorVersion);
        Assert.Equal("combined.route-flow.gap.v1", otherGap.RuleId);
        Assert.Equal("server", otherGap.SourceLabel);
        Assert.Equal("abc123", otherGap.CommitSha);
        Assert.Equal("tracemap-milestone15", otherGap.ExtractorVersion);
        Assert.NotEqual(gap.GapId, otherGap.GapId);
        Assert.Equal(
            argumentGaps.Select(row => row.GapId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            repeated.Report.Gaps
                .Where(row => row.GapKind == "ArgumentProjectionUnavailable")
                .Select(row => row.GapId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

        Assert.DoesNotContain(result.Report.FlowRows, row => row.SourceSymbol.Contains("Server.Unrelated", StringComparison.Ordinal)
            || row.TargetSymbol?.Contains("Server.Unrelated", StringComparison.Ordinal) == true
            || row.SourceSymbol.Contains("Server.OtherUnrelated", StringComparison.Ordinal)
            || row.TargetSymbol?.Contains("Server.OtherUnrelated", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.FilePath == "Services/Unrelated.cs");
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.FilePath == "Services/OtherUnrelated.cs");
        Assert.DoesNotContain(result.Report.Gaps, row => row.GapKind == "NoRouteFlowEvidence");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "value-origin"
            && group.MatchKind == "argument-flow"
            && group.SupportingRowIds.Contains(argumentProjection.LogicRowId));
    }

    [Fact]
    public async Task Route_flow_attaches_async_callback_boundaries_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.OrderService.QueryAsync(System.Int32)";
        var unrelatedService = "Server.AuditService.ScheduleAsync(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, service, "Services/OrderService.cs", 31),
            BoundaryFact(server, service, FactTypes.AsyncBoundary, "AwaitBoundary", "Services/OrderService.cs", 24, asyncOperationKind: "await"),
            BoundaryFact(server, service, FactTypes.CallbackBoundary, "CallbackBoundary", "Services/OrderService.cs", 25, callbackBoundaryKind: "LambdaExpression"),
            BoundaryFact(server, unrelatedService, FactTypes.AsyncBoundary, "TaskSchedulingBoundary", "Services/AuditService.cs", 41, asyncOperationKind: "task-scheduling")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var boundaryRows = result.Report.LogicRows
            .Where(row => row.LogicKind == "flow-boundary"
                && row.AttachmentKind == "fact-symbol-projection")
            .OrderBy(row => row.DisplayName, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, boundaryRows.Length);
        Assert.All(boundaryRows, row =>
        {
            Assert.NotNull(row.AttachedFlowRowId);
            Assert.Equal(RouteFlowClassifications.NeedsReviewStaticRouteFlow, row.Classification);
            Assert.Equal("combined.route-flow.fact-symbol-projection.v1", row.Evidence.RuleId);
            Assert.Contains(RuleIds.CSharpSemanticFlowBoundary, row.Evidence.SupportingRuleIds);
            Assert.Equal("flow-boundary", row.SafeMetadata["evidenceKind"]);
        });
        Assert.Contains(boundaryRows, row => row.SafeMetadata.TryGetValue("boundaryKind", out var boundaryKind)
            && boundaryKind == "AwaitBoundary"
            && row.SafeMetadata.TryGetValue("asyncOperationKind", out var asyncOperationKind)
            && asyncOperationKind == "await");
        Assert.Contains(boundaryRows, row => row.SafeMetadata.TryGetValue("callbackBoundaryKind", out var callbackBoundaryKind)
            && callbackBoundaryKind == "LambdaExpression"
            && row.SafeMetadata.ContainsKey("callbackExpressionHash"));
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.FilePath == "Services/AuditService.cs");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "FactSymbolProjectionUnavailable" or "NoRouteFlowEvidence");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "value-origin"
            && group.MatchKind == "fact-symbol"
            && group.RuleIds.Contains("combined.route-flow.fact-symbol-projection.v1"));
        Assert.Equal(
            boundaryRows.Select(row => row.LogicRowId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            repeated.Report.LogicRows
                .Where(row => row.LogicKind == "flow-boundary" && row.AttachmentKind == "fact-symbol-projection")
                .Select(row => row.LogicRowId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_async_callback_boundary_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.OrderService.QueryAsync(System.Int32)";
        var unrelatedService = "Server.AuditService.ScheduleAsync(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, service, "Services/OrderService.cs", 31),
            BoundaryFact(server, unrelatedService, FactTypes.AsyncBoundary, "TaskSchedulingBoundary", "Services/AuditService.cs", 41, asyncOperationKind: "task-scheduling")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.DoesNotContain(result.Report.LogicRows, row => row.LogicKind == "flow-boundary"
            && row.AttachmentKind == "fact-symbol-projection");
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "FactSymbolProjectionUnavailable");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Contains(gap.Limitations, limitation => limitation.Contains("combined.route-flow.fact-symbol-projection.v1", StringComparison.Ordinal));
        Assert.Equal("Services/AuditService.cs", gap.FilePath);
        Assert.Equal("server", gap.SourceLabel);
        Assert.Equal("abc123", gap.CommitSha);
        Assert.Equal("tracemap-milestone15", gap.ExtractorVersion);
        Assert.Contains("none could be connected to the selected route-flow path", gap.Message, StringComparison.Ordinal);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "FactSymbolProjectionUnavailable").GapId);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
    }

    [Fact]
    public async Task Route_flow_attaches_validation_guard_branches_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.OrderService.Query(System.Int32)";
        var unrelatedService = "Server.AuditService.Check(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, service, "Services/OrderService.cs", 31),
            GuardFact(server, service, "If", "NullCheckNotEquals", "customerKey", "Services/OrderService.cs", 22),
            GuardFact(server, unrelatedService, "If", "NullCheckEquals", "auditId", "Services/AuditService.cs", 41)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var guard = Assert.Single(result.Report.LogicRows, row => row.LogicKind == "validation-guard"
            && row.AttachmentKind == "fact-symbol-projection");
        Assert.NotNull(guard.AttachedFlowRowId);
        Assert.Equal("validation-guard:NullCheckNotEquals", guard.DisplayName);
        Assert.Equal(RouteFlowClassifications.NeedsReviewStaticRouteFlow, guard.Classification);
        Assert.Equal("combined.route-flow.fact-symbol-projection.v1", guard.Evidence.RuleId);
        Assert.Contains(RuleIds.CSharpSemanticRuntimeEvidence, guard.Evidence.SupportingRuleIds);
        Assert.Equal("validation-guard", guard.SafeMetadata["evidenceKind"]);
        Assert.Equal("If", guard.SafeMetadata["branchKind"]);
        Assert.Equal("NullCheckNotEquals", guard.SafeMetadata["feasibilityKind"]);
        Assert.Equal("!=", guard.SafeMetadata["comparisonOperator"]);
        Assert.Equal("IdentifierName", guard.SafeMetadata["conditionExpressionKind"]);
        Assert.Contains("checkedSymbolHash", guard.SafeMetadata.Keys);
        Assert.DoesNotContain("checkedSymbol", guard.SafeMetadata.Keys);
        Assert.DoesNotContain("customerKey", JsonSerializer.Serialize(guard.SafeMetadata), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.FilePath == "Services/AuditService.cs");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "FactSymbolProjectionUnavailable" or "NoRouteFlowEvidence");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "method"
            && group.MatchKind == "fact-symbol"
            && group.RuleIds.Contains("combined.route-flow.fact-symbol-projection.v1")
            && group.SupportingRowIds.Contains(guard.LogicRowId));
        Assert.Equal(
            guard.LogicRowId,
            Assert.Single(repeated.Report.LogicRows, row => row.LogicKind == "validation-guard"
                && row.AttachmentKind == "fact-symbol-projection").LogicRowId);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_validation_guard_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.OrderService.Query(System.Int32)";
        var unrelatedService = "Server.AuditService.Check(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, service, "Services/OrderService.cs", 31),
            GuardFact(server, unrelatedService, "If", "NullCheckEquals", "auditId", "Services/AuditService.cs", 41)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.DoesNotContain(result.Report.LogicRows, row => row.LogicKind == "validation-guard"
            && row.AttachmentKind == "fact-symbol-projection");
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "FactSymbolProjectionUnavailable");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Contains(gap.Limitations, limitation => limitation.Contains("combined.route-flow.fact-symbol-projection.v1", StringComparison.Ordinal));
        Assert.Equal("Services/AuditService.cs", gap.FilePath);
        Assert.Equal("server", gap.SourceLabel);
        Assert.Equal("abc123", gap.CommitSha);
        Assert.Equal("tracemap-milestone15", gap.ExtractorVersion);
        Assert.Contains("none could be connected to the selected route-flow path", gap.Message, StringComparison.Ordinal);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "FactSymbolProjectionUnavailable").GapId);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
    }

    [Fact]
    public async Task Route_flow_attaches_serializer_contract_members_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var dtoType = "Server.Contracts.OrderResponse";
        var alternateDtoType = "Server.Contracts.OrderSummaryResponse";
        var unrelatedDtoType = "Server.Contracts.AuditResponse";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            ObjectCreationFact(server, controller, dtoType, "Controllers/OrdersController.cs", 14),
            ObjectCreationFact(server, controller, alternateDtoType, "Controllers/OrdersController.cs", 15),
            QueryPatternFact(server, dtoType, "Contracts/OrderResponse.cs", 31, attachSymbol: true),
            QueryPatternFact(server, alternateDtoType, "Contracts/OrderSummaryResponse.cs", 32, attachSymbol: true),
            SerializerContractFact(server, dtoType, "Status", "System.String", "customer_status", "Contracts/OrderResponse.cs", 7),
            SerializerContractFact(server, alternateDtoType, "SummaryStatus", "System.String", "customer_status", "Contracts/OrderSummaryResponse.cs", 8),
            SerializerContractFact(server, unrelatedDtoType, "InternalStatus", "System.String", "audit_status", "Contracts/AuditResponse.cs", 11)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        var serializerRows = result.Report.LogicRows
            .Where(row => row.LogicKind == "serializer-contract"
                && row.AttachmentKind == "fact-symbol-projection")
            .OrderBy(row => row.Evidence.FilePath, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, serializerRows.Length);
        Assert.Equal(2, serializerRows.Select(row => row.DisplayName).Distinct(StringComparer.Ordinal).Count());
        Assert.All(serializerRows, serializer =>
        {
            Assert.NotNull(serializer.AttachedFlowRowId);
            Assert.StartsWith("serializer-contract:contract-name-hash:", serializer.DisplayName, StringComparison.Ordinal);
            Assert.Contains(":type-hash:", serializer.DisplayName, StringComparison.Ordinal);
            Assert.Contains(":member-hash:", serializer.DisplayName, StringComparison.Ordinal);
            Assert.Equal(RouteFlowClassifications.NeedsReviewStaticRouteFlow, serializer.Classification);
            Assert.Equal("combined.route-flow.fact-symbol-projection.v1", serializer.Evidence.RuleId);
            Assert.Contains(RuleIds.CSharpSemanticRuntimeEvidence, serializer.Evidence.SupportingRuleIds);
            Assert.Equal("serializer-contract", serializer.SafeMetadata["evidenceKind"]);
            Assert.Contains("attributeNameHash", serializer.SafeMetadata.Keys);
            Assert.Contains("contractNameHash", serializer.SafeMetadata.Keys);
            Assert.Contains("memberNameHash", serializer.SafeMetadata.Keys);
            Assert.Contains("memberTypeHash", serializer.SafeMetadata.Keys);
            Assert.Contains("containingTypeHash", serializer.SafeMetadata.Keys);
            Assert.DoesNotContain("attributeName", serializer.SafeMetadata.Keys);
            Assert.DoesNotContain("contractName", serializer.SafeMetadata.Keys);
            Assert.DoesNotContain("memberName", serializer.SafeMetadata.Keys);
            Assert.DoesNotContain("customer_status", JsonSerializer.Serialize(serializer.SafeMetadata), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Status", JsonSerializer.Serialize(serializer.SafeMetadata), StringComparison.Ordinal);
            Assert.DoesNotContain("JsonPropertyName", JsonSerializer.Serialize(serializer.SafeMetadata), StringComparison.Ordinal);
        });
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.FilePath == "Contracts/AuditResponse.cs");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "FactSymbolProjectionUnavailable" or "NoRouteFlowEvidence");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "data-surface"
            && group.MatchKind == "fact-symbol"
            && group.RuleIds.Contains("combined.route-flow.fact-symbol-projection.v1")
            && group.SupportingRowIds.Contains(serializerRows[0].LogicRowId));
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "data-surface"
            && group.MatchKind == "fact-symbol"
            && group.RuleIds.Contains("combined.route-flow.fact-symbol-projection.v1")
            && group.SupportingRowIds.Contains(serializerRows[1].LogicRowId));
        Assert.Equal(
            serializerRows.Select(row => row.LogicRowId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            repeated.Report.LogicRows
                .Where(row => row.LogicKind == "serializer-contract"
                    && row.AttachmentKind == "fact-symbol-projection")
                .Select(row => row.LogicRowId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_serializer_contract_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var unrelatedDtoType = "Server.Contracts.AuditResponse";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31),
            SerializerContractFact(server, unrelatedDtoType, "InternalStatus", "System.String", "audit_status", "Contracts/AuditResponse.cs", 11)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.DoesNotContain(result.Report.LogicRows, row => row.LogicKind == "serializer-contract"
            && row.AttachmentKind == "fact-symbol-projection");
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "FactSymbolProjectionUnavailable");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Contains(gap.Limitations, limitation => limitation.Contains("combined.route-flow.fact-symbol-projection.v1", StringComparison.Ordinal));
        Assert.Equal("Contracts/AuditResponse.cs", gap.FilePath);
        Assert.Equal("server", gap.SourceLabel);
        Assert.Equal("abc123", gap.CommitSha);
        Assert.Equal("tracemap-milestone15", gap.ExtractorVersion);
        Assert.Contains("none could be connected to the selected route-flow path", gap.Message, StringComparison.Ordinal);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "FactSymbolProjectionUnavailable").GapId);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
    }

    [Fact]
    public async Task Route_flow_attaches_message_surfaces_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var publisher = "Server.OrderPublisher.Publish(System.Int32)";
        var unrelatedPublisher = "Server.OtherPublisher.Publish(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/publish", "/api/orders/{}/publish", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, publisher, "Controllers/OrdersController.cs", 15),
            MessageSurfaceFact(server, publisher, "message-stream", "publish", "orders.events", "Services/OrderPublisher.cs", 24),
            MessageSurfaceFact(server, unrelatedPublisher, "message-queue", "publish", "audit.jobs", "Services/OtherPublisher.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/publish",
            ToSurface: "message-stream"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/publish",
            ToSurface: "message-stream"));

        var surface = Assert.Single(result.Report.DependencySurfaces);
        Assert.Equal("message-stream", surface.SurfaceKind);
        Assert.Contains("orders.events", surface.DisplayName, StringComparison.Ordinal);
        Assert.Equal(surface.SurfaceId, Assert.Single(repeated.Report.DependencySurfaces).SurfaceId);
        Assert.Equal("combined.route-flow.dependency-surface.v1", surface.Evidence.RuleId);
        Assert.Contains(RuleIds.MessageSurfacePublish, surface.Evidence.SupportingRuleIds);
        Assert.DoesNotContain(result.Report.DependencySurfaces, item => item.SurfaceKind == "message-queue");

        var terminal = result.Report.FlowRows.Single(row => row.EdgeKind == "terminal-surface"
            && row.TargetSymbol?.Contains("orders.events", StringComparison.Ordinal) == true);
        Assert.Equal("terminal-surface", terminal.RowKind);
        Assert.Equal(RouteFlowClassifications.NeedsReviewStaticRouteFlow, terminal.Classification);
        Assert.DoesNotContain(result.Report.LogicRows, row => row.DisplayName.Contains("audit.jobs", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "DataSurfaceAttachmentMissing" or "NoRouteFlowEvidence");
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_message_surface_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var publisher = "Server.OrderPublisher.Publish(System.Int32)";
        var unrelatedPublisher = "Server.OtherPublisher.Publish(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/publish", "/api/orders/{}/publish", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, publisher, "Controllers/OrdersController.cs", 15),
            MessageSurfaceFact(server, unrelatedPublisher, "message-stream", "publish", "orders.events", "Services/OtherPublisher.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/publish",
            ToSurface: "message-stream"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/publish",
            ToSurface: "message-stream"));

        Assert.Empty(result.Report.DependencySurfaces);
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "DataSurfaceAttachmentMissing").GapId);
        Assert.DoesNotContain(result.Report.FlowRows, row => row.EdgeKind == "terminal-surface"
            && row.TargetSymbol?.Contains("orders.events", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_attaches_wcf_operation_surface_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var wcfClient = "Server.ServiceReference.RatingClient.Rate(System.Int32)";
        var unrelatedClient = "Server.ServiceReference.AuditClient.Rate(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/rate", "/api/orders/{}/rate", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, wcfClient, "Controllers/OrdersController.cs", 15),
            WcfSurfaceFact(server, wcfClient, "Rate", "Service References/Rating/Reference.cs", 24),
            WcfSurfaceFact(server, unrelatedClient, "Rate", "Service References/Audit/Reference.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/rate",
            ToSurface: "wcf-operation"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/rate",
            ToSurface: "wcf-operation"));

        var surface = Assert.Single(result.Report.DependencySurfaces);
        Assert.Equal("wcf-operation", surface.SurfaceKind);
        Assert.Equal("Rate", surface.DisplayName);
        Assert.Equal("combined.route-flow.dependency-surface.v1", surface.Evidence.RuleId);
        Assert.Contains(RuleIds.LegacyWcfMapping, surface.Evidence.SupportingRuleIds);
        Assert.Equal(surface.SurfaceId, Assert.Single(repeated.Report.DependencySurfaces).SurfaceId);
        Assert.Equal(surface.StableKey, Assert.Single(repeated.Report.DependencySurfaces).StableKey);
        Assert.StartsWith("surface-key-hash:", surface.StableKey, StringComparison.Ordinal);

        var terminal = Assert.Single(result.Report.FlowRows, row => row.EdgeKind == "terminal-surface");
        Assert.Equal("terminal-surface", terminal.RowKind);
        Assert.Contains(wcfClient, terminal.SourceSymbol, StringComparison.Ordinal);
        Assert.Contains("Rate", terminal.TargetSymbol!, StringComparison.Ordinal);
        Assert.Equal(terminal.RowId, Assert.Single(repeated.Report.FlowRows, row => row.EdgeKind == "terminal-surface").RowId);

        Assert.DoesNotContain(result.Report.FlowRows, row => row.SourceSymbol.Contains("AuditClient", StringComparison.Ordinal)
            || row.TargetSymbol?.Contains("AuditClient", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.DependencySurfaces, item => item.Evidence.FilePath == "Service References/Audit/Reference.cs");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "DataSurfaceAttachmentMissing" or "NoRouteFlowEvidence");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "dependency"
            && group.MatchKind == "dependency-surface"
            && group.SupportingRowIds.Contains(surface.SurfaceId));
    }

    [Fact]
    public async Task Route_flow_keeps_same_operation_wcf_surfaces_distinct_by_mapping_identity()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var ratingClient = "Server.ServiceReference.RatingClient.Rate(System.Int32)";
        var auditClient = "Server.ServiceReference.AuditClient.Rate(System.Int32)";
        var ratingMappingHash = FactFactory.Hash($"{ratingClient}:Rate", 32);
        var auditMappingHash = FactFactory.Hash($"{auditClient}:Rate", 32);

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/rate", "/api/orders/{}/rate", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, ratingClient, "Controllers/OrdersController.cs", 15),
            CallFact(server, controller, auditClient, "Controllers/OrdersController.cs", 16),
            WcfSurfaceFact(server, ratingClient, "Rate", "Service References/Rating/Reference.cs", 24),
            WcfSurfaceFact(server, auditClient, "Rate", "Service References/Audit/Reference.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/rate",
            ToSurface: "wcf-operation"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/rate",
            ToSurface: "wcf-operation"));

        Assert.Equal(2, result.Report.DependencySurfaces.Count);
        Assert.All(result.Report.DependencySurfaces, surface =>
        {
            Assert.Equal("wcf-operation", surface.SurfaceKind);
            Assert.Equal("Rate", surface.DisplayName);
            Assert.Equal("combined.route-flow.dependency-surface.v1", surface.Evidence.RuleId);
            Assert.StartsWith("surface-key-hash:", surface.StableKey, StringComparison.Ordinal);
        });
        Assert.Equal(2, result.Report.DependencySurfaces.Select(surface => surface.StableKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SafeMetadata.TryGetValue("shapeHash", out var value) && value == ratingMappingHash);
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SafeMetadata.TryGetValue("shapeHash", out var value) && value == auditMappingHash);
        Assert.Equal(
            result.Report.DependencySurfaces.Select(surface => surface.StableKey).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            repeated.Report.DependencySurfaces.Select(surface => surface.StableKey).OrderBy(value => value, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "DataSurfaceAttachmentMissing" or "NoRouteFlowEvidence");
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_wcf_operation_surface_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var service = "Server.OrderRatingService.Rate(System.Int32)";
        var unrelatedClient = "Server.ServiceReference.AuditClient.Rate(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/rate", "/api/orders/{}/rate", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 15),
            WcfSurfaceFact(server, unrelatedClient, "Rate", "Service References/Audit/Reference.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/rate",
            ToSurface: "wcf-operation"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/rate",
            ToSurface: "wcf-operation"));

        Assert.Empty(result.Report.DependencySurfaces);
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "DataSurfaceAttachmentMissing").GapId);
        Assert.DoesNotContain(result.Report.FlowRows, row => row.EdgeKind == "terminal-surface"
            && row.TargetSymbol?.Contains("Rate", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_attaches_remoting_endpoint_surface_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var configure = "Server.Legacy.RemotingHost.Configure()";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/remoting", "/api/orders/{}/remoting", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, configure, "Controllers/OrdersController.cs", 15),
            CallFact(server, configure, "System.Runtime.Remoting.RemotingConfiguration.Configure", "Services/RemotingHost.cs", 20),
            RemotingConfigureApiFact(server, "Services/RemotingHost.cs", 20, "App.config"),
            RemotingEndpointFact(server, null, "App.config", 24, "Server.Legacy.RemoteService", "abcdef1234567890"),
            RemotingEndpointFact(server, null, "Other.config", 31, "Server.Legacy.OtherRemoteService", "fedcba0987654321")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/remoting",
            ToSurface: "remoting-endpoint"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/remoting",
            ToSurface: "remoting-endpoint"));

        var surface = Assert.Single(result.Report.DependencySurfaces);
        Assert.Equal("remoting-endpoint", surface.SurfaceKind);
        Assert.Equal("objectUri-abcdef12", surface.DisplayName);
        Assert.Equal("combined.route-flow.dependency-surface.v1", surface.Evidence.RuleId);
        Assert.Contains(RuleIds.LegacyRemotingConfig, surface.Evidence.SupportingRuleIds);
        Assert.Equal(surface.SurfaceId, Assert.Single(repeated.Report.DependencySurfaces).SurfaceId);
        Assert.Equal(surface.StableKey, Assert.Single(repeated.Report.DependencySurfaces).StableKey);
        Assert.StartsWith("surface-key-hash:", surface.StableKey, StringComparison.Ordinal);
        Assert.Equal("objectUri-abcdef1234567890", surface.SafeMetadata["shapeHash"]);

        var terminal = Assert.Single(result.Report.FlowRows, row => row.EdgeKind == "terminal-surface"
            && string.Equals(row.SourceSymbol, configure, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains("objectUri-abcdef12", StringComparison.Ordinal) == true);
        Assert.Equal("terminal-surface", terminal.RowKind);
        Assert.Contains(configure, terminal.SourceSymbol, StringComparison.Ordinal);
        Assert.Contains("objectUri-abcdef12", terminal.TargetSymbol!, StringComparison.Ordinal);
        Assert.Equal(terminal.RowId, Assert.Single(repeated.Report.FlowRows, row => row.EdgeKind == "terminal-surface"
            && string.Equals(row.SourceSymbol, configure, StringComparison.Ordinal)
            && row.TargetSymbol?.Contains("objectUri-abcdef12", StringComparison.Ordinal) == true).RowId);

        Assert.DoesNotContain(result.Report.FlowRows, row => row.SourceSymbol.Contains("OtherRemotingHost", StringComparison.Ordinal)
            || row.TargetSymbol?.Contains("OtherRemoteService", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.DependencySurfaces, item => item.Evidence.FilePath == "Other.config");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "DataSurfaceAttachmentMissing" or "NoRouteFlowEvidence");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "dependency"
            && group.MatchKind == "dependency-surface"
            && group.SupportingRowIds.Contains(surface.SurfaceId));
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_remoting_endpoint_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var service = "Server.OrderRatingService.Rate(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/remoting", "/api/orders/{}/remoting", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 15),
            RemotingEndpointFact(server, null, "Other.config", 31, "Server.Legacy.OtherRemoteService", "fedcba0987654321")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/remoting",
            ToSurface: "remoting-endpoint"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/remoting",
            ToSurface: "remoting-endpoint"));

        Assert.Empty(result.Report.DependencySurfaces);
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "DataSurfaceAttachmentMissing").GapId);
        Assert.DoesNotContain(result.Report.FlowRows, row => row.EdgeKind == "terminal-surface"
            && row.TargetSymbol?.Contains("objectUri-fedcba09", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_attaches_asmx_client_surface_only_from_selected_static_path()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var soapClientType = "Server.LegacyRatingClient";
        var soapClient = "Server.LegacyRatingClient.Rate";
        var unrelatedClientType = "Server.OtherLegacyClient";
        var unrelatedClient = "Server.OtherLegacyClient.Rate";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/rate", "/api/orders/{}/rate", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, soapClient, "Controllers/OrdersController.cs", 15),
            AsmxSurfaceFact(server, soapClientType, soapClient, FactTypes.AsmxClientOperationDeclared, RuleIds.LegacyAsmxClient, "asmx-client", "Rate", "Services/RatingReference.cs", 24),
            AsmxSurfaceFact(server, unrelatedClientType, unrelatedClient, FactTypes.AsmxClientOperationDeclared, RuleIds.LegacyAsmxClient, "asmx-client", "Rate", "Services/OtherRatingReference.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/rate",
            ToSurface: "asmx-client"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/rate",
            ToSurface: "asmx-client"));

        var surface = Assert.Single(result.Report.DependencySurfaces);
        Assert.Equal("asmx-client", surface.SurfaceKind);
        Assert.Equal("Rate", surface.DisplayName);
        Assert.Equal("combined.route-flow.dependency-surface.v1", surface.Evidence.RuleId);
        Assert.Contains(RuleIds.LegacyAsmxClient, surface.Evidence.SupportingRuleIds);
        Assert.Equal(surface.SurfaceId, Assert.Single(repeated.Report.DependencySurfaces).SurfaceId);

        var terminal = Assert.Single(result.Report.FlowRows, row => row.EdgeKind == "terminal-surface");
        Assert.Equal("terminal-surface", terminal.RowKind);
        Assert.Contains(soapClient, terminal.SourceSymbol, StringComparison.Ordinal);
        Assert.Contains("Rate", terminal.TargetSymbol!, StringComparison.Ordinal);
        Assert.Equal(terminal.RowId, Assert.Single(repeated.Report.FlowRows, row => row.EdgeKind == "terminal-surface").RowId);

        Assert.DoesNotContain(result.Report.FlowRows, row => row.SourceSymbol.Contains("OtherLegacyClient", StringComparison.Ordinal)
            || row.TargetSymbol?.Contains("OtherLegacyClient", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.DependencySurfaces, item => item.Evidence.FilePath == "Services/OtherRatingReference.cs");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "DataSurfaceAttachmentMissing" or "NoRouteFlowEvidence");
        Assert.Contains(result.Report.ContextGroups!, group => group.GroupKind == "dependency"
            && group.MatchKind == "dependency-surface"
            && group.SupportingRowIds.Contains(surface.SurfaceId));
    }

    [Fact]
    public async Task Route_flow_does_not_infer_adjacent_asmx_client_surface_without_selected_join()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Post(System.Int32)";
        var service = "Server.OrderRatingService.Rate(System.Int32)";
        var unrelatedClientType = "Server.OtherLegacyClient";
        var unrelatedClient = "Server.OtherLegacyClient.Rate";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/orders/{id}/rate", "/api/orders/{}/rate", controller, "Controllers/OrdersController.cs", 10, EvidenceTiers.Tier1Semantic),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 15),
            AsmxSurfaceFact(server, unrelatedClientType, unrelatedClient, FactTypes.AsmxClientOperationDeclared, RuleIds.LegacyAsmxClient, "asmx-client", "Rate", "Services/OtherRatingReference.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "POST /api/orders/{id}/rate",
            ToSurface: "asmx-client"));
        var repeated = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow-repeat"),
            Route: "POST /api/orders/{id}/rate",
            ToSurface: "asmx-client"));

        Assert.Empty(result.Report.DependencySurfaces);
        var gap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DataSurfaceAttachmentMissing");
        Assert.Equal("combined.route-flow.gap.v1", gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal(gap.GapId, Assert.Single(repeated.Report.Gaps, item => item.GapKind == "DataSurfaceAttachmentMissing").GapId);
        Assert.DoesNotContain(result.Report.FlowRows, row => row.EdgeKind == "terminal-surface"
            && row.TargetSymbol?.Contains("Rate", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
    }


    [Fact]
    public async Task Route_flow_json_file_output_and_classification_filter_empty_rows_yields_selector_gap()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp);
        var outputPath = Path.Combine(temp.Path, "route-flow.json");

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            outputPath,
            Format: "json",
            Route: "GET /api/orders/%7Bid%7D",
            ToSurface: "sql-query",
            Classification: RouteFlowClassifications.NoRouteFlowEvidence));

        Assert.Null(result.MarkdownPath);
        Assert.Equal(outputPath, result.JsonPath);
        Assert.Empty(result.Report.FlowRows);
        Assert.Empty(result.Report.DependencySurfaces);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch");
        Assert.Null(result.Report.Query.SelectorTrace);
        var json = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("\"reportType\": \"route-flow\"", json);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Route_flow_classification_filter_empty_rows_preserves_matched_selector_trace()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp);

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query",
            Classification: RouteFlowClassifications.NoRouteFlowEvidence));

        Assert.Empty(result.Report.FlowRows);
        Assert.Empty(result.Report.LogicRows);
        Assert.Empty(result.Report.DependencySurfaces);
        Assert.NotEmpty(result.Report.Gaps);
        Assert.NotNull(result.Report.Query.SelectorTrace);
        Assert.Equal("route", result.Report.Query.SelectorTrace!.SelectorKind);
        Assert.NotEmpty(result.Report.Query.SelectorTrace.SupportingFactIds);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
    }

    [Fact]
    public async Task Route_flow_query_omits_raw_route_selector_values()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp);
        const string rawSelector = "GET https://example.test/api/orders/{id}?token=secret-token";

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: rawSelector,
            ToSurface: "sql-query"));

        Assert.Equal("GET /api/orders/{}", result.Report.Query.Route);
        Assert.NotNull(result.Report.Query.SelectorTrace);
        Assert.Equal("route", result.Report.Query.SelectorTrace!.SelectorKind);
        Assert.Equal("GET /api/orders/{}", result.Report.Query.SelectorTrace.SafeNormalizedKey);
        Assert.Equal("normalized-redacted", result.Report.Query.SelectorTrace.RedactionState);
        Assert.Contains("Selector values are normalized or redacted before rendering.", result.Report.Query.SelectorTrace.Limitations);
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "route-flow", "route-flow-report.json"));
        Assert.DoesNotContain(rawSelector, json, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Route_flow_selector_trace_redacts_sensitive_normalized_keys()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateSensitiveRouteCombinedIndexAsync(temp);

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/token/{id}"));

        Assert.NotNull(result.Report.Query.SelectorTrace);
        Assert.Equal("route", result.Report.Query.SelectorTrace!.SelectorKind);
        Assert.StartsWith("redacted-hash:", result.Report.Query.SelectorTrace.SafeNormalizedKey, StringComparison.Ordinal);
        Assert.StartsWith("redacted-hash:", result.Report.Query.SelectorTrace.SafeSelector, StringComparison.Ordinal);
        Assert.Equal("redacted", result.Report.Query.SelectorTrace.RedactionState);
    }

    [Fact]
    public async Task Route_flow_rejects_single_language_index()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "server.sqlite");
        var manifest = Manifest("server", "tracemap-milestone15");
        SqliteIndexWriter.Write(indexPath, manifest, [
            RouteFact(manifest, "GET", "/api/orders/{id}", "/api/orders/{}", "Server.OrdersController.Get(System.Int32)", "Controllers/OrdersController.cs", 10)
        ]);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CombinedRouteFlowReporter.BuildReportAsync(new CombinedRouteFlowOptions(
            indexPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}")));
        Assert.Contains("requires a combined index", error.Message);
    }

    [Fact]
    public async Task Route_flow_identity_reduced_coverage_gap_reduces_report_coverage()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp, serverBuildStatus: "Failed");

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Equal("ReducedCoverage", result.Report.ReportCoverage);
        Assert.Equal("ReducedCoverage", result.Report.Summary.ReportCoverage);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
        Assert.NotEqual(RouteFlowClassifications.StrongStaticRouteFlow, result.Report.Summary.Classification);
        Assert.NotEqual(RouteFlowClassifications.NoRouteFlowEvidence, result.Report.Summary.Classification);
        Assert.NotNull(result.Report.Query.SelectorTrace);
        Assert.Equal("ReducedCoverage", result.Report.Query.SelectorTrace!.Coverage);
        Assert.Contains("Reduced coverage caps selector-trace conclusions.", result.Report.Query.SelectorTrace.Limitations);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ReducedCoverage" && gap.SourceLabel == "server");
    }

    [Fact]
    public async Task Route_flow_cli_exit_code_follows_summary_mapping()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp);
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync([
            "route-flow",
            "--index", combinedPath,
            "--out", Path.Combine(temp.Path, "route-flow"),
            "--route", "GET /api/orders/{id}",
            "--to-surface", "sql-query",
            "--exit-code"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Classification:", output.ToString());
        Assert.DoesNotContain(temp.Path, output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Route_flow_cli_validation_error_takes_precedence_over_exit_code_mapping()
    {
        using var temp = new TempDirectory();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync([
            "route-flow",
            "--out", Path.Combine(temp.Path, "route-flow"),
            "--route", "GET /api/orders/{id}",
            "--exit-code"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("route-flow requires --index", error.ToString());
        Assert.DoesNotContain("Classification:", output.ToString());
        Assert.DoesNotContain(temp.Path, error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Route_flow_optional_projection_tables_emit_scoped_gaps_when_rows_cannot_join_selected_path()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateUnjoinableProjectionCombinedIndexAsync(temp);

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}"));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "ArgumentProjectionUnavailable");
        // The fixture includes same-source fact-symbol context that cannot join through selected
        // source-local route-flow symbols.
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "FactSymbolProjectionUnavailable");
        // Unsupported fact-symbol shapes that do join selected symbols must be reported as skipped
        // context rather than rendered as projection rows.
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "FactSymbolUnsupportedTypeSkipped");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "NoRouteFlowEvidence");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "ExtractorUnavailable");
        Assert.All(result.Report.Gaps.Where(gap => gap.GapKind is "ArgumentProjectionUnavailable" or "FactSymbolProjectionUnavailable" or "FactSymbolUnsupportedTypeSkipped"),
            gap => Assert.Equal(RouteFlowClassifications.NeedsReviewStaticRouteFlow, gap.Classification));
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.RuleId is "combined.route-flow.argument-projection.v1" or "combined.route-flow.fact-symbol-projection.v1");
    }

    [Fact]
    public async Task Route_flow_fact_symbol_projection_requires_selected_source_symbol_identity()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateUnjoinableProjectionCombinedIndexAsync(temp);

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}"));

        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query");
        var factSymbolGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "FactSymbolProjectionUnavailable");
        Assert.True(factSymbolGap.SupportingFactIds.Count > 0);
        Assert.Equal("server", factSymbolGap.SourceLabel);
        Assert.Equal("abc123", factSymbolGap.CommitSha);
        Assert.Equal("Infrastructure/MisleadingTarget.cs", factSymbolGap.FilePath);
        Assert.Equal("tracemap-milestone15", factSymbolGap.ExtractorVersion);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "FactSymbolProjectionUnavailable"
            && gap.RuleId == "combined.route-flow.gap.v1");
        Assert.DoesNotContain(result.Report.LogicRows, row => row.AttachmentKind == "fact-symbol-projection");
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.FilePath == "Infrastructure/MisleadingTarget.cs");
        Assert.DoesNotContain(result.Report.ContextGroups ?? [], group => group.RuleIds.Contains("combined.route-flow.fact-symbol-projection.v1"));
    }

    [Fact]
    public async Task Route_flow_missing_optional_projection_tables_emit_schema_gaps_without_silent_projection_loss()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp);

        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = combinedPath
        }.ToString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                drop table combined_argument_flows;
                drop table combined_fact_symbols;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "SchemaMissing"
            && gap.Message.Contains("combined_argument_flows", StringComparison.Ordinal));
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "SchemaMissing"
            && gap.Message.Contains("combined_fact_symbols", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Report.LogicRows, row => row.Evidence.RuleId is "combined.route-flow.argument-projection.v1" or "combined.route-flow.fact-symbol-projection.v1");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind is "ArgumentProjectionUnavailable" or "FactSymbolProjectionUnavailable");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "SelectorNoMatch");
        Assert.Equal("ReducedCoverage", result.Report.ReportCoverage);
        Assert.Equal(RouteFlowClassifications.UnknownAnalysisGap, result.Report.Summary.Classification);
    }

    [Fact]
    public void Route_flow_rule_catalog_resolves_projection_rule_ids()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var ruleId in new[]
        {
            "combined.route-flow.selector.v1",
            "combined.route-flow.entry.v1",
            "combined.route-flow.path.v1",
            "combined.route-flow.interface-bridge.v1",
            "combined.route-flow.logic-surface.v1",
            "combined.route-flow.dependency-surface.v1",
            "combined.route-flow.argument-projection.v1",
            "combined.route-flow.fact-symbol-projection.v1",
            "combined.route-flow.classification.v1",
            "combined.route-flow.gap.v1",
            "combined.route-flow.redaction.v1",
            "combined.route-flow.report.v1"
        })
        {
            Assert.Contains($"- id: {ruleId}", catalog);
        }

        Assert.DoesNotContain("- id: route.flow.", catalog, StringComparison.Ordinal);
        Assert.Contains("- MissingMethodSymbolBridge gap", catalog);
        Assert.Contains("- MissingCallEdge gap", catalog);
        Assert.Contains("- DataSurfaceAttachmentMissing gap", catalog);
        Assert.Contains("- FactSymbolUnsupportedTypeSkipped gap", catalog);
        Assert.Contains("- TraversalBounds gap", catalog);
        Assert.Contains("- route-flow-report.json", catalog);
    }

    [Fact]
    public async Task Route_flow_emitted_rule_ids_resolve_to_rule_catalog()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _, _) = await CreateRouteFlowCombinedIndexAsync(temp);
        var result = await CombinedRouteFlowReporter.WriteAsync(new CombinedRouteFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "route-flow"),
            Route: "GET /api/orders/{id}",
            ToSurface: "sql-query"));
        var catalogIds = RuleCatalogIds();

        var emittedRuleIds = EmittedRouteFlowRuleIds(result.Report)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var missing = emittedRuleIds
            .Where(ruleId => !catalogIds.Contains(ruleId))
            .ToArray();

        Assert.NotEmpty(emittedRuleIds);
        Assert.Empty(missing);
    }

    private static async Task<(string CombinedPath, string Controller, string Repository)> CreateRouteFlowCombinedIndexAsync(TempDirectory temp, string serverBuildStatus = "Succeeded")
    {
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15", buildStatus: serverBuildStatus);
        var clientMethod = "Client.OrderService.loadOrder(System.Int32)";
        var controller = "Server.OrdersController.Get(System.Int32)";
        var service = "Server.IOrderService.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5, clientMethod)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, service, "Controllers/OrdersController.cs", 14),
            ArgumentPassedFact(server, controller, service, "apiSecret", "apiSecret", "System.String", "Controllers/OrdersController.cs", 14),
            CallFact(server, service, repository, "Services/OrderService.cs", 21),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31, attachSymbol: true),
            ConnectionStringFact(server, repository, "Infrastructure/OrderRepository.cs", 32)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));
        return (combinedPath, controller, repository);
    }

    private static async Task<string> CreateSensitiveRouteCombinedIndexAsync(TempDirectory temp)
    {
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        const string controller = "Server.AuthController.Get(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/token/{id}", "/api/token/{}", controller, "Controllers/AuthController.cs", 10)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));
        return combinedPath;
    }

    private static async Task<string> CreateUnjoinableProjectionCombinedIndexAsync(TempDirectory temp)
    {
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone15");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        var unrelatedCaller = "Server.Unrelated.Start(System.Int32)";
        var unrelatedCallee = "Server.Unrelated.Finish(System.Int32)";

        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(server, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, repository, "Infrastructure/OrderRepository.cs", 31),
            ArgumentPassedFact(server, unrelatedCaller, unrelatedCallee, "id", "id", "System.Int32", "Services/Unrelated.cs", 20),
            QueryPatternFact(server, unrelatedCallee, "Infrastructure/UnrelatedRepository.cs", 41, attachSymbol: true),
            QueryPatternFact(server, unrelatedCallee, "Infrastructure/MisleadingTarget.cs", 42, attachSymbol: true, targetSymbol: repository, attachTargetSymbol: true)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));
        return combinedPath;
    }

    private static ScanManifest Manifest(string repo, string scannerVersion, string buildStatus = "Succeeded")
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            null,
            "main",
            "abc123",
            scannerVersion,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            "Level1SemanticAnalysis",
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

    private static CodeFact RouteFact(ScanManifest manifest, string method, string template, string key, string methodSymbol, string file, int line, string evidenceTier = EvidenceTiers.Tier3SyntaxOrTextual)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            evidenceTier,
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

    private static CodeFact RouteFactWithoutMethodSymbol(ScanManifest manifest, string method, string template, string key, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
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

    private static CodeFact SymbolRelationshipFact(
        ScanManifest manifest,
        string implementationMethodSymbol,
        string interfaceMethodSymbol,
        string file,
        int line,
        string evidenceTier = EvidenceTiers.Tier1Semantic,
        string relationshipSource = "InterfaceImplementation")
    {
        return FactFactory.Create(
            manifest,
            FactTypes.SymbolRelationship,
            RuleIds.CSharpSemanticSymbolRelationship,
            evidenceTier,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: implementationMethodSymbol,
            targetSymbol: interfaceMethodSymbol,
            contractElement: "ImplementsInterfaceMember",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["relationshipKind"] = "ImplementsInterfaceMember",
                ["relationshipSource"] = relationshipSource,
                ["sourceSymbolDisplayName"] = implementationMethodSymbol,
                ["sourceSymbolId"] = implementationMethodSymbol,
                ["sourceSymbolKind"] = "Method",
                ["sourceSymbolLanguage"] = "csharp",
                ["targetSymbolDisplayName"] = interfaceMethodSymbol,
                ["targetSymbolId"] = interfaceMethodSymbol,
                ["targetSymbolKind"] = "Method",
                ["targetSymbolLanguage"] = "csharp"
            });
    }

    private static CodeFact RuntimeEvidenceFact(
        ScanManifest manifest,
        string factType,
        string evidenceKind,
        string sourceSymbol,
        string targetSymbol,
        string file,
        int line)
    {
        return FactFactory.Create(
            manifest,
            factType,
            RuleIds.CSharpSemanticRuntimeEvidence,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: targetSymbol,
            contractElement: evidenceKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["evidenceKind"] = evidenceKind,
                ["sourceSymbolDisplayName"] = sourceSymbol,
                ["sourceSymbolId"] = sourceSymbol,
                ["targetSymbolDisplayName"] = targetSymbol,
                ["targetSymbolId"] = targetSymbol
            });
    }

    private static CodeFact CallFact(ScanManifest manifest, string caller, string callee, string file, int line, string evidenceTier = EvidenceTiers.Tier1Semantic, string targetSymbolKind = "Method")
    {
        return FactFactory.Create(
            manifest,
            FactTypes.CallEdge,
            RuleIds.CSharpSemanticCallGraph,
            evidenceTier,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: caller,
            targetSymbol: callee,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["callKind"] = "method",
                ["sourceSymbolDisplayName"] = caller,
                ["sourceSymbolId"] = caller,
                ["sourceSymbolKind"] = "Method",
                ["sourceSymbolLanguage"] = "csharp",
                ["targetSymbolDisplayName"] = callee,
                ["targetSymbolId"] = callee,
                ["targetSymbolKind"] = targetSymbolKind,
                ["targetSymbolLanguage"] = "csharp"
            });
    }

    private static CodeFact LegacyDataEntityFact(ScanManifest manifest, string? sourceSymbol, string displayName, string file, int line, string? targetSymbol = null)
    {
        return LegacyDataFact(
            manifest,
            FactTypes.LegacyDataEntityDeclared,
            sourceSymbol,
            displayName,
            file,
            line,
            "entity",
            "conceptual",
            "4d20bb6c8ed47712",
            "ldm:route-flow-model-key",
            targetSymbol);
    }

    private static CodeFact LegacyDataStorageObjectFact(
        ScanManifest manifest,
        string? sourceSymbol,
        string displayName,
        string file,
        int line,
        string displayNameHash = "5c31ea90a85f4d62",
        string stableModelKey = "ldm:route-flow-storage-key",
        string? targetSymbol = null)
    {
        return LegacyDataFact(
            manifest,
            FactTypes.LegacyDataStorageObjectDeclared,
            sourceSymbol,
            displayName,
            file,
            line,
            "storage-object",
            "storage",
            displayNameHash,
            stableModelKey,
            targetSymbol);
    }

    private static CodeFact LegacyDataFact(
        ScanManifest manifest,
        string factType,
        string? sourceSymbol,
        string displayName,
        string file,
        int line,
        string modelKind,
        string descriptorRole,
        string displayNameHash,
        string stableModelKey,
        string? targetSymbol = null)
    {
        return FactFactory.Create(
            manifest,
            factType,
            RuleIds.LegacyDataDbml,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: targetSymbol ?? displayName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverageLabel"] = "reduced",
                ["descriptorRole"] = descriptorRole,
                ["displayName"] = displayName,
                ["displayNameHash"] = displayNameHash,
                ["metadataFormat"] = "dbml",
                ["metadataHash"] = "metadata-hash",
                ["metadataKind"] = "Dbml",
                ["modelKind"] = modelKind,
                ["stableModelKey"] = stableModelKey,
                ["targetSymbolId"] = targetSymbol ?? displayName
            });
    }

    private static CodeFact ObjectCreationFact(ScanManifest manifest, string caller, string createdType, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ObjectCreated,
            RuleIds.CSharpSemanticObjectCreation,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: caller,
            targetSymbol: createdType,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["assignedTo"] = "repository",
                ["callerSymbolId"] = caller,
                ["callerSymbolDisplayName"] = caller,
                ["callerSymbolKind"] = "Method",
                ["callerSymbolLanguage"] = "csharp",
                ["createdType"] = createdType,
                ["createdTypeName"] = "OrderRepository",
                ["constructorSymbol"] = $"{createdType}.#ctor()",
                ["creationKind"] = "SemanticObjectCreation",
                ["sourceSymbolId"] = caller,
                ["sourceSymbolDisplayName"] = caller,
                ["sourceSymbolKind"] = "Method",
                ["sourceSymbolLanguage"] = "csharp",
                ["targetSymbolId"] = createdType,
                ["targetSymbolDisplayName"] = createdType,
                ["targetSymbolKind"] = "Type",
                ["targetSymbolLanguage"] = "csharp"
            });
    }

    private static CodeFact ObjectShapeFact(
        ScanManifest manifest,
        string sourceSymbol,
        string objectKind,
        string file,
        int line,
        string shapeHash,
        IReadOnlyList<string> fieldNames)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ObjectShapeInferred,
            RuleIds.CSharpSyntaxObjectShape,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: objectKind,
            contractElement: objectKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["expressionHash"] = FactFactory.Hash($"{sourceSymbol}:{shapeHash}", 32),
                ["fieldCount"] = fieldNames.Count.ToString(),
                ["fieldNamesHash"] = FactFactory.Hash(string.Join("|", fieldNames), 32),
                ["objectKind"] = objectKind,
                ["shapeHash"] = shapeHash,
                ["sourceSymbolDisplayName"] = sourceSymbol,
                ["sourceSymbolId"] = sourceSymbol,
                ["sourceSymbolKind"] = "Method",
                ["sourceSymbolLanguage"] = "csharp",
                ["targetSymbolDisplayName"] = objectKind,
                ["targetSymbolId"] = objectKind,
                ["targetSymbolKind"] = "ObjectShape",
                ["targetSymbolLanguage"] = "csharp"
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

    private static CodeFact QueryPatternFact(
        ScanManifest manifest,
        string? sourceSymbol,
        string file,
        int line,
        bool attachSymbol = false,
        string targetSymbol = "orders",
        bool attachTargetSymbol = false,
        string tableName = "orders",
        string columnNames = "id;status",
        string queryShapeHash = "shape123")
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["operationName"] = "SELECT",
            ["tableName"] = tableName,
            ["columnNames"] = columnNames,
            ["sqlSourceKind"] = "literal-string",
            ["queryShapeHash"] = queryShapeHash
        };
        if (attachSymbol && sourceSymbol is not null)
        {
            properties["sourceSymbolId"] = sourceSymbol;
            properties["sourceSymbolDisplayName"] = sourceSymbol;
            properties["sourceSymbolKind"] = "Method";
            properties["sourceSymbolLanguage"] = "csharp";
        }

        if (attachTargetSymbol)
        {
            properties["targetSymbolId"] = targetSymbol;
            properties["targetSymbolDisplayName"] = targetSymbol;
            properties["targetSymbolKind"] = "Method";
            properties["targetSymbolLanguage"] = "csharp";
        }

        return FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: targetSymbol,
            properties: properties);
    }

    private static CodeFact ConnectionStringFact(ScanManifest manifest, string sourceSymbol, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ConnectionStringDeclared,
            RuleIds.ConfigKey,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: "ConnectionStrings:Orders",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["configKey"] = "ConnectionStrings:Orders",
                ["connectionName"] = "Orders",
                ["connectionStringHash"] = "conn-shape-hash",
                ["sourceSymbolId"] = sourceSymbol,
                ["sourceSymbolDisplayName"] = sourceSymbol,
                ["sourceSymbolKind"] = "Method",
                ["sourceSymbolLanguage"] = "csharp",
                ["value"] = "Server=private;Password=secret;"
            });
    }

    private static CodeFact PackageConfigFact(
        ScanManifest manifest,
        string sourceSymbol,
        string factType,
        string ruleId,
        string? packageName,
        string? configKey,
        string file,
        int line)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceSymbolId"] = sourceSymbol,
            ["sourceSymbolDisplayName"] = sourceSymbol,
            ["sourceSymbolKind"] = "Method",
            ["sourceSymbolLanguage"] = "csharp",
            ["surfaceKind"] = "package-config"
        };
        if (!string.IsNullOrWhiteSpace(packageName))
        {
            properties["packageName"] = packageName!;
            properties["ecosystem"] = "nuget";
            properties["manifestKind"] = "PackageReference";
            properties["version"] = "1.2.3";
        }

        if (!string.IsNullOrWhiteSpace(configKey))
        {
            if (factType == FactTypes.ConnectionStringDeclared)
            {
                properties["connectionName"] = configKey!;
            }
            else if (factType == FactTypes.ConfigBinding)
            {
                properties["boundType"] = "Server.Options";
                properties["mappingKind"] = "ConfigBinding";
                properties["sectionName"] = configKey!;
            }
            else
            {
                properties["configKey"] = configKey!;
            }
        }

        return FactFactory.Create(
            manifest,
            factType,
            ruleId,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: packageName ?? configKey,
            contractElement: packageName ?? configKey,
            properties: properties);
    }

    private static CodeFact BoundaryFact(
        ScanManifest manifest,
        string sourceSymbol,
        string factType,
        string boundaryKind,
        string file,
        int line,
        string? callbackBoundaryKind = null,
        string? asyncOperationKind = null)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["boundaryKind"] = boundaryKind,
            ["sourceSymbolDisplayName"] = sourceSymbol,
            ["sourceSymbolId"] = sourceSymbol,
            ["sourceSymbolKind"] = "Method",
            ["sourceSymbolLanguage"] = "csharp"
        };
        if (!string.IsNullOrWhiteSpace(callbackBoundaryKind))
        {
            properties["callbackBoundaryKind"] = callbackBoundaryKind!;
            properties["callbackExpressionHash"] = FactFactory.Hash($"{sourceSymbol}:{boundaryKind}:callback", 32);
            properties["callbackExpressionKind"] = "LambdaExpression";
        }

        if (!string.IsNullOrWhiteSpace(asyncOperationKind))
        {
            properties["asyncOperationKind"] = asyncOperationKind!;
        }

        return FactFactory.Create(
            manifest,
            factType,
            RuleIds.CSharpSemanticFlowBoundary,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: boundaryKind,
            contractElement: boundaryKind,
            properties: properties);
    }

    private static CodeFact GuardFact(
        ScanManifest manifest,
        string sourceSymbol,
        string branchKind,
        string feasibilityKind,
        string checkedSymbol,
        string file,
        int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.BranchFeasibility,
            RuleIds.CSharpSemanticRuntimeEvidence,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: checkedSymbol,
            contractElement: branchKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["branchKind"] = branchKind,
                ["checkedSymbol"] = checkedSymbol,
                ["comparisonOperator"] = feasibilityKind == "NullCheckEquals" ? "==" : "!=",
                ["conditionExpressionHash"] = FactFactory.Hash($"{sourceSymbol}:{checkedSymbol}:{feasibilityKind}", 32),
                ["conditionExpressionKind"] = "IdentifierName",
                ["evidenceKind"] = "BranchFeasibility",
                ["feasibilityKind"] = feasibilityKind,
                ["sourceSymbolDisplayName"] = sourceSymbol,
                ["sourceSymbolId"] = sourceSymbol,
                ["sourceSymbolKind"] = "Method",
                ["sourceSymbolLanguage"] = "csharp"
            });
    }

    private static CodeFact SerializerContractFact(
        ScanManifest manifest,
        string containingType,
        string memberName,
        string memberType,
        string contractName,
        string file,
        int line)
    {
        var memberSymbol = $"{containingType}.{memberName}";
        return FactFactory.Create(
            manifest,
            FactTypes.SerializerContractMember,
            RuleIds.CSharpSemanticRuntimeEvidence,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: containingType,
            targetSymbol: memberSymbol,
            contractElement: contractName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["attributeName"] = "System.Text.Json.Serialization.JsonPropertyNameAttribute",
                ["containingType"] = containingType,
                ["contractName"] = contractName,
                ["evidenceKind"] = "SerializerContractMember",
                ["memberName"] = memberName,
                ["memberSymbol"] = memberSymbol,
                ["memberType"] = memberType,
                ["sourceSymbolDisplayName"] = containingType,
                ["sourceSymbolId"] = containingType,
                ["sourceSymbolKind"] = "Type",
                ["sourceSymbolLanguage"] = "csharp",
                ["targetSymbolDisplayName"] = memberSymbol,
                ["targetSymbolId"] = memberSymbol,
                ["targetSymbolKind"] = "Property",
                ["targetSymbolLanguage"] = "csharp"
            });
    }

    private static CodeFact MessageSurfaceFact(
        ScanManifest manifest,
        string sourceSymbol,
        string surfaceKind,
        string operationDirection,
        string destination,
        string file,
        int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.MessagePublisherSurface,
            RuleIds.MessageSurfacePublish,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: destination,
            contractElement: destination,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["destinationIdentityStatus"] = "static",
                ["frameworkFamily"] = "test-message",
                ["normalizedDestinationKey"] = destination,
                ["operationDirection"] = operationDirection,
                ["operationKind"] = operationDirection,
                ["safeMetadataHash"] = FactFactory.Hash($"{surfaceKind}:{destination}:metadata", 32),
                ["sourceSymbolDisplayName"] = sourceSymbol,
                ["sourceSymbolId"] = sourceSymbol,
                ["sourceSymbolKind"] = "Method",
                ["sourceSymbolLanguage"] = "csharp",
                ["stableMessageSurfaceKey"] = FactFactory.Hash($"{surfaceKind}:{destination}", 32),
                ["surfaceKind"] = surfaceKind
            });
    }

    private static CodeFact AsmxSurfaceFact(
        ScanManifest manifest,
        string sourceSymbol,
        string targetSymbol,
        string factType,
        string ruleId,
        string surfaceKind,
        string operationName,
        string file,
        int line)
    {
        return FactFactory.Create(
            manifest,
            factType,
            ruleId,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: targetSymbol,
            contractElement: operationName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["clientName"] = "RatingSoapClient",
                ["coverageLabel"] = "syntax-asmx-client-operation",
                ["operationName"] = operationName,
                ["sourceSymbolDisplayName"] = sourceSymbol,
                ["sourceSymbolId"] = sourceSymbol,
                ["sourceSymbolKind"] = "Type",
                ["sourceSymbolLanguage"] = "csharp",
                ["surfaceKind"] = surfaceKind
            });
    }

    private static CodeFact WcfSurfaceFact(
        ScanManifest manifest,
        string clientOperationSymbol,
        string operationName,
        string file,
        int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.WcfServiceReferenceMapping,
            RuleIds.LegacyWcfMapping,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: clientOperationSymbol,
            targetSymbol: $"IRatingService.{operationName}",
            contractElement: operationName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["mappingHash"] = FactFactory.Hash($"{clientOperationSymbol}:{operationName}", 32),
                ["mappingKind"] = "GeneratedClientToMetadataOperation",
                ["normalizedOperationName"] = operationName,
                ["supportingFactIds"] = "wcf-client,wcf-operation"
            });
    }

    private static CodeFact RemotingEndpointFact(
        ScanManifest manifest,
        string? sourceSymbol,
        string file,
        int line,
        string typeName,
        string objectUriHash)
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
                ["coverage"] = "static-xml-config",
                ["limitation"] = "Static Remoting config service evidence only; runtime hosting, activation, reachability, deployment, and production usage are not proven.",
                ["objectUriHash"] = objectUriHash,
                ["registrationKind"] = "well-known-service",
                ["sourceKind"] = "config",
                ["typeName"] = typeName
            });
    }

    private static CodeFact RemotingConfigureApiFact(
        ScanManifest manifest,
        string file,
        int line,
        string configFileName)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingApiUsageDeclared,
            RuleIds.LegacyRemotingRegistration,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", ScannerVersions.LegacyRemotingExtractor),
            targetSymbol: "System.Runtime.Remoting.RemotingConfiguration.Configure",
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

    private static async Task<string> CombinedIndexFingerprintAsync(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var parts = new List<string>
        {
            $"integrity:{await ScalarAsync(connection, "pragma integrity_check;")}",
            $"schema:{await ScalarAsync(connection, "select group_concat(type || ':' || name || ':' || coalesce(sql, ''), char(10)) from sqlite_master where name not like 'sqlite_%' order by type, name;")}"
        };

        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "select name from sqlite_master where type = 'table' and name not like 'sqlite_%' order by name;";
        await using var reader = await tableCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            parts.Add($"{table}:{await CountRowsAsync(connection, table)}");
        }

        return string.Join("\n", parts);
    }

    private static async Task<string> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private static async Task<string[]> CombinedRouteFactIdsInCanonicalOrderAsync(string path, int limit)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select combined_fact_id
            from combined_facts
            where fact_type = $fact_type
            order by file_path, start_line, combined_fact_id
            limit $limit;
            """;
        command.Parameters.AddWithValue("$fact_type", FactTypes.HttpRouteBinding);
        command.Parameters.AddWithValue("$limit", limit);

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values.ToArray();
    }

    private static async Task SetCombinedSourceLanguageAsync(string path, string language)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "update index_sources set language = $language;";
        command.Parameters.AddWithValue("$language", language);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {tableName};";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static void AssertForbiddenRuntimeWording(string value)
    {
        Assert.DoesNotContain("executed", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("impacted", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("called at runtime", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorized", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("used in production", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("query runs", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("proves DI target", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("selected runtime implementation", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resolved runtime target", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("service locator target", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("factory target proof", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reflection target proof", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dynamic dispatch proof", value, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static HashSet<string> RuleCatalogIds()
    {
        return File.ReadAllLines(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"))
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- id: ", StringComparison.Ordinal))
            .Select(line => line["- id: ".Length..].Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> EmittedRouteFlowRuleIds(RouteFlowReport report)
    {
        foreach (var evidence in report.EntryEvidence.Select(row => row.Evidence)
            .Concat(report.FlowRows.Select(row => row.Evidence))
            .Concat(report.LogicRows.Select(row => row.Evidence))
            .Concat(report.DependencySurfaces.Select(row => row.Evidence))
            .Concat(report.TouchedFiles.Select(row => row.Evidence))
            .Concat(report.TouchedSymbols.Select(row => row.Evidence))
            .Concat((report.ContextGroups ?? []).Select(row => row.Evidence)))
        {
            yield return evidence.RuleId;
            foreach (var ruleId in evidence.SupportingRuleIds)
            {
                yield return ruleId;
            }
        }

        foreach (var touchedFile in report.TouchedFiles)
        {
            foreach (var ruleId in touchedFile.RuleIds)
            {
                yield return ruleId;
            }
        }

        foreach (var touchedSymbol in report.TouchedSymbols)
        {
            foreach (var ruleId in touchedSymbol.RuleIds)
            {
                yield return ruleId;
            }
        }

        foreach (var contextGroup in report.ContextGroups ?? [])
        {
            foreach (var ruleId in contextGroup.RuleIds)
            {
                yield return ruleId;
            }
        }

        foreach (var gap in report.Gaps)
        {
            yield return gap.RuleId;
        }
    }
}
