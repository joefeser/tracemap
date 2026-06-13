using Microsoft.Data.Sqlite;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class CombineTests
{
    [Fact]
    public async Task Combine_imports_sources_facts_symbols_and_dependency_tables()
    {
        using var temp = new TempDirectory();
        var firstRepo = Path.Combine(temp.Path, "first");
        var secondRepo = Path.Combine(temp.Path, "second");
        var firstOut = Path.Combine(temp.Path, "first-out");
        var secondOut = Path.Combine(temp.Path, "second-out");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var combinedJsonPath = Path.Combine(temp.Path, "combined-export.json");
        var combinedMermaidPath = Path.Combine(temp.Path, "combined-export.mmd");
        Directory.CreateDirectory(firstRepo);
        Directory.CreateDirectory(secondRepo);
        File.WriteAllText(Path.Combine(firstRepo, "First.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(firstRepo, "First.cs"), """
            namespace FirstRepo;

            public sealed class FirstService
            {
                public void Run()
                {
                    Save("first");
                }

                private void Save(string value)
                {
                }
            }
            """);
        File.WriteAllText(Path.Combine(secondRepo, "Second.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(secondRepo, "Second.cs"), """
            namespace SecondRepo;

            public sealed class SecondService
            {
                public void Run()
                {
                    Save("second");
                }

                private void Save(string value)
                {
                }
            }
            """);

        await RunCliAsync(["scan", "--repo", firstRepo, "--out", firstOut]);
        await RunCliAsync(["scan", "--repo", secondRepo, "--out", secondOut]);
        var firstFactCount = await CountAsync(Path.Combine(firstOut, "index.sqlite"), "facts");
        var secondFactCount = await CountAsync(Path.Combine(secondOut, "index.sqlite"), "facts");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(
            [
                "combine",
                "--index", Path.Combine(firstOut, "index.sqlite"),
                "--label", "first-service",
                "--index", Path.Combine(secondOut, "index.sqlite"),
                "--label", "second-service",
                "--out", combinedPath
            ],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("TraceMap combine completed:", output.ToString());
        Assert.True(File.Exists(combinedPath));

        await using var connection = new SqliteConnection($"Data Source={combinedPath}");
        await connection.OpenAsync();

        Assert.Equal(2L, await ExecuteScalarAsync<long>(connection, "select count(*) from index_sources;"));
        Assert.Equal(firstFactCount + secondFactCount, await ExecuteScalarAsync<long>(connection, "select count(*) from combined_facts;"));
        Assert.True(await ExecuteScalarAsync<long>(connection, "select count(*) from combined_symbols;") > 0);
        Assert.True(await ExecuteScalarAsync<long>(connection, "select count(*) from combined_call_edges where callee_symbol like '%Save%';") >= 2);
        Assert.True(await ExecuteScalarAsync<long>(connection, "select count(*) from combined_dependency_edges where edge_kind = 'calls' and target_symbol like '%Save%';") >= 2);
        Assert.Equal(0L, await ExecuteScalarAsync<long>(connection, "select count(*) from endpoint_matches;"));

        var sourceIds = await ReadStringsAsync(connection, "select source_index_id from index_sources order by label;");
        Assert.Equal(2, sourceIds.Count);
        Assert.All(sourceIds, sourceId => Assert.Matches("^[a-f0-9]{24}$", sourceId));

        var namespacedFacts = await ExecuteScalarAsync<long>(
            connection,
            """
            select count(*)
            from combined_facts
            where combined_fact_id = source_index_id || ':' || original_fact_id
              and original_fact_id is not null
              and original_scan_id = scan_id;
            """);
        Assert.Equal(firstFactCount + secondFactCount, namespacedFacts);

        var firstLabelFacts = await ExecuteScalarAsync<long>(
            connection,
            """
            select count(*)
            from combined_facts facts
            join index_sources sources on sources.source_index_id = facts.source_index_id
            where sources.label = 'first-service';
            """);
        Assert.Equal(firstFactCount, firstLabelFacts);

        await RunCliAsync(["export", "--index", combinedPath, "--out", combinedJsonPath, "--format", "json"]);
        var json = await File.ReadAllTextAsync(combinedJsonPath);
        Assert.Contains("\"sources\"", json);
        Assert.Contains("\"factsBySourceAndType\"", json);
        Assert.Contains("\"relationships\"", json);
        Assert.Contains("\"callEdges\"", json);
        Assert.Contains("\"sourceLabel\": \"first-service\"", json);
        Assert.Contains("\"sourceLabel\": \"second-service\"", json);
        Assert.DoesNotContain("public sealed class FirstService", json);

        await RunCliAsync(["export", "--index", combinedPath, "--out", combinedMermaidPath, "--format", "mermaid"]);
        var mermaid = await File.ReadAllTextAsync(combinedMermaidPath);
        Assert.StartsWith("flowchart TD", mermaid);
        Assert.Contains("first-service", mermaid);
        Assert.Contains("second-service", mermaid);
        Assert.Contains("calls", mermaid);
    }

    [Fact]
    public async Task Combine_rejects_label_count_mismatch()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var scanOut = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Only.cs"), "public sealed class Only { }");
        await RunCliAsync(["scan", "--repo", repo, "--out", scanOut]);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(
            [
                "combine",
                "--index", Path.Combine(scanOut, "index.sqlite"),
                "--label", "one",
                "--label", "extra",
                "--out", Path.Combine(temp.Path, "combined.sqlite")
            ],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("one --label value per --index", error.ToString());
    }

    [Fact]
    public async Task Combine_defaults_root_scan_labels_without_colliding()
    {
        using var temp = new TempDirectory();
        var firstRepo = Path.Combine(temp.Path, "first-root");
        var secondRepo = Path.Combine(temp.Path, "second-root");
        var firstOut = Path.Combine(temp.Path, "first-out");
        var secondOut = Path.Combine(temp.Path, "second-out");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        Directory.CreateDirectory(firstRepo);
        Directory.CreateDirectory(secondRepo);
        File.WriteAllText(Path.Combine(firstRepo, "First.cs"), "public sealed class FirstRoot { }");
        File.WriteAllText(Path.Combine(secondRepo, "Second.cs"), "public sealed class SecondRoot { }");
        await RunCliAsync(["scan", "--repo", firstRepo, "--out", firstOut]);
        await RunCliAsync(["scan", "--repo", secondRepo, "--out", secondOut]);

        await RunCliAsync(
            [
                "combine",
                "--index", Path.Combine(firstOut, "index.sqlite"),
                "--index", Path.Combine(secondOut, "index.sqlite"),
                "--out", combinedPath
            ]);

        await using var connection = new SqliteConnection($"Data Source={combinedPath}");
        await connection.OpenAsync();
        var labels = await ReadStringsAsync(connection, "select label from index_sources order by label;");
        Assert.Equal(["first-root", "second-root"], labels);
        Assert.DoesNotContain(".", labels);
        Assert.Equal(0L, await ExecuteScalarAsync<long>(
            connection,
            "select count(*) from index_sources where imported_at != json_extract(manifest_json, '$.scannedAt');"));
    }

    [Fact]
    public async Task Combine_rejects_duplicate_index_paths_and_output_overwrite()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var scanOut = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Only.cs"), "public sealed class Only { }");
        await RunCliAsync(["scan", "--repo", repo, "--out", scanOut]);
        var indexPath = Path.Combine(scanOut, "index.sqlite");

        using var duplicateOutput = new StringWriter();
        using var duplicateError = new StringWriter();
        var duplicateExitCode = await TraceMapCommand.RunAsync(
            [
                "combine",
                "--index", indexPath,
                "--label", "one",
                "--index", indexPath,
                "--label", "two",
                "--out", Path.Combine(temp.Path, "combined.sqlite")
            ],
            duplicateOutput,
            duplicateError);

        Assert.Equal(1, duplicateExitCode);
        Assert.Contains("duplicate --index path detected", duplicateError.ToString());

        using var overwriteOutput = new StringWriter();
        using var overwriteError = new StringWriter();
        var overwriteExitCode = await TraceMapCommand.RunAsync(
            [
                "combine",
                "--index", indexPath,
                "--out", indexPath
            ],
            overwriteOutput,
            overwriteError);

        Assert.Equal(1, overwriteExitCode);
        Assert.Contains("must not overwrite an input index", overwriteError.ToString());
        Assert.True(File.Exists(indexPath));
    }

    private static async Task RunCliAsync(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(args, output, error);
        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
    }

    private static async Task<long> CountAsync(string sqlitePath, string table)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();
        return await ExecuteScalarAsync<long>(connection, $"select count(*) from {table};");
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T));
    }

    private static async Task<IReadOnlyList<string>> ReadStringsAsync(SqliteConnection connection, string sql)
    {
        var values = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
