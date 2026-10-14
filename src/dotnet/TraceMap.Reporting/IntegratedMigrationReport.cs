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
        var handles = facts.Where(f => f.FactType == FactTypes.VisualBasicEventBindingDeclared
            && f.RuleId == RuleIds.VisualBasicSyntaxEventWiring
            && f.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && f.Properties.GetValueOrDefault("wiringKind") == "Handles"
            && f.Properties.GetValueOrDefault("isAttach") == "True").ToArray();
        var types = facts.Where(f => f.FactType == FactTypes.TypeDeclared).ToArray();
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
        var rejectedCalls = new List<(CodeFact Call, string Reason)>();
        var calls = facts.Where(f => f.FactType == FactTypes.CallEdge).ToArray();
        var typeDeclarations = types.GroupBy(t => t.Properties.GetValueOrDefault("qualifiedName", ""), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        string? ReceiverType(Method from, string text)
        {
            if (text.StartsWith("Global.", StringComparison.OrdinalIgnoreCase)) return text[7..];
            var first = text.Split('.')[0];
            // An alias or generic parameter must not be reinterpreted as a nested class.
            if (new[] { "importAliasNames", "methodTypeParameters", "containingTypeParameters" }
                .Any(key => from.Fact.Properties.GetValueOrDefault(key, "").Split(';').Contains(first, StringComparer.OrdinalIgnoreCase))) return null;
            // Old scans do not carry alias/shadow guards: retain their old exact-only behavior.
            if (!from.Fact.Properties.ContainsKey("importAliasNames")) return text;
            var scope = from.Type;
            while (scope.Length > 0)
            {
                if (typeDeclarations.ContainsKey(scope))
                {
                    var candidate = scope + "." + text;
                    if (typeDeclarations.TryGetValue(candidate, out var count)) return count == 1 ? candidate : null;
                }
                var dot = scope.LastIndexOf('.');
                scope = dot < 0 ? "" : scope[..dot];
            }
            return text;
        }
        // Bound the Cartesian source-only matching work; never silently call it complete.
        var matchingLimited = methods.Length > 2000 || calls.Length > 10000
            || boundaries.Length + handles.Length > 10000 || types.Length > 2000;
        if (!matchingLimited)
        foreach (var call in calls)
        {
            var from = Owner(call);
            if (from is null) { unresolvedCalls++; rejectedCalls.Add((call, "call-owner-missing-or-ambiguous")); continue; }
            var targetId = call.Properties.GetValueOrDefault("targetSymbolId");
            var candidates = !string.IsNullOrEmpty(targetId)
                ? methods.Where(m => m.Fact.Properties.GetValueOrDefault("symbolId") == targetId)
                : Enumerable.Empty<Method>();
            var selected = candidates.Take(2).ToArray();
            if (selected.Length == 0 && call.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual)
            {
                var receiver = call.Properties.GetValueOrDefault("receiverName", "");
                var inlineReceiver = call.Properties.GetValueOrDefault("receiverTypeResolution") == "inline-object-creation-syntax";
                var type = !inlineReceiver && (receiver.Length == 0 || receiver.Equals("Me", StringComparison.OrdinalIgnoreCase))
                    ? from.Type : ReceiverType(from, call.Properties.GetValueOrDefault("receiverType", ""));
                var name = call.Properties.GetValueOrDefault("calleeName", "");
                var arity = Number(call, "argumentCount", -1);
                if (!string.IsNullOrEmpty(type) && arity >= 0)
                    selected = methods.Where(m => m.Type.Equals(type, StringComparison.OrdinalIgnoreCase)
                        && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && AcceptsArity(m, call, arity)).Take(2).ToArray();
            }
            if (selected.Length == 1) hops.Add(new(from, selected[0], call));
            else
            {
                unresolvedCalls++;
                rejectedCalls.Add((call, selected.Length > 1 ? "target-ambiguous" : "no-supported-target"));
            }
        }
        var roots = matchingLimited ? Array.Empty<(CodeFact Boundary, Method? Method)>()
            : boundaries.Select(b => (Boundary: b, Method: Owner(b))).Where(p => p.Method is not null).ToArray();
        // Keep Handles evidence at its original syntax tier. A unique lexical owner
        // and same-file type identity are required; framework ancestry is a separate claim.
        var syntaxRoots = new List<(CodeFact Boundary, Method? Method)>();
        var anchorReasons = new Dictionary<string, string>(StringComparer.Ordinal);
        var frameworkReasons = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var binding in handles)
        {
            if (matchingLimited) { anchorReasons[binding.FactId] = "matching-budget-exceeded-unassessed"; continue; }
            var owners = methods.Where(m => m.Fact.Evidence.FilePath == binding.Evidence.FilePath
                && m.Start <= binding.Evidence.StartLine && m.End >= binding.Evidence.EndLine).Take(2).ToArray();
            if (owners.Length != 1)
            {
                anchorReasons[binding.FactId] = owners.Length == 0 ? "method-owner-missing" : "method-owner-ambiguous";
                continue;
            }
            var owner = owners[0];
            if (!owner.Name.Equals(binding.Properties.GetValueOrDefault("handlerName"), StringComparison.OrdinalIgnoreCase))
            { anchorReasons[binding.FactId] = "handler-name-mismatch"; continue; }
            if (string.IsNullOrWhiteSpace(binding.Properties.GetValueOrDefault("receiverName")))
            { anchorReasons[binding.FactId] = "receiver-name-missing"; continue; }
            if (string.IsNullOrWhiteSpace(binding.Properties.GetValueOrDefault("eventName")))
            { anchorReasons[binding.FactId] = "event-name-missing"; continue; }
            if (string.IsNullOrWhiteSpace(owner.Type))
            { anchorReasons[binding.FactId] = "containing-type-identity-missing"; continue; }
            var declarations = types.Where(t => t.Evidence.FilePath == binding.Evidence.FilePath
                && t.Properties.GetValueOrDefault("qualifiedName", "").Equals(owner.Type, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if (declarations.Length != 1)
            {
                anchorReasons[binding.FactId] = declarations.Length == 0
                    ? "same-file-type-declaration-missing" : "same-file-type-declaration-ambiguous";
                continue;
            }
            var bases = declarations[0].Properties.GetValueOrDefault("baseTypes", "");
            var explicitUserControl = bases.Split(';')
                .Any(b => b.Equals("System.Web.UI.UserControl", StringComparison.OrdinalIgnoreCase)
                    || b.Equals("Global.System.Web.UI.UserControl", StringComparison.OrdinalIgnoreCase));
            frameworkReasons[binding.FactId] = explicitUserControl ? "explicit-usercontrol-base-syntax-candidate"
                : string.IsNullOrWhiteSpace(bases) ? "same-file-base-evidence-missing"
                : "explicit-usercontrol-base-not-established";
            anchorReasons[binding.FactId] = "candidate-anchor-established";
            syntaxRoots.Add((binding, owner));
        }
        var allRoots = roots.Concat(syntaxRoots).ToArray();
        var incoming = hops.GroupBy(h => h.To.Fact.FactId).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var anchorsByMethod = allRoots.GroupBy(r => r.Method!.Fact.FactId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Boundary).ToArray(), StringComparer.Ordinal);
        var rejectedByName = rejectedCalls.GroupBy(r => r.Call.Properties.GetValueOrDefault("calleeName", ""), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        var rows = operations.GroupBy(f => (f.Evidence.FilePath, f.Evidence.StartLine, f.Evidence.EndLine,
            Family: f.FactType == FactTypes.HttpCallDetected ? "outbound-http" : "database", f.SourceSymbol, f.TargetSymbol))
            .OrderBy(g => g.Key.FilePath, StringComparer.Ordinal).ThenBy(g => g.Key.StartLine)
            .ThenBy(g => g.Key.Family, StringComparer.Ordinal).ThenBy(g => g.Key.TargetSymbol, StringComparer.Ordinal).ToArray();
        lines.AddRange(["", "## Integrated migration inventory", "",
            $"Rule `{Rule}`. Scope: all supported operation facts in this scan, independent of selected query roots. {rows.Length} callsite evidence groups from {operations.Length} operation facts; {parameters.Length} parameter facts. These are not unique business operations or procedures.",
            $"Incoming WebForms: {bindings.Length} binding facts, {boundaries.Length} handler-resolution facts, {roots.Length} unique-span method attachments. Outbound HTTP is a separate operation family.",
            $"VB Handles: {handles.Length} syntax binding facts retained; {syntaxRoots.Count} observed-event candidate anchors. These are not necessarily WebForms entries and do not establish markup hosting, control identity or a deployed HTTP route.",
            $"Path matching: {(matchingLimited ? "limited: method/call budget exceeded or boundary/type limit reached; calls unassessed" : $"bounded; {unresolvedCalls} calls without a unique supported method target")}. Analysis gaps in scan: {facts.Count(f => f.FactType == FactTypes.AnalysisGap)}. Missing operations and unsupported providers remain unknown.",
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
            var stops = new Dictionary<string, (Method Node, List<Hop> Path, string Reason)>(StringComparer.Ordinal);
            if (owner is not null && !matchingLimited)
            {
                var queue = new Queue<(Method Node, List<Hop> Path, HashSet<string> Seen)>();
                queue.Enqueue((owner, [], new(StringComparer.Ordinal) { owner.Fact.FactId }));
                while (queue.Count > 0)
                {
                    if (++work > 4096) { limited = true; break; }
                    var item = queue.Dequeue();
                    foreach (var anchor in anchorsByMethod.GetValueOrDefault(item.Node.Fact.FactId) ?? [])
                    {
                        if (++work > 4096 || paths.Count >= 8) { limited = true; break; }
                        paths.Add((anchor, item.Path));
                    }
                    if (work > 4096 || paths.Count >= 8)
                    {
                        if (queue.Count > 0 || incoming.ContainsKey(item.Node.Fact.FactId)) limited = true;
                        break;
                    }
                    var callers = incoming.GetValueOrDefault(item.Node.Fact.FactId) ?? [];
                    if (!anchorsByMethod.ContainsKey(item.Node.Fact.FactId) && callers.Length == 0)
                        stops.TryAdd(item.Node.Fact.FactId, (item.Node, item.Path, "no-retained-incoming-call-or-anchor"));
                    if (item.Path.Count >= 16)
                    {
                        if (callers.Any(h => !item.Seen.Contains(h.From.Fact.FactId)))
                        {
                            limited = true;
                            stops.TryAdd(item.Node.Fact.FactId, (item.Node, item.Path, "depth-limit"));
                        }
                        continue;
                    }
                    if (callers.Length > 0 && callers.All(h => item.Seen.Contains(h.From.Fact.FactId))
                        && !anchorsByMethod.ContainsKey(item.Node.Fact.FactId))
                        stops.TryAdd(item.Node.Fact.FactId, (item.Node, item.Path, "cycle-boundary"));
                    foreach (var hop in callers)
                    {
                        if (++work > 4096) { limited = true; break; }
                        if (item.Seen.Contains(hop.From.Fact.FactId)) continue;
                        queue.Enqueue((hop.From, [hop, .. item.Path], new(item.Seen, StringComparer.Ordinal) { hop.From.Fact.FactId }));
                        if (queue.Count >= 4096) { limited = true; break; }
                    }
                    if (work > 4096) break;
                }
            }
            if (paths.Count > 0) connected++;
            lines.AddRange(["", $"### {group.Key.Family}: `{Safe(group.Key.FilePath)}:{group.Key.StartLine}`",
                $"Entry association: {(paths.Count == 0 ? "unresolved—not proof of unused code" : "candidate ordered source path")}; search limited: {limited || matchingLimited}.",
                "Operation evidence: " + string.Join("; ", group.Select(Reference))]);
            var associationReason = matchingLimited ? "matching-budget-exceeded-unassessed"
                : owner is null ? "operation-owner-missing-or-ambiguous"
                : paths.Count > 0 ? "candidate-path-retained"
                : limited ? "reverse-search-limit-reached"
                : !incoming.ContainsKey(owner.Fact.FactId) ? "no-supported-source-callers-or-local-anchor"
                : "no-retained-anchor-in-supported-caller-graph";
            lines.Add($"Operation path assessment: `{associationReason}`; reverse-search work: {work}. Missing call edges are not proof of unreachable code.");
            if (paths.Count == 0 || limited)
            {
                lines.Add($"Caller frontier diagnostics: {stops.Count} distinct observed stops; showing {Math.Min(20, stops.Count)}. Search incomplete: {limited || matchingLimited}. Same-name rejected calls below are investigation leads only, never joined edges; absence cannot distinguish unextracted calls from out-of-scope or unused code.");
                foreach (var stop in stops.Values.OrderBy(s => s.Node.Fact.FactId, StringComparer.Ordinal).Take(20))
                {
                    lines.Add($"- Caller frontier `{stop.Reason}`: {Reference(stop.Node.Fact)}; retained chain: "
                        + string.Join(" → ", stop.Path.Select(h => Reference(h.Call) + " ⇒ " + Reference(h.To.Fact)).Append(Reference(operation))));
                    var leads = rejectedByName.GetValueOrDefault(stop.Node.Name) ?? [];
                    lines.Add($"  Same-name rejected call leads: {leads.Length}; showing {Math.Min(5, leads.Length)} (receiver/arity may differ; not evidence of a call to this method).");
                    foreach (var lead in leads.Take(5))
                        lines.Add($"  - `{lead.Reason}`: {Reference(lead.Call)}; receiver type `{Safe(lead.Call.Properties.GetValueOrDefault("receiverType", "unavailable"))}`; argument count `{Number(lead.Call, "argumentCount", -1)}`.");
                }
            }
            foreach (var path in paths)
            {
                connectedBoundaries.Add(path.Boundary.FactId);
                var label = path.Boundary.FactType == FactTypes.VisualBasicEventBindingDeclared
                    ? $"VB Handles observed-event syntax candidate (hosting/route unresolved); framework assessment `{frameworkReasons[path.Boundary.FactId]}`"
                    : $"WebForms source `{Safe(path.Boundary.Properties.GetValueOrDefault("markupFile", "unknown"))}`; binding `{Safe(path.Boundary.Properties.GetValueOrDefault("bindingFactId", "unavailable"))}`";
                lines.Add($"- {label} → {Reference(path.Boundary)} → "
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
            $"{boundaries.Count(b => connectedBoundaries.Contains(b.FactId))}/{boundaries.Length} handler-resolution facts appear in displayed operation paths. Unconnected handlers can be UI-only, unsupported or outside current evidence; no automatic retirement decision follows.",
            $"{syntaxRoots.Count(r => connectedBoundaries.Contains(r.Boundary.FactId))}/{syntaxRoots.Count} VB Handles observed-event candidate anchors appear in displayed operation paths. Unattached or unsupported Handles facts: {handles.Length - syntaxRoots.Count}; no resolution is invented."]);
        lines.Add("Handles anchor assessment counts (all retained Handles bindings; first failing guard only):");
        foreach (var reason in anchorReasons.Values.GroupBy(r => r).OrderBy(g => g.Key, StringComparer.Ordinal))
            lines.Add($"- `{reason.Key}`: {reason.Count()}.");
        lines.Add("Framework assessment counts (candidate event anchors only; separate from chain connectivity):");
        foreach (var reason in frameworkReasons.Values.GroupBy(r => r).OrderBy(g => g.Key, StringComparer.Ordinal))
            lines.Add($"- `{reason.Key}`: {reason.Count()}.");
        lines.Add("Handles detail display cap: 100 unresolved paths. A candidate anchor with no displayed path is a downstream connectivity/search gap, not an anchor rejection. Missing base evidence may reside in an unjoined partial declaration; unsupported spelling is not proof of a non-UserControl type.");
        foreach (var binding in handles.Where(b => !connectedBoundaries.Contains(b.FactId)).Take(100))
            lines.Add("- Handles entry path unresolved: " + Reference(binding)
                + $"; anchor assessment `{anchorReasons[binding.FactId]}`; framework assessment `{frameworkReasons.GetValueOrDefault(binding.FactId, "unassessed")}`.");
        foreach (var boundary in boundaries.Where(b => !connectedBoundaries.Contains(b.FactId)).Take(100))
            lines.Add($"- Entry path unresolved: {Reference(boundary)}; source markup `{Safe(boundary.Properties.GetValueOrDefault("markupFile", "unknown"))}`.");
        var bindingIds = boundaries.Select(b => b.Properties.GetValueOrDefault("bindingFactId", "")).ToHashSet(StringComparer.Ordinal);
        var unbound = bindings.Where(b => !bindingIds.Contains(b.FactId)).ToArray();
        lines.Add($"Bindings without retained handler resolution: {unbound.Length}; showing at most 100. Handler display cap: 100. Remaining facts stay in the index.");
        foreach (var binding in unbound.Take(100)) lines.Add("- Unresolved event binding: " + Reference(binding));
    }

    private static bool AcceptsArity(Method method, CodeFact call, int arity)
    {
        if (method.Arity == arity) return true;
        if (arity < 0 || arity >= method.Arity || call.Properties.GetValueOrDefault("allArgumentsPositional") != "True"
            || method.Fact.Properties.GetValueOrDefault("hasParamArray") != "False") return false;
        var flags = method.Fact.Properties.GetValueOrDefault("parameterOptionalFlags", "").Split(';');
        return flags.Length == method.Arity && flags.Skip(arity).All(flag => flag == "optional");
    }

    private static int Number(CodeFact fact, string key, int fallback) => int.TryParse(fact.Properties.GetValueOrDefault(key), out var value) ? value : fallback;
    private static string Safe(string value) => value.Replace("`", "'").Replace("\r", " ").Replace("\n", " ");
    private static string Reference(CodeFact fact) => $"`{Safe(fact.FactId)}` ({fact.EvidenceTier}, `{Safe(fact.RuleId)}`, `{Safe(fact.Evidence.FilePath)}:{fact.Evidence.StartLine}`)";
}
