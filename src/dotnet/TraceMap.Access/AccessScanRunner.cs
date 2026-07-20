using TraceMap.Core;

namespace TraceMap.Access;

public sealed class AccessScanRunner
{
    private readonly AccessLimits _limits;
    private readonly AccessWorkerSupervisor _supervisor;

    public AccessScanRunner(AccessLimits? limits = null)
    {
        _limits = limits ?? AccessLimits.Default;
        _supervisor = new AccessWorkerSupervisor(_limits);
    }

    public async Task<ScanResult> RunAsync(AccessScanOptions options, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new AccessScanException("AccessUnsupportedPlatform");
        AccessValidatedInput input;
        try { input = AccessInputValidator.Validate(options, _limits); }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException("AccessInputValidationFailed"); }
        AccessDatabaseProjection projection;
        AccessWorkingCopy workingCopy;
        try { workingCopy = AccessWorkingCopy.Create(input); }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException("AccessWorkingCopyFailed"); }
        try
        {
            try { projection = await _supervisor.RunAsync(workingCopy.DatabasePath, input, options.TimeoutSeconds, cancellationToken); }
            catch (AccessScanException) { throw; }
            catch { throw new AccessScanException("AccessWorkerSupervisorFailed"); }
        }
        finally
        {
            workingCopy.Dispose();
        }

        string originalHash;
        try { originalHash = AccessInputValidator.HashFile(input.DatabaseFullPath); }
        catch { throw new AccessScanException("AccessOriginalInputVerificationFailed"); }
        if (!string.Equals(originalHash, input.DatabaseHash, StringComparison.Ordinal))
            throw new AccessScanException("AccessOriginalInputChangedDuringScan");

        if (workingCopy.CleanupFailed)
        {
            projection = projection with
            {
                Gaps = [.. projection.Gaps, new AccessGapProjection("AccessWorkingCopyCleanupFailed", "scan", null)]
            };
        }
        try { return AccessFactBuilder.Build(input, projection, options, _limits); }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException("AccessFactProjectionFailed"); }
    }
}
