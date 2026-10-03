using System.Security.Cryptography;
using System.Text.Json;

namespace TraceMap.Reporting;

/// <summary>Bounds projection of an already admitted report, not scanner or graph construction.</summary>
public sealed record GroupedCompiledPathLimits(
    long MaxInputBytes = 256L * 1024 * 1024,
    long MaxOutputBytes = 512L * 1024 * 1024,
    int MaxPaths = 100_000,
    int MaxRecords = 500_000,
    int MaxReferences = 2_000_000);

public sealed record GroupedCompiledPathVariant(
    CombinedPath Path,
    IReadOnlyList<string> NodeReferences,
    IReadOnlyList<string> EdgeReferences);

public sealed record CompiledMethodChainGroup(string ChainId, IReadOnlyList<int> VariantIndexes);

/// <summary>Private, lossless indexed projection. Header paths/inventory evidence are represented by references.</summary>
public sealed record GroupedCompiledPathHandoff(
    string SchemaVersion,
    string RuleId,
    string EvidenceTier,
    string Visibility,
    string ClaimLevel,
    string GeneratorSha256,
    string BoundedInputSha256,
    string InputIndexSha256,
    string InputReportSha256,
    GroupedCompiledPathLimits Limits,
    CombinedDependencyPathReport Header,
    IReadOnlyDictionary<string, CombinedPathNode> Nodes,
    IReadOnlyDictionary<string, CombinedPathEdge> Edges,
    IReadOnlyList<string> InventoryNodeReferences,
    IReadOnlyList<string> InventoryEdgeReferences,
    IReadOnlyList<GroupedCompiledPathVariant> Variants,
    IReadOnlyList<CompiledMethodChainGroup> Chains,
    string Limitation);

public static class GroupedCompiledPathHandoffBuilder
{
    public const string Schema = "webforms-compiled-grouped-handoff.v1";
    public const string Rule = "workflow.webforms.compiled-grouped-handoff.v1";
    private const string Limitation = "Local review-only static evidence. Exact method/source identities are grouped, not execution routes; every evidence variant remains indexed. No runtime SQL, page activation, source-line identity or complete-site claim. Input index admission belongs to the caller. Bounds apply to this projection, not upstream graph memory.";

