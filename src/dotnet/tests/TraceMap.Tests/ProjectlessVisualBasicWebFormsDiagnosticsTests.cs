using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ProjectlessVisualBasicWebFormsDiagnosticsTests
{
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
}
