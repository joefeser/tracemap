Namespace TraceMap.CompiledFixtures.Equivalence
    Public Class NestedMatrix(Of TOuter)
        Public Class Inner(Of TInner)
            Public Shared Function Outer(value As TOuter) As TOuter
                Return value
            End Function
            Public Shared Function InnerValue(value As TInner) As TInner
                Return value
            End Function
            Public Shared Function Method(Of TMethod)(value As TMethod) As TMethod
                Return value
            End Function
            Public Shared Function Construct(value As NestedMatrix(Of Integer).Inner(Of String)) As NestedMatrix(Of Integer).Inner(Of String)
                Return value
            End Function
            Public Shared Function Swap(value As NestedMatrix(Of String).Inner(Of Integer)) As NestedMatrix(Of String).Inner(Of Integer)
                Return value
            End Function
        End Class
        Public Class Plain
            Public Shared Function Outer(value As TOuter) As TOuter
                Return value
            End Function
        End Class
    End Class
End Namespace
