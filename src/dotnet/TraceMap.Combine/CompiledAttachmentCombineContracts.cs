using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Combine;

internal sealed class CompiledAttachmentCombineContracts
{
    private sealed record Pinned(string Path, long Bytes, string Sha256);
    private sealed record Contract(Pinned ParentIndex, Pinned ParentManifest, Pinned AttachmentIndex,
        Pinned AttachmentManifest, ScanManifest Parent, ScanManifest Attachment);
    private readonly List<Contract> contracts = [];
    private readonly Dictionary<string, Pinned> pins = new(StringComparer.OrdinalIgnoreCase);
    private string generator = "";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, MaxDepth = 32 };

    internal static async Task<CompiledAttachmentCombineContracts?> AdmitAsync(CombineOptions options, CancellationToken token)
    {
        if (options.CompiledAttachments.Count == 0) return null;
        if (options.CompiledAttachments.Count > 128 || options.IndexPaths.Count > 256
            || options.MaxAttachmentIndexBytes < 1 || options.MaxAttachmentHashBytes < 1)
            throw Fail("LIMIT_INVALID");
        if (File.Exists(options.OutputPath) || Directory.Exists(options.OutputPath)) throw Fail("OUTPUT_ALREADY_EXISTS");
        var result = new CompiledAttachmentCombineContracts { generator = Generator() };
        var inputs = options.IndexPaths.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedChildren = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in options.CompiledAttachments)
        {
            token.ThrowIfCancellationRequested();
            var parentIndex = Path.GetFullPath(item.ParentIndexPath); var childIndex = Path.GetFullPath(item.AttachmentIndexPath);
            if (parentIndex.Equals(childIndex, StringComparison.OrdinalIgnoreCase) || !inputs.Contains(parentIndex)
                || !inputs.Contains(childIndex) || !usedChildren.Add(childIndex)) throw Fail("INPUT_SET_INVALID");
            var pi = await Pin(parentIndex, options.MaxAttachmentIndexBytes);
            var pm = await Pin(item.ParentManifestPath, 4_194_304);
            var ci = await Pin(childIndex, options.MaxAttachmentIndexBytes);
            var cm = await Pin(item.AttachmentManifestPath, 4_194_304);
            var parent = await ReadManifest(pm, pi, token); var child = await ReadManifest(cm, ci, token);
            CompiledAttachmentProducer.ValidateContext(child);
            var context = child.CompiledAttachment!;
            if (parent.CompiledAttachment is not null || context.ParentScanId != parent.ScanId
                || context.ParentManifestSha256 != pm.Sha256 || context.ParentIndexSha256 != pi.Sha256
                || context.ParentSourceSnapshotDigest != parent.SourceSnapshotDigest
                || child.CommitSha != parent.CommitSha || child.RepoName != parent.RepoName
                || child.RemoteUrl != parent.RemoteUrl || child.GitRootHash != parent.GitRootHash
                || child.ScanRootPathHash != parent.ScanRootPathHash || child.ScanRootRelativePath != parent.ScanRootRelativePath)
                throw Fail("PARENT_CONTEXT_MISMATCH");
            result.contracts.Add(new(pi, pm, ci, cm, parent, child));
        }
        await result.RecheckAsync(token);
        return result;

        async Task<Pinned> Pin(string path, long limit)
        {
            path = Path.GetFullPath(path);
            if (result.pins.TryGetValue(path, out var prior))
            { if (prior.Bytes > limit) throw Fail("INPUT_LIMIT"); return prior; }
            RejectLinksAndSidecars(path);
            var bytes = new FileInfo(path).Length;
            if (bytes < 1 || bytes > limit || bytes > options.MaxAttachmentHashBytes - result.pins.Values.Sum(value => value.Bytes))
                throw Fail("INPUT_LIMIT");
            var value = new Pinned(path, bytes, await HashAsync(path, bytes, token)); result.pins.Add(path, value); return value;
        }
    }

    internal async Task RecheckAsync(CancellationToken token)
    {
        foreach (var pin in pins.Values)
        {
            RejectLinksAndSidecars(pin.Path);
            if (new FileInfo(pin.Path).Length != pin.Bytes || await HashAsync(pin.Path, pin.Bytes, token) != pin.Sha256)
                throw Fail("INPUT_CHANGED");
            RejectLinksAndSidecars(pin.Path);
        }
        if (Generator() != generator) throw Fail("GENERATOR_CHANGED");
    }

    internal async Task WriteAsync(SqliteConnection connection, IReadOnlyList<CombinedIndexSource> sources, CancellationToken token)
    {
        await RecheckAsync(token);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "create table compiled_attachment_links (attachment_source_index_id text primary key, parent_source_index_id text not null, payload_json text not null);";
        await command.ExecuteNonQueryAsync(token);
        foreach (var contract in contracts.OrderBy(item => item.Attachment.ScanId, StringComparer.Ordinal))
        {
            var parent = sources.Single(item => item.IndexPath == contract.ParentIndex.Path);
            var child = sources.Single(item => item.IndexPath == contract.AttachmentIndex.Path);
            var context = contract.Attachment.CompiledAttachment!;
            var link = new CompiledAttachmentIndexLink(CompiledAttachmentIndexLink.Schema, CompiledAttachmentIndexLink.Rule,
                EvidenceTiers.Tier2Structural, "local-only-review-only", generator, "", parent.SourceIndexId, child.SourceIndexId,
                parent.ScanId, child.ScanId, contract.ParentManifest.Sha256, contract.ParentIndex.Sha256,
                contract.AttachmentManifest.Sha256, contract.AttachmentIndex.Sha256, context.ParentSourceSnapshotDigest,
                context.BoundedInputSha256);
            link = link with { BoundedInputSha256 = CompiledAttachmentIndexLink.InputDigest(link) };
            CompiledAttachmentIndexLink.Validate(link);
            command.CommandText = "insert into compiled_attachment_links values ($child, $parent, $json);";
            command.Parameters.Clear(); command.Parameters.AddWithValue("$child", child.SourceIndexId);
            command.Parameters.AddWithValue("$parent", parent.SourceIndexId);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(link));
            await command.ExecuteNonQueryAsync(token);
        }
        await RecheckAsync(token);
        await transaction.CommitAsync(token);
    }

    private static async Task<ScanManifest> ReadManifest(Pinned manifest, Pinned index, CancellationToken token)
    {
        var bytes = new byte[checked((int)manifest.Bytes)];
        await using (var stream = File.OpenRead(manifest.Path))
        {
            if (stream.Length != manifest.Bytes) throw Fail("INPUT_CHANGED");
            await stream.ReadExactlyAsync(bytes, token);
            if (stream.ReadByte() != -1 || Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != manifest.Sha256)
                throw Fail("INPUT_CHANGED");
        }
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicates(document.RootElement);
        var value = JsonSerializer.Deserialize<ScanManifest>(bytes, Json) ?? throw Fail("MANIFEST_INVALID");
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = ImmutableUri(index.Path), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = "select case when count(*) = 1 and max(length(cast(manifest_json as blob))) <= 4194304 then 1 else 0 end from scan_manifest;";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(token)) != 1) throw Fail("INDEX_MANIFEST_LIMIT_OR_CARDINALITY");
        command.CommandText = "select case when length(cast(manifest_json as blob)) <= 4194304 then manifest_json else null end from scan_manifest;";
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token) || reader.IsDBNull(0)) throw Fail("INDEX_MANIFEST_UNAVAILABLE");
        using var embedded = JsonDocument.Parse(reader.GetString(0), new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicates(embedded.RootElement);
        if (!JsonElement.DeepEquals(document.RootElement, embedded.RootElement) || await reader.ReadAsync(token))
            throw Fail("INDEX_MANIFEST_MISMATCH");
        return value;
    }

    internal static string ImmutableUri(string path) => new Uri(Path.GetFullPath(path)).AbsoluteUri + "?mode=ro&immutable=1";
    private static async Task<string> HashAsync(string path, long expectedBytes, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        if (stream.Length != expectedBytes) throw Fail("INPUT_CHANGED");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536]; long remaining = expectedBytes;
        while (remaining > 0)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), token);
            if (count == 0) throw Fail("INPUT_CHANGED"); hash.AppendData(buffer, 0, count); remaining -= count;
        }
        if (stream.ReadByte() != -1 || stream.Length != expectedBytes) throw Fail("INPUT_CHANGED");
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    private static string Generator()
    {
        using var stream = File.OpenRead(typeof(CompiledAttachmentCombineContracts).Assembly.Location);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    private static void RejectLinksAndSidecars(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).LinkTarget is not null) throw Fail("INPUT_UNAVAILABLE_OR_LINKED");
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
            if (directory.LinkTarget is not null) throw Fail("INPUT_UNAVAILABLE_OR_LINKED");
        if (new[] { "-wal", "-shm", "-journal" }.Any(suffix => File.Exists(path + suffix) || Directory.Exists(path + suffix)))
            throw Fail("INPUT_SIDECAR");
    }
    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw Fail("MANIFEST_DUPLICATE_PROPERTY"); RejectDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) RejectDuplicates(child);
    }
    private static InvalidDataException Fail(string code) => new("COMPILED_ATTACHMENT_COMBINE_" + code);
}
