using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsAngularClickTests
{
    private static readonly ScanManifest Manifest = new("angular", "synthetic", null, "test", "abc123", "test/1.0",
        DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);

    [Fact]
    public void Click_in_page_resolves_through_master_slot_bundle_module_and_correct_controller()
    {
        using var temp = Fixture();
        var inventory = FileInventory.Collect(temp.Path);
        var all = Extract(temp.Path, inventory);
        var edge = Assert.Single(all, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        Assert.Equal("Scripts/editor.js", edge.Evidence.FilePath);
        Assert.Equal(2, edge.Evidence.StartLine);
        Assert.Equal("EditorController", edge.Properties["controller"]);
        Assert.Equal("sampleApp", edge.Properties["module"]);
        Assert.Equal("save", edge.Properties["method"]);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, edge.EvidenceTier);
        Assert.Equal(Manifest.CommitSha, edge.CommitSha);
        Assert.Equal(ScannerVersions.LegacyWebFormsExtractor, edge.Evidence.ExtractorVersion);
        foreach (var hash in new[] { "generatorSha256", "javascriptParserSha256", "markupParserSha256", "boundedInputSha256" })
            Assert.Matches("^[a-f0-9]{64}$", edge.Properties[hash]);

        var support = edge.Properties["supportingFactIds"].Split(',').Select(id => Assert.Single(all, f => f.FactId == id)).ToArray();
        Assert.Contains(support, f => f.FactType == FactTypes.WebFormsScriptBundleMemberCandidate && f.TargetSymbol == "~/Scripts/editor.js");
        Assert.Contains(support, f => f.FactType == FactTypes.WebFormsScriptBundleDeclaredCandidate);
        Assert.Contains(support, f => f.FactType == FactTypes.WebFormsScriptBundleReferenceCandidate);
        Assert.Contains(support, f => f.Properties.GetValueOrDefault("ownerKind") == "ng-app" && f.Evidence.FilePath == "Site.master");
        Assert.Contains(support, f => f.Properties.GetValueOrDefault("ownerKind") == "ng-controller" && f.Evidence.FilePath == "Page.aspx");
        var click = Assert.Single(support, f => f.FactType == FactTypes.WebFormsAngularClickCandidate);
        Assert.Equal(4, click.Evidence.StartLine);
        Assert.Equal(click.TargetSymbol, edge.SourceSymbol);
        Assert.Contains(all, f => f.FactId == click.Properties["supportingFactIds"] && f.Properties.GetValueOrDefault("relationshipKind") == "UsesMasterPage");
        var registration = Assert.Single(support, f => f.Properties.GetValueOrDefault("ownerKind") == "controller");
        Assert.Contains(all, f => registration.Properties["supportingFactIds"].Split(',').Contains(f.FactId)
            && f.Evidence.FilePath == "Scripts/module.js" && f.Properties.GetValueOrDefault("ownerKind") == "module");
        Assert.Equal(JsonSerializer.Serialize(all), JsonSerializer.Serialize(Extract(temp.Path, inventory.Reverse().ToArray())));
    }

    [Theory]
    [InlineData("noscript")]
    [InlineData("template")]
    public void Inert_clicks_do_not_count_towards_the_limit_or_produce_handlers(string container)
    {
        using var temp = Fixture();
        var page = Path.Combine(temp.Path, "Page.aspx");
        var hidden = string.Concat(Enumerable.Repeat("<button data-ng-click='save(item)'></button>", 65));
        File.WriteAllText(page, File.ReadAllText(page).Replace("</section>", $"<{container}>{hidden}</{container}></section>"));
        var facts = Extract(temp.Path);
        Assert.Equal(4, Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularClickCandidate).Evidence.StartLine);
        Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Three_inline_server_initializers_keep_physical_spans_and_external_handler(string newline)
    {
        using var temp = Fixture();
        var page = Path.Combine(temp.Path, "Page.aspx");
        File.WriteAllText(page, File.ReadAllText(page).ReplaceLineEndings(newline));
        var facts = Extract(temp.Path);
        var edge = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        var initializers = facts.Where(f => f.FactType == FactTypes.WebFormsInlineServerInitializerCandidate)
            .OrderBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(new[] { 7, 8, 9 }, initializers.Select(f => f.Evidence.StartLine));
        Assert.All(initializers, f =>
        {
            Assert.Equal("Page.aspx", f.Evidence.FilePath);
            Assert.Equal(f.Evidence.StartLine, f.Evidence.EndLine);
            Assert.Equal("unknown", f.Properties["valueStatus"]);
            Assert.Contains(f.FactId, edge.Properties["supportingFactIds"].Split(','));
        });
    }

    [Theory]
    [InlineData("var showHints = ;", "MalformedAngularScript")]
    [InlineData("var showHints = <%= Pick() %>;", "UnsupportedAngularServerInitializer")]
    [InlineData("var showHints = 3 + <%= Flags.Value %>;", "UnsupportedAngularServerInitializer")]
    [InlineData("/*\nvar pretend = <%= Flags.Value %>;\n*/", "UnsupportedAngularServerInitializer")]
    [InlineData("angular.module('sampleApp').controller('EditorController', function($scope){});", "AmbiguousAngularController")]
    public void Invalid_or_conflicting_inline_content_cannot_supply_a_handler(string replacement, string gap)
    {
        using var temp = Fixture();
        var file = Path.Combine(temp.Path, "Page.aspx");
        File.WriteAllText(file, File.ReadAllText(file).Replace("var showHints = <%= Preferences.ShowHints %>;", replacement));
        AssertGap(Extract(temp.Path), gap);
    }

    [Fact]
    public void More_than_64_inline_server_initializers_refuses_a_partial_join()
    {
        using var temp = Fixture();
        var file = Path.Combine(temp.Path, "Page.aspx");
        var extra = string.Join("\n", Enumerable.Range(0, 62).Select(i => $"var v{i} = <%= Flags.Value %>;"));
        File.WriteAllText(file, File.ReadAllText(file).Replace("</script>", extra + "\n</script>"));
        AssertGap(Extract(temp.Path), "AngularServerInitializerLimit");
    }

    [Fact]
    public void Every_position_in_a_controller_chain_has_its_own_registration_and_handler()
    {
        using var temp = Fixture();
        var page = Path.Combine(temp.Path, "Page.aspx");
        var markup = File.ReadAllText(page).Replace("</asp:Content>", """
            <section ng-controller="ListController"><button ng-click="save()">List</button></section>
            <section ng-controller="DraftController"><button ng-click="save()">Draft</button></section>
            </asp:Content>
            """);
        File.WriteAllText(page, markup);
        var facts = Extract(temp.Path);
        var handlers = facts.Where(f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate)
            .OrderBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(new[] { 2, 7, 10 }, handlers.Select(f => f.Evidence.StartLine));
        var owners = handlers.Select(handler => Assert.Single(facts, f =>
            handler.Properties["supportingFactIds"].Split(',').Contains(f.FactId)
            && f.Properties.GetValueOrDefault("ownerKind") == "controller")).ToArray();
        Assert.Equal(new[] { 1, 6, 9 }, owners.Select(f => f.Evidence.StartLine));
        Assert.Equal(3, owners.Select(f => f.TargetSymbol).Distinct().Count());
        Assert.Equal(handlers.Select(f => f.Properties["controller"]), owners.Select(f => f.Properties["ownerName"]));
    }

    [Theory]
    [InlineData("ListController", "EditorController", "AmbiguousAngularController")]
    [InlineData(".controller(\"ListController\"", ".unknown().controller(\"ListController\"", "UnsupportedAngularControllerRegistration")]
    [InlineData(".controller(\"ListController\"", ".controller(dynamicName", "DynamicAngularControllerRegistration")]
    [InlineData("sampleApp.controller", "if (enabled) sampleApp.controller", "UnsupportedAngularControllerRegistration")]
    [InlineData("}]);", "}]) +", "MalformedAngularScript")]
    public void Broken_or_ambiguous_controller_chains_keep_the_gap(string from, string to, string gap)
    {
        using var temp = Fixture();
        var file = Path.Combine(temp.Path, "Scripts/editor.js");
        File.WriteAllText(file, File.ReadAllText(file).Replace(from, to));
        AssertGap(Extract(temp.Path), gap);
    }

    [Fact]
    public void Chain_length_bound_includes_32_and_refuses_33()
    {
        using var temp = Fixture();
        var file = Path.Combine(temp.Path, "Scripts/editor.js");
        var text = File.ReadAllText(file).TrimEnd().TrimEnd(';');
        string Link(int index) => $".controller('Extra{index}', function(){{}})";
        text += string.Concat(Enumerable.Range(0, 29).Select(Link));
        File.WriteAllText(file, text + ";");
        Assert.Single(Extract(temp.Path), f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        File.WriteAllText(file, text + Link(29) + ";");
        AssertGap(Extract(temp.Path), "AngularControllerChainLimit");
    }

    [Fact]
    public void Direct_script_reference_supports_a_page_without_a_master()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Page.aspx"), """
            <%@ Page Language="C#" %>
            <div ng-app="demo" ng-controller="Owner">
              <button data-ng-click="save()">Save</button>
            </div>
            <script src="handler.js"></script>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "handler.js"), """
            angular.module("demo").controller("Owner", function ($scope) {
                $scope.save = function () {};
            });
            """);
        var facts = Extract(temp.Path);
        var include = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsExternalScriptReferenceCandidate);
        var handler = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        Assert.Contains(include.FactId, handler.Properties["supportingFactIds"].Split(','));
        Assert.Equal("handler.js", handler.Evidence.FilePath);
    }

    [Theory]
    [InlineData("Scripts/other.js", "OtherController", "EditorController", "AmbiguousAngularController")]
    [InlineData("Scripts/editor.js", "view.save =", "view.save = function () {}; view.save =", "AmbiguousAngularScopeMethod")]
    [InlineData("Scripts/editor.js", "view.save =", "view = other; view.save =", "AngularScopeReassigned")]
    [InlineData("Scripts/editor.js", "view.save =", "var view = other; view.save =", "AngularScopeReassigned")]
    [InlineData("Scripts/editor.js", "view.save =", "[view] = replacements; view.save =", "AngularScopeReassigned")]
    [InlineData("Scripts/editor.js", "view.save =", "function view() {} view.save =", "AngularScopeReassigned")]
    [InlineData("Scripts/editor.js", "return item;", "delete view.save; return item;", "AngularScopeMemberMutation")]
    [InlineData("Scripts/module.js", "var sampleApp", "angular.module(\"sampleApp\", []); var sampleApp", "AmbiguousAngularModule")]
    [InlineData("Page.aspx", "<section ng-controller", "<section ng-non-bindable ng-controller", "AngularNonBindable")]
    [InlineData("Page.aspx", "<section ng-controller", "<section ng-app=\"another\" ng-controller", "AmbiguousAngularApplicationScope")]
    [InlineData("Scripts/editor.js", "view.save =", "var shadow = view; view.save =", "AngularScopeEscaped")]
    [InlineData("Scripts/editor.js", "view.save =", "view[key] =", "DynamicAngularScopeMember")]
    [InlineData("Scripts/editor.js", "return item;", "return (;", "MalformedAngularScript")]
    [InlineData("Scripts/editor.js", "EditorController", "Different", "AngularControllerUnavailable")]
    [InlineData("Scripts/editor.js", "view.save =", "/* view.save = function(){}; */ view.other =", "AngularScopeMethodUnavailable")]
    [InlineData("Scripts/editor.js", "\"$scope\"", "\"different\"", "AngularScopeInjectionUnavailable")]
    [InlineData("Scripts/module.js", "var sampleApp", "var sampleApp = other; var sampleApp", "AngularModuleOwnershipUnavailable")]
    [InlineData("Site.master", "sampleApp", "wrongApp", "AngularControllerUnavailable")]
    [InlineData("Site.master", "</body>", "<script>angular.module('sampleApp').controller('EditorController', function($scope){});</script></body>", "AmbiguousAngularController")]
    [InlineData("Site.master", "<body>", "<body ng-non-bindable>", "AngularNonBindable")]
    [InlineData("Site.master", "</body>", "<%: Scripts.Render(DynamicPath()) %></body>", "AngularBundleRenderIncomplete")]
    [InlineData("Global.asax", "End Sub", "Dim other As New ScriptBundle(\"~/bundles/unresolved\")\nother.Include(DynamicPath())\nEnd Sub", "AngularBundleScopeIncomplete")]
    [InlineData("Page.aspx", "ng-controller=\"EditorController\"", "ng-controller=\"EditorController\" ng-controller=\"OtherController\"", "AmbiguousAngularMarkupAttribute")]
    [InlineData("Page.aspx", "save(item)", "save(item); other()", "UnsupportedAngularClickExpression")]
    [InlineData("Site.master", "ID=\"Main\"", "ID=\"Elsewhere\"", "AngularMasterScopeUnavailable")]
    public void Unsupported_or_ambiguous_inputs_keep_an_explicit_gap(string path, string from, string to, string reason)
    {
        using var temp = Fixture();
        var file = Path.Combine(temp.Path, path);
        File.WriteAllText(file, File.ReadAllText(file).Replace(from, to, StringComparison.Ordinal));
        AssertGap(Extract(temp.Path), reason);
    }

    [Fact]
    public void Inventory_exclusion_prevents_reading_a_script_that_exists_on_disk()
    {
        using var temp = Fixture();
        var filtered = FileInventory.Collect(temp.Path).Where(i => i.RelativePath != "Scripts/editor.js").ToArray();
        AssertGap(Extract(temp.Path, filtered), "AngularScriptOutsideInventory");
    }

    [Fact]
    public void Duplicate_bundle_in_same_application_refuses_but_another_application_does_not_collide()
    {
        using var temp = Fixture();
        var config = File.ReadAllText(Path.Combine(temp.Path, "Global.asax"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Child"));
        File.WriteAllText(Path.Combine(temp.Path, "Child", "Global.asax"), config);
        Assert.Single(Extract(temp.Path), f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        File.WriteAllText(Path.Combine(temp.Path, "Global.asax"), config.Replace("BundleTable.Bundles.Add(scripts)",
            "BundleTable.Bundles.Add(scripts)\n    Dim duplicate = New ScriptBundle(\"~/bundles/client\").Include(\"~/Scripts/other.js\")"));
        AssertGap(Extract(temp.Path), "AmbiguousAngularBundle");
    }

    [Fact]
    public void Script_size_limit_withholds_binding_and_snapshot_tracks_js_content()
    {
        using var temp = Fixture();
        var path = Path.Combine(temp.Path, "Scripts", "editor.js");
        var inventory = FileInventory.Collect(temp.Path);
        var original = ScanEngine.CreateSourceSnapshotDigest(temp.Path, inventory);
        File.WriteAllText(path, File.ReadAllText(path).Replace("view.save", "view.edit", StringComparison.Ordinal));
        Assert.NotEqual(original, ScanEngine.CreateSourceSnapshotDigest(temp.Path, inventory));
        File.WriteAllText(path, new string(' ', 1_048_577));
        AssertGap(Extract(temp.Path), "AngularScriptSizeLimit");
    }

    [Fact]
    public void Source_only_repository_without_webforms_keeps_js_out_of_dotnet_inventory()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "standalone.js"), "var sample = 1;");
        Assert.DoesNotContain(FileInventory.Collect(temp.Path), f => f.Kind == "JavaScript");
    }

    [Fact]
    public void Unincluded_file_and_registration_text_in_comments_cannot_supply_a_method()
    {
        using var temp = Fixture();
        var file = Path.Combine(temp.Path, "Scripts", "editor.js");
        File.Copy(file, Path.Combine(temp.Path, "Scripts", "orphan.js"));
        File.WriteAllText(file, "// " + File.ReadAllText(file).Replace("\n", "\n//", StringComparison.Ordinal));
        AssertGap(Extract(temp.Path), "AngularControllerUnavailable");
    }

    [Fact]
    public void An_origin_base_or_too_many_direct_assets_remains_a_gap()
    {
        using var temp = Fixture();
        var page = Path.Combine(temp.Path, "Page.aspx");
        var original = File.ReadAllText(page);
        File.WriteAllText(page, original + "<base href='/elsewhere/'><script src='Scripts/editor.js'></script>");
        AssertGap(Extract(temp.Path), "AngularScriptBaseUnavailable");
        var html = new System.Text.StringBuilder(original);
        for (var i = 0; i < 33; i++)
        {
            var name = $"Scripts/script{i}.js";
            File.WriteAllText(Path.Combine(temp.Path, name), "");
            html.Append($"<script src='{name}'></script>");
        }
        File.WriteAllText(page, html.ToString());
        AssertGap(Extract(temp.Path), "AngularIncludedScriptLimit");
    }

    [Fact]
    public void Master_relative_script_uses_the_content_page_url_directory()
    {
        using var temp = Fixture();
        var pageDir = Path.Combine(temp.Path, "Views");
        Directory.CreateDirectory(pageDir);
        File.Move(Path.Combine(temp.Path, "Page.aspx"), Path.Combine(pageDir, "Page.aspx"));
        var master = Path.Combine(temp.Path, "Site.master");
        File.WriteAllText(master, File.ReadAllText(master).Replace("<%: Scripts.Render(\"~/bundles/client\") %>",
            "<script src='controller.js'></script>"));
        File.WriteAllText(Path.Combine(pageDir, "controller.js"), "angular.module('sampleApp').controller('EditorController', function($scope){$scope.save = function(){};});");
        File.WriteAllText(Path.Combine(temp.Path, "controller.js"), "invalid !!!");
        var handler = Assert.Single(Extract(temp.Path), f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        Assert.Equal("Views/controller.js", handler.Evidence.FilePath);
    }

    [Fact]
    public void Marks_limited_click_and_markup_surfaces_explicitly()
    {
        using var temp = Fixture();
        var file = Path.Combine(temp.Path, "Page.aspx");
        var original = File.ReadAllText(file);
        File.WriteAllText(file, original + string.Concat(Enumerable.Repeat("<button ng-click='save()'></button>", 65)));
        AssertGap(Extract(temp.Path), "AngularClickLimit");
        File.WriteAllText(file, original.Replace("<button", string.Concat(Enumerable.Repeat("<div>", 150)) + "<button"));
        AssertGap(Extract(temp.Path), "AngularMarkupLimit");
    }

    [Theory]
    [InlineData("<!-- {0} -->")]
    [InlineData("<noscript>{0}</noscript>")]
    public void Inert_master_render_does_not_attach_bundle_members(string wrapper)
    {
        using var temp = Fixture();
        var file = Path.Combine(temp.Path, "Site.master");
        const string render = "<%: Scripts.Render(\"~/bundles/client\") %>";
        File.WriteAllText(file, File.ReadAllText(file).Replace(render, wrapper.Replace("{0}", render)));
        AssertGap(Extract(temp.Path), "AngularScriptIncludeUnavailable");
    }

    [Fact]
    public void Overridden_master_placeholder_scripts_are_not_page_inclusions()
    {
        using var temp = Fixture();
        var file = Path.Combine(temp.Path, "Site.master");
        const string render = "<%: Scripts.Render(\"~/bundles/client\") %>";
        File.WriteAllText(file, File.ReadAllText(file).Replace(render, "").Replace("</asp:ContentPlaceHolder>", render + "</asp:ContentPlaceHolder>"));
        AssertGap(Extract(temp.Path), "AngularScriptIncludeUnavailable");
    }

    [Fact]
    public void Multiline_click_span_covers_the_attribute_and_unclosed_server_comment_refuses()
    {
        using var temp = Fixture();
        var page = Path.Combine(temp.Path, "Page.aspx");
        File.WriteAllText(page, File.ReadAllText(page).Replace("<button ng-click", "<button\n      ng-click"));
        var click = Assert.Single(Extract(temp.Path), f => f.FactType == FactTypes.WebFormsAngularClickCandidate);
        Assert.Equal(4, click.Evidence.StartLine);
        Assert.Equal(5, click.Evidence.EndLine);
        File.AppendAllText(page, "<%-- unclosed.");
        AssertGap(Extract(temp.Path), "MalformedAngularMarkup");
    }

    [Fact]
    public void Token_budget_refuses_the_script_before_admitting_a_partial_handler()
    {
        using var temp = Fixture();
        File.AppendAllText(Path.Combine(temp.Path, "Scripts", "editor.js"), new string(';', 100_001));
        AssertGap(Extract(temp.Path), "AngularScriptTokenLimit");
    }

    private static void AssertGap(CodeFact[] facts, string reason)
    {
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        Assert.Contains(facts, f => f.RuleId == RuleIds.LegacyWebFormsAngularClick && f.EvidenceTier == EvidenceTiers.Tier4Unknown
            && f.Properties.GetValueOrDefault("gapKind") == reason);
    }

    private static TempDirectory Fixture()
    {
        var source = Path.Combine(SqlDatabaseMatrixCells.Repo(), "samples", "webforms-angularjs-click");
        var temp = new TempDirectory();
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(temp.Path, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
        return temp;
    }

    private static CodeFact[] Extract(string root, IReadOnlyList<FileInventoryItem>? inventory = null) =>
        LegacyWebFormsExtractor.Extract(root, Manifest, inventory ?? FileInventory.Collect(root), []).ToArray();
}
