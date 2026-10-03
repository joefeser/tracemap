using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class PromotionReviewRegressionTests
{
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
        Assert.Contains(calls, fact => fact.SourceSymbol?.Contains("Run", StringComparison.Ordinal) == true
            && fact.TargetSymbol?.Contains("Ping", StringComparison.Ordinal) == true);
        Assert.Contains(calls, fact => fact.SourceSymbol?.Contains("Tail", StringComparison.Ordinal) == true
            && fact.TargetSymbol?.Contains("Ping", StringComparison.Ordinal) == true);
    }
}

public sealed class PromotionReviewProjectScopeTests
{
    [Fact]
    public void Explicit_csharp_project_scope_in_mixed_repository_does_not_claim_missing_visual_basic_project()
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
            ProjectPaths = ["app/App.csproj"]
        });

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "NoVisualBasicProjectOrSolution");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "VisualBasicProjectsOutsideProjectScope");
        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
    }
}

public sealed class PromotionReviewWebFormsClientTests
{
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
              $(this).prop('disabled', true);
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
