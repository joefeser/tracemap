using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsInlineSettingsTests
{
    private const string Setting = """<%= ConfigurationManager.AppSettings("SampleKey") %>""";
    private static readonly ScanManifest Manifest = new("settings", "synthetic", null, "test", "abc123", "test/1",
        DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Unknown_local_setting_in_a_quoted_string_preserves_the_external_handler(string newline)
    {
        var facts = Extract($"(function () {{\n var label = '{Setting}-{Setting}';\n}}());".ReplaceLineEndings(newline));
        var settings = facts.Where(f => f.FactType == FactTypes.WebFormsInlineServerStringCandidate).ToArray();
        Assert.Equal(2, settings.Length);
        var edge = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        Assert.All(settings, f =>
        {
            Assert.Equal("Layout.master", f.Evidence.FilePath);
            Assert.Equal(7, f.Evidence.StartLine);
            Assert.Equal("unknown", f.Properties["valueStatus"]);
            Assert.Contains(f.FactId, edge.Properties["supportingFactIds"].Split(','));
            Assert.DoesNotContain("SampleKey", string.Join(",", f.Properties.Values));
        });
    }

    [Theory]
    [InlineData("var tag = document.createElement('script'); tag.src = 'SETTING';", "AngularDynamicScriptSourceUnavailable")]
    [InlineData("tag.src = baseUrl + 'prefixSETTING';", "AngularDynamicScriptSourceUnavailable")]
    [InlineData("var value = baseUrl + 'SETTING'; use(value);", "AngularServerStringFlowUnavailable")]
    public void Unknown_flow_records_partial_source_candidate_with_its_exact_gap(string body, string reason)
    {
        var facts = Extract("(function () {" + body.Replace("SETTING", Setting) + "}());");
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        var partial = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularPartialClickHandlerCandidate);
        var gap = Assert.Single(facts, f => f.Properties.GetValueOrDefault("gapKind") == reason);
        var setting = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsInlineServerStringCandidate);
        Assert.Contains(gap.FactId, partial.Properties["supportingFactIds"].Split(','));
        Assert.Contains(setting.FactId, gap.Properties["supportingFactIds"].Split(','));
        Assert.Equal("included-source-only", partial.Properties["coverageStatus"]);
        Assert.Equal("Scripts/panel.js", partial.Evidence.FilePath);
        Assert.Equal(4, partial.Evidence.StartLine);
        Assert.Contains(partial.Properties["clickFactId"], gap.Properties["supportingFactIds"].Split(','));
    }

    [Theory]
    [InlineData("var label = 'SETTING';", "UnsupportedAngularServerInitializer")]
    [InlineData("(function () { /* 'SETTING' */ }());", "UnsupportedAngularServerInitializer")]
    [InlineData("(function () { var s = `SETTING`; }());", "UnsupportedAngularServerInitializer")]
    [InlineData("(function () { angular.module('example').controller('SETTING', function(){}); }());", "AngularServerStringUseUnavailable")]
    [InlineData("(function () { var s = 'SETTING'; unexpected(; }());", "MalformedAngularScript")]
    [InlineData("(function () { var s = '<%= arbitrary() %>'; }());", "UnsupportedAngularServerInitializer")]
    [InlineData("(function () { var s = 'SETTING'; }()); angular.module('example').controller('Panel',function(){});", "AmbiguousAngularController")]
    public void Unsupported_or_competing_input_refuses_both_handler_types(string body, string reason)
    {
        var facts = Extract(body.Replace("SETTING", Setting));
        Assert.DoesNotContain(facts, f => f.FactType is FactTypes.WebFormsAngularClickHandlerCandidate or FactTypes.WebFormsAngularPartialClickHandlerCandidate);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == reason);
    }

    [Fact]
    public void Async_script_inserted_before_a_peer_retains_the_unknown_source_and_selected_method()
    {
        var facts = Extract("""
            (function () {
              var extra = document.createElement('script');
              extra.async = true;
              extra.src = '<%= System.Configuration.ConfigurationManager.AppSettings("SampleKey") %>';
              var peer = document.getElementsByTagName('script')[0];
              peer.parentNode.insertBefore(extra, peer);
            })();
            """);
        var setting = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsInlineServerStringCandidate);
        Assert.Equal(9, setting.Evidence.StartLine);
        var partial = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularPartialClickHandlerCandidate);
        var support = partial.Properties["supportingFactIds"].Split(',');
        Assert.Contains(setting.FactId, support);
        Assert.Contains(facts, f => support.Contains(f.FactId) && f.Properties.GetValueOrDefault("gapKind") == "AngularDynamicScriptSourceUnavailable");
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
    }

    [Fact]
    public void Direct_iife_argument_keeps_unknown_flow_on_the_partial_candidate()
    {
        var facts = Extract("(function (value) { display(value); })('" + Setting + "');");
        Assert.Contains(facts, f => f.FactType == FactTypes.WebFormsAngularPartialClickHandlerCandidate);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "AngularServerStringFlowUnavailable");
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
    }

    [Fact]
    public void Settings_share_the_inline_projection_limit()
    {
        var facts = Extract("(function () { var s = '" + string.Concat(Enumerable.Repeat(Setting, 65)) + "'; }());");
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "AngularServerInitializerLimit");
    }

    private static IReadOnlyList<CodeFact> Extract(string body)
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(SqlDatabaseMatrixCells.Repo(), "samples/webforms-inline-settings");
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(temp.Path, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
        var filePath = Path.Combine(temp.Path, "Layout.master");
        var markup = File.ReadAllText(filePath);
        var start = markup.IndexOf("    (function", StringComparison.Ordinal);
        var end = markup.IndexOf("  </script>", start, StringComparison.Ordinal);
        File.WriteAllText(filePath, markup[..start] + body + "\n" + markup[end..]);
        return LegacyWebFormsExtractor.Extract(temp.Path, Manifest, FileInventory.Collect(temp.Path), [], WebFormsIisMount.Parse("/Portal/Desk=."));
    }
}
