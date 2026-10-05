using System.Text.Json;
using Microsoft.CodeAnalysis;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class SourceMetadataReconciliationTests
{
    private const string WideOrdinals = "0,1,2,3,4,5,6,7,8,9,10";

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp.dll", LanguageNames.CSharp)]
    [InlineData("vb", "CompiledEvidence.VisualBasic.dll", LanguageNames.VisualBasic)]
    public void Optional_source_matrix_joins_bound_wide_markers_with_both_evidence_endpoints(
        string directory, string fileName, string language)
    {
        var source = Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", directory);
        var result = ScanBound(source, [Path.Combine(source, "bin", "Debug", "net10.0", fileName)]);
        var compiled = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol!.Contains("|names:13:OptionalShape|", StringComparison.Ordinal)
            && fact.Properties["metadataName"] != ".ctor").ToArray();
        Assert.Equal(4, compiled.Length);
        foreach (var member in compiled)
        {
            var expected = member.Properties["metadataName"] switch
            {
                "Required" => string.Empty,
                "Wide" => WideOrdinals,
                "OptionalSeven" or "OptionalNine" => "0",
                _ => throw new InvalidOperationException("Unexpected fixture member")
            };
            var edge = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled
                && fact.TargetSymbol == member.TargetSymbol);
            var observation = Assert.Single(result.Facts, fact => fact.FactId == edge.Properties["sourceFactId"]);
            Assert.Equal(expected, member.Properties["optionalParameterOrdinals"]);
            Assert.Equal(expected, observation.Properties["optionalParameterOrdinals"]);
            Assert.Equal(expected, edge.Properties["optionalParameterOrdinals"]);
            Assert.Equal(expected, edge.Properties["compiledOptionalParameterOrdinals"]);
            Assert.Equal("dotnet.compiled.source-identity.v1", edge.RuleId);
            Assert.Equal(EvidenceTiers.Tier1Semantic, edge.EvidenceTier);
            Assert.Equal(language, edge.Properties["sourceLanguage"]);
            Assert.Equal(observation.SourceSymbol, edge.SourceSymbol);
            Assert.NotEqual(edge.SourceSymbol, edge.TargetSymbol);
            Assert.Equal(member.FactId, edge.Properties["compiledFactId"]);
            Assert.Equal(member.CommitSha, edge.CommitSha);
            Assert.Matches("^[0-9a-f]{40}$", edge.CommitSha);
            Assert.Equal(ScannerVersions.SourceMetadataReconciliationExtractor, edge.Evidence.ExtractorVersion);
            Assert.Equal(member.Evidence.ExtractorVersion, edge.Properties["compiledExtractorVersion"]);
            Assert.Equal(observation.Evidence.FilePath, edge.Evidence.FilePath);
            Assert.Equal(observation.Evidence.StartLine, edge.Evidence.StartLine);
            Assert.True(edge.Evidence.StartLine > 0 && edge.Evidence.EndLine >= edge.Evidence.StartLine);
            Assert.Matches("^0x06[0-9a-f]{6}$", member.Properties["metadataToken"]);
            Assert.Equal(ManagedMetadataExtractor.MetadataLocationKind, member.Properties["evidenceLocationKind"]);
            Assert.Equal("bound", edge.Properties["compiledProvenanceState"]);
            foreach (var key in new[] { "boundedInputSha256", "provenanceBindingInputSha256" })
            {
                Assert.Matches("^[0-9a-f]{64}$", edge.Properties[key]);
                Assert.Equal(member.Properties[key], edge.Properties[key]);
            }
            Assert.Matches("^[0-9a-f]{64}$", edge.Properties["compiledGeneratorSha256"]);
            Assert.Equal(member.Properties["generatorSha256"], edge.Properties["compiledGeneratorSha256"]);
            Assert.Contains("does not prove runtime", edge.Properties["limitation"]);
            Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
                && fact.TargetSymbol == member.TargetSymbol);
        }
    }

    // These are deliberately injected comparator inputs, not admitted reader output.
    [Theory]
    [InlineData(WideOrdinals, 1, null)]
    [InlineData("0,1,10,2,3,4,5,6,7,8,9", 1, "SourceMetadataOptionalParameterMismatch")]
    [InlineData("0,1,2,3,4,5,6,7,8,9", 1, "SourceMetadataOptionalParameterMismatch")]
    [InlineData("0,1,2,3,4,5,6,7,8,9,10,10", 1, "SourceMetadataOptionalParameterMismatch")]
    [InlineData("not-an-ordinal", 1, "SourceMetadataOptionalParameterMismatch")]
    [InlineData(WideOrdinals, 0, "SourceMetadataReconciliationZeroCandidate")]
    [InlineData(WideOrdinals, 2, "SourceMetadataReconciliationMultipleCandidates")]
    public void Optional_source_matrix_refuses_noncanonical_missing_and_ambiguous_evidence(
        string markers, int count, string? gapKind)
    {
        var manifest = ReconciliationManifest("Level1SemanticAnalysis");
        const string identity = "assembly:test|type:test|method:Wide";
        var candidate = new SourceMetadataIdentityCandidate("source:C#|" + identity, identity, "method",
            LanguageNames.CSharp, new EvidenceSpan("Fixture.cs", 7, 9, null, "csharp-semantic", "test"),
            "Fixture.csproj", "source-declaration", [10, 2, 0, 9, 8, 7, 6, 5, 4, 3, 1], "Fixture.Wide");
        var inputs = Enumerable.Range(0, count).Select(index => FactFactory.Create(manifest,
            FactTypes.ManagedMethodDeclared, RuleIds.DotNetCompiledMember, EvidenceTiers.Tier2Structural,
            new EvidenceSpan($"fixture{index}.dll", 1, 1, null, "managed-metadata", "test"),
            targetSymbol: identity, properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["optionalParameterOrdinals"] = markers,
                ["sourceReconciliationEligibility"] = "eligible",
                ["provenanceState"] = "bound",
                ["provenanceBindingInputSha256"] = new string('d', 64)
            })).ToArray();
        var facts = SourceMetadataReconciler.Reconcile(manifest, [candidate], inputs, []);
        var repeat = SourceMetadataReconciler.Reconcile(manifest,
            [candidate with { OptionalParameterOrdinals = candidate.OptionalParameterOrdinals.Reverse().ToArray() }],
            inputs.Reverse().ToArray(), []);
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeat));
        var observation = Assert.Single(facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityObserved);
        Assert.Equal(WideOrdinals, observation.Properties["optionalParameterOrdinals"]);
        var outcome = Assert.Single(facts, fact => fact.FactType != FactTypes.SourceMetadataIdentityObserved);
        Assert.Equal("dotnet.compiled.source-identity.v1", outcome.RuleId);
        Assert.Equal(manifest.CommitSha, outcome.CommitSha);
        Assert.Equal(candidate.SourceIdentity, outcome.SourceSymbol);
        Assert.Equal(identity, outcome.TargetSymbol);
        Assert.Equal(candidate.Evidence.FilePath, outcome.Evidence.FilePath);
        Assert.Equal(7, outcome.Evidence.StartLine);
        Assert.Equal(9, outcome.Evidence.EndLine);
        Assert.Equal(ScannerVersions.SourceMetadataReconciliationExtractor, outcome.Evidence.ExtractorVersion);
        Assert.Equal(observation.FactId, outcome.Properties["sourceFactId"]);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Properties["limitation"]));
        if (gapKind is null)
        {
            Assert.Equal(FactTypes.SourceMetadataIdentityReconciled, outcome.FactType);
            Assert.Equal(EvidenceTiers.Tier1Semantic, outcome.EvidenceTier);
            Assert.Equal(WideOrdinals, outcome.Properties["optionalParameterOrdinals"]);
            Assert.Equal(inputs[0].FactId, outcome.Properties["compiledFactId"]);
        }
        else
        {
            Assert.Equal(FactTypes.AnalysisGap, outcome.FactType);
            Assert.Equal(EvidenceTiers.Tier4Unknown, outcome.EvidenceTier);
            Assert.Equal(gapKind, outcome.Properties["gapKind"]);
            Assert.Equal(count.ToString(), outcome.Properties["candidateCount"]);
            Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled);
        }
        var bounded = SourceMetadataReconciler.BuildSummary(manifest, facts, maximumEntries: 0)!;
        Assert.Empty(bounded.Entries);
        Assert.Equal(1, bounded.OmittedEntryCount);
        Assert.Matches("^[0-9a-f]{64}$", bounded.OmittedEntrySha256!);
        Assert.Equal(manifest.CompiledInputProvenance!.BoundedInputSha256, bounded.BoundedInputSha256);
        Assert.Equal(manifest.CompiledInputProvenance.GeneratorSha256, bounded.CompiledGeneratorSha256);
    }
}
