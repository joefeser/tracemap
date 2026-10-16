using System.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using TraceMap.Core;
using Xunit.Abstractions;

namespace TraceMap.Tests;

public sealed class VisualBasicSyntaxTypeLookupTests(ITestOutputHelper output)
{
    [Fact]
    public void Cached_lookup_matches_original_order_aliases_and_shadows()
    {
        var root = VisualBasicSyntaxTree.ParseText("Imports System.Data\nImports System.Data.SqlClient\nImports P = Global.System.Data.SqlClient.SqlParameter\nImports N = System.Data.SqlClient\nPublic Class Shadow\nEnd Class").GetCompilationUnitRoot();
        foreach (var name in new[] { "P", "p", "N.SqlParameter", "SqlParameter", "Shadow", "Shadow.Child", "Global.Shadow", "Other.Type", "  P  " })
            Assert.Equal(Original(root, name), VisualBasicSyntaxTypeLookup.For(root).Resolve(name));
        Assert.Same(VisualBasicSyntaxTypeLookup.For(root), VisualBasicSyntaxTypeLookup.For(root));
        Parallel.For(0, 64, _ =>
        {
            Assert.Same(VisualBasicSyntaxTypeLookup.For(root), VisualBasicSyntaxTypeLookup.For(root));
            Assert.Equal(new[] { "System.Data.SqlClient.SqlParameter" }, VisualBasicSyntaxTypeLookup.For(root).Resolve("P"));
        });
        var changed = root.WithImports(VisualBasicSyntaxTree.ParseText("Imports P = Other.Type").GetCompilationUnitRoot().Imports);
        Assert.NotSame(VisualBasicSyntaxTypeLookup.For(root), VisualBasicSyntaxTypeLookup.For(changed));
        Assert.Equal(new[] { "Other.Type" }, VisualBasicSyntaxTypeLookup.For(changed).Resolve("P"));
    }

    [Fact]
    public void Large_file_repeated_resolution_reuses_results_and_preserves_output()
    {
        var imports = string.Join("\n", Enumerable.Range(0, 20).Select(i => $"Imports Namespace{i}"));
        var statements = string.Join("\n", Enumerable.Range(0, 2000).Select(i => $"Dim x{i} = New Object()"));
        var root = VisualBasicSyntaxTree.ParseText($"{imports}\nPublic Class Example\nSub Run()\n{statements}\nEnd Sub\nEnd Class").GetCompilationUnitRoot();
        const int iterations = 500;
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) _ = Original(root, "Object");
        watch.Stop();
        var before = watch.Elapsed.TotalMilliseconds;
        var lookup = VisualBasicSyntaxTypeLookup.For(root);
        var expected = lookup.Resolve("Object");
        Assert.Equal(Original(root, "Object"), expected);
        watch.Restart();
        for (var i = 0; i < iterations; i++) Assert.True(expected.Equals(lookup.Resolve("Object")));
        watch.Stop();
        output.WriteLine($"Lookup-only synthetic benchmark: 2000 creations, 20 imports, {iterations} lookups; original={before:F2}ms; cached warm={watch.Elapsed.TotalMilliseconds:F2}ms. Not whole-scan performance.");
        // No flaky wall-clock threshold: the immutable array identity proves cached reuse.
    }

    private static string[] Original(CompilationUnitSyntax root, string text)
    {
        text = text.Trim();
        if (text.StartsWith("Global.", StringComparison.OrdinalIgnoreCase)) return [text[7..]];
        var first = text.Split('.')[0];
        if (root.DescendantNodes().OfType<TypeBlockSyntax>().Any(t => t.BlockStatement.Identifier.ValueText.Equals(first, StringComparison.OrdinalIgnoreCase))) return [];
        var imports = root.Imports.SelectMany(s => s.ImportsClauses).OfType<SimpleImportsClauseSyntax>().ToArray();
        var aliases = imports.Where(c => c.Alias?.Identifier.ValueText.Equals(first, StringComparison.OrdinalIgnoreCase) == true).ToArray();
        string Strip(string value) => value.StartsWith("Global.", StringComparison.OrdinalIgnoreCase) ? value[7..] : value;
        if (aliases.Length > 0) return aliases.Select(a => Strip(a.Name.ToString()) + text[first.Length..]).ToArray();
        if (text.Contains('.')) return [text];
        return imports.Where(c => c.Alias is null).Select(c => Strip(c.Name.ToString()) + "." + text).ToArray();
    }
}
