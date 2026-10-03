using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TraceMap.Core;

public sealed record WebFormsWizardProject(string Id, string InputPath, string WebRoot,
    string ProjectMode, string FormsMode, string[] Forms, string? PublishedRoot,
    string[] PrimaryAssemblies, string[] Dependencies, string Step, WebFormsWizardBuildEvidence? Build = null,
    WebFormsWizardInputSnapshot[]? Inputs = null, WebFormsWizardNativeReference? Native = null,
    WebFormsWizardRunReference? Run = null);
public sealed record WebFormsWizardRunReference(string RelativeRoot, string AttestedCommitSha, string NativeConfigSha256);
public sealed record WebFormsWizardNativeReference(string RelativePath, string Sha256, string SourceCommitSha,
    WebFormsWizardInputSnapshot[] Inputs);
public sealed record WebFormsWizardProjectReference(string Id, string ConfigSha256);
public sealed record WebFormsWizardRoot(long Revision, WebFormsWizardProjectReference[] Projects);
public sealed record WebFormsWizardDocument<T>(string SchemaVersion, string RuleId, string Visibility,
    string GeneratorSha256, string BoundedInputSha256, T Configuration);

/// <summary>
/// Local-only, exclusive wizard configuration store. Hashes detect changes, not authorship.
/// File replacement is atomic per file; a crash between project/root replacement fails closed
/// with PROJECT_CHANGED. No source or publication content is written or executed.
/// </summary>
public sealed class WebFormsWizardStore : IDisposable
{
    public const string RuleId = "workflow.webforms.wizard-state.v1";
    private const int MaxBytes = 1_048_576;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, MaxDepth = 24, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly FileStream lease;
    private readonly string generator;
    private string rootHash;
    private bool disposed;
    private WebFormsWizardRoot state;
    public string DirectoryPath { get; }
    public WebFormsWizardRoot State => state with { Projects = [.. state.Projects] };

    private WebFormsWizardStore(string root, FileStream lease, string generator, WebFormsWizardRoot state, string rootHash)
    {
        DirectoryPath = root;
        this.lease = lease;
        this.generator = generator;
        this.state = state;
        this.rootHash = rootHash;
    }

    public static WebFormsWizardStore Open(string directory, bool resume)
    {
        var root = Physical(directory);
        if (!resume && (File.Exists(root) || Directory.Exists(root))) throw Fail("ROOT_EXISTS");
        if (resume && !Directory.Exists(root)) throw Fail("ROOT_MISSING");
        if (!resume)
        {
            var parent = Path.GetDirectoryName(root) ?? throw Fail("ROOT_INVALID");
            Directory.CreateDirectory(parent);
            var staging = Path.Combine(parent, ".wizard-new-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            // Publish the root config inside the staged directory so an interrupted
            // create can never leave a visible root without its config.
            var stagedGenerator = Hash(File.ReadAllBytes(typeof(WebFormsWizardStore).Assembly.Location));
            try
            {
                Atomic(Path.Combine(staging, "root.config.json"), Encode("webforms-wizard-root.v1", stagedGenerator, new WebFormsWizardRoot(0, [])));
                Directory.Move(staging, root);
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
        }
        var lockPath = Path.Combine(root, ".wizard.lock");
        RejectLink(lockPath);
        FileStream held;
        try { held = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw Fail("ROOT_BUSY"); }
        try
        {
            var generator = Hash(File.ReadAllBytes(typeof(WebFormsWizardStore).Assembly.Location));
            var path = Path.Combine(root, "root.config.json");
            WebFormsWizardRoot state;
            byte[] bytes;
            bytes = ReadBytes(path);
            state = Read<WebFormsWizardRoot>(bytes, "webforms-wizard-root.v1");
            ValidateRoot(state);
            return new(root, held, generator, state, Hash(bytes));
        }
        catch { held.Dispose(); throw; }
    }

    public WebFormsWizardProject ReadProject(string id)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Id(id);
        var reference = state.Projects.SingleOrDefault(item => item.Id == id) ?? throw Fail("PROJECT_UNKNOWN");
        var path = ProjectPath(id);
        var bytes = ReadBytes(path);
        if (Hash(bytes) != reference.ConfigSha256) throw Fail("PROJECT_CHANGED");
        var project = Read<WebFormsWizardProject>(bytes, "webforms-wizard-project.v1");
        ValidateProject(project);
        if (project.Id != id) throw Fail("PROJECT_ID_MISMATCH");
        ProtectInputs(project);
        return project;
    }

    public void SaveProject(WebFormsWizardProject project)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ValidateProject(project);
        ProtectInputs(project);
        var rootPath = Path.Combine(DirectoryPath, "root.config.json");
        VerifyUnchanged();
        var old = state.Projects.SingleOrDefault(item => item.Id == project.Id);
        if (old is not null) _ = ReadProject(project.Id);
        if (old is null && state.Projects.Length >= 128) throw Fail("PROJECT_LIMIT");
        var path = ProjectPath(project.Id);
        if (old is null && Directory.Exists(Path.GetDirectoryName(path)!) && !IsAbandonedRegistration(Path.GetDirectoryName(path)!))
            throw Fail("PROJECT_FOLDER_EXISTS");
        var bytes = Encode("webforms-wizard-project.v1", generator, project);
        var next = new WebFormsWizardRoot(checked(state.Revision + 1), state.Projects
            .Where(item => item.Id != project.Id).Append(new(project.Id, Hash(bytes)))
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray());
        var nextBytes = Encode("webforms-wizard-root.v1", generator, next);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Atomic(path, bytes);
        Atomic(rootPath, nextBytes);
        state = next;
        rootHash = Hash(nextBytes);
    }

