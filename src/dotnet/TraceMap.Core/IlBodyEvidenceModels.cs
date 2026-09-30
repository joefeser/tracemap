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
    string CoverageState)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CompiledAdmissionWorkUsage? AdmissionWork { get; init; }
}

internal sealed record IlCallObservation(
    long Offset,
    string Opcode,
    string ReferenceKind,
    string ReferenceToken,
    string TargetIdentity,
    IlCallStackShape? StackShape = null);

internal sealed record IlCallStackShape(int ParameterCount, bool HasThis, bool ReturnsValue, bool Supported,
    string ByReferenceParameters = "");
internal sealed record IlValueOrigin(string Kind, string Identity);
internal static class IlValueAddresses
{
    internal static bool IsAddress(IlValueOrigin value) => value.Kind is "local-address" or "argument-address" or "field-address";
    internal static IlValueOrigin Create(string kind, int slot, IlValueOrigin value)
    {
        if (value.Identity.Length > 1024) return new("address-unavailable", "");
        var identity = $"{slot.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{value.Kind}:{value.Identity}";
        return identity.Length > 1024 ? new("address-unavailable", "") : new(kind, identity);
    }
    internal static IlValueOrigin Target(IlValueOrigin value)
    {
        for (var depth = 0; depth < 64; depth++)
        {
            if (!IsAddress(value)) return value;
            var parts = value.Identity.Split(':', 3);
            if (parts.Length != 3) return new("address-unavailable", "");
            value = new(parts[1], parts[2]);
        }
        return new("address-unavailable", "");
    }
    internal static bool Slot(IlValueOrigin value, out int slot)
    {
        slot = -1;
        return value.Kind is "local-address" or "argument-address"
            && int.TryParse(value.Identity.Split(':', 2)[0], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out slot);
    }
}
internal sealed record IlCallValueObservation(long Offset, int Region, string State,
    IlValueOrigin Receiver, IReadOnlyList<IlValueOrigin> Arguments, IlValueOrigin Result);
internal sealed record IlValueExceptionEntry(long Offset, int StackCount);
internal sealed record IlValueControlNode(long Offset, IReadOnlyList<long> Successors, bool InvalidatesConfiguration,
    int ExceptionEntryStackCount = -1, IReadOnlyList<IlValueOrigin>? ExposedOrigins = null);
internal sealed record IlValueFlowObservation(IReadOnlyList<IlCallValueObservation> Calls, IReadOnlyList<string> Gaps,
    IReadOnlyList<IlValueControlNode>? ControlFlow = null, int WorkUnits = 0);

internal sealed record IlBodyObservation(
    string MetadataToken,
    string MethodIdentity,
    int InstructionCount,
    string InstructionsSha256,
    // Digest over the opcode-name sequence only, without operands. Kept
    // internal to the IL lanes: it supports rewrite change classification
    // (opcode streams preserved while operands changed) and is not part of
    // the canonical body identity or any emitted body fact property.
    string OpcodesSha256,
    int LocalCount,
    string LocalsSha256,
    int ExceptionRegionCount,
    string ExceptionRegionsSha256,
    string MaxStack,
    bool InitLocals,
    string BodyIdentity,
    string BodySha256,
    IReadOnlyList<IlCallObservation> Calls,
    IReadOnlyList<string>? DiagnosticInstructions = null,
    IlValueFlowObservation? ValueFlow = null);

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
