using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TraceMap.Core;

// Local environment evidence, never a compilation receipt. Rechecked against
// the current source/configuration/provider bytes before any enrichment.
internal sealed record WebFormsVbImportContext(string Namespaces, string ReceiptSha256,
    string GeneratorSha256, string InputSha256, IReadOnlySet<string> SourcePaths)
{
    internal const string Schema = "webforms-vb-import-context.v1";
    internal const string Kind = "current-environment-not-build-proof";

    internal static WebFormsVbImportContext Read(string repoPath, string commitSha, string receiptPath,
        IReadOnlyList<FileInventoryItem> inventory)
        => ReadWithTrustedProvider(repoPath, commitSha, receiptPath, inventory, ResolveFrameworkProvider);

    // Test seam supplies an independently trusted provider, never a receipt field.
    internal static WebFormsVbImportContext ReadWithTrustedProvider(string repoPath, string commitSha, string receiptPath,
        IReadOnlyList<FileInventoryItem> inventory, Func<string> resolveProvider)
    {
        string? receiptHash = null;
        var scope = "receipt";
        var category = "malformed";
        try
        {
            var bytes = ReadBounded(Path.GetFullPath(receiptPath, repoPath), 1_048_576);
            receiptHash = Hash(bytes);
            using var envelope = Parse(bytes);
            var outer = envelope.RootElement;
            Require(Text(outer, "schemaVersion") == Schema && Text(outer, "visibility") == "local-only");
            var generator = Text(outer, "generatorSha256");
            var inputHash = Text(outer, "boundedInputSha256");
            var payload = Text(outer, "payloadJson");
            Require(IsHash(generator) && IsHash(inputHash) && Hash(Encoding.UTF8.GetBytes(payload)) == inputHash);
            using var document = Parse(Encoding.UTF8.GetBytes(payload));
            var root = document.RootElement;
            category = "malformed";
            Require(Text(root, "evidenceKind") == Kind && Text(root, "mappingKind") == "standalone-root"
                && Version.TryParse(Text(root, "runtimeVersion"), out var runtimeVersion) && runtimeVersion.Major == 4
                && Text(root, "processArchitecture") is "x86" or "x64");
            category = "stale";
            Require(commitSha.Length == 40 && commitSha.All(Uri.IsHexDigit)
                && Text(root, "sourceCommitSha") == commitSha && Text(root, "sourceRootPathSha256") == RootHash(repoPath));
            scope = "provider"; category = "provider-mismatch";
            Require(Text(root, "providerType") == "Microsoft.VisualBasic.VBCodeProvider");
            var trustedProvider = Path.GetFullPath(resolveProvider());
            var offeredProvider = Text(root, "providerAssemblyPath");
            Require(Path.IsPathFullyQualified(offeredProvider) && string.Equals(Path.GetFullPath(offeredProvider), trustedProvider,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            VerifyFile(trustedProvider, Text(root, "providerAssemblySha256"), 64 * 1024 * 1024);
            scope = "configuration"; category = "stale";
            var configs = Array(root, "configurationFiles", 1, 16);
            var configPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in configs)
            {
                var path = Text(item, "path");
                Require(Path.IsPathFullyQualified(path) && configPaths.Add(Path.GetFullPath(path)));
                VerifyFile(path, Text(item, "sha256"), 2 * 1024 * 1024);
            }
            // A newly-created root config or nested App_Code config must not evade
            // the captured chain just because it did not exist at capture time.
            var siteConfig = Path.Combine(Path.GetFullPath(repoPath), "Web.config");
            Require(!File.Exists(siteConfig) || configPaths.Contains(siteConfig));
            Require(!inventory.Any(f => f.RelativePath.StartsWith("App_Code/", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(f.RelativePath).Equals("web.config", StringComparison.OrdinalIgnoreCase)));
            // Config-only contexts cannot be applied across source files or roots.
            var sources = Array(root, "sourceFiles", 1, 256);
            scope = "source"; category = "out-of-scope";
            var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inventoried = inventory.Select(f => f.RelativePath).ToHashSet(StringComparer.Ordinal);
            long sourceBytes = 0;
            foreach (var item in sources)
            {
                var path = Text(item, "path");
                category = path.Split('/').Contains("..") || Path.IsPathRooted(path) ? "escaped" : "out-of-scope";
                Require(path.StartsWith("App_Code/", StringComparison.OrdinalIgnoreCase)
                    && path.EndsWith(".vb", StringComparison.OrdinalIgnoreCase)
                    && !Path.IsPathRooted(path) && !path.Contains('\\') && !path.Contains(':')
                    && path.Split('/').All(p => p.Length > 0 && p is not "." and not "..")
                    && sourcePaths.Add(path) && inventoried.Contains(path));
                var fullPath = Path.GetFullPath(Path.Combine(repoPath, path));
                Require(fullPath.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(repoPath)) + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
                category = "stale";
                sourceBytes += VerifyFile(fullPath, Text(item, "sha256"), 64 * 1024 * 1024);
                Require(sourceBytes <= 64 * 1024 * 1024);
            }
            scope = "namespaces"; category = "malformed";
            var names = Array(root, "namespaces", 1, 128).Select(e => e.GetString() ?? "").ToArray();
            Require(names.All(n => n.Length is > 0 and <= 512 && n.Split('.').All(p => p.Length > 0
                && (char.IsLetter(p[0]) || p[0] == '_') && p.All(c => char.IsLetterOrDigit(c) || c == '_')))
                && names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Length);
            return new(string.Join(';', names.Order(StringComparer.Ordinal)), Hash(bytes), generator, inputHash, sourcePaths);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or KeyNotFoundException or ArgumentException or BadImageFormatException)
        {
            throw new ImportContextRejectedException(receiptHash, scope,
                ex is JsonException or KeyNotFoundException or InvalidOperationException ? "malformed"
                : ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException ? "unavailable" : category);
        }
    }

    private static string ResolveFrameworkProvider()
    {
        // The capture lane admits only the OS .NET Framework 4 System provider.
        // Do not Assembly.Load an offered receipt path in the net10 scanner.
        if (!OperatingSystem.IsWindows()) throw new InvalidDataException("Provider platform unavailable.");
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Require(!string.IsNullOrEmpty(windows));
        var path = Path.Combine(windows, "Microsoft.NET", "assembly", "GAC_MSIL", "System",
            "v4.0_4.0.0.0__b77a5c561934e089", "System.dll");
        var bytes = ReadBounded(path, 64 * 1024 * 1024);
        using var stream = new MemoryStream(bytes, writable: false);
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(stream);
        Require(module.Assembly.Name.Name == "System" && module.Assembly.Name.Version == new Version(4, 0, 0, 0)
            && Convert.ToHexStringLower(module.Assembly.Name.PublicKeyToken) == "b77a5c561934e089"
            && module.Types.Any(t => t.FullName == "Microsoft.VisualBasic.VBCodeProvider"));
        return path;
    }

    internal static string RootHash(string path) => Hash(Encoding.UTF8.GetBytes(
        Path.GetFullPath(path).TrimEnd('/', '\\').Replace('\\', '/').ToUpperInvariant()));
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool IsHash(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Require(bool value) { if (!value) throw new InvalidDataException("VB import context validation failed."); }
    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? "";
    private static JsonElement[] Array(JsonElement value, string name, int min, int max)
    {
        var array = value.GetProperty(name);
        Require(array.ValueKind == JsonValueKind.Array && array.GetArrayLength() >= min && array.GetArrayLength() <= max);
        return array.EnumerateArray().ToArray();
    }
    private static JsonDocument Parse(byte[] bytes)
    {
        var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        try { CheckUnique(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static void CheckUnique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject()) { Require(names.Add(property.Name)); CheckUnique(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckUnique(item);
    }
    private static long VerifyFile(string path, string sha256, int limit)
    {
        Require(Path.IsPathFullyQualified(path) && IsHash(sha256));
        var bytes = ReadBounded(path, limit);
        Require(Hash(bytes) == sha256);
        return bytes.Length;
    }
    private static byte[] ReadBounded(string path, int limit)
    {
        // Do not follow a substituted link in any component of an evidence path.
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0);
        using var stream = File.OpenRead(path);
        Require(stream.Length <= limit);
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1);
        return bytes;
    }
}

internal sealed class ImportContextRejectedException(string? receiptSha256, string scope, string category)
    : IOException("VB import context rejected.")
{
    internal string? ReceiptSha256 { get; } = receiptSha256;
    internal string Scope { get; } = scope;
    internal string Category { get; } = category;
}
