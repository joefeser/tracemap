using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
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
            Assert.Contains("|targetFramework:.NETCoreApp,Version=v10.0", fact.TargetSymbol, StringComparison.Ordinal));
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
            && fact.TargetSymbol?.Contains("|method:Overload|", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(3, overloads.Length);
        Assert.Equal(3, overloads.Select(fact => fact.TargetSymbol).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("namespace:TraceMap.CompiledFixtures.CSharp.Alpha|name:Widget`1+Nested`1", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("method:Generic|arity:1", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("System.Int32&", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("System.Int32[]", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("System.Int32*", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("namespace:<global>|name:GlobalNamespaceShape", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.FactType == FactTypes.ManagedPropertyDeclared && fact.TargetSymbol?.Contains("property:Item", StringComparison.Ordinal) == true);
        Assert.Contains(facts, fact => fact.TargetSymbol?.Contains("method:RenamedForMetadata", StringComparison.Ordinal) == true);
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

        var baseline = ReadFacts(baselineOut).Where(IsSourceEvidence).Select(NormalizeFact).ToArray();
        var compiled = ReadFacts(firstOut).Where(IsSourceEvidence).Select(NormalizeFact).ToArray();
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
    public void Rule_catalog_registers_active_compiled_rules_and_defers_reconciliation()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains("- id: dotnet.compiled.input.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.assembly.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.member.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.gap.v1", catalog, StringComparison.Ordinal);
        var deferred = catalog[catalog.IndexOf("- id: dotnet.compiled.source-identity.v1", StringComparison.Ordinal)..];
        Assert.Contains("status: deferred", deferred[..Math.Min(deferred.Length, 500)], StringComparison.Ordinal);
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
