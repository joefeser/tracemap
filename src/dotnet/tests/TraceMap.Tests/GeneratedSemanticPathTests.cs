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
                AssertPathAbsent(temp.Path, output);
                AssertPathAbsent(fixture.ExternalRoot, output);
                AssertPathAbsent(fixture.ExternalFile, output);
                Assert.DoesNotContain("example.generator", output, StringComparison.OrdinalIgnoreCase);
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(home))
                {
                    AssertPathAbsent(home, output);
                }
            }
        }
    }

    [Fact]
    public void Every_semantic_rule_documents_external_source_path_projection()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        var ruleIds = new[]
        {
            "csharp.semantic.propertyaccess.v1",
            "csharp.semantic.methodinvocation.v1",
            "csharp.semantic.callgraph.v1",
            "csharp.semantic.objectcreation.v1",
            "csharp.semantic.valueflow.v1",
            "csharp.semantic.localalias.v1",
            "csharp.semantic.fieldalias.v1",
            "csharp.semantic.parameterforwarding.v1",
            "csharp.semantic.symbolidentity.v1",
            "csharp.semantic.symbolrelationship.v1",
            "csharp.semantic.flowboundary.v1",
            "csharp.semantic.runtimeevidence.v1",
            "csharp.semantic.contractmapping.v1",
            "csharp.semantic.declarations.v1"
        };

        foreach (var ruleId in ruleIds)
        {
            var start = catalog.IndexOf($"  - id: {ruleId}\n", StringComparison.Ordinal);
            Assert.True(start >= 0, $"Missing rule catalog entry for {ruleId}.");
            var end = catalog.IndexOf("\n  - id: ", start + 1, StringComparison.Ordinal);
            var section = catalog[start..(end < 0 ? catalog.Length : end)];
            Assert.Contains("External Roslyn source paths use deterministic synthetic identities", section, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void External_windows_drive_and_unc_paths_are_always_projected()
    {
        var repoPath = OperatingSystem.IsWindows() ? @"C:\repository" : "/repository";
        foreach (var externalPath in new[]
        {
            @"D:\sdk\10.0.100\GeneratedContract.cs",
            @"\\external-host\package-cache\GeneratedContract.cs"
        })
        {
            var projected = CSharpSemanticExtractor.ToRelativePath(repoPath, externalPath);

            Assert.StartsWith("__external__/csharp-", projected, StringComparison.Ordinal);
            Assert.False(Path.IsPathRooted(projected), projected);
            Assert.DoesNotMatch(@"^[A-Za-z]:[\\/]", projected);
            Assert.False(projected.StartsWith("//", StringComparison.Ordinal), projected);
        }
    }

    [Fact]
    public void Filesystem_root_repository_preserves_child_paths()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()));
        Assert.False(string.IsNullOrWhiteSpace(root));
        var child = Path.Combine(root, "tracemap-root-child", "GeneratedContract.cs");

        var projected = CSharpSemanticExtractor.ToRelativePath(root, child);

        Assert.Equal("tracemap-root-child/GeneratedContract.cs", projected);
        Assert.False(projected.StartsWith("__external__/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Checked_in_external_namespace_path_does_not_emit_projection_gap()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var sourceDirectory = Path.Combine(repo, "__external__");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(repo, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "csharp-contract.cs"), """
            namespace Sample;
            public sealed class CheckedInContract { }
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var facts = (await File.ReadAllLinesAsync(Path.Combine(outputPath, "facts.ndjson")))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToArray();
        Assert.Contains(facts, fact =>
            fact.GetProperty("factType").GetString() == FactTypes.TypeDeclared
            && fact.GetProperty("targetSymbol").GetString() == "global::Sample.CheckedInContract"
            && fact.GetProperty("evidence").GetProperty("filePath").GetString() == "__external__/csharp-contract.cs");
        Assert.DoesNotContain(facts, fact =>
            fact.GetProperty("factType").GetString() == FactTypes.AnalysisGap
            && fact.GetProperty("properties").TryGetProperty("gapKind", out var gapKind)
            && gapKind.GetString() == "ExternalSourcePathProjected");
    }

    [Fact]
    public async Task Checked_in_unix_drive_like_path_does_not_emit_projection_gap()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var sourceDirectory = Path.Combine(repo, "C:");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(repo, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "Contract.cs"), """
            namespace Sample;
            public sealed class DriveLikeContract { }
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var facts = (await File.ReadAllLinesAsync(Path.Combine(outputPath, "facts.ndjson")))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToArray();
        Assert.Contains(facts, fact =>
            fact.GetProperty("factType").GetString() == FactTypes.TypeDeclared
            && fact.GetProperty("targetSymbol").GetString() == "global::Sample.DriveLikeContract"
            && fact.GetProperty("evidence").GetProperty("filePath").GetString() == "C:/Contract.cs");
        Assert.DoesNotContain(facts, fact =>
            fact.GetProperty("factType").GetString() == FactTypes.AnalysisGap
            && fact.GetProperty("properties").TryGetProperty("gapKind", out var gapKind)
            && gapKind.GetString() == "ExternalSourcePathProjected");
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

    private static void AssertPathAbsent(string path, string output)
    {
        Assert.DoesNotContain(path, output, StringComparison.Ordinal);
        Assert.DoesNotContain(path.Replace('\\', '/'), output, StringComparison.Ordinal);
        Assert.DoesNotContain(path.Replace('/', '\\'), output, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "rules", "rule-catalog.yml")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed record FixtureResult(
        string ExternalFile,
        string ExternalRoot,
        IReadOnlyList<JsonElement> Facts,
        string SyntheticPath,
        IReadOnlyList<string> StandardOutputs);
}
