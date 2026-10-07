Namespace TraceMap.CompiledFixtures.Equivalence
    Public Class ReceiverBase
        Public Overridable Function Read() As Integer
            Return 1
        End Function
    End Class

    Public Class ReceiverDerived
        Inherits ReceiverBase
        Public Overrides Function Read() As Integer
            Return 2
        End Function
        Public Function InvokeVirtual() As Integer
            Return Me.Read()
        End Function
        Public Function InvokeBase() As Integer
            Return MyBase.Read()
        End Function
        Public Function InvokeCurrent() As Integer
            Return MyClass.Read()
        End Function
    End Class
End Namespace
