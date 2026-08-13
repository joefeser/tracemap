using System.Diagnostics;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ScanExecutionReceiptTests
{
    private const string CommitSha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task Receipt_is_canonical_bounded_and_omits_protected_inputs()
    {
        using var temp = new TempDirectory();
        var protectedPath = Path.Combine(temp.Path, "customer", "source");
        var recorder = new ScanReceiptRecorder(new ScanOptions(
            protectedPath,
            Path.Combine(temp.Path, "out"),
            IncludeGlobs: ["src/private/**"],
            ExcludeGlobs: ["secrets/**"]));
        recorder.Bind(new GitMetadata("customer-name", "https://private.example.invalid/customer/repo", "dev", CommitSha, []));
        using (var operation = recorder.StartStage("semantic-analysis", "webforms-static-extraction"))
        {
            operation.Complete(
                "partial",
                "semantic-reduced",
                "syntax-fallback-completed",
                "retry-after-dependency-restoration",
                "review-analysis-gaps",
                supportingFactIds: Enumerable.Range(0, 300).Select(index => $"fact-{index:x20}"),
                supportingGapIds: ["fact-bbbbbbbbbbbbbbbbbbbb", "fact-aaaaaaaaaaaaaaaaaaaa", "fact-aaaaaaaaaaaaaaaaaaaa"]);
        }
        recorder.Complete("partial", "semantic-reduced", ["fact-bbbbbbbbbbbbbbbbbbbb", "fact-aaaaaaaaaaaaaaaaaaaa"], ["fact-dddddddddddddddddddd", "fact-cccccccccccccccccccc"]);

        var receipt = recorder.CreateReceipt();
        var first = Path.Combine(temp.Path, "first.json");
        var second = Path.Combine(temp.Path, "second.json");
        await ScanExecutionReceiptWriter.WriteAsync(first, receipt);
        await ScanExecutionReceiptWriter.WriteAsync(second, receipt);

        Assert.Equal(await File.ReadAllBytesAsync(first), await File.ReadAllBytesAsync(second));
        Assert.Equal(ScanReceiptSchema.Version, receipt.SchemaVersion);
        Assert.Equal(RuleIds.ScannerStageReceipt, receipt.RuleId);
        Assert.Equal("operational-diagnostic", receipt.EvidenceClass);
        Assert.Equal(CommitSha, receipt.CommitSha);
        Assert.Equal(ScanReceiptSchema.MaxSupportingIds, Assert.Single(receipt.Stages).SupportingFactIds.Count);
        Assert.Equal(["fact-aaaaaaaaaaaaaaaaaaaa", "fact-bbbbbbbbbbbbbbbbbbbb"], Assert.Single(receipt.Stages).SupportingGapIds);
        Assert.Equal(["fact-aaaaaaaaaaaaaaaaaaaa", "fact-bbbbbbbbbbbbbbbbbbbb"], receipt.SupportingFactIds);
        var json = await File.ReadAllTextAsync(first);
        Assert.DoesNotContain(protectedPath, json, StringComparison.Ordinal);
        Assert.DoesNotContain("customer-name", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private.example.invalid", json, StringComparison.Ordinal);
        Assert.DoesNotContain("src/private", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets", json, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(typeof(TimeoutException), "operation-timed-out")]
    [InlineData(typeof(OperationCanceledException), "operation-cancelled")]
    [InlineData(typeof(UnauthorizedAccessException), "input-unreadable")]
    [InlineData(typeof(InvalidOperationException), "operation-failed")]
    public void Failure_classification_is_categorical(Type exceptionType, string expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        Assert.Equal(expected, ScanReceiptRecorder.ClassifyFailure(exception));
    }

    [Fact]
    public void Output_failure_classification_is_context_specific()
    {
        Assert.Equal("output-artifact-write-failed", ScanReceiptRecorder.ClassifyOutputFailure(new UnauthorizedAccessException()));
        Assert.Equal("output-artifact-write-failed", ScanReceiptRecorder.ClassifyOutputFailure(new DirectoryNotFoundException()));
        Assert.Equal("output-path-invalid", ScanReceiptRecorder.ClassifyOutputFailure(new ArgumentException()));
    }

    [Fact]
    public void Exception_timeout_and_cancellation_stages_fail_closed_without_exception_text()
    {
        var recorder = new ScanReceiptRecorder(new ScanOptions("repo", "out"));
        recorder.Bind(new GitMetadata("repo", null, "dev", CommitSha, []));
        using (var failed = recorder.StartStage("static-extraction", "webforms-static-extraction"))
            failed.Fail(new InvalidOperationException("protected customer detail"), "stage-started");
        using (var timedOut = recorder.StartStage("semantic-analysis", "compiler-and-syntax-analysis"))
            timedOut.Fail(new TimeoutException("protected timeout detail"), "stage-started");
        using (var cancelled = recorder.StartStage("discovery", "inventory-and-identity"))
            cancelled.Fail(new OperationCanceledException("protected cancellation detail"), "stage-started");
        recorder.Complete("failed", "unknown");

        var receipt = recorder.CreateReceipt();
        Assert.Collection(
            receipt.Stages,
            stage =>
            {
                Assert.Equal("failed", stage.Outcome);
                Assert.Equal("operation-failed", stage.NextAction);
                Assert.Equal("not-attempted", stage.CleanupResult);
            },
            stage =>
            {
                Assert.Equal("timed-out", stage.Outcome);
                Assert.Equal("operation-timed-out", stage.NextAction);
            },
            stage =>
            {
                Assert.Equal("cancelled", stage.Outcome);
                Assert.Equal("operation-cancelled", stage.NextAction);
            });
        var json = JsonSerializer.Serialize(receipt, JsonOptions.Stable);
        Assert.DoesNotContain("protected", json, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TimeoutException", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelled_atomic_write_removes_temporary_artifact()
    {
        using var temp = new TempDirectory();
        var recorder = new ScanReceiptRecorder(new ScanOptions("repo", "out"));
        recorder.Bind(new GitMetadata("repo", null, "dev", CommitSha, []));
        recorder.Complete("failed", "unknown");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ScanExecutionReceiptWriter.WriteAsync(
            Path.Combine(temp.Path, "scan-receipt.json"),
            recorder.CreateReceipt(),
            cancellation.Token));

        Assert.False(File.Exists(Path.Combine(temp.Path, "scan-receipt.json")));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Receipt_schema_and_rule_catalog_are_versioned_and_document_limitations()
    {
        var root = FindRepoRoot();
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "contracts", "scan-execution-receipt.v1.schema.json")));
        Assert.Equal(ScanReceiptSchema.Version, schema.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        Assert.Equal(RuleIds.ScannerStageReceipt, schema.RootElement.GetProperty("properties").GetProperty("ruleId").GetProperty("const").GetString());
        var catalog = File.ReadAllText(Path.Combine(root, "rules", "rule-catalog.yml"));
        var start = catalog.IndexOf("  - id: scanner.stage-receipt.v1", StringComparison.Ordinal);
        var end = catalog.IndexOf("\n  - id:", start + 1, StringComparison.Ordinal);
        var block = catalog[start..end];
        Assert.Contains("operational diagnostics, not static code facts", block, StringComparison.Ordinal);
        Assert.Contains("No authoritative receipt is emitted", block, StringComparison.Ordinal);
        Assert.Contains("Default receipts omit raw exception messages", block, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Commit_bound_cli_scan_writes_sanitized_receipt_with_gap_support()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "private-customer-repository");
        var outputPath = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Legacy.csproj"), """
            <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Broken.aspx.cs" />
                <Content Include="Broken.aspx" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Broken.aspx"), "<%@ Page Language=\"C#\" %><asp:Button runat=\"server\" OnClick=\"Missing");
        File.WriteAllText(Path.Combine(repo, "Broken.aspx.cs"), "public partial class Broken : System.Web.UI.Page { }");
        InitializeRepository(repo);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var receiptPath = Path.Combine(outputPath, "scan-receipt.json");
        Assert.True(File.Exists(receiptPath));
        var receipt = JsonSerializer.Deserialize<ScanExecutionReceipt>(
            await File.ReadAllTextAsync(receiptPath),
            JsonOptions.Stable)!;
        Assert.Equal(RuleIds.ScannerStageReceipt, receipt.RuleId);
        Assert.Matches("^[0-9a-f]{40}$", receipt.CommitSha);
        Assert.NotEmpty(receipt.Stages);
        Assert.Contains(receipt.Stages, stage => stage.Stage == "static-extraction" && stage.Outcome == "partial");
        Assert.NotEmpty(receipt.SupportingGapIds);
        Assert.DoesNotContain(repo, await File.ReadAllTextAsync(receiptPath), StringComparison.Ordinal);
        Assert.DoesNotContain("private-customer-repository", await File.ReadAllTextAsync(receiptPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Commit_bound_inaccessible_source_writes_failure_receipt_without_normal_artifacts()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var restricted = Path.Combine(repo, "restricted");
        var outputPath = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(restricted);
        File.WriteAllText(Path.Combine(restricted, "Hidden.cs"), "public sealed class Hidden { }");
        InitializeRepository(repo);
        var originalMode = File.GetUnixFileMode(restricted);
        try
        {
            File.SetUnixFileMode(restricted, UnixFileMode.None);
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], stdout, stderr);

            Assert.Equal(1, exitCode);
            Assert.Equal("error: SourceInventoryIncomplete" + Environment.NewLine, stderr.ToString());
            Assert.False(File.Exists(Path.Combine(outputPath, "scan-manifest.json")));
            var receipt = JsonSerializer.Deserialize<ScanExecutionReceipt>(
                await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-receipt.json")),
                JsonOptions.Stable)!;
            Assert.Equal("failed", receipt.Outcome);
            Assert.Contains(receipt.Stages, stage => stage.Stage == "discovery"
                && stage.Outcome == "failed"
                && stage.NextAction == "sourceinventoryincomplete");
            Assert.DoesNotContain(repo, await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-receipt.json")), StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(restricted, originalMode);
        }
    }

    [Fact]
    public async Task Artifact_write_failure_uses_output_specific_category()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "occupied-output-path");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Sample.cs"), "public sealed class Sample { }");
        File.WriteAllText(outputPath, "occupied");
        InitializeRepository(repo);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal("error: output-artifact-write-failed" + Environment.NewLine, stderr.ToString());
        Assert.DoesNotContain(outputPath, stderr.ToString(), StringComparison.Ordinal);
    }

    private static void InitializeRepository(string path)
    {
        RunGit(path, "init", "-b", "fixture");
        RunGit(path, "config", "user.email", "fixture@example.invalid");
        RunGit(path, "config", "user.name", "TraceMap Fixture");
        RunGit(path, "config", "commit.gpgsign", "false");
        RunGit(path, "add", ".");
        RunGit(path, "commit", "-m", "fixture");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TraceMap.slnx"))
                || Directory.Exists(Path.Combine(directory.FullName, "rules")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {stdout} {stderr}");
    }
}
