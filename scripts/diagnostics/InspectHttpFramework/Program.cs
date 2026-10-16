using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Mono.Cecil;

// Offline metadata/XML inspection only. No MSBuild evaluation, imports, assembly
// loading, resolution, application execution, recursive discovery or upload.
const long MaxXml = 2 * 1024 * 1024, MaxBinary = 64 * 1024 * 1024, MaxTotal = 256 * 1024 * 1024;
const int MaxInputs = 64, MaxMembers = 10000;
var selected = new List<(string Kind, string Path)>();
string? output = null, repoLabel = null, commit = null;
for (var i = 0; i < args.Length; i += 2)
{
    if (i + 1 >= args.Length) throw new ArgumentException("Every option requires a value.");
    switch (args[i])
    {
        case "--project": selected.Add(("project", args[i + 1])); break;
        case "--config": selected.Add(("config", args[i + 1])); break;
        case "--binary": selected.Add(("binary", args[i + 1])); break;
        case "--out": output = args[i + 1]; break;
        case "--repo-label": repoLabel = args[i + 1]; break;
        case "--commit-sha": commit = args[i + 1]; break;
        default: throw new ArgumentException("Unknown option.");
    }
}
if (selected.Count is 0 or > MaxInputs || output is null
    || repoLabel is null || !Regex.IsMatch(repoLabel, "^[a-z0-9-]{1,48}$")
    || commit is null || !(commit == "unknown" || Regex.IsMatch(commit, "^[0-9a-f]{40}$")))
    throw new ArgumentException("Required: --repo-label <public-safe-alias> --commit-sha <40-lowercase-hex-or-unknown> --out <new-local-md-file> and 1..64 explicit --project/--config/--binary inputs.");
var lines = new List<string>
{
    "# PRIVATE LOCAL framework inspection — do not upload this report",
    "", $"Repository alias: {repoLabel}; admitted source commit: {commit}.",
    "Commit is supplied metadata, not a verified binary/source binding. Unknown means provenance is incomplete.",
    "Rule: diagnostic.http-framework-inspection.v1; tier: Tier2Structural (XML declarations / encoded metadata); gaps: Tier4Unknown.",
    "", "Project declarations, config declarations, binary attributes and AssemblyRef/MemberRef identities are separate evidence sources.",
    "No effective/application TFM is selected. Missing, conditional, unresolved or differing values remain explicit.",
    "Assembly version 2.0.0.0 never identifies application target framework. No runtime execution/use/HTTP protocol is proved.",
    "No input paths, source snippets, configuration contents, request bodies, credentials or application type names are retained.",
    "Only selected files are read; imports, Directory.Build.props, external config and inherited server settings are not evaluated.",
    "", $"Generator SHA-256: {Hash(File.ReadAllBytes(typeof(Program).Assembly.Location))}",
    $"Cecil dependency SHA-256: {Hash(File.ReadAllBytes(typeof(ModuleDefinition).Assembly.Location))}", ""
};
lines.Add($"Selected input counts: project={selected.Count(input => input.Kind == "project")}, config={selected.Count(input => input.Kind == "config")}, binary={selected.Count(input => input.Kind == "binary")}. Unselected evidence categories remain uninspected.");
var bounded = new List<string>(); long total = 0;
var declarations = new SortedSet<string>(StringComparer.Ordinal);
var frameworkAssemblies = new HashSet<string>(StringComparer.Ordinal)
{ "mscorlib", "System", "System.Core", "System.Runtime", "System.Net", "System.Net.Primitives", "System.Net.Http", "System.Net.WebClient", "System.Net.Requests", "System.Net.Http.Json", "Microsoft.VisualBasic", "netstandard" };
var families = new HashSet<string>(StringComparer.Ordinal)
{ "System.Net.WebClient", "System.Net.WebRequest", "System.Net.HttpWebRequest", "System.Net.Http.HttpClient", "System.Net.Http.HttpMessageInvoker", "System.Net.Http.HttpRequestMessage", "System.Net.Http.Json.HttpClientJsonExtensions" };
var names = new HashSet<string>(StringComparer.Ordinal)
{ ".ctor", "GetAsync", "GetStringAsync", "GetByteArrayAsync", "GetStreamAsync", "PostAsync", "PutAsync", "DeleteAsync", "PatchAsync", "Send", "SendAsync", "GetResponse", "GetResponseAsync", "BeginGetResponse", "EndGetResponse", "GetFromJsonAsync", "GetFromJsonAsAsyncEnumerable", "PostAsJsonAsync", "PutAsJsonAsync", "PatchAsJsonAsync" };
foreach (var name in new[] { "DownloadString", "DownloadData", "DownloadFile", "UploadString", "UploadData", "UploadFile", "UploadValues", "OpenRead", "OpenWrite" })
    foreach (var suffix in new[] { "", "Async", "TaskAsync" }) names.Add(name + suffix);
