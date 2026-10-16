Option Strict Off

Namespace TraceMap.CompiledFixtures.Equivalence
    Public Class LateBindingTarget
        Public Function First() As Object
            Return Nothing
        End Function
        Public Function Second() As Object
            Return Nothing
        End Function
    End Class

    Public Class LateBindingMatrix
        Public Shared Function ReadFirst(value As Object) As Object
            Return value.First()
        End Function
        Public Shared Function ReadSecond(value As Object) As Object
            Return value.Second()
        End Function
        Public Shared Function ReadDirect(value As LateBindingTarget) As Object
            Return value.First()
        End Function
    End Class
End Namespace
