using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsIisMountTests
{
    private static readonly ScanManifest Manifest = new("iis", "synthetic", null, "test", "abc123", "test/1",
        DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);
    private const string Url = "/Portal/Desk/scripts/panel.js";

    [Fact]
    public void Master_maps_root_relative_url_to_one_selected_script_and_supports_exact_click()
    {
        using var temp = Fixture();
        var facts = Extract(temp.Path);
        var include = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsExternalScriptReferenceCandidate);
        Assert.Equal("Site/Layout.master", include.Evidence.FilePath);
        Assert.Equal(4, include.Evidence.StartLine);
        Assert.Equal("file:Site/Scripts/panel.js", include.TargetSymbol);
        Assert.Equal("operator-iis-mount", include.Properties["resolutionSource"]);
        Assert.Equal(Url, include.Properties["scriptUrl"]);
        Assert.Equal("/portal/desk", include.Properties["mountPrefix"]);
        Assert.Equal("Site", include.Properties["repositoryWebRoot"]);
        var handler = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        Assert.Contains(include.FactId, handler.Properties["supportingFactIds"].Split(','));
        Assert.Contains(facts, f => f.FactId == handler.Properties["clickFactId"] && f.Evidence.FilePath == "Site/Page.aspx");
        Assert.Equal("Site/Scripts/panel.js", handler.Evidence.FilePath);
    }

    [Theory]
    [InlineData(null, Url, "UnsupportedAngularScriptPath")]
    [InlineData("/portal/desk=Other", Url, "AngularApplicationRootMismatch")]
    [InlineData("/portal/desk=Site", "/Portal/Desktop/scripts/panel.js", "AngularIisMountPrefixMismatch")]
    [InlineData("/portal/desk=Site", "/Portal/Desk/scripts/missing.js", "AngularScriptOutsideInventory")]
    [InlineData("/portal/desk=Site", "/Portal/Desk/../Scripts/panel.js", "UnsupportedAngularScriptPath")]
    [InlineData("/portal/desk=Site", "/Portal/Desk/%2fScripts/panel.js", "UnsupportedAngularScriptPath")]
    [InlineData("/portal/desk=Site", "/Portal/Desk/scripts/<%= Value %>panel.js", "UnsupportedAngularScriptPath")]
    [InlineData("/portal/desk=Site", "//example.invalid/panel.js", "UnsupportedAngularScriptPath")]
    [InlineData("/portal/desk=Site", "https://example.invalid/panel.js", "UnsupportedAngularScriptPath")]
    public void Unproven_url_keeps_explicit_gap(string? mount, string url, string gap)
    {
        using var temp = Fixture();
        var file = Path.Combine(temp.Path, "Site", "Layout.master");
        File.WriteAllText(file, File.ReadAllText(file).Replace(Url, url));
        AssertGap(Extract(temp.Path, mount), gap);
    }

    [Fact]
    public void Mounted_auxiliary_script_stays_in_the_handler_support_and_can_prevent_a_join()
    {
        using var temp = Fixture();
        var master = Path.Combine(temp.Path, "Site/Layout.master");
        File.WriteAllText(master, File.ReadAllText(master).Replace(Url, "Scripts/panel.js")
            .Replace("</body>", "<script src='/Portal/Desk/Scripts/extra.js'></script></body>"));
        var extra = Path.Combine(temp.Path, "Site/Scripts/extra.js");
        File.WriteAllText(extra, "var ready = true;");
        var facts = Extract(temp.Path);
        var include = Assert.Single(facts, f => f.Properties.GetValueOrDefault("resolutionSource") == "operator-iis-mount");
        var handler = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        Assert.Contains(include.FactId, handler.Properties["supportingFactIds"].Split(','));
        File.WriteAllText(extra, "angular.module('example').controller('Panel', function($scope){});");
        AssertGap(Extract(temp.Path), "AmbiguousAngularController");
    }

    [Fact]
    public void Domain_root_mount_accepts_the_web_root_as_the_scan_root()
    {
        using var temp = Fixture();
        var site = Path.Combine(temp.Path, "Site");
        var master = Path.Combine(site, "Layout.master");
        File.WriteAllText(master, File.ReadAllText(master).Replace(Url, "/Scripts/panel.js"));
        var facts = Extract(site, "/=.");
        var handler = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        Assert.Equal("Scripts/panel.js", handler.Evidence.FilePath);
    }

    [Fact]
    public void Filtered_ambiguous_and_nested_application_scripts_cannot_satisfy_the_mapping()
    {
        using var temp = Fixture();
        var inventory = FileInventory.Collect(temp.Path);
        var js = Assert.Single(inventory, i => i.Kind == "JavaScript");
        AssertGap(Extract(temp.Path, inventory: inventory.Except([js]).ToArray()), "AngularScriptOutsideInventory");
        var duplicate = js with { RelativePath = js.RelativePath.ToUpperInvariant() };
        AssertGap(Extract(temp.Path, inventory: inventory.Append(duplicate).ToArray()), "AmbiguousAngularMountedScript");
        File.WriteAllText(Path.Combine(temp.Path, "Site/Scripts/Global.asax"), "<%@ Application Language=\"VB\" %>");
        AssertGap(Extract(temp.Path), "AngularApplicationRootMismatch");
    }

    [Theory]
    [InlineData("/=.")]
    [InlineData("/Portal/Desk=Site")]
    public void Mapping_can_name_a_repo_root_or_subdirectory(string value) => Assert.NotNull(WebFormsIisMount.Parse(value));

    [Theory]
    [InlineData("portal=Site")]
    [InlineData("/portal/=Site")]
    [InlineData("/portal=../Site")]
    [InlineData("/portal=/absolute")]
    [InlineData("/portal=Site%2f../other")]
    [InlineData("/portal=Site=Other")]
    public void Invalid_mapping_rejected(string value) => Assert.Throws<ArgumentException>(() => WebFormsIisMount.Parse(value));

    [Fact]
    public void Oversized_prefix_is_rejected() => Assert.Throws<ArgumentException>(() => WebFormsIisMount.Parse("/" + new string('a', 1025) + "=Site"));

    private static void AssertGap(IReadOnlyList<CodeFact> facts, string gap)
    {
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularClickHandlerCandidate);
        Assert.Contains(facts, f => f.RuleId == RuleIds.LegacyWebFormsAngularClick && f.Properties.GetValueOrDefault("gapKind") == gap);
    }

    private static TempDirectory Fixture()
    {
        var source = Path.Combine(SqlDatabaseMatrixCells.Repo(), "samples/webforms-iis-mount");
        var temp = new TempDirectory();
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(temp.Path, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
        return temp;
    }

    private static IReadOnlyList<CodeFact> Extract(string root, string? mount = "/portal/desk=Site", IReadOnlyList<FileInventoryItem>? inventory = null) =>
        LegacyWebFormsExtractor.Extract(root, Manifest, inventory ?? FileInventory.Collect(root), [],
            mount is null ? null : WebFormsIisMount.Parse(mount));
}
