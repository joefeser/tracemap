namespace TraceMap.Core;

/// <summary>
/// Bounded limits for the before/after Portable PDB side of the IL rewrite
/// lane. The file, document, method, sequence-point, text, and work bounds
/// mirror the standalone PDB input limits; source-reconciliation bounds do not
/// apply because this lane never joins PDB documents to workspace source files.
/// </summary>
public sealed record IlRewritePdbLimits(
    long MaxFileSizeBytes = 67_108_864,
    int MaxDocumentCount = 50_000,
    int MaxMethodCount = 250_000,
    int MaxSequencePointCount = 1_000_000,
    int MaxTextLength = 4_096,
    long MaxTotalWorkUnits = 1_500_000);

public sealed record IlRewritePdbExpectedPair(string PairId, string BeforePdbSafeLocator, string AfterPdbSafeLocator);

public sealed record IlRewritePdbPairOutcome(
    string PairId,
    string BeforePdbSafeLocator,
    string AfterPdbSafeLocator,
    string Outcome,
    string? BeforeRawFileSha256,
    string? AfterRawFileSha256,
    string? BeforePdbContentId,
    string? AfterPdbContentId,
    string? BeforeBindingState,
    string? AfterBindingState,
    string PrivacyProjectedPairSha256,
    IReadOnlyList<string> GapKinds,
    string? Side = null,
    string? Cause = null,
    int JoinedMethodCount = 0,
    int PdbRelationshipCount = 0,
    int OffsetsUnchangedCount = 0,
    int OffsetsChangedCount = 0,
    int MethodDebugInformationAbsentCount = 0);

public sealed record IlRewritePdbProvenance(
    string SchemaVersion,
    string PolicyVersion,
    string GeneratorSha256,
    IReadOnlyList<string> ExtractorIdentities,
    IReadOnlyList<IlRewritePdbExpectedPair> ExpectedPairs,
    IlRewritePdbLimits EffectiveLimits,
    IReadOnlyList<IlRewritePdbPairOutcome> Outcomes,
    string BoundedInputSha256,
    string ArtifactVisibility,
    string CoverageState);

/// <summary>
/// One proven before/after Portable PDB method-identity relationship for a
/// method already joined by the rewrite lane. The offset classification
/// compares IL offset vectors only; every other sequence-point component is
/// committed by the per-side digests.
/// </summary>
internal sealed record IlRewritePdbRelationship(
    string PairId,
    string MethodIdentity,
    string RewriteRelationshipKind,
    string BeforeMetadataToken,
    string AfterMetadataToken,
    string BeforeBodyIdentity,
    string AfterBodyIdentity,
    string BeforeIlBodySha256,
    string AfterIlBodySha256,
    string BeforePdbMethodIdentity,
    string AfterPdbMethodIdentity,
    int BeforeSequencePointCount,
    int AfterSequencePointCount,
    string BeforeSequencePointsSha256,
    string AfterSequencePointsSha256,
    bool SequencePointOffsetsUnchanged);

/// <summary>
/// Bounded one-side-only method debug-information delta: identities whose PDB
/// method debug information exists on exactly one side of an admitted pair.
/// </summary>
internal sealed record IlRewritePdbMethodDebugDelta(
    string PairId,
    string Side,
    int IdentityCount,
    IReadOnlyList<string> RetainedIdentities,
    int OmittedIdentityCount,
    string? OmittedIdentitySha256);

internal sealed record IlRewritePdbSideFailure(
    string Side,
    string GapKind,
    string Cause,
    string SideLocator);

internal sealed record EvaluatedIlRewritePdbPair(
    IlRewritePdbPairOutcome Outcome,
    IReadOnlyList<IlRewritePdbRelationship> Relationships,
    IReadOnlyList<IlRewritePdbMethodDebugDelta> MethodDebugDeltas,
    IReadOnlyList<IlRewritePdbSideFailure> SideFailures)
{
    public string PairId => Outcome.PairId;
}

internal sealed record IlRewritePdbEvaluation(
    IlRewritePdbProvenance? Provenance,
    IReadOnlyList<EvaluatedIlRewritePdbPair> Pairs,
    IReadOnlyList<string> KnownGaps)
{
    public static readonly IlRewritePdbEvaluation Disabled = new(null, [], []);
}
