using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string UnionOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:11:UnionMatrix|arity:0";
    private const string UnionMapping = "Microsoft.FSharp.Core.CompilationMappingAttribute";

    internal static string UnionSignature(string name, string language)
    {
        var assembly = language switch { "csharp" => "CompiledEvidence.CSharp", "vb" => "CompiledEvidence.VisualBasic", "fsharp" => "CompiledEvidence.FSharp", _ => throw new ArgumentOutOfRangeException(nameof(language)) };
        var result = "scope(assembly:name:" + assembly.Length + ":" + assembly
            + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null)type(namespace:37:TraceMap.CompiledFixtures.Equivalence|names:11:UnionMatrix)";
        return "arity:0" + ClrStatic + "(" + (name == "NewFailed" ? ClrInt : "") + ")->" + result;
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp")]
    [InlineData("vb", "CompiledEvidence.VisualBasic")]
    [InlineData("fsharp", "CompiledEvidence.FSharp")]
    public void Union_matrix_pins_factory_tokens_signatures_and_case_mapping(string language, string assemblyName)
    {
        var assembly = ClrAssembly(language);
        var (single, facts, commit) = EvaluateClrMatrix(inputs: [assembly]);
        var (combined, combinedFacts, combinedCommit) = EvaluateClrMatrix();
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var typeHandle = Assert.Single(reader.TypeDefinitions, handle =>
            reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
            && reader.GetString(reader.GetTypeDefinition(handle).Name) == "UnionMatrix");
        var type = reader.GetTypeDefinition(typeHandle);
        using var resolver = new UnionAttributeResolver();
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly, new Mono.Cecil.ReaderParameters { AssemblyResolver = resolver });
        var cecilType = Assert.Single(module.Types, candidate => candidate.FullName == "TraceMap.CompiledFixtures.Equivalence.UnionMatrix");
        CheckMapping(typeHandle, type.GetCustomAttributes(), cecilType.CustomAttributes, null);
        var expectedAssembly = "assembly:name:" + assemblyName.Length + ":" + assemblyName
            + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:" + (assemblyName.Length + 4)
            + ":" + assemblyName + ".dll|targetFramework:25:.NETCoreApp,Version=v10.0";
        foreach (var name in new[] { "get_Ready", "NewFailed" })
        {
            var handle = Assert.Single(type.GetMethods(), candidate => reader.GetString(reader.GetMethodDefinition(candidate).Name) == name);
            var method = reader.GetMethodDefinition(handle);
            Assert.Equal(typeHandle, method.GetDeclaringType());
            Assert.Equal(MethodAttributes.Public, method.Attributes & MethodAttributes.MemberAccessMask);
            Assert.True(method.Attributes.HasFlag(MethodAttributes.Static));
            Assert.Empty(method.GetGenericParameters());
            var blob = reader.GetBlobReader(method.Signature);
            Assert.Equal(0, blob.ReadByte());
            Assert.Equal(name == "NewFailed" ? 1 : 0, blob.ReadCompressedInteger());
            Assert.Equal(0x12, blob.ReadByte()); // CLASS, not a value-type return.
            Assert.Equal(typeHandle, blob.ReadTypeHandle());
            if (name == "NewFailed") Assert.Equal(SignatureTypeCode.Int32, blob.ReadSignatureTypeCode());
            Assert.Equal(0, blob.RemainingBytes);
            var cecil = Assert.Single(cecilType.Methods, candidate => candidate.Name == name);
            Assert.Equal(Token(handle), "0x" + cecil.MetadataToken.ToInt32().ToString("x8", System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(cecil.IsStatic && cecil.IsPublic);
            Assert.Equal(cecilType.MetadataToken, cecil.ReturnType.MetadataToken);
            Assert.Equal(cecilType.FullName, cecil.ReturnType.FullName);
            Assert.Equal(name == "NewFailed" ? 1 : 0, cecil.Parameters.Count);
            if (name == "NewFailed") Assert.Equal("System.Int32", cecil.Parameters[0].ParameterType.FullName);
            CheckMapping(handle, method.GetCustomAttributes(), cecil.CustomAttributes, name == "get_Ready" ? 0 : 1);
            var generated = language == "fsharp";
            Assert.Equal(generated, method.GetCustomAttributes().Any(attribute => AttributeName(attribute) == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"));
            Assert.Equal(generated, cecil.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"));
            var signature = UnionSignature(name, language);
            var identity = expectedAssembly + UnionOwner + "|method:" + name.Length + ":" + name + "|" + signature;
            var fact = Assert.Single(UnionFactories(facts), candidate => candidate.TargetSymbol == identity);
            var combinedFact = Assert.Single(UnionFactories(combinedFacts), candidate => candidate.TargetSymbol == identity);
            foreach (var observed in new[] { fact, combinedFact })
            {
                Assert.Equal(expectedAssembly, observed.Properties["assemblyIdentity"]);
                Assert.Equal(Token(handle), observed.Properties["metadataToken"]);
                Assert.Equal(name, observed.Properties["metadataName"]);
                Assert.Equal(signature, observed.Properties["signature"]);
                Assert.Equal(generated ? "true" : "false", observed.Properties["compilerGenerated"]);
                Assert.Equal("", observed.Properties["optionalParameterOrdinals"]);
            }
            AssertAccessorEvidence(fact, single.Provenance!, commit);
            AssertAccessorEvidence(combinedFact, combined.Provenance!, combinedCommit);
        }
        Assert.Equal(2, UnionFactories(facts).Length);
        Assert.DoesNotContain(combinedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");

        string? AttributeName(CustomAttributeHandle handle)
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) return null;
            var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference) return null;
            var owner = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            return reader.GetString(owner.Namespace) + "." + reader.GetString(owner.Name);
        }

        void CheckMapping(EntityHandle parent, CustomAttributeHandleCollection attributes, IEnumerable<Mono.Cecil.CustomAttribute> cecilAttributes, int? index)
        {
            var raw = attributes.Where(handle => AttributeName(handle) == UnionMapping).ToArray();
            var decoded = cecilAttributes.Where(attribute => attribute.AttributeType.FullName == UnionMapping).ToArray();
            if (language != "fsharp") { Assert.Empty(raw); Assert.Empty(decoded); return; }
            var attribute = reader.GetCustomAttribute(Assert.Single(raw));
            Assert.Equal(parent, attribute.Parent);
            var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            Assert.Equal(".ctor", reader.GetString(constructor.Name));
            var attributeType = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            Assert.Equal(HandleKind.AssemblyReference, attributeType.ResolutionScope.Kind);
            var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)attributeType.ResolutionScope);
            Assert.Equal("FSharp.Core", reader.GetString(scope.Name));
            Assert.Equal(new Version(10, 1, 0, 0), scope.Version);
            Assert.Equal("", reader.GetString(scope.Culture));
            Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
            var signature = reader.GetBlobReader(constructor.Signature);
            Assert.Equal(0x20, signature.ReadByte());
            Assert.Equal(index.HasValue ? 2 : 1, signature.ReadCompressedInteger());
            Assert.Equal(SignatureTypeCode.Void, signature.ReadSignatureTypeCode());
            Assert.Equal(0x11, signature.ReadByte()); // VALUETYPE SourceConstructFlags.
            var enumType = reader.GetTypeReference((TypeReferenceHandle)signature.ReadTypeHandle());
            Assert.Equal("Microsoft.FSharp.Core", reader.GetString(enumType.Namespace));
            Assert.Equal("SourceConstructFlags", reader.GetString(enumType.Name));
            Assert.Equal(attributeType.ResolutionScope, enumType.ResolutionScope);
            if (index.HasValue) Assert.Equal(SignatureTypeCode.Int32, signature.ReadSignatureTypeCode());
            Assert.Equal(0, signature.RemainingBytes);
            var value = reader.GetBlobReader(attribute.Value);
            Assert.Equal(1, value.ReadUInt16());
            Assert.Equal(index.HasValue ? 8 : 1, value.ReadInt32());
            if (index.HasValue) Assert.Equal(index.Value, value.ReadInt32());
            Assert.Equal(0, value.ReadUInt16());
            Assert.Equal(0, value.RemainingBytes);
            var cecil = Assert.Single(decoded);
            Assert.Equal(index.HasValue ? 2 : 1, cecil.ConstructorArguments.Count);
            Assert.Equal("Microsoft.FSharp.Core.SourceConstructFlags", cecil.ConstructorArguments[0].Type.FullName);
            Assert.Equal(index.HasValue ? 8 : 1, Assert.IsType<int>(cecil.ConstructorArguments[0].Value));
            if (index.HasValue) Assert.Equal(index.Value, Assert.IsType<int>(cecil.ConstructorArguments[1].Value));
        }
    }

    [Fact]
    public void Union_matrix_factory_shapes_preserve_six_distinct_scoped_endpoints_and_repeatability()
    {
        var (first, facts, commit) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        var methods = UnionFactories(facts);
        Assert.Equal(6, methods.Length);
        Assert.Equal(6, methods.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(6, methods.Select(fact => fact.FactId).Distinct().Count());
        foreach (var name in new[] { "get_Ready", "NewFailed" })
        {
            var selected = methods.Where(fact => fact.Properties["metadataName"] == name).ToArray();
            Assert.Equal(3, selected.Length);
            // Self-return types are assembly-scoped; similar shapes are NOT equal signatures.
            Assert.Equal(3, selected.Select(fact => fact.Properties["signature"]).Distinct().Count());
            Assert.Single(selected, fact => fact.Properties["compilerGenerated"] == "true");
            Assert.Equal(2, selected.Count(fact => fact.Properties["compilerGenerated"] == "false"));
            Assert.All(selected, fact => AssertAccessorEvidence(fact, first.Provenance!, commit));
        }
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(second.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Union_matrix_duplicate_malformed_and_member_limited_inputs_remain_gaps(string language)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(assembly, copy);
        var (duplicate, facts, commit) = EvaluateClrMatrix(inputs: [assembly, copy]);
        Assert.NotEmpty(UnionFactories(facts));
        Assert.All(UnionFactories(facts), fact => Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]));
        var gaps = facts.Where(fact => fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly").ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.All(gaps, gap => AssertClrGap(gap, duplicate.Provenance!, commit));
        var truncated = Path.Combine(temp.Path, "truncated.dll");
        File.WriteAllBytes(truncated, File.ReadAllBytes(assembly)[..64]);
        Check(truncated, null, "MalformedManagedInput");
        Check(assembly, new CompiledInputLimits(MaxMemberCount: 1), "ManagedInputMemberCountLimitExceeded");
        void Check(string input, CompiledInputLimits? limits, string kind)
        {
            var (evaluation, rejected, rejectedCommit) = EvaluateClrMatrix(inputs: [input], limits: limits);
            Assert.Empty(UnionFactories(rejected));
            AssertClrGap(Assert.Single(rejected, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), evaluation.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", evaluation.Provenance!.CoverageState);
        }
    }

    [Fact]
    public void Union_matrix_attribute_oracle_refuses_undeclared_or_wrong_version_dependencies()
    {
        using var resolver = new UnionAttributeResolver();
        var admitted = Mono.Cecil.AssemblyNameReference.Parse("FSharp.Core, Version=10.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        var dependency = resolver.Resolve(admitted);
        var flags = Assert.Single(dependency.MainModule.Types, type => type.FullName == "Microsoft.FSharp.Core.SourceConstructFlags");
        Assert.Equal("System.Int32", Assert.Single(flags.Fields, field => field.Name == "value__").FieldType.FullName);
        Assert.Equal(1, Assert.IsType<int>(Assert.Single(flags.Fields, field => field.Name == "SumType").Constant));
        Assert.Equal(8, Assert.IsType<int>(Assert.Single(flags.Fields, field => field.Name == "UnionCase").Constant));
        foreach (var name in new[]
        {
            "FSharp.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
            "System.Runtime, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
        })
            Assert.Throws<Mono.Cecil.AssemblyResolutionException>(() => resolver.Resolve(Mono.Cecil.AssemblyNameReference.Parse(name)));
    }

    // The independent test oracle may decode the enum only from this fixture's
    // explicitly pinned restore input. It never probes the runtime/GAC or other packages.
    private sealed class UnionAttributeResolver : Mono.Cecil.IAssemblyResolver
    {
        private readonly Mono.Cecil.AssemblyDefinition dependency;

        public UnionAttributeResolver()
        {
            var assetsPath = Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "fsharp", "obj", "project.assets.json");
            using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
            var package = assets.RootElement.GetProperty("libraries").GetProperty("FSharp.Core/10.1.302").GetProperty("path").GetString()!;
            const string relative = "lib/netstandard2.1/FSharp.Core.dll";
            Assert.True(assets.RootElement.GetProperty("targets").GetProperty("net10.0").GetProperty("FSharp.Core/10.1.302").GetProperty("compile").TryGetProperty(relative, out _));
            var path = Assert.Single(assets.RootElement.GetProperty("packageFolders").EnumerateObject()
                .Select(folder => Path.Combine(folder.Name, package, relative)), File.Exists);
            dependency = Mono.Cecil.AssemblyDefinition.ReadAssembly(path, new Mono.Cecil.ReaderParameters { AssemblyResolver = this });
            Assert.Equal("FSharp.Core, Version=10.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", dependency.Name.FullName);
        }

        public Mono.Cecil.AssemblyDefinition Resolve(Mono.Cecil.AssemblyNameReference name) =>
            name.FullName == dependency.Name.FullName ? dependency : throw new Mono.Cecil.AssemblyResolutionException(name);
        public Mono.Cecil.AssemblyDefinition Resolve(Mono.Cecil.AssemblyNameReference name, Mono.Cecil.ReaderParameters parameters) => Resolve(name);
        public void Dispose() => dependency.Dispose();
    }

    private static CodeFact[] UnionFactories(IReadOnlyList<CodeFact> facts) => facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
        && fact.TargetSymbol!.Contains(UnionOwner + "|", StringComparison.Ordinal)
        && fact.Properties["metadataName"] is "get_Ready" or "NewFailed").ToArray();
}
