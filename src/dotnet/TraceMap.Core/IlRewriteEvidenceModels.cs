namespace TraceMap.Core;

public sealed record IlRewriteLimits(int MaxPairCount = 16)
{
    public static IlRewriteLimits Default { get; } = new();
}

public sealed record IlRewriteExpectedPair(string PairId, string BeforeSafeLocator, string AfterSafeLocator);

public sealed record IlRewritePairOutcome(
    string PairId,
    string BeforeSafeLocator,
    string AfterSafeLocator,
    string Outcome,
    string? BeforeRawFileSha256,
    string? AfterRawFileSha256,
    string? BeforeAssemblyIdentity,
    string? AfterAssemblyIdentity,
    string PrivacyProjectedPairSha256,
    IReadOnlyList<string> GapKinds,
    string? Side = null,
    string? Cause = null);

public sealed record IlRewriteProvenance(
    string SchemaVersion,
    string PolicyVersion,
    string GeneratorSha256,
    IReadOnlyList<string> ExtractorIdentities,
    IReadOnlyList<IlRewriteExpectedPair> ExpectedPairs,
    IlRewriteLimits EffectiveLimits,
    IReadOnlyList<IlRewritePairOutcome> Outcomes,
    string BoundedInputSha256,
    string ArtifactVisibility,
    string CoverageState);

internal sealed record IlRewriteCallRetarget(
    int Ordinal,
    string Opcode,
    long BeforeOffset,
    long AfterOffset,
    string BeforeToken,
    string AfterToken,
    string BeforeTargetIdentity,
    string AfterTargetIdentity);

internal sealed record IlRewriteEdge(
    string PairId,
    string MethodIdentity,
    string BeforeBodyIdentity,
    string AfterBodyIdentity,
    string BeforeBodySha256,
    string AfterBodySha256,
    string BeforeMetadataToken,
    string AfterMetadataToken,
    bool TokenRetargeted,
    string RelationshipKind,
    bool OpcodeSequencePreserved,
    IReadOnlyList<IlRewriteCallRetarget> CallRetargets);

internal sealed record IlRewriteMembershipDelta(
    string PairId,
    string Side,
    int IdentityCount,
    IReadOnlyList<string> RetainedIdentities,
    int OmittedIdentityCount,
    string? OmittedIdentitySha256);

internal sealed record IlRewriteSideFailure(
    string Side,
    string GapKind,
    string Cause,
    string SideLocator);

/// <summary>
/// In-memory retention of one admitted assembly side for the PDB sub-lane:
/// the private full path (never serialized into any provenance or fact), the
/// admitted raw SHA-256 for re-verification before reuse, and the dual-reader
/// IL body result the join already proved.
/// </summary>
internal sealed record IlRewriteSideArtifact(
    string FullPath,
    string? RawFileSha256,
    IlBodyEvidenceExtractor.IlReaderResult? Reader);

internal sealed record EvaluatedIlRewritePair(
    IlRewritePairOutcome Outcome,
    IReadOnlyList<IlRewriteEdge> Edges,
    IReadOnlyList<IlRewriteMembershipDelta> MembershipDeltas,
    IReadOnlyList<IlRewriteSideFailure> SideFailures,
    IlRewriteSideArtifact? BeforeSide = null,
    IlRewriteSideArtifact? AfterSide = null)
{
    public string PairId => Outcome.PairId;
}

internal sealed record IlRewriteEvaluation(
    IlRewriteProvenance? Provenance,
    IReadOnlyList<EvaluatedIlRewritePair> Pairs,
    IReadOnlyList<string> KnownGaps)
{
    public static readonly IlRewriteEvaluation Disabled = new(null, [], []);
}
