using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TraceMap.Core;
namespace TraceMap.Tests;
/// <summary>
/// Phase 5 internal-helper source state over a multi-document AdhocWorkspace project (direct Roslyn, registered compilation scope).
/// Expectations follow the design matrix authored before the extractor ran.
/// </summary>
public sealed class SqlServerInternalHelperResultExtractionTests
{
    internal sealed record Site(string Path, SqlServerInternalHelperResultState Internal, SqlServerHelperResultState Helper);

    private const string CsUsings = "#pragma warning disable CS0618, CS0219, CS0168\nusing System; using System.Data; using System.Data.SqlClient;\n";
    private const string VbImports = "Imports System\nImports System.Data\nImports System.Data.SqlClient\n";
    internal const string CsBody = "{ var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table; }";
    internal const string VbBody = "Dim command = New SqlCommand(procedure)\ncommand.CommandType = CommandType.StoredProcedure\nDim adapter = New SqlDataAdapter(command)\nDim table = New DataTable()\nadapter.Fill(table)\nReturn table\n";

    /// <summary>A helper type <c>Data</c> with one helper named ReadTable carrying <paramref name="modifiers"/>.</summary>
    internal static string HelperDocument(string language, string modifiers = "internal", string extra = "")
        => language == "cs" ? CsUsings + $"internal static class Data {{\n{modifiers} static DataTable ReadTable(string procedure) {CsBody}\n{extra}\n}}\n"
            : VbImports + $"Friend Module Data\n{modifiers} Function ReadTable(procedure As String) As DataTable\n{VbBody}End Function\n{extra}\nEnd Module\n";

    /// <summary>A caller type with one method per body; each body is a complete method body.</summary>
    internal static string CallerDocument(string language, string type, params string[] bodies)
        => language == "cs" ? CsUsings + $"internal static class {type} {{\n" + string.Concat(bodies.Select((b, i) => $"static object Use{i}(string name) {{ {b} }}\n")) + "}\n"
            : VbImports + $"Friend Module {type}\n" + string.Concat(bodies.Select((b, i) => $"Function Use{i}(name As String) As Object\n{b}\nEnd Function\n")) + "End Module\n";

    internal static string Call(string language, string procedure = "dbo.ReadItems", string column = "Name")
        => language == "cs" ? $"var t = Data.ReadTable(\"{procedure}\"); return t.Rows[0][\"{column}\"];" : $"Dim t = Data.ReadTable(\"{procedure}\")\nReturn t.Rows(0)(\"{column}\")";

