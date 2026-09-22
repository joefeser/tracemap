using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;
using CecilAssembly = Mono.Cecil.AssemblyDefinition;
using CecilType = Mono.Cecil.TypeDefinition;

namespace TraceMap.Tests;

public sealed class IlRewriteAssemblyTopologyTests
{
    private const System.Reflection.TypeAttributes ExportedTypeForwarder = (System.Reflection.TypeAttributes)0x00200000;

    [Theory]
    [InlineData("netmodule", "ManagedNetmoduleInputUnsupported")]
    [InlineData("manifest", "MultiModuleManagedAssemblyUnsupported")]
    [InlineData("forwarder", "TypeForwardingManagedAssemblyUnsupported")]
    public void Unsupported_assembly_topology_withholds_rewrite_relationships(string shape, string cause)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, shape + ".dll");
        WriteMetadataAssembly(path, shape);
        using (var pe = new PEReader(File.OpenRead(path)))
        {
            var reader = pe.GetMetadataReader();
            Assert.Equal(shape != "netmodule", reader.IsAssembly);
            Assert.Equal(shape == "manifest", reader.AssemblyFiles.Any(h => reader.GetAssemblyFile(h).ContainsMetadata));
            Assert.Equal(shape == "forwarder", reader.ExportedTypes.Any(h =>
                (reader.GetExportedType(h).Attributes & ExportedTypeForwarder) != 0));
        }

        var options = Options(path, path);
        var evaluation = IlRewriteEvidenceExtractor.Evaluate(options);
        var pair = Assert.Single(evaluation.Pairs);
        Assert.Empty(pair.Edges);
        Assert.Equal("unsupported", pair.Outcome.Outcome);
        Assert.Equal("rewrite-partial", evaluation.Provenance!.CoverageState);
        Assert.Equal(2, pair.SideFailures.Count);
        Assert.All(pair.SideFailures, failure =>
        {
            Assert.Equal("IlRewriteUnsupportedShape", failure.GapKind);
            Assert.Equal(cause, failure.Cause);
            Assert.Contains(failure.Side, new[] { "before", "after" });
        });
        AssertDigest(evaluation.Provenance);
        var scan = ScanEngine.Scan(options);
        var gaps = scan.Facts.Where(f => f.RuleId == RuleIds.DotNetIlRewriteGap).ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.All(gaps, gap => AssertGapFact(gap, evaluation.Provenance, "IlRewriteUnsupportedShape", cause));
        Assert.DoesNotContain(scan.Facts, f => f.RuleId == RuleIds.DotNetIlRewrite);
    }

    [Fact]
    public void Duplicate_complete_method_identity_is_ambiguous_even_when_readers_agree()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "Duplicate.dll");
        WriteCecilAssembly(path, duplicateMethod: true);
        using (var pe = new PEReader(File.OpenRead(path)))
        {
            var reader = pe.GetMetadataReader();
            var signatures = reader.MethodDefinitions.Select(h =>
            {
                var method = reader.GetMethodDefinition(h);
                return (reader.GetString(method.Name), Convert.ToHexString(reader.GetBlobBytes(method.Signature)));
            }).ToArray();
            Assert.Equal(2, signatures.Count(s => s.Item1 == "Same" && s.Item2 == signatures[0].Item2));
        }

        var options = Options(path, path);
        var evaluation = IlRewriteEvidenceExtractor.Evaluate(options);
        var pair = Assert.Single(evaluation.Pairs);
        Assert.Empty(pair.Edges);
        Assert.Contains("IlRewriteIdentityAmbiguous", pair.Outcome.GapKinds);
        Assert.Equal("rewrite-partial", evaluation.Provenance!.CoverageState);
        AssertDigest(evaluation.Provenance);
        var scan = ScanEngine.Scan(options);
        var gap = Assert.Single(scan.Facts, f => f.RuleId == RuleIds.DotNetIlRewriteGap);
        AssertGapFact(gap, evaluation.Provenance, "IlRewriteIdentityAmbiguous");
        Assert.DoesNotContain(scan.Facts, f => f.RuleId == RuleIds.DotNetIlRewrite);
    }

    [Fact]
    public void Duplicate_assembly_names_are_scoped_to_the_declared_pair_ordinals()
    {
        using var temp = new TempDirectory();
        var first = Path.Combine(temp.Path, "first.dll");
        var second = Path.Combine(temp.Path, "second.dll");
        WriteCecilAssembly(first, duplicateMethod: false, value: 1);
        WriteCecilAssembly(second, duplicateMethod: false, value: 2);
        var options = Options(first, first) with
        {
            IlRewriteBeforePaths = [first, second],
            IlRewriteAfterPaths = [first, second]
        };

        var evaluation = IlRewriteEvidenceExtractor.Evaluate(options);
        Assert.Equal("rewrite-complete", evaluation.Provenance!.CoverageState);
        Assert.Equal(2, evaluation.Pairs.Count);
        Assert.Equal(evaluation.Pairs[0].Outcome.BeforeAssemblyIdentity, evaluation.Pairs[1].Outcome.BeforeAssemblyIdentity);
        Assert.NotEqual(evaluation.Pairs[0].Outcome.BeforeRawFileSha256, evaluation.Pairs[1].Outcome.BeforeRawFileSha256);
        Assert.All(evaluation.Pairs, pair =>
        {
            var edge = Assert.Single(pair.Edges);
            Assert.Contains("method:4:Same|", edge.MethodIdentity, StringComparison.Ordinal);
            Assert.Equal("unchanged", edge.RelationshipKind);
        });
        AssertDigest(evaluation.Provenance);
        var scan = ScanEngine.Scan(options);
        var edges = scan.Facts.Where(f => f.RuleId == RuleIds.DotNetIlRewrite).ToArray();
        Assert.Equal(2, edges.Length);
        Assert.All(edges, edge =>
        {
            Assert.Equal(EvidenceTiers.Tier2Structural, edge.EvidenceTier);
            Assert.Equal(evaluation.Provenance.GeneratorSha256, edge.Properties["ilRewriteGeneratorSha256"]);
            Assert.Equal(evaluation.Provenance.BoundedInputSha256, edge.Properties["ilRewriteBoundedInputSha256"]);
            Assert.Equal(IlRewriteEvidenceExtractor.EdgeLimitation, edge.Properties["limitation"]);
            Assert.Contains("assembly:name:12:SameAssembly|", edge.Properties["methodIdentity"], StringComparison.Ordinal);
            Assert.Contains("|module:10:SameModule|", edge.Properties["methodIdentity"], StringComparison.Ordinal);
            Assert.Contains("|method:4:Same|arity:0|call:default|hasThis:false|explicitThis:false|()->type(namespace:6:System|names:5:Int32)",
                edge.Properties["methodIdentity"], StringComparison.Ordinal);
            Assert.Equal("unchanged", edge.Properties["relationshipKind"]);
            Assert.False(string.IsNullOrWhiteSpace(edge.CommitSha));
            Assert.False(string.IsNullOrWhiteSpace(edge.Evidence.FilePath));
            Assert.False(string.IsNullOrWhiteSpace(edge.Evidence.ExtractorId));
            Assert.False(string.IsNullOrWhiteSpace(edge.Evidence.ExtractorVersion));
        });
        Assert.DoesNotContain(scan.Facts, f => f.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    private static void AssertGapFact(CodeFact gap, IlRewriteProvenance provenance, string kind, string? cause = null)
    {
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal(kind, gap.Properties["gapKind"]);
        Assert.Equal(IlRewriteEvidenceExtractor.GapLimitation, gap.Properties["limitation"]);
        Assert.Equal(provenance.GeneratorSha256, gap.Properties["ilRewriteGeneratorSha256"]);
        Assert.Equal(provenance.BoundedInputSha256, gap.Properties["ilRewriteBoundedInputSha256"]);
        Assert.False(string.IsNullOrWhiteSpace(gap.CommitSha));
        Assert.False(string.IsNullOrWhiteSpace(gap.Evidence.FilePath));
        Assert.False(string.IsNullOrWhiteSpace(gap.Evidence.ExtractorId));
        Assert.False(string.IsNullOrWhiteSpace(gap.Evidence.ExtractorVersion));
        if (cause is not null)
            Assert.Equal(cause, gap.Properties["cause"]);
    }

    private static void AssertDigest(IlRewriteProvenance provenance)
    {
        Assert.Matches("^[0-9a-fA-F]{64}$", provenance.GeneratorSha256);
        Assert.Matches("^[0-9a-fA-F]{64}$", provenance.BoundedInputSha256);
        Assert.Equal(IlRewriteEvidenceExtractor.SchemaVersion, provenance.SchemaVersion);
        Assert.Equal("local-only", provenance.ArtifactVisibility);
    }

    private static ScanOptions Options(string before, string after) => new(
        Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp"),
        Path.Combine(Path.GetTempPath(), "tracemap-topology-output-" + Guid.NewGuid().ToString("N")),
        IlRewriteEvidence: true,
        IlRewriteBeforePaths: [before],
        IlRewriteAfterPaths: [after]);

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    private static void WriteMetadataAssembly(string path, string shape)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("Synthetic.dll"),
            metadata.GetOrAddGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")), default, default);
        if (shape != "netmodule")
            metadata.AddAssembly(metadata.GetOrAddString("Synthetic"), new Version(1, 0), default, default,
                (System.Reflection.AssemblyFlags)0, System.Reflection.AssemblyHashAlgorithm.Sha256);
        if (shape == "manifest")
            metadata.AddAssemblyFile(metadata.GetOrAddString("secondary.netmodule"), default, containsMetadata: true);
        if (shape == "forwarder")
        {
            var target = metadata.AddAssemblyReference(metadata.GetOrAddString("ForwardedTarget"),
                new Version(1, 0), default, default, (System.Reflection.AssemblyFlags)0, default);
            metadata.AddExportedType(System.Reflection.TypeAttributes.Public | ExportedTypeForwarder,
                metadata.GetOrAddString("Synthetic"), metadata.GetOrAddString("Forwarded"), target, 0);
        }
        metadata.AddTypeDefinition(System.Reflection.TypeAttributes.NotPublic, default,
            metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var pe = new ManagedPEBuilder(new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage | Characteristics.Dll),
            new MetadataRootBuilder(metadata), new BlobBuilder(), flags: CorFlags.ILOnly);
        var blob = new BlobBuilder();
        pe.Serialize(blob);
        File.WriteAllBytes(path, blob.ToArray());
    }

    private static void WriteCecilAssembly(string path, bool duplicateMethod, int value = 1)
    {
        using var assembly = CecilAssembly.CreateAssembly(
            new AssemblyNameDefinition("SameAssembly", new Version(1, 0)), "SameModule", ModuleKind.Dll);
        var module = assembly.MainModule;
        module.Mvid = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var type = new CecilType("Synthetic", "Shape", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        AddMethod(value);
        if (duplicateMethod)
            AddMethod(value + 1);
        assembly.Write(path);

        void AddMethod(int constant)
        {
            var method = new Mono.Cecil.MethodDefinition("Same",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                module.TypeSystem.Int32);
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, constant));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(method);
        }
    }
}
