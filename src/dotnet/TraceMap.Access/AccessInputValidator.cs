using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Access;

public static partial class AccessInputValidator
{
    private const int MaxGitOutputChars = 1024 * 1024;

    public static AccessValidatedInput Validate(AccessScanOptions options, AccessLimits? limits = null)
    {
        limits ??= AccessLimits.Default;
        if (Path.IsPathFullyQualified(options.DatabasePath))
            throw new AccessScanException("AccessDatabasePathMustBeRelative");
        if (options.TimeoutSeconds is < 30 or > 3600)
            throw new AccessScanException("AccessInvalidTimeout");

        var repoCandidate = Path.GetFullPath(options.RepoPath);
        var rootResult = RunGit(repoCandidate, "rev-parse", "--show-toplevel");
        if (rootResult.ExitCode != 0 || string.IsNullOrWhiteSpace(rootResult.Output))
            throw new AccessScanException("AccessGitRootUnavailable");

        var gitRoot = Path.GetFullPath(rootResult.Output.Trim());
        var logicalGitRoot = FindLogicalGitRoot(repoCandidate) ?? gitRoot;
        var relativeSegments = NormalizeRelativeSegments(options.DatabasePath);
        var databaseRelativePath = string.Join('/', relativeSegments);
        var databaseFullPath = Path.GetFullPath(Path.Combine(gitRoot, Path.Combine(relativeSegments.ToArray())));
        EnsureContained(gitRoot, databaseFullPath);
        RejectReparsePoints(gitRoot, relativeSegments);

        if (!File.Exists(databaseFullPath) || (File.GetAttributes(databaseFullPath) & FileAttributes.Directory) != 0)
            throw new AccessScanException("AccessDatabaseFileUnavailable");

        var extension = Path.GetExtension(databaseRelativePath).ToLowerInvariant();
        if (extension is not (".accdb" or ".mdb"))
            throw new AccessScanException("AccessUnsupportedDatabaseExtension");

        var fileInfo = new FileInfo(databaseFullPath);
        if (fileInfo.Length <= 0 || fileInfo.Length > limits.MaxDatabaseBytes)
            throw new AccessScanException("AccessDatabaseSizeLimit");

        RequireGitSuccess(gitRoot, "AccessCommitUnavailable", "rev-parse", "--verify", "HEAD^{commit}");
        var commit = RunGit(gitRoot, "rev-parse", "HEAD").Required("AccessCommitUnavailable");
        RequireGitSuccess(gitRoot, "AccessInputNotTracked", "cat-file", "-e", $"HEAD:{databaseRelativePath}");
        RequireGitQuiet(gitRoot, "AccessInputNotAtCommit", "diff", "--quiet", "HEAD", "--", databaseRelativePath);
        RequireGitQuiet(gitRoot, "AccessInputNotAtCommit", "diff", "--cached", "--quiet", "HEAD", "--", databaseRelativePath);

        var hash = HashFile(databaseFullPath);
        var attr = RunGit(gitRoot, "check-attr", "filter", "--", databaseRelativePath);
        var isLfs = attr.ExitCode == 0 && attr.Output.TrimEnd().EndsWith("filter: lfs", StringComparison.OrdinalIgnoreCase);
        if (isLfs) ValidateLfs(gitRoot, databaseRelativePath, hash);
        else ValidateHeadBytes(gitRoot, databaseRelativePath, databaseFullPath);

        var rawRepoName = Path.GetFileName(gitRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var remote = RunGit(gitRoot, "config", "--get", "remote.origin.url");
        var branch = RunGit(gitRoot, "symbolic-ref", "--short", "-q", "HEAD");
        var remoteValue = remote.ExitCode == 0 ? NullIfWhiteSpace(remote.Output.Trim()) : null;
        var repositoryIdentityHash = AccessSafeValues.RoleHash("access-repository-identity", remoteValue ?? rawRepoName);
        var repoName = LegacyDataSafeValues.IsSafeIdentifier(rawRepoName) ? rawRepoName : $"repo-{repositoryIdentityHash[..16]}";
        var outputCandidate = Path.GetFullPath(options.OutputPath);
        var outputFullPath = IsAncestor(logicalGitRoot, outputCandidate)
            ? Path.GetFullPath(Path.Combine(gitRoot, Path.GetRelativePath(logicalGitRoot, outputCandidate)))
            : outputCandidate;
        ValidateOutputPath(outputFullPath, gitRoot, databaseFullPath);

        return new AccessValidatedInput(
            gitRoot,
            repoName,
            repositoryIdentityHash,
            RemoteUrl: null,
            branch.ExitCode == 0 && LegacyDataSafeValues.IsSafeIdentifier(branch.Output.Trim()) ? branch.Output.Trim() : null,
            commit.Trim(),
            databaseFullPath,
            databaseRelativePath,
            hash,
            extension,
            outputFullPath,
            isLfs);
    }

    public static IReadOnlyList<string> NormalizeRelativeSegments(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new AccessScanException("AccessDatabasePathMissing");
        var segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.Contains('\0')))
            throw new AccessScanException("AccessDatabasePathTraversal");
        return segments;
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void ValidateLfs(string gitRoot, string relativePath, string workingHash)
    {
        var size = RunGit(gitRoot, "cat-file", "-s", $"HEAD:{relativePath}");
        if (size.ExitCode != 0 || !long.TryParse(size.Output.Trim(), out var blobBytes) || blobBytes > 1024)
            throw new AccessScanException("AccessGitLfsPointerUnavailable");
        var pointer = RunGit(gitRoot, "show", $"HEAD:{relativePath}");
        var match = LfsOidPattern().Match(pointer.Output);
        if (!match.Success) throw new AccessScanException("AccessGitLfsPointerUnavailable");
        if (!string.Equals(match.Groups[1].Value, workingHash, StringComparison.OrdinalIgnoreCase))
            throw new AccessScanException("AccessGitLfsContentMismatch");
    }

