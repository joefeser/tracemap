using System.Security.Cryptography;

namespace TraceMap.Core;

/// <summary>Original parent artifact hashes must be independently checked by the caller.</summary>
public sealed record CompiledAttachmentParent(ScanManifest Manifest, string ManifestSha256, string IndexSha256);

public sealed record CompiledAttachmentContext(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility, string ClaimLevel,
    string GeneratorSha256, string BoundedInputSha256,
    string ParentScanId, string ParentManifestSha256, string ParentIndexSha256,
    string ParentSourceSnapshotDigest, long SourceFiles, long SourceBytes,
    long MaxSourceFiles, long MaxSourceBytes,
    string Limitation);

/// <summary>
/// Produces only explicitly requested compiled/PDB/publish evidence. No source
/// extractors, Git discovery, MSBuild, source fact copying or output writes run.
/// The caller owns immutable parent artifact validation and final admission.
/// </summary>
public static class CompiledAttachmentProducer
{
    public const string Schema = "compiled-source-attachment.v1";
    public const string RuleId = "workflow.compiled-source-attachment.v1";
    public const string Limitation = "This is a local review-only compiled evidence attachment over verified retained source bytes. Parent artifact hashes require independent caller validation; hashes are not authenticity signatures. Source analysis and builds were not rerun, parent facts were not relabeled or copied, and cross-index source joins require independently validated parent context. No runtime execution, source-line identity, clean working tree, build authenticity or full-site coverage is implied.";

