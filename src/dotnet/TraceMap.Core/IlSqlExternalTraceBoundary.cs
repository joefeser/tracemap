using Body = TraceMap.Core.IlSqlObjectPathExtractor.Body;

namespace TraceMap.Core;

internal static class IlSqlExternalTraceApis
{
    private const string Web = "scope(assembly:name:10:System.Web|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)";
    private static string Type(string name) => $"{Web}type(namespace:10:System.Web|names:{name.Length}:{name})";
    private static string Member(string owner, string name, bool instance, string result, string arguments = "")
        => $"memberref|type:{Type(owner)}|member:{name.Length}:{name}|arity:0|call:default|hasThis:{(instance ? "true" : "false")}|explicitThis:false|({arguments})->{result}";
    internal const string StringType = "type(namespace:6:System|names:6:String)";
    private const string Void = "type(namespace:6:System|names:4:Void)";
    internal static readonly string Current = Member("HttpContext", "get_Current", false, Type("HttpContext"));
    internal static readonly string Trace = Member("HttpContext", "get_Trace", true, Type("TraceContext"));
    internal static readonly string Write = Member("TraceContext", "Write", true, Void, StringType);
    internal static readonly string WriteCategory = Member("TraceContext", "Write", true, Void, StringType + "," + StringType);
}

internal static partial class IlSqlLifecycleExtractor
{
    internal const string ExternalTracePolicy = "explicit-external-web-trace/2";
    internal sealed record ExternalTrace(string Policy, IReadOnlyList<OpaqueCall> Calls,
        IReadOnlyList<string> ArgumentOrigins, Site? EnclosingCatch,
        string Boundary = "normal-return-assumed;receiver-non-null-unproven;external-effects-unmodeled;tracked-heap-noninterference-assumed;not-purity-proof")
    {
        public bool ArgumentsPartial { get; init; }
        public string ArgumentEffects { get; init; } = "scalar-values-only";
    }

    private sealed partial class Machine
    {
        internal readonly List<ExternalTrace> TraceBoundaries = [];
        private string? externalTraceRoot;
        private static bool TraceShape(IlCallObservation call, string identity, int count, bool instance, bool returns)
            => call.TargetIdentity == identity && call.ReferenceKind == "memberref"
                && call.StackShape is { Supported: true } shape && shape.ParameterCount == count
                && shape.HasThis == instance && shape.ReturnsValue == returns && !shape.ByReferenceParameters.Contains('1');

