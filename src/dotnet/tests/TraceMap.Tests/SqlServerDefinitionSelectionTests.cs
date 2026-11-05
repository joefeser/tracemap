using System.Text;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class SqlServerDefinitionSelectionTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string PathName = "Install.SqlDataProvider";
    private const string Original = "PRINT N'OMITTED_SENTINEL';\nGO\n"
        + "CREATE TABLE {databaseOwner}[{objectQualifier}Items](Id int NOT NULL, Name nvarchar(50));\nGO\n"
        + "CREATE PROC {databaseOwner}[{objectQualifier}ReadItems] @id int\nAS\n"
        + "SELECT Name AS Label, Id FROM {databaseOwner}{objectQualifier}Items;\nGO\n"
        + "PRINT N'OTHER_OMITTED_SENTINEL';\nGO\n";
    internal static SqlDefinitionSelectionInput Input(string original = Original) => new(
        SqlServerDefinitionSelection.InputVersion, "local-only", "web", Revision, "selected-install-definitions",
        "ExampleServer", "ExampleDatabase", [new(PathName, Hash(original))],
        [new(PathName, 3, 4, "table", "Items"), new(PathName, 5, 8, "procedure", "ReadItems")],
        new("dbo", "Trial_", "review-scenario"));
    private static string Hash(string value) => SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(value));
    private static Dictionary<string, string> Sources(string value = Original) => new() { [PathName] = value };
    private static SqlLocalExport Produce(SqlDefinitionSelectionInput? input = null, string original = Original) =>
        SqlServerDefinitionSelection.Produce(SqlServerSchemaEvidence.Json(input ?? Input(original)), Sources(original));

    [Fact]
    public void Selection_binds_original_bytes_explicit_settings_projected_bytes_and_original_lines()
    {
        var export = Produce();
        Assert.Equal(SqlServerDefinitionSelection.ExportVersion, export.Version);
        Assert.Equal("partial", export.Coverage);
        Assert.Equal(SqlServerSchemaEvidence.Digest(Input()), export.BoundedInputSha256);
        var evidence = Assert.IsType<SqlDefinitionSelectionEvidence>(export.Selection);
        Assert.Equal("review-scenario", evidence.Input.Template.ValuesOrigin);
        Assert.Equal(Hash(Original), Assert.Single(evidence.Input.Files).Sha256);
        var table = Assert.Single(export.Objects, item => item.Kind == "table");
        Assert.Equal("dbo", table.Schema); Assert.Equal("Trial_Items", table.Name);
        Assert.Equal(new SqlLocalSpan(PathName, 3, 3), table.Span);
        Assert.Equal(new[] { "Id", "Name" }, table.Table!.Columns.Select(column => column.Name));
        Assert.All(table.Table.Columns, column => Assert.Equal(3, column.Line));
        var proc = Assert.Single(export.Objects, item => item.Kind == "procedure");
        Assert.Equal("Trial_ReadItems", proc.Name);
        Assert.Equal(new SqlLocalSpan(PathName, 5, 7), proc.Span);
        Assert.Equal(5, Assert.Single(proc.Parameters).Span.StartLine);
        Assert.Equal("unknown", proc.ResultShape!.Completeness);
        Assert.Empty(proc.ResultShape.Columns);
        Assert.Equal("selected-script-context-not-modeled", proc.ResultShape.UnknownReason);
        var reference = Assert.Single(export.Dependencies);
        Assert.Equal(table.Id, reference.TargetId); Assert.Equal(7, reference.Span.StartLine);
        Assert.Equal(PathName, reference.Span.FilePath);
        var projected = evidence.Projections[0];
        Assert.Equal(Hash("CREATE TABLE [dbo].[Trial_Items](Id int NOT NULL, Name nvarchar(50));\nGO\n"), projected.ProjectedSha256);
        Assert.Equal(1, projected.OwnerReplacements); Assert.Equal(1, projected.QualifierReplacements);
        Assert.Equal(2, evidence.Projections[1].OwnerReplacements);
        Assert.Equal(2, evidence.Projections[1].QualifierReplacements);
        Assert.All(export.Gaps, gap => Assert.Equal(PathName, gap.Span.FilePath));
        Assert.Contains(export.Gaps, gap => gap.Kind == "selection-omits-script-and-session-context");
        Assert.DoesNotContain("SENTINEL", SqlServerSchemaEvidence.Json(export));
        Assert.Equal(SqlServerSchemaEvidence.Json(export), SqlServerSchemaEvidence.Json(Produce()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sample_")]
    public void Empty_qualifier_is_an_explicit_value_and_crlf_bom_lines_stay_original(string qualifier)
    {
        var original = "\uFEFF" + Original.Replace("\n", "\r\n", StringComparison.Ordinal);
        var input = Input(original) with { Template = new("dbo", qualifier, "owner-supplied") };
        var export = Produce(input, original);
        Assert.Equal(5, export.Objects.Single(item => item.Kind == "procedure").Span.StartLine);
        Assert.Equal(qualifier + "ReadItems", export.Objects.Single(item => item.Kind == "procedure").Name);
    }

    [Theory]
    [InlineData("owner-missing")]
    [InlineData("owner-empty")]
    [InlineData("owner-injection")]
    [InlineData("qualifier-missing")]
    [InlineData("qualifier-injection")]
    [InlineData("origin-missing")]
    [InlineData("wrong-kind")]
    [InlineData("wrong-name")]
    [InlineData("overlap")]
    [InlineData("fragment-start")]
    [InlineData("fragment-end")]
    [InlineData("unlisted-file")]
    public void Missing_settings_unsafe_values_wrong_identities_and_fragment_selection_refuse(string kind)
    {
        var input = Input();
        input = kind switch
        {
            "owner-missing" => input with { Template = input.Template with { DatabaseOwner = null! } },
            "owner-empty" => input with { Template = input.Template with { DatabaseOwner = "" } },
            "owner-injection" => input with { Template = input.Template with { DatabaseOwner = "dbo]; DROP TABLE X;--" } },
            "qualifier-missing" => input with { Template = input.Template with { ObjectQualifier = null! } },
            "qualifier-injection" => input with { Template = input.Template with { ObjectQualifier = "x]--" } },
            "origin-missing" => input with { Template = input.Template with { ValuesOrigin = "" } },
            "wrong-kind" => input with { Selections = [input.Selections[0] with { Kind = "view" }, input.Selections[1]] },
            "wrong-name" => input with { Selections = [input.Selections[0] with { Name = "Other" }, input.Selections[1]] },
            "overlap" => input with { Selections = [input.Selections[0], input.Selections[0]] },
            "fragment-start" => input with { Selections = [input.Selections[0] with { StartLine = 4 }, input.Selections[1]] },
            "fragment-end" => input with { Selections = [input.Selections[0] with { EndLine = 3 }, input.Selections[1]] },
            _ => input with { Selections = [input.Selections[0] with { SourcePath = "missing.sql" }] }
        };
        Assert.ThrowsAny<ArgumentException>(() => Produce(input));
    }

    [Theory]
    [InlineData("-- {databaseOwner}\n")]
    [InlineData("-- {objectQualifier}\n")]
    [InlineData("SELECT N'{databaseOwner}' AS Value;\n")]
    [InlineData("SELECT N'{objectQualifier}' AS Value;\n")]
    [InlineData("SELECT \"{objectQualifier}\" AS Value;\n")]
    public void Template_occurrences_in_comments_or_literals_are_refused_rather_than_rewritten(string body)
    {
        var original = "CREATE PROC {databaseOwner}[{objectQualifier}ReadItems] AS\n" + body + "GO";
        var input = Input(original) with { Selections = [new(PathName, 1, original.Split('\n').Length, "procedure", "ReadItems")] };
        Assert.Contains("template-in-literal-or-comment-unsupported", Assert.Throws<ArgumentException>(() => Produce(input, original)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PRINT N'head\nGO\nCREATE TABLE dbo.Items(Id int);\nGO\ntail';\nGO")]
    [InlineData("/* head\nGO\nCREATE TABLE dbo.Items(Id int);\nGO\ntail */\nGO")]
    public void Go_inside_multiline_tokens_is_not_a_selection_boundary(string original)
    {
        var input = Input(original) with { Selections = [new(PathName, 3, 4, "table", "Items")], Template = new("dbo", "", "review-scenario") };
        Assert.Contains("whole-batch-boundaries-required", Assert.Throws<ArgumentException>(() => Produce(input, original)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Larger_original_is_not_parsed_as_selected_sql_and_unselected_drift_still_refuses()
    {
        var original = Original + "-- " + new string('x', 300_000);
        var input = Input(original);
        var export = Produce(input, original);
        Assert.Equal(2, export.Objects.Count);
        Assert.ThrowsAny<ArgumentException>(() => Produce(input, original + "x"));
        var settingsChanged = Produce(input with { Template = new("dbo", "Other_", "review-scenario") }, original);
        Assert.NotEqual(export.BoundedInputSha256, settingsChanged.BoundedInputSha256);
        Assert.NotEqual(export.Objects[0].Id, settingsChanged.Objects[0].Id);
        Assert.ThrowsAny<ArgumentException>(() => Produce(Input(new string('x', SqlServerDefinitionSelection.MaxFileBytes + 1)), new string('x', SqlServerDefinitionSelection.MaxFileBytes + 1)));
    }

    [Theory]
    [InlineData("CREATE TABLE {unknownOwner}[{objectQualifier}Items](Id int);\nGO")]
    [InlineData("CREATE TABLE [{databaseOwner}][{objectQualifier}Items](Id int);\nGO")]
    [InlineData("CREATE TABLE dbo.Items(Id int); CREATE TABLE dbo.Extra(Id int);\nGO")]
    [InlineData("INSERT INTO dbo.Items VALUES(1);\nGO")]
    [InlineData("CREATE TABLE dbo.Items(Id int);\nGO 2\nPRINT 1;")]
    public void Unknown_macros_multiple_definitions_data_and_repeated_batches_refuse(string original)
    {
        var end = original.Split('\n').Length;
        if (original.Contains("GO 2", StringComparison.Ordinal)) end = 2;
        var input = Input(original) with { Selections = [new(PathName, 1, end, "table", "Items")], Template = new("dbo", "", "review-scenario") };
        Assert.ThrowsAny<ArgumentException>(() => Produce(input, original));
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    [InlineData(100000, false)]
    public void Larger_original_comment_recursion_is_bounded_before_entering_the_lexer(int depth, bool accepted)
    {
        var prefix = string.Concat(Enumerable.Repeat("/*", depth)) + string.Concat(Enumerable.Repeat("*/", depth));
        var original = Original.Replace("PRINT N'OMITTED_SENTINEL';", prefix, StringComparison.Ordinal);
        if (accepted) Assert.Equal(2, Produce(original: original).Objects.Count);
        else Assert.Contains("original-comment-depth-limit", Assert.Throws<ArgumentException>(() => Produce(original: original)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("double")]
    [InlineData("bracket")]
    public void Comment_depth_guard_respects_escaped_quoted_tokens(string kind)
    {
        var text = string.Concat(Enumerable.Repeat("/*", 300));
        var prefix = kind switch
        {
            "single" => "PRINT N'escaped '' " + text + "';",
            "double" => "SELECT \"escaped \"\" " + text + "\";",
            _ => "SELECT [escaped ]] " + text + "];"
        };
        var original = Original.Replace("PRINT N'OMITTED_SENTINEL';", prefix, StringComparison.Ordinal);
        Assert.Equal(2, Produce(original: original).Objects.Count);
    }

    [Theory]
    [InlineData("CREATE TABLE dbo.Items(Id int);\r")]
    [InlineData("CREATE TABLE dbo.Items(\rId int);\nGO\n")]
    public void Lone_carriage_returns_refuse_before_lexer_line_numbers_can_disagree_with_selection_lines(string original)
    {
        var input = Input(original) with { Selections = [new(PathName, 1, original.Split('\n').Length, "table", "Items")],
            Template = new("dbo", "", "review-scenario") };
        Assert.Contains("original-line-ending-unsupported", Assert.Throws<ArgumentException>(() => Produce(input, original)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Admission_rejects_forged_projection_spans_shapes_or_original_and_preserves_old_routes()
    {
        var input = Input(); var json = SqlServerSchemaEvidence.Json(input); var export = Produce();
        Assert.Equal(SqlServerSchemaEvidence.Json(export), SqlServerSchemaEvidence.Json(SqlServerDefinitionSelection.Admit(SqlServerSchemaEvidence.Json(export), json, Sources())));
        foreach (var bad in new[] { export with { Selection = null }, export with { BoundedInputSha256 = new('0', 64) },
            export with { Objects = [export.Objects[0] with { Span = new(PathName, 1, 1) }, export.Objects[1]] },
            export with { Selection = export.Selection! with { Projections = [] } } })
            Assert.ThrowsAny<ArgumentException>(() => SqlServerDefinitionSelection.Admit(SqlServerSchemaEvidence.Json(bad), json, Sources()));
        Assert.ThrowsAny<Exception>(() => SqlServerLocalDefinitions.Produce(json, Sources()));
        Assert.ThrowsAny<Exception>(() => SqlServerDdlEvidence.Produce(json, Sources()));
        Assert.ThrowsAny<Exception>(() => SqlServerDdlResultEvidence.Produce(json, Sources()));
        Assert.ThrowsAny<Exception>(() => SqlServerDdlTableResultEvidence.Produce(json, Sources()));
        Assert.Throws<OperationCanceledException>(() => SqlServerDefinitionSelection.Produce(json, Sources(), new CancellationToken(true)));
    }

    [Fact]
    public async Task Cli_keeps_original_read_only_emits_no_body_and_rechecks_original_bytes()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "originals"); Directory.CreateDirectory(source);
        var original = Original + "-- " + new string('x', 300_000);
        var file = Path.Combine(source, PathName); File.WriteAllText(file, original);
        var manifest = Path.Combine(temp.Path, "input.json"); File.WriteAllText(manifest, SqlServerSchemaEvidence.Json(Input(original)));
        var outputRoot = Path.Combine(temp.Path, "collected");
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(["sqlserver-definitions", "collect", "--input", manifest, "--source-root", source, "--out", outputRoot], output, error) == 0, error.ToString());
        Assert.Equal(original, File.ReadAllText(file));
        Assert.Equal(3, Directory.GetFiles(outputRoot).Length);
        Assert.DoesNotContain("SENTINEL", File.ReadAllText(Path.Combine(outputRoot, "definitions.local.json")) + File.ReadAllText(Path.Combine(outputRoot, "report.md")));
        Assert.Empty(Directory.GetFiles(outputRoot, "*.sql"));
        var plan = SqlServerLocalDefinitionsCommand.Inspect(manifest, source, default);
        Assert.Equal(PathName, Assert.Single(plan.SourceFiles).Path);
        File.AppendAllText(file, "\n-- unselected original changed");
        Assert.ThrowsAny<ArgumentException>(() => SqlServerLocalDefinitionsCommand.Recheck(plan, default));
    }
}
