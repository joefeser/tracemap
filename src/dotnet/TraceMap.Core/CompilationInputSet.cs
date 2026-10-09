using System.Security.Cryptography;
using Microsoft.CodeAnalysis.Text;

namespace TraceMap.Core;

// Checksums belong to the immutable Roslyn documents used for binding, not a later disk observation.
internal sealed class CompilationInputSet() : HashSet<string>(StringComparer.Ordinal)
{
    internal Dictionary<string, (SourceHashAlgorithm Algorithm, string Digest)> Checksums { get; } = new(StringComparer.Ordinal);

    internal void Record(string path, SourceText text)
    {
        var checksum = (text.ChecksumAlgorithm, Convert.ToHexString(text.GetChecksum().AsSpan()));
        if (Checksums.TryGetValue(path, out var prior) && prior != checksum)
            throw new SourceSnapshotException(details: ["compilation input changed between project evaluations"]);
        Checksums[path] = checksum;
        Add(path);
    }

    internal void Verify(string path, string fullPath)
    {
        if (!Checksums.TryGetValue(path, out var expected))
            throw new SourceSnapshotException(details: ["compilation input lacks an immutable compiler checksum"]);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var algorithm = expected.Algorithm switch
        {
            SourceHashAlgorithm.Sha1 => HashAlgorithmName.SHA1,
            SourceHashAlgorithm.Sha256 => HashAlgorithmName.SHA256,
            _ => throw new SourceSnapshotException(details: ["unsupported compiler checksum algorithm"])
        };
        using var hash = IncrementalHash.CreateHash(algorithm);
        var remaining = stream.Length;
        Span<byte> buffer = stackalloc byte[8192];
        while (remaining > 0)
        {
            var count = stream.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
            if (count == 0) throw new SourceSnapshotException(details: ["compilation input truncated during verification"]);
            hash.AppendData(buffer[..count]);
            remaining -= count;
        }
        if (stream.ReadByte() != -1) throw new SourceSnapshotException(details: ["compilation input grew during verification"]);
        var actual = hash.GetHashAndReset();
        if (Convert.ToHexString(actual) != expected.Digest)
            throw new SourceSnapshotException(details: ["compilation input differs from immutable compiler document"]);
    }
}
