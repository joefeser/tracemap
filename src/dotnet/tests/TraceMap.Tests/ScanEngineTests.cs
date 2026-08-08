using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Cli;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ScanEngineTests
{
    [Fact]
    public void File_inventory_retains_the_legacy_two_parameter_binary_contract()
    {
        var method = typeof(FileInventory).GetMethod(
            nameof(FileInventory.Collect),
            [typeof(string), typeof(string)]);

        Assert.NotNull(method);
    }

    [Theory]
    [InlineData("not-a-sha")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Scan_manifest_rejects_malformed_source_snapshot_digests(string digest)
    {
        var manifest = new ScanManifest(
            "scan-id", "repo", null, null, "commit", "scanner", DateTimeOffset.UtcNow,
            "Level1SemanticAnalysis", "Succeeded", [], [], [], [], SourceSnapshotDigest: digest);
        var json = JsonSerializer.Serialize(manifest, JsonOptions.Stable);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ScanManifest>(json, JsonOptions.Stable));
    }

    [Fact]
    public void Scan_manifest_accepts_null_and_valid_source_snapshot_digests()
    {
        foreach (var digest in new string?[] { null, new('a', 64) })
        {
            var manifest = new ScanManifest(
                "scan-id", "repo", null, null, "commit", "scanner", DateTimeOffset.UtcNow,
                "Level1SemanticAnalysis", "Succeeded", [], [], [], [], SourceSnapshotDigest: digest);
            var json = JsonSerializer.Serialize(manifest, JsonOptions.Stable);

            Assert.Equal(digest, JsonSerializer.Deserialize<ScanManifest>(json, JsonOptions.Stable)!.SourceSnapshotDigest);
        }
    }

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
    public async Task Scan_does_not_enter_an_explicitly_excluded_inaccessible_directory()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var temp = new TempDirectory();
        var sourceDirectory = Path.Combine(temp.Path, "restricted");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "Hidden.cs"), "public sealed class Hidden { }");
        File.WriteAllText(Path.Combine(temp.Path, "Visible.cs"), "public sealed class Visible { }");

        var originalMode = File.GetUnixFileMode(sourceDirectory);
        try
        {
            File.SetUnixFileMode(sourceDirectory, UnixFileMode.None);
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = await TraceMapCommand.RunAsync(
                ["scan", "--repo", temp.Path, "--out", outputPath, "--exclude", "restricted/**"],
                stdout,
                stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, stderr.ToString());
            Assert.True(File.Exists(Path.Combine(outputPath, "scan-manifest.json")));
            var facts = File.ReadLines(Path.Combine(outputPath, "facts.ndjson"))
                .Select(line => JsonSerializer.Deserialize<CodeFact>(line, JsonOptions.StableLine)!)
                .ToArray();
            Assert.DoesNotContain(facts, fact => fact.Evidence.FilePath.StartsWith("restricted/", StringComparison.Ordinal));
        }
        finally
        {
            File.SetUnixFileMode(sourceDirectory, originalMode);
        }
    }

    [Fact]
    public void Semantic_input_guard_detects_same_size_source_changes_across_project_loading()
    {
        using var temp = new TempDirectory();
        const string relativePath = "Sample.cs";
        var sourcePath = Path.Combine(temp.Path, relativePath);
        File.WriteAllText(sourcePath, "public sealed class Alpha { }");
        var inventory = new[] { new FileInventoryItem(relativePath, "CSharp", new FileInfo(sourcePath).Length) };
        var baseline = ScanEngine.CaptureSemanticInputSnapshot(temp.Path, inventory);
        var semanticResult = new SemanticExtractionResult([], [], true, false, new HashSet<string>(StringComparer.Ordinal) { relativePath });

        File.WriteAllText(sourcePath, "public sealed class Bravo { }");

        var exception = Assert.Throws<SourceSnapshotException>(() =>
            ScanEngine.VerifySemanticInputSnapshot(temp.Path, inventory, semanticResult, baseline));
        Assert.Equal(SourceSnapshotException.ErrorCode, exception.Message);
    }

    [Fact]
    public void Scoped_scan_identity_includes_out_of_scope_msbuild_metadata_that_can_affect_semantics()
    {
        using var temp = new TempDirectory();
        var sourcePath = Path.Combine(temp.Path, "Sample.cs");
        var propsPath = Path.Combine(temp.Path, "Directory.Build.props");
        File.WriteAllText(sourcePath, "public sealed class Sample { }");
        File.WriteAllText(propsPath, "<Project><PropertyGroup><DefineConstants>ALPHA</DefineConstants></PropertyGroup></Project>");
        var options = new ScanOptions(
            temp.Path,
            Path.Combine(temp.Path, "out"),
            IncludeGlobs: ["**/*.cs"]);

        var before = ScanEngine.Scan(options);
        File.WriteAllText(propsPath, "<Project><PropertyGroup><DefineConstants>BRAVO</DefineConstants></PropertyGroup></Project>");
        var after = ScanEngine.Scan(options with { OutputPath = Path.Combine(temp.Path, "out-after") });

        Assert.DoesNotContain(before.Inventory, item => item.RelativePath == "Directory.Build.props");
        Assert.NotEqual(before.Manifest.SourceSnapshotDigest, after.Manifest.SourceSnapshotDigest);
        Assert.NotEqual(before.Manifest.ScanId, after.Manifest.ScanId);
    }

    [Fact]
    public void Recursive_include_glob_matches_zero_or_multiple_directory_levels()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "nested"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "Root.cs"), "public sealed class Root { }");
        File.WriteAllText(Path.Combine(temp.Path, "src", "nested", "Nested.cs"), "public sealed class Nested { }");
        File.WriteAllText(Path.Combine(temp.Path, "Other.cs"), "public sealed class Other { }");

        var result = ScanEngine.Scan(new ScanOptions(
            temp.Path,
            Path.Combine(temp.Path, "out"),
            IncludeGlobs: ["src/**/*.cs"]));

        Assert.Contains(result.Inventory, item => item.RelativePath == "src/Root.cs");
        Assert.Contains(result.Inventory, item => item.RelativePath == "src/nested/Nested.cs");
        Assert.DoesNotContain(result.Inventory, item => item.RelativePath == "Other.cs");
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
        var repeat = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "repeat")));
        Assert.Equal(before.Manifest.ScanId, repeat.Manifest.ScanId);
        Assert.Equal(before.Manifest.SourceSnapshotDigest, repeat.Manifest.SourceSnapshotDigest);
        Assert.Equal(
            ExpectedSourceSnapshotDigest(("Sample.cs", "CSharp", Encoding.UTF8.GetBytes("public sealed class Alpha { }"))),
            before.Manifest.SourceSnapshotDigest);
        File.WriteAllText(sourcePath, "public sealed class Bravo { }");
        var after = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "after")));

        Assert.Equal(before.Manifest.CommitSha, after.Manifest.CommitSha);
        Assert.Equal(before.Inventory.Single().SizeBytes, after.Inventory.Single().SizeBytes);
        Assert.NotEqual(before.Manifest.ScanId, after.Manifest.ScanId);
        Assert.NotEqual(before.Manifest.SourceSnapshotDigest, after.Manifest.SourceSnapshotDigest);
        Assert.Matches("^[0-9a-f]{64}$", before.Manifest.SourceSnapshotDigest!);
        Assert.Matches("^[0-9a-f]{64}$", after.Manifest.SourceSnapshotDigest!);

        var manifestPath = Path.Combine(temp.Path, "after", "scan-manifest.json");
        var factsPath = Path.Combine(temp.Path, "after", "facts.ndjson");
        var indexPath = Path.Combine(temp.Path, "after", "index.sqlite");
        await ManifestWriter.WriteAsync(manifestPath, after.Manifest);
        await JsonlFactWriter.WriteAsync(factsPath, after.Facts);
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
        var persistedFacts = File.ReadLines(factsPath)
            .Select(line => JsonSerializer.Deserialize<CodeFact>(line, JsonOptions.StableLine)!)
            .ToArray();
        Assert.NotEmpty(persistedFacts);
        Assert.All(persistedFacts, fact => Assert.Equal(after.Manifest.ScanId, fact.ScanId));
        command.CommandText = "select count(distinct scan_id), min(scan_id) from facts;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(after.Manifest.ScanId, reader.GetString(1));
    }

    private static string ExpectedSourceSnapshotDigest(params (string Path, string Kind, byte[] Content)[] items)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var length = new byte[sizeof(long)];
        foreach (var item in items.OrderBy(item => item.Path, StringComparer.Ordinal))
        {
            Append(item.Path);
            Append(item.Kind);
            BinaryPrimitives.WriteInt64BigEndian(length, item.Content.LongLength);
            hash.AppendData(length);
            hash.AppendData(item.Content);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

        void Append(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt64BigEndian(length, bytes.LongLength);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
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
