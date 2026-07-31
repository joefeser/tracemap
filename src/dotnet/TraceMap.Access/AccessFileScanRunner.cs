using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using TraceMap.Core;

namespace TraceMap.Access;

public sealed record AccessFileScanOptions(
    string DatabasePath,
    string OutputPath,
    int TimeoutSeconds = 600);

public sealed class AccessFileScanRunner
{
    private const string SnapshotRepositoryName = "localfilesnapshot";
    private const int CleanupAttemptCount = 30;
    private const int CleanupRetryDelayMilliseconds = 200;
    private readonly AccessLimits _limits;

    public AccessFileScanRunner(AccessLimits? limits = null)
    {
        _limits = limits ?? AccessLimits.Default;
    }

    public Task<ScanResult> RunAsync(
        AccessFileScanOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            throw new AccessScanException("AccessUnsupportedPlatform");

        return RunCoreAsync(
            options,
            (scanOptions, token) => new AccessScanRunner(_limits).RunAsync(scanOptions, token),
            () => Directory.CreateTempSubdirectory("tracemap-access-file-").FullName,
            path => Directory.Delete(path, recursive: true),
            cancellationToken);
    }

    internal async Task<ScanResult> RunCoreAsync(
        AccessFileScanOptions options,
        Func<AccessScanOptions, CancellationToken, Task<ScanResult>> scan,
        Func<string> createScratchDirectory,
        Action<string> deleteScratchDirectory,
        CancellationToken cancellationToken,
        Action? cleanupRetryDelay = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validated = Validate(options, _limits);
        string? scratchDirectory = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            scratchDirectory = createScratchDirectory();
            ValidateScratchDirectory(scratchDirectory);
            AccessWorkingCopy.RestrictDirectory(scratchDirectory);

            var snapshotRepository = Path.Combine(scratchDirectory, SnapshotRepositoryName);
            Directory.CreateDirectory(snapshotRepository);
            var snapshotFileName = $"database{validated.Extension}";
            var snapshotDatabase = Path.Combine(snapshotRepository, snapshotFileName);
            CopyAndVerify(validated.FullPath, snapshotDatabase, validated.Hash);
            InitializeSnapshotRepository(snapshotRepository, snapshotFileName, options.TimeoutSeconds);

            ScanResult? result = null;
            ExceptionDispatchInfo? scanFailure = null;
            try
            {
                result = await scan(
                    new AccessScanOptions(
                        snapshotRepository,
                        snapshotFileName,
                        validated.OutputFullPath,
                        options.TimeoutSeconds,
                        AccessProvenanceKinds.LocalFileSnapshot),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                scanFailure = ExceptionDispatchInfo.Capture(ex);
            }

            VerifyOriginalUnchanged(validated);
            scanFailure?.Throw();
            return result ?? throw new AccessScanException("AccessFileSnapshotScanFailed");
        }
        finally
        {
            if (scratchDirectory is not null && Directory.Exists(scratchDirectory))
            {
                try
                {
                    DeleteScratchDirectoryWithRetry(
                        scratchDirectory,
                        deleteScratchDirectory,
                        cleanupRetryDelay ?? (() => Thread.Sleep(CleanupRetryDelayMilliseconds)));
                }
                catch
                {
                    throw new AccessScanException("AccessFileSnapshotCleanupFailed");
                }
            }
        }
    }

    internal static void DeleteScratchDirectoryWithRetry(
        string scratchDirectory,
        Action<string> deleteScratchDirectory,
        Action retryDelay)
    {
        for (var attempt = 1; attempt <= CleanupAttemptCount; attempt++)
        {
            try
            {
                deleteScratchDirectory(scratchDirectory);
                if (!Directory.Exists(scratchDirectory)) return;
            }
            catch when (attempt < CleanupAttemptCount)
            {
                // Windows can retain a short-lived handle after Git or Access exits.
                // Retry only this known disposable directory and verify removal below.
            }

            if (attempt < CleanupAttemptCount) retryDelay();
        }

        throw new AccessScanException("AccessFileSnapshotCleanupFailed");
    }

