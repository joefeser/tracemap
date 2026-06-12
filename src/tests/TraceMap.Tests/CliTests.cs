using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class CliTests
{
    [Fact]
    public async Task Help_for_scan_returns_usage()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("tracemap scan --repo <path> --out <path>", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Scan_against_temporary_directory_writes_required_outputs()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "TraceMap.Sample.sln"), "");
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(Path.Combine(repo, "src", "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Example.Package" Version="1.2.3" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "appsettings.json"), "{}");
        File.WriteAllText(Path.Combine(repo, "schema.sql"), "select 1;");

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(outputPath, "scan-manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputPath, "facts.ndjson")));
        Assert.True(File.Exists(Path.Combine(outputPath, "index.sqlite")));
        Assert.True(File.Exists(Path.Combine(outputPath, "report.md")));
        Assert.True(File.Exists(Path.Combine(outputPath, "logs", "analyzer.log")));
        var facts = await File.ReadAllTextAsync(Path.Combine(outputPath, "facts.ndjson"));
        Assert.Contains("\"factType\":\"PackageReferenced\"", facts);
        Assert.Contains("\"factType\":\"SqlFileDeclared\"", facts);
        Assert.Contains("\"analysisLevel\": \"Level3SyntaxAnalysis\"", await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-manifest.json")));
        Assert.Equal(string.Empty, error.ToString());
    }
}
