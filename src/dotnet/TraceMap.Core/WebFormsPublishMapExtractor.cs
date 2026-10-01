using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

public sealed record WebFormsPublishProvenance(
    string SchemaVersion,
    string GeneratorSha256,
    string BoundedInputSha256,
    string Status,
    IReadOnlyList<string> GapKinds,
    int SourceFileCount,
    int PublishedFileCount,
    int PageCount,
    string? PublishedRootPathHash = null,
    string? SourceRelativeBase = null);

/// <summary>Read-only receipt inspection through the scanner's authoritative policy. Not build or runtime proof.</summary>
public static class WebFormsPublishInputInspector
{
    public static WebFormsPublishProvenance? Inspect(ScanOptions options, string scanCommitSha,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RepoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(scanCommitSha);
        cancellationToken.ThrowIfCancellationRequested();
        return WebFormsPublishMapExtractor.Evaluate(Path.GetFullPath(options.RepoPath), scanCommitSha, options, cancellationToken).Provenance;
    }
}

internal sealed record WebFormsPublishPage(string SourcePath, string AssemblyName,
    string AssemblySha256, string GeneratedType, string MapSha256);
internal sealed record WebFormsPublishPageCandidate(string SourcePath);
internal sealed record WebFormsPublishAssembly(string Path, string Sha256);

internal sealed record WebFormsPublishEvaluation(WebFormsPublishProvenance? Provenance,
    IReadOnlyList<WebFormsPublishPage> Pages, IReadOnlyList<string> SourcePaths,
    IReadOnlyList<WebFormsPublishAssembly> Assemblies,
    IReadOnlyList<WebFormsPublishPageCandidate>? Candidates = null);

internal static partial class WebFormsPublishMapExtractor
{
    private const int MaxReceiptBytes = 1_048_576;
    private const int MaxMapBytes = 1_048_576;
    private const int MaxSourceFiles = 256;
    private const int MaxPublishedFiles = 64;
    private const int MaxPages = 32;
    private const long MaxArtifactBytes = 67_108_864;

    public static WebFormsPublishEvaluation Evaluate(string repoPath, string commitSha,
        ScanOptions options, CancellationToken cancellationToken)
        => EvaluateReceipt(repoPath, commitSha, options, cancellationToken, allowPartitionSet: true, allowInventoryPartition: false);

