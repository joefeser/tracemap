namespace TraceMap.CompiledFixtures.FSharp

type Status =
    | Ready
    | Failed of code: int

type RecordShape =
    { Name: string
      Count: int }

type GenericShape<'T>(value: 'T) =
    member _.Value = value
    member _.Echo<'U>(input: 'U) = input

module Functions =
    let curried left right = left + right
    let tupled (left, right) = left + right

    [<CompiledName("RenamedForMetadata")>]
    let compiledName value = value

/// FS-IL-ASSEMBLY-007: trivial compiled body with a direct call shape.
module IlBodyShapes =
    let ilIdentity (value: int) = value
    let ilCallShape (value: int) = Functions.tupled (value, value)

namespace TraceMap.CompiledFixtures.Equivalence

type SharedShape() =
    static member Select(value: int) : int = value
    static member Select(value: string) : string = value
    static member Reference(value: byref<int>) : int = value
    static member Echo<'T>(value: 'T) : 'T = value
    static member Echo<'TLeft, 'TRight>(value: 'TLeft) : 'TLeft = value
    static member Rank(value: int[]) : int[] = value
    static member Rank(value: int[,]) : int[,] = value
