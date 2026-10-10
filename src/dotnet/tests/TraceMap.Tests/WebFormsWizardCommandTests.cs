using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

// Terminal completion also rechecks Git identity; isolate it from concurrent corpus process load.
[Collection("Git metadata sensitive")]
public sealed class WebFormsWizardCommandTests
{
    [Fact]
    public async Task Full_terminal_replay_survives_subset_restart_and_adds_second_project_without_changing_first()
    {
        using var temp = new TempDirectory();
        var (site, published) = WebFormsWizardExecutionTests.Inputs(Path.Combine(temp.Path, "first-input"));
        var commit = GitMetadataProvider.Detect(site).CommitSha;
        var root = Path.Combine(temp.Path, "config");
        var first = await Run(["wizard", "--root", root], $"first\n{site}\nselected\n");
        Assert.Equal(2, first.Code);
        File.WriteAllText(Path.Combine(root, "first", "forms.txt"), "Default.aspx\n");
        var completed = await Run(["wizard", "--root", root, "--continue"], $"ready\n{published}\nall\nnone\nprepare\n{commit}\n");
        Assert.True(completed.Code == 0, completed.Error + completed.Output);
        Assert.Contains("Verified retained reports:", completed.Output);
        var firstConfig = File.ReadAllBytes(Path.Combine(root, "first", "project.config.json"));
        var (otherSite, otherPublished) = WebFormsWizardExecutionTests.Inputs(Path.Combine(temp.Path, "second-input"));
        var otherCommit = GitMetadataProvider.Detect(otherSite).CommitSha;
        var added = await Run(["wizard", "--root", root, "--continue", "--add-project"],
            $"second\n{otherSite}\nall\nready\n{otherPublished}\nall\nnone\nprepare\n{otherCommit}\n");
        Assert.True(added.Code == 0, added.Error + added.Output);
        Assert.Equal(firstConfig, File.ReadAllBytes(Path.Combine(root, "first", "project.config.json")));
        Assert.Equal(2, Directory.GetDirectories(Path.Combine(root, "runs")).Length);
        var verified = await Run(["wizard", "--root", root, "--continue"], "first\n");
        Assert.True(verified.Code == 0, verified.Error + verified.Output);
        Assert.Equal(firstConfig, File.ReadAllBytes(Path.Combine(root, "first", "project.config.json")));
    }

    [Fact]
    public async Task Fresh_config_inside_source_is_rejected_before_creating_any_files()
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var root = Path.Combine(site, "wizard-config");
        var result = await Run(["wizard", "--root", root], $"site\n{site}\nall\n");
        Assert.Equal(1, result.Code);
        Assert.Contains("CONFIG_OVERLAPS_INPUT", result.Error);
        Assert.False(Directory.Exists(root));
    }

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

    [Theory]
    [InlineData("broken config")]
    [InlineData("")]
    public async Task Terminal_repair_requires_confirmation_and_can_replace_corrupt_project_config(string corrupt)
    {
        using var temp = new TempDirectory();
        var site = Site(temp.Path);
        var root = Path.Combine(temp.Path, "config");
        await Run(["wizard", "--root", root], $"site\n{site}\nall\nlater\n");
        var path = Path.Combine(root, "site", "project.config.json");
        File.WriteAllText(path, corrupt);
        var declined = await Run(["wizard", "--root", root, "--continue", "--repair-project", "site"], $"{site}\nall\nno\n");
        Assert.Equal(2, declined.Code);
        Assert.Equal(corrupt, File.ReadAllText(path));
        var accepted = await Run(["wizard", "--root", root, "--continue", "--repair-project", "site"], $"{site}\nall\nsite\n");
        Assert.Equal(2, accepted.Code);
        Assert.Empty(accepted.Error);
        Assert.Contains("Repair archived", accepted.Output);
        var continued = await Run(["wizard", "--root", root, "--continue"], "later\n");
        Assert.Equal(2, continued.Code);
        Assert.Empty(continued.Error);
        Assert.DoesNotContain("Project ID", continued.Output);
    }

    [Fact]
    public async Task Terminal_prepares_native_configuration_without_attestation_or_execution()
    {
        using var temp = new TempDirectory();
        var (site, published) = WebFormsWizardPublicationTests.Fixture(temp.Path);
        WebFormsWizardNativeTests.InitGit(site);
        var root = Path.Combine(temp.Path, "config");
        var result = await Run(["wizard", "--root", root], $"site\n{site}\nall\nready\n{published}\nall\nnone\nprepare\n");
        Assert.Equal(2, result.Code);
        Assert.Empty(result.Error);
        Assert.Contains("Native configuration:", result.Output);
        Assert.False(Directory.Exists(Path.Combine(root, "runs")));
        using var store = WebFormsWizardStore.Open(root, true);
        Assert.Equal("ready", store.ReadProject("site").Step);
    }

    [Fact]
    public async Task Publication_selection_is_retained_across_dependency_pause()
    {
        using var temp = new TempDirectory();
        var (site, published) = WebFormsWizardPublicationTests.Fixture(temp.Path);
        var root = Path.Combine(temp.Path, "config");
        var first = await Run(["wizard", "--root", root], $"site\n{site}\nall\nready\n{published}\n1\nlater\n");
        Assert.Equal(2, first.Code);
        Assert.Empty(first.Error);
        var next = await Run(["wizard", "--root", root, "--continue"], "none\n");
        Assert.Equal(2, next.Code);
        Assert.Empty(next.Error);
        Assert.DoesNotContain("Where is the compiled", next.Output);
        Assert.Contains("saved step 'configuration'", next.Output);
        using var store = WebFormsWizardStore.Open(root, true);
        Assert.Single(store.ReadProject("site").PrimaryAssemblies);
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
