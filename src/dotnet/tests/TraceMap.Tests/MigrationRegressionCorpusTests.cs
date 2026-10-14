using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class MigrationRegressionCorpusTests
{
    [Fact]
    public void Checked_in_corpus_preserves_complete_chains_and_prior_SQL_fixes()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        using var output = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(root.FullName, "samples", "migration-regression-corpus"), output.Path));
        var report = MarkdownReportWriter.Build(scan);
        var parameters = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseParameterCandidate).ToArray();
        Assert.Equal(8, parameters.Length);
        var saved = parameters.Where(f => f.SourceSymbol?.Contains("SaveCore", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(5, saved.Length);
        foreach (var fact in saved)
        {
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, fact.EvidenceTier);
            Assert.Equal("syntax-local-declaration-candidate", fact.Properties["commandReceiverAssociation"]);
            Assert.Equal("unresolved", fact.Properties["commandExecutionEvidence"]);
            Assert.Equal("same-receiver-text-call-observed", fact.Properties["laterExecutionSyntax"]);
            Assert.Equal("1", fact.Properties["parameterValueObservationCount"]);
            Assert.Equal("bounded-local-constant-syntax-candidate", fact.Properties["commandTextTraceEvidence"]);
            Assert.Equal(FactFactory.Hash("DELETE FROM SyntheticRows WHERE RowId = @RowId", 64), fact.Properties["commandTextTraceHash"]);
            Assert.Equal("unknown", fact.Properties["parameterDirectionEvidence"]);
        }
        var reassigned = Assert.Single(parameters, f => f.SourceSymbol?.Contains("ReassignedCommand", StringComparison.Ordinal) == true);
        Assert.Equal("not-observed", reassigned.Properties["laterExecutionSyntax"]);
        Assert.Equal("receiver-reassigned", reassigned.Properties["laterExecutionSyntaxReason"]);
        var dynamic = Assert.Single(parameters, f => f.SourceSymbol?.Contains("UnknownText", StringComparison.Ordinal) == true);
        Assert.Equal("unresolved", dynamic.Properties["commandTextTraceEvidence"]);
        Assert.False(dynamic.Properties.ContainsKey("commandTextTraceHash"));
        Assert.Single(parameters, f => f.SourceSymbol?.Contains("AliasConstructor", StringComparison.Ordinal) == true);
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.ObjectCreated && f.TargetSymbol == "Object");
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.CallEdge && f.TargetSymbol == "Object");
        Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.WebFormsHandlerResolved);
        Assert.Contains("Displayed groups with candidate entry paths: 3/8", report);
        Assert.Contains("Parameter witnesses: 5", report);
        Assert.Contains("framework assessment `explicit-usercontrol-base-not-established`", report);
        Assert.Contains("framework assessment `explicit-usercontrol-base-syntax-candidate`", report);
        Assert.Contains("4/5 VB Handles observed-event candidate anchors", report);
        var ambiguousOperations = scan.Facts.Where(f => f.FactType == FactTypes.HttpCallDetected
            && f.Evidence.FilePath == "AmbiguousEvents.vb").ToArray();
        Assert.Equal(2, ambiguousOperations.Length);
        foreach (var operation in ambiguousOperations)
        {
            Assert.Contains(operation.FactId, report);
            Assert.DoesNotContain(report.Split('\n'), line => line.StartsWith("- VB Handles observed-event", StringComparison.Ordinal)
                && line.Contains(operation.FactId, StringComparison.Ordinal));
        }
        Assert.Contains("0/0 handler-resolution facts", report);
        var bulk = Assert.Single(scan.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate
            && f.TargetSymbol?.EndsWith("WriteToServer", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(parameters, f => f.SourceSymbol == bulk.SourceSymbol);
        Assert.Contains(bulk.FactId, report);
        Assert.DoesNotContain("DELETE FROM SyntheticRows", report);
        Assert.DoesNotContain("DELETE FROM SyntheticRows", JsonSerializer.Serialize(scan.Facts));
        Assert.Equal(report, MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Reverse().ToArray() }));
    }
}
