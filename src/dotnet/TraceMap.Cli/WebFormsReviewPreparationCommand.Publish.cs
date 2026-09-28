using System.Text;
using TraceMap.Core;

namespace TraceMap.Cli;

public static partial class WebFormsReviewPreparationCommand
{
    private sealed record PublishPartitionReference(string Path, string Sha256);
    private static async Task<IReadOnlyList<string>> WritePublishReceiptsAsync(string staging, WebFormsReviewConfig config,
        string generator, string inputHash, SourceFile[] sources, PublishedFile[] published, List<Page> pages, CancellationToken token)
    {
        const string rootName = "publish-receipt.local.json";
        if (sources.Length <= 256 && published.Length <= 64 && pages.Count <= 32)
        {
            await WriteJsonAsync(Path.Combine(staging, rootName), Document("webforms-publish-binding.v1", sources, published, pages), 1_048_576, token);
            return [rootName];
        }
        var sourceByName = sources.ToDictionary(source => source.Path, StringComparer.Ordinal);
        var publishedByName = published.ToDictionary(file => file.Path, StringComparer.Ordinal);
        var remainingSources = new SortedDictionary<string, SourceFile>(sourceByName, StringComparer.Ordinal);
        var remainingPublished = new SortedDictionary<string, PublishedFile>(publishedByName, StringComparer.Ordinal);
        var partitions = new List<PublishPartitionReference>();
        var artifacts = new List<string>();
        foreach (var pageGroup in pages.OrderBy(page => page.SourcePath, StringComparer.Ordinal).Chunk(32))
        {
            token.ThrowIfCancellationRequested();
            var partSources = new SortedDictionary<string, SourceFile>(StringComparer.Ordinal);
            var partPublished = new SortedDictionary<string, PublishedFile>(StringComparer.Ordinal);
            foreach (var page in pageGroup)
            {
                partSources.TryAdd(page.SourcePath, sourceByName[page.SourcePath]);
                if (page.BindingKind == "mapless-source-type-candidate")
                {
                    var context = published.First(file => file.Kind == "assembly"
                        && Path.GetFileName(file.Path).StartsWith("App_Web_", StringComparison.OrdinalIgnoreCase));
                    partPublished.TryAdd(context.Path, context);
                }
                else
                {
                    var assembly = "bin/" + page.Assembly + ".dll";
                    partPublished.TryAdd(assembly, publishedByName[assembly]);
                    partPublished.TryAdd(page.MapPath!, publishedByName[page.MapPath!]);
                }
            }
            if (partPublished.Count > 64) throw Fail("PAGE_PARTITION_ARTIFACT_LIMIT");
            foreach (var name in partSources.Keys) remainingSources.Remove(name);
            foreach (var name in partPublished.Keys) remainingPublished.Remove(name);
            Fill(partSources, remainingSources, 256); Fill(partPublished, remainingPublished, 64);
            await WritePart("webforms-publish-binding.v1", partSources.Values.ToArray(), partPublished.Values.ToArray(), pageGroup);
        }
        while (remainingSources.Count > 0 || remainingPublished.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var partSources = new SortedDictionary<string, SourceFile>(StringComparer.Ordinal);
            var partPublished = new SortedDictionary<string, PublishedFile>(StringComparer.Ordinal);
            Fill(partSources, remainingSources, 256); Fill(partPublished, remainingPublished, 64);
            if (partSources.Count == 0) partSources.Add(sources[0].Path, sources[0]);
            if (partPublished.Count == 0) partPublished.Add(published[0].Path, published[0]);
            await WritePart(WebFormsReviewPreflightCommand.PublishInventorySchema, partSources.Values.ToArray(), partPublished.Values.ToArray(), []);
        }
        var framed = WebFormsReviewPreflightCommand.PublishSetSchema + "\ngenerator:" + generator + "\ncommit:" + config.SourceCommitSha + "\n"
            + string.Join("\n", partitions.OrderBy(part => part.Path, StringComparer.Ordinal).Select(part => part.Path + ":" + part.Sha256)) + "\n";
        var set = new
        {
            schemaVersion = WebFormsReviewPreflightCommand.PublishSetSchema, ruleId = RuleIds.LegacyWebFormsPublishMap,
            visibility = "local-only", claimLevel = "operator-declared-review-only-not-build-proof", receiptGeneratorSha256 = generator,
            sourceCommitSha = config.SourceCommitSha, boundedInputSha256 = Digest(Encoding.UTF8.GetBytes(framed)), partitions
        };
        await WriteJsonAsync(Path.Combine(staging, rootName), set, 1_048_576, token);
        artifacts.Add(rootName);
        return artifacts;

        object Document(string schema, SourceFile[] partSources, PublishedFile[] partPublished, IReadOnlyList<Page> partPages)
        {
            var maps = partPublished.Where(file => file.Kind == "compiled-map").OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
            return new
            {
                schemaVersion = schema, ruleId = RuleId, visibility = "local-only", receiptGeneratorSha256 = generator,
                sourceCommitSha = config.SourceCommitSha,
                boundedInputSha256 = Digest(Encoding.UTF8.GetBytes(string.Join("\n", partSources.OrderBy(source => source.Path, StringComparer.Ordinal)
                    .Select(source => source.Path + ":" + source.Sha256)) + "\n")), receiptInputSha256 = inputHash,
                compilerSha256 = Digest(Encoding.UTF8.GetBytes("operator-declared-existing-publish-compiler-unavailable.v1")),
                compilerProvenance = "unavailable-existing-output", publishedMapCount = maps.Length,
                mapInventorySha256 = Digest(Encoding.UTF8.GetBytes(string.Join("\n", maps.Select(file => file.Path + ":" + file.Sha256)) + "\n")),
                sourceFiles = partSources, publishedFiles = partPublished, pages = partPages
            };
        }
        async Task WritePart(string schema, SourceFile[] partSources, PublishedFile[] partPublished, IReadOnlyList<Page> partPages)
        {
            if (partitions.Count >= 64) throw Fail("RECEIPT_PARTITION_COUNT_LIMIT");
            var name = $"publish-partitions/{partitions.Count + 1:D4}.local.json";
            var path = Path.Combine(staging, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await WriteJsonAsync(path, Document(schema, partSources, partPublished, partPages), 1_048_576, token);
            var actual = await WebFormsReviewPreflightCommand.HashAsync("publish-receipt-partition", path, 1_048_576, token);
            partitions.Add(new(name, actual.Sha256)); artifacts.Add(name);
        }
        static void Fill<T>(SortedDictionary<string, T> destination, SortedDictionary<string, T> remaining, int maximum)
        {
            foreach (var name in remaining.Keys.Take(maximum - destination.Count).ToArray())
            {
                destination.Add(name, remaining[name]); remaining.Remove(name);
            }
        }
    }
}