    public string ProjectPath(string id)
    {
        Id(id);
        var parent = Path.Combine(DirectoryPath, id);
        RejectLink(parent);
        return Path.Combine(parent, "project.config.json");
    }

    public void VerifyUnchanged()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Hash(ReadBytes(Path.Combine(DirectoryPath, "root.config.json"))) != rootHash) throw Fail("ROOT_CHANGED");
    }

    public string PreviewRepair(string id)
    {
        VerifyUnchanged();
        Id(id);
        if (!state.Projects.Any(item => item.Id == id)) throw Fail("PROJECT_UNKNOWN");
        var path = ProjectPath(id);
        RejectLink(path);
        return File.Exists(path) ? Hash(ReadBytes(path, allowEmpty: true)) : "missing";
    }

    public string? RepairProject(WebFormsWizardProject replacement, string observedHash, bool confirmed)
    {
        if (!confirmed) return null;
        replacement = replacement with { Step = "forms", Forms = [], PublishedRoot = null,
            PrimaryAssemblies = [], Dependencies = [], Build = null, Inputs = null, Native = null, Run = null };
        ValidateProject(replacement);
        ProtectInputs(replacement);
        if (PreviewRepair(replacement.Id) != observedHash) throw Fail("REPAIR_PREVIEW_CHANGED");
        var path = ProjectPath(replacement.Id);
        var forms = Path.Combine(Path.GetDirectoryName(path)!, "forms.txt");
        RejectLink(forms);
        var history = Path.Combine(DirectoryPath, "project-history");
        RejectLink(history);
        var archive = Path.Combine(history, replacement.Id + "-" + Guid.NewGuid().ToString("N"));
        var bytes = Encode("webforms-wizard-project.v1", generator, replacement);
        var next = new WebFormsWizardRoot(checked(state.Revision + 1), state.Projects
            .Select(item => item.Id == replacement.Id ? new WebFormsWizardProjectReference(item.Id, Hash(bytes)) : item).ToArray());
        var nextBytes = Encode("webforms-wizard-root.v1", generator, next);
        var original = observedHash == "missing" ? null : ReadBytes(path, allowEmpty: true);
        if (original is not null && Hash(original) != observedHash) throw Fail("REPAIR_PREVIEW_CHANGED");
        var receipt = Encode("webforms-wizard-repair.v1", generator, new
        {
            projectId = replacement.Id, observedProjectSha256 = observedHash,
            priorRegisteredSha256 = state.Projects.Single(item => item.Id == replacement.Id).ConfigSha256,
            replacementSha256 = Hash(bytes), preservedRuns = true, preservedNativeInputs = true
        });
        Directory.CreateDirectory(archive);
        if (original is not null) Atomic(Path.Combine(archive, "project.config.original.json"), original);
        Atomic(Path.Combine(archive, "repair.json"), receipt);
        // Keep native configs, staged publication paths and runs in place: old native
        // manifests refer to their exact locations. Only editable form selection moves.
        if (File.Exists(forms)) File.Move(forms, Path.Combine(archive, "forms.txt"), overwrite: false);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Atomic(path, bytes);
        Atomic(Path.Combine(DirectoryPath, "root.config.json"), nextBytes);
        state = next;
        rootHash = Hash(nextBytes);
        return archive;
    }

    public static void ValidateLocation(string directory, WebFormsWizardProject project)
    {
        ValidateProject(project);
        ProtectInputs(Physical(directory), project);
    }

    private void ProtectInputs(WebFormsWizardProject project) => ProtectInputs(DirectoryPath, project);

    private static void ProtectInputs(string directory, WebFormsWizardProject project)
    {
        var roots = new[] { Directory.Exists(project.InputPath) ? project.InputPath : Path.GetDirectoryName(project.InputPath)!,
            project.WebRoot, project.PublishedRoot }.Where(path => path is not null);
        foreach (var input in roots)
            if (Inside(directory, Physical(input!)) || Inside(Physical(input!), directory)) throw Fail("CONFIG_OVERLAPS_INPUT");
        foreach (var file in project.PrimaryAssemblies.Concat(project.Dependencies))
            if (Inside(directory, Physical(file))) throw Fail("CONFIG_OVERLAPS_INPUT");
    }

    private static bool Inside(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    public static string Physical(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var segment in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null) current = info.ResolveLinkTarget(true)?.FullName ?? throw Fail("LINK_UNRESOLVED");
        }
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }

    private static void ValidateRoot(WebFormsWizardRoot state)
    {
        if (state.Revision < 0 || state.Projects is null || state.Projects.Length > 128) throw Fail("ROOT_INVALID");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in state.Projects)
        {
            if (item is null) throw Fail("ROOT_INVALID");
            Id(item.Id);
            if (!ids.Add(item.Id) || !Digest(item.ConfigSha256)) throw Fail("ROOT_INVALID");
        }
    }

    private static void ValidateProject(WebFormsWizardProject project)
    {
        Id(project.Id);
        if (project.ProjectMode is not ("projectless" or "solution" or "project" or "dll") ||
            project.FormsMode is not ("all" or "selected") ||
            project.Step is not ("forms" or "build" or "publication" or "dependencies" or "configuration" or "ready" or "running" or "failed" or "completed") ||
            project.Forms is null || project.PrimaryAssemblies is null || project.Dependencies is null ||
            project.Forms.Length > WebFormsWizardForms.MaxEntries || project.PrimaryAssemblies.Length > 128 || project.Dependencies.Length > 128 ||
            project.Forms.Any(item => string.IsNullOrWhiteSpace(item)) ||
            !Absolute(project.InputPath) || !Absolute(project.WebRoot) ||
            (project.PublishedRoot is not null && !Absolute(project.PublishedRoot)) ||
            project.PrimaryAssemblies.Concat(project.Dependencies).Any(path => !Absolute(path))) throw Fail("PROJECT_INVALID");
        // Step is a cursor only. It never exempts these paths or their evidence from revalidation.
    }

    private static bool Absolute(string? path) => !string.IsNullOrWhiteSpace(path) && !path.Any(char.IsControl) && Path.IsPathFullyQualified(path);
    private static bool Digest(string? value) => value is not null && Regex.IsMatch(value, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant);
    private static void Id(string? id)
    {
        if (id is null || !Regex.IsMatch(id, "\\A[a-z][a-z0-9-]{0,47}\\z", RegexOptions.CultureInvariant) ||
            id is "runs" or "project-history" or "con" or "prn" or "aux" or "nul" || Regex.IsMatch(id, "\\A(com|lpt)[0-9]\\z")) throw Fail("PROJECT_ID_INVALID");
    }

    private static byte[] Encode<T>(string schema, string generator, T configuration)
    {
        var input = JsonSerializer.SerializeToUtf8Bytes(configuration, Json);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new WebFormsWizardDocument<T>(schema, RuleId, "local-only", generator, Hash(input), configuration), Json);
        if (bytes.Length > MaxBytes) throw Fail("CONFIG_LIMIT");
        return bytes;
    }

    private static T Read<T>(byte[] bytes, string schema)
    {
        using var parsed = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 24 });
        Duplicates(parsed.RootElement);
        var document = JsonSerializer.Deserialize<WebFormsWizardDocument<T>>(bytes, Json) ?? throw Fail("CONFIG_INVALID");
        if (document.SchemaVersion != schema || document.RuleId != RuleId || document.Visibility != "local-only" ||
            document.Configuration is null || !Digest(document.GeneratorSha256) ||
            document.BoundedInputSha256 != Hash(JsonSerializer.SerializeToUtf8Bytes(document.Configuration, Json))) throw Fail("CONFIG_INVALID");
        return document.Configuration;
    }

    private static void Duplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Fail("DUPLICATE_PROPERTY");
                Duplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Duplicates(item);
    }

    private static byte[] ReadBytes(string path, bool allowEmpty = false)
    {
        RejectLink(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxBytes || (!allowEmpty && stream.Length == 0)) throw Fail("CONFIG_LIMIT");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw Fail("CONFIG_CHANGED");
        return bytes;
    }

    private static void Atomic(string path, byte[] bytes)
    {
        RejectLink(path);
        var staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(flushToDisk: true); }
            File.Move(staging, path, overwrite: true);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }

    // A create interrupted after writing project.config.json but before the root
    // registration leaves only that file (and Atomic temp files). It carries no
    // registered state, so it may be replaced; anything else stays fail-closed.
    private static bool IsAbandonedRegistration(string folder) =>
        Directory.EnumerateDirectories(folder).FirstOrDefault() is null
        && Directory.EnumerateFiles(folder).All(file =>
        {
            var name = Path.GetFileName(file);
            return (name == "project.config.json" || name.StartsWith("project.config.json.", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal))
                && new FileInfo(file).LinkTarget is null;
        });

    private static void RejectLink(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (info.LinkTarget is not null) throw Fail("CONFIG_LINKED");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static InvalidOperationException Fail(string code) => new("WEBFORMS_WIZARD_" + code);
    public void Dispose() { disposed = true; lease.Dispose(); }
}
