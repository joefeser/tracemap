using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WorkspaceCompiledTests
{
    [Fact]
    public async Task Verify_displays_native_authority_and_partial_page_scope_before_collection()
    {
        using var fixture = new Fixture();
        await fixture.Prepare();
        Assert.Equal(0, await fixture.Run("verify"));
        Assert.Contains("Scope web: mode=native-projectless", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("sourceFolders=", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("pageMode=selected", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("primaryAssemblies=", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("WORKSPACE_PAGE_SCOPE_IS_NOT_REPOSITORY_SCOPE", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("SOURCE repositories: 2 selected / 2 configured", fixture.Output.ToString());
        Assert.Contains("Website folder mode is an explicit selection", fixture.Output.ToString());
        Assert.Contains("does not configure lifecycle root selectors", fixture.Output.ToString());
        Assert.False(Directory.Exists(fixture.RunRoot));
    }

    [Fact]
    public async Task Prepared_native_fresh_scan_combines_with_framework_and_keeps_the_verified_bundle()
    {
        using var fixture = new Fixture();
        await fixture.Prepare();
        var original = fixture.Native.Hashes(fixture.Native.Evidence);
        var published = fixture.Native.Hashes(fixture.Native.Published);
        Assert.Equal(0, await fixture.Run("verify"));
        Assert.False(Directory.Exists(fixture.RunRoot));
        Assert.True(await fixture.Run("collect") == 0, fixture.Error.ToString());
        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture.RunRoot, "workspace-run.local.json")))!;
        Assert.Equal("tracemap-workspace-run.v2", record["schemaVersion"]!.GetValue<string>());
        Assert.Equal("partial", record["outcome"]!.GetValue<string>());
        Assert.Equal(2, record["sources"]!.AsArray().Count);
        var compiled = record["compiledReview"]!;
        Assert.Equal("web", compiled["repositoryId"]!.GetValue<string>());
        var raw = Path.Combine(fixture.RunRoot, compiled["rawScan"]!.GetValue<string>());
        var bundle = Path.Combine(fixture.RunRoot, compiled["reportBundle"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(raw, "index.sqlite")));
        Assert.True(File.Exists(Path.Combine(bundle, "index.html")));
        Assert.True(File.Exists(Path.Combine(bundle, "handoff.local.json")));
        Assert.True(File.Exists(Path.Combine(fixture.RunRoot, "webforms", "webforms-modernization.md")));
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(fixture.RunRoot, "combined.sqlite")}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from index_sources;";
        Assert.Equal(2L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        command.CommandText = "select count(*) from combined_facts where rule_id = $rule;";
        command.Parameters.AddWithValue("$rule", RuleIds.DotNetCompiledAssembly);
        Assert.True(Convert.ToInt64(await command.ExecuteScalarAsync()) > 0);
        foreach (var (path, hash) in original) Assert.Equal(hash, FileHash(path));
        foreach (var (path, hash) in published) Assert.Equal(hash, FileHash(path));
        var start = File.ReadAllText(Path.Combine(fixture.RunRoot, "START-HERE.md"));
        Assert.Contains("Native compiled workbench:", start, StringComparison.Ordinal);
        Assert.Contains("SQL was not ingested", start, StringComparison.Ordinal);
        Assert.DoesNotContain("compiled-and-sql-not-ingested", record.ToJsonString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("excluded-web")]
    [InlineData("wrong-repository")]
    [InlineData("source-scope")]
    [InlineData("wrong-commit")]
    [InlineData("dll-drift")]
    [InlineData("source-drift")]
    [InlineData("receipt-drift")]
    [InlineData("missing-map")]
    [InlineData("attach")]
    [InlineData("legacy-schema")]
    [InlineData("v1-with-compiled")]
    public async Task Incompatible_or_changed_inputs_fail_before_collection_creates_output(string mutation)
    {
        using var fixture = new Fixture();
        await fixture.Prepare();
        var workspace = JsonNode.Parse(File.ReadAllText(fixture.ConfigPath))!.AsObject();
        var native = JsonNode.Parse(File.ReadAllText(fixture.Native.PreparedConfig))!.AsObject();
        string[] extra = [];
        switch (mutation)
        {
            case "excluded-web":
                var scope = Path.Combine(fixture.Root, "framework-only.txt"); File.WriteAllText(scope, "framework\n");
                extra = ["--scope", scope]; break;
            case "wrong-repository": workspace["compiledReview"]!["repositoryId"] = "unknown"; break;
            case "source-scope": workspace["repositories"]![0]!["exclude"] = new JsonArray("**/*.vb"); break;
            case "v1-with-compiled": workspace["schemaVersion"] = WorkspaceCommand.SchemaVersion; break;
            case "wrong-commit": native["sourceCommitSha"] = new string('f', 40); break;
            case "dll-drift": File.AppendAllText(fixture.Native.Primary, "changed"); break;
            case "source-drift": File.AppendAllText(Path.Combine(fixture.Native.Source, "Pages/Lookup.aspx.vb"), "' changed\n"); break;
            case "receipt-drift": File.WriteAllText(Path.Combine(fixture.Native.Evidence, "compiled-binding.local.json"), "{}"); break;
            case "missing-map": File.Delete(fixture.Native.Map); break;
            case "attach": native["operation"] = "attach"; break;
            case "legacy-schema": native["schemaVersion"] = "webforms-compile-web-config.v1"; break;
        }
        File.WriteAllText(fixture.ConfigPath, workspace.ToJsonString());
        File.WriteAllText(fixture.Native.PreparedConfig, native.ToJsonString());
        Assert.Equal(1, await fixture.Run("collect", extra));
        Assert.False(Directory.Exists(fixture.RunRoot));
    }

    [Fact]
    public async Task Unprepared_config_never_mints_an_implicit_attestation()
    {
        using var fixture = new Fixture();
        fixture.Native.Save();
        fixture.WriteConfiguration(fixture.Native.ConfigPath);
        Assert.Equal(1, await fixture.Run("collect"));
        Assert.Contains("WORKSPACE_COMPILED_PREPARED_CONFIG_REQUIRED", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.RunRoot));
        Assert.False(Directory.Exists(fixture.Native.Evidence));
    }

    [Fact]
    public async Task Failed_native_stage_stops_before_framework_scan_or_combine_and_preserves_failure()
    {
        using var fixture = new Fixture();
        await fixture.Prepare();
        var calls = 0;
        Task<int> Fail(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)
        {
            calls++;
            Assert.Equal("webforms-review", args[0]);
            Assert.DoesNotContain("--attest-exact-source-commit", args);
            return Task.FromResult(1);
        }
        Assert.Equal(1, await WorkspaceCommand.RunAsync(["collect", "--root", fixture.Workspace, "--out", fixture.RunRoot],
            fixture.Output, fixture.Error, Fail));
        Assert.Equal(1, calls);
        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture.RunRoot, "workspace-run.local.json")))!;
        Assert.Equal("failed", record["outcome"]!.GetValue<string>());
        Assert.Equal("native:web", record["stage"]!.GetValue<string>());
        Assert.Equal("tracemap-workspace-run.v2", record["schemaVersion"]!.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(fixture.RunRoot, "combined.sqlite")));
    }

    [Theory]
    [InlineData("native-index")]
    [InlineData("native-checkpoint")]
    [InlineData("publication")]
    [InlineData("framework-after-combine")]
    [InlineData("combined-after-report")]
    public async Task Changed_native_or_combined_inputs_never_publish_a_completed_start_page(string mutation)
    {
        using var fixture = new Fixture();
        await fixture.Prepare();
        async Task<int> Mutate(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)
        {
            var code = await TraceMapCommand.RunAsync(args, stdout, stderr, token);
            if (code != 0) return code;
            if (args[0] == "webforms-review" && mutation is "native-index" or "native-checkpoint" or "publication")
            {
                var run = Path.Combine(fixture.RunRoot, "native-review", "run");
                var snapshot = await WebFormsReviewExecutionCommand.ReadWorkspaceSnapshotAsync(run, token);
                if (mutation == "native-index") File.AppendAllText(Path.Combine(snapshot.ScanRoot, "index.sqlite"), "changed");
                if (mutation == "publication") File.AppendAllText(fixture.Native.Primary, "changed");
                if (mutation == "native-checkpoint") File.AppendAllText(Path.Combine(run, "checkpoints", "0001.json"), " ");
            }
            if (args[0] == "combine" && mutation == "framework-after-combine")
                File.AppendAllText(Path.Combine(fixture.RunRoot, "sources", "framework", "index.sqlite"), "changed");
            if (args[0] == "report" && mutation == "combined-after-report")
                File.AppendAllText(Path.Combine(fixture.RunRoot, "combined.sqlite"), "changed");
            return code;
        }
        Assert.Equal(1, await WorkspaceCommand.RunAsync(["collect", "--root", fixture.Workspace, "--out", fixture.RunRoot],
            fixture.Output, fixture.Error, Mutate));
        Assert.False(File.Exists(Path.Combine(fixture.RunRoot, "START-HERE.md")));
        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture.RunRoot, "workspace-run.local.json")))!;
        Assert.Equal("failed", record["outcome"]!.GetValue<string>());
        Assert.NotEqual("completed", record["stage"]!.GetValue<string>());
    }

    private static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed class Fixture : IDisposable
    {
        public WebFormsReviewPreparationTests.Fixture Native { get; } = new();
        public string Root => Native.Root;
        public string Workspace => Path.Combine(Root, "workspace");
        public string Framework => Path.Combine(Root, "framework");
        public string RunRoot => Path.Combine(Root, "collection");
        public string ConfigPath => Path.Combine(Workspace, "workspace.local.json");
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Fixture()
        {
            Directory.CreateDirectory(Workspace);
            Directory.CreateDirectory(Framework);
            File.WriteAllText(Path.Combine(Framework, "Framework.cs"), "namespace Example; public class Framework { public void Save() {} }");
            Git("init", "-q"); Git("add", ".");
            Git("-c", "user.name=Public Fixture", "-c", "user.email=fixture@example.invalid", "-c", "commit.gpgsign=false", "commit", "-qm", "public framework source");
        }
        public async Task Prepare()
        {
            Assert.True(await Native.Prepare() == 0, Native.Error.ToString());
            WriteConfiguration(Native.PreparedConfig);
        }
        public void WriteConfiguration(string nativeConfig) => File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new
        {
            schemaVersion = WorkspaceCommand.CompiledSchemaVersion,
            repositories = new[] { new { id = "web", path = Native.Source, projects = Array.Empty<string>() },
                new { id = "framework", path = Framework, projects = Array.Empty<string>() } },
            webFormsReport = true, compiledReview = new { repositoryId = "web", nativeConfigPath = nativeConfig }
        }));
        public Task<int> Run(string operation, params string[] extra) => TraceMapCommand.RunAsync(
            ["workspace", operation, "--root", Workspace, .. operation == "collect" ? new[] { "--out", RunRoot } : [], .. extra], Output, Error);
        private void Git(params string[] args)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = Framework, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit(); Task.WaitAll(stdout, stderr);
            Assert.True(process.ExitCode == 0, stderr.Result);
        }
        public void Dispose() { Output.Dispose(); Error.Dispose(); SqliteConnection.ClearAllPools(); Native.Dispose(); }
    }
}
