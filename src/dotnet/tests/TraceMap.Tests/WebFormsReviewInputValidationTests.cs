using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsReviewInputValidationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void Inspection_facade_preserves_the_exact_scanner_policy_and_global_gaps()
    {
        using var fixture = new Fixture();
        fixture.WriteReceipt("duplicate");
        var options = fixture.CompiledOptions();
        var expected = ManagedMetadataExtractor.Evaluate(fixture.Source, fixture.Commit, options);
        var actual = ManagedMetadataExtractor.InspectInputs(options, fixture.Commit);
        Assert.Equal(JsonSerializer.Serialize(expected.Provenance, JsonOptions), JsonSerializer.Serialize(actual.Provenance, JsonOptions));
        Assert.Equal(expected.KnownGaps, actual.KnownGaps);
        Assert.Contains("AmbiguousManagedBindingReceipt", actual.GapKinds);
        Assert.Contains("UnboundManagedInput", actual.GapKinds);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public void Disabled_inspection_and_cancellation_do_not_write_any_artifacts()
    {
        using var fixture = new Fixture();
        var disabled = ManagedMetadataExtractor.InspectInputs(new(fixture.Source, fixture.Output), fixture.Commit);
        Assert.Null(disabled.Provenance);
        Assert.Empty(disabled.GapKinds);
        Assert.Throws<OperationCanceledException>(() => ManagedMetadataExtractor.InspectInputs(
            fixture.CompiledOptions(), fixture.Commit, new CancellationToken(true)));
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public void Cancellation_interrupts_an_active_SQLite_query()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var cancellation = new CancellationTokenSource();
        using var registration = WebFormsReviewInputValidation.InterruptOnCancellation(connection, cancellation.Token);
        connection.CreateFunction("begin_cancellation", () =>
        {
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
            return 1;
        });
        using var command = connection.CreateCommand();
        command.CommandText = "with recursive numbers(n) as (select begin_cancellation() union all select n+1 from numbers where n<100000000) select sum(n) from numbers";
        var timer = Stopwatch.StartNew();
        var interrupted = Assert.Throws<SqliteException>(() => command.ExecuteScalar());
        Assert.Equal(9, interrupted.SqliteErrorCode);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData("bound", "bound", null)]
    [InlineData("wrong-locator", "unbound", "UnboundManagedInput")]
    [InlineData("wrong-identity", "mismatch", "ManagedInputProvenanceMismatch")]
    [InlineData("stale", "stale", "StaleManagedInput")]
    [InlineData("duplicate", "unbound", "AmbiguousManagedBindingReceipt")]
    public async Task Execution_gate_uses_receipt_identity_policy_not_hash_candidates(string kind, string state, string? gap)
    {
        using var fixture = new Fixture();
        fixture.WriteReceipt(kind);
        var plan = await fixture.Plan();
        var result = await WebFormsReviewInputValidation.ValidateAsync(plan);
        Assert.Equal(state, Assert.Single(result.CompiledProvenance.Outcomes).ProvenanceState);
        if (gap is not null) Assert.Contains(gap, result.Gaps);
        Assert.Contains("BuildAuthenticityNotEstablished", result.Gaps);
        Assert.Equal("current-source-snapshot-pending", result.SourceState);
        Assert.Null(result.ParentManifest);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public async Task Valid_parent_manifest_index_and_every_fact_are_pinned_without_mutation()
    {
        using var fixture = new Fixture();
        await fixture.CreateParent();
        var before = fixture.ParentHashes();
        var plan = await fixture.Plan();
        var result = await WebFormsReviewInputValidation.ValidateAsync(plan);
        Assert.NotNull(result.ParentManifest);
        Assert.True(result.ParentFactCount > 0);
        Assert.Equal("retained-parent-source-snapshot-verified", result.SourceState);
        Assert.Contains("RetainedSnapshotScopeNotFullCurrentInventory", result.Gaps);
        Assert.DoesNotContain("RetainedSourceSnapshotNotCurrentSourceValidation", result.Gaps);
        Assert.Equal(before.OrderBy(pair => pair.Key), fixture.ParentHashes().OrderBy(pair => pair.Key));
        Assert.DoesNotContain(Directory.GetFiles(fixture.Parent, "*", SearchOption.AllDirectories),
            path => path.EndsWith("-wal", StringComparison.Ordinal) || path.EndsWith("-shm", StringComparison.Ordinal));
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public async Task Checkpointed_WAL_parent_is_read_without_creating_sidecars()
    {
        using var fixture = new Fixture();
        await fixture.CreateParent();
        using (var connection = fixture.OpenIndex())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "pragma journal_mode=WAL";
            Assert.Equal("wal", command.ExecuteScalar());
        }
        var before = fixture.ParentHashes();
        var result = await WebFormsReviewInputValidation.ValidateAsync(await fixture.Plan());
        Assert.True(result.ParentFactCount > 0);
        Assert.Equal(before.OrderBy(pair => pair.Key), fixture.ParentHashes().OrderBy(pair => pair.Key));
        Assert.False(File.Exists(Path.Combine(fixture.Parent, "index.sqlite-wal")));
        Assert.False(File.Exists(Path.Combine(fixture.Parent, "index.sqlite-shm")));
    }

    [Theory]
    [InlineData("manifest-identity", "PARENT_IDENTITY_MISMATCH")]
    [InlineData("index-manifest", "PARENT_INDEX_MANIFEST_MISMATCH")]
    [InlineData("fact-content", "PARENT_FACT_INDEX_MISMATCH")]
    [InlineData("fact-duplicate", "PARENT_FACT_DUPLICATE")]
    [InlineData("fact-missing", "PARENT_FACT_COUNT_MISMATCH")]
    [InlineData("fact-rule", "PARENT_FACT_INVALID")]
    [InlineData("fact-count-limit", "PARENT_FACT_COUNT_LIMIT")]
    [InlineData("fact-line-limit", "PARENT_FACT_LINE_LIMIT")]
    [InlineData("sqlite-sidecar", "PARENT_INDEX_UNCHECKPOINTED")]
    [InlineData("oversized-index-row", "PARENT_FACT_INDEX_MISMATCH")]
    [InlineData("embedded-extra", "PARENT_INDEX_MANIFEST_MISMATCH")]
    public async Task Mismatched_or_unbounded_parent_is_rejected_before_execution(string mutation, string expected)
    {
        using var fixture = new Fixture();
        await fixture.CreateParent();
        var factsPath = Path.Combine(fixture.Parent, "facts.ndjson");
        var lines = File.ReadAllLines(factsPath);
        switch (mutation)
        {
            case "manifest-identity":
                var path = Path.Combine(fixture.Parent, "scan-manifest.json");
                var manifest = JsonSerializer.Deserialize<ScanManifest>(File.ReadAllText(path), JsonOptions)!;
                File.WriteAllText(path, JsonSerializer.Serialize(manifest with { GitRootHash = new string('b', 32) }, JsonOptions));
                break;
            case "index-manifest":
                using (var connection = fixture.OpenIndex())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "update scan_manifest set scanner_version='changed'";
                    command.ExecuteNonQuery();
                }
                break;
            case "fact-content":
            case "fact-rule":
                var fact = JsonSerializer.Deserialize<CodeFact>(lines[0], JsonOptions)!;
                lines[0] = JsonSerializer.Serialize(mutation == "fact-rule" ? fact with { RuleId = "" } :
                    fact with { Evidence = fact.Evidence with { ExtractorVersion = "changed" } }, JsonOptions);
                File.WriteAllLines(factsPath, lines);
                break;
            case "fact-duplicate": File.WriteAllLines(factsPath, lines.Concat([lines[0]])); break;
            case "fact-missing": File.WriteAllLines(factsPath, lines.Skip(1)); break;
            case "fact-count-limit": fixture.Config = fixture.Config with { Budgets = fixture.Config.Budgets with { MaxParentFacts = 1 } }; break;
            case "fact-line-limit": fixture.Config = fixture.Config with { Budgets = fixture.Config.Budgets with { MaxFactLineChars = 128 } }; break;
            case "sqlite-sidecar": File.WriteAllText(Path.Combine(fixture.Parent, "index.sqlite-wal"), "public unchecked sidecar"); break;
            case "oversized-index-row":
                using (var connection = fixture.OpenIndex())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "update facts set properties_json=$large where fact_id=(select fact_id from facts limit 1)";
                    command.Parameters.AddWithValue("$large", new string('x', fixture.Config.Budgets.MaxFactLineChars + 1));
                    command.ExecuteNonQuery();
                }
                break;
            case "embedded-extra":
                using (var connection = fixture.OpenIndex())
                using (var read = connection.CreateCommand())
                using (var write = connection.CreateCommand())
                {
                    read.CommandText = "select manifest_json from scan_manifest";
                    var json = (string)read.ExecuteScalar()!;
                    write.CommandText = "update scan_manifest set manifest_json=$json";
                    write.Parameters.AddWithValue("$json", json.TrimEnd()[..^1] + ",\"unrecognizedAdditionalState\":true}");
                    write.ExecuteNonQuery();
                }
                break;
        }
        var plan = await fixture.Plan();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => WebFormsReviewInputValidation.ValidateAsync(plan));
        Assert.Equal("WEBFORMS_REVIEW_" + expected, exception.Message);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public async Task Tampered_pinned_bytes_and_changed_git_head_cannot_use_an_old_plan()
    {
        using var fixture = new Fixture();
        var plan = await fixture.Plan();
        var original = File.ReadAllBytes(fixture.Assembly);
        original[^1] ^= 1;
        File.WriteAllBytes(fixture.Assembly, original);
        var changedBytes = await Assert.ThrowsAsync<InvalidOperationException>(() => WebFormsReviewInputValidation.ValidateAsync(plan));
        Assert.Equal("WEBFORMS_REVIEW_INPUT_CHANGED", changedBytes.Message);
        File.Copy(fixture.PublicFixtureAssembly, fixture.Assembly, overwrite: true);
        fixture.Git("commit", "--allow-empty", "-qm", "second public commit");
        var changedHead = await Assert.ThrowsAsync<InvalidOperationException>(() => WebFormsReviewInputValidation.ValidateAsync(plan));
        Assert.Equal("WEBFORMS_REVIEW_SOURCE_IDENTITY_CHANGED", changedHead.Message);
    }

    [Theory]
    [InlineData("same-size", "PARENT_SOURCE_SNAPSHOT_MISMATCH_OR_INCOMPLETE_INVENTORY")]
    [InlineData("size-change", "PARENT_SOURCE_SNAPSHOT_CHANGED")]
    [InlineData("missing", "PARENT_SOURCE_INPUT_UNAVAILABLE")]
    [InlineData("link", "PARENT_SOURCE_LINKED_INPUT")]
    public async Task Retained_attachment_refuses_changed_missing_or_linked_current_source(string mutation, string code)
    {
        using var fixture = new Fixture();
        await fixture.CreateParent();
        var before = fixture.ParentHashes();
        var path = Path.Combine(fixture.Source, "Lookup.aspx.vb");
        switch (mutation)
        {
            case "same-size": File.WriteAllText(path, File.ReadAllText(path).Replace("Load", "Save", StringComparison.Ordinal)); break;
            case "size-change": File.AppendAllText(path, "\n' changed source\n"); break;
            case "missing": File.Delete(path); break;
            case "link":
                var destination = Path.Combine(fixture.Root, "other.vb");
                File.Move(path, destination);
                File.CreateSymbolicLink(path, destination);
                break;
        }
        var plan = await fixture.Plan();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WebFormsReviewInputValidation.ValidateAsync(plan));
        Assert.Equal("WEBFORMS_REVIEW_" + code, error.Message);
        Assert.Equal(before.OrderBy(pair => pair.Key), fixture.ParentHashes().OrderBy(pair => pair.Key));
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public async Task Snapshot_gate_does_not_guess_missing_semantic_roster_members_or_expand_to_new_files()
    {
        using var fixture = new Fixture();
        await fixture.CreateParent();
        var plan = await fixture.Plan();
        var parent = (await WebFormsReviewInputValidation.ValidateParentAsync(plan,
            GitMetadataProvider.Detect(fixture.Source), default)).Manifest;
        File.WriteAllText(Path.Combine(fixture.Source, "NewUnanalyzed.vb"), "Public Class NewUnanalyzed\nEnd Class\n");
        var snapshot = WebFormsReviewInputValidation.ValidateRetainedSourceSnapshot(plan, parent, default);
        Assert.Equal(parent.SourceSnapshotDigest, snapshot.Digest);
        Assert.Equal(2, snapshot.FileCount);
        var missingRoster = parent with { SourceSnapshotDigest = new string('a', 64) };
        var error = Assert.Throws<InvalidOperationException>(() =>
            WebFormsReviewInputValidation.ValidateRetainedSourceSnapshot(plan, missingRoster, default));
        Assert.Equal("WEBFORMS_REVIEW_PARENT_SOURCE_SNAPSHOT_MISMATCH_OR_INCOMPLETE_INVENTORY", error.Message);
    }

    [Fact]
    public async Task Snapshot_gate_enforces_the_remaining_hash_byte_budget()
    {
        using var fixture = new Fixture();
        await fixture.CreateParent();
        var plan = await fixture.Plan();
        var parent = (await WebFormsReviewInputValidation.ValidateParentAsync(plan,
            GitMetadataProvider.Detect(fixture.Source), default)).Manifest;
        var bounded = plan with { Configuration = plan.Configuration with
        { Budgets = plan.Configuration.Budgets with { MaxTotalHashBytes = plan.Inputs.Sum(item => item.Bytes) + 1 } } };
        var error = Assert.Throws<InvalidOperationException>(() =>
            WebFormsReviewInputValidation.ValidateRetainedSourceSnapshot(bounded, parent, default));
        Assert.Equal("WEBFORMS_REVIEW_PARENT_SOURCE_INPUT_LIMIT", error.Message);
    }

    [Fact]
    public async Task Actual_scoped_parent_with_uninventoried_semantic_metadata_is_refused_not_guessed()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(Path.Combine(fixture.Source, "pages"));
        File.WriteAllText(Path.Combine(fixture.Source, "pages", "Scoped.vb"), "Public Class Scoped\nEnd Class\n");
        File.WriteAllText(Path.Combine(fixture.Source, "pages", "Scoped.vbproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(fixture.Source, "Directory.Build.props"), "<Project />");
        fixture.Git("add", ".");
        fixture.Git("commit", "-qm", "public scoped metadata");
        fixture.Config = fixture.Config with { SourceCommitSha = GitMetadataProvider.Detect(fixture.Source).CommitSha };
        await fixture.CreateParent("--project", "pages/Scoped.vbproj");
        var plan = await fixture.Plan();
        var parent = (await WebFormsReviewInputValidation.ValidateParentAsync(plan,
            GitMetadataProvider.Detect(fixture.Source), default)).Manifest;
        using (var connection = fixture.OpenIndex())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "select count(*) from facts where fact_type=$type and file_path='Directory.Build.props'";
            command.Parameters.AddWithValue("$type", FactTypes.FileInventoried);
            Assert.Equal(0L, command.ExecuteScalar());
        }
        var before = fixture.ParentHashes();
        var error = Assert.Throws<InvalidOperationException>(() =>
            WebFormsReviewInputValidation.ValidateRetainedSourceSnapshot(plan, parent, default));
        Assert.Equal("WEBFORMS_REVIEW_PARENT_SOURCE_SNAPSHOT_MISMATCH_OR_INCOMPLETE_INVENTORY", error.Message);
        Assert.Equal(before.OrderBy(pair => pair.Key), fixture.ParentHashes().OrderBy(pair => pair.Key));
    }

    [Fact]
    public async Task SQLite_roster_order_matches_scanner_UTF16_ordinal_order_not_UTF8_binary_order()
    {
        using var fixture = new Fixture();
        File.WriteAllText(Path.Combine(fixture.Source, "\uE000.vb"), "Public Class PrivateUseName\nEnd Class\n");
        File.WriteAllText(Path.Combine(fixture.Source, "\U0001F600.vb"), "Public Class SupplementaryName\nEnd Class\n");
        fixture.Git("add", ".");
        fixture.Git("commit", "-qm", "public Unicode inventory");
        fixture.Config = fixture.Config with { SourceCommitSha = GitMetadataProvider.Detect(fixture.Source).CommitSha };
        await fixture.CreateParent();
        var before = fixture.ParentHashes();
        var result = await WebFormsReviewInputValidation.ValidateAsync(await fixture.Plan());
        Assert.Equal("retained-parent-source-snapshot-verified", result.SourceState);
        Assert.Equal(before.OrderBy(pair => pair.Key), fixture.ParentHashes().OrderBy(pair => pair.Key));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = WebFormsReviewPreflightCommand.PhysicalPath(Path.Combine(Path.GetTempPath(), "tracemap input gate #&%-" + Guid.NewGuid().ToString("N")));
        public string Source => Path.Combine(Root, "source");
        public string Published => Path.Combine(Root, "published");
        public string Parent => Path.Combine(Root, "parent");
        public string Output => Path.Combine(Root, "run");
        public string Assembly => Path.Combine(Published, "Public.dll");
        public string PublicFixtureAssembly { get; }
        public string Commit { get; }
        public WebFormsReviewConfig Config { get; set; }

        public Fixture()
        {
            Directory.CreateDirectory(Source);
            Directory.CreateDirectory(Published);
            File.WriteAllText(Path.Combine(Source, "Lookup.aspx"), "<%@ Page Language=\"VB\" CodeFile=\"Lookup.aspx.vb\" Inherits=\"Lookup\" %>");
            File.WriteAllText(Path.Combine(Source, "Lookup.aspx.vb"), "Public Class Lookup\n Public Sub Load()\n End Sub\nEnd Class\n");
            var repo = FindRepoRoot();
            PublicFixtureAssembly = Path.Combine(repo, "samples", "compiled-dotnet-evidence", "csharp", "bin", "Debug", "net10.0", "CompiledEvidence.CSharp.dll");
            File.Copy(PublicFixtureAssembly, Assembly);
            Git("init", "-q");
            Git("config", "user.name", "Public test");
            Git("config", "user.email", "public-test@example.invalid");
            Git("add", ".");
            Git("commit", "-qm", "public source");
            Commit = GitMetadataProvider.Detect(Source).CommitSha;
            Config = new(WebFormsReviewPreflightCommand.ConfigSchema, "fresh", Source, Commit, "projectless", null,
                [], ["."], "selected", ["Lookup.aspx"], Published, ["Public.dll"], [], [], [], [], null, new());
        }

        public ScanOptions CompiledOptions() => new(Source, Output, CompiledInputPaths: [Assembly],
            CompiledBindingReceiptPaths: Config.BindingReceipts.Select(relative => Path.Combine(Published, relative)).ToArray(),
            CompiledInputLimits: new(MaxArtifactCount: Config.Budgets.MaxInputFiles, MaxTextLength: Config.Budgets.MetadataMaxText,
                MaxTotalWorkUnits: Config.Budgets.MetadataMaxWork));

        public void WriteReceipt(string kind)
        {
            var outcome = Assert.Single(ManagedMetadataExtractor.InspectInputs(CompiledOptions(), Commit).Provenance!.Outcomes);
            var binding = new
            {
                schemaVersion = "compiled-input-binding.v1",
                safeLocator = kind == "wrong-locator" ? "not-the-scanner-locator" : outcome.SafeLocator,
                artifactSha256 = outcome.RawFileSha256,
                assemblyIdentity = kind == "wrong-identity" ? "wrong-identity" : outcome.AssemblyIdentity,
                binarySourceRepository = "public-synthetic-repository",
                binarySourceCommitSha = kind == "stale" ? new string('a', 40) : Commit,
                binarySourceCommitRelation = kind == "stale" ? "ancestor-of-scan" : null,
                binaryBuildIdentity = "operator-declared-test-not-build-authenticity"
            };
            File.WriteAllText(Path.Combine(Published, "binding.json"), JsonSerializer.Serialize(new
            { schemaVersion = "compiled-input-binding-set.v1", bindings = kind == "duplicate" ? new[] { binding, binding } : [binding] }, JsonOptions));
            Config = Config with { BindingReceipts = ["binding.json"] };
        }

        public async Task<WebFormsReviewPreflightManifest> Plan()
        {
            var configPath = Path.Combine(Root, "config.json");
            File.WriteAllText(configPath, JsonSerializer.Serialize(Config, JsonOptions));
            return await WebFormsReviewPreflightCommand.BuildAsync(configPath, Output);
        }

        public async Task CreateParent(params string[] extraArguments)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", Source, "--out", Parent, .. extraArguments], output, error));
            Config = Config with { Operation = "attach", ParentScanRoot = Parent };
        }

        public Dictionary<string, string> ParentHashes() => Directory.GetFiles(Parent, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, path => { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); });

        public SqliteConnection OpenIndex()
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(Parent, "index.sqlite"), Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
            connection.Open();
            return connection;
        }

        public void Git(params string[] arguments)
        {
            using var process = new Process { StartInfo = new("git") { WorkingDirectory = Source,
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            Assert.True(process.Start());
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(10_000));
            Task.WaitAll(stdout, stderr);
            Assert.Equal(0, process.ExitCode);
        }

        private static string FindRepoRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(directory.FullName, "samples")))
                    return directory.FullName;
            throw new InvalidOperationException("Public test repository unavailable");
        }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
}
