using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TraceMap.Core;
namespace TraceMap.Tests;
/// <summary>
/// Phase 6 Field&lt;T&gt; / Field(Of T) read state (direct Roslyn). Expectations follow the design matrix authored before the extractor ran;
/// every Phase 2-5 state must keep its mode-off output for the same Field-bearing input.
/// </summary>
public sealed class SqlServerFieldReadExtractionTests
{
    internal sealed record Site(SqlServerFieldReadState Field, SqlServerDataTableState DataTable, SqlServerDataSetState DataSet,
        SqlServerHelperResultState Helper, IReadOnlyDictionary<string, string> Properties);

    private const string CsPrefix = "#pragma warning disable CS0618, CS0219, CS0168\nusing System; using System.Data; using System.Data.SqlClient; using System.Linq;\n";
    private const string VbPrefix = "Imports System\nImports System.Data\nImports System.Data.SqlClient\nImports System.Linq\n";
    private const string CsFill = "var command = new SqlCommand(\"dbo.ReadItems\");\ncommand.CommandType = CommandType.StoredProcedure;\nvar adapter = new SqlDataAdapter(command);\nvar table = new DataTable();\nadapter.Fill(table);\n";
    private const string VbFill = "Dim command = New SqlCommand(\"dbo.ReadItems\")\ncommand.CommandType = CommandType.StoredProcedure\nDim adapter = New SqlDataAdapter(command)\nDim table = New DataTable()\nadapter.Fill(table)\n";
    private const string CsSetFill = "var command = new SqlCommand(\"dbo.ReadItems\");\ncommand.CommandType = CommandType.StoredProcedure;\nvar adapter = new SqlDataAdapter(command);\nvar results = new DataSet();\nadapter.Fill(results);\n";
    private const string VbSetFill = "Dim command = New SqlCommand(\"dbo.ReadItems\")\ncommand.CommandType = CommandType.StoredProcedure\nDim adapter = New SqlDataAdapter(command)\nDim results = New DataSet()\nadapter.Fill(results)\n";

    private static string Method(string language, string body, bool set = false) => language == "cs"
        ? $"static object M(string columnName, DataColumn column) {{\n{(set ? CsSetFill : CsFill)}{body}\n}}\n"
        : $"Function M(columnName As String, column As DataColumn) As Object\n{(set ? VbSetFill : VbFill)}{body}\nEnd Function\n";

