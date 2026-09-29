using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class GitMetadataOutputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Undrained_stdout_is_a_failed_probe_even_when_empty_output_is_allowed(bool allowEmpty)
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = GitMetadataProvider.CompleteGitOutput(pending.Task, Task.FromResult(""), allowEmpty, TimeSpan.Zero);
        Assert.True(result.Failed); Assert.Null(result.Output);
        pending.SetResult("retained output");
        var complete = GitMetadataProvider.CompleteGitOutput(pending.Task, Task.FromResult(""), allowEmpty, TimeSpan.Zero);
        Assert.False(complete.Failed); Assert.Equal("retained output", complete.Output);
    }

    [Fact]
    public void Undrained_stderr_cannot_be_reported_as_successful_complete_output()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = GitMetadataProvider.CompleteGitOutput(Task.FromResult("exact identity"), pending.Task, false, TimeSpan.Zero);
        Assert.True(result.Failed); Assert.Null(result.Output);
    }

    [Theory]
    [InlineData("", false, true)]
    [InlineData(" \n", false, true)]
    [InlineData("", true, false)]
    [InlineData(" \n", true, false)]
    [InlineData(" exact identity \n", false, false)]
    public void Completed_output_distinguishes_an_empty_prefix_from_missing_identity(string output, bool allowEmpty, bool failed)
    {
        var result = GitMetadataProvider.CompleteGitOutput(Task.FromResult(output), Task.FromResult(""), allowEmpty, TimeSpan.Zero);
        Assert.Equal(failed, result.Failed); Assert.Equal(failed ? null : output.Trim(), result.Output);
    }

    [Theory]
    [InlineData("stdout-fault")]
    [InlineData("stderr-fault")]
    [InlineData("stdout-cancel")]
    public void Faulted_or_cancelled_pipe_reads_are_failed_probes(string scenario)
    {
        var output = scenario == "stdout-fault" ? Task.FromException<string>(new IOException("public read failure"))
            : scenario == "stdout-cancel" ? Task.FromCanceled<string>(new CancellationToken(true)) : Task.FromResult("exact identity");
        var error = scenario == "stderr-fault" ? Task.FromException<string>(new IOException("public read failure")) : Task.FromResult("");
        var result = GitMetadataProvider.CompleteGitOutput(output, error, false, TimeSpan.Zero);
        Assert.True(result.Failed); Assert.Null(result.Output);
    }
}
