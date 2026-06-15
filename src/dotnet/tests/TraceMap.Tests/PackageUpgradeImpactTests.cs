using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class PackageUpgradeImpactTests
{
    [Fact]
    public async Task PackageImpact_reads_single_index_and_redacts_unsafe_delta_versions()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var outDir = Path.Combine(temp.Path, "package-impact");
        var deltaPath = Path.Combine(temp.Path, "package-delta.json");
        var manifest = Manifest("api", "tracemap-milestone15");
        SqliteIndexWriter.Write(indexPath, manifest, [
            PackageFact(manifest, "Newtonsoft.Json", "nuget", "Api.csproj", "PackageReference", "13.0.1")
        ]);
        await File.WriteAllTextAsync(deltaPath, """
            {
              "version": "package-delta.v1",
              "changes": [
                {
                  "id": "pkg-newtonsoft",
                  "packageName": "Newtonsoft.Json",
                  "ecosystem": "nuget",
                  "changeType": "updated",
                  "oldVersion": "13.0.1",
                  "newVersion": "git+https://token@example.invalid/private/package.git"
                }
              ]
            }
            """);

        var result = await PackageUpgradeImpactReporter.WriteAsync(new PackageImpactOptions(indexPath, deltaPath, outDir));

        Assert.Equal("FullEvidenceAvailable", result.Report.ReportCoverage);
        var finding = Assert.Single(result.Report.Findings);
        Assert.Equal("StaticPackageEvidence", finding.Classification);
        Assert.Equal("package.upgrade.impact.v1", finding.RuleId);
        Assert.Equal(RuleIds.ProjectFile, finding.EvidenceRuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, finding.EvidenceTier);
        Assert.Equal("default", finding.SourceLabel);
        Assert.Equal("13.0.1", finding.RequestedOldVersion);
        Assert.Null(finding.RequestedNewVersion);
        Assert.StartsWith("version-hash:", finding.RequestedNewVersionHash);

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "package-impact-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "package-impact-report.json"));
        Assert.Contains("TraceMap Package Impact Report", markdown);
        Assert.Contains("package.upgrade.impact.v1", markdown);
        Assert.DoesNotContain("token@example", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token@example", json, StringComparison.OrdinalIgnoreCase);

        var document = JsonSerializer.Deserialize<PackageImpactDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(document);
        Assert.Equal(result.Report.Findings.Count, document!.Findings.Count);
    }

    [Fact]
    public async Task PackageImpact_reads_combined_index_and_filters_by_ecosystem()
    {
        using var temp = new TempDirectory();
        var dotnetIndex = Path.Combine(temp.Path, "dotnet.sqlite");
        var typescriptIndex = Path.Combine(temp.Path, "typescript.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var deltaPath = Path.Combine(temp.Path, "delta.json");
        var outPath = Path.Combine(temp.Path, "package-impact.json");
        var dotnetManifest = Manifest("api", "tracemap-milestone15");
        var typescriptManifest = Manifest("web", "tracemap-typescript/0.1.0");
        SqliteIndexWriter.Write(dotnetIndex, dotnetManifest, [
            PackageFact(dotnetManifest, "logging", "nuget", "Api.csproj", "PackageReference", "1.0.0")
        ]);
        SqliteIndexWriter.Write(typescriptIndex, typescriptManifest, [
            PackageFact(typescriptManifest, "logging", "npm", "package.json", "dependencies", "1.0.0")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([dotnetIndex, typescriptIndex], combinedPath, ["api", "web"]));
        await File.WriteAllTextAsync(deltaPath, """
            {
              "version": "package-delta.v1",
              "changes": [
                { "id": "pkg-logging", "packageName": "logging", "ecosystem": "npm", "changeType": "updated", "oldVersion": "1.0.0", "newVersion": "2.0.0" }
              ]
            }
            """);

        var result = await PackageUpgradeImpactReporter.WriteAsync(new PackageImpactOptions(combinedPath, deltaPath, outPath, Format: "json", Ecosystem: "npm"));

        var finding = Assert.Single(result.Report.Findings);
        Assert.Equal("web", finding.SourceLabel);
        Assert.Equal("npm", finding.Ecosystem);
        Assert.Equal("package.json", finding.FilePath);
        Assert.Null(result.MarkdownPath);
        Assert.True(File.Exists(outPath));
    }

    [Fact]
    public async Task PackageImpact_no_match_under_reduced_coverage_is_gap_not_clean_absence()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var deltaPath = Path.Combine(temp.Path, "delta.json");
        var outDir = Path.Combine(temp.Path, "package-impact");
        var manifest = Manifest(
            "api",
            "tracemap-milestone15",
            analysisLevel: "Level1SemanticAnalysisReduced",
            buildStatus: "FailedOrPartial",
            knownGaps: ["project load failed"]);
        SqliteIndexWriter.Write(indexPath, manifest, []);
        await File.WriteAllTextAsync(deltaPath, """
            {
              "version": "package-delta.v1",
              "changes": [
                { "id": "pkg-missing", "packageName": "Missing.Package", "ecosystem": "nuget", "changeType": "updated" }
              ]
            }
            """);

        var result = await PackageUpgradeImpactReporter.WriteAsync(new PackageImpactOptions(indexPath, deltaPath, outDir));

        Assert.Empty(result.Report.Findings);
        Assert.Equal("ReducedCoverage", result.Report.ReportCoverage);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "UnknownAnalysisGap" && gap.ChangeId == "pkg-missing");
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "ReducedCoverage" && gap.Message.Contains("project load failed", StringComparison.Ordinal));

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "package-impact-report.md"));
        Assert.Contains("UnknownAnalysisGap", markdown);
        Assert.DoesNotContain("NoImpact", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PackageImpact_does_not_match_missing_ecosystem_when_delta_requires_one()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        var deltaPath = Path.Combine(temp.Path, "delta.json");
        var outDir = Path.Combine(temp.Path, "package-impact");
        var manifest = Manifest("api", "tracemap-milestone15");
        SqliteIndexWriter.Write(indexPath, manifest, [
            PackageFact(manifest, "Ambiguous.Package", "", "Api.csproj", "PackageReference", "1.0.0")
        ]);
        await File.WriteAllTextAsync(deltaPath, """
            {
              "version": "package-delta.v1",
              "changes": [
                { "id": "pkg-ambiguous", "packageName": "Ambiguous.Package", "ecosystem": "nuget", "changeType": "updated" }
              ]
            }
            """);

        var result = await PackageUpgradeImpactReporter.WriteAsync(new PackageImpactOptions(indexPath, deltaPath, outDir));

        Assert.Empty(result.Report.Findings);
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "NoStaticPackageEvidence" && gap.ChangeId == "pkg-ambiguous");
    }

    [Fact]
    public async Task Cli_validates_required_package_delta()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["package-impact", "--index", "index.sqlite", "--out", "report.md"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("package-impact requires --package-delta", error.ToString());
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
            "0123456789abcdef0123456789abcdef01234567",
            scannerVersion,
            DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            analysisLevel,
            buildStatus,
            [],
            [],
            [],
            knownGaps ?? []);
    }

    private static CodeFact PackageFact(ScanManifest manifest, string name, string ecosystem, string file, string group, string version)
    {
        return new CodeFact(
            $"pkg-{manifest.RepoName}-{name}-{ecosystem}",
            manifest.ScanId,
            manifest.RepoName,
            manifest.CommitSha,
            null,
            FactTypes.PackageReferenced,
            RuleIds.ProjectFile,
            EvidenceTiers.Tier2Structural,
            null,
            name,
            "PackageManifest",
            new EvidenceSpan(file, 5, 5, null, "TestExtractor", "1.0.0"),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["dependencyGroup"] = group,
                ["ecosystem"] = ecosystem,
                ["manifestKind"] = file.EndsWith(".json", StringComparison.Ordinal) ? "package.json" : "csproj",
                ["packageName"] = name,
                ["packageManager"] = ecosystem,
                ["sourceKind"] = "manifest",
                ["surfaceKind"] = "package-config",
                ["version"] = version
            });
    }
}
