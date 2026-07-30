using System.Text;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class GeneratedSemanticPathTests
{
    [Fact]
    public async Task Scan_projects_external_package_source_to_stable_synthetic_identity_across_all_outputs()
    {
        using var temp = new TempDirectory();
        var first = await ScanFixtureAsync(temp.Path, "first");
        var second = await ScanFixtureAsync(temp.Path, "second");

        Assert.Equal(first.SyntheticPath, second.SyntheticPath);
        Assert.StartsWith("__external__/csharp-package-", first.SyntheticPath, StringComparison.Ordinal);
        Assert.EndsWith(".cs", first.SyntheticPath, StringComparison.Ordinal);

        foreach (var fixture in new[] { first, second })
        {
            Assert.Contains(fixture.Facts, fact =>
                fact.GetProperty("factType").GetString() == FactTypes.AnalysisGap
                && fact.GetProperty("ruleId").GetString() == RuleIds.CSharpSemanticWorkspace
                && fact.GetProperty("properties").GetProperty("gapKind").GetString() == "ExternalSourcePathProjected"
                && fact.GetProperty("evidence").GetProperty("filePath").GetString() == fixture.SyntheticPath);

            foreach (var outputPath in fixture.StandardOutputs)
            {
                var output = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(outputPath));
                Assert.DoesNotContain(temp.Path, output, StringComparison.Ordinal);
                Assert.DoesNotContain(fixture.ExternalRoot, output, StringComparison.Ordinal);
                Assert.DoesNotContain(fixture.ExternalFile, output, StringComparison.Ordinal);
                Assert.DoesNotContain("example.generator", output, StringComparison.OrdinalIgnoreCase);
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(home))
                {
                    Assert.DoesNotContain(home, output, StringComparison.Ordinal);
                }
            }
        }
    }

    private static async Task<FixtureResult> ScanFixtureAsync(string root, string name)
    {
        var repo = Path.Combine(root, $"{name}-repo");
        var project = Path.Combine(repo, "src", "Sample");
        var externalRoot = Path.Combine(root, $"{name}-host", ".nuget", "packages");
        var externalFile = Path.Combine(
            externalRoot,
            "example.generator",
            "1.0.0",
            "contentFiles",
            "cs",
            "ExternalSdkSource.cs");
        var outputPath = Path.Combine(root, $"{name}-out");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(Path.GetDirectoryName(externalFile)!);
        await File.WriteAllTextAsync(externalFile, """
            namespace ExternalPackage;
            public sealed class GeneratedContract { }
            """);
        await File.WriteAllTextAsync(Path.Combine(project, "Sample.cs"), """
            namespace Sample;
            public sealed class Consumer
            {
                public ExternalPackage.GeneratedContract Value { get; } = new();
            }
            """);
        var escapedExternalFile = externalFile
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(project, "Sample.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="{{escapedExternalFile}}" Link="Generated/ExternalSdkSource.cs" />
              </ItemGroup>
            </Project>
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var factsPath = Path.Combine(outputPath, "facts.ndjson");
        var facts = (await File.ReadAllLinesAsync(factsPath))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToArray();
        var syntheticPath = Assert.Single(facts, fact =>
            fact.GetProperty("factType").GetString() == FactTypes.TypeDeclared
            && fact.GetProperty("targetSymbol").GetString() == "global::ExternalPackage.GeneratedContract")
            .GetProperty("evidence")
            .GetProperty("filePath")
            .GetString();
        Assert.NotNull(syntheticPath);

        return new FixtureResult(
            externalFile,
            externalRoot,
            facts,
            syntheticPath,
            [
                Path.Combine(outputPath, "scan-manifest.json"),
                factsPath,
                Path.Combine(outputPath, "index.sqlite"),
                Path.Combine(outputPath, "report.md"),
                Path.Combine(outputPath, "logs", "analyzer.log")
            ]);
    }

    private sealed record FixtureResult(
        string ExternalFile,
        string ExternalRoot,
        IReadOnlyList<JsonElement> Facts,
        string SyntheticPath,
        IReadOnlyList<string> StandardOutputs);
}
