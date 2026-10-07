namespace TraceMap.CompiledFixtures.Equivalence

open System
open Microsoft.FSharp.Quotations

type QuotationMatrix =
    static member Tree() : Expr<int> = <@ 42 @>
    static member Delegate() : Func<int> = Func<int>(fun () -> 42)
    static member Echo(value: Expr<int>) : Expr<int> = value
