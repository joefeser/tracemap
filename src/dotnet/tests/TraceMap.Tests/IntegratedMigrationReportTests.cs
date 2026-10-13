using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class IntegratedMigrationReportTests
{
    [Fact]
    public void Projectless_webforms_to_backend_sql_and_http_is_one_bounded_report()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(root.FullName, "samples", "migration-vertical-slice"), temp.Path));
        var report = MarkdownReportWriter.Build(scan);
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.WebFormsHandlerResolved);
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.HttpCallDetected);
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate);
        Assert.Contains("candidate ordered source path", report);
        Assert.Contains("unresolved—not proof of unused code", report);
        Assert.Contains("value observations `1`", report);
        Assert.Contains("bounded-local-constant-syntax-candidate", report);
        Assert.Contains("outbound-http:", report);
        Assert.Contains("database:", report);
        Assert.Contains("Review pending:", report);
        Assert.Contains("Displayed groups with candidate entry paths: 2/3", report);
        Assert.Contains("Parameter facts not displayed in a group: 0", report);
        Assert.Contains("Parameter witnesses: 5", report);
        Assert.Contains("PublicWidget.ascx", report);
        Assert.Equal(report, MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Reverse().ToArray() }));
        var disconnected = MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Where(f => f.FactType != FactTypes.CallEdge).ToArray() });
        Assert.Contains("Displayed groups with candidate entry paths: 0/3", disconnected);
        Assert.Contains("3 callsite evidence groups", disconnected);
        Assert.Contains("Entry path unresolved:", disconnected);
        var unbound = MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Where(f => f.FactType != FactTypes.WebFormsHandlerResolved).ToArray() });
        Assert.Contains("Bindings without retained handler resolution: 2", unbound);
        Assert.Contains("Displayed groups with candidate entry paths: 0/3", unbound);
        Assert.DoesNotContain("DELETE FROM PublicRows", report);
        var save = Assert.Single(scan.Facts, f => f.FactType == FactTypes.MethodDeclared && f.Properties.GetValueOrDefault("name") == "Save");
        var ambiguous = save with { FactId = "synthetic-duplicate", Evidence = save.Evidence with { FilePath = "Other.vb" } };
        var ambiguousReport = MarkdownReportWriter.Build(scan with { Facts = [.. scan.Facts, ambiguous] });
        Assert.Contains("Displayed groups with candidate entry paths: 0/3", ambiguousReport);
        var tooMany = Enumerable.Range(0, 2001).Select(i => save with { FactId = "synthetic-limit-" + i }).ToArray();
        var limited = MarkdownReportWriter.Build(scan with { Facts = [.. scan.Facts, .. tooMany] });
        Assert.Contains("limited: method/call budget exceeded", limited);
        Assert.Contains("3 callsite evidence groups", limited);
    }
}