    /// <summary>Compiles <paramref name="members"/> in one type and returns every Fill site's Field, DataTable, DataSet and helper states.</summary>
    internal static Site[] Extract(string language, string members, string extraTypes = "", Func<IEnumerable<MetadataReference>, IEnumerable<MetadataReference>>? references = null,
        string? header = null, string assemblyName = "Synthetic", bool register = false)
    {
        var refs = (references ?? (r => r))(SqlServerHelperResultExtractionTests.References()).ToArray();
        Compilation compilation;
        SyntaxTree tree;
        if (language == "cs")
        {
            tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText((header ?? CsPrefix) + "static class Example {\nstatic void Consume(object o) {}\n" + members + "\n}\n" + extraTypes);
            compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(assemblyName, [tree], refs, new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
        else
        {
            tree = Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxTree.ParseText(VbPrefix + "Module Example\nSub Consume(o As Object)\nEnd Sub\n" + members + "\nEnd Module\n" + extraTypes);
            compilation = Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilation.Create("Synthetic", [tree], refs, new Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionInfer: true));
        }
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        if (register)
        {
            using var workspace = new AdhocWorkspace();
            var root = Path.Combine(Path.GetTempPath(), "tracemap-field-synthetic");
            tree = tree.WithFilePath(Path.Combine(root, language == "cs" ? "Input.cs" : "Input.vb"));
            compilation = compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(tree);
            var projectId = ProjectId.CreateNewId();
            var project = workspace.AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Synthetic", "Synthetic", compilation.Language,
                filePath: Path.Combine(root, language == "cs" ? "Synthetic.csproj" : "Synthetic.vbproj"),
                documents: [DocumentInfo.Create(DocumentId.CreateNewId(projectId), Path.GetFileName(tree.FilePath), filePath: tree.FilePath)]));
            SqlServerCompilationScopes.Register(root, project, compilation, hasErrors: false);
        }
        var model = compilation.GetSemanticModel(tree);
        return tree.GetRoot().DescendantNodes().Select(n => model.GetOperation(n)).OfType<IInvocationOperation>()
            .Where(o => o.TargetMethod.Name == "Fill").Distinct().OrderBy(o => o.Syntax.SpanStart)
            .Select(o =>
            {
                var p = new SortedDictionary<string, string>();
                DatabaseOperationApi.AddProperties(p, o);
                return new Site(p.TryGetValue("sqlServerFieldReadState", out var field) ? SqlServerSchemaEvidence.Read<SqlServerFieldReadState>(field) : SqlServerFieldReads.Extract(o),
                    SqlServerSchemaEvidence.Read<SqlServerDataTableState>(p["sqlServerDataTableState"]),
                    SqlServerSchemaEvidence.Read<SqlServerDataSetState>(p["sqlServerDataSetState"]),
                    SqlServerSchemaEvidence.Read<SqlServerHelperResultState>(p["sqlServerHelperResultState"]), p);
            }).ToArray();
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void DataTable_field_reads_are_bounded_with_recorded_type_arguments_while_phase2_keeps_its_refusal(string language)
    {
        var body = language == "cs"
            ? "var row = table.Rows[0]; Consume(row[\"Id\"]); Consume(row.Field<int?>(\"Id\")); Consume(row.Field<byte[]>(2)); Consume(table.Rows[0].Field<string>(\"Name\"));\n"
                + "if (table.Rows.Count > 0) foreach (DataRow r in table.Rows) Consume(r.Field<decimal>(\"Total\"));\nreturn (table.Rows[0]).Field<string>(columnName);"
            : "Dim row = table.Rows(0)\nConsume(row(\"Id\"))\nConsume(row.Field(Of Integer?)(\"Id\"))\nConsume(row.Field(Of Byte())(2))\nConsume(table.Rows(0).Field(Of String)(\"Name\"))\n"
                + "If table.Rows.Count > 0 Then\nFor Each r As DataRow In table.Rows\nConsume(r.Field(Of Decimal)(\"Total\"))\nNext\nEnd If\nReturn table.Rows(0).Field(Of String)(columnName)";
        var site = Assert.Single(Extract(language, Method(language, body)));
        var state = site.Field;
        Assert.Equal(("sqlserver-field-read/1", "bounded", (string?)null, "datatable", "datatable", "StoredProcedure", "fill-datatable", "constructor-command"),
            (state.Version, state.Coverage, state.Gap, state.Origin, state.ResultKind, state.CommandType, state.FillOverload, state.AdapterAssociation));
        Assert.Empty(state.OriginGaps);
        Assert.Null(state.TableName);
        var only = Assert.Single(state.Sites);
        Assert.Equal(("bounded", (string?)null, (string?)null, FactFactory.Hash("dbo.ReadItems", 64)), (only.Coverage, only.Gap, only.Path, only.CommandTextHash));
        Assert.Equal("M", only.Caller!.Split('(')[0].Split('.')[^1]);
        Assert.Equal(["foreach", "rows-count"], only.Guards);
        Assert.Equal([("name-indexer", "Item", "row-local", "Id", (int?)null, (string?)null, (string?)null),
            ("field-name", "Field", "row-local", "Id", null, "System.Nullable[System.Int32]", null),
            ("field-ordinal", "Field", "row-local", null, 2, "System.Byte[]", null),
            ("field-name", "Field", "rows-index", "Name", null, "System.String", null),
            ("field-name", "Field", "foreach-row", "Total", null, "System.Decimal", null),
            ("field-name", "Field", "rows-index", null, null, "System.String", "dynamic-column-name")],
            only.Accesses.Select(a => (a.Kind, a.Member, a.RowSource, a.Name, a.Ordinal, a.TypeArgument, a.Gap)));
        // Mode-off Phase 2 output is unchanged: the same input is still refused, and no Phase 2-5 JSON carries the new member.
        Assert.Equal(("unknown", "datarow-field-extension-unsupported"), (site.DataTable.Coverage, site.DataTable.Gap));
        Assert.Equal(("unknown", "fill-overload-unsupported"), (site.DataSet.Coverage, site.DataSet.Gap));
        Assert.Equal(("unknown", "helper-return-required"), (site.Helper.Coverage, site.Helper.Gap));
        foreach (var key in new[] { "sqlServerDataTableState", "sqlServerDataSetState", "sqlServerHelperResultState", "sqlServerInternalHelperResultState" })
            Assert.DoesNotContain("typeArgument", site.Properties[key], StringComparison.Ordinal);
    }

