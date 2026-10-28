using Body = TraceMap.Core.IlSqlObjectPathExtractor.Body;

namespace TraceMap.Core;

internal static partial class IlSqlLifecycleExtractor
{
    internal const string OpaquePolicy = "explicit-opaque-root-scalars/1";
    internal const string OpaqueEntrySchema = "il-sql-lifecycle-opaque-entry/1";
    internal sealed record OpaqueCall(Site Site, string SignatureSha256, bool MayThrow = true, string Effects = "unmodeled");
    internal sealed record OpaqueInitializer(string Policy, Site Store, string FieldSha256,
        string DeclaredType, IReadOnlyList<OpaqueCall> Calls,
        string Boundary = "conditional-normal-return;external-effects-unmodeled;no-tracked-reference-arguments;tracked-heap-noninterference-assumed;not-purity-proof");

    private sealed partial class Machine
    {
        internal readonly List<OpaqueInitializer> Initializers = [];
        private readonly HashSet<string> opaqueFields = new(StringComparer.Ordinal);
        private string? opaqueConstructor, opaqueInstance;
        private string opaqueRejection = "chain-shape-unproven";
        private bool RejectOpaque(string reason) { opaqueRejection = reason; return false; }
        private static readonly string[] OpaqueScalars = ["String", "Boolean", "Byte", "SByte", "Int16", "UInt16",
            "Int32", "UInt32", "Int64", "UInt64", "Single", "Double", "Char"];
        private static string Scalar(string name) => $"type(namespace:6:System|names:{name.Length}:{name})";

