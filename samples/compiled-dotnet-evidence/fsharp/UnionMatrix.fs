namespace TraceMap.CompiledFixtures.Equivalence

type UnionMatrix =
    | Ready
    | Failed of code: int
