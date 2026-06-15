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
    public void Scan_suppresses_unsafe_markup_values_and_records_malformed_gap()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Unsafe.aspx"), """
            <%@ Page Language="C#" CodeBehind="/Users/private/Unsafe.aspx.cs" Inherits="Sample.Unsafe" MasterPageFile="https://private.example.test/site.master" %>
            <asp:Button runat="server" ID="SaveButton" OnClientClick="PrivateHandler" />
            """);
        File.WriteAllText(Path.Combine(repo, "Broken.aspx"), """
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var serialized = SerializeFacts(result.Facts.Where(fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal) || fact.RuleId.StartsWith("legacy.webforms", StringComparison.Ordinal)));

        Assert.DoesNotContain("/Users/private", serialized);
        Assert.DoesNotContain("private.example.test", serialized);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "MalformedWebFormsDirective");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "UnsupportedWebFormsEventAttribute");
    }

    private static void WriteBasicPage(string repo, string handlerName, string handlerBody)
    {
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), $$"""
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="{{handlerName}}" />
            """);
        WriteCodeBehind(repo, handlerName, handlerBody);
    }

    private static void WriteCodeBehind(string repo, string handlerName, string handlerBody)
    {
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), $$"""
            using System;
            namespace Sample;
            public partial class Default
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
