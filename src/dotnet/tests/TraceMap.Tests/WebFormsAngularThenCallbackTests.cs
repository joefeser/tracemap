using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsAngularThenCallbackTests
{
    private static readonly ScanManifest Manifest = new("callback", "synthetic", null, "test", "abc123", "test/1",
        DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Distinct_clicks_retain_the_same_captured_method_and_each_unobserved_callback_gap(bool loader)
    {
        var facts = Extract(loader: loader);
        var edges = facts.Where(f => f.FactType == FactTypes.WebFormsAngularPartialClickHandlerCandidate).ToArray();
        Assert.Equal(2, edges.Length);
        Assert.Single(edges.Select(f => f.TargetSymbol).Distinct());
        var clicks = new HashSet<string>();
        foreach (var edge in edges)
        {
            Assert.True(clicks.Add(edge.Properties["clickFactId"]));
            Assert.Equal("Scripts/panel.js", edge.Evidence.FilePath);
            Assert.Equal(6, edge.Evidence.StartLine);
            var support = edge.Properties["supportingFactIds"].Split(',').Select(id => Assert.Single(facts, f => f.FactId == id)).ToArray();
            var callback = Assert.Single(support, f => f.FactType == FactTypes.WebFormsAngularThenCallbackCandidate);
            Assert.Equal((4, 7), (callback.Evidence.StartLine, callback.Evidence.EndLine));
            Assert.Equal("unobserved", callback.Properties["invocationStatus"]);
            Assert.Equal("7", callback.Properties["rejectionCallbackStartLine"]);
            Assert.Equal("9", callback.Properties["rejectionCallbackEndLine"]);
            Assert.Contains(support, f => f.FactId == callback.Properties["supportingFactIds"] && f.Properties.GetValueOrDefault("ownerName") == "Panel");
            var gap = Assert.Single(support, f => f.Properties.GetValueOrDefault("gapKind") == "AngularThenCallbackInvocationUnobserved");
            Assert.Contains(callback.FactId, gap.Properties["supportingFactIds"].Split(','));
            Assert.Contains(edge.Properties["clickFactId"], gap.Properties["supportingFactIds"].Split(','));
            Assert.Equal(loader, support.Any(f => f.Properties.GetValueOrDefault("gapKind") == "AngularDynamicScriptSourceUnavailable"));
        }
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
    }

    [Theory]
    [InlineData("function (rows)", "function (view)", "UnsupportedAngularScopeAssignment")]
    [InlineData("view.items = rows;", "var view;", "UnsupportedAngularScopeAssignment")]
    [InlineData("view.items = rows;", "var {view} = rows;", "UnsupportedAngularScopeAssignment")]
    [InlineData("records.list().then(", "if (enabled) records.list().then(", "UnsupportedAngularScopeAssignment")]
    [InlineData("records.list().then(", "records.list().catch(", "UnsupportedAngularScopeAssignment")]
    [InlineData("view.save = function", "if (ready) view.save = function", "UnsupportedAngularScopeAssignment")]
    [InlineData("view.items = rows;", "view.save = function () {};", "AmbiguousAngularScopeMethod")]
    [InlineData("view.items = rows;", "records.list().then(function () { view.save = function() {}; });", "UnsupportedAngularScopeAssignment")]
    [InlineData("}).catch(", "}, function () {}).catch(", "UnsupportedAngularScopeAssignment")]
    [InlineData("}).catch(", "}).finally(", "UnsupportedAngularScopeAssignment")]
    [InlineData("}).catch(", "}).catch(forward).catch(", "UnsupportedAngularScopeAssignment")]
    [InlineData("function (reason) {", "function (reason) { view.save = function () {};", "UnsupportedAngularScopeAssignment")]
    [InlineData("view.notice = reason;", "view = replacement;", "AngularScopeReassigned")]
    [InlineData("    });", "    }, extra);", "UnsupportedAngularScopeAssignment")]
    [InlineData("view.items = rows;", "view = other;", "AngularScopeReassigned")]
    [InlineData("records.list().then(", "view.save = function () {}; records.list().then(", "AmbiguousAngularScopeMethod")]
    [InlineData("records.list().then(", "records.list().then(;", "MalformedAngularScript")]
    public void Unknown_ownership_or_multiple_assignments_refuses_both_handler_types(string from, string to, string reason)
    {
        var facts = Extract(from, to);
        Assert.DoesNotContain(facts, f => f.FactType is FactTypes.WebFormsAngularClickHandlerCandidate or FactTypes.WebFormsAngularPartialClickHandlerCandidate);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == reason);
    }

    [Fact]
    public void Direct_then_without_a_catch_keeps_its_original_callback_span()
    {
        var facts = Extract("}).catch(function (reason) {\n        view.notice = reason;\n    });", "});");
        var callback = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularThenCallbackCandidate);
        Assert.Equal((4, 7), (callback.Evidence.StartLine, callback.Evidence.EndLine));
        Assert.False(callback.Properties.ContainsKey("rejectionCallbackStartLine"));
        Assert.Equal(2, facts.Count(f => f.FactType == FactTypes.WebFormsAngularPartialClickHandlerCandidate));
    }

    private static IReadOnlyList<CodeFact> Extract(string? from = null, string? to = null, bool loader = true)
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(SqlDatabaseMatrixCells.Repo(), "samples/webforms-angularjs-then");
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(temp.Path, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
        var script = Path.Combine(temp.Path, "Scripts/panel.js");
        if (from is not null) File.WriteAllText(script, File.ReadAllText(script).ReplaceLineEndings("\n").Replace(from, to));
        if (!loader)
        {
            var master = Path.Combine(temp.Path, "Layout.master");
            var markup = File.ReadAllText(master);
            var start = markup.IndexOf("  <script>", StringComparison.Ordinal);
            var end = markup.IndexOf("</body>", start, StringComparison.Ordinal);
            File.WriteAllText(master, markup[..start] + markup[end..]);
        }
        return LegacyWebFormsExtractor.Extract(temp.Path, Manifest, FileInventory.Collect(temp.Path), [], WebFormsIisMount.Parse("/Portal/Desk=."));
    }
}
