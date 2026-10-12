using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Read reference metadata only. Never load or execute any fixture or framework API.
if (args.Length != 3) throw new ArgumentException("Usage: <reference-directory> <tfm> <fixture-directory>");
var refs = Directory.GetFiles(args[0], "*.dll").Order(StringComparer.Ordinal)
    .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
var compilation = CSharpCompilation.Create("ReferenceInventory", references: refs);
var webNames = new[] { "DownloadString", "DownloadData", "DownloadFile", "UploadString", "UploadData", "UploadFile", "UploadValues", "OpenRead", "OpenWrite" }
    .SelectMany(name => new[] { name, name + "Async", name + "TaskAsync" }).ToHashSet();
var clientNames = new[] { "GetAsync", "GetStringAsync", "GetByteArrayAsync", "GetStreamAsync", "PostAsync", "PutAsync", "DeleteAsync", "Send", "SendAsync" }.ToHashSet();
var responseNames = new[] { "GetResponse", "GetResponseAsync", "BeginGetResponse", "EndGetResponse" }.ToHashSet();
var types = new[] { "System.Net.WebClient", "System.Net.WebRequest", "System.Net.HttpWebRequest", "System.Net.Http.HttpClient" };
var methods = types.Select(compilation.GetTypeByMetadataName).OfType<INamedTypeSymbol>()
    .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>().Where(method => method.DeclaredAccessibility == Accessibility.Public
        && (type.Name == "WebClient" ? webNames : type.Name == "HttpClient" ? clientNames : responseNames).Contains(method.Name)))
    .OrderBy(method => method.ContainingType.ToDisplayString(), StringComparer.Ordinal)
    .ThenBy(method => method.ToDisplayString(), StringComparer.Ordinal).ToArray();
string Type(ITypeSymbol type, bool vb)
{
    if (type is IArrayTypeSymbol array) return Type(array.ElementType, vb) + (vb ? "()" : "[]");
    var named = (INamedTypeSymbol)type;
    var name = named.ContainingNamespace + "." + named.Name;
    if (named.IsGenericType) name += (vb ? "(Of " : "<") + string.Join(", ", named.TypeArguments.Select(arg => Type(arg, vb))) + (vb ? ")" : ">");
    return (vb ? "Global." : "global::") + name;
}
var matrix = new List<string> { $"# {args[1]} reference overloads", "", "Reference metadata inventory; compile and scan only. Each case is emitted in C# and VB.", "", "| Case | Declaring assembly identity | Signature |", "|---|---|---|" };
foreach (var vb in new[] { false, true })
{
    var lines = new List<string> { vb ? "Public Class LegacyMatrix" : "public class LegacyMatrix {" };
    for (var i = 0; i < methods.Length; i++)
    {
        var method = methods[i];
        var id = $"Case{i:D3}";
        if (!vb) matrix.Add($"| {id} | {method.ContainingAssembly.Identity} | ``{method.ToDisplayString()}`` |");
        var arguments = method.Parameters.Select(parameter =>
        {
            if (parameter.Name is "address" or "requestUri" or "requestUriString") return parameter.Type.SpecialType == SpecialType.System_String
                ? "\"https://example.invalid/public-fixture\"" : (vb ? "New Global.System.Uri" : "new global::System.Uri") + "(\"https://example.invalid/public-fixture\")";
            if (parameter.Name == "method") return "\"PUT\"";
            if (parameter.Type.ToDisplayString() == "System.Net.Http.HttpRequestMessage") return (vb ? "New Global.System.Net.Http.HttpRequestMessage(Global.System.Net.Http.HttpMethod.Post" : "new global::System.Net.Http.HttpRequestMessage(global::System.Net.Http.HttpMethod.Post") + ", \"https://example.invalid/message\")";
            return vb ? $"CType(Nothing, {Type(parameter.Type, vb)})" : $"default({Type(parameter.Type, vb)})";
        });
        var call = $"receiver.{method.Name}({string.Join(", ", arguments)})";
        lines.Add(vb ? $"Public Sub {id}(receiver As {Type(method.ContainingType, true)})\nCall {call}\nEnd Sub"
            : $"public void {id}({Type(method.ContainingType, false)} receiver) {{ {call}; }}");
    }
    lines.Add(vb ? "End Class" : "}");
    Directory.CreateDirectory(Path.Combine(args[2], vb ? "vb" : "csharp"));
    File.WriteAllLines(Path.Combine(args[2], vb ? "vb/Matrix.vb" : "csharp/Matrix.cs"), lines);
}
File.WriteAllLines(Path.Combine(args[2], "OVERLOADS.md"), matrix);
Console.WriteLine($"{args[1]}: {methods.Length} overloads; " + string.Join(", ", methods.GroupBy(m => m.ContainingType.Name).Select(g => $"{g.Key}={g.Count()}")));
