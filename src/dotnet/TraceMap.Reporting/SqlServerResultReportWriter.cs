using System.Globalization;
using System.Net;
using System.Text;
using TraceMap.Core;
namespace TraceMap.Reporting;

/// <summary>Presentation of a result-column report; never a second verdict.</summary>
public static class SqlServerResultReportWriter
{
    private static string E(string value) => WebUtility.HtmlEncode(value).Replace("|", "&#124;", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    public static string Build(SqlServerDdlResultComparisonReport report)
    {
        var ddl = report.DdlEvidence;
        var text = new StringBuilder("# SQL Server DDL result-column evidence\n\n");
        text.AppendLine($"Origin: {E(ddl.Declarations.Origin)}; rule: {ddl.RuleId}; tier: {ddl.Tier}; extractor: {ddl.ExtractorVersion}.");
        text.AppendLine($"Parser: {ddl.ParserVersion}; parser SHA-256: {ddl.ParserSha256}.");
        text.AppendLine($"DDL generator SHA-256: {ddl.GeneratorSha256}; DDL bounded input SHA-256: {ddl.BoundedInputSha256}.");
        text.AppendLine($"Report generator SHA-256: {report.GeneratorSha256}; report bounded input SHA-256: {report.BoundedInputSha256}.\n");
        foreach (var shape in ddl.Declarations.Shapes)
            text.AppendLine($"- DDL first result shape: {Identity(shape.Identity)} at {E(shape.FilePath)}:{shape.StartLine}–{shape.EndLine}; {shape.Completeness}"
                + (shape.UnknownReason is null ? "" : " (" + E(shape.UnknownReason) + ")") + $"; {shape.Columns.Count} columns; {ddl.RuleId}; {(shape.Completeness == "complete" ? ddl.Tier : EvidenceTiers.Tier4Unknown)}.");
        foreach (var gap in ddl.Unsupported)
            text.AppendLine($"- Unsupported: {E(gap.Kind)} {Identity(gap.Identity)} at {E(gap.FilePath)}:{gap.StartLine}–{gap.EndLine}; {gap.RuleId}; {gap.Tier}.");
        text.Append('\n');
        text.Append(Build(report.Comparison).Replace("# SQL Server result-column comparison", "## Bounded DDL result-column comparison", StringComparison.Ordinal));
        return text.ToString();
    }

    public static string Build(SqlServerDdlTableResultComparisonReport report)
    {
        var ddl = report.DdlEvidence;
        var text = new StringBuilder("# SQL Server DDL table-backed result-column evidence\n\n");
        text.AppendLine($"Origin: {E(ddl.Declarations.Origin)}; rule: {ddl.RuleId}; tier: {ddl.Tier}; extractor: {ddl.ExtractorVersion}.");
        text.AppendLine($"Parser: {ddl.ParserVersion}; parser SHA-256: {ddl.ParserSha256}.");
        text.AppendLine($"DDL generator SHA-256: {ddl.GeneratorSha256}; DDL bounded input SHA-256: {ddl.BoundedInputSha256}.");
        text.AppendLine($"Report generator SHA-256: {report.GeneratorSha256}; report bounded input SHA-256: {report.BoundedInputSha256}.\n");
        foreach (var table in ddl.Tables)
            text.AppendLine($"- DDL table: {Identity(table.Identity)} at {E(table.FilePath)}:{table.StartLine}–{table.EndLine}; {table.Completeness}"
                + (table.UnknownReason is null ? "" : " (" + E(table.UnknownReason) + ")") + $"; {table.Columns.Count} columns; {ddl.RuleId}; {(table.Completeness == "complete" ? ddl.Tier : EvidenceTiers.Tier4Unknown)}.");
        foreach (var shape in ddl.Declarations.Shapes)
            text.AppendLine($"- DDL first result shape: {Identity(shape.Identity)} at {E(shape.FilePath)}:{shape.StartLine}–{shape.EndLine}; {shape.Completeness}"
                + (shape.UnknownReason is null ? "" : " (" + E(shape.UnknownReason) + ")") + $"; {shape.Columns.Count} columns; {ddl.RuleId}; {(shape.Completeness == "complete" ? ddl.Tier : EvidenceTiers.Tier4Unknown)}.");
        foreach (var projection in ddl.Projections)
            text.AppendLine($"- Projection: {Identity(projection.Procedure)} from {Identity(projection.Table)}: "
                + E(string.Join(", ", projection.Columns.Select(c => $"{c.Ordinal} {c.OutputName} ← {c.SourceColumn} (lines {c.Line}/{c.SourceColumnLine})"))) + ".");
        foreach (var gap in ddl.TableUnsupported)
            text.AppendLine($"- Unsupported table: {E(gap.Kind)} {Identity(gap.Identity)} at {E(gap.FilePath)}:{gap.StartLine}–{gap.EndLine}; {gap.RuleId}; {gap.Tier}.");
        foreach (var gap in ddl.Unsupported)
            text.AppendLine($"- Unsupported: {E(gap.Kind)} {Identity(gap.Identity)} at {E(gap.FilePath)}:{gap.StartLine}–{gap.EndLine}; {gap.RuleId}; {gap.Tier}.");
        text.Append('\n');
        text.Append(Build(report.Comparison).Replace("# SQL Server result-column comparison", "## Bounded DDL table-backed result-column comparison", StringComparison.Ordinal));
        return text.ToString();
    }

    public static string Build(SqlServerResultComparisonReport report)
    {
        var s = new StringBuilder("# SQL Server result-column comparison\n\nCoverage: partial. No aggregate clean verdict. First result set; column identity and ordinal bounds only.\n\n");
        s.AppendLine($"Origin: {E(report.Origin)}; schema {E(report.SchemaVersion)} / {E(report.SchemaRuleId)} / {E(report.SchemaTier)} / {E(report.SchemaExtractorVersion)}.");
        s.AppendLine($"Repo: {E(report.Repo)}; revision: {E(report.Revision)}; generator SHA-256: {report.GeneratorSha256}; public bounded input SHA-256: {report.BoundedInputSha256}.\n");
        s.AppendLine("| Source execution / access | Result shape / column | Status / rule / tier | Reasons | Uncompared facets |\n| --- | --- | --- | --- | --- |");
        foreach (var row in report.Comparisons)
        {
            var source = $"{E(row.SourcePath)}:{row.SourceStartLine}–{row.SourceEndLine} ({E(row.SourceExtractorVersion)}; {E(row.SourceRuleId)}; {E(row.SourceTier)})";
            if (row.Access is { } a)
                source += $" access #{row.AccessIndex} {E(a.Kind)} {E(a.Member)} " + (a.Name is not null ? "name " + E(a.Name) : a.Ordinal is { } o ? "ordinal " + o.ToString(CultureInfo.InvariantCulture) : "dynamic")
                    + $" at line {a.Line}–{a.EndLine}";
            var shape = row.Shape is { } sh
                ? $"{Identity(sh.Identity)} {E(sh.FilePath)}:{sh.StartLine}–{sh.EndLine} ({E(sh.Completeness)}, {sh.ColumnCount} columns)"
                    + (row.Column is { } c ? $"; column {E(c.Name)} ordinal {c.Ordinal} line {c.Line}" : "")
                : "unavailable";
            s.AppendLine($"| {source} | {shape} | {row.Status} / {row.RuleId} / {row.Tier} | {E(string.Join("; ", row.Reasons))} | {E(string.Join("; ", row.UncomparedFacets))} |");
        }
        s.AppendLine("\nLimitations:\n");
        foreach (var limit in report.Limitations) s.AppendLine("- " + E(limit));
        return s.ToString();
    }

    private static string Identity(SqlServerObjectIdentity i) => E(i.Server + "." + i.Database + "." + i.Schema + "." + i.Procedure);
    private static string Identity(SqlServerTableIdentity i) => E(i.Server + "." + i.Database + "." + i.Schema + "." + i.Table);
}
