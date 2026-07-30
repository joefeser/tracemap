using System.Text.Json;

namespace TraceMap.Access;

public sealed record AccessDesignEvidenceBinding(
    string RepositoryIdentityHash,
    string CommitSha,
    string BaseScanManifestSha256,
    string DatabaseIdentityHash,
    string DatabaseSha256);

public sealed record AccessDesignEvidenceManifestProjection(
    string ProducerId,
    string ProducerVersion,
    string Mechanism,
    string BundleSha256,
    string CopyBinding,
    IReadOnlyDictionary<string, int> CountsByKind)
{
    public required string RepositoryIdentityHash { get; init; }
    public required string CommitSha { get; init; }
    public required string BaseScanManifestSha256 { get; init; }
    public required string DatabaseIdentityHash { get; init; }
    public required string SourceCopySha256 { get; init; }
}

public sealed record AccessDesignEvidenceGap(
    string Classification,
    string ScopeKind,
    string? CanonicalRecordId = null);

public sealed record AccessDesignEvidenceSource(
    string DocumentRole,
    string CoordinateStatus,
    string? DocumentSha256,
    int? StartLine,
    int? EndLine);

/// <summary>
/// A validated design record. <see cref="Payload"/> remains protected caller
/// material and is valid only until its owning bundle is disposed.
/// </summary>
public sealed record AccessDesignEvidenceRecord(
    string Kind,
    string CanonicalRecordId,
    string? ParentCanonicalRecordId,
    AccessDesignEvidenceSource Source,
    string Completeness,
    JsonElement Payload);

/// <summary>
/// Owns the protected JSON documents backing validated records. Dispose the
/// bundle immediately after in-process projection; do not serialize it.
/// </summary>
public sealed class AccessDesignEvidenceBundle : IDisposable
{
    private readonly JsonDocument[] _documents;

    internal AccessDesignEvidenceBundle(
        bool acceptedForProjection,
        AccessDesignEvidenceManifestProjection manifest,
        IReadOnlyList<AccessDesignEvidenceRecord> records,
        IReadOnlyList<AccessDesignEvidenceGap> gaps,
        JsonDocument[] documents)
    {
        AcceptedForProjection = acceptedForProjection;
        Manifest = manifest;
        Records = records;
        Gaps = gaps;
        _documents = documents;
    }

    public bool AcceptedForProjection { get; }
    public AccessDesignEvidenceManifestProjection Manifest { get; }
    public IReadOnlyList<AccessDesignEvidenceRecord> Records { get; }
    public IReadOnlyList<AccessDesignEvidenceGap> Gaps { get; }

    public void Dispose()
    {
        foreach (var document in _documents)
            document.Dispose();
    }
}
