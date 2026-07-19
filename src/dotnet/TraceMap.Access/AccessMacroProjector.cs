using TraceMap.Core;

namespace TraceMap.Access;

internal sealed record AccessRawMacro(string Name, string MacroKind);

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
        var observedKinds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var raw in rawMacros
                     .OrderBy(item => NormalizeKind(item.MacroKind), StringComparer.Ordinal)
                     .ThenBy(item => AccessSafeValues.RoleHash("access-macro-sort-name", item.Name), StringComparer.Ordinal)
                     .Take(limits.MaxObjectsPerCollection))
        {
            var kind = NormalizeKind(raw.MacroKind);
            observedKinds.Add(kind);
            var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"macro-{kind}", raw.Name);
            macros.Add(new(
                identity,
                kind,
                string.Equals(raw.Name.Trim(), "AutoExec", StringComparison.OrdinalIgnoreCase) ? "autoexec" : "not-autoexec",
                "protected-omitted",
                "inventory-only"));
        }

        var gaps = observedKinds
            .Select(kind => new AccessGapProjection("AccessMacroBodyOmitted", $"macro-{kind}", null, RuleIds.LegacyAccessMacroGap))
            .ToList();
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
}
