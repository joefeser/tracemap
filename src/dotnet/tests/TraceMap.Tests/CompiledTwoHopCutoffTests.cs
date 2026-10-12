using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CompiledTwoHopCutoffTests
{
    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, 48)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 48)]
    public async Task Retained_external_call_is_distinct_from_terminal_filtered_traversal(bool query, int depth)
    {
        using var temp = new TempDirectory();
        var providerPath = Path.Combine(temp.Path, "PublicProvider.dll");
        var callerPath = Path.Combine(temp.Path, "PublicCaller.dll");
        using var provider = AssemblyDefinition.CreateAssembly(new("PublicProvider", new(1, 0)), "PublicProvider.dll", ModuleKind.Dll);
        var outer = Method(provider.MainModule, "Outer", "Open", true);
        outer.Parameters.Add(new(provider.MainModule.TypeSystem.String));
        var inner = Method(provider.MainModule, "Inner", "Open", true);
        var il = outer.Body.GetILProcessor();
        il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Callvirt, inner); il.Emit(OpCodes.Ret);
        EmitDatabaseCall(inner, query);
        provider.Write(providerPath);
        using var caller = AssemblyDefinition.CreateAssembly(new("PublicCaller", new(1, 0)), "PublicCaller.dll", ModuleKind.Dll);
        var start = Method(caller.MainModule, "Entry", "Start", false);
        il = start.Body.GetILProcessor();
        il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Ldstr, "synthetic");
        il.Emit(OpCodes.Callvirt, caller.MainModule.ImportReference(outer));
        // A short terminal route activates bounded reverse pruning independently
        // of whether the external branch has any database terminal.
        EmitDatabaseCall(start, true);
        caller.Write(callerPath);
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !Directory.Exists(Path.Combine(repo.FullName, "samples"))) repo = repo.Parent;
        Assert.NotNull(repo);
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(repo.FullName, "samples", "messy-dotnet-workspace", "framework-signature-boundary"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths: [callerPath, providerPath], IlBodyEvidence: true));
        var rootFact = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared && f.Properties.GetValueOrDefault("metadataName") == "Start");
        var innerFact = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared && f.TargetSymbol!.Contains("5:Inner", StringComparison.Ordinal));
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.ManagedIlCallObserved && f.Properties.GetValueOrDefault("opcode") == "callvirt"
            && f.Properties.GetValueOrDefault("targetIdentity") == innerFact.TargetSymbol);
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(Path.Combine(temp.Path, "scan", "index.sqlite"), scan.Manifest, scan.Facts);
        await CombinedIndexBuilder.CombineAsync(new([Path.Combine(temp.Path, "scan", "index.sqlite")], combined, ["synthetic"]));
        var graph = await CombinedDependencyPathReporter.BuildGraphInventoryAsync(combined);
        var innerNode = Assert.Single(graph.Nodes, n => n.SymbolId == innerFact.TargetSymbol);
        Assert.Contains(graph.Edges, e => e.ToNodeId == innerNode.NodeId && e.EdgeKind == "compiled-il-call");
        var source = Assert.Single(graph.Sources);
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(new(combined, temp.Path,
            ToSurface: "database-api", IncludeLegacyRoots: true, MaxDepth: depth)
            { ExactFromSymbol = true, MaxTraversalWork = 10000 },
            [new(source.SourceIndexId, source.ScanId, source.CommitSha, rootFact.TargetSymbol!)], combinedIndex: true);
        Assert.Contains(report.Paths, p => p.Nodes.Last().SurfaceName == "SqlCommand.ExecuteScalar");
        Assert.Equal(query && depth >= 3, report.Paths.Any(p => p.Nodes.Any(n => n.NodeId == innerNode.NodeId)));
        if (!query)
        {
            var gap = Assert.Single(report.Gaps, g => g.CutoffCause == "terminal-route-not-found-within-depth-bound");
            Assert.Equal(0, gap.CutoffTraversalDepth);
            Assert.Equal(depth, gap.CutoffMaxDepth);
            Assert.DoesNotContain(report.Paths, p => p.Nodes.Any(n => n.NodeId == innerNode.NodeId));
        }
    }

    private static MethodDefinition Method(ModuleDefinition module, string typeName, string name, bool instance)
    {
        var type = new TypeDefinition("Synthetic", typeName, TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new MethodDefinition(name, MethodAttributes.Public | (instance ? 0 : MethodAttributes.Static), module.TypeSystem.Void);
        type.Methods.Add(method);
        return method;
    }

    private static void EmitDatabaseCall(MethodDefinition method, bool query)
    {
        var module = method.Module;
        var data = new AssemblyNameReference("System.Data", new(4, 0, 0, 0)) { PublicKeyToken = Convert.FromHexString("b77a5c561934e089") };
        module.AssemblyReferences.Add(data);
        var type = new TypeReference("System.Data.SqlClient", query ? "SqlCommand" : "SqlConnection", module, data);
        var call = new MethodReference(query ? "ExecuteScalar" : "Open", query ? module.TypeSystem.Object : module.TypeSystem.Void, type) { HasThis = true };
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Callvirt, call);
        if (query) il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ret);
    }
}
