using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class PromotionReviewRegressionTests
{
    [Fact]
    public void With_and_conditional_members_preserve_semantic_calls_properties_and_database_operations()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Implicit.vbproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><OptionStrict>On</OptionStrict></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Implicit.vb"), """
            Imports System.Data
            Imports System.Data.Common
            Public Class Worker
                Public Property Count As Integer
                Public Function Ping() As Integer
                    Return 1
                End Function
                Public Sub Run(w As Worker, cmd As DbCommand)
                    With w
                        .Ping()
                        Dim a = .Ping
                        Dim b = .Count
                        .Count = 2
                    End With
                    Dim c = w?.Ping()
                    Dim d = w?.Ping
                    Dim e = w?.Count
                    With cmd
                        .CommandText = "select 1"
                        .CommandType = CommandType.Text
                        Dim f = .ExecuteScalar()
                        Dim g = .ExecuteScalar
                        .Parameters.Add(.CreateParameter())
                    End With
                    Dim h = cmd?.ExecuteScalar()
                    Dim i = cmd?.ExecuteScalar
                End Sub
            End Class
            """);
        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        Assert.DoesNotContain(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind")
            is "VisualBasicDocumentExtractionFailed" or "ProjectLoadFailed");
        var calls = result.Facts.Where(fact => fact.RuleId == RuleIds.VisualBasicSemanticCallGraph
            && fact.TargetSymbol?.Contains("Ping", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(4, calls.Length);
        Assert.All(calls, fact => Assert.Equal(EvidenceTiers.Tier1Semantic, fact.EvidenceTier));
        var invocations = result.Facts.Where(fact => fact.FactType == FactTypes.MethodInvoked
            && fact.TargetSymbol?.Contains("Ping", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(4, invocations.Length);
        Assert.All(invocations, fact => Assert.Equal("unknown", fact.Properties.GetValueOrDefault("receiverIdentityStatus")));
        Assert.Equal(3, result.Facts.Count(fact => fact.RuleId == RuleIds.VisualBasicSemanticPropertyAccess
            && fact.Properties.GetValueOrDefault("propertyName") == "Count"));
        Assert.Equal(4, result.Facts.Count(fact => fact.FactType == FactTypes.DatabaseOperationCandidate
            && fact.Properties.GetValueOrDefault("methodName") == "ExecuteScalar"));
    }

    [Fact]
    public void Visual_basic_parenthesis_free_and_null_conditional_invocations_do_not_abort_semantic_extraction()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Calls.vbproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OptionStrict>On</OptionStrict>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Calls.vb"), """
            Imports System.Collections.Generic

            Public Class Worker
                Public Sub Ping()
                End Sub

                Public Sub Run(items As List(Of Integer), callback As Action(Of Integer))
                    Ping
                    Call Ping
                    Me.Ping
                    Dim first = items?(0)
                    callback?(1)
                End Sub
            End Class

            Public Class Later
                Public Sub Tail()
                    Dim w As New Worker()
                    w.Ping()
                End Sub
            End Class
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") is "SolutionLoadFailed" or "ProjectLoadFailed" or "VisualBasicDocumentExtractionFailed");
        var calls = result.Facts.Where(fact => fact.RuleId == RuleIds.VisualBasicSemanticCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        Assert.Equal(3, calls.Count(fact => fact.SourceSymbol?.Contains("Run", StringComparison.Ordinal) == true
            && fact.TargetSymbol?.Contains("Ping", StringComparison.Ordinal) == true));
        Assert.Contains(calls, fact => fact.SourceSymbol?.Contains("Tail", StringComparison.Ordinal) == true
            && fact.TargetSymbol?.Contains("Ping", StringComparison.Ordinal) == true);
    }
}

public sealed class PromotionReviewProjectScopeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Relative_repository_with_explicit_scope_has_the_same_scan_identity(bool solution)
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "App.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(repo, "App.cs"), "class App {}");
        File.WriteAllText(Path.Combine(repo, "App.sln"), "");
        var options = new ScanOptions(repo, Path.Combine(temp.Path, "out"),
            ProjectPaths: solution ? null : ["App.csproj"], SolutionPaths: solution ? ["App.sln"] : null);
        var absolute = ScanEngine.Scan(options);
        var relative = ScanEngine.Scan(options with { RepoPath = Path.GetRelativePath(Environment.CurrentDirectory, repo) });
        Assert.Equal(absolute.Manifest.ScanId, relative.Manifest.ScanId);
    }

    [Theory]
    [InlineData("CSharpProjectsOutsideProjectScope", "ProjectScopeExcludedLanguage", "informational", "ReviewScanScope")]
    [InlineData("VisualBasicProjectsOutsideProjectScope", "ProjectScopeExcludedLanguage", "informational", "ReviewScanScope")]
    [InlineData("VisualBasicDocumentExtractionFailed", "DocumentExtractionFailed", "reduces-semantic-coverage", "ReportExtractorDefect")]
    public void Diagnostic_categories_preserve_scope_and_extractor_failure_semantics(string kind, string code, string coverage, string guidance)
    {
        var diagnostic = BuildEnvironmentDiagnosticExtractor.SanitizeWorkspaceGap(kind, "private-secret-path");
        Assert.Equal(code, diagnostic.DiagnosticCode);
        Assert.Equal(coverage, diagnostic.CoverageEffect);
        Assert.Equal(guidance, diagnostic.GuidanceCode);
        Assert.DoesNotContain("private-secret-path", diagnostic.Message);
        Assert.DoesNotContain("UncategorizedWorkspaceFailure", diagnostic.Message);
    }

    [Theory]
    [InlineData("app/Ap.csproj", false)]
    [InlineData("app/Ap.csproj", true)]
    [InlineData("lib/Li.vbproj", true)]
    [InlineData("app/App.cs", true)]
    [InlineData("../App.csproj", true)]
    public void Unmatched_explicit_project_scope_fails_before_semantic_analysis(string unmatched, bool includeValid)
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "app"));
        Directory.CreateDirectory(Path.Combine(repo, "lib"));
        File.WriteAllText(Path.Combine(repo, "app", "App.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(repo, "app", "App.cs"), "class App {}");
        File.WriteAllText(Path.Combine(repo, "lib", "Lib.vbproj"), "<Project />");
        var exception = Assert.Throws<ArgumentException>(() => ScanEngine.Scan(
            new ScanOptions(repo, Path.Combine(temp.Path, "out"))
            {
                ProjectPaths = includeValid ? ["app/App.csproj", "lib/Lib.vbproj", unmatched] : [unmatched]
            }));
        Assert.Contains("ProjectScopeUnmatched", exception.Message);
        Assert.False(File.Exists(Path.Combine(temp.Path, "out", "scan-manifest.json")));
    }

    [Theory]
    [InlineData("app/App.csproj")]
    [InlineData("app\\App.csproj")]
    [InlineData("./app/../app/App.csproj")]
    public void Explicit_csharp_project_scope_in_mixed_repository_does_not_claim_missing_visual_basic_project(string projectPath)
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "app"));
        Directory.CreateDirectory(Path.Combine(repo, "lib"));
        File.WriteAllText(Path.Combine(repo, "app", "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "app", "App.cs"), "public sealed class App { public void Run() { } }");
        File.WriteAllText(Path.Combine(repo, "lib", "Lib.vbproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "lib", "Lib.vb"), "Public Class Lib\nEnd Class\n");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out"))
        {
            ProjectPaths = [projectPath]
        });

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "NoVisualBasicProjectOrSolution");
        var excluded = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "VisualBasicProjectsOutsideProjectScope");
        Assert.Equal("ProjectScopeExcludedLanguage", excluded.Properties.GetValueOrDefault("diagnosticCode"));
        Assert.Equal("scan-scope", excluded.Properties.GetValueOrDefault("diagnosticKind"));
        Assert.Equal("informational", excluded.Properties.GetValueOrDefault("coverageEffect"));
        Assert.Equal("ReviewScanScope", excluded.Properties.GetValueOrDefault("guidanceCode"));
        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
    }
}

public sealed class PromotionReviewWebFormsClientTests
{
    [Theory]
    [InlineData("const r = /'/; next();")]
    [InlineData("const r = /https?:\\/\\//; next();")]
    [InlineData("const r = /[}'/]/g; next();")]
    [InlineData("return /}/.test(value);")]
    [InlineData("await /}/.test(value); next();")]
    [InlineData("await /[}'/]/g.test(value); next();")]
    [InlineData("const n = count / 2; next();")]
    [InlineData("const n = count++ / 2; next();")]
    [InlineData("const s = 'https://example.invalid/}'; next();")]
    [InlineData("const s = `// }`; next();")]
    [InlineData("<!-- } ignored\n next();")]
    [InlineData("//<![CDATA[ } ignored\n next();")]
    [InlineData("/* } ignored */ next();")]
    public void Javascript_block_bounds_ignore_literals_and_comments(string body)
    {
        var text = "{" + body + "} trailing {";
        Assert.Equal(body.Length + 1, LegacyWebFormsExtractor.FindJavascriptBlockEnd(text, 0, text.Length));
    }

