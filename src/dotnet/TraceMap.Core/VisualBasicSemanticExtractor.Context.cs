using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;

namespace TraceMap.Core;

public static partial class VisualBasicSemanticExtractor
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Compilation, Dictionary<MetadataReference, string>> ExplicitReferenceHashes = new();

    private static string? ExplicitReferenceMethodIdentity(MetadataReference reference, IMethodSymbol method)
    {
        try { return ReadExplicitReferenceMethodIdentity(reference, method); }
        catch (Exception exception) when (exception is BadImageFormatException or ArgumentException
            or ManagedMetadataExtractor.ManagedInputException or NotSupportedException)
        {
            // Keep the semantic call, but never invent an exact-reference witness
            // when its metadata signature cannot be decoded within the limits.
            return null;
        }
    }

    private static string? ReadExplicitReferenceMethodIdentity(MetadataReference reference, IMethodSymbol method)
    {
        // The compiler can retarget parameter type assembly versions. Its
        // resolved token still identifies the declaration in the captured PE;
        // never reconstruct that declaration from retargeted symbol types.
        var token = method.OriginalDefinition.MetadataToken;
        if ((token & unchecked((int)0xff000000)) != 0x06000000
            || reference is not PortableExecutableReference portable
            || portable.GetMetadata() is not AssemblyMetadata metadata) return null;
        var modules = metadata.GetModules();
        if (modules.Length != 1) return null; // No unverified companion-module bytes.
        var reader = modules[0].GetMetadataReader();
        if (reader.GetString(reader.GetModuleDefinition().Name) != method.ContainingModule.Name) return null;
        var handle = System.Reflection.Metadata.Ecma335.MetadataTokens.MethodDefinitionHandle(token & 0x00ffffff);
        if (handle.IsNil || (token & 0x00ffffff) > reader.MethodDefinitions.Count) return null;
        if (reader.GetString(reader.GetMethodDefinition(handle).Name) != method.MetadataName) return null;
        return ManagedMetadataExtractor.MethodDefinitionIdentity(reader, handle);
    }
    // Opt-in compiler context, never a reconstruction of the original build.
    private static SemanticExtractionResult ExtractExplicitContext(string repoPath,
        IReadOnlyList<FileInventoryItem> inventory, ScanOptions options, CancellationToken cancellationToken)
    {
        var facts = new List<SemanticFactCandidate>();
        var gaps = new List<SemanticFactCandidate>();
        var analyzed = new HashSet<string>(StringComparer.Ordinal);
        var inputs = new CompilationInputSet();
        try
        {
            if (inventory.Any(f => f.Kind is "VisualBasicProject" or "Solution")
                || options.ProjectPaths is { Count: > 0 } || options.SolutionPaths is { Count: > 0 }
                || options.Restore || options.TargetFramework is not null)
                throw new InvalidDataException("context-scope-conflict");
            var bytes = ReadContextBytes(Path.GetFullPath(options.VisualBasicCompilationContextPath!), 1_048_576);
            using var json = JsonDocument.Parse(DecodeExplicitContext(bytes), new JsonDocumentOptions { MaxDepth = 8 });
            var root = json.RootElement;
            RequireFields(root, "schemaVersion", "commitSha", "rootNamespace", "optionStrict", "optionInfer", "optionExplicit", "imports", "sources", "references");
            var commit = root.GetProperty("commitSha").GetString();
            if (root.GetProperty("schemaVersion").GetString() != "vb-compilation-context.v1"
                || commit is null || commit.Length is not (40 or 64) || !commit.All(Uri.IsHexDigit)
                || commit != GitMetadataProvider.Detect(repoPath).CommitSha)
                throw new InvalidDataException("context-identity-mismatch");
            var imports = root.GetProperty("imports").EnumerateArray().Select(x => x.GetString()!).ToArray();
            if (imports.Length > 128 || imports.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 512)
                || imports.Distinct(StringComparer.OrdinalIgnoreCase).Count() != imports.Length)
                throw new InvalidDataException("context-imports-invalid");
            var rootNamespace = root.GetProperty("rootNamespace").GetString()!;
            if (rootNamespace is null || rootNamespace.Length > 512) throw new InvalidDataException("context-namespace-invalid");
            var compilationOptions = new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                rootNamespace: rootNamespace, globalImports: imports.Select(GlobalImport.Parse),
                optionStrict: root.GetProperty("optionStrict").GetBoolean() ? OptionStrict.On : OptionStrict.Off,
                optionInfer: root.GetProperty("optionInfer").GetBoolean(),
                optionExplicit: root.GetProperty("optionExplicit").GetBoolean());
            var sources = root.GetProperty("sources").EnumerateArray().ToArray();
            var references = root.GetProperty("references").EnumerateArray().ToArray();
            if (sources.Length is < 1 or > 512 || references.Length is < 1 or > 256)
                throw new InvalidDataException("context-input-limit");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceData = new List<(string Path, SourceText Text)>();
            long total = 0;
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequireFields(source, "path", "sha256");
                var path = source.GetProperty("path").GetString()!;
                if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\')
                    || path.Split('/').Any(p => p is "." or ".." or "") || !paths.Add(path)
                    || !inventory.Any(f => f.RelativePath == path && FileInventory.IsVisualBasicKind(f.Kind)))
                    throw new InvalidDataException("context-source-outside-inventory");
                var data = ReadContextBytes(Path.Combine(repoPath, path), 8_388_608);
                VerifyContextHash(data, source);
                total += data.Length;
                if (total > 67_108_864) throw new InvalidDataException("context-source-limit");
                sourceData.Add((path, SourceText.From(new MemoryStream(data), encoding: null)));
            }
            var metadata = new List<MetadataReference>();
            var referenceHashes = new Dictionary<MetadataReference, string>();
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            total = 0;
            foreach (var reference in references)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequireFields(reference, "path", "sha256");
                var path = reference.GetProperty("path").GetString()!;
                if (!Path.IsPathRooted(path)) throw new InvalidDataException("context-reference-path-invalid");
                var data = ReadContextBytes(path, 67_108_864);
                VerifyContextHash(data, reference);
                total += data.Length;
                if (total > 536_870_912) throw new InvalidDataException("context-reference-limit");
                using var pe = new PEReader(new MemoryStream(data));
                var reader = pe.GetMetadataReader();
                if (!reader.IsAssembly || !identities.Add(reader.GetString(reader.GetAssemblyDefinition().Name)))
                    throw new InvalidDataException("context-reference-identity-conflict");
                var admittedReference = MetadataReference.CreateFromImage(ImmutableArray.CreateRange(data));
                metadata.Add(admittedReference);
                referenceHashes.Add(admittedReference, Convert.ToHexStringLower(SHA256.HashData(data)));
            }
            using var workspace = new AdhocWorkspace();
            var project = workspace.AddProject("ExplicitVbContext", LanguageNames.VisualBasic)
                .WithCompilationOptions(compilationOptions)
                .WithMetadataReferences(metadata);
            foreach (var source in sourceData.OrderBy(x => x.Path, StringComparer.Ordinal))
                project = project.AddDocument(source.Path, source.Text, filePath: Path.Combine(repoPath, source.Path)).Project;
            var compilation = (VisualBasicCompilation)WaitOnWorkspace(project.GetCompilationAsync(cancellationToken), cancellationToken)!;
            ExplicitReferenceHashes.Add(compilation, referenceHashes);
            AddCompilationDiagnostics(repoPath, ".", compilation, gaps, out _, out _);
            foreach (var document in project.Documents)
            {
                var path = Path.GetRelativePath(repoPath, document.FilePath!).Replace('\\', '/');
                inputs.Record(path, WaitOnWorkspace(document.GetTextAsync(cancellationToken), cancellationToken));
                ExtractDocument(repoPath, null, document, compilation, facts, gaps, analyzed, null, path, cancellationToken);
            }
            // Every semantic fact explicitly states the supplied-context boundary.
            var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
            for (var i = 0; i < facts.Count; i++)
            {
                var properties = new Dictionary<string, string>(facts[i].Properties ?? new Dictionary<string, string>())
                {
                    ["compilationContextKind"] = "explicit-projectless-inputs",
                    ["compilationContextSha256"] = digest,
                    ["originalBuildCorrespondence"] = "not-established"
                };
                facts[i] = facts[i] with { Properties = properties };
            }
            return new(facts, gaps, true, gaps.Count > 0, analyzed, CompilationInputFiles: inputs);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or ArgumentException or BadImageFormatException)
        {
            // Fail closed for the entire supplied context. Syntax fallback remains available.
            return new([], [CreateGap(".", "Explicit Visual Basic compilation context was refused.",
                "ExplicitCompilationContextRefused")], true, true, new HashSet<string>(StringComparer.Ordinal));
        }
    }

    public static byte[] DecodeExplicitContext(byte[] bytes)
    {
        if (bytes.Length > 1_048_576) throw new InvalidDataException("context-file-limit");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var schema)
            || schema.GetString() != "prepared-vb-compilation-context.v1") return bytes;
        RequireFields(root, "schemaVersion", "generatorSha256", "boundedInputSha256", "payloadJson", "ruleId", "visibility");
        if (root.GetProperty("ruleId").GetString() != "workflow.vb-compilation-context-preparation.v1"
            || root.GetProperty("visibility").GetString() != "local-only") throw new InvalidDataException("context-envelope-invalid");
        var generator = root.GetProperty("generatorSha256").GetString();
        if (generator is null || generator.Length != 64 || !generator.All(Uri.IsHexDigit))
            throw new InvalidDataException("context-generator-invalid");
        var payload = System.Text.Encoding.UTF8.GetBytes(root.GetProperty("payloadJson").GetString()!);
        if (root.GetProperty("boundedInputSha256").GetString() != Convert.ToHexStringLower(SHA256.HashData(payload)))
            throw new InvalidDataException("context-payload-mismatch");
        return payload;
    }

    private static void RequireFields(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("context-object-required");
        var actual = value.EnumerateObject().Select(x => x.Name).ToArray();
        if (actual.Length != fields.Length || actual.Distinct(StringComparer.Ordinal).Count() != fields.Length
            || actual.Except(fields, StringComparer.Ordinal).Any()) throw new InvalidDataException("context-fields-invalid");
    }

    private static void VerifyContextHash(byte[] bytes, JsonElement entry)
    {
        if (entry.GetProperty("sha256").GetString() != Convert.ToHexStringLower(SHA256.HashData(bytes)))
            throw new InvalidDataException("context-input-hash-mismatch");
    }

    private static byte[] ReadContextBytes(string path, int limit)
    {
        for (var current = new FileInfo(path) as FileSystemInfo; current is not null;
             current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent)
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("context-link-refused");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > limit) throw new InvalidDataException("context-file-limit");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidDataException("context-file-changed");
        return bytes;
    }
}
