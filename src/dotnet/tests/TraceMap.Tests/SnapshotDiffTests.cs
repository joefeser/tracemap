using System.Text.Json;
using Microsoft.Data.Sqlite;
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
        Assert.Empty(Assert.Single(result.Report.SourceDiffs).FileSpans);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "UnavailableEvidence" && gap.Section == "endpointDiffs");
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "UnavailableEvidence" && gap.Section == "graphDiffs");
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
    public async Task Snapshot_diff_single_projects_endpoint_and_surface_records()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        const string controller = "M:Sample.Controllers.OrdersController.Get";
        const string repository = "M:Sample.Infrastructure.OrderRepository.Get";
        SqliteIndexWriter.Write(beforeIndex, before, [
            RouteFact(before, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10, controller)
        ]);
        SqliteIndexWriter.Write(afterIndex, after, [
            RouteFact(after, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10, controller),
            QueryPatternFact(after, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "report"),
            Scope: "endpoints,surfaces"));

        Assert.Empty(result.Report.EndpointDiffs);
        var surface = Assert.Single(result.Report.SurfaceDiffs);
        Assert.Equal("surface", surface.EvidenceKind);
        Assert.Equal("added", surface.ChangeType);
        Assert.Contains(surface.RuleIds, ruleId => ruleId == "snapshot.diff.evidence.v1");
        Assert.Contains(surface.RuleIds, ruleId => ruleId == RuleIds.CSharpSyntaxQueryPattern);
        Assert.Contains(surface.SupportingFactIds, id => !string.IsNullOrWhiteSpace(id));
        Assert.NotEmpty(surface.FileSpans);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "UnavailableEvidence" && (gap.Section == "endpointDiffs" || gap.Section == "surfaceDiffs"));

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "snapshot-diff-report.json"));
        Assert.Contains("\"surfaceDiffs\"", json);
        Assert.DoesNotContain("select * from orders", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Snapshot_diff_single_same_sha_changed_endpoint_adds_identity_note()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [
            HttpClientFact(after, "GET", "/api/orders/{id}", "/api/orders/{}", "Clients/OrdersClient.cs", 10)
        ]);

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "report"),
            Scope: "endpoints"));

        var endpoint = Assert.Single(result.Report.EndpointDiffs);
        Assert.Equal(SnapshotDiffClassifications.ChangedEvidence, endpoint.Classification);
        Assert.Contains(endpoint.RuleIds, ruleId => ruleId == "snapshot.diff.identity.v1");
        Assert.Contains(endpoint.Notes, note => note.Contains("SameCommitShaDivergentEvidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Snapshot_diff_malformed_metadata_emits_gap_and_continues()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        var malformedFact = RouteFact(before, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10);
        SqliteIndexWriter.Write(beforeIndex, before, [malformedFact]);
        SqliteIndexWriter.Write(afterIndex, after, [
            RouteFact(after, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10)
        ]);
        CorruptMetadata(beforeIndex, malformedFact.FactId);

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "report"),
            Scope: "endpoints"));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "MalformedMetadataGap" && gap.RuleId == "snapshot.diff.schema.v1");
        Assert.Equal(SnapshotDiffClassifications.UnknownAnalysisGap, result.Report.Summary.RollupClassification);
    }

    [Fact]
    public async Task Snapshot_diff_rejects_trailing_space_endpoint_selector_without_crashing()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        SqliteIndexWriter.Write(beforeIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111"), []);
        SqliteIndexWriter.Write(afterIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222"), []);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "report"),
            Scope: "endpoints",
            Endpoint: "GET ")));

        Assert.Contains("--endpoint must be formatted", exception.Message);
    }

    [Fact]
    public async Task Snapshot_diff_source_only_scope_does_not_scan_fact_properties()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        var malformedFact = RouteFact(before, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10);
        SqliteIndexWriter.Write(beforeIndex, before, [malformedFact]);
        SqliteIndexWriter.Write(afterIndex, after, []);
        CorruptFactProperties(beforeIndex, malformedFact.FactId);

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "report"),
            Scope: "sources"));

        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "MalformedMetadataGap" && gap.SupportingFactIds.Contains(malformedFact.FactId));
    }

    [Fact]
    public async Task Snapshot_diff_duplicate_identity_gaps_include_snapshot_side()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, [
            RouteFact(before, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10),
            RouteFact(before, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 11)
        ]);
        SqliteIndexWriter.Write(afterIndex, after, [
            RouteFact(after, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10),
            RouteFact(after, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 11)
        ]);

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "report"),
            Scope: "endpoints"));

        var duplicateGaps = result.Report.Gaps.Where(gap => gap.GapKind == "DuplicateIdentity").ToArray();
        Assert.Equal(2, duplicateGaps.Length);
        Assert.Equal(2, duplicateGaps.Select(gap => gap.GapId).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(duplicateGaps, gap => gap.Metadata.Any(pair => pair.Key == "side" && pair.Value == "before"));
        Assert.Contains(duplicateGaps, gap => gap.Metadata.Any(pair => pair.Key == "side" && pair.Value == "after"));
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
    public async Task Snapshot_diff_validates_endpoint_selector_even_for_paths_scope()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        SqliteIndexWriter.Write(beforeIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111"), []);
        SqliteIndexWriter.Write(afterIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222"), []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeIndex], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterIndex], afterCombined, ["api"]));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "snapshot-diff",
            "--before", beforeCombined,
            "--after", afterCombined,
            "--out", Path.Combine(temp.Path, "report"),
            "--scope", "paths",
            "--include-paths",
            "--endpoint", "bad-selector"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("--endpoint must be formatted", error.ToString());
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
    public async Task Snapshot_diff_unknown_commit_sha_rolls_up_as_analysis_gap()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        SqliteIndexWriter.Write(beforeIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "unknown"), []);
        SqliteIndexWriter.Write(afterIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "unknown"), []);

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "report"),
            Scope: "sources"));

        Assert.Equal(SnapshotDiffClassifications.UnknownAnalysisGap, result.Report.Summary.RollupClassification);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "UnknownCommitSha" && gap.Classification == SnapshotDiffClassifications.UnknownAnalysisGap);
    }

    [Fact]
    public async Task Snapshot_diff_single_index_missing_language_metadata_does_not_fail_identity()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        SqliteIndexWriter.Write(beforeIndex, Manifest("api", "custom-scanner/1.0.0", commitSha: "1111111"), []);
        SqliteIndexWriter.Write(afterIndex, Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222"), []);

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "report"),
            Scope: "sources"));

        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "SourceIdentityConflict");
        Assert.Single(result.Report.SourceDiffs);
    }

    [Fact]
    public async Task Snapshot_diff_row_cap_emits_truncation_gap_and_partial_rollup()
    {
        using var temp = new TempDirectory();
        var beforeFirst = Path.Combine(temp.Path, "before-first.sqlite");
        var beforeSecond = Path.Combine(temp.Path, "before-second.sqlite");
        var afterFirst = Path.Combine(temp.Path, "after-first.sqlite");
        var afterSecond = Path.Combine(temp.Path, "after-second.sqlite");
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        SqliteIndexWriter.Write(beforeFirst, Manifest("first", ScannerVersions.TraceMap, commitSha: "1111111"), []);
        SqliteIndexWriter.Write(beforeSecond, Manifest("second", ScannerVersions.TraceMap, commitSha: "1111111"), []);
        SqliteIndexWriter.Write(afterFirst, Manifest("first", ScannerVersions.TraceMap, commitSha: "2222222"), []);
        SqliteIndexWriter.Write(afterSecond, Manifest("second", ScannerVersions.TraceMap, commitSha: "2222222"), []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeFirst, beforeSecond], beforeCombined, ["first", "second"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterFirst, afterSecond], afterCombined, ["first", "second"]));

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "report"),
            Scope: "sources",
            MaxDiffRows: 1));

        Assert.Single(result.Report.SourceDiffs);
        Assert.True(result.Report.Summary.Truncated);
        Assert.Equal(SnapshotDiffClassifications.TruncatedByLimit, result.Report.Summary.RollupClassification);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Section == "sourceDiffs");
    }

    [Fact]
    public async Task Snapshot_diff_combined_delegates_endpoint_surface_and_graph_sections()
    {
        using var temp = new TempDirectory();
        var beforeClient = Path.Combine(temp.Path, "before-client.sqlite");
        var beforeServer = Path.Combine(temp.Path, "before-server.sqlite");
        var afterClient = Path.Combine(temp.Path, "after-client.sqlite");
        var afterServer = Path.Combine(temp.Path, "after-server.sqlite");
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0", commitSha: "1111111");
        var serverBefore = Manifest("server", ScannerVersions.TraceMap, commitSha: "1111111");
        var serverAfter = Manifest("server", ScannerVersions.TraceMap, commitSha: "2222222");
        const string controller = "M:Sample.Controllers.OrdersController.Get";
        const string repository = "M:Sample.Infrastructure.OrderRepository.Get";
        SqliteIndexWriter.Write(beforeClient, client, [HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5)]);
        SqliteIndexWriter.Write(beforeServer, serverBefore, [RouteFact(serverBefore, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10, controller)]);
        SqliteIndexWriter.Write(afterClient, client, [HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5)]);
        SqliteIndexWriter.Write(afterServer, serverAfter, [
            RouteFact(serverAfter, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 20, controller),
            QueryPatternFact(serverAfter, repository, "Infrastructure/OrderRepository.cs", 31),
            CallFact(serverAfter, controller, repository, "Controllers/OrdersController.cs", 21)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeClient, beforeServer], beforeCombined, ["client", "server"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterClient, afterServer], afterCombined, ["client", "server"]));

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "report"),
            Scope: "endpoints,surfaces,graph",
            AllowIdentityMismatch: true));

        Assert.NotEmpty(result.Report.EndpointDiffs);
        Assert.NotEmpty(result.Report.SurfaceDiffs);
        Assert.NotEmpty(result.Report.GraphDiffs);
        Assert.Contains(result.Report.EndpointDiffs.SelectMany(row => row.RuleIds), ruleId => ruleId == "combined.diff.endpoint.v1");
        Assert.Contains(result.Report.SurfaceDiffs.SelectMany(row => row.RuleIds), ruleId => ruleId == "combined.diff.surface.v1");
        Assert.Contains(result.Report.GraphDiffs.SelectMany(row => row.RuleIds), ruleId => ruleId == "combined.diff.edge.v1");
        Assert.Contains(result.Report.GraphDiffs, row => row.SupportingEdgeIds.Count > 0 || row.SupportingFactIds.Count > 0);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "UnavailableEvidence" && (gap.Section == "endpointDiffs" || gap.Section == "surfaceDiffs" || gap.Section == "graphDiffs"));
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "snapshot-diff-report.json"));
        Assert.DoesNotContain("select * from orders", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Snapshot_diff_combined_coverage_scope_maps_without_source_rows()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysis", buildStatus: "Succeeded", commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", commitSha: "1111111");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeIndex], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterIndex], afterCombined, ["api"]));

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "report"),
            Scope: "coverage"));

        Assert.Empty(result.Report.SourceDiffs);
        Assert.Single(result.Report.CoverageDiffs);
        Assert.Equal(0, result.Report.Summary.SourceDiffCount);
        Assert.Equal(1, result.Report.Summary.CoverageDiffCount);
        Assert.Contains(result.Report.CoverageDiffs.SelectMany(row => row.RuleIds), ruleId => ruleId == "combined.diff.coverage.v1");
    }

    [Fact]
    public async Task Snapshot_diff_combined_source_selector_filters_unselected_metadata_gaps()
    {
        using var temp = new TempDirectory();
        var beforeApi = Path.Combine(temp.Path, "before-api.sqlite");
        var beforeWeb = Path.Combine(temp.Path, "before-web.sqlite");
        var afterApi = Path.Combine(temp.Path, "after-api.sqlite");
        var afterWeb = Path.Combine(temp.Path, "after-web.sqlite");
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        SqliteIndexWriter.Write(beforeApi, Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111"), []);
        SqliteIndexWriter.Write(beforeWeb, Manifest("web", ScannerVersions.TraceMap, commitSha: "1111111"), []);
        SqliteIndexWriter.Write(afterApi, Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222"), []);
        SqliteIndexWriter.Write(afterWeb, Manifest("web", ScannerVersions.TraceMap, commitSha: "2222222"), []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeApi, beforeWeb], beforeCombined, ["api", "web"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterApi, afterWeb], afterCombined, ["api", "web"]));
        CorruptCombinedSourceManifest(beforeCombined, "web");

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "report"),
            Scope: "sources",
            Source: "api"));

        Assert.DoesNotContain(result.Report.Gaps, gap => gap.GapKind == "MalformedMetadataGap" && gap.SourceLabel == "web");
        Assert.All(result.Report.Gaps.Where(gap => !string.IsNullOrWhiteSpace(gap.SourceLabel)), gap => Assert.Equal("api", gap.SourceLabel));
    }

    [Fact]
    public async Task Snapshot_diff_combined_gap_mapping_preserves_supporting_fact_ids()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, [
            QueryPatternFact(before, "M:Sample.Repository.First", "Repository/First.cs", 10),
            QueryPatternFact(before, "M:Sample.Repository.Second", "Repository/Second.cs", 20)
        ]);
        SqliteIndexWriter.Write(afterIndex, after, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeIndex], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterIndex], afterCombined, ["api"]));

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "report"),
            Scope: "surfaces"));

        var duplicateGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "DuplicateIdentity");
        Assert.Equal("combined.diff.identity.v1", duplicateGap.RuleId);
        Assert.NotEmpty(duplicateGap.SupportingFactIds);
    }

    [Fact]
    public async Task Snapshot_diff_combined_path_rows_preserve_provenance()
    {
        using var temp = new TempDirectory();
        var beforeClient = Path.Combine(temp.Path, "before-client.sqlite");
        var beforeServer = Path.Combine(temp.Path, "before-server.sqlite");
        var afterClient = Path.Combine(temp.Path, "after-client.sqlite");
        var afterServer = Path.Combine(temp.Path, "after-server.sqlite");
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0", commitSha: "1111111");
        var serverBefore = Manifest("server", ScannerVersions.TraceMap, commitSha: "1111111");
        var serverAfter = Manifest("server", ScannerVersions.TraceMap, commitSha: "2222222");
        const string controller = "M:Sample.Controllers.OrdersController.Get";
        const string repository = "M:Sample.Infrastructure.OrderRepository.Get";
        SqliteIndexWriter.Write(beforeClient, client, [HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5)]);
        SqliteIndexWriter.Write(beforeServer, serverBefore, [RouteFact(serverBefore, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10, controller)]);
        SqliteIndexWriter.Write(afterClient, client, [HttpClientFact(client, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 5)]);
        SqliteIndexWriter.Write(afterServer, serverAfter, [
            RouteFact(serverAfter, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 20, controller),
            QueryPatternFact(serverAfter, repository, "Infrastructure/OrderRepository.cs", 31),
            CallFact(serverAfter, controller, repository, "Controllers/OrdersController.cs", 21)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeClient, beforeServer], beforeCombined, ["client", "server"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterClient, afterServer], afterCombined, ["client", "server"]));

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "report"),
            Scope: "paths",
            IncludePaths: true,
            AllowIdentityMismatch: true));

        var pathDiff = Assert.Single(result.Report.PathDiffs);
        Assert.NotEmpty(pathDiff.FileSpans);
        Assert.NotEmpty(pathDiff.SupportingFactIds);
        Assert.NotEmpty(pathDiff.SupportingEdgeIds);
        Assert.NotNull(pathDiff.After);
        var evidence = pathDiff.After!;
        Assert.False(string.IsNullOrWhiteSpace(evidence.CommitSha));
        Assert.False(string.IsNullOrWhiteSpace(evidence.ScanId));
        Assert.NotEmpty(evidence.FileSpans);
    }

    [Fact]
    public async Task Snapshot_diff_combined_gaps_scope_keeps_gap_diff_availability_gap()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeIndex], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterIndex], afterCombined, ["api"]));

        var result = await SnapshotDiffReporter.WriteAsync(new SnapshotDiffOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "report"),
            Scope: "gaps"));

        Assert.Empty(result.Report.GapDiffs);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "UnavailableEvidence" && gap.Section == "gapDiffs");
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

    private static CodeFact HttpClientFact(ScanManifest manifest, string method, string template, string key, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: $"{method} {template}",
            contractElement: method,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethod"] = method,
                ["methodName"] = method,
                ["normalizedPathTemplate"] = template,
                ["normalizedPathKey"] = key,
                ["urlKind"] = "template"
            });
    }

    private static CodeFact RouteFact(ScanManifest manifest, string method, string template, string key, string file, int line, string? methodSymbol = null)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: methodSymbol,
            targetSymbol: methodSymbol ?? $"{method} {template}",
            contractElement: template,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethods"] = method,
                ["methodName"] = method,
                ["normalizedPathTemplate"] = template,
                ["normalizedPathKey"] = key,
                ["routeTemplates"] = template
            });
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string sourceSymbol, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: "orders",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationName"] = "SELECT",
                ["tableName"] = "orders",
                ["columnNames"] = "id;status",
                ["queryShapeHash"] = "shape123",
                ["sqlSourceKind"] = "literal-string",
                ["rawSql"] = "select * from orders"
            });
    }

    private static CodeFact CallFact(ScanManifest manifest, string caller, string callee, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.CallEdge,
            RuleIds.CSharpSemanticCallGraph,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: caller,
            targetSymbol: callee,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["callerSymbol"] = caller,
                ["calleeSymbol"] = callee
            });
    }

    private static void CorruptMetadata(string indexPath, string factId)
    {
        using var connection = new SqliteConnection($"Data Source={indexPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            update scan_manifest
            set manifest_json = '{'
            where scan_id = (select scan_id from scan_manifest limit 1);

            update facts
            set properties_json = '{'
            where fact_id = $fact_id;
            """;
        command.Parameters.AddWithValue("$fact_id", factId);
        command.ExecuteNonQuery();
    }

    private static void CorruptFactProperties(string indexPath, string factId)
    {
        using var connection = new SqliteConnection($"Data Source={indexPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            update facts
            set properties_json = '{'
            where fact_id = $fact_id;
            """;
        command.Parameters.AddWithValue("$fact_id", factId);
        command.ExecuteNonQuery();
    }

    private static void CorruptCombinedSourceManifest(string indexPath, string label)
    {
        using var connection = new SqliteConnection($"Data Source={indexPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            update index_sources
            set manifest_json = '{'
            where label = $label;
            """;
        command.Parameters.AddWithValue("$label", label);
        command.ExecuteNonQuery();
    }
}
