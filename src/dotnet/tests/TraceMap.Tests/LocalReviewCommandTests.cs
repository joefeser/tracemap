using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class LocalReviewCommandTests
{
    [Fact]
    public async Task Guided_scan_emits_structured_result_with_relative_artifacts()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        var review = Path.Combine(temp.Path, "review");
        Directory.CreateDirectory(review);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await TraceMapCommand.RunAsync(
            ["local-review", "run", "--repo", repo, "--out", review],
            output,
            error);

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, error.ToString());
        Assert.True(File.Exists(Path.Combine(review, "local-review-result.json")));
        Assert.True(File.Exists(Path.Combine(review, "README.md")));
        Assert.True(File.Exists(Path.Combine(review, "scan", "scan-manifest.json")));
        Assert.True(File.Exists(Path.Combine(review, "scan", "scan-receipt.json")));

        var json = await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json"));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("local-review-result.v1", root.GetProperty("schemaVersion").GetString());
        Assert.StartsWith("workflow-", root.GetProperty("workflowId").GetString());
        Assert.Equal("local-only", root.GetProperty("claimLevel").GetString());
        Assert.Matches("^[0-9a-f]{7,64}$", root.GetProperty("commitSha").GetString());
        Assert.Matches("^[0-9a-f]{64}$", root.GetProperty("sourceSnapshotDigest").GetString());
        Assert.Equal("scan-artifacts-verified", root.GetProperty("lastProvenSafeState").GetString());
        Assert.All(root.GetProperty("artifacts").EnumerateArray(), artifact =>
        {
            var relative = artifact.GetProperty("relativePath").GetString()!;
            Assert.False(Path.IsPathRooted(relative));
            Assert.DoesNotContain('\\', relative);
            Assert.Contains(
                artifact.GetProperty("producerStage").GetString(),
                new[] { "scan", "webforms-modernization", "explorer" });
            Assert.DoesNotContain(relative, new[] { "local-review-result.json", "README.md" });
        });
        Assert.DoesNotContain(temp.Path, json, StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, await File.ReadAllTextAsync(Path.Combine(review, "README.md")), StringComparison.Ordinal);
        Assert.Contains("Output: ", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Guided_scan_can_compose_webforms_packet_and_ordinary_explorer()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: true);
        var review = Path.Combine(temp.Path, "review");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await TraceMapCommand.RunAsync(
            [
                "local-review", "run", "--repo", repo, "--out", review,
                "--webforms-modernization", "--explorer"
            ],
            output,
            error);

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, error.ToString());
        Assert.True(File.Exists(Path.Combine(review, "webforms", "webforms-modernization.json")));
        Assert.True(File.Exists(Path.Combine(review, "webforms", "webforms-modernization.md")));
        Assert.True(File.Exists(Path.Combine(review, "explorer", "index.html")));

        var standaloneWebForms = Path.Combine(temp.Path, "standalone-webforms");
        var standaloneExplorer = Path.Combine(temp.Path, "standalone-explorer");
        Assert.Equal(0, await TraceMapCommand.RunAsync(
            [
                "webforms-modernization",
                "--index", Path.Combine(review, "scan", "index.sqlite"),
                "--out", standaloneWebForms
            ],
            TextWriter.Null,
            TextWriter.Null));
        Assert.Equal(0, await TraceMapCommand.RunAsync(
            [
                "explorer", "generate",
                "--input", standaloneWebForms,
                "--out", standaloneExplorer,
                "--safety-profile", "hidden-local"
            ],
            TextWriter.Null,
            TextWriter.Null));
        Assert.Equal(
            await File.ReadAllBytesAsync(Path.Combine(standaloneWebForms, "webforms-modernization.json")),
            await File.ReadAllBytesAsync(Path.Combine(review, "webforms", "webforms-modernization.json")));
        Assert.Equal(
            await File.ReadAllBytesAsync(Path.Combine(standaloneWebForms, "webforms-modernization.md")),
            await File.ReadAllBytesAsync(Path.Combine(review, "webforms", "webforms-modernization.md")));
        Assert.Equal(
            Directory.EnumerateFiles(standaloneExplorer, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(standaloneExplorer, path))
                .OrderBy(path => path, StringComparer.Ordinal),
            Directory.EnumerateFiles(Path.Combine(review, "explorer"), "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Path.Combine(review, "explorer"), path))
                .OrderBy(path => path, StringComparer.Ordinal));
        foreach (var relative in Directory.EnumerateFiles(standaloneExplorer, "*", SearchOption.AllDirectories)
                     .Select(path => Path.GetRelativePath(standaloneExplorer, path)))
        {
            Assert.Equal(
                await File.ReadAllBytesAsync(Path.Combine(standaloneExplorer, relative)),
                await File.ReadAllBytesAsync(Path.Combine(review, "explorer", relative)));
        }

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        var stages = document.RootElement.GetProperty("stages").EnumerateArray()
            .Select(stage => stage.GetProperty("stage").GetString())
            .ToArray();
        Assert.Equal(new[] { "scan", "webforms-modernization", "explorer" }, stages);
        Assert.Equal("partial", document.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("reduced", document.RootElement.GetProperty("coverage").GetString());
        Assert.Equal(
            "partial",
            document.RootElement.GetProperty("stages")[1].GetProperty("outcome").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("gaps").EnumerateArray(),
            gap => gap.GetString() == "LOCAL_REVIEW_WEBFORMS_PARTIAL");
        Assert.Equal("explorer-verified", document.RootElement.GetProperty("lastProvenSafeState").GetString());
        Assert.All(
            document.RootElement.GetProperty("stages")[1].GetProperty("inputs").EnumerateArray(),
            input => Assert.StartsWith("scan/", input.GetProperty("relativePath").GetString(), StringComparison.Ordinal));
        Assert.All(
            document.RootElement.GetProperty("stages")[2].GetProperty("inputs").EnumerateArray(),
            input => Assert.StartsWith("webforms/", input.GetProperty("relativePath").GetString(), StringComparison.Ordinal));

        using var explorerDocument = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(review, "explorer", "data", "explorer-data.json")));
        Assert.StartsWith(
            "packet-",
            explorerDocument.RootElement.GetProperty("webForms").GetProperty("summary").GetProperty("packetId").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Guided_scan_rejects_restore_unknown_options_and_unsafe_outputs_before_scan()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(1, await TraceMapCommand.RunAsync(
            ["local-review", "run", "--repo", repo, "--out", Path.Combine(temp.Path, "restore"), "--restore"],
            output,
            error));
        Assert.Contains("LOCAL_REVIEW_ARGUMENT_INVALID", error.ToString(), StringComparison.Ordinal);

        error.GetStringBuilder().Clear();
        Assert.Equal(1, await TraceMapCommand.RunAsync(
            ["local-review", "run", "--repo", repo, "--out", Path.Combine(repo, "review")],
            output,
            error));
        Assert.Contains("LOCAL_REVIEW_OUTPUT_UNSAFE", error.ToString(), StringComparison.Ordinal);

        var occupied = Path.Combine(temp.Path, "occupied");
        Directory.CreateDirectory(occupied);
        await File.WriteAllTextAsync(Path.Combine(occupied, "owner.txt"), "preserve");
        error.GetStringBuilder().Clear();
        Assert.Equal(1, await TraceMapCommand.RunAsync(
            ["local-review", "run", "--repo", repo, "--out", occupied],
            output,
            error));
        Assert.Contains("LOCAL_REVIEW_OUTPUT_COLLISION", error.ToString(), StringComparison.Ordinal);
        Assert.Equal("preserve", await File.ReadAllTextAsync(Path.Combine(occupied, "owner.txt")));
    }

    [Fact]
    public async Task Guided_scan_resolves_and_authorizes_a_symlink_parent_target()
    {
        if (OperatingSystem.IsWindows()) return;

        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        var realParent = Path.Combine(temp.Path, "real-parent");
        var linkedParent = Path.Combine(temp.Path, "linked-parent");
        Directory.CreateDirectory(realParent);
        Directory.CreateSymbolicLink(linkedParent, realParent);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await TraceMapCommand.RunAsync(
            ["local-review", "run", "--repo", repo, "--out", Path.Combine(linkedParent, "review")],
            output,
            error);

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, error.ToString());
        Assert.True(File.Exists(Path.Combine(realParent, "review", "local-review-result.json")));
    }

    [Fact]
    public async Task Guided_scan_rejects_a_dangling_symlink_output_before_scan()
    {
        if (OperatingSystem.IsWindows()) return;

        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        var outputPath = Path.Combine(temp.Path, "dangling-output");
        File.CreateSymbolicLink(outputPath, Path.Combine(temp.Path, "missing-target"));
        var scanCalled = false;
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", outputPath],
            output,
            error,
            (arguments, stdout, stderr, cancellationToken) =>
            {
                scanCalled = true;
                return Task.FromResult(0);
            });

        Assert.Equal(1, exit);
        Assert.False(scanCalled);
        Assert.Contains("LOCAL_REVIEW_OUTPUT_COLLISION", error.ToString(), StringComparison.Ordinal);
        Assert.NotNull(File.ResolveLinkTarget(outputPath, returnFinalTarget: false));
    }

    [Fact]
    public async Task Guided_scan_publishes_typed_failure_result_without_raw_diagnostics()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        var review = Path.Combine(temp.Path, "failed-review");
        using var output = new StringWriter();
        using var error = new StringWriter();

        static Task<int> FailedScan(
            string[] args,
            TextWriter stdout,
            TextWriter stderr,
            CancellationToken cancellationToken) => Task.FromResult(1);

        var exit = await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review],
            output,
            error,
            FailedScan);

        Assert.Equal(1, exit);
        Assert.Contains("LOCAL_REVIEW_SCAN_FAILED", error.ToString(), StringComparison.Ordinal);
        var json = await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json"));
        Assert.Contains("\"outcome\": \"failed\"", json, StringComparison.Ordinal);
        Assert.Contains("LOCAL_REVIEW_SCAN_FAILED", json, StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Identity_unavailable_failures_are_location_independent_and_do_not_fabricate_scan_identity()
    {
        using var firstTemp = new TempDirectory();
        using var secondTemp = new TempDirectory();
        var firstRepo = CreateRepository(firstTemp.Path, webForms: false);
        var secondRepo = CreateRepository(secondTemp.Path, webForms: false);
        var firstReview = Path.Combine(firstTemp.Path, "review");
        var secondReview = Path.Combine(secondTemp.Path, "review");

        static Task<int> FailedScan(
            string[] args,
            TextWriter stdout,
            TextWriter stderr,
            CancellationToken cancellationToken) => Task.FromResult(1);

        Assert.Equal(1, await LocalReviewCommand.RunAsync(
            ["run", "--repo", firstRepo, "--out", firstReview],
            TextWriter.Null,
            TextWriter.Null,
            FailedScan));
        Assert.Equal(1, await LocalReviewCommand.RunAsync(
            ["run", "--repo", secondRepo, "--out", secondReview],
            TextWriter.Null,
            TextWriter.Null,
            FailedScan));

        using var first = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(firstReview, "local-review-result.json")));
        using var second = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(secondReview, "local-review-result.json")));
        Assert.Equal(first.RootElement.GetProperty("workflowId").GetString(), second.RootElement.GetProperty("workflowId").GetString());
        Assert.False(first.RootElement.TryGetProperty("repositoryIdentityHash", out _));
        Assert.False(first.RootElement.TryGetProperty("commitSha", out _));
    }

    [Fact]
    public async Task Syntax_only_scan_is_partial_with_explicit_reduced_coverage()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "syntax-only");
        Directory.CreateDirectory(repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "Fixture.cs"), "internal sealed class Fixture { }");
        RunGit(repo, "init");
        RunGit(repo, "add", ".");
        RunGit(repo, "-c", "user.name=TraceMap", "-c", "user.email=fixture@example.invalid", "commit", "-m", "baseline");
        var review = Path.Combine(temp.Path, "review");

        Assert.Equal(0, await TraceMapCommand.RunAsync(
            ["local-review", "run", "--repo", repo, "--out", review],
            TextWriter.Null,
            TextWriter.Null));

        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        Assert.Equal("partial", result.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("reduced", result.RootElement.GetProperty("coverage").GetString());
        Assert.Contains(
            result.RootElement.GetProperty("gaps").EnumerateArray(),
            gap => gap.GetString() == "LOCAL_REVIEW_SCAN_PARTIAL");
    }

    [Fact]
    public async Task Downstream_failure_preserves_verified_scan_and_records_failed_stage()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        var review = Path.Combine(temp.Path, "downstream-failure");
        using var output = new StringWriter();
        using var error = new StringWriter();

        static async Task<int> SyntheticScan(
            string[] args,
            TextWriter stdout,
            TextWriter stderr,
            CancellationToken cancellationToken)
        {
            var outputPath = args[Array.IndexOf(args, "--out") + 1];
            Directory.CreateDirectory(Path.Combine(outputPath, "logs"));
            var manifest = new ScanManifest(
                "scan-synthetic",
                "synthetic",
                null,
                null,
                new string('a', 40),
                "test-scanner",
                DateTimeOffset.UnixEpoch,
                "Level1SemanticAnalysis",
                "Succeeded",
                [],
                [],
                ["net10.0"],
                [],
                null,
                null,
                new string('b', 64),
                new string('c', 64));
            await File.WriteAllTextAsync(
                Path.Combine(outputPath, "scan-manifest.json"),
                JsonSerializer.Serialize(manifest),
                cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(outputPath, "facts.ndjson"), string.Empty, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(outputPath, "index.sqlite"), string.Empty, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(outputPath, "report.md"), "report", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(outputPath, "logs", "analyzer.log"), string.Empty, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(outputPath, "scan-receipt.json"), "{}", cancellationToken);
            return 0;
        }

        var exit = await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review, "--webforms-modernization"],
            output,
            error,
            SyntheticScan);

        Assert.Equal(1, exit);
        Assert.Contains("LOCAL_REVIEW_STAGE_FAILED", error.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(review, "scan", "scan-manifest.json")));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        Assert.Equal("failed", document.RootElement.GetProperty("outcome").GetString());
        var stages = document.RootElement.GetProperty("stages").EnumerateArray().ToArray();
        Assert.Equal("scan", stages[0].GetProperty("stage").GetString());
        Assert.Equal("webforms-modernization", stages[1].GetProperty("stage").GetString());
        Assert.Equal("failed", stages[1].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Downstream_input_mutation_fails_closed_and_preserves_a_typed_result()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        var review = Path.Combine(temp.Path, "mutation-failure");
        using var output = new StringWriter();
        using var error = new StringWriter();

        static async Task MutateScan(
            TraceMap.Reporting.WebFormsModernizationOptions options,
            CancellationToken cancellationToken)
        {
            var scanDirectory = Path.GetDirectoryName(options.IndexPath)!;
            await File.AppendAllTextAsync(
                Path.Combine(scanDirectory, "report.md"),
                "mutated",
                cancellationToken);
            Directory.CreateDirectory(options.OutputDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(options.OutputDirectory, "webforms-modernization.json"),
                "{}",
                cancellationToken);
        }

        static Task NoExplorer(
            TraceMap.Reporting.StaticHtmlEvidenceExplorerOptions options,
            CancellationToken cancellationToken) => Task.CompletedTask;

        var services = new LocalReviewStageServices(MutateScan, NoExplorer);
        var exit = await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review, "--webforms-modernization"],
            output,
            error,
            async (arguments, stdout, stderr, token) =>
                await TraceMapCommand.RunAsync(["scan", .. arguments], stdout, stderr, token),
            stageServices: services);

        Assert.Equal(1, exit);
        Assert.Contains("LOCAL_REVIEW_INPUT_MUTATED", error.ToString(), StringComparison.Ordinal);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        Assert.Equal("failed", document.RootElement.GetProperty("outcome").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("gaps").EnumerateArray(),
            gap => gap.GetString() == "LOCAL_REVIEW_INPUT_MUTATED");
    }

    [Fact]
    public async Task Repeated_immutable_source_runs_keep_workflow_identity_stable()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        var first = Path.Combine(temp.Path, "review-one");
        var second = Path.Combine(temp.Path, "review-two");

        Assert.Equal(0, await TraceMapCommand.RunAsync(
            ["local-review", "run", "--repo", repo, "--out", first],
            TextWriter.Null,
            TextWriter.Null));
        Assert.Equal(0, await TraceMapCommand.RunAsync(
            ["local-review", "run", "--repo", repo, "--out", second],
            TextWriter.Null,
            TextWriter.Null));

        using var firstDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(first, "local-review-result.json")));
        using var secondDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(second, "local-review-result.json")));
        Assert.Equal(
            firstDocument.RootElement.GetProperty("workflowId").GetString(),
            secondDocument.RootElement.GetProperty("workflowId").GetString());
        Assert.Equal(
            firstDocument.RootElement.GetProperty("sourceSnapshotDigest").GetString(),
            secondDocument.RootElement.GetProperty("sourceSnapshotDigest").GetString());
    }

    [Fact]
    public void Local_review_schema_is_closed_and_versioned()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "docs", "contracts", "local-review-result.v1.schema.json")));

        Assert.False(document.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            "local-review-result.v1",
            document.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        Assert.False(document.RootElement.GetProperty("$defs").GetProperty("artifact").GetProperty("additionalProperties").GetBoolean());
        var required = document.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.DoesNotContain("repositoryIdentityHash", required);
        Assert.DoesNotContain("commitSha", required);
    }

    private static string CreateRepository(string root, bool webForms)
    {
        var repo = Path.Combine(root, webForms ? "webforms-repo" : "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Sample.csproj"), webForms
            ? """
              <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              </Project>
              """
            : """
              <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              </Project>
              """);
        File.WriteAllText(Path.Combine(repo, "Page.aspx"), webForms
            ? "<%@ Page Language=\"C#\" CodeBehind=\"Page.aspx.cs\" Inherits=\"Sample.Page\" %><asp:Button ID=\"Save\" runat=\"server\" OnClick=\"Save_Click\" />"
            : "plain fixture");
        if (webForms)
        {
            File.WriteAllText(Path.Combine(repo, "Page.aspx.cs"), """
                namespace Sample;
                public class Page
                {
                    protected void Save_Click(object sender, System.EventArgs e) { }
                }
                """);
        }

        RunGit(repo, "init");
        RunGit(repo, "add", ".");
        RunGit(repo, "-c", "user.name=TraceMap", "-c", "user.email=fixture@example.invalid", "commit", "-m", "baseline");
        return repo;
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "dotnet", "TraceMap.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
