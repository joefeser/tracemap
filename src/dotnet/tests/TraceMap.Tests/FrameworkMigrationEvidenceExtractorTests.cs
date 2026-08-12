using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class FrameworkMigrationEvidenceExtractorTests
{
    private static readonly string[] DeclarationKeys =
    [
        "coverageLabel", "declarationKind", "frameworkFamily", "limitations", "migrationTypeName",
        "providerScope", "targetAssemblyIdentity", "targetSymbolId", "targetSymbolKind"
    ];

    private static readonly HashSet<string> CommonOperationKeys = new(
    [
        "coverageLabel", "direction", "frameworkFamily", "invocationOrdinal", "limitations",
        "migrationTypeSymbolId", "objectKind", "operationKind", "providerScope",
        "sourceAssemblyIdentity", "sourceSymbolId", "sourceSymbolKind", "targetAssemblyIdentity",
        "targetSymbolId", "targetSymbolKind"
    ], StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedIdentityKeys = new(
    [
        "schemaName", "tableName", "newSchemaName", "newTableName", "columnName",
        "newColumnName", "indexName", "constraintName", "principalSchemaName",
        "principalTableName", "columnNames", "principalColumnNames"
    ], StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedGapKeys = new(
    [
        "coverageLabel", "direction", "frameworkFamily", "gapKind", "limitations",
        "migrationTypeSymbolId", "occurrenceCount", "operationKind", "providerScope",
        "sourceAssemblyIdentity", "sourceSymbolId", "sourceSymbolKind"
    ], StringComparer.Ordinal);

    [Fact]
    public void Trusted_ef_metadata_emits_closed_declaration_operations_and_aggregated_safe_gaps()
    {
        const string protectedValue = "SELECT credential_value FROM private_server";
        var (facts, gaps, _) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;

            namespace Sample;

            public sealed class AccountMigration : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    const string Table = "accounts";
                    migrationBuilder.AddColumn<int>(name: "status", table: Table, schema: "archive", defaultValue: 7);
                    migrationBuilder.CreateIndex(name: "ix_accounts_status", table: Table, columns: new[] { "status", "kind" });
                    migrationBuilder.Sql("SELECT credential_value FROM private_server");
                    migrationBuilder.Sql("SELECT credential_value FROM private_server");
                    Helper(migrationBuilder);
                }

                protected override void Down(MigrationBuilder migrationBuilder) =>
                    migrationBuilder.DropColumn(name: "status", table: "accounts", schema: "archive");

                private static void Helper(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropTable(name: "accounts", schema: "archive");
                }
            }

            public static class OrdinaryHelper
            {
                public static void Run(MigrationBuilder migrationBuilder) =>
                    migrationBuilder.DropTable(name: "must_not_emit");
            }
            """);

        var declaration = Assert.Single(facts, fact => fact.FactType == FactTypes.FrameworkMigrationDeclared);
        Assert.Equal(RuleIds.DatabaseFrameworkMigrationDeclaration, declaration.RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, declaration.EvidenceTier);
        Assert.Null(declaration.SourceSymbol);
        Assert.Equal("global::Sample.AccountMigration", declaration.TargetSymbol);
        Assert.Equal("framework-migration", declaration.ContractElement);
        Assert.Null(declaration.Evidence.SnippetHash);
        Assert.Equal(DeclarationKeys, declaration.Properties!.Keys);
        Assert.Equal("unknown", declaration.Properties["providerScope"]);
        Assert.StartsWith("Fixture, Version=", declaration.Properties["targetAssemblyIdentity"], StringComparison.Ordinal);

        var operations = facts.Where(fact => fact.FactType == FactTypes.FrameworkMigrationOperationCandidate).ToArray();
        Assert.Equal(4, operations.Length);
        Assert.Contains(operations, fact =>
            fact.ContractElement == "add-column"
            && fact.Properties!.GetValueOrDefault("columnName") == "status"
            && fact.Properties!.GetValueOrDefault("tableName") == "accounts"
            && fact.Properties!.GetValueOrDefault("schemaName") == "archive"
            && fact.Properties!.GetValueOrDefault("direction") == "up");
        Assert.Contains(operations, fact =>
            fact.ContractElement == "create-index"
            && fact.Properties!.GetValueOrDefault("columnNames") == "[\"status\",\"kind\"]");
        Assert.Contains(operations, fact => fact.ContractElement == "drop-column" && fact.Properties!.GetValueOrDefault("direction") == "down");
        Assert.Contains(operations, fact => fact.ContractElement == "drop-table" && fact.Properties!.GetValueOrDefault("direction") == "unknown");
        Assert.DoesNotContain(operations, fact => fact.Properties!.Values.Contains("must_not_emit", StringComparer.Ordinal));
        Assert.All(operations, fact =>
        {
            Assert.Equal(RuleIds.DatabaseFrameworkMigrationOperation, fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier1Semantic, fact.EvidenceTier);
            Assert.Equal("unknown", fact.Properties!["providerScope"]);
            Assert.Null(fact.Evidence.SnippetHash);
            Assert.Equal("Method", fact.Properties["sourceSymbolKind"]);
            Assert.Equal("Method", fact.Properties["targetSymbolKind"]);
            Assert.Contains("Microsoft.EntityFrameworkCore.Relational", fact.Properties["targetAssemblyIdentity"], StringComparison.Ordinal);
            Assert.All(fact.Properties.Keys, key => Assert.True(CommonOperationKeys.Contains(key) || AllowedIdentityKeys.Contains(key), $"Unexpected operation property: {key}"));
            Assert.DoesNotContain(protectedValue, string.Join("\n", fact.Properties.Values), StringComparison.Ordinal);
        });

        var migrationGaps = gaps.Where(gap => gap.RuleId == RuleIds.DatabaseFrameworkMigrationGap).ToArray();
        var rawSqlGap = Assert.Single(migrationGaps, gap => gap.Properties!.GetValueOrDefault("gapKind") == "RawSqlMigrationOperationUnavailable");
        Assert.Equal("2", rawSqlGap.Properties!["occurrenceCount"]);
        Assert.Null(rawSqlGap.Evidence.SnippetHash);
        Assert.Contains(migrationGaps, gap => gap.Properties!.GetValueOrDefault("gapKind") == "DefaultOrComputedExpressionUnavailable");
        Assert.Contains(migrationGaps, gap => gap.Properties!.GetValueOrDefault("gapKind") == "MigrationDirectionUnavailable");
        Assert.DoesNotContain(protectedValue, string.Join("\n", migrationGaps.SelectMany(gap => gap.Properties!.Values)), StringComparison.Ordinal);
        Assert.All(migrationGaps, gap =>
        {
            Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
            Assert.Equal(gap.Properties!["gapKind"], gap.ContractElement);
            Assert.Null(gap.TargetSymbol);
            Assert.Equal("reduced-static-migration", gap.Properties["coverageLabel"]);
            Assert.All(gap.Properties.Keys, key => Assert.Contains(key, AllowedGapKeys));
        });
    }

    [Fact]
    public void Source_declared_framework_lookalike_is_rejected_with_identity_gap()
    {
        var (facts, gaps, _) = Extract("""
            namespace Microsoft.EntityFrameworkCore.Migrations
            {
                public abstract class Migration { }
                public sealed class MigrationBuilder
                {
                    public void DropTable(string name) { }
                }
            }

            namespace Sample
            {
                using Microsoft.EntityFrameworkCore.Migrations;
                public sealed class Forged : Migration
                {
                    public void Up(MigrationBuilder builder) => builder.DropTable("accounts");
                }
            }
            """, includeEfReferences: false);

        Assert.DoesNotContain(facts, fact => fact.FactType is FactTypes.FrameworkMigrationDeclared or FactTypes.FrameworkMigrationOperationCandidate);
        var gap = Assert.Single(gaps, fact => fact.RuleId == RuleIds.DatabaseFrameworkMigrationGap);
        Assert.Equal("FrameworkAssemblyIdentityUnavailable", gap.Properties!["gapKind"]);
        Assert.DoesNotContain("migrationTypeSymbolId", gap.Properties.Keys);
    }

    [Fact]
    public void Source_declared_type_is_rejected_even_with_the_framework_strong_name_identity()
    {
        var publicKey = typeof(Migration).Assembly.GetName().GetPublicKey();
        Assert.NotNull(publicKey);
        var tree = CSharpSyntaxTree.ParseText("""
            namespace Microsoft.EntityFrameworkCore.Migrations;
            public abstract class Migration { }
            """);
        var compilation = CSharpCompilation.Create(
            "Microsoft.EntityFrameworkCore.Relational",
            [tree],
            PlatformReferences(includeEfReferences: false),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithCryptoPublicKey(publicKey.ToImmutableArray())
                .WithPublicSign(true));
        var type = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Migrations.Migration");

        Assert.NotNull(type);
        Assert.Equal("adb9793829ddae60", string.Concat(
            compilation.Assembly.Identity.PublicKeyToken.Select(value => value.ToString("x2"))));
        Assert.False(FrameworkMigrationEvidenceExtractor.IsTrustedEfSymbol(type));
    }

    [Fact]
    public void Unavailable_symbol_gap_order_is_independent_of_host_syntax_tree_path()
    {
        const string source = """
            namespace Sample;
            public sealed class First : Migration { }
            public sealed class Second : Migration { }
            """;
        var (_, first, _) = Extract(
            source,
            includeEfReferences: false,
            allowCompilationErrors: true,
            syntaxTreePath: "/host-a/private/Migration.cs");
        var (_, second, _) = Extract(
            source,
            includeEfReferences: false,
            allowCompilationErrors: true,
            syntaxTreePath: "C:\\host-b\\private\\Migration.cs");

        Assert.Equal(first.Select(GapProjection), second.Select(GapProjection));
    }

    [Fact]
    public void Unsigned_same_name_metadata_assembly_is_rejected()
    {
        var forgedTree = CSharpSyntaxTree.ParseText("""
            namespace Microsoft.EntityFrameworkCore.Migrations;
            public abstract class Migration
            {
                protected abstract void Up(MigrationBuilder builder);
                protected abstract void Down(MigrationBuilder builder);
            }
            public sealed class MigrationBuilder
            {
                public void DropTable(string name) { }
            }
            """);
        var forgedCompilation = CSharpCompilation.Create(
            "Microsoft.EntityFrameworkCore.Relational",
            [forgedTree],
            PlatformReferences(includeEfReferences: false),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emit = forgedCompilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        var forgedReference = MetadataReference.CreateFromImage(stream.ToArray());

        var (facts, gaps, _) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class Forged : Migration
            {
                protected override void Up(MigrationBuilder builder) => builder.DropTable("accounts");
                protected override void Down(MigrationBuilder builder) { }
            }
            """, includeEfReferences: false, additionalReferences: [forgedReference]);

        Assert.DoesNotContain(facts, fact => fact.FactType is FactTypes.FrameworkMigrationDeclared or FactTypes.FrameworkMigrationOperationCandidate);
        Assert.Contains(gaps, gap => gap.Properties!.GetValueOrDefault("gapKind") == "FrameworkAssemblyIdentityUnavailable");
    }

    [Fact]
    public void Materialized_framework_facts_are_deterministic_and_manifest_bound()
    {
        var (firstFacts, firstGaps, _) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                protected override void Up(MigrationBuilder b) { b.DropTable("one"); b.DropTable("two"); }
                protected override void Down(MigrationBuilder b) { }
            }
            """);
        var (secondFacts, secondGaps, _) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                protected override void Up(MigrationBuilder b) { b.DropTable("one"); b.DropTable("two"); }
                protected override void Down(MigrationBuilder b) { }
            }
            """);
        var manifest = Manifest();
        var first = CSharpSemanticExtractor.MaterializeFacts(manifest, firstFacts.Concat(firstGaps));
        var second = CSharpSemanticExtractor.MaterializeFacts(manifest, secondFacts.Concat(secondGaps));

        Assert.Equal(first.Select(fact => fact.FactId), second.Select(fact => fact.FactId));
        Assert.All(first, fact =>
        {
            Assert.Equal(manifest.ScanId, fact.ScanId);
            Assert.Equal(manifest.RepoName, fact.Repo);
            Assert.Equal(manifest.CommitSha, fact.CommitSha);
            Assert.Equal("src/Sample.csproj", fact.ProjectPath);
            Assert.Equal(ScannerVersions.CSharpSemanticExtractor, fact.Evidence.ExtractorVersion);
        });
        Assert.Equal(["1", "2"], first
            .Where(fact => fact.FactType == FactTypes.FrameworkMigrationOperationCandidate)
            .Select(fact => fact.Properties["invocationOrdinal"]));
    }

    [Fact]
    public void Operation_ordinals_ignore_unrelated_and_non_emitted_invocations()
    {
        var (facts, _, _) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                private static void Helper() { }
                private static string DynamicName() => "runtime";
                protected override void Up(MigrationBuilder b)
                {
                    Helper();
                    b.DropTable(DynamicName());
                    b.DropTable("one");
                    Helper();
                    b.DropColumn(name: "status", table: "one");
                }
                protected override void Down(MigrationBuilder b) { }
            }
            """);

        Assert.Equal(["1", "2"], facts
            .Where(fact => fact.FactType == FactTypes.FrameworkMigrationOperationCandidate)
            .Select(fact => fact.Properties!["invocationOrdinal"]));
    }

    [Fact]
    public void Protected_spans_remove_overlapping_generic_semantic_hash_facts()
    {
        var facts = new List<SemanticFactCandidate>
        {
            new(
                FactTypes.ArgumentPassed,
                RuleIds.CSharpSemanticValueFlow,
                EvidenceTiers.Tier1Semantic,
                new EvidenceSpan("src/Migration.cs", 8, 8, null, "test", "test/1"),
                Properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["argumentExpressionHash"] = "prohibited-digest"
                },
                SourceStart: 100,
                SourceLength: 20),
            new(
                FactTypes.MethodInvoked,
                RuleIds.CSharpSemanticMethodInvocation,
                EvidenceTiers.Tier1Semantic,
                new EvidenceSpan("src/Migration.cs", 9, 9, null, "test", "test/1"),
                SourceStart: 200,
                SourceLength: 10)
        };

        CSharpSemanticExtractor.RemoveProtectedSemanticFacts(
            facts,
            0,
            "src/Migration.cs",
            [new ProtectedSourceSpan("src/Migration.cs", 90, 40)]);

        Assert.Single(facts);
        Assert.Equal(200, facts[0].SourceStart);
        Assert.DoesNotContain(facts, fact => fact.Properties?.ContainsKey("argumentExpressionHash") == true);
    }

    [Fact]
    public void Unresolved_protected_call_still_marks_syntax_span_and_emits_only_a_gap()
    {
        var (facts, gaps, protectedSpans) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                protected override void Up(MigrationBuilder b) =>
                    b.Sql("SELECT secret FROM private_table", unsupported: true);
                protected override void Down(MigrationBuilder b) { }
            }
            """, allowCompilationErrors: true);

        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.FrameworkMigrationOperationCandidate);
        Assert.Contains(gaps, gap => gap.Properties!["gapKind"] == "RawSqlMigrationOperationUnavailable");
        Assert.Single(protectedSpans);
        Assert.True(protectedSpans[0].Length > 0);
    }

    [Fact]
    public void Unresolved_supported_sensitive_shapes_and_annotation_are_protected()
    {
        var (_, gaps, protectedSpans) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                protected override void Up(MigrationBuilder b)
                {
                    b.CreateTable(name: "audit", columns: t => new { Secret = "hidden" }, unsupported: true);
                    b.AddColumn<int>(name: "status", table: "accounts", defaultValue: "hidden", unsupported: true);
                    b.Annotation("key", "hidden", unsupported: true);
                }
                protected override void Down(MigrationBuilder b) { }
            }
            """, allowCompilationErrors: true);

        Assert.Equal(3, protectedSpans.Count);
        Assert.Contains(gaps, gap => gap.Properties!["gapKind"] == "SemanticBindingUnavailable");
        Assert.Contains(gaps, gap => gap.Properties!["gapKind"] == "AnnotationMigrationOperationUnavailable");
    }

    [Fact]
    public void Conditional_access_annotation_chain_is_protected()
    {
        var (_, gaps, protectedSpans) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                protected override void Up(MigrationBuilder b) =>
                    b.CreateTable(name: "audit", columns: table => new { Id = table.Column<int>() })
                        ?.Annotation("private-name", "private-value");
                protected override void Down(MigrationBuilder b) { }
            }
            """, allowCompilationErrors: true);

        Assert.Contains(gaps, gap => gap.Properties!["gapKind"] == "AnnotationMigrationOperationUnavailable");
        Assert.Contains(protectedSpans, span => span.Length > 0);
    }

    [Fact]
    public void Unsupported_framework_operation_protects_arbitrary_argument_expression()
    {
        var (_, gaps, protectedSpans) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                protected override void Up(MigrationBuilder b) => b.EnsureSchema(GetPrivateSchema());
                protected override void Down(MigrationBuilder b) { }
                private static string GetPrivateSchema() => "private-schema";
            }
            """);

        Assert.Single(protectedSpans);
        Assert.Contains(gaps, gap => gap.Properties!["gapKind"] == "UnsupportedMigrationOperation");
    }

    [Fact]
    public void Dynamic_identity_gap_protects_the_supported_operation_invocation()
    {
        var (facts, gaps, protectedSpans) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                protected override void Up(MigrationBuilder b) => b.DropTable(DynamicName());
                protected override void Down(MigrationBuilder b) { }
                private static string DynamicName() => "private-table";
            }
            """);

        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.FrameworkMigrationOperationCandidate);
        Assert.Contains(gaps, gap => gap.Properties!["gapKind"] == "DynamicIdentityUnavailable");
        Assert.Single(protectedSpans);
    }

    [Fact]
    public void Unresolved_migration_base_protects_bounded_syntax_candidate_operations()
    {
        const string source = """
            namespace Sample;
            public sealed class M : Migration
            {
                public void Up(object b)
                {
                    b.Sql("SELECT secret FROM private_table");
                    b.CreateTable("audit", table => new { Secret = "protected-value" });
                }
            }
            """;
        var (facts, gaps, protectedSpans) = Extract(
            source,
            includeEfReferences: false,
            allowCompilationErrors: true);

        Assert.Empty(facts);
        Assert.Contains(gaps, gap => gap.Properties!["gapKind"] == "SemanticBindingUnavailable");
        Assert.Equal(2, protectedSpans.Count);

        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Migration.cs"), source);
        var inventory = new[] { new FileInventoryItem("Migration.cs", "CSharp", source.Length) };
        var normalizedSpans = protectedSpans.Select(span => span with { FilePath = "Migration.cs" }).ToArray();
        var syntaxFacts = CSharpSyntaxExtractor.Extract(temp.Path, Manifest(), inventory, normalizedSpans);
        var integrationFacts = CSharpIntegrationSyntaxExtractor.Extract(
            temp.Path,
            Manifest(),
            inventory,
            new HashSet<string>(StringComparer.Ordinal),
            normalizedSpans);

        Assert.DoesNotContain(syntaxFacts, fact => fact.FactType == FactTypes.ObjectShapeInferred);
        Assert.DoesNotContain(integrationFacts, fact => fact.FactType is FactTypes.SqlTextUsed or FactTypes.QueryPatternDetected);
        Assert.DoesNotContain("protected-value", string.Join("\n", syntaxFacts.SelectMany(fact => fact.Properties.Values)), StringComparison.Ordinal);
    }

    [Fact]
    public void Syntax_fallback_protects_migration_content_when_no_file_has_semantic_coverage()
    {
        const string source = """
            using MigrationBase = Microsoft.EntityFrameworkCore.Migrations.Migration;
            namespace Sample;
            public sealed class M : MigrationBase
            {
                public void Up(object b)
                {
                    b?.Sql("SELECT fallback_secret FROM private_table");
                    b.CreateTable("audit", table => new { Secret = "fallback-protected" });
                }
            }
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Migration.cs"), source);
        var inventory = new[] { new FileInventoryItem("Migration.cs", "CSharp", source.Length) };

        var fallback = FrameworkMigrationEvidenceExtractor.ExtractSyntaxFallback(
            temp.Path,
            inventory,
            new HashSet<string>(StringComparer.Ordinal));
        var syntaxFacts = CSharpSyntaxExtractor.Extract(temp.Path, Manifest(), inventory, fallback.ProtectedSpans);
        var integrationFacts = CSharpIntegrationSyntaxExtractor.Extract(
            temp.Path,
            Manifest(),
            inventory,
            new HashSet<string>(StringComparer.Ordinal),
            fallback.ProtectedSpans);

        Assert.Equal(2, fallback.ProtectedSpans.Count);
        Assert.Single(fallback.Gaps, gap => gap.RuleId == RuleIds.DatabaseFrameworkMigrationGap);
        Assert.DoesNotContain(syntaxFacts, fact => fact.FactType == FactTypes.ObjectShapeInferred);
        Assert.DoesNotContain(integrationFacts, fact => fact.FactType is FactTypes.SqlTextUsed or FactTypes.QueryPatternDetected);
        Assert.DoesNotContain("fallback-protected", string.Join("\n", syntaxFacts.SelectMany(fact => fact.Properties.Values)), StringComparison.Ordinal);
    }

    [Fact]
    public void Protected_ranges_filter_later_legacy_csharp_evidence()
    {
        const string source = """
            using System.Runtime.Remoting.Channels.Tcp;
            namespace Sample;
            public sealed class M : Migration
            {
                public void Up(object b) => b.AddColumn("payload", "accounts", defaultValue: new TcpChannel("private-channel"));
            }
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Migration.cs"), source);
        var inventory = new[] { new FileInventoryItem("Migration.cs", "CSharp", source.Length) };
        var fallback = FrameworkMigrationEvidenceExtractor.ExtractSyntaxFallback(
            temp.Path,
            inventory,
            new HashSet<string>(StringComparer.Ordinal));
        var rawFacts = LegacyRemotingExtractor.Extract(temp.Path, Manifest(), inventory, [], semanticAttempted: false);
        var lineRanges = ScanEngine.BuildProtectedLineRanges(temp.Path, fallback.ProtectedSpans);
        var filteredFacts = ScanEngine.FilterProtectedEvidence(rawFacts, lineRanges).ToArray();

        Assert.Contains(rawFacts, fact => fact.Properties.ContainsKey("valueHash"));
        Assert.DoesNotContain(filteredFacts, fact => fact.Properties.ContainsKey("valueHash"));
        Assert.DoesNotContain("private-channel", string.Join("\n", filteredFacts.SelectMany(fact => fact.Properties.Values)), StringComparison.Ordinal);
    }

    [Fact]
    public void Syntax_fallback_does_not_classify_application_types_ending_in_migration()
    {
        const string source = """
            namespace Sample;
            public abstract class DataMigration { }
            public sealed class M : DataMigration
            {
                public void Run(object b) => b.Sql("SELECT ordinary_application_text");
            }
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Migration.cs"), source);

        var fallback = FrameworkMigrationEvidenceExtractor.ExtractSyntaxFallback(
            temp.Path,
            [new FileInventoryItem("Migration.cs", "CSharp", source.Length)],
            new HashSet<string>(StringComparer.Ordinal));

        Assert.Empty(fallback.ProtectedSpans);
        Assert.Empty(fallback.Gaps);
    }

    [Fact]
    public void Protected_defaults_suppress_nested_integration_syntax_facts()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                protected override void Up(MigrationBuilder b) =>
                    b.AddColumn<object>(name: "payload", table: "accounts", defaultValue: queue.SendToQueue("private-destination"));
                protected override void Down(MigrationBuilder b) { }
                private static readonly Queue queue = new();
            }
            public sealed class Queue { public object SendToQueue(string value) => value; }
            """;
        var (_, _, spans) = Extract(source);
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Migration.cs"), source);

        var facts = CSharpIntegrationSyntaxExtractor.Extract(
            temp.Path,
            Manifest(),
            [new FileInventoryItem("Migration.cs", "CSharp", source.Length)],
            new HashSet<string>(StringComparer.Ordinal),
            spans.Select(span => span with { FilePath = "Migration.cs" }).ToArray());

        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.MessagePublisherSurface);
        Assert.DoesNotContain("private-destination", string.Join("\n", facts.SelectMany(fact => fact.Properties.Values)), StringComparison.Ordinal);
    }

    [Fact]
    public void Protected_table_shape_suppresses_nested_message_attributes()
    {
        const string source = """
            namespace Sample;
            public sealed class M : Migration
            {
                public void Up(object b) =>
                    b.CreateTable("audit", ([QueueTrigger("private-queue")] object table) => new { Id = 1 });
            }
            public sealed class QueueTriggerAttribute(string value) : System.Attribute;
            """;
        var tree = CSharpSyntaxTree.ParseText(source);
        var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(node => node.Expression.ToString().EndsWith("CreateTable", StringComparison.Ordinal));
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Migration.cs"), source);

        var facts = CSharpIntegrationSyntaxExtractor.Extract(
            temp.Path,
            Manifest(),
            [new FileInventoryItem("Migration.cs", "CSharp", source.Length)],
            new HashSet<string>(StringComparer.Ordinal),
            [new ProtectedSourceSpan("Migration.cs", invocation.SpanStart, invocation.Span.Length)]);

        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.MessageBindingDeclared);
        Assert.DoesNotContain("private-queue", string.Join("\n", facts.SelectMany(fact => fact.Properties.Values)), StringComparison.Ordinal);
    }

    [Fact]
    public void Framework_facts_round_trip_through_sqlite_without_protected_content()
    {
        const string sentinel = "SELECT credential FROM private_host";
        var (facts, gaps, _) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                protected override void Up(MigrationBuilder b)
                {
                    b.DropTable("accounts");
                    b.Sql("SELECT credential FROM private_host");
                }
                protected override void Down(MigrationBuilder b) { }
            }
            """);
        var manifest = Manifest();
        var materialized = CSharpSemanticExtractor.MaterializeFacts(manifest, facts.Concat(gaps));
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(indexPath, manifest, materialized);

        using var connection = new SqliteConnection($"Data Source={indexPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT fact_type, rule_id, evidence_tier, properties_json FROM facts WHERE fact_type LIKE 'FrameworkMigration%' OR rule_id = $gap ORDER BY fact_id";
        command.Parameters.AddWithValue("$gap", RuleIds.DatabaseFrameworkMigrationGap);
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add(string.Join("\u001f", reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, row => row.Contains(FactTypes.FrameworkMigrationDeclared, StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains(FactTypes.FrameworkMigrationOperationCandidate, StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("RawSqlMigrationOperationUnavailable", StringComparison.Ordinal));
        Assert.DoesNotContain(sentinel, string.Join("\n", rows), StringComparison.Ordinal);
        connection.Close();
        SqliteConnection.ClearAllPools();
        Assert.DoesNotContain(sentinel, System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(indexPath)), StringComparison.Ordinal);
    }

    [Fact]
    public void Closed_operation_vocabulary_maps_required_identity_without_provider_claims()
    {
        var (facts, gaps, _) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class AllOperations : Migration
            {
                protected override void Up(MigrationBuilder b)
                {
                    b.CreateTable(name: "orders", columns: table => new { Id = table.Column<int>() });
                    b.AddColumn<int>(name: "status", table: "orders");
                    b.AlterColumn<int>(name: "status", table: "orders");
                    b.DropTable(name: "old_orders");
                    b.DropColumn(name: "old_status", table: "orders");
                    b.RenameTable(name: "orders", newName: "archived_orders");
                    b.RenameColumn(name: "status", newName: "archive_status", table: "archived_orders");
                    b.CreateIndex(name: "ix_status", table: "archived_orders", column: "archive_status");
                    b.DropIndex(name: "ix_old", table: "archived_orders");
                    b.AddForeignKey(name: "fk_owner", table: "archived_orders", column: "owner_id", principalTable: "owners", principalColumn: "id");
                    b.DropForeignKey(name: "fk_old", table: "archived_orders");
                }
                protected override void Down(MigrationBuilder b) { }
            }
            """);

        var operations = facts.Where(fact => fact.FactType == FactTypes.FrameworkMigrationOperationCandidate).ToArray();
        Assert.Equal(
            new[]
            {
                "add-column", "add-foreign-key", "alter-column", "create-index", "create-table",
                "drop-column", "drop-foreign-key", "drop-index", "drop-table", "rename-column", "rename-table"
            },
            operations.Select(fact => fact.ContractElement!).OrderBy(value => value, StringComparer.Ordinal));
        Assert.All(operations, fact => Assert.Equal("unknown", fact.Properties!["providerScope"]));
        Assert.DoesNotContain(operations, fact => fact.Properties!.Keys.Any(key => key.Contains("postgres", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(gaps, gap => gap.Properties!.GetValueOrDefault("gapKind") == "NestedTableShapeUnavailable");
        Assert.DoesNotContain(gaps, gap => gap.Properties!.GetValueOrDefault("gapKind") is "MissingRequiredIdentity" or "DynamicIdentityUnavailable");
    }

    [Fact]
    public void Protected_dynamic_and_unsupported_shapes_emit_only_closed_categorical_gaps()
    {
        const string sentinel = "credential-bearing-seed";
        var (facts, gaps, _) = Extract("""
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class GapShapes : Migration
            {
                private static string DynamicName() => "runtime_name";
                protected override void Up(MigrationBuilder b)
                {
                    var columns = new[] { "runtime_column" };
                    b.DropTable(DynamicName());
                    b.RenameTable(name: "orders");
                    b.CreateIndex(name: "ix_dynamic", table: "orders", columns: columns);
                    b.InsertData(table: "orders", column: "payload", value: "credential-bearing-seed");
                    b.EnsureSchema(name: "archive");
                    b.CreateTable(name: "audit", columns: table => new { Id = table.Column<int>() })
                        .Annotation("provider-private", "credential-bearing-seed");
                }
                protected override void Down(MigrationBuilder b) { }
            }
            """);

        var kinds = gaps.Where(gap => gap.RuleId == RuleIds.DatabaseFrameworkMigrationGap)
            .Select(gap => gap.Properties!["gapKind"])
            .ToHashSet(StringComparer.Ordinal);
        foreach (var expected in new[]
        {
            "DynamicIdentityUnavailable",
            "MissingRequiredIdentity",
            "IndexColumnShapeUnavailable",
            "DataMigrationOperationUnavailable",
            "UnsupportedMigrationOperation",
            "NestedTableShapeUnavailable",
            "AnnotationMigrationOperationUnavailable"
        })
        {
            Assert.Contains(expected, kinds);
        }
        Assert.DoesNotContain(facts, fact => fact.ContractElement is "drop-table" or "rename-table");
        Assert.DoesNotContain(sentinel, string.Join("\n", facts.Concat(gaps).SelectMany(fact => fact.Properties!.Values)), StringComparison.Ordinal);
        Assert.All(facts.Concat(gaps).Where(fact => fact.RuleId.StartsWith("database.framework-migration.", StringComparison.Ordinal)),
            fact => Assert.Null(fact.Evidence.SnippetHash));
    }

    [Fact]
    public void Rule_catalog_documents_framework_migration_boundaries()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var ruleId in new[]
        {
            RuleIds.DatabaseFrameworkMigrationDeclaration,
            RuleIds.DatabaseFrameworkMigrationOperation,
            RuleIds.DatabaseFrameworkMigrationGap
        })
        {
            Assert.Contains($"- id: {ruleId}", catalog, StringComparison.Ordinal);
        }
        Assert.Contains("generated SQL", catalog, StringComparison.Ordinal);
        Assert.Contains("provider selection", catalog, StringComparison.Ordinal);
        Assert.Contains("safe to run", catalog, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Protected_migration_sql_is_not_projected_by_overlapping_sql_text_or_shape_rules()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore.Migrations;
            namespace Sample;
            public sealed class M : Migration
            {
                protected override void Up(MigrationBuilder b)
                {
                    b.Sql("SELECT secret FROM private_table");
                    b.AddColumn<int>(name: "status", table: "accounts", defaultValueSql: "SELECT hidden_default FROM private_table");
                    b.CreateTable(name: "audit", columns: table => new { Id = table.Column<int>(defaultValueSql: "SELECT hidden_nested FROM private_table") });
                }
                protected override void Down(MigrationBuilder b) { }
            }
            """;
        var (_, gaps, protectedSpans) = Extract(source);
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Migration.cs"), source);

        var facts = CSharpIntegrationSyntaxExtractor.Extract(
            temp.Path,
            Manifest(),
            [new FileInventoryItem("Migration.cs", "CSharp", source.Length)],
            new HashSet<string>(["Migration.cs"], StringComparer.Ordinal),
            protectedSpans.Select(span => span with { FilePath = "Migration.cs" }).ToArray());

        Assert.Contains(gaps, gap => gap.Properties!.GetValueOrDefault("gapKind") == "RawSqlMigrationOperationUnavailable");
        Assert.DoesNotContain(facts, fact => fact.FactType is FactTypes.SqlTextUsed or FactTypes.QueryPatternDetected);
        Assert.DoesNotContain("SELECT", string.Join("\n", facts.SelectMany(fact => fact.Properties.Values)), StringComparison.Ordinal);
    }

    private static (
        IReadOnlyList<SemanticFactCandidate> Facts,
        IReadOnlyList<SemanticFactCandidate> Gaps,
        IReadOnlyList<ProtectedSourceSpan> ProtectedSpans) Extract(
        string source,
        bool includeEfReferences = true,
        IReadOnlyList<MetadataReference>? additionalReferences = null,
        bool allowCompilationErrors = false,
        string syntaxTreePath = "src/Migration.cs")
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: syntaxTreePath);
        var references = PlatformReferences(includeEfReferences).ToList();
        if (includeEfReferences)
        {
            var directory = Path.GetDirectoryName(typeof(Migration).Assembly.Location)!;
            references.AddRange(Directory.GetFiles(directory, "Microsoft.EntityFrameworkCore*.dll")
                .Select(path => MetadataReference.CreateFromFile(path)));
        }
        if (additionalReferences is not null)
        {
            references.AddRange(additionalReferences);
        }
        var compilation = CSharpCompilation.Create(
            "Fixture",
            [tree],
            references.DistinctBy(reference => reference.Display, StringComparer.Ordinal),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (!allowCompilationErrors)
        {
            Assert.Empty(errors);
        }
        var facts = new List<SemanticFactCandidate>();
        var gaps = new List<SemanticFactCandidate>();
        var protectedSpans = new List<ProtectedSourceSpan>();
        FrameworkMigrationEvidenceExtractor.Extract(
            "src/Sample.csproj",
            "src/Migration.cs",
            tree.GetRoot(),
            compilation.GetSemanticModel(tree),
            facts,
            gaps,
            protectedSpans);
        return (facts, gaps, protectedSpans);
    }

    private static string GapProjection(SemanticFactCandidate gap) =>
        string.Join("\u001f", gap.ContractElement, gap.Evidence.FilePath, gap.Evidence.StartLine,
            string.Join("\u001e", gap.Properties!.Select(pair => $"{pair.Key}={pair.Value}")));

    private static IReadOnlyList<MetadataReference> PlatformReferences(bool includeEfReferences = true) =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Where(path => includeEfReferences
                || !Path.GetFileName(path).StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToArray();

    private static ScanManifest Manifest() => new(
        "scan-framework",
        "sample",
        "https://example.test/sample.git",
        "main",
        "abc123",
        ScannerVersions.TraceMap,
        DateTimeOffset.Parse("2026-08-11T00:00:00Z"),
        "Level1SemanticAnalysis",
        "Succeeded",
        [],
        ["src/Sample.csproj"],
        ["net10.0"],
        []);

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
}