    public static ScanResult Create(CompiledAttachmentParent parent, ScanOptions options,
        Func<IEnumerable<FileInventoryItem>> retainedInventory,
        long maxSourceFiles, long maxSourceBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(parent.Manifest);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(retainedInventory);
        cancellationToken.ThrowIfCancellationRequested();
        var original = parent.Manifest;
        var root = Path.GetFullPath(options.RepoPath);
        if (!Sha(parent.ManifestSha256) || !Sha(parent.IndexSha256) || !Sha(original.SourceSnapshotDigest)
            || string.IsNullOrWhiteSpace(original.ScanId) || string.IsNullOrWhiteSpace(original.RepoName)
            || original.CommitSha.Length != 40 || !original.CommitSha.All(Hex)
            || original.ScanRootPathHash != FactFactory.Hash(root, 32))
            throw Fail("PARENT_CONTEXT_INVALID");
        if (options.Restore || options.SolutionPaths is { Count: > 0 } || options.ProjectPaths is { Count: > 0 }
            || options.IncludeGlobs is { Count: > 0 } || options.ExcludeGlobs is { Count: > 0 }
            || options.TargetFramework is not null || options.BinlogPaths is { Count: > 0 }
            || options.BinlogCommitSha is not null || options.ExactSourceScope || options.IlRewriteEvidence
            || options.IlRewritePdbEvidence || options.IlRewriteBeforePaths is { Count: > 0 }
            || options.IlRewriteAfterPaths is { Count: > 0 } || options.IlRewriteBeforePdbPaths is { Count: > 0 }
            || options.IlRewriteAfterPdbPaths is { Count: > 0 })
            throw Fail("SOURCE_BUILD_OR_REWRITE_OPTIONS_NOT_ALLOWED");
        // Retain only the bounded PDB source-checksum candidates from the
        // inventory pass that actually verified the original snapshot. Never
        // let a second caller enumeration substitute unverified PDB locators.
        var pdbInventory = new List<FileInventoryItem>();
        var capturePdbInventory = options.PdbInputPaths is { Count: > 0 };
        var pdbSourceLimit = (options.PdbInputLimits ?? new PdbInputLimits()).MaxSourceFileCount;
        if (capturePdbInventory && pdbSourceLimit < 1) throw Fail("PDB_SOURCE_LIMIT_INVALID");
        var source = Inspect(capturePdbInventory);
        var generator = Generator();
        var metadata = ManagedMetadataExtractor.Evaluate(root, original.CommitSha, options, cancellationToken);
        if (metadata.Provenance is null) throw Fail("NO_COMPILED_INPUTS");
        var pdb = PortablePdbExtractor.Evaluate(root, options, metadata, cancellationToken);
        var il = IlBodyEvidenceExtractor.Evaluate(options, metadata, cancellationToken);
        var publish = WebFormsPublishMapExtractor.Evaluate(root, original.CommitSha, options, cancellationToken);
        var context = new CompiledAttachmentContext(Schema, RuleId, EvidenceTiers.Tier2Structural,
            "local-only", "review-only-static-not-runtime", generator, "",
            original.ScanId, parent.ManifestSha256, parent.IndexSha256, original.SourceSnapshotDigest!,
            source.FileCount, source.Bytes, maxSourceFiles, maxSourceBytes, Limitation);
        var inputHash = ContextDigest(original with
        {
            ScannerVersion = ScannerVersions.TraceMap, CompiledInputProvenance = metadata.Provenance,
            PdbInputProvenance = pdb.Provenance, IlBodyProvenance = il.Provenance,
            WebFormsPublishProvenance = publish.Provenance
        }, context);
        context = context with { BoundedInputSha256 = inputHash };
        var attachmentGaps = new[] { "SourceAnalysisNotRunForAttachment", "BuildNotRunForAttachment",
            "SourceSemanticReconciliationNotRunForAttachment", "CompiledAttachmentReviewOnly" };
        var gaps = original.KnownGaps.Concat(attachmentGaps).Concat(metadata.KnownGaps)
            .Concat(pdb.KnownGaps).Concat(il.KnownGaps)
            .Concat(publish.Provenance?.GapKinds ?? [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var manifest = new ScanManifest("scan-compiled-" + inputHash[..20], original.RepoName,
            original.RemoteUrl, original.Branch, original.CommitSha, ScannerVersions.TraceMap,
            DateTimeOffset.UtcNow, "CompiledStaticEvidenceReduced", "NotRun", [], [], [], gaps,
            original.ScanRootRelativePath, original.ScanRootPathHash, original.GitRootHash,
            original.SourceSnapshotDigest, metadata.Provenance,
            PdbInputProvenance: pdb.Provenance, IlBodyProvenance: il.Provenance,
            WebFormsPublishProvenance: publish.Provenance) { CompiledAttachment = context };
        var compiledFacts = ManagedMetadataExtractor.MaterializeFacts(manifest, metadata);
        var facts = new List<CodeFact>(compiledFacts);
        facts.AddRange(PortablePdbExtractor.MaterializeFacts(root, manifest, pdb, compiledFacts,
            pdbInventory, cancellationToken));
        facts.AddRange(IlBodyEvidenceExtractor.MaterializeFacts(manifest, il, compiledFacts, cancellationToken));
        facts.AddRange(WebFormsPublishMapExtractor.MaterializeFacts(manifest, publish));
        foreach (var gap in attachmentGaps)
            facts.Add(FactFactory.Create(manifest, FactTypes.AnalysisGap, RuleId, EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(".", 1, 1, null, nameof(CompiledAttachmentProducer), Schema),
                contractElement: gap, properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gapKind"] = gap, ["generatorSha256"] = generator,
                    ["boundedInputSha256"] = inputHash, ["parentScanId"] = original.ScanId,
                    ["limitation"] = Limitation
                }));
        if (Inspect() != source) throw Fail("SOURCE_SNAPSHOT_CHANGED");
        if (Generator() != generator) throw Fail("GENERATOR_CHANGED");
        manifest = PortablePdbExtractor.FinalizeManifest(manifest, facts);
        manifest = manifest with { PdbEvidenceSummary = PortablePdbExtractor.BuildSummary(manifest, facts) };
        cancellationToken.ThrowIfCancellationRequested();
        // No invented FileInventoried rows, parent fact IDs or semantic results.
        return new(manifest, facts.GroupBy(fact => fact.FactId, StringComparer.Ordinal).Select(group => group.Single())
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine).ThenBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal).ToArray(), []);

