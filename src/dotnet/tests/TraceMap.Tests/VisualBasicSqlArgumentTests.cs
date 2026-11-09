using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class VisualBasicSqlArgumentTests
{
    private static readonly ScanManifest Manifest = new("public", "synthetic", null, "test", "abc123", "test/1",
        DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);

    [Theory]
    [InlineData("db.ExecSql(\"SELECT x FROM table_a\")", "table_a", "table_b", true)]
    [InlineData("Dim sql = \"SELECT x FROM table_a\"\ndb.ExecSql(sql)", "table_a", "table_b", false)]
    [InlineData("Dim sql = \"UPDATE table_a \"\nsql &= \"SET x = 1\"\ndb.ExecSql(sql)", "x = 1", "x = 2", false)]
    [InlineData("Dim sql = \"SELECT x FROM table_a\"\ndb.ExecSql(sql, part)", "part", "item", true)]
    public void Digest_tracks_construction_and_call_source(string source, string from, string to, bool callChanged)
    {
        CodeFact Hit(string statements) => Assert.Single(Extract(statements), f => f.FactType == FactTypes.SqlTextArgumentCandidate);
        var initial = Hit(source);
        var changed = Hit(source.Replace(from, to));
        Assert.NotNull(initial.Evidence.SnippetHash);
        Assert.Equal(callChanged, initial.Evidence.SnippetHash != changed.Evidence.SnippetHash);
        Assert.NotEqual(initial.Properties["boundedInputSha256"], changed.Properties["boundedInputSha256"]);
        Assert.Equal(initial.Properties["boundedInputSha256"], Hit(source).Properties["boundedInputSha256"]);
    }

    [Fact]
    public void Missing_source_after_semantic_analysis_reports_sql_coverage_gap()
    {
        using var temp = new TempDirectory();
        var file = Path.Combine(temp.Path, "Missing.vb");
        File.WriteAllText(file, "Public Class Sample\nEnd Class");
        var inventory = FileInventory.Collect(temp.Path);
        File.Delete(file);
        var facts = VisualBasicSyntaxExtractor.Extract(temp.Path, Manifest, inventory, new HashSet<string> { "Missing.vb" });
        var gap = Assert.Single(facts, f => f.RuleId == RuleIds.VisualBasicSqlArgument);
        Assert.Equal(FactTypes.AnalysisGap, gap.FactType);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal("Missing.vb", gap.Evidence.FilePath);
        Assert.Equal("reduces-syntax-coverage-for-file", gap.Properties["coverageEffect"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Review_fixture_exposes_only_the_two_expected_source_hits(bool semanticCoverage)
    {
        var root = Path.Combine(SqlDatabaseMatrixCells.Repo(), "samples/vb-sql-argument");
        var facts = VisualBasicSyntaxExtractor.Extract(root, Manifest, FileInventory.Collect(root),
            semanticCoverage ? new HashSet<string> { "Queries.vb" } : null);
        var hits = facts.Where(f => f.FactType == FactTypes.SqlTextArgumentCandidate).ToArray();
        Assert.Equal(new[] { 6, 10 }, hits.Select(f => f.Evidence.StartLine));
        Assert.Equal(new[] { "UPDATE", "SELECT" }, hits.Select(f => f.Properties["sqlOperation"]));
        Assert.Equal(new[] { "3", "10" }, hits.Select(f => f.Properties["constructionStartLine"]));
        Assert.Equal(new[] { "5", "10" }, hits.Select(f => f.Properties["constructionEndLine"]));
        Assert.All(hits, hit =>
        {
            Assert.Equal("unverified", hit.Properties["commandInstanceBinding"]);
            Assert.Equal(RuleIds.VisualBasicSqlArgument, hit.RuleId);
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, hit.EvidenceTier);
            Assert.Equal(Manifest.CommitSha, hit.CommitSha);
            Assert.Matches("^[0-9a-f]{64}$", hit.Properties["generatorSha256"]);
            Assert.Matches("^[0-9a-f]{64}$", hit.Properties["boundedInputSha256"]);
            Assert.DoesNotContain("sample_stock", System.Text.Json.JsonSerializer.Serialize(hit));
        });
    }

    [Theory]
    [InlineData("Dim sql As String = \"UPDATE fixture SET x = 1\"\nsql = \"SELECT x FROM fixture\"\ndb.ExecuteSql(sql)", "SELECT")]
    [InlineData("Dim sql As String = \"UPDATE fixture \"\nsql &= \"SET x = 1\"\ndb.ExecuteSql(sql)", "UPDATE")]
    [InlineData("Dim sql As String = \"UPDATE fixture SET x = 1\"\nChange(sql)\ndb.ExecuteSql(sql)", null)]
    [InlineData("db.ExecSql(\"SELECT x FROM fixture\" & GetKey())", null)]
    [InlineData("db.ExecSql(\"SELECT x FROM fixture\"", null)]
    public void Tracks_only_unambiguous_argument_syntax(string statements, string? expectedVerb)
    {
        var hits = Extract(statements).Where(f => f.FactType == FactTypes.SqlTextArgumentCandidate);
        if (expectedVerb is null) Assert.Empty(hits);
        else Assert.Equal(expectedVerb, Assert.Single(hits).Properties["sqlOperation"]);
    }

    [Fact]
    public void The_first_omitted_site_records_a_budget_gap()
    {
        var facts = Extract(string.Join("\n", Enumerable.Repeat("db.ExecSql(\"SELECT x FROM fixture\")", 257)));
        Assert.Equal(256, facts.Count(f => f.FactType == FactTypes.SqlTextArgumentCandidate));
        var gap = Assert.Single(facts, f => f.RuleId == RuleIds.VisualBasicSqlArgument && f.FactType == FactTypes.AnalysisGap);
        Assert.Equal("SqlTextArgumentLimit", gap.Properties["gapKind"]);
    }

    private static IReadOnlyList<CodeFact> Extract(string statements)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Demo.vb"), $"Class Demo\nSub Run(db As Object)\n{statements}\nEnd Sub\nEnd Class");
        return VisualBasicSyntaxExtractor.Extract(temp.Path, Manifest, FileInventory.Collect(temp.Path), null);
    }
}
