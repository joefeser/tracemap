using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record RetainedMethodGraph(IReadOnlyList<string> Roots, IReadOnlyList<CombinedPathNode> Nodes,
    IReadOnlyList<CombinedPathEdge> Edges, IReadOnlyList<RetainedMethodCall> Calls,
    IReadOnlyList<CombinedPathGap> Gaps, IReadOnlyList<string> Cutoffs, int Work, int MaxDepth, int MaxRecords, int MaxWork);
public sealed record RetainedMethodCall(string CallerNodeId, string FactId, string BodyFactId,
    string? Offset, string? Opcode, string? EncodedTarget, string RuleId, string EvidenceTier,
    string State, IReadOnlyList<string> GapReasons);

public static partial class CombinedDependencyPathReporter
{
    // Unique-node breadth-first traversal: aliases and recursion remain edges, never re-expanded paths.
    private static RetainedMethodGraph BuildMethodGraph(EvidenceGraph graph, CombinedReadResult read,
        IReadOnlyList<GraphNode> roots, CombinedDependencyPathOptions options)
    {
        var visited = roots.Select(n => n.NodeId).ToHashSet(StringComparer.Ordinal);
        var queue = new Queue<(string Id, int Depth)>(roots.Select(n => (n.NodeId, 0)));
        var edges = new List<CombinedPathEdge>();
        var cutoffs = new SortedSet<string>(StringComparer.Ordinal);
        var work = 0;
        while (queue.TryDequeue(out var current))
        {
            if (!graph.Outgoing.TryGetValue(current.Id, out var outgoing)) continue;
            foreach (var edge in outgoing.OrderBy(e => e.EdgeId, StringComparer.Ordinal))
            {
                if (++work > options.MaxTraversalWork) { cutoffs.Add("work-limit"); queue.Clear(); break; }
                if (current.Depth >= options.MaxDepth) { cutoffs.Add("depth-limit:" + current.Id); break; }
                if (edges.Count >= options.MaxFrontier) { cutoffs.Add("edge-limit"); queue.Clear(); break; }
                if (!visited.Contains(edge.ToNodeId) && visited.Count >= options.MaxFrontier)
                { cutoffs.Add("node-limit:" + current.Id); continue; }
                edges.Add(edge.ToReportEdge());
                if (visited.Add(edge.ToNodeId)) queue.Enqueue((edge.ToNodeId, current.Depth + 1));
            }
        }
        var calls = new List<RetainedMethodCall>();
        var facts = CombinedFactsByOriginalId(read.Facts);
        var gapsByFact = graph.Gaps.Where(g => g.CombinedFactId is not null).ToLookup(g => g.CombinedFactId!);
        foreach (var call in FactsOfTypes(read.Facts, FactTypes.ManagedIlCallObserved).OrderBy(c => c.CombinedFactId, StringComparer.Ordinal))
        {
            if (!TryUniqueFact(facts, call.SourceIndexId, call.Properties.GetValueOrDefault("ilBodyFactId"), out var body)
                || body.FactType != FactTypes.ManagedIlBodyDeclared
                || !TryUniqueFact(facts, call.SourceIndexId, body.Properties.GetValueOrDefault("compiledFactId"), out var caller)
                || caller.FactType != FactTypes.ManagedMethodDeclared || string.IsNullOrEmpty(caller.TargetSymbol)) continue;
            var id = SymbolNodeId(caller.SourceIndexId, caller.TargetSymbol);
            if (!visited.Contains(id)) continue;
            if (calls.Count >= options.MaxFrontier) { cutoffs.Add("call-record-limit"); break; }
            var admitted = graph.Outgoing.TryGetValue(id, out var outgoing) && outgoing.Any(e =>
                (e.EdgeKind is "compiled-il-call" or "compiled-il-callvirt-candidate") && e.SupportingFactIds.Contains(call.CombinedFactId));
            calls.Add(new(id, call.CombinedFactId, body.CombinedFactId, call.Properties.GetValueOrDefault("ilOffset"),
                call.Properties.GetValueOrDefault("opcode"), call.Properties.GetValueOrDefault("targetIdentity"),
                call.RuleId, call.EvidenceTier, admitted ? "retained-target-edge" : "no-admitted-method-target-edge",
                gapsByFact[call.CombinedFactId].Select(g => g.Reason ?? g.GapKind).Distinct().Order().ToArray()));
        }
        var callIds = calls.Select(c => c.FactId).ToHashSet(StringComparer.Ordinal);
        var relevantGaps = graph.Gaps.Where(g => g.NodeId is not null && visited.Contains(g.NodeId) ||
            g.CombinedFactId is not null && callIds.Contains(g.CombinedFactId)).Take(options.MaxFrontier + 1).ToArray();
        if (relevantGaps.Length > options.MaxFrontier) cutoffs.Add("gap-record-limit");
        return new(roots.Select(n => n.NodeId).ToArray(), visited.Order(StringComparer.Ordinal)
                .Select(id => SanitizeNode(graph.Nodes[id].ToReportNode())).ToArray(),
            edges.OrderBy(e => e.EdgeId, StringComparer.Ordinal).ToArray(), calls.OrderBy(c => c.FactId, StringComparer.Ordinal).ToArray(),
            relevantGaps.Take(options.MaxFrontier).Select(SanitizeGap).ToArray(), cutoffs.ToArray(), work, options.MaxDepth,
            options.MaxFrontier, options.MaxTraversalWork);
    }
}

