using System;

namespace TraceMap.CompiledFixtures.Equivalence;

public static class RefLikeMatrix
{
    public static Span<int> Pass(Span<int> value) => value;
    public static ReadOnlySpan<int> Pass(ReadOnlySpan<int> value) => value;
    public static ref Span<int> Borrow(ref Span<int> value) => ref value;
    public static ref readonly Span<int> BorrowReadOnly(in Span<int> value) => ref value;
}
