using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
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
        var limited = PackageDecisionRecordReader.Read("{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-long\",\"decisionKind\":\"admit\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\",\"recordDigest\":\"" + new string('a', 257) + "\"}]} ");
        Assert.Equal("DecisionInputLimitReached", limited.Gaps.Single().Classification);
        Assert.False(limited.Accepted);
        Assert.Equal("DecisionInputMalformed", PackageDecisionRecordReader.Read("{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-sha512\",\"decisionKind\":\"admit\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"artifactDigestAlgorithm\":\"sha512-base64\",\"artifactDigest\":\"AAAA\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]} ").Gaps.Single().Classification);

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
            ["resolvedVersion"] = "1.0.0",
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
        var report = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output, "package-decision-report.json")))!;
        Assert.Equal("TestExtractor", report["exactMatches"]![0]!["evidence"]!["extractorId"]!.GetValue<string>());
        Assert.Equal("1.0.0", report["exactMatches"]![0]!["evidence"]!["resolvedVersion"]!.GetValue<string>());
    }

    [Fact]
    public async Task Composition_preserves_labels_portfolio_identity_and_optional_context()
    {
        using var temp = new TempDirectory();
        var firstIndex = Path.Combine(temp.Path, "first.sqlite");
        var secondIndex = Path.Combine(temp.Path, "second.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var manifestPath = Path.Combine(temp.Path, "portfolio.json");
        var first = Manifest("first", "typescript-scanner");
        var second = Manifest("second", "typescript-scanner");
        var digest = new string('a', 64);
        SqliteIndexWriter.Write(firstIndex, first, [DigestPackageFact(first, "example", "1.0.0", digest)]);
        SqliteIndexWriter.Write(secondIndex, second, [PackageFact(second, "other", "npm", "package.json", "dependencies", "1.0.0")]);
        await File.WriteAllTextAsync(decisionPath, "{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-composed\",\"decisionKind\":\"reject\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"artifactDigestAlgorithm\":\"sha256\",\"artifactDigest\":\"" + digest + "\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]}");
        await File.WriteAllTextAsync(manifestPath, "{\"version\":\"1.0\",\"portfolioId\":\"fixture\",\"snapshotId\":\"one\",\"inputs\":[{\"label\":\"web\",\"indexPath\":\"first.sqlite\"},{\"label\":\"api\",\"indexPath\":\"second.sqlite\"}]}");

        var repeated = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath,
            firstIndex,
            Path.Combine(temp.Path, "repeated"),
            IndexPaths: [firstIndex, secondIndex],
            Labels: ["web", "api"],
            IncludePaths: true,
            IncludeReverse: true));
        Assert.Contains(repeated.Report.Sources, source => source.Label == "web" && source.ContainerLabel == "web");
        Assert.Contains(repeated.Report.Sources, source => source.Label == "api" && source.ContainerLabel == "api");
        Assert.Single(repeated.Report.ExactMatches);
        Assert.Equal("web", repeated.Report.ExactMatches[0].SourceLabel);
        Assert.Equal("unavailable", ((PackageDecisionContext)repeated.Report.PathContext!).Status);
        Assert.Equal("unavailable", ((PackageDecisionContext)repeated.Report.ReverseContext!).Status);
        Assert.Contains(((PackageDecisionContext)repeated.Report.PathContext!).Gaps, gap => gap.Classification == "UnknownAnalysisGap" && gap.SupportingFactIds!.Count > 0);

        var portfolio = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath,
            string.Empty,
            Path.Combine(temp.Path, "portfolio"),
            ManifestPath: manifestPath));
        Assert.Equal(2, portfolio.Report.Summary.SourceCount);
        Assert.Contains(portfolio.Report.ExactMatches, row => row.SourceLabel == "web");
        Assert.Contains(portfolio.Report.ExcludedSources, row => row.SourceLabel == "api");
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "portfolio", "package-decision-report.json"));
        Assert.DoesNotContain(firstIndex, json, StringComparison.Ordinal);
        Assert.DoesNotContain(secondIndex, json, StringComparison.Ordinal);

        var secondManifestPath = Path.Combine(temp.Path, "portfolio-second.json");
        await File.WriteAllTextAsync(secondManifestPath, "{\"version\":\"1.0\",\"portfolioId\":\"fixture\",\"snapshotId\":\"two\",\"inputs\":[{\"label\":\"web\",\"indexPath\":\"first.sqlite\"},{\"label\":\"api\",\"indexPath\":\"second.sqlite\"}]}");
        var secondPortfolio = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "portfolio-second"), ManifestPath: secondManifestPath));
        Assert.NotEqual(portfolio.Report.Query.IndexPathHash, secondPortfolio.Report.Query.IndexPathHash);
    }

    [Fact]
    public async Task Context_traverses_bounded_combined_graph_and_preserves_provenance()
    {
        using var temp = new TempDirectory();
        var sourceIndex = Path.Combine(temp.Path, "source.sqlite");
        var combinedIndex = Path.Combine(temp.Path, "combined.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var manifest = Manifest("context", "tracemap");
        var digest = new string('c', 64);
        var package = DigestPackageFact(manifest, "example", "1.0.0", digest) with { SourceSymbol = "method:terminal" };
        var otherPackage = DigestPackageFact(manifest, "other", "2.0.0", new string('d', 64)) with { SourceSymbol = "method:terminal" };
        var first = RelationshipFact(manifest, "call-root", "method:root", "method:middle", 10);
        var second = RelationshipFact(manifest, "call-middle", "method:middle", "method:terminal", 20);
        SqliteIndexWriter.Write(sourceIndex, manifest, [first, second, package, otherPackage]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([sourceIndex], combinedIndex, ["app"]));
        await File.WriteAllTextAsync(decisionPath, "{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-context\",\"decisionKind\":\"reject\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"artifactDigestAlgorithm\":\"sha256\",\"artifactDigest\":\"" + digest + "\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"},{\"decisionId\":\"dec-other\",\"decisionKind\":\"reject\",\"ecosystem\":\"npm\",\"packageName\":\"other\",\"artifactVersion\":\"2.0.0\",\"artifactDigestAlgorithm\":\"sha256\",\"artifactDigest\":\"" + new string('d', 64) + "\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]}");

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, combinedIndex, Path.Combine(temp.Path, "report"), IncludePaths: true, IncludeReverse: true, MaxDepth: 8, MaxPaths: 1));

        var pathContext = (PackageDecisionContext)result.Report.PathContext!;
        var path = Assert.Single(pathContext.Rows);
        Assert.Equal("truncated", pathContext.Status);
        Assert.DoesNotContain(pathContext.Gaps, gap => gap.Classification == "UnknownAnalysisGap" && gap.Message.StartsWith("No bounded static", StringComparison.Ordinal));
        Assert.Equal("3", path.Metadata.Single(pair => pair.Key == "pathLength").Value);
        Assert.True(path.SupportingEdgeIds.Count >= 3);
        Assert.Equal("package-lock.json", path.FilePath);
        Assert.Equal(5, path.StartLine);
        Assert.Equal(manifest.CommitSha, path.CommitSha);
        Assert.Equal("TestExtractor", path.ExtractorId);
        Assert.Equal("1.0.0", path.ExtractorVersion);
        Assert.NotEmpty(path.SupportingFactIds);
        Assert.Equal(CombinedDependencyPathClassifications.ProbableStaticPath, path.Classification);
        Assert.Equal("combined.paths.surface-evidence.v1", path.RuleId);
        Assert.Contains("combined.paths.surface-evidence.v1", path.RuleIds!);

        var reversePath = Assert.Single(((PackageDecisionContext)result.Report.ReverseContext!).Rows, row => row.PackageName == "example");
        Assert.Equal(CombinedReverseClassifications.NeedsReviewReversePath, reversePath.Classification);
        Assert.Equal(CombinedReverseReporter.PathRuleId, reversePath.RuleId);
        Assert.Contains(CombinedReverseReporter.PathRuleId, reversePath.RuleIds!);

        var repeated = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath,
            combinedIndex,
            Path.Combine(temp.Path, "repeated-combined"),
            IndexPaths: [combinedIndex, combinedIndex],
            Labels: ["first", "second"],
            IncludePaths: true));
        Assert.Contains(repeated.Report.Gaps, gap => gap.Message.Contains("Duplicate portfolio source identity", StringComparison.Ordinal));
        Assert.NotEmpty(((PackageDecisionContext)repeated.Report.PathContext!).Rows);
        Assert.DoesNotContain(((PackageDecisionContext)repeated.Report.PathContext!).Gaps, gap => gap.Message.Contains("inventory was unavailable", StringComparison.Ordinal));

        var bounded = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, combinedIndex, Path.Combine(temp.Path, "bounded"), IncludePaths: true, MaxDepth: 1));
        Assert.Equal("truncated", ((PackageDecisionContext)bounded.Report.PathContext!).Status);
        Assert.Contains(((PackageDecisionContext)bounded.Report.PathContext!).Gaps, gap => gap.Classification == "TruncatedByLimit");
    }

    [Fact]
    public async Task Correlation_fails_closed_for_invalid_envelope_and_protects_inputs()
    {
        using var temp = new TempDirectory();
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("fixture", "typescript-scanner");
        SqliteIndexWriter.Write(indexPath, manifest, [PackageFact(manifest, "example", "npm", "package.json", "dependencies", "1.0.0")]);
        await File.WriteAllTextAsync(decisionPath, "{}");

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "report"))));

        var validDecision = "{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-alias\",\"decisionKind\":\"admit\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]}";
        await File.WriteAllTextAsync(decisionPath, validDecision);
        await Assert.ThrowsAsync<InvalidDataException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, decisionPath)));
        Assert.Equal(validDecision, await File.ReadAllTextAsync(decisionPath));

        var manifestPath = Path.Combine(temp.Path, "portfolio.json");
        await File.WriteAllTextAsync(manifestPath, "{\"version\":\"1.0\",\"portfolioId\":\"fixture\",\"snapshotId\":\"one\",\"inputs\":[{\"label\":\"app\",\"indexPath\":\"index.sqlite\"}]}");
        var manifestBytes = await File.ReadAllBytesAsync(manifestPath);
        await Assert.ThrowsAsync<InvalidDataException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, string.Empty, manifestPath, ManifestPath: manifestPath)));
        Assert.Equal(manifestBytes, await File.ReadAllBytesAsync(manifestPath));
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            var caseVariant = Path.Combine(temp.Path, "PORTFOLIO.JSON");
            await Assert.ThrowsAsync<InvalidDataException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, string.Empty, caseVariant, ManifestPath: manifestPath)));
            Assert.Equal(manifestBytes, await File.ReadAllBytesAsync(manifestPath));
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "labels"), IndexPaths: [indexPath, indexPath], Labels: ["app", "app"])));

        var derivedOutput = Path.Combine(temp.Path, "derived-output");
        Directory.CreateDirectory(derivedOutput);
        var derivedManifest = Path.Combine(derivedOutput, "package-decision-report.json");
        await File.WriteAllTextAsync(derivedManifest, "{\"version\":\"1.0\",\"portfolioId\":\"fixture\",\"snapshotId\":\"one\",\"inputs\":[{\"label\":\"app\",\"indexPath\":\"../index.sqlite\"}]}");
        var derivedManifestBytes = await File.ReadAllBytesAsync(derivedManifest);
        await Assert.ThrowsAsync<InvalidDataException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, string.Empty, derivedOutput, ManifestPath: derivedManifest)));
        Assert.Equal(derivedManifestBytes, await File.ReadAllBytesAsync(derivedManifest));

        if (!OperatingSystem.IsWindows())
        {
            var actualOutput = Path.Combine(temp.Path, "actual-output");
            Directory.CreateDirectory(actualOutput);
            var linkedManifest = Path.Combine(actualOutput, "package-decision-report.json");
            await File.WriteAllTextAsync(linkedManifest, "{\"version\":\"1.0\",\"portfolioId\":\"fixture\",\"snapshotId\":\"one\",\"inputs\":[{\"label\":\"app\",\"indexPath\":\"../index.sqlite\"}]}");
            var linkedManifestBytes = await File.ReadAllBytesAsync(linkedManifest);
            var linkedOutput = Path.Combine(temp.Path, "linked-output");
            Directory.CreateSymbolicLink(linkedOutput, actualOutput);
            await Assert.ThrowsAsync<InvalidDataException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, string.Empty, linkedOutput, ManifestPath: linkedManifest)));
            Assert.Equal(linkedManifestBytes, await File.ReadAllBytesAsync(linkedManifest));
        }

        const string unsafeLabel = "/private/client-app";
        var safeLabelReport = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, indexPath, Path.Combine(temp.Path, "safe-label"), Source: unsafeLabel, IndexPaths: [indexPath], Labels: [unsafeLabel]));
        Assert.StartsWith("source-label-hash:", Assert.Single(safeLabelReport.Report.Sources).Label, StringComparison.Ordinal);
        Assert.StartsWith("source-label-hash:", safeLabelReport.Report.Query.Source, StringComparison.Ordinal);
        Assert.DoesNotContain(unsafeLabel, await File.ReadAllTextAsync(Path.Combine(temp.Path, "safe-label", "package-decision-report.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Correlation_reports_selector_and_ecosystem_capability_gaps()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var manifest = Manifest("fixture", "typescript-scanner");
        SqliteIndexWriter.Write(indexPath, manifest, [PackageFact(manifest, "example", "npm", "package.json", "dependencies", "1.0.0")]);
        await File.WriteAllTextAsync(decisionPath, "{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"dec-selector\",\"decisionKind\":\"admit\",\"ecosystem\":\"nuget\",\"packageName\":\"Example.Package\",\"artifactVersion\":\"1.0.0\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]} ");

        var report = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "report"), Source: "missing", Classification: "ExactArtifactMatch"));

        Assert.Contains(report.Report.Gaps, gap => gap.Classification == "SelectorNoMatch" && gap.SourceLabel == "missing");
        Assert.Contains(report.Report.Gaps, gap => gap.Classification == "SelectorNoMatch" && gap.Message.Contains("classification", StringComparison.Ordinal));
        Assert.Empty(report.Report.ExcludedSources);

        var capabilityReport = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "capability")));
        Assert.Contains(capabilityReport.Report.Gaps, gap => gap.Classification == "UnknownAnalysisGap");
        Assert.Empty(capabilityReport.Report.ExcludedSources);
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

    private static CodeFact DigestPackageFact(ScanManifest manifest, string name, string version, string digest) => new($"pkg-{manifest.RepoName}-{name}", manifest.ScanId, manifest.RepoName, manifest.CommitSha, null, FactTypes.PackageReferenced, RuleIds.ProjectFile, EvidenceTiers.Tier2Structural, null, name, "PackageManifest", new EvidenceSpan("package-lock.json", 5, 5, null, "TestExtractor", "1.0.0"), new SortedDictionary<string, string>(StringComparer.Ordinal) { ["dependencyGroup"] = "dependencies", ["dependencyRelation"] = "direct", ["ecosystem"] = "npm", ["manifestKind"] = "package-lock.json", ["packageName"] = name, ["packageManager"] = "npm", ["sourceKind"] = "lockfile", ["surfaceKind"] = "package-config", ["resolvedVersion"] = version, ["version"] = version, ["artifactDigestAlgorithm"] = "sha256", ["artifactDigest"] = digest, ["lockfilePath"] = "package-lock.json", ["lockfileHash"] = new string('b', 32) });

    private static CodeFact RelationshipFact(ScanManifest manifest, string id, string source, string target, int line) =>
        new(id, manifest.ScanId, manifest.RepoName, manifest.CommitSha, null, FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, EvidenceTiers.Tier1Semantic, source, target, "Calls", new EvidenceSpan("Flow.cs", line, line, null, "TestExtractor", "1.0.0"), new SortedDictionary<string, string>(StringComparer.Ordinal));

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
