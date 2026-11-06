using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class WorkspaceCommandTests
{
    [Fact]
    public async Task Init_preserves_existing_config_and_requires_explicit_repository_setup()
    {
        using var temp = new TempDirectory();
        var workspace = Path.Combine(temp.Path, "workspace");
        Assert.Equal(0, await Run("init", workspace));
        var config = Path.Combine(workspace, "workspace.local.json");
        var bytes = File.ReadAllBytes(config);
        using var document = JsonDocument.Parse(bytes);
        var provenance = document.RootElement.GetProperty("templateProvenance");
        Assert.Matches("^[0-9a-f]{64}$", provenance.GetProperty("generatorSha256").GetString()!);
        Assert.Matches("^[0-9a-f]{64}$", provenance.GetProperty("boundedInputSha256").GetString()!);
        Assert.Equal(1, await Run("verify", workspace));
        Assert.Equal(1, await Run("init", workspace));
        Assert.Equal(bytes, File.ReadAllBytes(config));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":\"tracemap-workspace.v1\",\"repositories\":[],\"repositories\":[]}")]
    [InlineData("{\"schemaVersion\":\"tracemap-workspace.v1\",\"repositories\":[],\"typo\":true}")]
    [InlineData("{\"schemaVersion\":\"other.v1\",\"repositories\":[]}")]
    [InlineData("{\"schemaVersion\":\"tracemap-workspace.v1\",\"repositories\":[null]}")]
    public async Task Invalid_configuration_never_creates_collection_output(string content)
    {
        using var temp = new TempDirectory();
        var root = Configure(temp.Path, content);
        var output = Path.Combine(temp.Path, "run");
        Assert.Equal(1, await Run("collect", root, "--out", output));
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# nothing selected\n")]
    [InlineData("missing\n")]
    [InlineData("web\nweb\n")]
    public async Task Invalid_scope_never_expands_to_all_repositories(string content)
    {
        using var temp = new TempDirectory();
        var repo = Repository(temp.Path, "web");
        var root = Configure(temp.Path, Configuration(new { id = "web", path = repo, projects = Array.Empty<string>() }));
        var scope = Path.Combine(temp.Path, "scope.txt");
        File.WriteAllText(scope, content);
        var output = Path.Combine(temp.Path, "run");
        Assert.Equal(1, await Run("collect", root, "--scope", scope, "--out", output));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task Filter_selects_only_named_repository_and_relative_config_paths_are_stable()
    {
        using var temp = new TempDirectory();
        Repository(temp.Path, "web");
        var root = Configure(temp.Path, Configuration(
            new { id = "web", path = "../web", projects = Array.Empty<string>() },
            new { id = "framework", path = "../missing", projects = Array.Empty<string>() }));
        File.WriteAllText(Path.Combine(root, "repositories.txt"), "# explicit selection\nweb\n");
        var config = Path.Combine(root, "workspace.local.json");
        File.WriteAllText(config, File.ReadAllText(config).Replace("\"scopeFile\":null", "\"scopeFile\":\"repositories.txt\"", StringComparison.Ordinal));
        Assert.Equal(0, await Run("verify", root));
        Assert.Equal(1, await Run("verify", root, "--scope", Path.Combine(root, "absent.txt")));
    }

    [Fact]
    public async Task Existing_run_and_input_overlap_are_refused_without_changing_inputs()
    {
        using var temp = new TempDirectory();
        var repo = Repository(temp.Path, "web");
        var root = Configure(temp.Path, Configuration(new { id = "web", path = repo, projects = Array.Empty<string>() }));
        var output = Path.Combine(temp.Path, "existing");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "workspace-run.local.json"), "keep");
        Assert.Equal(1, await Run("collect", root, "--out", output));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(output, "workspace-run.local.json")));
        var overlap = Path.Combine(repo, "output");
        Assert.Equal(1, await Run("collect", root, "--out", overlap));
        Assert.False(Directory.Exists(overlap));
    }

    [Fact]
    public async Task Two_real_source_scans_combine_render_and_preserve_partial_coverage()
    {
        using var temp = new TempDirectory();
        var web = Repository(temp.Path, "web");
        var framework = Repository(temp.Path, "framework");
        var root = Configure(temp.Path, Configuration(
            new { id = "web", path = web, projects = Array.Empty<string>() },
            new { id = "framework", path = framework, projects = Array.Empty<string>() }));
        var output = Path.Combine(temp.Path, "run");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = await TraceMapCommand.RunAsync(["workspace", "collect", "--root", root, "--out", output], stdout, stderr);
        Assert.True(code == 0, stderr.ToString());
        Assert.True(File.Exists(Path.Combine(output, "combined.sqlite")));
        Assert.True(File.Exists(Path.Combine(output, "report.md")));
        Assert.True(File.Exists(Path.Combine(output, "webforms", "webforms-modernization.md")));
        Assert.True(File.Exists(Path.Combine(output, "sources", "web", "scan-receipt.json")));
        Assert.True(File.Exists(Path.Combine(output, "sources", "framework", "index.sqlite")));
        using var record = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "workspace-run.local.json")));
        var result = record.RootElement;
        Assert.Equal("partial", result.GetProperty("outcome").GetString());
        Assert.Equal(2, result.GetProperty("sources").GetArrayLength());
        Assert.Equal("local-only", result.GetProperty("visibility").GetString());
        Assert.Equal(Hash(File.ReadAllBytes(typeof(WorkspaceCommand).Assembly.Location)), result.GetProperty("generatorSha256").GetString());
        var expectedInputs = JsonSerializer.SerializeToUtf8Bytes(result.GetProperty("inputs"), new JsonSerializerOptions { WriteIndented = true });
        Assert.Equal(Hash(expectedInputs), result.GetProperty("boundedInputSha256").GetString());
        var guide = File.ReadAllText(Path.Combine(output, "START-HERE.md"));
        Assert.Contains("database definitions were not ingested", guide, StringComparison.Ordinal);
        Assert.Contains("Selected source repositories collected: 2", guide);
        Assert.Contains("## Which evidence to use", guide);
        Assert.Contains("Selected-root lifecycle analysis is separate", guide);
        Assert.Contains("does not configure lifecycle root selectors or import prior lifecycle outputs", guide);
    }

    [Fact]
    public async Task Stage_failure_retains_diagnostics_and_does_not_run_later_stages()
    {
        using var temp = new TempDirectory();
        var repo = Repository(temp.Path, "web");
        var root = Configure(temp.Path, Configuration(new { id = "web", path = repo, projects = Array.Empty<string>() }));
        var output = Path.Combine(temp.Path, "failed");
        var calls = 0;
        Task<int> Fail(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)
        {
            calls++;
            Assert.Equal("scan", args[0]);
            var scan = args[Array.IndexOf(args, "--out") + 1];
            Directory.CreateDirectory(scan);
            File.WriteAllText(Path.Combine(scan, "diagnostic.txt"), "preserved");
            return Task.FromResult(1);
        }
        Assert.Equal(1, await WorkspaceCommand.RunAsync(["collect", "--root", root, "--out", output], TextWriter.Null, TextWriter.Null, Fail));
        Assert.Equal(1, calls);
        Assert.Equal("preserved", File.ReadAllText(Path.Combine(output, "sources", "web", "diagnostic.txt")));
        using var record = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "workspace-run.local.json")));
        Assert.Equal("failed", record.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("scan:web", record.RootElement.GetProperty("stage").GetString());
        Assert.False(File.Exists(Path.Combine(output, "combined.sqlite")));
    }

    [Fact]
    public async Task Workspace_help_is_discoverable_and_unknown_options_are_refused()
    {
        using var text = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["workspace", "--help"], text, TextWriter.Null));
        Assert.Contains("workspace init", text.ToString(), StringComparison.Ordinal);
        using var temp = new TempDirectory();
        Assert.Equal(1, await Run("init", temp.Path, "--typo", "value"));
        Assert.False(File.Exists(Path.Combine(temp.Path, "workspace.local.json")));
    }

    [Theory]
    [InlineData("project", "settings.json")]
    [InlineData("project", "Application.sln")]
    [InlineData("solution", "Application.csproj")]
    [InlineData("solution", "settings.json")]
    public async Task Wrong_selection_file_kind_is_refused_before_scanning(string kind, string name)
    {
        using var temp = new TempDirectory();
        var repo = Repository(temp.Path, "web");
        File.WriteAllText(Path.Combine(repo, name), "not a selection of the declared kind");
        var root = Configure(temp.Path, Configuration(new
        {
            id = "web", path = repo, projects = kind == "project" ? new[] { name } : [],
            solution = kind == "solution" ? name : null
        }));
        using var error = new StringWriter();
        var calls = 0;
        Task<int> Never(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)
        { calls++; return Task.FromResult(0); }
        Assert.Equal(1, await WorkspaceCommand.RunAsync(["collect", "--root", root,
            "--out", Path.Combine(temp.Path, "run")], TextWriter.Null, error, Never));
        Assert.Contains("WORKSPACE_SELECTION_FILE_KIND", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, calls);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "run")));
    }

    [Theory]
    [InlineData("Application.csproj", "projects")]
    [InlineData("Application.vbproj", "projects")]
    [InlineData("Application.sln", "solution")]
    public async Task Verify_explains_explicit_selection_and_displays_exclusions(string name, string mode)
    {
        using var temp = new TempDirectory();
        var repo = Repository(temp.Path, "web");
        File.WriteAllText(Path.Combine(repo, name), "selection is not evaluated during verification");
        var root = Configure(temp.Path, Configuration(new
        {
            id = "web", path = repo, projects = mode == "projects" ? new[] { name } : [],
            solution = mode == "solution" ? name : null, exclude = new[] { "generated/**" }
        }, new { id = "framework", path = "../not-present", projects = Array.Empty<string>() }));
        var scope = Path.Combine(root, "scope.txt"); File.WriteAllText(scope, "web\n");
        using var output = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["workspace", "verify", "--root", root,
            "--scope", scope], output, TextWriter.Null));
        Assert.Contains("Scope web: mode=" + mode, output.ToString(), StringComparison.Ordinal);
        Assert.Contains(name, output.ToString(), StringComparison.Ordinal);
        Assert.Contains("generated/**", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Excluded repositories: [\"framework\"]", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("selection is not a coverage proof", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discovery_preview_does_not_claim_projectless_compilation_or_nested_inventory()
    {
        using var temp = new TempDirectory();
        var repo = Repository(temp.Path, "web");
        var root = Configure(temp.Path, Configuration(new { id = "web", path = repo, projects = Array.Empty<string>() }));
        using var output = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["workspace", "verify", "--root", root], output, TextWriter.Null));
        Assert.Contains("Scope web: mode=discovery", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("WORKSPACE_WEBSITE_WITHOUT_ROOT_PROJECT", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("root-only", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("mode=projectless", output.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(root, "sources")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Root_name_preview_is_bounded_and_never_discovers_nested_projects(bool overLimit)
    {
        using var temp = new TempDirectory();
        var repo = Repository(temp.Path, "web");
        Directory.CreateDirectory(Path.Combine(repo, "nested"));
        File.WriteAllText(Path.Combine(repo, "nested", "Hidden.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(repo, "First.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(repo, "Second.vbproj"), "<Project />");
        if (overLimit)
            for (var index = 0; index < 260; index++) File.WriteAllText(Path.Combine(repo, $"item-{index}.txt"), "");
        var root = Configure(temp.Path, Configuration(new { id = "web", path = repo, projects = Array.Empty<string>() }));
        using var output = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["workspace", "verify", "--root", root], output, TextWriter.Null));
        Assert.DoesNotContain("Hidden.csproj", output.ToString(), StringComparison.Ordinal);
        if (overLimit)
        {
            Assert.Contains("WORKSPACE_ROOT_PREVIEW_LIMIT", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("First.csproj", output.ToString(), StringComparison.Ordinal);
        }
        else Assert.Contains("WORKSPACE_DISCOVERY_SELECTION", output.ToString(), StringComparison.Ordinal);
    }

    private static Task<int> Run(string operation, string root, params string[] options) =>
        TraceMapCommand.RunAsync(["workspace", operation, "--root", root, .. options], TextWriter.Null, TextWriter.Null);

    private static string Configuration(params object[] repositories) => JsonSerializer.Serialize(new
    {
        schemaVersion = WorkspaceCommand.SchemaVersion, repositories, scopeFile = (string?)null, webFormsReport = true
    });

    private static string Configure(string parent, string json)
    {
        var root = Path.Combine(parent, "workspace");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "workspace.local.json"), json);
        return root;
    }

    private static string Repository(string parent, string name)
    {
        var root = Path.Combine(parent, name);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Sample.cs"), "namespace Example; public class Sample { public void Save() {} }");
        if (name == "web")
        {
            File.WriteAllText(Path.Combine(root, "Web.config"), "<configuration />");
            File.WriteAllText(Path.Combine(root, "ExamplePage.aspx"), "<%@ Page Language=\"C#\" CodeBehind=\"ExamplePage.aspx.cs\" Inherits=\"Example.ExamplePage\" %><asp:Button ID=\"Save\" runat=\"server\" OnClick=\"Save_Click\" />");
            File.WriteAllText(Path.Combine(root, "ExamplePage.aspx.cs"), "namespace Example; public class ExamplePage { protected void Save_Click(object sender, System.EventArgs e) {} }");
        }
        Git(root, "init");
        Git(root, "add", ".");
        Git(root, "-c", "user.name=TraceMap", "-c", "user.email=fixture@example.invalid", "commit", "-m", "synthetic fixture");
        return root;
    }

    private static void Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, stderr.GetAwaiter().GetResult());
        stdout.GetAwaiter().GetResult();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
