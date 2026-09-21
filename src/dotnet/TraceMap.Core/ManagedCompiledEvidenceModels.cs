namespace TraceMap.Core;

public sealed record CompiledInputLimits(
    int MaxArtifactCount = 32,
    long MaxFileSizeBytes = 67_108_864,
    int MaxTypeCount = 50_000,
    int MaxMemberCount = 250_000,
    int MaxTextLength = 4_096,
    long MaxTotalWorkUnits = 500_000);

public sealed record CompiledExpectedInput(
    string SafeLocator,
    string Role);

public sealed record CompiledInputOutcome(
    string SafeLocator,
    string Role,
    string Outcome,
    string ProvenanceState,
    string? RawFileSha256,
    string? PrivacyProjectedInputSha256,
    string ProvenanceBindingInputSha256,
    string? AssemblyIdentity,
    string? ModuleName,
    string? ModuleMvid,
    IReadOnlyList<string> GapKinds,
    IReadOnlyList<string> DependencyResolutionOutcomes);

public sealed record CompiledInputProvenance(
    string SchemaVersion,
    string PolicyVersion,
    string GeneratorSha256,
    IReadOnlyList<string> ExtractorIdentities,
    IReadOnlyList<CompiledExpectedInput> ExpectedInputs,
    CompiledInputLimits EffectiveLimits,
    IReadOnlyList<CompiledInputOutcome> Outcomes,
    IReadOnlyList<string> ProvenanceBindingInputSha256s,
    string BoundedInputSha256,
    string ArtifactVisibility,
    string CoverageState,
    int OmittedInputCount = 0,
    string? OmittedInputSha256 = null);

public sealed record SourceMetadataReconciliationEntry(
    string ReconciliationState,
    string SourceIdentity,
    string MetadataIdentity,
    string SourceFactId,
    IReadOnlyList<string> CompiledFactIds,
    string RuleId,
    string EvidenceTier,
    string ExtractorVersion,
    string CompiledProvenanceState,
    string ProvenanceBindingInputSha256,
    string GapKind,
    string Limitation,
    string EvidenceFactId,
    string FilePath,
    int StartLine,
    int EndLine,
    string CommitSha,
    int OmittedCompiledFactIdCount,
    string? OmittedCompiledFactIdSha256);

public sealed record SourceMetadataReconciliationSummary(
    string SchemaVersion,
    string RuleId,
    string ExtractorVersion,
    string CoverageState,
    string BoundedInputSha256,
    string CompiledGeneratorSha256,
    int ExactJoinCount,
    int ExplicitGapCount,
    IReadOnlyList<SourceMetadataReconciliationEntry> Entries,
    int OmittedEntryCount,
    string? OmittedEntrySha256);

internal sealed record CompiledEvidenceCandidate(
    string SafeLocator,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string? SourceSymbol,
    string? TargetSymbol,
    string? ContractElement,
    IReadOnlyDictionary<string, string> Properties,
    string? MetadataToken);

internal sealed record CompiledInputEvaluation(
    CompiledInputProvenance? Provenance,
    IReadOnlyList<CompiledEvidenceCandidate> Candidates,
    IReadOnlyList<string> KnownGaps,
    IReadOnlyList<CompiledInputBindingArtifact> BindingArtifacts)
{
    public static readonly CompiledInputEvaluation Disabled = new(null, [], [], []);
}

internal sealed record CompiledInputBindingArtifact(
    string FullPath,
    string SafeLocator,
    string Outcome,
    string ProvenanceState,
    string? RawFileSha256,
    string? AssemblyIdentity,
    string ProvenanceBindingInputSha256);
