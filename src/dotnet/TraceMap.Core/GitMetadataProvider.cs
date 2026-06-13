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

        return new GitMetadata(repoName, remoteUrl, branch, commitSha, gaps, gitRoot);
    }

    private static string? RunGit(string workingDirectory, params string[] arguments)
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
                return null;
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

                return null;
            }

            if (process.ExitCode != 0)
            {
                return null;
            }

            Task.WaitAll([outputTask, errorTask], TimeSpan.FromSeconds(1));
            var output = outputTask.IsCompletedSuccessfully ? outputTask.Result.Trim() : string.Empty;
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }
}
