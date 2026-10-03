using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsPublishedRootTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Binding_inspects_only_the_bounded_hash_verified_map_snapshot(bool mapless)
    {
        using var f = new Fixture();
        var mapPath = Path.Combine(f.Published, "Pages/Lookup.aspx.compiled");
        var receipt = JsonNode.Parse(File.ReadAllText(f.Receipt))!;
        if (mapless)
        {
            File.WriteAllText(mapPath, "<preserve virtualPath=\"/Other.aspx\" />");
            receipt["publishedFiles"]![1]!["sha256"] = Hash(mapPath);
            receipt["publishedMapCount"] = 1;
            receipt["mapInventorySha256"] = HashText("Pages/Lookup.aspx.compiled:" + Hash(mapPath) + "\n");
            receipt["pages"] = JsonSerializer.SerializeToNode(new[] { new {
                virtualPath = "/Pages/Lookup.aspx", sourcePath = "Pages/Lookup.aspx",
                bindingKind = "mapless-source-type-candidate" } });
        }
        File.WriteAllText(f.Receipt, receipt.ToJsonString());
        var expectedMapHash = Hash(mapPath);
        var result = WebFormsPublishMapExtractor.Evaluate(f.Source, new string('a', 40),
            new(f.Source, "unused", WebFormsPublishReceiptPath: f.Receipt, WebFormsPublishedRootPath: f.Published),
            CancellationToken.None, artifactsCaptured: () => File.WriteAllText(mapPath, new string('x', 1_048_577)));
        Assert.Equal("bound", result.Provenance!.Status);
        if (mapless) Assert.Single(result.Candidates!);
        else Assert.Equal(expectedMapHash, Assert.Single(result.Pages).MapSha256);
        Assert.Contains("WebFormsPublishInputLimitExceeded", f.Evaluate(f.Published).Provenance!.GapKinds);
    }

    [Theory]
    [InlineData("assembly")]
    [InlineData("compiled-map")]
    public void Case_variant_artifact_paths_follow_host_identity(string kind)
    {
        using var f = new Fixture();
        var receipt = JsonNode.Parse(File.ReadAllText(f.Receipt))!;
        var files = receipt["publishedFiles"]!.AsArray();
        var row = files.Single(x => x!["kind"]!.GetValue<string>() == kind)!.DeepClone();
        // Windows additionally aliases case variants; other hosts exercise exact duplicates.
        if (OperatingSystem.IsWindows()) row["path"] = row["path"]!.GetValue<string>().ToUpperInvariant();
        files.Add(row);
        File.WriteAllText(f.Receipt, receipt.ToJsonString());
        var result = f.Evaluate(f.Published);
        Assert.Equal("gap", result.Provenance!.Status);
        Assert.Contains("WebFormsPublishReceiptAmbiguous", result.Provenance.GapKinds);
        Assert.Empty(result.Assemblies);
        Assert.Empty(result.Pages);
    }

    [Fact]
    public void Windows_artifact_lookup_accepts_one_consistent_case_alias_without_duplicate_inventory()
    {
        if (!OperatingSystem.IsWindows()) return; // Exercised by the Windows distribution job.
        using var f = new Fixture();
        var receipt = JsonNode.Parse(File.ReadAllText(f.Receipt))!;
        receipt["publishedFiles"]![0]!["path"] = "BIN/APP_WEB_PUBLIC.DLL";
        receipt["pages"]![0]!["mapPath"] = "PAGES/LOOKUP.ASPX.COMPILED";
        File.WriteAllText(f.Receipt, receipt.ToJsonString());
        var result = f.Evaluate(f.Published);
        Assert.Equal("bound", result.Provenance!.Status);
        Assert.Single(result.Assemblies);
        Assert.Single(result.Pages);
    }

    [Fact]
    public async Task CLI_requires_a_receipt_with_an_explicit_root()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(1, await TraceMapCommand.RunAsync(["scan", "--repo", "public-source", "--out", "public-output",
            "--webforms-published-root", "public-publish"], output, error));
        Assert.Contains("requires --webforms-publish-receipt", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_receipt_scope_distinguishes_the_explicit_published_root()
    {
        var options = new ScanOptions("public-source", "public-output", WebFormsPublishReceiptPath: "public-receipt");
        string Scope(ScanOptions value)
        {
            var recorder = new ScanReceiptRecorder(value);
            recorder.Bind(new GitMetadata("public", null, "dev", new string('a', 40), []));
            return recorder.CreateReceipt().AuthorizedScopeFingerprint;
        }
        Assert.NotEqual(Scope(options), Scope(options with { WebFormsPublishedRootPath = "public-publish-a" }));
        Assert.NotEqual(Scope(options with { WebFormsPublishedRootPath = "public-publish-a" }),
            Scope(options with { WebFormsPublishedRootPath = "public-publish-b" }));
        Assert.Equal(Scope(options), Scope(options with { WebFormsPublishedRootPath = null }));
        Assert.NotEqual(Scope(options), Scope(options with { WebFormsPublishSourceRelativeBase = "UBid" }));
        Assert.NotEqual(Scope(options with { WebFormsPublishSourceRelativeBase = "UBid" }),
            Scope(options with { WebFormsPublishSourceRelativeBase = "another" }));
        Assert.Equal(Scope(options), Scope(options with { WebFormsPublishSourceRelativeBase = "." }));
    }

    [Fact]
    public void Website_receipt_paths_are_mapped_to_repo_paths_without_rewriting_virtual_routes_or_receipts()
    {
        using var f = new Fixture();
        var website = Path.Combine(f.Source, "UBid"); Directory.CreateDirectory(website);
        Directory.Move(Path.Combine(f.Source, "Pages"), Path.Combine(website, "Pages"));
        var before = f.Hashes();
        var options = new ScanOptions(f.Source, "unused", WebFormsPublishReceiptPath: f.Receipt,
            WebFormsPublishedRootPath: f.Published, WebFormsPublishSourceRelativeBase: "UBid");
        var result = WebFormsPublishMapExtractor.Evaluate(f.Source, new string('a', 40), options, CancellationToken.None);
        Assert.Equal("bound", result.Provenance!.Status);
        Assert.Equal("UBid", result.Provenance.SourceRelativeBase);
        Assert.Equal(new[] { "UBid/Pages/Lookup.aspx" }, result.SourcePaths);
        Assert.Equal("UBid/Pages/Lookup.aspx", Assert.Single(result.Pages).SourcePath);
        var websiteOptions = options with { RepoPath = website, WebFormsPublishSourceRelativeBase = null };
        Assert.NotEqual(result.Provenance.BoundedInputSha256,
            WebFormsPublishInputInspector.Inspect(websiteOptions, new string('a', 40))!.BoundedInputSha256);
        Assert.Equal(before.OrderBy(pair => pair.Key), f.Hashes().OrderBy(pair => pair.Key));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute")]
    [InlineData("UBid/../Pages")]
    [InlineData("C:private")]
    public void Unsafe_receipt_source_bases_withhold_all_publish_evidence(string sourceBase)
    {
        using var f = new Fixture();
        var result = WebFormsPublishMapExtractor.Evaluate(f.Source, new string('a', 40),
            new(f.Source, "unused", WebFormsPublishReceiptPath: f.Receipt, WebFormsPublishedRootPath: f.Published,
                WebFormsPublishSourceRelativeBase: sourceBase), CancellationToken.None);
        Assert.Equal("gap", result.Provenance!.Status);
        Assert.Contains("WebFormsPublishUnsafePath", result.Provenance.GapKinds);
        Assert.Empty(result.Pages); Assert.Empty(result.SourcePaths); Assert.Empty(result.Assemblies);
    }
    [Fact]
    public void Explicit_root_reads_original_published_files_without_colocating_or_copying_them()
    {
        using var fixture = new Fixture();
        var before = fixture.Hashes();
        var result = fixture.Evaluate(fixture.Published);
        Assert.Equal("bound", result.Provenance!.Status);
        Assert.Single(result.Pages);
        Assert.Equal(HashText(Path.GetFullPath(fixture.Published)), result.Provenance.PublishedRootPathHash);
        Assert.NotEqual(Hash(fixture.Receipt), result.Provenance.BoundedInputSha256);
        Assert.Equal(before.OrderBy(pair => pair.Key), fixture.Hashes().OrderBy(pair => pair.Key));
        Assert.False(Directory.Exists(Path.Combine(fixture.Receipts, "bin")));
    }

    [Fact]
    public void Default_receipt_colocation_and_digest_remain_unchanged()
    {
        using var fixture = new Fixture();
        var colocated = Path.Combine(fixture.Published, "publish.json");
        File.Copy(fixture.Receipt, colocated);
        var result = WebFormsPublishMapExtractor.Evaluate(fixture.Source, new string('a', 40),
            new(fixture.Source, "unused", WebFormsPublishReceiptPath: colocated), CancellationToken.None);
        Assert.Equal("bound", result.Provenance!.Status);
        Assert.Null(result.Provenance.PublishedRootPathHash);
        Assert.Equal(Hash(colocated), result.Provenance.BoundedInputSha256);
    }

    [Theory]
    [InlineData("relative", "WebFormsPublishRootInvalid")]
    [InlineData("missing", "WebFormsPublishRootInvalid")]
    [InlineData("changed", "WebFormsPublishArtifactMismatch")]
    public void Invalid_or_changed_published_inputs_withhold_all_binding_facts(string mutation, string expected)
    {
        using var fixture = new Fixture();
        if (mutation == "changed") File.AppendAllText(Path.Combine(fixture.Published, "bin", "App_Web_Public.dll"), "changed");
        var root = mutation switch { "relative" => "relative", "missing" => Path.Combine(fixture.Root, "missing"), _ => fixture.Published };
        var result = fixture.Evaluate(root);
        Assert.Equal("gap", result.Provenance!.Status);
        Assert.Contains(expected, result.Provenance.GapKinds);
        Assert.Empty(result.Pages);
        Assert.Empty(result.SourcePaths);
        Assert.Empty(result.Assemblies);
    }

    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = WebFormsReviewPreflightCommand.PhysicalPath(Path.Combine(Path.GetTempPath(), "tracemap published-root " + Guid.NewGuid().ToString("N")));
        public string Source => Path.Combine(Root, "source");
        public string Published => Path.Combine(Root, "published");
        public string Receipts => Path.Combine(Root, "receipts");
        public string Receipt => Path.Combine(Receipts, "publish.json");
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Source, "Pages"));
            Directory.CreateDirectory(Path.Combine(Published, "bin"));
            Directory.CreateDirectory(Path.Combine(Published, "Pages"));
            Directory.CreateDirectory(Receipts);
            File.WriteAllText(Path.Combine(Source, "Pages", "Lookup.aspx"), "<%@ Page Language=\"VB\" Inherits=\"Lookup\" %>");
            File.WriteAllText(Path.Combine(Published, "bin", "App_Web_Public.dll"), "public byte-membership fixture, not PE/build evidence");
            File.WriteAllText(Path.Combine(Published, "Pages", "Lookup.aspx.compiled"), "<preserve virtualPath=\"/Pages/Lookup.aspx\" assembly=\"App_Web_Public\" type=\"ASP.lookup_aspx\" />");
            var sourceHash = Hash(Path.Combine(Source, "Pages", "Lookup.aspx"));
            File.WriteAllText(Receipt, JsonSerializer.Serialize(new
            {
                schemaVersion = "webforms-publish-binding.v1", visibility = "local-only", sourceCommitSha = new string('a', 40),
                receiptGeneratorSha256 = Hash(typeof(WebFormsPublishedRootTests).Assembly.Location),
                compilerSha256 = HashText("unknown-compiler-public-test-not-build-proof"),
                boundedInputSha256 = HashText("Pages/Lookup.aspx:" + sourceHash + "\n"),
                sourceFiles = new[] { new { path = "Pages/Lookup.aspx", sha256 = sourceHash } },
                publishedFiles = new[]
                {
                    new { path = "bin/App_Web_Public.dll", sha256 = Hash(Path.Combine(Published, "bin", "App_Web_Public.dll")), kind = "assembly" },
                    new { path = "Pages/Lookup.aspx.compiled", sha256 = Hash(Path.Combine(Published, "Pages", "Lookup.aspx.compiled")), kind = "compiled-map" }
                },
                pages = new[] { new { virtualPath = "/Pages/Lookup.aspx", sourcePath = "Pages/Lookup.aspx", assembly = "App_Web_Public", generatedType = "ASP.lookup_aspx", mapPath = "Pages/Lookup.aspx.compiled" } }
            }));
        }
        public WebFormsPublishEvaluation Evaluate(string root) => WebFormsPublishMapExtractor.Evaluate(Source, new string('a', 40),
            new(Source, "unused", WebFormsPublishReceiptPath: Receipt, WebFormsPublishedRootPath: root), CancellationToken.None);
        public Dictionary<string, string> Hashes() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
