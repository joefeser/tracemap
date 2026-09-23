using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class ExactSourceScopeTests
{
    [Fact]
    public void Complete_literal_selection_is_admitted()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "A.cs"), "public class A {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "B.cs"), "public class B {}\n");

        var scan = ScanEngine.Scan(Options(temp.Path, ["A.cs", "B.cs"]));

        Assert.Equal(2, scan.Inventory.Count(item => item.Kind == "CSharp"));
    }

    [Fact]
    public void Omitted_compilation_input_is_rejected_before_semantic_analysis()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Slice.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        File.WriteAllText(Path.Combine(temp.Path, "A.cs"), "public class A {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "B.cs"), "public class B {}\n");

        var error = Assert.Throws<InvalidOperationException>(() =>
            ScanEngine.Scan(Options(temp.Path, ["Slice.csproj", "A.cs"])));

        Assert.Equal("ExactSourceScopeInventoryMismatch", error.Message);
    }

    [Fact]
    public void Unsupported_selected_file_is_rejected()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "A.cs"), "public class A {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "README.txt"), "not inventoried\n");

        var error = Assert.Throws<InvalidOperationException>(() =>
            ScanEngine.Scan(Options(temp.Path, ["A.cs", "README.txt"])));

        Assert.Equal("ExactSourceScopeInventoryMismatch", error.Message);
    }

    [Fact]
    public void File_and_byte_limits_are_hard()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "A.cs"), "public class A {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "B.cs"), "public class B {}\n");

        Assert.Equal("ExactSourceScopeLimitExceeded", Assert.Throws<InvalidOperationException>(() =>
            ScanEngine.Scan(Options(temp.Path, ["A.cs", "B.cs"]) with { ExactSourceMaxFiles = 1 })).Message);
        Assert.Equal("ExactSourceScopeLimitExceeded", Assert.Throws<InvalidOperationException>(() =>
            ScanEngine.Scan(Options(temp.Path, ["A.cs", "B.cs"]) with { ExactSourceMaxBytes = 1 })).Message);
    }

    [Fact]
    public void Candidate_enumeration_is_bounded_before_inventory_materialization()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "A.cs"), "public class A {}\n");
        for (var index = 0; index < 1_025; index++)
            File.WriteAllText(Path.Combine(temp.Path, $"ignored-{index:D4}.txt"), string.Empty);

        Assert.Equal(1_026, Directory.EnumerateFiles(temp.Path).Count());
        Assert.Equal("ExactSourceScopeEnumerationLimitExceeded", Assert.Throws<InvalidOperationException>(() =>
            FileInventory.Collect(temp.Path, null, null, null, ["A.cs"], maxEnumerationEntries: 1_024)).Message);

        var error = Assert.Throws<InvalidOperationException>(() =>
            ScanEngine.Scan(Options(temp.Path, ["A.cs"]) with { ExactSourceMaxFiles = 1 }));

        Assert.Equal("ExactSourceScopeEnumerationLimitExceeded", error.Message);
    }

    [Fact]
    public async Task Cli_exact_source_scope_rejects_unselected_source()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "A.cs"), "public class A {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "B.cs"), "public class B {}\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await TraceMapCommand.RunAsync(
            ["scan", "--repo", temp.Path, "--out", Path.Combine(temp.Path, ".tracemap"),
                "--exact-source-scope", "--include", "A.cs"], output, error);

        Assert.Equal(1, exit);
        Assert.Contains("error: ExactSourceScopeInventoryMismatch", error.ToString(), StringComparison.Ordinal);
    }

    private static ScanOptions Options(string repo, IReadOnlyList<string> paths) =>
        new(repo, Path.Combine(repo, ".tracemap"), IncludeGlobs: paths, ExactSourceScope: true);
}
