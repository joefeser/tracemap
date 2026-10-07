using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class IlBodyEvidenceExtractorTests
{
    private const string LateAssembly = "CompiledEvidence.VisualBasic";
    private const string LateStatic = "arity:0|call:default|hasThis:false|explicitThis:false|";
    private static string LateMethod(string name) => EventMethod(ReceiverAssembly(LateAssembly), "LateBindingMatrix", name,
        LateStatic + "(" + (name == "ReadDirect"
            ? "scope(assembly:name:28:CompiledEvidence.VisualBasic|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null)type(namespace:37:TraceMap.CompiledFixtures.Equivalence|names:17:LateBindingTarget)"
            : CollectionObject) + ")->" + CollectionObject);
    private static string LateTarget(string name) => EventMethod(ReceiverAssembly(LateAssembly), "LateBindingTarget", name, EventInstance + "()->" + CollectionObject);

    [Fact]
    public void Late_binding_matrix_preserves_helper_calls_strings_and_source_gaps()
    {
        var fixture = Fixture("vb", LateAssembly);
        var options = new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [fixture.Assembly], IlBodyEvidence: true);
        var scan = Scan(options);
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        using var pe = new PEReader(File.OpenRead(fixture.Assembly));
        var reader = pe.GetMetadataReader();
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(fixture.Assembly);
        var owner = Assert.Single(module.Types, type => type.FullName == ReceiverNamespace + ".LateBindingMatrix");
        var bodies = new Dictionary<string, CodeFact>();
        var streams = new Dictionary<string, short[]>();
        foreach (var (name, memberName, line) in new[] { ("ReadFirst", "First", 15), ("ReadSecond", "Second", 18), ("ReadDirect", "First", 21) })
        {
            var direct = name == "ReadDirect";
            var handle = ReceiverHandle(reader, "LateBindingMatrix", name);
            var definition = reader.GetMethodDefinition(handle);
            var signature = reader.GetBlobReader(definition.Signature);
            Assert.Equal(new byte[] { 0, 1, 0x1c }, signature.ReadBytes(3)); // static (one argument) -> Object
            Assert.Equal(direct ? 0x12 : 0x1c, signature.ReadByte());
            var targetHandle = ReceiverHandle(reader, "LateBindingTarget", memberName);
            if (direct) Assert.Equal(reader.GetMethodDefinition(targetHandle).GetDeclaringType(), signature.ReadTypeHandle());
            Assert.Equal(0, signature.RemainingBytes);
            var cecil = Assert.Single(owner.Methods, method => method.MetadataToken.ToInt32() == MetadataTokens.GetToken(handle));
            Assert.Equal(name, cecil.Name);
            Assert.True(cecil.IsStatic);
            Assert.Equal("System.Object", cecil.ReturnType.FullName);
            Assert.Equal(direct ? ReceiverNamespace + ".LateBindingTarget" : "System.Object", Assert.Single(cecil.Parameters).ParameterType.FullName);
            var decoded = DecodeEventBody(pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()!);
            streams[name] = decoded.Select(instruction => instruction.OpCode.Value).ToArray();
            Assert.Equal(decoded.Select(instruction => (instruction.Offset, instruction.OpCode.Value)), cecil.Body.Instructions.Select(instruction => (instruction.Offset, instruction.OpCode.Value)));
            var encoded = Assert.Single(decoded, instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt);
            Assert.Equal(direct ? OpCodes.Callvirt : OpCodes.Call, encoded.OpCode);
            var independent = Assert.IsAssignableFrom<Mono.Cecil.MethodReference>(Assert.Single(cecil.Body.Instructions, instruction => instruction.Offset == encoded.Offset).Operand);
            Assert.Equal(encoded.Token, independent.MetadataToken.ToInt32());
            Assert.Equal("System.Object", independent.ReturnType.FullName);
            string targetIdentity;
            if (direct)
            {
                Assert.Equal(MetadataTokens.GetToken(targetHandle), encoded.Token);
                Assert.Equal(new byte[] { 0x20, 0, 0x1c }, reader.GetBlobBytes(reader.GetMethodDefinition(targetHandle).Signature));
                Assert.True(independent.HasThis);
                Assert.Empty(independent.Parameters);
                Assert.Equal("First", independent.Name);
                Assert.Equal(ReceiverNamespace + ".LateBindingTarget", independent.DeclaringType.FullName);
                targetIdentity = LateTarget("First");
                var targetFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == targetIdentity);
                Assert.Equal(ReceiverToken(targetHandle), targetFact.Properties["metadataToken"]);
                AssertReceiverEvidence(scan, targetFact, fixture.Assembly, RuleIds.DotNetCompiledMember);
                Assert.DoesNotContain(decoded, instruction => instruction.OpCode == OpCodes.Ldstr);
            }
            else
            {
                Assert.Equal(HandleKind.MemberReference, MetadataTokens.EntityHandle(encoded.Token).Kind);
                var reference = reader.GetMemberReference((MemberReferenceHandle)MetadataTokens.EntityHandle(encoded.Token));
                Assert.Equal("LateGet", reader.GetString(reference.Name));
                CheckType(reference.Parent, "Microsoft.VisualBasic.Core", "Microsoft.VisualBasic.CompilerServices", "NewLateBinding", new Version(15, 0, 0, 0));
                var raw = reader.GetBlobReader(reference.Signature);
                Assert.Equal(new byte[] { 0, 7, 0x1c, 0x1c, 0x12 }, raw.ReadBytes(5));
                var systemType = raw.ReadTypeHandle();
                CheckType(systemType, "System.Runtime", "System", "Type", new Version(10, 0, 0, 0));
                Assert.Equal(new byte[] { 0x0e, 0x1d, 0x1c, 0x1d, 0x0e, 0x1d, 0x12 }, raw.ReadBytes(7));
                Assert.Equal(systemType, raw.ReadTypeHandle());
                Assert.Equal(new byte[] { 0x1d, 2 }, raw.ReadBytes(2));
                Assert.Equal(0, raw.RemainingBytes);
                Assert.Equal("LateGet", independent.Name);
                Assert.False(independent.HasThis || independent.ExplicitThis);
                Assert.Equal("Microsoft.VisualBasic.CompilerServices.NewLateBinding", independent.DeclaringType.FullName);
                Assert.Equal("Microsoft.VisualBasic.Core, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", independent.DeclaringType.Scope.ToString());
                Assert.Equal(new[] { "System.Object", "System.Type", "System.String", "System.Object[]", "System.String[]", "System.Type[]", "System.Boolean[]" }, independent.Parameters.Select(parameter => parameter.ParameterType.FullName));
                var text = Assert.Single(decoded, instruction => instruction.OpCode == OpCodes.Ldstr);
                Assert.Equal(0x70000000, text.Token & unchecked((int)0xff000000));
                Assert.Equal(memberName, reader.GetUserString(MetadataTokens.UserStringHandle(text.Token & 0x00ffffff)));
                Assert.Equal(memberName, Assert.IsType<string>(Assert.Single(cecil.Body.Instructions, instruction => instruction.Offset == text.Offset).Operand));
                var typeIdentity = CollectionType("System.Runtime", "System", "Type");
                var stringIdentity = "type(namespace:6:System|names:6:String)";
                targetIdentity = "memberref|type:" + CollectionType("Microsoft.VisualBasic.Core", "Microsoft.VisualBasic.CompilerServices", "NewLateBinding", "15.0.0.0")
                    + "|member:7:LateGet|" + LateStatic + "(" + CollectionObject + "," + typeIdentity + "," + stringIdentity + ","
                    + CollectionObject + "[]," + stringIdentity + "[]," + typeIdentity + "[],type(namespace:6:System|names:7:Boolean)[])->" + CollectionObject;
            }
            var member = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == LateMethod(name));
            Assert.Equal(ReceiverToken(handle), member.Properties["metadataToken"]);
            AssertReceiverEvidence(scan, member, fixture.Assembly, RuleIds.DotNetCompiledMember);
            var body = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties.GetValueOrDefault("compiledFactId") == member.FactId);
            Assert.StartsWith(LateMethod(name) + "|il-body:instructions:", body.TargetSymbol);
            Assert.EndsWith(":sha256:" + body.Properties["ilBodySha256"], body.TargetSymbol);
            AssertReceiverEvidence(scan, body, fixture.Assembly, RuleIds.DotNetIlBody);
            bodies[name] = body;
            // Exactly one encoded call: names in strings must not create calls to First/Second.
            var call = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == body.FactId);
            var opcode = direct ? "callvirt" : "call";
            Assert.Equal(targetIdentity, call.Properties["targetIdentity"]);
            Assert.Equal(ReceiverToken(handle), call.Properties["metadataToken"]);
            Assert.Equal($"0x{encoded.Token:x8}", call.Properties["referenceToken"]);
            Assert.Equal(direct ? "methoddef" : "memberref", call.Properties["referenceKind"]);
            Assert.Equal(opcode, call.Properties["opcode"]);
            Assert.Equal(encoded.Offset.ToString(CultureInfo.InvariantCulture), call.Properties["ilOffset"]);
            Assert.Equal(body.TargetSymbol + "|call:" + opcode + ":" + encoded.Offset.ToString(CultureInfo.InvariantCulture) + ":" + targetIdentity, call.TargetSymbol);
            Assert.Null(call.SourceSymbol);
            AssertReceiverEvidence(scan, call, fixture.Assembly, RuleIds.DotNetIlCall);
            Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == member.TargetSymbol);
            var site = scan.Facts.Where(fact => fact.Evidence.FilePath == "LateBindingMatrix.vb" && fact.Evidence.StartLine == line).ToArray();
            var edge = Assert.Single(site, fact => fact.FactType == FactTypes.CallEdge);
            Assert.Equal(direct ? RuleIds.VisualBasicSemanticCallGraph : RuleIds.VisualBasicSyntaxCallGraph, edge.RuleId);
            Assert.Equal(direct ? EvidenceTiers.Tier1Semantic : EvidenceTiers.Tier3SyntaxOrTextual, edge.EvidenceTier);
            Assert.Equal("Global." + ReceiverNamespace + ".LateBindingMatrix." + name + "(value As " + (direct ? "Global." + ReceiverNamespace + ".LateBindingTarget" : "Object") + ")", edge.SourceSymbol);
            Assert.Equal(direct ? "Global." + ReceiverNamespace + ".LateBindingTarget.First()" : memberName, edge.TargetSymbol);
            CheckSource(edge, direct);
            if (!direct)
            {
                Assert.DoesNotContain(site, fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic);
                var gap = Assert.Single(site, fact => fact.Properties.GetValueOrDefault("gapKind") == "CallSiteSemanticResolutionUnavailable");
                Assert.Equal(RuleIds.VisualBasicSemanticMethodInvocation, gap.RuleId);
                Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
                Assert.Null(gap.SourceSymbol);
                Assert.Null(gap.TargetSymbol);
                var expectedHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("value." + memberName))).ToLowerInvariant()[..32];
                Assert.Equal(expectedHash, gap.Properties["siteHash"]);
                CheckSource(gap, true);
            }
            void CheckSource(CodeFact fact, bool semantic)
            {
                Assert.Equal(line, fact.Evidence.EndLine);
                Assert.Equal("CompiledEvidence.VisualBasic.vbproj", fact.ProjectPath);
                Assert.Equal(scan.Manifest.CommitSha, fact.CommitSha);
                Assert.Equal(scan.Manifest.RepoName, fact.Repo);
                Assert.Equal(semantic ? nameof(VisualBasicSemanticExtractor) : nameof(VisualBasicSyntaxExtractor), fact.Evidence.ExtractorId);
                Assert.Equal(semantic ? ScannerVersions.VisualBasicSemanticExtractor : ScannerVersions.VisualBasicSyntaxExtractor, fact.Evidence.ExtractorVersion);
            }
        }
        Assert.Equal(streams["ReadFirst"], streams["ReadSecond"]);
        Assert.NotEqual(bodies["ReadFirst"].Properties["ilBodySha256"], bodies["ReadSecond"].Properties["ilBodySha256"]);
        Assert.DoesNotContain(scan.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") is "MetadataReaderDisagreement" or "IlReaderDisagreement");
        var repeat = Scan(options with { OutputPath = TempOutput() });
        Assert.Equal(JsonSerializer.Serialize(scan.Facts), JsonSerializer.Serialize(repeat.Facts));
        Assert.Equal(JsonSerializer.Serialize(scan.Manifest.IlBodyProvenance), JsonSerializer.Serialize(repeat.Manifest.IlBodyProvenance));

        void CheckType(EntityHandle handle, string assembly, string ns, string name, Version version)
        {
            Assert.Equal(HandleKind.TypeReference, handle.Kind);
            var type = reader.GetTypeReference((TypeReferenceHandle)handle);
            Assert.Equal(ns, reader.GetString(type.Namespace));
            Assert.Equal(name, reader.GetString(type.Name));
            Assert.Equal(HandleKind.AssemblyReference, type.ResolutionScope.Kind);
            var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
            Assert.Equal(assembly, reader.GetString(scope.Name));
            Assert.Equal(version, scope.Version);
            Assert.Equal("", reader.GetString(scope.Culture));
            Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
        }
    }

    [Fact]
    public void Late_binding_matrix_string_only_mutation_changes_body_without_inventing_target()
    {
        var fixture = Fixture("vb", LateAssembly);
        using var temp = new TempDirectory();
        using var pe = new PEReader(File.OpenRead(fixture.Assembly));
        var reader = pe.GetMetadataReader();
        var first = reader.GetMethodDefinition(ReceiverHandle(reader, "LateBindingMatrix", "ReadFirst"));
        var second = reader.GetMethodDefinition(ReceiverHandle(reader, "LateBindingMatrix", "ReadSecond"));
        var originalBody = pe.GetMethodBody(first.RelativeVirtualAddress).GetILBytes()!;
        var otherBody = pe.GetMethodBody(second.RelativeVirtualAddress).GetILBytes()!;
        var originalString = Assert.Single(DecodeEventBody(originalBody), instruction => instruction.OpCode == OpCodes.Ldstr);
        var otherString = Assert.Single(DecodeEventBody(otherBody), instruction => instruction.OpCode == OpCodes.Ldstr);
        var bytes = File.ReadAllBytes(fixture.Assembly);
        var start = FileOffset(pe, first.RelativeVirtualAddress);
        var header = (bytes[start] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start, 2)) >> 12) * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(start + header + originalString.Offset + 1, 4), otherString.Token);
        var mutated = Path.Combine(temp.Path, "string-only.dll");
        File.WriteAllBytes(mutated, bytes); // Data only; no assembly execution.
        using var changedPe = new PEReader(File.OpenRead(mutated));
        Assert.Equal(otherBody, changedPe.GetMethodBody(first.RelativeVirtualAddress).GetILBytes());
        using var cecil = Mono.Cecil.ModuleDefinition.ReadModule(mutated);
        var method = Assert.Single(Assert.Single(cecil.Types, type => type.FullName == ReceiverNamespace + ".LateBindingMatrix").Methods, method => method.Name == "ReadFirst");
        Assert.Equal("Second", Assert.Single(method.Body.Instructions, instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldstr).Operand);
        var original = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [fixture.Assembly], IlBodyEvidence: true));
        var changed = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [mutated], IlBodyEvidence: true));
        var prior = Body(original, "ReadFirst", fixture.Assembly);
        var current = Body(changed, "ReadFirst", mutated);
        Assert.NotEqual(prior.Properties["ilBodySha256"], current.Properties["ilBodySha256"]);
        Assert.Equal(Body(changed, "ReadSecond", mutated).Properties["ilBodySha256"], current.Properties["ilBodySha256"]);
        var priorCall = Assert.Single(original.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == prior.FactId);
        var currentCall = Assert.Single(changed.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == current.FactId);
        Assert.Equal(priorCall.Properties["targetIdentity"], currentCall.Properties["targetIdentity"]);
        AssertReceiverEvidence(original, priorCall, fixture.Assembly, RuleIds.DotNetIlCall);
        AssertReceiverEvidence(changed, currentCall, mutated, RuleIds.DotNetIlCall);
        Assert.NotEqual(LateTarget("Second"), currentCall.Properties["targetIdentity"]);
        Assert.DoesNotContain(changed.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == LateMethod("ReadFirst"));
        static CodeFact Body(ScanResult scan, string name, string input)
        {
            var member = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == LateMethod(name));
            var body = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties.GetValueOrDefault("compiledFactId") == member.FactId);
            AssertReceiverEvidence(scan, member, input, RuleIds.DotNetCompiledMember);
            AssertReceiverEvidence(scan, body, input, RuleIds.DotNetIlBody);
            return body;
        }
    }

    [Fact]
    public void Late_binding_matrix_duplicate_inputs_keep_separate_evidence_and_ambiguity()
    {
        var fixture = Fixture("vb", LateAssembly);
        using var temp = new TempDirectory();
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(fixture.Assembly, copy);
        var scan = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [fixture.Assembly, copy], IlBodyEvidence: true));
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copy))).ToLowerInvariant();
        var locators = new[] { Path.GetRelativePath(fixture.Source, fixture.Assembly).Replace('\\', '/'), "__external__/primary/" + hash[..12] + "-copy.dll" };
        Assert.Equal(locators.Order(StringComparer.Ordinal), scan.Manifest.CompiledInputProvenance!.Outcomes.Select(input => input.SafeLocator).Order(StringComparer.Ordinal));
        foreach (var locator in locators)
        {
            CheckEventGap(scan, Assert.Single(scan.Facts, fact => fact.Evidence.FilePath == locator && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly"), copy, il: false);
            foreach (var name in new[] { "ReadFirst", "ReadSecond", "ReadDirect" })
            {
                var member = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.Evidence.FilePath == locator && fact.TargetSymbol == LateMethod(name));
                Assert.NotEqual("eligible", member.Properties["sourceReconciliationEligibility"]);
                AssertReceiverEvidence(scan, member, copy, RuleIds.DotNetCompiledMember);
                var body = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties.GetValueOrDefault("compiledFactId") == member.FactId);
                var call = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == body.FactId);
                Assert.Equal(locator, body.Evidence.FilePath);
                Assert.Equal(locator, call.Evidence.FilePath);
                AssertReceiverEvidence(scan, body, copy, RuleIds.DotNetIlBody);
                AssertReceiverEvidence(scan, call, copy, RuleIds.DotNetIlCall);
                Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == member.TargetSymbol);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Late_binding_matrix_malformed_helper_and_work_limit_remain_gaps(bool bounded)
    {
        var fixture = Fixture("vb", LateAssembly);
        using var temp = new TempDirectory();
        var input = fixture.Assembly;
        if (!bounded)
        {
            using var pe = new PEReader(File.OpenRead(input));
            var reader = pe.GetMetadataReader();
            var definition = reader.GetMethodDefinition(ReceiverHandle(reader, "LateBindingMatrix", "ReadFirst"));
            var operand = Assert.Single(DecodeEventBody(pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()!), instruction => instruction.OpCode == OpCodes.Call);
            var bytes = File.ReadAllBytes(input);
            var start = FileOffset(pe, definition.RelativeVirtualAddress);
            var header = (bytes[start] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start, 2)) >> 12) * 4;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(start + header + operand.Offset + 1, 4), 0x02000001);
            input = Path.Combine(temp.Path, "malformed.dll");
            File.WriteAllBytes(input, bytes); // Data only, never execute.
        }
        var scan = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [input], IlBodyEvidence: true,
            IlBodyLimits: bounded ? new IlBodyLimits(MaxTotalWorkUnits: 8) : null));
        Assert.Equal("il-partial", scan.Manifest.IlBodyProvenance!.CoverageState);
        var kind = bounded ? "IlTotalWorkLimitExceeded" : "IlCallTargetIdentityUnavailable";
        CheckEventGap(scan, Assert.Single(scan.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), input, il: true);
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
    }
}
