using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class LegacyWebFormsExtractorTests
{
    [Fact]
    public void Scan_extracts_markup_binding_handler_designer_and_report_sections()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteBasicPage(repo, "Save_Click", handlerBody: "Total.Text = \"saved\";");
        File.WriteAllText(Path.Combine(repo, "Default.aspx.designer.cs"), """
            namespace Sample;
            public partial class Default
            {
                protected global::System.Web.UI.WebControls.Button SaveButton;
                protected global::System.Web.UI.WebControls.Label Total;
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var facts = result.Facts;

        Assert.Contains(result.Inventory, item => item is { RelativePath: "Default.aspx", Kind: "WebFormsMarkup" });
        Assert.Contains(result.Inventory, item => item is { RelativePath: "Default.aspx.cs", Kind: "WebFormsCodeBehind" });
        Assert.Contains(result.Inventory, item => item is { RelativePath: "Default.aspx.designer.cs", Kind: "WebFormsDesigner" });
        var binding = Assert.Single(facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared);
        Assert.Equal(RuleIds.LegacyWebFormsEventBinding, binding.RuleId);
        Assert.Equal("SaveButton", binding.Properties.GetValueOrDefault("controlId"));
        Assert.Equal("Save_Click", binding.Properties.GetValueOrDefault("handlerName"));
        Assert.False(string.IsNullOrWhiteSpace(binding.Properties.GetValueOrDefault("designerFactId")));

        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.RuleId == RuleIds.LegacyWebFormsHandlerResolution
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural
            && fact.Properties.GetValueOrDefault("resolutionKind") == "StructuralLinkedPartialMethod");
        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.WebFormsLogicSignalDetected
            && fact.Properties.GetValueOrDefault("signalKind") == "UiBoilerplateSignal");

        var report = MarkdownReportWriter.Build(result);
        Assert.Contains("## WebForms Events", report);
        Assert.Contains("## WebForms Event Flow", report);
        Assert.Contains("## WebForms Limitations", report);
    }

    [Fact]
    public void Scan_preserves_duplicate_bindings_with_stable_distinct_fact_ids()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            <asp:Button runat="server" ID="SaveButton" OnCommand="Save_Click" />
            """);
        WriteCodeBehind(repo, "Save_Click", "");

        var first = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-a")));
        var second = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-b")));

        var firstIds = first.Facts
            .Where(fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared)
            .Select(fact => fact.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var secondIds = second.Facts
            .Where(fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared)
            .Select(fact => fact.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, firstIds.Length);
        Assert.Equal(2, firstIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(firstIds, secondIds);
    }

    [Fact]
    public void Scan_resolves_windows_style_relative_codebehind_paths()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Controls"));
        File.WriteAllText(Path.Combine(repo, "Nested.aspx"), """
            <%@ Page Language="C#" CodeBehind="Controls\Nested.aspx.cs" Inherits="Sample.Nested" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Controls", "Nested.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Nested
            {
                protected void Save_Click(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Save_Click"
            && fact.Properties.GetValueOrDefault("linkedCodePath") == "Controls/Nested.aspx.cs");
    }

    [Fact]
    public void Scan_emits_ambiguity_and_auto_wireup_gaps_conservatively()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" AutoEventWireup="false" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Default
            {
                protected void Save_Click(object sender, EventArgs e) { }
                protected void Save_Click(object sender, System.EventArgs e, string extra = "") { }
                protected void Page_Load(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsHandlerResolution
            && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousWebFormsHandler");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "AutoEventWireupUnavailable");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Page_Load");
    }

    [Fact]
    public void Scan_resolves_explicit_auto_wireup_when_enabled()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" AutoEventWireup="true" %>
            """);
        WriteCodeBehind(repo, "Page_Load", "");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Page_Load"
            && fact.Properties.GetValueOrDefault("autoEventWireup") == "True");
    }

    [Fact]
    public void Scan_resolves_lifecycle_handler_with_explicit_static_subscription()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" AutoEventWireup="false" %>
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Default
            {
                public Default()
                {
                    Load += Page_Load;
                }

                protected void Page_Load(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Page_Load"
            && fact.Properties.GetValueOrDefault("explicitEventSubscription") == "True");
    }

    [Fact]
    public void Scan_projects_direct_webforms_handler_flow_to_wcf_and_sql_without_raw_sql()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Contracts.cs"), """
            using System.ServiceModel;
            namespace Sample.Contracts;
            [ServiceContract]
            public interface IRatingService
            {
                [OperationContract]
                string Rate(string request);
            }
            """);
        File.WriteAllText(Path.Combine(repo, "RatingClient.cs"), """
            using System.ServiceModel;
            namespace Sample.Contracts;
            public partial class RatingClient : ClientBase<IRatingService>, IRatingService
            {
                public string Rate(string request) => Channel.Rate(request);
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            using Sample.Contracts;
            namespace Sample;
            public partial class Default
            {
                protected void Save_Click(object sender, EventArgs e)
                {
                    var client = new RatingClient();
                    client.Rate("x");
                    var sql = "select Id from Orders";
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WcfServiceReferenceMapping);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.QueryPatternDetected);
        var flow = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventFlowProjected);
        Assert.Contains(flow.Properties.GetValueOrDefault("flowClassification"), new[] { "ProbableStaticEventFlow", "NeedsReviewEventFlow", "StrongStaticEventFlow" });
        Assert.False(string.IsNullOrWhiteSpace(flow.Properties.GetValueOrDefault("supportingFactIds")));
        Assert.Contains(RuleIds.LegacyWebFormsEventFlow, flow.Properties.GetValueOrDefault("ruleIds"));

        var serializedWebForms = SerializeFacts(result.Facts.Where(fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal)));
        Assert.DoesNotContain("select Id from Orders", serializedWebForms);
    }

    [Fact]
    public void Scan_does_not_project_wcf_flow_from_operation_name_collision()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Contracts.cs"), """
            using System.ServiceModel;
            namespace Sample.Contracts;
            [ServiceContract]
            public interface IRatingService
            {
                [OperationContract]
                string Rate(string request);
            }
            """);
        File.WriteAllText(Path.Combine(repo, "RatingClient.cs"), """
            using System.ServiceModel;
            namespace Sample.Contracts;
            public partial class RatingClient : ClientBase<IRatingService>, IRatingService
            {
                public string Rate(string request) => Channel.Rate(request);
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Default
            {
                protected void Save_Click(object sender, EventArgs e)
                {
                    Rate();
                }

                private void Rate()
                {
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var wcfFactIds = result.Facts
            .Where(fact => fact.FactType == FactTypes.WcfServiceReferenceMapping)
            .Select(fact => fact.FactId)
            .ToArray();
        Assert.NotEmpty(wcfFactIds);
        var flow = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventFlowProjected);
        Assert.NotEqual("StrongStaticEventFlow", flow.Properties.GetValueOrDefault("flowClassification"));
        Assert.NotEqual("wcf-operation", flow.Properties.GetValueOrDefault("terminalSurfaceKind"));
        var supportingFactIds = flow.Properties.GetValueOrDefault("supportingFactIds") ?? string.Empty;
        Assert.DoesNotContain(wcfFactIds, id => supportingFactIds.Contains(id, StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_reports_no_backend_evidence_under_full_coverage()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteBasicPage(repo, "Save_Click", handlerBody: "");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventFlowProjected
            && fact.Properties.GetValueOrDefault("flowClassification") == "NoBackendEvidence"
            && fact.Properties.GetValueOrDefault("coverage") == "Full");
    }

    [Fact]
    public void Scan_scopes_unqualified_handler_evidence_to_resolved_file()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WritePage(repo, "Orders.aspx", "Orders.aspx.cs", "Sample.OrdersPage", "Save_Click");
        WritePage(repo, "Profile.aspx", "Profile.aspx.cs", "Sample.ProfilePage", "Save_Click");
        WritePageCodeBehind(repo, "Orders.aspx.cs", "OrdersPage", "Save_Click", "var sql = \"select Id from Orders\";");
        WritePageCodeBehind(repo, "Profile.aspx.cs", "ProfilePage", "Save_Click", "");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var profileFlow = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventFlowProjected
            && fact.Evidence.FilePath == "Profile.aspx.cs");
        Assert.Equal("NoBackendEvidence", profileFlow.Properties.GetValueOrDefault("flowClassification"));
    }

    [Fact]
    public void Scan_links_same_file_syntax_sql_terminal_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteBasicPage(repo, "Save_Click", handlerBody: "var sql = \"select Id from Orders\";");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.QueryPatternDetected);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventFlowProjected
            && fact.Properties.GetValueOrDefault("terminalSurfaceKind") == "sql-query"
            && fact.Properties.GetValueOrDefault("flowClassification") == "ProbableStaticEventFlow");
    }

    [Fact]
    public void Scan_does_not_emit_webforms_designer_facts_without_matching_markup()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Settings.designer.cs"), """
            namespace Sample;
            public partial class Settings
            {
                internal string Theme;
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Inventory, item => item is { RelativePath: "Settings.designer.cs", Kind: "CSharp" });
        Assert.DoesNotContain(result.Inventory, item => item is { RelativePath: "Settings.designer.cs", Kind: "WebFormsDesigner" });
        Assert.DoesNotContain(result.Facts, fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_suppresses_unsafe_markup_values_and_records_malformed_gap()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Unsafe.aspx"), """
            <%@ Page Language="C#" CodeBehind="$(UnsafeCodeBehindPath)" Inherits="Sample.Unsafe" MasterPageFile="$(UnsafeMasterPagePath)" %>
            <asp:Button runat="server" ID="SaveButton" OnClientClick="PrivateHandler" />
            """);
        File.WriteAllText(Path.Combine(repo, "Broken.aspx"), """
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var serialized = SerializeFacts(result.Facts.Where(fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal) || fact.RuleId.StartsWith("legacy.webforms", StringComparison.Ordinal)));

        Assert.DoesNotContain("UnsafeCodeBehindPath", serialized);
        Assert.DoesNotContain("UnsafeMasterPagePath", serialized);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsInventory
            && fact.Properties.GetValueOrDefault("gapKind") == "MalformedWebFormsDirective");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsEventBinding
            && fact.Properties.GetValueOrDefault("gapKind") == "UnsupportedWebFormsEventAttribute");
    }

    private static void WriteBasicPage(string repo, string handlerName, string handlerBody)
    {
        WritePage(repo, "Default.aspx", "Default.aspx.cs", "Sample.Default", handlerName);
        WriteCodeBehind(repo, handlerName, handlerBody);
    }

    private static void WritePage(string repo, string markupFileName, string codeBehindFileName, string inherits, string handlerName)
    {
        File.WriteAllText(Path.Combine(repo, markupFileName), $$"""
            <%@ Page Language="C#" CodeBehind="{{codeBehindFileName}}" Inherits="{{inherits}}" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="{{handlerName}}" />
            """);
    }

    private static void WriteCodeBehind(string repo, string handlerName, string handlerBody)
    {
        WritePageCodeBehind(repo, "Default.aspx.cs", "Default", handlerName, handlerBody);
    }

    private static void WritePageCodeBehind(string repo, string fileName, string className, string handlerName, string handlerBody)
    {
        File.WriteAllText(Path.Combine(repo, fileName), $$"""
            using System;
            namespace Sample;
            public partial class {{className}}
            {
                protected void {{handlerName}}(object sender, EventArgs e)
                {
                    {{handlerBody}}
                }
            }
            """);
    }

    private static string SerializeFacts(IEnumerable<CodeFact> facts)
    {
        return string.Join("\n", facts.Select(fact => JsonSerializer.Serialize(fact)));
    }
}
