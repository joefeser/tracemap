using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardBuildTests
{
    [Fact]
    public async Task Process_adapter_runs_installed_dotnet_version_without_shell()
    {
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.Combine(
            Directory.GetParent(runtime)!.Parent!.Parent!.FullName, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        Assert.True(File.Exists(host), "The test runtime must expose its dotnet host for process-adapter validation.");
        using var temp = new TempDirectory();
        var result = await TraceMap.Cli.WebFormsWizardProcess.RunAsync(host, temp.Path, ["--version"], CancellationToken.None);
        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"\A\d+\.\d+", result.StandardOutput.Trim());
    }

    [Fact]
    public async Task Consented_sdk_fixture_build_uses_real_process_adapter()
    {
        using var temp = new TempDirectory();
        var (project, _) = Fixture(temp.Path);
        File.WriteAllText(Path.Combine(project.WebRoot, "Fixture.cs"), "public class Fixture { public int Value => 42; }");
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.Combine(
            Directory.GetParent(runtime)!.Parent!.Parent!.FullName, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(project);
        Assert.True(await WebFormsWizardBuild.ExecuteAsync(store, "site", WebFormsWizardBuild.Plan(project, host), true,
            TraceMap.Cli.WebFormsWizardProcess.RunAsync));
        Assert.True(File.Exists(Path.Combine(project.WebRoot, "bin", "Debug", "net10.0", "Site.dll")));
        Assert.Equal("publication", store.ReadProject("site").Step);
        Assert.NotNull(store.ReadProject("site").Build);
    }

    [Fact]
    public async Task Declined_build_never_invokes_runner_or_advances()
    {
        using var temp = new TempDirectory();
        var (project, tool) = Fixture(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(project);
        Assert.False(await WebFormsWizardBuild.ExecuteAsync(store, project.Id, WebFormsWizardBuild.Plan(project, tool), false,
            (_, _, _, _) => throw new Exception("must not run")));
        Assert.Equal("build", store.ReadProject(project.Id).Step);
    }

    [Theory]
    [InlineData(0, 0, "publication", null)]
    [InlineData(1, 0, "build", "WEBFORMS_WIZARD_TOOLCHAIN_PROBE_FAILED")]
    [InlineData(0, 1, "build", "WEBFORMS_WIZARD_BUILD_FAILED")]
    public async Task Only_successful_probe_and_build_advance(int probeCode, int buildCode, string step, string? error)
    {
        using var temp = new TempDirectory();
        var (project, tool) = Fixture(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(project);
        var commands = new List<string[]>();
        Task<WebFormsWizardProcessResult> Runner(string exe, string cwd, IReadOnlyList<string> args, CancellationToken token)
        {
            commands.Add(args.ToArray());
            return Task.FromResult(new WebFormsWizardProcessResult(commands.Count == 1 ? probeCode : buildCode, "10.0\n", ""));
        }
        var plan = WebFormsWizardBuild.Plan(project, tool);
        if (error is null) Assert.True(await WebFormsWizardBuild.ExecuteAsync(store, project.Id, plan, true, Runner));
        else Assert.Equal(error, (await Assert.ThrowsAsync<InvalidOperationException>(() => WebFormsWizardBuild.ExecuteAsync(store, project.Id, plan, true, Runner))).Message);
        var saved = store.ReadProject(project.Id);
        Assert.Equal(step, saved.Step);
        Assert.Equal(new[] { "--version" }, commands[0]);
        Assert.Equal(probeCode == 0 ? 2 : 1, commands.Count);
        if (error is null)
        {
            Assert.NotNull(saved.Build);
            WebFormsWizardBuild.ValidateRetained(saved);
            File.AppendAllText(project.InputPath, " ");
            Assert.Throws<InvalidOperationException>(() => WebFormsWizardBuild.ValidateRetained(saved));
        }
    }

    [Fact]
    public async Task Changed_tool_or_preview_is_rejected_before_any_execution()
    {
        using var temp = new TempDirectory();
        var (project, tool) = Fixture(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(project);
        var plan = WebFormsWizardBuild.Plan(project, tool);
        File.AppendAllText(tool, "changed");
        Assert.Equal("WEBFORMS_WIZARD_BUILD_PLAN_CHANGED", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WebFormsWizardBuild.ExecuteAsync(store, project.Id, plan, true, (_, _, _, _) => throw new Exception("must not run")))).Message);
    }

    [Fact]
    public async Task Changed_input_during_build_does_not_advance()
    {
        using var temp = new TempDirectory();
        var (project, tool) = Fixture(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        store.SaveProject(project);
        var plan = WebFormsWizardBuild.Plan(project, tool);
        var call = 0;
        Assert.Equal("WEBFORMS_WIZARD_BUILD_INPUT_CHANGED", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WebFormsWizardBuild.ExecuteAsync(store, project.Id, plan, true, (_, _, _, _) =>
            {
                if (++call == 2) File.AppendAllText(project.InputPath, " ");
                return Task.FromResult(new WebFormsWizardProcessResult(0, "10.0", ""));
            }))).Message);
        Assert.Equal("build", store.ReadProject(project.Id).Step);
    }

    private static (WebFormsWizardProject, string) Fixture(string root)
    {
        var site = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        File.WriteAllText(Path.Combine(site, "web.config"), "<configuration />");
        File.WriteAllText(Path.Combine(site, "Default.aspx"), "page");
        var path = Path.Combine(site, "Site.csproj");
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var tool = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        File.WriteAllText(tool, "not executed; injected process runner");
        return (new("site", path, site, "project", "all", ["Default.aspx"], null, [], [], "build"), tool);
    }
}
