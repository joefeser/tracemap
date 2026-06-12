using TraceMap.Core;

namespace TraceMap.Reporting;

public static class MarkdownReportWriter
{
    public static async Task WriteAsync(string path, ScanResult result, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, Build(result), cancellationToken);
    }

    public static string Build(ScanResult result)
    {
        var manifest = result.Manifest;
        var factsByType = result.Facts
            .GroupBy(fact => fact.FactType)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (FactType: group.Key, Count: group.Count()))
            .ToArray();

        var lines = new List<string>
        {
            "# TraceMap Scan Report",
            "",
            "## Repository",
            "",
            $"- Repo: `{manifest.RepoName}`",
            $"- Commit SHA: `{manifest.CommitSha}`",
            $"- Branch: `{manifest.Branch ?? "unknown"}`",
            $"- Remote: `{manifest.RemoteUrl ?? "unknown"}`",
            $"- Scan ID: `{manifest.ScanId}`",
            $"- Scanner version: `{manifest.ScannerVersion}`",
            "",
            "## Analysis Coverage",
            "",
            $"- Analysis level: `{manifest.AnalysisLevel}`",
            $"- Build status: `{manifest.BuildStatus}`",
            "",
            "This report is an evidence inventory and syntax map only. It does not classify contract impact.",
            "",
            "## Inventory",
            "",
            $"- Solutions: `{manifest.Solutions.Count}`",
            $"- Projects: `{manifest.Projects.Count}`",
            $"- Target frameworks: `{manifest.TargetFrameworks.Count}`",
            $"- Inventoried files: `{result.Inventory.Count}`",
            "",
            "## Known Gaps",
            ""
        };

        if (manifest.KnownGaps.Count == 0)
        {
            lines.Add("- None recorded.");
        }
        else
        {
            lines.AddRange(manifest.KnownGaps.Select(gap => $"- {gap}"));
        }

        lines.Add("");
        lines.Add("## Facts By Type");
        lines.Add("");

        foreach (var item in factsByType)
        {
            lines.Add($"- `{item.FactType}`: `{item.Count}`");
        }

        lines.Add("");
        lines.Add("## Solutions");
        lines.Add("");
        lines.AddRange(manifest.Solutions.Count == 0 ? ["- None found."] : manifest.Solutions.Select(path => $"- `{path}`"));

        lines.Add("");
        lines.Add("## Projects");
        lines.Add("");
        lines.AddRange(manifest.Projects.Count == 0 ? ["- None found."] : manifest.Projects.Select(path => $"- `{path}`"));

        AddFactSection(
            lines,
            "HTTP Calls",
            result.Facts.Where(fact => fact.FactType is FactTypes.HttpCallDetected or FactTypes.HttpClientCreated),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Database Calls",
            result.Facts.Where(fact => fact.FactType is FactTypes.DbContextDeclared
                or FactTypes.DbSetDeclared
                or FactTypes.DbChangeSaved
                or FactTypes.DapperCallDetected
                or FactTypes.SqlCommandDetected
                or FactTypes.SqlTextUsed),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Config Keys",
            result.Facts.Where(fact => fact.FactType is FactTypes.ConfigKeyDeclared or FactTypes.ConnectionStringDeclared),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Call Flow",
            result.Facts.Where(fact => fact.FactType == FactTypes.CallEdge),
            fact => $"- `{DisplaySource(fact)}` -> `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Object Creations",
            result.Facts.Where(fact => fact.FactType == FactTypes.ObjectCreated),
            fact => $"- `{DisplaySource(fact)}` creates `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Argument Flow",
            result.Facts.Where(fact => fact.FactType == FactTypes.ArgumentPassed),
            fact => $"- `{DisplaySource(fact)}` passes argument `{fact.Properties.GetValueOrDefault("argumentOrdinal") ?? "?"}` to `{fact.Properties.GetValueOrDefault("parameterName") ?? "unknown"}` on `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Local Aliases",
            result.Facts.Where(fact => fact.FactType == FactTypes.LocalAlias),
            fact => $"- `{fact.Properties.GetValueOrDefault("aliasSymbol") ?? DisplayFactName(fact)}` aliases `{fact.Properties.GetValueOrDefault("originSymbol") ?? "unknown"}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Field Aliases",
            result.Facts.Where(fact => fact.FactType == FactTypes.FieldAlias),
            fact => $"- `{fact.Properties.GetValueOrDefault("fieldSymbol") ?? DisplayFactName(fact)}` aliases `{fact.Properties.GetValueOrDefault("originSymbol") ?? "unknown"}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Logic Hotspots",
            result.Facts.Where(fact => fact.FactType is FactTypes.CalculationExpression
                or FactTypes.BranchingLogic
                or FactTypes.RetryPolicyLogic
                or FactTypes.SerializationLogic),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Flow Boundaries",
            result.Facts.Where(fact => fact.FactType is FactTypes.DependencyResolved
                or FactTypes.DeserializedObject
                or FactTypes.ReflectionUsage
                or FactTypes.DynamicInvocation
                or FactTypes.CollectionMutation
                or FactTypes.ObjectMutation
                or FactTypes.BranchCondition),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Runtime Evidence",
            result.Facts.Where(fact => fact.FactType is FactTypes.DependencyRegistered
                or FactTypes.SerializerContractMember
                or FactTypes.ReflectionTarget
                or FactTypes.DynamicDispatchCandidate
                or FactTypes.CollectionElementFlow
                or FactTypes.MutationSemantics
                or FactTypes.BranchFeasibility),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Boilerplate Signals",
            result.Facts.Where(fact => fact.FactType == FactTypes.InfrastructureBoilerplate),
            fact => $"- `{fact.Properties.GetValueOrDefault("category") ?? "unknown"}` at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        lines.Add("");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddFactSection(List<string> lines, string title, IEnumerable<CodeFact> facts, Func<CodeFact, string> format)
    {
        lines.Add("");
        lines.Add($"## {title}");
        lines.Add("");

        var selectedFacts = facts
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => DisplayFactName(fact), StringComparer.Ordinal)
            .Take(50)
            .ToArray();

        lines.AddRange(selectedFacts.Length == 0 ? ["- None found."] : selectedFacts.Select(format));
    }

    private static string DisplayFactName(CodeFact fact)
    {
        return fact.ContractElement ?? fact.TargetSymbol ?? fact.Properties.GetValueOrDefault("keyPath") ?? "unknown";
    }

    private static string DisplaySource(CodeFact fact)
    {
        return string.IsNullOrWhiteSpace(fact.SourceSymbol) ? "unknown" : fact.SourceSymbol;
    }
}
