using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string ModuleOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:11:ModuleShape|arity:0";
    private const string ArgumentCountsAttribute = "Microsoft.FSharp.Core.CompilationArgumentCountsAttribute";
    private const string CompiledNameAttribute = "Microsoft.FSharp.Core.CompiledNameAttribute";
    private const string StandardModuleAttribute = "Microsoft.VisualBasic.CompilerServices.StandardModuleAttribute";

    internal static string ModuleSignature(string name) => "arity:0" + ClrStatic
        + (name == "Renamed" ? "(" + ClrInt + ")" : "(" + ClrInt + "," + ClrInt + ")") + "->" + ClrInt;

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp", true)]
    [InlineData("vb", "CompiledEvidence.VisualBasic", false)]
    [InlineData("fsharp", "CompiledEvidence.FSharp", true)]
    public void Module_matrix_pins_static_methods_compiled_names_and_argument_groups(string language, string assemblyName, bool abstractType)
    {
        var assembly = ClrAssembly(language);
        var (single, singleFacts, commit) = EvaluateClrMatrix(inputs: [assembly]);
        var (combined, combinedFacts, combinedCommit) = EvaluateClrMatrix();
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var typeHandle = Assert.Single(reader.TypeDefinitions, handle =>
            reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
            && reader.GetString(reader.GetTypeDefinition(handle).Name) == "ModuleShape");
        var type = reader.GetTypeDefinition(typeHandle);
        Assert.True(type.Attributes.HasFlag(TypeAttributes.Sealed));
        Assert.Equal(abstractType, type.Attributes.HasFlag(TypeAttributes.Abstract));
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
        var cecilType = Assert.Single(module.Types, candidate => candidate.FullName == "TraceMap.CompiledFixtures.Equivalence.ModuleShape");
        Assert.True(cecilType.IsSealed);
        Assert.Equal(abstractType, cecilType.IsAbstract);
        Assert.Equal(language == "vb", type.GetCustomAttributes().Any(handle => AttributeName(handle) == StandardModuleAttribute));
        Assert.Equal(language == "vb", cecilType.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == StandardModuleAttribute));
        if (language == "vb")
        {
            var raw = reader.GetCustomAttribute(Assert.Single(type.GetCustomAttributes(), handle => AttributeName(handle) == StandardModuleAttribute));
            Assert.Equal(typeHandle, raw.Parent);
            Assert.Equal("01000000", Convert.ToHexString(reader.GetBlobBytes(raw.Value)).ToLowerInvariant());
            var constructor = reader.GetMemberReference((MemberReferenceHandle)raw.Constructor);
            Assert.Equal(".ctor", reader.GetString(constructor.Name));
            Assert.Equal("200001", Convert.ToHexString(reader.GetBlobBytes(constructor.Signature)).ToLowerInvariant());
            var attributeType = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            Assert.Equal(HandleKind.AssemblyReference, attributeType.ResolutionScope.Kind);
            var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)attributeType.ResolutionScope);
            Assert.Equal("Microsoft.VisualBasic.Core", reader.GetString(scope.Name));
            Assert.Equal(new Version(15, 0, 0, 0), scope.Version);
            Assert.Equal("", reader.GetString(scope.Culture));
            Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
            Assert.Empty(Assert.Single(cecilType.CustomAttributes, attribute => attribute.AttributeType.FullName == StandardModuleAttribute).ConstructorArguments);
        }
        Assert.Equal(3, ModuleMethods(singleFacts).Length);
        var expectedAssembly = "assembly:name:" + assemblyName.Length + ":" + assemblyName
            + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:" + (assemblyName.Length + 4)
            + ":" + assemblyName + ".dll|targetFramework:25:.NETCoreApp,Version=v10.0";
        foreach (var name in new[] { "Curried", "Tupled", "Renamed" })
        {
            var handle = Assert.Single(type.GetMethods(), candidate => reader.GetString(reader.GetMethodDefinition(candidate).Name) == name);
            var method = reader.GetMethodDefinition(handle);
            Assert.Equal(typeHandle, method.GetDeclaringType());
            Assert.Equal(MethodAttributes.Public, method.Attributes & MethodAttributes.MemberAccessMask);
            Assert.True(method.Attributes.HasFlag(MethodAttributes.Static));
            Assert.Equal(name == "Renamed" ? "00010808" : "0002080808", Convert.ToHexString(reader.GetBlobBytes(method.Signature)).ToLowerInvariant());
            var cecil = Assert.Single(cecilType.Methods, candidate => candidate.Name == name);
            Assert.Equal(Token(handle), "0x" + cecil.MetadataToken.ToInt32().ToString("x8", System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(cecil.IsPublic && cecil.IsStatic);
            Assert.Equal("System.Int32", cecil.ReturnType.FullName);
            Assert.Equal(name == "Renamed" ? 1 : 2, cecil.Parameters.Count);
            Assert.All(cecil.Parameters, parameter => Assert.Equal("System.Int32", parameter.ParameterType.FullName));
            var groupAttributes = method.GetCustomAttributes().Where(attribute => AttributeName(attribute) == ArgumentCountsAttribute).ToArray();
            var cecilGroups = cecil.CustomAttributes.Where(attribute => attribute.AttributeType.FullName == ArgumentCountsAttribute).ToArray();
            if (language == "fsharp" && name == "Curried")
            {
                var raw = reader.GetCustomAttribute(Assert.Single(groupAttributes));
                Assert.Equal(handle, raw.Parent);
                var constructor = reader.GetMemberReference((MemberReferenceHandle)raw.Constructor);
                Assert.Equal(".ctor", reader.GetString(constructor.Name));
                Assert.Equal("2001011d08", Convert.ToHexString(reader.GetBlobBytes(constructor.Signature)).ToLowerInvariant());
                var attributeType = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
                Assert.Equal(HandleKind.AssemblyReference, attributeType.ResolutionScope.Kind);
                var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)attributeType.ResolutionScope);
                Assert.Equal("FSharp.Core", reader.GetString(scope.Name));
                Assert.Equal(new Version(10, 1, 0, 0), scope.Version);
                Assert.Equal("", reader.GetString(scope.Culture));
                Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
                Assert.Equal("01000200000001000000010000000000", Convert.ToHexString(reader.GetBlobBytes(raw.Value)).ToLowerInvariant());
                var argument = Assert.Single(Assert.Single(cecilGroups).ConstructorArguments);
                var counts = Assert.IsType<Mono.Cecil.CustomAttributeArgument[]>(argument.Value);
                Assert.Equal(new object[] { 1, 1 }, counts.Select(count => count.Value).ToArray());
            }
            else
            {
                Assert.Empty(groupAttributes);
                Assert.Empty(cecilGroups);
            }
            var compiledNames = method.GetCustomAttributes().Where(attribute => AttributeName(attribute) == CompiledNameAttribute).ToArray();
            var cecilNames = cecil.CustomAttributes.Where(attribute => attribute.AttributeType.FullName == CompiledNameAttribute).ToArray();
            // CompiledName is consumed by the F# compiler, not emitted as a
            // method attribute. The assembly cannot establish sourceAlias ownership.
            Assert.Empty(compiledNames);
            Assert.Empty(cecilNames);
            var fact = Assert.Single(ModuleMethods(singleFacts), candidate => candidate.Properties["metadataName"] == name);
            var combinedFact = Assert.Single(ModuleMethods(combinedFacts), candidate => candidate.TargetSymbol == fact.TargetSymbol);
            foreach (var observed in new[] { fact, combinedFact })
            {
                Assert.Equal(expectedAssembly, observed.Properties["assemblyIdentity"]);
                Assert.Equal(Token(handle), observed.Properties["metadataToken"]);
                Assert.Equal(name, observed.Properties["metadataName"]);
                Assert.Equal(ModuleSignature(name), observed.Properties["signature"]);
                Assert.Equal("", observed.Properties["optionalParameterOrdinals"]);
                Assert.Equal(expectedAssembly + ModuleOwner + "|method:" + name.Length + ":" + name + "|" + ModuleSignature(name), observed.TargetSymbol);
            }
            AssertAccessorEvidence(fact, single.Provenance!, commit);
            AssertAccessorEvidence(combinedFact, combined.Provenance!, combinedCommit);
        }
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
    }

    [Fact]
    public void Module_matrix_signature_equality_preserves_nine_endpoints_and_repeatability()
    {
        var (first, facts, commit) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        var methods = ModuleMethods(facts);
        Assert.Equal(9, methods.Length);
        Assert.Equal(9, methods.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(9, methods.Select(fact => fact.FactId).Distinct().Count());
        foreach (var name in new[] { "Curried", "Tupled", "Renamed" })
        {
            var selected = methods.Where(fact => fact.Properties["metadataName"] == name).ToArray();
            Assert.Equal(3, selected.Length);
            Assert.Equal(3, selected.Select(fact => fact.Properties["assemblyIdentity"]).Distinct().Count());
            Assert.All(selected, fact =>
            {
                Assert.Equal(ModuleSignature(name), fact.Properties["signature"]);
                AssertAccessorEvidence(fact, first.Provenance!, commit);
            });
        }
        Assert.Equal(ModuleSignature("Curried"), ModuleSignature("Tupled"));
        Assert.DoesNotContain(methods, fact => fact.Properties["metadataName"] is "curried" or "tupled" or "sourceAlias");
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(second.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Module_matrix_duplicate_malformed_and_member_limited_inputs_remain_explicit_gaps(string language)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(assembly, copy);
        var (duplicate, facts, commit) = EvaluateClrMatrix(inputs: [assembly, copy]);
        Assert.NotEmpty(ModuleMethods(facts));
        Assert.All(ModuleMethods(facts), fact => Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]));
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
            Assert.Empty(ModuleMethods(rejected));
            AssertClrGap(Assert.Single(rejected, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), evaluation.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", evaluation.Provenance!.CoverageState);
        }
    }

    private static CodeFact[] ModuleMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact =>
        fact.FactType == FactTypes.ManagedMethodDeclared
        && fact.TargetSymbol!.Contains(ModuleOwner + "|", StringComparison.Ordinal)
        && fact.Properties["metadataName"] != ".ctor").ToArray();
}
