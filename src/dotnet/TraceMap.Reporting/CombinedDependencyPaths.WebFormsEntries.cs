using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record WebFormsEntryInventory(string Version, string Coverage, string GeneratorSha256,
    string BoundedInputSha256, IReadOnlyList<WebFormsEntryRelation> Relationships);

public sealed record WebFormsEntryRelation(string CombinedFactId, string SourceIndexId, string CommitSha,
    string ExtractorVersion, string RuleId, string EdgeKind, string EvidenceTier, string Source,
    string Target, string? TargetSymbolId, string? ControlId, string? TargetType, string? TargetFilePath, string FilePath, int StartLine, int EndLine,
    IReadOnlyList<string> SupportingFactIds, string FirstUnresolvedBoundary);

public static partial class CombinedDependencyPathReporter
{
    // Deliberately separate from graph construction and traversal. These are entry
    // observations for the retained input, never invocation edges or route roots.
    private static WebFormsEntryInventory? BuildWebFormsEntryInventory(CombinedReadResult read, bool selectorLimited)
    {
        var rows = read.Facts.Where(f => f.FactType == FactTypes.WebFormsEntryRelationship
                || f.FactType == FactTypes.AnalysisGap && f.RuleId is RuleIds.WebFormsEntryContainment
                    or RuleIds.WebFormsEntryProperty or RuleIds.WebFormsEntryLifecycle)
            .OrderBy(f => f.CombinedFactId, StringComparer.Ordinal)
            .Select(f => new WebFormsEntryRelation(f.CombinedFactId, f.SourceIndexId, f.CommitSha,
                f.ExtractorVersion ?? "unavailable", f.RuleId,
                f.Properties.GetValueOrDefault("relationshipKind") ?? "unresolved-entry", f.EvidenceTier,
                f.SourceSymbol ?? "", f.TargetSymbol ?? "", f.Properties.GetValueOrDefault("targetSymbolId") ?? f.Properties.GetValueOrDefault("targetTypeSymbolId"),
                f.Properties.GetValueOrDefault("controlId"), f.Properties.GetValueOrDefault("targetType"), f.Properties.GetValueOrDefault("targetFilePath"),
                f.FilePath, f.StartLine, f.EndLine,
                // Original fact IDs are explicitly scoped by SourceIndexId, not
                // rejoined globally or mistaken for combined IDs.
                (f.Properties.GetValueOrDefault("supportingFactIds") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries),
                f.Properties.GetValueOrDefault("firstUnresolvedBoundary") ?? "Unavailable")).ToArray();
        if (rows.Length == 0 && !selectorLimited) return null;
        return new("webforms-entry-inventory.v1", selectorLimited ? "PartialStaticEvidence; selector-limited input; omitted entries are not absence evidence; no execution claim"
                : "PartialStaticEvidence; retained input only; no execution claim",
            Convert.ToHexStringLower(SHA256.HashData(GeneratorBinaryIdentity.ReadBytes(typeof(CombinedDependencyPathReporter).Assembly))),
            Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows))), rows);
    }

    private static void AppendWebFormsEntryInventory(StringBuilder builder, WebFormsEntryInventory? inventory)
    {
        if (inventory is null) return;
        builder.AppendLine("## WebForms entry relationships (separate from call paths)");
        builder.AppendLine();
        builder.AppendLine(Cell(inventory.Coverage));
        builder.AppendLine();
        builder.AppendLine("Retained-input inventory; partial static evidence. Containment is not execution, a declarative reference is not a getter call, and lifecycle candidates do not establish framework dispatch. Terminal tiers never upgrade these relationships. Path selectors and traversal caps do not make this a whole-application inventory.");
        builder.AppendLine();
        AppendRows(builder, inventory.Relationships,
            "| Kind | Tier | Control / type or surface | Source → target | Evidence | Rule | First unresolved boundary |",
            "| --- | --- | --- | --- | --- | --- | --- |",
            r => $"| {Cell(r.EdgeKind)} | {Cell(r.EvidenceTier)} | {Cell(r.ControlId ?? "n/a")} / {Cell(r.TargetType ?? r.TargetFilePath ?? "n/a")} | {Cell(r.Source)} → {Cell(r.Target)} | {Cell(r.SourceIndexId)} / {Cell(r.FilePath)}:{r.StartLine}-{r.EndLine} | {Cell(r.RuleId)} | {Cell(r.FirstUnresolvedBoundary)} |");
    }
}
