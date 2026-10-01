using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TraceMap.Cli;

/// <summary>Local draft conversion only: never scans, attests, discovers DLLs or rewrites inputs.</summary>
public static class WebFormsConfigMigrationCommand
{
    public const string RuleId = "workflow.webforms.config-migration.v1";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token = default)
    {
        try
        {
            if (args.Length != 5 || args[0] != "migrate-config" || args[1] is not ("--config" or "--review-root") || args[3] != "--out")
                throw Invalid("ARGUMENTS");
            var input = args[2];
            if (args[1] == "--review-root")
            {
                var json = Path.Combine(input, "config", "webforms-review.json");
                var jsonc = Path.Combine(input, "config", "webforms-review.jsonc");
                if (File.Exists(json) == File.Exists(jsonc)) throw Invalid("CONFIG_SELECTION");
                input = File.Exists(jsonc) ? jsonc : json;
            }
            var destination = Path.GetFullPath(args[4]);
            if (Directory.Exists(destination) || File.Exists(destination)) throw Invalid("OUTPUT_EXISTS");
            byte[] bytes;
            await using (var stream = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length is <= 0 or > 1_048_576) throw Invalid("INPUT_LIMIT");
                bytes = new byte[checked((int)stream.Length)];
                await stream.ReadExactlyAsync(bytes, token);
                if (stream.ReadByte() != -1) throw Invalid("INPUT_CHANGED");
            }
            var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, MaxDepth = 20 });
            var root = document.RootElement;
            RejectDuplicates(root);
            Shape(root, "schemaVersion", "sourceRoot", "webFormsFolder", "backendFolder", "controlsFolder", "projectSelection", "outputRoot", "pageSelection");
            if (String(root, "schemaVersion") != "focused-webforms-review-config.v1") throw Invalid("SCHEMA");
            var source = String(root, "sourceRoot");
            var previousOutput = String(root, "outputRoot");
            var folders = new[] { String(root, "webFormsFolder"), String(root, "backendFolder"), String(root, "controlsFolder") }.Distinct(StringComparer.Ordinal).ToArray();
            var project = root.GetProperty("projectSelection");
            Shape(project, "mode", "solutionRelativePath", "projectRelativePaths");
            var mode = String(project, "mode").ToLowerInvariant();
            var solution = OptionalString(project, "solutionRelativePath");
            var projects = Strings(project, "projectRelativePaths");
            if (mode is not ("solution" or "projects" or "projectless" or "discover")
                || (mode == "solution" ? solution is null : solution is not null)
                || (mode == "projects" ? projects.Length == 0 : projects.Length != 0)
                || projects.Any(path => !path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)))
                throw Invalid("PROJECT_SELECTION");
            var pages = root.GetProperty("pageSelection");
            Shape(pages, "mode", "forms");
            var pageMode = String(pages, "mode").ToLowerInvariant();
            var forms = Strings(pages, "forms");
            if (pageMode is not ("all" or "selected") || (pageMode == "all" ? forms.Length != 0 : forms.Length is < 1 or > 10_000)
                || forms.Any(path => !path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))) throw Invalid("PAGE_SELECTION");
            var missing = new List<string> { "sourceCommitSha", "publishedRoot", "primaryAssemblies", "publishSourceRelativePaths", "binding-or-publish-receipts-or-explicit-owner-attestation" };
            if (mode == "discover") missing.Add("explicit-projectMode-and-project-selection");
            if (forms.Any(path => path.StartsWith('/') || path.StartsWith('\\'))) missing.Add("repository-relative-page-paths-not-virtual-routes");
            var draft = new WebFormsReviewConfig(WebFormsReviewPreflightCommand.ConfigSchema, "fresh", source, "",
                mode == "discover" ? "" : mode, solution, projects, folders, pageMode, forms, "", [], [], [], [], [], null,
                new WebFormsReviewBudgets(), PublishSourceRelativePaths: []);
            var draftBytes = JsonSerializer.SerializeToUtf8Bytes(draft, JsonOptions);
            var receipt = new
            {
                schemaVersion = "webforms-config-migration.v1", ruleId = RuleId, evidenceTier = "Tier3SyntaxOrTextual",
                visibility = "local-only", state = "draft-needs-owner-input", readyToRun = false,
                generatorSha256 = Hash(await File.ReadAllBytesAsync(typeof(WebFormsConfigMigrationCommand).Assembly.Location, token)),
                boundedInputSha256 = Hash(bytes), inputSchema = "focused-webforms-review-config.v1",
                outputConfig = "review.draft.json", outputConfigSha256 = Hash(draftBytes), missingInputs = missing,
                legacyProjectMode = mode, legacyOutputRoot = previousOutput,
                limitations = new[] {
                    "Original config and source/scan/published inputs are not modified or inspected.",
                    "No Git commit, DLL inventory, source-to-DLL binding or owner attestation is inferred.",
                    "The draft is not runnable: fill missing fields, verify paths and run native preflight before collection.",
                    "Two separate source repositories remain separate configurations; migration does not replace their merge workflow.",
                    "Legacy outputRoot is retained only in this local receipt; choose a new native output root explicitly.",
                    "Budgets use native defaults, not inferred capacity; configuration migration is not scan or runtime proof."
                }
            };
            // Never overwrite either an old configuration or a previously generated draft.
            var parent = Path.GetDirectoryName(destination) ?? throw Invalid("OUTPUT_INVALID");
            Directory.CreateDirectory(parent);
            var staging = Path.Combine(parent, $".webforms-config-migration-{Guid.NewGuid():N}");
            Directory.CreateDirectory(staging);
            await WriteNew(Path.Combine(staging, "review.draft.json"), draftBytes, token);
            await WriteNew(Path.Combine(staging, "migration.local.json"), JsonSerializer.SerializeToUtf8Bytes(receipt, JsonOptions), token);
            token.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
            await output.WriteLineAsync($"configMigration=draft;readyToRun=false;missingInputs={missing.Count};originalUnchanged=true");
            await output.WriteLineAsync("nextAction=edit-review.draft.json-using-migration.local.json-then-run-preflight");
            return 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            await error.WriteLineAsync(ex is MigrationException ? ex.Message : "WEBFORMS_CONFIG_MIGRATION_INPUT_OR_OUTPUT_INVALID");
            return 1;
        }
    }

    private static async Task WriteNew(string path, byte[] bytes, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes, token);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static MigrationException Invalid(string code) => new($"WEBFORMS_CONFIG_MIGRATION_{code}");
    private sealed class MigrationException(string message) : InvalidOperationException(message);
    private static void Shape(JsonElement item, params string[] names)
    {
        if (item.ValueKind != JsonValueKind.Object) throw Invalid("SHAPE");
        var actual = item.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != names.Length || actual.Any(name => !names.Contains(name, StringComparer.Ordinal))) throw Invalid("SHAPE");
    }
    private static string String(JsonElement item, string name) => OptionalString(item, name) ?? throw Invalid("VALUE_REQUIRED");
    private static string? OptionalString(JsonElement item, string name)
    {
        var value = item.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw Invalid("VALUE_INVALID");
        var raw = value.GetString()!;
        if (raw.Any(char.IsControl)) throw Invalid("VALUE_INVALID");
        var result = raw.Trim();
        return result.Length == 0 ? null : result;
    }
    private static string[] Strings(JsonElement item, string name)
    {
        var value = item.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 10_000) throw Invalid("ARRAY_INVALID");
        return value.EnumerateArray().Select(element => element.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(element.GetString()) && !element.GetString()!.Any(char.IsControl)
            ? element.GetString()!.Trim() : throw Invalid("ARRAY_INVALID")).ToArray();
    }
    private static void RejectDuplicates(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in item.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Invalid("DUPLICATE_PROPERTY");
                RejectDuplicates(property.Value);
            }
        }
        else if (item.ValueKind == JsonValueKind.Array) foreach (var child in item.EnumerateArray()) RejectDuplicates(child);
    }
}
