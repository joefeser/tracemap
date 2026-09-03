using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsBoundedCoverageTests
{
    [Theory]
    [InlineData("calendar", "Sample.Controls", "Sample.Widgets", false, null)]
    [InlineData("CALENDAR", "Sample.Controls", "Sample.Widgets", false, null)]
    [InlineData("Calendar", "sample.Controls", "Sample.Widgets", false, "WebFormsAssemblyTypeUnavailable")]
    [InlineData("Calendar", "Sample.Controls", "Other.Widgets", false, "WebFormsAssemblyProjectUnavailable")]
    [InlineData("calendar", "Sample.Controls", "Sample.Widgets", true, "AmbiguousWebFormsAssemblyControlRegistration")]
    [InlineData("Calendar", "Sample.Controls", "Sample.Widgets", true, "AmbiguousWebFormsAssemblyControlRegistration")]
    public void Markup_type_matching_is_case_bounded_and_collision_safe(
        string tag, string namespaceName, string assemblyName, bool collision, string? expectedGap)
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteLegacyProject(repo, "Controls.cs");
        File.WriteAllText(Path.Combine(repo, "Controls.cs"),
            "namespace Sample.Controls { public class Calendar {} " + (collision ? "public class calendar {}" : "") + " }");
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), $$"""
            <%@ Page Language="C#" Inherits="Sample.Default" %>
            <%@ Register TagPrefix="widgets" Namespace="{{namespaceName}}" Assembly="{{assemblyName}}" %>
            <widgets:{{tag}} runat="server" ID="Control" />
            """);
        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        Assert.NotEqual("Succeeded", result.Manifest.BuildStatus);
        var compositions = result.Facts.Where(f => f.FactType == FactTypes.WebFormsCompositionDeclared
            && f.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredAssemblyControl").ToArray();
        if (expectedGap is null)
        {
            Assert.Equal("Sample.Controls.Calendar", Assert.Single(compositions).TargetSymbol);
            Assert.DoesNotContain(result.Facts, f => f.RuleId == RuleIds.LegacyWebFormsComposition && f.FactType == FactTypes.AnalysisGap);
        }
        else
        {
            Assert.Empty(compositions);
            Assert.Contains(result.Facts, f => f.RuleId == RuleIds.LegacyWebFormsComposition
                && f.Properties.GetValueOrDefault("gapKind") == expectedGap);
        }
    }

    [Theory]
    [InlineData("IsPostBack", "", "is-postback-syntax", null)]
    [InlineData("this.IsPostBack", "", "is-postback-syntax", null)]
    [InlineData("(IsPostBack)", "", "is-postback-syntax", null)]
    [InlineData("!IsPostBack", "", "not-is-postback-syntax", null)]
    [InlineData("!this.IsPostBack", "", "not-is-postback-syntax", null)]
    [InlineData("IsPostBack && Other", "", null, "UnsupportedWebFormsIsPostBackCondition")]
    [InlineData("IsPostBack == true", "", null, "UnsupportedWebFormsIsPostBackCondition")]
    [InlineData("other.IsPostBack", "", null, "UnsupportedWebFormsIsPostBackCondition")]
    [InlineData("IsPostBack", "bool IsPostBack = true;", null, "AmbiguousWebFormsIsPostBackReceiver")]
    public void Postback_polarity_remains_syntax_only_and_fail_closed(
        string condition, string declarations, string? context, string? expectedGap)
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteLegacyProject(repo, "Default.aspx.cs");
        File.WriteAllText(Path.Combine(repo, "Default.aspx"),
            "<%@ Page Language=\"C#\" CodeBehind=\"Default.aspx.cs\" Inherits=\"Sample.Default\" %>");
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), $$"""
            namespace Sample;
            public partial class Default {
                protected void Page_Load() {
                    {{declarations}}
                    if ({{condition}}) { ClientScript.RegisterStartupScript(GetType(), "key", "literal-script", true); }
                }
            }
            """);
        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        Assert.NotEqual("Succeeded", result.Manifest.BuildStatus);
        var branches = result.Facts.Where(f => f.FactType == FactTypes.WebFormsLifecycleBranchCandidate).ToArray();
        if (context is not null)
        {
            var branch = Assert.Single(branches);
            Assert.Equal(context, branch.Properties.GetValueOrDefault("branchContext"));
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, branch.EvidenceTier);
            Assert.Equal(RuleIds.LegacyWebFormsLifecycleContext, branch.RuleId);
        }
        else
        {
            Assert.Empty(branches);
            Assert.Contains(result.Facts, f => f.RuleId == RuleIds.LegacyWebFormsLifecycleContext
                && f.Properties.GetValueOrDefault("gapKind") == expectedGap);
        }
        if (context != "not-is-postback-syntax")
        {
            Assert.DoesNotContain(result.Facts, f => f.FactType == FactTypes.WebFormsClientScriptRegistrationCandidate
                && f.Properties.GetValueOrDefault("branchContext") == "inside-not-is-postback-syntax");
        }
        var again = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-again")));
        Assert.Equal(JsonSerializer.Serialize(result.Facts.Where(f => f.RuleId == RuleIds.LegacyWebFormsLifecycleContext)),
            JsonSerializer.Serialize(again.Facts.Where(f => f.RuleId == RuleIds.LegacyWebFormsLifecycleContext)));
    }

    [Fact]
    public void Client_and_non_identifier_values_are_distinct_gaps_not_server_handlers()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteLegacyProject(repo, "Default.aspx.cs");
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:Button runat="server" ID="A" OnClientClick="PrivateClientIdentifier" />
            <asp:Button runat="server" ID="B" OnClick="privateCall('privateArgument')" />
            <asp:Button runat="server" ID="C" OnClick="Save_Click" />
            <asp:Button runat="server" ID="D" On="PrivateMalformedValue" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"),
            "namespace Sample; public partial class Default { protected void Save_Click(object sender, System.EventArgs e) {} }");
        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        foreach (var kind in new[] { "ClientWebFormsEventAttribute", "NonIdentifierWebFormsEventValue", "UnsupportedWebFormsEventAttribute" })
        {
            var gap = Assert.Single(result.Facts, f => f.Properties.GetValueOrDefault("gapKind") == kind);
            Assert.Equal(RuleIds.LegacyWebFormsEventBinding, gap.RuleId);
            Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        }
        var binding = Assert.Single(result.Facts, f => f.FactType == FactTypes.WebFormsEventBindingDeclared);
        Assert.Equal("OnClick", binding.Properties.GetValueOrDefault("eventName"));
        var serialized = JsonSerializer.Serialize(result.Facts.Where(f => f.RuleId.StartsWith("legacy.webforms", StringComparison.Ordinal)));
        Assert.DoesNotContain("PrivateClientIdentifier", serialized);
        Assert.DoesNotContain("privateArgument", serialized);
        Assert.DoesNotContain("PrivateMalformedValue", serialized);
    }

    private static void WriteLegacyProject(string repo, string source) =>
        File.WriteAllText(Path.Combine(repo, "Legacy.csproj"), $$"""
            <Project ToolsVersion="4.0">
              <PropertyGroup><TargetFrameworkVersion>v4.5</TargetFrameworkVersion><AssemblyName>Sample.Widgets</AssemblyName></PropertyGroup>
              <ItemGroup><Compile Include="{{source}}" /><Compile Include="Missing.Generated.cs" /></ItemGroup>
            </Project>
            """);
}
