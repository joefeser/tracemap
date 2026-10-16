using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqlWrapperRouteTests
{
    [Theory]
    [InlineData("both", "Save", true)]
    [InlineData("both", "Search", true)]
    [InlineData("caller-only", "Save", false)]
    [InlineData("provider-only", "Save", false)]
    [InlineData("source-only", "Save", false)]
    public async Task Compiled_caller_and_separate_wrapper_require_both_inputs(string mode, string method, bool expected)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        var sample = Path.Combine(root!.FullName, "samples/sql-database-matrix");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var binaries = Path.Combine(sample, "wrapper-build/bin", configuration, "net472");
        var inputs = mode switch
        {
            "both" => new[] { Path.Combine(binaries, "SyntheticWebsite.dll"), Path.Combine(binaries, "SyntheticStore.dll") },
            "caller-only" => new[] { Path.Combine(binaries, "SyntheticWebsite.dll") },
            "provider-only" => new[] { Path.Combine(binaries, "SyntheticStore.dll") },
            _ => Array.Empty<string>()
        };
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(sample, "wrapper-website"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths:inputs, IlBodyEvidence:true));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var rootFact = scan.Facts.SingleOrDefault(f => f.FactType == FactTypes.ManagedMethodDeclared
            && f.Properties.GetValueOrDefault("metadataName") == method);
        var options = new CombinedDependencyPathOptions(index, temp.Path,
            FromSymbol:rootFact?.TargetSymbol ?? "CatalogPage." + method, ToSurface:"database-api")
            { ExactFromSymbol = rootFact is not null };
        var report = await CombinedDependencyPathReporter.BuildReportAsync(options);
        if (!expected)
        {
            Assert.Empty(report.Paths);
            Assert.NotEmpty(report.Gaps);
            return;
        }
        var path = Assert.Single(report.Paths);
        Assert.Equal(method == "Save" ? 4 : 3, path.Nodes.Count);
        Assert.Equal(path.Nodes.Count - 1, path.Edges.Count);
        for (var hop = 0; hop < path.Edges.Count; hop++)
        {
            Assert.Equal(path.Nodes[hop].NodeId, path.Edges[hop].FromNodeId);
            Assert.Equal(path.Nodes[hop + 1].NodeId, path.Edges[hop].ToNodeId);
        }
        Assert.Equal(rootFact!.TargetSymbol, path.Nodes[0].SymbolId);
        Assert.Contains(path.Nodes, n => n.SymbolId?.Contains("|method:4:Save|", StringComparison.Ordinal) == true
            || n.SymbolId?.Contains("|method:6:Search|", StringComparison.Ordinal) == true);
        var wrapper = method == "Save" ? "ExecuteProcedure" : "ExecuteText";
        Assert.Contains(path.Nodes, n => n.SymbolId?.Contains($"|method:{wrapper.Length}:{wrapper}|", StringComparison.Ordinal) == true);
        var terminal = path.Nodes.Last();
        Assert.Equal(method == "Save" ? "SqlCommand.ExecuteNonQuery" : "SqlCommand.ExecuteScalar", terminal.SurfaceName);
        Assert.NotNull(terminal.CommandBinding);
        if (method == "Save")
        {
            Assert.Equal("4", terminal.CommandBinding.CommandTypeFromPath!.Origin.Identity);
            Assert.Equal("constant-on-encoded-call-path", terminal.CommandBinding.CommandTextFromPath!.State);
            Assert.Equal(2, terminal.CommandBinding.CommandTextFromPath.Steps.Count);
        }
        else
        {
            Assert.Equal("symbolic-string-composition", terminal.CommandBinding.CommandTextFromPath!.State);
        }
        var handoff = GroupedCompiledPathHandoffBuilder.Create(report,
            Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(index))));
        Assert.Contains(handoff.BackendExtraction!.Operations, o => o.Family == "database");
        var output = await GroupedCompiledPathReportWriter.WriteAsync(handoff, Path.Combine(temp.Path, "report"));
        Assert.Contains("Backend extraction inventory", await File.ReadAllTextAsync(output.HtmlPath));
        Assert.DoesNotContain("PublicCatalog_Save", JsonSerializer.Serialize(handoff));
    }
}
