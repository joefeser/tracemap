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
        text.AppendLine("| Fact | Guard | Schema identity | Current value origin | Application assignment |\n| --- | --- | --- | --- | --- |");
        foreach (var row in report.Comparisons)
            text.AppendLine($"| {Cell(row.FactId)} | {Cell(row.Guard)} | {Cell(row.Status)} | {Cell(row.ValueOrigin)} | {row.ApplicationAssignment} |");
        text.AppendLine("\n## Limitations\n");
        foreach (var limitation in report.Limitations) text.AppendLine("- " + Cell(limitation));
        return text.ToString();
    }
}
