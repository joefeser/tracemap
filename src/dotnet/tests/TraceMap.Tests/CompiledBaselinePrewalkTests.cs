using System.Text.Json;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

// Regressions for depth recovery crossing the compiled-only traversal scope.
public sealed class CompiledBaselinePrewalkTests
{
    private const string FillMemberRef =
        "memberref|type:scope(assembly:name:11:System.Data|version:1)type(namespace:18:System.Data.Common|names:13:DbDataAdapter)|member:4:Fill|signature:y";

    [Fact]
    public async Task Compiled_only_depth_truncation_must_not_emit_source_bridge_prewalk_witness()
    {
        var report = await CompiledReport(maxDepth: 3, terminalOnIlChain: false, "sql-query", null);
        var leaked = report.Report.Paths
            .Where(path => path.Edges.Any(edge => !CombinedDependencyPathReporter.CompiledBaselineAllowsEdge(edge.EdgeKind, true)))
            .ToArray();
        Assert.True(leaked.Length == 0, JsonSerializer.Serialize(new
        {
            flaw = "compiled-only report retained out-of-scope path(s)",
            leakedPaths = leaked.Select(path => new
            {
                path.PathId,
                path.Classification,
                EdgeKinds = path.Edges.Select(edge => edge.EdgeKind).ToArray(),
                Notes = path.Notes.Select(note => note.Code).ToArray()
            }),
            gaps = report.Report.Gaps.Select(gap => new { gap.GapKind, gap.Reason }).ToArray(),
            inventoryEdgeKinds = report.InventoryEdgeKinds
        }));
        Assert.Contains(report.Report.Gaps, gap => gap.GapKind == "CompiledBaselineNoPath");
        Assert.Contains(report.Report.Gaps, gap => gap.Reason == "depth");
        Assert.DoesNotContain("calls", report.Report.RootTraversal!.TraversedEdgeKinds);
        Assert.DoesNotContain("surface-evidence", report.Report.RootTraversal.TraversedEdgeKinds);
    }

    [Fact]
    public async Task Compiled_only_depth_limit_keeps_gap_even_when_deeper_il_terminal_exists()
    {
        var report = await CompiledReport(maxDepth: 1, terminalOnIlChain: true, "database-api", "DbDataAdapter.Fill");
        Assert.Empty(report.Report.Paths);
        Assert.True(report.Report.Summary.Truncated);
        Assert.Contains(report.Report.Gaps, gap => gap.GapKind == "CompiledBaselineNoPath");
        Assert.Contains(report.Report.Gaps, gap => gap.Reason == "depth");
        Assert.DoesNotContain("compiled-database-api-candidate", report.Report.RootTraversal!.TraversedEdgeKinds);
    }

    [Fact]
    public async Task Mixed_query_keeps_existing_depth_recovery_behavior()
    {
        var report = await CompiledReport(maxDepth: 1, terminalOnIlChain: true, "database-api", "DbDataAdapter.Fill", compiledOnly: false);
        var path = Assert.Single(report.Report.Paths);
        Assert.Contains(path.Notes, note => note.Code == "TerminalReachabilityPrewalk");
        Assert.True(report.Report.Summary.Truncated);
    }

