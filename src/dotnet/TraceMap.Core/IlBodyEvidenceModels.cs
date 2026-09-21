namespace TraceMap.Core;

public sealed record IlBodyLimits(
    int MaxBodyCount = 50_000,
    int MaxInstructionsPerBody = 100_000,
    int MaxLocalsPerBody = 10_000,
    int MaxExceptionRegionsPerBody = 10_000,
    int MaxTextLength = 4_096,
    long MaxTotalWorkUnits = 2_000_000);

public sealed record IlExpectedInput(string SafeLocator, string Role);

public sealed record IlInputOutcome(
    string SafeLocator,
    string Role,
    string Outcome,
    string ProvenanceState,
    string? RawFileSha256,
    string PrivacyProjectedInputSha256,
    string? AssemblyIdentity,
    string? ModuleName,
    string? ModuleMvid,
    string? ProvenanceBindingInputSha256,
    IReadOnlyList<string> GapKinds);

public sealed record IlBodyProvenance(
    string SchemaVersion,
    string PolicyVersion,
    string GeneratorSha256,
    IReadOnlyList<string> ExtractorIdentities,
    IReadOnlyList<IlExpectedInput> ExpectedInputs,
    IlBodyLimits EffectiveLimits,
    IReadOnlyList<IlInputOutcome> Outcomes,
    string BoundedInputSha256,
    string ArtifactVisibility,
    string CoverageState);

internal sealed record IlCallObservation(
    long Offset,
    string Opcode,
    string ReferenceKind,
    string ReferenceToken,
    string TargetIdentity);

internal sealed record IlBodyObservation(
    string MetadataToken,
    string MethodIdentity,
    int InstructionCount,
    string InstructionsSha256,
    int LocalCount,
    string LocalsSha256,
    int ExceptionRegionCount,
    string ExceptionRegionsSha256,
    string MaxStack,
    bool InitLocals,
    string BodyIdentity,
    string BodySha256,
    IReadOnlyList<IlCallObservation> Calls);

internal sealed record EvaluatedIlInput(
    IlInputOutcome Outcome,
    IReadOnlyList<IlBodyObservation> Bodies);

internal sealed record IlBodyEvaluation(
    IlBodyProvenance? Provenance,
    IReadOnlyList<EvaluatedIlInput> Inputs,
    IReadOnlyList<string> KnownGaps,
    IReadOnlyList<CompiledInputBindingArtifact> BindingArtifacts)
{
    public static readonly IlBodyEvaluation Disabled = new(null, [], [], []);
}
