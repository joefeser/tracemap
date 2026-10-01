using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardCommandTests
{
    [Fact]
    public async Task Terminal_subset_then_continue_does_not_ask_setup_questions_again()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var root = Path.Combine(temp.Path, "config");
        var first = await Run(["wizard", "--root", root], $"site\n{site}\nselected\n");
        Assert.Equal(2, first.Code);
        Assert.Empty(first.Error);
        Assert.Contains("Edit ", first.Output);
        File.WriteAllText(Path.Combine(root, "site", "forms.txt"), "Default.aspx\n");
        var next = await Run(["wizard", "--root", root, "--continue"], "");
        Assert.Equal(2, next.Code);
        Assert.Empty(next.Error);
        Assert.DoesNotContain("Project ID", next.Output);
        Assert.DoesNotContain("Point me", next.Output);
        Assert.Contains("saved step 'build'", next.Output);
        using var store = WebFormsWizardStore.Open(root, true);
        Assert.Equal(new[] { "Default.aspx" }, store.ReadProject("site").Forms);
    }

    [Fact]
    public async Task Explicit_add_project_preserves_first_configuration()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var root = Path.Combine(temp.Path, "config");
        Assert.Equal(2, (await Run(["wizard", "--root", root], $"first\n{site}\nall\n")).Code);
        var firstPath = Path.Combine(root, "first", "project.config.json");
        var prior = File.ReadAllBytes(firstPath);
        Assert.Equal(2, (await Run(["wizard", "--root", root, "--continue", "--add-project"], $"second\n{site}\nall\n")).Code);
        Assert.Equal(prior, File.ReadAllBytes(firstPath));
        var choose = await Run(["wizard", "--root", root, "--continue"], "first\n");
        Assert.Equal(2, choose.Code);
        Assert.Contains("Configured projects: first, second", choose.Output);
    }

    [Fact]
    public async Task Existing_root_needs_continue_choice_and_eof_does_not_reset_it()
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "config");
        var site = Site(temp.Path);
        await Run(["wizard", "--root", root], $"site\n{site}\nall\n");
        var before = File.ReadAllBytes(Path.Combine(root, "root.config.json"));
        var stopped = await Run(["wizard", "--root", root], "");
        Assert.Equal(1, stopped.Code);
        Assert.Contains("INPUT_ENDED_CONTINUE_TO_RESUME", stopped.Error);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "root.config.json")));
        Assert.Equal(2, (await Run(["wizard", "--root", root], "continue\n")).Code);
    }

    [Fact]
    public async Task Terminal_build_preview_precedes_explicit_consent()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var project = Path.Combine(site, "Site.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var tool = Path.Combine(temp.Path, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        File.WriteAllText(tool, "test double");
        var root = Path.Combine(temp.Path, "config");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var calls = 0;
        Task<WebFormsWizardProcessResult> Runner(string exe, string cwd, IReadOnlyList<string> args, CancellationToken token)
        {
            Assert.Contains("Type 'build'", output.ToString());
            calls++;
            return Task.FromResult(new WebFormsWizardProcessResult(0, "10.0", ""));
        }
        using var denied = new StringReader($"site\n{project}\nall\nrun\n{tool}\nno\n");
        Assert.Equal(2, await WebFormsWizardCommand.RunAsync(["wizard", "--root", root], denied, output, error, buildRunner: Runner));
        Assert.Equal(0, calls);
        using var approved = new StringReader($"run\n{tool}\nbuild\n");
        Assert.Equal(2, await WebFormsWizardCommand.RunAsync(["wizard", "--root", root, "--continue"], approved, output, error, buildRunner: Runner));
        Assert.Equal(2, calls);
        Assert.Empty(error.ToString());
        using var store = WebFormsWizardStore.Open(root, true);
        Assert.Equal("publication", store.ReadProject("site").Step);
    }

    [Fact]
    public async Task Manual_projectless_publication_is_not_claimed_as_build_proof()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var root = Path.Combine(temp.Path, "config");
        var result = await Run(["wizard", "--root", root], $"site\n{site}\nall\nready\n");
        Assert.Equal(2, result.Code);
        Assert.Contains("not build proof", result.Output);
        using var store = WebFormsWizardStore.Open(root, true);
        Assert.Equal("publication", store.ReadProject("site").Step);
        Assert.Null(store.ReadProject("site").Build);
    }

    [Fact]
    public async Task Invalid_flags_fail_before_creating_configuration()
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "config");
        Assert.Equal(1, (await Run(["wizard", "--root", root, "--add-project"], "")).Code);
        Assert.False(Directory.Exists(root));
        Assert.Equal(1, (await Run(["wizard", "--root", root, "--continue", "--continue"], "")).Code);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task Command_dispatch_help_describes_pause_and_resume()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "wizard", "--help"], output, error));
        Assert.Contains("--continue", output.ToString());
        Assert.Contains("Exit 2", output.ToString());
    }

    private static async Task<(int Code, string Output, string Error)> Run(string[] args, string answers)
    {
        using var input = new StringReader(answers);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await WebFormsWizardCommand.RunAsync(args, input, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static string Site(string parent)
    {
        var site = Directory.CreateDirectory(Path.Combine(parent, "source")).FullName;
        File.WriteAllText(Path.Combine(site, "web.config"), "<configuration />");
        File.WriteAllText(Path.Combine(site, "Default.aspx"), "page");
        return site;
    }
}
