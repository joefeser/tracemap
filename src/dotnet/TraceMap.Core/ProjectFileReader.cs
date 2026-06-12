using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

public sealed record TargetFrameworkInfo(string ProjectPath, string TargetFramework, int Line);

public sealed record PackageReferenceInfo(string ProjectPath, string PackageName, string? Version, int Line);

public static class ProjectFileReader
{
    public static IReadOnlyList<TargetFrameworkInfo> ReadTargetFrameworks(string repoPath, IEnumerable<FileInventoryItem> inventory)
    {
        var results = new List<TargetFrameworkInfo>();
        foreach (var project in inventory.Where(item => item.Kind == "Project"))
        {
            var fullPath = Path.Combine(repoPath, project.RelativePath);
            foreach (var item in ReadProjectValues(fullPath, "TargetFramework", "TargetFrameworks"))
            {
                foreach (var target in item.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    results.Add(new TargetFrameworkInfo(project.RelativePath, target, item.Line));
                }
            }
        }

        return results
            .OrderBy(item => item.ProjectPath, StringComparer.Ordinal)
            .ThenBy(item => item.TargetFramework, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<PackageReferenceInfo> ReadPackageReferences(string repoPath, IEnumerable<FileInventoryItem> inventory)
    {
        var results = new List<PackageReferenceInfo>();
        foreach (var project in inventory.Where(item => item.Kind == "Project"))
        {
            var fullPath = Path.Combine(repoPath, project.RelativePath);
            results.AddRange(ReadPackageReferencesFromProject(fullPath, project.RelativePath));
        }

        foreach (var packagesConfig in inventory.Where(item => item.Kind == "PackagesConfig"))
        {
            var fullPath = Path.Combine(repoPath, packagesConfig.RelativePath);
            results.AddRange(ReadPackagesConfig(fullPath, packagesConfig.RelativePath));
        }

        return results
            .OrderBy(item => item.ProjectPath, StringComparer.Ordinal)
            .ThenBy(item => item.PackageName, StringComparer.Ordinal)
            .ThenBy(item => item.Version, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<(string Value, int Line)> ReadProjectValues(string fullPath, params string[] elementNames)
    {
        if (!TryLoadXml(fullPath, out var document))
        {
            yield break;
        }

        foreach (var element in document.Descendants().Where(element => elementNames.Contains(element.Name.LocalName, StringComparer.Ordinal)))
        {
            var value = element.Value.Trim();
            if (value.Length == 0)
            {
                continue;
            }

            yield return (value, GetLine(element));
        }
    }

    private static IEnumerable<PackageReferenceInfo> ReadPackageReferencesFromProject(string fullPath, string relativePath)
    {
        if (!TryLoadXml(fullPath, out var document))
        {
            yield break;
        }

        foreach (var element in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
        {
            var packageName = AttributeValue(element, "Include") ?? AttributeValue(element, "Update");
            if (string.IsNullOrWhiteSpace(packageName))
            {
                continue;
            }

            var version = AttributeValue(element, "Version")
                ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version")?.Value.Trim();
            yield return new PackageReferenceInfo(relativePath, packageName, version, GetLine(element));
        }
    }

    private static IEnumerable<PackageReferenceInfo> ReadPackagesConfig(string fullPath, string relativePath)
    {
        if (!TryLoadXml(fullPath, out var document))
        {
            yield break;
        }

        foreach (var element in document.Descendants().Where(element => element.Name.LocalName == "package"))
        {
            var packageName = AttributeValue(element, "id");
            if (string.IsNullOrWhiteSpace(packageName))
            {
                continue;
            }

            yield return new PackageReferenceInfo(relativePath, packageName, AttributeValue(element, "version"), GetLine(element));
        }
    }

    private static bool TryLoadXml(string fullPath, out XDocument document)
    {
        try
        {
            document = XDocument.Load(fullPath, LoadOptions.SetLineInfo);
            return true;
        }
        catch
        {
            document = new XDocument();
            return false;
        }
    }

    private static string? AttributeValue(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value.Trim();
    }

    private static int GetLine(XObject node)
    {
        return node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? Math.Max(1, lineInfo.LineNumber)
            : 1;
    }
}
