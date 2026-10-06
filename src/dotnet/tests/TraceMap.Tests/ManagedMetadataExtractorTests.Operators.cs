using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string OperatorOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:13:OperatorShape|";
    private const string OperatorType = "type(namespace:37:TraceMap.CompiledFixtures.Equivalence|names:13:OperatorShape)";
    private const string OperatorDecoy = "op_LooksLikeOperator";

    internal static string OperatorSignature(string name, string language)
    {
        var assemblyName = language switch
        {
            "csharp" => "CompiledEvidence.CSharp",
            "vb" => "CompiledEvidence.VisualBasic",
            "fsharp" => "CompiledEvidence.FSharp",
            _ => throw new ArgumentOutOfRangeException(nameof(language))
        };
        var scopedType = "scope(assembly:name:" + assemblyName.Length + ":" + assemblyName
            + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null)" + OperatorType;
        return "arity:0" + ClrStatic + (name switch
        {
            "op_Implicit" => "(" + ClrInt + ")->" + scopedType,
            "op_Explicit" => "(" + scopedType + ")->" + ClrInt,
            "op_Addition" or OperatorDecoy => "(" + scopedType + "," + scopedType + ")->" + scopedType,
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        });
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Operator_matrix_matches_raw_signature_tokens_flags_and_per_input_evidence(string language)
    {
        var assembly = ClrAssembly(language);
        var (single, singleFacts, commit) = EvaluateClrMatrix(inputs: [assembly]);
        var (combined, combinedFacts, combinedCommit) = EvaluateClrMatrix();
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var typeHandle = Assert.Single(reader.TypeDefinitions, handle =>
            reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
            && reader.GetString(reader.GetTypeDefinition(handle).Name) == "OperatorShape");
        var type = reader.GetTypeDefinition(typeHandle);
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
        var cecilType = Assert.Single(module.Types, candidate => candidate.FullName == "TraceMap.CompiledFixtures.Equivalence.OperatorShape");
        Assert.Equal(4, OperatorMethods(singleFacts).Length);
        foreach (var name in new[] { "op_Addition", "op_Implicit", "op_Explicit", OperatorDecoy })
        {
            var handle = Assert.Single(type.GetMethods(), candidate => reader.GetString(reader.GetMethodDefinition(candidate).Name) == name);
            var method = reader.GetMethodDefinition(handle);
            Assert.Equal(typeHandle, method.GetDeclaringType());
            Assert.Equal(MethodAttributes.Public, method.Attributes & MethodAttributes.MemberAccessMask);
            Assert.True(method.Attributes.HasFlag(MethodAttributes.Static));
            Assert.Equal(name != OperatorDecoy || language == "fsharp", method.Attributes.HasFlag(MethodAttributes.SpecialName));
            Assert.False(method.Attributes.HasFlag(MethodAttributes.RTSpecialName));
            Assert.Empty(method.GetGenericParameters());
            var blob = reader.GetBlobReader(method.Signature);
            Assert.Equal(0, blob.ReadByte()); // Default, static, non-generic method signature.
            var count = name is "op_Implicit" or "op_Explicit" ? 1 : 2;
            Assert.Equal(count, blob.ReadCompressedInteger());
            foreach (var integer in new[] { name == "op_Explicit" }.Concat(Enumerable.Repeat(name == "op_Implicit", count)))
            {
                Assert.Equal(integer ? SignatureTypeCode.Int32 : SignatureTypeCode.TypeHandle, blob.ReadSignatureTypeCode());
                if (!integer) Assert.Equal(typeHandle, blob.ReadTypeHandle());
            }
            Assert.Equal(0, blob.RemainingBytes);

            var cecil = Assert.Single(cecilType.Methods, candidate => candidate.Name == name);
            Assert.Equal(Token(handle), "0x" + cecil.MetadataToken.ToInt32().ToString("x8", System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(cecil.IsPublic && cecil.IsStatic);
            Assert.Equal(name != OperatorDecoy || language == "fsharp", cecil.IsSpecialName);
            Assert.False(cecil.IsRuntimeSpecialName);
            Assert.Equal(count, cecil.Parameters.Count);
            CheckCecilType(cecil.ReturnType, name == "op_Explicit");
            foreach (var parameter in cecil.Parameters) CheckCecilType(parameter.ParameterType, name == "op_Implicit");

            var fact = Assert.Single(OperatorMethods(singleFacts), candidate => candidate.Properties["metadataName"] == name);
            var combinedFact = Assert.Single(OperatorMethods(combinedFacts), candidate => candidate.TargetSymbol == fact.TargetSymbol);
            foreach (var observed in new[] { fact, combinedFact })
            {
                Assert.Equal(Token(handle), observed.Properties["metadataToken"]);
                Assert.Equal(name, observed.Properties["metadataName"]);
                Assert.Equal(OperatorSignature(name, language), observed.Properties["signature"]);
                Assert.Equal("", observed.Properties["optionalParameterOrdinals"]);
                Assert.Equal(observed.Properties["assemblyIdentity"] + OperatorOwner + "arity:0|method:" + name.Length + ":" + name + "|" + OperatorSignature(name, language), observed.TargetSymbol);
            }
            AssertAccessorEvidence(fact, single.Provenance!, commit);
            AssertAccessorEvidence(combinedFact, combined.Provenance!, combinedCommit);

            void CheckCecilType(Mono.Cecil.TypeReference reference, bool integer)
            {
                Assert.Equal(integer ? "System.Int32" : cecilType.FullName, reference.FullName);
                if (!integer) Assert.Equal(cecilType.MetadataToken, reference.MetadataToken);
            }
        }
        Assert.DoesNotContain(combinedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
    }

    [Fact]
    public void Operator_matrix_corresponding_shapes_keep_assembly_scopes_conversion_and_decoy_endpoints_distinct()
    {
        var (first, facts, commit) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        var methods = OperatorMethods(facts);
        Assert.Equal(12, methods.Length);
        Assert.Equal(12, methods.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(12, methods.Select(fact => fact.FactId).Distinct().Count());
        foreach (var name in new[] { "op_Addition", "op_Implicit", "op_Explicit", OperatorDecoy })
        {
            var selected = methods.Where(fact => fact.Properties["metadataName"] == name).ToArray();
            Assert.Equal(3, selected.Length);
            Assert.Equal(3, selected.Select(fact => fact.Properties["assemblyIdentity"]).Distinct().Count());
            Assert.Equal(3, selected.Select(fact => fact.Properties["signature"]).Distinct().Count());
            foreach (var language in new[] { "csharp", "vb", "fsharp" })
            {
                var locator = Path.GetRelativePath(FindRepoRoot(), ClrAssembly(language)).Replace('\\', '/');
                var fact = Assert.Single(selected, candidate => candidate.Evidence.FilePath == locator);
                Assert.Equal(OperatorSignature(name, language), fact.Properties["signature"]);
                AssertAccessorEvidence(fact, first.Provenance!, commit);
            }
        }
        Assert.Equal(OperatorSignature("op_Addition", "csharp"), OperatorSignature(OperatorDecoy, "csharp"));
        Assert.NotEqual(OperatorSignature("op_Implicit", "csharp"), OperatorSignature("op_Explicit", "csharp"));
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(second.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Operator_matrix_duplicate_malformed_and_bounded_inputs_retain_explicit_gaps(string language)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(assembly, copy);
        var (duplicate, facts, commit) = EvaluateClrMatrix(inputs: [assembly, copy]);
        Assert.NotEmpty(OperatorMethods(facts));
        Assert.All(OperatorMethods(facts), fact => Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]));
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
            Assert.Empty(OperatorMethods(rejected));
            AssertClrGap(Assert.Single(rejected, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), evaluation.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", evaluation.Provenance!.CoverageState);
        }
    }

    private static CodeFact[] OperatorMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact =>
        fact.FactType == FactTypes.ManagedMethodDeclared
        && fact.TargetSymbol!.Contains(OperatorOwner, StringComparison.Ordinal)
        && fact.Properties["metadataName"] != ".ctor").ToArray();
}
