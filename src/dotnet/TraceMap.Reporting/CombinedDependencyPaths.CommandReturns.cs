using System.Globalization;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    private const int MaxCommandProducerEdges = 256;
    private static string? CommandFactReference(CombinedFactRow fact) => fact.FactType switch
    {
        FactTypes.ManagedIlBodyDeclared => fact.Properties.GetValueOrDefault("compiledFactId"),
        FactTypes.ManagedIlReturnValuesObserved => fact.Properties.GetValueOrDefault("ilBodyFactId"),
        FactTypes.ManagedIlCallObserved when fact.Properties.TryGetValue("ilBodyFactId", out var body)
            && fact.Properties.TryGetValue("ilOffset", out var offset) => body + "/" + offset,
        _ => null
    };

    private sealed record RetainedReturn(long Offset, string State, CompiledCommandOperandOrigin Origin);

    private sealed class CommandReturnResolver(EvidenceGraph graph, List<object> materials,
        SortedSet<string> gaps, List<CompiledCommandReturnStep> steps, string constantKind)
    {
        private readonly HashSet<string> active = new(StringComparer.Ordinal);
        public int Work { get; set; }

        public bool Resolve(CompiledCommandOperandOrigin origin, string scope, string? method,
            out CompiledCommandOperandOrigin resolved, out string resolvedScope, out string? resolvedMethod)
        {
            resolved = origin; resolvedScope = scope; resolvedMethod = method;
            if (origin.Kind != "call-result") return true;
            if (++Work > MaxCompiledCommandValueHops) return Fail("IlCommandReturnWorkLimit");
            var key = scope + "/" + origin.Identity;
            if (!active.Add(key)) return Fail("IlCommandReturnCycle");
            try
            {
                var facts = graph.CommandFactsByCombinedId;
                if (!facts.TryGetValue(scope, out var callerBody) || callerBody.FactType != FactTypes.ManagedIlBodyDeclared
                    || method is null || !facts.TryGetValue(method, out var callerMethod) || callerMethod.TargetSymbol is null)
                    return Fail("IlCommandReturnCallerUnavailable");
                materials.Add(ProjectCommandValueFact(callerBody));
                materials.Add(ProjectCommandValueFact(callerMethod));
                var calls = Related(callerBody.SourceIndexId, FactTypes.ManagedIlCallObserved, callerBody.OriginalFactId + "/" + origin.Identity);
                if (calls.Count != 1) return Fail("IlCommandReturnProducerMissingOrAmbiguous");
                var producer = calls[0];
                if (!graph.Outgoing.TryGetValue(SymbolNodeId(callerMethod.SourceIndexId, callerMethod.TargetSymbol), out var outgoing))
                    return Fail("IlCommandReturnTargetUnavailable");
                materials.Add(new { ProducerNode = SymbolNodeId(callerMethod.SourceIndexId, callerMethod.TargetSymbol), ProducerEdgeCount = outgoing.Count });
                if (outgoing.Count > MaxCommandProducerEdges) return Fail("IlCommandReturnEdgeLimit");
                var candidates = outgoing.Where(edge => edge.SupportingFactIds.Contains(producer.CombinedFactId, StringComparer.Ordinal)
                    && edge.EdgeKind is "compiled-il-call" or "compiled-il-callvirt-candidate").Take(2).ToArray();
                foreach (var edge in candidates) materials.Add(edge.ToReportEdge());
                if (candidates.Length != 1) return Fail("IlCommandReturnTargetMissingOrAmbiguous");
                var selected = candidates[0].ToReportEdge();
                var targets = selected.SupportingFactIds.Select(id => facts.GetValueOrDefault(id)).OfType<CombinedFactRow>()
                    .Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol is not null
                        && SymbolNodeId(fact.SourceIndexId, fact.TargetSymbol) == selected.ToNodeId).Take(2).ToArray();
                foreach (var target in targets) materials.Add(ProjectCommandValueFact(target));
                if (targets.Length != 1) return Fail("IlCommandReturnTargetMissingOrAmbiguous");
                var agreed = TryCommandCallOperands(graph, facts, graph.CommandFactsByOriginalId, selected, targets[0].CombinedFactId,
                    out var call, out var operands, out var body, out var caller, out var callee,
                    out var receiver, out _, out var arguments, out var hasThis, out var inspected, out var reason);
                foreach (var fact in new[] { call, body, caller, callee }.OfType<CombinedFactRow>()) materials.Add(ProjectCommandValueFact(fact));
                foreach (var fact in inspected) materials.Add(ProjectCommandValueFact(fact));
                if (!agreed || body!.CombinedFactId != scope || caller!.CombinedFactId != method)
                    return Fail(agreed ? "IlCommandReturnCallerMismatch" : reason);
                // Do not select an override from a mere encoded virtual target.
                if (selected.EdgeKind == "compiled-il-callvirt-candidate") return Fail("IlCommandReturnVirtualDispatchUnproven");
                var returnType = constantKind == "constant-string-hash" ? "6:String" : "5:Int32";
                if (callee!.Properties.GetValueOrDefault("signature")?.EndsWith(
                    ")->type(namespace:6:System|names:" + returnType + ")", StringComparison.Ordinal) != true)
                    return Fail("IlCommandReturnSignatureUnsupported");
                var bodies = Related(callee.SourceIndexId, FactTypes.ManagedIlBodyDeclared, callee.OriginalFactId);
                if (bodies.Count != 1) return Fail("IlCommandReturnBodyMissingOrAmbiguous");
                var targetBody = bodies[0];
                var summaries = Related(callee.SourceIndexId, FactTypes.ManagedIlReturnValuesObserved, targetBody.OriginalFactId);
                if (summaries.Count != 1) return Fail("IlCommandReturnEvidenceMissingOrAmbiguous");
                var summary = summaries[0];
                if (targetBody.Properties.GetValueOrDefault("rawFileSha256") != callee.Properties.GetValueOrDefault("rawFileSha256")
                    || summary.RuleId != RuleIds.DotNetIlValues || summary.EvidenceTier != EvidenceTiers.Tier3SyntaxOrTextual
                    || summary.Properties.GetValueOrDefault("valueSchema") != "il-return-values.v1"
                    || summary.Properties.GetValueOrDefault("valueState") != "return-operands-candidate"
                    || new[] { "rawFileSha256", "ilGeneratorSha256", "ilBoundedInputSha256" }.Any(property =>
                        string.IsNullOrEmpty(targetBody.Properties.GetValueOrDefault(property))
                        || summary.Properties.GetValueOrDefault(property) != targetBody.Properties.GetValueOrDefault(property)))
                    return Fail("IlCommandReturnProvenanceUnavailable");
                var encoded = summary.Properties.GetValueOrDefault("returnOrigins");
                if (encoded is null || encoded.Length > 64 * 1024) return Fail("IlCommandReturnEvidenceMalformed");
                RetainedReturn[]? returns;
                try { returns = JsonSerializer.Deserialize<RetainedReturn[]>(encoded); }
                catch (JsonException) { return Fail("IlCommandReturnEvidenceMalformed"); }
                if (returns is not { Length: > 0 and <= 256 }
                    || !int.TryParse(summary.Properties.GetValueOrDefault("returnCount"), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                    || count != returns.Length || returns.Select(value => value?.Offset).Distinct().Count() != count
                    || returns.Any(value => value is null || value.Offset < 0 || value.State != "return-operand-candidate"
                        || value.Origin is null || !ValidCompiledCommandOrigin(value.Origin)))
                    return Fail("IlCommandReturnEvidenceUnavailable");
                steps.Add(new(producer.CombinedFactId, targetBody.CombinedFactId, summary.CombinedFactId,
                    summary.Properties["ilGeneratorSha256"], summary.Properties["ilBoundedInputSha256"]));
                (CompiledCommandOperandOrigin Origin, string Scope, string? Method)? common = null;
                foreach (var returned in returns)
                {
                    if (++Work > MaxCompiledCommandValueHops) return Fail("IlCommandReturnWorkLimit");
                    if (!Resolve(returned.Origin, targetBody.CombinedFactId, callee.CombinedFactId,
                        out var value, out var valueScope, out var valueMethod)) return false;
                    if (value.Kind == "argument-slot" && valueScope == targetBody.CombinedFactId)
                    {
                        if (!int.TryParse(value.Identity, NumberStyles.None, CultureInfo.InvariantCulture, out var slot)
                            || slot < 0 || slot - (hasThis ? 1 : 0) >= arguments!.Length)
                            return Fail("IlCommandReturnArgumentUnavailable");
                        value = hasThis && slot == 0 ? receiver! : arguments![slot - (hasThis ? 1 : 0)];
                        if (!Resolve(value, scope, method, out value, out valueScope, out valueMethod)) return false;
                    }
                    if (value.Kind != constantKind && value.Kind != "argument-slot") return Fail("IlCommandReturnValueUnresolved");
                    if (common is { } prior && (prior.Origin != value || value.Kind == "argument-slot"
                        && (prior.Scope != valueScope || prior.Method != valueMethod))) return Fail("IlCommandReturnValuesDisagree");
                    common = (value, valueScope, valueMethod);
                }
                resolved = common!.Value.Origin; resolvedScope = common.Value.Scope; resolvedMethod = common.Value.Method;
                return true;

            }
            finally { active.Remove(key); }
        }

        private IReadOnlyList<CombinedFactRow> Related(string source, string type, string reference)
        {
            var values = graph.CommandRelatedFacts(source, type, reference);
            materials.Add(new { Source = source, Type = type, Reference = reference, Count = values.Count });
            foreach (var value in values) materials.Add(ProjectCommandValueFact(value));
            return values;
        }
        private bool Fail(string gap) { gaps.Add(gap); return false; }
    }
}
