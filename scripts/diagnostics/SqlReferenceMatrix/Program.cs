using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Metadata-only enumeration. Never load assemblies or invoke fixture methods.
if (args.Length < 3) throw new ArgumentException("Usage: <tfm> <output-directory> <reference-dll>...");
var refs = args.Skip(2).Order(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
var compilation = CSharpCompilation.Create("SqlReferenceInventory", references: refs);
var types = new[] { "System.Data.Common.DbCommand", "System.Data.IDbCommand", "System.Data.Common.DataAdapter", "System.Data.Common.DbDataAdapter", "System.Data.IDataAdapter",
    "System.Data.SqlClient.SqlCommand", "System.Data.SqlClient.SqlDataAdapter", "System.Data.SqlClient.SqlBulkCopy",
    "Microsoft.Data.SqlClient.SqlCommand", "Microsoft.Data.SqlClient.SqlDataAdapter", "Microsoft.Data.SqlClient.SqlBulkCopy" };
var command = new[] { "ExecuteNonQuery", "ExecuteScalar", "ExecuteReader", "ExecuteXmlReader" }
    .SelectMany(name => new[] { name, name + "Async", "Begin" + name, "End" + name }).ToHashSet();
var methods = types.Select(compilation.GetTypeByMetadataName).OfType<INamedTypeSymbol>()
    .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>().Where(m => m.DeclaredAccessibility == Accessibility.Public
        && (command.Contains(m.Name) || m.Name is "Fill" or "FillSchema" or "Update" or "WriteToServer" or "WriteToServerAsync")))
    .OrderBy(m => m.ContainingType.ToDisplayString(), StringComparer.Ordinal).ThenBy(m => m.ToDisplayString(), StringComparer.Ordinal).ToArray();
string Type(ITypeSymbol type, bool vb)
{
    if (type is IArrayTypeSymbol a) return Type(a.ElementType, vb) + (vb ? "()" : "[]");
    var n = (INamedTypeSymbol)type;
    return (vb ? "Global." : "global::") + n.ContainingNamespace + "." + n.Name + (n.IsGenericType
        ? (vb ? "(Of " : "<") + string.Join(", ", n.TypeArguments.Select(t => Type(t, vb))) + (vb ? ")" : ">") : "");
}
var inventory = new List<string> { $"# {args[0]} public SQL API overloads", "", "Declared public members only; inherited overloads are represented under their declaring base type. Compile and scan only; never invoke.", "", "| Case | Assembly identity | Original signature |", "|---|---|---|" };
foreach (var vb in new[] { false, true })
{
    var source = new List<string> { vb ? "Public Class SqlMatrix" : "public class SqlMatrix {" };
    if (args[0] == "net10.0") source.Insert(0, vb ? "#Disable Warning BC40000 ' Intentional obsolete-provider compatibility fixture; never executed." : "#pragma warning disable CS0618 // Intentional obsolete-provider compatibility fixture; never executed.");
    for (int i = 0; i < methods.Length; i++)
    {
        var m = methods[i]; var id = $"Case{i:D3}";
        if (!vb) inventory.Add($"| {id} | {m.ContainingAssembly.Identity} | ``{m.ToDisplayString()}`` |");
        var parameters = m.Parameters.Select((p, j) => vb ? $"{(p.RefKind == RefKind.None ? "ByVal" : "ByRef")} arg{j} As {Type(p.Type, true)}"
            : $"{(p.RefKind == RefKind.None ? "" : "ref ")}{Type(p.Type, false)} arg{j}").ToArray();
        var call = $"receiver.{m.Name}({string.Join(", ", m.Parameters.Select((p,j) => (p.RefKind == RefKind.None || vb ? "" : "ref ") + $"arg{j}"))})";
        source.Add(vb ? $"Public Sub {id}(receiver As {Type(m.ContainingType,true)}{(parameters.Length > 0 ? ", " : "")}{string.Join(", ",parameters)})\nCall {call}\nEnd Sub"
            : $"public void {id}({Type(m.ContainingType,false)} receiver{(parameters.Length > 0 ? ", " : "")}{string.Join(", ",parameters)}) {{ {call}; }}");
    }
    source.Add(vb ? "End Class" : "}");
    var folder = Path.Combine(args[1], vb ? "vb" : "csharp"); Directory.CreateDirectory(folder);
    File.WriteAllLines(Path.Combine(folder, vb ? "Matrix.vb" : "Matrix.cs"),source);
}
Directory.CreateDirectory(args[1]); File.WriteAllLines(Path.Combine(args[1],"OVERLOADS.md"),inventory);
Console.WriteLine($"{args[0]}: {methods.Length}: " + string.Join(", ",methods.GroupBy(m => m.ContainingType.ToDisplayString()).Select(g => $"{g.Key}={g.Count()}")));
