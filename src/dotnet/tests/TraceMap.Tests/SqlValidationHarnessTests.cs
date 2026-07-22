using System.Text.Json;
using TraceMap.Reporting;
using TraceMap.SqlValidation;
using TraceMap.SqlValidation.Cli;
using HarnessContext = TraceMap.SqlValidation.SqlValidationTargetContext;
using ReportingContext = TraceMap.Reporting.SqlValidationTargetContext;

namespace TraceMap.Tests;

public sealed class SqlValidationHarnessTests
{
    private const string Repository = "synthetic-validation-harness";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private static readonly DateTimeOffset ObservedAt = DateTimeOffset.Parse("2026-07-22T10:00:00+00:00");
    private static readonly DateTimeOffset ExpiresAt = DateTimeOffset.Parse("2026-07-22T18:00:00+00:00");
    private static readonly HarnessContext Context = new("postgresql", "archive-target", "archive-data", "archive", "validation-only");

    [Fact]
    public void Plan_reader_accepts_only_closed_sorted_catalog_checks()
    {
        var plan = SqlValidationPlanReader.Parse(PlanJson());

        Assert.Equal(SqlValidationContract.PlanSchemaVersion, "sql-validation-plan/v1");
        Assert.Equal(6, plan.Checks.Count);
        Assert.Equal(plan.Checks.OrderBy(check => check.Code, StringComparer.Ordinal), plan.Checks);
        Assert.Equal(["dblink", "pg_cron"], plan.Checks.Single(check => check.Code == "postgres.required-extension-available").Identifiers);
    }

