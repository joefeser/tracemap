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
    internal const string ExternalTracePolicy = "explicit-external-web-trace/1";
    internal sealed record ExternalTrace(string Policy, IReadOnlyList<OpaqueCall> Calls,
        IReadOnlyList<string> ArgumentOrigins, Site? EnclosingCatch,
        string Boundary = "normal-return-assumed;receiver-non-null-unproven;external-effects-unmodeled;tracked-heap-noninterference-assumed;not-purity-proof");

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
            var arguments = new List<string>();
            var membership = Membership(offsets[start]);
            var foundTrace = false;
            for (var index = start + 1; index < code.Length && index <= start + 15; index++)
            {
                if (++work > 8192 || !budget.TryConsume(1)) throw new Refusal("lifecycle-work-limit");
                if (Membership(offsets[index]) != membership) Fail("exception-boundary-crossing");
                var op = code[index][2]; var operand = code[index][3];
                if (op == "nop") continue;
                if (!foundTrace)
                {
                    if (op != "callvirt" || !calls.TryGetValue(offsets[index], out var trace)
                        || !TraceShape(trace, IlSqlExternalTraceApis.Trace, 0, true, true)) Fail("trace-getter-shape");
                    sites.Add(new(At(body, offsets[index], invocation), Hash(calls[offsets[index]].TargetIdentity)));
                    foundTrace = true; continue;
                }
                if (op == "callvirt" && calls.TryGetValue(offsets[index], out var write)
                    && (TraceShape(write, IlSqlExternalTraceApis.Write, 1, true, false) && arguments.Count == 1
                        || TraceShape(write, IlSqlExternalTraceApis.WriteCategory, 2, true, false) && arguments.Count == 2))
                {
                    foreach (var line in code.Where(line => line[3].StartsWith("br:0x", StringComparison.Ordinal)))
                    {
                        var target = Offset(line[3][5..]);
                        if (target > offsets[start] && target <= offsets[index]) Fail("branch-enters-chain");
                    }
                    sites.Add(new(At(body, offsets[index], invocation), Hash(write.TargetIdentity)));
                    var handler = regions.Where(region => region.Kind == "catch" && offsets[start] >= region.Start && offsets[start] < region.End)
                        .OrderBy(region => region.End - region.Start).FirstOrDefault();
                    TraceBoundaries.Add(new(ExternalTracePolicy, sites.ToArray(), arguments.ToArray(),
                        handler is null ? null : At(body, handler.Handler, invocation)));
                    terminal = index; return true;
                }
                if (arguments.Count >= 2) Fail("argument-or-write-shape");
                if (op == "ldstr") { arguments.Add("literal-string"); continue; }
                if (op == "ldnull") { arguments.Add("null-string"); continue; }
                Value value;
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
                arguments.Add(value.Kind switch
                {
                    "string" => "literal-string",
                    "null" => "null-string",
                    "input" when value.DeclaredType == IlSqlExternalTraceApis.StringType => "argument:" + value.Text,
                    "opaque-initializer" when value.DeclaredType == IlSqlExternalTraceApis.StringType => "opaque-field:" + value.Text,
                    _ => throw new Refusal("lifecycle-external-trace-unproved-string", MetadataFailure(body, "external-trace"))
                });
            }
            Fail("chain-instruction-limit"); return false;

            void Fail(string reason) => throw new Refusal("lifecycle-external-trace-" + reason, MetadataFailure(body, "external-trace"));
            string Membership(long offset) => string.Join(',', regions.Select((region, index) =>
                offset >= region.Start && offset < region.End ? index + ":try"
                    : offset >= region.Handler && offset < region.HandlerEnd ? index + ":handler" : "").Where(value => value.Length > 0));
        }
    }
}
