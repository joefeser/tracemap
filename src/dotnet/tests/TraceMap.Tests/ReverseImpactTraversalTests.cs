using System.Text.Json;
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
    public void Analysis_gaps_are_propagated_and_syntax_relationships_do_not_name_match()
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
            "analysis-gap",
            FactTypes.AnalysisGap,
            RuleIds.AnalyzerCapabilitySemantic,
            null,
            null,
            new Dictionary<string, string> { ["message"] = "Project load failed." },
            EvidenceTiers.Tier4Unknown,
            1);

        var result = ReverseImpactTraversal.Analyze([syntaxCall, semantic, gap], new ReverseImpactOptions(seed, 1));

        Assert.Empty(result.Impacts);
        Assert.Contains(result.Gaps, candidate => candidate.GapKind == "RelationshipMissingCanonicalIdentity" && candidate.Message.Contains("syntax-call", StringComparison.Ordinal));
        var propagated = Assert.Single(result.Gaps, candidate => candidate.GapKind == "AnalysisGap");
        Assert.Equal("analysis-gap", propagated.GapId);
        Assert.Equal(RuleIds.AnalyzerCapabilitySemantic, propagated.RuleId);
        Assert.Equal("Project load failed.", propagated.Message);
    }

    [Fact]
    public void Not_found_selector_and_unknown_filter_fail_closed()
    {
        var result = ReverseImpactTraversal.Analyze([], new ReverseImpactOptions("Missing", 1));
        Assert.Equal("NotFound", result.Resolution);
        Assert.Empty(result.Impacts);
        Assert.Contains(result.Gaps, gap => gap.GapKind == "SelectorNotFound");

        Assert.Throws<ArgumentException>(() => ReverseImpactTraversal.Analyze([], new ReverseImpactOptions("Missing", 1, ["everything"])));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReverseImpactTraversal.Analyze([], new ReverseImpactOptions("Missing", 0)));
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

    private static string Id(string kind, string display) => $"csharp {kind} Tests@1.0.0.0 {Uri.EscapeDataString(display)}";
}
