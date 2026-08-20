using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

public sealed record TargetFrameworkInfo(string ProjectPath, string TargetFramework, int Line);

public sealed record PackageReferenceInfo(
    string ProjectPath,
    string PackageName,
    string? Version,
    int Line,
    string ManifestKind,
    string DependencyGroup,
    string DependencyScope,
    string? TargetFramework);

public sealed record NuGetLockfileEntry(
    string LockfilePath,
    string PackageName,
    string? ResolvedVersion,
    string? ResolvedVersionHash,
    string DependencyRelation,
    string TargetFramework,
    int Line,
    int DependencyCount,
    string? DependencyNames,
    string LockfileHash);

public sealed record NuGetLockfileGap(string LockfilePath, string Category, string Message, int Line);

public sealed record NuGetLockfileReadResult(
    IReadOnlyList<NuGetLockfileEntry> Entries,
    IReadOnlyList<NuGetLockfileGap> Gaps);

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
            yield return new PackageReferenceInfo(
                relativePath,
                packageName,
                version,
                GetLine(element),
                "csproj",
                "PackageReference",
                "runtime",
                null);
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

            yield return new PackageReferenceInfo(
                relativePath,
                packageName,
                AttributeValue(element, "version"),
                GetLine(element),
                "packages.config",
                "packages.config",
                "runtime",
                AttributeValue(element, "targetFramework"));
        }
    }

    /// <summary>
    /// Reads checked-in NuGet packages.lock.json files offline. The lockfile's contentHash field is
    /// intentionally never captured: it is not a registry artifact digest, so no artifactDigest
    /// property is ever emitted from this reader.
    /// </summary>
    public static NuGetLockfileReadResult ReadNuGetLockfiles(string repoPath, IEnumerable<FileInventoryItem> inventory)
    {
        var entries = new List<NuGetLockfileEntry>();
        var gaps = new List<NuGetLockfileGap>();
        foreach (var lockfile in inventory.Where(item => item.Kind == "PackagesLock").OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(Path.Combine(repoPath, lockfile.RelativePath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                gaps.Add(new NuGetLockfileGap(lockfile.RelativePath, "packages-lock-read-failed", "packages.lock.json could not be read; resolved-version evidence is unavailable.", 1));
                continue;
            }

            var lockfileHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..32];
            var lineCursor = new LockfileLineCursor(bytes);
            try
            {
                ReadNuGetLockfile(lockfile.RelativePath, lockfileHash, bytes, lineCursor, entries, gaps);
            }
            catch (JsonException)
            {
                gaps.Add(new NuGetLockfileGap(lockfile.RelativePath, "packages-lock-parse", "packages.lock.json is malformed or truncated JSON; resolved-version evidence is partial.", 1));
            }
        }

        return new NuGetLockfileReadResult(
            entries
                .OrderBy(entry => entry.LockfilePath, StringComparer.Ordinal)
                .ThenBy(entry => entry.TargetFramework, StringComparer.Ordinal)
                .ThenBy(entry => entry.PackageName, StringComparer.Ordinal)
                .ThenBy(entry => entry.ResolvedVersion ?? string.Empty, StringComparer.Ordinal)
                .ToArray(),
            gaps
                .OrderBy(gap => gap.LockfilePath, StringComparer.Ordinal)
                .ThenBy(gap => gap.Line)
                .ThenBy(gap => gap.Category, StringComparer.Ordinal)
                .ToArray());
    }

    private static void ReadNuGetLockfile(
        string relativePath,
        string lockfileHash,
        byte[] bytes,
        LockfileLineCursor lineCursor,
        List<NuGetLockfileEntry> entries,
        List<NuGetLockfileGap> gaps)
    {
        var reader = new Utf8JsonReader(bytes, isFinalBlock: true, default);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("packages.lock.json must contain a JSON object.");

        var sawDependencies = false;
        var lockfileVersion = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("packages.lock.json has an unexpected top-level token.");

            var propertyName = reader.GetString();
            if (propertyName == "version")
            {
                if (!reader.Read() || reader.TokenType is not (JsonTokenType.Number or JsonTokenType.String) || !TryReadLockfileVersion(ref reader, out lockfileVersion))
                    throw new JsonException("packages.lock.json version must be an integer.");
            }
            else if (propertyName == "dependencies")
            {
                sawDependencies = true;
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("packages.lock.json dependencies must be an object.");
                ReadNuGetLockfileDependencies(relativePath, lockfileHash, ref reader, lineCursor, entries, gaps);
            }
            else
            {
                if (!reader.Read())
                    throw new JsonException("packages.lock.json ended unexpectedly.");
                SkipLockfileValue(ref reader);
            }
        }

        if (lockfileVersion is not (1 or 2))
        {
            entries.RemoveAll(entry => entry.LockfilePath == relativePath);
            gaps.Add(new NuGetLockfileGap(relativePath, "packages-lock-unsupported", $"packages.lock.json schema version {lockfileVersion} is unsupported; resolved-version evidence is unavailable.", 1));
            return;
        }

        if (!sawDependencies)
            gaps.Add(new NuGetLockfileGap(relativePath, "packages-lock-unsupported", "packages.lock.json did not declare a dependencies object; resolved-version evidence is unavailable.", 1));
    }

    private static bool TryReadLockfileVersion(ref Utf8JsonReader reader, out int version)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.TryGetInt32(out version);
        var raw = reader.GetString();
        return int.TryParse(raw, out version);
    }

    private static void ReadNuGetLockfileDependencies(
        string relativePath,
        string lockfileHash,
        ref Utf8JsonReader reader,
        LockfileLineCursor lineCursor,
        List<NuGetLockfileEntry> entries,
        List<NuGetLockfileGap> gaps)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("packages.lock.json dependencies has an unexpected token.");

            var targetFramework = reader.GetString();
            if (!reader.Read())
                throw new JsonException("packages.lock.json ended unexpectedly.");
            if (reader.TokenType != JsonTokenType.StartObject || !IsSafeLockfileTargetFramework(targetFramework))
            {
                gaps.Add(new NuGetLockfileGap(relativePath, "packages-lock-group-unsupported", "packages.lock.json target-framework group is unsupported; its resolved-version evidence is unavailable.", lineCursor.LineAt(reader.TokenStartIndex)));
                SkipLockfileValue(ref reader);
                continue;
            }

            ReadNuGetLockfileGroup(relativePath, lockfileHash, targetFramework!, ref reader, lineCursor, entries, gaps);
        }
    }

    private static void ReadNuGetLockfileGroup(
        string relativePath,
        string lockfileHash,
        string targetFramework,
        ref Utf8JsonReader reader,
        LockfileLineCursor lineCursor,
        List<NuGetLockfileEntry> entries,
        List<NuGetLockfileGap> gaps)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("packages.lock.json dependency group has an unexpected token.");

            var packageName = reader.GetString();
            var line = lineCursor.LineAt(reader.TokenStartIndex);
            if (!reader.Read())
                throw new JsonException("packages.lock.json ended unexpectedly.");
            if (reader.TokenType != JsonTokenType.StartObject || !IsSafeNuGetPackageId(packageName))
            {
                gaps.Add(new NuGetLockfileGap(relativePath, "packages-lock-entry-unsafe", "packages.lock.json entry is malformed or has an unsafe package identity; its evidence is omitted.", line));
                SkipLockfileValue(ref reader);
                continue;
            }

            ReadNuGetLockfileEntry(relativePath, lockfileHash, targetFramework, packageName!, line, ref reader, lineCursor, entries, gaps);
        }
    }

    private static void ReadNuGetLockfileEntry(
        string relativePath,
        string lockfileHash,
        string targetFramework,
        string packageName,
        int line,
        ref Utf8JsonReader reader,
        LockfileLineCursor lineCursor,
        List<NuGetLockfileEntry> entries,
        List<NuGetLockfileGap> gaps)
    {
        string? type = null;
        string? resolved = null;
        List<string>? dependencyNames = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("packages.lock.json package entry has an unexpected token.");

            var propertyName = reader.GetString();
            if (!reader.Read())
                throw new JsonException("packages.lock.json ended unexpectedly.");
            if (propertyName == "type" && reader.TokenType == JsonTokenType.String)
            {
                type = reader.GetString();
            }
            else if (propertyName == "resolved" && reader.TokenType == JsonTokenType.String)
            {
                resolved = reader.GetString()?.Trim();
            }
            else if (propertyName == "dependencies" && reader.TokenType == JsonTokenType.StartObject)
            {
                dependencyNames = ReadNuGetLockfileEntryDependencies(ref reader);
            }
            else
            {
                // contentHash, requested, and unknown fields are deliberately ignored: contentHash is
                // package-content metadata, not a registry artifact digest, and is never emitted.
                SkipLockfileValue(ref reader);
            }
        }

        if (string.IsNullOrWhiteSpace(resolved))
        {
            gaps.Add(new NuGetLockfileGap(relativePath, "packages-lock-entry-resolved-missing", $"packages.lock.json entry for a package in {targetFramework} did not provide a resolved version.", line));
            return;
        }

        var dependencyRelation = type?.Trim().ToLowerInvariant() switch
        {
            "direct" => "direct",
            "transitive" => "transitive",
            _ => "unknown"
        };
        var safeDependencyNames = (dependencyNames ?? [])
            .Where(IsSafeNuGetPackageId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var dependencyNamesJoined = safeDependencyNames.Length == 0 || safeDependencyNames.Sum(name => name.Length + 1) > 256
            ? null
            : string.Join(',', safeDependencyNames);
        var unsafeResolved = !IsSafeNuGetResolvedVersion(resolved);
        entries.Add(new NuGetLockfileEntry(
            relativePath,
            packageName,
            unsafeResolved ? null : resolved,
            unsafeResolved ? FactFactory.Hash(resolved, 32) : null,
            dependencyRelation,
            targetFramework,
            line,
            safeDependencyNames.Length,
            dependencyNamesJoined,
            lockfileHash));
    }

    private static List<string> ReadNuGetLockfileEntryDependencies(ref Utf8JsonReader reader)
    {
        var names = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("packages.lock.json entry dependencies has an unexpected token.");
            names.Add(reader.GetString() ?? string.Empty);
            if (!reader.Read())
                throw new JsonException("packages.lock.json ended unexpectedly.");
            SkipLockfileValue(ref reader);
        }

        return names;
    }

    private static void SkipLockfileValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not (JsonTokenType.StartObject or JsonTokenType.StartArray))
            return;

        var depth = 1;
        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                depth++;
            else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
                if (depth == 0)
                    break;
            }
        }
    }

    private static bool IsSafeNuGetPackageId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 128
            && !value.Contains("..", StringComparison.Ordinal)
            && SafeNuGetPackageIdPattern.IsMatch(value);
    }

    private static bool IsSafeLockfileTargetFramework(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 128
            && SafeLockfileTargetFrameworkPattern.IsMatch(value);
    }

    private static bool IsSafeNuGetResolvedVersion(string value)
    {
        return value.Length is > 0 and <= 128
            && SafeNuGetResolvedVersionPattern.IsMatch(value);
    }

    private sealed class LockfileLineCursor(byte[] bytes)
    {
        private long _offset;
        private int _line = 1;

        public int LineAt(long offset)
        {
            if (offset < _offset)
                return _line;
            for (; _offset < offset && _offset < bytes.Length; _offset++)
            {
                if (bytes[_offset] == (byte)'\n')
                    _line++;
            }

            return _line;
        }
    }

    private static readonly Regex SafeNuGetPackageIdPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex SafeLockfileTargetFrameworkPattern = new("^[A-Za-z0-9][A-Za-z0-9._/+,-]{0,127}$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex SafeNuGetResolvedVersionPattern = new("^[0-9]+(?:\\.[0-9]+){0,3}(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\\+[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

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
