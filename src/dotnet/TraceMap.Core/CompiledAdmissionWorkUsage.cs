namespace TraceMap.Core;

/// <summary>Exact logical admission credits, not CPU time, allocation or complete extraction work.</summary>
public sealed record CompiledAdmissionWorkUsage(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility,
    string GeneratorSha256, string BoundedInputSha256, string SourceBoundedInputSha256,
    string Phase, string WorkUnitsScope, long ConsumedWorkUnits, long RefusedAggregateRequests,
    long MaxWorkUnits, IReadOnlyList<string> Limitations)
{
    public const string Schema = "compiled-admission-work-usage.v1";
    public const string Rule = "dotnet.compiled.admission-work-usage.v1";
    public const string MetadataScope = "receipt-and-dual-metadata-reader-admission-credits";
    public const string IlScope = "dual-il-body-reader-admission-credits";

    internal static CompiledAdmissionWorkUsage Create(string phase, string generator, string input,
        long consumed, long refused, long maximum)
    {
        var scope = phase == "metadata" ? MetadataScope : phase == "il-body" ? IlScope
            : throw new ArgumentOutOfRangeException(nameof(phase));
        var usage = new CompiledAdmissionWorkUsage(Schema, Rule, "Tier2Structural", "local-only", generator, "", input,
            phase, scope, consumed, refused, maximum,
            ["ConsumedWorkUnits counts successful reservations against this collector's logical aggregate work budget, not CPU instructions, elapsed time, bytes, allocation or total scan work.",
             "Metadata credits cover receipt and dual-reader admission; IL credits cover both bounded body readers. These independent budgets must not be treated as one query or runtime-call count.",
             "RefusedAggregateRequests counts denied aggregate reservation requests only. Per-input caps, preflight exceptions and skipped/unreadable inputs remain separate outcome gaps.",
             "Credits can be consumed before an input later fails admission. Zero credits or refusals do not imply complete coverage, no work, spare capacity or permission to raise limits.",
             "Historical absent measurements remain unknown. Generator and source-input hashes are private local integrity commitments, not authenticated profiling or execution evidence."]);
        usage = usage with { BoundedInputSha256 = InputDigest(usage) };
        Validate(usage, phase, generator, input, maximum);
        return usage;
    }

    public static void Validate(CompiledAdmissionWorkUsage? usage, string phase, string generator, string input, long maximum)
    {
        if (usage is null) return;
        var scope = phase == "metadata" ? MetadataScope : phase == "il-body" ? IlScope : "";
        if (usage.SchemaVersion != Schema || usage.RuleId != Rule || usage.EvidenceTier != "Tier2Structural"
            || usage.Visibility != "local-only" || usage.Phase != phase || usage.WorkUnitsScope != scope
            || scope.Length == 0 || !Sha(generator) || !Sha(input)
            || usage.GeneratorSha256 != generator || usage.SourceBoundedInputSha256 != input
            || usage.MaxWorkUnits != maximum || maximum < 1 || usage.ConsumedWorkUnits < 0
            || usage.ConsumedWorkUnits > maximum || usage.RefusedAggregateRequests < 0
            || usage.Limitations is null || usage.Limitations.Count is < 1 or > 16
            || usage.Limitations.Any(item => string.IsNullOrEmpty(item) || item.Length > 1024)
            || usage.BoundedInputSha256 != InputDigest(usage))
            throw new InvalidDataException("COMPILED_ADMISSION_USAGE_INVALID");
    }

    private static bool Sha(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string InputDigest(CompiledAdmissionWorkUsage usage) => ManagedMetadataExtractor.CanonicalDigest(new
    {
        usage.SchemaVersion, usage.RuleId, usage.GeneratorSha256, usage.SourceBoundedInputSha256,
        usage.Phase, usage.WorkUnitsScope, usage.ConsumedWorkUnits, usage.RefusedAggregateRequests, usage.MaxWorkUnits
    });
}
