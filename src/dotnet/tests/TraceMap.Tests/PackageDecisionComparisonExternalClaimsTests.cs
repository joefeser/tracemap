using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class PackageDecisionComparisonExternalClaimsTests
{
    private const string CrossSnapshotWording = "cross-snapshot portfolio evidence, not a single coherent release state";
    private const string RuntimeUnprovenWording = "TraceMap did not verify the build, deployment, installation, reachability, or runtime load.";

    [Fact]
    public void Advisory_reader_accepts_committed_fixture_and_is_deterministic()
    {
        var path = FixturePath("advisory-profile-example.json");

        var first = PackageDecisionAdvisoryProfileReader.Read(File.ReadAllText(path));
        var second = PackageDecisionAdvisoryProfileReader.Read(File.ReadAllText(path));

        Assert.True(first.Accepted);
        Assert.Empty(first.Gaps);
        Assert.Equal(2, first.Claims.Count);
        Assert.All(first.Claims, claim => Assert.Equal("package.decision.advisory.v1", claim.RuleId));
        Assert.All(first.Claims, claim => Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, claim.EvidenceTier));
        var exact = first.Claims.Single(claim => claim.ClaimId == "claim-next-rsc-001");
        Assert.Equal("framework-implied-server-surface", exact.ClaimKind);
        Assert.Equal("npm", exact.Ecosystem);
        Assert.Equal("next", exact.PackageName);
        Assert.Equal("exact", exact.VersionPredicateKind);
        Assert.Equal("14.2.3", exact.VersionPredicateVersion);
        Assert.Equal("next-rsc", exact.Framework);
        Assert.Equal("example-advisory-producer", exact.ProducerId);
        Assert.Equal("2026.08.1", exact.ProducerVersion);
        Assert.Matches("^[a-f0-9]{64}$", exact.ProfileDigest);
        var any = first.Claims.Single(claim => claim.ClaimId == "claim-render-any-002");
        Assert.Equal("any", any.VersionPredicateKind);
        Assert.Null(any.VersionPredicateVersion);
        Assert.Equal(first.Claims[0].ProfileDigest, second.Claims[0].ProfileDigest);
        Assert.Equal(first.Claims, second.Claims);
    }

    [Fact]
    public void Advisory_reader_rejects_out_of_grammar_and_unsafe_claims()
    {
        const string envelope = "\"version\":\"advisory-profile.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"claims\":[";
        Assert.False(PackageDecisionAdvisoryProfileReader.Read("{\"version\":\"advisory-profile.v2\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"claims\":[]}").Accepted);
        Assert.False(PackageDecisionAdvisoryProfileReader.Read("{\"version\":\"advisory-profile.v1\",\"claims\":[]}").Accepted);
        Assert.False(PackageDecisionAdvisoryProfileReader.Read("{{" + envelope + "]}").Accepted);
        Assert.Equal("DecisionInputSchemaUnsupported", PackageDecisionAdvisoryProfileReader.Read("{\"version\":\"advisory-profile.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"claims\":[],\"notes\":\"free text\"}").Gaps.Single().Classification);

        var rejectedClaims = new[]
        {
            Claim("claim-severity", extras: "\"severity\":\"high\""),
            Claim("claim-cve", extras: "\"cve\":\"CVE-2026-0001\""),
            Claim("claim-exploit", extras: "\"exploitability\":\"easy\""),
            Claim("claim-remediation", extras: "\"remediation\":\"upgrade now\""),
            Claim("claim-runtime", extras: "\"runtime\":\"server\""),
            Claim("claim-trust", extras: "\"trusted\":true"),
            Claim("claim-notes", extras: "\"notes\":\"producer says this is dangerous\""),
            Claim("claim-kind", claimKind: "observed-runtime-load"),
            Claim("claim-range", predicate: "{\"kind\":\"range\",\"version\":\">=1.0.0\"}"),
            Claim("claim-exact-missing-version", predicate: "{\"kind\":\"exact\"}"),
            Claim("claim-any-with-version", predicate: "{\"kind\":\"any\",\"version\":\"1.0.0\"}"),
            Claim("claim-params-open", parameters: "{\"framework\":\"next-rsc\",\"surface\":\"admin\"}"),
            Claim("claim-params-missing", parameters: "{}"),
            Claim("claim-unsafe-name", packageName: "../private"),
            Claim("claim-unsafe-framework", parameters: "{\"framework\":\"../path\"}")
        };
        var rejected = PackageDecisionAdvisoryProfileReader.Read("{\"version\":\"advisory-profile.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"claims\":[" + string.Join(",", rejectedClaims) + "]}");
        Assert.True(rejected.Accepted);
        Assert.Empty(rejected.Claims);
        Assert.Equal(15, rejected.Gaps.Count);
        Assert.Contains(rejected.Gaps, gap => gap.Classification == "DecisionInputIdentityUnsafe");
        Assert.Contains(rejected.Gaps, gap => gap.Classification == "DecisionInputMalformed");

        var mixed = PackageDecisionAdvisoryProfileReader.Read("{\"version\":\"advisory-profile.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"claims\":["
            + Claim("claim-good") + "," + Claim("claim-bad", extras: "\"severity\":\"high\"") + "]}");
        Assert.Single(mixed.Claims);
        Assert.Single(mixed.Gaps);

        var duplicate = PackageDecisionAdvisoryProfileReader.Read("{\"version\":\"advisory-profile.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"claims\":["
            + Claim("claim-good") + "," + Claim("claim-good") + "]}");
        Assert.Single(duplicate.Claims);
        Assert.Contains(duplicate.Gaps, gap => gap.Classification == "DecisionInputDuplicateConflict");
    }

    [Fact]
    public void Deployment_reference_reader_accepts_committed_fixture_and_hashes_provenance()
    {
        var admission = PackageDecisionDeploymentReferenceReader.Read(File.ReadAllText(FixturePath("deployment-references-example.json")));

        Assert.True(admission.Accepted);
        Assert.Empty(admission.Gaps);
        Assert.Equal(2, admission.References.Count);
        var build = admission.References.Single(reference => reference.ReferenceId == "ref-build-0042");
        Assert.Equal("build-attachment", build.ReferenceKind);
        Assert.Equal("npm", build.Ecosystem);
        Assert.Equal("@example/lib", build.PackageName);
        Assert.Equal("2.14.0", build.ArtifactVersion);
        Assert.Equal("registry.npmjs.org", build.RegistryOrigin);
        Assert.Equal("sha256", build.ArtifactDigestAlgorithm);
        Assert.Equal(new string('a', 64), build.ArtifactDigest);
        Assert.Equal("example-ci-producer", build.ProducerId);
        Assert.StartsWith("repo-hash:", build.SourceRepoHash);
        Assert.DoesNotContain("example.invalid", build.SourceRepoHash, StringComparison.Ordinal);
        Assert.Equal("1111111111111111111111111111111111111111", build.CommitSha);
        var deploy = admission.References.Single(reference => reference.ReferenceId == "ref-deploy-0043");
        Assert.Equal("deployment-manifest", deploy.ReferenceKind);
        Assert.Null(deploy.SourceRepoHash);
        Assert.Null(deploy.CommitSha);
    }

    [Fact]
    public void Deployment_reference_reader_rejects_runtime_claims_and_unsafe_values()
    {
        Assert.False(PackageDecisionDeploymentReferenceReader.Read("{\"version\":\"package-deployment-reference.v2\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"references\":[]}").Accepted);
        Assert.False(PackageDecisionDeploymentReferenceReader.Read("{\"version\":\"package-deployment-reference.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"references\":[]}").Accepted);
        Assert.Equal("DecisionInputSchemaUnsupported", PackageDecisionDeploymentReferenceReader.Read("{\"version\":\"package-deployment-reference.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"references\":[],\"metadata\":\"free text\"}").Gaps.Single().Classification);

        var secret = "git+ssh://user:pass@example.invalid/private/path";
        var rejectedReferences = new[]
        {
            Reference("ref-runtime", "\"referenceKind\":\"runtime-load\",\"artifactVersion\":\"1.0.0\""),
            Reference("ref-observed", "\"referenceKind\":\"observed-execution\",\"artifactVersion\":\"1.0.0\""),
            Reference("ref-env", "\"referenceKind\":\"build-attachment\",\"artifactVersion\":\"1.0.0\",\"environment\":\"production\""),
            Reference("ref-command", "\"referenceKind\":\"build-attachment\",\"artifactVersion\":\"1.0.0\",\"command\":\"npm ci\""),
            Reference("ref-detail", "\"referenceKind\":\"deployment-manifest\",\"artifactVersion\":\"1.0.0\",\"deploymentDetails\":\"k8s rollout\""),
            Reference("ref-notes", "\"referenceKind\":\"deployment-manifest\",\"artifactVersion\":\"1.0.0\",\"notes\":\"deployed to the cluster\""),
            Reference("ref-origin", "\"referenceKind\":\"build-attachment\",\"artifactVersion\":\"1.0.0\",\"registryOrigin\":\"https://token@example.invalid/registry\""),
            Reference("ref-version", $"\"referenceKind\":\"build-attachment\",\"artifactVersion\":\"{secret}\""),
            Reference("ref-repo", "\"referenceKind\":\"build-attachment\",\"artifactVersion\":\"1.0.0\",\"sourceRepo\":\"https://user:pass@example.invalid/repo.git\""),
            Reference("ref-commit", "\"referenceKind\":\"build-attachment\",\"artifactVersion\":\"1.0.0\",\"commitSha\":\"deadbeef\""),
            Reference("ref-digest", "\"referenceKind\":\"build-attachment\",\"artifactVersion\":\"1.0.0\",\"artifactDigestAlgorithm\":\"sha256\",\"artifactDigest\":\"AAAA\""),
            Reference("ref-range", "\"referenceKind\":\"build-attachment\",\"artifactVersion\":\"~1.2.3\"")
        };
        var rejected = PackageDecisionDeploymentReferenceReader.Read("{\"version\":\"package-deployment-reference.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"references\":[" + string.Join(",", rejectedReferences) + "]}");
        Assert.True(rejected.Accepted);
        Assert.Empty(rejected.References);
        Assert.Equal(12, rejected.Gaps.Count);
        Assert.Contains(rejected.Gaps, gap => gap.Classification == "DecisionInputIdentityUnsafe");
        Assert.Contains(rejected.Gaps, gap => gap.Classification == "DecisionInputMalformed");
        Assert.DoesNotContain(rejected.Gaps, gap => gap.Message.Contains(secret, StringComparison.Ordinal));
        Assert.All(rejected.Gaps, gap => Assert.Equal("package.decision.correlation.v1", gap.RuleId));
    }

    [Fact]
    public void Deployment_reference_reader_deduplicates_and_rejects_conflicting_ids()
    {
        const string reference = "\"referenceId\":\"ref-1\",\"referenceKind\":\"build-attachment\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\"";
        var duplicate = PackageDecisionDeploymentReferenceReader.Read("{\"version\":\"package-deployment-reference.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"references\":[{" + reference + "},{" + reference + "}]}");
        Assert.Single(duplicate.References);
        Assert.Contains(duplicate.Gaps, gap => gap.Classification == "DecisionInputDuplicateConflict");

        var conflict = PackageDecisionDeploymentReferenceReader.Read("{\"version\":\"package-deployment-reference.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"references\":[{"
            + reference + "},{" + reference + ",\"registryOrigin\":\"registry.npmjs.org\"}]}");
        Assert.Empty(conflict.References);
        Assert.Equal(2, conflict.Gaps.Count(gap => gap.Classification == "DecisionInputDuplicateConflict"));
    }

    [Fact]
    public async Task Advisory_claims_render_as_external_claims_without_altering_correlation()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var advisoryPath = FixturePath("advisory-profile-example.json");
        var manifest = Manifest("advisory-repo", "1111111111111111111111111111111111111111");
        SqliteIndexWriter.Write(indexPath, manifest, [DigestFact(manifest, "example", "1.0.0", new string('a', 64))]);
        await File.WriteAllTextAsync(decisionPath, RejectRecord("dec-advisory", "example", "1.0.0", new string('a', 64)));

        var baseline = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "baseline")));
        var withClaims = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, indexPath, Path.Combine(temp.Path, "claims"), AdvisoryProfilePath: advisoryPath));

        Assert.Single(baseline.Report.ExactMatches);
        Assert.Single(withClaims.Report.ExactMatches);
        Assert.Equal(JsonSerializer.Serialize(baseline.Report.ExactMatches), JsonSerializer.Serialize(withClaims.Report.ExactMatches));
        Assert.Equal(baseline.Report.Summary.ExactCount, withClaims.Report.Summary.ExactCount);
        Assert.Equal(baseline.Report.Summary.PossibleCount, withClaims.Report.Summary.PossibleCount);
        Assert.True(baseline.ExitCodeTriggered);
        Assert.Equal(baseline.ExitCodeTriggered, withClaims.ExitCodeTriggered);
        Assert.NotNull(withClaims.Report.AdvisoryClaims);
        Assert.Equal(2, withClaims.Report.AdvisoryClaims!.Count);
        Assert.Null(baseline.Report.AdvisoryClaims);

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "claims", "package-decision-report.json"));
        var parsed = JsonNode.Parse(json)!;
        Assert.Equal(2, parsed["advisoryClaims"]!.AsArray().Count);
        Assert.Equal("package.decision.advisory.v1", parsed["advisoryClaims"]![0]!["ruleId"]!.GetValue<string>());
        Assert.Equal("example-advisory-producer", parsed["advisoryClaims"]![0]!["producerId"]!.GetValue<string>());
        Assert.Matches("^[a-f0-9]{64}$", parsed["advisoryClaims"]![0]!["profileDigest"]!.GetValue<string>());
        Assert.Equal("any", parsed["advisoryClaims"]![0]!["versionPredicateKind"]!.GetValue<string>());
        Assert.Null(parsed["advisoryClaims"]![0]!["versionPredicateVersion"]);
        Assert.Equal("exact", parsed["advisoryClaims"]![1]!["versionPredicateKind"]!.GetValue<string>());
        Assert.Equal("14.2.3", parsed["advisoryClaims"]![1]!["versionPredicateVersion"]!.GetValue<string>());
        var markdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "claims", "package-decision-report.md"));
        Assert.Contains("## Advisory Claims (external)", markdown, StringComparison.Ordinal);
        Assert.Contains("`example-advisory-producer` version `2026.08.1`", markdown, StringComparison.Ordinal);
        Assert.Contains("claim-next-rsc-001", markdown, StringComparison.Ordinal);
        Assert.Contains("external producer opinions", markdown, StringComparison.Ordinal);

        var rejectedProfile = Path.Combine(temp.Path, "rejected.json");
        await File.WriteAllTextAsync(rejectedProfile, "{\"version\":\"advisory-profile.v1\",\"claims\":[]}");
        await Assert.ThrowsAsync<InvalidDataException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, indexPath, Path.Combine(temp.Path, "rejected"), AdvisoryProfilePath: rejectedProfile)));
    }

    [Fact]
    public async Task Deployment_references_render_runtime_unproven_and_never_upgrade_rungs()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var referencesPath = Path.Combine(temp.Path, "references.json");
        var commitSha = "2222222222222222222222222222222222222222";
        var manifest = Manifest("deploy-repo", commitSha);
        SqliteIndexWriter.Write(indexPath, manifest, [
            DigestFact(manifest, "@example/lib", "2.14.0", new string('a', 64)),
            PackageFact(manifest, "fixture-nameonly", "npm", "package.json", 5, "1.4.0")
        ]);
        await File.WriteAllTextAsync(decisionPath, RejectRecord("dec-deploy", "unrelated-package", "9.9.9", new string('c', 64)));
        await File.WriteAllTextAsync(referencesPath, "{\"version\":\"package-deployment-reference.v1\",\"producer\":{\"id\":\"ci-producer\",\"version\":\"1\"},\"references\":["
            + "{"
            + "\"referenceId\":\"ref-exact\",\"referenceKind\":\"build-attachment\",\"ecosystem\":\"npm\",\"packageName\":\"@example/lib\",\"artifactVersion\":\"2.14.0\","
            + "\"artifactDigestAlgorithm\":\"sha256\",\"artifactDigest\":\"" + new string('a', 64) + "\",\"sourceRepo\":\"https://example.invalid/org/app.git\",\"commitSha\":\"" + commitSha + "\""
            + "},"
            + "{"
            + "\"referenceId\":\"ref-name\",\"referenceKind\":\"deployment-manifest\",\"ecosystem\":\"npm\",\"packageName\":\"fixture-nameonly\",\"artifactVersion\":\"1.4.0\""
            + "},"
            + "{"
            + "\"referenceId\":\"ref-unmatched\",\"referenceKind\":\"build-attachment\",\"ecosystem\":\"npm\",\"packageName\":\"fixture-missing\",\"artifactVersion\":\"0.0.1\""
            + "}"
            + "]}");

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, indexPath, Path.Combine(temp.Path, "report"), DeploymentReferencesPath: referencesPath, ExitCode: true));

        Assert.Empty(result.Report.ExactMatches);
        Assert.False(result.ExitCodeTriggered);
        Assert.Equal(3, result.Report.RuntimeUnprovenReferences.Count);
        Assert.Equal(3, result.Report.Summary.RuntimeUnprovenCount);
        Assert.All(result.Report.RuntimeUnprovenReferences, row => Assert.Equal("RuntimeUnprovenReference", row.Classification));
        Assert.All(result.Report.RuntimeUnprovenReferences, row => Assert.Equal(RuntimeUnprovenWording, row.Message));
        Assert.All(result.Report.RuntimeUnprovenReferences, row => Assert.Equal("package.decision.correlation.v1", row.RuleId));
        var exactJoin = result.Report.RuntimeUnprovenReferences.Single(row => row.ReferenceId == "ref-exact");
        Assert.Equal("digest", exactJoin.JoinBasis);
        Assert.Contains("default", exactJoin.MatchedSourceLabels!);
        Assert.NotEmpty(exactJoin.MatchedFactIds!);
        Assert.StartsWith("repo-hash:", exactJoin.SourceRepoHash);
        Assert.Equal(commitSha, exactJoin.CommitSha);
        var nameJoin = result.Report.RuntimeUnprovenReferences.Single(row => row.ReferenceId == "ref-name");
        Assert.Equal("name-version", nameJoin.JoinBasis);
        Assert.Equal("unmatched", result.Report.RuntimeUnprovenReferences.Single(row => row.ReferenceId == "ref-unmatched").JoinBasis);

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "package-decision-report.json"));
        Assert.DoesNotContain("https://example.invalid", json, StringComparison.Ordinal);
        var markdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "package-decision-report.md"));
        Assert.Contains("ref-exact", markdown, StringComparison.Ordinal);
        Assert.Contains(RuntimeUnprovenWording, markdown, StringComparison.Ordinal);

        var scoped = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, indexPath, Path.Combine(temp.Path, "scoped"), DeploymentReferencesPath: referencesPath, Ecosystem: "nuget"));
        Assert.Empty(scoped.Report.RuntimeUnprovenReferences);
    }

    [Fact]
    public async Task Deployment_reference_digest_join_fails_closed_for_malformed_index_digest_length()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var referencesPath = Path.Combine(temp.Path, "references.json");
        var manifest = Manifest("deploy-malformed-digest", "1212121212121212121212121212121212121212");
        SqliteIndexWriter.Write(indexPath, manifest, [DigestFact(manifest, "example", "1.0.0", "short")]);
        await File.WriteAllTextAsync(decisionPath, RejectRecord("dec-unrelated", "other", "1.0.0", null));
        await File.WriteAllTextAsync(referencesPath, "{\"version\":\"package-deployment-reference.v1\",\"producer\":{\"id\":\"producer\",\"version\":\"1\"},\"references\":[{"
            + "\"referenceId\":\"ref-malformed-digest\",\"referenceKind\":\"build-attachment\",\"ecosystem\":\"npm\",\"packageName\":\"example\",\"artifactVersion\":\"1.0.0\","
            + "\"artifactDigestAlgorithm\":\"sha256\",\"artifactDigest\":\"" + new string('a', 64) + "\"}]}");

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, indexPath, Path.Combine(temp.Path, "report"), DeploymentReferencesPath: referencesPath));

        var reference = Assert.Single(result.Report.RuntimeUnprovenReferences);
        Assert.Equal("name-version", reference.JoinBasis);
    }

    [Fact]
    public async Task Comparison_mode_emits_exact_replacement_possible_only_and_skips_unchanged()
    {
        using var temp = new TempDirectory();
        var (decisionPath, beforeManifest, afterManifest) = await WriteComparisonFixtureAsync(temp);

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "report"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest));

        Assert.Equal("DecisionComparisonV1", result.Report.Mode);
        var changes = result.Report.ArtifactChanges!;
        Assert.Equal(2, changes.Count);
        Assert.Equal(2, result.Report.Summary.ArtifactChangeCount);
        var replaced = changes.Single(change => change.Classification == "ArtifactReplaced");
        Assert.Equal("artifact-replaced", replaced.ChangeKind);
        Assert.Equal("dec-compare-replaced", replaced.DecisionId);
        Assert.Equal("web", replaced.SourceLabel);
        Assert.NotNull(replaced.Before);
        Assert.NotNull(replaced.After);
        Assert.Equal(new string('a', 64), replaced.Before!.Evidence.ArtifactDigest);
        Assert.Equal(new string('b', 64), replaced.After!.Evidence.ArtifactDigest);
        Assert.Equal("before/web", replaced.Before.SourceLabel);
        Assert.Equal("after/web", replaced.After.SourceLabel);
        Assert.Equal("package-lock.json", replaced.Before.Evidence.FilePath);
        Assert.Equal("package.decision.correlation.v1", replaced.RuleId);
        Assert.Contains(CrossSnapshotWording, replaced.Message, StringComparison.Ordinal);
        var possible = changes.Single(change => change.ChangeKind == "possible-only");
        Assert.Equal("PossibleArtifactChange", possible.Classification);
        Assert.Equal("dec-compare-possible", possible.DecisionId);
        Assert.Null(possible.Before!.Evidence.ArtifactDigest);
        Assert.Null(possible.After!.Evidence.ArtifactDigest);
        Assert.DoesNotContain(changes, change => change.DecisionId == "dec-compare-unchanged");

        Assert.Single(result.Report.ExactMatches, row => row.DecisionId == "dec-compare-replaced" && row.SourceLabel == "before/web");
        Assert.Single(result.Report.DigestMismatches, row => row.DecisionId == "dec-compare-replaced" && row.SourceLabel == "after/web");
        Assert.Equal(2, result.Report.PossibleMatches.Count);

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "package-decision-report.json"));
        var parsed = JsonNode.Parse(json)!;
        Assert.Equal("DecisionComparisonV1", parsed["mode"]!.GetValue<string>());
        Assert.Equal(2, parsed["artifactChanges"]!.AsArray().Count);
        Assert.Contains(CrossSnapshotWording, json, StringComparison.Ordinal);
        var markdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "package-decision-report.md"));
        Assert.Contains("## Before/After Artifact Changes", markdown, StringComparison.Ordinal);
        Assert.Contains("`ArtifactReplaced`", markdown, StringComparison.Ordinal);

        var repeated = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "repeated"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest));
        Assert.Equal(await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "package-decision-report.json")), await File.ReadAllTextAsync(Path.Combine(temp.Path, "repeated", "package-decision-report.json")));
        Assert.Equal(await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "package-decision-report.md")), await File.ReadAllTextAsync(Path.Combine(temp.Path, "repeated", "package-decision-report.md")));
        Assert.Equal(JsonSerializer.Serialize(result.Report.ArtifactChanges), JsonSerializer.Serialize(repeated.Report.ArtifactChanges));
        Assert.DoesNotContain(temp.Path, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Comparison_replacement_sides_select_the_digest_evidence_used_by_the_claim()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var before = Manifest("mixed-evidence", "1313131313131313131313131313131313131313");
        var after = Manifest("mixed-evidence", "1414141414141414141414141414141414141414");
        SqliteIndexWriter.Write(beforeIndex, before, [
            PackageFact(before, "example", "npm", "aaa-package.json", 1, "1.0.0"),
            DigestFact(before, "example", "1.0.0", new string('a', 64))
        ]);
        SqliteIndexWriter.Write(afterIndex, after, [
            PackageFact(after, "example", "npm", "aaa-package.json", 1, "1.0.0"),
            DigestFact(after, "example", "1.0.0", new string('b', 64))
        ]);
        await File.WriteAllTextAsync(beforeManifest, "{\"version\":\"1.0\",\"inputs\":[{\"label\":\"web\",\"indexPath\":\"before.sqlite\"}]}");
        await File.WriteAllTextAsync(afterManifest, "{\"version\":\"1.0\",\"inputs\":[{\"label\":\"web\",\"indexPath\":\"after.sqlite\"}]}");
        await File.WriteAllTextAsync(decisionPath, "{\"version\":\"package-decision.v1\",\"records\":[" + RecordJson("dec-mixed", "example") + "]}");

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "report"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest));

        var replacement = Assert.Single(result.Report.ArtifactChanges!);
        Assert.Equal("ArtifactReplaced", replacement.Classification);
        Assert.Equal(new string('a', 64), replacement.Before!.Evidence.ArtifactDigest);
        Assert.Equal(new string('b', 64), replacement.After!.Evidence.ArtifactDigest);
        Assert.Equal("package-lock.json", replacement.Before.Evidence.FilePath);
        Assert.Equal("package-lock.json", replacement.After.Evidence.FilePath);
    }

    [Fact]
    public async Task Comparison_identity_preserves_before_and_after_manifest_roles()
    {
        using var temp = new TempDirectory();
        var (decisionPath, beforeManifest, afterManifest) = await WriteComparisonFixtureAsync(temp);
        var forward = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "forward"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest));
        var reversed = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "reversed"), BeforeManifestPath: afterManifest, AfterManifestPath: beforeManifest));

        Assert.NotEqual(forward.Report.Query.IndexPathHash, reversed.Report.Query.IndexPathHash);
    }

    [Fact]
    public async Task Comparison_pairing_limit_emits_gap_and_suppresses_change_claim()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var before = Manifest("large-evidence", "1515151515151515151515151515151515151515");
        var after = Manifest("large-evidence", "1616161616161616161616161616161616161616");
        var beforeFacts = Enumerable.Range(1, 1001).Select(index => PackageFact(before, "example", "npm", $"package-{index:D4}.json", index, "1.0.0")).ToArray();
        SqliteIndexWriter.Write(beforeIndex, before, beforeFacts);
        SqliteIndexWriter.Write(afterIndex, after, [PackageFact(after, "example", "npm", "package.json", 1, "1.0.0")]);
        await File.WriteAllTextAsync(beforeManifest, "{\"version\":\"1.0\",\"inputs\":[{\"label\":\"web\",\"indexPath\":\"before.sqlite\"}]}");
        await File.WriteAllTextAsync(afterManifest, "{\"version\":\"1.0\",\"inputs\":[{\"label\":\"web\",\"indexPath\":\"after.sqlite\"}]}");
        await File.WriteAllTextAsync(decisionPath, "{\"version\":\"package-decision.v1\",\"records\":[" + RecordJson("dec-large", "example") + "]}");

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "report"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest));

        Assert.Empty(result.Report.ArtifactChanges!);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "TruncatedByLimit" && gap.Message.Contains("comparison fact limit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Comparison_mode_covers_added_removed_and_identity_ambiguity()
    {
        using var temp = new TempDirectory();
        var beforeManifest = Path.Combine(temp.Path, "before.json");
        var afterManifest = Path.Combine(temp.Path, "after.json");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("compare-repo", "3333333333333333333333333333333333333333");
        var after = Manifest("compare-repo", "4444444444444444444444444444444444444444");
        SqliteIndexWriter.Write(beforeIndex, before, [
            DigestFact(before, "removed-package", "1.0.0", new string('e', 64)),
            DigestFact(before, "stable-package", "2.0.0", new string('f', 64))
        ]);
        SqliteIndexWriter.Write(afterIndex, after, [
            DigestFact(after, "added-package", "1.0.0", new string('e', 64)),
            DigestFact(after, "stable-package", "2.0.0", new string('f', 64))
        ]);
        await File.WriteAllTextAsync(beforeManifest, "{\"version\":\"1.0\",\"portfolioId\":\"comparison\",\"snapshotId\":\"before\",\"inputs\":[{\"label\":\"web\",\"indexPath\":\"before.sqlite\"}]}");
        await File.WriteAllTextAsync(afterManifest, "{\"version\":\"1.0\",\"portfolioId\":\"comparison\",\"snapshotId\":\"after\",\"inputs\":[{\"label\":\"web\",\"indexPath\":\"after.sqlite\"}]}");
        await File.WriteAllTextAsync(decisionPath, "{\"version\":\"package-decision.v1\",\"records\":["
            + RecordJson("dec-added", "added-package") + "," + RecordJson("dec-removed", "removed-package") + "," + RecordJson("dec-stable", "stable-package", "2.0.0")
            + "]}");

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "report"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest));

        var changes = result.Report.ArtifactChanges!;
        Assert.Equal(2, changes.Count);
        var added = changes.Single(change => change.ChangeKind == "added");
        Assert.Equal("PossibleArtifactChange", added.Classification);
        Assert.Null(added.Before);
        Assert.NotNull(added.After);
        var removed = changes.Single(change => change.ChangeKind == "removed");
        Assert.Equal("PossibleArtifactChange", removed.Classification);
        Assert.NotNull(removed.Before);
        Assert.Null(removed.After);
        Assert.DoesNotContain(changes, change => change.DecisionId == "dec-stable");
        Assert.All(changes, change => Assert.Contains(CrossSnapshotWording, change.Message, StringComparison.Ordinal));

        var ambiguousAfter = Manifest("other-repo", "5555555555555555555555555555555555555555");
        var ambiguousIndex = Path.Combine(temp.Path, "ambiguous.sqlite");
        SqliteIndexWriter.Write(ambiguousIndex, ambiguousAfter, [DigestFact(ambiguousAfter, "stable-package", "2.0.0", new string('0', 64))]);
        await File.WriteAllTextAsync(afterManifest, "{\"version\":\"1.0\",\"portfolioId\":\"comparison\",\"snapshotId\":\"after\",\"inputs\":[{\"label\":\"web\",\"indexPath\":\"ambiguous.sqlite\"}]}");
        var ambiguous = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "ambiguous"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest));
        Assert.Contains(ambiguous.Report.Gaps, gap => gap.Classification == "IdentityAmbiguous" && gap.Message.Contains("different repo identity", StringComparison.Ordinal));
        var downgraded = ambiguous.Report.ArtifactChanges!.Single(change => change.DecisionId == "dec-stable");
        Assert.Equal("AmbiguousIdentityChange", downgraded.Classification);
        Assert.Equal("identity-ambiguous", downgraded.ChangeKind);
        Assert.DoesNotContain("ArtifactReplaced", downgraded.Classification, StringComparison.Ordinal);
        Assert.Contains(downgraded.Notes, note => note.Code == "identity-ambiguous");
    }

    [Fact]
    public async Task Comparison_mode_enforces_input_rules_selectors_and_truncation()
    {
        using var temp = new TempDirectory();
        var (decisionPath, beforeManifest, afterManifest) = await WriteComparisonFixtureAsync(temp);
        var indexPath = Path.Combine(temp.Path, "before-web.sqlite");

        await Assert.ThrowsAsync<ArgumentException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, indexPath, Path.Combine(temp.Path, "mixed"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest)));
        await Assert.ThrowsAsync<ArgumentException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "half"), BeforeManifestPath: beforeManifest)));
        await Assert.ThrowsAsync<ArgumentException>(() => PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "half"), AfterManifestPath: afterManifest)));

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exit = await TraceMapCommand.RunAsync(["package-decision", "--decision", decisionPath, "--index", indexPath, "--out", Path.Combine(temp.Path, "cli-mixed"),
            "--before-manifest", beforeManifest, "--after-manifest", afterManifest], stdout, stderr);
        Assert.Equal(1, exit);
        Assert.Contains("cannot be mixed", stderr.ToString(), StringComparison.Ordinal);
        exit = await TraceMapCommand.RunAsync(["package-decision", "--decision", decisionPath, "--out", Path.Combine(temp.Path, "cli-half"), "--before-manifest", beforeManifest], stdout, stderr);
        Assert.Equal(1, exit);
        Assert.Contains("together", stderr.ToString(), StringComparison.Ordinal);

        var selected = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "selected"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest, DecisionId: "dec-compare-replaced"));
        var selectedChange = Assert.Single(selected.Report.ArtifactChanges!);
        Assert.Equal("dec-compare-replaced", selectedChange.DecisionId);

        var classificationScoped = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "classification"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest, Classification: "ExactArtifactMatch"));
        Assert.Equal(2, classificationScoped.Report.ArtifactChanges!.Count);
        Assert.Equal(3, classificationScoped.Report.ExactMatches.Count);

        var truncated = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "truncated"), BeforeManifestPath: beforeManifest, AfterManifestPath: afterManifest, MaxFindings: 1));
        Assert.Single(truncated.Report.ArtifactChanges!);
        Assert.Contains(truncated.Report.Gaps, gap => gap.Classification == "TruncatedByLimit" && gap.Message.Contains("artifact change limit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Snapshot_mode_keeps_new_sections_nullable_and_stable()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        var manifest = Manifest("snapshot-repo", "6666666666666666666666666666666666666666");
        SqliteIndexWriter.Write(indexPath, manifest, [PackageFact(manifest, "example", "npm", "package.json", 5, "1.0.0")]);
        await File.WriteAllTextAsync(decisionPath, RejectRecord("dec-snapshot", "example", "1.0.0", null));

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "report")));

        Assert.Equal("DecisionSnapshotV1", result.Report.Mode);
        Assert.Null(result.Report.AdvisoryClaims);
        Assert.Null(result.Report.ArtifactChanges);
        Assert.Empty(result.Report.RuntimeUnprovenReferences);
        Assert.Equal(0, result.Report.Summary.ArtifactChangeCount);
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "package-decision-report.json"));
        var parsed = JsonNode.Parse(json)!;
        Assert.Null(parsed["advisoryClaims"]);
        Assert.Null(parsed["artifactChanges"]);
        Assert.Equal("DecisionSnapshotV1", parsed["mode"]!.GetValue<string>());
        var markdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "package-decision-report.md"));
        Assert.Contains("No advisory profile was supplied.", markdown, StringComparison.Ordinal);
        Assert.Contains("Before/after artifact changes require --before-manifest and --after-manifest.", markdown, StringComparison.Ordinal);
        Assert.Contains("No runtime-unproven references were supplied.", markdown, StringComparison.Ordinal);
        Assert.Contains("- Mode: `DecisionSnapshotV1`", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Combined_external_inputs_render_together_deterministically()
    {
        using var temp = new TempDirectory();
        var (decisionPath, beforeManifest, afterManifest) = await WriteComparisonFixtureAsync(temp);

        var first = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "first"),
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest,
            AdvisoryProfilePath: FixturePath("advisory-profile-example.json"),
            DeploymentReferencesPath: FixturePath("deployment-references-example.json")));
        var second = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(
            decisionPath, string.Empty, Path.Combine(temp.Path, "second"),
            BeforeManifestPath: beforeManifest,
            AfterManifestPath: afterManifest,
            AdvisoryProfilePath: FixturePath("advisory-profile-example.json"),
            DeploymentReferencesPath: FixturePath("deployment-references-example.json")));

        Assert.Equal(2, first.Report.AdvisoryClaims!.Count);
        Assert.Equal(2, first.Report.RuntimeUnprovenReferences.Count);
        Assert.Equal(2, first.Report.ArtifactChanges!.Count);
        Assert.Equal(first.Report.AdvisoryClaims, second.Report.AdvisoryClaims);
        Assert.Equal(JsonSerializer.Serialize(first.Report.RuntimeUnprovenReferences), JsonSerializer.Serialize(second.Report.RuntimeUnprovenReferences));
        Assert.Equal(JsonSerializer.Serialize(first.Report.ArtifactChanges), JsonSerializer.Serialize(second.Report.ArtifactChanges));
        Assert.True(first.ExitCodeTriggered);
        Assert.Equal(first.ExitCodeTriggered, second.ExitCodeTriggered);
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "first", "package-decision-report.json"));
        Assert.DoesNotContain(temp.Path, json, StringComparison.Ordinal);
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(temp.Path, "second", "package-decision-report.json")));
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(temp.Path, "first", "package-decision-report.md")),
            await File.ReadAllTextAsync(Path.Combine(temp.Path, "second", "package-decision-report.md")));
    }

    [Fact]
    public void External_claim_rules_are_active_and_catalogued()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains("id: package.decision.advisory.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("id: package.decision.correlation.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("PackageDecisionAdvisoryClaim", catalog, StringComparison.Ordinal);
        Assert.Contains("PackageDecisionExternalReference", catalog, StringComparison.Ordinal);
        Assert.Contains("PackageDecisionArtifactChange", catalog, StringComparison.Ordinal);
        Assert.Contains("RuntimeUnprovenReference", catalog, StringComparison.Ordinal);
        Assert.Contains("cross-snapshot portfolio evidence, not a single coherent release state", catalog, StringComparison.Ordinal);
    }

    private static async Task<(string DecisionPath, string BeforeManifest, string AfterManifest)> WriteComparisonFixtureAsync(TempDirectory temp)
    {
        var beforeIndex = Path.Combine(temp.Path, "before-web.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after-web.sqlite");
        var before = Manifest("comparison-web", "7777777777777777777777777777777777777777");
        var after = Manifest("comparison-web", "8888888888888888888888888888888888888888");
        SqliteIndexWriter.Write(beforeIndex, before, [
            DigestFact(before, "@example/lib", "2.14.0", new string('a', 64)),
            PackageFact(before, "fixture-possible", "npm", "package.json", 5, "1.0.0"),
            DigestFact(before, "fixture-stable", "3.1.4", new string('d', 64))
        ]);
        SqliteIndexWriter.Write(afterIndex, after, [
            DigestFact(after, "@example/lib", "2.14.0", new string('b', 64)),
            PackageFact(after, "fixture-possible", "npm", "package.json", 6, "1.0.0"),
            DigestFact(after, "fixture-stable", "3.1.4", new string('d', 64))
        ]);
        var beforeManifest = Path.Combine(temp.Path, "before-portfolio.json");
        var afterManifest = Path.Combine(temp.Path, "after-portfolio.json");
        File.Copy(FixturePath("comparison/before-portfolio.json"), beforeManifest);
        File.Copy(FixturePath("comparison/after-portfolio.json"), afterManifest);
        return (FixturePath("comparison/decision-comparison.json"), beforeManifest, afterManifest);
    }

    private static string Claim(
        string claimId,
        string packageName = "next",
        string predicate = "{\"kind\":\"exact\",\"version\":\"14.2.3\"}",
        string claimKind = "framework-implied-server-surface",
        string parameters = "{\"framework\":\"next-rsc\"}",
        string extras = "") =>
        "{\"claimId\":\"" + claimId + "\",\"ecosystem\":\"npm\",\"packageName\":\"" + packageName + "\",\"versionPredicate\":" + predicate + ",\"claimKind\":\"" + claimKind + "\",\"claimParams\":" + parameters + (extras.Length == 0 ? string.Empty : "," + extras) + "}";

    private static string Reference(string referenceId, string fields) =>
        "{\"referenceId\":\"" + referenceId + "\",\"ecosystem\":\"npm\",\"packageName\":\"example\"," + fields + "}";

    private static string RecordJson(string decisionId, string packageName, string version = "1.0.0") =>
        "{\"decisionId\":\"" + decisionId + "\",\"decisionKind\":\"reject\",\"ecosystem\":\"npm\",\"packageName\":\"" + packageName + "\",\"artifactVersion\":\"" + version + "\",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}";

    private static string RejectRecord(string decisionId, string packageName, string version, string? digest) =>
        "{\"version\":\"package-decision.v1\",\"records\":[{\"decisionId\":\"" + decisionId + "\",\"decisionKind\":\"reject\",\"ecosystem\":\"npm\",\"packageName\":\"" + packageName
        + "\",\"artifactVersion\":\"" + version + "\"" + (digest is null ? string.Empty : ",\"artifactDigestAlgorithm\":\"sha256\",\"artifactDigest\":\"" + digest + "\"")
        + ",\"producer\":{\"id\":\"producer\",\"policyVersion\":\"1\"},\"decisionTimeUtc\":\"2026-08-18T00:00:00Z\"}]}";

    private static ScanManifest Manifest(string repo, string commitSha) => new($"scan-{repo}", repo, null, "main", commitSha, "typescript-scanner", DateTimeOffset.Parse("2026-08-01T00:00:00Z"), "Level1SemanticAnalysis", "Succeeded", [], [], [], []);

    private static CodeFact PackageFact(ScanManifest manifest, string name, string ecosystem, string file, int line, string version) => new($"pkg-{manifest.RepoName}-{name}-{line}", manifest.ScanId, manifest.RepoName, manifest.CommitSha, null, FactTypes.PackageReferenced, RuleIds.ProjectFile, EvidenceTiers.Tier2Structural, null, name, "PackageManifest", new EvidenceSpan(file, line, line, null, "TestExtractor", "1.0.0"), new SortedDictionary<string, string>(StringComparer.Ordinal) { ["dependencyGroup"] = "dependencies", ["ecosystem"] = ecosystem, ["manifestKind"] = "package.json", ["packageName"] = name, ["packageManager"] = ecosystem, ["sourceKind"] = "manifest", ["surfaceKind"] = "package-config", ["version"] = version });

    private static CodeFact DigestFact(ScanManifest manifest, string name, string version, string digest) => new($"pkg-{manifest.RepoName}-{name}", manifest.ScanId, manifest.RepoName, manifest.CommitSha, null, FactTypes.PackageReferenced, RuleIds.ProjectFile, EvidenceTiers.Tier2Structural, null, name, "PackageManifest", new EvidenceSpan("package-lock.json", 5, 5, null, "TestExtractor", "1.0.0"), new SortedDictionary<string, string>(StringComparer.Ordinal) { ["dependencyGroup"] = "dependencies", ["dependencyRelation"] = "direct", ["ecosystem"] = "npm", ["manifestKind"] = "package-lock.json", ["packageName"] = name, ["packageManager"] = "npm", ["sourceKind"] = "lockfile", ["surfaceKind"] = "package-config", ["resolvedVersion"] = version, ["version"] = version, ["artifactDigestAlgorithm"] = "sha256", ["artifactDigest"] = digest, ["lockfilePath"] = "package-lock.json", ["lockfileHash"] = new string('b', 32) });

    private static string FixturePath(string relative) => Path.Combine(FindRepoRoot(), "samples", "package-decisions", relative);

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
