using System.Text;
using System.Text.Json;

namespace TraceMap.Access;

internal static class AccessWorkerProtocol
{
    internal static long EncodedFrameBytes(string json) => Encoding.UTF8.GetByteCount(json) + 1L;

    public static async Task<AccessDatabaseProjection> ReadResultAsync(
        TextReader reader,
        string token,
        long maxProjectionBytes,
        TimeSpan idleTimeout,
        Func<int, OwnedAccessProcess?> validateOwnedProcess,
        Action<OwnedAccessProcess> processAccepted,
        CancellationToken cancellationToken)
    {
        OwnedAccessProcess? ownedProcess = null;
        long projectionBytes = 0;
        while (true)
        {
            string? line;
            try { line = await reader.ReadLineAsync(cancellationToken).AsTask().WaitAsync(idleTimeout, cancellationToken); }
            catch (TimeoutException) { throw new AccessScanException("AccessWorkerHeartbeatTimeout"); }
            if (line is null) throw new AccessScanException("AccessWorkerResultMissing");
            projectionBytes += EncodedFrameBytes(line);
            if (projectionBytes > maxProjectionBytes) throw new AccessScanException("AccessProjectionLimitReached");

            AccessWorkerFrame frame;
            try { frame = JsonSerializer.Deserialize(line, AccessJsonContext.Default.AccessWorkerFrame) ?? throw new JsonException(); }
            catch { throw new AccessScanException("AccessWorkerFrameInvalid"); }
            if (!string.Equals(frame.Token, token, StringComparison.Ordinal)) throw new AccessScanException("AccessWorkerTokenMismatch");

            switch (frame.Kind)
            {
                case "heartbeat":
                    break;
                case "hello":
                    if (frame.AccessProcessId is null or <= 0) throw new AccessScanException("AccessOwnedProcessIdentityUnavailable");
                    if (ownedProcess is not null) throw new AccessScanException("AccessOwnedProcessIdentityRepeated");
                    ownedProcess = validateOwnedProcess(frame.AccessProcessId.Value);
                    if (ownedProcess is null) throw new AccessScanException("AccessOwnedProcessIdentityRejected");
                    processAccepted(ownedProcess);
                    break;
                case "failure":
                    throw new AccessScanException(SafeClassification(frame.Classification));
                case "result":
                    if (ownedProcess is null || frame.Result is null || frame.Result.AccessProcessId != ownedProcess.ProcessId)
                        throw new AccessScanException("AccessWorkerResultInvalid");
                    return frame.Result;
                default:
                    throw new AccessScanException("AccessWorkerFrameKindUnknown");
            }
        }
    }

    internal static string SafeClassification(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? value
            : "AccessWorkerFailure";
}
