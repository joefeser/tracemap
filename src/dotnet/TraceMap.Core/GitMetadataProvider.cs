using System.Diagnostics;

namespace TraceMap.Core;

public static class GitMetadataProvider
{
    internal static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(5);
    public static GitMetadata Detect(string repoPath)
    {
        var root = Path.GetFullPath(repoPath);
        var repoName = new DirectoryInfo(root).Name;
        var gaps = new List<string>();
        var gitRoot = RunGit(root, "rev-parse", "--show-toplevel");
        if (!string.IsNullOrWhiteSpace(gitRoot))
        {
            repoName = new DirectoryInfo(gitRoot).Name;
        }

        var commitSha = RunGit(root, "rev-parse", "HEAD");
        if (string.IsNullOrWhiteSpace(commitSha))
        {
            commitSha = "unknown";
            gaps.Add("Git commit SHA unavailable; scan is labeled with commitSha 'unknown'.");
        }

        var branch = RunGit(root, "rev-parse", "--abbrev-ref", "HEAD");
        if (string.IsNullOrWhiteSpace(branch) || branch.Equals("HEAD", StringComparison.Ordinal))
        {
            branch = null;
        }

        var remoteUrl = RunGit(root, "config", "--get", "remote.origin.url");
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            remoteUrl = null;
        }

        var scanRootRelativePath = RunGit(root, allowEmpty: true, "rev-parse", "--show-prefix")?.TrimEnd('/', '\\');
        return new GitMetadata(repoName, remoteUrl, branch, commitSha, gaps, gitRoot, scanRootRelativePath);
    }

    private static string? RunGit(string workingDirectory, params string[] arguments) =>
        RunGit(workingDirectory, allowEmpty: false, arguments);

    private static string? RunGit(string workingDirectory, bool allowEmpty, params string[] arguments)
    {
        var result = TryRunGit(workingDirectory, allowEmpty, arguments);
        // A failed process invocation is retried exactly once: concurrent
        // scans spawn many git processes, and a transient spawn or timeout
        // failure must not flip repository identity to the directory-name
        // fallback. A genuine non-repository exits nonzero on both attempts
        // and keeps its null result.
        if (result.Failed)
            result = TryRunGit(workingDirectory, allowEmpty, arguments);
        return result.Output;
    }

    internal sealed record GitResult(string? Output, bool Failed);

    // Process exit is not proof that redirected pipes have finished draining.
    // Preserve the bounded probe/retry contract instead of admitting an empty
    // successful result when a continuation is delayed under concurrent scans.
    internal static GitResult CompleteGitOutput(Task<string> outputTask, Task<string> errorTask,
        bool allowEmpty, TimeSpan drainTimeout)
    {
        try
        {
            if (!Task.WaitAll([outputTask, errorTask], drainTimeout) ||
                !outputTask.IsCompletedSuccessfully || !errorTask.IsCompletedSuccessfully)
                return new(null, Failed: true);
            var output = outputTask.Result.Trim();
            if (!allowEmpty && string.IsNullOrWhiteSpace(output)) return new(null, Failed: true);
            return new(output, Failed: false);
        }
        catch
        {
            return new(null, Failed: true);
        }
    }

    private static GitResult TryRunGit(string workingDirectory, bool allowEmpty, params string[] arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            if (!process.Start())
            {
                return new GitResult(null, Failed: true);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort cleanup only.
                }

                return new GitResult(null, Failed: true);
            }

            if (process.ExitCode != 0)
            {
                return new GitResult(null, Failed: true);
            }

            // Use the same bounded budget as process exit. A one-second pipe
            // continuation budget can expire on loaded two-core CI runners even
            // after git exits successfully. Exhaustion still fails closed and
            // is retried only once; no previous identity is reused.
            return CompleteGitOutput(outputTask, errorTask, allowEmpty, OutputDrainTimeout);
        }
        catch
        {
            return new GitResult(null, Failed: true);
        }
    }
}
