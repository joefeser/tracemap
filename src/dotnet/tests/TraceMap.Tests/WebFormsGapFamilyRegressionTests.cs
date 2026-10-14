using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsGapFamilyRegressionTests
{
    [Theory]
    [InlineData("IsPostBack = False", "NotIsPostBackBranch")]
    [InlineData("False = Me.IsPostBack", "NotIsPostBackBranch")]
    [InlineData("MyBase.IsPostBack <> True", "NotIsPostBackBranch")]
    [InlineData("IsPostBack <> False", "IsPostBackBranch")]
    [InlineData("True = IsPostBack", "IsPostBackBranch")]
    [InlineData("Not (Me.IsPostBack = False)", "IsPostBackBranch")]
    [InlineData("(((Not (Not IsPostBack))))", "IsPostBackBranch")]
    [InlineData("IsPostBack = flag", null)]
    [InlineData("Other.IsPostBack = False", null)]
    [InlineData("Not IsPostBack AndAlso flag", null)]
    [InlineData("IsPostBack = 0", null)]
    public void Lifecycle_boolean_shapes_are_bounded(string condition, string? expected)
    {
        using var temp = new TempDirectory();
        WritePage(temp.Path, condition);
        var facts = Extract(temp.Path);
        var branches = facts.Where(f => f.FactType == FactTypes.WebFormsLifecycleBranchCandidate).ToArray();
        if (expected is null)
        {
            Assert.Empty(branches);
            Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "UnsupportedWebFormsIsPostBackCondition");
        }
        else
        {
            var branch = Assert.Single(branches);
            Assert.Equal(expected, branch.ContractElement);
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, branch.EvidenceTier);
        }
        Assert.Equal(facts.Select(f => f.FactId), Extract(temp.Path).Select(f => f.FactId));
    }

    [Fact]
    public void Lifecycle_shadow_and_depth_limits_stay_unresolved()
    {
        using var temp = new TempDirectory();
        WritePage(temp.Path, "IsPostBack = False", "Dim IsPostBack As Boolean");
        var facts = Extract(temp.Path);
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsLifecycleBranchCandidate);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "AmbiguousWebFormsIsPostBackReceiver");
        WritePage(temp.Path, new string('(', 40) + "IsPostBack" + new string(')', 40));
        Assert.DoesNotContain(Extract(temp.Path), f => f.FactType == FactTypes.WebFormsLifecycleBranchCandidate);
    }

    [Fact]
    public void Escaped_event_receivers_retain_names_but_dynamic_receivers_do_not()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Events.vb"), """
            Public Class Events
                Sub Wire()
                    AddHandler [Class].Changed, AddressOf Changed
                    RemoveHandler [Class].Changed, AddressOf Changed
                    AddHandler GetSource().Changed, AddressOf Changed
                    AddHandler Outer.Inner.Changed, AddressOf Changed
                End Sub
                Sub Changed() Handles [Class].Changed
                End Sub
            End Class
            """);
        var facts = VisualBasicSyntaxExtractor.Extract(temp.Path, Manifest(), FileInventory.Collect(temp.Path), null);
        var bindings = facts.Where(f => f.FactType == FactTypes.VisualBasicEventBindingDeclared).ToArray();
        Assert.Equal(3, bindings.Length);
        Assert.All(bindings, f =>
        {
            Assert.Equal("Class", f.Properties["receiverName"]);
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, f.EvidenceTier);
        });
        Assert.Equal(2, facts.Count(f => f.Properties.GetValueOrDefault("gapKind") == "UnsupportedVisualBasicEventReceiver"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Config_registration_clear_and_dynamic_postback_preserve_refusals(bool clear)
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Child"));
        File.WriteAllText(Path.Combine(temp.Path, "Widget.ascx"), "<%@ Control Language=\"VB\" Inherits=\"Widget\" %>");
        File.WriteAllText(Path.Combine(temp.Path, "web.config"), """
            <configuration><system.web><pages><controls>
            <add tagPrefix="local" tagName="Widget" src="~/Widget.ascx" />
            </controls></pages></system.web></configuration>
            """);
        if (clear) File.WriteAllText(Path.Combine(temp.Path, "Child", "web.config"),
            "<configuration><system.web><pages><controls><clear /></controls></pages></system.web></configuration>");
        File.WriteAllText(Path.Combine(temp.Path, "Child", "Page.aspx"), """
            <%@ Page Language="VB" Inherits="Page" %>
            <local:Widget ID="Widget" runat="server" />
            <asp:Button ID="Save" runat="server" />
            <script>__doPostBack('Save', ''); __doPostBack(variableTarget, '');</script>
            """);
        var facts = Extract(temp.Path);
        Assert.Equal(clear, facts.Any(f => f.Properties.GetValueOrDefault("gapKind") == "UnresolvedWebFormsControlRegistration"));
        Assert.Single(facts, f => f.FactType == FactTypes.WebFormsPostBackTargetCandidate);
        Assert.Single(facts, f => f.Properties.GetValueOrDefault("gapKind") == "DynamicWebFormsPostBackTarget");
    }

    private static void WritePage(string root, string condition, string declaration = "")
    {
        File.WriteAllText(Path.Combine(root, "Page.aspx"), "<%@ Page Language=\"VB\" CodeFile=\"Page.vb\" Inherits=\"SamplePage\" %>");
        File.WriteAllText(Path.Combine(root, "Page.vb"), $"""
            Public Class SamplePage
                Sub Page_Load()
                    {declaration}
                    If {condition} Then
                    End If
                End Sub
            End Class
            """);
    }

    private static ScanManifest Manifest() => new("gap-families", "synthetic", null, "main", "abc123", "test/1.0",
        DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);

    private static IReadOnlyList<CodeFact> Extract(string root) =>
        LegacyWebFormsExtractor.Extract(root, Manifest(), FileInventory.Collect(root), []);
}
