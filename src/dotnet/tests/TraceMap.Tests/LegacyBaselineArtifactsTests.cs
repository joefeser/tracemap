using System.Diagnostics;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class LegacyBaselineArtifactsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [Fact]
    public async Task Create_is_deterministic_and_counts_only_for_synthetic_fixture()
    {
        using var temp = new TempDirectory();
        var firstOut = Path.Combine(temp.Path, "first");
        var secondOut = Path.Combine(temp.Path, "second");
        var createdAt = new DateTimeOffset(2026, 6, 12, 13, 14, 15, TimeSpan.Zero);

        var first = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            SyntheticScanPath(),
            "synthetic-alpha",
            "original-parser-snapshot",
            firstOut,
            CreatedAt: createdAt));
        var second = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            SyntheticScanPath(),
            "synthetic-alpha",
            "original-parser-snapshot",
            secondOut,
            CreatedAt: createdAt));

        Assert.Equal("public-safe", first.Validation.Classification);
        Assert.Equal("synthetic-alpha__original-parser-snapshot__2026-06", first.Manifest.BaselineId);
        Assert.Equal("2026-06", first.Manifest.CreatedAt);
        Assert.True(first.Manifest.Scan.Partial);
        Assert.Equal(5, first.Manifest.Counts.FactsTotal);
        Assert.Equal(2, first.Manifest.Counts.ByRuleId["repo.manifest.v1"]);
        Assert.Equal(2, first.Manifest.Counts.ByEvidenceTier["Tier3SyntaxOrTextual"]);
        Assert.Equal("observed", first.Manifest.CoverageSnapshot.Surfaces.Single(surface => surface.Surface == "http").Status);
        Assert.Equal("not-in-scope", first.Manifest.CoverageSnapshot.Surfaces.Single(surface => surface.Surface == "wcf-service-reference").Status);
        Assert.Equal("unknown", first.Manifest.CoverageSnapshot.Surfaces.Single(surface => surface.Surface == "config").Status);
        Assert.Null(first.Manifest.Sample.RepoIdentityHash);
        Assert.Equal("category-only", first.Manifest.Sample.CommitIdentity.Kind);

        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(firstOut, "baseline-manifest.json")),
            await File.ReadAllTextAsync(Path.Combine(secondOut, "baseline-manifest.json")));
        Assert.DoesNotContain("/Users/", await File.ReadAllTextAsync(Path.Combine(firstOut, "baseline-manifest.json")));
        Assert.DoesNotContain("SemanticLoadFailed: fixture compiler package unavailable", await File.ReadAllTextAsync(Path.Combine(firstOut, "baseline-manifest.json")));
    }

    [Fact]
    public async Task Create_dry_run_reports_classification_without_writing()
    {
        using var temp = new TempDirectory();
        var outputPath = Path.Combine(temp.Path, "dry-run-output");

        var result = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            SyntheticScanPath(),
            "synthetic-alpha",
            "original-parser-snapshot",
            outputPath,
            DryRun: true,
            CreatedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal("public-safe", result.Validation.Classification);
        Assert.False(Directory.Exists(outputPath));
        Assert.Null(result.ManifestPath);
    }

    [Fact]
    public async Task Local_only_output_must_stay_under_ignored_tmp_storage()
    {
        using var temp = new TempDirectory();
        var localOnlyOut = Path.Combine(temp.Path, ".tmp", "legacy-baselines", "synthetic-alpha__original-parser-snapshot__2026-06");

        var result = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            SyntheticScanPath(),
            "synthetic-alpha",
            "original-parser-snapshot",
            localOnlyOut,
            CreatedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            LocalOnly: true));

        Assert.Equal("local-only", result.Manifest.Safety.Classification);
        Assert.True(File.Exists(Path.Combine(localOnlyOut, "baseline-manifest.json")));

        await Assert.ThrowsAsync<ArgumentException>(() => LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            SyntheticScanPath(),
            "synthetic-alpha",
            "original-parser-snapshot",
            Path.Combine(temp.Path, "tracked"),
            CreatedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            LocalOnly: true)));
    }

    [Fact]
    public void Safety_validator_rejects_unsafe_values_without_echoing_them()
    {
        var validation = LegacyBaselineArtifacts.ValidateText(
            """
            {"path":"/Users/example/private/sample","secret":"password=fixture","query":"select * from customers"}
            """,
            "candidate.json",
            "public-safe");

        Assert.Equal("rejected", validation.Classification);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Category == "absolute-path");
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Category == "raw-sql");
        Assert.DoesNotContain(validation.Diagnostics, diagnostic => diagnostic.Category.Contains("example", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("../sample")]
    [InlineData("https://example.invalid/repo")]
    [InlineData("@owner")]
    [InlineData("repo.git")]
    [InlineData("~/repo")]
    [InlineData("C:\\repo")]
    [InlineData("example.com")]
    public async Task Create_rejects_labels_that_look_identifying(string label)
    {
        using var temp = new TempDirectory();
        await Assert.ThrowsAsync<ArgumentException>(() => LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            SyntheticScanPath(),
            label,
            "original-parser-snapshot",
            Path.Combine(temp.Path, "out"),
            DryRun: true,
            CreatedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero))));
    }

    [Fact]
    public async Task Create_rejects_observed_rule_ids_missing_from_catalog()
    {
        using var temp = new TempDirectory();
        var catalogPath = Path.Combine(temp.Path, "rule-catalog.yml");
        await File.WriteAllTextAsync(catalogPath, """
            rules:
              - id: repo.manifest.v1
            """);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            SyntheticScanPath(),
            "synthetic-alpha",
            "original-parser-snapshot",
            Path.Combine(temp.Path, "out"),
            DryRun: true,
            CreatedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            RuleCatalogPath: catalogPath)));

        Assert.Contains("csharp.syntax.aspnetroute.v1", ex.Message);
    }

    [Fact]
    public async Task Compare_reports_movements_and_markdown_avoids_prohibited_claims()
    {
        using var temp = new TempDirectory();
        var baselineOut = Path.Combine(temp.Path, "baseline");
        var candidateOut = Path.Combine(temp.Path, "candidate");
        var comparisonOut = Path.Combine(temp.Path, ".tmp", "legacy-baselines", "comparisons", "synthetic-alpha");
        var baseline = await LegacyBaselineArtifacts.CreateAsync(new LegacyBaselineCreateOptions(
            SyntheticScanPath(),
            "synthetic-alpha",
            "original-parser-snapshot",
            baselineOut,
            CreatedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));
        Directory.CreateDirectory(candidateOut);

        var candidateRuleCounts = new SortedDictionary<string, int>(baseline.Manifest.Counts.ByRuleId, StringComparer.Ordinal)
        {
            ["config.key.v1"] = 1
        };
        var candidateCounts = baseline.Manifest.Counts with
        {
            FactsTotal = baseline.Manifest.Counts.FactsTotal + 1,
            ByRuleId = candidateRuleCounts
        };
        var candidate = baseline.Manifest with
        {
            BaselineId = "synthetic-alpha__candidate__2026-07",
            BaselinePurpose = "candidate",
            CreatedAt = "2026-07",
            Counts = candidateCounts,
            Scan = baseline.Manifest.Scan with { CoverageLabel = "Level1SemanticAnalysis" }
        };
        var candidatePath = Path.Combine(candidateOut, "baseline-manifest.json");
        await File.WriteAllTextAsync(candidatePath, JsonSerializer.Serialize(candidate, JsonOptions) + "\n");

        var result = await LegacyBaselineArtifacts.CompareAsync(new LegacyBaselineCompareOptions(
            Path.Combine(baselineOut, "baseline-manifest.json"),
            candidatePath,
            comparisonOut,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal("review-needed", result.Comparison.OverallStatus);
        Assert.Contains(result.Comparison.Dimensions.ByRuleId, row => row.Name == "config.key.v1" && row.Movement == "new-category");
        Assert.Contains(result.Comparison.Dimensions.Coverage, row => row.Name == "coverageLabel" && row.Movement == "coverage-changed");
        var markdown = await File.ReadAllTextAsync(Path.Combine(comparisonOut, "comparison.md"));
        foreach (var phrase in new[] { "impacted", "safe", "unsafe", "reachable", "production", "business" })
        {
            Assert.DoesNotMatch($@"\b{phrase}\b", markdown.ToLowerInvariant());
        }
    }

    [Fact]
    public async Task Cli_baseline_create_dry_run_uses_normal_command_surface()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var temp = new TempDirectory();

        var exitCode = await TraceMapCommand.RunAsync([
            "baseline",
            "create",
            "--scan-output",
            SyntheticScanPath(),
            "--label",
            "synthetic-alpha",
            "--purpose",
            "original-parser-snapshot",
            "--out",
            Path.Combine(temp.Path, "out"),
            "--created-at",
            "2026-06",
            "--dry-run"
        ], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Safety classification: public-safe", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Tmp_legacy_baselines_path_is_git_ignored()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            ArgumentList = { "check-ignore", ".tmp/legacy-baselines/example" },
            WorkingDirectory = RepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;

        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
    }

    private static string SyntheticScanPath()
    {
        return Path.Combine(RepoRoot(), "samples", "synthetic-legacy-scan");
    }

    private static string RepoRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "src", "dotnet", "TraceMap.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