    [Fact]
    public async Task Compiled_only_retains_pure_il_chain_to_database_api_terminal()
    {
        var report = await CompiledReport(maxDepth: 10, terminalOnIlChain: true, "database-api", "DbDataAdapter.Fill");
        var mixed = await CompiledReport(maxDepth: 10, terminalOnIlChain: true, "database-api", "DbDataAdapter.Fill", compiledOnly: false);
        Assert.True(report.Report.Paths.Count == 1, JsonSerializer.Serialize(new
        {
            paths = report.Report.Paths.Select(path => new
            {
                path.PathId,
                EdgeKinds = path.Edges.Select(edge => edge.EdgeKind).ToArray(),
                Notes = path.Notes.Select(note => note.Code).ToArray()
            }),
            mixedPaths = mixed.Report.Paths.Select(path => new
            {
                path.PathId,
                EdgeKinds = path.Edges.Select(edge => edge.EdgeKind).ToArray()
            }),
            gaps = report.Report.Gaps.Select(gap => new { gap.GapKind, gap.Reason, gap.NodeId }).ToArray(),
            traversedEdgeKinds = report.Report.RootTraversal?.TraversedEdgeKinds,
            inventoryEdgeKinds = report.InventoryEdgeKinds
        }));
        var path = report.Report.Paths[0];
        Assert.All(path.Edges, edge =>
            Assert.True(CombinedDependencyPathReporter.CompiledBaselineAllowsEdge(edge.EdgeKind, true), edge.EdgeKind));
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "compiled-il-call");
        Assert.Contains(path.Edges, edge => edge.EdgeKind == "compiled-database-api-candidate");
        Assert.DoesNotContain(path.Edges, edge => edge.EdgeKind == "calls");
        Assert.Equal("compiled-il-with-root-attachment", report.Report.Query.TraversalScope);
    }

    private sealed record ReviewReport(CombinedDependencyPathReport Report, string[] InventoryEdgeKinds);

    private static async Task<ReviewReport> CompiledReport(int maxDepth, bool terminalOnIlChain,
        string toSurface, string? surfaceName, bool compiledOnly = true)
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("server", "compiled-prewalk-review") with { CommitSha = new string('a', 40) };
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        const string raw = "0123456789abcdef0123456789abcdef";
        var facts = new List<CodeFact>();
        // Source bridge the compiled baseline must never use.
        facts.Add(CallFact(manifest, "Chain.Handler()", "Chain.Helper()", "Graph.cs", 1));
        facts.Add(QueryPatternFact(manifest, "Chain.Helper()", "Graph.cs", 2));
        var methods = (terminalOnIlChain
                ? new[] { "Chain.Handler()", "Chain.M1()", "Chain.M2()" }
                : new[] { "Chain.Handler()", "Chain.M1()", "Chain.M2()", "Chain.M3()", "Chain.M4()", "Chain.M5()" })
            .Select(symbol => MethodFact(manifest, symbol, raw))
            .ToArray();
        facts.AddRange(methods);
        for (var i = 0; i < methods.Length - 1; i++)
        {
            var body = BodyFact(manifest, methods[i], raw);
            facts.Add(body);
            facts.Add(IlCallFact(manifest, body, methods[i + 1].TargetSymbol!, raw, "methoddef", "call"));
        }
        if (terminalOnIlChain)
        {
            var body = BodyFact(manifest, methods[^1], raw);
            facts.Add(body);
            facts.Add(IlCallFact(manifest, body, FillMemberRef, raw, "memberref", "callvirt"));
        }

        SqliteIndexWriter.Write(index, manifest, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["server"]));
        var inventoryEdgeKinds = (await CombinedDependencyPathReporter.BuildGraphInventoryAsync(combined))
            .Edges.GroupBy(edge => edge.EdgeKind).Select(group => $"{group.Key}:{group.Count()}").ToArray();
        var source = Assert.Single((await CombinedDependencyPathReporter.BuildGraphInventoryAsync(combined)).Sources);
        var root = new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId, source.CommitSha, "Chain.Handler()");
        var options = new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: toSurface,
            SurfaceName: surfaceName, IncludeLegacyRoots: true, MaxDepth: maxDepth)
        { CompiledOnly = compiledOnly, ExactFromSymbol = true, MaxTraversalWork = 10_000 };
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, [root], combinedIndex: true);
        return new ReviewReport(report, inventoryEdgeKinds);
    }

    private static ScanManifest Manifest(string repo, string scannerVersion) => new(
        $"scan-{repo}", repo, null, "main", "abc123", scannerVersion,
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "Level1SemanticAnalysis", "Succeeded",
        [], [], [], [], ".", FactFactory.Hash(repo, 32), FactFactory.Hash("git-root", 32));

    private static CodeFact CallFact(ScanManifest manifest, string caller, string callee, string file, int line) =>
        FactFactory.Create(manifest, FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: caller, targetSymbol: callee,
            properties: Props(
                ("callKind", "method"),
                ("targetContainingSymbolId", "csharp type test " + ContainingType(callee)),
                ("targetSymbolId", callee)));

    private static CodeFact QueryPatternFact(ScanManifest manifest, string? sourceSymbol, string file, int line) =>
        FactFactory.Create(manifest, FactTypes.QueryPatternDetected, RuleIds.CSharpSyntaxQueryPattern, EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol, targetSymbol: "orders",
            properties: Props(
                ("operationName", "SELECT"),
                ("tableName", "orders"),
                ("columnNames", "id;status"),
                ("sqlSourceKind", "literal-string"),
                ("queryShapeHash", "shape123")));

    private static CodeFact MethodFact(ScanManifest manifest, string symbol, string raw) =>
        FactFactory.Create(manifest, FactTypes.ManagedMethodDeclared, RuleIds.DotNetCompiledMember, EvidenceTiers.Tier2Structural,
            new EvidenceSpan("app.dll", 1, 1, null, "test", "test/1.0"),
            targetSymbol: symbol, contractElement: "method",
            properties: Props(("provenanceState", "bound"), ("rawFileSha256", raw)));

    private static CodeFact BodyFact(ScanManifest manifest, CodeFact caller, string raw) =>
        FactFactory.Create(manifest, FactTypes.ManagedIlBodyDeclared, RuleIds.DotNetIlBody, EvidenceTiers.Tier2Structural,
            new EvidenceSpan("app.dll", 1, 1, null, "test", "test/1.0"),
            targetSymbol: caller.TargetSymbol + "|body", contractElement: "il-method-body",
            properties: Props(
                ("provenanceState", "bound"),
                ("rawFileSha256", raw),
                ("ilBoundedInputSha256", "bounded-sha"),
                ("ilGeneratorSha256", "generator-sha"),
                ("compiledFactId", caller.FactId)));

    private static CodeFact IlCallFact(ScanManifest manifest, CodeFact body, string targetIdentity, string raw,
        string referenceKind, string opcode) =>
        FactFactory.Create(manifest, FactTypes.ManagedIlCallObserved, RuleIds.DotNetIlCall, EvidenceTiers.Tier2Structural,
            new EvidenceSpan("app.dll", 1, 1, null, "test", "test/1.0"),
            targetSymbol: body.TargetSymbol + "|call:" + targetIdentity, contractElement: "il-call:" + opcode,
            properties: Props(
                ("provenanceState", "bound"),
                ("rawFileSha256", raw),
                ("ilBoundedInputSha256", "bounded-sha"),
                ("ilGeneratorSha256", "generator-sha"),
                ("referenceKind", referenceKind),
                ("opcode", opcode),
                ("targetIdentity", targetIdentity),
                ("ilBodyFactId", body.FactId)));

    private static SortedDictionary<string, string> Props(params (string Key, string Value)[] values)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
            properties[key] = value;
        return properties;
    }

    private static string ContainingType(string memberSymbol)
    {
        var memberEnd = memberSymbol.IndexOf('(', StringComparison.Ordinal);
        var prefix = memberEnd < 0 ? memberSymbol : memberSymbol[..memberEnd];
        var separator = prefix.LastIndexOf('.');
        return separator < 0 ? prefix : prefix[..separator];
    }
}
