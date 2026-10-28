namespace TraceMap.Core;

/// <summary>Exact signed net48 framework signatures; never a method-name allowlist.</summary>
internal static class IlSqlLifecycleApis
{
    private const string Core = "scope(assembly:name:8:mscorlib|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
    private const string Data = "scope(assembly:name:11:System.Data|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
    private static string Type(string ns, string name) => $"type(namespace:{ns.Length}:{ns}|names:{name.Length}:{name})";
    private static string Primitive(string name) => Type("System", name);
    private static string DataType(string name) => Data + Type("System.Data", name);
    private static string CoreType(string name) => Core + Primitive(name);
    internal static bool TypeOperand(string operand, string name)
        => operand == "t:" + (name == "DataRow" ? DataType(name) : CoreType(name));
    internal static bool ReturnsDataSet(string signature)
        => signature.EndsWith("->" + DataType("DataSet"), StringComparison.Ordinal);
    internal static bool ReturnsVoid(string signature)
        => signature.EndsWith("->" + Primitive("Void"), StringComparison.Ordinal);
    internal static bool HasSingleBooleanArgument(IlObjectBody body)
        => body.ParameterCount == 1 && body.MemberReference.Contains("|(" + Primitive("Boolean") + ")->", StringComparison.Ordinal);
    internal static bool MentionsDataSet(string identity)
        => identity.Contains(DataType("DataSet"), StringComparison.Ordinal);
    internal static string?[] RootArgumentTypes(IlObjectBody body)
    {
        var result = new string?[body.ParameterCount];
        var start = body.MemberReference.LastIndexOf("|(", StringComparison.Ordinal);
        var end = body.MemberReference.LastIndexOf(")->", StringComparison.Ordinal);
        if (start < 0 || end < start + 2 || end - start > 16384) return result;
        var parts = body.MemberReference[(start + 2)..end].Split(',');
        if (body.ParameterCount == 0) return result;
        var names = new[] { "Object", "String", "Boolean", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "Single", "Double", "Char" };
        if (parts.Length == body.ParameterCount && parts.All(part => names.Any(name => part == Primitive(name))))
            for (var i = 0; i < parts.Length; i++) result[i] = parts[i];
        return result;
    }
    internal static bool IsInputBox(string operand, string? declaredType)
        => new[] { "Boolean", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "Single", "Double", "Char" }
            .Any(name => declaredType == Primitive(name) && TypeOperand(operand, name));
    private static string Member(string owner, string name, string result, bool instance, params string[] args)
        => $"memberref|type:{owner}|member:{name.Length}:{name}|arity:0|call:default|hasThis:{(instance ? "true" : "false")}|explicitThis:false|({string.Join(",", args)})->{result}";
    private static readonly IReadOnlyDictionary<string, string> Fixed = Build();

    private static Dictionary<string, string> Build()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string label, string owner, string name, string returns, bool instance, params string[] args)
            => result.Add(Member(owner, name, returns, instance, args), label);
        var obj = Primitive("Object"); var text = Primitive("String"); var nothing = Primitive("Void");
        Add("table-new", DataType("DataTable"), ".ctor", nothing, true, text);
        Add("columns", DataType("DataTable"), "get_Columns", DataType("DataColumnCollection"), true);
        Add("column-add", DataType("DataColumnCollection"), "Add", DataType("DataColumn"), true, text, CoreType("Type"));
        Add("rows-add", DataType("DataRowCollection"), "Add", DataType("DataRow"), true, obj + "[]");
        Add("table-clear", DataType("DataTable"), "Clear", nothing, true);
        Add("row-write", DataType("DataRow"), "set_Item", nothing, true, text, obj);
        Add("object-value", Core + Type("System.Runtime.CompilerServices", "RuntimeHelpers"), "GetObjectValue", obj, false, obj);
        Add("type-handle", CoreType("Type"), "GetTypeFromHandle", CoreType("Type"), false, CoreType("RuntimeTypeHandle"));
        Add("parameter-value", Data + Type("System.Data.SqlClient", "SqlParameter"), "set_Value", nothing, true, obj);
        Add("parameter-value", Data + Type("System.Data.Common", "DbParameter"), "set_Value", nothing, true, obj);
        var enumerator = Core + Type("System.Collections", "IEnumerator");
        var arrayList = Core + Type("System.Collections", "ArrayList");
        Add("arraylist-new", arrayList, ".ctor", nothing, true);
        Add("arraylist-add", arrayList, "Add", Primitive("Int32"), true, obj);
        Add("arraylist-enumerator", arrayList, "GetEnumerator", enumerator, true);
        foreach (var owner in new[] { "DataRowCollection", "InternalDataCollectionBase" })
            Add("enumerator", DataType(owner), "GetEnumerator", enumerator, true);
        Add("enumerator-current", enumerator, "get_Current", obj, true);
        Add("enumerator-next", enumerator, "MoveNext", Primitive("Boolean"), true);
        Add("enumerator-dispose", CoreType("IDisposable"), "Dispose", nothing, true);
        return result;
    }

    internal static string? Resolve(string identity, IEnumerable<string?> admittedOwners)
    {
        if (Fixed.TryGetValue(identity, out var known)) return known;
        foreach (var owner in admittedOwners.Where(o => o is not null).Distinct(StringComparer.Ordinal))
        {
            var list = Core + Type("System.Collections.Generic", "List`1") + "<" + owner + ">";
            if (identity == Member(list, ".ctor", Primitive("Void"), true)) return "list-new";
            if (identity == Member(list, "Clear", Primitive("Void"), true)) return "list-clear";
            if (identity == Member(list, "Add", Primitive("Void"), true, "!0")) return "list-add";
            if (identity == Member(list, "get_Item", "!0", true, Primitive("Int32"))) return "list-item";
        }
        return null;
    }
}
