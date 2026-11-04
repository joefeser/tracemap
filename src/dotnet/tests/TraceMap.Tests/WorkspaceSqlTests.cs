using System.Diagnostics;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WorkspaceSqlTests
{
    [Fact]
    public async Task Sql_is_a_separate_local_sidecar_and_only_raw_scan_indexes_are_combined()
    {
        using var fixture = new Fixture();
        var commands = new List<string[]>();
        async Task<int> Execute(string[] args, TextWriter output, TextWriter error, CancellationToken token)
        {
            commands.Add(args);
            return await TraceMapCommand.RunAsync(args, output, error, token);
        }
        Assert.Equal(0, await fixture.Collect(Execute));
        var combine = Assert.Single(commands, args => args[0] == "combine");
        Assert.DoesNotContain(combine, value => value.Contains("sql-evidence", StringComparison.Ordinal));
        Assert.Equal(1, combine.Count(value => value == "--index"));
        using var record = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Run, "workspace-run.local.json")));
        Assert.Equal("tracemap-workspace-run.v3", record.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("partial", record.RootElement.GetProperty("outcome").GetString());
        var sql = record.RootElement.GetProperty("sqlEvidence");
        Assert.Equal("not-established", sql.GetProperty("callBinding").GetString());
        Assert.Equal("not-established", sql.GetProperty("databaseRevision").GetString());
        Assert.True(File.Exists(Path.Combine(fixture.Run, sql.GetProperty("index").GetString()!)));
        Assert.Contains("SQL is an offline local sidecar", File.ReadAllText(Path.Combine(fixture.Run, "START-HERE.md")), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(fixture.Run, "sql", "scan-manifest.json")));
    }

    [Theory]
    [InlineData("context")]
    [InlineData("scope")]
    [InlineData("digest")]
    [InlineData("v1")]
    [InlineData("overlap")]
    public async Task Invalid_sql_selection_refuses_before_creating_output(string kind)
    {
        using var fixture = new Fixture();
        var extra = Array.Empty<string>();
        switch (kind)
        {
            case "context": File.WriteAllText(fixture.Manifest, File.ReadAllText(fixture.Manifest).Replace(fixture.Head, new string('1', 40), StringComparison.Ordinal)); break;
            case "scope":
                var scope = Path.Combine(fixture.Root, "scope.txt"); File.WriteAllText(scope, "framework\n");
                extra = ["--scope", scope]; fixture.Configure(includeMissingFramework: true); break;
            case "digest": File.AppendAllText(fixture.SqlFile, "\n-- drift"); break;
            case "v1": fixture.Configure(version: WorkspaceCommand.SchemaVersion); break;
            case "overlap": fixture.Run = Path.Combine(fixture.SqlRoot, "output"); break;
        }
        Assert.Equal(1, await WorkspaceCommand.RunAsync(["collect", "--root", fixture.Root, "--out", fixture.Run, .. extra], TextWriter.Null, TextWriter.Null));
        Assert.False(Directory.Exists(fixture.Run));
    }

    [Theory]
    [InlineData("input")]
    [InlineData("index")]
    [InlineData("raw-index")]
    public async Task Drift_after_sql_publication_preserves_failure_without_completed_start_page(string change)
    {
        using var fixture = new Fixture();
        async Task<int> Execute(string[] args, TextWriter output, TextWriter error, CancellationToken token)
        {
            var result = await TraceMapCommand.RunAsync(args, output, error, token);
            if (args[0] == "report")
            {
                var path = change switch
                {
                    "input" => fixture.SqlFile,
                    "index" => Path.Combine(fixture.Run, "sql", "sql-evidence.sqlite"),
                    _ => Path.Combine(fixture.Run, "sources", "web", "index.sqlite")
                };
                File.AppendAllText(path, "\n-- drift");
            }
            return result;
        }
        Assert.Equal(1, await fixture.Collect(Execute));
        using var record = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Run, "workspace-run.local.json")));
        Assert.Equal("failed", record.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("completion-validation", record.RootElement.GetProperty("stage").GetString());
        Assert.False(File.Exists(Path.Combine(fixture.Run, "START-HERE.md")));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string repo;
        public string Root { get; }
        public string Run { get; set; }
        public string SqlRoot { get; }
        public string SqlFile { get; }
        public string Manifest { get; }
        public string Head { get; }
        public Fixture()
        {
            Root = Path.Combine(temp.Path, "workspace"); Directory.CreateDirectory(Root);
            repo = Path.Combine(temp.Path, "web"); Directory.CreateDirectory(repo);
            File.WriteAllText(Path.Combine(repo, "Sample.cs"), "namespace Example; public class Sample { public int Read() => 1; }");
            Git("init", "--quiet"); Git("add", ".");
            Git("-c", "user.name=Synthetic Fixture", "-c", "user.email=fixture@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "synthetic");
            Head = Git("rev-parse", "HEAD");
            Run = Path.Combine(temp.Path, "run");
            SqlRoot = Path.Combine(temp.Path, "definitions"); Directory.CreateDirectory(SqlRoot);
            SqlFile = Path.Combine(SqlRoot, "schema.sql");
            const string sql = "CREATE TABLE dbo.Items(Id int NOT NULL);\nGO\nCREATE PROC dbo.ReadItems AS SELECT Id FROM dbo.Items;";
            File.WriteAllText(SqlFile, sql);
            Manifest = Path.Combine(Root, "sql.local.json");
            File.WriteAllText(Manifest, SqlServerLocalDefinitionsTests.Input(new Dictionary<string, string> { ["schema.sql"] = sql }, Head));
            Configure();
        }
        public void Configure(string version = WorkspaceCommand.SqlSchemaVersion, bool includeMissingFramework = false)
        {
            var repositories = new List<object> { new { id = "web", path = repo, projects = Array.Empty<string>() } };
            if (includeMissingFramework) repositories.Add(new { id = "framework", path = repo, projects = Array.Empty<string>() });
            File.WriteAllText(Path.Combine(Root, "workspace.local.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = version, repositories, webFormsReport = false,
                sqlEvidence = new { repositoryId = "web", manifestPath = Manifest, sourceRoot = SqlRoot }
            }));
        }
        public Task<int> Collect(LocalReviewScanRunner execute) => WorkspaceCommand.RunAsync(
            ["collect", "--root", Root, "--out", Run], TextWriter.Null, TextWriter.Null, execute);
        private string Git(params string[] args)
        {
            using var process = new Process { StartInfo = new("git") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true } };
            foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
            process.Start(); var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit();
            Assert.True(process.ExitCode == 0, error); return output.Trim();
        }
        public void Dispose() => temp.Dispose();
    }
}
