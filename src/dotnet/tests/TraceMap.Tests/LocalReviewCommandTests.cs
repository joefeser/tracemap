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
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "scan", "scan-receipt.json")));
        Assert.Equal(
            receipt.RootElement.GetProperty("repositoryIdentityHash").GetString(),
            root.GetProperty("repositoryIdentityHash").GetString());
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
                "--input", Path.Combine(review, "scan"),
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
    public async Task Late_scan_failure_preserves_valid_available_manifest_identity()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        var review = Path.Combine(temp.Path, "late-scan-failure");

        static async Task<int> FailedAfterManifest(
            string[] args,
            TextWriter stdout,
            TextWriter stderr,
            CancellationToken cancellationToken)
        {
            var outputPath = args[Array.IndexOf(args, "--out") + 1];
            Directory.CreateDirectory(outputPath);
            await File.WriteAllTextAsync(
                Path.Combine(outputPath, "scan-manifest.json"),
                JsonSerializer.Serialize(SyntheticManifest(
                    "scan-late-failure",
                    new string('a', 40),
                    new string('b', 64))),
                cancellationToken);
            return 1;
        }

        Assert.Equal(1, await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review],
            TextWriter.Null,
            TextWriter.Null,
            FailedAfterManifest));

        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        Assert.Equal(new string('a', 40), result.RootElement.GetProperty("commitSha").GetString());
        Assert.Equal("scan-late-failure", result.RootElement.GetProperty("scanId").GetString());
        Assert.Equal(new string('b', 64), result.RootElement.GetProperty("sourceSnapshotDigest").GetString());
        Assert.Equal("full", result.RootElement.GetProperty("coverage").GetString());
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
            await WriteSyntheticReceiptAsync(outputPath, manifest, cancellationToken);
            return 0;
        }

        var exit = await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review, "--webforms-modernization"],
            output,
            error,
            SyntheticScan);

        Assert.Equal(1, exit);
        Assert.Contains("LOCAL_REVIEW_WEBFORMS_INPUT_INCOMPATIBLE", error.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(review, "scan", "scan-manifest.json")));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        Assert.Equal("failed", document.RootElement.GetProperty("outcome").GetString());
        var stages = document.RootElement.GetProperty("stages").EnumerateArray().ToArray();
        Assert.Equal("scan", stages[0].GetProperty("stage").GetString());
        Assert.Equal("webforms-modernization", stages[1].GetProperty("stage").GetString());
        Assert.Equal("failed", stages[1].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Successful_scan_rejects_conflicting_receipt_before_downstream_stages()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        var review = Path.Combine(temp.Path, "receipt-conflict");
        var downstreamCalled = false;
        var services = new LocalReviewStageServices(
            (options, token) =>
            {
                downstreamCalled = true;
                return Task.CompletedTask;
            },
            (options, token) => Task.CompletedTask);

        static async Task<int> ConflictingReceiptScan(
            string[] args,
            TextWriter stdout,
            TextWriter stderr,
            CancellationToken cancellationToken)
        {
            var outputPath = args[Array.IndexOf(args, "--out") + 1];
            var manifest = SyntheticManifest("scan-receipt-conflict", new string('a', 40), new string('b', 64));
            await WriteSyntheticScanAsync(outputPath, manifest, cancellationToken);
            await WriteSyntheticReceiptAsync(
                outputPath,
                manifest with { CommitSha = new string('e', 40) },
                cancellationToken);
            return 0;
        }

        using var error = new StringWriter();
        Assert.Equal(1, await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review, "--webforms-modernization"],
            TextWriter.Null,
            error,
            ConflictingReceiptScan,
            stageServices: services));

        Assert.False(downstreamCalled);
        Assert.Contains("LOCAL_REVIEW_IDENTITY_UNAVAILABLE", error.ToString(), StringComparison.Ordinal);
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        Assert.Equal("unknown", result.RootElement.GetProperty("coverage").GetString());
        Assert.Contains(
            result.RootElement.GetProperty("gaps").EnumerateArray(),
            gap => gap.GetString() == "LOCAL_REVIEW_IDENTITY_UNAVAILABLE");
    }

    [Fact]
    public async Task Incompatible_webforms_input_emits_typed_stage_failure()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);
        var review = Path.Combine(temp.Path, "webforms-incompatible");
        var services = new LocalReviewStageServices(
            (options, token) => throw new InvalidDataException("WebFormsModernizationIndexUnsupported"),
            (options, token) => Task.CompletedTask);
        using var error = new StringWriter();

        Assert.Equal(1, await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review, "--webforms-modernization"],
            TextWriter.Null,
            error,
            async (arguments, stdout, stderr, token) =>
                await TraceMapCommand.RunAsync(["scan", .. arguments], stdout, stderr, token),
            stageServices: services));

        Assert.Contains("LOCAL_REVIEW_WEBFORMS_INPUT_INCOMPATIBLE", error.ToString(), StringComparison.Ordinal);
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        Assert.Equal("review-scan-gaps", result.RootElement.GetProperty("nextAction").GetString());
        Assert.Contains(
            result.RootElement.GetProperty("gaps").EnumerateArray(),
            gap => gap.GetString() == "LOCAL_REVIEW_WEBFORMS_INPUT_INCOMPATIBLE");
    }

    [Fact]
    public async Task Downstream_failure_identity_includes_the_verified_snapshot()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: false);

        static LocalReviewScanRunner FailedWebFormsScan(string snapshot) => async (
            args,
            stdout,
            stderr,
            cancellationToken) =>
        {
            var outputPath = args[Array.IndexOf(args, "--out") + 1];
            await WriteSyntheticScanAsync(outputPath, SyntheticManifest("scan-" + snapshot[..8], new string('a', 40), snapshot), cancellationToken);
            return 0;
        };

        var firstOutput = Path.Combine(temp.Path, "failure-one");
        var secondOutput = Path.Combine(temp.Path, "failure-two");
        Assert.Equal(1, await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", firstOutput, "--webforms-modernization"],
            TextWriter.Null,
            TextWriter.Null,
            FailedWebFormsScan(new string('b', 64))));
        Assert.Equal(1, await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", secondOutput, "--webforms-modernization"],
            TextWriter.Null,
            TextWriter.Null,
            FailedWebFormsScan(new string('c', 64))));

        using var first = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(firstOutput, "local-review-result.json")));
        using var second = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(secondOutput, "local-review-result.json")));
        Assert.NotEqual(first.RootElement.GetProperty("workflowId").GetString(), second.RootElement.GetProperty("workflowId").GetString());
    }

    [Fact]
    public async Task Explorer_failure_preserves_reduced_webforms_packet_coverage()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, webForms: true);
        var review = Path.Combine(temp.Path, "partial-packet-failure");
        var services = new LocalReviewStageServices(
            async (options, token) => { _ = await TraceMap.Reporting.WebFormsModernizationPacketReporter.WriteAsync(options, token); },
            (options, token) => throw new InvalidOperationException("synthetic explorer failure"));

        Assert.Equal(1, await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review, "--webforms-modernization", "--explorer"],
            TextWriter.Null,
            TextWriter.Null,
            async (arguments, stdout, stderr, token) =>
                await TraceMapCommand.RunAsync(["scan", .. arguments], stdout, stderr, token),
            stageServices: services));

        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        Assert.Equal("reduced", result.RootElement.GetProperty("coverage").GetString());
        Assert.Contains(
            result.RootElement.GetProperty("gaps").EnumerateArray(),
            gap => gap.GetString() == "LOCAL_REVIEW_WEBFORMS_PARTIAL");
        Assert.Equal(
            "partial",
            result.RootElement.GetProperty("stages")[1].GetProperty("outcome").GetString());
        Assert.Equal(
            "failed",
            result.RootElement.GetProperty("stages")[2].GetProperty("outcome").GetString());
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

    private static ScanManifest SyntheticManifest(string scanId, string commitSha, string snapshot) => new(
        scanId,
        "synthetic",
        null,
        null,
        commitSha,
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
        new string('c', 32),
        snapshot);

    private static async Task WriteSyntheticScanAsync(
        string outputPath,
        ScanManifest manifest,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.Combine(outputPath, "logs"));
        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "scan-manifest.json"),
            JsonSerializer.Serialize(manifest),
            cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "facts.ndjson"), string.Empty, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "index.sqlite"), string.Empty, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "report.md"), "report", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "logs", "analyzer.log"), string.Empty, cancellationToken);
        await WriteSyntheticReceiptAsync(outputPath, manifest, cancellationToken);
    }

    private static async Task WriteSyntheticReceiptAsync(
        string outputPath,
        ScanManifest manifest,
        CancellationToken cancellationToken)
    {
        var receipt = new ScanExecutionReceipt(
            ScanReceiptSchema.Version,
            "receipt-synthetic",
            RuleIds.ScannerStageReceipt,
            "operational-diagnostic",
            manifest.ScanId,
            manifest.ScanId,
            "test-scanner",
            ["test-scanner"],
            new string('d', 64),
            manifest.CommitSha,
            new string('e', 64),
            manifest.SourceSnapshotDigest,
            "succeeded",
            "full",
            [],
            [],
            [],
            ["Synthetic test receipt."]);
        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "scan-receipt.json"),
            JsonSerializer.Serialize(receipt),
            cancellationToken);
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