    [Theory]
    [InlineData("{ const r = /unterminated }")]
    [InlineData("{ /* unterminated }")]
    [InlineData("{ const s = 'unterminated }")]
    public void Javascript_unterminated_literals_do_not_invent_a_block_end(string text) =>
        Assert.Equal(-1, LegacyWebFormsExtractor.FindJavascriptBlockEnd(text, 0, text.Length));

    [Fact]
    public void Inline_client_callbacks_skip_comments_stay_in_script_and_do_not_bind_external_urls_to_local_handlers()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "api"));
        File.WriteAllText(Path.Combine(repo, "Edit.aspx"), """
            <%@ Page Language="VB" CodeFile="Edit.aspx.vb" Inherits="Edit" %>
            <script>
            $('#Save').click(function () { // don't double-submit
              const work = async () => { await /}/.test(value); }; $(this).prop('disabled', true);
              $.ajax({ type: 'POST', url: 'https://partner.example.com/svc/Upload.ashx' });
            });
            </script>
            <script>
            function later() { $.ajax({ url: '//cdn.example.com/Upload.ashx' }); }
            </script>
            """);
        File.WriteAllText(Path.Combine(repo, "Edit.aspx.vb"), "Partial Public Class Edit\nEnd Class\n");
        File.WriteAllText(Path.Combine(repo, "api", "Upload.ashx"), """
            <%@ WebHandler Language="VB" Class="Upload" %>
            Public Class Upload : Implements System.Web.IHttpHandler
                Public Sub ProcessRequest(context As System.Web.HttpContext) Implements System.Web.IHttpHandler.ProcessRequest
                End Sub
                Public ReadOnly Property IsReusable As Boolean Implements System.Web.IHttpHandler.IsReusable
                    Get
                        Return False
                    End Get
                End Property
            End Class
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var click = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsClientEventBindingCandidate);
        Assert.Equal(3, click.Evidence.StartLine);
        Assert.Equal(6, click.Evidence.EndLine);
        var requests = result.Facts.Where(fact => fact.FactType == FactTypes.WebFormsClientHttpRequestCandidate)
            .OrderBy(fact => fact.Evidence.StartLine).ToArray();
        Assert.Equal(2, requests.Length);
        Assert.All(requests, request =>
        {
            Assert.Equal("external-absolute-url-not-resolved", request.Properties.GetValueOrDefault("targetResolution"));
            Assert.False(request.Properties.ContainsKey("endpointDeclarationFile"));
        });
        Assert.True(requests[0].Properties.ContainsKey("clientEventId"));
        Assert.Equal(5, requests[0].Evidence.StartLine);
        Assert.False(requests[1].Properties.ContainsKey("clientEventId"));
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.RuleId == RuleIds.LegacyWebFormsClientHttpHandlerResolution);
    }
}

public sealed class PromotionReviewInlineServerExpressionTests
{
    [Theory]
    [InlineData("VB", "Replace(p, \"\\\", \"/\") & Customer.DefaultName")]
    [InlineData("VisualBasic", "Replace(p, \"\\\", \"/\") & Customer.DefaultName")]
    [InlineData("C#", "p.Replace(\"\\\\\", \"/\") + Customer.DefaultName")]
    [InlineData("C#", "$\"{Customer.DefaultName}\"")]
    [InlineData("C#", "$@\"literal {{Customer.Decoy}} {Customer.DefaultName}\"")]
    [InlineData("C#", "@$\"literal {Customer.DefaultName}\"")]
    [InlineData("C#", "$\"{Customer.DefaultName + \"Other.Decoy\"}\"")]
    [InlineData("C#", "$\"{Customer.DefaultName:Other.Decoy}\"")]
    public void Inline_directive_language_controls_literal_masking_without_codebehind(string language, string expression)
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "App_Code"));
        File.WriteAllText(Path.Combine(repo, "Page.aspx"), $"<%@ Page Language=\"{language}\" %>\n<%= {expression} %>");
        File.WriteAllText(Path.Combine(repo, "App_Code", "Customer.vb"), "Public Class Customer\n Public Shared DefaultName As String\nEnd Class");
        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var reference = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsInlineServerExpressionReferenceCandidate);
        Assert.Equal(2, reference.Evidence.StartLine);
    }

    [Fact]
    public void Inline_server_expression_string_literals_are_not_type_references()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "App_Code"));
        File.WriteAllText(Path.Combine(repo, "List.aspx"), """
            <%@ Page Language="VB" CodeFile="List.aspx.vb" Inherits="List" %>
            <span><%# Eval("Customer.Name") %></span>
            <span><%= Customer.DefaultName %></span>
            """);
        File.WriteAllText(Path.Combine(repo, "List.aspx.vb"), "Partial Public Class List\nEnd Class\n");
        File.WriteAllText(Path.Combine(repo, "App_Code", "Customer.vb"), """
            Public Class Customer
                Public Shared DefaultName As String = "x"
            End Class
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var reference = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsInlineServerExpressionReferenceCandidate);
        Assert.Equal(3, reference.Evidence.StartLine);
        Assert.Equal("render-expression", reference.Properties.GetValueOrDefault("expressionKind"));
    }
}

