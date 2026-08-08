using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class ReverseImpactTraversalTests
{
    [Fact]
    public void Canonical_seed_traverses_callers_by_bounded_depth_and_breaks_cycles_deterministically()
    {
        var seed = Id("method", "Service.Target()");
        var caller = Id("method", "Controller.Get()");
        var root = Id("method", "Program.Main()");
        var facts = new[]
        {
            Relationship("call-z", FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, caller, "Controller.Get()", seed, "Service.Target()", "Calls", 20),
            Relationship("call-a", FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, caller, "Controller.Get()", seed, "Service.Target()", "Calls", 10),
            Relationship("call-root", FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, root, "Program.Main()", caller, "Controller.Get()", "Calls", 5),
            Relationship("call-cycle", FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, seed, "Service.Target()", root, "Program.Main()", "Calls", 30)
        };

        var result = ReverseImpactTraversal.Analyze(facts, new ReverseImpactOptions(seed, 2));
        var repeated = ReverseImpactTraversal.Analyze(facts.Reverse(), new ReverseImpactOptions(seed, 2));

        Assert.Equal("Resolved", result.Resolution);
        Assert.Equal(ReverseImpactContract.SchemaVersion, result.SchemaVersion);
        Assert.Equal("scan", result.Snapshot!.ScanId);
        Assert.Equal("0123456789012345678901234567890123456789", result.Snapshot.CommitSha);
        Assert.Equal([caller, root], result.Impacts.Select(impact => impact.Symbol.SymbolId));
        Assert.True(result.Impacts[0].IsDirect);
        Assert.False(result.Impacts[1].IsDirect);
        Assert.Equal(1, result.Impacts[0].Depth);
        Assert.Equal(2, result.Impacts[1].Depth);
        Assert.Equal("call-a", result.Impacts[0].Path[0].FactId);
        Assert.Equal("TargetToSource", result.Impacts[0].Path[0].TraversalDirection);
        Assert.Equal(
            result.Impacts.Select(impact => (impact.Symbol.SymbolId, impact.Depth, impact.PathId, HopIds: string.Join(";", impact.Path.Select(hop => hop.FactId)))),
            repeated.Impacts.Select(impact => (impact.Symbol.SymbolId, impact.Depth, impact.PathId, HopIds: string.Join(";", impact.Path.Select(hop => hop.FactId)))));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(repeated));
        Assert.DoesNotContain(result.Impacts, impact => impact.Symbol.SymbolId == seed);
    }

    [Fact]
    public void Relationship_filter_is_explicit_and_preserves_relationship_site_provenance()
    {
        var seed = Id("property", "Model.Name");
        var reader = Id("method", "Reader.Read()");
        var implementation = Id("method", "Implementation.Name.get");
        var reference = Relationship("property-ref", FactTypes.PropertyAccessed, RuleIds.CSharpSemanticPropertyAccess, reader, "Reader.Read()", seed, "Model.Name", "References", 44);
        var implementationFact = Relationship("implements", FactTypes.SymbolRelationship, RuleIds.CSharpSemanticSymbolRelationship, implementation, "Implementation.Name.get", seed, "Model.Name", "ImplementsInterfaceMember", 18);

        var onlyReferences = ReverseImpactTraversal.Analyze(
            [implementationFact, reference],
            new ReverseImpactOptions(seed, 1, ["references"]));

        var impact = Assert.Single(onlyReferences.Impacts);
        Assert.Equal(reader, impact.Symbol.SymbolId);
        Assert.Equal("references", impact.Path[0].RelationshipFilter);
        Assert.Equal(RuleIds.CSharpSemanticPropertyAccess, impact.Path[0].RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, impact.Path[0].EvidenceTier);
        Assert.Equal("Source.cs", impact.Path[0].Evidence.FilePath);
        Assert.Equal(44, impact.Path[0].Evidence.StartLine);
        Assert.Equal("csharp-semantic", impact.Path[0].Evidence.ExtractorId);
        Assert.Equal("1.2.3", impact.Path[0].Evidence.ExtractorVersion);
        Assert.Equal("0123456789012345678901234567890123456789", impact.Path[0].CommitSha);
        Assert.Equal(RuleIds.CSharpSemanticPropertyAccess, impact.Path[0].Evidence.RuleId);
    }

    [Theory]
    [InlineData("ExtendsInterface")]
    [InlineData("ImplementsInterface")]
    [InlineData("ImplementsInterfaceMember")]
    [InlineData("InheritsFrom")]
    [InlineData("Overrides")]
    public void Declared_semantic_relationship_kinds_opt_into_inheritance_impact(string relationshipKind)
    {
        var abstraction = Id("type", "Abstraction");
        var dependent = Id("type", $"Dependent.{relationshipKind}");
        var relationship = Relationship(
            $"relationship-{relationshipKind}",
            FactTypes.SymbolRelationship,
            RuleIds.CSharpSemanticSymbolRelationship,
            dependent,
            $"Dependent.{relationshipKind}",
            abstraction,
            "Abstraction",
            relationshipKind,
            8);

        var result = ReverseImpactTraversal.Analyze(
            [relationship],
            new ReverseImpactOptions(abstraction, 1, ["inheritance"]));

        var impact = Assert.Single(result.Impacts);
        Assert.Equal(dependent, impact.Symbol.SymbolId);
        Assert.Equal(relationshipKind, impact.Path[0].RelationshipKind);
    }

    [Fact]
    public void Type_seed_includes_callers_of_proven_contained_members()
    {
        var type = Id("type", "Service");
        var member = Id("method", "Service.Call()");
        var caller = Id("method", "Controller.Get()");
        var declaration = SymbolFact("member-declaration", member, "Service.Call()", "Method", type);
        var call = Relationship("call", FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, caller, "Controller.Get()", member, "Service.Call()", "Calls", 12);

        var included = ReverseImpactTraversal.Analyze([call, declaration], new ReverseImpactOptions(type, 1));
        var excluded = ReverseImpactTraversal.Analyze([call, declaration], new ReverseImpactOptions(type, 1, IncludeContainedMembers: false));

        Assert.Contains(included.TraversalSeeds, symbol => symbol.SymbolId == member);
        var impact = Assert.Single(included.Impacts);
        Assert.Equal(caller, impact.Symbol.SymbolId);
        Assert.Equal(member, impact.TraversalSeedSymbolId);
        Assert.Empty(excluded.Impacts);
    }

    [Fact]
    public void Exact_display_selector_fails_closed_for_overloads_or_cross_assembly_duplicates()
    {
        var first = Id("method", "A.Service.Call()");
        var second = Id("method", "B.Service.Call()");
        var facts = new[]
        {
            SymbolFact("one", first, "Service.Call()", "Method", assembly: "A"),
            SymbolFact("two", second, "Service.Call()", "Method", assembly: "B")
        };

        var result = ReverseImpactTraversal.Analyze(facts, new ReverseImpactOptions("service.call()", 2));

        Assert.Equal("Ambiguous", result.Resolution);
        Assert.Null(result.Seed);
        Assert.Equal([first, second], result.Candidates.Select(candidate => candidate.SymbolId));
        Assert.Empty(result.Impacts);
        Assert.Contains(result.Gaps, gap => gap.GapKind == "AmbiguousSelector" && gap.RuleId == RuleIds.ReverseImpactGap);
    }

    [Fact]
    public void Partial_type_occurrences_collapse_to_one_canonical_seed()
    {
        var type = Id("type", "Partial.Service");
        var facts = new[]
        {
            SymbolFact("part-b", type, "Partial.Service", "NamedType", file: "PartB.cs"),
            SymbolFact("part-a", type, "Partial.Service", "NamedType", file: "PartA.cs")
        };

        var result = ReverseImpactTraversal.Analyze(facts, new ReverseImpactOptions("Partial.Service", 1));

        Assert.Equal("Resolved", result.Resolution);
        Assert.Equal(type, result.Seed!.SymbolId);
        Assert.Single(result.TraversalSeeds);
    }

    [Fact]
    public void Resolved_queries_scope_gaps_to_visited_symbols_and_syntax_relationships_do_not_name_match()
    {
        var seed = Id("method", "Service.Target()");
        var semantic = SymbolFact("seed", seed, "Service.Target()", "Method");
        var syntaxCall = Fact(
            "syntax-call",
            FactTypes.CallEdge,
            RuleIds.CSharpSyntaxCallGraph,
            "Controller.Get()",
            "Service.Target()",
            new Dictionary<string, string>(),
            EvidenceTiers.Tier3SyntaxOrTextual,
            21);
        var gap = Fact(
            "unscoped-analysis-gap",
            FactTypes.AnalysisGap,
            RuleIds.AnalyzerCapabilitySemantic,
            null,
            null,
            new Dictionary<string, string> { ["message"] = "Project load failed." },
            EvidenceTiers.Tier4Unknown,
            1);
        var relatedGap = Fact(
            "related-analysis-gap",
            FactTypes.AnalysisGap,
            RuleIds.AnalyzerCapabilitySemantic,
            null,
            null,
            new Dictionary<string, string>
            {
                ["message"] = "Target body was only partially analyzed.",
                ["targetSymbolId"] = seed,
                ["targetSymbolDisplayName"] = "Service.Target()",
                ["targetSymbolKind"] = "Method",
                ["targetSymbolLanguage"] = "csharp"
            },
            EvidenceTiers.Tier4Unknown,
            2);

        var result = ReverseImpactTraversal.Analyze([syntaxCall, semantic, gap, relatedGap], new ReverseImpactOptions(seed, 1));

        Assert.Empty(result.Impacts);
        Assert.Contains(result.Gaps, candidate =>
            candidate.GapKind == ReverseImpactGapKinds.RelationshipMissingCanonicalIdentity
            && candidate.RelatedSymbolIds.Count == 0);
        var propagated = Assert.Single(result.Gaps, candidate => candidate.GapKind == "AnalysisGap");
        Assert.Equal("related-analysis-gap", propagated.GapId);
        Assert.Equal(RuleIds.AnalyzerCapabilitySemantic, propagated.RuleId);
        Assert.Equal("Target body was only partially analyzed.", propagated.Message);
        Assert.Equal(RuleIds.AnalyzerCapabilitySemantic, propagated.Evidence.RuleId);
        Assert.DoesNotContain(result.Gaps, candidate => candidate.GapId == "unscoped-analysis-gap");
    }

    [Fact]
    public void Not_found_selector_and_unknown_filter_fail_closed()
    {
        var analysisGap = Fact(
            "project-load-gap",
            FactTypes.AnalysisGap,
            RuleIds.AnalyzerCapabilitySemantic,
            null,
            null,
            new Dictionary<string, string> { ["message"] = "Project load failed." },
            EvidenceTiers.Tier4Unknown,
            1);
        var result = ReverseImpactTraversal.Analyze([analysisGap], new ReverseImpactOptions("Missing", 1));
        Assert.Equal("NotFound", result.Resolution);
        Assert.Empty(result.Impacts);
        Assert.Contains(result.Gaps, gap => gap.GapKind == "SelectorNotFound");
        Assert.Contains(result.Gaps, gap => gap.GapId == "project-load-gap" && gap.RuleId == RuleIds.AnalyzerCapabilitySemantic);

        Assert.Throws<ArgumentException>(() => ReverseImpactTraversal.Analyze([], new ReverseImpactOptions("Missing", 1, ["everything"])));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReverseImpactTraversal.Analyze([], new ReverseImpactOptions("Missing", 0)));
    }

    [Theory]
    [InlineData("scan")]
    [InlineData("repo")]
    [InlineData("commit")]
    public void Mixed_snapshots_fail_closed_before_graph_construction(string changedScope)
    {
        var seed = Id("method", "Service.Target()");
        var caller = Id("method", "Controller.Get()");
        var first = Relationship("first", FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, caller, "Controller.Get()", seed, "Service.Target()", "Calls", 10);
        var second = changedScope switch
        {
            "scan" => first with { FactId = "second", ScanId = "scan-two" },
            "repo" => first with { FactId = "second", Repo = "repo-two" },
            _ => first with { FactId = "second", CommitSha = "abcdefabcdefabcdefabcdefabcdefabcdefabcd" }
        };

        var exception = Assert.Throws<ReverseImpactInputException>(() =>
            ReverseImpactTraversal.Analyze([first, second], new ReverseImpactOptions(seed, 2)));

        Assert.Equal("MixedSnapshot", exception.ErrorCode);
        Assert.Contains("2 distinct", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_boundary_values_fail_with_stable_input_error_codes()
    {
        var nullFact = Assert.Throws<ReverseImpactInputException>(() =>
            ReverseImpactTraversal.Analyze(new CodeFact[] { null! }, new ReverseImpactOptions("seed", 1)));
        Assert.Equal("NullFact", nullFact.ErrorCode);

        var valid = SymbolFact("seed", Id("method", "Service.Target()"), "Service.Target()", "Method");
        var nullProperties = Assert.Throws<ReverseImpactInputException>(() =>
            ReverseImpactTraversal.Analyze([valid with { Properties = null! }], new ReverseImpactOptions("seed", 1)));
        Assert.Equal("NullFactProperties", nullProperties.ErrorCode);

        var blankCanonicalId = Assert.Throws<ReverseImpactInputException>(() =>
            ReverseImpactTraversal.Analyze(
                [valid with { Properties = new Dictionary<string, string> { ["targetSymbolId"] = " " } }],
                new ReverseImpactOptions("seed", 1)));
        Assert.Equal("InvalidCanonicalEndpoint", blankCanonicalId.ErrorCode);

        var nullFilter = Assert.Throws<ReverseImpactInputException>(() =>
            ReverseImpactTraversal.Analyze([valid], new ReverseImpactOptions("seed", 1, new string[] { null! })));
        Assert.Equal("InvalidRelationshipFilter", nullFilter.ErrorCode);

        var zeroLine = Assert.Throws<ReverseImpactInputException>(() =>
            ReverseImpactTraversal.Analyze(
                [valid with { Evidence = new EvidenceSpan("Source.cs", 0, 0, null, "extractor", "1.0.0") }],
                new ReverseImpactOptions("seed", 1)));
        Assert.Equal("InvalidEvidenceSpan", zeroLine.ErrorCode);
    }

    [Fact]
    public void Missing_relationship_evidence_becomes_a_scoped_gap_instead_of_an_impact_hop()
    {
        var seed = Id("method", "Service.Target()");
        var caller = Id("method", "Controller.Get()");
        var declaration = SymbolFact("seed", seed, "Service.Target()", "Method");
        var edge = Relationship("missing-evidence", FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, caller, "Controller.Get()", seed, "Service.Target()", "Calls", 10) with
        {
            Evidence = null!
        };

        var result = ReverseImpactTraversal.Analyze([declaration, edge], new ReverseImpactOptions(seed, 1));

        Assert.Empty(result.Impacts);
        var gap = Assert.Single(result.Gaps, candidate => candidate.GapKind == "RelationshipMissingEvidence");
        Assert.Equal(RuleIds.ReverseImpactGap, gap.RuleId);
        Assert.Equal(RuleIds.ReverseImpactGap, gap.Evidence.RuleId);
        Assert.Contains(seed, gap.RelatedSymbolIds);
    }

    [Fact]
    public void Missing_analysis_gap_evidence_remains_an_explicit_not_found_coverage_gap()
    {
        var analysisGap = Fact(
            "missing-gap-evidence",
            FactTypes.AnalysisGap,
            RuleIds.AnalyzerCapabilitySemantic,
            null,
            null,
            new Dictionary<string, string> { ["message"] = "Semantic analysis failed." },
            EvidenceTiers.Tier4Unknown,
            1) with
        {
            Evidence = null!
        };

        var result = ReverseImpactTraversal.Analyze([analysisGap], new ReverseImpactOptions("Missing.Target", 1));

        Assert.Equal("NotFound", result.Resolution);
        Assert.DoesNotContain(result.Gaps, gap => gap.GapId == "missing-gap-evidence");
        var reduced = Assert.Single(result.Gaps, gap => gap.GapKind == "AnalysisGapMissingEvidence");
        Assert.Equal(RuleIds.ReverseImpactGap, reduced.RuleId);
        Assert.Equal(RuleIds.ReverseImpactGap, reduced.Evidence.RuleId);
    }

    [Fact]
    public void Scan_engine_facts_cross_the_production_boundary_with_canonical_provenance()
    {
        using var temp = new TempDirectory();
        var project = Path.Combine(temp.Path, "src", "Fixture");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "Fixture.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(project, "Fixture.cs"), """
            namespace ReverseImpactFixture;

            public sealed class Service
            {
                public void Target() { }
            }

            public sealed class Controller
            {
                public void Get(Service service) => service.Target();
            }
            """);

        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));
        var targetCall = Assert.Single(scan.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.ContractElement == "Target"
            && fact.Properties.ContainsKey("targetSymbolId"));
        var seedId = targetCall.Properties["targetSymbolId"];

        var result = ReverseImpactTraversal.Analyze(scan.Facts, new ReverseImpactOptions(seedId, 1));
        var impact = Assert.Single(result.Impacts);
        var hop = Assert.Single(impact.Path);

        Assert.Contains("Controller.Get", impact.Symbol.DisplayName, StringComparison.Ordinal);
        Assert.Equal("Method", impact.Symbol.SymbolKind);
        Assert.Equal("Calls", hop.RelationshipKind);
        Assert.Equal(RuleIds.CSharpSemanticCallGraph, hop.RuleId);
        Assert.Equal(RuleIds.CSharpSemanticCallGraph, hop.Evidence.RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, hop.EvidenceTier);
        Assert.Equal(scan.Manifest.CommitSha, hop.CommitSha);
        Assert.Equal(ScannerVersions.CSharpSemanticExtractor, hop.Evidence.ExtractorVersion);
        Assert.False(string.IsNullOrWhiteSpace(impact.Symbol.ContainingSymbolId));

        var json = JsonSerializer.Serialize(result);
        var reloaded = JsonSerializer.Deserialize<ReverseImpactResult>(json);
        Assert.NotNull(reloaded);
        Assert.Equal(result.Snapshot, reloaded!.Snapshot);
        Assert.Equal(ReverseImpactContract.SchemaVersion, reloaded.SchemaVersion);
        Assert.Equal(result.Impacts[0].PathId, reloaded.Impacts[0].PathId);
    }

    [Fact]
    public void Type_seed_retains_one_shortest_path_per_contained_member_seed()
    {
        var type = Id("type", "Service");
        var firstMember = Id("method", "Service.First()");
        var secondMember = Id("method", "Service.Second()");
        var sharedCaller = Id("method", "Controller.Get()");
        var facts = new[]
        {
            SymbolFact("first-member", firstMember, "Service.First()", "Method", type),
            SymbolFact("second-member", secondMember, "Service.Second()", "Method", type),
            Relationship("first-call", FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, sharedCaller, "Controller.Get()", firstMember, "Service.First()", "Calls", 10),
            Relationship("second-call", FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph, sharedCaller, "Controller.Get()", secondMember, "Service.Second()", "Calls", 11)
        };

        var result = ReverseImpactTraversal.Analyze(facts, new ReverseImpactOptions(type, 1));

        Assert.Equal(2, result.Impacts.Count);
        Assert.All(result.Impacts, impact => Assert.Equal(sharedCaller, impact.Symbol.SymbolId));
        Assert.Equal([firstMember, secondMember], result.Impacts.Select(impact => impact.TraversalSeedSymbolId).Order(StringComparer.Ordinal));
        Assert.Equal(2, result.Impacts.Select(impact => impact.PathId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Machine_contract_versions_and_allowlists_are_stable()
    {
        Assert.Equal("tracemap.reverse-impact.v1", ReverseImpactContract.SchemaVersion);
        Assert.True(ReverseImpactContract.IsSupportedSchema("tracemap.reverse-impact.v1"));
        Assert.False(ReverseImpactContract.IsSupportedSchema("tracemap.reverse-impact.v2"));
        Assert.Equal(["Resolved", "NotFound", "Ambiguous"], ReverseImpactContract.SupportedResolutions);
        Assert.Contains(ReverseImpactGapKinds.AnalysisGap, ReverseImpactContract.SupportedGapKinds);
        Assert.Contains(ReverseImpactGapKinds.RelationshipMissingCanonicalIdentity, ReverseImpactContract.SupportedGapKinds);

        var json = JsonSerializer.Serialize(ReverseImpactTraversal.Analyze([], new ReverseImpactOptions("missing", 1)));
        Assert.Contains("\"SchemaVersion\":\"tracemap.reverse-impact.v1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Resolution\":\"NotFound\"", json, StringComparison.Ordinal);
        Assert.Contains("\"GapKind\":\"SelectorNotFound\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Rule_catalog_guards_reverse_impact_contracts()
    {
        Assert.Equal("impact.reverse.traversal.v1", RuleIds.ReverseImpactTraversal);
        Assert.Equal("impact.reverse.gap.v1", RuleIds.ReverseImpactGap);
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        var traversal = RuleBlock(catalog, RuleIds.ReverseImpactTraversal);
        var gap = RuleBlock(catalog, RuleIds.ReverseImpactGap);

        Assert.Contains("evidenceTier: Tier2Structural", traversal, StringComparison.Ordinal);
        Assert.Contains("ReverseImpactResult", traversal, StringComparison.Ordinal);
        Assert.Contains("ReverseImpactContract", traversal, StringComparison.Ordinal);
        Assert.Contains("ReverseImpactSnapshot", traversal, StringComparison.Ordinal);
        Assert.Contains("ReverseImpactItem", traversal, StringComparison.Ordinal);
        Assert.Contains("ReverseImpactHop", traversal, StringComparison.Ordinal);
        Assert.Contains("ReverseImpactEvidence", traversal, StringComparison.Ordinal);
        Assert.Contains("exactly one scan, repository, and commit snapshot", traversal, StringComparison.Ordinal);
        Assert.Contains("evidenceTier: Tier4Unknown", gap, StringComparison.Ordinal);
        Assert.Contains("ReverseImpactGap", gap, StringComparison.Ordinal);
        Assert.Contains("incomplete evidence, not proof", gap, StringComparison.Ordinal);
    }

    private static CodeFact Relationship(
        string factId,
        string factType,
        string ruleId,
        string sourceId,
        string sourceDisplay,
        string targetId,
        string targetDisplay,
        string relationshipKind,
        int line)
    {
        var properties = new Dictionary<string, string>
        {
            ["sourceSymbolId"] = sourceId,
            ["sourceSymbolDisplayName"] = sourceDisplay,
            ["sourceSymbolKind"] = "Method",
            ["sourceSymbolLanguage"] = "csharp",
            ["targetSymbolId"] = targetId,
            ["targetSymbolDisplayName"] = targetDisplay,
            ["targetSymbolKind"] = targetId.Contains("property", StringComparison.Ordinal) ? "Property" : "Method",
            ["targetSymbolLanguage"] = "csharp"
        };
        if (factType == FactTypes.SymbolRelationship)
        {
            properties["relationshipKind"] = relationshipKind;
        }

        return Fact(factId, factType, ruleId, sourceDisplay, targetDisplay, properties, EvidenceTiers.Tier1Semantic, line);
    }

    private static CodeFact SymbolFact(
        string factId,
        string symbolId,
        string display,
        string kind,
        string? containingId = null,
        string? assembly = null,
        string file = "Source.cs")
    {
        var properties = new Dictionary<string, string>
        {
            ["targetSymbolId"] = symbolId,
            ["targetSymbolDisplayName"] = display,
            ["targetSymbolKind"] = kind,
            ["targetSymbolLanguage"] = "csharp",
            ["targetSymbolAssemblyName"] = assembly ?? "Tests"
        };
        if (containingId is not null)
        {
            properties["targetContainingSymbolId"] = containingId;
        }

        return Fact(factId, kind == "NamedType" ? FactTypes.TypeDeclared : FactTypes.MethodDeclared, RuleIds.CSharpSemanticDeclarations, null, display, properties, EvidenceTiers.Tier1Semantic, 1, file);
    }

    private static CodeFact Fact(
        string factId,
        string factType,
        string ruleId,
        string? source,
        string? target,
        IReadOnlyDictionary<string, string> properties,
        string tier,
        int line,
        string file = "Source.cs") => new(
            factId,
            "scan",
            "repo",
            "0123456789012345678901234567890123456789",
            "Tests.csproj",
            factType,
            ruleId,
            tier,
            source,
            target,
            null,
            new EvidenceSpan(file, line, line, "hash", "csharp-semantic", "1.2.3"),
            properties);

    private static string RuleBlock(string catalog, string ruleId)
    {
        var start = Regex.Match(catalog, $@"(?m)^\s*-\s*id:\s*{Regex.Escape(ruleId)}\s*$");
        Assert.True(start.Success, $"Missing rule catalog entry for {ruleId}.");
        var afterStart = start.Index + start.Length;
        var next = Regex.Match(catalog[afterStart..], @"(?m)^\s*-\s*id:\s*\S+\s*$");
        return next.Success
            ? catalog[start.Index..(afterStart + next.Index)]
            : catalog[start.Index..];
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string Id(string kind, string display) => $"csharp {kind} Tests@1.0.0.0 {Uri.EscapeDataString(display)}";
}
