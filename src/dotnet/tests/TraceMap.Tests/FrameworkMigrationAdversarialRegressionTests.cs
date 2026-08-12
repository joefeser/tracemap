using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore.Migrations;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class FrameworkMigrationAdversarialRegressionTests
{
    [Fact]
    public void Same_named_migrations_in_different_source_assemblies_keep_distinct_canonical_identity()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Shared;
            public sealed class RenameAccounts : Migration
            {
                protected override void Up(MigrationBuilder builder) => builder.DropTable("accounts");
                protected override void Down(MigrationBuilder builder) { }
            }
            """;

        var first = Extract(new Dictionary<string, string> { ["src/Migration.cs"] = source }, "First.Migrations");
        var second = Extract(new Dictionary<string, string> { ["src/Migration.cs"] = source }, "Second.Migrations");
        var firstOperation = Assert.Single(first.Facts, IsOperation);
        var secondOperation = Assert.Single(second.Facts, IsOperation);

        Assert.Equal(firstOperation.SourceSymbol, secondOperation.SourceSymbol);
        Assert.NotEqual(firstOperation.Properties!["migrationTypeSymbolId"], secondOperation.Properties!["migrationTypeSymbolId"]);
        Assert.NotEqual(firstOperation.Properties["sourceSymbolId"], secondOperation.Properties["sourceSymbolId"]);
        Assert.NotEqual(firstOperation.Properties["sourceAssemblyIdentity"], secondOperation.Properties["sourceAssemblyIdentity"]);

        var firstId = Assert.Single(Materialize(first), IsOperation).FactId;
        var secondId = Assert.Single(Materialize(second), IsOperation).FactId;
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public void Overloads_and_reordered_named_arguments_bind_to_their_exact_parameters()
    {
        var result = Extract(new Dictionary<string, string>
        {
            ["src/Migration.cs"] = """
                using Microsoft.EntityFrameworkCore.Migrations;
                public sealed class IndexMigration : Migration
                {
                    protected override void Up(MigrationBuilder builder)
                    {
                        builder.CreateIndex(table: "orders", column: "id", name: "ix_scalar");
                        builder.CreateIndex(columns: new[] { "tenant", "id" }, name: "ix_multi", table: "orders");
                        builder.DropIndex(table: "orders", name: "ix_old");
                    }
                    protected override void Down(MigrationBuilder builder) { }
                }
                """
        });

        var operations = result.Facts.Where(IsOperation).ToArray();
        Assert.Equal(3, operations.Length);
        var scalar = Assert.Single(operations, fact => fact.Properties!["indexName"] == "ix_scalar");
        var multiple = Assert.Single(operations, fact => fact.Properties!["indexName"] == "ix_multi");
        var drop = Assert.Single(operations, fact => fact.ContractElement == "drop-index");

        Assert.Equal("[\"id\"]", scalar.Properties!["columnNames"]);
        Assert.Equal("[\"tenant\",\"id\"]", multiple.Properties!["columnNames"]);
        Assert.Equal("orders", drop.Properties!["tableName"]);
        Assert.NotEqual(scalar.Properties["targetSymbolId"], multiple.Properties["targetSymbolId"]);
        Assert.DoesNotContain(result.Gaps, gap => gap.Properties!["gapKind"] is "MissingRequiredIdentity" or "DynamicIdentityUnavailable");
    }

    [Fact]
    public void File_enumeration_order_does_not_change_materialized_framework_evidence()
    {
        var sources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/First.cs"] = MigrationSource("FirstMigration", "first_table"),
            ["src/Second.cs"] = MigrationSource("SecondMigration", "second_table")
        };

        var forward = Materialize(Extract(sources, extractionOrder: ["src/First.cs", "src/Second.cs"]));
        var reverse = Materialize(Extract(sources, extractionOrder: ["src/Second.cs", "src/First.cs"]));

        Assert.Equal(Project(forward), Project(reverse));
        Assert.Equal(4, forward.Count);
    }

    [Fact]
    public void Application_lookalike_operations_inside_a_real_migration_do_not_become_framework_evidence()
    {
        var result = Extract(new Dictionary<string, string>
        {
            ["src/Migration.cs"] = """
                using Microsoft.EntityFrameworkCore.Migrations;
                public sealed class FakeBuilder { public void DropTable(string name) { } }
                public static class FakeExtensions { public static void DropTable(this FakeBuilder builder, string name) { } }
                public sealed class RealMigration : Migration
                {
                    protected override void Up(MigrationBuilder builder)
                    {
                        new FakeBuilder().DropTable("lookalike_instance");
                        FakeExtensions.DropTable(new FakeBuilder(), "lookalike_static");
                        builder.DropTable("real_table");
                    }
                    protected override void Down(MigrationBuilder builder) { }
                }
                """
        });

        var operation = Assert.Single(result.Facts, IsOperation);
        Assert.Equal("real_table", operation.Properties!["tableName"]);
        Assert.DoesNotContain("lookalike", string.Join('\n', result.Facts.Concat(result.Gaps).SelectMany(fact => fact.Properties!.Values)), StringComparison.Ordinal);
    }

    [Fact]
    public void Helper_and_local_function_operations_remain_unknown_direction_with_separate_gaps()
    {
        var result = Extract(new Dictionary<string, string>
        {
            ["src/Migration.cs"] = """
                using Microsoft.EntityFrameworkCore.Migrations;
                public sealed class DirectionMigration : Migration
                {
                    protected override void Up(MigrationBuilder builder)
                    {
                        void Local() => builder.DropTable("local_table");
                        Local();
                        Helper(builder);
                        builder.DropTable("up_table");
                    }
                    private static void Helper(MigrationBuilder builder) => builder.DropTable("helper_table");
                    protected override void Down(MigrationBuilder builder) { }
                }
                """
        });

        var operations = result.Facts.Where(IsOperation).ToArray();
        Assert.Equal("up", Assert.Single(operations, fact => fact.Properties!["tableName"] == "up_table").Properties!["direction"]);
        Assert.Equal("unknown", Assert.Single(operations, fact => fact.Properties!["tableName"] == "local_table").Properties!["direction"]);
        Assert.Equal("unknown", Assert.Single(operations, fact => fact.Properties!["tableName"] == "helper_table").Properties!["direction"]);

        var directionGaps = result.Gaps.Where(gap => gap.Properties!["gapKind"] == "MigrationDirectionUnavailable").ToArray();
        Assert.Equal(2, directionGaps.Length);
        Assert.Equal(2, directionGaps.Select(gap => gap.Properties!["sourceSymbolId"]).Distinct(StringComparer.Ordinal).Count());
        Assert.All(directionGaps, gap =>
        {
            Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
            Assert.Equal("unknown", gap.Properties!["direction"]);
            Assert.Equal("drop-table", gap.Properties["operationKind"]);
        });
    }

    [Fact]
    public void Compiler_folded_constants_are_retained_but_partial_arrays_and_lambdas_are_gapped()
    {
        var result = Extract(new Dictionary<string, string>
        {
            ["src/Migration.cs"] = """
                using Microsoft.EntityFrameworkCore.Migrations;
                public sealed class ShapeMigration : Migration
                {
                    private const string Prefix = "arch";
                    private const string Suffix = "ive";
                    private const string StableColumn = "tenant";
                    private static string RuntimeColumn() => "runtime";
                    protected override void Up(MigrationBuilder builder)
                    {
                        builder.DropTable(name: Prefix + Suffix, schema: nameof(ShapeMigration));
                        builder.CreateIndex(name: "ix_partial", table: "orders", columns: new[] { StableColumn, RuntimeColumn() });
                        builder.CreateTable(name: "audit", columns: table => new { Id = table.Column<int>() });
                    }
                    protected override void Down(MigrationBuilder builder) { }
                }
                """
        });

        var folded = Assert.Single(result.Facts, fact => IsOperation(fact) && fact.ContractElement == "drop-table");
        Assert.Equal("archive", folded.Properties!["tableName"]);
        Assert.Equal("ShapeMigration", folded.Properties["schemaName"]);

        var partialArray = Assert.Single(result.Facts, fact => IsOperation(fact) && fact.ContractElement == "create-index");
        Assert.DoesNotContain("columnNames", partialArray.Properties!.Keys);
        Assert.Contains(result.Gaps, gap => gap.Properties!["gapKind"] == "IndexColumnShapeUnavailable");
        Assert.Contains(result.Gaps, gap => gap.Properties!["gapKind"] == "NestedTableShapeUnavailable");
        Assert.All(result.Facts.Concat(result.Gaps), fact => Assert.Null(fact.Evidence.SnippetHash));
    }

    [Fact]
    public void Semantic_unavailable_candidate_emits_only_a_bounded_rule_backed_gap()
    {
        using var temp = new TempDirectory();
        const string source = """
            using Microsoft.EntityFrameworkCore.Migrations;
            public sealed class MissingBinding : Migration
            {
                public void Up(object builder) => builder.Sql("private_value");
            }
            """;
        File.WriteAllText(Path.Combine(temp.Path, "Migration.cs"), source);

        var result = FrameworkMigrationEvidenceExtractor.ExtractSyntaxFallback(
            temp.Path,
            [new FileInventoryItem("Migration.cs", "CSharp", source.Length)],
            new HashSet<string>(StringComparer.Ordinal));

        var gap = Assert.Single(result.Gaps);
        Assert.Equal(FactTypes.AnalysisGap, gap.FactType);
        Assert.Equal(RuleIds.DatabaseFrameworkMigrationGap, gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal("SemanticBindingUnavailable", gap.ContractElement);
        Assert.Null(gap.SourceSymbol);
        Assert.Null(gap.TargetSymbol);
        Assert.Equal("Migration.cs", gap.Evidence.FilePath);
        Assert.Equal(2, gap.Evidence.StartLine);
        Assert.True(gap.Evidence.EndLine >= gap.Evidence.StartLine);
        Assert.Equal("FrameworkMigrationSyntaxFallbackExtractor", gap.Evidence.ExtractorId);
        Assert.Single(result.ProtectedSpans);
    }

    [Fact]
    public void Rule_catalog_keeps_exact_tiers_emissions_and_non_claims_for_each_framework_rule()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        var declaration = RuleBlock(catalog, RuleIds.DatabaseFrameworkMigrationDeclaration);
        var operation = RuleBlock(catalog, RuleIds.DatabaseFrameworkMigrationOperation);
        var gap = RuleBlock(catalog, RuleIds.DatabaseFrameworkMigrationGap);

        Assert.Contains("evidenceTier: Tier1Semantic", declaration, StringComparison.Ordinal);
        Assert.Contains("- FrameworkMigrationDeclared", declaration, StringComparison.Ordinal);
        Assert.Contains("provider selection", declaration, StringComparison.Ordinal);
        Assert.Contains("evidenceTier: Tier1Semantic", operation, StringComparison.Ordinal);
        Assert.Contains("- FrameworkMigrationOperationCandidate", operation, StringComparison.Ordinal);
        Assert.Contains("safe to run", operation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("evidenceTier: Tier4Unknown", gap, StringComparison.Ordinal);
        Assert.Contains("- AnalysisGap", gap, StringComparison.Ordinal);
        Assert.Contains("Raw SQL", gap, StringComparison.Ordinal);
        Assert.Contains("their digests are not retained", gap, StringComparison.Ordinal);
    }

    private static ExtractionResult Extract(
        IReadOnlyDictionary<string, string> sources,
        string assemblyName = "Fixture",
        IReadOnlyList<string>? extractionOrder = null)
    {
        var trees = sources.ToDictionary(
            pair => pair.Key,
            pair => CSharpSyntaxTree.ParseText(pair.Value, path: pair.Key),
            StringComparer.Ordinal);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            trees.Values,
            PlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));

        var facts = new List<SemanticFactCandidate>();
        var gaps = new List<SemanticFactCandidate>();
        var protectedSpans = new List<ProtectedSourceSpan>();
        foreach (var path in extractionOrder ?? sources.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray())
        {
            var tree = trees[path];
            FrameworkMigrationEvidenceExtractor.Extract(
                "src/Sample.csproj",
                path,
                tree.GetRoot(),
                compilation.GetSemanticModel(tree),
                facts,
                gaps,
                protectedSpans);
        }
        return new ExtractionResult(facts, gaps, protectedSpans);
    }

    private static IReadOnlyList<CodeFact> Materialize(ExtractionResult result) =>
        CSharpSemanticExtractor.MaterializeFacts(Manifest(), result.Facts.Concat(result.Gaps));

    private static string[] Project(IEnumerable<CodeFact> facts) => facts
        .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
        .Select(fact => string.Join("\u001f", fact.FactId, fact.Evidence.FilePath, fact.Evidence.StartLine,
            string.Join("\u001e", fact.Properties.Select(pair => $"{pair.Key}={pair.Value}"))))
        .ToArray();

    private static bool IsOperation(SemanticFactCandidate fact) =>
        fact.FactType == FactTypes.FrameworkMigrationOperationCandidate;

    private static bool IsOperation(CodeFact fact) =>
        fact.FactType == FactTypes.FrameworkMigrationOperationCandidate;

    private static string MigrationSource(string typeName, string tableName) => $$"""
        using Microsoft.EntityFrameworkCore.Migrations;
        public sealed class {{typeName}} : Migration
        {
            protected override void Up(MigrationBuilder builder) => builder.DropTable("{{tableName}}");
            protected override void Down(MigrationBuilder builder) { }
        }
        """;

    private static IReadOnlyList<MetadataReference> PlatformReferences()
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();
        var efDirectory = Path.GetDirectoryName(typeof(Migration).Assembly.Location)!;
        references.AddRange(Directory.GetFiles(efDirectory, "Microsoft.EntityFrameworkCore*.dll")
            .Select(path => MetadataReference.CreateFromFile(path)));
        return references.DistinctBy(reference => reference.Display, StringComparer.Ordinal).ToArray();
    }

    private static ScanManifest Manifest() => new(
        "scan-framework-regressions",
        "sample",
        "https://example.test/sample.git",
        "main",
        "abc123",
        ScannerVersions.TraceMap,
        DateTimeOffset.Parse("2026-08-12T00:00:00Z"),
        "Level1SemanticAnalysis",
        "Succeeded",
        [],
        ["src/Sample.csproj"],
        ["net10.0"],
        []);

    private static string RuleBlock(string catalog, string ruleId)
    {
        var start = catalog.IndexOf($"  - id: {ruleId}", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Rule {ruleId} was not found.");
        var next = catalog.IndexOf("\n  - id: ", start + 1, StringComparison.Ordinal);
        return next < 0 ? catalog[start..] : catalog[start..next];
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "rules", "rule-catalog.yml")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed record ExtractionResult(
        IReadOnlyList<SemanticFactCandidate> Facts,
        IReadOnlyList<SemanticFactCandidate> Gaps,
        IReadOnlyList<ProtectedSourceSpan> ProtectedSpans);
}
