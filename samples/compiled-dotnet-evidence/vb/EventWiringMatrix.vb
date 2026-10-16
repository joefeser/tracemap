Namespace TraceMap.CompiledFixtures.Equivalence
    Public Class EventEmitter
        Public Event Tick As Action
        Public Sub Raise()
            RaiseEvent Tick()
        End Sub
    End Class

    Public Class EventSubscriber
        Public WithEvents Source As EventEmitter
        Private Sub OnTick() Handles Source.Tick
        End Sub
    End Class
End Namespace
