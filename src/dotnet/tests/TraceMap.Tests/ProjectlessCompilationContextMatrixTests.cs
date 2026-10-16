using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class ProjectlessCompilationContextMatrixTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var layout in new[] { "app-code", "page", "mixed" })
        foreach (var imports in new[] { "source", "configured", "alias" })
        foreach (var references in new[] { "absent", "configured", "missing", "conflicting" })
            yield return new object[] { layout, imports, references };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Configuration_without_a_project_never_becomes_compiler_evidence(
        string layout, string imports, string references)
    {
        using var temp = new TempDirectory();
        void Write(string path, string content)
        {
            var full = Path.Combine(temp.Path, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
        var sourcePath = layout == "page" ? "Page.aspx.vb" : "App_Code/Worker.vb";
        var prefix = imports == "source" ? "Imports System\n" : imports == "alias" ? "Imports Console = Synthetic.Console\n" : "";
        Write(sourcePath, prefix + """
            Namespace Synthetic
                Public Class Worker
                    Public Sub Run()
                        System.Console.WriteLine("public-fixture")
                    End Sub
                End Class
            End Namespace
            """);
        if (layout == "mixed") Write("App_Code/Other.cs", "namespace Synthetic { public class Other {} }");
        Write("Page.aspx", "<%@ Page Language=\"VB\" CodeFile=\"Page.aspx.vb\" Inherits=\"Synthetic.Worker\" %>");
        var assemblies = references switch
        {
            "configured" => "<add assembly=\"System\" />",
            "missing" => "<add assembly=\"Synthetic.Missing, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\" />",
            "conflicting" => "<add assembly=\"Synthetic.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\" /><add assembly=\"Synthetic.Library, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null\" />",
            _ => ""
        };
        var provider = imports == "configured" ? "<system.codedom><compilers><compiler language=\"vb\" extension=\".vb\" type=\"Microsoft.VisualBasic.VBCodeProvider, System\" compilerOptions=\"/imports:System\" /></compilers></system.codedom>" : "";
        Write("web.config", "<configuration>" + provider + "<system.web><compilation><assemblies>"
            + assemblies + "</assemblies></compilation></system.web></configuration>");
        var inventory = FileInventory.Collect(temp.Path);
        var semantic = VisualBasicSemanticExtractor.Extract(temp.Path, inventory,
            new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        Assert.False(semantic.Attempted);
        Assert.True(semantic.ReducedCoverage);
        Assert.Empty(semantic.Facts);
        var gap = Assert.Single(semantic.GapFacts);
        Assert.Equal("NoVisualBasicProjectOrSolution", gap.Properties!["gapKind"]);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);

        var manifest = new ScanManifest("context-matrix", "synthetic", null, "main", "abc123", "test/1.0",
            DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);
        var syntax = VisualBasicSyntaxExtractor.Extract(temp.Path, manifest, inventory, null);
        Assert.Contains(syntax, f => f.FactType == FactTypes.TypeDeclared && f.Evidence.FilePath == sourcePath);
        Assert.Contains(syntax, f => f.Properties.GetValueOrDefault("gapKind") == "SemanticAnalysisUnavailable");
        Assert.DoesNotContain(syntax, f => f.EvidenceTier == EvidenceTiers.Tier1Semantic);
        Assert.All(syntax, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.RuleId));
            Assert.Equal("abc123", f.CommitSha);
        });
        var reversed = VisualBasicSyntaxExtractor.Extract(temp.Path, manifest, inventory.Reverse().ToArray(), null);
        Assert.Equal(syntax.Select(f => f.FactId).Order(), reversed.Select(f => f.FactId).Order());
    }
}
