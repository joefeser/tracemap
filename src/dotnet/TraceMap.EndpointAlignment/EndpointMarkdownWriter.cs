using System.Text;

namespace TraceMap.EndpointAlignment;

internal static class EndpointMarkdownWriter
{
    public static string Build(EndpointAlignmentReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Endpoint Alignment Report");
        builder.AppendLine();
        builder.AppendLine($"Rule ID: `{report.RuleId}`");
        builder.AppendLine($"Report coverage: `{report.ReportCoverage}`");
        builder.AppendLine();
        AppendSource(builder, "Client", report.ClientSource);
        AppendSource(builder, "Server", report.ServerSource);
        AppendSummary(builder, report);
        if (report.CoverageWarnings.Count > 0)
        {
            builder.AppendLine("## Coverage Warnings");
            builder.AppendLine();
            foreach (var warning in report.CoverageWarnings)
            {
                builder.AppendLine($"- {warning}");
            }
            builder.AppendLine();
        }

        foreach (var group in report.Findings.GroupBy(finding => finding.Classification).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"## {group.Key}");
            builder.AppendLine();
            builder.AppendLine("| Method | Path | Client evidence | Server evidence | Quality | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (var finding in group)
            {
                builder.Append("| ")
                    .Append(Escape(finding.Method))
                    .Append(" | ")
                    .Append(Escape(finding.NormalizedPathKey ?? finding.ClientPathTemplate ?? finding.ServerPathTemplate ?? "unknown"))
                    .Append(" | ")
                    .Append(EvidenceCell(finding.ClientEvidence))
                    .Append(" | ")
                    .Append(EvidenceCell(finding.ServerEvidence))
                    .Append(" | `")
                    .Append(Escape(finding.StaticMatchQuality))
                    .Append("` | ")
                    .Append(Escape(string.Join(" ", finding.Notes)))
                    .AppendLine(" |");
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendSummary(StringBuilder builder, EndpointAlignmentReport report)
    {
        var counts = report.Findings
            .GroupBy(finding => finding.Classification, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var classifications = new[]
        {
            EndpointClassifications.MatchedEndpoint,
            EndpointClassifications.OptionalSegmentMatch,
            EndpointClassifications.MethodMismatch,
            EndpointClassifications.ClientCallNoServerEndpoint,
            EndpointClassifications.ServerEndpointNoClientMatch,
            EndpointClassifications.DynamicClientUrlNeedsReview,
            EndpointClassifications.AmbiguousMatch,
            EndpointClassifications.UnknownAnalysisGap
        };

        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("| Classification | Count |");
        builder.AppendLine("| --- | ---: |");
        foreach (var classification in classifications)
        {
            builder.Append("| `")
                .Append(classification)
                .Append("` | ")
                .Append(counts.GetValueOrDefault(classification))
                .AppendLine(" |");
        }
        builder.AppendLine();
    }

    private static void AppendSource(StringBuilder builder, string label, EndpointSource source)
    {
        builder.AppendLine($"## {label} Source");
        builder.AppendLine();
        builder.AppendLine($"- Label: `{source.Label}`");
        builder.AppendLine($"- Scan ID: `{source.ScanId}`");
        builder.AppendLine($"- Repo: `{source.RepoName}`");
        builder.AppendLine($"- Commit SHA: `{source.CommitSha}`");
        builder.AppendLine($"- Analysis: `{source.AnalysisLevel}` / `{source.BuildStatus}`");
        builder.AppendLine($"- Scan root: `{source.ScanRootRelativePath ?? "unknown"}`");
        builder.AppendLine($"- Index path hash: `{source.IndexPathHash}`");
        builder.AppendLine();
    }

    private static string EvidenceCell(EndpointEvidence? evidence)
    {
        if (evidence is null)
        {
            return "";
        }

        return $"`{Escape(evidence.RuleId)}` `{Escape(evidence.EvidenceTier)}` `{Escape(evidence.FilePath)}:{evidence.StartLine}-{evidence.EndLine}` `{Escape(evidence.FactId)}`";
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }
}