    // Typed unknowns: the Field-mode DataTable gap, and the unchanged mode-off Phase 2 gap for the same input.
    [Theory]
    [InlineData("cs", "return table.Rows[0].Field<string>(column);", "datacolumn-access-unsupported", "datarow-field-extension-unsupported")]
    [InlineData("vb", "Return table.Rows(0).Field(Of String)(column)", "datacolumn-access-unsupported", "datarow-field-extension-unsupported")]
    [InlineData("cs", "return table.Rows[0].Field<string>(\"Name\", DataRowVersion.Original);", "row-version-access-unsupported", "datarow-field-extension-unsupported")]
    [InlineData("vb", "Return table.Rows(0).Field(Of String)(\"Name\", DataRowVersion.Original)", "row-version-access-unsupported", "datarow-field-extension-unsupported")]
    [InlineData("cs", "return table.Rows[0].Field<string>(0, DataRowVersion.Current);", "row-version-access-unsupported", "datarow-field-extension-unsupported")]
    [InlineData("cs", "return table.Rows[0].Field<string>(column, DataRowVersion.Original);", "datacolumn-access-unsupported", "datarow-field-extension-unsupported")]
    [InlineData("cs", "table.Rows[0].SetField(\"Name\", \"x\"); return null;", "datatable-mutation", "datatable-mutation")]
    [InlineData("vb", "table.Rows(0).SetField(\"Name\", \"x\")\nReturn Nothing", "datatable-mutation", "datatable-mutation")]
    [InlineData("cs", "return table.AsEnumerable().First().Field<string>(\"Name\");", "ambiguous-row-ownership", "ambiguous-row-ownership")]
    [InlineData("vb", "Return table.AsEnumerable().First().Field(Of String)(\"Name\")", "ambiguous-row-ownership", "ambiguous-row-ownership")]
    [InlineData("cs", "return table.AsEnumerable().Select(r => r.Field<string>(\"Name\")).First();", "unsupported-control-flow", "unsupported-control-flow")]
    [InlineData("vb", "Return table.AsEnumerable().Select(Function(r) r.Field(Of String)(\"Name\")).First()", "unsupported-control-flow", "unsupported-control-flow")]
    [InlineData("cs", "return DataRowExtensions.Field<string>(table.Rows[0], \"Name\");", "datarow-field-static-call-unsupported", "datarow-field-extension-unsupported")]
    [InlineData("vb", "Return DataRowExtensions.Field(Of String)(table.Rows(0), \"Name\")", "datarow-field-static-call-unsupported", "datarow-field-extension-unsupported")]
    [InlineData("cs", "var row = table.Rows[0]; return row?.Field<string>(\"Name\");", "unsupported-control-flow", "unsupported-control-flow")]
    [InlineData("cs", "var row = table.Rows[0]; row = table.Rows[1]; return row.Field<string>(\"Name\");", "row-reassignment", "row-reassignment")]
    public void Field_shapes_outside_the_matrix_are_typed_unknowns(string language, string body, string fieldGap, string phase2Gap)
    {
        var site = Assert.Single(Extract(language, Method(language, body)));
        Assert.Equal(("unknown", "field-read-origin-unbound"), (site.Field.Coverage, site.Field.Gap));
        Assert.Equal("datatable:" + fieldGap, site.Field.OriginGaps[0]);
        Assert.Equal(["datatable", "dataset", "helper", "internal-helper"], site.Field.OriginGaps.Select(g => g.Split(':')[0]));
        Assert.Empty(site.Field.Sites);
        Assert.Equal(("unknown", phase2Gap), (site.DataTable.Coverage, site.DataTable.Gap));
    }

