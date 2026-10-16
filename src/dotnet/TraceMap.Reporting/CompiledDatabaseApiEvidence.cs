using TraceMap.Core;

namespace TraceMap.Reporting;

/// <summary>Original encoded IL API operand and its observation provenance, never SQL/source text.</summary>
public sealed record CompiledDatabaseApiEvidence(string? OriginalTargetIdentity, bool SignatureTruncated,
    string? IlGeneratorSha256, string? IlBoundedInputSha256, string? RawFileSha256, string ProvenanceState)
{
    internal static CompiledDatabaseApiEvidence Create(CombinedFactRow call, CombinedFactRow caller)
    {
        static string? Hash(string? value) => value is { Length:64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ? value : null;
        var identity = call.Properties.GetValueOrDefault("targetIdentity");
        var truncated = identity is { Length: > 8192 };
        return new(truncated ? null : identity, truncated,
            Hash(call.Properties.GetValueOrDefault("ilGeneratorSha256")), Hash(call.Properties.GetValueOrDefault("ilBoundedInputSha256")),
            Hash(call.Properties.GetValueOrDefault("rawFileSha256")), caller.Properties.GetValueOrDefault("provenanceState") == "bound" ? "bound" : "unbound");
    }
}
