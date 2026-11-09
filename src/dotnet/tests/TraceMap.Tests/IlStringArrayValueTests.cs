using System.Globalization;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlStringArrayValueTests
{
    private const string StringScope = "scope(assembly:name:8:mscorlib|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)type(namespace:6:System|names:6:String)";
    private const string Concat = "memberref|type:" + StringScope + "|member:6:Concat|arity:0|call:default|hasThis:false|explicitThis:false|(type(namespace:6:System|names:6:String)[])->type(namespace:6:System|names:6:String)";

    [Theory]
    [InlineData("local", 3, true)]
    [InlineData("alias", 3, true)]
    [InlineData("null", 3, true)]
    [InlineData("empty", 0, true)]
    [InlineData("limit", 16, true)]
    [InlineData("oversize", 17, false)]
    [InlineData("missing", 3, false)]
    [InlineData("rewrite", 3, false)]
    [InlineData("unknown-index", 3, false)]
    [InlineData("negative-index", 3, false)]
    [InlineData("past-end", 3, false)]
    [InlineData("unknown-value", 3, false)]
    [InlineData("escape", 3, false)]
    [InlineData("field-store", 3, false)]
    [InlineData("byref", 3, false)]
    [InlineData("element-address", 3, false)]
    [InlineData("loop", 3, false)]
    [InlineData("branch", 3, false)]
    [InlineData("wrong-type", 3, false)]
    [InlineData("wrong-token", 3, false)]
    [InlineData("wrong-member", 3, false)]
    [InlineData("legacy2", 3, true)]
    [InlineData("array-count", 3, false)]
    [InlineData("exposed-slot", 3, false)]
    public void Only_complete_unescaped_bounded_framework_string_arrays_are_snapshotted(string mode, int count, bool expected)
    {
        var code = new List<(string Op, string Operand)>();
        var calls = new List<IlCallObservation>();
        void Add(string op, string operand = "") => code.Add((op, operand));
        void Call(string identity, IlCallStackShape shape)
        {
            var offset = code.Count;
            calls.Add(new(offset, "call", "memberref", "0x0a000001", identity, shape));
            Add("call", "m:" + identity);
        }
        var type = mode == "wrong-type" ? StringScope.Replace("6:String", "6:Object", StringComparison.Ordinal) : StringScope;
        if (mode == "legacy2") type = type.Replace("4.0.0.0", "2.0.0.0", StringComparison.Ordinal);
        if (mode == "exposed-slot") { Add("ldloca.s", "v:0"); Call("unknown-helper", new(1, false, false, true, "1")); }
        Add("ldc.i4", "i:" + count); Add("newarr", "t:" + type); Add("stloc.0");
        if (mode == "array-count")
            for (var i = 0; i < IlStringArrayOrigins.MaxArrays; i++)
            { Add("ldc.i4", "i:" + count); Add("newarr", "t:" + type); Add("stloc.0"); }
        if (mode == "alias") { Add("ldloc.0"); Add("stloc.1"); }
        for (var i = 0; i < count; i++)
        {
            if (mode == "missing" && i == 1) continue;
            Add(mode == "alias" ? "ldloc.1" : "ldloc.0"); Add("ldc.i4", "i:" + i);
            if (mode == "null" && i == 1) Add("ldnull");
            else if (i == 1) Add("ldarg.0");
            else Add("ldstr", "str:1:" + new string('a', 64));
            Add("stelem.ref");
        }
        if (mode is "rewrite" or "unknown-index" or "negative-index" or "past-end" or "unknown-value")
        {
            Add("ldloc.0");
            if (mode == "unknown-index") Add("ldarg.1");
            else Add("ldc.i4", "i:" + (mode == "negative-index" ? -1 : mode == "past-end" ? count : 0));
            if (mode == "unknown-value") { Add("ldsfld", "f:unknown"); } else Add("ldarg.0");
            Add("stelem.ref");
        }
        if (mode == "escape") { Add("ldloc.0"); Call("unknown-helper", new(1, false, false, true, "0")); }
        if (mode == "field-store") { Add("ldloc.0"); Add("stsfld", "f:unknown"); }
        if (mode == "byref") { Add("ldloca.s", "v:0"); Call("unknown-helper", new(1, false, false, true, "1")); }
        if (mode == "element-address") { Add("ldloc.0"); Add("ldc.i4.0"); Add("ldelema", "t:" + type); Add("pop"); }
        if (mode == "branch")
        {
            Add("ldarg.1"); var branch = code.Count; Add("brfalse.s");
            Add("ldloc.0"); Call("unknown-helper", new(1, false, false, true, "0"));
            code[branch] = ("brfalse.s", "br:0x" + code.Count.ToString("x", CultureInfo.InvariantCulture));
        }
        Add("ldloc.0");
        var target = Concat;
        if (mode == "legacy2") target = target.Replace("4.0.0.0", "2.0.0.0", StringComparison.Ordinal);
        if (mode == "wrong-token") target = target.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal);
        if (mode == "wrong-member") target = target.Replace("6:Concat", "6:DecoyX", StringComparison.Ordinal);
        var concatOffset = code.Count;
        Call(target, new(1, false, true, true, "0")); Add("pop");
        if (mode == "loop") { Add("ldarg.1"); Add("brtrue.s", "br:0x0"); }
        Add("ret");
        var instructions = code.Select((item, i) => $"{i}:{i:x}:{item.Op}:{item.Operand}").ToArray();
        var flow = IlCallValueExtractor.Extract(instructions, calls, 16, false);
        var operand = Assert.Single(Assert.Single(flow.Calls, call => call.Offset == concatOffset).Arguments);
        Assert.Equal(expected, operand.Kind == IlStringArrayOrigins.Kind);
        if (expected)
        {
            var elements = JsonSerializer.Deserialize<IlValueOrigin[]>(operand.Identity)!;
            Assert.Equal(count, elements.Length);
            if (count > 1) Assert.Equal(mode == "null" ? "null" : "argument-slot", elements[1].Kind);
        }
        else if (mode is not ("wrong-type" or "wrong-token" or "wrong-member"))
            Assert.Contains(flow.Gaps, gap => gap.StartsWith("IlValueStringArray", StringComparison.Ordinal));
        Assert.Equal(JsonSerializer.Serialize(flow), JsonSerializer.Serialize(IlCallValueExtractor.Extract(instructions, calls, 16, false)));
        Assert.InRange(flow.WorkUnits, 1, IlControlFlowValueExtractor.MaxWorkUnits);
    }
}
