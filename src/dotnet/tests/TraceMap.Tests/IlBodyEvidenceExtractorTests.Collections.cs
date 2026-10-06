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
    private const string CollectionObject = "type(namespace:6:System|names:6:Object)";
    private const string CollectionInt = "type(namespace:6:System|names:5:Int32)";
    private static string CollectionType(string assembly, string ns, string name, string version = "10.0.0.0") =>
        $"scope(assembly:name:{assembly.Length}:{assembly}|version:{version.Length}:{version}|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)type(namespace:{ns.Length}:{ns}|names:{name.Length}:{name})";

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp")]
    [InlineData("vb", "CompiledEvidence.VisualBasic")]
    [InlineData("fsharp", "CompiledEvidence.FSharp")]
    public void Collection_matrix_preserves_indexer_operands_and_conversion_paths(string language, string assemblyName)
    {
        var fixture = Fixture(language, assemblyName);
        var options = new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [fixture.Assembly], IlBodyEvidence: true);
        var scan = Scan(options);
        using var pe = new PEReader(File.OpenRead(fixture.Assembly));
        var reader = pe.GetMetadataReader();
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(fixture.Assembly);
        var owner = Assert.Single(module.Types, type => type.FullName == ReceiverNamespace + ".CollectionMatrix");
        var bodies = new Dictionary<string, CodeFact>();
        var opcodeStreams = new Dictionary<string, short[]>();
        foreach (var name in new[] { "ReadLegacy", "ReadGeneric", "ReadInteger" })
        {
            var generic = name == "ReadGeneric";
            var integer = name == "ReadInteger";
            var handle = ReceiverHandle(reader, "CollectionMatrix", name);
            var definition = reader.GetMethodDefinition(handle);
            Assert.True(definition.Attributes.HasFlag(MethodAttributes.Static));
            Assert.True(definition.Attributes.HasFlag(MethodAttributes.Public));
            var signature = reader.GetBlobReader(definition.Signature);
            Assert.Equal(0, signature.ReadByte()); // static, non-generic, default calling convention
            Assert.Equal(2, signature.ReadCompressedInteger());
            Assert.Equal(integer ? 8 : 0x1c, signature.ReadByte());
            var collection = ReadCollection(ref signature, generic);
            Assert.Equal(8, signature.ReadByte()); // Int32 index
            Assert.Equal(0, signature.RemainingBytes);
            var cecil = Assert.Single(owner.Methods, method => method.MetadataToken.ToInt32() == MetadataTokens.GetToken(handle));
            Assert.True(cecil.IsStatic && cecil.IsPublic);
            Assert.Equal(name, cecil.Name);
            Assert.Equal(integer ? "System.Int32" : "System.Object", cecil.ReturnType.FullName);
            Assert.Equal(2, cecil.Parameters.Count);
            Assert.Equal("System.Int32", cecil.Parameters[1].ParameterType.FullName);
            CheckCecilCollection(cecil.Parameters[0].ParameterType, generic);
            var decoded = DecodeEventBody(pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()!);
            opcodeStreams[name] = decoded.Select(instruction => instruction.OpCode.Value).ToArray();
            Assert.Equal(decoded.Select(instruction => (instruction.Offset, instruction.OpCode.Value)), cecil.Body.Instructions.Select(instruction => (instruction.Offset, instruction.OpCode.Value)));
            var indexer = Assert.Single(decoded, instruction => instruction.OpCode == OpCodes.Callvirt);
            Assert.Equal(HandleKind.MemberReference, MetadataTokens.EntityHandle(indexer.Token).Kind);
            var reference = reader.GetMemberReference((MemberReferenceHandle)MetadataTokens.EntityHandle(indexer.Token));
            Assert.Equal("get_Item", reader.GetString(reference.Name));
            if (generic)
            {
                Assert.Equal(HandleKind.TypeSpecification, reference.Parent.Kind);
                var parent = reader.GetBlobReader(reader.GetTypeSpecification((TypeSpecificationHandle)reference.Parent).Signature);
                Assert.Equal(collection, ReadCollection(ref parent, true));
                Assert.Equal(0, parent.RemainingBytes);
            }
            else Assert.Equal(collection, reference.Parent);
            var raw = reader.GetBlobReader(reference.Signature);
            Assert.Equal(0x20, raw.ReadByte()); // instance
            Assert.Equal(1, raw.ReadCompressedInteger());
            Assert.Equal(generic ? 0x13 : 0x1c, raw.ReadByte()); // VAR 0, not an invented substituted Object
            if (generic) Assert.Equal(0, raw.ReadCompressedInteger());
            Assert.Equal(8, raw.ReadByte());
            Assert.Equal(0, raw.RemainingBytes);
            var cecilTarget = Assert.IsAssignableFrom<Mono.Cecil.MethodReference>(Assert.Single(cecil.Body.Instructions, instruction => instruction.Offset == indexer.Offset).Operand);
            Assert.Equal(indexer.Token, cecilTarget.MetadataToken.ToInt32());
            Assert.Equal("get_Item", cecilTarget.Name);
            Assert.True(cecilTarget.HasThis);
            Assert.False(cecilTarget.ExplicitThis);
            Assert.Equal("System.Int32", Assert.Single(cecilTarget.Parameters).ParameterType.FullName);
            CheckCecilCollection(cecilTarget.DeclaringType, generic);
            if (generic)
            {
                var parameter = Assert.IsType<Mono.Cecil.GenericParameter>(cecilTarget.ReturnType);
                Assert.Equal(0, parameter.Position);
                Assert.Equal(Mono.Cecil.GenericParameterType.Type, parameter.Type);
            }
            else Assert.Equal("System.Object", cecilTarget.ReturnType.FullName);
            var collectionIdentity = generic
                ? CollectionType("System.Collections", "System.Collections.Generic", "List`1") + "<" + CollectionObject + ">"
                : CollectionType("System.Runtime", "System.Collections", "ArrayList");
            var methodIdentity = EventMethod(ReceiverAssembly(assemblyName), "CollectionMatrix", name,
                "arity:0|call:default|hasThis:false|explicitThis:false|(" + collectionIdentity + "," + CollectionInt + ")->" + (integer ? CollectionInt : CollectionObject));
            var member = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == methodIdentity);
            Assert.Equal(ReceiverToken(handle), member.Properties["metadataToken"]);
            AssertReceiverEvidence(scan, member, fixture.Assembly, RuleIds.DotNetCompiledMember);
            var body = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties.GetValueOrDefault("compiledFactId") == member.FactId);
            Assert.StartsWith(methodIdentity + "|il-body:instructions:", body.TargetSymbol);
            Assert.EndsWith(":sha256:" + body.Properties["ilBodySha256"], body.TargetSymbol);
            AssertReceiverEvidence(scan, body, fixture.Assembly, RuleIds.DotNetIlBody);
            bodies[name] = body;
            var calls = scan.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == body.FactId).ToArray();
            Assert.Equal(integer && language == "vb" ? 2 : 1, calls.Length);
            CheckCall(indexer.Offset, indexer.Token, "callvirt", "memberref|type:" + collectionIdentity
                + "|member:8:get_Item|" + EventInstance + "(" + CollectionInt + ")->" + (generic ? "!0" : CollectionObject));
            if (integer && language == "vb")
            {
                Assert.DoesNotContain(decoded, instruction => instruction.OpCode == OpCodes.Unbox_Any);
                var conversion = Assert.Single(decoded, instruction => instruction.OpCode == OpCodes.Call);
                Assert.Equal(HandleKind.MemberReference, MetadataTokens.EntityHandle(conversion.Token).Kind);
                var target = reader.GetMemberReference((MemberReferenceHandle)MetadataTokens.EntityHandle(conversion.Token));
                Assert.Equal("ToInteger", reader.GetString(target.Name));
                CheckType(target.Parent, "Microsoft.VisualBasic.Core", "Microsoft.VisualBasic.CompilerServices", "Conversions", new Version(15, 0, 0, 0));
                Assert.Equal(new byte[] { 0, 1, 8, 0x1c }, reader.GetBlobBytes(target.Signature));
                var independent = Assert.IsAssignableFrom<Mono.Cecil.MethodReference>(Assert.Single(cecil.Body.Instructions, instruction => instruction.Offset == conversion.Offset).Operand);
                Assert.Equal(conversion.Token, independent.MetadataToken.ToInt32());
                Assert.Equal("System.Int32", independent.ReturnType.FullName);
                Assert.Equal("System.Object", Assert.Single(independent.Parameters).ParameterType.FullName);
                Assert.False(independent.HasThis);
                Assert.Equal("Microsoft.VisualBasic.CompilerServices.Conversions", independent.DeclaringType.FullName);
                Assert.Equal("Microsoft.VisualBasic.Core, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", independent.DeclaringType.Scope.ToString());
                CheckCall(conversion.Offset, conversion.Token, "call", "memberref|type:"
                    + CollectionType("Microsoft.VisualBasic.Core", "Microsoft.VisualBasic.CompilerServices", "Conversions", "15.0.0.0")
                    + "|member:9:ToInteger|arity:0|call:default|hasThis:false|explicitThis:false|(" + CollectionObject + ")->" + CollectionInt);
            }
            else if (integer)
            {
                var unbox = Assert.Single(decoded, instruction => instruction.OpCode == OpCodes.Unbox_Any);
                CheckType(MetadataTokens.EntityHandle(unbox.Token), "System.Runtime", "System", "Int32", new Version(10, 0, 0, 0));
                var independent = Assert.IsAssignableFrom<Mono.Cecil.TypeReference>(Assert.Single(cecil.Body.Instructions, instruction => instruction.Offset == unbox.Offset).Operand);
                Assert.Equal(unbox.Token, independent.MetadataToken.ToInt32());
                Assert.Equal("System.Int32", independent.FullName);
                Assert.Equal("System.Runtime, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", independent.Scope.ToString());
            }
            // Encoded operands establish neither source ownership nor runtime conversion equivalence.
            Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == methodIdentity);

            void CheckCall(int offset, int token, string opcode, string target)
            {
                var call = Assert.Single(calls, fact => fact.Properties["ilOffset"] == offset.ToString(CultureInfo.InvariantCulture));
                Assert.Equal(target, call.Properties["targetIdentity"]);
                Assert.Equal(ReceiverToken(handle), call.Properties["metadataToken"]);
                Assert.Equal($"0x{token:x8}", call.Properties["referenceToken"]);
                Assert.Equal("memberref", call.Properties["referenceKind"]);
                Assert.Equal(opcode, call.Properties["opcode"]);
                Assert.Equal(body.TargetSymbol + "|call:" + opcode + ":" + offset.ToString(CultureInfo.InvariantCulture) + ":" + target, call.TargetSymbol);
                Assert.Null(call.SourceSymbol);
                AssertReceiverEvidence(scan, call, fixture.Assembly, RuleIds.DotNetIlCall);
            }
        }
        Assert.Equal(opcodeStreams["ReadLegacy"], opcodeStreams["ReadGeneric"]);
        Assert.NotEqual(bodies["ReadLegacy"].Properties["ilBodySha256"], bodies["ReadGeneric"].Properties["ilBodySha256"]);
        Assert.DoesNotContain(scan.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") is "MetadataReaderDisagreement" or "IlReaderDisagreement");
        var repeat = Scan(options with { OutputPath = TempOutput() });
        Assert.Equal(JsonSerializer.Serialize(scan.Facts), JsonSerializer.Serialize(repeat.Facts));
        Assert.Equal(JsonSerializer.Serialize(scan.Manifest.IlBodyProvenance), JsonSerializer.Serialize(repeat.Manifest.IlBodyProvenance));

        EntityHandle ReadCollection(ref BlobReader blob, bool generic)
        {
            if (generic) Assert.Equal(0x15, blob.ReadByte()); // GENERICINST
            Assert.Equal(0x12, blob.ReadByte()); // CLASS
            var type = blob.ReadTypeHandle();
            CheckType(type, generic ? "System.Collections" : "System.Runtime", generic ? "System.Collections.Generic" : "System.Collections", generic ? "List`1" : "ArrayList", new Version(10, 0, 0, 0));
            if (generic) { Assert.Equal(1, blob.ReadCompressedInteger()); Assert.Equal(0x1c, blob.ReadByte()); }
            return type;
        }
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
        static void CheckCecilCollection(Mono.Cecil.TypeReference type, bool generic)
        {
            if (generic)
            {
                var constructed = Assert.IsType<Mono.Cecil.GenericInstanceType>(type);
                Assert.Equal("System.Object", Assert.Single(constructed.GenericArguments).FullName);
                type = constructed.ElementType;
            }
            Assert.Equal(generic ? "System.Collections.Generic.List`1" : "System.Collections.ArrayList", type.FullName);
            Assert.Equal((generic ? "System.Collections" : "System.Runtime") + ", Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", type.Scope.ToString());
        }
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp")]
    [InlineData("vb", "CompiledEvidence.VisualBasic")]
    [InlineData("fsharp", "CompiledEvidence.FSharp")]
    public void Collection_matrix_duplicate_malformed_and_budget_inputs_remain_gaps(string language, string assemblyName)
    {
        var fixture = Fixture(language, assemblyName);
        using var temp = new TempDirectory();
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(fixture.Assembly, copy);
        using var pe = new PEReader(File.OpenRead(fixture.Assembly));
        var reader = pe.GetMetadataReader();
        var method = ReceiverHandle(reader, "CollectionMatrix", "ReadLegacy");
        var definition = reader.GetMethodDefinition(method);
        var operand = Assert.Single(DecodeEventBody(pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()!), instruction => instruction.OpCode == OpCodes.Callvirt);
        var duplicate = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [fixture.Assembly, copy], IlBodyEvidence: true));
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copy))).ToLowerInvariant();
        var locators = new[] { Path.GetRelativePath(fixture.Source, fixture.Assembly).Replace('\\', '/'), "__external__/primary/" + hash[..12] + "-copy.dll" };
        Assert.Equal(locators.Order(StringComparer.Ordinal), duplicate.Manifest.CompiledInputProvenance!.Outcomes.Select(input => input.SafeLocator).Order(StringComparer.Ordinal));
        foreach (var locator in locators)
        {
            var gap = Assert.Single(duplicate.Facts, fact => fact.Evidence.FilePath == locator && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly");
            CheckEventGap(duplicate, gap, copy, il: false);
            var member = Assert.Single(duplicate.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.Evidence.FilePath == locator && fact.Properties["metadataToken"] == ReceiverToken(method));
            Assert.NotEqual("eligible", member.Properties["sourceReconciliationEligibility"]);
            AssertReceiverEvidence(duplicate, member, copy, RuleIds.DotNetCompiledMember);
            var body = Assert.Single(duplicate.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties.GetValueOrDefault("compiledFactId") == member.FactId);
            Assert.Equal(locator, body.Evidence.FilePath);
            AssertReceiverEvidence(duplicate, body, copy, RuleIds.DotNetIlBody);
            var call = Assert.Single(duplicate.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == body.FactId);
            Assert.Equal($"0x{operand.Token:x8}", call.Properties["referenceToken"]);
            Assert.Equal(locator, call.Evidence.FilePath);
            AssertReceiverEvidence(duplicate, call, copy, RuleIds.DotNetIlCall);
            Assert.DoesNotContain(duplicate.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == member.TargetSymbol);
        }
        var bytes = File.ReadAllBytes(fixture.Assembly);
        var start = FileOffset(pe, definition.RelativeVirtualAddress);
        var header = (bytes[start] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start, 2)) >> 12) * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(start + header + operand.Offset + 1, 4), 0x02000001);
        var malformed = Path.Combine(temp.Path, "malformed.dll");
        File.WriteAllBytes(malformed, bytes); // Data only; never load or execute the malformed assembly.
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
}
