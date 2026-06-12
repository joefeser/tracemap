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
            "This report is an evidence inventory only. It does not classify contract impact.",
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

        lines.Add("");
        return string.Join(Environment.NewLine, lines);
    }
}
