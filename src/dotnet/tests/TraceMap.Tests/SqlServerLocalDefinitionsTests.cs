using System.Text;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

/// <summary>Authored expectations, independent of the AST visitor. No server or private inputs.</summary>
public sealed class SqlServerLocalDefinitionsTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string Table = "CREATE TABLE dbo.Items (Id int NOT NULL, Name nvarchar(50) NULL);\nGO\n";
    private const string Procedure = "CREATE PROC dbo.ReadItems @id int, @count int OUTPUT AS\nBEGIN\nSET NOCOUNT ON;\nSELECT Name AS Label, Id FROM dbo.Items;\nEND;";
    private static Dictionary<string, string> Sources(string sql) => new(StringComparer.Ordinal) { ["schema.sql"] = sql };
    internal static string Input(IReadOnlyDictionary<string, string> sources, string revision = Revision) =>
        SqlServerSchemaEvidence.Json(new SqlLocalInput(SqlServerLocalDefinitions.InputVersion, "local-only", "web", revision,
            "example-definitions", "ExampleServer", "ExampleDatabase", sources.Select(pair =>
                new SqlServerDdlFile(pair.Key, SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(pair.Value)))).ToArray()));
    private static SqlLocalExport Produce(string sql) => SqlServerLocalDefinitions.Produce(Input(Sources(sql)), Sources(sql));

    [Fact]
    public void Declared_parameters_columns_and_references_have_independent_order_and_spans()
    {
        var sql = "USE ExampleDatabase;\nGO\nSET ANSI_NULLS ON;\nGO\nSET QUOTED_IDENTIFIER ON;\nGO\n" + Table + Procedure;
        var export = Produce(sql);
        Assert.Equal("local-only", export.Visibility);
        Assert.Equal("partial", export.Coverage);
        Assert.Equal("no-aggregate-clean-verdict", export.Verdict);
        var table = Assert.Single(export.Objects, item => item.Kind == "table");
        Assert.Equal(new[] { "Id", "Name" }, table.Table!.Columns.Select(column => column.Name));
        var proc = Assert.Single(export.Objects, item => item.Kind == "procedure");
        Assert.Equal(new[] { "@id", "@count" }, proc.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new[] { "input", "output-capable" }, proc.Parameters.Select(parameter => parameter.Direction));
        Assert.All(proc.Parameters, parameter => Assert.Equal("int", parameter.DeclaredType));
        Assert.Equal(new SqlLocalSpan("schema.sql", 9, 13), proc.Span);
        Assert.Equal("complete", proc.ResultShape!.Completeness);
        Assert.Equal(new[] { "Label", "Id" }, proc.ResultShape.Columns.Select(column => column.Name));
        Assert.Equal(new[] { 0, 1 }, proc.ResultShape.Columns.Select(column => column.Ordinal));
        var reference = Assert.Single(export.Dependencies);
        Assert.Equal(table.Id, reference.TargetId);
        Assert.Equal("declared-definition-only", reference.Resolution);
        Assert.Equal(new SqlLocalSpan("schema.sql", 12, 12), reference.Span);
        Assert.All(export.Objects, item => Assert.Equal(SqlServerLocalDefinitions.RuleId, item.RuleId));
        Assert.All(export.Gaps, gap => Assert.Equal(EvidenceTiers.Tier4Unknown, gap.Tier));
        Assert.Equal(SqlServerSchemaEvidence.GeneratorSha256, export.GeneratorSha256);
        Assert.Matches("^[0-9a-f]{64}$", export.ParserSha256);
        Assert.Equal(SqlServerSchemaEvidence.Json(export), SqlServerSchemaEvidence.Json(Produce(sql)));
        Assert.Equal(export.Objects.Select(item => item.Span), Produce("\uFEFF" + sql).Objects.Select(item => item.Span));
    }

    [Theory]
    [InlineData("IF @id = 1 SELECT Id FROM dbo.Items; ELSE SELECT Name FROM dbo.Items;")]
    [InlineData("SELECT * FROM dbo.Items;")]
    [InlineData("SELECT i.Id FROM dbo.Items i JOIN dbo.Items j ON i.Id = j.Id;")]
    [InlineData("SELECT Id FROM dbo.Items WHERE Id = @id;")]
    [InlineData("EXEC(N'SELECT 1 AS Hidden');")]
    [InlineData("SELECT Id FROM dbo.Items; SELECT Name FROM dbo.Items;")]
    [InlineData("SELECT Id FROM Items;")]
    [InlineData("SELECT Id FROM OtherDatabase.dbo.Items;")]
    public void Unsupported_bodies_never_claim_complete_shapes(string body)
    {
        var export = Produce(Table + "CREATE PROC dbo.ReadItems @id int AS " + body);
        var shape = Assert.Single(export.Objects, item => item.Kind == "procedure").ResultShape!;
        Assert.Equal("unknown", shape.Completeness);
        Assert.Empty(shape.Columns);
        Assert.NotEmpty(export.Gaps);
    }

    [Fact]
    public void Missing_unqualified_and_cross_database_names_do_not_bind_to_local_definitions()
    {
        var export = Produce(Table + "CREATE PROC dbo.ReadItems AS SELECT a.Id FROM OtherDatabase.dbo.Items a; EXEC dbo.Missing; EXEC Missing; EXEC OtherDatabase.dbo.ReadItems;");
        Assert.Equal(4, export.Dependencies.Count);
        Assert.All(export.Dependencies, item => Assert.Null(item.TargetId));
        Assert.Equal(3, export.Dependencies.Count(item => item.Resolution == "unqualified-or-external-unknown"));
        Assert.Single(export.Dependencies, item => item.Resolution == "definition-not-admitted-unknown");
    }

    [Theory]
    [InlineData("ALTER TABLE dbo.Items ADD Extra int;")]
    [InlineData("DROP TABLE dbo.Items;")]
    [InlineData("SET QUOTED_IDENTIFIER OFF;")]
    public void Unapplied_schema_or_interpretation_changes_invalidate_complete_shapes(string change)
    {
        var export = Produce(Table + change + "\nGO\n" + Procedure);
        Assert.Equal("unknown", Assert.Single(export.Objects, item => item.Kind == "table").Table!.Completeness);
        Assert.Equal("unknown", Assert.Single(export.Objects, item => item.Kind == "procedure").ResultShape!.Completeness);
    }

    [Theory]
    [InlineData("CREATE PROC dbo.ReadItems AS SELECT 1 AS Id;\nGO 2")]
    [InlineData("CREATE PROC dbo.ReadItems AS SELECT $(Unknown) AS Id;")]
    [InlineData("-- $(EvenAComment)\nCREATE PROC dbo.ReadItems AS SELECT 1 AS Id;")]
    [InlineData("CREATE PROC dbo.ReadItems AS SELECT FROM;")]
    [InlineData("CREATE PROC ReadItems AS SELECT 1 AS Id;")]
    [InlineData("USE OtherDatabase;\nGO\nCREATE PROC dbo.ReadItems AS SELECT 1 AS Id;")]
    [InlineData("INSERT INTO dbo.Items VALUES (1, N'ROW_SENTINEL');")]
    [InlineData("EXEC dbo.ReadItems;")]
    [InlineData("CREATE TABLE dbo.Items(Id int);\nGO\nCREATE TABLE dbo.items(Id int);")]
    public void Invalid_or_executable_definition_sets_refuse_admission(string sql) => Assert.ThrowsAny<ArgumentException>(() => Produce(sql));

    [Fact]
    public void Changed_inputs_forged_exports_and_public_routes_are_refused()
    {
        var sources = Sources(Table + Procedure);
        var input = Input(sources);
        var export = SqlServerLocalDefinitions.Produce(input, sources);
        Assert.Equal(SqlServerSchemaEvidence.Json(export), SqlServerSchemaEvidence.Json(
            SqlServerLocalDefinitions.Admit(SqlServerSchemaEvidence.Json(export), input, sources)));
        foreach (var changed in new[] { export with { GeneratorSha256 = new('0', 64) }, export with { BoundedInputSha256 = new('0', 64) },
            export with { ParserSha256 = new('0', 64) }, export with { Objects = [] }, export with { Visibility = "public" } })
            Assert.ThrowsAny<ArgumentException>(() => SqlServerLocalDefinitions.Admit(SqlServerSchemaEvidence.Json(changed), input, sources));
        Assert.ThrowsAny<ArgumentException>(() => SqlServerLocalDefinitions.Produce(input, Sources(Table + Procedure + "\n-- drift")));
        Assert.Throws<System.Text.Json.JsonException>(() => SqlServerDdlEvidence.Produce(input, sources));
        var publicInput = SqlServerSchemaEvidence.Json(new SqlServerDdlInput(SqlServerDdlEvidence.InputVersion, "web", Revision,
            "ExampleServer", "ExampleDatabase", false, sources.Select(pair => new SqlServerDdlFile(pair.Key,
                SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(pair.Value)))).ToArray()));
        Assert.ThrowsAny<ArgumentException>(() => SqlServerDdlEvidence.Produce(publicInput, sources));
        Assert.ThrowsAny<ArgumentException>(() => SqlServerLocalDefinitions.ReadInput(input.Replace("local-only", "public", StringComparison.Ordinal)));
        Assert.ThrowsAny<ArgumentException>(() => Produce(new string(' ', SqlServerDdlEvidence.MaxFileBytes + 1)));
        Assert.Throws<OperationCanceledException>(() => SqlServerLocalDefinitions.Produce(input, sources, new CancellationToken(true)));
    }

    [Fact]
    public void Foreign_keys_views_and_functions_are_inventory_not_runtime_or_shape_proofs()
    {
        var export = Produce(Table + "CREATE TABLE dbo.Links(Id int REFERENCES dbo.Items(Id));\nGO\n"
            + "CREATE VIEW dbo.ItemView AS SELECT Id FROM dbo.Items;\nGO\n"
            + "CREATE FUNCTION dbo.One() RETURNS int AS BEGIN RETURN 1; END;\nGO\n"
            + "CREATE PROC dbo.ReadItems AS SELECT dbo.One() AS Value;");
        Assert.Equal(5, export.Objects.Count);
        Assert.Contains(export.Dependencies, item => item.Kind == "function" && item.Name == "One" && item.TargetId is not null);
        Assert.Contains(export.Dependencies, item => item.SourceId == export.Objects.Single(obj => obj.Name == "Links").Id
            && item.Name == "Items" && item.TargetId is not null);
        Assert.Equal(2, export.Gaps.Count(gap => gap.Kind == "view-or-function-shape-not-modeled"));
        Assert.All(export.Objects.Where(item => item.Kind is "view" or "function"), item => Assert.Null(item.ResultShape));
    }

    [Fact]
    public void Strict_manifest_and_lexical_limits_fail_closed()
    {
        var input = Input(Sources(Table));
        Assert.ThrowsAny<ArgumentException>(() => SqlServerLocalDefinitions.ReadInput(input.Replace("\"files\":", "\"files\":[],\"files\":", StringComparison.Ordinal)));
        Assert.Throws<System.Text.Json.JsonException>(() => SqlServerLocalDefinitions.ReadInput(input.Replace("\"files\":", "\"typo\":true,\"files\":", StringComparison.Ordinal)));
        Assert.ThrowsAny<ArgumentException>(() => SqlServerLocalDefinitions.ReadInput(input.Replace("schema.sql", "../schema.sql", StringComparison.Ordinal)));
        var complexity = "CREATE PROC dbo.ReadItems AS SELECT " + new string('(', 129) + "1" + new string(')', 129) + " AS Id;";
        Assert.Contains("syntax-complexity-limit", Assert.Throws<ArgumentException>(() => Produce(complexity)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_refuses_source_links_and_output_aliases_into_input()
    {
        if (OperatingSystem.IsWindows()) return; // Symlink privileges are not assumed on Windows.
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "sql"); Directory.CreateDirectory(source);
        var original = Path.Combine(temp.Path, "original.sql"); File.WriteAllText(original, Table);
        var file = Path.Combine(source, "schema.sql"); File.CreateSymbolicLink(file, original);
        var manifest = Path.Combine(temp.Path, "input.json"); File.WriteAllText(manifest, Input(Sources(Table)));
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(["sqlserver-definitions", "verify", "--input", manifest, "--source-root", source], output, error));
        File.Delete(file); File.WriteAllText(file, Table);
        var alias = Path.Combine(temp.Path, "alias"); Directory.CreateSymbolicLink(alias, source);
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(["sqlserver-definitions", "collect", "--input", manifest, "--source-root", source,
            "--out", Path.Combine(alias, "output")], output, error));
        Assert.False(Directory.Exists(Path.Combine(source, "output")));
        var plan = SqlServerLocalDefinitionsCommand.Inspect(manifest, source, default);
        var checks = 0;
        Assert.ThrowsAny<ArgumentException>(() => SqlServerLocalDefinitionsCommand.Publish(Path.Combine(temp.Path, "late-drift"), plan.Export, default, () =>
        {
            if (++checks == 2) File.AppendAllText(file, "\n-- late change");
            SqlServerLocalDefinitionsCommand.Recheck(plan, default);
        }));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "late-drift")));
        Assert.Empty(Directory.GetDirectories(temp.Path, ".sqlserver-report-*"));
    }

    [Fact]
    public async Task Cli_publishes_queryable_local_sidecar_without_default_body_or_literal_text()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "sql"); Directory.CreateDirectory(source);
        var sql = Table + "CREATE PROC dbo.ReadItems @label nvarchar(50) = N'DEFAULT_SENTINEL' AS BEGIN SET NOCOUNT ON; SELECT N'BODY_SENTINEL' AS Label; END;";
        var sources = Sources(sql);
        File.WriteAllText(Path.Combine(source, "schema.sql"), sql);
        var manifest = Path.Combine(temp.Path, "input.json"); File.WriteAllText(manifest, Input(sources));
        var destination = Path.Combine(temp.Path, "collected");
        var args = new[] { "sqlserver-definitions", "collect", "--input", manifest, "--source-root", source, "--out", destination };
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(args, output, error) == 0, error.ToString());
        var json = File.ReadAllText(Path.Combine(destination, "definitions.local.json"));
        var report = File.ReadAllText(Path.Combine(destination, "report.md"));
        Assert.DoesNotContain("SENTINEL", json + report);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(destination, "sql-evidence.sqlite"), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        {
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = "select count(*) from sql_parameters"; Assert.Equal(1L, command.ExecuteScalar());
            command.CommandText = "select completeness from sql_result_shapes"; Assert.Equal("complete", command.ExecuteScalar());
            command.CommandText = "select generator_sha256 from sql_collection_metadata";
            Assert.Equal(SqlServerLocalDefinitionsCommand.HashFile(typeof(WorkspaceCommand).Assembly.Location), command.ExecuteScalar());
            command.CommandText = "select payload_json from sql_collection_metadata";
            var payload = (string)command.ExecuteScalar()!; Assert.DoesNotContain("SENTINEL", payload);
            command.CommandText = "select bounded_input_sha256 from sql_collection_metadata";
            Assert.Equal(SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(payload)), command.ExecuteScalar());
        }
        Assert.False(File.Exists(Path.Combine(destination, "scan-manifest.json")));
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(args, output, error));
        Assert.Equal(json, File.ReadAllText(Path.Combine(destination, "definitions.local.json")));
        var plan = SqlServerLocalDefinitionsCommand.Inspect(manifest, source, default);
        File.AppendAllText(Path.Combine(source, "schema.sql"), "\n-- drift");
        Assert.ThrowsAny<ArgumentException>(() => SqlServerLocalDefinitionsCommand.Publish(Path.Combine(temp.Path, "drift"), plan.Export, default,
            () => SqlServerLocalDefinitionsCommand.Recheck(plan, default)));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "drift")));
    }
}
