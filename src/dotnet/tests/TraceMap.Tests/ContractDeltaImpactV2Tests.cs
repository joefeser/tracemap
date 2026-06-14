using System.Globalization;
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
              "id": "chg-package-surface-json",
              "kind": "dependency-surface",
              "changeType": "changed",
              "reference": {
                "surfaceKind": "package-config",
                "packageName": "Newtonsoft.Json",
                "ecosystem": "nuget"
              }
            },
            {
              "id": "chg-package-surface-name-json",
              "kind": "dependency-surface",
              "changeType": "changed",
              "reference": {
                "surfaceKind": "package-config",
                "surfaceName": "Newtonsoft.Json",
                "ecosystem": "nuget"
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
                    ["manifestKind"] = "csproj",
                    ["packageName"] = "Newtonsoft.Json",
                    ["surfaceKind"] = "package-config"
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
        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.json"));
        Assert.Contains("### `GET /api/orders`", markdown);
        Assert.Contains("### `nuget:Newtonsoft.Json`", markdown);
        Assert.Contains("### `package-config:Newtonsoft.Json`", markdown);
        Assert.Contains("\"changeId\": \"chg-package-surface-json\"", json);
        Assert.Contains("\"changeId\": \"chg-package-surface-name-json\"", json);
        Assert.Contains("### `Orders`", markdown);
        Assert.Contains("### `sql:Orders`", markdown);
        Assert.Contains("Classification: `ProbableImpact`", markdown);
    }

    [Fact]
    public async Task Reduce_v2_dependency_surface_selector_honors_ecosystem()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteV2Delta(temp.Path, """
            {
              "id": "chg-shared-package-name",
              "kind": "dependency-surface",
              "changeType": "changed",
              "reference": {
                "surfaceKind": "package-config",
                "packageName": "logging",
                "ecosystem": "nuget"
              }
            }
            """);
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.PackageReferenced,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/App.csproj", 5, 5, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ecosystem"] = "nuget",
                    ["packageName"] = "logging",
                    ["surfaceKind"] = "package-config"
                }),
            FactFactory.Create(
                manifest,
                FactTypes.PackageReferenced,
                "typescript.package-json.v1",
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("web/package.json", 8, 8, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ecosystem"] = "npm",
                    ["packageName"] = "logging",
                    ["surfaceKind"] = "package-config"
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outputPath
        ]));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.json"));
        Assert.Contains("src/App.csproj", markdown);
        Assert.DoesNotContain("web/package.json", markdown);
        Assert.Contains("\"ecosystem\": \"nuget\"", json);
        Assert.DoesNotContain("\"ecosystem\": \"npm\"", json);
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

        using var sourceOutput = new StringWriter();
        using var sourceError = new StringWriter();
        exitCode = await TraceMapCommand.RunAsync([
            "reduce",
            "--index", indexPath,
            "--contract-delta", goodDeltaPath,
            "--out", outputPath,
            "--source", "api"
        ], sourceOutput, sourceError);

        Assert.Equal(1, exitCode);
        Assert.Contains("reduce --source requires a combined TraceMap index", sourceError.ToString());
    }

    [Fact]
    public async Task Reduce_v2_rejects_package_surface_version_only_selector()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, []);
        var badDeltaPath = WriteV2Delta(temp.Path, """
            {
              "id": "chg-package-version-only",
              "kind": "dependency-surface",
              "changeType": "changed",
              "reference": {
                "surfaceKind": "package-config",
                "oldVersion": "1.0.0",
                "newVersion": "2.0.0"
              }
            }
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "reduce",
            "--index", indexPath,
            "--contract-delta", badDeltaPath,
            "--out", outputPath
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("reference is missing required identity fields", error.ToString());
    }

    [Fact]
    public async Task Reduce_v2_rejects_dependency_surface_without_surface_kind()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, []);
        var badDeltaPath = WriteV2Delta(temp.Path, """
            {
              "id": "chg-surface-name-only",
              "kind": "dependency-surface",
              "changeType": "changed",
              "reference": {
                "surfaceName": "Orders"
              }
            }
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "reduce",
            "--index", indexPath,
            "--contract-delta", badDeltaPath,
            "--out", outputPath
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("reference is missing required identity fields", error.ToString());
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

    [Fact]
    public async Task Reduce_v2_unrelated_analysis_gap_does_not_override_no_evidence()
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
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.CSharpSemanticWorkspace,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan("src/Other.cs", 1, 1, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["message"] = "Compilation failed near SomeOtherType."
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outputPath
        ]));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.md"));
        Assert.Contains("Classification: `NoEvidenceFullCoverage`", markdown);
        Assert.DoesNotContain("Classification: `UnknownAnalysisGap`", markdown);
    }

    [Fact]
    public async Task Reduce_v2_type_constrained_member_mismatch_is_review_only()
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
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.PropertyAccessed,
                RuleIds.CSharpSemanticPropertyAccess,
                EvidenceTiers.Tier1Semantic,
                new EvidenceSpan("src/OrderReader.cs", 8, 8, null, "test", "test/1.0"),
                targetSymbol: "global::Sample.Order.PrimaryEmail",
                contractElement: "PrimaryEmail",
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["containingType"] = "global::Sample.Order",
                    ["propertyName"] = "PrimaryEmail"
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outputPath
        ]));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.md"));
        Assert.Contains("Classification: `NeedsReview`", markdown);
        Assert.DoesNotContain("Classification: `DefiniteImpact`", markdown);
    }

    [Fact]
    public async Task Reduce_v2_signature_only_method_matches_exact_signature()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteV2Delta(temp.Path, """
            {
              "id": "chg-method-send",
              "kind": "method",
              "changeType": "signature_changed",
              "reference": {
                "signature": "Send(Order)"
              }
            }
            """);
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.MethodInvoked,
                RuleIds.CSharpSemanticMethodInvocation,
                EvidenceTiers.Tier1Semantic,
                new EvidenceSpan("src/Controller.cs", 20, 20, null, "test", "test/1.0"),
                targetSymbol: "global::Sample.OrderSender.Send(global::Sample.Order order)",
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["signature"] = "Send(Order)"
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outputPath
        ]));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.md"));
        Assert.Contains("Classification: `DefiniteImpact`", markdown);
        Assert.Contains("`MethodInvoked`", markdown);
    }

    [Fact]
    public async Task Reduce_v2_endpoint_matches_method_prefixed_normalized_path_key()
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
            }
            """);
        var manifest = Manifest("api", "tracemap-jvm/0.1.0");
        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.HttpRouteBinding,
                "jvm.integration.http.route.v1",
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan("src/main/java/OrdersController.java", 20, 20, null, "test", "test/1.0"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["normalizedPathKey"] = "GET /api/orders"
                })
        ]);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outputPath
        ]));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.md"));
        Assert.Contains("Classification: `ProbableImpact`", markdown);
        Assert.Contains("`HttpRouteBinding`", markdown);
    }

    [Fact]
    public async Task Reduce_v2_markdown_includes_scanner_version_and_escapes_backticks()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = Path.Combine(temp.Path, "contract-delta.json");
        File.WriteAllText(deltaPath, """
            {
              "version": "contract-delta-v2",
              "contract": "Customer`Profile",
              "source": {
                "label": "api"
              },
              "changes": [
                {
                  "id": "chg`property",
                  "kind": "property",
                  "changeType": "removed",
                  "reference": {
                    "propertyName": "PrimaryEmail"
                  }
                }
              ]
            }
            """);
        var manifest = Manifest("api", ScannerVersions.TraceMap);
        SqliteIndexWriter.Write(indexPath, manifest, []);

        Assert.Equal(0, await RunCliAsync([
            "reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outputPath
        ]));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outputPath, "impact-report.md"));
        Assert.Contains("Scanner version: `tracemap-milestone15`", markdown);
        Assert.Contains("Contract: `Customer\\`Profile`", markdown);
        Assert.Contains("Change id: `chg\\`property`", markdown);
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
            DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            analysisLevel,
            buildStatus,
            [],
            ["src/Sample.csproj"],
            ["net10.0"],
            []);
    }
}
