using System.Text;
using TraceMap.Core;

namespace TraceMap.Reporting;

public static class SqlServerCompiledLifecycleReportWriter
{
    public static string Build(SqlServerLifecycleReport report)
    {
        static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
        var text = new StringBuilder("# Compiled SQL lifecycle comparison\n\nCoverage: partial. Schema matches are not proof of runtime values or successful execution.\n\n");
        text.AppendLine($"Generator SHA-256: {Cell(report.GeneratorSha256)}\n\nBounded input SHA-256: {Cell(report.BoundedInputSha256)}\n");
        if (report.SchemaOrigin is not null)
            text.AppendLine($"Schema origin: {Cell(report.SchemaOrigin)}. Comparisons are conditional on this declaration, not independently verified database metadata. Private local report; do not publish.\n");
        text.AppendLine("| Fact | Guard | Schema identity | Current value origin | Application assignment |\n| --- | --- | --- | --- | --- |");
        foreach (var row in report.Comparisons)
            text.AppendLine($"| {Cell(row.FactId)} | {Cell(row.Guard)} | {Cell(row.Status)} | {Cell(row.ValueOrigin)} | {row.ApplicationAssignment} |");
        text.AppendLine("\nSchema ancestry and current value origins are separate. A retained column absent from the latest merged result does not establish a SQL value origin. Exact source-column hashes and execution identities are retained in JSON.\n");
        text.AppendLine("\n## Potential failure boundaries\n\nThese are possible boundaries, not observed failures. Before and normal-completion snapshots are retained in JSON. Catch continuations are not executed; no final post-catch state or rollback is claimed.\n");
        text.AppendLine("| Fact | Guard | Operation | Partial mutation possible | Earlier rows may be completed |\n| --- | --- | --- | --- | --- |");
        foreach (var failure in report.PotentialFailures)
            text.AppendLine($"| {Cell(failure.FactId)} | {Cell(failure.Guard)} | {Cell(failure.Operation)} | {failure.MayPartiallyMutate} | {failure.MayHaveCompletedEarlierRows} |");
        text.AppendLine("\n## Limitations\n");
        foreach (var limitation in report.Limitations) text.AppendLine("- " + Cell(limitation));
        return text.ToString();
    }
}
