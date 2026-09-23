using Microsoft.Data.Sqlite;

namespace TraceMap.Storage;

public static class SqliteIndexValidator
{
    public static void Validate(string path, string expectedCommit, long expectedFactCount)
    {
        if (expectedFactCount <= 0)
            throw new InvalidOperationException("IndexExpectedFactsInvalid");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using var integrity = connection.CreateCommand();
        integrity.CommandText = "pragma integrity_check";
        if (!string.Equals((string?)integrity.ExecuteScalar(), "ok", StringComparison.Ordinal))
            throw new InvalidOperationException("IndexIntegrityInvalid");

        using var manifest = connection.CreateCommand();
        manifest.CommandText = "select commit_sha from scan_manifest";
        using (var reader = manifest.ExecuteReader())
        {
            if (!reader.Read() || !string.Equals(reader.GetString(0), expectedCommit, StringComparison.Ordinal)
                || reader.Read())
                throw new InvalidOperationException("IndexManifestInvalid");
        }

        using var facts = connection.CreateCommand();
        facts.CommandText = "select count(*) from facts";
        if ((long)facts.ExecuteScalar()! != expectedFactCount)
            throw new InvalidOperationException("IndexFactCountMismatch");
    }
}
