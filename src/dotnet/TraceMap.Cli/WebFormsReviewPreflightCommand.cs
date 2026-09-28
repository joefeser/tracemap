using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TraceMap.Core;

namespace TraceMap.Cli;

public sealed record WebFormsReviewBudgets(
    int MaxInputFiles = 128,
    long MaxAssemblyBytes = 67_108_864,
    long MaxRetainedArtifactBytes = 17_179_869_184,
    long MaxTotalHashBytes = 68_719_476_736,
    long IlMaxWork = 30_000_000,
    int GraphMaxDepth = 20,
    int GraphMaxPaths = 256,
    long GraphMaxWork = 2_000_000,
    long MetadataMaxWork = 500_000,
    int MetadataMaxText = 8_192,
    int IlMaxText = 16_384,
    long MaxParentFacts = 5_000_000,
    int MaxFactLineChars = 1_048_576);

public sealed record WebFormsReviewConfig(
    string SchemaVersion,
    string Operation,
    string SourceRoot,
    string SourceCommitSha,
    string ProjectMode,
    string? SolutionRelativePath,
    string[] ProjectRelativePaths,
    string[] SourceFolders,
    string PageMode,
    string[] PageRelativePaths,
    string PublishedRoot,
    string[] PrimaryAssemblies,
    string[] DependencyAssemblies,
    string[] BindingReceipts,
    string[] PdbInputs,
    string[] PageMaps,
    string? ParentScanRoot,
    WebFormsReviewBudgets Budgets,
    string? PublishReceiptRelativePath = null,
    string? ReceiptRoot = null);

public sealed record WebFormsReviewInput(string Role, string Path, long Bytes, string Sha256, string? MetadataName = null);
public sealed record WebFormsReviewPhase(string Phase, string State, string NextAction);
public sealed record WebFormsReviewPreflightManifest(
    string SchemaVersion, string RuleId, string Visibility, string ClaimLevel,
    string RunId, string State, string GeneratorSha256, string BoundedInputSha256,
    string InputCanonicalization, WebFormsReviewConfig Configuration,
    IReadOnlyList<WebFormsReviewInput> Inputs, long HashedBytes,
    string? ParentScanId, IReadOnlyList<WebFormsReviewPhase> Phases,
    IReadOnlyList<string> Gaps, IReadOnlyList<string> Limitations);

/// <summary>Local-only planning boundary; never runs scans, builds, binds DLLs or rewrites retained inputs.</summary>
public static class WebFormsReviewPreflightCommand
{
    public const string ConfigSchema = "webforms-compiled-review-config.v1";
    public const string ManifestSchema = "webforms-compiled-review-run.v1";
    public const string RuleId = "workflow.webforms.compiled-review-preflight.v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public const string Help = """
        tracemap webforms-review preflight --config <private-json> --out <new-durable-run-root>
        tracemap webforms-review run --run <durable-run-root>
        tracemap webforms-review resume --run <durable-run-root>

