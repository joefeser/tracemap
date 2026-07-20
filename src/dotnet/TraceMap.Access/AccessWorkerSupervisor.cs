using System.Diagnostics;
using System.Reflection;

namespace TraceMap.Access;

public sealed class AccessWorkerSupervisor
{
    private readonly AccessLimits _limits;

    public AccessWorkerSupervisor(AccessLimits? limits = null) => _limits = limits ?? AccessLimits.Default;

    public async Task<AccessDatabaseProjection> RunAsync(
        string databaseCopyPath,
        AccessValidatedInput input,
        int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var preexisting = ExistingAccessProcessIds();
        var start = CreateStartInfo(token, databaseCopyPath, input);
        using var worker = Process.Start(start) ?? throw new AccessScanException("AccessWorkerStartFailed");
        var workerStartedAt = DateTimeOffset.UtcNow;
        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        total.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var stderrTask = DrainBoundedAsync(worker.StandardError, total.Token);
        OwnedAccessProcess? ownedProcess = null;
        AccessDatabaseProjection result;

        try
        {
            try
            {
                result = await AccessWorkerProtocol.ReadResultAsync(
                    worker.StandardOutput,
                    token,
                    _limits.MaxProjectionBytes,
                    TimeSpan.FromSeconds(30),
                    pid => ValidateOwnedAccessProcess(pid, preexisting, workerStartedAt),
                    accepted => ownedProcess = accepted,
                    total.Token);
            }
            catch (OperationCanceledException) when (total.IsCancellationRequested)
            {
                throw new AccessScanException(cancellationToken.IsCancellationRequested ? "AccessWorkerCancelled" : "AccessWorkerTimeout");
            }
            if (!string.Equals(result.DatabaseHash, input.DatabaseHash, StringComparison.Ordinal)) throw new AccessScanException("AccessProjectionHashMismatch");
            if (!worker.WaitForExit(30_000)) throw new AccessScanException("AccessWorkerExitTimeout");
            if (worker.ExitCode != 0) throw new AccessScanException("AccessWorkerFailedAfterResult");
            return result;
        }
        catch
        {
            TryKill(worker);
            if (ownedProcess is not null) TryKillOwned(ownedProcess);
            throw;
        }
        finally
        {
            total.Cancel();
            try { await stderrTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None); }
            catch { }
        }
    }

    private static ProcessStartInfo CreateStartInfo(string token, string databaseCopyPath, AccessValidatedInput input)
    {
        var processPath = Environment.ProcessPath ?? throw new AccessScanException("AccessWorkerExecutableUnavailable");
        var entryAssembly = Assembly.GetEntryAssembly()?.Location;
        var start = new ProcessStartInfo
        {
            FileName = processPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(databaseCopyPath) ?? throw new AccessScanException("AccessWorkerExecutableUnavailable")
        };
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(entryAssembly)) throw new AccessScanException("AccessWorkerExecutableUnavailable");
            start.ArgumentList.Add(entryAssembly);
        }
        start.ArgumentList.Add("worker");
        start.ArgumentList.Add("--token");
        start.ArgumentList.Add(token);
        start.ArgumentList.Add("--database-copy");
        start.ArgumentList.Add(Path.GetFileName(databaseCopyPath));
        start.ArgumentList.Add("--database-hash");
        start.ArgumentList.Add(input.DatabaseHash);
        start.ArgumentList.Add("--database-extension");
        start.ArgumentList.Add(input.DatabaseExtension);
        start.ArgumentList.Add("--database-identity-seed");
        start.ArgumentList.Add(AccessSafeValues.DatabaseIdentitySeed(input.RepositoryIdentityHash, input.CommitSha, input.DatabaseRelativePath, input.DatabaseHash));
        start.Environment["TRACEMAP_ACCESS_WORKER_TOKEN"] = token;
        return start;
    }

    internal static OwnedAccessProcess? ValidateOwnedAccessProcess(int pid, IReadOnlySet<int> preexisting, DateTimeOffset workerStartedAt)
    {
        if (!OperatingSystem.IsWindows() || preexisting.Contains(pid)) return null;
        try
        {
            using var process = Process.GetProcessById(pid);
            var startedAt = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            return AccessProcessOwnership.Accept(
                new(pid, process.ProcessName, process.SessionId, startedAt),
                preexisting,
                workerStartedAt,
                Process.GetCurrentProcess().SessionId);
        }
        catch { return null; }
    }

    private static HashSet<int> ExistingAccessProcessIds()
    {
        if (!OperatingSystem.IsWindows()) return [];
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName("MSACCESS"))
        {
            using (process) ids.Add(process.Id);
        }
        return ids;
    }

    private static async Task DrainBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
        }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private static void TryKillOwned(OwnedAccessProcess owned)
    {
        try
        {
            using var process = Process.GetProcessById(owned.ProcessId);
            var startedAt = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            var current = new OwnedAccessProcess(process.Id, process.ProcessName, process.SessionId, startedAt);
            if (!AccessProcessOwnership.CanTerminateFallback(owned, current))
                return;
            process.Kill(entireProcessTree: true);
        }
        catch { }
    }
}

internal sealed record OwnedAccessProcess(int ProcessId, string ProcessName, int SessionId, DateTimeOffset StartedAtUtc);

internal static class AccessProcessOwnership
{
    public static OwnedAccessProcess? Accept(
        OwnedAccessProcess candidate,
        IReadOnlySet<int> preexisting,
        DateTimeOffset workerStartedAt,
        int expectedSessionId) =>
        preexisting.Contains(candidate.ProcessId)
        || !candidate.ProcessName.Equals("MSACCESS", StringComparison.OrdinalIgnoreCase)
        || candidate.SessionId != expectedSessionId
        || candidate.StartedAtUtc < workerStartedAt.AddSeconds(-5)
            ? null
            : candidate;

    public static bool CanTerminateFallback(OwnedAccessProcess owned, OwnedAccessProcess current) =>
        current.ProcessId == owned.ProcessId
        && current.ProcessName.Equals("MSACCESS", StringComparison.OrdinalIgnoreCase)
        && current.SessionId == owned.SessionId
        && current.StartedAtUtc == owned.StartedAtUtc;
}
