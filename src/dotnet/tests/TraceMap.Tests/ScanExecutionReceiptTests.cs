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
                supportingGapIds: ["fact-bbbbbbbbbbbbbbbbbbbb", "fact-aaaaaaaaaaaaaaaaaaaa", "fact-aaaaaaaaaaaaaaaaaaaa", "0123456789abcdef01234567"]);
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
        Assert.Equal(["0123456789abcdef01234567", "fact-aaaaaaaaaaaaaaaaaaaa", "fact-bbbbbbbbbbbbbbbbbbbb"], Assert.Single(receipt.Stages).SupportingGapIds);
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
    public void Stage_identity_includes_categorical_diagnostic_fields()
    {
        var generic = new ScanReceiptRecorder(new ScanOptions("repo", "out"));
        generic.Bind(new GitMetadata("repo", null, "dev", CommitSha, []));
        using (var operation = generic.StartStage("discovery", "inventory-and-identity"))
            operation.Fail(new InvalidOperationException(), "stage-started");
        generic.Complete("failed", "unknown");

        var inventory = new ScanReceiptRecorder(new ScanOptions("repo", "out"));
        inventory.Bind(new GitMetadata("repo", null, "dev", CommitSha, []));
        using (var operation = inventory.StartStage("discovery", "inventory-and-identity"))
            operation.Fail(new SourceInventoryException(new IOException()), "stage-started");
        inventory.Complete("failed", "unknown");

        var genericStage = Assert.Single(generic.CreateReceipt().Stages);
        var inventoryStage = Assert.Single(inventory.CreateReceipt().Stages);
        Assert.Equal("failed", genericStage.Outcome);
        Assert.Equal("failed", inventoryStage.Outcome);
        Assert.NotEqual(genericStage.NextAction, inventoryStage.NextAction);
        Assert.NotEqual(genericStage.StageId, inventoryStage.StageId);
        Assert.NotEqual(generic.CreateReceipt().ReceiptId, inventory.CreateReceipt().ReceiptId);
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
        Assert.Contains(
            schema.RootElement.GetProperty("$defs").GetProperty("stage").GetProperty("properties").GetProperty("operationCode").GetProperty("enum").EnumerateArray(),
            value => value.GetString() == "receipt-write");
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

    [Fact]
    public async Task Sql_validation_gaps_downgrade_receipt_and_preserve_gap_support()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "output");
        var summaryPath = Path.Combine(temp.Path, "malformed-summary.json");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Sample.cs"), "public sealed class Sample { }");
        File.WriteAllText(summaryPath, "{ malformed");
        InitializeRepository(repo);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync([
            "scan", "--repo", repo, "--out", outputPath,
            "--sql-validation-summary", summaryPath,
            "--sql-validation-as-of", "2026-08-13T12:00:00+00:00"
        ], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var receipt = JsonSerializer.Deserialize<ScanExecutionReceipt>(
            await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-receipt.json")), JsonOptions.Stable)!;
        Assert.Equal("partial", receipt.Outcome);
        var reportStage = Assert.Single(receipt.Stages, stage => stage.OperationCode == "report-write");
        Assert.Equal("partial", reportStage.Outcome);
        var gapId = Assert.Single(reportStage.SupportingGapIds);
        Assert.Matches("^[0-9a-f]{24}$", gapId);
        Assert.Contains(gapId, receipt.SupportingGapIds);
    }

    [Fact]
    public async Task Source_only_scan_records_syntax_not_semantic_stage_coverage()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Sample.cs"), "public sealed class Sample { }");
        InitializeRepository(repo);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var receipt = JsonSerializer.Deserialize<ScanExecutionReceipt>(
            await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-receipt.json")), JsonOptions.Stable)!;
        var stage = Assert.Single(receipt.Stages, candidate => candidate.Stage == "semantic-analysis");
        Assert.StartsWith("syntax", stage.CoverageAfter, StringComparison.Ordinal);
        Assert.DoesNotContain("semantic", stage.CoverageAfter, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reduced_analysis_marks_top_level_receipt_partial_when_build_succeeds()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup><Compile Include="App.cs" /></ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "App.cs"), "public sealed class App { }");
        File.WriteAllText(Path.Combine(repo, "Migration.cs"), """
            using MigrationBase = Microsoft.EntityFrameworkCore.Migrations.Migration;
            public sealed class M : MigrationBase
            {
                public void Up(object builder) => builder.Sql("SELECT private_value");
            }
            """);
        InitializeRepository(repo);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var receipt = JsonSerializer.Deserialize<ScanExecutionReceipt>(
            await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-receipt.json")), JsonOptions.Stable)!;
        Assert.Equal("partial", receipt.Outcome);
        Assert.Equal("semantic-reduced", receipt.Coverage);
        Assert.NotEmpty(receipt.SupportingGapIds);
    }

    [Fact]
    public async Task Final_receipt_write_failure_is_recorded_before_fallback_retry()
    {
        using var temp = new TempDirectory();
        var recorder = new ScanReceiptRecorder(new ScanOptions("repo", "out"));
        recorder.Bind(new GitMetadata("repo", null, "dev", CommitSha, []));
        recorder.Complete("succeeded", "Level1SemanticAnalysis");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TraceMapCommand.WriteFinalScanReceiptAsync(
            Path.Combine(temp.Path, "scan-receipt.json"),
            recorder,
            "Level1SemanticAnalysis",
            cancellation.Token,
            (_, _, token) => Task.FromCanceled(token)));

        recorder.Complete("cancelled", "Level1SemanticAnalysis");
        var receiptPath = Path.Combine(temp.Path, "scan-receipt.json");
        await ScanExecutionReceiptWriter.WriteAsync(receiptPath, recorder.CreateReceipt(), CancellationToken.None);
        var receipt = JsonSerializer.Deserialize<ScanExecutionReceipt>(
            await File.ReadAllTextAsync(receiptPath), JsonOptions.Stable)!;
        var stage = Assert.Single(receipt.Stages, candidate => candidate.OperationCode == "receipt-write");
        Assert.Equal("cancelled", stage.Outcome);
        Assert.Equal("operation-cancelled", stage.NextAction);
        Assert.Equal("analyzer-log-written", stage.LastProvenSafeState);
    }

    [Fact]
    public async Task Log_directory_collision_records_failed_artifact_stage()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(repo, "Sample.cs"), "public sealed class Sample { }");
        File.WriteAllText(Path.Combine(outputPath, "logs"), "occupied");
        InitializeRepository(repo);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal("error: output-artifact-write-failed" + Environment.NewLine, stderr.ToString());
        var receipt = JsonSerializer.Deserialize<ScanExecutionReceipt>(
            await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-receipt.json")), JsonOptions.Stable)!;
        Assert.Equal("failed", receipt.Outcome);
        Assert.Contains(receipt.Stages, stage => stage.OperationCode == "output-directory-prepare"
            && stage.Outcome == "failed"
            && stage.NextAction == "output-artifact-write-failed");
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
