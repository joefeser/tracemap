namespace TraceMap.Core;

public sealed record PdbInputLimits(
    int MaxArtifactCount = 32,
    long MaxFileSizeBytes = 67_108_864,
    int MaxDocumentCount = 50_000,
    int MaxMethodCount = 250_000,
    int MaxSequencePointCount = 1_000_000,
    int MaxTextLength = 4_096,
    long MaxTotalWorkUnits = 1_500_000);

public sealed record PdbExpectedInput(string SafeLocator);

public sealed record PdbInputOutcome(
    string SafeLocator,
    string Outcome,
    string Format,
    string? RawFileSha256,
    string PrivacyProjectedInputSha256,
    string? PdbContentId,
    string? MatchedAssemblySafeLocator,
    string? MatchedAssemblyIdentity,
    string? ProvenanceBindingInputSha256,
    string BindingState,
    IReadOnlyList<string> GapKinds);

public sealed record PdbInputProvenance(
    string SchemaVersion,
    string PolicyVersion,
    string GeneratorSha256,
    IReadOnlyList<string> ExtractorIdentities,
    IReadOnlyList<PdbExpectedInput> ExpectedInputs,
    PdbInputLimits EffectiveLimits,
    IReadOnlyList<PdbInputOutcome> Outcomes,
    string BoundedInputSha256,
    string ArtifactVisibility,
    string CoverageState,
    int OmittedInputCount = 0,
    string? OmittedInputSha256 = null);

public sealed record PdbEvidenceSummaryEntry(
    string FactType,
    string SourceIdentity,
    string TargetIdentity,
    string EvidenceFactId,
    IReadOnlyList<string> SupportingFactIds,
    string RuleId,
    string EvidenceTier,
    string ExtractorVersion,
    string ProvenanceState,
    string ProvenanceBindingInputSha256,
    string Limitation);

public sealed record PdbEvidenceSummary(
    string SchemaVersion,
    string CoverageState,
    string BoundedInputSha256,
    string GeneratorSha256,
    int DocumentCount,
    int MethodCount,
    int SequencePointCount,
    int MetadataMethodJoinCount,
    int SourceDocumentJoinCount,
    int ExplicitGapCount,
    IReadOnlyList<PdbEvidenceSummaryEntry> Entries,
    int OmittedEntryCount,
    string? OmittedEntrySha256);

internal sealed record PdbDocumentObservation(
    int RowId,
    string Identity,
    string NameHash,
    string HashAlgorithm,
    string Checksum,
    string Language);

internal sealed record PdbSequencePointObservation(
    int Ordinal,
    int Offset,
    int DocumentRowId,
    bool Hidden,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    string Identity);

internal sealed record PdbMethodObservation(
    int MethodRowId,
    string MetadataToken,
    string Identity,
    IReadOnlyList<PdbSequencePointObservation> SequencePoints);

internal sealed record EvaluatedPdbInput(
    PdbInputOutcome Outcome,
    IReadOnlyList<PdbDocumentObservation> Documents,
    IReadOnlyList<PdbMethodObservation> Methods);

internal sealed record PdbInputEvaluation(
    PdbInputProvenance? Provenance,
    IReadOnlyList<EvaluatedPdbInput> Inputs,
    IReadOnlyList<string> KnownGaps)
{
    public static readonly PdbInputEvaluation Disabled = new(null, [], []);
}
