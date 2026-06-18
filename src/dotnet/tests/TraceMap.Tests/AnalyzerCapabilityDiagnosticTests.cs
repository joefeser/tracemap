using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class AnalyzerCapabilityDiagnosticTests
{
    [Fact]
    public void Legacy_scan_emits_closed_capability_diagnostics_without_private_values()
    {
        using var temp = new TempDirectory();
        var repo = CreateLegacyRepo(temp.Path);

        var first = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-one")));
        var second = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-two")));
        var capabilities = first.Facts
            .Where(fact => fact.FactType == FactTypes.AnalyzerCapabilityDiagnostic)
            .ToArray();

        Assert.NotEmpty(capabilities);
        Assert.All(capabilities, fact => Assert.True(AnalyzerCapabilityDiagnosticExtractor.IsClosedCapabilityFact(fact), fact.FactId));
        Assert.All(capabilities, fact => Assert.NotEqual(EvidenceTiers.Tier1Semantic, fact.EvidenceTier));
        Assert.All(capabilities, fact =>
        {
            var sourceScope = fact.Properties.GetValueOrDefault("sourceScope");
            Assert.False(Path.IsPathRooted(sourceScope ?? string.Empty), sourceScope);
        });

        AssertCapability(capabilities, AnalyzerCapabilityDiagnosticExtractor.Codes.LegacyProjectConfigInspection, "available", "config-only");
        AssertCapability(capabilities, AnalyzerCapabilityDiagnosticExtractor.Codes.LegacyFrameworkSignalDetected, "available", "informational");
        AssertCapability(capabilities, AnalyzerCapabilityDiagnosticExtractor.Codes.LegacyMSBuildToolsetSignalDetected, "available", "informational");
        AssertCapability(capabilities, AnalyzerCapabilityDiagnosticExtractor.Codes.LegacyNuGetRestoreAwareness, "not-requested", "informational");
        AssertCapability(capabilities, AnalyzerCapabilityDiagnosticExtractor.Codes.GeneratedDesignerLinkage, "unknown", "unknown-gap");
        AssertCapability(capabilities, AnalyzerCapabilityDiagnosticExtractor.Codes.DownstreamNoEvidenceCoverage, "reduced", "unknown-gap");
        Assert.Single(capabilities, fact =>
            fact.Properties.GetValueOrDefault("capabilityCode") == AnalyzerCapabilityDiagnosticExtractor.Codes.DownstreamNoEvidenceCoverage);

        var report = MarkdownReportWriter.Build(first);
        Assert.Contains("## Analyzer Capability Diagnostics", report);
        Assert.Contains("no-evidence", report);
        Assert.Contains("coverage-relative", report);

        var stableFirst = StableCapabilityFacts(first);
        var stableSecond = StableCapabilityFacts(second);
        Assert.Equal(stableFirst, stableSecond);
    }

    [Fact]
    public void Clean_sdk_semantic_success_keeps_capability_report_quiet()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Modern.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Handler.cs"), "namespace Modern; public sealed class Handler { public void Run() { } }");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var capabilities = result.Facts.Where(fact => fact.FactType == FactTypes.AnalyzerCapabilityDiagnostic).ToArray();

        Assert.Contains(capabilities, fact =>
            fact.Properties.GetValueOrDefault("capabilityCode") == AnalyzerCapabilityDiagnosticExtractor.Codes.CSharpSemanticCompilation
            && fact.Properties.GetValueOrDefault("capabilityState") is "available" or "reduced" or "unknown");
        Assert.DoesNotContain(capabilities, fact =>
            fact.Properties.GetValueOrDefault("capabilityCode") == AnalyzerCapabilityDiagnosticExtractor.Codes.LegacyProjectConfigInspection);
        Assert.DoesNotContain(capabilities, fact =>
            fact.Properties.GetValueOrDefault("capabilityCode") == AnalyzerCapabilityDiagnosticExtractor.Codes.LegacyMSBuildToolsetSignalDetected);
        Assert.DoesNotContain(MarkdownReportWriter.Build(result), "## Analyzer Capability Diagnostics");
    }

    [Fact]
    public void Failed_partial_semantic_state_emits_one_downstream_coverage_gap()
    {
        var manifest = Manifest("Level1SemanticAnalysisReduced", "FailedOrPartial");
        var inventory = new[] { new FileInventoryItem("src/Broken/Broken.csproj", "Project", 1) };
        var semantic = new SemanticExtractionResult([], [], Attempted: true, ReducedCoverage: true);

        var capabilities = AnalyzerCapabilityDiagnosticExtractor.Extract(
            manifest,
            inventory,
            semantic,
            [],
            new ScanOptions("repo", "out"));

        AssertCapability(capabilities, AnalyzerCapabilityDiagnosticExtractor.Codes.CSharpSemanticCompilation, "reduced", "reduced-semantic");
        var downstream = Assert.Single(capabilities, fact =>
            fact.Properties.GetValueOrDefault("capabilityCode") == AnalyzerCapabilityDiagnosticExtractor.Codes.DownstreamNoEvidenceCoverage);
        Assert.Equal(EvidenceTiers.Tier4Unknown, downstream.EvidenceTier);
        Assert.Equal("reduced", downstream.Properties.GetValueOrDefault("capabilityState"));
        Assert.Equal("coverage-context-only", downstream.Properties.GetValueOrDefault("limitationCode"));
    }

    [Fact]
    public void Full_semantic_state_does_not_turn_missing_syntax_fallback_into_coverage_gap()
    {
        var manifest = Manifest("Level1SemanticAnalysis", "Succeeded");
        var inventory = new[]
        {
            new FileInventoryItem("src/Modern/Modern.csproj", "Project", 1),
            new FileInventoryItem("src/Modern/Empty.cs", "CSharp", 0)
        };
        var semantic = new SemanticExtractionResult([], [], Attempted: true, ReducedCoverage: false);

        var capabilities = AnalyzerCapabilityDiagnosticExtractor.Extract(
            manifest,
            inventory,
            semantic,
            [],
            new ScanOptions("repo", "out"));

        AssertCapability(capabilities, AnalyzerCapabilityDiagnosticExtractor.Codes.SyntaxFallbackAvailable, "not-requested", "informational");
        Assert.DoesNotContain(capabilities, fact =>
            fact.Properties.GetValueOrDefault("capabilityCode") == AnalyzerCapabilityDiagnosticExtractor.Codes.DownstreamNoEvidenceCoverage);
        Assert.All(capabilities, fact => Assert.NotEqual("unknown-gap", fact.Properties.GetValueOrDefault("coverageEffect")));
    }

    [Fact]
    public void Reference_assembly_resolution_gap_is_unavailable_and_tier4()
    {
        var manifest = Manifest("Level1SemanticAnalysisReduced", "FailedOrPartial");
        var inventory = new[] { new FileInventoryItem("src/Legacy/Legacy.csproj", "Project", 1) };
        var semantic = new SemanticExtractionResult([], [], Attempted: true, ReducedCoverage: true);
        var support = FactFactory.Create(
            manifest,
            FactTypes.BuildEnvironmentDiagnostic,
            RuleIds.BuildEnvironmentWorkspaceDiagnostic,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(".", 1, 1, null, "test", "test/1.0"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["diagnosticCode"] = "MissingReferenceAssemblies",
                ["diagnosticKind"] = BuildEnvironmentDiagnosticExtractor.DiagnosticKindWorkspace
            });

        var capabilities = AnalyzerCapabilityDiagnosticExtractor.Extract(
            manifest,
            inventory,
            semantic,
            [support],
            new ScanOptions("repo", "out"));

        var reference = Assert.Single(capabilities, fact =>
            fact.Properties.GetValueOrDefault("capabilityCode") == AnalyzerCapabilityDiagnosticExtractor.Codes.ReferenceAssemblyResolution);
        Assert.Equal("unavailable", reference.Properties.GetValueOrDefault("capabilityState"));
        Assert.Equal(EvidenceTiers.Tier4Unknown, reference.EvidenceTier);
        Assert.NotEqual(EvidenceTiers.Tier1Semantic, reference.EvidenceTier);
    }

    [Fact]
    public void Syntax_only_remoting_support_preserves_tier3_capability_evidence()
    {
        var manifest = Manifest("Level1SemanticAnalysisReduced", "FailedOrPartial");
        var inventory = new[] { new FileInventoryItem("src/Legacy/RemotingHost.cs", "CSharp", 1) };
        var semantic = new SemanticExtractionResult([], [], Attempted: true, ReducedCoverage: true);
        var support = RemotingSyntaxFact(manifest);

        var capabilities = AnalyzerCapabilityDiagnosticExtractor.Extract(
            manifest,
            inventory,
            semantic,
            [support],
            new ScanOptions("repo", "out"));

        var remoting = Assert.Single(capabilities, fact =>
            fact.Properties.GetValueOrDefault("capabilityCode") == AnalyzerCapabilityDiagnosticExtractor.Codes.LegacyRemotingShape);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, remoting.EvidenceTier);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, remoting.Properties.GetValueOrDefault("strongestSupportingEvidenceTier"));
        Assert.Equal(support.FactId, remoting.Properties.GetValueOrDefault("supportingFactIds"));
    }

    [Fact]
    public async Task Capability_diagnostics_are_queryable_in_sqlite_and_combined_indexes()
    {
        using var temp = new TempDirectory();
        var repo = CreateLegacyRepo(temp.Path);
        var scanOut = Path.Combine(temp.Path, "scan");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var result = ScanEngine.Scan(new ScanOptions(repo, scanOut));
        SqliteIndexWriter.Write(Path.Combine(scanOut, "index.sqlite"), result.Manifest, result.Facts);

        await using (var connection = new SqliteConnection($"Data Source={Path.Combine(scanOut, "index.sqlite")}"))
        {
            await connection.OpenAsync();
            var count = await ExecuteScalarAsync<long>(
                connection,
                "select count(*) from facts where fact_type = 'AnalyzerCapabilityDiagnostic' and rule_id = 'analyzer.capability.legacy-toolchain.v1';");
            var propertiesJson = await ExecuteScalarAsync<string>(
                connection,
                "select properties_json from facts where fact_type = 'AnalyzerCapabilityDiagnostic' and json_extract(properties_json, '$.capabilityCode') = 'LegacyFrameworkSignalDetected' limit 1;");

            Assert.True(count >= 1);
            using var properties = JsonDocument.Parse(propertiesJson);
            Assert.Equal(AnalyzerCapabilityDiagnosticExtractor.SchemaVersion, properties.RootElement.GetProperty("schemaVersion").GetString());
        }

        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Path.Combine(scanOut, "index.sqlite")], combinedPath, ["legacy-source"]));
        await using (var connection = new SqliteConnection($"Data Source={combinedPath}"))
        {
            await connection.OpenAsync();
            var count = await ExecuteScalarAsync<long>(
                connection,
                """
                select count(*)
                from combined_facts facts
                join index_sources sources on sources.source_index_id = facts.source_index_id
                where sources.label = 'legacy-source'
                  and facts.fact_type = 'AnalyzerCapabilityDiagnostic'
                  and facts.rule_id = 'analyzer.capability.project-config.v1';
                """);
            Assert.True(count >= 1);
        }

        var report = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(
            combinedPath,
            Path.Combine(temp.Path, "combined-report"),
            Format: "json"));
        Assert.Contains(report.Report.KnownGaps, gap =>
            gap.SourceLabel == "legacy-source"
            && gap.Category.StartsWith("AnalyzerCapability:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cli_outputs_and_release_review_keep_capability_gaps_safe_and_coverage_relative()
    {
        using var temp = new TempDirectory();
        var beforeRepo = CreateLegacyRepo(Path.Combine(temp.Path, "before-root"));
        var afterRepo = CreateLegacyRepo(Path.Combine(temp.Path, "after-root"));
        var beforeOut = Path.Combine(temp.Path, "before-out");
        var afterOut = Path.Combine(temp.Path, "after-out");

        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", beforeRepo, "--out", beforeOut], output, error));
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", afterRepo, "--out", afterOut], output, error));
        await RewriteFirstCapabilitySchemaAsync(Path.Combine(afterOut, "index.sqlite"), "future-schema.v9");

        var artifactText = string.Join('\n',
            await File.ReadAllTextAsync(Path.Combine(afterOut, "facts.ndjson")),
            await File.ReadAllTextAsync(Path.Combine(afterOut, "scan-manifest.json")),
            await File.ReadAllTextAsync(Path.Combine(afterOut, "report.md")),
            await File.ReadAllTextAsync(Path.Combine(afterOut, "logs", "analyzer.log")));
        var unsafeKey = UnsafeFeedKey();
        var unsafeUrl = UnsafeFeedUrl();
        Assert.DoesNotContain(temp.Path, artifactText);
        Assert.DoesNotContain(unsafeKey, artifactText);
        Assert.DoesNotContain(unsafeUrl, artifactText);
        Assert.Contains("AnalyzerCapabilityDiagnostic", artifactText);

        var review = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            Path.Combine(beforeOut, "index.sqlite"),
            Path.Combine(afterOut, "index.sqlite"),
            Path.Combine(temp.Path, "release-review"),
            Format: "json"));
        if (review.JsonPath is not null)
        {
            artifactText += '\n' + await File.ReadAllTextAsync(review.JsonPath);
        }
        Assert.DoesNotContain(unsafeKey, artifactText);
        Assert.DoesNotContain(unsafeUrl, artifactText);

        Assert.NotEqual(ReleaseReviewClassifications.ActionableStaticEvidence, review.Report.Summary.RollupClassification);
        Assert.Contains(review.Report.Gaps, gap =>
            gap.GapKind == "ToolchainCapabilityReducedCoverage"
            && gap.Classification is ReleaseReviewClassifications.PartialAnalysis or ReleaseReviewClassifications.UnknownAnalysisGap
            && gap.Message.Contains("coverage-relative", StringComparison.Ordinal));
        Assert.Contains(review.Report.Gaps, gap =>
            gap.GapKind == "AnalyzerCapabilitySchemaUnsupported"
            && gap.Classification == ReleaseReviewClassifications.UnknownAnalysisGap);
        Assert.Contains(review.Report.Gaps, gap => gap.Message.Contains("no-evidence conclusions remain coverage-relative", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Release_review_counts_capability_gaps_as_reduced_source_coverage()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var beforeCombined = Path.Combine(temp.Path, "before-combined.sqlite");
        var afterCombined = Path.Combine(temp.Path, "after-combined.sqlite");
        var before = Manifest("Level1SemanticAnalysis", "Succeeded") with
        {
            ScanId = "scan-before",
            CommitSha = "1111111111111111111111111111111111111111"
        };
        var after = Manifest("Level1SemanticAnalysis", "Succeeded") with
        {
            ScanId = "scan-after",
            CommitSha = "2222222222222222222222222222222222222222"
        };

        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [CapabilityFact(after)]);

        var review = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeIndex,
            afterIndex,
            Path.Combine(temp.Path, "release-review"),
            Format: "json"));

        Assert.Equal("Full", review.Report.BeforeSnapshot.Sources.Single().Coverage);
        Assert.Equal("Reduced", review.Report.AfterSnapshot.Sources.Single().Coverage);
        Assert.Contains(review.Report.AfterSnapshot.CoverageWarnings, warning =>
            warning.Contains("reduced analysis coverage", StringComparison.Ordinal));
        Assert.Contains(review.Report.SourceCoverage, coverage =>
            coverage.SourceLabel == "single"
            && coverage.AfterCoverage == "Reduced"
            && coverage.GapIds.Count > 0);
        Assert.Contains(review.Report.Gaps, gap =>
            gap.GapKind == "ToolchainCapabilityReducedCoverage"
            && gap.SupportingFactIds.Contains("fact-capability-after"));

        await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeIndex], beforeCombined, ["api"]));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterIndex], afterCombined, ["api"]));

        var combinedReview = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            beforeCombined,
            afterCombined,
            Path.Combine(temp.Path, "combined-release-review"),
            Format: "json"));

        Assert.Equal("Full", combinedReview.Report.BeforeSnapshot.Sources.Single().Coverage);
        Assert.Equal("Reduced", combinedReview.Report.AfterSnapshot.Sources.Single().Coverage);
        Assert.Contains(combinedReview.Report.AfterSnapshot.CoverageWarnings, warning =>
            warning.Contains("reduced analysis coverage", StringComparison.Ordinal));
        Assert.Contains(combinedReview.Report.Gaps, gap =>
            gap.GapKind == "ToolchainCapabilityReducedCoverage"
            && gap.SourceLabel == "api");
    }

    private static string CreateLegacyRepo(string root)
    {
        var repo = Path.Combine(root, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "src", "LegacyWeb"));
        Directory.CreateDirectory(Path.Combine(repo, "src", "LegacyWeb", "Properties"));
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "LegacyWeb.csproj"), """
            <Project ToolsVersion="12.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                <VisualStudioVersion>12.0</VisualStudioVersion>
                <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21}</ProjectTypeGuids>
              </PropertyGroup>
              <Import Project="$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets" />
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "Default.aspx"), "<%@ Page Language=\"C#\" CodeBehind=\"Default.aspx.cs\" Inherits=\"LegacyWeb.Default\" %>");
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "Default.aspx.cs"), "namespace LegacyWeb; public partial class Default { }");
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "packages.config"), """
            <packages>
              <package id="Example.Legacy" version="1.2.3" targetFramework="net472" />
            </packages>
            """);
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "nuget.config"), """
            <configuration>
              <packageSources>
                <add key="__UNSAFE_KEY__" value="__UNSAFE_URL__" />
              </packageSources>
            </configuration>
            """
            .Replace("__UNSAFE_KEY__", UnsafeFeedKey(), StringComparison.Ordinal)
            .Replace("__UNSAFE_URL__", UnsafeFeedUrl(), StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "packages.lock.json"), "{}");
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "Properties", "Resources.resx"), "<root>");
        return repo;
    }

    private static ScanManifest Manifest(string analysisLevel, string buildStatus)
    {
        return new ScanManifest(
            "scan-test",
            "synthetic-repo",
            null,
            null,
            "1111111111111111111111111111111111111111",
            ScannerVersions.TraceMap,
            DateTimeOffset.UnixEpoch,
            analysisLevel,
            buildStatus,
            [],
            ["src/Legacy/Legacy.csproj"],
            [],
            []);
    }

    private static void AssertCapability(IReadOnlyList<CodeFact> facts, string code, string state, string coverageEffect)
    {
        Assert.Contains(facts, fact =>
            fact.Properties.GetValueOrDefault("capabilityCode") == code
            && fact.Properties.GetValueOrDefault("capabilityState") == state
            && fact.Properties.GetValueOrDefault("coverageEffect") == coverageEffect
            && fact.Properties.GetValueOrDefault("schemaVersion") == AnalyzerCapabilityDiagnosticExtractor.SchemaVersion
            && fact.Properties.ContainsKey("limitationCode"));
    }

    private static string UnsafeFeedKey()
    {
        return string.Concat("package", "-source", "-key");
    }

    private static string UnsafeFeedUrl()
    {
        return string.Concat("https", "://", "example", ".invalid", "/feed");
    }

    private static IReadOnlyList<string> StableCapabilityFacts(ScanResult result)
    {
        return result.Facts
            .Where(fact => fact.FactType == FactTypes.AnalyzerCapabilityDiagnostic)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(fact => string.Join("|",
                fact.FactId,
                fact.RuleId,
                fact.EvidenceTier,
                fact.Evidence.FilePath,
                fact.Evidence.StartLine,
                fact.Properties.GetValueOrDefault("capabilityCode"),
                fact.Properties.GetValueOrDefault("capabilityState"),
                fact.Properties.GetValueOrDefault("coverageEffect"),
                fact.Properties.GetValueOrDefault("sourceScope")))
            .ToArray();
    }

    private static CodeFact CapabilityFact(ScanManifest manifest)
    {
        return new CodeFact(
            "fact-capability-after",
            manifest.ScanId,
            manifest.RepoName,
            manifest.CommitSha,
            null,
            FactTypes.AnalyzerCapabilityDiagnostic,
            RuleIds.AnalyzerCapabilitySemantic,
            EvidenceTiers.Tier2Structural,
            null,
            null,
            null,
            new EvidenceSpan(".", 1, 1, null, "AnalyzerCapabilityDiagnosticExtractor", ScannerVersions.AnalyzerCapabilityExtractor),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["capabilityCode"] = AnalyzerCapabilityDiagnosticExtractor.Codes.CSharpSemanticCompilation,
                ["capabilityKind"] = "semantic",
                ["capabilityState"] = AnalyzerCapabilityDiagnosticExtractor.States.Reduced,
                ["coverageEffect"] = AnalyzerCapabilityDiagnosticExtractor.Effects.ReducedSemantic,
                ["guidanceCode"] = AnalyzerCapabilityDiagnosticExtractor.GuidanceCodes.TreatAsReducedCoverage,
                ["limitationCode"] = AnalyzerCapabilityDiagnosticExtractor.LimitationCodes.SemanticStatusDerived,
                ["schemaVersion"] = AnalyzerCapabilityDiagnosticExtractor.SchemaVersion,
                ["sourceScope"] = "workspace",
                ["supportingFactIds"] = string.Empty
            });
    }

    private static CodeFact RemotingSyntaxFact(ScanManifest manifest)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingApiUsageDeclared,
            RuleIds.LegacyRemotingApi,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan("src/Legacy/RemotingHost.cs", 12, 12, null, "LegacyRemotingExtractor", ScannerVersions.LegacyRemotingExtractor),
            targetSymbol: "System.Runtime.Remoting.RemotingConfiguration",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["apiKind"] = "syntax-name",
                ["apiName"] = "System.Runtime.Remoting.RemotingConfiguration",
                ["limitation"] = "Syntax-only Remoting API reference; project-defined lookalikes remain review-tier evidence."
            });
    }

    private static async Task RewriteFirstCapabilitySchemaAsync(string indexPath, string schemaVersion)
    {
        await using var connection = new SqliteConnection($"Data Source={indexPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update facts
            set properties_json = replace(properties_json, 'legacy-dotnet-toolchain-diagnostics.v1', $schema)
            where fact_id = (
              select fact_id
              from facts
              where fact_type = 'AnalyzerCapabilityDiagnostic'
              order by fact_id
              limit 1
            );
            """;
        command.Parameters.AddWithValue("$schema", schemaVersion);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
