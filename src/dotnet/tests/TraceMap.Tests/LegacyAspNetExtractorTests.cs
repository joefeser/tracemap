using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class LegacyAspNetExtractorTests
{
    [Fact]
    public void Scan_extracts_static_aspnet_route_config_handler_page_method_and_navigation_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteClassicAspNetFixture(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var facts = result.Facts;

        Assert.Contains(result.Inventory, item => item is { RelativePath: "Global.asax", Kind: "AspNetApplication" });
        Assert.Contains(result.Inventory, item => item is { RelativePath: "Ping.ashx", Kind: "AspNetHandler" });
        Assert.Contains(result.Inventory, item => item is { RelativePath: "Web.sitemap", Kind: "AspNetSiteMap" });
        Assert.Contains(result.Inventory, item => item is { RelativePath: "Controls/Picker.ascx", Kind: "WebFormsMarkup" });
        Assert.Contains(result.Inventory, item => item is { RelativePath: "Site.master", Kind: "WebFormsMarkup" });

        Assert.Contains(facts, fact => fact.FactType == FactTypes.AspNetSurfaceDeclared && fact.RuleId == RuleIds.LegacyAspNetSurface && fact.Properties.GetValueOrDefault("surfaceKind") == "application");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AspNetHandlerDeclared && fact.RuleId == RuleIds.LegacyAspNetHandler && fact.Properties.GetValueOrDefault("handlerKind") == "ashx-directive");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AspNetHandlerDeclared && fact.Properties.GetValueOrDefault("handlerKind") == "handler-interface-type");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AspNetConfigSurfaceDeclared && fact.Properties.GetValueOrDefault("sectionKind") == "system.webServer/handlers");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AspNetPageMethodDeclared && fact.RuleId == RuleIds.LegacyAspNetPageMethod && fact.ContractElement == "Lookup");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AspNetNavigationReferenceDeclared && fact.Properties.GetValueOrDefault("targetPath") == "Details.aspx");
        var pageNavigationEdges = facts.Where(fact => fact.FactType == FactTypes.AspNetNavigationEdgeDeclared && fact.Properties.GetValueOrDefault("targetFactType") == FactTypes.WebFormsPageDeclared).ToArray();
        Assert.NotEmpty(pageNavigationEdges);
        Assert.All(pageNavigationEdges, edge => Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, edge.EvidenceTier));
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AnalysisGap && fact.RuleId == RuleIds.LegacyAspNetRoute && fact.Properties.GetValueOrDefault("gapKind") == "DynamicRouteRegistration");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AnalysisGap && fact.RuleId == RuleIds.LegacyAspNetSurface && fact.Properties.GetValueOrDefault("gapKind") == "DesignerWithoutMarkupSurface");

        var route = Assert.Single(facts, fact => fact.FactType == FactTypes.AspNetRouteDeclared);
        Assert.Equal(RuleIds.LegacyAspNetRoute, route.RuleId);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, route.EvidenceTier);
        Assert.Equal("Details.aspx", route.Properties.GetValueOrDefault("mappedPagePath"));
        var routeHash = Assert.Contains("routePatternHash", route.Properties);
        Assert.Matches("^[0-9a-f]{32}$", routeHash);

        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.HttpRouteBinding);
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.AsmxOperationDeclared && fact.ContractElement == "Lookup");
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.AspNetSurfaceDeclared && fact.Evidence.FilePath == "Default.aspx");

        var report = MarkdownReportWriter.Build(result);
        Assert.Contains("## Legacy ASP.NET Static Surface Evidence", report);
        Assert.Contains("## Legacy ASP.NET Surface Limitations", report);
        Assert.Contains("route candidate", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scan_hashes_or_omits_unsafe_route_config_and_navigation_values_without_hash_only_edges()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:HyperLink runat="server" ID="Unsafe" NavigateUrl="Admin.aspx?tenant=one" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System.Web.Routing;
            namespace Sample;
            public partial class Default
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapPageRoute("UnsafeRoute", "Admin.aspx?tenant=one", "~/Default.aspx");
                }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "web.config"), """
            <configuration>
              <location path="Admin.aspx?tenant=one">
                <system.webServer>
                  <handlers>
                    <add name="Unsafe" path="Admin.aspx?tenant=one" verb="GET" type="Sample.UnsafeHandler" />
                  </handlers>
                </system.webServer>
              </location>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var serialized = SerializeFacts(result.Facts);

        Assert.DoesNotContain("Admin.aspx?tenant=one", serialized);
        Assert.DoesNotContain("tenant=one", serialized);
        Assert.DoesNotContain("https://", serialized);

        var routeHash = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AspNetRouteDeclared).Properties.GetValueOrDefault("routePatternHash");
        var configHash = result.Facts.First(fact => fact.FactType == FactTypes.AspNetConfigSurfaceDeclared).Properties.GetValueOrDefault("configScopePathHash");
        var navigationHash = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AspNetNavigationReferenceDeclared).Properties.GetValueOrDefault("targetPathHash");

        Assert.Matches("^[0-9a-f]{32}$", routeHash);
        Assert.Matches("^[0-9a-f]{32}$", configHash);
        Assert.Matches("^[0-9a-f]{32}$", navigationHash);
        Assert.Equal(3, new[] { routeHash, configHash, navigationHash }.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AspNetNavigationEdgeDeclared);
    }

    [Fact]
    public void Scan_does_not_create_hash_only_edges_across_navigation_and_config_facts()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:HyperLink runat="server" ID="Report" NavigateUrl="Reports/{year}" />
            """);
        File.WriteAllText(Path.Combine(repo, "web.config"), """
            <configuration>
              <location path="Reports/{year}">
                <system.webServer>
                  <handlers>
                    <add name="Report" path="*.aspx" verb="GET" type="Sample.ReportHandler" />
                  </handlers>
                </system.webServer>
              </location>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var navigation = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AspNetNavigationReferenceDeclared);
        var config = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AspNetConfigSurfaceDeclared);
        Assert.True(navigation.Properties.ContainsKey("targetPathHash"));
        Assert.False(navigation.Properties.ContainsKey("targetPath"));
        Assert.True(config.Properties.ContainsKey("configScopePathHash"));
        Assert.False(config.Properties.ContainsKey("configScopePath"));
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AspNetNavigationEdgeDeclared);
    }

    [Fact]
    public void Scan_emits_gap_for_sitemap_node_without_url()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Web.sitemap"), """
            <siteMap>
              <siteMapNode title="Root" />
            </siteMap>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAspNetNavigation
            && fact.Properties.GetValueOrDefault("gapKind") == "SiteMapNodeMissingUrl");
    }

    [Fact]
    public void Scan_does_not_emit_handler_fact_for_malformed_ashx_directive()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Broken.ashx"), """
            <%@ Handler Language="C#" Class="Sample.BrokenHandler" %>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAspNetHandler
            && fact.Properties.GetValueOrDefault("gapKind") == "MalformedAspNetHandlerDirective");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AspNetHandlerDeclared
            && fact.Evidence.FilePath == "Broken.ashx");
    }

    [Fact]
    public void Scan_ignores_static_navigation_inside_markup_comments()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:HyperLink runat="server" ID="DetailsLink" NavigateUrl="~/Details.aspx" />
            <!-- <asp:HyperLink runat="server" ID="HiddenHtml" NavigateUrl="~/HiddenHtml.aspx" /> -->
            <%-- <asp:HyperLink runat="server" ID="HiddenServer" NavigateUrl="~/HiddenServer.aspx" /> --%>
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            namespace Sample;
            public partial class Default { }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var navigationTargets = result.Facts
            .Where(fact => fact.FactType == FactTypes.AspNetNavigationReferenceDeclared)
            .Select(fact => fact.Properties.GetValueOrDefault("targetPath"))
            .Where(value => value is not null)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("Details.aspx", navigationTargets);
        Assert.DoesNotContain("HiddenHtml.aspx", navigationTargets);
        Assert.DoesNotContain("HiddenServer.aspx", navigationTargets);
    }

    [Fact]
    public void Scan_ignores_local_variables_named_like_navigation_properties()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            namespace Sample;
            public partial class Default
            {
                protected void Configure()
                {
                    string Action;
                    Action = "~/Hidden.aspx";
                    Link.NavigateUrl = "~/Details.aspx";
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var navigationTargets = result.Facts
            .Where(fact => fact.FactType == FactTypes.AspNetNavigationReferenceDeclared)
            .Select(fact => fact.Properties.GetValueOrDefault("targetPath"))
            .Where(value => value is not null)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("Details.aspx", navigationTargets);
        Assert.DoesNotContain("Hidden.aspx", navigationTargets);
    }

    [Fact]
    public void Scan_hashes_leading_slash_paths_before_normalizing()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="/Outside/Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:HyperLink runat="server" ID="Absolute" NavigateUrl="/Outside/Target.aspx" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var serialized = SerializeFacts(result.Facts);

        Assert.DoesNotContain("/Outside/Default.aspx.cs", serialized);
        Assert.DoesNotContain("Outside/Default.aspx.cs", serialized);
        Assert.DoesNotContain("/Outside/Target.aspx", serialized);
        Assert.DoesNotContain("Outside/Target.aspx", serialized);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AspNetNavigationReferenceDeclared
            && fact.Properties.ContainsKey("targetPathHash")
            && !fact.Properties.ContainsKey("targetPath"));
    }

    [Fact]
    public void Scan_omits_secret_like_navigation_target_without_edge()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:HyperLink runat="server" ID="SecretLink" NavigateUrl="Password=super-secret" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var reference = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AspNetNavigationReferenceDeclared);
        var serialized = SerializeFacts(result.Facts);

        Assert.Equal("secret-like", reference.Properties.GetValueOrDefault("targetOmitted"));
        Assert.False(reference.Properties.ContainsKey("targetPath"));
        Assert.False(reference.Properties.ContainsKey("targetPathHash"));
        Assert.Null(reference.TargetSymbol);
        Assert.DoesNotContain("Password=super-secret", serialized);
        Assert.DoesNotContain("super-secret", serialized);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AspNetNavigationEdgeDeclared);
    }

    [Fact]
    public void Scan_emits_script_service_class_evidence_without_page_method()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "ScriptApi.cs"), """
            using System.Web.Script.Services;
            namespace Sample;
            [ScriptService]
            public sealed class ScriptApi
            {
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AspNetPageMethodDeclared
            && fact.RuleId == RuleIds.LegacyAspNetPageMethod
            && fact.Properties.GetValueOrDefault("pageMethodKind") == "script-service-class"
            && fact.Properties.GetValueOrDefault("scriptServiceClass") == "True");
    }

    [Fact]
    public void Scan_marks_reduced_coverage_and_ignores_comments_strings_and_inactive_code()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            namespace Sample;
            public partial class Default
            {
                // [WebMethod] public static string CommentOnly() => "x";
                private const string Text = "Response.Redirect(\"~/Hidden.aspx\")";
            #if false
                [System.Web.Services.WebMethod]
                public static string Inactive() => "x";
            #endif
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAspNetSurface
            && fact.Properties.GetValueOrDefault("gapKind") == "ReducedSemanticCoverage");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AspNetPageMethodDeclared);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AspNetNavigationReferenceDeclared);
    }

    [Fact]
    public void Scan_outputs_stable_aspnet_fact_ids_for_fixed_inputs()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteClassicAspNetFixture(repo);

        var first = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-a")));
        var second = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-b")));

        var firstIds = first.Facts
            .Where(fact => fact.FactType.StartsWith("AspNet", StringComparison.Ordinal) || fact.RuleId.StartsWith("legacy.aspnet.", StringComparison.Ordinal))
            .Select(fact => fact.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var secondIds = second.Facts
            .Where(fact => fact.FactType.StartsWith("AspNet", StringComparison.Ordinal) || fact.RuleId.StartsWith("legacy.aspnet.", StringComparison.Ordinal))
            .Select(fact => fact.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(firstIds);
        Assert.Equal(firstIds, secondIds);
    }

    private static void WriteClassicAspNetFixture(string repo)
    {
        File.WriteAllText(Path.Combine(repo, "Global.asax"), """
            <%@ Application Language="C#" Codebehind="Global.asax.cs" Inherits="Sample.Global" %>
            """);
        File.WriteAllText(Path.Combine(repo, "Global.asax.cs"), """
            using System.Web.Routing;
            namespace Sample;
            public class Global
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapPageRoute("DetailsRoute", "Details/{id}", "~/Details.aspx");
                    var dynamicPattern = "Dynamic/" + "{id}";
                    routes.MapPageRoute("DynamicRoute", dynamicPattern, "~/Details.aspx");
                }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:HyperLink runat="server" ID="DetailsLink" NavigateUrl="~/Details.aspx" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System.Web.Services;
            namespace Sample;
            public partial class Default
            {
                [WebMethod]
                public static string Lookup(string id) => id;
                protected void Go()
                {
                    Response.Redirect("~/Details.aspx");
                }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.designer.cs"), """
            namespace Sample;
            public partial class Default
            {
                protected global::System.Web.UI.WebControls.HyperLink DetailsLink;
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Details.aspx"), """
            <%@ Page Language="C#" CodeBehind="Details.aspx.cs" Inherits="Sample.Details" %>
            """);
        File.WriteAllText(Path.Combine(repo, "Details.aspx.cs"), """
            namespace Sample;
            public partial class Details { }
            """);
        Directory.CreateDirectory(Path.Combine(repo, "Controls"));
        File.WriteAllText(Path.Combine(repo, "Controls", "Picker.ascx"), """
            <%@ Control Language="C#" CodeBehind="Picker.ascx.cs" Inherits="Sample.Controls.Picker" %>
            """);
        File.WriteAllText(Path.Combine(repo, "Controls", "Picker.ascx.cs"), """
            namespace Sample.Controls;
            public partial class Picker { }
            """);
        File.WriteAllText(Path.Combine(repo, "Site.master"), """
            <%@ Master Language="C#" CodeBehind="Site.master.cs" Inherits="Sample.Site" %>
            """);
        File.WriteAllText(Path.Combine(repo, "Site.master.cs"), """
            namespace Sample;
            public partial class Site { }
            """);
        File.WriteAllText(Path.Combine(repo, "Orphan.aspx.designer.cs"), """
            namespace Sample;
            public partial class Orphan
            {
                protected global::System.Web.UI.WebControls.Label Missing;
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Ping.ashx"), """
            <%@ WebHandler Language="C#" Class="Sample.PingHandler" CodeBehind="Ping.ashx.cs" %>
            """);
        File.WriteAllText(Path.Combine(repo, "Ping.ashx.cs"), """
            namespace Sample;
            public sealed class PingHandler : System.Web.IHttpHandler
            {
                public bool IsReusable => true;
                public void ProcessRequest(System.Web.HttpContext context) { }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "web.config"), """
            <configuration>
              <system.webServer>
                <handlers>
                  <add name="Ping" path="*.ashx" verb="GET,POST" type="Sample.PingHandler" />
                </handlers>
              </system.webServer>
              <system.web>
                <pages>
                  <controls>
                    <add tagPrefix="sample" namespace="Sample.Controls" />
                  </controls>
                </pages>
              </system.web>
            </configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Web.sitemap"), """
            <siteMap>
              <siteMapNode title="Root" url="Default.aspx">
                <siteMapNode title="Details" url="Details.aspx" />
              </siteMapNode>
            </siteMap>
            """);
    }

    private static string SerializeFacts(IEnumerable<CodeFact> facts)
    {
        return JsonSerializer.Serialize(facts, new JsonSerializerOptions { WriteIndented = true });
    }
}
