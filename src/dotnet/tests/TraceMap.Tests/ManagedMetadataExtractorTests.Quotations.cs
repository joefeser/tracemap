using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string QuotationOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:15:QuotationMatrix|arity:0";
    private const string DelegateInt = "scope(assembly:name:14:System.Runtime|version:8:10.0.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)type(namespace:6:System|names:6:Func`1)<" + ClrInt + ">";
    private const string ExpressionInt = "scope(assembly:name:23:System.Linq.Expressions|version:8:10.0.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)type(namespace:23:System.Linq.Expressions|names:12:Expression`1)<" + DelegateInt + ">";
    private const string QuotationInt = FSharpScope + "type(namespace:27:Microsoft.FSharp.Quotations|names:12:FSharpExpr`1)<" + ClrInt + ">";

    internal static string QuotationSignature(string name, string language)
    {
        var type = name == "Delegate" ? DelegateInt : language == "fsharp" ? QuotationInt : ExpressionInt;
        return "arity:0" + ClrStatic + "(" + (name == "Echo" ? type : "") + ")->" + type;
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp")]
    [InlineData("vb", "CompiledEvidence.VisualBasic")]
    [InlineData("fsharp", "CompiledEvidence.FSharp")]
    public void Quotation_matrix_pins_nested_generic_signatures_with_independent_readers(string language, string assemblyName)
    {
        var assembly = ClrAssembly(language);
        var (single, facts, commit) = EvaluateClrMatrix(inputs: [assembly]);
        var (combined, combinedFacts, combinedCommit) = EvaluateClrMatrix();
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var typeHandle = Assert.Single(reader.TypeDefinitions, handle =>
            reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
            && reader.GetString(reader.GetTypeDefinition(handle).Name) == "QuotationMatrix");
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
        var cecilType = Assert.Single(module.Types, type => type.FullName == "TraceMap.CompiledFixtures.Equivalence.QuotationMatrix");
        var expectedAssembly = "assembly:name:" + assemblyName.Length + ":" + assemblyName
            + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:" + (assemblyName.Length + 4)
            + ":" + assemblyName + ".dll|targetFramework:25:.NETCoreApp,Version=v10.0";
        foreach (var name in new[] { "Tree", "Delegate", "Echo" })
        {
            var handle = Assert.Single(reader.GetTypeDefinition(typeHandle).GetMethods(), candidate => reader.GetString(reader.GetMethodDefinition(candidate).Name) == name);
            var method = reader.GetMethodDefinition(handle);
            Assert.Equal(typeHandle, method.GetDeclaringType());
            Assert.Equal(MethodAttributes.Public, method.Attributes & MethodAttributes.MemberAccessMask);
            Assert.True(method.Attributes.HasFlag(MethodAttributes.Static));
            Assert.Empty(method.GetGenericParameters());
            var blob = reader.GetBlobReader(method.Signature);
            Assert.Equal(0, blob.ReadByte());
            Assert.Equal(name == "Echo" ? 1 : 0, blob.ReadCompressedInteger());
            var shape = name == "Delegate" ? "delegate" : language == "fsharp" ? "quotation" : "expression";
            CheckRaw(ref blob, shape);
            if (name == "Echo") CheckRaw(ref blob, shape);
            Assert.Equal(0, blob.RemainingBytes);
            var cecil = Assert.Single(cecilType.Methods, candidate => candidate.Name == name);
            Assert.Equal(Token(handle), "0x" + cecil.MetadataToken.ToInt32().ToString("x8", System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(cecil.IsStatic && cecil.IsPublic);
            Assert.Empty(cecil.GenericParameters);
            CheckCecil(cecil.ReturnType, shape);
            Assert.Equal(name == "Echo" ? 1 : 0, cecil.Parameters.Count);
            if (name == "Echo") CheckCecil(cecil.Parameters[0].ParameterType, shape);
            var signature = QuotationSignature(name, language);
            var identity = expectedAssembly + QuotationOwner + "|method:" + name.Length + ":" + name + "|" + signature;
            var fact = Assert.Single(QuotationMethods(facts), candidate => candidate.TargetSymbol == identity);
            var combinedFact = Assert.Single(QuotationMethods(combinedFacts), candidate => candidate.TargetSymbol == identity);
            foreach (var observed in new[] { fact, combinedFact })
            {
                Assert.Equal(expectedAssembly, observed.Properties["assemblyIdentity"]);
                Assert.Equal(Token(handle), observed.Properties["metadataToken"]);
                Assert.Equal(name, observed.Properties["metadataName"]);
                Assert.Equal(signature, observed.Properties["signature"]);
                Assert.Equal("", observed.Properties["optionalParameterOrdinals"]);
            }
            AssertAccessorEvidence(fact, single.Provenance!, commit);
            AssertAccessorEvidence(combinedFact, combined.Provenance!, combinedCommit);
        }
        Assert.Equal(3, QuotationMethods(facts).Length);
        Assert.DoesNotContain(combinedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");

        void CheckRaw(ref BlobReader blob, string shape)
        {
            Assert.Equal(0x15, blob.ReadByte()); // GENERICINST
            Assert.Equal(0x12, blob.ReadByte()); // CLASS
            var handle = blob.ReadTypeHandle();
            Assert.Equal(HandleKind.TypeReference, handle.Kind);
            var type = reader.GetTypeReference((TypeReferenceHandle)handle);
            var expected = Shape(shape);
            Assert.Equal(expected.Namespace, reader.GetString(type.Namespace));
            Assert.Equal(expected.Name, reader.GetString(type.Name));
            Assert.Equal(HandleKind.AssemblyReference, type.ResolutionScope.Kind);
            var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
            Assert.Equal(expected.Assembly, reader.GetString(scope.Name));
            Assert.Equal(new Version(expected.Version), scope.Version);
            Assert.Equal("", reader.GetString(scope.Culture));
            Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
            Assert.Equal(1, blob.ReadCompressedInteger());
            if (shape == "expression") CheckRaw(ref blob, "delegate");
            else Assert.Equal(SignatureTypeCode.Int32, blob.ReadSignatureTypeCode());
        }

        static void CheckCecil(Mono.Cecil.TypeReference type, string shape)
        {
            var generic = Assert.IsType<Mono.Cecil.GenericInstanceType>(type);
            Assert.False(generic.IsValueType);
            var expected = Shape(shape);
            Assert.Equal(expected.Namespace, generic.ElementType.Namespace);
            Assert.Equal(expected.Name, generic.ElementType.Name);
            var scope = Assert.IsType<Mono.Cecil.AssemblyNameReference>(generic.ElementType.Scope);
            Assert.Equal(expected.Assembly + ", Version=" + expected.Version + ", Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", scope.FullName);
            var argument = Assert.Single(generic.GenericArguments);
            if (shape == "expression") CheckCecil(argument, "delegate");
            else Assert.Equal(Mono.Cecil.MetadataType.Int32, argument.MetadataType);
        }
    }

    private static (string Namespace, string Name, string Assembly, string Version) Shape(string shape) => shape switch
    {
        "delegate" => ("System", "Func`1", "System.Runtime", "10.0.0.0"),
        "expression" => ("System.Linq.Expressions", "Expression`1", "System.Linq.Expressions", "10.0.0.0"),
        "quotation" => ("Microsoft.FSharp.Quotations", "FSharpExpr`1", "FSharp.Core", "10.1.0.0"),
        _ => throw new ArgumentOutOfRangeException(nameof(shape))
    };

    [Fact]
    public void Quotation_matrix_separates_tree_wrappers_from_delegates_and_repeats_deterministically()
    {
        var (first, facts, commit) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        var methods = QuotationMethods(facts);
        Assert.Equal(9, methods.Length);
        Assert.Equal(9, methods.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(9, methods.Select(fact => fact.FactId).Distinct().Count());
        foreach (var name in new[] { "Tree", "Delegate", "Echo" })
        {
            var selected = methods.Where(fact => fact.Properties["metadataName"] == name).ToArray();
            Assert.Equal(3, selected.Length);
            Assert.Equal(name == "Delegate" ? 1 : 2, selected.Select(fact => fact.Properties["signature"]).Distinct().Count());
            Assert.All(selected, fact => AssertAccessorEvidence(fact, first.Provenance!, commit));
        }
        Assert.NotEqual(QuotationSignature("Tree", "csharp"), QuotationSignature("Delegate", "csharp"));
        Assert.NotEqual(QuotationSignature("Tree", "fsharp"), QuotationSignature("Delegate", "fsharp"));
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(second.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Quotation_matrix_duplicate_malformed_and_member_limited_inputs_remain_gaps(string language)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(assembly, copy);
        var (duplicate, facts, commit) = EvaluateClrMatrix(inputs: [assembly, copy]);
        Assert.Equal(6, QuotationMethods(facts).Length);
        Assert.All(QuotationMethods(facts), fact => Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]));
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
            Assert.Empty(QuotationMethods(rejected));
            AssertClrGap(Assert.Single(rejected, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), evaluation.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", evaluation.Provenance!.CoverageState);
        }
    }

    private static CodeFact[] QuotationMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
        && fact.TargetSymbol!.Contains(QuotationOwner + "|", StringComparison.Ordinal)
        && fact.Properties["metadataName"] is "Tree" or "Delegate" or "Echo").ToArray();
}
