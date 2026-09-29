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
        // Closed, public-safe stage names: never print exception messages containing private paths.
        var stage = "arguments";
        try
        {
            var diagnoseOnly = args.Length > 0 && args[^1] == "--diagnose";
            var argumentCount = args.Length - (diagnoseOnly ? 1 : 0);
            if (argumentCount is not (9 or 11) || args[0] != "import-proof" || args[1] != "--config" || args[3] != "--proof-root"
                || args[5] != "--published-root" || args[7] != "--out"
                || argumentCount == 11 && args[9] != "--source-base") throw Fail("ARGUMENTS");
            var sourceBase = argumentCount == 11 ? args[10] : ".";
            if (sourceBase == ".") sourceBase = null;
            var draftPath = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            var proof = WebFormsReviewPreflightCommand.PhysicalPath(args[4]);
            var published = WebFormsReviewPreflightCommand.PhysicalPath(args[6]);
            var destination = WebFormsReviewPreflightCommand.PhysicalPath(args[8]);
            if (!diagnoseOnly && (File.Exists(destination) || Directory.Exists(destination))) throw Fail("OUTPUT_EXISTS");
            stage = "migration-draft";
            var draftBytes = await Read(draftPath, token);
            var draft = JsonSerializer.Deserialize<WebFormsReviewConfig>(draftBytes, JsonOptions) ?? throw Fail("CONFIG_INVALID");
            if (draft.SchemaVersion != WebFormsReviewPreflightCommand.ConfigSchema || draft.Operation != "fresh"
                || draft.ParentScanRoot is not null || draft.BindingReceipts is not { Length: 0 }
                || draft.PrimaryAssemblies is not { Length: 0 } || draft.DependencyAssemblies is not { Length: 0 }
                || draft.PageMaps is not { Length: 0 } || draft.PdbInputs is not { Length: 0 }
                || draft.PublishSourceRelativePaths is { Length: > 0 }
                || draft.PublishReceiptRelativePath is not null || draft.ReceiptRoot is not null || draft.PreparationProvenance is not null
                || draft.PublishSourceRelativeBase is not null
                || !Path.IsPathFullyQualified(draft.SourceRoot))
                throw Fail("FRESH_DRAFT_REQUIRED");
            if (!string.IsNullOrEmpty(draft.PublishedRoot) && !WebFormsReviewPreflightCommand.PhysicalPath(draft.PublishedRoot).Equals(published,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw Fail("DRAFT_PUBLISHED_ROOT_MISMATCH");
            var sourceRoot = WebFormsReviewPreflightCommand.PhysicalPath(draft.SourceRoot);
            stage = "receipt-source-base";
            var receiptSourceRoot = sourceBase is null ? sourceRoot : WebFormsReviewPreflightCommand.Child(sourceRoot, sourceBase);
            if (sourceBase?.Contains('\\') == true || !Directory.Exists(receiptSourceRoot)) throw Fail("SOURCE_BASE_INVALID");
            var sourceGit = GitMetadataProvider.Detect(sourceRoot);
            var baseGit = GitMetadataProvider.Detect(receiptSourceRoot);
            if (baseGit.GitRootPath != sourceGit.GitRootPath || baseGit.CommitSha != sourceGit.CommitSha
                || baseGit.RemoteUrl != sourceGit.RemoteUrl) throw Fail("SOURCE_BASE_REPOSITORY_MISMATCH");
            foreach (var root in new[] { sourceRoot, published, proof, Path.GetDirectoryName(draftPath)! })
                if (Overlap(root, destination)) throw Fail("OUTPUT_OVERLAPS_INPUT");
            var publishPath = WebFormsReviewPreflightCommand.Child(proof, "publish-receipt.local.json");
            var bindingPath = WebFormsReviewPreflightCommand.Child(proof, "compiled-binding.local.json");
            stage = "publish-receipt";
            var publishBytes = await Read(publishPath, token);
            stage = "binding-receipt";
            var bindingBytes = await Read(bindingPath, token);
            using var publishDocument = JsonDocument.Parse(publishBytes);
            using var bindingDocument = JsonDocument.Parse(bindingBytes);
            var receipt = publishDocument.RootElement;
            var binding = bindingDocument.RootElement;
            stage = "receipt-schema";
            if (Text(receipt, "schemaVersion") != "webforms-publish-binding.v1" || Text(receipt, "visibility") != "local-only"
                || Text(binding, "schemaVersion") != "compiled-input-binding-set.v1"
                || !Hex(Text(receipt, "receiptGeneratorSha256"), 64) || !Hex(Text(binding, "generatorSha256"), 64)
                || !Hex(Text(binding, "boundedInputSha256"), 64)) throw Fail("RECEIPT_SCHEMA_OR_PROVENANCE");
            var commit = Text(receipt, "sourceCommitSha");
            stage = "source-identity";
            var git = GitMetadataProvider.Detect(sourceRoot);
            if (!Hex(commit, 40) || git.CommitSha != commit || string.IsNullOrWhiteSpace(git.RemoteUrl)) throw Fail("SOURCE_COMMIT_OR_REPOSITORY_MISMATCH");
            if (!string.IsNullOrEmpty(draft.SourceCommitSha) && draft.SourceCommitSha != commit) throw Fail("DRAFT_COMMIT_MISMATCH");
            stage = "receipt-rosters";
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
            stage = "source-roster";
            foreach (var row in sources)
            {
                var name = Text(row, "path"); var sha = Text(row, "sha256");
                await Check("source", WebFormsReviewPreflightCommand.Child(receiptSourceRoot, name), sha, 67_108_864);
                sourceLines.Add($"{name}:{sha}");
            }
            stage = "assembly-inventory";
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
            stage = "published-roster";
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
            stage = "receipt-commitments";
            var sourceDigest = Hash(Encoding.UTF8.GetBytes(string.Join('\n', sourceLines) + "\n"));
            var inventoryDigest = Hash(Encoding.UTF8.GetBytes(string.Join('\n', assemblyLines) + "\n"));
            var mapDigest = Hash(Encoding.UTF8.GetBytes(string.Join('\n', mapLines.Order(StringComparer.Ordinal)) + "\n"));
            if (Text(receipt, "boundedInputSha256") != sourceDigest || Text(receipt, "assemblyInventorySha256") != inventoryDigest
                || Text(receipt, "mapInventorySha256") != mapDigest) throw Fail("RECEIPT_DIGEST_MISMATCH");
            var repositoryDigest = Hash(Encoding.UTF8.GetBytes(git.RemoteUrl));
            var receiptDigest = Hash(Encoding.UTF8.GetBytes($"source:{repositoryDigest}\ncommit:{commit}\nsource:{sourceDigest}\nassemblies:{inventoryDigest}\nmaps:{mapDigest}\n"));
            if (Text(receipt, "receiptInputSha256") != receiptDigest) throw Fail("RECEIPT_REPOSITORY_DIGEST_MISMATCH");
            stage = "binding-commitments";
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
            stage = "page-scope";
            var pageNames = pages.Select(page => Text(page, "sourcePath")).ToArray();
            if (pageNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != pageNames.Length
                || pageNames.Any(page => !sourceNames.Contains(page) || !page.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))) throw Fail("PAGE_SCOPE_INVALID");
            var config = draft with { SourceRoot = sourceRoot, SourceCommitSha = commit, PublishedRoot = published,
                PrimaryAssemblies = primary.Order(StringComparer.Ordinal).ToArray(), DependencyAssemblies = dependencies.Order(StringComparer.Ordinal).ToArray(),
                BindingReceipts = ["compiled-binding.local.json"], PageMaps = maps.Order(StringComparer.Ordinal).ToArray(),
                ReceiptRoot = proof, PublishReceiptRelativePath = "publish-receipt.local.json", PublishSourceRelativePaths = null,
                PageMode = "selected", PageRelativePaths = pageNames.Select(RepoName).ToArray(), PdbInputs = [],
                PublishSourceRelativeBase = sourceBase };
            stage = "source-membership";
            WebFormsReviewPreflightCommand.ValidateConfig(config);
            var repoSourceNames = sourceNames.Select(RepoName).ToArray();
            var membership = await WebFormsReviewPreparationCommand.ValidateCommittedSourceAsync(config, repoSourceNames, token);
            if (diagnoseOnly)
            {
                stage = "binding-admission";
                var budget = config.Budgets;
                var inspection = ManagedMetadataExtractor.InspectInputs(new ScanOptions(sourceRoot, "unused-read-only-diagnostic",
                    CompiledInputPaths: primary.Select(name => WebFormsReviewPreflightCommand.Child(published, name)).ToArray(),
                    CompiledDependencyPaths: dependencies.Select(name => WebFormsReviewPreflightCommand.Child(published, name)).ToArray(),
                    CompiledBindingReceiptPaths: [bindingPath],
                    CompiledInputLimits: new(MaxArtifactCount: budget.MaxInputFiles, MaxFileSizeBytes: budget.MaxAssemblyBytes,
                        MaxTextLength: budget.MetadataMaxText, MaxTotalWorkUnits: budget.MetadataMaxWork)), commit, token);
                await PrintAdmission(inspection.Provenance, inspection.GapKinds);
                await output.WriteLineAsync("proofImportDiagnostic=read-only;no-configuration-written;no-scan-started");
                if (!Admitted(inspection.Provenance)) throw Fail("RETAINED_BINDING_NOT_ADMITTED");
                return 0;
            }
            var generator = Hash(await File.ReadAllBytesAsync(typeof(WebFormsProofImportCommand).Assembly.Location, token));
            var bounded = Hash(JsonSerializer.SerializeToUtf8Bytes(new { draftSha256 = Hash(draftBytes), publishReceiptSha256 = Hash(publishBytes),
                bindingReceiptSha256 = Hash(bindingBytes), sourceCommitSha = commit, sourceRelativeBase = sourceBase, inputCommitments, membership }, JsonOptions));
            config = config with { PreparationProvenance = new(RuleId, generator, bounded) };
            var configBytes = JsonSerializer.SerializeToUtf8Bytes(config, JsonOptions);
            var parent = Path.GetDirectoryName(destination)!;
            stage = "output-staging";
            Directory.CreateDirectory(parent);
            var staging = Path.Combine(parent, ".webforms-proof-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var stagedConfig = Path.Combine(staging, "review-config.local.json");
            await WriteNew(stagedConfig, configBytes, token);
            stage = "native-preflight";
            var plan = await WebFormsReviewPreflightCommand.BuildAsync(stagedConfig, destination, token);
            stage = "binding-admission";
            var validated = await WebFormsReviewInputValidation.ValidateAsync(plan, token);
            if (!Admitted(validated.CompiledProvenance))
            {
                await PrintAdmission(validated.CompiledProvenance, validated.Gaps);
                throw Fail("RETAINED_BINDING_NOT_ADMITTED");
            }
            stage = "input-recheck";
            await WebFormsReviewPreparationCommand.ValidateCommittedSourceAsync(config, repoSourceNames, token);
            await Same(draftPath, draftBytes); await Same(publishPath, publishBytes); await Same(bindingPath, bindingBytes);
            foreach (var row in inventory) await Check("recheck-inventory", WebFormsReviewPreflightCommand.Child(published, Text(row, "path")), Text(row, "sha256"), 67_108_864);
            foreach (var row in sources) await Check("recheck-source", WebFormsReviewPreflightCommand.Child(receiptSourceRoot, Text(row, "path")), Text(row, "sha256"), 67_108_864);
            foreach (var row in files) await Check("recheck-published", WebFormsReviewPreflightCommand.Child(published, Text(row, "path")), Text(row, "sha256"), 67_108_864);
            var gitAfter = GitMetadataProvider.Detect(sourceRoot);
            var baseGitAfter = GitMetadataProvider.Detect(receiptSourceRoot);
            if (gitAfter.CommitSha != git.CommitSha || gitAfter.RemoteUrl != git.RemoteUrl || gitAfter.GitRootPath != git.GitRootPath
                || gitAfter.ScanRootRelativePath != git.ScanRootRelativePath || baseGitAfter.GitRootPath != git.GitRootPath
                || baseGitAfter.CommitSha != git.CommitSha || baseGitAfter.RemoteUrl != git.RemoteUrl
                || baseGitAfter.ScanRootRelativePath != baseGit.ScanRootRelativePath) throw Fail("SOURCE_IDENTITY_CHANGED");
            if (Hash(await Read(stagedConfig, token)) != Hash(configBytes)) throw Fail("OUTPUT_CONFIG_CHANGED");
            var audit = new { schemaVersion = "webforms-retained-proof-import.v1", ruleId = RuleId, evidenceTier = "Tier2Structural", visibility = "local-only",
                state = "verified-inputs-not-scanned", generatorSha256 = generator, boundedInputSha256 = bounded,
                outputConfigSha256 = Hash(configBytes), sourceCommitSha = commit, sourceRelativeBase = sourceBase,
                primaryAssemblies = primary.Count, dependencies = dependencies.Count,
                pages = pageNames.Length, retainedPublishReceiptSha256 = Hash(publishBytes), retainedBindingReceiptSha256 = Hash(bindingBytes),
                originalDraftSha256 = Hash(draftBytes), gaps = plan.Gaps.Concat(validated.Gaps).Distinct().Order(StringComparer.Ordinal).ToArray(),
                limitations = new[] { "Retained owner attestation is carried, not newly issued or authenticated compiler proof.",
                    "Selected page scope comes from the baseline receipt, not the wider migration draft; source/project scopes and budgets are preserved.",
                    "Inputs remain external. No scan, report comparison, all-pages claim, two-repository merge or runtime execution validation was performed.",
                    "Dependency context remains unbound; imported settings must pass native start gates again. Failed staging is retained but not admitted." } };
            stage = "output-publication";
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
            string RepoName(string name) => sourceBase is null ? name : sourceBase + "/" + name;
            bool Admitted(CompiledInputProvenance? provenance)
            {
                var outcomes = provenance?.Outcomes.Where(item => item.Role == "primary").ToArray();
                return outcomes?.Length == primary.Count && outcomes.All(item => item.Outcome == "admitted" && item.ProvenanceState == "bound");
            }
            async Task PrintAdmission(CompiledInputProvenance? provenance, IReadOnlyList<string> gaps)
            {
                var outcomes = provenance?.Outcomes.Where(item => item.Role == "primary").ToArray() ?? [];
                var locators = bindings.Select(item => Text(item, "safeLocator")).ToHashSet(StringComparer.Ordinal);
                await output.WriteLineAsync($"bindingDiagnostic=counts-only;primaryExpected={primary.Count};primaryObserved={outcomes.Length};omitted={provenance?.OmittedInputCount ?? 0};locatorMatches={outcomes.Count(item => locators.Contains(item.SafeLocator))};retainedTraversalLocators={locators.Count(value => value.StartsWith("../", StringComparison.Ordinal) || value.StartsWith("..\\", StringComparison.Ordinal))}");
                foreach (var group in outcomes.GroupBy(item => (item.Outcome, item.ProvenanceState)).OrderBy(group => group.Key.Outcome, StringComparer.Ordinal)
                    .ThenBy(group => group.Key.ProvenanceState, StringComparer.Ordinal))
                    await output.WriteLineAsync($"bindingDiagnostic.outcome={SafeCode(group.Key.Outcome)};state={SafeCode(group.Key.ProvenanceState)};count={group.Count()}");
                foreach (var gap in outcomes.SelectMany(item => item.GapKinds).Concat(gaps).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                    await output.WriteLineAsync("bindingDiagnostic.gap=" + SafeCode(gap));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (WebFormsReviewPreflightCommand.PreflightException ex) { return await Report(ex.Code); }
        catch (WebFormsReviewPreparationCommand.PreparationException ex) { return await Report(ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            var code = ex switch
            {
                ImportException => ex.Message,
                FileNotFoundException or DirectoryNotFoundException => "WEBFORMS_PROOF_IMPORT_FILE_OR_DIRECTORY_UNAVAILABLE",
                UnauthorizedAccessException => "WEBFORMS_PROOF_IMPORT_ACCESS_DENIED",
                JsonException => "WEBFORMS_PROOF_IMPORT_JSON_INVALID",
                IOException => "WEBFORMS_PROOF_IMPORT_IO_FAILED",
                _ => "WEBFORMS_PROOF_IMPORT_INPUT_OR_OUTPUT_INVALID"
            };
            return await Report(code);
        }

        async Task<int> Report(string code)
        {
            await error.WriteLineAsync($"{code};stage={stage};no-scan-started");
            return 1;
        }
    }

    private static JsonElement[] Rows(JsonElement root, string name, int limit)
    {
        var value = Required(root, name);
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
        var item = Required(root, name);
        if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()) || item.GetString()!.Any(char.IsControl)) throw Fail("RECEIPT_VALUE_INVALID");
        return item.GetString()!;
    }
    private static JsonElement Required(JsonElement root, string name)
    {
        // name is supplied only by this command, never by receipt content.
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value))
            throw Fail("RECEIPT_FIELD_UNAVAILABLE;field=" + name);
        return value;
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
    private static string SafeCode(string code) => code.Length is > 0 and <= 96 && code.All(character => char.IsAsciiLetterOrDigit(character) || character == '-') ? code : "other";
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static ImportException Fail(string code) => new("WEBFORMS_PROOF_IMPORT_" + code);
    private sealed class ImportException(string message) : InvalidOperationException(message);
}
