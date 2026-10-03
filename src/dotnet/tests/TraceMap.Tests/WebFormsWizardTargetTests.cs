using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardTargetTests
{
    private static string Site(string parent, string name = "site")
    {
        var root = Directory.CreateDirectory(Path.Combine(parent, name)).FullName;
        File.WriteAllText(Path.Combine(root, "web.config"), "<configuration />");
        File.WriteAllText(Path.Combine(root, "Default.aspx"), "<%@ Page Language=\"C#\" %>");
        return root;
    }

    [Fact]
    public void Folder_is_projectless_and_requires_manual_windows_publication()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var target = WebFormsWizardTarget.Inspect(site);
        Assert.Equal("projectless", target.ProjectMode);
        Assert.Equal("manual-aspnet-compiler", target.BuildTool);
        Assert.True(target.RequiresWindows);
        File.WriteAllText(Path.Combine(site, "Site.csproj"), "<Project />");
        Assert.Equal("WEBFORMS_WIZARD_SELECT_PROJECT_FILE", Assert.Throws<InvalidOperationException>(() => WebFormsWizardTarget.Inspect(site)).Message);
    }

    [Theory]
    [InlineData("csproj", "<Project><PropertyGroup><TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion></PropertyGroup></Project>", "windows-msbuild", true)]
    [InlineData("vbproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net48</TargetFramework></PropertyGroup></Project>", "windows-msbuild", true)]
    [InlineData("csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>", "dotnet", false)]
    [InlineData("csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>$(ImportedTarget)</TargetFramework></PropertyGroup></Project>", "unknown", false)]
    public void Project_inspection_does_not_evaluate_msbuild(string extension, string content, string tool, bool windows)
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var path = Path.Combine(site, "Site." + extension);
        File.WriteAllText(path, content);
        var target = WebFormsWizardTarget.Inspect(path);
        Assert.Equal(tool, target.BuildTool);
        Assert.Equal(windows, target.RequiresWindows);
        Assert.Equal("project", target.ProjectMode);
        Assert.Equal(WebFormsWizardStore.Physical(path), target.SelectedProject);
    }

    [Fact]
    public void Solution_requires_explicit_member_web_root_and_does_not_register_other_projects()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var path = Path.Combine(site, "Site.csproj");
        File.WriteAllText(path, "<Project />");
        var solution = Path.Combine(temp.Path, "Site.sln");
        File.WriteAllText(solution, "Microsoft Visual Studio Solution File, Format Version 12.00\n" +
            "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Site\", \"site\\Site.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\nEndProject\n");
        Assert.Equal("WEBFORMS_WIZARD_SOLUTION_WEB_ROOT_REQUIRED", Assert.Throws<InvalidOperationException>(() => WebFormsWizardTarget.Inspect(solution)).Message);
        Assert.Equal("solution", WebFormsWizardTarget.Inspect(solution, site).ProjectMode);
        var other = Site(temp.Path, "other");
        Assert.Equal("WEBFORMS_WIZARD_WEB_ROOT_NOT_IN_SOLUTION", Assert.Throws<InvalidOperationException>(() => WebFormsWizardTarget.Inspect(solution, other)).Message);
    }

    [Fact]
    public void Solution_website_entry_is_projectless_and_ambiguous_entries_fail()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var solution = Path.Combine(temp.Path, "Site.sln");
        var entry = "Project(\"{E24C65DC-7377-472B-9ABA-BC803B73C61A}\") = \"Site\", \"site\\\", \"{11111111-1111-1111-1111-111111111111}\"\nEndProject\n";
        File.WriteAllText(solution, "Microsoft Visual Studio Solution File, Format Version 12.00\n" + entry);
        Assert.Equal("projectless", WebFormsWizardTarget.Inspect(solution, site).ProjectMode);
        File.AppendAllText(solution, entry);
        Assert.Equal("WEBFORMS_WIZARD_WEB_ROOT_AMBIGUOUS", Assert.Throws<InvalidOperationException>(() => WebFormsWizardTarget.Inspect(solution, site)).Message);
    }

    [Fact]
    public void Wrong_root_missing_marker_and_xml_entities_are_refused()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var path = Path.Combine(site, "Site.csproj");
        File.WriteAllText(path, "<Project />");
        Assert.Equal("WEBFORMS_WIZARD_WEB_ROOT_MISMATCH", Assert.Throws<InvalidOperationException>(() => WebFormsWizardTarget.Inspect(path, temp.Path)).Message);
        File.Delete(Path.Combine(site, "web.config"));
        Assert.Equal("WEBFORMS_WIZARD_WEB_CONFIG_MISSING", Assert.Throws<InvalidOperationException>(() => WebFormsWizardTarget.Inspect(path)).Message);
        File.WriteAllText(path, "<!DOCTYPE Project [<!ENTITY external SYSTEM 'file:///not-read'>]><Project>&external;</Project>");
        Assert.Throws<System.Xml.XmlException>(() => WebFormsWizardTarget.Inspect(path));
    }
}
