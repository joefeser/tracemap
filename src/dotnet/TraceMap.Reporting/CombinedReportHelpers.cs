using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TraceMap.Reporting;

public static class CombinedReportHelpers
{
    public static bool SourceIdentityVerified(CombinedReportSource source)
    {
        return !string.IsNullOrWhiteSpace(source.CommitSha)
            && !source.CommitSha.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            && source.CommitSha.Trim('0').Length > 0
            && !string.IsNullOrWhiteSpace(source.GitRootHash);
    }

    public static string NormalizeFormat(string? format, string commandName)
    {
        return (format ?? "markdown").ToLowerInvariant() switch
        {
            "markdown" or "md" => "markdown",
            "json" => "json",
            _ => throw new ArgumentException($"{commandName} --format must be markdown or json.")
        };
    }

    public static async Task<(string? MarkdownPath, string? JsonPath)> WriteOutputsAsync<T>(
        string outputPath,
        string format,
        string markdownFileName,
        string jsonFileName,
        T report,
        Func<T, string> renderMarkdown,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(outputPath);
        if (Directory.Exists(fullPath) || string.IsNullOrWhiteSpace(Path.GetExtension(fullPath)))
        {
            Directory.CreateDirectory(fullPath);
            var markdownPath = Path.Combine(fullPath, markdownFileName);
            var jsonPath = Path.Combine(fullPath, jsonFileName);
            await File.WriteAllTextAsync(markdownPath, renderMarkdown(report), cancellationToken);
            await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, jsonOptions) + Environment.NewLine, cancellationToken);
            return (markdownPath, jsonPath);
        }

        var directoryName = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        if (format == "json")
        {
            await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, jsonOptions) + Environment.NewLine, cancellationToken);
            return (null, fullPath);
        }

        await File.WriteAllTextAsync(fullPath, renderMarkdown(report), cancellationToken);
        return (fullPath, null);
    }

    public static string SafePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "n/a";
        }

        var fullyQualified = false;
        try
        {
            fullyQualified = Path.IsPathFullyQualified(filePath);
        }
        catch (ArgumentException)
        {
            fullyQualified = false;
        }

        return fullyQualified
            || filePath.StartsWith("/", StringComparison.Ordinal)
            || filePath.StartsWith("\\", StringComparison.Ordinal)
            || LooksLikeWindowsDrivePath(filePath)
            || filePath.Contains("://", StringComparison.Ordinal)
            || filePath.Contains(":/", StringComparison.Ordinal)
            || filePath.Contains(":\\", StringComparison.Ordinal)
            ? $"absolute-path-hash:{Hash(filePath, 16)}"
            : filePath.Replace('\\', '/');
    }

    private static bool LooksLikeWindowsDrivePath(string value)
    {
        return value.Length >= 2
            && char.IsAsciiLetter(value[0])
            && value[1] == ':';
    }

    public static string Hash(string value, int length = 64)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return text[..Math.Min(length, text.Length)];
    }

    public static string Cell(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .ReplaceLineEndings(" ")
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("<", "\\<", StringComparison.Ordinal)
            .Replace(">", "\\>", StringComparison.Ordinal);
    }

    public static IReadOnlyList<KeyValuePair<string, string>> SortedMetadata(IEnumerable<KeyValuePair<string, string?>> values)
    {
        return values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value!.Trim()))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
