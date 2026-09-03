using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

internal sealed class ComReferenceWorkspaceFallback : IDisposable
{
    internal const string CustomAfterTargetsProperty = "CustomAfterMicrosoftCommonTargets";

    private const string TargetsFileName = "tracemap.com-reference-workspace-fallback.targets";

    private static readonly string TargetsContent = """
        <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
          <!-- TraceMap only: preserve semantic admission without executing COM tooling. -->
          <Target Name="ResolveComReferences" Returns="@(ReferencePath)" />
          <Target Name="ResolveComReferencesDesignTime" Returns="@(ComReferenceWrappers)" />
        </Project>
        """;

    private readonly string? directoryPath;

    private ComReferenceWorkspaceFallback(
        IReadOnlyList<string> projectPaths,
        string? targetsPath,
        string? unavailableReason,
        string? directoryPath)
    {
        ProjectPaths = projectPaths;
        TargetsPath = targetsPath;
        UnavailableReason = unavailableReason;
        this.directoryPath = directoryPath;
    }

    public IReadOnlyList<string> ProjectPaths { get; }

    public string? TargetsPath { get; }

    public string? UnavailableReason { get; }

    public bool IsActive => TargetsPath is not null;

    public static ComReferenceWorkspaceFallback Prepare(
        string repoPath,
        IReadOnlyList<FileInventoryItem> projects)
    {
        var projectDocuments = projects
            .Select(project => (Project: project, Document: TryLoadProject(repoPath, project.RelativePath)))
            .Where(item => item.Document is not null)
            .ToArray();
        var comReferenceProjects = projectDocuments
            .Where(item => DeclaresComReference(item.Document!))
            .Select(item => FileInventory.NormalizeRelativePath(item.Project.RelativePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (comReferenceProjects.Length == 0)
        {
            return new ComReferenceWorkspaceFallback([], null, null, null);
        }

        if (projectDocuments.Any(item => DefinesCustomAfterTargets(item.Document!)))
        {
            return new ComReferenceWorkspaceFallback(
                comReferenceProjects,
                null,
                "project-custom-after-targets",
                null);
        }

        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "tracemap-msbuild",
            Guid.NewGuid().ToString("N"));
        var targetsPath = Path.Combine(directoryPath, TargetsFileName);

        try
        {
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(targetsPath, TargetsContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new ComReferenceWorkspaceFallback(
                comReferenceProjects,
                targetsPath,
                null,
                directoryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteDirectory(directoryPath);
            return new ComReferenceWorkspaceFallback(
                comReferenceProjects,
                null,
                "temporary-targets-unavailable",
                null);
        }
    }

    public void Dispose()
    {
        if (directoryPath is not null)
        {
            TryDeleteDirectory(directoryPath);
        }
    }

    private static XDocument? TryLoadProject(string repoPath, string relativePath)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var stream = File.OpenRead(Path.Combine(repoPath, relativePath));
            using var reader = XmlReader.Create(stream, settings);
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException)
        {
            return null;
        }
    }

    private static bool DeclaresComReference(XDocument document)
    {
        return document.Descendants().Any(element =>
            element.Name.LocalName is "COMReference" or "COMFileReference"
            && element.Attribute("Include") is not null);
    }

    private static bool DefinesCustomAfterTargets(XDocument document)
    {
        return document.Descendants().Any(element =>
            element.Name.LocalName == CustomAfterTargetsProperty
            && !string.IsNullOrWhiteSpace(element.Value));
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The temporary file contains no repository data and can be removed by the OS later.
        }
    }
}
