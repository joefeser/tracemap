namespace TraceMap.Reporting;

// Bound serialized provenance separately from the number of report gaps.
internal static class PackageGapFactEvidence
{
    private const int JsonLimit = 256;
    private const int MarkdownLimit = 8;

    internal static (IReadOnlyList<string> Ids, int OmittedCount) Bound(IEnumerable<string> ids)
    {
        var ordered = ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return (ordered.Take(JsonLimit).ToArray(), Math.Max(0, ordered.Length - JsonLimit));
    }

    internal static string Summary(IReadOnlyList<string>? ids, int omittedCount)
    {
        var shown = (ids ?? []).Take(MarkdownLimit).ToArray();
        var omitted = omittedCount + Math.Max(0, (ids?.Count ?? 0) - shown.Length);
        return (shown.Length == 0 ? string.Empty : " facts " + string.Join(',', shown))
            + (omitted == 0 ? string.Empty : $" ({omitted} supporting fact IDs omitted)");
    }
}