    /// <param name="inputIndexSha256">Exact hash of the caller's independently admitted index bytes.</param>
    public static GroupedCompiledPathHandoff Create(CombinedDependencyPathReport report,
        string inputIndexSha256, GroupedCompiledPathLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        limits ??= new();
        ValidateLimits(limits);
        if (!IsHash(inputIndexSha256)) throw Invalid("INDEX_HASH");
        cancellationToken.ThrowIfCancellationRequested();
        if (report.Paths.Count > limits.MaxPaths) throw Invalid("PATH_LIMIT");
        long references = report.Inventory.EvidenceNodes.Count + (long)report.Inventory.EvidenceEdges.Count;
        foreach (var path in report.Paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            references = checked(references + path.Nodes.Count + (long)path.Edges.Count);
            if (references > limits.MaxReferences) throw Invalid("REFERENCE_LIMIT");
        }
        if (references > limits.MaxReferences) throw Invalid("REFERENCE_LIMIT");
        var reportHash = HashJson(report, limits.MaxInputBytes, cancellationToken);
        using var generator = File.OpenRead(typeof(GroupedCompiledPathHandoffBuilder).Assembly.Location);
        var generatorHash = Convert.ToHexStringLower(SHA256.HashData(generator));
        var nodes = new SortedDictionary<string, CombinedPathNode>(StringComparer.Ordinal);
        var edges = new SortedDictionary<string, CombinedPathEdge>(StringComparer.Ordinal);
        var groups = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        var variants = new List<GroupedCompiledPathVariant>(report.Paths.Count);
        string Node(CombinedPathNode node) => Add(nodes, node, "node");
        string Edge(CombinedPathEdge edge) => Add(edges, edge, "edge");
        string Add<T>(SortedDictionary<string, T> records, T record, string kind)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Full record content, not NodeId/EdgeId alone: differing evidence stays lossless.
            var reference = kind + ":" + HashJson(record, limits.MaxInputBytes, cancellationToken);
            if (!records.ContainsKey(reference))
            {
                if (nodes.Count + (long)edges.Count >= limits.MaxRecords) throw Invalid("RECORD_LIMIT");
                records.Add(reference, record);
            }
            return reference;
        }
        var inventoryNodes = report.Inventory.EvidenceNodes.Select(Node).ToArray();
        var inventoryEdges = report.Inventory.EvidenceEdges.Select(Edge).ToArray();
        foreach (var path in report.Paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chainId = ChainId(path.Nodes, limits.MaxInputBytes, cancellationToken);
            if (!groups.TryGetValue(chainId, out var members)) groups.Add(chainId, members = []);
            members.Add(variants.Count);
            variants.Add(new(path with { Nodes = [], Edges = [] },
                path.Nodes.Select(Node).ToArray(), path.Edges.Select(Edge).ToArray()));
        }
        var header = report with
        {
            Paths = [],
            Inventory = report.Inventory with { EvidenceNodes = [], EvidenceEdges = [] }
        };
        var result = new GroupedCompiledPathHandoff(Schema, Rule, "Tier2Structural", "local-only",
            "review-only-static-evidence", generatorHash,
            InputHash(inputIndexSha256, reportHash, generatorHash, limits, cancellationToken),
            inputIndexSha256, reportHash, limits, header, nodes, edges, inventoryNodes, inventoryEdges,
            variants, groups.Select(group => new CompiledMethodChainGroup(group.Key, group.Value)).ToArray(), Limitation);
        _ = HashJson(result, limits.MaxOutputBytes, cancellationToken);
        return result;
    }

    /// <summary>Validate the indexed representation and reconstruct original order and every report field.</summary>
    public static CombinedDependencyPathReport Restore(GroupedCompiledPathHandoff handoff,
        GroupedCompiledPathLimits? admissionLimits = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        admissionLimits ??= new();
        ValidateLimits(admissionLimits);
        ValidateLimits(handoff.Limits);
        if (handoff.Limits.MaxInputBytes > admissionLimits.MaxInputBytes ||
            handoff.Limits.MaxOutputBytes > admissionLimits.MaxOutputBytes ||
            handoff.Limits.MaxPaths > admissionLimits.MaxPaths ||
            handoff.Limits.MaxRecords > admissionLimits.MaxRecords ||
            handoff.Limits.MaxReferences > admissionLimits.MaxReferences) throw Invalid("ADMISSION_LIMIT");
        if (handoff.SchemaVersion != Schema || handoff.RuleId != Rule || handoff.EvidenceTier != "Tier2Structural" ||
            handoff.Visibility != "local-only" || handoff.ClaimLevel != "review-only-static-evidence" ||
            handoff.Limitation != Limitation || !IsHash(handoff.GeneratorSha256) ||
            !IsHash(handoff.InputIndexSha256) || !IsHash(handoff.InputReportSha256) ||
            handoff.BoundedInputSha256 != InputHash(handoff.InputIndexSha256, handoff.InputReportSha256,
                handoff.GeneratorSha256, handoff.Limits, cancellationToken)) throw Invalid("CONTEXT");
        if (handoff.Header.Paths.Count != 0 || handoff.Header.Inventory.EvidenceNodes.Count != 0 ||
            handoff.Header.Inventory.EvidenceEdges.Count != 0 || handoff.Variants.Count > handoff.Limits.MaxPaths ||
            handoff.Nodes.Count + (long)handoff.Edges.Count > handoff.Limits.MaxRecords) throw Invalid("SHAPE");
        long references = handoff.InventoryNodeReferences.Count + (long)handoff.InventoryEdgeReferences.Count;
        foreach (var variant in handoff.Variants)
        {
            references = checked(references + variant.NodeReferences.Count + (long)variant.EdgeReferences.Count);
            if (references > handoff.Limits.MaxReferences || variant.Path.Nodes.Count != 0 || variant.Path.Edges.Count != 0)
                throw Invalid("REFERENCE_LIMIT");
        }
        if (references > handoff.Limits.MaxReferences) throw Invalid("REFERENCE_LIMIT");
        _ = HashJson(handoff, handoff.Limits.MaxOutputBytes, cancellationToken);
        var usedNodes = new HashSet<string>(StringComparer.Ordinal);
        var usedEdges = new HashSet<string>(StringComparer.Ordinal);
        T Resolve<T>(string reference, IReadOnlyDictionary<string, T> records, HashSet<string> used, string kind)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!records.TryGetValue(reference, out var value)) throw Invalid("RECORD_HASH");
            if (!used.Contains(reference) &&
                reference != kind + ":" + HashJson(value, handoff.Limits.MaxInputBytes, cancellationToken))
                throw Invalid("RECORD_HASH");
            used.Add(reference);
            return value;
        }
        CombinedPathNode Node(string reference) => Resolve(reference, handoff.Nodes, usedNodes, "node");
        CombinedPathEdge Edge(string reference) => Resolve(reference, handoff.Edges, usedEdges, "edge");
        var paths = handoff.Variants.Select(variant => variant.Path with
        {
            Nodes = variant.NodeReferences.Select(Node).ToArray(),
            Edges = variant.EdgeReferences.Select(Edge).ToArray()
        }).ToArray();
        var report = handoff.Header with
        {
            Paths = paths,
            Inventory = handoff.Header.Inventory with
            {
                EvidenceNodes = handoff.InventoryNodeReferences.Select(Node).ToArray(),
                EvidenceEdges = handoff.InventoryEdgeReferences.Select(Edge).ToArray()
            }
        };
        if (usedNodes.Count != handoff.Nodes.Count || usedEdges.Count != handoff.Edges.Count ||
            HashJson(report, handoff.Limits.MaxInputBytes, cancellationToken) != handoff.InputReportSha256)
            throw Invalid("REPORT_HASH");
        var expectedGroups = paths.Select((path, index) => (Chain: ChainId(path.Nodes, handoff.Limits.MaxInputBytes, cancellationToken), Index: index))
            .GroupBy(item => item.Chain, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CompiledMethodChainGroup(group.Key, group.Select(item => item.Index).ToArray())).ToArray();
        if (HashJson(expectedGroups, handoff.Limits.MaxOutputBytes, cancellationToken) !=
            HashJson(handoff.Chains, handoff.Limits.MaxOutputBytes, cancellationToken)) throw Invalid("CHAIN_INDEX");
        return report;
    }

    private static string ChainId(IReadOnlyList<CombinedPathNode> nodes, long bytes, CancellationToken cancellationToken)
        => "chain:" + HashJson(nodes.Select(node => new
        {
            node.NodeKind, node.SourceIndexId, node.ScanId, node.CommitSha,
            Identity = node.SymbolId ?? node.NodeId, node.DisplayName
        }).ToArray(), bytes, cancellationToken);

    private static string InputHash(string index, string report, string generator, GroupedCompiledPathLimits limits,
        CancellationToken cancellationToken) => HashJson(new { Schema, Rule, Index = index, Report = report,
            Generator = generator, Limits = limits }, 4096, cancellationToken);

    internal static string HashJson<T>(T value, long maximumBytes, CancellationToken cancellationToken)
    {
        using var stream = new HashLimitStream(maximumBytes, cancellationToken);
        JsonSerializer.Serialize(stream, value);
        return stream.Digest();
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void ValidateLimits(GroupedCompiledPathLimits limits)
    {
        if (limits.MaxInputBytes <= 0 || limits.MaxOutputBytes <= 0 || limits.MaxPaths <= 0 ||
            limits.MaxRecords <= 0 || limits.MaxReferences <= 0) throw Invalid("LIMITS");
    }
    private static InvalidDataException Invalid(string category) => new("WEBFORMS_GROUPED_HANDOFF_" + category);

    private sealed class HashLimitStream(long maximumBytes, CancellationToken cancellationToken) : Stream
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long bytes;
        public string Digest() => Convert.ToHexStringLower(hash.GetHashAndReset());
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (buffer.Length > maximumBytes - bytes) throw Invalid("BYTE_LIMIT");
            hash.AppendData(buffer); bytes += buffer.Length;
        }
        protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => bytes;
        public override long Position { get => bytes; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
