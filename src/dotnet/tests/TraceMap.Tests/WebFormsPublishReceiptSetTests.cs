using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsPublishReceiptSetTests
{
    [Fact]
    public void Partition_set_admits_67_pages_and_preserves_global_unique_membership_and_input_bytes()
    {
        using var fixture = new Fixture();
        var before = fixture.Hashes();
        var result = fixture.Evaluate();
        Assert.Equal("bound", result.Provenance!.Status);
        Assert.Equal(67, result.Provenance.PageCount);
        Assert.Equal(67, result.Provenance.SourceFileCount);
        Assert.Equal(68, result.Provenance.PublishedFileCount);
        Assert.Equal(67, result.Pages.Count);
        Assert.Equal(67, result.SourcePaths.Count);
        Assert.Single(result.Assemblies);
        Assert.Empty(result.Candidates!);
        Assert.Equal(Hash(typeof(WebFormsPublishInputInspector).Assembly.Location), result.Provenance.GeneratorSha256);
        Assert.NotEqual(Hash(fixture.SetPath), result.Provenance.BoundedInputSha256);
        Assert.Equal(before.OrderBy(item => item.Key), fixture.Hashes().OrderBy(item => item.Key));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(fixture.Evaluate()));
        var facts = WebFormsPublishMapExtractor.MaterializeFacts(Manifest(), result);
        Assert.Equal(67, facts.Count(fact => fact.FactType == FactTypes.WebFormsPublishPageMapped));
        Assert.Equal(67, facts.Count(fact => fact.FactType == FactTypes.WebFormsPublishSourceBound));
        Assert.Single(facts, fact => fact.FactType == FactTypes.WebFormsPublishAssemblyBound);
        Assert.All(facts, fact =>
        {
            Assert.Equal(RuleIds.LegacyWebFormsPublishMap, fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
            Assert.Equal(result.Provenance.GeneratorSha256, fact.Properties["generatorSha256"]);
            Assert.Equal(result.Provenance.BoundedInputSha256, fact.Properties["boundedInputSha256"]);
        });
    }

    [Theory]
    [InlineData("tamper", "WebFormsPublishPartitionMismatch")]
    [InlineData("duplicate-page", "WebFormsPublishReceiptSetAmbiguous")]
    [InlineData("nested", "WebFormsPublishReceiptSetInvalid")]
    [InlineData("escape", "WebFormsPublishUnsafePath")]
    [InlineData("wrong-commit", "WebFormsPublishReceiptInvalid")]
    [InlineData("wrong-generator-shape", "WebFormsPublishReceiptSetInvalid")]
    [InlineData("wrong-input", "WebFormsPublishReceiptSetInvalid")]
    [InlineData("too-many", "WebFormsPublishReceiptSetInvalid")]
    [InlineData("duplicate-key", "WebFormsPublishReceiptSetAmbiguous")]
    [InlineData("unknown-field", "WebFormsPublishReceiptUnreadable")]
    [InlineData("null-partition", "WebFormsPublishReceiptSetInvalid")]
    [InlineData("null-source", "WebFormsPublishReceiptSetInvalid")]
    public void Any_invalid_partition_or_set_withholds_all_binding_evidence(string mutation, string gap)
    {
        using var fixture = new Fixture();
        switch (mutation)
        {
            case "tamper": File.AppendAllText(fixture.PartPath(2), " "); break;
            case "duplicate-page": File.Copy(fixture.PartPath(0), fixture.PartPath(2), overwrite: true); fixture.WriteSet(); break;
            case "nested": File.Copy(fixture.SetPath, fixture.PartPath(2), overwrite: true); fixture.WriteSet(); break;
            case "escape": fixture.Partitions[2] = new("../outside.json", new string('a', 64)); fixture.WriteSet(refreshHashes: false); break;
            case "wrong-commit": fixture.MutatePart(2, part => part["sourceCommitSha"] = new string('b', 40)); break;
            case "wrong-generator-shape": fixture.MutateSet(set => set["receiptGeneratorSha256"] = "not-a-hash"); break;
            case "wrong-input": fixture.MutateSet(set => set["boundedInputSha256"] = new string('c', 64)); break;
            case "too-many": fixture.MutateSet(set => { var rows = set["partitions"]!.AsArray(); for (var index = 3; index < 65; index++) rows.Add(new JsonObject { ["path"] = $"extra-{index}.json", ["sha256"] = new string('a', 64) }); }); break;
            case "duplicate-key": File.WriteAllText(fixture.SetPath, File.ReadAllText(fixture.SetPath).Replace("\"visibility\":\"local-only\"", "\"visibility\":\"local-only\",\"visibility\":\"local-only\"", StringComparison.Ordinal)); break;
            case "unknown-field": fixture.MutateSet(set => set["extra"] = "rejected"); break;
            case "null-partition": fixture.MutateSet(set => set["partitions"]!.AsArray()[0] = null); break;
            case "null-source": fixture.MutatePart(2, part => part["sourceFiles"]!.AsArray()[0] = null); break;
        }
        var result = fixture.Evaluate();
        Assert.Equal("gap", result.Provenance!.Status);
        Assert.Contains(gap, result.Provenance.GapKinds);
        Assert.Empty(result.Pages); Assert.Empty(result.SourcePaths); Assert.Empty(result.Assemblies); Assert.Empty(result.Candidates!);
        Assert.All(WebFormsPublishMapExtractor.MaterializeFacts(Manifest(), result), fact =>
        {
            Assert.Equal(FactTypes.AnalysisGap, fact.FactType);
            Assert.Equal(EvidenceTiers.Tier4Unknown, fact.EvidenceTier);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Global_maps_cannot_be_hidden_in_another_partition(bool mapless)
    {
        using var fixture = new Fixture();
        var mapPath = "Pages/Public00.aspx.compiled";
        if (mapless)
            fixture.MutatePart(0, part =>
            {
                var page = part["pages"]!.AsArray()[0]!.AsObject();
                page["assembly"] = null; page["generatedType"] = null; page["mapPath"] = null;
                page["bindingKind"] = "mapless-source-type-candidate";
                var published = part["publishedFiles"]!.AsArray();
                published.Remove(published.Single(item => item!["path"]!.GetValue<string>() == mapPath));
                Fixture.RefreshMapDigest(part);
            });
        else
        {
            var duplicate = "Pages/duplicate.compiled";
            File.Copy(Path.Combine(fixture.Published, mapPath), Path.Combine(fixture.Published, duplicate));
            mapPath = duplicate;
        }
        fixture.MutatePart(2, part => part["publishedFiles"]!.AsArray().Add(new JsonObject
        {
            ["path"] = mapPath, ["sha256"] = Hash(Path.Combine(fixture.Published, mapPath)), ["kind"] = "compiled-map"
        }));
        var result = fixture.Evaluate();
        Assert.Equal("gap", result.Provenance!.Status);
        Assert.Contains("WebFormsPublishReceiptSetMapAmbiguous", result.Provenance.GapKinds);
        Assert.Empty(result.Pages); Assert.Empty(result.Candidates!);
    }

    [Fact]
    public void Cancellation_is_not_translated_into_a_coverage_gap()
    {
        using var fixture = new Fixture();
        Assert.ThrowsAny<OperationCanceledException>(() => fixture.Evaluate(new CancellationToken(true)));
    }

    [Fact]
    public void Oversize_partition_artifact_is_rejected_before_reading_or_allocating_its_bytes()
    {
        using var fixture = new Fixture();
        using (var file = File.OpenWrite(Path.Combine(fixture.Published, "bin", "App_Web_Public.dll")))
            file.SetLength(67_108_865);
        var result = fixture.Evaluate();
        Assert.Equal("gap", result.Provenance!.Status);
        Assert.Contains("WebFormsPublishReceiptSetHashLimitExceeded", result.Provenance.GapKinds);
        Assert.Empty(result.Pages);
    }

    [Fact]
    public void Oversize_map_virtual_identity_cannot_grow_the_global_inventory_unbounded()
    {
        using var fixture = new Fixture();
        var map = "Pages/long-identity.compiled";
        File.WriteAllText(Path.Combine(fixture.Published, map), "<preserve virtualPath=\"/" + new string('a', 4096) + "\" />");
        fixture.MutatePart(2, part => part["publishedFiles"]!.AsArray().Add(new JsonObject
        {
            ["path"] = map, ["sha256"] = Hash(Path.Combine(fixture.Published, map)), ["kind"] = "compiled-map"
        }));
        var result = fixture.Evaluate();
        Assert.Equal("gap", result.Provenance!.Status);
        Assert.Contains("WebFormsPublishMapMismatch", result.Provenance.GapKinds);
        Assert.Empty(result.Pages);
    }

    [Fact]
    public void Inventory_only_partition_adds_declared_context_without_duplicating_or_inventing_pages()
    {
        using var fixture = new Fixture();
        var part = JsonNode.Parse(File.ReadAllText(fixture.PartPath(2)))!.AsObject();
        part["schemaVersion"] = WebFormsPublishMapExtractor.InventoryPartitionSchema;
        part["pages"] = new JsonArray();
        var context = "bin/PublicContext.dll";
        File.WriteAllText(Path.Combine(fixture.Published, context), "public inventory-only context bytes; not a build");
        part["publishedFiles"]!.AsArray().Add(new JsonObject
        {
            ["path"] = context, ["sha256"] = Hash(Path.Combine(fixture.Published, context)), ["kind"] = "assembly"
        });
        File.WriteAllText(fixture.PartPath(3), part.ToJsonString());
        fixture.Partitions.Add(new(Path.GetFileName(fixture.PartPath(3)), Hash(fixture.PartPath(3)))); fixture.WriteSet();
        var result = fixture.Evaluate();
        Assert.Equal("bound", result.Provenance!.Status);
        Assert.Equal(67, result.Provenance.PageCount); Assert.Equal(67, result.Pages.Count);
        Assert.Equal(67, result.SourcePaths.Count); Assert.Equal(2, result.Assemblies.Count);
        Assert.Equal(69, result.Provenance.PublishedFileCount);
        var standalone = WebFormsPublishMapExtractor.Evaluate(fixture.Source, new string('a', 40),
            new(fixture.Source, "unused", WebFormsPublishReceiptPath: fixture.PartPath(3), WebFormsPublishedRootPath: fixture.Published), CancellationToken.None);
        Assert.Equal("gap", standalone.Provenance!.Status);
        Assert.Contains("WebFormsPublishReceiptInvalid", standalone.Provenance.GapKinds);
        Assert.Empty(standalone.Pages); Assert.Empty(standalone.Assemblies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inventory_partitions_never_substitute_for_declared_page_bindings(bool retainsPages)
    {
        using var fixture = new Fixture();
        for (var ordinal = 0; ordinal < 3; ordinal++)
            fixture.MutatePart(ordinal, part =>
            {
                part["schemaVersion"] = WebFormsPublishMapExtractor.InventoryPartitionSchema;
                if (!retainsPages) part["pages"] = new JsonArray();
            });
        var result = fixture.Evaluate();
        Assert.Equal("gap", result.Provenance!.Status);
        Assert.Contains(retainsPages ? "WebFormsPublishReceiptSetInvalid" : "WebFormsPublishReceiptSetPagesUnavailable", result.Provenance.GapKinds);
        Assert.Empty(result.Pages); Assert.Empty(result.Assemblies); Assert.Empty(result.SourcePaths);
    }

    private static ScanManifest Manifest() => new("public-receipt-set-scan", "public-fixture", null, "dev", new string('a', 40),
        "public-fixture-scanner", DateTimeOffset.UnixEpoch, "syntax-only", "not-run", [], [], [], []);

    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
    private static string HashText(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tracemap-public-receipt-set-" + Guid.NewGuid().ToString("N"));
        public string Source => Path.Combine(Root, "source");
        public string Published => Path.Combine(Root, "published");
        public string Receipts => Path.Combine(Root, "receipts");
        public string SetPath => Path.Combine(Receipts, "set.json");
        public List<WebFormsPublishMapExtractor.ReceiptPartition> Partitions { get; } = [];
        public string PartPath(int ordinal) => Path.Combine(Receipts, $"part-{ordinal:D2}.json");
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Source, "Pages")); Directory.CreateDirectory(Path.Combine(Published, "Pages"));
            Directory.CreateDirectory(Path.Combine(Published, "bin")); Directory.CreateDirectory(Receipts);
            var assembly = Path.Combine(Published, "bin", "App_Web_Public.dll");
            File.WriteAllText(assembly, "public declared byte-membership fixture; not PE, build or runtime proof");
            for (var number = 0; number < 67; number++)
            {
                var page = $"Pages/Public{number:D2}.aspx";
                File.WriteAllText(Path.Combine(Source, page), "<%@ Page Language=\"VB\" %>");
                File.WriteAllText(Path.Combine(Published, page + ".compiled"), $"<preserve virtualPath=\"/{page}\" assembly=\"App_Web_Public\" type=\"ASP.public{number:D2}_aspx\" />");
            }
            for (var start = 0; start < 67; start += 32)
            {
                var names = Enumerable.Range(start, Math.Min(32, 67 - start)).Select(number => $"Pages/Public{number:D2}.aspx").ToArray();
                var sources = names.Select(path => new { path, sha256 = Hash(Path.Combine(Source, path)) }).ToArray();
                var published = new List<object> { new { path = "bin/App_Web_Public.dll", sha256 = Hash(assembly), kind = "assembly" } };
                published.AddRange(names.Select(page => new { path = page + ".compiled", sha256 = Hash(Path.Combine(Published, page + ".compiled")), kind = "compiled-map" }));
                var receipt = new
                {
                    schemaVersion = "webforms-publish-binding.v1", visibility = "local-only", sourceCommitSha = new string('a', 40),
                    receiptGeneratorSha256 = Hash(typeof(WebFormsPublishReceiptSetTests).Assembly.Location),
                    compilerSha256 = HashText("unavailable-public-fixture-compiler-not-build-proof"),
                    boundedInputSha256 = HashText(string.Join("\n", sources.Select(item => item.path + ":" + item.sha256)) + "\n"),
                    sourceFiles = sources, publishedFiles = published,
                    pages = names.Select(page => new { virtualPath = "/" + page, sourcePath = page, assembly = "App_Web_Public",
                        generatedType = "ASP.public" + Path.GetFileNameWithoutExtension(page)[6..] + "_aspx", mapPath = page + ".compiled" }).ToArray()
                };
                var ordinal = start / 32;
                File.WriteAllText(PartPath(ordinal), JsonSerializer.Serialize(receipt));
                Partitions.Add(new(Path.GetFileName(PartPath(ordinal)), Hash(PartPath(ordinal))));
            }
            WriteSet();
        }
        public void WriteSet(bool refreshHashes = true)
        {
            if (refreshHashes)
                for (var index = 0; index < Partitions.Count; index++) Partitions[index] = Partitions[index] with { Sha256 = Hash(PartPath(index)) };
            var generator = Hash(typeof(WebFormsPublishReceiptSetTests).Assembly.Location);
            var set = new WebFormsPublishMapExtractor.PublishReceiptSet(WebFormsPublishMapExtractor.ReceiptSetSchema,
                RuleIds.LegacyWebFormsPublishMap, "local-only", "operator-declared-review-only-not-build-proof", generator,
                new string('a', 40), WebFormsPublishMapExtractor.ReceiptSetInputDigest(generator, new string('a', 40), Partitions), Partitions);
            File.WriteAllText(SetPath, JsonSerializer.Serialize(set, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        public void MutatePart(int ordinal, Action<JsonObject> mutate)
        {
            var part = JsonNode.Parse(File.ReadAllText(PartPath(ordinal)))!.AsObject(); mutate(part);
            File.WriteAllText(PartPath(ordinal), part.ToJsonString()); WriteSet();
        }
        public void MutateSet(Action<JsonObject> mutate)
        {
            var set = JsonNode.Parse(File.ReadAllText(SetPath))!.AsObject(); mutate(set); File.WriteAllText(SetPath, set.ToJsonString());
        }
        public static void RefreshMapDigest(JsonObject part)
        {
            var maps = part["publishedFiles"]!.AsArray().Where(item => item!["kind"]!.GetValue<string>() == "compiled-map")
                .OrderBy(item => item!["path"]!.GetValue<string>(), StringComparer.Ordinal).ToArray();
            part["publishedMapCount"] = maps.Length;
            part["mapInventorySha256"] = HashText(string.Join("\n", maps.Select(item => item!["path"]!.GetValue<string>() + ":" + item!["sha256"]!.GetValue<string>())) + "\n");
        }
        public WebFormsPublishEvaluation Evaluate(CancellationToken token = default) => WebFormsPublishMapExtractor.Evaluate(Source,
            new string('a', 40), new(Source, "unused", WebFormsPublishReceiptPath: SetPath, WebFormsPublishedRootPath: Published), token);
        public Dictionary<string, string> Hashes() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
