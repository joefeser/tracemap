using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class RuntimeInventoryBoundaryTests
{
    [Theory]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(WebFormsReviewExecutionCommand.MaxRuntimeInventoryEntries)]
    public async Task Bounded_distribution_hashes_every_candidate(int count)
    {
        using var temp = new TempDirectory();
        for (var i = 0; i < count; i++)
            File.WriteAllText(Path.Combine(temp.Path, $"Dependency{i:D4}.dll"), "public synthetic runtime");
        var original = await WebFormsReviewExecutionCommand.RuntimeDigestAsync(temp.Path, default);
        Assert.Equal(original, await WebFormsReviewExecutionCommand.RuntimeDigestAsync(temp.Path, default));
        File.AppendAllText(Path.Combine(temp.Path, $"Dependency{count - 1:D4}.dll"), "changed");
        Assert.NotEqual(original, await WebFormsReviewExecutionCommand.RuntimeDigestAsync(temp.Path, default));
    }

    [Fact]
    public async Task Entry_limit_counts_ignored_files_and_refuses_overflow()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Tool.dll"), "public");
        for (var i = 1; i < WebFormsReviewExecutionCommand.MaxRuntimeInventoryEntries; i++)
            File.WriteAllText(Path.Combine(temp.Path, $"Ignored{i}.txt"), "");
        await WebFormsReviewExecutionCommand.RuntimeDigestAsync(temp.Path, default);
        File.WriteAllText(Path.Combine(temp.Path, "Overflow.dll"), "public");
        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            WebFormsReviewExecutionCommand.RuntimeDigestAsync(temp.Path, default));
        Assert.Equal("WEBFORMS_EXECUTION_RUNTIME_INVENTORY_INVALID", error.Message);
    }

    [Fact]
    public async Task Nested_satellites_and_hosts_remain_pinned_and_cancellable()
    {
        using var temp = new TempDirectory();
        var nested = Path.Combine(temp.Path, "BuildHost", "fr");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(temp.Path, "Tool.deps.json"), "{}");
        var satellite = Path.Combine(nested, "Host.resources.dll");
        File.WriteAllText(satellite, "public satellite");
        var original = await WebFormsReviewExecutionCommand.RuntimeDigestAsync(temp.Path, default);
        File.AppendAllText(satellite, "changed");
        Assert.NotEqual(original, await WebFormsReviewExecutionCommand.RuntimeDigestAsync(temp.Path, default));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            WebFormsReviewExecutionCommand.RuntimeDigestAsync(temp.Path, new CancellationToken(true)));
    }

    [Fact]
    public async Task Oversized_file_is_still_rejected_before_hashing()
    {
        using var temp = new TempDirectory();
        using (var file = File.Create(Path.Combine(temp.Path, "Oversized.dll")))
            file.SetLength(268_435_457);
        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            WebFormsReviewExecutionCommand.RuntimeDigestAsync(temp.Path, default));
        Assert.Equal("WEBFORMS_PREFLIGHT_FILE_BYTES_LIMIT", error.Message);
    }
}