        Validates the fresh/attach run contract and inventories explicit compiled inputs.
        Writes local-only run-manifest.json and README.md. No scan/build/publish/binding,
        report rendering, source mutation, cleanup or implicit TEMP discovery occurs.
        This output is preflight only, not a completed review workflow.
        Run/resume execute fresh source-plus-compiled scans with pinned checkpoints.
        Attachment and unified reports are not implemented yet; retain the proven wrappers.
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (args.Length != 5 || args[0] != "preflight" || args[1] != "--config" || args[3] != "--out")
                throw Fail("ARGUMENT_INVALID");
            var manifest = await BuildAsync(args[2], args[4], cancellationToken);
            var outputRoot = PhysicalPath(args[4]);
            // Recheck after hashing: inputs or output ancestors may have changed.
            ValidateOutput(outputRoot, manifest.Configuration);
            foreach (var input in manifest.Inputs)
            {
                var current = await HashAsync(input.Role, input.Path, input.Bytes, cancellationToken);
                if (current.Bytes != input.Bytes || current.Sha256 != input.Sha256) throw Fail("INPUT_CHANGED");
            }
            cancellationToken.ThrowIfCancellationRequested();
            var parent = Path.GetDirectoryName(outputRoot)!;
            Directory.CreateDirectory(parent);
            var staging = Path.Combine(parent, ".webforms-preflight-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            await File.WriteAllTextAsync(Path.Combine(staging, "run-manifest.json"),
                JsonSerializer.Serialize(manifest, JsonOptions) + "\n", new UTF8Encoding(false), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(staging, "README.md"),
                $"# Private Web Forms review run\n\nState: preflight only. Rule: `{RuleId}`.\n\n" +
                "The manifest is this run's explicit inventory, not proof that scanning, binding or reporting occurred.\n" +
                "Use webforms-review run/resume --run <this-root> for a fresh scan. Attachment/reports remain pending.\n" +
                "Source, publish and parent scans remain external and immutable; this run is not relocatable yet.\n" +
                "Do not upload this private manifest or delete referenced inputs.\n", cancellationToken);
            Directory.Move(staging, outputRoot); // Never replaces an existing run.
            await output.WriteLineAsync($"webFormsPreflight=completed;inputs={manifest.Inputs.Count};gaps={manifest.Gaps.Count}");
            await output.WriteLineAsync($"webFormsRunManifest={Path.Combine(outputRoot, "run-manifest.json")}");
            return 0;
        }
        catch (PreflightException exception) { await error.WriteLineAsync($"error: {exception.Code}"); return 1; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or BadImageFormatException or OverflowException or KeyNotFoundException or InvalidOperationException)
        { await error.WriteLineAsync("error: WEBFORMS_PREFLIGHT_INPUT_OR_OUTPUT_UNREADABLE"); return 1; }
    }

    public static async Task<WebFormsReviewPreflightManifest> BuildAsync(string configPath, string outputRoot,
        CancellationToken cancellationToken = default, Func<string, GitMetadata>? detectGit = null)
    {
        configPath = PhysicalPath(configPath);
        var configInput = await HashAsync("configuration", configPath, 1_048_576, cancellationToken);
        var configBytes = await ReadSmallAsync(configPath, 1_048_576, cancellationToken);
        RejectDuplicateProperties(configBytes);
        var config = JsonSerializer.Deserialize<WebFormsReviewConfig>(configBytes, JsonOptions) ?? throw Fail("CONFIG_INVALID");
        if (Digest(configBytes) != configInput.Sha256) throw Fail("INPUT_CHANGED");
        ValidateConfig(config);
        config = config with { SourceRoot = PhysicalPath(config.SourceRoot), PublishedRoot = PhysicalPath(config.PublishedRoot),
            ParentScanRoot = config.ParentScanRoot is null ? null : PhysicalPath(config.ParentScanRoot),
            ReceiptRoot = config.ReceiptRoot is null ? null : PhysicalPath(config.ReceiptRoot) };
        ValidateOutput(PhysicalPath(outputRoot), config);
        if (!Directory.Exists(config.SourceRoot) || !Directory.Exists(config.PublishedRoot)
            || (config.ReceiptRoot is not null && !Directory.Exists(config.ReceiptRoot))) throw Fail("ROOT_UNAVAILABLE");
        var receiptRoot = config.ReceiptRoot ?? config.PublishedRoot;
        var git = (detectGit ?? GitMetadataProvider.Detect)(config.SourceRoot);
        if (git.CommitSha != config.SourceCommitSha) throw Fail("SOURCE_COMMIT_MISMATCH");
        foreach (var folder in config.SourceFolders)
            if (!Directory.Exists(folder == "." ? config.SourceRoot : Child(config.SourceRoot, folder))) throw Fail("SOURCE_FOLDER_UNAVAILABLE");
        var inputs = new List<WebFormsReviewInput> { configInput };
        var paths = new HashSet<string>(PathComparer) { configPath };
        var gaps = new SortedSet<string>(StringComparer.Ordinal)
        {
            "BindingValidationDeferred", "SourceSnapshotValidationDeferred", "BuildProvenanceNotEstablished",
            "SourceLineIdentityNotEstablished", "CrossAssemblyAdmissionDeferred"
        };
        async Task<WebFormsReviewInput> Add(string role, string path, long limit)
        {
            path = PhysicalPath(path);
            if (!paths.Add(path)) throw Fail("DUPLICATE_INPUT");
            if (inputs.Count >= config.Budgets.MaxInputFiles) throw Fail("INPUT_COUNT_LIMIT");
            var item = await HashAsync(role, path, limit, cancellationToken);
            if (inputs.Sum(input => input.Bytes) > config.Budgets.MaxTotalHashBytes - item.Bytes) throw Fail("HASH_BYTES_LIMIT");
            inputs.Add(item);
            return item;
        }
        if (configInput.Bytes > config.Budgets.MaxTotalHashBytes) throw Fail("HASH_BYTES_LIMIT");
        if (config.SolutionRelativePath is not null) await Add("solution", Child(config.SourceRoot, config.SolutionRelativePath), 4_194_304);
        foreach (var project in config.ProjectRelativePaths.Order(StringComparer.Ordinal)) await Add("project", Child(config.SourceRoot, project), 4_194_304);
        foreach (var page in config.PageRelativePaths.Order(StringComparer.Ordinal)) await Add("selected-page", Child(config.SourceRoot, page), 4_194_304);
        foreach (var (role, names) in new[] { ("primary-assembly", config.PrimaryAssemblies), ("dependency-assembly", config.DependencyAssemblies) })
        {
            foreach (var name in names.Order(StringComparer.Ordinal))
            {
                var path = Child(config.PublishedRoot, name);
                var item = await Add(role, path, config.Budgets.MaxAssemblyBytes);
                using var stream = File.OpenRead(path);
                using var pe = new PEReader(stream, PEStreamOptions.PrefetchMetadata);
                if (!pe.HasMetadata || !pe.GetMetadataReader().IsAssembly) throw Fail("MANAGED_ASSEMBLY_REQUIRED");
                var reader = pe.GetMetadataReader();
                var definition = reader.GetAssemblyDefinition();
                inputs[inputs.IndexOf(item)] = item with { MetadataName = reader.GetString(definition.Name) + ", Version=" + definition.Version };
            }
        }
        var receiptHashes = new List<string>();
        foreach (var receiptPath in config.BindingReceipts.Order(StringComparer.Ordinal))
        {
            var input = await Add("binding-receipt", Child(receiptRoot, receiptPath), 1_048_576);
            var bytes = await ReadSmallAsync(input.Path, 1_048_576, cancellationToken);
            if (Digest(bytes) != input.Sha256) throw Fail("INPUT_CHANGED");
            RejectDuplicateProperties(bytes);
            using var receipt = JsonDocument.Parse(bytes);
            if (!receipt.RootElement.TryGetProperty("schemaVersion", out var schema) || schema.GetString() != "compiled-input-binding-set.v1" ||
                !receipt.RootElement.TryGetProperty("bindings", out var bindings) || bindings.ValueKind != JsonValueKind.Array ||
                bindings.GetArrayLength() > config.Budgets.MaxInputFiles) throw Fail("BINDING_RECEIPT_INVALID");
            foreach (var binding in bindings.EnumerateArray())
            {
                if (!binding.TryGetProperty("artifactSha256", out var hash) || !IsHex(hash.GetString(), 64)) throw Fail("BINDING_RECEIPT_INVALID");
                receiptHashes.Add(hash.GetString()!);
            }
        }
        foreach (var assembly in inputs.Where(input => input.Role.EndsWith("assembly", StringComparison.Ordinal)))
        {
            var candidates = receiptHashes.Count(hash => hash == assembly.Sha256);
            if (candidates == 0) gaps.Add("MissingBindingReceiptHashCandidate");
            if (candidates > 1) gaps.Add("AmbiguousBindingReceiptHashCandidate");
        }
        foreach (var pdb in config.PdbInputs.Order(StringComparer.Ordinal)) await Add("pdb", Child(config.PublishedRoot, pdb), config.Budgets.MaxAssemblyBytes);
        foreach (var map in config.PageMaps.Order(StringComparer.Ordinal)) await Add("page-map", Child(config.PublishedRoot, map), 1_048_576);
        if (config.PublishReceiptRelativePath is not null)
        {
            var receiptPath = Child(receiptRoot, config.PublishReceiptRelativePath);
            var receiptInput = await Add("publish-receipt", receiptPath, 1_048_576);
            var bytes = await ReadSmallAsync(receiptPath, 1_048_576, cancellationToken);
            if (Digest(bytes) != receiptInput.Sha256) throw Fail("INPUT_CHANGED");
            RejectDuplicateProperties(bytes);
            using var receipt = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            var root = receipt.RootElement;
            if (root.GetProperty("schemaVersion").GetString() != "webforms-publish-binding.v1" ||
                root.GetProperty("visibility").GetString() != "local-only" ||
                root.GetProperty("sourceCommitSha").GetString() != config.SourceCommitSha) throw Fail("PUBLISH_RECEIPT_INVALID");
            var sources = Array("sourceFiles", 256);
            var published = Array("publishedFiles", 64);
            var pages = Array("pages", 32);
            var sourceNames = new HashSet<string>(PathComparer);
            foreach (var source in sources.EnumerateArray())
            {
                var name = source.GetProperty("path").GetString() ?? throw Fail("PUBLISH_RECEIPT_INVALID");
                var sha = source.GetProperty("sha256").GetString();
                if (!sourceNames.Add(name) || !IsHex(sha, 64)) throw Fail("PUBLISH_RECEIPT_INVALID");
                var path = Child(config.SourceRoot, name);
                var existing = inputs.SingleOrDefault(input => PathComparer.Equals(input.Path, path));
                var item = existing ?? await Add("publish-source", path, 67_108_864);
                if (item.Sha256 != sha) throw Fail("PUBLISH_SOURCE_MISMATCH");
            }
            var publishedNames = new HashSet<string>(PathComparer);
            foreach (var item in published.EnumerateArray())
            {
                var name = item.GetProperty("path").GetString() ?? throw Fail("PUBLISH_RECEIPT_INVALID");
                var kind = item.GetProperty("kind").GetString();
                if (!publishedNames.Add(name) || kind is not ("assembly" or "compiled-map")) throw Fail("PUBLISH_RECEIPT_INVALID");
                var path = Child(config.PublishedRoot, name);
                var declared = inputs.SingleOrDefault(input => PathComparer.Equals(input.Path, path) &&
                    (kind == "assembly" ? input.Role is "primary-assembly" or "dependency-assembly" : input.Role == "page-map"));
                if (declared is null || declared.Sha256 != item.GetProperty("sha256").GetString()) throw Fail("PUBLISH_ARTIFACT_NOT_DECLARED_OR_MISMATCH");
            }
            var pageNames = new HashSet<string>(PathComparer);
            foreach (var page in pages.EnumerateArray())
            {
                var sourcePath = page.TryGetProperty("sourcePath", out var source) && source.ValueKind == JsonValueKind.String
                    ? source.GetString() : page.GetProperty("virtualPath").GetString()?.TrimStart('/');
                if (sourcePath is null || !pageNames.Add(sourcePath) || !sourceNames.Contains(sourcePath)) throw Fail("PUBLISH_PAGE_INVALID");
            }
            if (config.PageMode == "selected" && config.PageRelativePaths.Any(page => !pageNames.Contains(page))) throw Fail("PUBLISH_SELECTED_PAGE_UNAVAILABLE");
            gaps.Add("PublishReceiptSemanticValidationDeferred");

            JsonElement Array(string name, int max)
            {
                var value = root.GetProperty(name);
                if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 1 || value.GetArrayLength() > max) throw Fail("PUBLISH_RECEIPT_LIMIT_OR_SHAPE_INVALID");
                return value;
            }
        }
        gaps.Add("PdbIdentityValidationDeferred");
        gaps.Add("PageMapValidationDeferred");
        if (config.PdbInputs.Length == 0) gaps.Add("PdbInputsNotDeclared");
        if (config.PageMaps.Length == 0) gaps.Add("PageMapsNotDeclared");
        string? parentScanId = null;
        if (config.ParentScanRoot is not null)
        {
            foreach (var name in new[] { "scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log" })
                await Add("parent-" + name, Child(config.ParentScanRoot, name), name == "scan-manifest.json" ? 4_194_304 : config.Budgets.MaxRetainedArtifactBytes);
            var bytes = await ReadSmallAsync(Child(config.ParentScanRoot, "scan-manifest.json"), 4_194_304, cancellationToken);
            if (Digest(bytes) != inputs.Single(input => input.Role == "parent-scan-manifest.json").Sha256) throw Fail("INPUT_CHANGED");
            using var parent = JsonDocument.Parse(bytes);
            RejectDuplicateProperties(bytes);
            if (!parent.RootElement.TryGetProperty("commitSha", out var commit) || commit.GetString() != config.SourceCommitSha ||
                !parent.RootElement.TryGetProperty("scanId", out var id) || string.IsNullOrWhiteSpace(id.GetString())) throw Fail("PARENT_PROVENANCE_MISMATCH");
            parentScanId = id.GetString();
            gaps.Add("ParentRepositoryAndIndexValidationDeferred");
        }
        var ordered = inputs.OrderBy(input => input.Role, StringComparer.Ordinal).ThenBy(input => input.Path, StringComparer.Ordinal).ToArray();
        var boundedInput = Digest(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { configurationSha256 = configInput.Sha256, inputs = ordered }, JsonOptions)));
        var generator = await HashAsync("generator", typeof(WebFormsReviewPreflightCommand).Assembly.Location, 67_108_864, cancellationToken);
        return new(ManifestSchema, RuleId, "local-only", "preflight-only-not-executed", Guid.NewGuid().ToString("N"),
            "preflight-completed-with-deferred-validation", generator.Sha256, boundedInput, "config-sha256-and-ordered-inputs-camel-json-v1",
            config, ordered, ordered.Sum(input => input.Bytes), parentScanId,
            [new("preflight", "completed", "Retain this private manifest and its external input dependencies."),
             new("source", config.Operation == "attach" ? "retained-not-validated" : "pending", "Validate source snapshot and retained source/index identity before execution."),
             new("binding", "pending", "Use the authoritative compiled binding policy; hash candidates are not bindings."),
             new("compiled-scan", "pending", "Execute only after binding/parent validation; do not mutate a parent scan."),
             new("combine-and-report", "pending", "Preserve review-only paths, gaps and source page verdicts.")],
            gaps.ToArray(),
            ["No scanning, publishing, assembly loading, binding, source-line reconciliation, runtime SQL execution or cleanup was performed.",
             "File hash and managed header inspection are preflight inventory only. Receipt hash candidates are not admitted source/binary joins.",
             "Map/PDB content, dirty-worktree snapshots, retained index compatibility and parent repository identity require later authoritative validation.",
             "Inputs are explicitly enumerated beneath their declared roots; external/scattered DLLs require a future explicit locator contract, not implicit discovery.",
             "Hash limits are streamed and distinct from configured future IL/graph budgets. No large-corpus throughput, memory or completeness claim is made.",
             "This local-only manifest is not portable or shareable; relocation, resume, full execution and dependency-aware cleanup are not implemented in this slice."]);
    }

    private static void ValidateConfig(WebFormsReviewConfig config)
    {
        if (config.SchemaVersion != ConfigSchema || config.Operation is not ("fresh" or "attach") ||
            !IsHex(config.SourceCommitSha, 40) || string.IsNullOrWhiteSpace(config.SourceRoot) || string.IsNullOrWhiteSpace(config.PublishedRoot) ||
            config.Budgets is null || config.ProjectRelativePaths is null || config.SourceFolders is null || config.PageRelativePaths is null ||
            config.PrimaryAssemblies is null || config.DependencyAssemblies is null || config.BindingReceipts is null || config.PdbInputs is null || config.PageMaps is null)
            throw Fail("CONFIG_INVALID");
        if (!Path.IsPathFullyQualified(config.SourceRoot) || !Path.IsPathFullyQualified(config.PublishedRoot) ||
            (config.ParentScanRoot is not null && !Path.IsPathFullyQualified(config.ParentScanRoot)) ||
            (config.ReceiptRoot is not null && !Path.IsPathFullyQualified(config.ReceiptRoot))) throw Fail("ROOT_PATH_INVALID");
        var lists = new[] { config.ProjectRelativePaths, config.SourceFolders, config.PageRelativePaths, config.PrimaryAssemblies,
            config.DependencyAssemblies, config.BindingReceipts, config.PdbInputs, config.PageMaps };
        if (config.SourceFolders.Length == 0 || config.PrimaryAssemblies.Length == 0 ||
            lists.Any(list => list.Length > 256 || list.Any(string.IsNullOrWhiteSpace) || list.Distinct(PathComparer).Count() != list.Length)) throw Fail("CONFIG_INVALID");
        if (config.ProjectMode is not ("projectless" or "solution" or "projects") ||
            (config.ProjectMode == "solution") != (config.SolutionRelativePath is not null) ||
            (config.ProjectMode == "projects") != (config.ProjectRelativePaths.Length > 0)) throw Fail("PROJECT_SELECTION_INVALID");
        if (config.SolutionRelativePath is not null && !config.SolutionRelativePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)) throw Fail("PROJECT_SELECTION_INVALID");
        if (config.PageMode is not ("selected" or "all") || (config.PageMode == "selected") != (config.PageRelativePaths.Length > 0) ||
            config.PageRelativePaths.Any(path => !path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))) throw Fail("PAGE_SELECTION_INVALID");
        if ((config.Operation == "attach") != !string.IsNullOrWhiteSpace(config.ParentScanRoot)) throw Fail("PARENT_SELECTION_INVALID");
        var budget = config.Budgets;
        if (budget.MaxInputFiles is < 1 or > 256 || budget.MaxAssemblyBytes is < 1 or > 1_073_741_824 ||
            budget.MaxRetainedArtifactBytes is < 1 or > 1_099_511_627_776 || budget.MaxTotalHashBytes is < 1 or > 1_099_511_627_776 ||
            budget.IlMaxWork is < 1 or > 100_000_000 || budget.GraphMaxWork is < 1 or > 100_000_000 ||
            budget.GraphMaxDepth is < 1 or > 20 || budget.GraphMaxPaths is < 1 or > 256 ||
            budget.MetadataMaxWork is < 1 or > 100_000_000 || budget.MetadataMaxText is < 71 or > 65_536 ||
            budget.IlMaxText is < 71 or > 65_536 || budget.MaxParentFacts is < 1 or > 100_000_000 ||
            budget.MaxFactLineChars is < 128 or > 16_777_216) throw Fail("BUDGET_INVALID");
    }
    private static void ValidateOutput(string output, WebFormsReviewConfig config)
    {
        if (File.Exists(output) || Directory.Exists(output)) throw Fail("OUTPUT_EXISTS");
        foreach (var root in new[] { config.SourceRoot, config.PublishedRoot, config.ParentScanRoot, config.ReceiptRoot }.Where(root => root is not null))
            if (Contains(PhysicalPath(root!), output) || Contains(output, PhysicalPath(root!))) throw Fail("OUTPUT_OVERLAPS_INPUT");
    }
    private static bool Contains(string root, string child) => PathComparer.Equals(root, child) ||
        child.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, PathComparison);
    private static string Child(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') ||
            relative.Replace('\\', '/').Split('/').Any(segment => segment is "" or "." or "..")) throw Fail("RELATIVE_PATH_INVALID");
        var child = PhysicalPath(Path.Combine(root, relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
        if (!Contains(root, child) || PathComparer.Equals(root, child)) throw Fail("INPUT_ESCAPES_ROOT");
        return child;
    }
    internal static string PhysicalPath(string path)
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
    internal static async Task<WebFormsReviewInput> HashAsync(string role, string path, long maximum, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, FileOptions.SequentialScan | FileOptions.Asynchronous);
        if (stream.Length > maximum) throw Fail("FILE_BYTES_LIMIT");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65_536];
        long bytes = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0)
        {
            bytes = checked(bytes + read);
            if (bytes > maximum) throw Fail("FILE_BYTES_LIMIT");
            hash.AppendData(buffer, 0, read);
        }
        return new(role, path, bytes, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }
    internal static async Task<byte[]> ReadSmallAsync(string path, int maximum, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + read > maximum) throw Fail("FILE_BYTES_LIMIT");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
    internal static void RejectDuplicateProperties(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject()) { if (!names.Add(property.Name)) throw Fail("JSON_DUPLICATE_PROPERTY"); Visit(property.Value); }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Visit(item);
        }
        Visit(document.RootElement);
    }
    private static bool IsHex(string? value, int length) => value?.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static PreflightException Fail(string suffix) => new("WEBFORMS_PREFLIGHT_" + suffix);
    private sealed class PreflightException(string code) : Exception(code) { public string Code { get; } = code; }
}
