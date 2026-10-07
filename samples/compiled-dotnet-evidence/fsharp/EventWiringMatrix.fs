namespace TraceMap.CompiledFixtures.Equivalence

open System

[<AllowNullLiteral>]
type EventEmitter() =
    let tick = DelegateEvent<Action>()
    [<CLIEvent>]
    member _.Tick = tick.Publish
    member _.Raise() = tick.Trigger([||])

type EventSubscriber() as this =
    let mutable source: EventEmitter = null
    let handler = Action(this.OnTick)
    member private _.OnTick() = ()
    member this.Source
        with get() = source
        and set(value) =
            if not (isNull source) then
                source.remove_Tick(handler)
            source <- value
            if not (isNull source) then
                source.add_Tick(handler)
