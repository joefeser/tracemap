namespace TraceMap.Cli;

internal static class ScanOutputTransaction
{
    private static readonly string[] RequiredArtifacts =
    [
        "scan-manifest.json",
        "facts.ndjson",
        "index.sqlite",
        "report.md",
        "logs/analyzer.log"
    ];

    internal static async Task WriteAsync(string outputPath, string repoPath, Func<string, Task> writer)
    {
        var target = Path.GetFullPath(outputPath);
        var parent = Path.GetDirectoryName(target) ?? throw new IOException("OutputParentUnavailable");
        ValidateTarget(target, repoPath);

        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".tracemap-{Path.GetFileName(target)}-{Guid.NewGuid():N}");
        var previous = staging + ".previous";
        var previousMoved = false;
        Directory.CreateDirectory(staging);
        try
        {
            await writer(staging);
            EnsureComplete(staging);
            if (Directory.Exists(target))
            {
                Directory.Move(target, previous);
                previousMoved = true;
            }
            try
            {
                Directory.Move(staging, target);
            }
            catch
            {
                if (previousMoved && !Directory.Exists(target))
                {
                    Directory.Move(previous, target);
                    previousMoved = false;
                }
                throw;
            }
            if (previousMoved)
            {
                Directory.Delete(previous, recursive: true);
                previousMoved = false;
            }
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            if (previousMoved && Directory.Exists(previous) && !Directory.Exists(target))
                Directory.Move(previous, target);
        }
    }

    internal static bool HasCompleteOutput(string outputPath) =>
        Directory.Exists(outputPath)
        && RequiredArtifacts.All(relative => File.Exists(Path.Combine(outputPath, relative.Replace('/', Path.DirectorySeparatorChar))));

    internal static bool CanWriteFailureReceipt(string outputPath, string repoPath)
    {
        try
        {
            ValidateTarget(outputPath, repoPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    internal static void ValidateTarget(string outputPath, string repoPath)
    {
        var target = Path.GetFullPath(outputPath);
        ValidateProtectedRoot(target, repoPath);
        if (File.Exists(target) || Directory.Exists(target) && !CanReplace(target))
            throw new IOException("OutputArtifactSetNotReplaceable");
    }

    internal static void ValidateProtectedRoot(string outputPath, string repoPath)
    {
        var target = Path.GetFullPath(outputPath);
        var repo = Path.GetFullPath(repoPath);
        if (IsSameOrAncestor(target, repo))
            throw new IOException("OutputArtifactSetNotReplaceable");
    }

    private static bool IsSameOrAncestor(string candidate, string path)
    {
        var relative = Path.GetRelativePath(candidate, path);
        return relative is "." or ""
            || !(relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static bool CanReplace(string outputPath)
    {
        if (!Directory.Exists(outputPath)) return false;
        var entries = Directory.EnumerateFileSystemEntries(outputPath).ToArray();
        if (entries.Length == 0
            || entries.All(path => File.Exists(path) && Path.GetFileName(path).Equals("scan-receipt.json", StringComparison.Ordinal)))
            return true;
        if (!HasCompleteOutput(outputPath)) return false;

        var allowedRootEntries = new HashSet<string>(StringComparer.Ordinal)
        {
            "scan-manifest.json",
            "facts.ndjson",
            "index.sqlite",
            "report.md",
            "scan-receipt.json",
            "logs"
        };
        if (entries.Any(path => !allowedRootEntries.Contains(Path.GetFileName(path)))) return false;
        var logsPath = Path.Combine(outputPath, "logs");
        if (!Directory.Exists(logsPath)) return false;
        var logEntries = Directory.EnumerateFileSystemEntries(logsPath).ToArray();
        return logEntries.Length == 1
            && File.Exists(logEntries[0])
            && Path.GetFileName(logEntries[0]).Equals("analyzer.log", StringComparison.Ordinal);
    }

    private static void EnsureComplete(string stagingPath)
    {
        if (RequiredArtifacts.Any(relative => !File.Exists(Path.Combine(stagingPath, relative.Replace('/', Path.DirectorySeparatorChar)))))
            throw new IOException("RequiredScanArtifactUnavailable");
    }
}
