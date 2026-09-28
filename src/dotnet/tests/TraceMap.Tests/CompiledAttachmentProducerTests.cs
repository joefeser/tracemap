using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class CompiledAttachmentProducerTests
{
    [Fact]
    public void Compiled_only_producer_reuses_exact_metadata_and_il_facts_without_source_analysis_or_output()
    {
        using var fixture = new Fixture();
        var originalJson = JsonSerializer.Serialize(fixture.Parent);
        var beforeBytes = File.ReadAllBytes(fixture.Source);
        var result = fixture.Create();
        var manifest = result.Manifest;
        var context = Assert.IsType<CompiledAttachmentContext>(manifest.CompiledAttachment);
        CompiledAttachmentProducer.ValidateContext(manifest);
        Assert.Equal(originalJson, JsonSerializer.Serialize(fixture.Parent));
        Assert.Equal(beforeBytes, File.ReadAllBytes(fixture.Source));
        Assert.False(Directory.Exists(fixture.Options.OutputPath));
        Assert.Empty(result.Inventory);
        Assert.Null(result.SourceSnapshotInventory);
        Assert.Equal("NotRun", manifest.BuildStatus);
        Assert.Equal("CompiledStaticEvidenceReduced", manifest.AnalysisLevel);
        Assert.Equal(fixture.Parent.Manifest.SourceSnapshotDigest, manifest.SourceSnapshotDigest);
        Assert.NotEqual(fixture.Parent.Manifest.ScanId, manifest.ScanId);
        Assert.Equal(fixture.Parent.Manifest.ScanId, context.ParentScanId);
        Assert.Equal(fixture.Parent.ManifestSha256, context.ParentManifestSha256);
        Assert.Equal(fixture.Parent.IndexSha256, context.ParentIndexSha256);
        Assert.Equal(1, context.SourceFiles);
        Assert.Equal(beforeBytes.Length, context.SourceBytes);
        Assert.Equal(Hash(File.ReadAllBytes(typeof(CompiledAttachmentProducer).Assembly.Location)), context.GeneratorSha256);
        Assert.Equal("review-only-static-not-runtime", context.ClaimLevel);
        Assert.Equal("local-only", context.Visibility);
        Assert.Contains("ParentFixtureGap", manifest.KnownGaps);
        Assert.Null(manifest.SourceMetadataReconciliation);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.FileInventoried or FactTypes.MethodDeclared
            or FactTypes.CallEdge or FactTypes.SourceMetadataIdentityObserved or FactTypes.SourceMetadataIdentityReconciled);
        Assert.All(result.Facts, fact =>
        {
            Assert.Equal(manifest.ScanId, fact.ScanId);
            Assert.Equal(fixture.Parent.Manifest.CommitSha, fact.CommitSha);
            Assert.Null(fact.Evidence.SnippetHash);
            Assert.False(string.IsNullOrWhiteSpace(fact.RuleId));
        });
        var metadata = ManagedMetadataExtractor.Evaluate(fixture.Root, manifest.CommitSha, fixture.Options);
        var compiled = ManagedMetadataExtractor.MaterializeFacts(manifest, metadata);
        var il = IlBodyEvidenceExtractor.Evaluate(fixture.Options, metadata);
        var expected = compiled.Concat(IlBodyEvidenceExtractor.MaterializeFacts(manifest, il, compiled));
        Assert.Equal(expected.Select(fact => JsonSerializer.Serialize(fact)).Order(StringComparer.Ordinal),
            result.Facts.Where(fact => fact.RuleId != CompiledAttachmentProducer.RuleId)
                .Select(fact => JsonSerializer.Serialize(fact)).Order(StringComparer.Ordinal));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved);
        Assert.All(result.Facts.Where(fact => fact.RuleId == CompiledAttachmentProducer.RuleId), fact =>
        {
            Assert.Equal(EvidenceTiers.Tier4Unknown, fact.EvidenceTier);
            Assert.Equal(context.GeneratorSha256, fact.Properties["generatorSha256"]);
            Assert.Equal(context.BoundedInputSha256, fact.Properties["boundedInputSha256"]);
        });
    }

    [Fact]
    public void Repeated_production_is_deterministic_except_observation_time_and_context_changes_change_identity()
    {
        using var fixture = new Fixture();
        var first = fixture.Create();
        var second = fixture.Create();
        Assert.Equal(JsonSerializer.Serialize(first.Manifest with { ScannedAt = DateTimeOffset.UnixEpoch }),
            JsonSerializer.Serialize(second.Manifest with { ScannedAt = DateTimeOffset.UnixEpoch }));
        Assert.Equal(JsonSerializer.Serialize(first.Facts), JsonSerializer.Serialize(second.Facts));
        var changed = fixture.Create(fixture.Parent with { IndexSha256 = new string('c', 64) });
        Assert.NotEqual(first.Manifest.ScanId, changed.Manifest.ScanId);
        Assert.NotEqual(first.Manifest.CompiledAttachment!.BoundedInputSha256, changed.Manifest.CompiledAttachment!.BoundedInputSha256);
    }

    [Theory]
    [InlineData("index")]
    [InlineData("manifest")]
    [InlineData("root")]
    [InlineData("snapshot")]
    [InlineData("commit")]
    public void Invalid_parent_context_refuses_before_compiled_production(string field)
    {
        using var fixture = new Fixture();
        var parent = field switch
        {
            "index" => fixture.Parent with { IndexSha256 = "invalid" },
            "manifest" => fixture.Parent with { ManifestSha256 = "invalid" },
            "root" => fixture.Parent with { Manifest = fixture.Parent.Manifest with { ScanRootPathHash = new string('d', 32) } },
            "snapshot" => fixture.Parent with { Manifest = fixture.Parent.Manifest with { SourceSnapshotDigest = null } },
            _ => fixture.Parent with { Manifest = fixture.Parent.Manifest with { CommitSha = "unknown" } }
        };
        Assert.Equal("COMPILED_ATTACHMENT_PARENT_CONTEXT_INVALID", Assert.Throws<InvalidOperationException>(() => fixture.Create(parent)).Message);
    }

    [Theory]
    [InlineData("restore")]
    [InlineData("project")]
    [InlineData("include")]
    [InlineData("rewrite")]
    public void Source_build_and_rewrite_configuration_cannot_silently_run_in_attachment(string option)
    {
        using var fixture = new Fixture();
        var options = option switch
        {
            "restore" => fixture.Options with { Restore = true },
            "project" => fixture.Options with { ProjectPaths = ["missing.csproj"] },
            "include" => fixture.Options with { IncludeGlobs = ["**/*"] },
            _ => fixture.Options with { IlRewriteEvidence = true }
        };
        Assert.Equal("COMPILED_ATTACHMENT_SOURCE_BUILD_OR_REWRITE_OPTIONS_NOT_ALLOWED",
            Assert.Throws<InvalidOperationException>(() => fixture.Create(options: options)).Message);
    }

    [Fact]
    public void Snapshot_limits_changed_bytes_missing_inputs_and_cancellation_refuse()
    {
        using var fixture = new Fixture();
        Assert.Equal("SourceSnapshotInputLimit", Assert.Throws<InvalidOperationException>(() =>
            fixture.Create(maxBytes: 1)).Message);
        Assert.Equal("COMPILED_ATTACHMENT_NO_COMPILED_INPUTS", Assert.Throws<InvalidOperationException>(() =>
            fixture.Create(options: fixture.Options with { CompiledInputPaths = [] })).Message);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => fixture.Create(token: cancelled.Token));
        File.WriteAllText(fixture.Source, new string('x', (int)new FileInfo(fixture.Source).Length));
        Assert.Equal("COMPILED_ATTACHMENT_SOURCE_SNAPSHOT_MISMATCH", Assert.Throws<InvalidOperationException>(() => fixture.Create()).Message);
    }

    [Fact]
    public void Source_changed_during_compiled_extraction_refuses_final_admission()
    {
        using var fixture = new Fixture();
        var passes = 0;
        IEnumerable<FileInventoryItem> Inventory()
        {
            if (++passes == 2) File.WriteAllText(fixture.Source, new string('x', (int)new FileInfo(fixture.Source).Length));
            return fixture.Inventory;
        }
        Assert.Equal("COMPILED_ATTACHMENT_SOURCE_SNAPSHOT_MISMATCH", Assert.Throws<InvalidOperationException>(() =>
            CompiledAttachmentProducer.Create(fixture.Parent, fixture.Options, Inventory, 10, 1024)).Message);
        Assert.Equal(2, passes);
    }

    [Fact]
    public void Ordinary_manifest_serialization_remains_unchanged_when_attachment_is_absent()
    {
        using var fixture = new Fixture();
        Assert.DoesNotContain("CompiledAttachment", JsonSerializer.Serialize(fixture.Parent.Manifest), StringComparison.Ordinal);
        var result = fixture.Create();
        var roundtrip = JsonSerializer.Deserialize<ScanManifest>(JsonSerializer.Serialize(result.Manifest))!;
        Assert.Equal(result.Manifest.CompiledAttachment, roundtrip.CompiledAttachment);
    }

    [Theory]
    [InlineData("parent")]
    [InlineData("snapshot")]
    [InlineData("generator")]
    [InlineData("input")]
    [InlineData("limits")]
    [InlineData("claim")]
    [InlineData("metadata")]
    public void Context_revalidation_rejects_stale_parent_generator_limits_claim_and_lane_provenance(string mutation)
    {
        using var fixture = new Fixture();
        var result = fixture.Create();
        var manifest = result.Manifest;
        var context = manifest.CompiledAttachment!;
        context = mutation switch
        {
            "parent" => context with { ParentIndexSha256 = new string('d', 64) },
            "snapshot" => context with { ParentSourceSnapshotDigest = new string('d', 64) },
            "generator" => context with { GeneratorSha256 = new string('d', 64) },
            "input" => context with { BoundedInputSha256 = new string('d', 64) },
            "limits" => context with { MaxSourceFiles = 999 },
            "claim" => context with { ClaimLevel = "runtime" },
            _ => context
        };
        manifest = manifest with { CompiledAttachment = context };
        if (mutation == "metadata") manifest = manifest with { CompiledInputProvenance = manifest.CompiledInputProvenance! with { BoundedInputSha256 = new string('d', 64) } };
        Assert.Equal("COMPILED_ATTACHMENT_CONTEXT_INVALID", Assert.Throws<InvalidOperationException>(() =>
            CompiledAttachmentProducer.ValidateContext(manifest)).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Portable_pdb_uses_only_verified_retained_members_and_preserves_its_source_admission_limit(bool limited)
    {
        using var fixture = new Fixture();
        var assembly = fixture.Options.CompiledInputPaths!.Single();
        var publicSource = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assembly)!, "../../..", "FixtureShapes.cs"));
        File.Copy(publicSource, fixture.Source, overwrite: true);
        var inventory = new List<FileInventoryItem> { new("Unbuildable.cs", "CSharp", new FileInfo(fixture.Source).Length) };
        if (limited)
        {
            File.Copy(fixture.Source, Path.Combine(fixture.Root, "ZSecond.cs"));
            inventory.Add(new("ZSecond.cs", "CSharp", new FileInfo(fixture.Source).Length));
        }
        var snapshot = SourceSnapshotInspector.InspectOrderedInventory(fixture.Root, inventory, 10, 100_000);
        var manifest = fixture.Parent.Manifest with { SourceSnapshotDigest = snapshot.Digest };
        var parent = fixture.Parent with { Manifest = manifest, ManifestSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(manifest)) };
        var initial = ManagedMetadataExtractor.Evaluate(fixture.Root, manifest.CommitSha, fixture.Options);
        var binding = Path.Combine(fixture.Root, "binding.local.json");
        File.WriteAllText(binding, JsonSerializer.Serialize(new
        {
            schemaVersion = "compiled-input-binding-set.v1",
            bindings = initial.Provenance!.Outcomes.Select(outcome => new
            {
                schemaVersion = "compiled-input-binding.v1", safeLocator = outcome.SafeLocator,
                artifactSha256 = outcome.RawFileSha256, assemblyIdentity = outcome.AssemblyIdentity,
                binarySourceRepository = "public-fixture", binarySourceCommitSha = manifest.CommitSha,
                binaryBuildIdentity = "synthetic-attachment-pdb-policy-not-build-proof"
            })
        }));
        var options = fixture.Options with { CompiledBindingReceiptPaths = [binding],
            PdbInputPaths = [Path.ChangeExtension(assembly, ".pdb")],
            PdbInputLimits = new(MaxSourceFileCount: limited ? 1 : 10) };
        var result = CompiledAttachmentProducer.Create(parent, options, () => inventory, 10, 100_000);
        Assert.NotNull(result.Manifest.PdbEvidenceSummary);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbDocumentDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled);
        if (limited)
        {
            Assert.Contains(result.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "PdbSourceFileCountExceeded");
            Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.PdbSourceDocumentReconciled);
        }
        else Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbSourceDocumentReconciled
            && fact.Evidence.FilePath == "Unbuildable.cs");
        var evaluated = ManagedMetadataExtractor.Evaluate(fixture.Root, manifest.CommitSha, options);
        var compiled = ManagedMetadataExtractor.MaterializeFacts(result.Manifest, evaluated);
        var pdb = PortablePdbExtractor.Evaluate(fixture.Root, options, evaluated);
        var expected = PortablePdbExtractor.MaterializeFacts(fixture.Root, result.Manifest, pdb, compiled, inventory);
        Assert.Equal(expected.Select(fact => JsonSerializer.Serialize(fact)).Order(StringComparer.Ordinal),
            result.Facts.Where(fact => fact.RuleId.StartsWith("dotnet.compiled.pdb", StringComparison.Ordinal)
                || fact.RuleId == RuleIds.DotNetPdbSequencePoint)
                .Select(fact => JsonSerializer.Serialize(fact)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Missing_publish_receipt_preserves_policy_gap_without_manufacturing_source_bindings()
    {
        using var fixture = new Fixture();
        var options = fixture.Options with { WebFormsPublishReceiptPath = Path.Combine(fixture.Root, "missing-publish.json") };
        var result = fixture.Create(options: options);
        Assert.Equal("gap", result.Manifest.WebFormsPublishProvenance!.Status);
        Assert.Contains("WebFormsPublishReceiptUnreadable", result.Manifest.WebFormsPublishProvenance.GapKinds);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.WebFormsPublishSourceBound
            or FactTypes.WebFormsPublishPageMapped or FactTypes.WebFormsPublishPageCandidate);
        var expected = WebFormsPublishMapExtractor.MaterializeFacts(result.Manifest,
            WebFormsPublishMapExtractor.Evaluate(fixture.Root, result.Manifest.CommitSha, options, CancellationToken.None));
        Assert.Equal(expected.Select(fact => JsonSerializer.Serialize(fact)).Order(StringComparer.Ordinal),
            result.Facts.Where(fact => fact.RuleId == RuleIds.LegacyWebFormsPublishMap)
                .Select(fact => JsonSerializer.Serialize(fact)).Order(StringComparer.Ordinal));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tracemap-public-compiled-attachment-" + Guid.NewGuid().ToString("N"));
        public string Source => Path.Combine(Root, "Unbuildable.cs");
        public IReadOnlyList<FileInventoryItem> Inventory { get; }
        public CompiledAttachmentParent Parent { get; }
        public ScanOptions Options { get; }
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            // Intentionally invalid source: attachment must not invoke source
            // analysis. This tests Core production, not native parent admission.
            File.WriteAllText(Source, "intentionally unbuildable { @@@");
            Inventory = [new("Unbuildable.cs", "CSharp", new FileInfo(Source).Length)];
            var snapshot = SourceSnapshotInspector.InspectOrderedInventory(Root, Inventory, 10, 1024);
            var manifest = new ScanManifest("scan-retained-public-fixture", "public-fixture", null, "fixture",
                new string('a', 40), ScannerVersions.TraceMap, DateTimeOffset.UnixEpoch,
                "Level3SyntaxAnalysisReduced", "NotRun", [], [], [], ["ParentFixtureGap"],
                ScanRootPathHash: FactFactory.Hash(Root, 32), SourceSnapshotDigest: snapshot.Digest);
            Parent = new(manifest, Hash(JsonSerializer.SerializeToUtf8Bytes(manifest)), new string('b', 64));
            var repo = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "rules", "rule-catalog.yml"))) repo = repo.Parent;
            Assert.NotNull(repo);
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var assembly = Path.Combine(repo.FullName, "samples", "compiled-dotnet-evidence", "csharp", "bin", configuration,
                "net10.0", "CompiledEvidence.CSharp.dll");
            Assert.True(File.Exists(assembly));
            Options = new(Root, Path.Combine(Root, "must-not-be-written"), CompiledInputPaths: [assembly], IlBodyEvidence: true);
        }
        public ScanResult Create(CompiledAttachmentParent? parent = null, ScanOptions? options = null,
            long maxBytes = 1024, CancellationToken token = default) =>
            CompiledAttachmentProducer.Create(parent ?? Parent, options ?? Options, () => Inventory, 10, maxBytes, token);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
