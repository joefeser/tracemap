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

    [Fact]
    public async Task Attach_fails_explicitly_without_writing_to_the_parent_or_run()
    {
        using var fixture = new Fixture();
        var parent = Path.Combine(fixture.Root, "parent");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", fixture.Source, "--out", parent], TextWriter.Null, TextWriter.Null));
        fixture.Config = fixture.Config with { Operation = "attach", ParentScanRoot = parent };
        await fixture.Preflight();
        var before = Directory.GetFiles(parent, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        Assert.Equal(1, await fixture.Execute("run", (_, _, _, _) => throw new InvalidOperationException("must not execute")));
        Assert.Contains("ATTACH_EXECUTION_PENDING", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(fixture.Run, "attempts")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Run, "checkpoints")));
        foreach (var pair in before) Assert.Equal(pair.Value, Hash(pair.Key));
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
        await fixture.Preflight();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "run", "--run", fixture.Run], fixture.Output, fixture.Error));
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "resume", "--run", fixture.Run], fixture.Output, fixture.Error));
        Assert.Equal("scan-completed-reports-pending", fixture.LastCheckpoint().State);
        Assert.Equal(2, fixture.LastCheckpoint().Sequence);
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
        public Task<int> Execute(string action, LocalReviewScanRunner runner, CancellationToken token = default)
        {
            Output.GetStringBuilder().Clear(); Error.GetStringBuilder().Clear();
            return WebFormsReviewExecutionCommand.RunAsync([action, "--run", Run], Output, Error, runner, token);
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
