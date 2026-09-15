using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ProjectlessVisualBasicWebFormsDiagnosticsTests
{
    [Fact]
    public async Task Projectless_vb_webforms_handler_reaches_explicit_data_adapter_fill_boundary()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="VB" CodeFile="Default.aspx.vb" Inherits="DefaultPage" %>
            <form runat="server">
              <asp:Button ID="LoadButton" runat="server" OnClick="LoadButton_Click" Text="Load" />
            </form>
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.vb"), """
            Imports System
            Imports System.Data
            Imports System.Data.SqlClient

            Public Partial Class DefaultPage
                Inherits System.Web.UI.Page

                Protected Sub LoadButton_Click(sender As Object, e As EventArgs)
                    Dim adapter As SqlDataAdapter = New SqlDataAdapter()
                    adapter.Fill(New DataSet())
                End Sub
            End Class
            """);
        Commit(repo);

        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "scan")));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "packet")));

        Assert.Equal("Level3SyntaxAnalysis", scan.Manifest.AnalysisLevel);
        var operation = Assert.Single(scan.Facts, fact =>
            fact.FactType == FactTypes.DatabaseOperationCandidate
            && fact.RuleId == RuleIds.VisualBasicSyntaxDatabaseOperation);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, operation.EvidenceTier);

        var chain = Assert.Single(packet.EventChains, item => item.HandlerSymbol == "DefaultPage.LoadButton_Click");
        Assert.Equal("sql-query", chain.TerminalKind);
        Assert.Equal("none", chain.NextEvidenceKind);

        var boundary = Assert.Single(packet.DownstreamBoundaries, item => item.ChainId == chain.ChainId);
        Assert.Equal("database", boundary.BoundaryCategory);
        Assert.Equal("sql-query", boundary.BoundaryKind);
        Assert.Contains(RuleIds.VisualBasicSyntaxDatabaseOperation, boundary.RuleIds);
        Assert.Contains(EvidenceTiers.Tier3SyntaxOrTextual, boundary.EvidenceTiers);
        Assert.Contains("reduced-syntax-vb-database-operation", boundary.CoverageLabels);
    }

    [Fact]
    public async Task Projectless_vb_handlers_retain_parameter_count_suffixed_syntax_calls()
    {
        var repo = Path.Combine(FindRepoRoot(), "samples", "vb-projectless-webforms-sample");
        Assert.Empty(Directory.EnumerateFiles(repo, "*.vbproj", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(repo, "*.sln", SearchOption.AllDirectories));
        using var temp = new TempDirectory();

        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "scan")));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "packet")));

        Assert.Equal("Level3SyntaxAnalysis", scan.Manifest.AnalysisLevel);
        Assert.Contains(packet.ClientBehaviorInventory, behavior => behavior.BehaviorKind == "client-event-binding");

        var search = Assert.Single(packet.EventChains, chain => chain.HandlerSymbol == "ReviewBoard.SearchButton_Click");
        Assert.Equal(2, search.CallEvidenceTotalCount);
        Assert.Contains(search.CallEvidence, call => call.CalleeName == "LoadQueue");
        Assert.Contains(search.CallEvidence, call => call.CalleeName == "DataBind");
        Assert.Equal("observed-downstream-without-supported-terminal", search.TraversalObservation?.StopState);

        var approve = Assert.Single(packet.EventChains, chain => chain.HandlerSymbol == "ReviewBoard.ApproveButton_Click");
        Assert.Equal(3, approve.CallEvidenceTotalCount);
        Assert.Contains(approve.CallEvidence, call => call.CalleeName == "ExtractValuesFromItem");
        Assert.Contains(approve.CallEvidence, call => call.CalleeName == "Save");
        Assert.Equal("observed-downstream-without-supported-terminal", approve.TraversalObservation?.StopState);

        var uiOnly = Assert.Single(packet.EventChains, chain => chain.HandlerSymbol == "ReviewBoard.ToggleButton_Click");
        Assert.Empty(uiOnly.CallEvidence);
        Assert.Equal(0, uiOnly.CallEvidenceTotalCount);
        Assert.Equal("no-observed-downstream-edge", uiOnly.TraversalObservation?.StopState);

        Assert.Equal(2, packet.Gaps.Count(gap => gap.Classification == "DownstreamWithoutSupportedTerminal"));
        Assert.Single(packet.Gaps, gap => gap.Classification == "NoBackendEvidence");
        Assert.DoesNotContain(packet.Gaps, gap => gap.Classification == "EvidenceCoverageLabelUnavailable");
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))
                || File.Exists(Path.Combine(current.FullName, "TraceMap.slnx"))
                || Directory.Exists(Path.Combine(current.FullName, ".kiro")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
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
}
