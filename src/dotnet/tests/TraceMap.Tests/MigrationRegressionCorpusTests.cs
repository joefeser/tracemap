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
        Assert.Contains("Displayed groups with candidate entry paths: 5/10", report);
        Assert.Contains("Parameter witnesses: 5", report);
        Assert.Contains("framework assessment `explicit-usercontrol-base-not-established`", report);
        Assert.Contains("framework assessment `explicit-usercontrol-base-syntax-candidate`", report);
        Assert.Contains("5/6 VB Handles observed-event candidate anchors", report);
        var nestedOperations = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate
            && f.Evidence.FilePath == "NestedReceiver.vb").ToArray();
        Assert.Equal(2, nestedOperations.Length);
        var nestedDeclaration = Assert.Single(scan.Facts, f => f.FactType == FactTypes.MethodDeclared
            && f.Properties.GetValueOrDefault("qualifiedContainingType") == "NestedService.DataStore");
        foreach (var operation in nestedOperations)
            Assert.Contains(report.Split('\n'), line => line.StartsWith("- VB Handles observed-event", StringComparison.Ordinal)
                && line.Contains(operation.FactId, StringComparison.Ordinal) && line.Contains(nestedDeclaration.FactId, StringComparison.Ordinal));
        var wrapper = Assert.Single(scan.Facts, f => f.FactType == FactTypes.MethodDeclared
            && f.Properties.GetValueOrDefault("qualifiedContainingType") == "NestedService"
            && f.Properties.GetValueOrDefault("name") == "Save");
        foreach (var guard in new[] { "importAliasNames", "methodTypeParameters", "containingTypeParameters" })
        {
            var properties = wrapper.Properties.ToDictionary(p => p.Key, p => p.Value);
            properties[guard] = "DataStore";
            var refused = MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Select(f => f == wrapper ? f with { Properties = properties } : f).ToArray() });
            Assert.Contains("Displayed groups with candidate entry paths: 3/10", refused);
        }
        var duplicate = nestedDeclaration with { FactId = "duplicate-nested-method", Evidence = nestedDeclaration.Evidence with { FilePath = "Duplicate.vb" } };
        Assert.Contains("Displayed groups with candidate entry paths: 3/10", MarkdownReportWriter.Build(scan with { Facts = [.. scan.Facts, duplicate] }));
        var nestedType = Assert.Single(scan.Facts, f => f.FactType == FactTypes.TypeDeclared && f.Properties.GetValueOrDefault("qualifiedName") == "NestedService.DataStore");
        Assert.Contains("Displayed groups with candidate entry paths: 3/10", MarkdownReportWriter.Build(scan with
        { Facts = [.. scan.Facts, nestedType with { FactId = "duplicate-type", Evidence = nestedType.Evidence with { FilePath = "Duplicate.vb" } }] }));
        var wrapperCall = Assert.Single(scan.Facts, f => f.FactType == FactTypes.CallEdge && f.Evidence.FilePath == "NestedReceiver.vb"
            && f.Properties.GetValueOrDefault("receiverName") == "store");
        foreach (var spelling in new[] { "Global.NestedService.DataStore", "" })
        {
            var properties = wrapperCall.Properties.ToDictionary(p => p.Key, p => p.Value);
            properties["receiverType"] = spelling;
            var changed = MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Select(f => f == wrapperCall ? f with { Properties = properties } : f).ToArray() });
            Assert.Contains($"Displayed groups with candidate entry paths: {(spelling.Length == 0 ? 3 : 5)}/10", changed);
        }
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
            && f.TargetSymbol?.EndsWith("WriteToServer", StringComparison.Ordinal) == true && f.Evidence.FilePath == "SqlShapes.vb");
        Assert.DoesNotContain(parameters, f => f.SourceSymbol == bulk.SourceSymbol);
        Assert.Contains(bulk.FactId, report);
        Assert.DoesNotContain("DELETE FROM SyntheticRows", report);
        Assert.DoesNotContain("DELETE FROM SyntheticRows", JsonSerializer.Serialize(scan.Facts));
        Assert.Equal(report, MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Reverse().ToArray() }));
    }

    [Fact]
    public void Nested_receiver_shadow_guards_are_extracted_from_source()
    {
        using var source = new TempDirectory();
        using var output = new TempDirectory();
        File.WriteAllText(Path.Combine(source.Path, "Guards.vb"), """
            Imports AliasStore = ExternalLibrary.Store
            Public Class Container(Of OuterT)
                Public Sub Run(Of MethodT)()
                End Sub
            End Class
            """);
        var scan = ScanEngine.Scan(new ScanOptions(source.Path, output.Path));
        var method = Assert.Single(scan.Facts, f => f.FactType == FactTypes.MethodDeclared);
        Assert.Equal("AliasStore", method.Properties["importAliasNames"]);
        Assert.Equal("OuterT", method.Properties["containingTypeParameters"]);
        Assert.Equal("MethodT", method.Properties["methodTypeParameters"]);
        Assert.Equal("vb-syntax/0.4.12", method.Evidence.ExtractorVersion);
    }
}
