using System.Globalization;

namespace TraceMap.Core;

internal static class IlSqlTraceStringApis
{
    internal sealed record Rule(int Parameters, bool Instance, bool ArrayTail, bool Format, string? ScalarReceiver = null);
    private static readonly IReadOnlyDictionary<string, Rule> Rules = Build();
    private static string Primitive(string name) => $"type(namespace:6:System|names:{name.Length}:{name})";
    private static Dictionary<string, Rule> Build()
    {
        var result = new Dictionary<string, Rule>(StringComparer.Ordinal);
        var text = Primitive("String"); var obj = Primitive("Object");
        void Add(string scope, string ns, string owner, string name, bool instance, bool array, bool format, params string[] args)
        {
            var identity = $"memberref|type:{scope}type(namespace:{ns.Length}:{ns}|names:{owner.Length}:{owner})|member:{name.Length}:{name}|arity:0|call:default|hasThis:{(instance ? "true" : "false")}|explicitThis:false|({string.Join(",", args)})->{text}";
            result.Add(identity, new(args.Length, instance, array, format,
                instance && owner != "String" ? Primitive(owner) : null));
        }
        foreach (var version in new[] { "2.0.0.0", "4.0.0.0" })
        {
            var core = $"scope(assembly:name:8:mscorlib|version:7:{version}|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
            for (var count = 2; count <= 4; count++) Add(core, "System", "String", "Concat", false, false, false, Enumerable.Repeat(text, count).ToArray());
            for (var count = 1; count <= 3; count++) Add(core, "System", "String", "Concat", false, false, false, Enumerable.Repeat(obj, count).ToArray());
            Add(core, "System", "String", "Concat", false, true, false, text + "[]");
            Add(core, "System", "String", "Concat", false, true, false, obj + "[]");
            for (var count = 1; count <= 3; count++) Add(core, "System", "String", "Format", false, false, true, new[] { text }.Concat(Enumerable.Repeat(obj, count)).ToArray());
            Add(core, "System", "String", "Format", false, true, true, text, obj + "[]");
            foreach (var name in new[] { "String", "Object", "Boolean", "Char", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "Single", "Double", "Decimal" })
                Add(core, "System", "Convert", "ToString", false, false, false, Primitive(name));
            foreach (var owner in new[] { "Boolean", "Char", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "Single", "Double" })
                Add(core, "System", owner, "ToString", true, false, false);
            foreach (var name in new[] { "ToString", "Trim", "ToUpperInvariant", "ToLowerInvariant" })
                Add(core, "System", "String", name, true, false, false);
        }
        foreach (var version in new[] { "8.0.0.0", "10.0.0.0" })
        {
            var vb = $"scope(assembly:name:21:Microsoft.VisualBasic|version:{version.Length}:{version}|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)";
            foreach (var name in new[] { "Object", "Boolean", "Char", "Byte", "Int16", "Int32", "Int64", "Single", "Double", "Decimal" })
                Add(vb, "Microsoft.VisualBasic.CompilerServices", "Conversions", "ToString", false, false, false, Primitive(name));
        }
        return result;
    }
    internal static Rule? Resolve(string identity) => Rules.GetValueOrDefault(identity);
}

internal static partial class IlSqlLifecycleExtractor
{
    internal sealed record TraceStringComputation(string Id, OpaqueCall Call, IReadOnlyList<string> ArgumentOrigins,
        string Result = "unbound-string", string Effects = "scalar-argument-computation;normal-return-assumed;value-unmodeled");

    private sealed partial class Machine
    {
        internal readonly List<TraceStringComputation> StringComputations = [];
        private static bool TraceStringValue(Value value) => value.Kind is "string" or "null" or "computed-string"
            || value.Kind is "input" or "opaque-initializer" && value.DeclaredType == IlSqlExternalTraceApis.StringType;
        private static bool TraceScalarValue(Value value) => value.Kind is "string" or "null" or "int" or "computed-string"
            || value.Kind is "input" or "opaque-initializer" && OpaqueScalars.Any(name => value.DeclaredType == Scalar(name));
        private static string TraceOrigin(Value value) => value.Kind switch
        {
            "string" => "literal-string", "null" => "null-string", "int" => "literal-int32",
            "input" => "argument:" + value.Text, "opaque-initializer" => "opaque-field:" + value.Text,
            "computed-string" => value.Text, _ => "unproved"
        };
        private Value BuildTraceString(IlCallObservation call, IlSqlTraceStringApis.Rule rule, Value receiver, Value[] arguments, Site site)
        {
            if (call.StackShape is not { Supported: true, ReturnsValue: true } shape || shape.HasThis != rule.Instance
                || shape.ParameterCount != rule.Parameters || shape.ByReferenceParameters.Contains('1')
                || arguments.Length != rule.Parameters) throw new Refusal("lifecycle-trace-string-call-shape");
            var values = new List<Value>();
            if (rule.Instance)
            {
                if (rule.ScalarReceiver is null)
                {
                    if (!TraceStringValue(receiver)) throw new Refusal("lifecycle-trace-string-receiver");
                    values.Add(receiver);
                }
                else
                {
                    if (receiver.Kind != "trace-scalar-address" || receiver.DeclaredType != rule.ScalarReceiver)
                        throw new Refusal("lifecycle-trace-string-scalar-receiver");
                    values.Add(new("computed-string", "scalar-address:" + receiver.Text, IlSqlExternalTraceApis.StringType));
                }
            }
            if (rule.Format && (arguments.Length == 0 || !TraceStringValue(arguments[0]))) throw new Refusal("lifecycle-trace-string-format");
            for (var index = 0; index < arguments.Length; index++)
            {
                if (rule.ArrayTail && index == arguments.Length - 1)
                {
                    var array = Get(arguments[index], "array");
                    if (array.Slots.Count > 16 || array.Slots.Any(value => !TraceScalarValue(value)))
                        throw new Refusal("lifecycle-trace-string-array");
                    values.AddRange(array.Slots);
                }
                else
                {
                    if (!TraceScalarValue(arguments[index])) throw new Refusal("lifecycle-trace-string-tracked-or-unproved-argument");
                    values.Add(arguments[index]);
                }
            }
            if (loopEnumerator.Kind != "unknown") throw new Refusal("lifecycle-trace-string-loop-effect");
            if (StringComputations.Count >= 32 || !budget.TryConsume(values.Count + 1)) throw new Refusal("lifecycle-trace-string-limit");
            var id = "computed-string:" + StringComputations.Count.ToString(CultureInfo.InvariantCulture);
            StringComputations.Add(new(id, new(site, Hash(call.TargetIdentity)), values.Select(TraceOrigin).ToArray()));
            return new("computed-string", id, IlSqlExternalTraceApis.StringType);
        }
    }
}
