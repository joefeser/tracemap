using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

internal static class CompiledAttachmentLinkReader
{
    private static readonly JsonSerializerOptions LinkJson = new()
    { MaxDepth = 32, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    internal static async Task<IReadOnlyList<CompiledAttachmentIndexLink>> ReadAsync(SqliteConnection connection,
        IReadOnlyList<CombinedSourceReadRow> sources, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = 'compiled_attachment_links';";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(token)) == 0) return [];
        command.CommandText = "select count(*), coalesce(max(length(cast(payload_json as blob))), 0) from compiled_attachment_links;";
        await using (var size = await command.ExecuteReaderAsync(token))
        {
            await size.ReadAsync(token);
            if (size.GetInt64(0) > 128 || size.GetInt64(1) > 32768) throw Fail("INPUT_LIMIT");
        }
        var byId = sources.ToDictionary(row => row.Source.SourceIndexId, StringComparer.Ordinal);
        var result = new List<CompiledAttachmentIndexLink>(); var children = new HashSet<string>(StringComparer.Ordinal);
        command.CommandText = "select attachment_source_index_id, parent_source_index_id, case when length(cast(payload_json as blob)) <= 32768 then payload_json else null end from compiled_attachment_links order by attachment_source_index_id;";
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            if (reader.IsDBNull(2) || result.Count >= 128) throw Fail("INPUT_LIMIT");
            var json = reader.GetString(2); using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            RejectDuplicates(document.RootElement);
            var link = JsonSerializer.Deserialize<CompiledAttachmentIndexLink>(json, LinkJson) ?? throw Fail("INVALID");
            if (string.IsNullOrEmpty(link.ParentEmbeddedManifestSha256) || string.IsNullOrEmpty(link.AttachmentEmbeddedManifestSha256))
                throw Fail("RECOMBINE_REQUIRED");
            CompiledAttachmentIndexLink.Validate(link);
            if (link.AttachmentSourceIndexId != reader.GetString(0) || link.ParentSourceIndexId != reader.GetString(1)
                || !children.Add(link.AttachmentSourceIndexId)
                || !byId.TryGetValue(link.ParentSourceIndexId, out var parentRow)
                || !byId.TryGetValue(link.AttachmentSourceIndexId, out var childRow)) throw Fail("SOURCE_MISMATCH");
            if (Hash(parentRow.ManifestJson) != link.ParentEmbeddedManifestSha256
                || Hash(childRow.ManifestJson) != link.AttachmentEmbeddedManifestSha256) throw Fail("MANIFEST_CHANGED");
            var parent = Manifest(parentRow); var child = Manifest(childRow);
            CompiledAttachmentProducer.ValidateContext(child); var context = child.CompiledAttachment!;
            if (parent.CompiledAttachment is not null || parent.ScanId != link.ParentScanId || child.ScanId != link.AttachmentScanId
                || context.ParentScanId != parent.ScanId || context.ParentManifestSha256 != link.ParentManifestSha256
                || context.ParentIndexSha256 != link.ParentIndexSha256 || context.BoundedInputSha256 != link.AttachmentContextSha256
                || context.ParentSourceSnapshotDigest != link.SourceSnapshotDigest || parent.SourceSnapshotDigest != link.SourceSnapshotDigest
                || parent.CommitSha != child.CommitSha || parent.RepoName != child.RepoName || parent.RemoteUrl != child.RemoteUrl
                || parent.GitRootHash != child.GitRootHash || parent.ScanRootPathHash != child.ScanRootPathHash
                || parent.ScanRootRelativePath != child.ScanRootRelativePath) throw Fail("PARENT_CONTEXT_MISMATCH");
            result.Add(link);
        }
        return result;
    }

    private static ScanManifest Manifest(CombinedSourceReadRow row)
    {
        using var document = JsonDocument.Parse(row.ManifestJson, new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicates(document.RootElement);
        var manifest = JsonSerializer.Deserialize<ScanManifest>(row.ManifestJson, CombinedDependencyReporter.JsonOptions) ?? throw Fail("MANIFEST_INVALID");
        var source = row.Source;
        if (source.ScanId != manifest.ScanId || source.CommitSha != manifest.CommitSha || source.RepoName != manifest.RepoName
            || source.RemoteUrl != manifest.RemoteUrl || source.ScanRootPathHash != manifest.ScanRootPathHash
            || source.GitRootHash != manifest.GitRootHash || source.ScanRootRelativePath != manifest.ScanRootRelativePath
            || source.AnalysisLevel != manifest.AnalysisLevel || source.BuildStatus != manifest.BuildStatus
            || source.ScannerVersion != manifest.ScannerVersion
            || source.SourceIndexId != FactFactory.Hash($"{source.Label}|{manifest.ScanId}|{manifest.CommitSha}", 24))
            throw Fail("SOURCE_MISMATCH");
        return manifest;
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw Fail("DUPLICATE_PROPERTY"); RejectDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) RejectDuplicates(child);
    }
    private static InvalidDataException Fail(string code) => new("COMPILED_ATTACHMENT_REPORT_LINK_" + code);
}
