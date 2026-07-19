using System.Diagnostics;
using System.Security.Principal;

namespace TraceMap.Access;

public sealed class AccessWorkingCopy : IDisposable
{
    private bool _disposed;

    private AccessWorkingCopy(string directoryPath, string databasePath)
    {
        DirectoryPath = directoryPath;
        DatabasePath = databasePath;
    }

    public string DirectoryPath { get; }
    public string DatabasePath { get; }
    public bool CleanupFailed { get; private set; }

    public static AccessWorkingCopy Create(AccessValidatedInput input)
    {
        var directory = Directory.CreateTempSubdirectory("tracemap-access-").FullName;
        var copyPath = Path.Combine(directory, $"input{input.DatabaseExtension}");
        try
        {
            RestrictDirectory(directory);
            using (var source = new FileStream(input.DatabaseFullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
            using (var target = new FileStream(copyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.None))
            {
                source.CopyTo(target);
                target.Flush(flushToDisk: true);
            }
            if (!string.Equals(AccessInputValidator.HashFile(copyPath), input.DatabaseHash, StringComparison.Ordinal))
                throw new AccessScanException("AccessWorkingCopyHashMismatch");
            return new AccessWorkingCopy(directory, copyPath);
        }
        catch
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
            throw;
        }
    }

    private static void RestrictDirectory(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return;
        }

        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(sid)) throw new AccessScanException("AccessWorkingCopyAclUnavailable");
        var start = new ProcessStartInfo("icacls.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(directory);
        start.ArgumentList.Add("/inheritance:r");
        start.ArgumentList.Add("/grant:r");
        start.ArgumentList.Add($"*{sid}:(OI)(CI)F");
        using var process = Process.Start(start) ?? throw new AccessScanException("AccessWorkingCopyAclUnavailable");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new AccessScanException("AccessWorkingCopyAclUnavailable");
        }
        _ = outputTask.GetAwaiter().GetResult();
        _ = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new AccessScanException("AccessWorkingCopyAclUnavailable");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
        }
        catch
        {
            CleanupFailed = true;
        }
    }
}
