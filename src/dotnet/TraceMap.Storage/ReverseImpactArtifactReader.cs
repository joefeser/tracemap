using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Storage;

public sealed record ReverseImpactArtifact(
    ScanManifest Manifest,
    IReadOnlyList<CodeFact> Facts);

public sealed class ReverseImpactArtifactException : Exception
{
    public ReverseImpactArtifactException(string errorCode, string message, Exception? innerException = null)
        : base($"{errorCode}: {message}", innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

/// <summary>
/// Loads one immutable TraceMap scan snapshot from its standard SQLite artifact.
/// Combined or mixed-snapshot indexes fail closed instead of being flattened.
/// </summary>
public static class ReverseImpactArtifactReader
{
    public const int DefaultMaxFacts = 1_000_000;
    public const int MaximumFacts = 1_000_000;

    public static async Task<ReverseImpactArtifact> ReadAsync(
        string indexPath,
        int maxFacts = DefaultMaxFacts,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(indexPath) || !File.Exists(indexPath))
        {
            throw Error("ReverseImpactArtifactUnavailable", "The requested TraceMap index is unavailable.");
        }

        if (maxFacts is < 1 or > MaximumFacts)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFacts),
                $"Reverse-impact input fact limit must be between 1 and {MaximumFacts}.");
        }

        var fullPath = Path.GetFullPath(indexPath);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        }.ToString();

        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await SetQueryOnlyAsync(connection, cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: true);

            if (!await TableExistsAsync(connection, transaction, "scan_manifest", cancellationToken)
                || !await TableExistsAsync(connection, transaction, "facts", cancellationToken)
                || await TableExistsAsync(connection, transaction, "index_sources", cancellationToken))
            {
                throw Error(
                    "ReverseImpactArtifactSchemaUnsupported",
                    "Reverse impact requires one standard TraceMap scan index, not a combined or unrelated SQLite artifact.");
            }

            var manifest = await ReadManifestAsync(connection, transaction, cancellationToken);
            var factCount = await CountFactsAsync(connection, transaction, cancellationToken);
            if (factCount == 0)
            {
                throw Error("ReverseImpactArtifactEmpty", "The TraceMap index contains no facts.");
            }

            if (factCount > maxFacts)
            {
                throw Error(
                    "ReverseImpactArtifactFactLimitExceeded",
                    $"The TraceMap index contains {factCount} facts, exceeding the configured limit of {maxFacts}.");
            }

            var facts = await ReadFactsAsync(connection, transaction, manifest, factCount, cancellationToken);
            return new ReverseImpactArtifact(manifest, Array.AsReadOnly(facts));
        }
        catch (ReverseImpactArtifactException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Error("ReverseImpactArtifactJsonInvalid", "The TraceMap index contains invalid JSON metadata.", exception);
        }
        catch (SqliteException exception)
        {
            throw Error("ReverseImpactArtifactUnreadable", "The TraceMap index could not be read as a supported SQLite artifact.", exception);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            throw Error(
                "ReverseImpactArtifactSchemaUnsupported",
                "The TraceMap index contains values that do not match the standard scan schema.",
                exception);
        }
    }

    private static async Task SetQueryOnlyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "pragma query_only = on;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<ScanManifest> ReadManifestAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText = "select count(*) from scan_manifest;";
        if (Convert.ToInt64(await countCommand.ExecuteScalarAsync(cancellationToken)) != 1)
        {
            throw Error(
                "ReverseImpactArtifactSnapshotInvalid",
                "The TraceMap index must contain exactly one scan manifest.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select scan_id, repo, commit_sha, manifest_json from scan_manifest limit 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw Error("ReverseImpactArtifactSnapshotInvalid", "The TraceMap index does not contain a scan manifest.");
        }

        var storedScanId = reader.GetString(0);
        var storedRepo = reader.GetString(1);
        var storedCommitSha = reader.GetString(2);
        var manifest = JsonSerializer.Deserialize<ScanManifest>(reader.GetString(3), JsonOptions.Stable)
            ?? throw Error("ReverseImpactArtifactSnapshotInvalid", "The TraceMap scan manifest could not be parsed.");
        if (!string.Equals(storedScanId, manifest.ScanId, StringComparison.Ordinal)
            || !string.Equals(storedRepo, manifest.RepoName, StringComparison.Ordinal)
            || !string.Equals(storedCommitSha, manifest.CommitSha, StringComparison.Ordinal))
        {
            throw Error(
                "ReverseImpactArtifactSnapshotInvalid",
                "The scan manifest columns and serialized manifest identify different snapshots.");
        }

        return manifest;
    }

    private static async Task<int> CountFactsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select count(*) from facts;";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    private static async Task<CodeFact[]> ReadFactsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ScanManifest manifest,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select fact_id,
                   scan_id,
                   repo,
                   commit_sha,
                   project_path,
                   fact_type,
                   rule_id,
                   evidence_tier,
                   source_symbol,
                   target_symbol,
                   contract_element,
                   file_path,
                   start_line,
                   end_line,
                   snippet_hash,
                   extractor_id,
                   extractor_version,
                   properties_json
            from facts
            order by fact_id;
            """;
        var facts = new List<CodeFact>(expectedCount);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var properties = ReadProperties(reader.GetString(17));
            var fact = new CodeFact(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                new EvidenceSpan(
                    reader.GetString(11),
                    reader.GetInt32(12),
                    reader.GetInt32(13),
                    reader.IsDBNull(14) ? null : reader.GetString(14),
                    reader.GetString(15),
                    reader.GetString(16)),
                properties);
            if (!string.Equals(fact.ScanId, manifest.ScanId, StringComparison.Ordinal)
                || !string.Equals(fact.Repo, manifest.RepoName, StringComparison.Ordinal)
                || !string.Equals(fact.CommitSha, manifest.CommitSha, StringComparison.Ordinal))
            {
                throw Error(
                    "ReverseImpactArtifactMixedSnapshot",
                    "A fact does not belong to the index scan manifest's repository and commit snapshot.");
            }

            facts.Add(fact);
        }

        if (facts.Count != expectedCount)
        {
            throw Error(
                "ReverseImpactArtifactChangedDuringRead",
                "The TraceMap index fact count changed while it was being read.");
        }

        return facts.ToArray();
    }

    private static IReadOnlyDictionary<string, string> ReadProperties(string json)
    {
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions.Stable)
            ?? throw Error("ReverseImpactArtifactPropertiesInvalid", "A fact properties object is unavailable.");
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in parsed)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                throw Error(
                    "ReverseImpactArtifactPropertiesInvalid",
                    "A fact properties object contains a blank key or null value.");
            }

            properties[key] = value;
        }

        return properties;
    }

    private static ReverseImpactArtifactException Error(
        string code,
        string message,
        Exception? innerException = null) => new(code, message, innerException);
}
