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
