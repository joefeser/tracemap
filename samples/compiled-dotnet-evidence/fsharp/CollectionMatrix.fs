namespace TraceMap.CompiledFixtures.Equivalence

open System.Collections
open System.Collections.Generic

type CollectionMatrix() =
    static member ReadLegacy(values: ArrayList, index: int) : obj = values.[index]
    static member ReadGeneric(values: List<obj>, index: int) : obj = values.[index]
    static member ReadInteger(values: ArrayList, index: int) : int = unbox<int> values.[index]