public static class RetainedMethodGraphWriter
{
    public const string HtmlName = "method-graph.local.html";
    public const string JsonName = "method-graph.local.json";
    public static async Task WriteAsync(RetainedMethodGraph graph, string directory, string indexSha256,
        long maxBytes, CancellationToken token)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var generator = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(typeof(RetainedMethodGraphWriter).Assembly.Location, token)));
        var input = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { indexSha256, graph }, options)));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = "retained-method-graph.v1", ruleId = "combined.graph.retained-outgoing.v1",
            visibility = "local-only", generatorSha256 = generator, boundedInputSha256 = input, indexSha256,
            limitations = new[] { "Retained static evidence only; not runtime completeness. No terminal filtering. Missing target edges do not prove missing behavior.",
                "Unique nodes expanded once; repeated edges are cross-references, not proof of recursion. Graph admission and output bounds remain in force.",
                "Encoded call targets are observations, not resolved dispatch. Call records require unique retained body/caller joins; missing or ambiguous joins remain outside this projection. No raw SQL or source snippets are exported." }, graph }, options);
        if (bytes.LongLength > maxBytes) throw new InvalidDataException("METHOD_GRAPH_OUTPUT_LIMIT");
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "unavailable");
        static string Label(CombinedPathNode node)
        {
            var symbol = node.SymbolId ?? node.DisplayName;
            var member = System.Text.RegularExpressions.Regex.Match(symbol, @"\|(?:method|constructor):\d+:([^|]+)\|");
            if (!member.Success) return symbol;
            var types = System.Text.RegularExpressions.Regex.Matches(symbol[..member.Index], @"\|names:\d+:([^|]+)\|");
            return (types.Count > 0 ? types[^1].Groups[1].Value + "." : "") + member.Groups[1].Value;
        }
        var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Retained method graph</title><style>body{font:15px system-ui;max-width:1200px;margin:2em auto}pre{white-space:pre-wrap;overflow-wrap:anywhere}details{margin:1em;padding:.6em;border:1px solid #bbb}li{margin:.5em}a{overflow-wrap:anywhere}</style><h1>Unfiltered retained method graph</h1><p>PRIVATE — static evidence, not runtime completeness. No database-terminal pruning. Each node appears once; links preserve cycles and shared callees. Use browser Find for a method or encoded target.</p>");
        html.Append($"<p>{graph.Nodes.Count} nodes; {graph.Edges.Count} edges; {graph.Calls.Count} encoded calls; {graph.Work} work units; depth bound {graph.MaxDepth}.</p><pre>Cutoffs: {E(string.Join("\n", graph.Cutoffs))}</pre><h2>Roots</h2>");
        var ids = graph.Nodes.Select((n, i) => (n.NodeId, Anchor: "n" + i)).ToDictionary(x => x.NodeId, x => x.Anchor);
        var nodes = graph.Nodes.ToDictionary(n => n.NodeId);
        var outgoing = graph.Edges.ToLookup(e => e.FromNodeId);
        var callsByNode = graph.Calls.ToLookup(c => c.CallerNodeId);
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        void Tree(string id, int depth)
        {
            html.Append($"<li><a href='#{ids[id]}'>{E(Label(nodes[id]))}</a>");
            if (!expanded.Add(id)) { html.Append(" — back-reference</li>"); return; }
            if (depth >= 64) { html.Append(" — display depth bound; follow node link</li>"); return; }
            html.Append("<details><summary>Calls and candidate connections</summary><ul>");
            foreach (var edge in outgoing[id]) { html.Append($"<li>{E(edge.EdgeKind)}</li>"); Tree(edge.ToNodeId, depth + 1); }
            foreach (var call in callsByNode[id].Where(c => c.State != "retained-target-edge"))
                html.Append($"<li>UNRESOLVED METHOD TARGET: {E(call.EncodedTarget)} — {E(call.Opcode)} at IL {E(call.Offset)}</li>");
            html.Append("</ul></details></li>");
        }
        foreach (var root in graph.Roots) html.Append($"<p><a href='#{ids[root]}'>{E(Label(nodes[root]))}</a></p>");
        html.Append("<h2>Call graph tree</h2><ul>");
        foreach (var root in graph.Roots) Tree(root, 0);
        html.Append("</ul>");
        foreach (var node in graph.Nodes)
        {
            html.Append($"<section id='{ids[node.NodeId]}'><h2>{E(Label(node))}</h2><p>{E(node.FilePath)}:{node.StartLine}</p><details><summary>Exact identity and retained metadata</summary><pre>{E(JsonSerializer.Serialize(node, options))}</pre></details><h3>Outgoing connections</h3><ul>");
            foreach (var edge in outgoing[node.NodeId])
                html.Append($"<li>{E(edge.EdgeKind)} → <a href='#{ids[edge.ToNodeId]}'>{E(Label(nodes[edge.ToNodeId]))}</a><details><summary>Evidence</summary><pre>{E(JsonSerializer.Serialize(edge, options))}</pre></details></li>");
            html.Append("</ul><details open><summary>Encoded calls — including unresolved targets</summary>");
            foreach (var call in callsByNode[node.NodeId])
                html.Append($"<pre>{E(JsonSerializer.Serialize(call, options))}</pre>");
            html.Append("</details></section>");
        }
        html.Append($"<details><summary>Retained gaps</summary><pre>{E(JsonSerializer.Serialize(graph.Gaps, options))}</pre></details>");
        if (Encoding.UTF8.GetByteCount(html.ToString()) > maxBytes) throw new InvalidDataException("METHOD_GRAPH_OUTPUT_LIMIT");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, JsonName), bytes, token);
        await File.WriteAllTextAsync(Path.Combine(directory, HtmlName), html.ToString(), token);
    }
}