    [Theory]
    [InlineData("unknown-property", "SqlValidationPlanUnexpectedProperty")]
    [InlineData("duplicate-check", "SqlValidationPlanDuplicateCheck")]
    [InlineData("raw-sql", "SqlValidationPlanUnexpectedProperty")]
    [InlineData("unsafe-identifier", "SqlValidationPlanInvalidIdentifiers")]
    [InlineData("unsafe-check", "SqlValidationPlanUnsupportedCheck")]
    [InlineData("bad-window", "SqlValidationPlanInvalidTimeWindow")]
    public void Plan_reader_rejects_unsafe_or_ambiguous_inputs(string variant, string classification)
    {
        var json = variant switch
        {
            "unknown-property" => PlanJson().Replace("\"checks\":", "\"password\":\"planted-secret\",\"checks\":", StringComparison.Ordinal),
            "duplicate-check" => PlanJson().Replace("{\"code\":\"postgres.required-extension-available\",\"identifiers\":[\"pg_cron\",\"dblink\"]}", "{\"code\":\"postgres.server-version-compatible\",\"expectedMajor\":16}", StringComparison.Ordinal),
            "raw-sql" => PlanJson().Replace("\"expectedMajor\":16", "\"expectedMajor\":16,\"sql\":\"select planted-secret\"", StringComparison.Ordinal),
            "unsafe-identifier" => PlanJson().Replace("\"archive.audit_log\"", "\"archive.audit_log;drop table x\"", StringComparison.Ordinal),
            "unsafe-check" => PlanJson().Replace("postgres.server-version-compatible", "postgres.archive-link-connectivity", StringComparison.Ordinal),
            "bad-window" => PlanJson().Replace("2026-07-22T18:00:00Z", "2026-07-22T09:00:00Z", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };

        var exception = Assert.Throws<SqlValidationHarnessException>(() => SqlValidationPlanReader.Parse(json));
        Assert.Equal(classification, exception.Classification);
        Assert.DoesNotContain("planted-secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generated_summary_is_deterministic_public_safe_and_accepted_by_ingestion()
    {
        using var temp = new TempDirectory();
        var plan = SqlValidationPlanReader.Parse(PlanJson());
        var outcomes = plan.Checks.ToDictionary(check => check.Code, check => check.Code != "postgres.scheduled-job-registered", StringComparer.Ordinal);
        var executor = new RecordingExecutor(outcomes);
        var firstPath = Path.Combine(temp.Path, "first.json");
        var secondPath = Path.Combine(temp.Path, "second.json");

        var first = await new SqlValidationHarnessRunner().RunAsync(plan, firstPath, executor, dryRun: false);
        var second = await new SqlValidationHarnessRunner().RunAsync(plan, secondPath, executor, dryRun: false);
        var firstJson = await File.ReadAllTextAsync(firstPath);
        var secondJson = await File.ReadAllTextAsync(secondPath);

        Assert.Equal(first.Digest, second.Digest);
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(SqlValidationContract.AssertionCodes, first.Assertions.Select(assertion => assertion.Code));
        Assert.Equal("observed-fail", first.Assertions.Single(assertion => assertion.Code == "postgres.scheduled-job-registered").Status);
        Assert.Equal("not-run", first.Assertions.Single(assertion => assertion.Code == "postgres.archive-link-connectivity").Status);
        Assert.Equal(plan.Checks, executor.Received);
        Assert.DoesNotContain("private_archive", firstJson, StringComparison.Ordinal);
        Assert.DoesNotContain("archive.audit_log", firstJson, StringComparison.Ordinal);
        Assert.DoesNotContain("nightly archive", firstJson, StringComparison.Ordinal);

        var composition = await SqlValidationSummaryReader.ReadAsync([firstPath], [new SqlValidationExpectedSource(
            "database", Repository, Commit, DateTimeOffset.Parse("2026-07-22T12:00:00+00:00"),
            [new ReportingContext(Context.Engine, Context.ServerRole, Context.DatabaseRole, Context.SchemaRole, Context.ExecutionMode)])]);
        Assert.Empty(composition.Gaps);
        Assert.Equal(10, composition.Observations.Count);
        Assert.All(composition.Observations, observation => Assert.Equal(SqlValidationSummaryReader.ValidatorId, observation.ValidatorId));
    }

    [Fact]
    public async Task Probe_failure_is_categorical_and_does_not_leak_provider_detail()
    {
        using var temp = new TempDirectory();
        var plan = SqlValidationPlanReader.Parse(PlanJson());
        var path = Path.Combine(temp.Path, "failed.json");

        var result = await new SqlValidationHarnessRunner().RunAsync(plan, path, new FailingExecutor(), dryRun: false);
        var json = await File.ReadAllTextAsync(path);

        Assert.Equal("completed-with-indeterminate-observations", result.CompletionClassification);
        Assert.All(result.Assertions.Where(assertion => SqlValidationContract.ExecutableAssertionCodes.Contains(assertion.Code)),
            assertion => Assert.Equal("observed-indeterminate", assertion.Status));
        Assert.DoesNotContain("private-host", json, StringComparison.Ordinal);
        Assert.DoesNotContain("authentication failed", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_individual_probe_result_is_indeterminate_without_discarding_other_results()
    {
        using var temp = new TempDirectory();
        var plan = SqlValidationPlanReader.Parse(PlanJson());
        var outcomes = plan.Checks
            .Where(check => check.Code != "postgres.scheduled-job-registered")
            .ToDictionary(check => check.Code, _ => true, StringComparer.Ordinal);

        var result = await new SqlValidationHarnessRunner().RunAsync(
            plan, Path.Combine(temp.Path, "partial.json"), new RecordingExecutor(outcomes), dryRun: false);

        Assert.Equal("completed-with-indeterminate-observations", result.CompletionClassification);
        Assert.Equal("observed-indeterminate", result.Assertions.Single(assertion => assertion.Code == "postgres.scheduled-job-registered").Status);
        Assert.Equal("observed-pass", result.Assertions.Single(assertion => assertion.Code == "postgres.server-version-compatible").Status);
    }

    [Fact]
    public async Task Dry_run_never_reads_connection_environment_and_emits_only_not_run()
    {
        using var temp = new TempDirectory();
        var planPath = Path.Combine(temp.Path, "plan.json");
        var outputPath = Path.Combine(temp.Path, "summary.json");
        await File.WriteAllTextAsync(planPath, PlanJson());
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Program.SqlValidationCommand.RunAsync([
            "validate", "--plan", planPath, "--out", outputPath, "--dry-run"
        ], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.DoesNotContain(planPath, output.ToString(), StringComparison.Ordinal);
        var json = await File.ReadAllTextAsync(outputPath);
        using var document = JsonDocument.Parse(json);
        Assert.All(document.RootElement.GetProperty("assertions").EnumerateArray(), assertion =>
            Assert.Equal("not-run", assertion.GetProperty("status").GetString()));
        Assert.DoesNotContain("private_archive", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_rejects_connection_values_without_disclosure()
    {
        using var temp = new TempDirectory();
        var planPath = Path.Combine(temp.Path, "plan.json");
        var outputPath = Path.Combine(temp.Path, "summary.json");
        await File.WriteAllTextAsync(planPath, PlanJson());
        await File.WriteAllTextAsync(outputPath, "sentinel");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Program.SqlValidationCommand.RunAsync([
            "validate", "--plan", planPath, "--out", outputPath, "--connection-env", "Host=private-host;Password=planted-secret"
        ], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("SqlValidationConnectionEnvironmentInvalid", error.ToString());
        Assert.DoesNotContain("private-host", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("planted-secret", error.ToString(), StringComparison.Ordinal);
        Assert.Equal("sentinel", await File.ReadAllTextAsync(outputPath));
    }

    [Fact]
    public async Task Dry_run_preserves_an_existing_output_file()
    {
        using var temp = new TempDirectory();
        var planPath = Path.Combine(temp.Path, "plan.json");
        var outputPath = Path.Combine(temp.Path, "summary.json");
        await File.WriteAllTextAsync(planPath, PlanJson());
        await File.WriteAllTextAsync(outputPath, "sentinel");
        var error = new StringWriter();

        var exitCode = await Program.SqlValidationCommand.RunAsync([
            "validate", "--plan", planPath, "--out", outputPath, "--dry-run"
        ], new StringWriter(), error);

        Assert.Equal(1, exitCode);
        Assert.Contains("SqlValidationOutputWriteFailed", error.ToString());
        Assert.Equal("sentinel", await File.ReadAllTextAsync(outputPath));
    }

    private static string PlanJson() => $$"""
        {
          "schemaVersion":"sql-validation-plan/v1",
          "artifactId":"archive-validation-001",
          "repository":"{{Repository}}",
          "commitSha":"{{Commit}}",
          "observedAt":"2026-07-22T10:00:00Z",
          "expiresAt":"2026-07-22T18:00:00Z",
          "targetContext":{"engine":"postgresql","serverRole":"archive-target","databaseRole":"archive-data","schemaRole":"archive","executionMode":"validation-only"},
          "checks":[
            {"code":"postgres.server-version-compatible","expectedMajor":16},
            {"code":"postgres.required-extension-available","identifiers":["pg_cron","dblink"]},
            {"code":"postgres.migration-schema-present","identifiers":["archive.audit_log"]},
            {"code":"postgres.permission-probe-authorized","identifiers":["private_archive"]},
            {"code":"postgres.archive-function-callable","identifiers":["archive.move_batch(integer)"]},
            {"code":"postgres.scheduled-job-registered","identifiers":["nightly archive"]}
          ]
        }
        """;

    private sealed class RecordingExecutor(IReadOnlyDictionary<string, bool> outcomes) : ISqlValidationProbeExecutor
    {
        public IReadOnlyList<SqlValidationPlanCheck> Received { get; private set; } = [];
        public Task<IReadOnlyDictionary<string, bool>> ExecuteAsync(IReadOnlyList<SqlValidationPlanCheck> checks, CancellationToken cancellationToken)
        {
            Received = checks;
            return Task.FromResult(outcomes);
        }
    }

    private sealed class FailingExecutor : ISqlValidationProbeExecutor
    {
        public Task<IReadOnlyDictionary<string, bool>> ExecuteAsync(IReadOnlyList<SqlValidationPlanCheck> checks, CancellationToken cancellationToken) =>
            throw new SqlValidationProbeException();
    }
}
