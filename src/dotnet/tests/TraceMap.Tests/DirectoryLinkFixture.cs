using System.Diagnostics;

namespace TraceMap.Tests;

internal sealed class DirectoryLinkFixture(string link) : IDisposable
{
    private bool disposed;

    public static DirectoryLinkFixture Create(string link, string target)
    {
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(link, target); return new(link); }
        using var process = new Process { StartInfo = new("cmd.exe")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Junction creation did not exit.");
        }
        Assert.True(process.ExitCode == 0, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
        Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
        return new(link);
    }

    public void Dispose()
    {
        if (disposed) return;
        // Remove only the link, before recursive fixture cleanup can remove its
        // target and leave a dangling Windows junction. Never traverse the target.
        Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
        Directory.Delete(link, recursive: false);
        disposed = true;
    }
}

public sealed class DirectoryLinkFixtureTests
{
    [Fact]
    public void Disposing_directory_link_preserves_target_and_allows_fixture_cleanup()
    {
        using var temp = new TempDirectory();
        var target = Path.Combine(temp.Path, "target");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "keep.txt");
        File.WriteAllText(marker, "public fixture");
        var path = Path.Combine(temp.Path, "link");
        using var link = DirectoryLinkFixture.Create(path, target);
        Assert.Equal("public fixture", File.ReadAllText(Path.Combine(path, "keep.txt")));
        link.Dispose();
        Assert.False(Directory.Exists(path));
        Assert.Equal("public fixture", File.ReadAllText(marker));
    }
}
