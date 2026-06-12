using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class ScanEngineTests
{
    [Fact]
    public void Scan_creates_manifest_with_unknown_commit_outside_git_repo()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Sample.sln"), "");
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "App"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "App", "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Equal("unknown", result.Manifest.CommitSha);
        Assert.Contains("Sample.sln", result.Manifest.Solutions);
        Assert.Contains("src/App/App.csproj", result.Manifest.Projects);
        Assert.Contains("net10.0", result.Manifest.TargetFrameworks);
        Assert.Contains(result.Manifest.KnownGaps, gap => gap.Contains("commit SHA unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap);
    }

    [Fact]
    public void Scan_id_does_not_depend_on_absolute_repo_parent_path()
    {
        using var temp = new TempDirectory();
        var repoA = Path.Combine(temp.Path, "a", "repo");
        var repoB = Path.Combine(temp.Path, "b", "repo");
        Directory.CreateDirectory(repoA);
        Directory.CreateDirectory(repoB);
        File.WriteAllText(Path.Combine(repoA, "Sample.cs"), "public sealed class Sample { }");
        File.WriteAllText(Path.Combine(repoB, "Sample.cs"), "public sealed class Sample { }");

        var resultA = ScanEngine.Scan(new ScanOptions(repoA, Path.Combine(temp.Path, "out-a")));
        var resultB = ScanEngine.Scan(new ScanOptions(repoB, Path.Combine(temp.Path, "out-b")));

        Assert.Equal(resultA.Manifest.ScanId, resultB.Manifest.ScanId);
    }
}
