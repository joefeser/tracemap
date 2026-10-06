using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string NestedPath = "namespace:37:TraceMap.CompiledFixtures.Equivalence|names:14:NestedMatrix`1";

    private static string NestedSignature(string name, string language)
    {
        var assemblyName = language == "csharp" ? "CompiledEvidence.CSharp" : "CompiledEvidence.VisualBasic";
        var scope = "scope(assembly:name:" + assemblyName.Length + ":" + assemblyName
            + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null)";
        var shape = name switch
        {
            "Outer" => "!0",
            "InnerValue" => "!1",
            "Method" => "!!0",
            "Construct" => scope + "type(" + NestedPath + "7:Inner`1)<" + ClrInt + "," + ClrString + ">",
            "Swap" => scope + "type(" + NestedPath + "7:Inner`1)<" + ClrString + "," + ClrInt + ">",
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        return "arity:" + (name == "Method" ? "1" : "0") + ClrStatic + "(" + shape + ")->" + shape;
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp")]
    [InlineData("vb", "CompiledEvidence.VisualBasic")]
    public void Nested_matrix_pins_generic_owners_positions_and_constructions(string language, string assemblyName)
    {
        var assembly = ClrAssembly(language);
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [assembly]);
        var (combined, all, combinedCommit) = EvaluateClrMatrix();
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var outer = Assert.Single(reader.TypeDefinitions, handle => reader.GetString(reader.GetTypeDefinition(handle).Name) == "NestedMatrix`1");
        Assert.Equal("TraceMap.CompiledFixtures.Equivalence", reader.GetString(reader.GetTypeDefinition(outer).Namespace));
        CheckParameters(outer, reader.GetTypeDefinition(outer).GetGenericParameters(), "TOuter");
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
        var cecilOuter = Assert.Single(module.Types, type => type.FullName == "TraceMap.CompiledFixtures.Equivalence.NestedMatrix`1");
        CheckCecilParameters(cecilOuter, cecilOuter.GenericParameters, "TOuter");
        var assemblyIdentity = "assembly:name:" + assemblyName.Length + ":" + assemblyName
            + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:" + (assemblyName.Length + 4)
            + ":" + assemblyName + ".dll|targetFramework:25:.NETCoreApp,Version=v10.0";
        foreach (var typeName in new[] { "Inner`1", "Plain" })
        {
            var typeHandle = Assert.Single(reader.GetTypeDefinition(outer).GetNestedTypes(), handle => reader.GetString(reader.GetTypeDefinition(handle).Name) == typeName);
            var type = reader.GetTypeDefinition(typeHandle);
            Assert.Equal(outer, type.GetDeclaringType());
            var names = typeName == "Inner`1" ? new[] { "TOuter", "TInner" } : new[] { "TOuter" };
            CheckParameters(typeHandle, type.GetGenericParameters(), names);
            var cecilType = Assert.Single(cecilOuter.NestedTypes, candidate => candidate.Name == typeName);
            Assert.Same(cecilOuter, cecilType.DeclaringType);
            Assert.Equal(Token(typeHandle), "0x" + cecilType.MetadataToken.ToInt32().ToString("x8"));
            CheckCecilParameters(cecilType, cecilType.GenericParameters, names);
            foreach (var name in typeName == "Plain" ? new[] { "Outer" } : new[] { "Outer", "InnerValue", "Method", "Construct", "Swap" })
            {
                var handle = Assert.Single(type.GetMethods(), candidate => reader.GetString(reader.GetMethodDefinition(candidate).Name) == name);
                var method = reader.GetMethodDefinition(handle);
                Assert.Equal(typeHandle, method.GetDeclaringType());
                CheckParameters(handle, method.GetGenericParameters(), name == "Method" ? ["TMethod"] : []);
                var blob = reader.GetBlobReader(method.Signature);
                Assert.Equal(name == "Method" ? 0x10 : 0, blob.ReadByte()); // static, generic bit only
                if (name == "Method") Assert.Equal(1, blob.ReadCompressedInteger());
                Assert.Equal(1, blob.ReadCompressedInteger());
                CheckRaw(ref blob, name, typeHandle);
                CheckRaw(ref blob, name, typeHandle);
                Assert.Equal(0, blob.RemainingBytes);
                var cecil = Assert.Single(cecilType.Methods, candidate => candidate.Name == name);
                Assert.True(cecil.IsStatic && cecil.IsPublic);
                Assert.Equal(Token(handle), "0x" + cecil.MetadataToken.ToInt32().ToString("x8"));
                CheckCecilParameters(cecil, cecil.GenericParameters, name == "Method" ? ["TMethod"] : []);
                CheckCecilType(cecil.ReturnType, name, cecil, cecilType);
                CheckCecilType(Assert.Single(cecil.Parameters).ParameterType, name, cecil, cecilType);
                var identity = assemblyIdentity + "|type:" + NestedPath + typeName.Length + ":" + typeName
                    + "|arity:" + names.Length + "|method:" + name.Length + ":" + name + "|" + NestedSignature(name, language);
                var fact = Assert.Single(NestedMethods(facts), candidate => candidate.TargetSymbol == identity);
                var combinedFact = Assert.Single(NestedMethods(all), candidate => candidate.TargetSymbol == identity);
                foreach (var observed in new[] { fact, combinedFact })
                {
                    Assert.Equal(Token(handle), observed.Properties["metadataToken"]);
                    Assert.Equal(assemblyIdentity, observed.Properties["assemblyIdentity"]);
                    Assert.Equal(NestedSignature(name, language), observed.Properties["signature"]);
                    Assert.Equal(name, observed.Properties["metadataName"]);
                }
                AssertAccessorEvidence(fact, evaluation.Provenance!, commit);
                AssertAccessorEvidence(combinedFact, combined.Provenance!, combinedCommit);
            }
        }
        Assert.Equal(6, NestedMethods(facts).Length);
        AssertNoQuotationEdges(facts);
        AssertNoQuotationEdges(all);
        Assert.DoesNotContain(all, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");

        void CheckParameters(EntityHandle owner, GenericParameterHandleCollection handles, params string[] names)
        {
            Assert.Equal(names.Length, handles.Count);
            foreach (var (handle, index) in handles.Select((handle, index) => (handle, index)))
            {
                var parameter = reader.GetGenericParameter(handle);
                Assert.Equal(owner, parameter.Parent);
                Assert.Equal(index, parameter.Index);
                Assert.Equal(names[index], reader.GetString(parameter.Name));
            }
        }

        static void CheckCecilParameters(Mono.Cecil.IGenericParameterProvider owner, IEnumerable<Mono.Cecil.GenericParameter> parameters, params string[] names)
        {
            var values = parameters.ToArray();
            Assert.Equal(names.Length, values.Length);
            for (var index = 0; index < values.Length; index++)
            {
                Assert.Same(owner, values[index].Owner);
                Assert.Equal(index, values[index].Position);
                Assert.Equal(names[index], values[index].Name);
            }
        }

        static void CheckRaw(ref BlobReader blob, string name, TypeDefinitionHandle typeHandle)
        {
            if (name is "Construct" or "Swap")
            {
                Assert.Equal(0x15, blob.ReadByte()); // GENERICINST
                Assert.Equal(0x12, blob.ReadByte()); // CLASS
                Assert.Equal(typeHandle, blob.ReadTypeHandle());
                Assert.Equal(2, blob.ReadCompressedInteger());
                Assert.Equal(name == "Construct" ? SignatureTypeCode.Int32 : SignatureTypeCode.String, blob.ReadSignatureTypeCode());
                Assert.Equal(name == "Construct" ? SignatureTypeCode.String : SignatureTypeCode.Int32, blob.ReadSignatureTypeCode());
            }
            else
            {
                Assert.Equal(name == "Method" ? 0x1e : 0x13, blob.ReadByte()); // MVAR vs VAR
                Assert.Equal(name == "InnerValue" ? 1 : 0, blob.ReadCompressedInteger());
            }
        }

        static void CheckCecilType(Mono.Cecil.TypeReference type, string name, Mono.Cecil.MethodDefinition method, Mono.Cecil.TypeDefinition owner)
        {
            if (name is "Construct" or "Swap")
            {
                var instance = Assert.IsType<Mono.Cecil.GenericInstanceType>(type);
                Assert.Same(owner, instance.ElementType);
                Assert.Equal(2, instance.GenericArguments.Count);
                Assert.Equal(name == "Construct" ? Mono.Cecil.MetadataType.Int32 : Mono.Cecil.MetadataType.String, instance.GenericArguments[0].MetadataType);
                Assert.Equal(name == "Construct" ? Mono.Cecil.MetadataType.String : Mono.Cecil.MetadataType.Int32, instance.GenericArguments[1].MetadataType);
            }
            else
            {
                var parameter = Assert.IsType<Mono.Cecil.GenericParameter>(type);
                Assert.Equal(name == "Method" ? Mono.Cecil.GenericParameterType.Method : Mono.Cecil.GenericParameterType.Type, parameter.Type);
                Assert.Same(name == "Method" ? (Mono.Cecil.IGenericParameterProvider)method : owner, parameter.Owner);
                Assert.Equal(name == "InnerValue" ? 1 : 0, parameter.Position);
            }
        }
    }

    [Fact]
    public void Nested_matrix_preserves_counterexamples_and_repeatability()
    {
        var (first, facts, _) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        var methods = NestedMethods(facts);
        Assert.Equal(12, methods.Length);
        Assert.Equal(12, methods.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(12, methods.Select(fact => fact.FactId).Distinct().Count());
        foreach (var assembly in methods.GroupBy(fact => fact.Properties["assemblyIdentity"]))
        {
            Assert.Equal(5, assembly.Select(fact => fact.Properties["signature"]).Distinct().Count());
            Assert.Equal(2, assembly.Count(fact => fact.Properties["metadataName"] == "Outer"));
        }
        foreach (var name in new[] { "Outer", "InnerValue", "Method", "Construct", "Swap" })
        {
            var selected = methods.Where(fact => fact.Properties["metadataName"] == name).ToArray();
            Assert.Equal(2, selected.Select(fact => fact.Properties["assemblyIdentity"]).Distinct().Count());
            // Local constructed types retain assembly scope even for equal source shapes.
            Assert.Equal(name is "Construct" or "Swap" ? 2 : 1, selected.Select(fact => fact.Properties["signature"]).Distinct().Count());
        }
        AssertNoQuotationEdges(facts);
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(second.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Nested_matrix_duplicate_malformed_and_limit_inputs_remain_explicit(string language)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(assembly, copy);
        var (_, baseline, _) = EvaluateClrMatrix(inputs: [assembly]);
        var expectedMembers = NestedMethods(baseline).Select(fact =>
            (fact.TargetSymbol, Token: fact.Properties["metadataToken"], Name: fact.Properties["metadataName"]))
            .OrderBy(member => member.TargetSymbol, StringComparer.Ordinal).ToArray();
        Assert.Equal(6, expectedMembers.Length);
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [assembly, copy]);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))).ToLowerInvariant();
        var locators = new[] { Path.GetRelativePath(FindRepoRoot(), assembly).Replace('\\', '/'), "__external__/primary/" + hash[..12] + "-copy.dll" };
        Assert.Equal(locators.Order(StringComparer.Ordinal), evaluation.Provenance!.Outcomes.Select(input => input.SafeLocator).Order(StringComparer.Ordinal));
        Assert.Equal(12, NestedMethods(facts).Select(fact => fact.FactId).Distinct().Count());
        foreach (var locator in locators)
        {
            var outcome = Assert.Single(evaluation.Provenance.Outcomes, input => input.SafeLocator == locator);
            Assert.Equal(hash, outcome.RawFileSha256);
            Assert.Contains("AmbiguousDuplicateManagedAssembly", outcome.GapKinds);
            var selected = NestedMethods(facts).Where(fact => fact.Evidence.FilePath == locator).ToArray();
            Assert.Equal(6, selected.Length);
            Assert.Equal(expectedMembers, selected.Select(fact =>
                (fact.TargetSymbol, Token: fact.Properties["metadataToken"], Name: fact.Properties["metadataName"]))
                .OrderBy(member => member.TargetSymbol, StringComparer.Ordinal));
            foreach (var fact in selected)
            {
                AssertClrProvenance(fact, evaluation.Provenance, commit);
                Assert.Equal("dotnet.compiled.member.v1", fact.RuleId);
                Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
                Assert.Equal(ManagedMetadataExtractor.MetadataLocationKind, fact.Properties["evidenceLocationKind"]);
                Assert.Matches("^0x06[0-9a-f]{6}$", fact.Properties["metadataToken"]);
                Assert.Equal(outcome.AssemblyIdentity, fact.Properties["assemblyIdentity"]);
                Assert.Equal(hash, fact.Properties["rawFileSha256"]);
                Assert.Equal(outcome.ProvenanceBindingInputSha256, fact.Properties["provenanceBindingInputSha256"]);
                Assert.Equal(NestedSignature(fact.Properties["metadataName"], language), fact.Properties["signature"]);
                Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]);
            }
            var gap = Assert.Single(facts, fact => fact.Evidence.FilePath == locator && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly");
            AssertClrGap(gap, evaluation.Provenance, commit);
            Assert.Equal(hash, gap.Properties["rawFileSha256"]);
            Assert.Equal(outcome.ProvenanceBindingInputSha256, gap.Properties["provenanceBindingInputSha256"]);
        }
        AssertNoQuotationEdges(facts);
        var truncated = Path.Combine(temp.Path, "truncated.dll");
        File.WriteAllBytes(truncated, File.ReadAllBytes(assembly)[..64]);
        Check(truncated, null, "MalformedManagedInput");
        Check(assembly, new CompiledInputLimits(MaxMemberCount: 1), "ManagedInputMemberCountLimitExceeded");
        void Check(string input, CompiledInputLimits? limits, string kind)
        {
            var (rejected, rejectedFacts, rejectedCommit) = EvaluateClrMatrix(inputs: [input], limits: limits);
            Assert.Empty(NestedMethods(rejectedFacts));
            Assert.DoesNotContain(rejectedFacts, fact => fact.FactType == FactTypes.ManagedMethodDeclared);
            AssertNoQuotationEdges(rejectedFacts);
            AssertClrGap(Assert.Single(rejectedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), rejected.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", rejected.Provenance!.CoverageState);
        }
    }

    private static CodeFact[] NestedMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
        && fact.TargetSymbol!.Contains("|type:" + NestedPath, StringComparison.Ordinal)
        && fact.Properties["metadataName"] != ".ctor").ToArray();
}
