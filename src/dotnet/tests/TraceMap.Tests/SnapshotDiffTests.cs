using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SnapshotDiffTests
{
    [Fact]
    public async Task Snapshot_diff_single_writes_safe_deterministic_shell_report()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var outDir = Path.Combine(temp.Path, "snapshot");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, []);

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(beforeIndex, afterIndex, outDir));

        Assert.True(File.Exists(Path.Combine(outDir, "snapshot-diff-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "snapshot-diff-report.json")));
        Assert.Equal("snapshot-diff", result.Report.ReportType);
        Assert.Equal("single", Assert.Single(result.Report.BeforeSnapshot.Sources).SourceLabel);
        Assert.Single(result.Report.SourceDiffs);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "UnavailableEvidence" && gap.Section == "endpointDiffs");
        Assert.All(result.Report.SourceDiffs, row => Assert.All(row.RuleIds, ruleId => Assert.False(string.IsNullOrWhiteSpace(ruleId))));
        Assert.All(result.Report.Gaps, gap => Assert.False(string.IsNullOrWhiteSpace(gap.RuleId)));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "snapshot-diff-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "snapshot-diff-report.json"));
        Assert.Contains("TraceMap Snapshot Diff Report", markdown);
        Assert.Contains("\"reportType\": \"snapshot-diff\"", json);
        Assert.DoesNotContain("https://example.invalid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);

        var secondOutDir = Path.Combine(temp.Path, "snapshot-second");
        await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(beforeIndex, afterIndex, secondOutDir));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "snapshot-diff-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "snapshot-diff-report.json")));
    }

    [Fact]
    public async Task Snapshot_diff_rejects_mixed_single_and_combined_without_writing_output()
    {
        using var temp = new TempDirectory();
        var singleIndex = Path.Combine(temp.Path, "single.sqlite");
        var combinedIndex = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "snapshot");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(singleIndex, manifest, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([singleIndex], combinedIndex, ["api"]));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "snapshot-diff",
            "--before", singleIndex,
            "--after", combinedIndex,
            "--out", outDir
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("mixed single and combined indexes are not supported", error.ToString());
        Assert.DoesNotContain(temp.Path, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(outDir, "snapshot-diff-report.md")));
        Assert.False(File.Exists(Path.Combine(outDir, "snapshot-diff-report.json")));
    }

    [Fact]
    public async Task Snapshot_diff_output_path_matrix_matches_report_helpers()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        SqliteIndexWriter.Write(beforeIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111"), []);
        SqliteIndexWriter.Write(afterIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222"), []);

        var dir = Path.Combine(temp.Path, "dir");
        await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(beforeIndex, afterIndex, dir));
        Assert.True(File.Exists(Path.Combine(dir, "snapshot-diff-report.md")));
        Assert.True(File.Exists(Path.Combine(dir, "snapshot-diff-report.json")));

        var jsonFile = Path.Combine(temp.Path, "snapshot.json");
        await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(beforeIndex, afterIndex, jsonFile, Format: "json"));
        Assert.True(File.Exists(jsonFile));
        Assert.Contains("\"reportType\": \"snapshot-diff\"", await File.ReadAllTextAsync(jsonFile));

        var mdFile = Path.Combine(temp.Path, "snapshot.md");
        await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(beforeIndex, afterIndex, mdFile));
        Assert.True(File.Exists(mdFile));
        Assert.Contains("TraceMap Snapshot Diff Report", await File.ReadAllTextAsync(mdFile));

        var extensionless = Path.Combine(temp.Path, "extensionless");
        await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(beforeIndex, afterIndex, extensionless));
        Assert.True(File.Exists(Path.Combine(extensionless, "snapshot-diff-report.md")));
        Assert.True(File.Exists(Path.Combine(extensionless, "snapshot-diff-report.json")));
    }

    [Fact]
    public async Task Snapshot_diff_rejects_unknown_scope_and_single_include_paths()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        SqliteIndexWriter.Write(beforeIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111"), []);
        SqliteIndexWriter.Write(afterIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222"), []);

        using var unknownOutput = new StringWriter();
        using var unknownError = new StringWriter();
        var unknownExit = await TraceMapCommand.RunAsync([
            "snapshot-diff",
            "--before", beforeIndex,
            "--after", afterIndex,
            "--out", Path.Combine(temp.Path, "bad-scope"),
            "--scope", "widgets"
        ], unknownOutput, unknownError);
        Assert.Equal(1, unknownExit);
        Assert.Contains("unsupported value `widgets`", unknownError.ToString());

        using var pathsOutput = new StringWriter();
        using var pathsError = new StringWriter();
        var pathsExit = await TraceMapCommand.RunAsync([
            "snapshot-diff",
            "--before", beforeIndex,
            "--after", afterIndex,
            "--out", Path.Combine(temp.Path, "paths"),
            "--include-paths"
        ], pathsOutput, pathsError);
        Assert.Equal(1, pathsExit);
        Assert.Contains("--include-paths requires combined indexes", pathsError.ToString());
    }

    [Fact]
    public async Task Snapshot_diff_identity_conflict_fails_unless_explicitly_allowed()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api-before", ScannerVersions.TraceMap, remoteUrl: "https://example.invalid/before.git", commitSha: "1111111");
        var after = Manifest("api-after", ScannerVersions.TraceMap, remoteUrl: "https://example.invalid/after.git", commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, []);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "snapshot-diff",
            "--before", beforeIndex,
            "--after", afterIndex,
            "--out", Path.Combine(temp.Path, "blocked")
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("source identity conflict", error.ToString());
        Assert.DoesNotContain("https://example.invalid", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, error.ToString(), StringComparison.OrdinalIgnoreCase);

        var allowed = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "allowed"),
            AllowIdentityMismatch: true));
        Assert.Contains(allowed.Report.Gaps, gap => gap.GapKind == "SourceIdentityConflict");
        Assert.Equal(SnapshotDiffClassifications.UnknownAnalysisGap, allowed.Report.Summary.RollupClassification);
    }

    [Fact]
    public async Task Snapshot_diff_cli_exit_code_is_opt_in()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        SqliteIndexWriter.Write(beforeIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111"), []);
        SqliteIndexWriter.Write(afterIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222"), []);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "snapshot-diff",
            "--before", beforeIndex,
            "--after", afterIndex,
            "--out", Path.Combine(temp.Path, "report"),
            "--scope", "sources",
            "--exit-code"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("TraceMap snapshot-diff completed:", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    private static ScanManifest Manifest(
        string repo,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded",
        string commitSha = "abc1234567890",
        string? remoteUrl = "https://example.invalid/repo.git")
    {
        return new ScanManifest(
            $"scan-{repo}-{commitSha}",
            repo,
            remoteUrl,
            "main",
            commitSha,
            scannerVersion,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            analysisLevel,
            buildStatus,
            [],
            [],
            [],
            [],
            ".",
            FactFactory.Hash(repo, 32),
            FactFactory.Hash($"{repo}-git-root", 32));
    }
}
