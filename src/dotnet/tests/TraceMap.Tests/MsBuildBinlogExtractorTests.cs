using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class MsBuildBinlogExtractorTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string Secret = "Server=private-db.internal;Password=SuperSecret!;Token=ghp_never_render";
    private const string Command = "powershell -Command Invoke-SecretPayload";
    private const string Source = "public sealed class NeverRenderThisSource { }";

    [Fact]
    public void Extract_projects_success_graph_and_safe_diagnostic_without_messages()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp.Path);
        var binlog = Path.Combine(temp.Path, "success.binlog");
        PrepareBinlog(binlog, repo, succeeded: true, includeOutsideRoot: false, extraMessages: 0);

        var manifest = Manifest();
        var facts = MsBuildBinlogExtractor.Extract(repo, manifest, [binlog]);

        var artifact = Assert.Single(facts, fact => fact.FactType == FactTypes.MsBuildBinlogObserved);
        Assert.Equal("succeeded", artifact.Properties["recordedBuildResult"]);
        Assert.Equal("observed-bounded", artifact.Properties["coverageLabel"]);
        Assert.Equal(64, artifact.Properties["artifactSha256"].Length);
        Assert.Equal(2, facts.Count(fact => fact.FactType == FactTypes.MsBuildProjectObserved));
        var edge = Assert.Single(facts, fact => fact.FactType == FactTypes.MsBuildProjectReferenceObserved);
        Assert.Equal("src/Root/Root.csproj", edge.SourceSymbol);
        Assert.Equal("src/Child/Child.csproj", edge.TargetSymbol);
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(indexPath, manifest, facts);
        using (var connection = new SqliteConnection($"Data Source={indexPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                select source_symbol, target_symbol, rule_id, evidence_tier, properties_json
                from facts
                where fact_id = $fact_id;
                """;
            command.Parameters.AddWithValue("$fact_id", edge.FactId);
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(edge.SourceSymbol, reader.GetString(0));
            Assert.Equal(edge.TargetSymbol, reader.GetString(1));
            Assert.Equal(RuleIds.BuildMsBuildBinlogObservation, reader.GetString(2));
            Assert.Equal(EvidenceTiers.Tier2Structural, reader.GetString(3));
            var properties = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4));
            Assert.NotNull(properties);
            Assert.Equal("recorded-project-build-edge", properties["relationshipKind"]);
            Assert.False(reader.Read());
        }
        var diagnostic = Assert.Single(facts, fact => fact.FactType == FactTypes.MsBuildDiagnosticObserved);
        Assert.Equal("warning", diagnostic.Properties["severity"]);
        Assert.Equal("CS0618", diagnostic.Properties["code"]);
        Assert.Equal("src/Child/Child.cs", diagnostic.Evidence.FilePath);
        Assert.Equal(7, diagnostic.Evidence.StartLine);
        Assert.All(facts, fact =>
        {
            Assert.Equal(Commit, fact.CommitSha);
            Assert.Contains("artifactSha256", fact.Properties.Keys);
            Assert.Equal(ScannerVersions.MsBuildBinlogExtractor, fact.Evidence.ExtractorVersion);
        });
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(facts), StringComparison.Ordinal);
        Assert.DoesNotContain(Command, JsonSerializer.Serialize(facts), StringComparison.Ordinal);
        Assert.DoesNotContain(Source, JsonSerializer.Serialize(facts), StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_failed_build_omits_outside_paths_and_all_secret_bearing_text()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp.Path);
        var binlog = Path.Combine(temp.Path, "failed.binlog");
        PrepareBinlog(binlog, repo, succeeded: false, includeOutsideRoot: true, extraMessages: 0);

        var facts = MsBuildBinlogExtractor.Extract(repo, Manifest(), [binlog]);
        var artifact = Assert.Single(facts, fact => fact.FactType == FactTypes.MsBuildBinlogObserved);

        Assert.Equal("failed", artifact.Properties["recordedBuildResult"]);
        Assert.Equal("observed-partial", artifact.Properties["coverageLabel"]);
        Assert.Contains(facts, fact => fact.RuleId == RuleIds.BuildMsBuildBinlogGap
            && fact.Properties["gapKind"] == "binlog-project-path-omitted");
        Assert.Contains(facts, fact => fact.RuleId == RuleIds.BuildMsBuildBinlogGap
            && fact.Properties["gapKind"] == "binlog-diagnostic-path-omitted");
        Assert.Contains(facts, fact => fact.RuleId == RuleIds.BuildMsBuildBinlogGap
            && fact.Properties["gapKind"] == "binlog-diagnostic-code-omitted");
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.MsBuildDiagnosticObserved
            && fact.Properties.GetValueOrDefault("code") is "CS1001" or "ghp_never_render");
        var serialized = JsonSerializer.Serialize(facts);
        Assert.DoesNotContain(Secret, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(Command, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(Source, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-db.internal", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_malformed_and_record_capped_inputs_emit_categorical_gaps()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp.Path);
        var malformed = Path.Combine(temp.Path, "malformed.binlog");
        File.WriteAllBytes(malformed, [0x01, 0x02, 0x03, 0x04]);
        var busy = Path.Combine(temp.Path, "busy.binlog");
        PrepareBinlog(busy, repo, succeeded: true, includeOutsideRoot: false, extraMessages: 12);
        var limits = new MsBuildBinlogLimits(MaxRecords: 3);

        var malformedFacts = MsBuildBinlogExtractor.Extract(repo, Manifest(), [malformed], limits);
        var cappedFacts = MsBuildBinlogExtractor.Extract(repo, Manifest(), [busy], limits);
        var sizeCappedFacts = MsBuildBinlogExtractor.Extract(
            repo,
            Manifest(),
            [busy],
            new MsBuildBinlogLimits(MaxArtifactBytes: 1));
        var expandedCappedFacts = MsBuildBinlogExtractor.Extract(
            repo,
            Manifest(),
            [busy],
            new MsBuildBinlogLimits(MaxExpandedBytes: 1));
        var projectionCappedFacts = MsBuildBinlogExtractor.Extract(
            repo,
            Manifest(),
            [busy],
            new MsBuildBinlogLimits(MaxProjects: 1, MaxEdges: 0, MaxDiagnostics: 0));
        var edgeCappedFacts = MsBuildBinlogExtractor.Extract(
            repo,
            Manifest(),
            [busy],
            new MsBuildBinlogLimits(MaxEdges: 0));
        var missingFacts = MsBuildBinlogExtractor.Extract(
            repo,
            Manifest(),
            [Path.Combine(temp.Path, "missing-a.binlog"), Path.Combine(temp.Path, "missing-b.binlog")],
            new MsBuildBinlogLimits());
        var secondBusy = Path.Combine(temp.Path, "busy-second.binlog");
        PrepareBinlog(secondBusy, repo, succeeded: false, includeOutsideRoot: false, extraMessages: 1);
        var runtimeUnavailableFacts = MsBuildBinlogExtractor.Extract(
            repo,
            Manifest(),
            [busy, secondBusy],
            new MsBuildBinlogLimits(),
            runtimeAvailableOverride: false);

        Assert.Contains(malformedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "binlog-malformed-or-unsupported");
        Assert.Contains(cappedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "binlog-record-cap-reached");
        Assert.Contains(sizeCappedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "binlog-size-cap-exceeded");
        Assert.Contains(expandedCappedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "binlog-expanded-size-cap-exceeded");
        Assert.Contains(projectionCappedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "binlog-project-cap-reached");
        Assert.Contains(projectionCappedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "binlog-diagnostic-cap-reached");
        Assert.Contains(edgeCappedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == "binlog-edge-cap-reached");
        var missingGap = Assert.Single(missingFacts);
        Assert.Equal("binlog-unavailable", missingGap.Properties["gapKind"]);
        Assert.Equal("2", missingGap.Properties["omittedCount"]);
        Assert.Equal(2, runtimeUnavailableFacts.Count);
        Assert.All(runtimeUnavailableFacts, fact =>
        {
            Assert.Equal("binlog-parser-runtime-unavailable", fact.Properties["gapKind"]);
            Assert.NotEqual("unavailable", fact.Properties["artifactSha256"]);
        });
        Assert.Equal(
            runtimeUnavailableFacts.Count,
            runtimeUnavailableFacts.Select(fact => fact.FactId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cappedFacts.Where(fact => fact.FactType == FactTypes.MsBuildBinlogObserved),
            fact => Assert.Equal("true", fact.Properties["partial"]));
    }

    [Fact]
    public void Extract_rejects_symlink_input_without_opening_target()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var temp = new TempDirectory();
        var repo = CreateRepo(temp.Path);
        var target = Path.Combine(temp.Path, "target.binlog");
        PrepareBinlog(target, repo, succeeded: true, includeOutsideRoot: false, extraMessages: 0);
        var link = Path.Combine(temp.Path, "linked.binlog");
        File.CreateSymbolicLink(link, target);

        var facts = MsBuildBinlogExtractor.Extract(repo, Manifest(), [link]);

        var gap = Assert.Single(facts);
        Assert.Equal("binlog-link-input-rejected", gap.Properties["gapKind"]);
        Assert.Equal("unavailable", gap.Properties["artifactSha256"]);
    }

    [Fact]
    public void Extract_rejects_input_beneath_a_symlinked_directory()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var temp = new TempDirectory();
        var repo = CreateRepo(temp.Path);
        var targetDirectory = Path.Combine(temp.Path, "target-binlogs");
        Directory.CreateDirectory(targetDirectory);
        var target = Path.Combine(targetDirectory, "build.binlog");
        PrepareBinlog(target, repo, succeeded: true, includeOutsideRoot: false, extraMessages: 0);
        var linkedDirectory = Path.Combine(temp.Path, "linked-binlogs");
        Directory.CreateSymbolicLink(linkedDirectory, targetDirectory);

        var facts = MsBuildBinlogExtractor.Extract(
            repo,
            Manifest(),
            [Path.Combine(linkedDirectory, "build.binlog")]);

        var gap = Assert.Single(facts);
        Assert.Equal("binlog-link-input-rejected", gap.Properties["gapKind"]);
        Assert.Equal("unavailable", gap.Properties["artifactSha256"]);
    }

    [Fact]
    public void Extract_is_deterministic_for_equivalent_inputs()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp.Path);
        var binlog = Path.Combine(temp.Path, "deterministic.binlog");
        PrepareBinlog(binlog, repo, succeeded: true, includeOutsideRoot: false, extraMessages: 2);

        var first = MsBuildBinlogExtractor.Extract(repo, Manifest(), [binlog]);
        var second = MsBuildBinlogExtractor.Extract(repo, Manifest(), [binlog]);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public async Task Cli_requires_matching_commit_and_standard_outputs_do_not_leak()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp.Path);
        InitializeGit(repo);
        var commit = GitMetadataProvider.Detect(repo).CommitSha;
        var binlog = Path.Combine(temp.Path, "cli.binlog");
        var outputPath = Path.Combine(temp.Path, "output");
        PrepareBinlog(binlog, repo, succeeded: true, includeOutsideRoot: true, extraMessages: 1);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var missingCommitExit = await TraceMapCommand.RunAsync(
            ["scan", "--repo", repo, "--out", outputPath, "--binlog", binlog],
            output,
            error);
        Assert.Equal(1, missingCommitExit);
        Assert.Contains("--binlog-commit-sha", error.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        var mismatchedCommitExit = await TraceMapCommand.RunAsync(
            ["scan", "--repo", repo, "--out", outputPath, "--binlog", binlog, "--binlog-commit-sha", Commit],
            output,
            error);
        Assert.Equal(1, mismatchedCommitExit);
        Assert.Contains("does not match", error.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        var exit = await TraceMapCommand.RunAsync(
            ["scan", "--repo", repo, "--out", outputPath, "--binlog", binlog, "--binlog-commit-sha", commit],
            output,
            error);

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, error.ToString());
        foreach (var file in new[]
                 {
                     "scan-manifest.json",
                     "facts.ndjson",
                     "index.sqlite",
                     "report.md",
                     Path.Combine("logs", "analyzer.log")
                 })
        {
            var bytes = File.ReadAllBytes(Path.Combine(outputPath, file));
            var text = Convert.ToHexString(bytes);
            Assert.DoesNotContain(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(Secret)), text, StringComparison.Ordinal);
            Assert.DoesNotContain(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(Command)), text, StringComparison.Ordinal);
            Assert.DoesNotContain(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(Source)), text, StringComparison.Ordinal);
            Assert.False(
                text.Contains(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(temp.Path)), StringComparison.Ordinal),
                $"{file} contains the temporary local path: {System.Text.Encoding.UTF8.GetString(bytes)}");
        }
    }

    [Fact]
    public void Invalid_input_path_is_deterministic_and_emits_a_gap_instead_of_throwing()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp.Path);
        const string invalidPath = "invalid\0.binlog";

        var firstSignature = MsBuildBinlogExtractor.CreateInputSignature([invalidPath], repo);
        var secondSignature = MsBuildBinlogExtractor.CreateInputSignature([invalidPath], repo);
        var facts = MsBuildBinlogExtractor.Extract(repo, Manifest(), [invalidPath]);

        Assert.Equal(firstSignature, secondSignature);
        Assert.StartsWith("invalid-path:", firstSignature, StringComparison.Ordinal);
        var gap = Assert.Single(facts);
        Assert.Equal(FactTypes.AnalysisGap, gap.FactType);
        Assert.Equal("binlog-path-invalid", gap.Properties["gapKind"]);
        Assert.Equal("1", gap.Properties["omittedCount"]);
    }

    [Fact]
    public void Failed_binlog_downgrades_scan_manifest_and_records_the_gap()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp.Path);
        InitializeGit(repo);
        var commit = GitMetadataProvider.Detect(repo).CommitSha;
        var binlog = Path.Combine(temp.Path, "failed-scan.binlog");
        PrepareBinlog(binlog, repo, succeeded: false, includeOutsideRoot: false, extraMessages: 0);

        var result = ScanEngine.Scan(new ScanOptions(
            repo,
            Path.Combine(temp.Path, "output"),
            BinlogPaths: [binlog],
            BinlogCommitSha: commit));

        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.EndsWith("Reduced", result.Manifest.AnalysisLevel, StringComparison.Ordinal);
        Assert.Contains(
            "An explicitly supplied MSBuild binlog recorded a failed build.",
            result.Manifest.KnownGaps);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.BuildStatus
            && fact.Properties.GetValueOrDefault("status") == "FailedOrPartial");
    }

    private static string CreateRepo(string root)
    {
        var repo = Path.Combine(root, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "src", "Root"));
        Directory.CreateDirectory(Path.Combine(repo, "src", "Child"));
        var project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;
        File.WriteAllText(Path.Combine(repo, "src", "Root", "Root.csproj"), project);
        File.WriteAllText(Path.Combine(repo, "src", "Child", "Child.csproj"), project);
        File.WriteAllText(Path.Combine(repo, "src", "Child", "Child.cs"), "public sealed class Child { }");
        return repo;
    }

    private static void PrepareBinlog(string path, string repo, bool succeeded, bool includeOutsideRoot, int extraMessages)
    {
        Assert.True(MsBuildRuntimeRegistration.TryRegister(out var error), error);
        WriteBinlog(path, repo, succeeded, includeOutsideRoot, extraMessages);
    }

    private static void WriteBinlog(string path, string repo, bool succeeded, bool includeOutsideRoot, int extraMessages)
    {
        var dispatcher = new EventArgsDispatcher();
        var logger = new BinaryLogger
        {
            Parameters = path,
            CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.None
        };
        logger.Initialize(dispatcher);
        dispatcher.Dispatch(new BuildStartedEventArgs($"start {Secret}", string.Empty));

        var rootContext = new BuildEventContext(1, 1, 1, 1, 1);
        var childContext = new BuildEventContext(1, 2, 2, 1, 1);
        var rootProject = new ProjectStartedEventArgs(
            1,
            $"root {Secret}",
            string.Empty,
            Path.Combine(repo, "src", "Root", "Root.csproj"),
            "Build",
            new Dictionary<string, string> { ["ConnectionString"] = Secret },
            new[] { Command, Source },
            null!)
        {
            BuildEventContext = rootContext
        };
        dispatcher.Dispatch(rootProject);

        var childProject = new ProjectStartedEventArgs(
            2,
            $"child {Secret}",
            string.Empty,
            Path.Combine(repo, "src", "Child", "Child.csproj"),
            "Build",
            Array.Empty<object>(),
            Array.Empty<object>(),
            rootContext)
        {
            BuildEventContext = childContext
        };
        dispatcher.Dispatch(childProject);

        var warning = new BuildWarningEventArgs(
            "compiler",
            "CS0618",
            Path.Combine(repo, "src", "Child", "Child.cs"),
            7,
            3,
            7,
            10,
            $"warning {Secret} {Command} {Source}",
            string.Empty,
            "compiler")
        {
            BuildEventContext = childContext,
            ProjectFile = Path.Combine(repo, "src", "Child", "Child.csproj")
        };
        dispatcher.Dispatch(warning);

        if (includeOutsideRoot)
        {
            var outsideContext = new BuildEventContext(1, 3, 3, 1, 1);
            dispatcher.Dispatch(new ProjectStartedEventArgs(
                3,
                Secret,
                string.Empty,
                Path.Combine(Path.GetDirectoryName(repo)!, "outside", "Secret.csproj"),
                "Build",
                Array.Empty<object>(),
                Array.Empty<object>(),
                rootContext)
            {
                BuildEventContext = outsideContext
            });
            dispatcher.Dispatch(new BuildErrorEventArgs(
                "compiler",
                "CS1001",
                "Secret.cs",
                1,
                1,
                1,
                1,
                $"{Secret} {Command} {Source}",
                string.Empty,
                "compiler")
            {
                BuildEventContext = outsideContext,
                ProjectFile = Path.Combine(Path.GetDirectoryName(repo)!, "outside", "Secret.csproj")
            });
            dispatcher.Dispatch(new BuildWarningEventArgs(
                "custom",
                "ghp_never_render",
                Path.Combine(repo, "src", "Child", "Child.cs"),
                1,
                1,
                1,
                1,
                Secret,
                string.Empty,
                "custom")
            {
                BuildEventContext = childContext,
                ProjectFile = Path.Combine(repo, "src", "Child", "Child.csproj")
            });
        }

        for (var index = 0; index < extraMessages; index++)
            dispatcher.Dispatch(new BuildMessageEventArgs($"{Secret} {Command} {Source} {index}", string.Empty, "test", MessageImportance.High));

        dispatcher.Dispatch(new BuildFinishedEventArgs($"finished {Secret}", string.Empty, succeeded));
        logger.Shutdown();
    }

    private static void InitializeGit(string repo)
    {
        RunGit(repo, "init", "-b", "test");
        RunGit(repo, "config", "user.email", "tests@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Tests");
        RunGit(repo, "add", ".");
        RunGit(repo, "commit", "-m", "fixture");
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }

    private static ScanManifest Manifest() =>
        new(
            "scan-binlog-test",
            "fixture",
            null,
            "test",
            Commit,
            ScannerVersions.TraceMap,
            DateTimeOffset.UnixEpoch,
            "Level3SyntaxAnalysis",
            "NotRun",
            [],
            [],
            [],
            []);
}
