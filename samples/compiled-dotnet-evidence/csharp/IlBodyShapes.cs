using System;
using System.Collections.Generic;

namespace TraceMap.CompiledFixtures.CSharp.Il;

// Deterministic IL body and call fixture shapes for the first Task 10 slice.
// Each paired method keeps the same opcode sequence while changing exactly one
// operand kind, so a canonical body identity that ignored operands would
// collide where the contract must not. Case IDs live in
// samples/compiled-dotnet-evidence/fixture-cases.json.

public static class IlBodyShapes
{
    // CS-IL-OPERAND-001: same opcodes (call; ret), different member operand.
    private static int MemberAlpha() => 7;
    private static int MemberBeta() => 11;

    public static int CallMemberAlpha() => MemberAlpha();

    public static int CallMemberBeta() => MemberBeta();

    // CS-IL-OPERAND-002: same opcodes (ldstr; ret), different string operand.
    public static string StringAlpha() => "il-alpha";

    public static string StringBeta() => "il-beta";

    // CS-IL-OPERAND-003: same opcodes (ldc.i4.s; ret), different constant operand.
    public static int ConstAlpha() => 41;

    public static int ConstBeta() => 42;

    // CS-IL-SIGNATURE-004: same simple name and same body opcode sequence
    // (ldarg.0; ldarg.0; add; ret), different full signature.
    public static int Twice(int value) => value + value;

    public static long Twice(long value) => value + value;

    // CS-IL-BRANCH-005: reordered case blocks keep one opcode and constant
    // stream while swapping the switch jump-table target operands.
    private static int Same(int value) => value;

    public static int SwitchOrderAlpha(int value)
    {
        switch (value)
        {
            case 0: return Same(1);
            case 1: return Same(1);
            default: return Same(2);
        }
    }

    public static int SwitchOrderBeta(int value)
    {
        switch (value)
        {
            case 1: return Same(1);
            case 0: return Same(1);
            default: return Same(2);
        }
    }

    // CS-IL-BRANCH-006: representative forward branch plus backward loop branch
    // with a different constant operand.
    public static int LoopTripAlpha(int count)
    {
        var total = 0;
        for (var i = 0; i < count; i++)
        {
            total = Same(total);
        }

        return total;
    }

    public static int LoopTripBeta(int count)
    {
        var total = 0;
        for (var i = 0; i < count; i++)
        {
            total = Same(total + 1);
        }

        return total;
    }

    // CS-IL-ASSEMBLY-007: identical trivial body shared across languages and
    // assemblies; identity scoping must keep them distinct.
    public static int IlIdentity(int value) => value;
}

public interface ICallShape
{
    int Apply(int value);
}

public class CallTargetWidget : ICallShape
{
    public CallTargetWidget()
    {
    }

    public virtual int Describe(int value) => value;

    public int DescribeInstance(int value) => Describe(value);

    public int Apply(int value) => value;
}

public static class GenericCallShapes
{
    public static T Echo<T>(T value) => value;

    // CS-IL-CALL-008: call through a MethodSpec instantiation.
    public static int EchoInstantiated(int value) => Echo<int>(value);

    // CS-IL-CALL-009: callvirt on a generic instance type member.
    private static readonly List<int> Items = [];

    public static void AddToList(int value) => Items.Add(value);

    // CS-IL-CALL-010: constrained. prefix plus callvirt on an unconstrained
    // generic parameter.
    public static string RenderConstrained<T>(T value) => value?.ToString() ?? string.Empty;

    // CS-IL-CALL-011: ldftn plus newobj delegate construction and a callvirt
    // through the delegate, plus a callvirt through an interface reference.
    public static int InvokeViaDelegate(int value)
    {
        Func<int, int> handler = Echo;
        return handler(value);
    }

    public static int ApplyViaInterface(ICallShape shape, int value) => shape.Apply(value);

    // CS-IL-BODY-012: locals of assorted shapes plus catch, filter, and finally
    // exception regions in one deterministic method.
    public static int ExceptionRegionShapes(int value)
    {
        var buffer = new byte[4];
        var label = "ready";
        var widget = new CallTargetWidget();
        try
        {
            return widget.Describe(value);
        }
        catch (InvalidOperationException exception) when (exception.Message.Length > 0)
        {
            return -1;
        }
        catch (FormatException)
        {
            return -2;
        }
        finally
        {
            _ = label.Length;
            _ = buffer.Length;
        }
    }
}
