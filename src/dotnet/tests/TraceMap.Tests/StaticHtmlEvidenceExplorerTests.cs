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
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.OmittedUnsafeValueRuleId
            && redaction.Location == "facts.properties"
            && redaction.Category == "secret-like-value");
        Assert.Contains(result.Manifest.Redactions, redaction =>
            redaction.RuleId == StaticHtmlEvidenceExplorer.OmittedUnsafeValueRuleId
            && redaction.Location == "scan-manifest.remoteUrl");
        Assert.Contains("Local generated artifact", allGenerated);
        Assert.Contains("does not rescan source code", allGenerated);
    }

    [Fact]
    public void Explorer_generated_string_validator_rejects_remote_references_without_printing_raw_value()
    {
        var failure = Assert.Throws<InvalidOperationException>(() =>
            StaticHtmlEvidenceExplorer.ValidateGeneratedStringsForTesting(new Dictionary<string, string>
            {
                ["index.html"] = "<a href=\"https://private.example.test/path\">unsafe</a>"
            }));

        Assert.Contains(StaticHtmlEvidenceExplorer.UnsafeRejectedRuleId, failure.Message);
        Assert.Contains("index.html", failure.Message);
        Assert.DoesNotContain("private.example.test", failure.Message);

        StaticHtmlEvidenceExplorer.ValidateGeneratedStringsForTesting(new Dictionary<string, string>
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
        Assert.Contains(StaticHtmlEvidenceExplorer.GeneratedFileStaleRuleId, failure.Message);
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
            "test-scanner.v1",
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
