using System.Net;
using System.Text;
using TraceMap.Core;
namespace TraceMap.Reporting;

public static class SqlServerParameterReportWriter
{
    public static string Build(SqlServerDdlComparisonReport report)
    {
        var ddl = report.DdlEvidence;
        var text = new StringBuilder("# SQL Server DDL parameter evidence\n\n");
        text.AppendLine($"Origin: {ddl.Declarations.Origin}; rule: {ddl.RuleId}; tier: {ddl.Tier}; extractor: {ddl.ExtractorVersion}.");
        text.AppendLine($"Parser: {ddl.ParserVersion}; parser SHA-256: {ddl.ParserSha256}.");
        text.AppendLine($"DDL generator SHA-256: {ddl.GeneratorSha256}; DDL bounded input SHA-256: {ddl.BoundedInputSha256}.");
        text.AppendLine($"Report generator SHA-256: {report.GeneratorSha256}; report bounded input SHA-256: {report.BoundedInputSha256}.\n");
        foreach (var span in ddl.ParameterSpans)
            text.AppendLine($"- DDL parameter: {WebUtility.HtmlEncode(span.Parameter)} at {WebUtility.HtmlEncode(span.FilePath)}:{span.StartLine}–{span.EndLine}; {ddl.RuleId}; {ddl.Tier}.");
        foreach (var gap in ddl.Unsupported)
            text.AppendLine($"- Unsupported: {WebUtility.HtmlEncode(gap.Kind)} {WebUtility.HtmlEncode(gap.Parameter)} at {WebUtility.HtmlEncode(gap.FilePath)}:{gap.StartLine}–{gap.EndLine}; {gap.RuleId}; {gap.Tier}.");
        text.Append(Build(report.Comparison).Replace("# SQL Server synthetic parameter comparison", "## Bounded DDL parameter comparison", StringComparison.Ordinal));
        return text.ToString();
    }

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
