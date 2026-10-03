using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class SourceSnapshotInspectorTests
{
    [Fact]
    public void Ordered_stream_uses_the_exact_scanner_snapshot_framing_without_discovery()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "One.cs"), "public class One { }");
        File.WriteAllText(Path.Combine(temp.Path, "Two.vb"), "Public Class Two\nEnd Class\n");
        File.WriteAllText(Path.Combine(temp.Path, "NotDeclared.cs"), "not inventoried");
        var roster = new[] { Item("One.cs", "CSharp"), Item("Two.vb", "VisualBasic") };
        var observed = SourceSnapshotInspector.InspectOrderedInventory(temp.Path, roster, 2, 1024);
        Assert.Equal(ScanEngine.CreateSourceSnapshotDigest(temp.Path, roster), observed.Digest);
        Assert.Equal(2, observed.FileCount);
        Assert.Equal(roster.Sum(item => item.SizeBytes), observed.Bytes);
        Assert.Equal(3, Directory.GetFiles(temp.Path).Length);

        FileInventoryItem Item(string path, string kind) => new(path, kind, new FileInfo(Path.Combine(temp.Path, path)).Length);
    }

    [Theory]
    [InlineData("../other.cs")]
    [InlineData("/other.cs")]
    [InlineData("a\\b.cs")]
    [InlineData("a//b.cs")]
    [InlineData("a/./b.cs")]
    [InlineData("a:b.cs")]
    public void Unsafe_retained_locators_are_rejected_before_access(string path)
    {
        using var temp = new TempDirectory();
        var error = Assert.Throws<InvalidOperationException>(() =>
            SourceSnapshotInspector.InspectOrderedInventory(temp.Path, [new(path, "CSharp", 1)], 10, 1024));
        Assert.Equal("SourceSnapshotInventoryInvalid", error.Message);
    }

    [Theory]
    [InlineData("empty", "SourceSnapshotInventoryEmpty")]
    [InlineData("duplicate", "SourceSnapshotInventoryInvalid")]
    [InlineData("unordered", "SourceSnapshotInventoryInvalid")]
    [InlineData("files", "SourceSnapshotInputLimit")]
    [InlineData("bytes", "SourceSnapshotInputLimit")]
    [InlineData("negative", "SourceSnapshotInventoryInvalid")]
    public void Empty_ambiguous_unordered_and_over_budget_rosters_fail(string kind, string code)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "a.cs"), "a");
        File.WriteAllText(Path.Combine(temp.Path, "b.cs"), "b");
        FileInventoryItem a = new("a.cs", "CSharp", 1), b = new("b.cs", "CSharp", 1);
        FileInventoryItem[] roster = kind switch
        {
            "empty" => [], "duplicate" => [a, a], "unordered" => [b, a],
            "negative" => [a with { SizeBytes = -1 }], _ => [a, b]
        };
        var error = Assert.Throws<InvalidOperationException>(() =>
            SourceSnapshotInspector.InspectOrderedInventory(temp.Path, roster,
                kind == "files" ? 1 : 10, kind == "bytes" ? 1 : 1024));
        Assert.Equal(code, error.Message);
    }

    [Fact]
    public void Cancellation_between_members_stops_streaming()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "a.cs"), "a");
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() =>
            SourceSnapshotInspector.InspectOrderedInventory(temp.Path, Roster(), 10, 1024, cancellation.Token));

        IEnumerable<FileInventoryItem> Roster()
        {
            yield return new("a.cs", "CSharp", 1);
            cancellation.Cancel();
            yield return new("b.cs", "CSharp", 1);
        }
    }
}
