using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record RetainedMethodGraph(IReadOnlyList<string> Roots, IReadOnlyList<CombinedPathNode> Nodes,
    IReadOnlyList<CombinedPathEdge> Edges, IReadOnlyList<RetainedMethodCall> Calls,
    IReadOnlyList<CombinedPathGap> Gaps, IReadOnlyList<string> Cutoffs, int Work, int MaxDepth, int MaxRecords, int MaxWork)
{
    public IReadOnlyList<RetainedCommandTrace> CommandTraces { get; init; } = [];
}
public sealed record RetainedCommandTrace(string EndpointNodeId, IReadOnlyList<string> PathNodeIds,
    CompiledCommandPathValueBinding? CommandText, IReadOnlyList<string> ProducerCallFactIds);
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
        var parents = new Dictionary<string, GraphEdge>(StringComparer.Ordinal);
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
                if (visited.Add(edge.ToNodeId))
                {
                    parents[edge.ToNodeId] = edge;
                    queue.Enqueue((edge.ToNodeId, current.Depth + 1));
                }
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
        var traces = new List<RetainedCommandTrace>();
        foreach (var endpoint in visited.Order(StringComparer.Ordinal).Where(id => graph.Nodes[id].CommandBinding is not null))
        {
            if (traces.Count >= 64) { cutoffs.Add("command-trace-limit"); break; }
            var pathIds = new List<string> { endpoint };
            var pathEdges = new List<CombinedPathEdge>();
            var cursor = endpoint;
            while (parents.TryGetValue(cursor, out var parent))
            {
                pathEdges.Add(parent.ToReportEdge()); cursor = parent.FromNodeId; pathIds.Add(cursor);
            }
            pathIds.Reverse(); pathEdges.Reverse();
            var pathNodes = pathIds.Select(id => graph.Nodes[id].ToReportNode()).ToArray();
            ProjectCompiledCommandValues(graph, pathNodes, pathEdges.ToArray());
            var text = pathNodes[^1].CommandBinding?.CommandTextFromPath;
            var producers = new List<string>();
            var expressions = new Queue<CompiledCommandPathValueBinding>();
            if (text is not null) expressions.Enqueue(text);
            for (var count = 0; count < MaxCommandExpressionNodes && expressions.TryDequeue(out var expression); count++)
            {
                producers.AddRange((expression.ReturnSteps ?? []).Select(step => step.ProducerCallFactId));
                if (expression.Origin.Kind == "call-result" && graph.CommandFactsByCombinedId.TryGetValue(expression.OriginBodyFactId, out var body))
                    producers.AddRange(graph.CommandRelatedFacts(body.SourceIndexId, FactTypes.ManagedIlCallObserved,
                        body.OriginalFactId + "/" + expression.Origin.Identity).Take(2).Select(f => f.CombinedFactId));
                foreach (var child in (expression.Alternatives ?? []).Concat(expression.Composition?.OperandBindings ?? []))
                    expressions.Enqueue(child);
            }
            traces.Add(new(endpoint, pathIds, text, producers.Distinct(StringComparer.Ordinal).ToArray()));
        }
        var relevantGaps = graph.Gaps.Where(g => g.NodeId is not null && visited.Contains(g.NodeId) ||
            g.CombinedFactId is not null && callIds.Contains(g.CombinedFactId)).Take(options.MaxFrontier + 1).ToArray();
        if (relevantGaps.Length > options.MaxFrontier) cutoffs.Add("gap-record-limit");
        return new(roots.Select(n => n.NodeId).ToArray(), visited.Order(StringComparer.Ordinal)
                .Select(id => SanitizeNode(graph.Nodes[id].ToReportNode())).ToArray(),
            edges.OrderBy(e => e.EdgeId, StringComparer.Ordinal).ToArray(), calls.OrderBy(c => c.FactId, StringComparer.Ordinal).ToArray(),
            relevantGaps.Take(options.MaxFrontier).Select(SanitizeGap).ToArray(), cutoffs.ToArray(), work, options.MaxDepth,
            options.MaxFrontier, options.MaxTraversalWork) { CommandTraces = traces };
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
            => MethodLabel(node.SymbolId ?? node.DisplayName);
        static string MethodLabel(string symbol)
        {
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
        html.Append("<h2>Command text routes</h2><p>One shortest retained witness per endpoint, not all variants. Static evidence only; no execution claim.</p>");
        foreach (var trace in graph.CommandTraces)
        {
            html.Append($"<h3>{E(string.Join(" → ", trace.PathNodeIds.Select(id => nodes.TryGetValue(id, out var n) ? Label(n) : id)))}</h3>");
            void AppendSymbolicInput(CompiledCommandPathValueBinding value)
            {
                if (value.SymbolicInput is { } input)
                    html.Append($"<p>Symbolic method input: {E(MethodLabel(input.MethodIdentity))}. Invocation evidence retained; return value not evaluated. Value uncertainty: {E(string.Join(", ", input.ValueGaps))}. No runtime value or branch feasibility claim.</p>");
            }
            if (trace.CommandText is { } commandText) AppendSymbolicInput(commandText);
            if (trace.CommandText?.Composition is { } composition)
            {
                static string OperandLabel(CompiledCommandOperandOrigin origin) => origin.Kind switch
                {
                    "constant-string-hash" => "constant (hash retained)",
                    "argument-slot" => "argument (IL slot " + origin.Identity + ")",
                    "null" => "null",
                    _ => origin.Kind + " (value unknown)"
                };
                html.Append($"<p>Supported symbolic expression: {E(composition.Operation)}({E(string.Join(", ", composition.Operands.Select(OperandLabel)))})</p><p>Limitation: plaintext value is not materialized. Parameter names are not retained; IL slots are not source names.</p><ol>");
                void AppendOperand(CompiledCommandPathValueBinding operand, int depth)
                {
                    if (depth > 4) { html.Append("<li>Expression display limit</li>"); return; }
                    html.Append($"<li>{E(OperandLabel(operand.Origin))} in {E(operand.OriginMethodIdentity is { } identity ? MethodLabel(identity) : "unknown method")}; {operand.Steps.Count} argument hops; state: {E(operand.State)}; gaps: {E(string.Join(", ", operand.Gaps))}");
                    AppendSymbolicInput(operand);
                    if (operand.Alternatives is { } choices)
                    {
                        html.Append("<p>Possible argument origins (branch feasibility not proven):</p><ul>");
                        foreach (var choice in choices.Take(4)) AppendOperand(choice, depth + 1);
                        html.Append("</ul>");
                    }
                    if (operand.Composition is { } nested)
                    {
                        html.Append($"<p>{E(nested.Operation)} (symbolic, no plaintext)</p><ol>");
                        foreach (var child in nested.OperandBindings.Take(3)) AppendOperand(child, depth + 1);
                        html.Append("</ol>");
                    }
                    html.Append("</li>");
                }
                foreach (var operand in composition.OperandBindings) AppendOperand(operand, 1);
                html.Append("</ol>");
            }
            html.Append($"<details><summary>Exact command evidence and operand traces</summary><pre>{E(JsonSerializer.Serialize(trace, options))}</pre></details>");
        }
        if (Encoding.UTF8.GetByteCount(html.ToString()) > maxBytes) throw new InvalidDataException("METHOD_GRAPH_OUTPUT_LIMIT");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, JsonName), bytes, token);
        await File.WriteAllTextAsync(Path.Combine(directory, HtmlName), html.ToString(), token);
    }
}
