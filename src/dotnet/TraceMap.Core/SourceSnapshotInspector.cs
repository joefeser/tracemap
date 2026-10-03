namespace TraceMap.Core;

public sealed record SourceSnapshotInspection(string Digest, long FileCount, long Bytes);

/// <summary>
/// Recomputes the scanner's exact byte snapshot over an explicitly retained,
/// ordinally ordered inventory. Does not discover files or run extractors.
/// </summary>
public static class SourceSnapshotInspector
{
    public static SourceSnapshotInspection InspectOrderedInventory(
        string repoPath, IEnumerable<FileInventoryItem> inventory,
        long maxFiles, long maxBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        cancellationToken.ThrowIfCancellationRequested();
        var root = Path.GetFullPath(repoPath);
        long files = 0, bytes = 0;
        var digest = ScanEngine.CreateOrderedSourceSnapshotDigest(root, CheckedInventory(), cancellationToken);
        if (files == 0) throw new InvalidOperationException("SourceSnapshotInventoryEmpty");
        return new(digest, files, bytes);

        IEnumerable<FileInventoryItem> CheckedInventory()
        {
            string? previous = null;
            foreach (var item in inventory)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item is null || string.IsNullOrWhiteSpace(item.RelativePath)
                    || item.RelativePath.Length > 4096 || string.IsNullOrWhiteSpace(item.Kind)
                    || item.Kind.Length > 256 || item.SizeBytes < 0
                    || Path.IsPathRooted(item.RelativePath) || item.RelativePath.Contains(':')
                    || item.RelativePath.Contains('\\')
                    || item.RelativePath.Split('/').Any(part => part is "" or "." or "..")
                    || (previous is not null && StringComparer.Ordinal.Compare(previous, item.RelativePath) >= 0))
                    throw new InvalidOperationException("SourceSnapshotInventoryInvalid");
                if (++files > maxFiles || item.SizeBytes > maxBytes - bytes)
                    throw new InvalidOperationException("SourceSnapshotInputLimit");
                bytes += item.SizeBytes;
                previous = item.RelativePath;
                // Never follow a retained locator through a link, including a
                // link inside the declared root. Raw path identity is retained.
                var current = root;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("SourceSnapshotLinkedInput");
                foreach (var part in item.RelativePath.Split('/'))
                {
                    current = Path.Combine(current, part);
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("SourceSnapshotLinkedInput");
                }
                yield return item;
            }
        }
    }
}
