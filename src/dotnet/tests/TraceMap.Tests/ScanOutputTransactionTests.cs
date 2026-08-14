using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class ScanOutputTransactionTests
{
    [Fact]
    public async Task Staged_artifact_failure_preserves_prior_complete_output()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        var output = Path.Combine(temp.Path, "output");
        await ScanOutputTransaction.WriteAsync(output, repo, staging => WritePacketAsync(staging, "baseline"));
        var baseline = await File.ReadAllBytesAsync(Path.Combine(output, "scan-manifest.json"));

        await Assert.ThrowsAsync<IOException>(() => ScanOutputTransaction.WriteAsync(output, repo, async staging =>
        {
            Directory.CreateDirectory(Path.Combine(staging, "logs"));
            await File.WriteAllTextAsync(Path.Combine(staging, "scan-manifest.json"), "replacement");
            throw new IOException("SyntheticArtifactWriteFailure");
        }));

        Assert.Equal(baseline, await File.ReadAllBytesAsync(Path.Combine(output, "scan-manifest.json")));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(temp.Path), path => Path.GetFileName(path).StartsWith(".tracemap-output-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Complete_artifacts_cannot_make_a_repository_replaceable()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        await WritePacketAsync(repo, "baseline");
        var sentinel = Path.Combine(repo, "source.cs");
        File.WriteAllText(sentinel, "internal sealed class Sentinel { }");

        var error = Assert.Throws<IOException>(() => ScanOutputTransaction.ValidateTarget(repo, repo));

        Assert.Equal("OutputArtifactSetNotReplaceable", error.Message);
        Assert.True(File.Exists(sentinel));
    }

    [Fact]
    public async Task Complete_artifacts_do_not_authorize_replacing_unknown_caller_files()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(repo);
        await WritePacketAsync(output, "baseline");
        var sentinel = Path.Combine(output, "caller-owned.txt");
        await File.WriteAllTextAsync(sentinel, "keep");

        var error = Assert.Throws<IOException>(() => ScanOutputTransaction.ValidateTarget(output, repo));

        Assert.Equal("OutputArtifactSetNotReplaceable", error.Message);
        Assert.Equal("keep", await File.ReadAllTextAsync(sentinel));
    }

    [Fact]
    public void Failure_receipt_eligibility_never_propagates_target_validation_errors()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);

        Assert.False(ScanOutputTransaction.CanWriteFailureReceipt("\0", repo));
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
