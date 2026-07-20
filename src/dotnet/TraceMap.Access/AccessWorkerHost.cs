using System.Text.Json;

namespace TraceMap.Access;

public static class AccessWorkerHost
{
    public static async Task<int> RunAsync(IReadOnlyDictionary<string, string> options, TextWriter output, CancellationToken cancellationToken = default)
    {
        var token = Required(options, "--token");
        if (!string.Equals(Environment.GetEnvironmentVariable("TRACEMAP_ACCESS_WORKER_TOKEN"), token, StringComparison.Ordinal))
            throw new AccessScanException("AccessWorkerTokenMismatch");
        var databaseCopyName = Required(options, "--database-copy");
        if (Path.IsPathFullyQualified(databaseCopyName) || Path.GetFileName(databaseCopyName) != databaseCopyName)
            throw new AccessScanException("AccessWorkerArgumentInvalid");
        var databaseCopy = Path.GetFullPath(databaseCopyName);
        var databaseHash = Required(options, "--database-hash");
        var databaseIdentitySeed = Required(options, "--database-identity-seed");
        var extension = Required(options, "--database-extension");
        var limits = AccessLimits.Default;
        var writeGate = new object();
        using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(5));
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        async Task WriteAsync(AccessWorkerFrame frame)
        {
            var json = JsonSerializer.Serialize(frame, AccessJsonContext.Default.AccessWorkerFrame);
            if (AccessWorkerProtocol.EncodedFrameBytes(json) > limits.MaxProjectionBytes) throw new AccessScanException("AccessProjectionLimitReached");
            lock (writeGate)
            {
                output.Write(json);
                output.Write('\n');
                output.Flush();
            }
            await Task.CompletedTask;
        }

        var heartbeatTask = Task.Run(async () =>
        {
            try
            {
                while (await heartbeat.WaitForNextTickAsync(heartbeatCancellation.Token))
                    await WriteAsync(AccessWorkerFrame.Heartbeat(token));
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);

        try
        {
            var reader = new AccessComReader(limits);
            var projection = reader.Read(databaseCopy, databaseHash, databaseIdentitySeed, extension, pid => WriteAsync(AccessWorkerFrame.Hello(token, pid)).GetAwaiter().GetResult());
            await WriteAsync(AccessWorkerFrame.Success(token, projection));
            return 0;
        }
        catch (AccessScanException ex)
        {
            await WriteAsync(AccessWorkerFrame.Failure(token, ex.Classification));
            return 1;
        }
        catch
        {
            await WriteAsync(AccessWorkerFrame.Failure(token, "AccessWorkerUnhandledFailure"));
            return 1;
        }
        finally
        {
            heartbeatCancellation.Cancel();
            try { await heartbeatTask; } catch { }
        }
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new AccessScanException("AccessWorkerArgumentMissing");
}