var publicTypes = new HashSet<string>(StringComparer.Ordinal)
{
    "System.Void", "System.String", "System.Boolean", "System.Byte", "System.Int32", "System.Int64", "System.Object", "System.Uri", "System.Type", "System.AsyncCallback", "System.IAsyncResult", "System.IO.Stream",
    "System.Collections.Specialized.NameValueCollection", "System.Threading.CancellationToken", "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1", "System.Collections.Generic.IAsyncEnumerable`1",
    "System.Net.Http.HttpClient", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpResponseMessage", "System.Net.Http.HttpCompletionOption", "System.Net.Http.HttpContent", "System.Net.Http.HttpMethod",
    "System.Text.Json.JsonSerializerOptions", "System.Text.Json.Serialization.JsonSerializerContext", "System.Text.Json.Serialization.Metadata.JsonTypeInfo`1", "System.Net.WebResponse"
};
for (var i = 0; i < selected.Count; i++)
{
    var input = selected[i]; var id = $"I{i + 1:D3}";
    lines.Add($"## {id} — {input.Kind}");
    try
    {
        var info = new FileInfo(input.Path);
        if (!info.Exists) { Gap("missing input"); continue; }
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0) { Gap("symbolic-link input refused"); continue; }
        var limit = input.Kind == "binary" ? MaxBinary : MaxXml;
        if (info.Length > limit || total + info.Length > MaxTotal) { Gap("input byte limit exceeded; unread"); continue; }
        // Bound the actual read even if a selected file grows after FileInfo.
        using var stream = new FileStream(input.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var memory = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, limit + 1 - memory.Length))) > 0)
        {
            memory.Write(buffer, 0, read);
            if (memory.Length > limit) break;
        }
        if (memory.Length > limit || total + memory.Length > MaxTotal) { Gap("input grew beyond byte limit; refused"); continue; }
        var bytes = memory.ToArray(); total += bytes.Length;
        bounded.Add($"{id}:{input.Kind}:{bytes.Length}:{Hash(bytes)}");
        lines.Add($"Input SHA-256 (private/local only): {Hash(bytes)}; bytes: {bytes.Length}.");
        if (input.Kind != "binary")
        {
            using var reader = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxXml });
            var xml = XDocument.Load(reader, LoadOptions.SetLineInfo);
            var found = 0;
            if (xml.Root?.Name.LocalName != (input.Kind == "project" ? "Project" : "configuration"))
            { Gap("unexpected XML root; requested evidence source not recognized"); continue; }
            if (input.Kind == "project")
            {
                foreach (var element in xml.Descendants().Where(element => element.Parent?.Name.LocalName == "PropertyGroup" && element.Name.LocalName is "TargetFramework" or "TargetFrameworks" or "TargetFrameworkVersion"))
                {
                    declarations.Add(SafeTfm(element.Value));
                    var conditional = element.AncestorsAndSelf().Any(parent => parent.Attribute("Condition") is not null);
                    lines.Add($"- Project {element.Name.LocalName}; line {((IXmlLineInfo)element).LineNumber}; conditional={conditional}: {SafeTfm(element.Value)}"); found++;
                }
                foreach (var field in new[] { "TargetFramework", "TargetFrameworks", "TargetFrameworkVersion" })
                    if (!xml.Descendants().Any(element => element.Parent?.Name.LocalName == "PropertyGroup" && element.Name.LocalName == field)) lines.Add($"- Project {field}: MISSING (selected XML only; imports not evaluated).");
                lines.Add($"Project import declarations: {xml.Descendants().Count(element => element.Name.LocalName == "Import")} (not evaluated). SDK/implicit imports not evaluated.");
            }
            else
            {
                foreach (var element in xml.Descendants().Where(element => element.Name.LocalName is "compilation" or "httpRuntime"
                    && element.Parent?.Name.LocalName == "system.web"))
                {
                    var setting = element.Attribute("targetFramework");
                    declarations.Add(setting is null ? "MISSING" : SafeTfm(setting.Value));
                    lines.Add($"- Web.config {element.Name.LocalName}.targetFramework; line {((IXmlLineInfo)element).LineNumber}: {(setting is null ? "MISSING" : SafeTfm(setting.Value))}"); found++;
                    if (element.Attribute("configSource") is not null) lines.Add("  External configSource present; not followed.");
                }
                foreach (var field in new[] { "compilation", "httpRuntime" })
                    if (!xml.Descendants().Any(element => element.Name.LocalName == field && element.Parent?.Name.LocalName == "system.web"))
                        lines.Add($"- Web.config {field}.targetFramework: MISSING (element absent; inherited/server settings uninspected).");
            }
            if (found == 0) Gap("requested framework declarations missing");
        }
        else
        {
            using var module = ModuleDefinition.ReadModule(new MemoryStream(bytes), new ReaderParameters { ReadingMode = ReadingMode.Deferred, ReadSymbols = false, AssemblyResolver = new DenyResolver() });
            var attributes = module.Assembly?.CustomAttributes.Where(attribute => attribute.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute").ToArray() ?? [];
            if (attributes.Length == 0) Gap("binary TargetFrameworkAttribute missing; TFM unknown");
            foreach (var attribute in attributes)
            {
                var value = attribute.ConstructorArguments.FirstOrDefault().Value as string;
                declarations.Add(SafeTfm(value ?? ""));
                lines.Add("- Binary TargetFrameworkAttribute: " + SafeTfm(value ?? ""));
            }
            if (attributes.Length > 1) Gap("multiple binary framework attributes; not reconciled");
            lines.Add("### Encoded framework AssemblyRef identities (not application TFM)");
            var references = module.AssemblyReferences.Where(reference => frameworkAssemblies.Contains(reference.Name)).OrderBy(reference => reference.FullName, StringComparer.Ordinal).ToArray();
            foreach (var reference in references) lines.Add("- " + Identity(reference));
            if (references.Length == 0) Gap("no selected framework AssemblyRef identities");
            lines.Add("### Encoded HTTP MemberRef signatures (not execution evidence)");
            var members = new SortedSet<string>(StringComparer.Ordinal); var examined = 0;
            foreach (var member in module.GetMemberReferences())
            {
                if (++examined > MaxMembers) { Gap("MemberRef limit reached; remaining signatures uninspected"); break; }
                if (member is not MethodReference method || !families.Contains(method.DeclaringType.FullName) || !names.Contains(method.Name)) continue;
                var scope = method.DeclaringType.Scope is AssemblyNameReference assembly && frameworkAssemblies.Contains(assembly.Name)
                    ? Identity(assembly) : "NON-FRAMEWORK/LOCAL/UNRESOLVED SCOPE (identity redacted; not trusted)";
                members.Add($"{method.DeclaringType.FullName}.{method.Name}; arity={method.GenericParameters.Count}; convention={method.CallingConvention}; hasThis={method.HasThis}; ({string.Join(", ", method.Parameters.Select(parameter => Type(parameter.ParameterType)))}) -> {Type(method.ReturnType)}; scope: {scope}");
            }
            foreach (var member in members) lines.Add("- " + member);
            if (members.Count == 0) Gap("no selected HTTP MemberRef signatures; absence does not mean unused");
            lines.Add("MemberRef identities/signatures are encoded references only; declaration resolution, inherited targets, overrides, wrappers, dynamic destinations and actual use remain unproved.");
        }
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or BadImageFormatException or ArgumentException or InvalidOperationException or NotSupportedException or IndexOutOfRangeException)
    { Gap("input inspection failed: " + exception.GetType().Name + " (details/paths suppressed)"); }
    finally { lines.Add(""); }
    void Gap(string message) { bounded.Add($"{id}:{input.Kind}:gap:{message}"); lines.Add("Tier4Unknown gap: " + message + "."); }
}
lines.Add("## Cross-source review — no reconciliation or inferred TFM");
var literals = declarations.Where(value => !value.Contains(" ", StringComparison.Ordinal)).ToArray();
if (literals.Length > 1) lines.Add("Multiple distinct literal framework strings are present: " + string.Join("; ", literals) + ". They may represent syntax differences, multi-targeting or conflicting declarations; inspect each source independently. No value wins automatically.");
else lines.Add("No agreement/completeness claim: absent, conditional, inherited or uninspected sources can still change the effective framework.");
lines.Add("Bounded input SHA-256 (private/local only): " + Hash(Encoding.UTF8.GetBytes(string.Join("\n", bounded))));
lines.Add("Bounded-input hash covers inspected byte hashes and explicit gap outcomes; it is not a complete repository fingerprint or build-authenticity proof.");
lines.Add("Do not bring this report or its private input hashes back. Fill the separate sanitized summary template manually using approved public framework values only.");
// Never overwrite an input, existing report or symlink target.
try
{
    using var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    using var writer = new StreamWriter(destination, new UTF8Encoding(false));
    writer.Write(string.Join("\n", lines) + "\n");
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine("Local report not written: output exists or is unavailable (details/paths suppressed).");
    Environment.ExitCode = 2; return;
}
Console.WriteLine("Saved local inspection report. Application-specific compatibility remains pending validation.");