    private static AccessFileValidatedInput Validate(AccessFileScanOptions options, AccessLimits limits)
    {
        if (string.IsNullOrWhiteSpace(options.DatabasePath))
            throw new AccessScanException("AccessDatabasePathMissing");
        if (string.IsNullOrWhiteSpace(options.OutputPath))
            throw new AccessScanException("AccessOutputMissing");
        if (options.TimeoutSeconds is < 30 or > 3600)
            throw new AccessScanException("AccessInvalidTimeout");

        if (IsNetworkHostedPath(options.DatabasePath))
            throw new AccessScanException("AccessNetworkDatabasePathRejected");

        var fullPath = Path.GetFullPath(options.DatabasePath);
        if (IsNetworkHostedPath(fullPath))
            throw new AccessScanException("AccessNetworkDatabasePathRejected");
        RejectReparsePath(fullPath, "AccessDatabaseReparsePointRejected");
        if (!File.Exists(fullPath) || (File.GetAttributes(fullPath) & FileAttributes.Directory) != 0)
            throw new AccessScanException("AccessDatabaseFileUnavailable");

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (extension is not (".accdb" or ".mdb"))
            throw new AccessScanException("AccessUnsupportedDatabaseExtension");
        var size = new FileInfo(fullPath).Length;
        if (size <= 0 || size > limits.MaxDatabaseBytes)
            throw new AccessScanException("AccessDatabaseSizeLimit");

        var output = Path.GetFullPath(options.OutputPath);
        var outputRoot = Path.GetPathRoot(output);
        if (string.IsNullOrWhiteSpace(outputRoot)
            || PathsEqual(output, outputRoot)
            || PathsEqual(output, fullPath)
            || IsAncestor(output, fullPath))
        {
            throw new AccessScanException("AccessUnsafeOutputPath");
        }

        RejectReparsePath(output, "AccessUnsafeOutputPath", allowMissingLeaf: true);
        if (File.Exists(output) || Directory.Exists(output))
            throw new AccessScanException("AccessOutputAlreadyExists");

        string hash;
        try
        {
            hash = AccessInputValidator.HashFile(fullPath);
        }
        catch
        {
            throw new AccessScanException("AccessOriginalInputVerificationFailed");
        }

        return new AccessFileValidatedInput(fullPath, extension, hash, output);
    }

