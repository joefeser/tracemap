using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string ExplicitOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:13:ExplicitShape|";
    private const string InterfaceOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:16:ISharedFormatter|";
    private const string ExplicitSignature = InstanceMethod + "(" + ClrString + ")->" + ClrString;
    private const string QualifiedFormat = "TraceMap.CompiledFixtures.Equivalence.ISharedFormatter.Format";

    [Theory]
    [InlineData("csharp", QualifiedFormat, true)]
    [InlineData("vb", "FormatContract", true)]
    [InlineData("fsharp", QualifiedFormat, false)]
    public void Explicit_interface_matrix_binds_declaration_body_and_decoy_by_raw_metadata_tokens(string language, string bodyName, bool finalBody)
    {
        var (combinedEvaluation, combinedFacts, combinedCommit) = EvaluateClrMatrix();
        var assembly = ClrAssembly(language);
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [assembly]);
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var ownerHandle = FindType("ExplicitShape");
        var interfaceHandle = FindType("ISharedFormatter");
        var owner = reader.GetTypeDefinition(ownerHandle);
        var contract = reader.GetTypeDefinition(interfaceHandle);
        Assert.NotEqual(0, (int)(contract.Attributes & TypeAttributes.Interface));
        var interfaceRow = reader.GetInterfaceImplementation(Assert.Single(owner.GetInterfaceImplementations()));
        Assert.Equal(interfaceHandle, interfaceRow.Interface);
        var implementation = reader.GetMethodImplementation(Assert.Single(owner.GetMethodImplementations()));
        Assert.Equal(ownerHandle, implementation.Type);
        Assert.Equal(HandleKind.MethodDefinition, implementation.MethodBody.Kind);
        Assert.Equal(HandleKind.MethodDefinition, implementation.MethodDeclaration.Kind);
        var bodyHandle = (MethodDefinitionHandle)implementation.MethodBody;
        var declarationHandle = (MethodDefinitionHandle)implementation.MethodDeclaration;
        Assert.Equal(ownerHandle, reader.GetMethodDefinition(bodyHandle).GetDeclaringType());
        Assert.Equal(interfaceHandle, reader.GetMethodDefinition(declarationHandle).GetDeclaringType());
        Assert.Equal(declarationHandle, Assert.Single(contract.GetMethods()));
        var decoyHandle = Assert.Single(owner.GetMethods(), handle => reader.GetString(reader.GetMethodDefinition(handle).Name) == "Format");
        Assert.NotEqual(bodyHandle, decoyHandle);
        Check(declarationHandle, "Format", InterfaceOwner);
        Check(bodyHandle, bodyName, ExplicitOwner);
        Check(decoyHandle, "Format", ExplicitOwner);
        var bodyAttributes = reader.GetMethodDefinition(bodyHandle).Attributes;
        Assert.Equal(MethodAttributes.Private, bodyAttributes & MethodAttributes.MemberAccessMask);
        Assert.Equal(MethodAttributes.Virtual | MethodAttributes.NewSlot | (finalBody ? MethodAttributes.Final : 0),
            bodyAttributes & (MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot));
        var decoyAttributes = reader.GetMethodDefinition(decoyHandle).Attributes;
        Assert.Equal(MethodAttributes.Public, decoyAttributes & MethodAttributes.MemberAccessMask);
        Assert.Equal(0, (int)(decoyAttributes & MethodAttributes.Virtual));
        Assert.NotEqual(0, (int)(reader.GetMethodDefinition(declarationHandle).Attributes & MethodAttributes.Abstract));

        using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
        var cecilOwner = Assert.Single(module.Types, type => type.FullName == "TraceMap.CompiledFixtures.Equivalence.ExplicitShape");
        var cecilInterface = Assert.Single(module.Types, type => type.FullName == "TraceMap.CompiledFixtures.Equivalence.ISharedFormatter");
        Assert.Equal(System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(interfaceHandle), Assert.Single(cecilOwner.Interfaces).InterfaceType.MetadataToken.ToInt32());
        var cecilBody = Assert.Single(cecilOwner.Methods, method => method.MetadataToken.ToInt32() == System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(bodyHandle));
        var cecilDeclaration = Assert.Single(cecilInterface.Methods);
        Assert.Equal(cecilDeclaration.MetadataToken, Assert.Single(cecilBody.Overrides).MetadataToken);
        var cecilDecoy = Assert.Single(cecilOwner.Methods, method => method.MetadataToken.ToInt32() == System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(decoyHandle));
        Assert.Empty(cecilDecoy.Overrides);
        Assert.True(cecilBody.IsPrivate && cecilBody.IsVirtual && cecilBody.IsNewSlot);
        Assert.Equal(finalBody, cecilBody.IsFinal);
        Assert.True(cecilDecoy.IsPublic && !cecilDecoy.IsVirtual);
        Assert.DoesNotContain(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");

        TypeDefinitionHandle FindType(string name) => Assert.Single(reader.TypeDefinitions, handle =>
            reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
            && reader.GetString(reader.GetTypeDefinition(handle).Name) == name);

        void Check(MethodDefinitionHandle handle, string name, string typeIdentity)
        {
            var fact = Assert.Single(InterfaceMethods(facts), item => item.Properties["metadataToken"] == Token(handle));
            var combined = Assert.Single(InterfaceMethods(combinedFacts), item => item.TargetSymbol == fact.TargetSymbol);
            foreach (var observed in new[] { fact, combined })
            {
                Assert.Equal(Token(handle), observed.Properties["metadataToken"]);
                Assert.Equal(name, observed.Properties["metadataName"]);
                Assert.Contains(typeIdentity, observed.TargetSymbol);
                Assert.Equal(ExplicitSignature, observed.Properties["signature"]);
                Assert.Equal("", observed.Properties["optionalParameterOrdinals"]);
                Assert.EndsWith("|method:" + name.Length + ":" + name + "|" + ExplicitSignature, observed.TargetSymbol);
            }
            AssertAccessorEvidence(fact, evaluation.Provenance!, commit);
            AssertAccessorEvidence(combined, combinedEvaluation.Provenance!, combinedCommit);
        }
    }

    [Fact]
    public void Explicit_interface_matrix_equal_signatures_retain_all_nine_member_endpoints_and_repeat_deterministically()
    {
        var (first, facts, commit) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        var methods = InterfaceMethods(facts);
        Assert.Equal(9, methods.Length);
        Assert.Equal(9, methods.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(9, methods.Select(fact => fact.FactId).Distinct().Count());
        Assert.Equal(3, methods.Select(fact => fact.Properties["assemblyIdentity"]).Distinct().Count());
        Assert.All(methods, fact =>
        {
            Assert.Equal(ExplicitSignature, fact.Properties["signature"]);
            AssertAccessorEvidence(fact, first.Provenance!, commit);
        });
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(second.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Explicit_interface_matrix_duplicate_malformed_and_member_limited_inputs_remain_explicit_gaps(string language)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(assembly, copy);
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [assembly, copy]);
        Assert.NotEmpty(InterfaceMethods(facts));
        Assert.All(InterfaceMethods(facts), fact => Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]));
        var gaps = facts.Where(fact => fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly").ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.All(gaps, gap => AssertClrGap(gap, evaluation.Provenance!, commit));
        var truncated = Path.Combine(temp.Path, "truncated.dll");
        File.WriteAllBytes(truncated, File.ReadAllBytes(assembly)[..64]);
        Check(truncated, null, "MalformedManagedInput");
        Check(assembly, new CompiledInputLimits(MaxMemberCount: 1), "ManagedInputMemberCountLimitExceeded");
        void Check(string input, CompiledInputLimits? limits, string kind)
        {
            var (rejected, rejectedFacts, rejectedCommit) = EvaluateClrMatrix(inputs: [input], limits: limits);
            Assert.Empty(InterfaceMethods(rejectedFacts));
            AssertClrGap(Assert.Single(rejectedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), rejected.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", rejected.Provenance!.CoverageState);
        }
    }

    [Theory]
    [InlineData(false, "rawFileSha256", false)]
    [InlineData(true, "rawFileSha256", false)]
    [InlineData(false, "provenanceBindingInputSha256", false)]
    [InlineData(true, "provenanceBindingInputSha256", false)]
    [InlineData(false, "rawFileSha256", true)]
    [InlineData(true, "rawFileSha256", true)]
    public void Explicit_interface_evidence_oracle_rejects_wrong_input_hashes(
        bool combined, string field, bool corruptOutcome)
    {
        var (all, _, _) = EvaluateClrMatrix();
        foreach (var language in new[] { "csharp", "vb", "fsharp" })
        {
            var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: combined ? null : [ClrAssembly(language)]);
            var expectedLocator = Path.GetRelativePath(FindRepoRoot(), ClrAssembly(language)).Replace('\\', '/');
            var methods = InterfaceMethods(facts).Where(fact => fact.Evidence.FilePath == expectedLocator).ToArray();
            Assert.Equal(3, methods.Length);
            var other = all.Provenance!.Outcomes.First(input => input.SafeLocator != expectedLocator);
            // Unbound inputs legitimately share the empty binding-set digest. Inject
            // another assembly's raw digest into either field to ensure a distinct
            // wrong value without claiming these unbound receipts differ.
            var wrongHash = other.RawFileSha256!;
            foreach (var fact in methods)
            {
                AssertAccessorEvidence(fact, evaluation.Provenance!, commit);
                Assert.NotEqual(fact.Properties[field], wrongHash);
                var corrupted = fact with
                {
                    Properties = new SortedDictionary<string, string>(fact.Properties.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal)
                    {
                        [field] = wrongHash
                    }
                };
                var provenance = evaluation.Provenance!;
                if (corruptOutcome)
                    provenance = provenance with
                    {
                        Outcomes = provenance.Outcomes.Select(input => input.SafeLocator == expectedLocator
                            ? input with { RawFileSha256 = other.RawFileSha256 } : input).ToArray()
                    };
                Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertAccessorEvidence(corrupted, provenance, commit));
            }
        }
    }

    private static CodeFact[] InterfaceMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact =>
        fact.FactType == FactTypes.ManagedMethodDeclared
        && (fact.TargetSymbol?.Contains(ExplicitOwner, StringComparison.Ordinal) == true
            || fact.TargetSymbol?.Contains(InterfaceOwner, StringComparison.Ordinal) == true)
        && fact.Properties["metadataName"] != ".ctor").ToArray();
}
