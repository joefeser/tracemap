using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class LegacyDataRelationshipGapClassifierTests
{
    [Fact]
    public void Classifier_preserves_full_and_existing_unidirectional_relationship_policies()
    {
        var full = Classify();
        var reduced = Classify(target: LegacyRelationshipEndpointState.Missing);
        var gap = Classify(
            target: LegacyRelationshipEndpointState.Missing,
            existingFamilyAllowsUnidirectional: false);
        var missingJoin = Classify(join: LegacyRelationshipJoinOrKeyState.Missing);

        Assert.Equal(LegacyRelationshipDecision.EmitRelationship, full.Decision);
        Assert.Equal("full", full.CoverageLabel);
        Assert.Equal("full", full.RelationshipEndpointCoverage);
        Assert.Equal("deterministic-relationship", full.SafeReasonCode);
        Assert.Equal(EvidenceTiers.Tier2Structural, full.EvidenceTier);

        Assert.Equal(LegacyRelationshipDecision.EmitReducedRelationship, reduced.Decision);
        Assert.Equal("reduced", reduced.CoverageLabel);
        Assert.Equal("unidirectional", reduced.RelationshipEndpointCoverage);
        Assert.Equal("missing-endpoint", reduced.SafeReasonCode);
        Assert.Equal(LegacyRelationshipDecision.EmitAnalysisGap, gap.Decision);
        Assert.Equal("IncompleteLegacyDataModelRelationship", gap.Classification);
        Assert.Equal(RuleIds.LegacyDataModelRelationship, gap.RuleId);
        Assert.Equal(LegacyRelationshipDecision.EmitAnalysisGap, missingJoin.Decision);
        Assert.Equal("missing-endpoint", missingJoin.SafeReasonCode);
    }

    [Fact]
    public void Classifier_returns_closed_gap_vocabulary_for_shape_flags()
    {
        var cases = new[]
        {
            (LegacyRelationshipShapeFlags.DuplicateRelationshipIdentity, "AmbiguousLegacyDataModelIdentity", "duplicate-relationship-identity"),
            (LegacyRelationshipShapeFlags.UnsupportedRelationshipShape, "UnsupportedLegacyOrmMappingShape", "unsupported-relationship-shape"),
            (LegacyRelationshipShapeFlags.UnsupportedDescriptorFamily, "UnsupportedLegacyOrmDescriptor", "unsupported-relationship-shape")
        };

        foreach (var (flags, classification, reason) in cases)
        {
            var result = Classify(flags: flags);

            Assert.Equal(LegacyRelationshipDecision.EmitAnalysisGap, result.Decision);
            Assert.Equal(classification, result.Classification);
            Assert.Equal(reason, result.SafeReasonCode);
            Assert.Equal("reduced", result.CoverageLabel);
            Assert.Equal(EvidenceTiers.Tier4Unknown, result.EvidenceTier);
        }
    }

    [Fact]
    public void Classifier_distinguishes_ambiguous_reduced_unsafe_and_not_in_scope_conditions()
    {
        var ambiguous = Classify(target: LegacyRelationshipEndpointState.Ambiguous);
        var reducedParser = Classify(parser: LegacyRelationshipParserCoverageState.Reduced);
        var unsafeEndpoint = Classify(target: LegacyRelationshipEndpointState.UnsafeRedacted);
        var notInScope = Classify(
            isRelationshipDescriptor: false,
            source: LegacyRelationshipEndpointState.NotApplicable,
            target: LegacyRelationshipEndpointState.NotApplicable,
            join: LegacyRelationshipJoinOrKeyState.NotApplicable,
            parser: LegacyRelationshipParserCoverageState.SecurityRejected,
            flags: LegacyRelationshipShapeFlags.UnsupportedRelationshipShape);

        Assert.Equal("ambiguous-endpoint-candidates", ambiguous.SafeReasonCode);
        Assert.Equal("reduced-parser-coverage", reducedParser.SafeReasonCode);
        Assert.Equal(LegacyRelationshipDecision.EmitReducedRelationship, unsafeEndpoint.Decision);
        Assert.Equal("unsafe-redacted-endpoint-identity", unsafeEndpoint.SafeReasonCode);
        Assert.Equal("full", unsafeEndpoint.RelationshipEndpointCoverage);
        Assert.Equal(LegacyRelationshipDecision.EmitNothing, notInScope.Decision);
        Assert.Equal("not-in-scope", notInScope.SafeReasonCode);
        Assert.Null(notInScope.Classification);
    }

    [Fact]
    public void Classifier_applies_stable_precedence_for_overlapping_conditions()
    {
        var input = Input(
            target: LegacyRelationshipEndpointState.Ambiguous,
            parser: LegacyRelationshipParserCoverageState.SecurityRejected,
            flags: LegacyRelationshipShapeFlags.UnsupportedDescriptorFamily
                | LegacyRelationshipShapeFlags.UnsupportedRelationshipShape
                | LegacyRelationshipShapeFlags.DuplicateRelationshipIdentity);

        var first = LegacyDataRelationshipGapClassifier.Classify(input);
        var second = LegacyDataRelationshipGapClassifier.Classify(input);

        Assert.Equal(first.Decision, second.Decision);
        Assert.Equal(first.Classification, second.Classification);
        Assert.Equal(first.CoverageLabel, second.CoverageLabel);
        Assert.Equal(first.RelationshipEndpointCoverage, second.RelationshipEndpointCoverage);
        Assert.Equal(first.Limitations, second.Limitations);
        Assert.Equal(first.EvidenceTier, second.EvidenceTier);
        Assert.Equal(first.RuleId, second.RuleId);
        Assert.Equal(first.SafeReasonCode, second.SafeReasonCode);
        Assert.Equal("ReducedLegacyDataModelRelationshipCoverage", first.Classification);
        Assert.Equal("reduced-parser-coverage", first.SafeReasonCode);
        Assert.Equal(["reduced-parser-coverage"], first.Limitations);
        Assert.Equal(RuleIds.LegacyDataModelRelationship, first.RuleId);
    }

    [Fact]
    public void Classifier_rejects_uncataloged_relationship_families()
    {
        var input = Input() with { RelationshipFamily = "future-runtime-family" };

        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyDataRelationshipGapClassifier.Classify(input));
    }

    private static LegacyRelationshipGapDecision Classify(
        bool isRelationshipDescriptor = true,
        LegacyRelationshipEndpointState source = LegacyRelationshipEndpointState.Deterministic,
        LegacyRelationshipEndpointState target = LegacyRelationshipEndpointState.Deterministic,
        LegacyRelationshipJoinOrKeyState join = LegacyRelationshipJoinOrKeyState.NotApplicable,
        LegacyRelationshipParserCoverageState parser = LegacyRelationshipParserCoverageState.Full,
        LegacyRelationshipShapeFlags flags = LegacyRelationshipShapeFlags.None,
        bool existingFamilyAllowsUnidirectional = true)
    {
        return LegacyDataRelationshipGapClassifier.Classify(Input(
            isRelationshipDescriptor,
            source,
            target,
            join,
            parser,
            flags,
            existingFamilyAllowsUnidirectional));
    }

    private static LegacyRelationshipGapInput Input(
        bool isRelationshipDescriptor = true,
        LegacyRelationshipEndpointState source = LegacyRelationshipEndpointState.Deterministic,
        LegacyRelationshipEndpointState target = LegacyRelationshipEndpointState.Deterministic,
        LegacyRelationshipJoinOrKeyState join = LegacyRelationshipJoinOrKeyState.NotApplicable,
        LegacyRelationshipParserCoverageState parser = LegacyRelationshipParserCoverageState.Full,
        LegacyRelationshipShapeFlags flags = LegacyRelationshipShapeFlags.None,
        bool existingFamilyAllowsUnidirectional = true)
    {
        return new LegacyRelationshipGapInput(
            "dbml",
            RuleIds.LegacyDataDbml,
            "association",
            isRelationshipDescriptor,
            source,
            target,
            join,
            parser,
            flags,
            existingFamilyAllowsUnidirectional);
    }
}
