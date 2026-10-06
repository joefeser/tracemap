using Microsoft.CodeAnalysis;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class SourceMetadataReconciliationTests
{
    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp.dll", LanguageNames.CSharp)]
    [InlineData("vb", "CompiledEvidence.VisualBasic.dll", LanguageNames.VisualBasic)]
    public void Operator_source_matrix_joins_bound_operators_conversions_and_ordinary_decoy(
        string directory, string fileName, string language)
    {
        var source = Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", directory);
        var result = ScanBound(source, [FixtureAssemblyPath(source, fileName)]);
        var methods = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol!.Contains("|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:13:OperatorShape|", StringComparison.Ordinal)
            && fact.Properties["metadataName"] != ".ctor").ToArray();
        Assert.Equal(4, methods.Length);
        foreach (var name in new[] { "op_Addition", "op_Implicit", "op_Explicit", "op_LooksLikeOperator" })
        {
            var member = Assert.Single(methods, fact => fact.Properties["metadataName"] == name);
            Assert.Equal(ManagedMetadataExtractorTests.OperatorSignature(name, directory), member.Properties["signature"]);
            var edge = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled
                && fact.TargetSymbol == member.TargetSymbol);
            var observation = Assert.Single(result.Facts, fact => fact.FactId == edge.Properties["sourceFactId"]);
            AssertMatrixSourceEndpoint(edge, observation, directory, "OperatorShape", name);
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
            Assert.Equal(observation.Evidence, edge.Evidence with
            {
                ExtractorId = observation.Evidence.ExtractorId,
                ExtractorVersion = observation.Evidence.ExtractorVersion
            });
            Assert.Equal("FixtureShapes." + (directory == "vb" ? "vb" : "cs"), edge.Evidence.FilePath);
            Assert.True(edge.Evidence.StartLine > 0 && edge.Evidence.EndLine >= edge.Evidence.StartLine);
            Assert.Equal("bound", edge.Properties["compiledProvenanceState"]);
            foreach (var key in new[] { "boundedInputSha256", "provenanceBindingInputSha256" })
            {
                Assert.Matches("^[0-9a-f]{64}$", edge.Properties[key]);
                Assert.Equal(member.Properties[key], edge.Properties[key]);
            }
            Assert.Equal(member.Properties["generatorSha256"], edge.Properties["compiledGeneratorSha256"]);
            Assert.Contains("does not prove runtime", edge.Properties["limitation"]);
            Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap && fact.TargetSymbol == member.TargetSymbol);
        }
    }
}
