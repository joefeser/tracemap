using System;
using System.Linq.Expressions;

namespace TraceMap.CompiledFixtures.Equivalence;

public static class QuotationMatrix
{
    public static Expression<Func<int>> Tree() => () => 42;
    public static Func<int> Delegate() => () => 42;
    public static Expression<Func<int>> Echo(Expression<Func<int>> value) => value;
}
