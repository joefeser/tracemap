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
    public void Missing_project_compilation_input_is_a_reduced_coverage_gap_not_a_snapshot_change()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "MissingInput.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Default.aspx.cs" />
                <Compile Include="Missing.cs" />
                <Content Include="Default.aspx" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Legacy.Default" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            namespace Legacy;
            public partial class Default
            {
                protected void Save_Click(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.Contains("Reduced", result.Manifest.AnalysisLevel, StringComparison.Ordinal);
        Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsPageDeclared);
        Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared);
        Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsHandlerResolved);
        var gap = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "CompilationInputUnavailable");
        Assert.Equal(RuleIds.CSharpSemanticWorkspace, gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal("MissingInput.csproj", gap.Evidence.FilePath);
        Assert.Equal("MissingInput.csproj", gap.ProjectPath);
        Assert.Equal("CompilationInputUnavailable", gap.Properties.GetValueOrDefault("diagnosticCode"));
        Assert.Equal("reduces-semantic-coverage", gap.Properties.GetValueOrDefault("coverageEffect"));
        Assert.Equal("category-only", gap.Properties.GetValueOrDefault("sanitization"));
        Assert.DoesNotContain("Missing.cs", gap.Properties.GetValueOrDefault("message"), StringComparison.Ordinal);
    }

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
        var json = $$"""
            {
              "scanId": "scan-id",
              "repoName": "repo",
              "commitSha": "commit",
              "scannerVersion": "scanner",
              "scannedAt": "2026-08-08T00:00:00Z",
              "analysisLevel": "Level1SemanticAnalysis",
              "buildStatus": "Succeeded",
              "solutions": [],
              "projects": [],
              "targetFrameworks": [],
              "knownGaps": [],
              "sourceSnapshotDigest": "{{digest}}"
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ScanManifest>(json, JsonOptions.Stable));
        Assert.Throws<JsonException>(() => new ScanManifest(
            "scan-id", "repo", null, null, "commit", "scanner", DateTimeOffset.UtcNow,
            "Level1SemanticAnalysis", "Succeeded", [], [], [], [], SourceSnapshotDigest: digest));
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
    public async Task Scan_fails_truthfully_when_an_in_scope_source_is_a_symbolic_link()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var temp = new TempDirectory();
        var targetPath = Path.Combine(temp.Path, "Target.cs");
        var linkPath = Path.Combine(temp.Path, "Linked.cs");
        var outputPath = Path.Combine(temp.Path, "out");
        File.WriteAllText(targetPath, "public sealed class Target { }");
        File.CreateSymbolicLink(linkPath, targetPath);
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

    [Fact]
    public void Explicit_exclusion_remains_authoritative_for_a_symbolic_link()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var temp = new TempDirectory();
        var targetPath = Path.Combine(temp.Path, "Target.cs");
        var linkPath = Path.Combine(temp.Path, "Linked.cs");
        File.WriteAllText(targetPath, "public sealed class Target { }");
        File.CreateSymbolicLink(linkPath, targetPath);

        var inventory = FileInventory.Collect(
            temp.Path,
            Path.Combine(temp.Path, "out"),
            ["Linked.cs"],
            StringComparer.Ordinal);

        Assert.Contains(inventory, item => item.RelativePath == "Target.cs");
        Assert.DoesNotContain(inventory, item => item.RelativePath == "Linked.cs");
    }

    [Fact]
    public void Single_segment_exclude_filters_direct_files_without_pruning_nested_descendants()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "nested"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "Direct.cs"), "public sealed class Direct { }");
        File.WriteAllText(Path.Combine(temp.Path, "src", "nested", "Nested.cs"), "public sealed class Nested { }");

        var inventory = FileInventory.Collect(
            temp.Path,
            Path.Combine(temp.Path, "out"),
            ["src/*"],
            StringComparer.Ordinal);

        Assert.DoesNotContain(inventory, item => item.RelativePath == "src/Direct.cs");
        Assert.Contains(inventory, item => item.RelativePath == "src/nested/Nested.cs");
    }

    [Theory]
    [InlineData("restricted/**")]
    [InlineData("restricted/**/*")]
    [InlineData("**/restricted/**/*")]
    public void Recursive_directory_exclude_prunes_the_entire_matching_subtree(string excludeGlob)
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "restricted", "nested"));
        File.WriteAllText(Path.Combine(temp.Path, "restricted", "nested", "Hidden.cs"), "public sealed class Hidden { }");
        File.WriteAllText(Path.Combine(temp.Path, "Visible.cs"), "public sealed class Visible { }");

        var inventory = FileInventory.Collect(
            temp.Path,
            Path.Combine(temp.Path, "out"),
            [excludeGlob],
            StringComparer.Ordinal);

        Assert.DoesNotContain(inventory, item => item.RelativePath.StartsWith("restricted/", StringComparison.Ordinal));
        Assert.Contains(inventory, item => item.RelativePath == "Visible.cs");
    }

    [Fact]
    public void Source_snapshot_digest_rejects_inventory_size_that_does_not_match_read_bytes()
    {
        using var temp = new TempDirectory();
        const string relativePath = "Sample.cs";
        File.WriteAllText(Path.Combine(temp.Path, relativePath), "public sealed class Sample { }");
        var staleInventory = new[] { new FileInventoryItem(relativePath, "CSharp", 1) };

        var exception = Assert.Throws<SourceSnapshotException>(() =>
            ScanEngine.CreateSourceSnapshotDigest(temp.Path, staleInventory));

        Assert.Equal(SourceSnapshotException.ErrorCode, exception.Message);
    }

    [Fact]
    public void Source_snapshot_inventory_rejects_files_created_after_initial_discovery()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Existing.cs"), "public sealed class Existing { }");
        var initial = FileInventory.Collect(temp.Path);
        File.WriteAllText(Path.Combine(temp.Path, "Added.sql"), "-- synthetic fixture");
        var observed = FileInventory.Collect(temp.Path);

        var exception = Assert.Throws<SourceSnapshotException>(() =>
            ScanEngine.VerifySourceSnapshotInventory(initial, observed));

        Assert.Equal(SourceSnapshotException.ErrorCode, exception.Message);
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
    public void Semantic_input_guard_covers_compilation_documents_outside_fact_extraction_scope()
    {
        using var temp = new TempDirectory();
        const string selectedPath = "A.cs";
        const string compilationOnlyPath = "B.cs";
        File.WriteAllText(Path.Combine(temp.Path, selectedPath), "public sealed class A { }");
        File.WriteAllText(Path.Combine(temp.Path, compilationOnlyPath), "public sealed class Alpha { }");
        var inventory = FileInventory.Collect(temp.Path);
        var baseline = ScanEngine.CaptureSemanticInputSnapshot(temp.Path, inventory);
        var semanticResult = new SemanticExtractionResult(
            [],
            [],
            true,
            false,
            new HashSet<string>(StringComparer.Ordinal) { selectedPath },
            CompilationInputFiles: new HashSet<string>(StringComparer.Ordinal)
            {
                selectedPath,
                compilationOnlyPath
            });

        File.WriteAllText(Path.Combine(temp.Path, compilationOnlyPath), "public sealed class Bravo { }");

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
    public void Explicit_include_scope_preserves_projectless_files_outside_selected_project_directories()
    {
        using var temp = new TempDirectory();
        var webFormsDirectory = Path.Combine(temp.Path, "source", "web");
        var backendDirectory = Path.Combine(temp.Path, "source", "backend");
        var unrelatedDirectory = Path.Combine(temp.Path, "unrelated");
        Directory.CreateDirectory(webFormsDirectory);
        Directory.CreateDirectory(backendDirectory);
        Directory.CreateDirectory(unrelatedDirectory);
        File.WriteAllText(Path.Combine(webFormsDirectory, "Default.aspx"), "<%@ Page Language=\"C#\" %>");
        File.WriteAllText(Path.Combine(backendDirectory, "Backend.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(backendDirectory, "Backend.cs"), "public sealed class Backend { }");
        File.WriteAllText(Path.Combine(unrelatedDirectory, "Excluded.cs"), "public sealed class Excluded { }");

        var result = ScanEngine.Scan(new ScanOptions(
            temp.Path,
            Path.Combine(temp.Path, "out"),
            ProjectPaths: ["source/backend/Backend.csproj"],
            IncludeGlobs: ["source/web/**", "source/backend/**"]));

        Assert.Contains(result.Inventory, item => item.RelativePath == "source/web/Default.aspx");
        Assert.Contains(result.Inventory, item => item.RelativePath == "source/backend/Backend.csproj");
        Assert.Contains(result.Inventory, item => item.RelativePath == "source/backend/Backend.cs");
        Assert.DoesNotContain(result.Inventory, item => item.RelativePath == "unrelated/Excluded.cs");
    }

    [Fact]
    public void Project_scope_honors_case_insensitive_filesystem_path_matching()
    {
        using var temp = new TempDirectory();
        if (!CSharpSemanticExtractor.CreateSourcePathComparer(temp.Path).Equals("a", "A"))
            return;

        var projectDirectory = Path.Combine(temp.Path, "Source", "Backend");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, "Backend.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(projectDirectory, "Backend.cs"), "public sealed class Backend { }");

        var result = ScanEngine.Scan(new ScanOptions(
            temp.Path,
            Path.Combine(temp.Path, "out"),
            ProjectPaths: ["source/backend/backend.csproj"]));

        Assert.Contains(result.Inventory, item => item.RelativePath == "Source/Backend/Backend.csproj");
        Assert.Contains(result.Inventory, item => item.RelativePath == "Source/Backend/Backend.cs");
    }

    [Fact]
    public void Include_scope_does_not_enter_an_unrelated_symbolic_link_directory()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var temp = new TempDirectory();
        using var outside = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "Visible.cs"), "public sealed class Visible { }");
        File.WriteAllText(Path.Combine(outside.Path, "Hidden.cs"), "public sealed class Hidden { }");
        Directory.CreateSymbolicLink(Path.Combine(temp.Path, "vendor-link"), outside.Path);

        var result = ScanEngine.Scan(new ScanOptions(
            temp.Path,
            Path.Combine(temp.Path, "out"),
            IncludeGlobs: ["src/**"]));

        Assert.Contains(result.Inventory, item => item.RelativePath == "src/Visible.cs");
        Assert.DoesNotContain(result.Inventory, item => item.RelativePath.StartsWith("vendor-link", StringComparison.Ordinal));
    }

    [Fact]
    public void Unsupported_symbolic_link_file_does_not_block_source_inventory()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var temp = new TempDirectory();
        using var outside = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Visible.cs"), "public sealed class Visible { }");
        var notesPath = Path.Combine(outside.Path, "notes.txt");
        File.WriteAllText(notesPath, "synthetic notes");
        File.CreateSymbolicLink(Path.Combine(temp.Path, "latest.txt"), notesPath);

        var inventory = FileInventory.Collect(temp.Path);

        Assert.Contains(inventory, item => item.RelativePath == "Visible.cs");
        Assert.DoesNotContain(inventory, item => item.RelativePath == "latest.txt");
    }

    [Fact]
    public void Scoped_scan_digest_covers_repository_local_roslyn_compilation_inputs()
    {
        using var temp = new TempDirectory();
        var projectPath = Path.Combine(temp.Path, "App.csproj");
        var helperPath = Path.Combine(temp.Path, "B.cs");
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(temp.Path, "A.cs"),
            "public sealed class A { public int Read() => B.Value; }");
        File.WriteAllText(
            helperPath,
            "public static class B { public static int Value => 1; }");
        var options = new ScanOptions(
            temp.Path,
            Path.Combine(temp.Path, "before"),
            IncludeGlobs: ["App.csproj", "A.cs"]);

        var before = ScanEngine.Scan(options);
        File.WriteAllText(
            helperPath,
            "public static class B { public static int Value => 2; }");
        var after = ScanEngine.Scan(options with { OutputPath = Path.Combine(temp.Path, "after") });

        Assert.DoesNotContain(before.Inventory, item => item.RelativePath == "B.cs");
        Assert.NotEqual(before.Manifest.SourceSnapshotDigest, after.Manifest.SourceSnapshotDigest);
        Assert.NotEqual(before.Manifest.ScanId, after.Manifest.ScanId);
    }

    [Fact]
    public void Include_prefix_preserves_out_of_prefix_repository_local_compilation_inputs()
    {
        using var temp = new TempDirectory();
        var projectDirectory = Path.Combine(temp.Path, "src", "App");
        var sharedDirectory = Path.Combine(temp.Path, "shared");
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(sharedDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="App.cs" />
                <Compile Include="../../shared/Helper.cs" Link="Helper.cs" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(projectDirectory, "App.cs"),
            "public sealed class App { public int Read() => Helper.Value; }");
        var helperPath = Path.Combine(sharedDirectory, "Helper.cs");
        File.WriteAllText(
            helperPath,
            "public static class Helper { public static int Value => 1; }");
        var options = new ScanOptions(
            temp.Path,
            Path.Combine(temp.Path, "before"),
            IncludeGlobs: ["src/**"]);

        var before = ScanEngine.Scan(options);
        File.WriteAllText(
            helperPath,
            "public static class Helper { public static int Value => 2; }");
        var after = ScanEngine.Scan(options with { OutputPath = Path.Combine(temp.Path, "after") });

        Assert.DoesNotContain(before.Inventory, item => item.RelativePath == "shared/Helper.cs");
        Assert.NotEqual(before.Manifest.SourceSnapshotDigest, after.Manifest.SourceSnapshotDigest);
        Assert.NotEqual(before.Manifest.ScanId, after.Manifest.ScanId);
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

    [Fact]
    public void Scan_identity_distinguishes_delimiter_colliding_option_authority()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Sample.cs"), "public sealed class Sample { }");
        RunGit(repo, "init");
        RunGit(repo, "config", "user.email", "fixture@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Fixture");
        RunGit(repo, "add", "Sample.cs");
        RunGit(repo, "commit", "-m", "baseline");

        var oneValue = ScanEngine.Scan(new ScanOptions(
            repo,
            Path.Combine(temp.Path, "one"),
            ExcludeGlobs: ["foo,bar"]));
        var twoValues = ScanEngine.Scan(new ScanOptions(
            repo,
            Path.Combine(temp.Path, "two"),
            ExcludeGlobs: ["foo", "bar"]));

        Assert.Equal(oneValue.Manifest.SourceSnapshotDigest, twoValues.Manifest.SourceSnapshotDigest);
        Assert.NotEqual(oneValue.Manifest.ScanId, twoValues.Manifest.ScanId);
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
