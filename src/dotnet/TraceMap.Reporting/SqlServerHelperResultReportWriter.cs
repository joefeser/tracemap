using System.Globalization;
using System.Net;
using System.Text;
using TraceMap.Core;
namespace TraceMap.Reporting;
/// <summary>Presentation of a helper-result column report per call site; never a second verdict.</summary>
public static class SqlServerHelperResultReportWriter
{
    private static string E(string value) => WebUtility.HtmlEncode(value).Replace("|", "&#124;", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    public static string Build(SqlServerHelperResultComparisonReport report)
    {
        var s = new StringBuilder("# SQL Server helper-result column comparison\n\nCoverage: partial. No aggregate clean verdict. One same-document static helper level; each call site binds separately. First result set (DataSet helpers: first DataSet table); column identity and ordinal bounds only; helper and Fill execution, caller completeness, tables, rows and later result sets are not claimed.\n\n");
        s.AppendLine($"Consumer: {E(report.ConsumerVersion)}; bindings: {E(report.BindingsVersion)}; rule: {SqlServerHelperResultEvidence.RuleId}.");
        s.AppendLine($"Shapes: route {E(report.SchemaRoute)}; origin {E(report.Origin)}; schema {E(report.SchemaVersion)} / {E(report.SchemaRuleId)} / {E(report.SchemaTier)} / {E(report.SchemaExtractorVersion)}.");
        s.AppendLine($"Schema generator SHA-256: {report.SchemaGeneratorSha256}; schema bounded input SHA-256: {report.SchemaInputSha256}; admitted export SHA-256: {report.SchemaEvidenceSha256}.");
        s.AppendLine("Privacy: local evidence report, not a sanitized shareable export. Source paths, member identities and column names are retained. Public synthetic schema admission does not establish that source facts are public. Do not publish private-input reports.\n");
        s.AppendLine($"Repo: {E(report.Repo)}; revision: {E(report.Revision)}; generator SHA-256: {report.GeneratorSha256}; bounded evidence input SHA-256: {report.BoundedInputSha256}.\n");
        foreach (var shape in report.Shapes)
            s.AppendLine($"- First result shape: {Identity(shape.Identity)} at {E(shape.FilePath)}:{shape.StartLine}–{shape.EndLine}; {E(shape.Completeness)}"
                + (shape.UnknownReason is null ? "" : " (" + E(shape.UnknownReason) + ")") + $"; {shape.Columns.Count} columns.");
        s.AppendLine("\n| Helper Fill / call site / DataRow access | Result shape / column | Status / rule / tier | Reasons | Uncompared facets |\n| --- | --- | --- | --- | --- |");
        foreach (var row in report.Comparisons)
        {
            var source = $"{E(row.SourcePath)}:{row.SourceStartLine}–{row.SourceEndLine} ({E(row.SourceExtractorVersion)}; {E(row.SourceRuleId)}; {E(row.SourceTier)})";
            if (row.CallerIndex is { } caller)
                source += $" call #{caller.ToString(CultureInfo.InvariantCulture)} at line {row.CallerStartLine}–{row.CallerEndLine}" + (row.Caller is null ? "" : " in " + E(row.Caller));
            if (row.Access is { } a)
                source += $" access #{row.AccessIndex} {E(a.Kind)} {E(a.Member)} via {E(a.RowSource)}"
                    + (a.RowIndex is { } r ? " row " + r.ToString(CultureInfo.InvariantCulture) : "") + " "
                    + (a.Name is not null ? "name " + E(a.Name) : a.Ordinal is { } o ? "ordinal " + o.ToString(CultureInfo.InvariantCulture) : "dynamic")
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
}
