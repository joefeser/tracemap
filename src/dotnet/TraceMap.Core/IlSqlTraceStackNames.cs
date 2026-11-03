namespace TraceMap.Core;

// Closed logging-only sequence. No runtime stack inspection or general reflection.
internal static class IlSqlTraceStackNameApis
{
    internal sealed record Rule(string ReceiverKind, string ResultKind, int Parameters, bool Constructor, string FrameworkVersion = "");
    private static readonly IReadOnlyDictionary<string, Rule> Rules = Build();
    private static Dictionary<string, Rule> Build()
    {
        var result = new Dictionary<string, Rule>(StringComparer.Ordinal);
        foreach (var version in new[] { "2.0.0.0", "4.0.0.0" })
        {
            var scope = $"scope(assembly:name:8:mscorlib|version:7:{version}|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
            string Type(string ns, string name) => $"{scope}type(namespace:{ns.Length}:{ns}|names:{name.Length}:{name})";
            void Add(string ns, string owner, string name, string returns, string arguments, Rule rule)
                => result.Add($"memberref|type:{Type(ns, owner)}|member:{name.Length}:{name}|arity:0|call:default|hasThis:true|explicitThis:false|({arguments})->{returns}", rule with { FrameworkVersion = version });
            Add("System.Diagnostics", "StackTrace", ".ctor", "type(namespace:6:System|names:4:Void)", "", new("", "trace-stack", 0, true));
            Add("System.Diagnostics", "StackTrace", "GetFrame", Type("System.Diagnostics", "StackFrame"),
                "type(namespace:6:System|names:5:Int32)", new("trace-stack", "trace-frame", 1, false));
            Add("System.Diagnostics", "StackFrame", "GetMethod", Type("System.Reflection", "MethodBase"), "", new("trace-frame", "trace-method", 0, false));
            Add("System.Reflection", "MemberInfo", "get_Name", IlSqlExternalTraceApis.StringType, "", new("trace-method", "computed-string", 0, false));
        }
        return result;
    }
    internal static Rule? Resolve(string identity) => Rules.GetValueOrDefault(identity);
}