        // A deliberately opt-in abstraction boundary, not a proof of purity/freshness
        // of unknown return objects. Only a fresh tracked root's private scalar store
        // survives the boundary. Every intervening return is otherwise opaque.
        private bool TryOpaqueInitializer(Body body, Value[] args, int depth, string[][] code,
            long[] offsets, Dictionary<long, IlCallObservation> calls, int start, int invocation, out int terminal)
        {
            terminal = start;
            if (opaqueConstructor != body.Observation.BodyIdentity || depth != 0
                || args.Length != 1 || args[0].Kind != "object" || args[0].Text != opaqueInstance
                || !Slot(code[start][2], code[start][3], "ldarg", out var arg) || arg != 0)
                return false;
            opaqueRejection = "chain-shape-unproven";
            var meta = body.Observation.ObjectBody!;
            if (!budget.TryConsume(code.Length)) throw new Refusal("lifecycle-work-limit");
            if (meta.Name != ".ctor" || meta.ParameterCount != 0 || !meta.HasThis || meta.ReturnsValue
                || body.Observation.ExceptionRegionCount != 0 || meta.ExceptionRegions is not { Count: 0 }
                || code.Any(line => line[3].StartsWith("br:", StringComparison.Ordinal)
                    || line[2] is "switch" or "leave" or "leave.s" or "throw" or "rethrow")) return RejectOpaque("constructor-control-or-handler");
            var values = new List<Value> { new("initializer-this", args[0].Text) };
            var sites = new List<OpaqueCall>();
            for (var index = start + 1; index < code.Length && index <= start + 31; index++)
            {
                if (values.Count > 16) return RejectOpaque("chain-stack-limit");
                if (++work > 8192 || !budget.TryConsume(1)) throw new Refusal("lifecycle-work-limit");
                var op = code[index][2]; var operand = code[index][3];
                if (op == "nop") continue;
                if (op == "ldstr") { values.Add(new("initializer-literal", "", Scalar("String"))); continue; }
                if (op.StartsWith("ldc.i4", StringComparison.Ordinal))
                { values.Add(new("initializer-literal", "", Scalar("Int32"))); continue; }
                if (op is "call" or "callvirt")
                {
                    if (sites.Count >= 8 || !calls.TryGetValue(offsets[index], out var call)
                        || call.ReferenceKind != "memberref"
                        || call.StackShape is not { Supported: true, ReturnsValue: true, ParameterCount: >= 0 and <= 4 } shape
                        || shape.ByReferenceParameters.Contains('1') || (op == "callvirt") != shape.HasThis
                        || !call.TargetIdentity.Contains("|arity:0|call:default|", StringComparison.Ordinal)
                        || !call.TargetIdentity.Contains("|explicitThis:false|", StringComparison.Ordinal)
                        || IlSqlObjectPathExtractor.KnownFrameworkApi(call.TargetIdentity) is not null
                        || IlSqlLifecycleApis.Resolve(call.TargetIdentity, []) is not null)
                        return RejectOpaque("call-shape-or-cap");
                    if (!budget.TryConsume(bodies.Count)) throw new Refusal("lifecycle-work-limit");
                    // An available or metadata-failed compiled callee is never replaced
                    // with an assumption about an external opaque call.
                    if (bodies.Any(candidate => candidate.Observation.MethodIdentity == call.TargetIdentity
                        || candidate.Observation.ObjectBody?.MemberReference == call.TargetIdentity
                        || candidate.MemberReferenceAlias == call.TargetIdentity)) return RejectOpaque("compiled-callee-requires-inspection");
                    var consumed = shape.ParameterCount + (shape.HasThis ? 1 : 0);
                    if (values.Count < consumed + 1 || values.Skip(values.Count - consumed)
                        .Any(value => value.Kind is not ("initializer-literal" or "initializer-temporary"))) return RejectOpaque("tracked-reference-or-stack");
                    if (shape.HasThis && values[values.Count - consumed].Kind != "initializer-temporary") return RejectOpaque("receiver-not-temporary");
                    var arrow = call.TargetIdentity.LastIndexOf("->", StringComparison.Ordinal);
                    if (arrow < 0) return RejectOpaque("return-metadata");
                    var returns = call.TargetIdentity[(arrow + 2)..];
                    if (!(returns.StartsWith("type(", StringComparison.Ordinal) || returns.StartsWith("scope(", StringComparison.Ordinal))
                        || returns.IndexOfAny(['&', '*', '[', ']', '!', '<', '>']) >= 0 || returns == Scalar("Void")) return RejectOpaque("return-type");
                    values.RemoveRange(values.Count - consumed, consumed);
                    values.Add(new("initializer-temporary", "", returns));
                    if (values.Count > 16) return RejectOpaque("chain-stack-limit");
                    sites.Add(new(At(body, offsets[index], invocation), Hash(call.TargetIdentity)));
                    continue;
                }
                if (op != "stfld" || sites.Count == 0 || values.Count != 2
                    || values[0].Kind != "initializer-this" || values[1].Kind != "initializer-temporary") return RejectOpaque("nonlinear-or-terminal-store");
                var parts = operand.Split(':', 4);
                if (parts.Length != 4 || parts[1] != "field") return false;
                var separator = parts[3].IndexOf('|');
                if (separator < 0) return false;
                var identity = parts[3][(separator + 1)..];
                var declared = OpaqueScalars.Select(Scalar).SingleOrDefault(type => identity.EndsWith("|" + type, StringComparison.Ordinal));
                if (declared is null || declared != values[1].DeclaredType
                    || !identity.StartsWith("type:" + meta.OwnerType + "|member:", StringComparison.Ordinal)
                    || !meta.PrivateInstanceFields.Contains(identity, StringComparer.Ordinal)) return RejectOpaque("field-type-or-metadata");
                var state = Get(args[0], "custom");
                var fieldKey = args[0].Text + ":" + identity;
                if (Initializers.Count >= 8) throw new Refusal("lifecycle-opaque-initializer-limit");
                if (state.Fields.ContainsKey(identity) || !opaqueFields.Add(fieldKey))
                    throw new Refusal("lifecycle-opaque-field-mutated");
                var fieldHash = Hash(identity);
                state.Fields.Add(identity, new("opaque-initializer", fieldHash, declared));
                Initializers.Add(new(OpaquePolicy, At(body, offsets[index], invocation), fieldHash, declared, sites.ToArray()));
                terminal = index;
                return true;
            }
            return RejectOpaque("chain-instruction-limit");
        }
    }
}
