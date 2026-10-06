Namespace TraceMap.CompiledFixtures.Equivalence
    ' CLR-RECORD-002: ordinary class with matching signatures, not a record.
    Public NotInheritable Class RecordMatrix
        Public ReadOnly Property Count As Integer
        Public Sub New(value As Integer)
            Count = value
        End Sub
        Public Overrides Function Equals(value As Object) As Boolean
            Return False
        End Function
        Public Overrides Function GetHashCode() As Integer
            Return Count
        End Function
        Public Overrides Function ToString() As String
            Return "ordinary"
        End Function
    End Class
End Namespace
