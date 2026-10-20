using System.Globalization;
using System.Reflection.Metadata;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CecilMethodDefinition = Mono.Cecil.MethodDefinition;
using CecilModuleDefinition = Mono.Cecil.ModuleDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using TraceMap.Core;

namespace TraceMap.Tests;

// Helpers reached by every IlRewriteEvidenceExtractorTests* sibling class. The split that
// created the siblings kept each test body byte-identical, so the classes take these
// through `using static` rather than qualifying every call.
internal static class IlRewriteEvidenceExtractorShared
{
    internal static ScanOptions PairOptions(string before, string after, IlBodyLimits? bodyLimits = null) => new(
        RepoRoot(),
        TempOutput(),
        IlRewriteEvidence: true,
        IlRewriteBeforePaths: [before],
        IlRewriteAfterPaths: [after],
        IlBodyLimits: bodyLimits);

    internal static CodeFact EdgeFact(ScanResult result, string methodName)
    {
        var matches = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlRewriteObserved
            && fact.Properties.GetValueOrDefault("methodIdentity")!.Contains(
                $"method:{methodName.Length.ToString(CultureInfo.InvariantCulture)}:{methodName}|",
                StringComparison.Ordinal)).ToArray();
        return Assert.Single(matches);
    }

    internal static ScanResult Scan(ScanOptions options) => ScanEngine.Scan(options);

    internal static string TempOutput() => Path.Combine(Directory.CreateTempSubdirectory("tracemap-ilrewrite-output-").FullName, "out");

    internal static string RepoRoot() => Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp");

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

    internal static void WriteBeforeAssembly(string path)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("RewriteShapes", new Version(1, 0)),
            "RewriteShapes",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "RewriteShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var target = AddStaticMethod(type, module, "Target", module.TypeSystem.Int32, [module.TypeSystem.Int32]);
        target.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        target.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        var alternate = AddStaticMethod(type, module, "Alternate", module.TypeSystem.Int32, [module.TypeSystem.Int32]);
        alternate.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        alternate.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, 1));
        alternate.Body.Instructions.Add(Instruction.Create(OpCodes.Add));
        alternate.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        var caller = AddStaticMethod(type, module, "Caller", module.TypeSystem.Int32, [module.TypeSystem.Int32]);
        var constant = AddStaticMethod(type, module, "Constant", module.TypeSystem.Int32, []);
        var stable = AddStaticMethod(type, module, "Stable", module.TypeSystem.Void, []);
        var loader = AddStaticMethod(type, module, "Loader", module.TypeSystem.String, []);
        caller.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        caller.Body.Instructions.Add(Instruction.Create(OpCodes.Call, FindMethod(type, "Target")));
        caller.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        constant.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, 41));
        constant.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        stable.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        loader.Body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "before"));
        loader.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        assembly.Write(path);
    }

    internal static CecilMethodDefinition AddStaticMethod(
        CecilTypeDefinition type,
        CecilModuleDefinition module,
        string name,
        Mono.Cecil.TypeReference returnType,
        IReadOnlyList<Mono.Cecil.TypeReference> parameters)
    {
        var method = new CecilMethodDefinition(
            name,
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            returnType);
        foreach (var parameter in parameters)
            method.Parameters.Add(new ParameterDefinition(parameter));
        type.Methods.Add(method);
        return method;
    }

    internal static CecilMethodDefinition FindMethod(CecilTypeDefinition type, string name) =>
        type.Methods.Single(method => method.Name == name);

    internal static void WriteManyMethodsAssembly(string path, int count)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("RewriteShapesMany", new Version(1, 0)),
            "RewriteShapesMany",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "RewriteShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        for (var index = 0; index < count; index++)
        {
            var method = new CecilMethodDefinition(
                $"Named{index.ToString("D2", CultureInfo.InvariantCulture)}",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                module.TypeSystem.Void);
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(method);
        }

        assembly.Write(path);
    }

    internal static void MutateRemoveAllMethods(string after, string before)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(before);
        var type = (CecilTypeDefinition)assembly.MainModule.Types.Single(item => item.Name == "RewriteShapes");
        type.Methods.Clear();
        assembly.Write(after);
    }

    internal static void Mutate(string before, string after, Action<CecilAssemblyDefinition, CecilTypeDefinition> mutate)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(before);
        var type = (CecilTypeDefinition)assembly.MainModule.Types.Single(item => item.Name == "RewriteShapes");
        mutate(assembly, type);
        assembly.Write(after);
    }

    internal static void MutateConstantOperand(string before, string after) => Mutate(before, after, (_, type) =>
    {
        var constant = FindMethod(type, "Constant");
        var instruction = Assert.Single(constant.Body.Instructions, item => item.OpCode == OpCodes.Ldc_I4);
        instruction.Operand = 42;
    });

    internal static void MutateInsertMethodFirst(string before, string after) => Mutate(before, after, (_, type) =>
    {
        var inserted = new CecilMethodDefinition(
            "Inserted",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            type.Module.TypeSystem.Void);
        inserted.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        // Inserting at position zero renumbers every existing MethodDef row,
        // retargeting module-local tokens while member identities stay equal.
        type.Methods.Insert(0, inserted);
    });

    internal static void MutateCallTarget(string before, string after) => Mutate(before, after, (_, type) =>
    {
        var caller = FindMethod(type, "Caller");
        var call = Assert.Single(caller.Body.Instructions, item => item.OpCode == OpCodes.Call);
        call.Operand = FindMethod(type, "Alternate");
    });

    internal static void MutateDuplicateStableIdentity(string before, string after) => Mutate(before, after, (_, type) =>
    {
        var duplicate = new CecilMethodDefinition(
            "Stable",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            type.Module.TypeSystem.Void);
        duplicate.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        type.Methods.Add(duplicate);
    });

    internal static void MutateAssemblyName(string before, string after) => Mutate(before, after, (assembly, _) =>
    {
        assembly.Name.Name = "RewriteShapesDifferent";
    });

    internal static void MutateAddLocal(string before, string after, string methodName)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(before);
        var type = (CecilTypeDefinition)assembly.MainModule.Types.Single(item => item.Name == "RewriteShapes");
        var method = FindMethod(type, methodName);
        // A new local changes only the non-instruction body structure: the
        // opcode stream, operands, and call sites stay byte-identical.
        method.Body.Variables.Add(new VariableDefinition(type.Module.TypeSystem.Int32));
        method.Body.InitLocals = true;
        assembly.Write(after);
    }

    internal static void MutateUserString(string before, string after, string from, string to)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(before);
        var replaced = false;
        foreach (var method in AllMethods(assembly.MainModule.Types))
        {
            if (!method.HasBody)
                continue;
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Ldstr && string.Equals(instruction.Operand as string, from, StringComparison.Ordinal))
                {
                    instruction.Operand = to;
                    replaced = true;
                }
            }
        }

        Assert.True(replaced, $"No ldstr {from} operand found to mutate.");
        assembly.Write(after);
    }

    internal static IEnumerable<CecilMethodDefinition> AllMethods(IEnumerable<CecilTypeDefinition> types)
    {
        foreach (var type in types)
        foreach (var method in type.Methods)
            yield return method;
    }

    internal static void CorruptMethodBody(string path, string methodName)
    {
        var bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (reader.GetString(method.Name) != methodName || method.RelativeVirtualAddress == 0)
                continue;
            var bodyOffset = FileOffset(pe, method.RelativeVirtualAddress);
            var headerSize = (bytes[bodyOffset] & 0x03) == 0x02 ? 1 : 12;
            // 0xee is an unassigned single-byte opcode; any conforming reader
            // must fail closed instead of decoding past the corrupted stream.
            bytes[bodyOffset + headerSize] = 0xee;
            File.WriteAllBytes(path, bytes);
            return;
        }

        throw new InvalidOperationException($"{methodName} body not found.");
    }

    internal static int FileOffset(System.Reflection.PortableExecutable.PEReader pe, int rva)
    {
        foreach (var section in pe.PEHeaders.SectionHeaders)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + Math.Max(section.VirtualSize, section.SizeOfRawData))
                return section.PointerToRawData + (rva - section.VirtualAddress);
        }

        throw new InvalidOperationException("RVA outside any section.");
    }
}
