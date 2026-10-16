using System.Text.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

// The integration cases create Git repositories and invoke a bounded SDK build.
[Collection("Git metadata sensitive")]
public sealed class DepsJsonEvidenceTests
{
    private const string Target = ".NETCoreApp,Version=v8.0";
    private static string Fixture => File.ReadAllText(Path.Combine(FindRoot(), "samples/deps-json-evidence/bin/Debug/net8.0/Sample.deps.json"));

    [Fact]
    public void Standard_manifest_emits_package_graph_relations_and_exact_provenance()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, Fixture);
        var result = Read(temp.Path);
        Assert.Empty(result.Gaps);
        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, x => x.Package == "Example.Direct" && x.Version == "1.2.0" && x.Relation == "direct");
        Assert.Contains(result.Rows, x => x.Package == "Example.Transitive" && x.Version == "2.3.0" && x.Relation == "transitive");
        Assert.All(result.Rows, x => Assert.Equal(Target, x.Target));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(temp.Path, "bin/Debug/net8.0/Sample.deps.json")))).ToLowerInvariant(), result.Rows[0].Hash);
        Assert.Equal(64, result.GeneratorSha256.Length);
        Assert.Equal(result.BoundedInputSha256, Read(temp.Path).BoundedInputSha256);
    }

    [Fact]
    public void Multiple_configurations_targets_and_rids_remain_separate()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, Fixture);
        Write(temp.Path, Fixture.Replace(Target, ".NETCoreApp,Version=v9.0/linux-x64"), "bin/Release/net9.0/linux-x64/Sample.deps.json");
        var result = Read(temp.Path);
        Assert.Empty(result.Gaps);
        Assert.Equal(4, result.Rows.Count);
        Assert.Equal(4, result.Rows.Select(x => (x.Path, x.Target, x.Package)).Distinct().Count());
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("duplicate")]
    [InlineData("case-collision")]
    [InlineData("missing-library")]
    [InlineData("version-mismatch")]
    [InlineData("library-version-mismatch")]
    [InlineData("unsafe-version")]
    [InlineData("top-level-only")]
    public void Invalid_manifest_is_an_explicit_gap_with_no_partial_package_rows(string kind)
    {
        using var temp = new TempDirectory();
        var content = kind switch
        {
            "truncated" => Fixture[..^10],
            "duplicate" => Fixture.Replace("\"targets\": {", "\"targets\": {}, \"targets\": {"),
            "case-collision" => Fixture.Replace("\"targets\": {", "\"TARGETS\": {}, \"targets\": {"),
            "missing-library" => Fixture.Replace("\"type\": \"package\"", "\"other\": \"package\""),
            "version-mismatch" => Fixture.Replace("\"Example.Transitive/2.3.0\": {}", "\"Example.Transitive/2.3.0\": { \"version\": \"9.0.0\" }"),
            "library-version-mismatch" => Fixture.Replace("\"type\": \"package\"", "\"type\": \"package\", \"version\": \"9.0.0\""),
            "unsafe-version" => Fixture.Replace("2.3.0", "credential@host"),
            _ => "{\"dependencies\":{\"Example.Direct\":\"1.2.0\"}}"
        };
        Write(temp.Path, content);
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, x => x.Kind == "deps-json-invalid");
    }

    [Theory]
    [InlineData("ambiguous-root")]
    [InlineData("missing-edge")]
    [InlineData("no-root")]
    public void Relation_is_unknown_when_graph_cannot_prove_a_root_or_edge(string kind)
    {
        using var temp = new TempDirectory();
        var text = kind switch
        {
            "ambiguous-root" => Fixture.Replace(", \"Example.Provider\": \"1.0.0\"", ""),
            "missing-edge" => Fixture.Replace("\"Example.Transitive\": \"2.3.0\"", "\"Example.Transitive\": \"9.0.0\""),
            _ => Fixture.Replace("\"type\": \"project\"", "\"type\": \"reference\"")
        };
        Write(temp.Path, text);
        var result = Read(temp.Path);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, x => Assert.Equal("unknown", x.Relation));
        Assert.Contains(result.Gaps, x => x.Kind == "deps-json-relation-unproven");
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("total")]
    [InlineData("libraries")]
    [InlineData("discovery")]
    public void Limits_do_not_claim_complete_evidence(string kind)
    {
        using var temp = new TempDirectory();
        Write(temp.Path, Fixture);
        var limits = kind switch
        {
            "bytes" => new DepsJsonLimits(MaxFileBytes: 10),
            "total" => new DepsJsonLimits(MaxTotalBytes: 10),
            "libraries" => new DepsJsonLimits(MaxLibraries: 1),
            _ => new DepsJsonLimits(MaxDirectoryEntries: 1)
        };
        var result = Read(temp.Path, limits);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, x => x.Kind.EndsWith("-limit", StringComparison.Ordinal));
    }

    [Fact]
    public void File_limit_emits_gap_and_preserves_only_bounded_observations()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, Fixture);
        Write(temp.Path, Fixture, "bin/Release/Sample.deps.json");
        var result = Read(temp.Path, new DepsJsonLimits(MaxFiles: 1));
        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Gaps, x => x.Kind == "deps-json-file-limit");
    }

    [Fact]
    public void Discovery_honors_bin_boundary_output_exclusions_and_links()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, Fixture, "src/Outside.deps.json");
        Write(temp.Path, Fixture, "obj/bin/Hidden.deps.json");
        Write(temp.Path, Fixture, "bin/out/Hidden.deps.json");
        Write(temp.Path, Fixture, "bin/Excluded.deps.json");
        Write(temp.Path, Fixture);
        var options = new ScanOptions(temp.Path, Path.Combine(temp.Path, "bin/out"), ExcludeGlobs: ["**/Excluded.deps.json"]);
        var result = DepsJsonExtractor.Read(options, default);
        Assert.Equal(2, result.Rows.Count);
        Assert.Empty(result.Gaps);
        if (!OperatingSystem.IsWindows())
        {
            File.CreateSymbolicLink(Path.Combine(temp.Path, "bin/Linked.deps.json"), Path.Combine(temp.Path, "bin/Debug/net8.0/Sample.deps.json"));
            result = DepsJsonExtractor.Read(options, default);
            Assert.Equal(2, result.Rows.Count);
            Assert.Contains(result.Gaps, x => x.Kind == "deps-json-linked-path");
        }
    }

    [Fact]
    public async Task Cli_opt_in_emits_qualified_facts_and_does_not_watch_bin()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Write(repo, Fixture);
        InitGit(repo);
        var before = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "off")));
        Assert.DoesNotContain(before.Facts, x => x.Properties.GetValueOrDefault("manifestKind") == "deps.json");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var destination = Path.Combine(temp.Path, "on");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", destination, "--index-deps-json"], output, error));
        var on = ScanEngine.Scan(new ScanOptions(repo, destination) { IndexDepsJson = true });
        Assert.Equal(before.Manifest.SourceSnapshotDigest, on.Manifest.SourceSnapshotDigest);
        Assert.NotEqual(before.Manifest.ScanId, on.Manifest.ScanId);
        var facts = on.Facts.Where(x => x.Properties.GetValueOrDefault("manifestKind") == "deps.json").ToArray();
        Assert.Equal(2, facts.Length);
        Assert.All(facts, fact =>
        {
            Assert.Equal(RuleIds.ProjectFile, fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
            Assert.Equal(ScannerVersions.DepsJsonExtractor, fact.Evidence.ExtractorVersion);
            Assert.Equal("build-output", fact.Properties["evidenceSource"]);
            Assert.Equal("unknown", fact.Properties["buildCommitSha"]);
            Assert.Equal("unknown", fact.Properties["freshness"]);
            Assert.StartsWith("/targets/", fact.Properties["metadataLocation"]);
            Assert.Equal(64, fact.Properties["boundedInputSha256"].Length);
        });
        Assert.DoesNotContain(on.Inventory, x => x.RelativePath.StartsWith("bin/", StringComparison.Ordinal));
        Assert.DoesNotContain(on.SourceSnapshotInventory!, x => x.RelativePath.StartsWith("bin/", StringComparison.Ordinal));
        Write(repo, Fixture.Replace("2.3.0", "2.4.0"));
        var changed = ScanEngine.Scan(new ScanOptions(repo, destination) { IndexDepsJson = true });
        Assert.Equal(on.Manifest.SourceSnapshotDigest, changed.Manifest.SourceSnapshotDigest);
        Assert.NotEqual(on.Manifest.ScanId, changed.Manifest.ScanId);
        Assert.Contains(changed.Facts, x => x.Properties.GetValueOrDefault("resolvedVersion") == "2.4.0");
        Assert.Contains("deps.json", File.ReadAllText(Path.Combine(destination, "facts.ndjson")));
    }

    [Fact]
    public void Invalid_opt_in_evidence_reduces_coverage_without_snapshot_failure()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "{");
        InitGit(temp.Path);
        var result = ScanEngine.Scan(new ScanOptions(temp.Path, temp.Path + "-out") { IndexDepsJson = true });
        Assert.EndsWith("Reduced", result.Manifest.AnalysisLevel, StringComparison.Ordinal);
        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.BuildStatus
            && fact.Properties["reason"].StartsWith("Package/dependency evidence", StringComparison.Ordinal));
        Assert.Contains(result.Facts, x => x.FactType == FactTypes.AnalysisGap && x.Properties.GetValueOrDefault("gapKind") == "deps-json-invalid");
    }

    [Theory]
    [InlineData("name")]
    [InlineData("version")]
    [InlineData("target")]
    public void Trailing_newlines_are_not_safe_package_or_target_identity(string field)
    {
        using var temp = new TempDirectory();
        var json = System.Text.Json.Nodes.JsonNode.Parse(Fixture)!;
        var targets = json["targets"]!.AsObject();
        if (field == "target")
        {
            var value = targets[Target]!.DeepClone();
            targets.Remove(Target);
            targets[Target + "\n"] = value;
        }
        else
        {
            const string oldKey = "Example.Direct/1.2.0";
            var newKey = field == "name" ? "Example.Direct\n/1.2.0" : "Example.Direct/1.2.0\n";
            var group = targets[Target]!.AsObject();
            var value = group[oldKey]!.DeepClone();
            group.Remove(oldKey);
            group[newKey] = value;
            var libraries = json["libraries"]!.AsObject();
            value = libraries[oldKey]!.DeepClone();
            libraries.Remove(oldKey);
            libraries[newKey] = value;
        }
        Write(temp.Path, json.ToJsonString());
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, x => x.Kind == "deps-json-invalid");
    }

    [Fact]
    public void Existing_writer_handle_does_not_block_observed_build_output_reads()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, Fixture);
        using var writer = new FileStream(Path.Combine(temp.Path, "bin/Debug/net8.0/Sample.deps.json"),
            FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        var result = Read(temp.Path);
        Assert.Empty(result.Gaps);
        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void Explicit_scope_does_not_admit_sibling_build_outputs()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, Fixture, "selected/bin/Sample.deps.json");
        Write(temp.Path, Fixture, "sibling/bin/Sample.deps.json");
        var options = new ScanOptions(temp.Path, temp.Path + "-out", ProjectPaths: ["selected/Sample.csproj"]);
        var result = DepsJsonExtractor.Read(options, default);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.StartsWith("selected/", row.Path));
        result = DepsJsonExtractor.Read(options with { IncludeGlobs = ["sibling/**"] }, default);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.StartsWith("sibling/", row.Path));
        result = DepsJsonExtractor.Read(options with { IncludeGlobs = ["", "  "] }, default);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.StartsWith("selected/", row.Path));
    }

    [Fact]
    public void Missing_manifests_and_cancelled_discovery_are_explicit()
    {
        using var temp = new TempDirectory();
        Assert.Equal("deps-json-not-found", Assert.Single(Read(temp.Path).Gaps).Kind);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => DepsJsonExtractor.Read(
            new ScanOptions(temp.Path, temp.Path + "-out"), cancellation.Token));
    }

    [Fact]
    public void Bom_and_multiple_targets_are_supported_but_duplicate_package_versions_are_not()
    {
        using var temp = new TempDirectory();
        var json = System.Text.Json.Nodes.JsonNode.Parse(Fixture)!;
        var targets = json["targets"]!.AsObject();
        targets[".NETCoreApp,Version=v9.0"] = targets[Target]!.DeepClone();
        Write(temp.Path, "\ufeff" + json.ToJsonString());
        Assert.Equal(4, Read(temp.Path).Rows.Count);
        targets[Target]!["Example.Direct/9.0.0"] = new System.Text.Json.Nodes.JsonObject();
        json["libraries"]!["Example.Direct/9.0.0"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "package" };
        Write(temp.Path, json.ToJsonString());
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, x => x.Kind == "deps-json-invalid");
    }

    [Fact]
    public void Build_output_created_mid_scan_does_not_change_the_source_snapshot()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Write(repo, Fixture);
        InitGit(repo);
        var builder = Path.Combine(temp.Path, "builder");
        Directory.CreateDirectory(builder);
        File.WriteAllText(Path.Combine(builder, "Generated.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(builder, "Generated.cs"), "public class Generated { }");
        var initial = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "baseline")) { IndexDepsJson = true });
        using var writer = new BuildDuringScanWriter(() =>
            Run(builder, "dotnet", "build", "Generated.csproj", "--nologo", "-o", Path.Combine(repo, "bin/generated")));
        using var progress = new ScanProgressReporter(writer, null);
        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "during")) { IndexDepsJson = true },
            receiptRecorder: null, progress: progress);
        Assert.True(writer.Built);
        Assert.True(File.Exists(Path.Combine(repo, "bin/generated/Generated.deps.json")));
        Assert.Equal(initial.Manifest.SourceSnapshotDigest, result.Manifest.SourceSnapshotDigest);
        Assert.DoesNotContain(result.SourceSnapshotInventory!, x => x.RelativePath.StartsWith("bin/", StringComparison.Ordinal));
        var reread = Read(repo);
        Assert.DoesNotContain(reread.Gaps, x => x.Path.EndsWith("Generated.deps.json", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("file")]
    [InlineData("total")]
    [InlineData("count")]
    [InlineData("directories")]
    [InlineData("libraries")]
    public void Custom_limits_cannot_exceed_absolute_caps(string limit)
    {
        using var temp = new TempDirectory();
        var limits = limit switch
        {
            "file" => new DepsJsonLimits(MaxFileBytes: int.MaxValue),
            "total" => new DepsJsonLimits(MaxTotalBytes: long.MaxValue),
            "count" => new DepsJsonLimits(MaxFiles: int.MaxValue),
            "directories" => new DepsJsonLimits(MaxDirectoryEntries: int.MaxValue),
            _ => new DepsJsonLimits(MaxLibraries: int.MaxValue)
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => Read(temp.Path, limits));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Output_root_or_ancestor_is_not_traversed(bool ancestor)
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "repo");
        Write(root, Fixture);
        var result = DepsJsonExtractor.Read(new ScanOptions(root, ancestor ? temp.Path : root), default);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "deps-json-output-boundary");
    }

    [Fact]
    public void Unrelated_file_links_do_not_reduce_dependency_coverage()
    {
        if (OperatingSystem.IsWindows()) return;
        using var temp = new TempDirectory();
        Write(temp.Path, Fixture);
        var target = Path.Combine(temp.Path, "bin/Debug/net8.0/Sample.deps.json");
        File.CreateSymbolicLink(Path.Combine(temp.Path, "LICENSE"), target);
        File.CreateSymbolicLink(Path.Combine(temp.Path, "bin/Assembly.dll"), target);
        Assert.Empty(Read(temp.Path).Gaps);
    }

    [Fact]
    public void Opt_in_properties_preserve_the_existing_positional_constructor()
    {
        var constructor = Assert.Single(typeof(ScanOptions).GetConstructors(),
            ctor => ctor.GetParameters().LastOrDefault()?.Name == "WebFormsPublishSourceRelativeBase");
        Assert.DoesNotContain(constructor.GetParameters(), parameter => parameter.Name == "IndexDepsJson");
        Assert.False(new ScanOptions("repo", "out").IndexDepsJson);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    [InlineData(130)]
    public async Task Build_output_cannot_become_current_source_correlation_or_impact_evidence(int copies)
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "repo");
        Write(root, Fixture);
        InitGit(root);
        var scan = ScanEngine.Scan(new ScanOptions(root, Path.Combine(temp.Path, "scan")) { IndexDepsJson = true });
        var index = Path.Combine(temp.Path, "index.sqlite");
        var facts = scan.Facts.SelectMany(fact => fact.FactType == FactTypes.PackageReferenced
            ? Enumerable.Range(0, copies).Reverse().Select(i => fact with { FactId = fact.FactId + $"-{i:D3}" })
            : [fact]).ToArray();
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var decision = Path.Combine(temp.Path, "decision.json");
        await File.WriteAllTextAsync(decision, """
            {"version":"package-decision.v1","records":[{"decisionId":"synthetic-decision","decisionKind":"reject",
            "ecosystem":"nuget","packageName":"Example.Direct","artifactVersion":"1.2.0",
            "producer":{"id":"synthetic-producer","policyVersion":"1"},"decisionTimeUtc":"2026-08-18T00:00:00Z"}]}
            """);
        var correlation = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decision, index, Path.Combine(temp.Path, "decision-report")));
        Assert.Empty(correlation.Report.PossibleMatches);
        Assert.Empty(correlation.Report.ExactMatches);
        var expectedSupportingIds = facts.Where(fact => fact.FactType == FactTypes.PackageReferenced
            && fact.Properties.GetValueOrDefault("evidenceSource") == "build-output")
            .Select(fact => fact.FactId).Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(expectedSupportingIds);
        var freshness = Assert.Single(correlation.Report.Gaps, gap => gap.Classification == "BuildOutputFreshnessUnknown");
        Assert.Equal(expectedSupportingIds.Take(256), freshness.SupportingFactIds);
        Assert.Equal(Math.Max(0, expectedSupportingIds.Length - 256), freshness.SupportingFactIdsOmittedCount);
        var markdown = await File.ReadAllTextAsync(correlation.MarkdownPath!);
        Assert.Contains(expectedSupportingIds[0], markdown);
        if (expectedSupportingIds.Length > 8)
        {
            Assert.DoesNotContain(expectedSupportingIds[^1], markdown);
            Assert.Contains($"{expectedSupportingIds.Length - 8} supporting fact IDs omitted", markdown);
        }
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(correlation.JsonPath!));
        var jsonGap = Assert.Single(json.RootElement.GetProperty("gaps").EnumerateArray(),
            gap => gap.GetProperty("classification").GetString() == "BuildOutputFreshnessUnknown");
        Assert.Equal(Math.Min(256, expectedSupportingIds.Length), jsonGap.GetProperty("supportingFactIds").GetArrayLength());
        Assert.Equal(freshness.SupportingFactIdsOmittedCount, jsonGap.GetProperty("supportingFactIdsOmittedCount").GetInt32());
        Assert.Equal(scan.Manifest.ScanId, freshness.ScanId);
        Assert.Equal(scan.Manifest.CommitSha, freshness.CommitSha);
        var empty = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decision, index, Path.Combine(temp.Path, "empty-report"), DecisionId: "not-selected"));
        Assert.Equal(expectedSupportingIds.Take(256), Assert.Single(empty.Report.Gaps, gap => gap.Classification == "BuildOutputFreshnessUnknown").SupportingFactIds);
        var capped = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decision, index, Path.Combine(temp.Path, "capped-report"), DecisionId: "not-selected", MaxGaps: 1));
        Assert.Equal("SelectorNoMatch", Assert.Single(capped.Report.Gaps).Classification);
        Assert.True(capped.Report.Summary.GapCapReached);
        var delta = Path.Combine(temp.Path, "delta.json");
        await File.WriteAllTextAsync(delta, """
            {"version":"package-delta.v1","changes":[{"id":"synthetic-change","packageName":"Example.Direct",
            "ecosystem":"nuget","changeType":"updated","oldVersion":"1.2.0","newVersion":"2.0.0"}]}
            """);
        var impact = await PackageUpgradeImpactReporter.WriteAsync(new PackageImpactOptions(index, delta, Path.Combine(temp.Path, "impact")));
        Assert.Empty(impact.Report.Findings);
        Assert.Equal("ReducedCoverage", impact.Report.ReportCoverage);
        Assert.Contains(impact.Report.Gaps, gap => gap.Message.Contains("unknown freshness", StringComparison.Ordinal));
    }

    private sealed class BuildDuringScanWriter(Action build) : StringWriter
    {
        public bool Built { get; private set; }
        public override void WriteLine(string? value)
        {
            if (!Built && value?.Contains("specialized-extraction", StringComparison.Ordinal) == true)
            {
                Built = true;
                build();
            }
            base.WriteLine(value);
        }
    }

    private static DepsJsonResult Read(string root, DepsJsonLimits? limits = null) => DepsJsonExtractor.Read(
        new ScanOptions(root, Path.Combine(root, "out")) { DepsJsonLimits = limits }, default);
    private static void Write(string root, string text, string relative = "bin/Debug/net8.0/Sample.deps.json")
    {
        var file = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
    }
    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "TraceMap.sln"))) return Path.GetFullPath(Path.Combine(dir.FullName, "../.."));
        throw new InvalidOperationException("Repository root unavailable");
    }
    private static void InitGit(string root)
    {
        File.WriteAllText(Path.Combine(root, "README.md"), "Synthetic dependency evidence fixture.");
        Run(root, "git", "init");
        Run(root, "git", "add", "README.md");
        Run(root, "git", "-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-m", "fixture");
    }
    private static void Run(string root, string command, params string[] args)
    {
        var info = new ProcessStartInfo(command) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000)) { process.Kill(entireProcessTree: true); throw new TimeoutException(); }
        Assert.True(process.ExitCode == 0, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}
