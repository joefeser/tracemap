using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class BuildEnvironmentDiagnosticTests
{
    [Fact]
    public void Scan_emits_legacy_build_environment_diagnostics()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "src", "LegacyWeb"));
        Directory.CreateDirectory(Path.Combine(repo, "src", "LegacyWeb", "Properties"));
        Directory.CreateDirectory(Path.Combine(repo, "src", "LegacyWeb", "Service References", "Orders"));
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "LegacyWeb.csproj"), """
            <Project ToolsVersion="12.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                <VisualStudioVersion>12.0</VisualStudioVersion>
                <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21};{11111111-1111-1111-1111-111111111111}</ProjectTypeGuids>
              </PropertyGroup>
              <Import Project="$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets" />
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "packages.config"), """
            <packages>
              <package id="Example.Legacy" version="1.2.3" targetFramework="net472" />
            </packages>
            """);
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "nuget.config"), """
            <configuration>
              <packageSources>
                <add key="redacted" value="https://example.invalid/feed" />
              </packageSources>
            </configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "packages.lock.json"), "{}");
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "Default.aspx"), "<%@ Page Language=\"C#\" %>");
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "Service References", "Orders", "Orders.svcmap"), "<ReferenceGroup />");
        File.WriteAllText(Path.Combine(repo, "src", "LegacyWeb", "Properties", "Resources.resx"), "<root>");
        File.WriteAllText(Path.Combine(repo, "src", "Tools.vbproj"), "<Project><PropertyGroup><TargetFrameworkVersion>v4.8</TargetFrameworkVersion></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(repo, "Orphan.aspx"), "<%@ Page Language=\"C#\" %>");
        File.WriteAllText(Path.Combine(repo, "Orphan.aspx.cs"), "public partial class Orphan { }");
        File.WriteAllText(Path.Combine(repo, "Orphan.aspx.designer.cs"), "public partial class Orphan { }");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var diagnostics = result.Facts
            .Where(fact => fact.FactType == FactTypes.BuildEnvironmentDiagnostic)
            .ToArray();

        AssertDiagnostic(diagnostics, "LegacyTargetFramework", RuleIds.BuildEnvironmentTargetFramework, EvidenceTiers.Tier2Structural);
        AssertDiagnostic(diagnostics, "OldMsBuildToolsVersion", RuleIds.BuildEnvironmentToolset, EvidenceTiers.Tier2Structural);
        AssertDiagnostic(diagnostics, "VisualStudioVersionDeclared", RuleIds.BuildEnvironmentToolset, EvidenceTiers.Tier2Structural);
        AssertDiagnostic(diagnostics, "ImportedLegacyTargets", RuleIds.BuildEnvironmentToolset, EvidenceTiers.Tier2Structural);
        AssertDiagnostic(diagnostics, "WebApplicationProjectTargets", RuleIds.BuildEnvironmentProjectFormat, EvidenceTiers.Tier2Structural);
        AssertDiagnostic(diagnostics, "UnknownLegacyProjectFormat", RuleIds.BuildEnvironmentProjectFormat, EvidenceTiers.Tier4Unknown);
        AssertDiagnostic(diagnostics, "PackagesConfigPresent", RuleIds.BuildEnvironmentRestore, EvidenceTiers.Tier2Structural);
        AssertDiagnostic(diagnostics, "NuGetConfigPresent", RuleIds.BuildEnvironmentRestore, EvidenceTiers.Tier2Structural);
        AssertDiagnostic(diagnostics, "PackagesLockPresent", RuleIds.BuildEnvironmentRestore, EvidenceTiers.Tier2Structural);
        AssertDiagnostic(diagnostics, "GeneratedFileMissing", RuleIds.BuildEnvironmentGeneratedFiles, EvidenceTiers.Tier4Unknown);
        AssertDiagnostic(diagnostics, "GeneratedFileMalformed", RuleIds.BuildEnvironmentGeneratedFiles, EvidenceTiers.Tier4Unknown);
        AssertDiagnostic(diagnostics, "GeneratedFileUnlinked", RuleIds.BuildEnvironmentGeneratedFiles, EvidenceTiers.Tier3SyntaxOrTextual);
        Assert.Contains(diagnostics, fact =>
            fact.CommitSha == "unknown"
            && fact.Evidence.StartLine > 0
            && fact.Evidence.ExtractorVersion == ScannerVersions.BuildEnvironmentExtractor
            && fact.Properties.ContainsKey("guidanceCode")
            && fact.Properties.ContainsKey("limitation"));
        Assert.True(diagnostics.Select(fact => fact.FactId).Distinct(StringComparer.Ordinal).Count() > 1);
    }

    [Fact]
    public void Scan_emits_modern_target_framework_without_restore_not_requested_noise()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "src", "Modern"));
        File.WriteAllText(Path.Combine(repo, "src", "Modern", "Modern.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "src", "Modern", "Handler.cs"), "namespace Modern; public sealed class Handler { }");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var diagnostics = result.Facts.Where(fact => fact.FactType == FactTypes.BuildEnvironmentDiagnostic).ToArray();

        AssertDiagnostic(diagnostics, "SdkStyleTargetFramework", RuleIds.BuildEnvironmentTargetFramework, EvidenceTiers.Tier2Structural);
        Assert.DoesNotContain(diagnostics, fact => fact.Properties.GetValueOrDefault("diagnosticCode") == "RestoreNotRequested");
        Assert.DoesNotContain(MarkdownReportWriter.Build(result), "RestoreNotRequested");
    }

    [Fact]
    public async Task Cli_restore_failure_artifacts_are_sanitized()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        var unsafeFeed = Path.Combine(temp.Path, "private-feed-with-token-abc123");
        Directory.CreateDirectory(Path.Combine(repo, "src", "RestoreSample"));
        File.WriteAllText(Path.Combine(repo, "src", "RestoreSample", "RestoreSample.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RestoreSources>{unsafeFeed}</RestoreSources>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="TraceMap.Does.Not.Exist" Version="999.0.0" />
              </ItemGroup>
            </Project>
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath, "--restore"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var artifactText = string.Join('\n',
            await File.ReadAllTextAsync(Path.Combine(outputPath, "facts.ndjson")),
            await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-manifest.json")),
            await File.ReadAllTextAsync(Path.Combine(outputPath, "report.md")),
            await File.ReadAllTextAsync(Path.Combine(outputPath, "logs", "analyzer.log")));
        Assert.DoesNotContain(unsafeFeed, artifactText);
        Assert.DoesNotContain("private-feed-with-token-abc123", artifactText);
        Assert.Contains("NuGetRestoreFailed", artifactText);
        Assert.Contains("Build Environment Diagnostics", artifactText);

        await using var connection = new SqliteConnection($"Data Source={Path.Combine(outputPath, "index.sqlite")}");
        await connection.OpenAsync();
        var sqliteText = await ExecuteScalarAsync<string>(
            connection,
            "select group_concat(properties_json, char(10)) from facts where fact_type in ('AnalysisGap', 'BuildEnvironmentDiagnostic');");
        Assert.DoesNotContain(unsafeFeed, sqliteText);
        Assert.DoesNotContain("private-feed-with-token-abc123", sqliteText);
        Assert.Contains("NuGetRestoreFailed", sqliteText);
    }

    [Fact]
    public void Sanitized_gap_fact_ids_do_not_depend_on_raw_workspace_messages()
    {
        var manifest = new ScanManifest(
            "scan-test",
            "repo",
            null,
            null,
            "abc123",
            ScannerVersions.TraceMap,
            DateTimeOffset.UnixEpoch,
            "Level1SemanticAnalysisReduced",
            "FailedOrPartial",
            [],
            [],
            [],
            []);
        var first = BuildEnvironmentDiagnosticExtractor.SanitizeWorkspaceGap(
            "ProjectLoadFailed",
            "Unable to load project from /tmp/private-one because SDK Microsoft.NET.Sdk was unavailable.");
        var second = BuildEnvironmentDiagnosticExtractor.SanitizeWorkspaceGap(
            "ProjectLoadFailed",
            "Unable to load project from /tmp/private-two because SDK Microsoft.NET.Sdk was unavailable.");

        var firstFact = CreateGapFact(manifest, first);
        var secondFact = CreateGapFact(manifest, second);

        Assert.Equal("SdkResolutionFailed", first.DiagnosticCode);
        Assert.Equal(first.DiagnosticCode, second.DiagnosticCode);
        Assert.Equal(firstFact.FactId, secondFact.FactId);
        Assert.DoesNotContain("private-one", first.Message);
        Assert.DoesNotContain("private-two", second.Message);
    }

    [Fact]
    public void Observed_value_hashes_are_stable_distinct_and_secret_categories_are_not_hashed()
    {
        var first = BuildEnvironmentDiagnosticExtractor.HashObservedValue("import", "UnknownImportedTargets", "/tmp/private/a.targets");
        var same = BuildEnvironmentDiagnosticExtractor.HashObservedValue("import", "UnknownImportedTargets", "/tmp/private/a.targets");
        var second = BuildEnvironmentDiagnosticExtractor.HashObservedValue("import", "UnknownImportedTargets", "/tmp/private/b.targets");
        var credential = BuildEnvironmentDiagnosticExtractor.SanitizeWorkspaceGap(
            "RestoreFailed",
            "401 unauthorized for https://user:secret@example.invalid/feed");

        Assert.Equal(32, first.Length);
        Assert.Equal(first, same);
        Assert.NotEqual(first, second);
        Assert.Equal("CredentialRequired", credential.DiagnosticCode);
        Assert.Equal("category-only", credential.Sanitization);
    }

    [Fact]
    public void Report_places_environment_diagnostics_before_fact_counts()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "src", "Legacy"));
        File.WriteAllText(Path.Combine(repo, "src", "Legacy", "Legacy.csproj"), """
            <Project ToolsVersion="4.0">
              <PropertyGroup>
                <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
              </PropertyGroup>
            </Project>
            """);

        var report = MarkdownReportWriter.Build(ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out"))));

        var diagnosticsIndex = report.IndexOf("## Build Environment Diagnostics", StringComparison.Ordinal);
        var factsIndex = report.IndexOf("## Facts By Type", StringComparison.Ordinal);
        Assert.True(diagnosticsIndex > 0);
        Assert.True(diagnosticsIndex < factsIndex);
        Assert.Contains("| Code | Tier | Rule | Evidence | Guidance | Limitation |", report);
    }

    [Fact]
    public async Task Build_environment_diagnostics_are_queryable_in_sqlite()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(Path.Combine(repo, "src", "Legacy"));
        File.WriteAllText(Path.Combine(repo, "src", "Legacy", "Legacy.csproj"), """
            <Project ToolsVersion="4.0">
              <PropertyGroup>
                <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
              </PropertyGroup>
            </Project>
            """);
        var result = ScanEngine.Scan(new ScanOptions(repo, outputPath));
        SqliteIndexWriter.Write(Path.Combine(outputPath, "index.sqlite"), result.Manifest, result.Facts);

        await using var connection = new SqliteConnection($"Data Source={Path.Combine(outputPath, "index.sqlite")}");
        await connection.OpenAsync();
        var count = await ExecuteScalarAsync<long>(
            connection,
            "select count(*) from facts where fact_type = 'BuildEnvironmentDiagnostic' and rule_id = 'build.environment.target-framework.v1';");
        var propertiesJson = await ExecuteScalarAsync<string>(
            connection,
            "select properties_json from facts where fact_type = 'BuildEnvironmentDiagnostic' and json_extract(properties_json, '$.diagnosticCode') = 'LegacyTargetFramework' limit 1;");

        Assert.True(count >= 1);
        using var properties = JsonDocument.Parse(propertiesJson);
        Assert.Equal("LegacyTargetFramework", properties.RootElement.GetProperty("diagnosticCode").GetString());
    }

    private static void AssertDiagnostic(IReadOnlyList<CodeFact> diagnostics, string code, string ruleId, string tier)
    {
        Assert.Contains(diagnostics, fact =>
            fact.RuleId == ruleId
            && fact.EvidenceTier == tier
            && fact.Properties.GetValueOrDefault("diagnosticCode") == code
            && fact.CommitSha == "unknown"
            && fact.Evidence.StartLine >= 1);
    }

    private static CodeFact CreateGapFact(ScanManifest manifest, BuildEnvironmentDiagnosticExtractor.SanitizedDiagnostic diagnostic)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.CSharpSemanticWorkspace,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(".", 1, 1, null, "test", "test/1.0"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverageEffect"] = diagnostic.CoverageEffect,
                ["diagnosticCode"] = diagnostic.DiagnosticCode,
                ["diagnosticKind"] = diagnostic.DiagnosticKind,
                ["gapKind"] = "ProjectLoadFailed",
                ["guidanceCode"] = diagnostic.GuidanceCode,
                ["message"] = diagnostic.Message,
                ["messageHash"] = FactFactory.Hash(diagnostic.Message, 32),
                ["sanitization"] = diagnostic.Sanitization
            });
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
