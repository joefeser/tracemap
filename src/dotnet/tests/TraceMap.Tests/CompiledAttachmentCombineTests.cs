using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CompiledAttachmentCombineTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" uri #%?")]
    public async Task Explicit_attachment_link_pins_actual_bytes_without_relabeling_parent_facts(string suffix)
    {
        using var fixture = await Fixture.CreateAsync(suffix);
        var parentBefore = File.ReadAllBytes(fixture.ParentIndex);
        var childBefore = File.ReadAllBytes(fixture.ChildIndex);
        var result = await CombinedIndexBuilder.CombineAsync(fixture.Options);
        Assert.Equal(parentBefore, File.ReadAllBytes(fixture.ParentIndex));
        Assert.Equal(childBefore, File.ReadAllBytes(fixture.ChildIndex));
        await using var connection = new SqliteConnection($"Data Source={result.OutputPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select payload_json from compiled_attachment_links;";
        var link = JsonSerializer.Deserialize<CompiledAttachmentIndexLink>((string)(await command.ExecuteScalarAsync())!)!;
        CompiledAttachmentIndexLink.Validate(link);
        Assert.Equal(Hash(fixture.ParentIndex), link.ParentIndexSha256);
        Assert.Equal(Hash(fixture.ChildIndex), link.AttachmentIndexSha256);
        Assert.Equal(Hash(fixture.ParentManifest), link.ParentManifestSha256);
        Assert.Equal(Hash(fixture.ChildManifest), link.AttachmentManifestSha256);
        Assert.Equal(Hash(typeof(CombinedIndexBuilder).Assembly.Location), link.GeneratorSha256);
        Assert.Equal(result.Sources.Single(source => source.Label == "retained").SourceIndexId, link.ParentSourceIndexId);
        Assert.Equal(result.Sources.Single(source => source.Label == "compiled").SourceIndexId, link.AttachmentSourceIndexId);
        command.CommandText = "select original_fact_id from combined_facts where source_index_id = $id;";
        command.Parameters.AddWithValue("$id", link.ParentSourceIndexId);
        Assert.Equal(fixture.ParentFactId, await command.ExecuteScalarAsync());
        command.CommandText = "select count(*) from combined_facts where source_index_id = $id and fact_type = 'MethodDeclared';";
        command.Parameters.Clear(); command.Parameters.AddWithValue("$id", link.AttachmentSourceIndexId);
        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        Assert.Empty(Directory.GetFiles(fixture.Path, "*-wal", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("parent-manifest")]
    [InlineData("parent-index")]
    [InlineData("child-manifest")]
    [InlineData("sidecar")]
    [InlineData("duplicate")]
    [InlineData("missing-index")]
    [InlineData("limit")]
    [InlineData("index-limit")]
    [InlineData("child-index-manifest")]
    public async Task Wrong_or_unbounded_contract_refuses_before_output_creation(string mutation)
    {
        using var fixture = await Fixture.CreateAsync();
        var options = fixture.Options;
        switch (mutation)
        {
            case "parent-manifest": File.AppendAllText(fixture.ParentManifest, " "); break;
            case "parent-index": using (var stream = new FileStream(fixture.ParentIndex, FileMode.Append)) stream.WriteByte(0); break;
            case "child-manifest":
                var manifest = JsonSerializer.Deserialize<ScanManifest>(File.ReadAllText(fixture.ChildManifest),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                await ManifestWriter.WriteAsync(fixture.ChildManifest, manifest with { RepoName = "different" }); break;
            case "sidecar": File.WriteAllText(fixture.ParentIndex + "-wal", "unadmitted"); break;
            case "duplicate": options = options with { CompiledAttachments = [.. options.CompiledAttachments, options.CompiledAttachments[0]] }; break;
            case "missing-index": options = options with { IndexPaths = [fixture.ChildIndex], Labels = ["compiled"] }; break;
            case "index-limit": options = options with { MaxAttachmentIndexBytes = 1 }; break;
            case "child-index-manifest":
                await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = fixture.ChildIndex, Pooling = false }.ToString()))
                {
                    await connection.OpenAsync(); await using var command = connection.CreateCommand();
                    command.CommandText = "update scan_manifest set manifest_json = replace(manifest_json, 'public-fixture', 'wrong-fixture');";
                    await command.ExecuteNonQueryAsync();
                }
                break;
            default: options = options with { MaxAttachmentHashBytes = 1 }; break;
        }
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => CombinedIndexBuilder.CombineAsync(options));
        Assert.StartsWith("COMPILED_ATTACHMENT_COMBINE_", failure.Message);
        Assert.False(File.Exists(options.OutputPath));
    }

    [Fact]
    public async Task Existing_output_and_precancelled_execution_preserve_original_bytes()
    {
        using var fixture = await Fixture.CreateAsync();
        File.WriteAllText(fixture.Options.OutputPath, "keep-me");
        await Assert.ThrowsAnyAsync<Exception>(() => CombinedIndexBuilder.CombineAsync(fixture.Options));
        Assert.Equal("keep-me", File.ReadAllText(fixture.Options.OutputPath));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var options = fixture.Options with { OutputPath = System.IO.Path.Combine(fixture.Path, "cancel.sqlite") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CombinedIndexBuilder.CombineAsync(options, cancel.Token));
        Assert.False(File.Exists(options.OutputPath));
    }

    [Fact]
    public async Task Ordinary_combine_does_not_infer_a_link_from_adjacent_manifests_or_matching_commit()
    {
        using var fixture = await Fixture.CreateAsync();
        await CombinedIndexBuilder.CombineAsync(fixture.Options with { CompiledAttachments = [] });
        await using var connection = new SqliteConnection($"Data Source={fixture.Options.OutputPath}");
        await connection.OpenAsync(); await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where name = 'compiled_attachment_links';";
        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Reusing_an_index_locator_as_a_manifest_cannot_bypass_the_smaller_role_budget()
    {
        using var fixture = await Fixture.CreateAsync();
        using (var stream = new FileStream(fixture.ParentIndex, FileMode.Append)) stream.SetLength(4_194_305);
        var contract = fixture.Options.CompiledAttachments[0] with { ParentManifestPath = fixture.ParentIndex };
        var options = fixture.Options with { CompiledAttachments = [contract] };
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => CombinedIndexBuilder.CombineAsync(options));
        Assert.Equal("COMPILED_ATTACHMENT_COMBINE_INPUT_LIMIT", failure.Message);
        Assert.False(File.Exists(options.OutputPath));
    }

    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private sealed class Fixture : IDisposable
    {
        public string Path { get; }
        private Fixture(string suffix) => Path = System.IO.Path.Combine(WebFormsReviewPreflightCommand.PhysicalPath(System.IO.Path.GetTempPath()), "tracemap-public-combine-attachment-" + Guid.NewGuid().ToString("N") + suffix);
        public string ParentIndex => System.IO.Path.Combine(Path, "parent", "index.sqlite");
        public string ParentManifest => System.IO.Path.Combine(Path, "parent", "scan-manifest.json");
        public string ChildIndex => System.IO.Path.Combine(Path, "child", "index.sqlite");
        public string ChildManifest => System.IO.Path.Combine(Path, "child", "scan-manifest.json");
        public string ParentFactId { get; private set; } = "";
        public CombineOptions Options => new([ParentIndex, ChildIndex], System.IO.Path.Combine(Path, "combined.sqlite"), ["retained", "compiled"])
        { CompiledAttachments = [new(ParentIndex, ParentManifest, ChildIndex, ChildManifest)] };
        public static async Task<Fixture> CreateAsync(string suffix = "")
        {
            var fixture = new Fixture(suffix); var root = System.IO.Path.Combine(fixture.Path, "source"); Directory.CreateDirectory(root);
            var source = System.IO.Path.Combine(root, "Public.cs"); File.WriteAllText(source, "public class Public { public void Run() {} }");
            FileInventoryItem[] inventory = [new("Public.cs", "CSharp", new FileInfo(source).Length)];
            var snapshot = SourceSnapshotInspector.InspectOrderedInventory(root, inventory, 10, 1024);
            var parent = new ScanManifest("scan-public-retained", "public-fixture", null, "fixture", new string('a', 40),
                ScannerVersions.TraceMap, DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysisReduced", "NotRun", [], [], [], [],
                ScanRootPathHash: FactFactory.Hash(root, 32), SourceSnapshotDigest: snapshot.Digest);
            var fact = FactFactory.Create(parent, FactTypes.MethodDeclared, RuleIds.CSharpSyntaxDeclarations,
                EvidenceTiers.Tier3SyntaxOrTextual, new("Public.cs", 1, 1, null, "PublicFixture", "1.0"), targetSymbol: "Public.Run()");
            fixture.ParentFactId = fact.FactId;
            await ManifestWriter.WriteAsync(fixture.ParentManifest, parent);
            SqliteIndexWriter.Write(fixture.ParentIndex, parent, [fact]);
            var repo = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (repo is not null && !File.Exists(System.IO.Path.Combine(repo.FullName, "rules", "rule-catalog.yml"))) repo = repo.Parent;
            Assert.NotNull(repo);
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var assembly = System.IO.Path.Combine(repo.FullName, "samples", "compiled-dotnet-evidence", "csharp", "bin", configuration, "net10.0", "CompiledEvidence.CSharp.dll");
            var result = CompiledAttachmentProducer.Create(new(parent, Hash(fixture.ParentManifest), Hash(fixture.ParentIndex)),
                new(root, System.IO.Path.Combine(fixture.Path, "unused"), CompiledInputPaths: [assembly], IlBodyEvidence: true), () => inventory, 10, 1024);
            await ManifestWriter.WriteAsync(fixture.ChildManifest, result.Manifest);
            SqliteIndexWriter.Write(fixture.ChildIndex, result.Manifest, result.Facts);
            return fixture;
        }
        public void Dispose() => Directory.Delete(Path, true);
    }
}
