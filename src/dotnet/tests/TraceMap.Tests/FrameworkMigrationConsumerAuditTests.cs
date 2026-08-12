using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class FrameworkMigrationConsumerAuditTests
{
    private const string ProtectedSymbol = "private-framework-migration-symbol-sentinel";
    private const string DeclarationLimitation = "Static framework migration declaration only; execution, ordering, provider selection, generated SQL, database state, rollback, and safety are not proven.";
    private const string OperationLimitation = "Static framework migration operation candidate only; execution, ordering, provider selection, generated SQL, database state, rollback, reversibility, and safety are not proven.";
    private const string GapLimitation = "Static framework migration coverage is reduced; omitted protected content and runtime behavior were not analyzed.";

    [Fact]
    public void Markdown_report_counts_framework_migration_facts_without_rendering_properties()
    {
        var manifest = Manifest('a');
        var facts = FrameworkFacts(manifest);

        var report = MarkdownReportWriter.Build(new ScanResult(manifest, facts, []));

        Assert.Contains($"`{FactTypes.FrameworkMigrationDeclared}`: `1`", report, StringComparison.Ordinal);
        Assert.Contains($"`{FactTypes.FrameworkMigrationOperationCandidate}`: `1`", report, StringComparison.Ordinal);
        Assert.Contains($"`{FactTypes.AnalysisGap}`: `1`", report, StringComparison.Ordinal);
        Assert.DoesNotContain(ProtectedSymbol, report, StringComparison.Ordinal);
        Assert.DoesNotContain(DeclarationLimitation, report, StringComparison.Ordinal);
        Assert.DoesNotContain("bounded-static-migration", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explorer_preserves_valid_framework_metadata_and_omits_invalid_rows_with_a_gap()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "scan");
        var output = Path.Combine(temp.Path, "explorer");
        Directory.CreateDirectory(input);
        var manifest = Manifest('b');
        var validOperation = FrameworkFacts(manifest).Single(fact => fact.RuleId == RuleIds.DatabaseFrameworkMigrationOperation);
        var invalidFacts = new[]
        {
            InvalidFrameworkOperation(manifest),
            validOperation with { FactId = "invalid-tier", EvidenceTier = EvidenceTiers.Tier4Unknown },
            validOperation with { FactId = "invalid-commit", CommitSha = new string('f', 40) },
            validOperation with { FactId = "invalid-span", Evidence = validOperation.Evidence with { StartLine = 0 } },
            validOperation with { FactId = "invalid-extractor", Evidence = validOperation.Evidence with { ExtractorVersion = "framework-migration/9.9.9" } }
        };
        var facts = FrameworkFacts(manifest).Concat(invalidFacts).ToArray();
        await ManifestWriter.WriteAsync(Path.Combine(input, "scan-manifest.json"), manifest);
        await JsonlFactWriter.WriteAsync(Path.Combine(input, "facts.ndjson"), facts);
        await File.WriteAllTextAsync(Path.Combine(input, "index.sqlite"), "fixture");
        await File.WriteAllTextAsync(Path.Combine(input, "report.md"), "# fixture\n");

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new StaticHtmlEvidenceExplorerOptions(input, output));

        var declaration = Assert.Single(result.Data.EvidenceRows, row => row.RuleId == RuleIds.DatabaseFrameworkMigrationDeclaration);
        Assert.Equal("bounded-static-migration", declaration.CoverageLabel);
        Assert.Equal([DeclarationLimitation], declaration.Limitations);
        var operation = Assert.Single(result.Data.EvidenceRows, row => row.RuleId == RuleIds.DatabaseFrameworkMigrationOperation);
        Assert.Equal([OperationLimitation], operation.Limitations);
        var gapRow = Assert.Single(result.Data.EvidenceRows, row => row.RuleId == RuleIds.DatabaseFrameworkMigrationGap);
        Assert.Equal("reduced-static-migration", gapRow.CoverageLabel);
        Assert.Equal([GapLimitation], gapRow.Limitations);
        var metadataGaps = result.Data.Gaps
            .Where(gap => gap.RuleId == StaticHtmlEvidenceExplorer.FrameworkMigrationMetadataUnavailableRuleId)
            .ToArray();
        Assert.Equal(invalidFacts.Length, metadataGaps.Length);
        Assert.All(invalidFacts, invalid =>
        {
            Assert.Contains(metadataGaps, gap => gap.SupportIds.Contains(invalid.FactId, StringComparer.Ordinal));
            Assert.DoesNotContain(result.Data.EvidenceRows, row => row.SupportId == invalid.FactId);
        });

        var generated = string.Join('\n', Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain(ProtectedSymbol, generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vault_and_docs_export_omit_framework_semantics_with_supporting_rule_backed_gaps()
    {
        using var temp = new TempDirectory();
        var combined = await CombinedIndexAsync(temp.Path, Manifest('c'));

        var vault = await VaultExporter.ExportAsync(new VaultExportOptions(combined, Path.Combine(temp.Path, "vault"), Format: "json"));
        var vaultGap = Assert.Single(vault.Graph.Gaps, gap => gap.RuleId == "vault-export.gap.framework-migration-consumer-unsupported.v1");
        Assert.Equal("FrameworkMigrationEvidenceConsumerUnsupported", vaultGap.Classification);
        Assert.NotEmpty(vaultGap.SupportingFactIds ?? []);

        var docs = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(combined, Path.Combine(temp.Path, "docs")));
        var docsGap = Assert.Single(docs.Chunks, chunk => chunk.RuleIds.Contains("docs-export.gap.framework-migration-consumer-unsupported.v1", StringComparer.Ordinal));
        Assert.Equal("gap", docsGap.ChunkFamily);
        Assert.NotEmpty(docsGap.SupportingIds);
        Assert.DoesNotContain(docs.Chunks, chunk =>
            chunk.ChunkFamily == "dependency-surface"
            && chunk.RuleIds.Any(ruleId => ruleId is RuleIds.DatabaseFrameworkMigrationDeclaration or RuleIds.DatabaseFrameworkMigrationOperation));

        var generated = string.Join('\n', Directory.EnumerateFiles(temp.Path, "*", SearchOption.AllDirectories)
            .Where(path => path.Contains("vault", StringComparison.Ordinal) || path.Contains("docs", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        Assert.DoesNotContain(ProtectedSymbol, generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Snapshot_diff_records_framework_projection_gap_for_single_and_combined_indexes()
    {
        using var temp = new TempDirectory();
        var beforeManifest = Manifest('d');
        var afterManifest = Manifest('e');
        var before = Path.Combine(temp.Path, "before.sqlite");
        var after = Path.Combine(temp.Path, "after.sqlite");
        SqliteIndexWriter.Write(before, beforeManifest, FrameworkFacts(beforeManifest));
        SqliteIndexWriter.Write(after, afterManifest, FrameworkFacts(afterManifest));

        var single = await SnapshotDiffReporter.BuildReportAsync(new SnapshotDiffOptions(before, after, Path.Combine(temp.Path, "single")));
        AssertFrameworkSnapshotGap(single);

        var combinedBefore = await CombinedIndexAsync(Path.Combine(temp.Path, "combined-before"), beforeManifest);
        var combinedAfter = await CombinedIndexAsync(Path.Combine(temp.Path, "combined-after"), afterManifest);
        var combined = await SnapshotDiffReporter.BuildReportAsync(new SnapshotDiffOptions(combinedBefore, combinedAfter, Path.Combine(temp.Path, "combined")));
        AssertFrameworkSnapshotGap(combined);

        var scopedBefore = await CombinedIndexWithUnselectedFrameworkAsync(Path.Combine(temp.Path, "scoped-before"), Manifest('1'));
        var scopedAfter = await CombinedIndexWithUnselectedFrameworkAsync(Path.Combine(temp.Path, "scoped-after"), Manifest('2'));
        var scoped = await SnapshotDiffReporter.BuildReportAsync(new SnapshotDiffOptions(
            scopedBefore,
            scopedAfter,
            Path.Combine(temp.Path, "scoped"),
            Source: "selected"));
        Assert.DoesNotContain(scoped.Gaps, gap => gap.RuleId == "snapshot.diff.framework-migration-unsupported.v1");
    }

    private static void AssertFrameworkSnapshotGap(SnapshotDiffDocument report)
    {
        var gap = Assert.Single(report.Gaps, candidate => candidate.RuleId == "snapshot.diff.framework-migration-unsupported.v1");
        Assert.Equal("FrameworkMigrationSnapshotDiffUnsupported", gap.GapKind);
        Assert.Equal(SnapshotDiffClassifications.UnknownAnalysisGap, gap.Classification);
        Assert.NotEmpty(gap.SupportingFactIds);
        Assert.Equal("Partial", report.ReportCoverage);
    }

    private static async Task<string> CombinedIndexAsync(string directory, ScanManifest manifest)
    {
        Directory.CreateDirectory(directory);
        var index = Path.Combine(directory, "source.sqlite");
        var combined = Path.Combine(directory, "combined.sqlite");
        SqliteIndexWriter.Write(index, manifest, FrameworkFacts(manifest));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["framework"]));
        return combined;
    }

    private static async Task<string> CombinedIndexWithUnselectedFrameworkAsync(string directory, ScanManifest frameworkManifest)
    {
        Directory.CreateDirectory(directory);
        var selectedManifest = frameworkManifest with
        {
            ScanId = $"selected-{frameworkManifest.ScanId}",
            RepoName = "selected-repo",
            ScanRootPathHash = FactFactory.Hash("selected-repo", 32),
            GitRootHash = FactFactory.Hash("selected-repo-git", 32)
        };
        var selectedIndex = Path.Combine(directory, "selected.sqlite");
        var frameworkIndex = Path.Combine(directory, "framework.sqlite");
        var combined = Path.Combine(directory, "combined.sqlite");
        SqliteIndexWriter.Write(selectedIndex, selectedManifest, []);
        SqliteIndexWriter.Write(frameworkIndex, frameworkManifest, FrameworkFacts(frameworkManifest));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions(
            [selectedIndex, frameworkIndex],
            combined,
            ["selected", "unselected-framework"]));
        return combined;
    }

    private static ScanManifest Manifest(char commitCharacter) =>
        new(
            $"scan-{commitCharacter}",
            "framework-repo",
            null,
            "dev",
            new string(commitCharacter, 40),
            ScannerVersions.TraceMap,
            DateTimeOffset.Parse("2026-08-12T00:00:00Z"),
            "Level1SemanticAnalysis",
            "Succeeded",
            [],
            [],
            [],
            [],
            ".",
            FactFactory.Hash("framework-repo", 32),
            FactFactory.Hash("framework-repo-git-root", 32));

    private static IReadOnlyList<CodeFact> FrameworkFacts(ScanManifest manifest) =>
    [
        Fact(manifest, FactTypes.FrameworkMigrationDeclared, RuleIds.DatabaseFrameworkMigrationDeclaration, EvidenceTiers.Tier1Semantic, 4,
            ("coverageLabel", "bounded-static-migration"), ("limitations", DeclarationLimitation)),
        Fact(manifest, FactTypes.FrameworkMigrationOperationCandidate, RuleIds.DatabaseFrameworkMigrationOperation, EvidenceTiers.Tier1Semantic, 8,
            ("coverageLabel", "bounded-static-migration"), ("limitations", OperationLimitation)),
        Fact(manifest, FactTypes.AnalysisGap, RuleIds.DatabaseFrameworkMigrationGap, EvidenceTiers.Tier4Unknown, 12,
            ("coverageLabel", "reduced-static-migration"), ("limitations", GapLimitation), ("gapKind", "RawSqlMigrationOperationUnavailable"))
    ];

    private static CodeFact InvalidFrameworkOperation(ScanManifest manifest) =>
        Fact(manifest, FactTypes.FrameworkMigrationOperationCandidate, RuleIds.DatabaseFrameworkMigrationOperation, EvidenceTiers.Tier1Semantic, 16,
            ("coverageLabel", "unexpected"), ("limitations", "unexpected"));

    private static CodeFact Fact(
        ScanManifest manifest,
        string factType,
        string ruleId,
        string evidenceTier,
        int line,
        params (string Key, string Value)[] properties) =>
        FactFactory.Create(
            manifest,
            factType,
            ruleId,
            evidenceTier,
            new EvidenceSpan(
                "Migrations/AddStatus.cs",
                line,
                line,
                FactFactory.Hash($"framework:{line}", 32),
                "FrameworkMigrationEvidenceExtractor",
                ScannerVersions.FrameworkMigrationEvidenceExtractor),
            sourceSymbol: ProtectedSymbol,
            properties: new SortedDictionary<string, string>(
                properties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                StringComparer.Ordinal));
}
