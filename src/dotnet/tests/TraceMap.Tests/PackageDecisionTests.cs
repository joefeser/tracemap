using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class PackageDecisionTests
{
    [Fact]
    public void Reader_accepts_closed_v1_and_is_stable_across_property_order()
    {
        const string first = "{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-0001\",\"decisionKind\":\"admit\",\"ecosystem\":\"npm\",\"packageName\":\"@example/lib\",\"artifactVersion\":\"1.2.3\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"2026-08\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]}";
        const string second = "{\"records\":[{\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\",\"producer\":{\"policyVersion\":\"2026-08\",\"id\":\"producer\"},\"artifactVersion\":\"1.2.3\",\"packageName\":\"@example/lib\",\"ecosystem\":\"npm\",\"decisionKind\":\"admit\",\"decisionId\":\"dec-0001\"}],\"version\":\"package-decision.v1\"}";

        var one = PackageDecisionRecordReader.Read(first);
        var two = PackageDecisionRecordReader.Read(second);

        var a = Assert.Single(one.Records);
        var b = Assert.Single(two.Records);
        Assert.Equal(a.RecordDigest, b.RecordDigest);
        Assert.Equal("admit", a.DecisionKind);
        Assert.Empty(one.Gaps);
    }

    [Fact]
    public void Reader_rejects_unsafe_values_without_echoing_them()
    {
        const string secret = "git+ssh://user:pass@example.invalid/private/path";
        var json = $"{{\"version\":\"package-decision.v1\",\"records\":[{{\"decisionId\":\"dec-unsafe\",\"decisionKind\":\"reject\",\"ecosystem\":\"npm\",\"packageName\":\"../private\",\"artifactVersion\":\"{secret}\",\"registryOrigin\":\"https://token@example.invalid/registry\",\"producer\":{{\"id\":\"producer\",\"policyVersion\":\"1\"}},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}}]}}";

        var result = PackageDecisionRecordReader.Read(json);

        Assert.Empty(result.Records);
        Assert.Contains(result.Gaps, gap => gap.Classification == "DecisionInputIdentityUnsafe" || gap.Classification == "DecisionInputMalformed");
        Assert.DoesNotContain(result.Gaps, gap => gap.Message.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public void Reader_deduplicates_identical_and_rejects_conflicting_identity()
    {
        const string record = "{\"decisionId\":\"dec-0002\",\"decisionKind\":\"quarantine\",\"ecosystem\":\"nuget\",\"packageName\":\"Example.Package\",\"artifactVersion\":\"1.0.0\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}";
        var duplicate = PackageDecisionRecordReader.Read($"{{\"version\":\"package-decision.v1\",\"records\":[{record},{record}]}}");
        Assert.Single(duplicate.Records);
        Assert.Contains(duplicate.Gaps, gap => gap.Classification == "DecisionInputDuplicateConflict");

        var conflict = PackageDecisionRecordReader.Read($"{{\"version\":\"package-decision.v1\",\"records\":[{record},{record.Replace("quarantine", "revoke", StringComparison.Ordinal)}]}}");
        Assert.Empty(conflict.Records);
        Assert.True(conflict.Gaps.Count >= 2);
        Assert.All(conflict.Gaps, gap => Assert.Equal("DecisionInputDuplicateConflict", gap.Classification));
    }

    [Fact]
    public async Task Reader_emits_closed_failure_classifications_and_verifies_self_digest()
    {
        Assert.Equal("DecisionInputSchemaUnsupported", PackageDecisionRecordReader.Read("{}").Gaps.Single().Classification);
        Assert.Equal("DecisionInputReadFailed", (await PackageDecisionRecordReader.ReadAsync(Path.Combine(Path.GetTempPath(), "missing-package-decision.json"))).Gaps.Single().Classification);
        Assert.Equal("DecisionInputDecisionKindUnsupported", PackageDecisionRecordReader.Read("{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-kind\",\"decisionKind\":\"future\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]} ").Gaps.Single().Classification);
        Assert.Equal("DecisionInputLimitReached", PackageDecisionRecordReader.Read("{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-long\",\"decisionKind\":\"admit\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\",\"recordDigest\":\"" + new string('a', 257) + "\"}]} ").Gaps.Single().Classification);

        var root = JsonNode.Parse("{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-digest\",\"decisionKind\":\"admit\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\",\"recordDigest\":\"" + new string('0', 64) + "\"}]}")!.AsObject();
        var record = root["records"]![0]!.AsObject();
        record["recordDigest"] = CanonicalJsonDigest.Compute(record.ToJsonString(), "recordDigest");
        var valid = PackageDecisionRecordReader.Read(root.ToJsonString());
        Assert.Single(valid.Records);
        record["artifactVersion"] = "2.0.0";
        Assert.Equal("DecisionInputDigestMismatch", PackageDecisionRecordReader.Read(root.ToJsonString()).Gaps.Single().Classification);
    }

    [Fact]
    public async Task Correlation_keeps_possible_and_exit_matrix_bounded()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var output = Path.Combine(temp.Path, "report");
        var manifest = Manifest("fixture", "typescript-scanner");
        SqliteIndexWriter.Write(indexPath, manifest, [PackageFact(manifest, "example", "npm", "package.json", "dependencies", "1.0.0")]);
        await File.WriteAllTextAsync(decisionPath, "{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-0003\",\"decisionKind\":\"reject\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"artifactDigestAlgorithm\":\"sha256\",\"artifactDigest\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]}");

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, output, ExitCode: true));

        Assert.Empty(result.Report.ExactMatches);
        Assert.Single(result.Report.PossibleMatches);
        Assert.Equal("declared-exact", result.Report.PossibleMatches[0].MatchBasis);
        Assert.Equal("unknown", result.Report.PossibleMatches[0].DependencyRelation);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "LockfileDigestUnavailable");
        Assert.False(result.ExitCodeTriggered);
        var json = await File.ReadAllTextAsync(Path.Combine(output, "package-decision-report.json"));
        var markdown = await File.ReadAllTextAsync(Path.Combine(output, "package-decision-report.md"));
        Assert.DoesNotContain("/Users/", json, StringComparison.Ordinal);
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(output, "package-decision-report.json")));
        Assert.Contains("Possible Matches", markdown);
    }

    [Fact]
    public async Task Cli_exit_code_fires_only_for_exact_revoke_with_injected_digest()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var output = Path.Combine(temp.Path, "exact");
        var manifest = Manifest("fixture", "typescript-scanner");
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["dependencyGroup"] = "dependencies",
            ["ecosystem"] = "npm",
            ["manifestKind"] = "package.json",
            ["packageName"] = "example",
            ["packageManager"] = "npm",
            ["sourceKind"] = "manifest",
            ["surfaceKind"] = "package-config",
            ["version"] = "1.0.0",
            ["artifactDigestAlgorithm"] = "sha256",
            ["artifactDigest"] = new string('a', 64)
        };
        var fact = new CodeFact("pkg-fixture-example", manifest.ScanId, manifest.RepoName, manifest.CommitSha, null, FactTypes.PackageReferenced, RuleIds.ProjectFile, EvidenceTiers.Tier2Structural, null, "example", "PackageManifest", new EvidenceSpan("package.json", 5, 5, null, "TestExtractor", "1.0.0"), properties);
        SqliteIndexWriter.Write(indexPath, manifest, [fact]);
        await File.WriteAllTextAsync(decisionPath, "{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-0004\",\"decisionKind\":\"revoke\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"artifactDigestAlgorithm\":\"sha256\",\"artifactDigest\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]}");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = await TraceMapCommand.RunAsync(["package-decision", "--decision", decisionPath, "--index", indexPath, "--out", output, "--format", "json", "--exit-code"], stdout, stderr);

        Assert.Equal(1, exit);
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public async Task Correlation_keeps_digest_mismatch_and_range_ambiguous_separate()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var manifest = Manifest("fixture", "typescript-scanner");
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["ecosystem"] = "npm",
            ["packageName"] = "example",
            ["surfaceKind"] = "package-config",
            ["version"] = "1.0.0",
            ["artifactDigestAlgorithm"] = "sha256",
            ["artifactDigest"] = new string('b', 64)
        };
        var fact = new CodeFact("pkg-fixture-example", manifest.ScanId, manifest.RepoName, manifest.CommitSha, null, FactTypes.PackageReferenced, RuleIds.ProjectFile, EvidenceTiers.Tier2Structural, null, "example", "PackageManifest", new EvidenceSpan("package.json", 5, 5, null, "TestExtractor", "1.0.0"), properties);
        SqliteIndexWriter.Write(indexPath, manifest, [fact]);
        await File.WriteAllTextAsync(decisionPath, "{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-mismatch\",\"decisionKind\":\"reject\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"artifactDigestAlgorithm\":\"sha256\",\"artifactDigest\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]}");
        var mismatch = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "mismatch")));
        Assert.Single(mismatch.Report.DigestMismatches);
        Assert.Empty(mismatch.Report.ExactMatches);
        Assert.False(mismatch.ExitCodeTriggered);

        properties["version"] = "^1.0.0";
        var rangeIndexPath = Path.Combine(temp.Path, "range.sqlite");
        SqliteIndexWriter.Write(rangeIndexPath, manifest, [fact]);
        var ambiguous = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, rangeIndexPath, Path.Combine(temp.Path, "ambiguous")));
        Assert.Single(ambiguous.Report.AmbiguousReferences);
        Assert.Empty(ambiguous.Report.PossibleMatches);
    }

    [Fact]
    public void Package_decision_rules_are_active_and_catalogued()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains("id: package.decision.record.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("id: package.decision.correlation.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("status: active", catalog, StringComparison.Ordinal);
    }

    private static ScanManifest Manifest(string repo, string scannerVersion) => new($"scan-{repo}", repo, null, "main", new string('0', 40), scannerVersion, DateTimeOffset.Parse("2026-08-01T00:00:00Z"), "Level1SemanticAnalysis", "Succeeded", [], [], [], []);

    private static CodeFact PackageFact(ScanManifest manifest, string name, string ecosystem, string file, string group, string version) => new($"pkg-{manifest.RepoName}-{name}", manifest.ScanId, manifest.RepoName, manifest.CommitSha, null, FactTypes.PackageReferenced, RuleIds.ProjectFile, EvidenceTiers.Tier2Structural, null, name, "PackageManifest", new EvidenceSpan(file, 5, 5, null, "TestExtractor", "1.0.0"), new SortedDictionary<string, string>(StringComparer.Ordinal) { ["dependencyGroup"] = group, ["ecosystem"] = ecosystem, ["manifestKind"] = "package.json", ["packageName"] = name, ["packageManager"] = ecosystem, ["sourceKind"] = "manifest", ["surfaceKind"] = "package-config", ["version"] = version });

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "rules", "rule-catalog.yml"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("TraceMap repository root was not found.");
    }
}
