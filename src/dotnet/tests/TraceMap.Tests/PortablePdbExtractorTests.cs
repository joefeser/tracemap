using System.Text.Json;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class PortablePdbExtractorTests
{
    [Fact]
    public void Bound_portable_pdb_emits_exact_document_method_sequence_point_and_source_edges()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb);

        Assert.NotNull(result.Manifest.PdbInputProvenance);
        Assert.NotNull(result.Manifest.PdbEvidenceSummary);
        Assert.Equal("pdb-partial", result.Manifest.PdbInputProvenance.CoverageState);
        Assert.Single(result.Facts, fact => fact.FactType == FactTypes.PdbInputAdmitted);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbDocumentDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbMethodDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbSequencePointDeclared);
        Assert.Equal(
            result.Facts.Count(fact => fact.FactType == FactTypes.PdbMethodDeclared),
            result.Facts.Count(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbSourceDocumentReconciled
            && fact.Evidence.FilePath == "FixtureShapes.cs"
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbSequencePointDeclared
            && fact.Properties.GetValueOrDefault("hidden") == "true");
        Assert.All(result.Facts.Where(fact => fact.RuleId is RuleIds.DotNetPdbInput or RuleIds.DotNetPdbIdentity or RuleIds.DotNetPdbSequencePoint), fact =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("pdbBoundedInputSha256")));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("pdbGeneratorSha256")));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("provenanceBindingInputSha256")));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("limitation")));
        });
        var byId = result.Facts.ToDictionary(fact => fact.FactId, StringComparer.Ordinal);
        Assert.All(result.Facts.Where(fact => fact.FactType == FactTypes.PdbDocumentDeclared), fact =>
            Assert.Equal(FactTypes.PdbInputAdmitted, byId[fact.Properties["pdbInputFactId"]].FactType));
        Assert.All(result.Facts.Where(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled), fact =>
        {
            Assert.Equal(FactTypes.ManagedMethodDeclared, byId[fact.Properties["compiledFactId"]].FactType);
            Assert.Equal(FactTypes.PdbMethodDeclared, byId[fact.Properties["pdbMethodFactId"]].FactType);
        });
        Assert.All(result.Facts.Where(fact => fact.FactType == FactTypes.PdbSequencePointDeclared), fact =>
        {
            Assert.Equal(FactTypes.PdbMethodDeclared, byId[fact.Properties["pdbMethodFactId"]].FactType);
            var document = byId[fact.Properties["pdbDocumentFactId"]];
            Assert.Equal(FactTypes.PdbDocumentDeclared, document.FactType);
            Assert.Equal(document.TargetSymbol, fact.TargetSymbol);
            Assert.Contains($"|document:{document.Properties["documentRowId"]}|", fact.ContractElement, StringComparison.Ordinal);
            Assert.Equal(FactTypes.MetadataPdbMethodReconciled, byId[fact.Properties["metadataPdbReconciliationFactId"]].FactType);
        });
        var serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(Path.GetFullPath(fixture.Source), serialized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp", "FixtureShapes.cs", true)]
    [InlineData("vb", "CompiledEvidence.VisualBasic", "FixtureShapes.vb", true)]
    [InlineData("fsharp", "CompiledEvidence.FSharp", "FixtureShapes.fs", false)]
    public void Portable_pdb_language_matrix_preserves_exact_compiled_evidence_and_never_guesses_source(
        string language,
        string assemblyName,
        string expectedSourceFile,
        bool sourceAdapterSupported)
    {
        var fixture = Fixture(language, assemblyName);
        var result = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbDocumentDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbMethodDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbSequencePointDeclared);
        Assert.Equal(
            result.Facts.Count(fact => fact.FactType == FactTypes.PdbMethodDeclared),
            result.Facts.Count(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled));

        if (sourceAdapterSupported)
        {
            Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbSourceDocumentReconciled
                && fact.Evidence.FilePath == expectedSourceFile);
        }
        else
        {
            Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.PdbSourceDocumentReconciled);
            Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
                && fact.Properties.GetValueOrDefault("gapKind") == "PdbSourceReconciliationUnsupportedLanguage");
        }
    }

    [Fact]
    public void Unbound_compiled_input_cannot_produce_positive_pdb_facts()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            PdbInputPaths: [fixture.Pdb]));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.PdbInputAdmitted
            or FactTypes.PdbDocumentDeclared
            or FactTypes.PdbMethodDeclared
            or FactTypes.PdbSequencePointDeclared
            or FactTypes.MetadataPdbMethodReconciled
            or FactTypes.PdbSourceDocumentReconciled);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbCompiledEvidenceUnacceptable");
    }

    [Fact]
    public void Bound_dependency_pdb_uses_the_exact_dependency_provenance_outcome()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var receipt = Path.Combine(temp.Path, "binding.json");
        WriteBoundReceipt(fixture.Source, fixture.Assembly, receipt, dependency: true);
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledDependencyPaths: [fixture.Assembly],
            CompiledBindingReceiptPaths: [receipt],
            PdbInputPaths: [fixture.Pdb]));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbInputAdmitted);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbSequencePointDeclared);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbAssemblyIdentityMismatch");
    }

    [Fact]
    public void Omitted_duplicate_compiled_input_cannot_reenter_pdb_binding()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var repo = new TempDirectory();
        var source = Path.Combine(repo.Path, "FixtureShapes.cs");
        var admitted = Path.Combine(repo.Path, "a-admitted.dll");
        var omitted = Path.Combine(repo.Path, "z-omitted-duplicate.dll");
        File.Copy(Path.Combine(fixture.Source, "FixtureShapes.cs"), source);
        File.Copy(fixture.Assembly, admitted);
        File.Copy(fixture.Assembly, omitted);
        RunGit(repo.Path, "init", "-b", "main");
        RunGit(repo.Path, "config", "user.email", "fixtures@tracemap.invalid");
        RunGit(repo.Path, "config", "user.name", "TraceMap Fixtures");
        RunGit(repo.Path, "add", ".");
        RunGit(repo.Path, "commit", "-m", "fixture");
        var receipt = Path.Combine(repo.Path, "binding.json");
        WriteBoundReceipt(repo.Path, admitted, receipt);

        var result = Scan(new ScanOptions(
            repo.Path,
            TempOutput(),
            CompiledInputPaths: [omitted, admitted],
            CompiledBindingReceiptPaths: [receipt],
            CompiledInputLimits: new CompiledInputLimits(MaxArtifactCount: 1),
            PdbInputPaths: [fixture.Pdb]));

        Assert.Equal(1, result.Manifest.CompiledInputProvenance!.OmittedInputCount);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbInputAdmitted);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousPdbAssemblyMatch");
    }

    [Fact]
    public void Cecil_type_traversal_is_iterative_and_charges_empty_nested_types()
    {
        const int nestedTypeCount = 10_000;
        var root = new Mono.Cecil.TypeDefinition("Fixture", "Root", Mono.Cecil.TypeAttributes.Public);
        var current = root;
        for (var index = 0; index < nestedTypeCount; index++)
        {
            var nested = new Mono.Cecil.TypeDefinition(string.Empty, $"Nested{index:D5}", Mono.Cecil.TypeAttributes.NestedPublic);
            current.NestedTypes.Add(nested);
            current = nested;
        }

        var budget = new PortablePdbExtractor.PdbWorkBudget(nestedTypeCount + 1);
        var flattened = PortablePdbExtractor.AllTypes([root], budget).ToArray();

        Assert.Equal(nestedTypeCount + 1, flattened.Length);
        Assert.Equal(nestedTypeCount + 1, budget.Consumed);
        Assert.Equal($"Nested{nestedTypeCount - 1:D5}", flattened[^1].Name);

        var bounded = new PortablePdbExtractor.PdbWorkBudget(1);
        var exception = Assert.Throws<PortablePdbExtractor.PdbInputException>(() =>
            PortablePdbExtractor.AllTypes([root], bounded).ToArray());
        Assert.Equal("PdbInputTotalWorkLimitExceeded", exception.Message);
    }

    [Fact]
    public void Pdb_with_different_codeview_identity_emits_mismatch_gap_without_candidate_selection()
    {
        var csharp = Fixture("csharp", "CompiledEvidence.CSharp");
        var visualBasic = Fixture("vb", "CompiledEvidence.VisualBasic");
        var result = ScanBound(csharp.Source, csharp.Assembly, visualBasic.Pdb);

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbAssemblyIdentityMismatch");
    }

    [Fact]
    public void Duplicate_exact_document_checksums_emit_multiple_candidate_gap_without_selecting_a_path()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var bytes = File.ReadAllBytes(Path.Combine(fixture.Source, "FixtureShapes.cs"));
        File.WriteAllBytes(Path.Combine(temp.Path, "First.cs"), bytes);
        File.WriteAllBytes(Path.Combine(temp.Path, "Second.cs"), bytes);
        RunGit(temp.Path, "init", "-b", "main");
        RunGit(temp.Path, "config", "user.email", "fixtures@tracemap.invalid");
        RunGit(temp.Path, "config", "user.name", "TraceMap Fixtures");
        RunGit(temp.Path, "add", ".");
        RunGit(temp.Path, "commit", "-m", "fixture");

        var result = ScanBound(temp.Path, fixture.Assembly, fixture.Pdb);

        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbSourceDocumentMultipleCandidates"
            && fact.Properties.GetValueOrDefault("candidateCount") == "2");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.PdbSourceDocumentReconciled
            && (fact.Evidence.FilePath == "First.cs" || fact.Evidence.FilePath == "Second.cs"));
    }

    [Fact]
    public void Portable_pdb_limits_fail_closed_before_partial_sequence_point_output()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb, new PdbInputLimits(MaxSequencePointCount: 1));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.PdbSequencePointDeclared);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbSequencePointCountExceeded");
    }

    [Fact]
    public void Portable_pdb_total_metadata_work_limit_fails_closed()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb, new PdbInputLimits(MaxTotalWorkUnits: 1));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.PdbSequencePointDeclared);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbInputTotalWorkLimitExceeded");
    }

    [Fact]
    public void Source_checksum_index_limits_fail_closed_without_removing_compiled_pdb_evidence()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb, new PdbInputLimits(MaxSourceFileCount: 1));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbMethodDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbSequencePointDeclared);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.PdbSourceDocumentReconciled);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbSourceFileCountExceeded");
        Assert.Equal("pdb-partial", result.Manifest.PdbInputProvenance!.CoverageState);
    }

    [Fact]
    public void Source_checksum_index_observes_scan_cancellation_before_hashing()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var receipt = Path.Combine(temp.Path, "binding.json");
        WriteBoundReceipt(fixture.Source, fixture.Assembly, receipt);
        var options = new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            CompiledBindingReceiptPaths: [receipt],
            PdbInputPaths: [fixture.Pdb]);
        var baseline = Scan(options);
        var compiledEvaluation = ManagedMetadataExtractor.Evaluate(fixture.Source, baseline.Manifest.CommitSha, options);
        var pdbEvaluation = PortablePdbExtractor.Evaluate(fixture.Source, options, compiledEvaluation);
        var compiledFacts = ManagedMetadataExtractor.MaterializeFacts(baseline.Manifest, compiledEvaluation);
        var sourcePath = Path.Combine(fixture.Source, "FixtureShapes.cs");
        var inventory = new[]
        {
            new FileInventoryItem("FixtureShapes.cs", "CSharp", new FileInfo(sourcePath).Length)
        };

        Assert.Throws<OperationCanceledException>(() => PortablePdbExtractor.MaterializeFacts(
            fixture.Source,
            baseline.Manifest,
            pdbEvaluation,
            compiledFacts,
            inventory,
            new CancellationToken(canceled: true)));
    }

    [Fact]
    public void Compiled_binding_admission_observes_scan_cancellation()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var options = new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            PdbInputPaths: [fixture.Pdb]);
        var compiled = ManagedMetadataExtractor.Evaluate(
            fixture.Source,
            GitMetadataProvider.Detect(fixture.Source).CommitSha,
            options);

        Assert.Throws<OperationCanceledException>(() => PortablePdbExtractor.Evaluate(
            fixture.Source,
            options,
            compiled,
            new CancellationToken(canceled: true)));
    }

    [Fact]
    public void Bounded_input_read_observes_cancellation_between_chunks()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new CancellingReadStream(new byte[200_000], cancellation);

        Assert.Throws<OperationCanceledException>(() => PortablePdbExtractor.ReadBoundedStream(
            stream,
            200_000,
            cancellation.Token));
        Assert.Equal(1, stream.ReadCount);
    }

    [Fact]
    public void Matched_compiled_artifact_is_reverified_before_pdb_evidence()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bound.dll");
        var original = new byte[] { 1, 2, 3, 4 };
        var expectedDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)).ToLowerInvariant();
        File.WriteAllBytes(path, original);

        Assert.Equal(original, PortablePdbExtractor.ReadVerifiedCompiledBytes(path, expectedDigest, 4));
        File.WriteAllBytes(path, [4, 3, 2, 1]);
        Assert.Null(PortablePdbExtractor.ReadVerifiedCompiledBytes(path, expectedDigest, 4));
        Assert.Null(PortablePdbExtractor.ReadVerifiedCompiledBytes(path, expectedDigest, 3));
        File.Delete(path);
        Assert.Null(PortablePdbExtractor.ReadVerifiedCompiledBytes(path, expectedDigest, 4));
    }

    [Fact]
    public void Pdb_expected_inputs_and_outcomes_remain_within_the_published_artifact_bound()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var paths = Enumerable.Range(0, PortablePdbExtractor.MaximumArtifactCount + 1)
            .Select(index => Path.Combine(Path.GetDirectoryName(fixture.Pdb)!, $"missing-{index:D2}.pdb"))
            .ToArray();
        var result = Scan(new ScanOptions(fixture.Source, TempOutput(), PdbInputPaths: paths));

        Assert.Equal(PortablePdbExtractor.MaximumArtifactCount, result.Manifest.PdbInputProvenance!.ExpectedInputs.Count);
        Assert.Equal(PortablePdbExtractor.MaximumArtifactCount + 1, result.Manifest.PdbInputProvenance.Outcomes.Count);
        Assert.Equal(1, result.Manifest.PdbInputProvenance.OmittedInputCount);
        Assert.Matches("^[0-9a-f]{64}$", result.Manifest.PdbInputProvenance.OmittedInputSha256);
        Assert.Throws<ArgumentException>(() => Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            PdbInputPaths: [fixture.Pdb],
            PdbInputLimits: new PdbInputLimits(MaxArtifactCount: PortablePdbExtractor.MaximumArtifactCount + 1))));
    }

    [Fact]
    public void Overlong_pdb_locator_is_projected_and_text_limit_fails_closed_without_leaking_the_name()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var temp = Directory.CreateTempSubdirectory("tracemap-pdb-long-locator-");
        try
        {
            var privateName = new string('x', 100) + "-private-customer-name.pdb";
            var pdb = Path.Combine(temp.FullName, privateName);
            File.Copy(fixture.Pdb, pdb);
            var result = ScanBound(fixture.Source, fixture.Assembly, pdb, new PdbInputLimits(MaxTextLength: PortablePdbExtractor.MinimumProjectedTextLength));

            Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
                && fact.Properties.GetValueOrDefault("gapKind") == "PdbInputTextLimitExceeded");
            Assert.DoesNotContain(privateName, JsonSerializer.Serialize(result), StringComparison.Ordinal);
            Assert.All(result.Manifest.PdbInputProvenance!.ExpectedInputs, input => Assert.True(input.SafeLocator.Length <= PortablePdbExtractor.MinimumProjectedTextLength));
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    public void Sequence_point_matrix_preserves_multi_document_non_monotonic_hidden_and_generated_shapes()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb);
        var points = result.Facts.Where(fact => fact.FactType == FactTypes.PdbSequencePointDeclared).ToArray();

        Assert.Contains(points, point => point.Properties.GetValueOrDefault("hidden") == "true");
        Assert.Contains(points.GroupBy(point => point.Properties["pdbMethodFactId"]), group =>
            group.Select(point => point.Properties["pdbDocumentFactId"]).Distinct(StringComparer.Ordinal).Count() > 1);
        Assert.Contains(points.GroupBy(point => point.Properties["pdbMethodFactId"]), group =>
        {
            var lines = group.Where(point => point.Properties.GetValueOrDefault("hidden") == "false")
                .OrderBy(point => int.Parse(point.Properties["ordinal"], System.Globalization.CultureInfo.InvariantCulture))
                .Select(point => int.Parse(point.Properties["startLine"], System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            return lines.Zip(lines.Skip(1), (left, right) => right < left).Any(value => value);
        });

        var factsById = result.Facts.ToDictionary(fact => fact.FactId, StringComparer.Ordinal);
        var generatedReconciliations = result.Facts.Where(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled)
            .Where(fact =>
            {
                var metadata = factsById[fact.Properties["compiledFactId"]];
                return (metadata.TargetSymbol?.Contains("<AsyncShape>", StringComparison.Ordinal) == true
                        || metadata.TargetSymbol?.Contains("<IteratorShape>", StringComparison.Ordinal) == true
                        || metadata.TargetSymbol?.Contains("b__", StringComparison.Ordinal) == true)
                    && !result.Facts.Any(candidate => candidate.FactType == FactTypes.SourceMetadataIdentityReconciled
                        && candidate.Properties.GetValueOrDefault("compiledFactId") == fact.Properties["compiledFactId"]);
            })
            .ToArray();
        Assert.NotEmpty(generatedReconciliations);
        Assert.All(generatedReconciliations, fact => Assert.DoesNotContain(
            result.Facts,
            candidate => candidate.FactType == FactTypes.SourceMetadataIdentityReconciled
                && candidate.Properties.GetValueOrDefault("compiledFactId") == fact.Properties["compiledFactId"]));
    }

    [Fact]
    public void Sequence_points_require_exactly_one_metadata_method_candidate()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var receipt = Path.Combine(temp.Path, "binding.json");
        WriteBoundReceipt(fixture.Source, fixture.Assembly, receipt);
        var options = new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            CompiledBindingReceiptPaths: [receipt],
            PdbInputPaths: [fixture.Pdb]);
        var baseline = Scan(options);
        var compiledEvaluation = ManagedMetadataExtractor.Evaluate(fixture.Source, baseline.Manifest.CommitSha, options);
        var pdbEvaluation = PortablePdbExtractor.Evaluate(fixture.Source, options, compiledEvaluation);
        var compiledFacts = ManagedMetadataExtractor.MaterializeFacts(baseline.Manifest, compiledEvaluation);

        var zero = PortablePdbExtractor.MaterializeFacts(
            fixture.Source,
            baseline.Manifest,
            pdbEvaluation,
            compiledFacts.Where(fact => fact.FactType != FactTypes.ManagedMethodDeclared).ToArray(),
            []);
        Assert.Contains(zero, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbMetadataMethodZeroCandidate");
        Assert.DoesNotContain(zero, fact => fact.FactType == FactTypes.PdbSequencePointDeclared);

        var duplicated = compiledFacts.ToList();
        var duplicate = compiledFacts.First(fact => fact.FactType == FactTypes.ManagedMethodDeclared);
        duplicated.Add(duplicate with { FactId = "fact-00000000000000000000" });
        var multiple = PortablePdbExtractor.MaterializeFacts(
            fixture.Source,
            baseline.Manifest,
            pdbEvaluation,
            duplicated,
            []);
        var multipleGap = Assert.Single(multiple, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbMetadataMethodMultipleCandidates");
        Assert.DoesNotContain(multiple, fact => fact.FactType == FactTypes.PdbSequencePointDeclared
            && fact.SourceSymbol == multipleGap.Properties["pdbMethodIdentity"]);

        var otherAssembly = compiledFacts.ToList();
        otherAssembly.Add(duplicate with
        {
            FactId = "fact-00000000000000000001",
            Evidence = duplicate.Evidence with { FilePath = "another-assembly.dll" }
        });
        var exactAssembly = PortablePdbExtractor.MaterializeFacts(
            fixture.Source,
            baseline.Manifest,
            pdbEvaluation,
            otherAssembly,
            []);
        Assert.Equal(
            baseline.Facts.Count(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled),
            exactAssembly.Count(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled));
        Assert.DoesNotContain(exactAssembly, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbMetadataMethodMultipleCandidates");
    }

    [Fact]
    public async Task Present_pdb_path_aliases_are_one_admitted_input_and_one_cli_fact_set()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory(Path.GetDirectoryName(FindRepoRoot()));
        var receipt = Path.Combine(temp.Path, "binding.json");
        WriteBoundReceipt(fixture.Source, fixture.Assembly, receipt);
        var relative = Path.GetRelativePath(fixture.Source, fixture.Pdb);
        var output = Path.Combine(temp.Path, "out");
        var options = new ScanOptions(
            fixture.Source,
            output,
            CompiledInputPaths: [fixture.Assembly],
            CompiledBindingReceiptPaths: [receipt],
            PdbInputPaths: [fixture.Pdb, relative, $".{Path.DirectorySeparatorChar}{relative}"]);
        var result = Scan(options);

        Assert.Single(result.Manifest.PdbInputProvenance!.ExpectedInputs);
        Assert.Single(result.Manifest.PdbInputProvenance.Outcomes);
        Assert.Single(result.Facts, fact => fact.FactType == FactTypes.PdbInputAdmitted);
        Assert.Equal(result.Facts.Count, result.Facts.Select(fact => fact.FactId).Distinct(StringComparer.Ordinal).Count());

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([
            "scan", "--repo", fixture.Source, "--out", Path.Combine(temp.Path, "cli"),
            "--compiled-input", fixture.Assembly,
            "--compiled-binding-receipt", receipt,
            "--pdb-input", fixture.Pdb,
            "--pdb-input", relative
        ], stdout, stderr);
        Assert.True(exitCode == 0, $"stdout: {stdout}{Environment.NewLine}stderr: {stderr}");
    }

    [Fact]
    public void Missing_pdb_path_aliases_emit_one_gap()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var missing = Path.Combine(fixture.Source, "missing-pdb-alias.pdb");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            PdbInputPaths: [missing, "missing-pdb-alias.pdb", $".{Path.DirectorySeparatorChar}missing-pdb-alias.pdb"]));

        Assert.Single(result.Manifest.PdbInputProvenance!.ExpectedInputs);
        Assert.Single(result.Manifest.PdbInputProvenance.Outcomes);
        Assert.Single(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.Properties.GetValueOrDefault("gapKind") == "MissingPdbInput");
        Assert.Equal(result.Facts.Count, result.Facts.Select(fact => fact.FactId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Missing_malformed_and_windows_pdb_inputs_emit_bounded_gaps()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var temp = Directory.CreateTempSubdirectory("tracemap-pdb-invalid-");
        try
        {
            var malformed = Path.Combine(temp.FullName, "malformed.pdb");
            File.WriteAllBytes(malformed, [(byte)'B', (byte)'S', (byte)'J', (byte)'B', 0xff]);
            var windows = Path.Combine(temp.FullName, "windows.pdb");
            File.WriteAllBytes(windows, "Microsoft C/C++ MSF 7.00\r\n\u001aDS\0\0\0"u8.ToArray());
            var unrelated = Path.Combine(temp.FullName, "unrelated.pdb");
            File.WriteAllBytes(unrelated, "not a pdb"u8.ToArray());
            var result = Scan(new ScanOptions(
                fixture.Source,
                TempOutput(),
                CompiledInputPaths: [fixture.Assembly],
                PdbInputPaths: [Path.Combine(temp.FullName, "missing.pdb"), malformed, windows, unrelated]));

            var gaps = result.Facts.Where(fact => fact.RuleId == RuleIds.DotNetPdbGap)
                .Select(fact => fact.Properties.GetValueOrDefault("gapKind"))
                .ToArray();
            Assert.Contains("MissingPdbInput", gaps);
            Assert.Contains("MalformedPortablePdb", gaps);
            Assert.Contains("MalformedPdbInput", gaps);
            Assert.Contains(OperatingSystem.IsWindows() ? "WindowsPdbIndependentReaderUnavailable" : "WindowsPdbRequiresWindows", gaps);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    public void Undeclared_sequence_point_document_rows_are_recoverable_malformed_input()
    {
        var documents = new[]
        {
            new PdbDocumentObservation(1, "document-1", "name", Guid.Empty.ToString("D"), "checksum", Guid.Empty.ToString("D"))
        };
        var methods = new[]
        {
            new PdbMethodObservation(1, "0x06000001", "method-1",
            [
                new PdbSequencePointObservation(0, 0, 2, false, 1, 1, 1, 2, "point-1")
            ])
        };

        var exception = Assert.Throws<InvalidDataException>(() => PortablePdbExtractor.CanonicalShapes(documents, methods));
        Assert.True(PortablePdbExtractor.IsRecoverable(exception));
    }

    [Fact]
    public void Windows_produced_native_pdb_lane_fails_closed_without_independent_reader()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var root = Environment.GetEnvironmentVariable("TRACEMAP_WINDOWS_PDB_ROOT");
        if (string.IsNullOrWhiteSpace(root))
            return;

        foreach (var (language, assemblyName) in new[]
                 {
                     ("csharp", "CompiledEvidence.CSharp"),
                     ("vb", "CompiledEvidence.VisualBasic")
                 })
        {
            var assembly = Path.Combine(root, language, assemblyName + ".dll");
            var pdb = Path.Combine(root, language, assemblyName + ".pdb");
            Assert.True(File.Exists(assembly), assembly);
            Assert.True(File.Exists(pdb), pdb);
            Assert.True(PortablePdbExtractor.IsWindowsPdb(File.ReadAllBytes(pdb)), $"Expected a native Windows PDB: {pdb}");

            var source = Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", language);
            var result = ScanBound(source, assembly, pdb);
            Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.PdbInputAdmitted
                or FactTypes.PdbDocumentDeclared or FactTypes.PdbMethodDeclared or FactTypes.PdbSequencePointDeclared);
            Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
                && fact.Properties.GetValueOrDefault("gapKind") == "WindowsPdbIndependentReaderUnavailable");
        }
    }

    [Fact]
    public void Repeated_bound_pdb_scans_are_deterministic()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var first = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb);
        var second = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb);

        Assert.Equal(first.Manifest.ScanId, second.Manifest.ScanId);
        Assert.Equal(
            JsonSerializer.Serialize(first.Manifest.PdbInputProvenance),
            JsonSerializer.Serialize(second.Manifest.PdbInputProvenance));
        Assert.Equal(
            first.Facts.Select(fact => JsonSerializer.Serialize(fact)).ToArray(),
            second.Facts.Select(fact => JsonSerializer.Serialize(fact)).ToArray());
    }

    [Fact]
    public void Bounded_pdb_summary_commits_omitted_endpoint_entries()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb);

        var first = PortablePdbExtractor.BuildSummary(result.Manifest, result.Facts, maximumEntries: 1)!;
        var second = PortablePdbExtractor.BuildSummary(result.Manifest, result.Facts, maximumEntries: 1)!;

        var entry = Assert.Single(first.Entries);
        Assert.False(string.IsNullOrWhiteSpace(entry.SourceIdentity));
        Assert.False(string.IsNullOrWhiteSpace(entry.TargetIdentity));
        Assert.NotEmpty(entry.SupportingFactIds);
        Assert.True(first.OmittedEntryCount > 0);
        Assert.Matches("^[0-9a-f]{64}$", first.OmittedEntrySha256);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Pdb_summary_input_commitment_changes_with_the_source_snapshot()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var repo = new TempDirectory();
        File.Copy(Path.Combine(fixture.Source, "FixtureShapes.cs"), Path.Combine(repo.Path, "FixtureShapes.cs"));
        RunGit(repo.Path, "init", "-b", "main");
        RunGit(repo.Path, "config", "user.email", "fixtures@tracemap.invalid");
        RunGit(repo.Path, "config", "user.name", "TraceMap Fixtures");
        RunGit(repo.Path, "add", ".");
        RunGit(repo.Path, "commit", "-m", "fixture");

        var first = ScanBound(repo.Path, fixture.Assembly, fixture.Pdb);
        File.WriteAllText(Path.Combine(repo.Path, "Additional.cs"), "internal sealed class Additional { }");
        var second = ScanBound(repo.Path, fixture.Assembly, fixture.Pdb);

        Assert.Equal(first.Manifest.CommitSha, second.Manifest.CommitSha);
        Assert.Equal(first.Manifest.PdbInputProvenance!.BoundedInputSha256, second.Manifest.PdbInputProvenance!.BoundedInputSha256);
        Assert.NotEqual(first.Manifest.SourceSnapshotDigest, second.Manifest.SourceSnapshotDigest);
        Assert.NotEqual(first.Manifest.PdbEvidenceSummary!.BoundedInputSha256, second.Manifest.PdbEvidenceSummary!.BoundedInputSha256);
    }

    [Fact]
    public async Task Cli_repeat_scans_preserve_pdb_provenance_and_endpoints_in_all_artifacts()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        // Keep CLI output on the checkout volume. On Windows CI the system temp
        // directory is on C: while the checkout is on D:, and the protected-root
        // validation intentionally reasons about paths within one volume.
        using var temp = new TempDirectory(Path.GetDirectoryName(FindRepoRoot()));
        var binding = Path.Combine(temp.Path, "binding.json");
        WriteBoundReceipt(fixture.Source, fixture.Assembly, binding);
        var first = Path.Combine(temp.Path, "first");
        var second = Path.Combine(temp.Path, "second");
        await Run(first);
        await Run(second);

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
        var provenance = manifest.RootElement.GetProperty("pdbInputProvenance");
        var summary = manifest.RootElement.GetProperty("pdbEvidenceSummary");
        Assert.Equal(provenance.GetRawText(), secondManifest.RootElement.GetProperty("pdbInputProvenance").GetRawText());
        Assert.Equal(summary.GetRawText(), secondManifest.RootElement.GetProperty("pdbEvidenceSummary").GetRawText());
        Assert.Equal("pdb-input-provenance.v1", provenance.GetProperty("schemaVersion").GetString());
        Assert.Equal("pdb-partial", provenance.GetProperty("coverageState").GetString());
        Assert.Equal("pdb-evidence-summary.v1", summary.GetProperty("schemaVersion").GetString());
        Assert.Contains(summary.GetProperty("entries").EnumerateArray(), entry =>
            !string.IsNullOrWhiteSpace(entry.GetProperty("sourceIdentity").GetString())
            && !string.IsNullOrWhiteSpace(entry.GetProperty("targetIdentity").GetString())
            && !string.IsNullOrWhiteSpace(entry.GetProperty("evidenceFactId").GetString())
            && entry.GetProperty("supportingFactIds").GetArrayLength() > 0
            && !string.IsNullOrWhiteSpace(entry.GetProperty("filePath").GetString())
            && entry.GetProperty("startLine").GetInt32() > 0
            && entry.GetProperty("endLine").GetInt32() > 0
            && !string.IsNullOrWhiteSpace(entry.GetProperty("commitSha").GetString())
            && !string.IsNullOrWhiteSpace(entry.GetProperty("provenanceBindingInputSha256").GetString()));
        Assert.Contains("Compiled .NET PDB Evidence", await File.ReadAllTextAsync(Path.Combine(first, "report.md")), StringComparison.Ordinal);

        var operationalReceipt = JsonSerializer.Deserialize<ScanExecutionReceipt>(
            await File.ReadAllTextAsync(Path.Combine(first, "scan-receipt.json")),
            TraceMap.Storage.JsonOptions.Stable)!;
        Assert.Equal(provenance.GetProperty("boundedInputSha256").GetString(), operationalReceipt.PdbInputProvenance!.BoundedInputSha256);
        Assert.Contains(operationalReceipt.PdbEvidenceSummary!.Entries, entry =>
            !string.IsNullOrWhiteSpace(entry.SourceIdentity) && !string.IsNullOrWhiteSpace(entry.TargetIdentity));

        using var connection = new SqliteConnection($"Data Source={Path.Combine(first, "index.sqlite")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where rule_id = $rule and fact_type = $type and source_symbol is not null and target_symbol is not null and properties_json like '%pdbMethodFactId%' and properties_json like '%pdbDocumentFactId%'";
        command.Parameters.AddWithValue("$rule", RuleIds.DotNetPdbSequencePoint);
        command.Parameters.AddWithValue("$type", FactTypes.PdbSequencePointDeclared);
        Assert.True(Convert.ToInt32(command.ExecuteScalar()) > 0);

        async Task Run(string output)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = await TraceMapCommand.RunAsync([
                "scan", "--repo", fixture.Source, "--out", output,
                    "--compiled-input", fixture.Assembly,
                    "--compiled-binding-receipt", binding,
                    "--pdb-input", fixture.Pdb
            ], stdout, stderr);
            var receipt = Directory.Exists(output)
                ? Directory.EnumerateFiles(output, "scan-receipt.json", SearchOption.AllDirectories).FirstOrDefault()
                : null;
            var receiptText = receipt is null ? string.Empty : await File.ReadAllTextAsync(receipt);
            Assert.True(exitCode == 0, $"stdout: {stdout}{Environment.NewLine}stderr: {stderr}{Environment.NewLine}receipt: {receiptText}");
        }
    }

    [Fact]
    public void Rule_catalog_registers_active_pdb_contracts()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains("- id: dotnet.compiled.pdb-input.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.pdb-identity.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.sequence-point.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.pdb-gap.v1", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture_catalog_records_stable_pdb_cases_gaps_and_non_claims()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepoRoot(), "samples", "compiled-dotnet-evidence", "fixture-cases.json")));
        Assert.Equal("compiled-dotnet-fixture-cases.v3", document.RootElement.GetProperty("schemaVersion").GetString());
        var cases = document.RootElement.GetProperty("pdbCases").EnumerateArray().ToArray();
        Assert.Equal(6, cases.Length);
        Assert.Equal(cases.Length, cases.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cases, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("shape").GetString()));
            Assert.NotEmpty(item.GetProperty("expectedRuleIds").EnumerateArray());
            Assert.Contains(item.GetProperty("expectedTier").GetString(), new[] { EvidenceTiers.Tier2Structural, EvidenceTiers.Tier4Unknown });
            Assert.NotEmpty(item.GetProperty("nonClaims").EnumerateArray());
        });
    }

    private static ScanResult ScanBound(
        string source,
        string assembly,
        string pdb,
        PdbInputLimits? limits = null)
    {
        var temp = Directory.CreateTempSubdirectory("tracemap-pdb-bound-");
        var receipt = Path.Combine(temp.FullName, "binding.json");
        WriteBoundReceipt(source, assembly, receipt);
        return Scan(new ScanOptions(
            source,
            Path.Combine(temp.FullName, "out"),
            CompiledInputPaths: [assembly],
            CompiledBindingReceiptPaths: [receipt],
            PdbInputPaths: [pdb],
            PdbInputLimits: limits));
    }

    private static void WriteBoundReceipt(string source, string assembly, string receipt, bool dependency = false)
    {
        var commit = GitMetadataProvider.Detect(source).CommitSha;
        var initial = ManagedMetadataExtractor.Evaluate(source, commit, new ScanOptions(
            source,
            "unused",
            CompiledInputPaths: dependency ? null : [assembly],
            CompiledDependencyPaths: dependency ? [assembly] : null));
        File.WriteAllText(receipt, JsonSerializer.Serialize(new
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
                binaryBuildIdentity = "pdb-test-build"
            }).ToArray()
        }));
    }

    private static ScanResult Scan(ScanOptions options) => ScanEngine.Scan(options);

    private static string TempOutput() => Path.Combine(Directory.CreateTempSubdirectory("tracemap-pdb-output-").FullName, "out");

    private static (string Source, string Assembly, string Pdb) Fixture(string language, string assemblyName)
    {
        var source = Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", language);
        var binary = Path.Combine(source, "bin", "Debug", "net10.0", assemblyName);
        return (source, binary + ".dll", binary + ".pdb");
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

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start git.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {standardOutput}{standardError}");
    }

    private sealed class CancellingReadStream(byte[] bytes, CancellationTokenSource cancellation) : MemoryStream(bytes)
    {
        public int ReadCount { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCount++;
            var read = base.Read(buffer, offset, count);
            cancellation.Cancel();
            return read;
        }
    }
}
