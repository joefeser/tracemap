using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string RecordOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:12:RecordMatrix|arity:0";
    private static readonly (string Name, string Raw, string Parameters, string Return, string CecilReturn)[] RecordCases =
    [
        ("Equals", "2001021c", "type(namespace:6:System|names:6:Object)", "type(namespace:6:System|names:7:Boolean)", "System.Boolean"),
        ("GetHashCode", "200008", "", ClrInt, "System.Int32"),
        ("ToString", "20000e", "", ClrString, "System.String")
    ];

    private static string RecordSignature(string parameters, string result) =>
        "arity:0|call:default|hasThis:true|explicitThis:false|(" + parameters + ")->" + result;

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp", true)]
    [InlineData("vb", "CompiledEvidence.VisualBasic", false)]
    [InlineData("fsharp", "CompiledEvidence.FSharp", true)]
    public void Record_matrix_pins_generated_overrides_and_ordinary_lookalikes(string language, string assemblyName, bool generated)
    {
        var assembly = ClrAssembly(language);
        var (single, singleFacts, commit) = EvaluateClrMatrix(inputs: [assembly]);
        var (combined, combinedFacts, combinedCommit) = EvaluateClrMatrix();
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var typeHandle = Assert.Single(reader.TypeDefinitions, handle =>
            reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
            && reader.GetString(reader.GetTypeDefinition(handle).Name) == "RecordMatrix");
        var type = reader.GetTypeDefinition(typeHandle);
        Assert.True(type.Attributes.HasFlag(TypeAttributes.Sealed));
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
        var cecilType = Assert.Single(module.Types, candidate => candidate.FullName == "TraceMap.CompiledFixtures.Equivalence.RecordMatrix");
        Assert.True(cecilType.IsSealed);
        var expectedAssembly = "assembly:name:" + assemblyName.Length + ":" + assemblyName
            + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:" + (assemblyName.Length + 4)
            + ":" + assemblyName + ".dll|targetFramework:25:.NETCoreApp,Version=v10.0";
        foreach (var test in RecordCases)
        {
            // Select the exact object overload by a raw signature, never by display name alone.
            var handle = Assert.Single(type.GetMethods(), candidate =>
                reader.GetString(reader.GetMethodDefinition(candidate).Name) == test.Name
                && Convert.ToHexString(reader.GetBlobBytes(reader.GetMethodDefinition(candidate).Signature)).ToLowerInvariant() == test.Raw);
            var method = reader.GetMethodDefinition(handle);
            Assert.Equal(typeHandle, method.GetDeclaringType());
            Assert.Equal(MethodAttributes.Public, method.Attributes & MethodAttributes.MemberAccessMask);
            Assert.True(method.Attributes.HasFlag(MethodAttributes.Virtual));
            Assert.False(method.Attributes.HasFlag(MethodAttributes.Static));
            var rawGenerated = method.GetCustomAttributes().Where(IsCompilerGenerated).ToArray();
            Assert.Equal(generated ? 1 : 0, rawGenerated.Length);
            foreach (var attributeHandle in rawGenerated)
            {
                var attribute = reader.GetCustomAttribute(attributeHandle);
                Assert.Equal(handle, attribute.Parent);
                Assert.Equal("01000000", Convert.ToHexString(reader.GetBlobBytes(attribute.Value)).ToLowerInvariant());
                var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                Assert.Equal(".ctor", reader.GetString(constructor.Name));
                Assert.Equal("200001", Convert.ToHexString(reader.GetBlobBytes(constructor.Signature)).ToLowerInvariant());
                var attributeType = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
                Assert.Equal(HandleKind.AssemblyReference, attributeType.ResolutionScope.Kind);
                var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)attributeType.ResolutionScope);
                Assert.Equal("System.Runtime", reader.GetString(scope.Name));
                Assert.Equal(new Version(10, 0, 0, 0), scope.Version);
                Assert.Equal("", reader.GetString(scope.Culture));
                Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
            }
            var cecil = Assert.Single(cecilType.Methods, candidate => candidate.Name == test.Name
                && candidate.Parameters.Count == (test.Name == "Equals" ? 1 : 0)
                && (test.Name != "Equals" || candidate.Parameters[0].ParameterType.FullName == "System.Object"));
            Assert.Equal(Token(handle), "0x" + cecil.MetadataToken.ToInt32().ToString("x8", System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(cecil.IsPublic && cecil.IsVirtual && !cecil.IsStatic);
            Assert.Equal(test.CecilReturn, cecil.ReturnType.FullName);
            Assert.Equal(generated, cecil.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"));
            var signature = RecordSignature(test.Parameters, test.Return);
            var identity = expectedAssembly + RecordOwner + "|method:" + test.Name.Length + ":" + test.Name + "|" + signature;
            var fact = Assert.Single(singleFacts, candidate => candidate.TargetSymbol == identity);
            var combinedFact = Assert.Single(combinedFacts, candidate => candidate.TargetSymbol == identity);
            foreach (var observed in new[] { fact, combinedFact })
            {
                Assert.Equal(FactTypes.ManagedMethodDeclared, observed.FactType);
                Assert.Equal(((int)cecil.Attributes).ToString(System.Globalization.CultureInfo.InvariantCulture), observed.Properties["methodDispatchFlags"]);
                Assert.Equal(expectedAssembly, observed.Properties["assemblyIdentity"]);
                Assert.Equal(Token(handle), observed.Properties["metadataToken"]);
                Assert.Equal(signature, observed.Properties["signature"]);
                Assert.Equal(generated ? "true" : "false", observed.Properties["compilerGenerated"]);
                Assert.Equal("", observed.Properties["optionalParameterOrdinals"]);
            }
            AssertAccessorEvidence(fact, single.Provenance!, commit);
            AssertAccessorEvidence(combinedFact, combined.Provenance!, combinedCommit);
        }
        Assert.DoesNotContain(combinedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");

        bool IsCompilerGenerated(CustomAttributeHandle handle)
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) return false;
            var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference) return false;
            var owner = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            return reader.GetString(owner.Namespace) == "System.Runtime.CompilerServices"
                && reader.GetString(owner.Name) == "CompilerGeneratedAttribute";
        }
    }

    [Fact]
    public void Record_matrix_equal_signatures_keep_distinct_generated_and_ordinary_endpoints()
    {
        var (first, facts, commit) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        foreach (var test in RecordCases)
        {
            var methods = RecordMethods(facts).Where(fact => fact.Properties["metadataName"] == test.Name
                && fact.Properties["signature"] == RecordSignature(test.Parameters, test.Return)).ToArray();
            Assert.Equal(3, methods.Length);
            Assert.Equal(3, methods.Select(fact => fact.TargetSymbol).Distinct().Count());
            Assert.Equal(3, methods.Select(fact => fact.FactId).Distinct().Count());
            Assert.Equal(2, methods.Count(fact => fact.Properties["compilerGenerated"] == "true"));
            Assert.Single(methods, fact => fact.Properties["compilerGenerated"] == "false");
            Assert.All(methods, fact => AssertAccessorEvidence(fact, first.Provenance!, commit));
        }
        // Record helpers are additional members, never substitutes for the object overloads.
        Assert.Contains(RecordMethods(facts), fact => fact.Properties["metadataName"] == "Equals"
            && fact.Properties["signature"] != RecordSignature(RecordCases[0].Parameters, RecordCases[0].Return));
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(second.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Record_matrix_duplicate_malformed_and_member_limits_remain_gaps(string language)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(assembly, copy);
        var (duplicate, facts, commit) = EvaluateClrMatrix(inputs: [assembly, copy]);
        Assert.NotEmpty(RecordMethods(facts));
        Assert.All(RecordMethods(facts), fact => Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]));
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
            Assert.Empty(RecordMethods(rejected));
            AssertClrGap(Assert.Single(rejected, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), evaluation.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", evaluation.Provenance!.CoverageState);
        }
    }

    private static CodeFact[] RecordMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact =>
        fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol!.Contains(RecordOwner + "|", StringComparison.Ordinal)).ToArray();
}
