using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class IlBodyEvidenceExtractorTests
{
    private const string ReceiverNamespace = "TraceMap.CompiledFixtures.Equivalence";
    private const string ReceiverSignature = "arity:0|call:default|hasThis:true|explicitThis:false|()->type(namespace:6:System|names:5:Int32)";

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp", "InvokeVirtual", "callvirt", "ReceiverBase")]
    [InlineData("csharp", "CompiledEvidence.CSharp", "InvokeBase", "call", "ReceiverBase")]
    [InlineData("vb", "CompiledEvidence.VisualBasic", "InvokeVirtual", "callvirt", "ReceiverDerived")]
    [InlineData("vb", "CompiledEvidence.VisualBasic", "InvokeBase", "call", "ReceiverBase")]
    [InlineData("vb", "CompiledEvidence.VisualBasic", "InvokeCurrent", "call", "ReceiverDerived")]
    public void Receiver_matrix_pins_encoded_dispatch_targets_and_body_links(string language, string assemblyName, string name, string opcode, string targetOwner)
    {
        var fixture = Fixture(language, assemblyName);
        var result = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [fixture.Assembly], IlBodyEvidence: true));
        var expectedAssembly = ReceiverAssembly(assemblyName);
        var caller = ReceiverMethod(expectedAssembly, "ReceiverDerived", name);
        var target = ReceiverMethod(expectedAssembly, targetOwner, "Read");
        using var pe = new PEReader(File.OpenRead(fixture.Assembly));
        var reader = pe.GetMetadataReader();
        var callerHandle = ReceiverHandle(reader, "ReceiverDerived", name);
        var targetHandle = ReceiverHandle(reader, targetOwner, "Read");
        var targetDefinition = reader.GetMethodDefinition(targetHandle);
        Assert.True(targetDefinition.Attributes.HasFlag(System.Reflection.MethodAttributes.Virtual));
        Assert.Equal(targetOwner == "ReceiverBase", targetDefinition.Attributes.HasFlag(System.Reflection.MethodAttributes.NewSlot));
        Assert.Equal(reader.GetMethodDefinition(ReceiverHandle(reader, "ReceiverBase", "Read")).GetDeclaringType(),
            reader.GetTypeDefinition(reader.GetMethodDefinition(callerHandle).GetDeclaringType()).BaseType);
        Assert.Equal(System.Reflection.MethodAttributes.Public, targetDefinition.Attributes & System.Reflection.MethodAttributes.MemberAccessMask);
        foreach (var handle in new[] { callerHandle, targetHandle })
        {
            var signature = reader.GetBlobReader(reader.GetMethodDefinition(handle).Signature);
            Assert.Equal(new byte[] { 0x20, 0, 8 }, signature.ReadBytes(signature.Length)); // instance () -> Int32
        }
        var raw = pe.GetMethodBody(reader.GetMethodDefinition(callerHandle).RelativeVirtualAddress).GetILBytes()!;
        var encoded = ReadReceiverCall(raw);
        Assert.Equal(opcode, encoded.Opcode);
        Assert.Equal(MetadataTokens.GetToken(targetHandle), encoded.Token);
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(fixture.Assembly);
        var cecilOwner = Assert.Single(module.Types, type => type.FullName == ReceiverNamespace + ".ReceiverDerived");
        var cecilMethod = Assert.Single(cecilOwner.Methods, method => method.Name == name);
        Assert.Equal(MetadataTokens.GetToken(callerHandle), cecilMethod.MetadataToken.ToInt32());
        var cecilCall = Assert.Single(cecilMethod.Body.Instructions, instruction => instruction.OpCode.Code is Mono.Cecil.Cil.Code.Call or Mono.Cecil.Cil.Code.Callvirt);
        Assert.Equal(encoded.Offset, cecilCall.Offset);
        Assert.Equal(opcode, cecilCall.OpCode.Name);
        var cecilTarget = Assert.IsAssignableFrom<Mono.Cecil.MethodReference>(cecilCall.Operand);
        Assert.Equal(encoded.Token, cecilTarget.MetadataToken.ToInt32());
        Assert.Equal(ReceiverNamespace + "." + targetOwner, cecilTarget.DeclaringType.FullName);
        Assert.Equal("Read", cecilTarget.Name);
        Assert.True(cecilTarget.HasThis);
        Assert.Empty(cecilTarget.Parameters);
        Assert.Equal(Mono.Cecil.MetadataType.Int32, cecilTarget.ReturnType.MetadataType);

        var methodFact = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == caller);
        var targetFact = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == target);
        Assert.Equal(ReceiverToken(callerHandle), methodFact.Properties["metadataToken"]);
        Assert.Equal(ReceiverToken(targetHandle), targetFact.Properties["metadataToken"]);
        var body = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties.GetValueOrDefault("compiledFactId") == methodFact.FactId);
        Assert.StartsWith(caller + "|il-body:instructions:", body.TargetSymbol);
        Assert.Matches("^[0-9a-f]{64}$", body.Properties["ilBodySha256"]);
        Assert.EndsWith(":sha256:" + body.Properties["ilBodySha256"], body.TargetSymbol);
        var call = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == body.FactId);
        Assert.Equal(ReceiverToken(callerHandle), call.Properties["metadataToken"]);
        Assert.Equal(ReceiverToken(targetHandle), call.Properties["referenceToken"]);
        Assert.Equal("methoddef", call.Properties["referenceKind"]);
        Assert.Equal(encoded.Offset.ToString(CultureInfo.InvariantCulture), call.Properties["ilOffset"]);
        Assert.Equal(opcode, call.Properties["opcode"]);
        Assert.Equal(target, call.Properties["targetIdentity"]);
        Assert.Equal(body.TargetSymbol + "|call:" + opcode + ":" + encoded.Offset.ToString(CultureInfo.InvariantCulture) + ":" + target, call.TargetSymbol);
        AssertReceiverEvidence(result, methodFact, fixture.Assembly, RuleIds.DotNetCompiledMember);
        AssertReceiverEvidence(result, targetFact, fixture.Assembly, RuleIds.DotNetCompiledMember);
        AssertReceiverEvidence(result, body, fixture.Assembly, RuleIds.DotNetIlBody);
        AssertReceiverEvidence(result, call, fixture.Assembly, RuleIds.DotNetIlCall);
        Assert.Null(call.SourceSymbol); // Encoded call observation, not a resolved runtime graph edge.
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == caller);
        Assert.DoesNotContain(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") is "MetadataReaderDisagreement" or "IlReaderDisagreement");
    }

    [Fact]
    public void Receiver_matrix_same_opcode_stream_preserves_distinct_target_operands_and_repeatability()
    {
        var fixture = Fixture("vb", "CompiledEvidence.VisualBasic");
        var options = new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [fixture.Assembly], IlBodyEvidence: true);
        var first = Scan(options);
        var repeated = Scan(options with { OutputPath = TempOutput() });
        Assert.Equal(JsonSerializer.Serialize(first.Facts), JsonSerializer.Serialize(repeated.Facts));
        Assert.Equal(JsonSerializer.Serialize(first.Manifest.IlBodyProvenance), JsonSerializer.Serialize(repeated.Manifest.IlBodyProvenance));
        using var pe = new PEReader(File.OpenRead(fixture.Assembly));
        var reader = pe.GetMetadataReader();
        var handles = new[] { ReceiverHandle(reader, "ReceiverDerived", "InvokeBase"), ReceiverHandle(reader, "ReceiverDerived", "InvokeCurrent") };
        var normalized = new List<byte[]>();
        var bodies = new List<CodeFact>();
        foreach (var handle in handles)
        {
            var bytes = pe.GetMethodBody(reader.GetMethodDefinition(handle).RelativeVirtualAddress).GetILBytes()!;
            var call = ReadReceiverCall(bytes);
            Assert.Equal("call", call.Opcode);
            var copy = bytes.ToArray();
            Array.Clear(copy, call.Offset + 1, 4); // Test-only comparison; never an identity digest.
            normalized.Add(copy);
            bodies.Add(Assert.Single(first.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties["metadataToken"] == ReceiverToken(handle)));
        }
        Assert.Equal(normalized[0], normalized[1]);
        Assert.NotEqual(bodies[0].Properties["instructionsSha256"], bodies[1].Properties["instructionsSha256"]);
        Assert.NotEqual(bodies[0].Properties["ilBodySha256"], bodies[1].Properties["ilBodySha256"]);
        Assert.NotEqual(bodies[0].TargetSymbol, bodies[1].TargetSymbol);
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp", 2)]
    [InlineData("vb", "CompiledEvidence.VisualBasic", 3)]
    public void Receiver_matrix_duplicate_malformed_and_budget_cases_remain_explicit(string language, string assemblyName, int callCount)
    {
        using var temp = new TempDirectory();
        var fixture = Fixture(language, assemblyName);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(fixture.Assembly, copy);
        var result = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [fixture.Assembly, copy], IlBodyEvidence: true));
        var rawHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copy))).ToLowerInvariant();
        var locators = new[] { Path.GetRelativePath(fixture.Source, fixture.Assembly).Replace('\\', '/'), "__external__/primary/" + rawHash[..12] + "-copy.dll" };
        Assert.Equal(locators.Order(StringComparer.Ordinal), result.Manifest.CompiledInputProvenance!.Outcomes.Select(outcome => outcome.SafeLocator).Order(StringComparer.Ordinal));
        foreach (var locator in locators)
        {
            var outcome = Assert.Single(result.Manifest.CompiledInputProvenance.Outcomes, input => input.SafeLocator == locator);
            Assert.Contains("AmbiguousDuplicateManagedAssembly", outcome.GapKinds);
            var gap = Assert.Single(result.Facts, fact => fact.Evidence.FilePath == locator && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly");
            Assert.Equal(RuleIds.DotNetCompiledGap, gap.RuleId);
            Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
            Assert.Equal(result.Manifest.CommitSha, gap.CommitSha);
            Assert.Equal(result.Manifest.RepoName, gap.Repo);
            Assert.Equal(1, gap.Evidence.StartLine);
            Assert.Equal(1, gap.Evidence.EndLine);
            Assert.Equal(nameof(ManagedMetadataExtractor), gap.Evidence.ExtractorId);
            Assert.Equal(ScannerVersions.ManagedMetadataExtractor, gap.Evidence.ExtractorVersion);
            Assert.Equal(result.Manifest.CompiledInputProvenance.BoundedInputSha256, gap.Properties["boundedInputSha256"]);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ManagedMetadataExtractor).Assembly.Location))).ToLowerInvariant(), gap.Properties["generatorSha256"]);
            Assert.Equal(outcome.ProvenanceBindingInputSha256, gap.Properties["provenanceBindingInputSha256"]);
            Assert.False(string.IsNullOrWhiteSpace(gap.Properties["limitation"]));
            Assert.Equal(rawHash, gap.Properties["rawFileSha256"]);
            var methods = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.Evidence.FilePath == locator
                && fact.TargetSymbol!.Contains("|names:15:ReceiverDerived|", StringComparison.Ordinal) && fact.Properties["metadataName"].StartsWith("Invoke", StringComparison.Ordinal)).ToArray();
            Assert.Equal(callCount, methods.Length);
            Assert.All(methods, method => Assert.NotEqual("eligible", method.Properties["sourceReconciliationEligibility"]));
            foreach (var method in methods)
            {
                var body = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties.GetValueOrDefault("compiledFactId") == method.FactId);
                var call = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == body.FactId);
                Assert.Equal(locator, body.Evidence.FilePath);
                Assert.Equal(locator, call.Evidence.FilePath);
                AssertReceiverEvidence(result, body, copy, RuleIds.DotNetIlBody);
                AssertReceiverEvidence(result, call, copy, RuleIds.DotNetIlCall);
                Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == method.TargetSymbol);
            }
        }
        var corrupted = File.ReadAllBytes(fixture.Assembly);
        using (var pe = new PEReader(new MemoryStream(corrupted)))
        {
            var reader = pe.GetMetadataReader();
            var handle = ReceiverHandle(reader, "ReceiverDerived", "InvokeBase");
            var definition = reader.GetMethodDefinition(handle);
            var call = ReadReceiverCall(pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()!);
            var start = FileOffset(pe, definition.RelativeVirtualAddress);
            var header = (corrupted[start] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(corrupted.AsSpan(start, 2)) >> 12) * 4;
            BinaryPrimitives.WriteInt32LittleEndian(corrupted.AsSpan(start + header + call.Offset + 1, 4), 0x02000001); // TypeDef is not a call operand.
        }
        var malformed = Path.Combine(temp.Path, "malformed.dll");
        File.WriteAllBytes(malformed, corrupted);
        Check(malformed, null, "IlCallTargetIdentityUnavailable");
        Check(fixture.Assembly, new IlBodyLimits(MaxTotalWorkUnits: 8), "IlTotalWorkLimitExceeded");
        void Check(string input, IlBodyLimits? limits, string expectedGap)
        {
            var rejected = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [input], IlBodyEvidence: true, IlBodyLimits: limits));
            Assert.Equal("il-partial", rejected.Manifest.IlBodyProvenance!.CoverageState);
            var ilGaps = rejected.Facts.Where(fact => fact.RuleId == RuleIds.DotNetIlGap).ToArray();
            Assert.Contains(expectedGap, ilGaps.Select(fact => fact.Properties["gapKind"]));
            var gap = Assert.Single(ilGaps, fact => fact.Properties["gapKind"] == expectedGap);
            Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
            Assert.Equal(rejected.Manifest.CommitSha, gap.CommitSha);
            Assert.Equal(rejected.Manifest.RepoName, gap.Repo);
            Assert.Equal(1, gap.Evidence.StartLine);
            Assert.Equal(1, gap.Evidence.EndLine);
            Assert.Equal(nameof(IlBodyEvidenceExtractor), gap.Evidence.ExtractorId);
            var outcome = Assert.Single(rejected.Manifest.IlBodyProvenance.Outcomes, item => item.SafeLocator == gap.Evidence.FilePath);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))).ToLowerInvariant(), gap.Properties["rawFileSha256"]);
            Assert.Equal(outcome.ProvenanceBindingInputSha256, gap.Properties["provenanceBindingInputSha256"]);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(IlBodyEvidenceExtractor).Assembly.Location))).ToLowerInvariant(), gap.Properties["ilGeneratorSha256"]);
            Assert.Equal(ScannerVersions.IlBodyEvidenceExtractor, gap.Evidence.ExtractorVersion);
            Assert.Equal(rejected.Manifest.IlBodyProvenance.BoundedInputSha256, gap.Properties["ilBoundedInputSha256"]);
            Assert.Equal(rejected.Manifest.IlBodyProvenance.GeneratorSha256, gap.Properties["ilGeneratorSha256"]);
            Assert.False(string.IsNullOrWhiteSpace(gap.Properties["limitation"]));
            Assert.DoesNotContain(rejected.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
        }
    }

    // Independent narrow decoder for these public compiler fixtures. Unknown
    // opcodes fail the oracle instead of searching arbitrary operand bytes for call.
    private static (int Offset, string Opcode, int Token) ReadReceiverCall(byte[] bytes)
    {
        var calls = new List<(int Offset, string Opcode, int Token)>();
        for (var offset = 0; offset < bytes.Length;)
        {
            var start = offset;
            var opcode = bytes[offset++];
            switch (opcode)
            {
                case 0x00: case 0x02: case 0x06: case 0x0a: case 0x2a: break; // nop, ldarg.0, ldloc.0, stloc.0, ret
                case 0x2b: Assert.True(offset < bytes.Length); offset++; break; // br.s
                case 0x28: case 0x6f:
                    Assert.True(offset + 4 <= bytes.Length);
                    calls.Add((start, opcode == 0x28 ? "call" : "callvirt", BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4))));
                    offset += 4;
                    break;
                default: Assert.Fail("Unexpected receiver fixture opcode: " + opcode.ToString("x2", CultureInfo.InvariantCulture)); break;
            }
        }
        return Assert.Single(calls);
    }

    private static MethodDefinitionHandle ReceiverHandle(MetadataReader reader, string owner, string name)
    {
        var type = Assert.Single(reader.TypeDefinitions, handle => reader.GetString(reader.GetTypeDefinition(handle).Namespace) == ReceiverNamespace && reader.GetString(reader.GetTypeDefinition(handle).Name) == owner);
        return Assert.Single(reader.GetTypeDefinition(type).GetMethods(), handle => reader.GetString(reader.GetMethodDefinition(handle).Name) == name);
    }
    private static string ReceiverToken(MethodDefinitionHandle handle) => "0x" + MetadataTokens.GetToken(handle).ToString("x8", CultureInfo.InvariantCulture);
    private static string ReceiverAssembly(string name) => "assembly:name:" + name.Length + ":" + name + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:" + (name.Length + 4) + ":" + name + ".dll|targetFramework:25:.NETCoreApp,Version=v10.0";
    private static string ReceiverMethod(string assembly, string owner, string name) => assembly + "|type:namespace:37:" + ReceiverNamespace + "|names:" + owner.Length + ":" + owner + "|arity:0|method:" + name.Length + ":" + name + "|" + ReceiverSignature;

    private static void AssertReceiverEvidence(ScanResult result, CodeFact fact, string assembly, string rule)
    {
        Assert.Equal(rule, fact.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
        Assert.Equal(result.Manifest.CommitSha, fact.CommitSha);
        Assert.Equal(result.Manifest.RepoName, fact.Repo);
        Assert.Matches("^[0-9a-f]{40}$", fact.CommitSha);
        Assert.Equal(1, fact.Evidence.StartLine);
        Assert.Equal(1, fact.Evidence.EndLine);
        Assert.Null(fact.Evidence.SnippetHash);
        Assert.False(string.IsNullOrWhiteSpace(fact.Properties["limitation"]));
        var compiled = result.Manifest.CompiledInputProvenance!;
        var input = Assert.Single(compiled.Outcomes, outcome => outcome.SafeLocator == fact.Evidence.FilePath);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))).ToLowerInvariant(), fact.Properties["rawFileSha256"]);
        Assert.Equal(input.ProvenanceBindingInputSha256, fact.Properties["provenanceBindingInputSha256"]);
        var il = rule != RuleIds.DotNetCompiledMember;
        var provenance = result.Manifest.IlBodyProvenance!;
        Assert.Equal(il ? nameof(IlBodyEvidenceExtractor) : nameof(ManagedMetadataExtractor), fact.Evidence.ExtractorId);
        Assert.Equal(il ? ScannerVersions.IlBodyEvidenceExtractor : ScannerVersions.ManagedMetadataExtractor, fact.Evidence.ExtractorVersion);
        Assert.Equal(il ? "managed-il-v1" : ManagedMetadataExtractor.MetadataLocationKind, fact.Properties["evidenceLocationKind"]);
        Assert.Equal(il ? provenance.BoundedInputSha256 : compiled.BoundedInputSha256, fact.Properties[il ? "ilBoundedInputSha256" : "boundedInputSha256"]);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(IlBodyEvidenceExtractor).Assembly.Location))).ToLowerInvariant(), fact.Properties[il ? "ilGeneratorSha256" : "generatorSha256"]);
    }
}
