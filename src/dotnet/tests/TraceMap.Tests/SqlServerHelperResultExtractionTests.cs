using System.Text.Json;
using TraceMap.Core;
namespace TraceMap.Tests;
/// <summary>Phase 4 helper-boundary source state (direct Roslyn). Expectations follow the design matrix authored before the extractor ran.</summary>
public sealed class SqlServerHelperResultExtractionTests
{
    private static string RepoRoot()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        return repo!.FullName;
    }
    internal static IEnumerable<Microsoft.CodeAnalysis.MetadataReference> References()
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "samples/sqlserver-parameter-evidence/csharp/obj/project.assets.json")));
        var package = assets.RootElement.GetProperty("libraries").GetProperty("System.Data.SqlClient/4.9.0").GetProperty("path").GetString()!;
        var compile = assets.RootElement.GetProperty("targets").GetProperty("net10.0").GetProperty("System.Data.SqlClient/4.9.0").GetProperty("compile")
            .EnumerateObject().Single(p => p.Name.EndsWith(".dll", StringComparison.Ordinal)).Name;
        var provider = assets.RootElement.GetProperty("packageFolders").EnumerateObject().Select(p => Path.Combine(p.Name, package, compile)).First(File.Exists);
        return ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(provider).Distinct(StringComparer.Ordinal).Select(p => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(p)).ToArray();
    }
    internal sealed record Site(SqlServerHelperResultState Helper, SqlServerDataTableState DataTable, SqlServerDataSetState DataSet);
    /// <summary>Compiles <paramref name="members"/> inside one type and returns every Fill site's three sibling states in source order.</summary>
    internal static Site[] Extract(string language, string members, string? extraTypes = null)
    {
        Microsoft.CodeAnalysis.Compilation compilation;
        Microsoft.CodeAnalysis.SyntaxTree tree;
        if (language == "cs")
        {
            tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("#pragma warning disable CS0618, CS0219, CS0168\nusing System; using System.Data; using System.Data.SqlClient;\n"
                + "static class Example {\nstatic void Consume(object o) {}\nstatic void Touch() {}\n" + members + "\n}\n" + extraTypes);
            compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Synthetic", [tree], References(), new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        }
        else
        {
            tree = Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxTree.ParseText("Imports System\nImports System.Data\nImports System.Data.SqlClient\nModule Example\nSub Consume(o As Object)\nEnd Sub\nSub Touch()\nEnd Sub\n"
                + members + "\nEnd Module\n" + extraTypes);
            compilation = Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilation.Create("Synthetic", [tree], References(), new Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary, optionInfer: true));
        }
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        var model = compilation.GetSemanticModel(tree);
        return tree.GetRoot().DescendantNodes().Select(n => model.GetOperation(n)).OfType<Microsoft.CodeAnalysis.Operations.IInvocationOperation>()
            .Where(o => o.TargetMethod.Name == "Fill").Distinct().OrderBy(o => o.Syntax.SpanStart)
            .Select(o =>
            {
                var p = new SortedDictionary<string, string>();
                SqlServerDataTableConsumers.Add(p, o);
                SqlServerDataSetConsumers.Add(p, o);
                SqlServerHelperResults.Add(p, o);
                return new Site(SqlServerSchemaEvidence.Read<SqlServerHelperResultState>(p["sqlServerHelperResultState"]),
                    SqlServerSchemaEvidence.Read<SqlServerDataTableState>(p["sqlServerDataTableState"]), SqlServerSchemaEvidence.Read<SqlServerDataSetState>(p["sqlServerDataSetState"]));
            }).ToArray();
    }

    internal const string CsTable = "private static DataTable ReadTable(string procedure) {\nvar command = new SqlCommand(procedure);\ncommand.CommandType = CommandType.StoredProcedure;\nvar adapter = new SqlDataAdapter(command);\nvar table = new DataTable();\nadapter.Fill(table);\nreturn table;\n}\n";
    internal const string VbTable = "Private Function ReadTable(procedure As String) As DataTable\nDim command = New SqlCommand(procedure)\ncommand.CommandType = CommandType.StoredProcedure\nDim adapter = New SqlDataAdapter(command)\nDim table = New DataTable()\nadapter.Fill(table)\nReturn table\nEnd Function\n";
    private static string Table(string language) => language == "cs" ? CsTable : VbTable;

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Multiple_callers_with_different_procedures_get_separate_bounded_entries(string language)
    {
        var callers = language == "cs"
            ? "static object First() { var items = ReadTable(\"dbo.ReadItems\"); return items.Rows[0][\"Name\"]; }\n"
                + "static object Second() { var other = ReadTable(\"dbo.ReadOther\"); var row = other.Rows[0]; return row[0]; }\n"
                + "static object Both() { var a = ReadTable(\"dbo.ReadItems\"); var b = ReadTable(\"dbo.ReadOther\"); Consume(a.Rows[0][\"Id\"]); return b.Rows[0][\"Title\"]; }\n"
            : "Function First() As Object\nDim items = ReadTable(\"dbo.ReadItems\")\nReturn items.Rows(0)(\"Name\")\nEnd Function\n"
                + "Function Second() As Object\nDim other = ReadTable(\"dbo.ReadOther\")\nDim row = other.Rows(0)\nReturn row(0)\nEnd Function\n"
                + "Function Both() As Object\nDim a = ReadTable(\"dbo.ReadItems\")\nDim b = ReadTable(\"dbo.ReadOther\")\nReturn CStr(a.Rows(0)(\"Id\")) & CStr(b.Rows(0)(\"Title\"))\nEnd Function\n";
        var site = Assert.Single(Extract(language, Table(language) + callers));
        var state = site.Helper;
        Assert.Equal(("bounded", (string?)null, "datatable", "StoredProcedure", "constructor-parameter", "constructor-command", "fill-datatable"),
            (state.Coverage, state.Gap, state.ResultKind, state.CommandType, state.CommandTextSource, state.AdapterAssociation, state.FillOverload));
        Assert.Null(state.TableName);
        Assert.Equal(4, state.Callers.Count);
        Assert.All(state.Callers, c => Assert.Equal(("bounded", (string?)null), (c.Coverage, c.Gap)));
        Assert.Equal([FactFactory.Hash("dbo.ReadItems", 64), FactFactory.Hash("dbo.ReadOther", 64), FactFactory.Hash("dbo.ReadItems", 64), FactFactory.Hash("dbo.ReadOther", 64)],
            state.Callers.Select(c => c.CommandTextHash));
        Assert.Equal(["First", "Second", "Both", "Both"], state.Callers.Select(c => c.Caller!.Split('(')[0].Split('.')[^1]));
        Assert.Equal("Name", Assert.Single(state.Callers[0].Accesses).Name);
        Assert.Equal((0, "row-local"), (Assert.Single(state.Callers[1].Accesses).Ordinal, state.Callers[1].Accesses[0].RowSource));
        Assert.Equal("Id", Assert.Single(state.Callers[2].Accesses).Name);
        Assert.Equal("Title", Assert.Single(state.Callers[3].Accesses).Name);
        Assert.True(state.Callers.Select(c => c.Line).SequenceEqual(state.Callers.Select(c => c.Line).Order()));
        // Phase 2/3 sibling states are unchanged: the returned table is still a DataTable escape and DataSet overloads refused.
        Assert.Equal(("unknown", "datatable-alias-or-escape"), (site.DataTable.Coverage, site.DataTable.Gap));
        Assert.Equal(("unknown", "fill-overload-unsupported"), (site.DataSet.Coverage, site.DataSet.Gap));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void DataSet_helper_binds_source_table_name_and_command_text_write_with_connection(string language)
    {
        var members = language == "cs"
            ? "private static DataSet ReadSet(SqlConnection connection, string procedure) {\nusing var command = new SqlCommand();\ncommand.CommandText = procedure;\ncommand.CommandType = CommandType.StoredProcedure;\ncommand.Connection = connection;\nusing var adapter = new SqlDataAdapter();\nadapter.SelectCommand = command;\nvar results = new DataSet();\nadapter.Fill(results, \"Items\");\nreturn results;\n}\n"
                + "static object Use(SqlConnection connection) { var set = ReadSet(connection, \"dbo.ReadItems\"); if (set.Tables.Count > 0) { var t = set.Tables[\"Items\"]; foreach (DataRow row in t.Rows) { Consume(row[\"Total\"]); } } return set.Tables[0].Rows[0][\"Name\"]; }\n"
            : "Private Function ReadSet(connection As SqlConnection, procedure As String) As DataSet\nUsing command As New SqlCommand()\ncommand.CommandText = procedure\ncommand.CommandType = CommandType.StoredProcedure\ncommand.Connection = connection\nUsing adapter As New SqlDataAdapter()\nadapter.SelectCommand = command\nDim results As New DataSet()\nadapter.Fill(results, \"Items\")\nReturn results\nEnd Using\nEnd Using\nEnd Function\n"
                + "Function Use(connection As SqlConnection) As Object\nDim [set] = ReadSet(connection, \"dbo.ReadItems\")\nIf [set].Tables.Count > 0 Then\nDim t = [set].Tables(\"Items\")\nFor Each row As DataRow In t.Rows\nConsume(row(\"Total\"))\nNext\nEnd If\nReturn [set].Tables!Items.Rows(0)!Name\nEnd Function\n";
        var site = Assert.Single(Extract(language, members));
        var state = site.Helper;
        Assert.Equal(("bounded", "dataset", "command-text-parameter", "select-command-property", "fill-dataset-source-table", "Items"),
            (state.Coverage, state.ResultKind, state.CommandTextSource, state.AdapterAssociation, state.FillOverload, state.TableName));
        var caller = Assert.Single(state.Callers);
        Assert.Equal(("bounded", FactFactory.Hash("dbo.ReadItems", 64)), (caller.Coverage, caller.CommandTextHash));
        Assert.Equal(language == "cs" ? "table-local,tables-index,tables-name" : "table-local,tables-name", string.Join(",", caller.Selectors));
        Assert.Equal("foreach,tables-count", string.Join(",", caller.Guards));
        Assert.Equal(["Total", "Name"], caller.Accesses.Select(a => a.Name));
        Assert.Equal(("unknown", "dataset-alias-or-escape"), (site.DataSet.Coverage, site.DataSet.Gap));
        Assert.Equal(("unknown", "fill-overload-unsupported"), (site.DataTable.Coverage, site.DataTable.Gap));
    }

    // Helper-level refusals: (language, helper members replacing ReadTable, one caller, expected gap).
    [Theory]
    [InlineData("cs", "internal static DataTable ReadTable(string procedure) {B}", "helper-accessibility-unsupported")]
    [InlineData("cs", "public static DataTable ReadTable(string procedure) {B}", "helper-accessibility-unsupported")]
    [InlineData("cs", "private static DataTable ReadTable<T>(string procedure) {B}", "helper-generic-unsupported")]
    [InlineData("cs", "private static DataTable ReadTable(this string procedure) {B}", "helper-extension-unsupported")]
    [InlineData("cs", "private static DataTable ReadTable(string procedure) {B} private static DataTable ReadTable(string procedure, int unused) {B}", "helper-overload-ambiguous")]
    [InlineData("cs", "private static DataTable ReadTable(ref string procedure) {B}", "helper-parameter-unsupported")]
    [InlineData("cs", "private static DataTable ReadTable(out string procedure) { procedure = null; {R}", "helper-parameter-unsupported")]
    [InlineData("cs", "private static DataTable ReadTable(string procedure = \"dbo.ReadItems\") {B}", "helper-parameter-unsupported")]
    [InlineData("cs", "private static DataTable ReadTable(string procedure, params int[] extra) {B}", "helper-parameter-unsupported")]
    [InlineData("cs", "private static DataTable ReadTable(string procedure, string other) {B}", "helper-parameter-unsupported")]
    [InlineData("cs", "private static DataTable ReadTable(string procedure, int timeout) {B}", "helper-parameter-unsupported")]
    [InlineData("cs", "private static object ReadTable(string procedure) {B}", "helper-return-type-unsupported")]
    [InlineData("cs", "private static readonly Func<string, DataTable> Reader = ReadTable; private static DataTable ReadTable(string procedure) {B}", "helper-reference-escape")]
    [InlineData("cs", "private static string Name = nameof(ReadTable); private static DataTable ReadTable(string procedure) {B}", "helper-reference-escape")]
    [InlineData("cs", "private static DataTable ReadTable(string procedure) { if (procedure.Length == 0) return ReadTable(\"dbo.ReadItems\"); {R}", "helper-recursion")]
    [InlineData("cs", "private static DataTable ReadTable(string procedure) { Func<DataTable> again = () => ReadTable(procedure); {R}", "helper-recursion")]
    [InlineData("cs", "private static DataTable ReadTable(string procedure) { Touch(); {R}", "helper-body-unsupported")]
    [InlineData("cs", "private static DataTable ReadTable(string procedure) { var text = procedure.Trim(); {R}", "helper-command-text-parameter-required")]
    [InlineData("cs", "private static DataTable ReadTable(string procedure) { procedure = \"dbo.\" + procedure; {R}", "helper-parameter-mutation")]
    [InlineData("vb", "Friend Function ReadTable(procedure As String) As DataTable\nB\nEnd Function", "helper-accessibility-unsupported")]
    [InlineData("vb", "Private Function ReadTable(Of T)(procedure As String) As DataTable\nB\nEnd Function", "helper-generic-unsupported")]
    [InlineData("vb", "<System.Runtime.CompilerServices.Extension> Private Function ReadTable(procedure As String) As DataTable\nB\nEnd Function", "helper-extension-unsupported")]
    [InlineData("vb", "Private Function ReadTable(ByRef procedure As String) As DataTable\nB\nEnd Function", "helper-parameter-unsupported")]
    [InlineData("vb", "Private Function ReadTable(Optional procedure As String = \"dbo.ReadItems\") As DataTable\nB\nEnd Function", "helper-parameter-unsupported")]
    [InlineData("vb", "Private Function ReadTable(procedure As String) As Object\nB\nEnd Function", "helper-return-type-unsupported")]
    [InlineData("vb", "Private ReadOnly Reader As Func(Of String, DataTable) = AddressOf ReadTable\nPrivate Function ReadTable(procedure As String) As DataTable\nB\nEnd Function", "helper-reference-escape")]
    [InlineData("vb", "Private Function ReadTable(procedure As String) As DataTable\nIf procedure.Length = 0 Then Return READTABLE(\"dbo.ReadItems\")\nB\nEnd Function", "helper-recursion")]
    [InlineData("vb", "Private Function ReadTable(procedure As String) As DataTable\nTouch()\nB\nEnd Function", "helper-body-unsupported")]
    public void Helper_symbol_signature_and_reference_cells_fail_closed(string language, string helper, string gap)
    {
        var body = language == "cs"
            ? "var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table;"
            : "Dim command = New SqlCommand(procedure)\ncommand.CommandType = CommandType.StoredProcedure\nDim adapter = New SqlDataAdapter(command)\nDim table = New DataTable()\nadapter.Fill(table)\nReturn table";
        // {B} is a whole C# body, {R} the rest of a body whose opening brace and first statements the cell supplies.
        var members = language == "cs" ? helper.Replace("{B}", "{ " + body + " }", StringComparison.Ordinal).Replace("{R}", body + " }", StringComparison.Ordinal)
            : helper.Replace("\nB\n", "\n" + body + "\n", StringComparison.Ordinal);
        var caller = language == "cs"
            ? gap == "helper-generic-unsupported" ? "static object Use() { var t = ReadTable<int>(\"dbo.ReadItems\"); return t.Rows[0][\"Name\"]; }"
                : gap == "helper-extension-unsupported" ? "static object Use() { var t = \"dbo.ReadItems\".ReadTable(); return t.Rows[0][\"Name\"]; }"
                : gap == "helper-parameter-unsupported" ? ""
                : gap == "helper-return-type-unsupported" ? "static object Use() { var t = ReadTable(\"dbo.ReadItems\"); return t; }"
                : "static object Use() { var t = ReadTable(\"dbo.ReadItems\"); return t.Rows[0][\"Name\"]; }"
            : gap == "helper-generic-unsupported" ? "Function Use() As Object\nDim t = ReadTable(Of Integer)(\"dbo.ReadItems\")\nReturn t.Rows(0)(\"Name\")\nEnd Function"
                : gap is "helper-parameter-unsupported" or "helper-return-type-unsupported" ? ""
                : "Function Use() As Object\nDim t = ReadTable(\"dbo.ReadItems\")\nReturn t.Rows(0)(\"Name\")\nEnd Function";
        var sites = Extract(language, members + "\n" + caller);
        Assert.NotEmpty(sites);
        Assert.All(sites, s => Assert.Equal(("unknown", gap), (s.Helper.Coverage, s.Helper.Gap)));
        Assert.All(sites, s => Assert.Empty(s.Helper.Callers));
    }

    [Theory]
    [InlineData("cs", "class Instances { private DataTable ReadTable(string procedure) {B} object Use() { var t = ReadTable(\"dbo.ReadItems\"); return t.Rows[0][\"Name\"]; } }", "helper-static-required")]
    [InlineData("cs", "class Virtuals { protected virtual DataTable ReadTable(string procedure) {B} }", "helper-virtual-or-interface-unsupported")]
    [InlineData("cs", "static partial class Parts { private static DataTable ReadTable(string procedure) {B} } static partial class Parts { static object Use() { var t = ReadTable(\"dbo.ReadItems\"); return t.Rows[0][\"Name\"]; } }", "helper-partial-type-unsupported")]
    [InlineData("cs", "class Outer<T> { private static DataTable ReadTable(string procedure) {B} }", "helper-generic-unsupported")]
    [InlineData("vb", "Class Instances\nPrivate Function ReadTable(procedure As String) As DataTable\nB\nEnd Function\nEnd Class", "helper-static-required")]
    [InlineData("vb", "Class Virtuals\nProtected Overridable Function ReadTable(procedure As String) As DataTable\nB\nEnd Function\nEnd Class", "helper-virtual-or-interface-unsupported")]
    [InlineData("vb", "Partial Class Parts\nPrivate Shared Function ReadTable(procedure As String) As DataTable\nB\nEnd Function\nEnd Class\nPartial Class Parts\nShared Function Use() As Object\nDim t = ReadTable(\"dbo.ReadItems\")\nReturn t.Rows(0)(\"Name\")\nEnd Function\nEnd Class", "helper-partial-type-unsupported")]
    public void Instance_virtual_partial_and_generic_type_helpers_fail_closed(string language, string type, string gap)
    {
        var body = language == "cs"
            ? "{ var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table; }"
            : "Dim command = New SqlCommand(procedure)\ncommand.CommandType = CommandType.StoredProcedure\nDim adapter = New SqlDataAdapter(command)\nDim table = New DataTable()\nadapter.Fill(table)\nReturn table";
        var site = Assert.Single(Extract(language, "", language == "cs" ? type.Replace("{B}", body, StringComparison.Ordinal) : type.Replace("\nB\n", "\n" + body + "\n", StringComparison.Ordinal)));
        Assert.Equal(("unknown", gap), (site.Helper.Coverage, site.Helper.Gap));
    }

    // Body refusals: the helper body replaces the bounded C# body; one bounded caller.
    [Theory]
    [InlineData("var command = new SqlCommand(\"dbo.ReadItems\"); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table;", "helper-command-text-parameter-required")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandText = procedure; command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table;", "helper-command-text-parameter-required")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); table.Rows.Clear(); return table;", "datatable-mutation")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); var peek = table.Rows[0][\"Name\"]; return table;", "helper-body-unsupported")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); if (procedure.Length > 0) { adapter.Fill(table); } return table;", "unsupported-control-flow")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; command.ExecuteNonQuery(); var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table;", "command-executed-outside-fill")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); adapter.Fill(table); return table;", "repeated-fill")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); if (procedure.Length == 0) return table; return table;", "helper-multiple-returns")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); using var table = new DataTable(); adapter.Fill(table); return table;", "helper-result-disposed")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(\"Items\"); adapter.Fill(table); return table;", "datatable-constructor-unsupported")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); adapter.MissingSchemaAction = MissingSchemaAction.Error; var table = new DataTable(); adapter.Fill(table); return table;", "adapter-missing-schema-action-unsupported")]
    [InlineData("var command = new SqlCommand(procedure); var adapter = new SqlDataAdapter(command); command.CommandType = CommandType.StoredProcedure; var table = new DataTable(); adapter.Fill(table); return table;", "command-mutation-after-adapter-association")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); var copy = table; return copy;", "helper-return-required")]
    [InlineData("var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); Func<int> fill = () => adapter.Fill(table); fill(); return table;", "helper-method-unsupported")]
    public void Helper_body_mutation_guards_second_execution_and_returns_fail_closed(string body, string gap)
    {
        var members = "private static DataTable ReadTable(string procedure) {\n" + body + "\n}\nstatic object Use() { var t = ReadTable(\"dbo.ReadItems\"); return t.Rows[0][\"Name\"]; }";
        var sites = Extract("cs", members);
        Assert.Contains(sites, s => s.Helper.Gap == gap);
        Assert.All(sites, s => Assert.Equal("unknown", s.Helper.Coverage));
    }

    [Theory]
    [InlineData("cs", "var extra = new SqlCommand(); extra.ExecuteNonQuery();")]
    [InlineData("cs", "var extra = new SqlCommand(); extra.ExecuteScalar();")]
    [InlineData("cs", "var extra = new SqlDataAdapter(); var other = new DataTable(); extra.Fill(other);")]
    [InlineData("cs", "var unused = new DataSet();")]
    [InlineData("vb", "Dim extra = New SqlCommand()\nextra.ExecuteNonQuery()")]
    [InlineData("vb", "Dim extra = New SqlCommand()\nextra.ExecuteScalar()")]
    [InlineData("vb", "Dim extra = New SqlDataAdapter()\nDim other = New DataTable()\nextra.Fill(other)")]
    [InlineData("vb", "Dim unused = New DataSet()")]
    public void Signed_provider_identity_does_not_admit_unrelated_helper_work(string language, string extra)
    {
        var source = Table(language).Replace(language == "cs" ? "return table;" : "Return table",
            extra + "\n" + (language == "cs" ? "return table;" : "Return table"), StringComparison.Ordinal);
        var sites = Extract(language, source);
        Assert.Equal("helper-body-unsupported", sites[0].Helper.Gap);
        Assert.All(sites, s => Assert.Equal("unknown", s.Helper.Coverage));
    }

    [Theory]
    [InlineData("cs", 64)]
    [InlineData("cs", 65)]
    [InlineData("vb", 64)]
    [InlineData("vb", 65)]
    public void Caller_limit_is_enforced_during_collection(string language, int count)
    {
        var callers = string.Concat(Enumerable.Range(0, count).Select(i => language == "cs"
            ? $"static object Use{i}() {{ var t = ReadTable(\"dbo.ReadItems\"); return t.Rows[0][\"Name\"]; }}\n"
            : $"Function Use{i}() As Object\nDim t = ReadTable(\"dbo.ReadItems\")\nReturn t.Rows(0)(\"Name\")\nEnd Function\n"));
        // A later escape must not require traversing the rest of the document after the cap is exceeded.
        if (count > 64) callers += language == "cs" ? "static string Escaped = nameof(ReadTable);"
            : "Private Escaped As String = NameOf(ReadTable)";
        var state = Assert.Single(Extract(language, Table(language) + callers)).Helper;
        Assert.Equal(count == 64 ? "bounded" : "unknown", state.Coverage);
        Assert.Equal(count == 64 ? null : "helper-caller-limit", state.Gap);
        Assert.Equal(count == 64 ? 64 : 0, state.Callers.Count);
    }

    // Caller refusals: each caller is its own entry; the helper stays bounded and other callers are unaffected.
    [Theory]
    [InlineData("cs", "var t = ReadTable(procedure: \"dbo.ReadItems\"); return t.Rows[0][\"Name\"];", "helper-named-argument-unsupported")]
    [InlineData("cs", "var t = ReadTable(name); return t.Rows[0][\"Name\"];", "helper-procedure-argument-nonconstant")]
    [InlineData("cs", "var t = ReadTable(null); return t.Rows[0][\"Name\"];", "helper-procedure-argument-invalid")]
    [InlineData("cs", "var t = ReadTable(\"\"); return t.Rows[0][\"Name\"];", "helper-procedure-argument-invalid")]
    [InlineData("cs", "ReadTable(\"dbo.ReadItems\"); return null;", "helper-result-local-required")]
    [InlineData("cs", "return ReadTable(\"dbo.ReadItems\").Rows[0][\"Name\"];", "helper-result-local-required")]
    [InlineData("cs", "object t = ReadTable(\"dbo.ReadItems\"); return t;", "helper-result-local-required")]
    [InlineData("cs", "var t = ReadTable(\"dbo.ReadItems\"); t.Rows[0][\"Name\"] = 1; return null;", "datatable-mutation")]
    [InlineData("cs", "var t = ReadTable(\"dbo.ReadItems\"); var copy = t; return copy.Rows[0][\"Name\"];", "datatable-alias-or-escape")]
    [InlineData("cs", "var t = ReadTable(\"dbo.ReadItems\"); t.Clear(); return null;", "datatable-mutation")]
    [InlineData("cs", "var t = ReadTable(\"dbo.ReadItems\"); return t.Columns.Count;", "datatable-schema-access-unsupported")]
    [InlineData("cs", "var t = ReadTable(\"dbo.ReadItems\"); try { return t.Rows[0][\"Name\"]; } catch (Exception) { return null; }", "unsupported-control-flow")]
    [InlineData("cs", "Func<DataTable> read = () => { var t = ReadTable(\"dbo.ReadItems\"); return t; }; return read();", "unsupported-control-flow")]
    [InlineData("cs", "var t = ReadTable(\"dbo.ReadItems\"); return t.Rows[index][\"Name\"];", "dynamic-row-index-unsupported")]
    [InlineData("vb", "Dim t = ReadTable(procedure:=\"dbo.ReadItems\")\nReturn t.Rows(0)(\"Name\")", "helper-named-argument-unsupported")]
    [InlineData("vb", "Dim t = ReadTable(name)\nReturn t.Rows(0)(\"Name\")", "helper-procedure-argument-nonconstant")]
    [InlineData("vb", "Dim t = ReadTable(Nothing)\nReturn t.Rows(0)(\"Name\")", "helper-procedure-argument-invalid")]
    [InlineData("vb", "Call ReadTable(\"dbo.ReadItems\")\nReturn Nothing", "helper-result-local-required")]
    [InlineData("vb", "Dim t As Object = ReadTable(\"dbo.ReadItems\")\nReturn t", "helper-result-local-required")]
    [InlineData("vb", "Dim t = ReadTable(\"dbo.ReadItems\")\nt = ReadTable(\"dbo.ReadOther\")\nReturn t.Rows(0)(\"Name\")", "datatable-reassignment")]
    [InlineData("vb", "Dim t = ReadTable(\"dbo.ReadItems\")\nTry\nReturn t.Rows(0)(\"Name\")\nCatch ex As Exception\nReturn Nothing\nEnd Try", "unsupported-control-flow")]
    public void Caller_argument_result_and_consumer_cells_fail_closed_per_call_site(string language, string body, string gap)
    {
        var bounded = language == "cs"
            ? "static object Good() { var g = ReadTable(\"dbo.ReadItems\"); return g.Rows[0][\"Name\"]; }\nstatic object Use(string name, int index) {\n" + body + "\n}"
            : "Function Good() As Object\nDim g = ReadTable(\"dbo.ReadItems\")\nReturn g.Rows(0)(\"Name\")\nEnd Function\nFunction Use(name As String, index As Integer) As Object\n" + body + "\nEnd Function";
        var state = Assert.Single(Extract(language, Table(language) + bounded)).Helper;
        Assert.Equal(("bounded", (string?)null), (state.Coverage, state.Gap));
        Assert.Equal(("bounded", "Name"), (state.Callers[0].Coverage, Assert.Single(state.Callers[0].Accesses).Name));
        var refused = state.Callers.Skip(1).ToArray();
        Assert.NotEmpty(refused);
        Assert.Equal(gap, refused[0].Gap);
        Assert.All(refused.Where(c => c.Coverage == "unknown"), c => Assert.Empty(c.Accesses));
        Assert.All(refused.Where(c => c.Coverage == "unknown"), c => Assert.Null(c.CommandTextHash));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Reassigned_caller_splits_into_reassignment_and_result_local_entries_and_connection_argument_is_bounded(string language)
    {
        var members = language == "cs"
            ? "private static DataTable ReadTable(SqlConnection connection, string procedure) {\nusing var command = new SqlCommand(procedure, connection);\ncommand.CommandType = CommandType.StoredProcedure;\nusing var adapter = new SqlDataAdapter(command);\nvar table = new DataTable();\nadapter.Fill(table);\nreturn table;\n}\n"
                + "static object Use(SqlConnection c) { var t = ReadTable(c, \"dbo.ReadItems\"); t = ReadTable(c, \"dbo.ReadOther\"); var u = ReadTable(new SqlConnection(), \"dbo.ReadItems\"); var v = ReadTable(c, \"dbo.ReadOther\"); return v.Rows[0][\"Title\"]; }"
            : "Private Function ReadTable(connection As SqlConnection, procedure As String) As DataTable\nUsing command As New SqlCommand(procedure, connection)\ncommand.CommandType = CommandType.StoredProcedure\nUsing adapter As New SqlDataAdapter(command)\nDim table As New DataTable()\nadapter.Fill(table)\nReturn table\nEnd Using\nEnd Using\nEnd Function\n"
                + "Function Use(c As SqlConnection) As Object\nDim t = ReadTable(c, \"dbo.ReadItems\")\nt = ReadTable(c, \"dbo.ReadOther\")\nDim u = ReadTable(New SqlConnection(), \"dbo.ReadItems\")\nDim v = ReadTable(c, \"dbo.ReadOther\")\nReturn v.Rows(0)(\"Title\")\nEnd Function";
        var state = Assert.Single(Extract(language, members)).Helper;
        Assert.Equal(("bounded", "constructor-parameter"), (state.Coverage, state.CommandTextSource));
        Assert.Equal(["datatable-reassignment", "helper-result-local-required", "helper-connection-argument-unsupported", null], state.Callers.Select(c => c.Gap));
        Assert.Equal(FactFactory.Hash("dbo.ReadOther", 64), state.Callers[3].CommandTextHash);
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Inline_fill_keeps_bounded_phase_2_state_and_has_no_helper_state(string language)
    {
        var members = language == "cs"
            ? "static object Inline() { var command = new SqlCommand(\"dbo.ReadItems\"); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table.Rows[0][\"Name\"]; }"
            : "Function Inline() As Object\nDim command = New SqlCommand(\"dbo.ReadItems\")\ncommand.CommandType = CommandType.StoredProcedure\nDim adapter = New SqlDataAdapter(command)\nDim table = New DataTable()\nadapter.Fill(table)\nReturn table.Rows(0)(\"Name\")\nEnd Function";
        var site = Assert.Single(Extract(language, members));
        Assert.Equal(("unknown", "helper-return-required"), (site.Helper.Coverage, site.Helper.Gap));
        Assert.Equal(("bounded", (string?)null), (site.DataTable.Coverage, site.DataTable.Gap));
    }

    [Fact]
    public void Caller_limit_and_uncalled_helpers_are_explicit()
    {
        var many = string.Concat(Enumerable.Range(0, 65).Select(i => $"static object Use{i}() {{ var t = ReadTable(\"dbo.ReadItems\"); return t.Rows[0][\"Name\"]; }}\n"));
        Assert.Equal("helper-caller-limit", Assert.Single(Extract("cs", CsTable + many)).Helper.Gap);
        var uncalled = Assert.Single(Extract("cs", CsTable)).Helper;
        Assert.Equal(("bounded", 0), (uncalled.Coverage, uncalled.Callers.Count));
        var text = Assert.Single(Extract("cs", CsTable.Replace("CommandType.StoredProcedure", "CommandType.Text", StringComparison.Ordinal))).Helper;
        Assert.Equal(("bounded", "Text"), (text.Coverage, text.CommandType));
    }
}
