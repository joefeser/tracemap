using System.Security.Cryptography;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class VisualBasicConfiguredImportsTests
{
    [Theory]
    [InlineData("System", true)]
    [InlineData("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", true)]
    [InlineData("System, PublicKeyToken=B77A5C561934E089, Culture=neutral, Version=4.0.0.0", true)]
    [InlineData("System, Version=999.0.0.0, Culture=neutral, PublicKeyToken=null", false)]
    [InlineData("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null", false)]
    [InlineData("System, Version=4.0.0.0, Culture=fr, PublicKeyToken=b77a5c561934e089", false)]
    [InlineData("System, Version=4.0.0.0, Culture=, PublicKeyToken=b77a5c561934e089", false)]
    [InlineData("System, Version=garbage, Culture=neutral, PublicKeyToken=b77a5c561934e089", false)]
    [InlineData("System, Version=4.0.0.0", false)]
    [InlineData("System, Version=4.0.0.0, Version=4.0.0.0, PublicKeyToken=b77a5c561934e089", false)]
    [InlineData("System, Unknown=true, Culture=neutral, PublicKeyToken=b77a5c561934e089", false)]
    [InlineData("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=not-hex", false)]
    public void Configured_provider_requires_short_form_or_complete_supported_identity(string identity, bool admitted)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Web.config"), Config.Replace("VBCodeProvider, System", "VBCodeProvider, " + identity));
        var result = VisualBasicConfiguredImports.Read(temp.Path, [new("Web.config", "Config", 0)]);
        Assert.Equal(admitted, result is not null);
    }

    private const string Config = """
        <configuration><system.codedom><compilers>
          <compiler language="vb;visualbasic" extension=".vb" type="Microsoft.VisualBasic.VBCodeProvider, System"
            compilerOptions="/optionstrict+ /imports:System,System.Web.UI,System.Collections.Generic" />
        </compilers></system.codedom></configuration>
        """;

    [Theory]
    [InlineData("valid", true)]
    [InlineData("page-only", false)]
    [InlineData("codebehind", false)]
    [InlineData("sibling", false)]
    [InlineData("alias", false)]
    [InlineData("source-alias", false)]
    [InlineData("custom-provider", false)]
    [InlineData("custom-provider-assembly", false)]
    [InlineData("nested-config", false)]
    [InlineData("project", false)]
    [InlineData("duplicate", false)]
    [InlineData("location", false)]
    [InlineData("external", false)]
    [InlineData("response", false)]
    [InlineData("malformed", false)]
    [InlineData("dtd", false)]
    [InlineData("oversized", false)]
    [InlineData("pages-add", true)]
    [InlineData("pages-remove", false)]
    [InlineData("pages-clear", false)]
    [InlineData("pages-clear-add", true)]
    [InlineData("pages-external", false)]
    [InlineData("legacy-provider-override", false)]
    [InlineData("import-limit", false)]
    public void Configured_imports_are_scope_bound_and_hash_backed(string scenario, bool admitted)
    {
        using var temp = new TempDirectory();
        var source = "Public Class Example\n Public Sub Run(value As List(Of Integer))\n End Sub\nEnd Class";
        var config = Config;
        var sourcePath = "App_Code/Example.vb";
        if (scenario.StartsWith("pages-", StringComparison.Ordinal))
        {
            config = config.Replace("/imports:System,System.Web.UI,System.Collections.Generic", "");
            var entries = "<add namespace=\"System\" /><add namespace=\"System.Collections.Generic\" /><add namespace=\"System.Web.UI\" />";
            if (scenario == "pages-remove") entries += "<remove namespace=\"System\" /><remove namespace=\"System.Collections.Generic\" /><remove namespace=\"System.Web.UI\" />";
            if (scenario == "pages-clear") entries += "<clear />";
            if (scenario == "pages-clear-add") entries = "<add namespace=\"Other\" /><clear />" + entries;
            config = config.Replace("</configuration>", "<system.web><pages" + (scenario == "pages-external" ? " configSource=\"pages.config\"" : "") + "><namespaces>" + entries + "</namespaces></pages></system.web></configuration>");
        }
        switch (scenario)
        {
            case "page-only": config = "<configuration><system.web><pages><namespaces><add namespace=\"System.Collections.Generic\" /></namespaces></pages></system.web></configuration>"; break;
            case "codebehind": sourcePath = "Page.aspx.vb"; break;
            case "sibling": sourcePath = "Other/Example.vb"; break;
            case "alias": config = config.Replace("/imports:System,", "/imports:G=System,"); break;
            case "source-alias": source = "Imports List = Other.List\n" + source; break;
            case "custom-provider": config = config.Replace("Microsoft.VisualBasic.VBCodeProvider", "Other.Provider"); break;
            case "custom-provider-assembly": config = config.Replace("VBCodeProvider, System", "VBCodeProvider, Other"); break;
            case "duplicate": config = config.Replace("</compilers>", config[(config.IndexOf("<compiler language", StringComparison.Ordinal))..config.IndexOf("</compilers>", StringComparison.Ordinal)] + "</compilers>"); break;
            case "location": config = config.Replace("</configuration>", "<location path=\"App_Code\" /></configuration>"); break;
            case "external": config = config.Replace("<system.codedom>", "<system.codedom configSource=\"other.config\">"); break;
            case "response": config = config.Replace("/optionstrict+", "@options.rsp"); break;
            case "malformed": config = "<configuration>"; break;
            case "dtd": config = "<!DOCTYPE configuration [<!ENTITY x 'value'>]>" + config; break;
            case "oversized": config += new string(' ', 256 * 1024); break;
            case "legacy-provider-override": config = config.Replace("</configuration>", "<system.web><compilation><compilers /></compilation></system.web></configuration>"); break;
            case "import-limit":
                config = config.Replace("/imports:System,System.Web.UI,System.Collections.Generic", "").Replace("</configuration>",
                    "<system.web><pages><namespaces autoImportVBNamespace=\"true\">" +
                    string.Concat(Enumerable.Range(0, 128).Select(i => $"<add namespace=\"Synthetic.N{i}\" />")) +
                    "</namespaces></pages></system.web></configuration>");
                break;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(temp.Path, sourcePath))!);
        File.WriteAllText(Path.Combine(temp.Path, sourcePath), source);
        File.WriteAllText(Path.Combine(temp.Path, "Web.config"), config);
        var inventory = new List<FileInventoryItem> { new(sourcePath, "VisualBasic", source.Length), new("Web.config", "Config", config.Length) };
        if (scenario == "nested-config") inventory.Add(new("App_Code/Web.config", "Config", 0));
        if (scenario == "project") inventory.Add(new("Example.vbproj", "Project", 0));
        var manifest = new ScanManifest("synthetic-imports", "synthetic", null, "test", "0123456789abcdef", ScannerVersions.TraceMap,
            DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);
        var facts = VisualBasicSyntaxExtractor.Extract(temp.Path, manifest, inventory, null);
        var declaration = Assert.Single(facts, f => f.FactType == FactTypes.MethodDeclared);
        Assert.Equal(admitted, declaration.Properties.ContainsKey("configuredImportedNamespaces"));
        if (!admitted) return;
        Assert.Equal("System;System.Collections.Generic;System.Web.UI", declaration.Properties["configuredImportedNamespaces"]);
        Assert.Equal("", declaration.Properties["importedNamespaces"]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(temp.Path, "Web.config")))), declaration.Properties["configuredImportsSha256"]);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, declaration.EvidenceTier);
    }
}
