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
    public void Branch_target_cannot_inherit_lexically_skipped_configuration()
    {
        var flow = IlCallValueExtractor.Extract([
            "0:0:br.s:br:0x4", "1:1:ldarg.0:-", "2:2:stloc.0:-",
            "3:3:nop:-", "4:4:ldloc.0:-", "5:5:call:m:consume"
        ], [Call(5, "call", new(1, false, false, true))], 8, false);
        Assert.Equal("unknown", Assert.Single(Assert.Single(flow.Calls).Arguments).Kind);
        Assert.NotNull(flow.ControlFlow);
    }

    [Fact]
    public void Unconditional_branch_preserves_the_actual_overwritten_argument_not_original_slot()
    {
        var flow = IlCallValueExtractor.Extract([
            "0:0:ldstr:str:3:abcdef", "1:1:starg.s:v:0", "2:2:br.s:br:0x3",
            "3:3:ldarg.0:-", "4:4:call:m:consume"
        ], [Call(4, "call", new(1, false, false, true))], 8, false);
        Assert.Equal(new IlValueOrigin("constant-string-hash", "str:3:abcdef"), Assert.Single(Assert.Single(flow.Calls).Arguments));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Conditional_join_retains_only_equal_origins(bool equal)
    {
        var flow = IlCallValueExtractor.Extract([
            "0:0:ldarg.0:-", "1:1:brfalse.s:br:0x5", "2:2:ldstr:str:3:aaa", "3:3:stloc.0:-",
            "4:4:br.s:br:0x7", $"5:5:ldstr:str:3:{(equal ? "aaa" : "bbb")}", "6:6:stloc.0:-",
            "7:7:ldloc.0:-", "8:8:call:m:consume", "9:9:ret:-"
        ], [Call(8, "call", new(1, false, false, true))], 8, false);
        var argument = Assert.Single(Assert.Single(flow.Calls).Arguments);
        Assert.Equal(equal ? "constant-string-hash" : "unknown", argument.Kind);
        Assert.Equal("control-flow-candidate", Assert.Single(flow.Calls).State);
        Assert.NotNull(flow.ControlFlow);
    }

    [Fact]
    public void Loop_fixed_point_preserves_unchanged_receiver_and_widens_counter()
    {
        var instructions = new[] {
            "0:0:newobj:m:ctor", "1:1:stloc.0:-", "2:2:ldc.i4.0:-", "3:3:stloc.1:-",
            "4:4:ldloc.0:-", "5:5:callvirt:m:use", "6:6:ldloc.1:-", "7:7:ldc.i4.1:-",
            "8:8:add:-", "9:9:stloc.1:-", "10:a:ldloc.1:-", "11:b:ldc.i4.4:-",
            "12:c:blt.s:br:0x4", "13:d:ldloc.0:-", "14:e:callvirt:m:use", "15:f:ret:-"
        };
        var calls = new[] { Call(0, "newobj", new(0, true, false, true)),
            Call(5, "callvirt", new(0, true, false, true)), Call(14, "callvirt", new(0, true, false, true)) };
        var flow = IlCallValueExtractor.Extract(instructions, calls, 8, false);
        Assert.Equal(3, flow.Calls.Count);
        Assert.All(flow.Calls.Skip(1), call => Assert.Equal(new IlValueOrigin("allocation-site", "0"), call.Receiver));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(flow),
            System.Text.Json.JsonSerializer.Serialize(IlCallValueExtractor.Extract(instructions, calls, 8, false)));
    }

    [Fact]
    public void Address_exposure_does_not_recreate_an_original_argument_after_clear()
    {
        var flow = IlCallValueExtractor.Extract([
            "0:0:br.s:br:0x1", "1:1:ldarga.s:v:0", "2:2:pop:-", "3:3:ldarg.0:-", "4:4:call:m:use"
        ], [Call(4, "call", new(1, false, false, true))], 8, false);
        Assert.Equal("unknown", Assert.Single(Assert.Single(flow.Calls).Arguments).Kind);
        Assert.Equal("stack-unavailable", Assert.Single(flow.Calls).State);
    }

    [Fact]
    public void Control_flow_instruction_and_fixed_point_work_limits_withhold_all_operands()
    {
        var oversized = Enumerable.Repeat("0:0:br.s:br:0x0", IlControlFlowValueExtractor.MaxInstructions + 1).ToArray();
        var instructionLimited = IlControlFlowValueExtractor.Extract(oversized, [], 8);
        Assert.Empty(instructionLimited.Calls);
        Assert.Contains("IlValueControlFlowInstructionLimit", instructionLimited.Gaps);
        var instructions = new List<string>();
        void Add(string opcode, string operand = "-") => instructions.Add($"{instructions.Count}:{instructions.Count:x}:{opcode}:{operand}");
        for (var slot = 0; slot < 100; slot++) { Add("ldarg.0"); Add("stloc.s", $"v:{slot}"); }
        for (var index = 0; index < 3000; index++) Add("nop");
        Add("br.s", "br:0xc8");
        var workLimited = IlControlFlowValueExtractor.Extract(instructions, [], 8);
        Assert.Empty(workLimited.Calls);
        Assert.Contains("IlValueControlFlowWorkLimit", workLimited.Gaps);
        Assert.Equal(IlControlFlowValueExtractor.MaxWorkUnits, workLimited.WorkUnits);
    }

    [Fact]
    public void Exception_handler_entry_does_not_inherit_a_try_local_receiver()
    {
        var flow = IlCallValueExtractor.Extract([
            "0:0:newobj:m:ctor", "1:1:stloc.0:-", "2:2:br.s:br:0x8", "3:3:nop:-",
            "4:4:pop:-", "5:5:ldloc.0:-", "6:6:callvirt:m:use", "7:7:ret:-", "8:8:ret:-"
        ], [Call(0, "newobj", new(0, true, false, true)), Call(6, "callvirt", new(0, true, false, true))],
            8, true, [4], [new(4, 1)]);
        Assert.Equal("unknown", flow.Calls.Single(call => call.Offset == 6).Receiver.Kind);
        Assert.Contains("IlValueExceptionFlowUnavailable", flow.Gaps);
        var entry = Assert.Single(flow.ControlFlow!, node => node.ExceptionEntryStackCount == 1);
        Assert.True(entry.InvalidatesConfiguration);
    }

    [Fact]
    public void Leave_continuation_cannot_inherit_configuration_or_local_origins_past_finally()
    {
        var flow = IlCallValueExtractor.Extract([
            "0:0:newobj:m:ctor", "1:1:stloc.0:-", "2:2:leave.s:br:0x6",
            "3:3:nop:-", "4:4:endfinally:-", "5:5:nop:-", "6:6:ldloc.0:-", "7:7:callvirt:m:use"
        ], [Call(0, "newobj", new(0, true, false, true)), Call(7, "callvirt", new(0, true, false, true))],
            8, true, [3], [new(3, 0)]);
        Assert.Equal("unknown", flow.Calls.Single(call => call.Offset == 7).Receiver.Kind);
        Assert.Equal("stack-unavailable", flow.Calls.Single(call => call.Offset == 7).State);
        Assert.True(flow.ControlFlow!.Single(node => node.Offset == 2).InvalidatesConfiguration);
    }

    [Fact]
    public void Read_only_field_load_is_unknown_without_destroying_unrelated_local_origins()
    {
        var flow = IlCallValueExtractor.Extract([
            "0:0:br.s:br:0x1", "1:1:ldarg.0:-", "2:2:stloc.0:-", "3:3:ldarg.1:-",
            "4:4:ldfld:field", "5:5:call:m:use", "6:6:ldloc.0:-", "7:7:call:m:use"
        ], [Call(5, "call", new(1, false, false, true)), Call(7, "call", new(1, false, false, true))], 8, false);
        Assert.Equal("unknown", Assert.Single(flow.Calls[0].Arguments).Kind);
        Assert.Equal(new IlValueOrigin("argument-slot", "0"), Assert.Single(flow.Calls[1].Arguments));
        Assert.Contains("IlValueFieldOriginUnavailable", flow.Gaps);
    }

    [Fact]
    public void Exception_entry_limit_fails_closed_before_seeding_handler_states()
    {
        var flow = IlControlFlowValueExtractor.Extract(["0:0:ret:-"], [], 8,
            Enumerable.Repeat(new IlValueExceptionEntry(0, 0), IlControlFlowValueExtractor.MaxExceptionEntries + 1).ToArray());
        Assert.Empty(flow.Calls);
        Assert.Contains("IlValueExceptionEntryLimit", flow.Gaps);
    }

    [Fact]
    public void Known_exception_boundaries_admit_only_local_straight_line_origins()
    {
        var flow = IlCallValueExtractor.Extract([
            "0:0:ldarg.0:-", "1:1:stloc.0:-", "2:2:ldloc.0:-", "3:3:call:m:consume",
            "4:4:ldarg.1:-", "5:5:call:m:consume"
        ], [Call(3, "call", new(1, false, false, true)), Call(5, "call", new(1, false, false, true))],
            8, true, [2, 4]);
        Assert.Equal("unknown", Assert.Single(flow.Calls[0].Arguments).Kind);
        Assert.Equal(new IlValueOrigin("argument-slot", "1"), Assert.Single(flow.Calls[1].Arguments));
        Assert.NotEqual(flow.Calls[0].Region, flow.Calls[1].Region);
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
