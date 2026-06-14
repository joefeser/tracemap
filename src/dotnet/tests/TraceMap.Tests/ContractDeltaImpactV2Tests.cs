using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ContractDeltaImpactV2Tests
{
    [Fact]
    public async Task Reduce_v2_property_writes_markdown_json_and_redacts_unsafe_values()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteV2Delta(temp.Path, """
            {
              "id": "chg-property-primary-email",
              "kind": "property",
              "changeType": "removed",
              "reference": {
                "typeName": "CustomerProfile",
                "propertyName": "PrimaryEmail"
              }
            }
            """);
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        var fact = FactFactory.Create(
            manifest,
            FactTypes.PropertyAccessed,
            RuleIds.CSharpSemanticPropertyAccess,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan("/private/sample/ProfileReader.cs", 8, 8, null, "test", "test/1.0"),
            sourceSymbol: "global::Sample.ProfileReader.Read()",
            targetSymbol: "global::Sample.CustomerProfile.PrimaryEmail",
            contractElement: "PrimaryEmail",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["containingType"] = "global::Sample.CustomerProfile",
                ["propertyName"] = "PrimaryEmail",
                ["rawValue"] = "Server=db;Password=secret;"
            });
        SqliteIndexWriter.Write(indexPath, manifest, [fact]);

        var exitCode = await RunCliAsync([
            "reduce",
            "--index", indexPath,
            "--contract-delta", deltaPath,
            "--out", outputPath,
            "--format", "json"
        ]);

        Assert.Equal(0, exitCode);
        var markdown = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.json"));
        Assert.Contains("Report type: `contract-delta-impact-single`", markdown);
        Assert.Contains("Input compatibility: `ContractDeltaV2`", markdown);
        Assert.Contains("Classification: `DefiniteImpact`", markdown);
        Assert.Contains("\"reportType\": \"contract-delta-impact-single\"", json);
        Assert.Contains("\"changeId\": \"chg-property-primary-email\"", json);
        Assert.Contains("path-hash:", json);
        Assert.DoesNotContain("/private/sample/ProfileReader.cs", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://private.example.invalid", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reduce_v2_matches_endpoint_package_sql_and_surface_evidence()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteV2Delta(temp.Path, """
            {
              "id": "chg-endpoint-orders",
              "kind": "endpoint",
              "changeType": "changed",
              "reference": {
                "method": "GET",
                "path": "/api/orders"
              }
            },
            {
              "id": "chg-package-json",
              "kind": "package",
              "changeType": "removed",
              "reference": {
                "ecosystem": "nuget",
                "packageName": "Newtonsoft.Json"
              }
            },
            {
              "id": "chg-sql-orders",
              "kind": "sql-table",
              "changeType": "changed",
              "reference": {
                "tableName": "Orders"
              }
            },
            {
              "id": "chg-surface-orders",
              "kind": "dependency-surface",
              "changeType": "changed",
              "reference": {
                "surfaceKind": "sql",
                "surfaceName": "Orders"
              }
            }
            """);
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.HttpRouteBinding,
                RuleIds.CSharpSyntaxAspNetRoute,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("Controllers/OrdersController.cs", 10, 10, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["httpMethod"] = "GET",
                    ["path"] = "/api/orders"
                }),
            FactFactory.Create(
                manifest,
                FactTypes.PackageReferenced,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("Api.csproj", 5, 5, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ecosystem"] = "nuget",
                    ["packageName"] = "Newtonsoft.Json"
                }),
            FactFactory.Create(
                manifest,
                FactTypes.QueryPatternDetected,
                RuleIds.DatabaseSqlShape,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("Infrastructure/OrderRepository.cs", 30, 30, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["operationName"] = "select",
                    ["tableName"] = "Orders",
                    ["queryShapeHash"] = "abc123"
                })
        ]);

        var exitCode = await RunCliAsync([
            "reduce",
            "--index", indexPath,
            "--contract-delta", deltaPath,
            "--out", outputPath
        ]);

        Assert.Equal(0, exitCode);
        var markdown = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.md"));
        Assert.Contains("### `GET /api/orders`", markdown);
        Assert.Contains("### `nuget:Newtonsoft.Json`", markdown);
        Assert.Contains("### `Orders`", markdown);
        Assert.Contains("### `sql:Orders`", markdown);
        Assert.Contains("Classification: `ProbableImpact`", markdown);
    }

    [Fact]
    public async Task Reduce_v2_combined_index_reports_combined_classification_and_source()
    {
        using var temp = new TempDirectory();
        var apiIndex = Path.Combine(temp.Path, "api.sqlite");
        var webIndex = Path.Combine(temp.Path, "web.sqlite");
        var combinedIndex = Path.Combine(temp.Path, "combined.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
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
        var apiManifest = Manifest("api", ScannerVersions.TraceMap);
        var webManifest = Manifest("web", "tracemap-typescript/0.1.0");
        SqliteIndexWriter.Write(apiIndex, apiManifest, [
            FactFactory.Create(
                apiManifest,
                FactTypes.PackageReferenced,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("Api.csproj", 5, 5, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ecosystem"] = "nuget",
                    ["packageName"] = "Newtonsoft.Json"
                })
        ]);
        SqliteIndexWriter.Write(webIndex, webManifest, []);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([apiIndex, webIndex], combinedIndex, ["api", "web"]));

        var exitCode = await RunCliAsync([
            "reduce",
            "--index", combinedIndex,
            "--contract-delta", deltaPath,
            "--out", outputPath,
            "--source", "api",
            "--include-paths",
            "--include-reverse"
        ]);

        Assert.Equal(0, exitCode);
        var markdown = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.json"));
        Assert.Contains("Report type: `contract-delta-impact-combined`", markdown);
        Assert.Contains("Classification: `ProbableStaticImpact`", markdown);
        Assert.Contains("PathContextUnavailable", markdown);
        Assert.Contains("ReverseContextUnavailable", markdown);
        Assert.Contains("\"sourceLabel\": \"api\"", json);
    }

    [Fact]
    public async Task Reduce_v2_rejects_unknown_kind_and_single_index_context()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, []);
        var badDeltaPath = WriteV2Delta(temp.Path, """
            {
              "id": "chg-bad",
              "kind": "secret-url",
              "changeType": "removed",
              "reference": {
                "name": "https://private.example.invalid/secret"
              }
            }
            """);

        using var badOutput = new StringWriter();
        using var badError = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "reduce",
            "--index", indexPath,
            "--contract-delta", badDeltaPath,
            "--out", outputPath
        ], badOutput, badError);

        Assert.Equal(1, exitCode);
        Assert.Contains("unsupported change kind", badError.ToString());
        Assert.DoesNotContain("private.example", badError.ToString(), StringComparison.OrdinalIgnoreCase);

        var goodDeltaPath = WriteV2Delta(temp.Path, """
            {
              "id": "chg-property-primary-email",
              "kind": "property",
              "changeType": "removed",
              "reference": {
                "propertyName": "PrimaryEmail"
              }
            }
            """);
        using var contextOutput = new StringWriter();
        using var contextError = new StringWriter();
        exitCode = await TraceMapCommand.RunAsync([
            "reduce",
            "--index", indexPath,
            "--contract-delta", goodDeltaPath,
            "--out", outputPath,
            "--include-paths"
        ], contextOutput, contextError);

        Assert.Equal(1, exitCode);
        Assert.Contains("require a combined TraceMap index", contextError.ToString());
    }

    [Fact]
    public async Task Reduce_v2_exit_code_is_opt_in_and_json_is_deterministic()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outOne = Path.Combine(temp.Path, "out-one");
        var outTwo = Path.Combine(temp.Path, "out-two");
        var outExit = Path.Combine(temp.Path, "out-exit");
        var deltaPath = WriteV2Delta(temp.Path, """
            {
              "id": "chg-property-primary-email",
              "kind": "property",
              "changeType": "removed",
              "reference": {
                "typeName": "CustomerProfile",
                "propertyName": "PrimaryEmail"
              }
            }
            """);
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.PropertyAccessed,
                RuleIds.CSharpSemanticPropertyAccess,
                EvidenceTiers.Tier1Semantic,
                new EvidenceSpan("src/ProfileReader.cs", 8, 8, null, "test", "test/1.0"),
                targetSymbol: "global::Sample.CustomerProfile.PrimaryEmail",
                contractElement: "PrimaryEmail",
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["containingType"] = "global::Sample.CustomerProfile",
                    ["propertyName"] = "PrimaryEmail"
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outOne
        ]));
        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outTwo
        ]));
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(outOne, "impact-report.json")),
            await File.ReadAllTextAsync(Path.Combine(outTwo, "impact-report.json")));
        Assert.Equal(1, await RunCliAsync([
            "reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outExit, "--exit-code"
        ]));
    }

    private static async Task<int> RunCliAsync(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(args, output, error);
        if (exitCode != 0 && error.ToString().Length > 0)
        {
            return exitCode;
        }

        Assert.Equal(string.Empty, error.ToString());
        return exitCode;
    }

    private static string WriteV2Delta(string directory, string changesJson)
    {
        var path = Path.Combine(directory, $"contract-delta-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, $$"""
            {
              "version": "contract-delta-v2",
              "contract": "CustomerProfile",
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

    private static ScanManifest Manifest(
        string repoName,
        string scannerVersion,
        string analysisLevel = "Level1SemanticAnalysis",
        string buildStatus = "Succeeded")
    {
        return new ScanManifest(
            $"scan-{repoName}",
            repoName,
            "https://example.invalid/repo.git",
            "main",
            "abc123",
            scannerVersion,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            analysisLevel,
            buildStatus,
            [],
            ["src/Sample.csproj"],
            ["net10.0"],
            []);
    }
}
