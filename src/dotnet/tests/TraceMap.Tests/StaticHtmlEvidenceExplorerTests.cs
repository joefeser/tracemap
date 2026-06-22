using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
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
            limitation.RuleId == StaticHtmlEvidenceExplorer.ProvenanceConflictRuleId
            && limitation.LimitationKind == "claim-level-conflict-detection-deferred"
            && limitation.ClaimEffect == "claim-level");
        Assert.Contains("Local generated artifact", allGenerated);
        Assert.Contains("does not rescan source code", allGenerated);
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
        Assert.True(
            html.IndexOf("<tr><th scope=\"row\">Evidence Overview</th>", StringComparison.Ordinal)
            < html.IndexOf("<tr><th scope=\"row\">Sources</th>", StringComparison.Ordinal));
        Assert.True(
            html.IndexOf("<tr><th scope=\"row\">Sources</th>", StringComparison.Ordinal)
            < html.IndexOf("<tr><th scope=\"row\">Artifacts</th>", StringComparison.Ordinal));
        Assert.True(
            html.IndexOf("<tr><th scope=\"row\">Artifacts</th>", StringComparison.Ordinal)
            < html.IndexOf("<tr><th scope=\"row\">Evidence Rows</th>", StringComparison.Ordinal));

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
        Assert.Contains("No static evidence rows were found in the provided fact stream under the current coverage.", html);
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
}
