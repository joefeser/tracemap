using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mono.Cecil;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ManagedMetadataExtractorTests
{
    [Fact]
    public void Portable_fixture_matrix_retains_exact_cross_language_metadata_identities()
    {
        var repo = FindRepoRoot();
        var commit = Git(repo, "rev-parse", "HEAD");
        var assemblies = FixtureAssemblies(repo);

        var evaluation = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(
            repo,
            Path.Combine(Path.GetTempPath(), "unused-tracemap-output"),
            CompiledInputPaths: [assemblies.CSharp, assemblies.VisualBasic, assemblies.FSharp]));
        var manifest = Manifest(commit, evaluation.Provenance);
        var facts = ManagedMetadataExtractor.MaterializeFacts(manifest, evaluation);

        Assert.NotNull(evaluation.Provenance);
        Assert.Equal(3, evaluation.Provenance.Outcomes.Count);
        Assert.Equal("local-only", evaluation.Provenance.ArtifactVisibility);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ManagedMetadataExtractor).Assembly.Location))).ToLowerInvariant(),
            evaluation.Provenance.GeneratorSha256);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblies.CSharp))).ToLowerInvariant(),
            evaluation.Provenance.Outcomes.Single(item => item.SafeLocator.EndsWith("CompiledEvidence.CSharp.dll", StringComparison.Ordinal)).RawFileSha256);
        Assert.Equal(3, facts.Count(fact => fact.FactType == FactTypes.ManagedAssemblyDeclared));
        Assert.All(facts.Where(fact => fact.FactType == FactTypes.ManagedAssemblyDeclared), fact =>
            Assert.Contains("|targetFramework:25:.NETCoreApp,Version=v10.0", fact.TargetSymbol, StringComparison.Ordinal));
        Assert.DoesNotContain(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
        Assert.All(facts.Where(fact => fact.RuleId.StartsWith("dotnet.compiled", StringComparison.Ordinal)), fact =>
        {
            Assert.Equal(commit, fact.CommitSha);
            Assert.Equal(manifest.RepoName, fact.Repo);
            Assert.Equal(64, fact.Properties["generatorSha256"].Length);
            Assert.Equal(64, fact.Properties["boundedInputSha256"].Length);
            Assert.DoesNotContain(repo, JsonSerializer.Serialize(fact), StringComparison.Ordinal);
        });
        Assert.All(facts.Where(fact => fact.Properties.GetValueOrDefault("evidenceLocationKind") == ManagedMetadataExtractor.MetadataLocationKind), fact =>
        {
            Assert.Equal(1, fact.Evidence.StartLine);
            Assert.Equal(1, fact.Evidence.EndLine);
            Assert.Null(fact.Evidence.SnippetHash);
            Assert.Matches("^0x[0-9a-f]{8}$", fact.Properties["metadataToken"]);
        });

        var overloads = facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol?.Contains("|method:8:Overload|", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(3, overloads.Length);
        Assert.Equal(3, overloads.Select(fact => fact.TargetSymbol).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("|names:", StringComparison.Ordinal) == true
            && fact.TargetSymbol.Contains("Widget`1", StringComparison.Ordinal)
            && fact.TargetSymbol.Contains("Nested`1", StringComparison.Ordinal));
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("method:7:Generic|arity:1", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("type(namespace:6:System|names:5:Int32)&", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("type(namespace:6:System|names:5:Int32)[]", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("type(namespace:6:System|names:5:Int32)*", StringComparison.Ordinal) == true);
        var functionPointers = facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol?.Contains("Pointer|", StringComparison.Ordinal) == true).ToArray();
        Assert.Contains(functionPointers, fact => fact.TargetSymbol?.Contains("call:cdecl", StringComparison.Ordinal) == true);
        Assert.Contains(functionPointers, fact => fact.TargetSymbol?.Contains("call:stdcall", StringComparison.Ordinal) == true);
        Assert.Equal(functionPointers.Length, functionPointers.Select(fact => fact.TargetSymbol).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("namespace:0:|names:", StringComparison.Ordinal) == true
            && fact.TargetSymbol.Contains("GlobalNamespaceShape", StringComparison.Ordinal));
        Assert.Contains(facts, fact => fact.FactType == FactTypes.ManagedPropertyDeclared && fact.TargetSymbol?.Contains("property:4:Item", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains(":RenamedForMetadata|", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("compilerGenerated") == "true");
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("genericConstructionState") == "open-definition");
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "UnresolvedManagedAssemblyReference");
        Assert.Contains(evaluation.Provenance.Outcomes.SelectMany(item => item.DependencyResolutionOutcomes), outcome =>
            outcome.EndsWith("=>unresolved", StringComparison.Ordinal));
    }

    [Fact]
    public void Bounded_input_digest_and_candidates_are_order_independent_and_byte_deterministic()
    {
        var repo = FindRepoRoot();
        var commit = Git(repo, "rev-parse", "HEAD");
        var assemblies = FixtureAssemblies(repo);
        var first = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused",
            CompiledInputPaths: [assemblies.CSharp, assemblies.VisualBasic]));
        var second = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused",
            CompiledInputPaths: [assemblies.VisualBasic, assemblies.CSharp]));

        Assert.Equal(first.Provenance!.BoundedInputSha256, second.Provenance!.BoundedInputSha256);
        Assert.Equal(
            JsonSerializer.Serialize(first.Provenance, JsonOptions.Stable),
            JsonSerializer.Serialize(second.Provenance, JsonOptions.Stable));
        Assert.Equal(
            JsonSerializer.Serialize(first.Candidates, JsonOptions.Stable),
            JsonSerializer.Serialize(second.Candidates, JsonOptions.Stable));
    }

    [Fact]
    public void Nested_type_inventory_is_iterative_and_preserves_depth_first_order()
    {
        const int nestedTypeCount = 10_000;
        var root = new TypeDefinition("Fixture", "Root", Mono.Cecil.TypeAttributes.Public);
        var current = root;
        for (var index = 0; index < nestedTypeCount; index++)
        {
            var nested = new TypeDefinition(string.Empty, $"Nested{index:D5}", Mono.Cecil.TypeAttributes.NestedPublic);
            current.NestedTypes.Add(nested);
            current = nested;
        }
        var secondRoot = new TypeDefinition("Fixture", "SecondRoot", Mono.Cecil.TypeAttributes.Public);

        var flattened = ManagedMetadataExtractor.FlattenTypes([root, secondRoot]).ToArray();

        Assert.Equal(nestedTypeCount + 2, flattened.Length);
        Assert.Same(root, flattened[0]);
        Assert.Equal("Nested00000", flattened[1].Name);
        Assert.Equal($"Nested{nestedTypeCount - 1:D5}", flattened[^2].Name);
        Assert.Same(secondRoot, flattened[^1]);
    }

    [Fact]
    public void Input_deduplication_uses_actual_filesystem_case_semantics()
    {
        using var temp = new TempDirectory();
        var source = FixtureAssemblies(FindRepoRoot()).CSharp;
        var mixedCasePath = Path.Combine(temp.Path, "MixedCase.dll");
        var alternateCasePath = Path.Combine(temp.Path, "mixedcase.dll");
        File.Copy(source, mixedCasePath);
        var comparer = CSharpSemanticExtractor.CreateSourcePathComparer(temp.Path);

        var first = ManagedMetadataExtractor.Evaluate(temp.Path, new string('a', 40), new ScanOptions(
            temp.Path,
            "unused",
            CompiledInputPaths: [mixedCasePath, alternateCasePath]));
        var second = ManagedMetadataExtractor.Evaluate(temp.Path, new string('a', 40), new ScanOptions(
            temp.Path,
            "unused",
            CompiledInputPaths: [alternateCasePath, mixedCasePath]));

        Assert.Equal(comparer.Equals(mixedCasePath, alternateCasePath) ? 1 : 2, first.Provenance!.Outcomes.Count);
        Assert.Equal(
            JsonSerializer.Serialize(first.Provenance, JsonOptions.Stable),
            JsonSerializer.Serialize(second.Provenance, JsonOptions.Stable));
        if (comparer.Equals(mixedCasePath, alternateCasePath))
            Assert.DoesNotContain(first.Provenance.Outcomes.SelectMany(outcome => outcome.GapKinds), gap => gap == "AmbiguousDuplicateManagedAssembly");
    }

    [Fact]
    public void Overlong_safe_locators_are_projected_before_missing_or_admitted_outcomes_are_retained()
    {
        using var temp = new TempDirectory();
        const int maxTextLength = ManagedMetadataExtractor.MinimumProjectedTextLength;
        var privateSegment = new string('p', 80);
        var missingPath = Path.Combine(temp.Path, privateSegment, privateSegment, "missing.dll");
        var evaluation = ManagedMetadataExtractor.Evaluate(temp.Path, new string('a', 40), new ScanOptions(
            temp.Path,
            "unused",
            CompiledInputPaths: [missingPath],
            CompiledInputLimits: new CompiledInputLimits(MaxTextLength: maxTextLength)));

        var outcome = Assert.Single(evaluation.Provenance!.Outcomes);
        Assert.Equal("limit-exhausted", outcome.Outcome);
        Assert.Contains("ManagedInputTextLimitExceeded", outcome.GapKinds);
        Assert.True(outcome.SafeLocator.Length <= maxTextLength);
        Assert.DoesNotContain(privateSegment, JsonSerializer.Serialize(evaluation.Provenance), StringComparison.Ordinal);
    }

    [Fact]
    public void Text_limits_smaller_than_a_complete_projected_digest_are_rejected()
    {
        using var temp = new TempDirectory();

        var exception = Assert.Throws<ArgumentException>(() => ManagedMetadataExtractor.Evaluate(
            temp.Path,
            new string('a', 40),
            new ScanOptions(
                temp.Path,
                "unused",
                CompiledInputPaths: [Path.Combine(temp.Path, "missing.dll")],
                CompiledInputLimits: new CompiledInputLimits(MaxTextLength: ManagedMetadataExtractor.MinimumProjectedTextLength - 1))));

        Assert.Contains(ManagedMetadataExtractor.MinimumProjectedTextLength.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_limit_retains_a_bounded_input_set_with_an_aggregate_omission_commitment()
    {
        using var temp = new TempDirectory();
        var paths = Enumerable.Range(0, 100)
            .Select(index => Path.Combine(temp.Path, $"missing-{index:D3}.dll"))
            .ToArray();

        var first = Evaluate(paths);
        var second = Evaluate(paths.Reverse().ToArray());

        Assert.Equal(3, first.Provenance!.ExpectedInputs.Count);
        Assert.Equal(3, first.Provenance.Outcomes.Count);
        Assert.Equal(97, first.Provenance.OmittedInputCount);
        Assert.Matches("^[0-9a-f]{64}$", first.Provenance.OmittedInputSha256!);
        Assert.Equal("compiled-metadata-partial", first.Provenance.CoverageState);
        Assert.Contains(first.KnownGaps, gap => gap.Contains("LimitArtifactCountExceeded", StringComparison.Ordinal));
        var aggregateGap = Assert.Single(first.Candidates, candidate =>
            candidate.Properties.GetValueOrDefault("gapKind") == "LimitArtifactCountExceeded");
        Assert.Equal("97", aggregateGap.Properties["omittedInputCount"]);
        Assert.Equal(first.Provenance.OmittedInputSha256, aggregateGap.Properties["omittedInputSha256"]);
        Assert.Equal(
            JsonSerializer.Serialize(first.Provenance, JsonOptions.Stable),
            JsonSerializer.Serialize(second.Provenance, JsonOptions.Stable));

        CompiledInputEvaluation Evaluate(IReadOnlyList<string> inputs) => ManagedMetadataExtractor.Evaluate(
            temp.Path,
            new string('a', 40),
            new ScanOptions(
                temp.Path,
                "unused",
                CompiledInputPaths: inputs,
                CompiledInputLimits: new CompiledInputLimits(MaxArtifactCount: 3)));
    }

    [Fact]
    public void Receipt_binding_preflight_ignores_nested_bindings_properties_and_counts_the_complete_top_level_array()
    {
        using var temp = new TempDirectory();
        var receipt = Path.Combine(temp.Path, "receipt.json");
        File.WriteAllText(receipt, """
            {
              "schemaVersion": "compiled-input-binding-set.v1",
              "bindings": [
                {
                  "schemaVersion": "compiled-input-binding.v1",
                  "safeLocator": "first.dll",
                  "ignored": { "bindings": [ {} ] }
                },
                {
                  "schemaVersion": "compiled-input-binding.v1",
                  "safeLocator": "second.dll"
                }
              ]
            }
            """);

        var evaluation = ManagedMetadataExtractor.Evaluate(temp.Path, new string('a', 40), new ScanOptions(
            temp.Path,
            "unused",
            CompiledBindingReceiptPaths: [receipt],
            CompiledInputLimits: new CompiledInputLimits(MaxArtifactCount: 1)));

        Assert.Contains(evaluation.KnownGaps, gap => gap.Contains("ManagedBindingReceiptBindingCountLimitExceeded", StringComparison.Ordinal));
    }

    [Fact]
    public void Receipt_paths_are_deduplicated_using_actual_filesystem_case_semantics()
    {
        using var temp = new TempDirectory();
        var receipt = Path.Combine(temp.Path, "Receipt.json");
        var alternateCase = Path.Combine(temp.Path, "receipt.json");
        File.WriteAllText(receipt, """
            {
              "schemaVersion": "compiled-input-binding-set.v1",
              "bindings": [
                {
                  "schemaVersion": "compiled-input-binding.v1",
                  "safeLocator": "fixture.dll",
                  "artifactSha256": "0000000000000000000000000000000000000000000000000000000000000000"
                }
              ]
            }
            """);

        var evaluation = ManagedMetadataExtractor.Evaluate(temp.Path, new string('a', 40), new ScanOptions(
            temp.Path,
            "unused",
            CompiledBindingReceiptPaths: [receipt, alternateCase]));

        Assert.Single(evaluation.Provenance!.ProvenanceBindingInputSha256s);
        if (CSharpSemanticExtractor.CreateSourcePathComparer(temp.Path).Equals(receipt, alternateCase))
            Assert.DoesNotContain(evaluation.KnownGaps, gap => gap.Contains("AmbiguousManagedBindingReceipt", StringComparison.Ordinal));
    }

    [Fact]
    public void Metadata_bearing_secondary_modules_are_explicitly_unsupported()
    {
        using var temp = new TempDirectory();
        var assemblyPath = Path.Combine(temp.Path, "multi-module.dll");
        WriteMultiModuleManifest(assemblyPath);

        var evaluation = ManagedMetadataExtractor.Evaluate(temp.Path, new string('a', 40), new ScanOptions(
            temp.Path,
            "unused",
            CompiledInputPaths: [assemblyPath]));

        AssertGap(evaluation, "MultiModuleManagedAssemblyUnsupported");
    }

    [Fact]
    public void Metadata_identity_length_prefixes_delimiter_bearing_components()
    {
        using var temp = new TempDirectory();
        var assemblyPath = Path.Combine(temp.Path, "delimiter-identities.dll");
        using (var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("DelimiterIdentities", new Version(1, 0)),
            "DelimiterIdentities",
            ModuleKind.Dll))
        {
            var module = assembly.MainModule;
            module.Types.Add(new TypeDefinition("A", "B|name:C", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object));
            module.Types.Add(new TypeDefinition("A|name:B", "C", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object));
            assembly.Write(assemblyPath);
        }

        var evaluation = ManagedMetadataExtractor.Evaluate(temp.Path, new string('a', 40), new ScanOptions(
            temp.Path,
            "unused",
            CompiledInputPaths: [assemblyPath]));
        var facts = ManagedMetadataExtractor.MaterializeFacts(Manifest(new string('a', 40), evaluation.Provenance), evaluation);
        var types = facts.Where(fact => fact.FactType == FactTypes.ManagedTypeDeclared).ToArray();

        Assert.Equal(2, types.Length);
        Assert.Equal(2, types.Select(fact => fact.TargetSymbol).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(types, fact => fact.TargetSymbol?.Contains("8:B|name:C", StringComparison.Ordinal) == true);
        Assert.Contains(types, fact => fact.TargetSymbol?.Contains("8:A|name:B", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(evaluation.KnownGaps, gap => gap.Contains("MetadataReaderDisagreement", StringComparison.Ordinal));
    }

    [Fact]
    public void Deep_metadata_signature_nesting_becomes_an_explicit_partial_gap()
    {
        using var temp = new TempDirectory();
        var assemblyPath = Path.Combine(temp.Path, "deep-signature.dll");
        using (var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("DeepSignature", new Version(1, 0)),
            "DeepSignature",
            ModuleKind.Dll))
        {
            var module = assembly.MainModule;
            var type = new TypeDefinition("Fixture", "DeepSignature", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
            module.Types.Add(type);
            TypeReference returnType = module.TypeSystem.Int32;
            for (var index = 0; index <= ManagedMetadataExtractor.MaximumSignatureTypeNesting; index++)
                returnType = new PointerType(returnType);
            type.Methods.Add(new MethodDefinition(
                "Read",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                returnType));
            assembly.Write(assemblyPath);
        }

        var evaluation = ManagedMetadataExtractor.Evaluate(temp.Path, new string('a', 40), new ScanOptions(
            temp.Path,
            "unused",
            CompiledInputPaths: [assemblyPath]));

        AssertGap(evaluation, "ManagedInputSignatureNestingLimitExceeded");
    }

    [Fact]
    public void Missing_native_corrupt_and_limit_exhausted_inputs_are_explicit_partial_gaps()
    {
        using var temp = new TempDirectory();
        var repo = FindRepoRoot();
        var commit = Git(repo, "rev-parse", "HEAD");
        var fixture = FixtureAssemblies(repo).CSharp;
        var native = Path.Combine(temp.Path, "native.bin");
        var corrupt = Path.Combine(temp.Path, "corrupt.dll");
        File.WriteAllText(native, "not a portable executable");
        File.WriteAllBytes(corrupt, [0x4d, 0x5a, 0x01, 0x02, 0x03]);

        AssertGap(Evaluate(Path.Combine(temp.Path, "missing.dll")), "MissingManagedInput");
        AssertGap(Evaluate(native), "NonManagedBinaryInput", "MalformedManagedInput");
        AssertGap(Evaluate(corrupt), "MalformedManagedInput");
        var limited = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused",
            CompiledInputPaths: [fixture],
            CompiledInputLimits: new CompiledInputLimits(MaxTypeCount: 1)));
        AssertGap(limited, "ManagedInputTypeCountLimitExceeded");
        var workLimited = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused",
            CompiledInputPaths: [fixture],
            CompiledInputLimits: new CompiledInputLimits(MaxTotalWorkUnits: 1)));
        AssertGap(workLimited, "ManagedInputTotalWorkLimitExceeded");
        var sizeLimited = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused",
            CompiledInputPaths: [native],
            CompiledInputLimits: new CompiledInputLimits(MaxFileSizeBytes: 1)));
        AssertGap(sizeLimited, "ManagedInputFileSizeLimitExceeded");

        CompiledInputEvaluation Evaluate(string path) => ManagedMetadataExtractor.Evaluate(repo, commit,
            new ScanOptions(repo, "unused", CompiledInputPaths: [path]));
    }

    [Fact]
    public void Receipt_only_and_oversized_assembly_reference_inputs_emit_rule_backed_gaps()
    {
        using var temp = new TempDirectory();
        var repo = FindRepoRoot();
        var commit = Git(repo, "rev-parse", "HEAD");
        var receipt = Path.Combine(temp.Path, "receipt.json");
        File.WriteAllText(receipt, JsonSerializer.Serialize(new
        {
            schemaVersion = "compiled-input-binding-set.v1",
            bindings = Array.Empty<object>()
        }));

        var receiptOnly = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(
            repo,
            "unused",
            CompiledBindingReceiptPaths: [receipt]));

        Assert.Equal("compiled-metadata-partial", receiptOnly.Provenance!.CoverageState);
        Assert.Contains(receiptOnly.KnownGaps, gap => gap.Contains("NoManagedInputDeclared", StringComparison.Ordinal));
        Assert.Contains(receiptOnly.Candidates, candidate => candidate.FactType == FactTypes.AnalysisGap
            && candidate.Properties.GetValueOrDefault("gapKind") == "NoManagedInputDeclared");

        var oversizedReferenceAssembly = Path.Combine(temp.Path, "oversized-reference.dll");
        using (var assembly = AssemblyDefinition.ReadAssembly(FixtureAssemblies(repo).CSharp))
        {
            assembly.MainModule.AssemblyReferences.Add(new AssemblyNameReference(new string('x', 5_000), new Version(1, 0)));
            assembly.Write(oversizedReferenceAssembly);
        }

        var textLimited = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(
            repo,
            "unused",
            CompiledInputPaths: [oversizedReferenceAssembly]));

        AssertGap(textLimited, "ManagedInputTextLimitExceeded");
        Assert.DoesNotContain(textLimited.Provenance!.Outcomes.SelectMany(outcome => outcome.DependencyResolutionOutcomes),
            outcome => outcome.Length > textLimited.Provenance.EffectiveLimits.MaxTextLength);
    }

    [Fact]
    public void Binding_receipts_require_explicit_ancestry_evidence_for_stale_classification()
    {
        using var temp = new TempDirectory();
        var repo = FindRepoRoot();
        var commit = Git(repo, "rev-parse", "HEAD");
        var assembly = FixtureAssemblies(repo).CSharp;
        var unbound = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused", CompiledInputPaths: [assembly]));
        var outcome = Assert.Single(unbound.Provenance!.Outcomes);
        Assert.Equal("unbound", outcome.ProvenanceState);
        Assert.NotNull(outcome.RawFileSha256);
        Assert.NotNull(outcome.AssemblyIdentity);

        var bound = EvaluateReceipt(commit, outcome.RawFileSha256!);
        Assert.Equal("bound", Assert.Single(bound.Provenance!.Outcomes).ProvenanceState);
        Assert.DoesNotContain("UnboundManagedInput", Assert.Single(bound.Provenance.Outcomes).GapKinds);
        var serializedBound = JsonSerializer.Serialize(bound.Provenance) + JsonSerializer.Serialize(
            ManagedMetadataExtractor.MaterializeFacts(Manifest(commit, bound.Provenance), bound));
        Assert.DoesNotContain("credential-bearing.example", serializedBound, StringComparison.Ordinal);
        Assert.DoesNotContain("private-build-name", serializedBound, StringComparison.Ordinal);
        Assert.Contains("binarySourceRepositorySha256", serializedBound, StringComparison.Ordinal);

        var stale = EvaluateReceipt(new string('1', 40), outcome.RawFileSha256!, "ancestor-of-scan");
        Assert.Equal("stale", Assert.Single(stale.Provenance!.Outcomes).ProvenanceState);
        Assert.Contains("StaleManagedInput", Assert.Single(stale.Provenance.Outcomes).GapKinds);

        var unrelatedCommit = EvaluateReceipt(new string('2', 40), outcome.RawFileSha256!);
        Assert.Equal("mismatch", Assert.Single(unrelatedCommit.Provenance!.Outcomes).ProvenanceState);
        Assert.Contains("ManagedInputProvenanceMismatch", Assert.Single(unrelatedCommit.Provenance.Outcomes).GapKinds);

        var unsupportedRelation = EvaluateReceipt(commit, outcome.RawFileSha256!, "descendant-of-scan");
        Assert.Equal("mismatch", Assert.Single(unsupportedRelation.Provenance!.Outcomes).ProvenanceState);
        Assert.Contains("ManagedInputProvenanceMismatch", Assert.Single(unsupportedRelation.Provenance.Outcomes).GapKinds);

        var mismatch = EvaluateReceipt(commit, new string('0', 64));
        Assert.Equal("mismatch", Assert.Single(mismatch.Provenance!.Outcomes).ProvenanceState);
        Assert.Contains("ManagedInputProvenanceMismatch", Assert.Single(mismatch.Provenance.Outcomes).GapKinds);

        var receiptGap = EvaluateReceipt(
            commit,
            outcome.RawFileSha256!,
            additionalReceiptPaths: [Path.Combine(temp.Path, "missing-receipt.json")]);
        Assert.Equal("bound", Assert.Single(receiptGap.Provenance!.Outcomes).ProvenanceState);
        Assert.Equal("compiled-metadata-partial", receiptGap.Provenance.CoverageState);
        Assert.Contains(receiptGap.KnownGaps, gap => gap.Contains("UnreadableManagedBindingReceipt", StringComparison.Ordinal));

        CompiledInputEvaluation EvaluateReceipt(
            string sourceCommit,
            string artifactSha256,
            string? sourceCommitRelation = null,
            IReadOnlyList<string>? additionalReceiptPaths = null)
        {
            var receipt = Path.Combine(temp.Path, Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(receipt, JsonSerializer.Serialize(new
            {
                schemaVersion = "compiled-input-binding-set.v1",
                bindings = new[]
                {
                    new
                    {
                        schemaVersion = "compiled-input-binding.v1",
                        safeLocator = outcome.SafeLocator,
                        artifactSha256,
                        assemblyIdentity = outcome.AssemblyIdentity,
                        binarySourceRepository = "https://token@credential-bearing.example/private/repository",
                        binarySourceCommitSha = sourceCommit,
                        binarySourceCommitRelation = sourceCommitRelation,
                        binaryBuildIdentity = "private-build-name"
                    }
                }
            }));
            return ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused",
                CompiledInputPaths: [assembly],
                CompiledBindingReceiptPaths: [receipt, .. additionalReceiptPaths ?? []]));
        }
    }

    [Fact]
    public void Reader_disagreement_is_deterministic_and_identifies_the_disputed_row()
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var cecil = new ManagedMetadataExtractor.MetadataObservation(
            "method:0x06000001", "identity-a", FactTypes.ManagedMethodDeclared, RuleIds.DotNetCompiledMember, "method", "0x06000001", properties);
        var srm = cecil with { Identity = "identity-b" };

        var gaps = ManagedMetadataExtractor.CrossCheck([cecil], [srm]);

        var gap = Assert.Single(gaps);
        Assert.Equal("method:0x06000001", gap.Key);
        Assert.Equal("identity-a", gap.CecilIdentity);
        Assert.Equal("identity-b", gap.SystemReflectionMetadataIdentity);
        Assert.Equal(["MetadataReaderDisagreement"], ManagedMetadataExtractor.ReaderDisagreementGapKinds(gaps));
    }

    [Fact]
    public void Duplicate_dependency_candidates_fail_closed_without_host_resolution()
    {
        using var temp = new TempDirectory();
        var repo = FindRepoRoot();
        var commit = Git(repo, "rev-parse", "HEAD");
        var primary = FixtureAssemblies(repo).CSharp;
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var systemRuntime = Path.Combine(runtimeDirectory, "System.Runtime.dll");
        Assert.True(File.Exists(systemRuntime), systemRuntime);
        var firstDirectory = Path.Combine(temp.Path, "first");
        var secondDirectory = Path.Combine(temp.Path, "second");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        var first = Path.Combine(firstDirectory, "System.Runtime.dll");
        var second = Path.Combine(secondDirectory, "System.Runtime.dll");
        File.Copy(systemRuntime, first);
        File.Copy(systemRuntime, second);

        var evaluation = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused",
            CompiledInputPaths: [primary],
            CompiledDependencyPaths: [second, first]));

        Assert.Contains(evaluation.Provenance!.Outcomes.SelectMany(item => item.GapKinds), gap =>
            gap == "AmbiguousManagedAssemblyReference");
        Assert.Contains(evaluation.Provenance.Outcomes.SelectMany(item => item.DependencyResolutionOutcomes), outcome =>
            outcome.Contains("=>ambiguous:", StringComparison.Ordinal));
    }

    [Fact]
    public void Duplicate_primary_assembly_candidates_are_explicitly_ambiguous()
    {
        using var temp = new TempDirectory();
        var repo = FindRepoRoot();
        var commit = Git(repo, "rev-parse", "HEAD");
        var source = FixtureAssemblies(repo).CSharp;
        var firstDirectory = Path.Combine(temp.Path, "first");
        var secondDirectory = Path.Combine(temp.Path, "second");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        var first = Path.Combine(firstDirectory, "fixture.dll");
        var second = Path.Combine(secondDirectory, "fixture.dll");
        File.Copy(source, first);
        File.Copy(source, second);

        var evaluation = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused", CompiledInputPaths: [first, second]));

        Assert.Equal(2, evaluation.Provenance!.Outcomes.Count);
        Assert.All(evaluation.Provenance.Outcomes, outcome => Assert.Contains("AmbiguousDuplicateManagedAssembly", outcome.GapKinds));
    }

    [Fact]
    public void Dual_role_inputs_and_receipt_limits_fail_closed_without_aborting()
    {
        using var temp = new TempDirectory();
        var repo = FindRepoRoot();
        var commit = Git(repo, "rev-parse", "HEAD");
        var assembly = FixtureAssemblies(repo).CSharp;
        var dualRole = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused",
            CompiledInputPaths: [assembly],
            CompiledDependencyPaths: [assembly]));

        Assert.Equal(2, dualRole.Provenance!.Outcomes.Count);
        Assert.Equal(["dependency", "primary"], dualRole.Provenance.Outcomes.Select(item => item.Role).OrderBy(value => value, StringComparer.Ordinal).ToArray());
        Assert.Equal(2, dualRole.Provenance.Outcomes.Select(item => item.SafeLocator).Distinct(StringComparer.Ordinal).Count());

        var oversizedReceipt = Path.Combine(temp.Path, "oversized.json");
        File.WriteAllText(oversizedReceipt, new string('x', 32));
        var sizeLimited = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused",
            CompiledBindingReceiptPaths: [oversizedReceipt],
            CompiledInputLimits: new CompiledInputLimits(MaxFileSizeBytes: 8)));
        Assert.Contains(sizeLimited.KnownGaps, gap => gap.Contains("ManagedBindingReceiptFileSizeLimitExceeded", StringComparison.Ordinal));

        var bindingLimitedReceipt = Path.Combine(temp.Path, "bindings.json");
        File.WriteAllText(bindingLimitedReceipt, JsonSerializer.Serialize(new
        {
            schemaVersion = "compiled-input-binding-set.v1",
            bindings = new[]
            {
                new { schemaVersion = "compiled-input-binding.v1", safeLocator = "one", artifactSha256 = new string('0', 64) },
                new { schemaVersion = "compiled-input-binding.v1", safeLocator = "two", artifactSha256 = new string('1', 64) }
            }
        }));
        var bindingLimited = ManagedMetadataExtractor.Evaluate(repo, commit, new ScanOptions(repo, "unused",
            CompiledBindingReceiptPaths: [bindingLimitedReceipt],
            CompiledInputLimits: new CompiledInputLimits(MaxArtifactCount: 1)));
        Assert.Contains(bindingLimited.KnownGaps, gap => gap.Contains("ManagedBindingReceiptBindingCountLimitExceeded", StringComparison.Ordinal));
    }

    [Fact]
    public void Array_shape_normalization_preserves_non_vector_rank_sizes_and_bounds()
    {
        Assert.Equal("[rank=1;sizes=-;lowerBounds=-]", ManagedMetadataExtractor.FormatArrayShape(1, [], []));
        Assert.Equal("[rank=2;sizes=3,4;lowerBounds=-1,2]", ManagedMetadataExtractor.FormatArrayShape(2, [3, 4], [-1, 2]));
        Assert.NotEqual("[]", ManagedMetadataExtractor.FormatArrayShape(1, [], []));
    }

    [Fact]
    public async Task Cli_outputs_are_deterministic_private_safe_and_preserve_source_evidence()
    {
        var repoRoot = FindRepoRoot();
        using var temp = new TempDirectory(Path.GetDirectoryName(repoRoot));
        var repo = Path.Combine(repoRoot, "samples", "modern-sample");
        var assembly = FixtureAssemblies(repoRoot).CSharp;
        var baselineOut = Path.Combine(temp.Path, "baseline");
        var firstOut = Path.Combine(temp.Path, "first");
        var secondOut = Path.Combine(temp.Path, "second");

        Assert.Equal(0, await RunScan(repo, baselineOut));
        Assert.Equal(0, await RunScan(repo, firstOut, assembly));
        Assert.Equal(0, await RunScan(repo, secondOut, assembly));

        Assert.Equal(
            await File.ReadAllBytesAsync(Path.Combine(firstOut, "facts.ndjson")),
            await File.ReadAllBytesAsync(Path.Combine(secondOut, "facts.ndjson")));
        using var baselineManifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(baselineOut, "scan-manifest.json")));
        using var firstManifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(firstOut, "scan-manifest.json")));
        using var secondManifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(secondOut, "scan-manifest.json")));
        var baselineScanId = baselineManifestDocument.RootElement.GetProperty("scanId").GetString();
        var firstScanId = firstManifestDocument.RootElement.GetProperty("scanId").GetString();
        var secondScanId = secondManifestDocument.RootElement.GetProperty("scanId").GetString();
        Assert.NotEqual(baselineScanId, firstScanId);
        Assert.Equal(firstScanId, secondScanId);
        Assert.Equal(
            baselineManifestDocument.RootElement.GetProperty("analysisLevel").GetString(),
            firstManifestDocument.RootElement.GetProperty("analysisLevel").GetString());
        Assert.Equal(
            "compiled-metadata-partial",
            firstManifestDocument.RootElement.GetProperty("compiledInputProvenance").GetProperty("coverageState").GetString());
        foreach (var output in new[] { firstOut, secondOut })
        {
            Assert.True(File.Exists(Path.Combine(output, "scan-manifest.json")));
            Assert.True(File.Exists(Path.Combine(output, "facts.ndjson")));
            Assert.True(File.Exists(Path.Combine(output, "index.sqlite")));
            Assert.True(File.Exists(Path.Combine(output, "report.md")));
            Assert.True(File.Exists(Path.Combine(output, "logs", "analyzer.log")));
            var machineReadable = await File.ReadAllTextAsync(Path.Combine(output, "scan-manifest.json"))
                + await File.ReadAllTextAsync(Path.Combine(output, "facts.ndjson"));
            Assert.DoesNotContain(repoRoot, machineReadable, StringComparison.Ordinal);
            Assert.Contains("compiledInputProvenance", machineReadable, StringComparison.Ordinal);
        }

        // Compiled facts may change their interleaving with source facts in the complete
        // scan output. Compare the canonical source-evidence projection so this assertion
        // proves content and cardinality are unchanged without depending on that interleaving.
        var baseline = ReadFacts(baselineOut).Where(IsSourceEvidence).Select(NormalizeFact).Order(StringComparer.Ordinal).ToArray();
        var compiled = ReadFacts(firstOut).Where(IsSourceEvidence).Select(NormalizeFact).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(baseline, compiled);

        using var connection = new SqliteConnection($"Data Source={Path.Combine(firstOut, "index.sqlite")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where rule_id like 'dotnet.compiled.%'";
        Assert.True(Convert.ToInt32(command.ExecuteScalar()) > 0);

        async Task<int> RunScan(string repository, string output, string? compiledInput = null)
        {
            var arguments = new List<string> { "scan", "--repo", repository, "--out", output };
            if (compiledInput is not null)
                arguments.AddRange(["--compiled-input", compiledInput]);
            return await TraceMapCommand.RunAsync(arguments.ToArray(), TextWriter.Null, TextWriter.Null);
        }
    }

    [Fact]
    public void Rule_catalog_registers_active_compiled_and_reconciliation_rules()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains("- id: dotnet.compiled.input.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.assembly.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.member.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.gap.v1", catalog, StringComparison.Ordinal);
        var reconciliation = catalog[catalog.IndexOf("- id: dotnet.compiled.source-identity.v1", StringComparison.Ordinal)..];
        Assert.Contains("status: active", reconciliation[..Math.Min(reconciliation.Length, 1_000)], StringComparison.Ordinal);
        Assert.Contains("SourceMetadataIdentityReconciled", reconciliation[..Math.Min(reconciliation.Length, 1_000)], StringComparison.Ordinal);
    }

    private static void WriteMultiModuleManifest(string path)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString("multi-module.dll"),
            metadata.GetOrAddGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            default,
            default);
        metadata.AddAssembly(
            metadata.GetOrAddString("MultiModule"),
            new Version(1, 0, 0, 0),
            default,
            default,
            (System.Reflection.AssemblyFlags)0,
            System.Reflection.AssemblyHashAlgorithm.Sha256);
        metadata.AddAssemblyFile(
            metadata.GetOrAddString("secondary.netmodule"),
            metadata.GetOrAddBlob(SHA256.HashData([1, 2, 3])),
            containsMetadata: true);
        metadata.AddTypeDefinition(
            System.Reflection.TypeAttributes.NotPublic,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        var peBuilder = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage | Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            new System.Reflection.Metadata.BlobBuilder(),
            flags: CorFlags.ILOnly);
        var peBlob = new System.Reflection.Metadata.BlobBuilder();
        peBuilder.Serialize(peBlob);
        File.WriteAllBytes(path, peBlob.ToArray());
    }

    private static void AssertGap(CompiledInputEvaluation evaluation, params string[] expected)
    {
        Assert.NotNull(evaluation.Provenance);
        Assert.Equal("compiled-metadata-partial", evaluation.Provenance.CoverageState);
        var gaps = evaluation.Provenance.Outcomes.SelectMany(item => item.GapKinds).ToArray();
        Assert.Contains(gaps, actual => expected.Contains(actual, StringComparer.Ordinal));
    }

    private static IReadOnlyList<CodeFact> ReadFacts(string output) =>
        File.ReadLines(Path.Combine(output, "facts.ndjson"))
            .Select(line => JsonSerializer.Deserialize<CodeFact>(line, JsonOptions.StableLine)!)
            .ToArray();

    private static string NormalizeFact(CodeFact fact) => JsonSerializer.Serialize(fact with
    {
        FactId = string.Empty,
        ScanId = string.Empty
    }, JsonOptions.StableLine);

    private static bool IsSourceEvidence(CodeFact fact) =>
        !fact.RuleId.StartsWith("dotnet.compiled", StringComparison.Ordinal)
        && fact.FactType != FactTypes.AnalyzerCapabilityDiagnostic;

    private static (string CSharp, string VisualBasic, string FSharp) FixtureAssemblies(string repo)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        string Resolve(string language, string name) => Path.Combine(repo, "samples", "compiled-dotnet-evidence", language, "bin", configuration, "net10.0", name);
        var result = (
            Resolve("csharp", "CompiledEvidence.CSharp.dll"),
            Resolve("vb", "CompiledEvidence.VisualBasic.dll"),
            Resolve("fsharp", "CompiledEvidence.FSharp.dll"));
        Assert.True(File.Exists(result.Item1), result.Item1);
        Assert.True(File.Exists(result.Item2), result.Item2);
        Assert.True(File.Exists(result.Item3), result.Item3);
        return result;
    }

    private static ScanManifest Manifest(string commit, CompiledInputProvenance? provenance) => new(
        "scan-compiled-fixture",
        "compiled-fixture",
        null,
        "fixture",
        commit,
        ScannerVersions.TraceMap,
        DateTimeOffset.UnixEpoch,
        "Level1SemanticAnalysisReduced",
        "Succeeded",
        [],
        [],
        ["net10.0"],
        [],
        SourceSnapshotDigest: new string('0', 64),
        CompiledInputProvenance: provenance);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string Git(string repo, params string[] arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
        return output;
    }
}
