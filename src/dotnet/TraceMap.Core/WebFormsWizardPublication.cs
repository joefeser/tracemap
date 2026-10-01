using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

public sealed record WebFormsWizardInputSnapshot(string Role, string Path, long Bytes, string Sha256);
public sealed record WebFormsWizardPublicationInfo(string Root, string[] ManagedAssemblies, string[] NativeAssemblies,
    string[] PageMaps, string[] MetadataFiles);

/// <summary>Static publication inventory and local input change detection, not build authenticity.</summary>
public static class WebFormsWizardPublication
{
    public const string RuleId = "workflow.webforms.wizard-publication.v1";
    public const int MaxNativeInputFiles = 256;
    // Configuration, selected project, binding receipt and publication receipt.
    public const int MaxCombinedAssemblies = MaxNativeInputFiles - 4;
    private const int MaxAssemblySelections = 128;
    private const int MaxPublicationFiles = 1024;
    private const int MaxSourceFiles = 10_000;
    // Primary assemblies share the bin inventory with maps; dependencies may be external.
    // Source input may additionally name a solution outside the website root.
    internal const int MaxRetainedInputs = MaxSourceFiles + 1 + MaxPublicationFiles + 2 + MaxAssemblySelections;
    private const long MaxFileBytes = 67_108_864;
    private const long MaxTotalBytes = 2_147_483_648;