    private static void ValidateHeadBytes(string gitRoot, string relativePath, string fullPath)
    {
        var committedObject = RunGit(gitRoot, "rev-parse", $"HEAD:{relativePath}").Required("AccessInputNotAtCommit");
        var workingObject = RunGit(gitRoot, "hash-object", "--no-filters", "--", fullPath).Required("AccessInputNotAtCommit");
        if (!string.Equals(committedObject.Trim(), workingObject.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new AccessScanException("AccessInputNotAtCommit");
    }

    private static void ValidateOutputPath(string output, string gitRoot, string database)
    {
        var root = Path.GetPathRoot(output);
        if (string.IsNullOrWhiteSpace(root) || PathsEqual(output, root) || PathsEqual(output, gitRoot) || PathsEqual(output, database))
            throw new AccessScanException("AccessUnsafeOutputPath");
        if (IsAncestor(output, gitRoot) || IsAncestor(output, database)) throw new AccessScanException("AccessUnsafeOutputPath");
        RejectExistingReparsePath(output);
    }

    private static void RejectExistingReparsePath(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new AccessScanException("AccessUnsafeOutputPath");
            return;
        }
        var root = Path.GetPathRoot(path) ?? throw new AccessScanException("AccessUnsafeOutputPath");
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new AccessScanException("AccessUnsafeOutputPath");
        }
    }

    private static void RejectReparsePoints(string root, IReadOnlyList<string> segments)
    {
        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current))
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new AccessScanException("AccessDatabaseReparsePointRejected");
            }
        }
    }

    private static void EnsureContained(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative))
            throw new AccessScanException("AccessDatabasePathTraversal");
    }

    private static string? FindLogicalGitRoot(string repoCandidate)
    {
        var current = new DirectoryInfo(repoCandidate);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
                return Path.GetFullPath(current.FullName);
            current = current.Parent;
        }
        return null;
    }

    private static bool IsAncestor(string candidateAncestor, string path)
    {
        var relative = Path.GetRelativePath(candidateAncestor, path);
        return relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !Path.IsPathFullyQualified(relative);
    }

    private static bool PathsEqual(string left, string right) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void RequireGitSuccess(string cwd, string classification, params string[] args)
    {
        if (RunGit(cwd, args).ExitCode != 0) throw new AccessScanException(classification);
    }

    private static void RequireGitQuiet(string cwd, string classification, params string[] args)
    {
        var result = RunGit(cwd, args);
        if (result.ExitCode != 0) throw new AccessScanException(classification);
    }

    private static GitResult RunGit(string cwd, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new AccessScanException("AccessGitUnavailable");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15_000))
        {
            process.Kill(true);
            throw new AccessScanException("AccessGitTimeout");
        }
        var output = outputTask.GetAwaiter().GetResult();
        _ = errorTask.GetAwaiter().GetResult();
        if (output.Length > MaxGitOutputChars) throw new AccessScanException("AccessGitOutputLimit");
        return new GitResult(process.ExitCode, output);
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    [GeneratedRegex(@"oid sha256:([0-9a-f]{64})", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LfsOidPattern();

    private sealed record GitResult(int ExitCode, string Output)
    {
        public string Required(string classification) => ExitCode == 0 && !string.IsNullOrWhiteSpace(Output)
            ? Output
            : throw new AccessScanException(classification);
    }
}

public sealed class AccessScanException(string classification) : Exception(classification)
{
    public string Classification { get; } = classification;
}
