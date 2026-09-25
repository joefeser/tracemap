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
    int PageCount);

internal sealed record WebFormsPublishPage(string SourcePath, string AssemblyName,
    string AssemblySha256, string GeneratedType, string MapSha256);
internal sealed record WebFormsPublishAssembly(string Path, string Sha256);

internal sealed record WebFormsPublishEvaluation(WebFormsPublishProvenance? Provenance,
    IReadOnlyList<WebFormsPublishPage> Pages, IReadOnlyList<string> SourcePaths,
    IReadOnlyList<WebFormsPublishAssembly> Assemblies);

internal static class WebFormsPublishMapExtractor
{
    private const int MaxReceiptBytes = 1_048_576;
    private const int MaxMapBytes = 1_048_576;
    private const int MaxSourceFiles = 256;
    private const int MaxPublishedFiles = 64;
    private const int MaxPages = 32;
    private const long MaxArtifactBytes = 67_108_864;

    public static WebFormsPublishEvaluation Evaluate(string repoPath, string commitSha,
        ScanOptions options, CancellationToken cancellationToken)
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
        var gaps = new List<string>();
        var pages = new List<WebFormsPublishPage>();
        var sourcePaths = new List<string>();
        var assemblies = new List<WebFormsPublishAssembly>();
        var sourceCount = 0;
        var publishedCount = 0;
        var pageCount = 0;
        string boundedInputSha256;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = ReadBounded(receiptPath, MaxReceiptBytes);
            boundedInputSha256 = Sha256(bytes);
            var receipt = JsonSerializer.Deserialize<PublishReceipt>(bytes,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 16 });
            if (receipt?.SchemaVersion != "webforms-publish-binding.v1"
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
            if (sourceCount is < 1 or > MaxSourceFiles || publishedCount is < 2 or > MaxPublishedFiles
                || pageCount is < 1 or > MaxPages)
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
                var path = ResolveChild(repoPath, item.Path);
                if (Sha256(ReadBounded(path, MaxArtifactBytes)) != item.Sha256)
                    throw new PublishException("WebFormsPublishSourceMismatch");
                inputLines.Add($"{item.Path}:{item.Sha256}");
                sourcePaths.Add(item.Path!);
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
                if (string.IsNullOrWhiteSpace(item.Assembly) || string.IsNullOrWhiteSpace(item.GeneratedType)
                    || item.VirtualPath is null || !item.VirtualPath.StartsWith("/", StringComparison.Ordinal))
                    throw new PublishException("WebFormsPublishReceiptInvalid");
                var sourcePath = item.VirtualPath.TrimStart('/');
                _ = ResolveChild(repoPath, sourcePath);
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
                pages.Add(new WebFormsPublishPage(sourcePath, item.Assembly,
                    assembly.Sha256!, item.GeneratedType, map.Sha256!));
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (PublishException exception)
        {
            boundedInputSha256 = SafeReceiptDigest(receiptPath);
            gaps.Add(exception.GapKind);
            pages.Clear();
            sourcePaths.Clear();
            assemblies.Clear();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or JsonException or XmlException or ArgumentException or OverflowException)
        {
            boundedInputSha256 = SafeReceiptDigest(receiptPath);
            gaps.Add("WebFormsPublishReceiptUnreadable");
            pages.Clear();
            sourcePaths.Clear();
            assemblies.Clear();
        }
        var provenance = new WebFormsPublishProvenance("webforms-publish-provenance.v1",
            generatorSha256, boundedInputSha256, gaps.Count == 0 ? "bound" : "gap",
            gaps, sourceCount, publishedCount, pageCount);
        return new WebFormsPublishEvaluation(provenance, pages, sourcePaths, assemblies);
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

    private static bool IsSha256(string? value) => value is { Length: 64 }
        && value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Sha256(Stream stream) => Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

    private sealed record PublishReceipt(string? SchemaVersion, string? Visibility,
        string? ReceiptGeneratorSha256, string? CompilerSha256, string? SourceCommitSha,
        string? BoundedInputSha256, IReadOnlyList<SourceFile>? SourceFiles,
        IReadOnlyList<PublishedFile>? PublishedFiles, IReadOnlyList<Page>? Pages);
    private sealed record SourceFile(string? Path, string? Sha256);
    private sealed record PublishedFile(string? Path, string? Sha256, string? Kind);
    private sealed record Page(string? VirtualPath, string? Assembly, string? GeneratedType,
        string? MapPath);
    private sealed class PublishException(string gapKind) : Exception { public string GapKind { get; } = gapKind; }
}
