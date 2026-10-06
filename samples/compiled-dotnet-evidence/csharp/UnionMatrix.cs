namespace TraceMap.CompiledFixtures.Equivalence;

// Ordinary factories with union-looking names; no union semantics are implied.
public sealed class UnionMatrix
{
    public static UnionMatrix Ready => new();
    public static UnionMatrix NewFailed(int code) => new();
}
