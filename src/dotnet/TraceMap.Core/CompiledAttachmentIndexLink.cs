using System.Security.Cryptography;
using System.Text.Json;

namespace TraceMap.Core;

/// <summary>Local combine-time parent admission, never runtime or authenticity proof.</summary>
public sealed record CompiledAttachmentIndexLink(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility,
    string GeneratorSha256, string BoundedInputSha256,
    string ParentSourceIndexId, string AttachmentSourceIndexId,
    string ParentScanId, string AttachmentScanId,
    string ParentManifestSha256, string ParentIndexSha256,
    string AttachmentManifestSha256, string AttachmentIndexSha256,
    string SourceSnapshotDigest, string AttachmentContextSha256)
{
    public const string Schema = "compiled-attachment-index-link.v1";
    public const string Rule = "workflow.compiled-attachment-index-link.v1";
    public string ParentEmbeddedManifestSha256 { get; init; } = "";
    public string AttachmentEmbeddedManifestSha256 { get; init; } = "";

    public static string InputDigest(CompiledAttachmentIndexLink link) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(link with { BoundedInputSha256 = "" })))
            .ToLowerInvariant();

    public static void Validate(CompiledAttachmentIndexLink link)
    {
        if (link.SchemaVersion != Schema || link.RuleId != Rule || link.EvidenceTier != EvidenceTiers.Tier2Structural
            || link.Visibility != "local-only-review-only" || link.ParentSourceIndexId == link.AttachmentSourceIndexId
            || !Hex(link.ParentSourceIndexId, 24) || !Hex(link.AttachmentSourceIndexId, 24)
            || string.IsNullOrWhiteSpace(link.ParentScanId) || string.IsNullOrWhiteSpace(link.AttachmentScanId)
            || new[] { link.GeneratorSha256, link.BoundedInputSha256, link.ParentManifestSha256, link.ParentIndexSha256,
                link.AttachmentManifestSha256, link.AttachmentIndexSha256, link.SourceSnapshotDigest, link.AttachmentContextSha256,
                link.ParentEmbeddedManifestSha256, link.AttachmentEmbeddedManifestSha256 }
                .Any(value => !Hex(value, 64)) || link.BoundedInputSha256 != InputDigest(link))
            throw new InvalidDataException("COMPILED_ATTACHMENT_INDEX_LINK_INVALID");
    }

    private static bool Hex(string? value, int length) => value?.Length == length
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
