using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Reduction;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class StaticHtmlEvidenceExplorerTests
{
    [Fact]
    public async Task Explorer_generate_writes_local_static_bundle_without_raw_private_values()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);

        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("a"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Equal(StaticHtmlEvidenceExplorer.SchemaVersion, result.Manifest.SchemaVersion);
        Assert.True(result.Manifest.TracemapGenerated);
        Assert.Equal("public-demo", result.Manifest.SafetyProfile);
        Assert.Equal("commit-sha-only", result.Manifest.RepoIdentityPolicy);
        Assert.Equal("omitted-deterministic", result.Manifest.GenerationTimestampPolicy);
        Assert.Null(result.Manifest.GeneratedAt);
        Assert.Equal(FortyCharCommit("a"), result.Manifest.CommitSha);
        Assert.True(File.Exists(Path.Combine(output, "index.html")));
        Assert.True(File.Exists(Path.Combine(output, "assets", "explorer.css")));
        Assert.True(File.Exists(Path.Combine(output, "assets", "explorer.js")));
        Assert.True(File.Exists(Path.Combine(output, "data", "explorer-manifest.json")));
        Assert.True(File.Exists(Path.Combine(output, "data", "explorer-data.json")));
        Assert.True(File.Exists(Path.Combine(output, "README.md")));

        var allGenerated = string.Join("\n", Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));
        Assert.DoesNotContain("C:\\sample-root", allGenerated);
        Assert.DoesNotContain("git@example.com:internal/example-repo.git", allGenerated);
        Assert.DoesNotContain("Server=prod;Password=secret", allGenerated);
        Assert.DoesNotContain("public class Secret", allGenerated);
        Assert.DoesNotContain("https://", allGenerated);
        Assert.DoesNotContain("http://", allGenerated);
        Assert.Contains("absolute-path-hash:", allGenerated);
        Assert.Contains("explorer.render.redacted-display-value.v1", allGenerated);
        Assert.Contains("explorer.render.section-status.v1", allGenerated);
        Assert.Contains("Safety &amp; Redactions", await File.ReadAllTextAsync(Path.Combine(output, "index.html")));
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.RedactedDisplayValueRuleId
            && redaction.Location == "scanner-version"
            && redaction.Category == "secret-like-value");
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.OmittedUnsafeValueRuleId
            && redaction.Location == "facts.properties"
            && redaction.Category == "secret-like-value");
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.OmittedUnsafeValueRuleId
            && redaction.Location == "scan-manifest.remoteUrl");
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.OmittedUnsafeValueRuleId
            && redaction.Location == "scan-manifest.branch"
            && redaction.Category == "branch-name");
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.OmittedUnsafeValueRuleId
            && redaction.Location == "scan-manifest.solutions"
            && redaction.Category == "solution-name");
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.OmittedUnsafeValueRuleId
            && redaction.Location == "scan-manifest.projects"
            && redaction.Category == "project-path");
        Assert.Contains(result.Manifest.Limitations, limitation =>
            limitation.RuleId == StaticHtmlEvidenceExplorer.CompatibilityLedgerRuleId
            && limitation.LimitationKind == "claim-level-metadata-unknown"
            && limitation.ClaimEffect == "claim-level");
        Assert.Equal("tracemap-static-html-evidence-explorer.v4", result.Data.SchemaVersion);
        Assert.Contains("Compatibility Ledger", allGenerated);
        Assert.Contains("explorer.render.compatibility-ledger.v1", allGenerated);
        Assert.Contains("Local generated artifact", allGenerated);
        Assert.Contains("does not rescan source code", allGenerated);
    }

    [Fact]
    public async Task Explorer_generate_emits_deterministic_safe_compatibility_ledger_with_absence_states()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("1"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectKind == "artifact"
            && row.SubjectId == "artifact:scan-manifest"
            && row.CompatibilityStatus == "rendered-compatible"
            && row.RuleId == StaticHtmlEvidenceExplorer.CompatibilityLedgerRuleId);
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectId == "artifact:sqlite-index"
            && row.CompatibilityStatus == "provenance-only");
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectId == "artifact:rule-catalog"
            && row.CompatibilityStatus == "not-provided"
            && row.Message.Contains("does not prove evidence absence", StringComparison.Ordinal));
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectKind == "section"
            && row.SubjectId == "surfaces"
            && row.CompatibilityStatus == "provenance-only");
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectKind == "safety-profile"
            && row.SubjectId == "safety-profile:public-demo"
            && row.CompatibilityStatus == "compatible");
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectKind == "claim-level"
            && row.SubjectId == "claim-level:unknown"
            && row.CompatibilityStatus == "partial"
            && row.RuleId == StaticHtmlEvidenceExplorer.CompatibilityLedgerRuleId
            && row.LimitationIds.Contains("claim-level-metadata-unavailable"));
        Assert.DoesNotContain(result.Data.CompatibilityLedger, row =>
            row.CompatibilityStatus == "profile-incompatible"
            || row.SubjectId.Contains("claim-level-conflict", StringComparison.Ordinal));

        var ordered = result.Data.CompatibilityLedger
            .OrderBy(row => row.SubjectKind, StringComparer.Ordinal)
            .ThenBy(row => row.SubjectId, StringComparer.Ordinal)
            .ThenBy(row => row.CompatibilityStatus, StringComparer.Ordinal)
            .ThenBy(row => row.RuleId, StringComparer.Ordinal)
            .ThenBy(row => row.RowId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ordered, result.Data.CompatibilityLedger);
        Assert.All(result.Data.CompatibilityLedger, row =>
        {
            Assert.Equal(EvidenceTiers.Tier4Unknown, row.EvidenceTier);
            Assert.NotEmpty(row.RuleId);
            Assert.NotEmpty(row.SupportIds);
            Assert.Equal(row.SupportIds.OrderBy(value => value, StringComparer.Ordinal), row.SupportIds);
            Assert.Equal(row.LimitationIds.OrderBy(value => value, StringComparer.Ordinal), row.LimitationIds);
            Assert.DoesNotContain("impacted", row.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("safe to deploy", row.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("used in production", row.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("runtime reachable", row.Message, StringComparison.OrdinalIgnoreCase);
        });

        var html = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
        Assert.Contains("<h2 id=\"compatibility-ledger-heading\">Compatibility Ledger</h2>", html);
        Assert.Contains("artifact:rule-catalog", html);
        Assert.Contains("claim-level:unknown", html);
        var dataJson = await File.ReadAllTextAsync(Path.Combine(output, "data", "explorer-data.json"));
        using var document = JsonDocument.Parse(dataJson);
        Assert.Equal(result.Data.CompatibilityLedger.Count, document.RootElement.GetProperty("compatibilityLedger").GetArrayLength());
        Assert.DoesNotContain("C:\\sample-root", dataJson);
        Assert.DoesNotContain("git@example.com:internal/example-repo.git", dataJson);
    }

    [Fact]
    public async Task Explorer_generate_reads_release_review_v12_compatibility_metadata_without_rendering_report_content()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var publicOutput = Path.Combine(temp.Path, "public-explorer");
        var hiddenOutput = Path.Combine(temp.Path, "hidden-explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("a"));
        await WriteReleaseReviewArtifactAsync(input, afterCommitSha: FortyCharCommit("a"));

        var publicResult = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, publicOutput));
        var hiddenResult = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, hiddenOutput, "hidden-local"));

        foreach (var result in new[] { publicResult, hiddenResult })
        {
            var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "release-review");
            Assert.Equal("artifact:release-review", artifact.ArtifactId);
            Assert.Equal("release-review/1.2", artifact.SchemaVersion);
            Assert.Equal("supported", artifact.Compatibility);
            Assert.Equal(
                ["ReleaseReviewAfterReduced", "ReleaseReviewBeforeFull", "ReleaseReviewGapsPresent", "ReleaseReviewNotTruncated"],
                artifact.CoverageLabels);
            Assert.Contains("limitation:release-review-content-not-rendered", artifact.Limitations);
            Assert.Contains(result.Data.Limitations, limitation =>
                limitation.LimitationId == "limitation:release-review-content-not-rendered"
                && limitation.RuleId == StaticHtmlEvidenceExplorer.ReleaseReviewInputRuleId
                && limitation.ClaimEffect == "compatibility-only");
            Assert.Contains(result.Data.CompatibilityLedger, row =>
                row.SubjectId == "artifact:release-review"
                && row.CompatibilityStatus == "rendered-compatible"
                && row.LimitationIds.Contains("limitation:release-review-content-not-rendered"));
            Assert.Contains(result.Data.Rules, rule => rule.RuleId == StaticHtmlEvidenceExplorer.ReleaseReviewInputRuleId);
            Assert.DoesNotContain(result.Gaps, gap => gap.Scope == "artifact:release-review");
        }

        foreach (var output in new[] { publicOutput, hiddenOutput })
        {
            var generated = string.Join("\n", RelativeFileMap(output).Values);
            Assert.DoesNotContain("private-source-name", generated, StringComparison.Ordinal);
            Assert.DoesNotContain("C:\\private\\release\\Source.cs", generated, StringComparison.Ordinal);
            Assert.DoesNotContain("SELECT secret_value FROM private_table", generated, StringComparison.Ordinal);
            Assert.DoesNotContain("private release message", generated, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("unsupported-version")]
    [InlineData("duplicate-version")]
    [InlineData("invalid-commit")]
    public async Task Explorer_generate_keeps_incompatible_release_review_metadata_unavailable(string fixtureKind)
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("b"));
        await WriteReleaseReviewArtifactAsync(input, fixtureKind, FortyCharCommit("b"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "release-review");
        Assert.Equal("release-review/unsupported", artifact.SchemaVersion);
        Assert.Equal("unsupported", artifact.Compatibility);
        var gap = Assert.Single(result.Gaps, row => row.Scope == "artifact:release-review");
        Assert.Equal(StaticHtmlEvidenceExplorer.UnsupportedSchemaRuleId, gap.RuleId);
        Assert.Equal("unsupported-schema", gap.GapKind);
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectId == "artifact:release-review"
            && row.CompatibilityStatus == "unsupported-schema"
            && row.LimitationIds.Contains(gap.GapId));
        Assert.DoesNotContain(result.Data.Artifacts, row => row.ArtifactKind == "unsupported-json");
    }

    [Fact]
    public async Task Explorer_generate_is_deterministic_with_compatible_release_review_metadata()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var firstOutput = Path.Combine(temp.Path, "first");
        var secondOutput = Path.Combine(temp.Path, "second");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("c"));
        await WriteReleaseReviewArtifactAsync(input, afterCommitSha: FortyCharCommit("c"));

        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, firstOutput));
        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, secondOutput));

        Assert.Equal(RelativeFileMap(firstOutput), RelativeFileMap(secondOutput));
    }

    [Fact]
    public async Task Explorer_generate_does_not_bind_release_review_when_after_snapshot_commit_differs()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("1"));
        await WriteReleaseReviewArtifactAsync(input, afterCommitSha: FortyCharCommit("2"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "release-review");
        Assert.Empty(artifact.SourceIds);
        var gap = Assert.Single(result.Gaps, row =>
            row.Scope == "artifact:release-review"
            && row.RuleId == StaticHtmlEvidenceExplorer.ProvenanceConflictRuleId
            && row.GapKind == "commit-conflict");
        Assert.Contains("artifact:scan-manifest", gap.SupportIds);
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectId == "artifact:release-review"
            && row.CompatibilityStatus == "partial"
            && row.LimitationIds.Contains(gap.GapId));
    }

    [Fact]
    public async Task Explorer_generate_keeps_release_review_unbound_without_authoritative_scan_commit()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "report-only-input");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteReleaseReviewArtifactAsync(input, afterCommitSha: FortyCharCommit("3"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "release-review");
        Assert.Empty(artifact.SourceIds);
        var gap = Assert.Single(result.Gaps, row =>
            row.Scope == "artifact:release-review"
            && row.RuleId == StaticHtmlEvidenceExplorer.MissingCommitRuleId
            && row.GapKind == "source-association-unknown");
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectId == "artifact:release-review"
            && row.CompatibilityStatus == "partial"
            && row.LimitationIds.Contains(gap.GapId));
    }

    [Fact]
    public async Task Explorer_generate_rejects_partially_unidentified_fact_stream_as_commit_authority()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        var validCommit = FortyCharCommit("4");
        await JsonlFactWriter.WriteAsync(
            Path.Combine(input, "facts.ndjson"),
            [
                Fact(validCommit) with { FactId = "fact-valid" },
                Fact("unknown") with { FactId = "fact-unknown" }
            ]);
        await WriteReleaseReviewArtifactAsync(input, afterCommitSha: validCommit);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "release-review");
        Assert.Empty(artifact.SourceIds);
        Assert.Contains(result.Gaps, row =>
            row.Scope == "artifact:facts-ndjson"
            && row.RuleId == StaticHtmlEvidenceExplorer.MissingCommitRuleId
            && row.GapKind == "missing-commit");
        Assert.Contains(result.Gaps, row =>
            row.Scope == "artifact:release-review"
            && row.RuleId == StaticHtmlEvidenceExplorer.MissingCommitRuleId
            && row.GapKind == "source-association-unknown");
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectId == "artifact:release-review"
            && row.CompatibilityStatus == "partial");
    }

    [Fact]
    public async Task Explorer_generate_rejects_oversized_release_review_before_json_parsing()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("f"));
        await File.WriteAllTextAsync(Path.Combine(input, "release-review.json"), new string(' ', 16_777_217));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "release-review");
        Assert.Equal("unavailable:artifact-too-large", artifact.ContentHash);
        Assert.Contains(result.Gaps, gap =>
            gap.Scope == "artifact:release-review"
            && gap.RuleId == StaticHtmlEvidenceExplorer.UnsupportedSchemaRuleId
            && gap.GapKind == "artifact-too-large");
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectId == "artifact:release-review"
            && row.CompatibilityStatus == "unsupported-schema");
    }

    [Fact]
    public async Task Explorer_generate_renders_bounded_contract_delta_reducer_results_without_private_free_text()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        var commitSha = FortyCharCommit("8");
        await WriteScanArtifactsAsync(input, commitSha: commitSha);
        await WriteReducerImpactArtifactAsync(input, commitSha, includeGap: true);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Equal("tracemap-static-html-evidence-explorer.v4", result.Data.SchemaVersion);
        var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "reducer-impact-report");
        Assert.Equal("contract-delta-impact/2.0", artifact.SchemaVersion);
        Assert.Equal("supported", artifact.Compatibility);
        Assert.Equal(["ReducerFullCoverage", "ReducerGapsPresent", "ReducerNotTruncated"], artifact.CoverageLabels);
        var reducerResult = Assert.Single(result.Data.ReducerResults);
        Assert.Equal(ImpactClassifications.DefiniteImpact, reducerResult.Classification);
        Assert.Equal("high", reducerResult.Confidence);
        Assert.Equal(RuleIds.ContractDeltaImpact, reducerResult.RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, reducerResult.EvidenceTier);
        Assert.Equal("contract-delta-fact-match/2.0", reducerResult.ReducerVersion);
        Assert.Single(reducerResult.EvidenceIds);
        var evidence = Assert.Single(result.Data.EvidenceRows, row => row.EvidenceKind == "reducer-impact-evidence");
        Assert.Equal(RuleIds.CSharpSemanticPropertyAccess, evidence.RuleId);
        Assert.Equal(commitSha, evidence.CommitSha);
        Assert.Equal("test-reducer-v2", evidence.ExtractorVersion);
        Assert.Null(evidence.SnippetHash);
        Assert.StartsWith("absolute-path-hash:", evidence.FilePath, StringComparison.Ordinal);
        Assert.Contains(result.Gaps, gap =>
            gap.Scope == "artifact:reducer-impact-report"
            && gap.GapKind == "reducer-impact-gap"
            && gap.RuleId == RuleIds.ContractDeltaImpact);
        Assert.True(result.Data.Summary.ReducerOutputPresent);
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "reducer-results" && row.Status == "partial");

        var generated = string.Join("\n", RelativeFileMap(output).Values);
        Assert.Contains("<h2 id=\"reducer-results-heading\">Reducer Results</h2>", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Private.Customer.Email", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("private impact reason", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("private-source-label", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\private\\Customer.cs", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("private reducer gap message", generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explorer_generate_marks_reduced_or_truncated_reducer_results_partial()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "reducer-only");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteReducerImpactArtifactAsync(input, FortyCharCommit("9"), reducedCoverage: true, truncated: true);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains("ReducerReducedCoverage", result.Data.Summary.CoverageLabels);
        Assert.Contains("ReducerTruncated", result.Data.Summary.CoverageLabels);
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "reducer-results" && row.Status == "partial");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "evidence-rows" && row.Status == "partial");
        Assert.NotEqual("available", result.Data.Summary.CoverageStatus);
    }

    [Theory]
    [InlineData("unsupported-report")]
    [InlineData("duplicate-provenance")]
    [InlineData("mismatched-commit")]
    public async Task Explorer_generate_fails_closed_for_incompatible_reducer_reports(string fixtureKind)
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "reducer-only");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteReducerImpactArtifactAsync(input, FortyCharCommit("a"), fixtureKind: fixtureKind);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "reducer-impact-report");
        Assert.Equal("unsupported", artifact.Compatibility);
        Assert.Empty(result.Data.ReducerResults);
        Assert.DoesNotContain(result.Data.EvidenceRows, row => row.EvidenceKind == "reducer-impact-evidence");
        Assert.Contains(result.Gaps, gap =>
            gap.Scope == "artifact:reducer-impact-report"
            && gap.GapKind == "unsupported-schema"
            && gap.RuleId == StaticHtmlEvidenceExplorer.UnsupportedSchemaRuleId);
    }

    [Fact]
    public async Task Explorer_generate_is_deterministic_for_reducer_input()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "reducer-only");
        var firstOutput = Path.Combine(temp.Path, "first");
        var secondOutput = Path.Combine(temp.Path, "second");
        Directory.CreateDirectory(input);
        await WriteReducerImpactArtifactAsync(input, FortyCharCommit("b"), includeGap: true);

        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, firstOutput));
        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, secondOutput));

        Assert.Equal(RelativeFileMap(firstOutput), RelativeFileMap(secondOutput));
    }

    [Fact]
    public async Task Explorer_generate_renders_safe_ordered_paths_and_surfaces_from_paths_report_v10()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        var commitSha = FortyCharCommit("6");
        await WriteScanArtifactsAsync(input, commitSha: commitSha);
        await WritePathsReportArtifactAsync(input, commitSha);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Equal("tracemap-static-html-evidence-explorer.v4", result.Data.SchemaVersion);
        var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "paths-report");
        Assert.Equal("paths-report/1.0", artifact.SchemaVersion);
        Assert.Equal("supported", artifact.Compatibility);
        Assert.Equal(["source:scan-output"], artifact.SourceIds);
        Assert.Contains("limitation:paths-report-display-text-not-rendered", artifact.Limitations);

        var surface = Assert.Single(result.Data.Surfaces);
        Assert.Equal("sql-query", surface.SurfaceKind);
        Assert.Equal("database.sql.shape.v1", surface.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, surface.EvidenceTier);
        Assert.Equal("StaticDependencySurface", surface.Classification);
        Assert.Equal("source:scan-output", surface.SourceId);
        Assert.Equal(commitSha, surface.CommitSha);
        Assert.Equal("test-scanner-v1", surface.ExtractorVersion);
        Assert.StartsWith("absolute-path-hash:", surface.FilePath, StringComparison.Ordinal);

        var path = Assert.Single(result.Data.Paths);
        Assert.Equal("dependency-path", path.PathKind);
        Assert.Equal(CombinedDependencyPathClassifications.NeedsReviewStaticPath, path.Classification);
        Assert.Equal("Low", path.Confidence);
        var hop = Assert.Single(path.Hops);
        Assert.Equal(1, hop.Sequence);
        Assert.Equal("calls", hop.EdgeKind);
        Assert.Equal("combined.paths.path.v1", hop.RuleId);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, hop.EvidenceTier);
        Assert.Equal(commitSha, hop.CommitSha);
        Assert.Equal("test-scanner-v1", hop.ExtractorVersion);
        Assert.StartsWith("node:", hop.FromNodeId, StringComparison.Ordinal);
        Assert.StartsWith("node:", hop.ToNodeId, StringComparison.Ordinal);
        Assert.All(hop.SupportIds, supportId => Assert.StartsWith("support:", supportId, StringComparison.Ordinal));

        Assert.Equal(1, result.Manifest.Counts.SurfaceCount);
        Assert.Equal(1, result.Manifest.Counts.PathCount);
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "surfaces" && row.Status == "available");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "paths" && row.Status == "available");
        Assert.Contains(result.Data.EvidenceRows, row => row.EvidenceKind == "path-hop" && row.RuleId == "combined.paths.path.v1" && row.SnippetHash is null);
        Assert.Contains(result.Data.EvidenceRows, row => row.EvidenceKind == "dependency-surface" && row.RuleId == "database.sql.shape.v1" && row.SnippetHash is null);

        var generated = string.Join("\n", RelativeFileMap(output).Values);
        Assert.Contains("<h2 id=\"surfaces-heading\">Surfaces</h2>", generated, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"paths-heading\">Paths</h2>", generated, StringComparison.Ordinal);
        Assert.Contains("Tier3SyntaxOrTextual", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("private-source-label", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Private.OrderService", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT private_value FROM private_table", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\private\\OrderService.cs", generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explorer_generate_rejects_paths_report_with_non_contiguous_hop_endpoints()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        var commitSha = FortyCharCommit("7");
        await WriteScanArtifactsAsync(input, commitSha: commitSha);
        await WritePathsReportArtifactAsync(input, commitSha, invalidEdgeTarget: true);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "paths-report");
        Assert.Equal("unsupported", artifact.Compatibility);
        Assert.Empty(result.Data.Surfaces);
        Assert.Empty(result.Data.Paths);
        Assert.Contains(result.Gaps, row =>
            row.Scope == "artifact:paths-report"
            && row.RuleId == StaticHtmlEvidenceExplorer.UnsupportedSchemaRuleId
            && row.GapKind == "unsupported-schema");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "paths" && row.Status == "unsupported-schema");
    }

    [Fact]
    public async Task Explorer_generate_paths_report_projection_is_deterministic()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var first = Path.Combine(temp.Path, "first");
        var second = Path.Combine(temp.Path, "second");
        Directory.CreateDirectory(input);
        var commitSha = FortyCharCommit("8");
        await WriteScanArtifactsAsync(input, commitSha: commitSha);
        await WritePathsReportArtifactAsync(input, commitSha);

        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, first));
        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, second));

        Assert.Equal(RelativeFileMap(first), RelativeFileMap(second));
    }

    [Fact]
    public async Task Explorer_generate_uses_safe_paths_report_source_without_phantom_scan_source()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "paths-only");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WritePathsReportArtifactAsync(input, FortyCharCommit("9"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var source = Assert.Single(result.Data.Sources);
        Assert.StartsWith("source:paths:", source.SourceId, StringComparison.Ordinal);
        Assert.Equal("Paths report source 01", source.SafeLabel);
        Assert.Equal(FortyCharCommit("9"), source.CommitSha);
        Assert.Equal(["artifact:paths-report"], source.ArtifactIds);
        Assert.DoesNotContain(result.Data.Sources, row => row.SourceId == "source:scan-output");
        Assert.All(result.Data.Paths.SelectMany(path => path.Hops), hop => Assert.Equal(source.SourceId, hop.SourceId));

        var generated = string.Join("\n", RelativeFileMap(output).Values);
        Assert.DoesNotContain("private-source-index", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("private-source-label", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("private-repository", generated, StringComparison.Ordinal);
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "evidence-rows" && row.Status != "not-provided");
    }

    [Fact]
    public async Task Explorer_generate_attributes_endpoint_match_to_server_source()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "paths-only");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WritePathsReportArtifactAsync(input, FortyCharCommit("1"), crossSourceEndpoint: true);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var serverSource = Assert.Single(result.Data.Sources, source => source.CommitSha == FortyCharCommit("2"));
        var hop = Assert.Single(Assert.Single(result.Data.Paths).Hops);
        Assert.Equal("endpoint-match", hop.EdgeKind);
        Assert.Equal(serverSource.SourceId, hop.SourceId);
        Assert.Equal(serverSource.CommitSha, hop.CommitSha);
        Assert.Equal("test-server-scanner-v2", hop.ExtractorVersion);
        var evidence = Assert.Single(result.Data.EvidenceRows, row => row.EvidenceKind == "path-hop");
        Assert.Equal(serverSource.SourceId, evidence.SourceId);
        Assert.Equal(serverSource.CommitSha, evidence.CommitSha);
    }

    [Fact]
    public async Task Explorer_generate_marks_warning_only_reduced_paths_report_sections_partial()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "paths-only");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WritePathsReportArtifactAsync(input, FortyCharCommit("3"), reducedCoverage: true);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "surfaces" && row.Status == "partial");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "paths" && row.Status == "partial");
        Assert.Contains("PathsReducedCoverage", Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "paths-report").CoverageLabels);
        Assert.Contains("PathsReducedCoverage", result.Data.Summary.CoverageLabels);
        Assert.NotEqual("available", result.Data.Summary.CoverageStatus);
    }

    [Fact]
    public async Task Explorer_generate_labels_missing_path_locations_as_partial_gaps()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "paths-only");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WritePathsReportArtifactAsync(input, FortyCharCommit("4"), omitPathLocations: true);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Gaps, gap => gap.GapKind == "path-hop-location-unavailable" && gap.AffectedSection == "paths");
        Assert.Contains(result.Gaps, gap => gap.GapKind == "surface-location-unavailable" && gap.AffectedSection == "surfaces");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "paths" && row.Status == "partial");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "surfaces" && row.Status == "partial");
    }

    [Fact]
    public async Task Explorer_generate_rejects_duplicate_nested_paths_report_provenance()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "paths-only");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WritePathsReportArtifactAsync(input, FortyCharCommit("5"));
        var reportPath = Path.Combine(input, "paths-report.json");
        var json = await File.ReadAllTextAsync(reportPath);
        json = json.Replace(
            "\"scannerVersion\": \"test-scanner-v1\",",
            "\"scannerVersion\": \"test-scanner-v1\",\n      \"scannerVersion\": \"order-dependent-version\",",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(reportPath, json);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var artifact = Assert.Single(result.Data.Artifacts, row => row.ArtifactKind == "paths-report");
        Assert.Equal("unsupported", artifact.Compatibility);
        Assert.Empty(result.Data.Paths);
        Assert.Empty(result.Data.Surfaces);
        Assert.Contains(result.Gaps, gap => gap.Scope == "artifact:paths-report" && gap.GapKind == "unsupported-schema");
    }

    [Fact]
    public async Task Explorer_generate_marks_truncated_full_coverage_paths_report_partial()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "paths-only");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WritePathsReportArtifactAsync(input, FortyCharCommit("6"), truncated: true);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains("PathsTruncated", result.Data.Summary.CoverageLabels);
        Assert.NotEqual("available", result.Data.Summary.CoverageStatus);
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "evidence-rows" && row.Status == "partial");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "surfaces" && row.Status == "partial");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "paths" && row.Status == "partial");
    }

    [Fact]
    public async Task Explorer_generate_does_not_render_paths_report_gap_kinds_or_messages()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WritePathsReportArtifactAsync(input, FortyCharCommit("a"), includePrivateGap: true);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Gaps, gap =>
            gap.Scope == "artifact:paths-report"
            && gap.GapKind == "paths-report-gap"
            && gap.RuleId == "combined.paths.path.v1"
            && gap.EvidenceTier == EvidenceTiers.Tier2Structural);
        var generated = string.Join("\n", RelativeFileMap(output).Values);
        Assert.DoesNotContain("PrivateCustomerGap", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("private customer gap message", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Explorer_paths_report_reader_rule_is_cataloged_with_static_non_claims()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));

        Assert.Contains($"id: {StaticHtmlEvidenceExplorer.PathsReportInputRuleId}", catalog, StringComparison.Ordinal);
        Assert.Contains("do not prove runtime reachability, execution, production use, business impact, release safety, or complete analysis", catalog, StringComparison.Ordinal);
        Assert.Contains("Query selectors, source labels, node and surface display names, report notes, raw SQL, and free-text report limitations are omitted", catalog, StringComparison.Ordinal);
        Assert.Contains("ordinary paths-report v1.0 contract only", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void Explorer_reducer_reader_rule_is_cataloged_with_bounded_static_non_claims()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));

        Assert.Contains($"id: {StaticHtmlEvidenceExplorer.ReducerImpactInputRuleId}", catalog, StringComparison.Ordinal);
        Assert.Contains("the explorer does not infer impact, risk, runtime reachability, execution, production behavior, business effect, release safety, or complete dependency coverage", catalog, StringComparison.Ordinal);
        Assert.Contains("contract-delta-impact-single and contract-delta-impact-combined version 2.0", catalog, StringComparison.Ordinal);
        Assert.Contains("Finding elements, reasons, warnings, references, source labels, scan IDs", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void Explorer_release_review_reader_rule_is_cataloged_with_non_claims()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));

        Assert.Contains($"id: {StaticHtmlEvidenceExplorer.ReleaseReviewInputRuleId}", catalog, StringComparison.Ordinal);
        Assert.Contains("does not prove runtime reachability, production behavior, release approval, deployment safety, or complete analysis", catalog, StringComparison.Ordinal);
        Assert.Contains("Richer release-review surface, path, and reducer projections require a separately bounded reader slice", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explorer_generate_renders_rule_backed_section_statuses_for_first_slice_gaps()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("2"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.All(result.Data.SectionStatuses, row =>
        {
            Assert.Equal(StaticHtmlEvidenceExplorer.SectionStatusRuleId, row.RuleId);
            Assert.Equal(EvidenceTiers.Tier4Unknown, row.EvidenceTier);
            Assert.NotEmpty(row.SupportIds);
        });
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "surfaces" && row.Status == "not-rendered-in-current-slice");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "paths" && row.Status == "not-rendered-in-current-slice");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "reducer-results" && row.Status == "not-rendered-in-current-slice");
        Assert.Contains(result.Data.SectionStatuses, row => row.SectionId == "rules" && row.Status == "built-in-stubs");

        var html = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
        Assert.Contains("<h2 id=\"coverage-heading\">Coverage</h2>", html);
        Assert.Contains("Section status rows describe explorer rendering coverage only", html);
        Assert.Contains("not-rendered-in-current-slice", html);
        Assert.Contains(StaticHtmlEvidenceExplorer.SectionStatusRuleId, html);
        Assert.DoesNotContain("complete analysis", html, StringComparison.OrdinalIgnoreCase);
        var overviewIndex = SectionRowIndex(html, "Evidence Overview");
        var sourcesIndex = SectionRowIndex(html, "Sources");
        var artifactsIndex = SectionRowIndex(html, "Artifacts");
        var evidenceRowsIndex = SectionRowIndex(html, "Evidence Rows");
        Assert.True(overviewIndex < sourcesIndex);
        Assert.True(sourcesIndex < artifactsIndex);
        Assert.True(artifactsIndex < evidenceRowsIndex);

        var dataJson = await File.ReadAllTextAsync(Path.Combine(output, "data", "explorer-data.json"));
        Assert.Contains("\"sectionStatuses\"", dataJson);
        Assert.Contains("\"not-rendered-in-current-slice\"", dataJson);
        Assert.DoesNotContain("C:\\sample-root", dataJson);
        Assert.DoesNotContain("git@example.com:internal/example-repo.git", dataJson);
        using var document = JsonDocument.Parse(dataJson);
        var sectionIds = document.RootElement.GetProperty("sectionStatuses")
            .EnumerateArray()
            .Select(row => row.GetProperty("sectionId").GetString() ?? string.Empty)
            .ToArray();
        Assert.Equal(
            [
                "overview",
                "sources",
                "artifacts",
                "evidence-rows",
                "surfaces",
                "paths",
                "reducer-results",
                "rules",
                "redactions"
            ],
            sectionIds);
    }

    [Fact]
    public async Task Explorer_generate_renders_richer_rule_gap_limitation_and_evidence_metadata()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("3"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Gaps, gap =>
            gap.RuleId == StaticHtmlEvidenceExplorer.CatalogUnavailableRuleId
            && gap.GapKind == "catalog-unavailable"
            && gap.AffectedSection == "rules"
            && gap.SupportIds.Contains(RuleIds.CSharpSyntaxDeclarations));
        Assert.Contains(result.Data.Rules, rule =>
            rule.RuleId == RuleIds.CSharpSyntaxDeclarations
            && rule.Title == "Observed evidence rule"
            && rule.RelatedSections.Contains("evidence-rows")
            && rule.Limitations.Any(limitation => limitation.Contains("partial", StringComparison.OrdinalIgnoreCase)));

        var html = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
        Assert.Contains("<th>Scope</th>", html);
        Assert.Contains("<th>Support IDs</th>", html);
        Assert.Contains("<th>Description</th>", html);
        Assert.Contains("<th>Artifact ID</th>", html);
        Assert.Contains("<th>Source ID</th>", html);
        Assert.Contains("<th>Coverage</th>", html);
        Assert.Contains(RuleIds.CSharpSyntaxDeclarations, html);
        Assert.Contains("Observed evidence rule", html);
        Assert.Contains("artifact:facts-ndjson", html);
        Assert.Contains("source:scan-output", html);

        var dataJson = await File.ReadAllTextAsync(Path.Combine(output, "data", "explorer-data.json"));
        using var document = JsonDocument.Parse(dataJson);
        var rules = document.RootElement.GetProperty("rules").EnumerateArray().ToArray();
        var ruleIds = rules.Select(rule => rule.GetProperty("ruleId").GetString()).ToArray();
        Assert.Equal(ruleIds.OrderBy(ruleId => ruleId, StringComparer.Ordinal), ruleIds);
        Assert.Contains(rules, rule =>
            rule.GetProperty("ruleId").GetString() == RuleIds.CSharpSyntaxDeclarations
            && rule.GetProperty("description").GetString()!.Contains("facts.ndjson", StringComparison.Ordinal));

        var evidenceRows = document.RootElement.GetProperty("evidenceRows").EnumerateArray().ToArray();
        Assert.Contains(evidenceRows, row =>
            row.GetProperty("artifactId").GetString() == "artifact:facts-ndjson"
            && row.GetProperty("sourceId").GetString() == "source:scan-output"
            && row.GetProperty("coverageLabel").GetString() == "Failed");
        Assert.DoesNotContain("C:\\sample-root", dataJson);
        Assert.DoesNotContain("git@example.com:internal/example-repo.git", dataJson);
    }

    [Fact]
    public async Task Explorer_generate_renders_compatible_rule_catalog_rows_when_provided()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("4"));
        await File.WriteAllTextAsync(Path.Combine(input, "rule-catalog.yml"), $$"""
            rules:
              - id: {{RuleIds.CSharpSyntaxDeclarations}}
                name: C# syntax declarations:
                description: Documents declarations discovered from deterministic C# syntax evidence.
                evidenceTier: Tier2Structural
                emits:
                  - TypeDeclared
                limitations:
                  - Syntax evidence does not prove runtime execution.
                  - Semantic binding may be unavailable under reduced coverage.
                  - Analysis gaps use Tier4Unknown in separate evidence rows.
                  - SELECT table extraction only claims visible top-level FROM/JOIN identifiers.
            """);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Manifest.Inputs, artifact =>
            artifact.ArtifactKind == "rule-catalog"
            && artifact.Compatibility == "supported");
        Assert.DoesNotContain(result.Gaps, gap =>
            gap.RuleId == StaticHtmlEvidenceExplorer.CatalogUnavailableRuleId
            && gap.GapKind == "catalog-unavailable");
        Assert.Contains(result.Data.Rules, rule =>
            rule.RuleId == RuleIds.CSharpSyntaxDeclarations
            && rule.Title == "C# syntax declarations:"
            && rule.Description.Contains("deterministic C# syntax evidence", StringComparison.Ordinal)
            && rule.EvidenceTier == "Tier2Structural"
            && rule.Limitations.Contains("Syntax evidence does not prove runtime execution.")
            && rule.RelatedSections.Contains("evidence-rows"));

        var html = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
        Assert.Contains("C# syntax declarations:", html);
        Assert.Contains("Rule catalog", html);
        Assert.Contains("Rules include compatible rule catalog rows", html);
        Assert.Contains("SELECT table extraction only claims visible top-level FROM/JOIN identifiers.", html);

        var dataJson = await File.ReadAllTextAsync(Path.Combine(output, "data", "explorer-data.json"));
        using var document = JsonDocument.Parse(dataJson);
        var rulesStatus = document.RootElement.GetProperty("sectionStatuses")
            .EnumerateArray()
            .Single(row => row.GetProperty("sectionId").GetString() == "rules");
        Assert.Equal("available", rulesStatus.GetProperty("status").GetString());
        Assert.Contains(document.RootElement.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("artifactKind").GetString() == "rule-catalog");
    }

    [Fact]
    public async Task Explorer_generate_marks_present_unsupported_rule_catalog_without_no_catalog_gap()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("b"));
        await File.WriteAllTextAsync(Path.Combine(input, "rule-catalog.yml"), """
            rules:
              - name: Missing ID
                description: This catalog row is intentionally unsupported.
            """);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Manifest.Inputs, artifact =>
            artifact.ArtifactKind == "rule-catalog"
            && artifact.Compatibility == "supported");
        Assert.Contains(result.Gaps, gap =>
            gap.RuleId == StaticHtmlEvidenceExplorer.UnsupportedSchemaRuleId
            && gap.GapKind == "unsupported-schema"
            && gap.AffectedSection == "rules");
        Assert.DoesNotContain(result.Gaps, gap =>
            gap.RuleId == StaticHtmlEvidenceExplorer.CatalogUnavailableRuleId
            && gap.GapKind == "catalog-unavailable");
        Assert.Contains(result.Data.SectionStatuses, row =>
            row.SectionId == "rules"
            && row.Status == "partial"
            && row.Message.Contains("provided, but no compatible rule rows were loaded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Explorer_generate_marks_oversized_rule_catalog_without_reading_rows()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("c"));
        await File.WriteAllTextAsync(Path.Combine(input, "rule-catalog.yml"), new string('#', 1_048_577));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Manifest.Inputs, artifact =>
            artifact.ArtifactKind == "rule-catalog"
            && artifact.Compatibility == "unsupported");
        Assert.Contains(result.Gaps, gap =>
            gap.RuleId == StaticHtmlEvidenceExplorer.UnsupportedSchemaRuleId
            && gap.GapKind == "artifact-too-large"
            && gap.AffectedSection == "rules");
        Assert.DoesNotContain(result.Gaps, gap =>
            gap.RuleId == StaticHtmlEvidenceExplorer.CatalogUnavailableRuleId
            && gap.GapKind == "catalog-unavailable");
    }

    [Fact]
    public async Task Explorer_generate_does_not_let_catalog_override_reserved_explorer_rules()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("d"));
        await File.WriteAllTextAsync(Path.Combine(input, "rule-catalog.yml"), $$"""
            rules:
              - id: {{StaticHtmlEvidenceExplorer.UnsafeRejectedRuleId}}
                name: Replaced unsafe rule
                description: External catalog text must not replace reserved explorer rule stubs.
                evidenceTier: Tier1Semantic
                limitations:
                  - Catalog limitations are also ignored for reserved explorer rules.
            """);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Data.Rules, rule =>
            rule.RuleId == StaticHtmlEvidenceExplorer.UnsafeRejectedRuleId
            && rule.Title == "Explorer unsafe generated value rejected"
            && !rule.Description.Contains("External catalog text", StringComparison.Ordinal)
            && !rule.Limitations.Contains("Catalog limitations are also ignored for reserved explorer rules."));
    }

    [Fact]
    public async Task Explorer_generate_hashes_unsafe_rule_catalog_text()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("e"));
        await File.WriteAllTextAsync(Path.Combine(input, "rule-catalog.yml"), $$"""
            rules:
              - id: {{RuleIds.CSharpSyntaxDeclarations}}
                name: C# syntax declarations
                description: Server=prod;Password=secret
                evidenceTier: Tier3SyntaxOrTextual
                limitations:
                  - git@example.com:internal/example-repo.git
                  - SELECT * FROM Users WHERE PasswordHash IS NOT NULL
                  - System.NullReferenceException at Sample.Widget.Handle
            """);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.RedactedDisplayValueRuleId
            && redaction.Location == "rule-catalog.description"
            && redaction.Category == "secret-like-value");
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.RedactedDisplayValueRuleId
            && redaction.Location == "rule-catalog.limitations"
            && redaction.Category == "raw-remote-or-url");
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.RedactedDisplayValueRuleId
            && redaction.Location == "rule-catalog.limitations"
            && redaction.Category == "raw-sql");
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.RedactedDisplayValueRuleId
            && redaction.Location == "rule-catalog.limitations"
            && redaction.Category == "stack-trace");

        var generated = string.Join("\n", Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));
        Assert.DoesNotContain("Server=prod;Password=secret", generated);
        Assert.DoesNotContain("git@example.com:internal/example-repo.git", generated);
        Assert.DoesNotContain("SELECT * FROM Users", generated);
        Assert.DoesNotContain("System.NullReferenceException", generated);
        Assert.Contains("rule-catalog.description-hash:", generated);
        Assert.Contains("rule-catalog.limitations-hash:", generated);
    }

    [Fact]
    public void Explorer_generated_string_validator_rejects_remote_references_without_printing_raw_value()
    {
        var failure = Assert.Throws<InvalidOperationException>(() =>
            StaticHtmlEvidenceExplorer.ValidateGeneratedFilesForSafety(new Dictionary<string, string>
            {
                ["index.html"] = "<a href=\"https://private.example.test/path\">unsafe</a>"
            }));

        Assert.Contains(StaticHtmlEvidenceExplorer.UnsafeRejectedRuleId, failure.Message);
        Assert.Contains("index.html", failure.Message);
        Assert.DoesNotContain("private.example.test", failure.Message);

        var unsafeLocalPath = "/" + "Users/example/private/repo/file.cs";
        var pathFailure = Assert.Throws<InvalidOperationException>(() =>
            StaticHtmlEvidenceExplorer.ValidateGeneratedFilesForSafety(new Dictionary<string, string>
            {
                ["data/explorer-data.json"] = $$"""{"path":"{{unsafeLocalPath}}"}"""
            }));
        Assert.Contains(StaticHtmlEvidenceExplorer.UnsafeRejectedRuleId, pathFailure.Message);
        Assert.Contains("data/explorer-data.json", pathFailure.Message);
        Assert.DoesNotContain(unsafeLocalPath, pathFailure.Message);

        var sshRemote = "git@example.com:internal/example-repo.git";
        var sshFailure = Assert.Throws<InvalidOperationException>(() =>
            StaticHtmlEvidenceExplorer.ValidateGeneratedFilesForSafety(new Dictionary<string, string>
            {
                ["data/explorer-data.json"] = $$"""{"remote":"{{sshRemote}}"}"""
            }));
        Assert.Contains(StaticHtmlEvidenceExplorer.UnsafeRejectedRuleId, sshFailure.Message);
        Assert.Contains("data/explorer-data.json", sshFailure.Message);
        Assert.DoesNotContain(sshRemote, sshFailure.Message);

        StaticHtmlEvidenceExplorer.ValidateGeneratedFilesForSafety(new Dictionary<string, string>
        {
            ["assets/explorer.js"] = "// local comment without a remote reference\n(() => {})();\n",
            ["assets/explorer.css"] = "/* local comment */\nbody { color: #111; }\n"
        });
    }

    [Fact]
    public async Task Explorer_generate_is_byte_stable_for_identical_inputs()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var first = Path.Combine(temp.Path, "first");
        var second = Path.Combine(temp.Path, "second");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("b"));

        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, first));
        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, second));

        var firstFiles = RelativeFileMap(first);
        var secondFiles = RelativeFileMap(second);
        Assert.Equal(firstFiles.Keys, secondFiles.Keys);
        foreach (var relativePath in firstFiles.Keys)
        {
            Assert.Equal(firstFiles[relativePath], secondFiles[relativePath]);
        }
    }

    [Fact]
    public async Task Explorer_generate_marks_missing_manifest_and_unsupported_json_as_partial()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await JsonlFactWriter.WriteAsync(Path.Combine(input, "facts.ndjson"), [Fact(FortyCharCommit("c"))]);
        await File.WriteAllTextAsync(Path.Combine(input, "unrecognized-report.json"), """{"schemaVersion":"unknown.v9","value":"safe"}""");

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Equal("partial", result.Manifest.CoverageStatus);
        Assert.Contains(result.Gaps, gap => gap.RuleId == StaticHtmlEvidenceExplorer.PartialSectionRuleId && gap.GapKind == "not-provided" && gap.AffectedSection == "sources");
        Assert.Contains(result.Gaps, gap => gap.RuleId == StaticHtmlEvidenceExplorer.UnsupportedSchemaRuleId && gap.GapKind == "unsupported-schema");
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectKind == "artifact"
            && row.SubjectId.StartsWith("artifact:unsupported-json:", StringComparison.Ordinal)
            && row.CompatibilityStatus == "unsupported-schema"
            && row.RuleId == StaticHtmlEvidenceExplorer.UnsupportedSchemaRuleId);
        var html = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
        Assert.Contains("partial", html);
        Assert.Contains("Unsupported JSON artifact", html);
    }

    [Fact]
    public async Task Explorer_generate_marks_commit_conflicts_as_rule_backed_gaps()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, manifestCommitSha: FortyCharCommit("d"), factCommitSha: FortyCharCommit("e"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Gaps, gap =>
            gap.RuleId == StaticHtmlEvidenceExplorer.ProvenanceConflictRuleId
            && gap.GapKind == "commit-conflict"
            && gap.AffectedSection == "evidence-rows");
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectId == "artifact:facts-ndjson"
            && row.CompatibilityStatus == "partial"
            && row.RuleId == StaticHtmlEvidenceExplorer.ProvenanceConflictRuleId
            && row.LimitationIds.Any(id => id.Contains("commit-conflict", StringComparison.Ordinal)));
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectKind == "section"
            && row.SubjectId == "evidence-rows"
            && row.CompatibilityStatus == "partial"
            && row.LimitationIds.Any(id => id.Contains("commit-conflict", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Explorer_generate_marks_unusable_manifest_commit_as_missing_commit_gap()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, manifestCommitSha: "unknown", factCommitSha: FortyCharCommit("6"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Gaps, gap =>
            gap.RuleId == StaticHtmlEvidenceExplorer.MissingCommitRuleId
            && gap.GapKind == "missing-commit"
            && gap.AffectedSection == "sources");
    }

    [Fact]
    public async Task Explorer_generate_refuses_user_authored_output_collision()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(output);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("f"));
        await File.WriteAllTextAsync(Path.Combine(output, "index.html"), "<!doctype html><title>User file</title>");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output)));
        Assert.Contains(StaticHtmlEvidenceExplorer.UserFileCollisionRuleId, failure.Message);
        Assert.DoesNotContain(temp.Path, failure.Message);
    }

    [Fact]
    public async Task Explorer_generate_force_still_refuses_user_authored_output_without_manifest()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(output);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("9"));
        await File.WriteAllTextAsync(Path.Combine(output, "index.html"), "<!doctype html><title>User file</title>");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output, Force: true)));
        Assert.Contains(StaticHtmlEvidenceExplorer.UserFileCollisionRuleId, failure.Message);
        Assert.DoesNotContain(temp.Path, failure.Message);
    }

    [Fact]
    public async Task Explorer_generate_requires_force_for_prior_generated_output()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("5"));

        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output)));
        Assert.Contains(StaticHtmlEvidenceExplorer.GeneratedFileStaleRuleId, failure.Message);
    }

    [Fact]
    public async Task Explorer_generate_force_overwrites_prior_generated_output()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("7"));

        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));
        await File.WriteAllTextAsync(Path.Combine(output, "index.html"), "<!doctype html><title>stale generated output</title>");

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output, Force: true));

        Assert.Equal("public-demo", result.Manifest.SafetyProfile);
        Assert.Contains("TraceMap Evidence Explorer", await File.ReadAllTextAsync(Path.Combine(output, "index.html")));
        Assert.DoesNotContain("stale generated output", await File.ReadAllTextAsync(Path.Combine(output, "index.html")));
    }

    [Theory]
    [InlineData("tracemap-static-html-evidence-explorer.v1")]
    [InlineData("tracemap-static-html-evidence-explorer.v2")]
    [InlineData("tracemap-static-html-evidence-explorer.v3")]
    public async Task Explorer_generate_force_recognizes_prior_generated_manifest_during_v4_upgrade(string priorSchema)
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(Path.Combine(output, "data"));
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("7"));
        await File.WriteAllTextAsync(
            Path.Combine(output, "data", "explorer-manifest.json"),
            $$"""
            {"schemaVersion":"{{priorSchema}}","tracemapGenerated":true}
            """);
        await File.WriteAllTextAsync(Path.Combine(output, "index.html"), "<!doctype html><title>prior generated output</title>");

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output, Force: true));

        Assert.Equal("tracemap-static-html-evidence-explorer.v4", result.Manifest.SchemaVersion);
        Assert.DoesNotContain("prior generated output", await File.ReadAllTextAsync(Path.Combine(output, "index.html")));
    }

    [Fact]
    public async Task Explorer_generate_hidden_local_is_visibly_labeled_and_recorded()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await WriteScanArtifactsAsync(input, commitSha: FortyCharCommit("8"));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output, SafetyProfile: "hidden-local"));

        Assert.Equal("hidden-local", result.Manifest.SafetyProfile);
        Assert.Equal("hidden-local", result.Manifest.ClaimLevel);
        Assert.All(result.Manifest.Inputs, artifact => Assert.Equal("hidden-local", artifact.ClaimLevel));
        Assert.True(result.Manifest.Counts.RedactionCount > 0);
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectKind == "safety-profile"
            && row.SubjectId == "safety-profile:hidden-local"
            && row.CompatibilityStatus == "compatible");
        var html = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
        Assert.Contains("Hidden/local output", html);
        Assert.Contains("Redacted or hashed", html);
    }

    [Fact]
    public async Task Explorer_generate_distinguishes_empty_fact_stream_from_missing_or_unsupported_inputs()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        var manifest = new ScanManifest(
            "scan-empty",
            "repo",
            null,
            "main",
            FortyCharCommit("a"),
            "test-scanner.v1",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            "Level1SemanticAnalysis",
            "Succeeded",
            [],
            [],
            [],
            [],
            GitRootHash: "sha256:def");
        await ManifestWriter.WriteAsync(Path.Combine(input, "scan-manifest.json"), manifest);
        await File.WriteAllTextAsync(Path.Combine(input, "facts.ndjson"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(input, "unrecognized-report.json"), """{"schemaVersion":"unknown.v9","value":"safe"}""");

        await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var html = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
        Assert.Contains("No static evidence rows were found in the compatible evidence artifacts under the current coverage.", html);
        Assert.Contains("index.sqlite was not provided", html);
        Assert.Contains("A JSON artifact was discovered but is not supported", html);

        var dataJson = await File.ReadAllTextAsync(Path.Combine(output, "data", "explorer-data.json"));
        using var document = JsonDocument.Parse(dataJson);
        var sectionStatuses = document.RootElement.GetProperty("sectionStatuses").EnumerateArray().ToArray();
        Assert.Contains(sectionStatuses, row =>
            row.GetProperty("sectionId").GetString() == "evidence-rows"
            && row.GetProperty("status").GetString() == "no-evidence-under-current-coverage");
        Assert.Contains(sectionStatuses, row =>
            row.GetProperty("sectionId").GetString() == "surfaces"
            && row.GetProperty("status").GetString() == "not-provided");
        Assert.Contains(document.RootElement.GetProperty("compatibilityLedger").EnumerateArray(), row =>
            row.GetProperty("subjectId").GetString() == "artifact:facts-ndjson"
            && row.GetProperty("compatibilityStatus").GetString() == "compatible-empty");
    }

    [Fact]
    public async Task Explorer_generate_keeps_empty_evidence_section_partial_when_commit_provenance_is_missing()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await File.WriteAllTextAsync(Path.Combine(input, "facts.ndjson"), string.Empty);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var missingCommitGap = Assert.Single(result.Gaps, gap =>
            gap.RuleId == StaticHtmlEvidenceExplorer.MissingCommitRuleId
            && gap.GapKind == "missing-commit"
            && gap.AffectedSection == "evidence-rows");
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectKind == "artifact"
            && row.SubjectId == "artifact:facts-ndjson"
            && row.CompatibilityStatus == "partial"
            && row.LimitationIds.Contains(missingCommitGap.GapId));
        Assert.Contains(result.Data.CompatibilityLedger, row =>
            row.SubjectKind == "section"
            && row.SubjectId == "evidence-rows"
            && row.CompatibilityStatus == "partial"
            && row.LimitationIds.Contains(missingCommitGap.GapId));
        Assert.DoesNotContain(result.Data.CompatibilityLedger, row =>
            row.SubjectId == "evidence-rows"
            && row.CompatibilityStatus == "compatible-empty");
    }

    [Fact]
    public async Task Explorer_generate_handles_legacy_null_manifest_and_fact_fields_as_gaps()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan-output");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        await File.WriteAllTextAsync(Path.Combine(input, "scan-manifest.json"), """
            {
              "scanId": "legacy-scan",
              "repoName": "example-repo",
              "commitSha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "scannerVersion": "legacy-scanner",
              "scannedAt": "2026-01-01T00:00:00Z",
              "analysisLevel": null,
              "buildStatus": null,
              "knownGaps": null,
              "solutions": [],
              "projects": [],
              "targetFrameworks": []
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(input, "facts.ndjson"), """
            {"factId":"legacy-fact","scanId":"legacy-scan","repo":"example-repo","commitSha":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","factType":"TypeDeclared","ruleId":"csharp.syntax.declarations.v1","evidenceTier":"Tier3SyntaxOrTextual","evidence":null,"properties":null}

            """);

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        Assert.Contains(result.Gaps, gap => gap.GapKind == "missing-evidence-span");
        var html = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
        Assert.Contains("legacy-fact", await File.ReadAllTextAsync(Path.Combine(output, "data", "explorer-data.json")));
        Assert.Contains("UnknownAnalysisLevel", html);
    }

    [Fact]
    public async Task Explorer_cli_generates_bundle_from_scan_output()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var scanOutput = Path.Combine(temp.Path, "scan-output");
        var explorerOutput = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "Sample.cs"), "namespace Sample; public sealed class Widget { }");

        using var scanStdout = new StringWriter();
        using var scanStderr = new StringWriter();
        var scanExitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", scanOutput], scanStdout, scanStderr);
        Assert.Equal(0, scanExitCode);
        Assert.Equal(string.Empty, scanStderr.ToString());

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["explorer", "generate", "--input", scanOutput, "--out", explorerOutput], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("TraceMap explorer generate completed", output.ToString());
        Assert.True(File.Exists(Path.Combine(explorerOutput, "index.html")));
        Assert.True(File.Exists(Path.Combine(explorerOutput, "data", "explorer-manifest.json")));
    }

    [Fact]
    public async Task Help_for_explorer_returns_usage()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["explorer", "--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("tracemap explorer generate --input <artifact-dir> --out <explorer-output>", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    private static async Task WriteScanArtifactsAsync(string directory, string? commitSha = null, string? manifestCommitSha = null, string? factCommitSha = null)
    {
        var manifest = new ScanManifest(
            "scan-test",
            "example-repo",
            "git@example.com:internal/example-repo.git",
            "main",
            manifestCommitSha ?? commitSha ?? FortyCharCommit("1"),
            "test-scanner-token=redacted",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            "Level1SemanticAnalysisReduced",
            "Failed",
            ["Private.sln"],
            ["src/Private.csproj"],
            ["net10.0"],
            ["semantic-load-failed"],
            ScanRootRelativePath: null,
            ScanRootPathHash: "sha256:abc",
            GitRootHash: "sha256:def");
        await ManifestWriter.WriteAsync(Path.Combine(directory, "scan-manifest.json"), manifest);
        await JsonlFactWriter.WriteAsync(Path.Combine(directory, "facts.ndjson"), [Fact(factCommitSha ?? commitSha ?? FortyCharCommit("1"))]);
        await File.WriteAllTextAsync(Path.Combine(directory, "index.sqlite"), "not raw sqlite for this unit test");
        await File.WriteAllTextAsync(Path.Combine(directory, "report.md"), "# Report\n");
    }

    private static async Task WriteReleaseReviewArtifactAsync(
        string directory,
        string fixtureKind = "compatible",
        string? afterCommitSha = null)
    {
        var version = fixtureKind == "unsupported-version" ? "9.9" : "1.2";
        var beforeCommit = fixtureKind == "invalid-commit" ? "not-a-commit" : FortyCharCommit("d");
        var json = JsonSerializer.Serialize(new
        {
            reportType = "release-review",
            version,
            mode = "ReleaseReviewSingleV1",
            query = new { source = "private-source-name" },
            beforeSnapshot = new
            {
                side = "before",
                indexKind = "single",
                reportCoverage = "Full",
                sources = new[] { new { sourceLabel = "private-source-name", commitSha = beforeCommit } }
            },
            afterSnapshot = new
            {
                side = "after",
                indexKind = "single",
                reportCoverage = "Reduced",
                sources = new[] { new { sourceLabel = "private-source-name", commitSha = afterCommitSha ?? FortyCharCommit("e") } }
            },
            summary = new
            {
                rollupClassification = "ReviewRecommended",
                gapCount = 2,
                truncated = false,
                message = "private release message"
            },
            topChangedSurfaces = new
            {
                findings = new[]
                {
                    new
                    {
                        filePath = "C:\\private\\release\\Source.cs",
                        metadata = "SELECT secret_value FROM private_table"
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
        if (fixtureKind == "duplicate-version")
        {
            json = json.Replace("\"version\": \"1.2\",", "\"version\": \"1.2\",\n  \"version\": \"1.2\",", StringComparison.Ordinal);
        }

        await File.WriteAllTextAsync(Path.Combine(directory, "release-review.json"), json);
    }

    private static async Task WriteReducerImpactArtifactAsync(
        string directory,
        string commitSha,
        bool includeGap = false,
        bool reducedCoverage = false,
        bool truncated = false,
        string? fixtureKind = null)
    {
        var evidenceCommitSha = fixtureKind == "mismatched-commit" ? FortyCharCommit("f") : commitSha;
        var report = new
        {
            reportType = fixtureKind == "unsupported-report" ? "combined-change-impact" : "contract-delta-impact-single",
            version = "2.0",
            inputCompatibility = "ContractDeltaV2",
            reportCoverage = reducedCoverage ? "Reduced" : "Full",
            coverageWarnings = reducedCoverage ? new[] { "private coverage warning" } : Array.Empty<string>(),
            query = new
            {
                algorithm = "contract-delta-fact-match",
                algorithmVersion = "2.0"
            },
            index = new
            {
                indexKind = "single",
                sourceCount = 1,
                repoIdentityHash = "private-repo-hash",
                commitSha,
                analysisLevel = "Level1SemanticAnalysis",
                buildStatus = "Succeeded",
                sources = new[]
                {
                    new
                    {
                        label = "private-source-label",
                        sourceIndexId = (string?)null,
                        scanId = "private-scan-id",
                        language = "csharp",
                        commitSha,
                        scannerVersion = "test-reducer-v2",
                        analysisLevel = "Level1SemanticAnalysis",
                        buildStatus = "Succeeded",
                        repositoryIdentityHash = "private-repo-hash"
                    }
                }
            },
            summary = new
            {
                changeCount = 1,
                findingCount = 1,
                evidenceRowCount = 1,
                gapCount = includeGap ? 1 : 0,
                truncated
            },
            findings = new[]
            {
                new
                {
                    element = "Private.Customer.Email",
                    changeType = "changed",
                    classification = ImpactClassifications.DefiniteImpact,
                    ruleId = RuleIds.ContractDeltaImpact,
                    reason = "private impact reason",
                    warnings = new[] { "private finding warning" },
                    findingId = "private-finding-id",
                    changeId = "private-change-id",
                    changeKind = "property",
                    confidence = "high",
                    evidenceTier = EvidenceTiers.Tier1Semantic,
                    sourceLabel = "private-source-label",
                    reference = new Dictionary<string, string> { ["private-key"] = "private-value" },
                    pathContext = Array.Empty<object>(),
                    reverseContext = Array.Empty<object>(),
                    evidence = new[]
                    {
                        new
                        {
                            factId = "private-fact-id",
                            factType = "PropertyRead",
                            ruleId = RuleIds.CSharpSemanticPropertyAccess,
                            evidenceTier = EvidenceTiers.Tier1Semantic,
                            filePath = "C:\\private\\Customer.cs",
                            startLine = 10,
                            endLine = 11,
                            targetSymbol = "Private.Customer.Email",
                            contractElement = "Email",
                            commitSha = evidenceCommitSha,
                            sourceLabel = "private-source-label",
                            sourceIndexId = (string?)null,
                            scanId = "private-scan-id",
                            sourceSymbol = "Private.Customer.ReadEmail()",
                            metadata = new Dictionary<string, string> { ["private"] = "secret" }
                        }
                    },
                    limitations = new[] { "private finding limitation" }
                }
            },
            gaps = includeGap
                ? new[]
                {
                    new
                    {
                        gapId = "private-gap-id",
                        gapKind = "PrivateGapKind",
                        changeId = "private-change-id",
                        sourceLabel = "private-source-label",
                        ruleId = RuleIds.ContractDeltaImpact,
                        evidenceTier = EvidenceTiers.Tier4Unknown,
                        classification = ImpactClassifications.UnknownAnalysisGap,
                        message = "private reducer gap message",
                        supportingFactIds = new[] { "private-fact-id" }
                    }
                }
                : Array.Empty<object>(),
            limitations = new[] { "private top-level limitation" }
        };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        if (fixtureKind == "duplicate-provenance")
        {
            json = json.Replace(
                "\"scannerVersion\": \"test-reducer-v2\",",
                "\"scannerVersion\": \"test-reducer-v2\",\n        \"scannerVersion\": \"order-dependent-version\",",
                StringComparison.Ordinal);
        }

        await File.WriteAllTextAsync(Path.Combine(directory, "impact-report.json"), json);
    }

    private static async Task WritePathsReportArtifactAsync(
        string directory,
        string commitSha,
        bool invalidEdgeTarget = false,
        bool includePrivateGap = false,
        bool crossSourceEndpoint = false,
        bool reducedCoverage = false,
        bool omitPathLocations = false,
        bool truncated = false)
    {
        var serverCommitSha = FortyCharCommit("2");
        var primarySource = new
        {
            sourceIndexId = "private-source-index",
            label = "private-source-label",
            indexPathHash = "sha256:index",
            scanId = "private-scan",
            repoName = "private-repository",
            remoteUrl = "git@example.com:private/repository.git",
            branch = "private-branch",
            commitSha,
            scannerVersion = "test-scanner-v1",
            language = "csharp",
            storedLanguage = "csharp",
            languageCorrected = false,
            scanRootRelativePath = ".",
            scanRootPathHash = "sha256:root",
            gitRootHash = "sha256:git",
            analysisLevel = "Level1SemanticAnalysisReduced",
            buildStatus = "Succeeded"
        };
        var serverSource = new
        {
            sourceIndexId = "private-server-source-index",
            label = "private-server-source-label",
            indexPathHash = "sha256:server-index",
            scanId = "private-server-scan",
            repoName = "private-server-repository",
            remoteUrl = "git@example.com:private/server.git",
            branch = "private-server-branch",
            commitSha = serverCommitSha,
            scannerVersion = "test-server-scanner-v2",
            language = "csharp",
            storedLanguage = "csharp",
            languageCorrected = false,
            scanRootRelativePath = ".",
            scanRootPathHash = "sha256:server-root",
            gitRootHash = "sha256:server-git",
            analysisLevel = "Level1SemanticAnalysisReduced",
            buildStatus = "Succeeded"
        };
        var report = new
        {
            version = "1.0",
            schemaVersion = (string?)null,
            view = (string?)null,
            reportCoverage = reducedCoverage ? "ReducedCoverage" : "FullEvidenceAvailable",
            coverageWarnings = reducedCoverage ? new[] { "private warning text" } : Array.Empty<string>(),
            query = new
            {
                fromEndpoint = "private endpoint selector",
                fromSymbol = "Private.OrderService",
                fromSource = "private-source-label",
                fromWebFormsEvent = (string?)null,
                toSurface = "sql-query",
                surfaceName = "SELECT private_value FROM private_table",
                sourcePair = (string?)null,
                classification = (string?)null,
                includeLegacyRoots = false,
                maxDepth = 8,
                maxPaths = 100,
                maxFrontier = 10000,
                algorithm = "bounded-bfs",
                algorithmVersion = "1.0",
                messageDirection = "both"
            },
            sources = crossSourceEndpoint ? new[] { primarySource, serverSource } : new[] { primarySource },
            summary = new
            {
                sourceCount = crossSourceEndpoint ? 2 : 1,
                graphNodeCount = 2,
                graphEdgeCount = 1,
                pathCount = 1,
                gapCount = includePrivateGap ? 1 : 0,
                selectorCandidateCount = 1,
                truncated
            },
            paths = new[]
            {
                new
                {
                    pathId = "private-path-id",
                    classification = CombinedDependencyPathClassifications.NeedsReviewStaticPath,
                    confidence = "Low",
                    length = 1,
                    startNodeId = "private-node-start",
                    endNodeId = "private-node-end",
                    nodes = new object[]
                    {
                        new
                        {
                            nodeId = "private-node-start",
                            nodeKind = "Method",
                            displayName = "Private.OrderService",
                            sourceIndexId = "private-source-index",
                            sourceLabel = "private-source-label",
                            scanId = "private-scan",
                            commitSha,
                            symbolId = "Private.OrderService.Run()",
                            combinedFactId = "private-fact-entry",
                            ruleId = "csharp.semantic.invocation.v1",
                            evidenceTier = EvidenceTiers.Tier1Semantic,
                            filePath = "C:\\private\\OrderService.cs",
                            startLine = 10,
                            endLine = 12,
                            surfaceKind = (string?)null,
                            surfaceName = (string?)null,
                            httpMethod = (string?)null,
                            normalizedPathKey = (string?)null,
                            operationName = (string?)null,
                            tableName = (string?)null,
                            columnNames = (string?)null,
                            sourceKind = (string?)null,
                            shapeHash = (string?)null,
                            textHash = (string?)null,
                            textLength = (string?)null,
                            packageName = (string?)null,
                            configKey = (string?)null,
                            operationDirection = (string?)null,
                            surfaceSubtype = (string?)null,
                            limitations = Array.Empty<string>()
                        },
                        new
                        {
                            nodeId = "private-node-end",
                            nodeKind = "DependencySurface",
                            displayName = "SELECT private_value FROM private_table",
                            sourceIndexId = crossSourceEndpoint ? "private-server-source-index" : "private-source-index",
                            sourceLabel = crossSourceEndpoint ? "private-server-source-label" : "private-source-label",
                            scanId = crossSourceEndpoint ? "private-server-scan" : "private-scan",
                            commitSha = crossSourceEndpoint ? serverCommitSha : commitSha,
                            symbolId = (string?)null,
                            combinedFactId = "private-fact-surface",
                            ruleId = "database.sql.shape.v1",
                            evidenceTier = EvidenceTiers.Tier2Structural,
                            filePath = omitPathLocations ? null : "C:\\private\\OrderService.cs",
                            startLine = omitPathLocations ? (int?)null : 20,
                            endLine = omitPathLocations ? (int?)null : 22,
                            surfaceKind = "sql-query",
                            surfaceName = "SELECT private_value FROM private_table",
                            httpMethod = (string?)null,
                            normalizedPathKey = (string?)null,
                            operationName = "select",
                            tableName = "private_table",
                            columnNames = "private_value",
                            sourceKind = "sql",
                            shapeHash = "sha256:private-shape",
                            textHash = "sha256:private-text",
                            textLength = "39",
                            packageName = (string?)null,
                            configKey = (string?)null,
                            operationDirection = "read",
                            surfaceSubtype = "query",
                            limitations = new[] { "private surface limitation" }
                        }
                    },
                    edges = new[]
                    {
                        new
                        {
                            edgeId = "private-edge-id",
                            edgeKind = crossSourceEndpoint ? "endpoint-match" : "calls",
                            fromNodeId = "private-node-start",
                            toNodeId = invalidEdgeTarget ? "wrong-private-node" : "private-node-end",
                            classification = crossSourceEndpoint ? CombinedEndpointClassifications.MatchedEndpoint : "EvidenceEdge",
                            ruleId = crossSourceEndpoint ? "combined.paths.endpoint-match.v1" : "combined.paths.path.v1",
                            evidenceTier = crossSourceEndpoint ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier3SyntaxOrTextual,
                            supportingFactIds = new[] { "private-support-fact" },
                            supportingCombinedEdgeIds = new[] { "private-support-edge" },
                            filePath = omitPathLocations ? null : "C:\\private\\OrderService.cs",
                            startLine = omitPathLocations ? (int?)null : 14,
                            endLine = omitPathLocations ? (int?)null : 14,
                            registrationContext = (string?)null,
                            supportingRegistrationFactIds = Array.Empty<string>(),
                            candidateCount = (int?)null,
                            omittedCount = (int?)null,
                            candidateLimit = (int?)null,
                            candidateCapReason = (string?)null,
                            candidateState = (string?)null,
                            candidateBridgeKind = (string?)null,
                            supportingRelationshipIds = Array.Empty<string>()
                        }
                    },
                    supportingFactIds = new[] { "private-support-fact" },
                    supportingEdgeIds = new[] { "private-support-edge" },
                    notes = new[] { new { code = "PrivateNote", message = "private path note" } }
                }
            },
            gaps = includePrivateGap
                ? new object[]
                {
                    new
                    {
                        gapId = "private-gap-id",
                        gapKind = "PrivateCustomerGap",
                        classification = CombinedDependencyPathClassifications.AnalysisGap,
                        message = "private customer gap message",
                        sourceIndexId = "private-source-index",
                        sourceLabel = "private-source-label",
                        nodeId = "private-node-start",
                        combinedFactId = "private-gap-fact",
                        ruleId = "combined.paths.path.v1",
                        evidenceTier = EvidenceTiers.Tier2Structural,
                        filePath = "C:\\private\\OrderService.cs",
                        startLine = 30,
                        reason = "private reason",
                        commitSha,
                        extractorVersion = "private-extractor",
                        evidenceScope = "private scope",
                        endLine = 31,
                        supportingFactIds = new[] { "private-gap-support" }
                    }
                }
                : Array.Empty<object>(),
            inventory = new
            {
                nodesByKind = new Dictionary<string, int> { ["Method"] = 1, ["DependencySurface"] = 1 },
                edgesByKind = new Dictionary<string, int> { ["calls"] = 1 },
                nodesBySource = new Dictionary<string, int> { ["private-source-index"] = 2 },
                surfacesByKind = new Dictionary<string, int> { ["sql-query"] = 1 },
                gapsByKind = new Dictionary<string, int>(),
                evidenceNodes = Array.Empty<object>(),
                evidenceEdges = Array.Empty<object>()
            },
            limitations = new[] { "SELECT private_value FROM private_table" }
        };

        await File.WriteAllTextAsync(
            Path.Combine(directory, "paths-report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static CodeFact Fact(string commitSha)
    {
        return new CodeFact(
            "fact-1",
            "scan-test",
            "example-repo",
            commitSha,
            "src/Private.csproj",
            FactTypes.TypeDeclared,
            RuleIds.CSharpSyntaxDeclarations,
            EvidenceTiers.Tier3SyntaxOrTextual,
            "Sample.Widget",
            null,
            null,
            new EvidenceSpan("C:\\sample-root\\src\\Widget.cs", 10, 12, "public class Secret { }", "test.extractor", "test.extractor.v1"),
            new Dictionary<string, string>
            {
                ["connectionString"] = "Server=prod;Password=secret"
            });
    }

    private static SortedDictionary<string, string> RelativeFileMap(string root)
    {
        return new SortedDictionary<string, string>(
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                    File.ReadAllText),
            StringComparer.Ordinal);
    }

    private static string FortyCharCommit(string character)
    {
        return string.Concat(Enumerable.Repeat(character, 40));
    }

    private static int SectionRowIndex(string html, string label)
    {
        var index = html.IndexOf($"<tr><th scope=\"row\">{label}</th>", StringComparison.Ordinal);
        Assert.True(index >= 0, $"Expected section status row for {label}.");
        return index;
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "rules", "rule-catalog.yml")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