    /// <summary>
    /// Builds a project <c>proj/P.*proj</c> under a temporary repository root, optionally excludes documents through the scanner's
    /// own exclusion path, registers the compilation scope, and returns both helper states of every Fill site in (path, position) order.
    /// </summary>
    internal static Site[] Extract(string language, (string Path, string Text)[] documents, bool register = true, string[]? exclude = null,
        bool allowErrors = false, Func<Compilation, Compilation>? mutate = null)
    {
        var repo = Directory.CreateTempSubdirectory("tracemap-internal-helper-").FullName;
        try
        {
            using var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var cs = language == "cs";
            CompilationOptions options = cs ? new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                : new Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionInfer: true);
            var solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "P", "P",
                cs ? LanguageNames.CSharp : LanguageNames.VisualBasic, filePath: Path.Combine(repo, "proj", cs ? "P.csproj" : "P.vbproj"),
                compilationOptions: options, metadataReferences: SqlServerHelperResultExtractionTests.References()));
            foreach (var (path, text) in documents)
                solution = solution.AddDocument(DocumentId.CreateNewId(projectId), Path.GetFileName(path), SourceText.From(text), filePath: Path.Combine(repo, path));
            if (exclude is not null) solution = CSharpSemanticExtractor.RemoveExplicitlyExcludedSourceDocuments(repo, solution, exclude, out _);
            var project = solution.GetProject(projectId)!;
            var compilation = project.GetCompilationAsync().GetAwaiter().GetResult()!;
            if (mutate is not null) compilation = mutate(compilation);
            if (!allowErrors) Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
            if (register) SqlServerCompilationScopes.Register(repo, project, compilation, compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error));
            return compilation.SyntaxTrees.SelectMany(tree =>
            {
                var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
                return tree.GetRoot().DescendantNodes().Select(n => model.GetOperation(n)).OfType<Microsoft.CodeAnalysis.Operations.IInvocationOperation>()
                    .Where(o => o.TargetMethod.Name == "Fill").Distinct()
                    .Select(o => (Path: Path.GetRelativePath(repo, tree.FilePath).Replace('\\', '/'), o.Syntax.SpanStart,
                        Site: new Site(Path.GetRelativePath(repo, tree.FilePath).Replace('\\', '/'), SqlServerInternalHelperResults.Extract(o), SqlServerHelperResults.Extract(o))));
            }).OrderBy(s => s.Path, StringComparer.Ordinal).ThenBy(s => s.SpanStart).Select(s => s.Site).ToArray();
        }
        finally
        {
            Directory.Delete(repo, recursive: true);
        }
    }

    private static string Ext(string language) => language == "cs" ? "cs" : "vb";

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Callers_in_other_documents_are_bounded_with_repo_relative_paths_in_path_order(string language)
    {
        var e = Ext(language);
        var site = Assert.Single(Extract(language, [
            ($"proj/Helpers.{e}", HelperDocument(language, language == "cs" ? "internal" : "Friend",
                language == "cs" ? "static object Same() { var s = ReadTable(\"dbo.ReadItems\"); return s.Rows[0][\"Id\"]; }"
                    : "Function Same() As Object\nDim s = ReadTable(\"dbo.ReadItems\")\nReturn s.Rows(0)(\"Id\")\nEnd Function")),
            ($"proj/Sub/More.{e}", CallerDocument(language, "More", Call(language, column: "Total"))),
            ($"proj/Callers.{e}", CallerDocument(language, "Callers", Call(language), Call(language, "dbo.ReadOther", "Title"))),
        ]));
        var state = site.Internal;
        Assert.Equal(("sqlserver-internal-helper-result/1", "bounded", (string?)null, "datatable", "StoredProcedure", "constructor-parameter", "fill-datatable"),
            (state.Version, state.Coverage, state.Gap, state.ResultKind, state.CommandType, state.CommandTextSource, state.FillOverload));
        Assert.Equal([$"proj/Callers.{e}", $"proj/Callers.{e}", $"proj/Helpers.{e}", $"proj/Sub/More.{e}"], state.Callers.Select(c => c.Path));
        Assert.All(state.Callers, c => Assert.Equal(("bounded", (string?)null), (c.Coverage, c.Gap)));
        Assert.All(state.Callers, c => Assert.False(Path.IsPathRooted(c.Path)));
        Assert.Equal([FactFactory.Hash("dbo.ReadItems", 64), FactFactory.Hash("dbo.ReadOther", 64), FactFactory.Hash("dbo.ReadItems", 64), FactFactory.Hash("dbo.ReadItems", 64)],
            state.Callers.Select(c => c.CommandTextHash));
        Assert.Equal(["Name", "Title", "Id", "Total"], state.Callers.Select(c => Assert.Single(c.Accesses).Name));
        Assert.Equal(["Use0", "Use1", "Same", "Use0"], state.Callers.Select(c => c.Caller!.Split('(')[0].Split('.')[^1]));
        // Phase 4 is unchanged: an internal helper stays outside the private same-document boundary.
        Assert.Equal(("unknown", "helper-accessibility-unsupported", 0), (site.Helper.Coverage, site.Helper.Gap, site.Helper.Callers.Count));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void DataSet_helper_callers_in_other_documents_keep_Phase_3_selectors_and_guards(string language)
    {
        var e = Ext(language);
        var helper = language == "cs"
            ? CsUsings + "internal static class Data {\ninternal static DataSet ReadSet(string procedure) { var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure; var adapter = new SqlDataAdapter(command); var results = new DataSet(); adapter.Fill(results); return results; }\n}\n"
            : VbImports + "Friend Module Data\nFriend Function ReadSet(procedure As String) As DataSet\nDim command = New SqlCommand(procedure)\ncommand.CommandType = CommandType.StoredProcedure\nDim adapter = New SqlDataAdapter(command)\nDim results = New DataSet()\nadapter.Fill(results)\nReturn results\nEnd Function\nEnd Module\n";
        var caller = language == "cs"
            ? "var set = Data.ReadSet(\"dbo.ReadItems\"); if (set.Tables.Count > 0) { var t = set.Tables[0]; foreach (DataRow row in t.Rows) { Consume(row[\"Name\"]); } } return set.Tables[1].Rows.Count;"
            : "Dim [set] = Data.ReadSet(\"dbo.ReadItems\")\nIf [set].Tables.Count > 0 Then\nDim t = [set].Tables(0)\nFor Each row As DataRow In t.Rows\nConsume(row(\"Name\"))\nNext\nEnd If\nReturn Nothing";
        var good = language == "cs" ? caller.Replace("return set.Tables[1].Rows.Count;", "return null;", StringComparison.Ordinal) : caller;
        var later = language == "cs" ? caller : "Dim [set] = Data.ReadSet(\"dbo.ReadItems\")\nReturn [set].Tables(1).Rows(0)(\"Value\")";
        var consume = language == "cs" ? "static void Consume(object o) { }" : "Sub Consume(o As Object)\nEnd Sub";
        var callers = CallerDocument(language, "Callers", good, later).Replace(language == "cs" ? "internal static class Callers {\n" : "Friend Module Callers\n",
            (language == "cs" ? "internal static class Callers {\n" : "Friend Module Callers\n") + consume + "\n", StringComparison.Ordinal);
        var state = Assert.Single(Extract(language, [($"proj/Helpers.{e}", helper), ($"proj/Callers.{e}", callers)])).Internal;
        Assert.Equal(("bounded", "dataset", "Table"), (state.Coverage, state.ResultKind, state.TableName));
        Assert.Equal(("bounded", "table-local,tables-index", "foreach,tables-count"),
            (state.Callers[0].Coverage, string.Join(",", state.Callers[0].Selectors), string.Join(",", state.Callers[0].Guards)));
        Assert.Equal(("unknown", "dataset-later-table-unsupported"), (state.Callers[1].Coverage, state.Callers[1].Gap));
    }

    [Theory]
    [InlineData("cs", "private", "helper-private-phase4-scope", "bounded")]
    [InlineData("cs", "public", "helper-accessibility-unsupported", "unknown")]
    [InlineData("vb", "Private", "helper-private-phase4-scope", "bounded")]
    [InlineData("vb", "Public", "helper-accessibility-unsupported", "unknown")]
    public void Private_and_public_helpers_are_not_internal_evidence(string language, string modifiers, string gap, string phase4)
    {
        var e = Ext(language);
        var same = language == "cs" ? "static object Same() { var s = ReadTable(\"dbo.ReadItems\"); return s.Rows[0][\"Id\"]; }"
            : "Function Same() As Object\nDim s = ReadTable(\"dbo.ReadItems\")\nReturn s.Rows(0)(\"Id\")\nEnd Function";
        var site = Assert.Single(Extract(language, [($"proj/Helpers.{e}", HelperDocument(language, modifiers, same))]));
        Assert.Equal(("unknown", gap), (site.Internal.Coverage, site.Internal.Gap));
        Assert.Empty(site.Internal.Callers);
        Assert.Equal(phase4, site.Helper.Coverage);
    }

    [Theory]
    [InlineData("cs", "protected internal")]
    [InlineData("cs", "private protected")]
    [InlineData("vb", "Protected Friend")]
    [InlineData("vb", "Private Protected")]
    public void Protected_internal_and_private_protected_helpers_fail_closed(string language, string modifiers)
    {
        var e = Ext(language);
        var type = language == "cs"
            ? CsUsings + $"internal class Bases {{\n{modifiers} static DataTable ReadTable(string procedure) {CsBody}\nstatic object Same() {{ var s = ReadTable(\"dbo.ReadItems\"); return s.Rows[0][\"Id\"]; }}\n}}\n"
            : VbImports + $"Friend Class Bases\n{modifiers} Shared Function ReadTable(procedure As String) As DataTable\n{VbBody}End Function\nShared Function Same() As Object\nDim s = ReadTable(\"dbo.ReadItems\")\nReturn s.Rows(0)(\"Id\")\nEnd Function\nEnd Class\n";
        var site = Assert.Single(Extract(language, [($"proj/Bases.{e}", type)]));
        Assert.Equal(("unknown", "helper-accessibility-unsupported"), (site.Internal.Coverage, site.Internal.Gap));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void InternalsVisibleTo_makes_the_helper_unknown(string language)
    {
        var e = Ext(language);
        var attribute = language == "cs" ? "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"Other\")]\n"
            : "<Assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"Other\")>\n";
        var site = Assert.Single(Extract(language, [($"proj/Helpers.{e}", HelperDocument(language, language == "cs" ? "internal" : "Friend")),
            ($"proj/Callers.{e}", CallerDocument(language, "Callers", Call(language))), ($"proj/Properties.{e}", attribute)]));
        Assert.Equal(("unknown", "helper-internals-visible-to-unsupported"), (site.Internal.Coverage, site.Internal.Gap));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Unregistered_reduced_and_erroneous_compilations_fail_closed(string language)
    {
        var e = Ext(language);
        var modifiers = language == "cs" ? "internal" : "Friend";
        (string, string)[] documents = [($"proj/Helpers.{e}", HelperDocument(language, modifiers)), ($"proj/Callers.{e}", CallerDocument(language, "Callers", Call(language))),
            ($"proj/Excluded.{e}", CallerDocument(language, "Excluded", Call(language)))];
        var unregistered = Assert.Single(Extract(language, documents, register: false));
        Assert.Equal("helper-compilation-scope-unavailable", unregistered.Internal.Gap);
        // The scanner's own exclusion path marks the compilation reduced: an excluded caller must not vanish silently.
        Assert.Equal("helper-compilation-reduced", Assert.Single(Extract(language, documents, exclude: [$"proj/Excluded.{e}"])).Internal.Gap);
        Assert.Equal("bounded", Assert.Single(Extract(language, documents, exclude: ["proj/Absent.cs"])).Internal.Coverage);
        var broken = language == "cs" ? "internal static class Broken { static int M() => Missing(); }" : "Friend Module Broken\nFunction M() As Integer\nReturn Missing()\nEnd Function\nEnd Module";
        Assert.Equal("helper-compilation-errors", Assert.Single(Extract(language, [.. documents, ($"proj/Broken.{e}", broken)], allowErrors: true)).Internal.Gap);
    }

    [Theory]
    [InlineData("cs", "proj/Gen.g.cs", false, "helper-generated-document-unsupported")]
    [InlineData("cs", "proj/Header.cs", true, "helper-generated-document-unsupported")]
    [InlineData("cs", "proj/Form.Designer.cs", false, "helper-generated-document-unsupported")]
    [InlineData("cs", "shared/Linked.cs", false, "helper-linked-document-unsupported")]
    [InlineData("cs", "proj/My Callers.cs", false, "helper-document-path-unsupported")]
    [InlineData("vb", "proj/Gen.g.vb", false, "helper-generated-document-unsupported")]
    [InlineData("vb", "proj/Header.vb", true, "helper-generated-document-unsupported")]
    [InlineData("vb", "shared/Linked.vb", false, "helper-linked-document-unsupported")]
    [InlineData("vb", "proj/My Callers.vb", false, "helper-document-path-unsupported")]
    public void A_caller_in_a_generated_linked_or_unsafe_document_makes_the_helper_unknown(string language, string path, bool header, string gap)
    {
        var e = Ext(language);
        var marker = header ? (language == "cs" ? "// <auto-generated/>\n" : "' <auto-generated/>\n") : "";
        var site = Assert.Single(Extract(language, [($"proj/Helpers.{e}", HelperDocument(language, language == "cs" ? "internal" : "Friend")),
            ($"proj/Callers.{e}", CallerDocument(language, "Callers", Call(language))), (path, marker + CallerDocument(language, "Other", Call(language)))]));
        Assert.Equal(("unknown", gap), (site.Internal.Coverage, site.Internal.Gap));
        Assert.Empty(site.Internal.Callers);
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void External_caller_documents_and_helpers_in_linked_documents_fail_closed(string language)
    {
        var e = Ext(language);
        var modifiers = language == "cs" ? "internal" : "Friend";
        var outside = Path.Combine(Path.GetTempPath(), "tracemap-outside-" + Guid.NewGuid().ToString("N"), $"Outside.{e}");
        Assert.Equal("helper-linked-document-unsupported", Assert.Single(Extract(language, [($"proj/Helpers.{e}", HelperDocument(language, modifiers)),
            (outside, CallerDocument(language, "Outside", Call(language)))])).Internal.Gap);
        var linked = Assert.Single(Extract(language, [($"shared/Helpers.{e}", HelperDocument(language, modifiers)), ($"proj/Callers.{e}", CallerDocument(language, "Callers", Call(language)))]));
        Assert.Equal(("unknown", "helper-linked-document-unsupported"), (linked.Internal.Coverage, linked.Internal.Gap));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Trees_that_are_not_project_documents_are_generated(string language)
    {
        var e = Ext(language);
        var generated = language == "cs"
            ? Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(CallerDocument(language, "Generator", Call(language)), path: "/generator/Out.cs")
            : Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxTree.ParseText(CallerDocument(language, "Generator", Call(language)), path: "/generator/Out.vb");
        var site = Assert.Single(Extract(language, [($"proj/Helpers.{e}", HelperDocument(language, language == "cs" ? "internal" : "Friend"))],
            mutate: c => c.AddSyntaxTrees(generated)));
        Assert.Equal("helper-generated-document-unsupported", site.Internal.Gap);
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Method_group_and_nameof_references_in_other_documents_escape(string language)
    {
        var e = Ext(language);
        var modifiers = language == "cs" ? "internal" : "Friend";
        var escapes = language == "cs"
            ? new[] { CsUsings + "internal static class Escapes { internal static readonly Func<string, DataTable> Reader = Data.ReadTable; }",
                CsUsings + "internal static class Escapes { internal static readonly string Name = nameof(Data.ReadTable); }" }
            : [VbImports + "Friend Module Escapes\nFriend ReadOnly Reader As Func(Of String, DataTable) = AddressOf Data.ReadTable\nEnd Module",
                VbImports + "Friend Module Escapes\nFriend ReadOnly Name As String = NameOf(Data.ReadTable)\nEnd Module"];
        foreach (var escape in escapes)
        {
            var site = Assert.Single(Extract(language, [($"proj/Helpers.{e}", HelperDocument(language, modifiers)),
                ($"proj/Callers.{e}", CallerDocument(language, "Callers", Call(language))), ($"proj/Escapes.{e}", escape)]));
            Assert.Equal(("unknown", "helper-reference-escape"), (site.Internal.Coverage, site.Internal.Gap));
        }
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Partial_types_across_documents_fail_closed(string language)
    {
        var e = Ext(language);
        var (a, b) = language == "cs"
            ? (CsUsings + $"internal static partial class Split {{\ninternal static DataTable ReadTable(string procedure) {CsBody}\n}}\n",
                CsUsings + "internal static partial class Split {\nstatic object Use() { var t = ReadTable(\"dbo.ReadItems\"); return t.Rows[0][\"Name\"]; }\n}\n")
            : (VbImports + $"Partial Friend NotInheritable Class Split\nFriend Shared Function ReadTable(procedure As String) As DataTable\n{VbBody}End Function\nEnd Class\n",
                VbImports + "Partial Friend NotInheritable Class Split\nShared Function Use() As Object\nDim t = ReadTable(\"dbo.ReadItems\")\nReturn t.Rows(0)(\"Name\")\nEnd Function\nEnd Class\n");
        var site = Assert.Single(Extract(language, [($"proj/SplitA.{e}", a), ($"proj/SplitB.{e}", b)]));
        Assert.Equal(("unknown", "helper-partial-type-unsupported"), (site.Internal.Coverage, site.Internal.Gap));
    }

    [Theory]
    [InlineData("cs", 64)]
    [InlineData("cs", 65)]
    [InlineData("vb", 64)]
    [InlineData("vb", 65)]
    public void Caller_limit_spans_documents(string language, int count)
    {
        var e = Ext(language);
        var documents = Enumerable.Range(0, count).Select(i => ($"proj/Callers{i:00}.{e}", CallerDocument(language, $"Callers{i:00}", Call(language)))).ToList();
        documents.Insert(0, ($"proj/Helpers.{e}", HelperDocument(language, language == "cs" ? "internal" : "Friend")));
        var state = Assert.Single(Extract(language, [.. documents])).Internal;
        Assert.Equal((count == 64 ? "bounded" : "unknown", count == 64 ? null : "helper-caller-limit", count == 64 ? 64 : 0), (state.Coverage, state.Gap, state.Callers.Count));
    }

    [Fact]
    public void Compilation_tree_and_character_bounds_are_enforced_before_any_reference_scan()
    {
        var helper = ("proj/Helpers.cs", HelperDocument("cs"));
        var caller = ("proj/Callers.cs", CallerDocument("cs", "Callers", Call("cs")));
        var trees = Enumerable.Range(0, SqlServerCompilationScope.MaxTrees - 1).Select(i => ($"proj/T{i:0000}.cs", $"internal static class T{i:0000} {{ }}\n")).ToArray();
        Assert.Equal("helper-compilation-limit", Assert.Single(Extract("cs", [helper, caller, .. trees])).Internal.Gap);
        Assert.Equal("bounded", Assert.Single(Extract("cs", [helper, caller, .. trees[..^1]])).Internal.Coverage);
        var large = ("proj/Large.cs", "// " + new string('x', SqlServerCompilationScope.MaxCharacters) + "\ninternal static class Large { }\n");
        Assert.Equal("helper-compilation-limit", Assert.Single(Extract("cs", [helper, caller, large])).Internal.Gap);
    }

    [Theory]
    [InlineData("cs", "var t = Data.ReadTable(procedure: \"dbo.ReadItems\"); return t.Rows[0][\"Name\"];", "helper-named-argument-unsupported")]
    [InlineData("cs", "var t = Data.ReadTable(name); return t.Rows[0][\"Name\"];", "helper-procedure-argument-nonconstant")]
    [InlineData("cs", "Data.ReadTable(\"dbo.ReadItems\"); return null;", "helper-result-local-required")]
    [InlineData("cs", "var t = Data.ReadTable(\"dbo.ReadItems\"); var copy = t; return copy.Rows[0][\"Name\"];", "datatable-alias-or-escape")]
    [InlineData("cs", "var t = Data.ReadTable(\"dbo.ReadItems\"); t.Rows[0][\"Name\"] = 1; return null;", "datatable-mutation")]
    [InlineData("vb", "Dim t = Data.ReadTable(procedure:=\"dbo.ReadItems\")\nReturn t.Rows(0)(\"Name\")", "helper-named-argument-unsupported")]
    [InlineData("vb", "Dim t = Data.ReadTable(name)\nReturn t.Rows(0)(\"Name\")", "helper-procedure-argument-nonconstant")]
    [InlineData("vb", "Try\nDim t = Data.ReadTable(\"dbo.ReadItems\")\nReturn t.Rows(0)(\"Name\")\nCatch ex As Exception\nReturn Nothing\nEnd Try", "unsupported-control-flow")]
    public void Phase_4_caller_rules_apply_per_call_site_in_other_documents(string language, string body, string gap)
    {
        var e = Ext(language);
        var state = Assert.Single(Extract(language, [($"proj/Helpers.{e}", HelperDocument(language, language == "cs" ? "internal" : "Friend")),
            ($"proj/Callers.{e}", CallerDocument(language, "Callers", Call(language), body))])).Internal;
        Assert.Equal("bounded", state.Coverage);
        Assert.Equal([("bounded", (string?)null), ("unknown", gap)], state.Callers.Select(c => (c.Coverage, c.Gap)));
        Assert.Equal($"proj/Callers.{e}", state.Callers[1].Path);
    }

    [Theory]
    [InlineData("cs", "internal static DataTable ReadTable(string procedure) { Touch(); {R}", "helper-body-unsupported")]
    [InlineData("cs", "internal static DataTable ReadTable(string procedure) { if (procedure.Length == 0) return ReadTable(\"dbo.ReadItems\"); {R}", "helper-recursion")]
    [InlineData("cs", "internal DataTable ReadTable(string procedure) {B}", "helper-static-required")]
    [InlineData("cs", "internal static DataTable ReadTable(string procedure) {B} internal static DataTable ReadTable(string procedure, int unused) {B}", "helper-overload-ambiguous")]
    [InlineData("cs", "internal static DataTable ReadTable(string procedure, string other) {B}", "helper-parameter-unsupported")]
    public void Phase_4_helper_rules_are_unchanged_for_internal_helpers(string language, string helper, string gap)
    {
        var body = CsBody.Trim('{', '}', ' ');
        var members = helper.Replace("{B}", CsBody, StringComparison.Ordinal).Replace("{R}", body + " }", StringComparison.Ordinal);
        var type = CsUsings + (gap == "helper-static-required" ? "internal sealed class Data {\n" : "internal static class Data {\nstatic void Touch() { }\n") + members + "\n}\n";
        var caller = gap == "helper-static-required" ? CallerDocument(language, "Callers", "var t = new Data().ReadTable(\"dbo.ReadItems\"); return t.Rows[0][\"Name\"];")
            : gap == "helper-parameter-unsupported" ? CallerDocument(language, "Callers", "return null;") : CallerDocument(language, "Callers", Call(language));
        var sites = Extract(language, [("proj/Helpers.cs", type), ("proj/Callers.cs", caller)]);
        Assert.NotEmpty(sites);
        Assert.All(sites, s => Assert.Equal(("unknown", gap), (s.Internal.Coverage, s.Internal.Gap)));
    }
}