    private static WebFormsPublishEvaluation EvaluateReceipt(string repoPath, string commitSha,
        ScanOptions options, CancellationToken cancellationToken, bool allowPartitionSet, bool allowInventoryPartition)
    {
        if (string.IsNullOrWhiteSpace(options.WebFormsPublishReceiptPath))
            return new WebFormsPublishEvaluation(null, [], [], []);

        var generatorPath = typeof(WebFormsPublishMapExtractor).Assembly.Location;
        if (string.IsNullOrWhiteSpace(generatorPath) || !File.Exists(generatorPath))
            throw new InvalidOperationException("The exact Web Forms publish map generator bytes are unavailable.");
        using var generator = File.OpenRead(generatorPath);
        var generatorSha256 = Sha256(generator);
        var receiptPath = Path.GetFullPath(Path.IsPathRooted(options.WebFormsPublishReceiptPath)
            ? options.WebFormsPublishReceiptPath : Path.Combine(repoPath, options.WebFormsPublishReceiptPath));
        var publishRoot = Path.GetDirectoryName(receiptPath)!;
        var explicitPublishedRoot = options.WebFormsPublishedRootPath;
        var publishedRootHash = explicitPublishedRoot is null ? null : Sha256(Encoding.UTF8.GetBytes(explicitPublishedRoot));
        var gaps = new List<string>();
        var sourceBase = options.WebFormsPublishSourceRelativeBase;
        if (sourceBase == ".") sourceBase = null;
        var pages = new List<WebFormsPublishPage>();
        var candidates = new List<WebFormsPublishPageCandidate>();
        var sourcePaths = new List<string>();
        var assemblies = new List<WebFormsPublishAssembly>();
        var sourceCount = 0;
        var publishedCount = 0;
        var pageCount = 0;
        string boundedInputSha256;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sourceBase is not null) _ = ResolveChild(repoPath, SourceName(null, sourceBase));
            if (explicitPublishedRoot is not null)
            {
                if (!Path.IsPathFullyQualified(explicitPublishedRoot) || !Directory.Exists(explicitPublishedRoot)
                    || File.GetAttributes(explicitPublishedRoot).HasFlag(FileAttributes.ReparsePoint))
                    throw new PublishException("WebFormsPublishRootInvalid");
                publishRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(explicitPublishedRoot));
                publishedRootHash = Sha256(Encoding.UTF8.GetBytes(publishRoot));
            }
            var bytes = ReadBounded(receiptPath, MaxReceiptBytes);
            boundedInputSha256 = InputDigest(Sha256(bytes), publishedRootHash, sourceBase);
            using (var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 }))
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("schemaVersion", out var schema)
                    && schema.ValueKind == JsonValueKind.String && schema.GetString() == ReceiptSetSchema)
                {
                    if (!allowPartitionSet) throw new PublishException("WebFormsPublishNestedReceiptSet");
                    return EvaluateSet(repoPath, commitSha, options, cancellationToken, receiptPath,
                        publishRoot, bytes, generatorSha256, boundedInputSha256, publishedRootHash);
                }
            var receipt = JsonSerializer.Deserialize<PublishReceipt>(bytes,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 16 });
            var inventoryPartition = receipt?.SchemaVersion == InventoryPartitionSchema && allowInventoryPartition;
            if (receipt is null || (receipt.SchemaVersion != "webforms-publish-binding.v1" && !inventoryPartition)
                || receipt.Visibility != "local-only"
                || receipt.SourceCommitSha != commitSha
                || !IsSha256(receipt.ReceiptGeneratorSha256)
                || !IsSha256(receipt.CompilerSha256)
                || !IsSha256(receipt.BoundedInputSha256)
                || receipt.SourceFiles is null || receipt.PublishedFiles is null || receipt.Pages is null)
                throw new PublishException("WebFormsPublishReceiptInvalid");
            sourceCount = receipt.SourceFiles.Count;
            publishedCount = receipt.PublishedFiles.Count;
            pageCount = receipt.Pages.Count;
            if (sourceCount is < 1 or > MaxSourceFiles || publishedCount is < 1 or > MaxPublishedFiles
                || (inventoryPartition ? pageCount != 0 : pageCount is < 1 or > MaxPages))
                throw new PublishException("WebFormsPublishInputLimitExceeded");
            if (HasDuplicatePaths(receipt.SourceFiles.Select(item => item.Path))
                || HasDuplicatePaths(receipt.PublishedFiles.Select(item => item.Path))
                || HasDuplicatePaths(receipt.Pages.Select(item => item.VirtualPath)))
                throw new PublishException("WebFormsPublishReceiptAmbiguous");

            var inputLines = new List<string>();
            foreach (var item in receipt.SourceFiles.OrderBy(item => item.Path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsSha256(item.Sha256)) throw new PublishException("WebFormsPublishReceiptInvalid");
                var sourceName = SourceName(sourceBase, item.Path);
                var path = ResolveChild(repoPath, sourceName);
                if (Sha256(ReadBounded(path, MaxArtifactBytes)) != item.Sha256)
                    throw new PublishException("WebFormsPublishSourceMismatch");
                inputLines.Add($"{item.Path}:{item.Sha256}");
                sourcePaths.Add(sourceName);
            }
            var sourceDigest = Sha256(Encoding.UTF8.GetBytes(string.Join("\n", inputLines) + "\n"));
            if (sourceDigest != receipt.BoundedInputSha256)
                throw new PublishException("WebFormsPublishSourceMismatch");

            var published = new Dictionary<string, PublishedFile>(StringComparer.Ordinal);
            foreach (var item in receipt.PublishedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsSha256(item.Sha256) || item.Kind is not ("assembly" or "compiled-map"))
                    throw new PublishException("WebFormsPublishReceiptInvalid");
                var path = ResolveChild(publishRoot, item.Path);
                var limit = item.Kind == "compiled-map" ? MaxMapBytes : MaxArtifactBytes;
                if (Sha256(ReadBounded(path, limit)) != item.Sha256)
                    throw new PublishException("WebFormsPublishArtifactMismatch");
                published.Add(item.Path!, item);
                if (item.Kind == "assembly") assemblies.Add(new WebFormsPublishAssembly(item.Path!, item.Sha256!));
            }
            foreach (var item in receipt.Pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.BindingKind == "mapless-source-type-candidate")
                {
                    if (item.SourcePath is null || item.VirtualPath is null
                        || !item.VirtualPath.StartsWith("/", StringComparison.Ordinal)
                        || !item.VirtualPath.EndsWith("/" + item.SourcePath, StringComparison.OrdinalIgnoreCase)
                        || item.Assembly is not null || item.GeneratedType is not null || item.MapPath is not null)
                        throw new PublishException("WebFormsPublishReceiptInvalid");
                    _ = ResolveChild(repoPath, SourceName(sourceBase, item.SourcePath));
                    if (!receipt.SourceFiles.Any(source => source.Path == item.SourcePath))
                        throw new PublishException("WebFormsPublishSourceUnavailable");
                    if (!assemblies.Any(assembly => Path.GetFileName(assembly.Path).StartsWith("App_Web_", StringComparison.OrdinalIgnoreCase)))
                        throw new PublishException("WebFormsPublishAssemblyUnavailable");
                    var mapRows = receipt.PublishedFiles.Where(file => file.Kind == "compiled-map")
                        .OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
                    if (receipt.PublishedMapCount != mapRows.Length || !IsSha256(receipt.MapInventorySha256))
                        throw new PublishException("WebFormsPublishReceiptInvalid");
                    var mapLines = mapRows.Select(file => $"{file.Path}:{file.Sha256}");
                    if (Sha256(Encoding.UTF8.GetBytes(string.Join("\n", mapLines) + "\n")) != receipt.MapInventorySha256)
                        throw new PublishException("WebFormsPublishReceiptInvalid");
                    foreach (var mapRow in mapRows)
                    {
                        using var mapStream = File.OpenRead(ResolveChild(publishRoot, mapRow.Path));
                        using var mapReader = XmlReader.Create(mapStream,
                            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                        var virtualPath = XDocument.Load(mapReader).Root?.Attribute("virtualPath")?.Value;
                        if (virtualPath is null)
                            throw new PublishException("WebFormsPublishMapMismatch");
                        if (virtualPath.Equals(item.VirtualPath, StringComparison.OrdinalIgnoreCase)
                            || virtualPath.EndsWith("/" + item.SourcePath, StringComparison.OrdinalIgnoreCase))
                            throw new PublishException("WebFormsPublishMapMismatch");
                    }
                    candidates.Add(new WebFormsPublishPageCandidate(SourceName(sourceBase, item.SourcePath)));
                    continue;
                }
                if (item.BindingKind is not null)
                    throw new PublishException("WebFormsPublishReceiptInvalid");
                if (string.IsNullOrWhiteSpace(item.Assembly) || string.IsNullOrWhiteSpace(item.GeneratedType)
                    || item.VirtualPath is null || !item.VirtualPath.StartsWith("/", StringComparison.Ordinal))
                    throw new PublishException("WebFormsPublishReceiptInvalid");
                var sourcePath = item.SourcePath ?? item.VirtualPath.TrimStart('/');
                if (item.SourcePath is not null
                    && !item.VirtualPath.EndsWith("/" + sourcePath, StringComparison.OrdinalIgnoreCase))
                    throw new PublishException("WebFormsPublishReceiptInvalid");
                _ = ResolveChild(repoPath, SourceName(sourceBase, sourcePath));
                if (!receipt.SourceFiles.Any(source => source.Path == sourcePath))
                    throw new PublishException("WebFormsPublishSourceUnavailable");
                if (!published.TryGetValue(item.MapPath!, out var map) || map.Kind != "compiled-map")
                    throw new PublishException("WebFormsPublishMapUnavailable");
                var assemblyPath = "bin/" + item.Assembly + ".dll";
                if (!published.TryGetValue(assemblyPath, out var assembly) || assembly.Kind != "assembly")
                    throw new PublishException("WebFormsPublishAssemblyUnavailable");
                using var stream = File.OpenRead(ResolveChild(publishRoot, item.MapPath));
                using var reader = XmlReader.Create(stream,
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                var root = XDocument.Load(reader).Root;
                if (root?.Name.LocalName != "preserve"
                    || root.Attribute("virtualPath")?.Value != item.VirtualPath
                    || root.Attribute("assembly")?.Value != item.Assembly
                    || root.Attribute("type")?.Value != item.GeneratedType)
                    throw new PublishException("WebFormsPublishMapMismatch");
                pages.Add(new WebFormsPublishPage(SourceName(sourceBase, sourcePath), item.Assembly,
                    assembly.Sha256!, item.GeneratedType, map.Sha256!));
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (PublishException exception)
        {
            boundedInputSha256 = InputDigest(SafeReceiptDigest(receiptPath), publishedRootHash, sourceBase);
            gaps.Add(exception.GapKind);
            pages.Clear();
            candidates.Clear();
            sourcePaths.Clear();
            assemblies.Clear();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or JsonException or XmlException or ArgumentException or OverflowException)
        {
            boundedInputSha256 = InputDigest(SafeReceiptDigest(receiptPath), publishedRootHash, sourceBase);
            gaps.Add("WebFormsPublishReceiptUnreadable");
            pages.Clear();
            candidates.Clear();
            sourcePaths.Clear();
            assemblies.Clear();
        }
        var provenance = new WebFormsPublishProvenance("webforms-publish-provenance.v1",
            generatorSha256, boundedInputSha256, gaps.Count == 0 ? "bound" : "gap",
            gaps, sourceCount, publishedCount, pageCount, publishedRootHash, sourceBase);
        return new WebFormsPublishEvaluation(provenance, pages, sourcePaths, assemblies, candidates);
    }

    public static IReadOnlyList<CodeFact> MaterializeFacts(ScanManifest manifest,
        WebFormsPublishEvaluation evaluation)
    {
        if (evaluation.Provenance is not { } provenance) return [];
        var facts = new List<CodeFact>();
        foreach (var sourcePath in evaluation.SourcePaths)
            facts.Add(FactFactory.Create(manifest, FactTypes.WebFormsPublishSourceBound,
                RuleIds.LegacyWebFormsPublishMap, EvidenceTiers.Tier2Structural,
                new EvidenceSpan(sourcePath, 1, 1, null, nameof(WebFormsPublishMapExtractor),
                    ScannerVersions.WebFormsPublishMapExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["boundedInputSha256"] = provenance.BoundedInputSha256,
                    ["generatorSha256"] = provenance.GeneratorSha256,
                    ["sourcePath"] = sourcePath,
                    ["limitation"] = "Verified receipt membership, not source-to-binary method identity."
                }));
        foreach (var assembly in evaluation.Assemblies)
            facts.Add(FactFactory.Create(manifest, FactTypes.WebFormsPublishAssemblyBound,
                RuleIds.LegacyWebFormsPublishMap, EvidenceTiers.Tier2Structural,
                new EvidenceSpan(assembly.Path, 1, 1, null, nameof(WebFormsPublishMapExtractor),
                    ScannerVersions.WebFormsPublishMapExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["boundedInputSha256"] = provenance.BoundedInputSha256,
                    ["generatorSha256"] = provenance.GeneratorSha256,
                    ["assemblyRawSha256"] = assembly.Sha256,
                    ["limitation"] = "Verified published assembly bytes, not source-to-binary method identity."
                }));
        foreach (var page in evaluation.Pages)
            facts.Add(FactFactory.Create(manifest, FactTypes.WebFormsPublishPageMapped,
                RuleIds.LegacyWebFormsPublishMap, EvidenceTiers.Tier2Structural,
                new EvidenceSpan(page.SourcePath, 1, 1, null, nameof(WebFormsPublishMapExtractor),
                    ScannerVersions.WebFormsPublishMapExtractor),
                targetSymbol: page.AssemblyName,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["assemblyName"] = page.AssemblyName,
                    ["assemblyRawSha256"] = page.AssemblySha256,
                    ["boundedInputSha256"] = provenance.BoundedInputSha256,
                    ["generatedType"] = page.GeneratedType,
                    ["generatorSha256"] = provenance.GeneratorSha256,
                    ["mapSha256"] = page.MapSha256,
                    ["sourcePath"] = page.SourcePath,
                    ["limitation"] = "Verified operator-declared publish inputs bind a page to an emitted assembly, not a source method, PDB line, runtime page activation, or execution."
                }));
        foreach (var candidate in evaluation.Candidates ?? [])
            facts.Add(FactFactory.Create(manifest, FactTypes.WebFormsPublishPageCandidate,
                RuleIds.LegacyWebFormsPublishMap, EvidenceTiers.Tier3SyntaxOrTextual,
                new EvidenceSpan(candidate.SourcePath, 1, 1, null, nameof(WebFormsPublishMapExtractor),
                    ScannerVersions.WebFormsPublishMapExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sourcePath"] = candidate.SourcePath,
                    ["boundedInputSha256"] = provenance.BoundedInputSha256,
                    ["generatorSha256"] = provenance.GeneratorSha256,
                    ["limitation"] = "Operator-declared mapless page and hashed assemblies permit only a unique source-type-to-metadata review candidate; no page map, PDB line, build, activation, or runtime execution is proven."
                }));
        foreach (var gap in provenance.GapKinds)
            facts.Add(FactFactory.Create(manifest, FactTypes.AnalysisGap,
                RuleIds.LegacyWebFormsPublishMap, EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(".", 1, 1, null, nameof(WebFormsPublishMapExtractor),
                    ScannerVersions.WebFormsPublishMapExtractor),
                contractElement: gap,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gapKind"] = gap,
                    ["boundedInputSha256"] = provenance.BoundedInputSha256,
                    ["generatorSha256"] = provenance.GeneratorSha256,
                    ["limitation"] = "The declared publish evidence is withheld; no source-to-binary or runtime absence is inferred."
                }));
        return facts;
    }

    private static bool HasDuplicatePaths(IEnumerable<string?> paths) =>
        paths.Any(path => string.IsNullOrWhiteSpace(path))
        || paths.Distinct(StringComparer.Ordinal).Count() != paths.Count();

    private static string ResolveChild(string root, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains('\\')
            || relativePath.StartsWith("/", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath)
            || relativePath.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new PublishException("WebFormsPublishUnsafePath");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new PublishException("WebFormsPublishUnsafePath");
        var current = fullRoot;
        foreach (var segment in relativePath.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new PublishException("WebFormsPublishUnsafePath");
        }
        return path;
    }

    private static byte[] ReadBounded(string path, long maxBytes)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > maxBytes) throw new PublishException("WebFormsPublishInputLimitExceeded");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        if (output.Length > maxBytes) throw new PublishException("WebFormsPublishInputLimitExceeded");
        return output.ToArray();
    }

    private static string SafeReceiptDigest(string path)
    {
        try { return Sha256(ReadBounded(path, MaxReceiptBytes)); }
        catch { return Sha256(Encoding.UTF8.GetBytes("receipt-unavailable")); }
    }

    private static string InputDigest(string receiptSha256, string? rootHash, string? sourceBase = null)
    {
        var digest = rootHash is null ? receiptSha256 : Sha256(Encoding.UTF8.GetBytes(
            $"webforms-explicit-published-root.v1\nreceipt:{receiptSha256}\nroot:{rootHash}\n"));
        return sourceBase is null ? digest : Sha256(Encoding.UTF8.GetBytes(
            $"webforms-explicit-source-base.v1\ninput:{digest}\nsource-base:{sourceBase}\n"));
    }
    private static string SourceName(string? sourceBase, string? path)
    {
        // Validate the original path independently; prefixing must never sanitize unsafe receipt content.
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Contains(':')
            || path.StartsWith('/') || path.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new PublishException("WebFormsPublishUnsafePath");
        return sourceBase is null or "." ? path : sourceBase + "/" + path;
    }

    private static bool IsSha256(string? value) => value is { Length: 64 }
        && value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Sha256(Stream stream) => Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

    private sealed record PublishReceipt(string? SchemaVersion, string? Visibility,
        string? ReceiptGeneratorSha256, string? CompilerSha256, string? SourceCommitSha,
        string? BoundedInputSha256, IReadOnlyList<SourceFile>? SourceFiles,
        IReadOnlyList<PublishedFile>? PublishedFiles, IReadOnlyList<Page>? Pages,
        int? PublishedMapCount = null, string? MapInventorySha256 = null);
    private sealed record SourceFile(string? Path, string? Sha256);
    private sealed record PublishedFile(string? Path, string? Sha256, string? Kind);
    private sealed record Page(string? VirtualPath, string? Assembly, string? GeneratedType,
        string? MapPath, string? SourcePath = null, string? BindingKind = null);
    private sealed class PublishException(string gapKind) : Exception { public string GapKind { get; } = gapKind; }
}
