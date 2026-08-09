using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record AccessScreenDataFlowOptions(
    string IndexPath,
    string OutputDirectory,
    int MaxDepth = 12,
    int MaxPaths = 100,
    int MaxGaps = 1_000);

public sealed record AccessScreenDataFlowResult(
    AccessScreenDataFlowReport Report,
    string MarkdownPath,
    string JsonPath);

public sealed record AccessScreenDataFlowReport(
    string SchemaVersion,
    string RepositoryId,
    string CommitSha,
    string Coverage,
    AccessScreenDataFlowQuery Query,
    AccessScreenDataFlowSummary Summary,
    IReadOnlyList<AccessFlowRoot> Roots,
    IReadOnlyList<AccessFlowPath> Paths,
    IReadOnlyList<AccessFlowGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record AccessScreenDataFlowQuery(int MaxDepth, int MaxPaths, int MaxGaps);
public sealed record AccessScreenDataFlowSummary(int RootCount, int PathCount, int GapCount, bool Truncated);
public sealed record AccessFlowRoot(string RootId, string RootKind, string NodeId, AccessFlowEvidence Evidence);
public sealed record AccessFlowPath(
    string PathId,
    string RootId,
    string Classification,
    string EvidenceTier,
    string TerminalKind,
    int Depth,
    IReadOnlyList<AccessFlowNode> Nodes,
    IReadOnlyList<AccessFlowEdge> Edges,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> Limitations);
public sealed record AccessFlowNode(string NodeId, string NodeKind, string StableKey, IReadOnlyList<string> SupportingFactIds);
public sealed record AccessFlowEdge(string EdgeId, string EdgeKind, string FromNodeId, string ToNodeId, AccessFlowEvidence Evidence);
public sealed record AccessFlowEvidence(
    string FactId,
    string RuleId,
    string EvidenceTier,
    string CommitSha,
    string FilePath,
    int StartLine,
    int EndLine,
    string ExtractorId,
    string ExtractorVersion,
    string CoverageLabel,
    IReadOnlyList<string> Limitations);
public sealed record AccessFlowGap(
    string GapId,
    string Classification,
    string ScopeKind,
    string? ScopeNodeId,
    string RuleId,
    string EvidenceTier,
    string CommitSha,
    string FilePath,
    int StartLine,
    int EndLine,
    string ExtractorId,
    string ExtractorVersion,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> Limitations);

public static class AccessScreenDataFlowReporter
{
    public const string SchemaVersion = "tracemap.access-screen-data-flow.v1";
    private const int MaxPersistedSupportCharacters = 65_536;
    private const int MaxPersistedSupportIds = 1_024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly IReadOnlyList<string> ReportLimitations =
    [
        "Static candidate trails do not prove startup selection, event firing, user navigation, branch feasibility, runtime reachability, or execution.",
        "The report does not prove row access, external connectivity, production use, correctness, completeness, safety to run, or release approval.",
        "Opaque stable identities are rendered; raw names, SQL, VBA, expressions, macro bodies, connections, credentials, customer identity, and local paths are omitted."
    ];

    public static async Task<AccessScreenDataFlowResult> WriteAsync(
        AccessScreenDataFlowOptions options,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        var report = await BuildReportAsync(options, cancellationToken);
        var output = Path.GetFullPath(options.OutputDirectory);
        if (Directory.Exists(output) || File.Exists(output))
            throw new InvalidDataException("AccessFlowOutputExists");
        var parent = Path.GetDirectoryName(output) ?? throw new InvalidDataException("AccessFlowOutputInvalid");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".{Path.GetFileName(output)}.access-flow-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(staging);
            var markdown = Path.Combine(staging, "access-flow.md");
            var json = Path.Combine(staging, "access-flow.json");
            await File.WriteAllTextAsync(markdown, RenderMarkdown(report), new UTF8Encoding(false), cancellationToken);
            await File.WriteAllTextAsync(json, JsonSerializer.Serialize(report, JsonOptions) + "\n", new UTF8Encoding(false), cancellationToken);
            Directory.Move(staging, output);
            return new(report, Path.Combine(output, "access-flow.md"), Path.Combine(output, "access-flow.json"));
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    public static async Task<AccessScreenDataFlowReport> BuildReportAsync(
        AccessScreenDataFlowOptions options,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        var (repository, commitSha, facts) = await ReadFactsAsync(options.IndexPath, cancellationToken);
        return Build(repository, commitSha, facts, options.MaxDepth, options.MaxPaths, options.MaxGaps);
    }

    internal static AccessScreenDataFlowReport Build(
        string repository,
        string? commitSha,
        IReadOnlyList<CodeFact> facts,
        int maxDepth,
        int maxPaths,
        int maxGaps)
    {
        var safeCommit = SafeCommit(commitSha)
            ?? throw new InvalidDataException("AccessFlowScanIdentityUnavailable");
        if (string.IsNullOrWhiteSpace(repository))
            throw new InvalidDataException("AccessFlowScanIdentityUnavailable");
        var accessFacts = facts.Where(IsAccessFact).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();
        if (accessFacts.Any(fact =>
                !string.Equals(fact.Repo, repository, StringComparison.Ordinal)
                || !string.Equals(SafeCommit(fact.CommitSha), safeCommit, StringComparison.Ordinal)))
            throw new InvalidDataException("AccessFlowScanIdentityUnavailable");
        var gapSupportIndex = BuildGapSupportIndex(accessFacts);
        var nodes = new Dictionary<string, MutableNode>(StringComparer.Ordinal);
        var edges = new List<AccessFlowEdge>();
        var gaps = new List<AccessFlowGap>();
        var truncated = false;

        foreach (var fact in accessFacts)
        {
            var kind = NodeKind(fact.FactType);
            var stableKey = fact.TargetSymbol;
            if (kind is not null && SafeStableKey(stableKey))
                AddNode(nodes, stableKey!, kind, fact.FactId);
        }

        foreach (var fact in accessFacts)
        {
            if (fact.FactType == FactTypes.AnalysisGap)
            {
                var classification = SafeCategory(
                    fact.Properties.GetValueOrDefault("classification"),
                    "AccessAnalysisGap");
                var supportingFactIds = SupportingGapFactIds(fact, accessFacts, gapSupportIndex);
                AddGap(gaps, maxGaps, ref truncated, new(
                    Id("gap", classification, fact.FactId),
                    classification,
                    SafeCategory(fact.Properties.GetValueOrDefault("scopeKind"), "access"),
                    NodeId(fact.TargetSymbol),
                    fact.RuleId,
                    EvidenceTiers.Tier4Unknown,
                    SafeCommit(fact.CommitSha)!,
                    SafeEvidencePath(fact.Evidence.FilePath),
                    fact.Evidence.StartLine,
                    fact.Evidence.EndLine,
                    SafeToken(fact.Evidence.ExtractorId),
                    SafeToken(fact.Evidence.ExtractorVersion),
                    supportingFactIds,
                    SafeLimitations(fact.Properties.GetValueOrDefault("limitations"))));
                continue;
            }

            var edge = EdgeFrom(fact, nodes, gaps, maxGaps, ref truncated);
            if (edge is not null) edges.Add(edge);
        }

        if (!accessFacts.Any(fact => fact.FactType is FactTypes.AccessFormDeclared
                or FactTypes.AccessEventBindingCandidate
                or FactTypes.AccessCommandCandidate
                or FactTypes.AccessVbaProcedureDeclared
                or FactTypes.AccessNavigationCandidate
                or FactTypes.AccessBindingDeclared))
            AddGap(gaps, maxGaps, ref truncated, Gap("AccessDesignFlowEvidenceUnavailable", "database", null, safeCommit));

        var roots = new List<AccessFlowRoot>();
        foreach (var fact in accessFacts.Where(fact => fact.FactType == FactTypes.AccessMacroDeclared
                     && fact.Properties.GetValueOrDefault("startupRole") == "autoexec"
                     && SafeStableKey(fact.TargetSymbol)))
            roots.Add(new(Id("root", "startup", fact.TargetSymbol!), "startup-candidate", NodeId(fact.TargetSymbol)!, Evidence(fact)));

        foreach (var fact in accessFacts.Where(fact => fact.FactType == FactTypes.AccessFormDeclared && SafeStableKey(fact.TargetSymbol)))
            roots.Add(new(Id("root", "form", fact.TargetSymbol!), "ui-root-candidate", NodeId(fact.TargetSymbol)!, Evidence(fact)));

        if (!roots.Any(root => root.RootKind == "startup-candidate"))
            AddGap(gaps, maxGaps, ref truncated, Gap("AccessStartupIdentityUnavailable", "startup", null, safeCommit));
        if (roots.Count == 0)
            AddGap(gaps, maxGaps, ref truncated, Gap("AccessFlowRootUnavailable", "root", null, safeCommit));

        var immutableNodes = nodes.Values.ToDictionary(
            node => node.NodeId,
            node => new AccessFlowNode(node.NodeId, node.Kind, node.StableKey, node.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray()),
            StringComparer.Ordinal);
        var partialNodeIds = nodes.Values
            .Where(node => !node.Declared || node.Kind == "unresolved-target")
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        var outgoing = edges.GroupBy(edge => edge.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(edge => edge.EdgeId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var paths = Traverse(
            roots.OrderBy(root => root.RootId, StringComparer.Ordinal).ToArray(),
            immutableNodes,
            partialNodeIds,
            outgoing,
            maxDepth,
            maxPaths,
            maxGaps,
            safeCommit,
            gaps,
            ref truncated);
        var orderedGaps = gaps.OrderBy(gap => gap.Classification, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal).Take(maxGaps).ToArray();
        var coverage = orderedGaps.Length == 0
            && !truncated
            && paths.All(path => path.Classification == "StaticCandidateTrail")
                ? "complete"
                : "partial";
        return new(
            SchemaVersion,
            RepositoryId(repository),
            safeCommit,
            coverage,
            new(maxDepth, maxPaths, maxGaps),
            new(roots.Count, paths.Count, orderedGaps.Length, truncated),
            roots.OrderBy(root => root.RootId, StringComparer.Ordinal).ToArray(),
            paths,
            orderedGaps,
            ReportLimitations);
    }

    private static AccessFlowEdge? EdgeFrom(
        CodeFact fact,
        IDictionary<string, MutableNode> nodes,
        List<AccessFlowGap> gaps,
        int maxGaps,
        ref bool truncated)
    {
        if (fact.FactType == FactTypes.AccessQueryOutputSourceCandidate)
        {
            var sourceRole = fact.Properties.GetValueOrDefault("sourceRole");
            if (!string.IsNullOrWhiteSpace(sourceRole) && sourceRole != "output-expression")
                return null;
        }

        string? kind = fact.FactType switch
        {
            FactTypes.AccessControlDeclared => "surface-control-ownership",
            FactTypes.AccessEventBindingCandidate => "static-event-binding-candidate",
            // A command is a terminal effect of its procedure, not a traversable
            // association back to the owning surface. Keeping it out of the
            // graph prevents a false procedure -> surface -> procedure cycle;
            // the command fact remains available to composition/reporting.
            FactTypes.AccessCommandCandidate => null,
            FactTypes.AccessNavigationCandidate => "static-vba-call-navigation-candidate",
            FactTypes.AccessBindingDeclared => "declared-data-binding",
            FactTypes.AccessQueryDependencyCandidate => "static-query-dependency-candidate",
            FactTypes.AccessQueryOutputDeclared => "declared-query-output",
            FactTypes.AccessQueryOutputSourceCandidate => "static-query-output-source-candidate",
            FactTypes.AccessQueryActionLineageCandidate => "static-action-query-lineage",
            _ => null
        };
        if (kind is not null)
        {
            if (fact.FactType == FactTypes.AccessBindingDeclared
                && fact.Properties.GetValueOrDefault("targetKind") == "context"
                && string.IsNullOrWhiteSpace(fact.TargetSymbol))
            {
                // Host-provided values such as report Page/Pages are evidence about
                // rendering context, not a traversable data target.
                return null;
            }
            if (string.IsNullOrWhiteSpace(fact.SourceSymbol) || string.IsNullOrWhiteSpace(fact.TargetSymbol))
            {
                AddGap(gaps, maxGaps, ref truncated, Gap(
                    fact.FactType == FactTypes.AccessNavigationCandidate
                        ? "AccessFlowDynamicOrUnresolvedTarget"
                        : "AccessFlowTargetUnavailable",
                    "edge",
                    fact.FactId,
                    SafeCommit(fact.CommitSha)!,
                    fact));
                return null;
            }
            if (!SafeStableKey(fact.SourceSymbol) || !SafeStableKey(fact.TargetSymbol))
            {
                AddGap(gaps, maxGaps, ref truncated, Gap(
                    "AccessFlowUnsafeStableIdentity", "edge", null, SafeCommit(fact.CommitSha)!, fact));
                return null;
            }
            EnsureReferencedNode(nodes, fact.SourceSymbol!, fact, source: true);
            EnsureReferencedNode(nodes, fact.TargetSymbol!, fact, source: false);
            if (nodes[fact.TargetSymbol!].Declared == false)
                AddGap(gaps, maxGaps, ref truncated, Gap(
                    "AccessFlowTargetDeclarationMissing", "target", NodeId(fact.TargetSymbol), SafeCommit(fact.CommitSha)!, fact));
            return new(
                Id("edge", kind, fact.FactId),
                kind,
                NodeId(fact.SourceSymbol)!,
                NodeId(fact.TargetSymbol)!,
                Evidence(fact));
        }

        if (fact.FactType == FactTypes.AccessExternalLinkDeclared && SafeStableKey(fact.SourceSymbol))
        {
            EnsureReferencedNode(nodes, fact.SourceSymbol!, fact, source: true);
            var boundaryKey = $"access-boundary-{Id("key", fact.Properties.GetValueOrDefault("boundaryKind") ?? "unknown", fact.FactId)}";
            AddNode(nodes, boundaryKey, "external-boundary", fact.FactId);
            return new(
                Id("edge", "external-boundary", fact.FactId),
                "declared-external-boundary",
                NodeId(fact.SourceSymbol)!,
                NodeId(boundaryKey)!,
                Evidence(fact));
        }
        return null;
    }

    private static IReadOnlyList<AccessFlowPath> Traverse(
        IReadOnlyList<AccessFlowRoot> roots,
        IReadOnlyDictionary<string, AccessFlowNode> nodes,
        IReadOnlySet<string> partialNodeIds,
        IReadOnlyDictionary<string, AccessFlowEdge[]> outgoing,
        int maxDepth,
        int maxPaths,
        int maxGaps,
        string commitSha,
        List<AccessFlowGap> gaps,
        ref bool truncated)
    {
        var paths = new List<AccessFlowPath>();
        var edgeLookup = outgoing.Values.SelectMany(value => value)
            .ToDictionary(edge => edge.EdgeId, StringComparer.Ordinal);
        var queue = new Queue<PathState>(roots.Take(maxPaths).Select(root => new PathState(root, [root.NodeId], [])));
        var pathLimitReached = roots.Count > maxPaths;
        while (queue.Count > 0 && paths.Count < maxPaths)
        {
            var state = queue.Dequeue();
            var current = state.NodeIds[^1];
            var next = outgoing.GetValueOrDefault(current) ?? [];
            var terminal = next.Length == 0 || nodes[current].NodeKind is "field" or "report" or "external-boundary";
            if (terminal)
            {
                paths.Add(ToPath(state, nodes, terminalKind: nodes[current].NodeKind));
                continue;
            }
            if (state.EdgeIds.Count >= maxDepth)
            {
                truncated = true;
                AddGap(gaps, maxGaps, ref truncated, Gap("AccessFlowDepthLimitReached", "path", current, commitSha));
                paths.Add(ToPath(state, nodes, "depth-limit"));
                continue;
            }
            foreach (var edge in next)
            {
                if (paths.Count + queue.Count >= maxPaths)
                {
                    pathLimitReached = true;
                    break;
                }
                if (state.NodeIds.Contains(edge.ToNodeId, StringComparer.Ordinal))
                {
                    AddGap(gaps, maxGaps, ref truncated, Gap(
                        "AccessFlowCycleDetected", "path", edge.ToNodeId, commitSha, evidence: edge.Evidence));
                    paths.Add(ToPath(state with
                    {
                        NodeIds = state.NodeIds.Append(edge.ToNodeId).ToArray(),
                        EdgeIds = state.EdgeIds.Append(edge.EdgeId).ToArray()
                    }, nodes, "cycle"));
                    continue;
                }
                queue.Enqueue(state with
                {
                    NodeIds = state.NodeIds.Append(edge.ToNodeId).ToArray(),
                    EdgeIds = state.EdgeIds.Append(edge.EdgeId).ToArray()
                });
            }
        }
        if (queue.Count > 0 || pathLimitReached)
        {
            truncated = true;
            AddGap(gaps, maxGaps, ref truncated, Gap("AccessFlowPathLimitReached", "report", null, commitSha));
        }
        return paths.OrderBy(path => path.PathId, StringComparer.Ordinal).ToArray();

        AccessFlowPath ToPath(PathState state, IReadOnlyDictionary<string, AccessFlowNode> nodeLookup, string terminalKind)
        {
            var pathEdges = state.EdgeIds.Select(id => edgeLookup[id]).ToArray();
            var pathNodes = state.NodeIds.Select(id => nodeLookup[id]).ToArray();
            var evidence = pathEdges.Select(edge => edge.Evidence).Append(state.Root.Evidence).ToArray();
            var partial = evidence.Any(item => item.CoverageLabel is "partial" or "unknown")
                || state.NodeIds.Any(partialNodeIds.Contains)
                || terminalKind is "cycle" or "depth-limit";
            return new(
                Id("path", state.Root.RootId, string.Join('>', state.NodeIds), string.Join('>', state.EdgeIds)),
                state.Root.RootId,
                partial ? "PartialStaticCandidateTrail" : "StaticCandidateTrail",
                evidence.Select(item => item.EvidenceTier).OrderBy(TierRank).FirstOrDefault() ?? EvidenceTiers.Tier4Unknown,
                terminalKind,
                pathEdges.Length,
                pathNodes,
                pathEdges,
                evidence.Select(item => item.FactId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                evidence.Select(item => item.RuleId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                evidence.Select(item => item.EvidenceTier).Distinct(StringComparer.Ordinal).OrderBy(TierRank).ThenBy(value => value, StringComparer.Ordinal).ToArray(),
                evidence.Select(item => item.CoverageLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                evidence.SelectMany(item => item.Limitations).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
        }
    }

    private static AccessFlowEvidence Evidence(CodeFact fact) => new(
        fact.FactId,
        fact.RuleId,
        SafeEvidenceTier(fact.EvidenceTier),
        SafeCommit(fact.CommitSha) ?? throw new InvalidDataException("AccessFlowScanIdentityUnavailable"),
        SafeEvidencePath(fact.Evidence.FilePath),
        fact.Evidence.StartLine,
        fact.Evidence.EndLine,
        SafeToken(fact.Evidence.ExtractorId),
        SafeToken(fact.Evidence.ExtractorVersion),
        SafeCategory(
            fact.FactType == FactTypes.AccessBindingDeclared
                && !string.Equals(
                    fact.Properties.GetValueOrDefault("runtimeValueCoverage"),
                    "complete",
                    StringComparison.OrdinalIgnoreCase)
                ? "partial"
                : fact.Properties.GetValueOrDefault("coverageLabel"),
            "unknown"),
        SafeLimitations(fact.Properties.GetValueOrDefault("limitations")));

    private static IReadOnlyList<string> SupportingGapFactIds(
        CodeFact gap,
        IReadOnlyList<CodeFact> facts,
        GapSupportIndex supportIndex)
    {
        var supporting = new SortedSet<string>(StringComparer.Ordinal) { gap.FactId };
        if (gap.Properties.TryGetValue("supportingFactIds", out var persistedFactIds)
            && persistedFactIds is not null
            && persistedFactIds.Length <= MaxPersistedSupportCharacters
            && persistedFactIds.Count(character => character == ';') < MaxPersistedSupportIds)
        {
            var parsed = persistedFactIds.Split(';', StringSplitOptions.TrimEntries);
            var encodingValid = parsed.Length > 0
                && parsed.All(factId => !string.IsNullOrEmpty(factId) && SafeFactId(factId))
                && parsed.Distinct(StringComparer.Ordinal).Count() == parsed.Length;
            if (encodingValid)
            {
                var persistedScope = gap.Properties.GetValueOrDefault("scopeKind");
                bool valid;
                if (IsQueryOutputScope(persistedScope))
                {
                    var expected = CompleteQueryOutputSupport(gap, supportIndex);
                    valid = expected.Count > 0
                        && parsed.ToHashSet(StringComparer.Ordinal).SetEquals(expected);
                }
                else
                {
                    valid = parsed.All(factId => facts.Any(candidate => candidate.FactId == factId
                        && IsValidPersistedGapSupport(gap, candidate, facts)));
                }
                if (valid)
                {
                    supporting.UnionWith(parsed);
                    return supporting.ToArray();
                }
            }
        }
        if (!SafeStableKey(gap.TargetSymbol))
            return supporting.ToArray();

        var scope = gap.Properties.GetValueOrDefault("scopeKind");
        if (IsQueryOutputScope(scope))
        {
            supporting.UnionWith(CompleteQueryOutputSupport(gap, supportIndex));
        }
        else if (scope == "query"
                 && gap.Properties.GetValueOrDefault("classification")?.StartsWith(
                     "AccessQueryOutput",
                     StringComparison.Ordinal) == true)
        {
            if (supportIndex.Queries.TryGetValue((gap.ScanId, gap.TargetSymbol!), out var queries))
                supporting.UnionWith(queries.Select(query => query.FactId));
        }
        return supporting.ToArray();
    }

    private static bool IsQueryOutputScope(string? scope) =>
        scope is "query-output-field" or "query-output-field-owner-unknown";

    private static IReadOnlyList<string> CompleteQueryOutputSupport(CodeFact gap, GapSupportIndex supportIndex)
    {
        if (string.IsNullOrWhiteSpace(gap.TargetSymbol)
            || !supportIndex.Outputs.TryGetValue((gap.ScanId, gap.TargetSymbol), out var outputs))
            return [];
        var supporting = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var output in outputs)
        {
            supporting.Add(output.FactId);
            var ownerKey = !string.IsNullOrWhiteSpace(output.SourceSymbol)
                ? output.SourceSymbol
                : output.Properties.GetValueOrDefault("queryStableKey");
            if (SafeStableKey(ownerKey)
                && supportIndex.Queries.TryGetValue((gap.ScanId, ownerKey!), out var queries))
                supporting.UnionWith(queries.Select(query => query.FactId));
        }
        return supporting.ToArray();
    }

    private static GapSupportIndex BuildGapSupportIndex(IReadOnlyList<CodeFact> facts) => new(
        facts.Where(fact => fact.FactType == FactTypes.AccessQueryOutputDeclared
                && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
            .GroupBy(fact => (fact.ScanId, fact.TargetSymbol!))
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray()),
        facts.Where(fact => fact.FactType == FactTypes.AccessQueryDeclared
                && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
            .GroupBy(fact => (fact.ScanId, fact.TargetSymbol!))
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray()));

    private static bool IsValidPersistedGapSupport(CodeFact gap, CodeFact candidate, IReadOnlyList<CodeFact> facts) =>
        candidate.ScanId == gap.ScanId && gap.Properties.GetValueOrDefault("scopeKind") switch
        {
            "binding" => IsValidBindingGapSupport(gap, candidate, facts),
            "query" => candidate.FactType == FactTypes.AccessQueryDeclared
                && candidate.TargetSymbol == gap.TargetSymbol,
            _ => false
        };

    private static bool IsValidBindingGapSupport(CodeFact gap, CodeFact candidate, IReadOnlyList<CodeFact> facts)
    {
        var binding = facts.FirstOrDefault(fact => fact.ScanId == gap.ScanId
            && fact.FactType == FactTypes.AccessBindingDeclared
            && fact.Properties.GetValueOrDefault("stableBindingKey") == gap.TargetSymbol);
        if (binding is null)
            return false;
        if (candidate.FactId == binding.FactId)
            return true;
        return (binding.Properties.GetValueOrDefault("supportingFactIds") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(candidate.FactId, StringComparer.Ordinal);
    }

    private static void EnsureReferencedNode(IDictionary<string, MutableNode> nodes, string key, CodeFact fact, bool source)
    {
        if (nodes.TryGetValue(key, out var existing))
        {
            existing.SupportingFactIds.Add(fact.FactId);
            return;
        }
        var kind = source ? SourceKind(fact) : TargetKind(fact);
        nodes[key] = new(NodeId(key)!, kind, key, [fact.FactId], false);
    }

    private static void AddNode(IDictionary<string, MutableNode> nodes, string key, string kind, string factId)
    {
        if (nodes.TryGetValue(key, out var existing))
        {
            existing.Kind = existing.Declared ? existing.Kind : kind;
            existing.Declared = true;
            existing.SupportingFactIds.Add(factId);
            return;
        }
        nodes[key] = new(NodeId(key)!, kind, key, [factId], true);
    }

    private static string? NodeKind(string factType) => factType switch
    {
        FactTypes.AccessFormDeclared => "form",
        FactTypes.AccessReportDeclared => "report",
        FactTypes.AccessControlDeclared => "control",
        FactTypes.AccessVbaProcedureDeclared => "procedure",
        FactTypes.AccessQueryDeclared => "saved-query",
        FactTypes.AccessQueryOutputDeclared => "query-output-field",
        FactTypes.LegacyDataEntityDeclared => "table",
        FactTypes.LegacyDataColumnDeclared => "field",
        FactTypes.AccessMacroDeclared => "macro",
        _ => null
    };

    private static string SourceKind(CodeFact fact) => fact.FactType switch
    {
        FactTypes.AccessQueryDependencyCandidate => "saved-query",
        FactTypes.AccessNavigationCandidate => "procedure",
        _ => "access-object"
    };

    private static string TargetKind(CodeFact fact) =>
        fact.Properties.GetValueOrDefault("targetKind") switch
        {
            "query" or "saved-query" => "saved-query",
            "table" => "table",
            "field" => "field",
            "form" => "form",
            "report" => "report",
            "procedure" => "procedure",
            _ => "unresolved-target"
        };

    private static bool IsAccessFact(CodeFact fact) =>
        fact.FactId.StartsWith("fact-", StringComparison.Ordinal)
        && fact.FactId.Length <= 128
        && fact.FactId.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
        && fact.RuleId.StartsWith("legacy.access.", StringComparison.Ordinal)
        && fact.RuleId.Length <= 128
        && fact.RuleId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.');

    private static AccessFlowGap Gap(
        string classification,
        string scope,
        string? nodeId,
        string commitSha,
        CodeFact? fact = null,
        AccessFlowEvidence? evidence = null)
    {
        var factId = fact?.FactId ?? evidence?.FactId;
        return new(
            Id("gap", classification, nodeId ?? "global", factId ?? "none"),
            classification,
            scope,
            nodeId,
            RuleIds.LegacyAccessScreenDataFlow,
            EvidenceTiers.Tier4Unknown,
            fact is null ? evidence?.CommitSha ?? commitSha : SafeCommit(fact.CommitSha)!,
            fact is null ? evidence?.FilePath ?? "access-flow-report" : SafeEvidencePath(fact.Evidence.FilePath),
            fact is null ? evidence?.StartLine ?? 1 : fact.Evidence.StartLine,
            fact is null ? evidence?.EndLine ?? 1 : fact.Evidence.EndLine,
            fact is null ? evidence?.ExtractorId ?? "AccessScreenDataFlowReporter" : SafeToken(fact.Evidence.ExtractorId),
            fact is null ? evidence?.ExtractorVersion ?? SchemaVersion : SafeToken(fact.Evidence.ExtractorVersion),
            factId is null ? [] : [factId],
            ["static-composition-gap", "not-clean-absence", "no-runtime-conclusion"]);
    }

    private static void AddGap(List<AccessFlowGap> gaps, int maxGaps, ref bool truncated, AccessFlowGap gap)
    {
        if (gaps.Count < maxGaps) gaps.Add(gap);
        else
        {
            truncated = true;
            if (maxGaps > 0 && !gaps.Any(item => item.Classification == "AccessFlowGapLimitReached"))
                gaps[^1] = Gap("AccessFlowGapLimitReached", "report", null, gap.CommitSha);
        }
    }

    private static string? NodeId(string? stableKey) =>
        string.IsNullOrWhiteSpace(stableKey) ? null : Id("node", stableKey);
    private static string Id(params string[] parts) => $"access-flow-{FactFactory.Hash(string.Join('|', parts), 32)}";
    private static bool SafeStableKey(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 192
        && value.StartsWith("access-", StringComparison.Ordinal)
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    private static bool SafeFactId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    private static string SafeEvidencePath(string value)
    {
        var normalized = value.Replace('\\', '/');
        return !string.IsNullOrWhiteSpace(normalized)
            && !Path.IsPathFullyQualified(normalized)
            && !IsWindowsAbsolutePath(normalized)
            && !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..")
                ? normalized
                : "unavailable";
    }
    private static bool IsWindowsAbsolutePath(string value) =>
        value.StartsWith("//", StringComparison.Ordinal)
        || (value.Length >= 3
            && char.IsAsciiLetter(value[0])
            && value[1] == ':'
            && value[2] == '/');
    private static string SafeCategory(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            ? value
            : fallback;
    private static string SafeToken(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '/')
            ? value
            : "unknown";
    private static string? SafeCommit(string? value) =>
        value is { Length: 40 or 64 } && value.All(Uri.IsHexDigit)
            ? value.ToLowerInvariant()
            : null;
    private static string[] SafeLimitations(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => item.Length <= 128
                    && item.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.'))
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
    private static string SafeEvidenceTier(string? tier) => tier switch
    {
        EvidenceTiers.Tier1Semantic => EvidenceTiers.Tier1Semantic,
        EvidenceTiers.Tier2Structural => EvidenceTiers.Tier2Structural,
        EvidenceTiers.Tier3SyntaxOrTextual => EvidenceTiers.Tier3SyntaxOrTextual,
        EvidenceTiers.Tier4Unknown => EvidenceTiers.Tier4Unknown,
        _ => EvidenceTiers.Tier4Unknown
    };
    private static string RepositoryId(string repository) =>
        $"repo-{FactFactory.Hash($"access-flow-repository/v1\0{repository.Trim()}", 32)}";
    private static int TierRank(string tier) => tier switch
    {
        EvidenceTiers.Tier4Unknown => 0,
        EvidenceTiers.Tier3SyntaxOrTextual => 1,
        EvidenceTiers.Tier2Structural => 2,
        _ => 3
    };

    internal static async Task<(string Repository, string? CommitSha, IReadOnlyList<CodeFact> Facts)> ReadFactsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        }.ToString());
        await connection.OpenAsync(cancellationToken);
        string? repository;
        string? commitSha;
        await using (var manifest = connection.CreateCommand())
        {
            manifest.CommandText = "select repo, commit_sha from scan_manifest order by scanned_at desc limit 1;";
            await using var manifestReader = await manifest.ExecuteReaderAsync(cancellationToken);
            if (!await manifestReader.ReadAsync(cancellationToken))
                throw new InvalidDataException("AccessFlowScanIdentityUnavailable");
            repository = manifestReader.IsDBNull(0) ? null : manifestReader.GetString(0);
            commitSha = manifestReader.IsDBNull(1) ? null : manifestReader.GetString(1);
        }
        var facts = new List<CodeFact>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier,
                   source_symbol, target_symbol, contract_element, file_path, start_line, end_line,
                   snippet_hash, extractor_id, extractor_version, properties_json
            from facts
            where rule_id like 'legacy.access.%'
               or fact_type in ('LegacyDataEntityDeclared', 'LegacyDataColumnDeclared')
            order by fact_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var properties = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(17))
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
            facts.Add(new(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                new(
                    reader.GetString(11),
                    reader.GetInt32(12),
                    reader.GetInt32(13),
                    reader.IsDBNull(14) ? null : reader.GetString(14),
                    reader.GetString(15),
                    reader.GetString(16)),
                new SortedDictionary<string, string>(properties, StringComparer.Ordinal)));
        }
        return (repository ?? throw new InvalidDataException("AccessFlowScanIdentityUnavailable"), commitSha, facts);
    }

    private static string RenderMarkdown(AccessScreenDataFlowReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Microsoft Access Screen-to-Data Static Flow");
        builder.AppendLine();
        builder.AppendLine($"- Schema: `{report.SchemaVersion}`");
        builder.AppendLine($"- Repository: `{report.RepositoryId}`");
        builder.AppendLine($"- Commit: `{report.CommitSha}`");
        builder.AppendLine($"- Coverage: `{report.Coverage}`");
        builder.AppendLine($"- Roots: `{report.Summary.RootCount}`");
        builder.AppendLine($"- Paths: `{report.Summary.PathCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        builder.AppendLine();
        builder.AppendLine("## Candidate roots");
        builder.AppendLine();
        if (report.Roots.Count == 0) builder.AppendLine("- No supported candidate roots were available; see gaps.");
        foreach (var root in report.Roots)
        {
            builder.AppendLine($"- `{root.RootKind}` / `{root.NodeId}` — fact `{root.Evidence.FactId}`, rule `{root.Evidence.RuleId}`, tier `{root.Evidence.EvidenceTier}`, coverage `{root.Evidence.CoverageLabel}`, span `{root.Evidence.FilePath}:{root.Evidence.StartLine}-{root.Evidence.EndLine}`, extractor `{root.Evidence.ExtractorId}/{root.Evidence.ExtractorVersion}`.");
        }
        builder.AppendLine();
        builder.AppendLine("## Candidate paths");
        builder.AppendLine();
        if (report.Paths.Count == 0) builder.AppendLine("- No supported candidate path could be composed; this is not clean absence.");
        foreach (var path in report.Paths)
        {
            builder.AppendLine($"### `{path.PathId}`");
            builder.AppendLine();
            builder.AppendLine($"- Classification: `{path.Classification}`");
            builder.AppendLine($"- Weakest evidence tier: `{path.EvidenceTier}`");
            builder.AppendLine($"- Terminal: `{path.TerminalKind}`");
            builder.AppendLine($"- Nodes: {string.Join(" -> ", path.Nodes.Select(node => $"`{node.NodeKind}:{node.NodeId}`"))}");
            builder.AppendLine($"- Supporting facts: {string.Join(", ", path.SupportingFactIds.Select(id => $"`{id}`"))}");
            builder.AppendLine($"- Rules: {string.Join(", ", path.RuleIds.Select(id => $"`{id}`"))}");
            builder.AppendLine($"- Coverage: {string.Join(", ", path.CoverageLabels.Select(value => $"`{value}`"))}");
            builder.AppendLine($"- Limitations: {string.Join("; ", path.Limitations)}");
            builder.AppendLine("- Edges:");
            foreach (var edge in path.Edges)
                builder.AppendLine($"  - `{edge.EdgeKind}` `{edge.FromNodeId}` -> `{edge.ToNodeId}`; fact `{edge.Evidence.FactId}`, rule `{edge.Evidence.RuleId}`, tier `{edge.Evidence.EvidenceTier}`, coverage `{edge.Evidence.CoverageLabel}`, span `{edge.Evidence.FilePath}:{edge.Evidence.StartLine}-{edge.Evidence.EndLine}`, extractor `{edge.Evidence.ExtractorId}/{edge.Evidence.ExtractorVersion}`.");
            builder.AppendLine();
        }
        builder.AppendLine("## Gaps");
        builder.AppendLine();
        foreach (var gap in report.Gaps)
            builder.AppendLine($"- `{gap.Classification}` ({gap.ScopeKind}); rule `{gap.RuleId}`, tier `{gap.EvidenceTier}`, commit `{gap.CommitSha}`, span `{gap.FilePath}:{gap.StartLine}-{gap.EndLine}`, extractor `{gap.ExtractorId}/{gap.ExtractorVersion}`, supporting facts {string.Join(", ", gap.SupportingFactIds.Select(id => $"`{id}`"))}.");
        builder.AppendLine();
        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations) builder.AppendLine($"- {limitation}");
        return builder.ToString();
    }

    private static void Validate(AccessScreenDataFlowOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath) || !File.Exists(options.IndexPath))
            throw new InvalidDataException("AccessFlowIndexUnavailable");
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            throw new InvalidDataException("AccessFlowOutputRequired");
        if (options.MaxDepth is <= 0 or > 64 || options.MaxPaths is <= 0 or > 10_000 || options.MaxGaps is <= 0 or > 10_000)
            throw new InvalidDataException("AccessFlowBoundsInvalid");
        var index = Path.GetFullPath(options.IndexPath);
        var output = Path.GetFullPath(options.OutputDirectory);
        if (string.Equals(index, output, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("AccessFlowOutputInvalid");
    }

    private sealed record PathState(AccessFlowRoot Root, IReadOnlyList<string> NodeIds, IReadOnlyList<string> EdgeIds);
    private sealed class MutableNode(
        string nodeId,
        string kind,
        string stableKey,
        HashSet<string> supportingFactIds,
        bool declared)
    {
        public string NodeId { get; } = nodeId;
        public string Kind { get; set; } = kind;
        public string StableKey { get; } = stableKey;
        public HashSet<string> SupportingFactIds { get; } = supportingFactIds;
        public bool Declared { get; set; } = declared;
    }

    private sealed record GapSupportIndex(
        IReadOnlyDictionary<(string ScanId, string TargetSymbol), CodeFact[]> Outputs,
        IReadOnlyDictionary<(string ScanId, string TargetSymbol), CodeFact[]> Queries);
}
