// #856 stage 2c: consumes the corpus's recorded instrumentation-data goldens.
//   samples/dotnetperf-rewriter/src/NetPerf.Tests.Integration/TestAssemblies/
//     CallGraphTestClasses.xml           — the factory's saved id map (414 methods)
//     CallGraphTestClasses.dll           — the era-instrumented assembly whose
//                                          NetPerfMetadata.InstrumentationData
//                                          literal is canonically the same map
//     CallGraphTestClassesDuplicates.dll — a second era run (no XML twin; checked
//                                          for internal consistency only)
// The era signature format mirrors corpus ReflectionExtensionMethods
// .AsNetPerfSignature (a Cecil reimplementation — the corpus original is
// System.Reflection-based): "{ReturnType} {DeclaringType with + -> /}::{Name}({params})"
// where generic instances render as "Ns.Name`N<argNames...>" and generic
// parameters as their bare names. Module ids and instanceHash are run-random
// (era Random/GUIDs) — compared, never re-derived.
using System.Xml.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NetPerf.Goldens;

internal sealed record EraInstrumentationData(
    string InstanceHash,
    int ModuleId,
    string AssemblyIdentity,
    string PreInstrumentedHash,
    IReadOnlyDictionary<int, string> MethodsById)
{
    public static EraInstrumentationData ParseXml(string path)
    {
        var root = XDocument.Load(path).Root ?? throw new InvalidOperationException($"no root element in {path}");
        return FromXml(root, $"file:{path}");
    }

    public static EraInstrumentationData ReadLiteral(string assemblyPath)
    {
        using var module = ModuleDefinition.ReadModule(assemblyPath);
        var marker = module.GetType("NetPerf", "NetPerfMetadata")
            ?? throw new InvalidOperationException($"no NetPerf.NetPerfMetadata marker in {assemblyPath}");
        var data = (string)marker.Fields.First(field => field.Name == "InstrumentationData").Constant
            ?? throw new InvalidOperationException($"empty InstrumentationData literal in {assemblyPath}");
        return FromXml(XDocument.Parse(data).Root!, $"literal:{assemblyPath}");
    }

    private static EraInstrumentationData FromXml(XElement root, string origin)
    {
        var assembly = root.Element("assemblies")?.Element("assembly")
            ?? throw new InvalidOperationException($"no assembly element in {origin}");
        var moduleElement = assembly.Element("modules")?.Element("module")
            ?? throw new InvalidOperationException($"no module element in {origin}");
        var methods = (moduleElement.Element("types")?.Elements("type")
                .SelectMany(typeElement => typeElement.Element("methods")?.Elements("method") ?? Enumerable.Empty<XElement>())
            ?? Enumerable.Empty<XElement>()).ToList();
        if (methods.Count == 0)
            throw new InvalidOperationException($"no method records in {origin}");
        var map = methods.ToDictionary(
            method => (int)method.Attribute("id")!,
            method => (string)method.Attribute("signature")!);
        return new EraInstrumentationData(
            (string)root.Attribute("instanceHash")!,
            (int)moduleElement.Attribute("id")!,
            (string)assembly.Attribute("identity")!,
            (string)assembly.Attribute("preInstrumentedHash")!,
            map);
    }
}

internal static class EraInstrumentationDecoder
{
    // The messaging call-site shape the era instrumenter emits:
    //   ldsfld NetPerf.NetPerfMetadata::ModuleIdentifier ; ldc.i4 <methodId> ; call NetPerf.Client.Messaging::Enter|Exit...
    // Returns (era signature, recorded method id) for every method that carries
    // at least one enter or exit callback. The era signature format omits
    // generic arity, so signatures are not unique — consumers compare pairs.
    public static IReadOnlyList<(int Id, string Signature)> DecodedMethodIdPairs(string assemblyPath)
    {
        using var module = ModuleDefinition.ReadModule(assemblyPath);
        var pairs = new List<(int Id, string Signature)>();
        foreach (var type in AllTypes(module))
            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                    continue;
                var id = FirstMessagingMethodId(method.Body);
                if (id.HasValue)
                    pairs.Add((id.Value, EraSignature(method)));
            }
        return pairs;
    }

    private static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module) =>
        module.Types.SelectMany(Flatten);

    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type) =>
        new[] { type }.Concat(type.NestedTypes.SelectMany(Flatten));

    private static int? FirstMessagingMethodId(MethodBody body)
    {
        // Canonical callback prefix for every enter/exit variant (generic or not):
        //   ldsfld NetPerf.NetPerfMetadata::ModuleIdentifier ; ldc.i4 <methodId> ; ... ; call NetPerf.Client.Messaging::Enter|Exit...
        // The ldc.i4 directly after the marker ldsfld is the method id — in the
        // generic variants later ldc.i4 pushes are array sizes, not ids.
        for (var i = 0; i < body.Instructions.Count; i++)
        {
            if (body.Instructions[i].OpCode != OpCodes.Ldsfld
                || body.Instructions[i].Operand is not FieldReference field
                || field.DeclaringType.FullName != "NetPerf.NetPerfMetadata"
                || field.Name != "ModuleIdentifier")
                continue;
            if (i + 1 < body.Instructions.Count
                && body.Instructions[i + 1].OpCode == OpCodes.Ldc_I4
                && body.Instructions[i + 1].Operand is int id)
                return id;
            throw new InvalidOperationException("marker ldsfld without a following ldc.i4 method id");
        }
        return null;
    }

    // Era NetPerf signature format (see file header). Generic-instance
    // arguments render as bare names, exactly like the corpus
    // ProcessForGenericType's ga.Name loop.
    public static string EraSignature(MethodDefinition method)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(method.IsConstructor ? "System.Void " : RenderType(method.ReturnType) + " ");
        builder.Append(method.DeclaringType.FullName.Replace('+', '/'));
        builder.Append("::").Append(method.Name).Append('(');
        var hasParameters = false;
        foreach (var parameter in method.Parameters)
        {
            builder.Append(RenderType(parameter.ParameterType)).Append(',');
            hasParameters = true;
        }
        if (hasParameters)
            builder.Length -= 1;
        builder.Append(')');
        return builder.ToString();
    }

    private static string RenderType(TypeReference type) =>
        type switch
        {
            GenericParameter genericParameter => genericParameter.Name,
            GenericInstanceType genericInstance => genericInstance.Namespace + "." + genericInstance.Name
                + "<" + string.Join(",", genericInstance.GenericArguments.Select(argument => argument.Name)) + ">",
            _ => type.FullName,
        };
}
