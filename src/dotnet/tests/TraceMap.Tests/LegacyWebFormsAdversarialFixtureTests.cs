using System.Diagnostics;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class LegacyWebFormsAdversarialFixtureTests
{
    [Fact]
    public async Task Legacy_non_sdk_partial_build_preserves_structural_chain_and_provenance_through_packet()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Legacy.csproj"), """
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
                <OutputType>Library</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="System" />
                <Reference Include="System.Web" />
                <Reference Include="Missing.Legacy.Dependency" />
                <Compile Include="Default.aspx.cs"><DependentUpon>Default.aspx</DependentUpon></Compile>
                <Content Include="Default.aspx" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Legacy.Default" AutoEventWireup="false" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            namespace Legacy;
            public partial class Default : System.Web.UI.Page
            {
                protected void Save_Click(object sender, EventArgs e) { }
            }
            """);
        InitializeGit(repo);

        var output = Path.Combine(temp.Path, "scan");
        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.Contains("Reduced", result.Manifest.AnalysisLevel, StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{64}$", result.Manifest.SourceSnapshotDigest!);
        var page = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsPageDeclared);
        var binding = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared);
        var handler = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsHandlerResolved);
        Assert.Equal(page.SourceSymbol, binding.Properties["surfaceIdentity"]);
        Assert.Equal(binding.SourceSymbol, handler.SourceSymbol);
        Assert.Equal(binding.TargetSymbol, handler.TargetSymbol);
        Assert.Equal(binding.FactId, handler.Properties["supportingFactIds"]);
        Assert.Contains(handler.EvidenceTier, new[] { EvidenceTiers.Tier1Semantic, EvidenceTiers.Tier2Structural });
        Assert.Equal(RuleIds.LegacyWebFormsHandlerResolution, handler.RuleId);
        Assert.Equal(ScannerVersions.LegacyWebFormsExtractor, handler.Evidence.ExtractorVersion);
        Assert.Equal(result.Manifest.CommitSha, handler.CommitSha);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId != RuleIds.LegacyWebFormsHandlerResolution);

        await JsonlFactWriter.WriteAsync(Path.Combine(output, "facts.ndjson"), result.Facts);
        SqliteIndexWriter.Write(Path.Combine(output, "index.sqlite"), result.Manifest, result.Facts);
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(
            Path.Combine(output, "index.sqlite"),
            Path.Combine(temp.Path, "packet")));
        var packetHandler = Assert.Single(packet.EventChains, chain =>
            chain.HandlerFactId == handler.FactId);
        Assert.Equal(binding.FactId, packetHandler.BindingFactId);
        Assert.Equal("reduced-static-webforms-modernization", packet.Coverage);
        Assert.Contains(packet.Gaps, gap => gap.Classification == "SourceAnalysisCoverageReduced"
            && gap.EvidenceTier == EvidenceTiers.Tier4Unknown
            && gap.Limitations.Count > 0);

        using var connection = new SqliteConnection($"Data Source={Path.Combine(output, "index.sqlite")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select source_symbol, target_symbol, rule_id, evidence_tier, file_path, start_line, end_line, extractor_version from facts where fact_id = $id";
        command.Parameters.AddWithValue("$id", handler.FactId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(handler.SourceSymbol, reader.GetString(0));
        Assert.Equal(handler.TargetSymbol, reader.GetString(1));
        Assert.Equal(handler.RuleId, reader.GetString(2));
        Assert.Equal(handler.EvidenceTier, reader.GetString(3));
        Assert.Equal("Default.aspx.cs", reader.GetString(4));
        Assert.True(reader.GetInt32(5) > 0);
        Assert.True(reader.GetInt32(6) >= reader.GetInt32(5));
        Assert.Equal(ScannerVersions.LegacyWebFormsExtractor, reader.GetString(7));
    }

    [Fact]
    public void Duplicate_cross_project_surfaces_and_partial_handlers_do_not_collide_without_designers()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var shared = Path.Combine(repo, "Shared");
        Directory.CreateDirectory(shared);
        File.WriteAllText(Path.Combine(shared, "Default.Handlers.cs"), """
            using System;
            namespace Shared;
            public partial class Default
            {
                protected void Save_Click(object sender, EventArgs e) { }
            }
            """);
        foreach (var projectName in new[] { "First", "Second" })
        {
            var project = Path.Combine(repo, projectName);
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(project, $"{projectName}.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <ItemGroup>
                    <Compile Include="..\Shared\Default.Handlers.cs" Link="Default.Handlers.cs" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(project, "Default.aspx"), """
                <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Shared.Default" %>
                <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
                """);
            File.WriteAllText(Path.Combine(project, "Default.aspx.cs"), """
                namespace Shared;
                public partial class Default { }
                """);
        }
        InitializeGit(repo);

        var first = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-a")));
        var second = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-b")));
        var pages = Facts(first, FactTypes.WebFormsPageDeclared);
        var controls = Facts(first, FactTypes.WebFormsControlDeclared);
        var bindings = Facts(first, FactTypes.WebFormsEventBindingDeclared);
        var handlers = Facts(first, FactTypes.WebFormsHandlerResolved);

        Assert.Equal(2, pages.Length);
        Assert.Equal(2, controls.Length);
        Assert.Equal(2, bindings.Length);
        Assert.Equal(2, handlers.Length);
        Assert.Equal(2, pages.Select(fact => fact.Properties["surfaceIdentity"]).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, controls.Select(fact => fact.Properties["controlIdentity"]).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, bindings.Select(fact => fact.SourceSymbol).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, handlers.Select(fact => fact.TargetSymbol).Distinct(StringComparer.Ordinal).Count());
        Assert.All(controls, fact => Assert.False(fact.Properties.ContainsKey("designerFactId")));
        Assert.All(handlers, handler =>
        {
            var binding = Assert.Single(bindings, candidate => candidate.FactId == handler.Properties["bindingFactId"]);
            Assert.Equal(binding.SourceSymbol, handler.SourceSymbol);
            Assert.Equal(binding.TargetSymbol, handler.TargetSymbol);
            Assert.Equal(EvidenceTiers.Tier1Semantic, handler.EvidenceTier);
            Assert.Equal("Default.Handlers.cs", Path.GetFileName(handler.Evidence.FilePath));
        });
        Assert.Equal(
            first.Facts.Where(IsWebFormsEvidence).Select(fact => fact.FactId).OrderBy(value => value, StringComparer.Ordinal),
            second.Facts.Where(IsWebFormsEvidence).Select(fact => fact.FactId).OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void Full_snapshot_rename_delete_exclude_and_partial_failure_transitions_are_attributable()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var pages = Path.Combine(repo, "Pages");
        Directory.CreateDirectory(pages);
        File.WriteAllText(Path.Combine(repo, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        WriteSurface(pages, "Default");
        InitializeGit(repo);

        var baseline = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "baseline")));
        var repeat = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "repeat")));
        var baselineFacts = baseline.Facts.Where(IsWebFormsEvidence).ToArray();
        Assert.NotEmpty(baselineFacts);
        Assert.Equal(baseline.Manifest.SourceSnapshotDigest, repeat.Manifest.SourceSnapshotDigest);
        Assert.Equal(
            baselineFacts.Select(fact => fact.FactId).OrderBy(value => value, StringComparer.Ordinal),
            repeat.Facts.Where(IsWebFormsEvidence).Select(fact => fact.FactId).OrderBy(value => value, StringComparer.Ordinal));

        File.Move(Path.Combine(pages, "Default.aspx"), Path.Combine(pages, "Renamed.aspx"));
        File.Move(Path.Combine(pages, "Default.aspx.cs"), Path.Combine(pages, "Renamed.aspx.cs"));
        WriteSurface(pages, "Renamed");
        var renamed = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "renamed")));
        Assert.NotEqual(baseline.Manifest.SourceSnapshotDigest, renamed.Manifest.SourceSnapshotDigest);
        Assert.DoesNotContain(renamed.Facts, fact => fact.Evidence.FilePath.Contains("Default.aspx", StringComparison.Ordinal));
        Assert.Contains(renamed.Facts, fact => fact.FactType == FactTypes.WebFormsPageDeclared && fact.Evidence.FilePath == "Pages/Renamed.aspx");

        File.Delete(Path.Combine(pages, "Renamed.aspx"));
        File.Delete(Path.Combine(pages, "Renamed.aspx.cs"));
        var deleted = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "deleted")));
        Assert.NotEqual(renamed.Manifest.SourceSnapshotDigest, deleted.Manifest.SourceSnapshotDigest);
        Assert.DoesNotContain(deleted.Facts, IsWebFormsEvidence);

        WriteSurface(pages, "Restored");
        var excluded = ScanEngine.Scan(new ScanOptions(
            repo,
            Path.Combine(temp.Path, "excluded"),
            ExcludeGlobs: ["Pages/**"]));
        Assert.DoesNotContain(excluded.Facts, IsWebFormsEvidence);
        Assert.Contains(excluded.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "ScanScopeExcludedSources");
        var excludedScan = Assert.Single(excluded.Facts, fact => fact.FactType == FactTypes.RepoScanned);
        Assert.Equal("Pages/**", excludedScan.Properties["scanScopeExcludes"]);

        File.WriteAllText(Path.Combine(repo, "Sample.csproj"), """
            <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                <OutputType>Library</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Unavailable.Legacy.Dependency">
                  <HintPath>lib\Unavailable.Legacy.Dependency.dll</HintPath>
                </Reference>
                <Compile Include="Pages\Restored.aspx.cs" />
                <Content Include="Pages\Restored.aspx" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
            </Project>
            """);
        var partial = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "partial")));
        Assert.Equal("FailedOrPartial", partial.Manifest.BuildStatus);
        Assert.Contains("Reduced", partial.Manifest.AnalysisLevel, StringComparison.Ordinal);
        Assert.Contains(partial.Facts, fact => fact.FactType == FactTypes.WebFormsPageDeclared);
        Assert.Contains(partial.Facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared);
        Assert.Contains(partial.Facts, fact => fact.FactType == FactTypes.AnalysisGap);
    }

    private static CodeFact[] Facts(ScanResult result, string factType) =>
        result.Facts.Where(fact => fact.FactType == factType).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();

    private static bool IsWebFormsEvidence(CodeFact fact) =>
        fact.FactType.StartsWith("WebForms", StringComparison.Ordinal)
        || fact.RuleId.StartsWith("legacy.webforms", StringComparison.Ordinal);

    private static void WriteSurface(string directory, string name)
    {
        File.WriteAllText(Path.Combine(directory, $"{name}.aspx"), $$"""
            <%@ Page Language="C#" CodeBehind="{{name}}.aspx.cs" Inherits="Sample.{{name}}" AutoEventWireup="false" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(directory, $"{name}.aspx.cs"), $$"""
            using System;
            namespace Sample;
            public partial class {{name}}
            {
                protected void Save_Click(object sender, EventArgs e) { }
            }
            """);
    }

    private static void InitializeGit(string repo)
    {
        RunGit(repo, "init", "-b", "fixture");
        RunGit(repo, "config", "user.email", "fixture@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Fixture");
        RunGit(repo, "add", ".");
        RunGit(repo, "commit", "-m", "fixture");
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }
}
