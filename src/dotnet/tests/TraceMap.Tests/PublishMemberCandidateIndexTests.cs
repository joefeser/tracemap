using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class PublishMemberCandidateIndexTests
{
    [Fact]
    public void Page_join_inventory_reads_each_typed_roster_once_and_retains_all_competitors()
    {
        var row = Method("Wanted", "Entry", "hash-a");
        var facts = new List<CombinedFactRow>
        {
            row, row with { CombinedFactId = "duplicate-method" },
            Properties(row with { CombinedFactId = "unbound-method" }, ("provenanceState", "unbound")),
            row with { FactType = FactTypes.WebFormsPageDeclared, FilePath = "Page.aspx" },
            row with { FactType = FactTypes.WebFormsPageDeclared, FilePath = "Page.aspx", CombinedFactId = "duplicate-page" },
            row with { FactType = FactTypes.ManagedTypeDeclared },
            Properties(row with { FactType = FactTypes.ManagedTypeDeclared, CombinedFactId = "unbound-type" }, ("provenanceState", "unbound")),
            Properties(row with { FactType = FactTypes.WebFormsHandlerResolved }, ("markupFile", "Page.aspx")),
            row with { FactType = FactTypes.WebFormsPublishSourceBound, FilePath = "Page.aspx.vb" },
            row with { FactType = FactTypes.WebFormsPublishSourceBound, FilePath = "Page.aspx.vb", CombinedFactId = "duplicate-binding" },
            row with { FactType = FactTypes.MethodDeclared, RuleId = RuleIds.VisualBasicSyntaxDeclarations, FilePath = "Page.aspx.vb" }
        };
        facts.AddRange(Enumerable.Range(0, 1000).Select(number => Method("Sparse" + number, "Unused" + number, "hash-a")));
        facts.AddRange(Enumerable.Range(0, 7000).Select(number => row with
            { FactType = "UnknownPublicFixture", CombinedFactId = "irrelevant-" + number }));
        var counted = new CountedFacts(facts);
        var inventory = new CombinedDependencyPathReporter.PublishPageCandidateInventory(counted);
        Assert.Equal(7, counted.Enumerations);
        Assert.Equal(7L * facts.Count, counted.RowsRead);
        for (var page = 0; page < 1000; page++)
        {
            Assert.Equal(2, inventory.Pages[("source", "Page.aspx")].Length);
            Assert.Equal(2, inventory.Types["source"].Length);
            Assert.Contains(inventory.Types["source"], type => type.Properties["provenanceState"] == "unbound");
            Assert.Single(inventory.Handlers[("source", "Page.aspx")]);
            Assert.Equal(2, inventory.Bindings[("source", "Page.aspx.vb")].Length);
            Assert.Single(inventory.Declarations[("source", "Page.aspx.vb")]);
            var selected = inventory.Methods.Select("source", "ENTRY", TypePath("Wanted"), new HashSet<string?> { "hash-a" });
            Assert.Equal(2, selected.QualifiedCandidates.Count);
            Assert.Equal(2, selected.NamedCount);
            Assert.All(selected.QualifiedCandidates, method => Assert.Equal("bound", method.Properties["provenanceState"]));
        }
        Assert.Equal(7, counted.Enumerations);
        Assert.Equal(7L * facts.Count, counted.RowsRead);
        Assert.False(inventory.Pages.ContainsKey(("other-source", "Page.aspx")));
    }

    private sealed class CountedFacts(IReadOnlyList<CombinedFactRow> rows) : IReadOnlyList<CombinedFactRow>
    {
        internal int Enumerations { get; private set; }
        internal long RowsRead { get; private set; }
        public int Count => rows.Count;
        public CombinedFactRow this[int index] => rows[index];
        public IEnumerator<CombinedFactRow> GetEnumerator()
        {
            Enumerations++;
            foreach (var row in rows) { RowsRead++; yield return row; }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public async Task Qualified_competitor_work_exhaustion_withholds_the_entire_member_pass()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest();
        var hash = new string('c', 64);
        var input = new string('b', 64);
        CodeFact Fact(string type, string rule, string file, string? target, IReadOnlyDictionary<string, string> properties) =>
            FactFactory.Create(manifest, type, rule, EvidenceTiers.Tier2Structural,
                new(file, 1, 1, null, "PublicMemberIndexFixture", "1.0"), targetSymbol: target, properties: properties);
        var facts = new List<CodeFact>
        {
            Fact(FactTypes.WebFormsPublishAssemblyBound, RuleIds.LegacyWebFormsPublishMap, "bin/Public.dll", null,
                new Dictionary<string, string> { ["boundedInputSha256"] = input, ["assemblyRawSha256"] = hash }),
            Fact(FactTypes.WebFormsPublishSourceBound, RuleIds.LegacyWebFormsPublishMap, "App_Code/Wanted.vb", null,
                new Dictionary<string, string> { ["boundedInputSha256"] = input, ["sourcePath"] = "App_Code/Wanted.vb" })
        };
        var method = Method("Wanted", "Entry", hash);
        var compiled = Fact(FactTypes.ManagedMethodDeclared, RuleIds.DotNetCompiledMember,
            "bin/Public.dll", method.TargetSymbol, method.Properties);
        for (var index = 0; index < 1001; index++) facts.Add(compiled with { FactId = "compiled-competitor-" + index });
        for (var index = 0; index < 101; index++)
            facts.Add(Fact(FactTypes.MethodDeclared, RuleIds.VisualBasicSyntaxDeclarations, "App_Code/Wanted.vb", "Public.Wanted.Entry()",
                new Dictionary<string, string> { ["name"] = "Entry", ["qualifiedContainingType"] = "Public.Wanted",
                    ["memberIdentity"] = "Public.Wanted.Entry()", ["parameterCount"] = "0", ["parameterTypes"] = "" })
                with { FactId = "source-declaration-" + index });
        var path = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(path, manifest, facts);
        await CombinedIndexBuilder.CombineAsync(new([path], combined, ["public"]));
        var graph = await CombinedDependencyPathReporter.BuildGraphInventoryAsync(combined);
        Assert.DoesNotContain(graph.Edges, edge => edge.EdgeKind == "projectless-publish-member-candidate");
        Assert.Contains(graph.Gaps, gap => gap.GapKind == "ProjectlessPublishMemberWorkLimit"
            && gap.Reason == "bounded-publish-member-join-work-exceeded" && gap.EvidenceTier == EvidenceTiers.Tier4Unknown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Four_hundred_same_name_types_retain_qualified_edges_and_same_type_ambiguity(bool duplicate)
    {
        using var temp = new TempDirectory();
        var manifest = Manifest();
        const string inputHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string dllHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        CodeFact Fact(string type, string rule, string tier, string file, string? target, IReadOnlyDictionary<string, string> properties) =>
            FactFactory.Create(manifest, type, rule, tier, new(file, 1, 1, null, "PublicMemberIndexFixture", "1.0"),
                targetSymbol: target, properties: properties);
        var facts = new List<CodeFact>
        {
            Fact(FactTypes.WebFormsPublishAssemblyBound, RuleIds.LegacyWebFormsPublishMap, EvidenceTiers.Tier2Structural,
                "bin/Public.dll", null, new Dictionary<string, string> { ["boundedInputSha256"] = inputHash, ["assemblyRawSha256"] = dllHash })
        };
        for (var number = 0; number < 400; number++)
        {
            var name = $"Store{number:D4}";
            var file = "App_Code/" + name + ".vb";
            facts.Add(Fact(FactTypes.WebFormsPublishSourceBound, RuleIds.LegacyWebFormsPublishMap, EvidenceTiers.Tier2Structural,
                file, null, new Dictionary<string, string> { ["boundedInputSha256"] = inputHash, ["sourcePath"] = file }));
            facts.Add(Fact(FactTypes.MethodDeclared, RuleIds.VisualBasicSyntaxDeclarations, EvidenceTiers.Tier3SyntaxOrTextual,
                file, "Public." + name + ".Entry()", new Dictionary<string, string>
                { ["name"] = "Entry", ["qualifiedContainingType"] = "Public." + name,
                  ["memberIdentity"] = "Public." + name + ".Entry()", ["parameterCount"] = "0", ["parameterTypes"] = "" }));
            var method = Method(name, "Entry", dllHash);
            var compiled = Fact(FactTypes.ManagedMethodDeclared, RuleIds.DotNetCompiledMember, EvidenceTiers.Tier2Structural,
                "bin/Public.dll", method.TargetSymbol, method.Properties);
            facts.Add(compiled);
            if (duplicate && number == 0) facts.Add(compiled with { FactId = compiled.FactId + "-duplicate" });
        }
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(index, manifest, facts);
        await CombinedIndexBuilder.CombineAsync(new([index], combined, ["public"]));
        var graph = await CombinedDependencyPathReporter.BuildGraphInventoryAsync(combined);
        Assert.DoesNotContain(graph.Gaps, gap => gap.GapKind == "ProjectlessPublishMemberWorkLimit");
        var edges = graph.Edges.Where(edge => edge.EdgeKind == "projectless-publish-member-candidate").ToArray();
        Assert.Equal((400 - (duplicate ? 1 : 0)) * 2, edges.Length);
        for (var number = duplicate ? 1 : 0; number < 400; number++)
            Assert.Equal(2, edges.Count(edge => edge.FilePath == $"App_Code/Store{number:D4}.vb"));
        Assert.All(edges, edge => Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, edge.EvidenceTier));
        if (duplicate)
        {
            Assert.DoesNotContain(edges, edge => edge.FilePath == "App_Code/Store0000.vb");
            Assert.Contains(graph.Gaps, gap => gap.GapKind == "ProjectlessPublishMemberAmbiguous"
                && gap.FilePath == "App_Code/Store0000.vb" && gap.Reason == "multiple-qualified-compatible-members");
        }
    }

    [Fact]
    public void Qualified_index_retains_name_and_assembly_competitors_without_charging_unrelated_types()
    {
        var methods = Enumerable.Range(0, 256).SelectMany(type => new[]
        {
            Method($"Store{type:D4}", "Entry", "hash-a"), Method($"Store{type:D4}", "Entry", "hash-b")
        }).ToArray();
        var index = new CombinedDependencyPathReporter.PublishMemberCandidateIndex(methods);
        for (var type = 0; type < 256; type++)
        {
            var path = TypePath($"Store{type:D4}");
            Assert.Equal(2, index.CandidateWork("source", "ENTRY", path));
            var selected = index.Select("source", "ENTRY", path.ToLowerInvariant(), new HashSet<string?> { "hash-a", "hash-b" });
            Assert.Equal(512, selected.NamedCount);
            Assert.Equal(512, selected.BoundAssemblyCount);
            Assert.Equal(2, selected.QualifiedCandidates.Count);
            var historical = methods.Where(row => row.TargetSymbol!.Contains("|type:" + path + "|arity:0|method:", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(historical, selected.QualifiedCandidates);
        }
        Assert.Empty(index.Select("different-source", "Entry", TypePath("Store0000"), new HashSet<string?> { "hash-a" }).QualifiedCandidates);
        Assert.Single(index.Select("source", "Entry", TypePath("Store0000"), new HashSet<string?> { "hash-a" }).QualifiedCandidates);
    }

    [Theory]
    [InlineData("missing-assembly")]
    [InlineData("mismatched-assembly")]
    [InlineData("header-marker")]
    [InlineData("method-marker")]
    [InlineData("signature-marker")]
    [InlineData("nested")]
    [InlineData("malformed-length")]
    public void Opaque_or_delimiter_bearing_identity_retains_historical_matching_and_work(string mutation)
    {
        var ordinary = Method("Wanted", "Entry", "hash-a");
        var row = Method("Other", "Entry", "hash-b");
        var marker = "|type:" + TypePath("Wanted") + "|arity:0|method:";
        var assembly = row.Properties["assemblyIdentity"];
        var identity = row.TargetSymbol!;
        row = mutation switch
        {
            "missing-assembly" => Properties(row, ("assemblyIdentity", "")),
            "mismatched-assembly" => Properties(row, ("assemblyIdentity", "wrong")),
            "header-marker" => Properties(row with { TargetSymbol = assembly + marker + "header" + identity[assembly.Length..] },
                ("assemblyIdentity", assembly + marker + "header")),
            "method-marker" => row with { TargetSymbol = identity.Replace("|method:5:Entry", $"|method:{(marker + "Entry").Length}:{marker}Entry", StringComparison.Ordinal) },
            "signature-marker" => row with { TargetSymbol = identity + marker },
            "nested" => row with { TargetSymbol = identity.Replace(TypePath("Other"), TypePath("Other") + "6:Nested", StringComparison.Ordinal) },
            "malformed-length" => row with { TargetSymbol = identity.Replace("names:5:Other", "names:99:Other", StringComparison.Ordinal) },
            _ => throw new InvalidOperationException()
        };
        var methods = new[] { ordinary, row };
        var index = new CombinedDependencyPathReporter.PublishMemberCandidateIndex(methods);
        Assert.Equal(2, index.CandidateWork("source", "Entry", TypePath("Wanted")));
        var actual = index.Select("source", "Entry", TypePath("Wanted"), new HashSet<string?> { "hash-a", "hash-b" });
        Assert.Equal(2, actual.NamedCount);
        Assert.Equal(2, actual.BoundAssemblyCount);
        Assert.Equal(methods.Where(method => method.TargetSymbol!.Contains(marker, StringComparison.OrdinalIgnoreCase)), actual.QualifiedCandidates);
    }

    [Fact]
    public void Same_qualified_competitors_are_never_deduplicated_or_hidden_from_work_limit()
    {
        var row = Method("Wanted", "Entry", "hash-a");
        var competitors = Enumerable.Repeat(row, 1001).ToArray();
        var index = new CombinedDependencyPathReporter.PublishMemberCandidateIndex(competitors);
        Assert.Equal(1001, index.CandidateWork("source", "Entry", TypePath("Wanted")));
        Assert.True(101L * index.CandidateWork("source", "Entry", TypePath("Wanted")) > 100_000);
        var actual = index.Select("source", "Entry", TypePath("Wanted"), new HashSet<string?> { "hash-a" });
        Assert.Equal(1001, actual.NamedCount);
        Assert.Equal(1001, actual.BoundAssemblyCount);
        Assert.Equal(1001, actual.QualifiedCandidates.Count);
    }

    [Theory]
    [InlineData("absent-name", 0, 0, 0)]
    [InlineData("absent-hash", 1, 0, 0)]
    [InlineData("absent-type", 1, 1, 0)]
    public void Indexed_counts_preserve_gap_matching_stages(string mismatch, int named, int bound, int qualified)
    {
        var index = new CombinedDependencyPathReporter.PublishMemberCandidateIndex([Method("Wanted", "Entry", "hash-a")]);
        var actual = index.Select("source", mismatch == "absent-name" ? "Missing" : "Entry",
            TypePath(mismatch == "absent-type" ? "Other" : "Wanted"), new HashSet<string?> { mismatch == "absent-hash" ? "missing" : "hash-a" });
        Assert.Equal(named, actual.NamedCount);
        Assert.Equal(bound, actual.BoundAssemblyCount);
        Assert.Equal(qualified, actual.QualifiedCandidates.Count);
    }

    private static ScanManifest Manifest() => new("scan-public-member-index", "public-member-index", null, "dev", new('a', 40),
        "test", DateTimeOffset.Parse("2026-09-28T00:00:00Z"), "Level1SemanticAnalysisReduced", "FailedOrPartial",
        [], [], [], ["Generated public static fixture, not compilation or runtime proof."], ".", "snapshot", "git-root");
    private static string TypePath(string name) => $"namespace:6:Public|names:{name.Length}:{name}";
    private static CombinedFactRow Method(string type, string name, string hash)
    {
        const string assembly = "assembly:name:6:Public|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:10:Public.dll|targetFramework:7:unknown";
        const string signature = "arity:0|call:default|hasThis:false|explicitThis:false|()->type(namespace:6:System|names:4:Void)";
        return new(hash + type, "source", "public", hash + type, "scan", "public", new('a', 40),
            FactTypes.ManagedMethodDeclared, RuleIds.DotNetCompiledMember, EvidenceTiers.Tier2Structural,
            null, assembly + "|type:" + TypePath(type) + $"|arity:0|method:{name.Length}:{name}|" + signature,
            null, "bin/Public.dll", 1, 1, new Dictionary<string, string>
            { ["assemblyIdentity"] = assembly, ["metadataName"] = name, ["rawFileSha256"] = hash, ["provenanceState"] = "bound", ["signature"] = signature });
    }
    private static CombinedFactRow Properties(CombinedFactRow row, params (string Key, string Value)[] changes)
    {
        var properties = row.Properties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var (key, value) in changes) properties[key] = value;
        return row with { Properties = properties };
    }
}
