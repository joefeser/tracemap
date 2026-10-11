using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class PublishGenericParameterTests
{
    private static string Type(string ns, string name) => $"type(namespace:{ns.Length}:{ns}|names:{name.Length}:{name})";
    private static readonly string Integer = Type("System", "Int32");
    private static readonly string Text = Type("System", "String");
    private static string Generic(string name, params string[] args) => Type("System.Collections.Generic", name) + "<" + string.Join(',', args) + ">";

    [Theory]
    [InlineData("list", true)]
    [InlineData("dictionary", true)]
    [InlineData("nested", true)]
    [InlineData("qualified", true)]
    [InlineData("vector", true)]
    [InlineData("argument-vector", true)]
    [InlineData("wrong-argument", false)]
    [InlineData("wrong-arity", false)]
    [InlineData("missing-import", false)]
    [InlineData("open-parameter", false)]
    [InlineData("wrong-namespace", false)]
    [InlineData("trailing-data", false)]
    [InlineData("unclosed", false)]
    [InlineData("empty-argument", false)]
    [InlineData("non-generic", false)]
    [InlineData("missing-close", false)]
    [InlineData("extra-argument", false)]
    [InlineData("malformed-length", false)]
    [InlineData("spacing", true)]
    public void Closed_generic_matching_is_structural_and_scope_bound(string scenario, bool expected)
    {
        var syntax = "List(Of Integer)";
        var metadata = Generic("List`1", Integer);
        var imports = "System.Collections.Generic";
        switch (scenario)
        {
            case "dictionary": syntax = "Dictionary(Of String, Integer)"; metadata = Generic("Dictionary`2", Text, Integer); break;
            case "nested": syntax = "Dictionary(Of String, List(Of Integer))"; metadata = Generic("Dictionary`2", Text, metadata); break;
            case "qualified": syntax = "Global.System.Collections.Generic.List(Of Integer)"; imports = ""; break;
            case "vector": syntax += "()"; metadata += "[]"; break;
            case "argument-vector": syntax = "List(Of Integer())"; metadata = Generic("List`1", Integer + "[]"); break;
            case "wrong-argument": metadata = Generic("List`1", Text); break;
            case "wrong-arity": metadata = Generic("List`2", Integer, Integer); break;
            case "missing-import": imports = ""; break;
            case "open-parameter": syntax = "List(Of T)"; metadata = Generic("List`1", "!!0"); break;
            case "wrong-namespace": metadata = Type("Other", "List`1") + "<" + Integer + ">"; break;
            case "trailing-data": metadata += "garbage"; break;
            case "unclosed": metadata = metadata[..^1]; break;
            case "empty-argument": syntax = "List(Of )"; break;
            case "non-generic": metadata = Type("System.Collections.Generic", "List`1"); break;
            case "missing-close": syntax = "List(Of Integer"; break;
            case "extra-argument": metadata = Generic("List`1", Integer, Text); break;
            case "malformed-length": metadata = metadata.Replace("5:Int32", "999:Int32", StringComparison.Ordinal); break;
            case "spacing": syntax = "list( Of integer )"; break;
        }
        Assert.Equal(expected, CombinedDependencyPathReporter.PublishParameterMatches(syntax, metadata, importedNamespaces: imports));
    }

    [Fact]
    public void Generic_variable_binding_does_not_capture_predefined_types()
    {
        var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Integer"] = "!!0" };
        Assert.False(CombinedDependencyPathReporter.PublishParameterMatches("Integer", "!!0", methodBindings: bindings));
        Assert.True(CombinedDependencyPathReporter.PublishParameterMatches("Integer", Integer, methodBindings: bindings));
        Assert.True(CombinedDependencyPathReporter.PublishParameterMatches("[Integer]", "!!0", methodBindings: bindings));
        Assert.False(CombinedDependencyPathReporter.PublishParameterMatches("[Integer]", "!0", methodBindings: bindings));
        Assert.False(CombinedDependencyPathReporter.PublishParameterMatches("Global.Integer", "!!0", methodBindings: bindings));
    }

    [Fact]
    public void Deep_generic_nesting_is_bounded()
    {
        var syntax = "Integer";
        var metadata = Integer;
        for (var index = 0; index < 18; index++) { syntax = "List(Of " + syntax + ")"; metadata = Generic("List`1", metadata); }
        Assert.False(CombinedDependencyPathReporter.PublishParameterMatches(syntax, metadata, importedNamespaces: "System.Collections.Generic"));
    }
}
