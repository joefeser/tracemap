using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace TraceMap.SqlValidation;

public sealed class NpgsqlValidationProbeExecutor(string connectionString) : ISqlValidationProbeExecutor
{
    private const int CommandTimeoutSeconds = 15;

    public async Task<IReadOnlyDictionary<string, bool>> ExecuteAsync(
        IReadOnlyList<SqlValidationPlanCheck> checks,
        CancellationToken cancellationToken)
    {
        try
        {
            var builder = BuildConnectionString(connectionString);
            await using var dataSource = NpgsqlDataSource.Create(builder.ConnectionString);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var results = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var check in checks)
            {
                try
                {
                    await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                    await ExecuteScalarAsync(connection, transaction, "SET TRANSACTION READ ONLY", [], cancellationToken);
                    var result = await ExecuteCheckAsync(connection, transaction, check, cancellationToken);
                    await transaction.RollbackAsync(cancellationToken);
                    results[check.Code] = result;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (NpgsqlException)
                {
                    // Omission is the internal categorical signal for one indeterminate probe.
                    // Provider details never leave this process.
                }
            }
            return results;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (SqlValidationHarnessException) { throw; }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            throw new SqlValidationProbeException();
        }
    }

    private static NpgsqlConnectionStringBuilder BuildConnectionString(string value)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(value)
            {
                ApplicationName = "tracemap-sql-validation",
                CommandTimeout = CommandTimeoutSeconds,
                IncludeErrorDetail = false,
                Pooling = false,
                Timeout = 10
            };
            return builder;
        }
        catch (ArgumentException)
        {
            throw new SqlValidationHarnessException("SqlValidationConnectionConfigurationInvalid");
        }
    }

    private static async Task<bool> ExecuteCheckAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SqlValidationPlanCheck check,
        CancellationToken cancellationToken) => check.Code switch
    {
        "postgres.server-version-compatible" => await ServerVersionMatchesAsync(connection, transaction, check.ExpectedMajor!.Value, cancellationToken),
        "postgres.required-extension-available" => await AllNamedValuesMatchAsync(connection, transaction,
            "SELECT count(*) = $1 FROM pg_catalog.pg_extension WHERE extname = ANY($2)", check.Identifiers, cancellationToken),
        "postgres.migration-schema-present" => await AllNamedValuesMatchAsync(connection, transaction,
            "SELECT count(*) = $1 FROM unnest($2::text[]) AS expected(name) WHERE pg_catalog.to_regclass(expected.name) IS NOT NULL", check.Identifiers, cancellationToken),
        "postgres.permission-probe-authorized" => await AllNamedValuesMatchAsync(connection, transaction,
            "SELECT count(*) = $1 FROM unnest($2::text[]) AS expected(name) WHERE pg_catalog.has_schema_privilege(current_user, expected.name, 'USAGE')", check.Identifiers, cancellationToken),
        "postgres.archive-function-callable" => await AllNamedValuesMatchAsync(connection, transaction,
            "SELECT count(*) = $1 FROM unnest($2::text[]) AS expected(name) WHERE pg_catalog.to_regprocedure(expected.name) IS NOT NULL AND pg_catalog.has_function_privilege(current_user, pg_catalog.to_regprocedure(expected.name), 'EXECUTE')", check.Identifiers, cancellationToken),
        "postgres.scheduled-job-registered" => await ScheduledJobsMatchAsync(connection, transaction, check.Identifiers, cancellationToken),
        _ => throw new SqlValidationHarnessException("SqlValidationUnsupportedProbe")
    };

    private static async Task<bool> ServerVersionMatchesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int expectedMajor,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(connection, transaction,
            "SELECT current_setting('server_version_num')::integer", [], cancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) / 10000 == expectedMajor;
    }

    private static async Task<bool> AllNamedValuesMatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string commandText,
        IReadOnlyList<string> identifiers,
        CancellationToken cancellationToken)
    {
        var parameters = new NpgsqlParameter[]
        {
            new() { NpgsqlDbType = NpgsqlDbType.Integer, Value = identifiers.Count },
            new() { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text, Value = identifiers.ToArray() }
        };
        var result = await ExecuteScalarAsync(connection, transaction, commandText, parameters, cancellationToken);
        return result is true;
    }

    private static async Task<bool> ScheduledJobsMatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<string> identifiers,
        CancellationToken cancellationToken)
    {
        var catalogPresent = await ExecuteScalarAsync(connection, transaction,
            "SELECT pg_catalog.to_regclass('cron.job') IS NOT NULL", [], cancellationToken);
        if (catalogPresent is not true) return false;
        return await AllNamedValuesMatchAsync(connection, transaction,
            "SELECT count(*) = $1 FROM cron.job WHERE jobname = ANY($2) AND active", identifiers, cancellationToken);
    }

    private static async Task<object?> ExecuteScalarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string commandText,
        IReadOnlyList<NpgsqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(commandText, connection, transaction)
        {
            CommandTimeout = CommandTimeoutSeconds
        };
        foreach (var parameter in parameters) command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
