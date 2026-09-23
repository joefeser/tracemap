using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

/// <summary>
/// Messy .NET workspace regression slice (Task 10 third slice). Synthetic,
/// public-safe fixtures under <c>samples/messy-dotnet-workspace</c> reproduce
/// the workspace shapes observed in real Web Forms/.NET scans. The stable
/// case catalog is <c>samples/messy-dotnet-workspace/case-catalog.json</c>;
/// every assertion here names its catalog case id and pipeline stage
/// (extraction, combining, reconciliation, traversal) so a failure identifies
/// both. Deferred cases and their exact blockers live in the catalog.
/// </summary>
public sealed class MessyWorkspaceRegressionTests
{
    private const string CatalogPath = "samples/messy-dotnet-workspace/case-catalog.json";
    private const string DeepHandler = "DeepButton_Click";
    private const string LoopHandler = "LoopButton_Click";
    private const string EnginesHandler = "EnginesButton_Click";
    private const string SelfHandler = "SelfButton_Click";
    private const string AmbiguityHandler = "AmbiguityButton_Click";
    private const string GeneratedHandler = "GeneratedButton_Click";
    private const string CrossLanguageHandler = "CrossLanguageButton_Click";
    private const string CrossLanguageSourcePath = "csharp/Pages/CrossLanguage.aspx.cs";
    private const int CrossLanguageInvocationLine = 7;
    private const string VbHandler = "SubmitButton_Click";

