using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class SqlStreamedLocalDefinitionsTests
{
    private static string Input(string text) => JsonSerializer.Serialize(new SqlLocalInput(
        SqlServerLocalDefinitions.StreamInputVersion, "local-only", "web", new('a', 40), "public-stream",
        "ExampleServer", "ExampleDatabase", [new("export.sql", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))))]),
        new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static SqlLocalExport Produce(string text) => SqlServerLocalDefinitions.ProduceStreamed(Input(text), _ => new MemoryStream(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void Deep_control_tree_is_refused_before_recursive_evidence_visitors()
    {
        var text = "CREATE PROC dbo.ReadItems AS " + string.Concat(Enumerable.Repeat("IF 1=1 ", 512)) + "SELECT 1 AS Id;\nGO\n";
        var failure = Assert.Throws<ArgumentException>(() => Produce(text));
        var guard = Assert.IsType<SqlSyntaxComplexityLimit>(failure.Data["sqlSyntaxComplexityLimit"]);
        Assert.Equal("ast-depth", guard.Metric);
        Assert.Equal(257, guard.Observed); Assert.Equal(256, guard.Limit);
        Assert.Equal("export.sql", guard.FilePath);
    }

    [Fact]
    public void Flat_wide_table_and_repeated_type_facets_are_not_nested_complexity()
    {
        var columns = string.Join(",\n", Enumerable.Range(0, 512).Select(i => $"C{i} nvarchar(50) NULL"));
        var text = "CREATE TABLE dbo.WideItems (\n" + columns + ");\nGO\nCREATE PROC dbo.ReadItems AS SELECT C0 AS Id FROM dbo.WideItems;\nGO\n";
        var export = Produce(text);
        Assert.Equal(512, export.Objects.Single(item => item.Kind == "table").Table!.Columns.Count);
        Assert.Equal("complete", export.Objects.Single(item => item.Kind == "procedure").ResultShape!.Completeness);
        var legacy = JsonSerializer.Deserialize<SqlLocalInput>(Input(text), new JsonSerializerOptions(JsonSerializerDefaults.Web))! with { Version = SqlServerLocalDefinitions.InputVersion };
        Assert.Contains("syntax-complexity-limit", Assert.Throws<ArgumentException>(() => SqlServerLocalDefinitions.Produce(
            JsonSerializer.Serialize(legacy, new JsonSerializerOptions(JsonSerializerDefaults.Web)), new Dictionary<string, string> { ["export.sql"] = text })).Message);
    }

    [Fact]
    public void Sequential_conditionals_parse_but_do_not_invent_complete_result_shapes()
    {
        var text = "CREATE PROC dbo.ReadItems AS BEGIN\n" + string.Concat(Enumerable.Repeat("IF (1=1) BEGIN SELECT 1 AS Id; END;\n", 512)) + "END;\nGO\n";
        var export = Produce(text);
        Assert.Equal("unknown", Assert.Single(export.Objects).ResultShape!.Completeness);
    }

    [Theory]
    [InlineData("parenthesis-depth")]
    [InlineData("control-construct-count")]
    public void Streamed_syntax_budgets_still_fail_closed_with_measured_guard_location(string metric)
    {
        var text = metric == "parenthesis-depth"
            ? "CREATE PROC dbo.ReadItems AS SELECT " + new string('(', 129) + "1" + new string(')', 129) + " AS Id;\nGO\n"
            : "CREATE PROC dbo.ReadItems AS BEGIN " + string.Concat(Enumerable.Repeat("IF (1=1) SELECT 1;", 4096)) + " END;\nGO\n";
        var failure = Assert.Throws<ArgumentException>(() => Produce(text));
        var guard = Assert.IsType<SqlSyntaxComplexityLimit>(failure.Data["sqlSyntaxComplexityLimit"]);
        Assert.Equal(metric, guard.Metric); Assert.Equal("export.sql", guard.FilePath);
        Assert.Equal(1, guard.Line); Assert.Equal(guard.Limit + 1, guard.Observed);
        Assert.Contains("syntax-complexity-limit", failure.Message);
    }

    [Fact]
    public void Literal_and_comment_parentheses_do_not_count_as_syntax_depth()
    {
        var text = "/*" + new string('(', 2048) + "*/\nCREATE PROC dbo.ReadItems AS SELECT N'" + new string('(', 2048) + "' AS Value;\nGO\n";
        Assert.Single(Produce(text).Objects);
    }

    [Fact]
    public void Database_mismatch_diagnostic_uses_verified_whole_input_and_exact_source_location()
    {
        var text = "USE [ExampleCatalog];\nGO\nCREATE PROC dbo.ReadItems AS SELECT 1 AS Id;\nGO\n";
        var failure = Assert.Throws<ArgumentException>(() => Produce(text));
        var context = Assert.IsType<SqlLocalDatabaseContext>(failure.Data["sqlLocalDatabaseContext"]);
        Assert.Equal("sql-local-database-context-mismatch", failure.Message);
        Assert.Equal("ExampleDatabase", context.ManifestDatabase);
        Assert.Equal("ExampleCatalog", context.SqlDatabase);
        Assert.Equal(new SqlLocalSpan("export.sql", 1, 1), context.Span);
        Assert.True(context.WholeInputInspected);
        Assert.Equal(1, context.DistinctDatabaseCount);
        Assert.Contains("source-digest-mismatch", Assert.Throws<ArgumentException>(() =>
            SqlServerLocalDefinitions.ProduceStreamed(Input(text), _ => new MemoryStream(Encoding.UTF8.GetBytes(text + "-- drift")))).Message);
    }

    [Fact]
    public void Multiple_database_declarations_are_not_a_single_database_repair()
    {
        var text = "USE [ExampleCatalog];\nGO\nCREATE PROC dbo.ReadItems AS SELECT 1 AS Id;\nGO\nUSE [OtherCatalog];\nGO\n";
        var context = Assert.IsType<SqlLocalDatabaseContext>(Assert.Throws<ArgumentException>(() => Produce(text)).Data["sqlLocalDatabaseContext"]);
        Assert.True(context.WholeInputInspected);
        Assert.Equal(2, context.DistinctDatabaseCount);
        Assert.Null(context.SqlDatabase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bom_marked_utf16_preserves_raw_digest_unicode_and_original_lines(bool bigEndian)
    {
        var text = "-- public\r\nCREATE PROC dbo.ReadItems AS SELECT N'café漢字😀' AS Value;\r\nGO\r\nCREATE PROC dbo.ReadOther AS SELECT 1 AS Id;\r\nGO\r\n";
        var encoding = new UnicodeEncoding(bigEndian, true, true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        var input = JsonSerializer.Deserialize<SqlLocalInput>(Input(text), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var json = JsonSerializer.Serialize(input with { Files = [new("export.sql", Convert.ToHexStringLower(SHA256.HashData(bytes)))] }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var export = SqlServerLocalDefinitions.ProduceStreamed(json, _ => new MemoryStream(bytes));
        Assert.Equal(2, export.Objects.Count);
        Assert.Equal(2, export.Objects[0].Span.StartLine);
        Assert.Equal(4, export.Objects[1].Span.StartLine);
        Assert.All(export.Objects, item => Assert.Equal("complete", item.ResultShape!.Completeness));
        Assert.Contains("source-digest-mismatch", Assert.Throws<ArgumentException>(() => SqlServerLocalDefinitions.ProduceStreamed(json,
            _ => new MemoryStream(bytes.Concat(encoding.GetBytes(" ")).ToArray()))).Message);
        Assert.Contains("source-digest-mismatch", Assert.Throws<ArgumentException>(() => SqlServerLocalDefinitions.ProduceStreamed(Input(text),
            _ => new MemoryStream(bytes))).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Malformed_utf16_inside_otherwise_valid_SQL_is_not_replacement_decoded(bool bigEndian)
    {
        var encoding = new UnicodeEncoding(bigEndian, true, true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes("CREATE PROC dbo.ReadItems AS SELECT N'"))
            .Concat(bigEndian ? new byte[] { 0xd8, 0 } : new byte[] { 0, 0xd8 })
            .Concat(encoding.GetBytes("' AS Value;\nGO\n")).ToArray();
        Assert.Throws<DecoderFallbackException>(() => SqlServerLocalDefinitions.ProduceStreamed(Input(""), _ => new MemoryStream(bytes)));
    }

    [Theory]
    [InlineData(new byte[] { 0xff, 0xfe, 0x00, 0xd8 })] // unpaired surrogate
    [InlineData(new byte[] { 0xfe, 0xff, 0xd8, 0x00 })]
    [InlineData(new byte[] { 0xff, 0xfe, 0x61 })] // truncated code unit
    [InlineData(new byte[] { 0xff, 0xfe, 0, 0 })] // UTF32 is not UTF16
    [InlineData(new byte[] { 0, 0, 0xfe, 0xff })]
    public void Invalid_utf16_and_utf32_are_not_replaced_or_guessed(byte[] bytes)
        => Assert.ThrowsAny<ArgumentException>(() => SqlServerLocalDefinitions.ProduceStreamed(Input(""), _ => new MemoryStream(bytes)));

    [Theory]
    [InlineData(667158)]
    [InlineData(6671580)]
    public void Normal_export_sizes_parse_in_verified_batches_with_original_line_spans(int size)
    {
        var text = new StringBuilder();
        var count = 0;
        while (text.Length + 12100 < size)
        {
            text.Append("/*").Append('x', 12000).Append("*/\n");
            text.Append($"CREATE PROC dbo.Read{count++} AS SELECT 1 AS Id;\nGO\n");
        }
        var remaining = size - text.Length;
        text.Append("--").Append('x', remaining - 2);
        Assert.Equal(size, Encoding.UTF8.GetByteCount(text.ToString()));
        var export = Produce(text.ToString());
        Assert.Equal(SqlServerLocalDefinitions.StreamExportVersion, export.Version);
        Assert.Equal(count, export.Objects.Count);
        Assert.Equal(2, export.Objects[0].Span.StartLine);
        Assert.Equal(5, export.Objects[1].Span.StartLine);
        Assert.All(export.Objects, item => Assert.Equal("complete", item.ResultShape!.Completeness));
    }

    [Fact]
    public void Go_inside_multiline_literals_and_comments_is_not_a_boundary_and_global_mutation_stays_unknown()
    {
        var text = "/*\nGO\n*/\nCREATE TABLE dbo.Items (Id int);\nGO\nCREATE PROC dbo.ReadItems AS SELECT 'a\nGO\nb' AS Value;\nGO\nSET ANSI_NULLS OFF;\nGO\n";
        var export = Produce(text);
        Assert.Equal(2, export.Objects.Count);
        Assert.Equal(6, export.Objects[1].Span.StartLine);
        Assert.Equal("unknown", export.Objects[1].ResultShape!.Completeness);
        Assert.Equal("unknown", export.Objects[0].Table!.Completeness);
    }

    [Theory]
    [InlineData("CREATE PROC dbo.ReadItems AS SELECT 1;\nGO 2\n")]
    [InlineData("CREATE PROC dbo.ReadItems AS SELECT 1;\nGO\nCREATE PROC dbo.ReadItems AS SELECT 2;\nGO\n")]
    [InlineData("INSERT INTO dbo.Items VALUES (1);\nGO\n")]
    [InlineData("$(private)\nGO\n")]
    public void Existing_repetition_duplicate_executable_and_sqlcmd_refusals_remain(string text)
        => Assert.Throws<ArgumentException>(() => Produce(text));

    [Fact]
    public void Whole_original_digest_is_checked_before_any_export_is_returned()
    {
        var text = "CREATE PROC dbo.ReadItems AS SELECT 1 AS Id;\nGO\n";
        Assert.Contains("source-digest-mismatch", Assert.Throws<ArgumentException>(() =>
            SqlServerLocalDefinitions.ProduceStreamed(Input(text), _ => new MemoryStream(Encoding.UTF8.GetBytes(text + "-- changed")))).Message);
    }

    [Fact]
    public void Tables_and_references_across_batches_keep_existing_global_resolution()
    {
        var export = Produce("CREATE PROC dbo.ReadItems AS SELECT Id FROM dbo.Items;\nGO\nCREATE TABLE dbo.Items (Id int);\nGO\n");
        Assert.Equal("complete", export.Objects[0].ResultShape!.Completeness);
        Assert.Contains(export.Dependencies, item => item.Name == "Items" && item.Resolution == "declared-definition-only");
    }

    [Fact]
    public void Source_above_eight_GiB_is_refused_before_any_read()
    {
        var text = "CREATE PROC dbo.ReadItems AS SELECT 1;";
        Assert.Contains("stream-file-size-limit", Assert.Throws<ArgumentException>(() =>
            SqlServerLocalDefinitions.ProduceStreamed(Input(text), _ => new OverLimitStream())).Message);
    }

    private sealed class OverLimitStream : MemoryStream
    {
        public override long Length => SqlServerLocalDefinitions.MaxStreamFileBytes + 1;
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("must not read");
    }

    [Fact]
    public void V1_admission_and_batch_budgets_are_not_raised()
    {
        var text = "--" + new string('x', SqlServerLocalDefinitions.MaxStreamBatchBytes) + "\n";
        Assert.Throws<ArgumentException>(() => Produce(text));
        Assert.Throws<ArgumentException>(() => SqlServerLocalDefinitions.ReadInput(Input("")));
        Assert.Equal(8L * 1024 * 1024 * 1024, SqlServerLocalDefinitions.MaxStreamFileBytes);
    }
}