    [Theory]
    [InlineData("cs", "return table.Rows[0][\"Name\"];", true)]
    [InlineData("vb", "Return table.Rows(0)(\"Name\")", true)]
    [InlineData("cs", "var other = new DataTable(); return other.Rows[0].Field<string>(\"Name\");", false)]
    [InlineData("vb", "Dim other = New DataTable()\nReturn other.Rows(0).Field(Of String)(\"Name\")", false)]
    public void Sites_without_a_bound_field_read_stay_their_own_route_evidence(string language, string body, bool phase2Accesses)
    {
        var site = Assert.Single(Extract(language, Method(language, body)));
        Assert.Equal(("bounded", "datatable"), (site.Field.Coverage, site.Field.Origin));
        var only = Assert.Single(site.Field.Sites);
        Assert.Equal(("unknown", "field-read-absent", (string?)null), (only.Coverage, only.Gap, only.CommandTextHash));
        Assert.Empty(only.Accesses);
        // Phase 2 still owns the indexer read; a Field read on an unfilled second table is never bound by either route.
        Assert.Equal("bounded", site.DataTable.Coverage);
        Assert.Equal(phase2Accesses, site.DataTable.Accesses.Count > 0);
    }

    [Fact]
    public void Unrenderable_type_argument_is_an_access_level_gap_and_never_a_claim()
    {
        var site = Assert.Single(Extract("cs", Method("cs", "return table.Rows[0].Field<dynamic>(\"Name\");")));
        var access = Assert.Single(Assert.Single(site.Field.Sites).Accesses);
        Assert.Equal(("field-name", (string?)null, (string?)null, "field-type-argument-unsupported"), (access.Kind, access.Name, access.TypeArgument, access.Gap));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void DataSet_first_table_field_reads_bind_and_later_tables_stay_unknown(string language)
    {
        var bodies = language == "cs"
            ? new[] { "var t = results.Tables[\"Table\"]; foreach (DataRow row in t.Rows) Consume(row.Field<int>(0)); return results.Tables[0].Rows[0].Field<string>(\"Name\");",
                "return results.Tables[1].Rows[0].Field<int>(\"Value\");" }
            : ["Dim t = results.Tables(\"Table\")\nFor Each row As DataRow In t.Rows\nConsume(row.Field(Of Integer)(0))\nNext\nReturn results.Tables(0).Rows(0).Field(Of String)(\"Name\")",
                "Return results.Tables(1).Rows(0).Field(Of Integer)(\"Value\")"];
        var bounded = Assert.Single(Extract(language, Method(language, bodies[0], set: true)));
        Assert.Equal(("bounded", "dataset", "dataset", "fill-dataset", "Table"), (bounded.Field.Coverage, bounded.Field.Origin, bounded.Field.ResultKind, bounded.Field.FillOverload, bounded.Field.TableName));
        var only = Assert.Single(bounded.Field.Sites);
        Assert.Equal(["table-local", "tables-index", "tables-name"], only.Selectors);
        Assert.Equal(["foreach"], only.Guards);
        Assert.Equal([("field-ordinal", (string?)null, (int?)0, "System.Int32"), ("field-name", "Name", null, "System.String")],
            only.Accesses.Select(a => (a.Kind, a.Name, a.Ordinal, a.TypeArgument)));
        Assert.Equal(("unknown", "datarow-field-extension-unsupported"), (bounded.DataSet.Coverage, bounded.DataSet.Gap));
        var later = Assert.Single(Extract(language, Method(language, bodies[1], set: true)));
        Assert.Equal("dataset:dataset-later-table-unsupported", later.Field.OriginGaps[1]);
        Assert.Equal("dataset-later-table-unsupported", later.DataSet.Gap);
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Private_helper_call_sites_partition_into_field_reads_absent_reads_and_typed_gaps(string language)
    {
        var callers = language == "cs"
            ? "static object A() { var t = ReadTable(\"dbo.ReadItems\"); return t.Rows[0].Field<string>(\"Name\"); }\n"
                + "static object B() { var t = ReadTable(\"dbo.ReadOther\"); return t.Rows[0][\"Title\"]; }\n"
                + "static object C(DataColumn column) { var t = ReadTable(\"dbo.ReadItems\"); return t.Rows[0].Field<string>(column); }\n"
            : "Function A() As Object\nDim t = ReadTable(\"dbo.ReadItems\")\nReturn t.Rows(0).Field(Of String)(\"Name\")\nEnd Function\n"
                + "Function B() As Object\nDim t = ReadTable(\"dbo.ReadOther\")\nReturn t.Rows(0)(\"Title\")\nEnd Function\n"
                + "Function C(column As DataColumn) As Object\nDim t = ReadTable(\"dbo.ReadItems\")\nReturn t.Rows(0).Field(Of String)(column)\nEnd Function\n";
        var helper = language == "cs" ? SqlServerHelperResultExtractionTests.CsTable : SqlServerHelperResultExtractionTests.VbTable;
        var site = Assert.Single(Extract(language, helper + callers));
        Assert.Equal(("bounded", "helper", "datatable", "constructor-parameter"), (site.Field.Coverage, site.Field.Origin, site.Field.ResultKind, site.Field.CommandTextSource));
        Assert.Equal([("bounded", (string?)null), ("unknown", "field-read-absent"), ("unknown", "datacolumn-access-unsupported")],
            site.Field.Sites.Select(s => (s.Coverage, s.Gap)));
        Assert.All(site.Field.Sites, s => Assert.Null(s.Path));
        Assert.Equal(["A", "B", "C"], site.Field.Sites.Select(s => s.Caller!.Split('(')[0].Split('.')[^1]));
        Assert.Equal(FactFactory.Hash("dbo.ReadItems", 64), site.Field.Sites[0].CommandTextHash);
        Assert.Equal(("field-name", "Name", "System.String"), (Assert.Single(site.Field.Sites[0].Accesses).Kind, site.Field.Sites[0].Accesses[0].Name, site.Field.Sites[0].Accesses[0].TypeArgument));
        // Phase 4 is unchanged: Field callers keep the mode-off refusal, the indexer caller stays bounded.
        Assert.Equal([("unknown", "datarow-field-extension-unsupported"), ("bounded", null), ("unknown", "datarow-field-extension-unsupported")],
            site.Helper.Callers.Select(c => (c.Coverage, c.Gap)));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Field_reads_inside_a_helper_body_do_not_widen_the_body(string language)
    {
        var members = language == "cs"
            ? "private static DataTable ReadBody(string procedure) {\nvar command = new SqlCommand(procedure);\ncommand.CommandType = CommandType.StoredProcedure;\nvar adapter = new SqlDataAdapter(command);\nvar table = new DataTable();\nadapter.Fill(table);\nvar name = table.Rows[0].Field<string>(\"Name\");\nreturn table;\n}\n"
                + "static object Use() { var t = ReadBody(\"dbo.ReadItems\"); return t.Rows[0][\"Name\"]; }\n"
            : "Private Function ReadBody(procedure As String) As DataTable\nDim command = New SqlCommand(procedure)\ncommand.CommandType = CommandType.StoredProcedure\nDim adapter = New SqlDataAdapter(command)\nDim table = New DataTable()\nadapter.Fill(table)\nDim name = table.Rows(0).Field(Of String)(\"Name\")\nReturn table\nEnd Function\n"
                + "Function Use() As Object\nDim t = ReadBody(\"dbo.ReadItems\")\nReturn t.Rows(0)(\"Name\")\nEnd Function\n";
        var site = Assert.Single(Extract(language, members));
        Assert.Equal("field-read-origin-unbound", site.Field.Gap);
        Assert.Equal("helper:helper-body-unsupported", site.Field.OriginGaps[2]);
        Assert.Equal("datarow-field-extension-unsupported", site.Helper.Gap);
    }

    [Fact]
    public void Defining_assembly_identity_is_checked_and_a_lookalike_type_is_untrusted()
    {
        // .NET 10: the System.Data.DataSetExtensions reference forwards DataRowExtensions to System.Data.Common.
        var real = Assert.Single(Extract("cs", Method("cs", "return table.Rows[0].Field<string>(\"Name\");"))).Field;
        Assert.Equal("bounded", Assert.Single(real.Sites).Coverage);
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Probe", [], SqlServerHelperResultExtractionTests.References());
        var extensions = compilation.GetTypeByMetadataName("System.Data.DataRowExtensions")!;
        Assert.Equal("System.Data.Common", extensions.ContainingAssembly.Identity.Name);
        Assert.True(SqlServerDataTableConsumers.FieldAssembly(extensions.ContainingAssembly));
        Assert.False(SqlServerDataTableConsumers.FieldAssembly(compilation.Assembly));

        // A source-defined System.Data.DataRowExtensions, with the real System.Data.Common behind an extern alias.
        const string header = "extern alias real;\n#pragma warning disable CS0618, CS0219, CS0168, CS0436\nusing System; using System.Data; using System.Data.SqlClient;\n";
        const string lookalike = "namespace System.Data { static class DataRowExtensions { public static T Field<T>(this real::System.Data.DataRow row, string name) => default; } }\n";
        var body = "static object M() {\nvar command = new SqlCommand(\"dbo.ReadItems\");\ncommand.CommandType = real::System.Data.CommandType.StoredProcedure;\n"
            + "var adapter = new SqlDataAdapter(command);\nvar table = new real::System.Data.DataTable();\nadapter.Fill(table);\nreturn table.Rows[0].Field<string>(\"Name\");\n}\n";
        foreach (var name in new[] { "Synthetic", "System.Data.Common", "System.Data.DataSetExtensions" })
        {
            var site = Assert.Single(Extract("cs", body, lookalike, refs => refs.Select(r =>
                Path.GetFileName(((PortableExecutableReference)r).FilePath) == "System.Data.Common.dll" ? r.WithAliases(["real"]) : r), header, name));
            Assert.Equal("datatable:datarow-field-extension-untrusted", site.Field.OriginGaps[0]);
            Assert.Equal("datarow-field-extension-unsupported", site.DataTable.Gap);
        }
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Unregistered_non_field_compilation_retains_sibling_payload_and_existing_binding_state(string language)
    {
        var body = language == "cs" ? "return table.Rows[0][\"Name\"];" : "Return table.Rows(0)(\"Name\")";
        var site = Assert.Single(Extract(language, Method(language, body)));
        Assert.True(site.Properties.ContainsKey("sqlServerFieldReadState"));
        Assert.Equal("bounded", site.DataTable.Coverage);
        Assert.Equal("Name", Assert.Single(site.DataTable.Accesses).Name);
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Presence_omission_requires_current_complete_registered_error_free_scope(string language)
    {
        var ext = language == "cs" ? "cs" : "vb";
        foreach (var scope in new[] { "complete", "reduced", "errors", "unregistered" })
        foreach (var cachedAbsence in new[] { false, true })
        {
            if (scope == "unregistered" && cachedAbsence) continue;
            var helper = SqlServerInternalHelperResultExtractionTests.HelperDocument(language, language == "cs" ? "internal" : "Friend");
            if (scope == "errors") helper += language == "cs" ? "class Broken { MissingType value; }" : "Class Broken\nDim value As MissingType\nEnd Class\n";
            var caller = $"proj/Excluded.{ext}";
            var documents = new[] { ($"proj/Helpers.{ext}", helper),
                (caller, SqlServerInternalHelperResultExtractionTests.CallerDocument(language, "Callers",
                    scope == "reduced" ? FieldCall(language) : SqlServerInternalHelperResultExtractionTests.Call(language))) };
            var site = Assert.Single(SqlServerInternalHelperResultExtractionTests.Extract(language, documents,
                register: scope != "unregistered", exclude: scope == "reduced" ? ["**/Excluded.*"] : null,
                allowErrors: scope == "errors", beforeRegister: (compilation, project) =>
                {
                    var call = compilation.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes()
                        .Select(n => compilation.GetSemanticModel(tree).GetOperation(n))).OfType<IInvocationOperation>()
                        .First(o => o.TargetMethod.Name == "Fill");
                    if (cachedAbsence)
                    {
                        // Register a complete snapshot first, then let the harness replace it with
                        // the actual reduced/error scope. Only immutable syntax may be cached.
                        using var cleanWorkspace = new AdhocWorkspace();
                        var cleanProject = cleanWorkspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                            "Clean", "Clean", compilation.Language, filePath: project.FilePath));
                        var repo = Path.GetDirectoryName(Path.GetDirectoryName(project.FilePath!)!)!;
                        SqlServerCompilationScopes.Register(repo, cleanProject, compilation, hasErrors: false);
                    }
                    var properties = new SortedDictionary<string, string>();
                    SqlServerFieldReads.Add(properties, call);
                    Assert.Equal(!cachedAbsence, properties.ContainsKey("sqlServerFieldReadState"));
                }));
            Assert.Equal(scope != "complete", site.FieldStatePresent);
            if (scope != "complete")
                Assert.Contains("internal-helper:" + (scope switch
                {
                    "reduced" => "helper-compilation-reduced",
                    "errors" => "helper-compilation-errors",
                    _ => "helper-compilation-scope-unavailable"
                }), site.Field.OriginGaps);
        }
    }

    [Theory]
    [InlineData("int", "System.Int32")]
    [InlineData("int?", "System.Nullable[System.Int32]")]
    [InlineData("int[,]", "System.Int32[,]")]
    [InlineData("System.Collections.Generic.Dictionary<string, byte[]>", "System.Collections.Generic.Dictionary[System.String,System.Byte[]]")]
    [InlineData("(int, string)", "System.ValueTuple[System.Int32,System.String]")]
    [InlineData("Outer.Inner", "Outer.Inner")]
    [InlineData("string?", "System.String")]
    [InlineData("dynamic", null)]
    public void Type_arguments_render_to_a_bounded_language_neutral_vocabulary(string type, string? expected)
    {
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText($"#nullable enable\nclass Outer {{ public class Inner {{}} }}\nclass Probe {{ {type} Value = default!; }}\n");
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Probe", [tree], SqlServerHelperResultExtractionTests.References());
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("Probe")!.GetMembers("Value").Single();
        Assert.Equal(expected, SqlServerDataTableConsumers.TypeArgument(field.Type));
    }

    [Fact]
    public void Visual_basic_type_arguments_render_like_csharp()
    {
        var tree = Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxTree.ParseText("Class Probe\nPublic A As Integer?\nPublic B As Byte()\nEnd Class\n");
        var compilation = Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilation.Create("Probe", [tree], SqlServerHelperResultExtractionTests.References());
        var type = compilation.GetTypeByMetadataName("Probe")!;
        Assert.Equal("System.Nullable[System.Int32]", SqlServerDataTableConsumers.TypeArgument(((IFieldSymbol)type.GetMembers("A").Single()).Type));
        Assert.Equal("System.Byte[]", SqlServerDataTableConsumers.TypeArgument(((IFieldSymbol)type.GetMembers("B").Single()).Type));
    }

    // Internal-helper origin over the Phase 5 registered-scope harness, including the merged #918 repairs.
    private static string FieldCall(string language, string column = "Name")
        => language == "cs" ? $"var t = Data.ReadTable(\"dbo.ReadItems\"); return t.Rows[0].Field<string>(\"{column}\");"
            : $"Dim t = Data.ReadTable(\"dbo.ReadItems\")\nReturn t.Rows(0).Field(Of String)(\"{column}\")";

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Internal_helper_cross_document_field_reads_carry_caller_paths(string language)
    {
        var e = language == "cs" ? "cs" : "vb";
        var site = Assert.Single(SqlServerInternalHelperResultExtractionTests.Extract(language, [
            ($"proj/Helpers.{e}", SqlServerInternalHelperResultExtractionTests.HelperDocument(language, language == "cs" ? "internal" : "Friend")),
            ($"proj/Callers.{e}", SqlServerInternalHelperResultExtractionTests.CallerDocument(language, "Callers", FieldCall(language),
                SqlServerInternalHelperResultExtractionTests.Call(language)))]));
        Assert.Equal(("bounded", "internal-helper"), (site.Field.Coverage, site.Field.Origin));
        Assert.Equal([($"proj/Callers.{e}", "bounded", (string?)null), ($"proj/Callers.{e}", "unknown", "field-read-absent")],
            site.Field.Sites.Select(s => (s.Path!, s.Coverage, s.Gap)));
        Assert.Equal("System.String", Assert.Single(site.Field.Sites[0].Accesses).TypeArgument);
        Assert.Equal([("unknown", "datarow-field-extension-unsupported"), ("bounded", (string?)null)], site.Internal.Callers.Select(c => (c.Coverage, c.Gap)));
    }

    [Fact]
    public void Escaped_internal_helper_calls_stay_enumerated_for_field_reads()
    {
        var body = FieldCall("cs").Replace("ReadTable", @"\u0052eadTable", StringComparison.Ordinal);
        var site = Assert.Single(SqlServerInternalHelperResultExtractionTests.Extract("cs", [
            ("proj/Helpers.cs", SqlServerInternalHelperResultExtractionTests.HelperDocument("cs")),
            ("proj/Callers.cs", SqlServerInternalHelperResultExtractionTests.CallerDocument("cs", "Callers", body))]));
        Assert.Equal(("internal-helper", "proj/Callers.cs", "bounded"), (site.Field.Origin, Assert.Single(site.Field.Sites).Path, site.Field.Sites[0].Coverage));
    }

    [Theory]
    [InlineData("cs", "A")]
    [InlineData("vb", "Z")]
    public void Unavailable_inputs_and_missing_scope_keep_internal_field_reads_unknown(string language, string prefix)
    {
        var e = language == "cs" ? "cs" : "vb";
        var missing = $"proj/{prefix}Missing.{e}";
        (string, string)[] documents = [($"proj/Helpers.{e}", SqlServerInternalHelperResultExtractionTests.HelperDocument(language, language == "cs" ? "internal" : "Friend")),
            ($"proj/Callers.{e}", SqlServerInternalHelperResultExtractionTests.CallerDocument(language, "Callers", FieldCall(language))), (missing, "")];
        var reduced = Assert.Single(SqlServerInternalHelperResultExtractionTests.Extract(language, documents, unavailablePath: missing)).Field;
        Assert.Equal(("field-read-origin-unbound", "internal-helper:helper-compilation-reduced"), (reduced.Gap, reduced.OriginGaps[3]));
        var unregistered = Assert.Single(SqlServerInternalHelperResultExtractionTests.Extract(language, documents[..2], register: false)).Field;
        Assert.Equal("internal-helper:helper-compilation-scope-unavailable", unregistered.OriginGaps[3]);
    }
}
