using System.Diagnostics;

namespace TraceMap.Core;

public static class GitMetadataProvider
{
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

    private sealed record GitResult(string? Output, bool Failed);

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

            Task.WaitAll([outputTask, errorTask], TimeSpan.FromSeconds(1));
            var output = outputTask.IsCompletedSuccessfully ? outputTask.Result.Trim() : string.Empty;
            return new GitResult(allowEmpty || !string.IsNullOrWhiteSpace(output) ? output : null, Failed: false);
        }
        catch
        {
            return new GitResult(null, Failed: true);
        }
    }
}
