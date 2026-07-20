using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Access;
using TraceMap.Access.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class AccessFoundationTests
{
    private const string SecretMarker = "Password_ProdVault_92817";
    private const string SqlMarker = "SELECT * FROM PayrollSecrets_92817";
    private const string ConnectionMarker = "ODBC;DSN=PrivateLedger_92817;PWD=NeverPersistThis";

    [Fact]
    public void Access_rule_catalog_uses_standard_primary_tiers_and_documents_all_possible_tiers()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var ruleId in new[]
        {
            "legacy.access.database.inventory.v1",
            "legacy.access.schema.v1",
            "legacy.access.query.v1",
            "legacy.access.external-link.v1",
            "legacy.access.ui-surface.v1",
            "legacy.access.binding.v1",
            "legacy.access.macro-gap.v1"
        })
        {
            var start = Regex.Match(catalog, $@"(?m)^\s*-\s*id:\s*{Regex.Escape(ruleId)}\s*$");
            Assert.True(start.Success, $"Missing rule catalog entry for {ruleId}.");
            var remainder = catalog[start.Index..];
            var next = Regex.Match(remainder[start.Length..], @"(?m)^\s*-\s*id:\s*\S+\s*$");
            var block = next.Success ? remainder[..(start.Length + next.Index)] : remainder;
            Assert.Matches(@"(?m)^\s*evidenceTier:\s*Tier(?:1Semantic|2Structural|3SyntaxOrTextual|4Unknown)\s*$", block);
            Assert.Contains("possibleEvidenceTiers:", block, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Access_worker_always_observes_the_bounded_stderr_drain()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "dotnet", "TraceMap.Access", "AccessWorkerSupervisor.cs"));

        Assert.Contains("DrainBoundedAsync(worker.StandardError, total.Token)", source, StringComparison.Ordinal);
        Assert.Contains("finally", source, StringComparison.Ordinal);
        Assert.Contains("await stderrTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Safe_identity_hashes_protected_names_and_scopes_keys_to_repository_commit_and_path()
    {
        var firstSeed = AccessSafeValues.DatabaseIdentitySeed("repo-a", new string('a', 40), "db/app.accdb", "db-hash");
        var secondSeed = AccessSafeValues.DatabaseIdentitySeed("repo-a", new string('b', 40), "db/app.accdb", "db-hash");
        var protectedIdentity = AccessSafeValues.Identity(firstSeed, "table", SecretMarker);

        Assert.Null(protectedIdentity.DisplayName);
        Assert.DoesNotContain(SecretMarker, JsonSerializer.Serialize(protectedIdentity), StringComparison.Ordinal);
        Assert.NotEqual(
            AccessSafeValues.Identity(firstSeed, "table", "Orders").StableKey,
            AccessSafeValues.Identity(secondSeed, "table", "Orders").StableKey);
    }

    [Fact]
    public void Query_projector_ignores_literals_and_comments_and_marks_external_in_clause_partial()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Orders"] = [("table-orders", "table")],
            ["PayrollSecrets_92817"] = [("table-secret", "table")],
            ["CommentOnly"] = [("table-comment", "table")]
        };

        var projected = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM [Orders] WHERE note='FROM [PayrollSecrets_92817]' -- JOIN CommentOnly\n", known);
        var external = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM Orders IN 'C:\\\\PrivateLedger_92817.accdb'", known);

        Assert.Equal(["table-orders"], projected.Dependencies.Select(item => item.TargetStableKey));
        Assert.Equal("complete", projected.Coverage);
        Assert.True(external.UnsupportedShape);
        Assert.Equal("partial", external.Coverage);
    }

    [Fact]
    public void Query_projector_marks_unresolved_references_partial_and_recognizes_access_from_lists()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Customers"] = [("table-customers", "table")],
            ["Orders"] = [("table-orders", "table")]
        };

        var commaList = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM [Customers], Orders WHERE Customers.Id = Orders.CustomerId", known);
        var parenthesizedJoin = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM ([Customers] INNER JOIN Orders ON Customers.Id = Orders.CustomerId)", known);
        var unresolved = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM LinkedOrders", known);

        Assert.Equal(["table-customers", "table-orders"], commaList.Dependencies.Select(item => item.TargetStableKey));
        Assert.Equal("complete", commaList.Coverage);
        Assert.False(commaList.UnsupportedShape);
        Assert.Equal(["table-customers", "table-orders"], parenthesizedJoin.Dependencies.Select(item => item.TargetStableKey));
        Assert.Equal("complete", parenthesizedJoin.Coverage);
        Assert.True(unresolved.UnsupportedShape);
        Assert.Equal("partial", unresolved.Coverage);
        Assert.Empty(unresolved.Dependencies);
    }

    [Fact]
    public void Input_validator_requires_exact_tracked_head_bytes_preserves_requested_output_and_rejects_destructive_ancestor()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "access-fixture");
        Directory.CreateDirectory(Path.Combine(repo, "data"));
        RunGit(repo, "init", "-b", "test");
        RunGit(repo, "config", "user.email", "test@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Test");
        File.WriteAllBytes(Path.Combine(repo, "data", "fixture.accdb"), [1, 2, 3, 4]);
        RunGit(repo, "add", "data/fixture.accdb");
        RunGit(repo, "commit", "-m", "fixture");

        var valid = AccessInputValidator.Validate(new(repo, "data/fixture.accdb", Path.Combine(repo, "out")));
        Assert.Equal("data/fixture.accdb", valid.DatabaseRelativePath);
        Assert.Null(valid.RemoteUrl);

        var subdirectoryOutput = Path.Combine(repo, "data", "requested-output");
        var fromSubdirectory = AccessInputValidator.Validate(new(Path.Combine(repo, "data"), "data/fixture.accdb", subdirectoryOutput));
        Assert.Equal(Path.GetDirectoryName(fromSubdirectory.DatabaseFullPath), Path.GetDirectoryName(fromSubdirectory.OutputFullPath));
        Assert.Equal("requested-output", Path.GetFileName(fromSubdirectory.OutputFullPath));

        var gitMetadata = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "data/fixture.accdb", Path.Combine(repo, ".git", "hooks", "access-output"))));
        Assert.Equal("AccessUnsafeOutputPath", gitMetadata.Classification);

        var existingOutput = Path.Combine(repo, "existing-output");
        Directory.CreateDirectory(existingOutput);
        var sentinel = Path.Combine(existingOutput, "do-not-delete.txt");
        File.WriteAllText(sentinel, "owned by caller");
        var existing = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "data/fixture.accdb", existingOutput)));
        Assert.Equal("AccessOutputAlreadyExists", existing.Classification);
        Assert.Equal("owned by caller", File.ReadAllText(sentinel));

        var ancestor = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "data/fixture.accdb", Path.Combine(repo, "data"))));
        Assert.Equal("AccessUnsafeOutputPath", ancestor.Classification);

        File.WriteAllBytes(Path.Combine(repo, "data", "fixture.accdb"), [4, 3, 2, 1]);
        var dirty = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "data/fixture.accdb", Path.Combine(repo, "out"))));
        Assert.Equal("AccessInputNotAtCommit", dirty.Classification);

        File.WriteAllBytes(Path.Combine(repo, "data", "fixture.accdb"), [1, 2, 3, 4]);
        RunGit(repo, "update-index", "--assume-unchanged", "data/fixture.accdb");
        File.WriteAllBytes(Path.Combine(repo, "data", "fixture.accdb"), [4, 3, 2, 1]);
        var assumedUnchanged = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "data/fixture.accdb", Path.Combine(repo, "out"))));
        Assert.Equal("AccessInputNotAtCommit", assumedUnchanged.Classification);
    }

    [Fact]
    public async Task Cli_help_and_version_do_not_require_windows_or_com_and_scan_fails_cleanly_off_windows()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(0, await AccessCommand.RunAsync(["--help"], output, error));
        Assert.Contains("static design metadata only", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        Assert.Equal(0, await AccessCommand.RunAsync(["--version"], output, error));
        Assert.Equal(AccessFactBuilder.ScannerVersion, output.ToString().Trim());

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(1, await AccessCommand.RunAsync(
                ["scan", "--repo", ".", "--database", "fixture.accdb", "--out", "out"], output, error));
            Assert.Contains("AccessUnsupportedPlatform", error.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Environment_probe_classifies_unsupported_platform_and_missing_access_com()
    {
        var unsupported = Assert.Throws<AccessScanException>(() => AccessEnvironmentProbe.Probe(false, () => typeof(object)));
        Assert.Equal("AccessUnsupportedPlatform", unsupported.Classification);
        var missingCom = Assert.Throws<AccessScanException>(() => AccessEnvironmentProbe.Probe(true, () => null));
        Assert.Equal("AccessComUnavailable", missingCom.Classification);
        Assert.Equal(typeof(object), AccessEnvironmentProbe.Probe(true, () => typeof(object)));
    }

    [Fact]
    public async Task Facts_and_all_standard_text_artifacts_suppress_raw_sql_connections_paths_and_credentials()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "artifacts"));
        var projection = Projection(input);
        var result = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));

        var serializedFacts = JsonSerializer.Serialize(result.Facts);
        var report = AccessArtifactWriter.Report(result);
        var log = AccessArtifactWriter.AnalyzerLog(result);
        foreach (var protectedValue in new[] { SecretMarker, SqlMarker, ConnectionMarker, temp.Path })
        {
            Assert.DoesNotContain(protectedValue, serializedFacts, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(protectedValue, report, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(protectedValue, log, StringComparison.OrdinalIgnoreCase);
        }

        await AccessArtifactWriter.WriteAsync(input.OutputFullPath, result, AccessLimits.Default);
        Assert.Equal(
            ["facts.ndjson", "index.sqlite", "logs/analyzer.log", "report.md", "scan-manifest.json"],
            Directory.EnumerateFiles(input.OutputFullPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(input.OutputFullPath, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal));
        foreach (var file in Directory.EnumerateFiles(input.OutputFullPath, "*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(file).Equals(".sqlite", StringComparison.OrdinalIgnoreCase)) continue;
            var contents = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain(SecretMarker, contents, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ConnectionMarker, contents, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(temp.Path, contents, StringComparison.OrdinalIgnoreCase);
        }

        var existingArtifactHash = AccessInputValidator.HashFile(Path.Combine(input.OutputFullPath, "facts.ndjson"));
        var existingOutput = await Assert.ThrowsAsync<AccessScanException>(() =>
            AccessArtifactWriter.WriteAsync(input.OutputFullPath, result, AccessLimits.Default));
        Assert.Equal("AccessOutputAlreadyExists", existingOutput.Classification);
        Assert.Equal(existingArtifactHash, AccessInputValidator.HashFile(Path.Combine(input.OutputFullPath, "facts.ndjson")));
    }

    [Fact]
    public void Fact_ids_and_order_are_deterministic_and_case_only_names_remain_distinct()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var projection = Projection(input);

        var first = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));
        var second = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));

        Assert.Equal(first.Facts.Select(fact => fact.FactId), second.Facts.Select(fact => fact.FactId));
        Assert.Equal(first.Facts.Select(fact => fact.FactType), first.Facts.Select(fact => fact.FactType).OrderBy(value => value, StringComparer.Ordinal));
        var tableKeys = projection.Tables.Select(table => table.Identity.StableKey).ToArray();
        Assert.Equal(2, tableKeys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Controlled_working_copies_are_distinct_hash_verified_private_and_deleted()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        string firstDirectory;
        string secondDirectory;
        using (var first = AccessWorkingCopy.Create(input))
        using (var second = AccessWorkingCopy.Create(input))
        {
            firstDirectory = first.DirectoryPath;
            secondDirectory = second.DirectoryPath;
            Assert.NotEqual(firstDirectory, secondDirectory);
            Assert.Equal(input.DatabaseHash, AccessInputValidator.HashFile(first.DatabasePath));
            Assert.Equal(input.DatabaseHash, AccessInputValidator.HashFile(second.DatabasePath));
            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(firstDirectory);
                Assert.Equal(UnixFileMode.None, mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute));
            }
        }
        Assert.False(Directory.Exists(firstDirectory));
        Assert.False(Directory.Exists(secondDirectory));
        Assert.Equal(input.DatabaseHash, AccessInputValidator.HashFile(databasePath));
    }

    [Theory]
    [InlineData("../fixture.accdb", "AccessDatabasePathTraversal")]
    [InlineData("data/../fixture.accdb", "AccessDatabasePathTraversal")]
    [InlineData("", "AccessDatabasePathMissing")]
    public void Relative_path_normalization_rejects_traversal(string path, string classification)
    {
        var exception = Assert.Throws<AccessScanException>(() => AccessInputValidator.NormalizeRelativeSegments(path));
        Assert.Equal(classification, exception.Classification);
    }

    [Fact]
    public void Absolute_database_path_is_rejected_before_git_or_com()
    {
        var exception = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new("missing-repo", Path.GetFullPath("fixture.accdb"), Path.GetFullPath("out"))));
        Assert.Equal("AccessDatabasePathMustBeRelative", exception.Classification);
    }

    [Fact]
    public void Untracked_database_and_missing_git_metadata_are_rejected()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllBytes(Path.Combine(repo, "fixture.accdb"), [1, 2, 3, 4]);
        var missingGit = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "fixture.accdb", Path.Combine(repo, "out"))));
        Assert.Equal("AccessGitRootUnavailable", missingGit.Classification);

        RunGit(repo, "init", "-b", "test");
        RunGit(repo, "config", "user.email", "test@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Test");
        File.WriteAllText(Path.Combine(repo, "README.md"), "fixture");
        RunGit(repo, "add", "README.md");
        RunGit(repo, "commit", "-m", "fixture root");
        var untracked = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "fixture.accdb", Path.Combine(repo, "out"))));
        Assert.Equal("AccessInputNotTracked", untracked.Classification);
    }

    [Fact]
    public void Git_lfs_pointer_bytes_are_not_accepted_as_a_materialized_database()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        RunGit(repo, "init", "-b", "test");
        RunGit(repo, "config", "user.email", "test@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Test");
        File.WriteAllText(Path.Combine(repo, ".gitattributes"), "*.accdb filter=lfs\n");
        File.WriteAllText(Path.Combine(repo, "fixture.accdb"), $"version https://git-lfs.github.com/spec/v1\noid sha256:{new string('a', 64)}\nsize 1234\n");
        RunGit(repo, "add", ".gitattributes", "fixture.accdb");
        RunGit(repo, "commit", "-m", "pointer fixture");

        var pointer = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "fixture.accdb", Path.Combine(repo, "out"))));
        Assert.Equal("AccessGitLfsContentMismatch", pointer.Classification);
    }

    [Fact]
    public void Fact_ceiling_keeps_foundational_evidence_and_emits_an_explicit_gap()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var limits = AccessLimits.Default with { MaxFacts = 4 };

        var result = AccessFactBuilder.Build(input, Projection(input), new(temp.Path, "fixture.accdb", input.OutputFullPath), limits);

        Assert.Equal(4, result.Facts.Count);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.FileInventoried);
        Assert.Contains(result.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "AccessFactLimitReached");
        Assert.Contains("AccessFactLimitReached", result.Manifest.KnownGaps);
    }

    [Fact]
    public void Identical_catalog_gaps_are_deduplicated_before_sqlite_persistence()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.mdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out")) with { DatabaseExtension = ".mdb" };
        var projection = Projection(input) with
        {
            DatabaseExtension = ".mdb",
            Gaps = [new("AccessTableCatalogUnavailable", "database-tables", null), new("AccessTableCatalogUnavailable", "database-tables", null)]
        };

        var result = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.mdb", input.OutputFullPath));
        Assert.Single(result.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "AccessTableCatalogUnavailable");
        Assert.Equal(result.Facts.Count, result.Facts.Select(fact => fact.FactId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Exact_gap_limit_preserves_all_gaps_and_over_limit_replaces_only_one_with_limit_evidence()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var limits = AccessLimits.Default with { MaxGaps = 2 };
        var two = Projection(input) with { Gaps = [new("GapA", "database", null), new("GapB", "database", null)] };
        var three = two with { Gaps = [.. two.Gaps, new("GapC", "database", null)] };

        var exact = AccessFactBuilder.Build(input, two, new(temp.Path, "fixture.accdb", input.OutputFullPath), limits);
        Assert.Contains(exact.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "GapA");
        Assert.Contains(exact.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "GapB");
        Assert.DoesNotContain(exact.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "AccessGapLimitReached");

        var truncated = AccessFactBuilder.Build(input, three, new(temp.Path, "fixture.accdb", input.OutputFullPath), limits);
        Assert.Single(truncated.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "AccessGapLimitReached");
        Assert.Equal(2, truncated.Facts.Count(fact => fact.FactType == FactTypes.AnalysisGap));
    }

    [Fact]
    public void Internal_raw_field_lookup_supports_redacted_names_and_index_scopes_are_table_specific()
    {
        var databaseSeed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('a', 40), "fixture.accdb", "hash");
        var firstTable = AccessSafeValues.Identity(databaseSeed, "table", "First");
        var secondTable = AccessSafeValues.Identity(databaseSeed, "table", "Second");
        var protectedField = new AccessFieldProjection(AccessSafeValues.Identity(databaseSeed, $"field-{firstTable.StableKey}", "Order ID"), 0, "long", 4, true);
        var lookup = new Dictionary<string, List<AccessFieldProjection>>(StringComparer.OrdinalIgnoreCase) { ["Order ID"] = [protectedField] };

        Assert.Null(protectedField.Identity.DisplayName);
        Assert.True(AccessComReader.UniqueField(lookup, "order id", out var resolved));
        Assert.Same(protectedField, resolved);
        Assert.NotEqual(
            AccessSafeValues.Identity(databaseSeed, $"index-{firstTable.StableKey}", "PrimaryKey").StableKey,
            AccessSafeValues.Identity(databaseSeed, $"index-{secondTable.StableKey}", "PrimaryKey").StableKey);
    }

    [Fact]
    public void Bounded_com_strings_preserve_classification_and_frame_limits_count_utf8_plus_lf()
    {
        var failure = Assert.Throws<AccessScanException>(() =>
            AccessComReader.BoundedString(() => throw new InvalidOperationException("raw COM failure"), 10, "AccessVersionUnavailable"));
        Assert.Equal("AccessVersionUnavailable", failure.Classification);
        Assert.Equal(3, AccessWorkerProtocol.EncodedFrameBytes("é"));
    }

    [Fact]
    public void Worker_failure_frames_allow_only_bounded_classification_tokens()
    {
        var frame = AccessWorkerFrame.Failure("safe-token", $"failure at {Path.GetTempPath()} {ConnectionMarker}");
        var json = JsonSerializer.Serialize(frame, AccessJsonContext.Default.AccessWorkerFrame);

        Assert.Equal("AccessWorkerFailure", frame.Classification);
        Assert.DoesNotContain(ConnectionMarker, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetTempPath(), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Owned_process_decision_rejects_preexisting_stale_wrong_name_and_foreign_session_candidates()
    {
        var workerStart = DateTimeOffset.UtcNow;
        var valid = new OwnedAccessProcess(100, "MSACCESS", 7, workerStart);

        Assert.Equal(valid, AccessProcessOwnership.Accept(valid, new HashSet<int>(), workerStart, 7));
        Assert.Null(AccessProcessOwnership.Accept(valid, new HashSet<int> { 100 }, workerStart, 7));
        Assert.Null(AccessProcessOwnership.Accept(valid with { StartedAtUtc = workerStart.AddMinutes(-1) }, new HashSet<int>(), workerStart, 7));
        Assert.Null(AccessProcessOwnership.Accept(valid with { ProcessName = "notepad" }, new HashSet<int>(), workerStart, 7));
        Assert.Null(AccessProcessOwnership.Accept(valid with { SessionId = 8 }, new HashSet<int>(), workerStart, 7));
    }

    [Fact]
    public void Owned_pid_fallback_after_job_rejection_revalidates_identity_before_termination()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var owned = new OwnedAccessProcess(100, "MSACCESS", 7, startedAt);

        Assert.True(AccessProcessOwnership.CanTerminateFallback(owned, owned));
        Assert.False(AccessProcessOwnership.CanTerminateFallback(owned, owned with { ProcessId = 101 }));
        Assert.False(AccessProcessOwnership.CanTerminateFallback(owned, owned with { ProcessName = "notepad" }));
        Assert.False(AccessProcessOwnership.CanTerminateFallback(owned, owned with { SessionId = 8 }));
        Assert.False(AccessProcessOwnership.CanTerminateFallback(owned, owned with { StartedAtUtc = startedAt.AddMilliseconds(1) }));
    }

    [Fact]
    public async Task Worker_protocol_accepts_heartbeats_and_owned_result_and_rejects_crash_token_and_idle_failures()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var projection = Projection(input) with { AccessProcessId = 42 };
        var token = "protocol-token";
        var owned = new OwnedAccessProcess(42, "MSACCESS", 1, DateTimeOffset.UtcNow);
        var frames = string.Join('\n',
            JsonSerializer.Serialize(AccessWorkerFrame.Heartbeat(token), AccessJsonContext.Default.AccessWorkerFrame),
            JsonSerializer.Serialize(AccessWorkerFrame.Hello(token, 42), AccessJsonContext.Default.AccessWorkerFrame),
            JsonSerializer.Serialize(AccessWorkerFrame.Success(token, projection), AccessJsonContext.Default.AccessWorkerFrame)) + "\n";

        var accepted = await AccessWorkerProtocol.ReadResultAsync(
            new StringReader(frames), token, 1024 * 1024, TimeSpan.FromSeconds(1), _ => owned, _ => { }, CancellationToken.None);
        Assert.Equal(
            JsonSerializer.Serialize(projection, AccessJsonContext.Default.AccessDatabaseProjection),
            JsonSerializer.Serialize(accepted, AccessJsonContext.Default.AccessDatabaseProjection));

        var crashed = await Assert.ThrowsAsync<AccessScanException>(() => AccessWorkerProtocol.ReadResultAsync(
            new StringReader(string.Empty), token, 1024, TimeSpan.FromSeconds(1), _ => owned, _ => { }, CancellationToken.None));
        Assert.Equal("AccessWorkerResultMissing", crashed.Classification);

        var wrongTokenFrame = JsonSerializer.Serialize(AccessWorkerFrame.Heartbeat("wrong"), AccessJsonContext.Default.AccessWorkerFrame);
        var wrongToken = await Assert.ThrowsAsync<AccessScanException>(() => AccessWorkerProtocol.ReadResultAsync(
            new StringReader(wrongTokenFrame), token, 1024, TimeSpan.FromSeconds(1), _ => owned, _ => { }, CancellationToken.None));
        Assert.Equal("AccessWorkerTokenMismatch", wrongToken.Classification);

        // A fully stalled worker is classified by the idle watchdog.
        var idle = await Assert.ThrowsAsync<AccessScanException>(() => AccessWorkerProtocol.ReadResultAsync(
            new BlockingTextReader(), token, 1024, TimeSpan.FromMilliseconds(25), _ => owned, _ => { }, CancellationToken.None));
        Assert.Equal("AccessWorkerHeartbeatTimeout", idle.Classification);

        // A modal COM call can leave the independent heartbeat alive. The total
        // deadline must still win and invoke supervisor containment.
        using var totalDeadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AccessWorkerProtocol.ReadResultAsync(
            new EndlessHeartbeatTextReader(token), token, 1024 * 1024, TimeSpan.FromSeconds(1), _ => owned, _ => { }, totalDeadline.Token));
    }

    private static AccessValidatedInput Input(string databasePath, string outputPath)
    {
        var hash = AccessInputValidator.HashFile(databasePath);
        return new(
            Path.GetDirectoryName(databasePath)!,
            "fixture-repo",
            AccessSafeValues.RoleHash("access-repository-identity", "fixture-repo"),
            null,
            "test",
            new string('a', 40),
            databasePath,
            "fixture.accdb",
            hash,
            ".accdb",
            outputPath,
            false);
    }

    private static AccessDatabaseProjection Projection(AccessValidatedInput input)
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed(input.RepositoryIdentityHash, input.CommitSha, input.DatabaseRelativePath, input.DatabaseHash);
        var orders = AccessSafeValues.Identity(seed, "table", "Orders");
        var ordersCase = AccessSafeValues.Identity(seed, "table", "orders");
        var protectedQuery = AccessSafeValues.Identity(seed, "query", SecretMarker);
        var protectedExternal = AccessSafeValues.Identity(seed, "table", "PrivateServer_Password");
        return new(
            "tracemap.access-projection.v1",
            input.DatabaseHash,
            ".accdb",
            "16.0",
            1234,
            false,
            false,
            2,
            [new(orders, [], []), new(ordersCase, [], [])],
            [],
            [new(protectedQuery, "select", AccessSafeValues.RoleHash("access-query-sql", SqlMarker), SqlMarker.Length, "partial", [], [], false, null, null)],
            [new(protectedExternal, "odbc", AccessSafeValues.RoleHash("access-linked-source", ConnectionMarker), "linked-table")],
            [new("AccessQueryDependencyPartial", "query", protectedQuery.StableKey)],
            [new("rowDataRead", "false"), new("executionPerformed", "false")]);
    }

    private static void RunGit(string workingDirectory, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git unavailable");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', args)} failed: {output} {error}");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class BlockingTextReader : TextReader
    {
        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }
    }

    private sealed class EndlessHeartbeatTextReader(string token) : TextReader
    {
        private readonly string _frame = JsonSerializer.Serialize(AccessWorkerFrame.Heartbeat(token), AccessJsonContext.Default.AccessWorkerFrame);

        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return _frame;
        }
    }
}
