using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TraceMap.Core;

namespace TraceMap.Cli;

/// <summary>Reuse explicit legacy receipt bytes; never create a new attestation or scan.</summary>
public static class WebFormsProofImportCommand
{
    public const string RuleId = "workflow.webforms.retained-proof-import.v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32
    };

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token = default)
    {
        try
        {
            if (args.Length != 9 || args[0] != "import-proof" || args[1] != "--config" || args[3] != "--proof-root"
                || args[5] != "--published-root" || args[7] != "--out") throw Fail("ARGUMENTS");
            var draftPath = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            var proof = WebFormsReviewPreflightCommand.PhysicalPath(args[4]);
            var published = WebFormsReviewPreflightCommand.PhysicalPath(args[6]);
            var destination = WebFormsReviewPreflightCommand.PhysicalPath(args[8]);
            if (File.Exists(destination) || Directory.Exists(destination)) throw Fail("OUTPUT_EXISTS");
            var draftBytes = await Read(draftPath, token);
            var draft = JsonSerializer.Deserialize<WebFormsReviewConfig>(draftBytes, JsonOptions) ?? throw Fail("CONFIG_INVALID");
            if (draft.SchemaVersion != WebFormsReviewPreflightCommand.ConfigSchema || draft.Operation != "fresh"
                || draft.ParentScanRoot is not null || draft.BindingReceipts is not { Length: 0 }
                || draft.PrimaryAssemblies is not { Length: 0 } || draft.DependencyAssemblies is not { Length: 0 }
                || draft.PageMaps is not { Length: 0 } || draft.PdbInputs is not { Length: 0 }
                || draft.PublishSourceRelativePaths is { Length: > 0 }
                || draft.PublishReceiptRelativePath is not null || draft.ReceiptRoot is not null || draft.PreparationProvenance is not null
                || !Path.IsPathFullyQualified(draft.SourceRoot))
                throw Fail("FRESH_DRAFT_REQUIRED");
            if (!string.IsNullOrEmpty(draft.PublishedRoot) && !WebFormsReviewPreflightCommand.PhysicalPath(draft.PublishedRoot).Equals(published,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw Fail("DRAFT_PUBLISHED_ROOT_MISMATCH");
            var sourceRoot = WebFormsReviewPreflightCommand.PhysicalPath(draft.SourceRoot);
            foreach (var root in new[] { sourceRoot, published, proof, Path.GetDirectoryName(draftPath)! })
                if (Overlap(root, destination)) throw Fail("OUTPUT_OVERLAPS_INPUT");
            var publishPath = WebFormsReviewPreflightCommand.Child(proof, "publish-receipt.local.json");
            var bindingPath = WebFormsReviewPreflightCommand.Child(proof, "compiled-binding.local.json");
            var publishBytes = await Read(publishPath, token);
            var bindingBytes = await Read(bindingPath, token);
            using var publishDocument = JsonDocument.Parse(publishBytes);
            using var bindingDocument = JsonDocument.Parse(bindingBytes);
            var receipt = publishDocument.RootElement;
            var binding = bindingDocument.RootElement;
            if (Text(receipt, "schemaVersion") != "webforms-publish-binding.v1" || Text(receipt, "visibility") != "local-only"
                || Text(binding, "schemaVersion") != "compiled-input-binding-set.v1"
                || !Hex(Text(receipt, "receiptGeneratorSha256"), 64) || !Hex(Text(binding, "generatorSha256"), 64)
                || !Hex(Text(binding, "boundedInputSha256"), 64)) throw Fail("RECEIPT_SCHEMA_OR_PROVENANCE");
            var commit = Text(receipt, "sourceCommitSha");
            var git = GitMetadataProvider.Detect(sourceRoot);
            if (!Hex(commit, 40) || git.CommitSha != commit || string.IsNullOrWhiteSpace(git.RemoteUrl)) throw Fail("SOURCE_COMMIT_OR_REPOSITORY_MISMATCH");
            if (!string.IsNullOrEmpty(draft.SourceCommitSha) && draft.SourceCommitSha != commit) throw Fail("DRAFT_COMMIT_MISMATCH");
            var sources = Rows(receipt, "sourceFiles", 256);
            var files = Rows(receipt, "publishedFiles", 64);
            var inventory = Rows(receipt, "assemblyInventory", 256);
            var pages = Rows(receipt, "pages", 32);
            var bindings = Rows(binding, "bindings", 256);
            var inputCommitments = new List<object>();
            var primary = new List<string>(); var dependencies = new List<string>(); var maps = new List<string>();
            var sourceNames = UniquePaths(sources);
            var fileNames = UniquePaths(files);
            _ = UniquePaths(inventory);
            var sourceLines = new List<string>(); var assemblyLines = new List<string>(); var mapLines = new List<string>();
            foreach (var row in sources)
            {
                var name = Text(row, "path"); var sha = Text(row, "sha256");
                await Check("source", WebFormsReviewPreflightCommand.Child(sourceRoot, name), sha, 67_108_864);
                sourceLines.Add($"{name}:{sha}");
            }
            foreach (var row in inventory)
            {
                var name = Text(row, "path"); var sha = Text(row, "sha256"); var disposition = Text(row, "disposition");
                if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) throw Fail("ASSEMBLY_INVENTORY_INVALID");
                assemblyLines.Add($"{name}:{sha}:{disposition}");
                if (disposition == "selected") primary.Add(name);
                else if (disposition == "artifact-context-no-source-commit") dependencies.Add(name);
                else if (disposition != "operator-declared-out-of-scope") throw Fail("ASSEMBLY_DISPOSITION_INVALID");
                // Even out-of-scope recorded DLLs must still match the baseline inventory.
                await Check("published-inventory", WebFormsReviewPreflightCommand.Child(published, name), sha, 67_108_864);
            }
            foreach (var row in files)
            {
                var name = Text(row, "path"); var sha = Text(row, "sha256"); var kind = Text(row, "kind");
                if (kind == "assembly")
                {
                    var item = inventory.SingleOrDefault(item => Text(item, "path") == name);
                    if (item.ValueKind == JsonValueKind.Undefined || Text(item, "sha256") != sha
                        || !primary.Contains(name) && !dependencies.Contains(name)) throw Fail("PUBLISHED_INVENTORY_MISMATCH");
                }
                else if (kind == "compiled-map") { maps.Add(name); mapLines.Add($"{name}:{sha}"); }
                else throw Fail("PUBLISHED_KIND_INVALID");
                await Check("published", WebFormsReviewPreflightCommand.Child(published, name), sha, 67_108_864);
            }
            if (primary.Count == 0 || primary.Concat(dependencies).Any(name => !fileNames.Contains(name))) throw Fail("PRIMARY_INVENTORY_INCOMPLETE");
            var sourceDigest = Hash(Encoding.UTF8.GetBytes(string.Join('\n', sourceLines) + "\n"));
            var inventoryDigest = Hash(Encoding.UTF8.GetBytes(string.Join('\n', assemblyLines) + "\n"));
            var mapDigest = Hash(Encoding.UTF8.GetBytes(string.Join('\n', mapLines.Order(StringComparer.Ordinal)) + "\n"));
            if (Text(receipt, "boundedInputSha256") != sourceDigest || Text(receipt, "assemblyInventorySha256") != inventoryDigest
                || Text(receipt, "mapInventorySha256") != mapDigest) throw Fail("RECEIPT_DIGEST_MISMATCH");
            var repositoryDigest = Hash(Encoding.UTF8.GetBytes(git.RemoteUrl));
            var receiptDigest = Hash(Encoding.UTF8.GetBytes($"source:{repositoryDigest}\ncommit:{commit}\nsource:{sourceDigest}\nassemblies:{inventoryDigest}\nmaps:{mapDigest}\n"));
            if (Text(receipt, "receiptInputSha256") != receiptDigest) throw Fail("RECEIPT_REPOSITORY_DIGEST_MISMATCH");
            var boundHashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in bindings)
            {
                if (Text(item, "schemaVersion") != "compiled-input-binding.v1" || Text(item, "binarySourceCommitSha") != commit
                    || Text(item, "binarySourceRepository") != git.RemoteUrl || !boundHashes.Add(Text(item, "artifactSha256"))) throw Fail("BINDING_SOURCE_OR_DUPLICATE_MISMATCH");
            }
            var selectedHashes = inventory.Where(item => primary.Contains(Text(item, "path"))).Select(item => Text(item, "sha256")).ToHashSet(StringComparer.Ordinal);
            if (!boundHashes.SetEquals(selectedHashes) || selectedHashes.Count != primary.Count) throw Fail("BINDING_SELECTED_INVENTORY_MISMATCH");
            var bindingLines = bindings.OrderBy(item => Text(item, "safeLocator"), StringComparer.Ordinal)
                .Select(item => $"{Text(item, "safeLocator")}:{Text(item, "artifactSha256")}:{Text(item, "assemblyIdentity")}:{Text(item, "binarySourceCommitSha")}").ToList();
            bindingLines.AddRange([$"source:{sourceDigest}", $"source-repository:{repositoryDigest}", $"assembly-inventory:{inventoryDigest}", $"map-inventory:{mapDigest}"]);
            if (Text(binding, "boundedInputSha256") != Hash(Encoding.UTF8.GetBytes(string.Join('\n', bindingLines) + "\n"))) throw Fail("BINDING_DIGEST_MISMATCH");
            var pageNames = pages.Select(page => Text(page, "sourcePath")).ToArray();
            if (pageNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != pageNames.Length
                || pageNames.Any(page => !sourceNames.Contains(page) || !page.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))) throw Fail("PAGE_SCOPE_INVALID");
            var config = draft with { SourceRoot = sourceRoot, SourceCommitSha = commit, PublishedRoot = published,
                PrimaryAssemblies = primary.Order(StringComparer.Ordinal).ToArray(), DependencyAssemblies = dependencies.Order(StringComparer.Ordinal).ToArray(),
                BindingReceipts = ["compiled-binding.local.json"], PageMaps = maps.Order(StringComparer.Ordinal).ToArray(),
                ReceiptRoot = proof, PublishReceiptRelativePath = "publish-receipt.local.json", PublishSourceRelativePaths = null,
                PageMode = "selected", PageRelativePaths = pageNames, PdbInputs = [] };
            WebFormsReviewPreflightCommand.ValidateConfig(config);
            var membership = await WebFormsReviewPreparationCommand.ValidateCommittedSourceAsync(config, sourceNames.ToArray(), token);
            var generator = Hash(await File.ReadAllBytesAsync(typeof(WebFormsProofImportCommand).Assembly.Location, token));
            var bounded = Hash(JsonSerializer.SerializeToUtf8Bytes(new { draftSha256 = Hash(draftBytes), publishReceiptSha256 = Hash(publishBytes),
                bindingReceiptSha256 = Hash(bindingBytes), sourceCommitSha = commit, inputCommitments, membership }, JsonOptions));
            config = config with { PreparationProvenance = new(RuleId, generator, bounded) };
            var configBytes = JsonSerializer.SerializeToUtf8Bytes(config, JsonOptions);
            var parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            var staging = Path.Combine(parent, ".webforms-proof-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var stagedConfig = Path.Combine(staging, "review-config.local.json");
            await WriteNew(stagedConfig, configBytes, token);
            var plan = await WebFormsReviewPreflightCommand.BuildAsync(stagedConfig, destination, token);
            var validated = await WebFormsReviewInputValidation.ValidateAsync(plan, token);
            var primaryOutcomes = validated.CompiledProvenance.Outcomes.Where(item => item.Role == "primary").ToArray();
            if (primaryOutcomes.Length != primary.Count || primaryOutcomes.Any(item => item.Outcome != "admitted" || item.ProvenanceState != "bound")) throw Fail("RETAINED_BINDING_NOT_ADMITTED");
            await WebFormsReviewPreparationCommand.ValidateCommittedSourceAsync(config, sourceNames.ToArray(), token);
            await Same(draftPath, draftBytes); await Same(publishPath, publishBytes); await Same(bindingPath, bindingBytes);
            foreach (var row in inventory) await Check("recheck-inventory", WebFormsReviewPreflightCommand.Child(published, Text(row, "path")), Text(row, "sha256"), 67_108_864);
            foreach (var row in sources) await Check("recheck-source", WebFormsReviewPreflightCommand.Child(sourceRoot, Text(row, "path")), Text(row, "sha256"), 67_108_864);
            foreach (var row in files) await Check("recheck-published", WebFormsReviewPreflightCommand.Child(published, Text(row, "path")), Text(row, "sha256"), 67_108_864);
            var gitAfter = GitMetadataProvider.Detect(sourceRoot);
            if (gitAfter.CommitSha != git.CommitSha || gitAfter.RemoteUrl != git.RemoteUrl || gitAfter.GitRootPath != git.GitRootPath
                || gitAfter.ScanRootRelativePath != git.ScanRootRelativePath) throw Fail("SOURCE_IDENTITY_CHANGED");
            if (Hash(await Read(stagedConfig, token)) != Hash(configBytes)) throw Fail("OUTPUT_CONFIG_CHANGED");
            var audit = new { schemaVersion = "webforms-retained-proof-import.v1", ruleId = RuleId, evidenceTier = "Tier2Structural", visibility = "local-only",
                state = "verified-inputs-not-scanned", generatorSha256 = generator, boundedInputSha256 = bounded,
                outputConfigSha256 = Hash(configBytes), sourceCommitSha = commit, primaryAssemblies = primary.Count, dependencies = dependencies.Count,
                pages = pageNames.Length, retainedPublishReceiptSha256 = Hash(publishBytes), retainedBindingReceiptSha256 = Hash(bindingBytes),
                originalDraftSha256 = Hash(draftBytes), gaps = plan.Gaps.Concat(validated.Gaps).Distinct().Order(StringComparer.Ordinal).ToArray(),
                limitations = new[] { "Retained owner attestation is carried, not newly issued or authenticated compiler proof.",
                    "Selected page scope comes from the baseline receipt, not the wider migration draft; source/project scopes and budgets are preserved.",
                    "Inputs remain external. No scan, report comparison, all-pages claim, two-repository merge or runtime execution validation was performed.",
                    "Dependency context remains unbound; imported settings must pass native start gates again. Failed staging is retained but not admitted." } };
            await WriteNew(Path.Combine(staging, "proof-import.local.json"), JsonSerializer.SerializeToUtf8Bytes(audit, JsonOptions), token);
            token.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
            await output.WriteLineAsync($"proofImport=verified-inputs-not-scanned;primary={primary.Count};dependencies={dependencies.Count};pages={pageNames.Length};originalsUnchanged=true");
            await output.WriteLineAsync("nextAction=start-native-verification-from-review-config.local.json-in-a-new-output-folder");
            return 0;

            async Task Check(string role, string path, string sha, long limit)
            {
                if (!Hex(sha, 64)) throw Fail("INVENTORY_HASH_INVALID");
                var observed = await WebFormsReviewPreflightCommand.HashAsync(role, path, limit, token);
                if (observed.Sha256 != sha) throw Fail(role is "source" or "recheck-source" ? "SOURCE_BYTES_MISMATCH" : "PUBLISHED_BYTES_MISMATCH");
                if (!role.StartsWith("recheck", StringComparison.Ordinal)) inputCommitments.Add(new { role, observed.Path, observed.Bytes, observed.Sha256 });
            }
            async Task Same(string path, byte[] bytes) { if (Hash(await Read(path, token)) != Hash(bytes)) throw Fail("INPUT_CHANGED"); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (WebFormsReviewPreflightCommand.PreflightException ex) { await error.WriteLineAsync(ex.Code); return 1; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        { await error.WriteLineAsync(ex is ImportException ? ex.Message : "WEBFORMS_PROOF_IMPORT_INPUT_OR_OUTPUT_INVALID"); return 1; }
    }

    private static JsonElement[] Rows(JsonElement root, string name, int limit)
    {
        var value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 1 || value.GetArrayLength() > limit) throw Fail("RECEIPT_LIMIT_OR_SHAPE");
        return value.EnumerateArray().ToArray();
    }
    private static HashSet<string> UniquePaths(JsonElement[] rows)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows) if (!names.Add(Text(row, "path"))) throw Fail("DUPLICATE_PATH");
        return names;
    }
    private static string Text(JsonElement root, string name)
    {
        var item = root.GetProperty(name);
        if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()) || item.GetString()!.Any(char.IsControl)) throw Fail("RECEIPT_VALUE_INVALID");
        return item.GetString()!;
    }
    private static async Task<byte[]> Read(string path, CancellationToken token)
    {
        var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(path, 1_048_576, token);
        WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
        return bytes;
    }
    private static async Task WriteNew(string path, byte[] bytes, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes, token);
    }
    private static bool Overlap(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return first.Equals(second, comparison) || second.StartsWith(Prefix(first), comparison)
            || first.StartsWith(Prefix(second), comparison);
        static string Prefix(string path) => Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
    }
    private static bool Hex(string value, int length) => value.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static ImportException Fail(string code) => new("WEBFORMS_PROOF_IMPORT_" + code);
    private sealed class ImportException(string message) : InvalidOperationException(message);
}
