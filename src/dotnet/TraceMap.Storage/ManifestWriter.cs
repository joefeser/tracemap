using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Storage;

public static class ManifestWriter
{
    public static async Task WriteAsync(string path, ScanManifest manifest, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions.Stable, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
    }
}
