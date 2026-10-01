using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardPublicationTests
{
    [Fact]
    public void Selects_metadata_without_loading_and_retains_external_dependency()
    {
        using var temp = new TempDirectory();
        var (site, published) = Fixture(temp.Path);
        var external = Path.Combine(temp.Path, "Shared.dll");
        File.Copy(typeof(WebFormsWizardPublication).Assembly.Location, external);
        var inventory = WebFormsWizardPublication.Inspect(published, true);
        Assert.Single(inventory.ManagedAssemblies);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site));
        WebFormsWizardPublication.Configure(store, "site", published, ["bin\\Site.dll"], [external]);
        var saved = store.ReadProject("site");
        Assert.Equal("dependencies", saved.Step);
        Assert.Equal(WebFormsWizardStore.Physical(external), Assert.Single(saved.Dependencies));
        WebFormsWizardPublication.ValidateRetained(saved);
        Assert.Contains(saved.Inputs!, item => item.Role == "dependency-assembly");
    }

    [Theory]
    [InlineData("publication-dll")]
    [InlineData("source-form")]
    [InlineData("publication-config")]
    [InlineData("new-form")]
    [InlineData("new-codebehind")]
    public void Resume_detects_changed_retained_inputs(string kind)
    {
        using var temp = new TempDirectory();
        var (site, published) = Fixture(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site));
        WebFormsWizardPublication.Configure(store, "site", published, ["bin/Site.dll"], []);
        var saved = store.ReadProject("site");
        switch (kind)
        {
            case "publication-dll": File.AppendAllText(Path.Combine(published, "bin", "Site.dll"), "changed"); break;
            case "source-form": File.AppendAllText(Path.Combine(site, "Default.aspx"), "changed"); break;
            case "publication-config": File.AppendAllText(Path.Combine(published, "web.config"), " "); break;
            case "new-form": File.WriteAllText(Path.Combine(site, "New.aspx"), "new page"); break;
            case "new-codebehind": File.WriteAllText(Path.Combine(site, "Default.aspx.cs"), "class PageCode {}"); break;
        }
        Assert.Throws<InvalidOperationException>(() => WebFormsWizardPublication.ValidateRetained(saved));
    }

    [Fact]
    public void Bin_is_not_a_site_root_and_projectless_requires_compilation_marker()
    {
        using var temp = new TempDirectory();
        var (_, published) = Fixture(temp.Path);
        Assert.Equal("WEBFORMS_WIZARD_PUBLICATION_BIN_MISSING_CHOOSE_SITE_ROOT", Assert.Throws<InvalidOperationException>(() =>
            WebFormsWizardPublication.Inspect(Path.Combine(published, "bin"), true)).Message);
        File.Delete(Path.Combine(published, "PrecompiledApp.config"));
        Assert.Throws<FileNotFoundException>(() => WebFormsWizardPublication.Inspect(published, true));
        Assert.Single(WebFormsWizardPublication.Inspect(published, false).ManagedAssemblies);
    }

    [Fact]
    public void Duplicate_primary_and_dependency_are_rejected_without_state_advance()
    {
        using var temp = new TempDirectory();
        var (site, published) = Fixture(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site));
        Assert.Equal("WEBFORMS_WIZARD_ASSEMBLY_SELECTION_DUPLICATE", Assert.Throws<InvalidOperationException>(() =>
            WebFormsWizardPublication.Configure(store, "site", published, ["bin/Site.dll"], ["bin/Site.dll"])).Message);
        Assert.Equal("publication", store.ReadProject("site").Step);
    }

    [Fact]
    public void New_compiled_map_invalidates_inventory_even_when_old_files_are_unchanged()
    {
        using var temp = new TempDirectory();
        var (site, published) = Fixture(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site));
        WebFormsWizardPublication.Configure(store, "site", published, ["bin/Site.dll"], []);
        File.WriteAllText(Path.Combine(published, "bin", "extra.compiled"), "<preserve />");
        Assert.Equal("WEBFORMS_WIZARD_PUBLICATION_INVENTORY_CHANGED", Assert.Throws<InvalidOperationException>(() =>
            WebFormsWizardPublication.ValidateRetained(store.ReadProject("site"))).Message);
    }

    private static WebFormsWizardProject Project(string site) => new("site", WebFormsWizardStore.Physical(site),
        WebFormsWizardStore.Physical(site), "projectless", "all", ["Default.aspx"], null, [], [], "publication");

    internal static (string Site, string Published) Fixture(string root)
    {
        var site = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        File.WriteAllText(Path.Combine(site, "web.config"), "<configuration />");
        File.WriteAllText(Path.Combine(site, "Default.aspx"), "page");
        var published = Directory.CreateDirectory(Path.Combine(root, "published")).FullName;
        Directory.CreateDirectory(Path.Combine(published, "bin"));
        File.Copy(typeof(WebFormsWizardPublication).Assembly.Location, Path.Combine(published, "bin", "Site.dll"));
        File.WriteAllText(Path.Combine(published, "web.config"), "<configuration />");
        File.WriteAllText(Path.Combine(published, "PrecompiledApp.config"), "<precompiledApp version=\"2\" />");
        return (site, published);
    }
}
