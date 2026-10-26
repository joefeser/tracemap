using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class SqlServerDdlRuleCatalogTests
{
    [Fact]
    public void Parameter_ddl_rule_has_one_catalog_entry_with_evidence_and_limits()
    {
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "rules", "rule-catalog.yml")))
            root = root.Parent;
        Assert.NotNull(root);
        var catalog = File.ReadAllText(Path.Combine(root.FullName, "rules", "rule-catalog.yml"));
        var entries = Regex.Matches(catalog,
            $@"(?m)^  - id: {Regex.Escape(SqlServerDdlEvidence.RuleId)}\r?$");
        var entry = Assert.Single(entries.Cast<Match>());
        var end = catalog.IndexOf("\n  - id:", entry.Index + entry.Length, StringComparison.Ordinal);
        var block = catalog[entry.Index..(end < 0 ? catalog.Length : end)];
        foreach (var required in new[] { "Tier2Structural", "Tier4Unknown", "SqlServerDdlExport",
            "SqlServerDdlUnsupported", "limitations:", "SQLCMD", "SHA-256", "64 MiB", "no SQL execution" })
            Assert.Contains(required, block, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_result_rule_has_one_catalog_entry_with_evidence_and_limits()
    {
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "rules", "rule-catalog.yml")))
            root = root.Parent;
        Assert.NotNull(root);
        var catalog = File.ReadAllText(Path.Combine(root.FullName, "rules", "rule-catalog.yml"));
        var entry = Assert.Single(Regex.Matches(catalog, $@"(?m)^  - id: {Regex.Escape(SqlServerDdlTableResultEvidence.RuleId)}\r?$").Cast<Match>());
        var end = catalog.IndexOf("\n  - id:", entry.Index + entry.Length, StringComparison.Ordinal);
        var block = catalog[entry.Index..(end < 0 ? catalog.Length : end)];
        foreach (var required in new[] { "Tier2Structural", "Tier4Unknown", "SqlServerDdlTableResultExport", "SqlServerDdlTableUnsupported",
            SqlServerDdlTableResultEvidence.InputVersion, "limitations:", "SQLCMD", "SHA-256", "64 MiB", "no SQL execution", "never partial column lists" })
            Assert.Contains(required, block, StringComparison.Ordinal);
    }
}