        SourceSnapshotInspection Inspect(bool capture = false)
        {
            var observed = SourceSnapshotInspector.InspectOrderedInventory(root, Inventory(),
                maxSourceFiles, maxSourceBytes, cancellationToken);
            if (observed.Digest != original.SourceSnapshotDigest) throw Fail("SOURCE_SNAPSHOT_MISMATCH");
            return observed;

            IEnumerable<FileInventoryItem> Inventory()
            {
                foreach (var item in retainedInventory())
                {
                    yield return item;
                    if (capture && PortablePdbExtractor.IsSourceChecksumCandidateKind(item.Kind)
                        && pdbInventory.Count <= pdbSourceLimit)
                        pdbInventory.Add(item);
                }
            }
        }
    }

    /// <summary>Checks local context integrity, not parent artifact authenticity or contents.</summary>
    public static void ValidateContext(ScanManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var context = manifest.CompiledAttachment ?? throw Fail("CONTEXT_MISSING");
        if (context.SchemaVersion != Schema || context.RuleId != RuleId
            || context.EvidenceTier != EvidenceTiers.Tier2Structural || context.Visibility != "local-only"
            || context.ClaimLevel != "review-only-static-not-runtime" || context.Limitation != Limitation
            || !Sha(context.GeneratorSha256) || !Sha(context.BoundedInputSha256)
            || !Sha(context.ParentManifestSha256) || !Sha(context.ParentIndexSha256)
            || !Sha(context.ParentSourceSnapshotDigest) || string.IsNullOrWhiteSpace(context.ParentScanId)
            || context.ParentSourceSnapshotDigest != manifest.SourceSnapshotDigest
            || context.SourceFiles < 1 || context.SourceFiles > context.MaxSourceFiles
            || context.SourceBytes < 0 || context.SourceBytes > context.MaxSourceBytes || context.MaxSourceBytes < 1
            || manifest.CompiledInputProvenance?.GeneratorSha256 != context.GeneratorSha256
            || manifest.BuildStatus != "NotRun" || manifest.AnalysisLevel != "CompiledStaticEvidenceReduced"
            || manifest.ScanId != "scan-compiled-" + context.BoundedInputSha256[..20]
            || context.BoundedInputSha256 != ContextDigest(manifest, context))
            throw Fail("CONTEXT_INVALID");
    }

    private static string ContextDigest(ScanManifest manifest, CompiledAttachmentContext context) =>
        ManagedMetadataExtractor.CanonicalDigest(new
        {
            schemaVersion = Schema, ruleId = RuleId, generatorSha256 = context.GeneratorSha256,
            scannerVersion = manifest.ScannerVersion, parentScanId = context.ParentScanId,
            parentManifestSha256 = context.ParentManifestSha256, parentIndexSha256 = context.ParentIndexSha256,
            parentCommitSha = manifest.CommitSha, manifest.ScanRootPathHash,
            parentSourceSnapshotDigest = context.ParentSourceSnapshotDigest,
            sourceFiles = context.SourceFiles, sourceBytes = context.SourceBytes,
            maxSourceFiles = context.MaxSourceFiles, maxSourceBytes = context.MaxSourceBytes,
            compiledInputSha256 = manifest.CompiledInputProvenance?.BoundedInputSha256,
            pdbInputSha256 = manifest.PdbInputProvenance?.BoundedInputSha256,
            ilInputSha256 = manifest.IlBodyProvenance?.BoundedInputSha256,
            publishInputSha256 = manifest.WebFormsPublishProvenance?.BoundedInputSha256
        });

    private static string Generator()
    {
        using var stream = File.OpenRead(typeof(CompiledAttachmentProducer).Assembly.Location);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    private static bool Hex(char character) => character is (>= '0' and <= '9') or (>= 'a' and <= 'f');
    private static bool Sha(string? value) => value is { Length: 64 } && value.All(Hex);
    private static InvalidOperationException Fail(string code) => new("COMPILED_ATTACHMENT_" + code);
}
