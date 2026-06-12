using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ReducerTests
{
    [Fact]
    public async Task Reduce_removed_property_with_semantic_property_access_reports_definite_impact()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteDelta(temp.Path, "CustomerProfile.primaryEmail");
        var manifest = CreateManifest("Level1SemanticAnalysis", "Succeeded");
        var fact = FactFactory.Create(
            manifest,
            FactTypes.PropertyAccessed,
            RuleIds.CSharpSemanticPropertyAccess,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan("src/ProfileReader.cs", 8, 8, null, "test", "test/1.0"),
            sourceSymbol: "global::Sample.ProfileReader.Read()",
            targetSymbol: "global::Sample.CustomerProfile.PrimaryEmail",
            contractElement: "PrimaryEmail",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["containingType"] = "global::Sample.CustomerProfile",
                ["propertyName"] = "PrimaryEmail"
            });
        SqliteIndexWriter.Write(indexPath, manifest, [fact]);

        var report = await RunReduceAsync(indexPath, deltaPath, outputPath);

        Assert.Contains("Classification: `DefiniteImpact`", report);
        Assert.Contains("`src/ProfileReader.cs:8-8`", report);
        Assert.Contains("`csharp.semantic.propertyaccess.v1`", report);
    }

    [Fact]
    public async Task Reduce_removed_property_with_syntax_only_match_reports_needs_review()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteDelta(temp.Path, "CustomerProfile.primaryEmail");
        var manifest = CreateManifest("Level3SyntaxAnalysis", "NotRun");
        var fact = FactFactory.Create(
            manifest,
            FactTypes.MemberAccessName,
            RuleIds.CSharpSyntaxMemberAccess,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan("src/ProfileReader.cs", 8, 8, null, "test", "test/1.0"),
            sourceSymbol: "profile",
            targetSymbol: "PrimaryEmail",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["memberName"] = "PrimaryEmail"
            });
        SqliteIndexWriter.Write(indexPath, manifest, [fact]);

        var report = await RunReduceAsync(indexPath, deltaPath, outputPath);

        Assert.Contains("Classification: `NeedsReview`", report);
        Assert.Contains("syntax-only", report);
    }

    [Fact]
    public async Task Reduce_no_match_with_full_semantic_coverage_reports_no_evidence_full_coverage()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteDelta(temp.Path, "CustomerProfile.primaryEmail");
        var manifest = CreateManifest("Level1SemanticAnalysis", "Succeeded");
        SqliteIndexWriter.Write(indexPath, manifest, []);

        var report = await RunReduceAsync(indexPath, deltaPath, outputPath);

        Assert.Contains("Classification: `NoEvidenceFullCoverage`", report);
        Assert.Contains("Manifest coverage evidence", report);
    }

    [Fact]
    public async Task Reduce_no_match_with_reduced_coverage_reports_no_evidence_reduced_coverage()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "out");
        var deltaPath = WriteDelta(temp.Path, "CustomerProfile.primaryEmail");
        var manifest = CreateManifest("Level1SemanticAnalysisReduced", "FailedOrPartial", ["Project load failed."]);
        SqliteIndexWriter.Write(indexPath, manifest, []);

        var report = await RunReduceAsync(indexPath, deltaPath, outputPath);

        Assert.Contains("Classification: `NoEvidenceReducedCoverage`", report);
        Assert.Contains("reduced or syntax-only coverage", report);
    }

    private static async Task<string> RunReduceAsync(string indexPath, string deltaPath, string outputPath)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(
            ["reduce", "--index", indexPath, "--contract-delta", deltaPath, "--out", outputPath],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var reportPath = Path.Combine(outputPath, "impact-report.md");
        Assert.True(File.Exists(reportPath));
        return await File.ReadAllTextAsync(reportPath);
    }

    private static string WriteDelta(string directory, string element)
    {
        var path = Path.Combine(directory, "contract-delta.json");
        File.WriteAllText(path, $$"""
            {
              "contract": "CustomerProfile",
              "source": "contracts/customer-profile.json",
              "changes": [
                {
                  "element": "{{element}}",
                  "changeType": "removed",
                  "oldType": "string",
                  "newType": null
                }
              ]
            }
            """);
        return path;
    }

    private static ScanManifest CreateManifest(string analysisLevel, string buildStatus, IReadOnlyList<string>? knownGaps = null)
    {
        return new ScanManifest(
            "scan-test",
            "sample",
            "https://example.test/sample.git",
            "main",
            "abc123",
            ScannerVersions.TraceMap,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            analysisLevel,
            buildStatus,
            [],
            ["src/Sample.csproj"],
            ["net10.0"],
            knownGaps ?? []);
    }
}
