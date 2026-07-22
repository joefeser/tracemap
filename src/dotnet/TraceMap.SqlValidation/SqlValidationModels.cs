namespace TraceMap.SqlValidation;

public static class SqlValidationContract
{
    public const string PlanSchemaVersion = "sql-validation-plan/v1";
    public const string SummarySchemaVersion = "sql-validation-summary/v1";
    public const string ValidatorId = "tracemap.sql-validation-harness";
    public const string ValidatorVersion = "1.0.0";
    public const string DigestAlgorithm = "sha256-canonical-json-v1";

    public static readonly IReadOnlyList<string> AssertionCodes =
    [
        "postgres.archive-function-callable",
        "postgres.archive-link-connectivity",
        "postgres.cleanup-probe-observed",
        "postgres.migration-schema-present",
        "postgres.permission-probe-authorized",
        "postgres.required-extension-available",
        "postgres.scheduled-job-registered",
        "postgres.server-version-compatible",
        "postgres.target-schema-compatible",
        "postgres.validation-query-expected-shape"
    ];

    public static readonly IReadOnlySet<string> ExecutableAssertionCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "postgres.archive-function-callable",
        "postgres.migration-schema-present",
        "postgres.permission-probe-authorized",
        "postgres.required-extension-available",
        "postgres.scheduled-job-registered",
        "postgres.server-version-compatible"
    };

    public static readonly IReadOnlyList<string> Limitations =
    [
        "does-not-approve-release",
        "does-not-establish-continuing-state",
        "does-not-establish-safe-to-run",
        "point-in-time-observation"
    ];
}

public sealed record SqlValidationTargetContext(
    string Engine,
    string ServerRole,
    string DatabaseRole,
    string SchemaRole,
    string ExecutionMode);

public sealed record SqlValidationPlanCheck(
    string Code,
    int? ExpectedMajor,
    IReadOnlyList<string> Identifiers);

public sealed record SqlValidationPlan(
    string ArtifactId,
    string Repository,
    string CommitSha,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    SqlValidationTargetContext TargetContext,
    IReadOnlyList<SqlValidationPlanCheck> Checks);

public sealed record SqlValidationAssertion(string Code, string Status);

public sealed record SqlValidationHarnessResult(
    string ArtifactId,
    string Digest,
    IReadOnlyList<SqlValidationAssertion> Assertions,
    string CompletionClassification);

public sealed class SqlValidationHarnessException(string classification) : Exception(classification)
{
    public string Classification { get; } = classification;
}

public sealed class SqlValidationProbeException : Exception;

public interface ISqlValidationProbeExecutor
{
    Task<IReadOnlyDictionary<string, bool>> ExecuteAsync(
        IReadOnlyList<SqlValidationPlanCheck> checks,
        CancellationToken cancellationToken);
}
