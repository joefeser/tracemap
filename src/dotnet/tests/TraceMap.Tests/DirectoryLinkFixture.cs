using System.Diagnostics;

namespace TraceMap.Tests;

internal static class DirectoryLinkFixture
{
    public static void Create(string link, string target)
    {
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(link, target); return; }
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
    }
}
