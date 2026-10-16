Namespace TraceMap.CompiledFixtures.Equivalence
    ' Ordinary factories with union-looking names; not discriminated union cases.
    Public NotInheritable Class UnionMatrix
        Public Shared ReadOnly Property Ready As UnionMatrix
            Get
                Return New UnionMatrix()
            End Get
        End Property
        Public Shared Function NewFailed(code As Integer) As UnionMatrix
            Return New UnionMatrix()
        End Function
    End Class
End Namespace
