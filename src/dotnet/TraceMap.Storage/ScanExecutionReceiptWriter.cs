using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Storage;

public static class ScanExecutionReceiptWriter
{
    public static async Task WriteAsync(string path, ScanExecutionReceipt receipt, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, receipt, JsonOptions.Stable, cancellationToken);
                await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
