namespace TraceMap.Core;

public static class FileInventory
{
    private static readonly HashSet<string> IncludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sln",
        ".csproj",
        ".vbproj",
        ".fsproj",
        ".props",
        ".targets",
        ".resx",
        ".settings",
        ".config",
        ".json",
        ".cs",
        ".sql",
        ".aspx",
        ".ascx",
        ".master",
        ".svc",
        ".asmx",
        ".svcmap",
        ".wsdl",
        ".disco",
        ".xsd"
    };

    private static readonly HashSet<string> IncludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "packages.config",
        "packages.lock.json",
        "nuget.config",
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

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        };

        var candidates = Directory.EnumerateFiles(root, "*", options)
            .Where(path => !ShouldExclude(root, path, outputFullPath))
            .ToArray();
        var serviceReferenceFolders = candidates
            .Where(path => Path.GetExtension(path).Equals(".svcmap", StringComparison.OrdinalIgnoreCase))
            .Select(path => NormalizeRelativePath(Path.GetDirectoryName(Path.GetRelativePath(root, path)) ?? "."))
            .ToHashSet(StringComparer.Ordinal);

        var items = candidates
            .Where(path => ShouldInclude(root, path, serviceReferenceFolders))
            .Select(path => TryCreateItem(root, path))
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();

        return items;
    }

    private static FileInventoryItem? TryCreateItem(string root, string path)
    {
        try
        {
            var info = new FileInfo(path);
            return new FileInventoryItem(
                NormalizeRelativePath(Path.GetRelativePath(root, path)),
                GetKind(path),
                info.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool ShouldExclude(string root, string path, string? outputFullPath)
    {
        var fullPath = Path.GetFullPath(path);
        if (outputFullPath is not null && IsUnderDirectory(fullPath, outputFullPath))
        {
            return true;
        }

        var relativePath = Path.GetRelativePath(root, fullPath);
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => ExcludedDirectoryNames.Contains(part));
    }

    private static bool ShouldInclude(string root, string path, ISet<string> serviceReferenceFolders)
    {
        var fullPath = Path.GetFullPath(path);
        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath);
        if (IsWcfMetadataExtension(extension))
        {
            return IsServiceReferenceMetadataPath(root, fullPath, serviceReferenceFolders);
        }

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

        if (fileName.Equals("packages.lock.json", StringComparison.OrdinalIgnoreCase))
        {
            return "PackagesLock";
        }

        if (fileName.Equals("nuget.config", StringComparison.OrdinalIgnoreCase))
        {
            return "NuGetConfig";
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
            ".vbproj" => "NonCSharpProject",
            ".fsproj" => "NonCSharpProject",
            ".props" => "MSBuildProps",
            ".targets" => "MSBuildTargets",
            ".resx" => "Resource",
            ".settings" => "Settings",
            ".json" => "Json",
            ".cs" when IsWebFormsDesignerFile(fileName) => "WebFormsDesigner",
            ".cs" when IsWebFormsCodeBehindFile(fileName) => "WebFormsCodeBehind",
            ".cs" => "CSharp",
            ".sql" => "Sql",
            ".aspx" => "WebFormsMarkup",
            ".ascx" => "WebFormsMarkup",
            ".master" => "WebFormsMarkup",
            ".svc" => "ServiceHost",
            ".asmx" => "ServiceHost",
            ".svcmap" => "ServiceReferenceMetadata",
            ".wsdl" => "ServiceReferenceMetadata",
            ".disco" => "ServiceReferenceMetadata",
            ".xsd" => "ServiceReferenceMetadata",
            _ => "File"
        };
    }

    public static bool IsCSharpKind(string kind)
    {
        return kind.Equals("CSharp", StringComparison.Ordinal)
            || kind.Equals("WebFormsCodeBehind", StringComparison.Ordinal)
            || kind.Equals("WebFormsDesigner", StringComparison.Ordinal);
    }

    private static bool IsWebFormsCodeBehindFile(string fileName)
    {
        return fileName.EndsWith(".aspx.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".ascx.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".master.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWebFormsDesignerFile(string fileName)
    {
        return fileName.EndsWith(".aspx.designer.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".ascx.designer.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".master.designer.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWcfMetadataExtension(string extension)
    {
        return extension.Equals(".svcmap", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".wsdl", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".disco", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xsd", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsServiceReferenceMetadataPath(string root, string fullPath, ISet<string> serviceReferenceFolders)
    {
        var extension = Path.GetExtension(fullPath);
        if (extension.Equals(".svcmap", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var relativePath = Path.GetRelativePath(root, fullPath);
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var directory = NormalizeRelativePath(Path.GetDirectoryName(relativePath) ?? ".");
        if (serviceReferenceFolders.Contains(directory))
        {
            return true;
        }

        return normalizedRelativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(IsServiceReferenceSegment);
    }

    private static bool IsServiceReferenceSegment(string segment)
    {
        return segment.Equals("Service Reference", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Service References", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("ServiceReference", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("ServiceReferences", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
