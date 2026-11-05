using System.Diagnostics;
using System.Text;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class SqlServerSourceBindingTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string FileName = "Providers.cs";
    private const string Code = """
        using System.Data;
        using System.Text;
        using System.Globalization;
        namespace Example;
        public abstract partial class DataProvider {
          public abstract IDataReader ExecuteReader(string p, params object[] a);
          public abstract IDataReader ExecuteReader(int t, string p, params object[] a);
          public IDataReader GetItems(int portalId) { return this.ExecuteReader("ReadItems", portalId); }
        }
        public class SqlDataProvider : DataProvider {
          static DatabaseConnectionProvider connection = DatabaseConnectionProvider.Instance() ?? new SqlDatabaseConnectionProvider();
          public override IDataReader ExecuteReader(string p, params object[] a) {
            return connection.ExecuteReader(this.ConnectionString, CommandType.StoredProcedure, 0, this.DatabaseOwner + this.ObjectQualifier + p, a);
          }
        }
        public class SqlDatabaseConnectionProvider : DatabaseConnectionProvider {
          public override IDataReader ExecuteReader(string c, CommandType type, int timeout, string sql, params object[] args) {
            return PetaPocoHelper.ExecuteReader(c, type, timeout, sql, args);
          }
        }
        public static class PetaPocoHelper {
          const string SqlProviderName = "System.Data.SqlClient";
          public static IDataReader ExecuteReader(string c, CommandType type, int timeout, string sql, params object[] args) {
            var db = new Database(c, SqlProviderName) { EnableAutoSelect = false };
            if (type == CommandType.StoredProcedure) { sql = DataUtil.BuildExec(sql, args); }
            if (timeout > 0) { db.CommandTimeout = timeout; }
            try { return db.ExecuteReader(sql, args); } catch (Exception ex) { Log(ex); throw; }
          }
        }
        public static class DataUtil {
          public static string BuildExec(string proc, params object[] args) {
            var text = new StringBuilder(";Exec ");
            text.Append(proc);
            for (int i = 0; i < args.Length; i++) {
              var format = " @{0}";
              string name = null;
              var param = args[i] as IDataParameter;
              if (param != null) { format = " @{1}=@{0}"; name = param.ParameterName; }
              text.AppendFormat(CultureInfo.InvariantCulture, format, i, name);
              if (i < args.Length - 1) { text.Append(","); }
            }
            return text.ToString();
          }
        }
        // BODY_SENTINEL: this must not be emitted.
        """;
    private const string Sql = "CREATE PROC {databaseOwner}[{objectQualifier}ReadItems] @Id int AS SELECT 1 AS Id;\nGO";
    private static string Hash(string value) => SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(value));
    private static Dictionary<string, string> Sources(string code = Code) => new() { [FileName] = code };
    private static Dictionary<string, string> SqlSources(string sql = Sql) => new() { ["install.sql"] = sql };
    private static string SqlInput(string revision = Revision, string sql = Sql) => SqlServerSchemaEvidence.Json(new SqlDefinitionSelectionInput(
        SqlServerDefinitionSelection.InputVersion, "local-only", "web", revision, "source-binding", "ExampleServer", "ExampleDatabase",
        [new("install.sql", Hash(sql))], [new("install.sql", 1, sql.Split('\n').Length, "procedure", "ReadItems")], new("dbo", "Trial_", "review-scenario")));
    private static SqlSourceBindingInput Input(string code = Code, string revision = Revision, string? sqlInput = null) => new(
        SqlServerSourceBinding.InputVersion, "local-only", "web", revision, "dnn-reader-wrapper/1", "review-scenario", Hash(sqlInput ?? SqlInput(revision)),
        new("dbo", "Trial_", "review-scenario"), [
            new("entry", FileName, Hash(code), "Example.DataProvider", "GetItems", ["int"]),
            new("provider", FileName, Hash(code), "Example.SqlDataProvider", "ExecuteReader", ["string", "object[]"]),
            new("connection", FileName, Hash(code), "Example.SqlDatabaseConnectionProvider", "ExecuteReader", ["string", "CommandType", "int", "string", "object[]"]),
            new("helper", FileName, Hash(code), "Example.PetaPocoHelper", "ExecuteReader", ["string", "CommandType", "int", "string", "object[]"]),
            new("formatter", FileName, Hash(code), "Example.DataUtil", "BuildExec", ["string", "object[]"])]);
    private static SqlSourceBindingReport Produce(string code = Code, string sql = Sql)
    {
        var sqlInput = SqlInput(sql: sql);
        return SqlServerSourceBinding.Produce(SqlServerSchemaEvidence.Json(Input(code, sqlInput: sqlInput)), Sources(code), sqlInput, SqlSources(sql));
    }

    [Fact]
    public void Five_ast_witnesses_support_only_a_conditional_ordinal_candidate_with_retained_gaps()
    {
        var report = Produce();
        Assert.Equal("conditional-profile-candidate", report.Status);
        Assert.Equal("partial", report.Coverage); Assert.Equal("no-aggregate-clean-verdict", report.Verdict);
        Assert.All(report.Observations, item => Assert.Equal("profile-shape-supported", item.Status));
        Assert.All(report.Observations, item => Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, item.Tier));
        var parameter = Assert.Single(report.Parameters);
        Assert.Equal("portalId", parameter.SourceName); Assert.Equal(0, parameter.SourceOrdinal);
        Assert.Equal("@Id", parameter.SqlName); Assert.Equal(0, parameter.SqlOrdinal);
        Assert.Equal(report.SqlObjectId, parameter.SqlObjectId);
        Assert.Equal(8, parameter.SourceSpan.StartLine); Assert.Equal(1, parameter.SqlSpan.StartLine);
        Assert.Equal("conditional-positional-candidate", parameter.Status);
        Assert.Contains(report.Gaps, gap => gap.Kind == "external-petapoco-parameter-and-command-transport-unverified");
        Assert.Contains(report.Gaps, gap => gap.Kind == "configured-connection-provider-dispatch-unverified");
        Assert.Contains(report.Gaps, gap => gap.Kind == "compiler-symbol-and-overload-resolution-unverified");
        Assert.Contains(report.Gaps, gap => gap.Kind == "caller-cache-event-and-hydration-path-not-bound");
        Assert.DoesNotContain("BODY_SENTINEL", SqlServerSchemaEvidence.Json(report));
        Assert.DoesNotContain("new StringBuilder", SqlServerSchemaEvidence.Json(report));
        Assert.Matches("^[0-9a-f]{64}$", report.SourceParserSha256);
        Assert.Equal(SqlServerSchemaEvidence.Json(report), SqlServerSchemaEvidence.Json(Produce()));
    }

    [Theory]
    [InlineData("\"ReadItems\", portalId", "\"ReadItems\", portalId + 1")]
    [InlineData("\"ReadItems\", portalId", "NameFromElsewhere(), portalId")]
    [InlineData("return this.ExecuteReader(\"ReadItems\", portalId);", "if (portalId > 0) return this.ExecuteReader(\"ReadItems\", portalId); return null;")]
    [InlineData("public abstract IDataReader ExecuteReader(string p, params object[] a);", "public abstract IDataReader ExecuteReader(string p, params object[] a); public abstract IDataReader ExecuteReader(string p, int a);")]
    [InlineData("this.DatabaseOwner + this.ObjectQualifier + p", "this.DatabaseOwner + p")]
    [InlineData("connection.ExecuteReader(this.ConnectionString, CommandType.StoredProcedure", "connection.ExecuteReader(this.ConnectionString, CommandType.Text")]
    [InlineData("+ p, a);", "+ p, new object[] { 1 });")]
    [InlineData("PetaPocoHelper.ExecuteReader(c, type, timeout, sql, args)", "PetaPocoHelper.ExecuteReader(c, type, timeout, sql, new object[] { 2 })")]
    [InlineData("sql = DataUtil.BuildExec(sql, args);", "sql = Change(sql);")]
    [InlineData("System.Data.SqlClient", "Other.Provider")]
    [InlineData("db.ExecuteReader(sql, args)", "db.ExecuteReader(sql, new object[] { 3 })")]
    [InlineData(";Exec ", ";Select ")]
    [InlineData("i < args.Length;", "i < args.Length - 1;")]
    [InlineData("format, i, name", "format, 0, name")]
    [InlineData("args[i] as IDataParameter", "args[0] as IDataParameter")]
    [InlineData("if (param != null)", "if (param == null)")]
    [InlineData("text.AppendFormat(CultureInfo.InvariantCulture, format, i, name);", "args[0] = 42; text.AppendFormat(CultureInfo.InvariantCulture, format, i, name);")]
    public void Changed_forwarding_dispatch_or_formatter_never_emits_a_parameter_candidate(string before, string after)
    {
        Assert.Contains(before, Code, StringComparison.Ordinal);
        var report = Produce(Code.Replace(before, after, StringComparison.Ordinal));
        Assert.Equal("unknown", report.Status); Assert.Null(report.SqlObjectId); Assert.Empty(report.Parameters);
        Assert.Contains(report.Gaps, gap => gap.Kind == "wrapper-shape-outside-profile");
    }

    [Theory]
    [InlineData("using CommandType = Other.Enum;\n")]
    [InlineData("class IDataParameter {}\n")]
    public void Visible_protocol_symbol_alias_or_shadow_removes_the_candidate(string prefix)
    {
        // An extra namespace keeps file-scoped syntax valid for the shadow declaration.
        var code = prefix.StartsWith("class", StringComparison.Ordinal) ? Code + "\n" + prefix : prefix + Code;
        var report = Produce(code);
        Assert.Equal("unknown", report.Status); Assert.Empty(report.Parameters);
        Assert.Contains(report.Gaps, gap => gap.Kind == "visible-protocol-symbol-alias-or-shadow-outside-profile");
    }

    [Fact]
    public void Sql_parameter_names_are_read_from_formal_ordinal_not_guessed_from_source_spelling()
    {
        Assert.Equal("@CompletelyDifferent", Assert.Single(Produce(sql: Sql.Replace("@Id", "@CompletelyDifferent", StringComparison.Ordinal)).Parameters).SqlName);
        Assert.Empty(Produce(sql: Sql.Replace("@Id int", "@Id int, @Other int", StringComparison.Ordinal)).Parameters);
        Assert.Empty(Produce(sql: Sql.Replace("@Id int", "@Id int OUTPUT", StringComparison.Ordinal)).Parameters);
        Assert.Equal("unknown", Produce(Code.Replace("\"ReadItems\"", "\"OtherName\"", StringComparison.Ordinal)).Status);
    }

    [Fact]
    public void Strict_context_hashes_sql_origin_and_report_admission_fail_closed()
    {
        var sqlInput = SqlInput(); var input = Input(); var json = SqlServerSchemaEvidence.Json(input); var report = Produce();
        Assert.Equal(SqlServerSchemaEvidence.Json(report), SqlServerSchemaEvidence.Json(SqlServerSourceBinding.Admit(SqlServerSchemaEvidence.Json(report), json, Sources(), sqlInput, SqlSources())));
        Assert.ThrowsAny<ArgumentException>(() => SqlServerSourceBinding.Produce(json, Sources(Code + "\n// drift"), sqlInput, SqlSources()));
        Assert.ThrowsAny<ArgumentException>(() => SqlServerSourceBinding.Produce(json, Sources(), sqlInput + " ", SqlSources()));
        Assert.ThrowsAny<ArgumentException>(() => SqlServerSourceBinding.Produce(SqlServerSchemaEvidence.Json(input with { Template = input.Template with { ObjectQualifier = "Other_" } }), Sources(), sqlInput, SqlSources()));
        Assert.ThrowsAny<ArgumentException>(() => SqlServerSourceBinding.ReadInput(SqlServerSchemaEvidence.Json(input with { Methods = [input.Methods[0], input.Methods[0], .. input.Methods.Skip(2)] })));
        Assert.ThrowsAny<ArgumentException>(() => SqlServerSourceBinding.ReadInput(SqlServerSchemaEvidence.Json(input with { Visibility = "public" })));
        foreach (var bad in new[] { report with { Gaps = [] }, report with { Status = "proven" }, report with { Parameters = [] }, report with { GeneratorSha256 = new('0', 64) } })
            Assert.ThrowsAny<ArgumentException>(() => SqlServerSourceBinding.Admit(SqlServerSchemaEvidence.Json(bad), json, Sources(), sqlInput, SqlSources()));
        Assert.Throws<OperationCanceledException>(() => SqlServerSourceBinding.Produce(json, Sources(), sqlInput, SqlSources(), new CancellationToken(true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cli_checks_committed_source_writes_queryable_sidecar_and_preserves_existing_outputs(bool bom)
    {
        using var fixture = new Fixture(bom);
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(fixture.Args, output, error) == 0, error.ToString());
        var json = File.ReadAllText(Path.Combine(fixture.Destination, "binding.local.json"));
        Assert.DoesNotContain("BODY_SENTINEL", json);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(fixture.Destination, "source-sql-bindings.sqlite"), Pooling = false, Mode = SqliteOpenMode.ReadOnly }.ToString()))
        {
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = "select count(*) from binding_observations"; Assert.Equal(5L, command.ExecuteScalar());
            command.CommandText = "select count(*) from binding_parameters"; Assert.Equal(1L, command.ExecuteScalar());
            command.CommandText = "select source_commit_verified from binding_metadata"; Assert.Equal(1L, command.ExecuteScalar());
            command.CommandText = "select generator_sha256 from binding_metadata";
            Assert.Equal(SqlServerLocalDefinitionsCommand.HashFile(typeof(WorkspaceCommand).Assembly.Location), command.ExecuteScalar());
        }
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(fixture.Args, output, error));
        Assert.Equal(json, File.ReadAllText(Path.Combine(fixture.Destination, "binding.local.json")));
        Assert.False(File.Exists(Path.Combine(fixture.Destination, "scan-manifest.json")));
    }

    [Fact]
    public async Task Dirty_but_rehashed_source_refuses_before_output_because_it_is_not_the_declared_commit()
    {
        using var fixture = new Fixture();
        var code = Code + "\n// changed";
        File.WriteAllText(Path.Combine(fixture.Source, FileName), code);
        File.WriteAllText(fixture.InputPath, SqlServerSchemaEvidence.Json(Input(code, fixture.Head, fixture.SqlInputJson)));
        using var error = new StringWriter();
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(fixture.Args, TextWriter.Null, error));
        Assert.Contains("source-not-at-declared-commit", error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Destination));
    }

    [Theory]
    [InlineData("selector")]
    [InlineData("ambiguous")]
    [InlineData("nonprimitive")]
    public void Unavailable_ambiguous_or_nonprimitive_entry_remains_unknown(string kind)
    {
        var code = kind switch
        {
            "ambiguous" => Code.Replace("public IDataReader GetItems(int portalId)", "public IDataReader GetItems(int portalId) { return null; } public IDataReader GetItems(int portalId)", StringComparison.Ordinal),
            "nonprimitive" => Code.Replace("GetItems(int portalId)", "GetItems(object portalId)", StringComparison.Ordinal),
            _ => Code
        };
        var input = Input(code);
        if (kind == "selector") input = input with { Methods = [input.Methods[0] with { TypeName = "Other.DataProvider" }, .. input.Methods.Skip(1)] };
        var report = SqlServerSourceBinding.Produce(SqlServerSchemaEvidence.Json(input), Sources(code), SqlInput(), SqlSources());
        Assert.Equal("unknown", report.Status); Assert.Empty(report.Parameters);
        Assert.Contains(report.Gaps, gap => gap.Kind == "selected-method-unavailable-or-ambiguous");
    }

    [Fact]
    public async Task Source_links_stale_git_context_and_output_overlap_are_refused_without_artifacts()
    {
        using var fixture = new Fixture();
        using var error = new StringWriter();
        var overlap = (string[])fixture.Args.Clone(); overlap[^1] = Path.Combine(fixture.Source, "output");
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(overlap, TextWriter.Null, error));
        Assert.False(Directory.Exists(overlap[^1]));
        var current = SqlServerSchemaEvidence.Read<SqlSourceBindingInput>(File.ReadAllText(fixture.InputPath));
        File.WriteAllText(fixture.InputPath, SqlServerSchemaEvidence.Json(current with { CommitSha = new('1', 40) }));
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(fixture.Args, TextWriter.Null, error));
        Assert.False(Directory.Exists(fixture.Destination));
        File.WriteAllText(fixture.InputPath, SqlServerSchemaEvidence.Json(current));
        if (!OperatingSystem.IsWindows())
        {
            var source = Path.Combine(fixture.Source, FileName); var original = Path.Combine(fixture.Source, "original.cs");
            File.Move(source, original); File.CreateSymbolicLink(source, original);
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(fixture.Args, TextWriter.Null, error));
            Assert.False(Directory.Exists(fixture.Destination));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDirectory temp = new();
        public string Source { get; }
        public string Head { get; }
        public string Destination { get; }
        public string InputPath { get; }
        public string SqlInputJson { get; }
        public string[] Args { get; }
        public Fixture(bool bom = false)
        {
            Source = Path.Combine(temp.Path, "source"); Directory.CreateDirectory(Source);
            var code = bom ? "\uFEFF" + Code : Code;
            File.WriteAllText(Path.Combine(Source, FileName), code);
            Git("init", "--quiet"); Git("config", "core.autocrlf", "false"); Git("add", ".");
            Git("-c", "user.name=Synthetic Fixture", "-c", "user.email=fixture@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "synthetic source");
            Head = Git("rev-parse", "HEAD");
            var sqlRoot = Path.Combine(temp.Path, "sql"); Directory.CreateDirectory(sqlRoot); File.WriteAllText(Path.Combine(sqlRoot, "install.sql"), Sql);
            SqlInputJson = SqlInput(Head); var sqlPath = Path.Combine(temp.Path, "sql-input.json"); File.WriteAllText(sqlPath, SqlInputJson);
            InputPath = Path.Combine(temp.Path, "binding-input.json"); File.WriteAllText(InputPath, SqlServerSchemaEvidence.Json(Input(code, revision: Head, sqlInput: SqlInputJson)));
            Destination = Path.Combine(temp.Path, "collected");
            Args = ["sqlserver-source-binding", "compare", "--input", InputPath, "--source-root", Source, "--sql-input", sqlPath, "--sql-source-root", sqlRoot, "--out", Destination];
        }
        private string Git(params string[] args)
        {
            using var process = new Process { StartInfo = new("git") { WorkingDirectory = Source, RedirectStandardOutput = true, RedirectStandardError = true } };
            foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
            process.Start(); var result = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit();
            Assert.True(process.ExitCode == 0, error); return result.Trim();
        }
        public void Dispose() => temp.Dispose();
    }
}
