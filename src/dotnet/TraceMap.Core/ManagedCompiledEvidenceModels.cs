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
    string CoverageState);

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
    IReadOnlyList<string> KnownGaps)
{
    public static readonly CompiledInputEvaluation Disabled = new(null, [], []);
}