string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
string SafeTfm(string value)
{
    value = value.Trim();
    if (value.Length is 0 or > 256) return "MISSING/INVALID (value suppressed)";
    if (value.Contains("$(", StringComparison.Ordinal)) return "UNRESOLVED PROPERTY EXPRESSION (value suppressed; not evaluated)";
    return Regex.IsMatch(value, "^(?:v?[0-9]+(?:\\.[0-9]+){1,3}|net(?:standard|coreapp)?[0-9]+(?:\\.[0-9]+)?(?:-windows(?:[0-9]+(?:\\.[0-9]+){1,3})?)?)(?:;(?:net(?:standard|coreapp)?[0-9]+(?:\\.[0-9]+)?))*$", RegexOptions.CultureInvariant)
        || Regex.IsMatch(value, "^\\.NET(?:Framework|CoreApp|Standard|Portable),Version=v[0-9]+(?:\\.[0-9]+){1,3}(?:,Profile=(?:Client|Profile[0-9]+))?$", RegexOptions.CultureInvariant)
        ? value : "UNRECOGNIZED LITERAL (value suppressed; retain locally outside summary if needed)";
}
string Identity(AssemblyNameReference reference)
{
    var culture = string.IsNullOrEmpty(reference.Culture) ? "neutral" : Regex.IsMatch(reference.Culture, "^[a-z]{2}(?:-[A-Za-z]{2,8})?$", RegexOptions.CultureInvariant) ? reference.Culture : "unrecognized-culture";
    var token = reference.PublicKeyToken.Length switch { 0 => "null", 8 => Convert.ToHexStringLower(reference.PublicKeyToken), _ => "invalid-token-length (value suppressed)" };
    return $"{reference.Name}, Version={reference.Version}, Culture={culture}, PublicKeyToken={token}";
}
string Type(TypeReference type)
{
    if (type is GenericParameter generic) return (generic.Type == GenericParameterType.Method ? "!!" : "!") + generic.Position;
    if (type is ArrayType array) return Type(array.ElementType) + "[" + new string(',', Math.Min(array.Rank - 1, 16)) + "]";
    if (type is ByReferenceType byReference) return Type(byReference.ElementType) + "&";
    if (type is GenericInstanceType instance) return Type(instance.ElementType) + "<" + string.Join(", ", instance.GenericArguments.Select(Type)) + ">";
    return publicTypes.Contains(type.FullName) && type.Scope is AssemblyNameReference assembly && frameworkAssemblies.Contains(assembly.Name)
        || publicTypes.Contains(type.FullName) && type.FullName.StartsWith("System.Text.Json.", StringComparison.Ordinal) && type.Scope is AssemblyNameReference jsonAssembly && jsonAssembly.Name == "System.Text.Json"
        ? type.FullName : "<unresolved-or-private-type>";
}

sealed class DenyResolver : IAssemblyResolver
{
    public AssemblyDefinition Resolve(AssemblyNameReference name) => throw new NotSupportedException("Referenced assemblies are not resolved.");
    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => throw new NotSupportedException("Referenced assemblies are not resolved.");
    public void Dispose() { }
}
