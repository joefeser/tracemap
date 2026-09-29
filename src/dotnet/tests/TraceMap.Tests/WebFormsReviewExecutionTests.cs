using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsReviewExecutionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    [Fact]
    public async Task Preflight_status_is_read_only_and_does_not_create_a_lock_or_invent_execution_usage()
    {
        using var fixture = new Fixture(); await fixture.Preflight();
        var before = Directory.GetFiles(fixture.Run, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        using var output = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "status", "--run", fixture.Run, "--json"], output, fixture.Error));
        var status = JsonSerializer.Deserialize<WebFormsReviewStatus>(output.ToString(), JsonOptions)!;
        Assert.Equal("preflight-completed-with-deferred-validation", status.State); Assert.Equal(0, status.CheckpointSequence);
        Assert.False(status.RetainedArtifactsVerified); Assert.Null(status.WorkbenchPath);
        Assert.Equal("local-only", status.Visibility); Assert.Equal("retained-status-not-fresh-source-or-runtime", status.ClaimLevel);
        Assert.Equal(Hash(typeof(WebFormsReviewExecutionCommand).Assembly.Location), status.GeneratorSha256);
        Assert.Equal(64, status.BoundedInputSha256.Length); Assert.All(status.Phases, phase => Assert.Null(phase.WorkUnitsUsed));
        Assert.Equal(fixture.Config.Operation, status.Operation); Assert.Equal(fixture.Config.PageMode, status.PageMode);
        Assert.Equal(fixture.Config.SourceCommitSha, status.SourceCommitSha);
        Assert.Equal(fixture.Config.Budgets.IlMaxBodies ?? 50_000,
            status.Phases.Single(phase => phase.Name == "scan").ConfiguredLimits["ilBodies"]);
        Assert.Contains(status.NextActions, action => action.Arguments.FirstOrDefault() == "run");
        Assert.False(File.Exists(Path.Combine(fixture.Run, ".native-run.lock")));
        foreach (var (path, sha) in before) Assert.Equal(sha, Hash(path));
        Assert.Equal(before.Count, Directory.GetFiles(fixture.Run, "*", SearchOption.AllDirectories).Length);
    }

    [Theory]
    [InlineData("scan-failed")]
    [InlineData("scan-completed")]
    [InlineData("reports-failed")]
    [InlineData("reports-completed")]
    [InlineData("relocated")]
    [InlineData("missing-inputs")]
    public async Task Native_status_reports_actual_retained_state_and_bounded_counts_without_scanning_or_repair(string scenario)
    {
        using var fixture = new Fixture(); fixture.AddPublicEventFixture(); await fixture.Preflight();
        Assert.Equal(scenario == "scan-failed" ? 1 : 0, await fixture.Execute("run", scenario == "scan-failed" ? (_, _, _, _) => Task.FromResult(1) : Scan));
        if (scenario == "reports-failed") Assert.Equal(1, await fixture.ExecuteReports("resume", (_, _, _, _) => throw new InvalidOperationException("public failure")));
        else if (scenario is not ("scan-failed" or "scan-completed")) Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var root = fixture.Run;
        if (scenario == "relocated")
        {
            root = Path.Combine(fixture.Root, "copy");
            Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "relocate", "--run", fixture.Run, "--out", root], TextWriter.Null, fixture.Error));
        }
        if (scenario == "missing-inputs") Directory.Move(fixture.Source, Path.Combine(fixture.Root, "source-preserved-elsewhere"));
        var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        using var output = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "status", "--run", root, "--json"], output, fixture.Error));
        var status = JsonSerializer.Deserialize<WebFormsReviewStatus>(output.ToString(), JsonOptions)!;
        var checkpoint = fixture.LastCheckpoint();
        Assert.Equal(checkpoint.State, status.State); Assert.Equal(checkpoint.Sequence, status.CheckpointSequence);
        Assert.Equal(fixture.Config.Operation, status.Operation); Assert.Equal(fixture.Config.PageMode, status.PageMode);
        Assert.Equal(fixture.Config.SourceCommitSha, status.SourceCommitSha);
        Assert.Equal(scenario != "scan-failed", status.RetainedArtifactsVerified);
        Assert.All(status.Phases, phase => Assert.NotEmpty(phase.UsageGaps));
        Assert.All(status.Phases.Where(phase => phase.Name != "reports"), phase => Assert.Null(phase.WorkUnitsUsed));
        var complete = checkpoint.State == "reports-completed-review-only";
        Assert.Equal(complete, status.WorkbenchPath is not null);
        Assert.Contains(status.NextActions, action => action.Arguments.FirstOrDefault() == (complete ? "query" : "resume"));
        if (complete)
        {
            var reports = status.Phases.Single(phase => phase.Name == "reports");
            Assert.Equal(checkpoint.Reports!.Surfaces, reports.ObservedCounts["surfaces"]);
            Assert.Equal(checkpoint.Reports.CompiledPaths, reports.ObservedCounts["compiledVariants"]);
            Assert.Equal(fixture.Config.Budgets.GraphMaxPaths, reports.ConfiguredLimits["pathsPerGraphQuery"]);
            Assert.Equal(fixture.Config.Budgets.GraphMaxWork, reports.ConfiguredLimits["traversalWorkPerGraphQuery"]);
            Assert.Equal(fixture.Config.Budgets.Reports?.MaxGraphStorageBytes ?? 512L * 1024 * 1024,
                reports.ConfiguredLimits["graphStorageBytes"]);
            Assert.NotNull(reports.WorkUnitsUsed);
            Assert.InRange(reports.WorkUnitsUsed.GetValueOrDefault(), 0, fixture.Config.Budgets.GraphMaxWork * 2);
            Assert.Equal(reports.WorkUnitsUsed, reports.ObservedCounts["compiledTraversalWorkUnits"] + reports.ObservedCounts["pageTraversalWorkUnits"]);
            Assert.Equal("page-and-compiled-graph-query-traversal-all-selected-roots", reports.WorkUnitsScope);
            Assert.Contains("Measured traversal work units: page query", File.ReadAllText(status.WorkbenchPath!), StringComparison.Ordinal);
            Assert.Equal(checkpoint.Reports.Coverage, status.Coverage);
        }
        else Assert.Null(status.Phases.Single(phase => phase.Name == "reports").WorkUnitsUsed);
        if (scenario == "missing-inputs")
        {
            Assert.True(status.InputLocators.Sum(item => item.Missing) > 0);
            Assert.Contains(status.NextActions, action => action.Kind == "restore-input-locators");
        }
        foreach (var (path, sha) in before) Assert.Equal(sha, Hash(path));
        Assert.Equal(before.Count, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        using var text = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "status", "--run", root], text, fixture.Error));
        Assert.Contains("resumeAdmissionPerformed=false", text.ToString(), StringComparison.Ordinal);
        Assert.Contains("workUsage=not-fully-retained", text.ToString(), StringComparison.Ordinal);
        if (complete) Assert.Contains("sharedAcrossSelectedRoots=true", text.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("changed-artifact")]
    [InlineData("busy")]
    public async Task Native_status_refuses_corrupt_or_concurrently_locked_evidence(string scenario)
    {
        using var fixture = new Fixture(); await fixture.Preflight(); Assert.Equal(0, await fixture.Execute("run", Scan));
        var checkpoint = fixture.LastCheckpoint();
        if (scenario == "changed-artifact") File.AppendAllText(Path.Combine(fixture.Run, checkpoint.Artifacts[0].RelativePath), "changed");
        using var held = scenario == "busy" ? new FileStream(Path.Combine(fixture.Run, ".native-run.lock"), FileMode.Open, FileAccess.Read, FileShare.None) : null;
        using var output = new StringWriter();
        Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "status", "--run", fixture.Run, "--json"], output, fixture.Error));
        Assert.Empty(output.ToString()); Assert.Equal(checkpoint.Sequence, fixture.LastCheckpoint().Sequence);
    }

    [Fact]
    public async Task Completed_relocation_preserves_original_checkpoints_artifacts_query_and_resume_without_rewriting()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var checkpoint = fixture.LastCheckpoint();
        var before = Directory.GetFiles(fixture.Run, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(fixture.Run, path), Hash, StringComparer.Ordinal);
        File.WriteAllText(Path.Combine(fixture.Run, "unadmitted-private-marker.txt"), "must remain only in source run");
        var destination = Path.Combine(fixture.Root, "durable-copy");
        Assert.Equal(0, await Native(["relocate", "--run", fixture.Run, "--out", destination]));
        Assert.False(File.Exists(Path.Combine(destination, "unadmitted-private-marker.txt")));
        Assert.True(File.Exists(Path.Combine(fixture.Run, "unadmitted-private-marker.txt")));
        foreach (var (relative, sha) in before)
        {
            Assert.Equal(sha, Hash(Path.Combine(fixture.Run, relative)));
            Assert.Equal(sha, Hash(Path.Combine(destination, relative)));
        }
        var location = JsonSerializer.Deserialize<WebFormsReviewLocation>(File.ReadAllText(Path.Combine(destination,
            WebFormsReviewExecutionCommand.LocationName)), JsonOptions)!;
        Assert.Equal(fixture.Run, location.OriginalPolicyRoot); Assert.Equal(destination, location.CurrentRoot);
        Assert.Equal(Hash(typeof(WebFormsReviewExecutionCommand).Assembly.Location), location.GeneratorSha256);
        Assert.Equal(64, location.BoundedInputSha256.Length); Assert.Equal("local-only", location.Visibility);
        Assert.Equal(0, await Native(["query", "--run", destination, "--pointer", "/coverage", "--depth", "0"]));
        Assert.Equal(0, await WebFormsReviewExecutionCommand.RunWithReportsAsync(["resume", "--run", destination],
            TextWriter.Null, fixture.Error, (_, _, _, _) => throw new InvalidOperationException("must not scan"),
            (_, _, _, _) => throw new InvalidOperationException("must not attach"),
            (_, _, _, _) => throw new InvalidOperationException("must not render")));
        Assert.Equal(checkpoint.Sequence, Directory.GetFiles(Path.Combine(destination, "checkpoints"), "*.json").Length);
        var second = Path.Combine(fixture.Root, "second-copy");
        Assert.Equal(0, await Native(["relocate", "--run", destination, "--out", second]));
        var secondLocation = JsonSerializer.Deserialize<WebFormsReviewLocation>(File.ReadAllText(Path.Combine(second,
            WebFormsReviewExecutionCommand.LocationName)), JsonOptions)!;
        Assert.Equal(fixture.Run, secondLocation.OriginalPolicyRoot);
        Assert.Equal(Hash(Path.Combine(destination, WebFormsReviewExecutionCommand.LocationName)), secondLocation.PreviousLocationSha256);
        // Old locations need not exist for a read-only query; no old-root reads or redirects.
        Directory.Move(fixture.Run, Path.Combine(fixture.Root, "original-preserved-elsewhere"));
        Directory.Move(destination, Path.Combine(fixture.Root, "first-copy-preserved-elsewhere"));
        Assert.Equal(0, await Native(["query", "--run", second, "--pointer", "/coverage", "--depth", "0"]));
        Directory.Move(fixture.Source, Path.Combine(fixture.Root, "source-preserved-elsewhere"));
        Directory.Move(fixture.Published, Path.Combine(fixture.Root, "published-preserved-elsewhere"));
        Assert.Equal(0, await Native(["retention-plan", "--run", second]));
        Assert.Equal(0, await Native(["query", "--run", second, "--pointer", "/coverage", "--depth", "0"]));

        async Task<int> Native(string[] args)
        {
            fixture.Error.GetStringBuilder().Clear();
            return await TraceMapCommand.RunAsync(["webforms-review", .. args], TextWriter.Null, fixture.Error);
        }
    }

    [Fact]
    public async Task Retention_plan_is_hash_bound_protect_only_and_does_not_change_run_or_external_dependencies()
    {
        using var fixture = new Fixture();
        var parent = await fixture.PreflightAttachment();
        Assert.Equal(0, await fixture.Execute("run", (_, _, _, _) => throw new InvalidOperationException("no scan")));
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var before = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        using var output = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "retention-plan", "--run", fixture.Run], output, fixture.Error));
        var plan = JsonSerializer.Deserialize<WebFormsReviewRetentionPlan>(output.ToString(), JsonOptions)!;
        Assert.Equal("dry-run-protect-only", plan.Mode); Assert.Empty(plan.DeletionCandidates);
        Assert.Equal("local-only", plan.Visibility); Assert.Equal(64, plan.BoundedInputSha256.Length);
        Assert.Equal(Hash(typeof(WebFormsReviewExecutionCommand).Assembly.Location), plan.GeneratorSha256);
        Assert.Contains(plan.ProtectedDependencies, item => item.Role == "parent-scan-root" && item.Path == parent);
        Assert.Contains(plan.ProtectedDependencies, item => item.Role == "source-root" && item.Path == fixture.Source);
        Assert.Contains(plan.ProtectedDependencies, item => item.Role == "published-root" && item.Path == fixture.Published);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        foreach (var (path, sha) in before) Assert.Equal(sha, Hash(path));
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("changed-artifact")]
    [InlineData("changed-checkpoint")]
    [InlineData("source-overlap")]
    [InlineData("published-overlap")]
    [InlineData("run-overlap")]
    [InlineData("existing-output")]
    [InlineData("active-lock")]
    public async Task Relocation_refuses_unadmitted_changed_overlapping_or_busy_runs(string scenario)
    {
        using var fixture = new Fixture(); await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        if (scenario != "incomplete") Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var checkpoint = fixture.LastCheckpoint();
        var destination = scenario switch
        {
            "source-overlap" => Path.Combine(fixture.Source, "copy"),
            "published-overlap" => Path.Combine(fixture.Published, "copy"),
            "run-overlap" => Path.Combine(fixture.Run, "copy"),
            _ => Path.Combine(fixture.Root, "copy")
        };
        if (scenario == "changed-artifact") File.AppendAllText(Path.Combine(fixture.Run, checkpoint.Artifacts[0].RelativePath), "changed");
        if (scenario == "changed-checkpoint") File.AppendAllText(Path.Combine(fixture.Run, "checkpoints", "0001.json"), "changed");
        if (scenario == "existing-output") Directory.CreateDirectory(destination);
        using var held = scenario == "active-lock" ? new FileStream(Path.Combine(fixture.Run, ".native-run.lock"), FileMode.Open, FileAccess.Read, FileShare.None) : null;
        using var output = new StringWriter();
        Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "relocate", "--run", fixture.Run, "--out", destination], output, fixture.Error));
        Assert.Empty(output.ToString()); Assert.True(File.Exists(fixture.Manifest));
        Assert.False(File.Exists(Path.Combine(destination, WebFormsReviewExecutionCommand.LocationName)));
    }

    [Theory]
    [InlineData("location-changed")]
    [InlineData("missing-location")]
    [InlineData("copied-again-manually")]
    [InlineData("changed-index")]
    public async Task Relocated_query_refuses_invalid_location_or_owned_evidence(string scenario)
    {
        using var fixture = new Fixture(); await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var destination = Path.Combine(fixture.Root, "copy");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "relocate", "--run", fixture.Run, "--out", destination], TextWriter.Null, fixture.Error));
        var location = Path.Combine(destination, WebFormsReviewExecutionCommand.LocationName);
        if (scenario == "location-changed") File.AppendAllText(location, "changed");
        if (scenario == "missing-location") File.Move(location, Path.Combine(fixture.Root, "preserved-location.json"));
        if (scenario == "copied-again-manually")
        {
            var moved = Path.Combine(fixture.Root, "unregistered-move"); Directory.Move(destination, moved); destination = moved;
        }
        if (scenario == "changed-index") File.AppendAllText(Path.Combine(destination, fixture.LastCheckpoint().Reports!.ReportAttempt, "review-evidence.sqlite"), "changed");
        using var output = new StringWriter();
        Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "query", "--run", destination], output, fixture.Error));
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task Relocation_policy_root_tampering_is_rejected_even_with_recomputed_location_hashes()
    {
        using var fixture = new Fixture(); await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var destination = Path.Combine(fixture.Root, "copy");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "relocate", "--run", fixture.Run, "--out", destination], TextWriter.Null, fixture.Error));
        var path = Path.Combine(destination, WebFormsReviewExecutionCommand.LocationName);
        var location = JsonSerializer.Deserialize<WebFormsReviewLocation>(File.ReadAllText(path), JsonOptions)!;
        location = location with { OriginalPolicyRoot = Path.Combine(fixture.Root, "wrong-original-root") };
        location = location with { BoundedInputSha256 = Digest(new
        {
            location.GeneratorSha256, location.RunId, location.PreflightSha256, location.CheckpointSha256, location.ArtifactsSha256,
            location.OriginalPolicyRoot, location.CurrentRoot, location.CopiedFromRoot, location.PreviousLocationSha256, location.CopiedFiles
        }), PayloadSha256 = "" };
        location = location with { PayloadSha256 = Digest(location) };
        File.WriteAllText(path, JsonSerializer.Serialize(location, JsonOptions));
        using var output = new StringWriter();
        Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "query", "--run", destination], output, fixture.Error));
        Assert.Empty(output.ToString());
        Assert.Contains("CHECKPOINT_INVALID", fixture.Error.ToString(), StringComparison.Ordinal);

        static string Digest<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions)));
    }

    [Theory]
    [InlineData("scan")]
    [InlineData("reports")]
    public async Task Relocated_history_never_uses_original_policy_locators_to_bypass_current_owned_links(string phase)
    {
        if (OperatingSystem.IsWindows()) return; // Native Windows junction acceptance remains required.
        using var fixture = new Fixture(); await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var destination = Path.Combine(fixture.Root, "copy");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "relocate", "--run", fixture.Run, "--out", destination], TextWriter.Null, fixture.Error));
        var checkpoint = fixture.LastCheckpoint();
        var directory = Path.Combine(destination, phase == "scan" ? checkpoint.Attempt + "/scan" : checkpoint.Reports!.ReportAttempt);
        var preserved = Path.Combine(fixture.Root, "unowned-" + phase);
        Directory.Move(directory, preserved); Directory.CreateSymbolicLink(directory, preserved);
        using var output = new StringWriter();
        Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "query", "--run", destination], output, fixture.Error));
        Assert.Empty(output.ToString()); Assert.Contains("OUTPUT_LINK_INVALID", fixture.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelled_relocation_does_not_create_a_completed_copy_or_delete_source_proof()
    {
        using var fixture = new Fixture(); await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var checkpointHash = Hash(Path.Combine(fixture.Run, "checkpoints", "0004.json"));
        var destination = Path.Combine(fixture.Root, "cancelled-copy");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TraceMapCommand.RunAsync(
            ["webforms-review", "relocate", "--run", fixture.Run, "--out", destination], TextWriter.Null, fixture.Error, cancellation.Token));
        Assert.False(Directory.Exists(destination));
        Assert.Equal(checkpointHash, Hash(Path.Combine(fixture.Run, "checkpoints", "0004.json")));
    }

    [Fact]
    public async Task Fresh_executes_one_normal_scan_and_resume_keeps_retained_evidence_unchanged()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        var preflightHash = Hash(fixture.Manifest);
        var called = 0;
        Assert.Equal(0, await fixture.Execute("run", async (args, output, error, token) =>
        {
            called++;
            return await Scan(args, output, error, token);
        }));
        Assert.Equal(1, called);
        var checkpoint = fixture.LastCheckpoint();
        Assert.Equal("scan-completed-reports-pending", checkpoint.State);
        Assert.Equal("review-only-static-not-runtime", checkpoint.ClaimLevel);
        Assert.True(checkpoint.FactCount > 0);
        Assert.Equal(64, checkpoint.SourceSnapshotDigest!.Length);
        Assert.Contains("UnifiedReportsPending", checkpoint.Gaps);
        Assert.Equal(Hash(typeof(WebFormsReviewExecutionCommand).Assembly.Location), checkpoint.GeneratorSha256);
        Assert.Equal(preflightHash, checkpoint.PreflightSha256);
        Assert.Equal(2, checkpoint.Sequence);
        Assert.Equal(Hash(Path.Combine(fixture.Run, "checkpoints", "0001.json")), checkpoint.PreviousCheckpointSha256);
        Assert.Equal(preflightHash, Hash(fixture.Manifest));
        var hashes = checkpoint.Artifacts.ToDictionary(item => item.RelativePath, item => Hash(Path.Combine(fixture.Run, item.RelativePath)));
        var scan = Path.Combine(fixture.Run, checkpoint.Attempt, "scan");
        var manifest = JsonSerializer.Deserialize<ScanManifest>(File.ReadAllText(Path.Combine(scan, "scan-manifest.json")), JsonOptions)!;
        Assert.NotNull(manifest.CompiledInputProvenance);
        Assert.NotNull(manifest.IlBodyProvenance);
        Assert.Contains(checkpoint.Artifacts, item => item.RelativePath.EndsWith("/" + SourceSnapshotRetention.ManifestName, StringComparison.Ordinal));
        Assert.Contains(checkpoint.Artifacts, item => item.RelativePath.EndsWith("/" + SourceSnapshotRetention.RosterName, StringComparison.Ordinal));
        Assert.Empty(manifest.Projects);
        var facts = File.ReadLines(Path.Combine(scan, "facts.ndjson")).Select(line => JsonSerializer.Deserialize<CodeFact>(line, JsonOptions)!).ToArray();
        Assert.DoesNotContain(facts, fact => fact.Evidence.FilePath.StartsWith("Unselected/", StringComparison.Ordinal));
        Assert.DoesNotContain(facts, fact => fact.Evidence.FilePath.EndsWith("Ignored.vbproj", StringComparison.Ordinal));
        Assert.Contains(facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared);
        Assert.Contains(facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared);
        // Resume is retained-evidence validation, not a scan of current source.
        File.AppendAllText(Path.Combine(fixture.Source, "Pages", "Lookup.aspx.vb"), "' changed current source\n");
        Assert.Equal(0, await fixture.Execute("resume", (_, _, _, _) => throw new InvalidOperationException("must not rescan")));
        Assert.Contains("retainedSnapshot=true;sourceRescanned=false", fixture.Output.ToString(), StringComparison.Ordinal);
        foreach (var item in hashes) Assert.Equal(item.Value, Hash(Path.Combine(fixture.Run, item.Key)));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(fixture.Run, "checkpoints"), "*.json").Length);
        Assert.Equal(preflightHash, Hash(fixture.Manifest));
        Assert.Equal(1, await fixture.Execute("run", Scan));
        Assert.Contains("RUN_ALREADY_STARTED_USE_RESUME", fixture.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failed_attempt_is_retained_and_resume_uses_a_fresh_owned_attempt()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        Assert.Equal(1, await fixture.Execute("run", (args, _, _, _) =>
        {
            var path = args[Array.IndexOf(args, "--out") + 1];
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "incomplete.txt"), "public incomplete output");
            return Task.FromResult(1);
        }));
        var failed = fixture.LastCheckpoint();
        Assert.Equal("scan-failed", failed.State);
        Assert.Empty(failed.Artifacts);
        Assert.Null(failed.ScanId);
        var retained = Path.Combine(fixture.Run, failed.Attempt, "scan", "incomplete.txt");
        var before = Hash(retained);
        Assert.Equal(0, await fixture.Execute("resume", Scan));
        var completed = fixture.LastCheckpoint();
        Assert.NotEqual(failed.Attempt, completed.Attempt);
        Assert.Equal(4, completed.Sequence);
        Assert.Equal(before, Hash(retained));
    }

    [Fact]
    public async Task Cancelled_attempt_records_a_resumable_checkpoint_without_completion_claim()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Execute("run", (_, _, _, _) =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }, cancellation.Token));
        Assert.Equal("scan-cancelled", fixture.LastCheckpoint().State);
        Assert.Equal(0, await fixture.Execute("resume", Scan));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("sidecar")]
    public async Task Resume_rejects_modified_missing_extra_or_active_sidecar_artifacts(string kind)
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        var scan = Path.Combine(fixture.Run, fixture.LastCheckpoint().Attempt, "scan");
        if (kind == "changed") File.AppendAllText(Path.Combine(scan, "report.md"), "changed");
        if (kind == "missing") File.Delete(Path.Combine(scan, "facts.ndjson"));
        if (kind == "extra") File.WriteAllText(Path.Combine(scan, "extra.txt"), "extra");
        if (kind == "sidecar") File.WriteAllText(Path.Combine(scan, "index.sqlite-wal"), "active");
        Assert.Equal(1, await fixture.Execute("resume", (_, _, _, _) => throw new InvalidOperationException("must not rescan")));
        Assert.Equal(2, fixture.LastCheckpoint().Sequence);
        Assert.DoesNotContain("webFormsExecution=scan-completed", fixture.Output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("generator")]
    [InlineData("budget")]
    [InlineData("sequence")]
    [InlineData("bounded")]
    [InlineData("preflight")]
    [InlineData("state")]
    [InlineData("gaps")]
    public async Task Tool_config_and_checkpoint_tampering_fail_before_execution(string kind)
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        if (kind is "sequence" or "bounded" or "preflight" or "state" or "gaps")
        {
            Assert.Equal(1, await fixture.Execute("run", (_, _, _, _) => Task.FromResult(1)));
            var path = Path.Combine(fixture.Run, "checkpoints", "0002.json");
            var value = fixture.LastCheckpoint();
            value = kind switch
            {
                "sequence" => value with { PreviousCheckpointSha256 = new string('a', 64) },
                "bounded" => value with { BoundedInputSha256 = new string('a', 64) },
                "state" => value with { State = "scan-cancelled" },
                "gaps" => value with { Gaps = [] },
                _ => value with { PreflightSha256 = new string('a', 64) }
            };
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
        }
        else
        {
            var plan = fixture.Plan();
            plan = kind == "generator" ? plan with { GeneratorSha256 = new string('a', 64) }
                : plan with { Configuration = plan.Configuration with { Budgets = plan.Configuration.Budgets with { IlMaxWork = 1 } } };
            File.WriteAllText(fixture.Manifest, JsonSerializer.Serialize(plan, JsonOptions));
        }
        Assert.Equal(1, await fixture.Execute(kind is "generator" or "budget" ? "run" : "resume",
            (_, _, _, _) => throw new InvalidOperationException("must not execute")));
        Assert.DoesNotContain("webFormsExecution=scan-completed", fixture.Output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Attach_produces_a_new_compiled_only_index_and_resume_preserves_parent_and_derived_bytes(bool retainedRoster)
    {
        using var fixture = new Fixture();
        var parent = Path.Combine(fixture.Root, "parent");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", fixture.Source, "--out", parent,
            "--include", "Pages/**", "--exclude", "**/*.vbproj", .. retainedRoster ? new[] { "--retain-source-snapshot" } : []], TextWriter.Null, TextWriter.Null));
        fixture.Config = fixture.Config with { Operation = "attach", ParentScanRoot = parent,
            Budgets = fixture.Config.Budgets with { IlMaxBodies = retainedRoster ? 250_000 : null } };
        await fixture.Preflight();
        var before = Directory.GetFiles(parent, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        Assert.Equal(0, await fixture.Execute("run", (_, _, _, _) => throw new InvalidOperationException("must not source scan")));
        var checkpoint = fixture.LastCheckpoint();
        Assert.Equal("scan-completed-reports-pending", checkpoint.State);
        Assert.Contains("CrossIndexParentJoinsPending", checkpoint.Gaps);
        var derivedRoot = Path.Combine(fixture.Run, checkpoint.Attempt, "scan");
        var derived = JsonSerializer.Deserialize<ScanManifest>(File.ReadAllText(Path.Combine(derivedRoot, "scan-manifest.json")), JsonOptions)!;
        CompiledAttachmentProducer.ValidateContext(derived);
        Assert.Equal(retainedRoster ? 250_000 : 50_000, derived.IlBodyProvenance!.EffectiveLimits.MaxBodyCount);
        Assert.Equal(Hash(Path.Combine(parent, "index.sqlite")), derived.CompiledAttachment!.ParentIndexSha256);
        Assert.Equal(Hash(Path.Combine(parent, "scan-manifest.json")), derived.CompiledAttachment.ParentManifestSha256);
        Assert.Equal("NotRun", derived.BuildStatus);
        Assert.NotEqual(fixture.Plan().ParentScanId, derived.ScanId);
        var facts = File.ReadLines(Path.Combine(derivedRoot, "facts.ndjson")).Select(line => JsonSerializer.Deserialize<CodeFact>(line, JsonOptions)!).ToArray();
        Assert.Contains(facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared);
        Assert.DoesNotContain(facts, fact => fact.FactType is FactTypes.FileInventoried or FactTypes.MethodDeclared or FactTypes.CallEdge);
        Assert.False(File.Exists(Path.Combine(derivedRoot, SourceSnapshotRetention.RosterName)));
        var report = File.ReadAllText(Path.Combine(derivedRoot, "report.md"));
        Assert.Contains(derived.CompiledAttachment.ParentIndexSha256, report, StringComparison.Ordinal);
        Assert.Contains("Source analysis and builds were not rerun", report, StringComparison.Ordinal);
        var derivedHashes = Directory.GetFiles(derivedRoot, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        Assert.Equal(0, await fixture.Execute("resume", (_, _, _, _) => throw new InvalidOperationException("must not source scan")));
        Assert.Equal(2, fixture.LastCheckpoint().Sequence);
        foreach (var pair in derivedHashes) Assert.Equal(pair.Value, Hash(pair.Key));
        Assert.Equal(before.Count, Directory.GetFiles(parent, "*", SearchOption.AllDirectories).Length);
        foreach (var pair in before) Assert.Equal(pair.Value, Hash(pair.Key));
        File.AppendAllText(Path.Combine(fixture.Source, "Pages", "Lookup.aspx.vb"), "' changed retained source\n");
        Assert.Equal(1, await fixture.Execute("resume", (_, _, _, _) => throw new InvalidOperationException("must not source scan")));
        Assert.DoesNotContain("webFormsExecution=scan-completed", fixture.Output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_or_cancelled_attachment_keeps_attempt_bytes_and_resumes_without_a_source_scan(bool cancelled)
    {
        using var fixture = new Fixture();
        var parent = await fixture.PreflightAttachment();
        var hashes = Directory.GetFiles(parent, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        using var cancellation = new CancellationTokenSource();
        Task FailAttempt(WebFormsReviewPreflightManifest _, ScanManifest __, string output, CancellationToken ___)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "incomplete.txt"), "retained public failed attachment bytes");
            if (cancelled) { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
            throw new InvalidOperationException("public fixture failure");
        }
        var run = fixture.ExecuteAttachment("run", FailAttempt, cancellation.Token);
        if (cancelled) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        else Assert.Equal(1, await run);
        var failed = fixture.LastCheckpoint();
        Assert.Equal(cancelled ? "scan-cancelled" : "scan-failed", failed.State);
        Assert.Empty(failed.Artifacts);
        var marker = Path.Combine(fixture.Run, failed.Attempt, "scan", "incomplete.txt");
        var markerHash = Hash(marker);
        Assert.Equal(0, await fixture.Execute("resume", (_, _, _, _) => throw new InvalidOperationException("must not source scan")));
        Assert.Equal(4, fixture.LastCheckpoint().Sequence);
        Assert.NotEqual(failed.Attempt, fixture.LastCheckpoint().Attempt);
        Assert.Equal(markerHash, Hash(marker));
        foreach (var pair in hashes) Assert.Equal(pair.Value, Hash(pair.Key));
    }

    [Theory]
    [InlineData("parent-report")]
    [InlineData("parent-sidecar")]
    [InlineData("source")]
    [InlineData("derived")]
    public async Task Post_extraction_parent_source_and_derived_tampering_cannot_admit_completion(string mutation)
    {
        using var fixture = new Fixture();
        var parent = await fixture.PreflightAttachment();
        Assert.Equal(1, await fixture.ExecuteAttachment("run", async (plan, manifest, output, token) =>
        {
            await WebFormsReviewAttachmentExecution.WriteAsync(plan, manifest, output, token);
            if (mutation == "parent-report") File.AppendAllText(Path.Combine(parent, "report.md"), "changed public fixture");
            if (mutation == "parent-sidecar") File.WriteAllText(Path.Combine(parent, "index.sqlite-wal"), "active public fixture");
            if (mutation == "source") File.AppendAllText(Path.Combine(fixture.Source, "Pages", "Lookup.aspx.vb"), "' changed public fixture\n");
            if (mutation == "derived")
            {
                var path = Path.Combine(output, "scan-manifest.json");
                var derived = JsonSerializer.Deserialize<ScanManifest>(File.ReadAllText(path), JsonOptions)!;
                File.WriteAllText(path, JsonSerializer.Serialize(derived with
                { CompiledAttachment = derived.CompiledAttachment! with { ParentIndexSha256 = new string('a', 64) } }, JsonOptions));
            }
        }));
        Assert.Equal("scan-failed", fixture.LastCheckpoint().State);
        Assert.Null(fixture.LastCheckpoint().ScanId);
        Assert.DoesNotContain("webFormsExecution=scan-completed", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Root, fixture.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("parent")]
    [InlineData("output")]
    [InlineData("extra")]
    public async Task Attachment_resume_rejects_changed_parent_or_derived_artifacts_without_reextracting(string mutation)
    {
        using var fixture = new Fixture();
        var parent = await fixture.PreflightAttachment();
        Assert.Equal(0, await fixture.Execute("run", (_, _, _, _) => throw new InvalidOperationException("must not source scan")));
        var output = Path.Combine(fixture.Run, fixture.LastCheckpoint().Attempt, "scan");
        if (mutation == "parent") File.AppendAllText(Path.Combine(parent, "report.md"), "changed public fixture");
        if (mutation == "output") File.AppendAllText(Path.Combine(output, "report.md"), "changed public fixture");
        if (mutation == "extra") File.WriteAllText(Path.Combine(output, "extra.json"), "{}");
        Assert.Equal(1, await fixture.ExecuteAttachment("resume", (_, _, _, _) => throw new InvalidOperationException("must not reextract")));
        Assert.Equal(2, fixture.LastCheckpoint().Sequence);
        Assert.DoesNotContain("webFormsExecution=scan-completed", fixture.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrent_executor_cannot_start_a_second_scan()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = fixture.Execute("run", async (_, _, _, _) => { started.SetResult(); await release.Task; return 1; });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(20));
        try
        {
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.Equal(1, await WebFormsReviewExecutionCommand.RunAsync(["run", "--run", fixture.Run], output, error,
                (_, _, _, _) => throw new InvalidOperationException("must not execute")));
        }
        finally { release.SetResult(); }
        Assert.Equal(1, await first);
        Assert.Equal(2, fixture.LastCheckpoint().Sequence);
    }

    [Fact]
    public async Task Actual_CLI_dispatch_executes_and_resumes_the_fresh_scan()
    {
        using var fixture = new Fixture();
        fixture.AddPublicEventFixture();
        await fixture.Preflight();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "run", "--run", fixture.Run], fixture.Output, fixture.Error));
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "resume", "--run", fixture.Run], fixture.Output, fixture.Error));
        Assert.Equal("reports-completed-review-only", fixture.LastCheckpoint().State);
        Assert.Equal(4, fixture.LastCheckpoint().Sequence);
        var checkpoint = fixture.LastCheckpoint();
        var report = Path.Combine(fixture.Run, checkpoint.Reports!.ReportAttempt);
        Assert.True(File.Exists(Path.Combine(report, "index.html")));
        Assert.True(File.Exists(Path.Combine(report, "handoff.local.json")));
        Assert.True(File.Exists(Path.Combine(report, "compiled", "compiled-paths.handoff.local.json")));
        Assert.DoesNotContain("UnifiedReportsPending", checkpoint.Gaps);
        Assert.Equal(Hash(Path.Combine(fixture.Run, "checkpoints", "0002.json")), checkpoint.Reports.ScanCheckpointSha256);
        Assert.Contains("sourceRescanned=false", fixture.Output.ToString(), StringComparison.Ordinal);
        var handoff = JsonSerializer.Deserialize<NativeWebFormsReviewHandoff>(File.ReadAllText(Path.Combine(report, "handoff.local.json")), JsonOptions)!;
        Assert.NotEmpty(handoff.Packet.EventChains);
        Assert.True(handoff.RequestedCompiledRoots > 0);
        Assert.Equal(handoff.PacketSha256, WebFormsReviewReportExecution.CanonicalHash(handoff.Packet, 268_435_456, CancellationToken.None));
        Assert.Equal("review-evidence.sqlite", handoff.EvidenceIndexRelativePath);
        Assert.Contains(checkpoint.Artifacts, artifact => artifact.RelativePath.EndsWith("/review-evidence.sqlite", StringComparison.Ordinal));
        fixture.Output.GetStringBuilder().Clear();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "query", "--run", fixture.Run,
            "--pointer", "/packet/eventChains/0", "--depth", "2", "--limit", "50"], fixture.Output, fixture.Error));
        var slice = JsonSerializer.Deserialize<WebFormsEvidenceResponse>(fixture.Output.ToString(), JsonOptions)!;
        Assert.Equal("webforms-review-evidence-slice.v1", slice.SchemaVersion);
        Assert.Equal("Lookup.Names_Init(Object,System.EventArgs)", slice.Result.Children.Single(item => item.Pointer.EndsWith("/handlerSymbol", StringComparison.Ordinal)).Value!.Value.GetString());
        Assert.Equal(Hash(typeof(WebFormsReviewExecutionCommand).Assembly.Location), slice.GeneratorSha256);
        Assert.Equal(Hash(Path.Combine(report, "review-evidence.sqlite")), slice.IndexSha256);
        Assert.Equal(4, fixture.LastCheckpoint().Sequence);
    }

    [Theory]
    [InlineData("changed-index")]
    [InlineData("sidecar")]
    [InlineData("external-inputs-unavailable")]
    public async Task Query_only_consumes_the_checkpointed_index_and_never_repairs_it(string scenario)
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var checkpoint = fixture.LastCheckpoint();
        var index = Path.Combine(fixture.Run, checkpoint.Reports!.ReportAttempt, "review-evidence.sqlite");
        var hash = Hash(index);
        if (scenario == "changed-index") File.AppendAllText(index, "changed retained bytes");
        if (scenario == "sidecar") File.WriteAllText(index + "-wal", "unadmitted sidecar");
        if (scenario == "external-inputs-unavailable")
        {
            Directory.Move(fixture.Source, Path.Combine(fixture.Root, "unavailable-source"));
            Directory.Move(fixture.Published, Path.Combine(fixture.Root, "unavailable-published"));
        }
        fixture.Output.GetStringBuilder().Clear(); fixture.Error.GetStringBuilder().Clear();
        var exit = await TraceMapCommand.RunAsync(["webforms-review", "query", "--run", fixture.Run, "--pointer", "/coverage", "--depth", "0"], fixture.Output, fixture.Error);
        Assert.Equal(scenario == "external-inputs-unavailable" ? 0 : 1, exit);
        Assert.Equal(checkpoint.Sequence, fixture.LastCheckpoint().Sequence);
        if (scenario != "changed-index") Assert.Equal(hash, Hash(index));
        if (exit != 0) Assert.Empty(fixture.Output.ToString());
        else Assert.Contains("External source, DLLs and other artifacts were not revalidated", fixture.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failed_report_keeps_scan_and_partial_bytes_and_resumes_into_fresh_report_attempt()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        var scan = fixture.LastCheckpoint();
        string? marker = null;
        Assert.Equal(1, await fixture.ExecuteReports("resume", (_, _, path, _) =>
        {
            Directory.CreateDirectory(path);
            marker = Path.Combine(path, "partial.txt"); File.WriteAllText(marker, "retained unadmitted partial bytes");
            throw new InvalidOperationException("public simulated report failure");
        }));
        var failed = fixture.LastCheckpoint();
        Assert.Equal("reports-failed", failed.State);
        Assert.Equal(scan.Artifacts, failed.Artifacts);
        var markerHash = Hash(marker!);
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var complete = fixture.LastCheckpoint();
        Assert.Equal("reports-completed-review-only", complete.State);
        Assert.Equal(6, complete.Sequence);
        Assert.Equal(scan.Attempt, complete.Attempt);
        Assert.NotEqual(failed.Reports!.ReportAttempt, complete.Reports!.ReportAttempt);
        Assert.Equal(markerHash, Hash(marker!));
        foreach (var artifact in scan.Artifacts) Assert.Equal(artifact.Sha256, Hash(Path.Combine(fixture.Run, artifact.RelativePath)));
    }

    [Fact]
    public async Task Cancelled_report_is_recorded_without_rescanning_on_resume()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.ExecuteReports("resume", (_, _, _, _) =>
        {
            cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token);
        }, cancellation.Token));
        Assert.Equal("reports-cancelled", fixture.LastCheckpoint().State);
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        Assert.Equal("reports-completed-review-only", fixture.LastCheckpoint().State);
    }

    [Theory]
    [InlineData("before-admission")]
    [InlineData("completed-resume")]
    public async Task Changed_report_bytes_refuse_admission_or_completed_resume(string when)
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        var result = await fixture.ExecuteReports("resume", async (plan, scan, path, token) =>
        {
            var produced = await WebFormsReviewReportExecution.WriteAsync(plan, scan, path, token);
            if (when == "before-admission") File.AppendAllText(Path.Combine(path, "index.html"), "changed");
            return produced;
        });
        if (when == "before-admission")
        {
            Assert.Equal(1, result);
            Assert.Equal("reports-failed", fixture.LastCheckpoint().State);
            Assert.Contains("REPORT_OUTPUT_CHANGED", fixture.Error.ToString(), StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(0, result);
            var complete = fixture.LastCheckpoint();
            File.AppendAllText(Path.Combine(fixture.Run, complete.Reports!.ReportAttempt, "handoff.local.json"), "changed");
            Assert.Equal(1, await fixture.ExecuteReports("resume", (_, _, _, _) => throw new InvalidOperationException("must not regenerate")));
            Assert.Contains("OUTPUT_CHANGED", fixture.Error.ToString(), StringComparison.Ordinal);
            Assert.Equal(complete.Sequence, fixture.LastCheckpoint().Sequence);
        }
    }

    [Fact]
    public async Task Native_attachment_reports_pin_parent_and_generated_view_without_source_scan()
    {
        using var fixture = new Fixture();
        var parent = await fixture.PreflightAttachment();
        var parentHash = Hash(Path.Combine(parent, "index.sqlite"));
        Assert.Equal(0, await fixture.Execute("run", (_, _, _, _) => throw new InvalidOperationException("must not source scan")));
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var checkpoint = fixture.LastCheckpoint();
        Assert.Equal("reports-completed-review-only", checkpoint.State);
        Assert.Equal(parentHash, Hash(Path.Combine(parent, "index.sqlite")));
        var handoff = JsonSerializer.Deserialize<NativeWebFormsReviewHandoff>(File.ReadAllText(Path.Combine(fixture.Run,
            checkpoint.Reports!.ReportAttempt, "handoff.local.json")), JsonOptions)!;
        Assert.Equal(2, handoff.ScanManifests.Count);
        Assert.Equal("review-only-static-not-runtime", handoff.ClaimLevel);
        Assert.Equal(checkpoint.Reports.ResultBoundedInputSha256, handoff.BoundedInputSha256);
        Assert.Equal(Hash(typeof(WebFormsReviewExecutionCommand).Assembly.Location), handoff.GeneratorSha256);
        Assert.Equal(0, await fixture.ExecuteReports("resume", (_, _, _, _) => throw new InvalidOperationException("must not rerender")));
        Assert.Equal(checkpoint.Sequence, fixture.LastCheckpoint().Sequence);
    }

    [Fact]
    public async Task Report_policy_tampering_is_rejected_even_with_a_recomputed_payload_hash()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        Assert.Equal(0, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        var checkpoint = fixture.LastCheckpoint();
        var changed = checkpoint with { Reports = checkpoint.Reports! with { PolicySha256 = new string('a', 64) }, CheckpointPayloadSha256 = "" };
        changed = changed with { CheckpointPayloadSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(changed, JsonOptions))) };
        File.WriteAllText(Path.Combine(fixture.Run, "checkpoints", "0004.json"), JsonSerializer.Serialize(changed, JsonOptions));
        Assert.Equal(1, await fixture.ExecuteReports("resume", (_, _, _, _) => throw new InvalidOperationException("must not execute")));
        Assert.Contains("REPORT_CONTEXT_INVALID", fixture.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Report_output_limit_keeps_scan_completed_and_partial_report_unadmitted()
    {
        using var fixture = new Fixture();
        fixture.Config = fixture.Config with { Budgets = fixture.Config.Budgets with { Reports = new(MaxOutputBytes: 1) } };
        await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", Scan));
        var scan = fixture.LastCheckpoint();
        Assert.Equal(1, await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync));
        Assert.Equal("reports-failed", fixture.LastCheckpoint().State);
        Assert.Equal(scan.Artifacts, fixture.LastCheckpoint().Artifacts);
        Assert.True(File.Exists(Path.Combine(fixture.Run, fixture.LastCheckpoint().Reports!.ReportAttempt, "combined.sqlite")));
        Assert.Contains("WEBFORMS_NATIVE_REPORT_OUTPUT_LIMIT", fixture.LastCheckpoint().Gaps);
        Assert.Contains("WEBFORMS_NATIVE_REPORT_OUTPUT_LIMIT", fixture.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Changed_input_during_scan_is_not_published_as_completed_evidence()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        Assert.Equal(1, await fixture.Execute("run", async (args, output, error, token) =>
        {
            var result = await Scan(args, output, error, token);
            File.AppendAllText(Path.Combine(fixture.Published, "Public.dll"), "changed-after-scan");
            return result;
        }));
        Assert.Equal("scan-failed", fixture.LastCheckpoint().State);
        Assert.Empty(fixture.LastCheckpoint().Artifacts);
        Assert.DoesNotContain("webFormsExecution=scan-completed", fixture.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_produced_index_is_categorical_and_retained_but_not_admitted()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        Assert.Equal(1, await fixture.Execute("run", async (args, output, error, token) =>
        {
            var result = await Scan(args, output, error, token);
            var scan = args[Array.IndexOf(args, "--out") + 1];
            File.WriteAllText(Path.Combine(scan, "index.sqlite"), "not a SQLite file");
            return result;
        }));
        Assert.Equal("scan-failed", fixture.LastCheckpoint().State);
        Assert.Contains("WEBFORMS_EXECUTION_INPUT_OUTPUT_OR_CHECKPOINT_INVALID", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Source, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Run, fixture.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_digest_pins_dependency_bytes_without_paths_or_timestamp_discovery()
    {
        using var fixture = new Fixture();
        var runtime = Path.Combine(fixture.Root, "runtime");
        Directory.CreateDirectory(runtime);
        File.WriteAllText(Path.Combine(runtime, "Public.Core.dll"), "public tool dependency bytes");
        File.WriteAllText(Path.Combine(runtime, "tool.deps.json"), "{}");
        var original = await WebFormsReviewExecutionCommand.RuntimeDigestAsync(runtime, CancellationToken.None);
        File.SetLastWriteTimeUtc(Path.Combine(runtime, "Public.Core.dll"), DateTime.UtcNow.AddDays(-1));
        Assert.Equal(original, await WebFormsReviewExecutionCommand.RuntimeDigestAsync(runtime, CancellationToken.None));
        File.AppendAllText(Path.Combine(runtime, "Public.Core.dll"), "changed");
        Assert.NotEqual(original, await WebFormsReviewExecutionCommand.RuntimeDigestAsync(runtime, CancellationToken.None));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WebFormsReviewExecutionCommand.RuntimeDigestAsync(runtime, new CancellationToken(true)));
    }

    [Theory]
    [InlineData("projects")]
    [InlineData("solution")]
    public async Task Explicit_project_or_solution_outside_source_folder_is_admitted_as_a_selected_input(string mode)
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        var plan = fixture.Plan();
        var relative = mode == "projects" ? "Public.vbproj" : "Public.sln";
        plan = plan with { Configuration = plan.Configuration with
        { ProjectMode = mode, ProjectRelativePaths = mode == "projects" ? [relative] : [], SolutionRelativePath = mode == "solution" ? relative : null } };
        var args = WebFormsReviewExecutionCommand.ScanArguments(plan, "public-output");
        Assert.Contains(Enumerable.Range(0, args.Length - 1), index => args[index] == "--include" && args[index + 1] == relative);
        Assert.Contains(Enumerable.Range(0, args.Length - 1), index => args[index] == (mode == "projects" ? "--project" : "--solution") && args[index + 1] == relative);
        Assert.DoesNotContain("--restore", args);
    }

    [Fact]
    public async Task Literal_source_folder_with_glob_character_cannot_expand_scope()
    {
        using var fixture = new Fixture();
        await fixture.Preflight();
        var plan = fixture.Plan();
        plan = plan with { Configuration = plan.Configuration with { SourceFolders = ["Pages*"] } };
        var exception = Assert.ThrowsAny<Exception>(() => WebFormsReviewExecutionCommand.ScanArguments(plan, "public-output"));
        Assert.Equal("WEBFORMS_EXECUTION_SOURCE_SCOPE_GLOB_UNSUPPORTED", exception.Message);
    }

    [Fact]
    public async Task Native_receipt_references_original_published_files_and_pins_its_source_membership()
    {
        using var fixture = new Fixture();
        fixture.WritePublishReceipt();
        var publishedBefore = Directory.GetFiles(fixture.Published, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        await fixture.Preflight();
        var plan = fixture.Plan();
        Assert.Contains(plan.Inputs, input => input.Role == "publish-receipt");
        Assert.Contains(plan.Inputs, input => input.Role == "publish-source" && input.Path.EndsWith("Lookup.aspx.vb", StringComparison.Ordinal));
        Assert.Equal(1, plan.Inputs.Count(input => input.Path.EndsWith("Lookup.aspx", StringComparison.Ordinal)));
        Assert.Equal(0, await fixture.Execute("run", Scan));
        var checkpoint = fixture.LastCheckpoint();
        Assert.DoesNotContain("PublishMapExecutionPending", checkpoint.Gaps);
        Assert.Contains("BuildAuthenticityNotEstablished", checkpoint.Gaps);
        var scanPath = Path.Combine(fixture.Run, checkpoint.Attempt, "scan");
        var manifest = JsonSerializer.Deserialize<ScanManifest>(File.ReadAllText(Path.Combine(scanPath, "scan-manifest.json")), JsonOptions)!;
        Assert.Equal("bound", manifest.WebFormsPublishProvenance!.Status);
        Assert.NotNull(manifest.WebFormsPublishProvenance.PublishedRootPathHash);
        var facts = File.ReadLines(Path.Combine(scanPath, "facts.ndjson")).Select(line => JsonSerializer.Deserialize<CodeFact>(line, JsonOptions)!).ToArray();
        Assert.Single(facts, fact => fact.FactType == FactTypes.WebFormsPublishPageMapped);
        Assert.DoesNotContain(checkpoint.Artifacts, item => item.RelativePath.EndsWith(".dll", StringComparison.Ordinal));
        foreach (var item in publishedBefore) Assert.Equal(item.Value, Hash(item.Key));
    }

    [Theory]
    [InlineData("source-hash")]
    [InlineData("undeclared-dll")]
    [InlineData("page")]
    [InlineData("duplicate")]
    [InlineData("escape")]
    [InlineData("commit")]
    public async Task Invalid_publish_receipt_cannot_start_a_native_run(string mutation)
    {
        using var fixture = new Fixture();
        fixture.WritePublishReceipt(mutation);
        File.WriteAllText(fixture.ConfigPath, JsonSerializer.Serialize(fixture.Config, JsonOptions));
        Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "preflight", "--config", fixture.ConfigPath, "--out", fixture.Run], fixture.Output, fixture.Error));
        Assert.False(Directory.Exists(fixture.Run));
        Assert.DoesNotContain(fixture.Source, fixture.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("changed")]
    [InlineData("missing")]
    public async Task External_receipts_are_used_in_place_and_resume_rechecks_them(string mutation)
    {
        using var fixture = new Fixture();
        fixture.WritePublishReceipt();
        var evidence = Path.Combine(fixture.Root, "evidence");
        Directory.CreateDirectory(evidence);
        var receipt = Path.Combine(evidence, "publish.json");
        File.Move(Path.Combine(fixture.Published, "receipts", "publish.json"), receipt);
        var binding = Path.Combine(evidence, "binding.json");
        File.WriteAllText(binding, "{\"schemaVersion\":\"compiled-input-binding-set.v1\",\"bindings\":[]}");
        fixture.Config = fixture.Config with { ReceiptRoot = evidence, PublishReceiptRelativePath = "publish.json", BindingReceipts = ["binding.json"] };
        var publishedBefore = Directory.GetFiles(fixture.Published, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        var receiptBefore = Hash(receipt);
        var bindingBefore = Hash(binding);
        await fixture.Preflight();
        Assert.Equal(0, await fixture.Execute("run", async (args, output, error, token) =>
        {
            Assert.Equal(receipt, args[Array.IndexOf(args, "--webforms-publish-receipt") + 1]);
            Assert.Equal(binding, args[Array.IndexOf(args, "--compiled-binding-receipt") + 1]);
            Assert.Equal(fixture.Published, args[Array.IndexOf(args, "--webforms-published-root") + 1]);
            return await Scan(args, output, error, token);
        }));
        var checkpoint = fixture.LastCheckpoint();
        Assert.DoesNotContain("PublishMapExecutionPending", checkpoint.Gaps);
        Assert.DoesNotContain("PublishReceiptCoverageReduced", checkpoint.Gaps);
        Assert.Equal(receiptBefore, Hash(receipt));
        Assert.Equal(bindingBefore, Hash(binding));
        foreach (var pair in publishedBefore) Assert.Equal(pair.Value, Hash(pair.Key));
        Assert.Equal(publishedBefore.Count, Directory.GetFiles(fixture.Published, "*", SearchOption.AllDirectories).Length);
        if (mutation == "changed") File.AppendAllText(receipt, "\n");
        if (mutation == "missing") File.Delete(binding);
        Assert.Equal(mutation == "unchanged" ? 0 : 1, await fixture.Execute("resume",
            (_, _, _, _) => throw new InvalidOperationException("must not rescan")));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(fixture.Run, "checkpoints"), "*.json").Length);
        Assert.DoesNotContain(evidence, fixture.Error.ToString(), StringComparison.Ordinal);
    }

    private static Task<int> Scan(string[] args, TextWriter output, TextWriter error, CancellationToken token) =>
        TraceMapCommand.RunAsync(["scan", .. args], output, error, token);
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = WebFormsReviewPreflightCommand.PhysicalPath(Path.Combine(Path.GetTempPath(), "tracemap native execution #&%-" + Guid.NewGuid().ToString("N")));
        public string Source => Path.Combine(Root, "source");
        public string Published => Path.Combine(Root, "published");
        public string Run => Path.Combine(Root, "run");
        public string Manifest => Path.Combine(Run, "run-manifest.json");
        public string ConfigPath => Path.Combine(Root, "config.json");
        public WebFormsReviewConfig Config { get; set; }
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Source, "Pages"));
            Directory.CreateDirectory(Path.Combine(Source, "Unselected"));
            Directory.CreateDirectory(Published);
            File.WriteAllText(Path.Combine(Source, "Pages", "Lookup.aspx"), "<%@ Page Language=\"VB\" CodeFile=\"Lookup.aspx.vb\" Inherits=\"Lookup\" %>");
            File.WriteAllText(Path.Combine(Source, "Pages", "Lookup.aspx.vb"), "Public Class Lookup\n Public Sub Load()\n End Sub\nEnd Class\n");
            File.WriteAllText(Path.Combine(Source, "Pages", "Ignored.vbproj"), "<Project/>");
            File.WriteAllText(Path.Combine(Source, "Unselected", "Other.vb"), "Public Class Other\nEnd Class\n");
            var repo = FindRepoRoot();
            File.Copy(Path.Combine(repo, "samples", "compiled-dotnet-evidence", "csharp", "bin", "Debug", "net10.0", "CompiledEvidence.CSharp.dll"), Path.Combine(Published, "Public.dll"));
            foreach (var args in new[] { new[] { "init", "-q" }, new[] { "config", "user.name", "Public test" },
                new[] { "config", "user.email", "public@example.invalid" }, new[] { "add", "." }, new[] { "commit", "-qm", "public source" } })
            {
                using var process = new Process { StartInfo = new("git") { WorkingDirectory = Source,
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
                foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
                process.Start(); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                Assert.True(process.WaitForExit(10_000)); Task.WaitAll(stdout, stderr); Assert.Equal(0, process.ExitCode);
            }
            Config = new(WebFormsReviewPreflightCommand.ConfigSchema, "fresh", Source, GitMetadataProvider.Detect(Source).CommitSha,
                "projectless", null, [], ["Pages"], "selected", ["Pages/Lookup.aspx"], Published, ["Public.dll"], [], [], [], [], null, new());
        }
        public async Task Preflight()
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, JsonOptions));
            Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "preflight", "--config", ConfigPath, "--out", Run], Output, Error));
            Output.GetStringBuilder().Clear(); Error.GetStringBuilder().Clear();
        }
        public void WritePublishReceipt(string? mutation = null)
        {
            Directory.CreateDirectory(Path.Combine(Published, "bin"));
            Directory.CreateDirectory(Path.Combine(Published, "Pages"));
            Directory.CreateDirectory(Path.Combine(Published, "receipts"));
            File.Copy(Path.Combine(Published, "Public.dll"), Path.Combine(Published, "bin", "Public.dll"));
            File.WriteAllText(Path.Combine(Published, "Pages", "Lookup.aspx.compiled"), "<preserve virtualPath=\"/Pages/Lookup.aspx\" assembly=\"Public\" type=\"Public.GeneratedPage\" />");
            var sources = new[] { "Pages/Lookup.aspx", "Pages/Lookup.aspx.vb" }.Select(path => new
            { path, sha256 = mutation == "source-hash" ? new string('a', 64) : Hash(Path.Combine(Source, path)) }).ToArray();
            var sourceDigest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", sources.Select(source => source.path + ":" + source.sha256)) + "\n"))).ToLowerInvariant();
            var published = new[]
            {
                new { path = mutation == "escape" ? "../bin/Public.dll" : "bin/Public.dll", sha256 = Hash(Path.Combine(Published, "bin", "Public.dll")), kind = "assembly" },
                new { path = "Pages/Lookup.aspx.compiled", sha256 = Hash(Path.Combine(Published, "Pages", "Lookup.aspx.compiled")), kind = "compiled-map" }
            };
            var receipt = new
            {
                schemaVersion = "webforms-publish-binding.v1", visibility = "local-only", sourceCommitSha = mutation == "commit" ? new string('a', 40) : Config.SourceCommitSha,
                receiptGeneratorSha256 = Hash(typeof(WebFormsReviewExecutionTests).Assembly.Location),
                compilerSha256 = new string('a', 64), compilerProvenance = "unavailable-public-input-membership-test-not-build-proof",
                boundedInputSha256 = sourceDigest, sourceFiles = mutation == "duplicate" ? [.. sources, sources[0]] : sources,
                publishedFiles = published,
                pages = new[] { new { virtualPath = "/Pages/Lookup.aspx", sourcePath = mutation == "page" ? "Pages/Other.aspx" : "Pages/Lookup.aspx", assembly = "Public", generatedType = "Public.GeneratedPage", mapPath = "Pages/Lookup.aspx.compiled" } }
            };
            File.WriteAllText(Path.Combine(Published, "receipts", "publish.json"), JsonSerializer.Serialize(receipt, JsonOptions));
            Config = Config with { PrimaryAssemblies = mutation == "undeclared-dll" ? ["Public.dll"] : ["bin/Public.dll"],
                PageMaps = ["Pages/Lookup.aspx.compiled"], PublishReceiptRelativePath = "receipts/publish.json" };
        }
        public void AddPublicEventFixture()
        {
            File.WriteAllText(Path.Combine(Source, "Pages", "Lookup.aspx"), "<%@ Page Language=\"VB\" CodeFile=\"Lookup.aspx.vb\" Inherits=\"Lookup\" %>\n<asp:DropDownList ID=\"Names\" runat=\"server\" OnInit=\"Names_Init\" />");
            File.WriteAllText(Path.Combine(Source, "Pages", "Lookup.aspx.vb"), "Public Class Lookup\n Protected Sub Names_Init(sender As Object, e As System.EventArgs)\n  System.Console.WriteLine(\"public fixture\")\n End Sub\nEnd Class\n");
            foreach (var arguments in new[] { new[] { "add", "." }, new[] { "commit", "-qm", "public event fixture" } })
            {
                var start = new ProcessStartInfo("git") { WorkingDirectory = Source, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                foreach (var argument in arguments) start.ArgumentList.Add(argument);
                using var process = Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                Assert.True(process.WaitForExit(10_000)); Task.WaitAll(stdout, stderr); Assert.Equal(0, process.ExitCode);
            }
            Config = Config with { SourceCommitSha = GitMetadataProvider.Detect(Source).CommitSha };
        }
        public Task<int> Execute(string action, LocalReviewScanRunner runner, CancellationToken token = default)
        {
            Output.GetStringBuilder().Clear(); Error.GetStringBuilder().Clear();
            return WebFormsReviewExecutionCommand.RunWithAttachmentAsync([action, "--run", Run], Output, Error, runner,
                WebFormsReviewAttachmentExecution.WriteAsync, token);
        }
        public Task<int> ExecuteAttachment(string action, WebFormsReviewAttachmentRunner runner, CancellationToken token = default)
        {
            Output.GetStringBuilder().Clear(); Error.GetStringBuilder().Clear();
            return WebFormsReviewExecutionCommand.RunWithAttachmentAsync([action, "--run", Run], Output, Error,
                (_, _, _, _) => throw new InvalidOperationException("must not source scan"), runner, token);
        }
        public Task<int> ExecuteReports(string action, WebFormsReviewReportRunner runner, CancellationToken token = default)
        {
            Output.GetStringBuilder().Clear(); Error.GetStringBuilder().Clear();
            return WebFormsReviewExecutionCommand.RunWithReportsAsync([action, "--run", Run], Output, Error,
                (_, _, _, _) => throw new InvalidOperationException("must not source scan"),
                WebFormsReviewAttachmentExecution.WriteAsync, runner, token);
        }
        public async Task<string> PreflightAttachment()
        {
            var parent = Path.Combine(Root, "retained-parent");
            Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", Source, "--out", parent,
                "--include", "Pages/**", "--exclude", "**/*.vbproj", "--retain-source-snapshot"], TextWriter.Null, TextWriter.Null));
            Config = Config with { Operation = "attach", ParentScanRoot = parent };
            await Preflight();
            return parent;
        }
        public WebFormsReviewPreflightManifest Plan() => JsonSerializer.Deserialize<WebFormsReviewPreflightManifest>(File.ReadAllText(Manifest), JsonOptions)!;
        public WebFormsReviewCheckpoint LastCheckpoint() => JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(File.ReadAllText(
            Directory.GetFiles(Path.Combine(Run, "checkpoints"), "*.json").Order(StringComparer.Ordinal).Last()), JsonOptions)!;
        private static string FindRepoRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(directory.FullName, "samples"))) return directory.FullName;
            throw new InvalidOperationException("Public fixture unavailable");
        }
        public void Dispose() { Output.Dispose(); Error.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
}
