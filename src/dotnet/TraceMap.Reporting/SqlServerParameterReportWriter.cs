using System.Net;
using System.Text;
using TraceMap.Core;
namespace TraceMap.Reporting;

public static class SqlServerParameterReportWriter
{
    public static string Build(SqlServerComparisonReport report)
    {
        static string E(string value) => WebUtility.HtmlEncode(value).Replace("|", "&#124;", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        var s = new StringBuilder("# SQL Server synthetic parameter comparison\n\nCoverage: partial. No aggregate clean verdict.\n\n");
        s.AppendLine($"Repo: {E(report.Repo)}; revision: {E(report.Revision)}; generator SHA-256: {report.GeneratorSha256}; public bounded input SHA-256: {report.BoundedInputSha256}.\n");
        s.AppendLine("| Source | Schema declaration | Status / rule / tier | Reasons |\n| --- | --- | --- | --- |");
        foreach (var row in report.Comparisons)
            s.AppendLine($"| {E(row.SourcePath)}:{row.SourceStartLine}–{row.SourceEndLine} ({E(row.SourceExtractorVersion)}) | {(row.Declaration is { } d ? E(d.Identity.Server + "." + d.Identity.Database + "." + d.Identity.Schema + "." + d.Identity.Procedure) + " " + E(d.FilePath) + ":" + d.StartLine + "–" + d.EndLine : "unavailable")} | {row.Status} / {row.RuleId} / {row.Tier} | {E(string.Join("; ", row.Reasons))} |");
        s.AppendLine("\nLimitations:\n");
        foreach (var limit in report.Limitations) s.AppendLine("- " + E(limit));
        return s.ToString();
    }
}
