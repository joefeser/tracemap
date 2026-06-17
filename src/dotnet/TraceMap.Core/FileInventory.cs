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
        ".cshtml",
        ".sql",
        ".aspx",
        ".ascx",
        ".master",
        ".svc",
        ".asmx",
        ".svcmap",
        ".dbml",
        ".edmx",
        ".wsdl",
        ".disco",
        ".discomap",
        ".map",
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
        ".nuget",
        "bin",
        "node_modules",
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

        var candidateFiles = EnumerateCandidateFiles(root, options, outputFullPath).ToArray();

        var serviceReferenceFolders = candidateFiles
            .Where(path => Path.GetExtension(path).Equals(".svcmap", StringComparison.OrdinalIgnoreCase))
            .Select(path => NormalizeRelativePath(Path.GetDirectoryName(Path.GetRelativePath(root, path)) ?? "."))
            .ToHashSet(StringComparer.Ordinal);
        var webReferenceFolders = candidateFiles
            .Where(path => IsAsmxMetadataExtension(Path.GetExtension(path)))
            .Where(path => IsWebReferenceMetadataPath(root, path))
            .Select(path => NormalizeRelativePath(Path.GetDirectoryName(Path.GetRelativePath(root, path)) ?? "."))
            .ToHashSet(StringComparer.Ordinal);

        var items = candidateFiles
            .Where(path => ShouldInclude(root, path, serviceReferenceFolders, webReferenceFolders))
            .Select(path => TryCreateItem(root, path, serviceReferenceFolders, webReferenceFolders))
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();

        return items;
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string root, EnumerationOptions options, string? outputFullPath)
    {
        return Directory.EnumerateFiles(root, "*", options)
            .Where(path => !ShouldExclude(root, path, outputFullPath));
    }

    private static FileInventoryItem? TryCreateItem(string root, string path, ISet<string> serviceReferenceFolders, ISet<string> webReferenceFolders)
    {
        try
        {
            var info = new FileInfo(path);
            return new FileInventoryItem(
                NormalizeRelativePath(Path.GetRelativePath(root, path)),
                GetKind(root, path, serviceReferenceFolders, webReferenceFolders),
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
        return IsRootPackagesDirectory(parts)
            || parts.Any(part => ExcludedDirectoryNames.Contains(part));
    }

    private static bool ShouldInclude(string root, string path, ISet<string> serviceReferenceFolders, ISet<string> webReferenceFolders)
    {
        var fullPath = Path.GetFullPath(path);
        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath);
        if (extension.Equals(".xsd", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsWcfMetadataExtension(extension))
        {
            return IsServiceReferenceMetadataPath(root, fullPath, serviceReferenceFolders)
                || (IsAsmxMetadataExtension(extension) && IsAsmxMetadataPath(root, fullPath, webReferenceFolders));
        }

        if (IsAsmxMetadataExtension(extension))
        {
            return IsAsmxMetadataPath(root, fullPath, webReferenceFolders);
        }

        return IncludedFileNames.Contains(fileName) || IncludedExtensions.Contains(extension);
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return path.Equals(directory, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRootPackagesDirectory(IReadOnlyList<string> parts)
    {
        return parts.Count > 1 && parts[0].Equals("packages", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetKind(string root, string path, ISet<string> serviceReferenceFolders, ISet<string> webReferenceFolders)
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
            ".cshtml" => "Razor",
            ".sql" => "Sql",
            ".aspx" => "WebFormsMarkup",
            ".ascx" => "WebFormsMarkup",
            ".master" => "WebFormsMarkup",
            ".svc" => "ServiceHost",
            ".asmx" => "AsmxServiceHost",
            ".svcmap" => "ServiceReferenceMetadata",
            ".wsdl" when IsServiceReferenceMetadataPath(root, path, serviceReferenceFolders) => "ServiceReferenceMetadata",
            ".wsdl" when IsAsmxMetadataPath(root, path, webReferenceFolders) => "AsmxServiceReferenceMetadata",
            ".disco" when IsServiceReferenceMetadataPath(root, path, serviceReferenceFolders) => "ServiceReferenceMetadata",
            ".disco" when IsAsmxMetadataPath(root, path, webReferenceFolders) => "AsmxServiceReferenceMetadata",
            ".discomap" when IsAsmxMetadataPath(root, path, webReferenceFolders) => "AsmxServiceReferenceMetadata",
            ".map" when IsAsmxMetadataPath(root, path, webReferenceFolders) => "AsmxServiceReferenceMetadata",
            ".xsd" when IsServiceReferenceMetadataPath(root, path, serviceReferenceFolders) => "ServiceReferenceMetadata",
            ".xsd" => "XsdSchema",
            ".dbml" => "Dbml",
            ".edmx" => "Edmx",
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

    private static bool IsAsmxMetadataExtension(string extension)
    {
        return extension.Equals(".wsdl", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".disco", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".discomap", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".map", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsAsmxMetadataPath(string root, string fullPath, ISet<string> webReferenceFolders)
    {
        var relativePath = Path.GetRelativePath(root, fullPath);
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var directory = NormalizeRelativePath(Path.GetDirectoryName(relativePath) ?? ".");
        if (webReferenceFolders.Contains(directory))
        {
            return true;
        }

        return IsWebReferenceMetadataPath(root, fullPath)
            || normalizedRelativePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(IsWebReferenceSegment);
    }

    private static bool IsWebReferenceMetadataPath(string root, string fullPath)
    {
        var relativePath = Path.GetRelativePath(root, fullPath);
        return NormalizeRelativePath(relativePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(IsWebReferenceSegment);
    }

    private static bool IsWebReferenceSegment(string segment)
    {
        return segment.Equals("Web Reference", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Web References", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("WebReference", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("WebReferences", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
