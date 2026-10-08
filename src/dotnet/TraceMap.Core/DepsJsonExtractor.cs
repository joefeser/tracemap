using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TraceMap.Core;

public sealed record DepsJsonLimits(int MaxFiles = 128, int MaxFileBytes = 8_388_608,
    long MaxTotalBytes = 67_108_864, int MaxDirectoryEntries = 100_000, int MaxLibraries = 20_000);

internal sealed record DepsJsonRow(string Path, string Target, string Package, string Version,
    string Relation, string Hash, string Location, int EndLine);
internal sealed record DepsJsonGap(string Path, string Kind);
internal sealed record DepsJsonResult(IReadOnlyList<DepsJsonRow> Rows, IReadOnlyList<DepsJsonGap> Gaps,
    string GeneratorSha256, string BoundedInputSha256);

/// Reads observed build output separately from the immutable source inventory. No builds or loads.
internal static class DepsJsonExtractor
{
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
        { ".git", ".tracemap", ".nuget", "obj", "node_modules" };
    private static readonly Regex Name = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Version = new("^[0-9]+(?:\\.[0-9]+){0,3}(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\\+[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Target = new("^[A-Za-z0-9.][A-Za-z0-9._/+,=\\-]{0,255}\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    internal static DepsJsonResult Read(ScanOptions options, CancellationToken cancellationToken)
    {
        var limits = options.DepsJsonLimits ?? new DepsJsonLimits();
        if (limits.MaxFiles <= 0 || limits.MaxFileBytes <= 0 || limits.MaxTotalBytes <= 0
            || limits.MaxDirectoryEntries <= 0 || limits.MaxLibraries <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "deps.json limits must be positive.");
        var root = Path.GetFullPath(options.RepoPath);
        var output = Path.GetFullPath(options.OutputPath).TrimEnd(Path.DirectorySeparatorChar);
        var comparer = CSharpSemanticExtractor.CreateSourcePathComparer(root);
        var projectDirectories = (options.ProjectPaths ?? []).Select(project => Path.GetDirectoryName(
            Path.GetRelativePath(root, Path.GetFullPath(project, root)))!.Replace('\\', '/')).ToArray();
        var rows = new List<DepsJsonRow>();
        var gaps = new List<DepsJsonGap>();
        var observed = new List<string>();
        var files = 0;
        var work = 0;
        long bytesRead = 0;
        var pending = new Stack<(string Path, bool InBin)>();
        pending.Push((root, false));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, inBin) = pending.Pop();
            var relativeDirectory = Path.GetRelativePath(root, directory).Replace('\\', '/');
            try
            {
                // Never follow directory links, including the explicitly supplied repository root.
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    gaps.Add(new(relativeDirectory, "deps-json-linked-path"));
                    continue;
                }
                var entries = new List<string>();
                foreach (var item in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++work > limits.MaxDirectoryEntries)
                    {
                        gaps.Add(new(".", "deps-json-discovery-limit"));
                        pending.Clear();
                        break;
                    }
                    entries.Add(item);
                }
                // A truncated directory is not treated as an exhaustive inventory.
                if (work > limits.MaxDirectoryEntries) break;
                foreach (var path in entries.Order(StringComparer.Ordinal))
                {
                    var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                    if (comparer.Equals(path.TrimEnd(Path.DirectorySeparatorChar), output)
                        || (options.ExcludeGlobs ?? []).Any(glob => ScanEngine.GlobMatches(relative, glob, comparer)))
                        continue;
                    var attrs = File.GetAttributes(path);
                    var isDirectory = (attrs & FileAttributes.Directory) != 0;
                    if (isDirectory && Excluded.Contains(Path.GetFileName(path))) continue;
                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                    {
                        gaps.Add(new(relative, "deps-json-linked-path"));
                        continue;
                    }
                    if (isDirectory)
                    {
                        pending.Push((path, inBin || Path.GetFileName(path).Equals("bin", StringComparison.OrdinalIgnoreCase)));
                        continue;
                    }
                    if (!inBin || !path.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)) continue;
                    if ((options.IncludeGlobs?.Count ?? 0) > 0
                        && !options.IncludeGlobs!.Any(glob => ScanEngine.GlobMatches(relative, glob, comparer))) continue;
                    if (projectDirectories.Length > 0 && !projectDirectories.Any(dir => string.IsNullOrEmpty(dir)
                        || comparer.Equals(dir, ".") || relative.StartsWith(dir + "/", comparer == StringComparer.OrdinalIgnoreCase
                            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))) continue;
                    if (++files > limits.MaxFiles)
                    {
                        gaps.Add(new(".", "deps-json-file-limit"));
                        pending.Clear();
                        break;
                    }
                    try
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        if (stream.Length > limits.MaxFileBytes || stream.Length > limits.MaxTotalBytes - bytesRead)
                        {
                            gaps.Add(new(relative, "deps-json-byte-limit"));
                            continue;
                        }
                        // Bounded read even if another writer grows the file on a platform without mandatory locks.
                        var bytes = new byte[checked((int)stream.Length)];
                        bytesRead += bytes.Length;
                        stream.ReadExactly(bytes);
                        if (stream.ReadByte() != -1) throw new IOException("Changed build output");
                        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                        observed.Add(relative + "\0" + hash);
                        Parse(relative, bytes, hash, limits, rows, gaps);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        gaps.Add(new(relative, "deps-json-read-failed"));
                    }
                    catch (Exception ex) when (ex is JsonException or FormatException)
                    {
                        gaps.Add(new(relative, "deps-json-invalid"));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                gaps.Add(new(relativeDirectory, "deps-json-discovery-failed"));
            }
        }
        if (files == 0 && gaps.Count == 0) gaps.Add(new(".", "deps-json-not-found"));
        using var generator = File.OpenRead(typeof(DepsJsonExtractor).Assembly.Location);
        // The signature includes bounded reads and categorical omissions. It is not a source snapshot.
        var signature = JsonSerializer.Serialize(new { observed = observed.Order(StringComparer.Ordinal),
            gaps = gaps.OrderBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.Kind, StringComparer.Ordinal), limits });
        return new(rows.OrderBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.Target, StringComparer.Ordinal)
            .ThenBy(x => x.Package, StringComparer.Ordinal).ToArray(), gaps,
            Convert.ToHexString(SHA256.HashData(generator)).ToLowerInvariant(), FactFactory.Hash(signature, 64));
    }

    private static void Parse(string path, byte[] bytes, string hash, DepsJsonLimits limits,
        List<DepsJsonRow> rows, List<DepsJsonGap> gaps)
    {
        using var doc = JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? 3 : 0),
            new JsonDocumentOptions { MaxDepth = 64 });
        RejectDuplicates(doc.RootElement);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("targets", out var targets)
            || targets.ValueKind != JsonValueKind.Object || !root.TryGetProperty("libraries", out var libraries)
            || libraries.ValueKind != JsonValueKind.Object) throw new FormatException();
        if (!targets.EnumerateObject().Any()) throw new FormatException();
        var endLine = 1 + bytes.Count(b => b == (byte)'\n');
        var staged = new List<DepsJsonRow>();
        var stagedGaps = new List<DepsJsonGap>();
        var count = 0;
        foreach (var target in targets.EnumerateObject())
        {
            if (!Target.IsMatch(target.Name) || target.Value.ValueKind != JsonValueKind.Object) throw new FormatException();
            var entries = new Dictionary<string, (string Name, string Version, string Type, JsonElement Value)>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in target.Value.EnumerateObject())
            {
                if (++count > limits.MaxLibraries)
                {
                    gaps.Add(new(path, "deps-json-library-limit"));
                    return; // No partially classified graph.
                }
                var split = entry.Name.LastIndexOf('/');
                if (split < 1 || entry.Name.Length - split - 1 > 128 || !Name.IsMatch(entry.Name[..split])
                    || !Version.IsMatch(entry.Name[(split + 1)..]) || entry.Value.ValueKind != JsonValueKind.Object
                    || !libraries.TryGetProperty(entry.Name, out var library) || library.ValueKind != JsonValueKind.Object
                    || !library.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String
                    || !names.Add(entry.Name[..split])) throw new FormatException();
                if (entry.Value.TryGetProperty("version", out var explicitVersion)
                    && (explicitVersion.ValueKind != JsonValueKind.String || explicitVersion.GetString() != entry.Name[(split + 1)..])) throw new FormatException();
                if (library.TryGetProperty("version", out var libraryVersion)
                    && (libraryVersion.ValueKind != JsonValueKind.String || libraryVersion.GetString() != entry.Name[(split + 1)..])) throw new FormatException();
                entries.Add(entry.Name, (entry.Name[..split], entry.Name[(split + 1)..], type.GetString()!, entry.Value));
            }
            var edges = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var incoming = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var completeGraph = true;
            foreach (var (key, entry) in entries)
            {
                var deps = new List<string>();
                if (entry.Value.TryGetProperty("dependencies", out var dependencies))
                {
                    if (dependencies.ValueKind != JsonValueKind.Object) throw new FormatException();
                    foreach (var dep in dependencies.EnumerateObject())
                    {
                        if (dep.Value.ValueKind != JsonValueKind.String) throw new FormatException();
                        var depKey = dep.Name + "/" + dep.Value.GetString();
                        if (!entries.ContainsKey(depKey)) completeGraph = false;
                        else { deps.Add(depKey); incoming.Add(depKey); }
                    }
                }
                edges.Add(key, deps);
            }
            var roots = entries.Where(x => x.Value.Type == "project" && !incoming.Contains(x.Key)).Select(x => x.Key).ToArray();
            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var direct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (roots.Length == 1 && completeGraph)
            {
                direct.UnionWith(edges[roots[0]]);
                var queue = new Queue<string>(direct);
                while (queue.TryDequeue(out var key))
                    if (reachable.Add(key)) foreach (var dep in edges[key]) queue.Enqueue(dep);
            }
            var unknown = false;
            foreach (var (key, entry) in entries)
            {
                if (entry.Type == "project") continue;
                if (entry.Type != "package") { stagedGaps.Add(new(path, "deps-json-library-type-unsupported")); continue; }
                var relation = direct.Contains(key) ? "direct" : reachable.Contains(key) ? "transitive" : "unknown";
                unknown |= relation == "unknown";
                staged.Add(new(path, target.Name, entry.Name, entry.Version, relation, hash,
                    "/targets/" + Pointer(target.Name) + "/" + Pointer(key), endLine));
            }
            if (unknown) stagedGaps.Add(new(path, "deps-json-relation-unproven"));
        }
        rows.AddRange(staged);
        gaps.AddRange(stagedGaps.Distinct());
    }

    private static string Pointer(string value) => value.Replace("~", "~0").Replace("/", "~1");
    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in value.EnumerateObject())
            {
                if (!names.Add(p.Name)) throw new FormatException();
                RejectDuplicates(p.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicates(item);
    }

    internal static IEnumerable<CodeFact> Materialize(ScanManifest manifest, DepsJsonResult result)
    {
        foreach (var row in result.Rows)
            yield return FactFactory.Create(manifest, FactTypes.PackageReferenced, RuleIds.ProjectFile, EvidenceTiers.Tier2Structural,
                new EvidenceSpan(row.Path, 1, row.EndLine, null, "DepsJsonExtractor", ScannerVersions.DepsJsonExtractor), targetSymbol: row.Package,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["manifestKind"] = "deps.json", ["manifestPath"] = row.Path, ["manifestSha256"] = row.Hash,
                    ["generatorSha256"] = result.GeneratorSha256, ["boundedInputSha256"] = result.BoundedInputSha256,
                    ["metadataLocation"] = row.Location, ["evidenceSource"] = "build-output", ["sourceKind"] = "build-output",
                    ["dependencyGroup"] = "build-output", ["dependencyRelation"] = row.Relation,
                    ["relationBasis"] = "emitted-target-graph", ["freshness"] = "unknown", ["buildCommitSha"] = "unknown",
                    ["closureScope"] = "emitted-target-libraries", ["ecosystem"] = "nuget", ["packageManager"] = "nuget",
                    ["package"] = row.Package, ["packageName"] = row.Package, ["resolvedVersion"] = row.Version,
                    ["version"] = row.Version, ["targetFramework"] = row.Target, ["surfaceKind"] = "package-config"
                });
        foreach (var gap in result.Gaps.Distinct())
            yield return FactFactory.Create(manifest, FactTypes.AnalysisGap, RuleIds.ProjectFile, EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(gap.Path, 1, 1, null, "DepsJsonExtractor", ScannerVersions.DepsJsonExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gapKind"] = gap.Kind, ["message"] = "Build-output dependency evidence is partial or unavailable.",
                    ["evidenceSource"] = "build-output", ["generatorSha256"] = result.GeneratorSha256,
                    ["boundedInputSha256"] = result.BoundedInputSha256
                });
    }
}
