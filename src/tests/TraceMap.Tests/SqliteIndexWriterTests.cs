using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class SqliteIndexWriterTests
{
    [Fact]
    public async Task Scan_writes_manifest_and_all_jsonl_facts_to_sqlite()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Sample.cs"), """
            public sealed class CustomerProfile
            {
                public string PrimaryEmail { get; set; } = "";

                public void Load(HttpClient client)
                {
                    var profile = new CustomerProfile();
                    _ = profile.PrimaryEmail;
                    client.GetAsync("/profiles");
                }
            }
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);
        Assert.Equal(0, exitCode);

        var sqlitePath = Path.Combine(outputPath, "index.sqlite");
        var jsonlPath = Path.Combine(outputPath, "facts.ndjson");
        Assert.True(File.Exists(sqlitePath));

        var jsonlCount = File.ReadLines(jsonlPath).Count();
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();

        Assert.Equal(1L, await ExecuteScalarAsync<long>(connection, "select count(*) from scan_manifest;"));
        Assert.Equal(jsonlCount, await ExecuteScalarAsync<long>(connection, "select count(*) from facts;"));
        Assert.Equal("Level3SyntaxAnalysis", await ExecuteScalarAsync<string>(connection, "select analysis_level from scan_manifest;"));
        Assert.Equal("NotRun", await ExecuteScalarAsync<string>(connection, "select build_status from scan_manifest;"));

        var memberAccessCount = await ExecuteScalarAsync<long>(
            connection,
            "select count(*) from facts where fact_type = 'MemberAccessName' and target_symbol = 'PrimaryEmail' and evidence_tier = 'Tier3SyntaxOrTextual';");
        Assert.Equal(1L, memberAccessCount);

        var invocationCount = await ExecuteScalarAsync<long>(
            connection,
            "select count(*) from facts where fact_type = 'InvocationName' and target_symbol = 'GetAsync' and file_path = 'Sample.cs';");
        Assert.Equal(1L, invocationCount);

        var propertiesJson = await ExecuteScalarAsync<string>(
            connection,
            "select properties_json from facts where fact_type = 'PropertyDeclared' and target_symbol = 'PrimaryEmail';");
        using var properties = JsonDocument.Parse(propertiesJson);
        Assert.Equal("PrimaryEmail", properties.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Scan_writes_required_sqlite_indexes()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Only.cs"), "public sealed class Only { }");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);
        Assert.Equal(0, exitCode);

        await using var connection = new SqliteConnection($"Data Source={Path.Combine(outputPath, "index.sqlite")}");
        await connection.OpenAsync();

        var indexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'facts';";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexNames.Add(reader.GetString(0));
        }

        Assert.Contains("ix_facts_type", indexNames);
        Assert.Contains("ix_facts_rule", indexNames);
        Assert.Contains("ix_facts_target_symbol", indexNames);
        Assert.Contains("ix_facts_contract_element", indexNames);
        Assert.Contains("ix_facts_file", indexNames);
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
