using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Reflection;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Reporting.ReviewPriority;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ReleaseReviewTests
{
    [Fact]
    public async Task Release_review_combined_writes_safe_deterministic_packet_with_contract_context()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var beforeApi = Path.Combine(temp.Path, "before-api.sqlite");
        var afterApi = Path.Combine(temp.Path, "after-api.sqlite");
        var beforeClient = Path.Combine(temp.Path, "before-client.sqlite");
        var afterClient = Path.Combine(temp.Path, "after-client.sqlite");
        var outDir = Path.Combine(temp.Path, "release");
        var apiBefore = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var apiAfter = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        var clientBefore = Manifest("client", "tracemap-typescript/0.1.0", commitSha: "1111111");
        var clientAfter = Manifest("client", "tracemap-typescript/0.1.0", commitSha: "2222222");
        var deltaPath = WriteV2Delta(temp.Path, """
            {
              "id": "chg-package-json",
              "kind": "package",
              "changeType": "removed",
              "reference": {
                "ecosystem": "nuget",
                "packageName": "Newtonsoft.Json"
              }
            }
            """);

        SqliteIndexWriter.Write(beforeApi, apiBefore, [
            RouteFact(apiBefore, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);
        SqliteIndexWriter.Write(afterApi, apiAfter, [
            RouteFact(apiAfter, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10),
            RouteFact(apiAfter, "POST", "/api/orders/archive", "/api/orders/archive", "Controllers/OrdersController.cs", 20),
            QueryPatternFact(apiAfter, "Infrastructure/OrderRepository.cs", 30),
            PackageFact(apiAfter, "Api.csproj", 5)
        ]);
        SqliteIndexWriter.Write(beforeClient, clientBefore, [
            HttpClientFact(clientBefore, "GET", "/api/orders", "/api/orders", "src/orders.ts", 8)
        ]);
        SqliteIndexWriter.Write(afterClient, clientAfter, [
            HttpClientFact(clientAfter, "GET", "/api/orders", "/api/orders", "src/orders.ts", 8)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeApi, beforeClient], beforeCombined, ["api", "client"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterApi, afterClient], afterCombined, ["api", "client"]));

        var result = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            outDir,
            Format: "json",
            ContractDeltaPath: deltaPath));

        Assert.True(File.Exists(Path.Combine(outDir, "release-review.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "release-review.json")));
        Assert.Equal("ReleaseReviewCombinedV1", result.Report.Mode);
        Assert.Equal(ReleaseReviewClassifications.ActionableStaticEvidence, result.Report.Summary.RollupClassification);
        Assert.NotEmpty(result.Report.TopChangedSurfaces.Findings);
        Assert.NotEmpty(result.Report.ContractImpact.Findings);
        Assert.NotEmpty(result.Report.PackageImpact.Findings);
        Assert.DoesNotContain(
            result.Report.PackageImpact.Findings.Select(finding => finding.FindingId),
            id => result.Report.TopChangedSurfaces.Findings.Any(finding => finding.FindingId == id));
        Assert.All(result.Report.PackageImpact.Findings, finding => Assert.Equal("packageImpact", finding.Section));
        Assert.Equal(ReleaseReviewStatuses.NotRequested, result.Report.PathContext.Status);
        Assert.Equal(ReleaseReviewStatuses.NotRequested, result.Report.ReverseContext.Status);
        Assert.All(result.Report.Gaps, gap => Assert.False(string.IsNullOrWhiteSpace(gap.RuleId)));
        Assert.All(result.Report.ReviewerChecklist, item => Assert.False(string.IsNullOrWhiteSpace(item.RuleId)));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "release-review.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "release-review.json"));
        Assert.Contains("TraceMap Release Review Report", markdown);
        Assert.Contains("\"reportType\": \"release-review\"", json);
        Assert.DoesNotContain("\"reviewPriority\"", json);
        Assert.DoesNotContain("## Review Priority", markdown);
        Assert.DoesNotContain("https://example.invalid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select * from orders", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);

        var secondOutDir = Path.Combine(temp.Path, "release-second");
        await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            secondOutDir,
            Format: "json",
            ContractDeltaPath: deltaPath));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "release-review.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "release-review.json")));
    }

    [Fact]
    public async Task Release_review_include_priority_writes_scored_markdown_and_json_sidecar()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var outDir = Path.Combine(temp.Path, "release");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [
            RouteFact(after, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10),
            QueryPatternFact(after, "/private/tmp/secret/OrderRepository.cs", 30)
        ]);
        var beforeHash = Sha256(beforeIndex);
        var afterHash = Sha256(afterIndex);

        var result = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeIndex,
            afterIndex,
            outDir,
            IncludePriority: true));

        Assert.NotNull(result.ReviewPriority);
        Assert.NotNull(result.ReviewPriorityRows);
        Assert.Equal(ReviewPriorityStatuses.Available, result.ReviewPriority!.Status);
        Assert.Equal(ReviewPriorityRules.ModelVersion, result.ReviewPriority.ModelVersion);
        Assert.Null(result.ReviewPriority.PriorityScore);
        Assert.Contains(result.ReviewPriority.AttentionLevel, new[]
        {
            ReviewPriorityAttentionLevels.HighAttention,
            ReviewPriorityAttentionLevels.ModerateAttention,
            ReviewPriorityAttentionLevels.Unknown
        });
        Assert.NotEmpty(result.ReviewPriorityRows!);
        Assert.All(result.ReviewPriorityRows!, row =>
        {
            Assert.Contains(row.SeverityHint, new[]
            {
                ReviewPrioritySeverityHints.CriticalReview,
                ReviewPrioritySeverityHints.HighReview,
                ReviewPrioritySeverityHints.MediumReview,
                ReviewPrioritySeverityHints.LowReview,
                ReviewPrioritySeverityHints.Info,
                ReviewPrioritySeverityHints.Unknown
            });
            Assert.Null(row.PriorityScore);
            Assert.NotEmpty(row.Components);
            Assert.All(row.Components, component =>
            {
                Assert.StartsWith("review.priority.", component.RuleId, StringComparison.Ordinal);
                Assert.Null(component.ComponentValue);
                Assert.NotEmpty(component.Limitations);
                Assert.True(component.SourceEvidence.Count > 0 || component.Metadata.Count > 0);
            });
        });
        Assert.Contains(result.ReviewPriorityRows!, row => row.Components.Any(component => component.ComponentKind == "coverage_gap" && component.Direction == "cap"));
        Assert.DoesNotContain(result.ReviewPriorityRows!, row => row.SeverityHint == ReviewPrioritySeverityHints.CriticalReview);

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "release-review.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "release-review.json"));
        Assert.Contains("## Review Priority", markdown);
        Assert.Contains("\"reviewPriority\"", json);
        Assert.Contains("\"reviewPriorityRows\"", json);
        Assert.Contains("\"priorityScore\": null", json);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select * from orders", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select * from orders", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeHash, Sha256(beforeIndex));
        Assert.Equal(afterHash, Sha256(afterIndex));

        var root = JsonNode.Parse(json)!.AsObject();
        Assert.Equal("review-priority.v1", root["reviewPriority"]!["modelVersion"]!.GetValue<string>());
        Assert.Equal(ReviewPriorityStatuses.Available, root["reviewPriority"]!["status"]!.GetValue<string>());
        Assert.True(root["reviewPriorityRows"]!.AsArray().Count > 0);
    }

    [Fact]
    public async Task Release_review_include_priority_does_not_change_later_opt_out_output()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [
            RouteFact(after, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);

        var firstOptOutDir = Path.Combine(temp.Path, "first-opt-out");
        await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeIndex,
            afterIndex,
            firstOptOutDir));
        var firstMarkdown = await File.ReadAllTextAsync(Path.Combine(firstOptOutDir, "release-review.md"));
        var firstJson = await File.ReadAllTextAsync(Path.Combine(firstOptOutDir, "release-review.json"));

        await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "scored"),
            IncludePriority: true));

        var secondOptOutDir = Path.Combine(temp.Path, "second-opt-out");
        await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeIndex,
            afterIndex,
            secondOptOutDir,
            IncludePriority: false));
        var secondMarkdown = await File.ReadAllTextAsync(Path.Combine(secondOptOutDir, "release-review.md"));
        var secondJson = await File.ReadAllTextAsync(Path.Combine(secondOptOutDir, "release-review.json"));

        Assert.Equal(firstMarkdown, secondMarkdown);
        Assert.Equal(firstJson, secondJson);
        Assert.DoesNotContain("## Review Priority", secondMarkdown);
        Assert.DoesNotContain("\"reviewPriority\"", secondJson);
        Assert.DoesNotContain("\"reviewPriorityRows\"", secondJson);
    }

    [Fact]
    public async Task Release_review_priority_output_is_byte_stable_for_identical_inputs()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [
            RouteFact(after, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);

        var firstDir = Path.Combine(temp.Path, "first");
        var secondDir = Path.Combine(temp.Path, "second");
        await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(beforeIndex, afterIndex, firstDir, IncludePriority: true));
        await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(beforeIndex, afterIndex, secondDir, IncludePriority: true));

        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(firstDir, "release-review.md")),
            await File.ReadAllTextAsync(Path.Combine(secondDir, "release-review.md")));
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(firstDir, "release-review.json")),
            await File.ReadAllTextAsync(Path.Combine(secondDir, "release-review.json")));
    }

    [Fact]
    public void Release_review_priority_cap_semantics_do_not_upgrade_low_or_info_rows()
    {
        var method = typeof(ReleaseReviewPriorityScorer).GetMethod("SeverityFromComponents", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var cap = PriorityComponent("review_tier_evidence", "cap", ReviewPriorityRules.Downgrade, EvidenceTiers.Tier3SyntaxOrTextual);

        var strongCapped = (string)method!.Invoke(null, [CombinedDependencyDiffClassifications.Added, EvidenceTiers.Tier2Structural, new[] { cap }])!;
        var infoCapped = (string)method.Invoke(null, [ReleaseReviewClassifications.NoActionableEvidence, EvidenceTiers.Tier3SyntaxOrTextual, new[] { cap }])!;

        Assert.Equal(ReviewPrioritySeverityHints.MediumReview, strongCapped);
        Assert.Equal(ReviewPrioritySeverityHints.Info, infoCapped);
    }

    [Fact]
    public void Release_review_priority_markdown_escapes_scoring_limitations()
    {
        var report = MinimalReleaseReviewDocument();
        var unsafeLimitation = "pipe | bracket [x] paren (y) tick `z` angle <q>\nnext";
        var row = new ReviewPriorityRow(
            "row:unsafe",
            "finding",
            "topChangedSurfaces",
            "api",
            CombinedDependencyDiffClassifications.Added,
            "combined.diff.surface.v1",
            EvidenceTiers.Tier2Structural,
            ReviewPrioritySeverityHints.HighReview,
            null,
            true,
            [PriorityComponent("static_change_evidence", "increase", ReviewPriorityRules.Component, EvidenceTiers.Tier2Structural)],
            ["finding:unsafe"],
            [unsafeLimitation]);
        var summary = new ReviewPrioritySummary(
            ReviewPriorityStatuses.Available,
            ReviewPriorityRules.ModelVersion,
            ReviewPriorityAttentionLevels.HighAttention,
            null,
            true,
            ["topChangedSurfaces"],
            [],
            [PriorityComponent("report_aggregation", "increase", ReviewPriorityRules.Aggregate, EvidenceTiers.Tier2Structural)],
            [unsafeLimitation]);
        var priority = new ReleaseReviewPriorityResult(summary, [row]);
        var method = typeof(ReleaseReviewReporter).GetMethod("RenderMarkdown", BindingFlags.NonPublic | BindingFlags.Static, [typeof(ReleaseReviewDocument), typeof(ReleaseReviewPriorityResult)]);

        var markdown = (string)method!.Invoke(null, [report, priority])!;

        Assert.Contains("pipe \\| bracket \\[x\\] paren \\(y\\) tick \\`z\\` angle \\<q\\> next", markdown);
    }

    [Fact]
    public async Task Release_review_priority_selector_no_match_is_unknown_under_reduced_coverage()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var beforeApi = Path.Combine(temp.Path, "before-api.sqlite");
        var afterApi = Path.Combine(temp.Path, "after-api.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", commitSha: "2222222");
        SqliteIndexWriter.Write(beforeApi, before, []);
        SqliteIndexWriter.Write(afterApi, after, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeApi], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterApi], afterCombined, ["api"]));

        var result = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "release"),
            Scope: "surfaces",
            Endpoint: "GET /missing",
            IncludePriority: true));

        Assert.Contains(result.ReviewPriorityRows!, row => row.Components.Any(component => component.ComponentKind == "selector_no_match_uncertain" && component.Direction == "unknown"));
        Assert.DoesNotContain(result.ReviewPriorityRows!, row => row.Components.Any(component => component.ComponentKind == "selector_no_match_credible"));
    }

    [Fact]
    public void Release_review_priority_vocabularies_and_model_version_are_closed()
    {
        Assert.Equal("review-priority.v1", ReviewPriorityRules.ModelVersion);
        Assert.Equal(
            new[] { "critical_review", "high_review", "medium_review", "low_review", "info", "unknown" },
            PrioritySeverityVocabulary());
        Assert.Equal(
            new[] { "highest_attention", "high_attention", "moderate_attention", "low_attention", "informational", "unknown" },
            PriorityAttentionVocabulary());
        Assert.Equal(
            new[] { "available", "not_requested", "unavailable", "deferred", "truncated" },
            PriorityStatusVocabulary());
    }

    [Fact]
    public async Task Release_review_priority_marks_reduced_coverage_and_truncation_as_incomplete()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var beforeApi = Path.Combine(temp.Path, "before-api.sqlite");
        var afterApi = Path.Combine(temp.Path, "after-api.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", commitSha: "2222222");
        SqliteIndexWriter.Write(beforeApi, before, []);
        SqliteIndexWriter.Write(afterApi, after, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeApi], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterApi], afterCombined, ["api"]));

        var result = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "release"),
            Scope: "surfaces,api-dto,sql-schema,packages",
            MaxGaps: 50,
            MaxChecklistItems: 1,
            IncludePriority: true));

        Assert.NotNull(result.ReviewPriority);
        Assert.Equal(ReviewPriorityStatuses.Truncated, result.ReviewPriority!.Status);
        Assert.False(result.ReviewPriority.Complete);
        Assert.Equal(ReviewPriorityAttentionLevels.Unknown, result.ReviewPriority.AttentionLevel);
        Assert.Contains("sourceCoverage", result.ReviewPriority.LimitedSections);
        Assert.Contains(result.ReviewPriorityRows!, row => row.Components.Any(component => component.RuleId == ReviewPriorityRules.Truncation));
        Assert.Contains(result.ReviewPriorityRows!, row => row.Components.Any(component => component.RuleId == ReviewPriorityRules.Coverage));
        Assert.Contains(result.ReviewPriorityRows!, row => row.SeverityHint == ReviewPrioritySeverityHints.Unknown);
    }

    [Fact]
    public async Task Release_review_cli_include_priority_uses_boolean_flag()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var outDir = Path.Combine(temp.Path, "release");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [
            RouteFact(after, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "release-review",
            "--before", beforeIndex,
            "--after", afterIndex,
            "--out", outDir,
            "--include-priority"
        ], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("TraceMap release-review completed:", output.ToString());
        Assert.Contains("\"reviewPriority\"", await File.ReadAllTextAsync(Path.Combine(outDir, "release-review.json")));
        Assert.Contains("## Review Priority", await File.ReadAllTextAsync(Path.Combine(outDir, "release-review.md")));
    }

    [Fact]
    public void Release_review_priority_rule_ids_are_documented()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var ruleId in new[]
                 {
                     ReviewPriorityRules.Component,
                     ReviewPriorityRules.Aggregate,
                     ReviewPriorityRules.Downgrade,
                     ReviewPriorityRules.Coverage,
                     ReviewPriorityRules.Identity,
                     ReviewPriorityRules.Commit,
                     ReviewPriorityRules.Schema,
                     ReviewPriorityRules.Truncation,
                     ReviewPriorityRules.Workflow,
                     ReviewPriorityRules.Selector
                 })
        {
            Assert.Contains($"id: {ruleId}", catalog, StringComparison.Ordinal);
            var start = catalog.IndexOf($"id: {ruleId}", StringComparison.Ordinal);
            var block = catalog[start..Math.Min(catalog.Length, start + 900)];
            Assert.Contains("limitations:", block, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Release_review_single_index_renders_requested_path_and_reverse_context_unavailable()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var outDir = Path.Combine(temp.Path, "release");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [
            RouteFact(after, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "release-review",
            "--before", beforeIndex,
            "--after", afterIndex,
            "--out", outDir,
            "--include-paths",
            "--include-reverse"
        ], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("TraceMap release-review completed:", output.ToString());
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "release-review.json"));
        var document = JsonSerializer.Deserialize<ReleaseReviewDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(document);
        Assert.Equal("ReleaseReviewSingleV1", document!.Mode);
        Assert.Equal(ReleaseReviewClassifications.ReviewRecommended, document.Summary.RollupClassification);
        Assert.Equal(0, document.Summary.ActionableFindingCount);
        Assert.Contains(document.TopChangedSurfaces.Findings, finding =>
            finding.Classification == CombinedDependencyDiffClassifications.Added
            && finding.EvidenceTier == EvidenceTiers.Tier2Structural
            && finding.Metadata.Any(pair => pair.Key == "coverageRelative" && pair.Value == "true"));
        Assert.Equal(ReleaseReviewStatuses.Unavailable, document.PathContext.Status);
        Assert.Equal(ReleaseReviewStatuses.Unavailable, document.ReverseContext.Status);
        Assert.Contains(document.Gaps, gap => gap.GapKind == "UnsupportedMode" && gap.Section == "pathContext");
        Assert.Contains(document.Gaps, gap => gap.GapKind == "UnsupportedMode" && gap.Section == "reverseContext");
    }

    [Fact]
    public async Task Release_review_single_index_applies_endpoint_and_surface_selectors()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [
            RouteFact(after, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10),
            RouteFact(after, "POST", "/api/orders/archive", "/api/orders/archive", "Controllers/OrdersController.cs", 20),
            QueryPatternFact(after, "Infrastructure/OrderRepository.cs", 30)
        ]);

        var endpoint = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "endpoint"),
            Endpoint: "GET /api/orders"));
        var endpointFinding = Assert.Single(endpoint.Report.TopChangedSurfaces.Findings);
        Assert.Equal("GET /api/orders", endpointFinding.DisplayName);

        var surface = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "surface"),
            Surface: "sql-query",
            SurfaceName: "orders"));
        var surfaceFinding = Assert.Single(surface.Report.TopChangedSurfaces.Findings);
        Assert.Equal("sql-query", surfaceFinding.Metadata.First(pair => pair.Key == "surfaceKind").Value);
    }

    [Fact]
    public async Task Release_review_single_index_identical_facts_emit_no_changed_surface_evidence()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, [
            RouteFact(before, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);
        SqliteIndexWriter.Write(afterIndex, after, [
            RouteFact(after, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10)
        ]);

        var result = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "release")));

        Assert.Empty(result.Report.TopChangedSurfaces.Findings);
        Assert.NotEqual(ReleaseReviewClassifications.ActionableStaticEvidence, result.Report.Summary.RollupClassification);
    }

    [Fact]
    public async Task Release_review_contract_scope_does_not_fall_back_to_surface_impact()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var beforeApi = Path.Combine(temp.Path, "before-api.sqlite");
        var afterApi = Path.Combine(temp.Path, "after-api.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeApi, before, []);
        SqliteIndexWriter.Write(afterApi, after, [
            RouteFact(after, "POST", "/api/orders/archive", "/api/orders/archive", "Controllers/OrdersController.cs", 20)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeApi], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterApi], afterCombined, ["api"]));

        var result = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "release"),
            Scope: "contracts"));

        Assert.Equal(ReleaseReviewStatuses.NotRequested, result.Report.TopChangedSurfaces.Status);
        Assert.Empty(result.Report.TopChangedSurfaces.Findings);
        Assert.Equal(ReleaseReviewStatuses.NotRequested, result.Report.ApiDtoChanges.Status);
        Assert.Empty(result.Report.ApiDtoChanges.Gaps);
        Assert.DoesNotContain(result.Report.Gaps, gap => gap.Section == "apiDtoChanges");
    }

    [Fact]
    public async Task Release_review_source_count_uses_union_of_before_after_sources()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var beforeApi = Path.Combine(temp.Path, "before-api.sqlite");
        var afterApi = Path.Combine(temp.Path, "after-api.sqlite");
        var afterClient = Path.Combine(temp.Path, "after-client.sqlite");
        var apiBefore = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var apiAfter = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        var clientAfter = Manifest("client", "tracemap-typescript/0.1.0", commitSha: "2222222");
        SqliteIndexWriter.Write(beforeApi, apiBefore, []);
        SqliteIndexWriter.Write(afterApi, apiAfter, []);
        SqliteIndexWriter.Write(afterClient, clientAfter, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeApi], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterApi, afterClient], afterCombined, ["api", "client"]));

        var result = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "release")));

        Assert.Equal(2, result.Report.Summary.SourceCount);
        Assert.Equal(result.Report.SourceCoverage.Count, result.Report.Summary.SourceCount);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "SourceOnlyOnOneSide" && gap.SourceLabel == "client");
    }

    [Fact]
    public async Task Release_review_gap_and_checklist_caps_emit_truncation_gaps()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var beforeApi = Path.Combine(temp.Path, "before-api.sqlite");
        var afterApi = Path.Combine(temp.Path, "after-api.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", commitSha: "2222222");
        SqliteIndexWriter.Write(beforeApi, before, []);
        SqliteIndexWriter.Write(afterApi, after, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeApi], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterApi], afterCombined, ["api"]));

        var gapCapped = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "gap-capped"),
            Scope: "surfaces,api-dto,sql-schema,packages",
            MaxGaps: 1,
            MaxChecklistItems: 50));
        Assert.Single(gapCapped.Report.Gaps);
        Assert.Contains(gapCapped.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Section == "gaps");
        Assert.Empty(gapCapped.Report.TopChangedSurfaces.Gaps);
        Assert.Empty(gapCapped.Report.ApiDtoChanges.Gaps);
        Assert.Empty(gapCapped.Report.SqlSchemaImpact.Gaps);
        Assert.Empty(gapCapped.Report.PackageImpact.Gaps);

        var checklistCapped = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "checklist-capped"),
            Scope: "surfaces,api-dto,sql-schema,packages",
            MaxGaps: 50,
            MaxChecklistItems: 1));
        Assert.Contains(checklistCapped.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Section == "checklist");
    }

    [Fact]
    public async Task Release_review_reverse_findings_include_after_commit_sha()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var beforeServer = Path.Combine(temp.Path, "before-server.sqlite");
        var afterServer = Path.Combine(temp.Path, "after-server.sqlite");
        var before = Manifest("server", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("server", ScannerVersions.TraceMap, commitSha: "2222222");
        var controller = "Server.OrdersController.Get(System.Int32)";
        var repository = "Server.OrderRepository.Query(System.Int32)";
        SqliteIndexWriter.Write(beforeServer, before, []);
        SqliteIndexWriter.Write(afterServer, after, [
            RouteFact(after, "GET", "/api/orders/{id}", "/api/orders/{}", controller, "Controllers/OrdersController.cs", 10),
            CallFact(after, controller, repository, "Controllers/OrdersController.cs", 14),
            QueryPatternFact(after, repository, "Infrastructure/OrderRepository.cs", 31)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeServer], beforeCombined, ["server"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterServer], afterCombined, ["server"]));

        var result = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "release"),
            IncludeReverse: true,
            Surface: "sql-query",
            SurfaceName: "orders"));

        Assert.NotEmpty(result.Report.ReverseContext.Findings);
        Assert.All(result.Report.ReverseContext.Findings, finding => Assert.Equal("2222222", finding.CommitSha));
    }

    [Fact]
    public async Task Release_review_reduced_coverage_rolls_up_as_partial_analysis()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var beforeApi = Path.Combine(temp.Path, "before-api.sqlite");
        var afterApi = Path.Combine(temp.Path, "after-api.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", commitSha: "2222222");
        SqliteIndexWriter.Write(beforeApi, before, []);
        SqliteIndexWriter.Write(afterApi, after, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeApi], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterApi], afterCombined, ["api"]));

        var result = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "release"),
            Scope: "surfaces",
            Endpoint: "GET /missing"));

        Assert.Equal(ReleaseReviewClassifications.PartialAnalysis, result.Report.Summary.RollupClassification);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == ReleaseReviewClassifications.PartialAnalysis && gap.GapKind == "ReducedCoverage");
    }

    [Fact]
    public void Release_review_rollup_precedence_prefers_partial_analysis_over_selector_no_match()
    {
        var method = typeof(ReleaseReviewReporter).GetMethod("SelectRollup", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var gaps = new[]
        {
            GapForTest("selector", ReleaseReviewClassifications.SelectorNoMatch),
            GapForTest("coverage", ReleaseReviewClassifications.PartialAnalysis)
        };

        var rollup = method!.Invoke(null, [gaps, Array.Empty<ReleaseReviewFinding>(), false]);

        Assert.Equal(ReleaseReviewClassifications.PartialAnalysis, rollup);
    }

    [Fact]
    public void Release_review_selector_no_match_checklist_items_are_informational()
    {
        var method = typeof(ReleaseReviewReporter).GetMethod("BuildChecklist", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var summary = new ReleaseReviewSummary(
            ReleaseReviewClassifications.SelectorNoMatch,
            "release.review.rollup.v1",
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            false,
            "Selectors matched no static evidence under requested scope.");
        var gap = GapForTest("selector", ReleaseReviewClassifications.SelectorNoMatch);

        var checklist = (IReadOnlyList<ReleaseReviewChecklistItem>)method!.Invoke(null, [summary, Array.Empty<ReleaseReviewFinding>(), new[] { gap }, 50])!;

        var item = Assert.Single(checklist);
        Assert.Equal("informational", item.Severity);
    }

    [Fact]
    public async Task Release_review_rejects_mixed_single_and_combined_inputs_without_raw_paths()
    {
        using var temp = new TempDirectory();
        var singleIndex = Path.Combine(temp.Path, "single.sqlite");
        var combinedIndex = Path.Combine(temp.Path, "combined.sqlite");
        var combinedSource = Path.Combine(temp.Path, "source.sqlite");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(singleIndex, manifest, []);
        SqliteIndexWriter.Write(combinedSource, manifest, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([combinedSource], combinedIndex, ["api"]));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "release-review",
            "--before", singleIndex,
            "--after", combinedIndex,
            "--out", Path.Combine(temp.Path, "release")
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("mixed input modes are deferred", error.ToString());
        Assert.DoesNotContain(temp.Path, error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Release_review_output_path_matrix_matches_spec()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api", ScannerVersions.TraceMap, commitSha: "1111111");
        var after = Manifest("api", ScannerVersions.TraceMap, commitSha: "2222222");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, []);

        await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(beforeIndex, afterIndex, Path.Combine(temp.Path, "dir"), Format: "json"));
        Assert.True(File.Exists(Path.Combine(temp.Path, "dir", "release-review.md")));
        Assert.True(File.Exists(Path.Combine(temp.Path, "dir", "release-review.json")));

        var jsonFile = Path.Combine(temp.Path, "release.json");
        await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(beforeIndex, afterIndex, jsonFile, Format: "json"));
        Assert.True(File.Exists(jsonFile));
        Assert.Contains("\"reportType\": \"release-review\"", await File.ReadAllTextAsync(jsonFile));

        var markdownFile = Path.Combine(temp.Path, "release.md");
        await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(beforeIndex, afterIndex, markdownFile));
        Assert.True(File.Exists(markdownFile));
        Assert.Contains("TraceMap Release Review Report", await File.ReadAllTextAsync(markdownFile));

        var extensionless = Path.Combine(temp.Path, "extensionless");
        await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(beforeIndex, afterIndex, extensionless, Format: "markdown"));
        Assert.True(File.Exists(Path.Combine(extensionless, "release-review.md")));
        Assert.True(File.Exists(Path.Combine(extensionless, "release-review.json")));
    }

    private static ScanManifest Manifest(
        string repo,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded",
        string commitSha = "abc1234567890")
    {
        return new ScanManifest(
            $"scan-{repo}-{commitSha}",
            repo,
            "https://example.invalid/repo.git",
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

    private static CodeFact RouteFact(ScanManifest manifest, string method, string template, string key, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: $"{method} {template}",
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

    private static CodeFact RouteFact(ScanManifest manifest, string method, string template, string key, string methodSymbol, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: methodSymbol,
            targetSymbol: methodSymbol,
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
                ["callKind"] = "method"
            });
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string file, int line)
    {
        return QueryPatternFact(manifest, null, file, line);
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string? sourceSymbol, string file, int line)
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
                ["sqlSourceKind"] = "literal-string",
                ["queryShapeHash"] = "shape123",
                ["rawSql"] = "select * from orders"
            });
    }

    private static CodeFact PackageFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.PackageReferenced,
            RuleIds.ProjectFile,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["ecosystem"] = "nuget",
                ["packageName"] = "Newtonsoft.Json",
                ["version"] = "13.0.3"
            });
    }

    private static string WriteV2Delta(string directory, string changesJson)
    {
        var path = Path.Combine(directory, $"contract-delta-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, $$"""
            {
              "version": "contract-delta-v2",
              "contract": "PackageManifest",
              "source": {
                "label": "api",
                "remoteUrl": "https://private.example.invalid/repo.git"
              },
              "changes": [
                {{changesJson}}
              ]
            }
            """);
        return path;
    }

    private static ReleaseReviewGap GapForTest(string kind, string classification)
    {
        return new ReleaseReviewGap(
            $"gap:test:{kind}",
            kind,
            "summary",
            null,
            "release.review.selector.v1",
            EvidenceTiers.Tier4Unknown,
            classification,
            $"{kind} gap",
            [],
            [],
            [],
            []);
    }

    private static string Sha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private static ReviewPriorityComponent PriorityComponent(string kind, string direction, string ruleId, string tier)
    {
        return new ReviewPriorityComponent(
            $"component:{kind}:{direction}:{ruleId}:{tier}",
            kind,
            null,
            direction,
            ruleId,
            tier,
            [
                new ReviewPriorityEvidenceRef(
                    "evidence:test",
                    "test",
                    ruleId,
                    tier,
                    "api",
                    "1111111",
                    "Controllers/TestController.cs",
                    10,
                    10)
            ],
            ["test limitation"],
            []);
    }

    private static ReleaseReviewDocument MinimalReleaseReviewDocument()
    {
        var query = new ReleaseReviewQuery(
            ["all"],
            false,
            false,
            null,
            null,
            null,
            null,
            false,
            false,
            false,
            100,
            50,
            25,
            1000,
            50,
            [],
            "release-review-composition",
            "1.0");
        var source = new ReleaseReviewSourceInfo(
            "api",
            "csharp",
            "scan-api",
            "1111111",
            "repo-hash",
            "root-hash",
            "Full",
            "Succeeded",
            "Level1SemanticAnalysis",
            []);
        var snapshot = new ReleaseReviewSnapshot("before", "single", [source], "Full", [], []);
        var summary = new ReleaseReviewSummary(
            ReleaseReviewClassifications.ActionableStaticEvidence,
            "release.review.rollup.v1",
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            false,
            "Static evidence requires review.");
        var section = new ReleaseReviewSection(ReleaseReviewStatuses.Available, [], [], []);
        return new ReleaseReviewDocument(
            "release-review",
            "1.0",
            "ReleaseReviewSingleV1",
            query,
            snapshot,
            snapshot with { Side = "after" },
            summary,
            [],
            section,
            section,
            section,
            section,
            section,
            section,
            section,
            [],
            [],
            []);
    }

    private static string[] PrioritySeverityVocabulary()
    {
        return
        [
            ReviewPrioritySeverityHints.CriticalReview,
            ReviewPrioritySeverityHints.HighReview,
            ReviewPrioritySeverityHints.MediumReview,
            ReviewPrioritySeverityHints.LowReview,
            ReviewPrioritySeverityHints.Info,
            ReviewPrioritySeverityHints.Unknown
        ];
    }

    private static string[] PriorityAttentionVocabulary()
    {
        return
        [
            ReviewPriorityAttentionLevels.HighestAttention,
            ReviewPriorityAttentionLevels.HighAttention,
            ReviewPriorityAttentionLevels.ModerateAttention,
            ReviewPriorityAttentionLevels.LowAttention,
            ReviewPriorityAttentionLevels.Informational,
            ReviewPriorityAttentionLevels.Unknown
        ];
    }

    private static string[] PriorityStatusVocabulary()
    {
        return
        [
            ReviewPriorityStatuses.Available,
            ReviewPriorityStatuses.NotRequested,
            ReviewPriorityStatuses.Unavailable,
            ReviewPriorityStatuses.Deferred,
            ReviewPriorityStatuses.Truncated
        ];
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
