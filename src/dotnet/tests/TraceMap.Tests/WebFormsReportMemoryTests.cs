using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;
using Xunit.Abstractions;

namespace TraceMap.Tests;

public sealed class WebFormsReportMemoryTests(ITestOutputHelper output)
{
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
        var options = PathOptions(Write(temp.Path, facts));
        var full = await CombinedDependencyPathReporter.BuildReportAsync(options);
        var compact = await CombinedDependencyPathReporter.BuildBoundedSingleIndexReportAsync(options, Budget());
        Assert.Equal(JsonSerializer.Serialize(full), JsonSerializer.Serialize(compact));
        Assert.DoesNotContain(compact.Paths, path => path.Nodes.Last().SurfaceKind == "sql-query");
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

    [Theory]
    [InlineData(5, 100, 100_000)]
    [InlineData(100, 1, 100_000)]
    [InlineData(100, 100, 2_000)]
    public async Task Input_limits_keep_inventory_but_never_classify_an_incomplete_graph(int facts, int edges, int bytes)
    {
        using var temp = new TempDirectory();
        var index = Write(temp.Path, Fixture());
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, "unused",
            MaxGaps: 1, MaxInputFacts: facts, MaxInputEdges: edges, MaxInputTextBytes: bytes));
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
    [InlineData("FutureFactKind")]
    [InlineData(FactTypes.ArgumentPassed)]
    public async Task Oversized_graph_payload_fails_closed_before_JSON_allocation(string factType)
    {
        using var temp = new TempDirectory();
        var facts = Fixture();
        facts.Add(Fact(factType, "future.rule.v1", null, null, 30,
            ("unusedPayload", new string('x', ReportInputBudget.MaxRowTextBytes + 1))));
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(Write(temp.Path, facts), "unused"));
        Assert.Contains(packet.Surfaces, surface => surface.Evidence.FilePath == "Pages/Page.aspx");
        Assert.Contains(packet.Gaps, gap => gap.Classification == "WebFormsModernizationInputLimitReached"
            && gap.ScopeId == "row-text-bytes");
        Assert.Empty(packet.DownstreamBoundaries);
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
