using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardSelectionTests
{
    [Fact]
    public void Subset_pauses_then_resumes_after_process_restart_without_other_answers()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var config = Path.Combine(temp.Path, "config");
        string edit;
        using (var store = WebFormsWizardStore.Open(config, false))
        {
            store.SaveProject(Project(site));
            var result = WebFormsWizardSelection.Advance(store, "site");
            Assert.True(result.Paused);
            edit = result.EditFile!;
            Assert.Contains("Other.aspx", File.ReadAllText(edit));
            Assert.Equal("forms", store.ReadProject("site").Step);
        }
        File.WriteAllText(edit, "Default.aspx\n");
        using var resumed = WebFormsWizardStore.Open(config, true);
        var next = WebFormsWizardSelection.Advance(resumed, "site");
        Assert.False(next.Paused);
        Assert.Equal("build", next.Step);
        Assert.Equal(new[] { "Default.aspx" }, resumed.ReadProject("site").Forms);
        File.Delete(Path.Combine(site, "Default.aspx"));
        Assert.Throws<InvalidOperationException>(() => WebFormsWizardSelection.Advance(resumed, "site"));
    }

    [Fact]
    public void Blank_regenerates_but_comments_only_does_not_silently_select_all()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site));
        var path = WebFormsWizardSelection.Advance(store, "site").EditFile!;
        File.WriteAllText(path, " \n");
        Assert.True(WebFormsWizardSelection.Advance(store, "site").Paused);
        File.WriteAllText(path, "# deliberately no selection\n");
        Assert.Throws<InvalidOperationException>(() => WebFormsWizardSelection.Advance(store, "site"));
        Assert.Equal("forms", store.ReadProject("site").Step);
        Assert.Equal("# deliberately no selection\n", File.ReadAllText(path));
    }

    [Fact]
    public void Blank_replacement_is_atomic_with_a_delete_sharing_reader_and_leaves_no_staging_files()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site));
        var path = Path.Combine(Path.GetDirectoryName(store.ProjectPath("site"))!, "forms.txt");
        File.WriteAllText(path, " \n");
        using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            Assert.True(WebFormsWizardSelection.Advance(store, "site").Paused);
            Assert.Equal(2, reader.Length);
        }
        Assert.Contains("Default.aspx", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        Assert.Equal("forms", store.ReadProject("site").Step);
    }

    [Fact]
    public void All_advances_without_selection_file_and_resume_rechecks_target()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(Project(site) with { FormsMode = "all" });
        Assert.Equal("build", WebFormsWizardSelection.Advance(store, "site").Step);
        Assert.Equal(2, store.ReadProject("site").Forms.Length);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(store.ProjectPath("site"))!, "forms.txt")));
        File.Delete(Path.Combine(site, "web.config"));
        Assert.Throws<InvalidOperationException>(() => WebFormsWizardSelection.Advance(store, "site"));
    }

    private static WebFormsWizardProject Project(string site) => new("site", site, site, "projectless", "selected", [], null, [], [], "forms");
    private static string Site(string root)
    {
        var site = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        File.WriteAllText(Path.Combine(site, "web.config"), "<configuration />");
        File.WriteAllText(Path.Combine(site, "Default.aspx"), "page");
        File.WriteAllText(Path.Combine(site, "Other.aspx"), "page");
        return site;
    }
}
