using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class LegacyBaselineArtifactsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Create_from_synthetic_scan_is_deterministic_and_counts_only()
    {
        var firstOut = TestBaselinePath("deterministic-a");
        var secondOut = TestBaselinePath("deterministic-b");
        DeleteIfExists(firstOut);
        DeleteIfExists(secondOut);

        var first = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            "samples/synthetic-legacy-scan",
            "synthetic-alpha",
            "original-parser-snapshot",
            firstOut,
            CreatedAt: "2026-06"));
        var second = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            "samples/synthetic-legacy-scan",
            "synthetic-alpha",
            "original-parser-snapshot",
            secondOut,
            CreatedAt: "2026-06"));

        Assert.Equal("synthetic-alpha__original-parser-snapshot__2026-06", first.Manifest.BaselineId);
        Assert.Equal(LegacyBaselineClassifications.PublicSafe, first.Manifest.Safety.Classification);
        Assert.Equal(4, first.Manifest.Counts.FactsTotal);
        Assert.Equal(1, first.Manifest.Counts.GapsTotal);
        Assert.Equal(1, first.Manifest.Counts.ByEvidenceTier["Tier1Semantic"]);
        Assert.Equal(2, first.Manifest.Counts.ByEvidenceTier["Tier2Structural"]);
        Assert.Equal(1, first.Manifest.Counts.ByEvidenceTier["Tier4Unknown"]);
        Assert.Equal("observed", first.Manifest.Surfaces["csharp"]);
        Assert.Equal("observed", first.Manifest.Surfaces["ui-events"]);
        Assert.Equal("observed", first.Manifest.Surfaces["packages"]);
        Assert.Equal("not-in-scope", first.Manifest.Surfaces["wcf-service-reference"]);
        Assert.Null(first.Manifest.Sample.RepoIdentityHash);
        Assert.Null(first.Manifest.Sample.CommitIdentity.Value);
        Assert.Equal("2026-06", first.Manifest.CreatedAt);
        Assert.Equal("2026-06", first.Manifest.Scan.ScanStartedAt);

        var firstJson = await File.ReadAllTextAsync(first.ManifestPath!);
        var secondJson = await File.ReadAllTextAsync(second.ManifestPath!);
        Assert.Equal(firstJson, secondJson);
        Assert.DoesNotContain("Controllers/HomeController.cs", firstJson);
        Assert.DoesNotContain("Synthetic.Package", firstJson);
        Assert.DoesNotContain("select ", firstJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dry_run_reports_classification_without_writing_files()
    {
        var outputPath = TestBaselinePath("dry-run");
        DeleteIfExists(outputPath);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "baseline",
            "create",
            "--scan-output",
            "samples/synthetic-legacy-scan",
            "--label",
            "synthetic-alpha",
            "--purpose",
            "original-parser-snapshot",
            "--out",
            outputPath,
            "--created-at",
            "2026-06",
            "--dry-run"
        ], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Safety classification: public-safe", output.ToString());
        Assert.False(Directory.Exists(outputPath));
        Assert.Equal(string.Empty, error.ToString());
    }

    [Theory]
    [InlineData("owner/repo")]
    [InlineData("https://example.test/repo")]
    [InlineData("@owner")]
    [InlineData("sample.git")]
    [InlineData("~/sample")]
    [InlineData("C:\\sample")]
    [InlineData("repo.example.com")]
    [InlineData("too-many-identity-looking-parts-in-this-label")]
    public async Task Unsafe_labels_are_rejected(string label)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync([
            "baseline",
            "create",
            "--scan-output",
            "samples/synthetic-legacy-scan",
            "--label",
            label,
            "--purpose",
            "original-parser-snapshot",
            "--out",
            TestBaselinePath("bad-label"),
            "--dry-run"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("neutral slug", error.ToString());
    }

    [Fact]
    public async Task Validate_rejects_unsafe_content_without_echoing_value()
    {
        var outputPath = TestBaselinePath("unsafe-validate");
        DeleteIfExists(outputPath);
        var result = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            "samples/synthetic-legacy-scan",
            "synthetic-alpha",
            "original-parser-snapshot",
            outputPath,
            CreatedAt: "2026-06"));
        var unsafeText = (await File.ReadAllTextAsync(result.ManifestPath!)).Replace("synthetic-alpha", "/home/example/private-sample", StringComparison.Ordinal);
        await File.WriteAllTextAsync(result.ManifestPath!, unsafeText);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["baseline", "validate", "--manifest", result.ManifestPath!], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("absolute-path", error.ToString());
        Assert.Contains("ruleId=legacy.baseline.safety-validation.v1", error.ToString());
        Assert.Contains("path=", error.ToString());
        Assert.DoesNotContain("/home/example/private-sample", error.ToString());
    }

    [Fact]
    public async Task Unknown_rule_ids_mark_create_as_rejected()
    {
        var scanPath = TestBaselinePath("unknown-rule-scan");
        DeleteIfExists(scanPath);
        Directory.CreateDirectory(scanPath);
        File.Copy(Path.Combine(RepoRoot(), "samples", "synthetic-legacy-scan", "scan-manifest.json"), Path.Combine(scanPath, "scan-manifest.json"));
        await File.WriteAllTextAsync(Path.Combine(scanPath, "facts.ndjson"), """
            {"factId":"fact-unknown","scanId":"synthetic-legacy-scan-001","repo":"synthetic-legacy-fixture","commitSha":"1111111111111111111111111111111111111111","projectPath":null,"factType":"MethodDeclared","ruleId":"project.file.v","evidenceTier":"Tier3SyntaxOrTextual","sourceSymbol":null,"targetSymbol":null,"contractElement":null,"evidence":{"filePath":"src/Synthetic/Unknown.cs","startLine":1,"endLine":1,"snippetHash":null,"extractorId":"synthetic","extractorVersion":"1.0.0"},"properties":{}}
            """);

        var result = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            scanPath,
            "synthetic-alpha",
            "original-parser-snapshot",
            TestBaselinePath("unknown-rule-out"),
            CreatedAt: "2026-06"));

        Assert.Equal(LegacyBaselineClassifications.Rejected, result.Manifest.Safety.Classification);
        Assert.Contains(result.Diagnostics, item => item.Category == "rule-catalog-entry-missing");
        Assert.Null(result.ManifestPath);
    }

    [Fact]
    public async Task Local_only_and_public_identity_boundaries_are_classified()
    {
        var localOnly = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            "samples/synthetic-legacy-scan",
            "synthetic-alpha",
            "original-parser-snapshot",
            TestBaselinePath("local-only"),
            LegacyBaselineClassifications.LocalOnly,
            "2026-06"));

        Assert.Equal(LegacyBaselineClassifications.LocalOnly, localOnly.Manifest.Safety.Classification);
        Assert.Equal("neutral-label", localOnly.Manifest.Sample.IdentityKind);
        Assert.Null(localOnly.Manifest.Sample.RepoIdentityHash);

        var publicSource = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            "samples/synthetic-legacy-scan",
            "synthetic-alpha",
            "original-parser-snapshot",
            TestBaselinePath("public-source"),
            CreatedAt: "2026-06",
            PublicSourceIdentity: true));

        Assert.Equal("public-repo-sha", publicSource.Manifest.Sample.IdentityKind);
        Assert.StartsWith("sha256:", publicSource.Manifest.Sample.RepoIdentityHash, StringComparison.Ordinal);
        Assert.Equal("1111111111111111111111111111111111111111", publicSource.Manifest.Sample.CommitIdentity.Value);

        var secretScan = TestBaselinePath("secret-identity-scan");
        DeleteIfExists(secretScan);
        Directory.CreateDirectory(secretScan);
        var manifest = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "samples", "synthetic-legacy-scan", "scan-manifest.json"));
        manifest = manifest.Replace("synthetic-legacy-fixture", "token-fixture", StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(secretScan, "scan-manifest.json"), manifest);
        await File.WriteAllTextAsync(Path.Combine(secretScan, "facts.ndjson"), "");
        var secretSource = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            secretScan,
            "synthetic-alpha",
            "original-parser-snapshot",
            TestBaselinePath("secret-source"),
            CreatedAt: "2026-06",
            PublicSourceIdentity: true));

        Assert.Equal("private-category-only", secretSource.Manifest.Sample.IdentityKind);
        Assert.Null(secretSource.Manifest.Sample.RepoIdentityHash);
        Assert.Equal("omitted-secret-like", secretSource.Manifest.Sample.CommitIdentity.Kind);
    }

    [Fact]
    public async Task Empty_in_scope_scan_and_gap_states_are_labeled()
    {
        var scanPath = TestBaselinePath("empty-scan");
        DeleteIfExists(scanPath);
        Directory.CreateDirectory(scanPath);
        await File.WriteAllTextAsync(Path.Combine(scanPath, "scan-manifest.json"), """
            {
              "scanId": "empty-scan",
              "repoName": "synthetic-empty-fixture",
              "remoteUrl": null,
              "branch": null,
              "commitSha": "2222222222222222222222222222222222222222",
              "scannerVersion": "test-fixture",
              "scannedAt": "2026-06-01T00:00:00Z",
              "analysisLevel": "Level1SemanticAnalysisReduced",
              "buildStatus": "FailedOrPartial",
              "solutions": ["Empty.sln"],
              "projects": ["src/Empty/Empty.csproj"],
              "targetFrameworks": ["net48"],
              "knownGaps": ["TimeoutExceeded", "DeferredLargeArtifact", "TruncatedByLimit", "SyntaxFallbackUsed"]
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(scanPath, "facts.ndjson"), "");

        var result = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            scanPath,
            "synthetic-alpha",
            "original-parser-snapshot",
            TestBaselinePath("empty-out"),
            CreatedAt: "2026-06"));

        Assert.Equal(0, result.Manifest.Counts.FactsTotal);
        Assert.True(result.Manifest.Scan.Partial);
        Assert.True(result.Manifest.Scan.Timeout);
        Assert.True(result.Manifest.Scan.Truncated);
        Assert.True(result.Manifest.Scan.Deferred);
        Assert.Equal("not-observed", result.Manifest.Surfaces["csharp"]);
        Assert.Equal("not-observed", result.Manifest.Surfaces["packages"]);
        Assert.Equal("not-in-scope", result.Manifest.Surfaces["wcf-service-reference"]);
    }

    [Fact]
    public async Task Compare_reports_movements_review_flags_and_neutral_markdown()
    {
        var baselineOut = TestBaselinePath("compare-baseline");
        var candidateScan = TestBaselinePath("compare-candidate-scan");
        var candidateOut = TestBaselinePath("compare-candidate");
        var compareOut = TestBaselinePath("comparisons/synthetic-alpha");
        DeleteIfExists(baselineOut);
        DeleteIfExists(candidateScan);
        DeleteIfExists(candidateOut);
        DeleteIfExists(compareOut);
        CopyDirectory(Path.Combine(RepoRoot(), "samples", "synthetic-legacy-scan"), candidateScan);
        await File.AppendAllTextAsync(Path.Combine(candidateScan, "facts.ndjson"), "\n" + """
            {"factId":"fact-005","scanId":"synthetic-legacy-scan-001","repo":"synthetic-legacy-fixture","commitSha":"1111111111111111111111111111111111111111","projectPath":"src/Synthetic/Synthetic.csproj","factType":"HttpCallDetected","ruleId":"http.client.invocation.v1","evidenceTier":"Tier2Structural","sourceSymbol":null,"targetSymbol":null,"contractElement":"GET /fixture","evidence":{"filePath":"src/Synthetic/Clients/FixtureClient.cs","startLine":4,"endLine":4,"snippetHash":null,"extractorId":"csharp.syntax","extractorVersion":"1.0.0"},"properties":{"method":"GET"}}
            """);

        var baseline = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            "samples/synthetic-legacy-scan",
            "synthetic-alpha",
            "original-parser-snapshot",
            baselineOut,
            CreatedAt: "2026-06"));
        var candidate = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            candidateScan,
            "synthetic-alpha",
            "candidate",
            candidateOut,
            CreatedAt: "2026-07"));

        var comparison = await LegacyBaselineArtifacts.CompareAsync(new LegacyBaselineCompareOptions(
            baseline.ManifestPath!,
            candidate.ManifestPath!,
            compareOut,
            GeneratedAt: "2026-07"));

        Assert.Equal("unchanged-or-increase-only", comparison.Comparison.OverallStatus);
        Assert.Contains(comparison.Comparison.Dimensions["byFactType"], row => row.Category == "HttpCallDetected" && row.Movement == "new-category");
        Assert.Contains(comparison.Comparison.Dimensions["totals"], row => row.Category == "factsTotal" && row.Movement == "increase");
        Assert.Contains(comparison.Comparison.Dimensions["coverage"], row => row.Category == "coverageLabel" && row.Movement == "unchanged");
        var markdown = await File.ReadAllTextAsync(comparison.MarkdownPath);
        foreach (var phrase in new[] { "impacted", "safe", "unsafe", "reachable", "production", "business" })
        {
            Assert.DoesNotContain(phrase, markdown, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Compare_rejects_tracked_output_without_writing_files()
    {
        var baselineOut = TestBaselinePath("tracked-boundary-baseline");
        var candidateOut = TestBaselinePath("tracked-boundary-candidate");
        var unsafeOut = Path.Combine(RepoRoot(), ".kiro", "baselines", "legacy", "tracked-comparison-output");
        DeleteIfExists(baselineOut);
        DeleteIfExists(candidateOut);
        DeleteIfExists(unsafeOut);

        var baseline = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            "samples/synthetic-legacy-scan",
            "synthetic-alpha",
            "original-parser-snapshot",
            baselineOut,
            CreatedAt: "2026-06"));
        var candidate = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            "samples/synthetic-legacy-scan",
            "synthetic-alpha",
            "candidate",
            candidateOut,
            CreatedAt: "2026-07"));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "baseline",
            "compare",
            "--baseline",
            baseline.ManifestPath!,
            "--candidate",
            candidate.ManifestPath!,
            "--out",
            unsafeOut,
            "--generated-at",
            "2026-07"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains(".tmp/legacy-baselines", error.ToString());
        Assert.False(File.Exists(Path.Combine(unsafeOut, "comparison.json")));
        Assert.False(File.Exists(Path.Combine(unsafeOut, "comparison.md")));
    }

    [Fact]
    public async Task Schema_mismatch_requires_matching_migration_map()
    {
        var baselineOut = TestBaselinePath("schema-baseline");
        var candidateOut = TestBaselinePath("schema-candidate");
        var compareOut = TestBaselinePath("comparisons/schema");
        DeleteIfExists(baselineOut);
        DeleteIfExists(candidateOut);
        DeleteIfExists(compareOut);

        var baseline = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            "samples/synthetic-legacy-scan",
            "synthetic-alpha",
            "original-parser-snapshot",
            baselineOut,
            CreatedAt: "2026-06"));
        var candidate = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            "samples/synthetic-legacy-scan",
            "synthetic-alpha",
            "candidate",
            candidateOut,
            CreatedAt: "2026-07"));

        var candidateManifest = candidate.Manifest with { SchemaVersion = "legacy-baseline-manifest.v2" };
        await WriteJsonAsync(candidate.ManifestPath!, candidateManifest);

        var comparison = await LegacyBaselineArtifacts.CompareAsync(new LegacyBaselineCompareOptions(
            baseline.ManifestPath!,
            candidate.ManifestPath!,
            compareOut,
            GeneratedAt: "2026-07"));

        Assert.Equal("not-comparable", comparison.Comparison.SchemaCompatibility.Status);
        Assert.Contains(comparison.Comparison.ReviewNeeded, item => item.Category == "schema");

        var migrationMapPath = Path.Combine(compareOut, "migration-map.json");
        await WriteJsonAsync(migrationMapPath, new LegacyBaselineMigrationMap(
            LegacyBaselineSchemas.MigrationMap,
            LegacyBaselineSchemas.Manifest,
            "legacy-baseline-manifest.v2",
            [new LegacyBaselineRuleRename("csharp.semantic.declarations.v1", "csharp.semantic.declarations.v1", "schema test")],
            [new LegacyBaselineFactTypeRename("MethodDeclared", "MethodDeclared", "schema test")],
            ["Fixture migration map for schema compatibility tests."]));
        var withMigration = await LegacyBaselineArtifacts.CompareAsync(new LegacyBaselineCompareOptions(
            baseline.ManifestPath!,
            candidate.ManifestPath!,
            compareOut,
            migrationMapPath,
            "2026-07"));

        Assert.Equal("comparable-with-migration-map", withMigration.Comparison.SchemaCompatibility.Status);
    }

    [Fact]
    public void Redaction_hash_is_context_separated_and_stable()
    {
        var first = LegacyBaselineArtifacts.RedactionHash("ab", "c", "value");
        var second = LegacyBaselineArtifacts.RedactionHash("a", "bc", "value");

        Assert.StartsWith("sha256:", first, StringComparison.Ordinal);
        Assert.Equal(71, first.Length);
        Assert.Equal(first, LegacyBaselineArtifacts.RedactionHash("ab", "c", "value"));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Tmp_legacy_baselines_is_git_ignored()
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git", "check-ignore .tmp/legacy-baselines/example")
        {
            WorkingDirectory = RepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = System.Diagnostics.Process.Start(startInfo)!;
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
    }

    [Fact]
    public async Task Baseline_help_is_available()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["baseline", "--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("tracemap baseline create", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Checked_in_public_safe_fixture_validates()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync([
            "baseline",
            "validate",
            "--manifest",
            ".kiro/baselines/legacy/synthetic-alpha__original-parser-snapshot__2026-06/baseline-manifest.json"
        ], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Safety classification: public-safe", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repo root.");
    }

    private static string TestBaselinePath(string suffix)
    {
        return Path.Combine(RepoRoot(), ".tmp", "legacy-baselines", "tests", suffix);
    }

    private static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, destination, StringComparison.Ordinal), overwrite: true);
        }
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions) + "\n");
    }
}
