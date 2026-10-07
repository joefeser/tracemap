Namespace TraceMap.CompiledFixtures.Equivalence
    Public Class NarrowingMatrix
        Public Shared Function AcceptInteger(value As Integer) As Integer
            Return value
        End Function
        Public Shared Function FromLong(value As Long) As Integer
            Return AcceptInteger(CInt(value))
        End Function
        Public Shared Function FromInteger(value As Integer) As Integer
            Return AcceptInteger(value)
        End Function
    End Class
End Namespace
