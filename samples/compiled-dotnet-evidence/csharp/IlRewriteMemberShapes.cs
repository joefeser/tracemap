using System;
using System.Collections.Generic;

namespace TraceMap.CompiledFixtures.CSharp.Il;

public sealed class IlRewriteMemberShapes
{
    public static int First = 1;
    public static int Second = 2;

    private int value;
    private int otherValue = 3;
    public int OtherValue() => otherValue;
    public int Value
    {
        get => value;
        set => this.value = value;
    }

    public event Action<int>? Changed;
    public void Raise(int next) => Changed?.Invoke(next);

    public static int ReadField() => First;
    public static object? TypeTest(object value) => value is List<int> ? value : null;
    public static Type TypeToken() => typeof(Dictionary<int, string>);
    public static void Ignore<T>() { }
    public static void GenericCall() => Ignore<int>();
    public static object GenericType() => new List<int>();
    public static int ByReadonlyRef(in int value) => value;
    public static int VarArg(int first, __arglist) => first;
#if VARARG_CALL
    public static int UseVarArg() => VarArg(1, __arglist(2));
#endif

    public static int Target(int value) => value + 1;
    public static unsafe int Indirect(int value)
    {
        delegate* managed<int, int> pointer = &Target;
        return pointer(value);
    }

    public static void RemovedLater() { }
}
