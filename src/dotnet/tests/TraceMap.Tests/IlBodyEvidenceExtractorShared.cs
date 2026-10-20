using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Mono.Cecil;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CecilMethodDefinition = Mono.Cecil.MethodDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using TraceMap.Core;

namespace TraceMap.Tests;

// Helpers reached by every IlBodyEvidenceExtractorTests* sibling class. The split that
// created the siblings kept each test body byte-identical, so the classes take these
// through `using static` rather than qualifying every call.
internal static class IlBodyEvidenceExtractorShared
{
    internal static CodeFact BodyFact(ScanResult result, string methodName, string? signatureFragment = null, string? assemblyIdentity = null)
    {
        var matches = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.TargetSymbol!.Contains($"method:{methodName.Length.ToString(CultureInfo.InvariantCulture)}:{methodName}|", StringComparison.Ordinal)
            && (signatureFragment is null || fact.TargetSymbol.Contains(signatureFragment, StringComparison.Ordinal))
            && (assemblyIdentity is null || fact.Properties.GetValueOrDefault("assemblyIdentity")!.Contains(assemblyIdentity, StringComparison.Ordinal))).ToArray();
        return Assert.Single(matches);
    }

    internal static (List<string> FirstOpcodes, List<string> SecondOpcodes) OpcodeSequences(string assembly, string first, string second)
    {
        using var loaded = CecilAssemblyDefinition.ReadAssembly(assembly);
        var methods = AllMethods(loaded.MainModule.Types)
            .GroupBy(method => method.Name)
            .ToDictionary(group => group.Key, group => group.First());
        return (Opcodes(methods[first]), Opcodes(methods[second]));
    }

    internal static IEnumerable<CecilMethodDefinition> AllMethods(IEnumerable<CecilTypeDefinition> types)
    {
        foreach (var type in types)
        foreach (var method in type.Methods)
            yield return method;
    }

    internal static List<string> Opcodes(CecilMethodDefinition method) => method.Body.Instructions.Select(instruction => instruction.OpCode.Name.ToString()).ToList();

    internal static byte[] CorruptFirstMethodBody(string assembly)
    {
        var bytes = File.ReadAllBytes(assembly);
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (method.RelativeVirtualAddress == 0)
                continue;
            var body = pe.GetMethodBody(method.RelativeVirtualAddress);
            var rva = method.RelativeVirtualAddress;
            // Tiny bodies: IL starts at rva + 1; fat bodies: rva + 12. Use the
            // reader-reported IL size to land inside either header shape.
            var headerSize = (bytes[FileOffset(pe, rva)] & 0x03) == 0x02 ? 1 : 12;
            var ilOffset = FileOffset(pe, rva) + headerSize;
            // 0x0f is an unassigned single-byte opcode; any reader must fail
            // closed instead of decoding past the corrupted stream.
            bytes[ilOffset] = 0xee;
            return bytes;
        }
        throw new InvalidOperationException("No method body found to corrupt.");
    }

    internal static byte[] CorruptSwitchTable(string assembly, int declareCount)
    {
        var bytes = File.ReadAllBytes(assembly);
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (method.RelativeVirtualAddress == 0 || reader.GetString(method.Name) != "SwitchTable")
                continue;
            var ilStart = FileOffset(pe, method.RelativeVirtualAddress) + ((bytes[FileOffset(pe, method.RelativeVirtualAddress)] & 0x03) == 0x02 ? 1 : 12);
            var position = ilStart;
            // Skip nops/locals prologue opcodes until the switch opcode (0x45).
            while (bytes[position] != 0x45)
                position += bytes[position] == 0x00 ? 1 : 2;
            var countOffset = position + 1;
            bytes[countOffset] = (byte)(declareCount & 0xff);
            bytes[countOffset + 1] = (byte)((declareCount >> 8) & 0xff);
            bytes[countOffset + 2] = (byte)((declareCount >> 16) & 0xff);
            bytes[countOffset + 3] = (byte)((declareCount >> 24) & 0xff);
            return bytes;
        }
        throw new InvalidOperationException("SwitchTable body not found.");
    }

    internal static void WriteBranchAndJmpAssembly(string path)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("BranchAndJmp", new Version(1, 0)),
            "BranchAndJmp",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "IlShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var target = new CecilMethodDefinition("Target", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        target.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        type.Methods.Add(target);
        var jump = new CecilMethodDefinition("Jump", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        jump.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Jmp, target));
        type.Methods.Add(jump);
        var otherTarget = new CecilMethodDefinition("OtherTarget", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        otherTarget.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        type.Methods.Add(otherTarget);
        var jumpOther = new CecilMethodDefinition("JumpOther", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        jumpOther.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Jmp, otherTarget));
        type.Methods.Add(jumpOther);
        var branch = new CecilMethodDefinition("Branch", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        var ret = Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret);
        branch.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Br_S, ret));
        branch.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4, 123456));
        branch.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Pop));
        branch.Body.Instructions.Add(ret);
        type.Methods.Add(branch);
        assembly.Write(path);
    }

    internal static void WriteTerminalHandlerAssembly(string path)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("TerminalHandler", new Version(1, 0)),
            "TerminalHandler",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "IlShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new CecilMethodDefinition("TerminalHandler", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        var tryStart = Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldnull);
        var throwInstruction = Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Throw);
        var handlerStart = Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Pop);
        method.Body.Instructions.Add(tryStart);
        method.Body.Instructions.Add(throwInstruction);
        method.Body.Instructions.Add(handlerStart);
        method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        method.Body.ExceptionHandlers.Add(new Mono.Cecil.Cil.ExceptionHandler(Mono.Cecil.Cil.ExceptionHandlerType.Catch)
        {
            TryStart = tryStart,
            TryEnd = handlerStart,
            HandlerStart = handlerStart,
            HandlerEnd = null,
            CatchType = module.ImportReference(typeof(Exception))
        });
        type.Methods.Add(method);
        assembly.Write(path);
    }

    internal static void WriteDeepLocalAssembly(string path)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("DeepLocal", new Version(1, 0)),
            "DeepLocal",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "IlShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new CecilMethodDefinition("DeepLocal", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        Mono.Cecil.TypeReference localType = module.TypeSystem.Int32;
        for (var index = 0; index <= ManagedMetadataExtractor.MaximumSignatureTypeNesting; index++)
            localType = new Mono.Cecil.ArrayType(localType);
        method.Body.InitLocals = true;
        method.Body.Variables.Add(new Mono.Cecil.Cil.VariableDefinition(localType));
        method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        type.Methods.Add(method);
        assembly.Write(path);
    }

    internal static void WriteManyLocalsAssembly(string path)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("ManyLocals", new Version(1, 0)),
            "ManyLocals",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "IlShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new CecilMethodDefinition("ManyLocals", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        method.Body.InitLocals = true;
        for (var index = 0; index < 128; index++)
            method.Body.Variables.Add(new Mono.Cecil.Cil.VariableDefinition(module.TypeSystem.Int32));
        method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        type.Methods.Add(method);
        assembly.Write(path);
    }

    internal static void RewriteShortBranchDelta(byte[] bytes, string methodName, byte delta)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (reader.GetString(method.Name) != methodName)
                continue;
            var bodyOffset = FileOffset(pe, method.RelativeVirtualAddress);
            var headerSize = (bytes[bodyOffset] & 0x03) == 0x02 ? 1 : 12;
            var ilOffset = bodyOffset + headerSize;
            Assert.Equal((byte)Mono.Cecil.Cil.OpCodes.Br_S.Value, bytes[ilOffset]);
            bytes[ilOffset + 1] = delta;
            return;
        }
        throw new InvalidOperationException($"{methodName} body not found.");
    }

    internal static int FileOffset(PEReader pe, int rva)
    {
        foreach (var section in pe.PEHeaders.SectionHeaders)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + Math.Max(section.VirtualSize, section.SizeOfRawData))
                return section.PointerToRawData + (rva - section.VirtualAddress);
        }
        throw new InvalidOperationException("RVA outside any section.");
    }

    internal static ScanResult Scan(ScanOptions options) => ScanEngine.Scan(options);

    internal static string TempOutput() => Path.Combine(Directory.CreateTempSubdirectory("tracemap-il-output-").FullName, "out");

    internal static (string Source, string Assembly) Fixture(string language, string assemblyName)
    {
        var source = Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", language);
        return (source, Path.Combine(source, "bin", "Debug", "net10.0", assemblyName + ".dll"));
    }

    internal static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    internal const string CollectionObject = "type(namespace:6:System|names:6:Object)";

    internal static string CollectionType(string assembly, string ns, string name, string version = "10.0.0.0") =>
        $"scope(assembly:name:{assembly.Length}:{assembly}|version:{version.Length}:{version}|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)type(namespace:{ns.Length}:{ns}|names:{name.Length}:{name})";

    internal const string EventInstance = "arity:0|call:default|hasThis:true|explicitThis:false|";

    internal static string EventMethod(string assembly, string owner, string name, string signature) => assembly
        + "|type:namespace:37:" + ReceiverNamespace + "|names:" + owner.Length + ":" + owner
        + "|arity:0|method:" + name.Length + ":" + name + "|" + signature;

    internal static void CheckEventGap(ScanResult scan, CodeFact gap, string assembly, bool il)
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
    internal static (int Offset, OpCode OpCode, int Token)[] DecodeEventBody(byte[] bytes)
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
                _ => throw new InvalidDataException("Unexpected fixture operand: " + opcode.OperandType)
            };
            Assert.True(offset + size <= bytes.Length);
            var token = opcode.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineString ? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4)) : 0;
            decoded.Add((start, opcode, token));
            offset += size;
        }
        return decoded.ToArray();
    }

    internal const string LateAssembly = "CompiledEvidence.VisualBasic";

    internal const string LateStatic = "arity:0|call:default|hasThis:false|explicitThis:false|";

    internal const string OverloadInt = "type(namespace:6:System|names:5:Int32)";

    internal const string ReceiverNamespace = "TraceMap.CompiledFixtures.Equivalence";

    internal static MethodDefinitionHandle ReceiverHandle(MetadataReader reader, string owner, string name)
    {
        var type = Assert.Single(reader.TypeDefinitions, handle => reader.GetString(reader.GetTypeDefinition(handle).Namespace) == ReceiverNamespace && reader.GetString(reader.GetTypeDefinition(handle).Name) == owner);
        return Assert.Single(reader.GetTypeDefinition(type).GetMethods(), handle => reader.GetString(reader.GetMethodDefinition(handle).Name) == name);
    }

    internal static string ReceiverToken(MethodDefinitionHandle handle) => "0x" + MetadataTokens.GetToken(handle).ToString("x8", CultureInfo.InvariantCulture);

    internal static string ReceiverAssembly(string name) => "assembly:name:" + name.Length + ":" + name + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:" + (name.Length + 4) + ":" + name + ".dll|targetFramework:25:.NETCoreApp,Version=v10.0";

    internal static void AssertReceiverEvidence(ScanResult result, CodeFact fact, string assembly, string rule)
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
