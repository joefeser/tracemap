using System.Globalization;
using System.Text.Json;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CecilMethodDefinition = Mono.Cecil.MethodDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using TraceMap.Core;

namespace TraceMap.Tests;

/// <summary>Public Task 10 metadata/member rewrite cases. The C# compiler builds
/// the before side; Cecil writes bounded after sides. The scanner must admit
/// each side through SRM and Cecil independently before reporting an edge.</summary>
public sealed class IlRewriteMemberShapeEvidenceExtractorTests
{
    private const string FixtureType = "TraceMap.CompiledFixtures.CSharp.Il.IlRewriteMemberShapes";

    [Fact]
    public void Compiler_fixture_admits_generic_methodspec_typespec_calli_modreq_and_accessors()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "baseline");
        var result = Scan(before, after, temp);
        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
        foreach (var method in new[] { "GenericCall", "GenericType", "TypeTest", "Indirect", "ByReadonlyRef", "get_Value", "set_Value", "add_Changed", "remove_Changed" })
            Assert.Equal("unchanged", Edge(result, method).Properties["relationshipKind"]);

        using var stream = File.OpenRead(before);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        Assert.True(reader.GetTableRowCount(TableIndex.MethodSpec) > 0);
        Assert.True(reader.GetTableRowCount(TableIndex.TypeSpec) > 0);
        Assert.True(reader.GetTableRowCount(TableIndex.StandAloneSig) > 0);
        Assert.Contains(reader.PropertyDefinitions, handle => reader.GetString(reader.GetPropertyDefinition(handle).Name) == "Value");
        Assert.Contains(reader.EventDefinitions, handle => reader.GetString(reader.GetEventDefinition(handle).Name) == "Changed");
        Assert.Contains(BodyBytes(pe, reader, "Indirect"), octet => octet == 0x29); // calli
        Assert.Contains(BodyBytes(pe, reader, "TypeToken"), octet => octet == 0xd0); // ldtoken
        Assert.Contains(reader.MethodDefinitions, handle =>
            reader.GetString(reader.GetMethodDefinition(handle).Name) == "ByReadonlyRef");
    }

    [Fact]
    public void Compiler_vararg_call_preserves_required_and_optional_signature_shape()
    {
        using var temp = new TempDirectory();
        var fixture = Fixture("CompiledEvidence.CSharp.VarArgCall.dll");
        var pair = Assert.Single(IlRewriteEvidenceExtractor.Evaluate(new ScanOptions(
            Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp"),
            Path.Combine(temp.Path, "out"), IlRewriteEvidence: true,
            IlRewriteBeforePaths: [fixture], IlRewriteAfterPaths: [fixture])).Pairs);
        Assert.Empty(pair.SideFailures);
        Assert.Equal("admitted", pair.Outcome.Outcome);
        var edge = Assert.Single(pair.Edges, item => item.MethodIdentity.Contains("method:9:UseVarArg|", StringComparison.Ordinal));
        Assert.Equal("unchanged", edge.RelationshipKind);
        using var stream = File.OpenRead(fixture);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var reference = reader.MemberReferences.Select(reader.GetMemberReference).Single(item =>
            reader.GetString(item.Name) == "VarArg" && item.Parent.Kind == HandleKind.MethodDefinition);
        var signature = reference.DecodeMethodSignature(new ManagedMetadataExtractor.MetadataTypeProvider(reader), null);
        Assert.Equal(SignatureCallingConvention.VarArgs, signature.Header.CallingConvention);
        Assert.Equal(1, signature.RequiredParameterCount);
        Assert.Equal(2, signature.ParameterTypes.Length);
    }

    [Fact]
    public void Public_case_catalog_pins_rule_tier_limitations_and_runtime_digest_properties()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepoRoot(), "samples", "compiled-dotnet-evidence", "fixture-cases.json")));
        Assert.Equal("compiled-dotnet-fixture-cases.v8", document.RootElement.GetProperty("schemaVersion").GetString());
        var cases = document.RootElement.GetProperty("ilRewriteCases").EnumerateArray()
            .Where(item => (item.GetProperty("id").GetString() ?? "").EndsWith("-021", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-022", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-023", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-024", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-025", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-026", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-027", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-028", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-029", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-030", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-031", StringComparison.Ordinal)
                || (item.GetProperty("id").GetString() ?? "").EndsWith("-032", StringComparison.Ordinal)).ToArray();
        Assert.Equal(12, cases.Length);
        Assert.Equal(12, cases.Select(item => item.GetProperty("id").GetString()).Distinct().Count());
        Assert.All(cases, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("expectedClrShape").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("expectedIdentity").GetString()));
            Assert.NotEmpty(item.GetProperty("expectedRuleIds").EnumerateArray());
            Assert.Contains(item.GetProperty("expectedTier").GetString(),
                new[] { EvidenceTiers.Tier2Structural, EvidenceTiers.Tier4Unknown });
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("limitations").GetString()));
            Assert.Equal("ilRewriteGeneratorSha256", item.GetProperty("generatorSha256Property").GetString());
            Assert.Equal("ilRewriteBoundedInputSha256", item.GetProperty("boundedInputSha256Property").GetString());
            Assert.NotEmpty(item.GetProperty("nonClaims").EnumerateArray());
        });
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "catalog-digest");
        var result = Scan(before, after, temp);
        Assert.All(result.Facts.Where(fact => fact.RuleId == RuleIds.DotNetIlRewrite), fact =>
        {
            Assert.Matches("^[0-9a-f]{64}$", fact.Properties["ilRewriteGeneratorSha256"]);
            Assert.Matches("^[0-9a-f]{64}$", fact.Properties["ilRewriteBoundedInputSha256"]);
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties["limitation"]));
        });
    }

    [Fact]
    public void Field_and_typespec_operands_change_with_same_opcodes()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "field-type");
        Mutate(before, after, type =>
        {
            var field = type.Fields.Single(item => item.Name == "Second");
            Find(type, "ReadField").Body.Instructions.Single(item => item.OpCode == OpCodes.Ldsfld).Operand = field;
            var test = Find(type, "TypeTest").Body.Instructions.Single(item => item.OpCode == OpCodes.Isinst);
            var generic = (GenericInstanceType)test.Operand;
            generic.GenericArguments[0] = type.Module.TypeSystem.String;
        });
        var result = Scan(before, after, temp);
        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        foreach (var method in new[] { "ReadField", "TypeTest" })
        {
            var edge = Edge(result, method);
            Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
            Assert.Equal("true", edge.Properties["opcodeSequencePreserved"]);
            Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        }
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void InlineTok_member_operand_retargets_without_a_call_opcode()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "member-token");
        foreach (var (path, fieldName) in new[] { (before, "First"), (after, "Second") })
            Mutate(Fixture(), path, type =>
            {
                var method = new CecilMethodDefinition("FieldHandle",
                    Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                    type.Module.ImportReference(typeof(RuntimeFieldHandle)));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldtoken, type.Fields.Single(field => field.Name == fieldName)));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                type.Methods.Add(method);
            });
        var result = Scan(before, after, temp);
        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = Edge(result, "FieldHandle");
        Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
        Assert.Equal("0", edge.Properties["callRetargetCount"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Same_field_token_with_changed_full_signature_changes_body_identity()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "same-field-token");
        Mutate(before, after, type =>
        {
            var first = type.Fields.Single(field => field.Name == "First");
            var marker = type.Module.ImportReference(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute));
            first.FieldType = new OptionalModifierType(marker, first.FieldType);
        });
        var result = Scan(before, after, temp);
        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = Edge(result, "ReadField");
        Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
        Assert.Equal("false", edge.Properties["tokenRetargeted"]);
        Assert.Equal(edge.Properties["beforeMetadataToken"], edge.Properties["afterMetadataToken"]);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Methodspec_generic_instantiation_changes_without_opcode_change()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "methodspec");
        Mutate(before, after, type =>
        {
            var call = Find(type, "GenericCall").Body.Instructions.Single(item => item.OpCode == OpCodes.Call);
            ((GenericInstanceMethod)call.Operand).GenericArguments[0] = type.Module.TypeSystem.String;
        });
        var result = Scan(before, after, temp);
        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = Edge(result, "GenericCall");
        Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
        var retarget = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallRetargetObserved
            && fact.Properties.GetValueOrDefault("rewriteFactId") == edge.FactId);
        Assert.Contains("|gargs:<", retarget.Properties["afterTargetIdentity"], StringComparison.Ordinal);
        Assert.NotEqual(retarget.Properties["beforeTargetIdentity"], retarget.Properties["afterTargetIdentity"]);
    }

    [Fact]
    public void Property_and_event_declarations_retain_accessor_relationship_after_body_rewrite()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "accessors");
        Mutate(before, after, type =>
        {
            Find(type, "get_Value").Body.Instructions.Single(item => item.OpCode == OpCodes.Ldfld).Operand =
                type.Fields.Single(item => item.Name == "otherValue");
            var add = Find(type, "add_Changed");
            add.Body.GetILProcessor().InsertBefore(add.Body.Instructions.Last(), Instruction.Create(OpCodes.Nop));
        });
        var result = Scan(before, after, temp);
        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        Assert.Equal("operand-only-change", Edge(result, "get_Value").Properties["relationshipKind"]);
        Assert.Equal("instruction-stream-change", Edge(result, "add_Changed").Properties["relationshipKind"]);
        Assert.Equal("true", Edge(result, "set_Value").Properties["opcodeSequencePreserved"]);
        Assert.Equal("true", Edge(result, "remove_Changed").Properties["opcodeSequencePreserved"]);
        AssertAccessorMetadata(before, after, result);
    }

    [Fact]
    public void Inserted_and_removed_methods_are_explicit_membership_deltas()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "membership");
        Mutate(before, after, type =>
        {
            type.Methods.Remove(Find(type, "RemovedLater"));
            var inserted = new CecilMethodDefinition("InsertedLater",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                type.Module.TypeSystem.Void);
            inserted.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(inserted);
        });
        var result = Scan(before, after, temp);
        var gaps = result.Facts.Where(fact => fact.RuleId == RuleIds.DotNetIlRewriteGap).ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.Contains(gaps, fact => fact.Properties["gapKind"] == "IlRewriteMethodBeforeOnly"
            && fact.Properties["identity[0]"].Contains("method:12:RemovedLater|", StringComparison.Ordinal));
        Assert.Contains(gaps, fact => fact.Properties["gapKind"] == "IlRewriteMethodAfterOnly"
            && fact.Properties["identity[0]"].Contains("method:13:InsertedLater|", StringComparison.Ordinal));
        var assemblyIdentity = result.Manifest.IlRewriteProvenance!.Outcomes[0].BeforeAssemblyIdentity!;
        Assert.All(gaps, fact => Assert.StartsWith(assemblyIdentity + "|type:",
            fact.Properties["identity[0]"], StringComparison.Ordinal));
        Assert.Equal("membership-delta", result.Manifest.IlRewriteProvenance!.Outcomes[0].Outcome);
        Assert.DoesNotContain(gaps, fact => fact.Properties["gapKind"] == "IlRewriteIdentityAmbiguous");
    }

    [Fact]
    public void Calli_standalone_signature_records_calling_convention_identity()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "calli");
        Mutate(before, after, type =>
        {
            var calli = Find(type, "Indirect").Body.Instructions.Single(item => item.OpCode == OpCodes.Calli);
            ((CallSite)calli.Operand).CallingConvention = MethodCallingConvention.C;
        });
        var result = Scan(before, after, temp);
        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = Edge(result, "Indirect");
        Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
        var retarget = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallRetargetObserved
            && fact.Properties.GetValueOrDefault("rewriteFactId") == edge.FactId);
        Assert.Equal("calli", retarget.Properties["opcode"]);
        Assert.Contains("callconv:0", retarget.Properties["beforeTargetIdentity"], StringComparison.Ordinal);
        Assert.Contains("callconv:1", retarget.Properties["afterTargetIdentity"], StringComparison.Ordinal);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
        Assert.Equal(SignatureCallingConvention.Default, CalliConvention(before));
        Assert.Equal(SignatureCallingConvention.CDecl, CalliConvention(after));
    }

    [Fact]
    public void Vararg_calli_boundary_changes_with_equal_tokens_and_parameter_types()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "calli-vararg-boundary");
        foreach (var (path, optional) in new[] { (before, false), (after, true) })
            Mutate(Fixture(), path, type =>
            {
                var calli = Find(type, "Indirect").Body.Instructions.Single(item => item.OpCode == OpCodes.Calli);
                var signature = (CallSite)calli.Operand;
                signature.CallingConvention = MethodCallingConvention.VarArg;
                if (optional)
                    signature.Parameters[0].ParameterType = new SentinelType(signature.Parameters[0].ParameterType);
            });
        var result = Scan(before, after, temp);
        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = Edge(result, "Indirect");
        Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
        var retarget = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallRetargetObserved
            && fact.Properties.GetValueOrDefault("rewriteFactId") == edge.FactId);
        Assert.Equal("calli", retarget.Properties["opcode"]);
        Assert.Equal(retarget.Properties["beforeToken"], retarget.Properties["afterToken"]);
        Assert.Contains("callconv:5", retarget.Properties["beforeTargetIdentity"], StringComparison.Ordinal);
        Assert.Contains("callconv:5", retarget.Properties["afterTargetIdentity"], StringComparison.Ordinal);
        Assert.Contains("required:1", retarget.Properties["beforeTargetIdentity"], StringComparison.Ordinal);
        Assert.Contains("required:0", retarget.Properties["afterTargetIdentity"], StringComparison.Ordinal);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Required_and_optional_modifiers_remain_in_complete_method_identities()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "modifiers");
        Mutate(before, after, type =>
        {
            var method = Find(type, "ByReadonlyRef");
            var marker = type.Module.ImportReference(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute));
            method.Parameters[0].ParameterType = new OptionalModifierType(marker, method.Parameters[0].ParameterType);
            var required = Find(type, "OtherValue");
            required.ReturnType = new RequiredModifierType(marker, required.ReturnType);
        });
        var result = Scan(before, after, temp);
        Assert.Equal("rewrite-partial", result.Manifest.IlRewriteProvenance!.CoverageState);
        var gaps = result.Facts.Where(fact => fact.RuleId == RuleIds.DotNetIlRewriteGap).ToArray();
        Assert.Contains(gaps, fact => fact.Properties.GetValueOrDefault("gapKind") == "IlRewriteMethodBeforeOnly");
        Assert.Contains(gaps, fact => fact.Properties.GetValueOrDefault("gapKind") == "IlRewriteMethodAfterOnly"
            && fact.Properties.Values.Any(value => value.Contains("modopt", StringComparison.Ordinal)));
        Assert.DoesNotContain(gaps, fact => fact.Properties.GetValueOrDefault("gapKind") == "IlRewriteIdentityAmbiguous");
        Assert.Contains(gaps, fact => fact.Properties.GetValueOrDefault("gapKind") == "IlRewriteMethodAfterOnly"
            && fact.Properties.Values.Any(value => value.Contains("modreq", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("ReadField", 0x7e)] // InlineField
    [InlineData("Indirect", 0x29)]  // InlineSig
    public void Invalid_metadata_operand_token_withholds_the_pair(string method, byte opcode)
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "hostile-" + method);
        PatchOperandToken(after, method, opcode, (byte)(opcode == 0x29 ? 0x11 : 0x04));
        var pair = Assert.Single(IlRewriteEvidenceExtractor.Evaluate(new ScanOptions(
            Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp"),
            Path.Combine(temp.Path, "out"), IlRewriteEvidence: true,
            IlRewriteBeforePaths: [before], IlRewriteAfterPaths: [after])).Pairs);
        Assert.Empty(pair.Edges);
        Assert.Equal("malformed", pair.Outcome.Outcome);
        Assert.Contains(pair.SideFailures, failure => failure.Side == "after"
            && failure.GapKind == "IlRewriteMalformedInput");
    }

    [Fact]
    public void Unsupported_calli_convention_withholds_the_pair()
    {
        using var temp = new TempDirectory();
        var (before, after) = Pair(temp, "unsupported-calli");
        Mutate(before, after, type =>
        {
            var calli = Find(type, "Indirect").Body.Instructions.Single(item => item.OpCode == OpCodes.Calli);
            ((CallSite)calli.Operand).CallingConvention = (MethodCallingConvention)0x0f;
        });
        var pair = Assert.Single(IlRewriteEvidenceExtractor.Evaluate(new ScanOptions(
            Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp"),
            Path.Combine(temp.Path, "out"), IlRewriteEvidence: true,
            IlRewriteBeforePaths: [before], IlRewriteAfterPaths: [after])).Pairs);
        Assert.Empty(pair.Edges);
        Assert.Contains(pair.SideFailures, failure => failure.Side == "after"
            && failure.GapKind == "IlRewriteMalformedInput" && failure.Cause == "MalformedIlBody");
    }

    private static SignatureCallingConvention CalliConvention(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var body = BodyBytes(pe, reader, "Indirect");
        var offset = Array.IndexOf(body, (byte)0x29);
        Assert.True(offset >= 0);
        var token = BitConverter.ToInt32(body, offset + 1);
        var signature = reader.GetStandaloneSignature((StandaloneSignatureHandle)MetadataTokens.EntityHandle(token));
        return reader.GetBlobReader(signature.Signature).ReadSignatureHeader().CallingConvention;
    }

    private static void PatchOperandToken(string path, string methodName, byte opcode, byte table)
    {
        var bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var method = reader.MethodDefinitions.Select(reader.GetMethodDefinition)
            .Single(item => reader.GetString(item.Name) == methodName);
        var body = pe.GetMethodBody(method.RelativeVirtualAddress);
        var il = body.GetILBytes()!;
        var offset = Array.IndexOf(il, opcode);
        Assert.True(offset >= 0);
        var rva = method.RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders.Single(item => rva >= item.VirtualAddress
            && rva < item.VirtualAddress + Math.Max(item.VirtualSize, item.SizeOfRawData));
        var headerOffset = section.PointerToRawData + rva - section.VirtualAddress;
        var headerSize = (bytes[headerOffset] & 3) == 2 ? 1 : 12;
        var operand = headerOffset + headerSize + offset + 1;
        bytes[operand] = 0xff;
        bytes[operand + 1] = 0xff;
        bytes[operand + 2] = 0xff;
        bytes[operand + 3] = table;
        File.WriteAllBytes(path, bytes);
    }

    private static void AssertAccessorMetadata(string before, string after, ScanResult result)
    {
        foreach (var (path, side) in new[] { (before, "before"), (after, "after") })
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var reader = pe.GetMetadataReader();
            var property = reader.PropertyDefinitions.Select(reader.GetPropertyDefinition)
                .Single(item => reader.GetString(item.Name) == "Value");
            var accessors = property.GetAccessors();
            Assert.Equal("get_Value", reader.GetString(reader.GetMethodDefinition(accessors.Getter).Name));
            Assert.Equal("set_Value", reader.GetString(reader.GetMethodDefinition(accessors.Setter).Name));
            Assert.Equal(ManagedMetadataExtractor.Token(MetadataTokens.GetToken(accessors.Getter)),
                Edge(result, "get_Value").Properties[$"{side}MetadataToken"]);
            Assert.Equal(ManagedMetadataExtractor.Token(MetadataTokens.GetToken(accessors.Setter)),
                Edge(result, "set_Value").Properties[$"{side}MetadataToken"]);
            var @event = reader.EventDefinitions.Select(reader.GetEventDefinition)
                .Single(item => reader.GetString(item.Name) == "Changed");
            var eventAccessors = @event.GetAccessors();
            Assert.Equal("add_Changed", reader.GetString(reader.GetMethodDefinition(eventAccessors.Adder).Name));
            Assert.Equal("remove_Changed", reader.GetString(reader.GetMethodDefinition(eventAccessors.Remover).Name));
            Assert.Equal(ManagedMetadataExtractor.Token(MetadataTokens.GetToken(eventAccessors.Adder)),
                Edge(result, "add_Changed").Properties[$"{side}MetadataToken"]);
            Assert.Equal(ManagedMetadataExtractor.Token(MetadataTokens.GetToken(eventAccessors.Remover)),
                Edge(result, "remove_Changed").Properties[$"{side}MetadataToken"]);
        }
    }

    private static byte[] BodyBytes(PEReader pe, MetadataReader reader, string methodName)
    {
        var method = reader.MethodDefinitions.Select(reader.GetMethodDefinition)
            .Single(item => reader.GetString(item.Name) == methodName);
        return pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
    }

    private static (string Before, string After) Pair(TempDirectory temp, string role)
    {
        var before = Path.Combine(temp.Path, $"MemberShapes.{role}.before.dll");
        var after = Path.Combine(temp.Path, $"MemberShapes.{role}.after.dll");
        File.Copy(Fixture(), before);
        File.Copy(Fixture(), after);
        return (before, after);
    }

    private static string Fixture(string assemblyName = "CompiledEvidence.CSharp.MemberShapes.dll")
    {
        var activeConfiguration = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.Name;
        foreach (var configuration in new[] { activeConfiguration, "Debug", "Release" }
                     .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!)
                     .Distinct(StringComparer.Ordinal))
        {
            var path = Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp",
                "bin", configuration, "net10.0", assemblyName);
            if (File.Exists(path)) return path;
        }
        throw new InvalidOperationException($"Build the {assemblyName} fixture project first.");
    }

    private static void Mutate(string before, string after, Action<CecilTypeDefinition> change)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(before);
        change((CecilTypeDefinition)assembly.MainModule.GetType(FixtureType)!);
        assembly.Write(after);
    }

    private static CecilMethodDefinition Find(CecilTypeDefinition type, string name) =>
        type.Methods.Single(method => method.Name == name);

    private static ScanResult Scan(string before, string after, TempDirectory temp) =>
        ScanEngine.Scan(new ScanOptions(Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp"),
            Path.Combine(temp.Path, $"scan-{Path.GetRandomFileName()}", "out"),
            IlRewriteEvidence: true, IlRewriteBeforePaths: [before], IlRewriteAfterPaths: [after]));

    private static CodeFact Edge(ScanResult result, string name) => Assert.Single(result.Facts,
        fact => fact.FactType == FactTypes.ManagedIlRewriteObserved
            && fact.Properties.GetValueOrDefault("methodIdentity")!.Contains(
                $"method:{name.Length.ToString(CultureInfo.InvariantCulture)}:{name}|", StringComparison.Ordinal));

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