public sealed class PromotionReviewVisualBasicExpressionCallTests
{
    [Theory]
    [InlineData("late.Foo", "Off")]
    [InlineData("MissingValue", "On")]
    public void Unresolved_parenthesis_free_expressions_are_labeled_not_invented_as_calls(string expression, string strict)
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Calls.vbproj"), $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OptionStrict>{strict}</OptionStrict></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(repo, "Calls.vb"), $"Public Class Calls\n Public Sub Run(late As Object)\n Dim x = {expression}\n End Sub\nEnd Class");
        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        Assert.Contains(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "ExpressionSemanticResolutionUnavailable");
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.VisualBasicSemanticCallGraph);
        Assert.DoesNotContain(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "VisualBasicDocumentExtractionFailed");
    }

    [Fact]
    public void Visual_basic_parenthesis_free_expression_calls_emit_call_and_database_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        var sqliteAssembly = typeof(Microsoft.Data.Sqlite.SqliteCommand).Assembly.Location;
        File.WriteAllText(Path.Combine(repo, "Data.vbproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OptionStrict>On</OptionStrict>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Microsoft.Data.Sqlite">
                  <HintPath>{System.Security.SecurityElement.Escape(sqliteAssembly)}</HintPath>
                </Reference>
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Data.vb"), """
            Imports Microsoft.Data.Sqlite
            Imports System.Collections.Generic

            Public Class Repository
                Public Function GetValue() As Integer
                    Return 1
                End Function

                Public Function GenericValue(Of T)() As T
                    Return Nothing
                End Function

                Public Function GetList() As List(Of Integer)
                    Return New List(Of Integer) From {1}
                End Function

                Public Function Load(cmd As SqliteCommand) As Integer
                    Dim value = GetValue
                    Dim scalar = cmd.ExecuteScalar
                    Dim reader = cmd.ExecuteReader
                    Dim generic = GenericValue(Of Integer)
                    Dim indexed = GetList(0)
                    Dim address As System.Func(Of Integer) = AddressOf GetValue
                    Return value
                End Function
            End Class
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.VisualBasicSemanticCallGraph
            && fact.SourceSymbol?.Contains("Load", StringComparison.Ordinal) == true
            && fact.TargetSymbol?.Contains("GetValue", StringComparison.Ordinal) == true);
        foreach (var target in new[] { "GetValue", "GenericValue", "GetList" })
            Assert.Single(result.Facts, fact => fact.RuleId == RuleIds.VisualBasicSemanticCallGraph
                && fact.SourceSymbol?.Contains("Load", StringComparison.Ordinal) == true
                && fact.TargetSymbol?.Contains(target, StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "VisualBasicDocumentExtractionFailed");
        var operations = result.Facts.Where(fact => fact.FactType == FactTypes.DatabaseOperationCandidate
                && fact.SourceSymbol?.Contains("Load", StringComparison.Ordinal) == true)
            .Select(fact => fact.Properties["methodName"]).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["ExecuteReader", "ExecuteScalar"], operations);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.CallEdge
            && fact.TargetSymbol?.Contains("GetValue", StringComparison.Ordinal) == true
            && fact.SourceSymbol?.Contains("GetValue", StringComparison.Ordinal) == true);
    }
}
