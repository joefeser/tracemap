using System.Security.Cryptography;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class BackendExtractionSampleTests
{
    [Fact]
    public async Task Compiler_built_http_and_database_fixture_emits_evidence_without_execution()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        Assert.NotNull(repo);
        var root = Path.Combine(repo.FullName, "samples", "backend-extraction");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var assembly = Path.Combine(root, "bin", configuration, "net10.0", "BackendExtraction.Sample.dll");
        Assert.True(File.Exists(assembly));
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(root, Path.Combine(temp.Path, "scan"),
            CompiledInputPaths: [assembly], IlBodyEvidence: true));
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved);
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.DatabaseOperationCandidate);
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.HttpCallDetected);
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.HttpCallDetected
            && fact.SourceSymbol?.Contains("DynamicDestination", StringComparison.Ordinal) == true);
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.DatabaseOperationCandidate
            && fact.SourceSymbol?.Contains("UnconnectedQuery", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.HttpCallDetected
            && fact.SourceSymbol?.Contains("NameOnlyDecoy", StringComparison.Ordinal) == true);
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        using var input = File.OpenRead(index);
        var hash = Convert.ToHexStringLower(SHA256.HashData(input));
        foreach (var (rootMethod, surface, family) in new[]
            { ("RefreshClick", "http-client", "outbound-http"), ("UnconnectedQuery", "sql-query", "database") })
        {
            var report = await CombinedDependencyPathReporter.BuildReportAsync(new(index, temp.Path,
                FromSymbol: rootMethod, ToSurface: surface, MaxDepth: 16));
            var handoff = GroupedCompiledPathHandoffBuilder.Create(report, hash);
            Assert.Contains(handoff.BackendExtraction!.Operations, operation => operation.Family == family);
            var output = await GroupedCompiledPathReportWriter.WriteAsync(handoff, Path.Combine(temp.Path, rootMethod));
            Assert.Contains("Backend extraction inventory", await File.ReadAllTextAsync(output.HtmlPath));
        }
    }
}
