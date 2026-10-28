using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlSqlLifecycleSliceTests
{
    [Fact]
    public void Selections_are_exact_normalized_and_deduplicated()
    {
        var sha = new string('a', 64);
        var root = Assert.Single(IlLifecycleRootSelection.Parse([sha + ":0x0600000a", sha.ToUpperInvariant() + ":0X0600000A"]));
        Assert.Equal(sha, root.BinarySha256);
        Assert.Equal("0x0600000a", root.MetadataToken);
    }

    [Theory]
    [InlineData("0x06000000")]
    [InlineData("0x0a000001")]
    [InlineData("0x06000001:extra")]
    public void Non_methoddef_or_incomplete_selections_are_refused(string token)
        => Assert.Throws<ArgumentException>(() => IlLifecycleRootSelection.Parse([new string('a', 64) + ":" + token]));

    [Fact]
    public void Root_count_and_hash_bounds_are_enforced_before_inspection()
    {
        Assert.Throws<ArgumentException>(() => IlLifecycleRootSelection.Parse(["short:0x06000001"]));
        Assert.Throws<ArgumentException>(() => IlLifecycleRootSelection.Parse([new string('g', 64) + ":0x06000001"]));
        Assert.Throws<ArgumentException>(() => IlLifecycleRootSelection.Parse(
            Enumerable.Repeat(new string('a', 64) + ":0x06000001", 17).ToArray()));
        Assert.Empty(IlLifecycleRootSelection.Parse(null));
    }

    [Fact]
    public void Selection_requires_explicit_il_admission()
    {
        var options = new ScanOptions(".", ".") { IlLifecycleRoots = [new string('a', 64) + ":0x06000001"] };
        Assert.Throws<ArgumentException>(() => IlBodyEvidenceExtractor.Evaluate(options, new(null, [], [], [])));
    }
}
