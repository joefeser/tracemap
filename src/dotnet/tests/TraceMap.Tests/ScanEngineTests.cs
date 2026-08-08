using TraceMap.Core;
using TraceMap.Cli;

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

    [Fact]
    public async Task Scan_fails_truthfully_when_an_in_scope_source_directory_is_inaccessible()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var temp = new TempDirectory();
        var sourceDirectory = Path.Combine(temp.Path, "restricted");
        var sourcePath = Path.Combine(sourceDirectory, "Hidden.cs");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(sourcePath, "public sealed class Hidden { }");

        var originalMode = File.GetUnixFileMode(sourceDirectory);
        try
        {
            File.SetUnixFileMode(sourceDirectory, UnixFileMode.None);
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = await TraceMapCommand.RunAsync(
                ["scan", "--repo", temp.Path, "--out", outputPath],
                stdout,
                stderr);

            Assert.Equal(1, exitCode);
            Assert.Equal("error: SourceInventoryIncomplete" + Environment.NewLine, stderr.ToString());
            Assert.DoesNotContain(temp.Path, stderr.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(outputPath, "scan-manifest.json")));
        }
        finally
        {
            File.SetUnixFileMode(sourceDirectory, originalMode);
        }
    }
}
