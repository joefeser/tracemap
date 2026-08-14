using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class ScanOutputTransactionTests
{
    [Fact]
    public async Task Staged_artifact_failure_preserves_prior_complete_output()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "output");
        await ScanOutputTransaction.WriteAsync(output, staging => WritePacketAsync(staging, "baseline"));
        var baseline = await File.ReadAllBytesAsync(Path.Combine(output, "scan-manifest.json"));

        await Assert.ThrowsAsync<IOException>(() => ScanOutputTransaction.WriteAsync(output, async staging =>
        {
            Directory.CreateDirectory(Path.Combine(staging, "logs"));
            await File.WriteAllTextAsync(Path.Combine(staging, "scan-manifest.json"), "replacement");
            throw new IOException("SyntheticArtifactWriteFailure");
        }));

        Assert.Equal(baseline, await File.ReadAllBytesAsync(Path.Combine(output, "scan-manifest.json")));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(temp.Path), path => Path.GetFileName(path).StartsWith(".tracemap-output-", StringComparison.Ordinal));
    }

    private static async Task WritePacketAsync(string staging, string value)
    {
        Directory.CreateDirectory(Path.Combine(staging, "logs"));
        foreach (var relative in new[] { "scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log" })
        {
            var path = Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar));
            await File.WriteAllTextAsync(path, value);
        }
    }
}
