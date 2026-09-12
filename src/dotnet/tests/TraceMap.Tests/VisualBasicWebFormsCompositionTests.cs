using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class VisualBasicWebFormsCompositionTests
{
    [Fact]
    public void Semantic_event_sites_emit_resolved_handles_attach_detach_and_raise_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Events.vbproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><OptionStrict>On</OptionStrict></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Events.vb"), """
            Imports System
            Public Class Source
                Public Event Changed As EventHandler
                Public Sub Fire()
                    RaiseEvent Changed(Me, EventArgs.Empty)
                End Sub
            End Class
            Public Class Consumer
                Private WithEvents _source As New Source()
                Public Sub New()
                    AddHandler _source.Changed, AddressOf OnChanged
                    RemoveHandler _source.Changed, AddressOf OnChanged
                End Sub
                Private Sub OnChanged(sender As Object, e As EventArgs) Handles _source.Changed
                End Sub
            End Class
            """);
        Commit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var bindings = result.Facts.Where(fact => fact.FactType == FactTypes.VisualBasicEventBindingDeclared).ToArray();
        Assert.Contains(bindings, fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic && fact.Properties["wiringKind"] == "Handles");
        Assert.Contains(bindings, fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic && fact.Properties["wiringKind"] == "AddHandler" && fact.Properties["isAttach"] == "True");
        Assert.Contains(bindings, fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic && fact.Properties["wiringKind"] == "RemoveHandler" && fact.Properties["isAttach"] == "False");
        Assert.All(bindings.Where(fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic), fact =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties["sourceSymbolId"]));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties["targetSymbolId"]));
        });
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.VisualBasicEventRaised
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.ContractElement == "Changed");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.FieldDeclared
            && fact.ContractElement == "_source"
            && fact.Properties["isWithEvents"] == "True");
    }

    [Fact]
    public void Syntax_fallback_keeps_event_shapes_bounded_and_marks_unsupported_delegate()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Loose.vb"), """
            Public Class Loose
                Public Event Changed()
                Public Sub Wire(value As Object)
                    AddHandler value.Changed, AddressOf OnChanged
                    AddHandler value.Changed, Sub() value.Run()
                    RemoveHandler value.Changed, AddressOf OnChanged
                    RaiseEvent Changed()
                End Sub
                Private Sub OnChanged()
                End Sub
            End Class
            """);
        Commit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.VisualBasicEventBindingDeclared
            && fact.RuleId == RuleIds.VisualBasicSyntaxEventWiring
            && fact.Properties["wiringKind"] == "AddHandler");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.VisualBasicEventBindingDeclared
            && fact.Properties["wiringKind"] == "RemoveHandler");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.VisualBasicEventRaised
            && fact.RuleId == RuleIds.VisualBasicSyntaxEventWiring);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "UnsupportedVisualBasicEventHandlerDelegate");
    }

    [Fact]
    public void Vb_webforms_fixture_composes_shared_page_control_handler_lifecycle_and_event_facts()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(FindRepoRoot(), "samples", "vb-webforms-sample");
        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsPageDeclared && fact.ContractElement == "Default.aspx");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsDesignerControlDeclared && fact.ContractElement == "SaveButton");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.Properties.GetValueOrDefault("wiringKind") == "Handles"
            && fact.Properties["eventName"] == "OnClick"
            && fact.Properties["handlerName"] == "SaveButton_Click");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.Properties.GetValueOrDefault("wiringKind") == "AddHandler"
            && fact.Properties["handlerName"] == "RefreshButton_Click");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.Properties.GetValueOrDefault("wiringKind") == "RemoveHandler");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "SaveButton_Click"
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Properties.GetValueOrDefault("handlerSymbolId")?.StartsWith("visualbasic method ", StringComparison.Ordinal) == true);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventFlowProjected
            && fact.ContractElement == "SaveButton_Click"
            && !string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("supportingEdgeIds")));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsLifecycleBranchCandidate
            && fact.ContractElement == "NotIsPostBackBranch"
            && fact.Properties["language"] == "Visual Basic");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.VisualBasicEventRaised && fact.ContractElement == "StatusChanged");
    }

    [Fact]
    public void Late_bound_vb_webforms_event_is_a_gap_not_a_guessed_shared_binding()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Late.vbproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><OptionStrict>Off</OptionStrict></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Late.aspx"), """
            <%@ Page Language="VB" CodeFile="Late.aspx.vb" Inherits="LatePage" %>
            <form runat="server"><asp:Button ID="KnownButton" runat="server" OnClick="Markup_Click" /></form>
            """);
        File.WriteAllText(Path.Combine(repo, "Late.aspx.vb"), """
            Option Strict Off
            Partial Public Class LatePage
                Private Sub Wire(value As Object)
                    AddHandler value.Click, AddressOf Late_Click
                End Sub
                Private Sub Late_Click(sender As Object, e As EventArgs)
                End Sub
                Private Sub Markup_Click(sender As Object, e As EventArgs)
                End Sub
            End Class
            """);
        Commit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.VisualBasicSemanticEventWiring
            && fact.Properties.GetValueOrDefault("gapKind") == "LateBoundOrUnresolvedVisualBasicEvent");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.Properties.GetValueOrDefault("handlerName") == "Markup_Click"
            && fact.Properties.GetValueOrDefault("bindingKind") == "MarkupAttribute");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Markup_Click");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.Properties.GetValueOrDefault("handlerName") == "Late_Click");
    }

    [Theory]
    [InlineData("Widget.ascx", "Control")]
    [InlineData("Shell.master", "Master")]
    public void Vb_user_control_and_master_directives_join_to_linked_code_behind(string markupName, string directiveName)
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, markupName), $"""
            <%@ {directiveName} Language="VB" CodeFile="{markupName}.vb" Inherits="LegacySurface" %>
            <asp:Button ID="SubmitButton" runat="server" OnClick="SubmitButton_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, markupName + ".vb"), """
            Option Strict Off
            Partial Public Class LegacySurface
                Protected Sub SubmitButton_Click(sender As Object, e As EventArgs) Handles SubmitButton.Click
                End Sub
            End Class
            """);
        Commit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsPageDeclared
            && fact.ContractElement == markupName);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.Properties.GetValueOrDefault("handlerName") == "SubmitButton_Click");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "SubmitButton_Click");
    }

    private static void Commit(string repo)
    {
        RunGit(repo, "init");
        RunGit(repo, "add", "-A");
        RunGit(repo, "-c", "user.name=TraceMap", "-c", "user.email=tests@example.invalid", "commit", "-m", "fixture");
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = repo,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        process.WaitForExit(30_000);
        Assert.Equal(0, process.ExitCode);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "samples"))
                && (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git"))))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
