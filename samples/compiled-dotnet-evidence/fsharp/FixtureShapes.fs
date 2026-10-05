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

// CLI Optional metadata is distinct from F# option-valued source arguments.
type OptionalShape() =
    static member Required(value: int) = value
    static member OptionalSeven([<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(7)>] value: int) = value
    static member OptionalNine([<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(9)>] value: int) = value
    static member Wide(
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p0: int,
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p1: int,
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p2: int,
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p3: int,
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p4: int,
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p5: int,
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p6: int,
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p7: int,
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p8: int,
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p9: int,
        [<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(0)>] p10: int) = p10
