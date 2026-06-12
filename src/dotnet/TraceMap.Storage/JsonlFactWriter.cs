using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Storage;

public static class JsonlFactWriter
{
    public static async Task WriteAsync(string path, IEnumerable<CodeFact> facts, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream);

        foreach (var fact in facts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = JsonSerializer.Serialize(fact, JsonOptions.StableLine);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }
}
