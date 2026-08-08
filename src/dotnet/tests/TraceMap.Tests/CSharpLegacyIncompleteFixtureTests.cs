using System.Diagnostics;
using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class CSharpLegacyIncompleteFixtureTests
{
    [Fact]
    public void Legacy_project_with_unavailable_reference_preserves_syntax_and_reports_reduced_coverage()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var projectDirectory = Path.Combine(repo, "src", "LegacyApp");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, "LegacyApp.csproj"), """
            <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                <AssemblyName>LegacyApp</AssemblyName>
                <RootNamespace>LegacyApp</RootNamespace>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Unavailable.Contracts">
                  <HintPath>lib\Unavailable.Contracts.dll</HintPath>
                </Reference>
                <Compile Include="Handler.cs" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectDirectory, "Handler.cs"), """
            using Unavailable.Contracts;

            namespace LegacyApp;

            public sealed class Handler
            {
                public void Run(IContract contract)
                {
                    contract.Execute();
                }
            }
            """);
        InitializeGit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.Contains("Reduced", result.Manifest.AnalysisLevel, StringComparison.Ordinal);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.CSharpSyntaxDeclarations
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.TargetSymbol == "Handler"
            && fact.Evidence.FilePath == "src/LegacyApp/Handler.cs"
            && fact.Evidence.StartLine == 5);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.InvocationName
            && fact.RuleId == RuleIds.CSharpSyntaxInvocation
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.TargetSymbol == "Execute"
            && fact.Evidence.FilePath == "src/LegacyApp/Handler.cs"
            && fact.Evidence.StartLine == 9);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.CommitSha == result.Manifest.CommitSha);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.BuildEnvironmentDiagnostic
            && fact.RuleId == RuleIds.BuildEnvironmentTargetFramework
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural
            && fact.Properties.GetValueOrDefault("diagnosticCode") == "LegacyTargetFramework");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.TargetSymbol?.Contains("IContract.Execute", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Out_of_root_project_reference_uses_compiler_identity_without_disclosing_host_paths()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var appDirectory = Path.Combine(repo, "src", "App");
        var externalDirectory = Path.Combine(temp.Path, "external", "Contracts");
        Directory.CreateDirectory(appDirectory);
        Directory.CreateDirectory(externalDirectory);
        File.WriteAllText(Path.Combine(externalDirectory, "Contracts.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>External.Contracts</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(externalDirectory, "ExternalService.cs"), """
            namespace External.Contracts;

            public sealed class ExternalService
            {
                public void Execute() { }
            }
            """);
        var relativeReference = Path.GetRelativePath(appDirectory, Path.Combine(externalDirectory, "Contracts.csproj"));
        File.WriteAllText(Path.Combine(appDirectory, "App.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>App</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="{relativeReference}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(appDirectory, "Caller.cs"), """
            using External.Contracts;

            namespace App;

            public sealed class Caller
            {
                public void Run()
                {
                    new ExternalService().Execute();
                }
            }
            """);
        InitializeGit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var call = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.ContractElement == "Execute");
        Assert.Equal("src/App/Caller.cs", call.Evidence.FilePath);
        Assert.Equal(9, call.Evidence.StartLine);
        Assert.Equal("External.Contracts", call.Properties["calleeAssemblyName"]);
        Assert.Contains("External.Contracts.ExternalService", call.Properties["targetSymbolId"], StringComparison.Ordinal);
        Assert.All(result.Inventory, item => Assert.DoesNotContain("external/Contracts", item.RelativePath, StringComparison.OrdinalIgnoreCase));

        var serialized = JsonSerializer.Serialize(result.Facts) + MarkdownReportWriter.Build(result);
        Assert.DoesNotContain(temp.Path, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(externalDirectory, serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static void InitializeGit(string repo)
    {
        RunGit(repo, "init");
        RunGit(repo, "config", "user.email", "fixture@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Fixture");
        RunGit(repo, "add", "-A");
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
