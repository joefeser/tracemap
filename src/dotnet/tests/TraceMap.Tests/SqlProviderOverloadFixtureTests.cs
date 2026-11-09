using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqlProviderOverloadFixtureTests
{
    [Theory]
    [InlineData("provider-overloads", "ProviderOverloads", "DbDataAdapter")]
    [InlineData("provider-overloads-csharp", "ProviderOverloadsCSharp", "DataAdapter")]
    public void Same_name_readers_have_distinct_compiled_overload_chains(string folder, string assembly, string fillOwner)
    {
        using var temp = new TempDirectory();
        var matrix = Path.Combine(SqlDatabaseMatrixCells.Repo(), "samples", "sql-database-matrix");
        var repo = Path.Combine(matrix, folder);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dll = Path.Combine(repo, "bin", config, "net472", assembly + ".dll");
        var scan = ScanEngine.Scan(new ScanOptions(repo, temp.Path, CompiledInputPaths: [dll], IlBodyEvidence: true) { CompiledOnly = true });
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = index, Pooling = false }.ToString());
        connection.Open();
        using var query = connection.CreateCommand();
        query.CommandText = File.ReadAllText(Path.Combine(matrix, "provider-overloads", "review.sql"));
        using var rows = query.ExecuteReader();
        var overloads = new HashSet<string>(StringComparer.Ordinal);
        string? fill = null;
        foreach (var (root, adapter) in new[] { ("FromDriver", "OdbcDataAdapter"), ("FromLedger", "SqlDataAdapter") })
        {
            Assert.True(rows.Read());
            Assert.Equal(root, rows.GetString(0));
            Assert.True(overloads.Add(rows.GetString(1)));
            Assert.True(overloads.Add(rows.GetString(2)));
            Assert.Contains(adapter + ")|member:5:.ctor", rows.GetString(3));
            fill ??= rows.GetString(4);
            Assert.Equal(fill, rows.GetString(4));
            Assert.Contains(fillOwner.Length + ":" + fillOwner + ")|member:4:Fill", fill);
        }
        Assert.False(rows.Read());
    }
}
