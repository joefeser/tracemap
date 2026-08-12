using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class FrameworkMigrationCompositionTests
{
    [Fact]
    public async Task Database_design_review_projects_provider_unknown_framework_migrations_with_provenance_and_gaps()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "after.sqlite");
        var output = Path.Combine(temp.Path, "design-review");
        var manifest = Manifest("2222222");
        var declaration = FrameworkDeclaration(manifest);
        var operation = FrameworkOperation(manifest);
        var upstreamGap = FrameworkGap(manifest);
        var incompatible = FrameworkOperation(manifest) with
        {
            FactId = "fact-incompatible-framework",
            Evidence = Evidence(16) with { ExtractorId = "unknown", ExtractorVersion = "unknown" }
        };
        SqliteIndexWriter.Write(index, manifest, [declaration, operation, upstreamGap, incompatible]);

        var result = await DatabaseDesignReviewReporter.WriteAsync(new DatabaseDesignReviewOptions(index, output));

        var declared = Assert.Single(result.Report.GlobalObjects, item => item.EvidenceKind == "framework-migration");
        Assert.Equal("StaticEvidence", declared.Classification);
        Assert.Equal(RuleIds.DatabaseFrameworkMigrationDeclaration, declared.Evidence.RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, declared.Evidence.EvidenceTier);
        Assert.Equal(manifest.CommitSha, declared.Evidence.CommitSha);
        Assert.Equal("framework-migration/0.1.0", declared.Evidence.ExtractorVersion);
        Assert.Contains(declaration.FactId, declared.Evidence.SupportingFactIds);
        Assert.Contains(declared.Evidence.Limitations, limitation => limitation.Contains("provider", StringComparison.OrdinalIgnoreCase));

        var projectedOperation = Assert.Single(result.Report.GlobalObjects, item => item.EvidenceKind == "framework-migration-operation");
        Assert.Equal("ReviewRecommended", projectedOperation.Classification);
        Assert.Equal("add-column", projectedOperation.DisplayName);
        Assert.Contains(projectedOperation.Metadata, pair => pair.Key == "providerScope" && pair.Value == "unknown");
        Assert.Empty(result.Report.Tables);

        var providerGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "FrameworkMigrationProviderUnknown");
        Assert.Equal(DatabaseDesignReviewReporter.GapRuleId, providerGap.RuleId);
        Assert.Contains(RuleIds.DatabaseFrameworkMigrationOperation, providerGap.SupportingRuleIds);
        Assert.Contains(operation.FactId, providerGap.SupportingFactIds);
        Assert.Equal("Migrations/AddStatus.cs", providerGap.FilePath);
        var rawSqlGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "RawSqlMigrationOperationUnavailable");
        Assert.Equal(RuleIds.DatabaseFrameworkMigrationGap, rawSqlGap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, rawSqlGap.EvidenceTier);
        Assert.Contains(upstreamGap.FactId, rawSqlGap.SupportingFactIds);
        var provenanceGap = Assert.Single(result.Report.Gaps, gap => gap.GapKind == "FrameworkMigrationEvidenceProvenanceUnavailable");
        Assert.DoesNotContain("PostgreSQL", provenanceGap.Message, StringComparison.Ordinal);
        Assert.Contains(incompatible.FactId, provenanceGap.SupportingFactIds);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "CompatiblePostgresEvidenceUnavailable");
        Assert.Equal("partial", result.Report.Coverage);

        var rendered = await File.ReadAllTextAsync(Path.Combine(output, "database-design-review.md"))
            + await File.ReadAllTextAsync(Path.Combine(output, "database-design-review.json"));
        Assert.DoesNotContain("private-source-symbol-sentinel", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("safe to run", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider-unknown", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Release_review_reads_framework_migrations_without_postgres_claims(bool combined)
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var output = Path.Combine(temp.Path, "release-review");
        var before = Manifest("1111111");
        var after = Manifest("2222222");
        var declaration = FrameworkDeclaration(after);
        var operation = FrameworkOperation(after);
        var upstreamGap = FrameworkGap(after);
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [declaration, operation, upstreamGap]);
        var reportBefore = beforeIndex;
        var reportAfter = afterIndex;
        var sourceLabel = "single";
        if (combined)
        {
            reportBefore = Path.Combine(temp.Path, "before-combined.sqlite");
            reportAfter = Path.Combine(temp.Path, "after-combined.sqlite");
            sourceLabel = "framework";
            await CombinedIndexBuilder.CombineAsync(new CombineOptions([beforeIndex], reportBefore, [sourceLabel]));
            await CombinedIndexBuilder.CombineAsync(new CombineOptions([afterIndex], reportAfter, [sourceLabel]));
        }

        var result = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            reportBefore,
            reportAfter,
            output,
            Scope: "sql-evidence",
            Source: sourceLabel));

        Assert.Equal(ReleaseReviewStatuses.Available, result.Report.SqlEvidence.Status);
        var declared = Assert.Single(result.Report.SqlEvidence.Findings, finding => finding.RuleId == RuleIds.DatabaseFrameworkMigrationDeclaration);
        Assert.Equal(ReleaseReviewClassifications.NoActionableEvidence, declared.Classification);
        Assert.Equal(sourceLabel, declared.SourceLabel);
        Assert.Equal(after.CommitSha, declared.CommitSha);
        Assert.Contains(declared.SupportingFactIds, factId => factId.EndsWith(declaration.FactId, StringComparison.Ordinal));
        Assert.Contains(declared.Limitations, limitation => limitation.Contains("provider", StringComparison.OrdinalIgnoreCase));

        var projectedOperation = Assert.Single(result.Report.SqlEvidence.Findings, finding => finding.RuleId == RuleIds.DatabaseFrameworkMigrationOperation);
        Assert.Equal(ReleaseReviewClassifications.ReviewRecommended, projectedOperation.Classification);
        Assert.Equal("add-column", projectedOperation.DisplayName);
        Assert.Contains(projectedOperation.Metadata, pair => pair.Key == "providerScope" && pair.Value == "unknown");
        var providerGap = Assert.Single(result.Report.SqlEvidence.Gaps, item => item.GapKind == "FrameworkMigrationProviderUnknown");
        Assert.Equal(RuleIds.DatabaseFrameworkMigrationOperation, providerGap.RuleId);
        Assert.Contains(providerGap.SupportingFactIds, factId => factId.EndsWith(operation.FactId, StringComparison.Ordinal));
        var gap = Assert.Single(result.Report.SqlEvidence.Gaps, item => item.GapKind == "RawSqlMigrationOperationUnavailable");
        Assert.Equal(RuleIds.DatabaseFrameworkMigrationGap, gap.RuleId);
        Assert.Contains(gap.SupportingFactIds, factId => factId.EndsWith(upstreamGap.FactId, StringComparison.Ordinal));

        var rendered = await File.ReadAllTextAsync(Path.Combine(output, "release-review.md"))
            + await File.ReadAllTextAsync(Path.Combine(output, "release-review.json"));
        Assert.DoesNotContain("private-source-symbol-sentinel", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("safe to run", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not prove PostgreSQL selection", rendered, StringComparison.Ordinal);
    }

    private static ScanManifest Manifest(string commitSha) =>
        new(
            $"scan-framework-{commitSha}",
            "framework-repo",
            null,
            "dev",
            commitSha,
            ScannerVersions.TraceMap,
            DateTimeOffset.Parse("2026-08-12T00:00:00Z"),
            "Level1SemanticAnalysisReduced",
            "Succeeded",
            [],
            [],
            [],
            [],
            ".",
            FactFactory.Hash("framework-repo", 32),
            FactFactory.Hash("framework-repo-git-root", 32));

    private static CodeFact FrameworkDeclaration(ScanManifest manifest) =>
        FactFactory.Create(
            manifest,
            FactTypes.FrameworkMigrationDeclared,
            RuleIds.DatabaseFrameworkMigrationDeclaration,
            EvidenceTiers.Tier1Semantic,
            Evidence(4),
            sourceSymbol: "private-source-symbol-sentinel",
            properties: Properties(
                ("declarationKind", "migration-type"),
                ("frameworkFamily", "ef-core"),
                ("providerScope", "unknown"),
                ("coverageLabel", "bounded-static-framework-migration"),
                ("limitations", "Static framework migration declaration; provider and execution are not proven.")));

    private static CodeFact FrameworkOperation(ScanManifest manifest) =>
        FactFactory.Create(
            manifest,
            FactTypes.FrameworkMigrationOperationCandidate,
            RuleIds.DatabaseFrameworkMigrationOperation,
            EvidenceTiers.Tier1Semantic,
            Evidence(8),
            sourceSymbol: "private-source-symbol-sentinel",
            properties: Properties(
                ("frameworkFamily", "ef-core"),
                ("providerScope", "unknown"),
                ("direction", "up"),
                ("operationKind", "add-column"),
                ("objectKind", "column"),
                ("invocationOrdinal", "1"),
                ("schemaName", "public"),
                ("tableName", "orders"),
                ("columnName", "status"),
                ("coverageLabel", "bounded-static-framework-migration"),
                ("limitations", "Static application-side operation; provider, generated SQL, ordering, execution, and safety are not proven.")));

    private static CodeFact FrameworkGap(ScanManifest manifest) =>
        FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DatabaseFrameworkMigrationGap,
            EvidenceTiers.Tier4Unknown,
            Evidence(12),
            sourceSymbol: "private-source-symbol-sentinel",
            properties: Properties(
                ("gapKind", "RawSqlMigrationOperationUnavailable"),
                ("frameworkFamily", "ef-core"),
                ("providerScope", "unknown"),
                ("operationKind", "sql"),
                ("direction", "up"),
                ("occurrenceCount", "1"),
                ("coverageLabel", "reduced-static-framework-migration"),
                ("limitations", "Protected SQL content is omitted; migration behavior is unavailable.")));

    private static EvidenceSpan Evidence(int line) =>
        new(
            "Migrations/AddStatus.cs",
            line,
            line,
            FactFactory.Hash($"framework:{line}", 32),
            "framework-migration",
            "framework-migration/0.1.0");

    private static IReadOnlyDictionary<string, string> Properties(params (string Key, string Value)[] pairs) =>
        new SortedDictionary<string, string>(pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), StringComparer.Ordinal);
}
