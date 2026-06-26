using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CombinedDependencyReportTests
{
    [Fact]
    public async Task Report_rejects_single_language_index()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outPath = Path.Combine(temp.Path, "report.md");
        SqliteIndexWriter.Write(indexPath, Manifest("single", "tracemap-milestone15"), []);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["report", "--index", indexPath, "--out", outPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("combined index", error.ToString());
        Assert.False(File.Exists(outPath));
    }

    [Fact]
    public async Task Report_writes_markdown_json_and_endpoint_surface_evidence_without_mutating_endpoint_matches()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverOneIndex = Path.Combine(temp.Path, "server-one.sqlite");
        var serverTwoIndex = Path.Combine(temp.Path, "server-two.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var clientManifest = Manifest("client", "tracemap-typescript/0.1.0", analysisLevel: "Level1SemanticAnalysisReduced", buildStatus: "FailedOrPartial", knownGaps: ["tsconfig: dependency missing"]);
        var serverOneManifest = Manifest("server-one", "tracemap-milestone15");
        var serverTwoManifest = Manifest("server-two", "tracemap-milestone15");

        SqliteIndexWriter.Write(clientIndex, clientManifest, [
            HttpClientFact(clientManifest, "GET", "/api/orders/{id}", "/api/orders/{}", "src/orders.ts", 8),
            HttpClientFact(clientManifest, "DELETE", "/api/orders/archive", "/api/orders/archive", "src/orders.ts", 12),
            HttpClientFact(clientManifest, "GET", "/api/client-only", "/api/client-only", "src/orders.ts", 16),
            DynamicClientFact(clientManifest, "src/orders.ts", 20, "HelperFunctionCall:http://private.example/api")
        ]);
        SqliteIndexWriter.Write(serverOneIndex, serverOneManifest, [
            RouteFact(serverOneManifest, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10),
            RouteFact(serverOneManifest, "POST", "/api/orders/archive", "/api/orders/archive", "Controllers/OrdersController.cs", 20),
            RouteFact(serverOneManifest, "GET", "/api/server-only", "/api/server-only", "Controllers/OrdersController.cs", 30),
            SqlTextFact(serverOneManifest, "Infrastructure/queries.sql", 3),
            QueryPatternFact(serverOneManifest, "Infrastructure/OrderRepository.cs", 14),
            ConfigFact(serverOneManifest, "appsettings.json", 4)
        ]);
        SqliteIndexWriter.Write(serverTwoIndex, serverTwoManifest, [
            RouteFact(serverTwoManifest, "GET", "/api/orders/{id}", "/api/orders/{}", "Controllers/OrdersController.cs", 10)
        ]);

        await CombinedIndexBuilder.CombineAsync(
            new CombineOptions(
                [clientIndex, serverOneIndex, serverTwoIndex],
                combinedPath,
                ["client", "server-one", "server-two"]));
        var endpointRowsBefore = await CountAsync(combinedPath, "endpoint_matches");

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        Assert.True(File.Exists(Path.Combine(outDir, "dependency-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "dependency-report.json")));
        Assert.Equal(endpointRowsBefore, await CountAsync(combinedPath, "endpoint_matches"));
        Assert.Contains(result.Report.CoverageWarnings, warning => warning.Contains("known gaps", StringComparison.Ordinal));
        Assert.Equal(2, result.Report.EndpointFindings.Count(finding => finding.Classification == CombinedEndpointClassifications.MatchedEndpoint && finding.NormalizedPathKey == "/api/orders/{}"));
        Assert.Contains(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.MethodMismatch);
        Assert.Contains(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.DynamicClientUrlNeedsReview);
        Assert.Contains(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.ClientCallNoServerEndpoint);
        Assert.Contains(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.ServerEndpointNoClientMatch);
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query" && surface.TableName == "orders" && surface.ColumnNames == "id;status");
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query" && surface.TextHash == "abc123" && surface.ColumnNames == "n/a");
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "package-config" && surface.ConfigKey == "ConnectionStrings:Default");

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.md"));
        Assert.Contains("TraceMap Dependency Report", markdown);
        Assert.Contains("static shape or hash evidence", markdown);
        Assert.DoesNotContain("select * from orders", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private.example", markdown, StringComparison.OrdinalIgnoreCase);

        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.json"));
        Assert.Contains("\"clientScanId\"", json);
        Assert.Contains("\"serverEndLine\"", json);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private.example", json, StringComparison.OrdinalIgnoreCase);
        var document = JsonSerializer.Deserialize<CombinedDependencyReportDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(document);
        Assert.Equal(result.Report.EndpointFindings.Count, document!.EndpointFindings.Count);

        var secondOutDir = Path.Combine(temp.Path, "report-second");
        await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, secondOutDir));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "dependency-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "dependency-report.json")));
    }

    [Fact]
    public async Task Report_redacts_absolute_paths_and_raw_surface_target_symbols()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "safe-report");
        var clientManifest = Manifest("client", "tracemap-typescript/0.1.0");
        var serverManifest = Manifest("server", "tracemap-milestone15");
        var absoluteSqlPath = Path.GetFullPath(Path.Combine(temp.Path, "..", "outside-repo", "queries.sql"));
        const string rawSql = "select password from users where api_key = 'secret'";
        const string rawConfig = "Server=private;Password=secret;";

        SqliteIndexWriter.Write(clientIndex, clientManifest, []);
        SqliteIndexWriter.Write(serverIndex, serverManifest, [
            RawSqlTextFact(serverManifest, absoluteSqlPath, 7, rawSql),
            RawConfigFact(serverManifest, absoluteSqlPath, 9, rawConfig)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "sql-query" && surface.DisplayName.StartsWith("unknown-sql:", StringComparison.Ordinal));
        Assert.Contains(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "package-config" && surface.DisplayName.StartsWith("unknown-package-config:", StringComparison.Ordinal));
        Assert.All(result.Report.DependencySurfaces, surface => Assert.DoesNotContain(absoluteSqlPath, surface.FilePath, StringComparison.Ordinal));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.json"));
        foreach (var text in new[] { markdown, json })
        {
            Assert.Contains("absolute-path-hash:", text);
            Assert.DoesNotContain(absoluteSqlPath, text, StringComparison.Ordinal);
            Assert.DoesNotContain(rawSql, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(rawConfig, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Report_summarizes_value_origin_evidence_and_boundary_review_notes_deterministically()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "api.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var manifest = Manifest("api", "tracemap-milestone15");
        var controller = "Api.OrdersController.Get(System.String)";
        var service = "Api.OrderService.Query(System.String)";

        SqliteIndexWriter.Write(index, manifest, [
            ArgumentPassedFact(manifest, controller, service, "System.String request", "id", "System.String", "Controllers/OrdersController.cs", 12),
            FlowBoundaryFact(manifest, FactTypes.CallbackBoundary, "CapturedValueCallbackBoundary", "Controllers/OrdersController.cs", 16),
            FlowBoundaryFact(manifest, FactTypes.AsyncBoundary, "AwaitBoundary", "Controllers/OrdersController.cs", 18)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["api"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        Assert.Equal(1L, result.Report.Summary.ValueOriginEvidenceCounts["argument-flows"]);
        Assert.Equal(1L, result.Report.Summary.ValueOriginEvidenceCounts["parameter-forward-edges"]);
        Assert.Equal(1L, result.Report.Summary.ValueOriginEvidenceCounts["callback-boundaries"]);
        Assert.Equal(1L, result.Report.Summary.ValueOriginEvidenceCounts["async-boundaries"]);
        Assert.Contains(result.Report.NeedsReview, row =>
            row.ReviewKind == FactTypes.CallbackBoundary
            && row.RuleId == RuleIds.CSharpSemanticFlowBoundary
            && row.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && row.Message.Contains("does not prove callback invocation", StringComparison.Ordinal));
        Assert.Contains(result.Report.NeedsReview, row =>
            row.ReviewKind == FactTypes.AsyncBoundary
            && row.RuleId == RuleIds.CSharpSemanticFlowBoundary
            && row.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && row.Message.Contains("runtime scheduling", StringComparison.Ordinal));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.json"));
        Assert.Contains("Value-origin evidence by kind", markdown);
        Assert.Contains("callback-boundaries", json);
        Assert.Contains("\"ruleId\": \"csharp.semantic.flowboundary.v1\"", json);
        Assert.Contains("\"evidenceTier\": \"Tier3SyntaxOrTextual\"", json);
        Assert.Contains("does not prove callback invocation", markdown);

        var secondOutDir = Path.Combine(temp.Path, "report-second");
        await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, secondOutDir));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "dependency-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(secondOutDir, "dependency-report.json")));
    }

    [Fact]
    public async Task Report_keeps_same_table_sql_surfaces_separate_by_shape_hash()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "api.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var manifest = Manifest("api", "tracemap-milestone15");
        SqliteIndexWriter.Write(indexPath, manifest, [
            QueryPatternFact(manifest, "Infrastructure/Orders.cs", 10, "shape-select-id", "id"),
            QueryPatternFact(manifest, "Infrastructure/Orders.cs", 20, "shape-select-status", "status")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([indexPath], combinedPath, ["api"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        var sqlSurfaces = result.Report.DependencySurfaces.Where(surface => surface.SurfaceKind == "sql-query").ToArray();
        Assert.Equal(2, sqlSurfaces.Length);
        Assert.Contains(sqlSurfaces, surface => surface.DisplayName == "shape:shape-select-id" && surface.TableName == "orders" && surface.ColumnNames == "id");
        Assert.Contains(sqlSurfaces, surface => surface.DisplayName == "shape:shape-select-status" && surface.TableName == "orders" && surface.ColumnNames == "status");
    }

    [Fact]
    public async Task Report_renders_package_surface_metadata_without_unsafe_version_values()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "api.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var manifest = Manifest("api", "tracemap-milestone15");
        const string unsafeVersion = "git+https://token@example.invalid/private/package.git";
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.PackageReferenced,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/App.csproj", 9, 9, null, "test", "test/1.0"),
                targetSymbol: "Private.Package",
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["dependencyGroup"] = "PackageReference",
                    ["dependencyScope"] = "runtime",
                    ["ecosystem"] = "nuget",
                    ["manifestKind"] = "csproj",
                    ["packageManager"] = "nuget",
                    ["packageName"] = "Private.Package",
                    ["surfaceKind"] = "package-config",
                    ["version"] = unsafeVersion
                })
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([indexPath], combinedPath, ["api"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        var surface = Assert.Single(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "package-config");
        Assert.Equal("Private.Package", surface.PackageName);
        Assert.Equal("nuget", surface.Ecosystem);
        Assert.Equal("csproj", surface.ManifestKind);
        Assert.Equal("runtime", surface.DependencyScope);
        Assert.Null(surface.Version);
        Assert.NotNull(surface.VersionHash);
        Assert.Equal("unsafe-package-version", surface.RedactionReason);
        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.json"));
        Assert.DoesNotContain(unsafeVersion, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(unsafeVersion, json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unsafe-package-version", json);
    }

    [Fact]
    public async Task Report_treats_blank_package_version_as_missing_not_unsafe()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "api.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var manifest = Manifest("api", "tracemap-milestone15");
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.PackageReferenced,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/App.csproj", 9, 9, null, "test", "test/1.0"),
                targetSymbol: "Versionless.Package",
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["dependencyGroup"] = "PackageReference",
                    ["dependencyScope"] = "runtime",
                    ["ecosystem"] = "nuget",
                    ["manifestKind"] = "csproj",
                    ["packageName"] = "Versionless.Package",
                    ["surfaceKind"] = "package-config",
                    ["version"] = ""
                })
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([indexPath], combinedPath, ["api"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        var surface = Assert.Single(result.Report.DependencySurfaces, surface => surface.SurfaceKind == "package-config");
        Assert.Equal("Versionless.Package", surface.PackageName);
        Assert.Null(surface.Version);
        Assert.Null(surface.VersionHash);
        Assert.Null(surface.RedactionReason);
        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.json"));
        Assert.Contains("version n/a", markdown);
        Assert.DoesNotContain("unsafe-package-version", json);
        Assert.Contains("\"versionHash\": null", json);
        Assert.Contains("\"redactionReason\": null", json);
    }

    [Fact]
    public async Task Report_normalizes_legacy_package_surface_kind()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "api.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var manifest = Manifest("api", "tracemap-milestone15");
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.PackageReferenced,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/App.csproj", 9, 9, null, "test", "test/1.0"),
                targetSymbol: "Legacy.Package",
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ecosystem"] = "nuget",
                    ["manifestKind"] = "csproj",
                    ["packageName"] = "Legacy.Package",
                    ["surfaceKind"] = "package"
                })
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([indexPath], combinedPath, ["api"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        var surface = Assert.Single(result.Report.DependencySurfaces);
        Assert.Equal("package-config", surface.SurfaceKind);
        Assert.Equal("Legacy.Package", surface.PackageName);
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.json"));
        Assert.Contains("\"surfaceKind\": \"package-config\"", json);
        Assert.DoesNotContain("\"surfaceKind\": \"package\"", json);
    }

    [Fact]
    public async Task Report_projects_legacy_data_descriptors_with_hash_only_display_and_analysis_gap_exclusion()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "api.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var manifest = Manifest("api", "tracemap-milestone15");
        const string descriptorName = "Customer|Ledger";

        SqliteIndexWriter.Write(indexPath, manifest, [
            LegacyDataEntityFact(manifest, descriptorName, "Models/Store.dbml", 12),
            LegacyDataGapFact(manifest, "Models/Store.dbml", 20)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([indexPath], combinedPath, ["api"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        var surface = Assert.Single(result.Report.DependencySurfaces, row => row.SurfaceKind == "legacy-data");
        Assert.Equal("data-model", surface.SurfaceSubtype);
        Assert.Equal(RuleIds.LegacyDataDbml, surface.RuleId);
        Assert.Equal(RuleIds.LegacyDataModelSurface, surface.LegacyDataProjectionRuleId);
        Assert.Equal("dbml", surface.LegacyDataMetadataFormat);
        Assert.Equal("entity", surface.LegacyDataModelKind);
        Assert.Equal("conceptual", surface.LegacyDataDescriptorRole);
        Assert.Equal("reduced", surface.LegacyDataCoverageLabel);
        Assert.Equal("tracemap-milestone15", surface.LegacyDataExtractorVersion);
        Assert.False(surface.LegacyDataDisplayClearance);
        Assert.StartsWith("entity:hash:", surface.DisplayName, StringComparison.Ordinal);
        Assert.DoesNotContain(descriptorName, surface.DisplayName, StringComparison.Ordinal);
        Assert.Contains("descriptor-display-hash-only-without-claim-context", surface.LegacyDataLimitations ?? []);
        Assert.Contains(result.Report.NeedsReview, row => row.RuleId == RuleIds.LegacyDataDbml && row.ReviewKind == FactTypes.AnalysisGap);

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.json"));
        Assert.Contains("static descriptor evidence only", markdown);
        Assert.Contains("subtype data-model", markdown);
        Assert.Contains("role conceptual model entity", markdown);
        Assert.Contains("extractor tracemap-milestone15", markdown);
        Assert.Contains("\"surfaceSubtype\": \"data-model\"", json);
        Assert.Contains("\"legacyDataProjectionRuleId\": \"legacy.data.model.surface.v1\"", json);
        Assert.Contains("\"legacyDataExtractorVersion\": \"tracemap-milestone15\"", json);
        Assert.DoesNotContain(descriptorName, markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(descriptorName, json, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime database use", surface.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Legacy_data_projection_flags_duplicate_descriptor_identity()
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["descriptorRole"] = "conceptual",
            ["displayNameHash"] = "display-hash",
            ["metadataFormat"] = "dbml",
            ["modelKind"] = "entity",
            ["stableModelKey"] = "ldm:duplicate"
        };

        var surfaces = CombinedSurfaceProjection.BuildSurfaces([
            LegacyDataInput("cf-1", "of-1", properties),
            LegacyDataInput("cf-2", "of-2", properties)
        ]);

        Assert.Equal(2, surfaces.Count);
        Assert.All(surfaces, surface => Assert.Contains("duplicate-stable-identity", surface.LegacyDataLimitations ?? []));
    }

    [Fact]
    public void Legacy_data_projection_falls_back_when_model_identity_fields_are_absent()
    {
        var surfaces = CombinedSurfaceProjection.BuildSurfaces([
            LegacyDataInput(
                "cf-current",
                "of-current",
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["entityHash"] = "entity-hash",
                    ["metadataKind"] = "Dbml"
                })
        ]);

        var surface = Assert.Single(surfaces);
        Assert.Equal("legacy-data", surface.SurfaceKind);
        Assert.Equal("data-model", surface.SurfaceSubtype);
        Assert.Equal("dbml", surface.LegacyDataMetadataFormat);
        Assert.Equal("entity", surface.LegacyDataModelKind);
        Assert.Equal("unknown", surface.LegacyDataCoverageLabel);
        Assert.Equal("entity:hash:entity-hash", surface.DisplayName);
        Assert.False(surface.LegacyDataDisplayClearance);
    }

    [Fact]
    public void Legacy_data_projection_preserves_legacy_rule_sql_facts_as_sql_surfaces()
    {
        var surfaces = CombinedSurfaceProjection.BuildSurfaces([
            new CombinedSurfaceFactInput(
                "cf-sql",
                "src-1",
                "api",
                "of-sql",
                "scan-api",
                "abc123",
                FactTypes.SqlTextUsed,
                RuleIds.LegacyDataTypedDataSet,
                EvidenceTiers.Tier3SyntaxOrTextual,
                "Models/Orders.xsd",
                18,
                18,
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sqlSourceKind"] = "tableadapter-command",
                    ["textHash"] = "sql-shape-hash",
                    ["textLength"] = "42"
                })
        ]);

        var surface = Assert.Single(surfaces);
        Assert.Equal("sql-query", surface.SurfaceKind);
        Assert.Equal("sql-shape-hash", surface.TextHash);
        Assert.Null(surface.LegacyDataProjectionRuleId);
    }

    [Fact]
    public void Surface_projection_scopes_remoting_hash_identity_to_remoting_facts()
    {
        var surfaces = CombinedSurfaceProjection.BuildSurfaces([
            new CombinedSurfaceFactInput(
                "cf-config",
                "src-1",
                "api",
                "of-config",
                "scan-api",
                "abc123",
                FactTypes.ConnectionStringDeclared,
                RuleIds.ConfigKey,
                EvidenceTiers.Tier3SyntaxOrTextual,
                "App.config",
                8,
                8,
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["configKey"] = "ConnectionStrings:Orders",
                    ["valueHash"] = "abcdef1234567890"
                }),
            new CombinedSurfaceFactInput(
                "cf-remoting",
                "src-1",
                "api",
                "of-remoting",
                "scan-api",
                "abc123",
                FactTypes.RemotingConfigServiceDeclared,
                RuleIds.LegacyRemotingConfig,
                EvidenceTiers.Tier2Structural,
                "App.config",
                12,
                12,
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["objectUriHash"] = "abcdef1234567890",
                    ["registrationKind"] = "well-known-service"
                })
        ]);

        var config = Assert.Single(surfaces, surface => surface.CombinedFactId == "cf-config");
        Assert.Equal("package-config", config.SurfaceKind);
        Assert.Null(config.ShapeHash);

        var remoting = Assert.Single(surfaces, surface => surface.CombinedFactId == "cf-remoting");
        Assert.Equal("remoting-endpoint", remoting.SurfaceKind);
        Assert.Equal("objectUri-abcdef12", remoting.DisplayName);
        Assert.Equal("objectUri-abcdef1234567890", remoting.ShapeHash);
    }

    [Fact]
    public void Legacy_data_projection_classifies_edmx_msl_from_source_section()
    {
        var surfaces = CombinedSurfaceProjection.BuildSurfaces([
            LegacyDataInput(
                "cf-msl",
                "of-msl",
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["descriptorKind"] = "entity-table",
                    ["displayNameHash"] = "display-hash",
                    ["metadataFormat"] = "edmx",
                    ["modelKind"] = "mapping",
                    ["sourceSection"] = "msl",
                    ["stableModelKey"] = "ldm:msl"
                })
        ]);

        var surface = Assert.Single(surfaces);
        Assert.Equal("legacy-data", surface.SurfaceKind);
        Assert.Equal("edmx-msl", surface.LegacyDataSourceArtifactType);
    }

    [Fact]
    public async Task Report_includes_same_source_endpoint_matches_and_directory_row_caps()
    {
        using var temp = new TempDirectory();
        var bffIndex = Path.Combine(temp.Path, "bff.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "row-capped-report");
        var manifest = Manifest("bff", "tracemap-milestone15");
        var facts = new List<CodeFact>
        {
            HttpClientFact(manifest, "GET", "/api/self", "/api/self", "src/Self.cs", 8),
            RouteFact(manifest, "GET", "/api/self", "/api/self", "src/SelfController.cs", 10)
        };
        for (var index = 0; index < 205; index++)
        {
            facts.Add(PackageFact(manifest, index));
        }

        SqliteIndexWriter.Write(bffIndex, manifest, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([bffIndex], combinedPath, ["bff"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        Assert.Contains(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.MatchedEndpoint && finding.SameSource);
        Assert.Equal(205, result.Report.DependencySurfaces.Count(surface => surface.SurfaceKind == "package-config"));
        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.md"));
        Assert.Contains("Showing first 200 of", markdown);
    }

    [Fact]
    public async Task Report_caps_message_candidate_edges_per_destination_with_warning()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "api.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var manifest = Manifest("api", "tracemap-milestone15");
        var facts = new List<CodeFact>();
        for (var index = 0; index < 11; index++)
        {
            facts.Add(MessageSurfaceFact(manifest, FactTypes.MessagePublisherSurface, RuleIds.MessageSurfacePublish, "publish", index));
            facts.Add(MessageSurfaceFact(manifest, FactTypes.MessageConsumerSurface, RuleIds.MessageSurfaceConsume, "consume", index));
        }

        SqliteIndexWriter.Write(indexPath, manifest, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([indexPath], combinedPath, ["api"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        Assert.Equal(100, result.Report.DependencyEdges.Count(edge => edge.EdgeKind == "message-publish-consume"));
        Assert.Contains(result.Report.CoverageWarnings, warning =>
            warning.Contains("Message candidate edge generation truncated for destination hash", StringComparison.Ordinal)
            && warning.Contains("at 100 rows", StringComparison.Ordinal));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "dependency-report.json"));
        Assert.Contains("Message candidate edge generation truncated", json);
    }

    [Fact]
    public async Task Report_matches_multi_method_any_and_optional_server_routes()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var outDir = Path.Combine(temp.Path, "report");
        var clientManifest = Manifest("client", "tracemap-typescript/0.1.0");
        var serverManifest = Manifest("server", "tracemap-milestone15");

        SqliteIndexWriter.Write(clientIndex, clientManifest, [
            HttpClientFact(clientManifest, "GET", "/api/methods", "/api/methods", "src/client.ts", 5),
            HttpClientFact(clientManifest, "ANY", "/api/any", "/api/any", "src/client.ts", 10),
            HttpClientFact(clientManifest, "GET", "/api/items", "/api/items", "src/client.ts", 15)
        ]);
        SqliteIndexWriter.Write(serverIndex, serverManifest, [
            RouteFact(serverManifest, "GET,POST", "/api/methods", "/api/methods", "Controllers/ApiController.cs", 5),
            RouteFact(serverManifest, "POST", "/api/any", "/api/any", "Controllers/ApiController.cs", 10),
            RouteFact(serverManifest, "GET", "/api/items/{id?}", "/api/items/{?}", "Controllers/ApiController.cs", 15)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, outDir));

        Assert.Contains(result.Report.EndpointFindings, finding =>
            finding.Classification == CombinedEndpointClassifications.MatchedEndpoint
            && finding.NormalizedPathKey == "/api/methods"
            && finding.HttpMethod == "GET");
        Assert.Contains(result.Report.EndpointFindings, finding =>
            finding.Classification == CombinedEndpointClassifications.MatchedEndpoint
            && finding.NormalizedPathKey == "/api/any"
            && finding.HttpMethod == "ANY");
        Assert.Contains(result.Report.EndpointFindings, finding =>
            finding.Classification == CombinedEndpointClassifications.OptionalSegmentMatch
            && finding.NormalizedPathKey == "/api/items");
        Assert.DoesNotContain(result.Report.EndpointFindings, finding => finding.Classification == CombinedEndpointClassifications.MethodMismatch);
    }

    [Fact]
    public async Task Combine_infers_jvm_and_python_languages()
    {
        using var temp = new TempDirectory();
        var jvmIndex = Path.Combine(temp.Path, "jvm.sqlite");
        var pythonIndex = Path.Combine(temp.Path, "python.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(jvmIndex, Manifest("jvm", "tracemap-jvm-mvp"), []);
        SqliteIndexWriter.Write(pythonIndex, Manifest("python", "tracemap-python/0.1.0"), []);

        await CombinedIndexBuilder.CombineAsync(new CombineOptions([jvmIndex, pythonIndex], combinedPath, ["jvm", "python"]));

        var labels = await ReadLabelLanguagesAsync(combinedPath);
        Assert.Equal("jvm", labels["jvm"]);
        Assert.Equal("python", labels["python"]);
    }

    private static ScanManifest Manifest(
        string repo,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded",
        IReadOnlyList<string>? knownGaps = null)
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            null,
            "main",
            "abc123",
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
            FactFactory.Hash("git-root", 32));
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
                ["urlKind"] = "template",
                ["clientFramework"] = "test"
            });
    }

    private static CodeFact DynamicClientFact(ScanManifest manifest, string file, int line, string dynamicReason)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "dynamic",
            contractElement: "GET",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethod"] = "GET",
                ["methodName"] = "GET",
                ["urlKind"] = "dynamic",
                ["dynamicReason"] = dynamicReason
            });
    }

    private static CodeFact ArgumentPassedFact(
        ScanManifest manifest,
        string caller,
        string callee,
        string argumentSymbol,
        string parameterName,
        string parameterType,
        string file,
        int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ArgumentPassed,
            RuleIds.CSharpSemanticValueFlow,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: caller,
            targetSymbol: callee,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["argumentExpressionHash"] = "arg-hash",
                ["argumentExpressionKind"] = "IdentifierName",
                ["argumentOrdinal"] = "0",
                ["argumentSymbol"] = argumentSymbol,
                ["argumentSymbolKind"] = "Parameter",
                ["argumentType"] = parameterType,
                ["callKind"] = "method",
                ["parameterName"] = parameterName,
                ["parameterOrdinal"] = "0",
                ["parameterType"] = parameterType
            });
    }

    private static CodeFact FlowBoundaryFact(ScanManifest manifest, string factType, string boundaryKind, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            factType,
            RuleIds.CSharpSemanticFlowBoundary,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: "Api.OrdersController.Get(System.String)",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["boundaryKind"] = boundaryKind
            });
    }

    private static CodeFact RouteFact(ScanManifest manifest, string method, string template, string key, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
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

    private static CodeFact SqlTextFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.SqlTextUsed,
            RuleIds.DatabaseSqlText,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "sql",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["textHash"] = "abc123",
                ["textLength"] = "21",
                ["sqlSourceKind"] = "sql-file"
            });
    }

    private static CodeFact RawSqlTextFact(ScanManifest manifest, string file, int line, string rawSql)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.SqlTextUsed,
            RuleIds.DatabaseSqlText,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: rawSql,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["sqlSourceKind"] = "literal-string"
            });
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string file, int line)
    {
        return QueryPatternFact(manifest, file, line, "shape123", "id;status");
    }

    private static CodeFact QueryPatternFact(ScanManifest manifest, string file, int line, string shapeHash, string columns)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "orders",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationName"] = "SELECT",
                ["tableName"] = "orders",
                ["columnNames"] = columns,
                ["sqlSourceKind"] = "literal-string",
                ["queryShapeHash"] = shapeHash
            });
    }

    private static CodeFact ConfigFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ConnectionStringDeclared,
            RuleIds.ConfigKey,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "ConnectionStrings:Default",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["keyPath"] = "ConnectionStrings:Default",
                ["valueHash"] = "hash-only"
            });
    }

    private static CodeFact RawConfigFact(ScanManifest manifest, string file, int line, string rawConfig)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ConnectionStringDeclared,
            RuleIds.ConfigKey,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: rawConfig,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["valueHash"] = "hash-only"
            });
    }

    private static CodeFact PackageFact(ScanManifest manifest, int index)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.PackageReferenced,
            RuleIds.ProjectFile,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("src/App.csproj", index + 1, index + 1, null, "test", "test/1.0"),
            targetSymbol: $"Package.{index:000}",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["dependencyGroup"] = "PackageReference",
                ["dependencyScope"] = "runtime",
                ["ecosystem"] = "nuget",
                ["manifestKind"] = "csproj",
                ["package"] = $"Package.{index:000}",
                ["packageManager"] = "nuget",
                ["packageName"] = $"Package.{index:000}",
                ["surfaceKind"] = "package-config",
                ["version"] = "1.0.0"
            });
    }

    private static CodeFact MessageSurfaceFact(ScanManifest manifest, string factType, string ruleId, string direction, int index)
    {
        return FactFactory.Create(
            manifest,
            factType,
            ruleId,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan($"Messaging/Message{index:00}.cs", index + 1, index + 1, null, "test", "test/1.0"),
            targetSymbol: $"{direction}:orders.shared:{index:00}",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["destinationIdentityStatus"] = "static",
                ["frameworkFamily"] = "test-broker",
                ["frameworkFeature"] = "test-surface",
                ["normalizedDestinationKey"] = "orders.shared",
                ["operationDirection"] = direction,
                ["operationKind"] = direction == "publish" ? "send" : "receive",
                ["stableMessageSurfaceKey"] = $"message:orders.shared:{direction}:{index:00}",
                ["surfaceKind"] = "message-queue"
            });
    }

    private static CodeFact LegacyDataEntityFact(ScanManifest manifest, string displayName, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.LegacyDataEntityDeclared,
            RuleIds.LegacyDataDbml,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: displayName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverageLabel"] = "reduced",
                ["descriptorRole"] = "conceptual",
                ["displayName"] = displayName,
                ["metadataFormat"] = "dbml",
                ["metadataHash"] = "metadata-hash",
                ["metadataKind"] = "Dbml",
                ["modelKind"] = "entity",
                ["sourceMetadataFactId"] = "metadata-fact-1",
                ["stableModelKey"] = "ldm:test-model-key",
                ["supportingFactIds"] = "metadata-fact-1"
            });
    }

    private static CodeFact LegacyDataGapFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.LegacyDataDbml,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "Legacy data descriptor gap requires review.",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = "AmbiguousLegacyDataModelIdentity",
                ["metadataFormat"] = "dbml"
            });
    }

    private static CombinedSurfaceFactInput LegacyDataInput(string combinedFactId, string originalFactId, IReadOnlyDictionary<string, string> properties)
    {
        return new CombinedSurfaceFactInput(
            combinedFactId,
            "src-1",
            "api",
            originalFactId,
            "scan-api",
            "abc123",
            FactTypes.LegacyDataEntityDeclared,
            RuleIds.LegacyDataDbml,
            EvidenceTiers.Tier2Structural,
            "Models/Store.dbml",
            12,
            12,
            properties);
    }

    private static async Task<long> CountAsync(string sqlitePath, string table)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {table};";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<Dictionary<string, string>> ReadLabelLanguagesAsync(string sqlitePath)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select label, language from index_sources order by label;";
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }

        return result;
    }
}
