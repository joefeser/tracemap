using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class ScanProgressDiagnosticsTests
{
    [Fact]
    public async Task Progress_is_observable_before_a_blocking_scan_completes()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        var checkpoint = Path.Combine(temp.Path, "outside", "progress.json");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var progress = new ConcurrentStringWriter();

        var run = RunLocalReviewAsync(
            repo,
            review,
            checkpoint,
            progress,
            runner: async (args, stdout, stderr, token) =>
            {
                await gate.Task.WaitAsync(token);
                await WriteSyntheticScanAsync(args[Array.IndexOf(args, "--out") + 1], token);
                return 0;
            });

        var observed = AwaitLine(progress, "stage=scan state=started");
        Assert.True(observed, "expected a scan-started progress line before the blocking scan completed");
        Assert.True(File.Exists(checkpoint));

        gate.SetResult();
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task Diagnostic_progress_is_not_hidden_behind_buffered_scan_stringwriters()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        var checkpoint = Path.Combine(temp.Path, "progress.json");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var progress = new ConcurrentStringWriter();
        string? bufferedScanStderr = null;

        var run = RunLocalReviewAsync(
            repo,
            review,
            checkpoint,
            progress,
            runner: async (args, stdout, stderr, token) =>
            {
                await stderr.WriteLineAsync("noisy scan output that stays buffered");
                bufferedScanStderr = stderr.ToString();
                await gate.Task.WaitAsync(token);
                await WriteSyntheticScanAsync(args[Array.IndexOf(args, "--out") + 1], token);
                return 0;
            });

        Assert.True(AwaitLine(progress, "stage=scan state=started"));
        Assert.NotNull(bufferedScanStderr);
        Assert.Contains("noisy scan output", bufferedScanStderr, StringComparison.Ordinal);
        Assert.DoesNotContain("tracemap-progress", bufferedScanStderr, StringComparison.Ordinal);

        gate.SetResult();
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task Heartbeats_report_categorical_stage_elapsed_and_last_completed_stage()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        var checkpoint = Path.Combine(temp.Path, "progress.json");
        var time = new ManualTimeProvider();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var progress = new ConcurrentStringWriter();

        var run = RunLocalReviewAsync(
            repo,
            review,
            checkpoint,
            progress,
            timeProvider: time,
            runner: async (args, stdout, stderr, token) =>
            {
                await gate.Task.WaitAsync(token);
                await WriteSyntheticScanAsync(args[Array.IndexOf(args, "--out") + 1], token);
                return 0;
            });

        Assert.True(AwaitLine(progress, "stage=scan state=started"));
        for (var beat = 1; beat <= 4; beat++)
        {
            time.Advance(TimeSpan.FromSeconds(15));
            Assert.True(
                AwaitLine(progress, $"state=heartbeat elapsedMs={beat * 15_000} "),
                $"expected heartbeat {beat}");
            var line = progress.Lines().Last(line => line.Contains("state=heartbeat", StringComparison.Ordinal));
            Assert.Contains("stage=scan ", line, StringComparison.Ordinal);
            Assert.Contains("lastSuccessfulStage=staging-initialized", line, StringComparison.Ordinal);
        }

        gate.SetResult();
        Assert.Equal(0, await run);

        var checkpointDocument = ParseCheckpoint(checkpoint);
        Assert.All(
            checkpointDocument.RootElement.GetProperty("history").EnumerateArray(),
            historyEvent => Assert.NotEqual("heartbeat", historyEvent.GetProperty("state").GetString()));
    }

    [Fact]
    public async Task Heartbeat_history_is_bounded()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var checkpointPath = Path.Combine(temp.Path, "progress.json");
        using var progress = new ConcurrentStringWriter();
        using var reporter = new ScanProgressReporter(progress, checkpointPath);
        reporter.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.Scan);
        for (var index = 0; index < ScanProgressSchema.MaxHistoryEvents + 20; index++)
        {
            reporter.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.ProjectLoad, index + 1);
            reporter.FinishStage(ScanProgressReporter.ScanOperation, ScanProgressStages.ProjectLoad, "completed", ordinal: index + 1);
        }

        var checkpoint = ParseCheckpoint(checkpointPath);
        Assert.Equal(ScanProgressSchema.MaxHistoryEvents, checkpoint.RootElement.GetProperty("history").GetArrayLength());
    }

    [Fact]
    public async Task Progress_output_contains_no_repository_project_or_path_values()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(
            temp.Path,
            repoName: "AdversarialPrivateRepo",
            projectName: "VerySecretProject",
            pageName: "TopSecretPage");
        File.WriteAllText(
            Path.Combine(repo, "TopSecretPage.aspx.cs"),
            "namespace VerySecretProject; public class TopSecretSymbol { public string SecretValue { get; set; } }");
        RunGit(repo, "add", ".");
        RunGit(repo, "-c", "user.name=TraceMap", "-c", "user.email=fixture@example.invalid", "commit", "-m", "adversarial");
        var review = Path.Combine(temp.Path, "review");
        var checkpoint = Path.Combine(temp.Path, "progress.json");
        using var progress = new ConcurrentStringWriter();
        using var error = new StringWriter();

        var exit = await LocalReviewCommand.RunAsync(
            [
                "run", "--repo", repo, "--out", review,
                "--diagnostic-progress", checkpoint,
                "--timeout-seconds", "300"
            ],
            TextWriter.Null,
            error,
            ScanRunner,
            progressConsole: progress);

        Assert.Equal(0, exit);
        var observedText = string.Join('\n', progress.Lines()) + Environment.NewLine
            + await File.ReadAllTextAsync(checkpoint);
        Assert.DoesNotContain("AdversarialPrivateRepo", observedText, StringComparison.Ordinal);
        Assert.DoesNotContain("VerySecretProject", observedText, StringComparison.Ordinal);
        Assert.DoesNotContain("TopSecretPage", observedText, StringComparison.Ordinal);
        Assert.DoesNotContain("TopSecretSymbol", observedText, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretValue", observedText, StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, observedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Durable_checkpoint_exists_before_final_publication_and_output_stays_atomic()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        var checkpoint = Path.Combine(temp.Path, "progress.json");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var progress = new ConcurrentStringWriter();
        var observedCheckpointBeforePublication = false;

        var run = RunLocalReviewAsync(
            repo,
            review,
            checkpoint,
            progress,
            runner: async (args, stdout, stderr, token) =>
            {
                observedCheckpointBeforePublication = File.Exists(checkpoint);
                Assert.False(Directory.Exists(review), "final output must not exist while staging is in flight");
                await gate.Task.WaitAsync(token);
                await WriteSyntheticScanAsync(args[Array.IndexOf(args, "--out") + 1], token);
                return 0;
            });

        Assert.True(AwaitLine(progress, "stage=scan state=started"));
        gate.SetResult();
        Assert.Equal(0, await run);
        Assert.True(observedCheckpointBeforePublication);
        Assert.True(File.Exists(Path.Combine(review, "local-review-result.json")));
        Assert.True(File.Exists(Path.Combine(review, "scan", "scan-manifest.json")));

        var latest = ParseCheckpoint(checkpoint).RootElement.GetProperty("latest");
        Assert.Equal("local-review-publication", latest.GetProperty("stage").GetString());
        Assert.Equal("completed", latest.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Timeout_returns_typed_failure_and_preserves_sanitized_checkpoint()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        var checkpoint = Path.Combine(temp.Path, "progress.json");
        var time = new ManualTimeProvider();
        using var progress = new ConcurrentStringWriter();
        using var error = new StringWriter();

        var run = LocalReviewCommand.RunAsync(
            [
                "run", "--repo", repo, "--out", review,
                "--diagnostic-progress", checkpoint,
                "--timeout-seconds", "30"
            ],
            TextWriter.Null,
            error,
            async (args, stdout, stderr, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return 0;
            },
            progressConsole: progress,
            timeProvider: time);

        time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(1, await run);
        Assert.Contains("LOCAL_REVIEW_TIMEOUT", error.ToString(), StringComparison.Ordinal);

        Assert.True(File.Exists(checkpoint));
        var latest = ParseCheckpoint(checkpoint).RootElement.GetProperty("latest");
        Assert.Equal("timed-out", latest.GetProperty("state").GetString());
        Assert.Equal("scan", latest.GetProperty("stage").GetString());
        Assert.Equal("staging-initialized", latest.GetProperty("lastSuccessfulStage").GetString());

        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        Assert.Equal("timed-out", result.RootElement.GetProperty("outcome").GetString());
        Assert.Contains(
            result.RootElement.GetProperty("gaps").EnumerateArray(),
            gap => gap.GetString() == "LOCAL_REVIEW_TIMEOUT");
        Assert.False(File.Exists(Path.Combine(review, "scan", "facts.ndjson")));
    }

    [Fact]
    public async Task Checkpoint_survives_external_cancellation()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        var checkpoint = Path.Combine(temp.Path, "progress.json");
        using var progress = new ConcurrentStringWriter();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review, "--diagnostic-progress", checkpoint],
            TextWriter.Null,
            TextWriter.Null,
            async (args, stdout, stderr, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return 0;
            },
            cancellationToken: cancellation.Token,
            progressConsole: progress);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.True(File.Exists(checkpoint));
        var latest = ParseCheckpoint(checkpoint).RootElement.GetProperty("latest");
        Assert.Equal("cancelled", latest.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Timeout_option_fails_closed_before_scanning()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        var runnerInvoked = false;

        foreach (var invalid in new[] { "10", "90000", "0", "abc", "-30" })
        {
            using var error = new StringWriter();
            var exit = await LocalReviewCommand.RunAsync(
                ["run", "--repo", repo, "--out", review, "--timeout-seconds", invalid],
                TextWriter.Null,
                error,
                (args, stdout, stderr, token) => { runnerInvoked = true; return Task.FromResult(1); });
            Assert.Equal(1, exit);
            Assert.Contains("LOCAL_REVIEW_TIMEOUT_INVALID", error.ToString(), StringComparison.Ordinal);
        }

        Assert.False(runnerInvoked);
    }

    [Fact]
    public async Task Progress_path_inside_repository_or_output_fails_closed()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        var runnerInvoked = false;
        LocalReviewScanRunner runner = (args, stdout, stderr, token) =>
        {
            runnerInvoked = true;
            return Task.FromResult(1);
        };

        foreach (var unsafePath in new[]
                 {
                     Path.Combine(repo, "progress.json"),
                     Path.Combine(repo, "sub", "progress.json"),
                     Path.Combine(review, "progress.json"),
                     review,
                     Path.Combine(temp.Path, $".review.local-review-{new string('a', 32)}", "progress.json")
                 })
        {
            using var error = new StringWriter();
            var exit = await LocalReviewCommand.RunAsync(
                ["run", "--repo", repo, "--out", review, "--diagnostic-progress", unsafePath],
                TextWriter.Null,
                error,
                runner);
            Assert.Equal(1, exit);
            Assert.Contains("LOCAL_REVIEW_PROGRESS_PATH_UNSAFE", error.ToString(), StringComparison.Ordinal);
        }

        Assert.False(runnerInvoked);
        Assert.False(Directory.Exists(review));
    }

    [Fact]
    public async Task Missing_progress_option_value_fails_closed()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");

        var error = new StringWriter();
        var exit = await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review, "--diagnostic-progress"],
            TextWriter.Null,
            error,
            (args, stdout, stderr, token) => Task.FromResult(1));

        Assert.Equal(1, exit);
        Assert.Contains("LOCAL_REVIEW_ARGUMENT_INVALID", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_reaches_scan_engine_and_semantic_extractor_seams()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() =>
            ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")), cancelled.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() =>
            CSharpSemanticExtractor.Extract(
                repo,
                [],
                cancellationToken: cancelled.Token)));
    }

    [Fact]
    public async Task Solution_project_and_compilation_ordinals_are_deterministic()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(
            temp.Path,
            repoName: "plain-repo",
            projectName: "Ordinal",
            extraProjects: ["Alpha", "Beta"]);

        var firstCheckpoint = Path.Combine(temp.Path, "first-progress.json");
        var secondCheckpoint = Path.Combine(temp.Path, "second-progress.json");
        using var progress = new ConcurrentStringWriter();

        Assert.Equal(0, await RunRealLocalReviewAsync(repo, Path.Combine(temp.Path, "first"), firstCheckpoint, progress));
        Assert.Equal(0, await RunRealLocalReviewAsync(repo, Path.Combine(temp.Path, "second"), secondCheckpoint, progress));

        var first = OrdinalTrail(firstCheckpoint);
        var second = OrdinalTrail(secondCheckpoint);
        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        Assert.Contains(first, entry => entry.stage == "solution-load" && entry.ordinal == 1);
        // Projects loaded through the solution must carry deterministic
        // one-based compilation ordinals, not name identities.
        Assert.Contains(first, entry => entry.stage == "compilation" && entry.ordinal == 1);
        Assert.Contains(first, entry => entry.stage == "compilation" && entry.ordinal == 2);
    }

    [Fact]
    public void Nested_stage_completion_restores_the_enclosing_stage_for_heartbeats()
    {
        using var temp = new TempDirectory();
        var checkpointPath = Path.Combine(temp.Path, "progress.json");
        var time = new ManualTimeProvider();
        using var progress = new ConcurrentStringWriter();
        using var reporter = new ScanProgressReporter(progress, checkpointPath, time);

        reporter.StartStage(ScanProgressReporter.LocalReviewOperation, ScanProgressStages.Scan);
        reporter.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.ProjectLoad, 1);
        reporter.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.Compilation, 1);
        reporter.FinishStage(
            ScanProgressReporter.ScanOperation,
            ScanProgressStages.Compilation,
            "completed",
            ordinal: 1);

        // After the nested compilation stage completes, document-level Roslyn
        // waits must stay observable through the enclosing stages.
        time.Advance(TimeSpan.FromSeconds(15));
        var heartbeat = progress.Lines().Last(line => line.Contains("state=heartbeat", StringComparison.Ordinal));
        Assert.Contains("stage=project-load ordinal=1", heartbeat, StringComparison.Ordinal);
        Assert.Contains("lastSuccessfulStage=compilation", heartbeat, StringComparison.Ordinal);

        reporter.FinishStage(
            ScanProgressReporter.ScanOperation,
            ScanProgressStages.ProjectLoad,
            "completed",
            ordinal: 1);
        time.Advance(TimeSpan.FromSeconds(15));
        var enclosing = progress.Lines().Last(line => line.Contains("state=heartbeat", StringComparison.Ordinal));
        Assert.Contains("op=local-review stage=scan ", enclosing, StringComparison.Ordinal);

        reporter.FinishStage(ScanProgressReporter.LocalReviewOperation, ScanProgressStages.Scan, "completed");
        var linesBeforeExpiry = progress.Lines().Count;
        time.Advance(TimeSpan.FromSeconds(45));
        Assert.All(
            progress.Lines().Skip(linesBeforeExpiry),
            line => Assert.DoesNotContain("state=heartbeat", line, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Timeout_deadline_records_timed_out_even_when_the_runner_ignores_cancellation()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        var checkpoint = Path.Combine(temp.Path, "progress.json");
        var time = new ManualTimeProvider();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var progress = new ConcurrentStringWriter();
        using var error = new StringWriter();

        var run = LocalReviewCommand.RunAsync(
            [
                "run", "--repo", repo, "--out", review,
                "--diagnostic-progress", checkpoint,
                "--timeout-seconds", "30"
            ],
            TextWriter.Null,
            error,
            async (args, stdout, stderr, token) =>
            {
                // Deliberately ignores the token, like an API that blocks
                // without honoring cancellation.
                await release.Task;
                await WriteSyntheticScanAsync(args[Array.IndexOf(args, "--out") + 1], CancellationToken.None);
                return 0;
            },
            progressConsole: progress,
            timeProvider: time);

        time.Advance(TimeSpan.FromSeconds(31));
        // The deadline callback must record the timeout observation even
        // though no OperationCanceledException can reach the workflow yet.
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (File.Exists(checkpoint)
                && (await File.ReadAllTextAsync(checkpoint)).Contains("\"state\": \"timed-out\"", StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(10);
        }

        var stuckCheckpoint = ParseCheckpoint(checkpoint).RootElement.GetProperty("latest");
        Assert.Equal("timed-out", stuckCheckpoint.GetProperty("state").GetString());
        Assert.Equal("staging-initialized", stuckCheckpoint.GetProperty("lastSuccessfulStage").GetString());
        Assert.False(Directory.Exists(review), "no output may be published while the run is stuck");

        release.SetResult();
        Assert.Equal(1, await run);
        Assert.Contains("LOCAL_REVIEW_TIMEOUT", error.ToString(), StringComparison.Ordinal);
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(review, "local-review-result.json")));
        Assert.Equal("timed-out", result.RootElement.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Failing_progress_console_never_fails_the_scan()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        var checkpoint = Path.Combine(temp.Path, "progress.json");
        using var failingConsole = new ThrowingTextWriter();

        var exit = await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review, "--diagnostic-progress", checkpoint],
            TextWriter.Null,
            TextWriter.Null,
            ScanRunner,
            progressConsole: failingConsole);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(checkpoint));
        Assert.True(File.Exists(Path.Combine(review, "local-review-result.json")));
    }

    private sealed class ThrowingTextWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value) => throw new IOException("diagnostic console closed");
    }

    [Fact]
    public async Task Diagnostic_mode_does_not_alter_deterministic_evidence_bytes()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var plainReview = Path.Combine(temp.Path, "plain");
        var diagnosticReview = Path.Combine(temp.Path, "diagnostic");
        var checkpoint = Path.Combine(temp.Path, "progress.json");
        using var progress = new ConcurrentStringWriter();

        Assert.Equal(0, await RunRealLocalReviewAsync(repo, plainReview, null, progress));
        Assert.Equal(0, await RunRealLocalReviewAsync(repo, diagnosticReview, checkpoint, progress));

        // facts and report are byte-deterministic; the manifest carries a
        // wall-clock scannedAt and the receipt carries stage durations, so both
        // are compared after normalizing those pre-existing observations.
        foreach (var relative in new[] { "facts.ndjson", "report.md" })
        {
            Assert.Equal(
                await File.ReadAllBytesAsync(Path.Combine(plainReview, "scan", relative)),
                await File.ReadAllBytesAsync(Path.Combine(diagnosticReview, "scan", relative)));
        }

        using var plainManifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(plainReview, "scan", "scan-manifest.json")));
        using var diagnosticManifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(diagnosticReview, "scan", "scan-manifest.json")));
        Assert.Equal(
            NormalizeField(plainManifest.RootElement, "scannedAt").ToString(),
            NormalizeField(diagnosticManifest.RootElement, "scannedAt").ToString());

        using var plainReceipt = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(plainReview, "scan", "scan-receipt.json")));
        using var diagnosticReceipt = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(diagnosticReview, "scan", "scan-receipt.json")));
        Assert.Equal(
            NormalizeField(plainReceipt.RootElement, "durationMilliseconds").ToString(),
            NormalizeField(diagnosticReceipt.RootElement, "durationMilliseconds").ToString());
    }

    private static JsonDocument NormalizeField(JsonElement element, string fieldName)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            RewriteWithoutField(element, writer, fieldName);
        }

        stream.Position = 0;
        return JsonDocument.Parse(stream);
    }

    private static void RewriteWithoutField(JsonElement element, Utf8JsonWriter writer, string fieldName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == fieldName)
                    {
                        writer.WriteString(property.Name, "normalized");
                        continue;
                    }

                    writer.WritePropertyName(property.Name);
                    RewriteWithoutField(property.Value, writer, fieldName);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    RewriteWithoutField(item, writer, fieldName);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    [Fact]
    public async Task Diagnostics_disabled_behavior_remains_compatible()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, repoName: "plain-repo");
        var review = Path.Combine(temp.Path, "review");
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var progress = new ConcurrentStringWriter();

        var exit = await LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review],
            output,
            error,
            ScanRunner,
            progressConsole: progress);

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Empty(progress.Lines());
        Assert.False(File.Exists(Path.Combine(temp.Path, "progress.json")));
        Assert.True(File.Exists(Path.Combine(review, "local-review-result.json")));
        Assert.Contains("Output: ", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Reporter_normalizes_unknown_categorical_values()
    {
        using var temp = new TempDirectory();
        var checkpointPath = Path.Combine(temp.Path, "progress.json");
        using var progress = new ConcurrentStringWriter();
        using var reporter = new ScanProgressReporter(progress, checkpointPath);

        reporter.Emit(
            "totally-unknown-operation",
            "untrusted-stage-value-do-not-emit",
            "weird-state",
            ordinal: -5,
            counts: new Dictionary<string, long> { ["files"] = 7, ["passwords"] = 9 },
            failureCode: "code with spaces && symbols");

        var latest = ParseCheckpoint(checkpointPath).RootElement.GetProperty("latest");
        Assert.Equal("scan", latest.GetProperty("operation").GetString());
        Assert.Equal("other", latest.GetProperty("stage").GetString());
        Assert.Equal("failed", latest.GetProperty("state").GetString());
        Assert.Equal(0, latest.GetProperty("ordinal").GetInt32());
        Assert.Equal(7, latest.GetProperty("counts").GetProperty("files").GetInt64());
        Assert.False(latest.GetProperty("counts").TryGetProperty("passwords", out _));
        Assert.Equal("CODE-WITH-SPACES----SYMBOLS", latest.GetProperty("failureCode").GetString());
        Assert.DoesNotContain("untrusted-stage-value-do-not-emit", string.Join('\n', progress.Lines()), StringComparison.Ordinal);
    }

    [Fact]
    public void Reporter_checkpoint_rewrites_atomically_and_stays_parseable()
    {
        using var temp = new TempDirectory();
        var checkpointPath = Path.Combine(temp.Path, "nested", "progress.json");
        using var reporter = new ScanProgressReporter(TextWriter.Null, checkpointPath);

        for (var index = 0; index < 10; index++)
        {
            reporter.StartStage(ScanProgressReporter.LocalReviewOperation, ScanProgressStages.Inventory);
            reporter.FinishStage(ScanProgressReporter.LocalReviewOperation, ScanProgressStages.Inventory, "completed");
            var parsed = ParseCheckpoint(checkpointPath);
            Assert.Equal("tracemap-scan-progress/v1", parsed.RootElement.GetProperty("schemaVersion").GetString());
        }

        Assert.DoesNotContain(Directory.EnumerateFiles(Path.Combine(temp.Path, "nested")), file =>
            Path.GetFileName(file).StartsWith("progress.json.tmp-", StringComparison.Ordinal));
    }

    [Fact]
    public void Progress_schema_is_closed_versioned_and_categorical()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "docs", "contracts", "tracemap-scan-progress.v1.schema.json")));

        Assert.False(document.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            "tracemap-scan-progress/v1",
            document.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        var eventSchema = document.RootElement.GetProperty("$defs").GetProperty("event");
        Assert.False(eventSchema.GetProperty("additionalProperties").GetBoolean());
        foreach (var property in eventSchema.GetProperty("properties").EnumerateObject())
        {
            Assert.DoesNotContain(property.Name, new[] { "path", "filePath", "repository", "project", "solution", "message", "commandLine" }, StringComparer.Ordinal);
        }

        Assert.Equal(
            32,
            document.RootElement.GetProperty("properties").GetProperty("history").GetProperty("maxItems").GetInt32());
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

            current = current.Parent!;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static async Task<int> RunRealLocalReviewAsync(
        string repo,
        string review,
        string? checkpoint,
        ConcurrentStringWriter progress)
    {
        var arguments = new List<string> { "run", "--repo", repo, "--out", review };
        if (checkpoint is not null)
        {
            arguments.Add("--diagnostic-progress");
            arguments.Add(checkpoint);
        }

        return await LocalReviewCommand.RunAsync(
            arguments.ToArray(),
            TextWriter.Null,
            TextWriter.Null,
            ScanRunner,
            progressConsole: progress);
    }

    private static Task<int> RunLocalReviewAsync(
        string repo,
        string review,
        string checkpoint,
        ConcurrentStringWriter progress,
        LocalReviewScanRunner runner,
        TimeProvider? timeProvider = null) =>
        LocalReviewCommand.RunAsync(
            ["run", "--repo", repo, "--out", review, "--diagnostic-progress", checkpoint],
            TextWriter.Null,
            TextWriter.Null,
            runner,
            progressConsole: progress,
            timeProvider: timeProvider);

    private static Task<int> ScanRunner(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken) =>
        TraceMapCommand.RunAsync(["scan", .. args], stdout, stderr, cancellationToken);

    private static bool AwaitLine(ConcurrentStringWriter writer, string fragment)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (writer.Lines().Any(line => line.Contains(fragment, StringComparison.Ordinal)))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    private static List<(string stage, int ordinal)> OrdinalTrail(string checkpointPath)
    {
        using var document = ParseCheckpoint(checkpointPath);
        return document.RootElement.GetProperty("history").EnumerateArray()
            .Where(stageEvent => stageEvent.TryGetProperty("ordinal", out _))
            .Select(stageEvent => (stageEvent.GetProperty("stage").GetString()!, stageEvent.GetProperty("ordinal").GetInt32()))
            .ToList();
    }

    private static JsonDocument ParseCheckpoint(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream);
    }

    private static ScanManifest SyntheticManifest() => new(
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
        new string('c', 32),
        new string('d', 64));

    private static async Task WriteSyntheticScanAsync(string outputPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.Combine(outputPath, "logs"));
        var manifest = SyntheticManifest();
        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "scan-manifest.json"),
            JsonSerializer.Serialize(manifest),
            cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "facts.ndjson"), string.Empty, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "index.sqlite"), string.Empty, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "report.md"), "report", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "logs", "analyzer.log"), string.Empty, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "scan-receipt.json"),
            JsonSerializer.Serialize(new ScanExecutionReceipt(
                ScanReceiptSchema.Version,
                "receipt-synthetic",
                RuleIds.ScannerStageReceipt,
                "operational-diagnostic",
                manifest.ScanId,
                manifest.ScanId,
                "test-scanner",
                ["test-scanner"],
                new string('e', 64),
                manifest.CommitSha,
                new string('f', 64),
                manifest.SourceSnapshotDigest,
                "succeeded",
                "full",
                [],
                [],
                [],
                ["Synthetic test receipt."])),
            cancellationToken);
    }

    private static string CreateRepository(
        string root,
        string repoName,
        string projectName = "Sample",
        string pageName = "Page",
        string[]? extraProjects = null)
    {
        var repo = Path.Combine(root, repoName);
        Directory.CreateDirectory(repo);
        void WriteProject(string name)
        {
            Directory.CreateDirectory(Path.Combine(repo, name));
            File.WriteAllText(Path.Combine(repo, name, $"{name}.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                </Project>
                """);
            File.WriteAllText(
                Path.Combine(repo, name, $"{name}.cs"),
                "namespace " + name + ";" + Environment.NewLine +
                "public class Service" + Environment.NewLine +
                "{" + Environment.NewLine +
                "    public string Value { get; set; } = \"" + name + "\";" + Environment.NewLine +
                "}");
        }

        if (extraProjects is { Length: > 0 })
        {
            Directory.CreateDirectory(Path.Combine(repo, projectName));
            var projectGuids = extraProjects
                .Select((name, index) => (name, guid: $"{index + 1:00000000}-0000-0000-0000-000000000000"))
                .ToArray();
            File.WriteAllText(Path.Combine(repo, projectName, $"{projectName}.sln"), string.Join(Environment.NewLine,
                new[]
                {
                    "Microsoft Visual Studio Solution File, Format Version 12.00",
                    "# Visual Studio Version 17",
                    "VisualStudioVersion = 17.0.31903.59",
                    "MinimumVisualStudioVersion = 10.0.40219.1"
                }
                .Concat(projectGuids.SelectMany(entry => new[]
                {
                    $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{entry.name}\", \"..\\{entry.name}\\{entry.name}.csproj\", \"{{{entry.guid}}}\"",
                    "EndProject"
                }))
                .Concat(["Global"])
                .Concat(["    GlobalSection(SolutionConfigurationPlatforms) = preSolution"])
                .Concat(["        Debug|Any CPU = Debug|Any CPU"])
                .Concat(["    EndGlobalSection"])
                .Concat(["    GlobalSection(ProjectConfigurationPlatforms) = postSolution"])
                .Concat(projectGuids.SelectMany(entry => new[]
                {
                    $"        {{{entry.guid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
                    $"        {{{entry.guid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU"
                }))
                .Concat(["    EndGlobalSection"])
                .Concat(["EndGlobal"])));
            foreach (var name in extraProjects)
            {
                WriteProject(name);
            }
        }
        else
        {
            WriteProject(projectName);
        }

        File.WriteAllText(
            Path.Combine(repo, $"{pageName}.aspx"),
            $"<%@ Page Language=\"C#\" CodeBehind=\"{pageName}.aspx.cs\" Inherits=\"{projectName}.Page\" %>");
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
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed");
    }

    private sealed class ConcurrentStringWriter : TextWriter
    {
        private readonly object gate = new();
        private readonly StringBuilder builder = new();

        public override Encoding Encoding => Encoding.UTF8;

        public IReadOnlyList<string> Lines()
        {
            lock (gate)
            {
                return builder.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }

        public override void Write(char value)
        {
            lock (gate)
            {
                builder.Append(value);
            }
        }

        public override void WriteLine(string? value)
        {
            lock (gate)
            {
                builder.Append(value).Append('\n');
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ManualTimer> timers = [];
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            lock (gate)
            {
                return timestamp;
            }
        }

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            List<(ManualTimer Timer, long Deadlines)> fired = [];
            lock (gate)
            {
                timestamp += duration.Ticks;
                foreach (var timer in timers)
                {
                    if (!timer.IsEnabled || timer.NextDeadline > timestamp)
                    {
                        continue;
                    }

                    long count = 0;
                    var deadline = timer.NextDeadline;
                    while (deadline <= timestamp)
                    {
                        count++;
                        deadline += timer.PeriodTicks;
                        if (timer.PeriodTicks <= 0)
                        {
                            break;
                        }
                    }

                    if (count > 0)
                    {
                        timer.NextDeadline = deadline;
                        fired.Add((timer, count));
                        if (timer.PeriodTicks <= 0)
                        {
                            timer.IsEnabled = false;
                        }
                    }
                }
            }

            foreach (var (timer, count) in fired)
            {
                for (var index = 0; index < count; index++)
                {
                    timer.Fire();
                }
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, period);
            lock (gate)
            {
                timer.NextDeadline = timestamp + Math.Max(0, dueTime.Ticks);
                timers.Add(timer);
            }

            return timer;
        }

        private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan period) : ITimer
        {
            private readonly TimerCallback callback = callback;
            private readonly object? state = state;

            internal long PeriodTicks { get; } = period.Ticks;

            internal long NextDeadline { get; set; }

            internal bool IsEnabled { get; set; } = true;

            public void Fire() => callback(state);

            public bool Change(TimeSpan newDueTime, TimeSpan newPeriod) => true;

            public void Dispose() => IsEnabled = false;

            public bool Dispose(WaitHandle notifyObject)
            {
                Dispose();
                return true;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
