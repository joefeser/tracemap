using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class SourceMetadataReconciliationTests
{
    [Fact]
    public void Bound_csharp_fixture_reconciles_exact_complete_identities()
    {
        var repo = FindRepoRoot();
        var source = Path.Combine(repo, "samples", "compiled-dotnet-evidence", "csharp");
        var assembly = Path.Combine(source, "bin", "Debug", "net10.0", "CompiledEvidence.CSharp.dll");

        var result = ScanBound(source, [assembly]);
        var edges = result.Facts.Where(fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled).ToArray();
        var fixtureCase = ReadCase("CS-RECON-EXACT-001");

        Assert.NotEmpty(edges);
        Assert.Contains(edges, edge => edge.SourceSymbol == fixtureCase.SourceIdentity && edge.TargetSymbol == fixtureCase.MetadataIdentity);
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("|method:8:Overload|", StringComparison.Ordinal)
            && edge.TargetSymbol.Contains("names:5:Int32", StringComparison.Ordinal));
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("|constructor:5:.ctor|", StringComparison.Ordinal));
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("|property:4:Name|", StringComparison.Ordinal));
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("|event:7:Changed|", StringComparison.Ordinal));
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("8:Nested`1|arity:2|method:4:Echo|", StringComparison.Ordinal)
            && edge.TargetSymbol.Contains("|(!0,!1)->!1", StringComparison.Ordinal));
        Assert.Equal(2, edges.Count(edge => edge.TargetSymbol!.Contains("|method:8:RefShape|", StringComparison.Ordinal)));
        Assert.Equal(2, edges.Count(edge => edge.TargetSymbol!.Contains("|method:12:GenericArity|", StringComparison.Ordinal)));
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("|method:6:Shapes|", StringComparison.Ordinal)
            && edge.TargetSymbol.Contains("scope(assembly:name:14:System.Runtime", StringComparison.Ordinal)
            && edge.TargetSymbol.Contains("type(namespace:6:System|names:4:Guid)", StringComparison.Ordinal));
        foreach (var caseId in new[]
                 {
                     "CS-RECON-REF-006",
                     "CS-RECON-NESTED-GENERIC-007",
                     "CS-RECON-DECIMAL-SCOPE-010",
                     "CS-RECON-CONSTRUCTED-NESTED-011",
                     "CS-RECON-DATETIME-SCOPE-012",
                     "CS-RECON-NONGENERIC-NESTED-013",
                     "CS-RECON-PRIMARY-CONSTRUCTOR-014",
                     "CS-RECON-REF-FIELD-015",
                     "CS-RECON-TYPEDREFERENCE-016"
                 })
        {
            var expected = ReadCase(caseId);
            Assert.Contains(edges, edge => edge.SourceSymbol == expected.SourceIdentity && edge.TargetSymbol == expected.MetadataIdentity);
        }
        Assert.All(edges, edge =>
        {
            Assert.Equal(RuleIds.DotNetCompiledSourceIdentity, edge.RuleId);
            Assert.Equal(EvidenceTiers.Tier1Semantic, edge.EvidenceTier);
            Assert.Equal("bound", edge.Properties["compiledProvenanceState"]);
            Assert.False(string.IsNullOrWhiteSpace(edge.Properties["sourceFactId"]));
            Assert.False(string.IsNullOrWhiteSpace(edge.Properties["compiledFactId"]));
            Assert.False(string.IsNullOrWhiteSpace(edge.Properties["provenanceBindingInputSha256"]));
        });
        Assert.Contains(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "SourceMetadataIdentityIncomplete"
            && fact.Properties.GetValueOrDefault("details")!.Contains("SourceFunctionPointerIdentityUnsupported", StringComparison.Ordinal));
        var compiledById = result.Facts.Where(fact => fact.RuleId == RuleIds.DotNetCompiledMember).ToDictionary(fact => fact.FactId, StringComparer.Ordinal);
        Assert.All(edges.Where(edge => compiledById[edge.Properties["compiledFactId"]].Properties.GetValueOrDefault("compilerGenerated") == "true"), edge =>
            Assert.Contains("roslyn-associated-", edge.Properties["relationshipProof"], StringComparison.Ordinal));
    }

    [Fact]
    public void Bound_visual_basic_fixture_reconciles_byref_indexer_optional_constructor_and_explicit_interface_shapes()
    {
        var repo = FindRepoRoot();
        var source = Path.Combine(repo, "samples", "compiled-dotnet-evidence", "vb");
        var assembly = Path.Combine(source, "bin", "Debug", "net10.0", "CompiledEvidence.VisualBasic.dll");

        var result = ScanBound(source, [assembly]);
        var edges = result.Facts.Where(fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled).ToArray();
        var fixtureCase = ReadCase("VB-RECON-EXACT-002");

        Assert.Contains(edges, edge => edge.SourceSymbol == fixtureCase.SourceIdentity && edge.TargetSymbol == fixtureCase.MetadataIdentity);
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("|constructor:5:.ctor|", StringComparison.Ordinal));
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("|property:4:Item|", StringComparison.Ordinal)
            && edge.TargetSymbol.Contains("names:5:Int32", StringComparison.Ordinal));
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("|method:6:Shapes|", StringComparison.Ordinal)
            && edge.TargetSymbol.Contains("Int32)&", StringComparison.Ordinal));
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("OptionalValue", StringComparison.Ordinal)
            && edge.Properties["optionalParameterOrdinals"] == "0");
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("|property:12:OptionalItem|", StringComparison.Ordinal)
            && edge.Properties["optionalParameterOrdinals"] == "0");
        Assert.Equal(2, edges.Count(edge => edge.TargetSymbol!.Contains("|method:12:GenericArity|", StringComparison.Ordinal)));
        foreach (var caseId in new[] { "VB-RECON-OPTIONAL-PROPERTY-008", "VB-RECON-GENERIC-ARITY-009" })
        {
            var expected = ReadCase(caseId);
            Assert.Contains(edges, edge => edge.SourceSymbol == expected.SourceIdentity && edge.TargetSymbol == expected.MetadataIdentity);
        }
        Assert.Contains(edges, edge => edge.TargetSymbol!.Contains("|names:6:Widget|arity:0|method:6:Format|", StringComparison.Ordinal));
    }

    [Fact]
    public void Exact_zero_candidate_emits_gap_and_no_edge()
    {
        var repo = FindRepoRoot();
        var source = Path.Combine(repo, "samples", "compiled-dotnet-evidence", "csharp");
        var otherAssembly = Path.Combine(repo, "samples", "compiled-dotnet-evidence", "vb", "bin", "Debug", "net10.0", "CompiledEvidence.VisualBasic.dll");

        var result = ScanBound(source, [otherAssembly]);

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled);
        Assert.Contains(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "SourceMetadataReconciliationZeroCandidate");
    }

    [Fact]
    public void Exact_multiple_candidates_emit_gap_and_no_edge()
    {
        var repo = FindRepoRoot();
        var source = Path.Combine(repo, "samples", "compiled-dotnet-evidence", "csharp");
        var assembly = Path.Combine(source, "bin", "Debug", "net10.0", "CompiledEvidence.CSharp.dll");
        var duplicateDirectory = Directory.CreateTempSubdirectory("tracemap-duplicate-metadata-");
        var duplicate = Path.Combine(duplicateDirectory.FullName, "duplicate-csharp.dll");
        File.Copy(assembly, duplicate);
        try
        {
            var result = ScanBound(source, [assembly, duplicate]);

            Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled);
            Assert.Contains(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "SourceMetadataReconciliationMultipleCandidates"
                && fact.Properties.GetValueOrDefault("candidateCount") == "2");
        }
        finally
        {
            duplicateDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Unbound_compiled_evidence_cannot_produce_positive_join()
    {
        var repo = FindRepoRoot();
        var source = Path.Combine(repo, "samples", "compiled-dotnet-evidence", "csharp");
        var assembly = Path.Combine(source, "bin", "Debug", "net10.0", "CompiledEvidence.CSharp.dll");

        var result = ScanUnbound(source, [assembly]);

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled);
        Assert.Contains(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "SourceMetadataReconciliationCompiledEvidenceUnacceptable"
            && fact.Properties.GetValueOrDefault("details")!.Contains("CompiledProvenanceUnbound", StringComparison.Ordinal));
    }

    [Fact]
    public void Fsharp_fixture_retains_compiled_identities_and_emits_unsupported_source_gap_without_guessed_join()
    {
        var repo = FindRepoRoot();
        var source = Path.Combine(repo, "samples", "compiled-dotnet-evidence", "fsharp");
        var assembly = Path.Combine(source, "bin", "Debug", "net10.0", "CompiledEvidence.FSharp.dll");

        var result = ScanBound(source, [assembly]);
        var fixtureCase = ReadCase("FS-RECON-UNSUPPORTED-003");

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == fixtureCase.MetadataIdentity);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled);
        var gap = Assert.Single(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "SourceMetadataReconciliationUnsupportedLanguage");
        Assert.Equal("fsharp", gap.Properties["language"]);
        var entry = Assert.Single(result.Manifest.SourceMetadataReconciliation!.Entries);
        Assert.False(string.IsNullOrWhiteSpace(entry.EvidenceFactId));
        Assert.False(string.IsNullOrWhiteSpace(entry.FilePath));
        Assert.False(string.IsNullOrWhiteSpace(entry.CommitSha));
    }

    [Theory]
    [InlineData("Level3SyntaxAnalysis")]
    [InlineData("Level1SemanticAnalysisReduced")]
    public void Reconciliation_summary_is_partial_when_semantic_source_identity_is_unavailable(string analysisLevel)
    {
        var manifest = ReconciliationManifest(analysisLevel);

        var summary = SourceMetadataReconciler.BuildSummary(manifest, []);

        Assert.NotNull(summary);
        Assert.Equal("source-metadata-partial", summary.CoverageState);
        Assert.Equal(0, summary.ExactJoinCount);
        Assert.Equal(0, summary.ExplicitGapCount);
    }

    [Fact]
    public void Reconciliation_summary_bounds_compiled_support_and_commits_every_omitted_entry_field()
    {
        var manifest = ReconciliationManifest("Level1SemanticAnalysis");
        var compiledIds = Enumerable.Range(0, 300).Select(index => $"fact-{index:x20}").ToArray();
        CodeFact Gap(string limitation) => FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DotNetCompiledSourceIdentity,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan("Fixture.cs", 7, 9, null, nameof(SourceMetadataReconciler), ScannerVersions.SourceMetadataReconciliationExtractor),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["details"] = string.Join(',', compiledIds),
                ["gapKind"] = "SourceMetadataReconciliationMultipleCandidates",
                ["limitation"] = limitation,
                ["reconciliationState"] = "unjoined",
                ["sourceIdentity"] = "source:C#|complete",
                ["sourceFactId"] = "fact-aaaaaaaaaaaaaaaaaaaa"
            });

        var retained = SourceMetadataReconciler.BuildSummary(manifest, [Gap("first")])!;
        var firstDigest = SourceMetadataReconciler.BuildSummary(manifest, [Gap("first")], maximumEntries: 0)!;
        var secondDigest = SourceMetadataReconciler.BuildSummary(manifest, [Gap("second")], maximumEntries: 0)!;

        var entry = Assert.Single(retained.Entries);
        Assert.Equal(256, entry.CompiledFactIds.Count);
        Assert.Equal(44, entry.OmittedCompiledFactIdCount);
        Assert.False(string.IsNullOrWhiteSpace(entry.OmittedCompiledFactIdSha256));
        Assert.NotEqual(firstDigest.OmittedEntrySha256, secondDigest.OmittedEntrySha256);
    }

    [Fact]
    public void Roslyn_error_types_emit_incomplete_identity_candidates_instead_of_exact_shapes()
    {
        var tree = CSharpSyntaxTree.ParseText("[assembly:System.Runtime.Versioning.TargetFramework(\".NETCoreApp,Version=v10.0\")] namespace Fixture; public class Sample { public Missing Echo(Missing value) => value; }");
        var compilation = CSharpCompilation.Create(
            "ErrorTypeFixture",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var candidates = new List<SourceMetadataIdentityCandidate>();

        SourceMetadataIdentityCollector.Collect(
            tree.GetRoot(),
            compilation.GetSemanticModel(tree, ignoreAccessibility: true),
            "Fixture.csproj",
            "Fixture.cs",
            LanguageNames.CSharp,
            candidates);

        var candidate = Assert.Single(candidates, item => item.SourceDeclarationIdentity.Contains("Echo", StringComparison.Ordinal));
        Assert.Null(candidate.MetadataIdentity);
        Assert.Equal("SourceErrorTypeIdentityUnavailable", candidate.IncompleteReason);
    }

    [Fact]
    public void Csharp_top_level_statements_are_not_collected_as_unresolved_declarations()
    {
        var tree = CSharpSyntaxTree.ParseText("[assembly:System.Runtime.Versioning.TargetFramework(\".NETCoreApp,Version=v10.0\")]\nSystem.Console.WriteLine(\"fixture\");\npublic sealed class Sample { public int Value { get; set; } }");
        var compilation = CSharpCompilation.Create(
            "TopLevelFixture",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        var candidates = new List<SourceMetadataIdentityCandidate>();

        SourceMetadataIdentityCollector.Collect(
            tree.GetRoot(),
            compilation.GetSemanticModel(tree, ignoreAccessibility: true),
            "Fixture.csproj",
            "Fixture.cs",
            LanguageNames.CSharp,
            candidates);

        Assert.NotEmpty(candidates);
        Assert.DoesNotContain(candidates, candidate => candidate.RelationshipProof == "syntax-located-unresolved-declaration");
    }

    [Theory]
    [InlineData("public class Primary(int value) { }")]
    [InlineData("public struct Primary(int value) { }")]
    [InlineData("public record class Primary(int value);")]
    [InlineData("public record struct Primary(int value);")]
    public void Csharp_primary_constructor_declaration_matrix_collects_complete_constructor_identity(string declaration)
    {
        var candidates = CollectCsharpCandidates(declaration);

        var constructor = Assert.Single(candidates, candidate => candidate.RelationshipProof == "roslyn-primary-constructor");
        Assert.Equal("constructor", constructor.MemberKind);
        Assert.Null(constructor.IncompleteReason);
        Assert.Contains("|constructor:5:.ctor|", constructor.MetadataIdentity, StringComparison.Ordinal);
        Assert.Contains("names:5:Int32", constructor.MetadataIdentity, StringComparison.Ordinal);
    }

    [Fact]
    public void Csharp_field_signature_matrix_encodes_plain_ref_and_fails_closed_for_custom_modifier()
    {
        var candidates = CollectCsharpCandidates("""
            public ref struct Holder
            {
                private ref int writable;
                private volatile int guarded;
                public Holder(ref int value)
                {
                    writable = ref value;
                }
            }
            """);

        var writable = Assert.Single(candidates, candidate => candidate.SourceDeclarationIdentity.Contains("writable", StringComparison.Ordinal));
        Assert.Null(writable.IncompleteReason);
        Assert.EndsWith("names:5:Int32)&", writable.MetadataIdentity, StringComparison.Ordinal);

        var guarded = Assert.Single(candidates, candidate => candidate.SourceDeclarationIdentity.Contains("guarded", StringComparison.Ordinal));
        Assert.Null(guarded.MetadataIdentity);
        Assert.Equal("SourceCustomModifierIdentityUnsupported", guarded.IncompleteReason);
    }

    [Fact]
    public void Syntax_located_unresolved_observations_are_not_tier1_semantic_evidence()
    {
        var manifest = ReconciliationManifest("Level1SemanticAnalysisReduced");
        var candidate = new SourceMetadataIdentityCandidate(
            "C#:unresolved:Fixture.cs:7:0",
            null,
            "declaration",
            LanguageNames.CSharp,
            new EvidenceSpan("Fixture.cs", 7, 7, null, "csharp-semantic", "test"),
            "Fixture.csproj",
            "syntax-located-unresolved-declaration",
            [],
            "C#:unresolved:Fixture.cs:7:0",
            "SourceDeclaredSymbolUnavailable");

        var facts = SourceMetadataReconciler.Reconcile(manifest, [candidate], [], []);

        var observation = Assert.Single(facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityObserved);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, observation.EvidenceTier);
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "SourceMetadataIdentityIncomplete");
    }

    [Fact]
    public void Optional_parameter_mismatch_gap_preserves_rejected_compiled_provenance()
    {
        var manifest = ReconciliationManifest("Level1SemanticAnalysis");
        const string metadataIdentity = "assembly:test|type:test|method:Optional";
        var source = new SourceMetadataIdentityCandidate(
            $"source:C#|{metadataIdentity}",
            metadataIdentity,
            "method",
            LanguageNames.CSharp,
            new EvidenceSpan("Fixture.cs", 7, 7, null, "csharp-semantic", "test"),
            "Fixture.csproj",
            "source-declaration",
            [0],
            "csharp method Fixture.Optional");
        var compiled = FactFactory.Create(
            manifest,
            FactTypes.ManagedMethodDeclared,
            RuleIds.DotNetCompiledMember,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("fixture.dll", 1, 1, null, "managed-metadata", "test"),
            targetSymbol: metadataIdentity,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["optionalParameterOrdinals"] = string.Empty,
                ["provenanceBindingInputSha256"] = new string('d', 64),
                ["provenanceState"] = "bound",
                ["sourceReconciliationEligibility"] = "eligible"
            });

        var facts = SourceMetadataReconciler.Reconcile(manifest, [source], [compiled], []);

        var gap = Assert.Single(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "SourceMetadataOptionalParameterMismatch");
        Assert.Equal("bound", gap.Properties["compiledProvenanceState"]);
        Assert.Equal(new string('d', 64), gap.Properties["provenanceBindingInputSha256"]);
        var summaryEntry = Assert.Single(SourceMetadataReconciler.BuildSummary(manifest, facts)!.Entries);
        Assert.Equal("bound", summaryEntry.CompiledProvenanceState);
        Assert.Equal(new string('d', 64), summaryEntry.ProvenanceBindingInputSha256);
    }

    [Fact]
    public async Task Cli_repeat_scans_preserve_reconciliation_in_all_artifacts_and_operational_receipt()
    {
        var repo = FindRepoRoot();
        var source = Path.Combine(repo, "samples", "compiled-dotnet-evidence", "csharp");
        var assembly = Path.Combine(source, "bin", "Debug", "net10.0", "CompiledEvidence.CSharp.dll");
        var temp = Directory.CreateTempSubdirectory("tracemap-source-metadata-cli-");
        try
        {
            var receipt = Path.Combine(temp.FullName, "binding.json");
            WriteBoundReceipt(source, [assembly], receipt);
            var first = Path.Combine(temp.FullName, "first");
            var second = Path.Combine(temp.FullName, "second");
            Assert.Equal(0, await Run(first));
            Assert.Equal(0, await Run(second));

            foreach (var output in new[] { first, second })
            {
                Assert.True(File.Exists(Path.Combine(output, "scan-manifest.json")));
                Assert.True(File.Exists(Path.Combine(output, "facts.ndjson")));
                Assert.True(File.Exists(Path.Combine(output, "index.sqlite")));
                Assert.True(File.Exists(Path.Combine(output, "report.md")));
                Assert.True(File.Exists(Path.Combine(output, "logs", "analyzer.log")));
                Assert.True(File.Exists(Path.Combine(output, "scan-receipt.json")));
            }
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(first, "facts.ndjson")), await File.ReadAllBytesAsync(Path.Combine(second, "facts.ndjson")));
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(first, "report.md")), await File.ReadAllBytesAsync(Path.Combine(second, "report.md")));

            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(first, "scan-manifest.json")));
            using var secondManifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(second, "scan-manifest.json")));
            var summary = manifest.RootElement.GetProperty("sourceMetadataReconciliation");
            Assert.Equal(summary.GetRawText(), secondManifest.RootElement.GetProperty("sourceMetadataReconciliation").GetRawText());
            Assert.Equal("source-metadata-reconciliation.v1", summary.GetProperty("schemaVersion").GetString());
            Assert.True(summary.GetProperty("exactJoinCount").GetInt32() > 0);
            Assert.Contains(summary.GetProperty("entries").EnumerateArray(), entry => entry.GetProperty("reconciliationState").GetString() == "exact-one-candidate"
                && !string.IsNullOrWhiteSpace(entry.GetProperty("sourceIdentity").GetString())
                && !string.IsNullOrWhiteSpace(entry.GetProperty("metadataIdentity").GetString())
                && !string.IsNullOrWhiteSpace(entry.GetProperty("evidenceFactId").GetString())
                && !string.IsNullOrWhiteSpace(entry.GetProperty("filePath").GetString())
                && !string.IsNullOrWhiteSpace(entry.GetProperty("commitSha").GetString())
                && entry.GetProperty("compiledProvenanceState").GetString() == "bound");

            var operationalReceipt = JsonSerializer.Deserialize<ScanExecutionReceipt>(
                await File.ReadAllTextAsync(Path.Combine(first, "scan-receipt.json")),
                TraceMap.Storage.JsonOptions.Stable)!;
            Assert.Contains(operationalReceipt.SourceMetadataReconciliation!.Entries,
                entry => entry.ReconciliationState == "exact-one-candidate" && entry.CompiledProvenanceState == "bound");
            Assert.Contains("Source/metadata reconciliation", await File.ReadAllTextAsync(Path.Combine(first, "report.md")), StringComparison.Ordinal);

            using var connection = new SqliteConnection($"Data Source={Path.Combine(first, "index.sqlite")}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "select count(*) from facts where rule_id = $rule and fact_type = $type and source_symbol is not null and target_symbol is not null";
            command.Parameters.AddWithValue("$rule", RuleIds.DotNetCompiledSourceIdentity);
            command.Parameters.AddWithValue("$type", FactTypes.SourceMetadataIdentityReconciled);
            Assert.True(Convert.ToInt32(command.ExecuteScalar()) > 0);

            async Task<int> Run(string output) => await TraceMapCommand.RunAsync([
                "scan", "--repo", source, "--out", output,
                "--compiled-input", assembly,
                "--compiled-binding-receipt", receipt
            ], TextWriter.Null, TextWriter.Null);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    private static ScanResult ScanBound(string sourceRepo, IReadOnlyList<string> assemblies)
    {
        var commit = GitMetadataProvider.Detect(sourceRepo).CommitSha;
        var temp = Directory.CreateTempSubdirectory("tracemap-source-metadata-test-");
        var receiptPath = Path.Combine(temp.FullName, "binding-receipt.json");
        var output = Path.Combine(temp.FullName, "out");
        try
        {
            var initial = ManagedMetadataExtractor.Evaluate(sourceRepo, commit,
                new ScanOptions(sourceRepo, output, CompiledInputPaths: assemblies));
            WriteBoundReceipt(receiptPath, commit, initial);
            return ScanEngine.Scan(new ScanOptions(
                sourceRepo,
                output,
                CompiledInputPaths: assemblies,
                CompiledBindingReceiptPaths: [receiptPath]));
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    private static ScanManifest ReconciliationManifest(string analysisLevel) => new(
        "scan-aaaaaaaaaaaaaaaaaaaa",
        "fixture",
        null,
        "test",
        new string('a', 40),
        "test",
        DateTimeOffset.UnixEpoch,
        analysisLevel,
        analysisLevel.StartsWith("Level1", StringComparison.Ordinal) ? "Succeeded" : "NotRun",
        [],
        [],
        [],
        [],
        CompiledInputProvenance: new CompiledInputProvenance(
            "compiled-input-provenance.v1",
            "explicit-managed-input.v1",
            new string('b', 64),
            ["managed-metadata/test"],
            [],
            new CompiledInputLimits(),
            [],
            [],
            new string('c', 64),
            "local-only",
            "compiled-metadata-complete"));

    private static void WriteBoundReceipt(string sourceRepo, IReadOnlyList<string> assemblies, string receiptPath)
    {
        var commit = GitMetadataProvider.Detect(sourceRepo).CommitSha;
        var initial = ManagedMetadataExtractor.Evaluate(sourceRepo, commit,
            new ScanOptions(sourceRepo, "unused", CompiledInputPaths: assemblies));
        WriteBoundReceipt(receiptPath, commit, initial);
    }

    private static void WriteBoundReceipt(string receiptPath, string commit, CompiledInputEvaluation initial) =>
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(new
        {
            schemaVersion = "compiled-input-binding-set.v1",
            bindings = initial.Provenance!.Outcomes.Select(outcome => new
            {
                schemaVersion = "compiled-input-binding.v1",
                safeLocator = outcome.SafeLocator,
                artifactSha256 = outcome.RawFileSha256,
                assemblyIdentity = outcome.AssemblyIdentity,
                binarySourceRepository = "public-fixture",
                binarySourceCommitSha = commit,
                binaryBuildIdentity = "test-build"
            }).ToArray()
        }));

    private static ScanResult ScanUnbound(string sourceRepo, IReadOnlyList<string> assemblies)
    {
        var temp = Directory.CreateTempSubdirectory("tracemap-source-metadata-unbound-");
        try
        {
            return ScanEngine.Scan(new ScanOptions(sourceRepo, Path.Combine(temp.FullName, "out"), CompiledInputPaths: assemblies));
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    private static IReadOnlyList<SourceMetadataIdentityCandidate> CollectCsharpCandidates(string declaration)
    {
        var source = $"""
            [assembly:System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v10.0")]
            namespace Fixture;
            {declaration}
            """;
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create(
            "DeclarationMatrixFixture",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var candidates = new List<SourceMetadataIdentityCandidate>();
        SourceMetadataIdentityCollector.Collect(
            tree.GetRoot(),
            compilation.GetSemanticModel(tree, ignoreAccessibility: true),
            "Fixture.csproj",
            "Fixture.cs",
            LanguageNames.CSharp,
            candidates);
        return candidates;
    }

    private static (string? SourceIdentity, string MetadataIdentity) ReadCase(string caseId)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepoRoot(), "samples", "compiled-dotnet-evidence", "fixture-cases.json")));
        Assert.Equal("compiled-dotnet-fixture-cases.v3", document.RootElement.GetProperty("schemaVersion").GetString());
        var item = document.RootElement.GetProperty("reconciliationCases").EnumerateArray()
            .Single(candidate => candidate.GetProperty("id").GetString() == caseId);
        Assert.Equal(RuleIds.DotNetCompiledSourceIdentity, item.GetProperty("expectedRuleId").GetString());
        Assert.NotEmpty(item.GetProperty("nonClaims").EnumerateArray());
        return (
            item.GetProperty("expectedSourceIdentity").ValueKind == JsonValueKind.Null
                ? null
                : item.GetProperty("expectedSourceIdentity").GetString(),
            item.GetProperty("expectedMetadataIdentity").GetString()!);
    }
}