    private static void CopyAndVerify(string source, string target, string expectedHash)
    {
        try
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
            using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.None))
            {
                input.CopyTo(output);
                output.Flush(flushToDisk: true);
            }
        }
        catch (AccessScanException)
        {
            throw;
        }
        catch
        {
            throw new AccessScanException("AccessFileSnapshotCopyFailed");
        }

        if (!string.Equals(AccessInputValidator.HashFile(target), expectedHash, StringComparison.Ordinal))
            throw new AccessScanException("AccessFileSnapshotHashMismatch");
    }

    private static void InitializeSnapshotRepository(string repository, string databaseFileName, int timeoutSeconds)
    {
        var emptyGlobalConfig = Path.Combine(repository, AccessGitIsolation.EmptyGlobalConfigFileName);
        var emptyTemplate = Path.Combine(repository, ".tracemap-empty-git-template");
        var disabledHooks = Path.Combine(repository, ".tracemap-disabled-hooks");
        File.WriteAllText(emptyGlobalConfig, string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Directory.CreateDirectory(emptyTemplate);
        Directory.CreateDirectory(disabledHooks);
        RunGit(
            repository,
            "AccessFileSnapshotGitInitFailed",
            timeoutSeconds,
            "init",
            "--quiet",
            "--object-format=sha1",
            "--initial-branch=access-file-snapshot",
            $"--template={emptyTemplate}");
        RunGit(repository, "AccessFileSnapshotGitInitFailed", timeoutSeconds, "config", "user.name", "TraceMap Local File Snapshot");
        RunGit(repository, "AccessFileSnapshotGitInitFailed", timeoutSeconds, "config", "user.email", "local-snapshot@tracemap.invalid");
        RunGit(repository, "AccessFileSnapshotGitInitFailed", timeoutSeconds, "config", "core.autocrlf", "false");
        RunGit(repository, "AccessFileSnapshotGitInitFailed", timeoutSeconds, "config", "core.hooksPath", disabledHooks);
        RunGit(repository, "AccessFileSnapshotGitInitFailed", timeoutSeconds, "config", "commit.gpgsign", "false");
        File.WriteAllText(
            Path.Combine(repository, ".gitattributes"),
            "/database.accdb -text -filter -diff\n/database.mdb -text -filter -diff\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        RunGit(
            repository,
            "AccessFileSnapshotGitCommitFailed",
            timeoutSeconds,
            "add",
            "--",
            AccessGitIsolation.EmptyGlobalConfigFileName,
            ".gitattributes",
            databaseFileName);
        RunGit(
            repository,
            "AccessFileSnapshotGitCommitFailed",
            timeoutSeconds,
            ["commit", "--quiet", "--no-verify", "-m", "TraceMap local file snapshot"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["GIT_AUTHOR_DATE"] = "2000-01-01T00:00:00Z",
                ["GIT_COMMITTER_DATE"] = "2000-01-01T00:00:00Z"
            });
        var remotes = RunGit(repository, "AccessFileSnapshotGitInitFailed", timeoutSeconds, "remote");
        if (!string.IsNullOrWhiteSpace(remotes))
            throw new AccessScanException("AccessFileSnapshotRemoteRejected");
    }

    private static string RunGit(string workingDirectory, string classification, int timeoutSeconds, params string[] arguments) =>
        RunGit(workingDirectory, classification, timeoutSeconds, arguments, null);

    private static string RunGit(
        string workingDirectory,
        string classification,
        int timeoutSeconds,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        AccessGitIsolation.Configure(start, workingDirectory);
        if (environment is not null)
        {
            foreach (var pair in environment)
                start.Environment[pair.Key] = pair.Value;
        }

        try
        {
            using var process = Process.Start(start) ?? throw new AccessScanException(classification);
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(GitTimeoutMilliseconds(timeoutSeconds)))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new AccessScanException("AccessFileSnapshotGitTimeout");
            }
            var output = outputTask.GetAwaiter().GetResult();
            _ = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
                throw new AccessScanException(classification);
            return output;
        }
        catch (AccessScanException)
        {
            throw;
        }
        catch
        {
            throw new AccessScanException("AccessFileSnapshotGitUnavailable");
        }
    }

    internal static int GitTimeoutMilliseconds(int timeoutSeconds) => checked(timeoutSeconds * 1000);

    internal static void ValidateScratchDirectory(
        string path,
        Func<string, DriveType>? driveType = null)
    {
        if (IsNetworkHostedPath(path, driveType))
            throw new AccessScanException("AccessFileSnapshotNetworkScratchRejected");
        var fullPath = Path.GetFullPath(path);
        if (IsNetworkHostedPath(fullPath, driveType))
            throw new AccessScanException("AccessFileSnapshotNetworkScratchRejected");
        RejectReparsePath(fullPath, "AccessFileSnapshotUnsafeScratchRejected");
        if (!Directory.Exists(fullPath))
            throw new AccessScanException("AccessFileSnapshotUnsafeScratchRejected");
    }

    internal static bool IsNetworkHostedPath(
        string path,
        Func<string, DriveType>? driveType = null)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal)
            || path.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        string? root;
        try
        {
            root = Path.GetPathRoot(Path.GetFullPath(path));
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(root))
            return false;

        try
        {
            return (driveType ?? (value => new DriveInfo(value).DriveType))(root) == DriveType.Network;
        }
        catch
        {
            return false;
        }
    }

    private static void VerifyOriginalUnchanged(AccessFileValidatedInput input)
    {
        string currentHash;
        try
        {
            currentHash = AccessInputValidator.HashFile(input.FullPath);
        }
        catch
        {
            throw new AccessScanException("AccessOriginalInputVerificationFailed");
        }
        if (!string.Equals(currentHash, input.Hash, StringComparison.Ordinal))
            throw new AccessScanException("AccessOriginalInputChangedDuringScan");
    }

    private static void RejectReparsePath(string path, string classification, bool allowMissingLeaf = false)
    {
        var fullPath = Path.GetFullPath(path);
        if (!OperatingSystem.IsWindows())
        {
            if ((File.Exists(fullPath) || Directory.Exists(fullPath))
                && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new AccessScanException(classification);
            }
            return;
        }
        var root = Path.GetPathRoot(fullPath) ?? throw new AccessScanException(classification);
        var current = root;
        var segments = Path.GetRelativePath(root, fullPath)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current, segments[index]);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                if (allowMissingLeaf)
                    return;
                throw new AccessScanException(classification);
            }
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new AccessScanException(classification);
        }
    }

    private static bool IsAncestor(string ancestor, string path)
    {
        var relative = Path.GetRelativePath(ancestor, path);
        return relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathFullyQualified(relative);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed record AccessFileValidatedInput(
        string FullPath,
        string Extension,
        string Hash,
        string OutputFullPath);
}
