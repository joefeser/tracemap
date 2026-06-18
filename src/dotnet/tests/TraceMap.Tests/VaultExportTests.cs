using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class VaultExportTests
{
    [Fact]
    public async Task Vault_export_writes_deterministic_json_and_markdown_without_private_paths()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(temp.Path);
        var sourceIds = await ReadSourceIdsAsync(combinedPath);
        var catalogPath = WriteClaimCatalog(temp.Path, sourceIds.Values, "public-safe");
        var firstOut = Path.Combine(temp.Path, "vault-a");
        var secondOut = Path.Combine(temp.Path, "vault-b");

        var first = await VaultExporter.ExportAsync(new VaultExportOptions(
            combinedPath,
            firstOut,
            SourceClaimCatalogPath: catalogPath,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));
        var second = await VaultExporter.ExportAsync(new VaultExportOptions(
            combinedPath,
            secondOut,
            SourceClaimCatalogPath: catalogPath,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));

        Assert.Equal("public-safe", first.Graph.Classification);
        Assert.Contains(first.Graph.Nodes, node => node.Kind == "source");
        Assert.Contains(first.Graph.Nodes, node => node.Kind == "endpoint");
        Assert.Contains(first.Graph.Nodes, node => node.Kind == "surface" && node.SurfaceKind == "sql-query");
        Assert.Contains(first.Graph.Edges, edge => edge.Kind == "surface-evidence");
        Assert.Contains(first.Graph.Inputs, input => input.SourceProvenance is { Count: > 0 });
        Assert.Contains(first.Graph.Nodes, node => node.Kind == "source" && !string.IsNullOrWhiteSpace(node.ScannerVersion) && !string.IsNullOrWhiteSpace(node.RepositoryIdentityHash));
        Assert.Contains(first.Graph.Nodes, node => node.Kind is "endpoint" or "surface" && node.EvidenceLocations is { Count: > 0 });
        Assert.Contains(first.Graph.Edges, edge => edge.EvidenceLocations is { Count: > 0 });
        Assert.True(VaultExporter.IsSelfConsistentGraphJson(await File.ReadAllTextAsync(Path.Combine(firstOut, "graph.json"))));
        Assert.True(VaultExporter.IsSelfConsistentMarkdown(await File.ReadAllTextAsync(Path.Combine(firstOut, "index.md"))));
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(firstOut, "graph.json")),
            await File.ReadAllTextAsync(Path.Combine(secondOut, "graph.json")));
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(firstOut, "index.md")),
            await File.ReadAllTextAsync(Path.Combine(secondOut, "index.md")));

        var allText = string.Join('\n', Directory.EnumerateFiles(firstOut, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("sourceProvenance", allText);
        Assert.Contains("evidenceLocations", allText);
        Assert.DoesNotContain(temp.Path, allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/Users/", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git@", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select *", allText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Vault_export_cli_supports_format_dry_run_and_no_writes()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(temp.Path);
        var outDir = Path.Combine(temp.Path, "dry-run-vault");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(
            [
                "vault", "export",
                "--combined-index", combinedPath,
                "--out", outDir,
                "--format", "markdown,json",
                "--dry-run"
            ],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("TraceMap vault export dry run:", output.ToString());
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task Vault_export_rejects_stale_generated_output_unless_forced()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(temp.Path);
        var outDir = Path.Combine(temp.Path, "vault");

        await VaultExporter.ExportAsync(new VaultExportOptions(combinedPath, outDir));
        var indexPath = Path.Combine(outDir, "index.md");
        await File.AppendAllTextAsync(indexPath, "\nmanual edit\n");

        var stale = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VaultExporter.ExportAsync(new VaultExportOptions(combinedPath, outDir)));
        Assert.Contains("GeneratedFileStale", stale.Message);

        await VaultExporter.ExportAsync(new VaultExportOptions(combinedPath, outDir, Force: true));
        Assert.True(VaultExporter.IsSelfConsistentMarkdown(await File.ReadAllTextAsync(indexPath)));
    }

    [Fact]
    public async Task Vault_export_refuses_non_generated_user_note_collision()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(temp.Path);
        var outDir = Path.Combine(temp.Path, "vault");
        Directory.CreateDirectory(outDir);
        await File.WriteAllTextAsync(Path.Combine(outDir, "README.md"), """
            # user note

            This note mentions tracemap_export_schema: "evidence-graph-vault-export.v1"
            while remaining user-authored content.
            """);

        var collision = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VaultExporter.ExportAsync(new VaultExportOptions(combinedPath, outDir, Force: true)));
        Assert.Contains("UserFileCollision", collision.Message);
    }

    [Fact]
    public async Task Vault_export_fails_when_every_report_input_is_incompatible()
    {
        using var temp = new TempDirectory();
        var badReport = Path.Combine(temp.Path, "bad-paths-report.json");
        await File.WriteAllTextAsync(badReport, "{\"schemaVersion\":\"not-paths-report\"}\n");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VaultExporter.ExportAsync(new VaultExportOptions(
                null,
                Path.Combine(temp.Path, "vault"),
                PathsReportPaths: [badReport])));

        Assert.Contains("InputSchemaUnsupported", failure.Message);
        Assert.DoesNotContain(badReport, failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Vault_export_applies_claim_catalog_to_report_only_paths_export()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(temp.Path);
        var sourceIds = await ReadSourceIdsAsync(combinedPath);
        var pathsDir = Path.Combine(temp.Path, "paths");
        await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            combinedPath,
            pathsDir,
            FromEndpoint: "GET /api/orders/{}",
            FromSource: "client",
            ToSurface: "sql-query"));
        var catalogPath = WriteClaimCatalog(temp.Path, sourceIds.Values, "public-safe");

        var result = await VaultExporter.ExportAsync(new VaultExportOptions(
            null,
            Path.Combine(temp.Path, "vault"),
            PathsReportPaths: [Path.Combine(pathsDir, "paths-report.json")],
            SourceClaimCatalogPath: catalogPath,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));

        Assert.Equal("public-safe", result.Graph.Classification);
        Assert.Contains(result.Graph.Nodes, node => node.Kind == "report" && node.ClaimLevel == "public-safe");
        Assert.Contains(result.Graph.Inputs, input => input.Kind == "paths-report" && input.SourceProvenance is { Count: 2 });
    }

    [Fact]
    public async Task Vault_export_uses_stable_input_identity_across_roots()
    {
        using var temp = new TempDirectory();
        var firstRoot = Path.Combine(temp.Path, "first-root");
        var secondRoot = Path.Combine(temp.Path, "second-root");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        var firstCombinedPath = await CreateCombinedIndexAsync(firstRoot);
        var secondCombinedPath = await CreateCombinedIndexAsync(secondRoot);
        var firstSourceIds = await ReadSourceIdsAsync(firstCombinedPath);
        var secondSourceIds = await ReadSourceIdsAsync(secondCombinedPath);
        var firstCatalogPath = WriteClaimCatalog(firstRoot, firstSourceIds.Values, "public-safe");
        var secondCatalogPath = WriteClaimCatalog(secondRoot, secondSourceIds.Values, "public-safe");
        var firstOut = Path.Combine(temp.Path, "first-vault");
        var secondOut = Path.Combine(temp.Path, "second-vault");

        await VaultExporter.ExportAsync(new VaultExportOptions(
            firstCombinedPath,
            firstOut,
            SourceClaimCatalogPath: firstCatalogPath,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));
        await VaultExporter.ExportAsync(new VaultExportOptions(
            secondCombinedPath,
            secondOut,
            SourceClaimCatalogPath: secondCatalogPath,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));

        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(firstOut, "graph.json")),
            await File.ReadAllTextAsync(Path.Combine(secondOut, "graph.json")));
    }

    [Fact]
    public async Task Vault_export_uses_stable_report_identity_across_roots()
    {
        using var temp = new TempDirectory();
        var firstRoot = Path.Combine(temp.Path, "first-root");
        var secondRoot = Path.Combine(temp.Path, "second-root");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        var firstCombinedPath = await CreateCombinedIndexAsync(firstRoot);
        var secondCombinedPath = await CreateCombinedIndexAsync(secondRoot);
        var firstSourceIds = await ReadSourceIdsAsync(firstCombinedPath);
        var secondSourceIds = await ReadSourceIdsAsync(secondCombinedPath);
        var firstPathsDir = Path.Combine(firstRoot, "paths");
        var secondPathsDir = Path.Combine(secondRoot, "paths");
        await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            firstCombinedPath,
            firstPathsDir,
            FromEndpoint: "GET /api/orders/{}",
            FromSource: "client",
            ToSurface: "sql-query"));
        await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            secondCombinedPath,
            secondPathsDir,
            FromEndpoint: "GET /api/orders/{}",
            FromSource: "client",
            ToSurface: "sql-query"));
        var firstCatalogPath = WriteClaimCatalog(firstRoot, firstSourceIds.Values, "public-safe");
        var secondCatalogPath = WriteClaimCatalog(secondRoot, secondSourceIds.Values, "public-safe");
        var firstOut = Path.Combine(temp.Path, "first-report-vault");
        var secondOut = Path.Combine(temp.Path, "second-report-vault");

        await VaultExporter.ExportAsync(new VaultExportOptions(
            null,
            firstOut,
            PathsReportPaths: [Path.Combine(firstPathsDir, "paths-report.json")],
            SourceClaimCatalogPath: firstCatalogPath,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));
        await VaultExporter.ExportAsync(new VaultExportOptions(
            null,
            secondOut,
            PathsReportPaths: [Path.Combine(secondPathsDir, "paths-report.json")],
            SourceClaimCatalogPath: secondCatalogPath,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));

        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(firstOut, "graph.json")),
            await File.ReadAllTextAsync(Path.Combine(secondOut, "graph.json")));
    }

    [Fact]
    public async Task Vault_export_applies_claim_catalog_to_report_only_reverse_export()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(temp.Path);
        var sourceIds = await ReadSourceIdsAsync(combinedPath);
        var reverseDir = Path.Combine(temp.Path, "reverse");
        await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            reverseDir,
            Format: "json",
            Surface: "sql-query",
            SurfaceName: "orders",
            To: "sources"));
        var catalogPath = WriteClaimCatalog(temp.Path, sourceIds.Values, "public-safe");

        var result = await VaultExporter.ExportAsync(new VaultExportOptions(
            null,
            Path.Combine(temp.Path, "vault"),
            ReverseReportPaths: [Path.Combine(reverseDir, "reverse-report.json")],
            SourceClaimCatalogPath: catalogPath,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));

        Assert.Equal("public-safe", result.Graph.Classification);
        Assert.Contains(result.Graph.Nodes, node => node.Kind == "report" && node.ClaimLevel == "public-safe");
        Assert.Contains(result.Graph.Inputs, input => input.Kind == "reverse-report" && input.SourceProvenance is { Count: 2 });
    }

    [Fact]
    public async Task Vault_export_filters_hidden_evidence_and_marks_output_partial()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(temp.Path);
        var sourceIds = await ReadSourceIdsAsync(combinedPath);
        var catalogPath = WriteClaimCatalog(temp.Path, [sourceIds["server"]], "public-safe");
        var outDir = Path.Combine(temp.Path, "vault");

        var result = await VaultExporter.ExportAsync(new VaultExportOptions(
            combinedPath,
            outDir,
            SourceClaimCatalogPath: catalogPath,
            MinimumClaimLevel: "public-safe",
            Date: "2026-06"));

        Assert.True(result.Graph.Settings.Partial);
        Assert.True(result.Graph.Settings.OmittedHiddenNodeCount > 0);
        Assert.Contains(result.Graph.Gaps, gap => gap.RuleId == "vault-export.gap.hidden-evidence-omitted.v1");
        Assert.DoesNotContain(result.Graph.Nodes, node => node.Kind != "rule" && node.DisplayName.Contains("client", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Graph.Nodes, node => node.Kind != "rule" && node.DisplayName.Contains("server", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Vault_export_public_filter_fails_when_no_visible_evidence_remains()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(temp.Path);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VaultExporter.ExportAsync(new VaultExportOptions(
                combinedPath,
                Path.Combine(temp.Path, "vault"),
                MinimumClaimLevel: "public-safe",
                Date: "2026-06")));

        Assert.Contains("NoVisibleEvidenceAfterFiltering", failure.Message);
    }

    [Fact]
    public async Task Vault_export_rejects_unsafe_public_values_without_echoing_them()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(temp.Path, unsafeEndpoint: true);
        var sourceIds = await ReadSourceIdsAsync(combinedPath);
        var catalogPath = WriteClaimCatalog(temp.Path, sourceIds.Values, "public-safe");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VaultExporter.ExportAsync(new VaultExportOptions(
                combinedPath,
                Path.Combine(temp.Path, "vault"),
                SourceClaimCatalogPath: catalogPath,
                MinimumClaimLevel: "public-safe",
                Date: "2026-06")));

        Assert.Contains("UnsafeValueRejected", failure.Message);
        Assert.DoesNotContain("private.example", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Vault_export_hidden_allows_safe_secret_like_evidence_locations()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(
            temp.Path,
            routeKey: "/api/token-review/{}",
            controller: "Server.TokenReviewController.GetSecretToken(System.Int32)",
            repository: "Server.TokenRepository.UpdateToken(System.Int32)",
            routeFile: "Controllers/TokenReviewController.cs",
            callFile: "Controllers/TokenReviewController.cs",
            queryFile: "Infrastructure/TokenRepository.cs");
        var firstOut = Path.Combine(temp.Path, "vault-a");
        var secondOut = Path.Combine(temp.Path, "vault-b");

        var first = await VaultExporter.ExportAsync(new VaultExportOptions(combinedPath, firstOut));
        await VaultExporter.ExportAsync(new VaultExportOptions(combinedPath, secondOut));

        Assert.Equal("hidden", first.Graph.Classification);
        Assert.Contains(first.Graph.Gaps, gap =>
            gap.RuleId == "vault-export.gap.hidden-safe-context-omitted.v1"
            && gap.EvidenceTier == "Tier4Unknown"
            && gap.Classification == "HiddenSafeContextAccepted");

        var graphJson = await File.ReadAllTextAsync(Path.Combine(firstOut, "graph.json"));
        var markdown = string.Join('\n', Directory.EnumerateFiles(firstOut, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));
        Assert.Contains("Controllers/TokenReviewController.cs", graphJson);
        Assert.Contains("Controllers/TokenReviewController.cs", markdown);
        Assert.Contains("Infrastructure/TokenRepository.cs", graphJson);
        Assert.DoesNotContain(temp.Path, graphJson, StringComparison.OrdinalIgnoreCase);
        Assert.True(VaultExporter.IsSelfConsistentGraphJson(graphJson));
        Assert.Equal(
            graphJson,
            await File.ReadAllTextAsync(Path.Combine(secondOut, "graph.json")));
    }

    [Theory]
    [InlineData("demo-safe")]
    [InlineData("public-safe")]
    public async Task Vault_export_public_and_demo_keep_rejecting_secret_like_safe_context_names(string claimLevel)
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(
            temp.Path,
            routeKey: "/api/token-review/{}",
            controller: "Server.TokenReviewController.GetSecretToken(System.Int32)",
            repository: "Server.TokenRepository.UpdateToken(System.Int32)",
            routeFile: "Controllers/TokenReviewController.cs",
            callFile: "Controllers/TokenReviewController.cs",
            queryFile: "Infrastructure/TokenRepository.cs");
        var sourceIds = await ReadSourceIdsAsync(combinedPath);
        var catalogPath = WriteClaimCatalog(temp.Path, sourceIds.Values, claimLevel);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VaultExporter.ExportAsync(new VaultExportOptions(
                combinedPath,
                Path.Combine(temp.Path, $"vault-{claimLevel}"),
                SourceClaimCatalogPath: catalogPath,
                MinimumClaimLevel: claimLevel,
                Date: "2026-06")));

        Assert.Contains("UnsafeValueRejected", failure.Message);
        Assert.DoesNotContain("TokenReviewController", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GetSecretToken", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Controllers/../TokenReviewController.cs", "local-path")]
    [InlineData("C:\\Temp\\TokenReviewController.cs", "local-path")]
    [InlineData("\\\\server\\share\\TokenReviewController.cs", "local-path")]
    [InlineData("~/TokenReviewController.cs", "local-path")]
    [InlineData("$HOME/TokenReviewController.cs", "local-path")]
    [InlineData("Controllers/Authorization: Bearer synthetic-token-value.cs", "credential")]
    [InlineData("Controllers/sk_test_abcdefghijklmnop.cs", "credential")]
    [InlineData("Controllers/Server=example;Database=sample;User ID=user;Password=value;.cs", "connection-string")]
    [InlineData("Controllers/select id from Orders.cs", "raw-sql")]
    [InlineData("https://example.invalid/TokenReviewController.cs", "raw-remote-or-url")]
    public async Task Vault_export_hidden_rejects_raw_unsafe_evidence_locations_without_echoing_values(string filePath, string category)
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(
            temp.Path,
            routeKey: "/api/orders/{}",
            routeFile: filePath);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VaultExporter.ExportAsync(new VaultExportOptions(combinedPath, Path.Combine(temp.Path, "vault"))));

        Assert.Contains("UnsafeValueRejected", failure.Message);
        Assert.Contains(category, failure.Message);
        Assert.DoesNotContain(filePath, failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Vault_export_graph_hash_detects_stale_manifest()
    {
        using var temp = new TempDirectory();
        var combinedPath = await CreateCombinedIndexAsync(temp.Path);
        var outDir = Path.Combine(temp.Path, "vault");

        await VaultExporter.ExportAsync(new VaultExportOptions(combinedPath, outDir, Format: "json"));
        var graphPath = Path.Combine(outDir, "graph.json");
        var graphJson = await File.ReadAllTextAsync(graphPath);
        Assert.True(VaultExporter.IsSelfConsistentGraphJson(graphJson));

        var staleJson = graphJson.Replace("\"classification\": \"hidden\"", "\"classification\": \"public-safe\"", StringComparison.Ordinal);
        Assert.False(VaultExporter.IsSelfConsistentGraphJson(staleJson));
        await File.WriteAllTextAsync(graphPath, staleJson);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VaultExporter.ExportAsync(new VaultExportOptions(combinedPath, outDir, Format: "json")));
        Assert.Contains("GeneratedFileStale", failure.Message);
    }

    private static async Task<string> CreateCombinedIndexAsync(
        string root,
        bool unsafeEndpoint = false,
        string? routeKey = null,
        string? controller = null,
        string? repository = null,
        string? routeFile = null,
        string? callFile = null,
        string? queryFile = null)
    {
        var clientIndex = Path.Combine(root, unsafeEndpoint ? "unsafe-client.sqlite" : "client.sqlite");
        var serverIndex = Path.Combine(root, unsafeEndpoint ? "unsafe-server.sqlite" : "server.sqlite");
        var combinedPath = Path.Combine(root, unsafeEndpoint ? "unsafe-combined.sqlite" : "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone15");
        var effectiveRouteKey = routeKey ?? (unsafeEndpoint ? "http://private.example/api/orders/{}" : "/api/orders/{}");
        var effectiveController = controller ?? "Server.OrdersController.Get(System.Int32)";
        var effectiveRepository = repository ?? "Server.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(clientIndex, client, [
            HttpClientFact(client, "GET", "/api/orders/{id}", effectiveRouteKey, "src/orders.ts", 5)
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "GET", "/api/orders/{id}", effectiveRouteKey, effectiveController, routeFile ?? "Controllers/OrdersController.cs", 10),
            CallFact(server, effectiveController, effectiveRepository, callFile ?? routeFile ?? "Controllers/OrdersController.cs", 14),
            QueryPatternFact(server, effectiveRepository, queryFile ?? "Infrastructure/OrderRepository.cs", 31)
        ]);
        IReadOnlyList<string> labels = unsafeEndpoint ? ["private.example", "server"] : ["client", "server"];
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, labels));
        return combinedPath;
    }

    private static string WriteClaimCatalog(string root, IEnumerable<string> sourceIndexIds, string claimLevel)
    {
        var path = Path.Combine(root, $"source-claims-{Guid.NewGuid():N}.json");
        var entries = sourceIndexIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => new Dictionary<string, string>
            {
                ["sourceIndexId"] = value,
                ["claimLevel"] = claimLevel,
                ["proofId"] = $"proof-{Hash(value, 8)}"
            })
            .ToArray();
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["schemaVersion"] = "source-claim-catalog.v1",
            ["sources"] = entries
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        File.WriteAllText(path, json);
        return path;
    }

    private static async Task<Dictionary<string, string>> ReadSourceIdsAsync(string combinedPath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var connection = new SqliteConnection($"Data Source={combinedPath};Mode=ReadOnly");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select label, source_index_id from index_sources order by label;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }

        return result;
    }

    private static ScanManifest Manifest(
        string repo,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded")
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
            [],
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
                ["operationName"] = "READ",
                ["tableName"] = "orders",
                ["columnNames"] = "id;status",
                ["sqlSourceKind"] = "literal-string",
                ["queryShapeHash"] = "shape123"
            });
    }

    private static string Hash(string value, int length)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return text[..length];
    }
}
