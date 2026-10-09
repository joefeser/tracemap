using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    private const string CompiledCommandValueRuleId = "combined.paths.compiled-command-value.v1";
    private const int MaxCompiledCommandValueHops = 64;
    private const int MaxCompiledCommandCallArguments = 128;

    private static void ProjectCompiledCommandValues(EvidenceGraph graph, CombinedPathNode[] nodes, CombinedPathEdge[] edges)
    {
        if (!nodes.Any(node => node.CommandBinding is not null)) return;
        var facts = graph.CommandFactsByCombinedId;
        var original = graph.CommandFactsByOriginalId;
        var generator = graph.CommandProjectionGeneratorSha256 ??= Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(typeof(CombinedDependencyPathReporter).Assembly.Location)));
        for (var index = 1; index < nodes.Length; index++)
        {
            if (nodes[index].CommandBinding is not { } binding) continue;
            var text = Resolve(binding.CommandTextOrigin, "constant-string-hash");
            var type = Resolve(binding.CommandTypeOrigin, "constant-int32");
            nodes[index] = nodes[index] with { CommandBinding = binding with { CommandTextFromPath = text, CommandTypeFromPath = type } };

            CompiledCommandPathValueBinding Resolve(CompiledCommandOperandOrigin initial, string constantKind,
                string? initialScope = null, string? initialMethod = null, int? initialIncoming = null, bool allowComposition = true)
            {
                var current = initial;
                var scope = initialScope ?? binding.IlBodyFactId;
                var methodId = initialMethod ?? binding.ContainingMethodFactId;
                var steps = new List<CompiledCommandValueStep>();
                var gaps = new SortedSet<string>(StringComparer.Ordinal);
                var materials = new List<object>();
                foreach (var id in new[] { scope, methodId }.OfType<string>())
                    if (facts.TryGetValue(id, out var originFact)) materials.Add(ProjectCommandValueFact(originFact));
                var returnSteps = new List<CompiledCommandReturnStep>();
                var returns = new CommandReturnResolver(graph, materials, gaps, returnSteps, constantKind, allowComposition);
                var state = "unresolved-operand";
                var incoming = initialIncoming ?? index - 2;
                while (current.Kind is "argument-slot" or "call-result")
                {
                    if (++returns.Work > MaxCompiledCommandValueHops)
                    { gaps.Add("IlCommandCallerHopLimit"); state = "limit"; break; }
                    if (current.Kind == "call-result")
                    {
                        if (!returns.Resolve(current, scope, methodId, out var returned, out var returnScope, out var returnMethod)) break;
                        current = returned; scope = returnScope; methodId = returnMethod;
                        continue;
                    }
                    if (incoming < 0 || methodId is null)
                    { gaps.Add("IlCommandRootArgumentUnresolved"); state = "unresolved-root-argument"; break; }
                    var edge = edges[incoming--];
                    materials.Add(new { edge.EdgeId, edge.EdgeKind, edge.FromNodeId, edge.ToNodeId, edge.SupportingFactIds });
                    if (edge.EdgeKind is not ("compiled-il-call" or "compiled-il-callvirt-candidate"))
                    { gaps.Add("IlCommandNonIlCallerBridge"); state = "unresolved-non-il-bridge"; break; }
                    var agreed = TryCommandCallOperands(graph, facts, original, edge, methodId,
                            out var call, out var value, out var body, out var caller, out var target,
                            out var receiver, out var result, out var arguments, out var hasThis, out var operandCandidates, out var reason);
                    foreach (var inspected in new[] { call, body, caller, target }.OfType<CombinedFactRow>())
                        materials.Add(ProjectCommandValueFact(inspected));
                    foreach (var inspected in operandCandidates) materials.Add(ProjectCommandValueFact(inspected));
                    if (!agreed) { gaps.Add(reason); state = "unresolved-call-evidence"; break; }
                    if (!int.TryParse(current.Identity, NumberStyles.None, CultureInfo.InvariantCulture, out var slot)
                        || slot < 0 || slot - (hasThis ? 1 : 0) >= arguments!.Length)
                    { gaps.Add("IlCommandCallerSlotUnavailable"); state = "unresolved-slot"; break; }
                    current = hasThis && slot == 0
                        ? call!.Properties["opcode"] == "newobj" ? result! : receiver!
                        : arguments![slot - (hasThis ? 1 : 0)];
                    scope = body!.CombinedFactId; methodId = caller!.CombinedFactId;
                    steps.Add(new(call!.CombinedFactId, value!.CombinedFactId, body.CombinedFactId,
                        caller.CombinedFactId, target!.CombinedFactId, slot, call.Properties["opcode"],
                        value.Properties["ilGeneratorSha256"], value.Properties["ilBoundedInputSha256"]));
                    if (edge.EdgeKind == "compiled-il-callvirt-candidate") gaps.Add("IlCommandVirtualDispatchUnproven");
                }
                if (current.Kind == constantKind) state = steps.Count == 0 && returnSteps.Count == 0 ? "method-local-constant" : "constant-on-encoded-call-path";
                else if (returns.Composition is not null) state = "symbolic-string-composition";
                else if (current.Kind != "argument-slot") gaps.Add("IlCommandOperandValueUnresolved");
                var composition = returns.Composition;
                if (composition is not null)
                    composition = composition with { OperandBindings = composition.Operands.Select(operand =>
                        Resolve(operand, constantKind, scope, methodId, incoming, allowComposition: false)).ToArray() };
                if (methodId is not null && facts.TryGetValue(methodId, out var finalMethod)) materials.Add(ProjectCommandValueFact(finalMethod));
                var input = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    Schema = "compiled-command-path-value.v1", Initial = initial, binding.IlBodyFactId,
                    binding.ContainingMethodFactId, binding.GeneratorSha256, binding.BoundedInputSha256,
                    MaxHops = MaxCompiledCommandValueHops, MaxArguments = MaxCompiledCommandCallArguments,
                    MaxOperandCompetitors = 2,
                    MaxReturnSites = 256, MaxProducerEdges = MaxCommandProducerEdges,
                    ExpectedConstantKind = constantKind, InitialScope = initialScope, InitialMethod = initialMethod,
                    InitialIncoming = initialIncoming, AllowComposition = allowComposition,
                    OperandBindings = composition?.OperandBindings, Inputs = materials
                });
                return new("compiled-command-path-value.v1", CompiledCommandValueRuleId, EvidenceTiers.Tier3SyntaxOrTextual,
                    state, current, scope, steps, gaps.ToArray(), generator, Convert.ToHexStringLower(SHA256.HashData(input)))
                    { ReturnSteps = returnSteps.Count == 0 ? null : returnSteps, Composition = composition,
                        OriginMethodIdentity = methodId is not null ? facts.GetValueOrDefault(methodId)?.TargetSymbol : null,
                        Limitations = composition is null ? null : ["SymbolicStringValueNotMaterialized"] };
            }
        }
    }

    private static object ProjectCommandValueFact(CombinedFactRow fact)
    {
        // Hash the exact bounded projection consumed by this rule, not unused
        // properties or a private source blob. This artifact is local-only.
        var keys = new[] { "rawFileSha256", "ilGeneratorSha256", "ilBoundedInputSha256", "ilBodyFactId", "compiledFactId",
            "ilCallFactId", "ilOffset", "opcode", "referenceKind", "targetIdentity", "signature", "valueSchema", "valueState",
            "callHasThis", "callParameterCount", "callShapeSupported", "callByReferenceParameters", "receiverOrigin", "resultOrigin", "argumentOrigins",
            "returnOrigins", "returnCount", "returnFlowGaps", "methodDispatchFlags" };
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in keys)
            if (fact.Properties.TryGetValue(key, out var value)) properties.Add(key, value.Length <= 64 * 1024 ? value
                : $"oversized-length:{value.Length}:sha256:{Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))}");
        return new { fact.SourceIndexId, fact.OriginalFactId, fact.FactType, fact.RuleId, fact.EvidenceTier, fact.TargetSymbol, Properties = properties };
    }

    private static bool TryCommandCallOperands(EvidenceGraph graph,
        IReadOnlyDictionary<string, CombinedFactRow> facts,
        IReadOnlyDictionary<(string SourceIndexId, string OriginalFactId), CombinedFactRow[]> original,
        CombinedPathEdge edge, string targetId, out CombinedFactRow? call, out CombinedFactRow? value,
        out CombinedFactRow? body, out CombinedFactRow? caller, out CombinedFactRow? target,
        out CompiledCommandOperandOrigin? receiver, out CompiledCommandOperandOrigin? result,
        out CompiledCommandOperandOrigin[]? arguments, out bool hasThis,
        out IReadOnlyList<CombinedFactRow> operandCandidates, out string reason)
    {
        call = value = body = caller = target = null;
        receiver = result = null; arguments = null; hasThis = false; reason = "IlCommandCallerFactJoinUnavailable";
        operandCandidates = [];
        if (!facts.TryGetValue(targetId, out target) || target.FactType != FactTypes.ManagedMethodDeclared
            || !edge.SupportingFactIds.Contains(targetId, StringComparer.Ordinal)
            || target.TargetSymbol is null || edge.ToNodeId != SymbolNodeId(target.SourceIndexId, target.TargetSymbol)) return false;
        var calls = edge.SupportingFactIds.Select(id => facts.GetValueOrDefault(id))
            .OfType<CombinedFactRow>().Where(fact => fact.FactType == FactTypes.ManagedIlCallObserved).Take(2).ToArray();
        operandCandidates = calls;
        if (calls.Length != 1) return false;
        call = calls[0];
        if (call.RuleId != RuleIds.DotNetIlCall || call.Properties.GetValueOrDefault("opcode") is not ("call" or "callvirt" or "newobj")
            || !TryUniqueFact(original, call.SourceIndexId, call.Properties.GetValueOrDefault("ilBodyFactId"), out var joinedBody)) return false;
        body = joinedBody;
        if (body.FactType != FactTypes.ManagedIlBodyDeclared
            || !TryUniqueFact(original, call.SourceIndexId, body.Properties.GetValueOrDefault("compiledFactId"), out var joinedCaller)) return false;
        caller = joinedCaller;
        if (caller.FactType != FactTypes.ManagedMethodDeclared || caller.TargetSymbol is null
            || edge.FromNodeId != SymbolNodeId(caller.SourceIndexId, caller.TargetSymbol)) return false;
        if (call.Properties.GetValueOrDefault("referenceKind") == "methoddef"
            && target.SourceIndexId != call.SourceIndexId) return false;
        var expectedTarget = call.Properties.GetValueOrDefault("referenceKind") == "methoddef" ? target.TargetSymbol : ExpectedMemberReference(target);
        if (expectedTarget is null || expectedTarget != call.Properties.GetValueOrDefault("targetIdentity")) return false;
        var operands = graph.CommandFacts is IIndexedCombinedFacts indexed
            ? indexed.CallOperandFacts(call.SourceIndexId, call.OriginalFactId)
            : graph.CommandOperandFacts(call.SourceIndexId, call.OriginalFactId);
        operandCandidates = operands;
        if (operands.Count != 1) { reason = "IlCommandCallerOperandMissingOrAmbiguous"; return false; }
        value = operands[0];
        var operandFact = value; var bodyFact = body; var callFact = call;
        if (value.RuleId != RuleIds.DotNetIlValues || value.EvidenceTier != EvidenceTiers.Tier3SyntaxOrTextual
            || value.Properties.GetValueOrDefault("valueSchema") != "il-call-values.v1"
            || value.Properties.GetValueOrDefault("valueState") is not ("straight-line-candidate" or "control-flow-candidate")
            || value.Properties.GetValueOrDefault("callShapeSupported") != "true"
            || value.Properties.GetValueOrDefault("ilBodyFactId") != body.OriginalFactId
            || value.Properties.GetValueOrDefault("ilOffset") != call.Properties.GetValueOrDefault("ilOffset")
            || new[] { "rawFileSha256", "ilGeneratorSha256", "ilBoundedInputSha256" }.Any(key =>
                string.IsNullOrEmpty(bodyFact.Properties.GetValueOrDefault(key))
                || operandFact.Properties.GetValueOrDefault(key) != bodyFact.Properties.GetValueOrDefault(key)
                || callFact.Properties.GetValueOrDefault(key) != bodyFact.Properties.GetValueOrDefault(key))
            || caller.Properties.GetValueOrDefault("rawFileSha256") != body.Properties.GetValueOrDefault("rawFileSha256"))
        { reason = "IlCommandCallerOperandProvenanceUnavailable"; return false; }
        var signature = target.Properties.GetValueOrDefault("signature")?.Split('|', 5);
        if (signature is not { Length: 5 } || signature[1] != "call:default" || signature[3] != "explicitThis:false"
            || signature[2] is not ("hasThis:true" or "hasThis:false")) return false;
        hasThis = signature[2] == "hasThis:true";
        if (value.Properties.GetValueOrDefault("callHasThis") != (hasThis ? "true" : "false")) return false;
        try
        {
            CompiledCommandOperandOrigin? Read(string key)
            {
                var encoded = operandFact.Properties.GetValueOrDefault(key);
                if (encoded is null || encoded.Length > 1024) return null;
                var origin = JsonSerializer.Deserialize<CompiledCommandOperandOrigin>(encoded);
                return origin is not null && (origin.Kind == "null" && origin.Identity == "" || ValidCompiledCommandOrigin(origin)) ? origin : null;
            }
            receiver = Read("receiverOrigin"); result = Read("resultOrigin");
            var encodedArguments = value.Properties.GetValueOrDefault("argumentOrigins");
            if (encodedArguments is null || encodedArguments.Length > 64 * 1024) return false;
            arguments = JsonSerializer.Deserialize<CompiledCommandOperandOrigin[]>(encodedArguments);
            if (receiver is null || result is null || arguments is null || arguments.Length > MaxCompiledCommandCallArguments
                || arguments.Any(origin => origin is null || !(origin.Kind == "null" && origin.Identity == "" || ValidCompiledCommandOrigin(origin)))
                || !int.TryParse(value.Properties.GetValueOrDefault("callParameterCount"), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                || count != arguments.Length) return false;
            if (value.Properties.TryGetValue("callByReferenceParameters", out var mask)
                && (mask.Length != arguments.Length || mask.Any(character => character is not ('0' or '1'))
                    || arguments.Where((origin, index) => mask[index] == '1').Any(origin => origin.Kind != "unknown")))
                return false;
            if (call.Properties["opcode"] == "newobj" && (result.Kind != "allocation-site" || result.Identity != call.Properties["ilOffset"])) return false;
            return true;
        }
        catch (JsonException) { reason = "IlCommandCallerOperandMalformed"; return false; }
    }
}
