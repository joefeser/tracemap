using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardNativeTests
{
    [Theory]
    [InlineData(127)]
    [InlineData(1023)]
    public async Task Page_maps_use_publish_inventory_budget_not_compiled_input_budget(int maps)
    {
        using var temp = new TempDirectory();
        var (site, published) = WebFormsWizardPublicationTests.Fixture(temp.Path);
        for (var i = 0; i < maps; i++)
        {
            File.WriteAllText(Path.Combine(site, $"Page{i}.aspx"), "<%@ Page Language=\"VB\" %>");
            File.WriteAllText(Path.Combine(published, "bin", $"Page{i}.compiled"),
                $"<preserve virtualPath=\"/Page{i}.aspx\" assembly=\"Site\" type=\"Public.Page{i}\" />");
        }
        InitGit(site);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        Prepare(store, site, published);
        var path = await WebFormsWizardNative.ConfigureAsync(store, "site");
        var plan = await WebFormsReviewPreflightCommand.BuildAsync(path, Path.Combine(temp.Path, "review"), default);
        Assert.Equal(maps, plan.Inputs.Count(input => input.Role == "page-map"));
        Assert.True(plan.Inputs.Count > plan.Configuration.Budgets.MaxInputFiles);
        Assert.True(plan.Inputs.Count(input => !WebFormsReviewPreflightCommand.IsPublishInventoryRole(input.Role))
            <= plan.Configuration.Budgets.MaxInputFiles);
        Assert.Equal("ready", store.ReadProject("site").Step);
        var legacy = plan.Configuration with { Budgets = plan.Configuration.Budgets with { MaxPublishInputFiles = null } };
        var legacyPath = Path.Combine(temp.Path, "legacy.config.json");
        File.WriteAllText(legacyPath, JsonSerializer.Serialize(legacy, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var error = await Record.ExceptionAsync(() => WebFormsReviewPreflightCommand.BuildAsync(legacyPath,
            Path.Combine(temp.Path, "legacy-review"), default));
        Assert.NotNull(error);
        Assert.Equal(maps > 256 ? "WEBFORMS_PREFLIGHT_CONFIG_INVALID" : "WEBFORMS_PREFLIGHT_INPUT_COUNT_LIMIT", error.Message);
    }

    [Theory]
    [InlineData("cli")]
    [InlineData("core")]
    public async Task Generator_identity_commits_both_implementations_without_installation_paths(string changed)
    {
        using var temp = new TempDirectory();
        var cli = Path.Combine(temp.Path, "cli.dll");
        var core = Path.Combine(temp.Path, "core.dll");
        File.WriteAllText(cli, "CLI implementation");
        File.WriteAllText(core, "Core implementation");
        var first = await WebFormsWizardNative.GeneratorHashAsync(cli, core);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(core, copy);
        Assert.Equal(first, await WebFormsWizardNative.GeneratorHashAsync(cli, copy));
        File.AppendAllText(changed == "cli" ? cli : core, " changed");
        Assert.NotEqual(first, await WebFormsWizardNative.GeneratorHashAsync(cli, core));
    }

    [Fact]
    public async Task Generates_native_config_and_stages_without_modifying_source_or_publication()
    {
        using var temp = new TempDirectory();
        var (site, published) = WebFormsWizardPublicationTests.Fixture(temp.Path);
        InitGit(site);
        var sourceBefore = Hash(Path.Combine(site, "Default.aspx"));
        var binaryBefore = Hash(Path.Combine(published, "bin", "Site.dll"));
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        Prepare(store, site, published);
        var path = await WebFormsWizardNative.ConfigureAsync(store, "site");
        var configuration = JsonSerializer.Deserialize<WebFormsReviewConfig>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(WebFormsWizardNative.RuleId, configuration.WizardProvenance!.RuleId);
        Assert.Equal(await WebFormsWizardNative.GeneratorHashAsync(typeof(WebFormsWizardNative).Assembly.Location,
            typeof(WebFormsWizardStore).Assembly.Location), configuration.WizardProvenance.GeneratorSha256);
        Assert.Equal("projectless", configuration.ProjectMode);
        Assert.Empty(configuration.BindingReceipts);
        Assert.Null(configuration.PreparationProvenance);
        Assert.NotEqual(WebFormsWizardStore.Physical(published), configuration.PublishedRoot);
        Assert.Equal(binaryBefore, Hash(Path.Combine(configuration.PublishedRoot, "bin", "Site.dll")));
        Assert.Equal(sourceBefore, Hash(Path.Combine(site, "Default.aspx")));
        Assert.Equal(binaryBefore, Hash(Path.Combine(published, "bin", "Site.dll")));
        Assert.Equal("ready", store.ReadProject("site").Step);
        Assert.Equal(path, await WebFormsWizardNative.ValidateAsync(store, "site"));
        File.AppendAllText(Path.Combine(configuration.PublishedRoot, "bin", "Site.dll"), "changed");
        Assert.Equal("WEBFORMS_WIZARD_STAGED_INPUT_CHANGED", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WebFormsWizardNative.ValidateAsync(store, "site"))).Message);
    }

    [Fact]
    public async Task Reconfiguration_after_repair_keeps_prior_native_config_and_staged_paths()
    {
        using var temp = new TempDirectory();
        var (site, published) = WebFormsWizardPublicationTests.Fixture(temp.Path);
        InitGit(site);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        Prepare(store, site, published);
        var oldPath = await WebFormsWizardNative.ConfigureAsync(store, "site");
        var oldBytes = File.ReadAllBytes(oldPath);
        var prior = store.ReadProject("site");
        store.RepairProject(prior, store.PreviewRepair("site"), true);
        store.SaveProject(store.ReadProject("site") with { Step = "publication", Forms = ["Default.aspx"] });
        WebFormsWizardPublication.Configure(store, "site", published, ["bin/Site.dll"], []);
        store.SaveProject(store.ReadProject("site") with { Step = "configuration" });
        var nextPath = await WebFormsWizardNative.ConfigureAsync(store, "site");
        Assert.NotEqual(oldPath, nextPath);
        Assert.Equal(oldBytes, File.ReadAllBytes(oldPath));
        Assert.All(prior.Native!.Inputs, input => Assert.Equal(input.Sha256, Hash(input.Path)));
        Assert.Equal(nextPath, await WebFormsWizardNative.ValidateAsync(store, "site"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task External_dependency_is_staged_inside_native_publication_root(bool identicalCopies)
    {
        using var temp = new TempDirectory();
        var (site, published) = WebFormsWizardPublicationTests.Fixture(temp.Path);
        InitGit(site);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        Prepare(store, site, published);
        store.SaveProject(store.ReadProject("site") with { Step = "dependencies" });
        var external = typeof(TraceMapCommand).Assembly.Location;
        var copy = Path.Combine(Directory.CreateDirectory(Path.Combine(temp.Path, "other")).FullName, Path.GetFileName(external));
        File.Copy(external, copy);
        WebFormsWizardPublication.Configure(store, "site", published, ["bin/Site.dll"], identicalCopies ? [external, copy] : [external]);
        store.SaveProject(store.ReadProject("site") with { Step = "configuration" });
        var path = await WebFormsWizardNative.ConfigureAsync(store, "site");
        var config = JsonSerializer.Deserialize<WebFormsReviewConfig>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Single(config.DependencyAssemblies);
        Assert.Equal(config.DependencyAssemblies.Length, config.DependencyAssemblies.Distinct().Count());
        foreach (var dependency in config.DependencyAssemblies)
        {
            Assert.StartsWith("dependencies/", dependency);
            Assert.Equal(Hash(external), Hash(Path.Combine(config.PublishedRoot, dependency)));
        }
        Assert.Equal(path, await WebFormsWizardNative.ValidateAsync(store, "site"));
        if (identicalCopies)
        {
            File.AppendAllText(copy, "changed");
            Assert.Equal("WEBFORMS_WIZARD_INPUT_CHANGED", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WebFormsWizardNative.ValidateAsync(store, "site"))).Message);
        }
    }

    [Fact]
    public async Task Missing_git_identity_cannot_generate_ready_config()
    {
        using var temp = new TempDirectory();
        var (site, published) = WebFormsWizardPublicationTests.Fixture(temp.Path);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        Prepare(store, site, published);
        Assert.Equal("WEBFORMS_WIZARD_COMMITTED_SOURCE_WITH_REMOTE_REQUIRED", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WebFormsWizardNative.ConfigureAsync(store, "site"))).Message);
        Assert.Equal("configuration", store.ReadProject("site").Step);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(store.ProjectPath("site"))!, "native.config.json")));
    }

    [Fact]
    public async Task Changed_native_configuration_is_not_reused_or_overwritten()
    {
        using var temp = new TempDirectory();
        var (site, published) = WebFormsWizardPublicationTests.Fixture(temp.Path);
        InitGit(site);
        using var store = WebFormsWizardStore.Open(Path.Combine(temp.Path, "config"), false);
        Prepare(store, site, published);
        var path = await WebFormsWizardNative.ConfigureAsync(store, "site");
        File.AppendAllText(path, " ");
        Assert.Equal("WEBFORMS_WIZARD_NATIVE_CONFIG_CHANGED", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WebFormsWizardNative.ValidateAsync(store, "site"))).Message);
    }

    private static void Prepare(WebFormsWizardStore store, string site, string published)
    {
        site = WebFormsWizardStore.Physical(site);
        store.SaveProject(new("site", site, site, "projectless", "all", ["Default.aspx"], null, [], [], "publication"));
        WebFormsWizardPublication.Configure(store, "site", published, ["bin/Site.dll"], []);
        store.SaveProject(store.ReadProject("site") with { Step = "configuration" });
    }
    internal static void InitGit(string root)
    {
        Git(root, "init", "-q");
        Git(root, "config", "user.name", "Wizard fixture");
        Git(root, "config", "user.email", "wizard@example.invalid");
        Git(root, "remote", "add", "origin", "https://example.invalid/wizard-fixture.git");
        Git(root, "add", ".");
        Git(root, "commit", "-qm", "fixture");
    }
    private static void Git(string root, params string[] args)
    {
        using var process = new Process { StartInfo = new("git") { WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(30_000));
        Assert.Equal(0, process.ExitCode);
        Task.WaitAll(output, error);
    }
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
