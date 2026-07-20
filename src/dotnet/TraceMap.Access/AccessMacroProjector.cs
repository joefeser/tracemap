using TraceMap.Core;

namespace TraceMap.Access;

internal sealed record AccessRawMacro(
    string Name,
    string MacroKind,
    string? OwnerStableKey = null,
    int Ordinal = 0);

internal sealed record AccessMacroProjectionResult(
    IReadOnlyList<AccessMacroProjection> Macros,
    IReadOnlyList<AccessGapProjection> Gaps);

internal static class AccessMacroProjector
{
    public static AccessMacroProjectionResult Project(
        string databaseIdentitySeed,
        IReadOnlyList<AccessRawMacro> rawMacros,
        AccessLimits? limits = null)
    {
        limits ??= AccessLimits.Default;
        var macros = new List<AccessMacroProjection>();
        var gaps = new List<AccessGapProjection>();
        var observedKinds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var raw in rawMacros
                     .OrderBy(item => NormalizeKind(item.MacroKind), StringComparer.Ordinal)
                     .ThenBy(item => SafeOwnerStableKey(item.OwnerStableKey) ?? string.Empty, StringComparer.Ordinal)
                     .ThenBy(item => AccessSafeValues.RoleHash("access-macro-sort-name", item.Name), StringComparer.Ordinal)
                     .ThenBy(item => item.Ordinal)
                     .Take(limits.MaxObjectsPerCollection))
        {
            var kind = NormalizeKind(raw.MacroKind);
            observedKinds.Add(kind);
            var ownerStableKey = SafeOwnerStableKey(raw.OwnerStableKey);
            var identity = AccessSafeValues.Identity(
                databaseIdentitySeed,
                $"macro-{kind}-{ownerStableKey ?? "database"}",
                raw.Name,
                raw.Ordinal);
            if (raw.OwnerStableKey is not null && ownerStableKey is null)
                gaps.Add(new("AccessMacroOwnerUnavailable", $"macro-{kind}", identity.StableKey, RuleIds.LegacyAccessMacroGap));
            macros.Add(new(
                identity,
                kind,
                ownerStableKey,
                raw.Ordinal,
                kind == "named" && ownerStableKey is null
                    && string.Equals(raw.Name.Trim(), "AutoExec", StringComparison.OrdinalIgnoreCase)
                        ? "autoexec"
                        : "not-autoexec",
                "protected-omitted",
                "inventory-only"));
        }

        gaps.AddRange(observedKinds
            .Select(kind => new AccessGapProjection("AccessMacroBodyOmitted", $"macro-{kind}", null, RuleIds.LegacyAccessMacroGap)));
        if (rawMacros.Count > limits.MaxObjectsPerCollection)
            gaps.Add(new("AccessMacroCollectionLimitReached", "macro-catalog", null, RuleIds.LegacyAccessMacroGap));

        return new(
            macros.OrderBy(item => item.Identity.StableKey, StringComparer.Ordinal).ToArray(),
            gaps.OrderBy(item => item.Classification, StringComparer.Ordinal).ThenBy(item => item.ScopeKind, StringComparer.Ordinal).ToArray());
    }

    private static string NormalizeKind(string value) => value.Trim().ToLowerInvariant() switch
    {
        "named" => "named",
        "ui" or "ui-macro" => "ui",
        "data" or "data-macro" => "data",
        "embedded" or "embedded-macro" => "embedded",
        _ => "unknown"
    };

    private static string? SafeOwnerStableKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= 128
            && trimmed.StartsWith("access-", StringComparison.Ordinal)
            && trimmed.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
                ? trimmed
                : null;
    }
}
