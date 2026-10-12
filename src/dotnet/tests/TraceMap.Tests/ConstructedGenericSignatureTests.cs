using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class ConstructedGenericSignatureTests
{
    private const string Owner = "scope(assembly:name:1:A|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null)type(namespace:1:N|names:3:G`1)";
    private const string Text = "type(namespace:6:System|names:6:String)";
    private const string Header = "|member:1:M|arity:0|call:default|hasThis:true|explicitThis:false|";
    private static string Definition(string signature) => "memberref|type:" + Owner + Header + signature;
    private static string Call(string signature, string argument = Text) => "memberref|type:" + Owner + "<" + argument + ">" + Header + signature;

    [Fact]
    public void Substitution_preserves_wrappers_and_concrete_types()
    {
        var call = Assert.IsType<CombinedDependencyPathReporter.ConstructedMember>(
            CombinedDependencyPathReporter.ParseConstructedMember(Call("(!0&,!0[])->!0")));
        Assert.True(CombinedDependencyPathReporter.ConstructedMemberMatches(Definition("(!0&,!0[])->!0"), 1, call));
        Assert.False(CombinedDependencyPathReporter.ConstructedMemberMatches(Definition("(!0,!0[])->!0"), 1, call));
        Assert.False(CombinedDependencyPathReporter.ConstructedMemberMatches(Definition("(!0&,!0[])->type(namespace:6:System|names:5:Int32)"), 1, call));
        Assert.False(CombinedDependencyPathReporter.ConstructedMemberMatches(Definition("(!0&,!0[])->!0"), 2, call));
        Assert.False(CombinedDependencyPathReporter.ConstructedMemberMatches(Definition("(!0&,!0[])->!0").Replace("1:A", "1:B"), 1, call));
        Assert.False(CombinedDependencyPathReporter.ConstructedMemberMatches(Definition("(!0&,!0[])->!0").Replace("1.0.0.0", "2.0.0.0"), 1, call));
    }

    [Theory]
    [InlineData("()->!!0")]
    [InlineData("()->!1")]
    [InlineData("()->!0*")]
    [InlineData("()->!0[0...3]")]
    [InlineData("()->fnptr:unsupported")]
    [InlineData("()->!0junk")]
    public void Unsupported_signatures_abstain(string signature) =>
        Assert.Null(CombinedDependencyPathReporter.ParseConstructedMember(Call(signature)));

    [Fact]
    public void Open_arguments_and_generic_methods_abstain()
    {
        Assert.Null(CombinedDependencyPathReporter.ParseConstructedMember(Call("()->!0", "!0")));
        Assert.Null(CombinedDependencyPathReporter.ParseConstructedMember(Call("()->!0").Replace("|arity:0", "|arity:1")));
        Assert.Null(CombinedDependencyPathReporter.ParseConstructedMember(new string('x', 65_537)));
    }

    [Fact]
    public void Framed_names_cannot_inject_type_parameter_syntax()
    {
        const string hostile = "type(namespace:1:N|names:5:!0|<>)";
        var call = Assert.IsType<CombinedDependencyPathReporter.ConstructedMember>(
            CombinedDependencyPathReporter.ParseConstructedMember(Call("()->" + hostile)));
        Assert.True(CombinedDependencyPathReporter.ConstructedMemberMatches(Definition("()->" + hostile), 1, call));
    }

    [Fact]
    public void Nested_owner_parameters_keep_their_ordinals()
    {
        const string number = "type(namespace:6:System|names:5:Int32)";
        var definition = Definition("(!0&,!1[])->!1").Replace("|names:3:G`1", "|names:7:Outer`17:Inner`1");
        var callIdentity = Call("(!0&,!1[])->!1", Text + "," + number)
            .Replace("|names:3:G`1", "|names:7:Outer`17:Inner`1");
        var call = Assert.IsType<CombinedDependencyPathReporter.ConstructedMember>(CombinedDependencyPathReporter.ParseConstructedMember(callIdentity));
        Assert.True(CombinedDependencyPathReporter.ConstructedMemberMatches(definition, 2, call));
        Assert.False(CombinedDependencyPathReporter.ConstructedMemberMatches(definition.Replace("(!0&,!1[])", "(!1&,!0[])"), 2, call));
        var sameArguments = Assert.IsType<CombinedDependencyPathReporter.ConstructedMember>(
            CombinedDependencyPathReporter.ParseConstructedMember(callIdentity.Replace(number, Text)));
        Assert.False(CombinedDependencyPathReporter.ConstructedMemberMatches(definition.Replace("(!0&,!1[])", "(!1&,!0[])"), 2, sameArguments));
    }
}
