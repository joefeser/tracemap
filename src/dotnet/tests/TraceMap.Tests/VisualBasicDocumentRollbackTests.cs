using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class VisualBasicDocumentRollbackTests
{
    [Fact]
    public void Owner_cancellation_is_not_reclassified_as_an_extractor_defect()
    {
        var gaps = new List<SemanticFactCandidate>();
        Assert.Throws<OperationCanceledException>(() => VisualBasicSemanticExtractor.ExtractDocumentAtomically(
            "File.vb", "Project.vbproj", [], gaps, null, new HashSet<string>(),
            () => throw new OperationCanceledException()));
        Assert.Empty(gaps);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_document_rolls_back_only_its_own_evidence_and_membership(bool alreadyAnalyzed)
    {
        const string file = "Shared.vb";
        var evidence = new EvidenceSpan(file, 1, 1, null, "fixture", "1");
        var existing = new SemanticFactCandidate(FactTypes.MethodInvoked, RuleIds.VisualBasicSemanticMethodInvocation,
            EvidenceTiers.Tier1Semantic, evidence);
        var metadata = new SourceMetadataIdentityCandidate("source", "metadata", "method", "visualbasic", evidence,
            "first.vbproj", "fixture", [], "declaration");
        var facts = new List<SemanticFactCandidate> { existing };
        var gaps = new List<SemanticFactCandidate>();
        var candidates = new List<SourceMetadataIdentityCandidate> { metadata };
        var analyzed = new HashSet<string>();
        if (alreadyAnalyzed) analyzed.Add(file);
        VisualBasicSemanticExtractor.ExtractDocumentAtomically(file, "second.vbproj", facts, gaps, candidates, analyzed, () =>
        {
            facts.Add(existing with { ProjectPath = "second.vbproj" });
            gaps.Add(existing);
            candidates.Add(metadata with { ProjectPath = "second.vbproj" });
            throw new ArgumentNullException("synthetic-extractor-failure");
        });
        Assert.Same(existing, Assert.Single(facts));
        Assert.Same(metadata, Assert.Single(candidates));
        var gap = Assert.Single(gaps);
        Assert.Equal("VisualBasicDocumentExtractionFailed", gap.Properties!["gapKind"]);
        Assert.Equal("retry-after-correction", ScanEngine.SemanticRetryability(true, gaps));
        Assert.Equal("retry-after-dependency-restoration", ScanEngine.SemanticRetryability(true, []));
        Assert.Equal("not-required", ScanEngine.SemanticRetryability(false, []));
        Assert.Equal(alreadyAnalyzed, analyzed.Contains(file));
        // A later successful project can still take ownership after a first failure.
        VisualBasicSemanticExtractor.ExtractDocumentAtomically(file, "third.vbproj", facts, gaps, candidates, analyzed,
            () => facts.Add(existing with { ProjectPath = "third.vbproj" }));
        Assert.Contains(file, analyzed);
        Assert.Equal(2, facts.Count);
        Assert.Single(gaps);
    }
}
