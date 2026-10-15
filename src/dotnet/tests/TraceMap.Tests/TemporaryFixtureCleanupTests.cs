namespace TraceMap.Tests;

public sealed class TemporaryFixtureCleanupTests
{
    [Fact]
    public void Cleanup_removes_readonly_git_objects_without_following_external_links()
    {
        using var temp = new TempDirectory();
        var fixture = Path.Combine(temp.Path, "fixture");
        var objects = Path.Combine(fixture, ".git", "objects");
        Directory.CreateDirectory(objects);
        var gitObject = Path.Combine(objects, "public-object");
        File.WriteAllText(gitObject, "public fixture");
        File.SetAttributes(gitObject, File.GetAttributes(gitObject) | FileAttributes.ReadOnly);
        var target = Path.Combine(temp.Path, "outside-fixture");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "keep.txt");
        File.WriteAllText(marker, "keep");
        File.SetAttributes(marker, File.GetAttributes(marker) | FileAttributes.ReadOnly);
        // Cleanup owns this link, so it removes it without traversing its target.
        _ = DirectoryLinkFixture.Create(Path.Combine(fixture, "linked-target"), target);
        TempDirectory.DeleteTree(fixture);
        Assert.False(Directory.Exists(fixture));
        Assert.Equal("keep", File.ReadAllText(marker));
        Assert.True((File.GetAttributes(marker) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void Cleanup_refuses_a_root_outside_the_temporary_directory()
    {
        Assert.Throws<InvalidOperationException>(() => TempDirectory.DeleteTree(Path.GetPathRoot(Path.GetTempPath())!));
    }

    [Fact]
    public void Cleanup_refuses_a_linked_root_without_removing_the_target()
    {
        using var temp = new TempDirectory();
        var target = Path.Combine(temp.Path, "target");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "keep.txt");
        File.WriteAllText(marker, "keep");
        var alias = Path.Combine(temp.Path, "alias");
        using var link = DirectoryLinkFixture.Create(alias, target);
        Assert.Throws<InvalidOperationException>(() => TempDirectory.DeleteTree(alias));
        Assert.Equal("keep", File.ReadAllText(marker));
    }
}
