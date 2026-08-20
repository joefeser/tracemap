using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

/// <summary>
/// Correlation coverage for the PR4 Python and JVM lockfile evidence shapes: native
/// PackageReferenced rows with sourceKind=lockfile that never carry an artifact digest,
/// so both ecosystems correlate at the possible rung with explicit capability gaps.
/// </summary>
public sealed class PackageDecisionPythonJvmEvidenceTests
{
    [Fact]
    public async Task PackageDecision_correlates_python_and_jvm_lockfile_evidence_as_possible_only()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var manifest = Manifest("fixture");
        SqliteIndexWriter.Write(indexPath, manifest,
        [
            PythonLockFact(manifest, "requests", "2.32.3", "direct"),
            PythonLockFact(manifest, "urllib3", "2.2.2", "transitive"),
            GradleLockFact(manifest, "org.springframework", "spring-web", "6.2.0"),
            MavenBuildFact(manifest, "com.example", "fixture-lib", "1.2.3"),
        ]);
        await File.WriteAllTextAsync(decisionPath, """
            {
              "version": "package-decision.v1",
              "records": [
                {
                  "decisionId": "dec-python-lock",
                  "decisionKind": "revoke",
                  "ecosystem": "python",
                  "packageName": "requests",
                  "artifactVersion": "2.32.3",
                  "registryOrigin": "pypi.org",
                  "artifactDigestAlgorithm": "sha256",
                  "artifactDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "producer": { "id": "sample-producer", "policyVersion": "2026-08" },
                  "decisionTimeUtc": "2026-08-18T00:00:00Z"
                },
                {
                  "decisionId": "dec-gradle-lock",
                  "decisionKind": "reject",
                  "ecosystem": "gradle",
                  "packageName": "org.springframework:spring-web",
                  "artifactVersion": "6.2.0",
                  "artifactDigestAlgorithm": "sha256",
                  "artifactDigest": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                  "producer": { "id": "sample-producer", "policyVersion": "2026-08" },
                  "decisionTimeUtc": "2026-08-18T00:00:00Z"
                },
                {
                  "decisionId": "dec-maven-build",
                  "decisionKind": "admit",
                  "ecosystem": "maven",
                  "packageName": "com.example:fixture-lib",
                  "artifactVersion": "1.2.3",
                  "producer": { "id": "sample-producer", "policyVersion": "2026-08" },
                  "decisionTimeUtc": "2026-08-18T00:00:00Z"
                }
              ]
            }
            """);

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "report"), ExitCode: true));

        Assert.Empty(result.Report.ExactMatches);
        Assert.Empty(result.Report.DigestMismatches);
        Assert.Empty(result.Report.AmbiguousReferences);
        var pythonRow = Assert.Single(result.Report.PossibleMatches, row => row.DecisionId == "dec-python-lock");
        Assert.Equal("resolved-version", pythonRow.MatchBasis);
        Assert.Equal("exact", pythonRow.RegistryOriginJoin);
        Assert.Equal("direct", pythonRow.DependencyRelation);
        Assert.Equal("python.package.metadata.v1", pythonRow.Evidence.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, pythonRow.Evidence.EvidenceTier);
        Assert.Equal("PythonLockfileExtractor", pythonRow.Evidence.ExtractorId);
        Assert.Equal("python-lockfile/0.1.0", pythonRow.Evidence.ExtractorVersion);
        Assert.Equal("uv.lock", pythonRow.Evidence.LockfilePath);
        Assert.Equal(32, pythonRow.Evidence.LockfileHash!.Length);
        Assert.Null(pythonRow.Evidence.ArtifactDigest);
        var gradleRow = Assert.Single(result.Report.PossibleMatches, row => row.DecisionId == "dec-gradle-lock");
        Assert.Equal("resolved-version", gradleRow.MatchBasis);
        Assert.Equal("unknown", gradleRow.DependencyRelation);
        Assert.Equal("jvm.buildfile.v1", gradleRow.Evidence.RuleId);
        Assert.Equal("GradleLockfileExtractor", gradleRow.Evidence.ExtractorId);
        Assert.Equal("jvm-gradle-lockfile/0.1.0", gradleRow.Evidence.ExtractorVersion);
        Assert.Equal("gradle.lockfile", gradleRow.Evidence.LockfilePath);
        Assert.Null(gradleRow.Evidence.ArtifactDigest);
        var mavenRow = Assert.Single(result.Report.PossibleMatches, row => row.DecisionId == "dec-maven-build");
        Assert.Equal("declared-exact", mavenRow.MatchBasis);
        Assert.Equal("unknown", mavenRow.DependencyRelation);
        Assert.Null(mavenRow.Evidence.LockfilePath);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "LockfileDigestUnavailable" && gap.DecisionId == "dec-python-lock");
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "LockfileDigestUnavailable" && gap.DecisionId == "dec-gradle-lock");
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "LockfileDigestUnavailable" && gap.DecisionId == "dec-maven-build");
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "DirectTransitiveUnavailable" && gap.DecisionId == "dec-gradle-lock");
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "DirectTransitiveUnavailable" && gap.DecisionId == "dec-maven-build");
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.Classification == "DirectTransitiveUnavailable" && gap.DecisionId == "dec-python-lock");
        Assert.False(result.ExitCodeTriggered);
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "package-decision-report.json"));
        Assert.DoesNotContain(temp.Path, json, StringComparison.Ordinal);

        var repeated = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "report-repeat"), ExitCode: true));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(temp.Path, "report-repeat", "package-decision-report.json")));
        Assert.Empty(repeated.Report.ExactMatches);
    }

    [Fact]
    public async Task PackageDecision_python_record_digest_changes_do_not_change_the_rung()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("fixture-digest");
        SqliteIndexWriter.Write(indexPath, manifest, [PythonLockFact(manifest, "requests", "2.32.3", "direct")]);
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        await File.WriteAllTextAsync(decisionPath, DecisionJson("cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"));
        var digestPath = Path.Combine(temp.Path, "decision-other-digest.json");
        await File.WriteAllTextAsync(digestPath, DecisionJson("dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"));

        var first = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "report-one"), ExitCode: true));
        var second = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(digestPath, indexPath, Path.Combine(temp.Path, "report-two"), ExitCode: true));

        foreach (var result in new[] { first, second })
        {
            Assert.Empty(result.Report.ExactMatches);
            Assert.Empty(result.Report.DigestMismatches);
            var row = Assert.Single(result.Report.PossibleMatches);
            Assert.Equal("resolved-version", row.MatchBasis);
            Assert.Null(row.Evidence.ArtifactDigest);
            Assert.False(result.ExitCodeTriggered);
        }
    }

    [Fact]
    public async Task PackageDecision_python_name_normalization_matches_pep503_folds()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var manifest = Manifest("fixture-normalize");
        SqliteIndexWriter.Write(indexPath, manifest, [PythonLockFact(manifest, "Flask_SQLAlchemy", "3.0.3", "direct")]);
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        await File.WriteAllTextAsync(decisionPath, DecisionJson("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "flask-sqlalchemy", "3.0.3"));

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "report")));

        var row = Assert.Single(result.Report.PossibleMatches);
        Assert.Equal("resolved-version", row.MatchBasis);
        Assert.Equal("flask-sqlalchemy", row.PackageName);
        Assert.Equal("3.0.3", row.Evidence.ResolvedVersion);
    }

    private static string DecisionJson(string digest, string packageName = "requests", string version = "2.32.3") => $$"""
        {
          "version": "package-decision.v1",
          "records": [
            {
              "decisionId": "dec-python-lock",
              "decisionKind": "revoke",
              "ecosystem": "python",
              "packageName": "{{packageName}}",
              "artifactVersion": "{{version}}",
              "artifactDigestAlgorithm": "sha256",
              "artifactDigest": "{{digest}}",
              "producer": { "id": "sample-producer", "policyVersion": "2026-08" },
              "decisionTimeUtc": "2026-08-18T00:00:00Z"
            }
          ]
        }
        """;

    private static ScanManifest Manifest(string repo) => new($"scan-{repo}", repo, null, "main", new string('0', 40), "python-adapter/0.1.0", DateTimeOffset.Parse("2026-08-01T00:00:00Z"), "Level1SemanticAnalysisReduced", "FailedOrPartial", [], [], ["python"], []);

    private static CodeFact PythonLockFact(ScanManifest manifest, string name, string version, string relation) => new(
        $"py-lock-{name}-{version}",
        manifest.ScanId,
        manifest.RepoName,
        manifest.CommitSha,
        null,
        FactTypes.PackageReferenced,
        "python.package.metadata.v1",
        EvidenceTiers.Tier2Structural,
        null,
        name.ToLowerInvariant(),
        name.ToLowerInvariant(),
        new EvidenceSpan("uv.lock", 19, 19, null, "PythonLockfileExtractor", "python-lockfile/0.1.0"),
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["dependencyGroup"] = "lockfile",
            ["dependencyRelation"] = relation,
            ["ecosystem"] = "python",
            ["lockfileHash"] = new string('e', 32),
            ["lockfilePath"] = "uv.lock",
            ["manifestKind"] = "uv.lock",
            ["name"] = name.ToLowerInvariant(),
            ["package"] = name.ToLowerInvariant(),
            ["packageManager"] = "uv",
            ["packageName"] = name.ToLowerInvariant(),
            ["registryOrigin"] = "pypi.org",
            ["resolvedVersion"] = version,
            ["sourceKind"] = "lockfile",
            ["surfaceKind"] = "package-config",
            ["version"] = version,
        });

    private static CodeFact GradleLockFact(ScanManifest manifest, string group, string artifact, string version) => new(
        $"gradle-lock-{group}-{artifact}-{version}",
        manifest.ScanId,
        manifest.RepoName,
        manifest.CommitSha,
        "gradle.lockfile",
        FactTypes.PackageReferenced,
        "jvm.buildfile.v1",
        EvidenceTiers.Tier2Structural,
        null,
        $"{group}:{artifact}",
        $"{group}:{artifact}",
        new EvidenceSpan("gradle.lockfile", 4, 4, null, "GradleLockfileExtractor", "jvm-gradle-lockfile/0.1.0"),
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["artifactId"] = artifact,
            ["buildTool"] = "gradle",
            ["dependencyGroup"] = "lockfile",
            ["ecosystem"] = "gradle",
            ["groupId"] = group,
            ["lockfileHash"] = new string('f', 32),
            ["lockfilePath"] = "gradle.lockfile",
            ["manifestKind"] = "gradle.lockfile",
            ["name"] = $"{group}:{artifact}",
            ["packageManager"] = "gradle",
            ["packageName"] = $"{group}:{artifact}",
            ["resolvedVersion"] = version,
            ["sourceKind"] = "lockfile",
            ["surfaceKind"] = "package-config",
            ["version"] = version,
        });

    private static CodeFact MavenBuildFact(ScanManifest manifest, string group, string artifact, string version) => new(
        $"maven-build-{group}-{artifact}-{version}",
        manifest.ScanId,
        manifest.RepoName,
        manifest.CommitSha,
        "pom.xml",
        FactTypes.PackageReferenced,
        "jvm.buildfile.v1",
        EvidenceTiers.Tier2Structural,
        null,
        $"{group}:{artifact}",
        $"{group}:{artifact}",
        new EvidenceSpan("pom.xml", 1, 1, null, "BuildFileExtractor", "jvm-buildfile/0.1.0"),
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["artifactId"] = artifact,
            ["buildTool"] = "maven",
            ["dependencyGroup"] = "",
            ["dependencyScope"] = "runtime",
            ["ecosystem"] = "maven",
            ["groupId"] = group,
            ["manifestKind"] = "pom.xml",
            ["name"] = $"{group}:{artifact}",
            ["packageManager"] = "maven",
            ["packageName"] = $"{group}:{artifact}",
            ["sourceKind"] = "build-file",
            ["surfaceKind"] = "package-config",
            ["version"] = version,
        });
}
