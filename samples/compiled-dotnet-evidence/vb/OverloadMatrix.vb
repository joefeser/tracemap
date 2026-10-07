Namespace TraceMap.CompiledFixtures.Equivalence
    Public Class OverloadMatrix
        Public Shared Function SelectValue(value As String) As Integer
            Return 1
        End Function
        Public Shared Function SelectValue(value As System.Uri) As Integer
            Return 2
        End Function
        Public Shared Function FromString() As Integer
            Return SelectValue(DirectCast(Nothing, String))
        End Function
        Public Shared Function FromUri() As Integer
            Return SelectValue(DirectCast(Nothing, System.Uri))
        End Function
    End Class
End Namespace
