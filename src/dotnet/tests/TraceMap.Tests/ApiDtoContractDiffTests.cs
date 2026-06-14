using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ApiDtoContractDiffTests
{
    [Fact]
    public async Task Contract_diff_single_writes_safe_deterministic_endpoint_and_dto_report()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var outDir = Path.Combine(temp.Path, "contract-diff");
        var manifest = Manifest("api", ScannerVersions.TraceMap);

        SqliteIndexWriter.Write(beforeIndex, manifest, [
            RouteFact(manifest, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10, "Api.OrdersController.Get(System.Int32)", "id"),
            DtoTypeFact(manifest, "Api.Contracts.OrderResponse", "Contracts/OrderResponse.cs", 3),
            PropertyFact(manifest, "Api.Contracts.OrderResponse", "status", "System.String", "Contracts/OrderResponse.cs", 6)
        ]);
        SqliteIndexWriter.Write(afterIndex, manifest, [
            RouteFact(manifest, "GET", "/api/orders/{orderId}", "/api/orders/{}", "Controllers/OrdersController.cs", 10, "Api.OrdersController.GetByOrderId(System.Int32)", "orderId"),
            DtoTypeFact(manifest, "Api.Contracts.OrderResponse", "Contracts/OrderResponse.cs", 3),
            PropertyFact(manifest, "Api.Contracts.OrderResponse", "status", "System.Int32", "Contracts/OrderResponse.cs", 6),
            PropertyFact(manifest, "Api.Contracts.OrderResponse", "trackingNumber", "System.String", "Contracts/OrderResponse.cs", 7)
        ]);

        var result = await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeIndex, afterIndex, outDir));

        Assert.True(File.Exists(Path.Combine(outDir, "contract-diff-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "contract-diff-report.json")));
        Assert.Equal("api-dto-contract-diff-single", result.Report.ReportType);
        Assert.Contains(result.Report.EndpointDiffs, row => row.Classification == ApiDtoContractDiffClassifications.ChangedEvidence);
        Assert.Contains(result.Report.RouteShapeDiffs, row => row.Classification == ApiDtoContractDiffClassifications.ChangedEvidence);
        Assert.Contains(result.Report.DtoPropertyDiffs, row => row.Classification == ApiDtoContractDiffClassifications.ChangedEvidence);
        Assert.Contains(result.Report.DtoPropertyDiffs, row => row.Classification == ApiDtoContractDiffClassifications.Added);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "AttachmentEvidenceUnavailable");
        Assert.All(result.Report.EndpointDiffs, row => Assert.Equal("api.dto.contract.diff.endpoint.v1", row.RuleId));
        Assert.All(result.Report.Gaps, gap => Assert.False(string.IsNullOrWhiteSpace(gap.RuleId)));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "contract-diff-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "contract-diff-report.json"));
        Assert.Contains("TraceMap API/DTO Contract Diff Report", markdown);
        Assert.Contains("\"reportType\": \"api-dto-contract-diff-single\"", json);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);

        var document = JsonSerializer.Deserialize<ApiDtoContractDiffReport>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(document);

        var secondOutDir = Path.Combine(temp.Path, "contract-diff-second");
        await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeIndex, afterIndex, secondOutDir));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "contract-diff-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "contract-diff-report.json")));
    }

    [Fact]
    public async Task Contract_diff_exit_code_only_for_actionable_rows_and_rejects_bad_selectors()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(beforeIndex, manifest, []);
        SqliteIndexWriter.Write(afterIndex, manifest, [DtoTypeFact(manifest, "Api.Contracts.OrderResponse", "Contracts/OrderResponse.cs", 3)]);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "contract-diff",
            "--before", beforeIndex,
            "--after", afterIndex,
            "--out", Path.Combine(temp.Path, "report"),
            "--exit-code"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("TraceMap contract-diff completed:", output.ToString());

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        exitCode = await TraceMapCommand.RunAsync([
            "contract-diff",
            "--before", beforeIndex,
            "--after", afterIndex,
            "--out", Path.Combine(temp.Path, "bad"),
            "--endpoint", "GET"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("--endpoint must be", error.ToString());

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        exitCode = await TraceMapCommand.RunAsync([
            "contract-diff",
            "--before", beforeIndex,
            "--after", afterIndex,
            "--out", Path.Combine(temp.Path, "bad-kind"),
            "--change-kind", "widget"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("--change-kind unsupported value", error.ToString());
    }

    [Fact]
    public async Task Contract_diff_downgrades_added_and_removed_for_reduced_coverage()
    {
        using var temp = new TempDirectory();
        var full = Manifest("api", ScannerVersions.TraceMap);
        var reduced = Manifest("api", ScannerVersions.TraceMap, analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", knownGaps: ["msbuild-load-failed"]);
        var beforeReduced = Path.Combine(temp.Path, "before-reduced.sqlite");
        var afterFull = Path.Combine(temp.Path, "after-full.sqlite");
        var beforeFull = Path.Combine(temp.Path, "before-full.sqlite");
        var afterReduced = Path.Combine(temp.Path, "after-reduced.sqlite");

        SqliteIndexWriter.Write(beforeReduced, reduced, []);
        SqliteIndexWriter.Write(afterFull, full, [DtoTypeFact(full, "Api.Contracts.OrderResponse", "Contracts/OrderResponse.cs", 3)]);
        SqliteIndexWriter.Write(beforeFull, full, [DtoTypeFact(full, "Api.Contracts.OrderResponse", "Contracts/OrderResponse.cs", 3)]);
        SqliteIndexWriter.Write(afterReduced, reduced, []);

        var added = await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeReduced, afterFull, Path.Combine(temp.Path, "added"), Scope: "dto-types"));
        var removed = await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeFull, afterReduced, Path.Combine(temp.Path, "removed"), Scope: "dto-types"));

        Assert.Contains(added.Report.DtoTypeDiffs, row => row.Classification == ApiDtoContractDiffClassifications.AddedWithBeforeGap);
        Assert.DoesNotContain(added.Report.DtoTypeDiffs, row => row.Classification == ApiDtoContractDiffClassifications.Added);
        Assert.Contains(removed.Report.DtoTypeDiffs, row => row.Classification == ApiDtoContractDiffClassifications.RemovedWithAfterGap);
        Assert.DoesNotContain(removed.Report.DtoTypeDiffs, row => row.Classification == ApiDtoContractDiffClassifications.Removed);
    }

    [Fact]
    public async Task Contract_diff_combined_keeps_same_route_in_different_sources_distinct()
    {
        using var temp = new TempDirectory();
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var clientManifest = Manifest("client", "tracemap-typescript/0.1.0");
        var serverManifest = Manifest("server", ScannerVersions.TraceMap);
        var beforeClient = Path.Combine(temp.Path, "before-client.sqlite");
        var beforeServer = Path.Combine(temp.Path, "before-server.sqlite");
        var afterClient = Path.Combine(temp.Path, "after-client.sqlite");
        var afterServer = Path.Combine(temp.Path, "after-server.sqlite");
        SqliteIndexWriter.Write(beforeClient, clientManifest, []);
        SqliteIndexWriter.Write(beforeServer, serverManifest, []);
        SqliteIndexWriter.Write(afterClient, clientManifest, [RouteFact(clientManifest, "GET", "/api/orders", "/api/orders", "src/routes.ts", 10, "Client.route", "")]);
        SqliteIndexWriter.Write(afterServer, serverManifest, [RouteFact(serverManifest, "GET", "/api/orders", "/api/orders", "Controllers/OrdersController.cs", 10, "Server.OrdersController.Get()", "")]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeClient, beforeServer], beforeCombined, ["client", "server"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterClient, afterServer], afterCombined, ["client", "server"]));

        var result = await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeCombined, afterCombined, Path.Combine(temp.Path, "diff"), Scope: "endpoints"));

        Assert.Equal("api-dto-contract-diff-combined", result.Report.ReportType);
        Assert.Equal(2, result.Report.EndpointDiffs.Count(row => row.Classification == ApiDtoContractDiffClassifications.Added));
        Assert.Equal(2, result.Report.EndpointDiffs.Select(row => row.StableKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(result.Report.EndpointDiffs, row => row.SourceLabel == "client");
        Assert.Contains(result.Report.EndpointDiffs, row => row.SourceLabel == "server");
    }

    [Fact]
    public async Task Contract_diff_generic_property_without_containing_type_is_review_tier()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(beforeIndex, manifest, []);
        SqliteIndexWriter.Write(afterIndex, manifest, [PropertyOnlyFact(manifest, "status", "Contracts/LooseShape.cs", 4)]);

        var result = await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeIndex, afterIndex, Path.Combine(temp.Path, "diff"), Scope: "dto-properties", Property: "status"));

        var row = Assert.Single(result.Report.DtoPropertyDiffs);
        Assert.Equal(ApiDtoContractDiffClassifications.NeedsReviewDiff, row.Classification);
        Assert.Contains(row.Notes, note => note.Contains("generic property", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Contract_diff_syntax_only_endpoint_is_review_tier_and_unsafe_values_do_not_render()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(beforeIndex, manifest, []);
        SqliteIndexWriter.Write(afterIndex, manifest, [
            RouteFact(manifest, "GET", "https://private.example.invalid/api/orders?password=secret", "/api/orders", "Controllers/OrdersController.cs", 10, "Api.OrdersController.Get()", "", EvidenceTiers.Tier3SyntaxOrTextual)
        ]);

        var result = await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeIndex, afterIndex, Path.Combine(temp.Path, "diff"), Scope: "endpoints"));
        var row = Assert.Single(result.Report.EndpointDiffs);
        Assert.Equal(ApiDtoContractDiffClassifications.NeedsReviewDiff, row.Classification);

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "diff", "contract-diff-report.json"));
        var markdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "diff", "contract-diff-report.md"));
        Assert.DoesNotContain("private.example.invalid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=secret", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hash:", json);
    }

    [Fact]
    public async Task Contract_diff_root_endpoint_keeps_strong_identity()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(beforeIndex, manifest, []);
        SqliteIndexWriter.Write(afterIndex, manifest, [
            RouteFact(manifest, "GET", "/", "/", "Controllers/HomeController.cs", 10, "Api.HomeController.Get()", "")
        ]);

        var result = await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeIndex, afterIndex, Path.Combine(temp.Path, "diff"), Scope: "endpoints"));

        var row = Assert.Single(result.Report.EndpointDiffs);
        Assert.Equal(ApiDtoContractDiffClassifications.Added, row.Classification);
        Assert.Equal("endpoint:self:GET:/", row.StableKey);
    }

    [Fact]
    public async Task Contract_diff_mixed_index_modes_fail_without_writing_output()
    {
        using var temp = new TempDirectory();
        var singleIndex = Path.Combine(temp.Path, "single.sqlite");
        var combinedIndex = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "out");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(singleIndex, manifest, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([singleIndex], combinedIndex, ["api"]));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "contract-diff",
            "--before", singleIndex,
            "--after", combinedIndex,
            "--out", outDir
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("mixed single and combined indexes are not supported", error.ToString());
        Assert.False(File.Exists(Path.Combine(outDir, "contract-diff-report.md")));
    }

    [Fact]
    public async Task Contract_diff_duplicate_identity_is_review_tier_with_gap()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(beforeIndex, manifest, []);
        SqliteIndexWriter.Write(afterIndex, manifest, [
            DtoTypeFact(manifest, "Api.Contracts.OrderResponse", "Contracts/OrderResponse.cs", 3),
            DtoTypeFact(manifest, "Api.Contracts.OrderResponse", "Contracts/Generated/OrderResponse.cs", 3)
        ]);

        var result = await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeIndex, afterIndex, Path.Combine(temp.Path, "diff"), Scope: "dto-types"));

        var row = Assert.Single(result.Report.DtoTypeDiffs);
        Assert.Equal(ApiDtoContractDiffClassifications.NeedsReviewDiff, row.Classification);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "DuplicateContractIdentity");
    }

    [Fact]
    public async Task Contract_diff_source_identity_conflict_downgrades_without_raw_remote()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var before = Manifest("api-before", ScannerVersions.TraceMap, remoteUrl: "https://example.invalid/before.git");
        var after = Manifest("api-after", ScannerVersions.TraceMap, remoteUrl: "https://example.invalid/after.git");
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [DtoTypeFact(after, "Api.Contracts.OrderResponse", "Contracts/OrderResponse.cs", 3)]);

        var result = await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeIndex, afterIndex, Path.Combine(temp.Path, "diff"), Scope: "dto-types"));

        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "SourceIdentityConflict");
        Assert.Contains(result.Report.DtoTypeDiffs, row => row.Classification == ApiDtoContractDiffClassifications.NeedsReviewDiff);
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "diff", "contract-diff-report.json"));
        Assert.DoesNotContain("example.invalid", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Contract_diff_opens_indexes_read_only_without_mutating_files()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(beforeIndex, manifest, []);
        SqliteIndexWriter.Write(afterIndex, manifest, [DtoTypeFact(manifest, "Api.Contracts.OrderResponse", "Contracts/OrderResponse.cs", 3)]);
        var beforeWrite = File.GetLastWriteTimeUtc(beforeIndex);
        var afterWrite = File.GetLastWriteTimeUtc(afterIndex);

        await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeIndex, afterIndex, Path.Combine(temp.Path, "diff"), Scope: "dto-types"));

        Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(beforeIndex));
        Assert.Equal(afterWrite, File.GetLastWriteTimeUtc(afterIndex));
    }

    [Fact]
    public async Task Contract_diff_caps_rows_and_emits_truncation_gap()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(beforeIndex, manifest, []);
        SqliteIndexWriter.Write(afterIndex, manifest, [
            DtoTypeFact(manifest, "Api.Contracts.A", "Contracts/A.cs", 3),
            DtoTypeFact(manifest, "Api.Contracts.B", "Contracts/B.cs", 3)
        ]);

        var result = await ApiDtoContractDiffReporter.WriteAsync(new ApiDtoContractDiffOptions(beforeIndex, afterIndex, Path.Combine(temp.Path, "diff"), Scope: "dto-types", MaxDiffRows: 1));

        Assert.Single(result.Report.DtoTypeDiffs);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "TruncatedByLimit");
        Assert.True(result.Report.Summary.Truncated);
    }

    private static ScanManifest Manifest(
        string repo,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded",
        IReadOnlyList<string>? knownGaps = null,
        string remoteUrl = "https://example.invalid/repo.git")
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            remoteUrl,
            "main",
            "abc1234567890",
            scannerVersion,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            analysisLevel,
            buildStatus,
            [],
            [],
            [],
            knownGaps ?? [],
            ".",
            FactFactory.Hash(repo, 32),
            FactFactory.Hash($"{repo}-git-root", 32));
    }

    private static CodeFact RouteFact(
        ScanManifest manifest,
        string method,
        string template,
        string key,
        string file,
        int line,
        string methodSymbol,
        string routeParameters,
        string tier = EvidenceTiers.Tier2Structural)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            tier,
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
                ["routeParameters"] = routeParameters,
                ["routeTemplates"] = template,
                ["handlerSymbol"] = methodSymbol
            });
    }

    private static CodeFact DtoTypeFact(ScanManifest manifest, string typeName, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.TypeDeclared,
            RuleIds.CSharpSemanticDeclarations,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: typeName,
            contractElement: typeName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["typeName"] = typeName,
                ["fullyQualifiedTypeName"] = typeName,
                ["namespace"] = "Api.Contracts",
                ["assemblyName"] = "Api"
            });
    }

    private static CodeFact PropertyFact(ScanManifest manifest, string containingType, string propertyName, string declaredType, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.PropertyDeclared,
            RuleIds.CSharpSemanticDeclarations,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: containingType,
            targetSymbol: $"{containingType}.{propertyName}",
            contractElement: propertyName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["containingType"] = containingType,
                ["propertyName"] = propertyName,
                ["declaredType"] = declaredType,
                ["nullability"] = "non-null",
                ["required"] = "true"
            });
    }

    private static CodeFact PropertyOnlyFact(ScanManifest manifest, string propertyName, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.PropertyDeclared,
            RuleIds.CSharpSyntaxDeclarations,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            contractElement: propertyName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["propertyName"] = propertyName
            });
    }
}
