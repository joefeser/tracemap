using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

// A set admits all declared partitions or none. Legacy per-receipt limits remain
// unchanged, and global inventories retain cross-partition ambiguity checks.
internal static partial class WebFormsPublishMapExtractor
{
    internal const string ReceiptSetSchema = "webforms-publish-binding-set.v1";
    internal const string InventoryPartitionSchema = "webforms-publish-inventory-partition.v1";
    internal const int MaxReceiptPartitions = 64;
    internal const long MaxReceiptSetArtifactHashBytes = 8L * 1024 * 1024 * 1024;
    private static readonly JsonSerializerOptions SetJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };

    internal sealed record ReceiptPartition(string Path, string Sha256);
    internal sealed record PublishReceiptSet(string SchemaVersion, string RuleId, string Visibility,
        string ClaimLevel, string ReceiptGeneratorSha256, string SourceCommitSha, string BoundedInputSha256,
        IReadOnlyList<ReceiptPartition> Partitions);

    internal static string ReceiptSetInputDigest(string generatorSha256, string commitSha,
        IEnumerable<ReceiptPartition> partitions) => Sha256(Encoding.UTF8.GetBytes(
            $"{ReceiptSetSchema}\ngenerator:{generatorSha256}\ncommit:{commitSha}\n" +
            string.Join("\n", partitions.OrderBy(item => item.Path, StringComparer.Ordinal)
                .Select(item => item.Path + ":" + item.Sha256)) + "\n"));

    private static WebFormsPublishEvaluation EvaluateSet(string repoPath, string commitSha, ScanOptions options,
        CancellationToken token, string setPath, string publishRoot, byte[] setBytes,
        string generatorSha256, string inputSha256, string? publishedRootHash)
    {
        using var document = JsonDocument.Parse(setBytes, new JsonDocumentOptions { MaxDepth = 16 });
        RejectDuplicateKeys(document.RootElement);
        var set = JsonSerializer.Deserialize<PublishReceiptSet>(setBytes, SetJson)
            ?? throw new PublishException("WebFormsPublishReceiptSetInvalid");
        if (set.SchemaVersion != ReceiptSetSchema || set.RuleId != RuleIds.LegacyWebFormsPublishMap
            || set.Visibility != "local-only" || set.ClaimLevel != "operator-declared-review-only-not-build-proof"
            || set.SourceCommitSha != commitSha || !IsSha256(set.ReceiptGeneratorSha256)
            || !IsSha256(set.BoundedInputSha256) || set.Partitions is null
            || set.Partitions.Count is < 1 or > MaxReceiptPartitions
            || set.Partitions.Any(item => item is null || !IsSha256(item.Sha256))
            || HasDuplicatePaths(set.Partitions.Select(item => item.Path))
            || set.BoundedInputSha256 != ReceiptSetInputDigest(set.ReceiptGeneratorSha256, commitSha, set.Partitions))
            throw new PublishException("WebFormsPublishReceiptSetInvalid");
        var receiptsRoot = Path.GetDirectoryName(setPath)!;
        if (File.GetAttributes(receiptsRoot).HasFlag(FileAttributes.ReparsePoint))
            throw new PublishException("WebFormsPublishUnsafePath");
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var published = new Dictionary<string, (string Sha256, string Kind)>(StringComparer.OrdinalIgnoreCase);
        var canonicalSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var canonicalPublished = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pageVirtualPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allPages = new List<Page>();
        var mapped = new List<WebFormsPublishPage>();
        var candidates = new List<WebFormsPublishPageCandidate>();
        var actualReceipts = new List<(string Path, string Sha256)>();
        long artifactHashBytes = 0;
        foreach (var partition in set.Partitions.OrderBy(item => item.Path, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            var path = ResolveChild(receiptsRoot, partition.Path);
            var bytes = ReadBounded(path, MaxReceiptBytes);
            if (Sha256(bytes) != partition.Sha256) throw new PublishException("WebFormsPublishPartitionMismatch");
            using var partDocument = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicateKeys(partDocument.RootElement);
            var receipt = JsonSerializer.Deserialize<PublishReceipt>(bytes,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 16 });
            var inventoryPartition = receipt?.SchemaVersion == InventoryPartitionSchema;
            if (receipt is null || (receipt.SchemaVersion != "webforms-publish-binding.v1" && !inventoryPartition) || receipt.SourceFiles is null
                || receipt.PublishedFiles is null || receipt.Pages is null
                || receipt.SourceFiles.Count is < 1 or > MaxSourceFiles
                || receipt.PublishedFiles.Count is < 1 or > MaxPublishedFiles
                || (inventoryPartition ? receipt.Pages.Count != 0 : receipt.Pages.Count is < 1 or > MaxPages)
                || receipt.SourceFiles.Any(item => item is null) || receipt.PublishedFiles.Any(item => item is null)
                || receipt.Pages.Any(item => item is null))
                throw new PublishException("WebFormsPublishReceiptSetInvalid");
            foreach (var source in receipt.SourceFiles) AdmitHashWork(ResolveChild(repoPath, source.Path), MaxArtifactBytes);
            foreach (var file in receipt.PublishedFiles)
                AdmitHashWork(ResolveChild(publishRoot, file.Path), file.Kind == "compiled-map" ? MaxMapBytes : MaxArtifactBytes,
                    file.Kind == "compiled-map" ? receipt.Pages.Count + 2 : 1);
            // Never recurse into a nested set or accept partially bound partitions.
            var evaluated = EvaluateReceipt(repoPath, commitSha, options with
            {
                WebFormsPublishReceiptPath = path, WebFormsPublishedRootPath = publishRoot
            }, token, allowPartitionSet: false, allowInventoryPartition: true);
            if (evaluated.Provenance?.Status != "bound")
                throw new PublishException(evaluated.Provenance?.GapKinds.FirstOrDefault() ?? "WebFormsPublishPartitionInvalid");
            foreach (var source in receipt.SourceFiles!)
            {
                if (sources.TryGetValue(source.Path!, out var prior)
                    && (prior != source.Sha256 || canonicalSources[source.Path!] != source.Path))
                    throw new PublishException("WebFormsPublishReceiptSetAmbiguous");
                sources.TryAdd(source.Path!, source.Sha256!);
                canonicalSources.TryAdd(source.Path!, source.Path!);
            }
            foreach (var file in receipt.PublishedFiles!)
            {
                if (published.TryGetValue(file.Path!, out var prior)
                    && (prior != (file.Sha256!, file.Kind!) || canonicalPublished[file.Path!] != file.Path))
                    throw new PublishException("WebFormsPublishReceiptSetAmbiguous");
                published.TryAdd(file.Path!, (file.Sha256!, file.Kind!));
                canonicalPublished.TryAdd(file.Path!, file.Path!);
            }
            foreach (var page in receipt.Pages!)
            {
                var sourcePath = page.SourcePath ?? page.VirtualPath!.TrimStart('/');
                if (!pageNames.Add(sourcePath) || !pageVirtualPaths.Add(page.VirtualPath!))
                    throw new PublishException("WebFormsPublishReceiptSetAmbiguous");
                allPages.Add(page);
            }
            mapped.AddRange(evaluated.Pages);
            candidates.AddRange(evaluated.Candidates ?? []);
            actualReceipts.Add((path, partition.Sha256));
        }
        if (allPages.Count == 0) throw new PublishException("WebFormsPublishReceiptSetPagesUnavailable");
        // A mapless declaration in one partition must not conceal a matching map
        // retained in another. Mapped pages likewise require one global map.
        var maps = new List<(string Path, string VirtualPath)>();
        foreach (var file in published.Where(item => item.Value.Kind == "compiled-map").OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            var path = ResolveChild(publishRoot, file.Key);
            AdmitHashWork(path, MaxMapBytes);
            var bytes = ReadBounded(path, MaxMapBytes);
            if (Sha256(bytes) != file.Value.Sha256) throw new PublishException("WebFormsPublishArtifactMismatch");
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxMapBytes
            });
            var root = XDocument.Load(reader).Root;
            var virtualPath = root?.Attribute("virtualPath")?.Value;
            if (root?.Name.LocalName != "preserve" || string.IsNullOrWhiteSpace(virtualPath)
                || virtualPath.Length > 4096)
                throw new PublishException("WebFormsPublishMapMismatch");
            maps.Add((file.Key, virtualPath));
        }
        foreach (var page in allPages)
        {
            token.ThrowIfCancellationRequested();
            var sourcePath = page.SourcePath ?? page.VirtualPath!.TrimStart('/');
            var matches = maps.Where(map => map.VirtualPath.Equals(page.VirtualPath, StringComparison.OrdinalIgnoreCase)
                || map.VirtualPath.EndsWith("/" + sourcePath, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (page.BindingKind == "mapless-source-type-candidate" ? matches.Length != 0
                : matches.Length != 1 || !StringComparer.Ordinal.Equals(matches[0].Path, page.MapPath))
                throw new PublishException("WebFormsPublishReceiptSetMapAmbiguous");
        }
        foreach (var (path, hash) in actualReceipts)
        {
            token.ThrowIfCancellationRequested();
            if (Sha256(ReadBounded(path, MaxReceiptBytes)) != hash) throw new PublishException("WebFormsPublishPartitionMismatch");
        }
        if (Sha256(ReadBounded(setPath, MaxReceiptBytes)) != Sha256(setBytes))
            throw new PublishException("WebFormsPublishPartitionMismatch");
        foreach (var source in sources)
            Recheck(ResolveChild(repoPath, source.Key), source.Value, MaxArtifactBytes);
        foreach (var file in published)
            Recheck(ResolveChild(publishRoot, file.Key), file.Value.Sha256,
                file.Value.Kind == "compiled-map" ? MaxMapBytes : MaxArtifactBytes);
        var provenance = new WebFormsPublishProvenance("webforms-publish-provenance.v1", generatorSha256, inputSha256,
            "bound", [], sources.Count, published.Count, allPages.Count, publishedRootHash);
        return new(provenance, mapped.OrderBy(page => page.SourcePath, StringComparer.Ordinal).ToArray(),
            sources.Keys.Order(StringComparer.Ordinal).ToArray(),
            published.Where(item => item.Value.Kind == "assembly").OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new WebFormsPublishAssembly(item.Key, item.Value.Sha256)).ToArray(),
            candidates.OrderBy(page => page.SourcePath, StringComparer.Ordinal).ToArray());

        void AdmitHashWork(string path, long limit, int reads = 1)
        {
            token.ThrowIfCancellationRequested();
            var length = new FileInfo(path).Length;
            if (length > limit || length > (MaxReceiptSetArtifactHashBytes - artifactHashBytes) / reads)
                throw new PublishException("WebFormsPublishReceiptSetHashLimitExceeded");
            artifactHashBytes += length * reads;
        }
        void Recheck(string path, string expected, long limit)
        {
            AdmitHashWork(path, limit);
            using var stream = File.OpenRead(path);
            if (stream.Length > limit || Sha256(stream) != expected)
                throw new PublishException("WebFormsPublishPartitionMismatch");
        }
    }

    private static void RejectDuplicateKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new PublishException("WebFormsPublishReceiptSetAmbiguous");
                RejectDuplicateKeys(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateKeys(item);
    }
}
