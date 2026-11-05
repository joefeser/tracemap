using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WorkspaceFromWizardTests
{
    [Theory]
    [InlineData("Framework.csproj", "projects")]
    [InlineData("Framework.sln", "solution")]
    [InlineData("discovery", "discovery")]
    public async Task Saved_wizard_receipts_and_loose_sql_generate_verified_v3_without_scanning(string target, string mode)
    {
        using var fixture = new Fixture();
        await fixture.Prepare();
        var wizard = File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "root.config.json"));
        var prepared = File.ReadAllBytes(fixture.Prepared);
        var sql = File.ReadAllBytes(fixture.Manifest);
        var calls = 0;
        Task<int> Never(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)
        { calls++; throw new InvalidOperationException("initialization must not scan"); }
        var arguments = fixture.Arguments(target);
        if (target == "discovery") arguments = WithoutWebsiteId(arguments);
        var exit = target == "discovery"
            ? await TraceMapCommand.RunAsync(["workspace", .. arguments], fixture.Output, fixture.Error)
            : await WorkspaceCommand.RunAsync(arguments, fixture.Output, fixture.Error, Never);
        Assert.True(exit == 0, fixture.Error.ToString());
        Assert.Equal(0, calls);
        using var config = JsonDocument.Parse(File.ReadAllText(fixture.Destination));
        var root = config.RootElement;
        Assert.Equal("tracemap-workspace.v3", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(fixture.Site, root.GetProperty("repositories")[0].GetProperty("path").GetString());
        Assert.Equal(WebFormsWizardStore.Physical(fixture.Prepared), root.GetProperty("compiledReview").GetProperty("nativeConfigPath").GetString());
        Assert.Equal(WebFormsWizardStore.Physical(fixture.Manifest), root.GetProperty("sqlEvidence").GetProperty("manifestPath").GetString());
        Assert.Equal(WebFormsWizardStore.Physical(fixture.SqlRoot), root.GetProperty("sqlEvidence").GetProperty("sourceRoot").GetString());
        Assert.Matches("^[0-9a-f]{64}$", root.GetProperty("templateProvenance").GetProperty("boundedInputSha256").GetString()!);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(WorkspaceCommand).Assembly.Location))),
            root.GetProperty("templateProvenance").GetProperty("generatorSha256").GetString());
        Assert.Contains("Scope framework: mode=" + mode, fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Equal(wizard, File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "root.config.json")));
        Assert.Equal(prepared, File.ReadAllBytes(fixture.Prepared));
        Assert.Equal(sql, File.ReadAllBytes(fixture.Manifest));
        Assert.Equal(new[] { "workspace.local.json" }, Directory.GetFiles(fixture.Workspace).Select(Path.GetFileName));
        Assert.Equal(0, await TraceMapCommand.RunAsync(["workspace", "verify", "--root", fixture.Workspace], fixture.Output, fixture.Error));
    }

    [Theory]
    [InlineData("missing-receipts")]
    [InlineData("prepared-scope")]
    [InlineData("sql-hash")]
    [InlineData("sql-context")]
    [InlineData("framework-missing")]
    [InlineData("target-kind")]
    [InlineData("missing-option")]
    [InlineData("inside-source")]
    [InlineData("ambiguous-site")]
    public async Task Invalid_imports_do_not_publish_a_workspace_or_rewrite_inputs(string mutation)
    {
        using var fixture = new Fixture();
        await fixture.Prepare();
        var args = fixture.Arguments("Framework.csproj");
        switch (mutation)
        {
            case "missing-receipts": File.Delete(fixture.Prepared); break;
            case "prepared-scope":
                var config = JsonNode.Parse(File.ReadAllText(fixture.Prepared))!;
                config["pageMode"] = "selected";
                config["pageRelativePaths"] = new JsonArray("Default.aspx");
                File.WriteAllText(fixture.Prepared, config.ToJsonString()); break;
            case "sql-hash": File.AppendAllText(Path.Combine(fixture.SqlRoot, "schema.sql"), "-- drift"); break;
            case "sql-context":
                var sql = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
                sql["contextCommitSha"] = new string('f', 40);
                File.WriteAllText(fixture.Manifest, sql.ToJsonString()); break;
            case "framework-missing": args[Array.IndexOf(args, "--framework-repo") + 1] += "-absent"; break;
            case "target-kind": args[Array.IndexOf(args, "--framework-target") + 1] = "README.md"; break;
            case "missing-option": args = args[..^2]; break;
            case "inside-source": args[Array.IndexOf(args, "--root") + 1] = Path.Combine(fixture.Site, "private-workspace"); break;
            case "ambiguous-site":
                using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
                    store.SaveProject(new("other", fixture.Site, fixture.Site, "projectless", "all", ["Default.aspx"], null, [], [], "publication"));
                args = WithoutWebsiteId(args); break;
        }
        var code = await WorkspaceCommand.RunAsync(args, fixture.Output, fixture.Error,
            (_, _, _, _) => throw new InvalidOperationException("invalid setup must not execute"));
        Assert.Equal(1, code);
        var expected = mutation switch
        {
            "missing-receipts" => "WORKSPACE_WIZARD_PREPARED_CONFIG_MISSING",
            "prepared-scope" => "WORKSPACE_WIZARD_PREPARED_SCOPE_MISMATCH",
            "sql-hash" => "sql-local-source-digest-mismatch",
            "sql-context" => "WORKSPACE_SQL_APPLICATION_CONTEXT_MISMATCH",
            "framework-missing" => "WORKSPACE_REPOSITORY_MISSING",
            "target-kind" => "WORKSPACE_SELECTION_FILE_KIND",
            "missing-option" => "WORKSPACE_SETUP_OPTION_REQUIRED",
            "inside-source" => "WORKSPACE_SETUP_INSIDE_GIT_REPOSITORY",
            "ambiguous-site" => "WORKSPACE_WIZARD_WEBSITE_ID_REQUIRED",
            _ => throw new InvalidOperationException("unknown mutation")
        };
        Assert.Contains(expected, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(args[Array.IndexOf(args, "--root") + 1], "workspace.local.json")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Workspace, "runs")));
    }

    [Fact]
    public async Task Existing_workspace_including_an_empty_template_is_never_overwritten()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.Workspace);
        File.WriteAllText(fixture.Destination, "keep existing configuration");
        Assert.Equal(1, await WorkspaceCommand.RunAsync(fixture.Arguments("discovery"), fixture.Output, fixture.Error));
        Assert.Equal("keep existing configuration", File.ReadAllText(fixture.Destination));
        Assert.Contains("WORKSPACE_CONFIG_EXISTS", fixture.Error.ToString(), StringComparison.Ordinal);
    }

    private static string[] WithoutWebsiteId(string[] arguments)
    {
        var index = Array.IndexOf(arguments, "--website-id");
        return [.. arguments[..index], .. arguments[(index + 2)..]];
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly TempDirectory temp = new();
        public string Site { get; }
        public string WizardRoot { get; }
        public string Framework { get; }
        public string Workspace { get; }
        public string Destination => Path.Combine(Workspace, "workspace.local.json");
        public string Manifest { get; }
        public string SqlRoot { get; }
        public string Prepared { get; private set; } = "";
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        private string Published { get; }
        public Fixture()
        {
            (Site, Published) = WebFormsWizardExecutionTests.Inputs(temp.Path);
            Site = WebFormsWizardStore.Physical(Site);
            Published = WebFormsWizardStore.Physical(Published);
            WizardRoot = Path.Combine(temp.Path, "wizard");
            Framework = Path.Combine(temp.Path, "framework"); Directory.CreateDirectory(Framework);
            File.WriteAllText(Path.Combine(Framework, "Framework.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(Framework, "Framework.sln"), "public selection fixture; verification does not load MSBuild");
            WebFormsWizardNativeTests.InitGit(Framework);
            Workspace = Path.Combine(temp.Path, "workspace");
            SqlRoot = Path.Combine(temp.Path, "loose-sql"); Directory.CreateDirectory(SqlRoot);
            const string sql = "CREATE PROC dbo.ReadItems AS SELECT 1 AS Id;";
            File.WriteAllText(Path.Combine(SqlRoot, "schema.sql"), sql);
            Manifest = Path.Combine(temp.Path, "sql.local.json");
            File.WriteAllText(Manifest, SqlServerLocalDefinitionsTests.Input(new Dictionary<string, string> { ["schema.sql"] = sql },
                GitMetadataProvider.Detect(Site).CommitSha));
        }
        public async Task Prepare()
        {
            using var store = WebFormsWizardStore.Open(WizardRoot, false);
            store.SaveProject(new("site", Site, Site, "projectless", "all", ["Default.aspx"], null, [], [], "publication"));
            WebFormsWizardPublication.Configure(store, "site", Published, ["bin/CompiledEvidence.CSharp.dll"], []);
            store.SaveProject(store.ReadProject("site") with { Step = "configuration" });
            var native = await WebFormsWizardNative.ConfigureAsync(store, "site");
            var project = store.ReadProject("site");
            var run = new WebFormsWizardRunReference("runs/site-" + new string('a', 32), project.Native!.SourceCommitSha, project.Native.Sha256);
            var evidence = Path.Combine(WizardRoot, run.RelativeRoot, "evidence");
            Assert.True(await TraceMapCommand.RunAsync(["webforms-review", "prepare", "--config", native,
                "--out", evidence, "--attest-exact-source-commit", run.AttestedCommitSha], Output, Error) == 0, Error.ToString());
            store.SaveProject(project with { Step = "failed", Run = run }); // Prepared receipts suffice; no scan/results fabricated.
            Prepared = Path.Combine(evidence, "review-config.local.json");
        }
        public string[] Arguments(string target) => ["init-from-wizard", "--root", Workspace,
            "--wizard-root", WizardRoot, "--website-id", "site", "--framework-repo", Framework,
            "--framework-target", target, "--sql-manifest", Manifest, "--sql-source-root", SqlRoot];
        public void Dispose() { Output.Dispose(); Error.Dispose(); temp.Dispose(); }
    }
}
