using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardFormsTests
{
    [Theory]
    [InlineData("Bin")]
    [InlineData("OBJ")]
    [InlineData(".Git")]
    public void Discovery_and_resume_skip_case_variant_build_directories(string name)
    {
        using var temp = new TempDirectory();
        var ignored = Directory.CreateDirectory(Path.Combine(temp.Path, name)).FullName;
        File.WriteAllText(Path.Combine(ignored, "Generated.aspx"), "ignored");
        File.WriteAllText(Path.Combine(temp.Path, "A.aspx"), "page");
        if (!OperatingSystem.IsWindows())
            File.CreateSymbolicLink(Path.Combine(ignored, "linked.aspx"), Path.Combine(temp.Path, "A.aspx"));
        Assert.Equal(new[] { "A.aspx" }, WebFormsWizardForms.Discover(temp.Path));
        Assert.Equal(new[] { "A.aspx" }, WebFormsWizardForms.Parse(temp.Path, "A.aspx"));
    }

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

    [Theory]
    [InlineData("A.aspx", "a.aspx")]
    [InlineData("Pages/A.aspx", "pages/A.aspx")]
    public void Subset_revalidates_case_identity_against_surrounding_inventory(string first, string sibling)
    {
        using var temp = new TempDirectory();
        var firstPath = Path.Combine(temp.Path, first);
        var siblingPath = Path.Combine(temp.Path, sibling);
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        File.WriteAllText(firstPath, "first");
        Assert.Single(WebFormsWizardForms.Parse(temp.Path, first));
        Directory.CreateDirectory(Path.GetDirectoryName(siblingPath)!);
        File.WriteAllText(siblingPath, "second");
        if (Directory.GetFiles(temp.Path, "*.aspx", SearchOption.AllDirectories).Length == 1)
        {
            // Case-insensitive hosts cannot represent the collision; Linux CI exercises rejection.
            Assert.Single(WebFormsWizardForms.Parse(temp.Path, first));
            return;
        }
        foreach (var selection in new[] { first, sibling, firstPath })
            Assert.Equal("WEBFORMS_WIZARD_CASE_AMBIGUOUS", Assert.Throws<InvalidOperationException>(() =>
                WebFormsWizardForms.Parse(temp.Path, selection)).Message);
        Assert.Equal("WEBFORMS_WIZARD_CASE_AMBIGUOUS", Assert.Throws<InvalidOperationException>(() =>
            WebFormsWizardForms.Discover(temp.Path)).Message);
    }

    [Fact]
    public void Unicode_selection_uses_the_same_utf8_byte_bound_as_resume()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "界.aspx"), "page");
        Assert.Equal(new[] { "界.aspx" }, WebFormsWizardForms.Parse(temp.Path, WebFormsWizardForms.Template(temp.Path)));
        var oversized = "#" + new string('界', WebFormsWizardForms.MaxSelectionBytes / 3 + 1);
        Assert.True(oversized.Length < WebFormsWizardForms.MaxSelectionChars);
        Assert.Equal("WEBFORMS_WIZARD_SELECTION_LIMIT", Assert.Throws<InvalidOperationException>(() =>
            WebFormsWizardForms.Parse(temp.Path, oversized)).Message);
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