    public static WebFormsWizardPublicationInfo Inspect(string folder, bool projectless)
    {
        var root = WebFormsWizardStore.Physical(folder);
        if (!Directory.Exists(root)) throw Fail("PUBLICATION_MISSING");
        var bin = Path.Combine(root, "bin");
        if (!Directory.Exists(bin)) throw Fail("PUBLICATION_BIN_MISSING_CHOOSE_SITE_ROOT");
        RejectLink(bin);
        var config = Path.Combine(root, "web.config");
        if (!File.Exists(config)) config = Path.Combine(root, "Web.config");
        ReadXml(config, "configuration");
        var metadata = new List<string> { config };
        var precompiled = Path.Combine(root, "PrecompiledApp.config");
        if (projectless || File.Exists(precompiled))
        {
            ReadXml(precompiled, "precompiledApp");
            metadata.Add(precompiled);
        }
        var files = Directory.EnumerateFiles(bin).Take(MaxPublicationFiles + 1).Order(StringComparer.Ordinal).ToArray();
        if (files.Length > MaxPublicationFiles) throw Fail("PUBLICATION_INVENTORY_LIMIT");
        var managed = new List<string>();
        var native = new List<string>();
        var maps = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            RejectLink(file);
            if (!names.Add(Path.GetFileName(file))) throw Fail("PUBLICATION_CASE_COLLISION");
            if (Path.GetExtension(file).Equals(".compiled", StringComparison.OrdinalIgnoreCase))
            {
                ReadXml(file, "preserve");
                maps.Add(file);
            }
            if (!Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase)) continue;
            if (IsManaged(file)) managed.Add(file); else native.Add(file);
        }
        if (managed.Count == 0) throw Fail("PUBLICATION_MANAGED_ASSEMBLIES_MISSING");
        return new(root, managed.ToArray(), native.ToArray(), maps.ToArray(), metadata.ToArray());
    }

    public static void Configure(WebFormsWizardStore store, string id, string folder,
        IReadOnlyList<string> primary, IReadOnlyList<string> dependencies)
    {
        var project = store.ReadProject(id);
        if (project.Step is not ("publication" or "dependencies")) throw Fail("PUBLICATION_STEP_REQUIRED");
        var inventory = Inspect(folder, project.ProjectMode == "projectless");
        if (primary.Count is 0 or > MaxAssemblySelections || dependencies.Count > MaxAssemblySelections
            || primary.Count + dependencies.Count > MaxCombinedAssemblies) throw Fail("ASSEMBLY_SELECTION_LIMIT");
        var selected = primary.Select(path => Resolve(inventory.Root, path)).ToArray();
        var deps = dependencies.Select(path => Resolve(inventory.Root, path)).ToArray();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in selected.Concat(deps))
        {
            if (!unique.Add(path)) throw Fail("ASSEMBLY_SELECTION_DUPLICATE");
            if (!IsManaged(path)) throw Fail("ASSEMBLY_NOT_MANAGED");
        }
        foreach (var path in selected)
            if (!inventory.ManagedAssemblies.Contains(path, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
                throw Fail("PRIMARY_NOT_IN_PUBLICATION_BIN");
        var inputs = new List<WebFormsWizardInputSnapshot>();
        long total = 0;
        foreach (var path in selected) Add("primary-assembly", path);
        foreach (var path in deps) Add("dependency-assembly", path);
        foreach (var path in inventory.MetadataFiles.Concat(inventory.PageMaps)) Add("publication-metadata", path);
        var forms = project.FormsMode == "all" ? WebFormsWizardForms.Discover(project.WebRoot) : WebFormsWizardForms.Parse(project.WebRoot, string.Join('\n', project.Forms));
        foreach (var path in SourceFiles(project)) Add("source-input", path);
        store.SaveProject(project with { PublishedRoot = inventory.Root, PrimaryAssemblies = selected,
            Dependencies = deps, Forms = forms.ToArray(), Inputs = inputs.OrderBy(item => item.Role, StringComparer.Ordinal)
                .ThenBy(item => item.Path, StringComparer.Ordinal).ToArray(), Step = "dependencies" });

        void Add(string role, string path)
        {
            if (inputs.Count >= MaxRetainedInputs) throw Fail("INPUT_TOTAL_LIMIT");
            if (new FileInfo(path).Length > MaxTotalBytes - total) throw Fail("INPUT_TOTAL_LIMIT");
            var value = Snapshot(role, path);
            total += value.Bytes;
            if (total > MaxTotalBytes) throw Fail("INPUT_TOTAL_LIMIT");
            inputs.Add(value);
        }
    }

    public static void ValidateRetained(WebFormsWizardProject project)
    {
        if (project.PublishedRoot is null) return;
        var inventory = Inspect(project.PublishedRoot, project.ProjectMode == "projectless");
        if (inventory.Root != project.PublishedRoot) throw Fail("PUBLICATION_ROOT_CHANGED");
        if (project.Inputs is not { Length: > 0 }) throw Fail("INPUT_SNAPSHOT_MISSING");
        if (project.Inputs.Length > MaxRetainedInputs || project.Inputs.Any(item => item is null || item.Bytes < 0 || item.Bytes > MaxFileBytes)
            || project.Inputs.Sum(item => item.Bytes) > MaxTotalBytes) throw Fail("INPUT_TOTAL_LIMIT");
        foreach (var input in project.Inputs)
        {
            var actual = Snapshot(input.Role, input.Path);
            if (actual != input) throw Fail("INPUT_CHANGED");
        }
        var expectedMetadata = inventory.MetadataFiles.Concat(inventory.PageMaps).Order(StringComparer.Ordinal);
        if (!expectedMetadata.SequenceEqual(project.Inputs.Where(item => item.Role == "publication-metadata")
                .Select(item => item.Path).Order(StringComparer.Ordinal))) throw Fail("PUBLICATION_INVENTORY_CHANGED");
        if (!SourceFiles(project).SequenceEqual(project.Inputs.Where(item => item.Role == "source-input")
                .Select(item => item.Path).Order(StringComparer.Ordinal))) throw Fail("SOURCE_INVENTORY_CHANGED");
        if (project.FormsMode == "all" && !WebFormsWizardForms.Discover(project.WebRoot).SequenceEqual(project.Forms)) throw Fail("SOURCE_FORMS_CHANGED");
    }

    private static string[] SourceFiles(WebFormsWizardProject project)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(project.WebRoot);
        var visited = 0;
        while (pending.Count > 0)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                if (++visited > WebFormsWizardForms.MaxInventoryEntries) throw Fail("SOURCE_INVENTORY_LIMIT");
                RejectLink(path);
                if (Directory.Exists(path))
                {
                    if (Path.GetFileName(path).ToLowerInvariant() is not ("bin" or "obj" or ".git")) pending.Push(path);
                }
                else if (Path.GetExtension(path).ToLowerInvariant() is ".cs" or ".vb" or ".config" or ".aspx" or ".ascx" or ".master"
                    or ".ashx" or ".asmx" or ".asax" or ".resx" or ".csproj" or ".vbproj") paths.Add(path);
                if (paths.Count > MaxSourceFiles) throw Fail("SOURCE_INVENTORY_LIMIT");
            }
        }
        if (File.Exists(project.InputPath)) paths.Add(project.InputPath);
        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    private static string Resolve(string root, string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!OperatingSystem.IsWindows() && normalized.Contains(':')) throw Fail("ASSEMBLY_PATH_INVALID");
        var full = Path.GetFullPath(normalized, root);
        RejectLink(full);
        return WebFormsWizardStore.Physical(full);
    }

    private static WebFormsWizardInputSnapshot Snapshot(string role, string path)
    {
        RejectLink(path);
        if (WebFormsWizardStore.Physical(path) != path) throw Fail("INPUT_PATH_CHANGED");
        using var stream = Open(path);
        var size = stream.Length;
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (stream.Length != size) throw Fail("INPUT_CHANGED");
        return new(role, path, size, hash);
    }
    private static bool IsManaged(string path)
    {
        RejectLink(path);
        using var stream = Open(path);
        using var pe = new PEReader(stream);
        return pe.HasMetadata && pe.GetMetadataReader().IsAssembly;
    }
    private static FileStream Open(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxFileBytes) { stream.Dispose(); throw Fail("INPUT_FILE_LIMIT"); }
        return stream;
    }
    private static void ReadXml(string path, string expected)
    {
        RejectLink(path);
        using var stream = Open(path);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = 1_048_576 });
        if (XDocument.Load(reader).Root?.Name.LocalName != expected) throw Fail("PUBLICATION_METADATA_INVALID");
    }
    private static void RejectLink(string path)
    {
        FileSystemInfo entry = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (entry.LinkTarget is not null) throw Fail("PUBLICATION_LINKED");
    }
    private static InvalidOperationException Fail(string code) => new("WEBFORMS_WIZARD_" + code);
}
