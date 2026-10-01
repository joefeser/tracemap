using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardFormsTests
{
    [Fact]
    public void Selection_normalizes_separators_absolute_paths_comments_and_order()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Pages"));
        File.WriteAllText(Path.Combine(temp.Path, "Pages/Z.aspx"), "page");
        File.WriteAllText(Path.Combine(temp.Path, "A.aspx"), "page");
        var text = "# choose pages\r\nPages\\Z.aspx\r\n" + Path.Combine(temp.Path, "A.aspx") + "\n";
        Assert.Equal(new[] { "A.aspx", "Pages/Z.aspx" }, WebFormsWizardForms.Parse(temp.Path, text));
        Assert.Equal(new[] { "A.aspx", "Pages/Z.aspx" }, WebFormsWizardForms.Parse(temp.Path + Path.DirectorySeparatorChar, text));
        Assert.Equal(WebFormsWizardForms.Discover(temp.Path), WebFormsWizardForms.Parse(temp.Path, WebFormsWizardForms.Template(temp.Path)));
    }

    [Theory]
    [InlineData("", "SELECTION_EMPTY")]
    [InlineData("# nothing\n \r\n", "SELECTION_EMPTY")]
    [InlineData("../A.aspx", "PATH_TRAVERSAL")]
    [InlineData("Pages/../A.aspx", "PATH_TRAVERSAL")]
    [InlineData("missing.aspx", "FORM_MISSING")]
    [InlineData("A.aspx.cs", "NOT_FORM")]
    [InlineData("A.aspx\nA.aspx", "DUPLICATE_FORM")]
    public void Invalid_selection_has_typed_error(string selection, string reason)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "A.aspx"), "page");
        Assert.Equal("WEBFORMS_WIZARD_" + reason,
            Assert.Throws<InvalidOperationException>(() => WebFormsWizardForms.Parse(temp.Path, selection)).Message);
    }

    [Fact]
    public void Absolute_sibling_is_not_inside_web_root()
    {
        using var temp = new TempDirectory();
        var web = Directory.CreateDirectory(Path.Combine(temp.Path, "web")).FullName;
        var other = Directory.CreateDirectory(Path.Combine(temp.Path, "web-other")).FullName;
        var page = Path.Combine(other, "A.aspx");
        File.WriteAllText(page, "page");
        Assert.Equal("WEBFORMS_WIZARD_OUTSIDE_ROOT", Assert.Throws<InvalidOperationException>(() => WebFormsWizardForms.Parse(web, page)).Message);
    }

    [Fact]
    public void Inventory_excludes_build_outputs_and_bounds_selection_text()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "bin"));
        File.WriteAllText(Path.Combine(temp.Path, "bin/Generated.aspx"), "page");
        File.WriteAllText(Path.Combine(temp.Path, "A.aspx"), "page");
        Assert.Equal(new[] { "A.aspx" }, WebFormsWizardForms.Discover(temp.Path));
        Assert.Equal("WEBFORMS_WIZARD_SELECTION_LIMIT", Assert.Throws<InvalidOperationException>(() =>
            WebFormsWizardForms.Parse(temp.Path, new string(' ', WebFormsWizardForms.MaxSelectionChars + 1))).Message);
    }
}
