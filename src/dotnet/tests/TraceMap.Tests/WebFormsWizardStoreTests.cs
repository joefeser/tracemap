using System.Text.Json.Nodes;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardStoreTests
{
    private static WebFormsWizardProject Project(string root, string id = "website") =>
        new(id, root, root, "projectless", "selected", [], null, [], [], "forms");

    [Fact]
    public void Fresh_resume_and_project_updates_preserve_other_projects()
    {
        using var temp = new TempDirectory();
        var config = Path.Combine(temp.Path, "config");
        var site = Directory.CreateDirectory(Path.Combine(temp.Path, "site")).FullName;
        byte[] other;
        using (var store = WebFormsWizardStore.Open(config, false))
        {
            store.SaveProject(Project(site));
            store.SaveProject(Project(site, "second"));
            other = File.ReadAllBytes(store.ProjectPath("second"));
            store.SaveProject(Project(site) with { Step = "publication" });
            Assert.Equal(3, store.State.Revision);
        }
        using var resumed = WebFormsWizardStore.Open(config, true);
        Assert.Equal("publication", resumed.ReadProject("website").Step);
        Assert.Equal(other, File.ReadAllBytes(resumed.ProjectPath("second")));
        Assert.Equal(2, resumed.State.Projects.Length);
    }

    [Fact]
    public void Interrupted_project_registration_is_recoverable_but_other_folders_stay_fail_closed()
    {
        using var temp = new TempDirectory();
        var site = Directory.CreateDirectory(Path.Combine(temp.Path, "site")).FullName;
        var config = Path.Combine(temp.Path, "config");
        using var store = WebFormsWizardStore.Open(config, false);
        Assert.True(File.Exists(Path.Combine(config, "root.config.json")));
        // Simulate a crash after the project file write but before root registration.
        Directory.CreateDirectory(Path.Combine(config, "website"));
        File.WriteAllText(Path.Combine(config, "website", "project.config.json"), "{\"partial\":");
        File.WriteAllText(Path.Combine(config, "website", "project.config.json.0123.tmp"), "");
        store.SaveProject(Project(site));
        Assert.Equal("website", store.ReadProject("website").Id);

        Directory.CreateDirectory(Path.Combine(config, "other"));
        File.WriteAllText(Path.Combine(config, "other", "unrelated.txt"), "keep");
        Assert.Equal("WEBFORMS_WIZARD_PROJECT_FOLDER_EXISTS", Assert.Throws<InvalidOperationException>(() =>
            store.SaveProject(Project(site, "other"))).Message);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(config, "other", "unrelated.txt")));
        Directory.CreateDirectory(Path.Combine(config, "empty"));
        Assert.Equal("WEBFORMS_WIZARD_PROJECT_FOLDER_EXISTS", Assert.Throws<InvalidOperationException>(() =>
            store.SaveProject(Project(site, "empty"))).Message);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(config, "empty")));
        Assert.Single(store.State.Projects);
    }

    [Fact]
    public void Caller_cannot_mutate_retained_root_references()
    {
        using var temp = new TempDirectory();
        var site = Directory.CreateDirectory(Path.Combine(temp.Path, "site")).FullName;
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site));
        store.State.Projects[0] = new("other", new string('0', 64));
        Assert.Equal("website", store.ReadProject("website").Id);
        Assert.Equal("website", store.State.Projects[0].Id);
    }

    [Fact]
    public void Duplicate_and_unknown_json_properties_are_refused()
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "config");
        using (WebFormsWizardStore.Open(root, false)) { }
        var path = Path.Combine(root, "root.config.json");
        var original = File.ReadAllText(path);
        File.WriteAllText(path, original.Insert(1, "\"visibility\":\"local-only\","));
        Assert.Equal("WEBFORMS_WIZARD_DUPLICATE_PROPERTY", Assert.Throws<InvalidOperationException>(() => WebFormsWizardStore.Open(root, true)).Message);
        File.WriteAllText(path, original.Insert(1, "\"unexpected\":true,"));
        Assert.Throws<System.Text.Json.JsonException>(() => WebFormsWizardStore.Open(root, true));
    }

    [Fact]
    public void Concurrent_open_and_accidental_start_over_are_refused()
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "config");
        using var store = WebFormsWizardStore.Open(root, false);
        Assert.Equal("WEBFORMS_WIZARD_ROOT_BUSY", Assert.Throws<InvalidOperationException>(() => WebFormsWizardStore.Open(root, true)).Message);
        Assert.Equal("WEBFORMS_WIZARD_ROOT_EXISTS", Assert.Throws<InvalidOperationException>(() => WebFormsWizardStore.Open(root, false)).Message);
    }

    [Fact]
    public void Changed_project_is_localized_and_never_overwritten()
    {
        using var temp = new TempDirectory();
        var site = Directory.CreateDirectory(Path.Combine(temp.Path, "site")).FullName;
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site));
        store.SaveProject(Project(site, "other"));
        var path = store.ProjectPath("website");
        File.AppendAllText(path, " ");
        var bytes = File.ReadAllBytes(path);
        Assert.Equal("WEBFORMS_WIZARD_PROJECT_CHANGED", Assert.Throws<InvalidOperationException>(() => store.ReadProject("website")).Message);
        Assert.Equal("forms", store.ReadProject("other").Step);
        Assert.Throws<InvalidOperationException>(() => store.SaveProject(Project(site)));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("boundedInputSha256")]
    [InlineData("generatorSha256")]
    public void Root_metadata_corruption_fails_closed(string field)
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "config");
        using (WebFormsWizardStore.Open(root, false)) { }
        var path = Path.Combine(root, "root.config.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        json[field] = "invalid";
        File.WriteAllText(path, json.ToJsonString());
        Assert.Equal("WEBFORMS_WIZARD_CONFIG_INVALID", Assert.Throws<InvalidOperationException>(() => WebFormsWizardStore.Open(root, true)).Message);
    }

    [Fact]
    public void Changed_root_and_disposed_store_cannot_write()
    {
        using var temp = new TempDirectory();
        var site = Directory.CreateDirectory(Path.Combine(temp.Path, "site")).FullName;
        var root = Path.Combine(temp.Path, "config");
        var store = WebFormsWizardStore.Open(root, false);
        File.AppendAllText(Path.Combine(root, "root.config.json"), " ");
        Assert.Equal("WEBFORMS_WIZARD_ROOT_CHANGED", Assert.Throws<InvalidOperationException>(() => store.SaveProject(Project(site))).Message);
        store.Dispose();
        Assert.Throws<ObjectDisposedException>(() => store.SaveProject(Project(site)));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("runs")]
    [InlineData("con")]
    [InlineData("lpt1")]
    public void Unsafe_project_identifiers_are_rejected(string id)
    {
        using var temp = new TempDirectory();
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        Assert.Equal("WEBFORMS_WIZARD_PROJECT_ID_INVALID", Assert.Throws<InvalidOperationException>(() => store.SaveProject(Project(temp.Path, id))).Message);
    }

    [Fact]
    public void Config_cannot_be_inside_source_or_contain_source()
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "config");
        using var store = WebFormsWizardStore.Open(root, false);
        Assert.Equal("WEBFORMS_WIZARD_CONFIG_OVERLAPS_INPUT", Assert.Throws<InvalidOperationException>(() => store.SaveProject(Project(temp.Path))).Message);
        var inside = Directory.CreateDirectory(Path.Combine(root, "site")).FullName;
        Assert.Equal("WEBFORMS_WIZARD_CONFIG_OVERLAPS_INPUT", Assert.Throws<InvalidOperationException>(() => store.SaveProject(Project(inside))).Message);
    }
}
