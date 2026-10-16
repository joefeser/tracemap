using Mono.Cecil;
using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ConstructedGenericSampleTests
{
    [Theory]
    [InlineData("ReadText", "Container", "System.String", "get_Current")]
    [InlineData("ReadNumber", "Container", "System.Int32", "get_Current")]
    [InlineData("ReadOther", "OtherContainer", "System.String", "get_Current")]
    [InlineData("CopyText", "Container", "System.String", "Copy")]
    [InlineData("ReadText", "Container", "System.String", "get_Current", 1)]
    [InlineData("ReadText", "Container", "System.String", "get_Current", 64)]
    public async Task Native_compiler_constructed_member_resolves_only_unique_definition(
        string entry, string container, string argument, string member, int duplicates = 0)
    {
        var (root, callerPath, providerPath) = Sample();
        // Inspect real compiler metadata, not fabricated facts or executable loading.
        using var caller = AssemblyDefinition.ReadAssembly(callerPath);
        var method = caller.MainModule.Types.Single(t => t.Name == "Entry").Methods.Single(m => m.Name == entry);
        var reference = Assert.Single(method.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>());
        Assert.Equal(member, reference.Name);
        var constructed = Assert.IsType<GenericInstanceType>(reference.DeclaringType);
        Assert.Equal(container, constructed.ElementType.DeclaringType.Name);
        Assert.Equal(argument, Assert.Single(constructed.GenericArguments).FullName);
        if (entry == "CopyText")
        {
            Assert.IsType<GenericParameter>(Assert.IsType<ByReferenceType>(reference.Parameters[0].ParameterType).ElementType);
            Assert.IsType<GenericParameter>(Assert.IsType<ArrayType>(reference.Parameters[1].ParameterType).ElementType);
        }
        using var provider = AssemblyDefinition.ReadAssembly(providerPath);
        var definition = provider.MainModule.Types.Single(t => t.Name == container).NestedTypes.Single();
        Assert.Single(definition.GenericParameters);
        Assert.True(definition.Methods.Single(m => m.Name == member).HasBody);

        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(root, Path.Combine(temp.Path, "scan"),
            CompiledInputPaths: [callerPath, providerPath], IlBodyEvidence: true));
        var declaration = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared
            && f.Properties.GetValueOrDefault("metadataName") == entry);
        var body = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedIlBodyDeclared
            && f.Properties.GetValueOrDefault("compiledFactId") == declaration.FactId);
        var call = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedIlCallObserved
            && f.Properties.GetValueOrDefault("ilBodyFactId") == body.FactId);
        Assert.Equal("memberref", call.Properties["referenceKind"]);
        Assert.Equal("callvirt", call.Properties["opcode"]);
        Assert.False(string.IsNullOrWhiteSpace(call.Properties["targetIdentity"]));
        var target = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared
            && f.Properties.GetValueOrDefault("metadataName") == member
            && f.TargetSymbol!.Contains($"|names:{container.Length}:{container}6:Slot`1|", StringComparison.Ordinal));
        var index = Path.Combine(temp.Path, "index.sqlite");
        var facts = scan.Facts.Concat(Enumerable.Range(0, duplicates)
            .Select(i => target with { FactId = target.FactId + "-duplicate-" + i })).ToArray();
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new([index], combined, ["public-generic-sample"]));
        var graph = await CombinedDependencyPathReporter.BuildGraphInventoryAsync(combined);
        var node = Assert.Single(graph.Nodes, n => n.SymbolId == declaration.TargetSymbol);
        if (duplicates > 0)
        {
            Assert.DoesNotContain(graph.Edges, e => e.FromNodeId == node.NodeId && e.EdgeKind == "compiled-il-call");
            Assert.Contains(graph.Gaps, g => g.CombinedFactId == node.SourceIndexId + ":" + call.FactId
                && g.GapKind == (duplicates >= 64 ? "CompiledIlTargetUnavailable" : "CompiledIlTargetAmbiguous")
                && g.CandidateCount == duplicates + 1
                && (duplicates < 64 || g.Reason == "constructed-generic-candidate-limit"));
        }
        else
        {
            var targetNode = Assert.Single(graph.Nodes, n => n.SymbolId == target.TargetSymbol);
            var edge = Assert.Single(graph.Edges, e => e.FromNodeId == node.NodeId && e.EdgeKind == "compiled-il-call");
            Assert.Equal(targetNode.NodeId, edge.ToNodeId);
            Assert.Equal("combined.paths.constructed-generic-member.v1", edge.RuleId);
            Assert.Contains("Downstream generic IL is not specialized", string.Join(" ", edge.Limitations!));
            var provenance = Assert.Single(graph.Gaps, g => g.CombinedFactId == node.SourceIndexId + ":" + call.FactId
                && g.GapKind == "CompiledIlArtifactContext");
            Assert.Contains("A compiled target edge was retained", provenance.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("no edge was inferred", provenance.Message, StringComparison.Ordinal);
            Assert.Equal("exact-il-target-present-without-source-commit-binding", provenance.Reason);
            Assert.Equal(EvidenceTiers.Tier4Unknown, provenance.EvidenceTier);
            Assert.DoesNotContain(graph.Gaps, g => g.CombinedFactId == node.SourceIndexId + ":" + call.FactId
                && g.GapKind == "CompiledIlTargetUnavailable");
        }
        var plain = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared
            && f.Properties.GetValueOrDefault("metadataName") == "ReadPlain");
        var plainNode = Assert.Single(graph.Nodes, n => n.SymbolId == plain.TargetSymbol);
        Assert.Contains(graph.Edges, e => e.FromNodeId == plainNode.NodeId && e.EdgeKind == "compiled-il-call");
        Assert.All(graph.Gaps.Where(g => g.GapKind == "CompiledIlArtifactContext"), g =>
            Assert.Contains("A compiled target edge was retained", g.Message, StringComparison.Ordinal));
        Assert.All(graph.Gaps.Where(g => g.GapKind is "CompiledIlTargetUnavailable" or "CompiledIlTargetAmbiguous"), g =>
            Assert.Contains("no edge was inferred", g.Message, StringComparison.Ordinal));
    }

    private static (string Root, string Caller, string Provider) Sample()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        Assert.NotNull(repo);
        var root = Path.Combine(repo.FullName, "samples", "compiled-dotnet-evidence", "constructed-generics");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var output = Path.Combine(root, "caller", "bin", configuration, "net10.0");
        var caller = Path.Combine(output, "ConstructedGenerics.Caller.dll");
        var provider = Path.Combine(output, "ConstructedGenerics.Provider.dll");
        Assert.True(File.Exists(caller), caller);
        Assert.True(File.Exists(provider), provider);
        return (root, caller, provider);
    }
}
