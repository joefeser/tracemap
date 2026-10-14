using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class StackedMigrationRegressionTests
{
    [Fact]
    public void Complete_stacked_chain_is_retained_and_each_missing_layer_breaks_it()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        using var output = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(root.FullName, "samples", "stacked-migration-regression"), output.Path));
        string Report(IEnumerable<CodeFact> facts) => MarkdownReportWriter.Build(scan with { Facts = facts.ToArray() });
        var report = Report(scan.Facts);
        Assert.Contains("Displayed groups with candidate entry paths: 4/4", report);
        Assert.Contains("Parameter witnesses: 5", report);
        Assert.Contains("Parameter facts not displayed in a group: 0", report);
        Assert.Contains("receiver declarations", report);
        Assert.Contains("explicit-usercontrol-base-not-established", report);
        Assert.DoesNotContain("search limited: True", report);
        Assert.DoesNotContain("DELETE FROM SyntheticRows", report);
        Assert.Equal(report, Report(scan.Facts.Reverse()));
        var parameters = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseParameterCandidate).ToArray();
        Assert.Equal(5, parameters.Length);
        Assert.All(parameters, p =>
        {
            Assert.Equal("bounded-local-constant-syntax-candidate", p.Properties["commandTextTraceEvidence"]);
            Assert.Equal("1", p.Properties["parameterValueObservationCount"]);
            Assert.Equal("unresolved", p.Properties["commandExecutionEvidence"]);
        });
        Assert.Contains("Displayed groups with candidate entry paths: 2/4",
            Report(scan.Facts.Where(f => f.FactType != FactTypes.PropertyDeclared)));
        foreach (var name in new[] { "Save", "WriteRows" })
            Assert.Contains("Displayed groups with candidate entry paths: 2/4",
                Report(scan.Facts.Where(f => !(f.FactType == FactTypes.CallEdge && f.Properties.GetValueOrDefault("calleeName") == name))));
        var withoutOptional = scan.Facts.Select(f => f with { Properties = f.Properties.Where(p => p.Key != "parameterOptionalFlags").ToDictionary(p => p.Key, p => p.Value) });
        Assert.Contains("Displayed groups with candidate entry paths: 1/4", Report(withoutOptional));
        Assert.Contains("Displayed groups with candidate entry paths: 0/4",
            Report(scan.Facts.Where(f => f.FactType != FactTypes.VisualBasicEventBindingDeclared)));
    }
}
