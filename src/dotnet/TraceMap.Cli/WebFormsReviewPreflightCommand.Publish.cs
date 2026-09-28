using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Cli;

public static partial class WebFormsReviewPreflightCommand
{
    internal const string PublishSetSchema = "webforms-publish-binding-set.v1";
    internal const string PublishInventorySchema = "webforms-publish-inventory-partition.v1";
    internal static bool IsPublishInventoryRole(string role) => role is "selected-page" or "preparation-source"
        or "publish-source" or "page-map" or "publish-receipt-partition";

    private static async Task ValidatePublishInventoryAsync(WebFormsReviewConfig config, string receiptRoot,
        List<WebFormsReviewInput> inputs, Func<string, string, long, Task<WebFormsReviewInput>> add, CancellationToken token)
    {
        var rootPath = Child(receiptRoot, config.PublishReceiptRelativePath!);
        var rootInput = await add("publish-receipt", rootPath, 1_048_576);
        var rootBytes = await ReadSmallAsync(rootPath, 1_048_576, token);
        if (Digest(rootBytes) != rootInput.Sha256) throw Fail("INPUT_CHANGED");
        RejectDuplicateProperties(rootBytes);
        using var rootDocument = JsonDocument.Parse(rootBytes, new JsonDocumentOptions { MaxDepth = 16 });
        var root = rootDocument.RootElement;
        Header(root);
        var isSet = root.GetProperty("schemaVersion").GetString() == PublishSetSchema;
        var pageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var globalSources = new Dictionary<string, (string Name, string Sha256)>(StringComparer.OrdinalIgnoreCase);
        var globalPublished = new Dictionary<string, (string Name, string Sha256, string Kind)>(StringComparer.OrdinalIgnoreCase);
        var byPath = inputs.ToDictionary(input => input.Path, PathComparer);
        if (isSet)
        {
            if (root.GetProperty("ruleId").GetString() != RuleIds.LegacyWebFormsPublishMap
                || root.GetProperty("claimLevel").GetString() != "operator-declared-review-only-not-build-proof"
                || !IsHex(root.GetProperty("receiptGeneratorSha256").GetString(), 64)
                || !IsHex(root.GetProperty("boundedInputSha256").GetString(), 64)) throw Fail("PUBLISH_RECEIPT_INVALID");
            var partitions = Array(root, "partitions", 64);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var partition in partitions.EnumerateArray())
            {
                token.ThrowIfCancellationRequested();
                var name = partition.GetProperty("path").GetString() ?? throw Fail("PUBLISH_RECEIPT_INVALID");
                var sha = partition.GetProperty("sha256").GetString();
                if (!names.Add(name) || !IsHex(sha, 64)) throw Fail("PUBLISH_RECEIPT_INVALID");
                var input = await add("publish-receipt-partition", Child(Path.GetDirectoryName(rootPath)!, name), 1_048_576);
                var bytes = await ReadSmallAsync(input.Path, 1_048_576, token);
                if (input.Sha256 != sha || Digest(bytes) != sha) throw Fail("PUBLISH_PARTITION_MISMATCH");
                RejectDuplicateProperties(bytes);
                using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
                await Member(document.RootElement, allowInventory: true);
            }
        }
        else await Member(root, allowInventory: false);
        if (pageNames.Count == 0) throw Fail("PUBLISH_PAGE_INVALID");
        if (config.PageMode == "selected" && config.PageRelativePaths.Any(page => !pageNames.Contains(page)))
            throw Fail("PUBLISH_SELECTED_PAGE_UNAVAILABLE");

        void Header(JsonElement value)
        {
            if (value.GetProperty("visibility").GetString() != "local-only"
                || value.GetProperty("sourceCommitSha").GetString() != config.SourceCommitSha) throw Fail("PUBLISH_RECEIPT_INVALID");
        }
        async Task Member(JsonElement value, bool allowInventory)
        {
            Header(value);
            var schema = value.GetProperty("schemaVersion").GetString();
            var inventory = allowInventory && schema == PublishInventorySchema;
            if (schema != "webforms-publish-binding.v1" && !inventory) throw Fail("PUBLISH_RECEIPT_INVALID");
            var sources = Array(value, "sourceFiles", 256);
            var published = Array(value, "publishedFiles", 64);
            var pages = Array(value, "pages", 32, inventory ? 0 : 1);
            if (inventory && pages.GetArrayLength() != 0) throw Fail("PUBLISH_RECEIPT_INVALID");
            var sourceNames = new HashSet<string>(PathComparer);
            foreach (var source in sources.EnumerateArray())
            {
                token.ThrowIfCancellationRequested();
                var name = source.GetProperty("path").GetString() ?? throw Fail("PUBLISH_RECEIPT_INVALID");
                var sha = source.GetProperty("sha256").GetString();
                if (!sourceNames.Add(name) || !IsHex(sha, 64)) throw Fail("PUBLISH_RECEIPT_INVALID");
                if (globalSources.TryGetValue(name, out var prior) && prior != (name, sha!)) throw Fail("PUBLISH_RECEIPT_INVALID");
                globalSources.TryAdd(name, (name, sha!));
                var path = Child(config.SourceRoot, name);
                if (!byPath.TryGetValue(path, out var input)) { input = await add("publish-source", path, 67_108_864); byPath.Add(path, input); }
                if (input.Sha256 != sha) throw Fail("PUBLISH_SOURCE_MISMATCH");
            }
            var publishedNames = new HashSet<string>(PathComparer);
            foreach (var file in published.EnumerateArray())
            {
                token.ThrowIfCancellationRequested();
                var name = file.GetProperty("path").GetString() ?? throw Fail("PUBLISH_RECEIPT_INVALID");
                var sha = file.GetProperty("sha256").GetString(); var kind = file.GetProperty("kind").GetString();
                if (!publishedNames.Add(name) || !IsHex(sha, 64) || kind is not ("assembly" or "compiled-map")) throw Fail("PUBLISH_RECEIPT_INVALID");
                if (globalPublished.TryGetValue(name, out var prior) && prior != (name, sha!, kind)) throw Fail("PUBLISH_RECEIPT_INVALID");
                globalPublished.TryAdd(name, (name, sha!, kind));
                if (!byPath.TryGetValue(Child(config.PublishedRoot, name), out var declared)
                    || !(kind == "assembly" ? declared.Role is "primary-assembly" or "dependency-assembly" : declared.Role == "page-map")
                    || declared.Sha256 != sha) throw Fail("PUBLISH_ARTIFACT_NOT_DECLARED_OR_MISMATCH");
            }
            foreach (var page in pages.EnumerateArray())
            {
                var sourcePath = page.TryGetProperty("sourcePath", out var source) && source.ValueKind == JsonValueKind.String
                    ? source.GetString() : page.GetProperty("virtualPath").GetString()?.TrimStart('/');
                if (sourcePath is null || !pageNames.Add(sourcePath) || !sourceNames.Contains(sourcePath)) throw Fail("PUBLISH_PAGE_INVALID");
            }
        }
        static JsonElement Array(JsonElement value, string name, int maximum, int minimum = 1)
        {
            var array = value.GetProperty(name);
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() < minimum || array.GetArrayLength() > maximum)
                throw Fail("PUBLISH_RECEIPT_LIMIT_OR_SHAPE_INVALID");
            return array;
        }
    }
}
