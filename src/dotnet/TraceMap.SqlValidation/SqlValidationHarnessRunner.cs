namespace TraceMap.SqlValidation;

public sealed class SqlValidationHarnessRunner
{
    public async Task<SqlValidationHarnessResult> RunAsync(
        SqlValidationPlan plan,
        string outputPath,
        ISqlValidationProbeExecutor? executor,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        await using var outputReservation = SqlValidationSummaryWriter.Reserve(outputPath);
        IReadOnlyDictionary<string, bool> outcomes = new Dictionary<string, bool>(StringComparer.Ordinal);
        var executionFailed = false;
        if (!dryRun)
        {
            if (executor is null) throw new SqlValidationHarnessException("SqlValidationExecutorMissing");
            try
            {
                outcomes = await executor.ExecuteAsync(plan.Checks, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (SqlValidationProbeException)
            {
                executionFailed = true;
            }
        }

        var configured = plan.Checks.Select(check => check.Code).ToHashSet(StringComparer.Ordinal);
        var assertions = SqlValidationContract.AssertionCodes.Select(code => new SqlValidationAssertion(code,
            dryRun || !configured.Contains(code)
                ? "not-run"
                : executionFailed || !outcomes.TryGetValue(code, out var passed)
                    ? "observed-indeterminate"
                    : passed ? "observed-pass" : "observed-fail")).ToArray();
        var hasIndeterminate = assertions.Any(assertion => assertion.Status == "observed-indeterminate");

        var (digest, _) = await SqlValidationSummaryWriter.WriteAsync(outputReservation, plan, assertions, cancellationToken);
        return new SqlValidationHarnessResult(plan.ArtifactId, digest, assertions,
            dryRun ? "dry-run-completed" : executionFailed || hasIndeterminate ? "completed-with-indeterminate-observations" : "completed");
    }
}
