using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;

namespace TraceMap.Tests;

/// <summary>
/// Bounded ordinary-CI corroboration for a public, pure rewrite. SRM reads the
/// emitted PE independently; runtime execution is an additional observation
/// and is never used to admit a scanner relationship.
/// </summary>
public sealed class IlRewritePublicIntegrationTests
{
    private const string FixtureType = "TraceMap.CompiledFixtures.CSharp.Il.IlBodyShapes";

    [Fact]
    public void Public_constant_rewrite_agrees_with_srm_and_pure_runtime_observation()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "before.dll");
        var after = Path.Combine(temp.Path, "after.dll");
        File.Copy(Fixture(), before);
        using (var assembly = CecilAssemblyDefinition.ReadAssembly(before))
        {
            var method = assembly.MainModule.GetType(FixtureType)!.Methods.Single(item => item.Name == "ConstAlpha");
            var constant = Assert.Single(method.Body.Instructions, item => item.OpCode == OpCodes.Ldc_I4_S);
            Assert.Equal((sbyte)41, constant.Operand);
            constant.Operand = (sbyte)43;
            assembly.Write(after);
        }

        var beforeMetadata = ReadWithSrm(before);
        var afterMetadata = ReadWithSrm(after);
        Assert.Equal(beforeMetadata.AssemblyIdentity, afterMetadata.AssemblyIdentity);
        Assert.Equal(beforeMetadata.ModuleName, afterMetadata.ModuleName);
        Assert.Contains("Int32", beforeMetadata.Signature, StringComparison.Ordinal);
        Assert.Equal(beforeMetadata.Signature, afterMetadata.Signature);
        Assert.Equal([0x1f, 0x29, 0x2a], beforeMetadata.Il);
        Assert.Equal([0x1f, 0x2b, 0x2a], afterMetadata.Il);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "scan"),
            IlRewriteEvidence: true, IlRewriteBeforePaths: [before], IlRewriteAfterPaths: [after]));
        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlRewriteObserved
            && fact.Properties.GetValueOrDefault("methodIdentity")!.Contains("method:10:ConstAlpha|", StringComparison.Ordinal));
        Assert.Equal(RuleIds.DotNetIlRewrite, edge.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, edge.EvidenceTier);
        Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
        Assert.Equal("true", edge.Properties["opcodeSequencePreserved"]);
        Assert.StartsWith(beforeMetadata.AssemblyIdentity, edge.Properties["beforeAssemblyIdentity"], StringComparison.Ordinal);
        Assert.Contains($"|module:{ManagedMetadataExtractor.EncodeIdentityComponent(beforeMetadata.ModuleName)}|",
            edge.Properties["beforeAssemblyIdentity"], StringComparison.Ordinal);
        Assert.EndsWith($"|{beforeMetadata.Signature}", edge.Properties["methodIdentity"], StringComparison.Ordinal);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        Assert.Matches("^[0-9a-f]{64}$", edge.Properties["ilRewriteGeneratorSha256"]);
        Assert.Matches("^[0-9a-f]{64}$", edge.Properties["ilRewriteBoundedInputSha256"]);
        Assert.False(string.IsNullOrWhiteSpace(edge.Properties["limitation"]));
        Assert.False(string.IsNullOrWhiteSpace(edge.Evidence.FilePath));
        Assert.False(string.IsNullOrWhiteSpace(edge.Evidence.ExtractorId));
        Assert.False(string.IsNullOrWhiteSpace(edge.Evidence.ExtractorVersion));
        Assert.False(string.IsNullOrWhiteSpace(edge.CommitSha));
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);

        // This one public method has no parameters, side effects, dependencies,
        // or user input. Behavior here corroborates only this exact fixture.
        Assert.Equal(41, InvokePureConstant(before));
        Assert.Equal(43, InvokePureConstant(after));
    }

    private static (string AssemblyIdentity, string ModuleName, string Signature, byte[] Il) ReadWithSrm(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var assembly = reader.GetAssemblyDefinition();
        var module = reader.GetModuleDefinition();
        var methodHandle = reader.MethodDefinitions.Single(handle =>
            reader.GetString(reader.GetMethodDefinition(handle).Name) == "ConstAlpha");
        var method = reader.GetMethodDefinition(methodHandle);
        var decoded = method.DecodeSignature(new ManagedMetadataExtractor.MetadataTypeProvider(reader), null);
        var signature = ManagedMetadataExtractor.MethodSignature(decoded.ReturnType, decoded.ParameterTypes,
            method.GetGenericParameters().Count,
            decoded.Header.CallingConvention == SignatureCallingConvention.VarArgs ? "vararg" : "default",
            decoded.Header.IsInstance, (decoded.Header.RawValue & 0x40) != 0);
        return (ManagedMetadataExtractor.AssemblyReferenceIdentity(reader.GetString(assembly.Name),
                assembly.Version.ToString(), assembly.Culture.IsNil ? null : reader.GetString(assembly.Culture), null),
            reader.GetString(module.Name), signature,
            pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!);
    }

    private static int InvokePureConstant(string path)
    {
        var context = new AssemblyLoadContext($"tracemap-public-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(path);
            var type = assembly.GetType(FixtureType, throwOnError: true)!;
            var method = type.GetMethod("ConstAlpha", BindingFlags.Public | BindingFlags.Static)!;
            Assert.Equal(typeof(int), method.ReturnType);
            Assert.Empty(method.GetParameters());
            return (int)method.Invoke(null, null)!;
        }
        finally
        {
            context.Unload();
        }
    }

    private static string Fixture()
    {
        var configuration = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.Name;
        foreach (var candidate in new[] { configuration, "Debug", "Release" }.Where(item => item is not null).Distinct())
        {
            var path = Path.Combine(RepoRoot(), "samples", "compiled-dotnet-evidence", "csharp", "bin",
                candidate!, "net10.0", "CompiledEvidence.CSharp.dll");
            if (File.Exists(path)) return path;
        }
        throw new InvalidOperationException("Build the public C# fixture first.");
    }

    private static string RepoRoot()
    {
        for (var current = AppContext.BaseDirectory; current is not null; current = Directory.GetParent(current)?.FullName)
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
        throw new InvalidOperationException("Repository root not found.");
    }
}