        private bool TryExternalTrace(Body body, Value[] args, int depth, string[][] code, long[] offsets,
            Dictionary<long, IlCallObservation> calls, Dictionary<int, Value> locals, IReadOnlyList<Region> regions,
            int start, int invocation, out int terminal)
        {
            terminal = start;
            if (body.Observation.BodyIdentity != externalTraceRoot || depth != 0 || code[start][2] != "call"
                || !calls.TryGetValue(offsets[start], out var current)
                || !TraceShape(current, IlSqlExternalTraceApis.Current, 0, false, true)) return false;
            if (loopEnumerator.Kind != "unknown") Fail("loop-effect");
            if (TraceBoundaries.Count >= 8) Fail("chain-limit");
            if (!budget.TryConsume(code.Length)) throw new Refusal("lifecycle-work-limit");
            var sites = new List<OpaqueCall> { new(At(body, offsets[start], invocation), Hash(current.TargetIdentity)) };
            var values = new List<Value>();
            var expressionArrays = new HashSet<string>(StringComparer.Ordinal);
            var membership = Membership(offsets[start]);
            var foundTrace = false;
            for (var index = start + 1; index < code.Length && index <= start + 63; index++)
            {
                if (++work > 8192 || !budget.TryConsume(1)) throw new Refusal("lifecycle-work-limit");
                if (Membership(offsets[index]) != membership) Fail("exception-boundary-crossing");
                var op = code[index][2]; var operand = code[index][3];
                if (values.Count > 16) Fail("expression-stack-limit");
                if (op == "nop") continue;
                if (!foundTrace)
                {
                    if (op != "callvirt" || !calls.TryGetValue(offsets[index], out var trace)
                        || !TraceShape(trace, IlSqlExternalTraceApis.Trace, 0, true, true)) Fail("trace-getter-shape");
                    sites.Add(new(At(body, offsets[index], invocation), Hash(calls[offsets[index]].TargetIdentity)));
                    foundTrace = true; continue;
                }
                if (op == "callvirt" && calls.TryGetValue(offsets[index], out var write)
                    && (TraceShape(write, IlSqlExternalTraceApis.Write, 1, true, false) && values.Count == 1
                        || TraceShape(write, IlSqlExternalTraceApis.WriteCategory, 2, true, false) && values.Count == 2))
                {
                    if (values.Any(value => !TraceStringValue(value))) Fail("unproved-string");
                    var arguments = values.Select(TraceOrigin).ToArray();
                    foreach (var line in code.Where(line => line[3].StartsWith("br:0x", StringComparison.Ordinal)))
                    {
                        var target = Offset(line[3][5..]);
                        if (target > offsets[start] && target <= offsets[index]) Fail("branch-enters-chain");
                    }
                    sites.Add(new(At(body, offsets[index], invocation), Hash(write.TargetIdentity)));
                    var handler = regions.Where(region => region.Kind == "catch" && offsets[start] >= region.Start && offsets[start] < region.End)
                        .OrderBy(region => region.End - region.Start).FirstOrDefault();
                    TraceBoundaries.Add(new(ExternalTracePolicy, sites.ToArray(), arguments.ToArray(),
                        handler is null ? null : At(body, handler.Handler, invocation))
                        { ArgumentsPartial = values.Any(value => value.Kind == "computed-string"),
                            ArgumentEffects = values.Any(value => value.Kind == "computed-string") ? "partial-unbound-values;computation-calls-recorded;may-throw" : "scalar-values-only" });
                    terminal = index; return true;
                }
                Value PopExpression()
                {
                    if (values.Count == 0) { Fail("sink-escape-or-expression-underflow"); return Value.Unknown; }
                    var last = values[^1]; values.RemoveAt(values.Count - 1); return last;
                }
                if (op == "ldstr") { values.Add(new("string", operand)); continue; }
                if (op == "ldnull") { values.Add(new("null", "")); continue; }
                if (op.StartsWith("ldc.i4", StringComparison.Ordinal))
                { values.Add(new("int", op == "ldc.i4.m1" ? "-1" : op is "ldc.i4" or "ldc.i4.s" ? operand[2..] : op[7..])); continue; }
                if (op == "dup") { var duplicate = PopExpression(); values.Add(duplicate); values.Add(duplicate); continue; }
                if (op == "box")
                {
                    var boxed = PopExpression();
                    if (!(boxed.Kind is "input" or "opaque-initializer" && IlSqlLifecycleApis.IsInputBox(operand, boxed.DeclaredType))
                        && !(boxed.Kind == "int" && (IlSqlLifecycleApis.TypeOperand(operand, "Int32")
                            || IlSqlLifecycleApis.TypeOperand(operand, "Boolean") && Number(boxed) is 0 or 1))) Fail("box-type");
                    values.Add(boxed); continue;
                }
                if (op == "newarr")
                {
                    if (!IlSqlLifecycleApis.TypeOperand(operand, "Object") && !IlSqlLifecycleApis.TypeOperand(operand, "String")) Fail("array-type");
                    var count = Number(PopExpression()); if (count is < 0 or > 16) Fail("array-limit");
                    var arrayValue = New("array"); var array = Get(arrayValue, "array");
                    for (var element = 0; element < count; element++) array.Slots.Add(Value.Unknown);
                    expressionArrays.Add(arrayValue.Text); values.Add(arrayValue); continue;
                }
                if (op == "stelem.ref")
                {
                    var element = PopExpression(); var position = Number(PopExpression()); var arrayValue = PopExpression();
                    if (!expressionArrays.Contains(arrayValue.Text) || !TraceScalarValue(element)) Fail("array-mutation-or-tracked-element");
                    var array = Get(arrayValue, "array"); if (position < 0 || position >= array.Slots.Count) Fail("array-index");
                    array.Slots[position] = element; continue;
                }
                if (op is "call" or "callvirt" && calls.TryGetValue(offsets[index], out var computation)
                    && IlSqlTraceStringApis.Resolve(computation.TargetIdentity) is { } rule)
                {
                    if (rule.ScalarReceiver is null && (op == "callvirt") != rule.Instance) Fail("string-call-opcode");
                    var input = new Value[rule.Parameters];
                    for (var argument = input.Length - 1; argument >= 0; argument--) input[argument] = PopExpression();
                    var receiver = rule.Instance ? PopExpression() : Value.Unknown;
                    values.Add(BuildTraceString(computation, rule, receiver, input, At(body, offsets[index], invocation))); continue;
                }
                Value value;
                if (Slot(op, operand, "ldloca", out var addressSlot) || Slot(op, operand, "ldarga", out addressSlot))
                {
                    var addressed = op.StartsWith("ldloca", StringComparison.Ordinal) ? locals.GetValueOrDefault(addressSlot, Value.Unknown)
                        : addressSlot >= 0 && addressSlot < args.Length ? args[addressSlot] : Value.Unknown;
                    if (!TraceScalarValue(addressed)) Fail("address-of-tracked-or-unproved-value");
                    values.Add(new("trace-scalar-address", TraceOrigin(addressed), addressed.DeclaredType ?? (addressed.Kind == "int" ? Scalar("Int32") : null)));
                    continue;
                }
                if (Slot(op, operand, "ldloc", out var slot)) value = locals.GetValueOrDefault(slot, Value.Unknown);
                else if (Slot(op, operand, "ldarg", out slot) && slot >= 0 && slot < args.Length)
                {
                    value = args[slot];
                    if (value.Kind == "object")
                    {
                        // Only a scalar field load from the modeled fresh root, never
                        // the root reference itself, may become a logging argument.
                        if (slot != 0 || index + 1 >= code.Length || code[index + 1][2] != "ldfld") Fail("tracked-reference-argument");
                        index++;
                        if (++work > 8192 || !budget.TryConsume(1)) throw new Refusal("lifecycle-work-limit");
                        if (Membership(offsets[index]) != membership) Fail("exception-boundary-crossing");
                        var parts = code[index][3].Split(':', 4);
                        if (parts.Length != 4 || parts[1] != "field") Fail("field-metadata");
                        var split = parts[3].IndexOf('|');
                        if (split < 0) Fail("field-metadata");
                        var identity = parts[3][(split + 1)..];
                        if (!body.Observation.ObjectBody!.PrivateInstanceFields.Contains(identity, StringComparer.Ordinal)
                            || !identity.EndsWith("|" + IlSqlExternalTraceApis.StringType, StringComparison.Ordinal)) Fail("field-metadata");
                        value = Get(value, "custom").Fields.GetValueOrDefault(identity, Value.Unknown);
                    }
                }
                else { Fail("argument-or-write-shape"); return false; }
                if (!TraceScalarValue(value) && !(value.Kind == "object" && heap.TryGetValue(value.Text, out var container) && container.Kind == "array"))
                    Fail("tracked-or-unproved-argument");
                values.Add(value);
            }
            Fail("chain-instruction-limit"); return false;

            void Fail(string reason) => throw new Refusal("lifecycle-external-trace-" + reason, MetadataFailure(body, "external-trace"));
            string Membership(long offset) => string.Join(',', regions.Select((region, index) =>
                offset >= region.Start && offset < region.End ? index + ":try"
                    : offset >= region.Handler && offset < region.HandlerEnd ? index + ":handler" : "").Where(value => value.Length > 0));
        }
    }
}
