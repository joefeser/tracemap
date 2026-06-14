using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class MarkdownReportWriterTests
{
    [Fact]
    public void Build_renders_query_builder_patterns_with_fields()
    {
        var manifest = CreateManifest();
        var fact = FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan("src/Orders.cs", 12, 12, null, "test", "test/1.0"),
            contractElement: "Where",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationName"] = "Where",
                ["filterFields"] = "Status",
                ["sortFields"] = "CreatedAt",
                ["patternHash"] = "0123456789abcdef0123456789abcdef"
            });

        var report = MarkdownReportWriter.Build(new ScanResult(manifest, [fact], []));

        Assert.Contains("Query builder `Where` fields `Status;CreatedAt` pattern `0123456789abcdef0123456789abcdef`", report);
        Assert.DoesNotContain("SQL shape `Where`", report);
        Assert.Contains("static shape evidence", report);
        Assert.Contains("runtime execution", report);
    }

    [Fact]
    public void Build_renders_synthetic_sql_shape_without_raw_sql()
    {
        var manifest = CreateManifest();
        // This tests report rendering only; the .NET extractor does not currently emit SQL-shape facts.
        var fact = FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("src/Orders.cs", 18, 18, null, "test", "test/1.0"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationName"] = "SELECT",
                ["tableName"] = "orders",
                ["columnNames"] = "id,status,total",
                ["sqlSourceKind"] = "orm-text",
                ["queryShapeHash"] = "abcdef0123456789abcdef0123456789",
                ["rawSql"] = "SELECT id, status, total FROM orders WHERE secret = 'keep-out'"
            });

        var report = MarkdownReportWriter.Build(new ScanResult(manifest, [fact], []));

        Assert.Contains("SQL shape `SELECT` table `orders` columns `id;status;total` source `orm-text` shape `abcdef0123456789abcdef0123456789`", report);
        Assert.Contains("rule `csharp.syntax.querypattern.v1`", report);
        Assert.DoesNotContain("fields `none`", report);
        Assert.DoesNotContain("secret", report);
        Assert.DoesNotContain("SELECT id, status, total FROM orders", report);
        Assert.Matches(@"shape `[a-f0-9]{32}`", report);
    }

    [Fact]
    public void Build_hashes_unsafe_sql_shape_identifiers()
    {
        var manifest = CreateManifest();
        // This tests report rendering only; the .NET extractor does not currently emit SQL-shape facts.
        var fact = FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("/tmp/private/Orders.cs", 18, 18, null, "test", "test/1.0"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationName"] = "SELECT",
                ["tableName"] = "orders WHERE tenant_id = 1",
                ["columnNames"] = "id,password;status",
                ["sqlSourceKind"] = "orm-text",
                ["queryShapeHash"] = "abcdef0123456789abcdef0123456789"
            });

        var report = MarkdownReportWriter.Build(new ScanResult(manifest, [fact], []));

        Assert.Contains("unsafe-identifier-hash:", report);
        Assert.Contains("columns `id;password;status`", report);
        Assert.Contains("absolute-path-hash:", report);
        Assert.DoesNotContain("/tmp/private", report);
        Assert.DoesNotContain("WHERE tenant_id", report);
    }

    [Fact]
    public void Build_hashes_url_like_evidence_paths()
    {
        var manifest = CreateManifest();
        // This tests report rendering only; the .NET extractor does not currently emit SQL-shape facts.
        var fact = FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("webpack://private/app/orders.cs", 18, 18, null, "test", "test/1.0"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationName"] = "SELECT",
                ["tableName"] = "orders",
                ["columnNames"] = "order_id,created_by",
                ["sqlSourceKind"] = "orm-text",
                ["queryShapeHash"] = "abcdef0123456789abcdef0123456789"
            });

        var report = MarkdownReportWriter.Build(new ScanResult(manifest, [fact], []));

        Assert.Contains("columns `order_id;created_by`", report);
        Assert.Contains("absolute-path-hash:", report);
        Assert.DoesNotContain("webpack://private", report);
        Assert.DoesNotContain("unsafe-identifier-hash:", report);
    }

    [Fact]
    public void Build_uses_sql_shape_placeholders_for_missing_optional_metadata()
    {
        var manifest = CreateManifest();
        // This tests report rendering only; the .NET extractor does not currently emit SQL-shape facts.
        var fact = FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("src/Orders.cs", 18, 18, null, "test", "test/1.0"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["sqlSourceKind"] = "orm-text"
            });

        var report = MarkdownReportWriter.Build(new ScanResult(manifest, [fact], []));

        Assert.Contains("SQL shape `unknown` table `unknown` columns `none` source `orm-text` shape `n/a`", report);
    }

    [Fact]
    public void Build_treats_empty_sql_shape_properties_as_missing()
    {
        var manifest = CreateManifest();
        // This tests report rendering only; the .NET extractor does not currently emit SQL-shape facts.
        var fact = FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.CSharpSyntaxQueryPattern,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("src/Orders.cs", 18, 18, null, "test", "test/1.0"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["tableName"] = "",
                ["tableNames"] = "orders;order_items",
                ["columnNames"] = "",
                ["fieldNames"] = "order_id,created_by",
                ["sqlSourceKind"] = "orm-text",
                ["queryShapeHash"] = "abcdef0123456789abcdef0123456789"
            });

        var report = MarkdownReportWriter.Build(new ScanResult(manifest, [fact], []));

        Assert.Contains("table `orders;order_items` columns `order_id;created_by`", report);
        Assert.DoesNotContain("table `unknown`", report);
    }

    [Fact]
    public void Build_renders_callback_and_async_boundaries()
    {
        var manifest = CreateManifest();
        var callbackFact = FactFactory.Create(
            manifest,
            FactTypes.CallbackBoundary,
            RuleIds.CSharpSemanticFlowBoundary,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan("src/Handler.cs", 18, 18, null, "test", "test/1.0"),
            targetSymbol: "System.Action<Request>",
            contractElement: "CallbackBoundary",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["boundaryKind"] = "CallbackBoundary"
            });
        var asyncFact = FactFactory.Create(
            manifest,
            FactTypes.AsyncBoundary,
            RuleIds.CSharpSemanticFlowBoundary,
            EvidenceTiers.Tier1Semantic,
            new EvidenceSpan("src/Handler.cs", 24, 24, null, "test", "test/1.0"),
            targetSymbol: "System.Threading.Tasks.Task",
            contractElement: "AwaitBoundary",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["boundaryKind"] = "AwaitBoundary"
            });

        var report = MarkdownReportWriter.Build(new ScanResult(manifest, [callbackFact, asyncFact], []));

        Assert.Contains("## Flow Boundaries", report);
        Assert.Contains("`CallbackBoundary` `CallbackBoundary`", report);
        Assert.Contains("`AsyncBoundary` `AwaitBoundary`", report);
    }

    private static ScanManifest CreateManifest()
    {
        return new ScanManifest(
            "scan-test",
            "repo",
            null,
            null,
            "abc123",
            "test",
            DateTimeOffset.UnixEpoch,
            "Level1SemanticAnalysis",
            "Succeeded",
            [],
            [],
            [],
            []);
    }
}
