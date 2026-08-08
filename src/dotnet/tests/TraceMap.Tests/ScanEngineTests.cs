using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Cli;
using TraceMap.Storage;

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

    [Fact]
    public async Task Scan_identity_changes_when_committed_source_bytes_change_without_changing_size_or_head()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        var sourcePath = Path.Combine(repo, "Sample.cs");
        File.WriteAllText(sourcePath, "public sealed class Alpha { }");
        RunGit(repo, "init");
        RunGit(repo, "config", "user.email", "fixture@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Fixture");
        RunGit(repo, "add", "Sample.cs");
        RunGit(repo, "commit", "-m", "baseline");

        var before = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "before")));
        File.WriteAllText(sourcePath, "public sealed class Bravo { }");
        var after = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "after")));

        Assert.Equal(before.Manifest.CommitSha, after.Manifest.CommitSha);
        Assert.Equal(before.Inventory.Single().SizeBytes, after.Inventory.Single().SizeBytes);
        Assert.NotEqual(before.Manifest.ScanId, after.Manifest.ScanId);
        Assert.NotEqual(before.Manifest.SourceSnapshotDigest, after.Manifest.SourceSnapshotDigest);
        Assert.Matches("^[0-9a-f]{64}$", before.Manifest.SourceSnapshotDigest!);
        Assert.Matches("^[0-9a-f]{64}$", after.Manifest.SourceSnapshotDigest!);

        var manifestPath = Path.Combine(temp.Path, "after", "scan-manifest.json");
        var indexPath = Path.Combine(temp.Path, "after", "index.sqlite");
        await ManifestWriter.WriteAsync(manifestPath, after.Manifest);
        SqliteIndexWriter.Write(indexPath, after.Manifest, after.Facts);

        var fileManifest = JsonSerializer.Deserialize<ScanManifest>(
            await File.ReadAllTextAsync(manifestPath),
            JsonOptions.Stable)!;
        using var connection = new SqliteConnection($"Data Source={indexPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select manifest_json from scan_manifest limit 1;";
        var indexManifest = JsonSerializer.Deserialize<ScanManifest>(
            (string)command.ExecuteScalar()!,
            JsonOptions.Stable)!;

        Assert.Equal(after.Manifest.ScanId, fileManifest.ScanId);
        Assert.Equal(after.Manifest.SourceSnapshotDigest, fileManifest.SourceSnapshotDigest);
        Assert.Equal(after.Manifest.ScanId, indexManifest.ScanId);
        Assert.Equal(after.Manifest.SourceSnapshotDigest, indexManifest.SourceSnapshotDigest);
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }
}
