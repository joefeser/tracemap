using System;

namespace TraceMap.CompiledFixtures.CSharp.Il;

// Deterministic ECMA-335 control-flow and exception-handling shapes for the
// Task 10 rewrite suite. Each method is the compiler-produced "before" side
// of a declared before/after pair; the "after" sides are deterministic
// Mono.Cecil-generated mutations or bounded byte patches produced inside the
// public test suite, never hand-written binaries. Case IDs live in
// samples/compiled-dotnet-evidence/fixture-cases.json.

public static class IlRewriteControlFlowShapes
{
    private static int Same(int value) => value;

    // CS-ILRW-CFLOW-010: a for loop compiles a forward unconditional branch
    // plus a backward conditional branch; retargeting one branch operand is
    // an operand-only control-flow rewrite.
    public static int LoopWithBranches(int count)
    {
        var total = 0;
        for (var i = 0; i < count; i++)
        {
            total = Same(total);
        }

        return total;
    }

    // CS-ILRW-CFLOW-011: a dense case set compiles to a genuine switch jump
    // table; permuting its target entries is an operand-only rewrite.
    public static int DenseSwitch(int value)
    {
        switch (value)
        {
            case 0: return 10;
            case 1: return 20;
            case 2: return 30;
            case 3: return 40;
            case 4: return 50;
            default: return 60;
        }
    }

    // CS-ILRW-CFLOW-012: try/catch/finally regions compile leave
    // instructions whose targets share the post-region code; retargeting one
    // leave is an operand-only rewrite that no evidence lane may read as
    // behavioral equivalence.
    public static int TryCatchFinallyWithLeave(int value)
    {
        var result = value;
        try
        {
            result = Same(value);
        }
        catch (InvalidOperationException)
        {
            result = -1;
        }
        finally
        {
            result = Same(result);
        }

        return result + 2;
    }

    // CS-ILRW-CFLOW-013/014: a catch region nested inside a finally region;
    // rebinding the inner try start or changing the inner handler kind
    // changes only non-instruction exception-region structure.
    public static int NestedTryRegions(int value)
    {
        var result = value;
        try
        {
            try
            {
                result = Same(value);
            }
            catch (FormatException)
            {
                result = -2;
            }
        }
        finally
        {
            result = Same(result);
        }

        return result + 3;
    }

    // CS-ILRW-CFLOW-015: locals and arithmetic drive a fat method-body
    // header whose recorded max-stack can change alone.
    public static int LocalsAndMaxStack(int value)
    {
        var a = value + 1;
        var b = value + 2;
        var c = value + 3;
        return a + b + c;
    }

    // CS-ILRW-CFLOW-016/017: evaluation-stack-sensitive rewrites; the
    // inserted dup/pop pair is transiently deeper but net stack-neutral,
    // while the inserted constant/add sequence reshapes the depth profile.
    // Static evidence must classify both as instruction-stream changes
    // without claiming runtime equivalence either way.
    public static int StackShape(int value) => value + 1;
}
