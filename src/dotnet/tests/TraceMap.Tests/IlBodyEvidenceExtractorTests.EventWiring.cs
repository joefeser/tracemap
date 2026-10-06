using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
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
    private const string EventVoid = "type(namespace:6:System|names:4:Void)";
    private const string EventAction = "scope(assembly:name:14:System.Runtime|version:8:10.0.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)type(namespace:6:System|names:6:Action)";
    private const string EventInstance = "arity:0|call:default|hasThis:true|explicitThis:false|";

    private static string EventMethod(string assembly, string owner, string name, string signature) => assembly
        + "|type:namespace:37:" + ReceiverNamespace + "|names:" + owner.Length + ":" + owner
        + "|arity:0|method:" + name.Length + ":" + name + "|" + signature;

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp", false)]
    [InlineData("vb", "CompiledEvidence.VisualBasic", true)]
    public void Event_wiring_matrix_pins_accessors_and_encoded_subscriptions(string language, string assemblyName, bool synchronized)
    {
        var fixture = Fixture(language, assemblyName);
        var options = new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [fixture.Assembly], IlBodyEvidence: true);
        var scan = Scan(options);
        using var pe = new PEReader(File.OpenRead(fixture.Assembly));
        var reader = pe.GetMetadataReader();
        var setter = ReceiverHandle(reader, "EventSubscriber", "set_Source");
        var getter = ReceiverHandle(reader, "EventSubscriber", "get_Source");
        var handler = ReceiverHandle(reader, "EventSubscriber", "OnTick");
        var subscriber = reader.GetMethodDefinition(setter).GetDeclaringType();
        var propertyHandle = Assert.Single(reader.GetTypeDefinition(subscriber).GetProperties());
        var property = reader.GetPropertyDefinition(propertyHandle);
        Assert.Equal("Source", reader.GetString(property.Name));
        Assert.Equal(setter, property.GetAccessors().Setter);
        Assert.Equal(getter, property.GetAccessors().Getter);
        Assert.Empty(property.GetAccessors().Others);
        var definition = reader.GetMethodDefinition(setter);
        Assert.Equal(synchronized, definition.ImplAttributes.HasFlag(MethodImplAttributes.Synchronized));
        var emitter = reader.GetMethodDefinition(ReceiverHandle(reader, "EventEmitter", "add_Tick")).GetDeclaringType();
        var eventHandle = Assert.Single(reader.GetTypeDefinition(emitter).GetEvents());
        var eventDefinition = reader.GetEventDefinition(eventHandle);
        Assert.Equal("Tick", reader.GetString(eventDefinition.Name));
        var targets = new[] { eventDefinition.GetAccessors().Remover, eventDefinition.GetAccessors().Adder };
        Assert.All(targets, target => Assert.False(target.IsNil));
        Assert.Equal(new[] { "remove_Tick", "add_Tick" }, targets.Select(target => reader.GetString(reader.GetMethodDefinition(target).Name)));
        CheckLocalSignature(definition.Signature, true, false);
        CheckLocalSignature(reader.GetMethodDefinition(getter).Signature, false, false);
        CheckLocalSignature(property.Signature, false, true);
        foreach (var target in targets)
        {
            var blob = reader.GetBlobReader(reader.GetMethodDefinition(target).Signature);
            Assert.Equal(new byte[] { 0x20, 1, 1, 0x12 }, blob.ReadBytes(4));
            Assert.Equal(eventDefinition.Type, blob.ReadTypeHandle());
            Assert.Equal(0, blob.RemainingBytes);
        }
        Assert.Equal(HandleKind.TypeReference, eventDefinition.Type.Kind);
        var action = reader.GetTypeReference((TypeReferenceHandle)eventDefinition.Type);
        Assert.Equal("System", reader.GetString(action.Namespace));
        Assert.Equal("Action", reader.GetString(action.Name));
        Assert.Equal(HandleKind.AssemblyReference, action.ResolutionScope.Kind);
        var runtime = reader.GetAssemblyReference((AssemblyReferenceHandle)action.ResolutionScope);
        Assert.Equal("System.Runtime", reader.GetString(runtime.Name));
        Assert.Equal(new Version(10, 0, 0, 0), runtime.Version);
        Assert.Equal("", reader.GetString(runtime.Culture));
        Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(runtime.PublicKeyOrToken)).ToLowerInvariant());
        var bytes = pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()!;
        var decoded = DecodeEventBody(bytes);
        var subscriptions = decoded.Where(instruction => instruction.OpCode == OpCodes.Callvirt
            && targets.Any(target => MetadataTokens.GetToken(target) == instruction.Token)).ToArray();
        Assert.Equal(2, subscriptions.Length);
        Assert.Equal(targets.Select(target => MetadataTokens.GetToken(target)), subscriptions.Select(instruction => instruction.Token));
        var handlerLoads = decoded.Where(instruction => instruction.OpCode == OpCodes.Ldftn).ToArray();
        Assert.Equal(synchronized ? 1 : 2, handlerLoads.Length);
        Assert.All(handlerLoads, instruction => Assert.Equal(MetadataTokens.GetToken(handler), instruction.Token));
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(fixture.Assembly);
        var cecilSubscriber = Assert.Single(module.Types, type => type.FullName == ReceiverNamespace + ".EventSubscriber");
        var cecilProperty = Assert.Single(cecilSubscriber.Properties);
        Assert.Equal(MetadataTokens.GetToken(propertyHandle), cecilProperty.MetadataToken.ToInt32());
        Assert.Equal(MetadataTokens.GetToken(getter), cecilProperty.GetMethod.MetadataToken.ToInt32());
        var cecilSetter = cecilProperty.SetMethod;
        Assert.Equal(MetadataTokens.GetToken(setter), cecilSetter.MetadataToken.ToInt32());
        Assert.Equal(synchronized, cecilSetter.IsSynchronized);
        Assert.Equal("Source", cecilProperty.Name);
        Assert.Equal(ReceiverNamespace + ".EventEmitter", cecilProperty.PropertyType.FullName);
        Assert.Equal(ReceiverNamespace + ".EventEmitter", Assert.Single(cecilSetter.Parameters).ParameterType.FullName);
        Assert.Equal("System.Void", cecilSetter.ReturnType.FullName);
        var cecilEmitter = Assert.Single(module.Types, type => type.FullName == ReceiverNamespace + ".EventEmitter");
        var cecilEvent = Assert.Single(cecilEmitter.Events);
        Assert.Equal(MetadataTokens.GetToken(eventHandle), cecilEvent.MetadataToken.ToInt32());
        Assert.Equal("System.Action", cecilEvent.EventType.FullName);
        Assert.Equal(MetadataTokens.GetToken(targets[0]), cecilEvent.RemoveMethod.MetadataToken.ToInt32());
        Assert.Equal(MetadataTokens.GetToken(targets[1]), cecilEvent.AddMethod.MetadataToken.ToInt32());
        var assembly = ReceiverAssembly(assemblyName);
        var self = "scope(assembly:name:" + assemblyName.Length + ":" + assemblyName + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null)type(namespace:37:" + ReceiverNamespace + "|names:12:EventEmitter)";
        var caller = EventMethod(assembly, "EventSubscriber", "set_Source", EventInstance + "(" + self + ")->" + EventVoid);
        var owner = assembly + "|type:namespace:37:" + ReceiverNamespace + "|names:15:EventSubscriber|arity:0";
        var propertyFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedPropertyDeclared && fact.TargetSymbol == owner + "|property:6:Source|call:default|hasThis:true|()->" + self);
        Assert.Equal(FactTypes.ManagedPropertyDeclared, propertyFact.FactType);
        Assert.Equal($"0x{MetadataTokens.GetToken(propertyHandle):x8}", propertyFact.Properties["metadataToken"]);
        AssertReceiverEvidence(scan, propertyFact, fixture.Assembly, RuleIds.DotNetCompiledMember);
        var getterFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == EventMethod(assembly, "EventSubscriber", "get_Source", EventInstance + "()->" + self));
        Assert.Equal(ReceiverToken(getter), getterFact.Properties["metadataToken"]);
        AssertReceiverEvidence(scan, getterFact, fixture.Assembly, RuleIds.DotNetCompiledMember);
        var eventFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedEventDeclared && fact.TargetSymbol == assembly + "|type:namespace:37:" + ReceiverNamespace + "|names:12:EventEmitter|arity:0|event:4:Tick|type:" + EventAction);
        Assert.Equal(FactTypes.ManagedEventDeclared, eventFact.FactType);
        Assert.Equal($"0x{MetadataTokens.GetToken(eventHandle):x8}", eventFact.Properties["metadataToken"]);
        AssertReceiverEvidence(scan, eventFact, fixture.Assembly, RuleIds.DotNetCompiledMember);
        var member = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == caller);
        Assert.Equal(ReceiverToken(setter), member.Properties["metadataToken"]);
        AssertReceiverEvidence(scan, member, fixture.Assembly, RuleIds.DotNetCompiledMember);
        var body = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties.GetValueOrDefault("compiledFactId") == member.FactId);
        Assert.StartsWith(caller + "|il-body:instructions:", body.TargetSymbol);
        Assert.EndsWith(":sha256:" + body.Properties["ilBodySha256"], body.TargetSymbol);
        AssertReceiverEvidence(scan, body, fixture.Assembly, RuleIds.DotNetIlBody);
        var emittedCalls = scan.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == body.FactId).ToArray();
        foreach (var (targetHandle, index) in targets.Select((target, index) => (target, index)))
        {
            var name = index == 0 ? "remove_Tick" : "add_Tick";
            var targetIdentity = EventMethod(assembly, "EventEmitter", name, EventInstance + "(" + EventAction + ")->" + EventVoid);
            var target = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == targetIdentity);
            Assert.Equal(ReceiverToken(targetHandle), target.Properties["metadataToken"]);
            AssertReceiverEvidence(scan, target, fixture.Assembly, RuleIds.DotNetCompiledMember);
            var call = Assert.Single(emittedCalls, fact => fact.Properties["targetIdentity"] == targetIdentity);
            var encoded = subscriptions[index];
            var cecilCall = Assert.Single(cecilSetter.Body.Instructions, instruction => instruction.Offset == encoded.Offset);
            Assert.Equal(Mono.Cecil.Cil.Code.Callvirt, cecilCall.OpCode.Code);
            Assert.Equal(encoded.Token, Assert.IsAssignableFrom<Mono.Cecil.MethodReference>(cecilCall.Operand).MetadataToken.ToInt32());
            Assert.Equal(ReceiverToken(setter), call.Properties["metadataToken"]);
            Assert.Equal(ReceiverToken(targetHandle), call.Properties["referenceToken"]);
            Assert.Equal("methoddef", call.Properties["referenceKind"]);
            Assert.Equal("callvirt", call.Properties["opcode"]);
            Assert.Equal(encoded.Offset.ToString(CultureInfo.InvariantCulture), call.Properties["ilOffset"]);
            Assert.Equal(body.TargetSymbol + "|call:callvirt:" + encoded.Offset.ToString(CultureInfo.InvariantCulture) + ":" + targetIdentity, call.TargetSymbol);
            AssertReceiverEvidence(scan, call, fixture.Assembly, RuleIds.DotNetIlCall);
            Assert.Null(call.SourceSymbol);
        }
        // This rule includes method-pointer operands. Opcode and null source endpoint
        // distinguish ldftn observations from executed handler-call assertions.
        var handlerIdentity = EventMethod(assembly, "EventSubscriber", "OnTick", EventInstance + "()->" + EventVoid);
        var handlerFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == handlerIdentity);
        Assert.Equal(ReceiverToken(handler), handlerFact.Properties["metadataToken"]);
        AssertReceiverEvidence(scan, handlerFact, fixture.Assembly, RuleIds.DotNetCompiledMember);
        var observedHandlers = emittedCalls.Where(fact => fact.Properties["targetIdentity"] == handlerIdentity).ToArray();
        Assert.Equal(handlerLoads.Length, observedHandlers.Length);
        foreach (var encoded in handlerLoads)
        {
            var observed = Assert.Single(observedHandlers, fact => fact.Properties["ilOffset"] == encoded.Offset.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(ReceiverToken(setter), observed.Properties["metadataToken"]);
            Assert.Equal("ldftn", observed.Properties["opcode"]);
            Assert.Equal(ReceiverToken(handler), observed.Properties["referenceToken"]);
            Assert.Equal("methoddef", observed.Properties["referenceKind"]);
            Assert.Equal(body.TargetSymbol + "|call:ldftn:" + encoded.Offset.ToString(CultureInfo.InvariantCulture) + ":" + handlerIdentity, observed.TargetSymbol);
            Assert.Null(observed.SourceSymbol);
            AssertReceiverEvidence(scan, observed, fixture.Assembly, RuleIds.DotNetIlCall);
            var independent = Assert.Single(cecilSetter.Body.Instructions, instruction => instruction.Offset == encoded.Offset);
            Assert.Equal(Mono.Cecil.Cil.Code.Ldftn, independent.OpCode.Code);
            Assert.Equal(encoded.Token, Assert.IsAssignableFrom<Mono.Cecil.MethodReference>(independent.Operand).MetadataToken.ToInt32());
        }
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == caller);
        Assert.DoesNotContain(scan.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") is "MetadataReaderDisagreement" or "IlReaderDisagreement");
        var repeat = Scan(options with { OutputPath = TempOutput() });
        Assert.Equal(JsonSerializer.Serialize(scan.Facts), JsonSerializer.Serialize(repeat.Facts));
        Assert.Equal(JsonSerializer.Serialize(scan.Manifest.IlBodyProvenance), JsonSerializer.Serialize(repeat.Manifest.IlBodyProvenance));

        void CheckLocalSignature(BlobHandle handle, bool isSetter, bool isProperty)
        {
            var blob = reader.GetBlobReader(handle);
            Assert.Equal(isProperty ? 0x28 : 0x20, blob.ReadByte());
            Assert.Equal(isSetter ? 1 : 0, blob.ReadCompressedInteger());
            if (isSetter) Assert.Equal(1, blob.ReadByte()); // Void return
            Assert.Equal(0x12, blob.ReadByte()); // CLASS
            Assert.Equal(emitter, blob.ReadTypeHandle());
            Assert.Equal(0, blob.RemainingBytes);
        }
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp")]
    [InlineData("vb", "CompiledEvidence.VisualBasic")]
    public void Event_wiring_matrix_duplicate_malformed_and_budget_inputs_remain_gaps(string language, string assemblyName)
    {
        var fixture = Fixture(language, assemblyName);
        using var temp = new TempDirectory();
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(fixture.Assembly, copy);
        using var pe = new PEReader(File.OpenRead(fixture.Assembly));
        var reader = pe.GetMetadataReader();
        var setter = ReceiverHandle(reader, "EventSubscriber", "set_Source");
        var tokens = new[] { ReceiverToken(ReceiverHandle(reader, "EventEmitter", "remove_Tick")), ReceiverToken(ReceiverHandle(reader, "EventEmitter", "add_Tick")) };
        var duplicate = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [fixture.Assembly, copy], IlBodyEvidence: true));
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copy))).ToLowerInvariant();
        var locators = new[] { Path.GetRelativePath(fixture.Source, fixture.Assembly).Replace('\\', '/'), "__external__/primary/" + hash[..12] + "-copy.dll" };
        Assert.Equal(locators.Order(StringComparer.Ordinal), duplicate.Manifest.CompiledInputProvenance!.Outcomes.Select(input => input.SafeLocator).Order(StringComparer.Ordinal));
        foreach (var locator in locators)
        {
            var outcome = Assert.Single(duplicate.Manifest.CompiledInputProvenance.Outcomes, input => input.SafeLocator == locator);
            Assert.Contains("AmbiguousDuplicateManagedAssembly", outcome.GapKinds);
            var gap = Assert.Single(duplicate.Facts, fact => fact.Evidence.FilePath == locator && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly");
            CheckEventGap(duplicate, gap, copy, il: false);
            var member = Assert.Single(duplicate.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.Evidence.FilePath == locator && fact.Properties["metadataToken"] == ReceiverToken(setter));
            Assert.NotEqual("eligible", member.Properties["sourceReconciliationEligibility"]);
            AssertReceiverEvidence(duplicate, member, copy, RuleIds.DotNetCompiledMember);
            var body = Assert.Single(duplicate.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties.GetValueOrDefault("compiledFactId") == member.FactId);
            Assert.Equal(locator, body.Evidence.FilePath);
            AssertReceiverEvidence(duplicate, body, copy, RuleIds.DotNetIlBody);
            foreach (var token in tokens)
            {
                var call = Assert.Single(duplicate.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == body.FactId && fact.Properties["referenceToken"] == token);
                Assert.Equal(locator, call.Evidence.FilePath);
                AssertReceiverEvidence(duplicate, call, copy, RuleIds.DotNetIlCall);
            }
            Assert.DoesNotContain(duplicate.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == member.TargetSymbol);
        }
        var definition = reader.GetMethodDefinition(setter);
        var operand = DecodeEventBody(pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()!)
            .First(instruction => instruction.OpCode == OpCodes.Callvirt && instruction.Token == MetadataTokens.GetToken(ReceiverHandle(reader, "EventEmitter", "remove_Tick")));
        var bytes = File.ReadAllBytes(fixture.Assembly);
        var start = FileOffset(pe, definition.RelativeVirtualAddress);
        var header = (bytes[start] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start, 2)) >> 12) * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(start + header + operand.Offset + 1, 4), 0x02000001);
        var malformed = Path.Combine(temp.Path, "malformed.dll");
        File.WriteAllBytes(malformed, bytes); // Read as data only, never execute.
        Check(malformed, null, "IlCallTargetIdentityUnavailable");
        Check(fixture.Assembly, new IlBodyLimits(MaxTotalWorkUnits: 8), "IlTotalWorkLimitExceeded");
        void Check(string input, IlBodyLimits? limits, string kind)
        {
            var rejected = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [input], IlBodyEvidence: true, IlBodyLimits: limits));
            Assert.Equal("il-partial", rejected.Manifest.IlBodyProvenance!.CoverageState);
            CheckEventGap(rejected, Assert.Single(rejected.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), input, il: true);
            Assert.DoesNotContain(rejected.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
        }
    }

    private static void CheckEventGap(ScanResult scan, CodeFact gap, string assembly, bool il)
    {
        Assert.Equal(il ? RuleIds.DotNetIlGap : RuleIds.DotNetCompiledGap, gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal(scan.Manifest.CommitSha, gap.CommitSha);
        Assert.Equal(scan.Manifest.RepoName, gap.Repo);
        Assert.Equal(1, gap.Evidence.StartLine);
        Assert.Equal(1, gap.Evidence.EndLine);
        Assert.Equal(il ? nameof(IlBodyEvidenceExtractor) : nameof(ManagedMetadataExtractor), gap.Evidence.ExtractorId);
        Assert.Equal(il ? ScannerVersions.IlBodyEvidenceExtractor : ScannerVersions.ManagedMetadataExtractor, gap.Evidence.ExtractorVersion);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))).ToLowerInvariant(), gap.Properties["rawFileSha256"]);
        var binding = il ? Assert.Single(scan.Manifest.IlBodyProvenance!.Outcomes, input => input.SafeLocator == gap.Evidence.FilePath).ProvenanceBindingInputSha256
            : Assert.Single(scan.Manifest.CompiledInputProvenance!.Outcomes, input => input.SafeLocator == gap.Evidence.FilePath).ProvenanceBindingInputSha256;
        Assert.Equal(binding, gap.Properties["provenanceBindingInputSha256"]);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(IlBodyEvidenceExtractor).Assembly.Location))).ToLowerInvariant(), gap.Properties[il ? "ilGeneratorSha256" : "generatorSha256"]);
        Assert.Equal(il ? scan.Manifest.IlBodyProvenance!.BoundedInputSha256 : scan.Manifest.CompiledInputProvenance!.BoundedInputSha256, gap.Properties[il ? "ilBoundedInputSha256" : "boundedInputSha256"]);
        Assert.False(string.IsNullOrWhiteSpace(gap.Properties["limitation"]));
    }

    // Independent operand-width decoder using framework opcode definitions, not
    // Cecil or the production decoder; reject unknown/truncated instructions.
    private static (int Offset, OpCode OpCode, int Token)[] DecodeEventBody(byte[] bytes)
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opcode => unchecked((ushort)opcode.Value));
        var decoded = new List<(int, OpCode, int)>();
        for (var offset = 0; offset < bytes.Length;)
        {
            var start = offset;
            ushort value = bytes[offset++];
            if (value == 0xfe) { Assert.True(offset < bytes.Length); value = (ushort)(0xfe00 | bytes[offset++]); }
            Assert.True(opcodes.TryGetValue(value, out var opcode));
            var size = opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineMethod
                    or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                _ => throw new InvalidDataException("Unexpected event fixture operand: " + opcode.OperandType)
            };
            Assert.True(offset + size <= bytes.Length);
            var token = opcode.OperandType == OperandType.InlineMethod ? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4)) : 0;
            decoded.Add((start, opcode, token));
            offset += size;
        }
        return decoded.ToArray();
    }
}
