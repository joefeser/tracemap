using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TraceMap.Core;

// Deliberately not an ASP.NET configuration evaluator. Only explicit compiler
// imports in a scan-root Web.config are retained, for App_Code source only.
// ASP.NET AssemblyBuilder adds application pages/namespaces to global VB
// compiler imports for the framework VB provider. Machine defaults and project
// evaluation are not inferred from this bounded, explicitly retained subset.
internal sealed record VisualBasicConfiguredImports(string Namespaces, string Path, string Sha256)
{
    internal static VisualBasicConfiguredImports? Read(string repoPath, IReadOnlyList<FileInventoryItem> inventory)
    {
        var configs = inventory.Where(f => f.RelativePath.Equals("web.config", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (configs.Length != 1 || inventory.Any(f => f.RelativePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))) return null;
        var path = configs[0].RelativePath;
        try
        {
            using var stream = File.OpenRead(System.IO.Path.Combine(repoPath, path));
            if (stream.Length > 256 * 1024) return null;
            using var buffer = new MemoryStream();
            // Bounded even if the file grows after checking Length.
            var bytes = new byte[256 * 1024 + 1];
            var length = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
            if (length == bytes.Length) return null;
            buffer.Write(bytes, 0, length);
            buffer.Position = 0;
            using var reader = XmlReader.Create(buffer, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 256 * 1024
            });
            var doc = XDocument.Load(reader);
            var root = doc.Root;
            if (root?.Name != "configuration" || root.Elements("location").Any()) return null;
            var sections = root.Elements("system.codedom").ToArray();
            if (sections.Length != 1 || sections[0].HasAttributes) return null;
            var containers = sections[0].Elements("compilers").ToArray();
            if (containers.Length != 1 || containers[0].HasAttributes
                || containers[0].Elements().Any(e => e.Name != "compiler")) return null;
            var compilers = containers[0].Elements("compiler").Where(e =>
                ((string?)e.Attribute("language") ?? "").Split(';').Any(l => l.Trim().Equals("vb", StringComparison.OrdinalIgnoreCase)
                    || l.Trim().Equals("visualbasic", StringComparison.OrdinalIgnoreCase))).ToArray();
            if (compilers.Length != 1) return null;
            var compiler = compilers[0];
            // Custom providers can reinterpret compilerOptions. Admit only the
            // framework VB provider, not a name-similar third-party type.
            var providerParts = ((string?)compiler.Attribute("type") ?? "").Split(',');
            var provider = providerParts[0].Trim();
            if (provider != "Microsoft.VisualBasic.VBCodeProvider"
                || providerParts.Length < 2 || providerParts[1].Trim() != "System"
                || compiler.Elements().Any(e => e.Name != "providerOption"
                    || (string?)e.Attribute("name") != "CompilerVersion")) return null;
            var webSections = root.Elements("system.web").ToArray();
            if (webSections.Length > 1 || webSections.Any(e => e.HasAttributes)) return null;
            var web = webSections.SingleOrDefault();
            // Legacy compilation/compilers can override the selected provider.
            if (web?.Elements("compilation").Any(e => e.Attribute("configSource") is not null || e.Elements("compilers").Any()) == true) return null;
            var pageSections = web?.Elements("pages").ToArray() ?? [];
            if (pageSections.Length > 1 || pageSections.Any(e => e.Attribute("configSource") is not null)) return null;
            var namespaceSections = pageSections.SingleOrDefault()?.Elements("namespaces").ToArray() ?? [];
            if (namespaceSections.Length > 1) return null;
            var namespaces = namespaceSections.SingleOrDefault();
            var imports = new List<string>();
            if (namespaces is not null)
            {
                if (namespaces.Attributes().Any(a => a.Name != "autoImportVBNamespace")) return null;
                foreach (var element in namespaces.Elements())
                {
                    if (element.Name == "clear" && !element.HasAttributes && !element.HasElements) { imports.Clear(); continue; }
                    var name = (string?)element.Attribute("namespace");
                    if (element.HasElements || element.Attributes().Count() != 1 || !IsNamespace(name)) return null;
                    if (element.Name == "remove") imports.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
                    else if (element.Name == "add") imports.Add(name!);
                    else return null;
                    if (imports.Count > 128) return null;
                }
                // Only explicit true; never materialize the inherited default.
                if ((string?)namespaces.Attribute("autoImportVBNamespace") == "true") imports.Add("Microsoft.VisualBasic");
            }
            var options = (string?)compiler.Attribute("compilerOptions") ?? "";
            if (options.Length > 16384) return null;
            var arguments = CommandLineParser.SplitCommandLineIntoArguments(options, removeHashComments: false).ToArray();
            if (arguments.Any(a => a.StartsWith('@'))) return null;
            foreach (var argument in arguments)
            {
                if (!argument.StartsWith("/imports:", StringComparison.OrdinalIgnoreCase)
                    && !argument.StartsWith("-imports:", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var item in argument[9..].Split(','))
                {
                    // No aliases or XML imports: they require name-shadowing
                    // resolution, which this syntax-only path does not provide.
                    var parsed = VisualBasicSyntaxTree.ParseText("Imports " + item).GetCompilationUnitRoot();
                    if (parsed.ContainsDiagnostics || parsed.Members.Count != 0 || parsed.Imports.Count != 1
                        || parsed.Imports[0].ImportsClauses.Count != 1
                        || parsed.Imports[0].ImportsClauses[0] is not SimpleImportsClauseSyntax { Alias: null } clause)
                        return null;
                    var name = clause.Name.ToString();
                    if (!IsNamespace(name)) return null;
                    imports.Add(name);
                    if (imports.Count > 128) return null;
                }
            }
            if (imports.Count is 0 or > 128) return null;
            return new(string.Join(';', imports.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)),
                path, Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan(0, length))));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or ArgumentException)
        {
            return null;
        }
    }

    private static bool IsNamespace(string? name) => !string.IsNullOrEmpty(name) && name.Length <= 512
        && name.Split('.').All(part => part.Length > 0 && (char.IsLetter(part[0]) || part[0] == '_')
            && part.All(c => char.IsLetterOrDigit(c) || c == '_'));

    internal bool Applies(string filePath, IReadOnlyList<FileInventoryItem> inventory, CompilationUnitSyntax root) =>
        filePath.StartsWith("App_Code/", StringComparison.OrdinalIgnoreCase)
        // Nested configuration and aliased source imports require evaluation;
        // withhold enrichment instead of guessing override/shadowing behavior.
        && !inventory.Any(f => f.RelativePath.StartsWith("App_Code/", StringComparison.OrdinalIgnoreCase)
            && System.IO.Path.GetFileName(f.RelativePath).Equals("web.config", StringComparison.OrdinalIgnoreCase))
        && !root.Imports.SelectMany(i => i.ImportsClauses).Any(c => c is not SimpleImportsClauseSyntax { Alias: null });
}
