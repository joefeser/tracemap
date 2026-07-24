using System.Text.RegularExpressions;
using TraceMap.SqlValidation;

namespace TraceMap.SqlValidation.Cli;

public static partial class Program
{
    public static Task<int> Main(string[] args) => SqlValidationCommand.RunAsync(args, Console.Out, Console.Error);

    public static class SqlValidationCommand
    {
        public static async Task<int> RunAsync(
            string[] args,
            TextWriter output,
            TextWriter error,
            CancellationToken cancellationToken = default)
        {
            if (args.Length == 0 || args.Any(IsHelp))
            {
                await output.WriteLineAsync(Help());
                return 0;
            }
            if (args.Length == 1 && args[0] is "--version" or "-v")
            {
                await output.WriteLineAsync(SqlValidationContract.ValidatorVersion);
                return 0;
            }
            if (!string.Equals(args[0], "validate", StringComparison.Ordinal))
            {
                await error.WriteLineAsync("error: SqlValidationUnknownCommand");
                return 1;
            }

            try
            {
                var (values, dryRun) = Parse(args.Skip(1).ToArray());
                var planPath = Required(values, "--plan", "SqlValidationPlanMissing");
                var outputPath = Required(values, "--out", "SqlValidationOutputMissing");
                var plan = SqlValidationPlanReader.Read(planPath);

                ISqlValidationProbeExecutor? executor = null;
                if (!dryRun)
                {
                    var environmentName = Required(values, "--connection-env", "SqlValidationConnectionEnvironmentMissing");
                    if (!EnvironmentName().IsMatch(environmentName))
                        throw new SqlValidationHarnessException("SqlValidationConnectionEnvironmentInvalid");
                    var connectionString = Environment.GetEnvironmentVariable(environmentName);
                    if (string.IsNullOrWhiteSpace(connectionString))
                        throw new SqlValidationHarnessException("SqlValidationConnectionUnavailable");
                    executor = new NpgsqlValidationProbeExecutor(connectionString);
                }
                else if (values.ContainsKey("--connection-env"))
                {
                    throw new SqlValidationHarnessException("SqlValidationDryRunConnectionNotAllowed");
                }

                var result = await new SqlValidationHarnessRunner().RunAsync(plan, outputPath, executor, dryRun, cancellationToken);
                await output.WriteLineAsync("TraceMap SQL validation summary written.");
                await output.WriteLineAsync($"Completion: {result.CompletionClassification}");
                await output.WriteLineAsync($"Assertions: {result.Assertions.Count}");
                return 0;
            }
            catch (SqlValidationHarnessException exception)
            {
                await error.WriteLineAsync($"error: {exception.Classification}");
                return 1;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await error.WriteLineAsync("error: SqlValidationCanceled");
                return 1;
            }
            catch
            {
                await error.WriteLineAsync("error: SqlValidationUnhandledFailure");
                return 1;
            }
        }

        internal static (IReadOnlyDictionary<string, string> Values, bool DryRun) Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var dryRun = false;
            for (var index = 0; index < args.Length; index++)
            {
                var key = args[index];
                if (key == "--dry-run")
                {
                    if (dryRun) throw new SqlValidationHarnessException("SqlValidationDuplicateArgument");
                    dryRun = true;
                    continue;
                }
                if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new SqlValidationHarnessException("SqlValidationInvalidArguments");
                if (key is not ("--plan" or "--out" or "--connection-env"))
                    throw new SqlValidationHarnessException("SqlValidationUnknownArgument");
                if (!values.TryAdd(key, args[++index]))
                    throw new SqlValidationHarnessException("SqlValidationDuplicateArgument");
            }
            return (values, dryRun);
        }

        private static string Required(IReadOnlyDictionary<string, string> values, string key, string classification) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new SqlValidationHarnessException(classification);
        private static bool IsHelp(string value) => value is "--help" or "-h" or "help";
        private static string Help() => """
            TraceMap SQL validation harness

            Usage:
              tracemap-sql-validation validate --plan <sql-validation-plan.v1.json> --connection-env <ENV_NAME> --out <new-summary.json>
              tracemap-sql-validation validate --plan <sql-validation-plan.v1.json> --out <new-summary.json> --dry-run

            The harness executes only compiled-in, parameterized, read-only PostgreSQL catalog probes.
            It never prints connection material, target identifiers, SQL results, or provider error details.
            """;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,63}$", RegexOptions.CultureInvariant)] private static partial Regex EnvironmentName();
}
