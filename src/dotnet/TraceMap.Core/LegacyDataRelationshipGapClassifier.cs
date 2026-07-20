namespace TraceMap.Core;

internal enum LegacyRelationshipDecision
{
    EmitRelationship,
    EmitReducedRelationship,
    EmitAnalysisGap,
    EmitNothing
}

internal enum LegacyRelationshipEndpointState
{
    Deterministic,
    Missing,
    Ambiguous,
    UnsafeRedacted,
    NotApplicable
}

internal enum LegacyRelationshipJoinOrKeyState
{
    Deterministic,
    Missing,
    Ambiguous,
    Unsupported,
    UnsafeRedacted,
    NotApplicable
}

internal enum LegacyRelationshipParserCoverageState
{
    Full,
    Reduced,
    TooLarge,
    Malformed,
    SecurityRejected,
    NotApplicable
}

[Flags]
internal enum LegacyRelationshipShapeFlags
{
    None = 0,
    DuplicateRelationshipIdentity = 1,
    UnsupportedRelationshipShape = 2,
    UnsupportedDescriptorFamily = 4
}

internal sealed record LegacyRelationshipGapInput(
    string RelationshipFamily,
    string SourceRuleId,
    string DescriptorKind,
    bool IsRelationshipDescriptor,
    LegacyRelationshipEndpointState SourceEndpointState,
    LegacyRelationshipEndpointState TargetEndpointState,
    LegacyRelationshipJoinOrKeyState JoinOrKeyState,
    LegacyRelationshipParserCoverageState ParserCoverageState,
    LegacyRelationshipShapeFlags UnsupportedShapeFlags,
    bool ExistingFamilyAllowsUnidirectional);

internal sealed record LegacyRelationshipGapDecision(
    LegacyRelationshipDecision Decision,
    string? Classification,
    string CoverageLabel,
    string? RelationshipEndpointCoverage,
    IReadOnlyList<string> Limitations,
    string EvidenceTier,
    string RuleId,
    string SafeReasonCode);

internal static class LegacyDataRelationshipGapClassifier
{
    private static readonly HashSet<string> SupportedFamilies = new(StringComparer.Ordinal)
    {
        "dbml",
        "edmx",
        "typed-dataset",
        "nhibernate-hbm"
    };

