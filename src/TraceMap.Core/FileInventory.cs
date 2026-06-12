namespace TraceMap.Core;

public static class FileInventory
{
    private static readonly HashSet<string> IncludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sln",
        ".csproj",
        ".config",
        ".json",
        ".cs",
        ".sql"
    };

    private static readonly HashSet<string> IncludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "packages.config",
        "Web.config",
        "App.config"
    };

    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".tracemap",
        "bin",
        "obj"
    };

    public static IReadOnlyList<FileInventoryItem> Collect(string repoPath, string? outputPath = null)
    {
        var root = Path.GetFullPath(repoPath);
        var outputFullPath = string.IsNullOrWhiteSpace(outputPath)
            ? null
            : Path.GetFullPath(outputPath);

        var items = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => ShouldInclude(root, path, outputFullPath))
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new FileInventoryItem(
                    NormalizeRelativePath(Path.GetRelativePath(root, path)),
                    GetKind(path),
                    info.Length);
            })
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();

        return items;
    }

    private static bool ShouldInclude(string root, string path, string? outputFullPath)
    {
        var fullPath = Path.GetFullPath(path);
        if (outputFullPath is not null && IsUnderDirectory(fullPath, outputFullPath))
        {
            return false;
        }

        var relativePath = Path.GetRelativePath(root, fullPath);
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(part => ExcludedDirectoryNames.Contains(part)))
        {
            return false;
        }

        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath);
        return IncludedFileNames.Contains(fileName) || IncludedExtensions.Contains(extension);
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return path.Equals(directory, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetKind(string path)
    {
        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);

        if (fileName.Equals("packages.config", StringComparison.OrdinalIgnoreCase))
        {
            return "PackagesConfig";
        }

        if (fileName.Equals("Web.config", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("App.config", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".config", StringComparison.OrdinalIgnoreCase))
        {
            return "Config";
        }

        return extension.ToLowerInvariant() switch
        {
            ".sln" => "Solution",
            ".csproj" => "Project",
            ".json" => "Json",
            ".cs" => "CSharp",
            ".sql" => "Sql",
            _ => "File"
        };
    }

    public static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
