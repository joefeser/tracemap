using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class GroupedCompiledPathHandoffTests
{
    private static readonly string IndexHash = new('a', 64);

    [Fact]
    public void Exact_chains_group_without_losing_variants_overloads_or_source_identity()
    {
        var report = Report();
        report = report with { Summary = report.Summary with { TraversalWorkUnits = 17 } };
        var packet = GroupedCompiledPathHandoffBuilder.Create(report, IndexHash);
        Assert.Equal(17, packet.Header.Summary.TraversalWorkUnits);
        Assert.Equal(4, packet.Variants.Count);
        Assert.Equal(3, packet.Chains.Count);
        Assert.Equal(new[] { 0, 1 }, Assert.Single(packet.Chains, chain => chain.VariantIndexes.Count == 2).VariantIndexes);
        Assert.Equal(4, packet.Nodes.Count); // same NodeId with differing evidence is not overwritten
        Assert.Equal(2, packet.Edges.Count); // repeated exact records are indexed once
        Assert.Empty(packet.Header.Paths);
        Assert.Empty(packet.Header.Inventory.EvidenceNodes);
        Assert.All(packet.Variants, variant => { Assert.Empty(variant.Path.Nodes); Assert.Empty(variant.Path.Edges); });
        Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(GroupedCompiledPathHandoffBuilder.Restore(packet)));
        using var stream = File.OpenRead(typeof(GroupedCompiledPathHandoffBuilder).Assembly.Location);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(stream)), packet.GeneratorSha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(report))), packet.InputReportSha256);
        Assert.Equal(IndexHash, packet.InputIndexSha256);
        Assert.Equal("review-only-static-evidence", packet.ClaimLevel);
        Assert.Equal(JsonSerializer.Serialize(packet), JsonSerializer.Serialize(GroupedCompiledPathHandoffBuilder.Create(report, IndexHash)));
        var serialized = JsonSerializer.Serialize(packet);
        Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(GroupedCompiledPathHandoffBuilder.Restore(
            JsonSerializer.Deserialize<GroupedCompiledPathHandoff>(serialized)!)));
    }

    [Fact]
    public void Empty_and_inventory_only_reports_preserve_gaps_coverage_and_order()
    {
        var report = Report() with { Paths = [], Summary = Report().Summary with { PathCount = 0 } };
        var packet = GroupedCompiledPathHandoffBuilder.Create(report, IndexHash);
        Assert.Empty(packet.Chains);
        Assert.Empty(packet.Variants);
        Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(GroupedCompiledPathHandoffBuilder.Restore(packet)));
        Assert.True(packet.Header.Summary.Truncated);
        Assert.Equal("Tier4Unknown", Assert.Single(packet.Header.Gaps).EvidenceTier);
    }

    [Fact]
    public void Synthetic_41_variants_form_13_exact_chains_without_dropping_any_evidence()
    {
        // Public shape regression only; these counts do not substitute for the external private proof.
        var report = Report();
        var template = report.Paths[0];
        var paths = Enumerable.Range(0, 41).Select(index => template with
        {
            PathId = $"path:{index + 1:0000}",
            Nodes = [Node("method-" + index % 13, $"Public.Lookup{index % 13}(String)")],
            SupportingFactIds = ["source:fact:variant-" + index],
            Edges = [template.Edges[0] with { SupportingFactIds = ["source:fact:variant-" + index] }]
        }).ToArray();
        report = report with { Paths = paths, Summary = report.Summary with { PathCount = 41 } };
        var packet = GroupedCompiledPathHandoffBuilder.Create(report, IndexHash);
        Assert.Equal(13, packet.Chains.Count);
        Assert.Equal(41, packet.Variants.Count);
        Assert.Equal(41, packet.Chains.Sum(chain => chain.VariantIndexes.Count));
        Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(GroupedCompiledPathHandoffBuilder.Restore(packet)));
    }

    [Theory]
    [InlineData("node")]
    [InlineData("edge")]
    [InlineData("missing-reference")]
    [InlineData("extra-record")]
    [InlineData("variant")]
    [InlineData("chain")]
    [InlineData("header")]
    [InlineData("hash")]
    [InlineData("limits")]
    [InlineData("visibility")]
    public void Tampered_records_indexes_or_context_are_not_admitted(string change)
    {
        var packet = GroupedCompiledPathHandoffBuilder.Create(Report(), IndexHash);
        var nodes = packet.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        var edges = packet.Edges.ToDictionary(pair => pair.Key, pair => pair.Value);
        var first = packet.Variants[0];
        packet = change switch
        {
            "node" => packet with { Nodes = Mutate(nodes, first.NodeReferences[0], node => node with { CommitSha = new('d', 40) }) },
            "edge" => packet with { Edges = Mutate(edges, first.EdgeReferences[0], edge => edge with { RuleId = "wrong-rule" }) },
            "missing-reference" => packet with { Variants = [first with { NodeReferences = ["missing"] }, .. packet.Variants.Skip(1)] },
            "extra-record" => packet with { Nodes = Mutate(nodes, "unused", _ => Node("unused", "Other()")) },
            "variant" => packet with { Variants = [first with { Path = first.Path with { Classification = "StrongStaticPath" } }, .. packet.Variants.Skip(1)] },
            "chain" => packet with { Chains = packet.Chains.Skip(1).ToArray() },
            "header" => packet with { Header = packet.Header with { CoverageWarnings = [] } },
            "hash" => packet with { BoundedInputSha256 = new('0', 64) },
            "limits" => packet with { Limits = packet.Limits with { MaxReferences = 0 } },
            _ => packet with { Visibility = "shareable" }
        };
        Assert.StartsWith("WEBFORMS_GROUPED_HANDOFF_", Assert.Throws<InvalidDataException>(() =>
            GroupedCompiledPathHandoffBuilder.Restore(packet)).Message);
    }

    [Theory]
    [InlineData("paths")]
    [InlineData("records")]
    [InlineData("references")]
    [InlineData("input-bytes")]
    [InlineData("output-bytes")]
    [InlineData("invalid")]
    public void Explicit_projection_limits_stop_instead_of_dropping_evidence(string bound)
    {
        var limits = bound switch
        {
            "paths" => new GroupedCompiledPathLimits(MaxPaths: 1),
            "records" => new GroupedCompiledPathLimits(MaxRecords: 1),
            "references" => new GroupedCompiledPathLimits(MaxReferences: 1),
            "input-bytes" => new GroupedCompiledPathLimits(MaxInputBytes: 1),
            "output-bytes" => new GroupedCompiledPathLimits(MaxOutputBytes: 1),
            _ => new GroupedCompiledPathLimits(MaxPaths: 0)
        };
        Assert.Throws<InvalidDataException>(() => GroupedCompiledPathHandoffBuilder.Create(Report(), IndexHash, limits));
    }

    [Fact]
    public void Caller_admission_limits_cancellation_and_index_hash_are_explicit()
    {
        var packet = GroupedCompiledPathHandoffBuilder.Create(Report(), IndexHash);
        Assert.Throws<InvalidDataException>(() => GroupedCompiledPathHandoffBuilder.Restore(packet,
            new(MaxOutputBytes: packet.Limits.MaxOutputBytes - 1)));
        Assert.Throws<InvalidDataException>(() => GroupedCompiledPathHandoffBuilder.Create(Report(), "not-a-hash"));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => GroupedCompiledPathHandoffBuilder.Create(Report(), IndexHash,
            cancellationToken: cancellation.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => GroupedCompiledPathHandoffBuilder.Restore(packet,
            cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Private_html_and_json_share_lossless_evidence_and_new_bounded_hash_verified_outputs()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tracemap-grouped-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            var report = Report() with { CoverageWarnings = ["<script>alert('public fixture')</script>"] };
            report = report with { Gaps = [report.Gaps[0] with
            {
                Reason = "depth", CutoffWitness = new(report.Paths[0].Nodes, report.Paths[0].Edges, false, true)
            }] };
            var packet = GroupedCompiledPathHandoffBuilder.Create(report, IndexHash);
            var result = await GroupedCompiledPathReportWriter.WriteAsync(packet, folder);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            Assert.Contains("Sampled root-to-cutoff prefixes", html);
            Assert.Contains("final candidate edge not traversed: True", html);
            Assert.Contains("compiled-il.v1", html);
            Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(GroupedCompiledPathHandoffBuilder.Restore(packet)));
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("3 exact chains", html);
            Assert.Contains("4 retained variants", html);
            Assert.Contains("not runtime execution", html);
            foreach (var variant in packet.Variants) Assert.Contains(variant.Path.PathId, html);
            var ids = Regex.Matches(html, " id=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
            Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
            foreach (var anchor in Regex.Matches(html, "href=\"#([^\"]+)\"").Select(match => match.Groups[1].Value))
                Assert.Contains(anchor, ids);
            var restoredPacket = JsonSerializer.Deserialize<GroupedCompiledPathHandoff>(await File.ReadAllTextAsync(result.HandoffPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(GroupedCompiledPathHandoffBuilder.Restore(restoredPacket)));
            Assert.Equal(Hash(result.HtmlPath), result.HtmlSha256);
            Assert.Equal(Hash(result.HandoffPath), result.HandoffSha256);
            Assert.Equal(new FileInfo(result.HtmlPath).Length + new FileInfo(result.HandoffPath).Length, result.OutputBytes);
            Assert.Equal("WEBFORMS_GROUPED_REPORT_OUTPUT_EXISTS", (await Assert.ThrowsAsync<InvalidDataException>(() =>
                GroupedCompiledPathReportWriter.WriteAsync(packet, folder))).Message);
            Assert.Equal(Hash(result.HtmlPath), result.HtmlSha256);
            Assert.Equal(Hash(result.HandoffPath), result.HandoffSha256);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    [Theory]
    [InlineData("method:6:Lookup", "Lookup")]
    [InlineData("constructor:5:.ctor", "New")]
    public async Task Local_compiled_labels_use_retained_symbols_when_general_display_is_redacted(string member, string label)
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tracemap-compiled-label-" + Guid.NewGuid().ToString("N"));
        try
        {
            var report = Report();
            var symbol = "assembly:name:6:Public|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|" +
                "type:namespace:7:Fixture|names:5:Model|arity:0|" + member + "|arity:0|call:default|hasThis:true|explicitThis:false|()->type(namespace:6:System|names:4:Void)";
            var template = report.Paths[0];
            report = report with { Paths = [template with { Nodes = [Node("compiled", symbol) with { DisplayName = "redacted-hash:fixture" }] }] };
            var packet = GroupedCompiledPathHandoffBuilder.Create(report, IndexHash);
            var result = await GroupedCompiledPathReportWriter.WriteAsync(packet, folder);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            Assert.Contains($"<p>Fixture.Model.{label}()</p>", html);
            Assert.Contains(System.Net.WebUtility.HtmlEncode(symbol), html); // exact identity remains in the detail, not replaced
            var restored = JsonSerializer.Deserialize<GroupedCompiledPathHandoff>(await File.ReadAllTextAsync(result.HandoffPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(GroupedCompiledPathHandoffBuilder.Restore(restored)));
            Assert.Equal("redacted-hash:fixture", Assert.Single(restored.Nodes.Values, node => node.NodeId == "compiled").DisplayName);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public async Task Cancellation_invalid_context_and_stale_generator_refuse_before_output_allocation()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tracemap-grouped-report-" + Guid.NewGuid().ToString("N"));
        var packet = GroupedCompiledPathHandoffBuilder.Create(Report(), IndexHash);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GroupedCompiledPathReportWriter.WriteAsync(packet, folder,
            cancellationToken: cancellation.Token));
        Assert.False(Directory.Exists(folder));
        await Assert.ThrowsAsync<InvalidDataException>(() => GroupedCompiledPathReportWriter.WriteAsync(packet with { ClaimLevel = "runtime" }, folder));
        Assert.False(Directory.Exists(folder));
        packet = packet with { GeneratorSha256 = new('f', 64) };
        packet = packet with { BoundedInputSha256 = GroupedCompiledPathHandoffBuilder.HashJson(new
        {
            Schema = GroupedCompiledPathHandoffBuilder.Schema, Rule = GroupedCompiledPathHandoffBuilder.Rule,
            Index = packet.InputIndexSha256, Report = packet.InputReportSha256, Generator = packet.GeneratorSha256, Limits = packet.Limits
        }, 4096, CancellationToken.None) };
        Assert.Equal("WEBFORMS_GROUPED_REPORT_GENERATOR_MISMATCH", (await Assert.ThrowsAsync<InvalidDataException>(() =>
            GroupedCompiledPathReportWriter.WriteAsync(packet, folder))).Message);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task Combined_output_budget_preserves_partial_bytes_as_unadmitted_instead_of_truncating()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tracemap-grouped-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            var packet = GroupedCompiledPathHandoffBuilder.Create(Report(), IndexHash);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(packet).LongLength;
            packet = GroupedCompiledPathHandoffBuilder.Create(Report(), IndexHash, new(MaxOutputBytes: bytes + 2000));
            Assert.Equal("WEBFORMS_GROUPED_REPORT_OUTPUT_LIMIT", (await Assert.ThrowsAsync<InvalidDataException>(() =>
                GroupedCompiledPathReportWriter.WriteAsync(packet, folder))).Message);
            Assert.True(File.Exists(System.IO.Path.Combine(folder, GroupedCompiledPathReportWriter.HandoffName)));
            Assert.True(File.Exists(System.IO.Path.Combine(folder, GroupedCompiledPathReportWriter.HtmlName)));
            Assert.True(Directory.GetFiles(folder).Sum(file => new FileInfo(file).Length) <= packet.Limits.MaxOutputBytes);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream)); }

    private static Dictionary<string, T> Mutate<T>(Dictionary<string, T> values, string key, Func<T, T> change)
    {
        values.TryGetValue(key, out var value);
        values[key] = change(value!);
        return values;
    }

    private static CombinedPathNode Node(string id, string symbol) => new(
        NodeId: id, NodeKind: "Method", DisplayName: symbol, SourceIndexId: "source", SourceLabel: "retained",
        ScanId: "scan", CommitSha: new('c', 40), SymbolId: symbol, CombinedFactId: null,
        RuleId: "method.v1", EvidenceTier: "Tier3SyntaxOrTextual", FilePath: "Pages/Public.aspx.vb",
        StartLine: 4, EndLine: 5, SurfaceKind: null, SurfaceName: null, HttpMethod: null,
        NormalizedPathKey: null, OperationName: null, TableName: null, ColumnNames: null, SourceKind: null,
        ShapeHash: null, TextHash: null, TextLength: null, PackageName: null, ConfigKey: null,
        Limitations: ["syntax-only"]);

    private static CombinedDependencyPathReport Report()
    {
        var node = Node("method", "Public.Lookup(String)");
        var otherEvidence = node with { RuleId = "alternate.v1", FilePath = "bin/Public.dll", StartLine = -1, EndLine = -1 };
        var overload = node with { NodeId = "overload", SymbolId = "Public.Lookup(Integer)", DisplayName = "Public.Lookup(Integer)" };
        var otherSource = node with { SourceIndexId = "another-source", ScanId = "another-scan" };
        var edge = new CombinedPathEdge("edge", "compiled-il-call", "method", "method", "NeedsReviewPath",
            "compiled-il.v1", "Tier2Structural", ["source:fact:1"], ["source:edge:1"], "bin/Public.dll", -1, -1)
            { CompiledAttachmentLinkSha256 = new('e', 64) };
        var alternate = edge with { SupportingFactIds = ["source:fact:2"], EdgeKind = "projectless-publish-method-candidate", EvidenceTier = "Tier3SyntaxOrTextual" };
        CombinedPath Path(string id, CombinedPathNode item, CombinedPathEdge evidence) => new(id, "NeedsReviewPath",
            "review-only", 1, item.NodeId, item.NodeId, [item], [evidence], evidence.SupportingFactIds,
            ["source:edge:1"], [new("Partial", "Bounded static candidate")]);
        return new("1", "test-public-report.v1", "compiled", "ReducedCoverage", ["partial"],
            new(null, "Public.Lookup", "retained", null, "database-api", null, null, null, false, 8, 100, 1000, "bounded", "1", null),
            [new("source", "retained", new('a', 64), "scan", "public-fixture", null, "dev", new('c', 40), "1", "VB", "VB", false,
                null, null, null, "Reduced", "NotRun")],
            new(1, 4, 2, 4, 1, 1, true),
            [Path("path:1", node, edge), Path("path:2", otherEvidence, alternate), Path("path:3", overload, edge), Path("path:4", otherSource, edge)],
            [new("gap", "Unresolved", "UnknownAnalysisGap", "No target", "source", "retained", "method", "source:fact:gap",
                "gap.v1", "Tier4Unknown", "Pages/Public.aspx.vb", 7, "missing", new('c', 40), "1", "local", 9)],
            new(new Dictionary<string, int> { ["Method"] = 4 }, new Dictionary<string, int> { ["compiled-il-call"] = 1 },
                new Dictionary<string, int> { ["source"] = 3 }, new Dictionary<string, int>(),
                new Dictionary<string, int> { ["Unresolved"] = 1 }, [node, otherEvidence, node], [edge, alternate, edge]),
            ["Static, not runtime"])
            { RootTraversal = new(4, 2, ["compiled-il-call"], ["Method"], false) { ReachableUnresolvedIlCallCount = 1 } };
    }
}