    [Fact]
    public void Case_catalog_records_stable_ids_and_deferred_blockers()
    {
        var catalogPath = Path.Combine(FindRepoRoot(), CatalogPath);
        Require("MW-CATALOG", "extraction", File.Exists(catalogPath), $"case catalog missing at {catalogPath}");
        using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var root = catalog.RootElement;
        Require("MW-CATALOG", "extraction",
            root.GetProperty("schemaVersion").GetString() == "messy-workspace-case-catalog.v1",
            "schemaVersion must stay stable at messy-workspace-case-catalog.v1");
        Require("MW-CATALOG", "extraction", root.GetProperty("nonClaims").GetArrayLength() > 0,
            "catalog-level non-claims are required");

        var allowedStages = new HashSet<string>(["extraction", "combining", "reconciliation", "traversal"], StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var implemented = 0;
        var deferred = 0;
        foreach (var entry in root.GetProperty("cases").EnumerateArray())
        {
            var id = entry.GetProperty("id").GetString()!;
            Require("MW-CATALOG", "extraction", ids.Add(id), $"duplicate case id {id}");
            Require("MW-CATALOG", "extraction", id.StartsWith("MW-", StringComparison.Ordinal), $"case id {id} must use the MW- prefix");
            var status = entry.GetProperty("status").GetString()!;
            Require("MW-CATALOG", "extraction", status is "implemented" or "deferred", $"case {id} has invalid status {status}");
            if (status == "implemented")
            {
                implemented++;
                Require("MW-CATALOG", "extraction", entry.TryGetProperty("assertions", out var assertions) && assertions.GetString() is { Length: > 0 },
                    $"implemented case {id} must record its assertions");
                var stages = entry.GetProperty("stages").EnumerateArray().Select(stage => stage.GetString()!).ToArray();
                Require("MW-CATALOG", "extraction", stages.Length > 0, $"implemented case {id} must name its stages");
                Require("MW-CATALOG", "extraction", stages.All(stage => allowedStages.Contains(stage)),
                    $"case {id} declares a stage outside the extraction/combining/reconciliation/traversal contract");
            }
            else
            {
                deferred++;
                Require("MW-CATALOG", "extraction",
                    entry.TryGetProperty("blocker", out var blocker) && blocker.GetString() is { Length: > 0 },
                    $"deferred case {id} must record its exact blocker");
            }

            Require("MW-CATALOG", "extraction", entry.GetProperty("shape").GetString() is { Length: > 0 }, $"case {id} must describe its shape");
        }

        Require("MW-CATALOG", "extraction", ids.Count == 12,
            $"expected the twelve pinned fixture cases, found {ids.Count}");
        Require("MW-CATALOG", "extraction", implemented == 12 && deferred == 0,
            $"all twelve pinned fixture cases must remain implemented; found {implemented} implemented and {deferred} deferred");

        // Catalog evidence annotations are load-bearing: every expected rule id must
        // exist in the rule catalog, tiers must be real evidence tiers, and gap
        // entries must use the documented Kind[:reason] vocabulary.
        var catalogRules = new HashSet<string>(
            System.Text.RegularExpressions.Regex.Matches(
                File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml")),
                @"- id:\s*(\S+)").Select(match => match.Groups[1].Value),
            StringComparer.Ordinal);
        var validTiers = new HashSet<string>([EvidenceTiers.Tier1Semantic, EvidenceTiers.Tier2Structural, EvidenceTiers.Tier3SyntaxOrTextual, EvidenceTiers.Tier4Unknown], StringComparer.Ordinal);
        var validTruncationReasons = new HashSet<string>(["depth", "frontier", "path", "cycle", "work"], StringComparer.Ordinal);
        foreach (var entry in root.GetProperty("cases").EnumerateArray())
        {
            var id = entry.GetProperty("id").GetString()!;
            var evidenceContracts = entry.TryGetProperty("alternativeEvidence", out var alternatives)
                ? new[] { entry }.Concat(alternatives.EnumerateArray()).ToArray()
                : [entry];
            foreach (var evidenceContract in evidenceContracts)
            {
                if (evidenceContract.TryGetProperty("expectedRuleIds", out var ruleIds))
                {
                    foreach (var rule in ruleIds.EnumerateArray())
                    {
                        Require("MW-CATALOG", "extraction", catalogRules.Contains(rule.GetString()!),
                            $"case {id} expects rule {rule.GetString()} which is not in rules/rule-catalog.yml");
                    }
                }

                if (evidenceContract.TryGetProperty("expectedTiers", out var tiers))
                {
                    foreach (var tier in tiers.EnumerateArray())
                    {
                        Require("MW-CATALOG", "extraction", validTiers.Contains(tier.GetString()!),
                            $"case {id} expects tier {tier.GetString()} which is not a valid evidence tier");
                    }
                }

                if (evidenceContract.TryGetProperty("expectedGaps", out var gaps))
                {
                    foreach (var gap in gaps.EnumerateArray())
                    {
                        var value = gap.GetString()!;
                        var separator = value.IndexOf(':', StringComparison.Ordinal);
                        var kind = separator < 0 ? value : value[..separator];
                        var reason = separator < 0 ? null : value[(separator + 1)..];
                        Require("MW-CATALOG", "extraction", kind.Length > 0, $"case {id} has an empty gap kind");
                        Require("MW-CATALOG", "extraction",
                            kind != "TruncatedByLimit" || (reason is not null && validTruncationReasons.Contains(reason)),
                            $"case {id} gap {value} must carry a documented truncation reason");
                    }
                }
            }
        }
    }

    [Fact]
    public void Folder_spread_extraction_covers_nested_folders_and_roots()
    {
        using var temp = new TempDirectory();
        var (alpha, alphaIndex) = ScanRoot(temp, "root-alpha", "alpha-site");
        var (vb, vbIndex) = ScanRoot(temp, "vb-projectless", "vb-site");
        Assert.True(File.Exists(alphaIndex) && File.Exists(vbIndex));

        // MW-FOLDER-SPREAD-001 [extraction]: facts span the nested folders.
        var alphaPaths = alpha.Facts.Select(fact => fact.Evidence.FilePath).ToHashSet(StringComparer.Ordinal);
        Require("MW-FOLDER-SPREAD-001", "extraction", alphaPaths.Contains("Pages/Engines.aspx.cs"), "page code-behind was not inventoried");
        Require("MW-FOLDER-SPREAD-001", "extraction", alphaPaths.Contains("Services/TenEngines.cs"), "Services folder was not inventoried");
        Require("MW-FOLDER-SPREAD-001", "extraction", alphaPaths.Contains("Data/DeepQueries.cs"), "Data folder was not inventoried");
        var vbPaths = vb.Facts.Select(fact => fact.Evidence.FilePath).ToHashSet(StringComparer.Ordinal);
        Require("MW-FOLDER-SPREAD-001", "extraction", vbPaths.Contains("Default.aspx.vb"), "VB page code file was not inventoried");
        Require("MW-FOLDER-SPREAD-001", "extraction", vbPaths.Contains("App_Code/Queue.vb"), "VB App_Code folder was not inventoried");

        // The deep chain crosses the Services/ to Data/ folder boundary.
        var crossFolderEdge = alpha.Facts.SingleOrDefault(fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol == "global::Alpha.Data.DeepQueries.FinalStep()");
        Require("MW-FOLDER-SPREAD-001", "extraction", crossFolderEdge is not null,
            "no semantic call edge reaches Data/DeepQueries.cs from Services/");
        Require("MW-FOLDER-SPREAD-001", "extraction",
            crossFolderEdge!.SourceSymbol == "global::Alpha.Services.DeepChain.Step10()",
            $"unexpected cross-folder edge source {crossFolderEdge.SourceSymbol}");
        Require("MW-FOLDER-SPREAD-001", "extraction", crossFolderEdge.Evidence.FilePath == "Services/DeepChain.cs",
            "cross-folder edge must carry its call-site file path");
        var commitSha = alpha.Manifest.CommitSha;
        Require("MW-FOLDER-SPREAD-001", "extraction",
            commitSha.Length is 40 or 64 && commitSha.All(char.IsAsciiHexDigit),
            $"in-repo roots must record a full SHA-1 or SHA-256 commit SHA, found {commitSha}");
        RequireCatalogEvidence("MW-FOLDER-SPREAD-001", "extraction",
            alpha.Facts.Select(fact => fact.RuleId), alpha.Facts.Select(fact => fact.EvidenceTier), []);
    }

    [Fact]
    public async Task Deep_chain_terminal_survives_beyond_depth_10_without_false_absence()
    {
        using var temp = new TempDirectory();
        var (_, alphaIndex) = ScanRoot(temp, "root-alpha", "alpha-site");

        // MW-DEEP-CHAIN-D10-001 [extraction]: exact identities of the chain and terminal.
        using (var connection = new SqliteConnection($"Data Source={alphaIndex}"))
        {
            connection.Open();
            var hops = new (string Source, string Target)[]
            {
                ("global::Alpha.Pages.EnginesPage.DeepButton_Click(object sender, global::System.EventArgs e)", "global::Alpha.Services.DeepChain.Run()"),
                ("global::Alpha.Services.DeepChain.Run()", "global::Alpha.Services.DeepChain.Step01()"),
                ("global::Alpha.Services.DeepChain.Step10()", "global::Alpha.Data.DeepQueries.FinalStep()"),
            };
            foreach (var (source, target) in hops)
            {
                var count = (long)ExecuteScalar(connection,
                    "SELECT COUNT(*) FROM call_edges WHERE caller_symbol = $source AND callee_symbol = $target AND evidence_tier = 'Tier1Semantic'",
                    ("$source", source), ("$target", target))!;
                Require("MW-DEEP-CHAIN-D10-001", "extraction", count >= 1, $"missing Tier1 call edge {source} -> {target}");
            }

            var terminal = (string?)ExecuteScalar(connection,
                "SELECT source_symbol FROM facts WHERE fact_type = 'DatabaseOperationCandidate' AND properties_json LIKE '%deep_orders%' LIMIT 1");
            Require("MW-DEEP-CHAIN-D10-001", "extraction", terminal == "global::Alpha.Data.DeepQueries.FinalStep()",
                $"deep terminal must attach to the chain identity, found {terminal}");
        }

        // MW-DEEP-CHAIN-D10-001 [traversal]: twelve call edges put the terminal at
        // graph distance 14; enumeration truncates at depth 12 but the terminal
        // inventory stays complete with no false absence.
        var depth12 = await WebFormsModernizationPacketReporter.BuildAsync(new(alphaIndex, Path.Combine(temp.Path, "depth12"), MaxDepth: 12));
        var depth16 = await WebFormsModernizationPacketReporter.BuildAsync(new(alphaIndex, Path.Combine(temp.Path, "depth16"), MaxDepth: 16));
        var depth10 = await WebFormsModernizationPacketReporter.BuildAsync(new(alphaIndex, Path.Combine(temp.Path, "depth10"), MaxDepth: 10));

        var deep12 = depth12.EventChains.Where(chain => chain.HandlerSymbol?.Contains(DeepHandler, StringComparison.Ordinal) == true).ToArray();
        var deep16 = depth16.EventChains.Where(chain => chain.HandlerSymbol?.Contains(DeepHandler, StringComparison.Ordinal) == true).ToArray();
        var deep10 = depth10.EventChains.Where(chain => chain.HandlerSymbol?.Contains(DeepHandler, StringComparison.Ordinal) == true).ToArray();
        Require("MW-DEEP-CHAIN-D10-001", "traversal", deep12.Length == 1 && deep16.Length == 1 && deep10.Length == 1,
            $"expected exactly one deep handler chain per packet, found {deep12.Length}/{deep16.Length}/{deep10.Length}");

        var observation12 = deep12[0].TraversalObservation;
        Require("MW-DEEP-CHAIN-D10-001", "traversal", observation12 is not null, "deep chain has no traversal observation");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", deep12[0].TerminalKind == "sql-query", $"deep terminal kind was {deep12[0].TerminalKind}");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", observation12!.TerminalReachabilityComplete, "terminal inventory must be complete at depth 12");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", observation12.DistinctReachableTerminalCount == 1,
            $"deep chain must inventory its single terminal, found {observation12.DistinctReachableTerminalCount}");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", observation12.MinimumTerminalDistance == 14,
            $"terminal sits at graph distance 14 from the handler, found {observation12.MinimumTerminalDistance}");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", observation12.MinimumTerminalDistance > 12,
            "terminal must sit beyond the configured depth for this case");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", observation12.PathEnumerationTruncated, "enumeration must honestly truncate before distance 14");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", observation12.PathEnumerationTruncationReasons.Contains("depth"),
            "truncation must record the depth reason");
        Require("MW-DEEP-CHAIN-D10-001", "traversal",
            observation12.StopState == "supported-terminal-reached",
            $"deep chain stop state was {observation12.StopState}");

        var boundaries12 = TerminalBoundaries(depth12, DeepHandler);
        var boundaries16 = TerminalBoundaries(depth16, DeepHandler);
        Require("MW-DEEP-CHAIN-D10-001", "traversal", boundaries12.Count == 1, "deep handler must own one terminal boundary at depth 12");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", boundaries16.Count == 1, "deep handler must own one terminal boundary at depth 16");
        Require("MW-DEEP-CHAIN-D10-001", "traversal",
            boundaries12.Select(boundary => boundary.TerminalEvidenceId).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(boundaries16.Select(boundary => boundary.TerminalEvidenceId).OrderBy(id => id, StringComparer.Ordinal)),
            "terminal boundary identity must not change between depth 12 and depth 16");
        var deepBoundary = boundaries12[0];
        RequireCatalogEvidence("MW-DEEP-CHAIN-D10-001", "traversal",
            deepBoundary.RuleIds.Concat(depth12.Gaps.Select(gap => gap.RuleId)),
            deepBoundary.EvidenceTiers.Concat(depth12.Gaps.Select(gap => gap.EvidenceTier)),
            depth12.Gaps.Select(gap => gap.TruncationReason is null ? gap.Classification : $"{gap.Classification}:{gap.TruncationReason}"));
        Require("MW-DEEP-CHAIN-D10-001", "traversal", deepBoundary.BoundaryKind == "sql-query", $"deep boundary kind was {deepBoundary.BoundaryKind}");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", deepBoundary.BoundaryCategory == "database", $"deep boundary category was {deepBoundary.BoundaryCategory}");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", deepBoundary.RuleIds.Contains(RuleIds.DatabaseOperationCallPattern),
            "deep boundary must cite the database call-pattern rule");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", deepBoundary.EvidenceTiers.Contains(EvidenceTiers.Tier1Semantic),
            "deep boundary must retain Tier1 evidence");
        Require("MW-DEEP-CHAIN-D10-001", "traversal",
            !depth12.Gaps.Any(gap => gap.Classification is "DownstreamWithoutSupportedTerminal" or "NoBackendEvidence"
                && gap.ScopeKind == "event-chain" && gap.ScopeId == deep12[0].BindingFactId),
            "the deep handler must not be reported as terminal-less while its terminal is inventoried");

        // Documented retained-closure boundary: at depth 10 the distance-14
        // terminal falls outside the depth-bounded retained closure, so the
        // observation must scope its completeness claim rather than invent or
        // imply a terminal.
        var observation10 = deep10[0].TraversalObservation!;
        Require("MW-DEEP-CHAIN-D10-001", "traversal", observation10.DistinctReachableTerminalCount == 0,
            "the distance-14 terminal must not be claimed inside the depth-10 retained closure");
        Require("MW-DEEP-CHAIN-D10-001", "traversal", observation10.TerminalReachabilityComplete,
            "depth-10 observation must stay complete for the retained graph");
        Require("MW-DEEP-CHAIN-D10-001", "traversal",
            observation10.Limitations.Any(limitation => limitation.Contains("retained graph", StringComparison.Ordinal)),
            "depth-10 observation must scope completeness to the retained graph");
    }

    [Fact]
    public async Task Cycles_terminate_and_report_truncation_without_invented_terminals()
    {
        using var temp = new TempDirectory();
        var (alpha, alphaIndex) = ScanRoot(temp, "root-alpha", "alpha-site");

        // MW-CYCLE-001 [extraction]: the three-node cycle and the self-cycle edges.
        var cycleEdges = alpha.Facts.Where(fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.SourceSymbol!.StartsWith("global::Alpha.Services.Loop.", StringComparison.Ordinal)
            && fact.TargetSymbol!.StartsWith("global::Alpha.Services.Loop.", StringComparison.Ordinal)).ToArray();
        foreach (var (source, target) in new[]
        {
            ("global::Alpha.Services.Loop.Enter()", "global::Alpha.Services.Loop.First()"),
            ("global::Alpha.Services.Loop.First()", "global::Alpha.Services.Loop.Second()"),
            ("global::Alpha.Services.Loop.Second()", "global::Alpha.Services.Loop.Third()"),
            ("global::Alpha.Services.Loop.Third()", "global::Alpha.Services.Loop.First()"),
        })
        {
            Require("MW-CYCLE-001", "extraction", cycleEdges.Any(edge => edge.SourceSymbol == source && edge.TargetSymbol == target),
                $"missing cycle edge {source} -> {target}");
        }

        // MW-CYCLE-SELF-002 [extraction]: the self-recursive edge.
        Require("MW-CYCLE-SELF-002", "extraction",
            cycleEdges.Any(edge => edge.SourceSymbol == "global::Alpha.Services.Loop.Self()" && edge.TargetSymbol == "global::Alpha.Services.Loop.Self()"),
            "missing self-recursive edge Self() -> Self()");

        // MW-CYCLE-001 [traversal]: traversal terminates, records cycle truncation
        // honestly, and never invents a terminal for the cyclic branch.
        var first = await WebFormsModernizationPacketReporter.BuildAsync(new(alphaIndex, Path.Combine(temp.Path, "cycle-a")));
        var second = await WebFormsModernizationPacketReporter.BuildAsync(new(alphaIndex, Path.Combine(temp.Path, "cycle-b")));
        RequireCatalogEvidence("MW-CYCLE-001", "traversal",
            cycleEdges.Select(fact => fact.RuleId).Concat(first.Gaps.Select(gap => gap.RuleId)),
            cycleEdges.Select(fact => fact.EvidenceTier).Concat(first.Gaps.Select(gap => gap.EvidenceTier)),
            first.Gaps.Select(gap => gap.TruncationReason is null ? gap.Classification : $"{gap.Classification}:{gap.TruncationReason}"));
        RequireCatalogEvidence("MW-CYCLE-SELF-002", "traversal",
            cycleEdges.Where(fact => fact.SourceSymbol == "global::Alpha.Services.Loop.Self()").Select(fact => fact.RuleId),
            cycleEdges.Where(fact => fact.SourceSymbol == "global::Alpha.Services.Loop.Self()").Select(fact => fact.EvidenceTier),
            first.Gaps.Select(gap => gap.Classification));
        Require("MW-CYCLE-001", "traversal",
            JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second),
            "cycle traversal must be deterministic across repeated packets");

        foreach (var (caseId, handlers) in new (string CaseId, string[] Handlers)[] { ("MW-CYCLE-001", [LoopHandler]), ("MW-CYCLE-SELF-002", [SelfHandler]) })
        {
            var chains = first.EventChains.Where(chain => handlers.Any(handler => chain.HandlerSymbol?.Contains(handler, StringComparison.Ordinal) == true)).ToArray();
            Require(caseId, "traversal", chains.Length == 1, $"expected one {handlers[0]} handler chain, found {chains.Length}");
            var observation = chains[0].TraversalObservation;
            Require(caseId, "traversal", observation is not null, $"{handlers[0]} chain has no traversal observation");
            Require(caseId, "traversal", observation!.PathEnumerationTruncationReasons.Contains("cycle"),
                $"cycle truncation must be recorded for {handlers[0]}, found [{string.Join(",", observation.PathEnumerationTruncationReasons)}]");
            Require(caseId, "traversal", observation.DistinctReachableTerminalCount == 0,
                $"the {handlers[0]} branch has no terminal and none may be invented");
            Require(caseId, "traversal", observation.TerminalReachabilityComplete,
                $"{handlers[0]} traversal must terminate and complete its inventory");
            Require(caseId, "traversal",
                observation.StopState == "observed-downstream-without-supported-terminal",
                $"{handlers[0]} chain stop state was {observation.StopState}");
            Require(caseId, "traversal", chains[0].TerminalKind is null, $"{handlers[0]} chain must not claim a terminal kind");
            Require(caseId, "traversal",
                first.Gaps.Any(gap => gap.Classification == "DownstreamWithoutSupportedTerminal"
                    && gap.ScopeKind == "event-chain" && gap.ScopeId == chains[0].BindingFactId),
                $"the terminal-less {handlers[0]} branch must surface an explicit gap");
        }
    }

    [Fact]
    public async Task Same_name_members_in_ten_classes_never_cross_join()
    {
        using var temp = new TempDirectory();
        var (alpha, alphaIndex) = ScanRoot(temp, "root-alpha", "alpha-site");

        // MW-SAME-NAME-TEN-001 [extraction]: ten container-distinct Process and Core
        // identities; every Core call stays inside its own engine.
        var processEdges = alpha.Facts.Where(fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol!.StartsWith("global::Alpha.Services.Engine", StringComparison.Ordinal)
            && fact.TargetSymbol.EndsWith(".Process()", StringComparison.Ordinal)).ToArray();
        Require("MW-SAME-NAME-TEN-001", "extraction", processEdges.Length == 10,
            $"expected ten Process call edges, found {processEdges.Length}");
        Require("MW-SAME-NAME-TEN-001", "extraction",
            processEdges.Select(edge => edge.TargetSymbol).Distinct(StringComparer.Ordinal).Count() == 10,
            "the ten Process targets must stay container-distinct");

        for (var engine = 1; engine <= 10; engine++)
        {
            var number = engine.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            var core = alpha.Facts.SingleOrDefault(fact =>
                fact.FactType == FactTypes.CallEdge
                && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
                && fact.SourceSymbol == $"global::Alpha.Services.Engine{number}.Process()"
                && fact.TargetSymbol == $"global::Alpha.Services.Engine{number}.Core()");
            Require("MW-SAME-NAME-TEN-001", "extraction", core is not null,
                $"Engine{number}.Process must call its own Engine{number}.Core");
            Require("MW-SAME-NAME-TEN-001", "extraction",
                core!.Evidence.FilePath == "Services/TenEngines.cs",
                $"Engine{number} Core edge must stay in TenEngines.cs");

            var terminal = alpha.Facts.SingleOrDefault(fact =>
                fact.FactType == FactTypes.DatabaseOperationCandidate
                && fact.SourceSymbol == $"global::Alpha.Services.Engine{number}.Core()");
            Require("MW-SAME-NAME-TEN-001", "extraction", terminal is not null,
                $"Engine{number}.Core must own its database terminal");
            Require("MW-SAME-NAME-TEN-001", "extraction",
                terminal!.Properties.GetValueOrDefault("tableName") == $"engine_{number}_queue",
                $"Engine{number} terminal table was {terminal.Properties.GetValueOrDefault("tableName")}");
            Require("MW-SAME-NAME-TEN-001", "extraction", terminal.RuleId == RuleIds.DatabaseOperationCallPattern,
                $"Engine{number} terminal rule was {terminal.RuleId}");
            Require("MW-SAME-NAME-TEN-001", "extraction", terminal.EvidenceTier == EvidenceTiers.Tier1Semantic,
                $"Engine{number} terminal tier was {terminal.EvidenceTier}");
        }

        // MW-SAME-NAME-TEN-001 [extraction]: no semantic edge may cross engines at
        // all; an erroneous EngineX.Process -> EngineY.Core edge must fail here.
        var engineInternalEdges = alpha.Facts.Where(fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.SourceSymbol!.StartsWith("global::Alpha.Services.Engine", StringComparison.Ordinal)
            && fact.TargetSymbol!.StartsWith("global::Alpha.Services.Engine", StringComparison.Ordinal)).ToArray();
        Require("MW-SAME-NAME-TEN-001", "extraction", engineInternalEdges.Length == 10,
            $"expected exactly ten engine-internal semantic edges, found {engineInternalEdges.Length}");
        Require("MW-SAME-NAME-TEN-001", "extraction",
            engineInternalEdges.All(edge =>
                EngineNumber(edge.SourceSymbol) is { } source && EngineNumber(edge.TargetSymbol) is { } target && source == target),
            $"a semantic edge crossed from one engine into another engine's member: [{string.Join(",", engineInternalEdges.Where(edge => !(EngineNumber(edge.SourceSymbol) is { } s && EngineNumber(edge.TargetSymbol) is { } t && s == t)).Select(edge => $"{edge.SourceSymbol}->{edge.TargetSymbol}"))}]");

        // MW-SAME-NAME-TEN-001 [reconciliation]: each engine's SQL literal keeps its
        // own shape identity; distinct hashes and tables prove no shared/fuzzy identity.
        var engineShapes = alpha.Facts
            .Where(fact => fact.FactType == FactTypes.QueryPatternDetected
                && fact.Properties.GetValueOrDefault("tableName")?.StartsWith("engine_", StringComparison.Ordinal) == true)
            .ToArray();
        Require("MW-SAME-NAME-TEN-001", "reconciliation", engineShapes.Length == 10, $"expected ten engine query shapes, found {engineShapes.Length}");
        Require("MW-SAME-NAME-TEN-001", "reconciliation",
            engineShapes.Select(fact => fact.Properties.GetValueOrDefault("queryShapeHash")).Distinct(StringComparer.Ordinal).Count() == 10,
            "each engine's SQL literal must keep a distinct query shape hash");
        Require("MW-SAME-NAME-TEN-001", "reconciliation",
            engineShapes.Select(fact => fact.Properties.GetValueOrDefault("tableName")).Distinct(StringComparer.Ordinal).Count() == 10,
            "each engine's terminal table must stay distinct");

        RequireCatalogEvidence("MW-SAME-NAME-TEN-001", "reconciliation",
            engineInternalEdges.Concat(engineShapes).Select(fact => fact.RuleId),
            engineInternalEdges.Concat(engineShapes).Select(fact => fact.EvidenceTier), []);

        // MW-SAME-NAME-TEN-001 [traversal]: the handler inventories ten distinct
        // terminal witnesses and no engine chain touches another engine's evidence.
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(alphaIndex, Path.Combine(temp.Path, "packet")));
        var engineBoundaries = TerminalBoundaries(packet, EnginesHandler);
        Require("MW-SAME-NAME-TEN-001", "traversal", engineBoundaries.Count == 10,
            $"the engines handler must inventory ten distinct terminal boundaries, found {engineBoundaries.Count}");
        Require("MW-SAME-NAME-TEN-001", "traversal",
            engineBoundaries.Select(boundary => boundary.TerminalEvidenceId).Distinct(StringComparer.Ordinal).Count() == 10,
            "engine terminal identities must not collapse");
        var factsById = alpha.Facts.ToDictionary(fact => fact.FactId, fact => fact);
        var engineLineRanges = EngineClassLineRanges(Path.Combine(MessyRoot("root-alpha"), "Services", "TenEngines.cs"));
        foreach (var boundary in engineBoundaries)
        {
            Require("MW-SAME-NAME-TEN-001", "traversal", boundary.BoundaryKind == "sql-query",
                $"engine boundary kind was {boundary.BoundaryKind}");
            Require("MW-SAME-NAME-TEN-001", "traversal", boundary.RuleIds.Contains(RuleIds.DatabaseOperationCallPattern),
                "engine boundary must cite the database call-pattern rule");

            var supportingFacts = boundary.SupportingFactIds
                .Select(NormalizeFactId)
                .Where(factsById.ContainsKey)
                .Select(id => factsById[id])
                .ToArray();
            var terminalFacts = supportingFacts.Where(fact => fact.FactType == FactTypes.DatabaseOperationCandidate).ToArray();
            Require("MW-SAME-NAME-TEN-001", "traversal", terminalFacts.Length == 1,
                $"engine boundary must support exactly one database terminal, found {terminalFacts.Length}");
            var engineNumber = terminalFacts[0].Properties.GetValueOrDefault("tableName")?.Replace("engine_", string.Empty, StringComparison.Ordinal).Replace("_queue", string.Empty, StringComparison.Ordinal);
            Require("MW-SAME-NAME-TEN-001", "traversal", engineNumber is { Length: 2 },
                $"engine boundary terminal table was {terminalFacts[0].Properties.GetValueOrDefault("tableName")}");
            Require("MW-SAME-NAME-TEN-001", "traversal",
                NormalizeFactId(boundary.TerminalEvidenceId) == terminalFacts[0].FactId,
                $"boundary terminal evidence must be its own engine's terminal fact for engine {engineNumber}");
            Require("MW-SAME-NAME-TEN-001", "traversal",
                !supportingFacts.Any(fact => fact.FactType == FactTypes.DatabaseOperationCandidate
                    && fact.FactId != terminalFacts[0].FactId),
                $"engine {engineNumber} boundary supports another engine's terminal");
            var foreignEdges = supportingFacts.Where(fact =>
                fact.FactType == FactTypes.CallEdge
                && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
                && (EngineNumber(fact.SourceSymbol ?? string.Empty), EngineNumber(fact.TargetSymbol ?? string.Empty)) is ({ } source, { } target)
                && (source != engineNumber || target != engineNumber)).ToArray();
            Require("MW-SAME-NAME-TEN-001", "traversal", foreignEdges.Length == 0,
                $"engine {engineNumber} boundary carries call evidence from another engine: [{string.Join(",", foreignEdges.Select(fact => $"{fact.SourceSymbol}->{fact.TargetSymbol}"))}]");
            var range = engineLineRanges[engineNumber!];
            var foreignLines = boundary.PathEvidence
                .Where(evidence => evidence.FilePath == "Services/TenEngines.cs" && evidence.StartLine is not null)
                .Where(evidence => evidence.StartLine < range.Start || evidence.StartLine > range.End)
                .ToArray();
            Require("MW-SAME-NAME-TEN-001", "traversal", foreignLines.Length == 0,
                $"engine {engineNumber} boundary cites lines outside its own class block: [{string.Join(",", foreignLines.Select(evidence => evidence.StartLine))}]");
        }

        var engineChains = packet.EventChains.Where(chain => chain.HandlerSymbol?.Contains(EnginesHandler, StringComparison.Ordinal) == true).ToArray();
        Require("MW-SAME-NAME-TEN-001", "traversal", engineChains.Length >= 1, "engines handler chain is missing");
        Require("MW-SAME-NAME-TEN-001", "traversal",
            engineChains.All(chain => chain.TraversalObservation?.TerminalReachabilityComplete == true),
            "engine terminal inventory must be complete");
        Require("MW-SAME-NAME-TEN-001", "traversal",
            engineChains.All(chain => chain.TraversalObservation?.DistinctReachableTerminalCount == 10),
            "the engines handler must inventory exactly ten distinct terminals");
    }

    [Fact]
    public async Task Overloads_and_uncertain_interface_receiver_stay_distinct_through_traversal()
    {
        using var temp = new TempDirectory();
        var (alpha, alphaIndex) = ScanRoot(temp, "root-alpha", "alpha-site");
        var handlerCalls = alpha.Facts.Where(fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.SourceSymbol?.Contains(AmbiguityHandler, StringComparison.Ordinal) == true
            && fact.TargetSymbol?.Contains("IAmbiguousGateway.Process(", StringComparison.Ordinal) == true).ToArray();
        Require("MW-OVERLOAD-001", "extraction", handlerCalls.Length == 2,
            $"the handler must retain two distinct interface overload calls, found {handlerCalls.Length}");
        Require("MW-OVERLOAD-001", "extraction",
            handlerCalls.Select(fact => fact.Properties.GetValueOrDefault("targetSymbolId"))
                .Distinct(StringComparer.Ordinal).Count() == 2,
            "the two overloads collapsed to one metadata-aware symbol identity");

        var relationships = alpha.Facts.Where(fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "ImplementsInterfaceMember"
            && fact.TargetSymbol?.Contains("IAmbiguousGateway.Process(", StringComparison.Ordinal) == true).ToArray();
        Require("MW-RECEIVER-AMBIGUITY-001", "extraction", relationships.Length == 4,
            $"two implementations of two overloads must produce four member relationships, found {relationships.Length}");
        Require("MW-OVERLOAD-001", "reconciliation",
            relationships.All(fact =>
                (fact.SourceSymbol?.Contains("(int value)", StringComparison.Ordinal) == true)
                == (fact.TargetSymbol?.Contains("(int value)", StringComparison.Ordinal) == true)),
            "an interface relationship crossed int and string signatures");

        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([alphaIndex], combinedPath, ["alpha-site"]));
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(alphaIndex, Path.Combine(temp.Path, "packet")));
        var packetChains = packet.EventChains.Where(chain => chain.HandlerSymbol?.Contains(AmbiguityHandler, StringComparison.Ordinal) == true).ToArray();
        var packetBoundaries = TerminalBoundaries(packet, AmbiguityHandler);
        var inventory = await CombinedDependencyPathReporter.BuildGraphInventoryAsync(combinedPath);
        var nodes = inventory.Nodes.ToDictionary(node => node.NodeId, node => node);
        var candidates = inventory.Edges.Where(edge =>
            edge.EdgeKind == "interface-candidate"
            && nodes[edge.FromNodeId].DisplayName.Contains("IAmbiguousGateway.Process(", StringComparison.Ordinal)).ToArray();
        Require("MW-RECEIVER-AMBIGUITY-001", "reconciliation", candidates.Length == 4,
            $"uncertain receiver must retain four explicit interface candidates, found {candidates.Length}");
        var handlerCallEdges = inventory.Edges.Where(edge =>
            edge.EdgeKind == "calls"
            && nodes[edge.FromNodeId].DisplayName.Contains(AmbiguityHandler, StringComparison.Ordinal)
            && nodes[edge.ToNodeId].DisplayName.Contains("IAmbiguousGateway.Process(", StringComparison.Ordinal)).ToArray();
        Require("MW-RECEIVER-AMBIGUITY-001", "reconciliation",
            handlerCallEdges.Length == 2
            && handlerCallEdges.All(edge => candidates.Any(candidate => candidate.FromNodeId == edge.ToNodeId)),
            $"semantic handler call nodes do not meet candidate interface nodes: calls=[{string.Join(',', handlerCallEdges.Select(edge => nodes[edge.ToNodeId].DisplayName))}], candidates=[{string.Join(',', candidates.Select(edge => nodes[edge.FromNodeId].DisplayName))}]");
        Require("MW-OVERLOAD-001", "reconciliation",
            candidates.All(edge =>
                (nodes[edge.FromNodeId].DisplayName.Contains("(int value)", StringComparison.Ordinal)
                 == nodes[edge.ToNodeId].DisplayName.Contains("(int value)", StringComparison.Ordinal))),
            "an interface candidate crossed overload signatures");
        Require("MW-RECEIVER-AMBIGUITY-001", "traversal", packetChains.Length >= 1,
            "ambiguous-receiver handler chain is missing");
        var observation = packetChains[0].TraversalObservation;
        Require("MW-RECEIVER-AMBIGUITY-001", "traversal",
            packetChains.All(chain => chain.TraversalObservation?.TerminalReachabilityComplete == true)
            && packetChains.SelectMany(chain => chain.TraversalObservation?.ReachableTerminalIds ?? []).Distinct(StringComparer.Ordinal).Count() == 4
            && packetChains.Any(chain => chain.TraversalObservation?.TraversedEdgeKinds.Contains("interface-candidate") == true),
            $"bounded traversal must inventory four candidate terminals; chains=[{string.Join(';', packetChains.Select(chain => $"{chain.ChainId}:complete={chain.TraversalObservation?.TerminalReachabilityComplete},count={chain.TraversalObservation?.DistinctReachableTerminalCount},edges={string.Join(',', chain.TraversalObservation?.TraversedEdgeKinds ?? [])}"))}]");
        Require("MW-OVERLOAD-001", "traversal", packetBoundaries.Count == 4
            && packetBoundaries.Select(boundary => boundary.TerminalEvidenceId).Distinct(StringComparer.Ordinal).Count() == 4,
            $"four overload/receiver terminal identities must survive, found {packetBoundaries.Count}");
        Require("MW-RECEIVER-AMBIGUITY-001", "traversal",
            packetBoundaries.All(boundary => boundary.Classification == CombinedDependencyPathClassifications.NeedsReviewStaticPath),
            $"candidate boundaries must require review; classifications=[{string.Join(';', packetBoundaries.Select(boundary => $"{boundary.Classification}:{string.Join(',', boundary.PathEvidence.Select(evidence => evidence.RuleId))}"))}]");
    }

    [Fact]
    public async Task Generated_designer_bridge_is_visible_but_does_not_invent_a_source_terminal()
    {
        using var temp = new TempDirectory();
        var (scan, index) = ScanRoot(temp, "root-generated", "generated-site");
        var bridgeCalls = scan.Facts.Where(fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.SourceSymbol?.Contains(GeneratedHandler, StringComparison.Ordinal) == true
            && fact.TargetSymbol?.Contains("GeneratedBridge.Run", StringComparison.Ordinal) == true).ToArray();
        Require("MW-GENERATED-MEMBERS-001", "extraction", bridgeCalls.Length == 1,
            $"expected one semantic call into the generated bridge, found {bridgeCalls.Length}");
        var generatedBodyEdges = scan.Facts.Where(fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.SourceSymbol?.Contains("GeneratedBridge.Run", StringComparison.Ordinal) == true).ToArray();
        Require("MW-GENERATED-MEMBERS-001", "extraction", generatedBodyEdges.Length == 0,
            $"auto-generated body was unexpectedly claimed as source call evidence: {generatedBodyEdges.Length}");

        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "packet")));
        var chains = packet.EventChains.Where(chain => chain.HandlerSymbol?.Contains(GeneratedHandler, StringComparison.Ordinal) == true).ToArray();
        Require("MW-GENERATED-MEMBERS-001", "traversal", chains.Length > 0,
            "generated-bridge handler chain is missing");
        Require("MW-GENERATED-MEMBERS-001", "traversal",
            chains.All(chain => chain.TraversalObservation?.DistinctReachableTerminalCount == 0)
            && TerminalBoundaries(packet, GeneratedHandler).Count == 0,
            "source traversal must not skip the excluded generated bridge and claim its terminal");
        Require("MW-GENERATED-MEMBERS-001", "traversal",
            packet.Gaps.Any(gap => gap.Classification == "DownstreamWithoutSupportedTerminal"),
            "generated bridge must leave an explicit static terminal-evidence gap");
        RequireCatalogEvidence("MW-GENERATED-MEMBERS-001", "traversal",
            bridgeCalls.Select(fact => fact.RuleId).Concat(chains.SelectMany(chain => chain.RuleIds))
                .Concat(chains.Where(chain => chain.TraversalObservation is not null)
                    .Select(chain => chain.TraversalObservation!.RuleId))
                .Concat(packet.Gaps.Select(gap => gap.RuleId)),
            bridgeCalls.Select(fact => fact.EvidenceTier).Concat(chains.SelectMany(chain => chain.EvidenceTiers))
                .Concat(packet.Gaps.Select(gap => gap.EvidenceTier)),
            packet.Gaps.Select(gap => gap.Classification));
    }

    [Fact]
    public void Cross_language_CSharp_VB_FSharp_chain_keeps_the_unsupported_FSharp_source_boundary()
    {
        using var temp = new TempDirectory();
        var (scan, _) = ScanRoot(temp, "root-crosslanguage", "cross-language");
        var calls = scan.Facts.Where(fact => fact.FactType == FactTypes.CallEdge).ToArray();
        var csharpToVb = calls.Where(fact =>
            fact.SourceSymbol?.Contains(CrossLanguageHandler, StringComparison.Ordinal) == true
            && fact.TargetSymbol?.Contains("VbBridge.Run", StringComparison.Ordinal) == true).ToArray();
        var csharpSyntaxCall = calls.Where(fact =>
            fact.RuleId == RuleIds.CSharpSyntaxCallGraph
            && fact.SourceSymbol?.Contains(CrossLanguageHandler, StringComparison.Ordinal) == true
            && fact.TargetSymbol == "Run").ToArray();
        var compilationGaps = scan.Facts.Where(fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.Evidence.FilePath == CrossLanguageSourcePath
            && fact.Evidence.StartLine == CrossLanguageInvocationLine
            && fact.Evidence.EndLine == CrossLanguageInvocationLine
            && fact.Properties.GetValueOrDefault("gapKind") == "CompilationDiagnostic"
            && fact.Properties.GetValueOrDefault("diagnosticId") == "CS0234").ToArray();
        // The catalog's alternatives are exclusive: a semantic edge cannot
        // coexist with fallback call evidence or its compilation gap.
        var semanticOutcome = csharpToVb.Length == 1
            && csharpToVb[0].RuleId == RuleIds.CSharpSemanticCallGraph
            && csharpToVb[0].EvidenceTier == EvidenceTiers.Tier1Semantic
            && csharpSyntaxCall.Length == 0 && compilationGaps.Length == 0;
        var syntaxFallbackOutcome = csharpToVb.Length == 0 && csharpSyntaxCall.Length == 1
            && csharpSyntaxCall[0].EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && csharpSyntaxCall[0].Evidence.FilePath == CrossLanguageSourcePath
            && csharpSyntaxCall[0].Evidence.StartLine == CrossLanguageInvocationLine
            && compilationGaps.Length > 0;
        Require("MW-CROSSLANGUAGE-001", "extraction",
            semanticOutcome || syntaxFallbackOutcome,
            $"C# to VB call must be exclusively semantic or explicitly downgraded to syntax plus compilation gap; semantic={csharpToVb.Length}, syntax={csharpSyntaxCall.Length}, gaps={compilationGaps.Length}");
        var vbToFsharp = calls.Where(fact =>
            fact.SourceSymbol?.Contains("VbBridge.Run", StringComparison.Ordinal) == true
            && fact.TargetSymbol?.Contains("Functions.Terminal", StringComparison.Ordinal) == true).ToArray();
        Require("MW-CROSSLANGUAGE-001", "extraction", vbToFsharp.Length == 1
            && vbToFsharp[0].RuleId == RuleIds.VisualBasicSemanticCallGraph
            && vbToFsharp[0].EvidenceTier == EvidenceTiers.Tier1Semantic,
            $"expected one VB to F# semantic call, found {vbToFsharp.Length}");
        Require("MW-CROSSLANGUAGE-001", "reconciliation",
            !scan.Facts.Any(fact => fact.SourceSymbol?.Contains("Functions.Terminal", StringComparison.Ordinal) == true
                && fact.FactType == FactTypes.CallEdge),
            "an unsupported F# source body must not be invented as a call edge");

        var source = MessyRoot("root-crosslanguage");
        var compiled = ScanBoundRoot(temp, "root-crosslanguage", "cross-language-bound", [
            Path.Combine(source, "csharp", "bin", "Debug", "net10.0", "CrossLanguageEntry.dll"),
            Path.Combine(source, "vb", "bin", "Debug", "net10.0", "CrossLanguage.VisualBasic.dll"),
            Path.Combine(source, "fsharp", "bin", "Debug", "net10.0", "CrossLanguage.FSharp.dll")
        ]);
        var fsharpMethods = compiled.Facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
                && fact.TargetSymbol?.Contains("Functions", StringComparison.Ordinal) == true
                && fact.TargetSymbol.Contains("Terminal", StringComparison.Ordinal)).ToArray();
        Require("MW-CROSSLANGUAGE-001", "reconciliation", fsharpMethods.Length > 0,
            "F# compiled method must be inventoried independently of unavailable F# source");
        var unsupportedLanguageGaps = compiled.Facts.Where(fact =>
            fact.Properties.GetValueOrDefault("gapKind") == "SourceMetadataReconciliationUnsupportedLanguage").ToArray();
        Require("MW-CROSSLANGUAGE-001", "reconciliation", unsupportedLanguageGaps.Length > 0,
            "F# source-to-compiled join must emit an explicit unsupported-language gap");
        Require("MW-CROSSLANGUAGE-001", "reconciliation",
            !compiled.Facts.Any(fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled
                && fact.TargetSymbol?.Contains("CrossLanguage.FSharp", StringComparison.Ordinal) == true),
            "an F# source-to-compiled identity must not be guessed from admitted metadata");
        var caseEvidence = csharpToVb.Concat(csharpSyntaxCall).Concat(vbToFsharp)
            .Concat(compilationGaps).Concat(fsharpMethods).Concat(unsupportedLanguageGaps).ToArray();
        RequireCatalogEvidence("MW-CROSSLANGUAGE-001", "reconciliation",
            caseEvidence.Select(fact => fact.RuleId), caseEvidence.Select(fact => fact.EvidenceTier),
            caseEvidence.Where(fact => fact.Properties.TryGetValue("gapKind", out _))
                .Select(fact => fact.Properties["gapKind"]));
    }

    [Fact]
    public void Messy_generated_root_joins_source_metadata_IL_and_portable_PDB_only_by_exact_compiled_identity()
    {
        using var temp = new TempDirectory();
        var source = MessyRoot("root-generated");
        var assembly = Path.Combine(source, "bin", "Debug", "net10.0", "GeneratedSite.dll");
        var pdb = Path.ChangeExtension(assembly, ".pdb");
        Require("MW-SOURCE-METADATA-IL-PDB-001", "extraction", File.Exists(assembly) && File.Exists(pdb),
            "the public generated-root assembly and portable PDB must be built before this test");

        var scan = ScanBoundRoot(temp, "root-generated", "generated-bound", [assembly], [pdb], ilBody: true);
        Require("MW-GENERATED-MEMBERS-001", "reconciliation",
            scan.Facts.Any(fact => fact.FactType == FactTypes.ManagedMethodDeclared
                && fact.TargetSymbol?.Contains("GeneratedBridge", StringComparison.Ordinal) == true
                && fact.TargetSymbol.Contains("Run", StringComparison.Ordinal))
            && !scan.Facts.Any(fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled
                && fact.TargetSymbol?.Contains("GeneratedBridge", StringComparison.Ordinal) == true
                && fact.TargetSymbol.Contains("Run", StringComparison.Ordinal)),
            "the generated bridge must be inventoried in metadata without inventing a source identity join");
        var sourceEdges = scan.Facts.Where(fact =>
            fact.FactType == FactTypes.SourceMetadataIdentityReconciled
            && fact.TargetSymbol?.Contains("GeneratedButton_Click", StringComparison.Ordinal) == true).ToArray();
        Require("MW-SOURCE-METADATA-IL-PDB-001", "reconciliation", sourceEdges.Length == 1,
            "handler source symbol must reconcile to one exact compiled method identity");
        var sourceEdge = sourceEdges[0];
        var compiledFactId = sourceEdge.Properties.GetValueOrDefault("compiledFactId");
        Require("MW-SOURCE-METADATA-IL-PDB-001", "reconciliation", !string.IsNullOrWhiteSpace(compiledFactId),
            "source-to-metadata edge omitted its exact compiled fact ID");
        var ilBodies = scan.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.Properties.GetValueOrDefault("compiledFactId") == compiledFactId).ToArray();
        var pdbMethods = scan.Facts.Where(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled
            && fact.Properties.GetValueOrDefault("compiledFactId") == compiledFactId).ToArray();
        Require("MW-SOURCE-METADATA-IL-PDB-001", "reconciliation", ilBodies.Length == 1 && pdbMethods.Length == 1,
            $"the exact compiled handler must own one IL body and one PDB method; il={ilBodies.Length}, pdb={pdbMethods.Length}");
        var ilBody = ilBodies[0];
        var pdbMethod = pdbMethods[0];
        Require("MW-SOURCE-METADATA-IL-PDB-001", "reconciliation",
            sourceEdge.EvidenceTier == EvidenceTiers.Tier1Semantic
            && ilBody.EvidenceTier == EvidenceTiers.Tier2Structural
            && !string.IsNullOrWhiteSpace(sourceEdge.Properties.GetValueOrDefault("provenanceBindingInputSha256"))
            && !string.IsNullOrWhiteSpace(ilBody.Properties.GetValueOrDefault("ilBoundedInputSha256"))
            && !string.IsNullOrWhiteSpace(ilBody.Properties.GetValueOrDefault("ilGeneratorSha256"))
            && !string.IsNullOrWhiteSpace(pdbMethod.Properties.GetValueOrDefault("pdbBoundedInputSha256"))
            && !string.IsNullOrWhiteSpace(pdbMethod.Properties.GetValueOrDefault("pdbGeneratorSha256")),
            "joined evidence must retain its tiers, binding input, bounded input, and generator hashes");
        var sequencePoints = scan.Facts.Where(fact => fact.FactType == FactTypes.PdbSequencePointDeclared
            && fact.Properties.GetValueOrDefault("metadataPdbReconciliationFactId") == pdbMethod.FactId).ToArray();
        Require("MW-SOURCE-METADATA-IL-PDB-001", "reconciliation", sequencePoints.Length > 0,
            "the bound handler PDB method must own a source sequence point");
        var joinedEvidence = new[] { sourceEdge, ilBody, pdbMethod }.Concat(sequencePoints).ToArray();
        RequireCatalogEvidence("MW-SOURCE-METADATA-IL-PDB-001", "reconciliation",
            joinedEvidence.Select(fact => fact.RuleId), joinedEvidence.Select(fact => fact.EvidenceTier), []);
    }

    [Fact]
    public async Task Separately_scanned_roots_merge_without_invented_joins()
    {
        using var temp = new TempDirectory();
        var (alpha, alphaIndex) = ScanRoot(temp, "root-alpha", "alpha-site");
        var (beta, betaIndex) = ScanRoot(temp, "root-beta", "beta-site");
        var (vb, vbIndex) = ScanRoot(temp, "vb-projectless", "vb-site");

        // MW-MERGED-ROOTS-001 [extraction]: root-beta reuses the Process/Core simple
        // names with its own independent identities and terminal.
        var betaProcess = beta.Facts.SingleOrDefault(fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol == "global::Beta.Services.Gateway.Process()");
        Require("MW-MERGED-ROOTS-001", "extraction", betaProcess is not null, "beta Process call edge is missing");
        var betaTerminal = beta.Facts.SingleOrDefault(fact =>
            fact.FactType == FactTypes.DatabaseOperationCandidate
            && fact.Properties.GetValueOrDefault("tableName") == "beta_status");
        Require("MW-MERGED-ROOTS-001", "extraction", betaTerminal is not null, "beta terminal is missing");
        Require("MW-MERGED-ROOTS-001", "extraction", betaTerminal!.SourceSymbol == "global::Beta.Services.Gateway.Core()",
            "beta terminal must attach to the beta Core identity");

        // MW-MERGED-ROOTS-001 [combining]: the merged index is the union of the three
        // scans with per-source namespacing and no symbol deduplication.
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var combined = await CombinedIndexBuilder.CombineAsync(new CombineOptions(
            [alphaIndex, betaIndex, vbIndex], combinedPath, ["alpha-site", "beta-site", "vb-site"]));
        Require("MW-MERGED-ROOTS-001", "combining", combined.Sources.Count == 3, $"combined sources was {combined.Sources.Count}");
        Require("MW-MERGED-ROOTS-001", "combining",
            combined.FactCount == alpha.Facts.Count + beta.Facts.Count + vb.Facts.Count,
            $"combined fact count {combined.FactCount} != sum {alpha.Facts.Count + beta.Facts.Count + vb.Facts.Count}");

        using (var connection = new SqliteConnection($"Data Source={combinedPath}"))
        {
            connection.Open();
            var labels = QueryStrings(connection, "SELECT label FROM index_sources ORDER BY label");
            RequireCatalogEvidence("MW-MERGED-ROOTS-001", "reconciliation",
                QueryStrings(connection, "SELECT DISTINCT rule_id FROM combined_call_edges"),
                QueryStrings(connection, "SELECT DISTINCT evidence_tier FROM combined_call_edges"), []);
            Require("MW-MERGED-ROOTS-001", "combining",
                labels.SequenceEqual(["alpha-site", "beta-site", "vb-site"]),
                $"combined labels were [{string.Join(",", labels)}]");
            var expectedSymbols = CountRows(alphaIndex, "symbols") + CountRows(betaIndex, "symbols") + CountRows(vbIndex, "symbols");
            var combinedSymbols = CountRows(combinedPath, "combined_symbols");
            Require("MW-MERGED-ROOTS-001", "combining", combinedSymbols == expectedSymbols,
                $"combined symbol count {combinedSymbols} != sum {expectedSymbols}; symbols were merged or lost");

            // MW-MERGED-ROOTS-001 [reconciliation]: the eleven same-named Process
            // identities across roots stay distinct rows; nothing cross-joins. The
            // complete (source label, caller, callee) tuple is validated so an edge
            // imported under the wrong source or a caller from the other root fails.
            using var processCommand = connection.CreateCommand();
            processCommand.CommandText = """
                SELECT s.label, e.caller_symbol, e.callee_symbol
                FROM combined_call_edges e
                JOIN index_sources s ON s.source_index_id = e.source_index_id
                WHERE e.callee_symbol LIKE '%.Process()'
                ORDER BY s.label, e.caller_symbol, e.callee_symbol
                """;
            var processTuples = new List<(string Label, string? Caller, string? Callee)>();
            using (var processReader = processCommand.ExecuteReader())
            {
                while (processReader.Read())
                {
                    processTuples.Add((processReader.GetString(0),
                        processReader.IsDBNull(1) ? null : processReader.GetString(1),
                        processReader.IsDBNull(2) ? null : processReader.GetString(2)));
                }
            }

            var expectedProcessTuples = new[] { (Label: "alpha-site", Scan: alpha), (Label: "beta-site", Scan: beta), (Label: "vb-site", Scan: vb) }
                .SelectMany(source => source.Scan.Facts
                    .Where(fact => fact.FactType == FactTypes.CallEdge
                        && fact.TargetSymbol?.EndsWith(".Process()", StringComparison.Ordinal) == true)
                    .Select(fact => (source.Label, Caller: fact.SourceSymbol, Callee: fact.TargetSymbol)))
                .OrderBy(tuple => tuple.Label, StringComparer.Ordinal)
                .ThenBy(tuple => tuple.Caller, StringComparer.Ordinal)
                .ThenBy(tuple => tuple.Callee, StringComparer.Ordinal)
                .ToArray();
            Require("MW-MERGED-ROOTS-001", "reconciliation", expectedProcessTuples.Length == 11,
                $"expected eleven original Process edges, found {expectedProcessTuples.Length}");
            Require("MW-MERGED-ROOTS-001", "reconciliation",
                processTuples.SequenceEqual(expectedProcessTuples),
                "merged Process edges must preserve every original (source label, caller, callee) tuple exactly, including multiplicity");
        }

        // MW-MERGED-ROOTS-001 [reconciliation]: every merged terminal keeps its own
        // source label and its own table identity; nothing crosses a source
        // boundary. The Web Forms packet itself requires exactly one page-bearing
        // source by design, so cross-source attribution is asserted directly over
        // the merged index instead of through a combined packet.
        using (var connection = new SqliteConnection($"Data Source={combinedPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT s.label, f.original_fact_id, f.source_symbol, json_extract(f.properties_json, '$.tableName')
                FROM combined_facts f
                JOIN index_sources s ON s.source_index_id = f.source_index_id
                WHERE f.fact_type = 'DatabaseOperationCandidate'
                ORDER BY s.label, f.original_fact_id
                """;
            var terminalTuples = new List<(string Label, string FactId, string? SourceSymbol, string? TableName)>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    terminalTuples.Add((reader.GetString(0), reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3)));
                }
            }

            var expectedTerminalTuples = new[] { (Label: "alpha-site", Scan: alpha), (Label: "beta-site", Scan: beta), (Label: "vb-site", Scan: vb) }
                .SelectMany(source => source.Scan.Facts
                    .Where(fact => fact.FactType == FactTypes.DatabaseOperationCandidate)
                    .Select(fact => (source.Label, fact.FactId, fact.SourceSymbol, TableName: fact.Properties.GetValueOrDefault("tableName"))))
                .OrderBy(tuple => tuple.Label, StringComparer.Ordinal)
                .ThenBy(tuple => tuple.FactId, StringComparer.Ordinal)
                .ToArray();
            Require("MW-MERGED-ROOTS-001", "reconciliation",
                expectedTerminalTuples.Count(tuple => tuple.Label == "alpha-site") == 15
                && expectedTerminalTuples.Count(tuple => tuple.Label == "beta-site") == 1
                && expectedTerminalTuples.Count(tuple => tuple.Label == "vb-site") == 1,
                "original scans must supply fifteen alpha, one beta, and one VB terminal");
            Require("MW-MERGED-ROOTS-001", "reconciliation",
                terminalTuples.SequenceEqual(expectedTerminalTuples),
                "merged terminals must preserve every original (source label, fact id, source symbol, table name) tuple exactly, including multiplicity");
        }

        // MW-MERGED-ROOTS-001 [combining]: the merged review report lists every
        // source and writes its artifacts.
        var reportOut = Path.Combine(temp.Path, "report");
        await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, reportOut));
        var reportPath = Path.Combine(reportOut, "dependency-report.json");
        Require("MW-MERGED-ROOTS-001", "combining", File.Exists(reportPath), "merged dependency report was not written");
        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        var reportLabels = report.RootElement.GetProperty("sources").EnumerateArray()
            .Select(source => source.GetProperty("label").GetString())
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();
        Require("MW-MERGED-ROOTS-001", "combining",
            reportLabels.SequenceEqual(["alpha-site", "beta-site", "vb-site"]),
            $"merged report sources were [{string.Join(",", reportLabels)}]");
    }

    [Fact]
    public async Task Projectless_vb_falls_back_to_syntax_with_fail_closed_gaps()
    {
        using var temp = new TempDirectory();
        var repo = MessyRoot("vb-projectless");
        Require("MW-VB-PROJECTLESS-001", "extraction",
            !Directory.EnumerateFiles(repo, "*.vbproj", SearchOption.AllDirectories).Any(),
            "the projectless VB root must not contain a vbproj");
        Require("MW-VB-PROJECTLESS-001", "extraction",
            !Directory.EnumerateFiles(repo, "*.sln", SearchOption.AllDirectories).Any(),
            "the projectless VB root must not contain a solution");

        var (scan, index) = ScanRoot(temp, "vb-projectless", "vb-site");
        Require("MW-VB-PROJECTLESS-001", "extraction", scan.Manifest.AnalysisLevel == "Level3SyntaxAnalysis",
            $"projectless VB analysis level was {scan.Manifest.AnalysisLevel}");

        var workspaceGap = scan.Facts.SingleOrDefault(fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.VisualBasicSemanticWorkspace
            && fact.Properties.GetValueOrDefault("gapKind") == "NoVisualBasicProjectOrSolution");
        Require("MW-VB-PROJECTLESS-001", "extraction", workspaceGap is not null,
            "the fail-closed NoVisualBasicProjectOrSolution gap is missing");
        Require("MW-VB-PROJECTLESS-001", "extraction", workspaceGap!.EvidenceTier == EvidenceTiers.Tier4Unknown,
            $"workspace gap tier was {workspaceGap.EvidenceTier}");

        foreach (var file in (string[])["App_Code/Queue.vb", "Default.aspx.vb"])
        {
            Require("MW-VB-PROJECTLESS-001", "extraction",
                scan.Facts.Any(fact =>
                    fact.FactType == FactTypes.AnalysisGap
                    && fact.Properties.GetValueOrDefault("gapKind") == "SemanticAnalysisUnavailable"
                    && fact.Evidence.FilePath == file),
                $"per-file SemanticAnalysisUnavailable gap missing for {file}");
        }

        RequireCatalogEvidence("MW-VB-PROJECTLESS-001", "extraction",
            scan.Facts.Select(fact => fact.RuleId), scan.Facts.Select(fact => fact.EvidenceTier),
            scan.Facts.Where(fact => fact.FactType == FactTypes.AnalysisGap)
                .Select(fact => fact.Properties.GetValueOrDefault("gapKind") ?? string.Empty));

        var operation = scan.Facts.SingleOrDefault(fact =>
            fact.FactType == FactTypes.DatabaseOperationCandidate
            && fact.RuleId == RuleIds.VisualBasicSyntaxDatabaseOperation);
        Require("MW-VB-PROJECTLESS-001", "extraction", operation is not null,
            "the VB data-adapter fill terminal is missing");
        Require("MW-VB-PROJECTLESS-001", "extraction", operation!.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual,
            $"VB terminal tier was {operation.EvidenceTier}");

        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "packet")));
        var chains = packet.EventChains.Where(chain => chain.HandlerSymbol?.Contains(VbHandler, StringComparison.Ordinal) == true).ToArray();
        Require("MW-VB-PROJECTLESS-001", "extraction", chains.Length >= 1, "the VB handler chain is missing");
        Require("MW-VB-PROJECTLESS-001", "extraction", chains.All(chain => chain.TerminalKind == "sql-query"),
            "the VB handler must reach its sql-query terminal");
        var boundary = TerminalBoundaries(packet, VbHandler).FirstOrDefault();
        Require("MW-VB-PROJECTLESS-001", "extraction", boundary is not null, "the VB handler boundary is missing");
        Require("MW-VB-PROJECTLESS-001", "extraction", boundary!.RuleIds.Contains(RuleIds.VisualBasicSyntaxDatabaseOperation),
            "the VB boundary must cite the VB syntax database rule");
        Require("MW-VB-PROJECTLESS-001", "extraction", boundary.EvidenceTiers.Contains(EvidenceTiers.Tier3SyntaxOrTextual),
            "the VB boundary must stay Tier3");
        Require("MW-VB-PROJECTLESS-001", "extraction", boundary.CoverageLabels.Contains("reduced-syntax-vb-database-operation"),
            $"the VB boundary coverage label was [{string.Join(",", boundary.CoverageLabels)}]");
    }

    [Fact]
    public async Task Repeat_scans_pin_byte_identical_facts_per_root()
    {
        using var temp = new TempDirectory();
        foreach (var root in (string[])["root-alpha", "root-beta", "vb-projectless", "root-generated", "root-crosslanguage"])
        {
            var first = Path.Combine(temp.Path, $"{root}-first");
            var second = Path.Combine(temp.Path, $"{root}-second");
            await RunCliAsync("scan", temp, MessyRoot(root), first);
            await RunCliAsync("scan", temp, MessyRoot(root), second);
            var firstFacts = Path.Combine(first, "facts.ndjson");
            var secondFacts = Path.Combine(second, "facts.ndjson");
            Require("MW-DETERMINISM", "extraction", File.Exists(firstFacts) && File.Exists(secondFacts), $"{root} scan did not write facts.ndjson");
            Require("MW-DETERMINISM", "extraction",
                SHA256.HashData(File.ReadAllBytes(firstFacts)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(secondFacts))),
                $"{root} repeat scan produced different facts.ndjson bytes");
        }
    }

    private static string NormalizeFactId(string factId) =>
        factId.StartsWith("single:", StringComparison.Ordinal) ? factId["single:".Length..] : factId;

    private static string? EngineNumber(string? symbol)
    {
        var match = System.Text.RegularExpressions.Regex.Match(symbol ?? string.Empty, @"Engine(\d\d)\.");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static Dictionary<string, (int Start, int End)> EngineClassLineRanges(string tenEnginesPath)
    {
        var ranges = new Dictionary<string, (int Start, int End)>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(tenEnginesPath);
        string? current = null;
        var start = 0;
        for (var index = 0; index < lines.Length; index++)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[index], @"sealed class Engine(\d\d)");
            if (match.Success)
            {
                if (current is not null)
                {
                    ranges[current] = (start, index);
                }

                current = match.Groups[1].Value;
                start = index + 1;
            }
        }

        if (current is not null)
        {
            ranges[current] = (start, lines.Length);
        }

        return ranges;
    }

    private static IReadOnlyList<WebFormsModernizationDownstreamBoundary> TerminalBoundaries(
        WebFormsModernizationPacket packet, string handlerName) =>
        packet.DownstreamBoundaries
            .Where(boundary => packet.EventChains
                .Any(chain => chain.ChainId == boundary.ChainId && chain.HandlerSymbol?.Contains(handlerName, StringComparison.Ordinal) == true))
            .ToList();


    private static (ScanResult Scan, string IndexPath) ScanRoot(TempDirectory temp, string rootName, string label)
    {
        var repo = MessyRoot(rootName);
        var outDir = Path.Combine(temp.Path, $"{rootName}-{label}-scan");
        var scan = ScanEngine.Scan(new ScanOptions(repo, outDir));
        var indexPath = Path.Combine(temp.Path, $"{rootName}-{label}.sqlite");
        SqliteIndexWriter.Write(indexPath, scan.Manifest, scan.Facts);
        return (scan, indexPath);
    }

    private static ScanResult ScanBoundRoot(
        TempDirectory temp,
        string rootName,
        string label,
        IReadOnlyList<string> assemblies,
        IReadOnlyList<string>? pdbs = null,
        bool ilBody = false)
    {
        var source = MessyRoot(rootName);
        var commit = GitMetadataProvider.Detect(source).CommitSha;
        var receipt = Path.Combine(temp.Path, $"{label}-binding-receipt.json");
        var output = Path.Combine(temp.Path, $"{label}-bound-scan");
        var evaluation = ManagedMetadataExtractor.Evaluate(source, commit,
            new ScanOptions(source, output, CompiledInputPaths: assemblies));
        File.WriteAllText(receipt, JsonSerializer.Serialize(new
        {
            schemaVersion = "compiled-input-binding-set.v1",
            bindings = evaluation.Provenance!.Outcomes.Select(outcome => new
            {
                schemaVersion = "compiled-input-binding.v1",
                safeLocator = outcome.SafeLocator,
                artifactSha256 = outcome.RawFileSha256,
                assemblyIdentity = outcome.AssemblyIdentity,
                binarySourceRepository = "public-fixture",
                binarySourceCommitSha = commit,
                binaryBuildIdentity = "test-build"
            }).ToArray()
        }));
        return ScanEngine.Scan(new ScanOptions(source, output,
            CompiledInputPaths: assemblies,
            CompiledBindingReceiptPaths: [receipt],
            PdbInputPaths: pdbs,
            IlBodyEvidence: ilBody));
    }

    private static string MessyRoot(string rootName) =>
        Path.Combine(FindRepoRoot(), "samples", "messy-dotnet-workspace", rootName);

    private static async Task RunCliAsync(string command, TempDirectory temp, string repo, string outDir)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync([command, "--repo", repo, "--out", outDir], output, error);
        Require("MW-DETERMINISM", "extraction", exitCode == 0, $"CLI scan failed: {error}");
    }

    private static long CountRows(string indexPath, string table)
    {
        using var connection = new SqliteConnection($"Data Source={indexPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }

    private static object? ExecuteScalar(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command.ExecuteScalar();
    }

    private static List<string> QueryStrings(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.IsDBNull(0) ? string.Empty : reader.GetString(0));
        }

        return values;
    }

    private static void RequireCatalogEvidence(string caseId, string stage,
        IEnumerable<string> ruleIds, IEnumerable<string> tiers, IEnumerable<string> gaps)
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), CatalogPath)));
        var entry = catalog.RootElement.GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == caseId);
        var observed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["expectedRuleIds"] = ruleIds.ToHashSet(StringComparer.Ordinal),
            ["expectedTiers"] = tiers.ToHashSet(StringComparer.Ordinal),
            ["expectedGaps"] = gaps.ToHashSet(StringComparer.Ordinal),
        };
        static bool Matches(JsonElement contract, IReadOnlyDictionary<string, HashSet<string>> observed) =>
            observed.All(pair => contract.GetProperty(pair.Key).EnumerateArray()
                .All(expected => pair.Value.Contains(expected.GetString()!)));

        foreach (var (property, actual) in observed)
        {
            foreach (var expected in entry.GetProperty(property).EnumerateArray().Select(item => item.GetString()!))
            {
                Require(caseId, stage, actual.Contains(expected),
                    $"catalog {property} value {expected} is missing from the case's produced evidence");
            }
        }

        if (entry.TryGetProperty("alternativeEvidence", out var alternatives))
        {
            Require(caseId, stage, alternatives.EnumerateArray().Any(alternative => Matches(alternative, observed)),
                "none of the catalog's alternative evidence outcomes was produced");
        }
    }

    private static void Require(string caseId, string stage, bool condition, string detail)
    {
        if (!condition)
        {
            throw new Xunit.Sdk.XunitException($"messy-workspace case {caseId} failed at stage {stage}: {detail}");
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))
                || File.Exists(Path.Combine(current.FullName, "TraceMap.slnx"))
                || Directory.Exists(Path.Combine(current.FullName, ".kiro")))
            {
                return current.FullName;
            }

            current = current.Parent!;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
