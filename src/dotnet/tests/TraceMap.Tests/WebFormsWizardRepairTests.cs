using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardRepairTests
{
    [Theory]
    [InlineData("broken json")]
    [InlineData("")]
    public void Confirmed_repair_archives_corrupt_config_and_preserves_other_projects_and_native_paths(string corrupt)
    {
        using var temp = new TempDirectory();
        var site = Directory.CreateDirectory(Path.Combine(temp.Path, "site")).FullName;
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        var replacement = Project(site);
        store.SaveProject(replacement);
        store.SaveProject(replacement with { Id = "other" });
        var other = File.ReadAllBytes(store.ProjectPath("other"));
        var folder = Path.GetDirectoryName(store.ProjectPath("site"))!;
        var staged = Directory.CreateDirectory(Path.Combine(folder, "publication-old")).FullName;
        File.WriteAllText(Path.Combine(staged, "keep.dll"), "old pinned bytes");
        File.WriteAllText(Path.Combine(folder, "native.config.json"), "old native config");
        File.WriteAllText(Path.Combine(folder, "forms.txt"), "Old.aspx\n");
        File.WriteAllText(store.ProjectPath("site"), corrupt);
        Assert.Throws<InvalidOperationException>(() => store.ReadProject("site"));
        var preview = store.PreviewRepair("site");
        var archive = store.RepairProject(replacement, preview, true)!;
        Assert.Equal(corrupt, File.ReadAllText(Path.Combine(archive, "project.config.original.json")));
        Assert.Equal("Old.aspx\n", File.ReadAllText(Path.Combine(archive, "forms.txt")));
        Assert.Equal("old pinned bytes", File.ReadAllText(Path.Combine(staged, "keep.dll")));
        Assert.Equal("old native config", File.ReadAllText(Path.Combine(folder, "native.config.json")));
        Assert.Equal(other, File.ReadAllBytes(store.ProjectPath("other")));
        Assert.Equal("forms", store.ReadProject("site").Step);
        Assert.Null(store.ReadProject("site").Run);
        Assert.True(File.Exists(Path.Combine(archive, "repair.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Declined_or_stale_preview_repair_does_not_change_files(bool truncate)
    {
        using var temp = new TempDirectory();
        var site = Directory.CreateDirectory(Path.Combine(temp.Path, "site")).FullName;
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site));
        if (truncate) File.WriteAllBytes(store.ProjectPath("site"), []);
        var preview = store.PreviewRepair("site");
        var before = File.ReadAllBytes(store.ProjectPath("site"));
        Assert.Null(store.RepairProject(Project(site), preview, false));
        Assert.Equal(before, File.ReadAllBytes(store.ProjectPath("site")));
        File.AppendAllText(store.ProjectPath("site"), " ");
        Assert.Equal("WEBFORMS_WIZARD_REPAIR_PREVIEW_CHANGED", Assert.Throws<InvalidOperationException>(() =>
            store.RepairProject(Project(site), preview, true)).Message);
        Assert.False(Directory.Exists(Path.Combine(store.DirectoryPath, "project-history")));
    }

    [Fact]
    public void Missing_project_file_can_be_recreated_only_for_a_registered_project()
    {
        using var temp = new TempDirectory();
        var site = Directory.CreateDirectory(Path.Combine(temp.Path, "site")).FullName;
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site));
        File.Delete(store.ProjectPath("site"));
        Assert.Equal("missing", store.PreviewRepair("site"));
        Assert.NotNull(store.RepairProject(Project(site), "missing", true));
        Assert.Equal("forms", store.ReadProject("site").Step);
        Assert.Throws<InvalidOperationException>(() => store.PreviewRepair("unregistered"));
    }

    private static WebFormsWizardProject Project(string site) => new("site", site, site, "projectless", "selected", [], null, [], [], "forms");
}
