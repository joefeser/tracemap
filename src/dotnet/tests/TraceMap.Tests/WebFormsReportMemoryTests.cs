using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;
using TraceMap.Cli;
using Xunit.Abstractions;

namespace TraceMap.Tests;

[CollectionDefinition("WebForms isolated allocation", DisableParallelization = true)]
public sealed class WebFormsAllocationCollection { }

[Collection("WebForms isolated allocation")]
public sealed class WebFormsReportMemoryTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Native_scan_capacity_admits_selected_roots_but_explicit_small_fact_cap_still_refuses()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "combined.sqlite");
        var combined = await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path,
            Enumerable.Range(0, 32).SelectMany(value => Fixture($"{value:D4}")))], index, ["retained"]));
        var before = Hash(index);
        var source = Assert.Single(combined.Sources);
        var roots = new[] { new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId,
            source.CommitSha, "Sample.Page0000.Load()") };
        var options = new CombinedDependencyPathOptions(index, temp.Path, ToSurface: "sql-query", IncludeLegacyRoots: true);
        var declared = new WebFormsReviewBudgets(MaxParentFacts: 10_000, MaxRetainedArtifactBytes: 8L * 1024 * 1024);
        async Task<CombinedDependencyPathReport> Report(WebFormsReviewReportBudgets caps) =>
            await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, roots, true,
                new(caps.MaxInputFacts, caps.MaxInputEdges, caps.MaxInputTextBytes) { MaxGraphStorageBytes = caps.MaxGraphStorageBytes });
        var refused = await Report(WebFormsReviewPreflightCommand.ResolveNewReportBudgets(declared with
            { Reports = new(MaxInputFacts: 1) }));
        Assert.Contains(refused.Gaps, gap => gap.GapKind == "GraphInputLimitReached" && gap.Reason == "graph-facts");
        Assert.Empty(refused.Paths);
        var admitted = await Report(WebFormsReviewPreflightCommand.ResolveNewReportBudgets(declared));
        Assert.DoesNotContain(admitted.Gaps, gap => gap.GapKind == "GraphInputLimitReached");
        Assert.NotEmpty(admitted.Paths);
        Assert.Equal(before, Hash(index));
    }

    [Fact]
    public async Task Indexed_fact_storage_retains_identical_original_ids_in_distinct_source_namespaces()
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        var first = Path.Combine(temp.Path, "first.sqlite");
        var second = Path.Combine(temp.Path, "second.sqlite");
        SqliteIndexWriter.Write(first, Manifest(), facts);
        var secondManifest = Manifest() with { ScanId = "scan-second", RepoName = "synthetic-second-repo" };
        SqliteIndexWriter.Write(second, secondManifest, facts.Select(fact => fact with
            { ScanId = secondManifest.ScanId, Repo = secondManifest.RepoName }).ToArray());
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([first, second], combined, ["first", "second"]));
        var hash = Hash(combined);
        var options = PathOptions(combined);
        var expected = await CombinedDependencyPathReporter.BuildReportAsync(options);
        var actual = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(options, Budget());
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual.Report));
        Assert.Equal(2 * facts.Count, actual.GraphStorage!.StoredFacts);
        Assert.Equal(2, actual.Report.Sources.Count);
        Assert.Contains(actual.Report.Paths, path => path.Nodes.Any(node => node.SourceLabel == "first"));
        Assert.Contains(actual.Report.Paths, path => path.Nodes.Any(node => node.SourceLabel == "second"));
        Assert.Equal(hash, Hash(combined));
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(1024, true)]
    [InlineData(128, false)]
    [InlineData(1024, false)]
    public async Task Indexed_high_fanout_paging_preserves_every_branch_in_both_traversal_directions(int branches, bool legacy)
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        for (var index = 0; index < branches; index++)
        {
            var target = $"Synthetic.Store{index:D5}.Save()";
            facts.Add(Fact(FactTypes.CallEdge, "csharp.semantic.call.v1", "Sample.Page.Load()", target, 40));
            facts.Add(Fact(FactTypes.QueryPatternDetected, RuleIds.CSharpSyntaxQueryPattern, target, $"query:{index:D5}", 41,
                ("operationName", "SELECT"), ("tableName", $"synthetic_orders_{index:D5}"), ("sqlSourceKind", "literal-string")));
        }
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, facts)], combined, ["page"]));
        var hash = Hash(combined);
        var options = legacy ? PathOptions(combined) with { MaxPaths = branches + 10 }
            : new CombinedDependencyPathOptions(combined, "unused", FromSymbol: "Sample.Page.Load()",
                ToSurface: "sql-query", MaxPaths: branches + 10) { ExactFromSymbol = true };
        var expected = await CombinedDependencyPathReporter.BuildReportAsync(options);
        var actual = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(options, Budget());
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual.Report));
        Assert.Equal(branches + 1, actual.Report.Paths.Count);
        Assert.False(actual.Report.Summary.Truncated);
        Assert.InRange(actual.GraphStorage!.MaximumOutgoingRowsLoaded, 1, 64);
        Assert.True(actual.GraphStorage.MaximumOutgoingRowsLoaded < branches);
        if (legacy)
        {
            Assert.True(actual.GraphStorage.IncomingCountQueries >= actual.Report.Paths.Count);
            Assert.InRange(actual.GraphStorage.IncomingReferenceRowsObserved.GetValueOrDefault(),
                0, 5 * actual.GraphStorage.IncomingCountQueries.GetValueOrDefault());
            Assert.InRange(actual.GraphStorage.GlobalEdgePayloadRowsRead.GetValueOrDefault(),
                0, 16L * actual.GraphStorage.StoredEdges);
        }
        Assert.Equal(hash, Hash(combined));
        output.WriteLine($"branches={branches};legacy={legacy};paths={actual.Report.Paths.Count};maximumOutgoingRowsLoaded={actual.GraphStorage.MaximumOutgoingRowsLoaded};logicalStorageBytes={actual.GraphStorage.LogicalStorageBytes}");
    }

    [Theory]
    [InlineData(32)]
    [InlineData(256)]
    public async Task Indexed_combined_graph_retains_global_parity_without_loading_all_outgoing_edges(int pages)
    {
        using var temp = new TempDirectory();
        var facts = Enumerable.Range(0, pages).SelectMany(index => Fixture(
            $"{index:D4}", $"Synthetic.{(index % 2 == 0 ? "\U00010000" : "\uE000")}.Page{index:D4}.Load()",
            $"Pages/Page{index:D4}.aspx"));
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, facts)], combined, ["public-page"]));
        var hash = Hash(combined);
        var options = PathOptions(combined) with { StartingNodeLimit = 1_000 };
        var expected = await CombinedDependencyPathReporter.BuildReportAsync(options);
        var actual = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(options, Budget());
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual.Report));
        var usage = Assert.IsType<CombinedDependencyPathReporter.IndexedGraphUsage>(actual.GraphStorage);
        Assert.Equal("sqlite-temporary", usage.Engine);
        Assert.Equal("length-framed-json-v1", usage.PayloadEncoding);
        Assert.NotNull(usage.StageElapsedMilliseconds);
        Assert.Contains("initial-nodes", usage.StageElapsedMilliseconds.Keys);
        Assert.Contains("report-traversal", usage.StageElapsedMilliseconds.Keys);
        Assert.InRange(usage.StageElapsedMilliseconds.Count, 1, 32);
        Assert.All(usage.StageElapsedMilliseconds.Values, milliseconds => Assert.True(milliseconds >= 0));
        Assert.True(usage.PayloadDecodedBytes > 0);
        Assert.True(usage.PayloadFrameBytes > 0);
        Assert.Equal(usage.PayloadDecodedBytes + 9L * (usage.StoredFacts + usage.StoredNodes + usage.StoredEdges), usage.PayloadFrameBytes);
        Assert.Equal(hash.ToLowerInvariant(), usage.InputSha256);
        Assert.Equal(Hash(typeof(CombinedDependencyPathReporter).Assembly.Location).ToLowerInvariant(), usage.GeneratorSha256);
        Assert.Equal(expected.Summary.GraphNodeCount, usage.StoredNodes);
        Assert.Equal(expected.Summary.GraphEdgeCount, usage.StoredEdges);
        Assert.Equal(pages * Fixture().Count, usage.StoredFacts);
        Assert.NotNull(usage.LogicalStorageBytes);
        Assert.InRange(usage.LogicalStorageBytes.GetValueOrDefault(), 4_096, 512L * 1024 * 1024);
        Assert.InRange(usage.MaximumOutgoingRowsLoaded, 1, 20);
        Assert.True(usage.MaximumOutgoingRowsLoaded < usage.StoredEdges);
        Assert.Equal(hash, Hash(combined));
        output.WriteLine($"pages={pages};nodes={usage.StoredNodes};edges={usage.StoredEdges};logicalStorageBytes={usage.LogicalStorageBytes};maxOutgoingRows={usage.MaximumOutgoingRowsLoaded}");
    }

    [Fact]
    public async Task Framed_outgoing_pages_budget_decoded_bytes_and_preserve_every_branch()
    {
        using var temp = new TempDirectory();
        const string root = "Synthetic.Page.Load()";
        var facts = new List<CodeFact> { Fact(FactTypes.MethodDeclared, "csharp.semantic.method.v1", root, null, 1) };
        for (var index = 0; index < 3; index++)
        {
            var target = $"Synthetic.Store{index:D4}.Save()";
            var call = Fact(FactTypes.CallEdge, "csharp.semantic.call.v1", root, target, 2 + index);
            facts.Add(call with { Evidence = call.Evidence with
                { FilePath = "Pages/" + new string('x', 300_000) + index + ".aspx" } });
            facts.Add(Fact(FactTypes.QueryPatternDetected, RuleIds.CSharpSyntaxQueryPattern, target, $"query:{index}", 10 + index,
                ("operationName", "SELECT"), ("tableName", $"synthetic_orders_{index}"), ("sqlSourceKind", "literal-string")));
        }
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, facts)], combined, ["page"]));
        var hash = Hash(combined);
        var options = new CombinedDependencyPathOptions(combined, "unused", FromSymbol: root, ToSurface: "sql-query")
            { ExactFromSymbol = true };
        var expected = await CombinedDependencyPathReporter.BuildReportAsync(options);
        var actual = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(options, Budget());
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual.Report));
        Assert.Equal(3, actual.Report.Paths.Count);
        var usage = Assert.IsType<CombinedDependencyPathReporter.IndexedGraphUsage>(actual.GraphStorage);
        Assert.InRange(usage.MaximumOutgoingDecodedBytesLoaded.GetValueOrDefault(), 300_000, 512 * 1024);
        Assert.True(usage.PayloadFrameBytes > usage.PayloadDecodedBytes);
        Assert.Equal(hash, Hash(combined));
    }

    [Fact]
    public async Task Indexed_graph_storage_refusal_discards_every_partial_path_and_preserves_input()
    {
        using var temp = new TempDirectory();
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var facts = Enumerable.Range(0, 32).SelectMany(index => Fixture($"{index:D4}", filePath: $"Pages/Page{index:D4}.aspx"));
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, facts)], combined, ["page"]));
        var hash = Hash(combined);
        var budget = new ReportInputBudget(1_000, 1_000, 1_000_000) { MaxGraphStorageBytes = 64 * 1024 };
        var result = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(PathOptions(combined), budget);
        Assert.Empty(result.Report.Paths);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "GraphInputLimitReached" && gap.Reason == "graph-storage-bytes");
        Assert.True(result.Report.Summary.Truncated);
        Assert.Single(result.Report.Sources);
        Assert.Null(result.GraphStorage);
        // This deliberately tiny bound can refuse schema creation before a
        // store exists; unavailable storage observations remain unknown.
        Assert.Null(result.RefusedGraphStorage);
        Assert.Equal(hash, Hash(combined));
    }

    [Fact]
    public async Task Explicit_scratch_budget_reaches_packet_and_selected_symbol_graphs_without_mutating_input()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "combined.sqlite");
        var combined = await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path,
            Enumerable.Range(0, 32).SelectMany(value => Fixture($"{value:D4}")))], index, ["page"]));
        var before = Hash(index);
        var source = Assert.Single(combined.Sources);
        var roots = new[] { new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId,
            source.CommitSha, "Sample.Page0000.Load()") };
        var options = new CombinedDependencyPathOptions(index, temp.Path, ToSurface: "sql-query", IncludeLegacyRoots: true);
        var refused = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, roots, true,
            new() { MaxGraphStorageBytes = 64 * 1024 });
        Assert.Contains(refused.Gaps, gap => gap.GapKind == "GraphInputLimitReached" && gap.Reason == "graph-storage-bytes");
        Assert.Empty(refused.Paths);
        var admitted = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, roots, true,
            new() { MaxGraphStorageBytes = 8L * 1024 * 1024 });
        Assert.DoesNotContain(admitted.Gaps, gap => gap.GapKind == "GraphInputLimitReached");
        Assert.NotEmpty(admitted.Paths);
        var packetOptions = new WebFormsModernizationOptions(index, temp.Path);
        var refusedPacket = await WebFormsModernizationPacketReporter.BuildAsync(packetOptions with
            { MaxGraphStorageBytes = 64 * 1024 });
        Assert.Contains(refusedPacket.Gaps, gap => gap.Classification == "WebFormsModernizationInputLimitReached");
        var admittedPacket = await WebFormsModernizationPacketReporter.BuildAsync(packetOptions with
            { MaxGraphStorageBytes = 8L * 1024 * 1024 });
        Assert.DoesNotContain(admittedPacket.Gaps, gap => gap.Classification == "WebFormsModernizationInputLimitReached");
        Assert.Equal(32, admittedPacket.Surfaces.Count);
        Assert.True(admittedPacket.Summary.TraversalWorkUnits > 0);
        Assert.Equal(before, Hash(index));
    }

    [Fact]
    public async Task Indexed_graph_storage_diagnostic_replays_an_explicit_retained_index_without_mutation()
    {
        using var temp = new TempDirectory();
        var retained = Environment.GetEnvironmentVariable("TRACEMAP_GRAPH_DIAGNOSTIC_INDEX");
        var retainedOutput = Environment.GetEnvironmentVariable("TRACEMAP_GRAPH_DIAGNOSTIC_OUT");
        if (retained is null && retainedOutput is not null || retained is not null && string.IsNullOrWhiteSpace(retainedOutput))
            throw new InvalidOperationException("Retained diagnostic requires both explicit index and new output directory.");
        var index = retained is null ? Path.Combine(temp.Path, "combined.sqlite") : Path.GetFullPath(retained);
        if (retained is null)
            await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path,
                Enumerable.Range(0, 32).SelectMany(value => Fixture($"{value:D4}")))], index, ["page"]));
        var root = retainedOutput is null ? Path.Combine(temp.Path, "diagnostic") : Path.GetFullPath(retainedOutput);
        Assert.False(Directory.Exists(root), "Diagnostic output must be new; no retained input or previous output is overwritten.");
        Directory.CreateDirectory(root);
        var before = Hash(index).ToLowerInvariant();
        var selectedStorage = Environment.GetEnvironmentVariable("TRACEMAP_GRAPH_DIAGNOSTIC_STORAGE_BYTES");
        var storageLimit = retained is null ? 128 * 1024 : selectedStorage is null ? 512L * 1024 * 1024
            : long.Parse(selectedStorage, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(storageLimit, 64 * 1024, 16L * 1024 * 1024 * 1024);
        var selectedText = Environment.GetEnvironmentVariable("TRACEMAP_GRAPH_DIAGNOSTIC_TEXT_BYTES");
        var textLimit = selectedText is null ? 512L * 1024 * 1024
            : long.Parse(selectedText, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(textLimit, 1, int.MaxValue);
        var budget = new ReportInputBudget(1_000_000, 500_000, textLimit) { MaxGraphStorageBytes = storageLimit };
        var options = PathOptions(index);
        var bounded = JsonSerializer.SerializeToUtf8Bytes(new { inputIndexSha256 = before,
            maxFacts = budget.MaxFacts, maxEdges = budget.MaxEdges, maxTextBytes = budget.MaxTextBytes,
            maxGraphStorageBytes = storageLimit, options });
        var boundedHash = Convert.ToHexString(SHA256.HashData(bounded)).ToLowerInvariant();
        var testGenerator = Hash(typeof(WebFormsReportMemoryTests).Assembly.Location).ToLowerInvariant();
        var reportingGenerator = Hash(typeof(CombinedDependencyPathReporter).Assembly.Location).ToLowerInvariant();
        var stagePath = Path.Combine(root, "graph-stages.ndjson");
        using var stageStream = new FileStream(stagePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var stages = new StreamWriter(stageStream, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        var started = Stopwatch.GetTimestamp();
        var stageCount = 0;
        void ObserveStage(string stage)
        {
            Assert.InRange(++stageCount, 1, 32);
            Assert.All(stage, character => Assert.True(character is >= 'a' and <= 'z' or '-'));
            stages.WriteLine(JsonSerializer.Serialize(new { schemaVersion = "diagnostic.webforms.graph-storage.v1",
                ruleId = "diagnostic.webforms.graph-storage.v1", evidenceTier = EvidenceTiers.Tier4Unknown,
                visibility = "local-only", observationState = "attempt-stage-entry-not-admission",
                generatorSha256 = testGenerator, reportingGeneratorSha256 = reportingGenerator,
                boundedInputSha256 = boundedHash, inputIndexSha256 = before,
                stage, elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds }));
        }
        ObserveStage("attempt-start");
        budget = new(budget.MaxFacts, budget.MaxEdges, budget.MaxTextBytes)
            { MaxGraphStorageBytes = storageLimit, GraphStageObserver = ObserveStage };
        var result = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(options, budget);
        var refused = result.Report.Gaps.Any(gap => gap.GapKind == "GraphInputLimitReached");
        var usage = refused ? result.RefusedGraphStorage : result.GraphStorage;
        Assert.NotNull(usage);
        Assert.Equal(before, usage.InputSha256);
        Assert.Equal(Hash(typeof(CombinedDependencyPathReporter).Assembly.Location).ToLowerInvariant(), usage.GeneratorSha256);
        Assert.Equal(before, Hash(index).ToLowerInvariant());
        if (refused)
        {
            Assert.Empty(result.Report.Paths);
            Assert.Equal(0, result.Report.Summary.GraphNodeCount);
            Assert.True(result.Report.Summary.Truncated);
        }
        if (retained is null) Assert.True(refused);
        var stageBytes = stageStream.Length;
        stages.Dispose();
        stageStream.Dispose();
        Assert.InRange(stageBytes, 1, 64 * 1024);
        Assert.Equal(stageCount, File.ReadLines(stagePath).Count());
        foreach (var line in File.ReadLines(stagePath))
        {
            using var entry = JsonDocument.Parse(line);
            Assert.Equal(boundedHash, entry.RootElement.GetProperty("boundedInputSha256").GetString());
            Assert.Equal(testGenerator, entry.RootElement.GetProperty("generatorSha256").GetString());
        }
        var receipt = new { schemaVersion = "diagnostic.webforms.graph-storage.v1",
            ruleId = "diagnostic.webforms.graph-storage.v1", evidenceTier = EvidenceTiers.Tier4Unknown,
            visibility = "local-only", claimLevel = "review-only-static-not-runtime",
            generatorSha256 = testGenerator, boundedInputSha256 = boundedHash,
            stageTraceSha256 = Hash(stagePath).ToLowerInvariant(),
            inputIndexSha256 = before, maxGraphStorageBytes = storageLimit,
            state = refused ? "admission-refused-partial-counts-only" : "admitted", usage,
            limitations = new[] { "Scratch counts on refusal are successful-write observations, never classified graph or path counts.",
                "Refused allocation samples precede the failed write and are not final file sizes; an unreadable disposable store is never queried after SQLITE_FULL.",
                "Physical object allocation is unknown when SQLite dbstat is unavailable.",
                "Stage timings are wall-clock intervals between graph-stage boundaries, including waits and GC; they are not CPU times or native CLI phase timings.",
                "This diagnostic does not measure OS memory or transient sorter disk peak." } };
        await File.WriteAllBytesAsync(Path.Combine(root, "graph-storage.receipt.json"), JsonSerializer.SerializeToUtf8Bytes(receipt,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
        output.WriteLine($"graphDiagnostic.refused={refused};phase={usage.StoragePhase};facts={usage.StoredFacts};nodes={usage.StoredNodes};edges={usage.StoredEdges};logicalBytes={usage.LogicalStorageBytes}");
        foreach (var stage in usage.StageElapsedMilliseconds ?? new Dictionary<string, long>())
            output.WriteLine($"graphDiagnostic.stage={stage.Key};elapsedMs={stage.Value}");
    }

    [Fact]
    public async Task Indexed_outgoing_order_uses_dotnet_ordinal_unicode_order_and_preserves_all_branches()
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        foreach (var name in new[] { "\uE000", "\U00010000" })
        {
            var target = $"Synthetic.{name}.Store.Save()";
            facts.Add(Fact(FactTypes.CallEdge, "csharp.semantic.call.v1", "Sample.Page.Load()", target, 40));
            facts.Add(Fact(FactTypes.QueryPatternDetected, RuleIds.CSharpSyntaxQueryPattern, target, $"query:{name}", 41,
                ("operationName", "SELECT"), ("tableName", $"synthetic_{name}"), ("sqlSourceKind", "literal-string")));
        }
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, facts)], combined, ["page"]));
        var hash = Hash(combined);
        var files = Directory.GetFiles(temp.Path).Order(StringComparer.Ordinal).ToArray();
        var expected = await CombinedDependencyPathReporter.BuildReportAsync(PathOptions(combined));
        var actual = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(PathOptions(combined), Budget());
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual.Report));
        Assert.Contains(actual.Report.Paths, path => path.Nodes.Any(node => node.DisplayName.Contains("\U00010000", StringComparison.Ordinal)));
        Assert.Contains(actual.Report.Paths, path => path.Nodes.Any(node => node.DisplayName.Contains("\uE000", StringComparison.Ordinal)));
        Assert.Equal(3, actual.GraphStorage!.MaximumOutgoingRowsLoaded);
        Assert.Equal(files, Directory.GetFiles(temp.Path).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(hash, Hash(combined));
    }

    [Fact]
    public async Task Cancelled_indexed_graph_does_not_modify_or_publish_input_artifacts()
    {
        using var temp = new TempDirectory();
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, Fixture())], combined, ["page"]));
        var hash = Hash(combined);
        var files = Directory.GetFiles(temp.Path).Order(StringComparer.Ordinal).ToArray();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(
            PathOptions(combined), Budget(), cancellation.Token));
        Assert.Equal(files, Directory.GetFiles(temp.Path).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(hash, Hash(combined));
    }

    [Fact]
    public async Task Indexed_graph_rejects_orphaned_source_facts_instead_of_silently_pruning_competitors()
    {
        using var temp = new TempDirectory();
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, Fixture())], combined, ["page"]));
        await using (var writer = new SqliteConnection($"Data Source={combined};Pooling=False"))
        {
            await writer.OpenAsync();
            await using var command = writer.CreateCommand();
            command.CommandText = """
                update combined_facts set source_index_id='missing-source',
                    combined_fact_id='missing-source:' || original_fact_id
                where combined_fact_id=(select min(combined_fact_id) from combined_facts);
                """;
            await command.ExecuteNonQueryAsync();
        }
        var hash = Hash(combined);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(PathOptions(combined), Budget()));
        Assert.Equal("COMBINED_FACT_SOURCE_UNAVAILABLE", exception.Message);
        Assert.Equal(hash, Hash(combined));
    }

    [Fact]
    public async Task Combined_compact_properties_preserve_every_fact_and_global_cross_source_competitor()
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        var witness = Fact(FactTypes.ArgumentPassed, "csharp.semantic.argument.v1", "Sample.Page.Load()", "Save", 3,
            ("largeUnusedValue", new string('x', 8_000))) with { FactId = "000-combined-witness" };
        facts.Add(witness);
        facts.Add(witness with { FactId = "001-combined-witness" });
        var handler = facts.Single(fact => fact.FactType == FactTypes.WebFormsHandlerResolved);
        facts[facts.IndexOf(handler)] = handler with
        {
            Properties = new SortedDictionary<string, string>(handler.Properties.ToDictionary())
            { ["supportingFactIds"] = "001-combined-witness" }
        };
        facts.Add(Fact("FutureFactKind", "future.rule.v1", "Sample.Store.Save()", "future", 7,
            ("surfaceKind", "sql-query"), ("operationName", "SELECT")));
        facts.Add(Fact(FactTypes.ArgumentPassed, "csharp.semantic.argument.v1", "Sample.Store.Save()", "package", 8,
            ("surfaceKind", "package-config"), ("packageName", "Synthetic.Dependency")));
        facts.Add(Fact(FactTypes.CallEdge, "future.call.rule.v1", "Sample.Store.Save()", "surface-call", 9,
            ("surfaceKind", "sql-query"), ("operationName", "SELECT")));
        var first = Write(Directory.CreateDirectory(Path.Combine(temp.Path, "first")).FullName, facts);
        var second = Write(Directory.CreateDirectory(Path.Combine(temp.Path, "second")).FullName,
            [Fact(FactTypes.MethodDeclared, "csharp.semantic.method.v1", "Other.Store.Save()", null, 40)]);
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([first, second], combined, ["page", "competitor"]));
        var hash = Hash(combined);
        var expected = await CombinedDependencyPathReporter.BuildReportAsync(PathOptions(combined));
        var budget = new ReportInputBudget(1000, 1000, 1_000_000);
        var actual = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(PathOptions(combined), budget);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual.Report));
        Assert.Equal(facts.Count + 1, budget.FactsVisited);
        Assert.Equal(facts.Count + 1, budget.FactsRetained);
        Assert.Contains(actual.Report.Paths.SelectMany(path => path.SupportingFactIds), id => id.EndsWith(":001-combined-witness", StringComparison.Ordinal));
        Assert.Equal(hash, Hash(combined));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Combined_legacy_extractor_columns_keep_the_full_reader_verdict(bool missingId, bool missingVersion)
    {
        using var temp = new TempDirectory();
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, Fixture())], combined, ["page"]));
        await using (var connection = new SqliteConnection($"Data Source={combined}"))
        {
            await connection.OpenAsync();
            foreach (var column in new[] { missingId ? "extractor_id" : null, missingVersion ? "extractor_version" : null }.OfType<string>())
            {
                await using var command = connection.CreateCommand();
                command.CommandText = column == "extractor_id"
                    ? "alter table combined_facts drop column extractor_id;"
                    : "alter table combined_facts drop column extractor_version;";
                await command.ExecuteNonQueryAsync();
            }
        }
        var hash = Hash(combined);
        var expected = await CombinedDependencyPathReporter.BuildReportAsync(PathOptions(combined));
        var actual = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(PathOptions(combined), Budget());
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual.Report));
        Assert.Equal(hash, Hash(combined));
    }

    [Theory]
    [InlineData("{\"sourceSymbolId\":\"wrong\",\"sourceSymbolId\":\"retained-exact\",\"argumentCount\":\"0\",\"argumentCount\":\"2\"}", true)]
    [InlineData("{\"sourceSymbolId\":42}", false)]
    [InlineData("[\"not-a-property-object\"]", false)]
    [InlineData("{\"sourceSymbolId\":{\"unsafe\":\"nested\"}}", false)]
    public async Task Unprojectable_graph_properties_keep_the_full_readers_semantics(string properties, bool duplicateKeys)
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        var call = facts.Single(fact => fact.FactType == FactTypes.CallEdge);
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, facts)], combined, ["page"]));
        await using var connection = new SqliteConnection($"Data Source={combined};Mode=ReadOnly;Pooling=False");
        await using (var writer = new SqliteConnection($"Data Source={combined}"))
        {
            await writer.OpenAsync();
            await using var update = writer.CreateCommand();
            update.CommandText = "update combined_facts set properties_json=$properties where original_fact_id=$id;";
            update.Parameters.AddWithValue("$id", call.FactId);
            update.Parameters.AddWithValue("$properties", properties);
            await update.ExecuteNonQueryAsync();
        }
        var hash = Hash(combined);
        await connection.OpenAsync();
        var expected = await CombinedDependencyReporter.ReadAsync(connection, CancellationToken.None);
        var budget = Budget();
        var actual = await CombinedDependencyReporter.ReadAsync(connection, CancellationToken.None,
            (input, sources, hasId, hasVersion, token) => CombinedDependencyPathReporter.ReadCompactCombinedFactsAsync(
                input, sources, hasId, hasVersion, budget, token), budget);
        var expectedCall = expected.Facts.Single(fact => fact.OriginalFactId == call.FactId);
        var actualCall = actual.Facts.Single(fact => fact.OriginalFactId == call.FactId);
        Assert.Equal(JsonSerializer.Serialize(expectedCall.Properties), JsonSerializer.Serialize(actualCall.Properties));
        if (duplicateKeys)
        {
            Assert.Equal("retained-exact", actualCall.Properties["sourceSymbolId"]);
            Assert.Equal("2", actualCall.Properties["argumentCount"]);
        }
        else Assert.Empty(actualCall.Properties);
        Assert.Equal(hash, Hash(combined));
    }

    [Fact]
    public async Task Combined_compact_reader_does_not_allocate_unused_witness_payload()
    {
        using var temp = new TempDirectory();
        var index = Write(temp.Path, Fixture());
        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                with recursive seq(n) as (select 1 union all select n+1 from seq where n < 1000)
                insert into facts
                select printf('zz-compact-%08d', n), scan_id, repo, commit_sha, project_path,
                       'ArgumentPassed', 'csharp.semantic.argument.v1', evidence_tier,
                       'Sample.Page.Load()', 'Sample.Store.Save()', contract_element,
                       file_path, start_line, end_line, snippet_hash, extractor_id, extractor_version, $properties
                from seq cross join (select * from facts order by fact_id limit 1);
                """;
            insert.Parameters.AddWithValue("$properties", JsonSerializer.Serialize(new { unusedPayload = new string('x', 16_000) }));
            await insert.ExecuteNonQueryAsync();
        }
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["page"]));
        var options = PathOptions(combined);
        // Warm both code paths before comparing managed allocations; this is a
        // regression bound, not a peak-working-set or representative-scale claim.
        await CombinedDependencyPathReporter.BuildReportAsync(options);
        await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(options, LargeBudget());
        var start = GC.GetTotalAllocatedBytes(precise: true);
        var expected = await CombinedDependencyPathReporter.BuildReportAsync(options);
        var fullBytes = GC.GetTotalAllocatedBytes(precise: true) - start;
        start = GC.GetTotalAllocatedBytes(precise: true);
        var budget = LargeBudget();
        var actual = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(options, budget);
        var compactBytes = GC.GetTotalAllocatedBytes(precise: true) - start;
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual.Report));
        Assert.Equal(1000 + Fixture().Count, budget.FactsRetained);
        Assert.Equal(budget.FactsRetained, actual.GraphStorage!.StoredFacts);
        Assert.True(compactBytes < fullBytes, $"compact={compactBytes};full={fullBytes}");
        output.WriteLine($"combinedFullAllocatedBytes={fullBytes};combinedCompactAllocatedBytes={compactBytes};factsRetained={budget.FactsRetained};textRetained={budget.TextBytesRetained}");
        static ReportInputBudget LargeBudget() => new(10_000, 10_000, 64L * 1024 * 1024);
    }

    [Fact]
    public async Task Combined_compact_reader_refuses_noncanonical_fact_namespaces_instead_of_reconstructing_identity()
    {
        using var temp = new TempDirectory();
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, Fixture())], combined, ["page"]));
        await using (var connection = new SqliteConnection($"Data Source={combined}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "update combined_facts set combined_fact_id='noncanonical' where combined_fact_id=(select min(combined_fact_id) from combined_facts);";
            await command.ExecuteNonQueryAsync();
        }
        var hash = Hash(combined);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(PathOptions(combined), Budget()));
        Assert.Equal("COMBINED_FACT_NAMESPACE_INVALID", exception.Message);
        Assert.Equal(hash, Hash(combined));
    }

    [Fact]
    public async Task Combined_edge_text_is_admitted_before_managed_allocation_or_path_classification()
    {
        using var temp = new TempDirectory();
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Write(temp.Path, Fixture())], combined, ["page"]));
        await using (var connection = new SqliteConnection($"Data Source={combined}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "update combined_call_edges set callee_symbol=$symbol;";
            command.Parameters.AddWithValue("$symbol", new string('x', 2 * 1024 * 1024));
            await command.ExecuteNonQueryAsync();
        }
        var hash = Hash(combined);
        var actual = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(
            PathOptions(combined), new ReportInputBudget(1000, 1000, 16L * 1024 * 1024));
        Assert.Empty(actual.Report.Paths);
        Assert.Contains(actual.Report.Gaps, gap => gap.GapKind == "GraphInputLimitReached" && gap.Reason == "row-text-bytes");
        Assert.Equal(hash, Hash(combined));
    }

    [Fact]
    public async Task Compact_input_preserves_exact_graph_paths_provenance_and_global_symbol_ambiguity()
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        var firstWitness = Fact(FactTypes.ArgumentPassed, "csharp.semantic.argument.v1", "Sample.Page.Load()", "Save", 3,
            ("largeUnusedValue", new string('x', 8_000))) with
        { FactId = "000-witness" };
        var referencedWitness = firstWitness with { FactId = "001-referenced" };
        facts.Add(firstWitness);
        facts.Add(referencedWitness);
        facts.Add(Fact(FactTypes.MethodDeclared, "csharp.semantic.method.v1", "Other.Store.Save()", null, 4));
        facts.Add(Fact(FactTypes.ArgumentPassed, "csharp.semantic.argument.v1", "Sample.Page.Load()", "Save", 5));
        var handler = facts.Single(fact => fact.FactType == FactTypes.WebFormsHandlerResolved);
        facts[facts.IndexOf(handler)] = handler with
        {
            Properties = new SortedDictionary<string, string>(handler.Properties.ToDictionary())
            {
                ["supportingFactIds"] = referencedWitness.FactId
            }
        };
        // A known syntax type can still declare a dependency surface. It must
        // keep its properties and not be compacted into a symbol witness.
        facts.Add(Fact(FactTypes.ArgumentPassed, "csharp.semantic.argument.v1", "Sample.Store.Save()", "config", 6,
            ("surfaceKind", "package-config"), ("packageName", "Synthetic.Dependency")));
        facts.Add(Fact("FutureFactKind", "future.rule.v1", "Sample.Store.Save()", "future", 7,
            ("surfaceKind", "sql-query"), ("operationName", "SELECT")));
        var duplicateSurface = Fact(FactTypes.ArgumentPassed, "csharp.semantic.argument.v1", "Sample.Store.Save()", "duplicate", 8,
            ("surfaceKind", "sql-query"), ("operationName", "SELECT"));
        facts.Add(duplicateSurface);
        var index = Write(temp.Path, facts);
        // System.Text.Json retains the last duplicate key while SQLite's path
        // lookup sees the first. Any surfaceKind presence must prevent pruning.
        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var update = connection.CreateCommand();
            update.CommandText = "update facts set properties_json=$properties where fact_id=$id";
            update.Parameters.AddWithValue("$id", duplicateSurface.FactId);
            update.Parameters.AddWithValue("$properties", """{"surfaceKind":"","surfaceKind":"sql-query","operationName":"SELECT"}""");
            await update.ExecuteNonQueryAsync();
        }
        var options = PathOptions(index);
        var expected = await CombinedDependencyPathReporter.BuildReportAsync(options);
        var budget = Budget();
        var actual = await CombinedDependencyPathReporter.BuildBoundedSingleIndexReportAsync(options, budget);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.True(budget.FactsRetained < facts.Count);
        Assert.Contains(actual.Paths.SelectMany(path => path.SupportingFactIds), id => id == "single:001-referenced");
    }

    [Fact]
    public async Task Competing_symbol_outside_the_root_path_cannot_be_hidden_to_create_a_unique_SQL_path()
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        facts[3] = facts[3] with { TargetSymbol = "Save" };
        var uniqueDirectory = Directory.CreateDirectory(Path.Combine(temp.Path, "unique")).FullName;
        var unique = await CombinedDependencyPathReporter.BuildReportAsync(PathOptions(Write(uniqueDirectory, facts)));
        Assert.Contains(unique.Paths, path => path.Nodes.Last().SurfaceKind == "sql-query");

        facts.Add(Fact(FactTypes.MethodDeclared, "csharp.semantic.method.v1", "Other.Store.Save()", null, 40)
            with
        { FactId = "zz-competing-symbol" });
        var options = PathOptions(Write(temp.Path, facts)) with
        {
            StartingFactIds = new HashSet<string>(StringComparer.Ordinal)
            {
                "single:" + facts.Single(fact => fact.FactType == FactTypes.WebFormsHandlerResolved).FactId
            }
        };
        var full = await CombinedDependencyPathReporter.BuildReportAsync(options);
        var compact = await CombinedDependencyPathReporter.BuildBoundedSingleIndexReportAsync(options, Budget());
        Assert.DoesNotContain(full.Paths, path => path.Nodes.Last().SurfaceKind == "sql-query");
        Assert.DoesNotContain(compact.Paths, path => path.Nodes.Last().SurfaceKind == "sql-query");
        Assert.DoesNotContain(compact.Gaps, gap => gap.GapKind == "GraphInputLimitReached");
    }

    [Fact]
    public async Task Large_repetitive_fact_payload_does_not_scale_retained_graph_input()
    {
        using var temp = new TempDirectory();
        var index = Write(temp.Path, Fixture());
        var options = PathOptions(index);
        var before = await CombinedDependencyPathReporter.BuildBoundedSingleIndexReportAsync(options, Budget());
        var count = int.TryParse(Environment.GetEnvironmentVariable("TRACEMAP_MEMORY_TEST_ROWS"), out var configured)
            ? Math.Clamp(configured, 100_000, 2_000_000) : 100_000;
        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                with recursive seq(n) as (select 1 union all select n+1 from seq where n < $count)
                insert into facts
                select printf('zz-noise-%08d', n), scan_id, repo, commit_sha, project_path,
                       'ArgumentPassed', 'csharp.semantic.argument.v1', evidence_tier,
                       'Sample.Page.Load()', 'Sample.Store.Save()', contract_element,
                       file_path, start_line, end_line, snippet_hash, extractor_id, extractor_version, $properties
                from seq cross join (select * from facts order by fact_id limit 1);
                """;
            insert.Parameters.AddWithValue("$count", count);
            insert.Parameters.AddWithValue("$properties", JsonSerializer.Serialize(new { unusedPayload = new string('x', 1024) }));
            await insert.ExecuteNonQueryAsync();
        }
        var hash = Hash(index);
        // Opt-in comparison only: keep normal CI on the bounded reader. Run
        // in a separate test host to compare peak working sets fairly.
        var fullReader = Environment.GetEnvironmentVariable("TRACEMAP_MEMORY_TEST_FULL_READER") == "1";
        long? originalAllocatedBytes = null;
        if (fullReader)
        {
            var originalStartBytes = GC.GetTotalAllocatedBytes(precise: true);
            var original = await CombinedDependencyPathReporter.BuildReportAsync(options);
            originalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - originalStartBytes;
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(original));
        }
        var budget = new ReportInputBudget(100, 100, 64 * 1024);
        var boundedStartBytes = GC.GetTotalAllocatedBytes(precise: true);
        var actual = await CombinedDependencyPathReporter.BuildBoundedSingleIndexReportAsync(options, budget);
        var boundedAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - boundedStartBytes;
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(actual));
        Assert.True(budget.FactsVisited >= count);
        Assert.True(budget.FactsRetained < 20);
        Assert.True(budget.TextBytesRetained < 64 * 1024);
        Assert.Equal(hash, Hash(index));
        output.WriteLine($"fullReaderComparison={fullReader}; noiseRows={count}; factsVisited={budget.FactsVisited}; factsRetained={budget.FactsRetained}; edgesRetained={budget.EdgesRetained}; retainedTextBytes={budget.TextBytesRetained}; indexBytes={new FileInfo(index).Length}; originalAllocatedBytes={originalAllocatedBytes}; boundedAllocatedBytes={boundedAllocatedBytes}");
    }

    [Fact]
    public async Task Graph_input_limits_keep_inventory_but_never_classify_an_incomplete_graph()
    {
        using var temp = new TempDirectory();
        var index = Write(temp.Path, Fixture());
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, "unused",
            MaxGaps: 1, MaxInputFacts: 100, MaxInputEdges: 1, MaxInputTextBytes: 100_000));
        Assert.True(packet.Summary.Truncated);
        Assert.Equal("reduced-static-webforms-modernization", packet.Coverage);
        Assert.Contains(packet.Gaps, gap => gap.Classification == "WebFormsModernizationInputLimitReached"
            && gap.EvidenceTier == EvidenceTiers.Tier4Unknown && gap.RuleId == WebFormsModernizationPacketReporter.PacketRuleId);
        Assert.Empty(packet.DownstreamBoundaries);
        Assert.All(packet.EventChains, chain =>
        {
            Assert.Equal("UnknownAnalysisGap", chain.Classification);
            Assert.Null(chain.LegacyPathId);
            Assert.Null(chain.TerminalKind);
        });
        Assert.DoesNotContain(packet.Gaps, gap => gap.Classification == "NoBackendEvidence");
    }

    [Theory]
    [InlineData("graph-facts")]
    [InlineData("graph-edges")]
    [InlineData("graph-text-bytes")]
    public async Task Combined_graph_limits_preserve_inventory_and_never_classify_partial_paths(string limit)
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        if (limit == "graph-facts")
            for (var i = 0; i < 100; i++)
                facts.Add(Fact(FactTypes.ArgumentPassed, "csharp.semantic.argument.v1", "Noise" + i, null, 10 + i));
        if (limit == "graph-text-bytes")
            facts.Add(Fact(FactTypes.ArgumentPassed, "csharp.semantic.argument.v1", "Noise", null, 10,
                ("largeUnusedValue", new string('x', 100_000))));
        var index = Write(temp.Path, facts);
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["existing-publish"]));
        var hash = Hash(combined);
        var options = new WebFormsModernizationOptions(combined, "unused", MaxGaps: 1,
            MaxInputFacts: 100, MaxInputEdges: limit == "graph-edges" ? 1 : 100,
            MaxInputTextBytes: limit == "graph-text-bytes" ? 50_000 : 1_000_000);
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(options);
        Assert.True(packet.Summary.Truncated);
        Assert.Equal("reduced-static-webforms-modernization", packet.Coverage);
        Assert.Single(packet.Surfaces);
        Assert.Single(packet.Sources);
        Assert.Contains(packet.Gaps, gap => gap.Classification == "WebFormsModernizationInputLimitReached"
            && gap.EvidenceTier == EvidenceTiers.Tier4Unknown);
        Assert.Empty(packet.DownstreamBoundaries);
        Assert.All(packet.EventChains, chain =>
        {
            Assert.Equal("UnknownAnalysisGap", chain.Classification);
            Assert.Null(chain.LegacyPathId);
            Assert.Null(chain.TerminalKind);
        });
        Assert.DoesNotContain(packet.Gaps, gap => gap.Classification == "NoBackendEvidence");
        var graph = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(
            PathOptions(combined), new ReportInputBudget(100, limit == "graph-edges" ? 1 : 100,
                limit == "graph-text-bytes" ? 50_000 : 1_000_000));
        Assert.Contains(graph.Report.Gaps, gap => gap.GapKind == "GraphInputLimitReached" && gap.Reason == limit);
        Assert.Empty(graph.Report.Paths);
        Assert.Equal(JsonSerializer.Serialize(packet), JsonSerializer.Serialize(
            await WebFormsModernizationPacketReporter.BuildAsync(options)));
        var written = await WebFormsModernizationPacketReporter.WriteAsync(
            options with { OutputDirectory = Path.Combine(temp.Path, "packet") });
        Assert.Contains("WebFormsModernizationInputLimitReached", await File.ReadAllTextAsync(written.JsonPath));
        Assert.Equal(hash, Hash(combined));
    }

    [Theory]
    [InlineData(5, 100_000)]
    [InlineData(100, 3_000)]
    public async Task Snapshot_and_graph_receive_independent_bounded_budgets(int facts, int bytes)
    {
        using var temp = new TempDirectory();
        var index = Write(temp.Path, Fixture());
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, "unused",
            MaxGaps: 10, MaxInputFacts: facts, MaxInputEdges: 100, MaxInputTextBytes: bytes));

        Assert.DoesNotContain(packet.Gaps, gap => gap.Classification == "WebFormsModernizationInputLimitReached");
        var chain = Assert.Single(packet.EventChains);
        Assert.NotEqual("UnknownAnalysisGap", chain.Classification);
        Assert.NotNull(chain.TerminalKind);
        Assert.Single(packet.DownstreamBoundaries);
    }

    [Theory]
    [InlineData("FutureFactKind")]
    [InlineData(FactTypes.ArgumentPassed)]
    public async Task Oversized_graph_payload_fails_closed_before_JSON_allocation(string factType)
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        facts.Add(Fact(factType, "future.rule.v1", "Sample.Page.Load()", null, 30,
            ("unusedPayload", new string('x', ReportInputBudget.MaxRowTextBytes + 1))));
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(Write(temp.Path, facts), "unused"));
        Assert.Contains(packet.Surfaces, surface => surface.Evidence.FilePath == "Pages/Page.aspx");
        Assert.Contains(packet.Gaps, gap => gap.Classification == "WebFormsModernizationInputLimitReached"
            && gap.ScopeId == "row-text-bytes");
        Assert.Empty(packet.DownstreamBoundaries);
    }

    [Fact]
    public async Task Selected_handler_neighborhood_ignores_unrelated_fact_rows_without_hiding_its_path()
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        for (var index = 0; index < 100; index++)
            facts.Add(Fact(FactTypes.ArgumentPassed, "csharp.semantic.argument.v1",
                $"Unrelated.Type{index}.Method()", $"Unrelated.Target{index}.Method()", 100 + index));

        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(Write(temp.Path, facts), "unused",
            MaxInputFacts: 25, MaxInputEdges: 25, MaxInputTextBytes: 100_000));

        var chain = Assert.Single(packet.EventChains);
        Assert.NotNull(chain.LegacyPathId);
        Assert.Single(packet.DownstreamBoundaries);
        Assert.DoesNotContain(packet.Gaps, gap => gap.Classification == "WebFormsModernizationInputLimitReached");
    }

    [Fact]
    public async Task Snapshot_limit_preserves_admitted_page_and_reports_incomplete_inventory()
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        facts[0] = facts[0] with { FactId = "000-page" };
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(Write(temp.Path, facts), "unused",
            MaxGaps: 1, MaxInputFacts: 1));
        Assert.Single(packet.Surfaces);
        Assert.True(packet.Summary.Truncated);
        Assert.Contains(packet.Gaps, gap => gap.Classification == "WebFormsModernizationInputLimitReached"
            && gap.ScopeId == "snapshot-fact-rows");
        Assert.Empty(packet.EventChains);
        Assert.Empty(packet.DownstreamBoundaries);
    }

    [Fact]
    public async Task More_than_250_webforms_roots_are_considered_with_explicit_packet_bounds()
    {
        using var temp = new TempDirectory();
        var facts = Enumerable.Range(0, 518).SelectMany(index => Fixture(index.ToString("D4"))).ToArray();
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(Write(temp.Path, facts), "unused",
            MaxSurfaces: 600, MaxEventChains: 600, MaxPaths: 600, MaxBoundaries: 600));
        Assert.Equal(518, packet.Surfaces.Count);
        Assert.Equal(518, packet.EventChains.Count);
        Assert.Equal(518, packet.DownstreamBoundaries.Count);
        Assert.All(packet.EventChains, chain => Assert.NotNull(chain.LegacyPathId));
        Assert.DoesNotContain(packet.Gaps, gap => gap.Classification == "WebFormsModernizationInputLimitReached");
    }

    [Fact]
    public async Task Event_chain_limit_selects_paths_for_the_bindings_retained_by_the_packet()
    {
        using var temp = new TempDirectory();
        var facts = Fixture("retained", "Z.Sample.Page.Load()", "Pages/A.aspx", "Pages/Z.aspx")
            .Concat(Fixture("omitted", "A.Sample.Page.Load()", "Pages/B.aspx", "Pages/A.aspx"));
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(Write(temp.Path, facts), "unused",
            MaxSurfaces: 10, MaxEventChains: 1, MaxPaths: 10, MaxBoundaries: 10));

        var chain = Assert.Single(packet.EventChains);
        Assert.NotNull(chain.LegacyPathId);
        Assert.Single(packet.DownstreamBoundaries);
        Assert.DoesNotContain(packet.Gaps, gap => gap.Classification == "NoBackendEvidence");
        Assert.True(packet.Summary.Truncated);
    }

    [Fact]
    public async Task Streaming_publication_matches_existing_JSON_contract_and_honors_cancellation()
    {
        using var temp = new TempDirectory();
        var index = Write(temp.Path, Fixture());
        var output = Path.Combine(temp.Path, "output");
        var result = await WebFormsModernizationPacketReporter.WriteAsync(new(index, output));
        var expected = JsonSerializer.Serialize(result.Packet, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }) + "\n";
        Assert.Equal(expected, await File.ReadAllTextAsync(result.JsonPath));
        var cancelledOutput = Path.Combine(temp.Path, "cancelled");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            WebFormsModernizationPacketReporter.WriteAsync(new(index, cancelledOutput), cts.Token));
        Assert.False(Directory.Exists(cancelledOutput));
    }

    private static ReportInputBudget Budget() => new(250_000, 250_000, 128 * 1024 * 1024);
    private static CombinedDependencyPathOptions PathOptions(string index) => new(index, "unused",
        View: LegacyFlowReportConstants.View, IncludeLegacyRoots: true, MaxPaths: 1_000);
    private static ScanManifest Manifest() => new("scan-memory-fixture", "synthetic-memory-repo", null, "dev",
        "0123456789abcdef0123456789abcdef01234567", "scanner-test", DateTimeOffset.Parse("2026-08-30T00:00:00Z"),
        "Level1SemanticAnalysisReduced", "FailedOrPartial", [], [], [], ["Synthetic memory fixture."], ".", "scan-root", "git-root");
    private static string Write(string directory, IEnumerable<CodeFact> facts)
    {
        var path = Path.Combine(directory, "index.sqlite");
        SqliteIndexWriter.Write(path, Manifest(), facts.ToArray());
        return path;
    }
    private static List<CodeFact> Fixture(
        string suffix = "",
        string? methodOverride = null,
        string? filePath = null,
        string? markupFileOverride = null)
    {
        var surface = "surface:page" + suffix;
        var method = methodOverride ?? $"Sample.Page{suffix}.Load()";
        var save = $"Sample.Store{suffix}.Save()";
        var markupFile = markupFileOverride ?? filePath ?? "Pages/Page.aspx";
        var page = Fact(FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, surface, "Sample.Page" + suffix, 1,
            ("surfaceIdentity", surface), ("directiveKind", "Page"));
        var binding = Fact(FactTypes.WebFormsEventBindingDeclared, RuleIds.LegacyWebFormsEventBinding,
            "control:load" + suffix, method, 2, ("surfaceIdentity", surface), ("eventName", "OnLoad"),
            ("eventSourceIdentity", "control:load" + suffix), ("handlerName", "Load"), ("markupFile", markupFile));
        var handler = Fact(FactTypes.WebFormsHandlerResolved, RuleIds.LegacyWebFormsHandlerResolution,
            "control:load" + suffix, method, 3, ("surfaceIdentity", surface), ("bindingFactId", binding.FactId),
            ("handlerSymbolId", method), ("handlerSymbol", method), ("supportingFactIds", binding.FactId),
            ("eventName", "OnLoad"), ("handlerName", "Load"), ("markupFile", markupFile));
        var call = Fact(FactTypes.CallEdge, "csharp.semantic.call.v1", method, save, 4);
        var terminal = Fact(FactTypes.QueryPatternDetected, RuleIds.CSharpSyntaxQueryPattern, save, "query:" + suffix, 5,
            ("operationName", "SELECT"), ("tableName", "synthetic_orders"), ("sqlSourceKind", "literal-string"));
        var facts = new List<CodeFact> { page, binding, handler, call, terminal };
        if (filePath is not null)
            for (var index = 0; index < facts.Count; index++)
                facts[index] = facts[index] with { Evidence = facts[index].Evidence with { FilePath = filePath } };
        return facts;
    }
    private static CodeFact Fact(string type, string rule, string? source, string? target, int line,
        params (string Key, string Value)[] properties) => FactFactory.Create(Manifest(), type, rule, EvidenceTiers.Tier2Structural,
        new("Pages/Page.aspx", line, line, null, "SyntheticMemoryFixture", "1.0"),
        sourceSymbol: source, targetSymbol: target,
        properties: new SortedDictionary<string, string>(properties.ToDictionary(pair => pair.Key, pair => pair.Value))
        { ["coverageLabel"] = "bounded-static-synthetic" });
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