    internal static LegacyRelationshipGapDecision Classify(LegacyRelationshipGapInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.RelationshipFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SourceRuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.DescriptorKind);
        if (!SupportedFamilies.Contains(input.RelationshipFamily))
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.RelationshipFamily, "Relationship family is not cataloged.");
        }

        if (!input.IsRelationshipDescriptor)
        {
            return new LegacyRelationshipGapDecision(
                LegacyRelationshipDecision.EmitNothing,
                null,
                "full",
                null,
                [],
                EvidenceTiers.Tier2Structural,
                input.SourceRuleId,
                "not-in-scope");
        }

        if (input.ParserCoverageState is LegacyRelationshipParserCoverageState.SecurityRejected
            or LegacyRelationshipParserCoverageState.Malformed
            or LegacyRelationshipParserCoverageState.TooLarge
            or LegacyRelationshipParserCoverageState.Reduced)
        {
            return Gap(
                RuleIds.LegacyDataModelRelationship,
                "ReducedLegacyDataModelRelationshipCoverage",
                "reduced-parser-coverage",
                "reduced-parser-coverage");
        }

        if (input.UnsupportedShapeFlags.HasFlag(LegacyRelationshipShapeFlags.UnsupportedDescriptorFamily))
        {
            return Gap(
                RuleIds.LegacyDataOrmUnsupported,
                "UnsupportedLegacyOrmDescriptor",
                "unsupported-relationship-shape",
                "unsupported-relationship-shape");
        }

        if (input.UnsupportedShapeFlags.HasFlag(LegacyRelationshipShapeFlags.UnsupportedRelationshipShape)
            || input.JoinOrKeyState == LegacyRelationshipJoinOrKeyState.Unsupported)
        {
            return Gap(
                input.SourceRuleId,
                "UnsupportedLegacyOrmMappingShape",
                "unsupported-relationship-shape",
                "unsupported-relationship-shape");
        }

        if (input.UnsupportedShapeFlags.HasFlag(LegacyRelationshipShapeFlags.DuplicateRelationshipIdentity))
        {
            return Gap(
                input.SourceRuleId,
                "AmbiguousLegacyDataModelIdentity",
                "duplicate-relationship-identity",
                "duplicate-relationship-identity");
        }

        if (input.SourceEndpointState == LegacyRelationshipEndpointState.Ambiguous
            || input.TargetEndpointState == LegacyRelationshipEndpointState.Ambiguous
            || input.JoinOrKeyState == LegacyRelationshipJoinOrKeyState.Ambiguous)
        {
            return Gap(
                input.SourceRuleId,
                "AmbiguousLegacyDataModelIdentity",
                "ambiguous-endpoint-candidates",
                "ambiguous-endpoint-candidates");
        }

        var endpointMissing = input.SourceEndpointState == LegacyRelationshipEndpointState.Missing
            || input.TargetEndpointState == LegacyRelationshipEndpointState.Missing;
        if (endpointMissing || input.JoinOrKeyState == LegacyRelationshipJoinOrKeyState.Missing)
        {
            var hasDeterministicEndpoint = input.SourceEndpointState == LegacyRelationshipEndpointState.Deterministic
                || input.TargetEndpointState == LegacyRelationshipEndpointState.Deterministic;
            if (endpointMissing
                && input.JoinOrKeyState != LegacyRelationshipJoinOrKeyState.Missing
                && input.ExistingFamilyAllowsUnidirectional
                && hasDeterministicEndpoint)
            {
                return new LegacyRelationshipGapDecision(
                    LegacyRelationshipDecision.EmitReducedRelationship,
                    null,
                    "reduced",
                    "unidirectional",
                    ["missing-endpoint"],
                    EvidenceTiers.Tier2Structural,
                    input.SourceRuleId,
                    "missing-endpoint");
            }

            return Gap(
                RuleIds.LegacyDataModelRelationship,
                "IncompleteLegacyDataModelRelationship",
                "missing-endpoint",
                "missing-endpoint");
        }

        if (input.SourceEndpointState == LegacyRelationshipEndpointState.UnsafeRedacted
            || input.TargetEndpointState == LegacyRelationshipEndpointState.UnsafeRedacted
            || input.JoinOrKeyState == LegacyRelationshipJoinOrKeyState.UnsafeRedacted)
        {
            return new LegacyRelationshipGapDecision(
                LegacyRelationshipDecision.EmitReducedRelationship,
                null,
                "reduced",
                "full",
                ["unsafe-redacted-endpoint-identity"],
                EvidenceTiers.Tier2Structural,
                input.SourceRuleId,
                "unsafe-redacted-endpoint-identity");
        }

        if (input.SourceEndpointState == LegacyRelationshipEndpointState.Deterministic
            && input.TargetEndpointState == LegacyRelationshipEndpointState.Deterministic)
        {
            return new LegacyRelationshipGapDecision(
                LegacyRelationshipDecision.EmitRelationship,
                null,
                "full",
                "full",
                [],
                EvidenceTiers.Tier2Structural,
                input.SourceRuleId,
                "deterministic-relationship");
        }

        return Gap(
            RuleIds.LegacyDataModelRelationship,
            "IncompleteLegacyDataModelRelationship",
            "missing-endpoint",
            "missing-endpoint");
    }

    private static LegacyRelationshipGapDecision Gap(
        string ruleId,
        string classification,
        string safeReasonCode,
        string limitation)
    {
        return new LegacyRelationshipGapDecision(
            LegacyRelationshipDecision.EmitAnalysisGap,
            classification,
            "reduced",
            null,
            [limitation],
            EvidenceTiers.Tier4Unknown,
            ruleId,
            safeReasonCode);
    }
}
