using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class WebFormsEvidenceQueryTests
{
    [Fact]
    public async Task Explicit_large_node_budget_indexes_more_than_historical_two_million_nodes()
    {
        using var fixture = new Fixture();
        fixture.Inputs(new { }, new { });
        File.WriteAllText(fixture.AppPath, "{\"items\":[" + string.Join(',', Enumerable.Repeat("0", 2_000_001)) + "]}");
        var before = fixture.AppSha;
        await WebFormsReviewEvidenceIndex.WriteAsync(fixture.Root, fixture.RunId, fixture.AppSha, fixture.CompiledSha,
            8_388_608, 1_073_741_824, CancellationToken.None, WebFormsReviewEvidenceIndex.NewPlanMaxNodes);
        using var connection = fixture.Open();
        var context = WebFormsReviewEvidenceIndex.ReadContext(connection, fixture.RunId, fixture.AppSha, fixture.CompiledSha);
        Assert.True(context.NodeCount > WebFormsReviewEvidenceIndex.MaxNodes);
        Assert.Equal(WebFormsReviewEvidenceIndex.NewPlanMaxNodes, context.MaxNodes);
        var (result, truncated) = WebFormsReviewExecutionCommand.ReadQuery(connection,
            new("application", "/items", Offset: 2_000_000, Limit: 1, Depth: 1), CancellationToken.None);
        Assert.Equal(0, Assert.Single(result.Children).Value!.Value.GetInt32());
        Assert.True(truncated);
        Assert.Equal(before, fixture.AppSha);
    }

    [Fact]
    public async Task Explicit_small_node_limit_still_refuses_and_historical_context_remains_readable()
    {
        using var limited = new Fixture(); limited.Inputs(new { values = Enumerable.Range(0, 20).ToArray() }, new { });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => WebFormsReviewEvidenceIndex.WriteAsync(limited.Root,
            limited.RunId, limited.AppSha, limited.CompiledSha, 8_388_608, 67_108_864, CancellationToken.None, 10));
        Assert.Equal("WEBFORMS_EVIDENCE_NODE_LIMIT", error.Message);
        using var historical = new Fixture(); historical.Inputs(new { value = 1 }, new { }); await historical.Write();
        using var connection = historical.Open();
        Assert.Equal(2_000_000, WebFormsReviewEvidenceIndex.ReadContext(connection, historical.RunId,
            historical.AppSha, historical.CompiledSha).MaxNodes);
    }

    [Fact]
    public async Task Status_truncation_aggregation_distinguishes_cycle_from_work_and_never_retains_free_form_reasons()
    {
        using var fixture = new Fixture();
        fixture.Inputs(new { }, new
        {
            header = new
            {
                gaps = new[]
                {
                    new { gapKind = "TruncatedByLimit", reason = "cycle" },
                    new { gapKind = "TruncatedByLimit", reason = "cycle" },
                    new { gapKind = "TruncatedByLimit", reason = "work" },
                    new { gapKind = "TruncatedByLimit", reason = "selector-candidates" },
                    new { gapKind = "TruncatedByLimit", reason = "public fixture free-form value must not be returned" },
                    new { gapKind = "OtherGap", reason = "work" }
                }
            },
            unrelated = new[] { new { gapKind = "TruncatedByLimit", reason = "path" } }
        });
        await fixture.Write(); using var connection = fixture.Open();
        var result = WebFormsReviewExecutionCommand.ReadStatusTruncationReasons(connection, CancellationToken.None);
        Assert.Equal(4, result.Count); Assert.Equal(2, result["cycle"]); Assert.Equal(1, result["work"]);
        Assert.Equal(1, result["selector-candidates"]); Assert.Equal(1, result["other-retained-reason"]);
        Assert.DoesNotContain("path", result.Keys);
        Assert.DoesNotContain(result.Keys, key => key.Contains("fixture", StringComparison.Ordinal));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => WebFormsReviewExecutionCommand.ReadStatusTruncationReasons(connection, cancelled.Token));
    }

    [Fact]
    public async Task Streaming_index_preserves_values_order_unicode_and_escaped_property_names_deterministically()
    {
        using var fixture = new Fixture();
        var value = new { numbers = new object?[] { 0, 1.5, null, true, false }, unicode = "é 🌱", nested = new Dictionary<string, object>
            { ["~/key"] = new { value = "retained" } }, crossing = new string('x', 100_000) };
        fixture.Inputs(value, new { chains = new[] { new { chainId = "chain:public", variantIndexes = new[] { 2, 0, 1 } } } });
        var artifact = await fixture.Write();
        using var connection = fixture.Open();
        var context = WebFormsReviewEvidenceIndex.ReadContext(connection, fixture.RunId, fixture.AppSha, fixture.CompiledSha);
        Assert.Equal(Hash(typeof(WebFormsReviewExecutionCommand).Assembly.Location), context.GeneratorSha256);
        var (result, truncated) = WebFormsReviewExecutionCommand.ReadQuery(connection, new("application", "", Limit: 50, Depth: 8), CancellationToken.None);
        Assert.False(truncated);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(value), JsonSerializer.SerializeToElement(Rebuild(result))));
        var (escaped, _) = WebFormsReviewExecutionCommand.ReadQuery(connection, new("application", "/nested/~0~1key/value", Depth: 0), CancellationToken.None);
        Assert.Equal("retained", escaped.Value!.Value.GetString());
        Assert.Equal(artifact.Sha256, Hash(Path.Combine(fixture.Root, WebFormsReviewEvidenceIndex.Name)));
        using var second = new Fixture(); second.Inputs(value, new { chains = new[] { new { chainId = "chain:public", variantIndexes = new[] { 2, 0, 1 } } } });
        Assert.Equal(artifact.Sha256, (await second.Write()).Sha256);
    }

    [Fact]
    public async Task Pagination_and_depth_omission_are_explicit_and_do_not_change_original_values()
    {
        using var fixture = new Fixture(); fixture.Inputs(new { items = Enumerable.Range(0, 80).ToArray() }, new { }); await fixture.Write();
        using var connection = fixture.Open();
        var (result, truncated) = WebFormsReviewExecutionCommand.ReadQuery(connection, new("application", "/items", Offset: 30, Limit: 5, Depth: 1), CancellationToken.None);
        Assert.True(truncated); Assert.Equal(80, result.ChildCount); Assert.Equal(5, result.ReturnedChildren); Assert.Equal(75, result.OmittedChildren);
        Assert.Equal(35, result.NextOffset);
        Assert.Equal(Enumerable.Range(30, 5), result.Children.Select(child => child.Value!.Value.GetInt32()));
        Assert.Equal("/items/30", result.Children[0].Pointer);
        var (shallow, omitted) = WebFormsReviewExecutionCommand.ReadQuery(connection, new("application", "/items", Depth: 0), CancellationToken.None);
        Assert.True(omitted); Assert.Null(shallow.Value); Assert.Empty(shallow.Children); Assert.Equal(80, shallow.OmittedChildren);
    }

    [Theory]
    [InlineData("--pointer", "invalid")]
    [InlineData("--pointer", "/bad~2escape")]
    [InlineData("--document", "source")]
    [InlineData("--limit", "0")]
    [InlineData("--limit", "51")]
    [InlineData("--depth", "9")]
    [InlineData("--offset", "-1")]
    [InlineData("--sql", "SELECT * FROM json_nodes")]
    public void Query_grammar_is_closed_and_bounded(string option, string value) =>
        Assert.Throws<InvalidDataException>(() => WebFormsReviewExecutionCommand.ParseQuery([option, value]));

    [Fact]
    public async Task Missing_pointer_or_noncanonical_array_index_never_falls_back_to_an_unrelated_record()
    {
        using var fixture = new Fixture(); fixture.Inputs(new { items = new[] { 1, 2 } }, new { }); await fixture.Write(); using var connection = fixture.Open();
        foreach (var pointer in new[] { "/missing", "/items/01", "/items/-", "/items/2", "/items/0/anything" })
            Assert.Throws<InvalidDataException>(() => WebFormsReviewExecutionCommand.ReadQuery(connection, new("application", pointer), CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_properties_oversized_tokens_and_cancelled_inputs_are_not_admitted()
    {
        using var duplicate = new Fixture(); duplicate.Inputs(new { }, new { }); File.WriteAllText(duplicate.AppPath, "{\"a\":1,\"a\":2}");
        await Assert.ThrowsAsync<SqliteException>(() => duplicate.Write());
        using var oversized = new Fixture(); oversized.Inputs(new { huge = new string('x', WebFormsReviewEvidenceIndex.MaxTokenBytes + 1) }, new { });
        await Assert.ThrowsAsync<InvalidDataException>(() => oversized.Write());
        using var cancelled = new Fixture(); cancelled.Inputs(new { }, new { }); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.Write(cancellation.Token));
        Assert.False(File.Exists(Path.Combine(cancelled.Root, WebFormsReviewEvidenceIndex.Name)));
    }

    [Fact]
    public async Task Query_node_budget_refuses_overwide_recursive_expansion()
    {
        using var fixture = new Fixture(); fixture.Inputs(new { items = Enumerable.Range(0, 50).Select(_ => Enumerable.Range(0, 50).ToArray()).ToArray() }, new { });
        await fixture.Write(); using var connection = fixture.Open();
        var exception = Assert.Throws<InvalidDataException>(() => WebFormsReviewExecutionCommand.ReadQuery(connection, new("application", "", Limit: 50, Depth: 8), CancellationToken.None));
        Assert.Equal("WEBFORMS_EVIDENCE_QUERY_NODE_LIMIT", exception.Message);
    }

    [Fact]
    public async Task Query_byte_budget_refuses_a_large_scalar_before_deserializing_it_into_the_response()
    {
        using var fixture = new Fixture(); fixture.Inputs(new { large = new string('x', 200_000) }, new { });
        await fixture.Write(); using var connection = fixture.Open();
        var exception = Assert.Throws<InvalidDataException>(() => WebFormsReviewExecutionCommand.ReadQuery(connection,
            new("application", "/large", Depth: 0), CancellationToken.None));
        Assert.Equal("WEBFORMS_EVIDENCE_QUERY_RESPONSE_LIMIT", exception.Message);
    }

    private static JsonNode? Rebuild(WebFormsEvidenceItem node) => node.Kind switch
    {
        "object" => new JsonObject(node.Children.Select(child => new KeyValuePair<string, JsonNode?>(child.Pointer.Split('/').Last().Replace("~1", "/").Replace("~0", "~"), Rebuild(child)))),
        "array" => new JsonArray(node.Children.Select(Rebuild).ToArray()),
        _ => JsonNode.Parse(node.Value!.Value.GetRawText())
    };
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tracemap-public-query-" + Guid.NewGuid().ToString("N"));
        public string RunId => new('a', 32);
        public string AppPath => Path.Combine(Root, "handoff.local.json");
        public string CompiledPath => Path.Combine(Root, "compiled", "compiled-paths.handoff.local.json");
        public string AppSha => Hash(AppPath);
        public string CompiledSha => Hash(CompiledPath);
        public Fixture() => Directory.CreateDirectory(Path.Combine(Root, "compiled"));
        public void Inputs<T, U>(T application, U compiled) { File.WriteAllText(AppPath, JsonSerializer.Serialize(application)); File.WriteAllText(CompiledPath, JsonSerializer.Serialize(compiled)); }
        public Task<WebFormsReviewArtifact> Write(CancellationToken token = default) => WebFormsReviewEvidenceIndex.WriteAsync(Root, RunId, AppSha, CompiledSha, 8_388_608, 67_108_864, token);
        public SqliteConnection Open() { var connection = new SqliteConnection($"Data Source={new Uri(Path.Combine(Root, WebFormsReviewEvidenceIndex.Name)).AbsoluteUri}?immutable=1;Mode=ReadOnly;Pooling=False"); connection.Open(); return connection; }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
