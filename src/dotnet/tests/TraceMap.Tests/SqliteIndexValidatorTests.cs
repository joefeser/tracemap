using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqliteIndexValidatorTests
{
    [Fact]
    public void Validates_schema_commit_and_fact_count()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "index.sqlite");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                create table scan_manifest (commit_sha text not null);
                create table facts (fact_id text primary key);
                insert into scan_manifest values ('abc');
                insert into facts values ('fact-1');
                """;
            command.ExecuteNonQuery();
        }

        SqliteIndexValidator.Validate(path, "abc", 1);
        Assert.Throws<InvalidOperationException>(() => SqliteIndexValidator.Validate(path, "def", 1));
        Assert.Throws<InvalidOperationException>(() => SqliteIndexValidator.Validate(path, "abc", 2));
    }

    [Fact]
    public void Header_only_and_missing_schema_are_not_valid_indexes()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "index.sqlite");
        File.WriteAllBytes(path, "SQLite format 3\0"u8.ToArray());
        Assert.Throws<SqliteException>(() => SqliteIndexValidator.Validate(path, "abc", 1));
    }

    [Fact]
    public async Task Cli_validation_rejects_a_header_only_index()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "index.sqlite");
        File.WriteAllBytes(path, "SQLite format 3\0"u8.ToArray());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await TraceMapCommand.RunAsync(
            ["validate-index", "--index", path, "--commit", "abc", "--facts", "1"], output, error);

        Assert.NotEqual(0, exit);
        Assert.DoesNotContain("index-valid", output.ToString(), StringComparison.Ordinal);
    }
}
