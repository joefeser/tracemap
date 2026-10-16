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

type AccessorShape() =
    let changed = new Event<System.EventHandler, System.EventArgs>()
    member val Value = 0 with get, set
    member this.Snapshot = this.Value
    [<CLIEvent>]
    member _.Changed = changed.Publish
    member _.get_Unbound() = 0

// CLI Optional metadata is distinct from F# option-valued source arguments.
type OptionShape() =
    static member NullableRoundtrip(value: System.Nullable<int>) : System.Nullable<int> = value
    static member OptionRoundtrip(value: int option) : int option = value
    static member ValueOptionRoundtrip(value: int voption) : int voption = value
    static member OptionalArgument(?value: int) : int option = value

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

type ConstraintShape() =
    static member Free<'T>(value: 'T) : 'T = value
    static member Reference<'T when 'T : not struct>(value: 'T) : 'T = value
    static member Value<'T when 'T : struct>(value: 'T) : 'T = value
    static member Construct<'T when 'T : (new : unit -> 'T)>(value: 'T) : 'T = value
    static member Disposable<'T when 'T :> System.IDisposable>(value: 'T) : 'T = value

type DefaultShape() =
    static member Required(value: int) = value
    static member IntSeven([<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(7)>] value: int) = value
    static member IntNine([<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(9)>] value: int) = value
    static member Text([<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue("seven")>] value: string) = value
    static member NullText([<System.Runtime.InteropServices.Optional; System.Runtime.InteropServices.DefaultParameterValue(null: string)>] value: string) = value
    static member DecimalSeven([<System.Runtime.InteropServices.Optional; System.Runtime.CompilerServices.DecimalConstant(0uy, 0uy, 0u, 0u, 7u)>] value: decimal) = value

type ISharedFormatter =
    abstract Format: value: string -> string

type ExplicitShape() =
    member _.Format(value: string) = "ordinary:" + value
    interface ISharedFormatter with
        member _.Format(value: string) = "explicit:" + value

type OperatorShape() =
    static member (+) (left: OperatorShape, right: OperatorShape) : OperatorShape = left
    static member op_Implicit(value: int) : OperatorShape = OperatorShape()
    static member op_Explicit(value: OperatorShape) : int = 0
    static member op_LooksLikeOperator(left: OperatorShape, right: OperatorShape) : OperatorShape = right

module ModuleShape =
    [<CompiledName("Curried")>]
    let curried (left: int) (right: int) : int = left + right
    [<CompiledName("Tupled")>]
    let tupled (left: int, right: int) : int = left + right
    [<CompiledName("Renamed")>]
    let sourceAlias (value: int) : int = value
