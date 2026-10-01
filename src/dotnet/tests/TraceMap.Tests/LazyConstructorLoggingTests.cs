using System.Text.Json;
using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class LazyConstructorLoggingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Deep_projectless_property_constructor_logging_retains_routes_and_return_value_gap(bool compiledOnly)
    {
        var repo = FindRepo();
        var source = Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-lazy-constructor");
        Assert.Empty(Directory.GetFiles(source, "*.*proj", SearchOption.AllDirectories));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bin = Path.Combine(repo, "samples", "fixture-build", "lazy-constructor", "bin", configuration, "net48");
        var assemblies = new[] { "PublicLazy.Website.dll", "PublicLazy.Framework.dll" }.Select(name => Path.Combine(bin, name)).ToArray();
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(source, Path.Combine(temp.Path, "scan"),
            CompiledInputPaths: assemblies, IlBodyEvidence: true));
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol!.Contains("LazyOverview|", StringComparison.Ordinal)
            && fact.TargetSymbol.Contains("|method:10:Load_Click|", StringComparison.Ordinal));
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var composition = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["public-lazy-corpus"]));
        var origin = Assert.Single(composition.Sources);
        var selector = new CombinedPathSymbolRoot(origin.SourceIndexId, origin.ScanId, origin.CommitSha, entry.TargetSymbol!);
        async Task<CombinedDependencyPathReport> Query(string? surface) =>
            await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
                new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: "database-api", SurfaceName: surface,
                    MaxDepth: 20, MaxPaths: 256)
                { CompiledOnly = compiledOnly, ExactFromSymbol = true, MaxTraversalWork = 100_000 },
                [selector], combinedIndex: true);

        var broad = await Query(null);
        var scalar = broad.Paths.Where(path => path.Nodes.Last().SurfaceName == "SqlCommand.ExecuteScalar").ToArray();
        Assert.Equal(5, broad.Paths.Count);
        Assert.Equal(4, scalar.Length);
        bool Type(CombinedPathNode node, string name) => node.SymbolId?.Contains($"names:{name.Length}:{name}|", StringComparison.Ordinal) == true;
        bool Method(CombinedPathNode node, string name) => node.SymbolId?.Contains($"|method:{name.Length}:{name}|", StringComparison.Ordinal) == true;
        bool Constructor(CombinedPathNode node, string name) => Type(node, name)
            && node.SymbolId!.Contains("|constructor:5:.ctor|", StringComparison.Ordinal);
        var logging = scalar.Where(path => path.Nodes.Any(node => Method(node, "InsertLog"))).ToArray();
        Assert.Equal(3, logging.Length);
        Console.WriteLine($"propertyCorpus.compiledOnly={compiledOnly};paths={broad.Paths.Count};scalar={scalar.Length};logging={logging.Length};work={broad.Summary.TraversalWorkUnits}");
        Assert.All(logging, path =>
        {
            Assert.Contains(path.Nodes, node => Constructor(node, "SyntheticChoices"));
            Assert.Contains(path.Nodes, node => Method(node, "get_EmployeeInfo"));
            Assert.Contains(path.Nodes, node => Constructor(node, "SyntheticEmployee"));
            Assert.DoesNotContain(path.Nodes, node => Type(node, "UnrelatedLog"));
            Assert.DoesNotContain(path.Nodes, node => Method(node, "BuildText"));
            var route = path.Nodes.ToList();
            Assert.True(route.FindIndex(node => Constructor(node, "SyntheticChoices")) < route.FindIndex(node => Method(node, "get_EmployeeInfo")));
            Assert.True(route.FindIndex(node => Method(node, "get_EmployeeInfo")) < route.FindIndex(node => Constructor(node, "SyntheticEmployee")));
            Assert.True(route.FindIndex(node => Constructor(node, "SyntheticEmployee")) < route.FindIndex(node => Method(node, "InsertLog")));
            var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(path.Nodes.Last().CommandBinding);
            Assert.Equal("argument-slot", binding.CommandTextOrigin.Kind);
            var text = Assert.IsType<CompiledCommandPathValueBinding>(binding.CommandTextFromPath);
            Assert.Equal("unresolved-operand", text.State);
            Assert.Equal("call-result", text.Origin.Kind);
            var producerBody = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
                && $"{origin.SourceIndexId}:{fact.FactId}" == text.OriginBodyFactId);
            var producerCall = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
                && fact.Properties.GetValueOrDefault("ilBodyFactId") == producerBody.FactId
                && fact.Properties.GetValueOrDefault("ilOffset") == text.Origin.Identity);
            Assert.Contains("|method:9:BuildText|", producerCall.Properties["targetIdentity"], StringComparison.Ordinal);
            Assert.Single(text.Steps);
            Assert.Contains("IlCommandOperandValueUnresolved", text.Gaps);
            Assert.Contains("IlCommandVirtualDispatchUnproven", text.Gaps);
            Assert.Equal("method-local-constant", binding.CommandTypeFromPath!.State);
            Assert.Equal("1", binding.CommandTypeFromPath.Origin.Identity);
            Assert.Matches("^[0-9a-f]{64}$", text.GeneratorSha256);
            Assert.Matches("^[0-9a-f]{64}$", text.BoundedInputSha256);
        });
        Assert.Contains(logging, path => path.Nodes.Any(node => Constructor(node, "SyntheticPreferences")));
        Assert.Contains(logging, path => !path.Nodes.Any(node => Constructor(node, "SyntheticPreferences")));
        Assert.Contains(scalar, path => path.Nodes.Any(node => Method(node, "InsertLiteral"))
            && path.Nodes.Last().CommandBinding?.CommandTextFromPath?.State == "constant-on-encoded-call-path");
        var fill = await Query("DbDataAdapter.Fill");
        Assert.Single(fill.Paths);
        Assert.All(fill.Paths, path =>
        {
            Assert.Equal("DbDataAdapter.Fill", path.Nodes.Last().SurfaceName);
            Assert.DoesNotContain(path.Nodes, node => Method(node, "InsertLog"));
            Assert.Equal("method-local-constant", path.Nodes.Last().CommandBinding!.CommandTextFromPath!.State);
        });
        Assert.DoesNotContain("SELECT LEN('", JsonSerializer.Serialize(broad), StringComparison.Ordinal);
        Assert.DoesNotContain(broad.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason is "path" or "work");
        Assert.InRange(broad.Summary.TraversalWorkUnits!.Value, 1, 100_000);
    }

    private static string FindRepo()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new InvalidOperationException("Repository unavailable.");
    }
}
