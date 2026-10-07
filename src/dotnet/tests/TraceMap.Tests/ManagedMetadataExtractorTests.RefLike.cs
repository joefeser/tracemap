using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string RefLikeOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:13:RefLikeMatrix|arity:0";
    private const string RefLikeAssembly = "assembly:name:23:CompiledEvidence.CSharp|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:27:CompiledEvidence.CSharp.dll|targetFramework:25:.NETCoreApp,Version=v10.0";
    private const string RefLikeScope = "scope(assembly:name:14:System.Runtime|version:8:10.0.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)";
    private const string RefLikeIn = RefLikeScope + "type(namespace:30:System.Runtime.InteropServices|names:11:InAttribute)";
    private static string RefLikeSpan(string name) => RefLikeScope + "type(namespace:6:System|names:" + name.Length + ":" + name + ")<" + ClrInt + ">";
    private static string RefLikeSignature(string span, bool byRef, string modifier = "none")
    {
        var type = RefLikeSpan(span) + (byRef ? "&" : "");
        return "arity:0" + ClrStatic + "(" + type + ")->" + (modifier == "none" ? "" : modifier + "(" + RefLikeIn + ") ") + type;
    }
    private static string RefLikeIdentity(string name, string signature) => RefLikeAssembly + RefLikeOwner + "|method:" + name.Length + ":" + name + "|" + signature;
    private static CodeFact[] RefLikeMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
        && fact.TargetSymbol!.StartsWith(RefLikeAssembly + RefLikeOwner + "|method:", StringComparison.Ordinal)).ToArray();

    [Theory]
    [InlineData("Pass", "Span`1", false, false)]
    [InlineData("Pass", "ReadOnlySpan`1", false, false)]
    [InlineData("Borrow", "Span`1", true, false)]
    [InlineData("BorrowReadOnly", "Span`1", true, true)]
    public void Ref_like_matrix_pins_constructed_value_types_byrefs_and_required_return_modifier(string name, string span, bool byRef, bool readOnly)
    {
        var input = ClrAssembly("csharp");
        using var pe = new PEReader(File.OpenRead(input));
        var reader = pe.GetMetadataReader();
        var type = Assert.Single(reader.TypeDefinitions, handle => reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
            && reader.GetString(reader.GetTypeDefinition(handle).Name) == "RefLikeMatrix");
        var handle = Assert.Single(reader.GetTypeDefinition(type).GetMethods(), candidate =>
            reader.GetString(reader.GetMethodDefinition(candidate).Name) == name && ReturnSpanName(candidate) == span);
        var method = reader.GetMethodDefinition(handle);
        Assert.Equal(type, method.GetDeclaringType());
        Assert.True(method.Attributes.HasFlag(MethodAttributes.Static));
        Assert.Equal(MethodAttributes.Public, method.Attributes & MethodAttributes.MemberAccessMask);
        CheckRefLikeSignature(reader, method.Signature, span, byRef, readOnly ? "modreq" : "none");
        var parameters = method.GetParameters().Select(reader.GetParameter).ToArray();
        var parameter = Assert.Single(parameters, item => item.SequenceNumber == 1);
        Assert.Equal(readOnly ? ParameterAttributes.In : ParameterAttributes.None, parameter.Attributes);
        Assert.Equal("value", reader.GetString(parameter.Name));
        // Param flags/attributes are separate from the return signature modifier.
        var returns = parameters.Where(item => item.SequenceNumber == 0).ToArray();
        Assert.Equal(readOnly ? 1 : 0, returns.Length);
        if (readOnly)
        {
            var attribute = reader.GetCustomAttribute(Assert.Single(returns[0].GetCustomAttributes()));
            Assert.Equal(new byte[] { 1, 0, 0, 0 }, reader.GetBlobBytes(attribute.Value));
            Assert.Equal(HandleKind.MemberReference, attribute.Constructor.Kind);
            var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            Assert.Equal(".ctor", reader.GetString(constructor.Name));
            Assert.Equal(new byte[] { 0x20, 0, 1 }, reader.GetBlobBytes(constructor.Signature));
            CheckRefLikeType(reader, constructor.Parent, "System.Runtime.CompilerServices", "IsReadOnlyAttribute");
        }
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(input);
        var cecilType = Assert.Single(module.Types, item => item.FullName == "TraceMap.CompiledFixtures.Equivalence.RefLikeMatrix");
        var cecil = Assert.Single(cecilType.Methods, item => item.MetadataToken.ToInt32() == MetadataTokens.GetToken(handle));
        Assert.Equal(name, cecil.Name);
        Assert.True(cecil.IsStatic && cecil.IsPublic && !cecil.HasThis);
        var cecilParameter = Assert.Single(cecil.Parameters);
        Assert.Equal(readOnly, cecilParameter.IsIn);
        CheckCecilRefLike(cecilParameter.ParameterType, span, byRef);
        var returnType = cecil.ReturnType;
        if (readOnly)
        {
            var modifier = Assert.IsType<Mono.Cecil.RequiredModifierType>(returnType);
            Assert.Equal("System.Runtime.InteropServices.InAttribute", modifier.ModifierType.FullName);
            Assert.Equal("System.Runtime, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", modifier.ModifierType.Scope.ToString());
            returnType = modifier.ElementType;
            var attribute = Assert.Single(cecil.MethodReturnType.CustomAttributes);
            Assert.Equal("System.Runtime.CompilerServices.IsReadOnlyAttribute", attribute.AttributeType.FullName);
            Assert.Empty(attribute.ConstructorArguments);
        }
        CheckCecilRefLike(returnType, span, byRef);
        var signature = RefLikeSignature(span, byRef, readOnly ? "modreq" : "none");
        foreach (var inputs in new[] { new[] { input }, new[] { input, ClrAssembly("vb"), ClrAssembly("fsharp") } })
        {
            var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: inputs);
            var fact = Assert.Single(RefLikeMethods(facts), item => item.TargetSymbol == RefLikeIdentity(name, signature));
            Assert.Equal(signature, fact.Properties["signature"]);
            Assert.Equal(Token(handle), fact.Properties["metadataToken"]);
            AssertAccessorEvidence(fact, evaluation.Provenance!, commit);
            Assert.DoesNotContain(facts, item => item.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
            Assert.DoesNotContain(facts, item => item.FactType == FactTypes.SourceMetadataIdentityReconciled);
        }
        string ReturnSpanName(MethodDefinitionHandle candidate)
        {
            var blob = reader.GetBlobReader(reader.GetMethodDefinition(candidate).Signature);
            Assert.Equal(new byte[] { 0, 1 }, blob.ReadBytes(2));
            if (readOnly) { Assert.Equal(0x1f, blob.ReadByte()); blob.ReadTypeHandle(); }
            if (byRef) Assert.Equal(0x10, blob.ReadByte());
            Assert.Equal(new byte[] { 0x15, 0x11 }, blob.ReadBytes(2));
            return reader.GetString(reader.GetTypeReference((TypeReferenceHandle)blob.ReadTypeHandle()).Name);
        }
    }

    [Fact]
    public void Ref_like_matrix_required_optional_and_absent_modifiers_never_collapse()
    {
        var input = ClrAssembly("csharp");
        using var temp = new TempDirectory();
        var identities = new List<string>();
        foreach (var kind in new[] { "modreq", "modopt", "none" })
        {
            using var module = Mono.Cecil.ModuleDefinition.ReadModule(input);
            var method = Assert.Single(Assert.Single(module.Types, type => type.FullName == "TraceMap.CompiledFixtures.Equivalence.RefLikeMatrix").Methods, method => method.Name == "BorrowReadOnly");
            var required = Assert.IsType<Mono.Cecil.RequiredModifierType>(method.ReturnType);
            method.ReturnType = kind switch { "modopt" => new Mono.Cecil.OptionalModifierType(required.ModifierType, required.ElementType), "none" => required.ElementType, _ => required };
            var path = Path.Combine(temp.Path, kind + ".dll");
            module.Write(path); // Public data-only variants; no runtime-validity or execution claim.
            using var pe = new PEReader(File.OpenRead(path));
            var reader = pe.GetMetadataReader();
            var owner = Assert.Single(reader.TypeDefinitions, handle => reader.GetString(reader.GetTypeDefinition(handle).Name) == "RefLikeMatrix");
            var handle = Assert.Single(reader.GetTypeDefinition(owner).GetMethods(), candidate => reader.GetString(reader.GetMethodDefinition(candidate).Name) == "BorrowReadOnly");
            CheckRefLikeSignature(reader, reader.GetMethodDefinition(handle).Signature, "Span`1", true, kind);
            var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [path]);
            var signature = RefLikeSignature("Span`1", true, kind);
            var fact = Assert.Single(RefLikeMethods(facts), item => item.TargetSymbol == RefLikeIdentity("BorrowReadOnly", signature));
            Assert.Equal(signature, fact.Properties["signature"]);
            Assert.Equal(Token(handle), fact.Properties["metadataToken"]);
            CheckRefLikeEvidence(fact, evaluation.Provenance!, commit, path);
            Assert.DoesNotContain(facts, item => item.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
            identities.Add(fact.TargetSymbol!);
        }
        Assert.Equal(3, identities.Distinct().Count());
    }

    [Fact]
    public void Ref_like_matrix_repeat_duplicate_malformed_and_member_limits_remain_explicit()
    {
        var input = ClrAssembly("csharp");
        var (first, facts, commit) = EvaluateClrMatrix();
        var (repeat, repeated, _) = EvaluateClrMatrix(reverse: true);
        Assert.Equal(4, RefLikeMethods(facts).Length);
        Assert.Equal(4, RefLikeMethods(facts).Select(fact => fact.Properties["signature"]).Distinct().Count());
        Assert.Equal(4, RefLikeMethods(facts).Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(repeat.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
        using var temp = new TempDirectory();
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(input, copy);
        var (duplicate, ambiguous, duplicateCommit) = EvaluateClrMatrix(inputs: [input, copy]);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copy))).ToLowerInvariant();
        var locators = new[] { Path.GetRelativePath(FindRepoRoot(), input).Replace('\\', '/'), "__external__/primary/" + hash[..12] + "-copy.dll" };
        Assert.Equal(locators.Order(StringComparer.Ordinal), duplicate.Provenance!.Outcomes.Select(item => item.SafeLocator).Order(StringComparer.Ordinal));
        foreach (var locator in locators)
        {
            var gap = Assert.Single(ambiguous, fact => fact.Evidence.FilePath == locator && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly");
            AssertClrGap(gap, duplicate.Provenance, duplicateCommit);
            CheckRefLikeInput(gap, duplicate.Provenance, copy);
            var members = RefLikeMethods(ambiguous).Where(fact => fact.Evidence.FilePath == locator).ToArray();
            Assert.Equal(4, members.Length);
            foreach (var member in members)
            {
                Assert.NotEqual("eligible", member.Properties["sourceReconciliationEligibility"]);
                CheckRefLikeEvidence(member, duplicate.Provenance, duplicateCommit, copy);
            }
        }
        var truncated = Path.Combine(temp.Path, "truncated.dll");
        File.WriteAllBytes(truncated, File.ReadAllBytes(input)[..64]);
        Check(truncated, null, "MalformedManagedInput");
        Check(input, new CompiledInputLimits(MaxMemberCount: 1), "ManagedInputMemberCountLimitExceeded");
        void Check(string path, CompiledInputLimits? limits, string kind)
        {
            var (evaluation, rejected, rejectedCommit) = EvaluateClrMatrix(inputs: [path], limits: limits);
            Assert.Empty(RefLikeMethods(rejected));
            Assert.Equal("compiled-metadata-partial", evaluation.Provenance!.CoverageState);
            var gap = Assert.Single(rejected, fact => fact.Properties.GetValueOrDefault("gapKind") == kind);
            AssertClrGap(gap, evaluation.Provenance, rejectedCommit);
            CheckRefLikeInput(gap, evaluation.Provenance, path);
        }
    }

    private static void CheckRefLikeSignature(MetadataReader reader, BlobHandle signature, string span, bool byRef, string modifier)
    {
        var blob = reader.GetBlobReader(signature);
        Assert.Equal(new byte[] { 0, 1 }, blob.ReadBytes(2));
        if (modifier != "none")
        {
            Assert.Equal(modifier == "modreq" ? 0x1f : 0x20, blob.ReadByte());
            CheckRefLikeType(reader, blob.ReadTypeHandle(), "System.Runtime.InteropServices", "InAttribute");
        }
        ReadSpan(ref blob);
        ReadSpan(ref blob);
        Assert.Equal(0, blob.RemainingBytes);
        void ReadSpan(ref BlobReader value)
        {
            if (byRef) Assert.Equal(0x10, value.ReadByte());
            Assert.Equal(new byte[] { 0x15, 0x11 }, value.ReadBytes(2)); // GENERICINST VALUETYPE, not CLASS
            CheckRefLikeType(reader, value.ReadTypeHandle(), "System", span);
            Assert.Equal(new byte[] { 1, 8 }, value.ReadBytes(2)); // one Int32 type argument
        }
    }
    private static void CheckRefLikeType(MetadataReader reader, EntityHandle handle, string ns, string name)
    {
        Assert.Equal(HandleKind.TypeReference, handle.Kind);
        var type = reader.GetTypeReference((TypeReferenceHandle)handle);
        Assert.Equal(ns, reader.GetString(type.Namespace));
        Assert.Equal(name, reader.GetString(type.Name));
        Assert.Equal(HandleKind.AssemblyReference, type.ResolutionScope.Kind);
        var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
        Assert.Equal("System.Runtime", reader.GetString(scope.Name));
        Assert.Equal(new Version(10, 0, 0, 0), scope.Version);
        Assert.Equal("", reader.GetString(scope.Culture));
        Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
    }
    private static void CheckCecilRefLike(Mono.Cecil.TypeReference type, string span, bool byRef)
    {
        if (byRef) type = Assert.IsType<Mono.Cecil.ByReferenceType>(type).ElementType;
        var constructed = Assert.IsType<Mono.Cecil.GenericInstanceType>(type);
        Assert.True(constructed.IsValueType);
        Assert.Equal("System.Int32", Assert.Single(constructed.GenericArguments).FullName);
        Assert.Equal("System." + span, constructed.ElementType.FullName);
        Assert.Equal("System.Runtime, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", constructed.ElementType.Scope.ToString());
    }
    private static void CheckRefLikeEvidence(CodeFact fact, CompiledInputProvenance provenance, string commit, string input)
    {
        AssertClrProvenance(fact, provenance, commit);
        Assert.Equal(RuleIds.DotNetCompiledMember, fact.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
        Assert.Equal(ManagedMetadataExtractor.MetadataLocationKind, fact.Properties["evidenceLocationKind"]);
        Assert.Matches("^0x06[0-9a-f]{6}$", fact.Properties["metadataToken"]);
        Assert.Equal(RefLikeAssembly, fact.Properties["assemblyIdentity"]);
        CheckRefLikeInput(fact, provenance, input);
    }
    private static void CheckRefLikeInput(CodeFact fact, CompiledInputProvenance provenance, string input)
    {
        var outcome = Assert.Single(provenance.Outcomes, item => item.SafeLocator == fact.Evidence.FilePath);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))).ToLowerInvariant();
        Assert.Equal(hash, outcome.RawFileSha256);
        Assert.Equal(hash, fact.Properties["rawFileSha256"]);
        Assert.Equal(outcome.ProvenanceBindingInputSha256, fact.Properties["provenanceBindingInputSha256"]);
    }
}
