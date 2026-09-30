using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlCallValueExtractorTests
{
    [Fact]
    public void Constructor_local_receiver_and_hashed_argument_are_preserved()
    {
        var calls = new[]
        {
            Call(0, "newobj", new(0, true, false, true)),
            Call(4, "callvirt", new(1, true, false, true)),
            Call(7, "call", new(1, false, false, true))
        };
        var flow = IlCallValueExtractor.Extract([
            "0:0:newobj:m:ctor", "1:1:stloc.0:", "2:2:ldloc.0:",
            "3:3:ldstr:str:3:abcdef", "4:4:callvirt:m:set",
            "5:5:ldarg.1:", "6:6:nop:", "7:7:call:m:consume", "8:8:ret:"
        ], calls, 8, false);
        Assert.Equal(new IlValueOrigin("allocation-site", "0"), flow.Calls[1].Receiver);
        Assert.Equal(new IlValueOrigin("constant-string-hash", "str:3:abcdef"), Assert.Single(flow.Calls[1].Arguments));
        Assert.Equal(new IlValueOrigin("argument-slot", "1"), Assert.Single(flow.Calls[2].Arguments));
        Assert.All(flow.Calls, call => Assert.Equal("straight-line-candidate", call.State));
    }

    [Theory]
    [InlineData("br.s", "IlValueControlFlowUnavailable")]
    [InlineData("ldfld", "IlValueInstructionUnavailable")]
    [InlineData("ldloca.s", "IlValueInstructionUnavailable")]
    public void Unsupported_operation_does_not_carry_a_receiver_across_boundary(string opcode, string gap)
    {
        var flow = IlCallValueExtractor.Extract([
            "0:0:newobj:m:ctor", "1:1:stloc.0:", $"2:2:{opcode}:v:0",
            "3:3:ldloc.0:", "4:4:ldstr:str:3:abcdef", "5:5:callvirt:m:set"
        ], [Call(0, "newobj", new(0, true, false, true)), Call(5, "callvirt", new(1, true, false, true))], 8, false);
        Assert.Equal("unknown", flow.Calls[1].Receiver.Kind);
        Assert.Contains(gap, flow.Gaps);
        Assert.Equal(1, flow.Calls[1].Region);
    }

    [Fact]
    public void Exception_regions_withhold_operand_origins()
    {
        var flow = IlCallValueExtractor.Extract(["0:0:ldstr:str:3:abcdef", "1:1:call:m:set"],
            [Call(1, "call", new(1, false, false, true))], 8, true);
        Assert.Equal("exception-flow-unavailable", Assert.Single(flow.Calls).State);
        Assert.Equal("unknown", Assert.Single(flow.Calls[0].Arguments).Kind);
        Assert.Contains("IlValueExceptionFlowUnavailable", flow.Gaps);
    }

    [Fact]
    public void Unsupported_byref_call_invalidates_local_origins()
    {
        var flow = IlCallValueExtractor.Extract([
            "0:0:ldarg.0:", "1:1:stloc.0:", "2:2:call:m:mutate",
            "3:3:ldloc.0:", "4:4:call:m:consume"
        ], [Call(2, "call", new(0, false, false, false)), Call(4, "call", new(1, false, false, true))], 8, false);
        Assert.Equal("call-shape-unavailable", flow.Calls[0].State);
        Assert.Equal("unknown", Assert.Single(flow.Calls[1].Arguments).Kind);
    }

    [Fact]
    public void Stack_underflow_is_not_a_successful_call_observation()
    {
        var flow = IlCallValueExtractor.Extract(["0:0:callvirt:m:set"],
            [Call(0, "callvirt", new(1, true, false, true))], 8, false);
        Assert.Equal("stack-unavailable", Assert.Single(flow.Calls).State);
        Assert.Contains("IlValueStackUnavailable", flow.Gaps);
    }

    private static IlCallObservation Call(long offset, string opcode, IlCallStackShape shape)
        => new(offset, opcode, "member-reference", "0x0a000001", "synthetic", shape);
}
