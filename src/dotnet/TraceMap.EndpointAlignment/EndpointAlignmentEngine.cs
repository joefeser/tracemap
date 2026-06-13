using System.Text.Json;

namespace TraceMap.EndpointAlignment;

public static class EndpointAlignmentEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<EndpointAlignmentWriteResult> AlignAsync(EndpointAlignmentOptions options, CancellationToken cancellationToken = default)
    {
        var client = await EndpointIndexReader.ReadClientAsync(options.ClientIndexPath, options.ClientLabel, cancellationToken);
        var server = await EndpointIndexReader.ReadServerAsync(options.ServerIndexPath, options.ServerLabel, cancellationToken);
        var report = EndpointMatcher.Match(client, server);
        var paths = ResolveOutputPaths(options.OutputPath, options.Format);

        if (paths.MarkdownPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.MarkdownPath)!);
            await File.WriteAllTextAsync(paths.MarkdownPath, EndpointMarkdownWriter.Build(report), cancellationToken);
        }

        if (paths.JsonPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.JsonPath)!);
            await File.WriteAllTextAsync(paths.JsonPath, JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine, cancellationToken);
        }

        return new EndpointAlignmentWriteResult(report, paths.MarkdownPath, paths.JsonPath);
    }

    private static (string? MarkdownPath, string? JsonPath) ResolveOutputPaths(string outputPath, string format)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var extension = Path.GetExtension(fullPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return (Path.Combine(fullPath, "endpoint-report.md"), Path.Combine(fullPath, "endpoint-report.json"));
        }

        return format.Equals("json", StringComparison.OrdinalIgnoreCase)
            ? (null, fullPath)
            : (fullPath, null);
    }
}
