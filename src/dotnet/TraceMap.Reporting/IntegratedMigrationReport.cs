using TraceMap.Core;

namespace TraceMap.Reporting;

/// <summary>Human-readable projection of one scan, never a runtime or contract verdict.</summary>
public static class IntegratedMigrationReport
{
    public const string Rule = "workflow.migration.scan-inventory.v1";
    private sealed record Method(CodeFact Fact, string Type, string Name, int Arity, int Start, int End);
    private sealed record Hop(Method From, Method To, CodeFact Call);

    public static void Append(List<string> lines, ScanResult scan)
    {
        var facts = scan.Facts.OrderBy(f => f.FactId, StringComparer.Ordinal).ToArray();
        var operations = facts.Where(f => f.FactType is FactTypes.DatabaseOperationCandidate
            or FactTypes.SqlCommandDetected or FactTypes.DapperCallDetected or FactTypes.DbChangeSaved
            or FactTypes.HttpCallDetected).ToArray();
        var boundaries = facts.Where(f => f.FactType == FactTypes.WebFormsHandlerResolved).ToArray();
        var bindings = facts.Where(f => f.FactType == FactTypes.WebFormsEventBindingDeclared).ToArray();
        var parameters = facts.Where(f => f.FactType == FactTypes.DatabaseParameterCandidate).ToArray();
        var methods = facts.Where(f => f.FactType == FactTypes.MethodDeclared).Select(f => new Method(f,
            f.Properties.GetValueOrDefault("qualifiedContainingType", ""),
            f.Properties.GetValueOrDefault("name", f.TargetSymbol ?? ""), Number(f, "parameterCount", -1),
            Number(f, "bodyStartLine", f.Evidence.StartLine), Number(f, "bodyEndLine", f.Evidence.EndLine))).ToArray();
        Method? Owner(CodeFact fact)
        {
            var matches = methods.Where(m => m.Fact.Evidence.FilePath == fact.Evidence.FilePath
                && m.Start <= fact.Evidence.StartLine && m.End >= fact.Evidence.EndLine).Take(2).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        var hops = new List<Hop>();
        var unresolvedCalls = 0;
        var calls = facts.Where(f => f.FactType == FactTypes.CallEdge).ToArray();
        // Bound the Cartesian source-only matching work; never silently call it complete.
        var matchingLimited = methods.Length > 2000 || calls.Length > 10000 || boundaries.Length > 10000;
        if (!matchingLimited)
        foreach (var call in calls)
        {
            var from = Owner(call);
            if (from is null) { unresolvedCalls++; continue; }
            var targetId = call.Properties.GetValueOrDefault("targetSymbolId");
            var candidates = !string.IsNullOrEmpty(targetId)
                ? methods.Where(m => m.Fact.Properties.GetValueOrDefault("symbolId") == targetId)
                : Enumerable.Empty<Method>();
            var selected = candidates.Take(2).ToArray();
            if (selected.Length == 0 && call.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual)
            {
                var receiver = call.Properties.GetValueOrDefault("receiverName", "");
                var type = receiver is "" or "Me" ? from.Type : call.Properties.GetValueOrDefault("receiverType", "");
                var name = call.Properties.GetValueOrDefault("calleeName", "");
                var arity = Number(call, "argumentCount", -1);
                if (type.Length > 0 && arity >= 0)
                    selected = methods.Where(m => m.Type.Equals(type, StringComparison.OrdinalIgnoreCase)
                        && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && m.Arity == arity).Take(2).ToArray();
            }
            if (selected.Length == 1) hops.Add(new(from, selected[0], call));
            else unresolvedCalls++;
        }
        var roots = matchingLimited ? Array.Empty<(CodeFact Boundary, Method? Method)>()
            : boundaries.Select(b => (Boundary: b, Method: Owner(b))).Where(p => p.Method is not null).ToArray();
        var outgoing = hops.GroupBy(h => h.From.Fact.FactId).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var rows = operations.GroupBy(f => (f.Evidence.FilePath, f.Evidence.StartLine, f.Evidence.EndLine,
            Family: f.FactType == FactTypes.HttpCallDetected ? "outbound-http" : "database", f.SourceSymbol, f.TargetSymbol))
            .OrderBy(g => g.Key.FilePath, StringComparer.Ordinal).ThenBy(g => g.Key.StartLine)
            .ThenBy(g => g.Key.Family, StringComparer.Ordinal).ThenBy(g => g.Key.TargetSymbol, StringComparer.Ordinal).ToArray();
        lines.AddRange(["", "## Integrated migration inventory", "",
            $"Rule `{Rule}`. Scope: all supported operation facts in this scan, independent of selected query roots. {rows.Length} callsite evidence groups from {operations.Length} operation facts; {parameters.Length} parameter facts. These are not unique business operations or procedures.",
            $"Incoming WebForms: {bindings.Length} binding facts, {boundaries.Length} handler-resolution facts, {roots.Length} unique-span method attachments. Outbound HTTP is a separate operation family.",
            $"Path matching: {(matchingLimited ? "limited: method/call budget exceeded or boundary limit reached" : "bounded")}; {unresolvedCalls} calls without a unique supported method target. Analysis gaps in scan: {facts.Count(f => f.FactType == FactTypes.AnalysisGap)}. Missing operations and unsupported providers remain unknown.",
            "Every ordered source path below is a static candidate, not a URL, runtime reachability, compiler identity bridge or verified API contract. WebForms markup identifies a source surface, not a deployed route. No name-only cross-type or ambiguous-overload joins. Compiled wrapper paths remain in the separate admitted compiled-path report; this source projection does not splice them into its paths.",
            $"Showing {Math.Min(200, rows.Length)} of {rows.Length} callsite groups. Parameter display cap: 50 per group; path cap: 8, depth: 16, search work: 4096 per group. Any limit is reported; omitted evidence remains in scan facts."]);
        var connected = 0;
        var associated = new HashSet<string>(StringComparer.Ordinal);
        var connectedBoundaries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in rows.Take(200))
        {
            var operation = group.First();
            var owner = matchingLimited ? null : Owner(operation);
            var paths = new List<(CodeFact Boundary, List<Hop> Hops)>();
            var work = 0;
            var limited = false;
            if (owner is not null && !matchingLimited)
            foreach (var root in roots)
            {
                var queue = new Queue<(Method Node, List<Hop> Path, HashSet<string> Seen)>();
                queue.Enqueue((root.Method!, [], new(StringComparer.Ordinal) { root.Method!.Fact.FactId }));
                while (queue.Count > 0)
                {
                    if (++work > 4096 || paths.Count >= 8) { limited = true; break; }
                    var item = queue.Dequeue();
                    if (item.Node == owner) { paths.Add((root.Boundary, item.Path)); continue; }
                    if (item.Path.Count >= 16) { limited = true; continue; }
                    foreach (var hop in outgoing.GetValueOrDefault(item.Node.Fact.FactId) ?? [])
                    {
                        if (++work > 4096) { limited = true; break; }
                        if (item.Seen.Contains(hop.To.Fact.FactId)) continue;
                        queue.Enqueue((hop.To, [.. item.Path, hop], new(item.Seen, StringComparer.Ordinal) { hop.To.Fact.FactId }));
                        if (queue.Count >= 4096) { limited = true; break; }
                    }
                }
                if (limited) break;
            }
            if (paths.Count > 0) connected++;
            lines.AddRange(["", $"### {group.Key.Family}: `{Safe(group.Key.FilePath)}:{group.Key.StartLine}`",
                $"Entry association: {(paths.Count == 0 ? "unresolved—not proof of unused code" : "candidate ordered source path")}; search limited: {limited || matchingLimited}.",
                "Operation evidence: " + string.Join("; ", group.Select(Reference))]);
            foreach (var path in paths)
            {
                connectedBoundaries.Add(path.Boundary.FactId);
                lines.Add($"- WebForms source `{Safe(path.Boundary.Properties.GetValueOrDefault("markupFile", "unknown"))}`; binding `{Safe(path.Boundary.Properties.GetValueOrDefault("bindingFactId", "unavailable"))}` → {Reference(path.Boundary)} → "
                    + string.Join(" → ", path.Hops.Select(h => Reference(h.Call) + " ⇒ declaration " + Reference(h.To.Fact)).Append(Reference(operation))));
            }
            var uniqueOperationLine = rows.Count(r => r.Key.FilePath == group.Key.FilePath
                && r.Key.StartLine == group.Key.StartLine && r.Key.SourceSymbol == group.Key.SourceSymbol) == 1;
            var related = parameters.Where(p => uniqueOperationLine && p.Evidence.FilePath == operation.Evidence.FilePath
                && p.SourceSymbol == operation.SourceSymbol && !string.IsNullOrEmpty(p.SourceSymbol)
                && Number(p, "laterExecutionSyntaxStartLine", -1) == operation.Evidence.StartLine).ToArray();
            lines.Add($"Parameter witnesses: {related.Length} (same source member/file and unique observed execution line; lexical association only; ambiguous operation lines are refused). Showing {Math.Min(50, related.Length)}.");
            foreach (var parameter in related.Take(50))
            {
                associated.Add(parameter.FactId);
                lines.Add($"- {Reference(parameter)}; type `{Safe(parameter.Properties.GetValueOrDefault("parameterTypeName", "unknown"))}`; value observations `{Number(parameter, "parameterValueObservationCount", 0)}`; text trace `{Safe(parameter.Properties.GetValueOrDefault("commandTextTraceEvidence", "unavailable"))}`; execution `{Safe(parameter.Properties.GetValueOrDefault("commandExecutionEvidence", "unresolved"))}`.");
            }
            lines.Add("Review pending: [ ] entry/route identity; [ ] current-build identity and path; [ ] command/destination contract; [ ] parameter/request/result semantics; [ ] transaction/auth/error behavior; [ ] owner retain/replace/retire decision and API acceptance tests.");
        }
        lines.Add($"Displayed groups with candidate entry paths: {connected}/{Math.Min(200, rows.Length)}. Parameter facts not displayed in a group: {parameters.Length - associated.Count}; these remain independently listed, not discarded. Zero matches never means zero application behavior.");
        lines.AddRange(["", "### Incoming boundary coverage", "",
            $"{connectedBoundaries.Count}/{boundaries.Length} handler-resolution facts appear in displayed operation paths. Unconnected handlers can be UI-only, unsupported or outside current evidence; no automatic retirement decision follows."]);
        foreach (var boundary in boundaries.Where(b => !connectedBoundaries.Contains(b.FactId)).Take(100))
            lines.Add($"- Entry path unresolved: {Reference(boundary)}; source markup `{Safe(boundary.Properties.GetValueOrDefault("markupFile", "unknown"))}`.");
        var bindingIds = boundaries.Select(b => b.Properties.GetValueOrDefault("bindingFactId", "")).ToHashSet(StringComparer.Ordinal);
        var unbound = bindings.Where(b => !bindingIds.Contains(b.FactId)).ToArray();
        lines.Add($"Bindings without retained handler resolution: {unbound.Length}; showing at most 100. Handler display cap: 100. Remaining facts stay in the index.");
        foreach (var binding in unbound.Take(100)) lines.Add("- Unresolved event binding: " + Reference(binding));
    }

    private static int Number(CodeFact fact, string key, int fallback) => int.TryParse(fact.Properties.GetValueOrDefault(key), out var value) ? value : fallback;
    private static string Safe(string value) => value.Replace("`", "'").Replace("\r", " ").Replace("\n", " ");
    private static string Reference(CodeFact fact) => $"`{Safe(fact.FactId)}` ({fact.EvidenceTier}, `{Safe(fact.RuleId)}`, `{Safe(fact.Evidence.FilePath)}:{fact.Evidence.StartLine}`)";
}
